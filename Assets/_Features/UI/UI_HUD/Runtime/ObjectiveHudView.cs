using TMPro;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class ObjectiveHudView : MonoBehaviour
    {
        private const string CompleteColor = "#8EE6A8";
        private const string ActiveColor = "#FFFFFF";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _objectiveLabel;

        private ObjectiveHudViewModel _viewModel;

        public ObjectiveHudViewModel ViewModel => _viewModel;

        public void Bind(ObjectiveHudViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_objectiveLabel, nameof(_objectiveLabel));
        }
#endif

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            var isVisible = _viewModel != null && _viewModel.IsVisible;
            if (_root != null)
            {
                _root.SetActive(isVisible);
            }

            if (_objectiveLabel == null)
            {
                return;
            }

            _objectiveLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _objectiveLabel.overflowMode = TextOverflowModes.Ellipsis;
            _objectiveLabel.color = _viewModel != null && _viewModel.IsComplete
                ? ParseColor(CompleteColor)
                : ParseColor(ActiveColor);
            _objectiveLabel.text = isVisible ? _viewModel.ObjectiveText : string.Empty;
        }

        private static Color ParseColor(string htmlString)
        {
            return ColorUtility.TryParseHtmlString(htmlString, out var color) ? color : Color.white;
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(ObjectiveHudView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
