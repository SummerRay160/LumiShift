using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LumiShift.Services;

namespace LumiShift.Infrastructure
{
    public readonly struct GammaParameters
    {
        public double RScale { get; }
        public double GScale { get; }
        public double BScale { get; }
        public double Gamma { get; }
        public int MasterBrightness { get; }

        public GammaParameters(double rScale, double gScale, double bScale, double gamma, int masterBrightness)
        {
            RScale = rScale;
            GScale = gScale;
            BScale = bScale;
            Gamma = gamma;
            MasterBrightness = masterBrightness;
        }
    }

    public class GammaController : IDisposable
    {
        private bool _disposed;

        private static readonly RAMP DefaultRamp;
        private readonly object _rampLock = new object();

        // Windows 的 SetDeviceGammaRamp 带"防止屏幕不可读"启发式：每个通道 ramp[255] 需达到约 50%、
        // ramp[128] 需达到约 12.5%，否则直接返回 FALSE 或静默丢弃。这里各留一点余量。
        private const double RampMaxFloor = 0.52;
        private const double RampMidFloor = 0.135;

        // 单通道增益低于该值时，任何整体缩放都无法让该通道通过系统检查；
        // 此时把增益抬到该下限，改用硬件背光来补足剩余亮度。
        private const double RampMinChannelScale = 0.52;

        public event EventHandler<string> StatusChanged;

        /// <summary>把屏幕设备名（如 \\.\DISPLAY2）转换为界面上显示的显示器名称；返回空时回退设备名。</summary>
        public Func<string, string> DisplayNameResolver { get; set; }

        static GammaController()
        {
            var ramp = new RAMP
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };
            for (int i = 0; i < 256; i++)
            {
                double x = i / 255.0;
                double value = Math.Pow(x, 1.0);
                ushort val = (ushort)Math.Max(0, Math.Min(65535, Math.Round(value * 65535.0)));
                ramp.Red[i] = val;
                ramp.Green[i] = val;
                ramp.Blue[i] = val;
            }
            DefaultRamp = ramp;
        }

