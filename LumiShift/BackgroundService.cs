using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management;
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
        private PresetService _presetService;
        private DisplayGammaStateService _displayGammaState;

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
        private Timer _gammaReapplyTimer;
        private Timer _gammaWatchdogTimer;
        private Timer _displayChangeDebounceTimer;
        private ManagementEventWatcher _brightnessEventWatcher;

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

        private ScheduleEvaluator _scheduleEvaluator;
        private int _parsedSegmentsHash;
        private DateTime _lastMonitorNotificationTime = DateTime.MinValue;

        public event Action MonitorsChanged;
        public event Action ScheduleStateChanged;

        /// <summary>外部（系统设置/Fn 键等）修改了某显示器的硬件亮度时触发，参数为设备 ID 与新亮度。</summary>
        public event Action<string, int> BrightnessChanged;

        internal void ShowWindowsNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (_trayIcon == null || _exiting || _disposed) return;
            try
            {
                _trayIcon.BalloonTipTitle = title;
                _trayIcon.BalloonTipText = message;
                _trayIcon.BalloonTipIcon = icon;
                _trayIcon.ShowBalloonTip(3500);
            }
            catch { }
        }

        internal void NotifyStatusSwitch(string title, string message)
        {
            if (!Settings.NotificationsEnabled || !Settings.NotifyStatusSwitch) return;
            ShowWindowsNotification(title, message);
        }

        private void NotifyScheduleSwitch(string presetName, ScheduleSegment segment)
        {
            if (!Settings.NotificationsEnabled || !Settings.NotifyScheduleSwitch) return;
            string range = segment == null ? "" : Lang.F("（{0}-{1}）", segment.StartTime, segment.EndTime);
            ShowWindowsNotification(Lang.Get("LumiShift 定时切换"), Lang.F("已切换到 {0} {1}", Lang.Get(presetName), range).Trim());
        }

        private void NotifyMonitorChange(int monitorCount, int removedCount)
        {
            if (!Settings.NotificationsEnabled || !Settings.NotifyMonitorChange) return;
            var now = DateTime.Now;
            if ((now - _lastMonitorNotificationTime).TotalSeconds < 2) return;
            _lastMonitorNotificationTime = now;
            string message = removedCount > 0
                ? Lang.F("显示器配置已变更，当前 {0} 台，移除 {1} 台。", monitorCount, removedCount)
                : Lang.F("显示器配置已变更，当前 {0} 台。", monitorCount);
            ShowWindowsNotification(Lang.Get("LumiShift 显示器变更"), message);
        }

        private void ScheduleStartupNotification()
        {
            if (!Settings.NotificationsEnabled || !Settings.NotifyStartup) return;

            var timer = new Timer { Interval = 1200 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                string scheduleText = Settings.ScheduleEnabled ? Lang.Get("定时调度已开启") : Lang.Get("定时调度未开启");
                string gammaText = Settings.GammaEnabled ? Lang.Get("显示调节已启用") : Lang.Get("显示调节未启用");
                ShowWindowsNotification(Lang.Get("LumiShift 已启动"), Lang.F("{0}，{1}。", gammaText, scheduleText));
            };
            timer.Start();
        }

        public BackgroundService()
        {
            Settings = SettingsStore.LoadSettings();
            GammaController = new GammaController();
            _presetService = new PresetService(Settings);
            _displayGammaState = new DisplayGammaStateService(Settings, _presetService);

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;

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
            UpdateTrayText();
            ScheduleStartupNotification();

            if (Settings.EyeProtectionEnabled)
            {
                EyeProtectionService.ApplyColor(
                    Settings.EyeProtectionRed,
                    Settings.EyeProtectionGreen,
                    Settings.EyeProtectionBlue);
            }

            Controls.GdiCache.Clear();

            _messageWindow = new MessageWindow(this);

            if (Settings.AutoCheckUpdates)
            {
                _updateCheckTimer = new Timer { Interval = 3000 };
                _updateCheckTimer.Tick += (s, e) =>
                {
                    _updateCheckTimer.Stop();
                    _updateCheckTimer.Dispose();
                    _updateCheckTimer = null;
                    RunUpdateCheck(silent: true);
                };
                _updateCheckTimer.Start();
            }

            _healthCheckTimer = new Timer { Interval = 5 * 60 * 1000 };
            _healthCheckTimer.Tick += HealthCheckTimer_Tick;
            _healthCheckTimer.Start();

            StartGammaWatchdog();
            StartBrightnessEventWatcher();
        }

        #region Tray Icon

        private void CreateTrayIcon()
        {
            _components = new System.ComponentModel.Container();
            _trayIcon = new NotifyIcon(_components)
            {
                Text = "LumiShift",
                Icon = Program.AppIcon,
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
            if (_trayMenuNeedsRebuild && !_trayClickInProgress)
            {
                _trayMenuNeedsRebuild = false;
                RebuildTrayMenu();
            }
            // 菜单打开时自动应用 Gamma，确保设置生效
            ApplyGammaToSystem();
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
                    ? Lang.Get("Gamma 校正: 已启用")
                    : Lang.Get("Gamma 校正: 已禁用"))
            {
                Checked = Settings.GammaEnabled
            };
            _trayGammaItem.Click += (s, e) => ExecuteTrayAction(GammaTrayToggle);
            _trayMenu.Items.Add(_trayGammaItem);

            _trayQuickMenu = new ToolStripMenuItem(Lang.Get("快速切换预设"));

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
                _trayRestoreItem = new ToolStripMenuItem(Lang.Get("恢复定时控制"), null, (s, ev) => ExecuteTrayAction(() =>
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

            _trayAllMonitorsItem = new ToolStripMenuItem(Lang.Get("全部显示器"));
            foreach (var p in PresetDefinitions.GetNames())
            {
                bool isActive = !anyMonitorOverride && Settings.GammaEnabled && globalPresetName == p;
                var item = new ToolStripMenuItem(Lang.Get(p)) { Checked = isActive };
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
                    var item = new ToolStripMenuItem(Lang.Get(cp.Name)) { Checked = isActive };
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
                monitorLabel += $" ({Lang.Get(GetMonitorPresetName(deviceId))})";

            var monitorItem = new ToolStripMenuItem(monitorLabel);
            string currentMonitorPreset = GetMonitorPresetName(deviceId);

            foreach (var p in PresetDefinitions.GetNames())
            {
                bool isActive = currentMonitorPreset == p;
                var item = new ToolStripMenuItem(Lang.Get(p)) { Checked = isActive };
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
                    var item = new ToolStripMenuItem(Lang.Get(cp.Name)) { Checked = isActive };
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
            if (_trayMenu.Items.Count > 0)
                _trayMenu.Items.Add(new ToolStripSeparator());

            var checkUpdateItem = new ToolStripMenuItem(Lang.Get("检查更新"), null, (s, ev) => ExecuteTrayAction(() => RunUpdateCheck()));
            _trayMenu.Items.Add(checkUpdateItem);

            var showItem = new ToolStripMenuItem(Lang.Get("显示主界面"), null, (s, ev) => ExecuteTrayAction(ShowMainWindow));
            _trayMenu.Items.Add(showItem);

            var powerItem = new ToolStripMenuItem(Lang.Get("关闭显示器"), null, (s, ev) => ExecuteTrayAction(TurnOffMonitor));
            _trayMenu.Items.Add(powerItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem(Lang.Get("退出"), null, (s, ev) => ExecuteTrayAction(ExitApplication));
            _trayMenu.Items.Add(exitItem);
        }

        private void RefreshDynamicTraySection()
        {
            if (_trayGammaItem == null)
            {
                UpdateTrayText();
                return;
            }

            bool gammaSupported = GammaController.IsSupported;
            _trayGammaItem.Text = gammaSupported && Settings.GammaEnabled
                ? Lang.Get("Gamma 校正: 已启用")
                : Lang.Get("Gamma 校正: 已禁用");
            _trayGammaItem.Checked = Settings.GammaEnabled;

            RefreshAllMonitorsSubMenu();

            bool hasRestoreItem = _trayRestoreItem != null;
            bool needsRestoreItem = Settings.ScheduleEnabled && _scheduleManualOverride;

            if (needsRestoreItem && !hasRestoreItem)
            {
                _trayRestoreItem = new ToolStripMenuItem(Lang.Get("恢复定时控制"), null, (s, ev) => ExecuteTrayAction(() =>
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

        internal string GetScheduleStatusText()
        {
            if (!Settings.ScheduleEnabled)
                return "LumiShift";

            string currentPreset = GetCurrentPresetName() ?? Lang.Get("自定义");
            if (_scheduleManualOverride)
            {
                string nextInfo = GetNextScheduleInfo();
                string overrideText = string.IsNullOrEmpty(nextInfo)
                    ? Lang.Get("手动调整")
                    : Lang.F("手动调整 ({0}恢复)", nextInfo);
                return $"LumiShift - {overrideText}";
            }
            return Lang.F("LumiShift - 定时: {0}", Lang.Get(currentPreset));
        }

        private void UpdateTrayText()
        {
            if (_trayIcon == null) return;
            _trayIcon.Text = GetScheduleStatusText();
            if (_trayIcon.Text.Length > 127)
                _trayIcon.Text = _trayIcon.Text.Substring(0, 127);
        }

        #endregion

        #region Preset Helpers

        private string MatchPresetName(double r, double g, double b, double gv, int brightness)
        {
            foreach (var bip in PresetDefinitions.BuiltIns)
            {
                if (bip.Matches(r, g, b, gv, brightness))
                    return bip.Name;
            }
            foreach (var cp in Settings.CustomGammaPresets)
            {
                if (Math.Abs(r - cp.RScale) < 0.01 &&
                    Math.Abs(g - cp.GScale) < 0.01 &&
                    Math.Abs(b - cp.BScale) < 0.01 &&
                    Math.Abs(gv - cp.GammaValue) < 0.01 &&
                    Math.Abs(brightness - cp.MasterBrightness) <= 1)
                    return cp.Name;
            }
            return null;
        }

        internal string GetCurrentPresetName()
        {
            if (!Settings.GammaEnabled)
                return PresetDefinitions.BuiltIns[0].Name;
            return MatchPresetName(Settings.GammaRScale, Settings.GammaGScale, Settings.GammaBScale, Settings.GammaValue, Settings.MasterBrightness);
        }

        internal string GetMonitorPresetName(string deviceId)
        {
            if (Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
            {
                if (!pdg.Enabled) return PresetDefinitions.BuiltIns[0].Name;
                string name = MatchPresetName(pdg.RScale, pdg.GScale, pdg.BScale, pdg.GammaValue, pdg.MasterBrightness);
                if (name != null) return name;
            }
            return GetCurrentPresetName();
        }

        internal bool TryApplyPreset(string name)
        {
            return _displayGammaState.SetGlobalPreset(name, GammaSource.Manual);
        }

        internal void ApplyPresetToMonitor(string presetName, string deviceId)
        {
            if (!_displayGammaState.SetDisplayPreset(deviceId, presetName, GammaSource.Manual))
                return;

            if (Settings.ScheduleEnabled)
                _scheduleManualOverride = true;

            ApplyGammaToSystem();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(Settings);
            NotifyStatusSwitch(Lang.Get("LumiShift 状态切换"), Lang.F("{0} 已应用到当前显示器", Lang.Get(presetName)));
            ScheduleStateChanged?.Invoke();
        }

        internal void SetGlobalGammaParameters(GammaConfig parameters, bool clearDisplayOverrides)
        {
            _displayGammaState.SetGlobalParameters(parameters, clearDisplayOverrides);
        }

        internal void SetDisplayGammaParameters(string deviceId, GammaConfig parameters)
        {
            _displayGammaState.SetDisplayParameters(deviceId, parameters, GammaSource.Manual);
        }

        internal bool ClearDisplayGammaOverride(string deviceId)
        {
            return _displayGammaState.ClearDisplayOverride(deviceId);
        }

        internal GammaConfig GetEffectiveGammaParameters(string deviceId)
        {
            return _displayGammaState.GetEffectiveParameters(deviceId);
        }

        internal bool HasDisplayGammaOverride(string deviceId)
        {
            return _displayGammaState.HasDisplayOverride(deviceId);
        }

        internal string GetDisplayGammaSource(string deviceId)
        {
            return _displayGammaState.GetDisplaySource(deviceId);
        }

        internal bool HasAnyDisplayGammaOverride()
        {
            return _displayGammaState.OverrideCount > 0;
        }

        internal bool HasManualDisplayGammaOverride()
        {
            return _displayGammaState.HasManualOverrides();
        }

        internal bool HasScheduleDisplayGammaOverride()
        {
            return _displayGammaState.HasScheduleOverrides();
        }

        internal void QuickPreset(string presetName)
        {
            if (Settings.ScheduleEnabled)
                _scheduleManualOverride = true;
            TryApplyPreset(presetName);
            ApplyGammaToSystem();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(Settings);
            NotifyStatusSwitch(Lang.Get("LumiShift 状态切换"), Lang.F("已切换到 {0}", Lang.Get(presetName)));
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
            NotifyStatusSwitch(Lang.Get("LumiShift 状态切换"), Settings.GammaEnabled ? Lang.Get("显示调节已启用") : Lang.Get("显示调节已关闭"));
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
                    var parameters = new Infrastructure.GammaParameters(
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

            var perScreenParams = new Dictionary<string, Infrastructure.GammaParameters>();
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
                        perScreenParams[screen.DeviceName] = new Infrastructure.GammaParameters(
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
                        perScreenParams[screen.DeviceName] = new Infrastructure.GammaParameters(
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
                    perScreenParams[screen.DeviceName] = new Infrastructure.GammaParameters(
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
                EnsureParsedSegments();
                var target = _scheduleEvaluator?.FindCurrent(DateTime.Now.TimeOfDay);
                if (target == null)
                {
                    // 当前不在任何时段内：仍要更新 lastScheduleMode 以便下次进入时段时正确切换
                    _lastScheduleMode = "";
                    return;
                }

                string targetMode = target.PresetName;
                string targetScheduleKey = target.Key;
                ScheduleSegment targetSegment = target.Segment;

                if (_scheduleManualOverride)
                {
                    if (targetScheduleKey == _lastScheduleMode)
                        return;

                    _scheduleManualOverride = false;
                }

                if (targetScheduleKey == _lastScheduleMode) return;

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
                    _lastScheduleMode = targetScheduleKey;
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
                    _lastScheduleMode = targetScheduleKey;
                    ApplyScheduleMonitorPresets(targetSegment);
                    return;
                }

                if (_lightweightMode)
                {
                    _lastScheduleMode = targetScheduleKey;
                    _scheduleChangedInLightweight = true;
                    ApplyScheduleMonitorPresets(targetSegment);
                    return;
                }

                SettingsStore.SaveSettings(Settings);
                UpdateTrayMenu();
                _lastScheduleMode = targetScheduleKey;

                ApplyScheduleMonitorPresets(targetSegment);

                NotifyScheduleSwitch(targetMode, targetSegment);
                ScheduleStateChanged?.Invoke();
            }
            catch
            {
            }
            finally
            {
                ScheduleNextWakeUp();
            }
        }

        /// <summary>
        /// 根据距下次切换的时长动态调整 Timer.Interval，平衡精度与功耗。
        /// 切换点附近高频轮询（5s/15s），平时低频兜底（1min/5min）。
        /// </summary>
        private void ScheduleNextWakeUp()
        {
            if (_scheduleTimer == null || _disposed) return;

            EnsureParsedSegments();
            double minutesToNext = _scheduleEvaluator?.MinutesToNextSwitch(DateTime.Now.TimeOfDay)
                                   ?? double.MaxValue;

            int interval;
            if (minutesToNext <= 1)
                interval = 5000;        // 切换前 1 分钟：5s 精度
            else if (minutesToNext <= 5)
                interval = 15000;       // 5 分钟内：15s 精度
            else if (minutesToNext <= 60)
                interval = 60000;       // 1 小时内：1 分钟精度
            else
                interval = 300000;      // 超过 1 小时：5 分钟

            // 轻量模式下
            if (_lightweightMode && minutesToNext > 5)
                interval = Math.Max(interval, 120000);

            _scheduleTimer.Interval = interval;
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (_exiting || _disposed) return;
            if (e.Mode != PowerModes.Resume) return;

            // 从睡眠/休眠恢复：Windows 会重置 Gamma 渐变，无论是否开启定时调度都必须重新应用
            if (Settings.ScheduleEnabled)
                _lastScheduleMode = "";

            InvokeOnUIThread(() =>
            {
                try
                {
                    if (_disposed) return;

                    ApplyGammaToSystem();
                    ScheduleDelayedGammaReapply();

                    // 定时调度开启时立即重新评估（避免睡眠期间错过切换点）
                    if (Settings.ScheduleEnabled && _scheduleTimer != null)
                    {
                        _scheduleTimer.Stop();
                        _scheduleTimer.Start();
                        ScheduleTimer_Tick(null, null);
                    }
                }
                catch { }
            });
        }

        internal void OnMonitorPowerOn()
        {
            if (_exiting || _disposed) return;

            // 显示器重新上电（电源计划关闭屏幕后恢复等）：
            // 驱动会重置 Gamma 渐变，且重新枚举后设备名可能变化，
            // 轻量模式下先刷新显示器缓存再应用，避免用过期设备名应用失败
            InvokeOnUIThread(() =>
            {
                try
                {
                    if (_exiting || _disposed) return;

                    if (_lightweightMode && _monitorManager != null)
                    {
                        var removed = _monitorManager.RefreshMonitors();
                        CleanupStaleSettings(removed);
                    }

                    ApplyGammaToSystem();
                    ScheduleDelayedGammaReapply();
                }
                catch { }
            });
        }

        /// <summary>
        /// 消息窗口收到显示器变更通知（UI 线程）。
        /// 显示器变更事件常成组触发，防抖后统一处理。
        /// </summary>
        internal void OnDisplayChangeMessage()
        {
            if (_exiting || _disposed) return;

            if (_displayChangeDebounceTimer == null)
            {
                _displayChangeDebounceTimer = new Timer { Interval = 1200 };
                _displayChangeDebounceTimer.Tick += (s, e) =>
                {
                    try { _displayChangeDebounceTimer.Stop(); } catch { }
                    HandleLightweightDisplayRefresh();
                };
            }

            _displayChangeDebounceTimer.Stop();
            _displayChangeDebounceTimer.Start();
        }

        private void HandleLightweightDisplayRefresh()
        {
            if (_exiting || _disposed) return;
            // UI 打开时由 HandleDisplayChange 实时路径处理
            if (!_lightweightMode || _monitorManager == null) return;

            try
            {
                // 刷新显示器缓存（设备名/枚举可能已变化），再重应用 Gamma，
                // 确保按显示器的 Gamma 设置在重新枚举后仍能正确应用
                var removedIds = _monitorManager.RefreshMonitors();
                CleanupStaleSettings(removedIds);
                ApplyGammaToSystem();
            }
            catch { }
        }

        private void ScheduleDelayedGammaReapply(int delayMs = 2000)
        {
            if (_exiting || _disposed) return;

            if (_gammaReapplyTimer == null)
            {
                _gammaReapplyTimer = new Timer();
                _gammaReapplyTimer.Tick += (s, e) =>
                {
                    try { _gammaReapplyTimer.Stop(); } catch { }
                    if (!_exiting && !_disposed)
                    {
                        try { ApplyGammaToSystem(); } catch { }
                    }
                };
            }

            _gammaReapplyTimer.Interval = delayMs;
            _gammaReapplyTimer.Stop();
            _gammaReapplyTimer.Start();
        }

        /// <summary>
        /// Gamma 渐变看门狗。微软文档明确说明：Gamma 渐变会在大多数显示事件
        /// （显示器断开/连接、分辨率更改、电源计划息屏/亮屏、睡眠恢复等）后被
        /// 系统或驱动重置，且无法保证已设置的渐变持续生效。
        /// 因此周期性重申（f.lux 等工具的标准做法），确保任何场景下 5 秒内恢复。
        /// </summary>
        private void StartGammaWatchdog()
        {
            _gammaWatchdogTimer = new Timer { Interval = 5000 };
            _gammaWatchdogTimer.Tick += (s, e) =>
            {
                if (_exiting || _disposed) return;
                if (!Settings.GammaEnabled) return;
                try { ApplyGammaToSystem(); } catch { }
            };
            _gammaWatchdogTimer.Start();
        }

        /// <summary>
        /// 监听 WmiMonitorBrightnessEvent：系统在亮度被外部修改
        /// （Fn 键/系统设置/快捷面板等）时触发并携带新亮度值。
        /// 仅笔记本内置屏等支持 WMI 亮度的设备可用，其余设备由轮询兜底。
        /// </summary>
        private void StartBrightnessEventWatcher()
        {
            try
            {
                var query = new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent");
                _brightnessEventWatcher = new ManagementEventWatcher(query);
                _brightnessEventWatcher.EventArrived += OnBrightnessEventArrived;
                _brightnessEventWatcher.Start();
            }
            catch
            {
                // 台式机（无 WMI 亮度类）或权限不足：回退到轮询
                StopBrightnessEventWatcher();
            }
        }

        private void StopBrightnessEventWatcher()
        {
            if (_brightnessEventWatcher == null) return;
            try { _brightnessEventWatcher.EventArrived -= OnBrightnessEventArrived; } catch { }
            try { _brightnessEventWatcher.Stop(); } catch { }
            try { _brightnessEventWatcher.Dispose(); } catch { }
            _brightnessEventWatcher = null;
        }

        private static void DisposeTimer(ref Timer timer)
        {
            try { timer?.Stop(); timer?.Dispose(); } catch { }
            timer = null;
        }

        private void OnBrightnessEventArrived(object sender, EventArrivedEventArgs e)
        {
            if (_exiting || _disposed) return;
            try
            {
                string instanceName = e.NewEvent["InstanceName"]?.ToString();
                object brightnessObj = e.NewEvent["Brightness"];
                if (string.IsNullOrEmpty(instanceName) || brightnessObj == null) return;

                int brightness = Convert.ToInt32(brightnessObj);
                if (brightness < 0 || brightness > 100) return;

                InvokeOnUIThread(() =>
                {
                    if (_exiting || _disposed) return;
                    string deviceId = ResolveMonitorDeviceId(instanceName);
                    if (deviceId == null) return;
                    Settings.BrightnessPerDisplay[deviceId] = brightness;
                    BrightnessChanged?.Invoke(deviceId, brightness);
                });
            }
            catch { }
        }

        private string ResolveMonitorDeviceId(string instanceName)
        {
            // WMI 实例名形如 "DISPLAY\CMM1234\4&..."，显示器的 DeviceId 形如 "MONITOR\CMM1234"，
            // 前缀不同永不相等，统一按硬件 ID 段（如 "CMM1234"）匹配
            string hardwareId = MonitorManager.ExtractHardwareId(instanceName);
            if (hardwareId == null) return null;

            foreach (var monitor in MonitorManager.Monitors)
            {
                if (string.IsNullOrEmpty(monitor.DeviceId)) continue;

                string monitorHwId = MonitorManager.ExtractHardwareId(monitor.DeviceId);
                if (string.Equals(monitorHwId, hardwareId, StringComparison.OrdinalIgnoreCase))
                    return monitor.DeviceId;
            }
            return null;
        }

        private void OnTimeChanged(object sender, EventArgs e)
        {
            if (_exiting || _disposed || !Settings.ScheduleEnabled) return;

            // 用户手动改系统时间或 NTP 同步：重新评估
            _lastScheduleMode = "";
            InvokeOnUIThread(() =>
            {
                try { ScheduleTimer_Tick(null, null); }
                catch { }
            });
        }

        private void InvokeOnUIThread(Action action)
        {
            var form = MainForm;
            if (form != null && !form.IsDisposed)
            {
                try
                {
                    if (form.InvokeRequired) form.Invoke(action);
                    else action();
                    return;
                }
                catch { }
            }
            // 无主窗体或调用失败：直接执行（事件回调可能在任意线程，但 ScheduleTimer_Tick 内部逻辑线程安全）
            try { action(); } catch { }
        }

        private void ApplyScheduleMonitorPresets(ScheduleSegment segment)
        {
            if (segment == null) return;

            if (segment.SyncMode != false)
            {
                if (!_presetService.IsMultiDisplayPreset(segment.PresetName))
                    _displayGammaState.ClearAllDisplayOverrides();
                ApplyGammaToSystem();
                SettingsStore.SaveSettings(Settings);
                return;
            }

            if (segment.MonitorPresets == null || segment.MonitorPresets.Count == 0)
            {
                ApplyGammaToSystem();
                SettingsStore.SaveSettings(Settings);
                return;
            }

            foreach (var monitor in MonitorManager.Monitors)
            {
                if (!segment.MonitorPresets.TryGetValue(monitor.DeviceId, out var presetName))
                    continue;

                if (!_displayGammaState.SetDisplayPreset(monitor.DeviceId, presetName, GammaSource.Schedule))
                    continue;
            }

            ApplyGammaToSystem();
            SettingsStore.SaveSettings(Settings);
        }

        internal string GetNextScheduleInfo()
        {
            if (Settings.ScheduleSegments == null || Settings.ScheduleSegments.Count == 0)
                return "";

            EnsureParsedSegments();
            return _scheduleEvaluator?.GetNextSwitchInfo(DateTime.Now.TimeOfDay) ?? "";
        }

        private void EnsureParsedSegments()
        {
            if (Settings.ScheduleSegments == null)
            {
                _scheduleEvaluator = null;
                _parsedSegmentsHash = 0;
                return;
            }

            int hash = ScheduleEvaluator.ComputeHash(Settings.ScheduleSegments);

            if (_scheduleEvaluator != null && _parsedSegmentsHash == hash)
                return;

            _parsedSegmentsHash = hash;
            _scheduleEvaluator = new ScheduleEvaluator(Settings.ScheduleSegments);
        }

        internal void OnScheduleSegmentChanged()
        {
            _scheduleEvaluator = null;
            _parsedSegmentsHash = 0;
            if (Settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);   // 内部 finally 已调用 ScheduleNextWakeUp
            }
            else
            {
                ScheduleNextWakeUp();   // 即使禁用也要重置间隔（虽然 timer 已停，但保持状态一致）
            }
            SettingsStore.SaveSettings(Settings);
        }

        internal void SetScheduleEnabled(bool enabled)
        {
            Settings.ScheduleEnabled = enabled;
            if (_scheduleTimer != null)
            {
                _scheduleTimer.Enabled = enabled;
                if (enabled) ScheduleNextWakeUp();   // 启用时立即用自适应间隔
            }
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

                // 仅在用户未手动覆盖时恢复定时前的 Gamma 参数
                if (!_scheduleManualOverride)
                {
                    Settings.GammaEnabled = _preScheduleGammaEnabled;
                    Settings.GammaRScale = _preScheduleGammaRScale;
                    Settings.GammaGScale = _preScheduleGammaGScale;
                    Settings.GammaBScale = _preScheduleGammaBScale;
                    Settings.GammaValue = _preScheduleGammaValue;
                    Settings.MasterBrightness = _preScheduleMasterBrightness;
                }

                _scheduleManualOverride = false;
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

                // 显示器重新枚举（屏幕关闭/唤醒可能触发）会使缓存的设备名失效，
                // 导致按显示器的 Gamma 无法重应用。必须立即刷新缓存并重应用，
                // 不能等到用户打开主界面。DisplaySettingsChanged 在后台线程触发，
                // 通过消息窗口投递到 UI 线程处理。
                if (_messageWindow != null && _messageWindow.Handle != IntPtr.Zero)
                {
                    try
                    {
                        NativeMethods.PostMessage(_messageWindow.Handle,
                            NativeMethods.WM_APP_DISPLAY_REFRESH, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch { }
                }
                return;
            }

            var removedIds = _monitorManager.RefreshMonitors();
            bool cleaned = CleanupStaleSettings(removedIds);
            NotifyMonitorChange(_monitorManager.Monitors.Count, removedIds == null ? 0 : removedIds.Count);
            if (!Form1IsOpen())
            {
                ApplyGammaToSystem();
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
            else if (cleaned)
            {
                SettingsStore.SaveSettings(Settings);
            }
        }

        private bool CleanupStaleSettings(HashSet<string> removedDeviceIds)
        {
            if (removedDeviceIds == null || removedDeviceIds.Count == 0)
                return false;

            bool changed = false;

            foreach (var id in removedDeviceIds)
            {
                changed |= Settings.BrightnessPerDisplay.Remove(id);
                changed |= Settings.GammaPerDisplay.Remove(id);
            }

            if (Settings.ScheduleSegments != null)
            {
                foreach (var segment in Settings.ScheduleSegments)
                {
                    if (segment.MonitorPresets != null)
                    {
                        foreach (var id in removedDeviceIds)
                        {
                            changed |= segment.MonitorPresets.Remove(id);
                        }
                        if (segment.MonitorPresets.Count == 0)
                        {
                            segment.MonitorPresets = null;
                            changed = true;
                        }
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
                            changed |= preset.PerDisplaySnapshot.Remove(id);
                        }
                        if (preset.PerDisplaySnapshot.Count == 0)
                        {
                            preset.PerDisplaySnapshot = null;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
                SettingsStore.SaveSettings(Settings);
            return changed;
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
                    {
                        var removedIds = _monitorManager.RefreshMonitors();
                        CleanupStaleSettings(removedIds);
                        ApplyGammaToSystem();
                        _trayMenuNeedsRebuild = true;
                    }
                    else
                    {
                        _monitorManager.ExitLightweightMode();
                    }
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
            _scheduleEvaluator = null;
            _parsedSegmentsHash = 0;
            // 消息窗口保留不销毁：轻量（托盘）模式下仍需接收电源广播，
            // 以便休眠唤醒/屏幕重新上电后重新应用 Gamma
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

        }

        private void HealthCheckTimer_Tick(object sender, EventArgs e)
        {
            if (_exiting || _lightweightMode) return;

            try
            {
                if (GcHelper.DetectLeakSuspect())
                {
                    GcHelper.CollectFull();
                    GcHelper.TrimWorkingSet();
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
                string skippedVersion = await UpdateService.CheckForUpdateAsync(silent, Settings.SkipVersion, token);
                if (skippedVersion != null)
                {
                    Settings.SkipVersion = skippedVersion;
                    SettingsStore.SaveSettings(Settings);
                }
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
            try { SystemEvents.PowerModeChanged -= OnPowerModeChanged; } catch { }
            try { SystemEvents.TimeChanged -= OnTimeChanged; } catch { }

            DisposeTimer(ref _updateCheckTimer);
            DisposeTimer(ref _lightweightEntryTimer);
            DisposeTimer(ref _healthCheckTimer);
            DisposeTimer(ref _gammaReapplyTimer);
            DisposeTimer(ref _gammaWatchdogTimer);
            DisposeTimer(ref _displayChangeDebounceTimer);

            StopBrightnessEventWatcher();

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

            _scheduleEvaluator = null;
            _parsedSegmentsHash = 0;
            MonitorsChanged = null;
            ScheduleStateChanged = null;
            BrightnessChanged = null;

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


            Application.Exit();
        }

        private void PerformEmergencyCleanup()
        {
            try { SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; } catch { }
            try { SystemEvents.PowerModeChanged -= OnPowerModeChanged; } catch { }
            try { SystemEvents.TimeChanged -= OnTimeChanged; } catch { }

            CancelUpdateCheck();

            DisposeTimer(ref _updateCheckTimer);
            DisposeTimer(ref _lightweightEntryTimer);
            DisposeTimer(ref _healthCheckTimer);
            DisposeTimer(ref _gammaReapplyTimer);
            DisposeTimer(ref _gammaWatchdogTimer);
            DisposeTimer(ref _displayChangeDebounceTimer);
            StopBrightnessEventWatcher();

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

            _scheduleEvaluator = null;
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

        }

        #endregion

        #region Message Window

        private class MessageWindow : NativeWindow
        {
            private readonly WeakReference<BackgroundService> _serviceRef;
            private IntPtr _monitorPowerNotifyHandle;

            public MessageWindow(BackgroundService service)
            {
                _serviceRef = new WeakReference<BackgroundService>(service);
                // 隐藏的顶层窗口（不能是 message-only：message-only 窗口收不到广播消息）
                CreateHandle(new CreateParams
                {
                    Caption = "LumiShiftMessageWindow"
                });

                // 注册显示器电源状态通知：电源计划关闭/开启屏幕时收到 WM_POWERBROADCAST
                try
                {
                    var guid = NativeMethods.GUID_MONITOR_POWER_ON;
                    _monitorPowerNotifyHandle = NativeMethods.RegisterPowerSettingNotification(
                        Handle, ref guid, 0);
                }
                catch { }
            }

            public void Dispose()
            {
                if (_monitorPowerNotifyHandle != IntPtr.Zero)
                {
                    try { NativeMethods.UnregisterPowerSettingNotification(_monitorPowerNotifyHandle); }
                    catch { }
                    _monitorPowerNotifyHandle = IntPtr.Zero;
                }
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

                if (m.Msg == NativeMethods.WM_APP_DISPLAY_REFRESH)
                {
                    if (_serviceRef.TryGetTarget(out var service) && !service._exiting)
                        service.OnDisplayChangeMessage();
                    return;
                }

                if (m.Msg == NativeMethods.WM_POWERBROADCAST &&
                    m.WParam.ToInt64() == NativeMethods.PBT_POWERSETTINGCHANGE)
                {
                    HandlePowerSettingChange(m.LParam);
                    m.Result = (IntPtr)1;
                    return;
                }

                base.WndProc(ref m);
            }

            private void HandlePowerSettingChange(IntPtr lParam)
            {
                if (lParam == IntPtr.Zero) return;
                try
                {
                    var setting = (NativeMethods.POWERBROADCAST_SETTING)
                        System.Runtime.InteropServices.Marshal.PtrToStructure(
                            lParam, typeof(NativeMethods.POWERBROADCAST_SETTING));

                    // Data: 0 = 屏幕已关闭, 1 = 屏幕已开启
                    if (setting.PowerSetting == NativeMethods.GUID_MONITOR_POWER_ON &&
                        setting.Data != 0 &&
                        _serviceRef.TryGetTarget(out var service))
                    {
                        service.OnMonitorPowerOn();
                    }
                }
                catch { }
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
