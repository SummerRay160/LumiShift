using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LumiShift.Services
{
    public class UpdateService
    {
        private const string RepoApiUrl = "https://api.github.com/repos/SummerRay160/LumiShift/releases/latest";
        private const string RepoReleaseUrl = "https://github.com/SummerRay160/LumiShift/releases/latest";

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, v.Build);
            }
        }

        public static void CheckForUpdate(bool silent = false)
        {
            var bw = new System.ComponentModel.BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                e.Result = FetchLatestRelease();
            };
            bw.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    if (!silent)
                        MessageBox.Show($"检查更新失败: {e.Error.Message}", "更新检查",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var release = e.Result as GitHubRelease;
                if (release == null)
                {
                    if (!silent)
                        MessageBox.Show("当前已是最新版本。", "更新检查",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var latestVersion = ParseVersion(release.tag_name);
                if (latestVersion == null)
                {
                    if (!silent)
                        MessageBox.Show("无法解析远程版本号。", "更新检查",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (latestVersion <= CurrentVersion)
                {
                    if (!silent)
                        MessageBox.Show($"当前已是最新版本 ({CurrentVersion})。", "更新检查",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var settings = SettingsStore.LoadSettings();
                if (silent && settings.SkipVersion == latestVersion.ToString())
                    return;

                ShowUpdateDialog(release, latestVersion);
            };
            bw.RunWorkerAsync();
        }

        private static GitHubRelease FetchLatestRelease()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "LumiShift-UpdateCheck");
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = client.GetAsync(RepoApiUrl).Result;
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = response.Content.ReadAsStringAsync().Result;
                return Serializer.Deserialize<GitHubRelease>(json);
            }
        }

        private static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            var match = Regex.Match(tag, @"v?(\d+\.\d+\.\d+)");
            if (match.Success)
                return new Version(match.Groups[1].Value);
            return null;
        }

        private static void ShowUpdateDialog(GitHubRelease release, Version latestVersion)
        {
            using (var dialog = new UpdateDialog())
            {
                dialog.SetUpdateInfo(CurrentVersion.ToString(), latestVersion.ToString(),
                    release.body ?? release.tag_name,
                    release.assets?.Select(a => a.browser_download_url).FirstOrDefault() ?? RepoReleaseUrl);

                var result = dialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(dialog.DownloadUrl) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法打开下载链接: {ex.Message}", "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else if (result == DialogResult.Ignore)
                {
                    var settings = SettingsStore.LoadSettings();
                    settings.SkipVersion = latestVersion.ToString();
                    SettingsStore.SaveSettings(settings);
                }
            }
        }

        private class GitHubRelease
        {
            public string tag_name { get; set; }
            public string name { get; set; }
            public string body { get; set; }
            public List<GitHubAsset> assets { get; set; }
            public bool prerelease { get; set; }
            public bool draft { get; set; }
        }

        private class GitHubAsset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
            public long size { get; set; }
        }
    }

    public class UpdateDialog : Form
    {
        private Label _titleLabel;
        private Label _versionLabel;
        private TextBox _changelogBox;
        private Button _downloadButton;
        private Button _skipButton;
        private Button _laterButton;

        public string DownloadUrl { get; private set; }

        public UpdateDialog()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Text = "LumiShift 更新";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(460, 340);
            MaximizeBox = false;
            MinimizeBox = false;

            _titleLabel = new Label
            {
                Text = "发现新版本",
                Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold),
                AutoSize = true,
                Location = new System.Drawing.Point(20, 16)
            };

            _versionLabel = new Label
            {
                Text = "",
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
                AutoSize = true,
                Location = new System.Drawing.Point(20, 50),
                ForeColor = System.Drawing.Color.Gray
            };

            _changelogBox = new TextBox
            {
                Location = new System.Drawing.Point(20, 78),
                Size = new System.Drawing.Size(420, 190),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
                BackColor = System.Drawing.Color.White
            };

            _downloadButton = new Button
            {
                Text = "立即下载",
                Size = new System.Drawing.Size(100, 32),
                Location = new System.Drawing.Point(148, 286),
                DialogResult = DialogResult.OK,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9F)
            };

            _skipButton = new Button
            {
                Text = "跳过此版本",
                Size = new System.Drawing.Size(100, 32),
                Location = new System.Drawing.Point(254, 286),
                DialogResult = DialogResult.Ignore,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9F)
            };

            _laterButton = new Button
            {
                Text = "稍后提醒",
                Size = new System.Drawing.Size(100, 32),
                Location = new System.Drawing.Point(360, 286),
                DialogResult = DialogResult.Cancel,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9F)
            };

            AcceptButton = _downloadButton;
            CancelButton = _laterButton;

            Controls.AddRange(new Control[] { _titleLabel, _versionLabel, _changelogBox, _downloadButton, _skipButton, _laterButton });
        }

        public void SetUpdateInfo(string currentVersion, string latestVersion, string changelog, string downloadUrl)
        {
            _versionLabel.Text = $"当前版本: {currentVersion}  →  最新版本: {latestVersion}";
            _changelogBox.Text = string.IsNullOrEmpty(changelog) ? "暂无更新说明" : changelog;
            DownloadUrl = downloadUrl;
        }
    }
}
