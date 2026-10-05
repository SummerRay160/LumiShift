using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
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

                var settings = new JavaScriptSerializer().Deserialize<UserSettings>(json);
                if (settings == null)
                    return CreateDefaultSettings();

                // 拿到配置后立即同步日志开关，使本次读取/迁移过程本身也能留痕
                Log.SetEnabled(settings.DiagnosticsLoggingEnabled);

                // 旧格式（旧版版本键名为 "_version"，反序列化后 Version 为 0）：备份原文件后按当前格式重存
                if (settings.Version < CurrentSettingsVersion)
                {
                    Log.Info("Settings", $"检测到旧格式配置 (v{settings.Version})，执行迁移");
                    MigrateFromLegacy(settings);
                }

                Log.Info("Settings", $"配置加载成功 (v{settings.Version})");
                return settings;
            }
            catch (Exception ex)
            {
                // 读取失败会回退默认值并在下次保存时覆盖原配置，属关键故障：绕过开关强制留痕
                Log.Critical("Settings", ex);
                return CreateDefaultSettings();
            }
        }

        public static void SaveSettings(UserSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);

                settings.Version = CurrentSettingsVersion;
                string json = new JavaScriptSerializer().Serialize(settings);

                // 先写临时文件再原子替换：进程中断最多留下 .tmp，不会损坏 settings.json
                string tmpPath = SettingsPath + ".tmp";
                File.WriteAllText(tmpPath, json);
                if (File.Exists(SettingsPath))
                    File.Replace(tmpPath, SettingsPath, null);
                else
                    File.Move(tmpPath, SettingsPath);
            }
            catch (Exception ex)
            {
                // 配置保存失败必须留痕，否则用户设置静默丢失无从排查
                Log.Error("Settings", ex);
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
            catch (Exception ex)
            {
                Log.Error("Settings", ex);
            }
        }

        /// <summary>删除 AppData 下的全部配置（目录连同 settings.json/.tmp/.bak 与 log.txt）。返回是否完整删除。</summary>
        public static bool DeleteAllConfig()
        {
            try
            {
                if (Directory.Exists(SettingsDir))
                    Directory.Delete(SettingsDir, true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("SettingsStore", ex);
                return false;
            }
        }

        internal static UserSettings CreateDefaultSettings()
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
