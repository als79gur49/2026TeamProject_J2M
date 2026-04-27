using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsScreenView : MonoBehaviour, IScreenView
    {
        public const string AudioSectionName = "SettingsAudioSection";
        public const string DisplaySectionName = "SettingsDisplaySection";
        public const string InputSectionName = "SettingsInputSection";

        private const string MissingAudioSectionMessage =
            "Settings screen is missing or miswired required authored audio section. Repair: assign SettingsScreenView._audioView to the SettingsAudioSection child view.";

        private const string MissingDisplaySectionMessage =
            "Settings screen is missing or miswired required authored display section. Repair: assign SettingsScreenView._displayView to the SettingsDisplaySection child view.";

        private const string MissingInputSectionMessage =
            "Settings screen is missing or miswired required authored input section. Repair: assign SettingsScreenView._inputView to the SettingsInputSection child view.";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _tooltipStatusLabel;
        [SerializeField] private TMP_Text _largeTextStatusLabel;
        [SerializeField] private Button _tooltipInfoButton;
        [SerializeField] private Button _audioTabButton;
        [SerializeField] private TMP_Text _audioTabButtonLabel;
        [SerializeField] private Button _displayTabButton;
        [SerializeField] private TMP_Text _displayTabButtonLabel;
        [SerializeField] private Button _inputTabButton;
        [SerializeField] private TMP_Text _inputTabButtonLabel;
        [SerializeField] private Button _tooltipToggleButton;
        [SerializeField] private Button _largeTextToggleButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _tooltipToggleButtonLabel;
        [SerializeField] private TMP_Text _largeTextToggleButtonLabel;
        [SerializeField] private TMP_Text _backButtonLabel;
        [SerializeField] private SettingsAudioView _audioView;
        [SerializeField] private SettingsDisplayView _displayView;
        [SerializeField] private SettingsInputView _inputView;

        private bool _isVisible;
        private SettingsScreenViewModel _viewModel;
        private Tween _enterTween;
        private CanvasGroup _rootCanvasGroup;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private float _rootRestAlpha = 1f;

        public event Action TooltipToggleRequested;

        public event Action TooltipInfoRequested;

        public event Action LargeTextToggleRequested;

        public event Action<SettingsSectionId> SectionSelected;

        public event Action BackRequested;

        public string CurrentDisplayValueText =>
            DisplayView != null ? DisplayView.CurrentDisplayValueText : string.Empty;

        public string DisplayStatusText =>
            DisplayView != null ? DisplayView.DisplayStatusText : string.Empty;

        public bool IsDisplayApplyInteractable =>
            DisplayView != null && DisplayView.IsDisplayApplyInteractable;

        public bool IsDisplayRevertInteractable =>
            DisplayView != null && DisplayView.IsDisplayRevertInteractable;

        public int SelectedDisplayResolutionIndex =>
            DisplayView != null ? DisplayView.SelectedResolutionIndex : 0;

        public bool IsDisplayFullscreenOn =>
            DisplayView != null && DisplayView.IsFullscreenOn;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public SettingsAudioView AudioView => _audioView;

        public SettingsDisplayView DisplayView => _displayView;

        public SettingsInputView InputView => _inputView;

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

        public void ValidateAuthoredStructureOrThrow()
        {
            ValidateSection(_audioView, nameof(_audioView), AudioSectionName, MissingAudioSectionMessage);
            ValidateSection(_displayView, nameof(_displayView), DisplaySectionName, MissingDisplaySectionMessage);
            ValidateSection(_inputView, nameof(_inputView), InputSectionName, MissingInputSectionMessage);
        }

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
        }

        // Temporary compatibility passthroughs only; no section-local logic belongs here.
        public void ClickDisplayApply()
        {
            if (!IsVisible || DisplayView == null)
            {
                return;
            }

            DisplayView.ClickApply();
        }

        public void ClickDisplayRevert()
        {
            if (!IsVisible || DisplayView == null)
            {
                return;
            }

            DisplayView.ClickRevert();
        }

        public void ClickAudioTab()
        {
            if (!IsVisible)
            {
                return;
            }

            SectionSelected?.Invoke(SettingsSectionId.Audio);
        }

        public void ClickDisplayTab()
        {
            if (!IsVisible)
            {
                return;
            }

            SectionSelected?.Invoke(SettingsSectionId.Display);
        }

        public void ClickInputTab()
        {
            if (!IsVisible)
            {
                return;
            }

            SectionSelected?.Invoke(SettingsSectionId.Input);
        }

        public void ClickLargeTextToggle()
        {
            if (!IsVisible)
            {
                return;
            }

            LargeTextToggleRequested?.Invoke();
        }

        public void ClickTooltipInfo()
        {
            if (!IsVisible)
            {
                return;
            }

            TooltipInfoRequested?.Invoke();
        }

        public void ClickTooltipToggle()
        {
            if (!IsVisible)
            {
                return;
            }

            TooltipToggleRequested?.Invoke();
        }

        public void CommitAudioInteraction(AudioSettingsChannel channel)
        {
            if (!IsVisible || AudioView == null)
            {
                return;
            }

            AudioView.CommitInteraction(channel);
        }

        public void SelectDisplayResolution(int index)
        {
            if (!IsVisible || DisplayView == null)
            {
                return;
            }

            DisplayView.SelectResolution(index);
        }

        public void SetAudioMuted(AudioSettingsChannel channel, bool isMuted)
        {
            if (!IsVisible || AudioView == null)
            {
                return;
            }

            AudioView.SetMuted(channel, isMuted);
        }

        public void SetAudioVolume(AudioSettingsChannel channel, float value)
        {
            if (!IsVisible || AudioView == null)
            {
                return;
            }

            AudioView.SetVolume(channel, value);
        }

        public void SetDisplayFullscreen(bool isFullscreen)
        {
            if (!IsVisible || DisplayView == null)
            {
                return;
            }

            DisplayView.SetFullscreen(isFullscreen);
        }

        private void OnEnable()
        {
            RebindButton(_tooltipInfoButton, ClickTooltipInfo);
            RebindButton(_audioTabButton, ClickAudioTab);
            RebindButton(_displayTabButton, ClickDisplayTab);
            RebindButton(_inputTabButton, ClickInputTab);
            RebindButton(_tooltipToggleButton, ClickTooltipToggle);
            RebindButton(_largeTextToggleButton, ClickLargeTextToggle);
            RebindButton(_backButton, ClickBack);
            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_tooltipInfoButton, ClickTooltipInfo);
            UnbindButton(_audioTabButton, ClickAudioTab);
            UnbindButton(_displayTabButton, ClickDisplayTab);
            UnbindButton(_inputTabButton, ClickInputTab);
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
            ValidateSerializedReference(_tooltipInfoButton, nameof(_tooltipInfoButton));
            ValidateSerializedReference(_audioTabButton, nameof(_audioTabButton));
            ValidateSerializedReference(_audioTabButtonLabel, nameof(_audioTabButtonLabel));
            ValidateSerializedReference(_displayTabButton, nameof(_displayTabButton));
            ValidateSerializedReference(_displayTabButtonLabel, nameof(_displayTabButtonLabel));
            ValidateSerializedReference(_inputTabButton, nameof(_inputTabButton));
            ValidateSerializedReference(_inputTabButtonLabel, nameof(_inputTabButtonLabel));
            ValidateSerializedReference(_tooltipToggleButton, nameof(_tooltipToggleButton));
            ValidateSerializedReference(_largeTextToggleButton, nameof(_largeTextToggleButton));
            ValidateSerializedReference(_backButton, nameof(_backButton));
            ValidateSerializedReference(_tooltipToggleButtonLabel, nameof(_tooltipToggleButtonLabel));
            ValidateSerializedReference(_largeTextToggleButtonLabel, nameof(_largeTextToggleButtonLabel));
            ValidateSerializedReference(_backButtonLabel, nameof(_backButtonLabel));
            ValidateSerializedReference(_audioView, nameof(_audioView));
            ValidateSerializedReference(_displayView, nameof(_displayView));
            ValidateSerializedReference(_inputView, nameof(_inputView));
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
            ApplySectionVisibility();

            ApplyRootVisibility();

            ApplySettingsLayout();

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

            if (_audioTabButtonLabel != null)
            {
                _audioTabButtonLabel.text = _viewModel.AudioTabLabel;
            }

            if (_displayTabButtonLabel != null)
            {
                _displayTabButtonLabel.text = _viewModel.DisplayTabLabel;
            }

            if (_inputTabButtonLabel != null)
            {
                _inputTabButtonLabel.text = _viewModel.InputTabLabel;
            }

            if (_audioTabButton != null)
            {
                _audioTabButton.interactable = _viewModel.SelectedSection != SettingsSectionId.Audio;
            }

            if (_displayTabButton != null)
            {
                _displayTabButton.interactable = _viewModel.SelectedSection != SettingsSectionId.Display;
            }

            if (_inputTabButton != null)
            {
                _inputTabButton.interactable = _viewModel.SelectedSection != SettingsSectionId.Input;
            }
        }

        private void ApplySectionVisibility()
        {
            var selectedSection = _viewModel != null ? _viewModel.SelectedSection : SettingsSectionId.Audio;
            ApplySectionVisibility(_audioView, IsVisible && selectedSection == SettingsSectionId.Audio);
            ApplySectionVisibility(_displayView, IsVisible && selectedSection == SettingsSectionId.Display);
            ApplySectionVisibility(_inputView, IsVisible && selectedSection == SettingsSectionId.Input);
        }

        private static void ApplySectionVisibility(Component sectionView, bool isVisible)
        {
            if (sectionView == null)
            {
                return;
            }

            if (isVisible && !sectionView.gameObject.activeSelf)
            {
                sectionView.gameObject.SetActive(true);
            }

            if (sectionView is SettingsAudioView audioView)
            {
                audioView.SetIsVisible(isVisible);
            }
            else if (sectionView is SettingsDisplayView displayView)
            {
                displayView.SetIsVisible(isVisible);
            }
            else if (sectionView is SettingsInputView inputView)
            {
                inputView.SetIsVisible(isVisible);
            }

            if (!isVisible && sectionView.gameObject.activeSelf)
            {
                sectionView.gameObject.SetActive(false);
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

        private void ApplySettingsLayout()
        {
            if (_root == null)
            {
                return;
            }

            var rootRect = _root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                var size = rootRect.sizeDelta;
                size.x = Mathf.Max(size.x, 460f);
                size.y = Mathf.Max(size.y, 600f);
                rootRect.sizeDelta = size;
            }

            LayoutRect(_titleLabel != null ? _titleLabel.rectTransform : null, new Vector2(16f, -16f), new Vector2(428f, 24f));
            LayoutRect(_audioTabButton != null ? _audioTabButton.GetComponent<RectTransform>() : null, new Vector2(24f, -52f), new Vector2(116f, 30f));
            LayoutRect(_displayTabButton != null ? _displayTabButton.GetComponent<RectTransform>() : null, new Vector2(148f, -52f), new Vector2(116f, 30f));
            LayoutRect(_inputTabButton != null ? _inputTabButton.GetComponent<RectTransform>() : null, new Vector2(272f, -52f), new Vector2(116f, 30f));
            LayoutRect(_audioView != null ? _audioView.transform as RectTransform : null, new Vector2(24f, -102f), new Vector2(412f, 124f));
            LayoutRect(_displayView != null ? _displayView.transform as RectTransform : null, new Vector2(24f, -102f), new Vector2(412f, 248f));
            LayoutRect(_inputView != null ? _inputView.transform as RectTransform : null, new Vector2(24f, -102f), new Vector2(412f, 248f));
            LayoutRect(_tooltipStatusLabel != null ? _tooltipStatusLabel.rectTransform : null, new Vector2(24f, -418f), new Vector2(160f, 22f));
            LayoutRect(_tooltipInfoButton != null ? _tooltipInfoButton.GetComponent<RectTransform>() : null, new Vector2(188f, -412f), new Vector2(24f, 28f));
            LayoutRect(_tooltipToggleButton != null ? _tooltipToggleButton.GetComponent<RectTransform>() : null, new Vector2(220f, -412f), new Vector2(140f, 28f));
            LayoutRect(_largeTextStatusLabel != null ? _largeTextStatusLabel.rectTransform : null, new Vector2(24f, -464f), new Vector2(160f, 22f));
            LayoutRect(_largeTextToggleButton != null ? _largeTextToggleButton.GetComponent<RectTransform>() : null, new Vector2(220f, -458f), new Vector2(140f, 28f));
            LayoutRect(_backButton != null ? _backButton.GetComponent<RectTransform>() : null, new Vector2(181f, -554f), new Vector2(98f, 30f));
        }

        private void ValidateSection(Component sectionView, string fieldName, string expectedSectionName, string baseMessage)
        {
            var issues = new List<string>();
            var expectedParent = _root != null ? _root.transform : transform;

            if (sectionView == null)
            {
                issues.Add($"serialized reference '{fieldName}' is not assigned");
                throw new InvalidOperationException(BuildValidationMessage(baseMessage, issues));
            }

            if (!string.Equals(sectionView.name, expectedSectionName, StringComparison.Ordinal))
            {
                issues.Add($"serialized reference '{fieldName}' resolved '{sectionView.name}' instead of '{expectedSectionName}'");
            }

            if (sectionView.transform.parent != expectedParent)
            {
                issues.Add($"'{expectedSectionName}' must remain a direct child of '{expectedParent.name}'");
            }

            if (issues.Count > 0)
            {
                throw new InvalidOperationException(BuildValidationMessage(baseMessage, issues));
            }
        }

        private static string BuildValidationMessage(string baseMessage, IReadOnlyList<string> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return baseMessage;
            }

            return $"{baseMessage} Details: {string.Join("; ", issues)}.";
        }

        private static void LayoutRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
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
