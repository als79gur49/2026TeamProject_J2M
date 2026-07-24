using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Feature.UI.ViewShared
{
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
        InputUseArrowKeys,
        InputPush,
        InputFlip,
        InputChange,
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
        InputRebindPushPrompt,
        InputRebindFlipPrompt,
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
            public const string InputUseArrowKeys = "ui.settings.input.use_arrow_keys";
            public const string InputPush = "ui.settings.input.push";
            public const string InputFlip = "ui.settings.input.flip";
            public const string InputChange = "ui.settings.input.change";
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
            public const string InputRebindPushPrompt = "ui.settings.input.rebind_push_prompt";
            public const string InputRebindFlipPrompt = "ui.settings.input.rebind_flip_prompt";
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
                Static(SettingsLocalizationEntryId.InputUseArrowKeys, Keys.InputUseArrowKeys),
                Static(SettingsLocalizationEntryId.InputPush, Keys.InputPush),
                Static(SettingsLocalizationEntryId.InputFlip, Keys.InputFlip),
                Static(SettingsLocalizationEntryId.InputChange, Keys.InputChange),
                Static(SettingsLocalizationEntryId.InputReset, Keys.InputReset),
                Static(SettingsLocalizationEntryId.Language, Keys.Language),
                Static(SettingsLocalizationEntryId.LanguageEnglish, Keys.LanguageEnglish),
                Static(SettingsLocalizationEntryId.LanguageKorean, Keys.LanguageKorean),
                Static(SettingsLocalizationEntryId.Back, Keys.Back),
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
                Dynamic(SettingsLocalizationEntryId.InputRebindPushPrompt, Keys.InputRebindPushPrompt),
                Dynamic(SettingsLocalizationEntryId.InputRebindFlipPrompt, Keys.InputRebindFlipPrompt),
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
            return string.Equals(descriptor.Table, SettingsLocalizationContract.Table, StringComparison.Ordinal) &&
                   _catalog.TryGetValue(localeCode, out var localeValues) &&
                   localeValues.TryGetValue(descriptor.Key, out value);
        }

        private static string FormatKnownDynamicText(LocalizedTextDescriptor descriptor, string value)
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
                    [SettingsLocalizationContract.Keys.InputUseArrowKeys] = "Use Arrow Keys",
                    [SettingsLocalizationContract.Keys.InputPush] = "Push",
                    [SettingsLocalizationContract.Keys.InputFlip] = "Flip",
                    [SettingsLocalizationContract.Keys.InputChange] = "Change",
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
                    [SettingsLocalizationContract.Keys.InputRebindCanceled] = "Rebind canceled.",
                    [SettingsLocalizationContract.Keys.InputResetComplete] = "Input settings reset.",
                    [SettingsLocalizationContract.Keys.InputReservedKey] = "This key is reserved.",
                    [SettingsLocalizationContract.Keys.InputMovementConflict] = "This key conflicts with movement keys.",
                    [SettingsLocalizationContract.Keys.InputAlreadyRebinding] = "Rebind already in progress.",
                    [SettingsLocalizationContract.Keys.InputRebindPushPrompt] = "Press a key for Push...",
                    [SettingsLocalizationContract.Keys.InputRebindFlipPrompt] = "Press a key for Flip...",
                    [SettingsLocalizationContract.Keys.Back] = "Back",
                    ["ui.common.settings"] = "Settings",
                    ["ui.main_menu.start"] = "Start",
                    ["ui.main_menu.quit"] = "Quit",
                    ["ui.pause.title"] = "Paused",
                    ["ui.pause.description"] = "Pausing modal popup",
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
                    [SettingsLocalizationContract.Keys.DisplayFullscreenWindow] = "전체 화면 창",
                    [SettingsLocalizationContract.Keys.DisplayFullscreenOn] = "켜짐",
                    [SettingsLocalizationContract.Keys.DisplayApply] = "적용",
                    [SettingsLocalizationContract.Keys.DisplayRevert] = "되돌리기",
                    [SettingsLocalizationContract.Keys.InputMovementKeys] = "이동 키",
                    [SettingsLocalizationContract.Keys.InputUseArrowKeys] = "화살표 키 사용",
                    [SettingsLocalizationContract.Keys.InputPush] = "밀기",
                    [SettingsLocalizationContract.Keys.InputFlip] = "뒤집기",
                    [SettingsLocalizationContract.Keys.InputChange] = "변경",
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
                    [SettingsLocalizationContract.Keys.InputRebindCanceled] = "키 변경 취소됨",
                    [SettingsLocalizationContract.Keys.InputResetComplete] = "입력 설정이 초기화되었습니다.",
                    [SettingsLocalizationContract.Keys.InputReservedKey] = "이 키는 예약되어 있습니다.",
                    [SettingsLocalizationContract.Keys.InputMovementConflict] = "이 키는 이동 키와 충돌합니다.",
                    [SettingsLocalizationContract.Keys.InputAlreadyRebinding] = "키 변경이 이미 진행 중입니다.",
                    [SettingsLocalizationContract.Keys.InputRebindPushPrompt] = "밀기 동작에 사용할 키를 누르세요...",
                    [SettingsLocalizationContract.Keys.InputRebindFlipPrompt] = "뒤집기 동작에 사용할 키를 누르세요...",
                    [SettingsLocalizationContract.Keys.Back] = "뒤로",
                    ["ui.common.settings"] = "설정",
                    ["ui.main_menu.start"] = "시작",
                    ["ui.main_menu.quit"] = "종료",
                    ["ui.pause.title"] = "일시 정지",
                    ["ui.pause.description"] = "일시 정지 팝업",
                    ["ui.pause.resume"] = "계속하기",
                    ["ui.pause.retry"] = "다시 시도",
                    ["ui.pause.main_menu"] = "메인 메뉴",
                },
            };

            ValidateSettingsCatalog(catalog);
            return catalog;
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
