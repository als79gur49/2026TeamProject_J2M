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
            return string.Equals(descriptor.Table, "UI", StringComparison.Ordinal) &&
                   _catalog.TryGetValue(localeCode, out var localeValues) &&
                   localeValues.TryGetValue(descriptor.Key, out value);
        }

        private static string FormatKnownDynamicText(LocalizedTextDescriptor descriptor, string value)
        {
            if ((string.Equals(descriptor.Key, "ui.settings.audio.volume_value", StringComparison.Ordinal) ||
                 string.Equals(descriptor.Key, "ui.settings.audio.volume_value_muted", StringComparison.Ordinal)) &&
                TryGetPercentArgument(descriptor, out var percent))
            {
                return value
                    .Replace("{percent}", percent.ToString(CultureInfo.InvariantCulture))
                    .Replace("{0}", percent.ToString(CultureInfo.InvariantCulture));
            }

            if (string.Equals(descriptor.Key, "ui.settings.display.resolution_value", StringComparison.Ordinal) &&
                descriptor.Arguments.Count > 0)
            {
                return value.Replace("{0}", descriptor.Arguments[0]?.ToString() ?? string.Empty);
            }

            if (string.Equals(descriptor.Key, "ui.settings.display.preview_countdown", StringComparison.Ordinal) &&
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
            return new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [DefaultLocaleCode] = new Dictionary<string, string>
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
                    ["ui.settings.audio.volume_value"] = "{0}%",
                    ["ui.settings.audio.volume_value_muted"] = "{0}% (Muted)",
                    ["ui.settings.display.resolution_value"] = "{0}",
                    ["ui.settings.display.preview_countdown"] = "Reverting in {0}s",
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
                },
                [KoreanLocaleCode] = new Dictionary<string, string>
                {
                    ["ui.settings.title"] = "설정",
                    ["ui.settings.audio"] = "오디오",
                    ["ui.settings.display"] = "디스플레이",
                    ["ui.settings.input"] = "입력",
                    ["ui.settings.input.movement_keys"] = "이동 키",
                    ["ui.settings.input.use_arrow_keys"] = "화살표 키 사용",
                    ["ui.settings.input.push"] = "밀기",
                    ["ui.settings.input.flip"] = "뒤집기",
                    ["ui.settings.input.change"] = "변경",
                    ["ui.settings.input.reset_input"] = "입력 초기화",
                    ["ui.settings.language"] = "언어",
                    ["ui.settings.language.english"] = "영어",
                    ["ui.settings.language.korean"] = "한국어",
                    ["ui.settings.audio.volume_value"] = "{0}%",
                    ["ui.settings.audio.volume_value_muted"] = "{0}% (음소거)",
                    ["ui.settings.display.resolution_value"] = "{0}",
                    ["ui.settings.display.preview_countdown"] = "{0}초 후 되돌림",
                    ["ui.settings.input.rebind_canceled"] = "키 변경 취소됨",
                    ["ui.settings.input.reset_complete"] = "입력 설정이 초기화되었습니다.",
                    ["ui.common.back"] = "뒤로",
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
        }
    }
}
