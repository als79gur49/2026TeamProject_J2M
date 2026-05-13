using UnityEngine;

namespace Game.Feature.UI.ViewShared
{
    [CreateAssetMenu(
        fileName = "UiSelectionVisualProfile",
        menuName = "UI/Selection Visual Profile")]
    public sealed class UiSelectionVisualProfile : ScriptableObject
    {
        [SerializeField] private Color _selectedFrameColor = Color.white;
        [SerializeField] private Color _editFrameColor = Color.yellow;
        [SerializeField] private Color _unselectedFrameColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private bool _hideUnselectedFrames = true;
        [SerializeField] private Sprite _frameSprite;

        public Color SelectedFrameColor => _selectedFrameColor;

        public Color EditFrameColor => _editFrameColor;

        public Color UnselectedFrameColor => _unselectedFrameColor;

        public bool HideUnselectedFrames => _hideUnselectedFrames;

        public Sprite FrameSprite => _frameSprite;

        public static UiSelectionVisualProfile CreateRuntimeDefault()
        {
            var profile = CreateInstance<UiSelectionVisualProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }
    }
}
