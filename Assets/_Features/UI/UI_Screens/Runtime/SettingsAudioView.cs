using System;
using System.Collections.Generic;
using Game.Feature.UI.Composition;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsAudioView : MonoBehaviour
    {
        private const float VolumeComparisonEpsilon = 0.0001f;
        private const string MissingControlsMessage =
            "Settings audio section is missing required authored controls. Repair: open SettingsScreen.prefab and assign every SettingsAudioView serialized row reference.";

        [SerializeField] private AudioControlRowRefs _mainRow = new();
        [SerializeField] private AudioControlRowRefs _bgmRow = new();
        [SerializeField] private AudioControlRowRefs _sfxRow = new();

        private readonly Dictionary<AudioSettingsChannel, AudioControlRowRefs> _audioControls = new();
        private readonly Dictionary<AudioSettingsChannel, UnityAction<float>> _sliderHandlers = new();
        private readonly Dictionary<AudioSettingsChannel, UnityAction<bool>> _toggleHandlers = new();
        private readonly Dictionary<AudioSettingsChannel, Action> _interactionStartedHandlers = new();
        private readonly Dictionary<AudioSettingsChannel, Action> _interactionCompletedHandlers = new();
        private readonly Dictionary<AudioSettingsChannel, float> _interactionStartValues = new();
        private readonly Dictionary<AudioSettingsChannel, float> _lastKnownValues = new();
        private List<LocalizedTmpTextBinding> _localizedStaticBindings;
        private ILocalizedTextResolver _localizedTextResolver;
        private GameplayUiTypographyTheme _typographyTheme;
        private bool _isVisible;
        private SettingsAudioViewModel _viewModel;

        public event Action<AudioSettingsChannel, float> VolumeChanged;

        public event Action<AudioSettingsChannel, bool> MuteChanged;

        public event Action InteractionCompleted;

        public void BindStaticLocalization(
            SettingsScreenPayload payload,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver,
            ILocalizedTmpFontResolver fontResolver,
            GameplayUiTypographyTheme typographyTheme)
        {
            UnbindStaticLocalization();
            if (payload == null)
            {
                return;
            }

            _localizedTextResolver = textResolver;
            _typographyTheme = typographyTheme;
            _localizedStaticBindings = new List<LocalizedTmpTextBinding>
            {
                CreateBinding(_mainRow.Label, payload.AudioMainLabelDescriptor, typographyResolver, fontResolver),
                CreateBinding(_mainRow.MuteLabel, payload.AudioMuteLabelDescriptor, typographyResolver, fontResolver),
                CreateBinding(_bgmRow.Label, payload.AudioBgmLabelDescriptor, typographyResolver, fontResolver),
                CreateBinding(_bgmRow.MuteLabel, payload.AudioMuteLabelDescriptor, typographyResolver, fontResolver),
                CreateBinding(_sfxRow.Label, payload.AudioSfxLabelDescriptor, typographyResolver, fontResolver),
                CreateBinding(_sfxRow.MuteLabel, payload.AudioMuteLabelDescriptor, typographyResolver, fontResolver),
            };
            if (_localizedTextResolver != null)
            {
                _localizedTextResolver.LocaleChanged += HandleLocaleChanged;
            }

            RefreshTypography();
        }

        public void UnbindStaticLocalization()
        {
            if (_localizedTextResolver != null)
            {
                _localizedTextResolver.LocaleChanged -= HandleLocaleChanged;
            }

            DisposeLocalizedStaticBindings();
            _localizedTextResolver = null;
            _typographyTheme = null;
        }

        public void BeginInteraction(AudioSettingsChannel channel)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.Slider != null)
            {
                if (!_interactionStartValues.ContainsKey(channel))
                {
                    _interactionStartValues[channel] = GetLastKnownValue(channel, widgets.Slider.value);
                }
            }
        }

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

            if (HasCommittedVolumeChange(channel))
            {
                InteractionCompleted?.Invoke();
            }

            CaptureCurrentValue(channel);
            _interactionStartValues.Remove(channel);
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

        public bool IsMuted(AudioSettingsChannel channel)
        {
            return _audioControls.TryGetValue(channel, out var widgets) &&
                   widgets.Toggle != null &&
                   widgets.Toggle.isOn;
        }

        public float GetVolume(AudioSettingsChannel channel)
        {
            return _audioControls.TryGetValue(channel, out var widgets) && widgets.Slider != null
                ? widgets.Slider.value
                : 0f;
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
            _interactionStartValues.Clear();
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

            UnbindStaticLocalization();
            UnbindAudioControls();
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
                BindInteractionRelay(channel, widgets.InteractionRelay);
            }

            var sliderRootRelay = GetSliderRootInteractionRelay(widgets, createIfMissing: true);
            if (sliderRootRelay != null && sliderRootRelay != widgets.InteractionRelay)
            {
                BindInteractionRelay(channel, sliderRootRelay);
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

        private void HandleAudioInteractionStarted(AudioSettingsChannel channel)
        {
            BeginInteraction(channel);
        }

        private void HandleAudioInteractionCompleted(AudioSettingsChannel channel)
        {
            CommitInteraction(channel);
        }

        private void HandleAudioSliderChanged(AudioSettingsChannel channel, float value)
        {
            if (!_isVisible)
            {
                return;
            }

            if (!_interactionStartValues.ContainsKey(channel))
            {
                var previousValue = GetLastKnownValue(channel, value);
                if (Mathf.Abs(value - previousValue) > VolumeComparisonEpsilon)
                {
                    _interactionStartValues[channel] = previousValue;
                }
            }

            VolumeChanged?.Invoke(channel, value);
            _lastKnownValues[channel] = value;
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

        private void HandleLocaleChanged()
        {
            RefreshTypography();
        }

        private void RefreshAudioControl(AudioSettingsChannel channel, AudioSettingsRowViewModel rowViewModel)
        {
            if (!_audioControls.TryGetValue(channel, out var widgets))
            {
                return;
            }

            if (widgets.Value != null)
            {
                widgets.Value.text = rowViewModel.ValueText;
            }

            if (widgets.Slider != null)
            {
                widgets.Slider.SetValueWithoutNotify(rowViewModel.NormalizedValue);
                _lastKnownValues[channel] = widgets.Slider.value;
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

            if (_viewModel == null)
            {
                return;
            }

            RefreshAudioControl(AudioSettingsChannel.Main, _viewModel.MainAudio);
            RefreshAudioControl(AudioSettingsChannel.Bgm, _viewModel.BgmAudio);
            RefreshAudioControl(AudioSettingsChannel.Sfx, _viewModel.SfxAudio);
            RefreshTypography();
        }

        private void RefreshTypography()
        {
            if (_typographyTheme == null)
            {
                return;
            }

            var localeCode = _localizedTextResolver != null
                ? _localizedTextResolver.CurrentLocaleCode
                : string.Empty;
            ApplyTypography(_mainRow, localeCode);
            ApplyTypography(_bgmRow, localeCode);
            ApplyTypography(_sfxRow, localeCode);
        }

        private void ApplyTypography(AudioControlRowRefs row, string localeCode)
        {
            if (row == null)
            {
                return;
            }

            LocalizedTmpTextApplicator.ApplyTypographyTheme(row.Label, _typographyTheme, localeCode);
            LocalizedTmpTextApplicator.ApplyTypographyTheme(row.Value, _typographyTheme, localeCode);
            LocalizedTmpTextApplicator.ApplyTypographyTheme(row.MuteLabel, _typographyTheme, localeCode);
        }

        private LocalizedTmpTextBinding CreateBinding(
            TMP_Text target,
            LocalizedTextDescriptor descriptor,
            ILocalizedTypographyResolver typographyResolver,
            ILocalizedTmpFontResolver fontResolver)
        {
            return new LocalizedTmpTextBinding(
                target,
                descriptor,
                _localizedTextResolver,
                typographyResolver,
                fontResolver,
                _typographyTheme);
        }

        private void DisposeLocalizedStaticBindings()
        {
            if (_localizedStaticBindings == null)
            {
                return;
            }

            foreach (var binding in _localizedStaticBindings)
            {
                binding?.Dispose();
            }

            _localizedStaticBindings = null;
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
                    UnbindInteractionRelay(pair.Key, widgets.InteractionRelay);
                }

                var sliderRootRelay = GetSliderRootInteractionRelay(widgets, createIfMissing: false);
                if (sliderRootRelay != null && sliderRootRelay != widgets.InteractionRelay)
                {
                    UnbindInteractionRelay(pair.Key, sliderRootRelay);
                }
            }
        }

        private void BindInteractionRelay(AudioSettingsChannel channel, SettingsSliderInteractionRelay relay)
        {
            if (relay == null)
            {
                return;
            }

            var startedHandler = GetOrCreateInteractionStartedHandler(channel);
            relay.InteractionStarted -= startedHandler;
            relay.InteractionStarted += startedHandler;

            var completedHandler = GetOrCreateInteractionCompletedHandler(channel);
            relay.InteractionCompleted -= completedHandler;
            relay.InteractionCompleted += completedHandler;
        }

        private void UnbindInteractionRelay(AudioSettingsChannel channel, SettingsSliderInteractionRelay relay)
        {
            if (relay == null)
            {
                return;
            }

            relay.InteractionStarted -= GetOrCreateInteractionStartedHandler(channel);
            relay.InteractionCompleted -= GetOrCreateInteractionCompletedHandler(channel);
        }

        private SettingsSliderInteractionRelay GetSliderRootInteractionRelay(
            AudioControlRowRefs widgets,
            bool createIfMissing)
        {
            if (widgets == null || widgets.Slider == null)
            {
                return null;
            }

            var relay = widgets.Slider.GetComponent<SettingsSliderInteractionRelay>();
            if (relay == null && createIfMissing)
            {
                relay = widgets.Slider.gameObject.AddComponent<SettingsSliderInteractionRelay>();
            }

            return relay;
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

        private Action GetOrCreateInteractionStartedHandler(AudioSettingsChannel channel)
        {
            if (_interactionStartedHandlers.TryGetValue(channel, out var handler))
            {
                return handler;
            }

            handler = () => HandleAudioInteractionStarted(channel);
            _interactionStartedHandlers[channel] = handler;
            return handler;
        }

        private Action GetOrCreateInteractionCompletedHandler(AudioSettingsChannel channel)
        {
            if (_interactionCompletedHandlers.TryGetValue(channel, out var handler))
            {
                return handler;
            }

            handler = () => HandleAudioInteractionCompleted(channel);
            _interactionCompletedHandlers[channel] = handler;
            return handler;
        }

        private bool HasCommittedVolumeChange(AudioSettingsChannel channel)
        {
            if (!_interactionStartValues.TryGetValue(channel, out var startValue) ||
                !_audioControls.TryGetValue(channel, out var widgets) ||
                widgets.Slider == null)
            {
                return false;
            }

            return Mathf.Abs(widgets.Slider.value - startValue) > VolumeComparisonEpsilon;
        }

        private float GetLastKnownValue(AudioSettingsChannel channel, float fallback)
        {
            return _lastKnownValues.TryGetValue(channel, out var value)
                ? value
                : fallback;
        }

        private void CaptureCurrentValue(AudioSettingsChannel channel)
        {
            if (_audioControls.TryGetValue(channel, out var widgets) && widgets.Slider != null)
            {
                _lastKnownValues[channel] = widgets.Slider.value;
            }
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
            ValidateSerializedReference(row.MuteLabel, $"{fieldName}._muteLabel");
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
            [SerializeField] private TMP_Text _muteLabel;
            [SerializeField] private Slider _slider;
            [SerializeField] private Toggle _toggle;
            [SerializeField] private SettingsSliderInteractionRelay _interactionRelay;

            public RectTransform RowRoot => _rowRoot;

            public TMP_Text Label => _label;

            public TMP_Text Value => _value;

            public TMP_Text MuteLabel => _muteLabel;

            public Slider Slider => _slider;

            public Toggle Toggle => _toggle;

            public SettingsSliderInteractionRelay InteractionRelay => _interactionRelay;

            public void CollectValidationIssues(Transform sectionRoot, string rowName, List<string> issues)
            {
                ValidateAssigned(_rowRoot, $"{rowName} row root", issues);
                ValidateAssigned(_label, $"{rowName} label", issues);
                ValidateAssigned(_value, $"{rowName} value label", issues);
                ValidateAssigned(_muteLabel, $"{rowName} mute label", issues);
                ValidateAssigned(_slider, $"{rowName} slider", issues);
                ValidateAssigned(_toggle, $"{rowName} mute toggle", issues);
                ValidateAssigned(_interactionRelay, $"{rowName} interaction relay", issues);

                ValidateChildOf(_rowRoot, sectionRoot, $"{rowName} row root", issues);
                ValidateChildOf(_label, sectionRoot, $"{rowName} label", issues);
                ValidateChildOf(_value, sectionRoot, $"{rowName} value label", issues);
                ValidateChildOf(_muteLabel, sectionRoot, $"{rowName} mute label", issues);
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
