using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class ActionPlanCorrelationDocumentationTests
    {
        [Test]
        [Category("Extended")]
        public void CanonicalSpec_DefinesPlanTokensAsCanonicalSurface_And_GAsCompatibilityToken()
        {
            var spec = ReadRepoFile("Docs/Architecture/Tick-Simulation-Canonical-Spec.md");

            Assert.That(spec, Does.Contain("structured trace의 `Plan=` / `SourcePlan=` token은 canonical structured trace surface다."));
            Assert.That(spec, Does.Contain("`G=` token은 compatibility token in free-form event log다."));
            Assert.That(spec, Does.Contain("current `ActionPlanId` value를 mirror하지만 old semantic GroupId revival이 아니다."));
            Assert.That(spec, Does.Contain("새 parser/test/tooling은 `G=`를 canonical parser surface로 읽지 않고"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayRulesAppendix_DefinesSourceOfTruthReadingOrder_ForTraceAndFreeFormLogs()
        {
            var appendix = ReadRepoFile("Docs/Architecture/Gameplay-Rules-Appendix.md");

            Assert.That(appendix, Does.Contain("source-of-truth reading order는 typed runtime carrier -> canonical structured trace `Plan=` / `SourcePlan=` -> free-form compatibility log `G=`다."));
            Assert.That(appendix, Does.Contain("free-form `G=` token은 human-readable compatibility surface일 뿐이며 canonical parser input이 아니다."));
            Assert.That(appendix, Does.Contain("obsolete alias token이지 old semantic GroupId revival이 아니다."));
            Assert.That(appendix, Does.Contain("primary source-of-truth는 `ActionPlanId` / `SourceActionPlanId`와 canonical structured trace `Plan=` / `SourcePlan=`다."));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTestAutomationGuide_ReservesGForCompatibility_AndDirectsNewToolingToPlanTokens()
        {
            var guide = ReadRepoFile("Docs/Testing/Gameplay-Test-Automation-Guide.md");

            Assert.That(guide, Does.Contain("governance-facing canonical structured trace surface는 `Plan=` / `SourcePlan=`다."));
            Assert.That(guide, Does.Contain("free-form `G=`는 compatibility token in free-form event log이며 current `ActionPlanId` value를 mirror하지만 old semantic GroupId revival이 아니다."));
            Assert.That(guide, Does.Contain("새 테스트/도구는 `G=` 대신 `ActionPlanId` / `SourceActionPlanId` 또는 `Plan=` / `SourcePlan=`를 읽어야 한다."));
            Assert.That(guide, Does.Contain("the governance-facing canonical structured trace surface is `Plan=` / `SourcePlan=`"));
            Assert.That(guide, Does.Contain("new tests/tools must read `ActionPlanId` / `SourceActionPlanId` or `Plan=` / `SourcePlan=`, not `G=`"));
        }

        [Test]
        [Category("Extended")]
        public void Adr_RecordsWhyFreeFormGTokenRename_IsDeferred()
        {
            var adr = ReadRepoFile("Docs/Architecture/ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md");

            Assert.That(adr, Does.Contain("structured trace `Plan=` / `SourcePlan=`는 canonical structured trace surface로 고정한다."));
            Assert.That(adr, Does.Contain("free-form `G=` token은 compatibility token in free-form event log로만 남기며 old semantic GroupId revival로 해석하지 않는다."));
            Assert.That(adr, Does.Contain("`G=` rename은 지금 하지 않는다."));
            Assert.That(adr, Does.Contain("broader free-form logging cleanup, legacy string assertion rewrite, token consolidation은 later logging cleanup 단계로 넘긴다."));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
