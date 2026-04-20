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
        [SerializeField] private Button _tooltipInfoButton;
        [SerializeField] private Button _tooltipToggleButton;
        [SerializeField] private Button _largeTextToggleButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _tooltipToggleButtonLabel;
        [SerializeField] private Text _largeTextToggleButtonLabel;
        [SerializeField] private Text _backButtonLabel;

        private SettingsAudioView _audioView;
        private SettingsDisplayView _displayView;
        private bool _isVisible;
        private SettingsScreenViewModel _viewModel;

        public event Action TooltipToggleRequested;

        public event Action TooltipInfoRequested;

        public event Action LargeTextToggleRequested;

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

        public SettingsAudioView AudioView => EnsureAudioView();

        public SettingsDisplayView DisplayView => EnsureDisplayView();

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

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
        }

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
            EnsureSectionViews();
            RebindButton(_tooltipInfoButton, ClickTooltipInfo);
            RebindButton(_tooltipToggleButton, ClickTooltipToggle);
            RebindButton(_largeTextToggleButton, ClickLargeTextToggle);
            RebindButton(_backButton, ClickBack);
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindButton(_tooltipInfoButton, ClickTooltipInfo);
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

        private SettingsAudioView EnsureAudioView()
        {
            if (_audioView != null)
            {
                return _audioView;
            }

            var audioRoot = EnsureSectionRoot("SettingsAudioSection");
            if (audioRoot == null)
            {
                return null;
            }

            _audioView = audioRoot.GetComponent<SettingsAudioView>() ?? audioRoot.gameObject.AddComponent<SettingsAudioView>();
            return _audioView;
        }

        private SettingsDisplayView EnsureDisplayView()
        {
            if (_displayView != null)
            {
                return _displayView;
            }

            var displayRoot = EnsureSectionRoot("SettingsDisplaySection");
            if (displayRoot == null)
            {
                return null;
            }

            _displayView = displayRoot.GetComponent<SettingsDisplayView>() ?? displayRoot.gameObject.AddComponent<SettingsDisplayView>();
            return _displayView;
        }

        private RectTransform EnsureSectionRoot(string objectName)
        {
            if (_root == null)
            {
                return null;
            }

            var sectionRoot = _root.transform.Find(objectName) as RectTransform;
            if (sectionRoot != null)
            {
                return sectionRoot;
            }

            var sectionObject = new GameObject(objectName, typeof(RectTransform));
            sectionObject.transform.SetParent(_root.transform, false);
            return sectionObject.GetComponent<RectTransform>();
        }

        private void EnsureSectionViews()
        {
            EnsureAudioView();
            EnsureDisplayView();
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            EnsureSectionViews();

            if (_audioView != null)
            {
                _audioView.SetIsVisible(IsVisible);
            }

            if (_displayView != null)
            {
                _displayView.SetIsVisible(IsVisible);
            }

            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

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
            LayoutRect(_audioView != null ? _audioView.transform as RectTransform : null, new Vector2(24f, -58f), new Vector2(412f, 124f));
            LayoutRect(_displayView != null ? _displayView.transform as RectTransform : null, new Vector2(24f, -194f), new Vector2(412f, 248f));
            LayoutRect(_tooltipStatusLabel != null ? _tooltipStatusLabel.rectTransform : null, new Vector2(24f, -458f), new Vector2(160f, 22f));
            LayoutRect(_tooltipInfoButton != null ? _tooltipInfoButton.GetComponent<RectTransform>() : null, new Vector2(188f, -452f), new Vector2(24f, 28f));
            LayoutRect(_tooltipToggleButton != null ? _tooltipToggleButton.GetComponent<RectTransform>() : null, new Vector2(220f, -452f), new Vector2(140f, 28f));
            LayoutRect(_largeTextStatusLabel != null ? _largeTextStatusLabel.rectTransform : null, new Vector2(24f, -504f), new Vector2(160f, 22f));
            LayoutRect(_largeTextToggleButton != null ? _largeTextToggleButton.GetComponent<RectTransform>() : null, new Vector2(220f, -498f), new Vector2(140f, 28f));
            LayoutRect(_backButton != null ? _backButton.GetComponent<RectTransform>() : null, new Vector2(181f, -554f), new Vector2(98f, 30f));
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
