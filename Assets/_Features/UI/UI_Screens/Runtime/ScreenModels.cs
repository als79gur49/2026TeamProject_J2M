using System;
using System.Collections.Generic;
using Game.Feature.Stages;

namespace Game.Feature.UI.Screens
{
    public interface IScreenPayload
    {
    }

    public interface IScreenView
    {
        void SetIsCurrent(bool isCurrent);
    }

    public sealed class GameplayRootPayload : IScreenPayload
    {
        public static readonly GameplayRootPayload Default = new();
    }

    public sealed class ObjectiveStatusScreenPayload : IScreenPayload
    {
        public static readonly ObjectiveStatusScreenPayload Default = new("Objective Status");

        public ObjectiveStatusScreenPayload(string titleText)
        {
            TitleText = titleText ?? string.Empty;
        }

        public string TitleText { get; }
    }

    public sealed class SettingsScreenPayload : IScreenPayload
    {
        public static readonly SettingsScreenPayload Default = new(
            "Settings",
            "Audio",
            "Display",
            "Input",
            "Movement Keys",
            "Use Arrow Keys",
            "Push",
            "Flip",
            "Change",
            "Reset Input",
            "Back");

        public SettingsScreenPayload(
            string titleText,
            string backLabel)
            : this(
                titleText,
                "Audio",
                "Display",
                "Input",
                "Movement Keys",
                "Use Arrow Keys",
                "Push",
                "Flip",
                "Change",
                "Reset Input",
                backLabel)
        {
        }

        public SettingsScreenPayload(
            string titleText,
            string audioTabLabel,
            string displayTabLabel,
            string inputTabLabel,
            string movementLabel,
            string useArrowKeysLabel,
            string pushLabel,
            string flipLabel,
            string inputChangeLabel,
            string resetInputLabel,
            string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            AudioTabLabel = audioTabLabel ?? string.Empty;
            DisplayTabLabel = displayTabLabel ?? string.Empty;
            InputTabLabel = inputTabLabel ?? string.Empty;
            MovementLabel = movementLabel ?? string.Empty;
            UseArrowKeysLabel = useArrowKeysLabel ?? string.Empty;
            PushLabel = pushLabel ?? string.Empty;
            FlipLabel = flipLabel ?? string.Empty;
            InputChangeLabel = inputChangeLabel ?? string.Empty;
            ResetInputLabel = resetInputLabel ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string AudioTabLabel { get; }

        public string DisplayTabLabel { get; }

        public string InputTabLabel { get; }

        public string MovementLabel { get; }

        public string UseArrowKeysLabel { get; }

        public string PushLabel { get; }

        public string FlipLabel { get; }

        public string InputChangeLabel { get; }

        public string ResetInputLabel { get; }

        public string BackLabel { get; }
    }

    public enum AudioSettingsChannel
    {
        Main = 0,
        Bgm = 1,
        Sfx = 2,
    }

    public readonly struct AudioSettingsRowViewModel
    {
        public AudioSettingsRowViewModel(
            string valueText,
            float normalizedValue,
            bool isMuted)
        {
            ValueText = valueText ?? string.Empty;
            NormalizedValue = normalizedValue;
            IsMuted = isMuted;
        }

        public string ValueText { get; }

        public float NormalizedValue { get; }

        public bool IsMuted { get; }
    }

    public readonly struct SettingsScreenState
    {
        public SettingsScreenState(bool areTooltipsEnabled, bool isLargeTextEnabled)
        {
            AreTooltipsEnabled = areTooltipsEnabled;
            IsLargeTextEnabled = isLargeTextEnabled;
        }

        public bool AreTooltipsEnabled { get; }

        public bool IsLargeTextEnabled { get; }
    }

    public enum SettingsSectionId
    {
        Audio = 0,
        Display = 1,
        Input = 2,
    }

    public enum KeyboardMovementScheme
    {
        Wasd = 0,
        ArrowKeys = 1,
    }

    public enum KeyboardBindableAction
    {
        Push = 0,
        Flip = 1,
    }

    public enum KeyboardBindingValidationResult
    {
        Success = 0,
        Canceled = 1,
        AlreadyRebinding = 2,
        MissingBinding = 3,
        InvalidKey = 4,
        ReservedKey = 5,
        DuplicateAction = 6,
        MovementConflict = 7,
    }

