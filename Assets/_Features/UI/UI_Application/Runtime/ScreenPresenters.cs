using System;
using System.Collections.Generic;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public readonly struct AudioSettingsPortChannelState
    {
        public AudioSettingsPortChannelState(float volume, bool isMuted)
        {
            Volume = volume;
            IsMuted = isMuted;
        }

        public float Volume { get; }

        public bool IsMuted { get; }
    }

    public readonly struct AudioSettingsPortSnapshot
    {
        public AudioSettingsPortSnapshot(
            AudioSettingsPortChannelState main,
            AudioSettingsPortChannelState bgm,
            AudioSettingsPortChannelState sfx)
        {
            Main = main;
            Bgm = bgm;
            Sfx = sfx;
        }

        public AudioSettingsPortChannelState Main { get; }

        public AudioSettingsPortChannelState Bgm { get; }

        public AudioSettingsPortChannelState Sfx { get; }

        public AudioSettingsPortChannelState GetChannelState(AudioSettingsChannel channel)
        {
            switch (channel)
            {
                case AudioSettingsChannel.Main:
                    return Main;
                case AudioSettingsChannel.Bgm:
                    return Bgm;
                case AudioSettingsChannel.Sfx:
                    return Sfx;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }
    }

    public interface IAudioSettingsPort
    {
        AudioSettingsPortSnapshot Read();

        void SetVolume(AudioSettingsChannel channel, float volume);

        void SetMuted(AudioSettingsChannel channel, bool isMuted);

        void Flush();
    }

    public enum DisplayWindowMode
    {
        Windowed = 0,
        FullScreenWindow = 1,
    }

    public readonly struct DisplaySettingsPortModeOption
    {
        public DisplaySettingsPortModeOption(int width, int height, string labelText)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            LabelText = labelText ?? string.Empty;
        }

        public int Width { get; }

        public int Height { get; }

        public string LabelText { get; }
    }

    public readonly struct DisplaySettingsPortPreviewRequest
    {
        public DisplaySettingsPortPreviewRequest(int modeIndex, DisplayWindowMode windowMode)
        {
            ModeIndex = modeIndex;
            WindowMode = windowMode;
        }

        public int ModeIndex { get; }

        public DisplayWindowMode WindowMode { get; }
    }

    public readonly struct DisplaySettingsPortSnapshot
    {
        public DisplaySettingsPortSnapshot(
            IReadOnlyList<DisplaySettingsPortModeOption> availableModes,
            int committedModeIndex,
            DisplayWindowMode committedWindowMode,
            string currentRuntimeResolutionLabel,
            DisplayWindowMode currentRuntimeWindowMode,
            bool isPreviewActive)
        {
            AvailableModes = availableModes ?? Array.Empty<DisplaySettingsPortModeOption>();
            CommittedModeIndex = committedModeIndex;
            CommittedWindowMode = committedWindowMode;
            CurrentRuntimeResolutionLabel = currentRuntimeResolutionLabel ?? string.Empty;
            CurrentRuntimeWindowMode = currentRuntimeWindowMode;
            IsPreviewActive = isPreviewActive;
        }

        public IReadOnlyList<DisplaySettingsPortModeOption> AvailableModes { get; }

        public int CommittedModeIndex { get; }

        public DisplayWindowMode CommittedWindowMode { get; }

        public string CurrentRuntimeResolutionLabel { get; }

        public DisplayWindowMode CurrentRuntimeWindowMode { get; }

        public bool IsPreviewActive { get; }
    }

    public interface IDisplaySettingsPort
    {
        DisplaySettingsPortSnapshot Read();

        bool BeginPreview(DisplaySettingsPortPreviewRequest request);

        bool CommitPreview();

        bool RevertPreview();
    }

    public sealed class ObjectiveStatusScreenPresenter : IDisposable
    {
        private readonly ObjectiveStatusPresenter _objectiveStatusPresenter;
        private ObjectiveStatusScreenState _state;
        private string _titleText = ObjectiveStatusScreenPayload.Default.TitleText;

        public ObjectiveStatusScreenPresenter(ObjectiveStatusPresenter objectiveStatusPresenter)
        {
            _objectiveStatusPresenter = objectiveStatusPresenter ?? throw new ArgumentNullException(nameof(objectiveStatusPresenter));
            _state = objectiveStatusPresenter.CurrentState;
            _objectiveStatusPresenter.StateChanged += HandleStateChanged;
            ApplyViewModel();
        }

        public ObjectiveStatusScreenViewModel ViewModel { get; } = new ObjectiveStatusScreenViewModel();

        public void ApplyPayload(ObjectiveStatusScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _titleText = payload.TitleText;
            ApplyViewModel();
        }

        public ObjectiveInfoPopupPayload BuildInfoPopupPayload()
        {
            if (!_state.HasObjective)
            {
                return new ObjectiveInfoPopupPayload(
                    "Objective Info",
                    "No active objective is configured for this stage.");
            }

            return new ObjectiveInfoPopupPayload(
                "Objective Info",
                $"{BuildObjectiveSummary(_state)} Goal reached: {FormatBoolean(_state.GoalReached)} | All conditions: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}");
        }

        public void Dispose()
        {
            _objectiveStatusPresenter.StateChanged -= HandleStateChanged;
            _objectiveStatusPresenter.Dispose();
        }

        private void HandleStateChanged(ObjectiveStatusScreenState state)
        {
            _state = state;
            ApplyViewModel();
        }

        private void ApplyViewModel()
        {
            ViewModel.SetContent(
                _titleText,
                badgeText: BuildObjectiveBadge(_state),
                summaryText: BuildObjectiveSummary(_state),
                detailText: BuildObjectiveDetailText(_state),
                secondaryText: $"Goal: {FormatBoolean(_state.GoalReached)} | Required: {FormatBoolean(_state.AllConditionsSatisfied)} | Cleared: {FormatBoolean(_state.IsCleared)}");
        }

        private static string BuildObjectiveBadge(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "No Objective";
            }

            if (state.IsCleared)
            {
                return "Cleared";
            }

            if (state.AllConditionsSatisfied)
            {
                return "Ready";
            }

            if (state.GoalReached)
            {
                return "Goal Reached";
            }

            return "Pending";
        }

        private static string BuildObjectiveSummary(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "This stage currently has no active objective.";
            }

            if (!string.IsNullOrWhiteSpace(state.ObjectiveTitle) &&
                !string.IsNullOrWhiteSpace(state.ObjectiveSummary))
            {
                return $"{state.ObjectiveTitle}: {state.ObjectiveSummary}";
            }

            if (!string.IsNullOrWhiteSpace(state.ObjectiveTitle))
            {
                return state.ObjectiveTitle;
            }

            if (!string.IsNullOrWhiteSpace(state.ObjectiveSummary))
            {
                return state.ObjectiveSummary;
            }

            if (state.IsCleared)
            {
                return "The objective chain is fully cleared.";
            }

            if (state.AllConditionsSatisfied)
            {
                return "All objective conditions are currently satisfied.";
            }

            if (state.GoalReached)
            {
                return "Primary goal reached. Waiting on remaining conditions.";
            }

            return "Primary goal is still in progress.";
        }

        private static string BuildObjectiveDetailText(ObjectiveStatusScreenState state)
        {
            if (!state.HasObjective)
            {
                return "No objective conditions are configured for display.";
            }

            return string.IsNullOrWhiteSpace(state.ConditionDetailText)
                ? "No displayable objective conditions."
                : state.ConditionDetailText;
        }

        private static string FormatBoolean(bool value)
        {
            return value ? "Yes" : "No";
        }

    }

    public sealed class AccessibilitySettingsStore
    {
        public SettingsScreenState State { get; private set; } = new SettingsScreenState(
            areTooltipsEnabled: true,
            isLargeTextEnabled: false);

        public void ToggleTooltips()
        {
            State = new SettingsScreenState(!State.AreTooltipsEnabled, State.IsLargeTextEnabled);
        }

        public void ToggleLargeText()
        {
            State = new SettingsScreenState(State.AreTooltipsEnabled, !State.IsLargeTextEnabled);
        }
    }

    public readonly struct SettingsAudioPresenterInput
    {
        public SettingsAudioPresenterInput(
            string mainAudioLabel,
            string bgmAudioLabel,
            string sfxAudioLabel)
        {
            MainAudioLabel = mainAudioLabel ?? string.Empty;
            BgmAudioLabel = bgmAudioLabel ?? string.Empty;
            SfxAudioLabel = sfxAudioLabel ?? string.Empty;
        }

        public string MainAudioLabel { get; }

        public string BgmAudioLabel { get; }

        public string SfxAudioLabel { get; }
    }

    public readonly struct SettingsDisplayPresenterInput
    {
        public SettingsDisplayPresenterInput(
            string displaySectionTitle,
            string currentDisplayLabel,
            string resolutionLabel,
            string resolutionHoverHintText,
            string fullscreenLabel,
            string displayApplyLabel,
            string displayRevertLabel)
        {
            DisplaySectionTitle = displaySectionTitle ?? string.Empty;
            CurrentDisplayLabel = currentDisplayLabel ?? string.Empty;
            ResolutionLabel = resolutionLabel ?? string.Empty;
            ResolutionHoverHintText = resolutionHoverHintText ?? string.Empty;
            FullscreenLabel = fullscreenLabel ?? string.Empty;
            DisplayApplyLabel = displayApplyLabel ?? string.Empty;
            DisplayRevertLabel = displayRevertLabel ?? string.Empty;
        }

        public string DisplaySectionTitle { get; }

        public string CurrentDisplayLabel { get; }

        public string ResolutionLabel { get; }

        public string ResolutionHoverHintText { get; }

        public string FullscreenLabel { get; }

        public string DisplayApplyLabel { get; }

        public string DisplayRevertLabel { get; }
    }

    public sealed class SettingsAudioPresenter
    {
        private readonly IAudioSettingsPort _audioSettingsPort;
        private SettingsAudioPresenterInput _input;

        public SettingsAudioPresenter(IAudioSettingsPort audioSettingsPort)
        {
            _audioSettingsPort = audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort));
        }

        public SettingsAudioViewModel ViewModel { get; } = new SettingsAudioViewModel();

        public void Apply(SettingsAudioPresenterInput input)
        {
            _input = input;
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
                BuildAudioRow(_input.MainAudioLabel, snapshot.Main),
                BuildAudioRow(_input.BgmAudioLabel, snapshot.Bgm),
                BuildAudioRow(_input.SfxAudioLabel, snapshot.Sfx));
        }

        private static AudioSettingsRowViewModel BuildAudioRow(
            string labelText,
            AudioSettingsPortChannelState state)
        {
            var normalizedVolume = Clamp01(state.Volume);
            var percent = (int)Math.Round(normalizedVolume * 100f, MidpointRounding.AwayFromZero);
            var valueText = state.IsMuted
                ? $"{percent}% (Muted)"
                : $"{percent}%";
            return new AudioSettingsRowViewModel(labelText, valueText, normalizedVolume, state.IsMuted);
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
        private SettingsDisplayPresenterInput _input;
        private int _stagedDisplayModeIndex;
        private DisplayWindowMode _stagedDisplayWindowMode;
        private string _displayStatusText = string.Empty;
        private DisplayPreviewCountdownSnapshot _previewCountdown = DisplayPreviewCountdownSnapshot.Inactive;

        public SettingsDisplayPresenter(IDisplaySettingsPort displaySettingsPort)
        {
            _displaySettingsPort = displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort));
        }

        public SettingsDisplayViewModel ViewModel { get; } = new SettingsDisplayViewModel();

        public void Apply(SettingsDisplayPresenterInput input, double previewTimeoutSeconds)
        {
            _input = input;
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
                overrideStatusText: started ? BuildPreviewActiveStatusText(previewTimeoutSeconds) : null);
            return started;
        }

        public bool CancelPreview()
        {
            var reverted = _displaySettingsPort.RevertPreview();
            ClearPreviewCountdown();
            ResyncState(
                resetStagedToCommitted: true,
                previewTimeoutSeconds: 0d,
                overrideStatusText: reverted ? PreviewRevertedStatusText : null);
            return reverted;
        }

        public bool ConfirmPreview()
        {
            var committed = _displaySettingsPort.CommitPreview();
            ClearPreviewCountdown();
            ResyncState(
                resetStagedToCommitted: true,
                previewTimeoutSeconds: 0d,
                overrideStatusText: committed ? PreviewCommittedStatusText : null);
            return committed;
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
            RefreshViewModel();
        }

        public void StageWindowMode(DisplayWindowMode mode)
        {
            if (_displaySnapshot.IsPreviewActive)
            {
                return;
            }

            _stagedDisplayWindowMode = mode;
            RefreshViewModel();
        }

        private void ResyncState(
            bool resetStagedToCommitted,
            double previewTimeoutSeconds,
            string overrideStatusText = null)
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
            RefreshViewModel();
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
                _input.DisplaySectionTitle,
                _input.CurrentDisplayLabel,
                _displaySnapshot.CurrentRuntimeResolutionLabel,
                _input.ResolutionLabel,
                _input.ResolutionHoverHintText,
                resolutionOptions,
                _stagedDisplayModeIndex,
                _input.FullscreenLabel,
                _stagedDisplayWindowMode == DisplayWindowMode.FullScreenWindow,
                _displayStatusText,
                _input.DisplayApplyLabel,
                IsDirty() && !_displaySnapshot.IsPreviewActive,
                _input.DisplayRevertLabel,
                IsDirty() && !_displaySnapshot.IsPreviewActive,
                _displaySnapshot.IsPreviewActive,
                previewCountdownText,
                previewCountdownNormalized,
                isPreviewCountdownVisible);
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
            string sectionTitle,
            string movementLabel,
            string useArrowKeysLabel,
            string pushLabel,
            string flipLabel,
            string changeLabel,
            string resetLabel)
        {
            SectionTitle = sectionTitle ?? string.Empty;
            MovementLabel = movementLabel ?? string.Empty;
            UseArrowKeysLabel = useArrowKeysLabel ?? string.Empty;
            PushLabel = pushLabel ?? string.Empty;
            FlipLabel = flipLabel ?? string.Empty;
            ChangeLabel = changeLabel ?? string.Empty;
            ResetLabel = resetLabel ?? string.Empty;
        }

        public string SectionTitle { get; }

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
            "Input",
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
        }

        private void RefreshViewModel(KeyboardBindingSettingsSnapshot snapshot)
        {
            var areControlsInteractable = !snapshot.IsRebinding;
            ViewModel.SetContent(
                _input.SectionTitle,
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
        private readonly AccessibilitySettingsStore _accessibilitySettingsStore;
        private SettingsScreenPayload _payload = SettingsScreenPayload.Default;
        private SettingsSectionId _selectedSection = SettingsSectionId.Audio;

        public SettingsScreenPresenter(
            AccessibilitySettingsStore accessibilitySettingsStore,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort)
            : this(
                accessibilitySettingsStore,
                audioSettingsPort,
                displaySettingsPort,
                NoOpKeyboardBindingSettingsPort.Instance)
        {
        }

        public SettingsScreenPresenter(
            AccessibilitySettingsStore accessibilitySettingsStore,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort)
        {
            _accessibilitySettingsStore = accessibilitySettingsStore ?? throw new ArgumentNullException(nameof(accessibilitySettingsStore));
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
            AudioPresenter.Apply(new SettingsAudioPresenterInput(
                _payload.MainAudioLabel,
                _payload.BgmAudioLabel,
                _payload.SfxAudioLabel));
            DisplayPresenter.Apply(new SettingsDisplayPresenterInput(
                _payload.DisplaySectionTitle,
                _payload.CurrentDisplayLabel,
                _payload.ResolutionLabel,
                _payload.ResolutionHoverHintText,
                _payload.FullscreenLabel,
                _payload.DisplayApplyLabel,
                _payload.DisplayRevertLabel),
                previewTimeoutSeconds);
            InputPresenter.Apply(new SettingsInputPresenterInput(
                _payload.InputSectionTitle,
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

        public TooltipPopupPayload BuildTooltipInfoPayload()
        {
            var tooltipsEnabledText = _accessibilitySettingsStore.State.AreTooltipsEnabled ? "Enabled" : "Disabled";
            return new TooltipPopupPayload(
                "Tooltips",
                $"Tooltips show short contextual hints for UI controls. They are currently {tooltipsEnabledText} in this session; use {_payload.TooltipToggleLabel} to change that.",
                TooltipPopupAnchorPreset.Center);
        }

        public void ToggleLargeText()
        {
            _accessibilitySettingsStore.ToggleLargeText();
            RefreshViewModel();
        }

        public void ToggleTooltips()
        {
            _accessibilitySettingsStore.ToggleTooltips();
            RefreshViewModel();
        }

        private void RefreshViewModel()
        {
            var accessibilityState = _accessibilitySettingsStore.State;
            ViewModel.SetContent(
                _payload.TitleText,
                accessibilityState.AreTooltipsEnabled ? "Enabled" : "Disabled",
                accessibilityState.IsLargeTextEnabled ? "Enabled" : "Disabled",
                _payload.TooltipToggleLabel,
                _payload.LargeTextToggleLabel,
                _payload.BackLabel,
                _payload.AudioTabLabel,
                _payload.DisplayTabLabel,
                _payload.InputTabLabel,
                _selectedSection);
        }
    }

    public sealed class StageResultScreenPresenter
    {
        public StageResultScreenViewModel ViewModel { get; } = new StageResultScreenViewModel();

        public void Apply(StageResultScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.SummaryText,
                payload.DetailText,
                payload.ContinueLabel);
        }
    }

    public sealed class LevelFailedScreenPresenter
    {
        public LevelFailedScreenViewModel ViewModel { get; } = new LevelFailedScreenViewModel();

        public void Apply(LevelFailedScreenPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.DetailText,
                payload.RestartLevelLabel,
                payload.MainLabel);
        }
    }
}
