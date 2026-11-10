using System;
using System.Drawing;
using System.Windows.Forms;
using LumiShift.Infrastructure;
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

            // 控件本身由 DpiScale 整体缩放，内部绘制尺寸需按同一比例换算
            int trackHeight = DpiScale.S(TrackHeight);
            int thumbSize = DpiScale.S(ThumbSize);
            float edge = DpiScale.S(1);
            float gap = DpiScale.S(2);

            // 计算位置
            float trackY = (Height - trackHeight) / 2f;
            float trackX = edge;
            float trackW = Width - gap;
            float thumbY = (Height - thumbSize) / 2f;
            float thumbX = _checked ? (trackX + trackW - thumbSize - edge) : (trackX + edge);

            // 轨道颜色
            Color trackColor = _checked ? Colors.Green : Colors.BorderLight;
            float radius = trackHeight / 2f;

            // 绘制轨道（使用缓存画笔）
            var trackBrush = GdiCache.GetBrush(trackColor);
            g.FillEllipse(trackBrush, trackX, trackY, trackHeight, trackHeight);
            g.FillEllipse(trackBrush, trackX + trackW - trackHeight, trackY, trackHeight, trackHeight);
            g.FillRectangle(trackBrush, trackX + radius, trackY, trackW - trackHeight, trackHeight);

            // 绘制滑块阴影
            var shadowBrush = GdiCache.GetBrush(ShadowColor);
            g.FillEllipse(shadowBrush, thumbX + edge, thumbY + edge, thumbSize, thumbSize);

            // 绘制滑块
            Color thumbColor = _hovered ? HoverThumbColor : Color.White;
            var thumbBrush = GdiCache.GetBrush(thumbColor);
            g.FillEllipse(thumbBrush, thumbX, thumbY, thumbSize, thumbSize);

            // 滑块边框
            var thumbPen = GdiCache.GetPen(ThumbBorderColor);
            g.DrawEllipse(thumbPen, thumbX, thumbY, thumbSize, thumbSize);
        }
    }
}
