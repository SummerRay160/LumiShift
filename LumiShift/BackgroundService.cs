using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Windows.Forms;
using LumiShift.Infrastructure;
using LumiShift.Models;
using LumiShift.Resources;
using LumiShift.Services;
using Microsoft.Win32;

namespace LumiShift
{
    public class BackgroundService : IDisposable
    {
        internal UserSettings Settings { get; }
        internal GammaController GammaController { get; }
        internal MonitorManager MonitorManager { get; }

        private Timer _scheduleTimer;
        private string _lastScheduleMode;
        private bool _scheduleManualOverride;
        private bool _preScheduleGammaEnabled;
        private double _preScheduleGammaRScale;
        private double _preScheduleGammaGScale;
        private double _preScheduleGammaBScale;
        private double _preScheduleGammaValue;
        private int _preScheduleMasterBrightness;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private Form1 _mainForm;
        private MessageWindow _messageWindow;
        private System.ComponentModel.IContainer _components;
        private bool _disposed;
        private bool _exiting;
        private bool _trayMenuNeedsRebuild;

        internal bool IsExiting => _exiting;
        internal bool ScheduleManualOverride => _scheduleManualOverride;
        private bool _lightweightMode;
        private Timer _lightweightGcTimer;

        private const int ScheduleTimerIntervalNormal = 30000;
        private const int ScheduleTimerIntervalLightweight = 120000;

        private ToolStripMenuItem _trayGammaItem;
        private ToolStripMenuItem _trayQuickMenu;
        private ToolStripMenuItem _trayAllMonitorsItem;
        private ToolStripMenuItem _trayRestoreItem;
        private Timer _microGcTimer;
        private int _lightweightGcTickCount;
        private const int LightweightGcMs = 30000;
        private const int FullCompactEveryNTicks = 10;

        private struct ParsedSegment
        {
            public TimeSpan Start;
            public TimeSpan End;
            public string PresetName;
            public Dictionary<string, string> MonitorPresets;
        }

        private List<ParsedSegment> _parsedSegments;
        private int _parsedSegmentsHash;

        public event Action MonitorsChanged;
        public event Action ScheduleStateChanged;

        private static Icon LoadAppIcon()
        {
            return Program.AppIcon;
        }

        public BackgroundService()
        {
            Settings = SettingsStore.LoadSettings();
            GammaController = new GammaController();
            MonitorManager = new MonitorManager();

            MonitorManager.MonitorsChanged += OnMonitorsChangedInternal;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            _lastScheduleMode = "";
            _scheduleTimer = new Timer { Interval = ScheduleTimerIntervalNormal };
            _scheduleTimer.Tick += ScheduleTimer_Tick;

            if (Settings.ScheduleEnabled)
            {
                _scheduleTimer.Start();
                ScheduleTimer_Tick(null, null);
            }

            ApplyGammaToSystem();
            CreateTrayIcon();
            _trayMenuNeedsRebuild = true;
            RebuildTrayMenu();

            if (Settings.EyeProtectionEnabled)
            {
                EyeProtectionService.ApplyColor(
                    Settings.EyeProtectionRed,
                    Settings.EyeProtectionGreen,
                    Settings.EyeProtectionBlue);
            }

            ThemeManager.CurrentMode = (ThemeMode)Settings.ThemeMode;
            ThemeManager.UpdateActiveTheme();
            ThemeManager.StartWatchingSystemTheme();

            _messageWindow = new MessageWindow(this);

            var updateTimer = new Timer { Interval = 3000 };
            updateTimer.Tick += (s, e) =>
            {
                updateTimer.Stop();
                updateTimer.Dispose();
                UpdateService.CheckForUpdate(silent: true);
            };
            updateTimer.Start();
        }

        #region Tray Icon

        private void CreateTrayIcon()
        {
            _components = new System.ComponentModel.Container();
            _trayIcon = new NotifyIcon(_components)
            {
                Text = "LumiShift",
                Icon = LoadAppIcon(),
                Visible = true
            };
            _trayMenu = new ContextMenuStrip(_components);
            _trayMenu.Opening += OnTrayMenuOpening;
            _trayMenu.Closed += OnTrayMenuClosed;
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void OnTrayMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void OnTrayMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (_trayMenuNeedsRebuild)
            {
                _trayMenuNeedsRebuild = false;
                RebuildTrayMenu();
            }
        }

        internal void UpdateTrayMenu()
        {
            if (!Form1IsOpen())
            {
                RefreshDynamicTraySection();
                ScheduleMicroGc();
            }
            else
            {
                _trayMenuNeedsRebuild = true;
            }
        }

