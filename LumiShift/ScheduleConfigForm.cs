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

            Text = Lang.Get("定时调度配置");
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
                Text = Lang.Get("定时调度"),
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
                    ? Lang.Get("设置一天中什么时候切换到哪个显示方案；多屏方案会自动应用每台显示器的设置。")
                    : Lang.Get("设置一天中什么时候切换到哪个显示方案；时段不可重叠。"),
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
                Text = Lang.Get("时段列表"),
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
                Text = Lang.Get("+ 添加时段"),
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
                Text = Lang.Get("确定"),
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
                Text = Lang.Get("取消"),
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
                MessageBox.Show(Lang.F("最多支持 {0} 个时段。", MaxSegments), Lang.Get("提示"),
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
            // 1) 严格校验每个时段的格式、起止时间与方案
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                if (!TryParseTime(seg.StartTime, out var start) || !TryParseTime(seg.EndTime, out var end))
                {
                    MessageBox.Show(Lang.F("时段 {0} 的时间格式无效（{1} → {2}），请使用 HH:mm 格式。", i + 1, seg.StartTime, seg.EndTime),
                        Lang.Get("无效时段"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (start == end)
                {
                    MessageBox.Show(Lang.F("时段 {0}（{1} → {2}）的起止时间相同，请修正。", i + 1, seg.StartTime, seg.EndTime),
                        Lang.Get("无效时段"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(seg.PresetName))
                {
                    MessageBox.Show(Lang.F("时段 {0} 未选择显示方案，请选择后再保存。", i + 1),
                        Lang.Get("无效时段"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // 2) 时段重叠检测
            for (int i = 0; i < _segments.Count; i++)
            {
                for (int j = i + 1; j < _segments.Count; j++)
                {
                    if (SegmentsOverlap(_segments[i], _segments[j]))
                    {
                        MessageBox.Show(Lang.F("时段 {0}（{1} → {2}）与时段 {3}（{4} → {5}）存在重叠，请调整。", i + 1, _segments[i].StartTime, _segments[i].EndTime, j + 1, _segments[j].StartTime, _segments[j].EndTime), Lang.Get("时段重叠"),
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            ResultSegments = _segments;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool TryParseTime(string text, out TimeSpan result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split(':');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;
            result = new TimeSpan(h, m, 0);
            return true;
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
                    ? Lang.F("独立多屏 {0} 个", independentCount)
                    : Lang.Get("单显示器模式");
                _summaryLabel.Text = Lang.F("已配置 {0}/{1} 个时段  {2}", _segments.Count, MaxSegments, multiText);
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
                    g.DrawString(Lang.Get("暂无时段，点击下方“添加时段”开始配置。"), Typography.Caption, brush, new PointF(10, 17));
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

            string mode = segment.SyncMode == false ? Lang.Get("逐台") : (IsMultiDisplayPreset(segment.PresetName) ? Lang.Get("多屏") : Lang.Get("统一"));
            string label = $"{Lang.Get(GetPresetNameFromDisplay(segment.PresetName))} · {mode}";
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

        /// <summary>
        /// 删除指定索引的时段，并重建后续行以刷新 idx 闭包。
        /// </summary>
        private void DeleteSegmentAt(int idx)
        {
            if (idx < 0 || idx >= _segments.Count) return;
            if (MessageBox.Show(Lang.F("确定删除此时段（{0} - {1}）？", _segments[idx].StartTime, _segments[idx].EndTime), Lang.Get("确认删除"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _segments.RemoveAt(idx);
            _segmentPanel.SuspendLayout();
            var oldRow = _segmentPanel.Controls[idx];
            _segmentPanel.Controls.RemoveAt(idx);
            DisposeControlTree(oldRow);
            // 重建后续行：每行的 idx 闭包需更新为新索引
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

        private Panel CreateSegmentRow(int i)
        {
            var segment = _segments[i];
            int idx = i;
            bool isMultiDisplayPreset = IsMultiDisplayPreset(segment.PresetName);
            bool isIndependent = segment.SyncMode == false;
            bool hasMonitorPresets = _hasMultipleMonitors && isIndependent && segment.MonitorPresets != null && segment.MonitorPresets.Count > 0;
            bool hasOverlap = HasOverlap(i);

            // 始终预留 22px overlap 提示空间，避免 overlap 状态变化时行高变化触发重建
            int containerHeight = 108;
            if (hasMonitorPresets)
                containerHeight += 6 + _monitors.Count * 30;

            var container = new FocusablePanel
            {
                Width = 548,
                Height = containerHeight,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 10, 12, 10)
            };
            // 点击容器空白处时让 picker/combo 失焦
            container.Click += (s, ev) => container.Focus();

            var accent = new Label
            {
                Location = new Point(0, 10),
                Width = 3,
                Height = containerHeight - 20,
                BackColor = hasOverlap ? Colors.Red : (isIndependent || isMultiDisplayPreset ? Colors.Brand : Colors.Green)
            };

            var timeTitle = new Label
            {
                Text = Lang.F("时段 {0}    {1} → {2}", i + 1, segment.StartTime, segment.EndTime) + (IsOvernight(segment) ? Lang.Get("  跨午夜") : ""),
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

            var startPicker = new DateTimePickerEx
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

            var endPicker = new DateTimePickerEx
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

            var presetCombo = new BlurComboBox
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

            // overlapLabel 始终创建，通过 Visible 切换；避免 overlap 状态变化时重建行
            var overlapLabel = new Label
            {
                Text = Lang.Get("此时段与其他时段重叠，请调整时间。"),
                Location = new Point(12, 66),
                Width = 420,
                Height = 18,
                Font = Typography.Caption,
                ForeColor = Colors.Red,
                BackColor = Color.Transparent,
                Visible = hasOverlap
            };
            container.Controls.Add(overlapLabel);

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
                    Text = isIndependent ? Lang.Get("逐台") : Lang.Get("方案"),
                    Location = new Point(434, 45),
                    AutoSize = true,
                    Font = Typography.Caption,
                    ForeColor = isIndependent ? Colors.Brand : Colors.TextSecondary,
                    BackColor = Color.Transparent
                };

                var modeTip = new ToolTip();
                _activeToolTips.Add(modeTip);
                modeTip.SetToolTip(monitorToggle, isIndependent ? Lang.Get("临时逐台配置：仅此时段为每台显示器选择方案") : Lang.Get("方案模式：此时段切换到一个显示方案"));
                modeTip.SetToolTip(monitorLabel, isIndependent ? Lang.Get("临时逐台配置：仅此时段为每台显示器选择方案") : Lang.Get("方案模式：此时段切换到一个显示方案"));

                var deleteBtn = CreateDeleteButton();

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
                                if (MessageBox.Show(Lang.Get("切换到方案模式将清除此时段逐台配置，之后可选择统一方案或多屏方案。是否继续？"), Lang.Get("确认"),
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

                deleteBtn.Click += (s, ev) => DeleteSegmentAt(idx);

                container.Controls.AddRange(new Control[] { monitorToggle, monitorLabel, deleteBtn });
            }
            else
            {
                var allScreensHint = new Label
                {
                    Text = Lang.Get("所有屏幕"),
                    Location = new Point(386, 45),
                    AutoSize = true,
                    Font = Typography.Caption,
                    ForeColor = Colors.TextSecondary,
                    BackColor = Color.Transparent
                };

                var deleteBtn = CreateDeleteButton();

                deleteBtn.Click += (s, ev) => DeleteSegmentAt(idx);

                container.Controls.AddRange(new Control[] { allScreensHint, deleteBtn });
            }

            startPicker.ValueChanged += (s, ev) =>
            {
                var t = startPicker.Value;
                _segments[idx].StartTime = $"{t.Hour:D2}:{t.Minute:D2}";
                UpdateRowLightweight(idx, timeTitle, modeSummary, accent, overlapLabel);
                UpdateSchedulePreview();
            };

            endPicker.ValueChanged += (s, ev) =>
            {
                var t = endPicker.Value;
                _segments[idx].EndTime = $"{t.Hour:D2}:{t.Minute:D2}";
                UpdateRowLightweight(idx, timeTitle, modeSummary, accent, overlapLabel);
                UpdateSchedulePreview();
            };

            presetCombo.SelectedIndexChanged += (s, ev) =>
            {
                _segments[idx].PresetName = GetPresetNameFromDisplay(presetCombo.SelectedItem?.ToString() ?? "标准");
                if (IsMultiDisplayPreset(_segments[idx].PresetName))
                {
                    _segments[idx].SyncMode = true;
                    _segments[idx].MonitorPresets = null;
                    ReplaceSegmentRow(idx);   // 切到多屏方案：结构变化，必须重建
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
                UpdateRowLightweight(idx, timeTitle, modeSummary, accent, overlapLabel);
                UpdateSchedulePreview();
            };

            // ComboBox 按 Enter 键时让父容器获得焦点（与 DateTimePickerEx 行为一致）
            WireComboEnterFocus(presetCombo, container);

            if (hasMonitorPresets)
            {
                int my = 100;  // 固定位置，行高已始终预留 overlap 空间
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

                    var monCombo = new BlurComboBox
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

                    // ComboBox 按 Enter 键时让父容器获得焦点
                    WireComboEnterFocus(monCombo, container);

                    container.Controls.Add(monIndent);
                    container.Controls.Add(monLabel);
                    container.Controls.Add(monCombo);
                    my += 28;
                }
            }

            return container;
        }

        /// <summary>
        /// 行内轻量更新：仅刷新文本/颜色/可见性，不重建控件，保留焦点。
        /// 用于时间或方案变化时刷新行显示，避免 ReplaceSegmentRow 导致焦点丢失。
        /// </summary>
        private Button CreateDeleteButton()
        {
            var btn = new Button
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
            btn.MouseEnter += (s, ev) => { btn.BackColor = Colors.Red; btn.ForeColor = Color.White; };
            btn.MouseLeave += (s, ev) => { btn.BackColor = Color.Transparent; btn.ForeColor = Colors.TextSecondary; };
            return btn;
        }

        private static void WireComboEnterFocus(ComboBox combo, Control container)
        {
            combo.PreviewKeyDown += (s, ev) => { if (ev.KeyCode == Keys.Enter) ev.IsInputKey = true; };
            combo.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                {
                    container.Focus();
                    ev.SuppressKeyPress = true;
                }
            };
        }

        private void UpdateRowLightweight(int idx, Label timeTitle, Label modeSummary, Label accent, Label overlapLabel)
        {
            var segment = _segments[idx];
            bool isMultiDisplayPreset = IsMultiDisplayPreset(segment.PresetName);
            bool isIndependent = segment.SyncMode == false;
            bool hasOverlap = HasOverlap(idx);

            timeTitle.Text = Lang.F("时段 {0}    {1} → {2}", idx + 1, segment.StartTime, segment.EndTime) + (IsOvernight(segment) ? Lang.Get("  跨午夜") : "");
            timeTitle.ForeColor = hasOverlap ? Colors.Red : Colors.TextPrimary;
            modeSummary.Text = GetModeSummaryText(segment);
            modeSummary.ForeColor = isIndependent || isMultiDisplayPreset ? Colors.Brand : Colors.TextSecondary;
            accent.BackColor = hasOverlap ? Colors.Red : (isIndependent || isMultiDisplayPreset ? Colors.Brand : Colors.Green);

            if (overlapLabel != null)
                overlapLabel.Visible = hasOverlap;
        }

        private void FillPresetCombo(ComboBox cb, string selected)
        {
            cb.Items.Clear();
            foreach (var p in PresetDefinitions.GetNames())
                cb.Items.Add(GetPresetDisplayName(p));
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
            string key = GetPresetNameFromDisplay(presetName);
            return Lang.Get(key) + " · " + Lang.Get(IsMultiDisplayPreset(key) ? "多屏方案" : "统一方案");
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
                return Lang.Get("临时逐台配置");
            return IsMultiDisplayPreset(segment.PresetName) ? Lang.Get("使用多屏方案") : Lang.Get("使用统一方案");
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
