using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [CreateAssetMenu(
        fileName = "GameplayScreenPrefabCatalog",
        menuName = "Game/UI/Screen Prefab Catalog")]
    public sealed class ScreenPrefabCatalog : ScriptableObject
    {
        [SerializeField] private GameplayScreenView _gameplayPrefab;
        [SerializeField] private HelpScreenView _helpPrefab;
        [SerializeField] private ObjectiveStatusScreenView _objectiveStatusPrefab;
        [SerializeField] private InventoryScreenView _inventoryPrefab;
        [SerializeField] private SettingsScreenView _settingsPrefab;
        [SerializeField] private StageResultScreenView _stageResultPrefab;

        public GameplayScreenView GameplayPrefab => _gameplayPrefab;

        public HelpScreenView HelpPrefab => _helpPrefab;

        public ObjectiveStatusScreenView ObjectiveStatusPrefab => _objectiveStatusPrefab;

        public InventoryScreenView InventoryPrefab => _inventoryPrefab;

        public SettingsScreenView SettingsPrefab => _settingsPrefab;

        public StageResultScreenView StageResultPrefab => _stageResultPrefab;
    }
}
