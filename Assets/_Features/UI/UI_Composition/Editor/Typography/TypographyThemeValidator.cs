using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Game.Feature.UI.Composition.Editor
{
    public static class TypographyThemeValidator
    {
        public const string ThemeAssetPath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";

        public static readonly string[] RequiredLocaleCodes = { "en-US", "ko-KR" };

        public static readonly TypographyStyleTag[] RequiredStyleTags =
            Enum.GetValues(typeof(TypographyStyleTag)).Cast<TypographyStyleTag>().ToArray();

        public static GameplayUiTypographyTheme FindThemeAsset()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemeAssetPath);
            if (theme != null)
            {
                return theme;
            }

            var guids = AssetDatabase.FindAssets("t:GameplayUiTypographyTheme");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(path);
                if (theme != null)
                {
                    return theme;
                }
            }

            return null;
        }

        public static TypographyValidationReport ValidateProductionTheme()
        {
            return ValidateTheme(FindThemeAsset(), ThemeAssetPath);
        }

        public static TypographyValidationReport ValidateTheme(
            GameplayUiTypographyTheme theme,
            string targetName = null)
        {
            var report = new TypographyValidationReport("Typography Theme Validation");
            var target = string.IsNullOrWhiteSpace(targetName) ? "GameplayUiTypographyTheme" : targetName;

            if (theme == null)
            {
                report.AddError(target, "GameplayUiTypographyTheme asset was not found.");
                return report;
            }

            ValidateRequiredLocaleCodes(theme, target, report);

            var cacheIsValid = TypographyThemeResolver.TryBuildCache(
                    theme.RequiredLocaleCodes,
                    theme.BaseRules,
                    theme.LocaleFontSets,
                    theme.SparseOverrides,
                    out var cache,
                    out var resolverErrors);
            if (!cacheIsValid)
            {
                foreach (var error in resolverErrors)
                {
                    report.AddError(target, error);
                }
            }

            if (cacheIsValid)
            {
                foreach (var styleTag in RequiredStyleTags)
                {
                    foreach (var localeCode in RequiredLocaleCodes)
                    {
                        if (!cache.TryGet(localeCode, styleTag, out var style))
                        {
                            report.AddError(target, $"Style '{styleTag}' does not resolve for locale '{localeCode}'.");
                            continue;
                        }

                        if (style.FontAsset == null)
                        {
                            report.AddError(target, $"Style '{styleTag}' locale '{localeCode}' resolves without a TMP font asset.");
                        }

                        if (style.MaterialPreset == null)
                        {
                            report.AddError(target, $"Style '{styleTag}' locale '{localeCode}' resolves without a material preset.");
                        }
                    }
                }
            }

            if (!report.HasErrors)
            {
                report.AddInfo(target, "Theme resolves all required typography styles for en-US and ko-KR.");
            }

            return report;
        }

        private static void ValidateRequiredLocaleCodes(
            GameplayUiTypographyTheme theme,
            string target,
            TypographyValidationReport report)
        {
            var configuredLocales = new HashSet<string>(
                theme.RequiredLocaleCodes.Select(NormalizeLocaleCode),
                StringComparer.Ordinal);
            var localeFontSets = new HashSet<string>(
                theme.LocaleFontSets
                    .Where(fontSet => fontSet != null)
                    .Select(fontSet => NormalizeLocaleCode(fontSet.LocaleCode)),
                StringComparer.Ordinal);

            foreach (var requiredLocaleCode in RequiredLocaleCodes)
            {
                if (!configuredLocales.Contains(requiredLocaleCode))
                {
                    report.AddError(target, $"Required locale '{requiredLocaleCode}' is not listed on the theme.");
                }

                if (!localeFontSets.Contains(requiredLocaleCode))
                {
                    report.AddError(target, $"Required locale '{requiredLocaleCode}' has no LocaleFontSet.");
                }
            }
        }

        private static string NormalizeLocaleCode(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
