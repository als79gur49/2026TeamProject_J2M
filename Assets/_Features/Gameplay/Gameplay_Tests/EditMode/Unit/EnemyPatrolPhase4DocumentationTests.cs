using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyPatrolPhase4DocumentationTests
    {
        [Test]
        [Category("Extended")]
        public void EnemyPatrolArchitectureReadme_ListsPhase4WallFollowDecision_AsSupportingTruthSource()
        {
            var readme = ReadRepoFile("Docs/Architecture/README.md");

            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md"));
            Assert.That(readme, Does.Contain("maintain-vs-redesign verdict"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase4Doc_DefinesTruthTable_OwnerSurface_Comparison_Verdict_AndNoTouchList()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Phase 4: `WallFollow` Decision (Bounded)"));
            Assert.That(doc, Does.Contain("## 4. `WallFollow` current truth table"));
            Assert.That(doc, Does.Contain("| rule | observable truth | current owner | existing evidence | proposal-frame fit | phase4 note |"));
            Assert.That(doc, Does.Contain("## 5. `WallFollow` owner surface"));
            Assert.That(doc, Does.Contain("## 6. `WallFollow` vs `Forward` / `RandomWalk`"));
            Assert.That(doc, Does.Contain("## 7. `유지` vs `재설계` 판정 기준"));
            Assert.That(doc, Does.Contain("`WallFollow` verdict는 `유지`다."));
            Assert.That(doc, Does.Contain("`EnemyPatrolRuntimeState`를 `WallFollow`에 즉시 적용하지 않는다."));
            Assert.That(doc, Does.Contain("## 10. out-of-scope / no-touch"));
            Assert.That(doc, Does.Contain("## 12. rollback / defer criteria"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase4Doc_KeepsWallFollowOutOfProposalSupportMatrix_AndDefinesConditionalRedesignSubProblems()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md");

            Assert.That(doc, Does.Contain("simple proposal candidate 아님"));
            Assert.That(doc, Does.Contain("`WallFollow`는 unsupported bounded strategy로 유지한다."));
            Assert.That(doc, Does.Contain("`RandomWalk` pilot과 `Forward` commonization을 흔들지 않는다."));
            Assert.That(doc, Does.Contain("## 11. 재설계 task를 여는 경우의 bounded sub-problem"));
            Assert.That(doc, Does.Contain("`anchor semantics extraction`"));
            Assert.That(doc, Does.Contain("`facing-only / rotate-only boundary`"));
            Assert.That(doc, Does.Contain("`same-cell passive-contact hold boundary`"));
            Assert.That(doc, Does.Contain("`turn-and-move same-tick contract`"));
            Assert.That(doc, Does.Contain("`adapter fit spike`"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
