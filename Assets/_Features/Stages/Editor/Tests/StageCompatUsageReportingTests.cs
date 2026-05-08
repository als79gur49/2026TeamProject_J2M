using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCompatUsageReportingTests
    {
        private const string CatalogAssetPath = StageContentPaths.StageCatalogAssetPath;
        private const string KnownWarningLedgerAssetPath =
            "Assets/_Features/Stages/Editor/Validation/StageCatalogKnownWarningLedger.asset";
        private const string AliasGovernanceLedgerAssetPath =
            StageAliasGovernanceUpdater.DefaultAliasGovernanceLedgerAssetPath;
        private const string GameplayGuidePath = "Docs/Testing/Gameplay-Test-Automation-Guide.md";
        private const string CloseNotePath = "Docs/Testing/Stage-Content-P3-Sunset-2026-04-22.md";
        private const string DirectPlayContractPath =
            "Docs/Testing/Stage-DefaultStageId-Editor-Direct-Play-Contract.md";
        private const string BoundedLaneGuidePath =
            "Docs/Testing/Post-Stage-Content-Bounded-Lane-Operations.md";
        private const string CloseTemplatePath =
            "Docs/Testing/Bounded-Lane-Close-Template.md";
        private const string DirectPlayChecklistPath =
            "Docs/Testing/Stage-Editor-Direct-Play-Adoption-Checklist.md";
        private const string DirectPlayCycleTemplatePath =
            "Docs/Testing/Stage-Editor-Direct-Play-Smoke-Cycle-Template.md";
        private const string LaneABaselineRefreezePath =
            "Docs/Testing/Lane-A-Full-Baseline-Refreeze-2026-04-22.md";
        private const string LaneARowLedgerPath =
            "Docs/Testing/Lane-A-Live-Row-Ledger-2026-04-22.md";
        private const string LaneAHandoffLedgerPath =
            "Docs/Testing/Lane-A-Handoff-Ledger-2026-04-22.md";
        private const string LaneFAuditPath =
            "Docs/Testing/Lane-F-Claim-Vocabulary-Audit-2026-04-22.md";
        private const string LaneFHygieneClosePath =
            "Docs/Testing/Lane-F-Governance-Hygiene-Close-2026-04-22.md";
        private const string SupportTreeDecisionPath =
            "Docs/Architecture/ADR/ADR-002-Stage-Support-Tree-Deferred-Relocation.md";
        private const string ArchitectureReadmePath = "Docs/Architecture/README.md";

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
            var aliasTable = AssetDatabase.LoadAssetAtPath<StageIdAliasTable>(StageContentPaths.StageIdAliasTableAssetPath);
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
        public void GameplayGuide_RecordsBoundedLaneClaimVocabulary_AndArtifactDateRules()
        {
            var guide = ReadRepoFile(GameplayGuidePath);

            Assert.That(guide, Does.Contain("Post-stage-content bounded lane reporting"));
            Assert.That(guide, Does.Contain("ui lane validated"));
            Assert.That(guide, Does.Contain("full-lane baseline recovered"));
            Assert.That(guide, Does.Contain("broad project-wide green"));
            Assert.That(guide, Does.Contain("same revision"));
            Assert.That(guide, Does.Contain("same execution window"));
            Assert.That(guide, Does.Contain("Bounded-Lane-Close-Template.md"));
            Assert.That(guide, Does.Contain("Post-Stage-Content-Bounded-Lane-Operations.md"));
            Assert.That(guide, Does.Contain("Stage-Editor-Direct-Play-Smoke-Cycle-Template.md"));
            Assert.That(guide, Does.Contain("open functional backlog / handoff"));
            Assert.That(guide, Does.Contain("`2026-04-22 core/ui`와 `2026-04-21 full`"));
            Assert.That(guide, Does.Contain("Counter Summary"));
            Assert.That(guide, Does.Contain("current same-revision artifact"));
            Assert.That(guide, Does.Contain("governance hygiene green alone does not close Lane B, Lane A, or any functional lane"));
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
            Assert.That(contract, Does.Contain("`defaultStageId` runtime fallback는 제거됐다."));
            Assert.That(contract, Does.Not.Contain("CreateEditorDirectPlayFallback"));
        }

        [Test]
        public void DirectPlayContract_RecordsOperationalMetrics_AndSoftHardAdoption()
        {
            var contract = ReadRepoFile(DirectPlayContractPath);
            var checklist = ReadRepoFile(DirectPlayChecklistPath);

            Assert.That(contract, Does.Contain("enabled stage-backed scene direct-play catalog coverage `100%`"));
            Assert.That(contract, Does.Contain("workflow compliance"));
            Assert.That(contract, Does.Contain("warning consistency"));
            Assert.That(contract, Does.Contain("onboarding parity"));
            Assert.That(contract, Does.Contain("## Soft Adoption"));
            Assert.That(contract, Does.Contain("## Hard Enforcement"));
            Assert.That(contract, Does.Contain("## Evidence Format"));
            Assert.That(contract, Does.Contain("unsupported reference case"));
            Assert.That(contract, Does.Contain("runtime fallback 추가가 아니라 warning, checklist, triage policy, close wording으로 고정한다."));
            Assert.That(contract, Does.Contain("checkpoint window"));
            Assert.That(contract, Does.Contain("Counter Summary"));
            Assert.That(contract, Does.Contain("same executor + governance reviewer co-sign"));
            Assert.That(checklist, Does.Contain("Tools/Stages/Direct Play/Launch Current Scene"));
            Assert.That(checklist, Does.Contain("Replay Last Stage-Backed Scene"));
            Assert.That(checklist, Does.Contain("Combined Gameplay Showcase"));
            Assert.That(checklist, Does.Contain("Tutorial Scene"));
            Assert.That(checklist, Does.Contain("UI Audio Scene"));
            Assert.That(checklist, Does.Contain("Cycle 1"));
            Assert.That(checklist, Does.Contain("Cycle 2"));
            Assert.That(checklist, Does.Contain("Counter Summary"));
            Assert.That(checklist, Does.Contain("7 calendar days"));
            Assert.That(checklist, Does.Contain("same executor + governance reviewer co-sign"));
        }

        [Test]
        public void PostStageContentOpsGuide_SplitsLaneA_AndDefinesHandoffSchema()
        {
            var opsGuide = ReadRepoFile(BoundedLaneGuidePath);

            Assert.That(opsGuide, Does.Contain("A1. new red candidate isolation"));
            Assert.That(opsGuide, Does.Contain("A2. host/view/bootstrap adjacency"));
            Assert.That(opsGuide, Does.Contain("A3. direct unrelated backlog"));
            Assert.That(opsGuide, Does.Contain("A4. baseline red continuation triage"));
            Assert.That(opsGuide, Does.Contain("`A1 -> A2 -> A3 -> A4`"));
            Assert.That(opsGuide, Does.Contain("full-lane baseline recovered"));
            Assert.That(opsGuide, Does.Contain("source lane"));
            Assert.That(opsGuide, Does.Contain("target lane"));
            Assert.That(opsGuide, Does.Contain("reason"));
            Assert.That(opsGuide, Does.Contain("blocking claim"));
            Assert.That(opsGuide, Does.Contain("required evidence"));
            Assert.That(opsGuide, Does.Contain("StageCatalogResolver"));
            Assert.That(opsGuide, Does.Contain("Lane F"));
            Assert.That(opsGuide, Does.Contain("classification confidence"));
            Assert.That(opsGuide, Does.Contain("first wrong oracle"));
            Assert.That(opsGuide, Does.Contain("pending-acceptance"));
            Assert.That(opsGuide, Does.Contain("accepted"));
            Assert.That(opsGuide, Does.Contain("rejected"));
            Assert.That(opsGuide, Does.Contain("newness / failure-shape change"));
            Assert.That(opsGuide, Does.Contain("source artifact"));
            Assert.That(opsGuide, Does.Contain("last reviewed at"));
        }

        [Test]
        public void CloseTemplate_RequiresMinimumCommonSections_AndOpenHandoff()
        {
            var template = ReadRepoFile(CloseTemplatePath);

            Assert.That(template, Does.Contain("# Bounded Lane Close Template"));
            Assert.That(template, Does.Contain("Scope"));
            Assert.That(template, Does.Contain("Executed Commands"));
            Assert.That(template, Does.Contain("Artifact List With Exact Dates"));
            Assert.That(template, Does.Contain("Allowed Claims"));
            Assert.That(template, Does.Contain("Explicit Non-Claims"));
            Assert.That(template, Does.Contain("Open Functional Backlog / Handoff"));
            Assert.That(template, Does.Contain("Open Risks"));
            Assert.That(template, Does.Contain("Reviewed Truth Sources"));
            Assert.That(template, Does.Contain("Drift Triage Summary"));
            Assert.That(template, Does.Contain("Claim Vocabulary Audit"));
            Assert.That(template, Does.Contain("Template Alignment Result"));
            Assert.That(template, Does.Contain("same revision, same execution window, same lane artifact"));
            Assert.That(template, Does.Contain("governance hygiene green alone does not close Lane B, Lane A, or any functional lane"));
        }

        [Test]
        public void DirectPlayCycleTemplate_RecordsRequiredSceneFields_AndCounterSummary()
        {
            var template = ReadRepoFile(DirectPlayCycleTemplatePath);

            Assert.That(template, Does.Contain("# Stage Editor Direct-Play Smoke Cycle Template"));
            Assert.That(template, Does.Contain("revision"));
            Assert.That(template, Does.Contain("cycle id"));
            Assert.That(template, Does.Contain("editor session identifier"));
            Assert.That(template, Does.Contain("catalog coverage check result"));
            Assert.That(template, Does.Contain("plain Play workflow classification"));
            Assert.That(template, Does.Contain("scene name"));
            Assert.That(template, Does.Contain("exact menu path"));
            Assert.That(template, Does.Contain("execution date/time"));
            Assert.That(template, Does.Contain("launch result"));
            Assert.That(template, Does.Contain("observed warning/fail-fast"));
            Assert.That(template, Does.Contain("plain Play attempted"));
            Assert.That(template, Does.Contain("Counter Summary"));
            Assert.That(template, Does.Contain("launcher bypass 정상 workflow 기록"));
            Assert.That(template, Does.Contain("fallback 요구 issue"));
        }

        [Test]
        public void FollowupArtifacts_RecordLaneABaselineRefreeze_Ledger_Handoff_Audit_AndLaneFClose()
        {
            var baseline = ReadRepoFile(LaneABaselineRefreezePath);
            var rowLedger = ReadRepoFile(LaneARowLedgerPath);
            var handoffLedger = ReadRepoFile(LaneAHandoffLedgerPath);
            var audit = ReadRepoFile(LaneFAuditPath);
            var laneFClose = ReadRepoFile(LaneFHygieneClosePath);

            Assert.That(baseline, Does.Contain("# Lane A Full Baseline Refreeze 2026-04-22"));
            Assert.That(baseline, Does.Contain("same-revision full baseline refreeze"));
            Assert.That(baseline, Does.Contain("./run_tests.sh full"));
            Assert.That(baseline, Does.Contain("Explicit Non-Claims"));

            Assert.That(rowLedger, Does.Contain("# Lane A Live Row Ledger 2026-04-22"));
            Assert.That(rowLedger, Does.Contain("classification confidence"));
            Assert.That(rowLedger, Does.Contain("first wrong oracle"));
            Assert.That(rowLedger, Does.Contain("First Active Queue"));
            Assert.That(rowLedger, Does.Contain("row id/test name"));

            Assert.That(handoffLedger, Does.Contain("# Lane A Handoff Ledger 2026-04-22"));
            Assert.That(handoffLedger, Does.Contain("pending-acceptance"));
            Assert.That(handoffLedger, Does.Contain("accepted by"));
            Assert.That(handoffLedger, Does.Contain("historical comparison artifact"));

            Assert.That(audit, Does.Contain("# Lane F Claim Vocabulary Audit 2026-04-22"));
            Assert.That(audit, Does.Contain("searched paths"));
            Assert.That(audit, Does.Contain("disallowed phrases"));
            Assert.That(audit, Does.Contain("allowed phrase spot-check"));
            Assert.That(audit, Does.Contain("revision"));
            Assert.That(audit, Does.Contain("date/time"));

            Assert.That(laneFClose, Does.Contain("# Lane F Governance Hygiene Close 2026-04-22"));
            Assert.That(laneFClose, Does.Contain("Reviewed Truth Sources"));
            Assert.That(laneFClose, Does.Contain("Drift Triage Summary"));
            Assert.That(laneFClose, Does.Contain("Claim Vocabulary Audit"));
            Assert.That(laneFClose, Does.Contain("Template Alignment Result"));
            Assert.That(laneFClose, Does.Contain("governance hygiene green alone does not close Lane B, Lane A, or any functional lane"));
        }

        [Test]
        public void SupportTreeDecisionRecord_StaysDeferred_ButDefinesReviewTriggers_AndPilotGate()
        {
            var record = ReadRepoFile(SupportTreeDecisionPath);
            var readme = ReadRepoFile(ArchitectureReadmePath);

            Assert.That(record, Does.Contain("`consumed asset complete`"));
            Assert.That(record, Does.Contain("`support tree full relocation`"));
            Assert.That(record, Does.Contain("현재 decision은 `deferred`다"));
            Assert.That(record, Does.Contain("## Scheduled Trigger"));
            Assert.That(record, Does.Contain("## Immediate Trigger"));
            Assert.That(record, Does.Contain("stage presentation / authoring owner"));
            Assert.That(record, Does.Contain("GUID/reference dry-run audit"));
            Assert.That(record, Does.Contain("pilot-eligible"));
            Assert.That(readme, Does.Contain("ADR-002-Stage-Support-Tree-Deferred-Relocation.md"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
