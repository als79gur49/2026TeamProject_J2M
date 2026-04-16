using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _tooltipStatusLabel;
        [SerializeField] private Text _largeTextStatusLabel;
        [SerializeField] private Button _tooltipToggleButton;
        [SerializeField] private Button _largeTextToggleButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _tooltipToggleButtonLabel;
        [SerializeField] private Text _largeTextToggleButtonLabel;
        [SerializeField] private Text _backButtonLabel;

        private SettingsScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action TooltipToggleRequested;


        public event Action LargeTextToggleRequested;

        public event Action BackRequested;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(SettingsScreenViewModel viewModel)
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

        public void ClickTooltipToggle()
        {
            if (!IsVisible)
            {
                return;
            }

            TooltipToggleRequested?.Invoke();
        }

        public void ClickLargeTextToggle()
        {
            if (!IsVisible)
            {
                return;
            }

            LargeTextToggleRequested?.Invoke();
        }

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
        }

        private void OnEnable()
        {
            RebindButton(_tooltipToggleButton, ClickTooltipToggle);
            RebindButton(_largeTextToggleButton, ClickLargeTextToggle);
            RebindButton(_backButton, ClickBack);
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindButton(_tooltipToggleButton, ClickTooltipToggle);
            UnbindButton(_largeTextToggleButton, ClickLargeTextToggle);
            UnbindButton(_backButton, ClickBack);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_tooltipStatusLabel, nameof(_tooltipStatusLabel));
            ValidateSerializedReference(_largeTextStatusLabel, nameof(_largeTextStatusLabel));
            ValidateSerializedReference(_tooltipToggleButton, nameof(_tooltipToggleButton));
            ValidateSerializedReference(_largeTextToggleButton, nameof(_largeTextToggleButton));
            ValidateSerializedReference(_backButton, nameof(_backButton));
            ValidateSerializedReference(_tooltipToggleButtonLabel, nameof(_tooltipToggleButtonLabel));
            ValidateSerializedReference(_largeTextToggleButtonLabel, nameof(_largeTextToggleButtonLabel));
            ValidateSerializedReference(_backButtonLabel, nameof(_backButtonLabel));
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

            if (_tooltipStatusLabel != null)
            {
                _tooltipStatusLabel.text = _viewModel.TooltipStatusText;
            }

            if (_largeTextStatusLabel != null)
            {
                _largeTextStatusLabel.text = _viewModel.LargeTextStatusText;
            }

            if (_tooltipToggleButtonLabel != null)
            {
                _tooltipToggleButtonLabel.text = _viewModel.TooltipToggleLabel;
            }

            if (_largeTextToggleButtonLabel != null)
            {
                _largeTextToggleButtonLabel.text = _viewModel.LargeTextToggleLabel;
            }

            if (_backButtonLabel != null)
            {
                _backButtonLabel.text = _viewModel.BackLabel;
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
                Debug.LogWarning($"{nameof(SettingsScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
