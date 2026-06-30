using System.Collections.Generic;
using System.Linq;
using LumiShift.Models;

namespace LumiShift.Services
{
    public class PresetService
    {
        private readonly UserSettings _settings;

        public PresetService(UserSettings settings)
        {
            _settings = settings;
        }

        public bool TryResolve(string presetName, out GammaConfig parameters)
        {
            parameters = null;

            var builtIn = PresetDefinitions.GetByName(presetName);
            if (builtIn != null)
            {
                parameters = GammaConfig.FromBuiltIn(builtIn);
                return true;
            }

            var custom = _settings.CustomGammaPresets?.FirstOrDefault(cp => cp.Name == presetName);
            if (custom != null)
            {
                parameters = GammaConfig.FromPreset(custom);
                return true;
            }

            return false;
        }

        public GammaPreset FindCustom(string presetName)
        {
            return _settings.CustomGammaPresets?.FirstOrDefault(cp => cp.Name == presetName);
        }

        public IEnumerable<string> GetPresetNames()
        {
            foreach (var name in PresetDefinitions.GetNames())
                yield return name;

            if (_settings.CustomGammaPresets == null) yield break;
            foreach (var preset in _settings.CustomGammaPresets)
                yield return preset.Name;
        }

        public bool IsMultiDisplayPreset(string presetName)
        {
            var custom = FindCustom(presetName);
            return custom?.PerDisplaySnapshot != null && custom.PerDisplaySnapshot.Count > 0;
        }
    }
}
