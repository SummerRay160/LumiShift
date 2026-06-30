namespace LumiShift.Models
{
    public enum GammaSource
    {
        Global,
        Manual,
        Schedule,
        Preset
    }

    public class GammaConfig
    {
        public double RScale { get; set; } = 1.0;
        public double GScale { get; set; } = 1.0;
        public double BScale { get; set; } = 1.0;
        public double GammaValue { get; set; } = 1.0;
        public int MasterBrightness { get; set; } = 100;
        public bool Enabled { get; set; } = true;

        public PerDisplayGamma ToPerDisplayGamma(GammaSource source)
        {
            return new PerDisplayGamma
            {
                RScale = RScale,
                GScale = GScale,
                BScale = BScale,
                GammaValue = GammaValue,
                MasterBrightness = MasterBrightness,
                Enabled = Enabled,
                Source = GammaSourceNames.ToSettingsValue(source)
            };
        }

        public static GammaConfig FromSettings(UserSettings settings)
        {
            return new GammaConfig
            {
                RScale = settings.GammaRScale,
                GScale = settings.GammaGScale,
                BScale = settings.GammaBScale,
                GammaValue = settings.GammaValue,
                MasterBrightness = settings.MasterBrightness,
                Enabled = settings.GammaEnabled
            };
        }

        public static GammaConfig FromPreset(GammaPreset preset)
        {
            return new GammaConfig
            {
                RScale = preset.RScale,
                GScale = preset.GScale,
                BScale = preset.BScale,
                GammaValue = preset.GammaValue,
                MasterBrightness = preset.MasterBrightness,
                Enabled = preset.Enabled
            };
        }

        public static GammaConfig FromBuiltIn(BuiltInPresetInfo preset)
        {
            return new GammaConfig
            {
                RScale = preset.RScale,
                GScale = preset.GScale,
                BScale = preset.BScale,
                GammaValue = preset.GammaValue,
                MasterBrightness = preset.MasterBrightness,
                Enabled = preset.Enabled
            };
        }

        public void ApplyTo(UserSettings settings)
        {
            settings.GammaEnabled = Enabled;
            settings.GammaRScale = RScale;
            settings.GammaGScale = GScale;
            settings.GammaBScale = BScale;
            settings.GammaValue = GammaValue;
            settings.MasterBrightness = MasterBrightness;
        }
    }

    public static class GammaSourceNames
    {
        public const string Manual = "manual";
        public const string Schedule = "schedule";
        public const string Preset = "preset";

        public static string ToSettingsValue(GammaSource source)
        {
            switch (source)
            {
                case GammaSource.Schedule:
                    return Schedule;
                case GammaSource.Preset:
                    return Preset;
                case GammaSource.Manual:
                    return Manual;
                default:
                    return null;
            }
        }
    }
}
