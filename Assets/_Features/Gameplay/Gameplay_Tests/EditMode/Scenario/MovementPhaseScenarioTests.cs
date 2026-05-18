using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Resolution;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class MovementPhaseScenarioTests
    {
        [Test]
        [Category("Extended")]
        public void Movement_EmptyCellMove_Succeeds()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                });

            var occupancyBefore = DumpUnitOccupancy(CreateSnapshot(worldState));

            var result = pipeline.RunTick(new TickInput(1));

            var occupancyAfter = DumpUnitOccupancy(CreateSnapshot(worldState));

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0)) },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(occupancyBefore, Is.EqualTo("10@(0,0)"));
            Assert.That(occupancyAfter, Is.EqualTo("10@(1,0)"));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Core")]
        public void PlayerMovement_GroundPlayer_CanEnterActiveDestroyTile_AndDies()
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var player = CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0));
            player.unitRole = UnitRole.Player;
            var worldState = CreateWorldState(
                new[]
                {
                    player,
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new[] { CreateDestroyTile(100, destroyCell) });
            var pipeline = CreatePlayerTileFeaturePipeline(
                worldState,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason => reason.Contains("DestroyTile", StringComparison.Ordinal)), Is.False);
            Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("MoveCommitted", StringComparison.Ordinal)), Is.True);
            Assert.That(result.FinalEntities.Any(entity => entity.entityId == 10), Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerMovement_AirPlayer_CanEnterActiveDestroyTile_AndSurvives()
        {
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var player = CreateUnit(
                entityId: 10,
                position: new SurfaceCell(FaceId.Floor, 0, 0),
                unitMobilityKind: UnitMobilityKind.Air);
            player.unitRole = UnitRole.Player;
            var worldState = CreateWorldState(
                new[]
                {
                    player,
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new[] { CreateDestroyTile(100, destroyCell) });
            var pipeline = CreatePlayerTileFeaturePipeline(
                worldState,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason => reason.Contains("DestroyTile", StringComparison.Ordinal)), Is.False);
            Assert.That(result.EventLog.Any(evt => evt.Contains("DestroyTile", StringComparison.Ordinal)), Is.False);
            var finalPlayer = result.FinalEntities.Single(entity => entity.entityId == 10);
            Assert.That(finalPlayer.position, Is.EqualTo(destroyCell));
            Assert.That(finalPlayer.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
        }

        [Test]
        [Category("Extended")]
        public void Movement_ScriptedMoveIntoUnit_SucceedsAndStacks()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                });

            var occupancyBefore = DumpUnitOccupancy(CreateSnapshot(worldState));
            var result = pipeline.RunTick(new TickInput(1));
            var occupancyAfter = DumpUnitOccupancy(CreateSnapshot(worldState));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(occupancyBefore, Is.EqualTo("10@(0,0),20@(1,0)"));
            Assert.That(occupancyAfter, Is.EqualTo("10@(1,0),20@(1,0)"));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                GetUnitIdsAt(worldState, new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_TwoScriptedUnitsEnteringSameDestinationInSameTick_BothSucceedAndStack()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(2, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 10, new Vector2Int(1, 0))),
                    new StubMovementLogic(new RawMovementIntent(20, 5, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=20",
                    "To=(1,0)",
                    "Facing=Left"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                GetUnitIdsAt(worldState, new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_MoveIntoPushBox_IsNoOpWithoutExplicitPush()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
            });
            var timingProfile = CreateTimingProfile();
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_MoveIntoUnit_SucceedsWithoutStartingPushAction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), teamId: 2),
            });
            var timingProfile = CreateTimingProfile();
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(
                        new Dictionary<int, RawMovementIntent>
                        {
                            { 1, new RawMovementIntent(10, priority: 100, destination: new Vector2Int(1, 0)) },
                        }),
                },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                GetUnitIdsAt(worldState, new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_EnemyMoveIntoPlayerCell_SucceedsAndStacks()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), teamId: 2, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(20, 5, new Vector2Int(0, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=20",
                    "To=(0,0)",
                    "Facing=Left"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(0, 0)));
            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                GetUnitIdsAt(worldState, new SurfaceCell(FaceId.Floor, 0, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputPushBox_StopsBeforeEntityBlocker_AndEntityTypeNoneWallRemainsValid()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push, facing: Direction.Left),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Push),
                },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination, intent.CommandKind))
                    .ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(GetEntityFacing(worldState, 10), Is.EqualTo(Direction.Up));
            Assert.That(GetEntityFacing(worldState, 30), Is.EqualTo(Direction.Right));
            var snapshotAfter = CreateSnapshot(worldState);
            Assert.That(snapshotAfter.TryGetEntity(30, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(11));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickEntityMotionKind.BoxSlide, Source: new SurfaceCell(FaceId.Floor, 1, 0), Destination: new SurfaceCell(FaceId.Floor, 2, 0)),
                },
                result.PresentationData
                    .EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == 30 &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.BoxActionMovement),
                Is.True);
            Assert.That(result.Trace.Text, Does.Contain("Boundary=BoxActionMovement"));
        }

        [Test]
        [Category("Core")]
        public void MovementPhase_BoxPush_RemainsGridTransaction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push, facing: Direction.Left),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(
                result,
                30,
                MovementExecutionBoundaryKind.BoxActionMovement);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMoveOperationOrDiagnostic(result, 10);
            Assert.That(
                result.PresentationData.EntityMotions.Any(
                    motion => motion.EntityId == 30 &&
                              motion.MotionKind == TickEntityMotionKind.BoxSlide &&
                              motion.SourceCell == new SurfaceCell(FaceId.Floor, 1, 0) &&
                              motion.DestinationCell == new SurfaceCell(FaceId.Floor, 2, 0)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void MovementPhase_Flip_RemainsGridTransaction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(
                result,
                30,
                MovementExecutionBoundaryKind.BoxActionMovement);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMoveOperationOrDiagnostic(result, 10);
            Assert.That(
                result.PresentationData.EntityMotions.Any(
                    motion => motion.EntityId == 30 &&
                              motion.MotionKind == TickEntityMotionKind.Flip &&
                              motion.SourceCell == new SurfaceCell(FaceId.Floor, -1, 0) &&
                              motion.DestinationCell == new SurfaceCell(FaceId.Floor, 1, 0)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void MovementPhase_Item_RemainsGridTransaction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                CreateTimingProfile(),
                CreateDefaultPlayerControlTimingSnapshot(CreateTimingProfile()),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(
                result,
                10,
                MovementExecutionBoundaryKind.BoxActionMovement);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMoveOperationOrDiagnostic(result, 10);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(
                SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog),
                Is.EqualTo(new[] { 20 }));
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_BoxActionMovement_RetainsRequiredMovePresentation()
        {
            MovementPhase_BoxPush_RemainsGridTransaction();
            MovementPhase_Flip_RemainsGridTransaction();
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_ItemOrGridMovement_Retained()
        {
            MovementPhase_Item_RemainsGridTransaction();
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_GridTransactions_Retained()
        {
            MovementPhase_BoxPush_RemainsGridTransaction();
            MovementPhase_Flip_RemainsGridTransaction();
            MovementPhase_Item_RemainsGridTransaction();
            DeprecationPhase1_MovementExpanderGridBranchStillAllowed();
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_ValidateLegacyExpansionIntents_AllowsGridTransactions()
        {
            var pushWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
            });
            var pushIntent = new PushIntent(10, priority: 100, destination: new Vector2Int(1, 0), localSequence: 0);
            pushIntent.AssignIntentId(1);

            var flipWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
            });
            var flipIntent = new FlipIntent(10, priority: 100, destination: new Vector2Int(-1, 0), localSequence: 0);
            flipIntent.AssignIntentId(1);

            var itemWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Item),
            });
            itemWorldState.CreateWriteContext().SetPlayerControlState(10, default);
            var itemIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            itemIntent.AssignIntentId(1);

            var topologyWorldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            topologyWorldState.CreateWriteContext().SetPlayerControlState(10, default);
            var topologyIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(0, 2));
            topologyIntent.AssignIntentId(1);

            AssertLegacyExpansionIntentAllowed(pushWorldState, pushIntent, GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
            AssertLegacyExpansionIntentAllowed(flipWorldState, flipIntent, GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
            AssertLegacyExpansionIntentAllowed(itemWorldState, itemIntent, GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
            AssertLegacyExpansionIntentAllowed(topologyWorldState, topologyIntent, GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_ValidateLegacyExpansionIntents_BlocksFlagOnUnitOrdinaryMove()
        {
            var playerWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            playerWorldState.CreateWriteContext().SetPlayerControlState(10, default);
            var playerIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            playerIntent.AssignIntentId(1);

            AssertLegacyExpansionIntentBlocked(
                playerWorldState,
                playerIntent,
                GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled,
                "PlayerCoveredLocomotionReachedLegacyExpansion");

            var enemyWorldState = CreateWorldState(new[]
            {
                CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var enemyIntent = new MoveIntent(40, priority: 100, destination: new Vector2Int(1, 0));
            enemyIntent.AssignIntentId(1);

            AssertLegacyExpansionIntentBlocked(
                enemyWorldState,
                enemyIntent,
                GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled,
                "EnemyCoveredOrdinaryKinematicReachedLegacyExpansion");

            var chargeEnemy = CreateFrontFaceEnemy(entityId: 50, position: new SurfaceCell(FaceId.Floor, 0, 0));
            chargeEnemy.aiMode = EnemyAiMode.Charge;
            var chargeWorldState = CreateWorldState(new[] { chargeEnemy });
            chargeWorldState.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            var chargeIntent = new MoveIntent(50, priority: 100, destination: new Vector2Int(1, 0));
            chargeIntent.AssignIntentId(1);

            AssertLegacyExpansionIntentBlocked(
                chargeWorldState,
                chargeIntent,
                GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled,
                "ChargeCoveredKinematicReachedLegacyExpansion");
        }

        [Test]
        [Category("Extended")]
        public void Phase2_PlayerLegacyFallback_ValidateLegacyExpansionIntents_PlayerFlagReachability()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);

            var free2DIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            free2DIntent.AssignIntentId(1);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                free2DIntent,
                GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled,
                "PlayerCoveredLocomotionReachedLegacyExpansion");

            var kinematicIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            kinematicIntent.AssignIntentId(2);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                kinematicIntent,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled,
                "PlayerCoveredLocomotionReachedLegacyExpansion");

            var flagOffIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            flagOffIntent.AssignIntentId(3);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                flagOffIntent,
                GameplayRuntimeFeatureFlags.None,
                LegacyMovementBoundaryAssert.ExplicitLegacyFallbackRequiredReason);

            var legacyBaselineIntent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            legacyBaselineIntent.AssignIntentId(4);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                legacyBaselineIntent,
                GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline,
                LegacyMovementBoundaryAssert.PlayerLegacyFallbackRemovedReason);
        }

        [Test]
        [Category("Extended")]
        public void Phase4_None_PlayerFallbackStillBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);

            var intent = new MoveIntent(10, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                intent,
                GameplayRuntimeFeatureFlags.None,
                LegacyMovementBoundaryAssert.ExplicitLegacyFallbackRequiredReason);
        }

        [Test]
        [Category("Extended")]
        public void Phase2B_EnemyLegacyFallback_ValidateLegacyExpansionIntents_EnemyFlagReachability()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });

            var kinematicIntent = new MoveIntent(40, priority: 100, destination: new Vector2Int(1, 0));
            kinematicIntent.AssignIntentId(1);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                kinematicIntent,
                GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled,
                "EnemyCoveredOrdinaryKinematicReachedLegacyExpansion");

            var flagOffIntent = new MoveIntent(40, priority: 100, destination: new Vector2Int(1, 0));
            flagOffIntent.AssignIntentId(2);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                flagOffIntent,
                GameplayRuntimeFeatureFlags.None,
                LegacyMovementBoundaryAssert.ExplicitLegacyFallbackRequiredReason);

            var legacyBaselineIntent = new MoveIntent(40, priority: 100, destination: new Vector2Int(1, 0));
            legacyBaselineIntent.AssignIntentId(3);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                legacyBaselineIntent,
                GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline,
                LegacyMovementBoundaryAssert.EnemyLegacyFallbackRemovedReason);
        }

        [Test]
        [Category("Extended")]
        public void Phase5_None_EnemyFallbackStillBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var intent = new MoveIntent(40, priority: 100, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);

            AssertLegacyExpansionIntentBlocked(
                worldState,
                intent,
                GameplayRuntimeFeatureFlags.None,
                LegacyMovementBoundaryAssert.ExplicitLegacyFallbackRequiredReason);
        }

        [Test]
        [Category("Extended")]
        public void Phase2C_ChargeLegacyFallback_ValidateLegacyExpansionIntents_ChargeFlagReachability()
        {
            var chargeEnemy = CreateFrontFaceEnemy(entityId: 50, position: new SurfaceCell(FaceId.Floor, 0, 0));
            chargeEnemy.aiMode = EnemyAiMode.Charge;
            var worldState = CreateWorldState(new[] { chargeEnemy });
            worldState.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });

            var kinematicIntent = new MoveIntent(50, priority: 100, destination: new Vector2Int(1, 0));
            kinematicIntent.AssignIntentId(1);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                kinematicIntent,
                GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled,
                "ChargeCoveredKinematicReachedLegacyExpansion");

            var flagOffIntent = new MoveIntent(50, priority: 100, destination: new Vector2Int(1, 0));
            flagOffIntent.AssignIntentId(2);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                flagOffIntent,
                GameplayRuntimeFeatureFlags.None,
                LegacyMovementBoundaryAssert.ExplicitLegacyFallbackRequiredReason);

            var legacyBaselineIntent = new MoveIntent(50, priority: 100, destination: new Vector2Int(1, 0));
            legacyBaselineIntent.AssignIntentId(3);
            AssertLegacyExpansionIntentBlocked(
                worldState,
                legacyBaselineIntent,
                GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline,
                LegacyMovementBoundaryAssert.ChargeLegacyFallbackRemovedReason);
        }

        [Test]
        [Category("Extended")]
        public void Phase6_ChargeKinematicFlagOn_NoLegacyChargeMove()
        {
            Phase2C_ChargeLegacyFallback_ValidateLegacyExpansionIntents_ChargeFlagReachability();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveCleanup_ChargeKinematicFlagOn_NoChargeMoveProducer()
        {
            Phase2C_ChargeLegacyFallback_ValidateLegacyExpansionIntents_ChargeFlagReachability();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveDeletion_ChargeKinematicFlagOn_ChargePresentationStillWorks()
        {
            ChargeMoveCleanup_ChargeKinematicFlagOn_NoChargeMoveProducer();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveProducer_ChargeKinematicFlagOn_Unreachable()
        {
            ChargeMoveCleanup_ChargeKinematicFlagOn_NoChargeMoveProducer();
        }

        [Test]
        [Category("Core")]
        public void ChargeMoveIsolation_ChargeKinematicFlagOn_NoChargeMove()
        {
            ChargeMoveProducer_ChargeKinematicFlagOn_Unreachable();
        }

        [Test]
        [Category("Extended")]
        public void Phase6_MovementExpander_GridBranchStillAllowed()
        {
            DeprecationPhase1_MovementExpanderGridBranchStillAllowed();
        }

        [Test]
        [Category("Core")]
        public void Boundary_UnknownInventory_NormalGameplayHasNoUnexpectedUnknownMovement()
        {
            var pushWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
            });
            var pushResult = GameplayCompositionRoot.CreateTickPipeline(
                    pushWorldState,
                    new IEntityLogic[] { CreateImmediatePushPlayerLogic(10) })
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            AssertNoUnexpectedUnknownMovementBoundary(pushResult);

            var flipWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
            });
            var flipResult = GameplayCompositionRoot.CreateTickPipeline(
                    flipWorldState,
                    new IEntityLogic[] { CreateImmediateFlipPlayerLogic(10) })
                .RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            AssertNoUnexpectedUnknownMovementBoundary(flipResult);

            var itemWorldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
            });
            var itemResult = GameplayCompositionRoot.CreateTickPipeline(
                    itemWorldState,
                    new IEntityLogic[] { new PlayerLogic(10) })
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            AssertNoUnexpectedUnknownMovementBoundary(itemResult);

            var topologyWorldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var topologyResult = GameplayCompositionRoot.CreateTickPipeline(
                    topologyWorldState,
                    new IEntityLogic[] { new PlayerLogic(10) })
                .RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            AssertNoUnexpectedUnknownMovementBoundary(topologyResult);
        }

        [Test]
        [Category("Extended")]
        public void MovementExpander_ForbiddenLegacyUnitOrdinaryIntent_DetectsDebug()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            var intent = new MoveIntent(10, priority: 1, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);
            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            new MovementExpander().Expand(
                CreateSnapshot(worldState),
                tickIndex: 1,
                sortedIntents: new[] { intent },
                playerTraversalSourceIds: null,
                frontFaceSupportContributors: null,
                buffer: expandedCandidates,
                rejectedReasons: rejectedReasons,
                frontFaceShieldBlockExports: null,
                forbiddenLegacyUnitOrdinaryIntentIds: new HashSet<int> { intent.IntentId });

            Assert.That(expandedCandidates, Is.Empty);
            Assert.That(
                rejectedReasons.Any(reason =>
                    reason.Contains("LegacyUnitOrdinaryMovementDetected", StringComparison.Ordinal) &&
                    reason.Contains("E=10", StringComparison.Ordinal) &&
                    reason.Contains("ForbiddenCoveredLocomotionReachedMovementExpander", StringComparison.Ordinal)),
                Is.True,
                "MovementExpander forbidden-intent diagnostics are guard coverage only; they do not make grid transactions or MoveEntity deletion candidates.\n" +
                string.Join("\n", rejectedReasons));
        }

        [Test]
        [Category("Extended")]
        public void CoveredLocomotion_ForcedLeak_IsBlocked()
        {
            MovementExpander_ForbiddenLegacyUnitOrdinaryIntent_DetectsDebug();
        }

        [Test]
        [Category("Core")]
        public void DeprecationPhase1_MovementExpanderGridBranchStillAllowed()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Item),
            });
            var intent = new MoveIntent(10, priority: 1, destination: new Vector2Int(1, 0));
            intent.AssignIntentId(1);
            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            new MovementExpander().Expand(
                CreateSnapshot(worldState),
                tickIndex: 1,
                sortedIntents: new[] { intent },
                playerTraversalSourceIds: null,
                frontFaceSupportContributors: null,
                buffer: expandedCandidates,
                rejectedReasons: rejectedReasons);

            Assert.That(expandedCandidates, Is.Not.Empty);
            Assert.That(
                rejectedReasons.Any(reason =>
                    reason.Contains("LegacyUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                Is.False,
                string.Join("\n", rejectedReasons));
        }

        [Test]
        [Category("Core")]
        public void ScopedDeletionPrep_MovementExpander_GridBranchIsRetained()
        {
            DeprecationPhase1_MovementExpanderGridBranchStillAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase4_MovementExpander_GridBranchStillAllowed()
        {
            DeprecationPhase1_MovementExpanderGridBranchStillAllowed();
        }

        [Test]
        [Category("Core")]
        public void Phase5_MovementExpander_GridBranchStillAllowed()
        {
            DeprecationPhase1_MovementExpanderGridBranchStillAllowed();
        }

        [Test]
        [Category("Extended")]
        public void Movement_SlidingPushBox_ContinuesOnLaterTicksUntilBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var secondTick = pipeline.RunTick(new TickInput(13));
            var laterIdleTicks = RunTicks(pipeline, startTickIndex: 14, endTickIndex: 24);
            var thirdTick = pipeline.RunTick(new TickInput(25));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 30, IntentId: 1, Destination: new Vector2Int(3, 0), Command: MovementCommandKind.Move),
                },
                secondTick.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination, intent.CommandKind))
                    .ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    secondTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    secondTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(3,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                secondTick.PresentationData.EntityMotions.Any(
                    motion => motion.EntityId == 30 &&
                              motion.MotionKind == TickEntityMotionKind.BoxSlide &&
                              motion.SourceCell == new SurfaceCell(FaceId.Floor, 2, 0) &&
                              motion.DestinationCell == new SurfaceCell(FaceId.Floor, 3, 0)),
                Is.True);
            Assert.That(laterIdleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    thirdTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Idle",
                    "Timer=0"),
                Is.True);
            Assert.That(thirdTick.PresentationData.BoxSlideStopSignals, Has.Count.EqualTo(1));
            var stopSignal = thirdTick.PresentationData.BoxSlideStopSignals[0];
            Assert.That(stopSignal.BoxEntityId, Is.EqualTo(30));
            Assert.That(stopSignal.StopperEntityId, Is.EqualTo(90));
            Assert.That(stopSignal.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 0)));
            Assert.That(stopSignal.StopperCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
            Assert.That(stopSignal.SlideDirection, Is.EqualTo(Direction.Right));
            Assert.That(stopSignal.StopperKind, Is.EqualTo(BoxSlideStopperKind.SolidEntity));
            Assert.That(stopSignal.Cause, Is.EqualTo(BoxSlideStopCause.SlidingContinuationBlocked));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(snapshotAfter.TryGetEntity(30, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void Movement_SlidingPushBox_StoppedByTerrain_DoesNotEmitBoxSlideStopSignal()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            pipeline.RunTick(new TickInput(13));
            RunTicks(pipeline, startTickIndex: 14, endTickIndex: 24);
            var stopTick = pipeline.RunTick(new TickInput(25));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(stopTick.PresentationData.BoxSlideStopSignals, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    stopTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Idle",
                    "Timer=0"),
                Is.True);
            Assert.That(snapshotAfter.TryGetEntity(30, out var pushedBox), Is.True);
            Assert.That(pushedBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 0)));
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Movement_CrossFaceSlidingPushBox_StoppedBySolidStillEmitsBoxSlideStopSignal()
        {
            var slidingBox = CreateBox(
                entityId: 30,
                position: new SurfaceCell(FaceId.Floor, 0, 1),
                capabilities: BoxCapabilities.Push,
                facing: Direction.Up);
            slidingBox.state = EntityPhaseState.Sliding;
            slidingBox.stateTimer = 0;
            var worldState = CreateWorldState(
                new[]
                {
                    slidingBox,
                    CreateNonUnitBlocker(entityId: 90, position: new SurfaceCell(FaceId.Front, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>());

            var stopTick = pipeline.RunTick(new TickInput(1));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(stopTick.PresentationData.BoxSlideStopSignals, Has.Count.EqualTo(1));
            var stopSignal = stopTick.PresentationData.BoxSlideStopSignals[0];
            Assert.That(stopSignal.BoxEntityId, Is.EqualTo(30));
            Assert.That(stopSignal.StopperEntityId, Is.EqualTo(90));
            Assert.That(stopSignal.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(stopSignal.StopperCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(stopSignal.SourceCell.face, Is.Not.EqualTo(stopSignal.StopperCell.face));
            Assert.That(stopSignal.SlideDirection, Is.EqualTo(Direction.Up));
            Assert.That(stopSignal.StopperKind, Is.EqualTo(BoxSlideStopperKind.SolidEntity));
            Assert.That(stopSignal.Cause, Is.EqualTo(BoxSlideStopCause.SlidingContinuationBlocked));
            Assert.That(snapshotAfter.TryGetEntity(30, out var stoppedBox), Is.True);
            Assert.That(stoppedBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(stoppedBox.state, Is.EqualTo(EntityPhaseState.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Movement_BoxSlideInterval_At60Tps_PreservesRealTimeCadence()
        {
            var timingProfile = CreateTimingProfile(
                simulationTicksPerSecond: 60,
                boxSlideStepIntervalSeconds: 0.1f);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile));

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 6);
            var slideTick = pipeline.RunTick(new TickInput(7));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=6"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    slideTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=6"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    slideTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(3,0)",
                    "Facing=Right"),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Movement_BoxSlideInterval_At120Tps_PreservesRealTimeCadence()
        {
            var timingProfile = CreateTimingProfile(
                simulationTicksPerSecond: 120,
                boxSlideStepIntervalSeconds: 0.1f);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile));

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var slideTick = pipeline.RunTick(new TickInput(13));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    slideTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    slideTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(3,0)",
                    "Facing=Right"),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputPushBox_StartsSlidingBeforeTerrainBlocker()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputPushBox_StartsSlidingBeforeBoardEdge()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputPushBox_IgnoresProjectileAsSlideStopper()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                    CreateProjectile(entityId: 40, position: new Vector2Int(2, 0), hp: 1),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(finalSnapshot.TryGetProjectileAt(new Vector2Int(2, 0), out var projectile), Is.True);
            Assert.That(projectile.entityId, Is.EqualTo(40));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputPushBox_StartsSlidingWhenBoundedLaneHasNoStopper()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=20",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=20",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            CollectionAssert.AreEqual(
                System.Array.Empty<string>(),
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        [Category("Core")]
        public void Push_BoxNextStepHasHostileUnit_CreatesImpactAndStopsBeforeUnitCell()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 30, position: new Vector2Int(2, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(evt => evt.StartsWith("ImpactReservationCreated|", StringComparison.Ordinal)),
                Is.True);
            Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("MoveCommitted")), Is.False);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, TargetId: 30, Position: new SurfaceCell(FaceId.Floor, 2, 0), Damage: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage))
                    .ToArray());
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(box.kineticInstigatorEntityId, Is.EqualTo(10));
            Assert.That(box.kineticInstigatorTeamId, Is.EqualTo(1));
            Assert.That(snapshotAfter.TryGetEntity(30, out var enemy), Is.True);
            Assert.That(enemy.hp, Is.EqualTo(2));
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void Impact_KillsEnemy_BoxAdvancesSameTickWhenTargetCellBecomesEmpty()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 30, position: new Vector2Int(2, 0), hp: 1, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);
            var resolvedOperations = result.MovementPhaseResult.ResolvedOperations;
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords.Single(record => record.ImpactSourceEntityId == 20);

            CollectionAssert.AreEqual(new[] { 30 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=30",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=20",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                result.PresentationData.EntityMotions.Any(
                    motion => motion.EntityId == 20 &&
                              motion.MotionKind == TickEntityMotionKind.BoxSlide &&
                              motion.SourceCell == new SurfaceCell(FaceId.Floor, 1, 0) &&
                              motion.DestinationCell == new SurfaceCell(FaceId.Floor, 2, 0)),
                Is.True);
            Assert.That(
                resolvedOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                                 operation.EntityId == 30 &&
                                 operation.BoardPresence == EntityBoardPresence.Detached &&
                                 operation.Metadata.LocalActionIndex == 1),
                Is.True);
            Assert.That(
                resolvedOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.MoveEntity &&
                                 operation.EntityId == 20 &&
                                 operation.Destination == new SurfaceCell(FaceId.Floor, 2, 0) &&
                                 operation.Metadata.LocalActionIndex == 1),
                Is.True);
            Assert.That(
                resolvedOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.SetFacing &&
                                 operation.EntityId == 20 &&
                                 operation.Facing == Direction.Right &&
                                 operation.Metadata.LocalActionIndex == 1),
                Is.True);
            Assert.That(
                resolvedOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.ApplyStateChange &&
                                 operation.EntityId == 20 &&
                                 operation.PhaseState == EntityPhaseState.Sliding &&
                                 operation.Metadata.LocalActionIndex == 1),
                Is.True);
            Assert.That(
                result.AttackPhaseResult.ResolvedOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.MarkDestroy &&
                                 operation.EntityId == 30),
                Is.True);
            Assert.That(
                result.AttackPhaseResult.ResolvedOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                                 operation.EntityId == 30),
                Is.False);
            Assert.That(
                result.AttackPhaseResult.ResolvedOperations.Any(
                    operation => operation.Kind == FinalizationOperationKind.MoveEntity &&
                                 operation.EntityId == 20),
                Is.False);

            var orderedOperations = resolvedOperations.ToList();
            var detachIndex = orderedOperations.FindIndex(
                operation => operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                             operation.EntityId == 30 &&
                             operation.Metadata.LocalActionIndex == 1);
            var moveIndex = orderedOperations.FindIndex(
                operation => operation.Kind == FinalizationOperationKind.MoveEntity &&
                             operation.EntityId == 20 &&
                             operation.Metadata.LocalActionIndex == 1);
            var boxFacingIndex = orderedOperations.FindIndex(
                operation => operation.Kind == FinalizationOperationKind.SetFacing &&
                             operation.EntityId == 20 &&
                             operation.Metadata.LocalActionIndex == 1);
            var stateIndex = orderedOperations.FindIndex(
                operation => operation.Kind == FinalizationOperationKind.ApplyStateChange &&
                             operation.EntityId == 20 &&
                             operation.Metadata.LocalActionIndex == 1);

            Assert.That(detachIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(moveIndex, Is.GreaterThan(detachIndex));
            Assert.That(boxFacingIndex, Is.GreaterThan(moveIndex));
            Assert.That(stateIndex, Is.GreaterThan(boxFacingIndex));
            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.PushLike));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(disposition.TargetDestroyed, Is.True);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.True);
            Assert.That(disposition.FollowThroughAccepted, Is.True);
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(snapshotAfter.TryGetEntity(30, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Impact_TargetCellHasFriendlyOnly_DoesNotDamageFriendly()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 30, position: new Vector2Int(2, 0), hp: 3, teamId: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshotAfter.TryGetEntity(30, out var friendly), Is.True);
            Assert.That(friendly.hp, Is.EqualTo(3));
        }

        [Test]
        [Category("Extended")]
        public void Impact_TargetCellHasStackedFriendlyAndHostile_DamagesAllUnitTargetsDeterministically()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 30, position: new Vector2Int(2, 0), hp: 3, teamId: 2),
                CreateUnit(entityId: 40, position: new Vector2Int(2, 0), hp: 3, teamId: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, TargetId: 30, Position: new SurfaceCell(FaceId.Floor, 2, 0), Damage: 1),
                    (SourceId: 20, TargetId: 40, Position: new SurfaceCell(FaceId.Floor, 2, 0), Damage: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage))
                    .ToArray());
            Assert.That(snapshotAfter.TryGetEntity(30, out var hostile), Is.True);
            Assert.That(hostile.hp, Is.EqualTo(2));
            Assert.That(snapshotAfter.TryGetEntity(40, out var friendly), Is.True);
            Assert.That(friendly.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void Impact_LethalTargetWithinStackedUnits_BoxDoesNotAdvanceIntoRemainingOccupant()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 30, position: new Vector2Int(2, 0), hp: 1, teamId: 2),
                CreateUnit(entityId: 40, position: new Vector2Int(2, 0), hp: 3, teamId: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, TargetId: 30, Position: new SurfaceCell(FaceId.Floor, 2, 0), Damage: 1),
                    (SourceId: 20, TargetId: 40, Position: new SurfaceCell(FaceId.Floor, 2, 0), Damage: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage))
                    .ToArray());
            CollectionAssert.AreEqual(new[] { 30 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("MoveCommitted")), Is.False);
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshotAfter.TryGetEntity(30, out _), Is.False);
            Assert.That(snapshotAfter.TryGetEntity(40, out var survivingOccupant), Is.True);
            Assert.That(survivingOccupant.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void Movement_ImmediatePushHoldMoveIntoPushBox_FallsBackToBlockedDestinationWhenEntityStopperIsAdjacent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 30, position: new Vector2Int(2, 0), capabilities: BoxCapabilities.Push),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.PresentationData.BoxSlideStopSignals, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=SlideStopperAdjacent",
                    "Target=20",
                    "StopperKind=Entity",
                    "Stopper=30",
                    "StopperType=Box",
                    "Cell=(2,0)"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_ImmediatePushHoldMoveIntoPushBox_FallsBackToBlockedDestinationWhenTerrainStopperIsAdjacent()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                new GameplayTerrainData(new[] { new Vector2Int(2, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.PresentationData.BoxSlideStopSignals, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=SlideStopperAdjacent",
                    "Target=20",
                    "StopperKind=Terrain",
                    "Cell=(2,0)"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_IdleBoxAdjacentToSolid_DoesNotEmitBoxSlideStopSignal()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(2, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>());

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.PresentationData.BoxSlideStopSignals, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_ItemBox_PlayerEntersCellInSameTick_AndCleanupRemovesBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                CreateTimingProfile(),
                CreateDefaultPlayerControlTimingSnapshot(CreateTimingProfile()),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.SortedIntents.Count, Is.EqualTo(1));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=20",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=20",
                    "Condition=AlwaysMark"),
                Is.True);
            CollectionAssert.AreEqual(new[] { 20 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
            });

            var movementOnly = RunMovementPhaseOnly(
                worldState,
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new PlayerLogic(10));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    movementOnly.Result.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=20",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    movementOnly.Result.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    movementOnly.Result.CommitEvents,
                    "DestroyMarked",
                    "Target=20",
                    "Condition=AlwaysMark"),
                Is.True);
            var units = new List<EntityState>();
            Assert.That(movementOnly.SnapshotAfterMovement.TryGetEntity(20, out var itemBox), Is.True);
            Assert.That(itemBox.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            movementOnly.SnapshotAfterMovement.EnumerateUnitsAt(new SurfaceCell(FaceId.Floor, 1, 0), units);
            Assert.That(units.Select(entity => entity.entityId).ToArray(), Is.EqualTo(new[] { 10 }));
            Assert.That(movementOnly.SnapshotAfterMovement.TryPickImpactTargetAt(new SurfaceCell(FaceId.Floor, 1, 0), sourceTeamId: 2, out var occupyingUnit), Is.True);
            Assert.That(occupyingUnit.entityId, Is.EqualTo(10));
            Assert.That(movementOnly.SnapshotAfterMovement.TryPickImpactTargetAt(new SurfaceCell(FaceId.Floor, 1, 0), sourceTeamId: 1, out var sameTeamFallbackTarget), Is.True);
            Assert.That(sameTeamFallbackTarget.entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputOnItemPushFlipBox_ResolvesAsItemBeforePushOrFlip()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    capabilities: BoxCapabilities.Item | BoxCapabilities.Push | BoxCapabilities.Flip),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=20",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=20",
                    "Condition=AlwaysMark"),
                Is.True);
            CollectionAssert.AreEqual(new[] { 20 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("Boundary=BoxActionMovement"));
            Assert.That(result.Trace.Text, Does.Contain("Command=Push"));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputOnItemPushFlipDestroyBox_ResolvesAsItemBeforePushFlipOrDestroy_AndPresentationUsesEntityExitOwnership()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    capabilities: BoxCapabilities.Item | BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=20",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=20",
                    "Condition=AlwaysMark"),
                Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 10, Kind: TickEntityMotionKind.Move, Source: new SurfaceCell(FaceId.Floor, 0, 0), Destination: new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                result.PresentationData
                    .EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
            CollectionAssert.AreEqual(
                Array.Empty<(int EntityId, TickVisibilityChangeKind Kind)>(),
                result.PresentationData
                    .VisibilityChanges
                    .Select(change => (change.EntityId, change.ChangeKind))
                    .ToArray());
            Assert.That(result.PresentationData.EntityExitSignals.Count, Is.EqualTo(1));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitedEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitCause, Is.EqualTo(TickEntityExitCause.ItemConsume));
            CollectionAssert.AreEqual(new[] { 20 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("Boundary=BoxActionMovement"));
            Assert.That(result.Trace.Text, Does.Contain("Command=Push"));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputPushBox_FailsWhenBoxLacksCapability()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.None),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=PushTargetNotPushBox",
                    "Cell=(1,0)",
                    "Target=20",
                    "Capabilities=None"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushInputIntoUnit_FailsBecausePushTargetsOnlyBoxes()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=PushTargetNotBox",
                    "Cell=(1,0)",
                    "Target=20",
                    "Type=Unit"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Core")]
        public void Movement_Flip_SucceedsWhenOppositeCellIsFree()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Destination: new Vector2Int(-1, 0), Command: MovementCommandKind.Flip),
                },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination, intent.CommandKind))
                    .ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "FacingCommitted",
                    "E=10",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityFacing(worldState, 10), Is.EqualTo(Direction.Right));
            Assert.That(GetEntityFacing(worldState, 30), Is.EqualTo(Direction.Right));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickEntityMotionKind.Flip, Source: new SurfaceCell(FaceId.Floor, -1, 0), Destination: new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                result.PresentationData
                    .EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
            Assert.That(result.Trace.Text, Does.Contain("Boundary=BoxActionMovement"));
            Assert.That(result.Trace.Text, Does.Contain("MoveCommitted|G=1|I=1|E=30|To=(1,0)|Facing=Right"));
        }

        [Test]
        [Category("Extended")]
        public void Flip_LandingHasStackedUnits_NonLethalImpact_DamagesAllAndDestroysSelfWithoutCommittedMove()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
                CreateUnit(entityId: 21, position: new Vector2Int(1, 0), hp: 3, teamId: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var snapshotAfter = CreateSnapshot(worldState);
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords.Single(record => record.ImpactSourceEntityId == 30);

            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(evt => evt.StartsWith("ImpactReservationCreated|", StringComparison.Ordinal)),
                Is.True);
            Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("MoveCommitted")), Is.False);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=30",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=30",
                    "Reason=ImpactDestroySelf"),
                Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 30, TargetId: 20, Position: new SurfaceCell(FaceId.Floor, 1, 0), Damage: 1),
                    (SourceId: 30, TargetId: 21, Position: new SurfaceCell(FaceId.Floor, 1, 0), Damage: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage))
                    .ToArray());
            CollectionAssert.AreEqual(new[] { 30 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(result.PresentationData.EntityExitSignals.Select(signal => signal.ExitedEntityId).ToArray(), Is.EqualTo(new[] { 30 }));
            Assert.That(result.PresentationData.ImpactTransientSignals, Is.Empty);
            Assert.That(result.PresentationData.FlipImpactSignals.Count, Is.EqualTo(1));
            var flipImpactSignal = result.PresentationData.FlipImpactSignals[0];
            Assert.That(flipImpactSignal.SourceActionPlanId, Is.GreaterThan(0));
            Assert.That(flipImpactSignal.BoxEntityId, Is.EqualTo(30));
            Assert.That(flipImpactSignal.ImpactTargetEntityId, Is.EqualTo(20));
            Assert.That(flipImpactSignal.ActorEntityId, Is.EqualTo(10));
            Assert.That(flipImpactSignal.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(flipImpactSignal.ImpactCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(flipImpactSignal.HasLandingCell, Is.False);
            Assert.That(flipImpactSignal.Disposition, Is.EqualTo(FlipImpactPresentationDisposition.DestroySelf));
            Assert.That(snapshotAfter.TryGetEntity(30, out _), Is.False);
            Assert.That(snapshotAfter.TryGetEntity(20, out var enemy), Is.True);
            Assert.That(enemy.hp, Is.EqualTo(2));
            Assert.That(snapshotAfter.TryGetEntity(21, out var friendly), Is.True);
            Assert.That(friendly.hp, Is.EqualTo(2));
            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.Flip));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.DestroySelf));
            Assert.That(disposition.TargetDestroyed, Is.False);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.False);
            Assert.That(disposition.FollowThroughAccepted, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Flip_LethalImpact_LandingAccepted_FollowsThroughSameTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 1, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var snapshotAfter = CreateSnapshot(worldState);
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords.Single(record => record.ImpactSourceEntityId == 30);

            CollectionAssert.AreEqual(new[] { 20 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=20",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(1,0)"),
                Is.True);
            Assert.That(
                result.PresentationData.EntityMotions.Any(
                    motion => motion.EntityId == 30 &&
                              motion.MotionKind == TickEntityMotionKind.Flip &&
                              motion.SourceCell == new SurfaceCell(FaceId.Floor, -1, 0) &&
                              motion.DestinationCell == new SurfaceCell(FaceId.Floor, 1, 0)),
                Is.True);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(result.PresentationData.ImpactTransientSignals, Is.Empty);
            Assert.That(snapshotAfter.TryGetEntity(30, out var flippedBox), Is.True);
            Assert.That(flippedBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out _), Is.False);
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(disposition.TargetDestroyed, Is.True);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.True);
            Assert.That(disposition.FollowThroughAccepted, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Flip_AllStackedTargetsDie_ButReservedLandingCell_StaysWithoutDestroySelf()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateUnit(entityId: 12, position: new Vector2Int(1, 1), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 1, teamId: 1),
                CreateUnit(entityId: 21, position: new Vector2Int(1, 0), hp: 1, teamId: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(
                        new RawMovementIntent(
                            sourceId: 12,
                            priority: 200,
                            destination: new Vector2Int(1, 0),
                            MovementCommandKind.Move,
                            localSequence: 0)),
                    new StubMovementLogic(
                        new RawMovementIntent(
                            sourceId: 10,
                            priority: 100,
                            destination: new Vector2Int(-1, 0),
                            MovementCommandKind.Flip,
                            localSequence: 0)),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.None));
            var snapshotAfter = CreateSnapshot(worldState);
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords.Single(record => record.ImpactSourceEntityId == 30);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 30, TargetId: 20, Position: new SurfaceCell(FaceId.Floor, 1, 0), Damage: 1),
                    (SourceId: 30, TargetId: 21, Position: new SurfaceCell(FaceId.Floor, 1, 0), Damage: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage))
                    .ToArray());
            CollectionAssert.AreEqual(new[] { 20, 21 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog).OrderBy(id => id).ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=12",
                    "To=(1,0)"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30"),
                Is.False);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=30",
                    "Reason=ImpactDestroySelf"),
                Is.False);
            Assert.That(result.PresentationData.EntityMotions.Any(motion => motion.EntityId == 30), Is.False);
            Assert.That(result.PresentationData.FlipImpactSignals.Count, Is.EqualTo(1));
            Assert.That(result.PresentationData.FlipImpactSignals[0].Disposition, Is.EqualTo(FlipImpactPresentationDisposition.Stay));
            Assert.That(snapshotAfter.TryGetEntity(30, out var flippedBox), Is.True);
            Assert.That(flippedBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshotAfter.TryGetEntity(21, out _), Is.False);
            Assert.That(snapshotAfter.TryGetEntity(12, out var reservedMover), Is.True);
            Assert.That(reservedMover.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.Flip));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.Stay));
            Assert.That(disposition.TargetDestroyed, Is.True);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.True);
            Assert.That(disposition.FollowThroughAccepted, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Flip_LethalImpact_CooldownJumpTarget_DoesNotThrowTickTrace()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 1, teamId: 2),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                20,
                CreateEnemyJumpState(EnemyJumpPhase.Cooldown, sequence: 41));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });
            TickResult result = null;

            Assert.DoesNotThrow(() => result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left))));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 20 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(result.Trace.Text, Does.Contain("E=20|"));
            Assert.That(
                result.Trace.Text,
                Does.Contain("Presence=Detached|SpatialKind=Anchored|SpatialOccClaim=0|SpatialGameplayVisible=0|SpatialSource=DetachedNonAirborne"));
            Assert.That(snapshotAfter.TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Flip_NonLethalImpact_CooldownJumpTarget_RemainsOccupyingCooldown()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                20,
                CreateEnemyJumpState(EnemyJumpPhase.Cooldown, sequence: 43));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(snapshotAfter.TryGetEntity(20, out var target), Is.True);
            Assert.That(target.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(target.hp, Is.EqualTo(2));
            Assert.That(snapshotAfter.TryGetEnemyJumpState(20, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
            Assert.That(jumpState.sequence, Is.EqualTo(43));
            Assert.That(result.Trace.Text, Does.Contain("E=20|"));
            Assert.That(result.Trace.Text, Does.Contain("Presence=Occupying|SpatialKind=Anchored"));
        }

        [Test]
        [Category("Extended")]
        public void Flip_MixedLethalAndSurvivingStackedImpact_DestroysSelf()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 1, teamId: 2),
                CreateUnit(entityId: 21, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var snapshotAfter = CreateSnapshot(worldState);
            var disposition = result.MovementPhaseResult.ImpactDispositionRecords.Single(record => record.ImpactSourceEntityId == 30);

            CollectionAssert.AreEqual(new[] { 20, 30 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog).OrderBy(id => id).ToArray());
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(result.PresentationData.ImpactTransientSignals, Is.Empty);
            Assert.That(result.PresentationData.FlipImpactSignals.Count, Is.EqualTo(1));
            var flipImpactSignal = result.PresentationData.FlipImpactSignals[0];
            Assert.That(flipImpactSignal.BoxEntityId, Is.EqualTo(30));
            Assert.That(flipImpactSignal.ImpactTargetEntityId, Is.EqualTo(20));
            Assert.That(flipImpactSignal.ActorEntityId, Is.EqualTo(10));
            Assert.That(flipImpactSignal.HasLandingCell, Is.False);
            Assert.That(flipImpactSignal.Disposition, Is.EqualTo(FlipImpactPresentationDisposition.DestroySelf));
            Assert.That(snapshotAfter.TryGetEntity(30, out _), Is.False);
            Assert.That(snapshotAfter.TryGetEntity(21, out var survivingOccupant), Is.True);
            Assert.That(survivingOccupant.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(survivingOccupant.hp, Is.EqualTo(2));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.DestroySelf));
            Assert.That(disposition.TargetDestroyed, Is.False);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.False);
            Assert.That(disposition.FollowThroughAccepted, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Movement_Flip_FailsWhenTargetIsNotFlippableBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.None),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=FlipTargetNotFlippableBox",
                    "Cell=(-1,0)",
                    "Target=20",
                    "Type=Box",
                    "Capabilities=None"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_FlipInputIntoUnit_FailsBecauseFlipTargetsOnlyBoxes()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(-1, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=FlipTargetNotFlippableBox",
                    "Cell=(-1,0)",
                    "Target=20",
                    "Type=Unit",
                    "Capabilities=None"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_FlipInputOnItemFlipDestroyBox_UsesFlipBranch_WithoutConsumeOrDestroy()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(
                    entityId: 30,
                    position: new Vector2Int(-1, 0),
                    capabilities: BoxCapabilities.Item | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                    facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "FacingCommitted",
                    "E=10",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("BoardPresenceCommitted")), Is.False);
            Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("DestroyMarked")), Is.False);
            Assert.That(SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog), Is.Empty);
            Assert.That(result.PresentationData.VisibilityChanges, Is.Empty);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(GetEntityCell(worldState, 30), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshotAfter.TryGetEntity(30, out var flippedBox), Is.True);
            Assert.That(flippedBox.boxCapabilities, Is.EqualTo(BoxCapabilities.Item | BoxCapabilities.Flip | BoxCapabilities.Destroy));
            Assert.That(result.Trace.Text, Does.Contain("Boundary=BoxActionMovement"));
        }

        [Test]
        [Category("Extended")]
        public void Flip_LandingHasWallOrBox_IsBlockedWithoutImpact()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateNonUnitBlocker(entityId: 20, position: new Vector2Int(1, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=FlipLandingBlocked",
                    "Cell=(1,0)",
                    "LegalityVerdict=Blocked"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(-1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Core")]
        public void MovementPhase_FlipLandingBlocked_DoesNotGenerateImpact()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), hp: 3, teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateNonUnitBlocker(entityId: 20, position: new Vector2Int(1, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.EventLog.Any(entry => entry.Contains("ImpactReservationCreated", StringComparison.Ordinal)), Is.False);
            Assert.That(result.EventLog.Any(entry => entry.Contains("DamageCommitted", StringComparison.Ordinal)), Is.False);
            Assert.That(result.EventLog.Any(entry => entry.Contains("DestroyMarked", StringComparison.Ordinal)), Is.False);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.BoxActionMovement),
                Is.False);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.hp, Is.EqualTo(3));
            Assert.That(player.markedForDeath, Is.False);
            Assert.That(snapshotAfter.TryGetEntity(20, out var blocker), Is.True);
            Assert.That(blocker.hp, Is.EqualTo(1));
            Assert.That(blocker.markedForDeath, Is.False);
            Assert.That(snapshotAfter.TryGetEntity(30, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(box.markedForDeath, Is.False);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=FlipLandingBlocked",
                    "LegalityVerdict=Blocked"),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void SlidingPush_BoxHitsHostileUnit_CreatesImpactAndStopsSliding()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, position: new Vector2Int(3, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var impactTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    impactTick.MovementPhaseResult.CommitEvents,
                    "ImpactReservationCreated",
                    "Source=30",
                    "Target=40",
                    "At=(3,0)",
                    "Damage=1",
                    "Sequence=1"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    impactTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=30",
                    "State=Idle",
                    "Timer=0"),
                Is.True);
            Assert.That(snapshotAfter.TryGetEntity(30, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(snapshotAfter.TryGetEntity(40, out var enemy), Is.True);
            Assert.That(enemy.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void SlidingPush_BoxKillsLoneHostile_AdvancesSameTickAsBoxSlide()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, position: new Vector2Int(3, 0), hp: 1, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var impactTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);
            var disposition = impactTick.MovementPhaseResult.ImpactDispositionRecords.Single(record => record.ImpactSourceEntityId == 30);

            CollectionAssert.AreEqual(new[] { 40 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(impactTick.EventLog));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    impactTick.MovementPhaseResult.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=40",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    impactTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(3,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(
                impactTick.PresentationData.EntityMotions.Any(
                    motion => motion.EntityId == 30 &&
                              motion.MotionKind == TickEntityMotionKind.BoxSlide &&
                              motion.SourceCell == new SurfaceCell(FaceId.Floor, 2, 0) &&
                              motion.DestinationCell == new SurfaceCell(FaceId.Floor, 3, 0)),
                Is.True);
            Assert.That(snapshotAfter.TryGetEntity(30, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 0)));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(snapshotAfter.TryGetEntity(40, out _), Is.False);
            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.PushLike));
            Assert.That(disposition.DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
            Assert.That(disposition.TargetDestroyed, Is.True);
            Assert.That(disposition.FollowThroughLegalityChecked, Is.True);
            Assert.That(disposition.FollowThroughAccepted, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void SlidingPush_BoxImpactsStackedHostileAndPlayer_DamagesBothAndDoesNotAdvance()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 40, position: new Vector2Int(3, 0), hp: 1, teamId: 2),
                CreateUnit(entityId: 50, position: new Vector2Int(3, 0), hp: 3, teamId: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var impactTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(new[] { 40 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(impactTick.EventLog));
            Assert.That(impactTick.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("MoveCommitted")), Is.False);
            Assert.That(impactTick.PresentationData.EntityMotions, Is.Empty);
            Assert.That(snapshotAfter.TryGetEntity(30, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(snapshotAfter.TryGetEntity(50, out var playerOccupant), Is.True);
            Assert.That(playerOccupant.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 3, 0)));
            Assert.That(playerOccupant.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void Movement_PlayerInput_FlipBeatsPushWhenBothButtonsArePressed()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(3, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(
                new TickInput(
                    1,
                    PlayerTickCommand.Flip(Direction.Right)));

            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_Flip_RejectsFrontBoundaryCrossing()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Up),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Flip),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Up)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=FlipCrossesBoundary",
                    "Origin=(0,1)",
                    "Direction=Up"),
                Is.True);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(GetEntityCell(worldState, 30), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        [Category("Full")]
        public void Movement_MoveAcrossBottomTopEdge_CommitsForwardTopologyChange()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "TopologyCommitted|G=1|I=1|Rotation=Forward|Bottom=Front|Front=Ceiling",
                    "MoveCommitted|G=1|I=1|E=10|To=Front(0,0)|Facing=Up",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(snapshotAfter.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_TopologyChangingTick_RejectsOrdinaryCandidateInSameTick()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Floor, 2, 0), teamId: 2, facing: Direction.Left),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                    new StubMovementLogic(new RawMovementIntent(20, 50, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=20|Reason=TopologyExclusive|BlockedBy=1|BlockingKind=Move|BlockingTopologyChange=True",
                },
                result.MovementPhaseResult.RejectedReasons);
            CollectionAssert.AreEqual(
                new[]
                {
                    "TopologyCommitted|G=1|I=1|Rotation=Forward|Bottom=Front|Front=Ceiling",
                    "MoveCommitted|G=1|I=1|E=10|To=Front(0,0)|Facing=Up",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_OrdinarySelection_RejectsLaterTopologyChangingCandidate()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Floor, 2, 0), teamId: 2, facing: Direction.Left),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(20, 150, new Vector2Int(1, 0))),
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=10|Reason=TopologyExclusive|BlockedBy=1|BlockingKind=Move|BlockingTopologyChange=False",
                },
                result.MovementPhaseResult.RejectedReasons);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MoveCommitted|G=1|I=1|E=20|To=(1,0)|Facing=Left",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_MoveAcrossBottomTopEdge_FailsWhenRotatedDestinationHasWallBlocker()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateNonUnitBlocker(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "I=1",
                    "Reason=BlockedDestination",
                    "Cell=Front(0,0)"),
                Is.True);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_MoveAcrossBottomTopEdge_FailsWhenRotatedDestinationTerrainBlocked()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new GameplayTerrainData(new[] { new Vector2Int(0, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "I=1",
                    "Reason=BlockedDestination",
                    "Cell=Front(0,0)"),
                Is.True);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
        }

        [Test]
        [Category("Full")]
        public void Movement_MoveAcrossBottomBottomEdge_CommitsBackwardTopologyChange()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "TopologyCommitted|G=1|I=1|Rotation=Backward|Bottom=Back|Front=Floor",
                    "MoveCommitted|G=1|I=1|E=10|To=Back(0,1)|Facing=Down",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(snapshotAfter.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Back)));
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Back, 0, 1)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_MoveFromFrontBottomEdge_FailsWithoutRotation()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "I=1",
                    "Reason=BlockedDestination",
                    "Cell=Front(0,-1)"),
                Is.True);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        [Category("Core")]
        public void Movement_MoveIntoItemBoxAcrossBottomFrontSharedEdge_IsRejectedAsCrossFaceInteraction()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Item),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "I=1",
                    "Reason=InteractionCrossesFaceBoundary",
                    "Command=Move",
                    "From=(0,1)",
                    "Cell=Front(0,0)",
                    "Target=20"),
                Is.True);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(CreateSnapshot(worldState).TryGetEntity(20, out var itemBox), Is.True);
            Assert.That(itemBox.markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Movement_RawPushIntentAcrossBottomFrontSharedEdge_IsRejectedAsCrossFaceInteraction()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 100, new Vector2Int(0, 2), MovementCommandKind.Push)),
                });

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "I=1",
                    "Reason=InteractionCrossesFaceBoundary",
                    "Command=Push",
                    "From=(0,1)",
                    "Cell=Front(0,0)",
                    "Target=20"),
                Is.True);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        [Category("Full")]
        public void Movement_PushInputPushBox_ContinuesAcrossBottomFrontSharedEdge()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 0, 1), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var secondTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=Front(0,0)|Facing=Up",
                },
                firstTick.MovementPhaseResult.CommitEvents);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=Front(0,1)|Facing=Up",
                },
                secondTick.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(11));
            Assert.That(firstTick.Trace.Text, Does.Contain("Boundary=BoxActionMovement"));
        }

        [Test]
        [Category("Full")]
        public void Movement_PushSlideImpactOnFrontFace_CreatesFaceAwareReservationAndStopsBeforeTarget()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 0, 1), capabilities: BoxCapabilities.Push),
                    CreateUnit(entityId: 30, position: new SurfaceCell(FaceId.Front, 0, 1), hp: 3, teamId: 2),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var impactTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                firstTick.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("To=Front(0,0)", StringComparison.Ordinal)),
                Is.True);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "ImpactReservationCreated|G=1|I=1|Source=20|Target=30|At=Front(0,1)|Damage=1|Sequence=1",
                },
                impactTick.MovementPhaseResult.CommitEvents
                    .Where(evt => evt.StartsWith("ImpactReservationCreated|", StringComparison.Ordinal))
                    .ToArray());
            Assert.That(impactTick.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("MoveCommitted", StringComparison.Ordinal)), Is.False);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, TargetId: 30, Position: new SurfaceCell(FaceId.Front, 0, 1), Damage: 1, Tick: 13),
                },
                impactTick.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage,
                        Tick: reservation.TickGenerated))
                    .ToArray());
            Assert.That(impactTick.Trace.Text, Does.Contain("Reservation|Source=20|Target=30|Position=Front(0,1)|Damage=1|Tick=13"));
            Assert.That(snapshotAfter.TryGetEntity(20, out var pushedBox), Is.True);
            Assert.That(pushedBox.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(snapshotAfter.TryGetEntity(30, out var enemy), Is.True);
            Assert.That(enemy.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Full")]
        public void Movement_PushInputPushBox_ContinuesAcrossFrontBottomSharedEdgeBackToBottom()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 0, 1)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var secondTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=(0,1)|Facing=Down",
                },
                firstTick.MovementPhaseResult.CommitEvents);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=(0,0)|Facing=Down",
                },
                secondTick.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(11));
            Assert.That(firstTick.Trace.Text, Does.Contain("Boundary=BoxActionMovement"));
        }

        [Test]
        [Category("Full")]
        public void Movement_PushBox_FirstSlideStepIntoFrontShield_IsRejected()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 0, 1), capabilities: BoxCapabilities.Push),
                    CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(CreateBoxSlideShieldSupportEffect(radius: 1));

            try
            {
                var pipeline = GameplayCompositionRoot
                    .CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { CreateImmediatePushPlayerLogic(10) });

                var warningResult = pipeline.RunTick(new TickInput(1));
                Assert.That(warningResult.PresentationData.FrontFaceShieldWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(warningResult.PresentationData.FrontFaceShieldSources, Is.Empty);
                Assert.That(warningResult.PresentationData.FrontFaceShieldBlocks, Is.Empty);
                Assert.That(warningResult.MovementPhaseResult.RejectedReasons, Is.Empty);

                var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
                var snapshotAfter = CreateSnapshot(worldState);

                Assert.That(
                    result.MovementPhaseResult.RejectedReasons,
                    Has.Some.Contains("Reason=BoxSlideBlockedByFrontFaceShield").And.Contains("MovementKind=PushStart"));
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        result.MovementPhaseResult.CommitEvents,
                        "BoxSlideBlockedByFrontFaceShield",
                        "MovementKind=PushStart",
                        "Box=20",
                        "ShieldSource=40"),
                    Is.True);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        result.MovementPhaseResult.CommitEvents,
                        "PlayerActionBlockedByFrontFaceShield",
                        "Actor=10",
                        "Box=20",
                        "ShieldSource=40"),
                    Is.True);
                Assert.That(result.PresentationData.FrontFaceShieldSources, Has.Count.EqualTo(1));
                var sourceSignal = result.PresentationData.FrontFaceShieldSources[0];
                Assert.That(sourceSignal.SourceEntityId, Is.EqualTo(40));
                Assert.That(sourceSignal.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
                Assert.That(sourceSignal.Radius, Is.EqualTo(1));
                Assert.That(sourceSignal.IncludeSourceCell, Is.False);
                Assert.That(sourceSignal.TargetPattern, Is.EqualTo(FrontFaceShieldTargetPattern.ManhattanRadius));
                Assert.That(sourceSignal.TickIndex, Is.EqualTo(2));

                Assert.That(result.PresentationData.FrontFaceShieldBlocks, Has.Count.EqualTo(1));
                var blockSignal = result.PresentationData.FrontFaceShieldBlocks[0];
                Assert.That(blockSignal.ShieldSourceEntityId, Is.EqualTo(40));
                Assert.That(blockSignal.BoxEntityId, Is.EqualTo(20));
                Assert.That(blockSignal.ActorEntityId, Is.EqualTo(10));
                Assert.That(blockSignal.BlockedCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
                Assert.That(blockSignal.ShieldSourceCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
                Assert.That(blockSignal.MovementKind, Is.EqualTo(FrontFaceShieldBlockMovementKind.PushStart));
                Assert.That(blockSignal.TickIndex, Is.EqualTo(2));
                Assert.That(snapshotAfter.TryGetBoxInteractionLockState(20, out _), Is.False);
                Assert.That(snapshotAfter.TryGetEntity(20, out var boxAfter), Is.True);
                Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
                Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
                Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("MoveCommitted|", StringComparison.Ordinal)), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void Movement_PushBox_FirstSlideStepIntoSquareFrontShield_CornerCellIsRejected()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 0, 2)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 1, 2), capabilities: BoxCapabilities.Push),
                    CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(
                CreateBoxSlideShieldSupportEffect(
                    radius: 2,
                    targetPattern: FrontFaceShieldTargetPattern.SquareRadius));

            try
            {
                var pipeline = GameplayCompositionRoot
                    .CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { CreateImmediatePushPlayerLogic(10) });

                var warningResult = pipeline.RunTick(new TickInput(1));
                Assert.That(warningResult.PresentationData.FrontFaceShieldWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(warningResult.PresentationData.FrontFaceShieldSources, Is.Empty);

                var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
                var snapshotAfter = CreateSnapshot(worldState);

                Assert.That(
                    result.MovementPhaseResult.RejectedReasons,
                    Has.Some.Contains("Reason=BoxSlideBlockedByFrontFaceShield").And.Contains("MovementKind=PushStart"));
                Assert.That(result.PresentationData.FrontFaceShieldSources, Has.Count.EqualTo(1));
                Assert.That(
                    result.PresentationData.FrontFaceShieldSources[0].TargetPattern,
                    Is.EqualTo(FrontFaceShieldTargetPattern.SquareRadius));
                Assert.That(result.PresentationData.FrontFaceShieldBlocks, Has.Count.EqualTo(1));
                Assert.That(
                    result.PresentationData.FrontFaceShieldBlocks[0].BlockedCell,
                    Is.EqualTo(new SurfaceCell(FaceId.Front, 2, 2)));
                Assert.That(snapshotAfter.TryGetEntity(20, out var boxAfter), Is.True);
                Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 2)));
                Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void Movement_PushBox_FirstSlideStepIntoManhattanFrontShield_CornerCellCommits()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 0, 2)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 1, 2), capabilities: BoxCapabilities.Push),
                    CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(
                CreateBoxSlideShieldSupportEffect(
                    radius: 2,
                    targetPattern: FrontFaceShieldTargetPattern.ManhattanRadius));

            try
            {
                var pipeline = GameplayCompositionRoot
                    .CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { CreateImmediatePushPlayerLogic(10) });

                pipeline.RunTick(new TickInput(1));
                var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));

                Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
                Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("FrontFaceShield"));
                Assert.That(result.PresentationData.FrontFaceShieldSources, Has.Count.EqualTo(1));
                Assert.That(result.PresentationData.FrontFaceShieldBlocks, Is.Empty);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        result.MovementPhaseResult.CommitEvents,
                        "MoveCommitted",
                        "E=20",
                        "To=Front(2,2)"),
                    Is.True);
                Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 2, 2)));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void Movement_PushBox_FirstSlideStepIntoFrontShield_IgnoresBottomFaceSource()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 0, 1), capabilities: BoxCapabilities.Push),
                    CreateBottomFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Floor, 1, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(CreateBoxSlideShieldSupportEffect(radius: 1));

            try
            {
                var pipeline = GameplayCompositionRoot
                    .CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { CreateImmediatePushPlayerLogic(10) });

                var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

                Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        result.MovementPhaseResult.CommitEvents,
                        "MoveCommitted",
                        "E=20",
                        "To=Front(0,0)"),
                    Is.True);
                Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("FrontFaceShield"));
                Assert.That(result.PresentationData.FrontFaceShieldSources, Is.Empty);
                Assert.That(result.PresentationData.FrontFaceShieldBlocks, Is.Empty);
                Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void Movement_FrontFaceShieldPresentation_ActiveSourceSignalEmitsWithoutBlock()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(
                CreateBoxSlideShieldSupportEffect(
                    radius: 2,
                    includeSourceCell: true,
                    targetPattern: FrontFaceShieldTargetPattern.ManhattanRadius));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);

                var warningResult = pipeline.RunTick(new TickInput(7));
                Assert.That(warningResult.PresentationData.FrontFaceShieldWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(warningResult.PresentationData.FrontFaceShieldSources, Is.Empty);

                var result = pipeline.RunTick(new TickInput(8));

                Assert.That(result.PresentationData.FrontFaceShieldSources, Has.Count.EqualTo(1));
                var sourceSignal = result.PresentationData.FrontFaceShieldSources[0];
                Assert.That(sourceSignal.SourceEntityId, Is.EqualTo(40));
                Assert.That(sourceSignal.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
                Assert.That(sourceSignal.Radius, Is.EqualTo(2));
                Assert.That(sourceSignal.IncludeSourceCell, Is.True);
                Assert.That(sourceSignal.TargetPattern, Is.EqualTo(FrontFaceShieldTargetPattern.ManhattanRadius));
                Assert.That(sourceSignal.TickIndex, Is.EqualTo(8));
                Assert.That(result.PresentationData.FrontFaceShieldBlocks, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Movement_FrontFaceShield_IneligibleDuringWindup_ClearsAndRestartsFullWindup()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(CreateBoxSlideShieldSupportEffect(radius: 1, cooldownTicks: 2));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);

                var firstWarning = pipeline.RunTick(new TickInput(1));
                Assert.That(firstWarning.PresentationData.FrontFaceShieldWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(firstWarning.PresentationData.FrontFaceShieldSources, Is.Empty);

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Floor, 0, 1));
                var clearedTick = pipeline.RunTick(new TickInput(2));
                var clearedSnapshot = CreateSnapshot(worldState);

                Assert.That(clearedTick.PresentationData.FrontFaceShieldWindupWarnings, Is.Empty);
                Assert.That(clearedTick.PresentationData.FrontFaceShieldSources, Is.Empty);
                Assert.That(clearedSnapshot.TryGetEnemyFrontFaceSupportState(40, out var clearedState), Is.True);
                Assert.That(clearedState.EffectStates[0].phase, Is.EqualTo(EnemyFrontFaceSupportEffectPhase.None));
                Assert.That(clearedState.EffectStates[0].cooldownTicksRemaining, Is.EqualTo(2));

                worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Front, 0, 1));
                var cooldownTick = pipeline.RunTick(new TickInput(3));
                var cooldownSnapshot = CreateSnapshot(worldState);

                Assert.That(cooldownTick.PresentationData.FrontFaceShieldWindupWarnings, Is.Empty);
                Assert.That(cooldownTick.PresentationData.FrontFaceShieldSources, Is.Empty);
                Assert.That(cooldownSnapshot.TryGetEnemyFrontFaceSupportState(40, out var cooldownState), Is.True);
                Assert.That(cooldownState.EffectStates[0].phase, Is.EqualTo(EnemyFrontFaceSupportEffectPhase.None));
                Assert.That(cooldownState.EffectStates[0].cooldownTicksRemaining, Is.EqualTo(1));

                var restartedWarning = pipeline.RunTick(new TickInput(4));
                Assert.That(restartedWarning.PresentationData.FrontFaceShieldWindupWarnings, Has.Count.EqualTo(1));
                Assert.That(restartedWarning.PresentationData.FrontFaceShieldWindupWarnings[0].ActivationSequence, Is.EqualTo(2));
                Assert.That(restartedWarning.PresentationData.FrontFaceShieldSources, Is.Empty);

                var activeTick = pipeline.RunTick(new TickInput(5));

                Assert.That(activeTick.PresentationData.FrontFaceShieldWindupWarnings, Is.Empty);
                Assert.That(activeTick.PresentationData.FrontFaceShieldSources, Has.Count.EqualTo(1));
                Assert.That(activeTick.PresentationData.FrontFaceShieldSources[0].SourceEntityId, Is.EqualTo(40));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [TestCase("OffFront")]
        [TestCase("DeadHp")]
        [TestCase("MarkedForDeath")]
        [TestCase("Detached")]
        [TestCase("DeadAiMode")]
        [TestCase("NoCapability")]
        [Category("Full")]
        public void Movement_FrontFaceShieldPresentation_InactiveSourceCasesEmitNoSourceSignal(string inactiveCase)
        {
            var enemy = CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 0, 0));
            EnemyAiProfile profile = null;
            if (!string.Equals(inactiveCase, "NoCapability", StringComparison.Ordinal))
            {
                profile = CreateFrontFaceSupportProfile(CreateBoxSlideShieldSupportEffect(radius: 1));
            }

            switch (inactiveCase)
            {
                case "OffFront":
                    enemy.position = new SurfaceCell(FaceId.Floor, 0, 0);
                    break;

                case "DeadHp":
                    enemy.hp = 0;
                    break;

                case "MarkedForDeath":
                    enemy.markedForDeath = true;
                    break;

                case "Detached":
                    enemy.boardPresence = EntityBoardPresence.Detached;
                    break;

                case "DeadAiMode":
                    enemy.aiMode = EnemyAiMode.Dead;
                    break;
            }

            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { enemy },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 0)),
                GameplayTerrainData.Empty);

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);

                var result = pipeline.RunTick(new TickInput(3));

                Assert.That(result.PresentationData.FrontFaceShieldSources, Is.Empty);
                Assert.That(result.PresentationData.FrontFaceShieldBlocks, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void Movement_PushBox_FirstSlideStepOutsideFrontShield_CommitsWithoutBlockSignal()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Up),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 0, 2), capabilities: BoxCapabilities.Push),
                    CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 0, 2)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 2)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(CreateBoxSlideShieldSupportEffect(radius: 1));
            worldState.SetEnemyFrontFaceSupportState(
                40,
                CreateActiveFrontFaceSupportState(
                    radius: 1,
                    includeSourceCell: false,
                    targetPattern: FrontFaceShieldTargetPattern.ManhattanRadius));

            try
            {
                var pipeline = GameplayCompositionRoot
                    .CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { CreateImmediatePushPlayerLogic(10) });

                var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

                Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
                Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("FrontFaceShield"));
                Assert.That(result.PresentationData.FrontFaceShieldSources, Has.Count.EqualTo(1));
                Assert.That(result.PresentationData.FrontFaceShieldBlocks, Is.Empty);
                Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void Movement_SlidingPushBox_NextStepIntoFrontShield_StopsWithoutImpact()
        {
            var slidingBox = CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Push, facing: Direction.Up);
            slidingBox.state = EntityPhaseState.Sliding;
            slidingBox.stateTimer = 0;
            slidingBox.kineticInstigatorEntityId = 10;
            slidingBox.kineticInstigatorTeamId = 1;
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    slidingBox,
                    CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 0, 2)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 2)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(CreateBoxSlideShieldSupportEffect(radius: 1));
            worldState.SetEnemyFrontFaceSupportState(
                40,
                CreateActiveFrontFaceSupportState(
                    radius: 1,
                    includeSourceCell: false,
                    targetPattern: FrontFaceShieldTargetPattern.ManhattanRadius));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);

                var result = pipeline.RunTick(new TickInput(1));
                var snapshotAfter = CreateSnapshot(worldState);

                Assert.That(
                    result.MovementPhaseResult.RejectedReasons,
                    Has.Some.Contains("Reason=BoxSlideBlockedByFrontFaceShield").And.Contains("MovementKind=SlidingContinuation"));
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        result.MovementPhaseResult.CommitEvents,
                        "BoxSlideBlockedByFrontFaceShield",
                        "MovementKind=SlidingContinuation",
                        "Box=20",
                        "ShieldSource=40"),
                    Is.True);
                Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("PlayerActionBlockedByFrontFaceShield"));
                Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("ImpactReservationCreated"));
                Assert.That(result.PresentationData.FrontFaceShieldSources, Has.Count.EqualTo(1));
                Assert.That(result.PresentationData.FrontFaceShieldSources[0].SourceEntityId, Is.EqualTo(40));
                Assert.That(result.PresentationData.FrontFaceShieldSources[0].SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 2)));
                Assert.That(result.PresentationData.FrontFaceShieldBlocks, Has.Count.EqualTo(1));
                var blockSignal = result.PresentationData.FrontFaceShieldBlocks[0];
                Assert.That(blockSignal.ShieldSourceEntityId, Is.EqualTo(40));
                Assert.That(blockSignal.BoxEntityId, Is.EqualTo(20));
                Assert.That(blockSignal.ActorEntityId, Is.EqualTo(10));
                Assert.That(blockSignal.BlockedCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
                Assert.That(blockSignal.ShieldSourceCell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 2)));
                Assert.That(blockSignal.MovementKind, Is.EqualTo(FrontFaceShieldBlockMovementKind.SlidingContinuation));
                Assert.That(snapshotAfter.TryGetEntity(20, out var boxAfter), Is.True);
                Assert.That(boxAfter.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
                Assert.That(boxAfter.state, Is.EqualTo(EntityPhaseState.Idle));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void Movement_FlipBox_DoesNotUseFrontFaceShieldInV1()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 1, 1)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 1, 2), capabilities: BoxCapabilities.Flip),
                    CreateFrontFaceEnemy(entityId: 40, position: new SurfaceCell(FaceId.Front, 2, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                GameplayTerrainData.Empty);
            var profile = CreateFrontFaceSupportProfile(CreateBoxSlideShieldSupportEffect(radius: 1));

            try
            {
                var pipeline = GameplayCompositionRoot
                    .CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(worldState, new IEntityLogic[] { CreateImmediateFlipPlayerLogic(10) });

                var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Up)));

                Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
                Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("FrontFaceShield"));
                Assert.That(
                    SemanticEventAssertions.ContainsEvent(
                        result.MovementPhaseResult.CommitEvents,
                        "MoveCommitted",
                        "E=20",
                        "To=Front(1,0)"),
                    Is.True);
                Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Movement_PushDestroyBox_WhenSlideStopperIsAdjacent_DetachesAndRemovesBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Destroy),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(2, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "BoardPresenceCommitted",
                    "E=20",
                    "Presence=Detached"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=20",
                    "Condition=AlwaysMark"),
                Is.True);
            CollectionAssert.AreEqual(
                Array.Empty<(int EntityId, TickVisibilityChangeKind Kind)>(),
                result.PresentationData
                    .VisibilityChanges
                    .Select(change => (change.EntityId, change.ChangeKind))
                    .ToArray());
            Assert.That(result.PresentationData.EntityExitSignals.Count, Is.EqualTo(1));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitedEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitCause, Is.EqualTo(TickEntityExitCause.BoxDestroy));
            CollectionAssert.AreEqual(new[] { 20 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Movement_SlidingPushDestroyBox_WhenLaterSlideStops_RemainsOnBoardAndBecomesIdle()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Destroy),
                    CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(3, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(5, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var secondTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=20",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=20",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            CollectionAssert.AreEqual(
                Array.Empty<string>(),
                idleTicks.SelectMany(result => result.MovementPhaseResult.CommitEvents).ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    secondTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=20",
                    "State=Idle",
                    "Timer=0"),
                Is.True);
            CollectionAssert.AreEqual(Array.Empty<string>(), secondTick.MovementPhaseResult.RejectedReasons);
            CollectionAssert.AreEqual(Array.Empty<int>(), SemanticEventAssertions.GetCleanupRemovedEntityIds(secondTick.EventLog));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void Movement_Flip_RejectsLaterCandidateThatMovesSameBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(2, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                    new StubMovementLogic(new RawMovementIntent(20, 5, new Vector2Int(1, 0), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Resolve",
                    "Source=20",
                    "Reason=SharedMovedEntity",
                    "Entity=30"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "FacingCommitted",
                    "E=10",
                    "Facing=Left"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=30",
                    "To=(-1,0)",
                    "Facing=Left"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(-1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        [Category("Extended")]
        public void Movement_SameDestination_OnlyHigherPriorityWins()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(2, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                    new StubMovementLogic(new RawMovementIntent(20, 10, new Vector2Int(1, 0))),
                });

            var occupancyBefore = DumpUnitOccupancy(CreateSnapshot(worldState));

            var result = pipeline.RunTick(new TickInput(1));

            var occupancyAfter = DumpUnitOccupancy(CreateSnapshot(worldState));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, IntentId: 1),
                    (SourceId: 10, IntentId: 2),
                },
                result.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=20",
                    "To=(1,0)",
                    "Facing=Left"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(occupancyBefore, Is.EqualTo("10@(0,0),20@(2,0)"));
            Assert.That(occupancyAfter, Is.EqualTo("10@(1,0),20@(1,0)"));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Core")]
        public void MovementPhase_SameDestination_OnlyHigherPriorityWins_BoundaryInvariant()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateUnit(entityId: 20, position: new Vector2Int(4, 0), facing: Direction.Left),
                CreateBox(entityId: 40, position: new Vector2Int(3, 0), capabilities: BoxCapabilities.Push),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0), MovementCommandKind.Push)),
                    new StubMovementLogic(new RawMovementIntent(20, 10, new Vector2Int(3, 0), MovementCommandKind.Push)),
                });

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, IntentId: 1),
                    (SourceId: 10, IntentId: 2),
                },
                result.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=40",
                    "To=(2,0)",
                    "Facing=Left"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Resolve",
                    "Source=10",
                    "Reason=DestinationReserved",
                    "Cell=(2,0)"),
                Is.True);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 40), Is.EqualTo(new Vector2Int(2, 0)));
            LegacyMovementBoundaryAssert.HasMoveEntityBoundary(
                result,
                40,
                MovementExecutionBoundaryKind.BoxActionMovement);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Unknown),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Movement_EdgeReservation_DoesNotPersistAcrossTicks()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(
                        new Dictionary<int, RawMovementIntent>
                        {
                            { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                            { 2, new RawMovementIntent(10, 5, new Vector2Int(0, 0)) },
                        }),
                });

            var firstTick = pipeline.RunTick(new TickInput(1));
            var secondTick = pipeline.RunTick(new TickInput(2));

            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right" },
                firstTick.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=10|To=(0,0)|Facing=Left" },
                secondTick.MovementPhaseResult.CommitEvents);
            Assert.That(firstTick.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(secondTick.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(secondTick.Trace.Text, Does.Not.Contain("Reason=EdgeReserved"));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        [Category("Core")]
        public void Movement_SameInput_AssignsDeterministicIntentIds()
        {
            var firstRun = RunDeterministicMovementTick();
            var secondRun = RunDeterministicMovementTick();

            CollectionAssert.AreEqual(
                firstRun.Result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray(),
                secondRun.Result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                firstRun.Result.MovementPhaseResult.CommitEvents,
                secondRun.Result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                firstRun.Result.MovementPhaseResult.RejectedReasons,
                secondRun.Result.MovementPhaseResult.RejectedReasons);
            Assert.That(firstRun.OccupancyAfter, Is.EqualTo(secondRun.OccupancyAfter));
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1),
                    (SourceId: 20, IntentId: 2),
                },
                firstRun.Result.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void Movement_ProjectileImpact_CreatesReservation_AndAttackConsumesIt()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, position: new Vector2Int(0, 0), hp: 1),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "ImpactReservationCreated",
                    "Source=10",
                    "Target=20",
                    "At=(1,0)",
                    "Damage=1",
                    "Sequence=1"),
                Is.True);
            Assert.That(result.MovementPhaseResult.CommitEvents.All(evt => !evt.Contains("DamageCommitted")), Is.True);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20, Damage: 1, Tick: 1, Position: new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.Damage,
                        Tick: reservation.TickGenerated,
                        reservation.ImpactCell))
                    .ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.CommitEvents,
                    "DamageCommitted",
                    "Target=20",
                    "Amount=1"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.CommitEvents,
                    "DamageCommitted",
                    "Target=10",
                    "Amount=1"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.AttackPhaseResult.CommitEvents,
                    "DestroyMarked",
                    "Target=10",
                    "Condition=WhenHpDepleted"),
                Is.True);
            CollectionAssert.AreEqual(new[] { 10 }, SemanticEventAssertions.GetCleanupRemovedEntityIds(result.EventLog));

            Assert.That(finalSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var targetAfterTick), Is.True);
            Assert.That(targetAfterTick.hp, Is.EqualTo(2));
            Assert.That(targetAfterTick.markedForDeath, Is.False);
            Assert.That(result.Trace.Text, Does.Contain("ImpactReservationCreated"));
            Assert.That(result.Trace.Text, Does.Contain("Source=10"));
            Assert.That(result.Trace.Text, Does.Contain("Target=20"));
        }

        [Test]
        [Category("Extended")]
        public void Movement_ProjectileImpact_PrefersHostileTargetWithinStackedUnits()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, position: new Vector2Int(0, 0), hp: 1),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 3, teamId: 1),
                CreateUnit(entityId: 30, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "ImpactReservationCreated",
                    "Source=10",
                    "Target=30",
                    "At=(1,0)",
                    "Damage=1",
                    "Sequence=1"),
                Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 30, Damage: 1, Tick: 1, Position: new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.Damage,
                        Tick: reservation.TickGenerated,
                        reservation.ImpactCell))
                    .ToArray());

            Assert.That(finalSnapshot.TryGetEntity(20, out var friendlyUnit), Is.True);
            Assert.That(friendlyUnit.hp, Is.EqualTo(3));
            Assert.That(finalSnapshot.TryGetEntity(30, out var hostileUnit), Is.True);
            Assert.That(hostileUnit.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void MovementCommitter_ProjectileImpact_UsesResolvedGroupTargetWithoutIntentLookup()
        {
            var timingProfile = CreateTimingProfile();
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, position: new Vector2Int(0, 0), hp: 1),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
            });
            var snapshot = CreateSnapshot(worldState);
            var projectileImpactGroup = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.ProjectileImpact);
            projectileImpactGroup.AssignGroupId(1);
            projectileImpactGroup.AssignProjectileImpactTarget(20);

            var transientBuffer = new ImpactReservationBuffer();
            var commitEvents = new List<string>();

            new MovementCommitter(CreateDefaultPlayerControlTimingSnapshot(timingProfile), timingProfile).Commit(
                snapshot,
                Array.Empty<MoveIntent>(),
                tickIndex: 1,
                worldState.CreateWriteContext(),
                transientBuffer,
                new[] { projectileImpactGroup },
                commitEvents);

            var reservations = transientBuffer.DrainImpacts();

            CollectionAssert.AreEqual(
                new[]
                {
                    "ImpactReservationCreated|G=1|I=1|Source=10|Target=20|At=(1,0)|Damage=1|Sequence=1",
                },
                commitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20, Position: new SurfaceCell(FaceId.Floor, 1, 0), Damage: 1, Tick: 1),
                },
                reservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage,
                        Tick: reservation.TickGenerated))
                    .ToArray());
        }

        [Test]
        [Category("Full")]
        public void Movement_ProjectileReservations_AssignSequenceByCommitOrder()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, position: new Vector2Int(0, 0), hp: 1),
                CreateProjectile(entityId: 20, position: new Vector2Int(4, 0), hp: 1),
                CreateUnit(entityId: 30, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
                CreateUnit(entityId: 40, position: new Vector2Int(3, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                    new StubMovementLogic(new RawMovementIntent(20, 10, new Vector2Int(3, 0))),
                });

            var result = pipeline.RunTick(new TickInput(7));

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "ImpactReservationCreated",
                    "Source=20",
                    "Target=40",
                    "At=(3,0)",
                    "Damage=1",
                    "Sequence=1"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.CommitEvents,
                    "ImpactReservationCreated",
                    "Source=10",
                    "Target=30",
                    "At=(1,0)",
                    "Damage=1",
                    "Sequence=2"),
                Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 30, Damage: 1, Tick: 7, Position: new SurfaceCell(FaceId.Floor, 1, 0)),
                    (SourceId: 20, TargetId: 40, Damage: 1, Tick: 7, Position: new SurfaceCell(FaceId.Floor, 3, 0)),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.Damage,
                        Tick: reservation.TickGenerated,
                        reservation.ImpactCell))
                    .ToArray());
        }

        [Test]
        [Category("Full")]
        public void Movement_CrossFaceImpactReservation_IsRejectedBeforePayloadCreation()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), capabilities: BoxCapabilities.Push),
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0), hp: 3, teamId: 2),
                },
                new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, Array.Empty<IEntityLogic>());
            var snapshot = CreateSnapshot(worldState);
            var group = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.BoxImpact);
            group.AssignGroupId(1);
            group.AssignImpactReservation(10, 20);
            var rejectedReasons = new List<string>();
            var payloads = InvokeBuildMovementActionPlanPayloads(
                pipeline,
                snapshot,
                Array.Empty<MoveIntent>(),
                new[] { group },
                rejectedReasons,
                tickIndex: 1);

            Assert.That(payloads.ContainsKey(1), Is.True);
            Assert.That(payloads[1].HasImpactReservationPayload, Is.False);
            CollectionAssert.AreEqual(
                new[]
                {
                    "ImpactReservationRejected|Stage=Plan|G=1|I=1|Source=10|Targets=20|Reason=CrossFaceUnsupported|SourceCell=(0,0)|ImpactCell=Front(0,0)",
                },
                rejectedReasons);

            var impactReservations = InvokeResolveMovementImpactReservationsCanonical(
                pipeline,
                tickIndex: 1,
                orderedActionPlanIds: new[] { 1 },
                payloads,
                new[]
                {
                    new ResolutionRecord(
                        contestId: 1,
                        ContestKind.Space,
                        accepted: true,
                        sourceId: 10,
                        priority: 5,
                        actionPlanId: 1,
                        affectedEntityId: 0,
                        localActionIndex: 0),
                });

            Assert.That(impactReservations, Is.Empty);
        }

        private static (TickResult Result, string OccupancyAfter) RunDeterministicMovementTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(2, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(20, 1, new Vector2Int(3, 0))),
                    new StubMovementLogic(new RawMovementIntent(10, 1, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(7));
            return (result, DumpUnitOccupancy(CreateSnapshot(worldState)));
        }

        private static List<TickResult> RunTicks(TickPipeline pipeline, int startTickIndex, int endTickIndex)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (endTickIndex < startTickIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endTickIndex));
            }

            var results = new List<TickResult>(endTickIndex - startTickIndex + 1);
            for (var tickIndex = startTickIndex; tickIndex <= endTickIndex; tickIndex++)
            {
                results.Add(pipeline.RunTick(new TickInput(tickIndex)));
            }

            return results;
        }

        private static GameplayTimingProfile CreateTimingProfile(
            int simulationTicksPerSecond = 60,
            float initialMoveDelaySeconds = 0f,
            float repeatedMoveIntervalSeconds = 0.4f,
            float boxSlideStepIntervalSeconds = 0.2f,
            float projectileStepIntervalSeconds = 0.2f,
            float pushMotionDurationSeconds = 0.2f,
            float flipMotionDurationSeconds = 0.2f,
            float flipArcHeightInCells = 0.65f,
            int maxTicksPerFrame = 8)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond,
                initialMoveDelaySeconds,
                repeatedMoveIntervalSeconds,
                boxSlideStepIntervalSeconds,
                projectileStepIntervalSeconds,
                pushMotionDurationSeconds,
                flipMotionDurationSeconds,
                flipArcHeightInCells,
                maxTicksPerFrame);
        }

        private static TickPipeline CreateTickPipelineWithFlags(
            WorldState worldState,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var timingProfile = CreateTimingProfile();
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                runtimeFeatureFlags: runtimeFeatureFlags);
        }

        private static List<MoveIntent> InvokeValidateLegacyExpansionIntents(
            TickPipeline pipeline,
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> expansionIntents,
            List<string> rejectedReasons)
        {
            var method = typeof(TickPipeline).GetMethod(
                "ValidateLegacyExpansionIntents",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);

            return (List<MoveIntent>)method.Invoke(
                pipeline,
                new object[]
                {
                    snapshot,
                    expansionIntents,
                    rejectedReasons,
                });
        }

        private static void AssertLegacyExpansionIntentAllowed(
            WorldState worldState,
            MoveIntent intent,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var rejectedReasons = new List<string>();
            var filteredIntents = InvokeValidateLegacyExpansionIntents(
                CreateTickPipelineWithFlags(worldState, runtimeFeatureFlags),
                CreateSnapshot(worldState),
                new[] { intent },
                rejectedReasons);

            CollectionAssert.AreEqual(new[] { intent.IntentId }, filteredIntents.Select(filtered => filtered.IntentId).ToArray());
            Assert.That(
                rejectedReasons.Any(reason => reason.Contains("LegacyUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                Is.False);
        }

        private static void AssertLegacyExpansionIntentBlocked(
            WorldState worldState,
            MoveIntent intent,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            string expectedReason)
        {
            var rejectedReasons = new List<string>();
            var filteredIntents = InvokeValidateLegacyExpansionIntents(
                CreateTickPipelineWithFlags(worldState, runtimeFeatureFlags),
                CreateSnapshot(worldState),
                new[] { intent },
                rejectedReasons);

            Assert.That(filteredIntents, Is.Empty);
            Assert.That(
                rejectedReasons.Any(reason =>
                    reason.Contains("LegacyUnitOrdinaryMovementDetected", StringComparison.Ordinal) &&
                    reason.Contains($"E={intent.SourceId}", StringComparison.Ordinal) &&
                    reason.Contains(expectedReason, StringComparison.Ordinal)),
                Is.True,
                string.Join("\n", rejectedReasons));
        }

        private static void AssertNoUnexpectedUnknownMovementBoundary(TickResult result)
        {
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Unknown),
                Is.False,
                string.Join(
                    "\n",
                    result.MovementPhaseResult.ResolvedOperations.Select(operation =>
                        $"Op|Kind={operation.Kind}|E={operation.EntityId}|Boundary={operation.Metadata.MovementExecutionBoundaryKind}|Reason={operation.Metadata.BoundaryReason}")));
            Assert.That(result.Trace.Text, Does.Not.Contain("Boundary=Unknown"));
        }

        private static IMovementEntityLogic CreateImmediatePushPlayerLogic(int entityId)
        {
            return new ImmediatePlayerInteractionLogic(entityId, MovementCommandKind.Push);
        }

        private static IMovementEntityLogic CreateImmediateFlipPlayerLogic(int entityId)
        {
            return new ImmediatePlayerInteractionLogic(entityId, MovementCommandKind.Flip);
        }

        private static EnemyAiProfile CreateFrontFaceSupportProfile(EnemyFrontFaceSupportEffectAuthoring effect)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
                FrontFaceSupportEffects = new[] { effect },
            });
        }

        private static EnemyFrontFaceSupportEffectAuthoring CreateBoxSlideShieldSupportEffect(
            int radius = 1,
            bool includeSourceCell = false,
            FrontFaceShieldTargetPattern targetPattern = FrontFaceShieldTargetPattern.ManhattanRadius,
            int windupTicks = 1,
            int cooldownTicks = 1)
        {
            var boxSlideShield = new BoxSlideShieldAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(boxSlideShield, "radius", radius);
            EnemyAiProfileTestFactory.SetSerializedField(boxSlideShield, "includeSourceCell", includeSourceCell);
            EnemyAiProfileTestFactory.SetSerializedField(boxSlideShield, "targetPattern", targetPattern);
            EnemyAiProfileTestFactory.SetSerializedField(boxSlideShield, "windupSeconds", TicksToSeconds(windupTicks));
            EnemyAiProfileTestFactory.SetSerializedField(boxSlideShield, "cooldownSeconds", TicksToSeconds(cooldownTicks));

            var effect = new EnemyFrontFaceSupportEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyFrontFaceSupportEffectKind.BoxSlideShield);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "boxSlideShield", boxSlideShield);
            return effect;
        }

        private static EnemyFrontFaceSupportRuntimeState CreateActiveFrontFaceSupportState(
            int radius,
            bool includeSourceCell,
            FrontFaceShieldTargetPattern targetPattern)
        {
            return new EnemyFrontFaceSupportRuntimeState(
                new[]
                {
                    new EnemyFrontFaceSupportEffectState
                    {
                        phase = EnemyFrontFaceSupportEffectPhase.Active,
                        windupStartTick = 0,
                        windupEndTick = 1,
                        activationSequence = 1,
                        radius = radius,
                        includeSourceCell = includeSourceCell,
                        targetPattern = targetPattern,
                    },
                });
        }

        private static float TicksToSeconds(int ticks)
        {
            return ticks / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        }

        private static (MovementPhaseResult Result, WorldSnapshot SnapshotAfterMovement) RunMovementPhaseOnly(
            WorldState worldState,
            TickInput input,
            params IMovementEntityLogic[] entityLogics)
        {
            var timingProfile = CreateTimingProfile();
            var snapshot = CreateSnapshot(worldState);
            var rawMovementIntents = new List<RawMovementIntent>();
            new MovementIntentCollector().Collect(snapshot, in input, entityLogics, rawMovementIntents);

            var idAllocator = new IdAllocator();
            idAllocator.ResetForTick(input.TickIndex);
            rawMovementIntents.Sort(RawMovementIntentComparer.Instance);

            var sortedIntents = new List<MoveIntent>(rawMovementIntents.Count);
            for (var i = 0; i < rawMovementIntents.Count; i++)
            {
                var rawIntent = rawMovementIntents[i];
                MoveIntent moveIntent = rawIntent.CommandKind switch
                {
                    MovementCommandKind.Push => new PushIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                    MovementCommandKind.Flip => new FlipIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                    _ => new MoveIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                };
                moveIntent.AssignIntentId(idAllocator.AllocateIntentId());
                sortedIntents.Add(moveIntent);
            }

            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();
            new MovementExpander().Expand(snapshot, sortedIntents, expandedCandidates, rejectedReasons);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                expandedCandidates[i].AssignGroupId(idAllocator.AllocateGroupId());
            }

            var selectedGroups = new List<ActionGroup>();
            new MovementResolver().Resolve(snapshot, expandedCandidates, selectedGroups, rejectedReasons);

            var commitEvents = new List<string>();
            new MovementCommitter(CreateDefaultPlayerControlTimingSnapshot(timingProfile), timingProfile).Commit(
                snapshot,
                sortedIntents,
                input.TickIndex,
                worldState.CreateWriteContext(),
                new ImpactReservationBuffer(),
                selectedGroups,
                commitEvents);

            return (
                CanonicalPhaseResultFactory.CreateMovementPhaseResult(
                    rawMovementIntents,
                    sortedIntents,
                    selectedGroups,
                    commitEvents,
                    rejectedReasons),
                CreateSnapshot(worldState));
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(
            GameplayTimingProfile timingProfile = null)
        {
            var generalTimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond,
                generalTimingProfile.RepeatedMoveIntervalSeconds);
        }

        private static EntityState CreateUnit(
            int entityId,
            Vector2Int position,
            int hp = 3,
            int teamId = 1,
            Direction facing = Direction.Right,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position), hp, teamId, facing, unitMobilityKind);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int hp = 3,
            int teamId = 1,
            Direction facing = Direction.Right,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitMobilityKind = unitMobilityKind,
                facing = facing,
                state = EntityPhaseState.Idle,
            };
        }

        private static TileFeatureState CreateDestroyTile(int tileId, SurfaceCell cell)
        {
            return new TileFeatureState(
                tileId,
                cell,
                TileFeatureKind.Destroy,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateTileFeatureDefinition(
            int tileId,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: string.Empty);
        }

        private static EntityState CreateFrontFaceEnemy(int entityId, SurfaceCell position, int hp = 3)
        {
            var enemy = CreateUnit(entityId, position, hp, teamId: 2, facing: Direction.Right);
            enemy.aiMode = EnemyAiMode.Patrol;
            enemy.unitRole = UnitRole.Enemy;
            return enemy;
        }

        private static EntityState CreateBottomFaceEnemy(int entityId, SurfaceCell position, int hp = 3)
        {
            var enemy = CreateUnit(entityId, position, hp, teamId: 2, facing: Direction.Right);
            enemy.aiMode = EnemyAiMode.Patrol;
            enemy.unitRole = UnitRole.Enemy;
            return enemy;
        }

        private static EntityState CreateNonUnitBlocker(int entityId, Vector2Int position)
        {
            return CreateNonUnitBlocker(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateNonUnitBlocker(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                facing = Direction.None,
            };
        }

        private static EnemyJumpRuntimeState CreateEnemyJumpState(EnemyJumpPhase phase, int sequence)
        {
            return new EnemyJumpRuntimeState
            {
                phase = phase,
                sequence = sequence,
                sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                lockedTargetCell = new SurfaceCell(FaceId.Floor, 1, 0),
                windupEndTick = 2,
                landingTick = 3,
                cooldownRemainingTicks = phase == EnemyJumpPhase.Cooldown ? 2 : 0,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            Vector2Int position,
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            Direction facing = Direction.Right)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position), capabilities, facing);
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
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
                facing = facing,
                state = EntityPhaseState.Idle,
                boxCapabilities = capabilities,
            };
        }

        private static EntityState CreateProjectile(int entityId, Vector2Int position, int hp)
        {
            return CreateProjectile(entityId, SurfaceCell.FromPlanar(position), hp);
        }

        private static EntityState CreateProjectile(int entityId, SurfaceCell position, int hp)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = 1,
                type = EntityType.Projectile,
                state = EntityPhaseState.Idle,
            };
        }

        private static string DumpUnitOccupancy(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            return string.Join(
                ",",
                entities
                    .Where(entity => entity.type == EntityType.Unit)
                    .Select(entity => $"{entity.entityId}@({entity.position.x},{entity.position.y})"));
        }

        private static Vector2Int GetEntityPosition(WorldState worldState, int entityId)
        {
            var snapshot = CreateSnapshot(worldState);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.position;
        }

        private static SurfaceCell GetEntityCell(WorldState worldState, int entityId)
        {
            var snapshot = CreateSnapshot(worldState);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.position;
        }

        private static int[] GetUnitIdsAt(WorldState worldState, SurfaceCell cell)
        {
            var snapshot = CreateSnapshot(worldState);
            var entities = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, entities);
            return entities.Select(entity => entity.entityId).ToArray();
        }

        private static Direction GetEntityFacing(WorldState worldState, int entityId)
        {
            var snapshot = CreateSnapshot(worldState);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.facing;
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, terrainData);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                initialTileFeatures);
        }

        private static TickPipeline CreatePlayerTileFeaturePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                new IEntityLogic[] { new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))) },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                playerRespawnDelayTicks: 1,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline,
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            var createSnapshotMethod = typeof(WorldState).GetMethod(
                "CreateSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createSnapshotMethod, Is.Not.Null);

            return (WorldSnapshot)createSnapshotMethod.Invoke(worldState, null);
        }

        private static void DestroyProfile(EnemyAiProfile profile)
        {
            EnemyAiProfileTestFactory.Destroy(profile);
        }

        private static Dictionary<int, MovementActionPlanPayload> InvokeBuildMovementActionPlanPayloads(
            TickPipeline pipeline,
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            IReadOnlyList<ActionGroup> expandedCandidates,
            List<string> rejectedReasons,
            int tickIndex)
        {
            var method = typeof(TickPipeline).GetMethod(
                "BuildMovementActionPlanPayloads",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);

            return (Dictionary<int, MovementActionPlanPayload>)method.Invoke(
                pipeline,
                new object[]
                {
                    snapshot,
                    sortedIntents,
                    expandedCandidates,
                    rejectedReasons,
                    tickIndex,
                });
        }

        private static List<ImpactReservation> InvokeResolveMovementImpactReservationsCanonical(
            TickPipeline pipeline,
            int tickIndex,
            IReadOnlyList<int> orderedActionPlanIds,
            IReadOnlyDictionary<int, MovementActionPlanPayload> payloads,
            IReadOnlyList<ResolutionRecord> resolutionRecords)
        {
            var method = typeof(TickPipeline).GetMethod(
                "ResolveMovementImpactReservationsCanonical",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);

            return (List<ImpactReservation>)method.Invoke(
                pipeline,
                new object[]
                {
                    tickIndex,
                    orderedActionPlanIds,
                    payloads,
                    resolutionRecords,
                });
        }

        private sealed class StubMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent? _movementIntent;

            public StubMovementLogic(RawMovementIntent? movementIntent)
            {
                _movementIntent = movementIntent;
            }

            public int ControlledEntityId => _movementIntent?.SourceId ?? 0;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_movementIntent.HasValue)
                {
                    buffer.Add(_movementIntent.Value);
                }
            }
        }

        private sealed class ScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _controlledEntityId;
            private readonly IReadOnlyDictionary<int, RawMovementIntent> _movementIntentsByTick;

            public ScriptedMovementLogic(IReadOnlyDictionary<int, RawMovementIntent> movementIntentsByTick)
            {
                _movementIntentsByTick = movementIntentsByTick;
                _controlledEntityId = ResolveControlledEntityId(movementIntentsByTick);
            }

            public int ControlledEntityId => _controlledEntityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_movementIntentsByTick.TryGetValue(input.TickIndex, out var movementIntent))
                {
                    buffer.Add(movementIntent);
                }
            }

            private static int ResolveControlledEntityId(IReadOnlyDictionary<int, RawMovementIntent> movementIntentsByTick)
            {
                foreach (var pair in movementIntentsByTick)
                {
                    return pair.Value.SourceId;
                }

                return 0;
            }
        }

        private sealed class ImmediatePlayerInteractionLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly MovementCommandKind _commandKind;
            private readonly int _entityId;

            public ImmediatePlayerInteractionLogic(int entityId, MovementCommandKind commandKind)
            {
                _entityId = entityId;
                _commandKind = commandKind;
            }

            public int ControlledEntityId => _entityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (snapshot == null)
                {
                    throw new ArgumentNullException(nameof(snapshot));
                }

                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (!snapshot.TryGetEntity(_entityId, out var entity) ||
                    entity.hp <= 0 ||
                    entity.markedForDeath ||
                    input.PlayerCommand.MoveDirection == Direction.None ||
                    (_commandKind == MovementCommandKind.Flip && !input.PlayerCommand.FlipPressed) ||
                    !TryResolveDelta(input.PlayerCommand.MoveDirection, out var delta))
                {
                    return;
                }

                buffer.Add(
                    new RawMovementIntent(
                        _entityId,
                        priority: 100,
                        entity.position.PlanarPosition + delta,
                        _commandKind,
                        localSequence: 0));
            }

            private static bool TryResolveDelta(Direction direction, out Vector2Int delta)
            {
                switch (direction)
                {
                    case Direction.Up:
                        delta = Vector2Int.up;
                        return true;

                    case Direction.Right:
                        delta = Vector2Int.right;
                        return true;

                    case Direction.Down:
                        delta = Vector2Int.down;
                        return true;

                    case Direction.Left:
                        delta = Vector2Int.left;
                        return true;

                    default:
                        delta = Vector2Int.zero;
                        return false;
                }
            }
        }
    }
}
