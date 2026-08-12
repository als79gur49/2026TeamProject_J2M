using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignStageSequenceValidatorTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null && !AssetDatabase.Contains(_createdObjects[i]))
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void AuthoritativeProductionAsset_TypedLoadsAndResolvesEverySequenceEntry()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            var validation = CampaignStageSequenceProductionValidation.Validate(
                catalog,
                StageValidationTiming.TestOrCi);

            Assert.That(validation.Definition, Is.Not.Null);
            Assert.That(validation.SourcePath, Is.EqualTo(CampaignStageSequenceAssetLoader.CanonicalAssetPath));
            Assert.That(
                AssetDatabase.AssetPathToGUID(validation.SourcePath),
                Is.EqualTo(CampaignStageSequenceAssetLoader.CanonicalAssetGuid));
            Assert.That(validation.EntryCount, Is.GreaterThan(0));
            Assert.That(validation.SourceReport.HasErrors, Is.False, FormatIssues(validation.SourceReport));
            Assert.That(validation.AuthoritativeReport.HasErrors, Is.False, FormatIssues(validation.AuthoritativeReport));
            var sequenceResolver = new CampaignStageSequenceResolver(validation.Definition);
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath);
            var catalogResolver = new StageCatalogResolver(provider);
            foreach (var entry in sequenceResolver.Entries)
            {
                Assert.That(catalogResolver.TryResolve(entry.StageId, out var catalogEntry), Is.True);
                Assert.That(catalogEntry, Is.Not.Null);
                Assert.That(catalogEntry.CampaignParticipation, Is.EqualTo(CampaignParticipation.Campaign));
                Assert.That(sequenceResolver.GetLevelGroupId(entry.StageId), Is.Not.Empty);
            }

            var catalogOnly = catalog.Entries.Single(entry => entry.StageId.Value == "legacy-stage-5-1");
            Assert.That(catalogOnly.CampaignParticipation, Is.EqualTo(CampaignParticipation.CatalogOnly));
            Assert.That(catalogOnly.CatalogOnlyReason, Is.EqualTo(CatalogOnlyReason.LegacyArchived));
        }

        [Test]
        public void SequenceEntrySchema_OwnsOnlyStageIdAndLevelGroup()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                CampaignStageSequenceAssetLoader.CanonicalAssetPath);
            var serialized = new SerializedObject(definition);
            var entry = serialized.FindProperty("entries").GetArrayElementAtIndex(0);

            Assert.That(entry.FindPropertyRelative("stageId"), Is.Not.Null);
            Assert.That(entry.FindPropertyRelative("levelGroupId"), Is.Not.Null);
            Assert.That(entry.FindPropertyRelative("displayName"), Is.Null);
        }

        [Test]
        public void AuthoritativeValidation_RejectsNullAndZeroEntrySources()
        {
            var validator = new CampaignStageSequenceValidator();
            var catalog = CreateCatalog(CreateAliasTable());

            var nullReport = validator.ValidateAuthoritativeAsset(
                null,
                catalog.Entries,
                catalog.StageIdAliasTable,
                StageValidationTiming.TestOrCi);
            var emptyDefinition = CreateDefinition();
            var emptyReport = validator.ValidateAuthoritativeAsset(
                emptyDefinition,
                catalog.Entries,
                catalog.StageIdAliasTable,
                StageValidationTiming.TestOrCi);

            AssertCode(nullReport, "campaign-sequence.authoritative.source-null");
            AssertCode(emptyReport, "campaign-sequence.authoritative.entries-empty");
        }

        [Test]
        public void AuthoritativeValidation_RejectsEmptyDuplicateAndUnknownStageIds()
        {
            var validator = new CampaignStageSequenceValidator();
            var aliasTable = CreateAliasTable();
            var knownStageId = CreateUniqueStageId("known");
            var unknownStageId = CreateUniqueStageId("unknown");
            var catalog = CreateCatalog(aliasTable, knownStageId);

            var emptyStageIdReport = validator.ValidateAuthoritativeAsset(
                CreateDefinition(CreateEntry(StageId.None, "level-test")),
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);
            var duplicateReport = validator.ValidateAuthoritativeAsset(
                CreateDefinition(
                    CreateEntry(StageId.CreateOrThrow(knownStageId), "level-test"),
                    CreateEntry(StageId.CreateOrThrow(knownStageId), "level-test")),
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);
            var unknownReport = validator.ValidateAuthoritativeAsset(
                CreateDefinition(CreateEntry(StageId.CreateOrThrow(unknownStageId), "level-test")),
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);

            AssertCode(emptyStageIdReport, "campaign-sequence.authoritative.stage-id-invalid");
            AssertCode(duplicateReport, "campaign-sequence.authoritative.stage-id-duplicate");
            AssertCode(unknownReport, "campaign-sequence.authoritative.catalog-missing");
        }

        [Test]
        public void AuthoritativeValidation_RejectsAliasSourceAndEmptyOrInvalidLevelGroup()
        {
            var canonicalStageId = CreateUniqueStageId("canonical");
            var aliasStageId = CreateUniqueStageId("alias");
            var aliasTable = CreateAliasTable(new StageIdAliasEntry
            {
                DeprecatedStageId = aliasStageId,
                CurrentStageId = StageId.CreateOrThrow(canonicalStageId),
            });
            var catalog = CreateCatalog(aliasTable, canonicalStageId);
            var validator = new CampaignStageSequenceValidator();

            var aliasReport = validator.ValidateAuthoritativeAsset(
                CreateDefinition(CreateEntry(StageId.CreateOrThrow(aliasStageId), "level-test")),
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);
            var invalidGroupReport = validator.ValidateAuthoritativeAsset(
                CreateDefinition(CreateEntry(StageId.CreateOrThrow(canonicalStageId), "Invalid Level Group!")),
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);
            var emptyGroupReport = validator.ValidateAuthoritativeAsset(
                CreateDefinition(CreateEntry(StageId.CreateOrThrow(canonicalStageId), string.Empty)),
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);

            AssertCode(aliasReport, "campaign-sequence.authoritative.stage-id-alias");
            AssertCode(emptyGroupReport, "campaign-sequence.authoritative.level-group-empty");
            AssertCode(invalidGroupReport, "campaign-sequence.authoritative.level-group-invalid");
        }

        [Test]
        public void AuthoritativeValidation_RejectsNoncontiguousLevelGroup()
        {
            var stageIds = new[]
            {
                CreateUniqueStageId("group-a-first"),
                CreateUniqueStageId("group-b"),
                CreateUniqueStageId("group-a-second"),
            };
            var aliasTable = CreateAliasTable();
            var catalog = CreateCatalog(aliasTable, stageIds);
            var report = new CampaignStageSequenceValidator().ValidateAuthoritativeAsset(
                CreateDefinition(
                    CreateEntry(StageId.CreateOrThrow(stageIds[0]), "level-a"),
                    CreateEntry(StageId.CreateOrThrow(stageIds[1]), "level-b"),
                    CreateEntry(StageId.CreateOrThrow(stageIds[2]), "level-a")),
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);

            AssertCode(report, "campaign-sequence.authoritative.level-group-noncontiguous");
        }

        [Test]
        public void AuthoritativeValidation_BlocksEligibilityCoverageMismatches()
        {
            var campaignStageId = CreateUniqueStageId("campaign-missing");
            var catalogOnlyStageId = CreateUniqueStageId("catalog-only-sequenced");
            var unsetStageId = CreateUniqueStageId("unset");
            var aliasTable = CreateAliasTable();
            var catalog = CreateCatalog(aliasTable, campaignStageId, catalogOnlyStageId, unsetStageId);
            catalog.Entries[1].AssignCampaignParticipation(
                CampaignParticipation.CatalogOnly,
                CatalogOnlyReason.LegacyArchived);
            catalog.Entries[2].AssignCampaignParticipation(CampaignParticipation.Unspecified);
            var validator = new CampaignStageSequenceValidator();
            var report = validator.ValidateAuthoritativeAsset(
                CreateDefinition(CreateEntry(StageId.CreateOrThrow(catalogOnlyStageId), "level-test")),
                catalog.Entries,
                aliasTable,
                StageValidationTiming.TestOrCi);

            AssertCode(report, "campaign-sequence.authoritative.eligible-catalog-entry-unsequenced");
            AssertCode(report, "campaign-sequence.authoritative.catalog-only-entry-sequenced");
            AssertCode(report, "campaign-sequence.authoritative.catalog-eligibility-unset");
        }

        [Test]
        public void PrebuildValidation_UsesProductionSequenceAndPropagatesSequenceErrors()
        {
            Assert.DoesNotThrow(() => StageCatalogBuildValidationHook.ValidateForBuild(null));

            var productionDefinition = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                CampaignStageSequenceAssetLoader.CanonicalAssetPath);
            var authoritativeReport = new StageValidationReport();
            authoritativeReport.Add(
                StageValidationSeverity.Error,
                "campaign-sequence.authoritative.synthetic-prebuild-error",
                "Synthetic sequence error used to prove prebuild propagation.");
            var injected = new CampaignStageSequenceProductionValidation(
                productionDefinition,
                new StageValidationReport(),
                authoritativeReport,
                CampaignStageSequenceAssetLoader.CanonicalAssetPath,
                CampaignStageSequenceAssetLoader.CanonicalAssetGuid);

            var exception = Assert.Throws<BuildFailedException>(
                () => StageCatalogBuildValidationHook.ValidateForBuild(injected));
            Assert.That(exception.Message, Does.Contain("campaign-sequence.authoritative.synthetic-prebuild-error"));
        }

        [Test]
        public void CampaignMainContentSmoke_ExercisesSequenceResolverSamples()
        {
            var result = StageCampaignMainContentSmokeCheck.Run();

            Assert.That(result, Is.EqualTo(0));
            Assert.That(File.Exists(StageCampaignMainContentSmokeCheck.ReportPath), Is.True);
            var report = File.ReadAllText(StageCampaignMainContentSmokeCheck.ReportPath);
            Assert.That(report, Does.Contain("CampaignSequenceEntryCount="));
            Assert.That(report, Does.Not.Contain("legacy-parity"));
        }

        private StageCatalog CreateCatalog(StageIdAliasTable aliasTable, params string[] stageIdValues)
        {
            var catalog = Track(ScriptableObject.CreateInstance<StageCatalog>());
            var entries = new StageContentEntry[stageIdValues.Length];
            for (var i = 0; i < stageIdValues.Length; i++)
            {
                var entry = Track(ScriptableObject.CreateInstance<StageContentEntry>());
                entry.AssignStageId(StageId.CreateOrThrow(stageIdValues[i]));
                entry.AssignGameplayDefinition(Track(ScriptableObject.CreateInstance<StageDefinition>()));
                entry.AssignCampaignParticipation(CampaignParticipation.Campaign);
                entries[i] = entry;
            }

            catalog.SetEntries(entries);
            catalog.AssignStageIdAliasTable(aliasTable);
            return catalog;
        }

        private StageIdAliasTable CreateAliasTable(params StageIdAliasEntry[] entries)
        {
            var aliasTable = Track(ScriptableObject.CreateInstance<StageIdAliasTable>());
            aliasTable.SetEntries(entries);
            return aliasTable;
        }

        private CampaignStageSequenceDefinition CreateDefinition(params CampaignStageSequenceEntry[] entries)
        {
            var definition = Track(ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>());
            definition.SetEntries(entries);
            return definition;
        }

        private static CampaignStageSequenceEntry CreateEntry(
            StageId stageId,
            string levelGroupId)
        {
            var entry = new CampaignStageSequenceEntry();
            entry.Set(stageId, levelGroupId);
            return entry;
        }

        private T Track<T>(T value)
            where T : UnityEngine.Object
        {
            _createdObjects.Add(value);
            return value;
        }

        private static string CreateUniqueStageId(string suffix)
        {
            return $"sequence-test-{suffix}-{Guid.NewGuid():N}";
        }

        private static void AssertCode(StageValidationReport report, string expectedCode)
        {
            Assert.That(
                report.Issues.Any(issue => string.Equals(issue.Code, expectedCode, StringComparison.Ordinal)),
                Is.True,
                FormatIssues(report));
        }

        private static string FormatIssues(StageValidationReport report)
        {
            return string.Join(
                Environment.NewLine,
                report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }
    }
}
