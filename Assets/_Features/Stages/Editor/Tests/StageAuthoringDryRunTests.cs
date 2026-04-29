using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringDryRunTests
    {
        [Test]
        public void DryRunDoesNotPersistNewEntityIdMappings()
        {
            var fixture = StageAuthoringTestFixture.Create();
            try
            {
                var before = fixture.Authoring.EntityIdMappings.Count;
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.DryRunValidation);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Authoring.EntityIdMappings.Count, Is.EqualTo(before));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void DryRunDoesNotRetireDeletedMappings()
        {
            var fixture = StageAuthoringTestFixture.CreateSynced();
            try
            {
                var mappingsBefore = fixture.Authoring.EntityIdMappings.ToArray();
                fixture.Authoring.SetPlacements(fixture.Authoring.Placements.Where(placement => placement.StableGuid != "enemy-a"));
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.DryRunValidation);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                CollectionAssert.AreEqual(mappingsBefore.Select(mapping => mapping.Retired), fixture.Authoring.EntityIdMappings.Select(mapping => mapping.Retired));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void DryRunDoesNotModifyGameplayOutput()
        {
            var fixture = StageAuthoringTestFixture.Create();
            try
            {
                var before = EditorJsonUtility.ToJson(fixture.Gameplay);
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.DryRunValidation);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(EditorJsonUtility.ToJson(fixture.Gameplay), Is.EqualTo(before));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void DryRunDoesNotModifyPresentationOutput()
        {
            var fixture = StageAuthoringTestFixture.Create();
            try
            {
                var before = EditorJsonUtility.ToJson(fixture.Presentation);
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.DryRunValidation);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(EditorJsonUtility.ToJson(fixture.Presentation), Is.EqualTo(before));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void ValidateDoesNotAllocateEntityIds()
        {
            var fixture = StageAuthoringTestFixture.Create();
            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.DryRunValidation);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Authoring.EntityIdMappings, Is.Empty);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void WriteGeneratePersistsMappingsAndOutputs()
        {
            var fixture = StageAuthoringTestFixture.Create();
            try
            {
                var report = StageAuthoringGenerator.Generate(fixture.Authoring, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                Assert.That(fixture.Authoring.EntityIdMappings.Count, Is.GreaterThan(0));
                Assert.That(fixture.Gameplay.Spawns.Length, Is.GreaterThan(0));
                Assert.That(fixture.Presentation.EnemyPresentationBindings.Length, Is.GreaterThan(0));
                Assert.That(fixture.Presentation.StaticEntityPresentationBindings.Length, Is.GreaterThan(0));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static string FormatIssues(StageAuthoringGenerationReport report)
        {
            return string.Join(System.Environment.NewLine, report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }
    }
}
