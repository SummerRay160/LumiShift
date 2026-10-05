using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using LumiShift.Controls;
using LumiShift.Infrastructure;
using LumiShift.Resources;
using LumiShift.Services;
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
            Icon = Program.AppIcon;

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

        private Label CreateSeparator(int y, int width = 382)
        {
            var lbl = new Label
            {
                Location = new Point(Spacing.LG, y),
                // 固定宽度的分隔线（显式关闭 AutoSize，避免宽度被忽略）
                AutoSize = false,
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
            _gammaTab = new TabPage(Lang.Get("调光"))
            {
                BackColor = Colors.Background
            };

            // 与设置页相同的内容宽度：固定窄宽防止多语言长文本撑出横向滚动条
            const int settingsContentWidth = 360;
            int gy = 14;

            var titleLabel = CreateTitleLabel(Lang.Get("屏幕显示调节"), gy);
            gy += 24;

            gy += 6;

            _gammaCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, gy), Checked = false };
            _gammaCheckBox.CheckedChanged += GammaCheckBox_CheckedChanged;

            var gammaLabel = new Label
            {
                Text = Lang.Get("启用显示调节"),
                Location = new Point(Spacing.LG + 48, gy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(gammaLabel, 'p');

            // —— 基础调节：启用 / 亮度 / 色温（高频操作前置，永远可用） ——
            gy += 32;

            var brightLbl = new Label { Text = Lang.Get("亮度"), Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(brightLbl, 's');
            _gammaBrightSlider = new ModernSlider { Location = new Point(72, gy), Width = 240, Minimum = 0, Maximum = 100, Value = 100 };
            _gammaBrightSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaBrightLabel = new Label { Text = "100%", Location = new Point(322, gy + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaBrightLabel, 'p');

            gy += 30;

            var tempLbl = new Label { Text = Lang.Get("色温"), Location = new Point(Spacing.LG, gy + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(tempLbl, 's');
            _gammaColorTempSlider = new ModernSlider { Location = new Point(72, gy), Width = 240, Minimum = 0, Maximum = 100, Value = 50 };
            _gammaColorTempSlider.ValueChanged += GammaColorTempSlider_ValueChanged;
            _gammaColorTempLabel = new Label { Text = Lang.Get("适中"), Location = new Point(322, gy + 2), AutoSize = true, Font = Typography.Caption };
            SetLabelTheme(_gammaColorTempLabel, 's');

            // —— 进阶调参折叠区：显示方案 + R/G/B/γ（默认收起，展开状态见 ApplyGammaAdvancedLayout） ——
            gy += 34;

            var sep1 = CreateSeparator(gy, 360);
            gy += 12;

            _advToggleLabel = new Label
            {
                Text = "▸ " + Lang.Get("进阶调参 · R/G/B/γ 与显示方案"),
                Location = new Point(Spacing.LG, gy),
                AutoSize = true,
                Font = Typography.BodyBold,
                Cursor = Cursors.Hand
            };
            _advToggleLabel.Click += AdvToggleLabel_Click;
            SetLabelTheme(_advToggleLabel, 'p');

            gy += 24;
            _gammaAdvBaseY = gy;

            _gammaAdvancedPanel = new Panel
            {
                Location = new Point(0, gy),
                Width = 364,
                Height = 152,
                BackColor = Colors.Background
            };

            var presetLabel = new Label
            {
                Text = Lang.Get("显示方案"),
                Location = new Point(Spacing.LG, 8),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(presetLabel, 's');

            _gammaModeComboBox = new BlurComboBox
            {
                Location = new Point(92, 6),
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
                Text = Lang.Get("保存方案"),
                Location = new Point(246, 4),
                Width = 68,
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
                Text = Lang.Get("删除"),
                Location = new Point(308, 4),
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

            int py = 38;
            var rLbl = new Label { Text = "R", Location = new Point(Spacing.LG, py + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(rLbl, 's');
            _gammaRSlider = new ModernSlider { Location = new Point(72, py), Width = 240, Minimum = 50, Maximum = 150, Value = 100 };
            _gammaRSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaRLabel = new Label { Text = "1.00", Location = new Point(322, py + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaRLabel, 'g');
            py += 28;

            var gLbl = new Label { Text = "G", Location = new Point(Spacing.LG, py + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(gLbl, 's');
            _gammaGSlider = new ModernSlider { Location = new Point(72, py), Width = 240, Minimum = 50, Maximum = 150, Value = 100 };
            _gammaGSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaGLabel = new Label { Text = "1.00", Location = new Point(322, py + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaGLabel, 'g');
            py += 28;

            var bLbl = new Label { Text = "B", Location = new Point(Spacing.LG, py + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(bLbl, 's');
            _gammaBSlider = new ModernSlider { Location = new Point(72, py), Width = 240, Minimum = 10, Maximum = 150, Value = 100 };
            _gammaBSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaBLabel = new Label { Text = "1.00", Location = new Point(322, py + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaBLabel, 'g');
            py += 28;

            var gvLbl = new Label { Text = "γ", Location = new Point(Spacing.LG, py + 2), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(gvLbl, 's');
            _gammaValueSlider = new ModernSlider { Location = new Point(72, py), Width = 240, Minimum = 50, Maximum = 200, Value = 100 };
            _gammaValueSlider.ValueChanged += GammaSlider_ValueChanged;
            _gammaValueLabel = new Label { Text = "1.00", Location = new Point(322, py + 2), AutoSize = true, Font = Typography.Mono };
            SetLabelTheme(_gammaValueLabel, 'g');

            _gammaAdvancedPanel.Controls.AddRange(new Control[] {
                presetLabel, _gammaModeComboBox, _gammaSaveCustomButton, _gammaDeleteCustomButton,
                rLbl, _gammaRSlider, _gammaRLabel,
                gLbl, _gammaGSlider, _gammaGLabel,
                bLbl, _gammaBSlider, _gammaBLabel,
                gvLbl, _gammaValueSlider, _gammaValueLabel
            });

            // —— 范围与定时：纵坐标随折叠状态移动（ApplyGammaAdvancedLayout） ——
            _gammaSep2 = CreateSeparator(gy, 360);

            int my = gy + 12;
            _monitorLabel = new Label
            {
                Text = Lang.Get("范围"),
                Location = new Point(Spacing.LG, my + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(_monitorLabel, 's');

            _monitorSelectorComboBox = new BlurComboBox
            {
                Location = new Point(72, my),
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
                Text = Lang.Get("跟随全部"),
                Location = new Point(262, my),
                Width = 86,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextSecondary,
                Font = Typography.Caption,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Enabled = false,
                Tag = "resetDisplayGamma"
            };
            _resetDisplayGammaButton.Click += ResetDisplayGammaButton_Click;
            _resetDisplayGammaButton.MouseEnter += (s, e) => { _resetDisplayGammaButton.BackColor = Colors.BrandHover; _resetDisplayGammaButton.ForeColor = Color.White; };
            _resetDisplayGammaButton.MouseLeave += (s, e) => { _resetDisplayGammaButton.BackColor = Colors.Surface; _resetDisplayGammaButton.ForeColor = Colors.TextSecondary; };

            my += 30;

            _scheduleQuickLabel = new Label
            {
                Text = Lang.Get("定时切换"),
                Location = new Point(Spacing.LG, my + 2),
                AutoSize = true,
                Font = Typography.Body
            };
            SetLabelTheme(_scheduleQuickLabel, 's');

            _gammaScheduleToggle = new ToggleSwitch { Location = new Point(92, my), Checked = false };
            _gammaScheduleToggle.CheckedChanged += GammaScheduleToggle_CheckedChanged;

            _gammaScheduleConfigButton = new Button
            {
                Text = Lang.Get("配置..."),
                Location = new Point(146, my),
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

            my += 30;

            _gammaTargetLabel = new Label
            {
                Text = Lang.Get("正在调整所有屏幕"),
                Location = new Point(Spacing.LG, my),
                // 与设置页一致的宽度处理：固定宽 + AutoSize 关闭，长文本由 AutoEllipsis/WordBreak 处理
                AutoSize = false,
                Width = settingsContentWidth,
                Height = 18,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            my += 20;

            _gammaGuideLabel = new Label
            {
                Text = Lang.Get("调“所有显示器”时，每块屏幕都会变；单独选某一块时，改动只对它生效，其他屏幕不动。")
                     + "\n" + Lang.Get("方案就是把当前调好的效果存个名字，以后选中就能直接用，定时切换也会用到它。"),
                Location = new Point(Spacing.LG, my),
                AutoSize = false,
                Width = settingsContentWidth,
                Height = 64,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            // 与设置页相同：按实际文本换行计算高度，多语言下长度不同也不会溢出（+6 渲染余量防英文末行被裁）
            _gammaGuideLabel.Height = Math.Max(18, TextRenderer.MeasureText(_gammaGuideLabel.Text, _gammaGuideLabel.Font,
                new Size(_gammaGuideLabel.Width, 0), TextFormatFlags.WordBreak).Height + 6);
            my += _gammaGuideLabel.Height + 2;

            _gammaStatusLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, my),
                // AutoEllipsis 仅在 AutoSize=false 时生效；否则长状态文本会撑宽 label 顶出横向滚动条
                AutoSize = false,
                Width = settingsContentWidth,
                Height = 18,
                Font = Typography.Caption,
                AutoEllipsis = true
            };
            SetLabelTheme(_gammaStatusLabel, 's');

            _gammaTab.Controls.AddRange(new Control[] {
                titleLabel, _gammaCheckBox, gammaLabel,
                brightLbl, _gammaBrightSlider, _gammaBrightLabel,
                tempLbl, _gammaColorTempSlider, _gammaColorTempLabel,
                sep1, _advToggleLabel,
                _gammaAdvancedPanel, _gammaSep2,
                _monitorLabel, _monitorSelectorComboBox, _resetDisplayGammaButton,
                _scheduleQuickLabel, _gammaScheduleToggle, _gammaScheduleConfigButton,
                _gammaTargetLabel, _gammaGuideLabel,
                _gammaStatusLabel
            });

            // 展开进阶调参后内容超出选项卡高度：允许竖向滚动。
            // 注意面板宽度必须小于"客户区-竖向滚动条宽度"（约397px），否则竖条出现时会连带冒出横向滚动条
            _gammaTab.AutoScroll = true;
            _gammaTab.AutoScrollMargin = new Size(0, 8);

            ApplyGammaAdvancedLayout();
        }

        // ======================================================================
        //  Brightness Tab
        // ======================================================================
        private void BuildBrightnessTab()
        {
            _brightnessTab = new TabPage(Lang.Get("亮度"))
            {
                BackColor = Colors.Background
            };

            _brightnessPanel = new FlowLayoutPanel
            {
                Location = new Point(Spacing.LG, 52),
                Width = 382,
                Height = 416,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var titleLabel = CreateTitleLabel(Lang.Get("硬件亮度"), 14);
            var separator = CreateSeparator(44);

            _brightnessTab.Controls.AddRange(new Control[] { titleLabel, separator });
            _brightnessTab.Controls.Add(_brightnessPanel);
        }

        // ======================================================================
        //  Settings Tab
        // ======================================================================
        private void BuildSettingsTab()
        {
            _settingsTab = new TabPage(Lang.Get("设置"))
            {
                BackColor = Colors.Background,
                AutoScroll = true
            };

            const int settingsContentWidth = 360;
            int sy = 14;

            var titleLabel = CreateTitleLabel(Lang.Get("偏好设置"), sy);
            sy += 24;

            sy += 6;

            _scheduleEnabledCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _scheduleEnabledCheckBox.CheckedChanged += ScheduleEnabledCheckBox_CheckedChanged;

            var scheduleLabel2 = new Label
            {
                Text = Lang.Get("自动定时切换"),
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(scheduleLabel2, 'p');

            _scheduleConfigButton = new Button
            {
                Text = Lang.Get("配置定时..."),
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

            // 不能用 AutoSize：长英文文案会超出窗口宽度撑出横向滚动条；固定宽度让其换行
            var scheduleHint = new Label
            {
                Text = Lang.Get("到指定时间自动切换显示方案，适合白天、夜间和办公场景。"),
                Location = new Point(Spacing.LG, sy),
                Width = settingsContentWidth,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };
            scheduleHint.Height = Math.Max(18, TextRenderer.MeasureText(scheduleHint.Text, scheduleHint.Font,
                new Size(scheduleHint.Width, 0), TextFormatFlags.WordBreak).Height + 2);

            sy += scheduleHint.Height + 2;

            var sepLine1 = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            _bgImageToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _bgImageToggle.CheckedChanged += BgImageToggle_CheckedChanged;

            var bgImageLabel = new Label
            {
                Text = Lang.Get("轻量背景图"),
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(bgImageLabel, 'p');

            sy += 30;

            _bgImageSelectButton = new Button
            {
                Text = Lang.Get("选择图片"),
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
                Text = Lang.Get("清除"),
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

            var opacityLbl = new Label { Text = Lang.Get("透明度"), Location = new Point(Spacing.LG + 170, sy + 2), AutoSize = true, Font = Typography.Body };
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
                Font = Typography.Caption,
                AutoEllipsis = true
            };
            SetLabelTheme(_bgImageStatusLabel, 's');

            sy += 20;

            var sepLine3 = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            _startWithWindowsCheckBox = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _startWithWindowsCheckBox.CheckedChanged += StartWithWindowsCheckBox_CheckedChanged;

            var startupLbl = new Label
            {
                Text = Lang.Get("开机自启动"),
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
                Text = Lang.Get("启动时最小化到托盘"),
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
                Text = Lang.Get("启动时自动检查更新"),
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
                Text = Lang.Get("退出时还原系统显示效果"),
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(restoreGammaLbl, 'p');

            sy += 30;

            var languageLbl = new Label
            {
                Text = Lang.Get("界面语言"),
                Location = new Point(Spacing.LG, sy + 4),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(languageLbl, 'p');

            _languageComboBox = new BlurComboBox
            {
                Location = new Point(278, sy),
                Width = 104,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            // 语言名称用各自母语写法（不随界面语言变化）；"跟随系统"仍随界面语言翻译
            _languageComboBox.Items.AddRange(new object[] { Lang.Get("跟随系统"), "简体中文", "繁體中文", "English" });
            _languageComboBox.SelectedIndex = 0;
            _languageComboBox.SelectedIndexChanged += LanguageComboBox_SelectedIndexChanged;

            sy += 34;

            var notificationSep = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            _notificationsEnabledToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notificationsEnabledToggle.CheckedChanged += NotificationsEnabledToggle_CheckedChanged;

            var notificationLbl = new Label
            {
                Text = Lang.Get("Windows 通知提醒"),
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(notificationLbl, 'p');

            sy += 30;

            _notifyStartupToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notifyStartupToggle.CheckedChanged += NotifyStartupToggle_CheckedChanged;
            var notifyStartupLbl = new Label { Text = Lang.Get("软件启动时通知"), Location = new Point(Spacing.LG + 48, sy + 1), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(notifyStartupLbl, 's');

            sy += 28;

            _notifyScheduleToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notifyScheduleToggle.CheckedChanged += NotifyScheduleToggle_CheckedChanged;
            var notifyScheduleLbl = new Label { Text = Lang.Get("定时切换方案时通知"), Location = new Point(Spacing.LG + 48, sy + 1), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(notifyScheduleLbl, 's');

            sy += 28;

            _notifyStatusToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notifyStatusToggle.CheckedChanged += NotifyStatusToggle_CheckedChanged;
            var notifyStatusLbl = new Label { Text = Lang.Get("显示调节开关变化时通知"), Location = new Point(Spacing.LG + 48, sy + 1), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(notifyStatusLbl, 's');

            sy += 28;

            _notifyMonitorToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = true };
            _notifyMonitorToggle.CheckedChanged += NotifyMonitorToggle_CheckedChanged;
            var notifyMonitorLbl = new Label { Text = Lang.Get("显示器变更时通知"), Location = new Point(Spacing.LG + 48, sy + 1), AutoSize = true, Font = Typography.Body };
            SetLabelTheme(notifyMonitorLbl, 's');

            sy += 28;

            var diagLogSep = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            _diagnosticsLoggingToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _diagnosticsLoggingToggle.CheckedChanged += DiagnosticsLoggingToggle_CheckedChanged;

            var diagLogLbl = new Label
            {
                Text = Lang.Get("诊断日志"),
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(diagLogLbl, 'p');

            var diagLogHint = new Label
            {
                Text = Lang.Get("将运行记录写入本地日志文件，用于问题排查"),
                Location = new Point(Spacing.LG + 48, sy + 20),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(diagLogHint, 's');

            sy += 40;

            _memoryMaintenanceToggle = new ToggleSwitch { Location = new Point(Spacing.LG, sy), Checked = false };
            _memoryMaintenanceToggle.CheckedChanged += MemoryMaintenanceToggle_CheckedChanged;

            var memoryMaintenanceLbl = new Label
            {
                Text = Lang.Get("内存维护"),
                Location = new Point(Spacing.LG + 48, sy + 1),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(memoryMaintenanceLbl, 'p');

            var memoryMaintenanceHint = new Label
            {
                Text = Lang.Get("定期检查并修剪内存占用（实验性）"),
                Location = new Point(Spacing.LG + 48, sy + 20),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(memoryMaintenanceHint, 's');

            sy += 40;

            var resetConfigSep = CreateSeparator(sy, settingsContentWidth);
            sy += 10;

            var resetConfigHint = new Label
            {
                Text = Lang.Get("删除本地全部配置并恢复默认"),
                Location = new Point(Spacing.LG, sy + 5),
                AutoSize = true,
                Font = Typography.Caption
            };
            SetLabelTheme(resetConfigHint, 's');

            _resetConfigButton = new Button
            {
                Text = Lang.Get("删除配置"),
                Location = new Point(278, sy),
                Width = 104,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.Red,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter
            };
            _resetConfigButton.Click += ResetConfigButton_Click;

            sy += 34;

            var sepLine4 = CreateSeparator(sy, settingsContentWidth);
            sy += 16;

            var versionPanel = new Panel
            {
                Location = new Point(Spacing.LG, sy),
                Width = settingsContentWidth,
                Height = 34,
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

            var githubLink = new LinkLabel
            {
                Text = "GitHub",
                Location = new Point(settingsContentWidth - 58, 11),
                AutoSize = true,
                Font = Typography.Caption,
                BackColor = Color.Transparent,
                LinkColor = Colors.TextSecondary,
                ActiveLinkColor = Colors.Brand,
                VisitedLinkColor = Colors.TextSecondary
            };
            githubLink.LinkClicked += (s, e) => System.Diagnostics.Process.Start("https://github.com/SummerRay160/LumiShift");
            versionPanel.Controls.AddRange(new Control[] { versionLabel, githubLink });
            sy += 50;
            
            _settingsTab.Controls.AddRange(new Control[] {
                titleLabel,
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
                languageLbl, _languageComboBox,
                notificationSep, _notificationsEnabledToggle, notificationLbl,
                _notifyStartupToggle, notifyStartupLbl,
                _notifyScheduleToggle, notifyScheduleLbl,
                _notifyStatusToggle, notifyStatusLbl,
                _notifyMonitorToggle, notifyMonitorLbl,
                diagLogSep, _diagnosticsLoggingToggle, diagLogLbl, diagLogHint,
                _memoryMaintenanceToggle, memoryMaintenanceLbl, memoryMaintenanceHint,
                resetConfigSep, resetConfigHint, _resetConfigButton,
                sepLine4,
                versionPanel
            });
        }

        // ======================================================================
        //  Eye Protection Tab
        // ======================================================================
        private void BuildEyeProtectionTab()
        {
            _eyeProtectionTab = new TabPage(Lang.Get("护眼"))
            {
                BackColor = Colors.Background
            };

            int ey = 14;

            var titleLabel = CreateTitleLabel(Lang.Get("护眼模式"), ey);
            ey += 24;

            ey += 6;

            _eyeProtectionToggle = new ToggleSwitch { Location = new Point(Spacing.LG, ey), Checked = false };
            _eyeProtectionToggle.CheckedChanged += EyeProtectionToggle_CheckedChanged;

            var eyeLabel = new Label
            {
                Text = Lang.Get("启用系统护眼色"),
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
                Text = Lang.Get("护眼颜色"),
                Location = new Point(Spacing.LG, ey + 2),
                AutoSize = true,
                Font = Typography.BodyBold
            };
            SetLabelTheme(presetHint, 'p');

            ey += 28;

            var presetColors = new (string text, int r, int g, int b)[]
            {
                (Lang.Get("绿豆沙色"), 204, 232, 207),
                (Lang.Get("纸页黄"), 255, 255, 224),
                (Lang.Get("天空蓝"), 199, 216, 237)
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
                Text = Lang.Get("自定义颜色"),
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
                Text = Lang.Get("恢复默认"),
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
                Font = Typography.Caption,
                AutoEllipsis = true
            };
            SetLabelTheme(_eyeProtectionStatusLabel, 's');

            _eyeProtectionTab.Controls.AddRange(new Control[] {
                titleLabel,
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
