using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [CreateAssetMenu(
        fileName = "GameplayScreenPrefabCatalog",
        menuName = "Game/UI/Screen Prefab Catalog")]
    public sealed class ScreenPrefabCatalog : ScriptableObject
    {
        [SerializeField] private SettingsScreenView _settingsPrefab;
        [SerializeField] private GameplayUiTypographyTheme _settingsTypographyTheme;
        [SerializeField] private StageResultScreenView _stageResultPrefab;
        [SerializeField] private LevelFailedScreenView _levelFailedPrefab;
        [SerializeField] private GameClearScreenView _gameClearPrefab;

        public SettingsScreenView SettingsPrefab => _settingsPrefab;

        internal GameplayUiTypographyTheme SettingsTypographyTheme => _settingsTypographyTheme;

        public StageResultScreenView StageResultPrefab => _stageResultPrefab;

        public LevelFailedScreenView LevelFailedPrefab => _levelFailedPrefab;

        public GameClearScreenView GameClearPrefab => _gameClearPrefab;
    }
}
