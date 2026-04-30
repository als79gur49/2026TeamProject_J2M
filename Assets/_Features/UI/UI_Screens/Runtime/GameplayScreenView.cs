using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class GameplayScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _helpButton;
        [SerializeField] private Button _objectiveButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _helpButtonLabel;
        [SerializeField] private TMP_Text _objectiveButtonLabel;
        [SerializeField] private TMP_Text _settingsButtonLabel;

        private GameplayScreenViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private CanvasGroup _rootCanvasGroup;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private float _rootRestAlpha = 1f;

        public event Action HelpRequested;

        public event Action ObjectivesRequested;

        public event Action SettingsRequested;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(GameplayScreenViewModel viewModel)
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

        public void ClickHelp()
        {
            if (!IsVisible)
            {
                return;
            }

            HelpRequested?.Invoke();
        }

        public void ClickObjectives()
        {
            if (!IsVisible)
            {
                return;
            }

            ObjectivesRequested?.Invoke();
        }

        public void ClickSettings()
        {
            if (!IsVisible)
            {
                return;
            }

            SettingsRequested?.Invoke();
        }

        private void OnEnable()
        {
            RebindButton(_helpButton, ClickHelp);
            RebindButton(_objectiveButton, ClickObjectives);
            RebindButton(_settingsButton, ClickSettings);
            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_helpButton, ClickHelp);
            UnbindButton(_objectiveButton, ClickObjectives);
            UnbindButton(_settingsButton, ClickSettings);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_helpButton, nameof(_helpButton));
            ValidateSerializedReference(_objectiveButton, nameof(_objectiveButton));
            ValidateSerializedReference(_settingsButton, nameof(_settingsButton));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_helpButtonLabel, nameof(_helpButtonLabel));
            ValidateSerializedReference(_objectiveButtonLabel, nameof(_objectiveButtonLabel));
            ValidateSerializedReference(_settingsButtonLabel, nameof(_settingsButtonLabel));
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

            if (_helpButtonLabel != null)
            {
                _helpButtonLabel.text = _viewModel.HelpLabel;
            }

            if (_objectiveButtonLabel != null)
            {
                _objectiveButtonLabel.text = _viewModel.ObjectivesLabel;
            }

            if (_settingsButtonLabel != null)
            {
                _settingsButtonLabel.text = _viewModel.SettingsLabel;
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
                Debug.LogWarning($"{nameof(GameplayScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
