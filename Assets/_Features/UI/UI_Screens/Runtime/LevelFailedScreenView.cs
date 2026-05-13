using System;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class LevelFailedScreenView : MonoBehaviour, IScreenView, IUiNavigationTarget
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Button _restartLevelButton;
        [SerializeField] private TMP_Text _restartLevelButtonLabel;
        [SerializeField] private Button _mainButton;
        [SerializeField] private TMP_Text _mainButtonLabel;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();

        private LevelFailedScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action RestartLevelRequested;

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

        public void Bind(LevelFailedScreenViewModel viewModel)
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

        public void ClickRestartLevel()
        {
            if (!IsVisible)
            {
                return;
            }

            RestartLevelRequested?.Invoke();
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
            if (!CanHandleUiNavigation || _navigationGroup == null)
            {
                return false;
            }

            switch (command)
            {
                case UiNavigationCommand.Up:
                    return _navigationGroup.TryMove(-1);

                case UiNavigationCommand.Down:
                    return _navigationGroup.TryMove(1);

                default:
                    return false;
            }
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            var selected = _navigationGroup != null ? _navigationGroup.GetSelectedButton() : null;
            if (selected == _mainButton)
            {
                ClickMain();
                return true;
            }

            if (selected == _restartLevelButton || selected == null)
            {
                ClickRestartLevel();
                return true;
            }

            selected.onClick.Invoke();
            return true;
        }

        public bool HandleCancel()
        {
            return false;
        }

        public void OnNavigationFocusGained()
        {
            _navigationGroup?.SetSelectedIndex(0);
        }

        public void OnNavigationFocusLost()
        {
            _navigationGroup?.HideAllFrames();
        }

        private void OnEnable()
        {
            RebindButton(_restartLevelButton, ClickRestartLevel);
            RebindButton(_mainButton, ClickMain);
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindButton(_restartLevelButton, ClickRestartLevel);
            UnbindButton(_mainButton, ClickMain);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_detailLabel, nameof(_detailLabel));
            ValidateSerializedReference(_restartLevelButton, nameof(_restartLevelButton));
            ValidateSerializedReference(_restartLevelButtonLabel, nameof(_restartLevelButtonLabel));
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

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }

            if (_restartLevelButtonLabel != null)
            {
                _restartLevelButtonLabel.text = _viewModel.RestartLevelLabel;
            }

            if (_mainButtonLabel != null)
            {
                _mainButtonLabel.text = _viewModel.MainLabel;
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
                Debug.LogWarning($"{nameof(LevelFailedScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
