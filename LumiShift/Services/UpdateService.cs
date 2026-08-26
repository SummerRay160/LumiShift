using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LumiShift.Services
{
    internal static class UpdateService
    {
        private const string ApiUrl = "https://api.github.com/repos/SummerRay160/LumiShift/releases/latest";
        private const int TimeoutSeconds = 30;

        private static readonly HttpClient _directClient;
        private static readonly HttpClient _proxyClient;

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
            var decompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;

            // 直连 client（不经过代理），适用于 TUN 模式
            var directHandler = new HttpClientHandler
            {
                AutomaticDecompression = decompression,
                UseProxy = false,
                MaxConnectionsPerServer = 2
            };
            _directClient = new HttpClient(directHandler);
            _directClient.DefaultRequestHeaders.UserAgent.TryParseAdd("LumiShift");
            _directClient.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github.v3+json");
            _directClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            // 代理 client（使用系统代理配置），适用于 HTTP 代理环境
            try
            {
                var proxyHandler = new HttpClientHandler
                {
                    AutomaticDecompression = decompression,
                    MaxConnectionsPerServer = 2,
                    UseProxy = true,
                    Proxy = System.Net.WebRequest.GetSystemWebProxy(),
                    PreAuthenticate = true
                };
                proxyHandler.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
                _proxyClient = new HttpClient(proxyHandler);
            }
            catch
            {
                _proxyClient = null;
            }
            if (_proxyClient != null)
            {
                _proxyClient.DefaultRequestHeaders.UserAgent.TryParseAdd("LumiShift");
                _proxyClient.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github.v3+json");
                _proxyClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
            }

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

                // 先尝试直连（适用于 TUN 模式 / 无代理环境），失败后回退到系统代理
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl))
                    using (var httpResponse = await _directClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        response = await ValidateResponseAsync(httpResponse);
                    }
                }
                catch (Exception) when (_proxyClient != null)
                {
                    // 直连失败，改用代理重试
                    using (var proxyRequest = new HttpRequestMessage(HttpMethod.Get, ApiUrl))
                    using (var httpResponse = await _proxyClient.SendAsync(proxyRequest, cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        response = await ValidateResponseAsync(httpResponse);
                    }
                }

                var result = ParseGitHubRelease(response);

                if (result.blockedReason != null)
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show(Lang.Get("未找到可用更新。"), "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return null;
                }

                if (string.IsNullOrEmpty(result.version))
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show(Lang.Get("当前已是最新版本。"), "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return null;
                }

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (!Version.TryParse(result.version, out var remoteVersion))
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show(Lang.Get("当前已是最新版本。"), "LumiShift",
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    return null;
                }

                if (remoteVersion <= currentVersion)
                {
                    if (!silent)
                        System.Windows.Forms.MessageBox.Show(Lang.Get("当前已是最新版本。"), "LumiShift",
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
                        Lang.Get("检查更新超时，请检查网络连接后重试。"),
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
                        Lang.Get("检查更新失败，请稍后重试。"),
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

        #region Helpers

        /// <summary>
        /// 验证 HTTP 响应是否为有效的 GitHub API 返回，避免代理拦截页被误解析为"最新版本"
        /// </summary>
        private static async Task<string> ValidateResponseAsync(HttpResponseMessage httpResponse)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorBody = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"{(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}: {Truncate(errorBody, 200)}");
            }

            string body = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            // 快速验证是否为 GitHub API 的有效 JSON 响应
            if (string.IsNullOrEmpty(body) || !body.Contains("\"tag_name\""))
            {
                throw new HttpRequestException(Lang.Get("服务器返回了无效的响应数据，请检查网络环境后重试。"));
            }

            return body;
        }

        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
                return text;
            return text.Substring(0, maxLen) + "...";
        }

        private static string GetHttpErrorMessage(HttpRequestException ex)
        {
            string msg = ex.Message ?? "";
            if (ex.InnerException != null)
                msg += " " + (ex.InnerException.Message ?? "");
            string lowerMsg = msg.ToLowerInvariant();

            if (lowerMsg.Contains("403"))
            {
                if (lowerMsg.Contains("rate limit") || lowerMsg.Contains("api rate"))
                    return Lang.Get("检查更新失败：API 访问频率限制，请稍后重试。");
                if (lowerMsg.Contains("proxy") || lowerMsg.Contains("407") || lowerMsg.Contains("require"))
                    return Lang.Get("检查更新失败：代理服务器验证失败，请检查代理设置。");
                return Lang.Get("检查更新失败：服务器拒绝了请求（403），请检查代理或网络设置后重试。");
            }
            if (msg.Contains("404"))
                return Lang.Get("检查更新失败：未找到更新信息。");
            if (msg.Contains("500") || msg.Contains("502") || msg.Contains("503"))
                return Lang.Get("检查更新失败：GitHub 服务器暂时不可用，请稍后重试。");
            if (msg.Contains("401"))
                return Lang.Get("检查更新失败：API 认证失败。");

            if (lowerMsg.Contains("dns") || lowerMsg.Contains("resolve") || lowerMsg.Contains("name"))
                return Lang.Get("检查更新失败：无法解析服务器地址，请检查网络连接。");
            if (lowerMsg.Contains("refused") || lowerMsg.Contains("unreachable"))
                return Lang.Get("检查更新失败：无法连接到服务器，请检查网络连接。");
            if (lowerMsg.Contains("tls") || lowerMsg.Contains("ssl") || lowerMsg.Contains("secure"))
                return Lang.Get("检查更新失败：安全连接失败，请检查系统时间或网络环境。");
            if (lowerMsg.Contains("timeout") || lowerMsg.Contains("timed out"))
                return Lang.Get("检查更新失败：连接超时，请检查网络连接后重试。");

            return Lang.Get("检查更新失败：网络请求失败，请检查网络连接后重试。");
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

        private class GitHubReleaseAsset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
        }

        private class GitHubReleaseInfo
        {
            public string tag_name { get; set; }
            public string name { get; set; }
            public string body { get; set; }
            public bool prerelease { get; set; }
            public bool draft { get; set; }
            public List<GitHubReleaseAsset> assets { get; set; }
        }

        private static (string version, string name, string body, string downloadUrl, string blockedReason) ParseGitHubRelease(string json)
        {
            var info = new JavaScriptSerializer().Deserialize<GitHubReleaseInfo>(json);
            if (info == null) return (null, null, null, null, null);

            if (info.draft) return (null, null, null, null, "draft");
            if (info.prerelease) return (null, null, null, null, "prerelease");

            string tagName = info.tag_name?.TrimStart('v');
            if (string.IsNullOrEmpty(tagName)) return (null, null, null, null, null);

            string downloadUrl = null;
            if (info.assets != null)
            {
                foreach (var asset in info.assets)
                {
                    if (asset.name != null && asset.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.browser_download_url;
                        break;
                    }
                }
            }
            if (downloadUrl == null) return (null, null, null, null, null);

            return (tagName, info.name, info.body, downloadUrl, null);
        }


        #endregion
    }
}