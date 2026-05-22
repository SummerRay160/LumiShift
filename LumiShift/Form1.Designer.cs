using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LumiShift.Controls;
using LumiShift.Resources;

namespace LumiShift
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components;
        private FlatTabControl _tabControl;
        private TabPage _gammaTab;
        private TabPage _brightnessTab;
        private TabPage _settingsTab;
        private TabPage _eyeProtectionTab;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SuspendLayout();

            _tabControl = new FlatTabControl
            {
                Dock = DockStyle.Fill,
                Font = Typography.Body
            };
            _tabControl.TabSelected += OnTabSelected;

            BuildGammaTab();
            BuildBrightnessTab();
            BuildSettingsTab();
            BuildEyeProtectionTab();

            _tabControl.TabPages.AddRange(new[] { _gammaTab, _brightnessTab, _settingsTab, _eyeProtectionTab });

            ClientSize = new Size(350, 410);
            Controls.Add(_tabControl);
            Text = "LumiShift";
            MinimumSize = new Size(350, 450);
            MaximumSize = new Size(350, 450);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = LoadAppIcon();

            ResumeLayout(false);

            _trayIcon = new NotifyIcon(components)
            {
                Text = "LumiShift",
                Icon = LoadAppIcon(),
                Visible = false
            };
            _trayMenu = new ContextMenuStrip(components);
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void RefreshTabTheme()
        {
            var c = Colors.Background;
            _tabControl.BackColor = c;
            _gammaTab.BackColor = c;
            _brightnessTab.BackColor = c;
            _settingsTab.BackColor = c;
            _eyeProtectionTab.BackColor = c;
            _tabControl.Invalidate();
        }

        private void OnTabSelected(object sender, int index)
        {
        }

        private void SetLabelTheme(Label lbl, char role, bool isBold = false)
        {
            lbl.Tag = role;
            ApplyLabelTheme(lbl, role);
        }

        internal static void ApplyLabelTheme(Label lbl, char role)
        {
            if (role == 'p')
                lbl.ForeColor = Colors.TextPrimary;
            else if (role == 's')
                lbl.ForeColor = Colors.TextSecondary;
            else if (role == 'g')
                lbl.ForeColor = Colors.Green;
            else if (role == 'r')
                lbl.ForeColor = Colors.Red;
            else if (role == 'b')
                lbl.BackColor = Colors.Border;
            else
                lbl.ForeColor = Colors.TextPrimary;
        }

        // ======================================================================
        //  Gamma Tab
        // ======================================================================
        private void BuildGammaTab()
        {
            _gammaTab = new TabPage("Gamma")
            {
                BackColor = Colors.Background
            };

            int gy = 6;

            _gammaCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, gy), Checked = false };
            _gammaCheckBox.CheckedChanged += GammaCheckBox_CheckedChanged;

            var gammaLabel = new Label
            {
                Text = "Gamma 校正",
                Location = new Point(Spacing.LG + 48, gy + 1),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(gammaLabel, 's');

            gy += 30;

            var presetLabel = new Label
            {
                Text = "预设",
                Location = new Point(Spacing.LG, gy + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(presetLabel, 's');

            _gammaModeComboBox = new ComboBox
            {
                Location = new Point(54, gy),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _gammaModeComboBox.SelectedIndexChanged += GammaModeComboBox_SelectedIndexChanged;

            _gammaSaveCustomButton = new Button
            {
                Text = "保存",
                Location = new Point(190, gy),
                Width = 50,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Brand,
                ForeColor = Color.White,
                Font = Typography.Caption,
                FlatAppearance = { BorderSize = 0 },
                Enabled = false
            };
            _gammaSaveCustomButton.Click += GammaSaveCustomButton_Click;
            _gammaSaveCustomButton.MouseEnter += (s, e) => _gammaSaveCustomButton.BackColor = Colors.BrandHover;
            _gammaSaveCustomButton.MouseLeave += (s, e) => _gammaSaveCustomButton.BackColor = Colors.Brand;

            _gammaDeleteCustomButton = new Button
            {
                Text = "删除",
                Location = new Point(244, gy),
                Width = 50,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.Red,
                Font = Typography.Caption,
                FlatAppearance = { BorderSize = 0 },
                Enabled = false
            };
            _gammaDeleteCustomButton.Click += GammaDeleteCustomButton_Click;
            _gammaDeleteCustomButton.MouseEnter += (s, e) => { _gammaDeleteCustomButton.BackColor = Colors.Red; _gammaDeleteCustomButton.ForeColor = Color.White; };
            _gammaDeleteCustomButton.MouseLeave += (s, e) => { _gammaDeleteCustomButton.BackColor = Colors.Surface; _gammaDeleteCustomButton.ForeColor = Colors.Red; };

            gy += 30;

            _gammaSimplifiedCheckBox = new CheckBox
            {
                Text = "只调节色温",
                Location = new Point(Spacing.LG, gy + 1),
                AutoSize = true,
                Font = Typography.Body,
                ForeColor = Colors.TextSecondary,
                BackColor = Colors.Background,
                FlatStyle = FlatStyle.Flat
            };
            _gammaSimplifiedCheckBox.CheckedChanged += GammaSimplifiedCheckBox_CheckedChanged;

            _gammaColorTempSlider = new ModernSlider
            {
                Location = new Point(90, gy),
                Width = 150,
                Minimum = 0,
                Maximum = 100,
                Value = 50
            };
            _gammaColorTempSlider.ValueChanged += GammaColorTempSlider_ValueChanged;

            _gammaColorTempLabel = new Label
            {
                Text = "适中",
                Location = new Point(248, gy + 2),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(_gammaColorTempLabel, 's');

            gy += 30;

            // R, G, B, Gamma, Brightness
            var rLbl = new Label { Text = "R", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(rLbl, 's');
            _gammaRSlider = new ModernSlider { Location = new Point(54, gy), Width = 180, Minimum = 50, Maximum = 150, Value = 100 };
            _gammaRSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaRLabel = new Label { Text = "1.00", Location = new Point(242, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaRLabel, 'g');
            gy += 28;

            var gLbl = new Label { Text = "G", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(gLbl, 's');
            _gammaGSlider = new ModernSlider { Location = new Point(54, gy), Width = 180, Minimum = 50, Maximum = 150, Value = 100 };
            _gammaGSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaGLabel = new Label { Text = "1.00", Location = new Point(242, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaGLabel, 'g');
            gy += 28;

            var bLbl = new Label { Text = "B", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(bLbl, 's');
            _gammaBSlider = new ModernSlider { Location = new Point(54, gy), Width = 180, Minimum = 10, Maximum = 150, Value = 100 };
            _gammaBSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaBLabel = new Label { Text = "1.00", Location = new Point(242, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaBLabel, 'g');
            gy += 28;

            var gvLbl = new Label { Text = "γ", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(gvLbl, 's');
            _gammaValueSlider = new ModernSlider { Location = new Point(54, gy), Width = 180, Minimum = 50, Maximum = 200, Value = 100 };
            _gammaValueSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaValueLabel = new Label { Text = "1.00", Location = new Point(242, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaValueLabel, 'g');
            gy += 28;

            var brightLbl = new Label { Text = "亮度", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(brightLbl, 's');
            _gammaBrightSlider = new ModernSlider { Location = new Point(54, gy), Width = 180, Minimum = 0, Maximum = 100, Value = 100 };
            _gammaBrightSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaBrightLabel = new Label { Text = "100%", Location = new Point(242, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaBrightLabel, 'p');
            gy += 30;

            _gammaStatusLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, gy),
                Width = 280,
                Height = 18,
                Font = Typography.Caption
            };
            SetLabelTheme(_gammaStatusLabel, 's');

            _gammaTab.Controls.AddRange(new Control[] {
                _gammaCheckBox, gammaLabel,
                presetLabel, _gammaModeComboBox, _gammaSaveCustomButton, _gammaDeleteCustomButton,
                _gammaSimplifiedCheckBox, _gammaColorTempSlider, _gammaColorTempLabel,
                rLbl, _gammaRSlider, _gammaRLabel,
                gLbl, _gammaGSlider, _gammaGLabel,
                bLbl, _gammaBSlider, _gammaBLabel,
                gvLbl, _gammaValueSlider, _gammaValueLabel,
                brightLbl, _gammaBrightSlider, _gammaBrightLabel,
                _gammaStatusLabel
            });
        }

        // ======================================================================
        //  Brightness Tab
        // ======================================================================
        private void BuildBrightnessTab()
        {
            _brightnessTab = new TabPage("亮度")
            {
                BackColor = Colors.Background
            };

            _brightnessPanel = new FlowLayoutPanel
            {
                Location = new Point(0, 0),
                Width = 350,
                Height = 360,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            _brightnessTab.Controls.Add(_brightnessPanel);
        }

        // ======================================================================
        //  Settings Tab
        // ======================================================================
        private void BuildSettingsTab()
        {
            _settingsTab = new TabPage("设置")
            {
                BackColor = Colors.Background
            };

            int sy = 6;

            _scheduleEnabledCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _scheduleEnabledCheckBox.CheckedChanged += ScheduleEnabledCheckBox_CheckedChanged;

            var scheduleLabel2 = new Label
            {
                Text = "定时切换",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(scheduleLabel2, 'p');

            sy += 30;

            var nightLbl2 = new Label { Text = "夜间", Location = new Point(Spacing.LG, sy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(nightLbl2, 's');

            _scheduleNightStartPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Location = new Point(54, sy),
                Width = 72,
                Value = DateTime.Today.AddHours(18),
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _scheduleNightStartPicker.ValueChanged += ScheduleNightStartPicker_ValueChanged;

            var tildeLbl2 = new Label { Text = "~", Location = new Point(130, sy + 2), AutoSize = true };
            SetLabelTheme(tildeLbl2, 's');

            _scheduleNightEndPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Location = new Point(144, sy),
                Width = 72,
                Value = DateTime.Today.AddHours(6),
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _scheduleNightEndPicker.ValueChanged += ScheduleNightEndPicker_ValueChanged;

            _scheduleNightPresetComboBox = new ComboBox
            {
                Location = new Point(222, sy),
                Width = 80,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _scheduleNightPresetComboBox.SelectedIndexChanged += SchedulePresetComboBox_SelectedIndexChanged;

            sy += 30;

            var dayLbl2 = new Label { Text = "白天", Location = new Point(Spacing.LG, sy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(dayLbl2, 's');

            _scheduleDayPresetComboBox = new ComboBox
            {
                Location = new Point(54, sy),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _scheduleDayPresetComboBox.SelectedIndexChanged += SchedulePresetComboBox_SelectedIndexChanged;

            sy += 34;

            var sepLine1 = new Label
            {
                Location = new Point(Spacing.LG, sy),
                Width = 286,
                Height = 1,
                Font = Typography.Caption
            };
            SetLabelTheme(sepLine1, 'b');
            sy += 10;

            var themeLabel = new Label
            {
                Text = "主题模式",
                Location = new Point(Spacing.LG, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(themeLabel, 'p');

            _themeComboBox = new ComboBox
            {
                Location = new Point(160, sy),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _themeComboBox.Items.AddRange(new object[] { "深色模式", "浅色模式", "跟随系统" });
            _themeComboBox.SelectedIndex = 2;
            _themeComboBox.SelectedIndexChanged += ThemeComboBox_SelectedIndexChanged;

            sy += 30;

            var sepLine2 = new Label
            {
                Location = new Point(Spacing.LG, sy),
                Width = 286,
                Height = 1,
                Font = Typography.Caption
            };
            SetLabelTheme(sepLine2, 'b');
            sy += 10;

            _bgImageToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _bgImageToggle.CheckedChanged += BgImageToggle_CheckedChanged;

            var bgImageLabel = new Label
            {
                Text = "自定义背景",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(bgImageLabel, 'p');

            sy += 28;

            _bgImageSelectButton = new Button
            {
                Text = "选择图片",
                Location = new Point(Spacing.LG, sy),
                Width = 90,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter
            };
            _bgImageSelectButton.Click += BgImageSelectButton_Click;

            _bgImageClearButton = new Button
            {
                Text = "清除",
                Location = new Point(Spacing.LG + 96, sy),
                Width = 60,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.Red,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter
            };
            _bgImageClearButton.Click += BgImageClearButton_Click;

            var opacityLbl = new Label { Text = "透明度", Location = new Point(Spacing.LG + 164, sy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(opacityLbl, 's');

            _bgImageOpacitySlider = new ModernSlider
            {
                Location = new Point(Spacing.LG + 210, sy),
                Width = 60,
                Minimum = 5,
                Maximum = 80,
                Value = 30
            };
            _bgImageOpacitySlider.ValueChanged += BgImageOpacitySlider_ValueChanged;

            _bgImageOpacityLabel = new Label
            {
                Text = "30%",
                Location = new Point(Spacing.LG + 274, sy + 2),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(_bgImageOpacityLabel, 's');

            sy += 28;

            _bgImageStatusLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, sy),
                Width = 280,
                Height = 16,
                Font = Typography.Caption
            };
            SetLabelTheme(_bgImageStatusLabel, 's');

            sy += 20;

            var sepLine3 = new Label
            {
                Location = new Point(Spacing.LG, sy),
                Width = 286,
                Height = 1,
                Font = Typography.Caption
            };
            SetLabelTheme(sepLine3, 'b');
            sy += 10;

            _startWithWindowsCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _startWithWindowsCheckBox.CheckedChanged += StartWithWindowsCheckBox_CheckedChanged;

            var startupLbl = new Label
            {
                Text = "开机自启动",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(startupLbl, 'p');

            sy += 30;

            _startMinimizedCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _startMinimizedCheckBox.CheckedChanged += StartMinimizedCheckBox_CheckedChanged;

            var minimizedLbl = new Label
            {
                Text = "启动时最小化到托盘",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(minimizedLbl, 'p');

            sy += 34;

            var sepLine4 = new Label
            {
                Location = new Point(Spacing.LG, sy),
                Width = 286,
                Height = 1,
                Font = Typography.Caption
            };
            SetLabelTheme(sepLine4, 'b');
            sy += 12;

            var githubButton = new Button
            {
                Text = "前往 GitHub",
                Location = new Point(Spacing.LG, sy),
                Width = 286,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Brand,
                ForeColor = Color.White,
                Font = Typography.BodyBold,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = Colors.BrandHover },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            githubButton.Click += (s, e) => System.Diagnostics.Process.Start("https://github.com/SummerRay160/LumiShift");

            sy += 36;

            var versionLabel = new Label
            {
                Text = $"v{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}  ·  SummerRay160",
                Location = new Point(Spacing.LG, sy),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(versionLabel, 's');
            
            sy += 20;

            var copyrightLabel = new Label
            {
                Text = "© 2026 SummerRay160",
                Location = new Point(Spacing.LG, sy),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(copyrightLabel, 's');

            _settingsTab.Controls.AddRange(new Control[] {
                _scheduleEnabledCheckBox, scheduleLabel2,
                nightLbl2, _scheduleNightStartPicker, tildeLbl2, _scheduleNightEndPicker, _scheduleNightPresetComboBox,
                dayLbl2, _scheduleDayPresetComboBox,
                sepLine1,
                themeLabel, _themeComboBox,
                sepLine2,
                _bgImageToggle, bgImageLabel,
                _bgImageSelectButton, _bgImageClearButton, opacityLbl, _bgImageOpacitySlider, _bgImageOpacityLabel,
                _bgImageStatusLabel,
                sepLine3,
                _startWithWindowsCheckBox, startupLbl,
                _startMinimizedCheckBox, minimizedLbl,
                sepLine4,
                githubButton,
                versionLabel, copyrightLabel
            });
        }

        // ======================================================================
        //  Eye Protection Tab
        // ======================================================================
        private void BuildEyeProtectionTab()
        {
            _eyeProtectionTab = new TabPage("护眼")
            {
                BackColor = Colors.Background
            };

            int ey = 6;

            _eyeProtectionToggle = new ToggleSwitch { Location = new Point(Spacing.LG, ey), Checked = false };
            _eyeProtectionToggle.CheckedChanged += EyeProtectionToggle_CheckedChanged;

            var eyeLabel = new Label
            {
                Text = "系统护眼模式",
                Location = new Point(Spacing.LG + 48, ey + 1),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(eyeLabel, 's');

            ey += 34;

            var sep1 = new Label
            {
                Location = new Point(Spacing.LG, ey),
                Width = 286,
                Height = 1,
                Font = Typography.Caption
            };
            SetLabelTheme(sep1, 'b');

            ey += 10;

            var presetHint = new Label
            {
                Text = "预设方案",
                Location = new Point(Spacing.LG, ey + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(presetHint, 'p');

            ey += 28;

            var presetColors = new (string text, int r, int g, int b)[]
            {
                ("绿豆沙色", 204, 232, 207),
                ("纸页黄", 255, 255, 224),
                ("天空蓝", 199, 216, 237)
            };

            int presetButtonWidth = 86;
            int presetGap = 8;
            int presetStartX = Spacing.LG;

            _eyeProtectionPreset1Button = CreatePresetButton(presetStartX, ey, presetButtonWidth, presetColors[0]);
            _eyeProtectionPreset2Button = CreatePresetButton(presetStartX + presetButtonWidth + presetGap, ey, presetButtonWidth, presetColors[1]);
            _eyeProtectionPreset3Button = CreatePresetButton(presetStartX + (presetButtonWidth + presetGap) * 2, ey, presetButtonWidth, presetColors[2]);

            ey += 30;

            _eyeProtectionCustomButton = new Button
            {
                Text = "自定义颜色",
                Location = new Point(Spacing.LG, ey),
                Width = 286,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter
            };
            _eyeProtectionCustomButton.Click += EyeProtectionCustomButton_Click;

            ey += 36;

            _eyeProtectionRestoreButton = new Button
            {
                Text = "恢复默认",
                Location = new Point(Spacing.LG, ey),
                Width = 286,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.Red,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter
            };
            _eyeProtectionRestoreButton.Click += EyeProtectionRestoreButton_Click;

            ey += 36;

            _eyeProtectionStatusLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, ey),
                Width = 280,
                Height = 18,
                Font = Typography.Caption
            };
            SetLabelTheme(_eyeProtectionStatusLabel, 's');

            _eyeProtectionTab.Controls.AddRange(new Control[] {
                _eyeProtectionToggle, eyeLabel,
                sep1,
                presetHint,
                _eyeProtectionPreset1Button, _eyeProtectionPreset2Button, _eyeProtectionPreset3Button,
                _eyeProtectionCustomButton,
                _eyeProtectionRestoreButton,
                _eyeProtectionStatusLabel
            });
        }

        private Button CreatePresetButton(int x, int y, int width, (string text, int r, int g, int b) preset)
        {
            var btn = new Button
            {
                Text = preset.text,
                Location = new Point(x, y),
                Width = width,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Font = Typography.Body,
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = preset
            };
            btn.Click += EyeProtectionPresetButton_Click;
            return btn;
        }
    }
}