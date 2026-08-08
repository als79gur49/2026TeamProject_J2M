using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Feature.UI.ViewShared
{
    public static class HudWorldGuideLocalizationKeys
    {
        public const string Movement = "ui.world_guide.move";
        public const string Push = "ui.world_guide.push";
        public const string Flip = "ui.world_guide.flip";
    }

    public enum LocalizedTextRole
    {
        Title,
        Subtitle,
        Body,
        Button,
        Label,
    }

    public enum LocalizedTextWeight
    {
        Default,
        Regular,
        Bold,
    }

    public enum SettingsLocalizationEntryId
    {
        Title,
        AudioTab,
        DisplayTab,
        InputTab,
        AudioMain,
        AudioBgm,
        AudioSfx,
        AudioMute,
        DisplayCurrent,
        DisplayResolution,
        DisplayResolutionHint,
        DisplayFullscreenWindow,
        DisplayFullscreenOn,
        DisplayApply,
        DisplayRevert,
        InputMovementKeys,
        InputPush,
        InputFlip,
        InputReset,
        Language,
        LanguageEnglish,
        LanguageKorean,
        Back,
        AudioVolumeValue,
        AudioVolumeValueMuted,
        DisplayResolutionValue,
        DisplayPreviewCountdown,
        DisplayPreviewActiveStatus,
        DisplayPreviewRevertedStatus,
        DisplaySavedStatus,
        DisplayExternalDriftStatus,
        InputRebindCanceled,
        InputResetComplete,
        InputReservedKey,
        InputMovementConflict,
        InputAlreadyRebinding,
        InputActionConflict,
        InputUnsupportedKey,
        InputRebindPushPrompt,
        InputRebindFlipPrompt,
        InputResetConfirmTitle,
        InputResetConfirmBody,
        InputResetConfirmLabel,
        Cancel,
        DisplayPreviewConfirmTitle,
        DisplayPreviewConfirmFullscreenBody,
        DisplayPreviewConfirmWindowedBody,
        DisplayPreviewConfirmKeep,
    }

    public enum SettingsLocalizationFormatKind
    {
        None,
        PercentArgument,
        PositionalArgument,
    }

    [Flags]
    public enum SettingsLocalizationCoverage
    {
        None = 0,
        StaticDescriptor = 1 << 0,
        DynamicDescriptor = 1 << 1,
        Bootstrap = 1 << 2,
        PackageFreeFallback = 1 << 3,
        InvariantFallback = 1 << 4,
    }

    public readonly struct SettingsLocalizationContractEntry
    {
        public SettingsLocalizationContractEntry(
            SettingsLocalizationEntryId id,
            string key,
            bool isSmart,
            SettingsLocalizationFormatKind formatKind,
            SettingsLocalizationCoverage coverage)
        {
            Id = id;
            Key = key ?? string.Empty;
            IsSmart = isSmart;
            FormatKind = formatKind;
            Coverage = coverage;
        }

        public SettingsLocalizationEntryId Id { get; }

        public string Table => SettingsLocalizationContract.Table;

        public string Key { get; }

        public bool IsSmart { get; }

        public SettingsLocalizationFormatKind FormatKind { get; }

        public SettingsLocalizationCoverage Coverage { get; }
    }

    public static class SettingsLocalizationContract
    {
        public const string Table = "UI";

        private const SettingsLocalizationCoverage StaticCoverage =
            SettingsLocalizationCoverage.StaticDescriptor |
            SettingsLocalizationCoverage.Bootstrap |
            SettingsLocalizationCoverage.PackageFreeFallback |
            SettingsLocalizationCoverage.InvariantFallback;

        private const SettingsLocalizationCoverage DynamicCoverage =
            SettingsLocalizationCoverage.DynamicDescriptor |
            SettingsLocalizationCoverage.Bootstrap |
            SettingsLocalizationCoverage.PackageFreeFallback |
            SettingsLocalizationCoverage.InvariantFallback;

        public static class Keys
        {
            public const string Title = "ui.settings.title";
            public const string AudioTab = "ui.settings.audio";
            public const string DisplayTab = "ui.settings.display";
            public const string InputTab = "ui.settings.input";
            public const string AudioMain = "ui.settings.audio.main";
            public const string AudioBgm = "ui.settings.audio.bgm";
            public const string AudioSfx = "ui.settings.audio.sfx";
            public const string AudioMute = "ui.settings.audio.mute";
            public const string DisplayCurrent = "ui.settings.display.current";
            public const string DisplayResolution = "ui.settings.display.resolution";
            public const string DisplayResolutionHint = "ui.settings.display.resolution_hint";
            public const string DisplayFullscreenWindow = "ui.settings.display.fullscreen_window";
            public const string DisplayFullscreenOn = "ui.settings.display.fullscreen_on";
            public const string DisplayApply = "ui.settings.display.apply";
            public const string DisplayRevert = "ui.settings.display.revert";
            public const string InputMovementKeys = "ui.settings.input.movement_keys";
            public const string InputPush = "ui.settings.input.push";
            public const string InputFlip = "ui.settings.input.flip";
            public const string InputReset = "ui.settings.input.reset_input";
            public const string Language = "ui.settings.language";
            public const string LanguageEnglish = "ui.settings.language.english";
            public const string LanguageKorean = "ui.settings.language.korean";
            public const string Back = "ui.common.back";
            public const string AudioVolumeValue = "ui.settings.audio.volume_value";
            public const string AudioVolumeValueMuted = "ui.settings.audio.volume_value_muted";
            public const string DisplayResolutionValue = "ui.settings.display.resolution_value";
            public const string DisplayPreviewCountdown = "ui.settings.display.preview_countdown";
            public const string DisplayPreviewActiveStatus = "ui.settings.display.status.preview_active";
            public const string DisplayPreviewRevertedStatus = "ui.settings.display.status.preview_reverted";
            public const string DisplaySavedStatus = "ui.settings.display.status.saved";
            public const string DisplayExternalDriftStatus = "ui.settings.display.status.external_drift";
            public const string InputRebindCanceled = "ui.settings.input.rebind_canceled";
            public const string InputResetComplete = "ui.settings.input.reset_complete";
            public const string InputReservedKey = "ui.settings.input.reserved_key";
            public const string InputMovementConflict = "ui.settings.input.movement_conflict";
            public const string InputAlreadyRebinding = "ui.settings.input.already_rebinding";
            public const string InputActionConflict = "ui.settings.input.action_conflict";
            public const string InputUnsupportedKey = "ui.settings.input.unsupported_key";
            public const string InputRebindPushPrompt = "ui.settings.input.rebind_push_prompt";
            public const string InputRebindFlipPrompt = "ui.settings.input.rebind_flip_prompt";
            public const string InputResetConfirmTitle = "ui.settings.input.reset_confirm.title";
            public const string InputResetConfirmBody = "ui.settings.input.reset_confirm.body";
            public const string InputResetConfirmLabel = "ui.settings.input.reset_confirm.confirm";
            public const string Cancel = "ui.common.cancel";
            public const string DisplayPreviewConfirmTitle = "ui.settings.display.preview_confirm.title";
            public const string DisplayPreviewConfirmFullscreenBody =
                "ui.settings.display.preview_confirm.fullscreen_body";
            public const string DisplayPreviewConfirmWindowedBody =
                "ui.settings.display.preview_confirm.windowed_body";
            public const string DisplayPreviewConfirmKeep = "ui.settings.display.preview_confirm.keep";
        }

        private static readonly IReadOnlyList<SettingsLocalizationContractEntry> ContractEntries =
            Array.AsReadOnly(new[]
            {
                Static(SettingsLocalizationEntryId.Title, Keys.Title),
                Static(SettingsLocalizationEntryId.AudioTab, Keys.AudioTab),
                Static(SettingsLocalizationEntryId.DisplayTab, Keys.DisplayTab),
                Static(SettingsLocalizationEntryId.InputTab, Keys.InputTab),
                Static(SettingsLocalizationEntryId.AudioMain, Keys.AudioMain),
                Static(SettingsLocalizationEntryId.AudioBgm, Keys.AudioBgm),
                Static(SettingsLocalizationEntryId.AudioSfx, Keys.AudioSfx),
                Static(SettingsLocalizationEntryId.AudioMute, Keys.AudioMute),
                Static(SettingsLocalizationEntryId.DisplayCurrent, Keys.DisplayCurrent),
                Static(SettingsLocalizationEntryId.DisplayResolution, Keys.DisplayResolution),
                Static(SettingsLocalizationEntryId.DisplayResolutionHint, Keys.DisplayResolutionHint),
                Static(SettingsLocalizationEntryId.DisplayFullscreenWindow, Keys.DisplayFullscreenWindow),
                Static(SettingsLocalizationEntryId.DisplayFullscreenOn, Keys.DisplayFullscreenOn),
                Static(SettingsLocalizationEntryId.DisplayApply, Keys.DisplayApply),
                Static(SettingsLocalizationEntryId.DisplayRevert, Keys.DisplayRevert),
                Static(SettingsLocalizationEntryId.InputMovementKeys, Keys.InputMovementKeys),
                Static(SettingsLocalizationEntryId.InputPush, Keys.InputPush),
                Static(SettingsLocalizationEntryId.InputFlip, Keys.InputFlip),
                Static(SettingsLocalizationEntryId.InputReset, Keys.InputReset),
                Static(SettingsLocalizationEntryId.Language, Keys.Language),
                Static(SettingsLocalizationEntryId.LanguageEnglish, Keys.LanguageEnglish),
                Static(SettingsLocalizationEntryId.LanguageKorean, Keys.LanguageKorean),
                Static(SettingsLocalizationEntryId.Back, Keys.Back),
                Static(SettingsLocalizationEntryId.InputResetConfirmTitle, Keys.InputResetConfirmTitle),
                Static(SettingsLocalizationEntryId.InputResetConfirmBody, Keys.InputResetConfirmBody),
                Static(SettingsLocalizationEntryId.InputResetConfirmLabel, Keys.InputResetConfirmLabel),
                Static(SettingsLocalizationEntryId.Cancel, Keys.Cancel),
                Static(SettingsLocalizationEntryId.DisplayPreviewConfirmTitle, Keys.DisplayPreviewConfirmTitle),
                Static(SettingsLocalizationEntryId.DisplayPreviewConfirmKeep, Keys.DisplayPreviewConfirmKeep),
                Dynamic(
                    SettingsLocalizationEntryId.AudioVolumeValue,
                    Keys.AudioVolumeValue,
                    SettingsLocalizationFormatKind.PercentArgument),
                Dynamic(
                    SettingsLocalizationEntryId.AudioVolumeValueMuted,
                    Keys.AudioVolumeValueMuted,
                    SettingsLocalizationFormatKind.PercentArgument),
                Dynamic(
                    SettingsLocalizationEntryId.DisplayResolutionValue,
                    Keys.DisplayResolutionValue,
                    SettingsLocalizationFormatKind.PositionalArgument),
                Dynamic(
                    SettingsLocalizationEntryId.DisplayPreviewCountdown,
                    Keys.DisplayPreviewCountdown,
                    SettingsLocalizationFormatKind.PositionalArgument),
                Dynamic(
                    SettingsLocalizationEntryId.DisplayPreviewActiveStatus,
                    Keys.DisplayPreviewActiveStatus,
                    SettingsLocalizationFormatKind.PositionalArgument),
                Dynamic(SettingsLocalizationEntryId.DisplayPreviewRevertedStatus, Keys.DisplayPreviewRevertedStatus),
                Dynamic(SettingsLocalizationEntryId.DisplaySavedStatus, Keys.DisplaySavedStatus),
                Dynamic(SettingsLocalizationEntryId.DisplayExternalDriftStatus, Keys.DisplayExternalDriftStatus),
                Dynamic(SettingsLocalizationEntryId.InputRebindCanceled, Keys.InputRebindCanceled),
                Dynamic(SettingsLocalizationEntryId.InputResetComplete, Keys.InputResetComplete),
                Dynamic(SettingsLocalizationEntryId.InputReservedKey, Keys.InputReservedKey),
                Dynamic(SettingsLocalizationEntryId.InputMovementConflict, Keys.InputMovementConflict),
                Dynamic(SettingsLocalizationEntryId.InputAlreadyRebinding, Keys.InputAlreadyRebinding),
                Dynamic(
                    SettingsLocalizationEntryId.InputActionConflict,
                    Keys.InputActionConflict,
                    SettingsLocalizationFormatKind.PositionalArgument),
                Dynamic(SettingsLocalizationEntryId.InputUnsupportedKey, Keys.InputUnsupportedKey),
                Dynamic(SettingsLocalizationEntryId.InputRebindPushPrompt, Keys.InputRebindPushPrompt),
                Dynamic(SettingsLocalizationEntryId.InputRebindFlipPrompt, Keys.InputRebindFlipPrompt),
                Dynamic(
                    SettingsLocalizationEntryId.DisplayPreviewConfirmFullscreenBody,
                    Keys.DisplayPreviewConfirmFullscreenBody,
                    SettingsLocalizationFormatKind.PositionalArgument),
                Dynamic(
                    SettingsLocalizationEntryId.DisplayPreviewConfirmWindowedBody,
                    Keys.DisplayPreviewConfirmWindowedBody,
                    SettingsLocalizationFormatKind.PositionalArgument),
            });

        public static IReadOnlyList<SettingsLocalizationContractEntry> Entries => ContractEntries;

        private static SettingsLocalizationContractEntry Static(
            SettingsLocalizationEntryId id,
            string key)
        {
            return new SettingsLocalizationContractEntry(
                id,
                key,
                false,
                SettingsLocalizationFormatKind.None,
                StaticCoverage);
        }

        private static SettingsLocalizationContractEntry Dynamic(
            SettingsLocalizationEntryId id,
            string key,
            SettingsLocalizationFormatKind formatKind = SettingsLocalizationFormatKind.None)
        {
            return new SettingsLocalizationContractEntry(
                id,
                key,
                formatKind != SettingsLocalizationFormatKind.None,
                formatKind,
                DynamicCoverage);
        }
    }

    public enum TerminalResultLocalizationEntryId
    {
        Continue,
        StageClearTitle,
        LevelFailedTitle,
        ChancesExhaustedDetail,
        RestartStage,
        MainMenu,
        GameClearTitle,
    }

    public readonly struct TerminalResultLocalizationContractEntry
    {
        public TerminalResultLocalizationContractEntry(
            TerminalResultLocalizationEntryId id,
            string key,
            string english,
            string korean,
            LocalizedTextRole role,
            LocalizedTextWeight weight)
        {
            Id = id;
            Key = key ?? string.Empty;
            English = english ?? string.Empty;
            Korean = korean ?? string.Empty;
            Role = role;
            Weight = weight;
        }

        public TerminalResultLocalizationEntryId Id { get; }

        public string Table => TerminalResultLocalizationContract.Table;

        public string Key { get; }

        public string English { get; }

        public string Korean { get; }

        public LocalizedTextRole Role { get; }

        public LocalizedTextWeight Weight { get; }

        public bool IsSmart => false;
    }

    public static class TerminalResultLocalizationContract
    {
        public const string Table = "UI";

        public static class Keys
        {
            public const string Continue = "ui.result.action.continue";
            public const string StageClearTitle = "ui.result.stage_clear.title";
            public const string LevelFailedTitle = "ui.result.level_failed.title";
            public const string ChancesExhaustedDetail =
                "ui.result.level_failed.detail.chances_exhausted";
            public const string RestartStage = "ui.result.action.restart_stage";
            public const string MainMenu = "ui.result.action.main_menu";
            public const string GameClearTitle = "ui.result.game_clear.title";
        }

        private static readonly IReadOnlyList<TerminalResultLocalizationContractEntry> ContractEntries =
            Array.AsReadOnly(new[]
            {
                Entry(
                    TerminalResultLocalizationEntryId.Continue,
                    Keys.Continue,
                    "Continue",
                    "계속",
                    LocalizedTextRole.Button,
                    LocalizedTextWeight.Regular),
                Entry(
                    TerminalResultLocalizationEntryId.StageClearTitle,
                    Keys.StageClearTitle,
                    "Stage Clear",
                    "스테이지 클리어",
                    LocalizedTextRole.Title,
                    LocalizedTextWeight.Bold),
                Entry(
                    TerminalResultLocalizationEntryId.LevelFailedTitle,
                    Keys.LevelFailedTitle,
                    "Stage Failed",
                    "스테이지 실패",
                    LocalizedTextRole.Title,
                    LocalizedTextWeight.Bold),
                Entry(
                    TerminalResultLocalizationEntryId.ChancesExhaustedDetail,
                    Keys.ChancesExhaustedDetail,
                    "All chances have been used. Restart the stage or return to the main menu.",
                    "모든 기회를 소진했습니다. 스테이지를 다시 시작하거나 메인 메뉴로 돌아가세요.",
                    LocalizedTextRole.Body,
                    LocalizedTextWeight.Regular),
                Entry(
                    TerminalResultLocalizationEntryId.RestartStage,
                    Keys.RestartStage,
                    "Restart Stage",
                    "스테이지 다시 시작",
                    LocalizedTextRole.Button,
                    LocalizedTextWeight.Regular),
                Entry(
                    TerminalResultLocalizationEntryId.MainMenu,
                    Keys.MainMenu,
                    "Main Menu",
                    "메인 메뉴",
                    LocalizedTextRole.Button,
                    LocalizedTextWeight.Regular),
                Entry(
                    TerminalResultLocalizationEntryId.GameClearTitle,
                    Keys.GameClearTitle,
                    "Game Clear",
                    "게임 클리어",
                    LocalizedTextRole.Title,
                    LocalizedTextWeight.Bold),
            });

        public static IReadOnlyList<TerminalResultLocalizationContractEntry> Entries => ContractEntries;

        private static TerminalResultLocalizationContractEntry Entry(
            TerminalResultLocalizationEntryId id,
            string key,
            string english,
            string korean,
            LocalizedTextRole role,
            LocalizedTextWeight weight)
        {
            return new TerminalResultLocalizationContractEntry(
                id,
                key,
                english,
                korean,
                role,
                weight);
        }
    }

    public enum SceneTransitionLocalizationEntryId
    {
        RemainingChances,
        Loading,
    }

    public readonly struct SceneTransitionLocalizationContractEntry
    {
        public SceneTransitionLocalizationContractEntry(
            SceneTransitionLocalizationEntryId id,
            string key,
            string english,
            string korean,
            LocalizedTextRole role,
            LocalizedTextWeight weight)
        {
            Id = id;
            Key = key ?? string.Empty;
            English = english ?? string.Empty;
            Korean = korean ?? string.Empty;
            Role = role;
            Weight = weight;
        }

        public SceneTransitionLocalizationEntryId Id { get; }

        public string Table => SceneTransitionLocalizationContract.Table;

        public string Key { get; }

        public string English { get; }

        public string Korean { get; }

        public LocalizedTextRole Role { get; }

        public LocalizedTextWeight Weight { get; }

        public bool IsSmart => false;
    }

    public static class SceneTransitionLocalizationContract
    {
        public const string Table = "UI";

        public static class Keys
        {
            public const string RemainingChances =
                "ui.transition.chance_lost.remaining_chances";
            public const string Loading = "ui.transition.loading";
        }

        private static readonly IReadOnlyList<SceneTransitionLocalizationContractEntry> ContractEntries =
            Array.AsReadOnly(new[]
            {
                new SceneTransitionLocalizationContractEntry(
                    SceneTransitionLocalizationEntryId.RemainingChances,
                    Keys.RemainingChances,
                    "Remaining Chances",
                    "재시도 기회",
                    LocalizedTextRole.Title,
                    LocalizedTextWeight.Bold),
                new SceneTransitionLocalizationContractEntry(
                    SceneTransitionLocalizationEntryId.Loading,
                    Keys.Loading,
                    "Loading...",
                    "불러오는 중...",
                    LocalizedTextRole.Label,
                    LocalizedTextWeight.Bold),
            });

        public static IReadOnlyList<SceneTransitionLocalizationContractEntry> Entries => ContractEntries;
    }

    public readonly struct LocalizedTextDescriptor : IEquatable<LocalizedTextDescriptor>
    {
        private readonly string _table;
        private readonly string _key;
        private readonly IReadOnlyList<object> _arguments;

        public LocalizedTextDescriptor(
            string table,
            string key,
            LocalizedTextRole role = LocalizedTextRole.Body,
            LocalizedTextWeight weight = LocalizedTextWeight.Default,
            IReadOnlyList<object> arguments = null)
        {
            _table = table ?? string.Empty;
            _key = key ?? string.Empty;
            _arguments = CopyArguments(arguments);
            Role = role;
            Weight = weight;
        }

        public string Table => _table ?? string.Empty;

        public string Key => _key ?? string.Empty;

        public LocalizedTextRole Role { get; }

        public LocalizedTextWeight Weight { get; }

        public IReadOnlyList<object> Arguments => _arguments ?? Array.Empty<object>();

        public bool Equals(LocalizedTextDescriptor other)
        {
            if (!string.Equals(Table, other.Table, StringComparison.Ordinal) ||
                !string.Equals(Key, other.Key, StringComparison.Ordinal) ||
                Role != other.Role ||
                Weight != other.Weight ||
                Arguments.Count != other.Arguments.Count)
            {
                return false;
            }

            for (var i = 0; i < Arguments.Count; i++)
            {
                if (!Equals(Arguments[i], other.Arguments[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizedTextDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Table, Key, Role, Weight);
            for (var i = 0; i < Arguments.Count; i++)
            {
                hash = HashCode.Combine(hash, Arguments[i]);
            }

            return hash;
        }

        public static bool operator ==(LocalizedTextDescriptor left, LocalizedTextDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LocalizedTextDescriptor left, LocalizedTextDescriptor right)
        {
            return !left.Equals(right);
        }

        private static IReadOnlyList<object> CopyArguments(IReadOnlyList<object> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return Array.Empty<object>();
            }

            var copy = new object[arguments.Count];
            for (var i = 0; i < arguments.Count; i++)
            {
                copy[i] = arguments[i];
            }

            return copy;
        }
    }

    public interface ILocalizedTextResolver
    {
        string CurrentLocaleCode { get; }

        event Action LocaleChanged;

        string Resolve(LocalizedTextDescriptor descriptor);
    }

    public readonly struct LocaleOptionModel
    {
        public LocaleOptionModel(string localeCode, LocalizedTextDescriptor displayNameDescriptor)
        {
            LocaleCode = localeCode ?? string.Empty;
            DisplayNameDescriptor = displayNameDescriptor;
        }

        public string LocaleCode { get; }

        public LocalizedTextDescriptor DisplayNameDescriptor { get; }
    }

    public interface IUiLocaleSelectionPort
    {
        string CurrentLocaleCode { get; }

        IReadOnlyList<string> AvailableLocaleCodes { get; }

        bool TrySetLocale(string localeCode);
    }

    public interface IUiLocalePreferenceStore
    {
        bool TryLoad(out string localeCode);

        void Save(string localeCode);
    }

    public sealed class PackageFreeLocalizedTextResolver : ILocalizedTextResolver, IUiLocaleSelectionPort
    {
        public const string DefaultLocaleCode = "en-US";
        public const string KoreanLocaleCode = "ko-KR";

        private static readonly IReadOnlyList<string> SupportedLocaleCodes =
            Array.AsReadOnly(new[] { DefaultLocaleCode, KoreanLocaleCode });

        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _catalog;
        private readonly IUiLocalePreferenceStore _localePreferenceStore;
        private string _currentLocaleCode;

        public PackageFreeLocalizedTextResolver(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog,
            string initialLocaleCode = DefaultLocaleCode,
            IUiLocalePreferenceStore localePreferenceStore = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _localePreferenceStore = localePreferenceStore;
            _currentLocaleCode = ResolveInitialLocaleCode(initialLocaleCode, localePreferenceStore);
        }

        public string CurrentLocaleCode => _currentLocaleCode;

        public IReadOnlyList<string> AvailableLocaleCodes => SupportedLocaleCodes;

        public event Action LocaleChanged;

        public static PackageFreeLocalizedTextResolver CreateSettingsDefault(string initialLocaleCode = DefaultLocaleCode)
        {
            return new PackageFreeLocalizedTextResolver(CreateSettingsCatalog(), initialLocaleCode);
        }

        public static PackageFreeLocalizedTextResolver CreateSettingsDefault(
            IUiLocalePreferenceStore localePreferenceStore)
        {
            return new PackageFreeLocalizedTextResolver(
                CreateSettingsCatalog(),
                DefaultLocaleCode,
                localePreferenceStore);
        }

        public string Resolve(LocalizedTextDescriptor descriptor)
        {
            if (TryResolve(_currentLocaleCode, descriptor, out var value) ||
                TryResolve(DefaultLocaleCode, descriptor, out value))
            {
                return FormatKnownDynamicText(descriptor, value);
            }

            return $"[{descriptor.Table}:{descriptor.Key}]";
        }

        public void SetLocale(string localeCode)
        {
            var normalizedLocaleCode = NormalizeLocaleCode(localeCode);
            if (string.Equals(_currentLocaleCode, normalizedLocaleCode, StringComparison.Ordinal))
            {
                return;
            }

            _currentLocaleCode = normalizedLocaleCode;
            LocaleChanged?.Invoke();
        }

        public bool TrySetLocale(string localeCode)
        {
            var normalizedLocaleCode = NormalizeLocaleCode(localeCode);
            if (!IsSupportedLocaleCode(normalizedLocaleCode))
            {
                return false;
            }

            if (string.Equals(_currentLocaleCode, normalizedLocaleCode, StringComparison.Ordinal))
            {
                return true;
            }

            SetLocale(normalizedLocaleCode);
            _localePreferenceStore?.Save(normalizedLocaleCode);
            return true;
        }

        private bool TryResolve(
            string localeCode,
            LocalizedTextDescriptor descriptor,
            out string value)
        {
            value = null;
            return (string.Equals(descriptor.Table, SettingsLocalizationContract.Table, StringComparison.Ordinal) ||
                    string.Equals(descriptor.Table, "Stage", StringComparison.Ordinal)) &&
                   _catalog.TryGetValue(localeCode, out var localeValues) &&
                   localeValues.TryGetValue(descriptor.Key, out value);
        }

        private string FormatKnownDynamicText(LocalizedTextDescriptor descriptor, string value)
        {
            if ((string.Equals(descriptor.Key, SettingsLocalizationContract.Keys.AudioVolumeValue, StringComparison.Ordinal) ||
                 string.Equals(descriptor.Key, SettingsLocalizationContract.Keys.AudioVolumeValueMuted, StringComparison.Ordinal)) &&
                TryGetPercentArgument(descriptor, out var percent))
            {
                return value
                    .Replace("{percent}", percent.ToString(CultureInfo.InvariantCulture))
                    .Replace("{0}", percent.ToString(CultureInfo.InvariantCulture));
            }

            if (string.Equals(descriptor.Key, SettingsLocalizationContract.Keys.DisplayResolutionValue, StringComparison.Ordinal) &&
                descriptor.Arguments.Count > 0)
            {
                return value.Replace("{0}", descriptor.Arguments[0]?.ToString() ?? string.Empty);
            }

            if ((string.Equals(descriptor.Key, SettingsLocalizationContract.Keys.DisplayPreviewCountdown, StringComparison.Ordinal) ||
                 string.Equals(descriptor.Key, SettingsLocalizationContract.Keys.DisplayPreviewActiveStatus, StringComparison.Ordinal)) &&
                descriptor.Arguments.Count > 0)
            {
                return value.Replace(
                    "{0}",
                    Convert.ToString(descriptor.Arguments[0], CultureInfo.InvariantCulture) ?? string.Empty);
            }

            if (string.Equals(
                    descriptor.Key,
                    SettingsLocalizationContract.Keys.InputActionConflict,
                    StringComparison.Ordinal) &&
                descriptor.Arguments.Count > 0)
            {
                return value.Replace("{0}", ResolveArgument(descriptor.Arguments[0]));
            }

            if ((string.Equals(
                     descriptor.Key,
                     SettingsLocalizationContract.Keys.DisplayPreviewConfirmFullscreenBody,
                     StringComparison.Ordinal) ||
                 string.Equals(
                     descriptor.Key,
                     SettingsLocalizationContract.Keys.DisplayPreviewConfirmWindowedBody,
                     StringComparison.Ordinal)) &&
                descriptor.Arguments.Count >= 3)
            {
                return value
                    .Replace(
                        "{0}",
                        Convert.ToString(descriptor.Arguments[0], CultureInfo.InvariantCulture) ?? string.Empty)
                    .Replace(
                        "{1}",
                        Convert.ToString(descriptor.Arguments[1], CultureInfo.InvariantCulture) ?? string.Empty)
                    .Replace(
                        "{2}",
                        Convert.ToString(descriptor.Arguments[2], CultureInfo.InvariantCulture) ?? string.Empty);
            }

            if (descriptor.Key.StartsWith("ui.main_menu.", StringComparison.Ordinal))
            {
                for (var i = 0; i < descriptor.Arguments.Count; i++)
                {
                    value = value.Replace(
                        $"{{{i}}}",
                        Convert.ToString(descriptor.Arguments[i], CultureInfo.InvariantCulture) ?? string.Empty);
                }
            }

            return value;
        }

        private string ResolveArgument(object argument)
        {
            return argument is LocalizedTextDescriptor nestedDescriptor
                ? Resolve(nestedDescriptor)
                : Convert.ToString(argument, CultureInfo.InvariantCulture) ?? string.Empty;
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

        private static string NormalizeLocaleCode(string localeCode)
        {
            return string.IsNullOrWhiteSpace(localeCode)
                ? DefaultLocaleCode
                : localeCode;
        }

        private static string ResolveInitialLocaleCode(
            string initialLocaleCode,
            IUiLocalePreferenceStore localePreferenceStore)
        {
            if (localePreferenceStore == null)
            {
                return NormalizeLocaleCode(initialLocaleCode);
            }

            if (localePreferenceStore.TryLoad(out var persistedLocaleCode) &&
                IsSupportedLocaleCode(NormalizeLocaleCode(persistedLocaleCode)))
            {
                return NormalizeLocaleCode(persistedLocaleCode);
            }

            return DefaultLocaleCode;
        }

        private static bool IsSupportedLocaleCode(string localeCode)
        {
            for (var i = 0; i < SupportedLocaleCodes.Count; i++)
            {
                if (string.Equals(SupportedLocaleCodes[i], localeCode, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CreateSettingsCatalog()
        {
            var catalog = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [DefaultLocaleCode] = new Dictionary<string, string>
                {
                    [SettingsLocalizationContract.Keys.Title] = "Settings",
                    [SettingsLocalizationContract.Keys.AudioTab] = "Audio",
                    [SettingsLocalizationContract.Keys.DisplayTab] = "Display",
                    [SettingsLocalizationContract.Keys.InputTab] = "Input",
                    [SettingsLocalizationContract.Keys.AudioMain] = "Main",
                    [SettingsLocalizationContract.Keys.AudioBgm] = "Background Music",
                    [SettingsLocalizationContract.Keys.AudioSfx] = "Effects",
                    [SettingsLocalizationContract.Keys.AudioMute] = "Mute",
                    [SettingsLocalizationContract.Keys.DisplayCurrent] = "Current Display",
                    [SettingsLocalizationContract.Keys.DisplayResolution] = "Resolution",
                    [SettingsLocalizationContract.Keys.DisplayResolutionHint] = "Only automatically detected resolutions are shown.",
                    [SettingsLocalizationContract.Keys.DisplayFullscreenWindow] = "Fullscreen Window",
                    [SettingsLocalizationContract.Keys.DisplayFullscreenOn] = "On",
                    [SettingsLocalizationContract.Keys.DisplayApply] = "Apply",
                    [SettingsLocalizationContract.Keys.DisplayRevert] = "Revert",
                    [SettingsLocalizationContract.Keys.InputMovementKeys] = "Movement Keys",
                    [SettingsLocalizationContract.Keys.InputPush] = "Push",
                    [SettingsLocalizationContract.Keys.InputFlip] = "Flip",
                    [SettingsLocalizationContract.Keys.InputReset] = "Reset Input",
                    [SettingsLocalizationContract.Keys.Language] = "Language",
                    [SettingsLocalizationContract.Keys.LanguageEnglish] = "English",
                    [SettingsLocalizationContract.Keys.LanguageKorean] = "Korean",
                    [SettingsLocalizationContract.Keys.AudioVolumeValue] = "{0}%",
                    [SettingsLocalizationContract.Keys.AudioVolumeValueMuted] = "{0}% (Muted)",
                    [SettingsLocalizationContract.Keys.DisplayResolutionValue] = "{0}",
                    [SettingsLocalizationContract.Keys.DisplayPreviewCountdown] = "Reverting in {0}s",
                    [SettingsLocalizationContract.Keys.DisplayPreviewActiveStatus] = "Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in {0} seconds.",
                    [SettingsLocalizationContract.Keys.DisplayPreviewRevertedStatus] = "Preview reverted to the previous saved display settings.",
                    [SettingsLocalizationContract.Keys.DisplaySavedStatus] = "Display settings saved.",
                    [SettingsLocalizationContract.Keys.DisplayExternalDriftStatus] = "Current display changed outside saved settings. Saved settings remain unchanged until you apply again.",
                    [SettingsLocalizationContract.Keys.InputRebindCanceled] = "Key reassignment cancelled.",
                    [SettingsLocalizationContract.Keys.InputResetComplete] = "Input settings reset.",
                    [SettingsLocalizationContract.Keys.InputReservedKey] = "This key cannot be used.",
                    [SettingsLocalizationContract.Keys.InputMovementConflict] = "Movement keys cannot overlap.",
                    [SettingsLocalizationContract.Keys.InputAlreadyRebinding] = "Another key is already being reassigned.",
                    [SettingsLocalizationContract.Keys.InputActionConflict] = "This key is already used by {0}.",
                    [SettingsLocalizationContract.Keys.InputUnsupportedKey] = "This key cannot be used.",
                    [SettingsLocalizationContract.Keys.InputRebindPushPrompt] = "Press a key for Push...",
                    [SettingsLocalizationContract.Keys.InputRebindFlipPrompt] = "Press a key for Flip...",
                    [SettingsLocalizationContract.Keys.InputResetConfirmTitle] = "Reset Input Settings",
                    [SettingsLocalizationContract.Keys.InputResetConfirmBody] = "Reset input settings to defaults?",
                    [SettingsLocalizationContract.Keys.InputResetConfirmLabel] = "Reset",
                    [SettingsLocalizationContract.Keys.Cancel] = "Cancel",
                    [SettingsLocalizationContract.Keys.DisplayPreviewConfirmTitle] = "Confirm Display Preview",
                    [SettingsLocalizationContract.Keys.DisplayPreviewConfirmFullscreenBody] =
                        "Preview {0} x {1} in Fullscreen Window. These changes are temporary and will revert in {2} seconds unless you confirm.",
                    [SettingsLocalizationContract.Keys.DisplayPreviewConfirmWindowedBody] =
                        "Preview {0} x {1} in Windowed mode. These changes are temporary and will revert in {2} seconds unless you confirm.",
                    [SettingsLocalizationContract.Keys.DisplayPreviewConfirmKeep] = "Keep",
                    [SettingsLocalizationContract.Keys.Back] = "Back",
                    ["ui.common.settings"] = "Settings",
                    ["ui.main_menu.start"] = "Start",
                    ["ui.main_menu.quit"] = "Quit",
                    ["ui.pause.title"] = "Paused",
                    ["ui.pause.resume"] = "Resume",
                    ["ui.pause.retry"] = "Retry",
                    ["ui.pause.main_menu"] = "Main Menu",
                },
                [KoreanLocaleCode] = new Dictionary<string, string>
                {
                    [SettingsLocalizationContract.Keys.Title] = "설정",
                    [SettingsLocalizationContract.Keys.AudioTab] = "오디오",
                    [SettingsLocalizationContract.Keys.DisplayTab] = "디스플레이",
                    [SettingsLocalizationContract.Keys.InputTab] = "입력",
                    [SettingsLocalizationContract.Keys.AudioMain] = "마스터",
                    [SettingsLocalizationContract.Keys.AudioBgm] = "배경 음악",
                    [SettingsLocalizationContract.Keys.AudioSfx] = "효과음",
                    [SettingsLocalizationContract.Keys.AudioMute] = "음소거",
                    [SettingsLocalizationContract.Keys.DisplayCurrent] = "현재 디스플레이",
                    [SettingsLocalizationContract.Keys.DisplayResolution] = "해상도",
                    [SettingsLocalizationContract.Keys.DisplayResolutionHint] = "자동으로 감지된 해상도만 표시됩니다.",
                    [SettingsLocalizationContract.Keys.DisplayFullscreenWindow] = "테두리 없는 전체 화면",
                    [SettingsLocalizationContract.Keys.DisplayFullscreenOn] = "켜짐",
                    [SettingsLocalizationContract.Keys.DisplayApply] = "적용",
                    [SettingsLocalizationContract.Keys.DisplayRevert] = "되돌리기",
                    [SettingsLocalizationContract.Keys.InputMovementKeys] = "이동 키",
                    [SettingsLocalizationContract.Keys.InputPush] = "밀기",
                    [SettingsLocalizationContract.Keys.InputFlip] = "뒤집기",
                    [SettingsLocalizationContract.Keys.InputReset] = "입력 초기화",
                    [SettingsLocalizationContract.Keys.Language] = "언어",
                    [SettingsLocalizationContract.Keys.LanguageEnglish] = "영어",
                    [SettingsLocalizationContract.Keys.LanguageKorean] = "한국어",
                    [SettingsLocalizationContract.Keys.AudioVolumeValue] = "{0}%",
                    [SettingsLocalizationContract.Keys.AudioVolumeValueMuted] = "{0}% (음소거)",
                    [SettingsLocalizationContract.Keys.DisplayResolutionValue] = "{0}",
                    [SettingsLocalizationContract.Keys.DisplayPreviewCountdown] = "{0}초 후 되돌림",
                    [SettingsLocalizationContract.Keys.DisplayPreviewActiveStatus] = "미리 보기 중입니다. 현재 화면 설정은 임시 상태이며 저장되지 않았습니다. 유지하려면 확인하세요. 그렇지 않으면 {0}초 후 되돌아갑니다.",
                    [SettingsLocalizationContract.Keys.DisplayPreviewRevertedStatus] = "미리 보기가 이전에 저장된 화면 설정으로 되돌아갔습니다.",
                    [SettingsLocalizationContract.Keys.DisplaySavedStatus] = "화면 설정이 저장되었습니다.",
                    [SettingsLocalizationContract.Keys.DisplayExternalDriftStatus] = "현재 화면이 저장된 설정과 다릅니다. 다시 적용하기 전까지 저장된 설정은 변경되지 않습니다.",
                    [SettingsLocalizationContract.Keys.InputRebindCanceled] = "키 재지정을 취소했습니다.",
                    [SettingsLocalizationContract.Keys.InputResetComplete] = "입력 설정이 초기화되었습니다.",
                    [SettingsLocalizationContract.Keys.InputReservedKey] = "이 키는 사용할 수 없습니다.",
                    [SettingsLocalizationContract.Keys.InputMovementConflict] = "이동 키는 서로 중복될 수 없습니다.",
                    [SettingsLocalizationContract.Keys.InputAlreadyRebinding] = "다른 키를 설정하는 중입니다.",
                    [SettingsLocalizationContract.Keys.InputActionConflict] = "이 키는 이미 {0}에 할당되어 있습니다.",
                    [SettingsLocalizationContract.Keys.InputUnsupportedKey] = "이 키는 사용할 수 없습니다.",
                    [SettingsLocalizationContract.Keys.InputRebindPushPrompt] = "밀기에 사용할 키를 누르세요...",
                    [SettingsLocalizationContract.Keys.InputRebindFlipPrompt] = "뒤집기에 사용할 키를 누르세요...",
                    [SettingsLocalizationContract.Keys.InputResetConfirmTitle] = "입력 설정 초기화",
                    [SettingsLocalizationContract.Keys.InputResetConfirmBody] = "입력 설정을 기본값으로 초기화할까요?",
                    [SettingsLocalizationContract.Keys.InputResetConfirmLabel] = "초기화",
                    [SettingsLocalizationContract.Keys.Cancel] = "취소",
                    [SettingsLocalizationContract.Keys.DisplayPreviewConfirmTitle] = "화면 설정을 유지할까요?",
                    [SettingsLocalizationContract.Keys.DisplayPreviewConfirmFullscreenBody] =
                        "{0} x {1} 해상도로 테두리 없는 전체 화면을 미리 적용했습니다. 확인하지 않으면 {2}초 후 이전 설정으로 돌아갑니다.",
                    [SettingsLocalizationContract.Keys.DisplayPreviewConfirmWindowedBody] =
                        "{0} x {1} 해상도로 창 모드를 미리 적용했습니다. 확인하지 않으면 {2}초 후 이전 설정으로 돌아갑니다.",
                    [SettingsLocalizationContract.Keys.DisplayPreviewConfirmKeep] = "유지",
                    [SettingsLocalizationContract.Keys.Back] = "뒤로",
                    ["ui.common.settings"] = "설정",
                    ["ui.main_menu.start"] = "시작",
                    ["ui.main_menu.quit"] = "종료",
                    ["ui.pause.title"] = "일시 정지",
                    ["ui.pause.resume"] = "계속하기",
                    ["ui.pause.retry"] = "다시 시도",
                    ["ui.pause.main_menu"] = "메인 메뉴",
                },
            };

            AddTerminalResultEntries(catalog);
            AddSceneTransitionEntries(catalog);
            AddMainMenuEntries(catalog);
            AddStageEntries(catalog);
            ValidateSettingsCatalog(catalog);
            ValidateTerminalResultCatalog(catalog);
            ValidateSceneTransitionCatalog(catalog);
            ValidateMainMenuCatalog(catalog);
            return catalog;
        }

        private static void AddMainMenuEntries(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog)
        {
            var english = (IDictionary<string, string>)catalog[DefaultLocaleCode];
            var korean = (IDictionary<string, string>)catalog[KoreanLocaleCode];
            foreach (var entry in MainMenuLocalizationContract.Entries)
            {
                english.Add(entry.Key, entry.English);
                korean.Add(entry.Key, entry.Korean);
            }
        }

        private static void AddStageEntries(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog)
        {
            var english = (IDictionary<string, string>)catalog[DefaultLocaleCode];
            var korean = (IDictionary<string, string>)catalog[KoreanLocaleCode];
            var stageEntries = new[]
            {
                (Key: "stage.stage-0-1.display_name", English: "Lab-01", Korean: "연구실-01"),
                (Key: "stage.stage-0-2.display_name", English: "Lab-02", Korean: "연구실-02"),
                (Key: "stage.stage-1-1.display_name", English: "Lobby-01", Korean: "로비-01"),
                (Key: "stage.stage-2-1.display_name", English: "Ward[A]-01", Korean: "병동[A]-01"),
                (Key: "stage.stage-2-2.display_name", English: "Ward[A]-02", Korean: "병동[A]-02"),
                (Key: "stage.stage-3-1.display_name", English: "Ward[B]-01", Korean: "병동[B]-01"),
                (Key: "stage.stage-3-2.display_name", English: "Ward[B]-02", Korean: "병동[B]-02"),
                (Key: "stage.stage-4-1.display_name", English: "Morgue-01", Korean: "영안실-01"),
                (Key: "stage.stage-4-2.display_name", English: "Morgue-02", Korean: "영안실-02"),
                (Key: "stage.legacy-stage-5-1.display_name", English: "Legacy 5-1", Korean: "Legacy 5-1"),
            };

            for (var i = 0; i < stageEntries.Length; i++)
            {
                english.Add(stageEntries[i].Key, stageEntries[i].English);
                korean.Add(stageEntries[i].Key, stageEntries[i].Korean);
            }
        }

        private static void AddTerminalResultEntries(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog)
        {
            var english = (IDictionary<string, string>)catalog[DefaultLocaleCode];
            var korean = (IDictionary<string, string>)catalog[KoreanLocaleCode];
            foreach (var entry in TerminalResultLocalizationContract.Entries)
            {
                english.Add(entry.Key, entry.English);
                korean.Add(entry.Key, entry.Korean);
            }
        }

        private static void AddSceneTransitionEntries(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog)
        {
            var english = (IDictionary<string, string>)catalog[DefaultLocaleCode];
            var korean = (IDictionary<string, string>)catalog[KoreanLocaleCode];
            foreach (var entry in SceneTransitionLocalizationContract.Entries)
            {
                english.Add(entry.Key, entry.English);
                korean.Add(entry.Key, entry.Korean);
            }
        }

        private static void ValidateTerminalResultCatalog(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog)
        {
            foreach (var localeCode in SupportedLocaleCodes)
            {
                if (!catalog.TryGetValue(localeCode, out var localeValues))
                {
                    throw new InvalidOperationException(
                        $"Package-free terminal-result catalog is missing locale '{localeCode}'.");
                }

                foreach (var entry in TerminalResultLocalizationContract.Entries)
                {
                    if (!localeValues.ContainsKey(entry.Key))
                    {
                        throw new InvalidOperationException(
                            $"Package-free terminal-result catalog locale '{localeCode}' " +
                            $"is missing key '{entry.Key}'.");
                    }
                }
            }
        }

        private static void ValidateSceneTransitionCatalog(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog)
        {
            foreach (var localeCode in SupportedLocaleCodes)
            {
                if (!catalog.TryGetValue(localeCode, out var localeValues))
                {
                    throw new InvalidOperationException(
                        $"Package-free scene-transition catalog is missing locale '{localeCode}'.");
                }

                foreach (var entry in SceneTransitionLocalizationContract.Entries)
                {
                    if (!localeValues.ContainsKey(entry.Key))
                    {
                        throw new InvalidOperationException(
                            $"Package-free scene-transition catalog locale '{localeCode}' " +
                            $"is missing key '{entry.Key}'.");
                    }
                }
            }
        }

        private static void ValidateMainMenuCatalog(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog)
        {
            foreach (var localeCode in SupportedLocaleCodes)
            {
                if (!catalog.TryGetValue(localeCode, out var localeValues))
                {
                    throw new InvalidOperationException(
                        $"Package-free Main Menu catalog is missing locale '{localeCode}'.");
                }

                foreach (var entry in MainMenuLocalizationContract.Entries)
                {
                    if (!localeValues.ContainsKey(entry.Key))
                    {
                        throw new InvalidOperationException(
                            $"Package-free Main Menu catalog locale '{localeCode}' " +
                            $"is missing key '{entry.Key}'.");
                    }
                }
            }
        }

        private static void ValidateSettingsCatalog(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog)
        {
            var requiredKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in SettingsLocalizationContract.Entries)
            {
                if (entry.Coverage.HasFlag(SettingsLocalizationCoverage.PackageFreeFallback))
                {
                    requiredKeys.Add(entry.Key);
                }
            }

            foreach (var localeCode in SupportedLocaleCodes)
            {
                if (!catalog.TryGetValue(localeCode, out var localeValues))
                {
                    throw new InvalidOperationException(
                        $"Package-free Settings catalog is missing locale '{localeCode}'.");
                }

                var missingKeys = new List<string>();
                foreach (var requiredKey in requiredKeys)
                {
                    if (!localeValues.ContainsKey(requiredKey))
                    {
                        missingKeys.Add(requiredKey);
                    }
                }

                var unexpectedKeys = new List<string>();
                foreach (var key in localeValues.Keys)
                {
                    if (IsManagedSettingsKey(key) && !requiredKeys.Contains(key))
                    {
                        unexpectedKeys.Add(key);
                    }
                }

                if (missingKeys.Count == 0 && unexpectedKeys.Count == 0)
                {
                    continue;
                }

                missingKeys.Sort(StringComparer.Ordinal);
                unexpectedKeys.Sort(StringComparer.Ordinal);
                throw new InvalidOperationException(
                    $"Package-free Settings catalog locale '{localeCode}' contract mismatch. " +
                    $"Missing: {FormatKeyList(missingKeys)}. " +
                    $"Unexpected: {FormatKeyList(unexpectedKeys)}.");
            }
        }

        private static bool IsManagedSettingsKey(string key)
        {
            return key != null &&
                   (key.StartsWith("ui.settings.", StringComparison.Ordinal) ||
                    string.Equals(key, SettingsLocalizationContract.Keys.Back, StringComparison.Ordinal));
        }

        private static string FormatKeyList(IReadOnlyList<string> keys)
        {
            return keys.Count == 0
                ? "<none>"
                : string.Join(", ", keys);
        }
    }
}
