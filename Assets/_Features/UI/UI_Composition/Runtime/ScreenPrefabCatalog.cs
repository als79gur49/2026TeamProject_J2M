using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [CreateAssetMenu(
        fileName = "GameplayScreenPrefabCatalog",
        menuName = "Game/UI/Screen Prefab Catalog")]
    public sealed class ScreenPrefabCatalog : ScriptableObject
    {
        [SerializeField] private ObjectiveStatusScreenView _objectiveStatusPrefab;
        [SerializeField] private SettingsScreenView _settingsPrefab;
        [SerializeField] private StageResultScreenView _stageResultPrefab;
        [SerializeField] private LevelFailedScreenView _levelFailedPrefab;

        public ObjectiveStatusScreenView ObjectiveStatusPrefab => _objectiveStatusPrefab;

        public SettingsScreenView SettingsPrefab => _settingsPrefab;

        public StageResultScreenView StageResultPrefab => _stageResultPrefab;

        public LevelFailedScreenView LevelFailedPrefab => _levelFailedPrefab;
    }
}
