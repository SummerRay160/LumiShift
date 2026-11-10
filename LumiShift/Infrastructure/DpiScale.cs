using System;
using System.Drawing;
using System.Windows.Forms;

namespace LumiShift.Infrastructure
{
    /// <summary>
    /// 统一 DPI 缩放。
    /// 界面全部按 96 DPI 下的像素坐标编写，进程声明为系统 DPI 感知后，
    /// 字体（以磅为单位）会随 DPI 放大，因此控件布局也必须同比缩放，
    /// 否则高分屏（如 4K 200%）下会出现文字互相重叠、控件错位。
    /// </summary>
    internal static class DpiScale
    {
        private static float _factor = -1f;

        /// <summary>当前系统 DPI 相对 96 DPI 的倍数（100% 缩放时为 1）。</summary>
        public static float Factor
        {
            get
            {
                if (_factor < 0f)
                    _factor = DetectFactor();
                return _factor;
            }
        }

        private static float DetectFactor()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    return g.DpiX > 0 ? g.DpiX / 96f : 1f;
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>把 96 DPI 设计值换算为当前 DPI 下的像素值。</summary>
        public static int S(int value)
        {
            return (int)Math.Round(value * Factor, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 测量多行文本在 96 DPI 设计宽度下需要的行高。
        /// 字体以磅为单位，会随 DPI 放大；若仍按设计宽度测量，行数会被高估，
        /// 因此这里按缩放后的宽度测量，再折算回设计值（随后由 Apply 统一放大）。
        /// </summary>
        public static int MeasureWrappedHeight(string text, Font font, int designWidth, int minHeight, int extraPadding)
        {
            if (string.IsNullOrEmpty(text) || font == null)
                return minHeight;

            bool scaled = Factor > 1.001f;
            int width = scaled ? S(designWidth) : designWidth;
            int measured;
            try
            {
                measured = TextRenderer.MeasureText(text, font, new Size(width, 0), TextFormatFlags.WordBreak).Height;
            }
            catch
            {
                return minHeight;
            }

            if (scaled)
                measured = (int)Math.Round(measured / Factor, MidpointRounding.AwayFromZero);

            return Math.Max(minHeight, measured + extraPadding);
        }

        /// <summary>整体缩放窗体及其全部子控件（含运行时动态创建的控件）。</summary>
        public static void Apply(Control root)
        {
            if (root == null || Factor <= 1.001f)
                return;

            root.Scale(new SizeF(Factor, Factor));
        }
    }
}
