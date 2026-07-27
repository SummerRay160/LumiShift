using System.Windows.Forms;

namespace LumiShift.Controls
{
    /// <summary>
    /// 可获取焦点的 Panel，用作行容器，使子控件（DateTimePicker/ComboBox）
    /// 能在按 Enter 或点击空白处时失焦。
    /// 默认 Panel 不可获取焦点，需设置 Selectable 样式。
    /// </summary>
    internal class FocusablePanel : Panel
    {
        public FocusablePanel()
        {
            // 让 Panel 可被 Focus() 选中
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;  // 不参与 Tab 流转，仅作为焦点吸收器
        }
    }
}
