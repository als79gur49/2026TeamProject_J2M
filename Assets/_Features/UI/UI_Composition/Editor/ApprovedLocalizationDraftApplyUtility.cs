using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

[assembly: InternalsVisibleTo("Game.Feature.UI.Tests")]

namespace Game.Feature.UI.Composition.Editor
{
    public static class ApprovedLocalizationDraftApplyUtility
    {
        private const string DraftPath = "Docs/Localization/Four-Locale-Translation-Draft.csv";
        private const string FontFolder = "Assets/_Shared/UI/Fonts/NotoSansCJK";
        private const int AtlasSize = 2048;
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const uint UnsupportedOrbitronCaret = 0x005e;
        private const uint Ellipsis = 0x2026;
        private const uint MissingGlyphMarker = 0x25a1;
        private const string PreloadAddressableLabel = "Preload";
        private const string AddressablesRoot = "Assets/AddressableAssetsData";

        private static readonly string[] ProductionLocaleCodes =
        {
            "en-US", "ko-KR", "ja-JP", "zh-CN",
        };

        private static readonly (string Code, string Column)[] TargetLocales =
        {
            ("ja-JP", "ja_JP"),
            ("zh-CN", "zh_CN"),
        };

        private static readonly (string Code, string LocalePath, string TableGroupName)[]
            GovernedLocales =
            {
                ("en-US", "Assets/Localization/Locales/en-US.asset",
                    "Localization-String-Tables-en-US"),
                ("ko-KR", "Assets/Localization/Locales/ko-KR.asset",
                    "Localization-String-Tables-ko-KR"),
                ("ja-JP", "Assets/Localization/Locales/ja-JP.asset",
                    "Localization-String-Tables-Japanese (Japan) (ja-JP)"),
                ("zh-CN", "Assets/Localization/Locales/zh-CN.asset",
                    "Localization-String-Tables-Chinese (Simplified) (zh-CN)"),
            };

        private static readonly (string Code, string Column, string Autonym, string FontPrefix)[] FontLocales =
        {
            ("ja-JP", "ja_JP", "日本語", "NotoSansJP"),
            ("zh-CN", "zh_CN", "简体中文", "NotoSansSC"),
        };

        private static readonly IReadOnlyDictionary<string, string> GovernedSourceFontHashes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [$"{FontFolder}/NotoSansJP-Regular.otf"] =
                    "dff723ba59d57d136764a04b9b2d03205544f7cd785a711442d6d2d085ac5073",
                [$"{FontFolder}/NotoSansJP-Bold.otf"] =
                    "1b0edfb500b73a4fa8a4fcaae1bbbd403994e08e73e3e0da37e70d3853f42c5f",
                [$"{FontFolder}/NotoSansSC-Regular.otf"] =
                    "faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9",
                [$"{FontFolder}/NotoSansSC-Bold.otf"] =
                    "c6cb5a93abaa9edc8ee7463b7ebb7f42d618d40e6ed2f7a5371c97b0b64767c0",
                ["Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/Exo2.0-Regular.otf"] =
                    "dca1f9e0702c15641a26d5616ecbb87f7f6c12e5604b03fcf086c1155b9b936d",
                ["Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/Exo2.0-SemiBold.otf"] =
                    "2cb43389c39ca1fb2ce07d40b68bc14a166d355dfde0d6fcf9c92101a1a26d2a",
                ["Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/SairaCondensed-SemiBold.ttf"] =
                    "30f8ed4d078211003a9715c80c51ce031bab5c9a17e8771182e4c4599205634b",
                ["Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Orbitron/Orbitron-ExtraBold.ttf"] =
                    "e2694ab08e4a1d120e495107b2b12d753372fa5cf569c8864a55422bbbb0580e",
                ["Assets/TextMesh Pro/Fonts/LiberationSans.ttf"] =
                    "e5b0af421ea2bfbc1ac8d251d647268087ae82786234c57f757d1f0b90fa8b49",
            };

        private static readonly (string AssetPath, string SourcePath)[] EnglishThemeFonts =
        {
            (
                "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/Exo2.0-Regular SDF.asset",
                "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/Exo2.0-Regular.otf"),
            (
                "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/Exo2.0-SemiBold SDF.asset",
                "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/Exo2.0-SemiBold.otf"),
            (
                "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Font_SciFiSoldier_Bold.asset",
                "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/SairaCondensed-SemiBold.ttf"),
            (
                "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Orbitron/Orbitron-ExtraBold SDF.asset",
                "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Orbitron/Orbitron-ExtraBold.ttf"),
            (
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
                "Assets/TextMesh Pro/Fonts/LiberationSans.ttf"),
        };