    public sealed class StageResultScreenPayload : IScreenPayload
    {
        public StageResultScreenPayload(
            string titleText,
            string summaryText,
            string detailText,
            string continueLabel,
            StageNavigationRequest continueStageRequest,
            StageNavigationRequest retryStageRequest,
            StageNavigationRequest nextStageRequest)
        {
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            ContinueLabel = continueLabel ?? string.Empty;
            ContinueStageRequest = continueStageRequest;
            RetryStageRequest = retryStageRequest;
            NextStageRequest = nextStageRequest;
        }

        public string TitleText { get; }

        public string SummaryText { get; }

        public string DetailText { get; }

        public string ContinueLabel { get; }

        public StageNavigationRequest ContinueStageRequest { get; }

        public StageNavigationRequest RetryStageRequest { get; }

        public StageNavigationRequest NextStageRequest { get; }
    }

    public sealed class LevelFailedScreenPayload : IScreenPayload
    {
        public LevelFailedScreenPayload(
            string titleText,
            string detailText,
            string restartLevelLabel,
            string mainLabel,
            StageNavigationRequest restartLevelRequest)
        {
            TitleText = string.IsNullOrWhiteSpace(titleText) ? "Level Failed" : titleText;
            DetailText = detailText ?? string.Empty;
            RestartLevelLabel = string.IsNullOrWhiteSpace(restartLevelLabel)
                ? "Restart Level"
                : restartLevelLabel;
            MainLabel = string.IsNullOrWhiteSpace(mainLabel) ? "Main" : mainLabel;
            RestartLevelRequest = restartLevelRequest;
        }

        public string TitleText { get; }

        public string DetailText { get; }

        public string RestartLevelLabel { get; }

        public string MainLabel { get; }

        public StageNavigationRequest RestartLevelRequest { get; }
    }

    public sealed class SettingsAudioViewModel
    {
        public event Action Changed;

        public AudioSettingsRowViewModel MainAudio { get; private set; }

        public AudioSettingsRowViewModel BgmAudio { get; private set; }

        public AudioSettingsRowViewModel SfxAudio { get; private set; }

        public void SetContent(
            AudioSettingsRowViewModel mainAudio,
            AudioSettingsRowViewModel bgmAudio,
            AudioSettingsRowViewModel sfxAudio)
        {
            MainAudio = mainAudio;
            BgmAudio = bgmAudio;
            SfxAudio = sfxAudio;
            Changed?.Invoke();
        }
    }

    public sealed class SettingsDisplayViewModel
    {
        public event Action Changed;

        public string CurrentDisplayValueText { get; private set; } = string.Empty;

        public IReadOnlyList<string> ResolutionOptionTexts { get; private set; } = Array.Empty<string>();

        public int SelectedResolutionIndex { get; private set; }

        public bool IsFullscreenEnabled { get; private set; }

        public string DisplayStatusText { get; private set; } = string.Empty;

        public bool IsDisplayApplyInteractable { get; private set; }

        public bool IsDisplayRevertInteractable { get; private set; }

        public bool IsDisplayPreviewActive { get; private set; }

        public string PreviewCountdownText { get; private set; } = string.Empty;

        public float PreviewCountdownNormalized { get; private set; }

        public bool IsPreviewCountdownVisible { get; private set; }

        public void SetContent(
            string currentDisplayValueText,
            IReadOnlyList<string> resolutionOptionTexts,
            int selectedResolutionIndex,
            bool isFullscreenEnabled,
            string displayStatusText,
            bool isDisplayApplyInteractable,
            bool isDisplayRevertInteractable,
            bool isDisplayPreviewActive,
            string previewCountdownText,
            float previewCountdownNormalized,
            bool isPreviewCountdownVisible)
        {
            CurrentDisplayValueText = currentDisplayValueText ?? string.Empty;
            ResolutionOptionTexts = resolutionOptionTexts ?? Array.Empty<string>();
            SelectedResolutionIndex = selectedResolutionIndex;
            IsFullscreenEnabled = isFullscreenEnabled;
            DisplayStatusText = displayStatusText ?? string.Empty;
            IsDisplayApplyInteractable = isDisplayApplyInteractable;
            IsDisplayRevertInteractable = isDisplayRevertInteractable;
            IsDisplayPreviewActive = isDisplayPreviewActive;
            PreviewCountdownText = previewCountdownText ?? string.Empty;
            PreviewCountdownNormalized = previewCountdownNormalized;
            IsPreviewCountdownVisible = isPreviewCountdownVisible;
            Changed?.Invoke();
        }
    }

