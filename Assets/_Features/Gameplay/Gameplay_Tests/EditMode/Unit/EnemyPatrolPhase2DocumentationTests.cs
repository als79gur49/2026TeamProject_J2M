using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyPatrolPhase2DocumentationTests
    {
        [Test]
        [Category("Extended")]
        public void ArchitectureReadme_ListsEnemyPatrolPhase2ResponsibilityMap_AsSupportingTruthSource()
        {
            var readme = ReadRepoFile("Docs/Architecture/README.md");

            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md"));
            Assert.That(readme, Does.Contain("EnemyLogic"));
            Assert.That(readme, Does.Contain("RandomWalk"));
            Assert.That(readme, Does.Contain("Forward"));
            Assert.That(readme, Does.Contain("WallFollow"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase2Doc_DefinesResponsibilityMap_Boundaries_And_Phase3Gate()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Phase 2: `EnemyLogic` Special-Case Responsibility Map"));
            Assert.That(doc, Does.Contain("`EnemyPatrolRuntimeState`는 계속 canonical patrol state 저장소로 유지한다."));
            Assert.That(doc, Does.Contain("`EnemyLogic` patrol responsibility 표"));
            Assert.That(doc, Does.Contain("`_patrolStrategyKind` 직접 해석"));
            Assert.That(doc, Does.Contain("`RandomWalk`만 special-case인 이유"));
            Assert.That(doc, Does.Contain("`PatrolDecisionProposal`"));
            Assert.That(doc, Does.Contain("`RawMovementIntent` 생성 직전에서 끝나며, write context를 받지 않는다."));
            Assert.That(doc, Does.Contain("accepted move 후 `CommitMove` write"));
            Assert.That(doc, Does.Contain("planner / common decision layer는 \"방향 / 회전 / init hint를 제안\"하는 곳까지가 범위다."));
            Assert.That(doc, Does.Contain("`Forward` 공통화 readiness 평가"));
            Assert.That(doc, Does.Contain("`WallFollow`는 legacy가 아니라 active bounded strategy이며, 단계 2의 공통화 후보가 아니다."));
            Assert.That(doc, Does.Contain("## 8. 단계 3 진입 gate"));
            Assert.That(doc, Does.Contain("## 10. no-touch list"));
            Assert.That(doc, Does.Contain("## 11. rollback / defer criteria"));
            Assert.That(doc, Does.Contain("docs-only close"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
