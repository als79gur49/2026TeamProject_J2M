using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsAudioView : MonoBehaviour
    {
        private const string MissingControlsMessage =
            "Settings audio section is missing required authored controls. Repair: open SettingsScreen.prefab and assign every SettingsAudioView serialized row reference.";

        [SerializeField] private AudioControlRowRefs _mainRow = new();
        [SerializeField] private AudioControlRowRefs _bgmRow = new();
        [SerializeField] private AudioControlRowRefs _sfxRow = new();

        private readonly Dictionary<AudioSettingsChannel, AudioControlRowRefs> _audioControls = new();
        private readonly Dictionary<AudioSettingsChannel, UnityAction<float>> _sliderHandlers = new();
        private readonly Dictionary<AudioSettingsChannel, UnityAction<bool>> _toggleHandlers = new();
        private bool _isVisible;
        private SettingsAudioViewModel _viewModel;

        public event Action<AudioSettingsChannel, float> VolumeChanged;

        public event Action<AudioSettingsChannel, bool> MuteChanged;

        public event Action InteractionCompleted;

        public void Bind(SettingsAudioViewModel viewModel)
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

        public void ValidateAuthoredControlsOrThrow()
        {
            CacheAudioControls();

            var issues = new List<string>();
            ValidateRow(_mainRow, "MainAudioRow", issues);
            ValidateRow(_bgmRow, "BgmAudioRow", issues);
            ValidateRow(_sfxRow, "SfxAudioRow", issues);

            if (issues.Count > 0)
            {
                throw new InvalidOperationException(BuildValidationMessage(MissingControlsMessage, issues));
            }
        }

        public void CommitInteraction(AudioSettingsChannel channel)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.InteractionRelay != null)
            {
                widgets.InteractionRelay.RaiseInteractionCompleted();
            }
        }

        public void SetIsVisible(bool isVisible)
        {
            _isVisible = isVisible;
            RefreshView();
        }

        public void SetMuted(AudioSettingsChannel channel, bool isMuted)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.Toggle != null)
            {
                widgets.Toggle.isOn = isMuted;
            }
        }

        public void SetVolume(AudioSettingsChannel channel, float value)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.Slider != null)
            {
                widgets.Slider.value = Mathf.Clamp01(value);
            }
        }

        private void OnEnable()
        {
            CacheAudioControls();
            RebindAudioControls();
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindAudioControls();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateRowSerializedReferences(_mainRow, nameof(_mainRow));
            ValidateRowSerializedReferences(_bgmRow, nameof(_bgmRow));
            ValidateRowSerializedReferences(_sfxRow, nameof(_sfxRow));
        }
