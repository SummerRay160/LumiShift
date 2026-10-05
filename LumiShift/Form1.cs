using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using LumiShift.Controls;
using LumiShift.Infrastructure;
using LumiShift.Models;
using LumiShift.Resources;
using LumiShift.Services;
using Microsoft.Win32;

namespace LumiShift
{
    public partial class Form1 : Form
    {
        internal static Image StaticBackgroundImage { get; set; }
        internal static float StaticBackgroundOpacity { get; set; } = 0.3f;
        internal static bool StaticUseBackgroundImage { get; set; }
        internal static Size StaticFormClientSize { get; set; }
        internal static Bitmap StaticCachedBackground { get; set; }

        private readonly BackgroundService _bgService;
        private readonly DisplaySchemeService _schemeService;

        private FlowLayoutPanel _brightnessPanel;
        private readonly Dictionary<string, Panel> _brightnessRows = new Dictionary<string, Panel>();
        private readonly Dictionary<string, EventHandler> _brightnessSliderHandlers = new Dictionary<string, EventHandler>();
        private ToggleSwitch _gammaCheckBox;
        private ComboBox _gammaModeComboBox;
        private Button _gammaSaveCustomButton;
        private Button _gammaDeleteCustomButton;
        private ModernSlider _gammaColorTempSlider;
        private Label _gammaColorTempLabel;
        private ModernSlider _gammaRSlider;
        private Label _gammaRLabel;
        private ModernSlider _gammaGSlider;
        private Label _gammaGLabel;
        private ModernSlider _gammaBSlider;
        private Label _gammaBLabel;
        private ModernSlider _gammaValueSlider;
        private Label _gammaValueLabel;
        private ModernSlider _gammaBrightSlider;
        private Label _gammaBrightLabel;
        private Label _gammaStatusLabel;
        private ComboBox _monitorSelectorComboBox;
        private Button _resetDisplayGammaButton;
        private ToggleSwitch _scheduleEnabledCheckBox;
        private Button _scheduleConfigButton;
        private ToggleSwitch _gammaScheduleToggle;
        // 进阶调参折叠状态：仅运行时有效，每次启动默认收起
        private bool _gammaAdvancedExpanded;
        private Button _gammaScheduleConfigButton;
        private Label _monitorLabel;
        private Label _gammaTargetLabel;
        private Label _gammaGuideLabel;
        private Label _scheduleQuickLabel;
        private Label _gammaSep2;
        private Label _advToggleLabel;
        private Panel _gammaAdvancedPanel;
        private int _gammaAdvBaseY;
        private ToggleSwitch _startWithWindowsCheckBox;
        private ToggleSwitch _startMinimizedCheckBox;
        private ToggleSwitch _autoCheckUpdatesToggle;
        private ToggleSwitch _restoreGammaToggle;
        private ComboBox _languageComboBox;
        private ToggleSwitch _notificationsEnabledToggle;
        private ToggleSwitch _notifyStartupToggle;
        private ToggleSwitch _notifyScheduleToggle;
        private ToggleSwitch _notifyStatusToggle;
        private ToggleSwitch _notifyMonitorToggle;
        private ToggleSwitch _diagnosticsLoggingToggle;
        private ToggleSwitch _memoryMaintenanceToggle;

        private ToggleSwitch _eyeProtectionToggle;
        private Button _eyeProtectionPreset1Button;
        private Button _eyeProtectionPreset2Button;
        private Button _eyeProtectionPreset3Button;
        private Button _eyeProtectionCustomButton;
        private Button _eyeProtectionRestoreButton;
        private Label _eyeProtectionStatusLabel;

        private ToggleSwitch _bgImageToggle;
        private Button _bgImageSelectButton;
        private Button _bgImageClearButton;
        private Button _resetConfigButton;
        private ModernSlider _bgImageOpacitySlider;
        private Label _bgImageOpacityLabel;
        private Label _bgImageStatusLabel;
        private Image _backgroundImage;
        private Bitmap _cachedBackground;
        private bool _isUpdatingBgImageUI;
        private bool _formDisposed;

        private Timer _resizeDebounceTimer;
        private Timer _brightnessRefreshTimer;
        private int _brightnessRefreshBusy;
        private DateTime _lastBrightnessUserChange = DateTime.MinValue;

        private bool _isUpdatingGammaSliders;
        private bool _isUpdatingBrightness;
        private bool _isPopulatingComboBox;
        private bool _isUpdatingSchedule;
        private bool _isUpdatingLanguage;
        private string _currentPresetName;
        private int _previousMonitorSelectedIndex;
        private Timer _initTimer;

        // 防抖机制
        private Timer _debounceTimer;
        private Action _pendingDebounceAction;