    public sealed class SettingsInputViewModel
    {
        public event Action Changed;

        public string MovementLabel { get; private set; } = string.Empty;

        public string UseArrowKeysLabel { get; private set; } = string.Empty;

        public bool UseArrowKeys { get; private set; }

        public string MovementCurrentText { get; private set; } = string.Empty;

        public string PushLabel { get; private set; } = string.Empty;

        public string PushCurrentText { get; private set; } = string.Empty;

        public string PushChangeLabel { get; private set; } = string.Empty;

        public string FlipLabel { get; private set; } = string.Empty;

        public string FlipCurrentText { get; private set; } = string.Empty;

        public string FlipChangeLabel { get; private set; } = string.Empty;

        public string ResetLabel { get; private set; } = string.Empty;

        public string StatusText { get; private set; } = string.Empty;

        public bool IsRebinding { get; private set; }

        public KeyboardBindableAction? RebindingAction { get; private set; }

        public bool AreControlsInteractable { get; private set; } = true;

        public void SetContent(
            string movementLabel,
            string useArrowKeysLabel,
            bool useArrowKeys,
            string movementCurrentText,
            string pushLabel,
            string pushCurrentText,
            string pushChangeLabel,
            string flipLabel,
            string flipCurrentText,
            string flipChangeLabel,
            string resetLabel,
            string statusText,
            bool isRebinding,
            KeyboardBindableAction? rebindingAction,
            bool areControlsInteractable)
        {
            MovementLabel = movementLabel ?? string.Empty;
            UseArrowKeysLabel = useArrowKeysLabel ?? string.Empty;
            UseArrowKeys = useArrowKeys;
            MovementCurrentText = movementCurrentText ?? string.Empty;
            PushLabel = pushLabel ?? string.Empty;
            PushCurrentText = pushCurrentText ?? string.Empty;
            PushChangeLabel = pushChangeLabel ?? string.Empty;
            FlipLabel = flipLabel ?? string.Empty;
            FlipCurrentText = flipCurrentText ?? string.Empty;
            FlipChangeLabel = flipChangeLabel ?? string.Empty;
            ResetLabel = resetLabel ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            IsRebinding = isRebinding;
            RebindingAction = rebindingAction;
            AreControlsInteractable = areControlsInteractable;
            Changed?.Invoke();
        }
    }

    public sealed class SettingsScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string BackLabel { get; private set; } = string.Empty;

        public string AudioTabLabel { get; private set; } = string.Empty;

        public string DisplayTabLabel { get; private set; } = string.Empty;

        public string InputTabLabel { get; private set; } = string.Empty;

        public SettingsSectionId SelectedSection { get; private set; }

        public void SetContent(
            string titleText,
            string backLabel,
            string audioTabLabel = "Audio",
            string displayTabLabel = "Display",
            string inputTabLabel = "Input",
            SettingsSectionId selectedSection = SettingsSectionId.Audio)
        {
            TitleText = titleText ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
            AudioTabLabel = audioTabLabel ?? string.Empty;
            DisplayTabLabel = displayTabLabel ?? string.Empty;
            InputTabLabel = inputTabLabel ?? string.Empty;
            SelectedSection = selectedSection;
            Changed?.Invoke();
        }
    }

    public sealed class StageResultScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string SummaryText { get; private set; } = string.Empty;

        public string DetailText { get; private set; } = string.Empty;

        public string ContinueLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string summaryText,
            string detailText,
            string continueLabel)
        {
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            ContinueLabel = continueLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class LevelFailedScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string DetailText { get; private set; } = string.Empty;

        public string RestartLevelLabel { get; private set; } = string.Empty;

        public string MainLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string detailText,
            string restartLevelLabel,
            string mainLabel)
        {
            TitleText = titleText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            RestartLevelLabel = restartLevelLabel ?? string.Empty;
            MainLabel = mainLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }
}