#endif

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            UnbindAudioControls();
        }

        private void ApplyLayout()
        {
            SettingsLayoutUtility.EnsureVerticalLayout(
                gameObject,
                new RectOffset(0, 0, 0, 0),
                14f,
                TextAnchor.UpperLeft);
            LayoutAudioRow(AudioSettingsChannel.Main);
            LayoutAudioRow(AudioSettingsChannel.Bgm);
            LayoutAudioRow(AudioSettingsChannel.Sfx);
            SetRowSiblingIndex(AudioSettingsChannel.Main, 0);
            SetRowSiblingIndex(AudioSettingsChannel.Bgm, 1);
            SetRowSiblingIndex(AudioSettingsChannel.Sfx, 2);
        }

        private void BindAudioControl(AudioSettingsChannel channel)
        {
            if (!_audioControls.TryGetValue(channel, out var widgets) || widgets.RowRoot == null)
            {
                return;
            }

            if (widgets.Slider != null)
            {
                var handler = GetOrCreateSliderHandler(channel);
                widgets.Slider.onValueChanged.RemoveListener(handler);
                widgets.Slider.onValueChanged.AddListener(handler);
            }

            if (widgets.Toggle != null)
            {
                var handler = GetOrCreateToggleHandler(channel);
                widgets.Toggle.onValueChanged.RemoveListener(handler);
                widgets.Toggle.onValueChanged.AddListener(handler);
            }

            if (widgets.InteractionRelay != null)
            {
                widgets.InteractionRelay.InteractionCompleted -= HandleAudioInteractionCompleted;
                widgets.InteractionRelay.InteractionCompleted += HandleAudioInteractionCompleted;
            }
        }

        private void CacheAudioControls()
        {
            if (_audioControls.Count > 0)
            {
                return;
            }

            _audioControls[AudioSettingsChannel.Main] = _mainRow;
            _audioControls[AudioSettingsChannel.Bgm] = _bgmRow;
            _audioControls[AudioSettingsChannel.Sfx] = _sfxRow;
        }

        private void HandleAudioInteractionCompleted()
        {
            if (!_isVisible)
            {
                return;
            }

            InteractionCompleted?.Invoke();
        }

        private void HandleAudioSliderChanged(AudioSettingsChannel channel, float value)
        {
            if (!_isVisible)
            {
                return;
            }

            VolumeChanged?.Invoke(channel, value);
        }

        private void HandleAudioToggleChanged(AudioSettingsChannel channel, bool isMuted)
        {
            if (!_isVisible)
            {
                return;
            }

            MuteChanged?.Invoke(channel, isMuted);
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void LayoutAudioRow(AudioSettingsChannel channel)
        {
            if (!_audioControls.TryGetValue(channel, out var widgets))
            {
                return;
            }

            SettingsLayoutUtility.MoveToParent(widgets.RowRoot, transform as RectTransform);
            SettingsLayoutUtility.EnsureHorizontalLayout(
                widgets.RowRoot.gameObject,
                new RectOffset(0, 0, 0, 0),
                10f,
                TextAnchor.MiddleLeft);
            SettingsLayoutUtility.EnsureLayoutElement(widgets.RowRoot, preferredHeight: 32f, flexibleWidth: 1f);
            SettingsLayoutUtility.FillLayoutChild(widgets.RowRoot);

            SettingsLayoutUtility.MoveToParent(widgets.Label != null ? widgets.Label.rectTransform : null, widgets.RowRoot);
            SettingsLayoutUtility.MoveToParent(widgets.Slider, widgets.RowRoot);
            SettingsLayoutUtility.MoveToParent(widgets.Value != null ? widgets.Value.rectTransform : null, widgets.RowRoot);
            SettingsLayoutUtility.MoveToParent(widgets.Toggle, widgets.RowRoot);

            SettingsLayoutUtility.EnsureLayoutElement(widgets.Label, preferredWidth: 120f, preferredHeight: 24f);
            SettingsLayoutUtility.EnsureLayoutElement(widgets.Slider, preferredWidth: 180f, preferredHeight: 22f, flexibleWidth: 1f);
            SettingsLayoutUtility.EnsureLayoutElement(widgets.Value, preferredWidth: 58f, preferredHeight: 24f);
            SettingsLayoutUtility.EnsureLayoutElement(widgets.Toggle, preferredWidth: 58f, preferredHeight: 24f);
        }

        private void SetRowSiblingIndex(AudioSettingsChannel channel, int siblingIndex)
        {
            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.RowRoot != null)
            {
                widgets.RowRoot.SetSiblingIndex(siblingIndex);
            }
        }

        private void RefreshAudioControl(AudioSettingsChannel channel, AudioSettingsRowViewModel rowViewModel)
        {
            if (!_audioControls.TryGetValue(channel, out var widgets))
            {
                return;
            }

            if (widgets.Label != null)
            {
                widgets.Label.text = rowViewModel.LabelText;
            }

            if (widgets.Value != null)
            {
                widgets.Value.text = rowViewModel.ValueText;
            }

            if (widgets.Slider != null)
            {
                widgets.Slider.SetValueWithoutNotify(rowViewModel.NormalizedValue);
            }

            if (widgets.Toggle != null)
            {
                widgets.Toggle.SetIsOnWithoutNotify(rowViewModel.IsMuted);
            }
        }

        private void RefreshView()
        {
            CacheAudioControls();
            RebindAudioControls();
            ApplyLayout();

            if (_viewModel == null)
            {
                return;
            }

            RefreshAudioControl(AudioSettingsChannel.Main, _viewModel.MainAudio);
            RefreshAudioControl(AudioSettingsChannel.Bgm, _viewModel.BgmAudio);
            RefreshAudioControl(AudioSettingsChannel.Sfx, _viewModel.SfxAudio);
        }

        private void RebindAudioControls()
        {
            BindAudioControl(AudioSettingsChannel.Main);
            BindAudioControl(AudioSettingsChannel.Bgm);
            BindAudioControl(AudioSettingsChannel.Sfx);
        }

        private void UnbindAudioControls()
        {
            foreach (var pair in _audioControls)
            {
                var widgets = pair.Value;
                if (widgets == null)
                {
                    continue;
                }

                if (widgets.Slider != null)
                {
                    widgets.Slider.onValueChanged.RemoveListener(GetOrCreateSliderHandler(pair.Key));
                }

                if (widgets.Toggle != null)
                {
                    widgets.Toggle.onValueChanged.RemoveListener(GetOrCreateToggleHandler(pair.Key));
                }

                if (widgets.InteractionRelay != null)
                {
                    widgets.InteractionRelay.InteractionCompleted -= HandleAudioInteractionCompleted;
                }
            }
        }

        private UnityAction<float> GetOrCreateSliderHandler(AudioSettingsChannel channel)
        {
            if (_sliderHandlers.TryGetValue(channel, out var handler))
            {
                return handler;
            }

            handler = value => HandleAudioSliderChanged(channel, value);
            _sliderHandlers[channel] = handler;
            return handler;
        }

        private UnityAction<bool> GetOrCreateToggleHandler(AudioSettingsChannel channel)
        {
            if (_toggleHandlers.TryGetValue(channel, out var handler))
            {
                return handler;
            }

            handler = value => HandleAudioToggleChanged(channel, value);
            _toggleHandlers[channel] = handler;
            return handler;
        }

        private void ValidateRow(AudioControlRowRefs row, string rowName, List<string> issues)
        {
            if (row == null)
            {
                issues.Add($"{rowName} row group is not assigned");
                return;
            }

            row.CollectValidationIssues(transform, rowName, issues);
        }

