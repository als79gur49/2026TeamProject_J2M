using System.Linq;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class PlayerKinematicLocomotionReplayTests
    {
        [Test]
        [Category("Extended")]
        public void Replay_PlayerSameFaceContinuousLocomotion_FlagOn_IsDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(2),
                new TickInput(3),
                new TickInput(4),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);
            var secondReplay = harness.Run(
                CreateWorldState(),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            Assert.That(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray()));
            Assert.That(firstReplay[0].DeterminismHash, Is.Not.EqualTo(firstReplay[3].DeterminismHash));
        }

        [Test]
        [Category("Extended")]
        public void Held_ReplayDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(2),
                new TickInput(3),
                new TickInput(4, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(5, PlayerTickCommand.Move(Direction.Right)),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerStoppableKinematicLocomotionEnabled);
            var secondReplay = harness.Run(
                CreateWorldState(),
                CreatePlayerLogics(),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerStoppableKinematicLocomotionEnabled);

            Assert.That(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(
                firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray()));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("Mode=Held"));
            Assert.That(firstReplay[3].EventLogDump, Does.Contain("Mode=Voluntary"));
            Assert.That(firstReplay[3].DeterminismHash, Is.Not.EqualTo(firstReplay[2].DeterminismHash));
        }

        [Test]
        [Category("Extended")]
        public void Replay_PlayerSameFaceContinuousLocomotion_MidMotionNonlethalHit_HashesInterruptedThenOmitSettledZero()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(2),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10, hp: 3),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2)),
                CreatePlayerLogics(new TickScriptedAttackLogic(40, 10, attackTick: 1)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);
            var secondReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10, hp: 3),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2)),
                CreatePlayerLogics(new TickScriptedAttackLogic(40, 10, attackTick: 1)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            Assert.That(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(firstReplay[0].DeterminismHash, Is.Not.EqualTo(firstReplay[1].DeterminismHash));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("KinematicMotionInterrupted|E=10"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("KinematicInterruptClosed|E=10"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=2"));
        }

        [Test]
        [Category("Extended")]
        public void Replay_PlayerSameFaceContinuousLocomotion_MidMotionLethalHit_RemovalPurgesKinematicState()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10, hp: 1),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2)),
                CreatePlayerLogics(new TickScriptedAttackLogic(40, 10, attackTick: 1)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);
            var secondReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10, hp: 1),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2)),
                CreatePlayerLogics(new TickScriptedAttackLogic(40, 10, attackTick: 1)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            Assert.That(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(firstReplay[0].FinalEntitiesDump, Does.Not.Contain("E=10|"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("KinematicMotionInterrupted|E=10"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("KinematicPoseRemoved|E=10"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("PlayerRespawnDelayStarted|E=10"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("CleanupRemoved|E=10"));
        }

        [Test]
        [Category("Extended")]
        public void Replay_PlayerSameFaceContinuousLocomotion_MidMotionLethalHit_RespawnDelayOrderIsDeterministic()
        {
            var inputs = new[]
            {
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new TickInput(2),
            };
            var harness = new TickReplayHarness();

            var firstReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10, hp: 1),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2)),
                CreatePlayerLogics(new TickScriptedAttackLogic(40, 10, attackTick: 1)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);
            var secondReplay = harness.Run(
                CreateWorldState(
                    CreatePlayer(10, hp: 1),
                    CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2)),
                CreatePlayerLogics(new TickScriptedAttackLogic(40, 10, attackTick: 1)),
                inputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            Assert.That(
                firstReplay.Select(frame => frame.DeterminismHash).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("KinematicMotionInterrupted|E=10"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("KinematicPoseRemoved|E=10"));
            Assert.That(firstReplay[0].EventLogDump, Does.Contain("PlayerRespawnDelayStarted|E=10"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("PlayerRespawnDelayElapsed|E=10"));
            Assert.That(firstReplay[1].EventLogDump, Does.Contain("RespawnCommitted|E=10"));
            Assert.That(firstReplay[1].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=3"));
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
            return GameplayWorldStateTestFactory.CreateBounded(
                entities.Length == 0
                    ? new[] { CreatePlayer(10, hp: 3) }
                    : entities);
        }

        private static EntityState CreatePlayer(int entityId, int hp)
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
                System.Collections.Generic.List<RawAttackIntent> buffer)
            {
                if (input.TickIndex == _attackTick)
                {
                    buffer.Add(new RawAttackIntent(_sourceId, priority: 50, _targetId));
                }
            }
        }
    }
}
