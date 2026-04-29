using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class ObjectiveHudView : MonoBehaviour
    {
        private const string CompleteColor = "#8EE6A8";
        private const string ActiveColor = "#FFFFFF";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _objectiveLabel;
        [SerializeField] private Button _dropdownButton;

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

        public void ClickDropdown()
        {
            _viewModel?.ToggleExpanded();
        }

        private void OnEnable()
        {
            RebindButton(_dropdownButton, ClickDropdown);
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindButton(_dropdownButton, ClickDropdown);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_objectiveLabel, nameof(_objectiveLabel));
            ValidateSerializedReference(_dropdownButton, nameof(_dropdownButton));
        }
#endif

        private void OnDestroy()
        {
            UnbindButton(_dropdownButton, ClickDropdown);
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

            if (_dropdownButton != null)
            {
                _dropdownButton.interactable = isVisible && _viewModel != null && _viewModel.CanExpand;
            }

            _objectiveLabel.textWrappingMode = _viewModel != null && _viewModel.IsExpanded
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;
            _objectiveLabel.overflowMode = _viewModel != null && _viewModel.IsExpanded
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Ellipsis;
            _objectiveLabel.color = _viewModel != null && _viewModel.IsComplete
                ? ParseColor(CompleteColor)
                : ParseColor(ActiveColor);
            _objectiveLabel.text = isVisible ? _viewModel.ObjectiveText : string.Empty;
        }

        private static Color ParseColor(string htmlString)
        {
            return ColorUtility.TryParseHtmlString(htmlString, out var color) ? color : Color.white;
        }

        private static void RebindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
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
