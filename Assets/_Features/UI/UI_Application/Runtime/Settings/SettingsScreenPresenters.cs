using System;
using System.Collections.Generic;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public readonly struct SettingsAudioPresenterInput
    {
    }

    public readonly struct SettingsDisplayPresenterInput
    {
    }

    public sealed class SettingsAudioPresenter
    {
        private readonly IAudioSettingsPort _audioSettingsPort;

        public SettingsAudioPresenter(IAudioSettingsPort audioSettingsPort)
        {
            _audioSettingsPort = audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort));
        }

        public SettingsAudioViewModel ViewModel { get; } = new SettingsAudioViewModel();

        public void Apply(SettingsAudioPresenterInput input)
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

        private void RefreshViewModel()
        {
            var snapshot = _audioSettingsPort.Read();
            ViewModel.SetContent(
                BuildAudioRow(snapshot.Main),
                BuildAudioRow(snapshot.Bgm),
                BuildAudioRow(snapshot.Sfx));
        }

        private static AudioSettingsRowViewModel BuildAudioRow(AudioSettingsPortChannelState state)
        {
            var normalizedVolume = Clamp01(state.Volume);
            var percent = (int)Math.Round(normalizedVolume * 100f, MidpointRounding.AwayFromZero);
            var valueText = state.IsMuted
                ? $"{percent}% (Muted)"
                : $"{percent}%";
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
        private DisplaySettingsPortSnapshot _displaySnapshot = new(
            Array.Empty<DisplaySettingsPortModeOption>(),
            0,
            DisplayWindowMode.Windowed,
            string.Empty,
            DisplayWindowMode.Windowed,
            false);
        private int _stagedDisplayModeIndex;
        private DisplayWindowMode _stagedDisplayWindowMode;
        private string _displayStatusText = string.Empty;
        private bool _isDisplayStatusTransient;
        private DisplayPreviewCountdownSnapshot _previewCountdown = DisplayPreviewCountdownSnapshot.Inactive;

        public SettingsDisplayPresenter(IDisplaySettingsPort displaySettingsPort)
        {
            _displaySettingsPort = displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort));
        }

        public SettingsDisplayViewModel ViewModel { get; } = new SettingsDisplayViewModel();

        public void Apply(SettingsDisplayPresenterInput input, double previewTimeoutSeconds)
        {
            ClearPreviewCountdown();
            ResyncState(resetStagedToCommitted: true, previewTimeoutSeconds: previewTimeoutSeconds);
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
                ? $"Reverting in {_previewCountdown.RemainingSeconds}s"
                : string.Empty;
            var previewCountdownNormalized = isPreviewCountdownVisible
                ? Clamp01((float)_previewCountdown.RemainingSeconds / _previewCountdown.TotalSeconds)
                : 0f;

            ViewModel.SetContent(
                _displaySnapshot.CurrentRuntimeResolutionLabel,
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
                _isDisplayStatusTransient);
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
    }

    public readonly struct SettingsInputPresenterInput
    {
        public SettingsInputPresenterInput(
            string movementLabel,
            string useArrowKeysLabel,
            string pushLabel,
            string flipLabel,
            string changeLabel,
            string resetLabel)
        {
            MovementLabel = movementLabel ?? string.Empty;
            UseArrowKeysLabel = useArrowKeysLabel ?? string.Empty;
            PushLabel = pushLabel ?? string.Empty;
            FlipLabel = flipLabel ?? string.Empty;
            ChangeLabel = changeLabel ?? string.Empty;
            ResetLabel = resetLabel ?? string.Empty;
        }

        public string MovementLabel { get; }

        public string UseArrowKeysLabel { get; }

        public string PushLabel { get; }

        public string FlipLabel { get; }

        public string ChangeLabel { get; }

        public string ResetLabel { get; }
    }

    public sealed class SettingsInputPresenter
    {
        private readonly IKeyboardBindingSettingsPort _keyboardBindingSettingsPort;
        private SettingsInputPresenterInput _input = new SettingsInputPresenterInput(
            "Movement Keys",
            "Use Arrow Keys",
            "Push",
            "Flip",
            "Change",
            "Reset Input");
        private string _statusText = string.Empty;

        public SettingsInputPresenter(IKeyboardBindingSettingsPort keyboardBindingSettingsPort)
        {
            _keyboardBindingSettingsPort = keyboardBindingSettingsPort ?? throw new ArgumentNullException(nameof(keyboardBindingSettingsPort));
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
            _statusText = ToStatusText(result, KeyboardBindableAction.Push);
            RefreshViewModel(_keyboardBindingSettingsPort.Read());
        }

        public void StartRebind(KeyboardBindableAction action)
        {
            var startResult = _keyboardBindingSettingsPort.StartRebind(action, HandleRebindCompleted);
            if (startResult.Started)
            {
                _statusText = action == KeyboardBindableAction.Push
                    ? "Press a key for Push..."
                    : "Press a key for Flip...";
                RefreshViewModel(startResult.Snapshot);
                return;
            }

            _statusText = ToStatusText(startResult.ValidationResult, action);
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
            _statusText = "Input settings reset.";
            RefreshViewModel(snapshot);
        }

        private void HandleRebindCompleted(KeyboardRebindResult result)
        {
            _statusText = ToStatusText(result.ValidationResult, result.Action);
            RefreshViewModel(result.Snapshot);
            RebindCompleted?.Invoke(result);
        }

        private void RefreshViewModel(KeyboardBindingSettingsSnapshot snapshot)
        {
            var areControlsInteractable = !snapshot.IsRebinding;
            ViewModel.SetContent(
                _input.MovementLabel,
                _input.UseArrowKeysLabel,
                snapshot.MovementScheme == KeyboardMovementScheme.ArrowKeys,
                snapshot.MovementDisplayName,
                _input.PushLabel,
                snapshot.PushDisplayName,
                _input.ChangeLabel,
                _input.FlipLabel,
                snapshot.FlipDisplayName,
                _input.ChangeLabel,
                _input.ResetLabel,
                _statusText,
                snapshot.IsRebinding,
                snapshot.RebindingAction,
                areControlsInteractable);
        }

        private static string ToStatusText(KeyboardBindingValidationResult result, KeyboardBindableAction action)
        {
            switch (result)
            {
                case KeyboardBindingValidationResult.Success:
                    return string.Empty;
                case KeyboardBindingValidationResult.Canceled:
                    return "Rebind canceled.";
                case KeyboardBindingValidationResult.ReservedKey:
                    return "This key is reserved.";
                case KeyboardBindingValidationResult.DuplicateAction:
                    return action == KeyboardBindableAction.Push
                        ? "This key is already used by Flip."
                        : "This key is already used by Push.";
                case KeyboardBindingValidationResult.MovementConflict:
                    return "This key conflicts with movement keys.";
                case KeyboardBindingValidationResult.AlreadyRebinding:
                    return "Rebind already in progress.";
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
                NoOpKeyboardBindingSettingsPort.Instance)
        {
        }

        public SettingsScreenPresenter(
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort)
        {
            AudioPresenter = new SettingsAudioPresenter(audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort)));
            DisplayPresenter = new SettingsDisplayPresenter(displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort)));
            InputPresenter = new SettingsInputPresenter(keyboardBindingSettingsPort ?? throw new ArgumentNullException(nameof(keyboardBindingSettingsPort)));
        }

        public SettingsAudioPresenter AudioPresenter { get; }

        public SettingsDisplayPresenter DisplayPresenter { get; }

        public SettingsInputPresenter InputPresenter { get; }

        public SettingsScreenViewModel ViewModel { get; } = new SettingsScreenViewModel();

        public void Apply(SettingsScreenPayload payload, double previewTimeoutSeconds)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            AudioPresenter.Apply(default);
            DisplayPresenter.Apply(default, previewTimeoutSeconds);
            InputPresenter.Apply(new SettingsInputPresenterInput(
                _payload.MovementLabel,
                _payload.UseArrowKeysLabel,
                _payload.PushLabel,
                _payload.FlipLabel,
                _payload.InputChangeLabel,
                _payload.ResetInputLabel));
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
                _payload.TitleText,
                _payload.BackLabel,
                _payload.AudioTabLabel,
                _payload.DisplayTabLabel,
                _payload.InputTabLabel,
                _selectedSection);
        }
    }
}
