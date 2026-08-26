using System;
using System.Drawing;
using System.Windows.Forms;
using LumiShift.Models;
using LumiShift.Resources;

namespace LumiShift.Services
{
    internal sealed class SaveDisplaySchemeDialog : Form
    {
        private readonly TextBox _nameTextBox;
        private readonly RadioButton _unifiedRadioButton;
        private readonly RadioButton _multiDisplayRadioButton;

        public string SchemeName => _nameTextBox.Text.Trim();
        public DisplaySchemeKind SchemeKind => _multiDisplayRadioButton.Checked ? DisplaySchemeKind.MultiDisplay : DisplaySchemeKind.Unified;

        public SaveDisplaySchemeDialog(bool hasDisplayOverrides)
        {
            Text = Lang.Get("保存显示方案");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 258);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Colors.Background;

            var title = new Label
            {
                Text = Lang.Get("保存显示方案"),
                Location = new Point(18, 16),
                AutoSize = true,
                Font = Typography.H1,
                ForeColor = Colors.TextPrimary,
                BackColor = Color.Transparent
            };

            var hint = new Label
            {
                Text = Lang.Get("显示方案用于手动应用，也可以被定时调度自动切换。"),
                Location = new Point(18, 42),
                Width = 380,
                Height = 18,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            var nameLabel = new Label
            {
                Text = Lang.Get("方案名称"),
                Location = new Point(18, 76),
                AutoSize = true,
                Font = Typography.Body,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            _nameTextBox = new TextBox
            {
                Text = hasDisplayOverrides ? Lang.Get("我的多屏方案") : Lang.Get("我的统一方案"),
                Location = new Point(92, 72),
                Width = 300,
                Font = Typography.Body,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary
            };

            _unifiedRadioButton = new RadioButton
            {
                Text = Lang.Get("统一方案"),
                Location = new Point(22, 112),
                Width = 120,
                Height = 22,
                Font = Typography.BodyBold,
                ForeColor = Colors.TextPrimary,
                BackColor = Color.Transparent,
                Checked = !hasDisplayOverrides
            };

            var unifiedHint = new Label
            {
                Text = Lang.Get("所有显示器使用当前这套显示效果，适合大多数场景。"),
                Location = new Point(42, 136),
                Width = 350,
                Height = 18,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            _multiDisplayRadioButton = new RadioButton
            {
                Text = Lang.Get("多屏方案"),
                Location = new Point(22, 164),
                Width = 120,
                Height = 22,
                Font = Typography.BodyBold,
                ForeColor = hasDisplayOverrides ? Colors.TextPrimary : Colors.TextSecondary,
                BackColor = Color.Transparent,
                Checked = hasDisplayOverrides,
                Enabled = hasDisplayOverrides
            };

            var multiHint = new Label
            {
                Text = hasDisplayOverrides
                    ? Lang.Get("保存每台显示器当前的独立设置，适合双屏或多屏工作流。")
                    : Lang.Get("当前没有单独设置的显示器，请先为单台显示器设置独立效果。"),
                Location = new Point(42, 188),
                Width = 350,
                Height = 18,
                Font = Typography.Caption,
                ForeColor = Colors.TextSecondary,
                BackColor = Color.Transparent
            };

            var saveButton = new Button
            {
                Text = Lang.Get("保存"),
                DialogResult = DialogResult.OK,
                Location = new Point(232, 218),
                Width = 76,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Brand,
                ForeColor = Color.White,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 }
            };

            var cancelButton = new Button
            {
                Text = Lang.Get("取消"),
                DialogResult = DialogResult.Cancel,
                Location = new Point(316, 218),
                Width = 76,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Body,
                FlatAppearance = { BorderSize = 0 }
            };

            Controls.AddRange(new Control[]
            {
                title, hint, nameLabel, _nameTextBox,
                _unifiedRadioButton, unifiedHint,
                _multiDisplayRadioButton, multiHint,
                saveButton, cancelButton
            });

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            Shown += (s, e) => { _nameTextBox.SelectAll(); _nameTextBox.Focus(); };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK && string.IsNullOrWhiteSpace(SchemeName))
            {
                MessageBox.Show(this, Lang.Get("请输入一个容易识别的方案名称。"), Lang.Get("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
            }

            base.OnFormClosing(e);
        }
    }
}
