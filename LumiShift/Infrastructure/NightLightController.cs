using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;

namespace LumiShift.Infrastructure
{
    public class NightLightController
    {
        private const string RegistryPath =
            @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\" +
            @"default$windows.data.bluelightreduction.bluelightreductionstate\" +
            @"windows.data.bluelightreduction.bluelightreductionstate";

        private System.Threading.Timer _debounceTimer;
        private int _pendingStrength = -1;
        private readonly object _strengthLock = new object();

        public event EventHandler<string> StatusChanged;

        public bool IsNightLightSupported
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                    {
                        return key != null;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool IsNightLightEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return false;
                    byte[] data = key.GetValue("Data") as byte[];
                    if (data == null || data.Length < 12) return false;

                    return DetectNightLightEnabled(data);
                }
            }
            catch
            {
                return false;
            }
        }

        public void SetNightLightEnabled(bool enabled)
        {
            try
            {
                byte[] originalData;
                using (var readKey = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (readKey == null)
                    {
                        StatusChanged?.Invoke(this, "夜间模式: 无法访问注册表");
                        return;
                    }
                    originalData = readKey.GetValue("Data") as byte[];
                }

                if (originalData == null || originalData.Length < 12)
                {
                    StatusChanged?.Invoke(this, "夜间模式: 注册表数据无效");
                    return;
                }

                byte[] newData = (byte[])originalData.Clone();
                bool modified = ApplyNightLightModification(newData, enabled);

                if (!modified)
                {
                    StatusChanged?.Invoke(this, "夜间模式: 无法识别注册表数据格式，将在保存时尝试");
                    modified = TryForceModifyNightLight(newData, enabled);
                }

                if (modified)
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("Data", newData, RegistryValueKind.Binary);
                        }
                    }

                    NotifySystemSettingsChanged();

                    bool verifyState = IsNightLightEnabled();
                    if (verifyState == enabled)
                    {
                        StatusChanged?.Invoke(this, enabled ? "夜间模式: 已开启" : "夜间模式: 已关闭");
                    }
                    else
                    {
                        StatusChanged?.Invoke(this, "夜间模式: 写入注册表后状态未变更，尝试使用系统设置页");
                        OpenNightLightSettings();
                    }
                }
                else
                {
                    StatusChanged?.Invoke(this, "夜间模式: 无法修改注册表数据，请使用系统设置");
                    OpenNightLightSettings();
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"夜间模式出错: {ex.Message}");
            }
        }

        public int GetNightLightStrength()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return 50;
                    byte[] data = key.GetValue("Data") as byte[];
                    if (data == null || data.Length < 20) return 50;

                    int kelvin = ReadColorTemperature(data);
                    if (kelvin < 1200 || kelvin > 6500)
                        return 50;

                    return KelvinToStrength(kelvin);
                }
            }
            catch
            {
                return 50;
            }
        }

        public void SetNightLightStrength(int strength)
        {
            try
            {
                int kelvin = StrengthToKelvin(strength);

                byte[] data;
                using (var readKey = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (readKey == null)
                    {
                        StatusChanged?.Invoke(this, "夜间模式强度: 无法访问注册表");
                        return;
                    }
                    data = readKey.GetValue("Data") as byte[];
                    if (data == null || data.Length < 20)
                    {
                        StatusChanged?.Invoke(this, "夜间模式强度: 注册表数据无效");
                        return;
                    }
                }

                byte[] newData = (byte[])data.Clone();
                if (!WriteColorTemperature(newData, kelvin))
                {
                    StatusChanged?.Invoke(this, "夜间模式强度: 无法找到色温数据位置");
                    return;
                }

                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key == null) return;
                    key.SetValue("Data", newData, RegistryValueKind.Binary);
                }

                NotifySystemSettingsChanged();

                int verifyStrength = GetNightLightStrength();
                if (Math.Abs(verifyStrength - strength) <= 5)
                {
                    StatusChanged?.Invoke(this, $"夜间模式强度: {strength}%");
                }
                else
                {
                    StatusChanged?.Invoke(this, "夜间模式强度: 写入后验证不匹配，尝试多次写入");
                    for (int retry = 0; retry < 3; retry++)
                    {
                        Thread.Sleep(200);
                        using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                        {
                            if (key != null)
                            {
                                key.SetValue("Data", newData, RegistryValueKind.Binary);
                            }
                        }
                        NotifySystemSettingsChanged();
                        verifyStrength = GetNightLightStrength();
                        if (Math.Abs(verifyStrength - strength) <= 5)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"夜间模式强度出错: {ex.Message}");
            }
        }

        public void ScheduleSetNightLightStrength(int strength)
        {
            lock (_strengthLock)
            {
                _pendingStrength = strength;
            }

            if (_debounceTimer == null)
            {
                _debounceTimer = new System.Threading.Timer(OnDebounceTimerElapsed);
            }

            _debounceTimer.Change(300, Timeout.Infinite);
        }

        public static void OpenNightLightSettings()
        {
            try
            {
                Process.Start("ms-settings:nightlight");
            }
            catch
            {
            }
        }

        private void OnDebounceTimerElapsed(object state)
        {
            try
            {
                int strength;
                lock (_strengthLock)
                {
                    if (_pendingStrength < 0)
                        return;
                    strength = _pendingStrength;
                    _pendingStrength = -1;
                }
                SetNightLightStrength(strength);
            }
            catch
            {
            }
        }

        private static bool DetectNightLightEnabled(byte[] data)
        {
            if (data == null || data.Length < 8) return false;

            byte[] enabledFlags = { 0x09, 0x0D, 0x01, 0x11, 0x15, 0x19, 0x1D, 0x05, 0x0B, 0x0F };

            for (int offset = 4; offset < Math.Min(data.Length, 40); offset++)
            {
                byte b = data[offset];
                if (Array.IndexOf(enabledFlags, b) >= 0)
                {
                    if (offset > 0 && data[offset - 1] == 0x00)
                        return true;
                    if (offset + 1 < data.Length && data[offset + 1] == 0x00)
                        return true;
                }
            }

            for (int i = 4; i < data.Length - 3 && i < 60; i++)
            {
                uint val = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                if (val == 1 && i > 1 && data[i - 1] == 0x00)
                    return true;
            }

            for (int i = 2; i < data.Length - 1 && i < 80; i += 2)
            {
                if (data[i] == 0x01 && data[i + 1] == 0x00 &&
                    i > 1 && data[i - 1] == 0x00 && data[i - 2] == 0x00)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ApplyNightLightModification(byte[] data, bool enabled)
        {
            bool changed = false;

            byte[] enabledFlags = { 0x09, 0x0D, 0x01, 0x11, 0x15, 0x19, 0x1D, 0x05, 0x0B, 0x0F };
            byte[] disabledFlags = { 0x0A, 0x0E, 0x00, 0x10, 0x14, 0x18, 0x1C, 0x04, 0x08, 0x0C };

            if (enabled)
            {
                for (int offset = 4; offset < Math.Min(data.Length, 40); offset++)
                {
                    if (Array.IndexOf(disabledFlags, data[offset]) >= 0)
                    {
                        if (offset > 0 && data[offset - 1] == 0x00)
                        {
                            data[offset] = 0x09;
                            changed = true;
                        }
                        else if (offset + 1 < data.Length && data[offset + 1] == 0x00)
                        {
                            data[offset] = 0x09;
                            changed = true;
                        }
                    }
                }

                for (int i = 4; i < data.Length - 3 && i < 60; i++)
                {
                    uint val = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                    if (val == 0 && i > 1 && data[i - 1] == 0x00)
                    {
                        data[i] = 0x01;
                        changed = true;
                        break;
                    }
                }

                for (int i = 2; i < data.Length - 1 && i < 80; i += 2)
                {
                    if (data[i] == 0x00 && data[i + 1] == 0x00 &&
                        i > 1 && data[i - 1] == 0x00 && data[i - 2] == 0x00)
                    {
                        data[i] = 0x01;
                        changed = true;
                        break;
                    }
                }
            }
            else
            {
                for (int offset = 4; offset < Math.Min(data.Length, 40); offset++)
                {
                    if (Array.IndexOf(enabledFlags, data[offset]) >= 0)
                    {
                        if (offset > 0 && data[offset - 1] == 0x00)
                        {
                            data[offset] = 0x0A;
                            changed = true;
                        }
                        else if (offset + 1 < data.Length && data[offset + 1] == 0x00)
                        {
                            data[offset] = 0x0A;
                            changed = true;
                        }
                    }
                }

                for (int i = 4; i < data.Length - 3 && i < 60; i++)
                {
                    uint val = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                    if (val == 1 && i > 1 && data[i - 1] == 0x00)
                    {
                        data[i] = 0x00;
                        changed = true;
                        break;
                    }
                }

                for (int i = 2; i < data.Length - 1 && i < 80; i += 2)
                {
                    if (data[i] == 0x01 && data[i + 1] == 0x00 &&
                        i > 1 && data[i - 1] == 0x00 && data[i - 2] == 0x00)
                    {
                        data[i] = 0x00;
                        changed = true;
                        break;
                    }
                }
            }

            return changed;
        }

        private static bool TryForceModifyNightLight(byte[] data, bool enabled)
        {
            if (data.Length < 12) return false;

            if (enabled)
            {
                data[8] = 0x09;

                if (data.Length > 12)
                    data[12] = 0x01;

                bool foundEnabledField = false;
                for (int i = 4; i < data.Length - 3; i += 2)
                {
                    if (data[i] == 0x00 && data[i + 1] == 0x00 && data[i - 1] == 0x00)
                    {
                        bool nearMarker = false;
                        int start = Math.Max(0, i - 8);
                        int end = Math.Min(data.Length, i + 8);
                        for (int j = start; j < end; j++)
                        {
                            if (data[j] == 0x09 || data[j] == 0x0A || data[j] == 0x0D || data[j] == 0x0E)
                            {
                                nearMarker = true;
                                break;
                            }
                        }
                        if (nearMarker)
                        {
                            data[i] = 0x01;
                            foundEnabledField = true;
                            break;
                        }
                    }
                }

                if (!foundEnabledField && data.Length > 14)
                {
                    data[14] = 0x01;
                    data[15] = 0x00;
                }

                return true;
            }
            else
            {
                data[8] = 0x0A;

                if (data.Length > 12)
                    data[12] = 0x00;

                for (int i = 4; i < data.Length - 1; i += 2)
                {
                    if (data[i] == 0x01 && data[i + 1] == 0x00 && data[i - 1] == 0x00)
                    {
                        data[i] = 0x00;
                        break;
                    }
                }

                return true;
            }
        }

        private static int ReadColorTemperature(byte[] data)
        {
            int lastValidOffset = -1;
            uint lastValidValue = 0;

            for (int i = 0; i + 3 < data.Length; i++)
            {
                uint val = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                if (val >= 1200 && val <= 6500)
                {
                    lastValidOffset = i;
                    lastValidValue = val;
                }
            }

            if (lastValidOffset >= 0)
                return (int)lastValidValue;

            for (int i = 0; i + 1 < data.Length && i < 60; i += 2)
            {
                uint val = (uint)(data[i] | (data[i + 1] << 8));
                if (val >= 1200 && val <= 6500)
                {
                    return (int)val;
                }
            }

            return 0;
        }

        private static bool WriteColorTemperature(byte[] data, int kelvin)
        {
            if (data.Length < 20) return false;

            int lastValidOffset = -1;
            int matchCount = 0;

            for (int i = 0; i + 3 < data.Length; i++)
            {
                uint val = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                if (val >= 1200 && val <= 6500)
                {
                    lastValidOffset = i;
                    matchCount++;
                }
            }

            if (matchCount > 1)
            {
                int firstOffset = -1;
                for (int i = 0; i + 3 < data.Length; i++)
                {
                    uint val = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                    if (val >= 1200 && val <= 6500)
                    {
                        firstOffset = i;
                        break;
                    }
                }

                if (firstOffset >= 0)
                {
                    data[firstOffset] = (byte)(kelvin & 0xFF);
                    data[firstOffset + 1] = (byte)((kelvin >> 8) & 0xFF);
                    data[firstOffset + 2] = (byte)((kelvin >> 16) & 0xFF);
                    data[firstOffset + 3] = (byte)((kelvin >> 24) & 0xFF);
                }

                if (lastValidOffset >= 0 && lastValidOffset != firstOffset)
                {
                    data[lastValidOffset] = (byte)(kelvin & 0xFF);
                    data[lastValidOffset + 1] = (byte)((kelvin >> 8) & 0xFF);
                    data[lastValidOffset + 2] = (byte)((kelvin >> 16) & 0xFF);
                    data[lastValidOffset + 3] = (byte)((kelvin >> 24) & 0xFF);
                }

                return firstOffset >= 0;
            }

            if (lastValidOffset < 0)
                return false;

            data[lastValidOffset] = (byte)(kelvin & 0xFF);
            data[lastValidOffset + 1] = (byte)((kelvin >> 8) & 0xFF);
            data[lastValidOffset + 2] = (byte)((kelvin >> 16) & 0xFF);
            data[lastValidOffset + 3] = (byte)((kelvin >> 24) & 0xFF);
            return true;
        }

        private static int KelvinToStrength(int kelvin)
        {
            double normalized = (kelvin - 1200.0) / (6500.0 - 1200.0);
            return (int)Math.Round((1.0 - normalized) * 100.0);
        }

        private static int StrengthToKelvin(int strength)
        {
            double normalized = Math.Max(0, Math.Min(100, strength)) / 100.0;
            return (int)Math.Round(6500.0 - normalized * (6500.0 - 1200.0));
        }

        private static void NotifySystemSettingsChanged()
        {
            try
            {
                NativeMethods.SendMessageTimeout(
                    NativeMethods.HWND_BROADCAST,
                    NativeMethods.WM_SETTINGCHANGE,
                    IntPtr.Zero,
                    null,
                    NativeMethods.SMTO_ABORTIFHUNG,
                    5000,
                    out UIntPtr _);

                NativeMethods.SendMessageTimeout(
                    NativeMethods.HWND_BROADCAST,
                    NativeMethods.WM_SETTINGCHANGE,
                    IntPtr.Zero,
                    "WindowsDisplaySettings",
                    NativeMethods.SMTO_ABORTIFHUNG,
                    5000,
                    out UIntPtr _);
            }
            catch
            {
            }
        }
    }
}