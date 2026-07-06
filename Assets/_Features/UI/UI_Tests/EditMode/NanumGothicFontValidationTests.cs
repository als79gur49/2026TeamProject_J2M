using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Feature.UI.Tests
{
    public sealed class NanumGothicFontValidationTests
    {
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string UiApplicationRuntimePath = "Assets/_Features/UI/UI_Application/Runtime";
        private const string UiViewSharedRuntimePath = "Assets/_Features/UI/UI_ViewShared/Runtime";

        private static readonly IReadOnlyDictionary<string, string> SettingsKoreanLabelsByKey =
            new Dictionary<string, string>
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
                ["ui.common.back"] = "뒤로",
            };

        [Test]
        public void NanumGothicSourceFont_IsAvailableForTmpGeneration()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(NanumGothicFontValidationUtility.SourceFontPath);
            var importer = AssetImporter.GetAtPath(NanumGothicFontValidationUtility.SourceFontPath) as TrueTypeFontImporter;

            Assert.That(sourceFont, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.includeFontData, Is.True);
            Assert.That(importer.fontNames, Does.Contain("NanumGothic"));
        }

        [Test]
        public void NanumGothicSdfAsset_UsesValidationGenerationPolicy()
        {
            var fontAsset = LoadFontAsset();
            var fontAssetYaml = File.ReadAllText(NanumGothicFontValidationUtility.FontAssetPath);
            var sourceFontGuid = AssetDatabase.AssetPathToGUID(NanumGothicFontValidationUtility.SourceFontPath);

            Assert.That(
                fontAssetYaml,
                Does.Contain($"m_SourceFontFileGUID: {sourceFontGuid}"),
                "The static validation asset must keep a serialized link to NanumGothic.ttf.");
            Assert.That(fontAsset.atlasWidth, Is.EqualTo(NanumGothicFontValidationUtility.AtlasSize));
            Assert.That(fontAsset.atlasHeight, Is.EqualTo(NanumGothicFontValidationUtility.AtlasSize));
            Assert.That(fontAsset.atlasPadding, Is.EqualTo(NanumGothicFontValidationUtility.Padding));
            Assert.That(fontAsset.atlasRenderMode, Is.EqualTo(GlyphRenderMode.SDFAA));
            Assert.That(fontAsset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(fontAsset.isMultiAtlasTexturesEnabled, Is.False);
            Assert.That(fontAsset.material, Is.Not.Null);
            Assert.That(fontAsset.fallbackFontAssetTable, Is.Empty);
        }

        [Test]
        public void NanumGothicSdfAsset_CoversSettingsKoreanLabelsWithoutFallback()
        {
            var fontAsset = LoadFontAsset();
            var failures = new List<string>();

            foreach (var entry in SettingsKoreanLabelsByKey)
            {
                var missing = NanumGothicFontValidationUtility.GetMissingCharacters(
                    fontAsset,
                    new[] { entry.Value });

                if (missing.Count > 0)
                {
                    failures.Add($"{entry.Key} '{entry.Value}' => {NanumGothicFontValidationUtility.FormatCharacters(missing)}");
                }
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void NanumGothicSdfAsset_CoversCommonSettingsUiSymbols()
        {
            var fontAsset = LoadFontAsset();
            var missing = NanumGothicFontValidationUtility.GetMissingCharacters(
                fontAsset,
                new[] { NanumGothicFontValidationUtility.CommonUiCharacters });

            Assert.That(
                missing,
                Is.Empty,
                NanumGothicFontValidationUtility.FormatCharacters(missing));
        }

        [Test]
        public void ProjectTmpSettings_GlobalFallbackDoesNotIncludeNanumGothicValidationAsset()
        {
            var fontAssetGuid = AssetDatabase.AssetPathToGUID(NanumGothicFontValidationUtility.FontAssetPath);
            var settingsYaml = File.ReadAllText(TmpSettingsPath);

            Assert.That(settingsYaml, Does.Contain("m_fallbackFontAssets: []"));
            Assert.That(settingsYaml, Does.Not.Contain(fontAssetGuid));
        }

        [Test]
        public void LowerUiContracts_DoNotReferenceTmpFontAsset()
        {
            AssertNoRuntimeSourceContains(UiApplicationRuntimePath, "TMP_FontAsset");
            AssertNoRuntimeSourceContains(UiViewSharedRuntimePath, "TMP_FontAsset");
        }

        [Test]
        public void SettingsStaticDescriptorKeys_MatchFontCoverageMap()
        {
            var descriptorKeys = new[]
            {
                SettingsStaticTextDescriptors.Title.Key,
                SettingsStaticTextDescriptors.AudioTab.Key,
                SettingsStaticTextDescriptors.DisplayTab.Key,
                SettingsStaticTextDescriptors.InputTab.Key,
                SettingsStaticTextDescriptors.MovementKeys.Key,
                SettingsStaticTextDescriptors.UseArrowKeys.Key,
                SettingsStaticTextDescriptors.Push.Key,
                SettingsStaticTextDescriptors.Flip.Key,
                SettingsStaticTextDescriptors.Change.Key,
                SettingsStaticTextDescriptors.ResetInput.Key,
                SettingsStaticTextDescriptors.Back.Key,
            };

            Assert.That(SettingsKoreanLabelsByKey.Keys, Is.EquivalentTo(descriptorKeys));
        }

        private static TMP_FontAsset LoadFontAsset()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumGothicFontValidationUtility.FontAssetPath);
            Assert.That(
                fontAsset,
                Is.Not.Null,
                $"Generate {NanumGothicFontValidationUtility.FontAssetPath} with {nameof(NanumGothicFontValidationUtility)} first.");
            return fontAsset;
        }

        private static void AssertNoRuntimeSourceContains(string rootPath, string token)
        {
            var hits = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(hits, Is.Empty, $"{token} leaked into {rootPath}: {string.Join(", ", hits)}");
        }
    }
}
