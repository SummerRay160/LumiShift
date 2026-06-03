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
        private readonly List<IBrightnessController> _allControllers;

        public IReadOnlyList<MonitorInfo> Monitors => _monitors.AsReadOnly();

        public event Action MonitorsChanged;

        public MonitorManager()
        {
            _monitors = new List<MonitorInfo>();
            _allControllers = new List<IBrightnessController>();
            RefreshMonitors();
        }

        public HashSet<string> RefreshMonitors()
        {
            var oldDeviceIds = new HashSet<string>(_monitors.Select(m => m.DeviceId));

            foreach (var ctrl in _allControllers)
            {
                try { (ctrl as IDisposable)?.Dispose(); }
                catch { }
            }
            _allControllers.Clear();
            _monitors.Clear();

            var wmiMonitors = GetWmiMonitorDetails();
            var screens = Screen.AllScreens;

            foreach (var screen in screens)
            {
                string deviceId = null;
                bool isBuiltIn = false;
                string monitorName = null;
                string manufacturerCode = null;

                if (wmiMonitors.TryGetValue(screen.DeviceName, out var wmiInfo))
                {
                    deviceId = wmiInfo.deviceId;
                    isBuiltIn = wmiInfo.isBuiltIn;
                    monitorName = wmiInfo.monitorName;
                    manufacturerCode = wmiInfo.manufacturerCode;
                }
                else
                {
                    deviceId = screen.DeviceName;
                }

                string displayName = BuildDisplayName(isBuiltIn, monitorName, manufacturerCode, screen, screens);

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
                    controller = ddcController;
                }

                _allControllers.Add(controller);

                _monitors.Add(new MonitorInfo
                {
                    DeviceId = deviceId ?? screen.DeviceName,
                    DisplayName = displayName,
                    Screen = screen,
                    Controller = controller,
                    IsBuiltIn = isBuiltIn
                });
            }

            var newDeviceIds = new HashSet<string>(_monitors.Select(m => m.DeviceId));
            var removedDeviceIds = new HashSet<string>(oldDeviceIds);
            removedDeviceIds.ExceptWith(newDeviceIds);

            OnMonitorsChanged();

            return removedDeviceIds;
        }

        private static string BuildDisplayName(bool isBuiltIn, string monitorName, string manufacturerCode, Screen screen, Screen[] allScreens)
        {
            if (isBuiltIn)
            {
                return screen.Primary ? "内置显示器 (主)" : "内置显示器";
            }

            string position = InferScreenPosition(screen, allScreens);
            string vendorName = ResolveManufacturerName(manufacturerCode);

            if (!string.IsNullOrWhiteSpace(monitorName))
            {
                string prefix = !string.IsNullOrWhiteSpace(vendorName) ? vendorName + " " : "";
                return $"{prefix}{monitorName} ({position})";
            }

            if (!string.IsNullOrWhiteSpace(vendorName))
            {
                return $"{vendorName} 外接显示器 ({position})";
            }

            int index = Array.IndexOf(allScreens, screen) + 1;
            return $"外接显示器 #{index} ({position})";
        }

        private static string InferScreenPosition(Screen screen, Screen[] allScreens)
        {
            if (screen.Primary)
                return "主";

            if (allScreens.Length <= 1)
                return "主";

            int centerX = screen.Bounds.X + screen.Bounds.Width / 2;
            int primaryCenterX = 0;

            foreach (var s in allScreens)
            {
                if (s.Primary)
                {
                    primaryCenterX = s.Bounds.X + s.Bounds.Width / 2;
                    break;
                }
            }

            int centerY = screen.Bounds.Y + screen.Bounds.Height / 2;
            int primaryCenterY = 0;

            foreach (var s in allScreens)
            {
                if (s.Primary)
                {
                    primaryCenterY = s.Bounds.Y + s.Bounds.Height / 2;
                    break;
                }
            }

            bool leftRight = Math.Abs(centerX - primaryCenterX) > Math.Abs(centerY - primaryCenterY);

            if (leftRight)
            {
                return centerX < primaryCenterX ? "左侧" : "右侧";
            }
            else
            {
                return centerY < primaryCenterY ? "上方" : "下方";
            }
        }

        private static readonly Dictionary<string, string> ManufacturerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"DEL", "Dell"},
            {"SAM", "Samsung"},
            {"GSM", "LG"},
            {"APP", "Apple"},
            {"ACI", "ASUS"},
            {"AOC", "AOC"},
            {"BNQ", "BenQ"},
            {"CMN", "Chimei"},
            {"ENC", "EIZO"},
            {"FNI", "Fujitsu"},
            {"GGL", "Google"},
            {"HAI", "Haier"},
            {"HSD", "HannStar"},
            {"HEI", "Hyundai"},
            {"HKC", "HKC"},
            {"HPN", "HP"},
            {"HSL", "Hansol"},
            {"IVO", "InfoVision"},
            {"LEN", "Lenovo"},
            {"LNX", "Linux"},
            {"MAG", "MAG"},
            {"MEI", "Panasonic"},
            {"MEL", "Mitsubishi"},
            {"NEC", "NEC"},
            {"NEX", "Nexgen"},
            {"OQI", "OPTIQUEST"},
            {"PHL", "Philips"},
            {"PIO", "Pioneer"},
            {"PNR", "Planar"},
            {"QDS", "Quanta"},
            {"RAT", "Acer"},
            {"SEC", "Samsung"},
            {"SHP", "Sharp"},
            {"SII", "Silicon Image"},
            {"SIS", "Silicon Integrated"},
            {"SMI", "Smile International"},
            {"SNI", "Siemens Nixdorf"},
            {"SNY", "Sony"},
            {"SPT", "Sceptre Tech"},
            {"SRC", "Shamrock"},
            {"STN", "Samsung"},
            {"SUN", "Sun"},
            {"TAT", "Tatung"},
            {"TOS", "Toshiba"},
            {"TSB", "Toshiba"},
            {"UNM", "Unisys"},
            {"VES", "Vestel"},
            {"VIT", "Visitech"},
            {"VSC", "ViewSonic"},
            {"WDE", "Westinghouse"},
            {"ZCM", "Zenith"},
        };

        private static string ResolveManufacturerName(string manufacturerCode)
        {
            if (string.IsNullOrWhiteSpace(manufacturerCode))
                return null;

            if (ManufacturerMap.TryGetValue(manufacturerCode, out string name))
                return name;

            return manufacturerCode.Length == 3 ? manufacturerCode.ToUpperInvariant() : null;
        }

        private static string DecodeEdidManufacturerId(byte b1, byte b2)
        {
            char c1 = (char)('@' + ((b1 >> 2) & 0x1F));
            char c2 = (char)('@' + (((b1 & 0x03) << 3) | ((b2 >> 5) & 0x07)));
            char c3 = (char)('@' + (b2 & 0x1F));
            return new string(new[] { c1, c2, c3 });
        }

        private static string ParseEdidMonitorName(byte[] edidData)
        {
            if (edidData == null || edidData.Length < 128)
                return null;

            for (int block = 0; block < 4; block++)
            {
                int offset = 54 + block * 18;

                if (offset + 18 > edidData.Length)
                    break;

                if (edidData[offset] == 0x00 && edidData[offset + 1] == 0x00 && edidData[offset + 2] == 0xFC)
                {
                    var chars = new List<char>();
                    for (int i = offset + 5; i < offset + 18; i++)
                    {
                        byte b = edidData[i];
                        if (b == 0x0A || b == 0x00)
                            break;
                        if (b >= 0x20 && b <= 0x7E)
                            chars.Add((char)b);
                    }

                    string name = new string(chars.ToArray()).Trim();
                    return string.IsNullOrWhiteSpace(name) ? null : name;
                }
            }

            return null;
        }

        private static Dictionary<string, string> GetDisplayToMonitorPnpMap()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            uint adapterIndex = 0;
            var adapter = new NativeMethods.DISPLAY_DEVICE();
            adapter.cb = System.Runtime.InteropServices.Marshal.SizeOf(adapter);

            while (NativeMethods.EnumDisplayDevices(null, adapterIndex, ref adapter, 0))
            {
                if ((adapter.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                {
                    uint monitorIndex = 0;
                    var monitor = new NativeMethods.DISPLAY_DEVICE();
                    monitor.cb = System.Runtime.InteropServices.Marshal.SizeOf(monitor);

                    while (NativeMethods.EnumDisplayDevices(adapter.DeviceName, monitorIndex, ref monitor, 0))
                    {
                        if ((monitor.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0
                            && !string.IsNullOrEmpty(monitor.DeviceID))
                        {
                            result[adapter.DeviceName] = monitor.DeviceID;
                            break;
                        }
                        monitorIndex++;
                        monitor.cb = System.Runtime.InteropServices.Marshal.SizeOf(monitor);
                    }
                }

                adapterIndex++;
                adapter.cb = System.Runtime.InteropServices.Marshal.SizeOf(adapter);
            }

            return result;
        }

        private static string ExtractPnpInstancePath(string pnpDeviceId)
        {
            string path = pnpDeviceId;
            if (path.StartsWith("MONITOR\\", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("MONITOR\\".Length);
            int lastSlash = path.LastIndexOf('\\');
            if (lastSlash > 0)
                path = path.Substring(0, lastSlash);
            return path;
        }

        private static string ExtractWmiInstancePath(string instanceName)
        {
            int underscore = instanceName.LastIndexOf('_');
            if (underscore > 0)
                return instanceName.Substring(0, underscore);
            return instanceName;
        }

        private static Dictionary<string, (string deviceId, bool isBuiltIn, string monitorName, string manufacturerCode)> GetWmiMonitorDetails()
        {
            var result = new Dictionary<string, (string deviceId, bool isBuiltIn, string monitorName, string manufacturerCode)>(StringComparer.OrdinalIgnoreCase);
            var connectionParams = GetMonitorConnectionParams();
            var displayToPnpMap = GetDisplayToMonitorPnpMap();

            var wmiMonitors = new List<(string instanceName, string deviceId, bool isBuiltIn, string monitorName, string manufacturerCode)>();

            ManagementObjectCollection collection = null;
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM WmiMonitorBasicDisplayParams"))
                using (collection = searcher.Get())
                {
                    foreach (ManagementObject mo in collection)
                    {
                        try
                        {
                            string instanceName = mo["InstanceName"]?.ToString();
                            if (string.IsNullOrEmpty(instanceName))
                                continue;

                            string deviceId = instanceName;
                            if (instanceName.Contains("\\"))
                                deviceId = instanceName.Substring(0, instanceName.IndexOf('\\'));

                            string edidDeviceId = null;
                            string monitorName = null;
                            string manufacturerCode = null;

                            var edidInfo = GetEdidDetails(instanceName);
                            if (edidInfo != null)
                            {
                                edidDeviceId = edidInfo.Value.deviceId;
                                monitorName = edidInfo.Value.monitorName;
                                manufacturerCode = edidInfo.Value.manufacturerCode;
                            }

                            bool isBuiltIn = IsBuiltInDisplay(instanceName, connectionParams);
                            wmiMonitors.Add((instanceName, edidDeviceId ?? deviceId, isBuiltIn, monitorName, manufacturerCode));
                        }
                        catch
                        {
                        }
                        finally
                        {
                            mo?.Dispose();
                        }
                    }
                }
            }
            catch
            {
            }

            foreach (var displayKvp in displayToPnpMap)
            {
                string screenName = displayKvp.Key;
                string monitorPnpId = displayKvp.Value;
                string pnpPath = ExtractPnpInstancePath(monitorPnpId);

                int matchIdx = -1;
                for (int i = 0; i < wmiMonitors.Count; i++)
                {
                    string wmiPath = ExtractWmiInstancePath(wmiMonitors[i].instanceName);
                    if (string.Equals(pnpPath, wmiPath, StringComparison.OrdinalIgnoreCase))
                    {
                        matchIdx = i;
                        break;
                    }
                }

                if (matchIdx < 0)
                {
                    string pnpHardwareId = pnpPath.Contains("\\")
                        ? pnpPath.Substring(0, pnpPath.IndexOf('\\'))
                        : pnpPath;

                    for (int i = 0; i < wmiMonitors.Count; i++)
                    {
                        string wmiHardwareId = wmiMonitors[i].instanceName.Contains("\\")
                            ? wmiMonitors[i].instanceName.Substring(0, wmiMonitors[i].instanceName.IndexOf('\\'))
                            : wmiMonitors[i].instanceName;

                        if (string.Equals(pnpHardwareId, wmiHardwareId, StringComparison.OrdinalIgnoreCase))
                        {
                            matchIdx = i;
                            break;
                        }
                    }
                }

                if (matchIdx < 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[MonitorManager] WMI 显示器匹配失败: screen={screenName}, pnpPath={pnpPath}");
                }

                if (matchIdx >= 0)
                {
                    var wm = wmiMonitors[matchIdx];
                    result[screenName] = (wm.deviceId, wm.isBuiltIn, wm.monitorName, wm.manufacturerCode);
                    wmiMonitors.RemoveAt(matchIdx);
                }
            }

            if (wmiMonitors.Count > 0)
            {
                var assignedScreenNames = new HashSet<string>(result.Keys, StringComparer.OrdinalIgnoreCase);
                var unassignedScreens = Screen.AllScreens
                    .Where(s => !assignedScreenNames.Contains(s.DeviceName))
                    .ToList();

                for (int i = 0; i < wmiMonitors.Count && i < unassignedScreens.Count; i++)
                {
                    var wm = wmiMonitors[i];
                    result[unassignedScreens[i].DeviceName] = (wm.deviceId, wm.isBuiltIn, wm.monitorName, wm.manufacturerCode);
                }
            }

            if (result.Count == 0)
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_DesktopMonitor"))
                    {
                        using (var desktopCollection = searcher.Get())
                        {
                            var desktopMonitors = new List<(string pnpId, bool isBuiltIn)>();
                            foreach (ManagementObject mo in desktopCollection)
                            {
                                try
                                {
                                    string pnpId = mo["PNPDeviceID"]?.ToString();
                                    if (!string.IsNullOrEmpty(pnpId))
                                    {
                                        bool isBuiltIn = IsBuiltInDeviceId(pnpId);
                                        desktopMonitors.Add((pnpId, isBuiltIn));
                                    }
                                }
                                catch
                                {
                                }
                                finally
                                {
                                    mo.Dispose();
                                }
                            }

                            foreach (var screen in Screen.AllScreens)
                            {
                                if (displayToPnpMap.TryGetValue(screen.DeviceName, out var pnpId))
                                {
                                    var dm = desktopMonitors.FirstOrDefault(d =>
                                        pnpId.IndexOf(d.pnpId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        d.pnpId.IndexOf(pnpId, StringComparison.OrdinalIgnoreCase) >= 0);
                                    result[screen.DeviceName] = (dm.pnpId ?? screen.DeviceName, dm.isBuiltIn, null, null);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[MonitorManager] Win32_DesktopMonitor fallback: {screen.DeviceName} 未匹配到 PnP ID，标记为外接显示器 (isBuiltIn=false)");
                                    result[screen.DeviceName] = (screen.DeviceName, false, null, null);
                                }
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
                System.Diagnostics.Debug.WriteLine("[MonitorManager] WMI 查询完全失败，所有显示器标记为外接显示器 (isBuiltIn=false)");
                foreach (Screen screen in Screen.AllScreens)
                {
                    result[screen.DeviceName] = (screen.DeviceName, false, null, null);
                }
            }

            return result;
        }

        private static (string deviceId, string monitorName, string manufacturerCode)? GetEdidDetails(string instanceName)
        {
            try
            {
                string escapedName = instanceName.Replace("\\", "\\\\").Replace("'", "''");

                using (var searcher = new ManagementObjectSearcher("root\\WMI",
                    $"SELECT * FROM WmiMonitorEDID WHERE InstanceName='{escapedName}'"))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject mo in collection)
                        {
                            try
                            {
                                byte[] edidData = mo["EDID"] as byte[];
                                if (edidData != null && edidData.Length >= 128)
                                {
                                    string manufacturerCode = DecodeEdidManufacturerId(edidData[0x08], edidData[0x09]);
                                    ushort productCode = (ushort)(edidData[0x0B] << 8 | edidData[0x0A]);
                                    string deviceId = $"MONITOR\\{manufacturerCode}{productCode:X4}";
                                    string monitorName = ParseEdidMonitorName(edidData);

                                    return (deviceId, monitorName, manufacturerCode);
                                }
                            }
                            finally
                            {
                                mo.Dispose();
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static Dictionary<string, uint> GetMonitorConnectionParams()
        {
            var result = new Dictionary<string, uint>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM WmiMonitorConnectionParams"))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject mo in collection)
                    {
                        try
                        {
                            string instanceName = mo["InstanceName"]?.ToString();
                            if (string.IsNullOrEmpty(instanceName)) continue;
                            uint videoTech = (uint)mo["VideoOutputTechnology"];
                            result[instanceName] = videoTech;
                        }
                        catch
                        {
                        }
                        finally
                        {
                            mo.Dispose();
                        }
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
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject mo in collection)
                        {
                            try
                            {
                                string monitorType = mo["MonitorType"]?.ToString() ?? "";
                                return monitorType.Contains("LCD") || monitorType.Contains("Internal");
                            }
                            finally
                            {
                                mo.Dispose();
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static string FindWmiInstanceName(string deviceId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM WmiMonitorBasicDisplayParams"))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject mo in collection)
                        {
                            try
                            {
                                string instanceName = mo["InstanceName"]?.ToString();
                                if (!string.IsNullOrEmpty(instanceName) && instanceName.StartsWith(deviceId))
                                {
                                    return instanceName;
                                }
                            }
                            finally
                            {
                                mo.Dispose();
                            }
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

        internal void OnMonitorsChanged()
        {
            MonitorsChanged?.Invoke();
        }

        public void EnterLightweightMode()
        {
            foreach (var ctrl in _allControllers)
            {
                try { (ctrl as IDisposable)?.Dispose(); }
                catch { }
            }
            _allControllers.Clear();
            foreach (var monitor in _monitors)
            {
                monitor.Controller = null;
            }
        }

        public void ExitLightweightMode()
        {
            RefreshMonitors();
        }

        public void Dispose()
        {
            foreach (var ctrl in _allControllers)
            {
                try { (ctrl as IDisposable)?.Dispose(); }
                catch { }
            }
            _allControllers.Clear();
            _monitors.Clear();
            MonitorsChanged = null;
        }
    }
}
