using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LumiShift.Controls;
using LumiShift.Infrastructure;
using LumiShift.Resources;
using Microsoft.Win32;

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
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;

                if (_bgService != null)
                {
                    _bgService.GammaController.StatusChanged -= OnGammaStatusChanged;
                    _bgService.MonitorsChanged -= OnMonitorsChanged;
                    _bgService.ScheduleStateChanged -= OnScheduleStateChanged;
                }

                if (_brightnessPanel != null)
                {
                    foreach (Control c in _brightnessPanel.Controls)
                        c.Dispose();
                    _brightnessPanel.Controls.Clear();
                }

                _brightnessRows?.Clear();

                if (_tabControl != null)
                {
                    foreach (TabPage page in _tabControl.TabPages)
                    {
                        foreach (Control c in page.Controls)
                        {
                            if (c is Panel panel)
                            {
                                foreach (Control child in panel.Controls)
                                    child.Dispose();
                                panel.Controls.Clear();
                            }
                            c.Dispose();
                        }
                        page.Controls.Clear();

                        var pageBg = page.BackgroundImage;
                        page.BackgroundImage = null;
                        pageBg?.Dispose();
                    }
                }

                BackgroundImage = null;
                _cachedBackground?.Dispose();
                _cachedBackground = null;
                _backgroundImage?.Dispose();
                _backgroundImage = null;

                StaticCachedBackground?.Dispose();
                StaticCachedBackground = null;
                StaticBackgroundImage?.Dispose();
                StaticBackgroundImage = null;
                StaticUseBackgroundImage = false;

                if (components != null)
                    components.Dispose();
            }
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

            ClientSize = new Size(400, 500);
            Controls.Add(_tabControl);
            Text = "LumiShift";
            MinimumSize = new Size(400, 500);
            MaximumSize = new Size(400, 500);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = LoadAppIcon();

            ResumeLayout(false);
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
            if (role == 'b')
                lbl.BackColor = Colors.Border;
            else
                lbl.BackColor = Color.Transparent;

            if (role == 'p')
                lbl.ForeColor = Colors.TextPrimary;
            else if (role == 's')
                lbl.ForeColor = Colors.TextSecondary;
            else if (role == 'g')
                lbl.ForeColor = Colors.Green;
            else if (role == 'r')
                lbl.ForeColor = Colors.Red;
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

            var monitorLabel = new Label
            {
                Text = "显示器",
                Location = new Point(Spacing.LG, gy + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(monitorLabel, 's');

            _monitorSelectorComboBox = new ComboBox
            {
                Location = new Point(62, gy),
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _monitorSelectorComboBox.SelectedIndexChanged += MonitorSelectorComboBox_SelectedIndexChanged;

            _resetDisplayGammaButton = new Button
            {
                Text = "重置",
                Location = new Point(228, gy),
                Width = 50,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.Red,
                Font = Typography.Caption,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Enabled = false,
                Tag = "resetDisplayGamma"
            };
            _resetDisplayGammaButton.Click += ResetDisplayGammaButton_Click;
            _resetDisplayGammaButton.MouseEnter += (s, e) => { _resetDisplayGammaButton.BackColor = Colors.Red; _resetDisplayGammaButton.ForeColor = Color.White; };
            _resetDisplayGammaButton.MouseLeave += (s, e) => { _resetDisplayGammaButton.BackColor = Colors.Surface; _resetDisplayGammaButton.ForeColor = Colors.Red; };

            gy += 30;

            var monitorHint = new Label
            {
                Text = "选择具体显示器可独立调整，[手动]/[定时] 表示配置来源",
                Location = new Point(Spacing.LG, gy),
                AutoSize = true,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            gy += 18;

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
                Width = 150,
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
                Location = new Point(210, gy),
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
                Location = new Point(264, gy),
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

            var scheduleQuickLabel = new Label
            {
                Text = "定时",
                Location = new Point(Spacing.LG, gy + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(scheduleQuickLabel, 's');

            _gammaScheduleToggle = new ToggleSwitch { Location = new Point(54, gy), Checked = false };
            _gammaScheduleToggle.CheckedChanged += GammaScheduleToggle_CheckedChanged;

            _gammaScheduleConfigButton = new Button
            {
                Text = "配置...",
                Location = new Point(108, gy),
                Width = 60,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Caption,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _gammaScheduleConfigButton.Click += ScheduleConfigButton_Click;

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
                Location = new Point(100, gy),
                Width = 170,
                Minimum = 0,
                Maximum = 100,
                Value = 50
            };
            _gammaColorTempSlider.ValueChanged += GammaColorTempSlider_ValueChanged;

            _gammaColorTempLabel = new Label
            {
                Text = "适中",
                Location = new Point(278, gy + 2),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(_gammaColorTempLabel, 's');

            gy += 30;

            var rLbl = new Label { Text = "R", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(rLbl, 's');
            _gammaRSlider = new ModernSlider { Location = new Point(54, gy), Width = 210, Minimum = 50, Maximum = 150, Value = 100 };
            _gammaRSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaRLabel = new Label { Text = "1.00", Location = new Point(272, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaRLabel, 'g');
            gy += 28;

            var gLbl = new Label { Text = "G", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(gLbl, 's');
            _gammaGSlider = new ModernSlider { Location = new Point(54, gy), Width = 210, Minimum = 50, Maximum = 150, Value = 100 };
            _gammaGSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaGLabel = new Label { Text = "1.00", Location = new Point(272, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaGLabel, 'g');
            gy += 28;

            var bLbl = new Label { Text = "B", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(bLbl, 's');
            _gammaBSlider = new ModernSlider { Location = new Point(54, gy), Width = 210, Minimum = 10, Maximum = 150, Value = 100 };
            _gammaBSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaBLabel = new Label { Text = "1.00", Location = new Point(272, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaBLabel, 'g');
            gy += 28;

            var gvLbl = new Label { Text = "γ", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(gvLbl, 's');
            _gammaValueSlider = new ModernSlider { Location = new Point(54, gy), Width = 210, Minimum = 50, Maximum = 200, Value = 100 };
            _gammaValueSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaValueLabel = new Label { Text = "1.00", Location = new Point(272, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaValueLabel, 'g');
            gy += 28;

            var brightLbl = new Label { Text = "亮度", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(brightLbl, 's');
            _gammaBrightSlider = new ModernSlider { Location = new Point(54, gy), Width = 210, Minimum = 0, Maximum = 100, Value = 100 };
            _gammaBrightSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaBrightLabel = new Label { Text = "100%", Location = new Point(272, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaBrightLabel, 'p');
            gy += 30;

            _gammaStatusLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, gy),
                Width = 340,
                Height = 18,
                Font = Typography.Caption
            };
            SetLabelTheme(_gammaStatusLabel, 's');

            _gammaTab.Controls.AddRange(new Control[] {
                _gammaCheckBox, gammaLabel,
                monitorLabel, _monitorSelectorComboBox, _resetDisplayGammaButton, monitorHint,
                presetLabel, _gammaModeComboBox, _gammaSaveCustomButton, _gammaDeleteCustomButton,
                scheduleQuickLabel, _gammaScheduleToggle, _gammaScheduleConfigButton,
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
                Width = 400,
                Height = 410,
                AutoScroll = false,
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
                BackColor = Colors.Background,
                AutoScroll = true
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

            _scheduleConfigButton = new Button
            {
                Text = "配置定时...",
                Location = new Point(240, sy),
                Width = 100,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Caption,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _scheduleConfigButton.Click += ScheduleConfigButton_Click;

            sy += 28;

            var scheduleHint = new Label
            {
                Text = "可为每个时段指定不同显示器的预设方案",
                Location = new Point(Spacing.LG, sy),
                AutoSize = true,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            sy += 20;

            var sepLine1 = new Label
            {
                Location = new Point(Spacing.LG, sy),
                Width = 340,
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
                Width = 160,
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
                Width = 340,
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
                Width = 90,
                Minimum = 5,
                Maximum = 80,
                Value = 30
            };
            _bgImageOpacitySlider.ValueChanged += BgImageOpacitySlider_ValueChanged;

            _bgImageOpacityLabel = new Label
            {
                Text = "30%",
                Location = new Point(Spacing.LG + 306, sy + 2),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(_bgImageOpacityLabel, 's');

            sy += 28;

            _bgImageStatusLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, sy),
                Width = 340,
                Height = 16,
                Font = Typography.Caption
            };
            SetLabelTheme(_bgImageStatusLabel, 's');

            sy += 20;

            var sepLine3 = new Label
            {
                Location = new Point(Spacing.LG, sy),
                Width = 340,
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
                Width = 340,
                Height = 1,
                Font = Typography.Caption
            };
            SetLabelTheme(sepLine4, 'b');
            sy += 12;

            var versionLabel = new Label
            {
                Text = $"v{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}  ·  SummerRay160",
                Location = new Point(Spacing.LG, sy),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(versionLabel, 's');

            var githubLink = new LinkLabel
            {
                Text = "GitHub",
                Location = new Point(Spacing.LG + versionLabel.PreferredWidth + 8, sy),
                AutoSize = true,
                Font = Typography.Caption,
                BackColor = Color.Transparent,
                LinkColor = Colors.TextSecondary,
                ActiveLinkColor = Colors.Brand,
                VisitedLinkColor = Colors.TextSecondary
            };
            githubLink.LinkClicked += (s, e) => System.Diagnostics.Process.Start("https://github.com/SummerRay160/LumiShift");
            
            _settingsTab.Controls.AddRange(new Control[] {
                _scheduleEnabledCheckBox, scheduleLabel2,
                _scheduleConfigButton, scheduleHint,
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
                versionLabel,
                githubLink
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
                Width = 340,
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

            int presetButtonWidth = 100;
            int presetGap = 10;
            int presetTotalWidth = presetButtonWidth * 3 + presetGap * 2;
            int presetContainerWidth = 340;
            int presetStartX = Spacing.LG + (presetContainerWidth - presetTotalWidth) / 2;

            _eyeProtectionPreset1Button = CreatePresetButton(presetStartX, ey, presetButtonWidth, presetColors[0]);
            _eyeProtectionPreset2Button = CreatePresetButton(presetStartX + presetButtonWidth + presetGap, ey, presetButtonWidth, presetColors[1]);
            _eyeProtectionPreset3Button = CreatePresetButton(presetStartX + (presetButtonWidth + presetGap) * 2, ey, presetButtonWidth, presetColors[2]);

            ey += 30;

            _eyeProtectionCustomButton = new Button
            {
                Text = "自定义颜色",
                Location = new Point(Spacing.LG, ey),
                Width = 340,
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
                Width = 340,
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
                Width = 340,
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