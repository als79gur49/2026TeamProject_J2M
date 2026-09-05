using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Composition.Editor
{
    public static class KboDiaGothicGlyphUpdateUtility
    {
        private const string SourceFontMediumPath =
            "Assets/_Shared/UI/Fonts/KBODiaGothic-Medium.ttf";
        private const string FontAssetMediumPath =
            "Assets/_Shared/UI/Fonts/KBODiaGothic-Medium SDF.asset";
        private const string SourceFontLightPath =
            "Assets/_Shared/UI/Fonts/KBODiaGothic-Light.ttf";
        private const string FontAssetLightPath =
            "Assets/_Shared/UI/Fonts/KBODiaGothic-Light SDF.asset";

        public static void GenerateFromCommandLine()
        {
            GenerateOrThrow();
            EditorApplication.Exit(0);
        }

        public static TMP_FontAsset GenerateOrThrow()
        {
            var medium = UpdateFontOrThrow(
                "KBO Dia Gothic Medium",
                SourceFontMediumPath,
                FontAssetMediumPath,
                "KBODiaGothic-Medium SDF",
                requireScaleRatios: true);
            UpdateFontOrThrow(
                "KBO Dia Gothic Light",
                SourceFontLightPath,
                FontAssetLightPath,
                "KBODiaGothic-Light SDF",
                requireScaleRatios: true);
            Debug.Log(
                "KBO Dia Gothic Medium/Light managed glyph update complete: " +
                "0 missing, 0 fallback.");
            Debug.Log(
                "GLYPH_UPDATE_VALIDATION missing=0 fallback=0 glyph_loss=0 " +
                "atlas_page_drift=0 source_linkage=PASS scale_ratio=PASS");
            return medium;
        }

        private static TMP_FontAsset UpdateFontOrThrow(
            string label,
            string sourceFontPath,
            string fontAssetPath,
            string canonicalAssetName,
            bool requireScaleRatios)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException(
                    $"Missing {label} source font: {sourceFontPath}");
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (fontAsset == null)
            {
                throw new InvalidOperationException(
                    $"Missing {label} TMP font asset: {fontAssetPath}");
            }

            var requiredCharacters = BuildRequiredCharacterSet(fontAsset);
            var before = CaptureContractSnapshot(fontAsset);

            AssignSourceFont(fontAsset, sourceFont);
            RefreshFontMetadata(fontAsset, sourceFont, requiredCharacters);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.ClearFontAssetData();
            if (!fontAsset.TryAddCharacters(requiredCharacters, out var missingCharacters))
            {
                throw new InvalidOperationException(
                    $"{label} source TTF cannot supply managed glyphs: " +
                    FormatCharacters(missingCharacters));
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            RenameFontSubAssets(fontAsset, canonicalAssetName);
            if (requireScaleRatios)
            {
                RestoreCanonicalScaleRatios(fontAsset, label);
            }
            fontAsset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            ImportAndPersistCanonicalSerialization(fontAssetPath);

            var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            ValidateContractOrThrow(
                label,
                reloaded,
                sourceFont,
                before,
                requireScaleRatios);
            return reloaded;
        }

        private static FontContractSnapshot CaptureContractSnapshot(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                throw new ArgumentNullException(nameof(fontAsset));
            }

            return new FontContractSnapshot(
                fontAsset.characterTable
                    .Select(character => character.unicode)
                    .ToHashSet(),
                fontAsset.atlasTextures?.Length ?? 0);
        }

        private static void ValidateContractOrThrow(
            string label,
            TMP_FontAsset fontAsset,
            Font expectedSourceFont,
            FontContractSnapshot before,
            bool requireScaleRatios)
        {
            if (fontAsset == null)
            {
                throw new InvalidOperationException($"{label} TMP font asset could not be reloaded.");
            }

            var missingCoverage = LoadManagedKoreanStrings()
                .SelectMany(value => value)
                .Where(character => character > 0x7f)
                .Distinct()
                .Where(character => !fontAsset.HasCharacter(character, false, false))
                .OrderBy(character => character)
                .ToArray();
            if (missingCoverage.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{label} asset is missing managed glyphs: {FormatCharacters(missingCoverage)}");
            }

            if (fontAsset.fallbackFontAssetTable.Count != 0)
            {
                throw new InvalidOperationException(
                    $"{label} glyph generation must not add fallback font assets.");
            }

            var afterCharacters = fontAsset.characterTable
                .Select(character => character.unicode)
                .ToHashSet();
            var lostCharacters = before.UnicodeCharacters
                .Where(unicode => !afterCharacters.Contains(unicode))
                .OrderBy(unicode => unicode)
                .ToArray();
            if (lostCharacters.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{label} glyph generation lost {lostCharacters.Length} existing characters.");
            }

            var atlasTextures = fontAsset.atlasTextures ?? Array.Empty<Texture2D>();
            if (atlasTextures.Length != before.AtlasPageCount || atlasTextures.Any(texture => texture == null))
            {
                throw new InvalidOperationException(
                    $"{label} atlas page contract changed from {before.AtlasPageCount} to " +
                    $"{atlasTextures.Length}, or contains a missing page.");
            }

            var serializedFont = new SerializedObject(fontAsset);
            var sourceGuidProperty = serializedFont.FindProperty("m_SourceFontFileGUID");
            var expectedSourceGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(expectedSourceFont));
            if (sourceGuidProperty == null ||
                !string.Equals(
                    sourceGuidProperty.stringValue,
                    expectedSourceGuid,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{label} TMP font asset lost its canonical source font linkage.");
            }

            if (!requireScaleRatios)
            {
                return;
            }

            var material = fontAsset.material;
            if (material == null ||
                !material.HasProperty("_ScaleRatioA") ||
                !material.HasProperty("_ScaleRatioC") ||
                !Mathf.Approximately(material.GetFloat("_ScaleRatioA"), 1f) ||
                !Mathf.Approximately(material.GetFloat("_ScaleRatioC"), 1f))
            {
                throw new InvalidOperationException(
                    $"{label} canonical material must retain ScaleRatioA = 1 and ScaleRatioC = 1.");
            }
        }

        private static string BuildRequiredCharacterSet(TMP_FontAsset fontAsset)
        {
            var characters = new SortedSet<char>(
                fontAsset.characterTable
                    .Where(character => character.unicode <= char.MaxValue)
                    .Select(character => (char)character.unicode));
            foreach (var value in LoadManagedKoreanStrings())
            {
                foreach (var character in value)
                {
                    if (!char.IsControl(character))
                    {
                        characters.Add(character);
                    }
                }
            }

            return string.Concat(characters);
        }

        private static void AssignSourceFont(TMP_FontAsset fontAsset, Font sourceFont)
        {
            var serializedFont = new SerializedObject(fontAsset);
            var sourceFontProperty = serializedFont.FindProperty("m_SourceFontFile");
            if (sourceFontProperty == null)
            {
                throw new InvalidOperationException("TMP font asset does not expose m_SourceFontFile.");
            }

            sourceFontProperty.objectReferenceValue = sourceFont;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RefreshFontMetadata(
            TMP_FontAsset fontAsset,
            Font sourceFont,
            string requiredCharacters)
        {
            var template = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                Mathf.RoundToInt(fontAsset.faceInfo.pointSize),
                fontAsset.atlasPadding,
                fontAsset.atlasRenderMode,
                fontAsset.atlasWidth,
                fontAsset.atlasHeight,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: false);
            if (template == null)
            {
                throw new InvalidOperationException(
                    $"Unable to read font metadata from {AssetDatabase.GetAssetPath(sourceFont)}.");
            }

            try
            {
                fontAsset.faceInfo = template.faceInfo;
                var creationSettings = template.creationSettings;
                var sourceFontPath = AssetDatabase.GetAssetPath(sourceFont);
                creationSettings.sourceFontFileName = Path.GetFileName(sourceFontPath);
                creationSettings.sourceFontFileGUID = AssetDatabase.AssetPathToGUID(sourceFontPath);
                creationSettings.faceIndex = 0;
                creationSettings.pointSizeSamplingMode = 1;
                creationSettings.pointSize = Mathf.RoundToInt(fontAsset.faceInfo.pointSize);
                creationSettings.padding = fontAsset.atlasPadding;
                creationSettings.paddingMode = 2;
                creationSettings.packingMode = 0;
                creationSettings.atlasWidth = fontAsset.atlasWidth;
                creationSettings.atlasHeight = fontAsset.atlasHeight;
                creationSettings.characterSetSelectionMode = 7;
                creationSettings.characterSequence = requiredCharacters;
                creationSettings.referencedFontAssetGUID = string.Empty;
                creationSettings.referencedTextAssetGUID = string.Empty;
                creationSettings.fontStyle = 0;
                creationSettings.fontStyleModifier = 0;
                creationSettings.renderMode = (int)fontAsset.atlasRenderMode;
                creationSettings.includeFontFeatures = false;
                fontAsset.creationSettings = creationSettings;
            }
            finally
            {
                var templateMaterial = template.material;
                var templateAtlases = template.atlasTextures ?? Array.Empty<Texture2D>();
                UnityEngine.Object.DestroyImmediate(template);
                if (templateMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(templateMaterial);
                }

                foreach (var atlas in templateAtlases)
                {
                    if (atlas != null)
                    {
                        UnityEngine.Object.DestroyImmediate(atlas);
                    }
                }
            }
        }

        private static void ImportAndPersistCanonicalSerialization(string assetPath)
        {
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
        }

        private static void RenameFontSubAssets(TMP_FontAsset fontAsset, string canonicalAssetName)
        {
            fontAsset.name = canonicalAssetName;
            if (fontAsset.material != null)
            {
                fontAsset.material.name = canonicalAssetName + " Material";
                EditorUtility.SetDirty(fontAsset.material);
            }

            var atlasTextures = fontAsset.atlasTextures ?? Array.Empty<Texture2D>();
            for (var index = 0; index < atlasTextures.Length; index++)
            {
                if (atlasTextures[index] == null)
                {
                    continue;
                }

                atlasTextures[index].name = index == 0
                    ? canonicalAssetName + " Atlas"
                    : canonicalAssetName + " Atlas " + index;
                EditorUtility.SetDirty(atlasTextures[index]);
            }
        }

        private static void RestoreCanonicalScaleRatios(TMP_FontAsset fontAsset, string label)
        {
            var material = fontAsset.material;
            if (material == null ||
                !material.HasProperty("_ScaleRatioA") ||
                !material.HasProperty("_ScaleRatioC"))
            {
                throw new InvalidOperationException(
                    $"{label} canonical material is missing its TMP scale-ratio properties.");
            }

            material.SetFloat("_ScaleRatioA", 1f);
            material.SetFloat("_ScaleRatioC", 1f);
            EditorUtility.SetDirty(material);
        }

        private static IEnumerable<string> LoadManagedKoreanStrings()
        {
            var paths = AssetDatabase
                .FindAssets("t:StringTable", new[] { "Assets/Localization/StringTables" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith("_ko-KR.asset", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>(path);
                if (table == null)
                {
                    throw new InvalidOperationException($"Unable to load managed Korean String Table: {path}");
                }

                foreach (var sharedEntry in table.SharedData.Entries)
                {
                    var value = table.GetEntry(sharedEntry.Key)?.LocalizedValue;
                    if (!string.IsNullOrEmpty(value))
                    {
                        yield return value;
                    }
                }
            }
        }

        private static string FormatCharacters(IEnumerable<char> characters)
        {
            return string.Join(
                ", ",
                characters.Select(character => $"{character} (U+{(int)character:X4})"));
        }

        private sealed class FontContractSnapshot
        {
            public FontContractSnapshot(
                IReadOnlyCollection<uint> unicodeCharacters,
                int atlasPageCount)
            {
                UnicodeCharacters = unicodeCharacters;
                AtlasPageCount = atlasPageCount;
            }

            public IReadOnlyCollection<uint> UnicodeCharacters { get; }

            public int AtlasPageCount { get; }
        }
    }
}
