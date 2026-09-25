using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class EnemyGlideExecutorContractTests
    {
        private const int PlayerId = 10;
        private const int SourceId = 40;

        [Test]
        [Category("Extended")]
        public void SuppressionQueryRequiresRuntimeAndStateAndDoesNotMutateEither()
        {
            Assert.That(EnemyGlideExecutor.ShouldSuppressMovement(null, SourceId, null), Is.False);

            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(0, 1, 3, 1, 0));
            try
            {
                var definition = profile.CreateRuntimeDefinition(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                Assert.That(definition.TryGetGlideBehavior(out var glide), Is.True);
                var world = CreateWorld();
                Assert.That(EnemyGlideExecutor.ShouldSuppressMovement(
                    world.CreateSnapshot(), SourceId, glide), Is.False);
                foreach (var phase in new[] { EnemyGlidePhase.Windup, EnemyGlidePhase.Recovery,
                             EnemyGlidePhase.Active, EnemyGlidePhase.Cooldown })
                {
                    world.CreateWriteContext().SetEnemyGlideState(SourceId, State(phase));
                    var before = world.CreateSnapshot();
                    var expected = phase == EnemyGlidePhase.Windup ||
                                   phase == EnemyGlidePhase.Recovery;
                    Assert.That(EnemyGlideExecutor.ShouldSuppressMovement(before, SourceId, glide),
                        Is.EqualTo(expected));
                    Assert.That(EnemyGlideExecutor.ShouldSuppressMovement(before, SourceId, glide),
                        Is.EqualTo(expected));
                    Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(SourceId, out var after), Is.True);
                    Assert.That(after.Phase, Is.EqualTo(phase));
                    Assert.That(after.Sequence, Is.EqualTo(7));
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void ZeroWindupStartCallsStrategiesAtOriginalTwoBoundariesWithCurrentDefinitions()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(0, 0, 3, 1, 0));
            try
            {
                var definition = profile.CreateRuntimeDefinition(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                Assert.That(definition.TryGetGlideBehavior(out var glide), Is.True);
                var world = CreateWorld();
                Assert.That(world.CreateSnapshot().TryGetEntity(SourceId, out var source), Is.True);
                var detection = new RecordingDetection();
                var chase = new RecordingChase();
                IReadOnlyList<TileFeatureRuntimeDefinition> definitions =
                    new TileFeatureRuntimeDefinition[0];
                var updates = new List<string>();
                var tick = new TickInput(1);

                EnemyGlideExecutor.Commit(world.CreateSnapshot(), in tick, source, SourceId,
                    glide, detection, chase, definition.CommonSettings,
                    definition.DetectionSettings, definition.ChaseSettings,
                    definitions, world.CreateWriteContext(), updates);

                Assert.That(detection.Policies, Is.EqualTo(new[]
                {
                    LineOfSightSolidBlockerPolicy.BlockSolid,
                    LineOfSightSolidBlockerPolicy.IgnoreSolid,
                }));
                Assert.That(chase.Definitions, Has.Count.EqualTo(2));
                Assert.That(chase.Definitions.TrueForAll(item => ReferenceEquals(item, definitions)), Is.True);
                Assert.That(updates, Has.Count.EqualTo(2));
                Assert.That(updates[0], Does.Contain("|Label=Start|"));
                Assert.That(updates[1], Does.Contain("|Label=EnterActive|"));
                Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(SourceId, out var state), Is.True);
                Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(state.LockedTargetEntityId, Is.EqualTo(PlayerId));
                Assert.That(state.LockedStepX, Is.EqualTo(1));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void ActiveEntryReadsSourceButWritesControlledEntityId()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(0, 1, 3, 1, 0));
            try
            {
                var definition = profile.CreateRuntimeDefinition(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                Assert.That(definition.TryGetGlideBehavior(out var glide), Is.True);
                var world = CreateWorld(includeSecondEnemy: true);
                world.CreateWriteContext().SetEnemyGlideState(50, State(EnemyGlidePhase.Windup));
                Assert.That(world.CreateSnapshot().TryGetEntity(SourceId, out var source), Is.True);
                var detection = new RecordingDetection();
                var chase = new RecordingChase();
                var updates = new List<string>();
                var tick = new TickInput(2);

                EnemyGlideExecutor.Commit(world.CreateSnapshot(), in tick, source, 50,
                    glide, detection, chase, definition.CommonSettings,
                    definition.DetectionSettings, definition.ChaseSettings,
                    Array.Empty<TileFeatureRuntimeDefinition>(), world.CreateWriteContext(), updates);

                Assert.That(detection.Policies, Is.EqualTo(new[]
                {
                    LineOfSightSolidBlockerPolicy.IgnoreSolid,
                }));
                Assert.That(chase.Definitions, Has.Count.EqualTo(1));
                Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(SourceId, out _), Is.False);
                Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(50, out var state), Is.True);
                Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(state.LockedTargetEntityId, Is.EqualTo(PlayerId));
                Assert.That(state.LockedStepX, Is.EqualTo(1));
                Assert.That(updates, Has.Count.EqualTo(1));
                Assert.That(updates[0], Does.StartWith("EnemyGlideStateUpdated|E=50|Label=EnterActive|"));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static EnemyGlideRuntimeState State(EnemyGlidePhase phase) =>
            EnemyGlideRuntimeState.Create(phase, sequence: 7,
                windupUntilTickExclusive: 2, activeUntilTickExclusive: 5,
                recoveryUntilTickExclusive: 6, cooldownUntilTickExclusive: 7,
                windupTicks: 1, durationTicks: 3, recoveryTicks: 1,
                cooldownTicks: 1, lastExitedTick: 0, hasLockedStep: true,
                lockedStepX: -1, lockedTargetEntityId: 99);

        private static WorldState CreateWorld(bool includeSecondEnemy = false)
        {
            var entities = new List<EntityState>
            {
                Unit(PlayerId, 1, 3, 0, EnemyAiMode.None),
                Unit(SourceId, 2, 0, 0, EnemyAiMode.Chase),
            };
            if (includeSecondEnemy) entities.Add(Unit(50, 2, 3, 2, EnemyAiMode.Chase));
            return GameplayWorldStateTestFactory.CreateBounded(entities);
        }

        private static EntityState Unit(int id, int team, int x, int y, EnemyAiMode mode) =>
            new EntityState
            {
                entityId = id,
                position = new SurfaceCell(FaceId.Floor, x, y),
                hp = 3,
                maxHp = 3,
                teamId = team,
                type = EntityType.Unit,
                unitRole = team == 1 ? UnitRole.Player : UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = mode,
            };

        private sealed class RecordingDetection : IDetectionStrategy
        {
            public readonly List<LineOfSightSolidBlockerPolicy> Policies = new();

            public bool TryFindTarget(WorldSnapshot snapshot, in EntityState source,
                in DetectionSettings settings, out EntityState target,
                EnemyDetectionQueryOptions options = default)
            {
                Policies.Add(options.SolidBlockerPolicy);
                return snapshot.TryGetEntity(PlayerId, out target);
            }
        }

        private sealed class RecordingChase : IChaseStrategy
        {
            public readonly List<IReadOnlyList<TileFeatureRuntimeDefinition>> Definitions = new();

            public bool TryBuildMovementIntent(WorldSnapshot snapshot, in EntityState source,
                in EntityState target, in EnemyAiCommonSettings commonSettings,
                in ChaseSettings settings,
                IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
                out RawMovementIntent intent, Direction? excludedDirection = null)
            {
                Definitions.Add(tileFeatureDefinitions);
                intent = default;
                return false;
            }
        }
    }
}