#if UNITY_EDITOR
        private void ValidateRowSerializedReferences(AudioControlRowRefs row, string fieldName)
        {
            if (row == null)
            {
                Debug.LogWarning($"{nameof(SettingsAudioView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
                return;
            }

            ValidateSerializedReference(row.RowRoot, $"{fieldName}._rowRoot");
            ValidateSerializedReference(row.Label, $"{fieldName}._label");
            ValidateSerializedReference(row.Value, $"{fieldName}._value");
            ValidateSerializedReference(row.Slider, $"{fieldName}._slider");
            ValidateSerializedReference(row.Toggle, $"{fieldName}._toggle");
            ValidateSerializedReference(row.InteractionRelay, $"{fieldName}._interactionRelay");
        }

        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(SettingsAudioView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif

        private static string BuildValidationMessage(string baseMessage, IReadOnlyList<string> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return baseMessage;
            }

            return $"{baseMessage} Details: {string.Join("; ", issues)}.";
        }

        [Serializable]
        private sealed class AudioControlRowRefs
        {
            [SerializeField] private RectTransform _rowRoot;
            [SerializeField] private TMP_Text _label;
            [SerializeField] private TMP_Text _value;
            [SerializeField] private Slider _slider;
            [SerializeField] private Toggle _toggle;
            [SerializeField] private SettingsSliderInteractionRelay _interactionRelay;

            public RectTransform RowRoot => _rowRoot;

            public TMP_Text Label => _label;

            public TMP_Text Value => _value;

            public Slider Slider => _slider;

            public Toggle Toggle => _toggle;

            public SettingsSliderInteractionRelay InteractionRelay => _interactionRelay;

            public void CollectValidationIssues(Transform sectionRoot, string rowName, List<string> issues)
            {
                ValidateAssigned(_rowRoot, $"{rowName} row root", issues);
                ValidateAssigned(_label, $"{rowName} label", issues);
                ValidateAssigned(_value, $"{rowName} value label", issues);
                ValidateAssigned(_slider, $"{rowName} slider", issues);
                ValidateAssigned(_toggle, $"{rowName} mute toggle", issues);
                ValidateAssigned(_interactionRelay, $"{rowName} interaction relay", issues);

                ValidateChildOf(_rowRoot, sectionRoot, $"{rowName} row root", issues);
                ValidateChildOf(_label, sectionRoot, $"{rowName} label", issues);
                ValidateChildOf(_value, sectionRoot, $"{rowName} value label", issues);
                ValidateChildOf(_slider, sectionRoot, $"{rowName} slider", issues);
                ValidateChildOf(_toggle, sectionRoot, $"{rowName} mute toggle", issues);
                ValidateChildOf(_interactionRelay, sectionRoot, $"{rowName} interaction relay", issues);
            }

            private static void ValidateAssigned(UnityEngine.Object value, string description, List<string> issues)
            {
                if (value == null)
                {
                    issues.Add($"{description} is not assigned");
                }
            }

            private static void ValidateChildOf(Component component, Transform sectionRoot, string description, List<string> issues)
            {
                if (component == null || sectionRoot == null)
                {
                    return;
                }

                if (!component.transform.IsChildOf(sectionRoot))
                {
                    issues.Add($"{description} must remain under '{sectionRoot.name}'");
                }
            }
        }
    }
}
