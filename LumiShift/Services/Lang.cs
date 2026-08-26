using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace LumiShift.Services
{
    /// <summary>
    /// 多语言入口：简体中文原文即 key，翻译来自嵌入式 StringsEn / StringsZhHant .resources，
    /// 缺失的条目原样回退简体中文，保证不因漏翻而崩溃。
    /// 用法：Lang.Get("标准")；带占位符用 Lang.F("当前亮度 {0}%", value)。
    /// </summary>
    internal static class Lang
    {
        private static readonly Dictionary<string, string> EmptyMap = new Dictionary<string, string>();

        private static Dictionary<string, string> _map = EmptyMap;

        /// <summary>当前语言："en" / "zh-Hant" / "zh"。</summary>
        public static string Current { get; private set; } = "zh";

        /// <summary>
        /// 在 Program 启动时、任何 UI 创建之前调用。
        /// language 取自 UserSettings.Language："" = 跟随系统，"en" / "zh" / "zh-Hant" 强制指定。
        /// </summary>
        public static void Init(string language)
        {
            Current = Resolve(language);
            _map = Current == "zh" ? EmptyMap : LoadMap(Current);
        }

        private static string Resolve(string language)
        {
            if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
                return "en";
            if (string.Equals(language, "zh-Hant", StringComparison.OrdinalIgnoreCase))
                return "zh-Hant";
            if (string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase))
                return "zh";

            // ponytail: 系统语言区分 en / 繁体区域 / 简体，其余一律回退简体
            var ui = Thread.CurrentThread.CurrentUICulture;
            if (ui.TwoLetterISOLanguageName == "en")
                return "en";
            if (ui.TwoLetterISOLanguageName == "zh")
            {
                // zh-TW / zh-HK / zh-MO / zh-Hant-* 视为繁体
                string name = ui.Name;
                if (name.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase))
                    return "zh-Hant";
            }
            return "zh";
        }

        private static Dictionary<string, string> LoadMap(string lang)
        {
            var map = new Dictionary<string, string>();
            try
            {
                var asm = Assembly.GetExecutingAssembly();

                // 不硬编码清单资源名，避免受 csproj 命名规则影响
                string suffix = lang == "zh-Hant" ? "StringsZhHant.resources" : "StringsEn.resources";
                var resourceName = Array.Find(asm.GetManifestResourceNames(),
                    n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                if (resourceName == null)
                    return map;

                using (var stream = asm.GetManifestResourceStream(resourceName))
                using (var reader = new ResourceReader(stream))
                {
                    foreach (DictionaryEntry entry in reader)
                        map[(string)entry.Key] = (string)entry.Value;
                }
            }
            catch
            {
                // 翻译表加载失败即整体回退简体显示，不阻断启动
            }
            return map;
        }

        /// <summary>查一条文案；翻译缺失时原样返回简体中文原文。</summary>
        public static string Get(string zh)
        {
            return _map.Count > 0 && _map.TryGetValue(zh, out string v) ? v : zh;
        }

        /// <summary>带格式化占位符的文案，中文格式串同样走 Get 查表。</summary>
        public static string F(string zhFormat, params object[] args)
        {
            return string.Format(Get(zhFormat), args);
        }
    }
}
