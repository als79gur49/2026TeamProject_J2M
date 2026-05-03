using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class FullEditModeKnownFailureBaselineTests
    {
        private const string KnownFailureName =
            "Game.Feature.Gameplay.Tests.Unit.ExistingFixture.ExistingFailure";
        private const string NewFailureName =
            "Game.Feature.Gameplay.Tests.Unit.NewFixture.NewFailure";
        private const string StageAuthoringFailureName =
            "Game.Feature.Stages.Editor.Tests.StageAuthoringSyntheticKnownFailureTests.SyntheticKnownFailure";

        [Test]
        public void FullEditModeBaseline_ParsesFailedTestsFromNUnitXml()
        {
            var result = FullEditModeKnownFailureBaseline.ParseNUnitXml(SampleXml());

            Assert.That(result.failedTests.Length, Is.EqualTo(2));
            Assert.That(result.failedTests[0].testFullName, Is.EqualTo(KnownFailureName));
            Assert.That(result.failedTests[0].assembly, Is.EqualTo("Game.Feature.Gameplay.Tests.dll"));
            Assert.That(result.failedTests[0].failureMessageHash, Is.Not.Empty);
        }

        [Test]
        public void FullEditModeBaseline_ClassifiesKnownFailure()
        {
            var current = FullEditModeKnownFailureBaseline.ParseNUnitXml(SampleXml());
            var baseline = BaselineFor(current.failedTests[0]);

            var comparison = FullEditModeKnownFailureBaseline.Compare(current, baseline);

            Assert.That(comparison.knownFailuresStillFailing.Select(failure => failure.testFullName), Does.Contain(KnownFailureName));
        }

        [Test]
        public void FullEditModeBaseline_ClassifiesNewFailure()
        {
            var current = FullEditModeKnownFailureBaseline.ParseNUnitXml(SampleXml());
            var baseline = BaselineFor(current.failedTests[0]);

            var comparison = FullEditModeKnownFailureBaseline.Compare(current, baseline);

            Assert.That(comparison.newFailures.Select(failure => failure.testFullName), Does.Contain(NewFailureName));
            Assert.That(comparison.result, Is.EqualTo(FullEditModeBaselineResult.FailedWithNewFailures.ToString()));
        }

        [Test]
        public void FullEditModeBaseline_ClassifiesResolvedKnownFailure()
        {
            var current = FullEditModeKnownFailureBaseline.ParseNUnitXml(SampleXml());
            var baseline = BaselineFor(
                current.failedTests[0],
                new FullEditModeKnownFailure
                {
                    testFullName = "Game.Feature.Gameplay.Tests.Unit.ResolvedFixture.ResolvedFailure",
                    assembly = "Game.Feature.Gameplay.Tests.dll",
                    failureMessageHash = "resolved-hash",
                });

            var comparison = FullEditModeKnownFailureBaseline.Compare(current, baseline);

            Assert.That(
                comparison.resolvedKnownFailures.Select(failure => failure.testFullName),
                Does.Contain("Game.Feature.Gameplay.Tests.Unit.ResolvedFixture.ResolvedFailure"));
        }

        [Test]
        public void FullEditModeBaseline_StageAuthoringFailureIsBlockingEvenIfKnown()
        {
            var current = FullEditModeKnownFailureBaseline.ParseNUnitXml(StageAuthoringXml());
            var baseline = BaselineFor(current.failedTests[0]);

            var comparison = FullEditModeKnownFailureBaseline.Compare(current, baseline);

            Assert.That(comparison.stageAuthoringBlockingFailures.Length, Is.EqualTo(1));
            Assert.That(comparison.result, Is.EqualTo(FullEditModeBaselineResult.FailedWithStageAuthoringFailures.ToString()));
        }

        [Test]
        public void FullEditModeBaseline_SummaryCountsMatchXml()
        {
            var result = FullEditModeKnownFailureBaseline.ParseNUnitXml(SampleXml());

            Assert.That(result.summary.total, Is.EqualTo(3));
            Assert.That(result.summary.passed, Is.EqualTo(1));
            Assert.That(result.summary.failed, Is.EqualTo(2));
            Assert.That(result.summary.skipped, Is.EqualTo(0));
        }

        [Test]
        public void FullEditModeBaseline_ReportIncludesNewFailuresSection()
        {
            var current = FullEditModeKnownFailureBaseline.ParseNUnitXml(SampleXml());
            var comparison = FullEditModeKnownFailureBaseline.Compare(current, BaselineFor(current.failedTests[0]));

            var report = FullEditModeKnownFailureBaseline.BuildMarkdownReport(comparison);

            Assert.That(report, Does.Contain("### New Failures"));
            Assert.That(report, Does.Contain(NewFailureName));
        }

        [Test]
        public void FullEditModeBaseline_DefaultBaselineContainsExtractedFailureList()
        {
            var baseline = FullEditModeKnownFailureBaseline.LoadBaselineJson(
                File.ReadAllText(FullEditModeKnownFailureBaseline.DefaultBaselinePath));

            Assert.That(baseline.summary.failed, Is.EqualTo(95));
            Assert.That(baseline.knownFailures.Length, Is.EqualTo(95));
            Assert.That(
                baseline.knownFailures.Count(failure => failure.assembly == "Game.Feature.Stages.Editor.Tests.dll"),
                Is.EqualTo(0));
        }

        private static FullEditModeKnownFailureBaselineDocument BaselineFor(params FullEditModeTestFailure[] failures)
        {
            return new FullEditModeKnownFailureBaselineDocument
            {
                knownFailures = failures.Select(ToKnownFailure).ToArray(),
            };
        }

        private static FullEditModeKnownFailureBaselineDocument BaselineFor(
            FullEditModeTestFailure failure,
            FullEditModeKnownFailure additional)
        {
            return new FullEditModeKnownFailureBaselineDocument
            {
                knownFailures = new[] { ToKnownFailure(failure), additional },
            };
        }

        private static FullEditModeKnownFailure ToKnownFailure(FullEditModeTestFailure failure)
        {
            return new FullEditModeKnownFailure
            {
                testFullName = failure.testFullName,
                assembly = failure.assembly,
                failureMessageHash = failure.failureMessageHash,
                authoringRelated = false,
            };
        }

        private static string SampleXml()
        {
            return $@"
<test-run total='3' passed='1' failed='2' skipped='0'>
  <test-suite type='Assembly' name='Game.Feature.Gameplay.Tests.dll' total='3' passed='1' failed='2' skipped='0' result='Failed'>
    <test-case fullname='{KnownFailureName}' result='Failed'>
      <failure><message>known message</message></failure>
    </test-case>
    <test-case fullname='{NewFailureName}' result='Failed'>
      <failure><message>new message</message></failure>
    </test-case>
  </test-suite>
</test-run>";
        }

        private static string StageAuthoringXml()
        {
            return $@"
<test-run total='1' passed='0' failed='1' skipped='0'>
  <test-suite type='Assembly' name='Game.Feature.Stages.Editor.Tests.dll' total='1' passed='0' failed='1' skipped='0' result='Failed'>
    <test-case fullname='{StageAuthoringFailureName}' result='Failed'>
      <failure><message>stage authoring message</message></failure>
    </test-case>
  </test-suite>
</test-run>";
        }
    }
}
