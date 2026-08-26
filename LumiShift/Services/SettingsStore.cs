using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
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

                var settings = new JavaScriptSerializer().Deserialize<UserSettings>(json);
                if (settings == null)
                    return CreateDefaultSettings();

                // 旧格式（无 _version）：备份原文件后按当前格式重存
                if (settings._version < CurrentSettingsVersion)
                    MigrateFromLegacy(settings);

                return settings;
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

                settings._version = CurrentSettingsVersion;
                string json = new JavaScriptSerializer().Serialize(settings);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
            }
        }

        private static void MigrateFromLegacy(UserSettings settings)
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
                AutoCheckUpdates = true,
                NotificationsEnabled = true,
                NotifyStartup = true,
                NotifyScheduleSwitch = true,
                NotifyStatusSwitch = true,
                NotifyMonitorChange = true
            };
        }
    }
}
