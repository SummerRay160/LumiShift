using System;
using System.Drawing;
using System.Windows.Forms;
using LumiShift.Resources;

namespace LumiShift.Services
{
    internal sealed class UpdateDialog : Form
    {
        private readonly Button _downloadButton;
        private readonly Button _laterButton;
        private readonly Button _skipButton;
        private readonly TextBox _bodyBox;

        public UpdateDialog(string version, string name, string body)
        {
            Text = "LumiShift 更新";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 380);
            BackColor = Colors.Background;

            var titleLabel = new Label
            {
                Text = $"发现新版本 {version}",
                Font = Typography.H1,
                ForeColor = Colors.TextPrimary,
                Location = new Point(16, 16),
                AutoSize = true
            };

            var nameLabel = new Label
            {
                Text = name,
                Font = Typography.BodyBold,
                ForeColor = Colors.TextSecondary,
                Location = new Point(16, titleLabel.Bottom + 6),
                AutoSize = true
            };

            _bodyBox = new TextBox
            {
                Text = body,
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                Font = Typography.Caption,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(16, nameLabel.Bottom + 12),
                Size = new Size(428, 232)
            };

            var buttonY = _bodyBox.Bottom + 14;

            _downloadButton = new Button
            {
                Text = "下载更新",
                Font = Typography.Body,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Brand,
                ForeColor = Color.White,
                FlatAppearance = { BorderSize = 0 },
                Size = new Size(100, 30),
                Location = new Point(16, buttonY),
                DialogResult = DialogResult.Yes
            };

            _laterButton = new Button
            {
                Text = "稍后提醒",
                Font = Typography.Body,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                FlatAppearance = { BorderColor = Colors.Border },
                Size = new Size(100, 30),
                Location = new Point(_downloadButton.Right + 8, buttonY),
                DialogResult = DialogResult.No
            };

            _skipButton = new Button
            {
                Text = "跳过版本",
                Font = Typography.Body,
                FlatStyle = FlatStyle.Flat,
                BackColor = Colors.Surface,
                ForeColor = Colors.TextPrimary,
                FlatAppearance = { BorderColor = Colors.Border },
                Size = new Size(100, 30),
                Location = new Point(_laterButton.Right + 8, buttonY),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(titleLabel);
            Controls.Add(nameLabel);
            Controls.Add(_bodyBox);
            Controls.Add(_downloadButton);
            Controls.Add(_laterButton);
            Controls.Add(_skipButton);

            _downloadButton.FlatAppearance.MouseOverBackColor = Colors.BrandHover;
            _laterButton.FlatAppearance.MouseOverBackColor = Colors.SurfaceLight;
            _skipButton.FlatAppearance.MouseOverBackColor = Colors.SurfaceLight;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _bodyBox.SelectionStart = 0;
            _bodyBox.SelectionLength = 0;
            _downloadButton.Focus();
        }
    }
}