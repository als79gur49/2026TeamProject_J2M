using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Screens;
using Game.Shared.Input;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsProductionTypographyCompositionTests
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        private const string GameplayScenePath = "Assets/Scenes/UIAudioScene.unity";
        private const string LocalePreferenceKey = "ui.selected_locale";

        private Locale _previousLocale;
        private bool _hadKeyboardBindingOverrides;
        private bool _hadKeyboardMovementScheme;
        private bool _hadLocalePreference;
        private MonoBehaviour _installedSceneInstaller;
        private string _previousKeyboardBindingOverrides;
        private string _previousKeyboardMovementScheme;
        private string _previousLocalePreference;

        [SetUp]
        public void SetUp()
        {
            _previousLocale = LocalizationSettings.SelectedLocale;
            _hadKeyboardBindingOverrides = PlayerPrefs.HasKey(
                PlayerPrefsKeyboardBindingStore.BindingOverridesJsonKey);
            _previousKeyboardBindingOverrides = PlayerPrefs.GetString(
                PlayerPrefsKeyboardBindingStore.BindingOverridesJsonKey,
                string.Empty);
            _hadKeyboardMovementScheme = PlayerPrefs.HasKey(
                PlayerPrefsKeyboardBindingStore.MovementSchemeKey);
            _previousKeyboardMovementScheme = PlayerPrefs.GetString(
                PlayerPrefsKeyboardBindingStore.MovementSchemeKey,
                string.Empty);
            _hadLocalePreference = PlayerPrefs.HasKey(LocalePreferenceKey);
            _previousLocalePreference = PlayerPrefs.GetString(LocalePreferenceKey, string.Empty);
            PlayerPrefs.SetString(LocalePreferenceKey, "en-US");
            PlayerPrefs.DeleteKey(PlayerPrefsKeyboardBindingStore.MovementSchemeKey);
            PlayerPrefs.DeleteKey(PlayerPrefsKeyboardBindingStore.BindingOverridesJsonKey);
            PlayerPrefs.Save();
            SetExternalLocale("en-US");
        }

        [TearDown]
        public void TearDown()
        {
            CloseAndDestroyLiveDropdownLists();
            DisposeInstalledSceneRuntime();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (_hadKeyboardBindingOverrides)
            {
                PlayerPrefs.SetString(
                    PlayerPrefsKeyboardBindingStore.BindingOverridesJsonKey,
                    _previousKeyboardBindingOverrides);
            }
            else
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKeyboardBindingStore.BindingOverridesJsonKey);
            }

            if (_hadKeyboardMovementScheme)
            {
                PlayerPrefs.SetString(
                    PlayerPrefsKeyboardBindingStore.MovementSchemeKey,
                    _previousKeyboardMovementScheme);
            }
            else
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKeyboardBindingStore.MovementSchemeKey);
            }

            if (_hadLocalePreference)
            {
                PlayerPrefs.SetString(LocalePreferenceKey, _previousLocalePreference);
            }
            else
            {
                PlayerPrefs.DeleteKey(LocalePreferenceKey);
            }

            PlayerPrefs.Save();
            if (_previousLocale != null)
            {
                LocalizationSettings.SelectedLocale = _previousLocale;
            }
        }

        [Test]
        public void SettingsPrefab_TypographyInventory_ClassifiesLocaleInvariantKeyDisplaysAndLocksGovernedTargets()
        {
            var prefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var inventory = BuildInventory(prefab);
            var allTmpTargets = prefab.GetComponentsInChildren<TMP_Text>(true);
            var allBindings = prefab.GetComponentsInChildren<TypographyBinding>(true);
            var nullTargetBindings = allBindings
                .Where(binding => GetSerializedBindingTarget(binding) == null)
                .Select(binding => GetHierarchyPath(binding.transform))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var validBindingTargets = allBindings
                .Select(binding => binding.Target)
                .Where(target => target != null)
                .ToArray();

            Assert.That(inventory, Has.Count.EqualTo(45));
            Assert.That(
                inventory.Count(item => item.Classification == TargetClassification.LocalizedStatic ||
                                        item.Classification == TargetClassification.LocalizedDynamic),
                Is.EqualTo(33));
            Assert.That(inventory.Count(item => item.Classification == TargetClassification.LocalizedStatic), Is.EqualTo(22));
            Assert.That(inventory.Count(item => item.Classification == TargetClassification.LocalizedDynamic), Is.EqualTo(11));
            Assert.That(inventory.Count(item => item.Classification == TargetClassification.LocaleInvariantKeyDisplay), Is.EqualTo(10));
            Assert.That(inventory.Count(item => item.Classification == TargetClassification.Decorative), Is.EqualTo(2));
            Assert.That(
                inventory.Count(item =>
                    TypographyBinding.FindFor(item.Target).LocaleParticipation ==
                    TypographyLocaleParticipation.LocaleThemed),
                Is.EqualTo(35));
            Assert.That(inventory.Select(item => item.Target), Is.Unique);
            Assert.That(nullTargetBindings, Is.Empty, $"Null-target TypographyBindings: {string.Join(", ", nullTargetBindings)}");
            var duplicateBindingTargets = validBindingTargets
                .GroupBy(target => target)
                .Where(group => group.Count() != 1)
                .Select(group => GetHierarchyPath(group.Key.transform))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                duplicateBindingTargets,
                Is.Empty,
                $"TMP targets with duplicate TypographyBindings: {string.Join(", ", duplicateBindingTargets)}");

            var inventoryTargets = inventory.Select(item => item.Target).ToArray();
            var unclassifiedTmpTargets = allTmpTargets
                .Except(inventoryTargets)
                .Select(target => GetHierarchyPath(target.transform))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var nonTmpInventoryTargets = inventoryTargets
                .Except(allTmpTargets)
                .Select(target => GetHierarchyPath(target.transform))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var unclassifiedBindings = validBindingTargets
                .Except(inventoryTargets)
                .Select(target => GetHierarchyPath(target.transform))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var missingBindings = inventoryTargets
                .Except(validBindingTargets)
                .Select(target => GetHierarchyPath(target.transform))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var invariantBindingsWithUnexpectedStyleTag = inventory
                .Where(item => item.Classification == TargetClassification.LocaleInvariantKeyDisplay)
                .Select(item => new
                {
                    item.Name,
                    Binding = TypographyBinding.FindFor(item.Target),
                })
                .Where(item => item.Binding == null || item.Binding.StyleTag != TypographyStyleTag.Value)
                .Select(item =>
                    $"{item.Name} ({(item.Binding == null ? "missing binding" : item.Binding.StyleTag.ToString())})")
                .ToArray();
            Assert.That(
                unclassifiedTmpTargets,
                Is.Empty,
                $"TMP targets missing inventory classification: {string.Join(", ", unclassifiedTmpTargets)}");
            Assert.That(
                nonTmpInventoryTargets,
                Is.Empty,
                $"Inventory entries outside the prefab TMP inventory: {string.Join(", ", nonTmpInventoryTargets)}");
            Assert.That(
                unclassifiedBindings,
                Is.Empty,
                $"TypographyBinding targets missing inventory classification: {string.Join(", ", unclassifiedBindings)}");
            Assert.That(missingBindings, Is.Empty, $"Manifest targets missing TypographyBinding: {string.Join(", ", missingBindings)}");
            Assert.That(
                invariantBindingsWithUnexpectedStyleTag,
                Is.Empty,
                "LocaleInvariant key displays must use TypographyStyleTag.Value: " +
                string.Join(", ", invariantBindingsWithUnexpectedStyleTag));

            foreach (var item in inventory)
            {
                var binding = TypographyBinding.FindFor(item.Target);
                Assert.That(binding, Is.Not.Null, item.Name);
                Assert.That(GetSerializedBindingTarget(binding), Is.SameAs(item.Target), $"{item.Name} serialized target");
                var expectedParticipation = item.Classification == TargetClassification.LocaleInvariantKeyDisplay
                    ? TypographyLocaleParticipation.LocaleInvariant
                    : TypographyLocaleParticipation.LocaleThemed;
                Assert.That(binding.LocaleParticipation, Is.EqualTo(expectedParticipation), item.Name);
            }

            Assert.That(
                inventory.Count(item =>
                    (item.Classification == TargetClassification.LocalizedStatic ||
                     item.Classification == TargetClassification.LocalizedDynamic) &&
                    TypographyBinding.FindFor(item.Target).LocaleParticipation == TypographyLocaleParticipation.LocaleThemed),
                Is.EqualTo(33));
        }

        [Test]
        public void MainMenuScene_ProductionSettings_RoundTripsTextAndTypographyThroughCatalogBuilder()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            var installer = FindInScene<MainMenuUiFlowInstaller>(scene);
            _installedSceneInstaller = installer;

            installer.Install();
            installer.MainMenuScreenView.ClickSettings();

            var view = FindInScene<SettingsScreenView>(scene);
            AssertProductionRoundTrip(view);
        }

        [Test]
        public void GameplayScene_ProductionSettings_RoundTripsTextAndTypographyThroughCatalogBuilder()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var installer = FindInScene<GameplayUiFlowInstaller>(scene);
            _installedSceneInstaller = installer;

            installer.Install(UiTestPortFactory.CreatePorts());
            Assert.That(installer.Coordinator.OpenSettingsScreen(), Is.True);

            var view = installer.SettingsScreenView;
            Assert.That(view, Is.Not.Null);
            AssertProductionRoundTrip(view);
        }

        private static void AssertProductionRoundTrip(SettingsScreenView view)
        {
            var catalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            var theme = catalog.SettingsTypographyTheme;
            var climateCrisisKr = UiTestPrefabAssetUtility.LoadClimateCrisisKrFont();
            var inventory = BuildInventory(view);
            var authoredInventory = BuildInventory(catalog.SettingsPrefab);
            var authoredByName = authoredInventory.ToDictionary(item => item.Name, StringComparer.Ordinal);
            var authoredStyles = inventory.ToDictionary(
                item => item.Target,
                item => new AppliedStyle(item.Target));

            Assert.That(GetField<TMP_Text>(view, "_titleLabel").text, Is.EqualTo("Settings"));
            Assert.That(view.InputView.IsMovementUsingArrowKeys, Is.False);
            Assert.That(GetField<TMP_Text>(view.InputView, "_pushCurrentText").text, Is.EqualTo("J"));
            Assert.That(GetField<TMP_Text>(view.InputView, "_pushKeyDisplayLabel").text, Is.EqualTo("J"));
            Assert.That(GetField<TMP_Text>(view.InputView, "_flipCurrentText").text, Is.EqualTo("K"));
            Assert.That(GetField<TMP_Text>(view.InputView, "_flipKeyDisplayLabel").text, Is.EqualTo("K"));
            AssertTypography(inventory, theme, "en-US");
            AssertSettingsStatusPreservesAuthoredSizing(view.DisplayView, theme, "en-US");
            AssertEnglishAuthoredPreservation(inventory, authoredInventory, theme);
            AssertLocaleInvariantKeyDisplays(inventory, authoredStyles, "initial en-US");

            view.ClickDisplayTab();
            EnsureDropdownEditModeLifecycle(view.DisplayView);
            Assert.That(view.DisplayView.OpenResolutionKeyboardList(0), Is.True);

            SetExternalLocale("ko-KR");

            Assert.That(GetField<TMP_Text>(view, "_titleLabel").text, Is.EqualTo("설정"));
            Assert.That(view.DisplayView.LanguageLabelText, Is.EqualTo("언어"));
            Assert.That(view.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
            Assert.That(GetAudioRowText(view.AudioView, "_mainRow", "Value").text, Is.Not.Empty);
            AssertTypography(inventory, theme, "ko-KR");
            AssertSettingsStatusPreservesAuthoredSizing(view.DisplayView, theme, "ko-KR");
            AssertLocaleInvariantKeyDisplays(inventory, authoredStyles, "ko-KR");
            foreach (var item in inventory.Where(item =>
                         item.Classification == TargetClassification.LocalizedStatic ||
                         item.Classification == TargetClassification.LocalizedDynamic))
            {
                Assert.That(item.Target.font, Is.SameAs(climateCrisisKr), item.Name);
            }

            Assert.That(view.DisplayView.IsResolutionKeyboardListOpen, Is.True);
            AssertLiveDropdownTypography(view.DisplayView, theme, "ko-KR");

            SetExternalLocale("en-US");

            Assert.That(GetField<TMP_Text>(view, "_titleLabel").text, Is.EqualTo("Settings"));
            Assert.That(view.DisplayView.LanguageLabelText, Is.EqualTo("Language"));
            Assert.That(view.DisplayView.CurrentLanguageText, Is.EqualTo("English"));
            AssertTypography(inventory, theme, "en-US");
            AssertSettingsStatusPreservesAuthoredSizing(view.DisplayView, theme, "restored en-US");
            AssertLocaleInvariantKeyDisplays(inventory, authoredStyles, "restored en-US");
            foreach (var item in inventory.Where(item =>
                         item.Classification == TargetClassification.LocalizedStatic ||
                         item.Classification == TargetClassification.LocalizedDynamic))
            {
                authoredStyles[item.Target].AssertThemeIdentitySame(item.Target, item.Name);
            }

            Assert.That(view.DisplayView.IsResolutionKeyboardListOpen, Is.True);
            AssertLiveDropdownTypography(view.DisplayView, theme, "en-US");
        }

        private static void AssertEnglishAuthoredPreservation(
            IEnumerable<TargetSpec> runtimeInventory,
            IEnumerable<TargetSpec> authoredInventory,
            GameplayUiTypographyTheme theme)
        {
            var authoredByName = authoredInventory.ToDictionary(item => item.Name, StringComparer.Ordinal);
            var mismatches = new List<string>();
            var auditRows = new List<string>();
            foreach (var runtimeItem in runtimeInventory)
            {
                if (runtimeItem.Classification == TargetClassification.LocaleInvariantKeyDisplay ||
                    runtimeItem.Classification == TargetClassification.Decorative)
                {
                    continue;
                }

                var authoredItem = authoredByName[runtimeItem.Name];
                var binding = TypographyBinding.FindFor(runtimeItem.Target);
                var expected = theme.ResolveOrThrow("en-US", binding.StyleTag);
                var korean = theme.ResolveOrThrow("ko-KR", binding.StyleTag);
                auditRows.Add(
                    $"{runtimeItem.Name} | {GetHierarchyPath(authoredItem.Target.transform)} | {DescribeValue(authoredItem.Target)} | {binding.StyleTag} | " +
                    $"authored {DescribeValue(authoredItem.Target.font)} / {DescribeValue(authoredItem.Target.fontSharedMaterial)} / {authoredItem.Target.fontStyle} | " +
                    $"en-US {DescribeValue(expected.FontAsset)} / {DescribeValue(expected.MaterialPreset)} / {expected.FontStyle} | " +
                    $"ko-KR {DescribeValue(korean.FontAsset)} / {DescribeValue(korean.MaterialPreset)} / {korean.FontStyle}");
                AddMismatch(
                    mismatches,
                    runtimeItem.Name,
                    binding.StyleTag,
                    "resolved font",
                    expected.FontAsset,
                    authoredItem.Target.font);
                AddMismatch(
                    mismatches,
                    runtimeItem.Name,
                    binding.StyleTag,
                    "resolved material",
                    expected.MaterialPreset,
                    authoredItem.Target.fontSharedMaterial);
                AddMismatch(
                    mismatches,
                    runtimeItem.Name,
                    binding.StyleTag,
                    "resolved fontStyle",
                    expected.FontStyle,
                    authoredItem.Target.fontStyle);

                AddMismatch(
                    mismatches,
                    runtimeItem.Name,
                    binding.StyleTag,
                    "runtime font",
                    runtimeItem.Target.font,
                    authoredItem.Target.font);
                AddMismatch(
                    mismatches,
                    runtimeItem.Name,
                    binding.StyleTag,
                    "runtime material",
                    runtimeItem.Target.fontSharedMaterial,
                    authoredItem.Target.fontSharedMaterial);
                AddMismatch(
                    mismatches,
                    runtimeItem.Name,
                    binding.StyleTag,
                    "runtime fontStyle",
                    runtimeItem.Target.fontStyle,
                    authoredItem.Target.fontStyle);
            }

            Assert.That(
                mismatches,
                Is.Empty,
                $"en-US authored typography mismatches ({mismatches.Count}):\n{string.Join("\n", mismatches)}" +
                $"\n35-target authored/theme audit:\n{string.Join("\n", auditRows)}");
        }

        private static void AddMismatch<T>(
            ICollection<string> mismatches,
            string targetName,
            TypographyStyleTag styleTag,
            string property,
            T actual,
            T authored)
        {
            if (EqualityComparer<T>.Default.Equals(actual, authored))
            {
                return;
            }

            mismatches.Add(
                $"{targetName} [{styleTag}] {property}: actual={DescribeValue(actual)}, authored={DescribeValue(authored)}");
        }

        private static string DescribeValue<T>(T value)
        {
            if (value is UnityEngine.Object unityObject)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(unityObject, out var guid, out long localId);
                return $"{unityObject.name} ({guid}:{localId})";
            }

            return value != null ? value.ToString() : "null";
        }

        private static void EnsureDropdownEditModeLifecycle(SettingsDisplayView displayView)
        {
            var dropdown = GetField<TMP_Dropdown>(displayView, "_resolutionDropdown");
            var tweenRunnerField = typeof(TMP_Dropdown).GetField(
                "m_AlphaTweenRunner",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tweenRunnerField, Is.Not.Null);
            if (tweenRunnerField.GetValue(dropdown) == null)
            {
                var start = typeof(TMP_Dropdown).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(start, Is.Not.Null);
                start.Invoke(dropdown, null);
            }

            dropdown.alphaFadeSpeed = 0f;
        }

        private static void AssertTypography(
            IEnumerable<TargetSpec> inventory,
            GameplayUiTypographyTheme theme,
            string localeCode)
        {
            foreach (var item in inventory)
            {
                if (item.Classification == TargetClassification.LocaleInvariantKeyDisplay)
                {
                    continue;
                }

                var binding = TypographyBinding.FindFor(item.Target);
                Assert.That(binding, Is.Not.Null, item.Name);
                var expected = theme.ResolveOrThrow(localeCode, binding.StyleTag);
                Assert.That(item.Target.font, Is.SameAs(expected.FontAsset), $"{item.Name} font");
                Assert.That(item.Target.fontSharedMaterial, Is.SameAs(expected.MaterialPreset), $"{item.Name} material");
                Assert.That(item.Target.fontStyle, Is.EqualTo(expected.FontStyle), $"{item.Name} fontStyle");
            }
        }

        private static void AssertLocaleInvariantKeyDisplays(
            IEnumerable<TargetSpec> inventory,
            IReadOnlyDictionary<TMP_Text, AppliedStyle> authoredStyles,
            string stage)
        {
            foreach (var item in inventory.Where(item =>
                         item.Classification == TargetClassification.LocaleInvariantKeyDisplay))
            {
                authoredStyles[item.Target].AssertSame(item.Target, $"{item.Name} at {stage}");
            }
        }

        private static void AssertSettingsStatusPreservesAuthoredSizing(
            SettingsDisplayView displayView,
            GameplayUiTypographyTheme theme,
            string stage)
        {
            var target = GetField<TMP_Text>(displayView, "_displayStatusLabel");
            var binding = TypographyBinding.FindFor(target);
            Assert.That(binding, Is.Not.Null, stage);
            Assert.That(binding.StyleTag, Is.EqualTo(TypographyStyleTag.SettingsStatus), stage);
            Assert.That(binding.SizingSourceOverride, Is.EqualTo(TypographySizingSource.Hybrid), stage);
            Assert.That(binding.UseApplyMaskOverride, Is.False, stage);

            var localeCode = string.Equals(stage, "ko-KR", StringComparison.Ordinal) ? "ko-KR" : "en-US";
            var style = theme.ResolveOrThrow(localeCode, TypographyStyleTag.SettingsStatus);
            Assert.That(style.SizingMode, Is.EqualTo(TypographySizingMode.PreserveAuthored), stage);
            Assert.That(style.ApplyMask & TypographyApplyMask.Sizing, Is.EqualTo(TypographyApplyMask.None), stage);
            Assert.That(target.fontSize, Is.EqualTo(14f), stage);
            Assert.That(target.enableAutoSizing, Is.True, stage);
            Assert.That(target.fontSizeMin, Is.EqualTo(10f), stage);
            Assert.That(target.fontSizeMax, Is.EqualTo(14f), stage);
        }

        private static void AssertLiveDropdownTypography(
            SettingsDisplayView displayView,
            GameplayUiTypographyTheme theme,
            string localeCode)
        {
            var dropdown = GetField<TMP_Dropdown>(displayView, "_resolutionDropdown");
            var liveList = dropdown.transform.Find("Dropdown List");
            Assert.That(liveList, Is.Not.Null);
            var liveLabels = liveList.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(liveLabels, Is.Not.Empty);
            foreach (var label in liveLabels)
            {
                var binding = TypographyBinding.FindFor(label);
                Assert.That(binding, Is.Not.Null, label.name);
                var expected = theme.ResolveOrThrow(localeCode, binding.StyleTag);
                Assert.That(label.font, Is.SameAs(expected.FontAsset), label.name);
                Assert.That(label.fontSharedMaterial, Is.SameAs(expected.MaterialPreset), label.name);
                Assert.That(label.fontStyle, Is.EqualTo(expected.FontStyle), label.name);
            }
        }

        private static List<TargetSpec> BuildInventory(SettingsScreenView view)
        {
            var result = new List<TargetSpec>
            {
                Static("Settings title", GetField<TMP_Text>(view, "_titleLabel")),
                Static("Audio tab", GetField<TMP_Text>(view, "_audioTabButtonLabel")),
                Static("Display tab", GetField<TMP_Text>(view, "_displayTabButtonLabel")),
                Static("Input tab", GetField<TMP_Text>(view, "_inputTabButtonLabel")),
                Static("Back", GetField<TMP_Text>(view, "_backButtonLabel")),
            };

            AddAudioRow(result, view.AudioView, "_mainRow", "Main");
            AddAudioRow(result, view.AudioView, "_bgmRow", "BGM");
            AddAudioRow(result, view.AudioView, "_sfxRow", "SFX");

            var display = view.DisplayView;
            result.AddRange(new[]
            {
                Static("Display current", GetField<TMP_Text>(display, "_currentDisplayLabel")),
                Dynamic("Display current value", GetField<TMP_Text>(display, "_currentDisplayValue")),
                Static("Display resolution", GetField<TMP_Text>(display, "_resolutionLabel")),
                Static("Display hint", GetField<TMP_Text>(display, "_resolutionHoverHintLabel")),
                Static("Display fullscreen", GetField<TMP_Text>(display, "_fullscreenLabel")),
                Static("Display fullscreen toggle", GetField<TMP_Text>(display, "_fullscreenToggleLabel")),
                Static("Display apply", GetField<TMP_Text>(display, "_applyButtonLabel")),
                Static("Display revert", GetField<TMP_Text>(display, "_revertButtonLabel")),
                Dynamic("Display language label", GetField<TMP_Text>(display, "_languageLabel")),
                Dynamic("Display language value", GetField<TMP_Text>(display, "_languageCycleButtonLabel")),
                Dynamic("Display status", GetField<TMP_Text>(display, "_displayStatusLabel")),
                Dynamic("Display countdown", GetField<TMP_Text>(display, "_previewCountdownLabel")),
                Dynamic("Dropdown caption", GetField<TMP_Dropdown>(display, "_resolutionDropdown").captionText),
                Dynamic("Dropdown item template", GetField<TMP_Dropdown>(display, "_resolutionDropdown").itemText),
                Decorative("Dropdown arrow", GetField<TMP_Dropdown>(display, "_resolutionDropdown").transform.Find("Arrow").GetComponent<TMP_Text>()),
                Decorative("Resolution info marker", GetField<RectTransform>(display, "_resolutionInfoHotspot").GetComponentInChildren<TMP_Text>(true)),
            });

            var input = view.InputView;
            result.AddRange(new[]
            {
                Static("Input movement", GetField<TMP_Text>(input, "_movementLabel")),
                Static("Input push", GetField<TMP_Text>(input, "_pushLabel")),
                LocaleInvariantKeyDisplay("Input push current", GetField<TMP_Text>(input, "_pushCurrentText")),
                LocaleInvariantKeyDisplay("Input push physical key", GetField<TMP_Text>(input, "_pushKeyDisplayLabel")),
                Static("Input flip", GetField<TMP_Text>(input, "_flipLabel")),
                LocaleInvariantKeyDisplay("Input flip current", GetField<TMP_Text>(input, "_flipCurrentText")),
                LocaleInvariantKeyDisplay("Input flip physical key", GetField<TMP_Text>(input, "_flipKeyDisplayLabel")),
                Static("Input reset", GetField<TMP_Text>(input, "_resetButtonLabel")),
                Dynamic("Input status", GetField<TMP_Text>(input, "_statusText")),
            });
            AddMovementKeycaps(result, input, "MovementInputRow/WASDKeyDisplay", 6);
            return result;
        }

        private static void AddMovementKeycaps(
            ICollection<TargetSpec> result,
            SettingsInputView input,
            string relativePath,
            int expectedCount)
        {
            var root = input.transform.Find(relativePath);
            Assert.That(root, Is.Not.Null, relativePath);
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(labels, Has.Length.EqualTo(expectedCount), relativePath);
            for (var i = 0; i < labels.Length; i++)
            {
                result.Add(LocaleInvariantKeyDisplay($"Movement keycap {relativePath} #{i}", labels[i]));
            }
        }

        private static void AddAudioRow(
            ICollection<TargetSpec> result,
            SettingsAudioView view,
            string rowField,
            string rowName)
        {
            result.Add(Static($"Audio {rowName} label", GetAudioRowText(view, rowField, "Label")));
            result.Add(Dynamic($"Audio {rowName} value", GetAudioRowText(view, rowField, "Value")));
            result.Add(Static($"Audio {rowName} mute", GetAudioRowText(view, rowField, "MuteLabel")));
        }

        private static TMP_Text GetAudioRowText(SettingsAudioView view, string rowField, string propertyName)
        {
            var row = GetField<object>(view, rowField);
            var property = row.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"{rowField}.{propertyName}");
            return (TMP_Text)property.GetValue(row);
        }

        private static TargetSpec Static(string name, TMP_Text target) =>
            new(name, target, TargetClassification.LocalizedStatic);

        private static TargetSpec Dynamic(string name, TMP_Text target) =>
            new(name, target, TargetClassification.LocalizedDynamic);

        private static TargetSpec LocaleInvariantKeyDisplay(string name, TMP_Text target) =>
            new(name, target, TargetClassification.LocaleInvariantKeyDisplay);

        private static TargetSpec Decorative(string name, TMP_Text target) =>
            new(name, target, TargetClassification.Decorative);

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            var component = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Single();
            Assert.That(component, Is.Not.Null, typeof(T).Name);
            return component;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private static TMP_Text GetSerializedBindingTarget(TypographyBinding binding)
        {
            var serializedObject = new SerializedObject(binding);
            return serializedObject.FindProperty("target")?.objectReferenceValue as TMP_Text;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }

        private static void SetExternalLocale(string localeCode)
        {
            var locale = LocalizationEditorSettings.GetLocale(localeCode);
            Assert.That(locale, Is.Not.Null, localeCode);
            LocalizationSettings.SelectedLocale = locale;
        }

        private static void CloseAndDestroyLiveDropdownLists()
        {
            var dropdowns = UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var dropdown in dropdowns)
            {
                if (dropdown == null)
                {
                    continue;
                }

                var dropdownType = typeof(TMP_Dropdown);
                var liveListField = dropdownType.GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
                var blockerField = dropdownType.GetField("m_Blocker", BindingFlags.Instance | BindingFlags.NonPublic);
                var itemsField = dropdownType.GetField("m_Items", BindingFlags.Instance | BindingFlags.NonPublic);
                var liveList = liveListField?.GetValue(dropdown) as GameObject;
                var blocker = blockerField?.GetValue(dropdown) as GameObject;
                liveListField?.SetValue(dropdown, null);
                blockerField?.SetValue(dropdown, null);
                if (itemsField?.GetValue(dropdown) is System.Collections.IList items)
                {
                    items.Clear();
                }

                if (liveList != null)
                {
                    UnityEngine.Object.DestroyImmediate(liveList);
                }

                if (blocker != null)
                {
                    UnityEngine.Object.DestroyImmediate(blocker);
                }
            }
        }

        private void DisposeInstalledSceneRuntime()
        {
            if (_installedSceneInstaller == null)
            {
                return;
            }

            var onDestroy = _installedSceneInstaller.GetType().GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onDestroy, Is.Not.Null);
            onDestroy.Invoke(_installedSceneInstaller, null);
            _installedSceneInstaller = null;
        }

        private enum TargetClassification
        {
            LocalizedStatic,
            LocalizedDynamic,
            LocaleInvariantKeyDisplay,
            Decorative,
        }

        private readonly struct TargetSpec
        {
            public TargetSpec(string name, TMP_Text target, TargetClassification classification)
            {
                Name = name;
                Target = target;
                Classification = classification;
            }

            public string Name { get; }
            public TMP_Text Target { get; }
            public TargetClassification Classification { get; }
        }

        private readonly struct AppliedStyle
        {
            private readonly TMP_FontAsset _font;
            private readonly Material _material;
            private readonly FontStyles _fontStyle;
            private readonly float _fontSize;
            private readonly bool _enableAutoSizing;
            private readonly float _fontSizeMin;
            private readonly float _fontSizeMax;
            private readonly float _lineSpacing;
            private readonly float _characterSpacing;
            private readonly string _text;

            public AppliedStyle(TMP_Text target)
            {
                _font = target.font;
                _material = target.fontSharedMaterial;
                _fontStyle = target.fontStyle;
                _fontSize = target.fontSize;
                _enableAutoSizing = target.enableAutoSizing;
                _fontSizeMin = target.fontSizeMin;
                _fontSizeMax = target.fontSizeMax;
                _lineSpacing = target.lineSpacing;
                _characterSpacing = target.characterSpacing;
                _text = target.text;
            }

            public void AssertSame(TMP_Text target, string name)
            {
                Assert.That(target.font, Is.SameAs(_font), $"{name} restored font");
                Assert.That(target.fontSharedMaterial, Is.SameAs(_material), $"{name} restored material");
                Assert.That(target.fontStyle, Is.EqualTo(_fontStyle), $"{name} restored fontStyle");
                Assert.That(target.fontSize, Is.EqualTo(_fontSize), $"{name} fontSize");
                Assert.That(target.enableAutoSizing, Is.EqualTo(_enableAutoSizing), $"{name} enableAutoSizing");
                Assert.That(target.fontSizeMin, Is.EqualTo(_fontSizeMin), $"{name} fontSizeMin");
                Assert.That(target.fontSizeMax, Is.EqualTo(_fontSizeMax), $"{name} fontSizeMax");
                Assert.That(target.lineSpacing, Is.EqualTo(_lineSpacing), $"{name} lineSpacing");
                Assert.That(target.characterSpacing, Is.EqualTo(_characterSpacing), $"{name} characterSpacing");
                Assert.That(target.text, Is.EqualTo(_text), $"{name} text");
            }

            public void AssertThemeIdentitySame(TMP_Text target, string name)
            {
                Assert.That(target.font, Is.SameAs(_font), $"{name} restored font");
                Assert.That(target.fontSharedMaterial, Is.SameAs(_material), $"{name} restored material");
                Assert.That(target.fontStyle, Is.EqualTo(_fontStyle), $"{name} restored fontStyle");
            }
        }
    }
}
