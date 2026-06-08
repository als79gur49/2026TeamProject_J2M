using System;
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

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class PlayerContinuousLocomotionReplayTests
    {
        [Test]
        [Category("Extended")]
        public void Replay_PlayerFree2D_StopTurnClamp_IsDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(2, PlayerTickCommand.None),
                new TickInput(3, PlayerTickCommand.Move(Direction.Up)),
                new TickInput(4, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(5, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(6, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(7, PlayerTickCommand.Move(Direction.Right)),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 0))),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
            var secondReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 0))),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay[0].DeterminismHash, Is.Not.EqualTo(firstReplay[1].DeterminismHash));
            Assert.That(firstReplay[firstReplay.Count - 1].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3"));
        }

        [Test]
        [Category("Extended")]
        public void Replay_PlayerFree2D_RadiusApproachBlocker_IsDeterministic()
        {
            var inputs = Enumerable.Range(1, 10)
                .Select(tick => new TickInput(tick, PlayerTickCommand.Move(Direction.Right)))
                .ToArray();
            var harness = new TickReplayHarness();
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerContinuousLocomotion = new PlayerContinuousLocomotionSettings
            {
                CollisionRadiusCells = 0.1875f,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);

            var firstReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 0))),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled,
                playerContinuousLocomotion: playerContinuousLocomotion);
            var secondReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 0))),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled,
                playerContinuousLocomotion: playerContinuousLocomotion);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay[firstReplay.Count - 1].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3"));
        }

        [Test]
        [Category("Extended")]
        public void Replay_PlayerFree2D_AnchorNormalizeContact_IsDeterministic()
        {
            var inputs = Enumerable.Range(1, 10)
                .Select(tick => new TickInput(tick, PlayerTickCommand.Move(Direction.Right)))
                .ToArray();
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2)),
                CreatePlayerLogics(new TickGatedPassiveContactProbeLogic(40, 10, firstTick: 9)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
            var secondReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2)),
                CreatePlayerLogics(new TickGatedPassiveContactProbeLogic(40, 10, firstTick: 9)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay[8].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3"));
            Assert.That(firstReplay[9].FinalEntitiesDump, Does.Contain("E=10|Pos=(1,0)|Hp=2"));
            Assert.That(firstReplay[9].EventLogDump, Does.Contain("ContinuousLocomotionInterrupted|E=10"));
        }

        [Test]
        [Category("Extended")]
        public void Replay_PlayerFree2D_TopologyHandoff_IsDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Up)),
            };
            var harness = new TickReplayHarness();
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));

            var firstReplay = harness.Run(
                CreateWorldState(
                    new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                    boardBounds,
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            var secondReplay = harness.Run(
                CreateWorldState(
                    new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                    boardBounds,
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Not.Contain("Pos=(0,1)"));
            Assert.That(firstReplay[0].OccupancyDump, Does.Not.Contain("Cell=(0,1)|E=10|Face=Floor"));
        }

        [Test]
        [Category("Extended")]
        public void Replay_Free2DTopology_WithLateralOffset_Deterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Up)),
            };
            var harness = new TickReplayHarness();
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));

            var firstWorld = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(firstWorld, 256, KinematicFixed.MaxPositiveLocalOffset);
            Assert.That(firstWorld.CreateSnapshot().TryGetUnitContinuousLocomotionState(10, out var firstPreState), Is.True);
            Assert.That(firstPreState.localOffset.X.RawValue, Is.GreaterThan(0));
            var secondWorld = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(secondWorld, 256, KinematicFixed.MaxPositiveLocalOffset);
            Assert.That(secondWorld.CreateSnapshot().TryGetUnitContinuousLocomotionState(10, out var secondPreState), Is.True);
            Assert.That(secondPreState.localOffset.X.RawValue, Is.GreaterThan(0));

            var firstReplay = harness.Run(
                firstWorld,
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DNativeTopologyTransitionEnabled);
            var secondReplay = harness.Run(
                secondWorld,
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DNativeTopologyTransitionEnabled);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay[0].Trace, Does.Contain("Free2DTopologyNativeTransition"));
            Assert.That(firstReplay[0].OccupancyDump, Does.Contain("Face=Front"));
        }

        [Test]
        [Category("Core")]
        public void Replay_PlayerFree2D_TopologyApproachHandoff_IsDeterministic()
        {
            var inputs = Enumerable.Range(1, 20)
                .Select(tick => new TickInput(tick, PlayerTickCommand.Move(Direction.Up)))
                .ToArray();
            var harness = new TickReplayHarness();
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));

            var firstReplay = harness.Run(
                CreateWorldState(
                    new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    boardBounds,
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            var secondReplay = harness.Run(
                CreateWorldState(
                    new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    boardBounds,
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay.Any(frame => !frame.FinalEntitiesDump.Contains("Pos=(0,0)")), Is.True);
            Assert.That(firstReplay.Any(frame => frame.EventLogDump.Contains("LegacyUnitOrdinaryMovementDetected")), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase2_PlayerDefaultGameplayLocomotion_NoLegacyFallback()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(2, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(3, PlayerTickCommand.None),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(CreatePlayer(10)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            var secondReplay = harness.Run(
                CreateWorldState(CreatePlayer(10)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            Assert.That(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.EventLogDump).ToArray()));
            Assert.That(firstReplay.Any(frame => frame.Trace.Contains("Boundary=LegacyFallback", StringComparison.Ordinal)), Is.False);
            Assert.That(firstReplay.Any(frame => frame.EventLogDump.Contains("LegacyUnitOrdinaryMovementDetected", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Replay_MoveOwnership_NoCoveredFallbackMove()
        {
            Replay_Phase2_PlayerDefaultGameplayLocomotion_NoLegacyFallback();
        }

        [Test]
        [Category("Extended")]
        // Historical/pre-Phase4 canary: delegates to the canonical player removed-diagnostic replay.
        public void Replay_Phase2_PlayerFlagOffLegacyFallback_BaselineDocumented()
        {
            Replay_Phase4_LegacyBaseline_PlayerFallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase3_None_NoCoveredLegacyFallback()
        {
            var inputs = new[] { new TickInput(1, PlayerTickCommand.Move(Direction.Right)) };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(CreatePlayer(10)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);
            var secondReplay = harness.Run(
                CreateWorldState(CreatePlayer(10)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            Assert.That(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.EventLogDump).ToArray()));
            Assert.That(firstReplay.Any(frame => frame.Trace.Contains("Boundary=LegacyFallback", StringComparison.Ordinal)), Is.False);
            Assert.That(
                firstReplay.Any(frame =>
                    frame.Trace.Contains(LegacyMovementBoundaryAssert.ExplicitLegacyFallbackRequiredReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(LegacyMovementBoundaryAssert.ExplicitLegacyFallbackRequiredReason, StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase4_LegacyBaseline_PlayerFallbackRemoved()
        {
            Replay_Phase7_PlayerLegacyFallbackBaseline_DiagnosticCompatibility();
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase7_PlayerLegacyFallbackBaseline_DiagnosticCompatibility()
        {
            Replay_Phase8C_PlayerRemovedDiagnosticBaseline_DiagnosticCompatibility();
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase8C_PlayerRemovedDiagnosticBaseline_DiagnosticCompatibility()
        {
            var inputs = new[] { new TickInput(1, PlayerTickCommand.Move(Direction.Right)) };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldStateWithPlayerControl(CreatePlayer(10)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);
            var secondReplay = harness.Run(
                CreateWorldStateWithPlayerControl(CreatePlayer(10)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);

            AssertReplayDeterministicAllowingLegacyDiagnostic(firstReplay, secondReplay);
            Assert.That(firstReplay.Any(frame => frame.Trace.Contains("Boundary=LegacyFallback", StringComparison.Ordinal)), Is.False);
            Assert.That(
                firstReplay.Any(frame =>
                    frame.Trace.Contains(LegacyMovementBoundaryAssert.PlayerLegacyFallbackRemovedReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(LegacyMovementBoundaryAssert.PlayerLegacyFallbackRemovedReason, StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_PlayerFree2D_HitDeath_IsDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(2),
                new TickInput(3),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10, hp: 1),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2)),
                CreatePlayerLogics(new TickScriptedAttackLogic(40, 10, attackTick: 2)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
            var secondReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10, hp: 1),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2)),
                CreatePlayerLogics(new TickScriptedAttackLogic(40, 10, attackTick: 2)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("ContinuousLocomotionInterrupted|E=10"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("ContinuousLocomotionPoseRemoved|E=10"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("PlayerRespawnDelayStarted|E=10"));
            Assert.That(firstReplay[2].EventLogDump, Does.Contain("RespawnCommitted|E=10"));
            Assert.That(firstReplay[2].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3"));
        }

        [Test]
        [Category("Extended")]
        public void Replay_Free2DActionAssist_QueueAlignExecute_IsDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                new TickInput(2, PlayerTickCommand.None),
                new TickInput(3, PlayerTickCommand.None),
                new TickInput(4, PlayerTickCommand.None),
                new TickInput(5, PlayerTickCommand.None),
                new TickInput(6, PlayerTickCommand.None),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldStateWithPlayerOffset(
                    512,
                    0,
                    CreatePlayer(10),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DActionAssistEnabled);
            var secondReplay = harness.Run(
                CreateWorldStateWithPlayerOffset(
                    512,
                    0,
                    CreatePlayer(10),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DActionAssistEnabled);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay.Any(frame => frame.PlayerControlDump.Contains("QueuedFree2DAction=Push")), Is.True);
            Assert.That(firstReplay.Any(frame => frame.PlayerControlDump.Contains("Action=Push")), Is.True);
            Assert.That(firstReplay.Any(frame => frame.Trace.Contains("Free2DActionAssistQueued")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Free2DActionAssist_OutsideWindowReject_IsDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                new TickInput(2, PlayerTickCommand.None),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldStateWithPlayerOffset(
                    513,
                    0,
                    CreatePlayer(10),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DActionAssistEnabled);
            var secondReplay = harness.Run(
                CreateWorldStateWithPlayerOffset(
                    513,
                    0,
                    CreatePlayer(10),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DActionAssistEnabled);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay.Any(frame => frame.PlayerControlDump.Contains("QueuedFree2DAction=Push")), Is.False);
            Assert.That(firstReplay.Any(frame => frame.Trace.Contains("Reason=OutsideSettleWindow")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Free2DActionAssist_NoCandidateReject_IsDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                new TickInput(2, PlayerTickCommand.None),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldStateWithPlayerOffset(
                    512,
                    0,
                    CreatePlayer(10)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DActionAssistEnabled);
            var secondReplay = harness.Run(
                CreateWorldStateWithPlayerOffset(
                    512,
                    0,
                    CreatePlayer(10)),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DActionAssistEnabled);

            AssertReplayEqual(firstReplay, secondReplay);
            Assert.That(firstReplay.Any(frame => frame.PlayerControlDump.Contains("QueuedFree2DAction=Push")), Is.False);
            Assert.That(firstReplay.Any(frame => frame.PlayerControlDump.Contains("QueuedFree2DAction=Flip")), Is.False);
            Assert.That(firstReplay.Any(frame => frame.Trace.Contains("Reason=NoActionCandidate")), Is.True);
        }

        private static void AssertReplayEqual(
            IReadOnlyList<TickReplayFrame> firstReplay,
            IReadOnlyList<TickReplayFrame> secondReplay)
        {
            AssertReplayDeterministicAllowingLegacyDiagnostic(firstReplay, secondReplay);
            Assert.That(
                firstReplay.Any(frame => frame.Trace.Contains("LegacyUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                Is.False);
        }

        private static void AssertReplayDeterministicAllowingLegacyDiagnostic(
            IReadOnlyList<TickReplayFrame> firstReplay,
            IReadOnlyList<TickReplayFrame> secondReplay)
        {
            Assert.That(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.EventLogDump).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.EventLogDump).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.Trace).ToArray()));
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

        private static WorldState CreateWorldStateWithPlayerControl(params EntityState[] entities)
        {
            var worldState = CreateWorldState(entities);
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            return worldState;
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> entities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, boardBounds, terrainData, topology);
        }

        private static WorldState CreateWorldStateWithPlayerOffset(
            int localX,
            int localY,
            params EntityState[] entities)
        {
            var worldState = CreateWorldState(entities);
            SetPlayerContinuousLocalOffset(worldState, localX, localY);
            return worldState;
        }

        private static void SetPlayerContinuousLocalOffset(WorldState worldState, int localX, int localY)
        {
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new KinematicOffset2(
                        KinematicFixed.FromRaw(localX),
                        KinematicFixed.FromRaw(localY)),
                    velocity = KinematicVelocity2.Zero,
                    facing = Direction.Right,
                    lastMoveDirection = Direction.Right,
                    speedUnitsPerTick = 0,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
        }

        private static EntityState CreatePlayer(int entityId, int hp = 3)
        {
            return CreatePlayer(entityId, new SurfaceCell(FaceId.Floor, 0, 0), hp);
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position, int hp = 3)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
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