        public static void ApplyAndQuit()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            // Validate the approved localization data before the first production mutation. Apply
            // must remain safe even when it is invoked directly instead of through the Python lane.
            ValidateMutationTopologyPreflightOrThrow();
            var managedAssetPaths = CollectManagedMutationAssetPathsOrThrow();
            var addressableAssetPaths = CollectAddressableAssetPathsOrThrow();
            ProductionAssetRollbackSnapshot.ValidateAssetGraphsCleanOrThrow(managedAssetPaths);
            ProductionAssetRollbackSnapshot.ValidateAssetGraphsCleanOrThrow(addressableAssetPaths);
            var plan = BuildValidatedApplyPlan();
            var rows = plan.Rows;
            ValidateKoreanTableCoverageOrThrow(rows);
            ValidateTargetLocaleTablesOrThrow(rows, requireExactApprovedState: false);
            var fontIdentities = ValidateFontApplyPreflightOrThrow(rows);
            ProductionAssetRollbackSnapshot.ValidateAssetGraphsCleanOrThrow(managedAssetPaths);
            plan = plan.WithSemanticManifests(CaptureCollectionSemanticManifestsOrThrow(plan));
            var addressablesFence = ProductionDirectoryByteFence.Capture(AddressablesRoot);
            var rollback = ProductionAssetRollbackSnapshot.Capture(managedAssetPaths);
            try
            {
                foreach (var locale in TargetLocales)
                {
                    ApplyLocale(plan, locale.Code);
                }

                ValidateTargetLocaleTablesOrThrow(rows, requireExactApprovedState: true);
                GenerateStaticFontAssets(rows);
                RebuildEnglishThemeFonts(rows);
                KboDiaGothicGlyphUpdateUtility.RebuildForProductionApplyOrThrow();
                ValidateProductionAssetsOrThrow(rows);

                var changedAssetPaths = SaveAndImportDirtyManagedAssetsOrThrow(managedAssetPaths);
                addressablesFence.ValidateUnchangedOrThrow();
                rollback.ValidateImmutableFilesUnchangedOrThrow(changedAssetPaths);
                ProductionAssetRollbackSnapshot.ValidateAssetGraphsCleanOrThrow(managedAssetPaths);
                ProductionAssetRollbackSnapshot.ValidateAssetGraphsCleanOrThrow(addressableAssetPaths);
                ValidateCollectionSemanticManifestsOrThrow(plan);
                ValidateTargetLocaleTablesOrThrow(rows, requireExactApprovedState: true);
                ValidateProductionAssetsOrThrow(rows);
                KboDiaGothicGlyphUpdateUtility.ValidatePersistedProductionAssetsOrThrow();
                ValidateFontIdentitySnapshotsOrThrow(fontIdentities);
                ValidateGovernedSourceFontHashesOrThrow();
                Debug.Log(
                    "APPROVED_LOCALIZATION_APPLY PASS " +
                    $"changed_assets={changedAssetPaths.Length} " +
                    $"paths={string.Join(",", changedAssetPaths)} addressables_unchanged=PASS");
            }
            catch (Exception applyException)
            {
                var failures = new List<Exception> { applyException };
                try
                {
                    rollback.RestoreOrThrow();
                }
                catch (Exception restoreException)
                {
                    failures.Add(restoreException);
                }

                try
                {
                    addressablesFence.RestoreOrThrow();
                }
                catch (Exception addressablesRestoreException)
                {
                    failures.Add(addressablesRestoreException);
                }

                if (failures.Count != 1)
                {
                    throw new AggregateException(
                        "Approved localization Apply failed and one or more production rollbacks also failed.",
                        failures);
                }

                throw;
            }
        }

        public static void ValidateProductionAssetsOrThrow()
        {
            ValidateProductionAssetsOrThrow(BuildValidatedApplyPlan().Rows);
        }

        private static void ValidateProductionAssetsOrThrow(
            IReadOnlyList<ApprovedLocalizationRow> rows)
        {
            foreach (var locale in FontLocales)
            {
                var expectedCorpus = new HashSet<uint>(
                    BuildUnicodeCorpus(rows, locale.Column, locale.Autonym));
                foreach (var weight in new[] { "Regular", "Bold" })
                {
                    var path = $"{FontFolder}/{locale.FontPrefix}-{weight} SDF.asset";
                    ValidateStaticFontAsset(path, expectedCorpus);
                }
            }

            var englishCorpus = new HashSet<uint>(BuildEnglishUnicodeCorpus(rows));
            foreach (var font in LoadDistinctThemeFonts("en-US"))
            {
                var expectedCorpus = new HashSet<uint>(englishCorpus);
                if (font == TMP_Settings.defaultFontAsset)
                {
                    expectedCorpus.Add(MissingGlyphMarker);
                }

                ValidateStaticFontAsset(font, expectedCorpus);
            }

            var koreanCorpus = new HashSet<uint>(
                KboDiaGothicGlyphUpdateUtility.BuildExactKoreanCorpusOrThrow());
            var koreanFonts = LoadDistinctThemeFonts("ko-KR");
            if (koreanFonts.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Production Korean Theme must reference exactly two distinct fonts, not {koreanFonts.Length}.");
            }

            foreach (var font in koreanFonts)
            {
                ValidateStaticFontAsset(font, koreanCorpus);
            }

            ValidateVisibleMissingGlyphMarkerOrThrow();

            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(
                TypographyThemeValidator.ThemeAssetPath);
            if (theme == null ||
                !theme.RequiredLocaleCodes.SequenceEqual(ProductionLocaleCodes, StringComparer.Ordinal) ||
                !ProductionLocaleCodes.All(code =>
                    theme.LocaleFontSets.Count(set =>
                        set != null && string.Equals(set.LocaleCode, code, StringComparison.Ordinal)) == 1))
            {
                throw new InvalidOperationException(
                    "Production typography Theme does not contain exactly one direct font set for every production locale.");
            }

            ValidateCanonicalKoreanThemeMappingOrThrow(theme);
            ValidateCanonicalCjkThemeMappingOrThrow(theme, "ja-JP", "NotoSansJP");
            ValidateCanonicalCjkThemeMappingOrThrow(theme, "zh-CN", "NotoSansSC");

            foreach (var localeCode in new[] { "en-US", "ko-KR" })
            {
                var fontSet = theme.LocaleFontSets.Single(set =>
                    set != null && string.Equals(set.LocaleCode, localeCode, StringComparison.Ordinal));
                if (fontSet.Entries.Any(entry =>
                        entry == null ||
                        entry.FontAsset == null ||
                        !MeetsStaticFontContract(entry.FontAsset)))
                {
                    throw new InvalidOperationException(
                        $"Every {localeCode} Theme font must satisfy the Static single-atlas, " +
                        "non-readable, no-fallback contract.");
                }
            }
        }

        private static void ValidateMutationTopologyPreflightOrThrow()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("AddressableAssetSettings is unavailable.");
            }

            ValidateLocaleMoveTopologyOrThrow(
                "Assets/Localization/StringTables/Stage/Japanese (Japan) (ja-JP).asset",
                "Assets/Localization/Locales/ja-JP.asset");
            ValidateLocaleMoveTopologyOrThrow(
                "Assets/Localization/StringTables/Stage/Chinese (Simplified) (zh-CN).asset",
                "Assets/Localization/Locales/zh-CN.asset");

            foreach (var locale in GovernedLocales)
            {
                foreach (var collectionName in new[] { "UI", "Stage" })
                {
                    var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                    if (!(collection?.GetTable(new LocaleIdentifier(locale.Code)) is StringTable))
                    {
                        throw new InvalidOperationException(
                            $"Production Apply requires the existing '{collectionName}/{locale.Code}' String Table.");
                    }
                }
            }

            ValidateAndCollectGovernedAddressableGroupPathsOrThrow(settings);
        }

        private static void ValidateLocaleMoveTopologyOrThrow(string sourcePath, string targetPath)
        {
            var sourceExists = AssetDatabase.LoadMainAssetAtPath(sourcePath) != null;
            var targetExists = AssetDatabase.LoadMainAssetAtPath(targetPath) != null;
            if (sourceExists || !targetExists)
            {
                throw new InvalidOperationException(
                    $"Production locale topology requires existing target '{targetPath}' and no staging source '{sourcePath}'.");
            }
        }

        internal static string[] ValidateAndCollectGovernedAddressableGroupPathsOrThrow(
            AddressableAssetSettings settings = null)
        {
            settings ??= AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("AddressableAssetSettings is unavailable.");
            }

            var expectedGroupNames = new HashSet<string>(
                GovernedLocales.Select(locale => locale.TableGroupName),
                StringComparer.Ordinal)
            {
                "Localization-Locales",
                "Localization-Assets-Shared",
            };
            var actualLocalizationGroups = settings.groups
                .Where(group => group != null && group.Name.StartsWith("Localization-", StringComparison.Ordinal))
                .ToArray();
            var actualGroupNames = new HashSet<string>(
                actualLocalizationGroups.Select(group => group.Name),
                StringComparer.Ordinal);
            if (!actualGroupNames.SetEquals(expectedGroupNames) ||
                actualLocalizationGroups.Length != expectedGroupNames.Count)
            {
                throw new InvalidOperationException(
                    "Production Addressables must contain exactly the six canonical localization groups. " +
                    $"Expected: {string.Join(", ", expectedGroupNames.OrderBy(value => value))}. " +
                    $"Actual: {string.Join(", ", actualGroupNames.OrderBy(value => value))}.");
            }

            var requiredLabels = new[]
            {
                "Locale", "Locale-en-US", "Locale-ko-KR", "Locale-ja-JP", "Locale-zh-CN",
                PreloadAddressableLabel,
            };
            var settingsLabels = new HashSet<string>(settings.GetLabels(), StringComparer.Ordinal);
            var missingLabels = requiredLabels.Where(label => !settingsLabels.Contains(label)).ToArray();
            if (missingLabels.Length != 0)
            {
                throw new InvalidOperationException(
                    "Production Addressables settings are missing required labels: " +
                    string.Join(", ", missingLabels));
            }

            var groupPaths = new HashSet<string>(StringComparer.Ordinal);
            var localeGroup = ValidateCanonicalGroupOrThrow(
                settings, "Localization-Locales", expectedEntryCount: GovernedLocales.Length,
                groupPaths);
            foreach (var locale in GovernedLocales)
            {
                var localeAsset = AssetDatabase.LoadAssetAtPath<Locale>(locale.LocalePath);
                if (localeAsset == null ||
                    !string.Equals(localeAsset.Identifier.Code, locale.Code, StringComparison.Ordinal) ||
                    LocalizationEditorSettings.GetLocale(locale.Code) != localeAsset)
                {
                    throw new InvalidOperationException(
                        $"Production Locale '{locale.Code}' must be registered from canonical path " +
                        $"'{locale.LocalePath}'.");
                }

                ValidateAddressableEntryOrThrow(
                    settings,
                    locale.LocalePath,
                    localeGroup,
                    localeAsset.LocaleName,
                    new[] { "Locale" });

                var tableGroup = ValidateCanonicalGroupOrThrow(
                    settings, locale.TableGroupName, expectedEntryCount: 2, groupPaths);
                foreach (var collectionName in new[] { "UI", "Stage" })
                {
                    var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                    var table = collection?.GetTable(new LocaleIdentifier(locale.Code)) as StringTable;
                    if (table == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing governed String Table '{collectionName}/{locale.Code}'.");
                    }

                    var expectedTablePath =
                        $"Assets/Localization/StringTables/{collectionName}/{collectionName}_{locale.Code}.asset";
                    var tablePath = AssetDatabase.GetAssetPath(table);
                    if (!string.Equals(tablePath, expectedTablePath, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Governed String Table '{collectionName}/{locale.Code}' must use canonical path " +
                            $"'{expectedTablePath}', not '{tablePath}'.");
                    }

                    ValidateAddressableEntryOrThrow(
                        settings,
                        tablePath,
                        tableGroup,
                        $"{collectionName}_{locale.Code}",
                        new[] { $"Locale-{locale.Code}", PreloadAddressableLabel });
                }
            }

            var sharedGroup = ValidateCanonicalGroupOrThrow(
                settings, "Localization-Assets-Shared", expectedEntryCount: 2, groupPaths);
            foreach (var collectionName in new[] { "UI", "Stage" })
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                var sharedPath = AssetDatabase.GetAssetPath(collection?.SharedData);
                var expectedSharedPath =
                    $"Assets/Localization/StringTables/{collectionName}/{collectionName} Shared Data.asset";
                if (!string.Equals(sharedPath, expectedSharedPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Governed Shared Table Data '{collectionName}' must use canonical path " +
                        $"'{expectedSharedPath}', not '{sharedPath}'.");
                }

                ValidateAddressableEntryOrThrow(
                    settings, sharedPath, sharedGroup, sharedPath, Array.Empty<string>());
            }

            return groupPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static AddressableAssetGroup ValidateCanonicalGroupOrThrow(
            AddressableAssetSettings settings,
            string groupName,
            int expectedEntryCount,
            ISet<string> groupPaths)
        {
            var group = settings.groups.SingleOrDefault(candidate =>
                candidate != null && string.Equals(candidate.Name, groupName, StringComparison.Ordinal));
            var expectedPath = $"Assets/AddressableAssetsData/AssetGroups/{groupName}.asset";
            if (group == null ||
                !string.Equals(AssetDatabase.GetAssetPath(group), expectedPath, StringComparison.Ordinal) ||
                group.Settings != settings ||
                !group.ReadOnly ||
                group.entries.Count != expectedEntryCount)
            {
                throw new InvalidOperationException(
                    $"Localization Addressables group '{groupName}' does not satisfy its canonical " +
                    $"path, ownership, read-only, or entry-count contract at '{expectedPath}'.");
            }

            if (group.Schemas.Count != 2 ||
                group.Schemas.Count(schema => schema is BundledAssetGroupSchema) != 1 ||
                group.Schemas.Count(schema => schema is ContentUpdateGroupSchema) != 1)
            {
                throw new InvalidOperationException(
                    $"Localization Addressables group '{groupName}' must contain exactly one bundled " +
                    "and one content-update schema.");
            }

            foreach (var schema in group.Schemas)
            {
                var expectedSchemaPath =
                    $"Assets/AddressableAssetsData/AssetGroups/Schemas/{groupName}_{schema.GetType().Name}.asset";
                if (schema == null || schema.Group != group ||
                    !string.Equals(
                        AssetDatabase.GetAssetPath(schema), expectedSchemaPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Localization Addressables schema for '{groupName}' is not canonical at " +
                        $"'{expectedSchemaPath}'.");
                }

                if (schema is BundledAssetGroupSchema bundled && !bundled.IncludeInBuild)
                {
                    throw new InvalidOperationException(
                        $"Localization Addressables group '{groupName}' must be included in builds.");
                }

                if (schema is ContentUpdateGroupSchema contentUpdate && contentUpdate.StaticContent)
                {
                    throw new InvalidOperationException(
                        $"Localization Addressables group '{groupName}' must not use Static Content.");
                }
            }

            groupPaths.Add(expectedPath);
            return group;
        }

        private static void ValidateAddressableEntryOrThrow(
            AddressableAssetSettings settings,
            string assetPath,
            AddressableAssetGroup expectedGroup,
            string expectedAddress,
            IEnumerable<string> expectedLabels)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.FindAssetEntry(guid);
            var expectedLabelSet = new HashSet<string>(expectedLabels, StringComparer.Ordinal);
            if (string.IsNullOrEmpty(guid) || entry == null ||
                entry.parentGroup != expectedGroup ||
                !entry.ReadOnly ||
                !string.Equals(entry.address, expectedAddress, StringComparison.Ordinal) ||
                !new HashSet<string>(entry.labels, StringComparer.Ordinal).SetEquals(expectedLabelSet))
            {
                throw new InvalidOperationException(
                    $"Addressables entry '{assetPath}' does not satisfy its canonical group, address, " +
                    "read-only, or label contract.");
            }
        }

        private static string[] CollectManagedMutationAssetPathsOrThrow()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal)
            {
                TypographyThemeValidator.ThemeAssetPath,
            };
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (addressableSettings == null)
            {
                throw new InvalidOperationException("AddressableAssetSettings is unavailable.");
            }

            ValidateAndCollectGovernedAddressableGroupPathsOrThrow(addressableSettings);
            paths.UnionWith(GovernedLocales.Select(locale => locale.LocalePath));
            foreach (var collectionName in new[] { "UI", "Stage" })
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                if (collection == null)
                {
                    throw new InvalidOperationException(
                        $"Missing governed String Table collection '{collectionName}'.");
                }

                paths.Add(AssetDatabase.GetAssetPath(collection));
                paths.Add(AssetDatabase.GetAssetPath(collection.SharedData));
                foreach (var locale in GovernedLocales)
                {
                    var table = collection.GetTable(new LocaleIdentifier(locale.Code)) as StringTable;
                    if (table == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing governed String Table '{collectionName}/{locale.Code}'.");
                    }

                    paths.Add(AssetDatabase.GetAssetPath(table));
                }
            }

            foreach (var fontAssetPath in GetCanonicalManagedFontAssetPaths())
            {
                paths.Add(fontAssetPath);
            }

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(Path.GetFullPath(path)))
                {
                    throw new InvalidOperationException(
                        $"Managed production asset is missing on disk before Apply: '{path}'.");
                }

            }

            return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static IEnumerable<string> GetCanonicalManagedFontAssetPaths()
        {
            foreach (var font in EnglishThemeFonts)
            {
                yield return font.AssetPath;
            }

            foreach (var locale in FontLocales)
            {
                yield return $"{FontFolder}/{locale.FontPrefix}-Regular SDF.asset";
                yield return $"{FontFolder}/{locale.FontPrefix}-Bold SDF.asset";
            }

            yield return "Assets/_Shared/UI/Fonts/KBODiaGothic-Medium SDF.asset";
            yield return "Assets/_Shared/UI/Fonts/KBODiaGothic-Light SDF.asset";
        }

        private static string[] CollectAddressableAssetPathsOrThrow()
        {
            var root = Path.GetFullPath(AddressablesRoot);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException(root);
            }

            return Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories)
                .Select(ToProjectRelativePathOrThrow)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static void ValidateKoreanTableCoverageOrThrow(
            IReadOnlyList<ApprovedLocalizationRow> rows)
        {
            foreach (var collectionName in new[] { "UI", "Stage" })
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                var korean = collection?.GetTable(new LocaleIdentifier("ko-KR")) as StringTable;
                var english = collection?.GetTable(new LocaleIdentifier("en-US")) as StringTable;
                var expectedKoreanPath =
                    $"Assets/Localization/StringTables/{collectionName}/{collectionName}_ko-KR.asset";
                if (korean == null || english == null ||
                    !string.Equals(
                        AssetDatabase.GetAssetPath(korean),
                        expectedKoreanPath,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Missing or non-canonical production Korean String Table '{expectedKoreanPath}'.");
                }

                var expectedKeys = rows
                    .Where(candidate =>
                        string.Equals(candidate.Collection, collectionName, StringComparison.Ordinal))
                    .Select(candidate => candidate.Key)
                    .ToHashSet(StringComparer.Ordinal);
                var actualKeys = korean.SharedData.Entries
                    .Where(sharedEntry => korean.GetEntry(sharedEntry.Key) != null)
                    .Select(sharedEntry => sharedEntry.Key)
                    .ToHashSet(StringComparer.Ordinal);
                if (!actualKeys.SetEquals(expectedKeys))
                {
                    var missing = expectedKeys.Except(actualKeys).OrderBy(value => value);
                    var unexpected = actualKeys.Except(expectedKeys).OrderBy(value => value);
                    throw new InvalidOperationException(
                        $"Production Korean '{collectionName}' key inventory is not exact. " +
                        $"Missing: {string.Join(", ", missing)}. " +
                        $"Unexpected: {string.Join(", ", unexpected)}.");
                }

                foreach (var row in rows.Where(candidate =>
                             string.Equals(candidate.Collection, collectionName, StringComparison.Ordinal)))
                {
                    var entry = korean.GetEntry(row.Key);
                    if (entry == null || string.IsNullOrWhiteSpace(entry.LocalizedValue))
                    {
                        throw new InvalidOperationException(
                            $"Production Korean table is missing a non-empty '{collectionName}/{row.Key}' entry.");
                    }

                    var englishEntry = english.GetEntry(row.Key);
                    if (englishEntry == null)
                    {
                        throw new InvalidOperationException(
                            $"English production table is missing '{collectionName}/{row.Key}'.");
                    }

                    ValidateTranslationStructureOrThrow(
                        row,
                        new EnglishLocalizationInventoryRow(
                            collectionName,
                            row.Key,
                            englishEntry.LocalizedValue,
                            englishEntry.IsSmart),
                        "ko_KR",
                        entry.LocalizedValue);
                }
            }
        }

        private static FontIdentitySnapshot[] ValidateFontApplyPreflightOrThrow(
            IReadOnlyList<ApprovedLocalizationRow> rows)
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(
                TypographyThemeValidator.ThemeAssetPath);
            if (theme == null ||
                !theme.RequiredLocaleCodes.SequenceEqual(ProductionLocaleCodes, StringComparer.Ordinal) ||
                !ProductionLocaleCodes.All(code =>
                    theme.LocaleFontSets.Count(set =>
                        set != null && string.Equals(set.LocaleCode, code, StringComparison.Ordinal)) == 1))
            {
                throw new InvalidOperationException(
                    "Production typography Theme must contain exactly one direct font set for every production locale before Apply.");
            }

            ValidateCanonicalKoreanThemeMappingOrThrow(theme);
            ValidateCanonicalCjkThemeMappingOrThrow(theme, "ja-JP", "NotoSansJP");
            ValidateCanonicalCjkThemeMappingOrThrow(theme, "zh-CN", "NotoSansSC");
            var identitySnapshots = CaptureThemeFontIdentitySnapshotsOrThrow(theme);

            foreach (var locale in FontLocales)
            {
                var corpus = BuildUnicodeCorpus(rows, locale.Column, locale.Autonym);
                foreach (var weight in new[] { "Regular", "Bold" })
                {
                    var sourcePath = $"{FontFolder}/{locale.FontPrefix}-{weight}.otf";
                    var assetPath = $"{FontFolder}/{locale.FontPrefix}-{weight} SDF.asset";
                    ValidateExistingFontTargetPreflightOrThrow(
                        assetPath,
                        sourcePath,
                        SamplingPointSize,
                        AtlasPadding,
                        GlyphRenderMode.SDFAA,
                        AtlasSize,
                        AtlasSize);
                    ValidateTransientSingleAtlasFitOrThrow(
                        sourcePath,
                        corpus,
                        SamplingPointSize,
                        AtlasPadding,
                        GlyphRenderMode.SDFAA,
                        AtlasSize,
                        AtlasSize);
                }
            }

            var englishCorpus = BuildEnglishUnicodeCorpus(rows);
            var themeFonts = new HashSet<TMP_FontAsset>(LoadDistinctThemeFonts("en-US"));
            var mappedFonts = new HashSet<TMP_FontAsset>();
            foreach (var mapping in EnglishThemeFonts)
            {
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(mapping.AssetPath);
                if (fontAsset == null || !themeFonts.Contains(fontAsset))
                {
                    throw new InvalidOperationException(
                        $"English font mapping is missing from the production Theme: '{mapping.AssetPath}'.");
                }

                ValidateExistingFontTargetPreflightOrThrow(
                    mapping.AssetPath,
                    mapping.SourcePath,
                    Mathf.RoundToInt(fontAsset.faceInfo.pointSize),
                    fontAsset.atlasPadding,
                    fontAsset.atlasRenderMode,
                    fontAsset.atlasWidth,
                    fontAsset.atlasHeight);

                var corpus = fontAsset == TMP_Settings.defaultFontAsset
                    ? englishCorpus.Append(MissingGlyphMarker).Distinct().OrderBy(value => value).ToArray()
                    : englishCorpus;
                ValidateTransientSingleAtlasFitOrThrow(
                    mapping.SourcePath,
                    corpus,
                    Mathf.RoundToInt(fontAsset.faceInfo.pointSize),
                    fontAsset.atlasPadding,
                    fontAsset.atlasRenderMode,
                    fontAsset.atlasWidth,
                    fontAsset.atlasHeight);
                mappedFonts.Add(fontAsset);
            }

            if (!mappedFonts.SetEquals(themeFonts))
            {
                var unmapped = themeFonts.Except(mappedFonts).Select(AssetDatabase.GetAssetPath);
                throw new InvalidOperationException(
                    $"Production Theme contains unmapped English fonts: {string.Join(", ", unmapped)}.");
            }

            KboDiaGothicGlyphUpdateUtility.ValidateProductionApplyPreflightOrThrow();
            return identitySnapshots;
        }

        private static void ValidateCanonicalKoreanThemeMappingOrThrow(
            GameplayUiTypographyTheme theme)
        {
            var fontSet = theme.LocaleFontSets.Single(set =>
                set != null && string.Equals(set.LocaleCode, "ko-KR", StringComparison.Ordinal));
            const string mediumPath =
                "Assets/_Shared/UI/Fonts/KBODiaGothic-Medium SDF.asset";
            const string lightPath =
                "Assets/_Shared/UI/Fonts/KBODiaGothic-Light SDF.asset";
            var expectedMappings = new Dictionary<
                (FontCategory Category, LocalizedTextWeight Weight), string>
            {
                [(FontCategory.Display, LocalizedTextWeight.Bold)] = mediumPath,
                [(FontCategory.Heading, LocalizedTextWeight.Bold)] = lightPath,
                [(FontCategory.Body, LocalizedTextWeight.Regular)] = lightPath,
                [(FontCategory.UI, LocalizedTextWeight.Regular)] = mediumPath,
                [(FontCategory.UI, LocalizedTextWeight.Bold)] = mediumPath,
                [(FontCategory.Utility, LocalizedTextWeight.Regular)] = mediumPath,
                [(FontCategory.Symbol, LocalizedTextWeight.Regular)] = mediumPath,
            };
            var entries = fontSet.Entries ?? new List<LocaleFontEntry>();
            var actualPairs = entries
                .Where(entry => entry != null)
                .Select(entry => (entry.FontCategory, entry.Weight))
                .ToHashSet();
            if (entries.Count != expectedMappings.Count ||
                !actualPairs.SetEquals(expectedMappings.Keys))
            {
                throw new InvalidOperationException(
                    "Production Korean Theme must contain the canonical seven category/weight mappings.");
            }

            foreach (var entry in entries)
            {
                if (!expectedMappings.TryGetValue(
                        (entry.FontCategory, entry.Weight), out var expectedPath) ||
                    entry.FontAsset == null ||
                    !string.Equals(
                        AssetDatabase.GetAssetPath(entry.FontAsset),
                        expectedPath,
                        StringComparison.Ordinal) ||
                    entry.MaterialPreset != entry.FontAsset.material ||
                    entry.WeightStrategy != TypographyWeightStrategy.UseFontAsset)
                {
                    throw new InvalidOperationException(
                        $"Production Korean Theme mapping '{entry.FontCategory}/{entry.Weight}' is not canonical.");
                }
            }
        }

        private static void ValidateCanonicalCjkThemeMappingOrThrow(
            GameplayUiTypographyTheme theme,
            string localeCode,
            string fontPrefix)
        {
            var fontSet = theme.LocaleFontSets.Single(set =>
                set != null && string.Equals(set.LocaleCode, localeCode, StringComparison.Ordinal));
            var regularPath = $"{FontFolder}/{fontPrefix}-Regular SDF.asset";
            var boldPath = $"{FontFolder}/{fontPrefix}-Bold SDF.asset";
            var expectedMappings = new Dictionary<
                (FontCategory Category, LocalizedTextWeight Weight), string>
            {
                [(FontCategory.Display, LocalizedTextWeight.Bold)] = boldPath,
                [(FontCategory.Heading, LocalizedTextWeight.Bold)] = boldPath,
                [(FontCategory.Body, LocalizedTextWeight.Regular)] = regularPath,
                [(FontCategory.UI, LocalizedTextWeight.Regular)] = regularPath,
                [(FontCategory.UI, LocalizedTextWeight.Bold)] = boldPath,
                [(FontCategory.Utility, LocalizedTextWeight.Regular)] = regularPath,
                [(FontCategory.Symbol, LocalizedTextWeight.Regular)] = regularPath,
            };
            var entries = fontSet.Entries ?? new List<LocaleFontEntry>();
            var actualPairs = entries
                .Where(entry => entry != null)
                .Select(entry => (entry.FontCategory, entry.Weight))
                .ToHashSet();
            if (entries.Count != expectedMappings.Count ||
                !actualPairs.SetEquals(expectedMappings.Keys))
            {
                throw new InvalidOperationException(
                    $"Production {localeCode} Theme must contain the canonical seven category/weight mappings.");
            }

            foreach (var entry in entries)
            {
                if (!expectedMappings.TryGetValue(
                        (entry.FontCategory, entry.Weight), out var expectedPath) ||
                    entry.FontAsset == null ||
                    !string.Equals(
                        AssetDatabase.GetAssetPath(entry.FontAsset),
                        expectedPath,
                        StringComparison.Ordinal) ||
                    entry.MaterialPreset != entry.FontAsset.material ||
                    entry.WeightStrategy != TypographyWeightStrategy.UseFontAsset)
                {
                    throw new InvalidOperationException(
                        $"Production {localeCode} Theme mapping " +
                        $"'{entry.FontCategory}/{entry.Weight}' is not canonical.");
                }
            }
        }

        private static FontIdentitySnapshot[] CaptureThemeFontIdentitySnapshotsOrThrow(
            GameplayUiTypographyTheme theme)
        {
            var fonts = theme.LocaleFontSets
                .Where(set => set != null)
                .SelectMany(set => set.Entries ?? new List<LocaleFontEntry>())
                .Where(entry => entry != null && entry.FontAsset != null)
                .Select(entry => entry.FontAsset)
                .Distinct()
                .ToArray();
            if (fonts.Length != 11)
            {
                throw new InvalidOperationException(
                    $"Production Theme must reference exactly 11 distinct font assets, not {fonts.Length}.");
            }

            return fonts.Select(fontAsset =>
            {
                var path = AssetDatabase.GetAssetPath(fontAsset);
                var atlases = fontAsset.atlasTextures ?? Array.Empty<Texture2D>();
                if (string.IsNullOrEmpty(path) || fontAsset.material == null ||
                    atlases.Length != 1 || atlases[0] == null)
                {
                    throw new InvalidOperationException(
                        $"Unable to capture the persistent font graph for '{fontAsset.name}'.");
                }

                return new FontIdentitySnapshot(
                    path,
                    CapturePersistentIdentityOrThrow(fontAsset, $"{fontAsset.name} font"),
                    CapturePersistentIdentityOrThrow(fontAsset.material, $"{fontAsset.name} material"),
                    CapturePersistentIdentityOrThrow(atlases[0], $"{fontAsset.name} atlas"));
            }).ToArray();
        }

        private static void ValidateFontIdentitySnapshotsOrThrow(
            IEnumerable<FontIdentitySnapshot> snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(snapshot.AssetPath);
                var atlases = fontAsset?.atlasTextures ?? Array.Empty<Texture2D>();
                if (fontAsset == null || fontAsset.material == null ||
                    atlases.Length != 1 || atlases[0] == null)
                {
                    throw new InvalidOperationException(
                        $"Persisted font graph is incomplete after Apply: '{snapshot.AssetPath}'.");
                }

                ValidatePersistentIdentityOrThrow(
                    fontAsset, snapshot.FontIdentity, $"{snapshot.AssetPath} font");
                ValidatePersistentIdentityOrThrow(
                    fontAsset.material, snapshot.MaterialIdentity, $"{snapshot.AssetPath} material");
                ValidatePersistentIdentityOrThrow(
                    atlases[0], snapshot.AtlasIdentity, $"{snapshot.AssetPath} atlas");
            }
        }

        private static PersistentIdentity CapturePersistentIdentityOrThrow(
            UnityEngine.Object asset,
            string label)
        {
            if (asset == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId))
            {
                throw new InvalidOperationException($"Unable to capture persistent identity for {label}.");
            }

            return new PersistentIdentity(guid, localId);
        }

        private static void ValidatePersistentIdentityOrThrow(
            UnityEngine.Object asset,
            PersistentIdentity expected,
            string label)
        {
            var actual = CapturePersistentIdentityOrThrow(asset, label);
            if (!string.Equals(actual.Guid, expected.Guid, StringComparison.Ordinal) ||
                actual.LocalId != expected.LocalId)
            {
                throw new InvalidOperationException(
                    $"Persistent identity changed for {label}: " +
                    $"{expected.Guid}:{expected.LocalId} -> {actual.Guid}:{actual.LocalId}.");
            }
        }

        private static void ValidateExistingFontTargetPreflightOrThrow(
            string assetPath,
            string sourcePath,
            int expectedPointSize,
            int expectedPadding,
            GlyphRenderMode expectedRenderMode,
            int expectedAtlasWidth,
            int expectedAtlasHeight)
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (fontAsset == null || sourceFont == null)
            {
                throw new InvalidOperationException(
                    $"Governed font target mapping is incomplete: '{assetPath}' -> '{sourcePath}'.");
            }

            if (!MeetsStaticFontContract(fontAsset) ||
                Mathf.RoundToInt(fontAsset.faceInfo.pointSize) != expectedPointSize ||
                fontAsset.atlasPadding != expectedPadding ||
                fontAsset.atlasRenderMode != expectedRenderMode ||
                fontAsset.atlasWidth != expectedAtlasWidth ||
                fontAsset.atlasHeight != expectedAtlasHeight)
            {
                throw new InvalidOperationException(
                    $"Governed font target '{assetPath}' drifted from its canonical Static atlas settings.");
            }

            var serializedFont = new SerializedObject(fontAsset);
            var sourceGuidProperty = serializedFont.FindProperty("m_SourceFontFileGUID");
            var expectedSourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (sourceGuidProperty == null ||
                !string.Equals(sourceGuidProperty.stringValue, expectedSourceGuid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Governed font target '{assetPath}' is not linked to '{sourcePath}'.");
            }
        }

        private static void ValidateTransientSingleAtlasFitOrThrow(
            string sourcePath,
            uint[] corpus,
            int pointSize,
            int padding,
            GlyphRenderMode renderMode,
            int atlasWidth,
            int atlasHeight)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException($"Missing governed source font '{sourcePath}'.");
            }

            if (GovernedSourceFontHashes.TryGetValue(sourcePath, out var expectedHash))
            {
                ValidateSourceFontHashOrThrow(sourcePath, expectedHash);
            }

            var temporary = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                pointSize,
                padding,
                renderMode,
                atlasWidth,
                atlasHeight,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: false);
            if (temporary == null)
            {
                throw new InvalidOperationException(
                    $"TMP could not create a preflight font asset from '{sourcePath}'.");
            }

            try
            {
                if (!temporary.TryAddCharacters(corpus, out var missing, includeFontFeatures: false) ||
                    (missing?.Length ?? 0) != 0)
                {
                    var formatted = string.Join(", ",
                        (missing ?? Array.Empty<uint>()).Select(value => $"U+{value:X4}"));
                    throw new InvalidOperationException(
                        $"'{sourcePath}' cannot supply the governed corpus in preflight. Missing: {formatted}.");
                }

                if (temporary.atlasTextures == null ||
                    temporary.atlasTextures.Length != 1 ||
                    temporary.atlasTextures[0] == null)
                {
                    throw new InvalidOperationException(
                        $"'{sourcePath}' does not fit its governed corpus in one atlas during preflight.");
                }
            }
            finally
            {
                DestroyGeneratedFontObjects(temporary);
            }
        }

        private static void ValidateSourceFontHashOrThrow(string sourcePath, string expectedHash)
        {
            using (var stream = File.OpenRead(Path.GetFullPath(sourcePath)))
            using (var sha256 = SHA256.Create())
            {
                var actualHash = string.Concat(
                    sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
                if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Governed source font hash mismatch for '{sourcePath}'. " +
                        $"Expected {expectedHash}, actual {actualHash}.");
                }
            }
        }

        private static void ValidateStaticFontAsset(string path, ISet<uint> expectedCorpus)
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            ValidateStaticFontAsset(fontAsset, expectedCorpus, path);
        }

        private static void ValidateStaticFontAsset(
            TMP_FontAsset fontAsset,
            ISet<uint> expectedCorpus,
            string label = null)
        {
            if (!MeetsStaticFontContract(fontAsset))
            {
                throw new InvalidOperationException(
                    $"Generated font '{label ?? fontAsset?.name ?? "<null>"}' violates the Static " +
                    "single-atlas, non-readable, no-fallback contract.");
            }

            var actualCorpus = fontAsset.characterTable
                .Select(character => character.unicode)
                .ToHashSet();
            if (!actualCorpus.SetEquals(expectedCorpus))
            {
                var missing = expectedCorpus.Except(actualCorpus).Select(value => $"U+{value:X4}");
                var unexpected = actualCorpus.Except(expectedCorpus).Select(value => $"U+{value:X4}");
                throw new InvalidOperationException(
                    $"Generated font '{label ?? fontAsset.name}' does not exactly match its governed corpus. " +
                    $"Missing: {string.Join(", ", missing)}. Unexpected: {string.Join(", ", unexpected)}.");
            }
        }

        private static void ValidateVisibleMissingGlyphMarkerOrThrow()
        {
            const uint marker = 0x25a1;
            if (TMP_Settings.missingGlyphCharacter != 0 &&
                TMP_Settings.missingGlyphCharacter != marker)
            {
                throw new InvalidOperationException(
                    $"TMP missing-glyph marker must remain U+25A1, not U+{TMP_Settings.missingGlyphCharacter:X4}.");
            }

            if (TMP_Settings.fallbackFontAssets == null || TMP_Settings.fallbackFontAssets.Count != 0)
            {
                throw new InvalidOperationException(
                    "TMP global fallback fonts must remain empty for strict governed-font admission.");
            }

            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont == null || !defaultFont.characterTable.Any(character => character.unicode == marker))
            {
                throw new InvalidOperationException(
                    "TMP default font must directly contain the visible U+25A1 missing-glyph marker.");
            }
        }

        private static bool MeetsStaticFontContract(TMP_FontAsset fontAsset)
        {
            return fontAsset != null &&
                   fontAsset.atlasPopulationMode == AtlasPopulationMode.Static &&
                   !fontAsset.isMultiAtlasTexturesEnabled &&
                   fontAsset.atlasTextures != null &&
                   fontAsset.atlasTextures.Length == 1 &&
                   fontAsset.atlasTextures[0] != null &&
                   !fontAsset.atlasTextures[0].isReadable &&
                   fontAsset.fallbackFontAssetTable != null &&
                   fontAsset.fallbackFontAssetTable.Count == 0;
        }

        private static void ApplyLocale(
            ApprovedLocalizationApplyPlan plan,
            string localeCode)
        {
            foreach (var collectionName in new[] { "UI", "Stage" })
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                if (collection == null)
                {
                    throw new InvalidOperationException(
                        $"Missing String Table collection '{collectionName}'.");
                }

                var identifier = new LocaleIdentifier(localeCode);
                var table = collection.GetTable(identifier) as StringTable;
                if (table == null)
                {
                    throw new InvalidOperationException(
                        $"Could not resolve {collectionName} table for '{localeCode}'.");
                }

                var changed = false;
                var manifest = plan.GetSemanticManifest(collectionName);
                foreach (var pair in manifest.GetExpectedLocaleEntries(localeCode))
                {
                    var expected = pair.Value;
                    var keyId = table.SharedData.GetId(expected.Key);
                    if (keyId == SharedTableData.EmptyId || keyId != expected.KeyId)
                    {
                        throw new InvalidOperationException(
                            $"Shared Table Data drifted for '{collectionName}/{expected.Key}'.");
                    }

                    var entry = table.GetEntry(keyId);
                    if (entry == null)
                    {
                        entry = table.AddEntry(keyId, expected.Value);
                        entry.IsSmart = expected.IsSmart;
                        changed = true;
                    }
                    else
                    {
                        if (!string.Equals(entry.LocalizedValue, expected.Value, StringComparison.Ordinal))
                        {
                            entry.Value = expected.Value;
                            changed = true;
                        }

                        if (entry.IsSmart != expected.IsSmart)
                        {
                            entry.IsSmart = expected.IsSmart;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(table);
                }
            }
        }

        private static void ValidateTargetLocaleTablesOrThrow(
            IReadOnlyList<ApprovedLocalizationRow> rows,
            bool requireExactApprovedState)
        {
            foreach (var collectionName in new[] { "UI", "Stage" })
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                var english = collection?.GetTable(new LocaleIdentifier("en-US")) as StringTable;
                if (collection == null || english == null)
                {
                    throw new InvalidOperationException(
                        $"Missing production English String Table collection '{collectionName}'.");
                }

                var collectionRows = rows
                    .Where(row => string.Equals(row.Collection, collectionName, StringComparison.Ordinal))
                    .ToArray();
                var expectedById = new Dictionary<long, ApprovedLocalizationRow>();
                foreach (var row in collectionRows)
                {
                    var englishEntry = english.GetEntry(row.Key);
                    if (englishEntry == null || expectedById.ContainsKey(englishEntry.KeyId))
                    {
                        throw new InvalidOperationException(
                            $"English source table does not provide a unique canonical key ID for " +
                            $"'{collectionName}/{row.Key}'.");
                    }

                    expectedById.Add(englishEntry.KeyId, row);
                }

                foreach (var locale in TargetLocales)
                {
                    var table = collection.GetTable(new LocaleIdentifier(locale.Code)) as StringTable;
                    if (table == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing governed String Table '{collectionName}/{locale.Code}'.");
                    }

                    var actualById = table.Values.ToDictionary(entry => entry.KeyId);
                    ValidateTargetKeyInventoryOrThrow(
                        actualById.Keys,
                        expectedById.Keys,
                        requireExactApprovedState,
                        $"{collectionName}/{locale.Code}");
                    if (!requireExactApprovedState)
                    {
                        continue;
                    }

                    foreach (var expected in expectedById)
                    {
                        var entry = actualById[expected.Key];
                        var row = expected.Value;
                        var approvedValue = DecodeEscapedNewlines(row.GetTranslation(locale.Column));
                        var englishEntry = english.GetEntry(expected.Key);
                        if (!string.Equals(entry.LocalizedValue, approvedValue, StringComparison.Ordinal) ||
                            entry.IsSmart != englishEntry.IsSmart)
                        {
                            throw new InvalidOperationException(
                                $"Governed String Table '{collectionName}/{locale.Code}' does not exactly " +
                                $"match approved value/Smart state for '{row.Key}'.");
                        }
                    }
                }
            }
        }

        internal static void ValidateTargetKeyInventoryOrThrow(
            IEnumerable<long> actualKeyIds,
            IEnumerable<long> expectedKeyIds,
            bool requireExact,
            string label)
        {
            var actual = new HashSet<long>(
                actualKeyIds ?? throw new ArgumentNullException(nameof(actualKeyIds)));
            var expected = new HashSet<long>(
                expectedKeyIds ?? throw new ArgumentNullException(nameof(expectedKeyIds)));
            var unexpected = actual.Except(expected).OrderBy(value => value).ToArray();
            var missing = requireExact
                ? expected.Except(actual).OrderBy(value => value).ToArray()
                : Array.Empty<long>();
            if (unexpected.Length == 0 && missing.Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Governed String Table '{label}' key inventory is not admissible. " +
                $"Missing IDs: {string.Join(", ", missing)}. " +
                $"Unexpected IDs: {string.Join(", ", unexpected)}.");
        }

        private static IReadOnlyDictionary<string, (TMP_FontAsset Regular, TMP_FontAsset Bold)>
            GenerateStaticFontAssets(IReadOnlyList<ApprovedLocalizationRow> rows)
        {
            var generated = new Dictionary<string, (TMP_FontAsset Regular, TMP_FontAsset Bold)>(
                StringComparer.Ordinal);
            foreach (var locale in FontLocales)
            {
                var corpus = BuildUnicodeCorpus(rows, locale.Column, locale.Autonym);
                var regular = GenerateStaticFontAsset(
                    $"{FontFolder}/{locale.FontPrefix}-Regular.otf",
                    $"{FontFolder}/{locale.FontPrefix}-Regular SDF.asset",
                    corpus);
                var bold = GenerateStaticFontAsset(
                    $"{FontFolder}/{locale.FontPrefix}-Bold.otf",
                    $"{FontFolder}/{locale.FontPrefix}-Bold SDF.asset",
                    corpus);
                generated.Add(locale.Code, (regular, bold));
            }

            return generated;
        }

        private static void RebuildEnglishThemeFonts(
            IReadOnlyList<ApprovedLocalizationRow> rows)
        {
            var corpus = BuildEnglishUnicodeCorpus(rows);
            var themeFonts = new HashSet<TMP_FontAsset>(LoadDistinctThemeFonts("en-US"));
            var rebuiltFonts = new HashSet<TMP_FontAsset>();
            foreach (var mapping in EnglishThemeFonts)
            {
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(mapping.AssetPath);
                var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(mapping.SourcePath);
                if (fontAsset == null || sourceFont == null)
                {
                    throw new InvalidOperationException(
                        $"English strict corpus source mapping is incomplete: '{mapping.AssetPath}' -> " +
                        $"'{mapping.SourcePath}'.");
                }

                if (!themeFonts.Contains(fontAsset))
                {
                    throw new InvalidOperationException(
                        $"Mapped English font '{mapping.AssetPath}' is no longer referenced by the production Theme.");
                }

                var fontCorpus = corpus;
                if (fontAsset == TMP_Settings.defaultFontAsset)
                {
                    fontCorpus = corpus.Append(MissingGlyphMarker).Distinct().OrderBy(value => value).ToArray();
                }

                if (!HasExactStaticCorpus(fontAsset, fontCorpus))
                {
                    RebuildExistingFontAssetUsingCurrentSettings(
                        fontAsset,
                        sourceFont,
                        fontCorpus,
                        mapping.SourcePath,
                        mapping.AssetPath);
                }
                rebuiltFonts.Add(fontAsset);
            }

            if (!rebuiltFonts.SetEquals(themeFonts))
            {
                var unmapped = themeFonts
                    .Except(rebuiltFonts)
                    .Select(font => AssetDatabase.GetAssetPath(font));
                throw new InvalidOperationException(
                    $"Production Theme contains unmapped English fonts: {string.Join(", ", unmapped)}.");
            }
        }

        private static TMP_FontAsset[] LoadDistinctThemeFonts(string localeCode)
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(
                TypographyThemeValidator.ThemeAssetPath);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    $"Missing typography Theme '{TypographyThemeValidator.ThemeAssetPath}'.");
            }

            var fontSet = theme.LocaleFontSets.SingleOrDefault(set =>
                set != null && string.Equals(set.LocaleCode, localeCode, StringComparison.Ordinal));
            if (fontSet == null)
            {
                throw new InvalidOperationException(
                    $"Production Theme has no unique font set for '{localeCode}'.");
            }

            return (fontSet.Entries ?? new List<LocaleFontEntry>())
                .Where(entry => entry != null && entry.FontAsset != null)
                .Select(entry => entry.FontAsset)
                .Distinct()
                .ToArray();
        }

        private static TMP_FontAsset GenerateStaticFontAsset(
            string sourcePath,
            string assetPath,
            uint[] corpus)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException($"Missing source font '{sourcePath}'.");
            }

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing == null)
            {
                throw new InvalidOperationException(
                    $"Production Apply requires the existing canonical font asset '{assetPath}'.");
            }

            if (!HasExactStaticCorpus(existing, corpus))
            {
                RebuildExistingFontAsset(
                    existing, sourceFont, corpus, sourcePath, assetPath);
            }

            return existing;
        }

        private static bool HasExactStaticCorpus(TMP_FontAsset fontAsset, IEnumerable<uint> corpus)
        {
            return MeetsStaticFontContract(fontAsset) &&
                   new HashSet<uint>(fontAsset.characterTable.Select(character => character.unicode))
                       .SetEquals(corpus);
        }

        private static void RebuildExistingFontAsset(
            TMP_FontAsset fontAsset,
            Font sourceFont,
            uint[] corpus,
            string sourcePath,
            string assetPath)
        {
            if (fontAsset.atlasWidth != AtlasSize ||
                fontAsset.atlasHeight != AtlasSize ||
                fontAsset.atlasPadding != AtlasPadding)
            {
                throw new InvalidOperationException(
                    $"Existing generated font '{assetPath}' no longer matches its governed source or atlas settings.");
            }

            AssignSourceFont(fontAsset, sourceFont);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = false;
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            fontAsset.ClearFontAssetData();

            if (!fontAsset.TryAddCharacters(corpus, out var missingUnicodes, includeFontFeatures: false) ||
                (missingUnicodes?.Length ?? 0) != 0)
            {
                var missing = string.Join(", ",
                    (missingUnicodes ?? Array.Empty<uint>()).Select(value => $"U+{value:X4}"));
                throw new InvalidOperationException(
                    $"'{sourcePath}' cannot supply the approved corpus. Missing: {missing}.");
            }

            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length != 1)
            {
                throw new InvalidOperationException(
                    $"'{sourcePath}' did not fit the governed corpus in one {AtlasSize}x{AtlasSize} atlas.");
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.isMultiAtlasTexturesEnabled = false;
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            fontAsset.ReadFontAssetDefinition();
            var atlas = fontAsset.atlasTextures[0];
            atlas.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            if (atlas.isReadable)
            {
                throw new InvalidOperationException($"Generated atlas '{assetPath}' remained CPU-readable.");
            }

            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(fontAsset.material);
            EditorUtility.SetDirty(atlas);
        }

        private static void RebuildExistingFontAssetUsingCurrentSettings(
            TMP_FontAsset fontAsset,
            Font sourceFont,
            uint[] corpus,
            string sourcePath,
            string assetPath)
        {
            if (fontAsset.atlasTextures == null ||
                fontAsset.atlasTextures.Length != 1 ||
                fontAsset.atlasTextures[0] == null)
            {
                throw new InvalidOperationException(
                    $"English font '{assetPath}' must have exactly one atlas before strict corpus regeneration.");
            }

            AssignSourceFont(fontAsset, sourceFont);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = false;
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            fontAsset.ClearFontAssetData();

            if (!fontAsset.TryAddCharacters(corpus, out var missingUnicodes, includeFontFeatures: false) ||
                (missingUnicodes?.Length ?? 0) != 0)
            {
                var missing = string.Join(", ",
                    (missingUnicodes ?? Array.Empty<uint>()).Select(value => $"U+{value:X4}"));
                throw new InvalidOperationException(
                    $"English source '{sourcePath}' cannot supply the strict governed corpus. Missing: {missing}.");
            }

            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length != 1)
            {
                throw new InvalidOperationException(
                    $"English font '{assetPath}' did not fit its strict corpus in the existing single atlas.");
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.isMultiAtlasTexturesEnabled = false;
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            fontAsset.ReadFontAssetDefinition();
            var atlas = fontAsset.atlasTextures[0];
            atlas.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            if (atlas.isReadable)
            {
                throw new InvalidOperationException(
                    $"English atlas '{assetPath}' remained CPU-readable after strict corpus regeneration.");
            }

            ValidateStaticFontAsset(fontAsset, new HashSet<uint>(corpus), assetPath);
            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(fontAsset.material);
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

        private static void DestroyGeneratedFontObjects(TMP_FontAsset fontAsset)
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

        private static uint[] BuildUnicodeCorpus(
            IEnumerable<ApprovedLocalizationRow> rows,
            string translationColumn,
            string autonym)
        {
            var scalars = new SortedSet<uint>();
            for (var value = 32u; value <= 126u; value++)
            {
                scalars.Add(value);
            }

            AddUnicodeScalars(scalars, "□");
            AddUnicodeScalars(scalars, autonym);
            foreach (var row in rows)
            {
                AddUnicodeScalars(scalars, DecodeEscapedNewlines(row.GetTranslation(translationColumn)));
            }

            return scalars.ToArray();
        }

        private static uint[] BuildEnglishUnicodeCorpus(
            IEnumerable<ApprovedLocalizationRow> rows)
        {
            var scalars = new SortedSet<uint>();
            for (var value = 32u; value <= 126u; value++)
            {
                // Orbitron intentionally remains the approved display face and its source does
                // not contain U+005E. Governed English text is still added below, so introducing
                // a caret in a new approved key fails regeneration instead of silently falling back.
                if (value != UnsupportedOrbitronCaret)
                {
                    scalars.Add(value);
                }
            }

            AddUnicodeScalars(scalars, "English");
            scalars.Add(Ellipsis);
            foreach (var row in rows)
            {
                AddUnicodeScalars(scalars, DecodeEscapedNewlines(row.SourceEnglish));
            }

            return scalars.ToArray();
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

        private static string[] SaveAndImportDirtyManagedAssetsOrThrow(
            IEnumerable<string> managedAssetPaths)
        {
            var allowedMutationPaths = new HashSet<string>(
                GetCanonicalManagedFontAssetPaths(),
                StringComparer.Ordinal);
            foreach (var locale in TargetLocales)
            {
                allowedMutationPaths.Add($"Assets/Localization/StringTables/UI/UI_{locale.Code}.asset");
                allowedMutationPaths.Add($"Assets/Localization/StringTables/Stage/Stage_{locale.Code}.asset");
            }

            var dirtyPaths = managedAssetPaths
                .Distinct(StringComparer.Ordinal)
                .Where(path => AssetDatabase.LoadAllAssetsAtPath(path)
                    .Any(asset => asset != null && EditorUtility.IsDirty(asset)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var unexpected = dirtyPaths
                .Where(path => !allowedMutationPaths.Contains(path))
                .ToArray();
            if (unexpected.Length != 0)
            {
                throw new InvalidOperationException(
                    "Production Apply dirtied immutable topology assets: " +
                    string.Join(", ", unexpected));
            }

            foreach (var path in dirtyPaths)
            {
                var dirtyObjects = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Where(asset => asset != null && EditorUtility.IsDirty(asset))
                    .ToArray();
                foreach (var dirtyObject in dirtyObjects)
                {
                    AssetDatabase.SaveAssetIfDirty(dirtyObject);
                }
            }

            foreach (var path in dirtyPaths)
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }

            var stillDirty = dirtyPaths
                .Where(path => AssetDatabase.LoadAllAssetsAtPath(path)
                    .Any(asset => asset != null && EditorUtility.IsDirty(asset)))
                .ToArray();
            if (stillDirty.Length != 0)
            {
                throw new InvalidOperationException(
                    "Targeted production save left dirty managed assets: " +
                    string.Join(", ", stillDirty));
            }

            return dirtyPaths;
        }

        private static string ToProjectRelativePathOrThrow(string fullPath)
        {
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var normalizedFullPath = Path.GetFullPath(fullPath);
            if (!normalizedFullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Path is outside the Unity project: '{fullPath}'.");
            }

            return normalizedFullPath.Substring(projectRoot.Length)
                .Replace(Path.DirectorySeparatorChar, '/');
        }

        private static ApprovedLocalizationApplyPlan BuildValidatedApplyPlan()
        {
            var path = Path.GetFullPath(DraftPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Approved localization Draft was not found.", path);
            }

            return ValidateDraftPreflightOrThrow(
                File.ReadAllText(path, Encoding.UTF8),
                CaptureEnglishInventory());
        }

        internal static ApprovedLocalizationApplyPlan ValidateDraftPreflightOrThrow(
            string csvText,
            IReadOnlyList<EnglishLocalizationInventoryRow> englishInventory)
        {
            if (csvText == null)
            {
                throw new ArgumentNullException(nameof(csvText));
            }

            if (englishInventory == null)
            {
                throw new ArgumentNullException(nameof(englishInventory));
            }

            var records = ParseCsv(csvText);
            if (records.Count < 2)
            {
                throw new InvalidOperationException("Localization Draft has no data rows.");
            }

            var header = records[0];
            var required = new[]
            {
                "collection", "key", "source_en_US", "ja_JP", "zh_CN", "review_state",
            };
            if (!header.SequenceEqual(required, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Localization Draft header does not match the governed schema.");
            }

            var rows = new List<ApprovedLocalizationRow>(records.Count - 1);
            var draftKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 1; index < records.Count; index++)
            {
                var record = records[index];
                if (record.Count != header.Count)
                {
                    throw new InvalidOperationException(
                        $"Localization Draft row {index + 1} has {record.Count} fields; expected {header.Count}.");
                }

                var row = new ApprovedLocalizationRow(
                    record[0], record[1], record[2], record[3], record[4], record[5], index + 1);
                if (string.IsNullOrWhiteSpace(row.Collection) || string.IsNullOrWhiteSpace(row.Key))
                {
                    throw new InvalidOperationException(
                        $"Localization Draft row {row.SourceRowNumber} has an empty collection or key.");
                }

                if (!string.Equals(row.ReviewState, "Approved", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Localization Draft row {row.SourceRowNumber} '{row.Identity}' is not Approved.");
                }

                if (string.IsNullOrWhiteSpace(row.SourceEnglish))
                {
                    throw new InvalidOperationException(
                        $"Localization Draft row {row.SourceRowNumber} '{row.Identity}' has an empty English source.");
                }

                if (string.IsNullOrWhiteSpace(row.Japanese) || string.IsNullOrWhiteSpace(row.SimplifiedChinese))
                {
                    throw new InvalidOperationException(
                        $"Localization Draft row {row.SourceRowNumber} '{row.Identity}' has an empty ja_JP or zh_CN translation.");
                }

                if (!draftKeys.Add(row.Identity))
                {
                    throw new InvalidOperationException(
                        $"Localization Draft contains duplicate row '{row.Identity}'.");
                }

                rows.Add(row);
            }

            var inventoryByKey = new Dictionary<string, EnglishLocalizationInventoryRow>(StringComparer.Ordinal);
            foreach (var inventoryRow in englishInventory)
            {
                if (inventoryRow == null ||
                    string.IsNullOrWhiteSpace(inventoryRow.Collection) ||
                    string.IsNullOrWhiteSpace(inventoryRow.Key))
                {
                    throw new InvalidOperationException(
                        "English production inventory contains an empty collection or key.");
                }

                if (inventoryByKey.ContainsKey(inventoryRow.Identity))
                {
                    throw new InvalidOperationException(
                        $"English production inventory contains duplicate row '{inventoryRow.Identity}'.");
                }

                inventoryByKey.Add(inventoryRow.Identity, inventoryRow);
            }

            var missing = inventoryByKey.Keys.Except(draftKeys).OrderBy(value => value).ToArray();
            var unexpected = draftKeys.Except(inventoryByKey.Keys).OrderBy(value => value).ToArray();
            if (missing.Length != 0 || unexpected.Length != 0)
            {
                throw new InvalidOperationException(
                    "Localization Draft key inventory does not exactly match the production English tables. " +
                    $"Missing: {string.Join(", ", missing)}. Unexpected: {string.Join(", ", unexpected)}.");
            }

            foreach (var row in rows)
            {
                var english = inventoryByKey[row.Identity];
                var approvedSource = DecodeEscapedNewlines(row.SourceEnglish);
                if (!string.Equals(english.Source, approvedSource, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Approved English source drift for '{row.Identity}'. Refresh and re-approve the Draft.");
                }

                ValidateTranslationStructureOrThrow(row, english, "ja_JP", row.Japanese);
                ValidateTranslationStructureOrThrow(row, english, "zh_CN", row.SimplifiedChinese);
            }

            return new ApprovedLocalizationApplyPlan(rows);
        }

        private static IReadOnlyList<EnglishLocalizationInventoryRow> CaptureEnglishInventory()
        {
            var inventory = new List<EnglishLocalizationInventoryRow>();
            foreach (var collectionName in new[] { "UI", "Stage" })
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                var english = collection?.GetTable(new LocaleIdentifier("en-US")) as StringTable;
                if (collection == null || english == null)
                {
                    throw new InvalidOperationException(
                        $"Missing production English String Table collection '{collectionName}'.");
                }

                foreach (var sharedEntry in collection.SharedData.Entries)
                {
                    var entry = english.GetEntry(sharedEntry.Key);
                    if (entry == null)
                    {
                        throw new InvalidOperationException(
                            $"English production table is missing '{collectionName}/{sharedEntry.Key}'.");
                    }

                    inventory.Add(new EnglishLocalizationInventoryRow(
                        collectionName, sharedEntry.Key, entry.LocalizedValue, entry.IsSmart));
                }
            }

            return inventory;
        }

        private static IReadOnlyList<CollectionSemanticManifest>
            CaptureCollectionSemanticManifestsOrThrow(ApprovedLocalizationApplyPlan plan)
        {
            var manifests = new List<CollectionSemanticManifest>();
            foreach (var collectionName in new[] { "UI", "Stage" })
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                if (collection?.SharedData == null)
                {
                    throw new InvalidOperationException(
                        $"Missing governed collection or Shared Data '{collectionName}'.");
                }

                var sharedEntries = collection.SharedData.Entries
                    .Select(entry => new SharedSemanticEntry(entry.Key, entry.Id))
                    .ToArray();
                ThrowIfDuplicateSharedEntries(sharedEntries, collectionName);

                var rows = plan.Rows
                    .Where(row => string.Equals(row.Collection, collectionName, StringComparison.Ordinal))
                    .ToArray();
                var expectedKeys = rows.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
                var actualKeys = sharedEntries.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
                if (!actualKeys.SetEquals(expectedKeys))
                {
                    throw new InvalidOperationException(
                        $"Shared Data '{collectionName}' key inventory does not exactly match the approved Draft.");
                }

                var sharedByKey = sharedEntries.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
                var tableIdentities = new Dictionary<string, PersistentIdentity>(StringComparer.Ordinal);
                var frozenLocales = new Dictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>>(
                    StringComparer.Ordinal);
                foreach (var locale in GovernedLocales)
                {
                    var table = collection.GetTable(new LocaleIdentifier(locale.Code)) as StringTable;
                    if (table == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing governed String Table '{collectionName}/{locale.Code}'.");
                    }

                    ValidatePersistentIdentityOrThrow(
                        table.SharedData,
                        CapturePersistentIdentityOrThrow(collection.SharedData, $"{collectionName} Shared Data"),
                        $"{collectionName}/{locale.Code} Shared Data");
                    tableIdentities.Add(
                        locale.Code,
                        CapturePersistentIdentityOrThrow(table, $"{collectionName}/{locale.Code} table"));

                    var entries = CaptureTableEntriesOrThrow(table, sharedEntries, collectionName, locale.Code);
                    if (locale.Code == "en-US" || locale.Code == "ko-KR")
                    {
                        ValidateExactEntryIdsOrThrow(entries.Keys, sharedEntries.Select(entry => entry.KeyId),
                            $"{collectionName}/{locale.Code}");
                        frozenLocales.Add(locale.Code, entries);
                    }
                }

                var english = frozenLocales["en-US"];
                foreach (var row in rows)
                {
                    var shared = sharedByKey[row.Key];
                    var englishEntry = english[shared.KeyId];
                    if (!string.Equals(
                            englishEntry.Value,
                            DecodeEscapedNewlines(row.SourceEnglish),
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Approved English source drift for '{collectionName}/{row.Key}'.");
                    }
                }

                var expectedTargets = new Dictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>>(
                    StringComparer.Ordinal);
                foreach (var locale in TargetLocales)
                {
                    var values = rows.ToDictionary(
                        row => sharedByKey[row.Key].KeyId,
                        row => new LocalizationSemanticEntry(
                            sharedByKey[row.Key].KeyId,
                            row.Key,
                            DecodeEscapedNewlines(row.GetTranslation(locale.Column)),
                            english[sharedByKey[row.Key].KeyId].IsSmart));
                    expectedTargets.Add(
                        locale.Code,
                        new ReadOnlyDictionary<long, LocalizationSemanticEntry>(values));
                }

                manifests.Add(new CollectionSemanticManifest(
                    collectionName,
                    CapturePersistentIdentityOrThrow(collection.SharedData, $"{collectionName} Shared Data"),
                    sharedEntries,
                    tableIdentities,
                    frozenLocales,
                    expectedTargets));
            }

            return manifests;
        }

        private static void ValidateCollectionSemanticManifestsOrThrow(
            ApprovedLocalizationApplyPlan plan)
        {
            foreach (var manifest in plan.SemanticManifests)
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(manifest.CollectionName);
                if (collection?.SharedData == null)
                {
                    throw new InvalidOperationException(
                        $"Governed collection disappeared after Apply: '{manifest.CollectionName}'.");
                }

                ValidatePersistentIdentityOrThrow(
                    collection.SharedData,
                    manifest.SharedDataIdentity,
                    $"{manifest.CollectionName} Shared Data");
                var sharedEntries = collection.SharedData.Entries
                    .Select(entry => new SharedSemanticEntry(entry.Key, entry.Id))
                    .ToArray();
                if (!sharedEntries.SequenceEqual(manifest.SharedEntries))
                {
                    throw new InvalidOperationException(
                        $"Shared Data semantic inventory changed during Apply: '{manifest.CollectionName}'.");
                }

                foreach (var locale in GovernedLocales)
                {
                    var table = collection.GetTable(new LocaleIdentifier(locale.Code)) as StringTable;
                    if (table == null)
                    {
                        throw new InvalidOperationException(
                            $"Governed table disappeared after Apply: '{manifest.CollectionName}/{locale.Code}'.");
                    }

                    ValidatePersistentIdentityOrThrow(
                        table,
                        manifest.GetTableIdentity(locale.Code),
                        $"{manifest.CollectionName}/{locale.Code} table");
                    ValidatePersistentIdentityOrThrow(
                        table.SharedData,
                        manifest.SharedDataIdentity,
                        $"{manifest.CollectionName}/{locale.Code} Shared Data");
                    var actual = CaptureTableEntriesOrThrow(
                        table, manifest.SharedEntries, manifest.CollectionName, locale.Code);
                    var expected = locale.Code == "en-US" || locale.Code == "ko-KR"
                        ? manifest.GetFrozenLocaleEntries(locale.Code)
                        : manifest.GetExpectedLocaleEntries(locale.Code);
                    if (!SemanticEntriesEqual(actual, expected))
                    {
                        throw new InvalidOperationException(
                            $"Governed String Table semantic state changed unexpectedly: " +
                            $"'{manifest.CollectionName}/{locale.Code}'.");
                    }
                }
            }
        }

        private static IReadOnlyDictionary<long, LocalizationSemanticEntry> CaptureTableEntriesOrThrow(
            StringTable table,
            IEnumerable<SharedSemanticEntry> sharedEntries,
            string collectionName,
            string localeCode)
        {
            var keyById = sharedEntries.ToDictionary(entry => entry.KeyId, entry => entry.Key);
            var values = new Dictionary<long, LocalizationSemanticEntry>();
            foreach (var entry in table.Values)
            {
                if (!keyById.TryGetValue(entry.KeyId, out var key))
                {
                    throw new InvalidOperationException(
                        $"Orphan key ID {entry.KeyId} exists in '{collectionName}/{localeCode}'.");
                }

                if (!values.TryAdd(entry.KeyId, new LocalizationSemanticEntry(
                        entry.KeyId, key, entry.LocalizedValue, entry.IsSmart)))
                {
                    throw new InvalidOperationException(
                        $"Duplicate key ID {entry.KeyId} exists in '{collectionName}/{localeCode}'.");
                }
            }

            return new ReadOnlyDictionary<long, LocalizationSemanticEntry>(values);
        }

        private static void ThrowIfDuplicateSharedEntries(
            IEnumerable<SharedSemanticEntry> entries,
            string collectionName)
        {
            var array = entries.ToArray();
            var duplicateKeys = array.GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            var duplicateIds = array.GroupBy(entry => entry.KeyId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateKeys.Length != 0 || duplicateIds.Length != 0)
            {
                throw new InvalidOperationException(
                    $"Shared Data '{collectionName}' contains duplicate keys or key IDs.");
            }
        }

        private static void ValidateExactEntryIdsOrThrow(
            IEnumerable<long> actual,
            IEnumerable<long> expected,
            string label)
        {
            if (!new HashSet<long>(actual).SetEquals(expected))
            {
                throw new InvalidOperationException(
                    $"Governed String Table '{label}' has a non-exact serialized entry inventory.");
            }
        }

        private static bool SemanticEntriesEqual(
            IReadOnlyDictionary<long, LocalizationSemanticEntry> actual,
            IReadOnlyDictionary<long, LocalizationSemanticEntry> expected)
        {
            return actual.Count == expected.Count && expected.All(pair =>
                actual.TryGetValue(pair.Key, out var value) && value.Equals(pair.Value));
        }

        private static void ValidateGovernedSourceFontHashesOrThrow()
        {
            foreach (var pair in GovernedSourceFontHashes)
            {
                ValidateSourceFontHashOrThrow(pair.Key, pair.Value);
            }
        }

        private static void ValidateTranslationStructureOrThrow(
            ApprovedLocalizationRow row,
            EnglishLocalizationInventoryRow english,
            string localeColumn,
            string encodedTranslation)
        {
            var translation = DecodeEscapedNewlines(encodedTranslation);
            if (CountCharacter(english.Source, '\n') != CountCharacter(translation, '\n'))
            {
                throw new InvalidOperationException(
                    $"Literal newline parity mismatch for '{row.Identity}' in {localeColumn}.");
            }

            var sourceTags = ExtractTmpTagSignature(english.Source);
            var translationTags = ExtractTmpTagSignature(translation);
            if (!sourceTags.SequenceEqual(translationTags, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"TMP tag parity mismatch for '{row.Identity}' in {localeColumn}.");
            }

            var sourceInvariantTokens = ExtractInvariantTokenSignature(english.Source);
            var translationInvariantTokens = ExtractInvariantTokenSignature(translation);
            var sourceIdentifiers = ExtractBracketIdentifierSignature(english.Source);
            var translationIdentifiers = ExtractTranslatedIdentifierSignature(
                translation,
                sourceIdentifiers);
            if (!sourceInvariantTokens.SequenceEqual(
                    translationInvariantTokens,
                    StringComparer.Ordinal) ||
                !sourceIdentifiers.SequenceEqual(
                    translationIdentifiers,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Meaningful number or identifier parity mismatch for '{row.Identity}' in {localeColumn}. " +
                    $"Source numbers: {string.Join(", ", sourceInvariantTokens)}; " +
                    $"source identifiers: {string.Join(", ", sourceIdentifiers)}. " +
                    $"Translation numbers: {string.Join(", ", translationInvariantTokens)}; " +
                    $"translation identifiers: {string.Join(", ", translationIdentifiers)}.");
            }

            if (!english.IsSmart)
            {
                return;
            }

            var formatter = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase?.SmartFormatter;
            var sourceAnalysis = LocalizationStringGovernanceUnityAdapter.AnalyzeEntry(
                row.Key, english.Source, true, formatter);
            var translationAnalysis = LocalizationStringGovernanceUnityAdapter.AnalyzeEntry(
                row.Key, translation, true, formatter);
            if (!sourceAnalysis.SmartAnalysisSucceeded || !translationAnalysis.SmartAnalysisSucceeded)
            {
                throw new InvalidOperationException(
                    $"Smart String parse failed for '{row.Identity}' in {localeColumn}: " +
                    $"source='{sourceAnalysis.SmartAnalysisFailureReason}', " +
                    $"translation='{translationAnalysis.SmartAnalysisFailureReason}'.");
            }

            if (!sourceAnalysis.PlaceholderSignature.Equals(translationAnalysis.PlaceholderSignature))
            {
                throw new InvalidOperationException(
                    $"Indexed placeholder parity mismatch for '{row.Identity}' in {localeColumn}. " +
                    $"Source: {sourceAnalysis.PlaceholderSignature}. Translation: " +
                    $"{translationAnalysis.PlaceholderSignature}.");
            }
        }

        private static int CountCharacter(string value, char target)
        {
            return (value ?? string.Empty).Count(character => character == target);
        }

        private static string[] ExtractTmpTagSignature(string value)
        {
            var tags = new List<string>();
            var text = value ?? string.Empty;
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] != '<')
                {
                    continue;
                }

                var end = text.IndexOf('>', index + 1);
                if (end < 0)
                {
                    throw new InvalidOperationException("Localization text contains an unterminated TMP tag.");
                }

                tags.Add(text.Substring(index, end - index + 1));
                index = end;
            }

            return tags.ToArray();
        }

        private static string[] ExtractInvariantTokenSignature(string value)
        {
            var withoutSmartFields = Regex.Replace(value ?? string.Empty, @"\{[^{}]*\}", string.Empty);
            return Regex.Matches(withoutSmartFields, @"\d+")
                .Cast<Match>()
                .Select(match => match.Value)
                .OrderBy(token => token, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ExtractBracketIdentifierSignature(string value)
        {
            var withoutSmartFields = Regex.Replace(value ?? string.Empty, @"\{[^{}]*\}", string.Empty);
            return Regex.Matches(withoutSmartFields, @"\[([A-Za-z][A-Za-z0-9_-]*)\]")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .OrderBy(token => token, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ExtractTranslatedIdentifierSignature(
            string value,
            IEnumerable<string> expectedIdentifiers)
        {
            var withoutSmartFields = Regex.Replace(value ?? string.Empty, @"\{[^{}]*\}", string.Empty);
            var bracketIdentifiers = new List<string>(
                ExtractBracketIdentifierSignature(withoutSmartFields));
            var withoutBracketIdentifiers = Regex.Replace(
                withoutSmartFields,
                @"\[[A-Za-z][A-Za-z0-9_-]*\]",
                string.Empty);

            foreach (var identifier in expectedIdentifiers)
            {
                var unbracketedPattern =
                    $@"(?<![A-Za-z0-9_]){Regex.Escape(identifier)}(?![A-Za-z0-9_])";
                if (Regex.IsMatch(withoutBracketIdentifiers, unbracketedPattern))
                {
                    bracketIdentifiers.Add(identifier);
                }
            }

            return bracketIdentifiers
                .OrderBy(token => token, StringComparer.Ordinal)
                .ToArray();
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            var quoted = false;

            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (quoted)
                {
                    if (character == '"' && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else if (character == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        field.Append(character);
                    }

                    continue;
                }

                if (character == '"')
                {
                    quoted = true;
                }
                else if (character == ',')
                {
                    record.Add(field.ToString());
                    field.Clear();
                }
                else if (character == '\n')
                {
                    record.Add(field.ToString().TrimEnd('\r'));
                    field.Clear();
                    if (record.Any(value => value.Length > 0))
                    {
                        records.Add(record);
                    }

                    record = new List<string>();
                }
                else
                {
                    field.Append(character);
                }
            }

            if (quoted)
            {
                throw new InvalidOperationException("Localization Draft contains an unterminated quoted field.");
            }

            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString().TrimEnd('\r'));
                records.Add(record);
            }

            return records;
        }

        private static string DecodeEscapedNewlines(string value)
        {
            return (value ?? string.Empty).Replace("\\n", "\n");
        }
    }

    internal sealed class ProductionDirectoryByteFence
    {
        private readonly string projectRelativeRoot;
        private readonly string rootPath;
        private readonly IReadOnlyDictionary<string, byte[]> baseline;

        private ProductionDirectoryByteFence(
            string projectRelativeRoot,
            string rootPath,
            IReadOnlyDictionary<string, byte[]> baseline)
        {
            this.projectRelativeRoot = projectRelativeRoot;
            this.rootPath = rootPath;
            this.baseline = baseline;
        }

        public static ProductionDirectoryByteFence Capture(string projectRelativeRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRelativeRoot) ||
                !projectRelativeRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A project-relative Assets directory is required.",
                    nameof(projectRelativeRoot));
            }

            var fullRoot = Path.GetFullPath(projectRelativeRoot);
            var fullAssetsRoot = Path.GetFullPath("Assets")
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!fullRoot.StartsWith(fullAssetsRoot, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    fullAssetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The byte-fence root must be a bounded child directory of Assets.",
                    nameof(projectRelativeRoot));
            }

            if (!Directory.Exists(fullRoot))
            {
                throw new DirectoryNotFoundException(fullRoot);
            }

            ValidateNoReparsePointsOrThrow(fullRoot);
            var rootMetaPath = fullRoot.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".meta";
            if (!File.Exists(rootMetaPath))
            {
                throw new FileNotFoundException(
                    "The immutable directory root meta file is missing.", rootMetaPath);
            }

            var files = Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories)
                .Append(rootMetaPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetFullPath(path),
                    File.ReadAllBytes,
                    StringComparer.OrdinalIgnoreCase);
            return new ProductionDirectoryByteFence(
                projectRelativeRoot.TrimEnd('/', '\\'),
                fullRoot,
                new ReadOnlyDictionary<string, byte[]>(files));
        }

        public void RestoreOrThrow()
        {
            ValidateNoReparsePointsOrThrow(rootPath);
            var currentPaths = EnumerateCurrentPaths().ToArray();
            var affectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var extraPath in currentPaths.Where(path => !baseline.ContainsKey(path)))
            {
                File.Delete(extraPath);
                affectedPaths.Add(extraPath);
            }

            foreach (var snapshot in baseline)
            {
                var directory = Path.GetDirectoryName(snapshot.Key);
                if (string.IsNullOrEmpty(directory))
                {
                    throw new IOException(
                        $"Addressables rollback target has no parent directory: '{snapshot.Key}'.");
                }

                if (!File.Exists(snapshot.Key) ||
                    !File.ReadAllBytes(snapshot.Key).SequenceEqual(snapshot.Value))
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(snapshot.Key, snapshot.Value);
                    affectedPaths.Add(snapshot.Key);
                }
            }

            foreach (var affectedPath in affectedPaths
                         .Select(ToImportAssetPath)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                AssetDatabase.ImportAsset(
                    affectedPath,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }

            ValidateUnchangedOrThrow();
        }

        public void ValidateUnchangedOrThrow()
        {
            ValidateNoReparsePointsOrThrow(rootPath);
            var currentPaths = EnumerateCurrentPaths()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var baselinePaths = baseline.Keys
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (!currentPaths.SequenceEqual(baselinePaths, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Production Apply changed the Addressables file inventory.");
            }

            var changed = currentPaths
                .Where(path => !File.ReadAllBytes(path).SequenceEqual(baseline[path]))
                .Select(path => path.Substring(rootPath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .ToArray();
            if (changed.Length != 0)
            {
                throw new InvalidOperationException(
                    "Production Apply changed immutable Addressables files: " +
                    string.Join(", ", changed));
            }
        }

        private IEnumerable<string> EnumerateCurrentPaths()
        {
            if (!Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            var rootMetaPath = rootPath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".meta";
            return Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Concat(File.Exists(rootMetaPath)
                    ? new[] { Path.GetFullPath(rootMetaPath) }
                    : Array.Empty<string>());
        }

        private static void ValidateNoReparsePointsOrThrow(string root)
        {
            var candidates = Directory.Exists(root)
                ? new[] { root }
                    .Concat(Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                : new[] { root };
            foreach (var path in candidates)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException(
                            $"Immutable production directory contains a reparse point: '{path}'.");
                    }
                }
            }
        }

        private static string ToImportAssetPath(string fullPath)
        {
            var normalized = fullPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(0, fullPath.Length - ".meta".Length)
                : fullPath;
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Rollback import path is outside the project: '{fullPath}'.");
            }

            return normalized.Substring(projectRoot.Length).Replace(Path.DirectorySeparatorChar, '/');
        }
    }

    internal sealed class ProductionAssetRollbackSnapshot
    {
        private readonly IReadOnlyDictionary<string, byte[]> snapshots;
        private readonly IReadOnlyList<string> assetPaths;

        private ProductionAssetRollbackSnapshot(
            IReadOnlyDictionary<string, byte[]> snapshots,
            IReadOnlyList<string> assetPaths)
        {
            this.snapshots = snapshots;
            this.assetPaths = assetPaths;
        }

        public static ProductionAssetRollbackSnapshot Capture(IEnumerable<string> assetPaths)
        {
            var paths = (assetPaths ?? throw new ArgumentNullException(nameof(assetPaths)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            ValidateAssetGraphsCleanOrThrow(paths);
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var captured = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var assetPath in paths)
            {
                var fullPath = Path.GetFullPath(assetPath);
                if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) ||
                    !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !File.Exists(fullPath))
                {
                    throw new InvalidOperationException(
                        $"Rollback snapshot target is not a validated project asset: '{assetPath}'.");
                }

                captured.Add(assetPath, File.ReadAllBytes(fullPath));
                var metaPath = assetPath + ".meta";
                var fullMetaPath = Path.GetFullPath(metaPath);
                if (!File.Exists(fullMetaPath))
                {
                    throw new InvalidOperationException(
                        $"Rollback snapshot target has no Unity meta file: '{metaPath}'.");
                }

                captured.Add(metaPath, File.ReadAllBytes(fullMetaPath));
            }

            return new ProductionAssetRollbackSnapshot(
                new ReadOnlyDictionary<string, byte[]>(captured),
                new ReadOnlyCollection<string>(paths));
        }

        internal static void ValidateAssetGraphsCleanOrThrow(IEnumerable<string> assetPaths)
        {
            foreach (var assetPath in assetPaths ?? throw new ArgumentNullException(nameof(assetPaths)))
            {
                if (string.IsNullOrEmpty(assetPath) || !File.Exists(Path.GetFullPath(assetPath)))
                {
                    throw new InvalidOperationException(
                        $"Managed production asset is missing on disk before Apply: '{assetPath}'.");
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .Where(asset => asset != null)
                    .ToArray();
                if (assets.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Managed production asset has no loadable Unity objects: '{assetPath}'.");
                }

                var dirtyDescriptions = assets
                    .Where(EditorUtility.IsDirty)
                    .Select(DescribePersistentObject)
                    .ToArray();
                if (dirtyDescriptions.Length != 0)
                {
                    throw new InvalidOperationException(
                        $"Managed production asset has unsaved Editor changes before Apply: '{assetPath}'. " +
                        $"Dirty objects: {string.Join(", ", dirtyDescriptions)}.");
                }
            }
        }

        private static string DescribePersistentObject(UnityEngine.Object asset)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset,
                    out string guid,
                    out long localId))
            {
                return $"{asset.GetType().Name} '{asset.name}' ({guid}:{localId})";
            }

            return $"{asset.GetType().Name} '{asset.name}'";
        }

        public void RestoreOrThrow()
        {
            var failures = new List<Exception>();
            foreach (var assetPath in assetPaths)
            {
                try
                {
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    {
                        if (asset != null)
                        {
                            EditorUtility.ClearDirty(asset);
                        }
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        $"Rollback could not clear dirty state for '{assetPath}'.", exception));
                }
            }

            var changedFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var snapshot in snapshots)
            {
                try
                {
                    var fullPath = Path.GetFullPath(snapshot.Key);
                    if (!File.Exists(fullPath) ||
                        !File.ReadAllBytes(fullPath).SequenceEqual(snapshot.Value))
                    {
                        File.WriteAllBytes(fullPath, snapshot.Value);
                        changedFiles.Add(snapshot.Key);
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        $"Rollback could not restore bytes for '{snapshot.Key}'.", exception));
                }
            }

            foreach (var assetPath in assetPaths.Where(path =>
                         changedFiles.Contains(path) || changedFiles.Contains(path + ".meta")))
            {
                try
                {
                    AssetDatabase.ImportAsset(
                        assetPath,
                        ImportAssetOptions.ForceUpdate |
                        ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        $"Rollback import failed for '{assetPath}'.", exception));
                }
            }

            foreach (var snapshot in snapshots)
            {
                try
                {
                    var actual = File.ReadAllBytes(Path.GetFullPath(snapshot.Key));
                    if (!actual.SequenceEqual(snapshot.Value))
                    {
                        failures.Add(new IOException(
                            $"Rollback verification failed for production file '{snapshot.Key}'."));
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        $"Rollback could not verify production file '{snapshot.Key}'.", exception));
                }
            }

            foreach (var assetPath in assetPaths)
            {
                try
                {
                    if (AssetDatabase.LoadAllAssetsAtPath(assetPath)
                        .Any(asset => asset != null && EditorUtility.IsDirty(asset)))
                    {
                        failures.Add(new IOException(
                            $"Rollback left a dirty imported object for production asset '{assetPath}'."));
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        $"Rollback could not verify dirty state for '{assetPath}'.", exception));
                }
            }

            if (failures.Count != 0)
            {
                throw new AggregateException(
                    "Production rollback was incomplete; the workspace is unsafe and must be discarded or reopened.",
                    failures);
            }
        }

        public void ValidateImmutableFilesUnchangedOrThrow(IEnumerable<string> mutableAssetPaths)
        {
            var mutable = new HashSet<string>(
                mutableAssetPaths ?? throw new ArgumentNullException(nameof(mutableAssetPaths)),
                StringComparer.Ordinal);
            var changed = snapshots
                .Where(snapshot =>
                    !mutable.Contains(snapshot.Key) &&
                    (!File.Exists(Path.GetFullPath(snapshot.Key)) ||
                     !File.ReadAllBytes(Path.GetFullPath(snapshot.Key)).SequenceEqual(snapshot.Value)))
                .Select(snapshot => snapshot.Key)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (changed.Length != 0)
            {
                throw new InvalidOperationException(
                    "Production Apply changed immutable managed files: " + string.Join(", ", changed));
            }
        }
    }

    internal sealed class PersistentIdentity
    {
        public PersistentIdentity(string guid, long localId)
        {
            Guid = guid;
            LocalId = localId;
        }

        public string Guid { get; }

        public long LocalId { get; }
    }

    internal sealed class FontIdentitySnapshot
    {
        public FontIdentitySnapshot(
            string assetPath,
            PersistentIdentity fontIdentity,
            PersistentIdentity materialIdentity,
            PersistentIdentity atlasIdentity)
        {
            AssetPath = assetPath;
            FontIdentity = fontIdentity;
            MaterialIdentity = materialIdentity;
            AtlasIdentity = atlasIdentity;
        }

        public string AssetPath { get; }

        public PersistentIdentity FontIdentity { get; }

        public PersistentIdentity MaterialIdentity { get; }

        public PersistentIdentity AtlasIdentity { get; }
    }

    internal sealed class SharedSemanticEntry : IEquatable<SharedSemanticEntry>
    {
        public SharedSemanticEntry(string key, long keyId)
        {
            Key = key ?? string.Empty;
            KeyId = keyId;
        }

        public string Key { get; }

        public long KeyId { get; }

        public bool Equals(SharedSemanticEntry other)
        {
            return other != null &&
                   KeyId == other.KeyId &&
                   string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as SharedSemanticEntry);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Key != null ? StringComparer.Ordinal.GetHashCode(Key) : 0) * 397) ^
                       KeyId.GetHashCode();
            }
        }
    }

    internal sealed class LocalizationSemanticEntry : IEquatable<LocalizationSemanticEntry>
    {
        public LocalizationSemanticEntry(long keyId, string key, string value, bool isSmart)
        {
            KeyId = keyId;
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
            IsSmart = isSmart;
        }

        public long KeyId { get; }

        public string Key { get; }

        public string Value { get; }

        public bool IsSmart { get; }

        public bool Equals(LocalizationSemanticEntry other)
        {
            return other != null &&
                   KeyId == other.KeyId &&
                   IsSmart == other.IsSmart &&
                   string.Equals(Key, other.Key, StringComparison.Ordinal) &&
                   string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as LocalizationSemanticEntry);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = KeyId.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Key);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Value);
                hashCode = (hashCode * 397) ^ IsSmart.GetHashCode();
                return hashCode;
            }
        }
    }

    internal sealed class CollectionSemanticManifest
    {
        private readonly IReadOnlyDictionary<string, PersistentIdentity> tableIdentities;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>>
            frozenLocales;
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>>
            expectedTargets;

        public CollectionSemanticManifest(
            string collectionName,
            PersistentIdentity sharedDataIdentity,
            IEnumerable<SharedSemanticEntry> sharedEntries,
            IDictionary<string, PersistentIdentity> tableIdentities,
            IDictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>> frozenLocales,
            IDictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>> expectedTargets)
        {
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
            SharedDataIdentity = sharedDataIdentity ?? throw new ArgumentNullException(nameof(sharedDataIdentity));
            SharedEntries = new ReadOnlyCollection<SharedSemanticEntry>(
                (sharedEntries ?? throw new ArgumentNullException(nameof(sharedEntries))).ToArray());
            this.tableIdentities = new ReadOnlyDictionary<string, PersistentIdentity>(
                new Dictionary<string, PersistentIdentity>(tableIdentities, StringComparer.Ordinal));
            this.frozenLocales = new ReadOnlyDictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>>(
                new Dictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>>(
                    frozenLocales, StringComparer.Ordinal));
            this.expectedTargets = new ReadOnlyDictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>>(
                new Dictionary<string, IReadOnlyDictionary<long, LocalizationSemanticEntry>>(
                    expectedTargets, StringComparer.Ordinal));
        }

        public string CollectionName { get; }

        public PersistentIdentity SharedDataIdentity { get; }

        public IReadOnlyList<SharedSemanticEntry> SharedEntries { get; }

        public PersistentIdentity GetTableIdentity(string localeCode) => tableIdentities[localeCode];

        public IReadOnlyDictionary<long, LocalizationSemanticEntry> GetFrozenLocaleEntries(
            string localeCode) => frozenLocales[localeCode];

        public IReadOnlyDictionary<long, LocalizationSemanticEntry> GetExpectedLocaleEntries(
            string localeCode) => expectedTargets[localeCode];
    }

    internal sealed class ApprovedLocalizationApplyPlan
    {
        public ApprovedLocalizationApplyPlan(IEnumerable<ApprovedLocalizationRow> rows)
            : this(rows, Array.Empty<CollectionSemanticManifest>())
        {
        }

        private ApprovedLocalizationApplyPlan(
            IEnumerable<ApprovedLocalizationRow> rows,
            IEnumerable<CollectionSemanticManifest> semanticManifests)
        {
            Rows = new ReadOnlyCollection<ApprovedLocalizationRow>(
                (rows ?? throw new ArgumentNullException(nameof(rows))).ToList());
            SemanticManifests = new ReadOnlyCollection<CollectionSemanticManifest>(
                (semanticManifests ?? throw new ArgumentNullException(nameof(semanticManifests))).ToList());
        }

        public IReadOnlyList<ApprovedLocalizationRow> Rows { get; }

        public IReadOnlyList<CollectionSemanticManifest> SemanticManifests { get; }

        public ApprovedLocalizationApplyPlan WithSemanticManifests(
            IEnumerable<CollectionSemanticManifest> semanticManifests)
        {
            return new ApprovedLocalizationApplyPlan(Rows, semanticManifests);
        }

        public CollectionSemanticManifest GetSemanticManifest(string collectionName)
        {
            var matches = SemanticManifests
                .Where(manifest => string.Equals(
                    manifest.CollectionName, collectionName, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Apply plan requires exactly one semantic manifest for '{collectionName}'.");
            }

            return matches[0];
        }
    }

    internal sealed class ApprovedLocalizationRow
    {
        public ApprovedLocalizationRow(
            string collection,
            string key,
            string sourceEnglish,
            string japanese,
            string simplifiedChinese,
            string reviewState,
            int sourceRowNumber)
        {
            Collection = collection ?? string.Empty;
            Key = key ?? string.Empty;
            SourceEnglish = sourceEnglish ?? string.Empty;
            Japanese = japanese ?? string.Empty;
            SimplifiedChinese = simplifiedChinese ?? string.Empty;
            ReviewState = reviewState ?? string.Empty;
            SourceRowNumber = sourceRowNumber;
        }

        public string Collection { get; }
        public string Key { get; }
        public string SourceEnglish { get; }
        public string Japanese { get; }
        public string SimplifiedChinese { get; }
        public string ReviewState { get; }
        public int SourceRowNumber { get; }
        public string Identity => $"{Collection}/{Key}";

        public string GetTranslation(string column)
        {
            switch (column)
            {
                case "ja_JP":
                    return Japanese;
                case "zh_CN":
                    return SimplifiedChinese;
                default:
                    throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown translation column.");
            }
        }
    }

    internal sealed class EnglishLocalizationInventoryRow
    {
        public EnglishLocalizationInventoryRow(
            string collection,
            string key,
            string source,
            bool isSmart)
        {
            Collection = collection ?? string.Empty;
            Key = key ?? string.Empty;
            Source = source ?? string.Empty;
            IsSmart = isSmart;
        }

        public string Collection { get; }
        public string Key { get; }
        public string Source { get; }
        public bool IsSmart { get; }
        public string Identity => $"{Collection}/{Key}";
    }
}
