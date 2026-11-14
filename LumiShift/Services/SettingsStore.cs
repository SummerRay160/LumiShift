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
            using (var op = Log.BeginOperation("SettingsLoad", $"path={SettingsPath}", LogLevel.Info))
            {
                try
                {
                    if (!File.Exists(SettingsPath))
                    {
                        op.Complete("Success", "配置文件不存在，使用默认设置");
                        return CreateDefaultSettings();
                    }

                    string json = File.ReadAllText(SettingsPath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        op.Complete("Success", "配置文件为空，使用默认设置");
                        return CreateDefaultSettings();
                    }

                    var settings = new JavaScriptSerializer().Deserialize<UserSettings>(json);
                    if (settings == null)
                    {
                        op.Complete("Success", "反序列化为空，使用默认设置");
                        return CreateDefaultSettings();
                    }

                    // 拿到配置后立即同步日志开关，使本次读取/迁移过程本身也能留痕
                    Log.SetEnabled(settings.DiagnosticsLoggingEnabled);

                    // 旧格式（旧版版本键名为 "_version"，反序列化后 Version 为 0）：备份原文件后按当前格式重存
                    if (settings.Version < CurrentSettingsVersion)
                    {
                        Log.Info("Settings", $"检测到旧格式配置 (v{settings.Version})，执行迁移");
                        MigrateFromLegacy(settings);
                    }

                    op.Complete("Success", $"version={settings.Version}, bytes={json.Length}");
                    return settings;
                }
                catch (Exception ex)
                {
                    op.Fail(ex, $"path={SettingsPath}",
                        () => $"exists={File.Exists(SettingsPath)}, length={(File.Exists(SettingsPath) ? new FileInfo(SettingsPath).Length : 0)}");
                    return CreateDefaultSettings();
                }
            }
        }

        public static void SaveSettings(UserSettings settings)
        {
            using (var op = Log.BeginOperation("SettingsSave", $"path={SettingsPath}, version={settings.Version}", LogLevel.Debug))
            {
                string json = null;
                string tmpPath = SettingsPath + ".tmp";
                try
                {
                    if (!Directory.Exists(SettingsDir))
                        Directory.CreateDirectory(SettingsDir);

                    settings.Version = CurrentSettingsVersion;
                    json = new JavaScriptSerializer().Serialize(settings);

                    // 先写临时文件再原子替换：进程中断最多留下 .tmp，不会损坏 settings.json
                    File.WriteAllText(tmpPath, json);
                    if (File.Exists(SettingsPath))
                        File.Replace(tmpPath, SettingsPath, null);
                    else
                        File.Move(tmpPath, SettingsPath);

                    op.Complete("Success", $"bytes={json.Length}");
                }
                catch (Exception ex)
                {
                    // 配置保存失败必须留痕，否则用户设置静默丢失无从排查
                    op.Fail(ex, $"path={SettingsPath}, bytes={json?.Length ?? 0}",
                        () => $"settingsExists={File.Exists(SettingsPath)}, tmpExists={File.Exists(tmpPath)}, logEnabled={Log.Enabled}, gammaEnabled={settings.GammaEnabled}");
                }
            }
        }

        private static void MigrateFromLegacy(UserSettings settings)
        {
            using (var op = Log.BeginOperation("SettingsMigrate", $"from=v{settings.Version}, backup={BackupPath}", LogLevel.Info))
            {
                try
                {
                    if (File.Exists(BackupPath))
                        File.Delete(BackupPath);
                    File.Move(SettingsPath, BackupPath);

                    SaveSettings(settings);
                    op.Complete("Success");
                }
                catch (Exception ex)
                {
                    op.Fail(ex, $"source={SettingsPath}, backup={BackupPath}");
                }
            }
        }

        /// <summary>删除 AppData 下的全部配置（目录连同 settings.json/.tmp/.bak 与 log.txt）。返回是否完整删除。</summary>
        public static bool DeleteAllConfig()
        {
            using (var op = Log.BeginOperation("ConfigDelete", $"path={SettingsDir}", LogLevel.Info))
            {
                try
                {
                    if (Directory.Exists(SettingsDir))
                        Directory.Delete(SettingsDir, true);
                    op.Complete("Success");
                    return true;
                }
                catch (Exception ex)
                {
                    op.Fail(ex, $"path={SettingsDir}", () => $"exists={Directory.Exists(SettingsDir)}");
                    return false;
                }
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
