using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Composition.Editor
{
    public static class KboDiaGothicGlyphUpdateUtility
    {
        private const string SourceFontMediumPath =
            "Assets/_Shared/UI/Fonts/KBODiaGothic-Medium.ttf";
        private const string FontAssetMediumPath =
            "Assets/_Shared/UI/Fonts/KBODiaGothic-Medium SDF.asset";
        private const string FontAssetMediumGuid = "40d61154fd6576b4d85c2d78460b16ad";
        private const long FontAssetLocalId = 11400000;
        private const long MaterialMediumLocalId = 1352911973252649374;
        private const long AtlasMediumLocalId = -2536001923755311345;
        private const string SourceFontMediumSha256 =
            "f88f06494fc4eb8fd06e15c1f6deacfa8d7855c9a4245d71962a90596ad41f02";
        private const string SourceFontLightPath =
            "Assets/_Shared/UI/Fonts/KBODiaGothic-Light.ttf";
        private const string FontAssetLightPath =
            "Assets/_Shared/UI/Fonts/KBODiaGothic-Light SDF.asset";
        private const string FontAssetLightGuid = "7dfd9aae81fc1d242b007a3b7a042fb0";
        private const long MaterialLightLocalId = 7808543287137721147;
        private const long AtlasLightLocalId = -5757234995057936259;
        private const string SourceFontLightSha256 =
            "607c0a894ea951489bd43f6a3ccc93adececbb46c425ccc5869f2327dbcfe747";
        private const string UiKoreanStringTablePath =
            "Assets/Localization/StringTables/UI/UI_ko-KR.asset";
        private const string StageKoreanStringTablePath =
            "Assets/Localization/StringTables/Stage/Stage_ko-KR.asset";
        private const string KoreanAutonym = "한국어";
        private const uint MissingGlyphMarker = 0x25a1;

        private static readonly (string CollectionName, string AssetPath)[] KoreanStringTables =
        {
            ("UI", UiKoreanStringTablePath),
            ("Stage", StageKoreanStringTablePath),
        };

        public static void GenerateFromCommandLine()
        {
            GenerateOrThrow();
            EditorApplication.Exit(0);
        }

        public static TMP_FontAsset GenerateOrThrow()
        {
            var plans = BuildPreflightPlansOrThrow();
            RebuildPlansInPlaceOrThrow(plans);
            AssetDatabase.SaveAssets();
            foreach (var plan in plans)
            {
                ImportCanonicalSerialization(plan.FontAssetPath);
            }

            AssetDatabase.SaveAssets();
            foreach (var plan in plans)
            {
                var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(plan.FontAssetPath);
                ValidateContractOrThrow(
                    plan.Label,
                    reloaded,
                    plan.SourceFont,
                    plan.Before,
                    plan.ExactCorpus,
                    requireScaleRatios: true);
            }

            Debug.Log(
                "KBO Dia Gothic Medium/Light exact-corpus rebuild complete: " +
                "0 missing, 0 fallback.");
            Debug.Log(
                "GLYPH_UPDATE_VALIDATION missing=0 fallback=0 exact_corpus=PASS " +
                "atlas_page_drift=0 identity=PASS source_linkage=PASS scale_ratio=PASS");
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetMediumPath);
        }

        public static void ValidateProductionApplyPreflightOrThrow()
        {
            BuildPreflightPlansOrThrow();
        }

        public static void ValidatePersistedProductionAssetsOrThrow()
        {
            var plans = BuildPreflightPlansOrThrow();
            foreach (var plan in plans)
            {
                var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(plan.FontAssetPath);
                ValidateContractOrThrow(
                    plan.Label,
                    reloaded,
                    plan.SourceFont,
                    plan.Before,
                    plan.ExactCorpus,
                    requireScaleRatios: true);
            }
        }

        public static TMP_FontAsset RebuildForProductionApplyOrThrow()
        {
            var plans = BuildPreflightPlansOrThrow();
            RebuildPlansInPlaceOrThrow(plans.Where(NeedsExactCorpusRebuild));
            return plans[0].FontAsset;
        }

        private static bool NeedsExactCorpusRebuild(FontRebuildPlan plan)
        {
            return !new HashSet<uint>(
                    plan.FontAsset.characterTable.Select(character => character.unicode))
                .SetEquals(plan.ExactCorpus);
        }

        private static FontRebuildPlan[] BuildPreflightPlansOrThrow()
        {
            var exactCorpus = BuildExactKoreanCorpusOrThrow();
            var plans = new[]
            {
                BuildPreflightPlanOrThrow(
                    "KBO Dia Gothic Medium",
                    SourceFontMediumPath,
                    SourceFontMediumSha256,
                    FontAssetMediumPath,
                    FontAssetMediumGuid,
                    MaterialMediumLocalId,
                    AtlasMediumLocalId,
                    "KBODiaGothic-Medium SDF",
                    exactCorpus),
                BuildPreflightPlanOrThrow(
                    "KBO Dia Gothic Light",
                    SourceFontLightPath,
                    SourceFontLightSha256,
                    FontAssetLightPath,
                    FontAssetLightGuid,
                    MaterialLightLocalId,
                    AtlasLightLocalId,
                    "KBODiaGothic-Light SDF",
                    exactCorpus),
            };

            if (!plans[0].ExactCorpus.SequenceEqual(plans[1].ExactCorpus))
            {
                throw new InvalidOperationException(
                    "KBO Dia Gothic Medium and Light must share one exact Korean corpus.");
            }

            return plans;
        }

        private static FontRebuildPlan BuildPreflightPlanOrThrow(
            string label,
            string sourceFontPath,
            string expectedSourceSha256,
            string fontAssetPath,
            string expectedAssetGuid,
            long expectedMaterialLocalId,
            long expectedAtlasLocalId,
            string canonicalAssetName,
            uint[] exactCorpus)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException(
                    $"Missing {label} source font: {sourceFontPath}");
            }
            ValidateSourceFontHashOrThrow(sourceFontPath, expectedSourceSha256, label);

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (fontAsset == null)
            {
                throw new InvalidOperationException(
                    $"Missing {label} TMP font asset: {fontAssetPath}");
            }

            var before = CaptureContractSnapshot(fontAsset);
            ValidateCanonicalIdentityGraphOrThrow(
                label,
                fontAssetPath,
                before,
                expectedAssetGuid,
                expectedMaterialLocalId,
                expectedAtlasLocalId);
            ValidateExistingSourceLinkageOrThrow(fontAsset, sourceFont, label);
            ValidatePreMutationStaticContractOrThrow(fontAsset, label);
            ValidateCanonicalScaleRatiosOrThrow(fontAsset, label);
            ValidateCorpusSupplyAndSingleAtlasFitOrThrow(
                label,
                sourceFont,
                fontAsset,
                exactCorpus);

            return new FontRebuildPlan(
                label,
                fontAssetPath,
                canonicalAssetName,
                sourceFont,
                fontAsset,
                exactCorpus,
                before);
        }

        private static void ValidateSourceFontHashOrThrow(
            string sourceFontPath,
            string expectedSha256,
            string label)
        {
            using (var stream = File.OpenRead(Path.GetFullPath(sourceFontPath)))
            using (var sha256 = SHA256.Create())
            {
                var actualSha256 = string.Concat(
                    sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{label} source font hash mismatch. Expected {expectedSha256}, actual {actualSha256}.");
                }
            }
        }

        private static void ValidatePreMutationStaticContractOrThrow(
            TMP_FontAsset fontAsset,
            string label)
        {
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Static ||
                fontAsset.isMultiAtlasTexturesEnabled ||
                fontAsset.fallbackFontAssetTable == null ||
                fontAsset.fallbackFontAssetTable.Count != 0)
            {
                throw new InvalidOperationException(
                    $"{label} must satisfy the Static, single-atlas, no-fallback contract before rebuild.");
            }

            var atlas = fontAsset.atlasTextures[0];
            if (atlas.isReadable)
            {
                throw new InvalidOperationException(
                    $"{label} shipping atlas must be non-readable before rebuild.");
            }
        }

        private static void RebuildPlansInPlaceOrThrow(IEnumerable<FontRebuildPlan> plans)
        {
            foreach (var plan in plans)
            {
                RebuildFontInPlaceOrThrow(plan);
            }
        }

        private static void RebuildFontInPlaceOrThrow(FontRebuildPlan plan)
        {
            var requiredCharacters = BuildRequiredCharacterSet(plan.ExactCorpus);

            AssignSourceFont(plan.FontAsset, plan.SourceFont);
            RefreshFontMetadata(plan.FontAsset, plan.SourceFont, requiredCharacters);
            plan.FontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            plan.FontAsset.ClearFontAssetData();
            if (!plan.FontAsset.TryAddCharacters(requiredCharacters, out var missingCharacters))
            {
                throw new InvalidOperationException(
                    $"{plan.Label} source TTF cannot supply managed glyphs: " +
                    FormatCharacters(missingCharacters));
            }

            plan.FontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            plan.FontAsset.isMultiAtlasTexturesEnabled = false;
            plan.FontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            RenameFontSubAssets(plan.FontAsset, plan.CanonicalAssetName);
            ApplyCanonicalScaleRatios(plan.FontAsset, plan.Label);
            plan.FontAsset.ReadFontAssetDefinition();
            MakeAtlasNonReadableOrThrow(plan.FontAsset, plan.Label);
            EditorUtility.SetDirty(plan.FontAsset);
            ValidateContractOrThrow(
                plan.Label,
                plan.FontAsset,
                plan.SourceFont,
                plan.Before,
                plan.ExactCorpus,
                requireScaleRatios: true);
        }

        private static FontContractSnapshot CaptureContractSnapshot(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                throw new ArgumentNullException(nameof(fontAsset));
            }

            var atlasTextures = fontAsset.atlasTextures ?? Array.Empty<Texture2D>();
            if (atlasTextures.Length != 1 || atlasTextures[0] == null)
            {
                throw new InvalidOperationException(
                    $"{fontAsset.name} must have exactly one canonical atlas before regeneration.");
            }

            return new FontContractSnapshot(
                CaptureAssetIdentity(fontAsset, "TMP font asset"),
                CaptureAssetIdentity(fontAsset.material, "canonical material"),
                CaptureAssetIdentity(atlasTextures[0], "canonical atlas"),
                atlasTextures.Length);
        }

        private static void ValidateCanonicalIdentityGraphOrThrow(
            string label,
            string fontAssetPath,
            FontContractSnapshot snapshot,
            string expectedAssetGuid,
            long expectedMaterialLocalId,
            long expectedAtlasLocalId)
        {
            var expectedGuid = AssetDatabase.AssetPathToGUID(fontAssetPath);
            var identities = new[]
            {
                snapshot.FontAssetIdentity,
                snapshot.MaterialIdentity,
                snapshot.AtlasIdentity,
            };
            if (string.IsNullOrEmpty(expectedGuid) ||
                !string.Equals(expectedGuid, expectedAssetGuid, StringComparison.Ordinal) ||
                identities.Any(identity =>
                    !string.Equals(identity.Guid, expectedGuid, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"{label} font, material, and atlas must remain sub-assets of '{fontAssetPath}'.");
            }

            if (snapshot.FontAssetIdentity.LocalId != FontAssetLocalId ||
                snapshot.MaterialIdentity.LocalId != expectedMaterialLocalId ||
                snapshot.AtlasIdentity.LocalId != expectedAtlasLocalId)
            {
                throw new InvalidOperationException(
                    $"{label} persistent local IDs drifted from the canonical production contract.");
            }

            if (identities.Select(identity => identity.LocalId).Distinct().Count() != identities.Length)
            {
                throw new InvalidOperationException(
                    $"{label} font, material, and atlas must retain distinct persistent local IDs.");
            }
        }

        private static void ValidateExistingSourceLinkageOrThrow(
            TMP_FontAsset fontAsset,
            Font expectedSourceFont,
            string label)
        {
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
                    $"{label} TMP font asset is not linked to its canonical source font.");
            }
        }

        private static void ValidateCorpusSupplyAndSingleAtlasFitOrThrow(
            string label,
            Font sourceFont,
            TMP_FontAsset productionFont,
            IReadOnlyCollection<uint> exactCorpus)
        {
            if (exactCorpus == null || exactCorpus.Count == 0)
            {
                throw new InvalidOperationException($"{label} governed corpus is empty.");
            }

            var template = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                Mathf.RoundToInt(productionFont.faceInfo.pointSize),
                productionFont.atlasPadding,
                productionFont.atlasRenderMode,
                productionFont.atlasWidth,
                productionFont.atlasHeight,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: false);
            if (template == null)
            {
                throw new InvalidOperationException(
                    $"{label} source font could not create a read-only preflight asset.");
            }

            try
            {
                var requiredCharacters = BuildRequiredCharacterSet(exactCorpus);
                if (!template.TryAddCharacters(requiredCharacters, out var missingCharacters))
                {
                    throw new InvalidOperationException(
                        $"{label} source or single-atlas budget cannot supply the governed corpus: " +
                        FormatCharacters(missingCharacters));
                }

                var atlasTextures = template.atlasTextures ?? Array.Empty<Texture2D>();
                if (atlasTextures.Length != 1 || atlasTextures[0] == null)
                {
                    throw new InvalidOperationException(
                        $"{label} governed corpus does not produce exactly one atlas in preflight.");
                }
            }
            finally
            {
                DestroyTemporaryFontAsset(template);
            }
        }

        private static void ValidateContractOrThrow(
            string label,
            TMP_FontAsset fontAsset,
            Font expectedSourceFont,
            FontContractSnapshot before,
            IReadOnlyCollection<uint> exactCorpus,
            bool requireScaleRatios)
        {
            if (fontAsset == null)
            {
                throw new InvalidOperationException($"{label} TMP font asset could not be reloaded.");
            }

            var actualCorpus = fontAsset.characterTable
                .Select(character => character.unicode)
                .ToHashSet();
            var expectedCorpus = exactCorpus.ToHashSet();
            if (!actualCorpus.SetEquals(expectedCorpus))
            {
                var missing = expectedCorpus.Except(actualCorpus).OrderBy(value => value);
                var unexpected = actualCorpus.Except(expectedCorpus).OrderBy(value => value);
                throw new InvalidOperationException(
                    $"{label} asset does not exactly match the governed Korean corpus. " +
                    $"Missing: {FormatCodePoints(missing)}. " +
                    $"Unexpected: {FormatCodePoints(unexpected)}.");
            }

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Static)
            {
                throw new InvalidOperationException(
                    $"{label} must remain a Static TMP font asset.");
            }

            if (fontAsset.isMultiAtlasTexturesEnabled)
            {
                throw new InvalidOperationException(
                    $"{label} must keep multi-atlas support disabled.");
            }

            if (fontAsset.fallbackFontAssetTable == null || fontAsset.fallbackFontAssetTable.Count != 0)
            {
                throw new InvalidOperationException(
                    $"{label} glyph generation must not add fallback font assets.");
            }

            var atlasTextures = fontAsset.atlasTextures ?? Array.Empty<Texture2D>();
            if (atlasTextures.Length != before.AtlasPageCount || atlasTextures.Any(texture => texture == null))
            {
                throw new InvalidOperationException(
                    $"{label} atlas page contract changed from {before.AtlasPageCount} to " +
                    $"{atlasTextures.Length}, or contains a missing page.");
            }
            if (atlasTextures.Any(texture => texture.isReadable))
            {
                throw new InvalidOperationException(
                    $"{label} shipping atlas textures must be non-readable.");
            }

            ValidateAssetIdentity(fontAsset, before.FontAssetIdentity, $"{label} TMP font asset");
            ValidateAssetIdentity(fontAsset.material, before.MaterialIdentity, $"{label} canonical material");
            ValidateAssetIdentity(atlasTextures[0], before.AtlasIdentity, $"{label} canonical atlas");

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

            ValidateCanonicalScaleRatiosOrThrow(fontAsset, label);
        }

        private static void ValidateCanonicalScaleRatiosOrThrow(TMP_FontAsset fontAsset, string label)
        {
            var material = RequireScaleRatioMaterial(fontAsset, label);
            // Compute on a copy so validation cannot silently repair the source material.
            var expected = new Material(material);
            try
            {
                ShaderUtilities.GetShaderPropertyIDs();
                ShaderUtilities.UpdateShaderRatios(expected);
                if (!Mathf.Approximately(material.GetFloat("_ScaleRatioA"), expected.GetFloat("_ScaleRatioA")) ||
                    !Mathf.Approximately(material.GetFloat("_ScaleRatioC"), expected.GetFloat("_ScaleRatioC")))
                {
                    throw new InvalidOperationException(
                        $"{label} canonical material must retain TMP-computed scale ratios.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(expected);
            }
        }

        public static uint[] BuildExactKoreanCorpusOrThrow()
        {
            var codePoints = new SortedSet<uint>();
            for (var value = 32u; value <= 126u; value++)
            {
                codePoints.Add(value);
            }

            AddUnicodeScalars(codePoints, KoreanAutonym);
            foreach (var value in LoadManagedKoreanStrings())
            {
                AddUnicodeScalars(codePoints, value);
            }

            codePoints.Add(MissingGlyphMarker);
            return codePoints.ToArray();
        }

        private static string BuildRequiredCharacterSet(IEnumerable<uint> exactCorpus)
        {
            return string.Concat(exactCorpus.Select(value => char.ConvertFromUtf32((int)value)));
        }

        private static void MakeAtlasNonReadableOrThrow(TMP_FontAsset fontAsset, string label)
        {
            var atlasTextures = fontAsset.atlasTextures ?? Array.Empty<Texture2D>();
            if (atlasTextures.Length != 1 || atlasTextures[0] == null)
            {
                throw new InvalidOperationException(
                    $"{label} must retain exactly one atlas before the non-readable freeze.");
            }

            var atlas = atlasTextures[0];
            if (atlas.isReadable)
            {
                atlas.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            }

            if (atlas.isReadable)
            {
                throw new InvalidOperationException(
                    $"{label} atlas remained readable after the shipping freeze.");
            }

            EditorUtility.SetDirty(atlas);
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

        private static void ImportCanonicalSerialization(string assetPath)
        {
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void DestroyTemporaryFontAsset(TMP_FontAsset fontAsset)
        {
            var material = fontAsset.material;
            var atlases = fontAsset.atlasTextures ?? Array.Empty<Texture2D>();
            UnityEngine.Object.DestroyImmediate(fontAsset);
            if (material != null)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            foreach (var atlas in atlases)
            {
                if (atlas != null)
                {
                    UnityEngine.Object.DestroyImmediate(atlas);
                }
            }
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

        private static void ApplyCanonicalScaleRatios(TMP_FontAsset fontAsset, string label)
        {
            var material = RequireScaleRatioMaterial(fontAsset, label);
            // Persist the same ratios TMP computes when text first loads the shared material.
            ShaderUtilities.GetShaderPropertyIDs();
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
        }

        private static Material RequireScaleRatioMaterial(TMP_FontAsset fontAsset, string label)
        {
            var material = fontAsset.material;
            if (material == null ||
                !material.HasProperty("_ScaleRatioA") ||
                !material.HasProperty("_ScaleRatioC"))
            {
                throw new InvalidOperationException(
                    $"{label} canonical material is missing its TMP scale-ratio properties.");
            }

            return material;
        }

        private static IEnumerable<string> LoadManagedKoreanStrings()
        {
            foreach (var mapping in KoreanStringTables)
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>(mapping.AssetPath);
                var liveTable = LocalizationEditorSettings
                    .GetStringTableCollection(mapping.CollectionName)?
                    .GetTable(new LocaleIdentifier("ko-KR")) as StringTable;
                if (table == null || liveTable == null || table != liveTable)
                {
                    throw new InvalidOperationException(
                        $"Managed Korean String Table is missing or not canonical: {mapping.AssetPath}");
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

        private static void AddUnicodeScalars(ISet<uint> destination, string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                uint scalar;
                if (char.IsHighSurrogate(character) &&
                    index + 1 < value.Length &&
                    char.IsLowSurrogate(value[index + 1]))
                {
                    scalar = (uint)char.ConvertToUtf32(character, value[++index]);
                }
                else if (char.IsSurrogate(character) || char.IsControl(character))
                {
                    continue;
                }
                else
                {
                    scalar = character;
                }

                destination.Add(scalar);
            }
        }

        private static AssetIdentity CaptureAssetIdentity(UnityEngine.Object asset, string label)
        {
            if (asset == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId))
            {
                throw new InvalidOperationException($"Unable to capture {label} identity.");
            }

            return new AssetIdentity(guid, localId);
        }

        private static void ValidateAssetIdentity(
            UnityEngine.Object asset,
            AssetIdentity expected,
            string label)
        {
            var actual = CaptureAssetIdentity(asset, label);
            if (!string.Equals(actual.Guid, expected.Guid, StringComparison.Ordinal) ||
                actual.LocalId != expected.LocalId)
            {
                throw new InvalidOperationException(
                    $"{label} identity changed from {expected.Guid}:{expected.LocalId} " +
                    $"to {actual.Guid}:{actual.LocalId}.");
            }
        }

        private static string FormatCharacters(IEnumerable<char> characters)
        {
            if (characters == null)
            {
                return "<unknown>";
            }

            return string.Join(
                ", ",
                characters.Select(character => $"{character} (U+{(int)character:X4})"));
        }

        private static string FormatCodePoints(IEnumerable<uint> codePoints)
        {
            return string.Join(", ", codePoints.Select(value => $"U+{value:X4}"));
        }

        private sealed class AssetIdentity
        {
            public AssetIdentity(string guid, long localId)
            {
                Guid = guid;
                LocalId = localId;
            }

            public string Guid { get; }

            public long LocalId { get; }
        }

        private sealed class FontContractSnapshot
        {
            public FontContractSnapshot(
                AssetIdentity fontAssetIdentity,
                AssetIdentity materialIdentity,
                AssetIdentity atlasIdentity,
                int atlasPageCount)
            {
                FontAssetIdentity = fontAssetIdentity;
                MaterialIdentity = materialIdentity;
                AtlasIdentity = atlasIdentity;
                AtlasPageCount = atlasPageCount;
            }

            public AssetIdentity FontAssetIdentity { get; }

            public AssetIdentity MaterialIdentity { get; }

            public AssetIdentity AtlasIdentity { get; }

            public int AtlasPageCount { get; }
        }

        private sealed class FontRebuildPlan
        {
            public FontRebuildPlan(
                string label,
                string fontAssetPath,
                string canonicalAssetName,
                Font sourceFont,
                TMP_FontAsset fontAsset,
                uint[] exactCorpus,
                FontContractSnapshot before)
            {
                Label = label;
                FontAssetPath = fontAssetPath;
                CanonicalAssetName = canonicalAssetName;
                SourceFont = sourceFont;
                FontAsset = fontAsset;
                ExactCorpus = exactCorpus;
                Before = before;
            }

            public string Label { get; }

            public string FontAssetPath { get; }

            public string CanonicalAssetName { get; }

            public Font SourceFont { get; }

            public TMP_FontAsset FontAsset { get; }

            public uint[] ExactCorpus { get; }

            public FontContractSnapshot Before { get; }
        }
    }
}