        public GammaController()
        {
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public bool IsSupported
        {
            get
            {
                try
                {
                    var screens = Screen.AllScreens;
                    if (screens.Length == 0) return false;

                    IntPtr hdc = CreateDC("DISPLAY", screens[0].DeviceName, null, IntPtr.Zero);
                    if (hdc == IntPtr.Zero)
                    {
                        hdc = GetDC(IntPtr.Zero);
                        if (hdc == IntPtr.Zero) return false;
                        ReleaseDC(IntPtr.Zero, hdc);
                        return true;
                    }
                    try { return true; }
                    finally { DeleteDC(hdc); }
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 把滑块亮度换算成 Windows 实际允许写入的 Gamma 缩放值。
        /// 系统会拒绝过暗的 Gamma 表，缺口部分由 <paramref name="hardwareDimFactor"/>（≤1）交给硬件背光补足。
        /// </summary>
        public static double GetWritableMaster(GammaParameters parameters, out double hardwareDimFactor)
        {
            double desired = 0.15 + parameters.MasterBrightness / 100.0 * 0.85;
            double master = Math.Max(desired, GetWritableFloor(parameters));
            if (master > 1.0) master = 1.0;

            hardwareDimFactor = master > 0 ? Math.Max(0.0, Math.Min(1.0, desired / master)) : 1.0;
            return master;
        }

        /// <summary>
        /// 当前参数下，仅靠 Gamma 能达到的最低滑块亮度百分比（0–100）。
        /// 用于界面标出分界点：分界点随色温/RGB/γ 变化，不是固定的 45%。
        /// </summary>
        public static int GetGammaOnlyFloorPercent(GammaParameters parameters)
        {
            double percent = (GetWritableFloor(parameters) - 0.15) / 0.85 * 100.0;
            return (int)Math.Max(0, Math.Min(100, Math.Round(percent)));
        }

        private static double GetWritableFloor(GammaParameters parameters)
        {
            double minScale = Math.Min(ClampChannelScale(parameters.RScale),
                Math.Min(ClampChannelScale(parameters.GScale), ClampChannelScale(parameters.BScale)));
            minScale = Math.Min(1.0, minScale);

            // 通道被压暗（如暖色温 B×0.70）或 gamma 曲线过陡时，整体缩放必须相应抬高
            double midPower = Math.Pow(128.0 / 255.0, Math.Max(0.0, parameters.Gamma));
            double writableFloor = Math.Max(RampMaxFloor / minScale, RampMidFloor / (minScale * midPower));
            if (writableFloor > 1.0) writableFloor = 1.0;
            return writableFloor;
        }

        /// <summary>仅取需要硬件背光补足的亮度系数（1.0 表示无需补足）。</summary>
        public static double GetHardwareDimFactor(GammaParameters parameters)
        {
            GetWritableMaster(parameters, out double dim);
            return dim;
        }

        /// <summary>向界面状态栏输出一条提示（用于非失败状态的告知，例如亮度已到系统下限）。</summary>
        public void ReportStatus(string message)
        {
            if (!string.IsNullOrEmpty(message))
                StatusChanged?.Invoke(this, message);
        }

        private static double ClampChannelScale(double scale)
        {
            return Math.Max(RampMinChannelScale, scale);
        }

        public bool ApplyGamma(IEnumerable<Screen> screens, GammaParameters parameters)
        {
            try
            {
                double master = GetWritableMaster(parameters, out _);
                bool allSucceeded = true;
                var failures = new List<(string DeviceName, string Reason)>();

                foreach (var screen in screens)
                {
                    IntPtr hdc = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
                    if (hdc == IntPtr.Zero)
                    {
                        allSucceeded = false;
                        failures.Add((screen.DeviceName, Lang.Get("无法打开显示设备")));
                        continue;
                    }

                    try
                    {
                        if (!TryWriteRamp(hdc, parameters, master))
                        {
                            allSucceeded = false;
                            failures.Add((screen.DeviceName, Lang.Get("系统拒绝写入 Gamma 表")));
                        }
                    }
                    catch (Exception ex)
                    {
                        allSucceeded = false;
                        failures.Add((screen.DeviceName, $"{ex.GetType().Name}: {ex.Message}"));
                    }
                    finally
                    {
                        DeleteDC(hdc);
                    }
                }

                if (allSucceeded)
                {
                    StatusChanged?.Invoke(this, Lang.F("Gamma: R×{0} G×{1} B×{2} γ{3} 亮度{4}%",
                        parameters.RScale.ToString("F2"), parameters.GScale.ToString("F2"), parameters.BScale.ToString("F2"),
                        parameters.Gamma.ToString("F2"), parameters.MasterBrightness));
                }
                else
                {
                    ReportFailures(failures);
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                Log.Error("Gamma", ex,
                    $"mode=global, gamma={parameters.Gamma:F2}, r={parameters.RScale:F2}, g={parameters.GScale:F2}, b={parameters.BScale:F2}, brightness={parameters.MasterBrightness}",
                    () => $"screens={string.Join(",", Screen.AllScreens.Select(s => s.DeviceName))}");
                StatusChanged?.Invoke(this, Lang.F("Gamma 出错: {0}", ex.Message));
                return false;
            }
        }

        public bool ResetGamma(IEnumerable<Screen> screens)
        {
            try
            {
                var ramp = DefaultRamp;
                bool allSucceeded = true;

                foreach (var screen in screens)
                {
                    IntPtr hdc = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
                    if (hdc == IntPtr.Zero)
                    {
                        allSucceeded = false;
                        continue;
                    }

                    try
                    {
                        if (!SetDeviceGammaRamp(hdc, ref ramp))
                        {
                            allSucceeded = false;
                        }
                    }
                    catch
                    {
                        allSucceeded = false;
                    }
                    finally
                    {
                        DeleteDC(hdc);
                    }
                }

                return allSucceeded;
            }
            catch
            {
                return false;
            }
        }

        public bool ApplyGammaPerScreen(IDictionary<string, GammaParameters> perScreenParams)
        {
            try
            {
                bool allSucceeded = true;
                var defaultRamp = DefaultRamp;
                var failures = new List<(string DeviceName, string Reason)>();

                foreach (var screen in Screen.AllScreens)
                {
                    IntPtr hdc = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
                    if (hdc == IntPtr.Zero)
                    {
                        allSucceeded = false;
                        failures.Add((screen.DeviceName, Lang.Get("无法打开显示设备")));
                        continue;
                    }

                    try
                    {
                        if (perScreenParams.TryGetValue(screen.DeviceName, out var parameters))
                        {
                            double master = GetWritableMaster(parameters, out _);
                            if (!TryWriteRamp(hdc, parameters, master))
                            {
                                allSucceeded = false;
                                failures.Add((screen.DeviceName, Lang.Get("系统拒绝写入 Gamma 表")));
                            }
                        }
                        else
                        {
                            if (!SetDeviceGammaRamp(hdc, ref defaultRamp))
                            {
                                allSucceeded = false;
                                failures.Add((screen.DeviceName, Lang.Get("系统拒绝写入 Gamma 表")));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        allSucceeded = false;
                        failures.Add((screen.DeviceName, $"{ex.GetType().Name}: {ex.Message}"));
                    }
                    finally
                    {
                        DeleteDC(hdc);
                    }
                }

                if (allSucceeded)
                    StatusChanged?.Invoke(this, Lang.Get("Gamma: 按显示器独立应用"));
                else
                    ReportFailures(failures);

                return allSucceeded;
            }
            catch (Exception ex)
            {
                Log.Error("Gamma", ex,
                    $"mode=perDisplay, displays={perScreenParams?.Count ?? 0}",
                    () => $"screens={string.Join(",", Screen.AllScreens.Select(s => s.DeviceName))}");
                StatusChanged?.Invoke(this, Lang.F("Gamma 出错: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>写入 Gamma 表；被系统启发式拒绝时抬高整体缩放再试一次，避免误报失败。</summary>
        private bool TryWriteRamp(IntPtr hdc, GammaParameters parameters, double master)
        {
            RAMP ramp;
            lock (_rampLock)
            {
                ramp = BuildRamp(parameters.Gamma, parameters.RScale, parameters.GScale, parameters.BScale, master);
            }
            if (SetDeviceGammaRamp(hdc, ref ramp)) return true;

            if (master >= 0.999) return false;

            lock (_rampLock)
            {
                ramp = BuildRamp(parameters.Gamma, parameters.RScale, parameters.GScale, parameters.BScale,
                    Math.Min(1.0, master * 1.15));
            }
            return SetDeviceGammaRamp(hdc, ref ramp);
        }

        private static RAMP BuildRamp(double gamma, double rScale, double gScale, double bScale, double master)
        {
            var ramp = new RAMP
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256]
            };

            rScale = ClampChannelScale(rScale);
            gScale = ClampChannelScale(gScale);
            bScale = ClampChannelScale(bScale);

            for (int i = 0; i < 256; i++)
            {
                double x = i / 255.0;
                double value = Math.Pow(x, gamma);

                double rVal = value * 65535.0 * rScale * master;
                double gVal = value * 65535.0 * gScale * master;
                double bVal = value * 65535.0 * bScale * master;

                ramp.Red[i] = (ushort)Math.Max(0, Math.Min(65535, Math.Round(rVal)));
                ramp.Green[i] = (ushort)Math.Max(0, Math.Min(65535, Math.Round(gVal)));
                ramp.Blue[i] = (ushort)Math.Max(0, Math.Min(65535, Math.Round(bVal)));
            }

            return ramp;
        }

        private void ReportFailures(List<(string DeviceName, string Reason)> failures)
        {
            var details = new List<string>(failures.Count);
            foreach (var failure in failures)
            {
                string device = DescribeDevice(failure.DeviceName);
                details.Add(Lang.F("{0}：{1}", device, failure.Reason));
            }

            // 状态栏空间有限，完整明细同时写入诊断日志，便于逐台排查
            foreach (string detail in details)
                Log.Warn("Gamma", detail);

            StatusChanged?.Invoke(this, Lang.F("Gamma: 部分显示器应用失败（{0}）", string.Join(" / ", details)));
        }

        private string DescribeDevice(string deviceName)
        {
            try
            {
                string resolved = DisplayNameResolver?.Invoke(deviceName);
                if (!string.IsNullOrEmpty(resolved))
                    return resolved;
            }
            catch
            {
                // 名称解析失败时回退设备名，不影响错误提示
            }

            const string prefix = @"\\.\";
            return !string.IsNullOrEmpty(deviceName) && deviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? deviceName.Substring(prefix.Length)
                : deviceName;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StatusChanged = null;
            }
        }


    }
}
