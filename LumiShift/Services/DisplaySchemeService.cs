using System.Collections.Generic;
using System.Linq;
using LumiShift.Models;

namespace LumiShift.Services
{
    public class DisplaySchemeService
    {
        private readonly UserSettings _settings;

        public DisplaySchemeService(UserSettings settings)
        {
            _settings = settings;
        }

        public List<DisplayScheme> GetSchemes()
        {
            var schemes = new List<DisplayScheme>();

            foreach (var builtIn in PresetDefinitions.BuiltIns)
            {
                schemes.Add(new DisplayScheme
                {
                    Name = builtIn.Name,
                    Kind = DisplaySchemeKind.Unified,
                    UnifiedConfig = GammaConfig.FromBuiltIn(builtIn),
                    IsBuiltIn = true
                });
            }

            if (_settings.CustomGammaPresets != null)
            {
                foreach (var preset in _settings.CustomGammaPresets)
                    schemes.Add(FromPreset(preset));
            }

            return schemes;
        }

        public DisplayScheme Find(string name)
        {
            return GetSchemes().FirstOrDefault(s => s.Name == name);
        }

        public bool IsMultiDisplayScheme(string name)
        {
            return Find(name)?.Kind == DisplaySchemeKind.MultiDisplay;
        }

        public string GetDisplayName(string name)
        {
            var scheme = Find(name);
            return scheme != null ? scheme.DisplayName : name;
        }

        public static string StripDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return displayName;
            const string unified = " · 统一方案";
            const string multi = " · 多屏方案";
            if (displayName.EndsWith(unified))
                return displayName.Substring(0, displayName.Length - unified.Length);
            if (displayName.EndsWith(multi))
                return displayName.Substring(0, displayName.Length - multi.Length);
            return displayName.Replace("（多屏方案）", "");
        }

        private static DisplayScheme FromPreset(GammaPreset preset)
        {
            var scheme = new DisplayScheme
            {
                Name = preset.Name,
                Kind = preset.PerDisplaySnapshot != null && preset.PerDisplaySnapshot.Count > 0
                    ? DisplaySchemeKind.MultiDisplay
                    : DisplaySchemeKind.Unified,
                UnifiedConfig = GammaConfig.FromPreset(preset),
                IsBuiltIn = false
            };

            if (scheme.Kind == DisplaySchemeKind.MultiDisplay)
            {
                scheme.DisplayConfigs = new Dictionary<string, GammaConfig>();
                foreach (var item in preset.PerDisplaySnapshot)
                {
                    scheme.DisplayConfigs[item.Key] = new GammaConfig
                    {
                        RScale = item.Value.RScale,
                        GScale = item.Value.GScale,
                        BScale = item.Value.BScale,
                        GammaValue = item.Value.GammaValue,
                        MasterBrightness = item.Value.MasterBrightness,
                        Enabled = item.Value.Enabled
                    };
                }
            }

            return scheme;
        }
    }
}
