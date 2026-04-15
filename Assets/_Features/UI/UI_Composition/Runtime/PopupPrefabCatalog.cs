using Game.Feature.UI.Popups;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [CreateAssetMenu(
        fileName = "GameplayPopupPrefabCatalog",
        menuName = "Game/UI/Popup Prefab Catalog")]
    public sealed class PopupPrefabCatalog : ScriptableObject
    {
        [SerializeField] private PausePopupView _pausePrefab;
        [SerializeField] private ObjectiveInfoPopupView _objectiveInfoPrefab;
        [SerializeField] private ConfirmPopupView _confirmPrefab;
        [SerializeField] private TooltipPopupView _tooltipPrefab;
        [SerializeField] private RewardPopupView _rewardPrefab;

        public PausePopupView PausePrefab => _pausePrefab;

        public ObjectiveInfoPopupView ObjectiveInfoPrefab => _objectiveInfoPrefab;

        public ConfirmPopupView ConfirmPrefab => _confirmPrefab;

        public TooltipPopupView TooltipPrefab => _tooltipPrefab;

        public RewardPopupView RewardPrefab => _rewardPrefab;
    }
}
