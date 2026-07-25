using System;
using System.Collections.Generic;
using Game.Feature.UI.ViewShared;

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

    public sealed class SettingsScreenPayload : IScreenPayload
    {
        public static readonly SettingsScreenPayload Default = new();

        public SettingsScreenPayload(
            LocalizedTextDescriptor titleTextDescriptor = default,
            LocalizedTextDescriptor audioTabLabelDescriptor = default,
            LocalizedTextDescriptor displayTabLabelDescriptor = default,
            LocalizedTextDescriptor inputTabLabelDescriptor = default,
            LocalizedTextDescriptor movementLabelDescriptor = default,
            LocalizedTextDescriptor useArrowKeysLabelDescriptor = default,
            LocalizedTextDescriptor pushLabelDescriptor = default,
            LocalizedTextDescriptor flipLabelDescriptor = default,
            LocalizedTextDescriptor inputChangeLabelDescriptor = default,
            LocalizedTextDescriptor resetInputLabelDescriptor = default,
            LocalizedTextDescriptor backLabelDescriptor = default,
            LocalizedTextDescriptor languageLabelDescriptor = default,
            LocalizedTextDescriptor englishLanguageLabelDescriptor = default,
            LocalizedTextDescriptor koreanLanguageLabelDescriptor = default,
            LocalizedTextDescriptor audioMainLabelDescriptor = default,
            LocalizedTextDescriptor audioBgmLabelDescriptor = default,
            LocalizedTextDescriptor audioSfxLabelDescriptor = default,
            LocalizedTextDescriptor audioMuteLabelDescriptor = default,
            LocalizedTextDescriptor currentDisplayLabelDescriptor = default,
            LocalizedTextDescriptor resolutionLabelDescriptor = default,
            LocalizedTextDescriptor resolutionHintDescriptor = default,
            LocalizedTextDescriptor fullscreenWindowLabelDescriptor = default,
            LocalizedTextDescriptor fullscreenOnLabelDescriptor = default,
            LocalizedTextDescriptor displayApplyLabelDescriptor = default,
            LocalizedTextDescriptor displayRevertLabelDescriptor = default)
        {
            TitleTextDescriptor = OrDefault(titleTextDescriptor, SettingsStaticTextDescriptors.Title);
            AudioTabLabelDescriptor = OrDefault(audioTabLabelDescriptor, SettingsStaticTextDescriptors.AudioTab);
            DisplayTabLabelDescriptor = OrDefault(displayTabLabelDescriptor, SettingsStaticTextDescriptors.DisplayTab);
            InputTabLabelDescriptor = OrDefault(inputTabLabelDescriptor, SettingsStaticTextDescriptors.InputTab);
            MovementLabelDescriptor = OrDefault(movementLabelDescriptor, SettingsStaticTextDescriptors.MovementKeys);
            UseArrowKeysLabelDescriptor = OrDefault(useArrowKeysLabelDescriptor, SettingsStaticTextDescriptors.UseArrowKeys);
            PushLabelDescriptor = OrDefault(pushLabelDescriptor, SettingsStaticTextDescriptors.Push);
            FlipLabelDescriptor = OrDefault(flipLabelDescriptor, SettingsStaticTextDescriptors.Flip);
            InputChangeLabelDescriptor = OrDefault(inputChangeLabelDescriptor, SettingsStaticTextDescriptors.Change);
            ResetInputLabelDescriptor = OrDefault(resetInputLabelDescriptor, SettingsStaticTextDescriptors.ResetInput);
            BackLabelDescriptor = OrDefault(backLabelDescriptor, SettingsStaticTextDescriptors.Back);
            LanguageLabelDescriptor = OrDefault(languageLabelDescriptor, SettingsStaticTextDescriptors.Language);
            EnglishLanguageLabelDescriptor = OrDefault(englishLanguageLabelDescriptor, SettingsStaticTextDescriptors.LanguageEnglish);
            KoreanLanguageLabelDescriptor = OrDefault(koreanLanguageLabelDescriptor, SettingsStaticTextDescriptors.LanguageKorean);
            AudioMainLabelDescriptor = OrDefault(audioMainLabelDescriptor, SettingsStaticTextDescriptors.AudioMain);
            AudioBgmLabelDescriptor = OrDefault(audioBgmLabelDescriptor, SettingsStaticTextDescriptors.AudioBgm);
            AudioSfxLabelDescriptor = OrDefault(audioSfxLabelDescriptor, SettingsStaticTextDescriptors.AudioSfx);
            AudioMuteLabelDescriptor = OrDefault(audioMuteLabelDescriptor, SettingsStaticTextDescriptors.AudioMute);
            DisplayCurrentLabelDescriptor = OrDefault(currentDisplayLabelDescriptor, SettingsStaticTextDescriptors.DisplayCurrent);
            DisplayResolutionTextDescriptor = OrDefault(resolutionLabelDescriptor, SettingsStaticTextDescriptors.DisplayResolution);
            ResolutionHintDescriptor = OrDefault(resolutionHintDescriptor, SettingsStaticTextDescriptors.DisplayResolutionHint);
            FullscreenWindowLabelDescriptor = OrDefault(fullscreenWindowLabelDescriptor, SettingsStaticTextDescriptors.DisplayFullscreenWindow);
            FullscreenOnLabelDescriptor = OrDefault(fullscreenOnLabelDescriptor, SettingsStaticTextDescriptors.DisplayFullscreenOn);
            DisplayApplyButtonTextDescriptor = OrDefault(displayApplyLabelDescriptor, SettingsStaticTextDescriptors.DisplayApply);
            DisplayRevertButtonTextDescriptor = OrDefault(displayRevertLabelDescriptor, SettingsStaticTextDescriptors.DisplayRevert);
        }

        public LocalizedTextDescriptor TitleTextDescriptor { get; }

        public LocalizedTextDescriptor AudioTabLabelDescriptor { get; }

        public LocalizedTextDescriptor DisplayTabLabelDescriptor { get; }

        public LocalizedTextDescriptor InputTabLabelDescriptor { get; }

        public LocalizedTextDescriptor MovementLabelDescriptor { get; }

        public LocalizedTextDescriptor UseArrowKeysLabelDescriptor { get; }

        public LocalizedTextDescriptor PushLabelDescriptor { get; }

        public LocalizedTextDescriptor FlipLabelDescriptor { get; }

        public LocalizedTextDescriptor InputChangeLabelDescriptor { get; }

        public LocalizedTextDescriptor ResetInputLabelDescriptor { get; }

        public LocalizedTextDescriptor BackLabelDescriptor { get; }

        public LocalizedTextDescriptor LanguageLabelDescriptor { get; }

        public LocalizedTextDescriptor EnglishLanguageLabelDescriptor { get; }

        public LocalizedTextDescriptor KoreanLanguageLabelDescriptor { get; }

        public LocalizedTextDescriptor AudioMainLabelDescriptor { get; }

        public LocalizedTextDescriptor AudioBgmLabelDescriptor { get; }

        public LocalizedTextDescriptor AudioSfxLabelDescriptor { get; }

        public LocalizedTextDescriptor AudioMuteLabelDescriptor { get; }

        public LocalizedTextDescriptor DisplayCurrentLabelDescriptor { get; }

        public LocalizedTextDescriptor DisplayResolutionTextDescriptor { get; }

        public LocalizedTextDescriptor ResolutionHintDescriptor { get; }

        public LocalizedTextDescriptor FullscreenWindowLabelDescriptor { get; }

        public LocalizedTextDescriptor FullscreenOnLabelDescriptor { get; }

        public LocalizedTextDescriptor DisplayApplyButtonTextDescriptor { get; }

        public LocalizedTextDescriptor DisplayRevertButtonTextDescriptor { get; }

        private static LocalizedTextDescriptor OrDefault(
            LocalizedTextDescriptor descriptor,
            LocalizedTextDescriptor fallback)
        {
            return string.IsNullOrEmpty(descriptor.Table) && string.IsNullOrEmpty(descriptor.Key)
                ? fallback
                : descriptor;
        }
    }

    public static class SettingsStaticTextDescriptors
    {
        public const string Table = SettingsLocalizationContract.Table;

        public static readonly LocalizedTextDescriptor Title = new(
            Table,
            SettingsLocalizationContract.Keys.Title,
            LocalizedTextRole.Title,
            LocalizedTextWeight.Bold);

        public static readonly LocalizedTextDescriptor AudioTab = new(
            Table,
            SettingsLocalizationContract.Keys.AudioTab,
            LocalizedTextRole.Subtitle,
            LocalizedTextWeight.Bold);

        public static readonly LocalizedTextDescriptor DisplayTab = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayTab,
            LocalizedTextRole.Subtitle,
            LocalizedTextWeight.Bold);

        public static readonly LocalizedTextDescriptor InputTab = new(
            Table,
            SettingsLocalizationContract.Keys.InputTab,
            LocalizedTextRole.Subtitle,
            LocalizedTextWeight.Bold);

        public static readonly LocalizedTextDescriptor AudioMain = new(
            Table,
            SettingsLocalizationContract.Keys.AudioMain,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor AudioBgm = new(
            Table,
            SettingsLocalizationContract.Keys.AudioBgm,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor AudioSfx = new(
            Table,
            SettingsLocalizationContract.Keys.AudioSfx,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor AudioMute = new(
            Table,
            SettingsLocalizationContract.Keys.AudioMute,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor DisplayCurrent = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayCurrent,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor DisplayResolution = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayResolution,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor DisplayResolutionHint = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayResolutionHint,
            LocalizedTextRole.Body,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor DisplayFullscreenWindow = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayFullscreenWindow,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor DisplayFullscreenOn = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayFullscreenOn,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor DisplayApply = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayApply,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor DisplayRevert = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayRevert,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor MovementKeys = new(
            Table,
            SettingsLocalizationContract.Keys.InputMovementKeys,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor UseArrowKeys = new(
            Table,
            SettingsLocalizationContract.Keys.InputUseArrowKeys,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Push = new(
            Table,
            SettingsLocalizationContract.Keys.InputPush,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Flip = new(
            Table,
            SettingsLocalizationContract.Keys.InputFlip,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Change = new(
            Table,
            SettingsLocalizationContract.Keys.InputChange,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor ResetInput = new(
            Table,
            SettingsLocalizationContract.Keys.InputReset,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Language = new(
            Table,
            SettingsLocalizationContract.Keys.Language,
            LocalizedTextRole.Label,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor LanguageEnglish = new(
            Table,
            SettingsLocalizationContract.Keys.LanguageEnglish,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor LanguageKorean = new(
            Table,
            SettingsLocalizationContract.Keys.LanguageKorean,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Back = new(
            Table,
            SettingsLocalizationContract.Keys.Back,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor InputResetConfirmTitle = new(
            Table,
            SettingsLocalizationContract.Keys.InputResetConfirmTitle,
            LocalizedTextRole.Title,
            LocalizedTextWeight.Bold);

        public static readonly LocalizedTextDescriptor InputResetConfirmBody = new(
            Table,
            SettingsLocalizationContract.Keys.InputResetConfirmBody,
            LocalizedTextRole.Body,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor InputResetConfirmLabel = new(
            Table,
            SettingsLocalizationContract.Keys.InputResetConfirmLabel,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Cancel = new(
            Table,
            SettingsLocalizationContract.Keys.Cancel,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor DisplayPreviewConfirmTitle = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayPreviewConfirmTitle,
            LocalizedTextRole.Title,
            LocalizedTextWeight.Bold);

        public static readonly LocalizedTextDescriptor DisplayPreviewConfirmKeep = new(
            Table,
            SettingsLocalizationContract.Keys.DisplayPreviewConfirmKeep,
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);
    }

    public static class SettingsDynamicTextDescriptors
    {
        public const string AudioVolumeValueKey = SettingsLocalizationContract.Keys.AudioVolumeValue;
        public const string AudioVolumeValueMutedKey = SettingsLocalizationContract.Keys.AudioVolumeValueMuted;
        public const string DisplayResolutionValueKey = SettingsLocalizationContract.Keys.DisplayResolutionValue;
        public const string DisplayPreviewCountdownKey = SettingsLocalizationContract.Keys.DisplayPreviewCountdown;
        public const string DisplayPreviewActiveStatusKey = SettingsLocalizationContract.Keys.DisplayPreviewActiveStatus;
        public const string DisplayPreviewRevertedStatusKey = SettingsLocalizationContract.Keys.DisplayPreviewRevertedStatus;
        public const string DisplaySavedStatusKey = SettingsLocalizationContract.Keys.DisplaySavedStatus;
        public const string DisplayExternalDriftStatusKey = SettingsLocalizationContract.Keys.DisplayExternalDriftStatus;
        public const string InputRebindCanceledKey = SettingsLocalizationContract.Keys.InputRebindCanceled;
        public const string InputResetCompleteKey = SettingsLocalizationContract.Keys.InputResetComplete;
        public const string InputReservedKeyKey = SettingsLocalizationContract.Keys.InputReservedKey;
        public const string InputMovementConflictKey = SettingsLocalizationContract.Keys.InputMovementConflict;
        public const string InputAlreadyRebindingKey = SettingsLocalizationContract.Keys.InputAlreadyRebinding;
        public const string InputRebindPushPromptKey = SettingsLocalizationContract.Keys.InputRebindPushPrompt;
        public const string InputRebindFlipPromptKey = SettingsLocalizationContract.Keys.InputRebindFlipPrompt;
        public const string DisplayPreviewConfirmFullscreenBodyKey =
            SettingsLocalizationContract.Keys.DisplayPreviewConfirmFullscreenBody;
        public const string DisplayPreviewConfirmWindowedBodyKey =
            SettingsLocalizationContract.Keys.DisplayPreviewConfirmWindowedBody;

        public static LocalizedTextDescriptor AudioVolumeValue(int percent, bool isMuted)
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                isMuted ? AudioVolumeValueMutedKey : AudioVolumeValueKey,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular,
                new object[] { percent });
        }

        public static LocalizedTextDescriptor DisplayResolutionValue(string resolutionLabel)
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                DisplayResolutionValueKey,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular,
                new object[] { resolutionLabel ?? string.Empty });
        }

        public static LocalizedTextDescriptor DisplayPreviewCountdown(int seconds)
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                DisplayPreviewCountdownKey,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular,
                new object[] { seconds });
        }

        public static LocalizedTextDescriptor DisplayPreviewActiveStatus(int seconds)
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                DisplayPreviewActiveStatusKey,
                LocalizedTextRole.Body,
                LocalizedTextWeight.Regular,
                new object[] { seconds });
        }

        public static LocalizedTextDescriptor DisplayPreviewConfirmBody(
            int width,
            int height,
            bool isFullscreen,
            int seconds)
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                isFullscreen
                    ? DisplayPreviewConfirmFullscreenBodyKey
                    : DisplayPreviewConfirmWindowedBodyKey,
                LocalizedTextRole.Body,
                LocalizedTextWeight.Regular,
                new object[]
                {
                    Math.Max(1, width),
                    Math.Max(1, height),
                    Math.Max(0, seconds),
                });
        }

        public static LocalizedTextDescriptor DisplayPreviewRevertedStatus()
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                DisplayPreviewRevertedStatusKey,
                LocalizedTextRole.Body,
                LocalizedTextWeight.Regular);
        }

        public static LocalizedTextDescriptor DisplaySavedStatus()
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                DisplaySavedStatusKey,
                LocalizedTextRole.Body,
                LocalizedTextWeight.Regular);
        }

        public static LocalizedTextDescriptor DisplayExternalDriftStatus()
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                DisplayExternalDriftStatusKey,
                LocalizedTextRole.Body,
                LocalizedTextWeight.Regular);
        }

        public static LocalizedTextDescriptor InputRebindCanceled()
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                InputRebindCanceledKey,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular);
        }

        public static LocalizedTextDescriptor InputResetComplete()
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                InputResetCompleteKey,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular);
        }

        public static LocalizedTextDescriptor InputReservedKey()
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                InputReservedKeyKey,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular);
        }

        public static LocalizedTextDescriptor InputMovementConflict()
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                InputMovementConflictKey,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular);
        }

        public static LocalizedTextDescriptor InputAlreadyRebinding()
        {
            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                InputAlreadyRebindingKey,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular);
        }

        public static LocalizedTextDescriptor InputRebindPrompt(KeyboardBindableAction action)
        {
            string key;
            switch (action)
            {
                case KeyboardBindableAction.Push:
                    key = InputRebindPushPromptKey;
                    break;
                case KeyboardBindableAction.Flip:
                    key = InputRebindFlipPromptKey;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(action),
                        action,
                        "Unsupported keyboard rebind action.");
            }

            return new LocalizedTextDescriptor(
                SettingsStaticTextDescriptors.Table,
                key,
                LocalizedTextRole.Label,
                LocalizedTextWeight.Regular);
        }
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

        public bool IsDisplayStatusVisible { get; private set; }

        public bool IsDisplayStatusTransient { get; private set; }

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
            bool isPreviewCountdownVisible,
            bool isDisplayStatusVisible = true,
            bool isDisplayStatusTransient = false,
            string languageLabelText = "",
            string currentLanguageText = "",
            bool isLanguageSelectionAvailable = false)
        {
            CurrentDisplayValueText = currentDisplayValueText ?? string.Empty;
            ResolutionOptionTexts = resolutionOptionTexts ?? Array.Empty<string>();
            SelectedResolutionIndex = selectedResolutionIndex;
            IsFullscreenEnabled = isFullscreenEnabled;
            DisplayStatusText = displayStatusText ?? string.Empty;
            IsDisplayStatusVisible = isDisplayStatusVisible && DisplayStatusText.Length > 0;
            IsDisplayStatusTransient = IsDisplayStatusVisible && isDisplayStatusTransient;
            IsDisplayApplyInteractable = isDisplayApplyInteractable;
            IsDisplayRevertInteractable = isDisplayRevertInteractable;
            IsDisplayPreviewActive = isDisplayPreviewActive;
            PreviewCountdownText = previewCountdownText ?? string.Empty;
            PreviewCountdownNormalized = previewCountdownNormalized;
            IsPreviewCountdownVisible = isPreviewCountdownVisible;
            LanguageLabelText = languageLabelText ?? string.Empty;
            CurrentLanguageText = currentLanguageText ?? string.Empty;
            IsLanguageSelectionAvailable = isLanguageSelectionAvailable;
            Changed?.Invoke();
        }

        public string LanguageLabelText { get; private set; } = string.Empty;

        public string CurrentLanguageText { get; private set; } = string.Empty;

        public bool IsLanguageSelectionAvailable { get; private set; }
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
            string audioTabLabel,
            string displayTabLabel,
            string inputTabLabel,
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

        public bool IsContinueEnabled { get; private set; } = true;

        public void SetContent(
            bool isContinueEnabled = true)
        {
            IsContinueEnabled = isContinueEnabled;
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

    public sealed class GameClearScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string MainLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string mainLabel)
        {
            TitleText = titleText ?? string.Empty;
            MainLabel = mainLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }
}
