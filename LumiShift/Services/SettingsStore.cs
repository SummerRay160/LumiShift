using System;
using System.Collections.Generic;
using System.IO;
using LumiShift.Infrastructure;
using LumiShift.Models;

namespace LumiShift.Services
{
    internal static class SettingsStore
    {
        private const int CurrentSettingsVersion = 1;

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LumiShift");

        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        private static readonly string BackupPath = Path.Combine(SettingsDir, "settings.json.v0.bak");

        public static UserSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return CreateDefaultSettings();

                string json = File.ReadAllText(SettingsPath);
                if (string.IsNullOrWhiteSpace(json))
                    return CreateDefaultSettings();

                using (var reader = new LightweightJsonReader(json))
                {
                    var root = reader.ReadObject();
                    if (root == null) return CreateDefaultSettings();

                    bool isLegacyFormat = !root.ContainsKey("_version");

                    var settings = DeserializeSettings(root);

                    if (isLegacyFormat)
                    {
                        MigrateFromLegacy(json, settings);
                    }

                    return settings;
                }
            }
            catch
            {
                return CreateDefaultSettings();
            }
        }

        public static void SaveSettings(UserSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);

                using (var writer = new LightweightJsonWriter())
                {
                    SerializeSettings(writer, settings);
                    string json = writer.ToString();
                    File.WriteAllText(SettingsPath, json);
                }
            }
            catch
            {
            }
        }

        private static void MigrateFromLegacy(string oldJson, UserSettings settings)
        {
            try
            {
                if (File.Exists(BackupPath))
                    File.Delete(BackupPath);
                File.Move(SettingsPath, BackupPath);

                SaveSettings(settings);
            }
            catch
            {
            }
        }

        private static UserSettings CreateDefaultSettings()
        {
            return new UserSettings
            {
                ScheduleSegments = new List<ScheduleSegment>(),
                CustomGammaPresets = new List<GammaPreset>(),
                BrightnessPerDisplay = new Dictionary<string, int>(),
                GammaPerDisplay = new Dictionary<string, PerDisplayGamma>(),
                GammaEnabled = true,
                MasterBrightness = 100,
                GammaValue = 1.0,
                GammaRScale = 1.0,
                GammaGScale = 1.0,
                GammaBScale = 1.0,
                ThemeMode = 2
            };
        }

        private static void SerializeSettings(LightweightJsonWriter w, UserSettings s)
        {
            w.WriteObjectStart();

            WriteProp(w, "_version", CurrentSettingsVersion); w.WriteComma();

            WriteProp(w, "StartWithWindows", s.StartWithWindows); w.WriteComma();
            WriteProp(w, "StartMinimized", s.StartMinimized); w.WriteComma();
            WriteProp(w, "EyeProtectionEnabled", s.EyeProtectionEnabled); w.WriteComma();
            WriteProp(w, "EyeProtectionRed", s.EyeProtectionRed); w.WriteComma();
            WriteProp(w, "EyeProtectionGreen", s.EyeProtectionGreen); w.WriteComma();
            WriteProp(w, "EyeProtectionBlue", s.EyeProtectionBlue); w.WriteComma();
            WriteProp(w, "ScheduleEnabled", s.ScheduleEnabled); w.WriteComma();
            WriteScheduleSegments(w, s.ScheduleSegments); w.WriteComma();
            WriteProp(w, "ScheduleNightStart", s.ScheduleNightStart ?? "18:00"); w.WriteComma();
            WriteProp(w, "ScheduleNightEnd", s.ScheduleNightEnd ?? "06:00"); w.WriteComma();
            WriteProp(w, "ScheduleDayPreset", s.ScheduleDayPreset ?? "标准"); w.WriteComma();
            WriteProp(w, "ScheduleNightPreset", s.ScheduleNightPreset ?? "护眼模式"); w.WriteComma();
            WriteProp(w, "GammaEnabled", s.GammaEnabled); w.WriteComma();
            WriteProp(w, "GammaRScale", s.GammaRScale); w.WriteComma();
            WriteProp(w, "GammaGScale", s.GammaGScale); w.WriteComma();
            WriteProp(w, "GammaBScale", s.GammaBScale); w.WriteComma();
            WriteProp(w, "GammaValue", s.GammaValue); w.WriteComma();
            WriteProp(w, "MasterBrightness", s.MasterBrightness); w.WriteComma();
            WriteProp(w, "ThemeMode", s.ThemeMode); w.WriteComma();
            WriteProp(w, "UseBackgroundImage", s.UseBackgroundImage); w.WriteComma();
            WriteProp(w, "BackgroundImageFile", s.BackgroundImageFile ?? ""); w.WriteComma();
            WriteProp(w, "BackgroundImageOpacity", s.BackgroundImageOpacity); w.WriteComma();
            WriteCustomGammaPresets(w, s.CustomGammaPresets); w.WriteComma();
            WriteBrightnessDict(w, s.BrightnessPerDisplay); w.WriteComma();
            WritePerDisplayGammaDict(w, s.GammaPerDisplay); w.WriteComma();
            WriteProp(w, "SkipVersion", s.SkipVersion ?? "");

            w.WriteObjectEnd();
        }

        private static void WriteProp(LightweightJsonWriter w, string key, string value)
        {
            w.WriteKey(key);
            w.WriteValue(value ?? "");
        }

        private static void WriteProp(LightweightJsonWriter w, string key, int value)
        {
            w.WriteKey(key);
            w.WriteValue(value);
        }

        private static void WriteProp(LightweightJsonWriter w, string key, double value)
        {
            w.WriteKey(key);
            w.WriteValue(value);
        }

        private static void WriteProp(LightweightJsonWriter w, string key, bool value)
        {
            w.WriteKey(key);
            w.WriteValue(value);
        }

        private static void WriteBrightnessDict(LightweightJsonWriter w, Dictionary<string, int> dict)
        {
            w.WriteKey("BrightnessPerDisplay");
            w.WriteObjectStart();
            bool first = true;
            if (dict != null)
            {
                foreach (var kv in dict)
                {
                    if (!first) w.WriteComma();
                    WriteProp(w, kv.Key, kv.Value);
                    first = false;
                }
            }
            w.WriteObjectEnd();
        }

        private static void WritePerDisplayGammaDict(LightweightJsonWriter w, Dictionary<string, PerDisplayGamma> dict)
        {
            w.WriteKey("GammaPerDisplay");
            w.WriteObjectStart();
            bool first = true;
            if (dict != null)
            {
                foreach (var kv in dict)
                {
                    if (!first) w.WriteComma();
                    w.WriteKey(kv.Key);
                    w.WriteObjectStart();
                    WriteProp(w, "RScale", kv.Value.RScale); w.WriteComma();
                    WriteProp(w, "GScale", kv.Value.GScale); w.WriteComma();
                    WriteProp(w, "BScale", kv.Value.BScale); w.WriteComma();
                    WriteProp(w, "GammaValue", kv.Value.GammaValue); w.WriteComma();
                    WriteProp(w, "MasterBrightness", kv.Value.MasterBrightness); w.WriteComma();
                    WriteProp(w, "Enabled", kv.Value.Enabled); w.WriteComma();
                    WriteProp(w, "Source", kv.Value.Source ?? "");
                    w.WriteObjectEnd();
                    first = false;
                }
            }
            w.WriteObjectEnd();
        }

        private static void WriteCustomGammaPresets(LightweightJsonWriter w, List<GammaPreset> list)
        {
            w.WriteKey("CustomGammaPresets");
            w.WriteArrayStart();
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) w.WriteComma();
                    var p = list[i];
                    w.WriteObjectStart();
                    WriteProp(w, "Name", p.Name ?? ""); w.WriteComma();
                    WriteProp(w, "RScale", p.RScale); w.WriteComma();
                    WriteProp(w, "GScale", p.GScale); w.WriteComma();
                    WriteProp(w, "BScale", p.BScale); w.WriteComma();
                    WriteProp(w, "GammaValue", p.GammaValue); w.WriteComma();
                    WriteProp(w, "MasterBrightness", p.MasterBrightness); w.WriteComma();
                    WriteProp(w, "Enabled", p.Enabled); w.WriteComma();
                    WritePerDisplaySnapshot(w, p.PerDisplaySnapshot);
                    w.WriteObjectEnd();
                }
            }
            w.WriteArrayEnd();
        }

        private static void WritePerDisplaySnapshot(LightweightJsonWriter w, Dictionary<string, PerDisplayGamma> dict)
        {
            w.WriteKey("PerDisplaySnapshot");
            if (dict == null)
            {
                w.WriteNull();
                return;
            }
            w.WriteObjectStart();
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) w.WriteComma();
                w.WriteKey(kv.Key);
                w.WriteObjectStart();
                WriteProp(w, "RScale", kv.Value.RScale); w.WriteComma();
                WriteProp(w, "GScale", kv.Value.GScale); w.WriteComma();
                WriteProp(w, "BScale", kv.Value.BScale); w.WriteComma();
                WriteProp(w, "GammaValue", kv.Value.GammaValue); w.WriteComma();
                WriteProp(w, "MasterBrightness", kv.Value.MasterBrightness); w.WriteComma();
                WriteProp(w, "Enabled", kv.Value.Enabled); w.WriteComma();
                WriteProp(w, "Source", kv.Value.Source ?? "");
                w.WriteObjectEnd();
                first = false;
            }
            w.WriteObjectEnd();
        }

        private static void WriteScheduleSegments(LightweightJsonWriter w, List<ScheduleSegment> segments)
        {
            w.WriteKey("ScheduleSegments");
            if (segments == null)
            {
                w.WriteNull();
                return;
            }
            w.WriteArrayStart();
            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0) w.WriteComma();
                var seg = segments[i];
                w.WriteObjectStart();
                WriteProp(w, "StartTime", seg.StartTime ?? ""); w.WriteComma();
                WriteProp(w, "EndTime", seg.EndTime ?? ""); w.WriteComma();
                WriteProp(w, "PresetName", seg.PresetName ?? ""); w.WriteComma();
                WriteSyncMode(w, seg.SyncMode); w.WriteComma();
                WriteMonitorPresets(w, seg.MonitorPresets);
                w.WriteObjectEnd();
            }
            w.WriteArrayEnd();
        }

        private static void WriteSyncMode(LightweightJsonWriter w, bool? syncMode)
        {
            w.WriteKey("SyncMode");
            if (syncMode.HasValue)
                w.WriteValue(syncMode.Value);
            else
                w.WriteNull();
        }

        private static void WriteMonitorPresets(LightweightJsonWriter w, Dictionary<string, string> dict)
        {
            w.WriteKey("MonitorPresets");
            if (dict == null)
            {
                w.WriteNull();
                return;
            }
            w.WriteObjectStart();
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) w.WriteComma();
                WriteProp(w, kv.Key, kv.Value ?? "");
                first = false;
            }
            w.WriteObjectEnd();
        }

        private static UserSettings DeserializeSettings(Dictionary<string, object> root)
        {
            var s = CreateDefaultSettings();

            s.StartWithWindows = GetBool(root, "StartWithWindows");
            s.StartMinimized = GetBool(root, "StartMinimized");
            s.EyeProtectionEnabled = GetBool(root, "EyeProtectionEnabled");
            s.EyeProtectionRed = GetInt(root, "EyeProtectionRed", 204);
            s.EyeProtectionGreen = GetInt(root, "EyeProtectionGreen", 232);
            s.EyeProtectionBlue = GetInt(root, "EyeProtectionBlue", 207);
            s.ScheduleEnabled = GetBool(root, "ScheduleEnabled");
            s.ScheduleNightStart = GetString(root, "ScheduleNightStart") ?? "18:00";
            s.ScheduleNightEnd = GetString(root, "ScheduleNightEnd") ?? "06:00";
            s.ScheduleDayPreset = GetString(root, "ScheduleDayPreset") ?? "标准";
            s.ScheduleNightPreset = GetString(root, "ScheduleNightPreset") ?? "护眼模式";
            s.GammaEnabled = GetBool(root, "GammaEnabled");
            s.GammaRScale = GetDouble(root, "GammaRScale", 1.0);
            s.GammaGScale = GetDouble(root, "GammaGScale", 1.0);
            s.GammaBScale = GetDouble(root, "GammaBScale", 1.0);
            s.GammaValue = GetDouble(root, "GammaValue", 1.0);
            s.MasterBrightness = GetInt(root, "MasterBrightness", 100);
            s.ThemeMode = GetInt(root, "ThemeMode", 2);
            s.UseBackgroundImage = GetBool(root, "UseBackgroundImage");
            s.BackgroundImageFile = GetString(root, "BackgroundImageFile") ?? "";
            s.BackgroundImageOpacity = GetInt(root, "BackgroundImageOpacity", 30);
            s.SkipVersion = GetString(root, "SkipVersion") ?? "";

            s.ScheduleSegments = DeserializeScheduleSegments(root);
            s.CustomGammaPresets = DeserializeCustomGammaPresets(root);
            s.BrightnessPerDisplay = DeserializeBrightnessDict(root);
            s.GammaPerDisplay = DeserializePerDisplayGammaDict(root);

            return s;
        }

        private static List<ScheduleSegment> DeserializeScheduleSegments(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("ScheduleSegments", out var val) || val == null)
                return new List<ScheduleSegment>();

            var result = new List<ScheduleSegment>();
            if (val is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object> d)
                    {
                        var seg = new ScheduleSegment
                        {
                            StartTime = GetString(d, "StartTime") ?? "06:00",
                            EndTime = GetString(d, "EndTime") ?? "18:00",
                            PresetName = GetString(d, "PresetName") ?? "标准",
                            SyncMode = GetNullableBool(d, "SyncMode"),
                            MonitorPresets = DeserializeStringDict(d, "MonitorPresets")
                        };
                        result.Add(seg);
                    }
                }
            }
            return result;
        }

        private static List<GammaPreset> DeserializeCustomGammaPresets(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("CustomGammaPresets", out var val) || val == null)
                return new List<GammaPreset>();

            var result = new List<GammaPreset>();
            if (val is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object> d)
                    {
                        double rScale = GetDouble(d, "RScale", 1.0);
                        double gScale = GetDouble(d, "GScale", 1.0);
                        double bScale = GetDouble(d, "BScale", 1.0);
                        double gammaValue = GetDouble(d, "GammaValue", 1.0);
                        int masterBrightness = GetInt(d, "MasterBrightness", 100);

                        var preset = new GammaPreset
                        {
                            Name = GetString(d, "Name") ?? "",
                            RScale = rScale,
                            GScale = gScale,
                            BScale = bScale,
                            GammaValue = gammaValue,
                            MasterBrightness = masterBrightness,
                            Enabled = GetBool(d, "Enabled"),
                            PerDisplaySnapshot = DeserializePerDisplayGammaDict(d, "PerDisplaySnapshot")
                        };
                        result.Add(preset);
                    }
                }
            }
            return result;
        }

        private static Dictionary<string, int> DeserializeBrightnessDict(Dictionary<string, object> root)
        {
            var result = new Dictionary<string, int>();
            if (root.TryGetValue("BrightnessPerDisplay", out var val) && val is Dictionary<string, object> d)
            {
                foreach (var kv in d)
                    result[kv.Key] = ConvertToInt(kv.Value);
            }
            return result;
        }

        private static Dictionary<string, PerDisplayGamma> DeserializePerDisplayGammaDict(
            Dictionary<string, object> root, string key = "GammaPerDisplay")
        {
            var result = new Dictionary<string, PerDisplayGamma>();
            if (root.TryGetValue(key, out var val) && val is Dictionary<string, object> outer)
            {
                foreach (var kv in outer)
                {
                    if (kv.Value is Dictionary<string, object> inner)
                    {
                        result[kv.Key] = new PerDisplayGamma
                        {
                            RScale = GetDouble(inner, "RScale", 1.0),
                            GScale = GetDouble(inner, "GScale", 1.0),
                            BScale = GetDouble(inner, "BScale", 1.0),
                            GammaValue = GetDouble(inner, "GammaValue", 1.0),
                            MasterBrightness = GetInt(inner, "MasterBrightness", 100),
                            Enabled = GetBool(inner, "Enabled"),
                            Source = GetString(inner, "Source")
                        };
                    }
                }
            }
            return result;
        }

        private static Dictionary<string, string> DeserializeStringDict(Dictionary<string, object> parent, string key)
        {
            var result = new Dictionary<string, string>();
            if (parent.TryGetValue(key, out var val) && val is Dictionary<string, object> d)
            {
                foreach (var kv in d)
                    result[kv.Key] = kv.Value?.ToString() ?? "";
            }
            return result;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var val))
                return val as string ?? val?.ToString();
            return null;
        }

        private static int GetInt(Dictionary<string, object> dict, string key, int defaultValue = 0)
        {
            return ConvertToInt(dict.TryGetValue(key, out var val) ? val : null, defaultValue);
        }

        private static double GetDouble(Dictionary<string, object> dict, string key, double defaultValue = 1.0)
        {
            return ConvertToDouble(dict.TryGetValue(key, out var val) ? val : null, defaultValue);
        }

        private static bool GetBool(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var val))
                return ConvertToBool(val);
            return false;
        }

        private static bool? GetNullableBool(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var val) || val == null)
                return null;
            return ConvertToBool(val);
        }

        private static int ConvertToInt(object value, int defaultValue = 0)
        {
            if (value is int i) return i;
            if (value is double d) return (int)d;
            if (value is long l) return (int)l;
            if (value is string s && int.TryParse(s, out var si)) return si;
            return defaultValue;
        }

        private static double ConvertToDouble(object value, double defaultValue = 1.0)
        {
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is string s && double.TryParse(s,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var sd))
                return sd;
            return defaultValue;
        }

        private static bool ConvertToBool(object value)
        {
            if (value is bool b) return b;
            if (value is string s && bool.TryParse(s, out var sb)) return sb;
            return false;
        }
    }
}