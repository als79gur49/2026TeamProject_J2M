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
        private const string CloseNotePath = "Docs/Testing/Stage-Content-P3-Sunset-2026-04-22.md";
        private const string DirectPlayContractPath =
            "Docs/Testing/Stage-DefaultStageId-Editor-Direct-Play-Contract.md";

        [Test]
        public void Audit_ReportsNoBuildSceneResidue_NoDuplicateLegacyAssets_AndNoAliasUsageHits()
        {
            var report = new StageCompatUsageAuditor().Audit();

            Assert.That(report.Snapshot.CanonicalGameplayAssetGuids.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(report.CanonicalGameplayWithLegacyPresentationIds, Is.Empty);
            Assert.That(report.NonCanonicalGameplayWithLegacyPresentationIds, Is.Empty);
            Assert.That(report.BuildSceneResiduePaths, Is.Empty);
            Assert.That(report.BuildSceneCoverageGapPaths, Is.Empty);
            Assert.That(report.DuplicateLegacyGameplayAssetPaths, Is.Empty);
            Assert.That(report.AliasUsage.Hits, Is.Empty);
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
                Phase = StageValidationPhase.Phase6_SunsetFinalization,
            });

            var governanceReport = new StageCatalogKnownWarningValidator().Validate(catalog, catalogReport, ledger);

            Assert.That(governanceReport.Issues, Is.Empty);
            Assert.That(catalogReport.Issues.Any(issue => issue.Code == "gameplay.name-drift"), Is.False);
        }

        [Test]
        public void KnownWarningLedger_RejectsUnexpectedRows_WhenCatalogWarningsAreZero()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogAssetPath);
            var catalogReport = new StageCatalogValidator().Validate(catalog, new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireClearEvaluationDefinition = true,
                RequireRewardDefinition = true,
                RequireProgressionDefinition = true,
                Timing = StageValidationTiming.TestOrCi,
                Phase = StageValidationPhase.Phase6_SunsetFinalization,
            });
            var ledger = ScriptableObject.CreateInstance<StageCatalogKnownWarningLedger>();
            ledger.SetEntries(new[]
            {
                new StageCatalogKnownWarningEntry
                {
                    IssueCode = "gameplay.name-drift",
                    AssetGuid = "synthetic-guid",
                    ExpectedAssetPath = "Assets/_Features/Stages/Content/synthetic-stage/synthetic-stage.asset",
                    ExpectedAssetName = "synthetic-stage",
                    ExpectedStageId = "synthetic-stage",
                    Owner = "stage-content-refactor",
                    Reason = "synthetic test row",
                    RemovalGate = "test-only",
                },
            });

            try
            {
                var governanceReport = new StageCatalogKnownWarningValidator().Validate(catalog, catalogReport, ledger);
                Assert.That(governanceReport.Issues.Any(issue => issue.Code == "known-warning.missing"), Is.True);
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
            Assert.That(aliasTable.Entries.Count, Is.Zero);
            Assert.That(ledger.Entries.Count, Is.Zero);
        }

        [Test]
        public void AliasGovernanceLedger_MetadataDriftRequiresLedgerUpdate()
        {
            var aliasTable = ScriptableObject.CreateInstance<StageIdAliasTable>();
            var ledger = ScriptableObject.CreateInstance<StageAliasGovernanceLedger>();
            aliasTable.SetEntries(new[]
            {
                new StageIdAliasEntry
                {
                    DeprecatedStageId = "synthetic-stage-alias",
                    CurrentStageId = StageId.CreateOrThrow("combined-gameplay-showcase"),
                },
            });
            ledger.SetEntries(new[]
            {
                new StageAliasGovernanceEntry
                {
                    DeprecatedStageId = "synthetic-stage-alias",
                    CurrentStageId = StageId.CreateOrThrow("combined-gameplay-showcase"),
                    SourceKind = "test",
                    SourceAssetGuid = "synthetic-guid",
                    Owner = string.Empty,
                    Reason = "synthetic row",
                    IntroducedBy = "test",
                    RemovalGate = "test-only",
                },
            });

            try
            {
                var governanceReport = new StageAliasGovernanceValidator().Validate(aliasTable, ledger);
                Assert.That(governanceReport.Issues.Any(issue => issue.Code == "alias-governance.ledger.metadata-missing"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ledger);
                UnityEngine.Object.DestroyImmediate(aliasTable);
            }
        }

        [Test]
        public void AliasUsageScanner_ReportsNoDeprecatedAliasHitsAcrossTrackedProjectFiles()
        {
            var result = new StageAliasUsageScanner().Scan(StageAliasUsageScanner.P3HistoricalAliasIds);

            Assert.That(result.Hits, Is.Empty);
        }

        [Test]
        public void GameplayGuide_RecordsP3StageContentReportingWording_WithoutBroadGreenClaims()
        {
            var guide = ReadRepoFile(GameplayGuidePath);

            Assert.That(guide, Does.Contain("Stage content refactor reporting wording"));
            Assert.That(guide, Does.Contain("P3 sunset validated"));
            Assert.That(guide, Does.Contain("targeted architecture/CI validated"));
            Assert.That(guide, Does.Contain("Stage-Content-P3-Sunset-2026-04-22.md"));
            Assert.That(guide, Does.Contain("Stage-DefaultStageId-Editor-Direct-Play-Contract.md"));
            Assert.That(guide, Does.Contain("project-wide green"));
            Assert.That(guide, Does.Contain("Disallowed wording"));
        }

        [Test]
        public void CloseNote_RecordsBoundedEvidence_AndSeparatesRemovedItemsFromConsciousExceptions()
        {
            var closeNote = ReadRepoFile(CloseNotePath);

            Assert.That(closeNote, Does.Contain("# Stage Content P3 Sunset 2026-04-22"));
            Assert.That(closeNote, Does.Contain("`./run_tests.sh core`: green"));
            Assert.That(closeNote, Does.Contain("Stage catalog CI validation passed."));
            Assert.That(closeNote, Does.Contain("## Removed"));
            Assert.That(closeNote, Does.Contain("## Conscious Exceptions"));
            Assert.That(closeNote, Does.Contain("P3 sunset validated"));
            Assert.That(closeNote, Does.Contain("targeted architecture/CI validated"));
            Assert.That(closeNote, Does.Contain("project-wide green"));
            Assert.That(closeNote, Does.Contain("금지 claim"));
        }

        [Test]
        public void DirectPlayContract_DocumentsLauncherOnlyWorkflow_AndRemovedRuntimeFallback()
        {
            var contract = ReadRepoFile(DirectPlayContractPath);

            Assert.That(contract, Does.Contain("# Stage Editor Direct-Play Launcher Contract"));
            Assert.That(contract, Does.Contain("StageLoadRequest.CreateLaunchContextOnly"));
            Assert.That(contract, Does.Contain("StageEditorDirectPlayCatalog"));
            Assert.That(contract, Does.Contain("StageEditorDirectPlayLauncher"));
            Assert.That(contract, Does.Contain("Launch Current Scene"));
            Assert.That(contract, Does.Contain("defaultStageId runtime fallback는 제거됐다"));
            Assert.That(contract, Does.Not.Contain("CreateEditorDirectPlayFallback"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
