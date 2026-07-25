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
        [SerializeField] private ConfirmPopupView _confirmPrefab;
        [SerializeField] private GameplayUiTypographyTheme _typographyTheme;

        public PausePopupView PausePrefab => _pausePrefab;

        public ConfirmPopupView ConfirmPrefab => _confirmPrefab;

        internal GameplayUiTypographyTheme TypographyTheme => _typographyTheme;
    }
}
