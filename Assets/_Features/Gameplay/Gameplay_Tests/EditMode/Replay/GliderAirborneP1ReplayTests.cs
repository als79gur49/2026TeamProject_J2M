using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class GliderAirborneP1ReplayTests
    {
        [Test]
        [Category("Core")]
        public void Glider_ActiveUnitSolidOverlap_OrderedOccupancyExportStableAcrossTopologyChange()
        {
            var firstReplay = RunActiveSolidOverlapReplay();
            var secondReplay = RunActiveSolidOverlapReplay();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay.Any(frame =>
                frame.OccupancyDump.Contains("Layer=Solid|Cell=(1,0)|E=30|Face=Floor", StringComparison.Ordinal) &&
                frame.OccupancyDump.Contains("Layer=Unit|Cell=(1,0)|E=40|Face=Floor", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        [Category("Core")]
        public void Glider_ExpiredActiveOnSolid_LongRun_DeterminismStable()
        {
            var firstReplay = RunExpiredActiveOnSolidReplay();
            var secondReplay = RunExpiredActiveOnSolidReplay();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay.Any(frame =>
                frame.Trace.Contains("Phase=Active|Active=1|WantsRecover=1", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        [Category("Core")]
        public void Glider_TopologyChurn_DeterminismStable()
        {
            var first = RunTopologyChurnTrace();
            var second = RunTopologyChurnTrace();

            CollectionAssert.AreEqual(first.Hashes, second.Hashes);
            CollectionAssert.AreEqual(first.Traces, second.Traces);
            Assert.That(first.FinalState, Is.EqualTo(second.FinalState));
        }

        private static IReadOnlyList<TickReplayFrame> RunActiveSolidOverlapReplay()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 8, recoveryTicks: 1, cooldownTicks: 0));
            var harness = new TickReplayHarness();
            try
            {
                return harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateActiveSolidOverlapWorld(lockExecution: true),
                    entityLogics: Array.Empty<IEntityLogic>(),
                    Enumerable.Range(1, 4).Select(tick => new TickInput(tick)).ToArray(),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static IReadOnlyList<TickReplayFrame> RunExpiredActiveOnSolidReplay()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlidePatrol(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 1, recoveryTicks: 2, cooldownTicks: 0));
            var harness = new TickReplayHarness();
            try
            {
                return harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateExpiredActiveOnSolidWorld(),
                    entityLogics: Array.Empty<IEntityLogic>(),
                    Enumerable.Range(3, 8).Select(tick => new TickInput(tick)).ToArray(),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static TopologyChurnOutcome RunTopologyChurnTrace()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(initialDelayTicks: 0, windupTicks: 0, durationTicks: 3, recoveryTicks: 1, cooldownTicks: 0, glideMoveTicks: 6));
            try
            {
                var worldState = CreateActiveSolidOverlapWorld(glideMoveTicks: 6, activeUntilTickExclusive: 2, wantsRecover: true);
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(
                        worldState,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                        unitKinematicLocomotionTiming: CreateKinematicTiming(ticksPerCell: 6));
                var hashes = new List<string>();
                var traces = new List<string>();

                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Back));
                var suspended = pipeline.RunTick(new TickInput(3));
                hashes.Add(suspended.DeterminismHash);
                traces.Add(suspended.Trace.Text);

                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
                var resumed = pipeline.RunTick(new TickInput(4));
                hashes.Add(resumed.DeterminismHash);
                traces.Add(resumed.Trace.Text);

                return new TopologyChurnOutcome(hashes, traces, DumpGlider(worldState));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static WorldState CreateActiveSolidOverlapWorld(
            int glideMoveTicks = 2,
            int activeUntilTickExclusive = 8,
            bool wantsRecover = false,
            bool lockExecution = false)
        {
            var solidCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 4, 0)),
                    CreateWall(30, solidCell),
                    CreateEnemy(40, SurfaceCell.FromPlanar(Vector2Int.zero)),
                },
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(6, 6)));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                40,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 1,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: activeUntilTickExclusive,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 0,
                    durationTicks: Math.Max(1, activeUntilTickExclusive),
                    recoveryTicks: 1,
                    cooldownTicks: 0,
                    glideMoveTicks: glideMoveTicks,
                    lastExitedTick: 0,
                    wantsRecover: wantsRecover,
                    hasLockedStep: true,
                    lockedStepX: 1,
                    lockedStepY: 0,
                    lockedTargetEntityId: 10));
            writeContext.MoveEntity(40, solidCell);
            if (lockExecution)
            {
                writeContext.SetEntityExecutionLockState(
                    40,
                    new EntityExecutionLockState
                    {
                        phase = EntityExecutionPhase.Attack,
                        sequence = 1,
                        unlockTickExclusive = 20,
                    });
            }

            return worldState;
        }

        private static WorldState CreateExpiredActiveOnSolidWorld()
        {
            return CreateActiveSolidOverlapWorld(activeUntilTickExclusive: 2, wantsRecover: true);
        }

        private static void AssertEquivalentReplayOutputs(
            IReadOnlyList<TickReplayFrame> firstReplay,
            IReadOnlyList<TickReplayFrame> secondReplay)
        {
            Assert.That(firstReplay.Select(frame => frame.DeterminismHash).ToArray(), Is.EqualTo(secondReplay.Select(frame => frame.DeterminismHash).ToArray()));
            Assert.That(firstReplay.Select(frame => frame.Trace).ToArray(), Is.EqualTo(secondReplay.Select(frame => frame.Trace).ToArray()));
            Assert.That(firstReplay.Select(frame => frame.EventLogDump).ToArray(), Is.EqualTo(secondReplay.Select(frame => frame.EventLogDump).ToArray()));
            Assert.That(firstReplay.Select(frame => frame.FinalEntitiesDump).ToArray(), Is.EqualTo(secondReplay.Select(frame => frame.FinalEntitiesDump).ToArray()));
            Assert.That(firstReplay.Select(frame => frame.OccupancyDump).ToArray(), Is.EqualTo(secondReplay.Select(frame => frame.OccupancyDump).ToArray()));
        }

        private static string DumpGlider(WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            var entity = snapshot.TryGetEntity(40, out var glider) ? glider.position.ToString() : "missing";
            var glide = snapshot.TryGetEnemyGlideState(40, out var state)
                ? $"{state.Phase}|Wants={state.WantsRecover}|ActiveUntil={state.ActiveUntilTickExclusive}"
                : "none";
            return $"{entity}|{glide}";
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static EntityState CreateEnemy(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Chase,
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
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreatePlayerTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static UnitKinematicLocomotionTimingSnapshot CreateKinematicTiming(int ticksPerCell)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new UnitKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = ticksPerCell / (float)timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private sealed class TopologyChurnOutcome
        {
            public TopologyChurnOutcome(IReadOnlyList<string> hashes, IReadOnlyList<string> traces, string finalState)
            {
                Hashes = hashes;
                Traces = traces;
                FinalState = finalState;
            }

            public IReadOnlyList<string> Hashes { get; }

            public IReadOnlyList<string> Traces { get; }

            public string FinalState { get; }
        }
    }
}
