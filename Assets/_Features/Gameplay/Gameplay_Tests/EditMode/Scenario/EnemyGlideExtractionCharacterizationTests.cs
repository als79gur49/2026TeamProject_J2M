using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    // Public Logic/pipeline characterization: this fixture must also compile without EnemyGlideExecutor.
    public sealed class EnemyGlideExtractionCharacterizationTests
    {
        private const int PlayerId = 10;
        private const int EnemyId = 40;
        private const string ProductionProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_GlideChaser/EnemyAi_GlideChaser.asset";

        [Test]
        [Category("Extended")]
        public void G01_DelayAndPhaseBoundaryRecordsKeepExclusiveTicksAndSameTickRestartGuard()
        {
            foreach (var delay in new[] { 0, 1, 2 })
            {
                var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                    new EnemyGlideTimingSettings(delay, 0, 1, 0, 0));
                var world = CreateWorld();
                var logic = new EnemyLogic(EnemyId, profile);
                try
                {
                    for (var tick = 1; tick <= 3; tick++)
                    {
                        Commit(logic, world, "G01", $"delay-{delay}", $"tick-{tick}", tick);
                        var snapshot = world.CreateSnapshot();
                        if (delay == 2 && tick == 1)
                        {
                            Assert.That(snapshot.TryGetEnemyGlideState(EnemyId, out var delayed), Is.True);
                            Assert.That(delayed.InitialDelayTicksRemaining, Is.EqualTo(1));
                            Assert.That(delayed.Phase, Is.EqualTo(EnemyGlidePhase.Ready));
                        }
                        if (tick == (delay == 2 ? 2 : 1))
                        {
                            Assert.That(snapshot.TryGetEnemyGlideState(EnemyId, out var started), Is.True);
                            Assert.That(started.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                            Assert.That(started.Sequence, Is.EqualTo(1));
                        }
                        if (tick == (delay == 2 ? 3 : 2))
                        {
                            Assert.That(snapshot.TryGetEnemyGlideState(EnemyId, out var exited), Is.True);
                            Assert.That(exited.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                            Assert.That(exited.LastExitedTick, Is.EqualTo(tick));
                        }
                    }
                }
                finally
                {
                    EnemyAiProfileTestFactory.Destroy(profile);
                }
            }

            var phasedProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(0, 1, 2, 1, 2));
            var phasedWorld = CreateWorld();
            var phasedLogic = new EnemyLogic(EnemyId, phasedProfile);
            try
            {
                foreach (var tick in new[] { 1, 2, 3, 4, 5, 6, 7 })
                {
                    Commit(phasedLogic, phasedWorld, "G01", "nonzero-phases", $"tick-{tick}", tick);
                    var state = GetState(phasedWorld);
                    if (tick == 1) Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
                    if (tick == 2) Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                    if (tick == 4) Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                    if (tick == 5) Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                    if (tick == 7) Assert.That(state.Sequence, Is.EqualTo(2));
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(phasedProfile);
            }

            foreach (var mixed in new[]
                     {
                         new { Name = "zero-windup-cooldown", Timing = new EnemyGlideTimingSettings(0, 2, 1, 0) },
                         new { Name = "zero-recovery", Timing = new EnemyGlideTimingSettings(1, 2, 0, 1) },
                     })
            {
                var profile = EnemyAiProfileTestFactory.CreateGlideChaser(mixed.Timing);
                var world = CreateWorld();
                var logic = new EnemyLogic(EnemyId, profile);
                try
                {
                    for (var tick = 1; tick <= 5; tick++)
                    {
                        Commit(logic, world, "G01", mixed.Name, $"tick-{tick}", tick);
                        Assert.That(GetState(world).Sequence, Is.GreaterThanOrEqualTo(1));
                    }
                }
                finally
                {
                    EnemyAiProfileTestFactory.Destroy(profile);
                }
            }

            var patrolProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(2, 0, 2, 0, 0));
            var patrolWorld = CreateWorld(EnemyAiMode.Patrol);
            var patrolLogic = new EnemyLogic(EnemyId, patrolProfile);
            try
            {
                Commit(patrolLogic, patrolWorld, "G01", "patrol-delay", "tick-1", 1);
                Assert.That(GetState(patrolWorld).InitialDelayTicksRemaining, Is.EqualTo(1));
                patrolWorld.CreateWriteContext().ApplyEnemyAiState(EnemyId, EnemyAiMode.Chase, 0);
                Commit(patrolLogic, patrolWorld, "G01", "patrol-delay", "tick-2", 2);
                Assert.That(GetState(patrolWorld).Phase, Is.EqualTo(EnemyGlidePhase.Active));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(patrolProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G02_SuppressionQueryIsRepeatableAcrossPhasesAndFirstRelease()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(1, 3, 1, 1));
            try
            {
                foreach (var phase in new[] { EnemyGlidePhase.Windup, EnemyGlidePhase.Recovery,
                             EnemyGlidePhase.Active, EnemyGlidePhase.Cooldown })
                {
                    var world = CreateWorld();
                    var logic = new EnemyLogic(EnemyId, profile);
                    world.CreateWriteContext().SetEnemyGlideState(EnemyId, State(phase,
                        windupUntil: 5, activeUntil: 5, recoveryUntil: 5, cooldownUntil: 5));
                    var before = world.CreateSnapshot();
                    var first = Intents(logic, before, 2);
                    var second = Intents(logic, before, 2);
                    Assert.That(second.Select(x => x.Destination), Is.EqualTo(first.Select(x => x.Destination)));
                    Assert.That(first.Count == 0,
                        Is.EqualTo(phase == EnemyGlidePhase.Windup || phase == EnemyGlidePhase.Recovery));
                    EnemyGlideCapture.Logic("G02", phase.ToString(), "query-twice", 2,
                        before, world.CreateSnapshot(), new[] { EnemyId },
                        new EnemyGlideRuntimeState?[] { null }, Array.Empty<string>(), Array.Empty<string>(),
                        first, new[] { $"FirstCount={first.Count}", $"SecondCount={second.Count}" });
                }

                var absentWorld = CreateWorld();
                var absentLogic = new EnemyLogic(EnemyId, profile);
                var absent = absentWorld.CreateSnapshot();
                var absentIntents = Intents(absentLogic, absent, 2);
                Assert.That(absentIntents, Is.Not.Empty);
                EnemyGlideCapture.Logic("G02", "state-absent", "query", 2, absent,
                    absentWorld.CreateSnapshot(), new[] { EnemyId }, new EnemyGlideRuntimeState?[] { null },
                    Array.Empty<string>(), Array.Empty<string>(), absentIntents);

                foreach (var phase in new[] { EnemyGlidePhase.Windup, EnemyGlidePhase.Recovery })
                {
                    var world = CreateWorld();
                    var logic = new EnemyLogic(EnemyId, profile);
                    world.CreateWriteContext().SetEnemyGlideState(EnemyId, State(phase,
                        windupUntil: 2, recoveryUntil: 2));
                    Commit(logic, world, "G02", $"{phase}-release", "tick-2", 2);
                    var released = Intents(logic, world.CreateSnapshot(), 2);
                    Assert.That(released, Is.Not.Empty);
                    var snapshot = world.CreateSnapshot();
                    EnemyGlideCapture.Logic("G02", $"{phase}-release", "first-query", 2, snapshot,
                        snapshot, new[] { EnemyId }, new EnemyGlideRuntimeState?[] { null },
                        Array.Empty<string>(), Array.Empty<string>(), released,
                        new[] { $"FirstReleaseCount={released.Count}" });
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }

            var noModule = EnemyAiProfileTestFactory.CreateNonAttacking();
            try
            {
                var world = CreateWorld();
                world.CreateWriteContext().SetEnemyGlideState(EnemyId, State(EnemyGlidePhase.Windup,
                    windupUntil: 5));
                var logic = new EnemyLogic(EnemyId, noModule);
                var before = world.CreateSnapshot();
                var intents = Intents(logic, before, 2);
                Assert.That(intents, Is.Not.Empty);
                EnemyGlideCapture.Logic("G02", "module-absent", "query", 2, before,
                    world.CreateSnapshot(), new[] { EnemyId }, new EnemyGlideRuntimeState?[] { null },
                    Array.Empty<string>(), Array.Empty<string>(), intents);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(noModule);
            }
        }

        [Test]
        [Category("Extended")]
        public void G03_ActiveFreshDetectionIgnoresSolidAndFailedRefreshKeepsLockedValues()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                DetectionStrategyKind = DetectionStrategyKind.CrossLineOfSightOpponent,
                DetectionSettings = new DetectionSettings(8, requireSameFace: true,
                    canTargetMarkedForDeath: false),
                IncludeGlideBehaviorModule = true,
                GlideTimingSettings = EnemyGlideTimingAuthoringSettings.FromRuntimeSettings(
                    new EnemyGlideTimingSettings(1, 4, 1, 0),
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });
            try
            {
                foreach (var found in new[] { true, false })
                {
                    var world = CreateWorld(EnemyAiMode.Chase,
                        new[] { Wall(20, new SurfaceCell(FaceId.Floor, 1, 0)) });
                    var logic = new EnemyLogic(EnemyId, profile);
                    world.CreateWriteContext().SetEnemyGlideState(EnemyId,
                        State(EnemyGlidePhase.Windup, windupUntil: 2, lockedX: 0,
                            lockedY: 1, lockedTarget: 99));
                    if (!found) world.CreateWriteContext().RemoveEntity(PlayerId);
                    Commit(logic, world, "G03", found ? "refresh-success" : "refresh-failure",
                        "enter-active", 2);
                    var active = GetState(world);
                    Assert.That(active.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                    Assert.That(active.LockedTargetEntityId, Is.EqualTo(found ? PlayerId : 99));
                    if (!found)
                    {
                        Assert.That(active.LockedStepX, Is.Zero);
                        Assert.That(active.LockedStepY, Is.EqualTo(1));
                    }
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G03_StartDetectionAndStrategyFallbackPreserveFaceDistanceAndAxisPriority()
        {
            var timing = new EnemyGlideTimingSettings(0, 2, 1, 0);
            var lineProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                DetectionStrategyKind = DetectionStrategyKind.CrossLineOfSightOpponent,
                DetectionSettings = new DetectionSettings(8, requireSameFace: true,
                    canTargetMarkedForDeath: false),
                IncludeGlideBehaviorModule = true,
                GlideTimingSettings = EnemyGlideTimingAuthoringSettings.FromRuntimeSettings(
                    timing, GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });
            try
            {
                var world = CreateWorld(EnemyAiMode.Chase,
                    new[] { Wall(20, new SurfaceCell(FaceId.Floor, 1, 0)) });
                var logic = new EnemyLogic(EnemyId, lineProfile);
                Commit(logic, world, "G03", "start-normal-solid-blocked", "tick-1", 1);
                Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(EnemyId, out _), Is.False);
                world.CreateWriteContext().RemoveEntity(20);
                Commit(logic, world, "G03", "start-normal-unblocked", "tick-2", 2);
                Assert.That(GetState(world).LockedTargetEntityId, Is.EqualTo(PlayerId));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(lineProfile);
            }

            foreach (var axis in new[] { ChaseAxisPriorityMode.HorizontalFirst,
                         ChaseAxisPriorityMode.VerticalFirst,
                         ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak })
            {
                var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
                {
                    ChaseSettings = new ChaseSettings(axis, trySecondaryAxisWhenBlocked: false),
                    IncludeGlideBehaviorModule = true,
                    GlideTimingSettings = EnemyGlideTimingAuthoringSettings.FromRuntimeSettings(
                        timing, GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                });
                try
                {
                    var horizontal = axis != ChaseAxisPriorityMode.VerticalFirst;
                    var wallCell = horizontal
                        ? new SurfaceCell(FaceId.Floor, 1, 0)
                        : new SurfaceCell(FaceId.Floor, 0, 1);
                    var world = GameplayWorldStateTestFactory.CreateBounded(new[]
                    {
                        Unit(PlayerId, 1, 3, 3, EnemyAiMode.None),
                        Unit(EnemyId, 2, 0, 0, EnemyAiMode.Chase),
                        Wall(20, wallCell),
                    });
                    Commit(new EnemyLogic(EnemyId, profile), world, "G03",
                        "fallback-" + axis, "tick-1", 1);
                    var started = GetState(world);
                    Assert.That(started.LockedStepX, Is.EqualTo(horizontal ? 1 : 0));
                    Assert.That(started.LockedStepY, Is.EqualTo(horizontal ? 0 : 1));
                    Assert.That(started.LockedTargetEntityId, Is.EqualTo(PlayerId));
                }
                finally
                {
                    EnemyAiProfileTestFactory.Destroy(profile);
                }
            }

            foreach (var variant in new[]
                     {
                         (Name: "fallback-greatest-horizontal", TargetX: 4, TargetY: 2,
                             Horizontal: true),
                         (Name: "fallback-greatest-vertical", TargetX: 2, TargetY: 4,
                             Horizontal: false),
                     })
            {
                var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
                {
                    ChaseSettings = new ChaseSettings(
                        ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak,
                        trySecondaryAxisWhenBlocked: false),
                    IncludeGlideBehaviorModule = true,
                    GlideTimingSettings = EnemyGlideTimingAuthoringSettings.FromRuntimeSettings(
                        timing, GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                });
                try
                {
                    var wallCell = variant.Horizontal
                        ? new SurfaceCell(FaceId.Floor, 1, 0)
                        : new SurfaceCell(FaceId.Floor, 0, 1);
                    var world = GameplayWorldStateTestFactory.CreateBounded(new[]
                    {
                        Unit(PlayerId, 1, variant.TargetX, variant.TargetY, EnemyAiMode.None),
                        Unit(EnemyId, 2, 0, 0, EnemyAiMode.Chase),
                        Wall(20, wallCell),
                    });
                    Commit(new EnemyLogic(EnemyId, profile), world, "G03",
                        variant.Name, "tick-1", 1);
                    var started = GetState(world);
                    Assert.That(started.LockedStepX, Is.EqualTo(variant.Horizontal ? 1 : 0));
                    Assert.That(started.LockedStepY, Is.EqualTo(variant.Horizontal ? 0 : 1));
                    Assert.That(started.LockedTargetEntityId, Is.EqualTo(PlayerId));
                }
                finally
                {
                    EnemyAiProfileTestFactory.Destroy(profile);
                }
            }

            foreach (var variant in new[] { "different-face", "within-desired-distance" })
            {
                var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
                {
                    ChaseSettings = new ChaseSettings(ChaseAxisPriorityMode.HorizontalFirst,
                        trySecondaryAxisWhenBlocked: false, desiredChaseDistance: 2),
                    IncludeGlideBehaviorModule = true,
                    GlideTimingSettings = EnemyGlideTimingAuthoringSettings.FromRuntimeSettings(
                        timing, GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                });
                try
                {
                    var player = Unit(PlayerId, 1, 1, 0, EnemyAiMode.None);
                    if (variant == "different-face")
                    {
                        player.position = new SurfaceCell(FaceId.Front, 1, 0);
                    }
                    var world = GameplayWorldStateTestFactory.CreateBounded(new[]
                    {
                        player, Unit(EnemyId, 2, 0, 0, EnemyAiMode.Chase),
                    });
                    Commit(new EnemyLogic(EnemyId, profile), world, "G03", variant, "tick-1", 1);
                    Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(EnemyId, out _), Is.False);
                }
                finally
                {
                    EnemyAiProfileTestFactory.Destroy(profile);
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void G04_UnsettledVoluntaryPoseBlocksStartButActiveMovementUsesFreshIntent()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(0, 1, 5, 1, 0, 2));
            var world = CreateWorld();
            var logic = new EnemyLogic(EnemyId, profile);
            try
            {
                world.CreateWriteContext().SetUnitKinematicState(EnemyId, new UnitKinematicRuntimeState
                {
                    mode = MotionMode.Voluntary,
                    localOffset = new SimulationOffset2(SimulationFixed.FromRaw(1024), SimulationFixed.Zero),
                    velocity = new SimulationVelocity2(SimulationFixed.FromRaw(1024), SimulationFixed.Zero),
                    remainingDistanceUnits = 3072,
                    remainingTicks = 3,
                    totalTicks = 4,
                    elapsedTicks = 1,
                    commitTick = 2,
                    startedTick = 1,
                    stepDirectionX = 1,
                    speedScalePermille = 1000,
                    sequenceId = 1,
                });
                Commit(logic, world, "G04", "unsettled-voluntary", "blocked", 1);
                Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(EnemyId, out _), Is.False);
                world.CreateWriteContext().SetUnitKinematicState(EnemyId, UnitKinematicRuntimeState.SettledZero);
                Commit(logic, world, "G04", "settled", "starts", 2);
                Assert.That(GetState(world).Phase, Is.EqualTo(EnemyGlidePhase.Windup));

                world.CreateWriteContext().SetEnemyGlideState(EnemyId,
                    State(EnemyGlidePhase.Active, activeUntil: 8, lockedX: -1,
                        lockedTarget: PlayerId));
                var snapshot = world.CreateSnapshot();
                var intents = Intents(logic, snapshot, 3);
                Assert.That(intents, Has.Count.EqualTo(1));
                Assert.That(intents[0].Destination, Is.EqualTo(new Vector2Int(1, 0)));
                EnemyGlideCapture.Logic("G04", "active-fresh-intent", "query", 3,
                    snapshot, world.CreateSnapshot(), new[] { EnemyId },
                    new EnemyGlideRuntimeState?[] { null }, Array.Empty<string>(), Array.Empty<string>(),
                    intents, new[] { "LockedStep=(-1,0)", "ActualStep=(1,0)" });

                var otherModeWorld = CreateWorld();
                otherModeWorld.CreateWriteContext().SetUnitKinematicState(EnemyId,
                    new UnitKinematicRuntimeState
                    {
                        mode = MotionMode.Forced,
                        localOffset = new SimulationOffset2(
                            SimulationFixed.FromRaw(1024), SimulationFixed.Zero),
                        remainingDistanceUnits = 3072,
                        remainingTicks = 3,
                        totalTicks = 4,
                        elapsedTicks = 1,
                        stepDirectionX = 1,
                    });
                Commit(new EnemyLogic(EnemyId, profile), otherModeWorld, "G04",
                    "unsettled-other-mode", "starts", 1);
                Assert.That(GetState(otherModeWorld).Phase, Is.EqualTo(EnemyGlidePhase.Windup));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G04_ActiveTargetLossMovesByPatrolFallbackWithActualFacingAndCell()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(0, 5, 1, 0));
            try
            {
                var world = GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    Unit(EnemyId, 2, 0, 0, EnemyAiMode.Chase),
                });
                world.CreateWriteContext().SetEnemyGlideState(EnemyId,
                    State(EnemyGlidePhase.Active, activeUntil: 6, lockedX: -1,
                        lockedTarget: PlayerId));
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(world, Array.Empty<IEntityLogic>());
                for (var tick = 1; tick <= 2; tick++)
                {
                    var before = world.CreateSnapshot();
                    var result = pipeline.RunTick(new TickInput(tick));
                    EnemyGlideCapture.Tick("G04", "target-lost-patrol-fallback",
                        $"tick-{tick}", before, result, world, EnemyId);
                    Assert.That(world.CreateSnapshot().TryGetEntity(EnemyId, out var source), Is.True);
                    Assert.That(source.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, tick, 0)));
                    Assert.That(source.facing, Is.EqualTo(Direction.Right));
                    Assert.That(GetState(world).Phase, Is.EqualTo(EnemyGlidePhase.Active));
                    Assert.That(result.MovementPhaseResult.RawIntents.Any(intent =>
                        intent.SourceId == EnemyId && intent.Destination == new Vector2Int(tick, 0)), Is.True);
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G05_ExpiredActiveOnSolidWaitsForNextNonSolidPreMovementAndChecksFace()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(0, 1, 2, 0));
            try
            {
                var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var world = CreateWorld(EnemyAiMode.Chase, new[] { Wall(20, wallCell) });
                var logic = new EnemyLogic(EnemyId, profile);
                world.CreateWriteContext().SetEnemyGlideState(EnemyId,
                    State(EnemyGlidePhase.Active, activeUntil: 2, wantsRecover: false));
                world.CreateWriteContext().MoveEntity(EnemyId, wallCell);
                Commit(logic, world, "G05", "solid-expiry", "tick-2", 2);
                Assert.That(GetState(world).WantsRecover, Is.True);
                Commit(logic, world, "G05", "solid-expiry", "tick-3", 3);
                Assert.That(GetState(world).Phase, Is.EqualTo(EnemyGlidePhase.Active));
                world.CreateWriteContext().RemoveEntity(20);
                Commit(logic, world, "G05", "non-solid-next-pre-movement", "tick-4", 4);
                Assert.That(GetState(world).Phase, Is.EqualTo(EnemyGlidePhase.Recovery));

                var otherFaceWorld = CreateWorld(EnemyAiMode.Chase,
                    new[] { Wall(20, new SurfaceCell(FaceId.Front, 0, 0)) });
                var otherFaceLogic = new EnemyLogic(EnemyId, profile);
                otherFaceWorld.CreateWriteContext().SetEnemyGlideState(EnemyId,
                    State(EnemyGlidePhase.Active, activeUntil: 2));
                Commit(otherFaceLogic, otherFaceWorld, "G05", "same-planar-other-face", "tick-2", 2);
                Assert.That(GetState(otherFaceWorld).Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G05_KinematicMoveOffSolidRecoversOnlyAtFollowingPreMovement()
        {
            var timing = new EnemyGlideTimingSettings(0, 0, 1, 2, 0, 6);
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(timing);
            var solidCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var nonSolidCell = new SurfaceCell(FaceId.Floor, 2, 1);
            try
            {
                var world = GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    Wall(20, solidCell),
                    Unit(PlayerId, 1, 4, 1, EnemyAiMode.None),
                    Unit(EnemyId, 2, 0, 1, EnemyAiMode.Chase),
                });
                world.CreateWriteContext().SetEnemyGlideState(EnemyId,
                    EnemyGlideRuntimeState.Create(EnemyGlidePhase.Active, sequence: 1,
                        windupUntilTickExclusive: 0, activeUntilTickExclusive: 2,
                        recoveryUntilTickExclusive: 0, cooldownUntilTickExclusive: 0,
                        windupTicks: 0, durationTicks: 1, recoveryTicks: 2, cooldownTicks: 0,
                        glideMoveTicks: 6, lastExitedTick: 0, wantsRecover: true,
                        hasLockedStep: true, lockedStepX: 1, lockedTargetEntityId: PlayerId));
                world.CreateWriteContext().MoveEntity(EnemyId, solidCell);
                var progress = KinematicProgressResolver.ResolvePose(solidCell, 1, 0, 2, 6);
                world.CreateWriteContext().SetUnitKinematicState(EnemyId,
                    new UnitKinematicRuntimeState
                    {
                        localOffset = progress.LocalOffset,
                        velocity = new SimulationVelocity2(
                            SimulationFixed.FromRaw(SimulationFixed.UnitsPerCell / 6),
                            SimulationFixed.Zero),
                        mode = MotionMode.Voluntary,
                        remainingDistanceUnits = progress.RemainingDistanceUnits,
                        remainingTicks = progress.RemainingTicks,
                        speedScalePermille = 1000,
                        sequenceId = 1,
                        elapsedTicks = 2,
                        totalTicks = 6,
                        commitTick = 3,
                        startedTick = 1,
                        stepDirectionX = 1,
                    }.NormalizedForStorage());
                var generalTiming = GameplayTimingProfile.CreateDefault();
                var playerTiming = PlayerControlTimingSettings.CreateDefault()
                    .CreateAuthoritativeSnapshot(generalTiming.SimulationTicksPerSecond,
                        generalTiming.RepeatedMoveIntervalSeconds);
                var kinematicTiming = new UnitKinematicLocomotionTimingSettings
                {
                    KinematicMoveDurationSeconds = 6f / generalTiming.SimulationTicksPerSecond,
                }.CreateAuthoritativeSnapshot(generalTiming.SimulationTicksPerSecond);
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(world, Array.Empty<IEntityLogic>(), generalTiming,
                        playerTiming,
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                        unitKinematicLocomotionTiming: kinematicTiming);

                var beforeMove = world.CreateSnapshot();
                var movement = pipeline.RunTick(new TickInput(3));
                EnemyGlideCapture.Tick("G05", "solid-to-nonsolid-movement", "tick-3",
                    beforeMove, movement, world, EnemyId);
                Assert.That(world.CreateSnapshot().TryGetEntity(EnemyId, out var moved), Is.True);
                Assert.That(moved.position, Is.EqualTo(nonSolidCell));
                Assert.That(GetState(world).Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(GetState(world).WantsRecover, Is.True);

                var beforeRecovery = world.CreateSnapshot();
                var recovery = pipeline.RunTick(new TickInput(4));
                EnemyGlideCapture.Tick("G05", "solid-to-nonsolid-movement", "tick-4",
                    beforeRecovery, recovery, world, EnemyId);
                Assert.That(GetState(world).Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G06_TopologySuspensionKeepsStoredDeadlineAndResumeUsesCurrentTick()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(1, 3, 2, 1));
            try
            {
                foreach (var phase in new[] { EnemyGlidePhase.Windup, EnemyGlidePhase.Active,
                             EnemyGlidePhase.Recovery })
                {
                    var world = CreateWorld();
                    var logic = new EnemyLogic(EnemyId, profile);
                    var original = State(phase, windupUntil: 3, activeUntil: 3, recoveryUntil: 3);
                    world.CreateWriteContext().SetEnemyGlideState(EnemyId, original);
                    world.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
                    Commit(logic, world, "G06", phase + "-off-topology", "tick-8", 8);
                    var suspended = GetState(world);
                    Assert.That(suspended.Phase, Is.EqualTo(phase));
                    Assert.That(suspended.WindupUntilTickExclusive, Is.EqualTo(original.WindupUntilTickExclusive));
                    Assert.That(suspended.ActiveUntilTickExclusive, Is.EqualTo(original.ActiveUntilTickExclusive));
                    Assert.That(suspended.RecoveryUntilTickExclusive, Is.EqualTo(original.RecoveryUntilTickExclusive));
                    world.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
                    Commit(logic, world, "G06", phase + "-resume", "tick-9", 9);
                    var resumed = GetState(world);
                    if (phase == EnemyGlidePhase.Windup)
                    {
                        Assert.That(resumed.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                        Assert.That(resumed.ActiveUntilTickExclusive, Is.EqualTo(12));
                    }
                    if (phase == EnemyGlidePhase.Active)
                    {
                        Assert.That(resumed.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                        Assert.That(resumed.RecoveryUntilTickExclusive, Is.EqualTo(11));
                    }
                    if (phase == EnemyGlidePhase.Recovery)
                    {
                        Assert.That(resumed.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                        Assert.That(resumed.CooldownUntilTickExclusive, Is.EqualTo(10));
                    }
                }
                var activeWorld = CreateWorld();
                var activeLogic = new EnemyLogic(EnemyId, profile);
                activeWorld.CreateWriteContext().SetEnemyGlideState(EnemyId,
                    State(EnemyGlidePhase.Active, activeUntil: 12));
                activeWorld.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
                Commit(activeLogic, activeWorld, "G06", "active-unexpired-off-topology", "tick-8", 8);
                Assert.That(GetState(activeWorld).ActiveUntilTickExclusive, Is.EqualTo(12));
                activeWorld.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
                Commit(activeLogic, activeWorld, "G06", "active-unexpired-resume", "tick-9", 9);
                Assert.That(GetState(activeWorld).ActiveUntilTickExclusive, Is.EqualTo(12));
                var absentWorld = CreateWorld();
                var absentLogic = new EnemyLogic(EnemyId, profile);
                absentWorld.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
                Commit(absentLogic, absentWorld, "G06", "absent-off-topology", "tick-8", 8);
                Assert.That(absentWorld.CreateSnapshot().TryGetEnemyGlideState(EnemyId, out _), Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G07_InvalidAndMissingSourceKeepReachableLookupAndClearPathsSeparate()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(1, 3, 1, 0));
            try
            {
                foreach (var kind in new[] { "LookupNone", "Missing", "HpZero", "Marked", "Dead", "Detached" })
                foreach (var hasState in new[] { false, true })
                {
                    // WorldState.RemoveEntity removes the Glide record in the same operation.
                    if (kind == "Missing" && hasState) continue;
                    var world = CreateWorld();
                    var logic = new EnemyLogic(EnemyId, profile);
                    var writer = world.CreateWriteContext();
                    switch (kind)
                    {
                        case "LookupNone": writer.ApplyEnemyAiState(EnemyId, EnemyAiMode.None, 0); break;
                        case "Missing": writer.RemoveEntity(EnemyId); break;
                        case "HpZero": writer.ApplyDamage(EnemyId, 3); break;
                        case "Marked": ((IMovementCommitContext)writer).MarkDestroy(EnemyId); break;
                        case "Dead": writer.ApplyEnemyAiState(EnemyId, EnemyAiMode.Dead, 0); break;
                        case "Detached": writer.SetBoardPresence(EnemyId, EntityBoardPresence.Detached); break;
                    }
                    if (hasState && kind != "Missing")
                    {
                        writer.SetEnemyGlideState(EnemyId, State(EnemyGlidePhase.Windup,
                            windupUntil: 5));
                    }
                    Commit(logic, world, "G07", $"{kind}-{(hasState ? "present" : "absent")}",
                        "tick-2", 2);
                    var after = world.CreateSnapshot();
                    if (kind == "Missing") Assert.That(after.TryGetEnemyGlideState(EnemyId, out _), Is.False);
                    if (kind == "LookupNone" && hasState)
                    {
                        Assert.That(after.TryGetEnemyGlideState(EnemyId, out var retained), Is.True);
                        Assert.That(retained.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
                    }
                    if (hasState && kind != "LookupNone" && kind != "Missing")
                    {
                        Assert.That(after.TryGetEnemyGlideState(EnemyId, out _), Is.False);
                    }
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G08_PendingBlockedReactionWaitsDuringSuppressionAndConsumesAtRelease()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(1, 3, 1, 0));
            try
            {
                foreach (var mode in new[] { EnemyAiMode.Patrol, EnemyAiMode.Chase })
                foreach (var phase in new[] { EnemyGlidePhase.Windup, EnemyGlidePhase.Recovery })
                {
                    var world = CreateWorld(mode);
                    var logic = new EnemyLogic(EnemyId, profile);
                    world.CreateWriteContext().SetEnemyGlideState(EnemyId, State(phase,
                        windupUntil: 3, recoveryUntil: 3));
                    world.CreateWriteContext().SetPendingEnemyBlockedReaction(EnemyId,
                        new PendingEnemyBlockedReaction(EnemyId,
                            EnemyBlockedReactionKind.KinematicContinuationTargetBlocked,
                            mode, new SurfaceCell(FaceId.Floor, 0, 0),
                            new SurfaceCell(FaceId.Floor, 1, 0), Direction.Right,
                            LegalityBlockerKind.Solid, SolidKind.Wall, EntityType.Wall, 20,
                            createdTick: 1, expireTick: 10));
                    var variant = $"{mode}-{phase}";
                    Commit(logic, world, "G08", variant, "suppressed-tick-2", 2);
                    Assert.That(world.CreateSnapshot().TryGetPendingEnemyBlockedReaction(EnemyId, out _), Is.True);
                    Commit(logic, world, "G08", variant, "boundary-tick-3", 3);
                    Assert.That(world.CreateSnapshot().TryGetPendingEnemyBlockedReaction(EnemyId, out _), Is.True);
                    Commit(logic, world, "G08", variant, "release-tick-4", 4);
                    Assert.That(world.CreateSnapshot().TryGetPendingEnemyBlockedReaction(EnemyId, out _), Is.False);
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void G10_SharedRuntimeKeepsSourceStateSeparateAcrossEntityOrder()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(2, 1, 3, 1, 0));
            try
            {
                string firstHash = null;
                foreach (var reversed in new[] { false, true })
                {
                    var entities = new List<EntityState> { Unit(PlayerId, 1, 3, 0, EnemyAiMode.None) };
                    var a = Unit(EnemyId, 2, 0, 0, EnemyAiMode.Chase);
                    var b = Unit(50, 2, 0, 1, EnemyAiMode.Chase);
                    entities.AddRange(reversed ? new[] { b, a } : new[] { a, b });
                    var world = GameplayWorldStateTestFactory.CreateBounded(entities);
                    world.CreateWriteContext().SetEnemyGlideState(EnemyId,
                        EnemyGlideRuntimeState.Create(EnemyGlidePhase.Ready, sequence: 2,
                            windupUntilTickExclusive: 0, activeUntilTickExclusive: 0,
                            recoveryUntilTickExclusive: 0, cooldownUntilTickExclusive: 0,
                            windupTicks: 1, durationTicks: 3, recoveryTicks: 1,
                            cooldownTicks: 0, lastExitedTick: 0,
                            initialDelayInitialized: true, initialDelayTicksRemaining: 2));
                    world.CreateWriteContext().SetEnemyGlideState(50,
                        State(EnemyGlidePhase.Cooldown, sequence: 7, cooldownUntil: 5));
                    var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                        .CreateTickPipeline(world, Array.Empty<IEntityLogic>());
                    var before = world.CreateSnapshot();
                    var result = pipeline.RunTick(new TickInput(1));
                    EnemyGlideCapture.Tick("G10", reversed ? "reverse" : "forward", "tick-1",
                        before, result, world, EnemyId, 50);
                    Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(EnemyId, out var one), Is.True);
                    Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(50, out var two), Is.True);
                    Assert.That(one.Phase, Is.EqualTo(EnemyGlidePhase.Ready));
                    Assert.That(two.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                    Assert.That(one.Sequence, Is.EqualTo(2));
                    Assert.That(two.Sequence, Is.EqualTo(7));
                    Assert.That(one.InitialDelayTicksRemaining, Is.EqualTo(1));
                    if (firstHash != null) Assert.That(result.DeterminismHash, Is.EqualTo(firstHash));
                    firstHash = result.DeterminismHash;
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionGlideProfile_CompiledProviderPipelinePreservesLifecycleAndSignals()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(ProductionProfilePath);
            Assert.That(profile, Is.Not.Null);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            Assert.That(definition.TryGetGlideBehavior(out var glide), Is.True);
            var world = CreateWorld();
            var sawWindup = false;
            var sawActive = false;
            var sawRecovery = false;
            var lastPhase = EnemyGlidePhase.Ready;
            var maxTick = glide.Timing.InitialDelayTicks + glide.Timing.WindupTicks +
                          glide.Timing.DurationTicks + glide.Timing.RecoveryTicks + 8;
            // Production initial delay exceeds ordinary movement timing. Hold the source at a
            // reachable chase distance so this test observes the behavior lifecycle itself.
            ((IPreMovementStateCommitContext)world.CreateWriteContext())
                .SetEnemyLocomotionCooldown(EnemyId, maxTick + 10);
            var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                .CreateTickPipeline(world, Array.Empty<IEntityLogic>());
            for (var tick = 1; tick <= maxTick && !sawRecovery; tick++)
            {
                var before = world.CreateSnapshot();
                var result = pipeline.RunTick(new TickInput(tick));
                Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(EnemyId, out var state), Is.True);
                if (state.Phase != lastPhase || tick == 1)
                {
                    EnemyGlideCapture.Tick("G11", "production-asset", $"tick-{tick}",
                        before, result, world, EnemyId);
                }
                sawWindup |= state.Phase == EnemyGlidePhase.Windup;
                sawActive |= state.Phase == EnemyGlidePhase.Active;
                sawRecovery |= state.Phase == EnemyGlidePhase.Recovery;
                if (state.Phase == EnemyGlidePhase.Active || state.Phase == EnemyGlidePhase.Recovery)
                {
                    Assert.That(result.PresentationData.EnemyGlideSignals.Any(signal =>
                        signal.EntityId == EnemyId), Is.True);
                }
                lastPhase = state.Phase;
            }
            Assert.That(sawActive, Is.True);
            Assert.That(sawRecovery, Is.True);
            if (glide.Timing.WindupTicks > 0) Assert.That(sawWindup, Is.True);
        }

        private static WorldState CreateWorld(EnemyAiMode mode = EnemyAiMode.Chase,
            IEnumerable<EntityState> additional = null)
        {
            var entities = new List<EntityState>
            {
                Unit(PlayerId, 1, 3, 0, EnemyAiMode.None),
                Unit(EnemyId, 2, 0, 0, mode),
            };
            if (additional != null) entities.AddRange(additional);
            return GameplayWorldStateTestFactory.CreateBounded(entities,
                new BoardBounds(new Vector2Int(-8, -8), new Vector2Int(8, 8)));
        }

        private static EntityState Unit(int id, int team, int x, int y, EnemyAiMode mode) =>
            new EntityState
            {
                entityId = id,
                position = new SurfaceCell(FaceId.Floor, x, y),
                hp = team == 1 ? 100 : 3,
                maxHp = team == 1 ? 100 : 3,
                teamId = team,
                type = EntityType.Unit,
                unitRole = team == 1 ? UnitRole.Player : UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = mode,
            };

        private static EntityState Wall(int id, SurfaceCell cell) =>
            new EntityState
            {
                entityId = id,
                position = cell,
                hp = 1,
                maxHp = 1,
                type = EntityType.Wall,
                boardPresence = EntityBoardPresence.Occupying,
            };

        private static EnemyGlideRuntimeState State(EnemyGlidePhase phase,
            int sequence = 1, int windupUntil = 0, int activeUntil = 0,
            int recoveryUntil = 0, int cooldownUntil = 0, bool wantsRecover = false,
            int lockedX = 1, int lockedY = 0, int lockedTarget = PlayerId) =>
            EnemyGlideRuntimeState.Create(phase, sequence, windupUntil, activeUntil,
                recoveryUntil, cooldownUntil, windupTicks: 1, durationTicks: 3,
                recoveryTicks: 2, cooldownTicks: 1, glideMoveTicks: 2,
                lastExitedTick: 0, wantsRecover: wantsRecover, hasLockedStep: true,
                lockedStepX: lockedX, lockedStepY: lockedY,
                lockedTargetEntityId: lockedTarget);

        private static EnemyGlideRuntimeState GetState(WorldState world)
        {
            Assert.That(world.CreateSnapshot().TryGetEnemyGlideState(EnemyId, out var state), Is.True);
            return state;
        }

        private static List<RawMovementIntent> Intents(EnemyLogic logic, WorldSnapshot snapshot, int tick)
        {
            var intents = new List<RawMovementIntent>();
            logic.CollectMovementIntents(snapshot, new TickInput(tick), intents);
            return intents;
        }

        private static void Commit(EnemyLogic logic, WorldState world, string caseId,
            string variant, string recordId, int tick)
        {
            var before = world.CreateSnapshot();
            var recorder = new GlideRecordingCommitContext(world.CreateWriteContext());
            var updates = new List<string>();
            ((IPreMovementStateLogic)logic).CommitPreMovementState(before, new TickInput(tick),
                recorder, updates, new List<PlayerActionTransition>());
            EnemyGlideCapture.Logic(caseId, variant, recordId, tick, before, world.CreateSnapshot(),
                new[] { EnemyId, PlayerId }, new EnemyGlideRuntimeState?[]
                {
                    recorder.HasGlideWrite ? recorder.LastGlideState : (EnemyGlideRuntimeState?)null,
                    null,
                }, recorder.Writes, updates);
        }

        private sealed class GlideRecordingCommitContext : IPreMovementStateCommitContext
        {
            private readonly IPreMovementStateCommitContext _inner;
            public readonly List<string> Writes = new();
            public bool HasGlideWrite;
            public EnemyGlideRuntimeState LastGlideState;

            public GlideRecordingCommitContext(IPreMovementStateCommitContext inner) => _inner = inner;
            public void SetPlayerControlState(int id, PlayerControlState state) { Writes.Add($"PlayerControl|E={id}"); _inner.SetPlayerControlState(id, state); }
            public void SetFacing(int id, Direction facing) { Writes.Add($"Facing|E={id}|To={facing}"); _inner.SetFacing(id, facing); }
            public void SetEnemyLocomotionCooldown(int id, int ticks) { Writes.Add($"LocomotionCooldown|E={id}|To={ticks}"); _inner.SetEnemyLocomotionCooldown(id, ticks); }
            public void SetEnemyAttackCooldown(int id, int ticks, int total) { Writes.Add($"AttackCooldown|E={id}|To={ticks}|Total={total}"); _inner.SetEnemyAttackCooldown(id, ticks, total); }
            public void SetEnemyPatrolState(int id, EnemyPatrolRuntimeState state) { Writes.Add($"Patrol|E={id}|Sequence={state.sequence}|Home={state.homeCell}|Direction={state.lastCommittedDirection}"); _inner.SetEnemyPatrolState(id, state); }
            public void SetPendingEnemyBlockedReaction(int id, PendingEnemyBlockedReaction state) { Writes.Add($"PendingSet|E={id}|Created={state.CreatedTick}|Expire={state.ExpireTick}"); _inner.SetPendingEnemyBlockedReaction(id, state); }
            public void ClearPendingEnemyBlockedReaction(int id) { Writes.Add($"PendingClear|E={id}"); _inner.ClearPendingEnemyBlockedReaction(id); }
            public void SetEnemyChargeState(int id, EnemyChargeRuntimeState state) { Writes.Add($"Charge|E={id}|Phase={state.phase}"); _inner.SetEnemyChargeState(id, state); }
            public void SetEnemyJumpState(int id, EnemyJumpRuntimeState state) { Writes.Add($"Jump|E={id}|Phase={state.phase}"); _inner.SetEnemyJumpState(id, state); }
            public void SetEnemyGlideState(int id, EnemyGlideRuntimeState state) { HasGlideWrite = true; LastGlideState = state; Writes.Add($"Glide|E={id}|Phase={state.Phase}|Sequence={state.Sequence}|WantsRecover={state.WantsRecover}"); _inner.SetEnemyGlideState(id, state); }
            public void SetEnemyUtilityState(int id, EnemyUtilityRuntimeState state) { Writes.Add($"Utility|E={id}"); _inner.SetEnemyUtilityState(id, state); }
            public void SetEnemySummonBehaviorState(int id, EnemySummonBehaviorRuntimeState state) { Writes.Add($"Summon|E={id}"); _inner.SetEnemySummonBehaviorState(id, state); }
            public void SetBoxInteractionLockState(int id, BoxInteractionLockState state) { Writes.Add($"BoxLock|E={id}"); _inner.SetBoxInteractionLockState(id, state); }
            public void SetEnemyGravityFieldAuraFieldState(int id, EnemyGravityFieldAuraFieldState state) { Writes.Add($"Aura|E={id}"); _inner.SetEnemyGravityFieldAuraFieldState(id, state); }
            public void SetGravityFieldState(int id, GravityFieldPhase phase, int ticks) { Writes.Add($"GravityField|E={id}|Phase={phase}|Timer={ticks}"); _inner.SetGravityFieldState(id, phase, ticks); }
        }
    }
}
