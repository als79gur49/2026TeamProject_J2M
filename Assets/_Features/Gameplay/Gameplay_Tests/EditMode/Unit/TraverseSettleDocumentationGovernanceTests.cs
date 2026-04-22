using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TraverseSettleDocumentationGovernanceTests
    {
        [Test]
        [Category("Extended")]
        public void CanonicalSpec_DescribesTraverseSettleSpatialState_And_ReservedFutureStates()
        {
            var spec = ReadRepoFile("Docs/Architecture/Tick-Simulation-Canonical-Spec.md");

            Assert.That(spec, Does.Contain("Traverse / Settle"));
            Assert.That(spec, Does.Contain("SpatialState"));
            Assert.That(spec, Does.Contain("TraverseContext"));
            Assert.That(spec, Does.Contain("SettlementContext"));
            Assert.That(spec, Does.Contain("orchestration-only"));
            Assert.That(spec, Does.Contain("reserved future state"));
            Assert.That(spec, Does.Contain("TS-01"));
            Assert.That(spec, Does.Contain("TS-09"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayRulesAppendix_DescribesTraverseSettleDistinction_And_AirborneLanding()
        {
            var appendix = ReadRepoFile("Docs/Architecture/Gameplay-Rules-Appendix.md");

            Assert.That(appendix, Does.Contain("Traverse vs Settle"));
            Assert.That(appendix, Does.Contain("Airborne"));
            Assert.That(appendix, Does.Contain("settlement"));
        }

        [Test]
        [Category("Extended")]
        public void PushFlipImpactDocs_RecordCurrentContractDecision_And_NarrowPresentationBoundary()
        {
            var spec = ReadRepoFile("Docs/Architecture/Tick-Simulation-Canonical-Spec.md");
            var appendix = ReadRepoFile("Docs/Architecture/Gameplay-Rules-Appendix.md");

            Assert.That(spec, Does.Contain("current runtime follow-through formalization"));
            Assert.That(spec, Does.Contain("not a generalized impact framework"));
            Assert.That(spec, Does.Contain("Push change is formalization, not a new framework."));
            Assert.That(spec, Does.Contain("Flip change is a narrow impact-result-dependent uplift."));
            Assert.That(spec, Does.Contain("`Flip lethal but landing denied = Stay` is a current contract decision."));
            Assert.That(spec, Does.Contain("Transient collision/break is presentation-only and must not be used as gameplay truth."));

            Assert.That(appendix, Does.Contain("current runtime lethal follow-through formalization"));
            Assert.That(appendix, Does.Contain("impact-result-dependent action uplift"));
            Assert.That(appendix, Does.Contain("current contract decision"));
            Assert.That(appendix, Does.Contain("presentation-only track"));
            Assert.That(appendix, Does.Contain("not a generalized impact framework"));
            Assert.That(appendix, Does.Contain("Push change is formalization, not a new framework."));
            Assert.That(appendix, Does.Contain("Transient collision/break is presentation-only and must not be used as gameplay truth."));
        }

        [Test]
        [Category("Extended")]
        public void PhasedDocs_DescribeStageDefaults_And_DeferredLockTaxonomy_AsClosedContracts()
        {
            var spec = ReadRepoFile("Docs/Architecture/Tick-Simulation-Canonical-Spec.md");
            var appendix = ReadRepoFile("Docs/Architecture/Gameplay-Rules-Appendix.md");

            Assert.That(spec, Does.Contain("current enemy lock retention is `EnemyActionStateTargeting` current lock path 전용 narrow hook"));
            Assert.That(spec, Does.Contain("current implementation default"));
            Assert.That(spec, Does.Contain("internal validation owner, not public scripted framework"));
            Assert.That(spec, Does.Contain("baseline validator consumer"));
            Assert.That(spec, Does.Contain("locked-target dependency is current validator-local dependency, not generic phase dependency"));
            Assert.That(spec, Does.Contain("same-face / straight-line / target-behind +1 / single-terminal chooser are local geometry rules, not reusable phase template"));
            Assert.That(appendix, Does.Contain("validator, not a movement framework"));
            Assert.That(appendix, Does.Contain("baseline validator consumer"));
            Assert.That(appendix, Does.Contain("not reusable phase template"));
            Assert.That(appendix, Does.Contain("Deferred lock taxonomy"));
            Assert.That(appendix, Does.Contain("generic lock framework의 seed"));
            Assert.That(appendix, Does.Contain("primary truth는 semantic contract"));
            Assert.That(appendix, Does.Contain("semantic contract is the primary source-of-truth; inline token count is only a secondary sentinel."));
            Assert.That(appendix, Does.Contain("SystemPreMovementValidation"));
            Assert.That(appendix, Does.Contain("FreshSelectionSuppressedWithCurrentEnemyLockRetention"));
        }

        [Test]
        [Category("Extended")]
        public void PhasedDocs_RecordBaselineValidatorHandoff_And_TraceContract()
        {
            var appendix = ReadRepoFile("Docs/Architecture/Gameplay-Rules-Appendix.md");
            var baseline = ReadRepoFile("Docs/Testing/Phased-v2-Horizontal-Expansion-Validation-Baseline-2026-04-19.md");

            Assert.That(appendix, Does.Contain("Role=BaselineValidatorOnly"));
            Assert.That(appendix, Does.Contain("LockDependency=CurrentEnemyLockPathOnly"));
            Assert.That(appendix, Does.Contain("ChooserLocality=SameFace|StraightLine|Behind+1|SingleTerminal"));
            Assert.That(appendix, Does.Contain("Reservation=TerminalCellOnlyPreSettle"));
            Assert.That(appendix, Does.Contain("ForbiddenGeneralization=NoRetarget|NoAlternate|NoFallback|NoSameTickCombat"));
            Assert.That(appendix, Does.Contain("NotEvidenceFor=GeneralizedPhaseMovement|Pathfinding|NonClaimOccupancy|TerminalPhaseSettle"));
            Assert.That(appendix, Does.Contain("stable trace contract는 `PhaseEnter/Exit`, `Owner`, `Timing`, `ReservationRead`, `Settle`, `ExistingEnemyLock`, `EnemyPhaseRelocation`의 `Label/Target/Direction/Destination/Result`까지만 본다."));
            Assert.That(appendix, Does.Contain("implementation detail"));

            Assert.That(baseline, Does.Contain("Role=BaselineValidatorOnly"));
            Assert.That(baseline, Does.Contain("NotEvidenceFor=GeneralizedPhaseMovement|Pathfinding|NonClaimOccupancy|TerminalPhaseSettle"));
        }

        [Test]
        [Category("Extended")]
        public void TerrainOccupancyGateAdr_DefinesVocabularyBoundary_And_MinimalHarnessGate()
        {
            var adr = ReadRepoFile("Docs/Architecture/ADR/ADR-004-Terrain-Occupancy-Implementation-Gate.md");
            var readme = ReadRepoFile("Docs/Architecture/README.md");

            Assert.That(adr, Does.Contain("E0. discovery"));
            Assert.That(adr, Does.Contain("E1. decision closed"));
            Assert.That(adr, Does.Contain("E2. implementation gate ready"));
            Assert.That(adr, Does.Contain("E3. slice implementation"));
            Assert.That(adr, Does.Contain("occupancy truth"));
            Assert.That(adr, Does.Contain("terrain truth"));
            Assert.That(adr, Does.Contain("blocker vocabulary"));
            Assert.That(adr, Does.Contain("`Traverse`"));
            Assert.That(adr, Does.Contain("`Settle`"));
            Assert.That(adr, Does.Contain("`Modifier`"));
            Assert.That(adr, Does.Contain("`Reservation`"));
            Assert.That(adr, Does.Contain("Tools/check_gameplay_semantic_query_migration.py"));
            Assert.That(adr, Does.Contain("legality context governance tests"));
            Assert.That(adr, Does.Contain("compatibility helper와 allowlist는 현재 상태로 동결"));
            Assert.That(adr, Does.Contain("decision closed를 근거로 즉시 runtime-wide semantics refactor"));
            Assert.That(readme, Does.Contain("ADR-004-Terrain-Occupancy-Implementation-Gate.md"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
