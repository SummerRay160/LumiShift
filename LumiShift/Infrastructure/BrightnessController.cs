using System;
using System.Management;
using System.Windows.Forms;
using LumiShift.Models;

namespace LumiShift.Infrastructure
{
    public class WmiBrightnessController : IBrightnessController, IDisposable
    {
        private readonly string _instanceName;
        private readonly string _deviceId;
        private readonly string _displayName;
        private int _cachedBrightness = -1;
        private bool _disposed;

        public string DeviceId => _deviceId;
        public string DisplayName => _displayName;
        public bool IsDDC => false;
        public bool IsSupported => !_disposed;

        public WmiBrightnessController(string instanceName, string deviceId, string displayName)
        {
            _instanceName = instanceName;
            _deviceId = deviceId;
            _displayName = displayName;
        }

        public int GetBrightness()
        {
            if (_disposed) return _cachedBrightness >= 0 ? _cachedBrightness : 50;
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
            if (_disposed) return;
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public class DdcBrightnessController : IBrightnessController, IDisposable
    {
        private readonly string _deviceId;
        private readonly string _displayName;
        private IntPtr _physicalMonitor;
        private NativeMethods.PHYSICAL_MONITOR[] _allPhysicalMonitors;
        private uint _physicalMonitorCount;
        private bool _disposed;
        private int _cachedBrightness = -1;

        public string DeviceId => _deviceId;
        public string DisplayName => _displayName;
        public bool IsDDC => true;
        public bool IsSupported { get; }

        public DdcBrightnessController(Screen screen, string deviceId, string displayName)
        {
            _deviceId = deviceId;
            _displayName = displayName;
            _physicalMonitor = IntPtr.Zero;
            _allPhysicalMonitors = null;
            _physicalMonitorCount = 0;

            IsSupported = InitializePhysicalMonitor(screen);
        }

        private bool InitializePhysicalMonitor(Screen screen)
        {
            try
            {
                // 使用屏幕完整边界矩形定位显示器，比单点定位更可靠
                var screenRect = new NativeMethods.RECT
                {
                    Left = screen.Bounds.Left,
                    Top = screen.Bounds.Top,
                    Right = screen.Bounds.Right,
                    Bottom = screen.Bounds.Bottom
                };

                // MONITOR_DEFAULTTONULL: 如果矩形不在任何显示器上，返回 IntPtr.Zero 而非默认值
                IntPtr hMonitor = NativeMethods.MonitorFromRect(ref screenRect, NativeMethods.MONITOR_DEFAULTTONULL);
                if (hMonitor == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"[DdcBrightness] MonitorFromRect 返回 null (screen={screen.DeviceName})");
                    return false;
                }

                // 验证获取到的 HMONITOR 是否对应预期的屏幕
                var monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(monitorInfo);
                if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    System.Diagnostics.Debug.WriteLine($"[DdcBrightness] GetMonitorInfo 失败 (screen={screen.DeviceName})");
                    return false;
                }

                // 验证显示器边界是否匹配
                if (monitorInfo.rcMonitor.Left != screen.Bounds.Left ||
                    monitorInfo.rcMonitor.Top != screen.Bounds.Top ||
                    monitorInfo.rcMonitor.Right != screen.Bounds.Right ||
                    monitorInfo.rcMonitor.Bottom != screen.Bounds.Bottom)
                {
                    System.Diagnostics.Debug.WriteLine($"[DdcBrightness] 显示器不匹配: 期望={screen.Bounds}, 实际=({monitorInfo.rcMonitor.Left},{monitorInfo.rcMonitor.Top},{monitorInfo.rcMonitor.Right},{monitorInfo.rcMonitor.Bottom}) (screen={screen.DeviceName})");
                    // 改用中心点重试
                    var centerPt = new NativeMethods.POINT
                    {
                        X = screen.Bounds.Left + screen.Bounds.Width / 2,
                        Y = screen.Bounds.Top + screen.Bounds.Height / 2
                    };
                    hMonitor = NativeMethods.MonitorFromPoint(centerPt, NativeMethods.MONITOR_DEFAULTTONULL);
                    if (hMonitor == IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DdcBrightness] 重试 MonitorFromPoint 也失败 (screen={screen.DeviceName})");
                        return false;
                    }

                    monitorInfo = new NativeMethods.MONITORINFO();
                    monitorInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(monitorInfo);
                    if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DdcBrightness] 重试 GetMonitorInfo 失败 (screen={screen.DeviceName})");
                        return false;
                    }
                }

                if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count))
                {
                    System.Diagnostics.Debug.WriteLine($"[DdcBrightness] GetNumberOfPhysicalMonitorsFromHMONITOR 失败 (screen={screen.DeviceName})");
                    return false;
                }

                if (count <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DdcBrightness] 无物理显示器 (screen={screen.DeviceName})");
                    return false;
                }

                var monitors = new NativeMethods.PHYSICAL_MONITOR[count];
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                {
                    // 如果获取失败，确保数组中的句柄不被泄露（此时数组可能未填充）
                    return false;
                }

                // 保存所有物理显示器句柄，确保在 Dispose 时全部销毁
                _allPhysicalMonitors = monitors;
                _physicalMonitorCount = count;
                _physicalMonitor = monitors[0].hPhysicalMonitor;

                // 检查亮度能力
                if (!NativeMethods.GetMonitorCapabilities(_physicalMonitor,
                    out uint caps, out uint _))
                {
                    // 如果能力检查失败，不保存句柄（Dispose 时仍会销毁所有已分配的句柄）
                    return false;
                }

                bool supportsBrightness = (caps & NativeMethods.MC_CAPS_BRIGHTNESS) != 0;

                if (!supportsBrightness)
                {
                    System.Diagnostics.Debug.WriteLine($"[DdcBrightness] 不支持 DDC/CI 亮度控制 (screen={screen.DeviceName})");
                }

                return supportsBrightness;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DdcBrightness] 初始化异常: {ex.Message} (screen={screen?.DeviceName})");
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
            if (_disposed) return;

            if (_allPhysicalMonitors != null && _physicalMonitorCount > 0)
            {
                // 使用 DestroyPhysicalMonitors 销毁所有物理显示器句柄
                NativeMethods.DestroyPhysicalMonitors(_physicalMonitorCount, _allPhysicalMonitors);
                _allPhysicalMonitors = null;
                _physicalMonitor = IntPtr.Zero;
                _physicalMonitorCount = 0;
            }
            else if (_physicalMonitor != IntPtr.Zero)
            {
                // 回退：如果没有完整数组就销毁单个句柄
                NativeMethods.DestroyPhysicalMonitor(_physicalMonitor);
                _physicalMonitor = IntPtr.Zero;
            }

            _disposed = true;
        }
    }
}