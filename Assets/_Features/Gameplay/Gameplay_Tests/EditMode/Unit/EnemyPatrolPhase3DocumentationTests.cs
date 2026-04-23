using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyPatrolPhase3DocumentationTests
    {
        private static readonly string[] ForbiddenEnemyLogicTokens =
        {
            "PatrolStrategyKind.Forward",
            "PatrolStrategyKind.RandomWalk",
            "_patrolStrategyKind == PatrolStrategyKind",
            "_patrolStrategyKind != PatrolStrategyKind",
        };

        [Test]
        [Category("Extended")]
        public void EnemyPatrolArchitectureReadme_ListsPhase2_ProposalContract_Phase3_RolloutGate()
        {
            var readme = ReadRepoFile("Docs/Architecture/README.md");
            var phase2Index = readme.IndexOf("Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md", System.StringComparison.Ordinal);
            var contractIndex = readme.IndexOf("Gameplay-EnemyPatrol-Decision-Proposal-Contract.md", System.StringComparison.Ordinal);
            var phase3Index = readme.IndexOf("Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md", System.StringComparison.Ordinal);
            var gateIndex = readme.IndexOf("Gameplay-EnemyPatrol-Forward-Rollout-Gate.md", System.StringComparison.Ordinal);

            Assert.That(readme, Does.Contain("## Enemy Patrol bounded rollout"));
            Assert.That(phase2Index, Is.GreaterThanOrEqualTo(0));
            Assert.That(contractIndex, Is.GreaterThan(phase2Index));
            Assert.That(phase3Index, Is.GreaterThan(contractIndex));
            Assert.That(gateIndex, Is.GreaterThan(phase3Index));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolDecisionProposalContract_DefinesSupportedKinds_UnsupportedKinds_AndOwnerBoundary()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Decision-Proposal-Contract.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Decision Proposal Contract"));
            Assert.That(doc, Does.Contain("`PatrolDecisionProposal`"));
            Assert.That(doc, Does.Contain("`EnemyPatrolRuntimeState`"));
            Assert.That(doc, Does.Contain("supported simple kinds는 `Forward`, `RandomWalk`"));
            Assert.That(doc, Does.Contain("`WallFollow`"));
            Assert.That(doc, Does.Contain("`Stationary`"));
            Assert.That(doc, Does.Contain("`Forward`는 stateless다."));
            Assert.That(doc, Does.Contain("`ShouldInitializeState`는 항상 `false`"));
            Assert.That(doc, Does.Contain("planner/common decision layer는 proposal까지만 담당한다."));
            Assert.That(doc, Does.Contain("`MovementCommitter`"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase3Doc_DefinesSingleProposalSeam_SpecialCaseReduction_AndRollbackChecklist()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Phase 3: `Forward` Commonization (Bounded)"));
            Assert.That(doc, Does.Contain("single proposal seam consumer"));
            Assert.That(doc, Does.Contain("Special-Case Reduction Checklist"));
            Assert.That(doc, Does.Contain("`EnemyLogic.cs` 안에 `PatrolStrategyKind.Forward`, `PatrolStrategyKind.RandomWalk` direct branch가 남지 않는다."));
            Assert.That(doc, Does.Contain("`WallFollow` out-of-scope"));
            Assert.That(doc, Does.Contain("`EnemyPatrolRuntimeState` 외 새 canonical patrol state 저장소"));
            Assert.That(doc, Does.Contain("Rollback Checklist"));
            Assert.That(doc, Does.Contain("docs-only defer"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolForwardRolloutGate_DefinesQuantitativeUnchangedCriteria_AndPostPhaseDecisionMatrix()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Forward-Rollout-Gate.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Forward Rollout Gate"));
            Assert.That(doc, Does.Contain("Quantitative Unchanged Matrix"));
            Assert.That(doc, Does.Contain("`Forward baseline unchanged`"));
            Assert.That(doc, Does.Contain("`RandomWalk unchanged`"));
            Assert.That(doc, Does.Contain("`EnemyLogic_ForwardProposalPath_MatchesLegacyForwardStrategy_OnCanonicalFixtures`"));
            Assert.That(doc, Does.Contain("`Replay_RandomWalkPilotProfile_ProducesStablePerTickHashTraceAndPatrolDump`"));
            Assert.That(doc, Does.Contain("`EnemyAi_NonAttackingRandomWalkPilot_First10Ticks_MatchPinnedSequence`"));
            Assert.That(doc, Does.Contain("Post-Phase Decision Matrix"));
            Assert.That(doc, Does.Contain("`ForwardPatrolStrategy`"));
            Assert.That(doc, Does.Contain("default archetype rollout | 자동 오픈 금지"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolSourceGovernance_EnemyLogic_DoesNotDirectlyBranchOnForwardOrRandomWalkOutsideProposalSeam()
        {
            var logicSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs");
            var plannerSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyPatrolDecisionPlanner.cs");

            Assert.That(logicSource, Does.Contain("TryBuildPatrolDecisionProposal("));
            Assert.That(plannerSource, Does.Contain("PatrolStrategyKind.Forward"));
            Assert.That(plannerSource, Does.Contain("PatrolStrategyKind.RandomWalk"));

            for (var i = 0; i < ForbiddenEnemyLogicTokens.Length; i++)
            {
                Assert.That(logicSource, Does.Not.Contain(ForbiddenEnemyLogicTokens[i]), $"Forbidden EnemyLogic token found: {ForbiddenEnemyLogicTokens[i]}");
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
