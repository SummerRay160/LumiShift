using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LumiShift.Services
{
    internal static class UpdateService
    {
        private const string ApiUrl = "https://api.github.com/repos/SummerRay160/LumiShift/releases/latest";

        private static readonly HttpClient _httpClient;

        private static readonly Regex RxImageRef = new Regex(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex RxLinkRef = new Regex(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex RxHeading = new Regex(@"^#{1,6}\s+", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex RxBold3 = new Regex(@"\*{3}(.+?)\*{3}", RegexOptions.Compiled);
        private static readonly Regex RxBold2 = new Regex(@"\*{2}(.+?)\*{2}", RegexOptions.Compiled);
        private static readonly Regex RxBold1 = new Regex(@"\*{1}(.+?)\*{1}", RegexOptions.Compiled);
        private static readonly Regex RxUnder3 = new Regex(@"_{3}(.+?)_{3}", RegexOptions.Compiled);
        private static readonly Regex RxUnder2 = new Regex(@"_{2}(.+?)_{2}", RegexOptions.Compiled);
        private static readonly Regex RxUnder1 = new Regex(@"_{1}(.+?)_{1}", RegexOptions.Compiled);
        private static readonly Regex RxStrike = new Regex(@"~~(.+?)~~", RegexOptions.Compiled);
        private static readonly Regex RxCodeBlock = new Regex(@"`{3}[\s\S]*?`{3}", RegexOptions.Compiled);
        private static readonly Regex RxInlineCode = new Regex(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex RxListItem = new Regex(@"^\s*[-*+]\s+", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex RxBlockquote = new Regex(@"^\s*>\s+", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex RxHr = new Regex(@"^[-*_]{3,}\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex RxHtml = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex RxMultiNewline = new Regex(@"\n{3,}", RegexOptions.Compiled);

        private static volatile bool _disposed;

        static UpdateService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                MaxConnectionsPerServer = 2,
                UseProxy = true,
                Proxy = System.Net.WebRequest.GetSystemWebProxy(),
                PreAuthenticate = true
            };
            handler.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("LumiShift");
            _httpClient.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github.v3+json");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                System.Net.ServicePointManager.FindServicePoint(new Uri(ApiUrl)).ConnectionLeaseTimeout = 60000;
            }
            catch { }
        }

        public static async Task<string> CheckForUpdateAsync(bool silent, string skipVersion = null, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return null;

            try
            {
                string response;
                using (var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl))
                using (var httpResponse = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    response = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                var result = ParseGitHubRelease(response);

                if (result.blockedReason != null)
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show("未找到可用更新。", "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return null;
                }

                if (string.IsNullOrEmpty(result.version))
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show("当前已是最新版本。", "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return null;
                }

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (!Version.TryParse(result.version, out var remoteVersion))
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show("当前已是最新版本。", "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return null;
                }

                if (remoteVersion <= currentVersion)
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show("当前已是最新版本。", "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return null;
                }

                if (!string.IsNullOrEmpty(skipVersion) && skipVersion == remoteVersion.ToString())
                    return null;

                string cleanBody = StripMarkdown(result.body ?? "");
                using (var dialog = new UpdateDialog(remoteVersion.ToString(), result.name, cleanBody))
                {
                    var dialogResult = dialog.ShowDialog();

                    if (dialogResult == System.Windows.Forms.DialogResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(result.downloadUrl);
                        }
                        catch { }
                    }

                    if (dialogResult == System.Windows.Forms.DialogResult.Cancel)
                        return remoteVersion.ToString();
                }
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;
                if (!silent)
                    System.Windows.Forms.MessageBox.Show(
                        "检查更新超时，请检查网络连接后重试。",
                        "LumiShift",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
            }
            catch (HttpRequestException ex)
            {
                if (!silent)
                {
                    string errorMsg = GetHttpErrorMessage(ex);
                    System.Windows.Forms.MessageBox.Show(
                        errorMsg,
                        "LumiShift",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                }
            }
            catch (Exception)
            {
                if (!silent)
                    System.Windows.Forms.MessageBox.Show(
                        "检查更新失败，请稍后重试。",
                        "LumiShift",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
            }

            return null;
        }

        public static void Shutdown()
        {
            _disposed = true;
        }

        private static string GetHttpErrorMessage(HttpRequestException ex)
        {
            string baseMessage = "检查更新失败";

            string msg = ex.Message ?? "";
            if (ex.InnerException != null)
                msg += " " + (ex.InnerException.Message ?? "");
            string lowerMsg = msg.ToLowerInvariant();

            if (lowerMsg.Contains("403"))
            {
                if (lowerMsg.Contains("rate limit") || lowerMsg.Contains("api rate"))
                    return $"{baseMessage}：API 访问频率限制，请稍后重试。";
                if (lowerMsg.Contains("proxy") || lowerMsg.Contains("407") || lowerMsg.Contains("require"))
                    return $"{baseMessage}：代理服务器验证失败，请检查代理设置。";
                return $"{baseMessage}：服务器拒绝了请求（403），请检查代理或网络设置后重试。";
            }
            if (msg.Contains("404"))
                return $"{baseMessage}：未找到更新信息。";
            if (msg.Contains("500") || msg.Contains("502") || msg.Contains("503"))
                return $"{baseMessage}：GitHub 服务器暂时不可用，请稍后重试。";
            if (msg.Contains("401"))
                return $"{baseMessage}：API 认证失败。";

            if (lowerMsg.Contains("dns") || lowerMsg.Contains("resolve") || lowerMsg.Contains("name"))
                return $"{baseMessage}：无法解析服务器地址，请检查网络连接。";
            if (lowerMsg.Contains("refused") || lowerMsg.Contains("unreachable"))
                return $"{baseMessage}：无法连接到服务器，请检查网络连接。";
            if (lowerMsg.Contains("tls") || lowerMsg.Contains("ssl") || lowerMsg.Contains("secure"))
                return $"{baseMessage}：安全连接失败，请检查系统时间或网络环境。";
            if (lowerMsg.Contains("timeout") || lowerMsg.Contains("timed out"))
                return $"{baseMessage}：连接超时，请检查网络连接后重试。";

            return $"{baseMessage}：网络请求失败，请检查网络连接后重试。";
        }

        private static string StripMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = RxImageRef.Replace(text, "$1");
            text = RxLinkRef.Replace(text, "$1");
            text = RxHeading.Replace(text, "");
            text = RxBold3.Replace(text, "$1");
            text = RxBold2.Replace(text, "$1");
            text = RxBold1.Replace(text, "$1");
            text = RxUnder3.Replace(text, "$1");
            text = RxUnder2.Replace(text, "$1");
            text = RxUnder1.Replace(text, "$1");
            text = RxStrike.Replace(text, "$1");
            text = RxCodeBlock.Replace(text, "");
            text = RxInlineCode.Replace(text, "$1");
            text = RxListItem.Replace(text, "  • ");
            text = RxBlockquote.Replace(text, "");
            text = RxHr.Replace(text, "");
            text = RxHtml.Replace(text, "");
            text = RxMultiNewline.Replace(text, "\n\n");

            return text.Trim();
        }

        private static (string version, string name, string body, string downloadUrl, string blockedReason) ParseGitHubRelease(string json)
        {
            using (var reader = new LumiShift.Infrastructure.LightweightJsonReader(json))
            {
                var root = reader.ReadObject();
                if (root == null) return (null, null, null, null, null);

                string tagName = GetString(root, "tag_name")?.TrimStart('v');
                string name = GetString(root, "name");
                string body = GetString(root, "body");
                bool prerelease = GetBool(root, "prerelease");
                bool draft = GetBool(root, "draft");

                if (draft)
                    return (null, null, null, null, "draft");
                if (prerelease)
                    return (null, null, null, null, "prerelease");
                if (string.IsNullOrEmpty(tagName))
                    return (null, null, null, null, null);

                string downloadUrl = null;

                if (root.TryGetValue("assets", out var assetsObj) && assetsObj is List<object>)
                {
                    var assets = (List<object>)assetsObj;
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
                    return (null, null, null, null, null);

                return (tagName, name, body, downloadUrl, null);
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