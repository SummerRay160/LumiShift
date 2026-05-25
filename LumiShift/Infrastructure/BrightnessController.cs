using System;
using System.Management;
using System.Windows.Forms;
using LumiShift.Models;

namespace LumiShift.Infrastructure
{
    public class WmiBrightnessController : IBrightnessController
    {
        private readonly string _instanceName;
        private readonly string _deviceId;
        private readonly string _displayName;
        private int _cachedBrightness = -1;

        public string DeviceId => _deviceId;
        public string DisplayName => _displayName;
        public bool IsDDC => false;
        public bool IsSupported => true;

        public WmiBrightnessController(string instanceName, string deviceId, string displayName)
        {
            _instanceName = instanceName;
            _deviceId = deviceId;
            _displayName = displayName;
        }

        public int GetBrightness()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root/WMI",
                    $"SELECT * FROM WmiMonitorBrightness WHERE InstanceName='{_instanceName.Replace("'", "''")}'"))
                {
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject mo in collection)
                        {
                            using (mo)
                            {
                                byte currentBrightness = (byte)mo["CurrentBrightness"];
                                _cachedBrightness = currentBrightness;
                                return currentBrightness;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return _cachedBrightness >= 0 ? _cachedBrightness : 50;
        }

        public void SetBrightness(int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            if (_cachedBrightness == percent) return;

            try
            {
                using (var methodClass = new ManagementObject("root/WMI",
                    $"WmiMonitorBrightnessMethods.InstanceName='{_instanceName.Replace("'", "''")}'", null))
                {
                    var inParams = methodClass.GetMethodParameters("WmiSetBrightness");
                    inParams["Brightness"] = (byte)percent;
                    inParams["Timeout"] = 1;
                    methodClass.InvokeMethod("WmiSetBrightness", inParams, null);
                    _cachedBrightness = percent;
                }
            }
            catch
            {
            }
        }
    }

    public class DdcBrightnessController : IBrightnessController, IDisposable
    {
        private readonly string _deviceId;
        private readonly string _displayName;
        private readonly Screen _screen;
        private IntPtr _physicalMonitor;
        private bool _disposed;
        private int _cachedBrightness = -1;

        public string DeviceId => _deviceId;
        public string DisplayName => _displayName;
        public bool IsDDC => true;
        public bool IsSupported { get; }

        public DdcBrightnessController(Screen screen, string deviceId, string displayName)
        {
            _screen = screen;
            _deviceId = deviceId;
            _displayName = displayName;
            _physicalMonitor = IntPtr.Zero;

            IsSupported = InitializePhysicalMonitor();
        }

        private bool InitializePhysicalMonitor()
        {
            try
            {
                var pt = new NativeMethods.POINT
                {
                    X = _screen.Bounds.Left + 1,
                    Y = _screen.Bounds.Top + 1
                };
                IntPtr hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (hMonitor == IntPtr.Zero)
                    return false;

                if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count))
                    return false;

                if (count <= 0)
                    return false;

                var monitors = new NativeMethods.PHYSICAL_MONITOR[count];
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                    return false;

                _physicalMonitor = monitors[0].hPhysicalMonitor;

                if (!NativeMethods.GetMonitorCapabilities(_physicalMonitor,
                    out uint caps, out uint _))
                    return false;

                return (caps & NativeMethods.MC_CAPS_BRIGHTNESS) != 0;
            }
            catch
            {
                return false;
            }
        }

        public int GetBrightness()
        {
            if (_physicalMonitor == IntPtr.Zero || !IsSupported)
                return 50;

            try
            {
                if (NativeMethods.GetMonitorBrightness(_physicalMonitor,
                    out uint min, out uint current, out uint max))
                {
                    if (max > min)
                    {
                        int brightness = (int)Math.Round((double)(current - min) / (max - min) * 100);
                        _cachedBrightness = brightness;
                        return brightness;
                    }
                    _cachedBrightness = (int)current;
                    return (int)current;
                }
            }
            catch
            {
            }
            return _cachedBrightness >= 0 ? _cachedBrightness : 50;
        }

        public void SetBrightness(int percent)
        {
            if (_physicalMonitor == IntPtr.Zero || !IsSupported)
                return;

            if (_cachedBrightness == percent) return;

            try
            {
                if (NativeMethods.GetMonitorBrightness(_physicalMonitor,
                    out uint min, out uint _, out uint max))
                {
                    uint newValue = min + (uint)Math.Round((double)percent / 100.0 * (max - min));
                    NativeMethods.SetMonitorBrightness(_physicalMonitor, newValue);
                    _cachedBrightness = percent;
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DdcBrightnessController()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed && _physicalMonitor != IntPtr.Zero)
            {
                NativeMethods.DestroyPhysicalMonitor(_physicalMonitor);
                _physicalMonitor = IntPtr.Zero;
                _disposed = true;
            }
        }
    }
}