        private void RebuildTrayMenu()
        {
            if (_trayMenu == null) return;

            if (_trayGammaItem == null)
            {
                for (int i = _trayMenu.Items.Count - 1; i >= 0; i--)
                {
                    var item = _trayMenu.Items[i];
                    _trayMenu.Items.RemoveAt(i);
                    RecursiveDispose(item);
                }
                _trayMenu.Items.Clear();
                BuildDynamicTraySection();
                BuildStaticTraySection();
            }
            else
            {
                ClearDynamicTraySection();
                BuildDynamicTraySection();
            }
        }

        private void ClearDynamicTraySection()
        {
            if (_trayRestoreItem != null)
            {
                _trayMenu.Items.Remove(_trayRestoreItem);
                RecursiveDispose(_trayRestoreItem);
                _trayRestoreItem = null;
            }
            if (_trayQuickMenu != null)
            {
                _trayMenu.Items.Remove(_trayQuickMenu);
                RecursiveDispose(_trayQuickMenu);
                _trayQuickMenu = null;
                _trayAllMonitorsItem = null;
            }
            if (_trayGammaItem != null)
            {
                _trayMenu.Items.Remove(_trayGammaItem);
                _trayGammaItem.Dispose();
                _trayGammaItem = null;
            }
        }

        private void BuildDynamicTraySection()
        {
            _trayGammaItem = new ToolStripMenuItem(
                GammaController.IsSupported && Settings.GammaEnabled
                    ? "Gamma 校正: 已启用"
                    : "Gamma 校正: 已禁用")
            {
                Checked = Settings.GammaEnabled
            };
            _trayGammaItem.Click += (s, e) => GammaTrayToggle();
            _trayMenu.Items.Add(_trayGammaItem);

            _trayQuickMenu = new ToolStripMenuItem("快速切换预设");

            BuildAllMonitorsSubMenu();

            bool anyMonitorOverride = Settings.GammaPerDisplay != null && Settings.GammaPerDisplay.Count > 0;

            if (MonitorManager.Monitors.Count > 1 || anyMonitorOverride)
            {
                _trayQuickMenu.DropDownItems.Add(new ToolStripSeparator());
                foreach (var monitor in MonitorManager.Monitors)
                {
                    BuildSingleMonitorSubMenu(monitor.DeviceId, monitor.DisplayName);
                }
            }

            _trayMenu.Items.Add(_trayQuickMenu);

            if (Settings.ScheduleEnabled && _scheduleManualOverride)
            {
                _trayRestoreItem = new ToolStripMenuItem("恢复定时控制", null, (s, ev) =>
                {
                    _scheduleManualOverride = false;
                    ScheduleTimer_Tick(null, null);
                    UpdateTrayMenu();
                    ScheduleStateChanged?.Invoke();
                });
                _trayMenu.Items.Add(_trayRestoreItem);
            }

            UpdateTrayText();
        }

        private void BuildAllMonitorsSubMenu()
        {
            bool anyMonitorOverride = Settings.GammaPerDisplay != null && Settings.GammaPerDisplay.Count > 0;
            string globalPresetName = GetCurrentPresetName();

            _trayAllMonitorsItem = new ToolStripMenuItem("全部显示器");
            foreach (var p in PresetDefinitions.GetNames())
            {
                bool isActive = !anyMonitorOverride && Settings.GammaEnabled && globalPresetName == p;
                var item = new ToolStripMenuItem(p) { Checked = isActive };
                string cp = p;
                item.Click += (s, ev) => QuickPreset(cp);
                _trayAllMonitorsItem.DropDownItems.Add(item);
            }
            if (Settings.CustomGammaPresets.Count > 0)
            {
                _trayAllMonitorsItem.DropDownItems.Add(new ToolStripSeparator());
                foreach (var cp in Settings.CustomGammaPresets)
                {
                    bool isActive = !anyMonitorOverride && Settings.GammaEnabled && globalPresetName == cp.Name;
                    var item = new ToolStripMenuItem(cp.Name) { Checked = isActive };
                    string name = cp.Name;
                    item.Click += (s, ev) => QuickPreset(name);
                    _trayAllMonitorsItem.DropDownItems.Add(item);
                }
            }
            _trayQuickMenu.DropDownItems.Add(_trayAllMonitorsItem);
        }

        private void BuildSingleMonitorSubMenu(string deviceId, string displayName)
        {
            string monitorLabel = displayName;
            if (Settings.GammaPerDisplay.ContainsKey(deviceId))
                monitorLabel += $" ({GetMonitorPresetName(deviceId)})";

            var monitorItem = new ToolStripMenuItem(monitorLabel);
            string currentMonitorPreset = GetMonitorPresetName(deviceId);

            foreach (var p in PresetDefinitions.GetNames())
            {
                bool isActive = currentMonitorPreset == p;
                var item = new ToolStripMenuItem(p) { Checked = isActive };
                string presetName = p;
                string monDeviceId = deviceId;
                item.Click += (s, ev) => ApplyPresetToMonitor(presetName, monDeviceId);
                monitorItem.DropDownItems.Add(item);
            }

            if (Settings.CustomGammaPresets.Count > 0)
            {
                monitorItem.DropDownItems.Add(new ToolStripSeparator());
                foreach (var cp in Settings.CustomGammaPresets)
                {
                    bool isActive = currentMonitorPreset == cp.Name;
                    var item = new ToolStripMenuItem(cp.Name) { Checked = isActive };
                    string presetName = cp.Name;
                    string monDeviceId = deviceId;
                    item.Click += (s, ev) => ApplyPresetToMonitor(presetName, monDeviceId);
                    monitorItem.DropDownItems.Add(item);
                }
            }

            _trayQuickMenu.DropDownItems.Add(monitorItem);
        }

