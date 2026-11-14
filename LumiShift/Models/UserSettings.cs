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
        /// <summary>设置文件格式版本。旧版键名为 "_version"，反序列化后为 0，会触发一次性迁移重存。</summary>
        public int Version { get; set; }
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

        /// <summary>
        /// Gamma 亮度低于 Windows 允许的下限时，是否联动硬件背光继续调暗。
        /// 默认关闭：只调 Gamma，绝不改动显示器硬件亮度，避免与"显示器亮度调节"页互相覆盖。
        /// </summary>
        public bool GammaHardwareAssistEnabled { get; set; }

        public bool UseBackgroundImage { get; set; }
        public string BackgroundImageFile { get; set; } = "";
        public int BackgroundImageOpacity { get; set; } = 30;

        public List<GammaPreset> CustomGammaPresets { get; set; } = new List<GammaPreset>();

        public Dictionary<string, int> BrightnessPerDisplay { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, PerDisplayGamma> GammaPerDisplay { get; set; } = new Dictionary<string, PerDisplayGamma>();

        public string SkipVersion { get; set; } = "";

        public bool AutoCheckUpdates { get; set; } = true;

        public bool RestoreGammaOnExit { get; set; } = true;
        public string Language { get; set; } = "";

        /// <summary>诊断文件日志开关，默认关闭；开启后写 %LocalAppData%\LumiShift\log.txt。</summary>
        public bool DiagnosticsLoggingEnabled { get; set; }

        /// <summary>内存维护开关（实验性），默认关闭；开启后周期性检查并压缩托管堆、修剪工作集。</summary>
        public bool MemoryMaintenanceEnabled { get; set; }

        public bool NotificationsEnabled { get; set; } = true;
        public bool NotifyStartup { get; set; } = true;
        public bool NotifyScheduleSwitch { get; set; } = true;
        public bool NotifyStatusSwitch { get; set; } = true;
        public bool NotifyMonitorChange { get; set; } = true;
    }
}
