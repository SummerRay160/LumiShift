using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Threading;
using System.Windows.Forms;
using LumiShift.Infrastructure;
using LumiShift.Models;
using LumiShift.Resources;
using LumiShift.Services;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace LumiShift
{
    public class BackgroundService : IDisposable
    {
        internal UserSettings Settings { get; }
        internal GammaController GammaController { get; }

        private MonitorManager _monitorManager;
        internal MonitorManager MonitorManager
        {
            get
            {
                if (_monitorManager == null)
                {
                    _monitorManager = new MonitorManager();
                    _monitorManager.MonitorsChanged += OnMonitorsChangedInternal;
                }
                return _monitorManager;
            }
        }

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
        private WeakReference<Form1> _mainFormRef;
        private MessageWindow _messageWindow;
        private System.ComponentModel.IContainer _components;
        private bool _disposed;
        private bool _exiting;
        private bool _trayMenuNeedsRebuild;
        private bool _trayMenuOpen;
        private bool _trayClickInProgress;
        private System.Threading.CancellationTokenSource _updateCheckCts;
        private Timer _updateCheckTimer;
        private Timer _lightweightEntryTimer;
        private Timer _healthCheckTimer;

        internal bool IsExiting => _exiting;
        internal bool ScheduleManualOverride => _scheduleManualOverride;
        private bool _lightweightMode;
        private bool _displayChangedInLightweight;
        private bool _scheduleChangedInLightweight;
        private Timer _lightweightGcTimer;

        private Form1 MainForm
        {
            get
            {
                if (_mainFormRef == null) return null;
                if (!_mainFormRef.TryGetTarget(out var form) || form == null || form.IsDisposed)
                {
                    _mainFormRef = null;
                    return null;
                }
                return form;
            }
            set
            {
                if (value == null)
                    _mainFormRef = null;
                else
                    _mainFormRef = new WeakReference<Form1>(value);
            }
        }

        private const int ScheduleTimerIntervalNormal = 30000;
        private const int ScheduleTimerIntervalLightweight = 120000;

        private ToolStripMenuItem _trayGammaItem;
        private ToolStripMenuItem _trayQuickMenu;
        private ToolStripMenuItem _trayAllMonitorsItem;
        private ToolStripMenuItem _trayRestoreItem;
        private Timer _microGcTimer;
        private Timer _menuCleanupTimer;
        private int _lightweightGcTickCount;
        private const int LightweightGcMs = 30000;
        private const int FullCompactEveryNTicks = 20;
        private const int Gen1CollectEveryNTicks = 5;

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

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            _lastScheduleMode = "";
            _scheduleTimer = new Timer { Interval = ScheduleTimerIntervalNormal };
            _scheduleTimer.Tick += ScheduleTimer_Tick;

            if (Settings.ScheduleEnabled)
            {
                _scheduleTimer.Start();
                ScheduleTimer_Tick(null, null);
                _preScheduleGammaEnabled = Settings.GammaEnabled;
                _preScheduleGammaRScale = Settings.GammaRScale;
                _preScheduleGammaGScale = Settings.GammaGScale;
                _preScheduleGammaBScale = Settings.GammaBScale;
                _preScheduleGammaValue = Settings.GammaValue;
                _preScheduleMasterBrightness = Settings.MasterBrightness;
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

            ThemeManager.UpdateActiveTheme();

            _messageWindow = new MessageWindow(this);

            _updateCheckTimer = new Timer { Interval = 3000 };
            _updateCheckTimer.Tick += (s, e) =>
            {
                _updateCheckTimer.Stop();
                _updateCheckTimer.Dispose();
                _updateCheckTimer = null;
                RunUpdateCheck(silent: true);
            };
            _updateCheckTimer.Start();

            _healthCheckTimer = new Timer { Interval = 5 * 60 * 1000 };
            _healthCheckTimer.Tick += HealthCheckTimer_Tick;
            _healthCheckTimer.Start();
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
            _trayIcon.DoubleClick += OnTrayIconDoubleClick;
        }

        private void OnTrayMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _trayMenuOpen = true;
        }

        private void OnTrayMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            _trayMenuOpen = false;
            if (_trayMenuNeedsRebuild && !_trayClickInProgress)
            {
                _trayMenuNeedsRebuild = false;
                RebuildTrayMenu();
            }
            ScheduleMenuCleanupGc();
        }

        private void OnTrayIconDoubleClick(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        internal void UpdateTrayMenu()
        {
            if (_trayMenuOpen)
            {
                _trayMenuNeedsRebuild = true;
                return;
            }

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

        private void ExecuteTrayAction(Action action)
        {
            _trayClickInProgress = true;
            try
            {
                action();
            }
            finally
            {
                _trayClickInProgress = false;
                if (_trayMenuNeedsRebuild && !_trayMenuOpen)
                {
                    _trayMenuNeedsRebuild = false;
                    RebuildTrayMenu();
                }
            }
        }

        private void RebuildTrayMenu()
        {
            if (_trayMenu == null || _trayMenu.IsDisposed) return;

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

            ScheduleMenuCleanupGc();
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
                RecursiveDispose(_trayGammaItem);
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
            _trayGammaItem.Click += (s, e) => ExecuteTrayAction(GammaTrayToggle);
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
                _trayRestoreItem = new ToolStripMenuItem("恢复定时控制", null, (s, ev) => ExecuteTrayAction(() =>
                {
                    _scheduleManualOverride = false;
                    ScheduleTimer_Tick(null, null);
                    UpdateTrayMenu();
                    ScheduleStateChanged?.Invoke();
                }));
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
                item.Click += (s, ev) => ExecuteTrayAction(() => QuickPreset(cp));
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
                    item.Click += (s, ev) => ExecuteTrayAction(() => QuickPreset(name));
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
                item.Click += (s, ev) => ExecuteTrayAction(() => ApplyPresetToMonitor(presetName, monDeviceId));
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
                    item.Click += (s, ev) => ExecuteTrayAction(() => ApplyPresetToMonitor(presetName, monDeviceId));
                    monitorItem.DropDownItems.Add(item);
                }
            }

            _trayQuickMenu.DropDownItems.Add(monitorItem);
        }

        private void BuildStaticTraySection()
        {
            _trayMenu.Items.Add(new ToolStripSeparator());

            var checkUpdateItem = new ToolStripMenuItem("检查更新", null, (s, ev) => ExecuteTrayAction(() => RunUpdateCheck()));
            _trayMenu.Items.Add(checkUpdateItem);

            var showItem = new ToolStripMenuItem("显示主界面", null, (s, ev) => ExecuteTrayAction(ShowMainWindow));
            _trayMenu.Items.Add(showItem);

            var powerItem = new ToolStripMenuItem("关闭显示器", null, (s, ev) => ExecuteTrayAction(TurnOffMonitor));
            _trayMenu.Items.Add(powerItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem("退出", null, (s, ev) => ExecuteTrayAction(ExitApplication));
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
                _trayRestoreItem = new ToolStripMenuItem("恢复定时控制", null, (s, ev) => ExecuteTrayAction(() =>
                {
                    _scheduleManualOverride = false;
                    ScheduleTimer_Tick(null, null);
                    UpdateTrayMenu();
                    ScheduleStateChanged?.Invoke();
                }));
                int restoreIndex = _trayMenu.Items.IndexOf(_trayQuickMenu) + 1;
                _trayMenu.Items.Insert(restoreIndex, _trayRestoreItem);
            }
            else if (!needsRestoreItem && hasRestoreItem)
            {
                _trayMenu.Items.Remove(_trayRestoreItem);
                RecursiveDispose(_trayRestoreItem);
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

                if (_lightweightMode)
                {
                    _lastScheduleMode = targetMode;
                    _scheduleChangedInLightweight = true;
                    ApplyScheduleMonitorPresets(targetSegment);
                    return;
                }

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
            if (_scheduleTimer != null)
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
            if (_exiting) return;
            var form = MainForm;
            if (form != null && !form.IsDisposed)
            {
                if (form.InvokeRequired)
                {
                    try { form.Invoke(new Action(() => { if (!_exiting && !form.IsDisposed) HandleDisplayChange(); })); }
                    catch { HandleDisplayChange(); }
                }
                else
                {
                    HandleDisplayChange();
                }
            }
            else
            {
                HandleDisplayChange();
            }
        }

        private void OnMonitorsChangedInternal()
        {
            if (_exiting) return;
            var form = MainForm;
            if (form != null && !form.IsDisposed)
            {
                if (form.InvokeRequired)
                {
                    try { form.Invoke(new Action(() => { if (!_exiting && !form.IsDisposed) MonitorsChanged?.Invoke(); })); }
                    catch { MonitorsChanged?.Invoke(); }
                }
                else
                {
                    MonitorsChanged?.Invoke();
                }
            }
            else
            {
                MonitorsChanged?.Invoke();
            }
        }

        private void HandleDisplayChange()
        {
            if (_monitorManager == null) return;

            if (_lightweightMode)
            {
                _displayChangedInLightweight = true;
                _trayMenuNeedsRebuild = true;
                return;
            }

            var removedIds = _monitorManager.RefreshMonitors();
            CleanupStaleSettings(removedIds);
            if (!Form1IsOpen())
            {
                if (_trayMenuOpen)
                {
                    _trayMenuNeedsRebuild = true;
                }
                else
                {
                    ClearDynamicTraySection();
                    BuildDynamicTraySection();
                    ScheduleMicroGc();
                }
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
            return MainForm != null;
        }

        public void ShowMainWindow()
        {
            if (Form1IsOpen())
            {
                var form = MainForm;
                if (form.InvokeRequired)
                    form.Invoke(new Action(() => ActivateExistingForm()));
                else
                    ActivateExistingForm();
                return;
            }

            _lightweightEntryTimer?.Stop();
            _lightweightEntryTimer?.Dispose();
            _lightweightEntryTimer = null;

            if (_lightweightMode)
            {
                _lightweightMode = false;
                var displayChanged = _displayChangedInLightweight;
                _displayChangedInLightweight = false;
                _lightweightGcTimer?.Stop();
                _lightweightGcTimer?.Dispose();
                _lightweightGcTimer = null;
                _microGcTimer?.Stop();
                _microGcTimer?.Dispose();
                _microGcTimer = null;
                if (_monitorManager != null)
                {
                    if (displayChanged)
                        _monitorManager.RefreshMonitors();
                    else
                        _monitorManager.ExitLightweightMode();
                }
                if (_scheduleTimer != null)
                    _scheduleTimer.Interval = ScheduleTimerIntervalNormal;
                if (_messageWindow == null)
                    _messageWindow = new MessageWindow(this);
                _healthCheckTimer?.Start();
                GcHelper.CollectFull();
                if (_scheduleChangedInLightweight)
                {
                    _scheduleChangedInLightweight = false;
                    ScheduleStateChanged?.Invoke();
                }
            }

            MainForm = new Form1(this);
            MainForm.Show();
        }

        internal void OnFormClosing(Form1 form)
        {
            if (MainForm == form)
            {
                MainForm = null;
            }
        }

        internal void EnterAppLightweightMode()
        {
            if (_lightweightMode) return;
            _lightweightMode = true;
            _scheduleChangedInLightweight = false;
            Form1.CleanupStaticFields();
            Controls.GdiCache.Clear();
            GammaController.TrimCache();
            _parsedSegments = null;
            _parsedSegmentsHash = 0;
            _messageWindow?.Dispose();
            _messageWindow = null;
            _healthCheckTimer?.Stop();
            if (_monitorManager != null)
                _monitorManager.EnterLightweightMode();
            if (_scheduleTimer != null)
                _scheduleTimer.Interval = ScheduleTimerIntervalLightweight;
            GcHelper.CollectFull();
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, true, true);
            }
            catch { }
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
                GcHelper.CollectFull();
                GcHelper.TrimWorkingSet();
            }
            else if (_lightweightGcTickCount % Gen1CollectEveryNTicks == 0)
            {
                GC.Collect(1, GCCollectionMode.Forced, false);
                GC.WaitForPendingFinalizers();
                GcHelper.TrimWorkingSet();
            }
            else
            {
                GC.Collect(0, GCCollectionMode.Forced);
            }

            if (_lightweightGcTickCount % 3 == 0)
            {
                GcHelper.RecordSampleAndCheck();
            }
        }

        private void HealthCheckTimer_Tick(object sender, EventArgs e)
        {
            if (_exiting || _lightweightMode) return;

            try
            {
                GcHelper.RecordSampleAndCheck();

                if (GcHelper.DetectLeakSuspect())
                {
                    GcHelper.CollectFull();
                    GcHelper.TrimWorkingSet();
                    GcHelper.LogDiagnosticReport();
                }
            }
            catch { }
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

        private void ScheduleMenuCleanupGc()
        {
            if (_menuCleanupTimer != null) return;

            _menuCleanupTimer = new Timer { Interval = 1500 };
            _menuCleanupTimer.Tick += (s, e) =>
            {
                _menuCleanupTimer?.Stop();
                _menuCleanupTimer?.Dispose();
                _menuCleanupTimer = null;
                GcHelper.CollectFull();
                GcHelper.TrimWorkingSet();
            };
            _menuCleanupTimer.Start();
        }

        internal void ScheduleLightweightModeEntry()
        {
            if (_lightweightEntryTimer != null)
            {
                _lightweightEntryTimer.Stop();
                _lightweightEntryTimer.Dispose();
            }
            _lightweightEntryTimer = new Timer { Interval = 5000 };
            _lightweightEntryTimer.Tick += (s, e) =>
            {
                _lightweightEntryTimer.Stop();
                _lightweightEntryTimer.Dispose();
                _lightweightEntryTimer = null;
                EnterAppLightweightMode();
            };
            _lightweightEntryTimer.Start();
        }

        private void ActivateExistingForm()
        {
            try
            {
                var form = MainForm;
                if (form == null)
                {
                    ShowMainWindow();
                    return;
                }
                form.Show();
                form.WindowState = FormWindowState.Normal;
                form.ShowInTaskbar = true;
                form.Activate();
            }
            catch
            {
                MainForm = null;
                ShowMainWindow();
            }
        }

        #endregion

        #region Other

        private async void RunUpdateCheck(bool silent = false)
        {
            CancelUpdateCheck();
            var cts = new System.Threading.CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _updateCheckCts, cts);
            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch { }
                oldCts.Dispose();
            }
            var token = cts.Token;
            try
            {
                await UpdateService.CheckForUpdateAsync(silent, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
            finally
            {
                if (Interlocked.CompareExchange(ref _updateCheckCts, null, cts) == cts)
                {
                    cts.Dispose();
                }
            }
        }

        private void CancelUpdateCheck()
        {
            var cts = Interlocked.Exchange(ref _updateCheckCts, null);
            if (cts != null)
            {
                try { cts.Cancel(); } catch { }
                cts.Dispose();
            }
        }

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

            CancelUpdateCheck();

            try { SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; } catch { }

            try { _updateCheckTimer?.Stop(); _updateCheckTimer?.Dispose(); _updateCheckTimer = null; } catch { }

            try { _lightweightEntryTimer?.Stop(); _lightweightEntryTimer?.Dispose(); _lightweightEntryTimer = null; } catch { }

            try { _healthCheckTimer?.Stop(); _healthCheckTimer?.Dispose(); _healthCheckTimer = null; } catch { }

            if (Form1IsOpen())
            {
                var form = MainForm;
                try { form.Close(); }
                catch { try { form.Dispose(); } catch { } }
                MainForm = null;
            }

            if (GammaController != null)
            {
                try
                {
                    if (Settings.RestoreGammaOnExit)
                    {
                        GammaController.ResetGamma(Screen.AllScreens);
                    }
                }
                catch { }
                try { GammaController.Dispose(); } catch { }
            }

            if (_monitorManager != null)
            {
                try { _monitorManager.MonitorsChanged -= OnMonitorsChangedInternal; } catch { }
                try { _monitorManager.Dispose(); } catch { }
            }

            try { _scheduleTimer?.Stop(); _scheduleTimer?.Dispose(); _scheduleTimer = null; } catch { }

            try { _lightweightGcTimer?.Stop(); _lightweightGcTimer?.Dispose(); _lightweightGcTimer = null; } catch { }

            try { _microGcTimer?.Stop(); _microGcTimer?.Dispose(); _microGcTimer = null; } catch { }

            try { _menuCleanupTimer?.Stop(); _menuCleanupTimer?.Dispose(); _menuCleanupTimer = null; } catch { }

            _parsedSegments = null;
            _parsedSegmentsHash = 0;
            MonitorsChanged = null;
            ScheduleStateChanged = null;

            try { Controls.GdiCache.Clear(); } catch { }

            try { Form1.CleanupStaticFields(); } catch { }

            try { _messageWindow?.Dispose(); _messageWindow = null; } catch { }

            if (_trayMenu != null)
            {
                try
                {
                    var items = new ToolStripItem[_trayMenu.Items.Count];
                    _trayMenu.Items.CopyTo(items, 0);
                    foreach (ToolStripItem item in items)
                        RecursiveDispose(item);
                    _trayMenu.Items.Clear();
                    _trayMenu.Opening -= OnTrayMenuOpening;
                    _trayMenu.Closed -= OnTrayMenuClosed;
                    _trayMenu.Dispose();
                    _trayMenu = null;
                }
                catch
                {
                    try { _trayMenu?.Dispose(); } catch { }
                }
            }

            if (_trayIcon != null)
            {
                try
                {
                    _trayIcon.DoubleClick -= OnTrayIconDoubleClick;
                    _trayIcon.Icon = null;
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                catch { }
            }

            try { _components?.Dispose(); } catch { }
            try { GcHelper.DisposeCachedProcess(); } catch { }

            Application.Exit();
        }

        private void PerformEmergencyCleanup()
        {
            try { SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; } catch { }

            CancelUpdateCheck();

            try { _updateCheckTimer?.Stop(); _updateCheckTimer?.Dispose(); } catch { }
            try { _lightweightEntryTimer?.Stop(); _lightweightEntryTimer?.Dispose(); } catch { }
            try { _healthCheckTimer?.Stop(); _healthCheckTimer?.Dispose(); } catch { }

            if (GammaController != null)
            {
                try { GammaController.Dispose(); } catch { }
            }

            if (_monitorManager != null)
            {
                try { _monitorManager.MonitorsChanged -= OnMonitorsChangedInternal; } catch { }
                try { _monitorManager.Dispose(); } catch { }
            }

            try { _scheduleTimer?.Stop(); _scheduleTimer?.Dispose(); } catch { }
            try { _lightweightGcTimer?.Stop(); _lightweightGcTimer?.Dispose(); } catch { }
            try { _microGcTimer?.Stop(); _microGcTimer?.Dispose(); } catch { }
            try { _menuCleanupTimer?.Stop(); _menuCleanupTimer?.Dispose(); } catch { }

            _parsedSegments = null;
            _parsedSegmentsHash = 0;
            MonitorsChanged = null;
            ScheduleStateChanged = null;

            try { Controls.GdiCache.Clear(); } catch { }
            try { Form1.CleanupStaticFields(); } catch { }
            try { _messageWindow?.Dispose(); } catch { }

            if (_trayMenu != null)
            {
                try
                {
                    _trayMenu.Opening -= OnTrayMenuOpening;
                    _trayMenu.Closed -= OnTrayMenuClosed;
                    _trayMenu.Dispose();
                }
                catch { }
            }

            if (_trayIcon != null)
            {
                try
                {
                    _trayIcon.DoubleClick -= OnTrayIconDoubleClick;
                    _trayIcon.Dispose();
                }
                catch { }
            }

            try { _components?.Dispose(); } catch { }
            try { GcHelper.DisposeCachedProcess(); } catch { }
        }

        #endregion

        #region Message Window

        private class MessageWindow : NativeWindow
        {
            private readonly WeakReference<BackgroundService> _serviceRef;

            public MessageWindow(BackgroundService service)
            {
                _serviceRef = new WeakReference<BackgroundService>(service);
                CreateHandle(new CreateParams
                {
                    Caption = "LumiShiftMessageWindow",
                    Parent = new IntPtr(-3)
                });
            }

            public void Dispose()
            {
                if (Handle != IntPtr.Zero)
                    DestroyHandle();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_SHOW_LUMISHIFT)
                {
                    if (_serviceRef.TryGetTarget(out var service) && !service._exiting)
                        service.ShowMainWindow();
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
            GC.SuppressFinalize(this);
            try { ExitApplication(); }
            catch
            {
                PerformEmergencyCleanup();
            }
        }
    }
}