        private void BuildStaticTraySection()
        {
            _trayMenu.Items.Add(new ToolStripSeparator());

            var checkUpdateItem = new ToolStripMenuItem("检查更新", null, (s, ev) => UpdateService.CheckForUpdate(silent: false));
            _trayMenu.Items.Add(checkUpdateItem);

            var showItem = new ToolStripMenuItem("显示主界面", null, (s, ev) => ShowMainWindow());
            _trayMenu.Items.Add(showItem);

            var powerItem = new ToolStripMenuItem("关闭显示器", null, (s, ev) => TurnOffMonitor());
            _trayMenu.Items.Add(powerItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem("退出", null, (s, ev) => ExitApplication());
            _trayMenu.Items.Add(exitItem);
        }

        private void RefreshDynamicTraySection()
        {
            if (_trayGammaItem == null) return;

            bool gammaSupported = GammaController.IsSupported;
            _trayGammaItem.Text = gammaSupported && Settings.GammaEnabled
                ? "Gamma 校正: 已启用"
                : "Gamma 校正: 已禁用";
            _trayGammaItem.Checked = Settings.GammaEnabled;

            RefreshAllMonitorsSubMenu();

            bool hasRestoreItem = _trayRestoreItem != null;
            bool needsRestoreItem = Settings.ScheduleEnabled && _scheduleManualOverride;

            if (needsRestoreItem && !hasRestoreItem)
            {
                _trayRestoreItem = new ToolStripMenuItem("恢复定时控制", null, (s, ev) =>
                {
                    _scheduleManualOverride = false;
                    ScheduleTimer_Tick(null, null);
                    UpdateTrayMenu();
                    ScheduleStateChanged?.Invoke();
                });
                int restoreIndex = _trayMenu.Items.IndexOf(_trayQuickMenu) + 1;
                _trayMenu.Items.Insert(restoreIndex, _trayRestoreItem);
            }
            else if (!needsRestoreItem && hasRestoreItem)
            {
                _trayMenu.Items.Remove(_trayRestoreItem);
                _trayRestoreItem.Dispose();
                _trayRestoreItem = null;
            }

            UpdateTrayText();
        }

        private void RefreshAllMonitorsSubMenu()
        {
            if (_trayAllMonitorsItem == null) return;

            bool anyMonitorOverride = Settings.GammaPerDisplay != null && Settings.GammaPerDisplay.Count > 0;
            string globalPresetName = GetCurrentPresetName();

            foreach (ToolStripItem item in _trayAllMonitorsItem.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && item != null && !(item is ToolStripSeparator))
                {
                    string presetName = menuItem.Text;
                    bool shouldCheck = !anyMonitorOverride && Settings.GammaEnabled && globalPresetName == presetName;
                    menuItem.Checked = shouldCheck;
                }
            }
        }

        private static void RecursiveDispose(ToolStripItem item)
        {
            if (item is ToolStripMenuItem menuItem)
            {
                while (menuItem.DropDownItems.Count > 0)
                {
                    var subItem = menuItem.DropDownItems[0];
                    menuItem.DropDownItems.RemoveAt(0);
                    RecursiveDispose(subItem);
                }
            }
            item.Dispose();
        }

        private void UpdateTrayText()
        {
            if (_trayIcon == null) return;

            if (!Settings.ScheduleEnabled)
            {
                _trayIcon.Text = "LumiShift";
                return;
            }

            string currentPreset = GetCurrentPresetName() ?? "自定义";
            if (_scheduleManualOverride)
            {
                string nextInfo = GetNextScheduleInfo();
                string overrideText = string.IsNullOrEmpty(nextInfo)
                    ? "手动调整"
                    : $"手动调整 ({nextInfo}恢复)";
                _trayIcon.Text = $"LumiShift - {overrideText}";
            }
            else
            {
                _trayIcon.Text = $"LumiShift - 定时: {currentPreset}";
            }

            if (_trayIcon.Text.Length > 127)
                _trayIcon.Text = _trayIcon.Text.Substring(0, 127);
        }

        #endregion

        #region Preset Helpers

