using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class ClimateCrisisKrTypographyContractTests
    {
        private const string ThemeAssetPath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";
        private const string PausePrefabPath = "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab";
        private const string SettingsPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab";
        private const string SourceFontPath = "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000.ttf";
        private const string FontAssetPath = "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";
        private const string UiKoreanStringTablePath =
            "Assets/Localization/StringTables/UI/UI_ko-KR.asset";
        private const string StageKoreanStringTablePath =
            "Assets/Localization/StringTables/Stage/Stage_ko-KR.asset";
        private const string ClimateFontGuid = "40d61154fd6576b4d85c2d78460b16ad";
        private const long ClimateFontLocalId = 11400000;
        private const long ClimateMaterialLocalId = 1352911973252649374;
        private const string EnglishContractSha256 =
            "3def0381e783b5bd7286e824a6fcea11dddb659ed5a3aab667a239d070868f92";

        [Test]
        public void ClimateAssets_KeepRuntimeIdentityAndCanonicalReferences()
        {
            var fontAsset = LoadClimateFont();
            var material = fontAsset.material;

            Assert.That(AssetDatabase.AssetPathToGUID(SourceFontPath), Is.EqualTo("5360535d0de75234ca21822297323672"));
            AssertAssetIdentity(fontAsset, ClimateFontGuid, ClimateFontLocalId, "Climate TMP font");
            AssertAssetIdentity(material, ClimateFontGuid, ClimateMaterialLocalId, "Climate canonical material");
            Assert.That(fontAsset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(fontAsset.atlasTextures, Has.Length.EqualTo(1));
            Assert.That(fontAsset.atlasTextures[0], Is.Not.Null);
            Assert.That(fontAsset.fallbackFontAssetTable, Is.Empty);
        }

        [Test]
        public void ClimateSourceGuard_SeparatesHeadBlobIntegrityFromWorkingImportState()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var runner = File.ReadAllText(Path.Combine(repoRoot, "run_tests.sh"));

            Assert.That(
                runner,
                Does.Contain("git show \"HEAD:$path\" | sha256sum"),
                "Committed source integrity must read Git HEAD blobs.");
            Assert.That(
                runner,
                Does.Contain("git_head_runner_constant"),
                "Historical HEAD blobs must use the constants committed with that HEAD.");
            Assert.That(
                runner,
                Does.Contain("Climate committed source integrity: PASS"));
            Assert.That(
                runner,
                Does.Contain("verify_climate_worktree_source_integrity"),
                "The candidate lane must validate current worktree bytes before commit.");
            Assert.That(
                runner,
                Does.Contain("sha256sum \"$PROJECT_PATH_WSL/$CLIMATE_SDF_ASSET\""),
                "The candidate Climate SDF hash must come from the current worktree.");
            Assert.That(
                runner,
                Does.Contain("require_worktree_file_text"),
                "Candidate meta/GUID/reference validation must read worktree files.");
            Assert.That(
                runner,
                Does.Contain("EXPECTED_IMPORT_DERIVED_DRIFT"));
            Assert.That(
                runner,
                Does.Contain("_ScaleRatioA:1->0.9"));
            Assert.That(
                runner,
                Does.Contain("_ScaleRatioC:1->0.73125"));
            Assert.That(
                runner,
                Does.Not.Contain("71ae00a952cf086150c90764db323bf078bf871e133ce52844cc1c94070d6445"),
                "An entire derived Climate blob must not be accepted as an allowlist.");
        }

        [Test]
        public void CaptureRunner_ObservesAllGuardedAssetsBeforeRestoreAndBeforeFailureReturn()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var runner = File.ReadAllText(Path.Combine(repoRoot, "run_tests.sh"));

            AssertVisualGuardRestoreOrdering(runner);
            AssertCaptureFailureOrdering(runner, "run_typography_visual()", "run_objective_hud_visual()");
            AssertCaptureFailureOrdering(runner, "run_objective_hud_visual()", "run_unity_full()");
            Assert.That(
                runner,
                Does.Contain("observation_order=ALL_GUARDED_PATHS_BEFORE_ANY_RESTORE"));
            Assert.That(runner, Does.Contain("UNEXPECTED_ASSET_MUTATION"));
            Assert.That(runner, Does.Contain("lane_verdict_before_restore"));
            Assert.That(runner, Does.Contain("restored_hash"));
        }

        [Test]
        public void ClimateSdf_NativelyCoversManagedKoreanStringTables()
        {
            var fontAsset = LoadClimateFont();
            var tablePaths = AssetDatabase
                .FindAssets("t:StringTable", new[] { "Assets/Localization/StringTables" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith("_ko-KR.asset", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var tables = tablePaths.Select(LoadStringTable).ToArray();
            var values = tables
                .SelectMany(table => table.SharedData.Entries.Select(entry => table.GetEntry(entry.Key)?.LocalizedValue))
                .Where(value => !string.IsNullOrEmpty(value))
                .ToArray();
            var codepoints = values
                .SelectMany(value => value)
                .Where(character => character > 0x7f)
                .Distinct()
                .OrderBy(character => character)
                .ToArray();
            var missing = codepoints
                .Where(character => !fontAsset.HasCharacter(character, searchFallbacks: false, tryAddCharacter: false))
                .ToArray();

            Assert.That(
                tablePaths,
                Is.EquivalentTo(new[] { StageKoreanStringTablePath, UiKoreanStringTablePath }),
                "Every managed ko-KR table must participate in native Climate glyph validation.");
            Assert.That(values, Has.Length.EqualTo(109));
            Assert.That(values.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(99));
            Assert.That(values, Does.Contain("밀기 키 입력하세요..."));
            Assert.That(values, Does.Contain("뒤집기 키 입력하세요..."));
            Assert.That(values, Does.Contain("기회"));
            Assert.That(values, Does.Contain("이동"));
            Assert.That(codepoints, Has.Length.EqualTo(158));
            Assert.That(missing, Is.Empty, FormatCharacters(missing));
            Assert.That(fontAsset.fallbackFontAssetTable, Is.Empty);
            Assert.That(TMP_Settings.fallbackFontAssets, Is.Empty);
        }

        [Test]
        public void ProductionTheme_ResolvesNineteenClimateNormalKoreanRolesWithoutSizing()
        {
            var theme = LoadTheme();
            var fontAsset = LoadClimateFont();
            var roles = Enum.GetValues(typeof(TypographyStyleTag)).Cast<TypographyStyleTag>().ToArray();
            var expectedMask =
                TypographyApplyMask.Font |
                TypographyApplyMask.Material |
                TypographyApplyMask.FontStyle;
            var baseDuplicates = theme.BaseRules
                .GroupBy(rule => rule.StyleTag)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            var localeDuplicates = theme.LocaleFontSets
                .GroupBy(fontSet => fontSet.LocaleCode, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            var koreanOverrides = theme.SparseOverrides
                .Where(styleOverride => string.Equals(styleOverride.LocaleCode, "ko-KR", StringComparison.Ordinal))
                .ToArray();
            var koreanOverrideDuplicates = koreanOverrides
                .GroupBy(styleOverride => styleOverride.Rule.StyleTag)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.That(roles, Has.Length.EqualTo(19));
            Assert.That(theme.RequiredLocaleCodes, Is.EqualTo(new[] { "en-US", "ko-KR" }));
            Assert.That(theme.BaseRules, Has.Count.EqualTo(19));
            Assert.That(theme.BaseRules.Select(rule => rule.StyleTag), Is.EqualTo(roles));
            Assert.That(baseDuplicates, Is.Empty, "Base role duplicates");
            Assert.That(theme.LocaleFontSets, Has.Count.EqualTo(2));
            Assert.That(localeDuplicates, Is.Empty, "Locale font-set duplicates");
            foreach (var fontSet in theme.LocaleFontSets)
            {
                var entryDuplicates = fontSet.Entries
                    .GroupBy(entry => (entry.FontCategory, entry.Weight))
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();

                Assert.That(fontSet.Entries, Has.Count.EqualTo(7), $"{fontSet.LocaleCode} font entries");
                Assert.That(entryDuplicates, Is.Empty, $"{fontSet.LocaleCode} font-entry duplicates");
            }
            Assert.That(
                koreanOverrides.Select(styleOverride => styleOverride.Rule.StyleTag),
                Is.SubsetOf(roles),
                "Sparse overrides only need entries where ko-KR differs from the base rule.");
            Assert.That(koreanOverrideDuplicates, Is.Empty, "ko-KR role override duplicates");
            Assert.That(theme.BuildCache().Count, Is.EqualTo(38));

            foreach (var role in roles)
            {
                Assert.That(theme.TryResolve("en-US", role, out _), Is.True, $"en-US {role}");
                Assert.That(theme.TryResolve("ko-KR", role, out var korean), Is.True, $"ko-KR {role}");
                Assert.That(korean.FontAsset, Is.SameAs(fontAsset), role.ToString());
                Assert.That(korean.MaterialPreset, Is.SameAs(fontAsset.material), role.ToString());
                Assert.That(korean.FontStyle, Is.EqualTo(FontStyles.Normal), role.ToString());
                Assert.That(korean.ApplyMask, Is.EqualTo(expectedMask), role.ToString());
                Assert.That(korean.SizingSource, Is.EqualTo(TypographySizingSource.Hybrid), role.ToString());
                Assert.That(korean.SizingMode, Is.EqualTo(TypographySizingMode.PreserveAuthored), role.ToString());
                Assert.That(korean.ApplyMask & TypographyApplyMask.Sizing, Is.EqualTo(TypographyApplyMask.None));
                Assert.That(korean.WeightStrategy, Is.EqualTo(TypographyWeightStrategy.UseFontAsset), role.ToString());
            }

            Assert.That(roles, Does.Contain(TypographyStyleTag.MainMenuCommand));
            Assert.That(roles, Does.Contain(TypographyStyleTag.PopupBody));
            Assert.That(roles, Does.Contain(TypographyStyleTag.PopupAction));
        }

        [Test]
        public void ProductionTheme_EnglishSerializedContractRemainsBitForBit()
        {
            var yaml = File.ReadAllText(ThemeAssetPath);
            var baseRules = Slice(yaml, "  baseRules:\n", "  localeFontSets:\n");
            var englishFontSet = Slice(yaml, "  - LocaleCode: en-US\n", "  - LocaleCode: ko-KR\n");

            Assert.That(ComputeTextSha256(baseRules + englishFontSet), Is.EqualTo(EnglishContractSha256));

            var genericButton = LoadTheme().ResolveOrThrow("en-US", TypographyStyleTag.Button);
            AssertAssetIdentity(
                genericButton.FontAsset,
                "dec0b1c5d015b39438a16d1bffa2e9ca",
                ClimateFontLocalId,
                "Generic Button SciFiSoldier font");
            AssertAssetIdentity(
                genericButton.MaterialPreset,
                "dec0b1c5d015b39438a16d1bffa2e9ca",
                6254369423063020181,
                "Generic Button SciFiSoldier material");
            Assert.That(genericButton.FontStyle, Is.EqualTo(FontStyles.Bold));

            var mainMenuCommand = LoadTheme().ResolveOrThrow("en-US", TypographyStyleTag.MainMenuCommand);
            AssertAssetIdentity(
                mainMenuCommand.FontAsset,
                "819507a38fa816a489de88dad2de2ce9",
                ClimateFontLocalId,
                "MainMenuCommand Orbitron font");
            AssertAssetIdentity(
                mainMenuCommand.MaterialPreset,
                "819507a38fa816a489de88dad2de2ce9",
                -6419728470944652023,
                "MainMenuCommand Orbitron material");
            Assert.That(mainMenuCommand.FontStyle, Is.EqualTo(FontStyles.Bold));
        }

        [Test]
        public void LegacyNanumAssets_RemainAvailableDuringClimateMigration()
        {
            var expected = new Dictionary<string, string>
            {
                ["Assets/_Shared/UI/Fonts/NanumGothic.ttf"] =
                    "9efe96b63470e314280dc43c0aa565db",
                ["Assets/_Shared/UI/Fonts/NanumGothic SDF.asset"] =
                    "4662feb1d501d1f479b757a82e304069",
                ["Assets/_Features/UI/UI_Composition/Authoring/Typography/NanumGothic SDF SyntheticBold.mat"] =
                    "2a2e67f1c1d143dc9f2d4af986ba7f21",
            };

            foreach (var pair in expected)
            {
                Assert.That(File.Exists(pair.Key), Is.True, pair.Key);
                Assert.That(File.Exists(pair.Key + ".meta"), Is.True, pair.Key + ".meta");
                Assert.That(AssetDatabase.AssetPathToGUID(pair.Key), Is.EqualTo(pair.Value), pair.Key);
                Assert.That(
                    File.ReadAllText(pair.Key + ".meta"),
                    Does.Contain($"guid: {pair.Value}"),
                    pair.Key + ".meta");
            }

            var sdf = File.ReadAllText("Assets/_Shared/UI/Fonts/NanumGothic SDF.asset");
            var material = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Authoring/Typography/NanumGothic SDF SyntheticBold.mat");
            Assert.That(
                sdf,
                Does.Contain("m_SourceFontFileGUID: 9efe96b63470e314280dc43c0aa565db"));
            Assert.That(
                material,
                Does.Contain("guid: 4662feb1d501d1f479b757a82e304069"));

            var repoRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var runner = File.ReadAllText(Path.Combine(repoRoot, "run_tests.sh"));
            Assert.That(
                runner,
                Does.Contain("git cat-file -e \"HEAD:$retained_path\""),
                "A working-tree-generated .meta must not satisfy committed retention.");
            foreach (var pair in expected)
            {
                Assert.That(runner, Does.Contain(pair.Key));
                Assert.That(runner, Does.Contain(pair.Key + ".meta"));
                Assert.That(runner, Does.Contain(pair.Value));
            }
        }

        [Test]
        public void ProductionPrefabs_KeepApprovedClimateLayoutContract()
        {
            var pause = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(PausePrefabPath);
            var settings = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(SettingsPrefabPath);
            Assert.That(pause, Is.Not.Null, PausePrefabPath);
            Assert.That(settings, Is.Not.Null, SettingsPrefabPath);

            AssertPauseTitleLayout(pause);
            AssertAudioValueLayouts(settings);
            AssertDisplayStatusLayout(settings);
        }

        private static void AssertPauseTitleLayout(PausePopupView pause)
        {
            var title = GetField<TMP_Text>(pause, "_titleLabel");
            var rect = title.rectTransform;
            var binding = TypographyBinding.FindFor(title);
            var visualCenter = rect.anchoredPosition.x + (0.5f - rect.pivot.x) * rect.sizeDelta.x;

            Assert.That(GetRelativePath(title.transform), Is.EqualTo("Title"));
            AssertAssetIdentity(title, AssetDatabase.AssetPathToGUID(PausePrefabPath), 8453535842778186484, "Pause title TMP");
            AssertAssetIdentity(rect, AssetDatabase.AssetPathToGUID(PausePrefabPath), 931421177423540265, "Pause title RectTransform");
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(120f, -20f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(160f, 40f)));
            Assert.That(visualCenter, Is.EqualTo(200f).Within(0.01f));
            Assert.That(title.fontSize, Is.EqualTo(30f));
            Assert.That(title.enableAutoSizing, Is.True);
            Assert.That(title.fontSizeMin, Is.EqualTo(14f));
            Assert.That(title.fontSizeMax, Is.EqualTo(30f));
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.StyleTag, Is.EqualTo(TypographyStyleTag.HeaderMedium));
            Assert.That(binding.SizingSourceOverride, Is.EqualTo(TypographySizingSource.Hybrid));
            Assert.That(binding.UseApplyMaskOverride, Is.False);
        }

        private static void AssertCaptureFailureOrdering(
            string runner,
            string functionName,
            string nextFunctionName)
        {
            var functionStart = runner.IndexOf(functionName, StringComparison.Ordinal);
            var functionEnd = runner.IndexOf(
                nextFunctionName,
                functionStart + functionName.Length,
                StringComparison.Ordinal);
            Assert.That(functionStart, Is.GreaterThanOrEqualTo(0), functionName);
            Assert.That(functionEnd, Is.GreaterThan(functionStart), nextFunctionName);

            var functionBody = runner.Substring(functionStart, functionEnd - functionStart);
            var observe = functionBody.IndexOf(
                "observe_capture_assets_before_restore",
                StringComparison.Ordinal);
            var cleanup = functionBody.IndexOf(
                "visual_guard_cleanup \"$unity_exit\"",
                StringComparison.Ordinal);
            var failureReturn = functionBody.IndexOf(
                "if [ \"$unity_exit\" -ne 0 ]",
                StringComparison.Ordinal);

            Assert.That(observe, Is.GreaterThanOrEqualTo(0), functionName + " observe");
            Assert.That(cleanup, Is.GreaterThan(observe), functionName + " cleanup ordering");
            Assert.That(
                failureReturn,
                Is.GreaterThan(cleanup),
                functionName + " must restore before returning Unity failure.");
        }

        private static void AssertVisualGuardRestoreOrdering(string runner)
        {
            const string functionName = "visual_guard_cleanup()";
            const string nextFunctionName = "visual_guard_handle_signal()";
            var functionStart = runner.IndexOf(functionName, StringComparison.Ordinal);
            var functionEnd = runner.IndexOf(
                nextFunctionName,
                functionStart + functionName.Length,
                StringComparison.Ordinal);
            Assert.That(functionStart, Is.GreaterThanOrEqualTo(0), functionName);
            Assert.That(functionEnd, Is.GreaterThan(functionStart), nextFunctionName);

            var functionBody = runner.Substring(functionStart, functionEnd - functionStart);
            var cleanupStarted = functionBody.IndexOf(
                "VISUAL_GUARD_CLEANUP_STARTED=1",
                StringComparison.Ordinal);
            var restore = functionBody.IndexOf(
                "restore_capture_assets_from_baseline",
                StringComparison.Ordinal);
            var cleanupCompleted = functionBody.IndexOf(
                "VISUAL_GUARD_CLEANUP_COMPLETED=1",
                StringComparison.Ordinal);

            Assert.That(restore, Is.GreaterThan(cleanupStarted), "restore starts inside cleanup");
            Assert.That(
                cleanupCompleted,
                Is.GreaterThan(restore),
                "cleanup completes only after guarded assets are restored.");
        }

        private static void AssertAudioValueLayouts(SettingsScreenView settings)
        {
            var audio = settings.AudioView;
            var targets = new[]
            {
                ("_mainRow", "SettingsSectionHost/SettingsAudioSection/MainAudioRow/Value"),
                ("_bgmRow", "SettingsSectionHost/SettingsAudioSection/BgmAudioRow/Value"),
                ("_sfxRow", "SettingsSectionHost/SettingsAudioSection/SfxAudioRow/Value"),
            };

            foreach (var target in targets)
            {
                var row = GetField<object>(audio, target.Item1);
                var valueProperty = row.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(valueProperty, Is.Not.Null, target.Item1);
                var value = valueProperty.GetValue(row) as TMP_Text;
                Assert.That(value, Is.Not.Null, target.Item1);
                var rect = value.rectTransform;
                var layoutElement = value.GetComponent<LayoutElement>();

                Assert.That(GetRelativePath(value.transform), Is.EqualTo(target.Item2));
                Assert.That(rect.sizeDelta.x, Is.EqualTo(140f), target.Item1);
                Assert.That(rect.sizeDelta.y, Is.EqualTo(32f), target.Item1);
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), target.Item1);
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)), target.Item1);
                Assert.That(rect.pivot, Is.EqualTo(new Vector2(0f, 1f)), target.Item1);
                Assert.That(layoutElement, Is.Not.Null, target.Item1);
                Assert.That(layoutElement.preferredWidth, Is.EqualTo(140f), target.Item1);
                Assert.That(layoutElement.preferredHeight, Is.EqualTo(32f), target.Item1);
            }
        }

        private static void AssertDisplayStatusLayout(SettingsScreenView settings)
        {
            var status = GetField<TMP_Text>(settings.DisplayView, "_displayStatusLabel");
            var layoutElement = status.GetComponent<LayoutElement>();
            var binding = TypographyBinding.FindFor(status);
            var koreanStyle = LoadTheme().ResolveOrThrow("ko-KR", TypographyStyleTag.SettingsStatus);

            Assert.That(
                GetRelativePath(status.transform),
                Is.EqualTo("SettingsSectionHost/SettingsDisplaySection/DisplayStatusRow/DisplayStatus"));
            Assert.That(status.rectTransform.sizeDelta.y, Is.EqualTo(28f));
            Assert.That(layoutElement, Is.Not.Null);
            Assert.That(layoutElement.preferredHeight, Is.EqualTo(28f));
            Assert.That(status.fontSize, Is.EqualTo(14f));
            Assert.That(status.enableAutoSizing, Is.True);
            Assert.That(status.fontSizeMin, Is.EqualTo(10f));
            Assert.That(status.fontSizeMax, Is.EqualTo(14f));
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.StyleTag, Is.EqualTo(TypographyStyleTag.SettingsStatus));
            Assert.That(binding.SizingSourceOverride, Is.EqualTo(TypographySizingSource.Hybrid));
            Assert.That(binding.UseApplyMaskOverride, Is.False);
            Assert.That(koreanStyle.SizingSource, Is.EqualTo(TypographySizingSource.Hybrid));
            Assert.That(koreanStyle.SizingMode, Is.EqualTo(TypographySizingMode.PreserveAuthored));
            Assert.That(koreanStyle.ApplyMask & TypographyApplyMask.Sizing, Is.EqualTo(TypographyApplyMask.None));
        }

        private static GameplayUiTypographyTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemeAssetPath);
            Assert.That(theme, Is.Not.Null, ThemeAssetPath);
            return theme;
        }

        private static TMP_FontAsset LoadClimateFont()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Assert.That(fontAsset, Is.Not.Null, FontAssetPath);
            return fontAsset;
        }

        private static StringTable LoadStringTable(string path)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(path);
            Assert.That(table, Is.Not.Null, path);
            return table;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            var value = field.GetValue(target);
            Assert.That(value, Is.InstanceOf<T>(), $"{target.GetType().Name}.{fieldName}");
            return (T)value;
        }

        private static string GetRelativePath(Transform target)
        {
            var segments = new Stack<string>();
            for (var current = target; current != null && current.parent != null; current = current.parent)
            {
                segments.Push(current.name);
            }

            return string.Join("/", segments);
        }

        private static void AssertAssetIdentity(UnityEngine.Object asset, string expectedGuid, long expectedLocalId, string context)
        {
            Assert.That(asset, Is.Not.Null, context);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId),
                Is.True,
                context);
            Assert.That(guid, Is.EqualTo(expectedGuid), context);
            Assert.That(localId, Is.EqualTo(expectedLocalId), context);
        }

        private static string Slice(string value, string startMarker, string endMarker)
        {
            var start = value.IndexOf(startMarker, StringComparison.Ordinal);
            var end = value.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return value.Substring(start + startMarker.Length, end - start - startMarker.Length);
        }

        private static string ComputeTextSha256(string value)
        {
            return ComputeSha256(Encoding.UTF8.GetBytes(value));
        }

        private static string ComputeSha256(byte[] value)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(value)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string FormatCharacters(IEnumerable<char> characters)
        {
            return string.Join(", ", characters.Select(character => $"{character} U+{(int)character:X4}"));
        }
    }
}
