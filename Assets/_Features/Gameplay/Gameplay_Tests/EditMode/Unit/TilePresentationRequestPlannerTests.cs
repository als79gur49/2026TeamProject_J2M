using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TilePresentationRequestPlannerTests
    {
        [Test]
        [Category("Extended")]
        public void BuildRequests_ButtonActivated_PreservesPresentationFacts()
        {
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var tileEvent = new TilePresentationEvent(
                TilePresentationEventKind.ButtonActivated,
                100,
                cell,
                TileFeatureKind.Button,
                30,
                40,
                2);
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(tileEvent));

            Assert.That(requests, Has.Count.EqualTo(1));
            var request = requests[0];
            Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.ButtonActivated));
            Assert.That(request.TileId, Is.EqualTo(100));
            Assert.That(request.Cell, Is.EqualTo(cell));
            Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.Button));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.OwnerEntityId, Is.EqualTo(40));
            Assert.That(request.TeamId, Is.EqualTo(2));
            Assert.That(request.TargetEntityId, Is.Zero);
            Assert.That(request.Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_DestroyTileTriggered_PreservesPresentationFacts()
        {
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var tileEvent = new TilePresentationEvent(
                TilePresentationEventKind.DestroyTileTriggered,
                100,
                cell,
                TileFeatureKind.Destroy,
                30,
                40,
                2,
                targetEntityId: 50);
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(tileEvent));

            Assert.That(requests, Has.Count.EqualTo(1));
            var request = requests[0];
            Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.DestroyTileTriggered));
            Assert.That(request.TileId, Is.EqualTo(100));
            Assert.That(request.Cell, Is.EqualTo(cell));
            Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.Destroy));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.OwnerEntityId, Is.EqualTo(40));
            Assert.That(request.TeamId, Is.EqualTo(2));
            Assert.That(request.TargetEntityId, Is.EqualTo(50));
            Assert.That(request.Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_SlideTileRedirected_PreservesPresentationFactsAndDirection()
        {
            var cell = new SurfaceCell(FaceId.Front, 2, 3);
            var tileEvent = new TilePresentationEvent(
                TilePresentationEventKind.SlideTileRedirected,
                100,
                cell,
                TileFeatureKind.Slide,
                30,
                40,
                2,
                targetEntityId: 50,
                direction: Direction.Up);
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(tileEvent));

            Assert.That(requests, Has.Count.EqualTo(1));
            var request = requests[0];
            Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.SlideTileRedirected));
            Assert.That(request.TileId, Is.EqualTo(100));
            Assert.That(request.Cell, Is.EqualTo(cell));
            Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.Slide));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.OwnerEntityId, Is.EqualTo(40));
            Assert.That(request.TeamId, Is.EqualTo(2));
            Assert.That(request.TargetEntityId, Is.EqualTo(50));
            Assert.That(request.Direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_BarricadeBlocked_PreservesPresentationFactsAndDirection()
        {
            var cell = new SurfaceCell(FaceId.Front, 2, 3);
            var tileEvent = new TilePresentationEvent(
                TilePresentationEventKind.BarricadeBlocked,
                100,
                cell,
                TileFeatureKind.Barricade,
                30,
                40,
                2,
                targetEntityId: 50,
                direction: Direction.Right);
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(tileEvent));

            Assert.That(requests, Has.Count.EqualTo(1));
            var request = requests[0];
            Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.BarricadeBlocked));
            Assert.That(request.TileId, Is.EqualTo(100));
            Assert.That(request.Cell, Is.EqualTo(cell));
            Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.OwnerEntityId, Is.EqualTo(40));
            Assert.That(request.TeamId, Is.EqualTo(2));
            Assert.That(request.TargetEntityId, Is.EqualTo(50));
            Assert.That(request.Direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_BarricadeCrushed_PreservesPresentationFacts()
        {
            var cell = new SurfaceCell(FaceId.Front, 2, 3);
            var tileEvent = new TilePresentationEvent(
                TilePresentationEventKind.BarricadeCrushed,
                100,
                cell,
                TileFeatureKind.Barricade,
                30,
                40,
                2,
                targetEntityId: 50);
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(tileEvent));

            Assert.That(requests, Has.Count.EqualTo(1));
            var request = requests[0];
            Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.BarricadeCrushed));
            Assert.That(request.TileId, Is.EqualTo(100));
            Assert.That(request.Cell, Is.EqualTo(cell));
            Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.OwnerEntityId, Is.EqualTo(40));
            Assert.That(request.TeamId, Is.EqualTo(2));
            Assert.That(request.TargetEntityId, Is.EqualTo(50));
            Assert.That(request.Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_BarricadeActiveStateEvents_PreservePresentationFacts()
        {
            var cell = new SurfaceCell(FaceId.Front, 2, 3);
            var planner = new TilePresentationRequestPlanner();
            var presentationData = CreatePresentationData(
                new TilePresentationEvent(
                    TilePresentationEventKind.BarricadeActivated,
                    100,
                    cell,
                    TileFeatureKind.Barricade,
                    30,
                    40,
                    2),
                new TilePresentationEvent(
                    TilePresentationEventKind.BarricadeDeactivated,
                    101,
                    cell,
                    TileFeatureKind.Barricade,
                    31,
                    41,
                    3));

            var requests = planner.BuildRequests(presentationData);

            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests[0].RequestKind, Is.EqualTo(TilePresentationRequestKind.BarricadeActivated));
            Assert.That(requests[0].TileId, Is.EqualTo(100));
            Assert.That(requests[0].Cell, Is.EqualTo(cell));
            Assert.That(requests[0].TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(requests[0].SourceEntityId, Is.EqualTo(30));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(40));
            Assert.That(requests[0].TeamId, Is.EqualTo(2));
            Assert.That(requests[0].TargetEntityId, Is.Zero);
            Assert.That(requests[0].Direction, Is.EqualTo(Direction.None));
            Assert.That(requests[1].RequestKind, Is.EqualTo(TilePresentationRequestKind.BarricadeDeactivated));
            Assert.That(requests[1].TileId, Is.EqualTo(101));
            Assert.That(requests[1].SourceEntityId, Is.EqualTo(31));
            Assert.That(requests[1].OwnerEntityId, Is.EqualTo(41));
            Assert.That(requests[1].TeamId, Is.EqualTo(3));
            Assert.That(requests[1].TargetEntityId, Is.Zero);
            Assert.That(requests[1].Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_ExitEvents_PreservePresentationFacts()
        {
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var planner = new TilePresentationRequestPlanner();
            var presentationData = CreatePresentationData(
                new TilePresentationEvent(
                    TilePresentationEventKind.ExitOpened,
                    100,
                    cell,
                    TileFeatureKind.Exit,
                    30,
                    40,
                    2),
                new TilePresentationEvent(
                    TilePresentationEventKind.ExitEntered,
                    100,
                    cell,
                    TileFeatureKind.Exit,
                    30,
                    40,
                    2,
                    targetEntityId: 50));

            var requests = planner.BuildRequests(presentationData);

            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests[0].RequestKind, Is.EqualTo(TilePresentationRequestKind.ExitOpened));
            Assert.That(requests[0].TileId, Is.EqualTo(100));
            Assert.That(requests[0].Cell, Is.EqualTo(cell));
            Assert.That(requests[0].TileFeatureKind, Is.EqualTo(TileFeatureKind.Exit));
            Assert.That(requests[0].SourceEntityId, Is.EqualTo(30));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(40));
            Assert.That(requests[0].TeamId, Is.EqualTo(2));
            Assert.That(requests[0].TargetEntityId, Is.Zero);
            Assert.That(requests[0].Direction, Is.EqualTo(Direction.None));
            Assert.That(requests[1].RequestKind, Is.EqualTo(TilePresentationRequestKind.ExitEntered));
            Assert.That(requests[1].TargetEntityId, Is.EqualTo(50));
            Assert.That(requests[1].Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_MoonBlockGenerated_PreservesPresentationFacts()
        {
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var tileEvent = new TilePresentationEvent(
                TilePresentationEventKind.MoonBlockGenerated,
                100,
                cell,
                TileFeatureKind.MoonBlockGenerator,
                30,
                40,
                2,
                targetEntityId: 50);
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(tileEvent));

            Assert.That(requests, Has.Count.EqualTo(1));
            var request = requests[0];
            Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.MoonBlockGenerated));
            Assert.That(request.TileId, Is.EqualTo(100));
            Assert.That(request.Cell, Is.EqualTo(cell));
            Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.MoonBlockGenerator));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.OwnerEntityId, Is.EqualTo(40));
            Assert.That(request.TeamId, Is.EqualTo(2));
            Assert.That(request.TargetEntityId, Is.EqualTo(50));
            Assert.That(request.Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_MoonBlockGeneratorBlocked_PreservesPresentationFacts()
        {
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var tileEvent = new TilePresentationEvent(
                TilePresentationEventKind.MoonBlockGeneratorBlocked,
                100,
                cell,
                TileFeatureKind.MoonBlockGenerator,
                30,
                40,
                2,
                targetEntityId: 50);
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(tileEvent));

            Assert.That(requests, Has.Count.EqualTo(1));
            var request = requests[0];
            Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.MoonBlockGeneratorBlocked));
            Assert.That(request.TileId, Is.EqualTo(100));
            Assert.That(request.Cell, Is.EqualTo(cell));
            Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.MoonBlockGenerator));
            Assert.That(request.SourceEntityId, Is.EqualTo(30));
            Assert.That(request.OwnerEntityId, Is.EqualTo(40));
            Assert.That(request.TeamId, Is.EqualTo(2));
            Assert.That(request.TargetEntityId, Is.EqualTo(50));
            Assert.That(request.Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_PreservesTileEventOrder()
        {
            var planner = new TilePresentationRequestPlanner();
            var presentationData = CreatePresentationData(
                CreateButtonActivatedEvent(30, new SurfaceCell(FaceId.Floor, 0, 0)),
                new TilePresentationEvent(
                    TilePresentationEventKind.SlideTileRedirected,
                    10,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    TileFeatureKind.Slide,
                    0,
                    0,
                    0,
                    targetEntityId: 50,
                    direction: Direction.Up),
                CreateButtonActivatedEvent(20, new SurfaceCell(FaceId.Front, 0, 1)));

            var requests = planner.BuildRequests(presentationData);

            Assert.That(requests.Select(request => request.TileId).ToArray(), Is.EqualTo(new[] { 30, 10, 20 }));
            Assert.That(
                requests.Select(request => request.RequestKind).ToArray(),
                Is.EqualTo(new[]
                {
                    TilePresentationRequestKind.ButtonActivated,
                    TilePresentationRequestKind.SlideTileRedirected,
                    TilePresentationRequestKind.ButtonActivated,
                }));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_PreservesSameTickDuplicates()
        {
            var tileEvent = CreateButtonActivatedEvent(100, new SurfaceCell(FaceId.Floor, 1, 1));
            var planner = new TilePresentationRequestPlanner();

            var requests = planner.BuildRequests(CreatePresentationData(tileEvent, tileEvent));

            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests.All(request => request.TileId == 100), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_IgnoresNoneAndUnsupportedTileEventKinds()
        {
            var planner = new TilePresentationRequestPlanner();
            var presentationData = CreatePresentationData(
                new TilePresentationEvent(
                    TilePresentationEventKind.None,
                    10,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    TileFeatureKind.Button,
                    0,
                    0,
                    0),
                new TilePresentationEvent(
                    (TilePresentationEventKind)999,
                    20,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    TileFeatureKind.Button,
                    0,
                    0,
                    0),
                CreateButtonActivatedEvent(30, new SurfaceCell(FaceId.Floor, 2, 0)));

            var requests = planner.BuildRequests(presentationData);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].TileId, Is.EqualTo(30));
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_EmptyPresentationData_ReturnsEmptyRequests()
        {
            var planner = new TilePresentationRequestPlanner();

            Assert.That(planner.BuildRequests(TickPresentationData.Empty), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BuildRequests_NullPresentationData_Throws()
        {
            var planner = new TilePresentationRequestPlanner();

            Assert.Throws<ArgumentNullException>(() => planner.BuildRequests(null));
        }

        private static TilePresentationEvent CreateButtonActivatedEvent(int tileId, SurfaceCell cell)
        {
            return new TilePresentationEvent(
                TilePresentationEventKind.ButtonActivated,
                tileId,
                cell,
                TileFeatureKind.Button,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TickPresentationData CreatePresentationData(params TilePresentationEvent[] tileEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents);
        }
    }
}
