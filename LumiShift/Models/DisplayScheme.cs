using System.Collections.Generic;

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

        public string KindText => Kind == DisplaySchemeKind.MultiDisplay ? "多屏方案" : "统一方案";
        public string DisplayName => $"{Name} · {KindText}";
    }
}
