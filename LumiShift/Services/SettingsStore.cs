using System;
using System.IO;
using System.Web.Script.Serialization;
using LumiShift.Models;

namespace LumiShift.Services
{
    public class SettingsStore
    {
        private static readonly string SettingsFilePath =
            Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "settings.json");

        private static readonly JavaScriptSerializer SerializerInstance = new JavaScriptSerializer();
        private static readonly object SerializerLock = new object();

        public static JavaScriptSerializer Serializer => SerializerInstance;

        public static UserSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    UserSettings settings;
                    lock (SerializerLock)
                    {
                        settings = SerializerInstance.Deserialize<UserSettings>(json) ?? new UserSettings();
                    }
                    MigrateSettings(settings);
                    return settings;
                }
            }
            catch
            {
            }
            var defaults = new UserSettings();
            MigrateSettings(defaults);
            return defaults;
        }

        public static void SaveSettings(UserSettings settings)
        {
            try
            {
                string json;
                lock (SerializerLock)
                {
                    json = SerializerInstance.Serialize(settings);
                }
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
            }
        }

        private static void MigrateSettings(UserSettings settings)
        {
            if (settings.ScheduleSegments == null)
            {
                string nightStart = settings.ScheduleNightStart ?? "18:00";
                string nightEnd = settings.ScheduleNightEnd ?? "06:00";
                string dayPreset = settings.ScheduleDayPreset ?? "标准";
                string nightPreset = settings.ScheduleNightPreset ?? "护眼模式";

                settings.ScheduleSegments = new System.Collections.Generic.List<ScheduleSegment>
                {
                    new ScheduleSegment
                    {
                        StartTime = nightEnd,
                        EndTime = nightStart,
                        PresetName = dayPreset
                    },
                    new ScheduleSegment
                    {
                        StartTime = nightStart,
                        EndTime = nightEnd,
                        PresetName = nightPreset
                    }
                };
            }
        }
    }
}