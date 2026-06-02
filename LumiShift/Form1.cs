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

        private FlowLayoutPanel _brightnessPanel;
        private readonly Dictionary<string, Panel> _brightnessRows = new Dictionary<string, Panel>();
        private readonly Dictionary<string, EventHandler> _brightnessSliderHandlers = new Dictionary<string, EventHandler>();
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
        private ToggleSwitch _restoreGammaToggle;

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
        private Bitmap _cachedBackground;
        private bool _isUpdatingBgImageUI;
        private bool _formDisposed;

        private Timer _resizeDebounceTimer;

        private bool _isUpdatingGammaSliders;
        private bool _isUpdatingBrightness;
        private bool _isPopulatingComboBox;
        private bool _isUpdatingSchedule;
        private string _currentPresetName;
        private int _previousMonitorSelectedIndex;
        private Timer _initTimer;

        private static Icon LoadAppIcon()
        {
            return Program.AppIcon;
        }

        public Form1(BackgroundService bgService)
        {
            DoubleBuffered = true;
            _bgService = bgService;
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

            PopulatePresetComboBox();
            UpdateGammaUI();
            _bgService.ApplyGammaToSystem();
            UpdateBrightnessUI();
            UpdateScheduleUI();
            UpdateStartupUI();
            UpdateEyeProtectionUI();
            UpdateBgImageUI();
            LoadBackgroundImage();
            SyncBackgroundStaticFields();
            SubscribeBackgroundPaintEvents();
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
            foreach (var p in PresetDefinitions.GetNames())
                _gammaModeComboBox.Items.Add(p);
            foreach (var cp in Settings.CustomGammaPresets)
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
            bool any = Settings.CustomGammaPresets.Count > 0;
            _gammaSaveCustomButton.Enabled = Settings.GammaEnabled && !_gammaSimplifiedCheckBox.Checked;
            _gammaDeleteCustomButton.Enabled = any && _gammaModeComboBox.SelectedItem is string selected && !PresetDefinitions.IsBuiltIn(selected);
        }

        private static string PromptForName(string prompt, string title, string defaultValue)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(360, 140);
                form.MaximizeBox = false;
                form.MinimizeBox = false;
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
        }

        #endregion

        #region UI Updates

        private void UpdateGammaUI()
        {
            _isUpdatingGammaSliders = true;

            bool supported = GammaCtrl.IsSupported;
            _gammaCheckBox.Enabled = supported;
            _gammaSimplifiedCheckBox.Enabled = supported;

            PopulateMonitorSelector();

            if (supported)
            {
                _gammaCheckBox.Checked = Settings.GammaEnabled;
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

            _isUpdatingGammaSliders = false;
        }

        private void PopulateMonitorSelector()
        {
            int prevIndex = _monitorSelectorComboBox.SelectedIndex;
            _monitorSelectorComboBox.Items.Clear();
            _monitorSelectorComboBox.Items.Add("所有显示器");
            foreach (var monitor in MonitorMgr.Monitors)
            {
                string label = monitor.DisplayName;
                if (Settings.GammaPerDisplay.TryGetValue(monitor.DeviceId, out var pdg))
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
                if (deviceId != null && Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
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
                    _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(Settings.GammaRScale * 100.0));
                    _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(Settings.GammaGScale * 100.0));
                    _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(Settings.GammaBScale * 100.0));
                    _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(Settings.GammaValue * 100.0));
                    _gammaBrightSlider.Value = ClampSlider(_gammaBrightSlider, Settings.MasterBrightness);
                    _gammaCheckBox.Checked = Settings.GammaEnabled;
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
            bool enabled;
            if (IsGlobalMonitorSelected())
            {
                enabled = Settings.GammaEnabled;
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null && Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                    enabled = pdg.Enabled;
                else
                    enabled = Settings.GammaEnabled;
            }

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

        private void UpdateTitleBar()
        {
            if (!Settings.ScheduleEnabled)
            {
                Text = "LumiShift";
                return;
            }

            string currentPreset = GetCurrentPresetName() ?? "自定义";

            if (_bgService.ScheduleManualOverride)
            {
                string nextInfo = _bgService.GetNextScheduleInfo();
                string overrideText = string.IsNullOrEmpty(nextInfo)
                    ? "手动调整"
                    : $"手动调整 ({nextInfo}恢复)";
                Text = $"LumiShift - {overrideText}";
            }
            else
            {
                Text = $"LumiShift - 定时: {currentPreset}";
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

            Settings.GammaRScale = Math.Round(r, 2);
            Settings.GammaGScale = 1.0;
            Settings.GammaBScale = Math.Round(b, 2);
            Settings.GammaValue = Math.Round(g, 2);
            Settings.GammaEnabled = true;
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
                    try { currentBrightness = monitor.Controller.GetBrightness(); }
                    catch { }
                    Settings.BrightnessPerDisplay[deviceId] = currentBrightness;
                }

                if (_brightnessRows.TryGetValue(deviceId, out var row))
                {
                    var slider = (ModernSlider)row.Controls[1];
                    var valLabel = (Label)row.Controls[2];
                    var nameLabel = (Label)row.Controls[0];

                    nameLabel.Text = monitor.DisplayName;
                    nameLabel.Width = _brightnessPanel.Width - row.Padding.Horizontal;
                    slider.Maximum = 100;
                    slider.Minimum = 0;
                    slider.Value = currentBrightness;
                    slider.Enabled = monitor.Controller?.IsSupported ?? false;
                    valLabel.Text = $"{currentBrightness}%";
                }
                else
                {
                    row = new Panel
                    {
                        Width = _brightnessPanel.Width,
                        Height = 56,
                        Padding = new Padding(Spacing.LG, 8, Spacing.LG, 6),
                        BackColor = Color.Transparent,
                        Tag = deviceId
                    };

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

                    EventHandler handler = (s, ev) =>
                    {
                        if (_isUpdatingBrightness) return;
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
            _restoreGammaToggle.Checked = Settings.RestoreGammaOnExit;
        }

        private void RestoreGammaToggle_CheckedChanged(object sender, EventArgs e)
        {
            Settings.RestoreGammaOnExit = _restoreGammaToggle.Checked;
            SettingsStore.SaveSettings(Settings);
        }

        private void UpdateEyeProtectionUI()
        {
            _eyeProtectionToggle.Checked = Settings.EyeProtectionEnabled;
            _eyeProtectionStatusLabel.Text = Settings.EyeProtectionEnabled
                ? $"已启用 (R:{Settings.EyeProtectionRed} G:{Settings.EyeProtectionGreen} B:{Settings.EyeProtectionBlue})"
                : "未启用";

            if (Settings.EyeProtectionEnabled)
            {
                EyeProtectionService.ApplyColor(Settings.EyeProtectionRed, Settings.EyeProtectionGreen, Settings.EyeProtectionBlue);
            }
        }

        private void EyeProtectionToggle_CheckedChanged(object sender, EventArgs e)
        {
            Settings.EyeProtectionEnabled = _eyeProtectionToggle.Checked;
            if (Settings.EyeProtectionEnabled)
            {
                EyeProtectionService.ApplyColor(Settings.EyeProtectionRed, Settings.EyeProtectionGreen, Settings.EyeProtectionBlue);
            }
            else
            {
                EyeProtectionService.RestoreDefault();
            }
            UpdateEyeProtectionUI();
            SettingsStore.SaveSettings(Settings);
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
                _bgImageStatusLabel.Text = "未设置";
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
                _bgImageStatusLabel.Text = "未设置";
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
                    _bgImageStatusLabel.Text = "文件不存在";
                }
            }
            catch (Exception ex)
            {
                _bgImageStatusLabel.Text = $"加载失败: {ex.Message}";
            }

            SyncBackgroundStaticFields();
            RebuildBackgroundCache();
            InvalidateBackgroundDisplay();
        }

        private void BgImageToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingBgImageUI) return;
            Settings.UseBackgroundImage = _bgImageToggle.Checked;
            LoadBackgroundImage();
            SettingsStore.SaveSettings(Settings);
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
                        MessageBox.Show($"设置背景图片失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void GammaCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;

            if (IsGlobalMonitorSelected())
            {
                Settings.GammaEnabled = _gammaCheckBox.Checked;
                if (Settings.GammaPerDisplay.Count > 0)
                {
                    Settings.GammaPerDisplay.Clear();
                    PopulateMonitorSelector();
                }
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    if (!Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                    {
                        pdg = new PerDisplayGamma
                        {
                            RScale = Settings.GammaRScale,
                            GScale = Settings.GammaGScale,
                            BScale = Settings.GammaBScale,
                            GammaValue = Settings.GammaValue,
                            MasterBrightness = Settings.MasterBrightness,
                            Source = "manual"
                        };
                        Settings.GammaPerDisplay[deviceId] = pdg;
                    }
                    pdg.Enabled = _gammaCheckBox.Checked;
                    pdg.Source = "manual";
                    PopulateMonitorSelector();
                }
            }

            if (Settings.ScheduleEnabled)
                _bgService.SetScheduleManualOverride(true);

            RefreshSliderVisibility();
            _bgService.ApplyGammaToSystem();
            SettingsStore.SaveSettings(Settings);
            _bgService.UpdateTrayMenu();
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

            if (Settings.ScheduleEnabled)
                _bgService.SetScheduleManualOverride(true);

            _isUpdatingGammaSliders = true;

            ApplyColorTempToSettings(_gammaColorTempSlider.Value);

            if (IsGlobalMonitorSelected())
            {
                Settings.MasterBrightness = _gammaBrightSlider.Value;
                if (Settings.GammaPerDisplay.Count > 0)
                {
                    Settings.GammaPerDisplay.Clear();
                    PopulateMonitorSelector();
                }
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    if (!Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                    {
                        pdg = new PerDisplayGamma
                        {
                            RScale = Settings.GammaRScale,
                            GScale = Settings.GammaGScale,
                            BScale = Settings.GammaBScale,
                            GammaValue = Settings.GammaValue,
                            MasterBrightness = Settings.MasterBrightness,
                            Enabled = Settings.GammaEnabled,
                            Source = "manual"
                        };
                        Settings.GammaPerDisplay[deviceId] = pdg;
                        PopulateMonitorSelector();
                    }
                    pdg.RScale = Settings.GammaRScale;
                    pdg.GScale = Settings.GammaGScale;
                    pdg.BScale = Settings.GammaBScale;
                    pdg.GammaValue = Settings.GammaValue;
                    pdg.MasterBrightness = _gammaBrightSlider.Value;
                    pdg.Enabled = true;
                    pdg.Source = "manual";
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
                _gammaModeComboBox.SelectedItem = current;

            _bgService.ApplyGammaToSystem();
            SettingsStore.SaveSettings(Settings);

            _isUpdatingGammaSliders = false;
        }

        private void GammaModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders || _isPopulatingComboBox) return;

            if (!(_gammaModeComboBox.SelectedItem is string selected)) return;

            _currentPresetName = selected;

            if (Settings.ScheduleEnabled)
                _bgService.SetScheduleManualOverride(true);

            if (IsGlobalMonitorSelected())
            {
                _bgService.TryApplyPreset(selected);
                if (Settings.GammaPerDisplay.Count > 0)
                {
                    Settings.GammaPerDisplay.Clear();
                    PopulateMonitorSelector();
                }
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    double r, g, b, gv;
                    int mb;
                    bool en;

                    if (PresetDefinitions.TryResolveParams(selected, Settings.CustomGammaPresets,
                        out r, out g, out b, out gv, out mb, out en))
                    {
                        if (!Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                        {
                            pdg = new PerDisplayGamma
                            {
                                RScale = Settings.GammaRScale,
                                GScale = Settings.GammaGScale,
                                BScale = Settings.GammaBScale,
                                GammaValue = Settings.GammaValue,
                                MasterBrightness = Settings.MasterBrightness,
                                Enabled = Settings.GammaEnabled,
                                Source = "manual"
                            };
                            Settings.GammaPerDisplay[deviceId] = pdg;
                        }
                        pdg.RScale = r; pdg.GScale = g; pdg.BScale = b;
                        pdg.GammaValue = gv; pdg.MasterBrightness = mb; pdg.Enabled = en;
                        pdg.Source = "manual";
                        PopulateMonitorSelector();
                    }
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
                Settings.GammaRScale = _gammaRSlider.Value / 100.0;
                Settings.GammaGScale = _gammaGSlider.Value / 100.0;
                Settings.GammaBScale = _gammaBSlider.Value / 100.0;
                Settings.GammaValue = _gammaValueSlider.Value / 100.0;
                Settings.MasterBrightness = _gammaBrightSlider.Value;
                Settings.GammaEnabled = _gammaCheckBox.Checked;
                if (Settings.GammaPerDisplay.Count > 0)
                {
                    Settings.GammaPerDisplay.Clear();
                    PopulateMonitorSelector();
                }
            }
            else
            {
                string deviceId = GetSelectedMonitorDeviceId();
                if (deviceId != null)
                {
                    if (!Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg))
                    {
                        pdg = new PerDisplayGamma
                        {
                            RScale = Settings.GammaRScale,
                            GScale = Settings.GammaGScale,
                            BScale = Settings.GammaBScale,
                            GammaValue = Settings.GammaValue,
                            MasterBrightness = Settings.MasterBrightness,
                            Enabled = Settings.GammaEnabled,
                            Source = "manual"
                        };
                        Settings.GammaPerDisplay[deviceId] = pdg;
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

            if (GammaCtrl.IsSupported)
                _bgService.ApplyGammaToSystem();

            SettingsStore.SaveSettings(Settings);
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

            var existing = Settings.CustomGammaPresets.FirstOrDefault(cp => cp.Name == name);
            if (existing != null)
            {
                if (MessageBox.Show($"预设 \"{name}\" 已存在，是否覆盖？", "确认覆盖",
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

            if (Settings.GammaPerDisplay != null && Settings.GammaPerDisplay.Count > 0)
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
            _gammaModeComboBox.SelectedItem = name;
            SettingsStore.SaveSettings(Settings);
            _bgService.UpdateTrayMenu();
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

            _gammaStatusLabel.Text = $"已删除自定义预设 \"{selected}\"";
        }

        private void MonitorSelectorComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;

            int prevIndex = _previousMonitorSelectedIndex;
            _previousMonitorSelectedIndex = _monitorSelectorComboBox.SelectedIndex;

            if (IsGlobalMonitorSelected() && Settings.GammaPerDisplay.Count > 0)
            {
                bool hasManual = Settings.GammaPerDisplay.Any(kvp => kvp.Value.Source == "manual");
                bool hasSchedule = Settings.GammaPerDisplay.Any(kvp => kvp.Value.Source == "schedule");

                if (hasManual || hasSchedule)
                {
                    string msg = hasSchedule && Settings.ScheduleEnabled
                        ? "切换到\"所有显示器\"模式将同步所有显示器参数，定时调度的独立配置将被清除。\n\n是否继续？"
                        : "切换到\"所有显示器\"模式将同步所有显示器参数，各显示器的独立配置将被清除。\n\n是否继续？";

                    if (MessageBox.Show(msg, "同步确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        _isUpdatingGammaSliders = true;
                        _monitorSelectorComboBox.SelectedIndex = prevIndex;
                        _isUpdatingGammaSliders = false;
                        return;
                    }
                }

                var primary = MonitorMgr.Monitors.FirstOrDefault(m => m.Screen?.Primary == true)
                    ?? MonitorMgr.Monitors.FirstOrDefault();

                if (primary != null && Settings.GammaPerDisplay.TryGetValue(primary.DeviceId, out var primaryPdg))
                {
                    Settings.GammaRScale = primaryPdg.RScale;
                    Settings.GammaGScale = primaryPdg.GScale;
                    Settings.GammaBScale = primaryPdg.BScale;
                    Settings.GammaValue = primaryPdg.GammaValue;
                    Settings.MasterBrightness = primaryPdg.MasterBrightness;
                    Settings.GammaEnabled = primaryPdg.Enabled;
                }

                Settings.GammaPerDisplay.Clear();
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

            bool isFromSchedule = Settings.GammaPerDisplay.TryGetValue(deviceId, out var pdg) && pdg.Source == "schedule";

            if (isFromSchedule && Settings.ScheduleEnabled)
            {
                if (MessageBox.Show(
                    "此配置由定时调度生成，重置后将在下次时段切换时恢复。\n\n是否仍要重置？",
                    "定时配置提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                    return;
            }

            Settings.GammaPerDisplay.Remove(deviceId);
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
                ? "已重置为全局设置（定时调度将在下次时段切换时恢复）"
                : "已重置为全局设置";
        }

        private void GammaScheduleToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            _bgService.SetScheduleEnabled(_gammaScheduleToggle.Checked);
            _scheduleEnabledCheckBox.Checked = Settings.ScheduleEnabled;
            UpdateScheduleOverrideStatus();
        }

        private void ScheduleEnabledCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            _bgService.SetScheduleEnabled(_scheduleEnabledCheckBox.Checked);
            _gammaScheduleToggle.Checked = Settings.ScheduleEnabled;
            UpdateScheduleOverrideStatus();
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
            _bgService.UpdateStartupRegistry();
            SettingsStore.SaveSettings(Settings);
        }

        private void StartMinimizedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.StartMinimized = _startMinimizedCheckBox.Checked;
            SettingsStore.SaveSettings(Settings);
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
            SyncSlidersToSettings();
            UpdateScheduleOverrideStatus();
            PopulateMonitorSelector();
        }

        private void RefreshAllMonitorUI()
        {
            if (_formDisposed || IsDisposed) return;
            UpdateBrightnessUI();
            UpdateGammaUI();
            _bgService.ApplyGammaToSystem();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_bgService.IsExiting)
            {
                _formDisposed = true;
                _resizeDebounceTimer?.Stop();
                _initTimer?.Stop();
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
            UnsubscribeEvents();
            _bgService.OnFormClosing(this);
            base.OnFormClosing(e);
        }

        private void UnsubscribeEvents()
        {
            _bgService.GammaController.StatusChanged -= OnGammaStatusChanged;
            _bgService.MonitorsChanged -= OnMonitorsChanged;
            _bgService.ScheduleStateChanged -= OnScheduleStateChanged;
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

                    int imgW = StaticBackgroundImage.Width;
                    int imgH = StaticBackgroundImage.Height;

                    float scale = Math.Max((float)w / imgW, (float)h / imgH);
                    int drawW = (int)(imgW * scale);
                    int drawH = (int)(imgH * scale);
                    int x = (w - drawW) / 2;
                    int y = (h - drawH) / 2;

                    g.DrawImage(StaticBackgroundImage, new Rectangle(x, y, drawW, drawH),
                        0, 0, imgW, imgH, GraphicsUnit.Pixel, attributes);
                }
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
                && Math.Abs(_lastRebuildCacheOpacity - opacity) < 0.001f)
                return;

            _lastRebuildCacheSize = currentSize;
            _lastRebuildCacheOpacity = opacity;

            _cachedBackground?.Dispose();
            _cachedBackground = null;

            Bitmap newCached = null;
            try
            {
                newCached = new Bitmap(formW, formH);
                using (var g = Graphics.FromImage(newCached))
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

                        int imgW = _backgroundImage.Width;
                        int imgH = _backgroundImage.Height;

                        float scale = Math.Max((float)formW / imgW, (float)formH / imgH);
                        int drawW = (int)(imgW * scale);
                        int drawH = (int)(imgH * scale);
                        int x = (formW - drawW) / 2;
                        int y = (formH - drawH) / 2;

                        g.DrawImage(_backgroundImage, new Rectangle(x, y, drawW, drawH),
                            0, 0, imgW, imgH, GraphicsUnit.Pixel, attributes);
                    }
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
                return;
            }

            int tabHeaderHeight = _tabControl.ItemSize.Height + 5;
            int controlW = _tabControl.ClientSize.Width;
            int controlH = Math.Max(_tabControl.ClientSize.Height - tabHeaderHeight, 100);

            float opacity = Settings.BackgroundImageOpacity / 100f;
            Size tabPageSize = new Size(controlW, controlH);

            if (_sharedTabPageBg == null ||
                Math.Abs(_lastTabPageBgOpacity - opacity) > 0.001f ||
                _lastTabPageBgSize != tabPageSize)
            {
                _sharedTabPageBg?.Dispose();
                _sharedTabPageBg = null;

                int pageW = Math.Max(tabPageSize.Width, 1);
                int pageH = Math.Max(tabPageSize.Height, 1);

                _sharedTabPageBg = new Bitmap(pageW, pageH);
                using (var g = Graphics.FromImage(_sharedTabPageBg))
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

                        int imgW = _backgroundImage.Width;
                        int imgH = _backgroundImage.Height;

                        float scale = Math.Max((float)pageW / imgW, (float)pageH / imgH);
                        int drawW = (int)(imgW * scale);
                        int drawH = (int)(imgH * scale);
                        int x = (pageW - drawW) / 2;
                        int y = (pageH - drawH) / 2;

                        g.DrawImage(_backgroundImage, new Rectangle(x, y, drawW, drawH),
                            0, 0, imgW, imgH, GraphicsUnit.Pixel, attributes);
                    }
                }

                _lastTabPageBgOpacity = opacity;
                _lastTabPageBgSize = tabPageSize;
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

        private void SubscribeBackgroundPaintEvents()
        {
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

        internal static void DrawBackgroundOnGraphics(Graphics g, Rectangle bounds, Point offset)
        {
            if (StaticCachedBackground == null) return;
            g.DrawImage(StaticCachedBackground, -offset.X, -offset.Y);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyTheme();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

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
