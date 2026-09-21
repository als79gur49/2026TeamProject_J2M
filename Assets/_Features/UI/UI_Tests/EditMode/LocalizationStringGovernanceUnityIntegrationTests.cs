using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Localization.Settings;

namespace Game.Feature.UI.Tests
{
    public sealed class LocalizationStringGovernanceUnityIntegrationTests
    {
        private const string DraftHeader =
            "collection,key,source_en_US,ja_JP,zh_CN,review_state\n";

        [Test]
        public void ProductionCjkFonts_MatchApprovedExactCorpusAndStaticResidencyContract()
        {
            Assert.DoesNotThrow(ApprovedLocalizationDraftApplyUtility.ValidateProductionAssetsOrThrow);
        }

        [Test]
        public void ApprovedApplyPreflight_ValidatesIntoImmutableRowsBeforeMutation()
        {
            var inventory = new[]
            {
                new EnglishLocalizationInventoryRow("UI", "ui.test", "Value {0}\n<b>Now</b>", true),
            };
            var csv = DraftHeader +
                      CsvRow("UI", "ui.test", "Value {0}\\n<b>Now</b>",
                          "値 {0}\\n<b>今</b>", "值 {0}\\n<b>现在</b>", "Approved");

            var plan = ApprovedLocalizationDraftApplyUtility.ValidateDraftPreflightOrThrow(csv, inventory);

            Assert.That(plan.Rows, Has.Count.EqualTo(1));
            Assert.That(plan.Rows[0].Identity, Is.EqualTo("UI/ui.test"));
            Assert.That(plan.Rows[0].Japanese, Is.EqualTo("値 {0}\\n<b>今</b>"));
            Assert.That(plan.Rows, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<ApprovedLocalizationRow>>());
        }

        [Test]
        public void ApprovedApplyPreflight_RejectsDuplicateDraftRows()
        {
            var inventory = Inventory("UI", "ui.test", "Value", false);
            var row = CsvRow("UI", "ui.test", "Value", "値", "值", "Approved");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateDraftPreflightOrThrow(
                    DraftHeader + row + row, inventory));

            Assert.That(exception.Message, Does.Contain("duplicate row 'UI/ui.test'"));
        }

        [TestCase("Stage", "stage.unexpected", "Unexpected")]
        [TestCase("UI", "ui.test", "Changed source")]
        public void ApprovedApplyPreflight_RejectsInventoryMismatchOrSourceDrift(
            string collection,
            string key,
            string source)
        {
            var inventory = Inventory("UI", "ui.test", "Value", false);
            var csv = DraftHeader + CsvRow(collection, key, source, "値", "值", "Approved");

            Assert.Throws<InvalidOperationException>(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateDraftPreflightOrThrow(csv, inventory));
        }

        [TestCase("", "值", "Approved", "empty ja_JP or zh_CN")]
        [TestCase("値", "", "Approved", "empty ja_JP or zh_CN")]
        [TestCase("値", "值", "Draft", "is not Approved")]
        public void ApprovedApplyPreflight_RejectsEmptyTranslationsAndUnapprovedRows(
            string japanese,
            string chinese,
            string reviewState,
            string expectedMessage)
        {
            var csv = DraftHeader + CsvRow(
                "UI", "ui.test", "Value", japanese, chinese, reviewState);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateDraftPreflightOrThrow(
                    csv, Inventory("UI", "ui.test", "Value", false)));

