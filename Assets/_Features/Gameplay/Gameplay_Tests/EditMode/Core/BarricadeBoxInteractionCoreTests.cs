using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class BarricadeBoxInteractionCoreTests
    {
        private static readonly BoardBounds TestBounds = new(
            Vector2Int.zero,
            new Vector2Int(4, 4));

        [Test]
        [Category("Core")]
        public void Flip_EnemyOnSuppressedBarricade_WhenEnemySurvives_DestroysSelf()
        {
            var run = RunFlipBlockedByEnemyOnBarricadeScenario(enemyHp: 2);
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            var disposition = run.Result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.Flip));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.DestroySelf));
            Assert.That(disposition.AllTargetsDestroyed, Is.False);
            Assert.That(run.Result.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated").And.Contains("At=Front(2,1)"));
            Assert.That(run.Result.PresentationData.FlipImpactSignals.Single().Disposition, Is.EqualTo(FlipImpactPresentationDisposition.DestroySelf));
            Assert.That(run.Result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(run.Result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(run.FinalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(run.FinalSnapshot.TryGetEntity(30, out var enemyAfter), Is.True);
            Assert.That(enemyAfter.hp, Is.EqualTo(1));
            Assert.That(run.FinalSnapshot.TryGetSolidSemanticAt(new SurfaceCell(FaceId.Front, 0, 1), out _), Is.False);
            Assert.That(run.FinalSnapshot.TryGetSolidSemanticAt(landingCell, out _), Is.False);
            Assert.That(run.FinalSnapshot.HasAnyUnitAt(landingCell), Is.True);
        }

        [Test]
        [Category("Core")]
        public void Flip_EnemyOnSuppressedBarricade_WhenEnemyDies_ReassertCrushesBox()
        {
            var run = RunFlipBlockedByEnemyOnBarricadeScenario(enemyHp: 1);
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            var disposition = run.Result.MovementPhaseResult.ImpactDispositionRecords
                .Single(record => record.ImpactSourceEntityId == 20);

            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.Flip));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.BarricadeReassertCrush));
            Assert.That(disposition.BarricadeTileId, Is.EqualTo(100));
            Assert.That(disposition.BarricadeCell, Is.EqualTo(landingCell));
            Assert.That(disposition.FollowThroughAccepted, Is.False);
            AssertBoxReassertCrushRemovalOps(run.Result.MovementPhaseResult.ResolvedOperations, boxEntityId: 20, landingCell);
            Assert.That(run.Result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(run.Result.PresentationData.TileEvents.Single(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed).TargetEntityId, Is.EqualTo(20));
            Assert.That(run.Result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(run.FinalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(run.FinalSnapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(run.FinalSnapshot.HasAnyUnitAt(landingCell), Is.False);
            Assert.That(run.FinalSnapshot.TryGetSolidSemanticAt(new SurfaceCell(FaceId.Front, 0, 1), out _), Is.False);
            Assert.That(run.FinalSnapshot.TryGetSolidSemanticAt(landingCell, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Flip_CrossSurfaceBarricadeCases_AreNotApplicable()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, TestBounds.MaxInclusive.y);
            var targetCell = new SurfaceCell(FaceId.Front, 0, TestBounds.MinInclusive.y);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(10, playerCell),
                    CreateBox(20, targetCell, boxCapabilities: BoxCapabilities.Flip),
                },
                new[] { CreateTileFeature(100, targetCell, TileFeatureKind.Barricade) });
            // Cross-face Flip is rejected by local geometry before any landing policy exists.
            // Keep this Barricade inactive so the test documents seam Flip as NOT_APPLICABLE
            // without mixing in the independent active-Barricade + Box transition crush invariant.
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.BottomFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, TestBounds.MaxInclusive.y + 1), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=FlipCrossesBoundary"));
            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.None.Contains("FlipLandingBlockedByBarricade"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.MovementPhaseResult.ImpactDispositionRecords, Is.Empty);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked), Is.False);
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(targetCell));
        }

        [Test]
        [Category("Core")]
        public void BottomToFrontSlide_IntoFrontBarricade_NoEnemy_BlocksBeforeImpact()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, TestBounds.MaxInclusive.y);
            var barricadeCell = new SurfaceCell(FaceId.Front, 2, TestBounds.MinInclusive.y);
            var slidingBox = CreateBox(
                20,
                sourceCell,
                state: EntityPhaseState.Sliding,
                facing: Direction.Up);
            slidingBox.stateTimer = 0;
            var worldState = CreateWorldState(
                new[] { slidingBox },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(result.MovementPhaseResult.RejectedReasons, Has.Some.Contains("Reason=BoxSlideBlockedByBarricade").And.Contains("MovementKind=SlidingContinuation"));
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("ImpactReservationCreated"));
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.PresentationData.TileEvents.Single(evt => evt.EventKind == TilePresentationEventKind.BarricadeBlocked).Cell, Is.EqualTo(barricadeCell));
            Assert.That(result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.BarricadeCrushed), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var boxAfter), Is.True);
            Assert.That(boxAfter.position, Is.EqualTo(sourceCell));
            Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(sourceCell, out var sourceSolid), Is.True);
            Assert.That(sourceSolid.Entity.entityId, Is.EqualTo(20));
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
        }

        private static PipelineScenarioRun RunFlipBlockedByEnemyOnBarricadeScenario(int enemyHp)
        {
            var landingCell = new SurfaceCell(FaceId.Front, 2, 1);
            var enemy = CreateEnemyUnit(30, landingCell);
            enemy.hp = enemyHp;
            enemy.maxHp = enemyHp;
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Front, 1, 1)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 0, 1), boxCapabilities: BoxCapabilities.Flip),
                    enemy,
                },
                new[] { CreateTileFeature(100, landingCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) },
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 1), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(7));
            return new PipelineScenarioRun(result, worldState.CreateSnapshot());
        }

        private static void AssertBoxReassertCrushRemovalOps(
            IReadOnlyList<FinalizationOperation> operations,
            int boxEntityId,
            SurfaceCell barricadeCell)
        {
            var boxOperations = operations
                .Where(operation => operation.EntityId == boxEntityId)
                .ToArray();

            Assert.That(
                boxOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                                 operation.BoardPresence == EntityBoardPresence.Detached &&
                                 operation.Metadata.BoundaryReason == "BarricadeCrush" &&
                                 operation.Metadata.PresentationTargetCell == barricadeCell),
                Is.True);
            Assert.That(
                boxOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.MarkDestroy &&
                                 operation.Metadata.BoundaryReason == "BarricadeCrush" &&
                                 operation.Metadata.PresentationTargetCell == barricadeCell),
                Is.True);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            IReadOnlyList<IEntityLogic> entityLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new TickPipeline(
                worldState,
                entityLogics,
                GameplayEntityLogicProviderFactory.CreateDefault(),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                objectiveDefinition: null,
                enemySpawnDefaultsByArchetypeId: null,
                runtimeFeatureFlags: default,
                unitKinematicLocomotionTiming: default,
                playerContinuousLocomotion: default,
                tileFeatureDefinitions: tileFeatureDefinitions,
                tileEffectResolver: null);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                TestBounds,
                new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateDefinition(
            int tileId,
            TileFeatureActivationRule activationRule,
            Direction2D direction = Direction2D.None,
            TileFeatureBoxSelector selector = TileFeatureBoxSelector.AnyPushableBox)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                direction,
                selector,
                boundEntityId: 0);
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position)
        {
            var unit = CreateUnit(entityId, position);
            unit.unitRole = UnitRole.Player;
            return unit;
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position)
        {
            var unit = CreateUnit(entityId, position);
            unit.teamId = 2;
            unit.unitRole = UnitRole.Enemy;
            return unit;
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities boxCapabilities = BoxCapabilities.Push,
            EntityPhaseState state = EntityPhaseState.Idle,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = state,
                facing = facing,
                boxCapabilities = boxCapabilities,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private readonly struct PipelineScenarioRun
        {
            public PipelineScenarioRun(TickResult result, WorldSnapshot finalSnapshot)
            {
                Result = result;
                FinalSnapshot = finalSnapshot;
            }

            public TickResult Result { get; }

            public WorldSnapshot FinalSnapshot { get; }
        }

        private sealed class ScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent _movementIntent;

            public ScriptedMovementLogic(RawMovementIntent movementIntent)
            {
                _movementIntent = movementIntent;
            }

            public int ControlledEntityId => _movementIntent.SourceId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                buffer.Add(_movementIntent);
            }
        }
    }
}
