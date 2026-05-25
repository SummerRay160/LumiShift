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

namespace LumiShift
{
    public class ScheduleConfigForm : Form
    {
        private const int MaxSegments = 10;

        private readonly List<MonitorInfo> _monitors;
        private List<ScheduleSegment> _segments;
        private List<GammaPreset> _customPresets;
        private FlowLayoutPanel _segmentPanel;
        private Button _addButton;
        private Button _okButton;
        private Button _cancelButton;
        private bool _isUpdatingToggle;
        private Timer _addDebounceTimer;
        private Bitmap _formBackground;

        public List<ScheduleSegment> ResultSegments { get; private set; }

        public ScheduleConfigForm(List<ScheduleSegment> segments, List<GammaPreset> customPresets, List<MonitorInfo> monitors)
        {
            _segments = segments.Select(s => new ScheduleSegment
            {
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                PresetName = s.PresetName,
                MonitorPresets = s.MonitorPresets != null
                    ? new Dictionary<string, string>(s.MonitorPresets)
                    : null
            }).ToList();
            _customPresets = customPresets;
            _monitors = monitors ?? new List<MonitorInfo>();

            Text = "定时调度配置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(570, 480);
            BackColor = Colors.Background;
            DoubleBuffered = true;

            BuildUI();
            ApplyBackgroundImage();
            EnablePanelDoubleBuffered();
            RebuildSegmentPanel();

            ThemeManager.ThemeChanged += OnThemeChanged;
            FormClosed += OnFormClosed;
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _addDebounceTimer?.Dispose();
            _addDebounceTimer = null;

            foreach (Control c in _segmentPanel.Controls)
                c.Dispose();
            _segmentPanel.Controls.Clear();

            foreach (Control c in Controls)
            {
                if (c != _segmentPanel)
                    c.Dispose();
            }
            Controls.Clear();

            _formBackground?.Dispose();
            _formBackground = null;
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

        private void OnThemeChanged(object sender, EventArgs e)
        {
            BackColor = Colors.Background;
            ApplyBackgroundImage();
            Invalidate(true);
            RebuildSegmentPanel();
        }

        private void BuildUI()
        {
            int y = 12;

            var hintLabel = new Label
            {
                Text = "配置各时段的起止时间与对应预设，时段不可重叠",
                Location = new Point(Spacing.LG, y),
                AutoSize = true,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };
            y += 24;

            var headerRow = new Panel
            {
                Location = new Point(Spacing.LG, y),
                Width = 538,
                Height = 20,
                BackColor = Color.Transparent
            };

            var hStart = new Label { Text = "时段", Location = new Point(0, 0), AutoSize = true, Font = Typography.Caption, ForeColor = Colors.TextSecondary, BackColor = Color.Transparent };
            var hPreset = new Label { Text = "预设", Location = new Point(210, 0), AutoSize = true, Font = Typography.Caption, ForeColor = Colors.TextSecondary, BackColor = Color.Transparent };
            var hMonitor = new Label { Text = "显示器", Location = new Point(340, 0), AutoSize = true, Font = Typography.Caption, ForeColor = Colors.TextSecondary, BackColor = Color.Transparent };
            headerRow.Controls.AddRange(new Control[] { hStart, hPreset, hMonitor });
            y += 24;

            _segmentPanel = new FlowLayoutPanel
            {
                Location = new Point(Spacing.LG, y),
                Width = 538,
                Height = 330,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            y += 334;

            _addButton = new Button
            {
                Text = "+ 添加时段",
                Location = new Point(Spacing.LG, y),
                Width = 120,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 },
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            _addButton.Click += AddButton_Click;
            y += 36;

            _addDebounceTimer = new Timer { Interval = 200 };
            _addDebounceTimer.Tick += (s, e) =>
            {
                _addDebounceTimer.Stop();
                _addButton.Enabled = true;
            };

            var sepLine = new Label
            {
                Location = new Point(Spacing.LG, y),
                Width = 538,
                Height = 1,
                BackColor = Colors.Border
            };
            y += 10;

            _okButton = new Button
            {
                Text = "确定",
                Location = new Point(370, y),
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
                Location = new Point(468, y),
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

            Controls.AddRange(new Control[] { hintLabel, headerRow, _segmentPanel, _addButton, sepLine, _okButton, _cancelButton });
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
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                var sParts = seg.StartTime.Split(':');
                var eParts = seg.EndTime.Split(':');
                if (sParts.Length < 2 || eParts.Length < 2) continue;
                var start = new TimeSpan(int.Parse(sParts[0]), int.Parse(sParts[1]), 0);
                var end = new TimeSpan(int.Parse(eParts[0]), int.Parse(eParts[1]), 0);

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

        private void RebuildSegmentPanel()
        {
            _segmentPanel.SuspendLayout();
            foreach (Control c in _segmentPanel.Controls)
                c.Dispose();
            _segmentPanel.Controls.Clear();

            for (int i = 0; i < _segments.Count; i++)
            {
                _segmentPanel.Controls.Add(CreateSegmentRow(i));
            }

            _segmentPanel.ResumeLayout(true);
        }

        private void ReplaceSegmentRow(int index)
        {
            _segmentPanel.SuspendLayout();
            var oldRow = _segmentPanel.Controls[index];
            oldRow.Dispose();
            _segmentPanel.Controls.RemoveAt(index);
            _segmentPanel.Controls.Add(CreateSegmentRow(index));
            _segmentPanel.Controls.SetChildIndex(_segmentPanel.Controls[_segmentPanel.Controls.Count - 1], index);
            _segmentPanel.ResumeLayout(true);
        }

        private Panel CreateSegmentRow(int i)
        {
            var segment = _segments[i];
            int idx = i;
            bool hasMonitorPresets = segment.MonitorPresets != null && segment.MonitorPresets.Count > 0;

            int containerHeight = 36;
            if (hasMonitorPresets)
                containerHeight += 4 + _monitors.Count * 28;

            var container = new Panel
            {
                Width = 472,
                Height = containerHeight,
                BackColor = i % 2 == 0 ? Color.Transparent : Color.FromArgb(8, 255, 255, 255)
            };

            var startPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Location = new Point(0, 6),
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
                Location = new Point(92, 8),
                AutoSize = true,
                Font = Typography.Body,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            var endPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Location = new Point(112, 6),
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
                Location = new Point(210, 6),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body
            };
            FillPresetCombo(presetCombo, segment.PresetName);

            var monitorToggle = new ToggleSwitch
            {
                Location = new Point(340, 6),
                Checked = hasMonitorPresets,
                Width = 44
            };

            var monitorLabel = new Label
            {
                Text = hasMonitorPresets ? "独立" : "统一",
                Location = new Point(388, 9),
                AutoSize = true,
                Font = Typography.Caption,
                ForeColor = hasMonitorPresets ? Colors.Brand : Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            var deleteBtn = new Button
            {
                Text = "×",
                Location = new Point(440, 6),
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

            startPicker.ValueChanged += (s, ev) =>
            {
                var t = startPicker.Value;
                _segments[idx].StartTime = $"{t.Hour:D2}:{t.Minute:D2}";
            };

            endPicker.ValueChanged += (s, ev) =>
            {
                var t = endPicker.Value;
                _segments[idx].EndTime = $"{t.Hour:D2}:{t.Minute:D2}";
            };

            presetCombo.SelectedIndexChanged += (s, ev) =>
            {
                _segments[idx].PresetName = presetCombo.SelectedItem?.ToString() ?? "标准";
            };

            monitorToggle.CheckedChanged += (s, ev) =>
            {
                if (_isUpdatingToggle) return;
                if (monitorToggle.Checked)
                {
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
                            if (MessageBox.Show("收起将清除各显示器的独立预设配置，是否继续？", "确认",
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
                    oldRow.Dispose();
                    _segmentPanel.Controls.RemoveAt(idx);
                    for (int j = idx; j < _segments.Count; j++)
                    {
                        var existingRow = _segmentPanel.Controls[j];
                        existingRow.Dispose();
                        _segmentPanel.Controls.RemoveAt(j);
                        _segmentPanel.Controls.Add(CreateSegmentRow(j));
                        _segmentPanel.Controls.SetChildIndex(_segmentPanel.Controls[_segmentPanel.Controls.Count - 1], j);
                    }
                    _segmentPanel.ResumeLayout(true);
                }
            };

            container.Controls.AddRange(new Control[] { startPicker, arrowLbl, endPicker, presetCombo, monitorToggle, monitorLabel, deleteBtn });

            if (hasMonitorPresets)
            {
                int my = 40;
                foreach (var mon in _monitors)
                {
                    var monId = mon.DeviceId;
                    var monName = mon.DisplayName ?? mon.DeviceId;

                    var monIndent = new Label
                    {
                        Text = "  └",
                        Location = new Point(8, my + 2),
                        AutoSize = true,
                        Font = Typography.Caption,
                        ForeColor = Colors.Border,
                        BackColor = Color.Transparent
                    };

                    var monLabel = new Label
                    {
                        Text = monName,
                        Location = new Point(36, my + 2),
                        AutoSize = true,
                        Font = Typography.Caption,
                        ForeColor = Colors.TextSecondary,
                        BackColor = Color.Transparent
                    };

                    var monCombo = new ComboBox
                    {
                        Location = new Point(210, my),
                        Width = 120,
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
                        _segments[idx].MonitorPresets[monId] = monCombo.SelectedItem?.ToString() ?? "标准";
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
                cb.Items.Add(p);
            foreach (var cp in _customPresets)
                cb.Items.Add(cp.Name);
            if (cb.Items.Contains(selected))
                cb.SelectedItem = selected;
            else
                cb.SelectedIndex = 0;
        }
    }
}
