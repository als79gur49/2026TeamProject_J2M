using UnityEngine;

namespace Game.Shared.Display
{
    public sealed class PlayerPrefsDisplaySettingsStore : IDisplaySettingsStore
    {
        private const string KeyPrefix = "settings.display.";
        private const string WidthKey = KeyPrefix + "width";
        private const string HeightKey = KeyPrefix + "height";
        private const string WindowModeKey = KeyPrefix + "windowMode";
        private const string RefreshNumeratorKey = KeyPrefix + "refreshNumerator";
        private const string RefreshDenominatorKey = KeyPrefix + "refreshDenominator";

        public bool TryLoad(out DisplaySettingsSnapshot snapshot)
        {
            if (!PlayerPrefs.HasKey(WidthKey) || !PlayerPrefs.HasKey(HeightKey))
            {
                snapshot = default;
                return false;
            }

            var width = PlayerPrefs.GetInt(WidthKey, 0);
            var height = PlayerPrefs.GetInt(HeightKey, 0);
            if (width <= 0 || height <= 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = new DisplaySettingsSnapshot(
                width,
                height,
                PlayerPrefs.GetInt(WindowModeKey, 0) == 0
                    ? DisplayWindowMode.Windowed
                    : DisplayWindowMode.FullScreenWindow,
                PlayerPrefs.GetInt(RefreshNumeratorKey, 0),
                PlayerPrefs.GetInt(RefreshDenominatorKey, 1));
            return true;
        }

        public void Save(DisplaySettingsSnapshot snapshot)
        {
            PlayerPrefs.SetInt(WidthKey, snapshot.Width);
            PlayerPrefs.SetInt(HeightKey, snapshot.Height);
            PlayerPrefs.SetInt(
                WindowModeKey,
                snapshot.WindowMode == DisplayWindowMode.Windowed ? 0 : 1);
            PlayerPrefs.SetInt(RefreshNumeratorKey, snapshot.RefreshRateNumerator);
            PlayerPrefs.SetInt(RefreshDenominatorKey, snapshot.RefreshRateDenominator);
            PlayerPrefs.Save();
        }
    }
}
