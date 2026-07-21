using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public enum TypographyStyleTag
    {
        Default,
        HeaderLarge,
        HeaderMedium,
        HeaderSmall,
        Body,
        BodySmall,
        Button,
        Label,
        Value,
        Status,
        Tooltip,
        SettingsDisplay,
        SettingsLabel,
        SettingsBody,
        SettingsAction,
        SettingsStatus,
    }

    public enum FontCategory
    {
        Display,
        Heading,
        Body,
        UI,
        Utility,
        Symbol,
    }

    public enum TypographySizingSource
    {
        Authored,
        Theme,
        Hybrid,
    }

    public enum TypographySizingMode
    {
        PreserveAuthored,
        Fixed,
        AutoSizeRange,
    }

    [Flags]
    public enum TypographyApplyMask
    {
        None = 0,
        Font = 1 << 0,
        Material = 1 << 1,
        FontStyle = 1 << 2,
        Sizing = 1 << 3,
        LineSpacing = 1 << 4,
        CharacterSpacing = 1 << 5,
    }

    public enum TypographyWeightStrategy
    {
        UseFontAsset,
        UseSyntheticBold,
        UseMaterialPreset,
    }

    [Serializable]
    public sealed class TypographyStyleRule
    {
        public TypographyStyleTag StyleTag;
        public FontCategory FontCategory;
        public LocalizedTextWeight Weight;
        public TypographySizingSource SizingSource;
        public TypographySizingMode SizingMode;
        public float FixedSize;
        public float MinSize;
        public float MaxSize;
        public float LineSpacing;
        public float CharacterSpacing;
        public FontStyles FontStyle;
        public TypographyApplyMask ApplyMask;

        public TypographyStyleRule()
        {
        }

        public TypographyStyleRule(
            TypographyStyleTag styleTag,
            FontCategory fontCategory,
            LocalizedTextWeight weight,
            TypographySizingSource sizingSource,
            TypographySizingMode sizingMode,
            float fixedSize,
            float minSize,
            float maxSize,
            float lineSpacing,
            float characterSpacing,
            FontStyles fontStyle,
            TypographyApplyMask applyMask)
        {
            StyleTag = styleTag;
            FontCategory = fontCategory;
            Weight = weight;
            SizingSource = sizingSource;
            SizingMode = sizingMode;
            FixedSize = fixedSize;
            MinSize = minSize;
            MaxSize = maxSize;
            LineSpacing = lineSpacing;
            CharacterSpacing = characterSpacing;
            FontStyle = fontStyle;
            ApplyMask = applyMask;
        }

        public TypographyStyleRule(TypographyStyleRule source)
            : this(
                source != null ? source.StyleTag : TypographyStyleTag.Default,
                source != null ? source.FontCategory : FontCategory.Body,
                source != null ? source.Weight : LocalizedTextWeight.Regular,
                source != null ? source.SizingSource : TypographySizingSource.Hybrid,
                source != null ? source.SizingMode : TypographySizingMode.PreserveAuthored,
                source != null ? source.FixedSize : 0f,
                source != null ? source.MinSize : 0f,
                source != null ? source.MaxSize : 0f,
                source != null ? source.LineSpacing : 0f,
                source != null ? source.CharacterSpacing : 0f,
                source != null ? source.FontStyle : FontStyles.Normal,
                source != null ? source.ApplyMask : TypographyApplyMask.None)
        {
        }
    }

    [Serializable]
    public sealed class LocaleFontEntry
    {
        public FontCategory FontCategory;
        public LocalizedTextWeight Weight;
        public TMP_FontAsset FontAsset;
        public Material MaterialPreset;
        public TypographyWeightStrategy WeightStrategy;

        public LocaleFontEntry()
        {
        }

        public LocaleFontEntry(
            FontCategory fontCategory,
            LocalizedTextWeight weight,
            TMP_FontAsset fontAsset,
            Material materialPreset,
            TypographyWeightStrategy weightStrategy)
        {
            FontCategory = fontCategory;
            Weight = weight;
            FontAsset = fontAsset;
            MaterialPreset = materialPreset;
            WeightStrategy = weightStrategy;
        }

        public LocaleFontEntry(LocaleFontEntry source)
            : this(
                source != null ? source.FontCategory : FontCategory.Body,
                source != null ? source.Weight : LocalizedTextWeight.Regular,
                source != null ? source.FontAsset : null,
                source != null ? source.MaterialPreset : null,
                source != null ? source.WeightStrategy : TypographyWeightStrategy.UseFontAsset)
        {
        }
    }

    [Serializable]
    public sealed class LocaleFontSet
    {
        public string LocaleCode = string.Empty;
        public List<LocaleFontEntry> Entries = new List<LocaleFontEntry>();

        public LocaleFontSet()
        {
        }

        public LocaleFontSet(string localeCode, IEnumerable<LocaleFontEntry> entries)
        {
            LocaleCode = localeCode ?? string.Empty;
            Entries = entries != null
                ? entries.Select(entry => new LocaleFontEntry(entry)).ToList()
                : new List<LocaleFontEntry>();
        }

        public LocaleFontSet(LocaleFontSet source)
            : this(source != null ? source.LocaleCode : string.Empty, source != null ? source.Entries : null)
        {
        }
    }

    [Serializable]
    public sealed class LocaleTypographyStyleRuleOverride
    {
        public string LocaleCode = string.Empty;
        public TypographyStyleRule Rule = new TypographyStyleRule();

        public LocaleTypographyStyleRuleOverride()
        {
        }

        public LocaleTypographyStyleRuleOverride(string localeCode, TypographyStyleRule rule)
        {
            LocaleCode = localeCode ?? string.Empty;
            Rule = new TypographyStyleRule(rule);
        }

        public LocaleTypographyStyleRuleOverride(LocaleTypographyStyleRuleOverride source)
            : this(source != null ? source.LocaleCode : string.Empty, source != null ? source.Rule : null)
        {
        }
    }

    public readonly struct ResolvedTmpTypographyStyle
    {
        public readonly TMP_FontAsset FontAsset;
        public readonly Material MaterialPreset;
        public readonly FontStyles FontStyle;
        public readonly TypographySizingSource SizingSource;
        public readonly TypographySizingMode SizingMode;
        public readonly float FixedSize;
        public readonly float MinSize;
        public readonly float MaxSize;
        public readonly float LineSpacing;
        public readonly float CharacterSpacing;
        public readonly TypographyApplyMask ApplyMask;
        public readonly TypographyWeightStrategy WeightStrategy;

        public ResolvedTmpTypographyStyle(
            TMP_FontAsset fontAsset,
            Material materialPreset,
            FontStyles fontStyle,
            TypographySizingSource sizingSource,
            TypographySizingMode sizingMode,
            float fixedSize,
            float minSize,
            float maxSize,
            float lineSpacing,
            float characterSpacing,
            TypographyApplyMask applyMask,
            TypographyWeightStrategy weightStrategy)
        {
            FontAsset = fontAsset;
            MaterialPreset = materialPreset;
            FontStyle = fontStyle;
            SizingSource = sizingSource;
            SizingMode = sizingMode;
            FixedSize = fixedSize;
            MinSize = minSize;
            MaxSize = maxSize;
            LineSpacing = lineSpacing;
            CharacterSpacing = characterSpacing;
            ApplyMask = applyMask;
            WeightStrategy = weightStrategy;
        }
    }

    public sealed class ResolvedTypographyStyleCache
    {
        private readonly Dictionary<ResolvedTypographyStyleKey, ResolvedTmpTypographyStyle> styles;

        public ResolvedTypographyStyleCache(
            IReadOnlyDictionary<ResolvedTypographyStyleKey, ResolvedTmpTypographyStyle> resolvedStyles)
        {
            styles = resolvedStyles != null
                ? new Dictionary<ResolvedTypographyStyleKey, ResolvedTmpTypographyStyle>(resolvedStyles)
                : new Dictionary<ResolvedTypographyStyleKey, ResolvedTmpTypographyStyle>();
        }

        public int Count => styles.Count;

        public bool TryGet(string localeCode, TypographyStyleTag styleTag, out ResolvedTmpTypographyStyle style)
        {
            return styles.TryGetValue(new ResolvedTypographyStyleKey(localeCode, styleTag), out style);
        }
    }

    public readonly struct ResolvedTypographyStyleKey : IEquatable<ResolvedTypographyStyleKey>
    {
        private readonly string localeCode;
        private readonly TypographyStyleTag styleTag;

        public ResolvedTypographyStyleKey(string localeCode, TypographyStyleTag styleTag)
        {
            this.localeCode = NormalizeLocaleCode(localeCode);
            this.styleTag = styleTag;
        }

        public bool Equals(ResolvedTypographyStyleKey other)
        {
            return string.Equals(localeCode, other.localeCode, StringComparison.Ordinal) &&
                   styleTag == other.styleTag;
        }

        public override bool Equals(object obj)
        {
            return obj is ResolvedTypographyStyleKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(localeCode), styleTag);
        }

        private static string NormalizeLocaleCode(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [CreateAssetMenu(
        fileName = "GameplayUiTypographyTheme",
        menuName = "Game/UI/Gameplay UI Typography Theme")]
    public sealed class GameplayUiTypographyTheme : ScriptableObject
    {
        [SerializeField] private List<string> requiredLocaleCodes = new List<string> { "en-US", "ko-KR" };
        [SerializeField] private List<TypographyStyleRule> baseRules = new List<TypographyStyleRule>();
        [SerializeField] private List<LocaleFontSet> localeFontSets = new List<LocaleFontSet>();
        [SerializeField] private List<LocaleTypographyStyleRuleOverride> sparseOverrides =
            new List<LocaleTypographyStyleRuleOverride>();

        [NonSerialized] private ResolvedTypographyStyleCache cache;

        public IReadOnlyList<string> RequiredLocaleCodes => requiredLocaleCodes;

        public IReadOnlyList<TypographyStyleRule> BaseRules => baseRules;

        public IReadOnlyList<LocaleFontSet> LocaleFontSets => localeFontSets;

        public IReadOnlyList<LocaleTypographyStyleRuleOverride> SparseOverrides => sparseOverrides;

        public ResolvedTypographyStyleCache BuildCache()
        {
            cache = TypographyThemeResolver.BuildCache(
                requiredLocaleCodes,
                baseRules,
                localeFontSets,
                sparseOverrides);
            return cache;
        }

        public bool TryResolve(string localeCode, TypographyStyleTag styleTag, out ResolvedTmpTypographyStyle style)
        {
            cache ??= BuildCache();
            return cache.TryGet(localeCode, styleTag, out style);
        }

        public ResolvedTmpTypographyStyle ResolveOrThrow(string localeCode, TypographyStyleTag styleTag)
        {
            if (TryResolve(localeCode, styleTag, out var style))
            {
                return style;
            }

            throw new InvalidOperationException(
                $"Typography style '{styleTag}' is not resolved for locale '{localeCode}'.");
        }

        public void SetRequiredLocaleCodes(IEnumerable<string> localeCodes)
        {
            requiredLocaleCodes = localeCodes != null
                ? localeCodes.Select(localeCode => localeCode ?? string.Empty).ToList()
                : new List<string>();
            InvalidateCache();
        }

        public void SetBaseRules(IEnumerable<TypographyStyleRule> rules)
        {
            baseRules = rules != null
                ? rules.Select(rule => new TypographyStyleRule(rule)).ToList()
                : new List<TypographyStyleRule>();
            InvalidateCache();
        }

        public void SetLocaleFontSets(IEnumerable<LocaleFontSet> fontSets)
        {
            localeFontSets = fontSets != null
                ? fontSets.Select(fontSet => new LocaleFontSet(fontSet)).ToList()
                : new List<LocaleFontSet>();
            InvalidateCache();
        }

        public void SetSparseOverrides(IEnumerable<LocaleTypographyStyleRuleOverride> overrides)
        {
            sparseOverrides = overrides != null
                ? overrides.Select(styleOverride => new LocaleTypographyStyleRuleOverride(styleOverride)).ToList()
                : new List<LocaleTypographyStyleRuleOverride>();
            InvalidateCache();
        }

        public void ResetToDefaultBaseRules()
        {
            SetBaseRules(CreateDefaultBaseRules());
        }

        public static IReadOnlyList<TypographyStyleRule> CreateDefaultBaseRules()
        {
            return new[]
            {
                CreateRule(
                    TypographyStyleTag.Default,
                    FontCategory.Body,
                    LocalizedTextWeight.Regular,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material),
                CreateRule(
                    TypographyStyleTag.HeaderLarge,
                    FontCategory.Display,
                    LocalizedTextWeight.Bold,
                    FontStyles.Bold,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
                CreateRule(
                    TypographyStyleTag.HeaderMedium,
                    FontCategory.Heading,
                    LocalizedTextWeight.Bold,
                    FontStyles.Bold,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
                CreateRule(
                    TypographyStyleTag.HeaderSmall,
                    FontCategory.Heading,
                    LocalizedTextWeight.Bold,
                    FontStyles.Bold,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
                CreateRule(
                    TypographyStyleTag.Body,
                    FontCategory.Body,
                    LocalizedTextWeight.Regular,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material),
                CreateRule(
                    TypographyStyleTag.BodySmall,
                    FontCategory.Body,
                    LocalizedTextWeight.Regular,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material),
                CreateRule(
                    TypographyStyleTag.Button,
                    FontCategory.UI,
                    LocalizedTextWeight.Bold,
                    FontStyles.Bold,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
                CreateRule(
                    TypographyStyleTag.Label,
                    FontCategory.UI,
                    LocalizedTextWeight.Regular,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material),
                CreateRule(
                    TypographyStyleTag.Value,
                    FontCategory.UI,
                    LocalizedTextWeight.Regular,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material),
                CreateRule(
                    TypographyStyleTag.Status,
                    FontCategory.UI,
                    LocalizedTextWeight.Regular,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material),
                CreateRule(
                    TypographyStyleTag.Tooltip,
                    FontCategory.Body,
                    LocalizedTextWeight.Regular,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material),
                CreateRule(
                    TypographyStyleTag.SettingsDisplay,
                    FontCategory.Display,
                    LocalizedTextWeight.Bold,
                    FontStyles.UpperCase,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
                CreateRule(
                    TypographyStyleTag.SettingsLabel,
                    FontCategory.Heading,
                    LocalizedTextWeight.Bold,
                    FontStyles.UpperCase,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
                CreateRule(
                    TypographyStyleTag.SettingsBody,
                    FontCategory.Utility,
                    LocalizedTextWeight.Regular,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
                CreateRule(
                    TypographyStyleTag.SettingsAction,
                    FontCategory.UI,
                    LocalizedTextWeight.Bold,
                    FontStyles.Normal,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
                CreateRule(
                    TypographyStyleTag.SettingsStatus,
                    FontCategory.UI,
                    LocalizedTextWeight.Regular,
                    FontStyles.UpperCase,
                    TypographyApplyMask.Font | TypographyApplyMask.Material | TypographyApplyMask.FontStyle),
            };
        }

        private void InvalidateCache()
        {
            cache = null;
        }

        private static TypographyStyleRule CreateRule(
            TypographyStyleTag styleTag,
            FontCategory fontCategory,
            LocalizedTextWeight weight,
            FontStyles fontStyle,
            TypographyApplyMask applyMask)
        {
            return new TypographyStyleRule(
                styleTag,
                fontCategory,
                weight,
                TypographySizingSource.Hybrid,
                TypographySizingMode.PreserveAuthored,
                0f,
                0f,
                0f,
                0f,
                0f,
                fontStyle,
                applyMask);
        }
    }

    public sealed class TypographyThemeResolver
    {
        private readonly GameplayUiTypographyTheme theme;

        public TypographyThemeResolver(GameplayUiTypographyTheme theme)
        {
            this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        public bool TryResolve(string localeCode, TypographyStyleTag styleTag, out ResolvedTmpTypographyStyle style)
        {
            return theme.TryResolve(localeCode, styleTag, out style);
        }

        public ResolvedTmpTypographyStyle ResolveOrThrow(string localeCode, TypographyStyleTag styleTag)
        {
            return theme.ResolveOrThrow(localeCode, styleTag);
        }

        public static ResolvedTypographyStyleCache BuildCache(
            IReadOnlyList<string> requiredLocaleCodes,
            IReadOnlyList<TypographyStyleRule> baseRules,
            IReadOnlyList<LocaleFontSet> localeFontSets,
            IReadOnlyList<LocaleTypographyStyleRuleOverride> sparseOverrides)
        {
            if (!TryBuildCache(
                    requiredLocaleCodes,
                    baseRules,
                    localeFontSets,
                    sparseOverrides,
                    out var cache,
                    out var errors))
            {
                throw new InvalidOperationException(
                    "Typography theme validation failed:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
            }

            return cache;
        }

        public static bool TryBuildCache(
            IReadOnlyList<string> requiredLocaleCodes,
            IReadOnlyList<TypographyStyleRule> baseRules,
            IReadOnlyList<LocaleFontSet> localeFontSets,
            IReadOnlyList<LocaleTypographyStyleRuleOverride> sparseOverrides,
            out ResolvedTypographyStyleCache cache,
            out IReadOnlyList<string> errors)
        {
            var validationErrors = new List<string>();
            var requiredLocales = BuildRequiredLocaleList(requiredLocaleCodes, validationErrors);
            var baseRuleMap = BuildBaseRuleMap(baseRules, validationErrors);
            var overrideMap = BuildOverrideMap(sparseOverrides, validationErrors);
            var localeFontSetMap = BuildLocaleFontSetMap(localeFontSets, validationErrors);

            foreach (var styleTag in GetRequiredStyleTags())
            {
                if (!baseRuleMap.ContainsKey(styleTag))
                {
                    validationErrors.Add($"Missing base typography rule for {styleTag}.");
                }
            }

            foreach (var requiredLocale in requiredLocales)
            {
                if (!localeFontSetMap.ContainsKey(requiredLocale))
                {
                    validationErrors.Add($"Missing locale font set for '{requiredLocale}'.");
                }
            }

            if (validationErrors.Count > 0)
            {
                cache = new ResolvedTypographyStyleCache(null);
                errors = validationErrors;
                return false;
            }

            var resolvedStyles = new Dictionary<ResolvedTypographyStyleKey, ResolvedTmpTypographyStyle>();

            foreach (var localeCode in requiredLocales)
            {
                var fontEntryMap = BuildFontEntryMap(localeFontSetMap[localeCode], validationErrors);
                foreach (var styleTag in GetRequiredStyleTags())
                {
                    var rule = overrideMap.TryGetValue(new ResolvedTypographyStyleKey(localeCode, styleTag), out var overrideRule)
                        ? overrideRule
                        : baseRuleMap[styleTag];
                    var fontKey = new FontEntryKey(rule.FontCategory, NormalizeWeight(rule.Weight));

                    if (!fontEntryMap.TryGetValue(fontKey, out var fontEntry))
                    {
                        validationErrors.Add(
                            $"Missing font entry for locale '{localeCode}', category '{rule.FontCategory}', weight '{NormalizeWeight(rule.Weight)}' required by style '{styleTag}'.");
                        continue;
                    }

                    if (fontEntry.FontAsset == null)
                    {
                        validationErrors.Add(
                            $"Missing TMP font asset for locale '{localeCode}', category '{rule.FontCategory}', weight '{NormalizeWeight(rule.Weight)}'.");
                    }

                    if (fontEntry.MaterialPreset == null)
                    {
                        validationErrors.Add(
                            $"Missing material preset for locale '{localeCode}', category '{rule.FontCategory}', weight '{NormalizeWeight(rule.Weight)}'.");
                    }

                    if (fontEntry.FontAsset == null || fontEntry.MaterialPreset == null)
                    {
                        continue;
                    }

                    resolvedStyles[new ResolvedTypographyStyleKey(localeCode, styleTag)] =
                        new ResolvedTmpTypographyStyle(
                            fontEntry.FontAsset,
                            fontEntry.MaterialPreset,
                            rule.FontStyle,
                            rule.SizingSource,
                            rule.SizingMode,
                            rule.FixedSize,
                            rule.MinSize,
                            rule.MaxSize,
                            rule.LineSpacing,
                            rule.CharacterSpacing,
                            rule.ApplyMask,
                            fontEntry.WeightStrategy);
                }
            }

            cache = new ResolvedTypographyStyleCache(resolvedStyles);
            errors = validationErrors;
            return validationErrors.Count == 0;
        }

        private static IReadOnlyList<TypographyStyleTag> GetRequiredStyleTags()
        {
            return Enum.GetValues(typeof(TypographyStyleTag)).Cast<TypographyStyleTag>().ToArray();
        }

        private static IReadOnlyList<string> BuildRequiredLocaleList(
            IReadOnlyList<string> requiredLocaleCodes,
            ICollection<string> errors)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (requiredLocaleCodes == null || requiredLocaleCodes.Count == 0)
            {
                errors.Add("At least one required locale code must be configured.");
                return result;
            }

            foreach (var localeCode in requiredLocaleCodes)
            {
                var normalized = NormalizeLocaleCode(localeCode);
                if (string.IsNullOrEmpty(normalized))
                {
                    errors.Add("Required locale code cannot be empty.");
                    continue;
                }

                if (!seen.Add(normalized))
                {
                    errors.Add($"Duplicate required locale code '{normalized}'.");
                    continue;
                }

                result.Add(normalized);
            }

            return result;
        }

        private static Dictionary<TypographyStyleTag, TypographyStyleRule> BuildBaseRuleMap(
            IReadOnlyList<TypographyStyleRule> rules,
            ICollection<string> errors)
        {
            var result = new Dictionary<TypographyStyleTag, TypographyStyleRule>();

            if (rules == null)
            {
                return result;
            }

            foreach (var rule in rules)
            {
                if (rule == null)
                {
                    errors.Add("Base typography rule cannot be null.");
                    continue;
                }

                if (result.ContainsKey(rule.StyleTag))
                {
                    errors.Add($"Duplicate base typography rule for {rule.StyleTag}.");
                    continue;
                }

                result.Add(rule.StyleTag, new TypographyStyleRule(rule));
            }

            return result;
        }

        private static Dictionary<ResolvedTypographyStyleKey, TypographyStyleRule> BuildOverrideMap(
            IReadOnlyList<LocaleTypographyStyleRuleOverride> overrides,
            ICollection<string> errors)
        {
            var result = new Dictionary<ResolvedTypographyStyleKey, TypographyStyleRule>();

            if (overrides == null)
            {
                return result;
            }

            foreach (var styleOverride in overrides)
            {
                if (styleOverride == null)
                {
                    errors.Add("Typography style override cannot be null.");
                    continue;
                }

                var localeCode = NormalizeLocaleCode(styleOverride.LocaleCode);
                if (string.IsNullOrEmpty(localeCode))
                {
                    errors.Add("Typography style override locale code cannot be empty.");
                    continue;
                }

                if (styleOverride.Rule == null)
                {
                    errors.Add($"Typography style override for locale '{localeCode}' cannot have a null rule.");
                    continue;
                }

                var key = new ResolvedTypographyStyleKey(localeCode, styleOverride.Rule.StyleTag);
                if (result.ContainsKey(key))
                {
                    errors.Add(
                        $"Duplicate typography style override for locale '{localeCode}', style '{styleOverride.Rule.StyleTag}'.");
                    continue;
                }

                result.Add(key, new TypographyStyleRule(styleOverride.Rule));
            }

            return result;
        }

        private static Dictionary<string, LocaleFontSet> BuildLocaleFontSetMap(
            IReadOnlyList<LocaleFontSet> fontSets,
            ICollection<string> errors)
        {
            var result = new Dictionary<string, LocaleFontSet>(StringComparer.Ordinal);

            if (fontSets == null)
            {
                return result;
            }

            foreach (var fontSet in fontSets)
            {
                if (fontSet == null)
                {
                    errors.Add("Locale font set cannot be null.");
                    continue;
                }

                var localeCode = NormalizeLocaleCode(fontSet.LocaleCode);
                if (string.IsNullOrEmpty(localeCode))
                {
                    errors.Add("Locale font set locale code cannot be empty.");
                    continue;
                }

                if (result.ContainsKey(localeCode))
                {
                    errors.Add($"Duplicate locale font set for '{localeCode}'.");
                    continue;
                }

                result.Add(localeCode, new LocaleFontSet(fontSet));
            }

            return result;
        }

        private static Dictionary<FontEntryKey, LocaleFontEntry> BuildFontEntryMap(
            LocaleFontSet fontSet,
            ICollection<string> errors)
        {
            var result = new Dictionary<FontEntryKey, LocaleFontEntry>();

            foreach (var entry in fontSet.Entries ?? new List<LocaleFontEntry>())
            {
                if (entry == null)
                {
                    errors.Add($"Locale font set '{fontSet.LocaleCode}' contains a null entry.");
                    continue;
                }

                var key = new FontEntryKey(entry.FontCategory, NormalizeWeight(entry.Weight));
                if (result.ContainsKey(key))
                {
                    errors.Add(
                        $"Duplicate font entry for locale '{fontSet.LocaleCode}', category '{entry.FontCategory}', weight '{NormalizeWeight(entry.Weight)}'.");
                    continue;
                }

                result.Add(key, new LocaleFontEntry(entry));
            }

            return result;
        }

        private static LocalizedTextWeight NormalizeWeight(LocalizedTextWeight weight)
        {
            return weight == LocalizedTextWeight.Default ? LocalizedTextWeight.Regular : weight;
        }

        private static string NormalizeLocaleCode(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private readonly struct FontEntryKey : IEquatable<FontEntryKey>
        {
            private readonly FontCategory fontCategory;
            private readonly LocalizedTextWeight weight;

            public FontEntryKey(FontCategory fontCategory, LocalizedTextWeight weight)
            {
                this.fontCategory = fontCategory;
                this.weight = weight;
            }

            public bool Equals(FontEntryKey other)
            {
                return fontCategory == other.fontCategory && weight == other.weight;
            }

            public override bool Equals(object obj)
            {
                return obj is FontEntryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(fontCategory, weight);
            }
        }
    }
}
