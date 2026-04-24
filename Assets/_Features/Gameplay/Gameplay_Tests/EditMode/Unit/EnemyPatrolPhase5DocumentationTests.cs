using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyPatrolPhase5DocumentationTests
    {
        [Test]
        [Category("Extended")]
        public void EnemyPatrolArchitectureReadme_ListsActivePhase5TruthSources_AndSeparatesHistoricalNotes()
        {
            var readme = ReadProjectFile("Docs/Architecture/README.md");
            var phase2Index = IndexOfOrFail(readme, "Gameplay-EnemyPatrol-Phase2-SpecialCase-Responsibility-Map.md");
            var contractIndex = IndexOfOrFail(readme, "Gameplay-EnemyPatrol-Decision-Proposal-Contract.md");
            var phase3Index = IndexOfOrFail(readme, "Gameplay-EnemyPatrol-Phase3-Forward-Commonization.md");
            var gateIndex = IndexOfOrFail(readme, "Gameplay-EnemyPatrol-Forward-Rollout-Gate.md");
            var phase4Index = IndexOfOrFail(readme, "Gameplay-EnemyPatrol-Phase4-WallFollow-Decision.md");
            var rolloutIndex = IndexOfOrFail(readme, "Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md");
            var closeExecutionIndex = IndexOfOrFail(readme, "Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md");
            var historicalHeadingIndex = IndexOfOrFail(readme, "## Historical Supporting Notes");
            var readinessHeadingIndex = IndexOfOrFail(readme, "## Inactive Readiness Templates");

            Assert.That(readme, Does.Contain("## Enemy Patrol bounded rollout"));
            Assert.That(phase2Index, Is.GreaterThanOrEqualTo(0));
            Assert.That(contractIndex, Is.GreaterThan(phase2Index));
            Assert.That(phase3Index, Is.GreaterThan(contractIndex));
            Assert.That(gateIndex, Is.GreaterThan(phase3Index));
            Assert.That(phase4Index, Is.GreaterThan(gateIndex));
            Assert.That(rolloutIndex, Is.GreaterThan(phase4Index));
            Assert.That(closeExecutionIndex, Is.GreaterThan(rolloutIndex));
            Assert.That(historicalHeadingIndex, Is.GreaterThan(closeExecutionIndex));
            Assert.That(readinessHeadingIndex, Is.GreaterThan(historicalHeadingIndex));

            Assert.That(readme, Does.Contain("current supporting truth for phase 5 official close decision"));
            Assert.That(readme, Does.Contain("same-revision targeted evidence bundle"));
            Assert.That(readme, Does.Contain("historical supporting note"));
            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md"));
            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md"));
            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md"));
            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase6-JumpChaser-Readiness.md"));
            Assert.That(readme, Does.Contain("Gameplay-EnemyPatrol-Phase6-Charge-Readiness.md"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5RolloutDoc_DefinesClosedStatus_CurrentTruth_AndExplicitNonClaims()
        {
            var doc = ReadProjectFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Rollout.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Phase 5: `WindupMelee` Bounded Rollout"));
            Assert.That(doc, Does.Contain("현재 상태: `closed`"));
            Assert.That(doc, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md"));
            Assert.That(doc, Does.Contain("historical supporting note"));
            Assert.That(doc, Does.Contain("## 4. drift matrix"));
            Assert.That(doc, Does.Contain("## 5. pilot patrol preset scorecard"));
            Assert.That(doc, Does.Contain("## 6. same-cell passive-contact / candidate ordering matrix"));
            Assert.That(doc, Does.Contain("## 7. evaluation sampling matrix"));
            Assert.That(doc, Does.Contain("## 11. post-phase decision matrix"));
            Assert.That(doc, Does.Contain("## 13. out-of-scope / no-touch"));
            Assert.That(doc, Does.Contain("## 14. rollback / success / failure"));
            Assert.That(doc, Does.Contain("## 15. closed 의미와 explicit non-claims"));
            Assert.That(doc, Does.Contain("same-revision targeted evidence bundle"));
            Assert.That(doc, Does.Contain("broad/full suite closed"));
            Assert.That(doc, Does.Contain("phase 6 자동 착수"));
            Assert.That(doc, Does.Contain("`Forward` cleanup"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5CloseExecutionDoc_DefinesCloseGate_EvidenceHierarchy_ApproveHold_AndNoTouchBoundary()
        {
            var doc = ReadProjectFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md");

            Assert.That(doc, Does.Contain("# Enemy Patrol Phase 5: `WindupMelee` Close Retry Execution"));
            Assert.That(doc, Does.Contain("현재 상태: `closed`"));
            Assert.That(doc, Does.Contain("`Close Retry Ready`"));
            Assert.That(doc, Does.Contain("## Scope"));
            Assert.That(doc, Does.Contain("## Executed Commands"));
            Assert.That(doc, Does.Contain("## Artifact List With Exact Dates"));
            Assert.That(doc, Does.Contain("## Result Summary"));
            Assert.That(doc, Does.Contain("## Reviewed Truth Sources"));
            Assert.That(doc, Does.Contain("## Close Gate"));
            Assert.That(doc, Does.Contain("## Supporting Truth-Source Hierarchy"));
            Assert.That(doc, Does.Contain("## Close Wording Migration"));
            Assert.That(doc, Does.Contain("## Decision Outcome"));
            Assert.That(doc, Does.Contain("Approved -> Closed"));
            Assert.That(doc, Does.Contain("Held -> Close Retry Ready 유지"));
            Assert.That(doc, Does.Contain("## Allowed Claims"));
            Assert.That(doc, Does.Contain("## Explicit Non-Claims"));
            Assert.That(doc, Does.Contain("## No-Touch Confirmation"));
            Assert.That(doc, Does.Contain("## Phase 6 Boundary"));
            Assert.That(doc, Does.Contain("## Open Risks"));
            Assert.That(doc, Does.Contain("bundle integrity"));
            Assert.That(doc, Does.Contain("baseline control"));
            Assert.That(doc, Does.Contain("runtime parity"));
            Assert.That(doc, Does.Contain("scenario parity"));
            Assert.That(doc, Does.Contain("replay / determinism"));
            Assert.That(doc, Does.Contain("authoring / doc / no-touch"));
            Assert.That(doc, Does.Contain("governance close lock"));
            Assert.That(doc, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md"));
            Assert.That(doc, Does.Contain("historical supporting note"));
            Assert.That(doc, Does.Contain("inactive readiness template"));
            Assert.That(doc, Does.Contain("baseline `EnemyAi_WindupMelee.asset` / `EnemyBrain_WindupMelee.asset` untouched"));
            Assert.That(doc, Does.Contain("broad/full suite"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5HistoricalDocs_AreMarkedAsHistoricalSupportingNotes_AndPointToCloseExecutionDoc()
        {
            var fixup = ReadProjectFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Fixup.md");
            var redClosure = ReadProjectFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Red-Closure-Plan.md");
            var runtimeFix = ReadProjectFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Runtime-Fix-Plan.md");

            AssertHistoricalNote(fixup);
            AssertHistoricalNote(redClosure);
            AssertHistoricalNote(runtimeFix);

            Assert.That(fixup, Does.Contain("runtime parity recovery path"));
            Assert.That(redClosure, Does.Contain("historical red-state hardening note"));
            Assert.That(runtimeFix, Does.Contain("bounded runtime fix touch set"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5EvidenceSummary_KeepsCloseRetryReadyVerdict_AndDefersFinalDecisionToExecutionDoc()
        {
            var summary = ReadProjectFile("TestResults/phase5-red-closure/evidence-summary.md");
            var artifactInventory = SliceSection(summary, "## Artifact Inventory", "## Retry Boundary");

            Assert.That(summary, Does.Contain("# Phase 5 Red-Closure Evidence Summary"));
            Assert.That(summary, Does.Contain("Verdict: `Close Retry Ready`"));
            Assert.That(summary, Does.Contain("Final Decision: `Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md`"));
            Assert.That(summary, Does.Contain("official close decision is recorded in `Docs/Architecture/Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md`"));
            Assert.That(summary, Does.Not.Contain("Verdict: `Closed`"));
            Assert.That(artifactInventory, Does.Not.Contain("lose-target-debug"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5ReadinessDocs_StayInactiveTemplates_AndDoNotAutoOpenPhase6()
        {
            var jumpDoc = ReadProjectFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase6-JumpChaser-Readiness.md");
            var chargeDoc = ReadProjectFile("Docs/Architecture/Gameplay-EnemyPatrol-Phase6-Charge-Readiness.md");

            Assert.That(jumpDoc, Does.Contain("readiness artifact template"));
            Assert.That(jumpDoc, Does.Contain("active rollout truth-source가 아니다"));
            Assert.That(jumpDoc, Does.Contain("자동 활성화"));
            Assert.That(jumpDoc, Does.Contain("별도 readiness decision artifact 승인"));

            Assert.That(chargeDoc, Does.Contain("readiness artifact template"));
            Assert.That(chargeDoc, Does.Contain("active rollout truth-source가 아니다"));
            Assert.That(chargeDoc, Does.Contain("자동 활성화"));
            Assert.That(chargeDoc, Does.Contain("별도 readiness decision artifact 승인"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolPhase5EvidenceBundle_ContainsMandatoryArtifacts_AndConsistentRevisionMarkers()
        {
            const string SummaryPath = "TestResults/phase5-red-closure/evidence-summary.md";
            var summary = ReadProjectFile(SummaryPath);
            var summaryRevision = ExtractMarkdownCodeValue(summary, "- Base Revision:");
            var summaryExecutedAt = ParseUtc(ExtractMarkdownCodeValue(summary, "- Executed At (UTC):"));

            var requiredArtifacts = new[]
            {
                "TestResults/phase5-red-closure/baseline-control-targeted.xml",
                "TestResults/phase5-red-closure/baseline-control-targeted.log",
                "TestResults/phase5-red-closure/unit-runtime-targeted.xml",
                "TestResults/phase5-red-closure/unit-runtime-targeted.log",
                "TestResults/phase5-red-closure/scenario-targeted.xml",
                "TestResults/phase5-red-closure/scenario-targeted.log",
                "TestResults/phase5-red-closure/replay-targeted.xml",
                "TestResults/phase5-red-closure/replay-targeted.log",
                "TestResults/phase5-red-closure/unit-authoring-doc-targeted.xml",
                "TestResults/phase5-red-closure/unit-authoring-doc-targeted.log",
                "TestResults/phase5-red-closure/attackcommitted-gate-summary.txt",
                "TestResults/phase5-red-closure/lockedtargetlost-conditional-summary.txt",
                "TestResults/phase5-red-closure/patrol-write-triage-summary.txt",
            };

            for (var i = 0; i < requiredArtifacts.Length; i++)
            {
                var absolutePath = GetAbsolutePath(requiredArtifacts[i]);
                Assert.That(File.Exists(absolutePath), Is.True, $"Missing required artifact: {requiredArtifacts[i]}");
            }

            var attackSummary = ReadProjectFile("TestResults/phase5-red-closure/attackcommitted-gate-summary.txt");
            var lockedSummary = ReadProjectFile("TestResults/phase5-red-closure/lockedtargetlost-conditional-summary.txt");
            var patrolSummary = ReadProjectFile("TestResults/phase5-red-closure/patrol-write-triage-summary.txt");

            Assert.That(ExtractKeyValue(attackSummary, "Revision"), Is.EqualTo(summaryRevision));
            Assert.That(ExtractKeyValue(lockedSummary, "Revision"), Is.EqualTo(summaryRevision));
            Assert.That(ExtractKeyValue(patrolSummary, "Revision"), Is.EqualTo(summaryRevision));

            var times = new List<DateTime>
            {
                summaryExecutedAt,
                ParseUtc(ExtractKeyValue(attackSummary, "ExecutedAtUtc")),
                ParseUtc(ExtractKeyValue(lockedSummary, "ExecutedAtUtc")),
                ParseUtc(ExtractKeyValue(patrolSummary, "ExecutedAtUtc")),
            };

            AddXmlStartTimes(times, requiredArtifacts.Where(path => path.EndsWith(".xml", StringComparison.Ordinal) && !path.EndsWith("unit-authoring-doc-targeted.xml", StringComparison.Ordinal)).ToArray());
            AddLogDates(times, requiredArtifacts.Where(path => path.EndsWith(".log", StringComparison.Ordinal) && !path.EndsWith("unit-authoring-doc-targeted.log", StringComparison.Ordinal)).ToArray());

            var minTime = times.Min();
            var maxTime = times.Max();
            Assert.That(maxTime - minTime, Is.LessThanOrEqualTo(TimeSpan.FromHours(2)), "Phase 5 close bundle must stay within one close execution window.");
        }

        private static void AssertHistoricalNote(string doc)
        {
            Assert.That(doc, Does.Contain("Historical supporting note."));
            Assert.That(doc, Does.Contain("current active close gate가 아니며"));
            Assert.That(doc, Does.Contain("current active truth는"));
            Assert.That(doc, Does.Contain("Gameplay-EnemyPatrol-Phase5-WindupMelee-Close-Retry-Execution.md"));
        }

        private static void AddXmlStartTimes(List<DateTime> times, IReadOnlyList<string> xmlPaths)
        {
            for (var i = 0; i < xmlPaths.Count; i++)
            {
                var xml = ReadProjectFile(xmlPaths[i]);
                var match = Regex.Match(xml, "start-time=\"(?<time>[^\"]+)\"", RegexOptions.CultureInvariant);
                Assert.That(match.Success, Is.True, $"Missing start-time in XML: {xmlPaths[i]}");
                times.Add(ParseUtc(match.Groups["time"].Value));
            }
        }

        private static void AddLogDates(List<DateTime> times, IReadOnlyList<string> logPaths)
        {
            for (var i = 0; i < logPaths.Count; i++)
            {
                var log = ReadProjectFile(logPaths[i]);
                var match = Regex.Match(log, @"^Date:\s*(?<time>.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
                Assert.That(match.Success, Is.True, $"Missing Date line in log: {logPaths[i]}");
                times.Add(ParseUtc(match.Groups["time"].Value.Trim()));
            }
        }

        private static string ExtractMarkdownCodeValue(string content, string label)
        {
            var match = Regex.Match(content, $"^{Regex.Escape(label)}\\s*`(?<value>[^`]+)`", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            Assert.That(match.Success, Is.True, $"Missing markdown code value for '{label}'.");
            return match.Groups["value"].Value.Trim();
        }

        private static string ExtractKeyValue(string content, string key)
        {
            var match = Regex.Match(content, $"^{Regex.Escape(key)}=(?<value>.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            Assert.That(match.Success, Is.True, $"Missing key '{key}'.");
            return match.Groups["value"].Value.Trim();
        }

        private static DateTime ParseUtc(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        private static int IndexOfOrFail(string content, string needle)
        {
            var index = content.IndexOf(needle, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"Missing text: {needle}");
            return index;
        }

        private static string SliceSection(string content, string startHeading, string endHeading)
        {
            var startIndex = IndexOfOrFail(content, startHeading);
            var endIndex = IndexOfOrFail(content, endHeading);
            Assert.That(endIndex, Is.GreaterThan(startIndex), $"Section order invalid: {startHeading} -> {endHeading}");
            return content.Substring(startIndex, endIndex - startIndex);
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath));
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