        internal string GetCurrentPresetName()
        {
            if (!Settings.GammaEnabled)
                return PresetDefinitions.BuiltIns[0].Name;

            foreach (var bip in PresetDefinitions.BuiltIns)
            {
                if (bip.Matches(Settings.GammaRScale, Settings.GammaGScale,
                    Settings.GammaBScale, Settings.GammaValue, Settings.MasterBrightness))
                    return bip.Name;
            }

            foreach (var cp in Settings.CustomGammaPresets)
            {
                if (Math.Abs(Settings.GammaRScale - cp.RScale) < 0.01 &&
                    Math.Abs(Settings.GammaGScale - cp.GScale) < 0.01 &&
                    Math.Abs(Settings.GammaBScale - cp.BScale) < 0.01 &&
                    Math.Abs(Settings.GammaValue - cp.GammaValue) < 0.01 &&
                    Math.Abs(Settings.MasterBrightness - cp.MasterBrightness) <= 1)
                    return cp.Name;
            }

            return null;
        }

        internal string GetMonitorPresetName(string deviceId)
        {
            if (Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
            {
                if (!pdg.Enabled) return PresetDefinitions.BuiltIns[0].Name;

                foreach (var bip in PresetDefinitions.BuiltIns)
                {
                    if (bip.Matches(pdg.RScale, pdg.GScale, pdg.BScale, pdg.GammaValue, pdg.MasterBrightness))
                        return bip.Name;
                }
                foreach (var cp in Settings.CustomGammaPresets)
                {
                    if (Math.Abs(pdg.RScale - cp.RScale) < 0.01 &&
                        Math.Abs(pdg.GScale - cp.GScale) < 0.01 &&
                        Math.Abs(pdg.BScale - cp.BScale) < 0.01 &&
                        Math.Abs(pdg.GammaValue - cp.GammaValue) < 0.01 &&
                        Math.Abs(pdg.MasterBrightness - cp.MasterBrightness) <= 1)
                        return cp.Name;
                }
            }
            return GetCurrentPresetName();
        }

        internal bool TryApplyPreset(string name)
        {
            var builtIn = PresetDefinitions.GetByName(name);
            if (builtIn != null)
            {
                Settings.GammaEnabled = builtIn.Enabled;
                Settings.GammaRScale = builtIn.RScale;
                Settings.GammaGScale = builtIn.GScale;
                Settings.GammaBScale = builtIn.BScale;
                Settings.GammaValue = builtIn.GammaValue;
                Settings.MasterBrightness = builtIn.MasterBrightness;
                return true;
            }

            var custom = Settings.CustomGammaPresets.FirstOrDefault(cp => cp.Name == name);
            if (custom != null)
            {
                Settings.GammaEnabled = custom.Enabled;
                Settings.GammaRScale = custom.RScale;
                Settings.GammaGScale = custom.GScale;
                Settings.GammaBScale = custom.BScale;
                Settings.GammaValue = custom.GammaValue;
                Settings.MasterBrightness = custom.MasterBrightness;

                if (custom.PerDisplaySnapshot != null && custom.PerDisplaySnapshot.Count > 0)
                {
                    Settings.GammaPerDisplay.Clear();
                    foreach (var kvp in custom.PerDisplaySnapshot)
                    {
                        Settings.GammaPerDisplay[kvp.Key] = new PerDisplayGamma
                        {
                            RScale = kvp.Value.RScale,
                            GScale = kvp.Value.GScale,
                            BScale = kvp.Value.BScale,
                            GammaValue = kvp.Value.GammaValue,
                            MasterBrightness = kvp.Value.MasterBrightness,
                            Enabled = kvp.Value.Enabled,
                            Source = "manual"
                        };
                    }
                }

                return true;
            }
            return false;
        }

        internal void ApplyPresetToMonitor(string presetName, string deviceId)
        {
            double r, g, b, gv;
            int mb;
            bool en;

            if (!PresetDefinitions.TryResolveParams(presetName, Settings.CustomGammaPresets,
                out r, out g, out b, out gv, out mb, out en))
                return;

            if (!Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
            {
                pdg = new PerDisplayGamma();
                Settings.GammaPerDisplay[deviceId] = pdg;
            }
            pdg.RScale = r; pdg.GScale = g; pdg.BScale = b;
            pdg.GammaValue = gv; pdg.MasterBrightness = mb; pdg.Enabled = en;
            pdg.Source = "manual";

            if (Settings.ScheduleEnabled)
                _scheduleManualOverride = true;

            ApplyGammaToSystem();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(Settings);
            ScheduleStateChanged?.Invoke();
        }

        internal void QuickPreset(string presetName)
        {
            if (Settings.ScheduleEnabled)
                _scheduleManualOverride = true;
            TryApplyPreset(presetName);
            ApplyGammaToSystem();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(Settings);
            ScheduleStateChanged?.Invoke();
        }

        internal void GammaTrayToggle()
        {
            Settings.GammaEnabled = !Settings.GammaEnabled;
            if (Settings.ScheduleEnabled)
                _scheduleManualOverride = true;
            ApplyGammaToSystem();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(Settings);
            ScheduleStateChanged?.Invoke();
        }

        #endregion

        #region Gamma

        internal void ApplyGammaToSystem()
        {
            bool hasOverrides = Settings.GammaPerDisplay != null &&
                                Settings.GammaPerDisplay.Count > 0;

            if (!hasOverrides)
            {
                if (Settings.GammaEnabled)
                {
                    var parameters = new GammaParameters(
                        Settings.GammaRScale,
                        Settings.GammaGScale,
                        Settings.GammaBScale,
                        Settings.GammaValue,
                        Settings.MasterBrightness);
                    GammaController.ApplyGamma(Screen.AllScreens, parameters);
                }
                else
                {
                    GammaController.ResetGamma(Screen.AllScreens);
                }
                return;
            }

            var perScreenParams = new Dictionary<string, GammaParameters>();
            var coveredDeviceNames = new HashSet<string>();

            foreach (var monitor in MonitorManager.Monitors)
            {
                var screen = monitor.Screen;
                if (screen == null) continue;

                coveredDeviceNames.Add(screen.DeviceName);

                if (Settings.GammaPerDisplay.TryGetValue(monitor.DeviceId, out var overrideGamma))
                {
                    if (overrideGamma.Enabled)
                    {
                        perScreenParams[screen.DeviceName] = new GammaParameters(
                            overrideGamma.RScale,
                            overrideGamma.GScale,
                            overrideGamma.BScale,
                            overrideGamma.GammaValue,
                            overrideGamma.MasterBrightness);
                    }
                }
                else
                {
                    if (Settings.GammaEnabled)
                    {
                        perScreenParams[screen.DeviceName] = new GammaParameters(
                            Settings.GammaRScale,
                            Settings.GammaGScale,
                            Settings.GammaBScale,
                            Settings.GammaValue,
                            Settings.MasterBrightness);
                    }
                }
            }

            foreach (Screen screen in Screen.AllScreens)
            {
                if (coveredDeviceNames.Contains(screen.DeviceName)) continue;
                if (Settings.GammaEnabled)
                {
                    perScreenParams[screen.DeviceName] = new GammaParameters(
                        Settings.GammaRScale,
                        Settings.GammaGScale,
                        Settings.GammaBScale,
                        Settings.GammaValue,
                        Settings.MasterBrightness);
                }
            }

            GammaController.ApplyGammaPerScreen(perScreenParams);
        }

        #endregion

        #region Schedule

        internal void ScheduleTimer_Tick(object sender, EventArgs e)
        {
            if (!Settings.ScheduleEnabled) return;

            try
            {
                var current = DateTime.Now.TimeOfDay;

                string targetMode = null;
                ScheduleSegment targetSegment = null;

                EnsureParsedSegments();

                foreach (var parsed in _parsedSegments)
                {
                    if (parsed.Start == parsed.End) continue;

                    bool inSegment;
                    if (parsed.Start < parsed.End)
                        inSegment = current >= parsed.Start && current < parsed.End;
                    else
                        inSegment = current >= parsed.Start || current < parsed.End;

                    if (inSegment)
                    {
                        targetMode = parsed.PresetName;
                        targetSegment = FindSegmentByPresetName(parsed.PresetName);
                        break;
                    }
                }

                if (targetMode == null) return;

                if (_scheduleManualOverride)
                {
                    if (targetMode == _lastScheduleMode)
                        return;

                    _scheduleManualOverride = false;
                }

                if (targetMode == _lastScheduleMode) return;

                var savedR = Settings.GammaRScale;
                var savedG = Settings.GammaGScale;
                var savedB = Settings.GammaBScale;
                var savedV = Settings.GammaValue;
                var savedE = Settings.GammaEnabled;
                var savedM = Settings.MasterBrightness;

                bool applied = TryApplyPreset(targetMode);

                if (!applied)
                {
                    Settings.GammaRScale = savedR;
                    Settings.GammaGScale = savedG;
                    Settings.GammaBScale = savedB;
                    Settings.GammaValue = savedV;
                    Settings.GammaEnabled = savedE;
                    Settings.MasterBrightness = savedM;
                    _lastScheduleMode = targetMode;
                    return;
                }

                bool changed = Math.Abs(Settings.GammaRScale - savedR) > 0.001 ||
                               Math.Abs(Settings.GammaGScale - savedG) > 0.001 ||
                               Math.Abs(Settings.GammaBScale - savedB) > 0.001 ||
                               Math.Abs(Settings.GammaValue - savedV) > 0.001 ||
                               Settings.GammaEnabled != savedE ||
                               Settings.MasterBrightness != savedM;

                if (!changed)
                {
                    Settings.GammaRScale = savedR;
                    Settings.GammaGScale = savedG;
                    Settings.GammaBScale = savedB;
                    Settings.GammaValue = savedV;
                    Settings.GammaEnabled = savedE;
                    Settings.MasterBrightness = savedM;
                    _lastScheduleMode = targetMode;
                    return;
                }

                ApplyGammaToSystem();
                SettingsStore.SaveSettings(Settings);
                UpdateTrayMenu();
                _lastScheduleMode = targetMode;

                ApplyScheduleMonitorPresets(targetSegment);

                ScheduleStateChanged?.Invoke();
            }
            catch
            {
            }
        }

        private void ApplyScheduleMonitorPresets(ScheduleSegment segment)
        {
            if (segment == null) return;

            if (segment.SyncMode != false)
            {
                Settings.GammaPerDisplay.Clear();
                ApplyGammaToSystem();
                SettingsStore.SaveSettings(Settings);
                return;
            }

            if (segment.MonitorPresets == null || segment.MonitorPresets.Count == 0)
                return;

            foreach (var monitor in MonitorManager.Monitors)
            {
                if (!segment.MonitorPresets.TryGetValue(monitor.DeviceId, out var presetName))
                    continue;

                double r, g, b, gv;
                int mb;
                bool en;

                if (!PresetDefinitions.TryResolveParams(presetName, Settings.CustomGammaPresets,
                    out r, out g, out b, out gv, out mb, out en))
                    continue;

                if (!Settings.GammaPerDisplay.TryGetValue(monitor.DeviceId, out var pdg))
                {
                    pdg = new PerDisplayGamma();
                    Settings.GammaPerDisplay[monitor.DeviceId] = pdg;
                }
                pdg.RScale = r; pdg.GScale = g; pdg.BScale = b;
                pdg.GammaValue = gv; pdg.MasterBrightness = mb; pdg.Enabled = en;
                pdg.Source = "schedule";
            }

            ApplyGammaToSystem();
            SettingsStore.SaveSettings(Settings);
        }

        internal string GetNextScheduleInfo()
        {
            if (Settings.ScheduleSegments == null || Settings.ScheduleSegments.Count == 0)
                return "";

            var current = DateTime.Now.TimeOfDay;
            EnsureParsedSegments();

            ScheduleSegment nextSegment = null;
            TimeSpan minDiff = TimeSpan.MaxValue;

            for (int i = 0; i < _parsedSegments.Count; i++)
            {
                var parsed = _parsedSegments[i];
                var start = parsed.Start;

                TimeSpan diff;
                if (start > current)
                    diff = start - current;
                else
                    diff = TimeSpan.FromHours(24) - (current - start);

                if (diff < minDiff)
                {
                    minDiff = diff;
                    nextSegment = Settings.ScheduleSegments[i];
                }
            }

            if (nextSegment == null) return "";

            if (minDiff.TotalHours < 1)
                return $"{(int)minDiff.TotalMinutes}分钟后";
            return $"{(int)minDiff.TotalHours}小时{(int)minDiff.Minutes}分钟后";
        }

        private void EnsureParsedSegments()
        {
            if (Settings.ScheduleSegments == null)
            {
                _parsedSegments = null;
                _parsedSegmentsHash = 0;
                return;
            }

            int hash = Settings.ScheduleSegments.Count;
            foreach (var s in Settings.ScheduleSegments)
            {
                if (s != null)
                {
                    hash ^= (s.StartTime ?? "").GetHashCode();
                    hash ^= (s.EndTime ?? "").GetHashCode();
                    hash ^= (s.PresetName ?? "").GetHashCode();
                }
            }

            if (_parsedSegments != null && _parsedSegmentsHash == hash)
                return;

            _parsedSegmentsHash = hash;
            _parsedSegments = new List<ParsedSegment>(Settings.ScheduleSegments.Count);

            foreach (var segment in Settings.ScheduleSegments)
            {
                if (segment == null) continue;
                var startParts = segment.StartTime?.Split(':') ?? new[] { "0", "0" };
                var endParts = segment.EndTime?.Split(':') ?? new[] { "0", "0" };
                if (startParts.Length < 2 || endParts.Length < 2) continue;

                var start = new TimeSpan(int.Parse(startParts[0]), int.Parse(startParts[1]), 0);
                var end = new TimeSpan(int.Parse(endParts[0]), int.Parse(endParts[1]), 0);

                _parsedSegments.Add(new ParsedSegment
                {
                    Start = start,
                    End = end,
                    PresetName = segment.PresetName,
                    MonitorPresets = segment.MonitorPresets
                });
            }
        }

        private ScheduleSegment FindSegmentByPresetName(string presetName)
        {
            if (Settings.ScheduleSegments == null) return null;
            foreach (var segment in Settings.ScheduleSegments)
            {
                if (segment.PresetName == presetName)
                    return segment;
            }
            return null;
        }

        internal void OnScheduleSegmentChanged()
        {
            _parsedSegments = null;
            _parsedSegmentsHash = 0;
            if (Settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);
            }
            SettingsStore.SaveSettings(Settings);
        }

        internal void SetScheduleEnabled(bool enabled)
        {
            Settings.ScheduleEnabled = enabled;
            _scheduleTimer.Enabled = enabled;
            if (enabled)
            {
                _preScheduleGammaEnabled = Settings.GammaEnabled;
                _preScheduleGammaRScale = Settings.GammaRScale;
                _preScheduleGammaGScale = Settings.GammaGScale;
                _preScheduleGammaBScale = Settings.GammaBScale;
                _preScheduleGammaValue = Settings.GammaValue;
                _preScheduleMasterBrightness = Settings.MasterBrightness;

                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);
            }
            else
            {
                var scheduleKeys = Settings.GammaPerDisplay
                    .Where(kvp => kvp.Value.Source == "schedule")
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var key in scheduleKeys)
                    Settings.GammaPerDisplay.Remove(key);

                Settings.GammaEnabled = _preScheduleGammaEnabled;
                Settings.GammaRScale = _preScheduleGammaRScale;
                Settings.GammaGScale = _preScheduleGammaGScale;
                Settings.GammaBScale = _preScheduleGammaBScale;
                Settings.GammaValue = _preScheduleGammaValue;
                Settings.MasterBrightness = _preScheduleMasterBrightness;

                ApplyGammaToSystem();
                ScheduleStateChanged?.Invoke();
            }
            SettingsStore.SaveSettings(Settings);
            UpdateTrayMenu();
        }

