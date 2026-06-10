using System;
using System.Drawing;
using System.Windows.Forms;
using LumiShift.Resources;

namespace LumiShift.Controls
{
    public class ToggleSwitch : Control
    {
        private bool _checked;
        private bool _hovered;

        // 预定义尺寸
        private const int SwitchWidth = 36;
        private const int SwitchHeight = 18;
        private const int TrackHeight = 12;
        private const int ThumbSize = 14;

        // 预定义颜色（避免每次绘制时创建）
        private static readonly Color ShadowColor = Color.FromArgb(100, 0, 0, 0);
        private static readonly Color ThumbBorderColor = Color.FromArgb(60, 0, 0, 0);
        private static readonly Color HoverThumbColor = Color.FromArgb(250, 250, 250);

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.DoubleBuffer |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Width = SwitchWidth;
            Height = SwitchHeight;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                Checked = !Checked;
            base.OnMouseClick(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            // 计算位置
            float trackY = (Height - TrackHeight) / 2f;
            float trackX = 1f;
            float trackW = Width - 2f;
            float thumbY = (Height - ThumbSize) / 2f;
            float thumbX = _checked ? (trackX + trackW - ThumbSize - 1f) : (trackX + 1f);

            // 轨道颜色
            Color trackColor = _checked ? Colors.Green : Colors.BorderLight;
            float radius = TrackHeight / 2f;

            // 绘制轨道（使用缓存画笔）
            var trackBrush = GdiCache.GetBrush(trackColor);
            g.FillEllipse(trackBrush, trackX, trackY, TrackHeight, TrackHeight);
            g.FillEllipse(trackBrush, trackX + trackW - TrackHeight, trackY, TrackHeight, TrackHeight);
            g.FillRectangle(trackBrush, trackX + radius, trackY, trackW - TrackHeight, TrackHeight);

            // 绘制滑块阴影
            var shadowBrush = GdiCache.GetBrush(ShadowColor);
            g.FillEllipse(shadowBrush, thumbX + 1, thumbY + 1, ThumbSize, ThumbSize);

            // 绘制滑块
            Color thumbColor = _hovered ? HoverThumbColor : Color.White;
            var thumbBrush = GdiCache.GetBrush(thumbColor);
            g.FillEllipse(thumbBrush, thumbX, thumbY, ThumbSize, ThumbSize);

            // 滑块边框
            var thumbPen = GdiCache.GetPen(ThumbBorderColor);
            g.DrawEllipse(thumbPen, thumbX, thumbY, ThumbSize, ThumbSize);
        }
    }
}
