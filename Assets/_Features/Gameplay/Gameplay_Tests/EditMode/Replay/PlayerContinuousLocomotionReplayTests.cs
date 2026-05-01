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

        private static WorldState CreateWorldStateWithPlayerOffset(
            int localX,
            int localY,
            params EntityState[] entities)
        {
            var worldState = CreateWorldState(entities);
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
            return worldState;
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
