using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class EnemyKinematicLocomotionReplayTests
    {
        [Test]
        [Category("Extended")]
        public void Replay_EnemySameFaceContinuousLocomotion_PassiveContactAtCommit_IsDeterministic()
        {
            var inputs = Enumerable.Range(1, 10)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = EnemyAiProfileTestFactory.CreateContactDamage();
            var harness = new TickReplayHarness();

            try
            {
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateContactWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateContactWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);

                AssertEquivalentReplayOutputs(firstReplay, secondReplay);
                Assert.That(firstReplay[8].EventLogDump, Does.Not.Contain("DamageCommitted"));
                Assert.That(firstReplay[9].EventLogDump, Does.Contain("DamageCommitted"));
                Assert.That(firstReplay[9].EventLogDump, Does.Contain("SourceKind=PassiveContact"));
                Assert.That(firstReplay[9].EventLogDump, Does.Contain("Target=10"));
                Assert.That(firstReplay[9].FinalEntitiesDump, Does.Contain("E=40|Pos=(0,0)|Hp=3"));
                Assert.That(firstReplay[9].FinalEntitiesDump, Does.Contain("E=10|Pos=(0,0)|Hp=2"));
                Assert.That(firstReplay[9].Trace, Does.Contain("KinematicAnchorCommitted"));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Replay_EnemySameFaceContinuousLocomotion_MidMotionDeath_RemovesKinematicStateDeterministically()
        {
            var inputs = new[]
            {
                new TickInput(1),
            };
            var profile = EnemyAiProfileTestFactory.CreateContactDamage();
            var harness = new TickReplayHarness();

            try
            {
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateDeathWorldState(),
                    new IEntityLogic[] { new ScriptedAttackLogic(10, 40) },
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateDeathWorldState(),
                    new IEntityLogic[] { new ScriptedAttackLogic(10, 40) },
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);

                AssertEquivalentReplayOutputs(firstReplay, secondReplay);
                Assert.That(firstReplay[0].FinalEntitiesDump, Does.Not.Contain("E=40|"));
                Assert.That(firstReplay[0].EventLogDump, Does.Contain("KinematicPoseRemoved|E=40"));
                Assert.That(firstReplay[0].EventLogDump, Does.Contain("CleanupRemoved|E=40"));
                Assert.That(firstReplay[0].EventLogDump, Does.Not.Contain("PlayerRespawnDelayStarted|E=40"));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static void AssertEquivalentReplayOutputs(
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
        }

        private static WorldState CreateContactWorldState()
        {
            return GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 1, 0)),
            });
        }

        private static WorldState CreateDeathWorldState()
        {
            return GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateEnemy(40, hp: 1, new SurfaceCell(FaceId.Floor, 1, 0)),
            });
        }

        private static EntityState CreatePlayer(int entityId, int hp, SurfaceCell position)
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

        private static EntityState CreateEnemy(int entityId, int hp, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                aiMode = EnemyAiMode.Chase,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private sealed class ScriptedAttackLogic : IAttackEntityLogic
        {
            private readonly int _sourceId;
            private readonly int _targetId;

            public ScriptedAttackLogic(int sourceId, int targetId)
            {
                _sourceId = sourceId;
                _targetId = targetId;
            }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (snapshot.TryGetEntity(_sourceId, out _) &&
                    snapshot.TryGetEntity(_targetId, out _))
                {
                    buffer.Add(new RawAttackIntent(_sourceId, priority: 100, _targetId));
                }
            }
        }
    }
}
