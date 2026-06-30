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
        private bool _designerDisposed;

        protected override void Dispose(bool disposing)
        {
            if (_designerDisposed) return;
            _designerDisposed = true;
            GC.SuppressFinalize(this);
            if (disposing)
            {
                try
                {
                    _initTimer?.Stop();
                    _initTimer?.Dispose();
                    _initTimer = null;

                    _resizeDebounceTimer?.Stop();
                    _resizeDebounceTimer?.Dispose();
                    _resizeDebounceTimer = null;

                    ClientSizeChanged -= OnFormClientSizeChanged;

                    if (_bgService != null)
                    {
                        _bgService.GammaController.StatusChanged -= OnGammaStatusChanged;
                        _bgService.MonitorsChanged -= OnMonitorsChanged;
                        _bgService.ScheduleStateChanged -= OnScheduleStateChanged;
                    }

                    if (_tabControl != null)
                        _tabControl.TabSelected -= OnTabSelected;

                    if (_brightnessPanel != null)
                    {
                        foreach (Control c in _brightnessPanel.Controls)
                        {
                            if (c is Panel row && row.Tag is string deviceId)
                            {
                                if (_brightnessSliderHandlers.TryGetValue(deviceId, out var handler))
                                {
                                    if (row.Controls.Count > 1 && row.Controls[1] is ModernSlider slider)
                                        slider.ValueChanged -= handler;
                                    _brightnessSliderHandlers.Remove(deviceId);
                                }
                            }
                            if (c is Panel row2)
                            {
                                foreach (Control child in row2.Controls)
                                    child.Dispose();
                                row2.Controls.Clear();
                            }
                            c.Dispose();
                        }
                        _brightnessPanel.Controls.Clear();
                    }

                    _brightnessSliderHandlers?.Clear();
                    _brightnessRows?.Clear();

                    if (_tabControl != null)
                    {
                        foreach (TabPage page in _tabControl.TabPages)
                        {
                            var pageBg = page.BackgroundImage;
                            page.BackgroundImage = null;
                            if (pageBg != null && pageBg != _sharedTabPageBg)
                                pageBg.Dispose();

                            foreach (Control c in page.Controls)
                            {
                                if (c is Panel panel)
                                {
                                    foreach (Control child in panel.Controls)
                                        child.Dispose();
                                    panel.Controls.Clear();
                                }
                                else if (c is FlowLayoutPanel flowPanel)
                                {
                                    foreach (Control child in flowPanel.Controls)
                                    {
                                        if (child is Panel flowRow)
                                        {
                                            foreach (Control grandChild in flowRow.Controls)
                                                grandChild.Dispose();
                                            flowRow.Controls.Clear();
                                        }
                                        child.Dispose();
                                    }
                                    flowPanel.Controls.Clear();
                                }
                                c.Dispose();
                            }
                            page.Controls.Clear();
                        }
                    }

                    _sharedTabPageBg?.Dispose();
                    _sharedTabPageBg = null;

                    var oldFormBg = BackgroundImage;
                    BackgroundImage = null;
                    oldFormBg?.Dispose();

                    if (_cachedBackground != null)
                    {
                        if (!ReferenceEquals(_cachedBackground, StaticCachedBackground))
                            _cachedBackground.Dispose();
                        _cachedBackground = null;
                    }

                    if (_backgroundImage != null)
                    {
                        if (!ReferenceEquals(_backgroundImage, StaticBackgroundImage))
                            _backgroundImage.Dispose();
                        _backgroundImage = null;
                    }

                    CleanupStaticFields();

                    if (components != null)
                        components.Dispose();
                }
                catch
                {
                    _backgroundImage?.Dispose();
                    _backgroundImage = null;
                    _cachedBackground?.Dispose();
                    _cachedBackground = null;
                    _sharedTabPageBg?.Dispose();
                    _sharedTabPageBg = null;
                    StaticBackgroundImage = null;
                    StaticCachedBackground = null;
                }
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

            ClientSize = new Size(430, 540);
            Controls.Add(_tabControl);
            Text = "LumiShift";
            MinimumSize = new Size(430, 540);
            MaximumSize = new Size(430, 540);
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

        private Label CreateTitleLabel(string text, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(Spacing.LG, y),
                AutoSize = true,
                Font = Typography.H1
            };
            SetLabelTheme(lbl, 'p');
            return lbl;
        }

        private Label CreateHintLabel(string text, int y, int width = 380)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(Spacing.LG, y),
                Width = width,
                Height = 18,
                Font = Typography.Caption
            };
            SetLabelTheme(lbl, 's');
            return lbl;
        }

        private Label CreateSeparator(int y, int width = 382)
        {
            var lbl = new Label
            {
                Location = new Point(Spacing.LG, y),
                Width = width,
                Height = 1,
                Font = Typography.Caption
            };
            SetLabelTheme(lbl, 'b');
            return lbl;
        }

        // ======================================================================
        //  Gamma Tab
        // ======================================================================
        private void BuildGammaTab()
        {
            _gammaTab = new TabPage("调光")
            {
                BackColor = Colors.Background
            };

            int gy = 14;

            var titleLabel = CreateTitleLabel("屏幕显示调节", gy);
            gy += 24;

            var titleHint = CreateHintLabel("先选择全部显示器或单台显示器，调好后可保存为显示方案。", gy);
            gy += 30;

            _gammaCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, gy), Checked = false };
            _gammaCheckBox.CheckedChanged += GammaCheckBox_CheckedChanged;

            var gammaLabel = new Label
            {
                Text = "启用显示调节",
                Location = new Point(Spacing.LG + 48, gy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(gammaLabel, 'p');

            gy += 32;

            var monitorLabel = new Label
            {
                Text = "范围",
                Location = new Point(Spacing.LG, gy + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(monitorLabel, 's');

            _monitorSelectorComboBox = new ComboBox
            {
                Location = new Point(72, gy),
                Width = 182,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _monitorSelectorComboBox.SelectedIndexChanged += MonitorSelectorComboBox_SelectedIndexChanged;

            _resetDisplayGammaButton = new Button
            {
                Text = "跟随全部",
                Location = new Point(262, gy),
                Width = 86,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.Red,
                Font = Typography.Caption,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Enabled = false,
                Tag = "resetDisplayGamma"
            };
            _resetDisplayGammaButton.Click += ResetDisplayGammaButton_Click;
            _resetDisplayGammaButton.MouseEnter += (s, e) => { _resetDisplayGammaButton.BackColor = Colors.Red; _resetDisplayGammaButton.ForeColor = Color.White; };
            _resetDisplayGammaButton.MouseLeave += (s, e) => { _resetDisplayGammaButton.BackColor = Colors.Surface; _resetDisplayGammaButton.ForeColor = Colors.Red; };

            gy += 30;

            var monitorHint = new Label
            {
                Text = "全部显示器适合统一调节；选择单台可做独立调整。",
                Location = new Point(Spacing.LG, gy),
                Width = 360,
                Height = 18,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            gy += 18;

            var presetLabel = new Label
            {
                Text = "显示方案",
                Location = new Point(Spacing.LG, gy + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(presetLabel, 's');

            _gammaModeComboBox = new ComboBox
            {
                Location = new Point(92, gy),
                Width = 152,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            _gammaModeComboBox.SelectedIndexChanged += GammaModeComboBox_SelectedIndexChanged;

            _gammaSaveCustomButton = new Button
            {
                Text = "保存方案",
                Location = new Point(252, gy),
                Width = 74,
                Height = 26,
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
                Location = new Point(332, gy),
                Width = 54,
                Height = 26,
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
                Text = "定时切换",
                Location = new Point(Spacing.LG, gy + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(scheduleQuickLabel, 's');

            _gammaScheduleToggle = new ToggleSwitch { Location = new Point(92, gy), Checked = false };
            _gammaScheduleToggle.CheckedChanged += GammaScheduleToggle_CheckedChanged;

            _gammaScheduleConfigButton = new Button
            {
                Text = "配置...",
                Location = new Point(146, gy),
                Width = 70,
                Height = 26,
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
                Text = "简洁模式：只调节冷暖和亮度",
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
                Location = new Point(164, gy),
                Width = 150,
                Minimum = 0,
                Maximum = 100,
                Value = 50
            };
            _gammaColorTempSlider.ValueChanged += GammaColorTempSlider_ValueChanged;

            _gammaColorTempLabel = new Label
            {
                Text = "适中",
                Location = new Point(322, gy + 2),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(_gammaColorTempLabel, 's');

            gy += 30;

            var rLbl = new Label { Text = "R", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(rLbl, 's');
            _gammaRSlider = new ModernSlider { Location = new Point(72, gy), Width = 240, Minimum = 50, Maximum = 150, Value = 100 };
            _gammaRSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaRLabel = new Label { Text = "1.00", Location = new Point(322, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaRLabel, 'g');
            gy += 28;

            var gLbl = new Label { Text = "G", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(gLbl, 's');
            _gammaGSlider = new ModernSlider { Location = new Point(72, gy), Width = 240, Minimum = 50, Maximum = 150, Value = 100 };
            _gammaGSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaGLabel = new Label { Text = "1.00", Location = new Point(322, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaGLabel, 'g');
            gy += 28;

            var bLbl = new Label { Text = "B", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(bLbl, 's');
            _gammaBSlider = new ModernSlider { Location = new Point(72, gy), Width = 240, Minimum = 10, Maximum = 150, Value = 100 };
            _gammaBSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaBLabel = new Label { Text = "1.00", Location = new Point(322, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaBLabel, 'g');
            gy += 28;

            var gvLbl = new Label { Text = "γ", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(gvLbl, 's');
            _gammaValueSlider = new ModernSlider { Location = new Point(72, gy), Width = 240, Minimum = 50, Maximum = 200, Value = 100 };
            _gammaValueSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaValueLabel = new Label { Text = "1.00", Location = new Point(322, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaValueLabel, 'g');
            gy += 28;

            var brightLbl = new Label { Text = "亮度", Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(brightLbl, 's');
            _gammaBrightSlider = new ModernSlider { Location = new Point(72, gy), Width = 240, Minimum = 0, Maximum = 100, Value = 100 };
            _gammaBrightSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaBrightLabel = new Label { Text = "100%", Location = new Point(322, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaBrightLabel, 'p');
            gy += 30;

            _gammaStatusLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, gy),
                Width = 380,
                Height = 18,
                Font = Typography.Caption
            };
            SetLabelTheme(_gammaStatusLabel, 's');

            _gammaTab.Controls.AddRange(new Control[] {
                titleLabel, titleHint,
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
                Location = new Point(Spacing.LG, 72),
                Width = 382,
                Height = 416,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var titleLabel = CreateTitleLabel("硬件亮度", 14);
            var titleHint = CreateHintLabel("调节显示器硬件亮度；不支持的设备会自动隐藏。", 38);
            var separator = CreateSeparator(62);

            _brightnessTab.Controls.AddRange(new Control[] { titleLabel, titleHint, separator });
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

            const int settingsContentWidth = 360;
            int sy = 14;

            var titleLabel = CreateTitleLabel("偏好设置", sy);
            sy += 24;

            var titleHint = CreateHintLabel("管理定时、通知、启动和界面选项，默认保持轻量运行。", sy, settingsContentWidth);
            sy += 34;

            _scheduleEnabledCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _scheduleEnabledCheckBox.CheckedChanged += ScheduleEnabledCheckBox_CheckedChanged;

            var scheduleLabel2 = new Label
            {
                Text = "自动定时切换",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(scheduleLabel2, 'p');

            _scheduleConfigButton = new Button
            {
                Text = "配置定时...",
                Location = new Point(278, sy),
                Width = 104,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _scheduleConfigButton.Click += ScheduleConfigButton_Click;

            sy += 30;

            var scheduleHint = new Label
            {
                Text = "到指定时间自动切换显示方案，适合白天、夜间和办公场景。",
                Location = new Point(Spacing.LG, sy),
                AutoSize = true,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            sy += 20;

            var sepLine1 = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            _bgImageToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _bgImageToggle.CheckedChanged += BgImageToggle_CheckedChanged;

            var bgImageLabel = new Label
            {
                Text = "轻量背景图",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(bgImageLabel, 'p');

            sy += 30;

            _bgImageSelectButton = new Button
            {
                Text = "选择图片",
                Location = new Point(Spacing.LG, sy),
                Width = 92,
                Height = 26,
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
                Width = 62,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.Red,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter
            };
            _bgImageClearButton.Click += BgImageClearButton_Click;

            var opacityLbl = new Label { Text = "透明度", Location = new Point(Spacing.LG + 170, sy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(opacityLbl, 's');

            _bgImageOpacitySlider = new ModernSlider
            {
                Location = new Point(Spacing.LG + 218, sy),
                Width = 104,
                Minimum = 5,
                Maximum = 80,
                Value = 30
            };
            _bgImageOpacitySlider.ValueChanged += BgImageOpacitySlider_ValueChanged;

            _bgImageOpacityLabel = new Label
            {
                Text = "30%",
                Location = new Point(Spacing.LG + 328, sy + 2),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(_bgImageOpacityLabel, 's');

            sy += 28;

            _bgImageStatusLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, sy),
                Width = settingsContentWidth,
                Height = 16,
                Font = Typography.Caption
            };
            SetLabelTheme(_bgImageStatusLabel, 's');

            sy += 20;

            var sepLine3 = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            _startWithWindowsCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _startWithWindowsCheckBox.CheckedChanged += StartWithWindowsCheckBox_CheckedChanged;

            var startupLbl = new Label
            {
                Text = "开机自启动",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
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
                Font = Typography.BodyBold
            };
            SetLabelTheme(minimizedLbl, 'p');

            sy += 30;

            _autoCheckUpdatesToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _autoCheckUpdatesToggle.CheckedChanged += AutoCheckUpdatesToggle_CheckedChanged;

            var autoCheckUpdatesLbl = new Label
            {
                Text = "启动时自动检查更新",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(autoCheckUpdatesLbl, 'p');

            sy += 34;

            var restoreGammaSep = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            _restoreGammaToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _restoreGammaToggle.CheckedChanged += RestoreGammaToggle_CheckedChanged;

            var restoreGammaLbl = new Label
            {
                Text = "退出时还原系统显示效果",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(restoreGammaLbl, 'p');

            sy += 34;

            var notificationSep = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            _notificationsEnabledToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notificationsEnabledToggle.CheckedChanged += NotificationsEnabledToggle_CheckedChanged;

            var notificationLbl = new Label
            {
                Text = "Windows 通知提醒",
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(notificationLbl, 'p');

            sy += 30;

            _notifyStartupToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notifyStartupToggle.CheckedChanged += NotifyStartupToggle_CheckedChanged;
            var notifyStartupLbl = new Label { Text = "软件启动时通知", Location = new Point(Spacing.LG + 48, sy + 1), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(notifyStartupLbl, 's');

            sy += 28;

            _notifyScheduleToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notifyScheduleToggle.CheckedChanged += NotifyScheduleToggle_CheckedChanged;
            var notifyScheduleLbl = new Label { Text = "定时切换方案时通知", Location = new Point(Spacing.LG + 48, sy + 1), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(notifyScheduleLbl, 's');

            sy += 28;

            _notifyStatusToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notifyStatusToggle.CheckedChanged += NotifyStatusToggle_CheckedChanged;
            var notifyStatusLbl = new Label { Text = "显示调节开关变化时通知", Location = new Point(Spacing.LG + 48, sy + 1), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(notifyStatusLbl, 's');

            sy += 28;

            _notifyMonitorToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notifyMonitorToggle.CheckedChanged += NotifyMonitorToggle_CheckedChanged;
            var notifyMonitorLbl = new Label { Text = "显示器变更时通知", Location = new Point(Spacing.LG + 48, sy + 1), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(notifyMonitorLbl, 's');

            sy += 34;

            var sepLine4 = CreateSeparator(sy, settingsContentWidth);
            sy += 16;

            var versionPanel = new Panel
            {
                Location = new Point(Spacing.LG, sy),
                Width = settingsContentWidth,
                Height = 54,
                BackColor = Colors.Surface
            };

            var versionLabel = new Label
            {
                Text = $"v{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}  ·  SummerRay160",
                Location = new Point(12, 10),
                AutoSize = true,
                Font = Typography.BodyBold,
                BackColor = Color.Transparent
            };
            SetLabelTheme(versionLabel, 's');

            var versionHintLabel = new Label
            {
                Text = "轻量显示调节工具",
                Location = new Point(12, 30),
                AutoSize = true,
                Font = Typography.Caption,
                BackColor = Color.Transparent
            };
            SetLabelTheme(versionHintLabel, 's');

            var githubLink = new LinkLabel
            {
                Text = "GitHub",
                Location = new Point(settingsContentWidth - 58, 18),
                AutoSize = true,
                Font = Typography.Caption,
                BackColor = Color.Transparent,
                LinkColor = Colors.TextSecondary,
                ActiveLinkColor = Colors.Brand,
                VisitedLinkColor = Colors.TextSecondary
            };
            githubLink.LinkClicked += (s, e) => System.Diagnostics.Process.Start("https://github.com/SummerRay160/LumiShift");
            versionPanel.Controls.AddRange(new Control[] { versionLabel, versionHintLabel, githubLink });
            sy += 70;
            
            _settingsTab.Controls.AddRange(new Control[] {
                titleLabel, titleHint,
                _scheduleEnabledCheckBox, scheduleLabel2,
                _scheduleConfigButton, scheduleHint,
                sepLine1,
                _bgImageToggle, bgImageLabel,
                _bgImageSelectButton, _bgImageClearButton, opacityLbl, _bgImageOpacitySlider, _bgImageOpacityLabel,
                _bgImageStatusLabel,
                sepLine3,
                _startWithWindowsCheckBox, startupLbl,
                _startMinimizedCheckBox, minimizedLbl,
                _autoCheckUpdatesToggle, autoCheckUpdatesLbl,
                restoreGammaSep, _restoreGammaToggle, restoreGammaLbl,
                notificationSep, _notificationsEnabledToggle, notificationLbl,
                _notifyStartupToggle, notifyStartupLbl,
                _notifyScheduleToggle, notifyScheduleLbl,
                _notifyStatusToggle, notifyStatusLbl,
                _notifyMonitorToggle, notifyMonitorLbl,
                sepLine4,
                versionPanel
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

            int ey = 14;

            var titleLabel = CreateTitleLabel("护眼模式", ey);
            ey += 24;

            var titleHint = CreateHintLabel("为窗口背景添加柔和色调，减少长时间阅读的刺眼感。", ey);
            ey += 34;

            _eyeProtectionToggle = new ToggleSwitch { Location = new Point(Spacing.LG, ey), Checked = false };
            _eyeProtectionToggle.CheckedChanged += EyeProtectionToggle_CheckedChanged;

            var eyeLabel = new Label
            {
                Text = "启用系统护眼色",
                Location = new Point(Spacing.LG + 48, ey + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(eyeLabel, 'p');

            ey += 34;

            var sep1 = CreateSeparator(ey);

            ey += 10;

            var presetHint = new Label
            {
                Text = "护眼颜色",
                Location = new Point(Spacing.LG, ey + 2),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(presetHint, 'p');

            ey += 28;

            var presetColors = new (string text, int r, int g, int b)[]
            {
                ("绿豆沙色", 204, 232, 207),
                ("纸页黄", 255, 255, 224),
                ("天空蓝", 199, 216, 237)
            };

            int presetButtonWidth = 116;
            int presetGap = 8;
            int presetTotalWidth = presetButtonWidth * 3 + presetGap * 2;
            int presetContainerWidth = 382;
            int presetStartX = Spacing.LG + (presetContainerWidth - presetTotalWidth) / 2;

            _eyeProtectionPreset1Button = CreatePresetButton(presetStartX, ey, presetButtonWidth, presetColors[0]);
            _eyeProtectionPreset2Button = CreatePresetButton(presetStartX + presetButtonWidth + presetGap, ey, presetButtonWidth, presetColors[1]);
            _eyeProtectionPreset3Button = CreatePresetButton(presetStartX + (presetButtonWidth + presetGap) * 2, ey, presetButtonWidth, presetColors[2]);

            ey += 30;

            _eyeProtectionCustomButton = new Button
            {
                Text = "自定义颜色",
                Location = new Point(Spacing.LG, ey),
                Width = 382,
                Height = 30,
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
                Width = 382,
                Height = 30,
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
                Width = 382,
                Height = 18,
                Font = Typography.Caption
            };
            SetLabelTheme(_eyeProtectionStatusLabel, 's');

            _eyeProtectionTab.Controls.AddRange(new Control[] {
                titleLabel, titleHint,
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
