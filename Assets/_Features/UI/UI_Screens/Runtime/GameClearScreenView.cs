using System;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class GameClearScreenView : MonoBehaviour, IScreenView, IUiNavigationTarget
    {
        private const int MainSelectionIndex = 0;

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Button _restartLevelButton;
        [SerializeField] private TMP_Text _restartLevelButtonLabel;
        [SerializeField] private Button _mainButton;
        [SerializeField] private TMP_Text _mainButtonLabel;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();

        private GameClearScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action MainRequested;

        public bool CanHandleUiNavigation => IsVisible && isActiveAndEnabled;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(GameClearScreenViewModel viewModel)
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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickMain()
        {
            if (!IsVisible)
            {
                return;
            }

            MainRequested?.Invoke();
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            return false;
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            _navigationGroup?.PlaySelectedSubmitFeedback();
            ClickMain();
            return true;
        }

        public bool HandleCancel()
        {
            return false;
        }

        public void OnNavigationFocusGained()
        {
            _navigationGroup?.SetSelectedIndex(MainSelectionIndex);
        }

        public void OnNavigationFocusLost()
        {
            _navigationGroup?.HideAllFrames();
        }

        private void OnEnable()
        {
            RebindButton(_mainButton, ClickMain);
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindButton(_mainButton, ClickMain);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_mainButton, nameof(_mainButton));
            ValidateSerializedReference(_mainButtonLabel, nameof(_mainButtonLabel));
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
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            HideUnusedAuthoredElements();

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_mainButtonLabel != null)
            {
                _mainButtonLabel.text = _viewModel.MainLabel;
            }
        }

        private void HideUnusedAuthoredElements()
        {
            if (_detailLabel != null)
            {
                _detailLabel.text = string.Empty;
                _detailLabel.gameObject.SetActive(false);
            }

            if (_restartLevelButton != null)
            {
                _restartLevelButton.gameObject.SetActive(false);
            }

            if (_restartLevelButtonLabel != null)
            {
                _restartLevelButtonLabel.text = string.Empty;
            }
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
                Debug.LogWarning($"{nameof(GameClearScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
