using UnityEngine;

namespace Game.Shared.Input
{
    public sealed class PlayerPrefsKeyboardBindingStore : IKeyboardBindingStore
    {
        public const string MovementSchemeKey = "Game.Feature.Input.KeyboardMovementScheme";
        public const string BindingOverridesJsonKey = "Game.Feature.Input.KeyboardBindingOverridesJson";

        public bool TryLoadMovementScheme(out KeyboardMovementScheme scheme)
        {
            var raw = PlayerPrefs.GetString(MovementSchemeKey, string.Empty);
            switch (raw)
            {
                case nameof(KeyboardMovementScheme.Wasd):
                    scheme = KeyboardMovementScheme.Wasd;
                    return true;
                case nameof(KeyboardMovementScheme.ArrowKeys):
                    scheme = KeyboardMovementScheme.ArrowKeys;
                    return true;
                default:
                    scheme = KeyboardMovementScheme.Wasd;
                    return false;
            }
        }

        public void SaveMovementScheme(KeyboardMovementScheme scheme)
        {
            PlayerPrefs.SetString(MovementSchemeKey, scheme.ToString());
        }

        public bool TryLoadBindingOverridesJson(out string json)
        {
            json = PlayerPrefs.GetString(BindingOverridesJsonKey, string.Empty);
            return !string.IsNullOrWhiteSpace(json);
        }

        public void SaveBindingOverridesJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                ClearBindingOverridesJson();
                return;
            }

            PlayerPrefs.SetString(BindingOverridesJsonKey, json);
        }

        public void ClearBindingOverridesJson()
        {
            PlayerPrefs.DeleteKey(BindingOverridesJsonKey);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
