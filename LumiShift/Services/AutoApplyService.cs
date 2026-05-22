using System;
using System.Diagnostics;
using System.Timers;
using LumiShift.Infrastructure;
using LumiShift.Models;

namespace LumiShift.Services
{
    public class AutoApplyService : IDisposable
    {
        private readonly MonitorManager _monitorManager;
        private readonly GammaController _gammaController;
        private Timer _timer;
        private bool _enabled;
        private DateTime _lastUserActivityTime;
        private bool _isApplying;
        private UserSettings _settings;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (_timer != null)
                {
                    _timer.Enabled = value;
                }
            }
        }

        public event EventHandler<string> StatusChanged;

        public AutoApplyService(
            MonitorManager monitorManager,
            GammaController gammaController,
            UserSettings settings)
        {
            _monitorManager = monitorManager;
            _gammaController = gammaController;
            _settings = settings;
            _lastUserActivityTime = DateTime.MinValue;
            _isApplying = false;

            InitializeTimer();
        }

        public void UpdateSettings(UserSettings settings)
        {
            _settings = settings;
        }

        private void InitializeTimer()
        {
            _timer = new Timer();
            _timer.Interval = 300000;
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Enabled = _enabled;
        }

        public void UpdateInterval()
        {
        }

        public void NotifyUserActivity()
        {
            _lastUserActivityTime = DateTime.Now;
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isApplying || !_enabled) return;

            if (IsSystemSettingsForeground())
            {
                StatusChanged?.Invoke(this, "检测到系统设置窗口，跳过本次覆盖");
                return;
            }

            if ((DateTime.Now - _lastUserActivityTime).TotalSeconds < 15)
            {
                return;
            }

            _isApplying = true;
            try
            {
                ApplyCurrentSettings();
                StatusChanged?.Invoke(this, "已自动应用当前设置");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"自动覆盖失败: {ex.Message}");
            }
            finally
            {
                _isApplying = false;
            }
        }

        public void ApplyCurrentSettings()
        {
            foreach (var kvp in _settings.BrightnessPerDisplay)
            {
                _monitorManager.ApplyBrightness(kvp.Key, kvp.Value);
            }

            if (_settings.GammaEnabled)
            {
                var parameters = new GammaParameters
                {
                    RScale = _settings.GammaRScale,
                    GScale = _settings.GammaGScale,
                    BScale = _settings.GammaBScale,
                    Gamma = _settings.GammaValue,
                    MasterBrightness = _settings.MasterBrightness
                };
                _gammaController.ApplyGamma(System.Windows.Forms.Screen.AllScreens, parameters);
            }
            else
            {
                _gammaController.ResetGamma(System.Windows.Forms.Screen.AllScreens);
            }
        }

        private static bool IsSystemSettingsForeground()
        {
            try
            {
                IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero) return false;

                uint pid;
                NativeMethods.GetWindowThreadProcessId(foregroundWindow, out pid);
                var process = Process.GetProcessById((int)pid);
                string foregroundProcessName = process.ProcessName.ToLowerInvariant();

                return foregroundProcessName.Contains("systemsettings") ||
                       foregroundProcessName.Contains("ms-settings");
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }
    }
}