using System.Collections.Generic;
using LumiShift.Models;

namespace LumiShift.Services
{
    public class DisplayGammaStateService
    {
        private readonly UserSettings _settings;
        private readonly PresetService _presetService;

        public DisplayGammaStateService(UserSettings settings, PresetService presetService)
        {
            _settings = settings;
            _presetService = presetService;
        }

        public bool SetGlobalPreset(string presetName, GammaSource source)
        {
            if (!_presetService.TryResolve(presetName, out var parameters))
                return false;

            parameters.ApplyTo(_settings);

            var custom = _presetService.FindCustom(presetName);
            if (custom?.PerDisplaySnapshot != null && custom.PerDisplaySnapshot.Count > 0)
            {
                _settings.GammaPerDisplay.Clear();
                foreach (var kvp in custom.PerDisplaySnapshot)
                    _settings.GammaPerDisplay[kvp.Key] = CopyPerDisplay(kvp.Value, source);
            }

            return true;
        }

        public bool SetDisplayPreset(string deviceId, string presetName, GammaSource source)
        {
            if (!_presetService.TryResolve(presetName, out var parameters))
                return false;

            SetDisplayParameters(deviceId, parameters, source);
            return true;
        }

        public void SetGlobalParameters(GammaConfig parameters, bool clearDisplayOverrides)
        {
            parameters.ApplyTo(_settings);
            if (clearDisplayOverrides)
                ClearAllDisplayOverrides();
        }

        public void SetDisplayParameters(string deviceId, GammaConfig parameters, GammaSource source)
        {
            _settings.GammaPerDisplay[deviceId] = parameters.ToPerDisplayGamma(source);
        }

        public bool ClearDisplayOverride(string deviceId)
        {
            return _settings.GammaPerDisplay.Remove(deviceId);
        }

        public void ClearAllDisplayOverrides()
        {
            _settings.GammaPerDisplay.Clear();
        }

        public GammaConfig GetEffectiveParameters(string deviceId)
        {
            if (!string.IsNullOrEmpty(deviceId) && _settings.GammaPerDisplay.TryGetValue(deviceId, out var perDisplay))
            {
                return new GammaConfig
                {
                    RScale = perDisplay.RScale,
                    GScale = perDisplay.GScale,
                    BScale = perDisplay.BScale,
                    GammaValue = perDisplay.GammaValue,
                    MasterBrightness = perDisplay.MasterBrightness,
                    Enabled = perDisplay.Enabled
                };
            }

            return GammaConfig.FromSettings(_settings);
        }

        public bool HasDisplayOverride(string deviceId)
        {
            return !string.IsNullOrEmpty(deviceId) && _settings.GammaPerDisplay.ContainsKey(deviceId);
        }

        public string GetDisplaySource(string deviceId)
        {
            if (!string.IsNullOrEmpty(deviceId) && _settings.GammaPerDisplay.TryGetValue(deviceId, out var perDisplay))
                return perDisplay.Source;
            return null;
        }

        public bool HasManualOverrides()
        {
            foreach (var item in _settings.GammaPerDisplay)
            {
                if (item.Value.Source == GammaSourceNames.Manual)
                    return true;
            }
            return false;
        }

        public bool HasScheduleOverrides()
        {
            foreach (var item in _settings.GammaPerDisplay)
            {
                if (item.Value.Source == GammaSourceNames.Schedule)
                    return true;
            }
            return false;
        }

        public int OverrideCount => _settings.GammaPerDisplay.Count;

        private static PerDisplayGamma CopyPerDisplay(PerDisplayGamma source, GammaSource gammaSource)
        {
            return new PerDisplayGamma
            {
                RScale = source.RScale,
                GScale = source.GScale,
                BScale = source.BScale,
                GammaValue = source.GammaValue,
                MasterBrightness = source.MasterBrightness,
                Enabled = source.Enabled,
                Source = GammaSourceNames.ToSettingsValue(gammaSource)
            };
        }
    }
}
