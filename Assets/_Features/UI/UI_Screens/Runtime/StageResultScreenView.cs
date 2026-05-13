using System;
using DG.Tweening;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class StageResultScreenView : MonoBehaviour, IScreenView, IUiNavigationTarget
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Button _continueButton;
        [SerializeField] private TMP_Text _continueButtonLabel;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();

        private StageResultScreenViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private CanvasGroup _rootCanvasGroup;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private float _rootRestAlpha = 1f;

        public event Action ContinueRequested;

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

        public void Bind(StageResultScreenViewModel viewModel)
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

        public void ClickContinue()
        {
            if (!IsVisible)
            {
                return;
            }

            ContinueRequested?.Invoke();
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

            ClickContinue();
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
            RebindButton(_continueButton, ClickContinue);
            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_continueButton, ClickContinue);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_summaryLabel, nameof(_summaryLabel));
            ValidateSerializedReference(_detailLabel, nameof(_detailLabel));
            ValidateSerializedReference(_continueButton, nameof(_continueButton));
            ValidateSerializedReference(_continueButtonLabel, nameof(_continueButtonLabel));
        }
#endif

        private void OnDestroy()
        {
            StopRootEnterMotion();
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
            ApplyRootVisibility();

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = _viewModel.SummaryText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }

            if (_continueButtonLabel != null)
            {
                _continueButtonLabel.text = _viewModel.ContinueLabel;
            }
        }

        private void ApplyRootVisibility()
        {
            var becameVisible = !_lastVisibleState && IsVisible;
            var becameHidden = _lastVisibleState && !IsVisible;

            if (becameVisible)
            {
                _lastVisibleState = true;
                if (_root != null)
                {
                    _root.SetActive(true);
                }

                PlayRootEnterMotion();
                return;
            }

            if (becameHidden || !IsVisible)
            {
                StopRootEnterMotion();
            }

            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            _lastVisibleState = IsVisible;
        }

        private void PlayRootEnterMotion()
        {
            _rootCanvasGroup = ScreenEnterTweenUtility.EnsureCanvasGroup(_root, _rootCanvasGroup);
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            _enterTween = ScreenEnterTweenUtility.PlayEnterFade(_rootCanvasGroup, out _rootRestAlpha);
            _hasRootRestAlpha = _rootCanvasGroup != null;
        }

        private void StopRootEnterMotion()
        {
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            if (_hasRootRestAlpha)
            {
                ScreenEnterTweenUtility.RestoreAlpha(_rootCanvasGroup, _rootRestAlpha);
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
                Debug.LogWarning($"{nameof(StageResultScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }

}
