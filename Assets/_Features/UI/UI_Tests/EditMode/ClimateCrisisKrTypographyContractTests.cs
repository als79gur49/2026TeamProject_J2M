using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Tests
{
    public sealed class ClimateCrisisKrTypographyContractTests
    {
        private const string ThemeAssetPath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";
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
            Assert.That(material.GetFloat("_ScaleRatioC"), Is.EqualTo(1f));
            Assert.That(ComputeSha256(SourceFontPath), Is.EqualTo(SourceFontSha256));
            Assert.That(ComputeSha256(FontAssetPath), Is.EqualTo(CommittedSdfSha256));
        }

        [Test]
        public void ClimateSdf_NativelyCoversManagedKoreanStringTables()
        {
            var fontAsset = LoadClimateFont();
            var tables = new[]
            {
                LoadStringTable(UiKoreanStringTablePath),
                LoadStringTable(StageKoreanStringTablePath),
            };
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

            Assert.That(values, Has.Length.EqualTo(66));
            Assert.That(values.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(65));
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

            Assert.That(roles, Has.Length.EqualTo(19));
            Assert.That(theme.BaseRules, Has.Count.EqualTo(19));
            Assert.That(theme.BaseRules.Select(rule => rule.StyleTag), Is.EqualTo(roles));
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
