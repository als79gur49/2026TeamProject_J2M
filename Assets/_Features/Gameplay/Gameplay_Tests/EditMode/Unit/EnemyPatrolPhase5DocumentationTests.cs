using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyPatrolPhase5DocumentationTests
    {
        [Test]
        [Category("Extended")]
        public void EnemyPatrolArchitectureReadme_ListsPhase5WindupRollout_AsSupportingTruthSource()
        {
            var readme = ReadRepoFile("Docs/Architecture/README.md");

            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md"));
            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md"));
            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md"));
            Assert.That(readme, Does.Contain("WindupMelee"));
            Assert.That(readme, Does.Contain("pilot preset scorecard"));
            Assert.That(readme, Does.Contain("runtime parity closure"));
            Assert.That(readme, Does.Contain("baseline-control-first close retry order"));
            Assert.That(readme, Does.Contain("green-vs-red evidence split"));
            Assert.That(readme, Does.Contain("evidence-summary.md"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5Doc_DefinesDriftMatrix_PilotScorecard_SameCellOrdering_Sampling_AndNextGate()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Phase 5: `WindupMelee` Bounded Rollout"));
            Assert.That(doc, Does.Contain("## 4. drift matrix"));
            Assert.That(doc, Does.Contain("`exact-contract`"));
            Assert.That(doc, Does.Contain("`bounded-exposure`"));
            Assert.That(doc, Does.Contain("## 5. pilot patrol preset scorecard"));
            Assert.That(doc, Does.Contain("`1 / 6 / 1 / 1`"));
            Assert.That(doc, Does.Contain("## 6. same-cell passive-contact / candidate ordering matrix"));
            Assert.That(doc, Does.Contain("`Combat(localSequence 0) -> PassiveContact(localSequence 1)`"));
            Assert.That(doc, Does.Contain("## 7. evaluation sampling matrix"));
            Assert.That(doc, Does.Contain("`DirectLaneAligned`"));
            Assert.That(doc, Does.Contain("`PrimedSameCellCombatPassive`"));
            Assert.That(doc, Does.Contain("## 11. post-phase decision matrix"));
            Assert.That(doc, Does.Contain("Gameplay-EnemyPatrol-Phase6-JumpChaser-Readiness.md"));
            Assert.That(doc, Does.Contain("Gameplay-EnemyPatrol-Phase6-Charge-Readiness.md"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5Doc_RepeatsForwardFallback_NoTouch_Rollback_SuccessFailure()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md");

            Assert.That(doc, Does.Contain("`Forward fallback untouched`"));
            Assert.That(doc, Does.Contain("baseline asset destructive overwrite 금지"));
            Assert.That(doc, Does.Contain("## 13. out-of-scope / no-touch"));
            Assert.That(doc, Does.Contain("## 14. rollback / success / failure"));
            Assert.That(doc, Does.Contain("shipping preset scorecard 실패"));
            Assert.That(doc, Does.Contain("phase 6 자동 착수"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5FixupDoc_DefinesBoundedFix_Triage_Checklist_Evidence_AndCloseGate()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Phase 5: `WindupMelee` Runtime Parity Fixup"));
            Assert.That(doc, Does.Contain("runtime parity red"));
            Assert.That(doc, Does.Contain("phase 5는 아직 close가 아니다"));
            Assert.That(doc, Does.Contain("## 4. 실패 4개 묶음 triage"));
            Assert.That(doc, Does.Contain("### 4.1 triage 표"));
            Assert.That(doc, Does.Contain("### 4.2 원인 후보 매핑 표"));
            Assert.That(doc, Does.Contain("## 6. runtime parity closure checklist"));
            Assert.That(doc, Does.Contain("## 7. same-revision evidence bundle"));
            Assert.That(doc, Does.Contain("## 8. fallback / rollback"));
            Assert.That(doc, Does.Contain("## 9. phase 5 close gate"));
            Assert.That(doc, Does.Contain("Current Green (non-close evidence)"));
            Assert.That(doc, Does.Contain("Current Red (close blockers)"));
            Assert.That(doc, Does.Contain("evidence-summary.md"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5FixupDoc_StaysBounded_NoTouch_AndDoesNotClaimClose()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md");

            Assert.That(doc, Does.Contain("bounded fix"));
            Assert.That(doc, Does.Contain("`Forward` fallback/oracle"));
            Assert.That(doc, Does.Contain("`NonAttacking` pilot"));
            Assert.That(doc, Does.Contain("다른 archetype rollout 확장"));
            Assert.That(doc, Does.Contain("phase 5는 아직 close가 아니다"));
            Assert.That(doc, Does.Not.Contain("phase 5 closed"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5RedClosureDoc_DefinesHardGate_SeparatedLanes_Checklist_Evidence_AndRollback()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Phase 5: `WindupMelee RandomWalk pilot` Red Closure Hardening Plan"));
            Assert.That(doc, Does.Contain("phase 5는 아직 close가 아니다"));
            Assert.That(doc, Does.Contain("baseline control green이 아니면 이하 증거는 close blocker 판정에 사용하지 않는다"));
            Assert.That(doc, Does.Contain("combat-start gate"));
            Assert.That(doc, Does.Contain("execute gate"));
            Assert.That(doc, Does.Contain("commit trace gate"));
            Assert.That(doc, Does.Contain("recover gate"));
            Assert.That(doc, Does.Contain("active-windup entry"));
            Assert.That(doc, Does.Contain("cancel owner"));
            Assert.That(doc, Does.Contain("trace wording"));
            Assert.That(doc, Does.Contain("home-return"));
            Assert.That(doc, Does.Contain("trace-only duplication"));
            Assert.That(doc, Does.Contain("semantic correlation mismatch"));
            Assert.That(doc, Does.Contain("Current Green (non-close evidence)"));
            Assert.That(doc, Does.Contain("Current Red (close blockers)"));
            Assert.That(doc, Does.Contain("baseline-control-targeted.xml"));
            Assert.That(doc, Does.Contain("evidence-summary.md"));
            Assert.That(doc, Does.Contain("TestResults/phase5-red-closure/"));
            Assert.That(doc, Does.Contain("rollback"));
            Assert.That(doc, Does.Contain("no-touch"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5RedClosureDoc_StaysBounded_NoTouch_AndDoesNotClaimClose()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md");

            Assert.That(doc, Does.Contain("bounded fix"));
            Assert.That(doc, Does.Contain("`Forward` fallback/oracle"));
            Assert.That(doc, Does.Contain("`NonAttacking` pilot"));
            Assert.That(doc, Does.Contain("broad backlog recovery 혼합 금지"));
            Assert.That(doc, Does.Not.Contain("phase 5 closed"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