            Assert.That(exception.Message, Does.Contain(expectedMessage));
        }

        [TestCase("値 {1}\\n<b>今</b>", "Indexed placeholder parity mismatch")]
        [TestCase("値 {0}<b>今</b>", "Literal newline parity mismatch")]
        [TestCase("値 {0}\\n<i>今</i>", "TMP tag parity mismatch")]
        [TestCase("値 {0}\\n</b>今<b>", "TMP tag parity mismatch")]
        public void ApprovedApplyPreflight_RejectsPlaceholderNewlineAndTmpTagDamage(
            string japanese,
            string expectedMessage)
        {
            var source = "Value {0}\n<b>Now</b>";
            var csv = DraftHeader + CsvRow(
                "UI", "ui.test", "Value {0}\\n<b>Now</b>", japanese,
                "值 {0}\\n<b>现在</b>", "Approved");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateDraftPreflightOrThrow(
                    csv, Inventory("UI", "ui.test", source, true)));

            Assert.That(exception.Message, Does.Contain(expectedMessage));
        }

        [TestCase("18 [B]", "18 [A]")]
        [TestCase("17 [A]", "18 [A]")]
        public void ApprovedApplyPreflight_RejectsMeaningfulNumberOrIdentifierDamage(
            string japanese,
            string chinese)
        {
            const string source = "18 [A]";
            var csv = DraftHeader + CsvRow(
                "UI", "ui.test", source, japanese, chinese, "Approved");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateDraftPreflightOrThrow(
                    csv, Inventory("UI", "ui.test", source, false)));

            Assert.That(exception.Message, Does.Contain("Meaningful number or identifier parity mismatch"));
        }

        [Test]
        public void ApprovedApplyPreflight_AcceptsLocalizedBracketIdentifierPresentation()
        {
            const string source = "Ward[A]-01";
            var csv = DraftHeader + CsvRow(
                "Stage", "stage.test", source, "A病棟-01", "A病区-01", "Approved");

            Assert.DoesNotThrow(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateDraftPreflightOrThrow(
                    csv, Inventory("Stage", "stage.test", source, false)));
        }

        [Test]
        public void ProductionAssetRollbackSnapshot_RestoresExactBytesAndClearsDirtyObjects()
        {
            var assetPath = $"Assets/__LocalizationRollbackProbe_{Guid.NewGuid():N}.txt";
            var fullPath = Path.GetFullPath(assetPath);
            var baseline = new byte[] { 0x4a, 0x32, 0x4d, 0x0a };
            try
            {
                File.WriteAllBytes(fullPath, baseline);
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
                var loaded = AssetDatabase.LoadMainAssetAtPath(assetPath);
                Assert.That(loaded, Is.Not.Null);
                var metaPath = fullPath + ".meta";
                var baselineMeta = File.ReadAllBytes(metaPath);
                var snapshot = ProductionAssetRollbackSnapshot.Capture(new[] { assetPath });

                EditorUtility.SetDirty(loaded);
                File.WriteAllBytes(fullPath, new byte[] { 0x42, 0x41, 0x44 });
                File.WriteAllBytes(metaPath, new byte[] { 0x42, 0x41, 0x44 });
                snapshot.RestoreOrThrow();

                Assert.That(File.ReadAllBytes(fullPath), Is.EqualTo(baseline));
                Assert.That(File.ReadAllBytes(metaPath), Is.EqualTo(baselineMeta));
                Assert.That(
                    AssetDatabase.LoadAllAssetsAtPath(assetPath)
                        .Any(asset => asset != null && EditorUtility.IsDirty(asset)),
                    Is.False);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void ProductionAssetRollbackSnapshot_RejectsChangedMetaAsImmutable()
        {
            var assetPath = $"Assets/__LocalizationImmutableMetaProbe_{Guid.NewGuid():N}.txt";
            var fullPath = Path.GetFullPath(assetPath);
            try
            {
                File.WriteAllText(fullPath, "baseline");
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
                var snapshot = ProductionAssetRollbackSnapshot.Capture(new[] { assetPath });
                File.WriteAllText(fullPath + ".meta", "mutated");

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    snapshot.ValidateImmutableFilesUnchangedOrThrow(new[] { assetPath }));

                Assert.That(exception.Message, Does.Contain(assetPath + ".meta"));
                snapshot.RestoreOrThrow();
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void ProductionAssetRollbackSnapshot_RejectsDirtyEmbeddedSubAssetBeforeCapture()
        {
            var assetPath = $"Assets/__LocalizationDirtyGraphProbe_{Guid.NewGuid():N}.asset";
            try
            {
                var main = UnityEngine.ScriptableObject.CreateInstance<LocalizationRollbackProbeAsset>();
                main.name = "Main";
                AssetDatabase.CreateAsset(main, assetPath);
                var atlas = new UnityEngine.Texture2D(1, 1) { name = "EmbeddedAtlas" };
                AssetDatabase.AddObjectToAsset(atlas, assetPath);
                AssetDatabase.SaveAssetIfDirty(main);
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);

                var loaded = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var loadedMain = loaded.OfType<LocalizationRollbackProbeAsset>().Single();
                var loadedAtlas = loaded.OfType<UnityEngine.Texture2D>().Single();
                Assert.That(EditorUtility.IsDirty(loadedMain), Is.False);
                Assert.That(EditorUtility.IsDirty(loadedAtlas), Is.False);

                EditorUtility.SetDirty(loadedAtlas);
                Assert.That(EditorUtility.IsDirty(loadedMain), Is.False);
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    ProductionAssetRollbackSnapshot.Capture(new[] { assetPath }));

                Assert.That(exception.Message, Does.Contain(assetPath));
                Assert.That(exception.Message, Does.Contain("Texture2D 'EmbeddedAtlas'"));
                Assert.That(EditorUtility.IsDirty(loadedAtlas), Is.True);
            }
            finally
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (asset != null)
                    {
                        EditorUtility.ClearDirty(asset);
                    }
                }

                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void ApprovedApplyTargetInventory_AllowsMissingBeforeMutationButRejectsUnexpectedKeys()
        {
            Assert.DoesNotThrow(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateTargetKeyInventoryOrThrow(
                    new long[] { 10 },
                    new long[] { 10, 20 },
                    requireExact: false,
                    "UI/ja-JP"));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateTargetKeyInventoryOrThrow(
                    new long[] { 10, 99 },
                    new long[] { 10, 20 },
                    requireExact: false,
                    "UI/ja-JP"));

            Assert.That(exception.Message, Does.Contain("Unexpected IDs: 99"));
        }

        [Test]
        public void ApprovedApplyTargetInventory_RequiresExactKeysAfterMutation()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                ApprovedLocalizationDraftApplyUtility.ValidateTargetKeyInventoryOrThrow(
                    new long[] { 10 },
                    new long[] { 10, 20 },
                    requireExact: true,
                    "Stage/zh-CN"));

            Assert.That(exception.Message, Does.Contain("Missing IDs: 20"));
        }

        [Test]
        public void ProductionDirectoryByteFence_RestoresChangedDeletedAndAddedFiles()
        {
            var root = $"Assets/__LocalizationDirectoryRollbackProbe_{Guid.NewGuid():N}";
            var fullRoot = Path.GetFullPath(root);
            var retainedPath = Path.Combine(fullRoot, "retained.txt");
            var retainedMetaPath = retainedPath + ".meta";
            var deletedPath = Path.Combine(fullRoot, "deleted.bin");
            var deletedMetaPath = deletedPath + ".meta";
            var addedPath = Path.Combine(fullRoot, "added.asset");
            var addedMetaPath = addedPath + ".meta";
            var retainedBytes = new byte[] { 0x4a, 0x32, 0x4d };
            var deletedBytes = new byte[] { 0x66, 0x65, 0x6e, 0x63, 0x65 };
            try
            {
                Directory.CreateDirectory(fullRoot);
                File.WriteAllBytes(retainedPath, retainedBytes);
                File.WriteAllBytes(deletedPath, deletedBytes);
                AssetDatabase.ImportAsset(
                    root,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ImportRecursive);
                Assert.That(File.Exists(retainedMetaPath), Is.True);
                Assert.That(File.Exists(deletedMetaPath), Is.True);
                var rootMetaPath = fullRoot + ".meta";
                Assert.That(File.Exists(rootMetaPath), Is.True);
                var rootMetaBytes = File.ReadAllBytes(rootMetaPath);
                var retainedMetaBytes = File.ReadAllBytes(retainedMetaPath);
                var fence = ProductionDirectoryByteFence.Capture(root);

                File.WriteAllBytes(rootMetaPath, new byte[] { 0x42, 0x41, 0x44 });
                File.WriteAllBytes(retainedPath, new byte[] { 0x42, 0x41, 0x44 });
                File.WriteAllBytes(retainedMetaPath, new byte[] { 0x42, 0x41, 0x44 });
                File.Delete(deletedPath);
                File.Delete(deletedMetaPath);
                File.WriteAllBytes(addedPath, new byte[] { 0x4e, 0x45, 0x57 });
                File.WriteAllBytes(addedMetaPath, new byte[] { 0x4e, 0x45, 0x57 });

                fence.RestoreOrThrow();

                Assert.That(File.ReadAllBytes(retainedPath), Is.EqualTo(retainedBytes));
                Assert.That(File.ReadAllBytes(rootMetaPath), Is.EqualTo(rootMetaBytes));
                Assert.That(File.ReadAllBytes(retainedMetaPath), Is.EqualTo(retainedMetaBytes));
                Assert.That(File.ReadAllBytes(deletedPath), Is.EqualTo(deletedBytes));
                Assert.That(File.Exists(deletedMetaPath), Is.True);
                Assert.That(File.Exists(addedPath), Is.False);
                Assert.That(File.Exists(addedMetaPath), Is.False);
                Assert.DoesNotThrow(fence.ValidateUnchangedOrThrow);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                if (Directory.Exists(fullRoot))
                {
                    Directory.Delete(fullRoot, recursive: true);
                }
            }
        }

        [Test]
        public void ProductionAddressableTopology_UsesCanonicalPreloadedLocaleGroups()
        {
            var groupPaths =
                ApprovedLocalizationDraftApplyUtility
                    .ValidateAndCollectGovernedAddressableGroupPathsOrThrow();

            Assert.That(groupPaths, Is.EqualTo(new[]
            {
                "Assets/AddressableAssetsData/AssetGroups/Localization-Assets-Shared.asset",
                "Assets/AddressableAssetsData/AssetGroups/Localization-Locales.asset",
                "Assets/AddressableAssetsData/AssetGroups/" +
                "Localization-String-Tables-Chinese (Simplified) (zh-CN).asset",
                "Assets/AddressableAssetsData/AssetGroups/" +
                "Localization-String-Tables-Japanese (Japan) (ja-JP).asset",
                "Assets/AddressableAssetsData/AssetGroups/Localization-String-Tables-en-US.asset",
                "Assets/AddressableAssetsData/AssetGroups/Localization-String-Tables-ko-KR.asset",
            }));
        }

        [Test]
        public void ApprovedProductionApply_SourceContainsNoTopologyRepairOrGlobalSave()
        {
            var source = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Editor/ApprovedLocalizationDraftApplyUtility.cs");
            foreach (var forbidden in new[]
                     {
                         ".AddNewTable(", "MoveGeneratedLocaleAssets", "AssetDatabase.MoveAsset(",
                         "SetPreloadTableFlag(", "AssetDatabase.SaveAssets(",
                         "AssetDatabase.Refresh(", "EditorUtility.SetDirty(table.SharedData)",
                         "EditorUtility.SetDirty(collection)"
                     })
            {
                Assert.That(source, Does.Not.Contain(forbidden), forbidden);
            }

            Assert.That(source, Does.Contain("AssetDatabase.SaveAssetIfDirty("));
            Assert.That(source, Does.Contain("ProductionDirectoryByteFence.Capture("));
        }

        private static IReadOnlyList<EnglishLocalizationInventoryRow> Inventory(
            string collection,
            string key,
            string source,
            bool isSmart)
        {
            return new[] { new EnglishLocalizationInventoryRow(collection, key, source, isSmart) };
        }

        private static string CsvRow(params string[] fields)
        {
            return string.Join(",", fields.Select(field =>
                $"\"{(field ?? string.Empty).Replace("\"", "\"\"")}\"")) + "\n";
        }

        [Test]
        public void ProductionCatalog_ActualUnityRegistrationIsExactlyCataloguedShipReadySet()
        {
            var catalog = UiLocaleCatalog.CreateProduction();
            var snapshot = LocalizationStringGovernanceUnityAdapter.Capture(catalog);
            var report = new LocalizationStringGovernanceValidator().Validate(snapshot);

            Assert.That(snapshot.RegisteredLocaleCodes, Does.Contain("en-US"));
            Assert.That(snapshot.RegisteredLocaleCodes, Does.Contain("ko-KR"));
            Assert.That(
                snapshot.RegisteredLocaleCodes.Where(code =>
                    !catalog.TryGetEntry(code, out var entry) ||
                    !string.Equals(entry.CanonicalCode, code, StringComparison.Ordinal) ||
                    entry.Lifecycle != LocaleLifecycle.ShipReady),
                Is.Empty,
                "Every registered Unity Locale must resolve to an exact production ShipReady catalog row.");
            Assert.That(
                snapshot.RegisteredLocaleCodes.Intersect(
                    catalog.AuthoringKnownLocales
                        .Where(locale => locale.Lifecycle == LocaleLifecycle.Draft)
                        .Select(locale => locale.CanonicalCode),
                    StringComparer.Ordinal),
                Is.Empty,
                "Production Unity registration must not contain catalog Draft locales.");
            Assert.That(
                snapshot.RegisteredLocaleCodes.Where(code =>
                    !catalog.TryGetEntry(code, out var entry) ||
                    !string.Equals(entry.CanonicalCode, code, StringComparison.Ordinal)),
                Is.Empty,
                "Production Unity registration must not contain uncatalogued locales.");
            Assert.That(
                report.Diagnostics,
                Is.Empty,
                string.Join(Environment.NewLine, report.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.LocaleCode}/{diagnostic.Table}/{diagnostic.Key} - {diagnostic.Message}")));
            Assert.That(report.HasBlockingFailures, Is.False);
        }

        [Test]
        public void GovernedUiProjection_UsesContractsAndDescriptorsWithoutBootstrapOrLocaleBranches()
        {
            var requirements = UiLocalizationRequirementProjection.Create();
            var distinctRequirements = requirements
                .GroupBy(requirement => requirement.Table + "\u001f" + requirement.Key, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var sharedData = UnityEditor.Localization.LocalizationEditorSettings
                .GetStringTableCollection("UI").SharedData.Entries.Select(entry => entry.Key);

            Assert.That(distinctRequirements.Select(requirement => requirement.Key), Is.EquivalentTo(sharedData));
            Assert.That(requirements.Select(requirement => requirement.Owner),
                Has.Some.StartsWith(nameof(SettingsLocalizationContract)));
            Assert.That(requirements.Select(requirement => requirement.Owner),
                Has.Some.StartsWith(nameof(MainMenuLocalizationContract)));

            var source = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/UiLocalizationRequirementProjection.cs");
            Assert.That(source, Does.Not.Contain("SettingsLocalizationAssetBootstrap"));
            Assert.That(source, Does.Not.Contain("en-US"));
            Assert.That(source, Does.Not.Contain("ko-KR"));
        }

        [Test]
        public void StageProjection_UsesActivePresentationKeysAndExplicitLegacyCompatibilityRequirement()
        {
            var snapshot = LocalizationStringGovernanceUnityAdapter.Capture(UiLocaleCatalog.CreateProduction());
            var stageRequirements = snapshot.Requirements
                .Where(requirement => requirement.Table == StageDisplayNameKeys.Table)
                .ToArray();
            var sequence = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);

            Assert.That(sequence, Is.Not.Null);
            Assert.That(stageRequirements.Length, Is.EqualTo(sequence.Entries.Count + 1));
            Assert.That(stageRequirements.Select(requirement => requirement.Key),
                Does.Contain(LocalizationStringGovernanceUnityAdapter.CompatibilityRetainedStageDisplayNameKey));
            Assert.That(stageRequirements.Single(requirement =>
                    requirement.Key == LocalizationStringGovernanceUnityAdapter.CompatibilityRetainedStageDisplayNameKey).Owner,
                Does.Contain("Compatibility-retained"));
        }

        [Test]
        public void SmartFormatterAnalysis_NormalizesOrderMultiplicityAndEscapedBraces()
        {
            var formatter = LocalizationSettings.StringDatabase.SmartFormatter;

            var analyzed = LocalizationStringGovernanceUnityAdapter.AnalyzeEntry(
                "key", "{1} / {0} / {1} / {{literal}}", true, formatter);

            Assert.That(analyzed.SmartAnalysisSucceeded, Is.True);
            Assert.That(analyzed.PlaceholderSignature,
                Is.EqualTo(PlaceholderSignature.FromSelectors(new[] { "0", "1", "1" })));
        }

        [Test]
        public void SmartFormatterAnalysis_MalformedInputDoesNotEscapeAsException()
        {
            var formatter = LocalizationSettings.StringDatabase.SmartFormatter;

            var analyzed = LocalizationStringGovernanceUnityAdapter.AnalyzeEntry(
                "key", "broken {0", true, formatter);

            Assert.That(analyzed.SmartAnalysisSucceeded, Is.False);
            Assert.That(analyzed.SmartAnalysisFailureReason, Is.Not.Empty);
        }

        [Test]
        public void SmartFormatterAnalysis_NonSmartLiteralBracesDoNotRequireParser()
        {
            var analyzed = LocalizationStringGovernanceUnityAdapter.AnalyzeEntry(
                "key", "literal { brace", false, null);

            Assert.That(analyzed.SmartAnalysisSucceeded, Is.True);
            Assert.That(analyzed.PlaceholderSignature, Is.EqualTo(PlaceholderSignature.Empty));
            Assert.That(analyzed.SmartAnalysisFailureReason, Is.Empty);
        }

        [Test]
        public void AdapterSource_IsReadOnlyAndPackageBoundaryRemainsOneWay()
        {
            var adapterSource = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Editor/LocalizationStringGovernanceUnityAdapter.cs");
            foreach (var forbidden in new[]
                     {
                         ".AddLocale(", ".CreateStringTableCollection(", ".AddNewTable(",
                         ".AddEntry(", ".RemoveEntry(", ".SetPreloadTableFlag(",
                         "EditorUtility.SetDirty(", "AssetDatabase.SaveAssets(",
                         "AssetDatabase.Refresh("
                     })
            {
                Assert.That(adapterSource, Does.Not.Contain(forbidden), forbidden);
            }

            Assert.That(adapterSource, Does.Not.Contain("SettingsLocalizationAssetBootstrap"));
            Assert.That(adapterSource, Does.Not.Contain("Addressable"));
            Assert.That(adapterSource, Does.Not.Contain("en-US"));
            Assert.That(adapterSource, Does.Not.Contain("ko-KR"));
            var viewSharedSource = File.ReadAllText(
                "Assets/_Features/UI/UI_ViewShared/Runtime/LocalizationStringGovernance.cs");
            Assert.That(viewSharedSource, Does.Not.Contain("UnityEngine"));
            Assert.That(viewSharedSource, Does.Not.Contain("LocalizationSettings"));
            Assert.That(viewSharedSource, Does.Not.Contain("LocalizationEditorSettings"));
            Assert.That(viewSharedSource, Does.Not.Contain("Addressables"));
        }
    }

    internal sealed class LocalizationRollbackProbeAsset : UnityEngine.ScriptableObject
    {
    }
}
