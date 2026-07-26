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
        private const string SourceFontSha256 =
            "aa0e58ef1dd54ae760c29bdd0ce28d6b710c2d5910e88efadf5e23416b01d0f1";
        private const string CommittedSdfSha256 =
            "c22ee5c03ebbe4f55322cf75b80acb7891173a5580ea56ef7b2f72c50f8431d5";
        private const string EnglishContractSha256 =
            "3def0381e783b5bd7286e824a6fcea11dddb659ed5a3aab667a239d070868f92";

        [Test]
        public void ClimateAssets_KeepCommittedIdentityAndCanonicalMaterial()
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
            Assert.That(material.GetFloat("_ScaleRatioA"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_ScaleRatioB"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_ScaleRatioC"), Is.EqualTo(1f));
            Assert.That(ComputeSha256(SourceFontPath), Is.EqualTo(SourceFontSha256));
            Assert.That(ComputeSha256(FontAssetPath), Is.EqualTo(CommittedSdfSha256));
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
            Assert.That(values, Has.Length.EqualTo(70));
            Assert.That(values.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(69));
            Assert.That(values, Does.Contain("밀기 키 입력하세요..."));
            Assert.That(values, Does.Contain("뒤집기 키 입력하세요..."));
            Assert.That(codepoints, Has.Length.EqualTo(116));
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
            var paths = new[]
            {
                "Assets/_Shared/UI/Fonts/NanumGothic.ttf",
                "Assets/_Shared/UI/Fonts/NanumGothic.ttf.meta",
                "Assets/_Shared/UI/Fonts/NanumGothic SDF.asset",
                "Assets/_Shared/UI/Fonts/NanumGothic SDF.asset.meta",
                "Assets/_Features/UI/UI_Composition/Authoring/Typography/NanumGothic SDF SyntheticBold.mat",
                "Assets/_Features/UI/UI_Composition/Authoring/Typography/NanumGothic SDF SyntheticBold.mat.meta",
            };

            Assert.That(paths.Where(path => !File.Exists(path)), Is.Empty);
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

        private static string ComputeSha256(string path)
        {
            return ComputeSha256(File.ReadAllBytes(path));
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
