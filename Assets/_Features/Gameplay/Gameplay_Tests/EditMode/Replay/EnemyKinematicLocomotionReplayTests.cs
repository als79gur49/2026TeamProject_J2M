using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class EnemyKinematicLocomotionReplayTests
    {
        [Test]
        [Category("Extended")]
        public void Replay_EnemySameFaceContinuousLocomotion_PassiveContactFiresOnFinalizedSameCellMove_IsDeterministic()
        {
            var inputs = Enumerable.Range(1, 10)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = EnemyAiProfileTestFactory.CreatePassiveContact();
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
        public void Replay_EnemySameFaceContinuousLocomotion_CooldownFallbackDuration_CommitsAtResolvedMidpoint()
        {
            var inputs = Enumerable.Range(1, 4)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = EnemyAiProfileTestFactory.CreatePassiveContact(moveCooldownTicks: 8);
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
                Assert.That(firstReplay[2].EventLogDump, Does.Not.Contain("DamageCommitted"));
                Assert.That(firstReplay[3].EventLogDump, Does.Contain("DamageCommitted"));
                Assert.That(firstReplay[3].EventLogDump, Does.Contain("SourceKind=PassiveContact"));
                Assert.That(firstReplay[3].Trace, Does.Contain("KinematicAnchorCommitted"));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Replay_EnemySameFaceContinuousLocomotion_OrdinaryKinematicDuration_ChangesDeterminismHash()
        {
            var inputs = new[] { new TickInput(1) };
            var fastProfile = CreatePassiveContactProfile(
                moveCooldownSeconds: 12f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                ordinaryKinematicMoveDurationSeconds: 4f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            var slowProfile = CreatePassiveContactProfile(
                moveCooldownSeconds: 12f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                ordinaryKinematicMoveDurationSeconds: 8f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            var harness = new TickReplayHarness();

            try
            {
                var fastReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(fastProfile),
                    CreateContactWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var slowReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(slowProfile),
                    CreateContactWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);

                Assert.That(fastReplay[0].DeterminismHash, Is.Not.EqualTo(slowReplay[0].DeterminismHash));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(fastProfile);
                EnemyAiProfileTestFactory.Destroy(slowProfile);
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
            var profile = EnemyAiProfileTestFactory.CreatePassiveContact();
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

        [Test]
        [Category("Extended")]
        public void Replay_EnemyGlideActiveKinematicLocomotion_IsDeterministic()
        {
            var inputs = Enumerable.Range(1, 4)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 3, recoveryTicks: 1, cooldownTicks: 0));
            var harness = new TickReplayHarness();

            try
            {
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateGlideWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateGlideWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);

                AssertEquivalentReplayOutputs(firstReplay, secondReplay);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("GlideActiveKinematicAnchorCommit", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("UnitKinematics", StringComparison.Ordinal) ||
                                             frame.EventLogDump.Contains("KinematicPoseCommitted", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("GenericUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                    Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void GlideActive_Kinematic_ReplayLandingDeterministic()
        {
            var inputs = Enumerable.Range(1, 13)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 12, recoveryTicks: 1, cooldownTicks: 0));
            var harness = new TickReplayHarness();

            try
            {
                var firstLandingReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateGlideLandingWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);
                var secondLandingReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateGlideLandingWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);

                AssertEquivalentReplayOutputs(firstLandingReplay, secondLandingReplay);
                // New 3-phase Glide keeps Active airborne state stable instead of forcing an
                // old anchor commit while the solid bypass/landing path is still resolving.
                Assert.That(firstLandingReplay[9].Trace, Does.Not.Contain("GlideActiveKinematicAnchorCommit"));
                Assert.That(firstLandingReplay[9].Trace, Does.Contain("Phase=Active|Active=1|WantsRecover=0"));
                Assert.That(firstLandingReplay[9].OccupancyDump, Does.Contain("Layer=Solid|Cell=(1,0)|E=30|Face=Floor"));
                Assert.That(firstLandingReplay[9].OccupancyDump, Does.Contain("Layer=Unit|Cell=(2,0)|E=40|Face=Floor"));
                Assert.That(firstLandingReplay[11].Trace, Does.Contain("Phase=Active|Active=1|WantsRecover=1"));
                Assert.That(
                    firstLandingReplay.Any(frame => frame.Trace.Contains("UnitKinematics", StringComparison.Ordinal) ||
                                                    frame.EventLogDump.Contains("KinematicPoseCommitted", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    firstLandingReplay.Any(frame => frame.Trace.Contains("GenericUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                    Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        [Category("GlideKinematicV11")]
        public void Replay_Determinism_GliderExpiredActiveOnSolidStable()
        {
            var inputs = Enumerable.Range(4, 5)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 0, durationTicks: 3, recoveryTicks: 1, cooldownTicks: 0));
            var harness = new TickReplayHarness();

            try
            {
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateExpiredActiveOnSolidWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateExpiredActiveOnSolidWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyGlideKinematicLocomotionEnabled);

                AssertEquivalentReplayOutputs(firstReplay, secondReplay);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("Phase=Active|Active=1|WantsRecover=1", StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_SettleWait_ReplayDeterministic()
        {
            var inputs = Enumerable.Range(1, 5)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = CreateChargeSettleWaitProfile();
            var harness = new TickReplayHarness();

            try
            {
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled);

                AssertEquivalentReplayOutputs(firstReplay, secondReplay);
                Assert.That(firstReplay[1].Trace, Does.Contain("EnemyChargeStartDeferred"));
                Assert.That(firstReplay[2].Trace, Does.Contain("Reason=ChargeStart"));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_SettleWait_TargetMovesDuringWait_ReplayDeterministic()
        {
            var inputs = Enumerable.Range(1, 5)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = CreateChargeSettleWaitProfile();
            var harness = new TickReplayHarness();
            var scriptedMoves = new Dictionary<int, RawMovementIntent>
            {
                { 2, new RawMovementIntent(10, priority: 100, destination: new Vector2Int(5, 1)) },
            };

            try
            {
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[] { new TickScriptedMovementLogic(10, scriptedMoves) },
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[] { new TickScriptedMovementLogic(10, scriptedMoves) },
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled);

                AssertEquivalentReplayOutputs(firstReplay, secondReplay);
                Assert.That(firstReplay[1].Trace, Does.Contain("EnemyChargeStartDeferred"));
                Assert.That(firstReplay[2].Trace, Does.Not.Contain("Reason=ChargeStart"));
                Assert.That(firstReplay[2].EnemyChargeDump, Does.Not.Contain("Phase=Active"));
                Assert.That(firstReplay[2].EnemyChargeDump, Does.Not.Contain("Phase=Windup"));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void Replay_DefaultGameplayLocomotion_NoUnexpectedLegacyOrdinaryMovement()
        {
            var harness = new TickReplayHarness();
            var playerInputs = Enumerable.Range(1, 6)
                .Select(tick => new TickInput(tick, PlayerTickCommand.Move(Direction.Right)))
                .ToArray();
            var firstPlayerReplay = harness.Run(
                GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                }),
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                playerInputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            var secondPlayerReplay = harness.Run(
                GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                }),
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                playerInputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

            AssertReplayBoundaryCanaryEqual(firstPlayerReplay, secondPlayerReplay);
            Assert.That(
                firstPlayerReplay[0].DeterminismHash,
                Is.Not.EqualTo(firstPlayerReplay[firstPlayerReplay.Count - 1].DeterminismHash));

            var enemyInputs = Enumerable.Range(1, 10)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var enemyProfile = EnemyAiProfileTestFactory.CreatePassiveContact();
            try
            {
                var firstEnemyReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(enemyProfile),
                    CreateContactWorldState(),
                    entityLogics: new IEntityLogic[0],
                    enemyInputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
                var secondEnemyReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(enemyProfile),
                    CreateContactWorldState(),
                    entityLogics: new IEntityLogic[0],
                    enemyInputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

                AssertReplayBoundaryCanaryEqual(firstEnemyReplay, secondEnemyReplay);
                Assert.That(firstEnemyReplay.Any(frame => frame.Trace.Contains("KinematicAnchorCommitted", StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(enemyProfile);
            }

            var chargeInputs = Enumerable.Range(1, 5)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var chargeProfile = CreateChargeSettleWaitProfile();
            try
            {
                var firstChargeReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(chargeProfile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[0],
                    chargeInputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
                var secondChargeReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(chargeProfile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[0],
                    chargeInputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

                AssertReplayBoundaryCanaryEqual(firstChargeReplay, secondChargeReplay);
                Assert.That(firstChargeReplay.Any(frame => frame.Trace.Contains("Reason=ChargeStart", StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(chargeProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void Replay_DeprecationPhase1_DefaultGameplayLocomotion_NoCoveredGenericExpansionOwned()
        {
            Replay_DefaultGameplayLocomotion_NoUnexpectedLegacyOrdinaryMovement();
        }

        [Test]
        [Category("Extended")]
        public void Replay_MoveOwnership_NoCoveredFallbackMove()
        {
            Replay_DefaultGameplayLocomotion_NoUnexpectedLegacyOrdinaryMovement();
        }

        [Test]
        [Category("Core")]
        public void Replay_ScopedDeletionPrep_NoCoveredFallbackInDefaultGameplayLocomotion()
        {
            Replay_DefaultGameplayLocomotion_NoUnexpectedLegacyOrdinaryMovement();
        }

        [Test]
        [Category("Core")]
        public void Replay_ScopedDeletionPrep_PlayerEnemyChargeRemoved()
        {
            Replay_Phase8C_CurrentOwnershipBaseline_DiagnosticsDeterministic();
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase6_CurrentOwnershipBaseline_PlayerEnemyChargeRemoved()
        {
            Replay_Phase8C_CurrentOwnershipBaseline_DiagnosticsDeterministic();
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase8A_DiagnosticBaseline_Deterministic()
        {
            Replay_Phase8C_CurrentOwnershipBaseline_DiagnosticsDeterministic();
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase8B_CurrentOwnershipBaseline_DiagnosticsDeterministic()
        {
            AssertCurrentOwnershipBaselineReplayDeterministic(
                GameplayRuntimeFeatureFlags.None);
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase8B_DefaultGameplay_NoCoveredFallback()
        {
            Replay_Phase7_DefaultGameplay_NoCoveredFallback();
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase8C_CurrentOwnershipBaseline_DiagnosticsDeterministic()
        {
            AssertCurrentOwnershipBaselineReplayDeterministic(
                GameplayRuntimeFeatureFlags.None);
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase8C_DefaultGameplay_NoCoveredFallback()
        {
            Replay_Phase7_DefaultGameplay_NoCoveredFallback();
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase8D_CurrentOwnershipHelper_DiagnosticsDeterministic()
        {            AssertCurrentOwnershipBaselineReplayDeterministic(
                GameplayRuntimeFeatureFlags.None);
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase8E_CurrentOwnershipRouting_Deterministic()
        {            AssertCurrentOwnershipBaselineReplayDeterministic(
                GameplayRuntimeFeatureFlags.None);
        }

        [Test]
        [Category("Core")]
        public void Replay_FallbackWrapperCleanup_DiagnosticsStillDeterministic()
        {
            AssertCurrentOwnershipBaselineReplayDeterministic(
                GameplayRuntimeFeatureFlags.None);
        }

        [Test]
        [Category("Core")]
        public void Replay_CompatibilityLayer_DiagnosticBaseline_StillDeterministic()
        {            AssertCurrentOwnershipBaselineReplayDeterministic(
                GameplayRuntimeFeatureFlags.None);
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase7_GenericExpansionOwnedBaseline_DiagnosticsDeterministic()
        {
            Replay_Phase8C_CurrentOwnershipBaseline_DiagnosticsDeterministic();
        }

        private static void AssertCurrentOwnershipBaselineReplayDeterministic(
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var harness = new TickReplayHarness();
            var playerInputs = new[] { new TickInput(1, PlayerTickCommand.Move(Direction.Right)) };
            var firstPlayerReplay = harness.Run(
                CreatePlayerFallbackWorldState(),
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                playerInputs,
                runtimeFeatureFlags: runtimeFeatureFlags);
            var secondPlayerReplay = harness.Run(
                CreatePlayerFallbackWorldState(),
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                playerInputs,
                runtimeFeatureFlags: runtimeFeatureFlags);

            AssertReplayCanonicalStateEqual(firstPlayerReplay, secondPlayerReplay);
            Assert.That(
                firstPlayerReplay.Select(frame => frame.Trace).ToArray(),
                Is.EqualTo(secondPlayerReplay.Select(frame => frame.Trace).ToArray()));
            Assert.That(firstPlayerReplay.Any(frame => frame.Trace.Contains("Boundary=GenericExpansionOwned", StringComparison.Ordinal)), Is.False);
            Assert.That(
                firstPlayerReplay.Any(frame =>
                    frame.Trace.Contains(MovementExecutionOwnershipAssert.PlayerGenericExpansionOwnedRemovedReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(MovementExecutionOwnershipAssert.PlayerGenericExpansionOwnedRemovedReason, StringComparison.Ordinal)),
                Is.True);

            var enemyInputs = new[] { new TickInput(1) };
            var enemyMove = new Dictionary<int, RawMovementIntent>
            {
                { 1, new RawMovementIntent(40, priority: 50, destination: new Vector2Int(1, 0)) },
            };
            var firstEnemyReplay = harness.Run(
                GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                }),
                new IEntityLogic[] { new TickScriptedMovementLogic(40, enemyMove) },
                enemyInputs,
                runtimeFeatureFlags: runtimeFeatureFlags);
            var secondEnemyReplay = harness.Run(
                GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                }),
                new IEntityLogic[] { new TickScriptedMovementLogic(40, enemyMove) },
                enemyInputs,
                runtimeFeatureFlags: runtimeFeatureFlags);

            AssertReplayCanonicalStateEqual(firstEnemyReplay, secondEnemyReplay);
            Assert.That(
                firstEnemyReplay.Select(frame => frame.Trace).ToArray(),
                Is.EqualTo(secondEnemyReplay.Select(frame => frame.Trace).ToArray()));
            Assert.That(firstEnemyReplay.Any(frame => frame.Trace.Contains("Boundary=GenericExpansionOwned", StringComparison.Ordinal)), Is.False);
            Assert.That(
                firstEnemyReplay.Any(frame =>
                    frame.Trace.Contains(MovementExecutionOwnershipAssert.EnemyGenericExpansionOwnedRemovedReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(MovementExecutionOwnershipAssert.EnemyGenericExpansionOwnedRemovedReason, StringComparison.Ordinal)),
                Is.True);

            var chargeMove = new Dictionary<int, RawMovementIntent>
            {
                { 1, new RawMovementIntent(50, priority: 50, destination: new Vector2Int(1, 0)) },
            };
            var firstChargeReplay = harness.Run(
                CreateActiveChargeFallbackWorldState(),
                new IEntityLogic[] { new TickScriptedMovementLogic(50, chargeMove) },
                enemyInputs,
                runtimeFeatureFlags: runtimeFeatureFlags);
            var secondChargeReplay = harness.Run(
                CreateActiveChargeFallbackWorldState(),
                new IEntityLogic[] { new TickScriptedMovementLogic(50, chargeMove) },
                enemyInputs,
                runtimeFeatureFlags: runtimeFeatureFlags);

            AssertReplayCanonicalStateEqual(firstChargeReplay, secondChargeReplay);
            Assert.That(
                firstChargeReplay.Select(frame => frame.Trace).ToArray(),
                Is.EqualTo(secondChargeReplay.Select(frame => frame.Trace).ToArray()));
            Assert.That(firstChargeReplay.Any(frame => frame.Trace.Contains("Boundary=GenericExpansionOwned", StringComparison.Ordinal)), Is.False);
            Assert.That(
                firstChargeReplay.Any(frame =>
                    frame.Trace.Contains(MovementExecutionOwnershipAssert.ChargeGenericExpansionOwnedRemovedReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(MovementExecutionOwnershipAssert.ChargeGenericExpansionOwnedRemovedReason, StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase3_None_NoCoveredFallback()
        {
            var enemyReplay = RunScriptedEnemyOrdinaryReplay(GameplayRuntimeFeatureFlags.None, out var secondEnemyReplay);
            AssertReplayCanonicalStateEqual(enemyReplay, secondEnemyReplay);
            Assert.That(enemyReplay.Any(frame => frame.Trace.Contains("Boundary=GenericExpansionOwned", StringComparison.Ordinal)), Is.False);
            Assert.That(
                enemyReplay.Any(frame =>
                    frame.Trace.Contains(MovementExecutionOwnershipAssert.ExplicitGenericExpansionOwnedRequiredReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(MovementExecutionOwnershipAssert.ExplicitGenericExpansionOwnedRequiredReason, StringComparison.Ordinal)),
                Is.True);

            var chargeReplay = RunScriptedChargeActiveReplay(GameplayRuntimeFeatureFlags.None, out var secondChargeReplay);
            AssertReplayCanonicalStateEqual(chargeReplay, secondChargeReplay);
            Assert.That(chargeReplay.Any(frame => frame.Trace.Contains("Boundary=GenericExpansionOwned", StringComparison.Ordinal)), Is.False);
            Assert.That(
                chargeReplay.Any(frame =>
                    frame.Trace.Contains(MovementExecutionOwnershipAssert.ExplicitGenericExpansionOwnedRequiredReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(MovementExecutionOwnershipAssert.ExplicitGenericExpansionOwnedRequiredReason, StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase3_DefaultGameplay_NoCoveredGenericExpansionOwned()
        {
            Replay_Phase7_DefaultGameplay_NoCoveredFallback();
        }

        [Test]
        [Category("Core")]
        public void Replay_Phase7_DefaultGameplay_NoCoveredFallback()
        {
            Replay_DefaultGameplayLocomotion_NoUnexpectedLegacyOrdinaryMovement();
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase2B_EnemyDefaultGameplayLocomotion_NoGenericExpansionOwned()
        {
            var replay = RunScriptedEnemyOrdinaryReplay(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion, out var secondReplay);

            AssertReplayBoundaryCanaryEqual(replay, secondReplay);
            AssertEnemyReplayHasNoGenericExpansionOwned(replay);
            Assert.That(
                replay.Any(frame => frame.Trace.Contains("Kinematic", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("KinematicPose", StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase5_DefaultGameplay_NoEnemyGenericExpansionOwned()
        {
            Replay_Phase2B_EnemyDefaultGameplayLocomotion_NoGenericExpansionOwned();
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase2B_EnemyKinematicFlagOn_NoGenericExpansionOwned()
        {
            var replay = RunScriptedEnemyOrdinaryReplay(GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled, out var secondReplay);

            AssertReplayBoundaryCanaryEqual(replay, secondReplay);
            AssertEnemyReplayHasNoGenericExpansionOwned(replay);
            Assert.That(
                replay.Any(frame => frame.Trace.Contains("Kinematic", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("KinematicPose", StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase5_EnemyCurrentOwnershipBaseline_FallbackRemoved()
        {
            var replay = RunScriptedEnemyOrdinaryReplay(
                GameplayRuntimeFeatureFlags.None,
                out var secondReplay);

            AssertReplayCanonicalStateEqual(replay, secondReplay);
            Assert.That(
                replay.Select(frame => frame.Trace).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.Trace).ToArray()));
            Assert.That(replay.Any(frame => frame.Trace.Contains("Boundary=GenericExpansionOwned", StringComparison.Ordinal)), Is.False);
            Assert.That(
                replay.Any(frame =>
                    frame.Trace.Contains(MovementExecutionOwnershipAssert.EnemyGenericExpansionOwnedRemovedReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(MovementExecutionOwnershipAssert.EnemyGenericExpansionOwnedRemovedReason, StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase2C_ChargeDefaultGameplayLocomotion_NoChargeGenericExpansionFallback()
        {
            var replay = RunScriptedChargeActiveReplay(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion, out var secondReplay);

            AssertReplayBoundaryCanaryEqual(replay, secondReplay);
            AssertChargeReplayHasNoChargeFallback(replay);
            Assert.That(
                replay.Any(frame => frame.Trace.Contains("Kinematic", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("KinematicPose", StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase2C_ChargeKinematicFlagOn_NoChargeGenericExpansionFallback()
        {
            var replay = RunScriptedChargeActiveReplay(GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled, out var secondReplay);

            AssertReplayBoundaryCanaryEqual(replay, secondReplay);
            AssertChargeReplayHasNoChargeFallback(replay);
            Assert.That(
                replay.Any(frame => frame.Trace.Contains("Kinematic", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("KinematicPose", StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase6_ChargeKinematicFlagOn_NoLegacyChargeEntityMotionOutput()
        {
            Replay_Phase2C_ChargeKinematicFlagOn_NoChargeGenericExpansionFallback();
        }

        [Test]
        [Category("Extended")]
        // Historical/pre-Phase6 canary: delegates to the canonical Charge removed-diagnostic replay.
        public void Replay_Phase2C_ChargeFlagOffGenericExpansionOwned_BaselineDocumented()
        {
            Replay_Phase6_ChargeCurrentOwnershipBaseline_FallbackRemoved();
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase6_ChargeCurrentOwnershipBaseline_FallbackRemoved()
        {
            var replay = RunScriptedChargeActiveReplay(
                GameplayRuntimeFeatureFlags.None,
                out var secondReplay);

            AssertReplayCanonicalStateEqual(replay, secondReplay);
            Assert.That(
                replay.Select(frame => frame.Trace).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.Trace).ToArray()));
            Assert.That(replay.Any(frame => frame.Trace.Contains("Boundary=GenericExpansionOwned", StringComparison.Ordinal)), Is.False);
            Assert.That(
                replay.Any(frame =>
                    frame.Trace.Contains(MovementExecutionOwnershipAssert.ChargeGenericExpansionOwnedRemovedReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(MovementExecutionOwnershipAssert.ChargeGenericExpansionOwnedRemovedReason, StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase6_DefaultGameplay_NoLegacyChargeEntityMotionOutput()
        {
            Replay_Phase2C_ChargeDefaultGameplayLocomotion_NoChargeGenericExpansionFallback();
        }

        [Test]
        [Category("Core")]
        public void Replay_NoLegacyChargeEntityMotionOutput_DefaultGameplay()
        {
            var replay = RunScriptedChargeActiveReplay(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion, out var secondReplay);

            AssertReplayBoundaryCanaryEqual(replay, secondReplay);
            AssertChargeReplayHasNoChargeFallback(replay);
        }

        [Test]
        [Category("Core")]
        public void Replay_LegacyChargeEntityMotionOutputDeletion_NoOutput()
        {
            Replay_NoLegacyChargeEntityMotionOutput_DefaultGameplay();
        }

        [Test]
        [Category("Core")]
        public void Replay_NoLegacyChargeEntityMotionOutput_DiagnosticBaseline()
        {
            var replay = RunScriptedChargeActiveReplay(
                GameplayRuntimeFeatureFlags.None,
                out var secondReplay);

            AssertReplayCanonicalStateEqual(replay, secondReplay);
            Assert.That(
                replay.Select(frame => frame.Trace).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.Trace).ToArray()));
            AssertReplayHasNoLegacyChargeEntityMotionOutput(replay);
            Assert.That(replay.Any(frame => frame.Trace.Contains("Boundary=GenericExpansionOwned", StringComparison.Ordinal)), Is.False);
            Assert.That(
                replay.Any(frame =>
                    frame.Trace.Contains(MovementExecutionOwnershipAssert.ChargeGenericExpansionOwnedRemovedReason, StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains(MovementExecutionOwnershipAssert.ChargeGenericExpansionOwnedRemovedReason, StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void Replay_LegacyChargeEntityMotionOutputProducer_RuntimeMatrix_NoOutput()
        {
            var defaultReplay = RunScriptedChargeActiveReplay(
                GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                out var secondDefaultReplay);
            AssertReplayBoundaryCanaryEqual(defaultReplay, secondDefaultReplay);
            AssertChargeReplayHasNoChargeFallback(defaultReplay);

            var chargeFlagReplay = RunScriptedChargeActiveReplay(
                GameplayRuntimeFeatureFlags.EnemyChargeKinematicLocomotionEnabled,
                out var secondChargeFlagReplay);
            AssertReplayBoundaryCanaryEqual(chargeFlagReplay, secondChargeFlagReplay);
            AssertChargeReplayHasNoChargeFallback(chargeFlagReplay);

            var allKinematicReplay = RunScriptedChargeActiveReplay(
                GameplayRuntimeFeatureFlags.AllKinematicLocomotionEnabled,
                out var secondAllKinematicReplay);
            AssertReplayBoundaryCanaryEqual(allKinematicReplay, secondAllKinematicReplay);
            AssertChargeReplayHasNoChargeFallback(allKinematicReplay);
        }

        [Test]
        [Category("Core")]
        public void Replay_LegacyChargeEntityMotionOutputIsolation_NoRuntimeOutput()
        {
            Replay_LegacyChargeEntityMotionOutputProducer_RuntimeMatrix_NoOutput();
        }

        [Test]
        [Category("Core")]
        public void Replay_LegacyChargeEntityMotionOutputProducer_DiagnosticBaseline_NoOutput()
        {
            Replay_NoLegacyChargeEntityMotionOutput_DiagnosticBaseline();
        }

        [Test]
        [Category("Core")]
        public void Replay_LegacyChargeEntityMotionOutputIsolation_DiagnosticBaseline_NoOutput()
        {
            Replay_LegacyChargeEntityMotionOutputProducer_DiagnosticBaseline_NoOutput();
        }

        [Test]
        [Category("Extended")]
        public void Replay_Phase6_ChargeKinematicAnchorCommit_UsesMoveEntityButNoLegacyChargeEntityMotionOutput()
        {
            var inputs = Enumerable.Range(1, 5)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var profile = CreateChargeSettleWaitProfile();
            var harness = new TickReplayHarness();

            try
            {
                var replay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(profile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled);

                AssertReplayBoundaryCanaryEqual(replay, secondReplay);
                Assert.That(replay.Any(frame => frame.Trace.Contains("KinematicAnchorCommitted", StringComparison.Ordinal)), Is.True);
                AssertChargeReplayHasNoChargeFallback(replay);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void Replay_DefaultGameplayLocomotion_GlideKinematic_IsDeterministic()
        {
            var flags = GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion;
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.True);

            var harness = new TickReplayHarness();
            var inputs = Enumerable.Range(1, 4)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 3, recoveryTicks: 1, cooldownTicks: 1));

            try
            {
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile),
                    CreateGlideWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: flags);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile),
                    CreateGlideWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: flags);

                AssertReplayBoundaryCanaryEqual(firstReplay, secondReplay);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("GlideActiveKinematicAnchorCommit", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("UnitKinematics", StringComparison.Ordinal) ||
                                             frame.EventLogDump.Contains("KinematicPoseCommitted|E=40", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("GenericExpansionOwned", StringComparison.Ordinal)),
                    Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void Replay_GlideFlagOff_FallbackStillDeterministic()
        {
            var flags = GameplayRuntimeFeatureFlags.None;
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.False);

            var harness = new TickReplayHarness();
            var inputs = Enumerable.Range(1, 3)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 2, recoveryTicks: 1, cooldownTicks: 1));

            try
            {
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile),
                    CreateGlideWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: flags);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile),
                    CreateGlideWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: flags);

                AssertReplayBoundaryCanaryEqual(firstReplay, secondReplay);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("GlideActiveKinematicAnchorCommit", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(
                    firstReplay.Any(frame => frame.EventLogDump.Contains("KinematicPoseCommitted|E=40", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("GenericExpansionOwned", StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void Replay_DeprecationPhase1_GlideRetainedException_IsDeterministic()
        {
            Replay_GlideFlagOff_FallbackStillDeterministic();
        }

        [Test]
        [Category("Core")]
        public void BoundaryInventory_SpecialMovement_ReplayCanary()
        {
            var harness = new TickReplayHarness();
            var jumpProfile = EnemyAiProfileTestFactory.CreateJumpChaser(
                new EnemyJumpTimingSettings(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1));
            try
            {
                var inputs = new[] { new TickInput(1) };
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(jumpProfile),
                    CreateJumpLandingWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(jumpProfile),
                    CreateJumpLandingWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

                AssertReplayBoundaryCanaryEqual(firstReplay, secondReplay);
                Assert.That(
                    firstReplay.Any(frame => frame.Trace.Contains("Boundary=UnitSpecialLocomotion", StringComparison.Ordinal)),
                    Is.True);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(jumpProfile);
            }

            var glideProfile = EnemyAiProfileTestFactory.CreateGlideChaser(
                new EnemyGlideTimingSettings(windupTicks: 1, durationTicks: 2, recoveryTicks: 1, cooldownTicks: 1));
            try
            {
                var inputs = Enumerable.Range(1, 3)
                    .Select(tick => new TickInput(tick))
                    .ToArray();
                var firstReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile),
                    CreateGlideWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
                var secondReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(glideProfile),
                    CreateGlideWorldState(),
                    entityLogics: new IEntityLogic[0],
                    inputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);

                AssertReplayBoundaryCanaryEqual(firstReplay, secondReplay);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(glideProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Replay_MoveOwnership_RetainedMoveStillDeterministic()
        {
            BoundaryInventory_SpecialMovement_ReplayCanary();
        }

        [Test]
        [Category("Core")]
        public void Replay_NoUnexpectedGenericUnitOrdinaryMovementDetected()
        {
            var harness = new TickReplayHarness();
            var playerInputs = Enumerable.Range(1, 6)
                .Select(tick => new TickInput(tick, PlayerTickCommand.Move(Direction.Right)))
                .ToArray();
            var firstPlayerReplay = harness.Run(
                GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                }),
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                playerInputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);
            var secondPlayerReplay = harness.Run(
                GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                }),
                new IEntityLogic[] { new PlayerLogic(10), new PlayerControlStateLogic(10) },
                playerInputs,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            AssertReplayBoundaryCanaryEqual(firstPlayerReplay, secondPlayerReplay);

            var enemyInputs = Enumerable.Range(1, 6)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var enemyProfile = EnemyAiProfileTestFactory.CreatePassiveContact();
            try
            {
                var firstEnemyReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(enemyProfile),
                    CreateContactWorldState(),
                    entityLogics: new IEntityLogic[0],
                    enemyInputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
                var secondEnemyReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(enemyProfile),
                    CreateContactWorldState(),
                    entityLogics: new IEntityLogic[0],
                    enemyInputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);

                AssertReplayBoundaryCanaryEqual(firstEnemyReplay, secondEnemyReplay);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(enemyProfile);
            }

            var chargeInputs = Enumerable.Range(1, 5)
                .Select(tick => new TickInput(tick))
                .ToArray();
            var chargeProfile = CreateChargeSettleWaitProfile();
            try
            {
                var firstChargeReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(chargeProfile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[0],
                    chargeInputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled);
                var secondChargeReplay = harness.Run(
                    GameplayCompositionRoot.CreateDefaultBootstrapper(chargeProfile),
                    CreateChargeSettleWaitWorldState(),
                    entityLogics: new IEntityLogic[0],
                    chargeInputs,
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemyAndChargeKinematicLocomotionEnabled);

                AssertReplayBoundaryCanaryEqual(firstChargeReplay, secondChargeReplay);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(chargeProfile);
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
            Assert.That(
                firstReplay.Any(frame => frame.Trace.Contains("GenericUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                Is.False);
        }

        private static void AssertEnemyReplayHasNoGenericExpansionOwned(IReadOnlyList<TickReplayFrame> replay)
        {
            Assert.That(
                replay.Any(frame => frame.Trace.Contains("GenericExpansionOwned", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("GenericExpansionOwned", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                replay.Any(frame => frame.Trace.Contains("GenericUnitOrdinaryMovementDetected", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("GenericUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                Is.False);
        }

        private static IReadOnlyList<TickReplayFrame> RunScriptedEnemyOrdinaryReplay(
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            out IReadOnlyList<TickReplayFrame> secondReplay)
        {
            var harness = new TickReplayHarness();
            var inputs = new[] { new TickInput(1) };
            var enemyMove = new Dictionary<int, RawMovementIntent>
            {
                { 1, new RawMovementIntent(40, priority: 50, destination: new Vector2Int(1, 0)) },
            };

            var firstReplay = harness.Run(
                GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                }),
                new IEntityLogic[] { new TickScriptedMovementLogic(40, enemyMove) },
                inputs,
                runtimeFeatureFlags: runtimeFeatureFlags);
            secondReplay = harness.Run(
                GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                }),
                new IEntityLogic[] { new TickScriptedMovementLogic(40, enemyMove) },
                inputs,
                runtimeFeatureFlags: runtimeFeatureFlags);

            return firstReplay;
        }

        private static void AssertChargeReplayHasNoChargeFallback(IReadOnlyList<TickReplayFrame> replay)
        {
            Assert.That(
                replay.Any(frame => frame.Trace.Contains("GenericExpansionOwned", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("GenericExpansionOwned", StringComparison.Ordinal) ||
                                    frame.Trace.Contains("ChargeMove", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("ChargeMove", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                replay.Any(frame => frame.Trace.Contains("GenericUnitOrdinaryMovementDetected", StringComparison.Ordinal) ||
                                    frame.EventLogDump.Contains("GenericUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                Is.False);
        }

        private static void AssertReplayHasNoLegacyChargeEntityMotionOutput(IReadOnlyList<TickReplayFrame> replay)
        {
            Assert.That(
                replay.Any(frame =>
                    frame.Trace.Contains("ChargeMove", StringComparison.Ordinal) ||
                    frame.EventLogDump.Contains("ChargeMove", StringComparison.Ordinal)),
                Is.False);
        }

        private static IReadOnlyList<TickReplayFrame> RunScriptedChargeActiveReplay(
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            out IReadOnlyList<TickReplayFrame> secondReplay)
        {
            var harness = new TickReplayHarness();
            var inputs = new[] { new TickInput(1) };
            var chargeMove = new Dictionary<int, RawMovementIntent>
            {
                { 1, new RawMovementIntent(50, priority: 50, destination: new Vector2Int(1, 0)) },
            };

            var firstReplay = harness.Run(
                CreateActiveChargeFallbackWorldState(),
                new IEntityLogic[] { new TickScriptedMovementLogic(50, chargeMove) },
                inputs,
                runtimeFeatureFlags: runtimeFeatureFlags);
            secondReplay = harness.Run(
                CreateActiveChargeFallbackWorldState(),
                new IEntityLogic[] { new TickScriptedMovementLogic(50, chargeMove) },
                inputs,
                runtimeFeatureFlags: runtimeFeatureFlags);

            return firstReplay;
        }

        private static void AssertReplayBoundaryCanaryEqual(
            IReadOnlyList<TickReplayFrame> firstReplay,
            IReadOnlyList<TickReplayFrame> secondReplay)
        {
            AssertReplayCanonicalStateEqual(firstReplay, secondReplay);
            Assert.That(
                firstReplay.Select(frame => frame.Trace).ToArray(),
                Is.EqualTo(secondReplay.Select(frame => frame.Trace).ToArray()));
            Assert.That(
                firstReplay.Any(frame => frame.Trace.Contains("GenericUnitOrdinaryMovementDetected", StringComparison.Ordinal)),
                Is.False);
        }

        private static void AssertReplayCanonicalStateEqual(
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

        private static WorldState CreatePlayerFallbackWorldState()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            return worldState;
        }

        private static WorldState CreateDeathWorldState()
        {
            return GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateEnemy(40, hp: 1, new SurfaceCell(FaceId.Floor, 1, 0)),
            });
        }

        private static WorldState CreateChargeSettleWaitWorldState()
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreatePlayer(10, hp: 5, new SurfaceCell(FaceId.Floor, 5, 0)),
                    CreateChargePatrolEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
                    new EntityState
                    {
                        entityId = 50,
                        position = new SurfaceCell(FaceId.Floor, 6, 0),
                        hp = 1,
                        maxHp = 1,
                        teamId = 0,
                        type = EntityType.Box,
                        boardPresence = EntityBoardPresence.Occupying,
                    },
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 1)));
        }

        private static WorldState CreateJumpLandingWorldState()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 3, 0)),
                CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0),
                    windupEndTick = 0,
                    landingTick = 1,
                });
            writeContext.SetBoardPresence(40, EntityBoardPresence.Detached);
            return worldState;
        }

        private static WorldState CreateGlideWorldState()
        {
            return GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 3, 0)),
                CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
            });
        }

        private static WorldState CreateActiveChargeFallbackWorldState()
        {
            var chargeEnemy = CreateEnemy(50, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0));
            chargeEnemy.aiMode = EnemyAiMode.Charge;
            chargeEnemy.facing = Direction.Right;
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { chargeEnemy });
            worldState.CreateWriteContext().SetEnemyChargeState(
                50,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });
            return worldState;
        }

        private static WorldState CreateGlideContactWorldState()
        {
            var landingCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, hp: 3, landingCell),
                CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            SeedActiveGlide(worldState, 40);
            return worldState;
        }

        private static WorldState CreateGlideLandingWorldState()
        {
            var landingCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, hp: 3, new SurfaceCell(FaceId.Floor, 2, 0)),
                CreateWall(30, landingCell),
                CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            SeedActiveGlide(worldState, 40);
            return worldState;
        }

        private static WorldState CreateExpiredActiveOnSolidWorldState()
        {
            var landingCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var enemy = CreateEnemy(40, hp: 3, new SurfaceCell(FaceId.Floor, 0, 0));
            enemy.aiMode = EnemyAiMode.Patrol;
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreateWall(30, landingCell),
                enemy,
            });
            worldState.CreateWriteContext().SetEnemyGlideState(
                40,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 1,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 3,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 0,
                    durationTicks: 3,
                    recoveryTicks: 1,
                    cooldownTicks: 0,
                    lastExitedTick: 0,
                    hasLockedStep: true,
                    lockedStepX: 1,
                    lockedStepY: 0));
            worldState.CreateWriteContext().MoveEntity(40, landingCell);
            return worldState;
        }

        private static void SeedActiveGlide(WorldState worldState, int entityId)
        {
            worldState.CreateWriteContext().SetEnemyGlideState(
                entityId,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 1,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 12,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 0,
                    durationTicks: 12,
                    recoveryTicks: 1,
                    cooldownTicks: 0,
                    lastExitedTick: 0,
                    hasLockedStep: true,
                    lockedStepX: 1,
                    lockedStepY: 0));
        }

        private static EnemyAiProfile CreateChargeSettleWaitProfile()
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(
                    moveCooldownSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    ordinaryKinematicMoveDurationSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                ChargeTimingSettings = new EnemyChargeTimingAuthoringSettings(
                    windupSeconds: 0f,
                    activeStepCooldownSeconds: 0f,
                    recoverSeconds: 0f),
                StateResolverKind = EnemyAiStateResolverKind.Charge,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = true,
            });
        }

        private static EntityState CreateChargePatrolEnemy(int entityId, int hp, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                aiMode = EnemyAiMode.Patrol,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EnemyAiProfile CreatePassiveContactProfile(
            float moveCooldownSeconds,
            float ordinaryKinematicMoveDurationSeconds)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(
                    movementPriority: 50,
                    attackPriority: 50,
                    recoverSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(
                    moveCooldownSeconds,
                    ordinaryKinematicMoveDurationSeconds),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = true,
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

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.None,
            };
        }

        private sealed class TickScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _controlledEntityId;
            private readonly IReadOnlyDictionary<int, RawMovementIntent> _movementIntentsByTick;

            public TickScriptedMovementLogic(
                int controlledEntityId,
                IReadOnlyDictionary<int, RawMovementIntent> movementIntentsByTick)
            {
                _controlledEntityId = controlledEntityId;
                _movementIntentsByTick = movementIntentsByTick ?? throw new ArgumentNullException(nameof(movementIntentsByTick));
            }

            public int ControlledEntityId => _controlledEntityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_movementIntentsByTick.TryGetValue(input.TickIndex, out var movementIntent))
                {
                    buffer.Add(movementIntent);
                }
            }
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
