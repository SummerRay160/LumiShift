using System;
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

namespace LumiShift
{
    public partial class Form1 : Form
    {
        private static readonly string[] BuiltInPresets = { "标准", "防蓝光", "护眼模式", "游戏模式" };

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
        private ToggleSwitch _scheduleEnabledCheckBox;
        private DateTimePicker _scheduleNightStartPicker;
        private DateTimePicker _scheduleNightEndPicker;
        private ComboBox _scheduleNightPresetComboBox;
        private ComboBox _scheduleDayPresetComboBox;
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

            ThemeManager.CurrentMode = (ThemeMode)_settings.ThemeMode;
            ThemeManager.UpdateActiveTheme();
            ThemeManager.ThemeChanged += OnThemeChanged;

            _lastScheduleMode = "";
            _scheduleTimer = new Timer { Interval = 30000 };
            _scheduleTimer.Tick += ScheduleTimer_Tick;

            PopulatePresetComboBox();
            PopulateScheduleComboBoxes();
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
                return BuiltInPresets[0];

            for (int i = 0; i < BuiltInPresets.Length; i++)
            {
                double r = 0, g = 0, b = 0, gv = 0;
                switch (i)
                {
                    case 0: r = 1.0; g = 1.0; b = 1.0; gv = 1.0; break;
                    case 1: r = 1.05; g = 1.0; b = 0.78; gv = 1.0; break;
                    case 2: r = 1.08; g = 1.0; b = 0.70; gv = 1.08; break;
                    case 3: r = 0.98; g = 1.0; b = 1.06; gv = 0.92; break;
                }
                if (Math.Abs(_settings.GammaRScale - r) < 0.01 &&
                    Math.Abs(_settings.GammaGScale - g) < 0.01 &&
                    Math.Abs(_settings.GammaBScale - b) < 0.01 &&
                    Math.Abs(_settings.GammaValue - gv) < 0.01)
                    return BuiltInPresets[i];
            }

            foreach (var cp in _settings.CustomGammaPresets)
            {
                if (Math.Abs(_settings.GammaRScale - cp.RScale) < 0.01 &&
                    Math.Abs(_settings.GammaGScale - cp.GScale) < 0.01 &&
                    Math.Abs(_settings.GammaBScale - cp.BScale) < 0.01 &&
                    Math.Abs(_settings.GammaValue - cp.GammaValue) < 0.01 &&
                    _settings.MasterBrightness == cp.MasterBrightness)
                    return cp.Name;
            }

            return null;
        }

        private void PopulatePresetComboBox()
        {
            _isPopulatingComboBox = true;
            _gammaModeComboBox.Items.Clear();
            foreach (var p in BuiltInPresets)
                _gammaModeComboBox.Items.Add(p);
            foreach (var cp in _settings.CustomGammaPresets)
                _gammaModeComboBox.Items.Add(cp.Name);

            string current = GetCurrentPresetName();
            if (current != null)
                _gammaModeComboBox.SelectedItem = current;
            _isPopulatingComboBox = false;
        }

        private void PopulateScheduleComboBoxes()
        {
            FillScheduleCombo(_scheduleNightPresetComboBox, _settings.ScheduleNightPreset);
            FillScheduleCombo(_scheduleDayPresetComboBox, _settings.ScheduleDayPreset);
        }

        private void FillScheduleCombo(ComboBox cb, string selected)
        {
            cb.Items.Clear();
            foreach (var p in BuiltInPresets)
                cb.Items.Add(p);
            foreach (var cp in _settings.CustomGammaPresets)
                cb.Items.Add(cp.Name);
            if (cb.Items.Contains(selected))
                cb.SelectedItem = selected;
            else
                cb.SelectedIndex = 0;
        }

        private void RefreshCustomPresetButtons()
        {
            bool any = _settings.CustomGammaPresets.Count > 0;
            _gammaSaveCustomButton.Enabled = _settings.GammaEnabled && !_gammaSimplifiedCheckBox.Checked;
            _gammaDeleteCustomButton.Enabled = any && _gammaModeComboBox.SelectedItem is string selected && !BuiltInPresets.Contains(selected);
        }

