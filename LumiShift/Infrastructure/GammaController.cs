using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LumiShift.Infrastructure
{
    public class GammaParameters
    {
        public double RScale { get; set; } = 1.0;
        public double GScale { get; set; } = 1.0;
        public double BScale { get; set; } = 1.0;
        public double Gamma { get; set; } = 1.0;
        public int MasterBrightness { get; set; } = 100;
    }

    public class GammaController : IDisposable
    {
        private bool _disposed;

        public event EventHandler<string> StatusChanged;

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
                    DeleteDC(hdc);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool ApplyGamma(IEnumerable<Screen> screens, GammaParameters parameters)
        {
            if (parameters == null)
                return ResetGamma(screens);

            try
            {
                double master = 0.15 + parameters.MasterBrightness / 100.0 * 0.85;

                var ramp = GenerateRamp(parameters.Gamma, parameters.RScale, parameters.GScale, parameters.BScale, master);

                bool allSucceeded = true;

                foreach (var screen in screens)
                {
                    try
                    {
                        IntPtr hdc = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
                        if (hdc == IntPtr.Zero)
                        {
                            allSucceeded = false;
                            continue;
                        }

                        bool result = SetDeviceGammaRamp(hdc, ref ramp);
                        if (!result)
                        {
                            allSucceeded = false;
                        }

                        DeleteDC(hdc);
                    }
                    catch
                    {
                        allSucceeded = false;
                    }
                }

                if (allSucceeded)
                {
                    StatusChanged?.Invoke(this, $"Gamma: R×{parameters.RScale:F2} G×{parameters.GScale:F2} B×{parameters.BScale:F2} γ{parameters.Gamma:F2} 亮度{parameters.MasterBrightness}%");
                }
                else
                {
                    StatusChanged?.Invoke(this, "Gamma: 部分显示器应用失败");
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Gamma 出错: {ex.Message}");
                return false;
            }
        }

        public bool ResetGamma(IEnumerable<Screen> screens)
        {
            try
            {
                var ramp = GenerateRamp(1.0, 1.0, 1.0, 1.0, 1.0);
                bool allSucceeded = true;

                foreach (var screen in screens)
                {
                    try
                    {
                        IntPtr hdc = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
                        if (hdc == IntPtr.Zero)
                        {
                            allSucceeded = false;
                            continue;
                        }

                        if (!SetDeviceGammaRamp(hdc, ref ramp))
                        {
                            allSucceeded = false;
                        }

                        DeleteDC(hdc);
                    }
                    catch
                    {
                        allSucceeded = false;
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
                var defaultRamp = GenerateRamp(1.0, 1.0, 1.0, 1.0, 1.0);

                foreach (var screen in Screen.AllScreens)
                {
                    try
                    {
                        IntPtr hdc = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
                        if (hdc == IntPtr.Zero)
                        {
                            allSucceeded = false;
                            continue;
                        }

                        if (perScreenParams.TryGetValue(screen.DeviceName, out var parameters))
                        {
                            double master = 0.15 + parameters.MasterBrightness / 100.0 * 0.85;
                            var ramp = GenerateRamp(parameters.Gamma, parameters.RScale, parameters.GScale, parameters.BScale, master);
                            if (!SetDeviceGammaRamp(hdc, ref ramp))
                                allSucceeded = false;
                        }
                        else
                        {
                            if (!SetDeviceGammaRamp(hdc, ref defaultRamp))
                                allSucceeded = false;
                        }

                        DeleteDC(hdc);
                    }
                    catch
                    {
                        allSucceeded = false;
                    }
                }

                if (allSucceeded)
                    StatusChanged?.Invoke(this, "Gamma: 按显示器独立应用");
                else
                    StatusChanged?.Invoke(this, "Gamma: 部分显示器应用失败");

                return allSucceeded;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Gamma 出错: {ex.Message}");
                return false;
            }
        }

        private static RAMP GenerateRamp(double gamma, double rScale, double gScale, double bScale, double master)
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

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}