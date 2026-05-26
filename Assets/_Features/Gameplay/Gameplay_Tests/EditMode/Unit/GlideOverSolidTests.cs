using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GlideOverSolidTests
    {
        [Test]
        [Category("Extended")]
        public void GlideCapability_CompilesTimingsAndGuardsWrongTimingLane()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(durationTicks: 3, cooldownTicks: 2));

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(definition.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.GlideOverSolid));
                Assert.That(definition.GlideTimingSettings.WindupTicks, Is.EqualTo(0));
                Assert.That(definition.GlideTimingSettings.DurationTicks, Is.EqualTo(3));
                Assert.That(definition.GlideTimingSettings.RecoveryTicks, Is.EqualTo(0));
                Assert.That(definition.GlideTimingSettings.CooldownTicks, Is.EqualTo(2));
                Assert.That(definition.Capabilities.TryGetMovementSkill(out var movementSkill), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                {
                    _ = movementSkill.JumpTimingSettings;
                });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideTimingAuthoring_CompilesPhaseTimingsAndRejectsInvalidDurations()
        {
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(0f, 0f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(-0.01f, 0f, 1f, 0f, 0f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(-0.01f, 1f, 0f, 0f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(0f, 1f, -0.01f, 0f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(1f, -0.01f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.Throws<ArgumentException>(() =>
                new EnemyGlideTimingAuthoringSettings(0f, 0f, 1f, 0f, 0f, -0.01f)
                    .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond));

            var runtime = new EnemyGlideTimingAuthoringSettings(
                    initialDelaySeconds: 3f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    windupSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    durationSeconds: 1f,
                    recoverySeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    cooldownSeconds: 0f,
                    glideMoveDurationSeconds: 4f / GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(runtime.InitialDelayTicks, Is.EqualTo(3));
            Assert.That(runtime.WindupTicks, Is.EqualTo(1));
            Assert.That(runtime.DurationTicks, Is.EqualTo(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.That(runtime.RecoveryTicks, Is.EqualTo(2));
            Assert.That(runtime.CooldownTicks, Is.EqualTo(0));
            Assert.That(runtime.GlideMoveTicks, Is.EqualTo(4));
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_GlideInitialDelay_BlocksFirstStartUntilDelayCompletes()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(
                    initialDelayTicks: 2,
                    windupTicks: 0,
                    durationTicks: 1,
                    recoveryTicks: 0,
                    cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);

            try
            {
                var delayedUpdates = CommitPreMovementAndGetUpdates(logic, worldState, tickIndex: 1);

                Assert.That(delayedUpdates, Has.Some.Contains("Label=InitialDelayTick"));
                Assert.That(delayedUpdates, Has.None.Contains("Label=Start"));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var delayed), Is.True);
                Assert.That(delayed.Phase, Is.EqualTo(EnemyGlidePhase.Ready));
                Assert.That(delayed.InitialDelayInitialized, Is.True);
                Assert.That(delayed.InitialDelayTicksRemaining, Is.EqualTo(1));

                var startUpdates = CommitPreMovementAndGetUpdates(logic, worldState, tickIndex: 2);

                Assert.That(startUpdates, Has.Some.Contains("Label=InitialDelayReady"));
                Assert.That(startUpdates, Has.Some.Contains("Label=Start"));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var started), Is.True);
                Assert.That(started.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(started.InitialDelayInitialized, Is.True);
                Assert.That(started.InitialDelayTicksRemaining, Is.Zero);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_GlideStartsExpiresAndBlocksSameTickRestartWhenCooldownIsZero()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(durationTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 1);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var started), Is.True);
                Assert.That(started.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(started.IsActive, Is.True);
                Assert.That(started.ActiveUntilTickExclusive, Is.EqualTo(2));

                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var exited), Is.True);
                Assert.That(exited.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                Assert.That(exited.IsActive, Is.False);
                Assert.That(exited.CooldownUntilTickExclusive, Is.EqualTo(2));
                Assert.That(exited.LastExitedTick, Is.EqualTo(2));

                CommitPreMovement(logic, worldState, tickIndex: 3);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var restarted), Is.True);
                Assert.That(restarted.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(restarted.IsActive, Is.True);
                Assert.That(restarted.Sequence, Is.EqualTo(2));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_GlideRunsFullNonZeroPhaseLifecycle()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 2, recoveryTicks: 1, cooldownTicks: 2));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 1);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var windup), Is.True);
                Assert.That(windup.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
                Assert.That(windup.WindupUntilTickExclusive, Is.EqualTo(2));

                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var active), Is.True);
                Assert.That(active.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(active.ActiveUntilTickExclusive, Is.EqualTo(4));

                CommitPreMovement(logic, worldState, tickIndex: 4);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var recovery), Is.True);
                Assert.That(recovery.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                Assert.That(recovery.RecoveryUntilTickExclusive, Is.EqualTo(5));

                CommitPreMovement(logic, worldState, tickIndex: 5);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var cooldown), Is.True);
                Assert.That(cooldown.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                Assert.That(cooldown.CooldownUntilTickExclusive, Is.EqualTo(7));
                Assert.That(cooldown.LastExitedTick, Is.EqualTo(5));

                CommitPreMovement(logic, worldState, tickIndex: 7);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var restarted), Is.True);
                Assert.That(restarted.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
                Assert.That(restarted.Sequence, Is.EqualTo(2));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_PoseUnsettledStillSuppressesNewIntent()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 2, recoveryTicks: 1, cooldownTicks: 0));
            var enemyCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, enemyCell, EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                40,
                CreateGlideState(
                    EnemyGlidePhase.Cooldown,
                    sequence: 1,
                    cooldownUntilTickExclusive: 10,
                    windupTicks: 1,
                    durationTicks: 2,
                    recoveryTicks: 1,
                    cooldownTicks: 0,
                    lastExitedTick: 9));
            writeContext.SetUnitKinematicState(
                40,
                CreateVoluntaryStepState(
                    enemyCell,
                    elapsedTicks: 1,
                    totalTicks: 4,
                    startedTick: 6,
                    stepDirectionX: 1,
                    stepDirectionY: 0));

            try
            {
                var blockedUpdates = CommitPreMovementAndGetUpdates(logic, worldState, tickIndex: 10);

                Assert.That(blockedUpdates, Has.None.Contains("EnemyGlideStateUpdated|E=40|Label=Start"));
                Assert.That(blockedUpdates, Has.None.Contains("|Label=Start|"));
                Assert.That(blockedUpdates, Has.None.Contains("|Label=EnterActive|"));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var blockedGlide), Is.True);
                Assert.That(blockedGlide.Phase, Is.Not.EqualTo(EnemyGlidePhase.Windup));
                Assert.That(blockedGlide.Phase, Is.Not.EqualTo(EnemyGlidePhase.Active));
                Assert.That(worldState.CreateSnapshot().TryGetUnitKinematicState(40, out _), Is.True);

                worldState.CreateWriteContext().SetUnitKinematicState(40, UnitKinematicRuntimeState.SettledZero);
                var startedUpdates = CommitPreMovementAndGetUpdates(logic, worldState, tickIndex: 11);

                Assert.That(
                    startedUpdates,
                    Has.Some.Contains("EnemyGlideStateUpdated|E=40|Label=Start"));
                Assert.That(startedUpdates, Has.None.Contains("StartBlockedPoseUnsettled"));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var windup), Is.True);
                Assert.That(windup.Phase, Is.EqualTo(EnemyGlidePhase.Windup));
                Assert.That(windup.WindupUntilTickExclusive, Is.EqualTo(12));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_ActiveDurationExpired_OnSolid_DoesNotEnterRecover()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 1, recoveryTicks: 1, cooldownTicks: 0));
            var solidCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(20, solidCell, BoxCapabilities.Push | BoxCapabilities.Flip),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 1, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);
            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                40,
                solidCell,
                CreateActiveGlide(activeUntilTickExclusive: 2, durationTicks: 1, cooldownTicks: 0, recoveryTicks: 1, lockedStepX: -1, lockedStepY: 0));

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var expiredOnSolid), Is.True);
                Assert.That(expiredOnSolid.IsActive, Is.True);
                Assert.That(expiredOnSolid.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(expiredOnSolid.WantsRecover, Is.True);

                CommitPreMovement(logic, worldState, tickIndex: 3);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var stillActive), Is.True);
                Assert.That(stillActive.IsActive, Is.True);
                Assert.That(stillActive.WantsRecover, Is.True);

                worldState.CreateWriteContext().RemoveEntity(20);
                CommitPreMovement(logic, worldState, tickIndex: 4);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var recovery), Is.True);
                Assert.That(recovery.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                Assert.That(recovery.RecoveryUntilTickExclusive, Is.EqualTo(5));

                CommitPreMovement(logic, worldState, tickIndex: 5);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var cooldown), Is.True);
                Assert.That(cooldown.Phase, Is.EqualTo(EnemyGlidePhase.Cooldown));
                Assert.That(cooldown.CooldownUntilTickExclusive, Is.EqualTo(5));
                Assert.That(cooldown.LastExitedTick, Is.EqualTo(5));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ActiveGlideAndCooldownDoNotSuppressChaseMovementIntent()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(durationTicks: 3, cooldownTicks: 1));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);
            var movementIntents = new List<RawMovementIntent>();
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(2), movementIntents);

                Assert.That(movementIntents.Select(intent => intent.SourceId), Is.EquivalentTo(new[] { 40 }));
                Assert.That(movementIntents[0].Destination, Is.EqualTo(new Vector2Int(1, 0)));

                movementIntents.Clear();
                worldState.CreateWriteContext().SetEnemyGlideState(
                    40,
                    CreateGlideState(
                        EnemyGlidePhase.Cooldown,
                        cooldownUntilTickExclusive: 5,
                        durationTicks: 3,
                        cooldownTicks: 1,
                        lastExitedTick: 2));

                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(3), movementIntents);

                Assert.That(movementIntents.Select(intent => intent.SourceId), Is.EquivalentTo(new[] { 40 }));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_WindupAndRecoverySuppressGroundMovementIntent()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 3, recoveryTicks: 1, cooldownTicks: 1));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            var logic = new EnemyLogic(40, profile);

            try
            {
                foreach (var glideState in new[]
                         {
                             CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5),
                             CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5),
                         })
                {
                    var movementIntents = new List<RawMovementIntent>();
                    worldState.CreateWriteContext().SetEnemyGlideState(40, glideState);

                    logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(2), movementIntents);

                    Assert.That(movementIntents, Is.Empty);
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [TestCase(EnemyGlidePhase.Windup)]
        [TestCase(EnemyGlidePhase.Recovery)]
        [Category("Extended")]
        public void EnemyLogic_GlideSuppression_TargetLostToPatrol_PreservesAutonomousPatrolFacing(EnemyGlidePhase phase)
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.RandomWalk,
                PatrolSettings = PatrolSettings.CreateDefaultRandomWalk(),
                MovementSkillStrategyKind = MovementSkillStrategyKind.GlideOverSolid,
                GlideTimingSettings = EnemyGlideTimingAuthoringSettings.FromRuntimeSettings(
                    new EnemyGlideTimingSettings(windupTicks: 3, durationTicks: 3, recoveryTicks: 3, cooldownTicks: 0),
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(40, teamId: 2, sourceCell, EnemyAiMode.Patrol),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(5, 1)),
                GameplayTerrainData.Empty);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyPatrolState(
                40,
                new EnemyPatrolRuntimeState
                {
                    sequence = 1,
                    homeCell = new SurfaceCell(FaceId.Floor, 0, 0),
                    lastCommittedDirection = Direction.Right,
                });
            writeContext.SetEnemyGlideState(
                40,
                phase == EnemyGlidePhase.Windup
                    ? CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5)
                    : CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5));

            try
            {
                var pipeline = CreateGlideKinematicPipeline(profile, worldState);

                var tick = pipeline.RunTick(new TickInput(2));
                var enemy = tick.FinalEntities.Single(entity => entity.entityId == 40);

                Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == 40), Is.Empty);
                Assert.That(enemy.position, Is.EqualTo(sourceCell));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Right));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_NotActive_StillBlockedBySolid()
        {
            var profile = CreateCrossLineGlideChaserProfile(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 3, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateWall(30, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Patrol),
            });

            try
            {
                var tick = CreateGlideKinematicPipeline(profile, worldState).RunTick(new TickInput(1));

                Assert.That(tick.FinalEntities.Single(entity => entity.entityId == 40).aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_Active_DetectionIgnoresSolidForAcquisition()
        {
            var profile = CreateCrossLineGlideChaserProfile(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 4, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateWall(30, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 5,
                    durationTicks: 4,
                    cooldownTicks: 0,
                    recoveryTicks: 1,
                    lockedTargetEntityId: 999));

            try
            {
                var tick = CreateGlideKinematicPipeline(profile, worldState).RunTick(new TickInput(1));

                Assert.That(tick.FinalEntities.Single(entity => entity.entityId == 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_Active_AcquiresNewTargetBehindSolid()
        {
            var profile = CreateCrossLineGlideChaserProfile(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 4, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(20, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateWall(30, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 5,
                    durationTicks: 4,
                    cooldownTicks: 0,
                    recoveryTicks: 1));

            try
            {
                var tick = CreateGlideKinematicPipeline(profile, worldState).RunTick(new TickInput(1));

                Assert.That(tick.FinalEntities.Single(entity => entity.entityId == 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_Active_CanTurnLikeNormalChase()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 4, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(0, 3), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 5,
                    durationTicks: 4,
                    cooldownTicks: 0,
                    recoveryTicks: 1,
                    lockedStepX: 1,
                    lockedStepY: 0));

            try
            {
                var tick = CreateGlideKinematicPipeline(profile, worldState).RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
                Assert.That(glider.position, Is.Not.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Glider_Active_KinematicAcceptsBaselineStepDifferentFromLockedStep()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 4, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(0, 3), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 5,
                    durationTicks: 4,
                    cooldownTicks: 0,
                    recoveryTicks: 1,
                    lockedStepX: 1,
                    lockedStepY: 0));

            try
            {
                var tick = CreateGlideKinematicPipeline(profile, worldState).RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.True);
                Assert.That(tick.Trace.Text, Does.Not.Contain("Reason=EnemyKinematicTraversalBlocked"));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_Active_UsesSameChaseStepAsNormalUnitExceptSolid()
        {
            var normalProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                DetectionSettings = new DetectionSettings(
                    senseRange: 8,
                    requireSameFace: true,
                    canTargetMarkedForDeath: false),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
            });
            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 4, recoveryTicks: 1, cooldownTicks: 0));

            try
            {
                var normalWorldState = CreateWorldState(new[]
                {
                    CreateUnit(10, teamId: 1, new Vector2Int(0, 3), EnemyAiMode.None),
                    CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
                });
                var glideWorldState = CreateWorldState(new[]
                {
                    CreateUnit(10, teamId: 1, new Vector2Int(0, 3), EnemyAiMode.None),
                    CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
                });
                glideWorldState.CreateWriteContext().SetEnemyGlideState(
                    40,
                    CreateActiveGlide(
                        activeUntilTickExclusive: 5,
                        durationTicks: 4,
                        cooldownTicks: 0,
                        recoveryTicks: 1,
                        lockedStepX: 1,
                        lockedStepY: 0));

                var normalIntents = CollectMovementIntents(normalProfile, normalWorldState);
                var glideIntents = CollectMovementIntents(glideProfile, glideWorldState);

                Assert.That(normalIntents.Single().Destination, Is.EqualTo(new Vector2Int(0, 1)));
                Assert.That(glideIntents.Single().Destination, Is.EqualTo(normalIntents.Single().Destination));

                var normalSolidWorldState = CreateWorldState(new[]
                {
                    CreateUnit(10, teamId: 1, new Vector2Int(0, 3), EnemyAiMode.None),
                    CreateWall(30, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
                });
                var glideSolidWorldState = CreateWorldState(new[]
                {
                    CreateUnit(10, teamId: 1, new Vector2Int(0, 3), EnemyAiMode.None),
                    CreateWall(30, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
                });
                glideSolidWorldState.CreateWriteContext().SetEnemyGlideState(
                    40,
                    CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 4, cooldownTicks: 0, recoveryTicks: 1));

                Assert.That(CollectMovementIntents(normalProfile, normalSolidWorldState), Is.Empty);
                Assert.That(
                    CollectMovementIntents(glideProfile, glideSolidWorldState).Single().Destination,
                    Is.EqualTo(new Vector2Int(0, 1)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(normalProfile);
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_ActiveGlide_StillRequiresRangeAlignmentFace()
        {
            var cases = new[]
            {
                ("Range", CreateUnit(10, teamId: 1, new Vector2Int(4, 0), EnemyAiMode.None)),
                ("Alignment", CreateUnit(10, teamId: 1, new Vector2Int(3, 1), EnemyAiMode.None)),
                ("Face", CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Front, 3, 0), EnemyAiMode.None)),
                ("Dead", CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None, hp: 0)),
                ("Marked", CreateMarkedUnit(10, teamId: 1, new Vector2Int(3, 0))),
                ("SameTeam", CreateUnit(10, teamId: 2, new Vector2Int(3, 0), EnemyAiMode.None)),
                ("Detached", CreateDetachedUnit(10, teamId: 1, new Vector2Int(3, 0))),
            };

            for (var i = 0; i < cases.Length; i++)
            {
                var profile = CreateCrossLineGlideChaserProfile(
                    new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 4, recoveryTicks: 1, cooldownTicks: 0),
                    senseRange: 3);
                var worldState = CreateWorldState(new[]
                {
                    cases[i].Item2,
                    CreateWall(30, new SurfaceCell(FaceId.Floor, 1, 0)),
                    CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
                });
                worldState.CreateWriteContext().SetEnemyGlideState(
                    40,
                    CreateActiveGlide(
                        activeUntilTickExclusive: 5,
                        durationTicks: 4,
                        cooldownTicks: 0,
                        recoveryTicks: 1,
                        lockedTargetEntityId: 10));

                try
                {
                    var tick = CreateGlideKinematicPipeline(profile, worldState).RunTick(new TickInput(1));

                    Assert.That(
                        tick.FinalEntities.Single(entity => entity.entityId == 40).aiMode,
                        Is.EqualTo(EnemyAiMode.Patrol),
                        cases[i].Item1);
                }
                finally
                {
                    EnemyAiProfileTestFactory.Destroy(profile);
                }
            }
        }

        [TestCase(EnemyGlidePhase.Windup)]
        [TestCase(EnemyGlidePhase.Recovery)]
        [Category("Extended")]
        public void Glider_WindupRecovery_DoNotIgnoreSolid(EnemyGlidePhase phase)
        {
            var profile = CreateCrossLineGlideChaserProfile(
                new EnemyGlideTimingSettings(windupTicks: 3, durationTicks: 3, recoveryTicks: 3, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new Vector2Int(3, 0), EnemyAiMode.None),
                CreateWall(30, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateUnit(40, teamId: 2, new Vector2Int(0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateGlideState(
                    phase,
                    windupUntilTickExclusive: phase == EnemyGlidePhase.Windup ? 5 : 0,
                    activeUntilTickExclusive: 5,
                    recoveryUntilTickExclusive: phase == EnemyGlidePhase.Recovery ? 5 : 0,
                    windupTicks: 3,
                    durationTicks: 3,
                    recoveryTicks: 3,
                    hasLockedStep: true,
                    lockedStepX: 1,
                    lockedStepY: 0,
                    lockedTargetEntityId: 10));

            try
            {
                var tick = CreateGlideKinematicPipeline(profile, worldState).RunTick(new TickInput(1));

                Assert.That(tick.FinalEntities.Single(entity => entity.entityId == 40).aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlide_IgnoresOnlyActiveGlider()
        {
            var origin = new SurfaceCell(FaceId.Floor, 0, 0);
            var target = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(20, origin, BoxCapabilities.Push),
                CreateUnit(40, teamId: 2, target, EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));

            var activeResolved = worldState.CreateSnapshot().TryResolveNextSurfaceBoxSlideStep(
                origin,
                Vector2Int.right,
                out var activeDestination,
                out _);
            Assert.That(activeResolved, Is.True);
            Assert.That(activeDestination, Is.EqualTo(target));

            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5));
            AssertBoxSlideBlockedBy(worldState, origin, 40);

            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5));
            AssertBoxSlideBlockedBy(worldState, origin, 40);
        }

        [Test]
        [Category("Extended")]
        public void MovementExpansion_BoxSlideImpactSkipsActiveGliderSharingStopperCellWithSolid()
        {
            var origin = new SurfaceCell(FaceId.Floor, 0, 0);
            var stopperCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, -1, 0), EnemyAiMode.None),
                CreateBox(20, origin, BoxCapabilities.Push),
                CreateWall(30, stopperCell),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 2, 0), EnemyAiMode.Chase),
            });
            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                40,
                stopperCell,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1, lockedStepX: -1, lockedStepY: 0));

            var groups = ExpandPushIntoBox(worldState);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void MovementExpansion_BoxSlideImpactFiltersOnlyActiveGliderFromStackedTargets()
        {
            var origin = new SurfaceCell(FaceId.Floor, 0, 0);
            var stopperCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, -1, 0), EnemyAiMode.None),
                CreateBox(20, origin, BoxCapabilities.Push),
                CreateUnit(40, teamId: 2, stopperCell, EnemyAiMode.Chase),
                CreateUnit(50, teamId: 2, stopperCell, EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));

            var groups = ExpandPushIntoBox(worldState);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].GroupKind, Is.EqualTo(ActionGroupKind.BoxImpact));
            CollectionAssert.AreEqual(new[] { 50 }, groups[0].ImpactTargetIds.ToArray());
        }

        [Test]
        [Category("Extended")]
        public void Legality_ActiveGlideBypassesOnlySolidBlockers()
        {
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var terrainCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var emptyCell = new SurfaceCell(FaceId.Floor, 0, -1);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateWall(30, wallCell),
                    CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
                },
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(1, 1)),
                new GameplayTerrainData(new[]
                {
                    new TerrainCellState(terrainCell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                }));
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(snapshot, EntityType.Unit, wallCell, 40).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(snapshot, EntityType.Unit, terrainCell, 40).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    snapshot,
                    EntityType.Unit,
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    40).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    snapshot,
                    EntityType.Unit,
                    emptyCell,
                    40,
                    ReservationStatus.Conflicted).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));

            foreach (var glideState in new[]
                     {
                         CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5),
                         CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5),
                     })
            {
                worldState.CreateWriteContext().SetEnemyGlideState(40, glideState);
                Assert.That(
                    RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                        worldState.CreateSnapshot(),
                        EntityType.Unit,
                        wallCell,
                        40).Verdict,
                    Is.EqualTo(LegalityVerdict.Blocked));
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_NonActive_OnSolid_CannotRepresentSolidOverlap()
        {
            var currentWallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(30, currentWallCell),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                40,
                currentWallCell,
                CreateActiveGlide(activeUntilTickExclusive: 2, durationTicks: 1, cooldownTicks: 1));

            foreach (var glideState in new[]
                     {
                         CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5),
                         CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5),
                         CreateGlideState(EnemyGlidePhase.Cooldown, cooldownUntilTickExclusive: 8, lastExitedTick: 5),
                     })
            {
                Assert.Throws<InvalidOperationException>(
                    () => worldState.CreateWriteContext().SetEnemyGlideState(40, glideState),
                    glideState.Phase.ToString());
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_RecoveryOnSolid_DoesNotKeepSolidTraversalPrivilege()
        {
            var currentWallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(30, currentWallCell),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                40,
                currentWallCell,
                CreateActiveGlide(activeUntilTickExclusive: 3, durationTicks: 3, cooldownTicks: 1));

            Assert.Throws<InvalidOperationException>(
                () => worldState.CreateWriteContext().SetEnemyGlideState(
                    40,
                    CreateGlideState(
                        EnemyGlidePhase.Recovery,
                        activeUntilTickExclusive: 3,
                        recoveryUntilTickExclusive: 6,
                        durationTicks: 3,
                        recoveryTicks: 3,
                        hasLockedStep: true,
                        lockedStepX: 1,
                        lockedStepY: 0)));
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_StillBlocksReservationConflict()
        {
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(30, wallCell),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1));

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                    worldState.CreateSnapshot(),
                    EntityType.Unit,
                    wallCell,
                    40,
                    ReservationStatus.Conflicted).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_PreservesSolidBypass()
        {
            AssertGlideActiveKinematicPreservesSolidBypass(
                GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideActive_DefaultGameplay_PreservesSolidBypass()
        {
            AssertGlideActiveKinematicPreservesSolidBypass(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_WithCooldown_DoesNotCreateKinematicPayload()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var enemy = CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase);
            enemy.enemyLocomotionCooldownTicks = 3;
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                enemy,
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 10, durationTicks: 6, cooldownTicks: 0, recoveryTicks: 1));

            try
            {
                var pipeline = CreateGlideKinematicPipeline(profile, worldState);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var state), Is.True);
                Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_Active_PatrolUsesBaselineForwardPatrol()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Patrol),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 10,
                    durationTicks: 6,
                    cooldownTicks: 0,
                    recoveryTicks: 1,
                    lockedStepX: 0,
                    lockedStepY: 1));

            try
            {
                var pipeline = CreateGlideKinematicPipeline(profile, worldState);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var state), Is.True);
                Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));

                var scriptedWorldState = CreateWorldState(new[]
                {
                    CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Patrol),
                });
                scriptedWorldState.CreateWriteContext().SetEnemyGlideState(
                    40,
                    CreateActiveGlide(activeUntilTickExclusive: 10, durationTicks: 6, cooldownTicks: 0, recoveryTicks: 1));
                var scriptedIntent = new ScriptedMovementLogic(
                    new RawMovementIntent(40, priority: 100, destination: new Vector2Int(0, 1)));
                var scriptedPipeline = CreateGlideKinematicPipelineWithoutGeneratedEntityLogics(scriptedWorldState, scriptedIntent);
                var scriptedTick = scriptedPipeline.RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(scriptedTick, 40), Is.True);
                Assert.That(scriptedWorldState.CreateSnapshot().TryGetEntity(40, out var scriptedEnemy), Is.True);
                Assert.That(scriptedEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_IntentCreated_KinematicPayloadCreated()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 10, durationTicks: 6, cooldownTicks: 0, recoveryTicks: 1));

            try
            {
                var pipeline = CreateGlideKinematicPipeline(profile, worldState);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.True);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_Active_UsesGlideMoveDuration()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(
                    initialDelayTicks: 0,
                    windupTicks: 0,
                    durationTicks: 6,
                    recoveryTicks: 1,
                    cooldownTicks: 0,
                    glideMoveTicks: 4));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 10,
                    durationTicks: 6,
                    cooldownTicks: 0,
                    recoveryTicks: 1,
                    glideMoveTicks: 4));

            try
            {
                var tick = CreateGlideKinematicPipeline(profile, worldState).RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetUnitKinematicState(40, out var kinematicState), Is.True);
                Assert.That(kinematicState.totalTicks, Is.EqualTo(4));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Glider_Active_NoLockedStepDashFallback()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Patrol),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 10,
                    durationTicks: 6,
                    cooldownTicks: 0,
                    recoveryTicks: 1,
                    lockedStepX: 1,
                    lockedStepY: 0));
            ((IPreMovementStateCommitContext)writeContext).SetFacing(40, Direction.Up);

            try
            {
                var pipeline = CreateGlideKinematicPipeline(profile, worldState);

                var activeTick = pipeline.RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(activeTick, 40), Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }
        [Test]
        [Category("Extended")]
        public void GlideActive_StillBlocksDisallowedSolidOrTerrain()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var enemy = CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase);
            var worldState = CreateWorldState(
                new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                enemy,
            },
                new GameplayTerrainData(new[]
                {
                    new TerrainCellState(new SurfaceCell(FaceId.Floor, 1, 0), TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                }));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 10, durationTicks: 6, cooldownTicks: 0, recoveryTicks: 1));

            try
            {
                var scriptedIntent = new ScriptedMovementLogic(
                    new RawMovementIntent(40, priority: 100, destination: new Vector2Int(1, 0)));
                var pipeline = CreateGlideKinematicPipelineWithoutGeneratedEntityLogics(worldState, scriptedIntent);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var blockedEnemy), Is.True);
                Assert.That(blockedEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_StillBlocksBoardEdge()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 2, 0), EnemyAiMode.Patrol),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                GameplayTerrainData.Empty);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 10,
                    durationTicks: 6,
                    cooldownTicks: 0,
                    recoveryTicks: 1,
                    lockedStepX: 0,
                    lockedStepY: -1));

            try
            {
                var scriptedIntent = new ScriptedMovementLogic(
                    new RawMovementIntent(40, priority: 100, destination: new Vector2Int(2, -1)));
                var pipeline = CreateGlideKinematicPipelineWithoutGeneratedEntityLogics(worldState, scriptedIntent);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var state), Is.True);
                Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_StillBlocksTopologyTransition()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 1, 1), EnemyAiMode.Patrol),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 1)),
                GameplayTerrainData.Empty);
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 10,
                    durationTicks: 6,
                    cooldownTicks: 0,
                    recoveryTicks: 1,
                    lockedStepX: 0,
                    lockedStepY: 1));

            try
            {
                var scriptedIntent = new ScriptedMovementLogic(
                    new RawMovementIntent(40, priority: 100, destination: new Vector2Int(1, 2)));
                var pipeline = CreateGlideKinematicPipelineWithoutGeneratedEntityLogics(worldState, scriptedIntent);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var state), Is.True);
                Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_MovementUsesSameUnitStackingContractAsOrdinaryMove_SameTeam()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Patrol),
                CreateUnit(41, teamId: 2, destination, EnemyAiMode.None),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 10, durationTicks: 6, cooldownTicks: 0, recoveryTicks: 1));

            try
            {
                var snapshot = worldState.CreateSnapshot();
                Assert.That(
                    RuntimeTraversalLegalityPolicy.EvaluateDestination(
                        snapshot,
                        EntityType.Unit,
                        destination,
                        40,
                        snapshot.Topology,
                        CubeRotationKind.None,
                        snapshot.Topology).Verdict,
                    Is.EqualTo(LegalityVerdict.Allowed));
                var pipeline = CreateGlideKinematicPipeline(profile, worldState);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var state), Is.True);
                Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(destination));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_MovementUsesSameUnitStackingContractAsOrdinaryMove_NonPlayer()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Patrol),
                CreateUnit(41, teamId: 3, destination, EnemyAiMode.None),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 10, durationTicks: 6, cooldownTicks: 0, recoveryTicks: 1));

            try
            {
                var snapshot = worldState.CreateSnapshot();
                Assert.That(
                    RuntimeTraversalLegalityPolicy.EvaluateDestination(
                        snapshot,
                        EntityType.Unit,
                        destination,
                        40,
                        snapshot.Topology,
                        CubeRotationKind.None,
                        snapshot.Topology).Verdict,
                    Is.EqualTo(LegalityVerdict.Allowed));
                var pipeline = CreateGlideKinematicPipeline(profile, worldState);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(destination));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static void AssertGlideActiveKinematicPreservesSolidBypass(
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 6, recoveryTicks: 1, cooldownTicks: 0));
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateWall(30, wallCell),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(activeUntilTickExclusive: 10, durationTicks: 6, cooldownTicks: 0, recoveryTicks: 1));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(
                        worldState,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: runtimeFeatureFlags,
                        playerKinematicLocomotionTiming: CreateTwoTickKinematicTiming());

                var startTick = pipeline.RunTick(new TickInput(1));

                Assert.That(
                    startTick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Voluntary),
                    Is.True);
                LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(startTick, 40);
                var commitTick = HasGlideActiveKinematicAnchorCommit(startTick, 40)
                    ? startTick
                    : null;
                for (var tickIndex = 2; tickIndex <= 8; tickIndex++)
                {
                    if (commitTick != null)
                    {
                        break;
                    }

                    var tick = pipeline.RunTick(new TickInput(tickIndex));
                    LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(tick, 40);
                    if (HasGlideActiveKinematicAnchorCommit(tick, 40))
                    {
                        commitTick = tick;
                    }
                }

                Assert.That(commitTick, Is.Not.Null);
                LegacyMovementBoundaryAssert.HasMoveEntityBoundaryReason(
                    commitTick,
                    40,
                    MovementExecutionBoundaryKind.LocomotionAnchorCommit,
                    "GlideActiveKinematicAnchorCommit");
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(wallCell));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }
        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Glide_CurrentContract_ActiveCanAnchorOnSolid()
        {
            var solidCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(238, solidCell),
                CreateUnit(241, teamId: 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Chase),
            });

            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                241,
                solidCell,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 0));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(241, out var glider), Is.True);
            Assert.That(glider.position, Is.EqualTo(solidCell));
            Assert.That(snapshot.TryGetSolidOccupantAt(solidCell, out var solid), Is.True);
            Assert.That(solid.entityId, Is.EqualTo(238));
        }
        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Glide_Target_ActiveStillCanUseExistingMovementContract()
        {
            var anchorCell = new SurfaceCell(FaceId.Floor, 3, 7);
            var tick = RunActiveAnchorCommitScenario(
                new SurfaceCell(FaceId.Floor, 2, 7),
                anchorCell,
                includeSolidAtAnchor: true).Tick;

            Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 241), Is.True);
            Assert.That(HasMoveEntity(tick, 241, anchorCell), Is.True);
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Stage31_Glider241_ActiveCanPassOverWall238StepByStep()
        {
            var origin = new SurfaceCell(FaceId.Floor, 2, 7);
            var wall238Cell = new SurfaceCell(FaceId.Floor, 3, 7);
            var nextCell = new SurfaceCell(FaceId.Floor, 4, 7);
            var worldState = CreateStage31Wall238Glider241World(origin, wall238Cell);

            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                241,
                wall238Cell,
                CreateActiveGlide(activeUntilTickExclusive: 10, durationTicks: 10, cooldownTicks: 0));

            var snapshotOnWall = worldState.CreateSnapshot();
            Assert.That(snapshotOnWall.TryGetEntity(241, out var gliderOnWall), Is.True);
            Assert.That(gliderOnWall.position, Is.EqualTo(wall238Cell));
            Assert.That(snapshotOnWall.TryGetSolidOccupantAt(wall238Cell, out var wall), Is.True);
            Assert.That(wall.entityId, Is.EqualTo(238));

            Assert.DoesNotThrow(() => worldState.CreateWriteContext().MoveEntity(241, nextCell));
            var snapshotAfterWall = worldState.CreateSnapshot();
            Assert.That(snapshotAfterWall.TryGetEntity(241, out var gliderAfterWall), Is.True);
            Assert.That(gliderAfterWall.position, Is.EqualTo(nextCell));
            Assert.That(snapshotAfterWall.TryGetSolidOccupantAt(wall238Cell, out wall), Is.True);
            Assert.That(wall.entityId, Is.EqualTo(238));
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Stage31_Glider241_DoesNotEnterRecoverOnWall238()
        {
            var wall238Cell = new SurfaceCell(FaceId.Floor, 3, 7);
            var worldState = CreateStage31Wall238Glider241World(new SurfaceCell(FaceId.Floor, 2, 7), wall238Cell);
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 1, recoveryTicks: 1, cooldownTicks: 0));
            var logic = new EnemyLogic(241, profile);
            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                241,
                wall238Cell,
                CreateActiveGlide(activeUntilTickExclusive: 2, durationTicks: 1, cooldownTicks: 0, recoveryTicks: 1));

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 2);

                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(241, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Active));
                Assert.That(glideState.WantsRecover, Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(241, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(wall238Cell));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Stage31_Glider241_RecoversOnlyAfterNonSolidCell()
        {
            var wall238Cell = new SurfaceCell(FaceId.Floor, 3, 7);
            var nextCell = new SurfaceCell(FaceId.Floor, 4, 7);
            var worldState = CreateStage31Wall238Glider241World(new SurfaceCell(FaceId.Floor, 2, 7), wall238Cell);
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 1, recoveryTicks: 1, cooldownTicks: 0));
            var logic = new EnemyLogic(241, profile);
            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                241,
                wall238Cell,
                CreateActiveGlide(activeUntilTickExclusive: 2, durationTicks: 1, cooldownTicks: 0, recoveryTicks: 1));

            try
            {
                CommitPreMovement(logic, worldState, tickIndex: 2);
                Assert.DoesNotThrow(() => worldState.CreateWriteContext().MoveEntity(241, nextCell));
                CommitPreMovement(logic, worldState, tickIndex: 3);

                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(241, out var glideState), Is.True);
                Assert.That(glideState.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                Assert.That(worldState.CreateSnapshot().TryGetEntity(241, out var glider), Is.True);
                Assert.That(glider.position, Is.EqualTo(nextCell));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Glide_CurrentContract_PresentationSignalDoesNotOwnWorldState()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 2, recoveryTicks: 2, cooldownTicks: 0));
            var origin = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, origin, EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 2,
                    durationTicks: 2,
                    cooldownTicks: 0,
                    recoveryTicks: 2,
                    glideMoveTicks: 6));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(
                        worldState,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                        playerKinematicLocomotionTiming: CreateKinematicTiming(ticksPerCell: 6));

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.True);
                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(origin));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }
        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Glide_Target_Recovery_NoActiveOriginCommit()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 2, recoveryTicks: 2, cooldownTicks: 10));
            var origin = new SurfaceCell(FaceId.Floor, 0, 0);
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateUnit(40, teamId: 2, origin, EnemyAiMode.Chase),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                CreateActiveGlide(
                    activeUntilTickExclusive: 2,
                    durationTicks: 2,
                    cooldownTicks: 10,
                    recoveryTicks: 2,
                    glideMoveTicks: 6));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(
                        worldState,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                        playerKinematicLocomotionTiming: CreateKinematicTiming(ticksPerCell: 6));

                var startTick = pipeline.RunTick(new TickInput(1));
                Assert.That(startTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == 40), Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetUnitKinematicState(40, out var started), Is.True);
                Assert.That(started.elapsedTicks, Is.EqualTo(1));

                var activeEndTick = pipeline.RunTick(new TickInput(2));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyGlideState(40, out var recovery), Is.True);
                Assert.That(recovery.Phase, Is.EqualTo(EnemyGlidePhase.Recovery));
                Assert.That(HasGlideActiveKinematicAnchorCommit(activeEndTick, 40), Is.False);
                Assert.That(HasMoveEntity(activeEndTick, 40, destination), Is.False);
                Assert.That(HasGlideBoundaryKinematicClose(activeEndTick, 40), Is.True);
                Assert.That(worldState.CreateSnapshot().TryGetUnitKinematicState(40, out _), Is.False);
                LegacyMovementBoundaryAssert.NoFlagOnLegacyOrdinaryReadinessLeaks(activeEndTick, 40);

                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(origin));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Glide_Target_Cooldown_NoActiveOriginCommit()
        {
            var origin = new SurfaceCell(FaceId.Floor, 0, 0);
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, teamId: 2, origin, EnemyAiMode.Chase),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                40,
                CreateGlideState(
                    EnemyGlidePhase.Cooldown,
                    activeUntilTickExclusive: 2,
                    cooldownUntilTickExclusive: 10,
                    durationTicks: 2,
                    recoveryTicks: 0,
                    cooldownTicks: 8,
                    lastExitedTick: 2));
            writeContext.SetUnitKinematicState(
                40,
                CreateVoluntaryStepState(
                    origin,
                    elapsedTicks: 2,
                    totalTicks: 6,
                    startedTick: 1,
                    stepDirectionX: 1,
                    stepDirectionY: 0));

            var tick = CreateGlideKinematicPipelineWithoutGeneratedEntityLogics(worldState)
                .RunTick(new TickInput(3));

            Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.False);
            Assert.That(HasMoveEntity(tick, 40, destination), Is.False);
            Assert.That(HasGlideBoundaryKinematicClose(tick, 40), Is.True);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(origin));
            Assert.That(worldState.CreateSnapshot().TryGetUnitKinematicState(40, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_DoesNotCommitSolidAfterRecoveryGrounded()
        {
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 2, recoveryTicks: 4, cooldownTicks: 0));
            var origin = new SurfaceCell(FaceId.Floor, 0, 0);
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.None),
                CreateWall(30, wallCell),
                CreateUnit(40, teamId: 2, origin, EnemyAiMode.Chase),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                40,
                CreateGlideState(
                    EnemyGlidePhase.Recovery,
                    activeUntilTickExclusive: 2,
                    recoveryUntilTickExclusive: 6,
                    durationTicks: 2,
                    recoveryTicks: 4,
                    cooldownTicks: 0));
            writeContext.SetUnitKinematicState(
                40,
                CreateVoluntaryStepState(
                    origin,
                    elapsedTicks: 2,
                    totalTicks: 6,
                    startedTick: 1,
                    stepDirectionX: 1,
                    stepDirectionY: 0));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                    .CreateTickPipeline(
                        worldState,
                        Array.Empty<IEntityLogic>(),
                        GameplayTimingProfile.CreateDefault(),
                        CreatePlayerTiming(),
                        runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                        playerKinematicLocomotionTiming: CreateKinematicTiming(ticksPerCell: 6));

                var tick = pipeline.RunTick(new TickInput(3));

                Assert.That(
                    tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                        operation.Kind == FinalizationOperationKind.MoveEntity &&
                        operation.EntityId == 40 &&
                        operation.Destination == wallCell),
                    Is.False);
                Assert.That(HasGlideActiveKinematicAnchorCommit(tick, 40), Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(origin));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void MovementExpansion_FlipCanImpactActiveGliderSharingLandingCellWithSolid()
        {
            var landingCell = new SurfaceCell(FaceId.Floor, -1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.None),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip),
                CreateWall(30, landingCell),
                CreateUnit(40, teamId: 2, new SurfaceCell(FaceId.Floor, 3, 0), EnemyAiMode.Chase),
            });
            MoveGliderOntoSolidWithActiveAllowance(
                worldState,
                40,
                landingCell,
                CreateActiveGlide(activeUntilTickExclusive: 5, durationTicks: 3, cooldownTicks: 1, lockedStepX: -1, lockedStepY: 0));

            AssertFlipCreatesImpactOnTarget(worldState, 40);
        }

        [Test]
        [Category("Extended")]
        public void MovementExpansion_FlipCanImpactNonActiveGlidePhasesWhenTargetCellHasNoSolid()
        {
            var landingCell = new SurfaceCell(FaceId.Floor, -1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, teamId: 1, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.None),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip),
                CreateUnit(40, teamId: 2, landingCell, EnemyAiMode.Chase),
            });

            foreach (var glideState in new[]
                     {
                         CreateGlideState(EnemyGlidePhase.Windup, windupUntilTickExclusive: 5),
                         CreateGlideState(EnemyGlidePhase.Recovery, recoveryUntilTickExclusive: 5),
                         CreateGlideState(EnemyGlidePhase.Cooldown, cooldownUntilTickExclusive: 8, lastExitedTick: 5),
                     })
            {
                worldState.CreateWriteContext().SetEnemyGlideState(40, glideState);

                AssertFlipCreatesImpactOnTarget(worldState, 40);
            }
        }

        private static void CommitPreMovement(EnemyLogic logic, WorldState worldState, int tickIndex)
        {
            _ = CommitPreMovementAndGetUpdates(logic, worldState, tickIndex);
        }

        private static List<string> CommitPreMovementAndGetUpdates(EnemyLogic logic, WorldState worldState, int tickIndex)
        {
            var updates = new List<string>();
            ((IPreMovementStateLogic)logic).CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(tickIndex),
                worldState.CreateWriteContext(),
                updates,
                new List<PlayerActionTransition>());
            return updates;
        }

        private static List<RawMovementIntent> CollectMovementIntents(EnemyAiProfile profile, WorldState worldState)
        {
            var intents = new List<RawMovementIntent>();
            new EnemyLogic(40, profile).CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1),
                intents);
            return intents;
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreatePlayerTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateTwoTickKinematicTiming()
        {
            return CreateKinematicTiming(ticksPerCell: 2);
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateKinematicTiming(int ticksPerCell)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = ticksPerCell / (float)timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static TickPipeline CreateGlideKinematicPipeline(
            EnemyAiProfile profile,
            WorldState worldState,
            params IEntityLogic[] extraLogics)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile)
                .CreateTickPipeline(
                    worldState,
                    extraLogics ?? Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    CreatePlayerTiming(),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                    playerKinematicLocomotionTiming: CreateTwoTickKinematicTiming());
        }

        private static TickPipeline CreateGlideKinematicPipelineWithoutGeneratedEntityLogics(
            WorldState worldState,
            params IEntityLogic[] staticLogics)
        {
            return new GameplayBootstrapper(
                    new SnapshotEntityLogicProvider(Array.Empty<IEntityLogicFactory>()))
                .CreateTickPipeline(
                    worldState,
                    staticLogics ?? Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    CreatePlayerTiming(),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled,
                    playerKinematicLocomotionTiming: CreateTwoTickKinematicTiming());
        }

        private static (TickResult Tick, WorldState WorldState) RunActiveAnchorCommitScenario(
            SurfaceCell origin,
            SurfaceCell anchorCell,
            bool includeSolidAtAnchor)
        {
            var entities = includeSolidAtAnchor
                ? new[]
                {
                    CreateWall(238, anchorCell),
                    CreateUnit(241, teamId: 2, origin, EnemyAiMode.Chase),
                }
                : new[]
                {
                    CreateUnit(241, teamId: 2, origin, EnemyAiMode.Chase),
                };
            var worldState = CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 8)),
                GameplayTerrainData.Empty);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyGlideState(
                241,
                CreateGlideState(
                    EnemyGlidePhase.Active,
                    activeUntilTickExclusive: 10,
                    durationTicks: 10,
                    recoveryTicks: 2,
                    cooldownTicks: 0,
                    hasLockedStep: true,
                    lockedStepX: anchorCell.x - origin.x,
                    lockedStepY: anchorCell.y - origin.y));
            writeContext.SetUnitKinematicState(
                241,
                CreateVoluntaryStepState(
                    origin,
                    elapsedTicks: 2,
                    totalTicks: 6,
                    startedTick: 1,
                    stepDirectionX: anchorCell.x - origin.x,
                    stepDirectionY: anchorCell.y - origin.y));

            var pipeline = CreateGlideKinematicPipelineWithoutGeneratedEntityLogics(worldState);
            TickResult tick = null;
            Assert.DoesNotThrow(() => tick = pipeline.RunTick(new TickInput(3)));
            return (tick, worldState);
        }

        private static WorldState CreateStage31Wall238Glider241World(SurfaceCell gliderCell, SurfaceCell wall238Cell)
        {
            return CreateWorldState(
                new[]
                {
                    CreateWall(238, wall238Cell),
                    CreateUnit(241, teamId: 2, gliderCell, EnemyAiMode.Chase),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 8)),
                GameplayTerrainData.Empty);
        }

        private static bool HasMoveEntity(TickResult tick, int entityId, SurfaceCell destination)
        {
            return tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MoveEntity &&
                operation.EntityId == entityId &&
                operation.Destination == destination);
        }

        private static EnemyAiProfile CreateCrossLineGlideChaserProfile(
            EnemyGlideTimingSettings glideTimingSettings,
            int senseRange = 8)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                DetectionStrategyKind = DetectionStrategyKind.CrossLineOfSightOpponent,
                DetectionSettings = new DetectionSettings(
                    senseRange,
                    requireSameFace: true,
                    canTargetMarkedForDeath: false),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                MovementSkillStrategyKind = MovementSkillStrategyKind.GlideOverSolid,
                GlideTimingSettings = EnemyGlideTimingAuthoringSettings.FromRuntimeSettings(
                    glideTimingSettings,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });
        }

        private static bool HasGlideActiveKinematicAnchorCommit(TickResult tick, int entityId)
        {
            return tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.MoveEntity &&
                operation.EntityId == entityId &&
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LocomotionAnchorCommit &&
                operation.Metadata.BoundaryReason == "GlideActiveKinematicAnchorCommit");
        }

        private static bool HasGlideBoundaryKinematicClose(TickResult tick, int entityId)
        {
            return tick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.SetUnitKinematicState &&
                operation.EntityId == entityId &&
                operation.UnitKinematicState.IsSettledZero &&
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LocomotionAnchorCommit &&
                operation.Metadata.BoundaryReason == "EnemyGlideBoundaryKinematicClosed");
        }

        private static UnitKinematicRuntimeState CreateVoluntaryStepState(
            SurfaceCell anchor,
            int elapsedTicks,
            int totalTicks,
            int startedTick,
            int stepDirectionX,
            int stepDirectionY)
        {
            var resolution = KinematicProgressResolver.ResolvePose(
                anchor,
                stepDirectionX,
                stepDirectionY,
                elapsedTicks,
                totalTicks);
            return new UnitKinematicRuntimeState
            {
                localOffset = resolution.LocalOffset,
                velocity = new KinematicVelocity2(
                    KinematicFixed.FromRaw(stepDirectionX * KinematicFixed.UnitsPerCell / totalTicks),
                    KinematicFixed.FromRaw(stepDirectionY * KinematicFixed.UnitsPerCell / totalTicks)),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = resolution.RemainingDistanceUnits,
                remainingTicks = resolution.RemainingTicks,
                speedScalePermille = 1000,
                sequenceId = 1,
                elapsedTicks = elapsedTicks,
                totalTicks = totalTicks,
                commitTick = totalTicks / 2,
                startedTick = startedTick,
                stepDirectionX = stepDirectionX,
                stepDirectionY = stepDirectionY,
            }.NormalizedForStorage();
        }

        private static EnemyGlideRuntimeState CreateActiveGlide(
            int activeUntilTickExclusive,
            int durationTicks,
            int cooldownTicks,
            int recoveryTicks = 0,
            int glideMoveTicks = 2,
            bool wantsRecover = false,
            int lockedStepX = 1,
            int lockedStepY = 0,
            int lockedTargetEntityId = 0)
        {
            return CreateGlideState(
                EnemyGlidePhase.Active,
                activeUntilTickExclusive: activeUntilTickExclusive,
                durationTicks: durationTicks,
                recoveryTicks: recoveryTicks,
                cooldownTicks: cooldownTicks,
                glideMoveTicks: glideMoveTicks,
                wantsRecover: wantsRecover,
                hasLockedStep: true,
                lockedStepX: lockedStepX,
                lockedStepY: lockedStepY,
                lockedTargetEntityId: lockedTargetEntityId);
        }

        private static EnemyGlideRuntimeState CreateGlideState(
            EnemyGlidePhase phase,
            int sequence = 1,
            int windupUntilTickExclusive = 0,
            int activeUntilTickExclusive = 0,
            int recoveryUntilTickExclusive = 0,
            int cooldownUntilTickExclusive = 0,
            int windupTicks = 0,
            int durationTicks = 3,
            int recoveryTicks = 0,
            int cooldownTicks = 1,
            int glideMoveTicks = 2,
            bool wantsRecover = false,
            int lastExitedTick = 0,
            SurfaceCell landingPendingCell = default,
            bool hasLockedStep = false,
            int lockedStepX = 0,
            int lockedStepY = 0,
            int lockedTargetEntityId = 0)
        {
            return EnemyGlideRuntimeState.Create(
                phase,
                sequence,
                windupUntilTickExclusive,
                activeUntilTickExclusive,
                recoveryUntilTickExclusive,
                cooldownUntilTickExclusive,
                windupTicks,
                durationTicks,
                recoveryTicks,
                cooldownTicks,
                glideMoveTicks,
                lastExitedTick,
                wantsRecover,
                landingPendingCell,
                hasLockedStep,
                lockedStepX,
                lockedStepY,
                lockedTargetEntityId: lockedTargetEntityId);
        }

        private static void MoveGliderOntoSolidWithActiveAllowance(
            WorldState worldState,
            int entityId,
            SurfaceCell solidCell,
            EnemyGlideRuntimeState activeGlideState)
        {
            worldState.CreateWriteContext().SetEnemyGlideState(entityId, activeGlideState);
            worldState.CreateWriteContext().MoveEntity(entityId, solidCell);
        }
        private static void AssertBoxSlideBlockedBy(WorldState worldState, SurfaceCell origin, int expectedBlockerEntityId)
        {
            var resolved = worldState.CreateSnapshot().TryResolveNextSurfaceBoxSlideStep(
                origin,
                Vector2Int.right,
                out _,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(stopper.EntityId, Is.EqualTo(expectedBlockerEntityId));
        }

        private static List<ActionGroup> ExpandPushIntoBox(WorldState worldState)
        {
            var pushIntent = new PushIntent(10, priority: 50, destination: new Vector2Int(0, 0));
            pushIntent.AssignIntentId(1);
            var groups = new List<ActionGroup>();
            var rejected = new List<string>();

            new MovementExpander().Expand(
                worldState.CreateSnapshot(),
                new[] { pushIntent },
                groups,
                rejected);

            return groups;
        }

        private static void AssertFlipCreatesImpactOnTarget(WorldState worldState, int expectedImpactTargetId)
        {
            var flipIntent = new FlipIntent(10, priority: 50, destination: new Vector2Int(1, 0));
            flipIntent.AssignIntentId(1);
            var groups = new List<ActionGroup>();
            var rejected = new List<string>();

            new MovementExpander().Expand(
                worldState.CreateSnapshot(),
                new[] { flipIntent },
                groups,
                rejected);

            Assert.That(groups, Has.Count.EqualTo(1), string.Join("\n", rejected));
            Assert.That(groups[0].GroupKind, Is.EqualTo(ActionGroupKind.BoxImpact));
            Assert.That(groups[0].ImpactTargetId, Is.EqualTo(expectedImpactTargetId));
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities)
        {
            return CreateWorldState(entities, GameplayTerrainData.Empty);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities, GameplayTerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(4, 4)),
                terrainData);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> entities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, boardBounds, terrainData);
        }

        private static EntityState CreateUnit(int entityId, int teamId, Vector2Int position, EnemyAiMode aiMode, int hp = 3)
        {
            return CreateUnit(entityId, teamId, SurfaceCell.FromPlanar(position), aiMode, hp);
        }

        private static EntityState CreateUnit(int entityId, int teamId, SurfaceCell position, EnemyAiMode aiMode, int hp = 3)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = Math.Max(1, hp),
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = teamId == 1 ? UnitRole.Player : UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }

        private static EntityState CreateMarkedUnit(int entityId, int teamId, Vector2Int position)
        {
            var unit = CreateUnit(entityId, teamId, position, EnemyAiMode.None);
            unit.markedForDeath = true;
            return unit;
        }

        private static EntityState CreateDetachedUnit(int entityId, int teamId, Vector2Int position)
        {
            var unit = CreateUnit(entityId, teamId, position, EnemyAiMode.None);
            unit.boardPresence = EntityBoardPresence.Detached;
            return unit;
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = capabilities,
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

        private sealed class ScriptedMovementLogic : IMovementEntityLogic
        {
            private readonly RawMovementIntent _intent;

            public ScriptedMovementLogic(RawMovementIntent intent)
            {
                _intent = intent;
            }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                _ = snapshot;
                _ = input;
                buffer.Add(_intent);
            }
        }
    }
}