        private void DebounceAction(Action action)
        {
            _pendingDebounceAction = action;
            if (_debounceTimer == null)
            {
                _debounceTimer = new Timer { Interval = 30 };
                _debounceTimer.Tick += (s, e) =>
                {
                    _debounceTimer.Stop();
                    var actionToRun = _pendingDebounceAction;
                    _pendingDebounceAction = null;
                    actionToRun?.Invoke();
                };
            }
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        public Form1(BackgroundService bgService)
        {
            DoubleBuffered = true;
            _bgService = bgService;
            _schemeService = new DisplaySchemeService(bgService.Settings);
            InitializeComponent();
            InitializeApp();
        }

        private UserSettings Settings => _bgService.Settings;
        private GammaController GammaCtrl => _bgService.GammaController;
        private MonitorManager MonitorMgr => _bgService.MonitorManager;

        private void InitializeApp()
        {
            _bgService.GammaController.StatusChanged += OnGammaStatusChanged;
            _bgService.MonitorsChanged += OnMonitorsChanged;
            _bgService.ScheduleStateChanged += OnScheduleStateChanged;
            _bgService.BrightnessChanged += OnBrightnessChanged;

            PopulatePresetComboBox();
            UpdateGammaUI();
            _bgService.ApplyGammaToSystem();
            UpdateBrightnessUI();
            StartBrightnessRefreshTimer();
            UpdateScheduleUI();
            UpdateStartupUI();
            UpdateEyeProtectionUI();
            UpdateBgImageUI();
            LoadBackgroundImage();
            SyncBackgroundStaticFields();
            ClientSizeChanged += OnFormClientSizeChanged;

            _initTimer = new Timer { Interval = 100 };
            _initTimer.Tick += (s, e) =>
            {
                _initTimer.Stop();
                _initTimer.Dispose();
                _initTimer = null;
                if (!IsDisposed) UpdateScheduleOverrideStatus();
            };
            _initTimer.Start();
        }

        #region Preset Helpers

        private string GetCurrentPresetName()
        {
            return _bgService.GetCurrentPresetName();
        }

        private string GetMonitorPresetName(string deviceId)
        {
            return _bgService.GetMonitorPresetName(deviceId);
        }

        private void PopulatePresetComboBox()
        {
            _isPopulatingComboBox = true;
            _gammaModeComboBox.Items.Clear();
            foreach (var scheme in _schemeService.GetSchemes())
                _gammaModeComboBox.Items.Add(scheme.DisplayName);

            string current = GetCurrentPresetName();
            if (current != null)
            {
                _gammaModeComboBox.SelectedItem = _schemeService.GetDisplayName(current);
                _currentPresetName = current;
            }
            _isPopulatingComboBox = false;
        }

        private void RefreshCustomPresetButtons()
        {
            string selectedName = _gammaModeComboBox.SelectedItem is string selected
                ? DisplaySchemeService.StripDisplayName(selected)
                : null;
            bool any = Settings.CustomGammaPresets.Count > 0;
            _gammaSaveCustomButton.Enabled = Settings.GammaEnabled;
            _gammaDeleteCustomButton.Enabled = any && selectedName != null && !PresetDefinitions.IsBuiltIn(selectedName);
        }

        #endregion

        #region UI Updates

        private void UpdateGammaUI()
        {
            _isUpdatingGammaSliders = true;

            bool supported = GammaCtrl.IsSupported;
            _gammaCheckBox.Enabled = supported;

            PopulateMonitorSelector();

            if (supported)
            {
                _gammaCheckBox.Checked = Settings.GammaEnabled;

                SyncSlidersToSelectedMonitor();

                string current = GetCurrentPresetName();
                _currentPresetName = current;
                if (current != null && !_isPopulatingComboBox)
                    _gammaModeComboBox.SelectedItem = _schemeService.GetDisplayName(current);

                UpdateGammaLabels();
                UpdateColorTempFromSliders();
                RefreshSliderVisibility();
                RefreshCustomPresetButtons();
            }

            _isUpdatingGammaSliders = false;
        }

        private void PopulateMonitorSelector()
        {
            // 按 DeviceId 恢复选中：显示器重枚举后顺序可能变化，按序号恢复会静默选中另一台
            string prevDeviceId = _monitorSelectorComboBox.SelectedIndex > 0
                ? GetSelectedMonitorDeviceId()
                : null;
            _monitorSelectorComboBox.Items.Clear();
            _monitorSelectorComboBox.Items.Add(Lang.Get("所有显示器"));
            foreach (var monitor in MonitorMgr.Monitors)
            {
                string label = monitor.DisplayName;
                if (_bgService.HasDisplayGammaOverride(monitor.DeviceId))
                {
                    string sourceTag = _bgService.GetDisplayGammaSource(monitor.DeviceId) == GammaSourceNames.Schedule ? Lang.Get("定时") : Lang.Get("手动");
                    label += Lang.F(" · 单独设置/{0}", sourceTag);
                }
                else
                {
                    label += Lang.Get(" · 跟随全部");
                }
                _monitorSelectorComboBox.Items.Add(label);
            }
            int restore = 0;
            if (prevDeviceId != null)
            {
                int i = 1;
                foreach (var monitor in MonitorMgr.Monitors)
                {
                    if (monitor.DeviceId == prevDeviceId)
                    {
                        restore = i;
                        break;
                    }
                    i++;
                }
            }
            _monitorSelectorComboBox.SelectedIndex = restore;
            UpdateGammaTargetHint();
        }

        /// <summary>动态提示当前调的是哪块屏幕，避免"改了 A 却以为在改 B"。</summary>
        private void UpdateGammaTargetHint()
        {
            string deviceId = IsGlobalMonitorSelected() ? null : GetSelectedMonitorDeviceId();
            if (deviceId == null)
            {
                _gammaTargetLabel.Text = Lang.Get("正在调整所有屏幕");
                return;
            }

            string name = deviceId;
            foreach (var monitor in MonitorMgr.Monitors)
            {
                if (monitor.DeviceId == deviceId)
                {
                    name = monitor.DisplayName;
                    break;
                }
            }

            if (!_bgService.HasDisplayGammaOverride(deviceId))
                _gammaTargetLabel.Text = Lang.F("正在调整「{0}」· 只对这台生效", name);
            else if (_bgService.GetDisplayGammaSource(deviceId) == GammaSourceNames.Schedule)
                _gammaTargetLabel.Text = Lang.F("「{0}」正由定时切换单独控制", name);
            else
                _gammaTargetLabel.Text = Lang.F("「{0}」正在单独设置 · 点“跟随全部”恢复一致", name);
        }

        private void SyncSlidersToSelectedMonitor()
        {
            if (IsGlobalMonitorSelected())
            {
                _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(Settings.GammaRScale * 100.0));
                _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(Settings.GammaGScale * 100.0));
                _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(Settings.GammaBScale * 100.0));
                _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(Settings.GammaValue * 100.0));
                _gammaBrightSlider.Value = ClampSlider(_gammaBrightSlider, Settings.MasterBrightness);
                _gammaCheckBox.Checked = Settings.GammaEnabled;
                _resetDisplayGammaButton.Enabled = false;
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    var config = _bgService.GetEffectiveGammaParameters(deviceId);
                    _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(config.RScale * 100.0));
                    _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(config.GScale * 100.0));
                    _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(config.BScale * 100.0));
                    _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(config.GammaValue * 100.0));
                    _gammaBrightSlider.Value = ClampSlider(_gammaBrightSlider, config.MasterBrightness);
                    _gammaCheckBox.Checked = config.Enabled;
                    _resetDisplayGammaButton.Enabled = _bgService.HasDisplayGammaOverride(deviceId);
                }
            }

            UpdateGammaTargetHint();
        }

        private static int ClampSlider(ModernSlider slider, int val)
        {
            return Math.Max(slider.Minimum, Math.Min(slider.Maximum, val));
        }

        private void RefreshSliderVisibility()
        {
            bool enabled;
            if (IsGlobalMonitorSelected())
            {
                enabled = Settings.GammaEnabled;
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                enabled = deviceId != null ? _bgService.GetEffectiveGammaParameters(deviceId).Enabled : Settings.GammaEnabled;
            }

            _gammaModeComboBox.Enabled = enabled;
            _gammaColorTempSlider.Enabled = enabled;
            _gammaRSlider.Enabled = enabled;
            _gammaGSlider.Enabled = enabled;
            _gammaBSlider.Enabled = enabled;
            _gammaValueSlider.Enabled = enabled;
            _gammaBrightSlider.Enabled = enabled;
        }

        /// <summary>
        /// 按折叠状态重排"进阶调参"下方控件。基础区（启用/亮度/色温）位置固定，
        /// 折叠只影响进阶面板与底部范围/定时区的纵坐标。
        /// </summary>
        private void ApplyGammaAdvancedLayout()
        {
            // 折叠状态仅运行时有效：每次启动默认收起，会话内可展开
            bool adv = _gammaAdvancedExpanded;
            _gammaAdvancedPanel.Visible = adv;
            _advToggleLabel.Text = (adv ? "▾ " : "▸ ") + Lang.Get("进阶调参 · R/G/B/γ 与显示方案");

            int y = _gammaAdvBaseY + (adv ? _gammaAdvancedPanel.Height + 8 : 0);

            _gammaSep2.Location = new Point(Spacing.LG, y);
            y += 12;

            _monitorLabel.Location = new Point(Spacing.LG, y + 2);
            _monitorSelectorComboBox.Location = new Point(72, y);
            _resetDisplayGammaButton.Location = new Point(262, y);
            y += 30;

            _scheduleQuickLabel.Location = new Point(Spacing.LG, y + 2);
            _gammaScheduleToggle.Location = new Point(92, y);
            _gammaScheduleConfigButton.Location = new Point(146, y);
            y += 30;

            _gammaTargetLabel.Location = new Point(Spacing.LG, y);
            y += 20;
            _gammaGuideLabel.Location = new Point(Spacing.LG, y);
            // 说明文字高度按文本换行动态计算（多语言长度不同），不能用固定步进
            y += _gammaGuideLabel.Height + 2;
            _gammaStatusLabel.Location = new Point(Spacing.LG, y);
        }

        private void AdvToggleLabel_Click(object sender, EventArgs e)
        {
            _gammaAdvancedExpanded = !_gammaAdvancedExpanded;
            ApplyGammaAdvancedLayout();
        }

        private void UpdateGammaLabels()
        {
            _gammaRLabel.Text = $"{_gammaRSlider.Value / 100.0:F2}";
            _gammaGLabel.Text = $"{_gammaGSlider.Value / 100.0:F2}";
            _gammaBLabel.Text = $"{_gammaBSlider.Value / 100.0:F2}";
            _gammaValueLabel.Text = $"{_gammaValueSlider.Value / 100.0:F2}";
            _gammaBrightLabel.Text = $"{_gammaBrightSlider.Value}%";
        }

        private void UpdateScheduleOverrideStatus()
        {
            if (!Settings.ScheduleEnabled)
            {
                _gammaStatusLabel.Text = "";
                UpdateTitleBar();
                return;
            }

            if (_bgService.ScheduleManualOverride)
            {
                string nextInfo = _bgService.GetNextScheduleInfo();
                _gammaStatusLabel.Text = string.IsNullOrEmpty(nextInfo)
                    ? Lang.Get("手动调整已覆盖定时设置，下次时段切换时恢复定时")
                    : Lang.F("手动调整已覆盖定时设置，{0}恢复定时", nextInfo);
            }
            else
            {
                string currentPreset = GetCurrentPresetName() ?? Lang.Get("自定义");
                _gammaStatusLabel.Text = Lang.F("定时运行中: 当前方案 \"{0}\"", Lang.Get(currentPreset));
            }

            UpdateTitleBar();
        }

        private void UpdateTitleBar()
        {
            Text = _bgService.GetScheduleStatusText();
        }

        private void UpdateColorTempFromSliders()
        {
            double r = _gammaRSlider.Value / 100.0;
            double b = _gammaBSlider.Value / 100.0;
            double colorTemp;

            if (r <= 1.0 && b >= 1.0)
            {
                double t = r <= 0.98 ? 0 : (1.0 - r) / 0.02;
                colorTemp = Math.Max(0, (1.0 - t) * 50);
            }
            else if (r >= 1.0 && b <= 1.0)
            {
                double t = b >= 0.70 ? (1.0 - b) / 0.30 : 1.0;
                colorTemp = Math.Min(100, 50 + t * 50);
            }
            else
            {
                colorTemp = 50;
            }

            _gammaColorTempSlider.Value = (int)Math.Round(Math.Max(0, Math.Min(100, colorTemp)));
            UpdateColorTempLabel();
        }

        private void UpdateColorTempLabel()
        {
            int val = _gammaColorTempSlider.Value;
            if (val <= 20) _gammaColorTempLabel.Text = Lang.Get("偏冷");
            else if (val <= 40) _gammaColorTempLabel.Text = Lang.Get("微冷");
            else if (val <= 60) _gammaColorTempLabel.Text = Lang.Get("适中");
            else if (val <= 80) _gammaColorTempLabel.Text = Lang.Get("微暖");
            else _gammaColorTempLabel.Text = Lang.Get("偏暖");
        }

        private GammaConfig BuildColorTempConfig(int colorTemp, int brightness)
        {
            double r, b, g;

            if (colorTemp <= 50)
            {
                double t = colorTemp / 50.0;
                r = 0.95 + (1.0 - 0.95) * t;
                b = 1.06 + (1.0 - 1.06) * t;
                g = 0.92 + (1.0 - 0.92) * t;
            }
            else
            {
                double t = (colorTemp - 50) / 50.0;
                r = 1.0 + (1.08 - 1.0) * t;
                b = 1.0 + (0.70 - 1.0) * t;
                g = 1.0 + (1.08 - 1.0) * t;
            }

            return new GammaConfig
            {
                RScale = Math.Round(r, 2),
                GScale = 1.0,
                BScale = Math.Round(b, 2),
                GammaValue = Math.Round(g, 2),
                MasterBrightness = brightness,
                Enabled = true
            };
        }

        private GammaConfig BuildSliderGammaConfig(bool enabled)
        {
            return new GammaConfig
            {
                RScale = _gammaRSlider.Value / 100.0,
                GScale = _gammaGSlider.Value / 100.0,
                BScale = _gammaBSlider.Value / 100.0,
                GammaValue = _gammaValueSlider.Value / 100.0,
                MasterBrightness = _gammaBrightSlider.Value,
                Enabled = enabled
            };
        }

        private bool IsGlobalMonitorSelected()
        {
            return _monitorSelectorComboBox.SelectedIndex <= 0;
        }

        private string GetSelectedMonitorDeviceId()
        {
            int idx = _monitorSelectorComboBox.SelectedIndex - 1;
            if (idx < 0 || idx >= MonitorMgr.Monitors.Count)
                return null;
            return MonitorMgr.Monitors[idx].DeviceId;
        }

        private void UpdateBrightnessUI()
        {
            _isUpdatingBrightness = true;
            var activeDeviceIds = new HashSet<string>();

            foreach (var monitor in MonitorMgr.Monitors)
            {
                string deviceId = monitor.DeviceId;
                activeDeviceIds.Add(deviceId);

                int currentBrightness = 50;
                if (Settings.BrightnessPerDisplay.ContainsKey(deviceId))
                {
                    currentBrightness = Settings.BrightnessPerDisplay[deviceId];
                }
                else if (monitor.Controller != null && monitor.Controller.IsSupported)
                {
                    try
                    {
                        int hardware = monitor.Controller.GetBrightness();
                        if (hardware >= 0)
                        {
                            currentBrightness = hardware;
                            Settings.BrightnessPerDisplay[deviceId] = currentBrightness;
                        }
                    }
                    catch { }
                }

                if (_brightnessRows.TryGetValue(deviceId, out var row))
                {
                    var slider = (ModernSlider)row.Controls[1];
                    var valLabel = (Label)row.Controls[2];
                    var nameLabel = (Label)row.Controls[0];

                    nameLabel.Text = monitor.DisplayName;
                    if (!(monitor.Controller?.IsSupported ?? true))
                        nameLabel.Text = monitor.DisplayName + Lang.Get(" (不支持硬件亮度调节)");
                    row.Width = _brightnessPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
                    nameLabel.Width = row.Width - row.Padding.Horizontal;
                    slider.Maximum = 100;
                    slider.Minimum = 0;
                    slider.Value = currentBrightness;
                    slider.Enabled = monitor.Controller?.IsSupported ?? false;
                    valLabel.Text = $"{currentBrightness}%";
                    int sliderWidth = Math.Max(210, row.Width - row.Padding.Horizontal - 66);
                    slider.Width = sliderWidth;
                    int sliderX = row.Padding.Left;
                    slider.Location = new Point(sliderX, row.Padding.Top + 32);
                    valLabel.Location = new Point(sliderX + slider.Width + 12, row.Padding.Top + 36);
                }
                else
                {
                    row = new Panel
                    {
                        Width = _brightnessPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2,
                        Height = 74,
                        Padding = new Padding(0, 10, Spacing.SM, 8),
                        BackColor = Color.Transparent,
                        Tag = deviceId
                    };

                    var nameLabel = new Label
                    {
                        Text = monitor.DisplayName,
                        AutoSize = false,
                        Width = row.Width - row.Padding.Horizontal,
                        Height = 20,
                        Font = Typography.BodyBold,
                        ForeColor = Colors.TextSecondary
                    };
                    if (!(monitor.Controller?.IsSupported ?? true))
                        nameLabel.Text = monitor.DisplayName + Lang.Get(" (不支持硬件亮度调节)");
                    nameLabel.Location = new Point(row.Padding.Left, row.Padding.Top);

                    int sliderWidth = Math.Max(210, row.Width - row.Padding.Horizontal - 66);

                    var tb = new ModernSlider
                    {
                        Minimum = 0,
                        Maximum = 100,
                        Width = sliderWidth,
                        Value = currentBrightness,
                        Enabled = monitor.Controller?.IsSupported ?? false
                    };

                    var valLabel = new Label
                    {
                        Text = $"{currentBrightness}%",
                        AutoSize = true,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Font = Typography.Mono,
                        ForeColor = Colors.TextPrimary
                    };

                    int sliderX = row.Padding.Left;
                    tb.Location = new Point(sliderX, row.Padding.Top + 32);
                    valLabel.Location = new Point(sliderX + tb.Width + 12, row.Padding.Top + 36);

                    EventHandler handler = (s, ev) =>
                    {
                        if (_isUpdatingBrightness) return;
                        _lastBrightnessUserChange = DateTime.Now;
                        valLabel.Text = $"{tb.Value}%";
                        Settings.BrightnessPerDisplay[deviceId] = tb.Value;
                        monitor.Controller?.SetBrightness(tb.Value);
                        SettingsStore.SaveSettings(Settings);
                    };
                    tb.ValueChanged += handler;
                    _brightnessSliderHandlers[deviceId] = handler;

                    row.Controls.AddRange(new Control[] { nameLabel, tb, valLabel });
                    _brightnessRows[deviceId] = row;
                    _brightnessPanel.Controls.Add(row);
                }
            }

            _isUpdatingBrightness = false;

            var removedIds = new List<string>();
            foreach (var kvp in _brightnessRows)
            {
                if (!activeDeviceIds.Contains(kvp.Key))
                {
                    if (_brightnessSliderHandlers.TryGetValue(kvp.Key, out var handler))
                    {
                        if (kvp.Value.Controls.Count > 1 && kvp.Value.Controls[1] is ModernSlider slider)
                            slider.ValueChanged -= handler;
                        _brightnessSliderHandlers.Remove(kvp.Key);
                    }
                    _brightnessPanel.Controls.Remove(kvp.Value);
                    kvp.Value.Dispose();
                    removedIds.Add(kvp.Key);
                }
            }
            foreach (var id in removedIds)
                _brightnessRows.Remove(id);
        }

        #region 亮度实时刷新

        /// <summary>把单个显示器的亮度写回 UI（滑块+百分比标签），返回值是否真的改变。
        /// 仅更新 UI，不碰 Settings/持久化（由调用方决定是否写盘）。row 缺失或正在更新时返回 false。</summary>
        private bool TrySetBrightnessSlider(string deviceId, int value, out bool changed)
        {
            changed = false;
            if (_formDisposed || IsDisposed || _isUpdatingBrightness) return false;
            if (!_brightnessRows.TryGetValue(deviceId, out var row) || row.Controls.Count < 3) return false;

            var slider = row.Controls[1] as ModernSlider;
            var valLabel = row.Controls[2] as Label;
            if (slider == null || valLabel == null) return false;

            changed = slider.Value != value;
            if (changed)
            {
                _isUpdatingBrightness = true;
                try { slider.Value = value; }
                catch { }
                finally { _isUpdatingBrightness = false; }
            }
            valLabel.Text = $"{value}%";
            return true;
        }

        /// <summary>系统亮度被外部修改（Fn 键/系统设置等）时同步 UI（事件来自 WMI，在 UI 线程触发）。</summary>
        private void OnBrightnessChanged(string deviceId, int brightness)
        {
            if (!TrySetBrightnessSlider(deviceId, brightness, out _)) return;
            // 外部快速连续调节（如按住 Fn 键）时避免频繁写盘
            DebounceAction(() => SettingsStore.SaveSettings(Settings));
        }

        private void StartBrightnessRefreshTimer()
        {
            if (_brightnessRefreshTimer != null) return;
            _brightnessRefreshTimer = new Timer { Interval = 2000 };
            _brightnessRefreshTimer.Tick += BrightnessRefreshTimer_Tick;
            _brightnessRefreshTimer.Start();
        }

        private void StopBrightnessRefreshTimer()
        {
            if (_brightnessRefreshTimer == null) return;
            _brightnessRefreshTimer.Stop();
            _brightnessRefreshTimer.Dispose();
            _brightnessRefreshTimer = null;
        }

        private void BrightnessRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_formDisposed || IsDisposed || !Visible) return;
            if (_isUpdatingBrightness) return;
            // 用户刚拖动过滑块时暂不刷新，避免打断操作
            if ((DateTime.Now - _lastBrightnessUserChange).TotalMilliseconds < 3000) return;
            if (System.Threading.Interlocked.CompareExchange(ref _brightnessRefreshBusy, 1, 0) != 0) return;

            // 在 UI 线程先快照显示器列表，避免后台枚举时集合被并发修改
            var snapshot = new List<MonitorInfo>();
            foreach (var m in MonitorMgr.Monitors)
            {
                if (m != null && m.Controller != null && m.Controller.IsSupported)
                    snapshot.Add(m);
            }
            if (snapshot.Count == 0)
            {
                System.Threading.Interlocked.Exchange(ref _brightnessRefreshBusy, 0);
                return;
            }

            // 硬件亮度查询（WMI/DDC）可能较慢，放到后台线程执行
            System.Threading.Tasks.Task.Run(() =>
            {
                var values = new List<KeyValuePair<string, int>>();
                foreach (var monitor in snapshot)
                {
                    try
                    {
                        int brightness = monitor.Controller.GetBrightness();
                        values.Add(new KeyValuePair<string, int>(monitor.DeviceId, brightness));
                    }
                    catch { }
                }

                try
                {
                    BeginInvoke(new Action(() => ApplyRefreshedBrightness(values)));
                }
                catch
                {
                    System.Threading.Interlocked.Exchange(ref _brightnessRefreshBusy, 0);
                }
            });
        }

        private void ApplyRefreshedBrightness(List<KeyValuePair<string, int>> values)
        {
            System.Threading.Interlocked.Exchange(ref _brightnessRefreshBusy, 0);

            if (_formDisposed || IsDisposed || _isUpdatingBrightness || !Visible) return;

            bool changed = false;
            foreach (var kvp in values)
            {
                // 读取失败（返回 -1）的显示器不更新，避免假值刷掉真实状态
                if (kvp.Value < 0) continue;

                if (TrySetBrightnessSlider(kvp.Key, kvp.Value, out var rowChanged) && rowChanged)
                {
                    // 外部（Fn 键/系统设置等）修改了亮度：同步滑块与设置
                    changed = true;
                    Settings.BrightnessPerDisplay[kvp.Key] = kvp.Value;
                }
            }

            if (changed)
                SettingsStore.SaveSettings(Settings);
        }

        #endregion

        private void UpdateScheduleUI()
        {
            _isUpdatingSchedule = true;

            _scheduleEnabledCheckBox.Checked = Settings.ScheduleEnabled;
            _gammaScheduleToggle.Checked = Settings.ScheduleEnabled;

            _isUpdatingSchedule = false;
        }

        private void UpdateStartupUI()
        {
            _startWithWindowsCheckBox.Checked = Settings.StartWithWindows;
            _startMinimizedCheckBox.Checked = Settings.StartMinimized;
            _autoCheckUpdatesToggle.Checked = Settings.AutoCheckUpdates;
            _restoreGammaToggle.Checked = Settings.RestoreGammaOnExit;
            _diagnosticsLoggingToggle.Checked = Settings.DiagnosticsLoggingEnabled;
            _memoryMaintenanceToggle.Checked = Settings.MemoryMaintenanceEnabled;

            _isUpdatingLanguage = true;
            _languageComboBox.SelectedIndex =
                string.Equals(Settings.Language, "zh", StringComparison.OrdinalIgnoreCase) ? 1 :
                string.Equals(Settings.Language, "zh-Hant", StringComparison.OrdinalIgnoreCase) ? 2 :
                string.Equals(Settings.Language, "en", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
            _isUpdatingLanguage = false;

            UpdateNotificationUI();
        }

        private void LanguageComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingLanguage) return;

            int idx = _languageComboBox.SelectedIndex;
            Settings.Language = idx == 1 ? "zh" : idx == 2 ? "zh-Hant" : idx == 3 ? "en" : "";
            SettingsStore.SaveSettings(Settings);

            // ponytail: 不用 Application.Restart()——它与 Program 的单实例 Mutex 零超时探测竞态，
            // 新进程起在旧进程释放锁之前必被判定为重复实例而自退。改为提示用户手动重启。
            MessageBox.Show(this,
                Lang.Get("语言已保存，重启 LumiShift 后生效。请手动重启应用。"),
                Lang.Get("重启确认"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AutoCheckUpdatesToggle_CheckedChanged(object sender, EventArgs e)
        {
            Settings.AutoCheckUpdates = _autoCheckUpdatesToggle.Checked;
            DebounceAction(() => SettingsStore.SaveSettings(Settings));
        }

        private void UpdateNotificationUI()
        {
            _notificationsEnabledToggle.Checked = Settings.NotificationsEnabled;
            _notifyStartupToggle.Checked = Settings.NotifyStartup;
            _notifyScheduleToggle.Checked = Settings.NotifyScheduleSwitch;
            _notifyStatusToggle.Checked = Settings.NotifyStatusSwitch;
            _notifyMonitorToggle.Checked = Settings.NotifyMonitorChange;
            _notifyStartupToggle.Enabled = Settings.NotificationsEnabled;
            _notifyScheduleToggle.Enabled = Settings.NotificationsEnabled;
            _notifyStatusToggle.Enabled = Settings.NotificationsEnabled;
            _notifyMonitorToggle.Enabled = Settings.NotificationsEnabled;
        }

        private void RestoreGammaToggle_CheckedChanged(object sender, EventArgs e)
        {
            Settings.RestoreGammaOnExit = _restoreGammaToggle.Checked;
            DebounceAction(() => SettingsStore.SaveSettings(Settings));
        }

        private void DiagnosticsLoggingToggle_CheckedChanged(object sender, EventArgs e)
        {
            Settings.DiagnosticsLoggingEnabled = _diagnosticsLoggingToggle.Checked;

            // 即时生效，无需重启；开启动作与会话头会立刻落盘
            Log.SetEnabled(Settings.DiagnosticsLoggingEnabled);
            if (Settings.DiagnosticsLoggingEnabled)
                Log.WriteSessionHeader("手动开启");

            DebounceAction(() => SettingsStore.SaveSettings(Settings));
        }

        private void MemoryMaintenanceToggle_CheckedChanged(object sender, EventArgs e)
        {
            Settings.MemoryMaintenanceEnabled = _memoryMaintenanceToggle.Checked;

            // 即时生效，无需重启
            _bgService.SetMemoryMaintenance(Settings.MemoryMaintenanceEnabled);

            DebounceAction(() => SettingsStore.SaveSettings(Settings));
        }

        private void NotificationsEnabledToggle_CheckedChanged(object sender, EventArgs e)
        {
            Settings.NotificationsEnabled = _notificationsEnabledToggle.Checked;
            UpdateNotificationUI();
            DebounceAction(() => SettingsStore.SaveSettings(Settings));
        }

        private void ApplyToggleSetting(ToggleSwitch toggle, Action<bool> apply)
        {
            apply(toggle.Checked);
            DebounceAction(() => SettingsStore.SaveSettings(Settings));
        }

        private void NotifyScheduleToggle_CheckedChanged(object sender, EventArgs e)
            => ApplyToggleSetting(_notifyScheduleToggle, v => Settings.NotifyScheduleSwitch = v);

        private void NotifyStartupToggle_CheckedChanged(object sender, EventArgs e)
            => ApplyToggleSetting(_notifyStartupToggle, v => Settings.NotifyStartup = v);

        private void NotifyStatusToggle_CheckedChanged(object sender, EventArgs e)
            => ApplyToggleSetting(_notifyStatusToggle, v => Settings.NotifyStatusSwitch = v);

        private void NotifyMonitorToggle_CheckedChanged(object sender, EventArgs e)
            => ApplyToggleSetting(_notifyMonitorToggle, v => Settings.NotifyMonitorChange = v);

        private void UpdateEyeProtectionUI()
        {
            _eyeProtectionToggle.Checked = Settings.EyeProtectionEnabled;
            _eyeProtectionStatusLabel.Text = Settings.EyeProtectionEnabled
                ? Lang.F("已启用 (R:{0} G:{1} B:{2})", Settings.EyeProtectionRed, Settings.EyeProtectionGreen, Settings.EyeProtectionBlue)
                : Lang.Get("未启用");

            if (Settings.EyeProtectionEnabled)
            {
                EyeProtectionService.ApplyColor(Settings.EyeProtectionRed, Settings.EyeProtectionGreen, Settings.EyeProtectionBlue);
            }
        }

        private void EyeProtectionToggle_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = _eyeProtectionToggle.Checked;
            Settings.EyeProtectionEnabled = _eyeProtectionToggle.Checked;
            UpdateEyeProtectionUI();
            DebounceAction(() =>
            {
                if (Settings.EyeProtectionEnabled)
                    EyeProtectionService.ApplyColor(Settings.EyeProtectionRed, Settings.EyeProtectionGreen, Settings.EyeProtectionBlue);
                else
                    EyeProtectionService.RestoreDefault();
                SettingsStore.SaveSettings(Settings);
                _bgService.NotifyStatusSwitch(Lang.Get("LumiShift 状态切换"), enabled ? Lang.Get("护眼模式已启用") : Lang.Get("护眼模式已关闭"));
            });
        }

        private void EyeProtectionPresetButton_Click(object sender, EventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is ValueTuple<string, int, int, int> preset))
                return;

            var (_, r, g, b) = preset;
            ApplyEyeProtectionColor(r, g, b);
        }

        private void EyeProtectionCustomButton_Click(object sender, EventArgs e)
        {
            using (var cd = new ColorDialog())
            {
                cd.Color = Color.FromArgb(Settings.EyeProtectionRed, Settings.EyeProtectionGreen, Settings.EyeProtectionBlue);
                cd.FullOpen = true;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    ApplyEyeProtectionColor(cd.Color.R, cd.Color.G, cd.Color.B);
                }
            }
        }

        private void EyeProtectionRestoreButton_Click(object sender, EventArgs e)
        {
            Settings.EyeProtectionEnabled = false;
            _eyeProtectionToggle.Checked = false;
            EyeProtectionService.RestoreDefault();
            UpdateEyeProtectionUI();
            SettingsStore.SaveSettings(Settings);
        }

        private void ApplyEyeProtectionColor(int r, int g, int b)
        {
            Settings.EyeProtectionRed = r;
            Settings.EyeProtectionGreen = g;
            Settings.EyeProtectionBlue = b;
            Settings.EyeProtectionEnabled = true;

            _eyeProtectionToggle.Checked = true;
            EyeProtectionService.ApplyColor(r, g, b);
            UpdateEyeProtectionUI();
            SettingsStore.SaveSettings(Settings);
        }

        private void UpdateBgImageUI()
        {
            if (_isUpdatingBgImageUI) return;
            _bgImageToggle.Checked = Settings.UseBackgroundImage;
            _bgImageOpacitySlider.Value = Settings.BackgroundImageOpacity;
            _bgImageOpacityLabel.Text = $"{Settings.BackgroundImageOpacity}%";

            if (!string.IsNullOrEmpty(Settings.BackgroundImageFile))
            {
                _bgImageStatusLabel.Text = Settings.BackgroundImageFile;
            }
            else
            {
                _bgImageStatusLabel.Text = Lang.Get("未设置");
            }
        }

        private void LoadBackgroundImage()
        {
            if (_backgroundImage != null)
            {
                StaticBackgroundImage = null;
                _backgroundImage.Dispose();
                _backgroundImage = null;
            }

            if (!Settings.UseBackgroundImage || string.IsNullOrEmpty(Settings.BackgroundImageFile))
            {
                _bgImageStatusLabel.Text = Lang.Get("未设置");
                SyncBackgroundStaticFields();
                RebuildBackgroundCache();
                InvalidateBackgroundDisplay();
                return;
            }

            try
            {
                string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string bgDir = System.IO.Path.Combine(exeDir, "bg_img");
                string filePath = System.IO.Path.Combine(bgDir, Settings.BackgroundImageFile);

                if (System.IO.File.Exists(filePath))
                {
                    using (var tempImage = Image.FromFile(filePath))
                    {
                        int maxDimension = 1920;
                        int imgW = tempImage.Width;
                        int imgH = tempImage.Height;

                        if (imgW > maxDimension || imgH > maxDimension)
                        {
                            float scale = Math.Min((float)maxDimension / imgW, (float)maxDimension / imgH);
                            int newW = (int)(imgW * scale);
                            int newH = (int)(imgH * scale);
                            _backgroundImage = new Bitmap(newW, newH);
                            using (var g = Graphics.FromImage(_backgroundImage))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.DrawImage(tempImage, 0, 0, newW, newH);
                            }
                        }
                        else
                        {
                            _backgroundImage = new Bitmap(tempImage);
                        }
                    }
                    _bgImageStatusLabel.Text = Settings.BackgroundImageFile;
                }
                else
                {
                    _bgImageStatusLabel.Text = Lang.Get("文件不存在");
                }
            }
            catch (Exception ex)
            {
                _bgImageStatusLabel.Text = Lang.F("加载失败: {0}", ex.Message);
            }

            SyncBackgroundStaticFields();
            RebuildBackgroundCache();
            InvalidateBackgroundDisplay();
        }

        private void BgImageToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingBgImageUI) return;
            Settings.UseBackgroundImage = _bgImageToggle.Checked;
            DebounceAction(() =>
            {
                LoadBackgroundImage();
                SettingsStore.SaveSettings(Settings);
            });
        }

        private void BgImageSelectButton_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = Lang.Get("图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*");
                ofd.Title = Lang.Get("选择背景图片");
                ofd.RestoreDirectory = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        string bgDir = System.IO.Path.Combine(exeDir, "bg_img");

                        if (!System.IO.Directory.Exists(bgDir))
                        {
                            System.IO.Directory.CreateDirectory(bgDir);
                        }

                        string fileName = System.IO.Path.GetFileName(ofd.FileName);
                        string destPath = System.IO.Path.Combine(bgDir, fileName);

                        string fullSourcePath = System.IO.Path.GetFullPath(ofd.FileName);
                        string fullDestPath = System.IO.Path.GetFullPath(destPath);

                        if (!string.Equals(fullSourcePath, fullDestPath, System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (_backgroundImage != null)
                            {
                                StaticBackgroundImage = null;
                                _backgroundImage.Dispose();
                                _backgroundImage = null;
                            }

                            System.IO.File.Copy(ofd.FileName, destPath, true);
                        }

                        if (!string.Equals(Settings.BackgroundImageFile, fileName, StringComparison.OrdinalIgnoreCase))
                            DeleteCurrentBackgroundFile();

                        Settings.BackgroundImageFile = fileName;
                        Settings.UseBackgroundImage = true;

                        _isUpdatingBgImageUI = true;
                        _bgImageToggle.Checked = true;
                        _isUpdatingBgImageUI = false;

                        LoadBackgroundImage();
                        SettingsStore.SaveSettings(Settings);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Lang.F("设置背景图片失败: {0}", ex.Message), Lang.Get("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BgImageClearButton_Click(object sender, EventArgs e)
        {
            DeleteCurrentBackgroundFile();
            Settings.BackgroundImageFile = "";
            Settings.UseBackgroundImage = false;
            _bgImageToggle.Checked = false;
            if (_backgroundImage != null)
            {
                StaticBackgroundImage = null;
                _backgroundImage.Dispose();
                _backgroundImage = null;
            }
            UpdateBgImageUI();
            LoadBackgroundImage();
            SettingsStore.SaveSettings(Settings);
        }

        private void DeleteCurrentBackgroundFile()
        {
            if (string.IsNullOrEmpty(Settings.BackgroundImageFile))
                return;

            try
            {
                string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string bgDir = System.IO.Path.Combine(exeDir, "bg_img");
                string filePath = System.IO.Path.Combine(bgDir, Settings.BackgroundImageFile);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch { }
        }

        private void ResetConfigButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this,
                Lang.Get("将删除 AppData 下的全部配置（设置、备份与日志），并恢复默认设置。") + "\n\n" + Lang.Get("是否继续？"),
                Lang.Get("删除配置"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            bool deleted = _bgService.ResetToDefaults();
            UpdateStartupUI();
            UpdateScheduleUI();
            UpdateEyeProtectionUI();
            UpdateBgImageUI();
            UpdateBrightnessUI();

            if (deleted)
            {
                MessageBox.Show(this,
                    Lang.Get("已删除配置并恢复默认设置。界面语言将在重启后生效。"),
                    Lang.Get("删除配置"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    Lang.Get("部分配置文件删除失败（可能被其他程序占用），已恢复默认设置。界面语言将在重启后生效。"),
                    Lang.Get("删除配置"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BgImageOpacitySlider_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingBgImageUI) return;
            Settings.BackgroundImageOpacity = _bgImageOpacitySlider.Value;
            _bgImageOpacityLabel.Text = $"{_bgImageOpacitySlider.Value}%";
            SyncBackgroundStaticFields();
            RebuildBackgroundCache();
            InvalidateBackgroundDisplay();
            SettingsStore.SaveSettings(Settings);
        }

        private void ApplyTheme()
        {
            RefreshTabTheme();
            RefreshControlTreeTheme(this);
            foreach (Control c in Controls) { c.Invalidate(); }
            Invalidate(true);
        }

        private void RefreshControlTreeTheme(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c == _gammaAdvancedPanel)
                {
                    c.BackColor = Colors.Background;
                }
                else if (c is Label lbl)
                {
                    if (lbl.Tag is char role)
                        ApplyLabelTheme(lbl, role);
                    else
                        lbl.ForeColor = Colors.TextPrimary;
                }
                else if (c is TableLayoutPanel)
                {
                    c.BackColor = Color.Transparent;
                }
                else if (c is ComboBox cb)
                {
                    cb.BackColor = Colors.Surface;
                    cb.ForeColor = Colors.TextPrimary;
                }
                else if (c is ListBox lb)
                {
                    lb.BackColor = Colors.Surface;
                    lb.ForeColor = Colors.TextSecondary;
                }
                else if (c is DateTimePicker dtp)
                {
                    dtp.BackColor = Colors.Surface;
                    dtp.ForeColor = Colors.TextPrimary;
                }
                else if (c is CheckBox chk)
                {
                    chk.ForeColor = Colors.TextSecondary;
                    chk.BackColor = Color.Transparent;
                }
                else if (c is Button btn)
                {
                    if (btn == _gammaSaveCustomButton)
                    {
                        btn.BackColor = Colors.Brand;
                        btn.ForeColor = Color.White;
                    }
                    else if (btn == _gammaDeleteCustomButton)
                    {
                        btn.BackColor = Colors.Surface;
                        btn.ForeColor = Colors.Red;
                    }
                    else if (btn == _eyeProtectionPreset1Button ||
                             btn == _eyeProtectionPreset2Button ||
                             btn == _eyeProtectionPreset3Button)
                    {
                        if (btn.Tag is ValueTuple<string, int, int, int> preset)
                        {
                            var (_, r, g, b) = preset;
                            btn.BackColor = Color.FromArgb(r, g, b);
                            int luminance = (int)(r * 0.299 + g * 0.587 + b * 0.114);
                            btn.ForeColor = luminance > 140 ? Color.Black : Color.White;
                        }
                    }
                    else if (btn == _eyeProtectionCustomButton || btn == _eyeProtectionRestoreButton)
                    {
                        btn.BackColor = Colors.Surface;
                        btn.ForeColor = btn == _eyeProtectionRestoreButton ? Colors.Red : Colors.TextPrimary;
                    }
                    else if (btn == _bgImageSelectButton)
                    {
                        btn.BackColor = Colors.Surface;
                        btn.ForeColor = Colors.TextPrimary;
                    }
                    else if (btn == _bgImageClearButton)
                    {
                        btn.BackColor = Colors.Surface;
                        btn.ForeColor = Colors.Red;
                    }
                    else if (btn == _resetConfigButton)
                    {
                        btn.BackColor = Colors.Surface;
                        btn.ForeColor = Colors.Red;
                    }
                    else if (btn.Tag as string == "resetDisplayGamma")
                    {
                        btn.BackColor = Colors.Surface;
                        btn.ForeColor = Colors.TextSecondary;
                    }
                    else
                    {
                        btn.BackColor = Colors.Surface;
                        btn.ForeColor = Colors.TextPrimary;
                    }
                }
                else if (c is ToggleSwitch || c is ModernSlider)
                {
                    c.Invalidate();
                }

                if (c.HasChildren)
                    RefreshControlTreeTheme(c);
            }
        }

        private void SyncSlidersToSettings()
        {
            _isUpdatingGammaSliders = true;
            SyncSlidersToSelectedMonitor();
            UpdateGammaLabels();
            UpdateColorTempFromSliders();
            if (_currentPresetName != null && _gammaModeComboBox.Items.Contains(_currentPresetName))
                _gammaModeComboBox.SelectedItem = _currentPresetName;
            else if (_currentPresetName != null && _gammaModeComboBox.Items.Contains(_schemeService.GetDisplayName(_currentPresetName)))
                _gammaModeComboBox.SelectedItem = _schemeService.GetDisplayName(_currentPresetName);
            else
            {
                string current = GetCurrentPresetName();
                if (current != null)
                    _gammaModeComboBox.SelectedItem = _schemeService.GetDisplayName(current);
            }
            RefreshSliderVisibility();
            RefreshCustomPresetButtons();
            _isUpdatingGammaSliders = false;
        }

        #endregion

        private void GammaCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;
            bool enabled = _gammaCheckBox.Checked;

            if (IsGlobalMonitorSelected())
            {
                var config = GammaConfig.FromSettings(Settings);
                config.Enabled = enabled;
                _bgService.SetGlobalGammaParameters(config, clearDisplayOverrides: true);
                PopulateMonitorSelector();
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    var config = _bgService.GetEffectiveGammaParameters(deviceId);
                    config.Enabled = enabled;
                    _bgService.SetDisplayGammaParameters(deviceId, config);
                    PopulateMonitorSelector();
                }
            }

            if (Settings.ScheduleEnabled)
                _bgService.SetScheduleManualOverride(true);

            RefreshSliderVisibility();
            DebounceAction(() =>
            {
                _bgService.ApplyGammaToSystem();
                SettingsStore.SaveSettings(Settings);
                _bgService.UpdateTrayMenu();
                UpdateScheduleOverrideStatus();
                _bgService.NotifyStatusSwitch(Lang.Get("LumiShift 状态切换"), enabled ? Lang.Get("显示调节已启用") : Lang.Get("显示调节已关闭"));
            });
        }

        private void GammaColorTempSlider_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;

            if (Settings.ScheduleEnabled)
                _bgService.SetScheduleManualOverride(true);

            _isUpdatingGammaSliders = true;

            var colorConfig = BuildColorTempConfig(_gammaColorTempSlider.Value, _gammaBrightSlider.Value);

            if (IsGlobalMonitorSelected())
            {
                _bgService.SetGlobalGammaParameters(colorConfig, clearDisplayOverrides: true);
                PopulateMonitorSelector();
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    _bgService.SetDisplayGammaParameters(deviceId, colorConfig);
                    PopulateMonitorSelector();
                    _resetDisplayGammaButton.Enabled = true;
                }
            }

            _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(Settings.GammaRScale * 100.0));
            _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(Settings.GammaGScale * 100.0));
            _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(Settings.GammaBScale * 100.0));
            _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(Settings.GammaValue * 100.0));

            UpdateGammaLabels();
            UpdateColorTempLabel();
            SyncSlidersToSelectedMonitor();

            string current = GetCurrentPresetName();
            _currentPresetName = current;
            if (current != null)
                _gammaModeComboBox.SelectedItem = _schemeService.GetDisplayName(current);

            _bgService.ApplyGammaToSystem();
            SettingsStore.SaveSettings(Settings);

            _isUpdatingGammaSliders = false;
        }

        private void GammaModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders || _isPopulatingComboBox) return;

            if (!(_gammaModeComboBox.SelectedItem is string selectedDisplay)) return;
            string selected = DisplaySchemeService.StripDisplayName(selectedDisplay);

            _currentPresetName = selected;

            if (Settings.ScheduleEnabled)
                _bgService.SetScheduleManualOverride(true);

            if (IsGlobalMonitorSelected())
            {
                _bgService.TryApplyPreset(selected);
                _bgService.SetGlobalGammaParameters(GammaConfig.FromSettings(Settings), clearDisplayOverrides: true);
                PopulateMonitorSelector();
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    _bgService.ApplyPresetToMonitor(selected, deviceId);
                    if (Settings.GammaPerDisplay.ContainsKey(deviceId))
                        PopulateMonitorSelector();
                }
            }

            SyncSlidersToSettings();
            _bgService.ApplyGammaToSystem();
            SettingsStore.SaveSettings(Settings);
            _bgService.UpdateTrayMenu();
            UpdateScheduleOverrideStatus();
        }

        private void GammaSlider_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;

            if (Settings.ScheduleEnabled)
                _bgService.SetScheduleManualOverride(true);

            UpdateGammaLabels();

            if (IsGlobalMonitorSelected())
            {
                _bgService.SetGlobalGammaParameters(BuildSliderGammaConfig(_gammaCheckBox.Checked), clearDisplayOverrides: true);
                PopulateMonitorSelector();
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    _bgService.SetDisplayGammaParameters(deviceId, BuildSliderGammaConfig(_gammaCheckBox.Checked));
                    PopulateMonitorSelector();
                    _resetDisplayGammaButton.Enabled = true;
                }
            }

            UpdateColorTempFromSliders();
            string current = GetCurrentPresetName();
            _currentPresetName = current;
            if (current != null && !_isPopulatingComboBox)
                _gammaModeComboBox.SelectedItem = _schemeService.GetDisplayName(current);

            if (GammaCtrl.IsSupported)
                _bgService.ApplyGammaToSystem();

            SettingsStore.SaveSettings(Settings);
            UpdateScheduleOverrideStatus();
        }

        private void GammaSaveCustomButton_Click(object sender, EventArgs e)
        {
            bool hasDisplayOverrides = Settings.GammaPerDisplay != null && Settings.GammaPerDisplay.Count > 0;
            string name;
            bool saveMultiDisplay;
            using (var dialog = new SaveDisplaySchemeDialog(hasDisplayOverrides))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                name = dialog.SchemeName;
                saveMultiDisplay = dialog.SchemeKind == DisplaySchemeKind.MultiDisplay;
            }

            if (PresetDefinitions.IsBuiltIn(name))
            {
                MessageBox.Show(Lang.Get("该名称与内置方案冲突，请更换名称。"), Lang.Get("提示"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var existing = Settings.CustomGammaPresets.FirstOrDefault(cp => cp.Name == name);
            if (existing != null)
            {
                if (MessageBox.Show(Lang.F("显示方案 \"{0}\" 已存在，是否覆盖？", name), Lang.Get("确认覆盖"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                Settings.CustomGammaPresets.Remove(existing);
            }

            var preset = new GammaPreset
            {
                Name = name,
                RScale = Settings.GammaRScale,
                GScale = Settings.GammaGScale,
                BScale = Settings.GammaBScale,
                GammaValue = Settings.GammaValue,
                MasterBrightness = Settings.MasterBrightness,
                Enabled = Settings.GammaEnabled
            };

            if (saveMultiDisplay)
            {
                preset.PerDisplaySnapshot = new Dictionary<string, PerDisplayGamma>();
                foreach (var kvp in Settings.GammaPerDisplay)
                {
                    preset.PerDisplaySnapshot[kvp.Key] = new PerDisplayGamma
                    {
                        RScale = kvp.Value.RScale,
                        GScale = kvp.Value.GScale,
                        BScale = kvp.Value.BScale,
                        GammaValue = kvp.Value.GammaValue,
                        MasterBrightness = kvp.Value.MasterBrightness,
                        Enabled = kvp.Value.Enabled,
                        Source = kvp.Value.Source
                    };
                }
            }

            Settings.CustomGammaPresets.Add(preset);

            PopulatePresetComboBox();
            _currentPresetName = name;
            _gammaModeComboBox.SelectedItem = _schemeService.GetDisplayName(name);
            SettingsStore.SaveSettings(Settings);
            _bgService.UpdateTrayMenu();
            RefreshCustomPresetButtons();

            _gammaStatusLabel.Text = saveMultiDisplay
                ? Lang.F("已保存多屏方案 \"{0}\"", name)
                : Lang.F("已保存统一方案 \"{0}\"", name);
        }

        private void GammaDeleteCustomButton_Click(object sender, EventArgs e)
        {
            if (!(_gammaModeComboBox.SelectedItem is string selectedDisplay)) return;
            string selected = DisplaySchemeService.StripDisplayName(selectedDisplay);
            if (PresetDefinitions.IsBuiltIn(selected)) return;

            if (MessageBox.Show(Lang.F("确定要删除显示方案 \"{0}\" 吗？", selected), Lang.Get("确认删除"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            bool wasActive = GetCurrentPresetName() == selected;
            Settings.CustomGammaPresets.RemoveAll(cp => cp.Name == selected);

            PopulatePresetComboBox();

            if (wasActive)
            {
                _currentPresetName = PresetDefinitions.BuiltIns[0].Name;
                _bgService.TryApplyPreset(PresetDefinitions.BuiltIns[0].Name);
                SyncSlidersToSettings();
                _bgService.ApplyGammaToSystem();
            }

            SettingsStore.SaveSettings(Settings);
            _bgService.UpdateTrayMenu();
            RefreshCustomPresetButtons();

            _gammaStatusLabel.Text = Lang.F("已删除显示方案 \"{0}\"", selected);
        }

        private void MonitorSelectorComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;

            int prevIndex = _previousMonitorSelectedIndex;
            _previousMonitorSelectedIndex = _monitorSelectorComboBox.SelectedIndex;

            if (IsGlobalMonitorSelected() && _bgService.HasAnyDisplayGammaOverride())
            {
                bool hasManual = _bgService.HasManualDisplayGammaOverride();
                bool hasSchedule = _bgService.HasScheduleDisplayGammaOverride();

                if (hasManual || hasSchedule)
                {
                    // ponytail: resx 属性无法存真实换行，\n\n 在代码侧拼接
                    string msg = (hasSchedule && Settings.ScheduleEnabled
                            ? Lang.Get("切换到“全部显示器”会把当前效果作为统一方案应用，并清除定时带来的单屏设置。")
                            : Lang.Get("切换到“全部显示器”会把当前效果作为统一方案应用，并清除各显示器的独立设置。"))
                        + "\n\n" + Lang.Get("是否继续？");

                    if (MessageBox.Show(msg, Lang.Get("同步确认"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        _isUpdatingGammaSliders = true;
                        _monitorSelectorComboBox.SelectedIndex = prevIndex;
                        _isUpdatingGammaSliders = false;
                        return;
                    }
                }

                var primary = MonitorMgr.Monitors.FirstOrDefault(m => m.Screen?.Primary == true)
                    ?? MonitorMgr.Monitors.FirstOrDefault();

                if (primary != null && _bgService.HasDisplayGammaOverride(primary.DeviceId))
                {
                    _bgService.SetGlobalGammaParameters(_bgService.GetEffectiveGammaParameters(primary.DeviceId), clearDisplayOverrides: false);
                }

                _bgService.SetGlobalGammaParameters(GammaConfig.FromSettings(Settings), clearDisplayOverrides: true);
                _bgService.ApplyGammaToSystem();
                SettingsStore.SaveSettings(Settings);
                PopulateMonitorSelector();
            }

            _isUpdatingGammaSliders = true;
            SyncSlidersToSelectedMonitor();
            UpdateGammaLabels();
            UpdateColorTempFromSliders();
            RefreshSliderVisibility();
            _isUpdatingGammaSliders = false;
        }

        private void ResetDisplayGammaButton_Click(object sender, EventArgs e)
        {
            string deviceId = GetSelectedMonitorDeviceId();
            if (deviceId == null) return;

            bool isFromSchedule = _bgService.GetDisplayGammaSource(deviceId) == GammaSourceNames.Schedule;

            if (isFromSchedule && Settings.ScheduleEnabled)
            {
                if (MessageBox.Show(
                    Lang.Get("此显示器当前由定时切换单独控制。恢复跟随后，它会先使用统一方案；下次时段切换时会再次按调度应用。")
                    + "\n\n" + Lang.Get("是否继续？"),
                    Lang.Get("恢复跟随统一方案"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                    return;
            }

            _bgService.ClearDisplayGammaOverride(deviceId);
            PopulateMonitorSelector();

            _isUpdatingGammaSliders = true;
            SyncSlidersToSelectedMonitor();
            UpdateGammaLabels();
            UpdateColorTempFromSliders();
            _isUpdatingGammaSliders = false;

            _bgService.ApplyGammaToSystem();
            SettingsStore.SaveSettings(Settings);
            _bgService.UpdateTrayMenu();

            _gammaStatusLabel.Text = isFromSchedule && Settings.ScheduleEnabled
                ? Lang.Get("已恢复跟随统一方案；下次时段切换会继续按调度应用")
                : Lang.Get("已恢复跟随统一方案");
        }

        private void GammaScheduleToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            _scheduleEnabledCheckBox.Checked = _gammaScheduleToggle.Checked;
            DebounceAction(() =>
            {
                _bgService.SetScheduleEnabled(_gammaScheduleToggle.Checked);
                UpdateScheduleOverrideStatus();
            });
        }

        private void ScheduleEnabledCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            _gammaScheduleToggle.Checked = _scheduleEnabledCheckBox.Checked;
            DebounceAction(() =>
            {
                _bgService.SetScheduleEnabled(_scheduleEnabledCheckBox.Checked);
                UpdateScheduleOverrideStatus();
            });
        }

        private void ScheduleConfigButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new ScheduleConfigForm(Settings.ScheduleSegments, Settings.CustomGammaPresets, MonitorMgr.Monitors.ToList()))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultSegments != null)
                {
                    Settings.ScheduleSegments = dlg.ResultSegments;
                    _bgService.OnScheduleSegmentChanged();
                }
            }
            GC.Collect(1, GCCollectionMode.Forced, false);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
            GcHelper.TrimWorkingSet();
        }

        private void StartWithWindowsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.StartWithWindows = _startWithWindowsCheckBox.Checked;
            DebounceAction(() =>
            {
                _bgService.UpdateStartupRegistry();
                SettingsStore.SaveSettings(Settings);
            });
        }

        private void StartMinimizedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.StartMinimized = _startMinimizedCheckBox.Checked;
            DebounceAction(() => SettingsStore.SaveSettings(Settings));
        }

        private void OnGammaStatusChanged(object sender, string status)
        {
            if (_formDisposed || IsDisposed) return;
            if (InvokeRequired)
            {
                try { Invoke(new Action(() => { if (!_formDisposed && !IsDisposed) _gammaStatusLabel.Text = status; })); }
                catch { }
            }
            else
            {
                _gammaStatusLabel.Text = status;
            }
        }

        private void OnMonitorsChanged()
        {
            if (_formDisposed || IsDisposed) return;
            UpdateBrightnessUI();
            UpdateGammaUI();
            _bgService.ApplyGammaToSystem();
        }

        private void OnScheduleStateChanged()
        {
            if (_formDisposed || IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(OnScheduleStateChanged)); }
                catch { }
                return;
            }
            SyncSlidersToSettings();
            UpdateScheduleOverrideStatus();
            PopulateMonitorSelector();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_bgService.IsExiting)
            {
                _formDisposed = true;
                _resizeDebounceTimer?.Stop();
                _initTimer?.Stop();
                _debounceTimer?.Stop();
                StopBrightnessRefreshTimer();
                UnsubscribeEvents();
                _bgService.OnFormClosing(this);
                e.Cancel = true;
                Hide();
                BeginInvoke(new Action(() =>
                {
                    _bgService.EnterAppLightweightMode();
                    if (!IsDisposed) Dispose();
                }));
                return;
            }
            _formDisposed = true;
            _resizeDebounceTimer?.Stop();
            _initTimer?.Stop();
            _debounceTimer?.Stop();
            StopBrightnessRefreshTimer();
            UnsubscribeEvents();
            _bgService.OnFormClosing(this);
            base.OnFormClosing(e);
        }

        private void UnsubscribeEvents()
        {
            _bgService.GammaController.StatusChanged -= OnGammaStatusChanged;
            _bgService.MonitorsChanged -= OnMonitorsChanged;
            _bgService.ScheduleStateChanged -= OnScheduleStateChanged;
            _bgService.BrightnessChanged -= OnBrightnessChanged;
            ClientSizeChanged -= OnFormClientSizeChanged;
            if (_tabControl != null)
                _tabControl.TabSelected -= OnTabSelected;
        }

        internal static void CleanupStaticFields()
        {
            var oldBg = StaticBackgroundImage;
            StaticBackgroundImage = null;
            oldBg?.Dispose();

            var oldCached = StaticCachedBackground;
            StaticCachedBackground = null;
            oldCached?.Dispose();

            StaticBackgroundOpacity = 0.3f;
            StaticUseBackgroundImage = false;
            StaticFormClientSize = default;
        }

        private static void DrawOpacityBlit(Graphics g, Image src, int w, int h, float opacity)
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                {
                    new float[] { 1, 0, 0, 0, 0 },
                    new float[] { 0, 1, 0, 0, 0 },
                    new float[] { 0, 0, 1, 0, 0 },
                    new float[] { 0, 0, 0, opacity, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });
                attributes.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                int imgW = src.Width;
                int imgH = src.Height;
                float scale = Math.Max((float)w / imgW, (float)h / imgH);
                int drawW = (int)(imgW * scale);
                int drawH = (int)(imgH * scale);
                int x = (w - drawW) / 2;
                int y = (h - drawH) / 2;
                g.DrawImage(src, new Rectangle(x, y, drawW, drawH), 0, 0, imgW, imgH, GraphicsUnit.Pixel, attributes);
            }
        }

        internal static Bitmap CreateBackgroundBitmap(Size targetSize)
        {
            if (StaticBackgroundImage == null || !StaticUseBackgroundImage)
                return null;

            int w = targetSize.Width;
            int h = targetSize.Height;
            if (w <= 0 || h <= 0) return null;

            float opacity = StaticBackgroundOpacity;

            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                DrawOpacityBlit(g, StaticBackgroundImage, w, h, opacity);
            }

            return bmp;
        }

        private void RebuildBackgroundCache()
        {
            if (_formDisposed) return;

            if (_backgroundImage == null || !Settings.UseBackgroundImage)
            {
                _lastRebuildCacheSize = default;
                _lastRebuildCacheOpacity = 0;
                _lastCachedBackgroundImage = null;
                _cachedBackground?.Dispose();
                _cachedBackground = null;

                var oldStaticCached = StaticCachedBackground;
                StaticCachedBackground = null;
                oldStaticCached?.Dispose();

                var oldFormBg2 = BackgroundImage;
                BackgroundImage = null;
                oldFormBg2?.Dispose();
                UpdateTabPageBackgrounds();
                return;
            }

            int formW = ClientSize.Width;
            int formH = ClientSize.Height;
            if (formW <= 0 || formH <= 0) return;

            float opacity = Settings.BackgroundImageOpacity / 100f;
            var currentSize = new Size(formW, formH);

            if (_cachedBackground != null
                && _lastRebuildCacheSize == currentSize
                && Math.Abs(_lastRebuildCacheOpacity - opacity) < 0.001f
                && _lastCachedBackgroundImage == _backgroundImage)
                return;

            _lastRebuildCacheSize = currentSize;
            _lastRebuildCacheOpacity = opacity;
            _lastCachedBackgroundImage = _backgroundImage;

            _cachedBackground?.Dispose();
            _cachedBackground = null;

            Bitmap newCached = null;
            try
            {
                newCached = new Bitmap(formW, formH);
                using (var g = Graphics.FromImage(newCached))
                {
                    DrawOpacityBlit(g, _backgroundImage, formW, formH, opacity);
                }
            }
            catch
            {
                newCached?.Dispose();
                return;
            }

            _cachedBackground = newCached;

            var oldStaticCachedBg = StaticCachedBackground;
            StaticCachedBackground = _cachedBackground;
            if (oldStaticCachedBg != null && oldStaticCachedBg != _cachedBackground)
                oldStaticCachedBg.Dispose();

            var oldFormBg = BackgroundImage;
            BackgroundImage = _cachedBackground;
            oldFormBg?.Dispose();
            BackgroundImageLayout = ImageLayout.Center;
            UpdateTabPageBackgrounds();
        }

        private Bitmap _sharedTabPageBg;
        private float _lastTabPageBgOpacity;
        private Size _lastTabPageBgSize;
        private Size _lastRebuildCacheSize;
        private float _lastRebuildCacheOpacity;
        private Image _lastCachedBackgroundImage;
        private Image _lastTabPageBackgroundImage;

        private void UpdateTabPageBackgrounds()
        {
            if (_formDisposed || _tabControl == null) return;
            if (_cachedBackground == null || _backgroundImage == null)
            {
                foreach (TabPage page in _tabControl.TabPages)
                {
                    var oldBg = page.BackgroundImage;
                    page.BackgroundImage = null;
                    oldBg?.Dispose();
                }
                _sharedTabPageBg?.Dispose();
                _sharedTabPageBg = null;
                _lastTabPageBackgroundImage = null;
                return;
            }

            int tabHeaderHeight = _tabControl.ItemSize.Height + 5;
            int controlW = _tabControl.ClientSize.Width;
            int controlH = Math.Max(_tabControl.ClientSize.Height - tabHeaderHeight, 100);

            float opacity = Settings.BackgroundImageOpacity / 100f;
            Size tabPageSize = new Size(controlW, controlH);

            if (_sharedTabPageBg == null ||
                Math.Abs(_lastTabPageBgOpacity - opacity) > 0.001f ||
                _lastTabPageBgSize != tabPageSize ||
                _lastTabPageBackgroundImage != _backgroundImage)
            {
                _sharedTabPageBg?.Dispose();
                _sharedTabPageBg = null;

                int pageW = Math.Max(tabPageSize.Width, 1);
                int pageH = Math.Max(tabPageSize.Height, 1);

                _sharedTabPageBg = new Bitmap(pageW, pageH);
                using (var g = Graphics.FromImage(_sharedTabPageBg))
                {
                    DrawOpacityBlit(g, _backgroundImage, pageW, pageH, opacity);
                }

                _lastTabPageBgOpacity = opacity;
                _lastTabPageBgSize = tabPageSize;
                _lastTabPageBackgroundImage = _backgroundImage;
            }

            foreach (TabPage page in _tabControl.TabPages)
            {
                var oldBg = page.BackgroundImage;
                if (oldBg != null && oldBg != _sharedTabPageBg)
                    oldBg.Dispose();
                page.BackgroundImage = _sharedTabPageBg;
                page.BackgroundImageLayout = ImageLayout.None;
            }
        }

        private void OnFormClientSizeChanged(object sender, EventArgs e)
        {
            if (_formDisposed) return;
            if (_resizeDebounceTimer == null)
            {
                _resizeDebounceTimer = new Timer { Interval = 150 };
                _resizeDebounceTimer.Tick += (s, ev) =>
                {
                    _resizeDebounceTimer.Stop();
                    if (_formDisposed || IsDisposed) return;
                    SyncBackgroundStaticFields();
                    RebuildBackgroundCache();
                    InvalidateBackgroundDisplay();
                };
            }
            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Start();
        }

        private void InvalidateBackgroundDisplay()
        {
            if (_formDisposed || IsDisposed) return;
            Invalidate();
            foreach (TabPage page in _tabControl.TabPages)
            {
                page.Invalidate();
            }
            _tabControl.Invalidate();
        }

        private void SyncBackgroundStaticFields()
        {
            if (_formDisposed) return;
            StaticBackgroundImage = _backgroundImage;
            StaticBackgroundOpacity = Settings.BackgroundImageOpacity / 100f;
            StaticUseBackgroundImage = Settings.UseBackgroundImage;
            StaticFormClientSize = ClientSize;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyTheme();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 首次显示后立即刷新一次硬件亮度（窗体打开前外部可能有修改）
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed && _brightnessRefreshTimer != null)
                    BrightnessRefreshTimer_Tick(null, EventArgs.Empty);
            }));

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || _backgroundImage == null || !Settings.UseBackgroundImage) return;
                SyncBackgroundStaticFields();
                RebuildBackgroundCache();
                InvalidateBackgroundDisplay();

                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || _backgroundImage == null || !Settings.UseBackgroundImage) return;
                    UpdateTabPageBackgrounds();
                    InvalidateBackgroundDisplay();
                }));
            }));
        }

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCRBUTTONUP = 0x00A5;
        private const int HTCAPTION = 2;
        private const int HTCLOSE = 20;
        private const int HTMINBUTTON = 8;
        private const int HTSYSMENU = 3;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                Point screenPoint = new Point(
                    unchecked((short)((long)m.LParam & 0xFFFF)),
                    unchecked((short)(((long)m.LParam >> 16) & 0xFFFF)));
                Point clientPoint = PointToClient(screenPoint);

                if (clientPoint.Y < 0)
                {
                    int btnW = 50;
                    int rightEdge = ClientSize.Width;

                    if (clientPoint.X >= rightEdge - btnW)
                    {
                        m.Result = (IntPtr)HTCLOSE;
                    }
                    else if (clientPoint.X >= rightEdge - btnW * 2 && MinimizeBox)
                    {
                        m.Result = (IntPtr)HTMINBUTTON;
                    }
                    else if (clientPoint.X <= btnW)
                    {
                        m.Result = (IntPtr)HTSYSMENU;
                    }
                    else
                    {
                        m.Result = (IntPtr)HTCAPTION;
                    }
                    return;
                }

                base.WndProc(ref m);
                return;
            }

            if (m.Msg == WM_NCRBUTTONUP)
            {
                int hitTest = m.WParam.ToInt32();
                if (hitTest == HTCAPTION || hitTest == HTSYSMENU)
                {
                    Point cursorPos = Cursor.Position;
                    IntPtr sysMenu = NativeMethods.GetSystemMenu(Handle, false);
                    int cmd = NativeMethods.TrackPopupMenu(sysMenu,
                        NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_TOPALIGN,
                        cursorPos.X, cursorPos.Y, 0, Handle, IntPtr.Zero);
                    if (cmd != 0)
                    {
                        NativeMethods.SendMessage(Handle, NativeMethods.WM_SYSCOMMAND,
                            (IntPtr)cmd, IntPtr.Zero);
                    }
                    return;
                }
            }

            base.WndProc(ref m);
        }
    }
}
