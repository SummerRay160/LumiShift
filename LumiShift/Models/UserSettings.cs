using System.Collections.Generic;

namespace LumiShift.Models
{
    public class GammaPreset
    {
        public string Name { get; set; }
        public double RScale { get; set; } = 1.0;
        public double GScale { get; set; } = 1.0;
        public double BScale { get; set; } = 1.0;
        public double GammaValue { get; set; } = 1.0;
        public int MasterBrightness { get; set; } = 100;
        public bool Enabled { get; set; } = true;
        public Dictionary<string, PerDisplayGamma> PerDisplaySnapshot { get; set; }
    }

    public class ScheduleSegment
    {
        public string StartTime { get; set; } = "06:00";
        public string EndTime { get; set; } = "18:00";
        public string PresetName { get; set; } = "标准";
        public bool? SyncMode { get; set; }
        public Dictionary<string, string> MonitorPresets { get; set; }
    }

    public class PerDisplayGamma
    {
        public double RScale { get; set; } = 1.0;
        public double GScale { get; set; } = 1.0;
        public double BScale { get; set; } = 1.0;
        public double GammaValue { get; set; } = 1.0;
        public int MasterBrightness { get; set; } = 100;
        public bool Enabled { get; set; } = true;
        public string Source { get; set; }
    }

    public class UserSettings
    {
        public bool StartWithWindows { get; set; }
        public bool StartMinimized { get; set; }

        public bool EyeProtectionEnabled { get; set; }
        public int EyeProtectionRed { get; set; } = 204;
        public int EyeProtectionGreen { get; set; } = 232;
        public int EyeProtectionBlue { get; set; } = 207;

        public bool ScheduleEnabled { get; set; }
        public List<ScheduleSegment> ScheduleSegments { get; set; }

        public string ScheduleNightStart { get; set; } = "18:00";
        public string ScheduleNightEnd { get; set; } = "06:00";
        public string ScheduleDayPreset { get; set; } = "标准";
        public string ScheduleNightPreset { get; set; } = "护眼模式";

        public bool GammaEnabled { get; set; }
        public double GammaRScale { get; set; } = 1.0;
        public double GammaGScale { get; set; } = 1.0;
        public double GammaBScale { get; set; } = 1.0;
        public double GammaValue { get; set; } = 1.0;
        public int MasterBrightness { get; set; } = 100;

        public int ThemeMode { get; set; } = 2;

        public bool UseBackgroundImage { get; set; }
        public string BackgroundImageFile { get; set; } = "";
        public int BackgroundImageOpacity { get; set; } = 30;

        public List<GammaPreset> CustomGammaPresets { get; set; } = new List<GammaPreset>();

        public Dictionary<string, int> BrightnessPerDisplay { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, PerDisplayGamma> GammaPerDisplay { get; set; } = new Dictionary<string, PerDisplayGamma>();

        public string SkipVersion { get; set; } = "";

        public bool RestoreGammaOnExit { get; set; } = true;
    }
}