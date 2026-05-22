using System;
using System.Collections.Generic;
using System.Linq;

namespace LumiShift.Models
{
    public class BuiltInPresetInfo
    {
        public string Name { get; set; }
        public double RScale { get; set; } = 1.0;
        public double GScale { get; set; } = 1.0;
        public double BScale { get; set; } = 1.0;
        public double GammaValue { get; set; } = 1.0;
        public int MasterBrightness { get; set; } = 100;
        public bool Enabled { get; set; } = true;

        public bool Matches(double r, double g, double b, double gv, int brightness = 100, double tolerance = 0.01)
        {
            return Math.Abs(RScale - r) < tolerance &&
                   Math.Abs(GScale - g) < tolerance &&
                   Math.Abs(BScale - b) < tolerance &&
                   Math.Abs(GammaValue - gv) < tolerance &&
                   Math.Abs(MasterBrightness - brightness) <= 1;
        }
    }

    public static class PresetDefinitions
    {
        public static readonly BuiltInPresetInfo[] BuiltIns = new[]
        {
            new BuiltInPresetInfo { Name = "标准", RScale = 1.0, GScale = 1.0, BScale = 1.0, GammaValue = 1.0, MasterBrightness = 100, Enabled = false },
            new BuiltInPresetInfo { Name = "防蓝光", RScale = 1.05, GScale = 1.0, BScale = 0.78, GammaValue = 1.0, MasterBrightness = 100, Enabled = true },
            new BuiltInPresetInfo { Name = "护眼模式", RScale = 1.08, GScale = 1.0, BScale = 0.70, GammaValue = 1.08, MasterBrightness = 100, Enabled = true },
            new BuiltInPresetInfo { Name = "游戏模式", RScale = 0.98, GScale = 1.0, BScale = 1.06, GammaValue = 0.92, MasterBrightness = 100, Enabled = true }
        };

        public static string[] GetNames()
        {
            return BuiltIns.Select(p => p.Name).ToArray();
        }

        public static bool IsBuiltIn(string name)
        {
            return BuiltIns.Any(p => p.Name == name);
        }

        public static BuiltInPresetInfo GetByName(string name)
        {
            return BuiltIns.FirstOrDefault(p => p.Name == name);
        }

        public static int IndexOf(string name)
        {
            for (int i = 0; i < BuiltIns.Length; i++)
            {
                if (BuiltIns[i].Name == name)
                    return i;
            }
            return -1;
        }

        public static bool TryResolveParams(string presetName, List<GammaPreset> customPresets,
            out double r, out double g, out double b, out double gv, out int mb, out bool en)
        {
            r = 1.0; g = 1.0; b = 1.0; gv = 1.0; mb = 100; en = true;

            var builtIn = GetByName(presetName);
            if (builtIn != null)
            {
                r = builtIn.RScale;
                g = builtIn.GScale;
                b = builtIn.BScale;
                gv = builtIn.GammaValue;
                mb = builtIn.MasterBrightness;
                en = builtIn.Enabled;
                return true;
            }

            var custom = customPresets?.FirstOrDefault(cp => cp.Name == presetName);
            if (custom != null)
            {
                r = custom.RScale;
                g = custom.GScale;
                b = custom.BScale;
                gv = custom.GammaValue;
                mb = custom.MasterBrightness;
                en = custom.Enabled;
                return true;
            }

            return false;
        }
    }
}
