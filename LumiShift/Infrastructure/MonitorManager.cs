using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Windows.Forms;

namespace LumiShift.Infrastructure
{
    public class MonitorInfo
    {
        public string DeviceId { get; set; }
        public string DisplayName { get; set; }
        public Screen Screen { get; set; }
        public IBrightnessController Controller { get; set; }
        public bool IsBuiltIn { get; set; }
    }

    public class MonitorManager : IDisposable
    {
        private List<MonitorInfo> _monitors;
        private readonly List<DdcBrightnessController> _ddcControllers;

        public IReadOnlyList<MonitorInfo> Monitors => _monitors.AsReadOnly();

        public MonitorManager()
        {
            _monitors = new List<MonitorInfo>();
            _ddcControllers = new List<DdcBrightnessController>();
            RefreshMonitors();
        }

        public void RefreshMonitors()
        {
            foreach (var ddc in _ddcControllers)
            {
                ddc.Dispose();
            }
            _ddcControllers.Clear();
            _monitors.Clear();

            var wmiMonitors = GetWmiMonitorDeviceIds();
            var screens = Screen.AllScreens;

            foreach (var screen in screens)
            {
                string deviceId = null;
                bool isBuiltIn = false;

                if (wmiMonitors.TryGetValue(screen.DeviceName, out var wmiInfo))
                {
                    deviceId = wmiInfo.deviceId;
                    isBuiltIn = wmiInfo.isBuiltIn;
                }
                else
                {
                    deviceId = screen.DeviceName;
                }

                string displayName = isBuiltIn ? "内置显示器" : $"外接显示器 ({screen.DeviceName})";

                IBrightnessController controller = null;

                if (isBuiltIn && deviceId != null)
                {
                    string instanceName = FindWmiInstanceName(deviceId);
                    if (instanceName != null)
                    {
                        controller = new WmiBrightnessController(instanceName, deviceId, displayName);
                    }
                }

                if (controller == null)
                {
                    var ddcController = new DdcBrightnessController(screen, deviceId ?? screen.DeviceName, displayName);
                    _ddcControllers.Add(ddcController);
                    controller = ddcController;
                }

                _monitors.Add(new MonitorInfo
                {
                    DeviceId = deviceId ?? screen.DeviceName,
                    DisplayName = displayName,
                    Screen = screen,
                    Controller = controller,
                    IsBuiltIn = isBuiltIn
                });
            }
        }

        private static Dictionary<string, (string deviceId, bool isBuiltIn)> GetWmiMonitorDeviceIds()
        {
            var result = new Dictionary<string, (string deviceId, bool isBuiltIn)>();
            int displayIndex = 1;
            var connectionParams = GetMonitorConnectionParams();

            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM WmiMonitorBasicDisplayParams"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string instanceName = mo["InstanceName"]?.ToString();
                        if (string.IsNullOrEmpty(instanceName))
                            continue;

                        string deviceId = instanceName;
                        if (instanceName.Contains("\\"))
                        {
                            deviceId = instanceName.Substring(0, instanceName.IndexOf('\\'));
                        }

                        string edidDeviceId = GetEdidDeviceId(instanceName);

                        bool isBuiltIn = IsBuiltInDisplay(instanceName, connectionParams);
                        string screenName = $"\\\\.\\DISPLAY{displayIndex++}";
                        result[screenName] = (edidDeviceId ?? deviceId, isBuiltIn);
                    }
                }
            }
            catch
            {
            }

            if (result.Count == 0)
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_DesktopMonitor"))
                    {
                        int idx = 1;
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            string pnpId = mo["PNPDeviceID"]?.ToString();
                            if (!string.IsNullOrEmpty(pnpId))
                            {
                                string screenName = $"\\\\.\\DISPLAY{idx}";
                                bool isBuiltIn = IsBuiltInDeviceId(pnpId);
                                result[screenName] = (pnpId, isBuiltIn);
                                idx++;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            if (result.Count == 0)
            {
                int idx = 1;
                foreach (Screen screen in Screen.AllScreens)
                {
                    result[screen.DeviceName] = (screen.DeviceName, idx == 1);
                    idx++;
                }
            }

            return result;
        }

        private static Dictionary<string, uint> GetMonitorConnectionParams()
        {
            var result = new Dictionary<string, uint>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM WmiMonitorConnectionParams"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string instanceName = mo["InstanceName"]?.ToString();
                        if (string.IsNullOrEmpty(instanceName)) continue;
                        uint videoTech = (uint)mo["VideoOutputTechnology"];
                        result[instanceName] = videoTech;
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        private static bool IsBuiltInDisplay(string instanceName, Dictionary<string, uint> connectionParams)
        {
            const uint VideoOutputTechnologyInternal = 0x80000000;

            string key = instanceName.Contains("\\")
                ? instanceName.Substring(0, instanceName.IndexOf('\\'))
                : instanceName;

            foreach (var kvp in connectionParams)
            {
                string connKey = kvp.Key.Contains("\\")
                    ? kvp.Key.Substring(0, kvp.Key.IndexOf('\\'))
                    : kvp.Key;

                if (string.Equals(key, connKey, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value == VideoOutputTechnologyInternal;
                }
            }

            return false;
        }

        private static bool IsBuiltInDeviceId(string pnpId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_DesktopMonitor WHERE PNPDeviceID = '{pnpId.Replace("'", "''")}'"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string monitorType = mo["MonitorType"]?.ToString() ?? "";
                        return monitorType.Contains("LCD") || monitorType.Contains("Internal");
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static string GetEdidDeviceId(string instanceName)
        {
            try
            {
                string escapedName = instanceName.Replace("\\", "\\\\").Replace("'", "''");

                using (var searcher = new ManagementObjectSearcher("root\\WMI",
                    $"SELECT * FROM WmiMonitorEDID WHERE InstanceName='{escapedName}'"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        byte[] edidData = mo["EDID"] as byte[];
                        if (edidData != null && edidData.Length >= 128)
                        {
                            string manufacturerId = System.Text.Encoding.ASCII.GetString(edidData, 0x08, 2);
                            ushort productCode = (ushort)(edidData[0x0B] << 8 | edidData[0x0A]);
                            return $"MONITOR\\{manufacturerId}{productCode:X4}";
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static string FindWmiInstanceName(string deviceId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM WmiMonitorBasicDisplayParams"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string instanceName = mo["InstanceName"]?.ToString();
                        if (!string.IsNullOrEmpty(instanceName) && instanceName.StartsWith(deviceId))
                        {
                            return instanceName;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public MonitorInfo GetMonitorByDeviceId(string deviceId)
        {
            return _monitors.FirstOrDefault(m =>
                m.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase));
        }

        public void ApplyBrightness(string deviceId, int brightness)
        {
            var monitor = GetMonitorByDeviceId(deviceId);
            if (monitor?.Controller != null && monitor.Controller.IsSupported)
            {
                monitor.Controller.SetBrightness(brightness);
            }
        }

        public Dictionary<string, int> GetCurrentBrightnessValues()
        {
            var result = new Dictionary<string, int>();
            foreach (var monitor in _monitors)
            {
                if (monitor.Controller != null && monitor.Controller.IsSupported)
                {
                    result[monitor.DeviceId] = monitor.Controller.GetBrightness();
                }
            }
            return result;
        }

        public void Dispose()
        {
            foreach (var ddc in _ddcControllers)
            {
                ddc.Dispose();
            }
            _ddcControllers.Clear();
        }
    }
}