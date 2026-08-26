using System.Collections.Generic;
using LumiShift.Services;

namespace LumiShift.Models
{
    public enum DisplaySchemeKind
    {
        Unified,
        MultiDisplay
    }

    public class DisplayScheme
    {
        public string Name { get; set; }
        public DisplaySchemeKind Kind { get; set; }
        public GammaConfig UnifiedConfig { get; set; }
        public Dictionary<string, GammaConfig> DisplayConfigs { get; set; }
        public bool IsBuiltIn { get; set; }

        public string DisplayName => $"{Lang.Get(Name)} · {(Kind == DisplaySchemeKind.MultiDisplay ? Lang.Get("多屏方案") : Lang.Get("统一方案"))}";
    }
}
