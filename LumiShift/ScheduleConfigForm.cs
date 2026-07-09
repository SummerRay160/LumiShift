using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    public class ScheduleConfigForm : Form
    {
        private const int MaxSegments = 10;

        private readonly List<MonitorInfo> _monitors;
        private readonly bool _hasMultipleMonitors;
        private List<ScheduleSegment> _segments;
        private List<GammaPreset> _customPresets;
        private FlowLayoutPanel _segmentPanel;
        private Label _summaryLabel;
        private Panel _timelinePanel;
        private Button _addButton;
        private Button _okButton;
        private Button _cancelButton;
        private bool _isUpdatingToggle;
        private Timer _addDebounceTimer;
        private Bitmap _formBackground;
        private readonly List<ToolTip> _activeToolTips = new List<ToolTip>();
        private bool _cleanedUp;

        public List<ScheduleSegment> ResultSegments { get; private set; }

        public ScheduleConfigForm(List<ScheduleSegment> segments, List<GammaPreset> customPresets, List<MonitorInfo> monitors)
        {
            _segments = segments.Select(s => new ScheduleSegment
            {
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                PresetName = s.PresetName,
                SyncMode = s.SyncMode,
                MonitorPresets = s.MonitorPresets != null
                    ? new Dictionary<string, string>(s.MonitorPresets)
                    : null
            }).ToList();
            _customPresets = customPresets;
            _monitors = monitors ?? new List<MonitorInfo>();
            _hasMultipleMonitors = _monitors.Count > 1;

            Text = "定时调度配置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 560);
            BackColor = Colors.Background;
            DoubleBuffered = true;

            BuildUI();
            ApplyBackgroundImage();
            EnablePanelDoubleBuffered();
            RebuildSegmentPanel();

            FormClosed += OnFormClosed;
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (_cleanedUp) return;
            _cleanedUp = true;

            _addDebounceTimer?.Stop();
            _addDebounceTimer?.Dispose();
            _addDebounceTimer = null;

            foreach (var tip in _activeToolTips)
            {
                tip.RemoveAll();
                tip.Dispose();
            }
            _activeToolTips.Clear();

            if (_segmentPanel != null)
            {
                foreach (Control c in _segmentPanel.Controls)
                {
                    if (c is Panel row)
                    {
                        foreach (Control child in row.Controls)
                            child.Dispose();
                        row.Controls.Clear();
                    }
                    c.Dispose();
                }
                _segmentPanel.Controls.Clear();
            }

            foreach (Control c in Controls)
            {
                if (c != _segmentPanel)
                    c.Dispose();
            }
            Controls.Clear();

            _segmentPanel?.Dispose();
            _segmentPanel = null;

            _formBackground?.Dispose();
            _formBackground = null;

            _customPresets = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                CleanupResources();
            base.Dispose(disposing);
        }

        private void ApplyBackgroundImage()
        {
            _formBackground?.Dispose();
            _formBackground = null;
            BackgroundImage = null;

            if (!Form1.StaticUseBackgroundImage || Form1.StaticBackgroundImage == null)
                return;

            _formBackground = Form1.CreateBackgroundBitmap(ClientSize);
            if (_formBackground != null)
            {
                BackgroundImage = _formBackground;
                BackgroundImageLayout = ImageLayout.Center;
            }
        }

        private void EnablePanelDoubleBuffered()
        {
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(_segmentPanel, true);
        }

        private void BuildUI()
        {
            int y = 14;

            var titleLabel = new Label
            {
                Text = "定时调度",
                Location = new Point(Spacing.LG, y),
                AutoSize = true,
                Font = Typography.H1,
                ForeColor = Colors.TextPrimary,
                BackColor = Color.Transparent
            };
            y += 24;

            var hintLabel = new Label
            {
                Text = _hasMultipleMonitors
                    ? "设置一天中什么时候切换到哪个显示方案；多屏方案会自动应用每台显示器的设置。"
                    : "设置一天中什么时候切换到哪个显示方案；时段不可重叠。",
                Location = new Point(Spacing.LG, y),
                Width = 572,
                Height = 18,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };
            y += 28;

            _summaryLabel = new Label
            {
                Text = "",
                Location = new Point(Spacing.LG, y),
                Width = 572,
                Height = 22,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };
            y += 28;

            _timelinePanel = new Panel
            {
                Location = new Point(Spacing.LG, y),
                Width = 572,
                Height = 78,
                BackColor = Color.Transparent
            };
            _timelinePanel.Paint += TimelinePanel_Paint;
            y += 86;

            var listTitle = new Label
            {
                Text = "时段列表",
                Location = new Point(Spacing.LG, y),
                AutoSize = true,
                Font = Typography.BodyBold,
                ForeColor = Colors.TextPrimary,
                BackColor = Color.Transparent
            };
            y += 24;

            _segmentPanel = new FlowLayoutPanel
            {
                Location = new Point(Spacing.LG, y),
                Width = 572,
                Height = 268,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            y += 276;

            _addButton = new Button
            {
                Text = "+ 添加时段",
                Location = new Point(Spacing.LG, y),
                Width = 124,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _addButton.Click += AddButton_Click;
            y += 40;

            _addDebounceTimer = new Timer { Interval = 200 };
            _addDebounceTimer.Tick += (s, e) =>
            {
                _addDebounceTimer.Stop();
                _addButton.Enabled = true;
            };

            var sepLine = new Label
            {
                Location = new Point(Spacing.LG, y),
                Width = 572,
                Height = 1,
                BackColor = Colors.BorderLight
            };
            y += 10;

            _okButton = new Button
            {
                Text = "确定",
                Location = new Point(432, y),
                Width = 90,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Brand,
                ForeColor = Color.White,
                Font = Typography.BodyBold,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = Colors.BrandHover },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _okButton.Click += OkButton_Click;

            _cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(530, y),
                Width = 70,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] { titleLabel, hintLabel, _summaryLabel, _timelinePanel, listTitle, _segmentPanel, _addButton, sepLine, _okButton, _cancelButton });
            UpdateSchedulePreview();
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            if (_segments.Count >= MaxSegments)
            {
                MessageBox.Show($"最多支持 {MaxSegments} 个时段。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _addButton.Enabled = false;
            _addDebounceTimer.Start();

            _segments.Add(new ScheduleSegment
            {
                StartTime = "12:00",
                EndTime = "14:00",
                PresetName = "标准"
            });

            _segmentPanel.SuspendLayout();
            var row = CreateSegmentRow(_segments.Count - 1);
            _segmentPanel.Controls.Add(row);
            _segmentPanel.ResumeLayout(true);
            _segmentPanel.ScrollControlIntoView(row);
            UpdateSchedulePreview();
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                var sParts = seg.StartTime.Split(':');
                var eParts = seg.EndTime.Split(':');
                if (sParts.Length < 2 || eParts.Length < 2) continue;
                if (!int.TryParse(sParts[0], out int sh) || !int.TryParse(sParts[1], out int sm)
                    || !int.TryParse(eParts[0], out int eh) || !int.TryParse(eParts[1], out int em))
                    continue;
                if (sh < 0 || sh > 23 || sm < 0 || sm > 59 || eh < 0 || eh > 23 || em < 0 || em > 59)
                    continue;
                var start = new TimeSpan(sh, sm, 0);
                var end = new TimeSpan(eh, em, 0);

                if (start == end)
                {
                    MessageBox.Show($"时段 {i + 1}（{seg.StartTime} → {seg.EndTime}）的起止时间相同，请修正。", "无效时段",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            for (int i = 0; i < _segments.Count; i++)
            {
                for (int j = i + 1; j < _segments.Count; j++)
                {
                    if (SegmentsOverlap(_segments[i], _segments[j]))
                    {
                        MessageBox.Show($"时段 {i + 1}（{_segments[i].StartTime} → {_segments[i].EndTime}）与时段 {j + 1}（{_segments[j].StartTime} → {_segments[j].EndTime}）存在重叠，请调整。", "时段重叠",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            ResultSegments = _segments;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool SegmentsOverlap(ScheduleSegment a, ScheduleSegment b)
        {
            var aStart = ParseTime(a.StartTime);
            var aEnd = ParseTime(a.EndTime);
            var bStart = ParseTime(b.StartTime);
            var bEnd = ParseTime(b.EndTime);

            if (!aStart.HasValue || !aEnd.HasValue || !bStart.HasValue || !bEnd.HasValue)
                return false;

            bool aOvernight = aStart.Value > aEnd.Value;
            bool bOvernight = bStart.Value > bEnd.Value;

            if (!aOvernight && !bOvernight)
            {
                return aStart.Value < bEnd.Value && bStart.Value < aEnd.Value;
            }

            if (aOvernight && bOvernight)
            {
                return true;
            }

            var oStart = aOvernight ? aStart.Value : bStart.Value;
            var oEnd = aOvernight ? aEnd.Value : bEnd.Value;
            var nStart = aOvernight ? bStart.Value : aStart.Value;
            var nEnd = aOvernight ? bEnd.Value : aEnd.Value;

            return nStart < oEnd || nEnd > oStart;
        }

        private static TimeSpan? ParseTime(string time)
        {
            var parts = time.Split(':');
            if (parts.Length < 2) return null;
            if (int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
                return new TimeSpan(h, m, 0);
            return null;
        }

        private static bool IsOvernight(ScheduleSegment segment)
        {
            var start = ParseTime(segment.StartTime);
            var end = ParseTime(segment.EndTime);
            return start.HasValue && end.HasValue && start.Value > end.Value;
        }

        private bool HasOverlap(int index)
        {
            if (index < 0 || index >= _segments.Count) return false;
            for (int i = 0; i < _segments.Count; i++)
            {
                if (i == index) continue;
                if (SegmentsOverlap(_segments[index], _segments[i]))
                    return true;
            }
            return false;
        }

        private void UpdateSchedulePreview()
        {
            if (_summaryLabel != null)
            {
                int independentCount = _segments.Count(s => s.SyncMode == false);
                string multiText = _hasMultipleMonitors
                    ? $"独立多屏 {independentCount} 个"
                    : "单显示器模式";
                _summaryLabel.Text = $"已配置 {_segments.Count}/{MaxSegments} 个时段  ·  {multiText}  ·  后台每 30 秒检查一次，关闭窗口后每 2 分钟检查一次";
            }

            _timelinePanel?.Invalidate();
        }

        private void TimelinePanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 8, _timelinePanel.Width - 1, 46);
            var barBounds = new Rectangle(8, 18, _timelinePanel.Width - 17, 24);
            using (var bg = new SolidBrush(Colors.Surface))
                g.FillRectangle(bg, bounds);
            using (var pen = new Pen(Colors.BorderLight))
                g.DrawRectangle(pen, bounds);

            if (_segments.Count == 0)
            {
                using (var brush = new SolidBrush(Colors.TextSecondary))
                    g.DrawString("暂无时段，点击下方“添加时段”开始配置。", Typography.Caption, brush, new PointF(10, 17));
                return;
            }

            using (var bg = new SolidBrush(Color.FromArgb(245, Colors.Background)))
                g.FillRectangle(bg, barBounds);
            using (var pen = new Pen(Colors.BorderLight))
                g.DrawRectangle(pen, barBounds);

            int left = barBounds.Left;
            int width = barBounds.Width;
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                var start = ParseTime(segment.StartTime);
                var end = ParseTime(segment.EndTime);
                if (!start.HasValue || !end.HasValue || start.Value == end.Value) continue;

                if (start.Value < end.Value)
                    DrawTimelineSegment(g, segment, i, start.Value, end.Value, left, width, barBounds);
                else
                {
                    DrawTimelineSegment(g, segment, i, start.Value, TimeSpan.FromDays(1), left, width, barBounds);
                    DrawTimelineSegment(g, segment, i, TimeSpan.Zero, end.Value, left, width, barBounds);
                }
            }

            DrawTimelineTicks(g, barBounds);

            using (var brush = new SolidBrush(Colors.TextSecondary))
            {
                g.DrawString("00:00", Typography.Caption, brush, new PointF(0, 62));
                g.DrawString("12:00", Typography.Caption, brush, new PointF((_timelinePanel.Width - 34) / 2f, 62));
                g.DrawString("24:00", Typography.Caption, brush, new PointF(_timelinePanel.Width - 38, 62));
            }
        }

        private void DrawTimelineSegment(Graphics g, ScheduleSegment segment, int index, TimeSpan start, TimeSpan end, int left, int timelineWidth, Rectangle barBounds)
        {
            float startRatio = (float)start.TotalMinutes / 1440f;
            float endRatio = (float)end.TotalMinutes / 1440f;
            int x = left + (int)Math.Round(timelineWidth * startRatio);
            int right = left + (int)Math.Round(timelineWidth * endRatio);
            int segmentWidth = Math.Max(3, right - x);
            Color color = HasOverlap(index) ? Colors.Red : (segment.SyncMode == false || IsMultiDisplayPreset(segment.PresetName) ? Colors.Brand : Colors.Green);

            var segmentRect = new Rectangle(x, barBounds.Y, segmentWidth, barBounds.Height);
            using (var brush = new SolidBrush(color))
                g.FillRectangle(brush, segmentRect);

            using (var divider = new Pen(Color.FromArgb(230, Color.White)))
                g.DrawLine(divider, x, barBounds.Y, x, barBounds.Bottom - 1);

            DrawTimelineSegmentLabel(g, segment, segmentRect);
        }

        private void DrawTimelineTicks(Graphics g, Rectangle barBounds)
        {
            using (var pen = new Pen(Color.FromArgb(90, Colors.TextSecondary)))
            {
                for (int hour = 6; hour <= 18; hour += 6)
                {
                    int x = barBounds.Left + (int)Math.Round(barBounds.Width * hour / 24.0);
                    g.DrawLine(pen, x, barBounds.Top, x, barBounds.Bottom);
                }
            }
        }

        private void DrawTimelineSegmentLabel(Graphics g, ScheduleSegment segment, Rectangle segmentRect)
        {
            if (segmentRect.Width < 28) return;

            string mode = segment.SyncMode == false ? "逐台" : (IsMultiDisplayPreset(segment.PresetName) ? "多屏" : "统一");
            string label = $"{segment.PresetName} · {mode}";
            int maxChars = Math.Max(2, (segmentRect.Width - 8) / 7);
            if (label.Length > maxChars)
                label = maxChars <= 3 ? label.Substring(0, Math.Min(label.Length, maxChars)) : label.Substring(0, maxChars - 1) + "…";

            var textSize = g.MeasureString(label, Typography.Caption);
            float textX = segmentRect.X + Math.Max(4, (segmentRect.Width - textSize.Width) / 2f);
            float textY = segmentRect.Y + (segmentRect.Height - textSize.Height) / 2f + 1;
            using (var brush = new SolidBrush(Color.White))
                g.DrawString(label, Typography.Caption, brush, new PointF(textX, textY));
        }

        private void RebuildSegmentPanel()
        {
            foreach (var tip in _activeToolTips)
                tip.RemoveAll();
            foreach (var tip in _activeToolTips)
                tip.Dispose();
            _activeToolTips.Clear();

            _segmentPanel.SuspendLayout();
            foreach (Control c in _segmentPanel.Controls)
            {
                DisposeControlTree(c);
            }
            _segmentPanel.Controls.Clear();

            for (int i = 0; i < _segments.Count; i++)
            {
                _segmentPanel.Controls.Add(CreateSegmentRow(i));
            }

            _segmentPanel.ResumeLayout(true);
            UpdateSchedulePreview();
        }

        private void ReplaceSegmentRow(int index)
        {
            _segmentPanel.SuspendLayout();
            var oldRow = _segmentPanel.Controls[index];
            _segmentPanel.Controls.RemoveAt(index);
            DisposeControlTree(oldRow);
            _segmentPanel.Controls.Add(CreateSegmentRow(index));
            _segmentPanel.Controls.SetChildIndex(_segmentPanel.Controls[_segmentPanel.Controls.Count - 1], index);
            _segmentPanel.ResumeLayout(true);
            UpdateSchedulePreview();
        }

        private Panel CreateSegmentRow(int i)
        {
            var segment = _segments[i];
            int idx = i;
            bool isMultiDisplayPreset = IsMultiDisplayPreset(segment.PresetName);
            bool isIndependent = segment.SyncMode == false;
            bool hasMonitorPresets = _hasMultipleMonitors && isIndependent && segment.MonitorPresets != null && segment.MonitorPresets.Count > 0;
            bool hasOverlap = HasOverlap(i);

            int containerHeight = 86;
            if (hasMonitorPresets)
                containerHeight += 6 + _monitors.Count * 30;
            if (hasOverlap)
                containerHeight += 22;

            var container = new Panel
            {
                Width = 548,
                Height = containerHeight,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 10, 12, 10)
            };

            var accent = new Label
            {
                Location = new Point(0, 10),
                Width = 3,
                Height = containerHeight - 20,
                BackColor = hasOverlap ? Colors.Red : (isIndependent || isMultiDisplayPreset ? Colors.Brand : Colors.Green)
            };

            var timeTitle = new Label
            {
                Text = $"时段 {i + 1}    {segment.StartTime} → {segment.EndTime}" + (IsOvernight(segment) ? "  跨午夜" : ""),
                Location = new Point(12, 8),
                Width = 300,
                Height = 20,
                Font = Typography.BodyBold,
                ForeColor = hasOverlap ? Colors.Red : Colors.TextPrimary,
                BackColor = Color.Transparent
            };

            var modeSummary = new Label
            {
                Text = GetModeSummaryText(segment),
                Location = new Point(354, 10),
                AutoSize = true,
                Font = Typography.Caption,
                ForeColor = isIndependent || isMultiDisplayPreset ? Colors.Brand : Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            var startPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Location = new Point(12, 40),
                Width = 88,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            try
            {
                var parts = segment.StartTime.Split(':');
                startPicker.Value = DateTime.Today.AddHours(int.Parse(parts[0])).AddMinutes(parts.Length > 1 ? int.Parse(parts[1]) : 0);
            }
            catch { startPicker.Value = DateTime.Today.AddHours(6); }

            var arrowLbl = new Label
            {
                Text = "→",
                Location = new Point(104, 42),
                AutoSize = true,
                Font = Typography.Body,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            var endPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Location = new Point(124, 40),
                Width = 88,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            try
            {
                var parts = segment.EndTime.Split(':');
                endPicker.Value = DateTime.Today.AddHours(int.Parse(parts[0])).AddMinutes(parts.Length > 1 ? int.Parse(parts[1]) : 0);
            }
            catch { endPicker.Value = DateTime.Today.AddHours(18); }

            var presetCombo = new ComboBox
            {
                Location = new Point(226, 40),
                Width = 138,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            FillPresetCombo(presetCombo, segment.PresetName);

            container.Controls.AddRange(new Control[] { accent, timeTitle, modeSummary, startPicker, arrowLbl, endPicker, presetCombo });

            if (hasOverlap)
            {
                var overlapLabel = new Label
                {
                    Text = "此时段与其他时段重叠，请调整时间。",
                    Location = new Point(12, 66),
                    Width = 420,
                    Height = 18,
                    Font = Typography.Caption,
                    ForeColor = Colors.Red,
                    BackColor = Color.Transparent
                };
                container.Controls.Add(overlapLabel);
            }

            if (_hasMultipleMonitors)
            {
                var monitorToggle = new ToggleSwitch
                {
                    Location = new Point(386, 42),
                    Checked = isIndependent,
                    Width = 44
                };

                var monitorLabel = new Label
                {
                    Text = isIndependent ? "逐台" : "方案",
                    Location = new Point(434, 45),
                    AutoSize = true,
                    Font = Typography.Caption,
                    ForeColor = isIndependent ? Colors.Brand : Colors.TextSecondary,
                    BackColor = Color.Transparent
                };

                var modeTip = new ToolTip();
                _activeToolTips.Add(modeTip);
                modeTip.SetToolTip(monitorToggle, isIndependent ? "临时逐台配置：仅此时段为每台显示器选择方案" : "方案模式：此时段切换到一个显示方案");
                modeTip.SetToolTip(monitorLabel, isIndependent ? "临时逐台配置：仅此时段为每台显示器选择方案" : "方案模式：此时段切换到一个显示方案");

                var deleteBtn = new Button
                {
                    Text = "×",
                    Location = new Point(510, 8),
                    Width = 24,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Colors.TextSecondary,
                    Font = Typography.Caption,
                    FlatAppearance = { BorderSize = 0 },
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand
                };
                deleteBtn.MouseEnter += (s, ev) => { deleteBtn.BackColor = Colors.Red; deleteBtn.ForeColor = Color.White; };
                deleteBtn.MouseLeave += (s, ev) => { deleteBtn.BackColor = Color.Transparent; deleteBtn.ForeColor = Colors.TextSecondary; };

                monitorToggle.CheckedChanged += (s, ev) =>
                {
                    if (_isUpdatingToggle) return;
                    if (monitorToggle.Checked)
                    {
                        segment.SyncMode = false;
                        if (segment.MonitorPresets == null)
                            segment.MonitorPresets = new Dictionary<string, string>();
                        if (segment.MonitorPresets.Count == 0)
                        {
                            foreach (var m in _monitors)
                                segment.MonitorPresets[m.DeviceId] = segment.PresetName;
                        }
                    }
                    else
                    {
                        if (segment.MonitorPresets != null && segment.MonitorPresets.Count > 0)
                        {
                            bool hasCustom = false;
                            foreach (var m in _monitors)
                            {
                                if (segment.MonitorPresets.TryGetValue(m.DeviceId, out var mp) && mp != segment.PresetName)
                                {
                                    hasCustom = true;
                                    break;
                                }
                            }
                            if (hasCustom)
                            {
                                if (MessageBox.Show("切换到方案模式将清除此时段逐台配置，之后可选择统一方案或多屏方案。是否继续？", "确认",
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                                {
                                    _isUpdatingToggle = true;
                                    monitorToggle.Checked = true;
                                    _isUpdatingToggle = false;
                                    return;
                                }
                            }
                            segment.MonitorPresets.Clear();
                            segment.MonitorPresets = null;
                        }
                        segment.SyncMode = true;
                    }
                    ReplaceSegmentRow(idx);
                };

                deleteBtn.Click += (s, ev) =>
                {
                    if (MessageBox.Show($"确定删除此时段（{_segments[idx].StartTime} - {_segments[idx].EndTime}）？", "确认删除",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _segments.RemoveAt(idx);
                        _segmentPanel.SuspendLayout();
                        var oldRow = _segmentPanel.Controls[idx];
                        _segmentPanel.Controls.RemoveAt(idx);
                        DisposeControlTree(oldRow);
                        for (int j = idx; j < _segments.Count; j++)
                        {
                            var existingRow = _segmentPanel.Controls[j];
                            _segmentPanel.Controls.RemoveAt(j);
                            DisposeControlTree(existingRow);
                            _segmentPanel.Controls.Add(CreateSegmentRow(j));
                            _segmentPanel.Controls.SetChildIndex(_segmentPanel.Controls[_segmentPanel.Controls.Count - 1], j);
                        }
                        _segmentPanel.ResumeLayout(true);
                        UpdateSchedulePreview();
                    }
                };

                container.Controls.AddRange(new Control[] { monitorToggle, monitorLabel, deleteBtn });
            }
            else
            {
                var allScreensHint = new Label
                {
                    Text = "所有屏幕",
                    Location = new Point(386, 45),
                    AutoSize = true,
                    Font = Typography.Caption,
                    ForeColor = Colors.TextSecondary,
                    BackColor = Color.Transparent
                };

                var deleteBtn = new Button
                {
                    Text = "×",
                    Location = new Point(510, 8),
                    Width = 24,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Colors.TextSecondary,
                    Font = Typography.Caption,
                    FlatAppearance = { BorderSize = 0 },
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand
                };
                deleteBtn.MouseEnter += (s, ev) => { deleteBtn.BackColor = Colors.Red; deleteBtn.ForeColor = Color.White; };
                deleteBtn.MouseLeave += (s, ev) => { deleteBtn.BackColor = Color.Transparent; deleteBtn.ForeColor = Colors.TextSecondary; };

                deleteBtn.Click += (s, ev) =>
                {
                    if (MessageBox.Show($"确定删除此时段（{_segments[idx].StartTime} - {_segments[idx].EndTime}）？", "确认删除",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _segments.RemoveAt(idx);
                        _segmentPanel.SuspendLayout();
                        var oldRow = _segmentPanel.Controls[idx];
                        _segmentPanel.Controls.RemoveAt(idx);
                        DisposeControlTree(oldRow);
                        for (int j = idx; j < _segments.Count; j++)
                        {
                            var existingRow = _segmentPanel.Controls[j];
                            _segmentPanel.Controls.RemoveAt(j);
                            DisposeControlTree(existingRow);
                            _segmentPanel.Controls.Add(CreateSegmentRow(j));
                            _segmentPanel.Controls.SetChildIndex(_segmentPanel.Controls[_segmentPanel.Controls.Count - 1], j);
                        }
                        _segmentPanel.ResumeLayout(true);
                        UpdateSchedulePreview();
                    }
                };

                container.Controls.AddRange(new Control[] { allScreensHint, deleteBtn });
            }

            startPicker.ValueChanged += (s, ev) =>
            {
                var t = startPicker.Value;
                _segments[idx].StartTime = $"{t.Hour:D2}:{t.Minute:D2}";
                ReplaceSegmentRow(idx);
            };

            endPicker.ValueChanged += (s, ev) =>
            {
                var t = endPicker.Value;
                _segments[idx].EndTime = $"{t.Hour:D2}:{t.Minute:D2}";
                ReplaceSegmentRow(idx);
            };

            presetCombo.SelectedIndexChanged += (s, ev) =>
            {
                _segments[idx].PresetName = GetPresetNameFromDisplay(presetCombo.SelectedItem?.ToString() ?? "标准");
                if (IsMultiDisplayPreset(_segments[idx].PresetName))
                {
                    _segments[idx].SyncMode = true;
                    _segments[idx].MonitorPresets = null;
                    ReplaceSegmentRow(idx);
                    return;
                }
                if (_segments[idx].MonitorPresets != null)
                {
                    foreach (var m in _monitors)
                    {
                        if (!_segments[idx].MonitorPresets.ContainsKey(m.DeviceId))
                            _segments[idx].MonitorPresets[m.DeviceId] = _segments[idx].PresetName;
                    }
                }
                UpdateSchedulePreview();
            };

            if (hasMonitorPresets)
            {
                int my = hasOverlap ? 100 : 78;
                foreach (var mon in _monitors)
                {
                    var monId = mon.DeviceId;
                    var monName = mon.DisplayName ?? mon.DeviceId;

                    var monIndent = new Label
                    {
                        Text = "  └",
                        Location = new Point(20, my + 2),
                        AutoSize = true,
                        Font = Typography.Caption,
                        ForeColor = Colors.TextDisabled,
                        BackColor = Color.Transparent
                    };

                    var monLabel = new Label
                    {
                        Text = monName,
                        Location = new Point(48, my + 2),
                        Width = 250,
                        Height = 18,
                        Font = Typography.Caption,
                        ForeColor = Colors.TextSecondary,
                        BackColor = Color.Transparent
                    };

                    var monCombo = new ComboBox
                    {
                        Location = new Point(322, my),
                        Width = 138,
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Colors.Surface,
                        ForeColor = Colors.TextPrimary,
                        Font = Typography.Caption,
                        Tag = monId
                    };

                    string monPreset = segment.MonitorPresets != null && segment.MonitorPresets.TryGetValue(monId, out var mp) ? mp : segment.PresetName;
                    FillPresetCombo(monCombo, monPreset);

                    monCombo.SelectedIndexChanged += (s, ev) =>
                    {
                        if (_segments[idx].MonitorPresets == null)
                            _segments[idx].MonitorPresets = new Dictionary<string, string>();
                        _segments[idx].MonitorPresets[monId] = GetPresetNameFromDisplay(monCombo.SelectedItem?.ToString() ?? "标准");
                        UpdateSchedulePreview();
                    };

                    container.Controls.Add(monIndent);
                    container.Controls.Add(monLabel);
                    container.Controls.Add(monCombo);
                    my += 28;
                }
            }

            return container;
        }

        private void FillPresetCombo(ComboBox cb, string selected)
        {
            cb.Items.Clear();
            foreach (var p in PresetDefinitions.GetNames())
                cb.Items.Add($"{p} · 统一方案");
            foreach (var cp in _customPresets)
                cb.Items.Add(GetPresetDisplayName(cp.Name));
            if (cb.Items.Contains(selected))
                cb.SelectedItem = selected;
            else if (cb.Items.Contains(GetPresetDisplayName(selected)))
                cb.SelectedItem = GetPresetDisplayName(selected);
            else
                cb.SelectedIndex = 0;
        }

        private string GetPresetDisplayName(string presetName)
        {
            return IsMultiDisplayPreset(presetName) ? $"{presetName} · 多屏方案" : $"{presetName} · 统一方案";
        }

        private string GetPresetNameFromDisplay(string displayName)
        {
            return DisplaySchemeService.StripDisplayName(displayName);
        }

        private bool IsMultiDisplayPreset(string presetName)
        {
            var name = GetPresetNameFromDisplay(presetName);
            var preset = _customPresets?.FirstOrDefault(p => p.Name == name);
            return preset?.PerDisplaySnapshot != null && preset.PerDisplaySnapshot.Count > 0;
        }

        private string GetModeSummaryText(ScheduleSegment segment)
        {
            if (segment.SyncMode == false)
                return "临时逐台配置";
            return IsMultiDisplayPreset(segment.PresetName) ? "使用多屏方案" : "使用统一方案";
        }

        private static void DisposeControlTree(Control control)
        {
            if (control is Panel panel)
            {
                foreach (Control child in panel.Controls)
                    DisposeControlTree(child);
                panel.Controls.Clear();
            }
            control.Dispose();
        }
    }
}
