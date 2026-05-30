using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using LumiShift.Infrastructure;

namespace LumiShift.Services
{
    internal static class UpdateService
    {
        private const string ApiUrl = "https://api.github.com/repos/SummerRay160/LumiShift/releases/latest";

        public static async Task CheckForUpdateAsync(bool silent)
        {
            try
            {
                string response;
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.TryParseAdd("LumiShift");
                    client.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github.v3+json");
                    client.Timeout = TimeSpan.FromSeconds(10);

                    response = await client.GetStringAsync(ApiUrl);
                }

                var result = ParseGitHubRelease(response);

                if (string.IsNullOrEmpty(result.version))
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show("当前已是最新版本。", "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return;
                }

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (!Version.TryParse(result.version, out var remoteVersion))
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show("当前已是最新版本。", "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return;
                }

                if (remoteVersion <= currentVersion)
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show("当前已是最新版本。", "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return;
                }

                string message = $"发现新版本 {remoteVersion}\n\n{result.name}\n\n{result.body}";
                var dialogResult = System.Windows.Forms.MessageBox.Show(
                    message, "LumiShift 更新",
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Information);

                if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(result.downloadUrl);
                    }
                    catch { }
                }
            }
            catch
            {
                if (!silent)
                    System.Windows.Forms.MessageBox.Show("检查更新失败，请稍后重试。", "LumiShift",
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private static (string version, string name, string body, string downloadUrl) ParseGitHubRelease(string json)
        {
            using (var reader = new LightweightJsonReader(json))
            {
                var root = reader.ReadObject();
                if (root == null) return (null, null, null, null);

                string tagName = GetString(root, "tag_name")?.TrimStart('v');
                string name = GetString(root, "name");
                string body = GetString(root, "body");
                bool prerelease = GetBool(root, "prerelease");
                bool draft = GetBool(root, "draft");

                if (draft || prerelease || string.IsNullOrEmpty(tagName))
                    return (null, null, null, null);

                string downloadUrl = null;

                if (root.TryGetValue("assets", out var assetsObj) && assetsObj is List<object> assets)
                {
                    foreach (var assetObj in assets)
                    {
                        if (assetObj is Dictionary<string, object> asset)
                        {
                            string assetNameStr = GetString(asset, "name");
                            if (assetNameStr != null && assetNameStr.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = GetString(asset, "browser_download_url");
                                break;
                            }
                        }
                    }
                }

                if (downloadUrl == null)
                    return (null, null, null, null);

                return (tagName, name, body, downloadUrl);
            }
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var val))
            {
                if (val is string s) return s;
                return val?.ToString();
            }
            return null;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var val))
            {
                if (val is bool b) return b;
                if (val is string s && bool.TryParse(s, out var sb)) return sb;
            }
            return false;
        }
    }
}