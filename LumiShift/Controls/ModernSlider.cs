using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LumiShift.Infrastructure;
using LumiShift.Resources;

namespace LumiShift.Controls
{
    public class ModernSlider : Control
    {
        private int _value = 50;
        private int _minimum;
        private int _maximum = 100;
        private bool _dragging;
        private bool _hovered;
        private bool _hoveredThumb;
        private int _segmentBoundary = -1;
        private Color _segmentColor = Color.Empty;

        public event EventHandler ValueChanged;

        /// <summary>分段边界（滑块值）。-1 表示不分段；边界以下用 <see cref="SegmentColor"/> 标示。</summary>
        [DefaultValue(-1)]
        public int SegmentBoundary
        {
            get => _segmentBoundary;
            set
            {
                if (_segmentBoundary == value) return;
                _segmentBoundary = value;
                Invalidate();
            }
        }

        /// <summary>边界以下区间的颜色。联动开启时用强调色，关闭时用灰色表示不可达。</summary>
        public Color SegmentColor
        {
            get => _segmentColor;
            set
            {
                if (_segmentColor == value) return;
                _segmentColor = value;
                Invalidate();
            }
        }

        [DefaultValue(0)]
        public int Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_value < value) _value = value;
                Invalidate();
            }
        }

        [DefaultValue(100)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_value > value) _value = value;
                Invalidate();
            }
        }

        [DefaultValue(50)]
        public int Value
        {
            get => _value;
            set
            {
                int newVal = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != newVal)
                {
                    _value = newVal;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ModernSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.DoubleBuffer |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Height = 22;
            Width = 200;
            BackColor = Color.Transparent;
        }

        private float GetThumbCenter()
        {
            float margin = DpiScale.S(8);
            if (_maximum <= _minimum) return margin;
            float ratio = (_value - _minimum) / (float)(_maximum - _minimum);
            return margin + ratio * (Width - margin * 2);
        }

        private int GetValueFromX(float x)
        {
            float margin = DpiScale.S(8);
            if (_maximum <= _minimum) return _minimum;
            float ratio = (x - margin) / (Width - margin * 2);
            ratio = Math.Max(0, Math.Min(1, ratio));
            return (int)(_minimum + ratio * (_maximum - _minimum));
        }

        private static readonly Color ShadowColor = Color.FromArgb(30, 0, 0, 0);
        private static readonly Color HighlightColor = Color.FromArgb(50, 255, 255, 255);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 控件本身由 DpiScale 整体缩放，内部绘制尺寸需按同一比例换算
            float margin = DpiScale.S(8);
            float trackH = DpiScale.S(4);
            float trackY = Height / 2f - trackH / 2f;
            float thumbCenter = GetThumbCenter();
            float thumbSize = DpiScale.S(_hoveredThumb ? 16 : 14);

            bool isActive = Enabled;

            Color inactiveTrack = _hovered ? Colors.Border : Colors.BorderLight;
            Color activeTrack = isActive ? Colors.Brand : Colors.TextDisabled;
            Color thumbColor = isActive
                ? (_hoveredThumb ? Colors.BrandGlow : Colors.Brand)
                : Colors.TextDisabled;
            Color segmentColor = _segmentColor.IsEmpty ? Colors.TextDisabled : _segmentColor;

            // 分段边界：随当前 Gamma 参数动态变化，不能用固定百分比
            float boundaryX = -1f;
            if (isActive && _segmentBoundary > _minimum && _segmentBoundary <= _maximum)
            {
                float ratio = (_segmentBoundary - _minimum) / (float)(_maximum - _minimum);
                boundaryX = margin + ratio * (Width - margin * 2);
            }

            using (var trackPath = CreateRoundRect(margin, trackY, Width - margin * 2, trackH, trackH / 2f))
                g.FillPath(GdiCache.GetBrush(inactiveTrack), trackPath);

            if (boundaryX > margin)
            {
                float segmentW = Math.Max(trackH, boundaryX - margin);
                using (var segmentPath = CreateRoundRect(margin, trackY, segmentW, trackH, trackH / 2f))
                    g.FillPath(GdiCache.GetBrush(Color.FromArgb(70, segmentColor)), segmentPath);
            }

            float filledW = thumbCenter - margin;
            if (filledW > 0 && isActive)
            {
                if (boundaryX > margin && boundaryX < thumbCenter)
                {
                    float lowerW = Math.Max(trackH, boundaryX - margin);
                    using (var lowerPath = CreateRoundRect(margin, trackY, lowerW, trackH, trackH / 2f))
                        g.FillPath(GdiCache.GetBrush(segmentColor), lowerPath);

                    float upperW = Math.Max(trackH, thumbCenter - boundaryX);
                    using (var upperPath = CreateRoundRect(boundaryX, trackY, upperW, trackH, trackH / 2f))
                        g.FillPath(GdiCache.GetBrush(activeTrack), upperPath);
                }
                else
                {
                    Color fill = boundaryX > margin && thumbCenter <= boundaryX ? segmentColor : activeTrack;
                    using (var fillPath = CreateRoundRect(margin, trackY, filledW, trackH, trackH / 2f))
                        g.FillPath(GdiCache.GetBrush(fill), fillPath);
                }
            }

            if (boundaryX > margin)
            {
                float lineW = Math.Max(1f, DpiScale.S(1));
                g.FillRectangle(GdiCache.GetBrush(Colors.TextPrimary),
                    boundaryX - lineW / 2f, trackY - DpiScale.S(2), lineW, trackH + DpiScale.S(4));
            }

            float thumbX = thumbCenter - thumbSize / 2f;
            float thumbY = (Height - thumbSize) / 2f;

            g.FillEllipse(GdiCache.GetBrush(ShadowColor), thumbX + 1, thumbY + 1, thumbSize, thumbSize);

            g.FillEllipse(GdiCache.GetBrush(thumbColor), thumbX, thumbY, thumbSize, thumbSize);

            if (_hoveredThumb && isActive)
            {
                float highlight = DpiScale.S(3);
                g.FillEllipse(GdiCache.GetBrush(HighlightColor), thumbX + highlight, thumbY + highlight, thumbSize / 2f, thumbSize / 2f);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _dragging = true;
                Value = GetValueFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            float thumbCenter = GetThumbCenter();
            float thumbSize = DpiScale.S(14);
            bool overThumb = Math.Abs(e.X - thumbCenter) <= thumbSize / 2f + DpiScale.S(3);

            if (_hoveredThumb != overThumb)
            {
                _hoveredThumb = overThumb;
                Invalidate();
            }

            if (_dragging && Enabled)
                Value = GetValueFromX(e.X);

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Cursor = Cursors.Hand;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _hoveredThumb = false;
            _dragging = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        private static GraphicsPath CreateRoundRect(float x, float y, float w, float h, float r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}