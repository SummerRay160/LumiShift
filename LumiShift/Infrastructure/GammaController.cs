using System;
using System.Collections.Generic;
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

        public event EventHandler<string> StatusChanged;

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

        public bool ApplyGamma(IEnumerable<Screen> screens, GammaParameters parameters)
        {
            try
            {
                double master = 0.15 + parameters.MasterBrightness / 100.0 * 0.85;

                RAMP ramp;
                lock (_rampLock)
                {
                    ramp = BuildRamp(parameters.Gamma, parameters.RScale, parameters.GScale, parameters.BScale, master);
                }

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
                        bool result = SetDeviceGammaRamp(hdc, ref ramp);
                        if (!result)
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

                if (allSucceeded)
                {
                    StatusChanged?.Invoke(this, Lang.F("Gamma: R×{0} G×{1} B×{2} γ{3} 亮度{4}%",
                        parameters.RScale.ToString("F2"), parameters.GScale.ToString("F2"), parameters.BScale.ToString("F2"),
                        parameters.Gamma.ToString("F2"), parameters.MasterBrightness));
                }
                else
                {
                    StatusChanged?.Invoke(this, Lang.Get("Gamma: 部分显示器应用失败"));
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
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

                foreach (var screen in Screen.AllScreens)
                {
                    IntPtr hdc = CreateDC("DISPLAY", screen.DeviceName, null, IntPtr.Zero);
                    if (hdc == IntPtr.Zero)
                    {
                        allSucceeded = false;
                        continue;
                    }

                    try
                    {
                        if (perScreenParams.TryGetValue(screen.DeviceName, out var parameters))
                        {
                            double master = 0.15 + parameters.MasterBrightness / 100.0 * 0.85;
                            RAMP ramp;
                            lock (_rampLock)
                            {
                                ramp = BuildRamp(parameters.Gamma, parameters.RScale, parameters.GScale, parameters.BScale, master);
                            }
                            if (!SetDeviceGammaRamp(hdc, ref ramp))
                                allSucceeded = false;
                        }
                        else
                        {
                            if (!SetDeviceGammaRamp(hdc, ref defaultRamp))
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

                if (allSucceeded)
                    StatusChanged?.Invoke(this, Lang.Get("Gamma: 按显示器独立应用"));
                else
                    StatusChanged?.Invoke(this, Lang.Get("Gamma: 部分显示器应用失败"));

                return allSucceeded;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, Lang.F("Gamma 出错: {0}", ex.Message));
                return false;
            }
        }

        private static RAMP BuildRamp(double gamma, double rScale, double gScale, double bScale, double master)
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
                StatusChanged = null;
            }
        }


    }
}