        private bool TryApplyPreset(string name)
        {
            for (int i = 0; i < BuiltInPresets.Length; i++)
            {
                if (name == BuiltInPresets[i])
                {
                    ApplyBuiltInPreset(i);
                    return true;
                }
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
                return true;
            }
            return false;
        }

        private void ApplyBuiltInPreset(int index)
        {
            switch (index)
            {
                case 0:
                    _settings.GammaEnabled = false;
                    _settings.GammaRScale = 1.0;
                    _settings.GammaGScale = 1.0;
                    _settings.GammaBScale = 1.0;
                    _settings.GammaValue = 1.0;
                    break;
                case 1:
                    _settings.GammaEnabled = true;
                    _settings.GammaRScale = 1.05;
                    _settings.GammaGScale = 1.0;
                    _settings.GammaBScale = 0.78;
                    _settings.GammaValue = 1.0;
                    break;
                case 2:
                    _settings.GammaEnabled = true;
                    _settings.GammaRScale = 1.08;
                    _settings.GammaGScale = 1.0;
                    _settings.GammaBScale = 0.70;
                    _settings.GammaValue = 1.08;
                    break;
                case 3:
                    _settings.GammaEnabled = true;
                    _settings.GammaRScale = 0.98;
                    _settings.GammaGScale = 1.0;
                    _settings.GammaBScale = 1.06;
                    _settings.GammaValue = 0.92;
                    break;
            }
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

            if (supported)
            {
                _gammaCheckBox.Checked = _settings.GammaEnabled;
                _gammaSimplifiedCheckBox.Checked = false;

                _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(_settings.GammaRScale * 100.0));
                _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(_settings.GammaGScale * 100.0));
                _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(_settings.GammaBScale * 100.0));
                _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(_settings.GammaValue * 100.0));
                _gammaBrightSlider.Value = ClampSlider(_gammaBrightSlider, _settings.MasterBrightness);

                string current = GetCurrentPresetName();
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
            if (_scheduleManualOverride && _settings.ScheduleEnabled)
                _gammaStatusLabel.Text = "手动调整已覆盖定时设置，下次时段切换时恢复定时";
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

        private void ApplyGammaToSystem()
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
        }

        private void UpdateBrightnessUI()
        {
            _brightnessPanel.Controls.Clear();

            foreach (var monitor in _monitorManager.Monitors)
            {
                string deviceId = monitor.DeviceId;

                var row = new TableLayoutPanel
                {
                    ColumnCount = 3,
                    RowCount = 1,
                    AutoSize = true,
                    Width = 330,
                    Padding = new Padding(4),
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

                var tb = new ModernSlider
                {
                    Minimum = 0,
                    Maximum = 100,
                    Width = 180,
                    Value = currentBrightness,
                    Enabled = monitor.Controller?.IsSupported ?? false
                };
                var valLabel = new Label
                {
                    Text = $"{currentBrightness}%",
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(6, 0, 0, 0),
                    ForeColor = Colors.TextPrimary
                };

                tb.ValueChanged += (s, ev) =>
                {
                    valLabel.Text = $"{tb.Value}%";
                    _settings.BrightnessPerDisplay[deviceId] = tb.Value;
                    monitor.Controller?.SetBrightness(tb.Value);
                    SettingsStore.SaveSettings(_settings);
                };

                row.Controls.Add(new Label
                {
                    Text = monitor.DisplayName,
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Anchor = AnchorStyles.Left,
                    ForeColor = Colors.TextSecondary
                }, 0, 0);
                row.Controls.Add(tb, 1, 0);
                row.Controls.Add(valLabel, 2, 0);

                _brightnessPanel.Controls.Add(row);
            }
        }

        private void UpdateScheduleUI()
        {
            _isUpdatingSchedule = true;

            _scheduleEnabledCheckBox.Checked = _settings.ScheduleEnabled;
            _scheduleNightPresetComboBox.SelectedItem = _settings.ScheduleNightPreset;
            _scheduleDayPresetComboBox.SelectedItem = _settings.ScheduleDayPreset;

            try
            {
                var parts = _settings.ScheduleNightStart.Split(':');
                int h = int.Parse(parts[0]);
                int m = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                _scheduleNightStartPicker.Value = DateTime.Today.AddHours(h).AddMinutes(m);
            }
            catch
            {
                _scheduleNightStartPicker.Value = DateTime.Today.AddHours(18);
            }

            try
            {
                var parts = _settings.ScheduleNightEnd.Split(':');
                int h = int.Parse(parts[0]);
                int m = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                _scheduleNightEndPicker.Value = DateTime.Today.AddHours(h).AddMinutes(m);
            }
            catch
            {
                _scheduleNightEndPicker.Value = DateTime.Today.AddHours(6);
            }

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
            foreach (var p in BuiltInPresets)
            {
                bool isActive = _settings.GammaEnabled && GetCurrentPresetName() == p;
                var item = new ToolStripMenuItem(p) { Checked = isActive };
                string cp = p;
                item.Click += (s, ev) => QuickPreset_Click(cp);
                quickMenu.DropDownItems.Add(item);
            }
            if (_settings.CustomGammaPresets.Count > 0)
            {
                quickMenu.DropDownItems.Add(new ToolStripSeparator());
                foreach (var cp in _settings.CustomGammaPresets)
                {
                    bool isActive = _settings.GammaEnabled && GetCurrentPresetName() == cp.Name;
                    var item = new ToolStripMenuItem(cp.Name) { Checked = isActive };
                    string name = cp.Name;
                    item.Click += (s, ev) => QuickPreset_Click(name);
                    quickMenu.DropDownItems.Add(item);
                }
            }
            _trayMenu.Items.Add(quickMenu);
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
            _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(_settings.GammaRScale * 100.0));
            _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(_settings.GammaGScale * 100.0));
            _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(_settings.GammaBScale * 100.0));
            _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(_settings.GammaValue * 100.0));
            _gammaBrightSlider.Value = ClampSlider(_gammaBrightSlider, _settings.MasterBrightness);
            UpdateGammaLabels();
            UpdateColorTempFromSliders();
            string current = GetCurrentPresetName();
            if (current != null)
                _gammaModeComboBox.SelectedItem = current;
            _gammaCheckBox.Checked = _settings.GammaEnabled;
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
                var now = DateTime.Now;
                var startTime = _scheduleNightStartPicker.Value;
                var endTime = _scheduleNightEndPicker.Value;
                var nightStart = new TimeSpan(startTime.Hour, startTime.Minute, 0);
                var nightEnd = new TimeSpan(endTime.Hour, endTime.Minute, 0);
                var current = now.TimeOfDay;

                bool isNight;
                if (nightStart < nightEnd)
                    isNight = current >= nightStart && current < nightEnd;
                else
                    isNight = current >= nightStart || current < nightEnd;

                string targetMode = isNight ? _settings.ScheduleNightPreset : _settings.ScheduleDayPreset;

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
                    SyncSlidersToSettings();

                SettingsStore.SaveSettings(_settings);

                string tag = isNight ? "夜间" : "白天";
                _gammaStatusLabel.Text = $"定时切换: 已切换至{tag}预设 \"{targetMode}\"";
                UpdateTrayMenu();
                _lastScheduleMode = targetMode;
            }
            catch
            {
            }
        }

        #endregion

        #region Event Handlers

        private void GammaCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingGammaSliders) return;
            _settings.GammaEnabled = _gammaCheckBox.Checked;
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
            _settings.MasterBrightness = _gammaBrightSlider.Value;

            _gammaRSlider.Value = ClampSlider(_gammaRSlider, (int)Math.Round(_settings.GammaRScale * 100.0));
            _gammaGSlider.Value = ClampSlider(_gammaGSlider, (int)Math.Round(_settings.GammaGScale * 100.0));
            _gammaBSlider.Value = ClampSlider(_gammaBSlider, (int)Math.Round(_settings.GammaBScale * 100.0));
            _gammaValueSlider.Value = ClampSlider(_gammaValueSlider, (int)Math.Round(_settings.GammaValue * 100.0));

            UpdateGammaLabels();
            UpdateColorTempLabel();
            _gammaCheckBox.Checked = true;

            string current = GetCurrentPresetName();
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

            if (_settings.ScheduleEnabled)
                _scheduleManualOverride = true;

            int builtInIndex = Array.IndexOf(BuiltInPresets, selected);
            if (builtInIndex >= 0)
            {
                ApplyBuiltInPreset(builtInIndex);
            }
            else
            {
                var custom = _settings.CustomGammaPresets.FirstOrDefault(cp => cp.Name == selected);
                if (custom != null)
                {
                    _settings.GammaEnabled = custom.Enabled;
                    _settings.GammaRScale = custom.RScale;
                    _settings.GammaGScale = custom.GScale;
                    _settings.GammaBScale = custom.BScale;
                    _settings.GammaValue = custom.GammaValue;
                    _settings.MasterBrightness = custom.MasterBrightness;
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
            _settings.GammaRScale = _gammaRSlider.Value / 100.0;
            _settings.GammaGScale = _gammaGSlider.Value / 100.0;
            _settings.GammaBScale = _gammaBSlider.Value / 100.0;
            _settings.GammaValue = _gammaValueSlider.Value / 100.0;
            _settings.MasterBrightness = _gammaBrightSlider.Value;
            _settings.GammaEnabled = _gammaCheckBox.Checked;

            UpdateColorTempFromSliders();
            string current = GetCurrentPresetName();
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

            if (Array.IndexOf(BuiltInPresets, name) >= 0)
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
            _settings.CustomGammaPresets.Add(preset);

            PopulatePresetComboBox();
            PopulateScheduleComboBoxes();
            _gammaModeComboBox.SelectedItem = name;
            SettingsStore.SaveSettings(_settings);
            UpdateTrayMenu();
            RefreshCustomPresetButtons();

            _gammaStatusLabel.Text = $"已保存自定义预设 \"{name}\"";
        }

        private void GammaDeleteCustomButton_Click(object sender, EventArgs e)
        {
            if (!(_gammaModeComboBox.SelectedItem is string selected)) return;
            if (BuiltInPresets.Contains(selected)) return;

            if (MessageBox.Show($"确定要删除自定义预设 \"{selected}\" 吗？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            bool wasActive = GetCurrentPresetName() == selected;
            _settings.CustomGammaPresets.RemoveAll(cp => cp.Name == selected);

            PopulatePresetComboBox();
            PopulateScheduleComboBoxes();

            if (wasActive)
            {
                ApplyBuiltInPreset(0);
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
            TryApplyPreset(presetName);
            SyncSlidersToSettings();
            ApplyGammaToSystem();
            UpdateTrayMenu();
            SettingsStore.SaveSettings(_settings);
            UpdateScheduleOverrideStatus();
        }

        private void ScheduleEnabledCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            _settings.ScheduleEnabled = _scheduleEnabledCheckBox.Checked;
            _scheduleTimer.Enabled = _settings.ScheduleEnabled;
            if (_settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);
            }
            SettingsStore.SaveSettings(_settings);
        }

        private void ScheduleNightStartPicker_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            var t = _scheduleNightStartPicker.Value;
            _settings.ScheduleNightStart = $"{t.Hour:D2}:{t.Minute:D2}";
            if (_settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);
            }
            SettingsStore.SaveSettings(_settings);
        }

        private void ScheduleNightEndPicker_ValueChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            var t = _scheduleNightEndPicker.Value;
            _settings.ScheduleNightEnd = $"{t.Hour:D2}:{t.Minute:D2}";
            if (_settings.ScheduleEnabled)
            {
                _lastScheduleMode = "";
                _scheduleManualOverride = false;
                ScheduleTimer_Tick(null, null);
            }
            SettingsStore.SaveSettings(_settings);
        }

        private void SchedulePresetComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSchedule) return;
            _settings.ScheduleNightPreset = _scheduleNightPresetComboBox.SelectedItem?.ToString() ?? "护眼模式";
            _settings.ScheduleDayPreset = _scheduleDayPresetComboBox.SelectedItem?.ToString() ?? "标准";
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

        private void ExitApplication()
        {
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