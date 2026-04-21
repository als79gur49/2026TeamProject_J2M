using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCompatUsageReportingTests
    {
        private const string CatalogAssetPath = "Assets/_Features/Stages/Content/StageCatalog.asset";
        private const string KnownWarningLedgerAssetPath =
            "Assets/_Features/Stages/Editor/Validation/StageCatalogKnownWarningLedger.asset";
        private const string AliasGovernanceLedgerAssetPath =
            StageAliasGovernanceUpdater.DefaultAliasGovernanceLedgerAssetPath;
        private const string GameplayGuidePath = "Docs/Testing/Gameplay-Test-Automation-Guide.md";
        private const string CloseNotePath = "Docs/Testing/Stage-Content-P2-Close-Hardening-2026-04-22.md";
        private const string DefaultStageIdContractPath =
            "Docs/Testing/Stage-DefaultStageId-Editor-Direct-Play-Contract.md";

        [Test]
        public void Audit_ReportsNoBuildSceneCompatResidue_AndGrandfatherCountRemainsLocked()
        {
            var report = new StageCompatUsageAuditor().Audit();

            Assert.That(report.Snapshot.CanonicalGameplayAssetGuids.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(report.CanonicalGameplayWithLegacyPresentationIds, Is.Empty);
            Assert.That(report.BuildSceneResiduePaths, Is.Empty);
            Assert.That(report.GrandfatherGameplayAssetCount, Is.EqualTo(2));
            Assert.That(report.GrandfatherCountMatchesExpected, Is.True);
        }

        [Test]
        public void KnownWarningLedger_MatchesCurrentCatalogWarningExactSet()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogAssetPath);
            var ledger = AssetDatabase.LoadAssetAtPath<StageCatalogKnownWarningLedger>(KnownWarningLedgerAssetPath);
            var catalogReport = new StageCatalogValidator().Validate(catalog, new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireClearEvaluationDefinition = true,
                RequireRewardDefinition = true,
                RequireProgressionDefinition = true,
                Timing = StageValidationTiming.TestOrCi,
                Phase = StageValidationPhase.Phase5_Hardening,
                GrandfatherGameplayAssetGuids = GrandfatherGameplayAssetGuidRegistry.CreateSet(),
            });

            var governanceReport = new StageCatalogKnownWarningValidator().Validate(catalog, catalogReport, ledger);

            Assert.That(governanceReport.Issues, Is.Empty);
            Assert.That(catalogReport.Issues.Count(issue => issue.Severity == StageValidationSeverity.Warning), Is.EqualTo(2));
        }

        [Test]
        public void KnownWarningLedger_PathDriftRequiresLedgerUpdate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogAssetPath);
            var sourceLedger = AssetDatabase.LoadAssetAtPath<StageCatalogKnownWarningLedger>(KnownWarningLedgerAssetPath);
            var catalogReport = new StageCatalogValidator().Validate(catalog, new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireClearEvaluationDefinition = true,
                RequireRewardDefinition = true,
                RequireProgressionDefinition = true,
                Timing = StageValidationTiming.TestOrCi,
                Phase = StageValidationPhase.Phase5_Hardening,
                GrandfatherGameplayAssetGuids = GrandfatherGameplayAssetGuidRegistry.CreateSet(),
            });
            var ledger = ScriptableObject.CreateInstance<StageCatalogKnownWarningLedger>();
            var entries = sourceLedger.Entries.ToArray();
            entries[0].ExpectedAssetPath = "Assets/_Features/Stages/Content/renamed.asset";
            ledger.SetEntries(entries);

            try
            {
                var governanceReport = new StageCatalogKnownWarningValidator().Validate(catalog, catalogReport, ledger);
                Assert.That(governanceReport.Issues.Any(issue => issue.Code == "known-warning.asset-path-drift"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ledger);
            }
        }

        [Test]
        public void AliasGovernanceLedger_MatchesAliasTableExactSet()
        {
            var aliasTable = AssetDatabase.LoadAssetAtPath<StageIdAliasTable>("Assets/_Features/Stages/Content/StageIdAliasTable.asset");
            var ledger = AssetDatabase.LoadAssetAtPath<StageAliasGovernanceLedger>(AliasGovernanceLedgerAssetPath);

            var governanceReport = new StageAliasGovernanceValidator().Validate(aliasTable, ledger);

            Assert.That(governanceReport.Issues, Is.Empty);
            Assert.That(aliasTable.Entries.Count, Is.EqualTo(3));
        }

        [Test]
        public void AliasGovernanceLedger_MetadataDriftRequiresLedgerUpdate()
        {
            var aliasTable = AssetDatabase.LoadAssetAtPath<StageIdAliasTable>("Assets/_Features/Stages/Content/StageIdAliasTable.asset");
            var sourceLedger = AssetDatabase.LoadAssetAtPath<StageAliasGovernanceLedger>(AliasGovernanceLedgerAssetPath);
            var ledger = ScriptableObject.CreateInstance<StageAliasGovernanceLedger>();
            var entries = sourceLedger.Entries.ToArray();
            entries[0].Owner = string.Empty;
            ledger.SetEntries(entries);

            try
            {
                var governanceReport = new StageAliasGovernanceValidator().Validate(aliasTable, ledger);
                Assert.That(governanceReport.Issues.Any(issue => issue.Code == "alias-governance.ledger.metadata-missing"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ledger);
            }
        }

        [Test]
        public void GameplayGuide_RecordsStageContentReportingWording_WithoutBroadGreenClaims()
        {
            var guide = ReadRepoFile(GameplayGuidePath);

            Assert.That(guide, Does.Contain("Stage content refactor reporting wording"));
            Assert.That(guide, Does.Contain("P2 close validated"));
            Assert.That(guide, Does.Contain("targeted architecture/CI validated"));
            Assert.That(guide, Does.Contain("broad project-wide regression validated"));
            Assert.That(guide, Does.Contain("project-wide green"));
            Assert.That(guide, Does.Contain("Stage-Content-P2-Close-Hardening-2026-04-22.md"));
            Assert.That(guide, Does.Contain("Stage-DefaultStageId-Editor-Direct-Play-Contract.md"));
        }

        [Test]
        public void CloseNote_RecordsBoundedEvidence_AndP3EntryGates()
        {
            var closeNote = ReadRepoFile(CloseNotePath);

            Assert.That(closeNote, Does.Contain("# Stage Content P2 Close Hardening 2026-04-22"));
            Assert.That(closeNote, Does.Contain("`./run_tests.sh core`: green"));
            Assert.That(closeNote, Does.Contain("Stage catalog CI validation passed."));
            Assert.That(closeNote, Does.Contain("`./run_tests.sh full`: 이 close note의 근거로 실행하지 않았다."));
            Assert.That(closeNote, Does.Contain("## P3 Entry Gates"));
            Assert.That(closeNote, Does.Contain("P3-A bridge sunset"));
            Assert.That(closeNote, Does.Contain("P3-E broad verification"));
            Assert.That(closeNote, Does.Contain("project-wide green"));
        }

        [Test]
        public void DefaultStageIdContract_DocumentsEditorOnlyFallbackAndSunsetCriteria()
        {
            var contract = ReadRepoFile(DefaultStageIdContractPath);

            Assert.That(contract, Does.Contain("# Stage defaultStageId Editor Direct-Play Contract"));
            Assert.That(contract, Does.Contain("production runtime source-of-truth는 launch context다."));
            Assert.That(contract, Does.Contain("CreateLaunchContextOnly"));
            Assert.That(contract, Does.Contain("CreateEditorDirectPlayFallback"));
            Assert.That(contract, Does.Contain("Application.isEditor == false"));
            Assert.That(contract, Does.Contain("## Sunset Criteria"));
            Assert.That(contract, Does.Contain("manual scene play without launch context"));
            Assert.That(contract, Does.Contain("runtime fallback support"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
