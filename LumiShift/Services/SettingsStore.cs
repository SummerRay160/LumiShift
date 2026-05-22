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

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static UserSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    return Serializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
            }
            catch
            {
            }
            return new UserSettings();
        }

        public static void SaveSettings(UserSettings settings)
        {
            try
            {
                string json = Serializer.Serialize(settings);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
            }
        }
    }
}