        internal void SetScheduleManualOverride(bool value)
        {
            _scheduleManualOverride = value;
            UpdateTrayMenu();
        }

        #endregion

        #region System Events

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (Form1IsOpen() && _mainForm.InvokeRequired)
            {
                try { _mainForm.Invoke(new Action(() => HandleDisplayChange())); }
                catch { HandleDisplayChange(); }
            }
            else
            {
                HandleDisplayChange();
            }
        }

        private void OnMonitorsChangedInternal()
        {
            if (Form1IsOpen() && _mainForm.InvokeRequired)
            {
                try { _mainForm.Invoke(new Action(() => MonitorsChanged?.Invoke())); }
                catch { MonitorsChanged?.Invoke(); }
            }
            else
            {
                MonitorsChanged?.Invoke();
            }
        }

        private void HandleDisplayChange()
        {
            var removedIds = MonitorManager.RefreshMonitors();
            CleanupStaleSettings(removedIds);
            if (!Form1IsOpen())
            {
                ClearDynamicTraySection();
                BuildDynamicTraySection();
                ScheduleMicroGc();
            }
        }

        private void CleanupStaleSettings(HashSet<string> removedDeviceIds)
        {
            if (removedDeviceIds == null || removedDeviceIds.Count == 0)
                return;

            foreach (var id in removedDeviceIds)
            {
                Settings.BrightnessPerDisplay.Remove(id);
                Settings.GammaPerDisplay.Remove(id);
            }

            if (Settings.ScheduleSegments != null)
            {
                foreach (var segment in Settings.ScheduleSegments)
                {
                    if (segment.MonitorPresets != null)
                    {
                        foreach (var id in removedDeviceIds)
                        {
                            segment.MonitorPresets.Remove(id);
                        }
                        if (segment.MonitorPresets.Count == 0)
                            segment.MonitorPresets = null;
                    }
                }
            }

            if (Settings.CustomGammaPresets != null)
            {
                foreach (var preset in Settings.CustomGammaPresets)
                {
                    if (preset.PerDisplaySnapshot != null)
                    {
                        foreach (var id in removedDeviceIds)
                        {
                            preset.PerDisplaySnapshot.Remove(id);
                        }
                        if (preset.PerDisplaySnapshot.Count == 0)
                            preset.PerDisplaySnapshot = null;
                    }
                }
            }

            SettingsStore.SaveSettings(Settings);
        }

        #endregion

        #region Form Lifecycle

        private bool Form1IsOpen()
        {
            return _mainForm != null && !_mainForm.IsDisposed;
        }

        public void ShowMainWindow()
        {
            if (Form1IsOpen())
            {
                if (_mainForm.InvokeRequired)
                    _mainForm.Invoke(new Action(() => ActivateExistingForm()));
                else
                    ActivateExistingForm();
                return;
            }

            if (_lightweightMode)
            {
                _lightweightMode = false;
                _lightweightGcTimer?.Stop();
                _lightweightGcTimer?.Dispose();
                _lightweightGcTimer = null;
                _microGcTimer?.Stop();
                _microGcTimer?.Dispose();
                _microGcTimer = null;
                MonitorManager.ExitLightweightMode();
                if (_scheduleTimer != null)
                    _scheduleTimer.Interval = ScheduleTimerIntervalNormal;
                GcHelper.CollectFull();
            }

            _mainForm = new Form1(this);
            _mainForm.Show();
        }

        internal void OnFormClosing(Form1 form)
        {
            if (_mainForm == form)
            {
                _mainForm = null;
            }
        }

        internal void EnterAppLightweightMode()
        {
            if (_lightweightMode) return;
            _lightweightMode = true;
            Controls.GdiCache.Clear();
            GammaController.TrimCache();
            MonitorManager.EnterLightweightMode();
            if (_scheduleTimer != null)
                _scheduleTimer.Interval = ScheduleTimerIntervalLightweight;
            GcHelper.CollectFull();
            GcHelper.TrimWorkingSet();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GcHelper.TrimWorkingSet();

            _lightweightGcTickCount = 0;
            _lightweightGcTimer = new Timer { Interval = LightweightGcMs };
            _lightweightGcTimer.Tick += LightweightGcTimer_Tick;
            _lightweightGcTimer.Start();
        }

        private void LightweightGcTimer_Tick(object sender, EventArgs e)
        {
            _lightweightGcTickCount++;

            if (_lightweightGcTickCount % FullCompactEveryNTicks == 0)
            {
                GC.Collect(1, GCCollectionMode.Forced, false);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
                try
                {
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(2, GCCollectionMode.Forced, true, true);
                }
                catch { }
            }
            else
            {
                GC.Collect(0, GCCollectionMode.Forced);
            }

            GcHelper.TrimWorkingSet();
        }

        private void ScheduleMicroGc()
        {
            if (_microGcTimer != null) return;

            _microGcTimer = new Timer { Interval = 2000 };
            _microGcTimer.Tick += (s, e) =>
            {
                _microGcTimer?.Stop();
                _microGcTimer?.Dispose();
                _microGcTimer = null;
                GC.Collect(0, GCCollectionMode.Forced);
                GcHelper.TrimWorkingSet();
            };
            _microGcTimer.Start();
        }

        internal void ScheduleLightweightModeEntry()
        {
            var delayTimer = new Timer { Interval = 5000 };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                delayTimer.Dispose();
                EnterAppLightweightMode();
            };
            delayTimer.Start();
        }

        private void ActivateExistingForm()
        {
            _mainForm.Show();
            _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.ShowInTaskbar = true;
            _mainForm.Activate();
        }

        #endregion

        #region Other

        internal void UpdateStartupRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (Settings.StartWithWindows)
                    {
                        string path = $"\"{Application.ExecutablePath}\"";
                        if (Settings.StartMinimized) path += " --minimized";
                        key.SetValue("LumiShift", path);
                    }
                    else
                    {
                        if (key.GetValue("LumiShift") != null)
                            key.DeleteValue("LumiShift");
                    }
                }
            }
            catch { }
        }

        private void TurnOffMonitor()
        {
            NativeMethods.SendMessage(
                NativeMethods.HWND_BROADCAST,
                NativeMethods.WM_SYSCOMMAND,
                (IntPtr)NativeMethods.SC_MONITORPOWER,
                (IntPtr)2);
        }

        public void ExitApplication()
        {
            if (_exiting) return;
            _exiting = true;

            ThemeManager.StopWatchingSystemTheme();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            if (GammaController != null)
            {
                GammaController.ResetGamma(Screen.AllScreens);
                GammaController.Dispose();
            }

            if (MonitorManager != null)
            {
                MonitorManager.MonitorsChanged -= OnMonitorsChangedInternal;
                MonitorManager.Dispose();
            }

            _scheduleTimer?.Dispose();

            _lightweightGcTimer?.Stop();
            _lightweightGcTimer?.Dispose();
            _lightweightGcTimer = null;

            _microGcTimer?.Stop();
            _microGcTimer?.Dispose();
            _microGcTimer = null;

            Controls.GdiCache.Clear();

            if (Form1IsOpen())
            {
                try { _mainForm.Close(); }
                catch { }
                _mainForm = null;
            }

            _messageWindow?.DestroyHandle();
            _messageWindow = null;

            if (_trayMenu != null)
            {
                var items = new ToolStripItem[_trayMenu.Items.Count];
                _trayMenu.Items.CopyTo(items, 0);
                foreach (ToolStripItem item in items)
                    item.Dispose();
                _trayMenu.Items.Clear();
            }

            _trayIcon?.Dispose();
            _components?.Dispose();

            Application.Exit();
        }

        #endregion

        #region Message Window

        private class MessageWindow : NativeWindow
        {
            private readonly BackgroundService _service;

            public MessageWindow(BackgroundService service)
            {
                _service = service;
                CreateHandle(new CreateParams
                {
                    Caption = "LumiShiftMessageWindow",
                    Parent = new IntPtr(-3)
                });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_SHOW_LUMISHIFT)
                {
                    _service.ShowMainWindow();
                    return;
                }
                base.WndProc(ref m);
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ExitApplication();
        }
    }
}
