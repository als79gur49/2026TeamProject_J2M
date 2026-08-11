using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Composition.Editor
{
    public static class ClimateCrisisKrGlyphUpdateUtility
    {
        private const string SourceFont2000Path =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000.ttf";
        private const string FontAsset2000Path =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";
        private const string SourceFont2019Path =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2019.ttf";
        private const string FontAsset2019Path =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2019 SDF.asset";
        private const string NanumSourceFontPath =
            "Assets/_Shared/UI/Fonts/NanumGothic.ttf";
        private const string NanumFontAssetPath =
            "Assets/_Shared/UI/Fonts/NanumGothic SDF.asset";

        [MenuItem("Tools/UI/Update Climate Crisis KR Managed Glyphs")]
        public static void GenerateFromMenu()
        {
            GenerateOrThrow();
        }

        public static void GenerateFromCommandLine()
        {
            GenerateOrThrow();
            EditorApplication.Exit(0);
        }

        public static TMP_FontAsset GenerateOrThrow()
        {
            var climate2000 = UpdateFontOrThrow(
                "Climate 2000",
                SourceFont2000Path,
                FontAsset2000Path,
                requireScaleRatios: true);
            UpdateFontOrThrow(
                "Climate 2019",
                SourceFont2019Path,
                FontAsset2019Path,
                requireScaleRatios: true);
            UpdateFontOrThrow(
                "Nanum",
                NanumSourceFontPath,
                NanumFontAssetPath,
                requireScaleRatios: false);
            Debug.Log(
                "Climate 2000/2019 and Nanum managed glyph update complete: " +
                "0 missing, 0 fallback.");
            Debug.Log(
                "GLYPH_UPDATE_VALIDATION missing=0 fallback=0 glyph_loss=0 glyph_remap=0 " +
                "atlas_page_drift=0 source_linkage=PASS scale_ratio=PASS");
            return climate2000;
        }

        private static TMP_FontAsset UpdateFontOrThrow(
            string label,
            string sourceFontPath,
            string fontAssetPath,
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

            var before = CaptureContractSnapshot(fontAsset);
            var requiredCharacters = BuildRequiredCharacterSet(fontAsset);
            var charactersToAdd = GetMissingCharacters(fontAsset, requiredCharacters);
            if (charactersToAdd.Length > 0)
            {
                AssignSourceFont(fontAsset, sourceFont);
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                if (!fontAsset.TryAddCharacters(charactersToAdd, out var missingCharacters))
                {
                    throw new InvalidOperationException(
                        $"{label} source TTF cannot supply managed glyphs: " +
                        FormatCharacters(missingCharacters));
                }
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            if (requireScaleRatios)
            {
                RestoreCanonicalClimateScaleRatios(fontAsset, label);
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
                fontAsset.characterTable.ToDictionary(
                    character => character.unicode,
                    character => character.glyphIndex),
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

            var afterCharacters = fontAsset.characterTable.ToDictionary(
                character => character.unicode,
                character => character.glyphIndex);
            var lostCharacters = before.UnicodeToGlyphIndex.Keys
                .Where(unicode => !afterCharacters.ContainsKey(unicode))
                .OrderBy(unicode => unicode)
                .ToArray();
            if (lostCharacters.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{label} glyph generation lost {lostCharacters.Length} existing characters.");
            }

            var remappedCharacters = before.UnicodeToGlyphIndex
                .Where(pair => afterCharacters[pair.Key] != pair.Value)
                .Select(pair => pair.Key)
                .OrderBy(unicode => unicode)
                .ToArray();
            if (remappedCharacters.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{label} glyph generation remapped {remappedCharacters.Length} existing characters.");
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

        private static string GetMissingCharacters(TMP_FontAsset fontAsset, string requiredCharacters)
        {
            return string.Concat(requiredCharacters.Where(
                character => !fontAsset.HasCharacter(character, false, false)));
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

        private static void ImportAndPersistCanonicalSerialization(string assetPath)
        {
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
        }

        private static void RestoreCanonicalClimateScaleRatios(TMP_FontAsset fontAsset, string label)
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
                IReadOnlyDictionary<uint, uint> unicodeToGlyphIndex,
                int atlasPageCount)
            {
                UnicodeToGlyphIndex = unicodeToGlyphIndex;
                AtlasPageCount = atlasPageCount;
            }

            public IReadOnlyDictionary<uint, uint> UnicodeToGlyphIndex { get; }

            public int AtlasPageCount { get; }
        }
    }
}
