using System;
using System.Collections.Generic;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public sealed class SettingsAudioPresenter
    {
        private readonly IAudioSettingsPort _audioSettingsPort;
        private readonly ILocalizedTextResolver _localizedTextResolver;

        public SettingsAudioPresenter(IAudioSettingsPort audioSettingsPort)
            : this(audioSettingsPort, InvariantSettingsLocalizedTextResolver.Instance)
        {
        }

        public SettingsAudioPresenter(
            IAudioSettingsPort audioSettingsPort,
            ILocalizedTextResolver localizedTextResolver)
        {
            _audioSettingsPort = audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort));
            _localizedTextResolver = localizedTextResolver ?? throw new ArgumentNullException(nameof(localizedTextResolver));
        }

        public SettingsAudioViewModel ViewModel { get; } = new SettingsAudioViewModel();

        public void Apply()
        {
            RefreshViewModel();
        }

        public void Flush()
        {
            _audioSettingsPort.Flush();
            RefreshViewModel();
        }

        public void SetMuted(AudioSettingsChannel channel, bool isMuted)
        {
            _audioSettingsPort.SetMuted(channel, isMuted);
            RefreshViewModel();
        }

        public void SetVolume(AudioSettingsChannel channel, float volume)
        {
            _audioSettingsPort.SetVolume(channel, volume);
            RefreshViewModel();
        }

        public void RefreshLocalization()
        {
            RefreshViewModel();
        }

        private void RefreshViewModel()
        {
            var snapshot = _audioSettingsPort.Read();
            ViewModel.SetContent(
                BuildAudioRow(snapshot.Main, _localizedTextResolver),
                BuildAudioRow(snapshot.Bgm, _localizedTextResolver),
                BuildAudioRow(snapshot.Sfx, _localizedTextResolver));
        }

        private static AudioSettingsRowViewModel BuildAudioRow(
            AudioSettingsPortChannelState state,
            ILocalizedTextResolver localizedTextResolver)
        {
            var normalizedVolume = Clamp01(state.Volume);
            var percent = (int)Math.Round(normalizedVolume * 100f, MidpointRounding.AwayFromZero);
            var valueText = localizedTextResolver.Resolve(
                SettingsDynamicTextDescriptors.AudioVolumeValue(percent, state.IsMuted));
            return new AudioSettingsRowViewModel(valueText, normalizedVolume, state.IsMuted);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }

    public readonly struct DisplayPreviewCountdownSnapshot
    {
        public static DisplayPreviewCountdownSnapshot Inactive => default;

        public DisplayPreviewCountdownSnapshot(bool isActive, int remainingSeconds, int totalSeconds)
        {
            IsActive = isActive;
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds;
        }

        public bool IsActive { get; }

        public int RemainingSeconds { get; }

        public int TotalSeconds { get; }

        public static DisplayPreviewCountdownSnapshot Create(double remainingSeconds, double totalSeconds)
        {
            var totalWholeSeconds = ComputeVisibleSeconds(totalSeconds, totalSeconds);
            if (totalWholeSeconds <= 0)
            {
                return Inactive;
            }

            var visibleRemainingSeconds = ComputeVisibleSeconds(remainingSeconds, totalWholeSeconds);
            if (visibleRemainingSeconds <= 0)
            {
                return Inactive;
            }

            return new DisplayPreviewCountdownSnapshot(
                isActive: true,
                remainingSeconds: visibleRemainingSeconds,
                totalSeconds: totalWholeSeconds);
        }

        public static int ComputeVisibleSeconds(double remainingSeconds, double totalSeconds)
        {
            var totalWholeSeconds = Math.Max(0, (int)Math.Ceiling(Math.Max(0d, totalSeconds)));
            if (totalWholeSeconds <= 0 || remainingSeconds <= 0d)
            {
                return 0;
            }

            if (remainingSeconds <= 1d)
            {
                return 1;
            }

            return Math.Min(totalWholeSeconds, (int)Math.Floor(remainingSeconds) + 1);
        }
    }

    public sealed class SettingsDisplayPresenter
    {
        private const string PreviewRevertedStatusText =
            "Preview reverted to the previous saved display settings.";
        private const string PreviewCommittedStatusText =
            "Display settings saved.";
        private const string ExternalDriftStatusText =
            "Current display changed outside saved settings. Saved settings remain unchanged until you apply again.";

        private readonly IDisplaySettingsPort _displaySettingsPort;
        private readonly ILocalizedTextResolver _localizedTextResolver;
        private readonly IUiLocaleSelectionPort _localeSelectionPort;
        private DisplaySettingsPortSnapshot _displaySnapshot = new(
            Array.Empty<DisplaySettingsPortModeOption>(),
            0,
            DisplayWindowMode.Windowed,
            string.Empty,
            DisplayWindowMode.Windowed,
            false);
        private LocalizedTextDescriptor _languageLabelDescriptor = SettingsStaticTextDescriptors.Language;
        private LocalizedTextDescriptor _englishLanguageLabelDescriptor = SettingsStaticTextDescriptors.LanguageEnglish;
        private LocalizedTextDescriptor _koreanLanguageLabelDescriptor = SettingsStaticTextDescriptors.LanguageKorean;
        private int _stagedDisplayModeIndex;
        private DisplayWindowMode _stagedDisplayWindowMode;
        private string _displayStatusText = string.Empty;
        private bool _isDisplayStatusTransient;
        private DisplayPreviewCountdownSnapshot _previewCountdown = DisplayPreviewCountdownSnapshot.Inactive;

        public SettingsDisplayPresenter(IDisplaySettingsPort displaySettingsPort)
            : this(
                displaySettingsPort,
                InvariantSettingsLocalizedTextResolver.Instance,
                NoOpUiLocaleSelectionPort.Instance)
        {
        }

        public SettingsDisplayPresenter(
            IDisplaySettingsPort displaySettingsPort,
            ILocalizedTextResolver localizedTextResolver,
            IUiLocaleSelectionPort localeSelectionPort)
        {
            _displaySettingsPort = displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort));
            _localizedTextResolver = localizedTextResolver ?? throw new ArgumentNullException(nameof(localizedTextResolver));
            _localeSelectionPort = localeSelectionPort ?? NoOpUiLocaleSelectionPort.Instance;
        }

        public SettingsDisplayViewModel ViewModel { get; } = new SettingsDisplayViewModel();

        public void Apply(double previewTimeoutSeconds)
        {
            Apply(
                SettingsStaticTextDescriptors.Language,
                SettingsStaticTextDescriptors.LanguageEnglish,
                SettingsStaticTextDescriptors.LanguageKorean,
                previewTimeoutSeconds);
        }

        public void Apply(
            LocalizedTextDescriptor languageLabelDescriptor,
            LocalizedTextDescriptor englishLanguageLabelDescriptor,
            LocalizedTextDescriptor koreanLanguageLabelDescriptor,
            double previewTimeoutSeconds)
        {
            _languageLabelDescriptor = languageLabelDescriptor;
            _englishLanguageLabelDescriptor = englishLanguageLabelDescriptor;
            _koreanLanguageLabelDescriptor = koreanLanguageLabelDescriptor;
            ClearPreviewCountdown();
            ResyncState(resetStagedToCommitted: true, previewTimeoutSeconds: previewTimeoutSeconds);
        }

        public bool SelectNextLocale()
        {
            if (_localeSelectionPort.AvailableLocaleCodes.Count < 2)
            {
                return false;
            }

            var currentIndex = FindCurrentLocaleIndex();
            var nextIndex = currentIndex < 0
                ? 0
                : (currentIndex + 1) % _localeSelectionPort.AvailableLocaleCodes.Count;
            if (!_localeSelectionPort.TrySetLocale(_localeSelectionPort.AvailableLocaleCodes[nextIndex]))
            {
                return false;
            }

            RefreshViewModel();
            return true;
        }

        public void RefreshLocalization()
        {
            RefreshViewModel();
        }

        public bool ApplyStagedSettings(double previewTimeoutSeconds)
        {
            if (_displaySnapshot.IsPreviewActive || !IsDirty())
            {
                return false;
            }

            var started = _displaySettingsPort.BeginPreview(
                new DisplaySettingsPortPreviewRequest(_stagedDisplayModeIndex, _stagedDisplayWindowMode));
            if (!started)
            {
                ClearPreviewCountdown();
            }

            ResyncState(
                resetStagedToCommitted: false,
                previewTimeoutSeconds: previewTimeoutSeconds,
                overrideStatusText: started ? BuildPreviewActiveStatusText(previewTimeoutSeconds) : null,
                overrideStatusTransient: false);
            return started;
        }

        public bool CancelPreview()
        {
            var reverted = _displaySettingsPort.RevertPreview();
            ClearPreviewCountdown();
            ResyncState(
                resetStagedToCommitted: true,
                previewTimeoutSeconds: 0d,
                overrideStatusText: reverted ? PreviewRevertedStatusText : null,
                overrideStatusTransient: reverted);
            return reverted;
        }

        public bool ConfirmPreview()
        {
            var committed = _displaySettingsPort.CommitPreview();
            ClearPreviewCountdown();
            ResyncState(
                resetStagedToCommitted: true,
                previewTimeoutSeconds: 0d,
                overrideStatusText: committed ? PreviewCommittedStatusText : null,
                overrideStatusTransient: committed);
            return committed;
        }

        public bool ClearTransientDisplayStatus(double previewTimeoutSeconds)
        {
            if (!_isDisplayStatusTransient)
            {
                return false;
            }

            ResyncState(resetStagedToCommitted: false, previewTimeoutSeconds: previewTimeoutSeconds);
            return true;
        }

        public void ClearPreviewCountdown()
        {
            _previewCountdown = DisplayPreviewCountdownSnapshot.Inactive;
            RefreshViewModel();
        }

        public void ResetStagedToCurrent()
        {
            if (_displaySnapshot.IsPreviewActive)
            {
                return;
            }

            _stagedDisplayModeIndex = ClampDisplayModeIndex(_displaySnapshot.CommittedModeIndex, _displaySnapshot.AvailableModes.Count);
            _stagedDisplayWindowMode = _displaySnapshot.CommittedWindowMode;
            ClearTransientDisplayStatusCore();
            RefreshViewModel();
        }

        public void ResyncState(double previewTimeoutSeconds)
        {
            ResyncState(resetStagedToCommitted: false, previewTimeoutSeconds: previewTimeoutSeconds);
        }

        public void SetPreviewCountdown(DisplayPreviewCountdownSnapshot snapshot)
        {
            _previewCountdown = snapshot;
            RefreshViewModel();
        }

        public void StageResolution(int modeIndex)
        {
            if (_displaySnapshot.IsPreviewActive || _displaySnapshot.AvailableModes.Count == 0)
            {
                return;
            }

            _stagedDisplayModeIndex = ClampDisplayModeIndex(modeIndex, _displaySnapshot.AvailableModes.Count);
            ClearTransientDisplayStatusCore();
            RefreshViewModel();
        }

        public void StageWindowMode(DisplayWindowMode mode)
        {
            if (_displaySnapshot.IsPreviewActive)
            {
                return;
            }

            _stagedDisplayWindowMode = mode;
            ClearTransientDisplayStatusCore();
            RefreshViewModel();
        }

        private void ResyncState(
            bool resetStagedToCommitted,
            double previewTimeoutSeconds,
            string overrideStatusText = null,
            bool overrideStatusTransient = false)
        {
            _displaySnapshot = _displaySettingsPort.Read();
            if (_displaySnapshot.AvailableModes.Count == 0)
            {
                _displaySnapshot = new DisplaySettingsPortSnapshot(
                    Array.Empty<DisplaySettingsPortModeOption>(),
                    0,
                    _displaySnapshot.CommittedWindowMode,
                    _displaySnapshot.CurrentRuntimeResolutionLabel,
                    _displaySnapshot.CurrentRuntimeWindowMode,
                    _displaySnapshot.IsPreviewActive);
            }

            if (resetStagedToCommitted || _displaySnapshot.AvailableModes.Count == 0)
            {
                _stagedDisplayModeIndex = ClampDisplayModeIndex(_displaySnapshot.CommittedModeIndex, _displaySnapshot.AvailableModes.Count);
                _stagedDisplayWindowMode = _displaySnapshot.CommittedWindowMode;
            }
            else
            {
                _stagedDisplayModeIndex = ClampDisplayModeIndex(_stagedDisplayModeIndex, _displaySnapshot.AvailableModes.Count);
            }

            _displayStatusText = overrideStatusText ?? BuildDisplayStatusText(previewTimeoutSeconds);
            _isDisplayStatusTransient = overrideStatusText != null && overrideStatusTransient;
            RefreshViewModel();
        }

        private void ClearTransientDisplayStatusCore()
        {
            if (!_isDisplayStatusTransient)
            {
                return;
            }

            _displayStatusText = string.Empty;
            _isDisplayStatusTransient = false;
        }

        private static string BuildPreviewActiveStatusText(double previewTimeoutSeconds)
        {
            var visibleTimeoutSeconds = DisplayPreviewCountdownSnapshot.ComputeVisibleSeconds(
                previewTimeoutSeconds,
                previewTimeoutSeconds);
            return $"Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in {visibleTimeoutSeconds} seconds.";
        }

        private string BuildDisplayStatusText(double previewTimeoutSeconds)
        {
            if (_displaySnapshot.IsPreviewActive)
            {
                return BuildPreviewActiveStatusText(previewTimeoutSeconds);
            }

            if (_displaySnapshot.CurrentRuntimeWindowMode != _displaySnapshot.CommittedWindowMode)
            {
                return ExternalDriftStatusText;
            }

            if (_displaySnapshot.AvailableModes.Count == 0)
            {
                return string.Empty;
            }

            var committedIndex = ClampDisplayModeIndex(_displaySnapshot.CommittedModeIndex, _displaySnapshot.AvailableModes.Count);
            if (_displaySnapshot.CurrentRuntimeResolutionLabel != _displaySnapshot.AvailableModes[committedIndex].LabelText)
            {
                return ExternalDriftStatusText;
            }

            return string.Empty;
        }

        private void RefreshViewModel()
        {
            var resolutionOptions = new List<string>(_displaySnapshot.AvailableModes.Count);
            for (var i = 0; i < _displaySnapshot.AvailableModes.Count; i++)
            {
                resolutionOptions.Add(_displaySnapshot.AvailableModes[i].LabelText);
            }

            var isPreviewCountdownVisible = _displaySnapshot.IsPreviewActive &&
                                            _previewCountdown.IsActive &&
                                            _previewCountdown.TotalSeconds > 0 &&
                                            _previewCountdown.RemainingSeconds > 0;
            var previewCountdownText = isPreviewCountdownVisible
                ? Resolve(SettingsDynamicTextDescriptors.DisplayPreviewCountdown(_previewCountdown.RemainingSeconds))
                : string.Empty;
            var previewCountdownNormalized = isPreviewCountdownVisible
                ? Clamp01((float)_previewCountdown.RemainingSeconds / _previewCountdown.TotalSeconds)
                : 0f;

            ViewModel.SetContent(
                Resolve(SettingsDynamicTextDescriptors.DisplayResolutionValue(
                    _displaySnapshot.CurrentRuntimeResolutionLabel)),
                resolutionOptions,
                _stagedDisplayModeIndex,
                _stagedDisplayWindowMode == DisplayWindowMode.FullScreenWindow,
                _displayStatusText,
                IsDirty() && !_displaySnapshot.IsPreviewActive,
                IsDirty() && !_displaySnapshot.IsPreviewActive,
                _displaySnapshot.IsPreviewActive,
                previewCountdownText,
                previewCountdownNormalized,
                isPreviewCountdownVisible,
                _displayStatusText.Length > 0,
                _isDisplayStatusTransient,
                Resolve(_languageLabelDescriptor),
                Resolve(CurrentLanguageDescriptor),
                _localeSelectionPort.AvailableLocaleCodes.Count > 1);
        }

        private bool IsDirty()
        {
            return _stagedDisplayModeIndex != ClampDisplayModeIndex(_displaySnapshot.CommittedModeIndex, _displaySnapshot.AvailableModes.Count) ||
                   _stagedDisplayWindowMode != _displaySnapshot.CommittedWindowMode;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }

        private static int ClampDisplayModeIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= count)
            {
                return count - 1;
            }

            return index;
        }

        private LocalizedTextDescriptor CurrentLanguageDescriptor =>
            string.Equals(_localeSelectionPort.CurrentLocaleCode, "ko-KR", StringComparison.Ordinal)
                ? _koreanLanguageLabelDescriptor
                : _englishLanguageLabelDescriptor;

        private int FindCurrentLocaleIndex()
        {
            for (var i = 0; i < _localeSelectionPort.AvailableLocaleCodes.Count; i++)
            {
                if (string.Equals(_localeSelectionPort.AvailableLocaleCodes[i], _localeSelectionPort.CurrentLocaleCode, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private string Resolve(LocalizedTextDescriptor descriptor)
        {
            return _localizedTextResolver.Resolve(descriptor);
        }
    }

    public readonly struct SettingsInputPresenterInput
    {
        public SettingsInputPresenterInput(
            LocalizedTextDescriptor movementLabelDescriptor,
            LocalizedTextDescriptor useArrowKeysLabelDescriptor,
            LocalizedTextDescriptor pushLabelDescriptor,
            LocalizedTextDescriptor flipLabelDescriptor,
            LocalizedTextDescriptor changeLabelDescriptor,
            LocalizedTextDescriptor resetLabelDescriptor)
        {
            MovementLabelDescriptor = movementLabelDescriptor;
            UseArrowKeysLabelDescriptor = useArrowKeysLabelDescriptor;
            PushLabelDescriptor = pushLabelDescriptor;
            FlipLabelDescriptor = flipLabelDescriptor;
            ChangeLabelDescriptor = changeLabelDescriptor;
            ResetLabelDescriptor = resetLabelDescriptor;
        }

        public LocalizedTextDescriptor MovementLabelDescriptor { get; }

        public LocalizedTextDescriptor UseArrowKeysLabelDescriptor { get; }

        public LocalizedTextDescriptor PushLabelDescriptor { get; }

        public LocalizedTextDescriptor FlipLabelDescriptor { get; }

        public LocalizedTextDescriptor ChangeLabelDescriptor { get; }

        public LocalizedTextDescriptor ResetLabelDescriptor { get; }
    }

    public sealed class SettingsInputPresenter
    {
        private readonly IKeyboardBindingSettingsPort _keyboardBindingSettingsPort;
        private readonly ILocalizedTextResolver _localizedTextResolver;
        private SettingsInputPresenterInput _input = new SettingsInputPresenterInput(
            SettingsStaticTextDescriptors.MovementKeys,
            SettingsStaticTextDescriptors.UseArrowKeys,
            SettingsStaticTextDescriptors.Push,
            SettingsStaticTextDescriptors.Flip,
            SettingsStaticTextDescriptors.Change,
            SettingsStaticTextDescriptors.ResetInput);
        private string _statusText = string.Empty;
        private LocalizedTextDescriptor _statusTextDescriptor;

        public SettingsInputPresenter(IKeyboardBindingSettingsPort keyboardBindingSettingsPort)
            : this(keyboardBindingSettingsPort, InvariantSettingsLocalizedTextResolver.Instance)
        {
        }

        public SettingsInputPresenter(
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort,
            ILocalizedTextResolver localizedTextResolver)
        {
            _keyboardBindingSettingsPort = keyboardBindingSettingsPort ?? throw new ArgumentNullException(nameof(keyboardBindingSettingsPort));
            _localizedTextResolver = localizedTextResolver ?? throw new ArgumentNullException(nameof(localizedTextResolver));
        }

        public SettingsInputViewModel ViewModel { get; } = new SettingsInputViewModel();

        public bool IsRebinding => _keyboardBindingSettingsPort.IsRebinding;

        public event Action<KeyboardRebindResult> RebindCompleted;

        public void Apply(SettingsInputPresenterInput input)
        {
            _input = input;
            RefreshViewModel(_keyboardBindingSettingsPort.Read());
        }

        public void SetMovementScheme(KeyboardMovementScheme scheme)
        {
            var result = _keyboardBindingSettingsPort.TrySetMovementScheme(scheme);
            SetStatus(result, KeyboardBindableAction.Push);
            RefreshViewModel(_keyboardBindingSettingsPort.Read());
        }

        public void StartRebind(KeyboardBindableAction action)
        {
            var startResult = _keyboardBindingSettingsPort.StartRebind(action, HandleRebindCompleted);
            if (startResult.Started)
            {
                SetRawStatus(action == KeyboardBindableAction.Push
                    ? "Press a key for Push..."
                    : "Press a key for Flip...");
                RefreshViewModel(startResult.Snapshot);
                return;
            }

            SetStatus(startResult.ValidationResult, action);
            RefreshViewModel(startResult.Snapshot);
        }

        public void CancelRebind()
        {
            _keyboardBindingSettingsPort.CancelRebind();
            RefreshViewModel(_keyboardBindingSettingsPort.Read());
        }

        public void ResetToDefaults()
        {
            var snapshot = _keyboardBindingSettingsPort.ResetToDefaults();
            SetStatusDescriptor(SettingsDynamicTextDescriptors.InputResetComplete());
            RefreshViewModel(snapshot);
        }

        private void HandleRebindCompleted(KeyboardRebindResult result)
        {
            SetStatus(result.ValidationResult, result.Action);
            RefreshViewModel(result.Snapshot);
            RebindCompleted?.Invoke(result);
        }

        private void RefreshViewModel(KeyboardBindingSettingsSnapshot snapshot)
        {
            var areControlsInteractable = !snapshot.IsRebinding;
            ViewModel.SetContent(
                Resolve(_input.MovementLabelDescriptor),
                Resolve(_input.UseArrowKeysLabelDescriptor),
                snapshot.MovementScheme == KeyboardMovementScheme.ArrowKeys,
                snapshot.MovementDisplayName,
                Resolve(_input.PushLabelDescriptor),
                snapshot.PushDisplayName,
                Resolve(_input.ChangeLabelDescriptor),
                Resolve(_input.FlipLabelDescriptor),
                snapshot.FlipDisplayName,
                Resolve(_input.ChangeLabelDescriptor),
                Resolve(_input.ResetLabelDescriptor),
                ResolveStatusText(),
                snapshot.IsRebinding,
                snapshot.RebindingAction,
                areControlsInteractable);
        }

        private string Resolve(LocalizedTextDescriptor descriptor)
        {
            return _localizedTextResolver.Resolve(descriptor);
        }

        private void SetStatus(KeyboardBindingValidationResult result, KeyboardBindableAction action)
        {
            if (result == KeyboardBindingValidationResult.Canceled)
            {
                SetStatusDescriptor(SettingsDynamicTextDescriptors.InputRebindCanceled());
                return;
            }

            if (result == KeyboardBindingValidationResult.ReservedKey)
            {
                SetStatusDescriptor(SettingsDynamicTextDescriptors.InputReservedKey());
                return;
            }

            if (result == KeyboardBindingValidationResult.MovementConflict)
            {
                SetStatusDescriptor(SettingsDynamicTextDescriptors.InputMovementConflict());
                return;
            }

            if (result == KeyboardBindingValidationResult.AlreadyRebinding)
            {
                SetStatusDescriptor(SettingsDynamicTextDescriptors.InputAlreadyRebinding());
                return;
            }

            SetRawStatus(ToStatusText(result, action));
        }

        private void SetStatusDescriptor(LocalizedTextDescriptor descriptor)
        {
            _statusTextDescriptor = descriptor;
            _statusText = string.Empty;
        }

        private void SetRawStatus(string statusText)
        {
            _statusTextDescriptor = default;
            _statusText = statusText ?? string.Empty;
        }

        private string ResolveStatusText()
        {
            return string.IsNullOrEmpty(_statusTextDescriptor.Table) &&
                   string.IsNullOrEmpty(_statusTextDescriptor.Key)
                ? _statusText
                : Resolve(_statusTextDescriptor);
        }

        private static string ToStatusText(KeyboardBindingValidationResult result, KeyboardBindableAction action)
        {
            switch (result)
            {
                case KeyboardBindingValidationResult.Success:
                    return string.Empty;
                case KeyboardBindingValidationResult.Canceled:
                    return string.Empty;
                case KeyboardBindingValidationResult.DuplicateAction:
                    return action == KeyboardBindableAction.Push
                        ? "This key is already used by Flip."
                        : "This key is already used by Push.";
                case KeyboardBindingValidationResult.MovementConflict:
                    return string.Empty;
                default:
                    return "This key cannot be used.";
            }
        }
    }

    public sealed class SettingsScreenPresenter
    {
        private SettingsScreenPayload _payload = SettingsScreenPayload.Default;
        private SettingsSectionId _selectedSection = SettingsSectionId.Audio;

        public SettingsScreenPresenter(
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort)
            : this(
                audioSettingsPort,
                displaySettingsPort,
                NoOpKeyboardBindingSettingsPort.Instance,
                InvariantSettingsLocalizedTextResolver.Instance)
        {
        }

        public SettingsScreenPresenter(
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort)
            : this(
                audioSettingsPort,
                displaySettingsPort,
                keyboardBindingSettingsPort,
                InvariantSettingsLocalizedTextResolver.Instance)
        {
        }

        public SettingsScreenPresenter(
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort,
            ILocalizedTextResolver localizedTextResolver)
            : this(
                audioSettingsPort,
                displaySettingsPort,
                keyboardBindingSettingsPort,
                localizedTextResolver,
                localizedTextResolver as IUiLocaleSelectionPort)
        {
        }

        public SettingsScreenPresenter(
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort,
            ILocalizedTextResolver localizedTextResolver,
            IUiLocaleSelectionPort localeSelectionPort)
        {
            LocalizedTextResolver = localizedTextResolver ?? throw new ArgumentNullException(nameof(localizedTextResolver));
            AudioPresenter = new SettingsAudioPresenter(
                audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort)),
                LocalizedTextResolver);
            DisplayPresenter = new SettingsDisplayPresenter(
                displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort)),
                LocalizedTextResolver,
                localeSelectionPort);
            InputPresenter = new SettingsInputPresenter(
                keyboardBindingSettingsPort ?? throw new ArgumentNullException(nameof(keyboardBindingSettingsPort)),
                LocalizedTextResolver);
        }

        public SettingsAudioPresenter AudioPresenter { get; }

        public SettingsDisplayPresenter DisplayPresenter { get; }

        public SettingsInputPresenter InputPresenter { get; }

        public SettingsScreenViewModel ViewModel { get; } = new SettingsScreenViewModel();

        private ILocalizedTextResolver LocalizedTextResolver { get; }

        public void Apply(SettingsScreenPayload payload, double previewTimeoutSeconds)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            AudioPresenter.Apply();
            DisplayPresenter.Apply(
                _payload.LanguageLabelDescriptor,
                _payload.EnglishLanguageLabelDescriptor,
                _payload.KoreanLanguageLabelDescriptor,
                previewTimeoutSeconds);
            InputPresenter.Apply(new SettingsInputPresenterInput(
                _payload.MovementLabelDescriptor,
                _payload.UseArrowKeysLabelDescriptor,
                _payload.PushLabelDescriptor,
                _payload.FlipLabelDescriptor,
                _payload.InputChangeLabelDescriptor,
                _payload.ResetInputLabelDescriptor));
            RefreshViewModel();
        }

        public bool SelectNextLocale()
        {
            var changed = DisplayPresenter.SelectNextLocale();
            if (changed)
            {
                RefreshLocalization();
            }

            return changed;
        }

        public void RefreshLocalization()
        {
            AudioPresenter.RefreshLocalization();
            DisplayPresenter.RefreshLocalization();
            InputPresenter.Apply(new SettingsInputPresenterInput(
                _payload.MovementLabelDescriptor,
                _payload.UseArrowKeysLabelDescriptor,
                _payload.PushLabelDescriptor,
                _payload.FlipLabelDescriptor,
                _payload.InputChangeLabelDescriptor,
                _payload.ResetInputLabelDescriptor));
            RefreshViewModel();
        }

        public bool SelectSection(SettingsSectionId sectionId)
        {
            if (_selectedSection == sectionId)
            {
                return false;
            }

            _selectedSection = sectionId;
            RefreshViewModel();
            return true;
        }

        private void RefreshViewModel()
        {
            ViewModel.SetContent(
                Resolve(_payload.TitleTextDescriptor),
                Resolve(_payload.BackLabelDescriptor),
                Resolve(_payload.AudioTabLabelDescriptor),
                Resolve(_payload.DisplayTabLabelDescriptor),
                Resolve(_payload.InputTabLabelDescriptor),
                _selectedSection);
        }

        private string Resolve(LocalizedTextDescriptor descriptor)
        {
            return LocalizedTextResolver.Resolve(descriptor);
        }
    }

    internal sealed class NoOpUiLocaleSelectionPort : IUiLocaleSelectionPort
    {
        public static readonly NoOpUiLocaleSelectionPort Instance = new();

        private NoOpUiLocaleSelectionPort()
        {
        }

        public string CurrentLocaleCode => "en-US";

        public IReadOnlyList<string> AvailableLocaleCodes => Array.Empty<string>();

        public bool TrySetLocale(string localeCode)
        {
            return false;
        }
    }

    internal sealed class InvariantSettingsLocalizedTextResolver : ILocalizedTextResolver
    {
        public static readonly InvariantSettingsLocalizedTextResolver Instance = new();

        private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
        {
            ["ui.settings.title"] = "Settings",
            ["ui.settings.audio"] = "Audio",
            ["ui.settings.display"] = "Display",
            ["ui.settings.input"] = "Input",
            ["ui.settings.input.movement_keys"] = "Movement Keys",
            ["ui.settings.input.use_arrow_keys"] = "Use Arrow Keys",
            ["ui.settings.input.push"] = "Push",
            ["ui.settings.input.flip"] = "Flip",
            ["ui.settings.input.change"] = "Change",
            ["ui.settings.input.reset_input"] = "Reset Input",
            ["ui.settings.language"] = "Language",
            ["ui.settings.language.english"] = "English",
            ["ui.settings.language.korean"] = "Korean",
            ["ui.settings.audio.volume_value"] = "{percent}%",
            ["ui.settings.audio.volume_value_muted"] = "{percent}% (Muted)",
            ["ui.settings.display.resolution_value"] = "{0}",
            ["ui.settings.input.rebind_canceled"] = "Rebind canceled.",
            ["ui.settings.input.reset_complete"] = "Input settings reset.",
            ["ui.common.back"] = "Back",
            ["ui.common.settings"] = "Settings",
            ["ui.main_menu.start"] = "Start",
            ["ui.main_menu.quit"] = "Quit",
            ["ui.pause.title"] = "Paused",
            ["ui.pause.description"] = "Pausing modal popup",
            ["ui.pause.resume"] = "Resume",
            ["ui.pause.retry"] = "Retry",
            ["ui.pause.main_menu"] = "Main Menu",
        };

        private InvariantSettingsLocalizedTextResolver()
        {
        }

        public string CurrentLocaleCode => "en-US";

        public event Action LocaleChanged
        {
            add { }
            remove { }
        }

        public string Resolve(LocalizedTextDescriptor descriptor)
        {
            if (string.Equals(descriptor.Table, SettingsStaticTextDescriptors.Table, StringComparison.Ordinal) &&
                Values.TryGetValue(descriptor.Key, out var value))
            {
                return FormatKnownDynamicText(descriptor, value);
            }

            return $"[{descriptor.Table}:{descriptor.Key}]";
        }

        private static string FormatKnownDynamicText(LocalizedTextDescriptor descriptor, string value)
        {
            if ((string.Equals(descriptor.Key, SettingsDynamicTextDescriptors.AudioVolumeValueKey, StringComparison.Ordinal) ||
                 string.Equals(descriptor.Key, SettingsDynamicTextDescriptors.AudioVolumeValueMutedKey, StringComparison.Ordinal)) &&
                TryGetPercentArgument(descriptor, out var percent))
            {
                return value
                    .Replace("{percent}", percent.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Replace("{0}", percent.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (string.Equals(descriptor.Key, SettingsDynamicTextDescriptors.DisplayResolutionValueKey, StringComparison.Ordinal) &&
                descriptor.Arguments.Count > 0)
            {
                return value.Replace("{0}", descriptor.Arguments[0]?.ToString() ?? string.Empty);
            }

            return value;
        }

        private static bool TryGetPercentArgument(LocalizedTextDescriptor descriptor, out int percent)
        {
            percent = 0;
            if (descriptor.Arguments.Count == 0 || descriptor.Arguments[0] == null)
            {
                return false;
            }

            if (descriptor.Arguments[0] is int positionalIntValue)
            {
                percent = positionalIntValue;
                return true;
            }

            if (descriptor.Arguments[0] is IDictionary<string, object> namedArguments &&
                namedArguments.TryGetValue("percent", out var namedValue) &&
                namedValue is int namedIntValue)
            {
                percent = namedIntValue;
                return true;
            }

            var property = descriptor.Arguments[0].GetType().GetProperty("percent");
            if (property == null)
            {
                return false;
            }

            var value = property.GetValue(descriptor.Arguments[0]);
            if (value is int intValue)
            {
                percent = intValue;
                return true;
            }

            return false;
        }
    }
}
