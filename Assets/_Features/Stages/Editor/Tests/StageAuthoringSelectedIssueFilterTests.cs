using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringSelectedIssueFilterTests
    {
        [Test]
        public void IssueSummary_FiltersByStableGuid()
        {
            var placement = Placement("selected",  "box_showcase");
            var issues = new[]
            {
                Issue(stableGuid: "selected"),
                Issue(stableGuid: "other"),
            };

            var filtered = StageAuthoringSelectedIssueFilter.Filter(issues, placement, entityId: 0);

            Assert.That(filtered, Has.Length.EqualTo(1));
            Assert.That(filtered[0].StableGuid, Is.EqualTo("selected"));
        }

        [Test]
        public void IssueSummary_FiltersByEntityId()
        {
            var placement = Placement("selected", "box_showcase");
            var issues = new[]
            {
                Issue(entityId: 207),
                Issue(entityId: 301),
            };

            var filtered = StageAuthoringSelectedIssueFilter.Filter(issues, placement, entityId: 207);

            Assert.That(filtered, Has.Length.EqualTo(1));
            Assert.That(filtered[0].EntityId, Is.EqualTo(207));
        }

        [Test]
        public void IssueSummary_ShowsPresentationCatalogIssue()
        {
            var placement = Placement("selected", "box_showcase");
            var issues = new[]
            {
                Issue(code: "PresentationCatalog.StaticPresentationIdMissing", presentationId: "box_showcase"),
            };

            var filtered = StageAuthoringSelectedIssueFilter.Filter(issues, placement, entityId: 0);

            Assert.That(filtered, Has.Length.EqualTo(1));
            Assert.That(filtered[0].Code, Is.EqualTo("PresentationCatalog.StaticPresentationIdMissing"));
        }

        [Test]
        public void IssueSummary_DoesNotShowUnrelatedPlacementIssue()
        {
            var placement = Placement("selected", "box_showcase");
            var issues = new[]
            {
                Issue(stableGuid: "other", entityId: 301, presentationId: "other_showcase"),
            };

            var filtered = StageAuthoringSelectedIssueFilter.Filter(issues, placement, entityId: 207);

            Assert.That(filtered, Is.Empty);
        }

        [Test]
        public void IssueSummary_EmptyWhenNoReport()
        {
            var placement = Placement("selected", "box_showcase");

            var filtered = StageAuthoringSelectedIssueFilter.Filter(null, placement, entityId: 207);

            Assert.That(filtered, Is.Empty);
        }

        private static StagePlacedEntityAuthoring Placement(string stableGuid, string presentationId)
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = stableGuid,
                DisplayName = stableGuid,
                Kind = StageAuthoringEntityKind.Box,
                Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                Facing = Direction.Right,
                Hp = 1,
                BoxCapabilities = BoxCapabilities.Push,
                PresentationId = presentationId,
            };
        }

        private static StageValidationIssue Issue(
            string code = "Test.Issue",
            string stableGuid = "",
            int entityId = 0,
            string presentationId = "")
        {
            return new StageValidationIssue(
                StageValidationSeverity.Warning,
                code,
                $"Issue for {stableGuid} {entityId} {presentationId}",
                stableGuid: stableGuid,
                entityId: entityId,
                presentationId: presentationId);
        }
    }
}
