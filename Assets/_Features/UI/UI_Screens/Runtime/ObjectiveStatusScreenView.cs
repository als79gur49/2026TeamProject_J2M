using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class ObjectiveStatusScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _badgeLabel;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private TMP_Text _secondaryLabel;
        [SerializeField] private Button _overviewButton;
        [SerializeField] private Button _sessionButton;
        [SerializeField] private Button _infoButton;
        [SerializeField] private Button _backButton;

        private ObjectiveStatusScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action OverviewRequested;

        public event Action SessionRequested;

        public event Action InfoRequested;

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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void Bind(ObjectiveStatusScreenViewModel viewModel)
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

        public void ClickOverview()
        {
            if (!IsVisible)
            {
                return;
            }

            OverviewRequested?.Invoke();
        }

        public void ClickSession()
        {
            if (!IsVisible)
            {
                return;
            }

            SessionRequested?.Invoke();
        }

        public void ClickInfo()
        {
            if (!IsVisible)
            {
                return;
            }

            InfoRequested?.Invoke();
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
            RebindButton(_overviewButton, ClickOverview);
            RebindButton(_sessionButton, ClickSession);
            RebindButton(_infoButton, ClickInfo);
            RebindButton(_backButton, ClickBack);
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindButton(_overviewButton, ClickOverview);
            UnbindButton(_sessionButton, ClickSession);
            UnbindButton(_infoButton, ClickInfo);
            UnbindButton(_backButton, ClickBack);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_badgeLabel, nameof(_badgeLabel));
            ValidateSerializedReference(_summaryLabel, nameof(_summaryLabel));
            ValidateSerializedReference(_detailLabel, nameof(_detailLabel));
            ValidateSerializedReference(_secondaryLabel, nameof(_secondaryLabel));
            ValidateSerializedReference(_overviewButton, nameof(_overviewButton));
            ValidateSerializedReference(_sessionButton, nameof(_sessionButton));
            ValidateSerializedReference(_infoButton, nameof(_infoButton));
            ValidateSerializedReference(_backButton, nameof(_backButton));
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

            if (_badgeLabel != null)
            {
                _badgeLabel.text = _viewModel.BadgeText;
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = _viewModel.SummaryText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }

            if (_secondaryLabel != null)
            {
                _secondaryLabel.text = _viewModel.SecondaryText;
            }

            if (_overviewButton != null)
            {
                _overviewButton.interactable = !_viewModel.IsOverviewSelected;
            }

            if (_sessionButton != null)
            {
                _sessionButton.interactable = !_viewModel.IsSessionSelected;
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
                Debug.LogWarning($"{nameof(ObjectiveStatusScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
