using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class PlayerContinuousLocomotionScenarioTests
    {
        [Test]
        [Category("Extended")]
        public void Player_Free2D_StartRight_FromCenter()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.GreaterThan(0));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Moving));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                track.EntityId == 10 &&
                track.DestinationAnchorCell == new SurfaceCell(FaceId.Floor, 0, 0) &&
                track.DestinationLocalOffset.X.RawValue == state.localOffset.X.RawValue), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_ReleaseInput_HoldsCurrentLocalPoint()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var before = worldState.CreateSnapshot();
            Assert.That(before.TryGetUnitContinuousLocomotionState(10, out var moving), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var after = worldState.CreateSnapshot();

            Assert.That(after.TryGetUnitContinuousLocomotionState(10, out var idle), Is.True);
            Assert.That(idle.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(idle.velocity, Is.EqualTo(KinematicVelocity2.Zero));
            Assert.That(idle.localOffset, Is.EqualTo(moving.localOffset));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RightOffset_ThenUpInput_MovesImmediatelyUp()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var rightSnapshot = worldState.CreateSnapshot();
            Assert.That(rightSnapshot.TryGetUnitContinuousLocomotionState(10, out var rightState), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var upSnapshot = worldState.CreateSnapshot();

            Assert.That(upSnapshot.TryGetUnitContinuousLocomotionState(10, out var upState), Is.True);
            Assert.That(upState.localOffset.X.RawValue, Is.EqualTo(rightState.localOffset.X.RawValue));
            Assert.That(upState.localOffset.Y.RawValue, Is.GreaterThan(0));
            Assert.That(upState.lastMoveDirection, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RightOffset_ThenLeftInput_MovesBackImmediately()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var rightSnapshot = worldState.CreateSnapshot();
            Assert.That(rightSnapshot.TryGetUnitContinuousLocomotionState(10, out var rightState), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Left)));
            var leftSnapshot = worldState.CreateSnapshot();

            Assert.That(leftSnapshot.TryGetUnitContinuousLocomotionState(10, out var leftState), Is.True);
            Assert.That(leftState.localOffset.X.RawValue, Is.LessThan(rightState.localOffset.X.RawValue));
            Assert.That(leftState.lastMoveDirection, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_CrossHalfBoundary_NormalizesAnchor()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_ApproachBox_ClampsAtBoundary()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_UnitOverlap_DoesNotBlock()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_LocalNonZero_PushFlipRejected()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_WallClamp()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            TickResult result = null;
            for (var tick = 1; tick <= 10; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("Free2DContinuousBlocked") &&
                    reason.Contains("TraversalBlocked")),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TerrainClamp()
        {
            var terrain = new GameplayTerrainData(
                new[]
                {
                    new TerrainCellState(
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        TerrainKind.Generic,
                        TerrainFlags.BlocksGroundTraversal),
                });
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                terrain);
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyEdge_ClampsOrRejects()
        {
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(player.position.face, Is.EqualTo(FaceId.Floor));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_BeforeAnchorBoundary_NoEnemyContact()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                new SameCellPassiveContactProbeLogic(40, 10));

            TickResult result = null;
            for (var tick = 1; tick <= 9; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.LessThan(KinematicFixed.HalfCellUnits));
            Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_AfterAnchorBoundary_EnemyContactPossible()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                new TickGatedPassiveContactProbeLogic(40, 10, firstTick: 10));

            TickResult result = null;
            for (var tick = 1; tick <= 10; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_LocalZero_PushFlipAllowed()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreatePipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10), Is.True);
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.PlayerActionSignals.Any(signal =>
                signal.EntityId == 10 &&
                signal.ActiveActionKind == PlayerActionKind.Push &&
                signal.StartedThisTick), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_LocalNonZero_ActionPreviewRejected()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(
                UnitSpatialQuery.TryResolveSettledProbeCell(snapshot, 10, Direction.Right, out var result),
                Is.False);
            Assert.That(result.RejectedBy, Is.EqualTo(UnitProbeRejectionReason.NotSettledAtAnchor));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_HitNonlethal_PreservesPose()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                new TickScriptedAttackLogic(40, 10, attackTick: 2));

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var preHitSnapshot = worldState.CreateSnapshot();
            Assert.That(preHitSnapshot.TryGetUnitContinuousLocomotionState(10, out var preHitState), Is.True);
            var hitResult = pipeline.RunTick(new TickInput(2));

            var hitSnapshot = worldState.CreateSnapshot();
            Assert.That(hitSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.hp, Is.EqualTo(2));
            Assert.That(hitSnapshot.TryGetUnitContinuousLocomotionState(10, out var interrupted), Is.True);
            Assert.That(interrupted.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(interrupted.velocity.IsZero, Is.True);
            Assert.That(interrupted.localOffset, Is.EqualTo(preHitState.localOffset));
            Assert.That(
                hitResult.PresentationData.ContinuousLocomotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.TerminalKind == TickKinematicMotionTerminalKind.Interrupted &&
                    track.DestinationLocalOffset.Equals(preHitState.localOffset)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_HitLethal_RemovedTerminalPreservesPose()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, hp: 1),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                new TickScriptedAttackLogic(40, 10, attackTick: 2));

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var preHitSnapshot = worldState.CreateSnapshot();
            Assert.That(preHitSnapshot.TryGetUnitContinuousLocomotionState(10, out var preHitState), Is.True);
            var hitResult = pipeline.RunTick(new TickInput(2));

            var hitSnapshot = worldState.CreateSnapshot();
            Assert.That(hitSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(hitSnapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            Assert.That(
                hitResult.EventLog.Any(entry =>
                    entry.Contains("ContinuousLocomotionPoseRemoved|E=10") &&
                    entry.Contains($"Offset=({preHitState.localOffset.X.RawValue},{preHitState.localOffset.Y.RawValue})")),
                Is.True);
            Assert.That(
                hitResult.PresentationData.ContinuousLocomotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.TerminalKind == TickKinematicMotionTerminalKind.Removed &&
                    track.DestinationLocalOffset.Equals(preHitState.localOffset)),
                Is.True);
            Assert.That(
                hitResult.PresentationData.PlayerDeathHoldSignals.Any(signal =>
                    signal.EntityId == 10 &&
                    signal.StartedThisTick),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_FlagOff_ExistingKinematicBaseline()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerStoppableKinematicLocomotionEnabled);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_DoesNotAffectEnemyOrCharge()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 2, 0), teamId: 2));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(40, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            return CreatePipeline(worldState, extraLogics: null);
        }

        private static TickPipeline CreatePipeline(WorldState worldState, params IEntityLogic[] extraLogics)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
        }

        private static IEntityLogic[] CreatePlayerLogics(params IEntityLogic[] extraLogics)
        {
            return new IEntityLogic[]
            {
                new PlayerLogic(10),
                new PlayerControlStateLogic(10),
            }.Concat(extraLogics ?? Enumerable.Empty<IEntityLogic>()).ToArray();
        }

        private static WorldState CreateWorldState(params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities, GameplayTerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, terrainData);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> entities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, boardBounds, terrainData, topology);
        }

        private static bool HasAcceptedPassiveContact(TickResult result, int sourceId, int targetId)
        {
            return result.AttackPhaseResult.DamageResolutions.Any(
                record => record.Accepted &&
                          record.SourceId == sourceId &&
                          record.TargetId == targetId &&
                          record.SourceKind == AttackSourceKind.PassiveContact);
        }

        private static EntityState CreatePlayer(int entityId, int hp = 3)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, int teamId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return CreateBox(entityId, position, BoxCapabilities.None);
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                boxCapabilities = capabilities,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private sealed class TickScriptedAttackLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _attackTick;
            private readonly int _sourceId;
            private readonly int _targetId;

            public TickScriptedAttackLogic(int sourceId, int targetId, int attackTick)
            {
                _sourceId = sourceId;
                _targetId = targetId;
                _attackTick = attackTick;
            }

            public int ControlledEntityId => _sourceId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (input.TickIndex == _attackTick)
                {
                    buffer.Add(new RawAttackIntent(_sourceId, priority: 50, _targetId));
                }
            }
        }

        private sealed class SameCellPassiveContactProbeLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _sourceId;
            private readonly int _targetId;

            public SameCellPassiveContactProbeLogic(int sourceId, int targetId)
            {
                _sourceId = sourceId;
                _targetId = targetId;
            }

            public int ControlledEntityId => _sourceId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                buffer.Add(new RawAttackIntent(
                    _sourceId,
                    priority: 50,
                    _targetId,
                    AttackSourceKind.PassiveContact,
                    localSequence: 1));
            }
        }

        private sealed class TickGatedPassiveContactProbeLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _firstTick;
            private readonly int _sourceId;
            private readonly int _targetId;

            public TickGatedPassiveContactProbeLogic(int sourceId, int targetId, int firstTick)
            {
                _sourceId = sourceId;
                _targetId = targetId;
                _firstTick = firstTick;
            }

            public int ControlledEntityId => _sourceId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (input.TickIndex < _firstTick)
                {
                    return;
                }

                buffer.Add(new RawAttackIntent(
                    _sourceId,
                    priority: 50,
                    _targetId,
                    AttackSourceKind.PassiveContact,
                    localSequence: 1));
            }
        }
    }
}
