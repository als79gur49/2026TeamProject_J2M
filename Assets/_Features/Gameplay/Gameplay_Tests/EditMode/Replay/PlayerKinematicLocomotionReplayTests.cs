using System.Linq;
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

        private static IEntityLogic[] CreatePlayerLogics()
        {
            return new IEntityLogic[]
            {
                new PlayerLogic(10),
                new PlayerControlStateLogic(10),
            };
        }

        private static WorldState CreateWorldState()
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    new EntityState
                    {
                        entityId = 10,
                        position = new SurfaceCell(FaceId.Floor, 0, 0),
                        hp = 3,
                        maxHp = 3,
                        teamId = 1,
                        type = EntityType.Unit,
                        unitRole = UnitRole.Player,
                        facing = Direction.Right,
                        boardPresence = EntityBoardPresence.Occupying,
                    },
                });
        }
    }
}
