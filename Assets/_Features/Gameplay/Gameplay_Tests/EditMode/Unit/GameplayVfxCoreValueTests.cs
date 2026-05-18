using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxCoreValueTests
    {
        [Test]
        [Category("Extended")]
        public void CueId_UsesTypedFamilyAndCodeIdentity()
        {
            var playerCue = GameplayVfxCueId.From(PlayerVfxCue.Damage);
            var boxCue = GameplayVfxCueId.From(BoxVfxCue.DestroySmoke);

            Assert.That(playerCue.Family, Is.EqualTo(GameplayVfxFamily.Player));
            Assert.That(playerCue.Code, Is.EqualTo((int)PlayerVfxCue.Damage));
            Assert.That(boxCue.Family, Is.EqualTo(GameplayVfxFamily.Box));
            Assert.That(boxCue.Code, Is.EqualTo((int)BoxVfxCue.DestroySmoke));
            Assert.That(typeof(GameplayVfxCueId).GetConstructors().Any(constructor =>
                constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(string))), Is.False);
            Assert.That(typeof(GameplayVfxCueId).GetMethod(nameof(GameplayVfxCueId.From), new[] { typeof(PlayerVfxCue) }), Is.Not.Null);
            Assert.That(typeof(GameplayVfxCueId).GetMethod(nameof(GameplayVfxCueId.From), new[] { typeof(BoxVfxCue) }), Is.Not.Null);
            Assert.That(typeof(GameplayVfxCueId).GetMethod(nameof(GameplayVfxCueId.From), new[] { typeof(EnemyVfxCue) }), Is.Not.Null);
            Assert.That(typeof(GameplayVfxCueId).GetMethod(nameof(GameplayVfxCueId.From), new[] { typeof(GravityFieldVfxCue) }), Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxRequest_SourceEntityId_ParticipatesInIdentity()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var anchor = VfxAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 1, 2),
                new CubeTopologyState(FaceId.Floor),
                VfxAnchorSlot.CellFloor);
            var missingSource = new GameplayVfxRequest(
                5,
                7,
                17,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);
            var sourceA = new GameplayVfxRequest(
                5,
                7,
                17,
                10,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);
            var sourceB = new GameplayVfxRequest(
                5,
                7,
                17,
                11,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);

            Assert.That(missingSource.SourceEntityId, Is.Zero);
            Assert.That(sourceA.SourceEntityId, Is.EqualTo(10));
            Assert.That(sourceA, Is.Not.EqualTo(sourceB));
            Assert.That(sourceA.GetHashCode(), Is.Not.EqualTo(sourceB.GetHashCode()));
            Assert.That(sourceA.CompareTo(sourceB), Is.LessThan(0));
            Assert.That(sourceB.CompareTo(sourceA), Is.GreaterThan(0));
            Assert.That(sourceA.ToString(), Does.Contain("SourceEntityId=10"));
        }

        [Test]
        [Category("Extended")]
        public void RequestPlan_SortsRequestsDeterministically()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var builder = new GameplayVfxRequestPlanBuilder();
            builder.Add(CreateRequest(2, 3, GameplayVfxCueId.From(EnemyVfxCue.Spawn), VfxAnchor.ForEntity(5)));
            builder.Add(CreateRequest(1, 2, GameplayVfxCueId.From(BoxVfxCue.DestroySmoke), VfxAnchor.ForCell(new SurfaceCell(FaceId.Front, 2, 3), topology)));
            builder.Add(CreateRequest(1, 1, GameplayVfxCueId.From(PlayerVfxCue.Damage), VfxAnchor.ForEntity(9)));
            builder.Add(CreateRequest(1, 0, GameplayVfxCueId.From(PlayerVfxCue.Death), VfxAnchor.ForEntity(1)));

            var plan = builder.Build();

            Assert.That(plan.Requests.Select(request => request.CueId).ToArray(), Is.EquivalentTo(new[]
            {
                GameplayVfxCueId.From(PlayerVfxCue.Death),
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
                GameplayVfxCueId.From(BoxVfxCue.DestroySmoke),
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
            }));
        }

        [Test]
        [Category("Extended")]
        public void PersistentKey_EqualityAndOrdering_AreDeterministic()
        {
            var cue = GameplayVfxCueId.From(TileFeatureVfxCue.HazardPulse);
            var cell = new SurfaceCell(FaceId.Front, 1, 2);
            var first = new VfxPersistentKey(cue, VfxAnchorKind.Cell, cell: cell, hasCell: true, effectIndex: 1, activationSequence: 10);
            var same = new VfxPersistentKey(cue, VfxAnchorKind.Cell, cell: cell, hasCell: true, effectIndex: 1, activationSequence: 10);
            var laterActivation = new VfxPersistentKey(cue, VfxAnchorKind.Cell, cell: cell, hasCell: true, effectIndex: 1, activationSequence: 11);

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(laterActivation));
            Assert.That(first.CompareTo(laterActivation), Is.LessThan(0));
        }

        [Test]
        [Category("Extended")]
        public void Request_DoesNotOwnExecutionPolicy()
        {
            var publicMemberNames = typeof(GameplayVfxRequest)
                .GetMembers()
                .Where(member => member.DeclaringType == typeof(GameplayVfxRequest))
                .Select(member => member.Name)
                .ToArray();

            Assert.That(publicMemberNames, Does.Not.Contain("MissingAnchorPolicy"));
            Assert.That(publicMemberNames, Does.Not.Contain("PlaybackMode"));
            Assert.That(publicMemberNames, Does.Not.Contain("StopPolicy"));
            Assert.That(publicMemberNames, Does.Not.Contain("TailSeconds"));
            Assert.That(publicMemberNames, Does.Not.Contain("MaxConcurrentInstances"));
            Assert.That(publicMemberNames, Does.Not.Contain("Requirement"));
        }

        [Test]
        [Category("Extended")]
        public void Anchor_PreservesSurfaceCellFace()
        {
            var floor = VfxAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 4, 7),
                new CubeTopologyState(FaceId.Floor));
            var front = VfxAnchor.ForCell(
                new SurfaceCell(FaceId.Front, 4, 7),
                new CubeTopologyState(FaceId.Floor));

            Assert.That(floor.HasCell, Is.True);
            Assert.That(front.HasCell, Is.True);
            Assert.That(floor, Is.Not.EqualTo(front));
            Assert.That(floor.Cell.PlanarPosition, Is.EqualTo(front.Cell.PlanarPosition));
            Assert.That(floor.Cell.face, Is.Not.EqualTo(front.Cell.face));
        }

        [Test]
        [Category("Extended")]
        public void FamilyPlannerSkeletons_AreNoOpAndExposeFamilyMetadata()
        {
            IGameplayVfxFamilyRequestPlanner[] planners =
            {
                new TerrainVfxRequestPlanner(),
                new ObjectiveStageVfxRequestPlanner(),
            };
            var builder = new GameplayVfxRequestPlanBuilder();
            var context = new GameplayVfxPlanningContext(12);

            foreach (var planner in planners)
            {
                planner.Plan(context, builder);
            }

            Assert.That(planners.Select(planner => planner.Family).ToArray(), Is.EqualTo(new[]
            {
                GameplayVfxFamily.Terrain,
                GameplayVfxFamily.ObjectiveStage,
            }));
            Assert.That(builder.Build(), Is.SameAs(GameplayVfxRequestPlan.Empty));
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_MapsEventsToCuesAndCellAnchors()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var events = new[]
            {
                new TilePresentationEvent(TilePresentationEventKind.ButtonActivated, 1, cell, TileFeatureKind.Button, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.DestroyTileTriggered, 2, cell, TileFeatureKind.Destroy, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.SlideTileRedirected, 3, cell, TileFeatureKind.Slide, 10, 0, 1, direction: Direction.Up),
                new TilePresentationEvent(TilePresentationEventKind.SlideTileRedirected, 4, cell, TileFeatureKind.Slide, 10, 0, 1, direction: Direction.Right),
                new TilePresentationEvent(TilePresentationEventKind.SlideTileRedirected, 5, cell, TileFeatureKind.Slide, 10, 0, 1, direction: Direction.Down),
                new TilePresentationEvent(TilePresentationEventKind.SlideTileRedirected, 6, cell, TileFeatureKind.Slide, 10, 0, 1, direction: Direction.Left),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeBlocked, 7, cell, TileFeatureKind.Barricade, 10, 0, 1, direction: Direction.Up),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeBlocked, 8, cell, TileFeatureKind.Barricade, 10, 0, 1, direction: Direction.Right),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeBlocked, 9, cell, TileFeatureKind.Barricade, 10, 0, 1, direction: Direction.Down),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeBlocked, 10, cell, TileFeatureKind.Barricade, 10, 0, 1, direction: Direction.Left),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeCrushed, 11, cell, TileFeatureKind.Barricade, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.ExitOpened, 12, cell, TileFeatureKind.Exit, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.ExitObjectiveCleared, 13, cell, TileFeatureKind.Exit, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.ExitEntered, 14, cell, TileFeatureKind.Exit, 10, 0, 1, targetEntityId: 20),
                new TilePresentationEvent(TilePresentationEventKind.MoonBlockGenerated, 15, cell, TileFeatureKind.MoonBlockGenerator, 10, 0, 1),
            };

            var plan = PlanTileFeature(topology, events);

            Assert.That(plan.Requests.Select(request => request.CueId).ToArray(), Is.EquivalentTo(new[]
            {
                GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActivated),
                GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileTriggered),
                GameplayVfxCueId.From(TileFeatureVfxCue.SlideTileRedirectedUp),
                GameplayVfxCueId.From(TileFeatureVfxCue.SlideTileRedirectedRight),
                GameplayVfxCueId.From(TileFeatureVfxCue.SlideTileRedirectedDown),
                GameplayVfxCueId.From(TileFeatureVfxCue.SlideTileRedirectedLeft),
                GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeBlockedUp),
                GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeBlockedRight),
                GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeBlockedDown),
                GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeBlockedLeft),
                GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeCrushed),
                GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpened),
                GameplayVfxCueId.From(TileFeatureVfxCue.ExitObjectiveCleared),
                GameplayVfxCueId.From(TileFeatureVfxCue.ExitEntered),
                GameplayVfxCueId.From(TileFeatureVfxCue.MoonBlockGenerated),
            }));
            Assert.That(plan.Requests.All(request => request.Anchor.Kind == VfxAnchorKind.Cell), Is.True);
            Assert.That(plan.Requests.All(request => request.Anchor.Cell.Equals(cell)), Is.True);
            Assert.That(plan.Requests.Select(request => request.SequenceId).ToArray(),
                Is.EqualTo(PlanTileFeature(topology, events).Requests.Select(request => request.SequenceId).ToArray()));
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_IgnoresDirectionlessAndAnimatorOnlyEvents()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);

            var plan = PlanTileFeature(
                topology,
                new TilePresentationEvent(TilePresentationEventKind.SlideTileRedirected, 1, cell, TileFeatureKind.Slide, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeBlocked, 2, cell, TileFeatureKind.Barricade, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeActivated, 3, cell, TileFeatureKind.Barricade, 10, 0, 1),
                new TilePresentationEvent(TilePresentationEventKind.BarricadeDeactivated, 4, cell, TileFeatureKind.Barricade, 10, 0, 1));

            Assert.That(plan.Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void TileFeaturePlanner_MapsDestroyActiveStateToPersistentLaserCue()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var activeVisualState = new TileFeatureActiveVisualState(
                100,
                cell,
                TileFeatureKind.Destroy,
                sourceEntityId: 10,
                ownerEntityId: 20,
                teamId: 1);
            var inactiveKindState = new TileFeatureActiveVisualState(
                101,
                cell,
                TileFeatureKind.Barricade,
                sourceEntityId: 11,
                ownerEntityId: 21,
                teamId: 1);
            var data = CreatePresentationData(tileFeatureActiveVisualStates: new[]
            {
                activeVisualState,
                inactiveKindState,
            });
            var builder = new GameplayVfxRequestPlanBuilder();

            new TileFeatureVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(21, data, topology),
                builder);
            var request = builder.Build().Requests.Single();
            var expectedCueId = GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive);
            var expectedKey = new VfxPersistentKey(
                expectedCueId,
                VfxAnchorKind.Cell,
                tileId: 100,
                cell: cell,
                hasCell: true);

            Assert.That(request.CueId, Is.EqualTo(expectedCueId));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.PersistentKey, Is.EqualTo(expectedKey));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Cell, Is.EqualTo(cell));
            Assert.That(request.SourceEntityId, Is.EqualTo(10));

            var secondBuilder = new GameplayVfxRequestPlanBuilder();
            new TileFeatureVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(22, data, topology),
                secondBuilder);

            Assert.That(secondBuilder.Build().Requests.Single().PersistentKey, Is.EqualTo(request.PersistentKey));
        }

        [Test]
        [Category("Extended")]
        public void GravityFieldPlanner_MapsEventsAndPersistentState()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 4, 5);
            var data = CreatePresentationData(
                gravityFieldEvents: new[]
                {
                    new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Activated, 40, cell),
                    new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Expired, 40, cell),
                },
                gravityFieldVisualStates: new[]
                {
                    new GravityFieldVisualState(40, cell, GravityFieldPhase.Charging, 1, 3, 0.33f),
                    new GravityFieldVisualState(41, cell, GravityFieldPhase.Active, 2, 3, 0.66f, lockedTargetEntityIds: new[] { 10, 11 }),
                });
            var builder = new GameplayVfxRequestPlanBuilder();

            new GravityFieldVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(21, data, topology),
                builder);
            var plan = builder.Build();

            Assert.That(plan.Requests.Select(request => request.CueId).ToArray(), Is.EquivalentTo(new[]
            {
                GameplayVfxCueId.From(GravityFieldVfxCue.ActiveStarted),
                GameplayVfxCueId.From(GravityFieldVfxCue.ChargeStarted),
                GameplayVfxCueId.From(GravityFieldVfxCue.ChargingArea),
                GameplayVfxCueId.From(GravityFieldVfxCue.ActiveArea),
                GameplayVfxCueId.From(GravityFieldVfxCue.LockedTarget),
                GameplayVfxCueId.From(GravityFieldVfxCue.LockedTarget),
            }));
            Assert.That(plan.Requests.Count(request => request.IsPersistent), Is.EqualTo(4));
            Assert.That(plan.Requests.Single(request => request.CueId == GameplayVfxCueId.From(GravityFieldVfxCue.ChargingArea)).Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(plan.Requests.Single(request => request.CueId == GameplayVfxCueId.From(GravityFieldVfxCue.ActiveArea)).PersistentKey.EntityId, Is.EqualTo(41));
            Assert.That(plan.Requests.Where(request => request.CueId == GameplayVfxCueId.From(GravityFieldVfxCue.LockedTarget)).All(request => request.Anchor.Kind == VfxAnchorKind.Entity), Is.True);
            Assert.That(plan.Requests.Where(request => request.CueId == GameplayVfxCueId.From(GravityFieldVfxCue.LockedTarget)).Select(request => request.Anchor.EntityId).ToArray(), Is.EquivalentTo(new[] { 10, 11 }));
        }

        private static GameplayVfxRequest CreateRequest(
            int tick,
            int sequence,
            GameplayVfxCueId cueId,
            VfxAnchor anchor)
        {
            return new GameplayVfxRequest(
                tick,
                sequence,
                presentationSeed: sequence * 17,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);
        }

        private static GameplayVfxRequestPlan PlanTileFeature(
            CubeTopologyState topology,
            params TilePresentationEvent[] events)
        {
            var builder = new GameplayVfxRequestPlanBuilder();
            new TileFeatureVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(21, CreatePresentationData(tileEvents: events), topology),
                builder);
            return builder.Build();
        }

        private static TickPresentationData CreatePresentationData(
            TilePresentationEvent[] tileEvents = null,
            GravityFieldPresentationEvent[] gravityFieldEvents = null,
            GravityFieldVisualState[] gravityFieldVisualStates = null,
            TileFeatureActiveVisualState[] tileFeatureActiveVisualStates = null)
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
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents,
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates,
                tileFeatureActiveVisualStates: tileFeatureActiveVisualStates);
        }
    }
}
