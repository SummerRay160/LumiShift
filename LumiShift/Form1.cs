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

        private GammaController _gammaController;
        private MonitorManager _monitorManager;
        private UserSettings _settings;

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;

        private FlowLayoutPanel _brightnessPanel;
        private ToggleSwitch _gammaCheckBox;
        private CheckBox _gammaSimplifiedCheckBox;
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
        private Button _gammaScheduleConfigButton;
        private ToggleSwitch _startWithWindowsCheckBox;
        private ToggleSwitch _startMinimizedCheckBox;
        private ComboBox _themeComboBox;

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
        private ModernSlider _bgImageOpacitySlider;
        private Label _bgImageOpacityLabel;
        private Label _bgImageStatusLabel;
        private Image _backgroundImage;
        private bool _isUpdatingBgImageUI;

        private bool _isUpdatingGammaSliders;
        private bool _isPopulatingComboBox;
        private bool _isUpdatingSchedule;
        private Timer _scheduleTimer;
        private string _lastScheduleMode;
        private bool _scheduleManualOverride;
        private string _currentPresetName;

        private static Icon LoadAppIcon()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "LumiShift.app.ico";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                    return new Icon(stream);
            }
            return SystemIcons.Application;
        }

        public Form1()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void InitializeApp()
        {
            _settings = SettingsStore.LoadSettings();
            _monitorManager = new MonitorManager();
            _gammaController = new GammaController();

            _gammaController.StatusChanged += OnGammaStatusChanged;
            _monitorManager.MonitorsChanged += OnMonitorsChanged;

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            ThemeManager.CurrentMode = (ThemeMode)_settings.ThemeMode;
            ThemeManager.UpdateActiveTheme();
            ThemeManager.ThemeChanged += OnThemeChanged;

            _lastScheduleMode = "";
            _scheduleTimer = new Timer { Interval = 30000 };
            _scheduleTimer.Tick += ScheduleTimer_Tick;

            PopulatePresetComboBox();
            UpdateGammaUI();
            ApplyGammaToSystem();
            UpdateBrightnessUI();
            UpdateScheduleUI();
            UpdateStartupUI();
            UpdateThemeUI();
            UpdateEyeProtectionUI();
            UpdateBgImageUI();
            LoadBackgroundImage();
            SyncBackgroundStaticFields();
            SubscribeBackgroundPaintEvents();
            UpdateTrayMenu();

            var updateTimer = new Timer { Interval = 3000 };
            updateTimer.Tick += (s, e) =>
            {
                updateTimer.Stop();
                updateTimer.Dispose();
                UpdateService.CheckForUpdate(silent: true);
            };
            updateTimer.Start();
        }

        #region Preset Helpers

        private string GetCurrentPresetName()
        {
            if (!_settings.GammaEnabled)
                return PresetDefinitions.BuiltIns[0].Name;

            foreach (var bip in PresetDefinitions.BuiltIns)
            {
                if (bip.Matches(_settings.GammaRScale, _settings.GammaGScale,
                    _settings.GammaBScale, _settings.GammaValue, _settings.MasterBrightness))
                    return bip.Name;
            }

            foreach (var cp in _settings.CustomGammaPresets)
            {
                if (Math.Abs(_settings.GammaRScale - cp.RScale) < 0.01 &&
                    Math.Abs(_settings.GammaGScale - cp.GScale) < 0.01 &&
                    Math.Abs(_settings.GammaBScale - cp.BScale) < 0.01 &&
                    Math.Abs(_settings.GammaValue - cp.GammaValue) < 0.01 &&
                    Math.Abs(_settings.MasterBrightness - cp.MasterBrightness) <= 1)
                    return cp.Name;
            }

            return null;
        }

        private string GetMonitorPresetName(string deviceId)
        {
            if (_settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
            {
                if (!pdg.Enabled) return PresetDefinitions.BuiltIns[0].Name;

                foreach (var bip in PresetDefinitions.BuiltIns)
                {
                    if (bip.Matches(pdg.RScale, pdg.GScale, pdg.BScale, pdg.GammaValue, pdg.MasterBrightness))
                        return bip.Name;
                }
                foreach (var cp in _settings.CustomGammaPresets)
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

        private void PopulatePresetComboBox()
        {
            _isPopulatingComboBox = true;
            _gammaModeComboBox.Items.Clear();
            foreach (var p in PresetDefinitions.GetNames())
                _gammaModeComboBox.Items.Add(p);
            foreach (var cp in _settings.CustomGammaPresets)
                _gammaModeComboBox.Items.Add(cp.Name);

            string current = GetCurrentPresetName();
            if (current != null)
            {
                _gammaModeComboBox.SelectedItem = current;
                _currentPresetName = current;
            }
            _isPopulatingComboBox = false;
        }

        private void RefreshCustomPresetButtons()
        {
            bool any = _settings.CustomGammaPresets.Count > 0;
            _gammaSaveCustomButton.Enabled = _settings.GammaEnabled && !_gammaSimplifiedCheckBox.Checked;
            _gammaDeleteCustomButton.Enabled = any && _gammaModeComboBox.SelectedItem is string selected && !PresetDefinitions.IsBuiltIn(selected);
        }

        private bool TryApplyPreset(string name)
        {
            var builtIn = PresetDefinitions.GetByName(name);
            if (builtIn != null)
            {
                _settings.GammaEnabled = builtIn.Enabled;
                _settings.GammaRScale = builtIn.RScale;
                _settings.GammaGScale = builtIn.GScale;
                _settings.GammaBScale = builtIn.BScale;
                _settings.GammaValue = builtIn.GammaValue;
                _settings.MasterBrightness = builtIn.MasterBrightness;
                return true;
            }

            var custom = _settings.CustomGammaPresets.FirstOrDefault(cp => cp.Name == name);
            if (custom != null)
            {
                _settings.GammaEnabled = custom.Enabled;
                _settings.GammaRScale = custom.RScale;
                _settings.GammaGScale = custom.GScale;
                _settings.GammaBScale = custom.BScale;
                _settings.GammaValue = custom.GammaValue;
                _settings.MasterBrightness = custom.MasterBrightness;

                if (custom.PerDisplaySnapshot != null && custom.PerDisplaySnapshot.Count > 0)
                {
                    _settings.GammaPerDisplay.Clear();
                    foreach (var kvp in custom.PerDisplaySnapshot)
                    {
                        _settings.GammaPerDisplay[kvp.Key] = new PerDisplayGamma
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
                    PopulateMonitorSelector();
                }

                return true;
            }
            return false;
        }

        private void ApplyPresetToMonitor(string presetName, string deviceId)
        {
            double r, g, b, gv;
            int mb;
            bool en;

            if (!PresetDefinitions.TryResolveParams(presetName, _settings.CustomGammaPresets,
                out r, out g, out b, out gv, out mb, out en))
                return;

            if (!_settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
            {
                pdg = new PerDisplayGamma();
                _settings.GammaPerDisplay[deviceId] = pdg;
            }
            pdg.RScale = r; pdg.GScale = g; pdg.BScale = b;
            pdg.GammaValue = gv; pdg.MasterBrightness = mb; pdg.Enabled = en;
            pdg.Source = "manual";

            if (_settings.ScheduleEnabled)
                _scheduleManualOverride = true;

            ApplyGammaToSystem();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(_settings);
            PopulateMonitorSelector();
            UpdateScheduleOverrideStatus();
        }

        private static string PromptForName(string prompt, string title, string defaultValue)
        {
            var form = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(360, 140),
                MaximizeBox = false,
                MinimizeBox = false
            };
            var lbl = new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true };
            var txt = new TextBox { Text = defaultValue, Location = new Point(12, 36), Width = 336 };
            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(180, 70), Width = 80, Height = 28 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(268, 70), Width = 80, Height = 28 };
            form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            if (form.ShowDialog() == DialogResult.OK)
                return txt.Text;
            return null;
        }

        #endregion

        #region UI Updates

        private void UpdateGammaUI()
        {
            _isUpdatingGammaSliders = true;

            bool supported = _gammaController.IsSupported;
            _gammaCheckBox.Enabled = supported;
            _gammaSimplifiedCheckBox.Enabled = supported;

            PopulateMonitorSelector();

            if (supported)
            {
                _gammaCheckBox.Checked = _settings.GammaEnabled;
                _gammaSimplifiedCheckBox.Checked = false;

                SyncSlidersToSelectedMonitor();

                string current = GetCurrentPresetName();
                _currentPresetName = current;
                if (current != null && !_isPopulatingComboBox)
                    _gammaModeComboBox.SelectedItem = current;

                UpdateGammaLabels();
                UpdateColorTempFromSliders();
                RefreshSliderVisibility();
                RefreshCustomPresetButtons();
            }
            else
            {
            }

            _isUpdatingGammaSliders = false;
        }

        private void PopulateMonitorSelector()
        {
            int prevIndex = _monitorSelectorComboBox.SelectedIndex;
            _monitorSelectorComboBox.Items.Clear();
            _monitorSelectorComboBox.Items.Add("所有显示器");
            foreach (var monitor in _monitorManager.Monitors)
            {
                string label = monitor.DisplayName;
                if (_settings.GammaPerDisplay.TryGetValue(monitor.DeviceId, out var pdg))
                {
                    string sourceTag = pdg.Source == "schedule" ? "定时" : "手动";
                    label += $" [{sourceTag}]";
                }
                _monitorSelectorComboBox.Items.Add(label);
            }
            if (prevIndex >= 0 && prevIndex < _monitorSelectorComboBox.Items.Count)
                _monitorSelectorComboBox.SelectedIndex = prevIndex;
            else
                _monitorSelectorComboBox.SelectedIndex = 0;
        }

        private void SyncSlidersToSelectedMonitor()
        {
            if (IsGlobalMonitorSelected())
            {
                _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(_settings.GammaRScale * 100.0));
                _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(_settings.GammaGScale * 100.0));
                _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(_settings.GammaBScale * 100.0));
                _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(_settings.GammaValue * 100.0));
                _gammaBrightSlider.Value = ClampSlider(_gammaBrightSlider, _settings.MasterBrightness);
                _gammaCheckBox.Checked = _settings.GammaEnabled;
                _resetDisplayGammaButton.Enabled = false;
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null && _settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                {
                    _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(pdg.RScale * 100.0));
                    _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(pdg.GScale * 100.0));
                    _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(pdg.BScale * 100.0));
                    _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(pdg.GammaValue * 100.0));
                    _gammaBrightSlider.Value = ClampSlider(_gammaBrightSlider, pdg.MasterBrightness);
                    _gammaCheckBox.Checked = pdg.Enabled;
                    _resetDisplayGammaButton.Enabled = true;
                }
                else
                {
                    _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(_settings.GammaRScale * 100.0));
                    _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(_settings.GammaGScale * 100.0));
                    _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(_settings.GammaBScale * 100.0));
                    _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(_settings.GammaValue * 100.0));
                    _gammaBrightSlider.Value = ClampSlider(_gammaBrightSlider, _settings.MasterBrightness);
                    _gammaCheckBox.Checked = _settings.GammaEnabled;
                    _resetDisplayGammaButton.Enabled = false;
                }
            }
        }

        private static int ClampSlider(ModernSlider slider, int val)
        {
            return Math.Max(slider.Minimum, Math.Min(slider.Maximum, val));
        }

        private void RefreshSliderVisibility()
        {
            bool enabled = _settings.GammaEnabled;
            bool simplified = _gammaSimplifiedCheckBox.Checked;

            _gammaModeComboBox.Enabled = enabled && !simplified;
            _gammaSaveCustomButton.Enabled = enabled && !simplified;
            _gammaColorTempSlider.Enabled = enabled && simplified;

            _gammaRSlider.Enabled = enabled && !simplified;
            _gammaGSlider.Enabled = enabled && !simplified;
            _gammaBSlider.Enabled = enabled && !simplified;
            _gammaValueSlider.Enabled = enabled && !simplified;
            _gammaBrightSlider.Enabled = enabled;
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
            if (!_settings.ScheduleEnabled)
            {
                _gammaStatusLabel.Text = "";
                UpdateTitleBar();
                return;
            }

            if (_scheduleManualOverride)
            {
                string nextInfo = GetNextScheduleInfo();
                _gammaStatusLabel.Text = string.IsNullOrEmpty(nextInfo)
                    ? "手动调整已覆盖定时设置，下次时段切换时恢复定时"
                    : $"手动调整已覆盖定时设置，{nextInfo}恢复定时";
            }
            else
            {
                string currentPreset = GetCurrentPresetName() ?? "自定义";
                _gammaStatusLabel.Text = $"定时运行中: 当前预设 \"{currentPreset}\"";
            }

            UpdateTitleBar();
        }

        private string GetNextScheduleInfo()
        {
            if (_settings.ScheduleSegments == null || _settings.ScheduleSegments.Count == 0)
                return "";

            var current = DateTime.Now.TimeOfDay;
            ScheduleSegment nextSegment = null;
            TimeSpan minDiff = TimeSpan.MaxValue;

            foreach (var segment in _settings.ScheduleSegments)
            {
                var startParts = segment.StartTime.Split(':');
                if (startParts.Length < 2) continue;
                var start = new TimeSpan(int.Parse(startParts[0]), int.Parse(startParts[1]), 0);

                TimeSpan diff;
                if (start > current)
                    diff = start - current;
                else
                    diff = TimeSpan.FromHours(24) - (current - start);

                if (diff < minDiff)
                {
                    minDiff = diff;
                    nextSegment = segment;
                }
            }

            if (nextSegment == null) return "";

            if (minDiff.TotalHours < 1)
                return $"{(int)minDiff.TotalMinutes}分钟后";
            return $"{(int)minDiff.TotalHours}小时{(int)minDiff.Minutes}分钟后";
        }

        private void UpdateTitleBar()
        {
            if (!_settings.ScheduleEnabled)
            {
                Text = "LumiShift";
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
                Text = $"LumiShift - {overrideText}";
                _trayIcon.Text = $"LumiShift - {overrideText}";
            }
            else
            {
                Text = $"LumiShift - 定时: {currentPreset}";
                _trayIcon.Text = $"LumiShift - 定时: {currentPreset}";
            }
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
            if (val <= 20) _gammaColorTempLabel.Text = "偏冷";
            else if (val <= 40) _gammaColorTempLabel.Text = "微冷";
            else if (val <= 60) _gammaColorTempLabel.Text = "适中";
            else if (val <= 80) _gammaColorTempLabel.Text = "微暖";
            else _gammaColorTempLabel.Text = "偏暖";
        }

        private void ApplyColorTempToSettings(int colorTemp)
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

            _settings.GammaRScale = Math.Round(r, 2);
            _settings.GammaGScale = 1.0;
            _settings.GammaBScale = Math.Round(b, 2);
            _settings.GammaValue = Math.Round(g, 2);
            _settings.GammaEnabled = true;
        }

        private bool IsGlobalMonitorSelected()
        {
            return _monitorSelectorComboBox.SelectedIndex <= 0;
        }

        private string GetSelectedMonitorDeviceId()
        {
            int idx = _monitorSelectorComboBox.SelectedIndex - 1;
            if (idx < 0 || idx >= _monitorManager.Monitors.Count)
                return null;
            return _monitorManager.Monitors[idx].DeviceId;
        }

        private void ApplyGammaToSystem()
        {
            bool hasOverrides = _settings.GammaPerDisplay != null &&
                                _settings.GammaPerDisplay.Count > 0;

            if (!hasOverrides)
            {
                if (_settings.GammaEnabled)
                {
                    var parameters = new GammaParameters
                    {
                        RScale = _settings.GammaRScale,
                        GScale = _settings.GammaGScale,
                        BScale = _settings.GammaBScale,
                        Gamma = _settings.GammaValue,
                        MasterBrightness = _settings.MasterBrightness
                    };
                    _gammaController.ApplyGamma(Screen.AllScreens, parameters);
                }
                else
                {
                    _gammaController.ResetGamma(Screen.AllScreens);
                }
                return;
            }

            var perScreenParams = new Dictionary<string, GammaParameters>();

            foreach (var monitor in _monitorManager.Monitors)
            {
                var screen = monitor.Screen;
                if (screen == null) continue;

                if (_settings.GammaPerDisplay.TryGetValue(monitor.DeviceId, out var overrideGamma))
                {
                    if (_settings.GammaEnabled && overrideGamma.Enabled)
                    {
                        perScreenParams[screen.DeviceName] = new GammaParameters
                        {
                            RScale = overrideGamma.RScale,
                            GScale = overrideGamma.GScale,
                            BScale = overrideGamma.BScale,
                            Gamma = overrideGamma.GammaValue,
                            MasterBrightness = overrideGamma.MasterBrightness
                        };
                    }
                }
                else
                {
                    if (_settings.GammaEnabled)
                    {
                        perScreenParams[screen.DeviceName] = new GammaParameters
                        {
                            RScale = _settings.GammaRScale,
                            GScale = _settings.GammaGScale,
                            BScale = _settings.GammaBScale,
                            Gamma = _settings.GammaValue,
                            MasterBrightness = _settings.MasterBrightness
                        };
                    }
                }
            }

            _gammaController.ApplyGammaPerScreen(perScreenParams);
        }

        private void UpdateBrightnessUI()
        {
            _brightnessPanel.Controls.Clear();

            foreach (var monitor in _monitorManager.Monitors)
            {
                string deviceId = monitor.DeviceId;

                var row = new Panel
                {
                    Width = _brightnessPanel.Width,
                    Height = 56,
                    Padding = new Padding(Spacing.LG, 8, Spacing.LG, 6),
                    BackColor = Color.Transparent
                };

                int currentBrightness = 50;
                if (_settings.BrightnessPerDisplay.ContainsKey(deviceId))
                {
                    currentBrightness = _settings.BrightnessPerDisplay[deviceId];
                }
                else if (monitor.Controller != null && monitor.Controller.IsSupported)
                {
                    try { currentBrightness = monitor.Controller.GetBrightness(); }
                    catch { }
                    _settings.BrightnessPerDisplay[deviceId] = currentBrightness;
                }

                var nameLabel = new Label
                {
                    Text = monitor.DisplayName,
                    AutoSize = true,
                    Font = Typography.Body,
                    ForeColor = Colors.TextSecondary
                };
                nameLabel.Location = new Point((row.Width - row.Padding.Horizontal - nameLabel.Width) / 2, row.Padding.Top);

                var tb = new ModernSlider
                {
                    Minimum = 0,
                    Maximum = 100,
                    Width = 220,
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

                int sliderGroupWidth = tb.Width + valLabel.PreferredWidth + 10;
                int sliderX = (row.Width - row.Padding.Horizontal - sliderGroupWidth) / 2;
                tb.Location = new Point(sliderX, row.Padding.Top + 26);
                valLabel.Location = new Point(sliderX + tb.Width + 10, row.Padding.Top + 30);

                tb.ValueChanged += (s, ev) =>
                {
                    valLabel.Text = $"{tb.Value}%";
                    _settings.BrightnessPerDisplay[deviceId] = tb.Value;
                    monitor.Controller?.SetBrightness(tb.Value);
                    SettingsStore.SaveSettings(_settings);
                };

                row.Controls.AddRange(new Control[] { nameLabel, tb, valLabel });
                _brightnessPanel.Controls.Add(row);
            }
        }

        private void UpdateScheduleUI()
        {
            _isUpdatingSchedule = true;

            _scheduleEnabledCheckBox.Checked = _settings.ScheduleEnabled;
            _gammaScheduleToggle.Checked = _settings.ScheduleEnabled;

            _scheduleTimer.Enabled = _settings.ScheduleEnabled;
            if (_settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                ScheduleTimer_Tick(null, null);
            }

            _isUpdatingSchedule = false;
        }

        private void UpdateStartupUI()
        {
            _startWithWindowsCheckBox.Checked = _settings.StartWithWindows;
            _startMinimizedCheckBox.Checked = _settings.StartMinimized;
        }

        private void UpdateThemeUI()
        {
            _themeComboBox.SelectedIndex = _settings.ThemeMode;
        }

        private void UpdateEyeProtectionUI()
        {
            _eyeProtectionToggle.Checked = _settings.EyeProtectionEnabled;
            _eyeProtectionStatusLabel.Text = _settings.EyeProtectionEnabled
                ? $"已启用 (R:{_settings.EyeProtectionRed} G:{_settings.EyeProtectionGreen} B:{_settings.EyeProtectionBlue})"
                : "未启用";

            if (_settings.EyeProtectionEnabled)
            {
                EyeProtectionService.ApplyColor(_settings.EyeProtectionRed, _settings.EyeProtectionGreen, _settings.EyeProtectionBlue);
            }
        }

        private void EyeProtectionToggle_CheckedChanged(object sender, EventArgs e)
        {
            _settings.EyeProtectionEnabled = _eyeProtectionToggle.Checked;
            if (_settings.EyeProtectionEnabled)
            {
                EyeProtectionService.ApplyColor(_settings.EyeProtectionRed, _settings.EyeProtectionGreen, _settings.EyeProtectionBlue);
            }
            else
            {
                EyeProtectionService.RestoreDefault();
            }
            UpdateEyeProtectionUI();
            SettingsStore.SaveSettings(_settings);
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
                cd.Color = Color.FromArgb(_settings.EyeProtectionRed, _settings.EyeProtectionGreen, _settings.EyeProtectionBlue);
                cd.FullOpen = true;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    ApplyEyeProtectionColor(cd.Color.R, cd.Color.G, cd.Color.B);
                }
            }
        }

        private void EyeProtectionRestoreButton_Click(object sender, EventArgs e)
        {
            _settings.EyeProtectionEnabled = false;
            _eyeProtectionToggle.Checked = false;
            EyeProtectionService.RestoreDefault();
            UpdateEyeProtectionUI();
            SettingsStore.SaveSettings(_settings);
        }

        private void ApplyEyeProtectionColor(int r, int g, int b)
        {
            _settings.EyeProtectionRed = r;
            _settings.EyeProtectionGreen = g;
            _settings.EyeProtectionBlue = b;
            _settings.EyeProtectionEnabled = true;

            _eyeProtectionToggle.Checked = true;
            EyeProtectionService.ApplyColor(r, g, b);
            UpdateEyeProtectionUI();
            SettingsStore.SaveSettings(_settings);
        }

        private void UpdateBgImageUI()
        {
            if (_isUpdatingBgImageUI) return;
            _bgImageToggle.Checked = _settings.UseBackgroundImage;
            _bgImageOpacitySlider.Value = _settings.BackgroundImageOpacity;
            _bgImageOpacityLabel.Text = $"{_settings.BackgroundImageOpacity}%";

            if (!string.IsNullOrEmpty(_settings.BackgroundImageFile))
            {
                _bgImageStatusLabel.Text = _settings.BackgroundImageFile;
            }
            else
            {
                _bgImageStatusLabel.Text = "未设置";
            }
        }

        private void LoadBackgroundImage()
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.Dispose();
                _backgroundImage = null;
            }

            if (!_settings.UseBackgroundImage || string.IsNullOrEmpty(_settings.BackgroundImageFile))
            {
                _bgImageStatusLabel.Text = "未设置";
                SyncBackgroundStaticFields();
                InvalidateBackgroundDisplay();
                return;
            }

            try
            {
                string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string bgDir = System.IO.Path.Combine(exeDir, "bg_img");
                string filePath = System.IO.Path.Combine(bgDir, _settings.BackgroundImageFile);

                if (System.IO.File.Exists(filePath))
                {
                    using (var tempImage = Image.FromFile(filePath))
                    {
                        _backgroundImage = new Bitmap(tempImage);
                    }
                    _bgImageStatusLabel.Text = _settings.BackgroundImageFile;
                }
                else
                {
                    _bgImageStatusLabel.Text = "文件不存在";
                }
            }
            catch (Exception ex)
            {
                _bgImageStatusLabel.Text = $"加载失败: {ex.Message}";
            }

            SyncBackgroundStaticFields();
            InvalidateBackgroundDisplay();
        }

        private void BgImageToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingBgImageUI) return;
            _settings.UseBackgroundImage = _bgImageToggle.Checked;
            LoadBackgroundImage();
            SettingsStore.SaveSettings(_settings);
        }

        private void BgImageSelectButton_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*";
                ofd.Title = "选择背景图片";
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
                                _backgroundImage.Dispose();
                                _backgroundImage = null;
                            }

                            System.IO.File.Copy(ofd.FileName, destPath, true);
                        }

                        _settings.BackgroundImageFile = fileName;
                        _settings.UseBackgroundImage = true;

                        _isUpdatingBgImageUI = true;
                        _bgImageToggle.Checked = true;
                        _isUpdatingBgImageUI = false;

                        LoadBackgroundImage();
                        SettingsStore.SaveSettings(_settings);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"设置背景图片失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BgImageClearButton_Click(object sender, EventArgs e)
        {
            _settings.BackgroundImageFile = "";
            _settings.UseBackgroundImage = false;
            _bgImageToggle.Checked = false;
            if (_backgroundImage != null)
            {
                _backgroundImage.Dispose();
                _backgroundImage = null;
            }
            UpdateBgImageUI();
            LoadBackgroundImage();
            SettingsStore.SaveSettings(_settings);
        }

        private void BgImageOpacitySlider_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingBgImageUI) return;
            _settings.BackgroundImageOpacity = _bgImageOpacitySlider.Value;
            _bgImageOpacityLabel.Text = $"{_bgImageOpacitySlider.Value}%";
            SyncBackgroundStaticFields();
            InvalidateBackgroundDisplay();
            SettingsStore.SaveSettings(_settings);
        }

        private void ApplyTheme()
        {
            RefreshTabTheme();
            RefreshControlTreeTheme(this);
            foreach (Control c in Controls) { c.Invalidate(); }
            Invalidate(true);
            UpdateTrayMenu();
        }

        private void RefreshControlTreeTheme(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is Label lbl)
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
                    else if (btn.Tag as string == "resetDisplayGamma")
                    {
                        btn.BackColor = Colors.Surface;
                        btn.ForeColor = Colors.Red;
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

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ApplyTheme()));
            }
            else
            {
                ApplyTheme();
            }
        }

        private void UpdateTrayMenu()
        {
            _trayMenu.Items.Clear();

            var gammaItem = new ToolStripMenuItem(
                _gammaController.IsSupported && _settings.GammaEnabled
                    ? "Gamma 校正: 已启用"
                    : "Gamma 校正: 已禁用")
            {
                Checked = _settings.GammaEnabled
            };
            gammaItem.Click += GammaTrayToggle_Click;
            _trayMenu.Items.Add(gammaItem);

            var quickMenu = new ToolStripMenuItem("快速切换预设");

            bool anyMonitorOverride = _settings.GammaPerDisplay != null && _settings.GammaPerDisplay.Count > 0;

            var allMonitorsItem = new ToolStripMenuItem("全部显示器");
            string globalPresetName = GetCurrentPresetName();
            foreach (var p in PresetDefinitions.GetNames())
            {
                bool isActive = !anyMonitorOverride && _settings.GammaEnabled && globalPresetName == p;
                var item = new ToolStripMenuItem(p) { Checked = isActive };
                string cp = p;
                item.Click += (s, ev) => QuickPreset_Click(cp);
                allMonitorsItem.DropDownItems.Add(item);
            }
            if (_settings.CustomGammaPresets.Count > 0)
            {
                allMonitorsItem.DropDownItems.Add(new ToolStripSeparator());
                foreach (var cp in _settings.CustomGammaPresets)
                {
                    bool isActive = !anyMonitorOverride && _settings.GammaEnabled && globalPresetName == cp.Name;
                    var item = new ToolStripMenuItem(cp.Name) { Checked = isActive };
                    string name = cp.Name;
                    item.Click += (s, ev) => QuickPreset_Click(name);
                    allMonitorsItem.DropDownItems.Add(item);
                }
            }
            quickMenu.DropDownItems.Add(allMonitorsItem);

            if (_monitorManager.Monitors.Count > 1 || anyMonitorOverride)
            {
                quickMenu.DropDownItems.Add(new ToolStripSeparator());

                foreach (var monitor in _monitorManager.Monitors)
                {
                    string deviceId = monitor.DeviceId;
                    string monitorLabel = monitor.DisplayName;
                    if (_settings.GammaPerDisplay.ContainsKey(deviceId))
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

                    if (_settings.CustomGammaPresets.Count > 0)
                    {
                        monitorItem.DropDownItems.Add(new ToolStripSeparator());
                        foreach (var cp in _settings.CustomGammaPresets)
                        {
                            bool isActive = currentMonitorPreset == cp.Name;
                            var item = new ToolStripMenuItem(cp.Name) { Checked = isActive };
                            string presetName = cp.Name;
                            string monDeviceId = deviceId;
                            item.Click += (s, ev) => ApplyPresetToMonitor(presetName, monDeviceId);
                            monitorItem.DropDownItems.Add(item);
                        }
                    }

                    quickMenu.DropDownItems.Add(monitorItem);
                }
            }

            _trayMenu.Items.Add(quickMenu);

            if (_settings.ScheduleEnabled && _scheduleManualOverride)
            {
                var restoreItem = new ToolStripMenuItem("恢复定时控制", null, (s, ev) =>
                {
                    _scheduleManualOverride = false;
                    ScheduleTimer_Tick(null, null);
                    UpdateTrayMenu();
                });
                _trayMenu.Items.Add(restoreItem);
            }

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

            _trayIcon.ContextMenuStrip = _trayMenu;
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void TurnOffMonitor()
        {
            NativeMethods.SendMessage(
                NativeMethods.HWND_BROADCAST,
                NativeMethods.WM_SYSCOMMAND,
                (IntPtr)NativeMethods.SC_MONITORPOWER,
                (IntPtr)2);
        }

        private void SyncSlidersToSettings()
        {
            _isUpdatingGammaSliders = true;
            SyncSlidersToSelectedMonitor();
            UpdateGammaLabels();
            UpdateColorTempFromSliders();
            if (_currentPresetName != null && _gammaModeComboBox.Items.Contains(_currentPresetName))
                _gammaModeComboBox.SelectedItem = _currentPresetName;
            else
            {
                string current = GetCurrentPresetName();
                if (current != null)
                    _gammaModeComboBox.SelectedItem = current;
            }
            RefreshSliderVisibility();
            RefreshCustomPresetButtons();
            _isUpdatingGammaSliders = false;
        }

        #endregion

        #region Schedule

        private void ScheduleTimer_Tick(object sender, EventArgs e)
        {
            if (!_settings.ScheduleEnabled) return;

            try
            {
                var current = DateTime.Now.TimeOfDay;

                string targetMode = null;
                ScheduleSegment targetSegment = null;

                foreach (var segment in _settings.ScheduleSegments)
                {
                    var startParts = segment.StartTime.Split(':');
                    var endParts = segment.EndTime.Split(':');
                    if (startParts.Length < 2 || endParts.Length < 2) continue;

                    var start = new TimeSpan(int.Parse(startParts[0]), int.Parse(startParts[1]), 0);
                    var end = new TimeSpan(int.Parse(endParts[0]), int.Parse(endParts[1]), 0);

                    if (start == end) continue;

                    bool inSegment;
                    if (start < end)
                        inSegment = current >= start && current < end;
                    else
                        inSegment = current >= start || current < end;

                    if (inSegment)
                    {
                        targetMode = segment.PresetName;
                        targetSegment = segment;
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

                var savedR = _settings.GammaRScale;
                var savedG = _settings.GammaGScale;
                var savedB = _settings.GammaBScale;
                var savedV = _settings.GammaValue;
                var savedE = _settings.GammaEnabled;
                var savedM = _settings.MasterBrightness;

                bool applied = TryApplyPreset(targetMode);

                if (!applied)
                {
                    _settings.GammaRScale = savedR;
                    _settings.GammaGScale = savedG;
                    _settings.GammaBScale = savedB;
                    _settings.GammaValue = savedV;
                    _settings.GammaEnabled = savedE;
                    _settings.MasterBrightness = savedM;
                    _lastScheduleMode = targetMode;
                    return;
                }

                bool changed = Math.Abs(_settings.GammaRScale - savedR) > 0.001 ||
                               Math.Abs(_settings.GammaGScale - savedG) > 0.001 ||
                               Math.Abs(_settings.GammaBScale - savedB) > 0.001 ||
                               Math.Abs(_settings.GammaValue - savedV) > 0.001 ||
                               _settings.GammaEnabled != savedE ||
                               _settings.MasterBrightness != savedM;

                if (!changed)
                {
                    _settings.GammaRScale = savedR;
                    _settings.GammaGScale = savedG;
                    _settings.GammaBScale = savedB;
                    _settings.GammaValue = savedV;
                    _settings.GammaEnabled = savedE;
                    _settings.MasterBrightness = savedM;
                    _lastScheduleMode = targetMode;
                    return;
                }

                ApplyGammaToSystem();
                if (!_isUpdatingGammaSliders)
                {
                    _currentPresetName = targetMode;
                    SyncSlidersToSettings();
                }

                SettingsStore.SaveSettings(_settings);

                _gammaStatusLabel.Text = $"定时切换: 已切换至预设 \"{targetMode}\"";
                UpdateTrayMenu();
                _lastScheduleMode = targetMode;

                ApplyScheduleMonitorPresets(targetSegment);
            }
            catch
            {
            }
        }

        private void ApplyScheduleMonitorPresets(ScheduleSegment segment)
        {
            if (segment?.MonitorPresets == null || segment.MonitorPresets.Count == 0)
                return;

            foreach (var monitor in _monitorManager.Monitors)
            {
                if (!segment.MonitorPresets.TryGetValue(monitor.DeviceId, out var presetName))
                    continue;

                double r, g, b, gv;
                int mb;
                bool en;

                if (!PresetDefinitions.TryResolveParams(presetName, _settings.CustomGammaPresets,
                    out r, out g, out b, out gv, out mb, out en))
                    continue;

                if (!_settings.GammaPerDisplay.TryGetValue(monitor.DeviceId, out var pdg))
                {
                    pdg = new PerDisplayGamma();
                    _settings.GammaPerDisplay[monitor.DeviceId] = pdg;
                }
                pdg.RScale = r; pdg.GScale = g; pdg.BScale = b;
                pdg.GammaValue = gv; pdg.MasterBrightness = mb; pdg.Enabled = en;
                pdg.Source = "schedule";
            }

            ApplyGammaToSystem();
            PopulateMonitorSelector();
            SettingsStore.SaveSettings(_settings);
        }

        #endregion

        #region Event Handlers

        private void GammaCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;

            if (IsGlobalMonitorSelected())
            {
                _settings.GammaEnabled = _gammaCheckBox.Checked;
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    if (!_settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                    {
                        pdg = new PerDisplayGamma
                        {
                            RScale = _settings.GammaRScale,
                            GScale = _settings.GammaGScale,
                            BScale = _settings.GammaBScale,
                            GammaValue = _settings.GammaValue,
                            MasterBrightness = _settings.MasterBrightness,
                            Source = "manual"
                        };
                        _settings.GammaPerDisplay[deviceId] = pdg;
                    }
                    pdg.Enabled = _gammaCheckBox.Checked;
                    pdg.Source = "manual";
                    PopulateMonitorSelector();
                }
            }

            if (_settings.ScheduleEnabled)
                _scheduleManualOverride = true;
            RefreshSliderVisibility();
            ApplyGammaToSystem();
            SettingsStore.SaveSettings(_settings);
            UpdateTrayMenu();
            UpdateScheduleOverrideStatus();
        }

        private void GammaSimplifiedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;
            RefreshSliderVisibility();
        }

        private void GammaColorTempSlider_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;
            if (!_gammaSimplifiedCheckBox.Checked) return;

            if (_settings.ScheduleEnabled)
                _scheduleManualOverride = true;

            _isUpdatingGammaSliders = true;

            ApplyColorTempToSettings(_gammaColorTempSlider.Value);

            if (IsGlobalMonitorSelected())
            {
                _settings.MasterBrightness = _gammaBrightSlider.Value;
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    if (!_settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                    {
                        pdg = new PerDisplayGamma
                        {
                            RScale = _settings.GammaRScale,
                            GScale = _settings.GammaGScale,
                            BScale = _settings.GammaBScale,
                            GammaValue = _settings.GammaValue,
                            MasterBrightness = _settings.MasterBrightness,
                            Enabled = _settings.GammaEnabled,
                            Source = "manual"
                        };
                        _settings.GammaPerDisplay[deviceId] = pdg;
                        PopulateMonitorSelector();
                    }
                    pdg.RScale = _settings.GammaRScale;
                    pdg.GScale = _settings.GammaGScale;
                    pdg.BScale = _settings.GammaBScale;
                    pdg.GammaValue = _settings.GammaValue;
                    pdg.MasterBrightness = _gammaBrightSlider.Value;
                    pdg.Enabled = true;
                    pdg.Source = "manual";
                    _resetDisplayGammaButton.Enabled = true;
                }
            }

            _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(_settings.GammaRScale * 100.0));
            _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(_settings.GammaGScale * 100.0));
            _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(_settings.GammaBScale * 100.0));
            _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(_settings.GammaValue * 100.0));

            UpdateGammaLabels();
            UpdateColorTempLabel();
            _gammaCheckBox.Checked = true;

            string current = GetCurrentPresetName();
            _currentPresetName = current;
            if (current != null)
                _gammaModeComboBox.SelectedItem = current;

            ApplyGammaToSystem();
            SettingsStore.SaveSettings(_settings);

            _isUpdatingGammaSliders = false;
        }

        private void GammaModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders || _isPopulatingComboBox) return;

            if (!(_gammaModeComboBox.SelectedItem is string selected)) return;

            _currentPresetName = selected;

            if (_settings.ScheduleEnabled)
                _scheduleManualOverride = true;

            if (IsGlobalMonitorSelected())
            {
                TryApplyPreset(selected);
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    double r, g, b, gv;
                    int mb;
                    bool en;

                    if (PresetDefinitions.TryResolveParams(selected, _settings.CustomGammaPresets,
                        out r, out g, out b, out gv, out mb, out en))
                    {
                        if (!_settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                        {
                            pdg = new PerDisplayGamma
                            {
                                RScale = _settings.GammaRScale,
                                GScale = _settings.GammaGScale,
                                BScale = _settings.GammaBScale,
                                GammaValue = _settings.GammaValue,
                                MasterBrightness = _settings.MasterBrightness,
                                Enabled = _settings.GammaEnabled,
                                Source = "manual"
                            };
                            _settings.GammaPerDisplay[deviceId] = pdg;
                        }
                        pdg.RScale = r; pdg.GScale = g; pdg.BScale = b;
                        pdg.GammaValue = gv; pdg.MasterBrightness = mb; pdg.Enabled = en;
                        pdg.Source = "manual";
                        PopulateMonitorSelector();
                    }
                }
            }

            SyncSlidersToSettings();
            ApplyGammaToSystem();
            SettingsStore.SaveSettings(_settings);
            UpdateTrayMenu();
            UpdateScheduleOverrideStatus();
        }

        private void GammaSlider_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;

            if (_settings.ScheduleEnabled)
                _scheduleManualOverride = true;

            UpdateGammaLabels();

            if (IsGlobalMonitorSelected())
            {
                _settings.GammaRScale = _gammaRSlider.Value / 100.0;
                _settings.GammaGScale = _gammaGSlider.Value / 100.0;
                _settings.GammaBScale = _gammaBSlider.Value / 100.0;
                _settings.GammaValue = _gammaValueSlider.Value / 100.0;
                _settings.MasterBrightness = _gammaBrightSlider.Value;
                _settings.GammaEnabled = _gammaCheckBox.Checked;
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    if (!_settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                    {
                        pdg = new PerDisplayGamma
                        {
                            RScale = _settings.GammaRScale,
                            GScale = _settings.GammaGScale,
                            BScale = _settings.GammaBScale,
                            GammaValue = _settings.GammaValue,
                            MasterBrightness = _settings.MasterBrightness,
                            Enabled = _settings.GammaEnabled,
                            Source = "manual"
                        };
                        _settings.GammaPerDisplay[deviceId] = pdg;
                        PopulateMonitorSelector();
                    }
                    pdg.RScale = _gammaRSlider.Value / 100.0;
                    pdg.GScale = _gammaGSlider.Value / 100.0;
                    pdg.BScale = _gammaBSlider.Value / 100.0;
                    pdg.GammaValue = _gammaValueSlider.Value / 100.0;
                    pdg.MasterBrightness = _gammaBrightSlider.Value;
                    pdg.Enabled = _gammaCheckBox.Checked;
                    pdg.Source = "manual";
                    _resetDisplayGammaButton.Enabled = true;
                }
            }

            UpdateColorTempFromSliders();
            string current = GetCurrentPresetName();
            _currentPresetName = current;
            if (current != null && !_isPopulatingComboBox)
                _gammaModeComboBox.SelectedItem = current;

            if (_gammaController.IsSupported)
                ApplyGammaToSystem();

            SettingsStore.SaveSettings(_settings);
            UpdateScheduleOverrideStatus();
        }

        private void GammaSaveCustomButton_Click(object sender, EventArgs e)
        {
            string name = PromptForName("请输入自定义预设名称:", "保存当前设置为预设", "我的自定义");
            if (name == null) return;
            name = name.Trim();
            if (name.Length == 0) return;

            if (PresetDefinitions.IsBuiltIn(name))
            {
                MessageBox.Show("该名称与内置预设冲突，请更换名称。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var existing = _settings.CustomGammaPresets.FirstOrDefault(cp => cp.Name == name);
            if (existing != null)
            {
                if (MessageBox.Show($"预设 \"{name}\" 已存在，是否覆盖？", "确认覆盖",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                _settings.CustomGammaPresets.Remove(existing);
            }

            var preset = new GammaPreset
            {
                Name = name,
                RScale = _settings.GammaRScale,
                GScale = _settings.GammaGScale,
                BScale = _settings.GammaBScale,
                GammaValue = _settings.GammaValue,
                MasterBrightness = _settings.MasterBrightness,
                Enabled = _settings.GammaEnabled
            };

            if (_settings.GammaPerDisplay != null && _settings.GammaPerDisplay.Count > 0)
            {
                preset.PerDisplaySnapshot = new Dictionary<string, PerDisplayGamma>();
                foreach (var kvp in _settings.GammaPerDisplay)
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

            _settings.CustomGammaPresets.Add(preset);

            PopulatePresetComboBox();
            _currentPresetName = name;
            _gammaModeComboBox.SelectedItem = name;
            SettingsStore.SaveSettings(_settings);
            UpdateTrayMenu();
            RefreshCustomPresetButtons();

            _gammaStatusLabel.Text = $"已保存自定义预设 \"{name}\"";
        }

        private void GammaDeleteCustomButton_Click(object sender, EventArgs e)
        {
            if (!(_gammaModeComboBox.SelectedItem is string selected)) return;
            if (PresetDefinitions.IsBuiltIn(selected)) return;

            if (MessageBox.Show($"确定要删除自定义预设 \"{selected}\" 吗？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            bool wasActive = GetCurrentPresetName() == selected;
            _settings.CustomGammaPresets.RemoveAll(cp => cp.Name == selected);

            PopulatePresetComboBox();

            if (wasActive)
            {
                _currentPresetName = PresetDefinitions.BuiltIns[0].Name;
                TryApplyPreset(PresetDefinitions.BuiltIns[0].Name);
                SyncSlidersToSettings();
                ApplyGammaToSystem();
            }

            SettingsStore.SaveSettings(_settings);
            UpdateTrayMenu();
            RefreshCustomPresetButtons();

            _gammaStatusLabel.Text = $"已删除自定义预设 \"{selected}\"";
        }

        private void GammaTrayToggle_Click(object sender, EventArgs e)
        {
            _settings.GammaEnabled = !_settings.GammaEnabled;
            if (_settings.ScheduleEnabled)
                _scheduleManualOverride = true;
            ApplyGammaToSystem();
            SyncSlidersToSettings();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(_settings);
            UpdateScheduleOverrideStatus();
        }

        private void QuickPreset_Click(string presetName)
        {
            if (_settings.ScheduleEnabled)
                _scheduleManualOverride = true;
            _currentPresetName = presetName;
            TryApplyPreset(presetName);
            SyncSlidersToSettings();
            ApplyGammaToSystem();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(_settings);
            UpdateScheduleOverrideStatus();
        }

        private void MonitorSelectorComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;
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

            bool isFromSchedule = _settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg) && pdg.Source == "schedule";

            if (isFromSchedule && _settings.ScheduleEnabled)
            {
                if (MessageBox.Show(
                    "此配置由定时调度生成，重置后将在下次时段切换时恢复。\n\n是否仍要重置？",
                    "定时配置提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                    return;
            }

            _settings.GammaPerDisplay.Remove(deviceId);
            PopulateMonitorSelector();

            _isUpdatingGammaSliders = true;
            SyncSlidersToSelectedMonitor();
            UpdateGammaLabels();
            UpdateColorTempFromSliders();
            _isUpdatingGammaSliders = false;

            ApplyGammaToSystem();
            SettingsStore.SaveSettings(_settings);
            UpdateTrayMenu();

            _gammaStatusLabel.Text = isFromSchedule && _settings.ScheduleEnabled
                ? "已重置为全局设置（定时调度将在下次时段切换时恢复）"
                : "已重置为全局设置";
        }

        private void GammaScheduleToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            _settings.ScheduleEnabled = _gammaScheduleToggle.Checked;
            _scheduleEnabledCheckBox.Checked = _settings.ScheduleEnabled;
            _scheduleTimer.Enabled = _settings.ScheduleEnabled;
            if (_settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);
            }
            else
            {
                UpdateScheduleOverrideStatus();
            }
            SettingsStore.SaveSettings(_settings);
        }

        private void ScheduleEnabledCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            _settings.ScheduleEnabled = _scheduleEnabledCheckBox.Checked;
            _gammaScheduleToggle.Checked = _settings.ScheduleEnabled;
            _scheduleTimer.Enabled = _settings.ScheduleEnabled;
            if (_settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);
            }
            SettingsStore.SaveSettings(_settings);
        }

        private void ScheduleConfigButton_Click(object sender, EventArgs e)
        {
            using (var dlg = new ScheduleConfigForm(_settings.ScheduleSegments, _settings.CustomGammaPresets, _monitorManager.Monitors.ToList()))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultSegments != null)
                {
                    _settings.ScheduleSegments = dlg.ResultSegments;
                    OnScheduleSegmentChanged();
                }
            }
        }

        private void OnScheduleSegmentChanged()
        {
            if (_settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);
            }
            SettingsStore.SaveSettings(_settings);
        }

        private void StartWithWindowsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _settings.StartWithWindows = _startWithWindowsCheckBox.Checked;
            UpdateStartupRegistry();
            SettingsStore.SaveSettings(_settings);
        }

        private void StartMinimizedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _settings.StartMinimized = _startMinimizedCheckBox.Checked;
            SettingsStore.SaveSettings(_settings);
        }

        private void ThemeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_themeComboBox.SelectedIndex < 0) return;
            _settings.ThemeMode = _themeComboBox.SelectedIndex;
            ThemeManager.CurrentMode = (ThemeMode)_themeComboBox.SelectedIndex;
            SettingsStore.SaveSettings(_settings);
        }

        private void OnGammaStatusChanged(object sender, string status)
        {
            if (InvokeRequired)
                Invoke(new Action(() => _gammaStatusLabel.Text = status));
            else
                _gammaStatusLabel.Text = status;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => HandleDisplayChange()));
            }
            else
            {
                HandleDisplayChange();
            }
        }

        private void OnMonitorsChanged()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => RefreshAllMonitorUI()));
            }
            else
            {
                RefreshAllMonitorUI();
            }
        }

        private void HandleDisplayChange()
        {
            var removedIds = _monitorManager.RefreshMonitors();
            CleanupStaleSettings(removedIds);
            RefreshAllMonitorUI();
        }

        private void CleanupStaleSettings(HashSet<string> removedDeviceIds)
        {
            if (removedDeviceIds == null || removedDeviceIds.Count == 0)
                return;

            foreach (var id in removedDeviceIds)
            {
                _settings.BrightnessPerDisplay.Remove(id);
                _settings.GammaPerDisplay.Remove(id);
            }

            if (_settings.ScheduleSegments != null)
            {
                foreach (var segment in _settings.ScheduleSegments)
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

            if (_settings.CustomGammaPresets != null)
            {
                foreach (var preset in _settings.CustomGammaPresets)
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

            SettingsStore.SaveSettings(_settings);
        }

        private void RefreshAllMonitorUI()
        {
            UpdateBrightnessUI();
            UpdateGammaUI();
            ApplyGammaToSystem();
        }

        private void ExitApplication()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _scheduleTimer?.Stop();
            _gammaController?.ResetGamma(Screen.AllScreens);
            _gammaController?.Dispose();
            _monitorManager?.Dispose();
            _trayIcon?.Dispose();
            _backgroundImage?.Dispose();
            Application.Exit();
        }

        private void UpdateStartupRegistry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (_settings.StartWithWindows)
                    {
                        string path = $"\"{Application.ExecutablePath}\"";
                        if (_settings.StartMinimized) path += " --minimized";
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

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_SHOW_LUMISHIFT)
            {
                ShowMainWindow();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            DrawBackgroundImage(e.Graphics, ClientRectangle);
        }

        private void DrawBackgroundImage(Graphics g, Rectangle bounds)
        {
            if (_backgroundImage == null || !_settings.UseBackgroundImage) return;

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            float opacity = _settings.BackgroundImageOpacity / 100f;

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

                int imgW = _backgroundImage.Width;
                int imgH = _backgroundImage.Height;
                int areaW = bounds.Width;
                int areaH = bounds.Height;

                float scale = Math.Max((float)areaW / imgW, (float)areaH / imgH);
                int drawW = (int)(imgW * scale);
                int drawH = (int)(imgH * scale);
                int x = bounds.X + (areaW - drawW) / 2;
                int y = bounds.Y + (areaH - drawH) / 2;

                g.DrawImage(_backgroundImage, new Rectangle(x, y, drawW, drawH),
                    0, 0, imgW, imgH, GraphicsUnit.Pixel, attributes);
            }
        }

        private void SubscribeBackgroundPaintEvents()
        {
            foreach (TabPage page in _tabControl.TabPages)
            {
                page.Paint -= OnTabPagePaint;
                page.Paint += OnTabPagePaint;
            }
            _tabControl.Paint -= OnTabControlPaint;
            _tabControl.Paint += OnTabControlPaint;
        }

        private void OnTabPagePaint(object sender, PaintEventArgs e)
        {
            DrawBackgroundImage(e.Graphics, ((Control)sender).ClientRectangle);
        }

        private void OnTabControlPaint(object sender, PaintEventArgs e)
        {
            DrawBackgroundImage(e.Graphics, ((Control)sender).ClientRectangle);
        }

        private void InvalidateBackgroundDisplay()
        {
            foreach (TabPage page in _tabControl.TabPages)
            {
                page.Invalidate();
            }
            _tabControl.Invalidate();
        }

        private void SyncBackgroundStaticFields()
        {
            StaticBackgroundImage = _backgroundImage;
            StaticBackgroundOpacity = _settings.BackgroundImageOpacity / 100f;
            StaticUseBackgroundImage = _settings.UseBackgroundImage;
        }

        internal static void DrawBackgroundOnGraphics(Graphics g, Rectangle bounds)
        {
            if (!StaticUseBackgroundImage || StaticBackgroundImage == null) return;

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
                {
                    new float[] { 1, 0, 0, 0, 0 },
                    new float[] { 0, 1, 0, 0, 0 },
                    new float[] { 0, 0, 1, 0, 0 },
                    new float[] { 0, 0, 0, StaticBackgroundOpacity, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });
                attributes.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);

                int imgW = StaticBackgroundImage.Width;
                int imgH = StaticBackgroundImage.Height;
                int areaW = bounds.Width;
                int areaH = bounds.Height;

                float scale = Math.Max((float)areaW / imgW, (float)areaH / imgH);
                int drawW = (int)(imgW * scale);
                int drawH = (int)(imgH * scale);
                int x = bounds.X + (areaW - drawW) / 2;
                int y = bounds.Y + (areaH - drawH) / 2;

                g.DrawImage(StaticBackgroundImage, new Rectangle(x, y, drawW, drawH),
                    0, 0, imgW, imgH, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _trayIcon.Visible = true;
            ApplyTheme();
            if (_settings.StartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
                Hide();
            }
        }
    }
}