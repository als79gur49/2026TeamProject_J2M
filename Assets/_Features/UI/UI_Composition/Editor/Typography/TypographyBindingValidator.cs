using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Composition.Editor
{
    public static class TypographyBindingValidator
    {
        public static readonly string[] RequiredPrefabPaths =
        {
            "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab",
            "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab",
            "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab",
        };

        public static TypographyValidationReport ValidateRequiredPrefabs(GameplayUiTypographyTheme theme = null)
        {
            var report = new TypographyValidationReport("Typography Binding Prefab Validation");
            theme ??= TypographyThemeValidator.FindThemeAsset();

            foreach (var prefabPath in RequiredPrefabPaths)
            {
                report.Merge(ValidatePrefabAtPath(prefabPath, theme));
            }

            return report;
        }

        public static TypographyValidationReport ValidatePrefabAtPath(
            string prefabPath,
            GameplayUiTypographyTheme theme)
        {
            var report = new TypographyValidationReport($"Typography Binding Validation - {prefabPath}");
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                report.AddError(prefabPath, "Prefab asset was not found.");
                return report;
            }

            ValidateRoot(prefabRoot, prefabPath, theme, report);
            return report;
        }

        public static TypographyValidationReport ValidateRoot(
            GameObject root,
            string targetName,
            GameplayUiTypographyTheme theme)
        {
            var report = new TypographyValidationReport($"Typography Binding Validation - {targetName}");
            ValidateRoot(root, targetName, theme, report);
            return report;
        }

        private static void ValidateRoot(
            GameObject root,
            string targetName,
            GameplayUiTypographyTheme theme,
            TypographyValidationReport report)
        {
            if (root == null)
            {
                report.AddError(targetName, "Root GameObject is null.");
                return;
            }

            if (theme == null)
            {
                report.AddError(targetName, "GameplayUiTypographyTheme asset was not found.");
            }

            var bindings = root.GetComponentsInChildren<TypographyBinding>(true);
            var textTargets = root.GetComponentsInChildren<TMP_Text>(true);
            if (bindings.Length == 0)
            {
                report.AddError(targetName, "No TypographyBinding components found.");
            }

            var bindingsByTarget = new Dictionary<TMP_Text, List<TypographyBinding>>();
            foreach (var binding in bindings)
            {
                ValidateBinding(binding, targetName, theme, report);
                var target = binding != null ? binding.Target : null;
                if (target == null)
                {
                    continue;
                }

                if (!bindingsByTarget.TryGetValue(target, out var targetBindings))
                {
                    targetBindings = new List<TypographyBinding>();
                    bindingsByTarget.Add(target, targetBindings);
                }

                targetBindings.Add(binding);
            }

            foreach (var pair in bindingsByTarget.Where(pair => pair.Value.Count > 1))
            {
                report.AddError(
                    BuildObjectTarget(targetName, pair.Key),
                    $"TMP_Text has {pair.Value.Count} TypographyBinding components targeting it.");
            }

            foreach (var textTarget in textTargets)
            {
                if (TypographyBinding.FindFor(textTarget) == null)
                {
                    report.AddWarning(
                        BuildObjectTarget(targetName, textTarget),
                        "TMP_Text has no TypographyBinding; verify it is not a localized/static typography surface.");
                }
            }

            if (!report.HasErrors)
            {
                report.AddInfo(targetName, $"{bindings.Length} TypographyBinding component(s) validated.");
            }
        }

        private static void ValidateBinding(
            TypographyBinding binding,
            string targetName,
            GameplayUiTypographyTheme theme,
            TypographyValidationReport report)
        {
            if (binding == null)
            {
                report.AddError(targetName, "TypographyBinding is null.");
                return;
            }

            var objectTarget = BuildObjectTarget(targetName, binding);
            var serializedTarget = GetSerializedTarget(binding);
            if (serializedTarget == null)
            {
                report.AddError(objectTarget, "TypographyBinding.target is not assigned.");
            }

            if (binding.Target == null)
            {
                report.AddError(objectTarget, "TypographyBinding cannot resolve a TMP_Text target.");
                return;
            }

            if (binding.StyleTag == TypographyStyleTag.Default)
            {
                report.AddWarning(objectTarget, "StyleTag is Default; confirm this is intentional.");
            }

            if (!Enum.IsDefined(typeof(TypographyLocaleParticipation), binding.LocaleParticipation))
            {
                report.AddError(
                    objectTarget,
                    $"Locale participation value '{(int)binding.LocaleParticipation}' is invalid.");
                return;
            }

            if (binding.LocaleParticipation == TypographyLocaleParticipation.LocaleInvariant)
            {
                return;
            }

            if (theme == null)
            {
                return;
            }

            foreach (var localeCode in TypographyThemeValidator.RequiredLocaleCodes)
            {
                if (!theme.TryResolve(localeCode, binding.StyleTag, out var style))
                {
                    report.AddError(objectTarget, $"StyleTag '{binding.StyleTag}' does not resolve for '{localeCode}'.");
                    continue;
                }

                if (style.FontAsset == null || style.MaterialPreset == null)
                {
                    report.AddError(
                        objectTarget,
                        $"StyleTag '{binding.StyleTag}' for '{localeCode}' resolves without a complete font/material pair.");
                }
            }
        }

        private static TMP_Text GetSerializedTarget(TypographyBinding binding)
        {
            var serializedObject = new SerializedObject(binding);
            var targetProperty = serializedObject.FindProperty("target");
            return targetProperty != null ? targetProperty.objectReferenceValue as TMP_Text : null;
        }

        private static string BuildObjectTarget(string rootName, UnityEngine.Object target)
        {
            return target == null
                ? rootName
                : $"{rootName}/{target.name}";
        }
    }
}
