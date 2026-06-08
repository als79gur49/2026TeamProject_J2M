using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class RuntimeBoardBoundsGuardTests
    {
        [Test]
        [Category("Full")]
        public void HostConfiguration_DefaultGameplayLocomotion_AppliesExpectedFlags()
        {
            var configuration = new GameplaySceneHostConfiguration();

            configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            var flags = configuration.CreateRuntimeFeatureFlags();

            Assert.That(flags.EnablePlayerFree2DLocalLocomotion, Is.True);
            Assert.That(flags.EnablePlayerFree2DActionAssist, Is.True);
            Assert.That(flags.EnablePlayerFree2DNativeTopologyTransition, Is.True);
            Assert.That(flags.EnablePlayerSameFaceContinuousLocomotion, Is.True);
            Assert.That(flags.EnablePlayerStoppableKinematicLocomotion, Is.True);
            Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.True);
            Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.True);
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.True);
            Assert.That(flags.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.EnablePlayerFree2DLocalLocomotion, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.EnableEnemyGlideKinematicLocomotion, Is.False);
            Assert.That(GameplayRuntimeFeatureFlags.None.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(new GameplaySceneHostConfiguration().CreateRuntimeFeatureFlags().EnablePlayerFree2DLocalLocomotion, Is.False);
            Assert.That(new GameplaySceneHostConfiguration().CreateRuntimeFeatureFlags().EnableEnemyGlideKinematicLocomotion, Is.False);
            Assert.That(new GameplaySceneHostConfiguration().CreateRuntimeFeatureFlags().RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
        }

        [Test]
        [Category("Full")]
        public void HostConfiguration_RemovedDiagnosticBaseline_IsNotSceneExposed()
        {
            var configuration = new GameplaySceneHostConfiguration();

            configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);
            var flags = configuration.CreateRuntimeFeatureFlags();

            Assert.That(GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline.RemovedLegacyFallbackDiagnosticsEnabled, Is.True);
            Assert.That(flags.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
            Assert.That(flags.EnablePlayerFree2DLocalLocomotion, Is.False);
            Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.False);
            Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.False);
            Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.False);
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_UnboundedBoard_Throws()
        {
            var gameObject = new GameObject("RuntimeBoardBoundsGuardTests");

            try
            {
                var host = gameObject.AddComponent<GameplaySceneHost>();

                Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(new GameplaySceneHostConfiguration()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCompositionRoot_CreateWorldState_RejectsUnboundedBoard()
        {
            Assert.Throws<InvalidOperationException>(
                () => GameplayCompositionRoot.CreateWorldState(
                    Array.Empty<EntityState>(),
                    BoardBounds.Unbounded,
                    GameplayTerrainData.Empty));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_NormalizesPreExistingProjectileCadence()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_NormalizesPreExistingProjectileCadence");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_Initialize_NormalizesPreExistingProjectileCadence_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                        InitialEntities = new[]
                        {
                            new EntityState
                            {
                                entityId = 20,
                                position = new SurfaceCell(FaceId.Floor, 0, 0),
                                hp = 1,
                                maxHp = 1,
                                teamId = 1,
                                type = EntityType.Projectile,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Right,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        SimulationTicksPerSecond = 10,
                        ProjectileStepIntervalSeconds = 0.3f,
                    });

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot.TryGetProjectileAt(new SurfaceCell(FaceId.Floor, 0, 0), out var projectile), Is.True);
                Assert.That(projectile.entityId, Is.EqualTo(20));
                Assert.That(projectile.stateTimer, Is.EqualTo(host.TimingProfile.ProjectileStepIntervalTicks));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreateTimingProfile_DefaultsTopologyMotionDurationToPushAndAllowsOverride()
        {
            var configuration = new GameplaySceneHostConfiguration
            {
                PushMotionDurationSeconds = 0.25f,
            };

            var defaultProfile = configuration.CreateTimingProfile();

            Assert.That(defaultProfile.MoveMotionDurationSeconds, Is.EqualTo(0.25f));
            Assert.That(defaultProfile.PushMotionDurationSeconds, Is.EqualTo(0.25f));
            Assert.That(defaultProfile.TopologyMotionDurationSeconds, Is.EqualTo(0.25f));
            Assert.That(defaultProfile.ItemConsumeEffectDurationSeconds, Is.EqualTo(GameplayTimingProfile.DefaultItemConsumeEffectDurationSeconds));
            Assert.That(defaultProfile.BoxDestroyEffectDurationSeconds, Is.EqualTo(GameplayTimingProfile.DefaultBoxDestroyEffectDurationSeconds));
            Assert.That(defaultProfile.EnemyDeathEffectDurationSeconds, Is.EqualTo(GameplayTimingProfile.DefaultEnemyDeathEffectDurationSeconds));

            configuration.MoveMotionDurationSeconds = 0.1f;
            configuration.TopologyMotionDurationSeconds = 0.45f;
            configuration.ItemConsumeEffectDurationSeconds = 0.6f;
            configuration.BoxDestroyEffectDurationSeconds = 0.3f;
            configuration.EnemyDeathEffectDurationSeconds = 0.35f;

            var overriddenProfile = configuration.CreateTimingProfile();

            Assert.That(overriddenProfile.MoveMotionDurationSeconds, Is.EqualTo(0.1f));
            Assert.That(overriddenProfile.PushMotionDurationSeconds, Is.EqualTo(0.25f));
            Assert.That(overriddenProfile.TopologyMotionDurationSeconds, Is.EqualTo(0.45f));
            Assert.That(overriddenProfile.ItemConsumeEffectDurationSeconds, Is.EqualTo(0.6f));
            Assert.That(overriddenProfile.BoxDestroyEffectDurationSeconds, Is.EqualTo(0.3f));
            Assert.That(overriddenProfile.EnemyDeathEffectDurationSeconds, Is.EqualTo(0.35f));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_DefaultsTopologyRotationVisualMappingToForwardUsesPositiveX()
        {
            var configuration = new GameplaySceneHostConfiguration();

            Assert.That(
                configuration.TopologyRotationVisualMapping,
                Is.EqualTo(TopologyRotationVisualMapping.ForwardUsesPositiveX));
            Assert.That(
                configuration.TopologyRotationTween.Ease,
                Is.EqualTo(TopologyRotationTweenEase.OutQuad));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_ResolveFaceSeamGap_DefaultsToCellSizeAndAllowsOverride()
        {
            var configuration = new GameplaySceneHostConfiguration
            {
                CellSize = 1.25f,
            };

            Assert.That(configuration.ResolveFaceSeamGap(), Is.EqualTo(1.25f).Within(0.0001f));

            configuration.FaceSeamGap = 0.4f;

            Assert.That(configuration.ResolveFaceSeamGap(), Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayTimingProfile_CreateDefault_PreservesDefaultTimeMeaningAtSixtyTps()
        {
            var profile = GameplayTimingProfile.CreateDefault();

            Assert.That(profile.SimulationTicksPerSecond, Is.EqualTo(60));
            Assert.That(profile.InitialMoveDelaySeconds, Is.EqualTo(0f));
            Assert.That(profile.InitialMoveDelayTicks, Is.EqualTo(0));
            Assert.That(profile.RepeatedMoveIntervalSeconds, Is.EqualTo(0.4f));
            Assert.That(profile.RepeatedMoveIntervalTicks, Is.EqualTo(24));
            Assert.That(profile.BoxSlideStepIntervalSeconds, Is.EqualTo(0.12f));
            Assert.That(profile.BoxSlideStepIntervalTicks, Is.EqualTo(7));
            Assert.That(profile.ProjectileStepIntervalSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.ProjectileStepIntervalTicks, Is.EqualTo(12));
            Assert.That(profile.MoveMotionDurationSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.PushMotionDurationSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.TopologyMotionDurationSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.FlipMotionDurationSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.ItemConsumeEffectDurationSeconds, Is.EqualTo(GameplayTimingProfile.DefaultItemConsumeEffectDurationSeconds));
            Assert.That(profile.BoxDestroyEffectDurationSeconds, Is.EqualTo(GameplayTimingProfile.DefaultBoxDestroyEffectDurationSeconds));
            Assert.That(profile.EnemyDeathEffectDurationSeconds, Is.EqualTo(GameplayTimingProfile.DefaultEnemyDeathEffectDurationSeconds));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreateTimingProfile_ChangingSimulationTicksPerSecondPreservesGeneralTimeMeaning()
        {
            var sixtyTpsProfile = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 60,
            }.CreateTimingProfile();
            var oneTwentyTpsProfile = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 120,
            }.CreateTimingProfile();

            Assert.That(sixtyTpsProfile.RepeatedMoveIntervalSeconds, Is.EqualTo(oneTwentyTpsProfile.RepeatedMoveIntervalSeconds));
            Assert.That(sixtyTpsProfile.BoxSlideStepIntervalSeconds, Is.EqualTo(oneTwentyTpsProfile.BoxSlideStepIntervalSeconds));
            Assert.That(sixtyTpsProfile.ProjectileStepIntervalSeconds, Is.EqualTo(oneTwentyTpsProfile.ProjectileStepIntervalSeconds));
            Assert.That(sixtyTpsProfile.RepeatedMoveIntervalTicks, Is.EqualTo(24));
            Assert.That(oneTwentyTpsProfile.RepeatedMoveIntervalTicks, Is.EqualTo(48));
            Assert.That(sixtyTpsProfile.BoxSlideStepIntervalTicks, Is.EqualTo(7));
            Assert.That(oneTwentyTpsProfile.BoxSlideStepIntervalTicks, Is.EqualTo(14));
            Assert.That(sixtyTpsProfile.ProjectileStepIntervalTicks, Is.EqualTo(12));
            Assert.That(oneTwentyTpsProfile.ProjectileStepIntervalTicks, Is.EqualTo(24));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreatePlayerControlTimingSnapshot_ChangingSimulationTicksPerSecondPreservesPlayerTimeMeaning()
        {
            var sixtyTpsSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 60,
                PlayerControlTiming = new PlayerControlTimingSettings
                {
                },
            }.CreatePlayerControlTimingSnapshot();
            var oneTwentyTpsSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 120,
                PlayerControlTiming = new PlayerControlTimingSettings
                {
                },
            }.CreatePlayerControlTimingSnapshot();

            Assert.That(sixtyTpsSnapshot.MoveCooldownSeconds, Is.EqualTo(oneTwentyTpsSnapshot.MoveCooldownSeconds));
            Assert.That(sixtyTpsSnapshot.MoveCooldownTicks, Is.EqualTo(24));
            Assert.That(oneTwentyTpsSnapshot.MoveCooldownTicks, Is.EqualTo(48));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreatePlayerControlTimingSnapshot_DefaultsMoveCooldownToRepeatedMoveIntervalAndConvertsTicks()
        {
            var configuration = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 120,
                RepeatedMoveIntervalSeconds = 0.35f,
                PlayerControlTiming = new PlayerControlTimingSettings
                {
                    PushExecuteDelaySeconds = 2f / 60f,
                    PushInputLockDurationSeconds = 5f / 60f,
                    FlipExecuteDelaySeconds = 0f,
                    FlipInputLockDurationSeconds = 4f / 60f,
                },
            };

            var snapshot = configuration.CreatePlayerControlTimingSnapshot();

            Assert.That(snapshot.MoveCooldownSeconds, Is.EqualTo(0.35f));
            Assert.That(snapshot.MoveCooldownTicks, Is.EqualTo(42));
            Assert.That(snapshot.PushExecuteDelayTicks, Is.EqualTo(4));
            Assert.That(snapshot.PushInputLockDurationTicks, Is.EqualTo(10));
            Assert.That(snapshot.PushWindupTicks, Is.EqualTo(4));
            Assert.That(snapshot.PushRecoveryTicks, Is.EqualTo(6));
            Assert.That(snapshot.FlipExecuteDelayTicks, Is.Zero);
            Assert.That(snapshot.FlipInputLockDurationTicks, Is.EqualTo(8));
            Assert.That(snapshot.FlipWindupTicks, Is.Zero);
            Assert.That(snapshot.FlipRecoveryTicks, Is.EqualTo(8));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreatePlayerRespawnTimingSnapshot_ChangingSimulationTicksPerSecondPreservesTimeMeaning()
        {
            var sixtyTpsSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 60,
                PlayerRespawnTiming = new PlayerRespawnTimingSettings
                {
                    RespawnDelaySeconds = 0.25f,
                },
            }.CreatePlayerRespawnTimingSnapshot();
            var oneTwentyTpsSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 120,
                PlayerRespawnTiming = new PlayerRespawnTimingSettings
                {
                    RespawnDelaySeconds = 0.25f,
                },
            }.CreatePlayerRespawnTimingSnapshot();

            Assert.That(sixtyTpsSnapshot.RespawnDelaySeconds, Is.EqualTo(oneTwentyTpsSnapshot.RespawnDelaySeconds));
            Assert.That(sixtyTpsSnapshot.RespawnDelayTicks, Is.EqualTo(15));
            Assert.That(oneTwentyTpsSnapshot.RespawnDelayTicks, Is.EqualTo(30));
            Assert.That(
                sixtyTpsSnapshot.RespawnDelayTicks / 60f,
                Is.EqualTo(oneTwentyTpsSnapshot.RespawnDelayTicks / 120f).Within(0.0001f));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreatePlayerRespawnTimingSnapshot_DefaultsToThreeQuarterSecond()
        {
            var snapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 60,
            }.CreatePlayerRespawnTimingSnapshot();

            Assert.That(snapshot.RespawnDelaySeconds, Is.EqualTo(0.75f));
            Assert.That(snapshot.RespawnDelayTicks, Is.EqualTo(45));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreatePlayerRespawnTimingSnapshot_ZeroSecondsStillRespectsNextTickRule()
        {
            var snapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 120,
                PlayerRespawnTiming = new PlayerRespawnTimingSettings
                {
                    RespawnDelaySeconds = 0f,
                },
            }.CreatePlayerRespawnTimingSnapshot();

            Assert.That(snapshot.RespawnDelaySeconds, Is.Zero);
            Assert.That(snapshot.RespawnDelayTicks, Is.EqualTo(1));
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreateEnemyAiRuntimeSnapshot_ChangingSimulationTicksPerSecondPreservesEnemyTimeMeaning()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(
                    movementPriority: 50,
                    attackPriority: 50,
                    recoverSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                AttackTimingSettings = new EnemyAttackTimingAuthoringSettings(
                    windupSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.WindupForwardCellProjectile,
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(
                    moveCooldownSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });

            try
            {
                var sixtyTpsSnapshot = new GameplaySceneHostConfiguration
                {
                    SimulationTicksPerSecond = 60,
                    DefaultEnemyAiProfile = profile,
                }.CreateEnemyAiRuntimeSnapshot();
                var thirtyTpsSnapshot = new GameplaySceneHostConfiguration
                {
                    SimulationTicksPerSecond = 30,
                    DefaultEnemyAiProfile = profile,
                }.CreateEnemyAiRuntimeSnapshot();

                Assert.That(sixtyTpsSnapshot.DefaultDefinition.CommonSettings.RecoverTicks, Is.EqualTo(2));
                Assert.That(thirtyTpsSnapshot.DefaultDefinition.CommonSettings.RecoverTicks, Is.EqualTo(1));
                Assert.That(sixtyTpsSnapshot.DefaultDefinition.AttackTimingSettings.WindupTicks, Is.EqualTo(2));
                Assert.That(thirtyTpsSnapshot.DefaultDefinition.AttackTimingSettings.WindupTicks, Is.EqualTo(1));
                Assert.That(sixtyTpsSnapshot.DefaultDefinition.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(2));
                Assert.That(thirtyTpsSnapshot.DefaultDefinition.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(1));
                Assert.That(
                    sixtyTpsSnapshot.DefaultDefinition.CommonSettings.RecoverTicks / 60f,
                    Is.EqualTo(thirtyTpsSnapshot.DefaultDefinition.CommonSettings.RecoverTicks / 30f).Within(0.0001f));
                Assert.That(
                    sixtyTpsSnapshot.DefaultDefinition.AttackTimingSettings.WindupTicks / 60f,
                    Is.EqualTo(thirtyTpsSnapshot.DefaultDefinition.AttackTimingSettings.WindupTicks / 30f).Within(0.0001f));
                Assert.That(
                    sixtyTpsSnapshot.DefaultDefinition.LocomotionTimingSettings.MoveCooldownTicks / 60f,
                    Is.EqualTo(thirtyTpsSnapshot.DefaultDefinition.LocomotionTimingSettings.MoveCooldownTicks / 30f).Within(0.0001f));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHostConfiguration_CreateEnemyAiRuntimeSnapshot_NullDefaultProfile_DoesNotCreateDefaultMeleeDefinition()
        {
            var snapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 30,
            }.CreateEnemyAiRuntimeSnapshot();

            Assert.That(snapshot.HasDefaultDefinition, Is.False);
            Assert.That(snapshot.DefinitionsByEntityId, Is.Null);
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_NormalizesEnemyWindupAgainstSimulationTickRate()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_NormalizesEnemyWindupAgainstSimulationTickRate");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var profile = EnemyAiProfileTestFactory.CreateTestOnlyMelee(windupTicks: 2);

                try
                {
                    host.Initialize(
                        new GameplaySceneHostConfiguration
                        {
                            AutoAdvanceTicks = false,
                            AutoCreateViews = false,
                            DefaultEnemyAiProfile = profile,
                            InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                            InitialEntities = new[]
                            {
                                new EntityState
                                {
                                    entityId = 10,
                                    position = new SurfaceCell(FaceId.Floor, 1, 0),
                                    hp = 3,
                                    maxHp = 3,
                                    teamId = 1,
                                    type = EntityType.Unit,
                                    state = EntityPhaseState.Idle,
                                    facing = Direction.Left,
                                    boardPresence = EntityBoardPresence.Occupying,
                                },
                                new EntityState
                                {
                                    entityId = 40,
                                    position = new SurfaceCell(FaceId.Floor, 0, 0),
                                    hp = 3,
                                    maxHp = 3,
                                    teamId = 2,
                                    type = EntityType.Unit,
                                    state = EntityPhaseState.Idle,
                                    facing = Direction.Up,
                                    boardPresence = EntityBoardPresence.Occupying,
                                    aiMode = EnemyAiMode.Attack,
                                },
                            },
                            InitialTopology = new CubeTopologyState(FaceId.Floor),
                            PlayerEntityId = 10,
                            SimulationTicksPerSecond = 30,
                        });

                    var firstTick = host.InputHost.RunSingleTick();
                    Assert.That(host.WorldState.CreateSnapshot().TryGetEntity(10, out var firstTickPlayer), Is.True);
                    Assert.That(firstTick.AttackPhaseResult.RawIntents, Is.Empty);
                    Assert.That(firstTickPlayer.hp, Is.EqualTo(3));

                    var secondTick = host.InputHost.RunSingleTick();
                    Assert.That(host.WorldState.CreateSnapshot().TryGetEntity(10, out var secondTickPlayer), Is.True);
                    Assert.That(secondTick.AttackPhaseResult.RawIntents.Count, Is.EqualTo(1));
                    Assert.That(secondTickPlayer.hp, Is.EqualTo(2));
                }
                finally
                {
                    EnemyAiProfileTestFactory.Destroy(profile);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PlayerControlTimingSettings_CreateAuthoritativeSnapshot_PreservesExplicitPlayerTimingValues()
        {
            var snapshot = new PlayerControlTimingSettings
            {
                MoveCooldownSeconds = 0.3f,
            }.CreateAuthoritativeSnapshot(
                simulationTicksPerSecond: 120,
                repeatedMoveIntervalSeconds: 0.4f);

            Assert.That(snapshot.MoveCooldownSeconds, Is.EqualTo(0.3f));
            Assert.That(snapshot.MoveCooldownTicks, Is.EqualTo(36));
        }

        [Test]
        [Category("Full")]
        public void PlayerControlTimingSettings_CreateAuthoritativeSnapshot_InputLockShorterThanExecuteDelay_Throws()
        {
            var settings = new PlayerControlTimingSettings
            {
                PushExecuteDelaySeconds = 0.1f,
                PushInputLockDurationSeconds = 0.05f,
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.CreateAuthoritativeSnapshot(60, 0.4f));
        }

        [Test]
        [Category("Full")]
        public void PlayerAnimationTimingAuthoring_CreateSnapshot_UsesAnimatorDurations()
        {
            var authoringRoot = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimationTimingAuthoring_CreateSnapshot");

            try
            {
                var authoring = authoringRoot.GetComponent<PlayerAnimationTimingAuthoring>();
                Assert.That(authoring, Is.Not.Null);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.25f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.4f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.5f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipRecoveryAnimatorDurationSeconds", 0.75f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "stageClearVictoryAnimatorDurationSeconds", 1.25f);

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.PushWindupAnimatorDurationSeconds, Is.EqualTo(0.25f));
                Assert.That(snapshot.PushRecoveryAnimatorDurationSeconds, Is.EqualTo(0.4f));
                Assert.That(snapshot.FlipWindupAnimatorDurationSeconds, Is.EqualTo(0.5f));
                Assert.That(snapshot.FlipRecoveryAnimatorDurationSeconds, Is.EqualTo(0.75f));
                Assert.That(snapshot.StageClearVictoryAnimatorDurationSeconds, Is.EqualTo(1.25f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoringRoot);
            }
        }

        [Test]
        [Category("Full")]
        public void PlayerAnimationTimingAuthoring_CreateSnapshot_NonPositiveAnimatorDuration_Throws()
        {
            var authoringRoot = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimationTimingAuthoring_InvalidDuration");

            try
            {
                var authoring = authoringRoot.GetComponent<PlayerAnimationTimingAuthoring>();
                Assert.That(authoring, Is.Not.Null);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0f);

                Assert.Throws<ArgumentOutOfRangeException>(() => authoring.CreateSnapshot());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoringRoot);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_UsesConfiguredPlayerControlTiming_WhenPlayerPrefabHasAnimationTimingAuthoringOnly()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_UsesConfiguredPlayerControlTiming_WhenPlayerPrefabHasAnimationTimingAuthoringOnly");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_Initialize_UsesConfiguredPlayerControlTiming_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                Assert.That(playerViewPrefab.GetComponent<PlayerAnimationTimingAuthoring>(), Is.Not.Null);

                var configuration = new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                        InitialEntities = new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = new SurfaceCell(FaceId.Floor, 0, 0),
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Right,
                            },
                            new EntityState
                            {
                                entityId = 20,
                                position = new SurfaceCell(FaceId.Floor, 1, 0),
                                hp = 1,
                                maxHp = 1,
                                teamId = 0,
                                type = EntityType.Box,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Right,
                                boxCapabilities = BoxCapabilities.Push,
                            },
                            new EntityState
                            {
                                entityId = 90,
                                position = new SurfaceCell(FaceId.Floor, 3, 0),
                                hp = 1,
                                maxHp = 1,
                                teamId = 0,
                                type = EntityType.None,
                                state = EntityPhaseState.Idle,
                                facing = Direction.None,
                            },
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerControlTiming = new PlayerControlTimingSettings
                        {
                            PushExecuteDelaySeconds = 2f / 60f,
                            PushInputLockDurationSeconds = 4f / 60f,
                        },
                        PlayerViewPrefab = playerViewPrefab,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    };
                configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
                host.Initialize(configuration);

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.BufferPush();
                var startTick = host.InputHost.RunSingleTick();
                var windupTick = host.InputHost.RunSingleTick();
                var executeTick = host.InputHost.RunSingleTick();

                Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
                Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
                Assert.That(windupTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
                Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
                Assert.That(executeTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_CreatesBoardRootHierarchyAndParentsViewsUnderEntityRoot()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_CreatesBoardRootHierarchyAndParentsViewsUnderEntityRoot");
            hostObject.transform.position = new Vector3(4f, -2f, 0f);

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_Initialize_CreatesBoardRootHierarchy_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = new SurfaceCell(FaceId.Floor, 0, 0),
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Up,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                Assert.That(host.BoardRoot, Is.Not.Null);
                Assert.That(host.BoardRoot.transform.parent, Is.EqualTo(host.transform));
                Assert.That(host.BoardRoot.BoardSurfaceRoot.parent, Is.EqualTo(host.BoardRoot.transform));
                Assert.That(host.BoardRoot.EntityRoot.parent, Is.EqualTo(host.BoardRoot.transform));
                Assert.That(host.BoardRoot.CameraTargetRoot.parent, Is.EqualTo(host.BoardRoot.transform));
                Assert.That(host.BoardRoot.BoardSkinRoot, Is.Null);
                Assert.That(host.BoardRoot.transform.Find("BoardSkinRoot"), Is.Null);
                Assert.That(host.BoardSurfaceRenderer, Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.transform, Is.EqualTo(host.BoardRoot.BoardSurfaceRoot));
                Assert.That(host.ViewCameraTarget, Is.SameAs(host.BoardRoot.CameraTargetRoot));
                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(hostObject.transform.position));
                Assert.That(host.ViewRegistry.SearchRoot, Is.SameAs(host.BoardRoot.EntityRoot));

                Assert.That(host.ViewRegistry.TryGetView(10, out var view), Is.True);
                Assert.That(view.transform.parent, Is.EqualTo(host.BoardRoot.EntityRoot));
                var projector = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    1f);
                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        EntityType.Unit,
                        out var projectedPose),
                    Is.True);
                Assert.That(view.transform.localPosition, Is.EqualTo(projectedPose.LocalPosition));
                Assert.That(view.transform.position, Is.EqualTo(host.BoardRoot.transform.TransformPoint(projectedPose.LocalPosition)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_AttachesConfiguredBoardRootPrefabUnderSkinRoot()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_AttachesConfiguredBoardRootPrefabUnderSkinRoot");
            var prefab = new GameObject("ConfiguredBoardRootPrefab");
            prefab.transform.localPosition = new Vector3(5f, 6f, 7f);
            prefab.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            prefab.transform.localScale = new Vector3(2f, 3f, 4f);
            prefab.AddComponent<BoardRootPrefabTestMarker>();
            var profile = ScriptableObject.CreateInstance<BoardPresentationProfile>();
            PlayerViewPrefabTestUtility.SetSerializedField(profile, "boardRootPrefab", prefab);

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = false,
                        BoardPresentationProfile = profile,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                        InitialEntities = Array.Empty<EntityState>(),
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                Assert.That(host.BoardRoot.BoardSkinRoot, Is.Not.Null);
                Assert.That(host.BoardRoot.BoardSkinRoot.parent, Is.EqualTo(host.BoardRoot.transform));
                var instance = host.BoardRoot.BoardSkinRoot.Find("ConfiguredBoardRootPrefab");
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.GetComponent<BoardRootPrefabTestMarker>(), Is.Not.Null);
                Assert.That(instance.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(instance.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(instance.localScale, Is.EqualTo(Vector3.one));
                Assert.That(host.BoardSurfaceRenderer, Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.transform, Is.EqualTo(host.BoardRoot.BoardSurfaceRoot));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_AutoCreateViewsFalse_UsesConfiguredPlayerControlTiming()
        {
            var hostObject = new GameObject("GameplaySceneHost_AutoCreateViewsFalse_UsesConfiguredPlayerControlTiming");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_AutoCreateViewsFalse_PlayerPrefab");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var configuration = new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = false,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)),
                        InitialEntities = new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = new SurfaceCell(FaceId.Floor, 0, 0),
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Right,
                            },
                            new EntityState
                            {
                                entityId = 20,
                                position = new SurfaceCell(FaceId.Floor, 1, 0),
                                hp = 1,
                                maxHp = 1,
                                teamId = 0,
                                type = EntityType.Box,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Right,
                                boxCapabilities = BoxCapabilities.Push,
                            },
                            new EntityState
                            {
                                entityId = 90,
                                position = new SurfaceCell(FaceId.Floor, 3, 0),
                                hp = 1,
                                maxHp = 1,
                                teamId = 0,
                                type = EntityType.None,
                                state = EntityPhaseState.Idle,
                                facing = Direction.None,
                            },
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerControlTiming = new PlayerControlTimingSettings
                        {
                            PushExecuteDelaySeconds = 2f / 60f,
                            PushInputLockDurationSeconds = 4f / 60f,
                        },
                        PlayerViewPrefab = playerViewPrefab,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    };
                configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
                host.Initialize(configuration);

                Assert.That(host.ViewRegistry.TryGetView(10, out _), Is.False);

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.BufferPush();
                var startTick = host.InputHost.RunSingleTick();
                var windupTick = host.InputHost.RunSingleTick();
                var executeTick = host.InputHost.RunSingleTick();
                var recoveryTick = host.InputHost.RunSingleTick();

                Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
                Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
                Assert.That(windupTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
                Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
                Assert.That(executeTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
                Assert.That(recoveryTick.MovementPhaseResult.SortedIntents, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCompositionRoot_DeclaresOnlyBoundedWorldFactory()
        {
            var worldFactories = typeof(GameplayCompositionRoot)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.ReturnType == typeof(WorldState))
                .Select(method => method.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(GameplayCompositionRoot.CreateWorldState),
                },
                worldFactories);
        }

        private sealed class BoardRootPrefabTestMarker : MonoBehaviour
        {
        }
    }

    internal static class PlayerViewPrefabTestUtility
    {
        public static GameplayEntityView CreatePlayerViewPrefab(string name)
        {
            return CreatePlayerViewPrefabObject(name).GetComponent<GameplayEntityView>();
        }

        public static GameObject CreatePlayerViewPrefabObject(string name)
        {
            var prefabObject = new GameObject(name);
            var view = prefabObject.AddComponent<GameplayEntityView>();
            view.Initialize(10);
            prefabObject.AddComponent<UnitLocomotionPresentationAuthoring>();
            prefabObject.AddComponent<EntityMotionPresentationAuthoring>();
            prefabObject.AddComponent<PlayerAnimationTimingAuthoring>();
            var driver = prefabObject.AddComponent<PlayerAnimatorDriver>();
            var animator = AttachTestAnimator(
                prefabObject,
                $"{name}_Animator",
                "Idle",
                "Walk_Loop",
                "Push_Windup",
                "Push_Recovery",
                "Flip_Windup",
                "Flip_Recovery",
                "Death",
                "Item");
            SetSerializedField(driver, "animator", animator);
            return prefabObject;
        }

        public static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        public static Animator AttachTestAnimator(GameObject rootObject, string name, params string[] stateNames)
        {
            var animatorObject = new GameObject(name);
            animatorObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var animator = animatorObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = CreateTestAnimatorController($"{name}_Controller", stateNames);
            return animator;
        }

        public static AnimatorController CreateTestAnimatorController(string name, params string[] stateNames)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = $"{name}_StateMachine",
                hideFlags = HideFlags.HideAndDontSave,
            };

            AnimatorState defaultState = null;
            for (var i = 0; i < stateNames.Length; i++)
            {
                var stateName = stateNames[i];
                var state = stateMachine.AddState(stateName);
                state.motion = CreateTestClip($"{name}_{stateName}_Clip");
                if (defaultState == null)
                {
                    defaultState = state;
                }
            }

            stateMachine.defaultState = defaultState;
            var controller = new AnimatorController
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                layers = new[]
                {
                    new AnimatorControllerLayer
                    {
                        name = "Base Layer",
                        defaultWeight = 1f,
                        stateMachine = stateMachine,
                    },
                },
            };
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Windup", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("JumpWindup", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("JumpAirborne", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Recover", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("EnemyAiMode", AnimatorControllerParameterType.Int);
            controller.AddParameter("EnemyActionKind", AnimatorControllerParameterType.Int);
            controller.AddParameter("EnemyJumpPhase", AnimatorControllerParameterType.Int);
            controller.AddParameter("EnemyChargePhase", AnimatorControllerParameterType.Int);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("PlayerPresentationState", AnimatorControllerParameterType.Int);
            controller.AddParameter("FlipOutcome", AnimatorControllerParameterType.Int);
            return controller;
        }

        private static AnimationClip CreateTestClip(string name)
        {
            var clip = new AnimationClip
            {
                name = name,
                frameRate = 60f,
                hideFlags = HideFlags.HideAndDontSave,
            };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }
    }

    // Final presentation guardrail:
    // Keep lifecycle, visibility, and motion sequencing coverage in this class so
    // the board-local cube contract stays fixed after the strip migration removal.
    public sealed class GameplayViewProjectionTests
    {
        [Test]
        [Category("Full")]
        public void GameplayEntityView_ApplyLocalPose_UsesLocalTransformSpace()
        {
            var rootObject = new GameObject("GameplayEntityView_ApplyLocalPose_UsesLocalTransformSpace");
            rootObject.transform.position = new Vector3(5f, 7f, 0f);

            try
            {
                var parentObject = new GameObject("EntityRoot");
                parentObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                parentObject.transform.localPosition = new Vector3(2f, -3f, 0f);

                var viewObject = new GameObject("EntityView");
                viewObject.transform.SetParent(parentObject.transform, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);

                view.ApplyLocalPose(new Vector3(1f, 4f, 0f), Quaternion.Euler(0f, 0f, 90f));

                Assert.That(view.transform.localPosition, Is.EqualTo(new Vector3(1f, 4f, 0f)));
                Assert.That(view.transform.position, Is.EqualTo(new Vector3(8f, 8f, 0f)));
                Assert.That(
                    Quaternion.Angle(view.transform.localRotation, Quaternion.Euler(0f, 0f, 90f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayEntityView_ConfigureModelRoot_CreatesDedicatedModelPivot()
        {
            var viewObject = new GameObject("GameplayEntityView_ConfigureModelRoot_CreatesDedicatedModelPivot");

            try
            {
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);
                var expectedRotation = Quaternion.Euler(15f, 25f, 35f);

                view.ConfigureModelRoot(new Vector3(0.1f, 0.2f, 0.3f), expectedRotation);

                Assert.That(view.ModelRoot, Is.Not.Null);
                Assert.That(view.ModelRoot.parent, Is.EqualTo(view.transform));
                Assert.That(view.ModelRoot.localPosition, Is.EqualTo(new Vector3(0.1f, 0.2f, 0.3f)));
                Assert.That(Quaternion.Angle(view.ModelRoot.localRotation, expectedRotation), Is.LessThan(0.001f));
                Assert.That(view.ModelRoot.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_CreatesCubeEntityVisualProfilesWithoutColliders()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_CreatesCubeEntityVisualProfilesWithoutColliders");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(parentObject.transform, 1f, playerEntityId: 10);

                AssertVisualMatchesProfile(
                    factory.CreateView(CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                    GameplayEntityVisualProfile.Create(EntityType.Unit, 1f),
                    new Color(0.2f, 0.85f, 0.35f));
                AssertVisualMatchesProfile(
                    factory.CreateView(CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right)),
                    GameplayEntityVisualProfile.Create(EntityType.Box, 1f),
                    new Color(0.72f, 0.5f, 0.24f));
                AssertVisualMatchesProfile(
                    factory.CreateView(CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right)),
                    GameplayEntityVisualProfile.Create(EntityType.Projectile, 1f),
                    new Color(0.9f, 0.4f, 0.2f));
                AssertVisualMatchesProfile(
                    factory.CreateView(CreateSurfaceWall(40, new SurfaceCell(FaceId.Floor, 0, 0))),
                    GameplayEntityVisualProfile.Create(EntityType.None, 1f),
                    new Color(0.25f, 0.28f, 0.33f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_AiControlledUnit_AddsEnemyAnimatorDriver()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_AiControlledUnit_AddsEnemyAnimatorDriver");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(parentObject.transform, 1f, playerEntityId: 10);
                var enemyView = factory.CreateView(CreateSurfaceUnit(20, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Patrol));
                var playerView = factory.CreateView(CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)));

                Assert.That(enemyView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(playerView.GetComponent<EnemyAnimatorDriver>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_PlayerUnit_AddsPlayerAnimatorDriverOnlyToPlayer()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_PlayerUnit_AddsPlayerAnimatorDriverOnlyToPlayer");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(parentObject.transform, 1f, playerEntityId: 10);
                var playerView = factory.CreateView(CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)));
                var enemyView = factory.CreateView(CreateSurfaceUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), aiMode: EnemyAiMode.Patrol));

                Assert.That(playerView.GetComponent<PlayerAnimatorDriver>(), Is.Not.Null);
                Assert.That(playerView.GetComponent<PlayerAnimationTimingAuthoring>(), Is.Not.Null);
                Assert.That(enemyView.GetComponent<PlayerAnimatorDriver>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_StaticEnemyBindingWithNoneAiMode_UsesEnemyPrefab()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_StaticEnemyBindingWithNoneAiMode_UsesEnemyPrefab");
            var prefabObject = new GameObject("StaticEnemyPrefab");

            try
            {
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();
                prefabObject.AddComponent<EnemyAnimatorDriver>();
                new GameObject("PrefabMarker").transform.SetParent(prefabObject.transform, worldPositionStays: false);

                var factory = new DefaultGameplayEntityViewFactory(
                    parentObject.transform,
                    1f,
                    playerEntityId: 10,
                    enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        { 20, prefabView },
                    });

                var view = factory.CreateView(
                    CreateSurfaceUnit(
                        20,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        aiMode: EnemyAiMode.None,
                        unitRole: UnitRole.Enemy));

                Assert.That(view.transform.Find("PrefabMarker"), Is.Not.Null);
                Assert.That(view.ModelRoot.Find("Visual"), Is.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_StaticBoxBinding_UsesPrefabAndSanitizesPhysics()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_StaticBoxBinding_UsesPrefabAndSanitizesPhysics");
            var prefabObject = new GameObject("StaticBoxPrefab");

            try
            {
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();
                prefabObject.AddComponent<BoxCollider>();
                prefabObject.AddComponent<Rigidbody>();
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "PrefabMarker";
                marker.transform.SetParent(prefabObject.transform, worldPositionStays: false);
                marker.AddComponent<BoxCollider>();
                marker.AddComponent<Rigidbody>();

                var factory = new DefaultGameplayEntityViewFactory(
                    parentObject.transform,
                    1f,
                    playerEntityId: 10,
                    staticViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        { 20, prefabView },
                    });

                var view = factory.CreateView(CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right));

                Assert.That(view.transform.Find("PrefabMarker"), Is.Not.Null);
                Assert.That(view.transform.Find("Visual"), Is.Null);
                Assert.That(view.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty);
                Assert.That(view.GetComponentsInChildren<Rigidbody>(includeInactive: true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_StaticBoxBinding_WithoutRenderer_Throws()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_StaticBoxBinding_WithoutRenderer_Throws");
            var prefabObject = new GameObject("StaticBoxPrefabWithoutRenderer");

            try
            {
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();

                var factory = new DefaultGameplayEntityViewFactory(
                    parentObject.transform,
                    1f,
                    playerEntityId: 10,
                    staticViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        { 20, prefabView },
                    });

                var exception = Assert.Throws<InvalidOperationException>(
                    () => factory.CreateView(CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right)));

                StringAssert.Contains("must provide an active Renderer", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_StaticBoxWithoutBinding_FallsBackToPrimitiveVisual()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_StaticBoxWithoutBinding_FallsBackToPrimitiveVisual");
            var prefabObject = new GameObject("UnusedStaticBoxPrefab");

            try
            {
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();
                new GameObject("PrefabMarker").transform.SetParent(prefabObject.transform, worldPositionStays: false);

                var factory = new DefaultGameplayEntityViewFactory(
                    parentObject.transform,
                    1f,
                    playerEntityId: 10,
                    staticViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        { 99, prefabView },
                    });

                var view = factory.CreateView(CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right));

                Assert.That(view.transform.Find("PrefabMarker"), Is.Null);
                Assert.That(view.transform.Find("Visual"), Is.Null);
                Assert.That(view.ModelRoot.Find("Visual"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayEntityVisualProfile_BoxVisualRecedesIntoFaceInterior()
        {
            var profile = GameplayEntityVisualProfile.Create(EntityType.Box, 1f);

            Assert.That(profile.ModelLocalScale, Is.EqualTo(new Vector3(1f, 1f, 0.5f)));
            Assert.That(profile.SurfaceOffsetFromFacePlane, Is.EqualTo(0.43f).Within(0.001f));
            Assert.That(profile.ModelLocalPosition.z, Is.LessThan(0f));
            Assert.That(profile.ModelLocalPosition.z, Is.GreaterThan(-(profile.ModelLocalScale.z * 0.5f)));
            Assert.That(profile.ModelLocalScale.z, Is.GreaterThan(0f));
            Assert.That(ResolveEntityOuterFaceDepth(profile), Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayEntityVisualProfile_UnitVisualRecedesIntoFaceInterior()
        {
            var profile = GameplayEntityVisualProfile.Create(EntityType.Unit, 1f);

            Assert.That(profile.SurfaceOffsetFromFacePlane, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(profile.ModelLocalPosition.z, Is.LessThan(0f));
            Assert.That(profile.ModelLocalPosition.z, Is.GreaterThan(-(profile.ModelLocalScale.z * 0.5f)));
            Assert.That(profile.ModelLocalScale.z, Is.GreaterThan(0f));
            Assert.That(ResolveEntityOuterFaceDepth(profile), Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayEntityVisualProfile_WallVisualRecedesIntoFaceInterior()
        {
            var profile = GameplayEntityVisualProfile.Create(EntityType.None, 1f);

            Assert.That(profile.ModelLocalScale, Is.EqualTo(new Vector3(1f, 1f, 0.5f)));
            Assert.That(profile.SurfaceOffsetFromFacePlane, Is.EqualTo(0.37f).Within(0.001f));
            Assert.That(profile.ModelLocalPosition.z, Is.LessThan(0f));
            Assert.That(profile.ModelLocalPosition.z, Is.GreaterThan(-(profile.ModelLocalScale.z * 0.5f)));
            Assert.That(profile.ModelLocalScale.z, Is.GreaterThan(0f));
            Assert.That(ResolveEntityOuterFaceDepth(profile), Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        [Category("Full")]
        public void ProjectedCellPose_NormalizesNormalVector()
        {
            var pose = new ProjectedCellPose(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0f, 3f, 0f));

            Assert.That(pose.LocalPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(
                Quaternion.Angle(pose.LocalRotation, Quaternion.Euler(0f, 0f, 45f)),
                Is.LessThan(0.001f));
            Assert.That(pose.Normal, Is.EqualTo(Vector3.up));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_ProjectsBottomFaceToHorizontalPlane()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Floor, 1, 2),
                    topology,
                    EntityType.Unit,
                    out var bottomPose),
                Is.True);

            Assert.That(bottomPose.LocalPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(bottomPose.LocalPosition.y, Is.LessThan(0f));
            Assert.That(bottomPose.LocalPosition.z, Is.EqualTo(1f).Within(0.001f));
            Assert.That(bottomPose.Normal, Is.EqualTo(Vector3.down));
            Assert.That(
                Quaternion.Angle(bottomPose.LocalRotation, Quaternion.LookRotation(Vector3.down, Vector3.forward)),
                Is.LessThan(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_ProjectsFrontFaceToVerticalPlane()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Front, 1, 0),
                    topology,
                    EntityType.Unit,
                    out var frontPose),
                Is.True);

            Assert.That(frontPose.LocalPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(frontPose.LocalPosition.y, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(frontPose.LocalPosition.z, Is.GreaterThan(0f));
            AssertVectorApproximately(frontPose.Normal, Vector3.forward);
            Assert.That(
                Quaternion.Angle(frontPose.LocalRotation, Quaternion.LookRotation(Vector3.forward, Vector3.up)),
                Is.LessThan(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_SurfaceFrames_KeepFaceCentersOnExplodedCubeAxes()
        {
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
            const float cellSize = 1.75f;
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            var topology = new CubeTopologyState(FaceId.Floor);
            var explodedDistance = cellSize;

            Assert.That(projector.TryProjectSurfaceCell(new SurfaceCell(FaceId.Floor, 0, 0), topology, out var bottomPose), Is.True);
            Assert.That(projector.TryProjectSurfaceCell(new SurfaceCell(FaceId.Front, 0, 0), topology, out var frontPose), Is.True);
            Assert.That(projector.TryProjectSurfaceCell(new SurfaceCell(FaceId.Ceiling, 0, 0), topology, out var topPose), Is.True);
            Assert.That(projector.TryProjectSurfaceCell(new SurfaceCell(FaceId.Back, 0, 0), topology, out var backPose), Is.True);

            Assert.That(Vector3.Distance(bottomPose.LocalPosition, new Vector3(0f, -explodedDistance, 0f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(frontPose.LocalPosition, new Vector3(0f, 0f, explodedDistance)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(topPose.LocalPosition, new Vector3(0f, explodedDistance, 0f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(backPose.LocalPosition, new Vector3(0f, 0f, -explodedDistance)), Is.LessThan(0.001f));
            AssertVectorApproximately(bottomPose.Normal, Vector3.down);
            AssertVectorApproximately(frontPose.Normal, Vector3.forward);
            AssertVectorApproximately(topPose.Normal, Vector3.up);
            AssertVectorApproximately(backPose.Normal, Vector3.back);
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceCellPresentationPoseResolver_ValidBottomFaceCell_ReturnsPose()
        {
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2));
            var topology = new CubeTopologyState(FaceId.Floor);
            var resolver = new BoardSurfaceCellPresentationPoseResolver(boardBounds, 1f, topology, 1f);
            var projector = new GameplayCubeProjector(boardBounds, 1f, 1f);
            var cell = new SurfaceCell(FaceId.Floor, 1, 2);

            Assert.That(resolver.TryResolvePose(cell, out var pose), Is.True);
            Assert.That(projector.TryProjectSurfaceCell(cell, topology, out var projectedPose), Is.True);
            AssertVectorApproximately(pose.LocalPosition, projectedPose.LocalPosition);
            Assert.That(Quaternion.Angle(pose.LocalRotation, projectedPose.LocalRotation), Is.LessThan(0.001f));
            Assert.That(pose.LocalScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceCellPresentationPoseResolver_ValidFrontFaceCell_ReturnsPose()
        {
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2));
            var topology = new CubeTopologyState(FaceId.Floor);
            var resolver = new BoardSurfaceCellPresentationPoseResolver(boardBounds, 1f, topology, 1f);
            var projector = new GameplayCubeProjector(boardBounds, 1f, 1f);
            var cell = new SurfaceCell(FaceId.Front, 1, 0);

            Assert.That(resolver.TryResolvePose(cell, out var pose), Is.True);
            Assert.That(projector.TryProjectSurfaceCell(cell, topology, out var projectedPose), Is.True);
            AssertVectorApproximately(pose.LocalPosition, projectedPose.LocalPosition);
            Assert.That(Quaternion.Angle(pose.LocalRotation, projectedPose.LocalRotation), Is.LessThan(0.001f));
            Assert.That(pose.LocalScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceCellPresentationPoseResolver_DifferentFacesSameCoordinates_ProduceDifferentPose()
        {
            var resolver = new BoardSurfaceCellPresentationPoseResolver(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f,
                new CubeTopologyState(FaceId.Floor),
                1f);

            Assert.That(resolver.TryResolvePose(new SurfaceCell(FaceId.Floor, 1, 1), out var bottomPose), Is.True);
            Assert.That(resolver.TryResolvePose(new SurfaceCell(FaceId.Front, 1, 1), out var frontPose), Is.True);

            var positionDiffers = Vector3.Distance(bottomPose.LocalPosition, frontPose.LocalPosition) > 0.001f;
            var rotationDiffers = Quaternion.Angle(bottomPose.LocalRotation, frontPose.LocalRotation) > 0.001f;
            Assert.That(positionDiffers || rotationDiffers, Is.True);
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceCellPresentationPoseResolver_OutOfBounds_ReturnsFalse()
        {
            var resolver = new BoardSurfaceCellPresentationPoseResolver(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f,
                new CubeTopologyState(FaceId.Floor),
                1f);

            Assert.That(
                resolver.TryResolvePose(new SurfaceCell(FaceId.Floor, 2, 1), out _),
                Is.False);
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceCellPresentationPoseResolver_InactiveFace_ReturnsFalse()
        {
            var resolver = new BoardSurfaceCellPresentationPoseResolver(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f,
                new CubeTopologyState(FaceId.Floor),
                1f);

            Assert.That(
                resolver.TryResolvePose(new SurfaceCell(FaceId.Ceiling, 0, 0), out _),
                Is.False);
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceCellPresentationPoseResolver_InvalidFace_ReturnsFalse()
        {
            var resolver = new BoardSurfaceCellPresentationPoseResolver(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f,
                new CubeTopologyState(FaceId.Floor),
                1f);

            Assert.That(
                resolver.TryResolvePose(new SurfaceCell((FaceId)999, 0, 0), out _),
                Is.False);
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceCellPresentationPoseResolver_RepeatedResolve_IsDeterministic()
        {
            var resolver = new BoardSurfaceCellPresentationPoseResolver(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f,
                new CubeTopologyState(FaceId.Floor),
                1f);
            var cell = new SurfaceCell(FaceId.Front, 2, 1);

            Assert.That(resolver.TryResolvePose(cell, out var first), Is.True);
            Assert.That(resolver.TryResolvePose(cell, out var second), Is.True);

            AssertVectorApproximately(second.LocalPosition, first.LocalPosition);
            Assert.That(Quaternion.Angle(second.LocalRotation, first.LocalRotation), Is.LessThan(0.001f));
            Assert.That(second.LocalScale, Is.EqualTo(first.LocalScale));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_TransitionProjection_UsesSourceAndDestinationVisibleFaceUnion()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);

            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    sourceTopology,
                    destinationTopology,
                    EntityType.Unit,
                    out _),
                Is.True);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    new SurfaceCell(FaceId.Ceiling, 1, 0),
                    sourceTopology,
                    destinationTopology,
                    EntityType.Unit,
                    out _),
                Is.True);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    new SurfaceCell(FaceId.Back, 1, 0),
                    sourceTopology,
                    destinationTopology,
                    EntityType.Unit,
                    out _),
                Is.False);
            Assert.That(
                projector.TryResolveTransitionEntityRotation(
                    new SurfaceCell(FaceId.Ceiling, 1, 0),
                    sourceTopology,
                    destinationTopology,
                    Direction.Right,
                    out _),
                Is.True);
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_TransitionProjection_MatchesOrdinaryProjection_WhenTopologyDoesNotChange()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Front, 1, 0);

            Assert.That(
                projector.TryProjectEntityCell(
                    cell,
                    topology,
                    EntityType.Box,
                    out var ordinaryPose),
                Is.True);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    cell,
                    topology,
                    topology,
                    EntityType.Box,
                    out var transitionPose),
                Is.True);
            Assert.That(
                projector.TryResolveEntityRotation(
                    cell,
                    topology,
                    Direction.Left,
                    out var ordinaryRotation),
                Is.True);
            Assert.That(
                projector.TryResolveTransitionEntityRotation(
                    cell,
                    topology,
                    topology,
                    Direction.Left,
                    out var transitionRotation),
                Is.True);

            Assert.That(Vector3.Distance(ordinaryPose.LocalPosition, transitionPose.LocalPosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(ordinaryPose.Normal, transitionPose.Normal), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(ordinaryPose.LocalRotation, transitionPose.LocalRotation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(ordinaryRotation, transitionRotation), Is.LessThan(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_TransitionProjection_KeepsPhysicalFacePoseAcrossTopologyPairs()
        {
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
            var projector = new GameplayCubeProjector(boardBounds, 1f);
            var cell = new SurfaceCell(FaceId.Floor, 1, 0);
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var forwardTopology = new CubeTopologyState(FaceId.Front);
            var secondForwardTopology = new CubeTopologyState(FaceId.Ceiling);

            Assert.That(projector.TryProjectEntityCell(cell, sourceTopology, EntityType.Unit, out var committedPose), Is.True);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    cell,
                    sourceTopology,
                    forwardTopology,
                    EntityType.Unit,
                    out var forwardTransitionPose),
                Is.True);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    cell,
                    sourceTopology,
                    secondForwardTopology,
                    EntityType.Unit,
                    out var secondForwardTransitionPose),
                Is.True);
            Assert.That(
                projector.TryResolveEntityRotation(
                    cell,
                    sourceTopology,
                    Direction.Right,
                    out var committedRotation),
                Is.True);
            Assert.That(
                projector.TryResolveTransitionEntityRotation(
                    cell,
                    sourceTopology,
                    forwardTopology,
                    Direction.Right,
                    out var forwardTransitionRotation),
                Is.True);
            Assert.That(
                projector.TryResolveTransitionEntityRotation(
                    cell,
                    sourceTopology,
                    secondForwardTopology,
                    Direction.Right,
                    out var secondForwardTransitionRotation),
                Is.True);

            Assert.That(Vector3.Distance(committedPose.LocalPosition, forwardTransitionPose.LocalPosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(committedPose.LocalPosition, secondForwardTransitionPose.LocalPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(committedRotation, forwardTransitionRotation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(committedRotation, secondForwardTransitionRotation), Is.LessThan(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_GetProjectedTransitionEntitySlot_ReturnsPhysicalSlotInsteadOfDestinationRemap()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);

            Assert.That(
                projector.TryGetProjectedTransitionEntitySlot(
                    new SurfaceCell(FaceId.Ceiling, 0, 0),
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    out var projectedSlot),
                Is.True);
            Assert.That(projectedSlot, Is.EqualTo(GameplayProjectedFaceSlot.Top));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_FrontFaceRows_RiseAwayFromFloor()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Front, 0, 0),
                    topology,
                    EntityType.Unit,
                    out var lowerRowPose),
                Is.True);
            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Front, 0, 1),
                    topology,
                    EntityType.Unit,
                    out var upperRowPose),
                Is.True);

            Assert.That(upperRowPose.LocalPosition.y, Is.GreaterThan(lowerRowPose.LocalPosition.y));
            Assert.That(upperRowPose.LocalPosition.z, Is.EqualTo(lowerRowPose.LocalPosition.z).Within(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_FloorRows_AdvanceTowardPositiveZ()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    topology,
                    EntityType.Unit,
                    out var backRowPose),
                Is.True);
            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    topology,
                    EntityType.Unit,
                    out var frontRowPose),
                Is.True);

            Assert.That(frontRowPose.LocalPosition.z, Is.GreaterThan(backRowPose.LocalPosition.z));
            Assert.That(frontRowPose.LocalPosition.y, Is.EqualTo(backRowPose.LocalPosition.y).Within(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_SeparatesFloorAndFrontFacesWithOneCellSeamGap()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectSurfaceCell(
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    topology,
                    out var floorTopRowPose),
                Is.True);
            Assert.That(
                projector.TryProjectSurfaceCell(
                    new SurfaceCell(FaceId.Front, 0, 0),
                    topology,
                    out var frontBottomRowPose),
                Is.True);

            Assert.That(floorTopRowPose.LocalPosition.y, Is.EqualTo(-1.5f).Within(0.001f));
            Assert.That(floorTopRowPose.LocalPosition.z, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(frontBottomRowPose.LocalPosition.y, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(frontBottomRowPose.LocalPosition.z, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_UsesExplicitFaceSeamGapIndependentOfCellSize()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                2f,
                0.5f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectSurfaceCell(
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    topology,
                    out var floorTopRowPose),
                Is.True);
            Assert.That(
                projector.TryProjectSurfaceCell(
                    new SurfaceCell(FaceId.Front, 0, 0),
                    topology,
                    out var frontBottomRowPose),
                Is.True);

            Assert.That(projector.FaceSeamGap, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(floorTopRowPose.LocalPosition.y, Is.EqualTo(-2.25f).Within(0.001f));
            Assert.That(frontBottomRowPose.LocalPosition.z, Is.EqualTo(2.25f).Within(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_FloorRightFacing_AlignsWithPositiveXAxis()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryResolveEntityRotation(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    topology,
                    Direction.Right,
                    out var rotation),
                Is.True);

            Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.right), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(rotation * Vector3.forward, Vector3.down), Is.LessThan(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_FrontRightFacing_AlignsWithPositiveXAxis()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryResolveEntityRotation(
                    new SurfaceCell(FaceId.Front, 0, 0),
                    topology,
                    Direction.Right,
                    out var rotation),
                Is.True);

            Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.right), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(rotation * Vector3.forward, Vector3.forward), Is.LessThan(0.001f));
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraRig_DefaultPose_ProjectsFloorPositiveXToScreenRight()
        {
            var rigObject = new GameObject("GameplayCameraRig_DefaultPose_ProjectsFloorPositiveXToScreenRight");
            var cameraObject = new GameObject("ViewCamera");
            var targetObject = new GameObject("CameraTarget");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var topology = new CubeTopologyState(FaceId.Floor);
                var projector = new GameplayCubeProjector(boardBounds, 1f);
                rig.Initialize(viewCamera, targetObject.transform, projector.GetVisibleCubeBounds(topology));

                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        topology,
                        EntityType.Unit,
                        out var leftPose),
                    Is.True);
                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        topology,
                        EntityType.Unit,
                        out var rightPose),
                    Is.True);

                var leftViewport = viewCamera.WorldToViewportPoint(leftPose.LocalPosition);
                var rightViewport = viewCamera.WorldToViewportPoint(rightPose.LocalPosition);

                Assert.That(leftViewport.z, Is.GreaterThan(0f));
                Assert.That(rightViewport.z, Is.GreaterThan(0f));
                Assert.That(rightViewport.x, Is.GreaterThan(leftViewport.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraRig_DefaultPose_ProjectsFrontPositiveXToScreenRight()
        {
            var rigObject = new GameObject("GameplayCameraRig_DefaultPose_ProjectsFrontPositiveXToScreenRight");
            var cameraObject = new GameObject("ViewCamera");
            var targetObject = new GameObject("CameraTarget");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var topology = new CubeTopologyState(FaceId.Floor);
                var projector = new GameplayCubeProjector(boardBounds, 1f);
                rig.Initialize(viewCamera, targetObject.transform, projector.GetVisibleCubeBounds(topology));

                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Front, 0, 0),
                        topology,
                        EntityType.Unit,
                        out var leftPose),
                    Is.True);
                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Front, 1, 0),
                        topology,
                        EntityType.Unit,
                        out var rightPose),
                    Is.True);

                var leftViewport = viewCamera.WorldToViewportPoint(leftPose.LocalPosition);
                var rightViewport = viewCamera.WorldToViewportPoint(rightPose.LocalPosition);

                Assert.That(leftViewport.z, Is.GreaterThan(0f));
                Assert.That(rightViewport.z, Is.GreaterThan(0f));
                Assert.That(rightViewport.x, Is.GreaterThan(leftViewport.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraRig_ManualDistance_KeepsPositionWhenFieldOfViewChanges()
        {
            var rigObject = new GameObject("GameplayCameraRig_ManualDistance_KeepsPositionWhenFieldOfViewChanges");
            var cameraObject = new GameObject("ViewCamera");
            var targetObject = new GameObject("CameraTarget");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var visibleBounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));

                rig.ApplySettings(new GameplayCameraSettings
                {
                    PitchDegrees = 35f,
                    YawDegrees = 0f,
                    PerspectiveFieldOfView = 60f,
                    FramingPadding = 1.2f,
                    DistanceMode = CameraDistanceMode.Manual,
                    ManualDistance = 7f,
                });
                rig.Initialize(viewCamera, targetObject.transform, visibleBounds);
                var initialPosition = viewCamera.transform.position;

                rig.ApplySettings(new GameplayCameraSettings
                {
                    PitchDegrees = 35f,
                    YawDegrees = 0f,
                    PerspectiveFieldOfView = 30f,
                    FramingPadding = 1.2f,
                    DistanceMode = CameraDistanceMode.Manual,
                    ManualDistance = 7f,
                });

                Assert.That(Vector3.Distance(viewCamera.transform.position, initialPosition), Is.LessThan(0.0001f));
                Assert.That(viewCamera.fieldOfView, Is.EqualTo(30f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraRig_AutoFit_ChangesDistanceWhenFieldOfViewChanges()
        {
            var rigObject = new GameObject("GameplayCameraRig_AutoFit_ChangesDistanceWhenFieldOfViewChanges");
            var cameraObject = new GameObject("ViewCamera");
            var targetObject = new GameObject("CameraTarget");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var visibleBounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));

                rig.ApplySettings(new GameplayCameraSettings
                {
                    PitchDegrees = 35f,
                    YawDegrees = 0f,
                    PerspectiveFieldOfView = 60f,
                    FramingPadding = 1.2f,
                    DistanceMode = CameraDistanceMode.AutoFit,
                });
                rig.Initialize(viewCamera, targetObject.transform, visibleBounds);
                var initialDistance = Vector3.Distance(viewCamera.transform.position, targetObject.transform.position);

                rig.ApplySettings(new GameplayCameraSettings
                {
                    PitchDegrees = 35f,
                    YawDegrees = 0f,
                    PerspectiveFieldOfView = 30f,
                    FramingPadding = 1.2f,
                    DistanceMode = CameraDistanceMode.AutoFit,
                });
                var narrowedDistance = Vector3.Distance(viewCamera.transform.position, targetObject.transform.position);

                Assert.That(narrowedDistance, Is.GreaterThan(initialDistance));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraRig_DirectCameraAuthoredBaselineAndShake_MatchesFinalHierarchyPose_AndResets()
        {
            var rootObject = new GameObject("GameplayCameraRig_DirectCameraAuthoredBaselineAndShake_MatchesFinalHierarchyPose_AndResets");
            var cameraObject = new GameObject("GameplayCameraRig_DirectCameraAuthoredBaselineAndShake_MatchesFinalHierarchyPose_AndResets_ViewCamera");

            try
            {
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var targetWorldPosition = new Vector3(1.5f, -2f, 3.5f);
                boardRoot.CameraTargetRoot.position = targetWorldPosition;

                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;

                var rig = rootObject.AddComponent<GameplayCameraRig>();
                var authoredWorldPosition = new Vector3(4f, 6f, -5f);
                var authoredWorldRotation =
                    Quaternion.LookRotation((targetWorldPosition - authoredWorldPosition).normalized, Vector3.up);
                viewCamera.transform.SetPositionAndRotation(authoredWorldPosition, authoredWorldRotation);
                rig.CaptureAuthoredSceneCameraPose(
                    viewCamera.transform,
                    fieldOfView: 42f,
                    nearClipPlane: 0.2f,
                    farClipPlane: 90f);

                var baselineAuthoringPolicy = GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault();
                var baseSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 10f,
                    YawDegrees = 15f,
                    DistanceMode = CameraDistanceMode.AutoFit,
                    ManualDistance = 3f,
                    FramingPadding = 1.2f,
                    PerspectiveFieldOfView = 60f,
                    NearClipPlane = 0.03f,
                    FarClipPlane = 100f,
                };
                var resolvedSettings = rig.ResolveConfiguredSettings(
                    baseSettings,
                    baselineAuthoringPolicy,
                    targetWorldPosition,
                    new CubeTopologyState(FaceId.Floor),
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);

                rig.ApplySettings(resolvedSettings);
                rig.ConfigureTopologyTransitionCameraShake(TopologyTransitionCameraShakeProfile.CreateDefault());
                rig.Initialize(viewCamera, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                var presentedOrbit = Quaternion.Euler(90f, 0f, 0f);
                rig.SetPresentedTopologyOrbit(presentedOrbit);
                rig.ApplyTopologyTransitionVisualState(
                    CreateTopologyTransitionVisualState(
                        progress01: 0.12f,
                        CubeRotationKind.Forward,
                        presentedOrbit));
                rig.SnapToTarget();

                var authoredBaselineLocalPosition = authoredWorldPosition - targetWorldPosition;
                var authoredBaselineLocalRotation = authoredWorldRotation;
                var expectedUnshakenWorldRotation = presentedOrbit * authoredBaselineLocalRotation;
                var expectedUnshakenWorldPosition =
                    targetWorldPosition + (presentedOrbit * authoredBaselineLocalPosition);
                var expectedShakenWorldPosition = expectedUnshakenWorldPosition +
                                                  (expectedUnshakenWorldRotation * rig.TopologyTransitionShakeLocalPosition);
                var expectedShakenWorldRotation =
                    expectedUnshakenWorldRotation * rig.TopologyTransitionShakeLocalRotation;

                Assert.That(
                    Quaternion.Angle(boardRoot.CameraOrbitPivot.localRotation, presentedOrbit),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(boardRoot.CameraPoseRoot.localPosition, authoredBaselineLocalPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.CameraPoseRoot.localRotation, authoredBaselineLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(boardRoot.CameraEffectsRoot.localPosition, rig.TopologyTransitionShakeLocalPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.CameraEffectsRoot.localRotation, rig.TopologyTransitionShakeLocalRotation),
                    Is.LessThan(0.001f));
                AssertTransformPoseApproximately(
                    viewCamera.transform,
                    expectedShakenWorldPosition,
                    expectedShakenWorldRotation);
                AssertTransformPoseApproximately(viewCamera.transform, boardRoot.CameraEffectsRoot);

                rig.ApplyTopologyTransitionVisualState(
                    TopologyTransitionVisualState.Inactive(new CubeTopologyState(FaceId.Floor), presentedOrbit));
                rig.SnapToTarget();

                Assert.That(boardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(boardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(
                    Vector3.Distance(boardRoot.CameraPoseRoot.localPosition, authoredBaselineLocalPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.CameraPoseRoot.localRotation, authoredBaselineLocalRotation),
                    Is.LessThan(0.001f));
                AssertTransformPoseApproximately(
                    viewCamera.transform,
                    expectedUnshakenWorldPosition,
                    expectedUnshakenWorldRotation);
                AssertTransformPoseApproximately(viewCamera.transform, boardRoot.CameraEffectsRoot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCameraRig_DirectCameraAndHierarchyPaths_PreserveBaselineOrbitShakeParity()
        {
            var directRootObject =
                new GameObject("GameplayCameraRig_DirectCameraAndHierarchyPaths_PreserveBaselineOrbitShakeParity_Direct");
            var hierarchyRootObject =
                new GameObject("GameplayCameraRig_DirectCameraAndHierarchyPaths_PreserveBaselineOrbitShakeParity_Hierarchy");
            var directCameraObject =
                new GameObject("GameplayCameraRig_DirectCameraAndHierarchyPaths_PreserveBaselineOrbitShakeParity_ViewCamera");
            var authoredPoseObject =
                new GameObject("GameplayCameraRig_DirectCameraAndHierarchyPaths_PreserveBaselineOrbitShakeParity_AuthoredPose");

            try
            {
                var directBoardRoot = directRootObject.AddComponent<GameplayBoardRoot>();
                directBoardRoot.EnsureHierarchy();
                var hierarchyBoardRoot = hierarchyRootObject.AddComponent<GameplayBoardRoot>();
                hierarchyBoardRoot.EnsureHierarchy();

                var targetWorldPosition = new Vector3(-1.5f, 0.5f, 2.5f);
                directBoardRoot.CameraTargetRoot.position = targetWorldPosition;
                hierarchyBoardRoot.CameraTargetRoot.position = targetWorldPosition;

                var authoredWorldPosition = new Vector3(6f, 5f, -7f);
                var authoredWorldRotation =
                    Quaternion.LookRotation((targetWorldPosition - authoredWorldPosition).normalized, Vector3.up);
                authoredPoseObject.transform.SetPositionAndRotation(authoredWorldPosition, authoredWorldRotation);

                var directCamera = directCameraObject.AddComponent<Camera>();
                directCamera.aspect = 16f / 9f;
                var directRig = directRootObject.AddComponent<GameplayCameraRig>();
                var hierarchyRig = hierarchyRootObject.AddComponent<GameplayCameraRig>();
                directRig.CaptureAuthoredSceneCameraPose(authoredPoseObject.transform, 44f, 0.2f, 88f);
                hierarchyRig.CaptureAuthoredSceneCameraPose(authoredPoseObject.transform, 44f, 0.2f, 88f);

                var baselineAuthoringPolicy = new GameplayCameraBaselineAuthoringPolicy
                {
                    UseAuthoredSceneCameraPose = true,
                    UseAuthoredSceneCameraLens = false,
                };
                var baseSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 5f,
                    YawDegrees = 17f,
                    DistanceMode = CameraDistanceMode.AutoFit,
                    ManualDistance = 2f,
                    FramingPadding = 1.2f,
                    PerspectiveFieldOfView = 60f,
                    NearClipPlane = 0.03f,
                    FarClipPlane = 100f,
                };
                var directResolvedSettings = directRig.ResolveConfiguredSettings(
                    baseSettings,
                    baselineAuthoringPolicy,
                    targetWorldPosition,
                    new CubeTopologyState(FaceId.Floor),
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);
                var hierarchyResolvedSettings = hierarchyRig.ResolveConfiguredSettings(
                    baseSettings,
                    baselineAuthoringPolicy,
                    targetWorldPosition,
                    new CubeTopologyState(FaceId.Floor),
                    TopologyRotationVisualMapping.ForwardUsesPositiveX);

                directRig.ApplySettings(directResolvedSettings);
                hierarchyRig.ApplySettings(hierarchyResolvedSettings);
                directRig.ConfigureTopologyTransitionCameraShake(TopologyTransitionCameraShakeProfile.CreateDefault());
                hierarchyRig.ConfigureTopologyTransitionCameraShake(TopologyTransitionCameraShakeProfile.CreateDefault());
                directRig.Initialize(directCamera, directBoardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));
                hierarchyRig.Initialize(null, hierarchyBoardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                var hierarchyCameraProxy = new GameObject("HierarchyCameraProxy");
                hierarchyCameraProxy.transform.SetParent(hierarchyBoardRoot.CameraEffectsRoot, worldPositionStays: false);
                hierarchyCameraProxy.transform.localPosition = Vector3.zero;
                hierarchyCameraProxy.transform.localRotation = Quaternion.identity;

                var presentedOrbit = Quaternion.Euler(90f, 0f, 0f);
                var activeVisualState = CreateTopologyTransitionVisualState(
                    progress01: 0.12f,
                    CubeRotationKind.Forward,
                    presentedOrbit);

                directRig.SetPresentedTopologyOrbit(presentedOrbit);
                hierarchyRig.SetPresentedTopologyOrbit(presentedOrbit);
                directRig.ApplyTopologyTransitionVisualState(activeVisualState);
                hierarchyRig.ApplyTopologyTransitionVisualState(activeVisualState);
                directRig.SnapToTarget();
                hierarchyRig.SnapToTarget();

                Assert.That(
                    Vector3.Distance(
                        directBoardRoot.CameraPoseRoot.localPosition,
                        hierarchyBoardRoot.CameraPoseRoot.localPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        directBoardRoot.CameraPoseRoot.localRotation,
                        hierarchyBoardRoot.CameraPoseRoot.localRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(
                        directBoardRoot.CameraEffectsRoot.localPosition,
                        hierarchyBoardRoot.CameraEffectsRoot.localPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        directBoardRoot.CameraEffectsRoot.localRotation,
                        hierarchyBoardRoot.CameraEffectsRoot.localRotation),
                    Is.LessThan(0.001f));
                AssertTransformPoseApproximately(directCamera.transform, hierarchyCameraProxy.transform);

                var inactiveVisualState =
                    TopologyTransitionVisualState.Inactive(new CubeTopologyState(FaceId.Floor), presentedOrbit);
                directRig.ApplyTopologyTransitionVisualState(inactiveVisualState);
                hierarchyRig.ApplyTopologyTransitionVisualState(inactiveVisualState);
                directRig.SnapToTarget();
                hierarchyRig.SnapToTarget();

                Assert.That(directBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(hierarchyBoardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(directBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(hierarchyBoardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
                AssertTransformPoseApproximately(directCamera.transform, hierarchyCameraProxy.transform);

                UnityEngine.Object.DestroyImmediate(hierarchyCameraProxy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(directRootObject);
                UnityEngine.Object.DestroyImmediate(hierarchyRootObject);
                UnityEngine.Object.DestroyImmediate(directCameraObject);
                UnityEngine.Object.DestroyImmediate(authoredPoseObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayCubeProjector_RejectsInactiveFaceEntityProjection()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Ceiling, 1, 1),
                    topology,
                    EntityType.Unit,
                    out _),
                Is.False);
        }

        [Test]
        [Category("Full")]
        public void GameplayBoardSurfaceRenderer_CreatesExpectedVisibleFaceTiles()
        {
            var rootObject = new GameObject("GameplayBoardSurfaceRenderer_CreatesExpectedVisibleFaceTiles");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var topology = new CubeTopologyState(FaceId.Floor);
                rootObject.transform.position = new Vector3(2f, 1f, -3f);

                renderer.Initialize(boardBounds, 1f, topology);

                Assert.That(renderer.VisibleTilePoolRoot.parent, Is.EqualTo(renderer.transform));
                Assert.That(renderer.VisibleTilePoolRoot.childCount, Is.EqualTo(8));
                Assert.That(renderer.ActiveTileCount, Is.EqualTo(8));

                var bottomTile = FindSurfaceTile(renderer, "ActiveBottom_Floor_0_0");
                var frontTile = FindSurfaceTile(renderer, "ActiveFront_Front_1_1");

                AssertSurfaceTileMatchesProjection(bottomTile, boardBounds, topology, new SurfaceCell(FaceId.Floor, 0, 0));
                AssertSurfaceTileMatchesProjection(frontTile, boardBounds, topology, new SurfaceCell(FaceId.Front, 1, 1));

                Assert.That(bottomTile.GetComponent<Collider>(), Is.Null);
                Assert.That(frontTile.GetComponent<Collider>(), Is.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("DecorativeTop_Ceiling_0_1"), Is.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("DecorativeBack_Back_1_0"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayBoardSurfaceRenderer_CompleteTopologyTransition_SameCommittedTopologyKeepsVisibleTilesActive()
        {
            var rootObject =
                new GameObject("GameplayBoardSurfaceRenderer_CompleteTopologyTransition_SameCommittedTopologyKeepsVisibleTilesActive");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();
                var boardBounds = new BoardBounds(Vector2Int.zero, Vector2Int.zero);
                var topology = new CubeTopologyState(FaceId.Floor);

                renderer.Initialize(boardBounds, 1f, topology);

                var bottomTile = FindSurfaceTile(renderer, "ActiveBottom_Floor_0_0");
                var frontTile = FindSurfaceTile(renderer, "ActiveFront_Front_0_0");
                var bottomProbe = bottomTile.AddComponent<SurfaceTileLifecycleProbe>();
                var frontProbe = frontTile.AddComponent<SurfaceTileLifecycleProbe>();
                bottomProbe.ResetCounts();
                frontProbe.ResetCounts();
                var visibleChildCount = renderer.VisibleTilePoolRoot.childCount;
                var activeTileCount = renderer.ActiveTileCount;

                Assert.That(renderer.CanSkipCompleteTopologyTransition(topology), Is.True);

                renderer.CompleteTopologyTransition(topology);

                Assert.That(renderer.CanSkipCompleteTopologyTransition(topology), Is.True);
                Assert.That(renderer.VisibleTilePoolRoot.childCount, Is.EqualTo(visibleChildCount));
                Assert.That(renderer.ActiveTileCount, Is.EqualTo(activeTileCount));
                Assert.That(FindSurfaceTile(renderer, "ActiveBottom_Floor_0_0"), Is.SameAs(bottomTile));
                Assert.That(FindSurfaceTile(renderer, "ActiveFront_Front_0_0"), Is.SameAs(frontTile));
                Assert.That(bottomTile.activeSelf, Is.True);
                Assert.That(frontTile.activeSelf, Is.True);
                Assert.That(bottomProbe.EnabledCount, Is.Zero);
                Assert.That(bottomProbe.DisabledCount, Is.Zero);
                Assert.That(frontProbe.EnabledCount, Is.Zero);
                Assert.That(frontProbe.DisabledCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayBoardSurfaceRenderer_CompleteTopologyTransition_AfterBeginRefreshesDestinationOnceThenSkips()
        {
            var rootObject =
                new GameObject("GameplayBoardSurfaceRenderer_CompleteTopologyTransition_AfterBeginRefreshesDestinationOnceThenSkips");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();
                var boardBounds = new BoardBounds(Vector2Int.zero, Vector2Int.zero);
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);

                renderer.Initialize(boardBounds, 1f, sourceTopology);
                var sourceBottomTile = FindSurfaceTile(renderer, "ActiveBottom_Floor_0_0");

                renderer.BeginTopologyTransition(sourceTopology, destinationTopology);

                Assert.That(renderer.CanSkipCompleteTopologyTransition(destinationTopology), Is.False);
                Assert.That(sourceBottomTile.activeSelf, Is.False);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveFront_Ceiling_0_0"), Is.Null);

                renderer.CompleteTopologyTransition(destinationTopology);

                Assert.That(renderer.SteadyTopology, Is.EqualTo(destinationTopology));
                Assert.That(renderer.TransitionTileCount, Is.Zero);
                Assert.That(renderer.CanSkipCompleteTopologyTransition(destinationTopology), Is.True);
                var destinationBottomTile = FindSurfaceTile(renderer, "ActiveBottom_Front_0_0");
                var destinationFrontTile = FindSurfaceTile(renderer, "ActiveFront_Ceiling_0_0");
                var destinationBottomProbe = destinationBottomTile.AddComponent<SurfaceTileLifecycleProbe>();
                var destinationFrontProbe = destinationFrontTile.AddComponent<SurfaceTileLifecycleProbe>();
                Assert.That(destinationBottomTile.activeSelf, Is.True);
                Assert.That(destinationFrontTile.activeSelf, Is.True);

                destinationBottomProbe.ResetCounts();
                destinationFrontProbe.ResetCounts();
                var visibleChildCount = renderer.VisibleTilePoolRoot.childCount;
                var activeTileCount = renderer.ActiveTileCount;

                renderer.CompleteTopologyTransition(destinationTopology);

                Assert.That(renderer.VisibleTilePoolRoot.childCount, Is.EqualTo(visibleChildCount));
                Assert.That(renderer.ActiveTileCount, Is.EqualTo(activeTileCount));
                Assert.That(FindSurfaceTile(renderer, "ActiveBottom_Front_0_0"), Is.SameAs(destinationBottomTile));
                Assert.That(FindSurfaceTile(renderer, "ActiveFront_Ceiling_0_0"), Is.SameAs(destinationFrontTile));
                Assert.That(destinationBottomProbe.EnabledCount, Is.Zero);
                Assert.That(destinationBottomProbe.DisabledCount, Is.Zero);
                Assert.That(destinationFrontProbe.EnabledCount, Is.Zero);
                Assert.That(destinationFrontProbe.DisabledCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_UsesConfiguredBoardSurfaceTextureForAllTiles()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_UsesConfiguredBoardSurfaceTextureForAllTiles");
            Texture2D sharedTileTexture = null;

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                sharedTileTexture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
                {
                    name = "SharedBoardSurfaceTexture",
                };
                sharedTileTexture.SetPixel(0, 0, new Color(0.15f, 0.35f, 0.65f, 1f));
                sharedTileTexture.SetPixel(1, 0, new Color(0.2f, 0.45f, 0.75f, 1f));
                sharedTileTexture.SetPixel(0, 1, new Color(0.1f, 0.25f, 0.55f, 1f));
                sharedTileTexture.SetPixel(1, 1, new Color(0.25f, 0.5f, 0.8f, 1f));
                sharedTileTexture.Apply();

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = new SurfaceCell(FaceId.Floor, 0, 0),
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Up,
                            },
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        BoardSurfaceTexture = sharedTileTexture,
                        AutoCreateViews = false,
                    });

                var bottomTile = FindSurfaceTile(host.BoardSurfaceRenderer, "ActiveBottom_Floor_0_0");
                var frontTile = FindSurfaceTile(host.BoardSurfaceRenderer, "ActiveFront_Front_1_1");
                var bottomRenderer = bottomTile.GetComponent<MeshRenderer>();
                var frontRenderer = frontTile.GetComponent<MeshRenderer>();

                Assert.That(bottomRenderer, Is.Not.Null);
                Assert.That(frontRenderer, Is.Not.Null);
                Assert.That(bottomRenderer.sharedMaterial, Is.SameAs(frontRenderer.sharedMaterial));
                Assert.That(bottomRenderer.sharedMaterial.mainTexture, Is.SameAs(sharedTileTexture));
            }
            finally
            {
                if (sharedTileTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(sharedTileTexture);
                }

                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayBoardSurfaceRenderer_TopologyTransition_UsesDestinationVisibleFacesAtStart()
        {
            var rootObject = new GameObject("GameplayBoardSurfaceRenderer_TopologyTransition_UsesDestinationVisibleFacesAtStart");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);

                renderer.Initialize(boardBounds, 1f, sourceTopology);
                renderer.BeginTopologyTransition(
                    sourceTopology,
                    destinationTopology);

                Assert.That(renderer.SteadyTileCount, Is.EqualTo(8));
                Assert.That(renderer.SteadyTopology, Is.EqualTo(sourceTopology));
                Assert.That(renderer.ActiveTileCount, Is.EqualTo(8));
                Assert.That(renderer.TransitionTileCount, Is.EqualTo(8));
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Not.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Floor_0_0").gameObject.activeSelf, Is.False);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveFront_Front_0_0"), Is.Not.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveFront_Front_0_0").gameObject.activeSelf, Is.False);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveFront_Ceiling_0_0"), Is.Null);
                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Null);
                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Not.Null);
                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveFront_Ceiling_0_0"), Is.Not.Null);

                AssertSurfaceTileMatchesRetainedTransitionProjection(
                    renderer.TransitionTilePoolRoot.Find("ActiveBottom_Front_0_0").gameObject,
                    boardBounds,
                    sourceTopology,
                    destinationTopology,
                    new SurfaceCell(FaceId.Front, 0, 0));
                AssertSurfaceTileMatchesRetainedTransitionProjection(
                    renderer.TransitionTilePoolRoot.Find("ActiveFront_Ceiling_0_0").gameObject,
                    boardBounds,
                    sourceTopology,
                    destinationTopology,
                    new SurfaceCell(FaceId.Ceiling, 0, 0));

                renderer.CompleteTopologyTransition(destinationTopology);

                Assert.That(renderer.SteadyTopology, Is.EqualTo(destinationTopology));
                Assert.That(renderer.TransitionTileCount, Is.Zero);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Not.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Front_0_0").gameObject.activeSelf, Is.True);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveFront_Ceiling_0_0"), Is.Not.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveFront_Ceiling_0_0").gameObject.activeSelf, Is.True);
                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Null);
                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Not.Null);
                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveBottom_Front_0_0").gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayBoardSurfaceRenderer_ActiveFaceCover_FollowsDestinationActiveFacesAcrossTopologyTransitions()
        {
            var rootObject = new GameObject(
                "GameplayBoardSurfaceRenderer_ActiveFaceCover_FollowsDestinationActiveFacesAcrossTopologyTransitions");
            var coverPrefab = CreateActiveFaceCoverPrefab("ActiveFaceCoverPrefab");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var floorTopology = new CubeTopologyState(FaceId.Floor);
                var frontTopology = new CubeTopologyState(FaceId.Front);

                renderer.Initialize(
                    boardBounds,
                    1f,
                    floorTopology,
                    activeFaceCoverPrefab: coverPrefab);

                AssertActiveFaceCoverTiling(AssertActiveFaceCover(renderer, "ActiveBottomCover_Floor"), 2f, 2f);
                AssertActiveFaceCoverTiling(AssertActiveFaceCover(renderer, "ActiveFrontCover_Front"), 2f, 2f);
                Assert.That(renderer.ActiveFaceCoverRoot.childCount, Is.EqualTo(2));

                renderer.BeginTopologyTransition(floorTopology, frontTopology);

                Assert.That(renderer.ActiveFaceCoverRoot.Find("ActiveBottomCover_Floor"), Is.Null);
                AssertActiveFaceCoverTiling(AssertActiveFaceCover(renderer, "ActiveBottomCover_Front"), 2f, 2f);
                AssertActiveFaceCoverTiling(AssertActiveFaceCover(renderer, "ActiveFrontCover_Ceiling"), 2f, 2f);
                Assert.That(renderer.ActiveFaceCoverRoot.childCount, Is.EqualTo(2));

                renderer.CompleteTopologyTransition(frontTopology);

                AssertActiveFaceCover(renderer, "ActiveBottomCover_Front");
                AssertActiveFaceCover(renderer, "ActiveFrontCover_Ceiling");
                Assert.That(renderer.ActiveFaceCoverRoot.childCount, Is.EqualTo(2));

                renderer.BeginTopologyTransition(frontTopology, floorTopology);

                Assert.That(renderer.ActiveFaceCoverRoot.Find("ActiveFrontCover_Ceiling"), Is.Null);
                AssertActiveFaceCover(renderer, "ActiveBottomCover_Floor");
                AssertActiveFaceCover(renderer, "ActiveFrontCover_Front");
                Assert.That(renderer.ActiveFaceCoverRoot.childCount, Is.EqualTo(2));

                renderer.CompleteTopologyTransition(floorTopology);

                AssertActiveFaceCover(renderer, "ActiveBottomCover_Floor");
                AssertActiveFaceCover(renderer, "ActiveFrontCover_Front");
                Assert.That(renderer.ActiveFaceCoverRoot.childCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(coverPrefab);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayBoardSurfaceRenderer_TopologyTransition_StartWorldPosesMatchDestinationVisibleSurfacePoses()
        {
            var rootObject = new GameObject("GameplayBoardSurfaceRenderer_TopologyTransition_StartWorldPosesMatchDestinationVisibleSurfacePoses");

            try
            {
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var renderer = boardRoot.EnsureBoardSurfaceRenderer();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 1));
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                const float cellSize = 1.75f;
                var sharedCell = new SurfaceCell(FaceId.Front, 1, 1);
                var enteringCell = new SurfaceCell(FaceId.Ceiling, 2, 1);
                renderer.Initialize(boardBounds, cellSize, sourceTopology);
                renderer.BeginTopologyTransition(
                    sourceTopology,
                    destinationTopology);

                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Null);
                var sharedTile = renderer.TransitionTilePoolRoot.Find("ActiveBottom_Front_1_1").gameObject;
                var enteringTile = renderer.TransitionTilePoolRoot.Find("ActiveFront_Ceiling_2_1").gameObject;

                AssertSurfaceTileMatchesRetainedTransitionProjection(
                    sharedTile,
                    boardBounds,
                    sourceTopology,
                    destinationTopology,
                    sharedCell,
                    cellSize);
                AssertSurfaceTileMatchesRetainedTransitionProjection(
                    enteringTile,
                    boardBounds,
                    sourceTopology,
                    destinationTopology,
                    enteringCell,
                    cellSize);
                var sharedStartLocalPosition = sharedTile.transform.localPosition;
                var sharedStartLocalRotation = sharedTile.transform.localRotation;
                var enteringStartLocalPosition = enteringTile.transform.localPosition;
                var enteringStartLocalRotation = enteringTile.transform.localRotation;
                var sharedEnteringStartDistance = Vector3.Distance(
                    sharedTile.transform.position,
                    enteringTile.transform.position);

                renderer.UpdateTopologyTransition(0.5f);

                Assert.That(sharedTile.transform.localPosition, Is.EqualTo(sharedStartLocalPosition));
                Assert.That(Quaternion.Angle(sharedTile.transform.localRotation, sharedStartLocalRotation), Is.LessThan(0.001f));
                Assert.That(enteringTile.transform.localPosition, Is.EqualTo(enteringStartLocalPosition));
                Assert.That(Quaternion.Angle(enteringTile.transform.localRotation, enteringStartLocalRotation), Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(sharedTile.transform.position, enteringTile.transform.position),
                    Is.EqualTo(sharedEnteringStartDistance).Within(0.001f));
                Assert.That(
                    sharedTile.transform.localScale.z,
                    Is.EqualTo(cellSize * GameplayPresentationGeometry.TileThicknessMultiplier).Within(0.001f));
                Assert.That(
                    enteringTile.transform.localScale.z,
                    Is.EqualTo(cellSize * GameplayPresentationGeometry.TileThicknessMultiplier).Within(0.001f));

                renderer.UpdateTopologyTransition(1f);
                AssertSurfaceTileMatchesRetainedTransitionProjection(
                    sharedTile,
                    boardBounds,
                    sourceTopology,
                    destinationTopology,
                    sharedCell,
                    cellSize);
                AssertSurfaceTileMatchesRetainedTransitionProjection(
                    enteringTile,
                    boardBounds,
                    sourceTopology,
                    destinationTopology,
                    enteringCell,
                    cellSize);
                Assert.That(
                    Vector3.Distance(sharedTile.transform.position, enteringTile.transform.position),
                    Is.EqualTo(sharedEnteringStartDistance).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_PresentsOnlyActiveFaceEntitiesIn3D()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentsOnlyActiveFaceEntitiesIn3D");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        CreateSurfaceUnit(20, new SurfaceCell(FaceId.Front, 1, 0)),
                        CreateSurfaceUnit(30, new SurfaceCell(FaceId.Ceiling, 0, 0)),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var bottomView), Is.True);
                Assert.That(bottomView.gameObject.activeSelf, Is.True);
                Assert.That(bottomView.transform.localPosition, Is.EqualTo(GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 1))));
                Assert.That(bottomView.transform.position, Is.EqualTo(bottomView.transform.localPosition));

                Assert.That(registry.TryGetView(20, out var frontView), Is.True);
                Assert.That(frontView.gameObject.activeSelf, Is.True);
                Assert.That(frontView.transform.localPosition, Is.EqualTo(GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    new SurfaceCell(FaceId.Front, 1, 0))));
                Assert.That(frontView.transform.position, Is.EqualTo(frontView.transform.localPosition));

                Assert.That(registry.TryGetView(30, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_HidesDetachedEntitiesBeforeCleanupRemoval()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_HidesDetachedEntitiesBeforeCleanupRemoval");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    },
                    topology);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), boardPresence: EntityBoardPresence.Detached),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_KeepsMarkedForDeathEntityVisibleWhileStillOccupying()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_KeepsMarkedForDeathEntityVisibleWhileStillOccupying");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), markedForDeath: true),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_ForwardTopologyChange_UsesProjectedCubePose()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ForwardTopologyChange_UsesProjectedCubePose");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var initialTopology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                        },
                        new CubeTopologyState(FaceId.Front)));
                presenter.UpdatePresentation(0f);

                Assert.That(
                    Quaternion.Angle(
                        presenter.PresentedBoardRotation,
                        ResolveRestTopologyReferenceRotation(new CubeTopologyState(FaceId.Front))),
                    Is.LessThan(0.001f));
                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(
                    view.transform.position,
                    Is.EqualTo(GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        new CubeTopologyState(FaceId.Front),
                        new SurfaceCell(FaceId.Front, 0, 0))));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_BackwardTopologyChange_UsesProjectedCubePose()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BackwardTopologyChange_UsesProjectedCubePose");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var initialTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        new CubeTopologyState(FaceId.Floor)));
                presenter.UpdatePresentation(0f);

                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(
                    view.transform.position,
                    Is.EqualTo(GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        new CubeTopologyState(FaceId.Floor),
                        new SurfaceCell(FaceId.Floor, 0, 1))));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_PresentationPhase_TransitionsBetweenIdleEntityMotionAndTopologyTransition()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentationPhase_TransitionsBetweenIdleEntityMotionAndTopologyTransition");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    },
                    initialTopology);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(presenter.IsPresentationActive, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.SourceTopology, Is.EqualTo(initialTopology));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.DestinationTopology, Is.EqualTo(initialTopology));

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                        },
                        initialTopology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    10,
                                    TickEntityMotionKind.Move,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
                Assert.That(presenter.IsPresentationActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);

                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(presenter.IsPresentationActive, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 1, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));
                Assert.That(presenter.IsPresentationActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(presenter.IsTopologyTransitionActive, Is.True);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.Progress01, Is.EqualTo(0f).Within(0.001f));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.SourceTopology, Is.EqualTo(initialTopology));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.DestinationTopology, Is.EqualTo(rotatedTopology));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
                Assert.That(
                    presenter.CurrentTopologyTransitionVisualState.DurationSeconds,
                    Is.EqualTo(timingProfile.TopologyMotionDurationSeconds).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        presenter.PresentedBoardRotation,
                        ResolveRestTopologyReferenceRotation(initialTopology)),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        presenter.CurrentTopologyTransitionVisualState.PresentedVisualRotation,
                        presenter.PresentedBoardRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    presenter.CurrentTopologyTransitionVisualState.AngularVelocityNormalized,
                    Is.EqualTo(0f).Within(0.001f));

                var topologyDuration = presenter.CurrentTopologyTransitionVisualState.DurationSeconds;
                presenter.UpdatePresentation(topologyDuration * 0.25f);
                var pausedProgress = presenter.CurrentTopologyTransitionVisualState.Progress01;

                presenter.SetPresentationPaused(true);
                presenter.UpdatePresentation(topologyDuration);

                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(presenter.IsTopologyTransitionActive, Is.True);
                Assert.That(
                    presenter.CurrentTopologyTransitionVisualState.Progress01,
                    Is.EqualTo(pausedProgress).Within(0.001f));

                presenter.SetPresentationPaused(false);
                presenter.UpdatePresentation(topologyDuration);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(presenter.IsPresentationActive, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.SourceTopology, Is.EqualTo(rotatedTopology));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.DestinationTopology, Is.EqualTo(rotatedTopology));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_PresentationPhase_TreatsVisibilityTrackAsEntityMotion()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentationPhase_TreatsVisibilityTrackAsEntityMotion");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(
                                    10,
                                    TickVisibilityChangeKind.Remove,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    topology,
                                    Direction.Up),
                            })));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
                Assert.That(presenter.IsPresentationActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);

                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_Present_MapsEnemyAttackHitAndMoveSignalsToAnimatorDrivers()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_MapsEnemyAttackHitAndMoveSignalsToAnimatorDrivers");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 1);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(40, sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                        CreateSurfaceUnit(50, targetCell, aiMode: EnemyAiMode.Chase),
                    },
                    topology);

                Assert.That(registry.TryGetView(40, out var attackerView), Is.True);
                Assert.That(registry.TryGetView(50, out var targetView), Is.True);

                var attackerDriver = attackerView.GetComponent<EnemyAnimatorDriver>();
                var targetDriver = targetView.GetComponent<EnemyAnimatorDriver>();
                Assert.That(attackerDriver, Is.Not.Null);
                Assert.That(targetDriver, Is.Not.Null);

                var attackGroup = new ActionGroup(intentId: 1, sourceId: 40, priority: 100, ActionGroupKind.Attack);
                attackGroup.Damages.Add(new DamageAction(targetId: 50, amount: 1));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(40, destinationCell, aiMode: EnemyAiMode.Recover, facing: Direction.Right),
                        CreateSurfaceUnit(50, targetCell, aiMode: EnemyAiMode.Chase),
                    },
                    topology,
                    new TickPresentationData(
                        new[]
                        {
                            new TickEntityMotion(40, TickEntityMotionKind.Move, sourceCell, destinationCell),
                        },
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>()),
                    attackPhaseResult: CreateAttackPhaseResult(attackGroup)));
                presenter.UpdatePresentation(0f);

                Assert.That(attackerDriver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(attackerDriver.AttackSignalCount, Is.GreaterThanOrEqualTo(0));
                Assert.That(attackerDriver.IsMoving, Is.True);
                Assert.That(targetDriver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(targetDriver.HitSignalCount, Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_Present_MapsEnemyWindupExecuteAndRecoverySignalsToAnimatorDriver()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_MapsEnemyWindupExecuteAndRecoverySignalsToAnimatorDriver");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var enemyCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(40, enemyCell, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                    },
                    topology);

                Assert.That(registry.TryGetView(40, out var view), Is.True);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(40, enemyCell, aiMode: EnemyAiMode.Attack, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            new TickEnemyActionPresentationSignal(
                                40,
                                EnemyActionKind.Melee,
                                activeActionSequence: 1,
                                startedThisTick: true,
                                canceledThisTick: false,
                                executedThisTick: false,
                                startedRecoveryThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Attack));
                Assert.That(driver.CurrentActiveActionKind, Is.EqualTo(EnemyActionKind.Melee));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.AttackSignalCount, Is.EqualTo(0));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(0));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(40, enemyCell, aiMode: EnemyAiMode.Recover, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            new TickEnemyActionPresentationSignal(
                                40,
                                EnemyActionKind.Melee,
                                activeActionSequence: 1,
                                startedThisTick: false,
                                canceledThisTick: false,
                                executedThisTick: true,
                                startedRecoveryThisTick: true),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(driver.CurrentActiveActionKind, Is.EqualTo(EnemyActionKind.Melee));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.AttackSignalCount, Is.EqualTo(1));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_Present_ZeroWindupEnemyExecuteSignal_DoesNotTriggerWindup()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_ZeroWindupEnemyExecuteSignal_DoesNotTriggerWindup");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var enemyCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(40, enemyCell, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                    },
                    topology);

                Assert.That(registry.TryGetView(40, out var view), Is.True);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(40, enemyCell, aiMode: EnemyAiMode.Recover, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            new TickEnemyActionPresentationSignal(
                                40,
                                EnemyActionKind.Melee,
                                activeActionSequence: 1,
                                startedThisTick: true,
                                canceledThisTick: false,
                                executedThisTick: true,
                                startedRecoveryThisTick: true),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(driver.CurrentActiveActionKind, Is.EqualTo(EnemyActionKind.Melee));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(0));
                Assert.That(driver.AttackSignalCount, Is.EqualTo(1));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
                Assert.That(driver.LastPresentationState.StartedWindupThisTick, Is.False);
                Assert.That(driver.LastPresentationState.ExecutedThisTick, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_Present_RemovedEnemy_MapsDeathSignalToAnimatorDriver()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_RemovedEnemy_MapsDeathSignalToAnimatorDriver");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(40, sourceCell, aiMode: EnemyAiMode.Chase),
                    },
                    topology);

                Assert.That(registry.TryGetView(40, out var view), Is.True);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    Array.Empty<EntityState>(),
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        new[]
                        {
                            new TickVisibilityChange(40, TickVisibilityChangeKind.Remove, sourceCell, topology, Direction.Up),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.DeathSignalCount, Is.EqualTo(1));
                Assert.That(view.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PlayerDeathVisibilityTail_DoesNotHideBeforeDeathClipCompletes()
        {
            var rootObject = new GameObject("PlayerDeathVisibilityTail_DoesNotHideBeforeDeathClipCompletes");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("PlayerDeathVisibilityTail_PlayerPrefab");

            try
            {
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3DM/1Player/Player_S1.controller");
                Assert.That(controller, Is.Not.Null);

                var driver = playerViewPrefab.GetComponent<PlayerAnimatorDriver>();
                var animator = playerViewPrefab.gameObject.AddComponent<Animator>();
                var deathClipLengthSeconds = controller.animationClips
                    .Where(clip => clip != null && string.Equals(clip.name, "Death", StringComparison.Ordinal))
                    .Select(clip => clip.length)
                    .Single();

                animator.runtimeAnimatorController = controller;
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "animator", animator);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "deathStateName", "Death");

                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.2f,
                    boxSlideStepIntervalSeconds: 0.1f,
                    projectileStepIntervalSeconds: 0.1f,
                    moveMotionDurationSeconds: 0.05f,
                    pushMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 0.05f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateSurfaceUnit(10, sourceCell, facing: Direction.Right) }, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var runtimeDriver = playerView.GetComponent<PlayerAnimatorDriver>();
                Assert.That(runtimeDriver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    Array.Empty<EntityState>(),
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        new[]
                        {
                            new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, sourceCell, topology, Direction.Right),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(runtimeDriver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(playerView.gameObject.activeSelf, Is.True);

                presenter.UpdatePresentation(deathClipLengthSeconds * 0.5f);
                Assert.That(playerView.gameObject.activeSelf, Is.True);

                presenter.UpdatePresentation((deathClipLengthSeconds * 0.5f) + 0.02f);
                Assert.That(playerView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_Present_PlayerMoveMotion_ResolvesWalkThenIdleAfterTrackCompletes()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_PlayerMoveMotion_ResolvesWalkThenIdleAfterTrackCompletes");

            try
            {
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.3f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(registry.transform, attachPlayerAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateSurfaceUnit(10, sourceCell, facing: Direction.Right) }, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        new[]
                        {
                            new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, destinationCell),
                        },
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: true, moveMotionGeneratedThisTick: true),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));

                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);
                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    TickPresentationData.Empty));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_Present_PlayerLocomotionSignal_KeepsWalkLoopAcrossCooldownGap()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_PlayerLocomotionSignal_KeepsWalkLoopAcrossCooldownGap");

            try
            {
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.3f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(registry.transform, attachPlayerAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateSurfaceUnit(10, sourceCell, facing: Direction.Right) }, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        new[]
                        {
                            new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, destinationCell),
                        },
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: true, moveMotionGeneratedThisTick: true),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: true, waitingForNextMoveCadence: true),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_Present_PlayerLocomotionSignal_FalseFallsBackToIdleOnlyAfterMoveTrackEnds()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_PlayerLocomotionSignal_FalseFallsBackToIdleOnlyAfterMoveTrackEnds");

            try
            {
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 1f,
                    pushMotionDurationSeconds: 0.3f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(registry.transform, attachPlayerAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateSurfaceUnit(10, sourceCell, facing: Direction.Right) }, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        new[]
                        {
                            new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, destinationCell),
                        },
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: true, moveMotionGeneratedThisTick: true),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0.5f);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: false),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));

                presenter.UpdatePresentation(0.5f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_Present_PlayerActionSignals_HoldPushAndFlipUntilPresentationDurationExpires()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_PlayerActionSignals_HoldPushAndFlipUntilPresentationDurationExpires");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(registry.transform, attachPlayerAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(new[] { CreateSurfaceUnit(10, sourceCell, facing: Direction.Right) }, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                var authoring = playerView.GetComponent<PlayerAnimationTimingAuthoring>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(authoring, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.3f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipRecoveryAnimatorDurationSeconds", 0.3f);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.Push, 1, startedThisTick: true, completedThisTick: false, canceledThisTick: false),
                        },
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: true),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));
                Assert.That(driver.ActionExecuteSignalCount, Is.Zero);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.Push, 1, startedThisTick: false, completedThisTick: false, canceledThisTick: false, executedThisTick: true),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));
                Assert.That(driver.ActionExecuteSignalCount, Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.None, 0, startedThisTick: false, completedThisTick: true, canceledThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));
                Assert.That(driver.ActionExecuteSignalCount, Is.EqualTo(1));

                presenter.UpdatePresentation(0.49f);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));

                presenter.UpdatePresentation(0.01f);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.Flip, 2, startedThisTick: true, completedThisTick: false, canceledThisTick: false),
                        },
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: true),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(2));
                Assert.That(driver.ActionExecuteSignalCount, Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.None, 0, startedThisTick: false, completedThisTick: true, canceledThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));

                presenter.UpdatePresentation(0.5f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_PlayerActionHold_YieldsImmediatelyToNewWalkPresentation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerActionHold_YieldsImmediatelyToNewWalkPresentation");

            try
            {
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 1f,
                    pushMotionDurationSeconds: 0.3f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(registry.transform, attachPlayerAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateSurfaceUnit(10, sourceCell, facing: Direction.Right) }, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                var authoring = playerView.GetComponent<PlayerAnimationTimingAuthoring>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(authoring, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipRecoveryAnimatorDurationSeconds", 0.3f);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.Flip, 1, startedThisTick: true, completedThisTick: false, canceledThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.None, 0, startedThisTick: false, completedThisTick: true, canceledThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        new[]
                        {
                            new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, destinationCell),
                        },
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: true, moveMotionGeneratedThisTick: true),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));

                presenter.UpdatePresentation(0.5f);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            CreatePlayerLocomotionSignal(10, shouldPlayWalkLoop: false),
                        },
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));

                presenter.UpdatePresentation(0.5f);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PlayerAnimatorDriver_TransitionsBetweenWindupAndRecoveryPhases()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_TransitionsBetweenWindupAndRecoveryPhases");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                var animator = rootObject.GetComponent<Animator>();
                if (animator != null)
                {
                    UnityEngine.Object.DestroyImmediate(animator);
                }

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.25f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.5f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.5f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipRecoveryAnimatorDurationSeconds", 0.75f);

                driver.Apply(new PlayerViewPresentationState(10, 1, PlayerActionKind.Push, 1, startedThisTick: true, executedThisTick: false, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);

                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));
                Assert.That(driver.ActionExecuteSignalCount, Is.Zero);
                Assert.That(driver.PushPresentationDurationSeconds, Is.EqualTo(0.75f));
                Assert.That(driver.GetPresentationDurationSeconds(PlayerPresentationPhase.PushWindup), Is.EqualTo(0.25f));
                Assert.That(driver.GetPresentationDurationSeconds(PlayerPresentationPhase.PushRecovery), Is.EqualTo(0.5f));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushWindup));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Windup"));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(4f).Within(0.0001f));

                driver.Apply(new PlayerViewPresentationState(10, 2, PlayerActionKind.Push, 1, startedThisTick: false, executedThisTick: true, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);

                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));
                Assert.That(driver.ActionExecuteSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushRecovery));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Recovery"));
                Assert.That(driver.LastCrossFadedStateName, Is.Not.EqualTo("Push_Windup"));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));

                driver.Apply(new PlayerViewPresentationState(10, 3, PlayerActionKind.Flip, 2, startedThisTick: true, executedThisTick: false, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip);

                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(2));
                Assert.That(driver.FlipPresentationDurationSeconds, Is.EqualTo(1.25f));
                Assert.That(driver.GetPresentationDurationSeconds(PlayerPresentationPhase.FlipWindup), Is.EqualTo(0.5f));
                Assert.That(driver.GetPresentationDurationSeconds(PlayerPresentationPhase.FlipRecovery), Is.EqualTo(0.75f));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipWindup));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Flip_Windup"));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));

                driver.Apply(new PlayerViewPresentationState(10, 4, PlayerActionKind.Flip, 2, startedThisTick: false, executedThisTick: true, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip);

                Assert.That(driver.ActionExecuteSignalCount, Is.EqualTo(2));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipRecovery));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Flip_Recovery"));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1.3333334f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PlayerAnimatorDriver_InspectorSurface_IsLimitedToCoreAuthoringFields()
        {
            var serializedFieldNames = typeof(PlayerAnimatorDriver)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                .Select(field => field.Name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "animator",
                    "idleStateName",
                    "walkStateName",
                    "pushWindupStateName",
                    "pushRecoveryStateName",
                    "flipWindupStateName",
                    "flipRecoveryStateName",
                    "deathStateName",
                    "stageClearVictoryStateName",
                    "hitTriggerName",
                    "walkExitStateName",
                    "stateTransitionCrossFadeDurationSeconds",
                    "animationTimingAuthoring",
                },
                serializedFieldNames);
            Assert.That(serializedFieldNames, Does.Not.Contain("stateParameterName"));
            Assert.That(serializedFieldNames, Does.Not.Contain("pushExecuteTriggerName"));
            Assert.That(serializedFieldNames, Does.Not.Contain("flipExecuteTriggerName"));
            Assert.That(serializedFieldNames, Does.Not.Contain("crossFadeDurationSeconds"));
        }

        [Test]
        [Category("Full")]
        public void EnemyAnimatorDriver_InspectorSurface_IsLimitedToCoreAuthoringFields()
        {
            var serializedFieldNames = typeof(EnemyAnimatorDriver)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                .Select(field => field.Name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "animator",
                    "animationTimingAuthoring",
                    "windupStateName",
                    "jumpWindupStateName",
                    "jumpAirborneStateName",
                    "chargeActiveStateName",
                    "recoveryStateName",
                    "glideWindupStateName",
                    "glideActiveStateName",
                    "glideRecoveryStateName",
                    "windupTriggerName",
                    "jumpWindupTriggerName",
                    "jumpAirborneTriggerName",
                    "attackTriggerName",
                    "recoveryTriggerName",
                    "hitTriggerName",
                    "deathTriggerName",
                },
                serializedFieldNames);
            Assert.That(serializedFieldNames, Does.Not.Contain("aiModeParameterName"));
            Assert.That(serializedFieldNames, Does.Not.Contain("activeActionKindParameterName"));
            Assert.That(serializedFieldNames, Does.Not.Contain("movingParameterName"));
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyMotion_MaintainsBoardRootIdentity()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_MaintainsBoardRootIdentity");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var cubeCenter = new Vector3(2f, 1f, -3f);
                var sourceReferenceRotation = ResolveRestTopologyReferenceRotation(initialTopology);
                var destinationReferenceRotation = ResolveRestTopologyReferenceRotation(
                    rotatedTopology,
                    TopologyRotationVisualMapping.ForwardUsesNegativeX);
                rootObject.transform.position = cubeCenter;

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, sourceReferenceRotation), Is.GreaterThan(0.1f));
                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, destinationReferenceRotation), Is.GreaterThan(0.1f));
                Assert.That(Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.Progress01, Is.GreaterThanOrEqualTo(0.499f));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.Progress01, Is.LessThan(1f));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.SourceTopology, Is.EqualTo(initialTopology));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.DestinationTopology, Is.EqualTo(rotatedTopology));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
                Assert.That(
                    presenter.CurrentTopologyTransitionVisualState.DurationSeconds,
                    Is.EqualTo(timingProfile.TopologyMotionDurationSeconds).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        presenter.CurrentTopologyTransitionVisualState.PresentedVisualRotation,
                        presenter.PresentedBoardRotation),
                    Is.LessThan(0.001f));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.AngularVelocityNormalized, Is.GreaterThan(0f));
                Assert.That(boardRoot.CameraTargetRoot, Is.Not.Null);
                Assert.That(registry.TryGetView(10, out var view), Is.True);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    rotatedTopology,
                    new SurfaceCell(FaceId.Front, 0, 0));
                Assert.That(
                    Vector3.Distance(view.transform.position, boardRoot.transform.TransformPoint(destinationPosition)),
                    Is.LessThan(1f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.SourceTopology, Is.EqualTo(rotatedTopology));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.DestinationTopology, Is.EqualTo(rotatedTopology));
                Assert.That(
                    Vector3.Distance(view.transform.position, boardRoot.transform.TransformPoint(destinationPosition)),
                    Is.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyMotion_FrontToCeiling_UsesShortestArcAcrossCeilingBoundary()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_FrontToCeiling_UsesShortestArcAcrossCeilingBoundary");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var tweenSettings = new TopologyRotationTweenSettings
                {
                    Ease = TopologyRotationTweenEase.Linear,
                };
                var initialTopology = new CubeTopologyState(FaceId.Front);
                var rotatedTopology = new CubeTopologyState(FaceId.Ceiling);
                var expectedMidRotation = Quaternion.Euler(-135f, 0f, 0f);
                var wrongMidRotation = Quaternion.Euler(45f, 0f, 0f);
                var destinationReferenceRotation = ResolveRestTopologyReferenceRotation(
                    rotatedTopology,
                    TopologyRotationVisualMapping.ForwardUsesNegativeX);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot,
                    topologyRotationVisualMapping: TopologyRotationVisualMapping.ForwardUsesNegativeX,
                    topologyRotationTweenSettings: tweenSettings);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Ceiling, 0, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, expectedMidRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, wrongMidRotation),
                    Is.GreaterThan(90f));
                Assert.That(
                    presenter.CurrentTopologyTransitionVisualState.Progress01,
                    Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);
                presenter.UpdatePresentation(0.05f);

                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, destinationReferenceRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyMotion_CeilingToFront_UsesShortestArcAcrossCeilingBoundary()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_CeilingToFront_UsesShortestArcAcrossCeilingBoundary");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var tweenSettings = new TopologyRotationTweenSettings
                {
                    Ease = TopologyRotationTweenEase.Linear,
                };
                var initialTopology = new CubeTopologyState(FaceId.Ceiling);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var expectedMidRotation = Quaternion.Euler(-135f, 0f, 0f);
                var wrongMidRotation = Quaternion.Euler(45f, 0f, 0f);
                var destinationReferenceRotation = ResolveRestTopologyReferenceRotation(
                    rotatedTopology,
                    TopologyRotationVisualMapping.ForwardUsesNegativeX);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot,
                    topologyRotationVisualMapping: TopologyRotationVisualMapping.ForwardUsesNegativeX,
                    topologyRotationTweenSettings: tweenSettings);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Ceiling, 0, 0)),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Backward),
                            Array.Empty<TickVisibilityChange>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, expectedMidRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, wrongMidRotation),
                    Is.GreaterThan(90f));
                Assert.That(
                    presenter.CurrentTopologyTransitionVisualState.Progress01,
                    Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);
                presenter.UpdatePresentation(0.05f);

                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, destinationReferenceRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyMotion_UsesDedicatedDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_UsesDedicatedDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.4f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var sourceReferenceRotation = ResolveRestTopologyReferenceRotation(initialTopology);
                var destinationReferenceRotation = ResolveRestTopologyReferenceRotation(rotatedTopology);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));
                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, sourceReferenceRotation), Is.GreaterThan(0.1f));
                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, destinationReferenceRotation), Is.GreaterThan(0.1f));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
                Assert.That(
                    presenter.CurrentTopologyTransitionVisualState.DurationSeconds,
                    Is.EqualTo(timingProfile.TopologyMotionDurationSeconds).Within(0.001f));

                presenter.UpdatePresentation(
                    timingProfile.TopologyMotionDurationSeconds - timingProfile.PushMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, destinationReferenceRotation),
                    Is.LessThan(0.001f));
                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyMotion_BackToCeiling_PreservesContinuousOrbitXDegrees()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_BackToCeiling_PreservesContinuousOrbitXDegrees");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var rig = rootObject.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.Initialize(null, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var tweenSettings = new TopologyRotationTweenSettings
                {
                    Ease = TopologyRotationTweenEase.Linear,
                };
                var initialTopology = new CubeTopologyState(FaceId.Back);
                var rotatedTopology = new CubeTopologyState(FaceId.Ceiling);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot,
                    topologyRotationVisualMapping: TopologyRotationVisualMapping.ForwardUsesPositiveX,
                    topologyRotationTweenSettings: tweenSettings);
                presenter.AttachCameraRig(rig);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Back, 0, 0)),
                    },
                    initialTopology);

                Assert.That(rig.PresentedTopologyOrbitXDegrees, Is.EqualTo(90f).Within(0.001f));

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Ceiling, 0, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Backward),
                            Array.Empty<TickVisibilityChange>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(rig.PresentedTopologyOrbitXDegrees, Is.EqualTo(135f).Within(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(rig.PresentedTopologyOrbitXDegrees, Is.EqualTo(180f).Within(0.001f));

                presenter.UpdatePresentation(0.05f);

                Assert.That(rig.PresentedTopologyOrbitXDegrees, Is.EqualTo(180f).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(rig.PresentedTopologyOrbit, Quaternion.Euler(180f, 0f, 0f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyMotion_WithCinemachineBrain_SyncsOutputCameraSameCall()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_WithCinemachineBrain_SyncsOutputCameraSameCall");
            var outputCameraObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_WithCinemachineBrain_OutputCamera");
            var cinemachineCameraObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_WithCinemachineBrain_CinemachineCamera");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));

                var rig = rootObject.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.Initialize(null, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                var outputCamera = outputCameraObject.AddComponent<Camera>();
                var brain = outputCameraObject.AddComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;

                var cinemachineCamera = cinemachineCameraObject.AddComponent<CinemachineCamera>();
                cinemachineCameraObject.transform.SetParent(boardRoot.CameraEffectsRoot, worldPositionStays: false);
                cinemachineCameraObject.transform.localPosition = Vector3.zero;
                cinemachineCameraObject.transform.localRotation = Quaternion.identity;
                cinemachineCamera.Target = new CameraTarget
                {
                    TrackingTarget = boardRoot.CameraTargetRoot,
                    LookAtTarget = boardRoot.CameraTargetRoot,
                    CustomLookAtTarget = true,
                };

                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var tweenSettings = new TopologyRotationTweenSettings
                {
                    Ease = TopologyRotationTweenEase.Linear,
                };
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot,
                    topologyRotationVisualMapping: TopologyRotationVisualMapping.ForwardUsesPositiveX,
                    topologyRotationTweenSettings: tweenSettings);
                presenter.AttachCameraRuntime(rig, brain);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    },
                    initialTopology);

                presenter.UpdatePresentation(0f);

                Assert.That(brain.UpdateMethod, Is.EqualTo(CinemachineBrain.UpdateMethods.ManualUpdate));
                Assert.That(
                    Vector3.Distance(outputCamera.transform.position, cinemachineCamera.transform.position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(outputCamera.transform.rotation, cinemachineCamera.transform.rotation),
                    Is.LessThan(0.001f));

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(
                    Vector3.Distance(outputCamera.transform.position, cinemachineCamera.transform.position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(outputCamera.transform.rotation, cinemachineCamera.transform.rotation),
                    Is.LessThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(
                    Quaternion.Angle(
                        presenter.PresentedBoardRotation,
                        ResolveRestTopologyReferenceRotation(rotatedTopology)),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(outputCamera.transform.position, cinemachineCamera.transform.position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(outputCamera.transform.rotation, cinemachineCamera.transform.rotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(outputCameraObject);
                UnityEngine.Object.DestroyImmediate(cinemachineCameraObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyTransition_CameraShake_OnlyCameraEffectsRootMovesAndResets()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyTransition_CameraShake_OnlyCameraEffectsRootMovesAndResets");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var rig = rootObject.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.ConfigureTopologyTransitionCameraShake(TopologyTransitionCameraShakeProfile.CreateDefault());
                rig.Initialize(null, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot,
                    topologyRotationVisualMapping: TopologyRotationVisualMapping.ForwardUsesPositiveX,
                    topologyRotationTweenSettings: new TopologyRotationTweenSettings
                    {
                        Ease = TopologyRotationTweenEase.Linear,
                    });
                presenter.AttachCameraRig(rig);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    },
                    initialTopology);

                Assert.That(boardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(boardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.12f);

                Assert.That(Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.CameraOrbitPivot.localRotation, rig.PresentedTopologyOrbit),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(boardRoot.CameraEffectsRoot.localPosition, rig.TopologyTransitionShakeLocalPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.CameraEffectsRoot.localRotation, rig.TopologyTransitionShakeLocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    boardRoot.CameraEffectsRoot.localPosition.sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(boardRoot.CameraEffectsRoot.localRotation, Quaternion.identity) > 0.001f,
                    Is.True);

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.88f);

                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
                Assert.That(Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.CameraOrbitPivot.localRotation, rig.PresentedTopologyOrbit),
                    Is.LessThan(0.001f));
                Assert.That(boardRoot.CameraEffectsRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(boardRoot.CameraEffectsRoot.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_InitialFrontTopology_UsesPositiveXRestPoseAndCameraOrbit()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_InitialFrontTopology_UsesPositiveXRestPoseAndCameraOrbit");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var initialTopology = new CubeTopologyState(FaceId.Front);
                var rig = rootObject.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.Initialize(null, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    boardRoot);
                presenter.AttachCameraRig(rig);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    },
                    initialTopology);

                var expectedRestRotation = ResolveRestTopologyReferenceRotation(initialTopology);

                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, expectedRestRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(rig.PresentedTopologyOrbit, Quaternion.Inverse(expectedRestRotation)),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_InitialFrontTopology_ConfiguredNegativeXMappingOverridesDefault()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_InitialFrontTopology_ConfiguredNegativeXMappingOverridesDefault");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var initialTopology = new CubeTopologyState(FaceId.Front);
                var rig = rootObject.AddComponent<GameplayCameraRig>();
                rig.ApplySettings(GameplayCameraSettings.CreateRuntimeDefault());
                rig.Initialize(null, boardRoot.CameraTargetRoot, new Bounds(Vector3.zero, Vector3.one));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    boardRoot,
                    topologyRotationVisualMapping: TopologyRotationVisualMapping.ForwardUsesNegativeX);
                presenter.AttachCameraRig(rig);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    },
                    initialTopology);

                var expectedRestRotation = ResolveRestTopologyReferenceRotation(
                    initialTopology,
                    TopologyRotationVisualMapping.ForwardUsesNegativeX);

                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, expectedRestRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(rig.PresentedTopologyOrbit, Quaternion.Inverse(expectedRestRotation)),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyTransition_HidesSourceOnlyEntityAtStart()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyTransition_HidesSourceOnlyEntityAtStart");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.4f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var retainedCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var shownCell = new SurfaceCell(FaceId.Ceiling, 0, 1);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(20, retainedCell, Direction.Left),
                        CreateSurfaceBox(30, shownCell, Direction.Right),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, retainedCell, Direction.Left),
                            CreateSurfaceBox(30, shownCell, Direction.Right),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            new[]
                            {
                                new TickTransitionVisibilityChange(
                                    30,
                                    TickTransitionVisibilityMode.ShowAtTransitionStart,
                                    shownCell,
                                    rotatedTopology,
                                    Direction.Right),
                            })));
                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var retainedView), Is.True);
                Assert.That(retainedView.gameObject.activeSelf, Is.False);
                Assert.That(registry.TryGetView(30, out var shownView), Is.True);
                Assert.That(shownView.gameObject.activeSelf, Is.True);
                Assert.That(Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(retainedView.gameObject.activeSelf, Is.False);
                Assert.That(shownView.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_TopologyTransition_ResolvesMotionStartPoseInTransitionSpace()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyTransition_ResolvesMotionStartPoseInTransitionSpace");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
                var destinationCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell),
                    },
                    initialTopology);
                Assert.That(registry.TryGetView(10, out var actorView), Is.True);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, destinationCell),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    10,
                                    TickEntityMotionKind.Move,
                                    sourceCell,
                                    destinationCell,
                                    initialTopology,
                                    rotatedTopology,
                                    Direction.Up,
                                    Direction.Up),
                            },
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));
                presenter.UpdatePresentation(0f);

                var expectedTransitionLocalPosition = GetPresenterTransitionLocalPosition(
                    boardBounds,
                    initialTopology,
                    rotatedTopology,
                    sourceCell);
                var expectedTransitionLocalRotation = GetPresenterTransitionLocalRotation(
                    boardBounds,
                    initialTopology,
                    rotatedTopology,
                    sourceCell,
                    Direction.Up);

                Assert.That(Vector3.Distance(actorView.transform.localPosition, expectedTransitionLocalPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(actorView.transform.localRotation, expectedTransitionLocalRotation), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(boardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_CameraTarget_StaysOnCubeCenterDuringTopologyTransition()
        {
            var hostObject = new GameObject("GameplaySceneHost_CameraTarget_StaysOnCubeCenterDuringTopologyTransition");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_CameraTarget_StaysOnCubeCenter_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                var initialTarget = host.ViewCameraTarget.position;
                var expectedCenter = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    1f).GetCubeCenter();

                host.InputHost.SetRawMoveInput(Vector2.up);
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(initialTarget, Is.EqualTo(expectedCenter));
                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(expectedCenter));
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveBottom_Floor_0_0").gameObject.activeSelf, Is.False);
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveFront_Front_0_0"), Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveFront_Front_0_0").gameObject.activeSelf, Is.False);
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Null);
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveFront_Ceiling_0_0"), Is.Null);
                Assert.That(host.BoardSurfaceRenderer.TransitionTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Null);
                Assert.That(host.BoardSurfaceRenderer.TransitionTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.TransitionTilePoolRoot.Find("ActiveBottom_Front_0_0").gameObject.activeSelf, Is.True);
                Assert.That(host.BoardSurfaceRenderer.TransitionTilePoolRoot.Find("ActiveFront_Ceiling_0_0"), Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.TransitionTilePoolRoot.Find("ActiveFront_Ceiling_0_0").gameObject.activeSelf, Is.True);
                Assert.That(host.BoardSurfaceRenderer.IsTopologyTransitionActive, Is.True);
                Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(GetViewPosition(host, 10).z, Is.GreaterThan(0f));

                host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(Vector3.Distance(host.ViewCameraTarget.position, expectedCenter), Is.LessThan(1f));
                Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveBottom_Front_0_0").gameObject.activeSelf, Is.True);
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveFront_Ceiling_0_0"), Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.VisibleTilePoolRoot.Find("ActiveFront_Ceiling_0_0").gameObject.activeSelf, Is.True);
                Assert.That(host.BoardSurfaceRenderer.TransitionTileCount, Is.Zero);
                Assert.That(GetViewPosition(host, 10).z, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_TopologyTransition_CameraOrbitPreservesScreenContinuityAtStart()
        {
            var hostObject = new GameObject("GameplaySceneHost_TopologyTransition_CameraOrbitPreservesScreenContinuityAtStart");
            var cameraObject = new GameObject("ViewCamera");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;

                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_TopologyTransition_CameraOrbit_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        SnapViewCameraToTarget = true,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        ViewCamera = viewCamera,
                    });

                var initialViewport = viewCamera.WorldToViewportPoint(GetViewPosition(host, 10));

                host.InputHost.SetRawMoveInput(Vector2.up);
                host.InputHost.RunSingleTick();

                var expectedTransitionWorldPosition = GetTransitionProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    new SurfaceCell(FaceId.Front, 0, 0));
                var sourceReferenceRotation = ResolveRestTopologyReferenceRotation(new CubeTopologyState(FaceId.Floor));
                var destinationReferenceRotation = ResolveRestTopologyReferenceRotation(new CubeTopologyState(FaceId.Front));
                var transitionWorldPosition = GetViewPosition(host, 10);
                var transitionViewport = viewCamera.WorldToViewportPoint(transitionWorldPosition);
                var rig = host.GetComponent<GameplayCameraRig>();

                Assert.That(rig, Is.Not.Null);
                Assert.That(transitionViewport.z, Is.GreaterThan(0f));
                Assert.That(Vector3.Distance(transitionWorldPosition, expectedTransitionWorldPosition), Is.LessThan(1f));
                Assert.That(
                    Vector2.Distance(
                        new Vector2(initialViewport.x, initialViewport.y),
                        new Vector2(transitionViewport.x, transitionViewport.y)),
                    Is.LessThan(0.005f));
                Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(host.Presenter.PresentedBoardRotation, sourceReferenceRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(rig.PresentedTopologyOrbit, Quaternion.Inverse(host.Presenter.PresentedBoardRotation)),
                    Is.LessThan(0.001f));

                host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);

                var midTransitionWorldPosition = GetViewPosition(host, 10);
                var midTransitionViewport = viewCamera.WorldToViewportPoint(midTransitionWorldPosition);

                Assert.That(Vector3.Distance(midTransitionWorldPosition, expectedTransitionWorldPosition), Is.LessThan(1f));
                Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(host.Presenter.PresentedBoardRotation, sourceReferenceRotation), Is.GreaterThan(0.1f));
                Assert.That(Quaternion.Angle(host.Presenter.PresentedBoardRotation, destinationReferenceRotation), Is.GreaterThan(0.1f));
                Assert.That(
                    Vector2.Distance(
                        new Vector2(transitionViewport.x, transitionViewport.y),
                        new Vector2(midTransitionViewport.x, midTransitionViewport.y)),
                    Is.GreaterThan(0.001f));

                host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(
                    Quaternion.Angle(host.Presenter.PresentedBoardRotation, destinationReferenceRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(rig.PresentedTopologyOrbit, Quaternion.Inverse(destinationReferenceRotation)),
                    Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(GetViewPosition(host, 10), expectedTransitionWorldPosition), Is.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_UsesPerspectiveCameraRigAndTracksCubeCenter()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_UsesPerspectiveCameraRigAndTracksCubeCenter");
            hostObject.transform.position = new Vector3(1.5f, -0.5f, 2f);
            var cameraObject = new GameObject("ViewCamera");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.orthographic = true;

                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_Initialize_UsesPerspectiveCameraRig_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        SnapViewCameraToTarget = true,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        ViewCamera = viewCamera,
                    });

                var expectedCenter = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    1f).GetCubeCenter();
                expectedCenter = host.BoardRoot.transform.TransformPoint(expectedCenter);

                Assert.That(host.GetComponent<GameplayCameraRig>(), Is.Not.Null);
                Assert.That(viewCamera.orthographic, Is.False);
                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(expectedCenter));
                Assert.That(
                    Vector3.Angle(viewCamera.transform.forward, (expectedCenter - viewCamera.transform.position).normalized),
                    Is.LessThan(0.1f));
                Assert.That(viewCamera.transform.position.y, Is.GreaterThan(expectedCenter.y));
                Assert.That(viewCamera.transform.position.z, Is.LessThan(expectedCenter.z));
                Assert.That(viewCamera.transform.forward.y, Is.LessThan(-0.25f));
                Assert.That(viewCamera.transform.forward.z, Is.GreaterThan(0.9f));
                Assert.That(Mathf.Abs(viewCamera.transform.forward.x), Is.LessThan(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_AppliesConfiguredCameraSettingsToRigAndCamera()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_AppliesConfiguredCameraSettingsToRigAndCamera");
            var cameraObject = new GameObject("ViewCamera");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                var expectedCameraSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 18f,
                    YawDegrees = 31f,
                    DistanceMode = CameraDistanceMode.Manual,
                    ManualDistance = 9f,
                    FramingPadding = 1.35f,
                    PerspectiveFieldOfView = 47f,
                    NearClipPlane = 0.15f,
                    FarClipPlane = 77f,
                    ClearFlags = CameraClearFlags.SolidColor,
                    BackgroundColor = new Color(0.1f, 0.2f, 0.3f),
                };

                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_Initialize_AppliesConfiguredCameraSettings_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CameraSettings = expectedCameraSettings,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        SnapViewCameraToTarget = true,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        ViewCamera = viewCamera,
                    });

                var rig = host.GetComponent<GameplayCameraRig>();
                Assert.That(rig, Is.Not.Null);
                Assert.That(rig.PitchDegrees, Is.EqualTo(expectedCameraSettings.PitchDegrees).Within(0.0001f));
                Assert.That(rig.YawDegrees, Is.EqualTo(expectedCameraSettings.YawDegrees).Within(0.0001f));
                Assert.That(rig.CurrentDistanceMode, Is.EqualTo(expectedCameraSettings.DistanceMode));
                Assert.That(rig.ManualDistance, Is.EqualTo(expectedCameraSettings.ManualDistance).Within(0.0001f));
                Assert.That(rig.FramingPadding, Is.EqualTo(expectedCameraSettings.FramingPadding).Within(0.0001f));
                Assert.That(rig.PerspectiveFieldOfView, Is.EqualTo(expectedCameraSettings.PerspectiveFieldOfView).Within(0.0001f));
                Assert.That(rig.NearClipPlane, Is.EqualTo(expectedCameraSettings.NearClipPlane).Within(0.0001f));
                Assert.That(rig.FarClipPlane, Is.EqualTo(expectedCameraSettings.FarClipPlane).Within(0.0001f));
                Assert.That(rig.ClearFlags, Is.EqualTo(expectedCameraSettings.ClearFlags));
                Assert.That(rig.BackgroundColor, Is.EqualTo(expectedCameraSettings.BackgroundColor));
                Assert.That(viewCamera.fieldOfView, Is.EqualTo(expectedCameraSettings.PerspectiveFieldOfView).Within(0.0001f));
                Assert.That(viewCamera.clearFlags, Is.EqualTo(expectedCameraSettings.ClearFlags));
                Assert.That(viewCamera.backgroundColor, Is.EqualTo(expectedCameraSettings.BackgroundColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_WithCinemachineBrain_AppliesResolvedStartupLensToSceneCinemachineCamera()
        {
            var hostObject =
                new GameObject("GameplaySceneHost_Initialize_WithCinemachineBrain_AppliesResolvedStartupLensToSceneCinemachineCamera");
            var outputCameraObject = new GameObject("Main Camera");
            var cinemachineCameraObject = new GameObject("StartupLensCinemachineCamera");

            try
            {
                outputCameraObject.tag = "MainCamera";
                var outputCamera = outputCameraObject.AddComponent<Camera>();
                var brain = outputCameraObject.AddComponent<CinemachineBrain>();
                var cinemachineCamera = cinemachineCameraObject.AddComponent<CinemachineCamera>();
                var authoredLens = cinemachineCamera.Lens;
                authoredLens.FieldOfView = 44f;
                authoredLens.NearClipPlane = 0.2f;
                authoredLens.FarClipPlane = 88f;
                cinemachineCamera.Lens = authoredLens;
                var authoredWorldPosition = new Vector3(3f, 4f, -7f);
                var authoredWorldRotation =
                    Quaternion.LookRotation((-authoredWorldPosition).normalized, Vector3.up);
                cinemachineCamera.transform.SetPositionAndRotation(authoredWorldPosition, authoredWorldRotation);

                var rig = hostObject.AddComponent<GameplayCameraRig>();
                rig.CaptureAuthoredSceneCameraPose(
                    cinemachineCamera.transform,
                    authoredLens.FieldOfView,
                    authoredLens.NearClipPlane,
                    authoredLens.FarClipPlane);

                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                    "GameplaySceneHost_Initialize_WithCinemachineBrain_AppliesResolvedStartupLens_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                var expectedCameraSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 10f,
                    YawDegrees = 15f,
                    DistanceMode = CameraDistanceMode.AutoFit,
                    ManualDistance = 2f,
                    FramingPadding = 1.2f,
                    PerspectiveFieldOfView = 60f,
                    NearClipPlane = 0.03f,
                    FarClipPlane = 100f,
                };

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CameraBaselineAuthoringPolicy = new GameplayCameraBaselineAuthoringPolicy
                        {
                            UseAuthoredSceneCameraPose = true,
                            UseAuthoredSceneCameraLens = false,
                        },
                        CameraSettings = expectedCameraSettings,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        SnapViewCameraToTarget = false,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        TopologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX,
                    });

                Assert.That(rig.PerspectiveFieldOfView, Is.EqualTo(expectedCameraSettings.PerspectiveFieldOfView).Within(0.0001f));
                Assert.That(rig.NearClipPlane, Is.EqualTo(expectedCameraSettings.NearClipPlane).Within(0.0001f));
                Assert.That(rig.FarClipPlane, Is.EqualTo(expectedCameraSettings.FarClipPlane).Within(0.0001f));
                Assert.That(cinemachineCamera.Lens.FieldOfView, Is.EqualTo(expectedCameraSettings.PerspectiveFieldOfView).Within(0.0001f));
                Assert.That(cinemachineCamera.Lens.NearClipPlane, Is.EqualTo(expectedCameraSettings.NearClipPlane).Within(0.0001f));
                Assert.That(cinemachineCamera.Lens.FarClipPlane, Is.EqualTo(expectedCameraSettings.FarClipPlane).Within(0.0001f));
                Assert.That(outputCamera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);
                Assert.That(brain.UpdateMethod, Is.EqualTo(CinemachineBrain.UpdateMethods.ManualUpdate));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(outputCameraObject);
                UnityEngine.Object.DestroyImmediate(cinemachineCameraObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_UsesCapturedAuthoredCameraPoseAsBaseline()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_UsesCapturedAuthoredCameraPoseAsBaseline");
            var cameraObject = new GameObject("ViewCamera");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                var authoredWorldPosition = new Vector3(2.5f, 3f, -6f);
                viewCamera.transform.SetPositionAndRotation(
                    authoredWorldPosition,
                    Quaternion.LookRotation(-authoredWorldPosition.normalized, Vector3.up));

                var rig = hostObject.AddComponent<GameplayCameraRig>();
                rig.CaptureAuthoredSceneCameraPose(
                    viewCamera.transform,
                    fieldOfView: 42f,
                    nearClipPlane: 0.2f,
                    farClipPlane: 88f);

                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_Initialize_UsesCapturedAuthoredCameraPose_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CameraBaselineAuthoringPolicy = GameplayCameraBaselineAuthoringPolicy.CreateShowcaseDefault(),
                        CameraSettings = new GameplayCameraSettings
                        {
                            PitchDegrees = 5f,
                            YawDegrees = 17f,
                            DistanceMode = CameraDistanceMode.AutoFit,
                            ManualDistance = 1f,
                            FramingPadding = 1.2f,
                            PerspectiveFieldOfView = 60f,
                            NearClipPlane = 0.03f,
                            FarClipPlane = 100f,
                        },
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        SnapViewCameraToTarget = true,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        TopologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX,
                        ViewCamera = viewCamera,
                    });

                Assert.That(rig.PerspectiveFieldOfView, Is.EqualTo(42f).Within(0.0001f));
                Assert.That(rig.NearClipPlane, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(rig.FarClipPlane, Is.EqualTo(88f).Within(0.0001f));
                Assert.That(Vector3.Distance(viewCamera.transform.position, authoredWorldPosition), Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        viewCamera.transform.rotation,
                        Quaternion.LookRotation(-authoredWorldPosition.normalized, Vector3.up)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_MoveMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates()
        {
            var hostObject = new GameObject("GameplaySceneHost_MoveMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_MoveMotion_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        MoveMotionDurationSeconds = 0.1f,
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        PushMotionDurationSeconds = 0.3f,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.MoveMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot, Is.Not.Null);

                var renderedPosition = GetViewPosition(host, 10);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 0, 0));
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 1, 0));
                Assert.That(renderedPosition.x, Is.GreaterThanOrEqualTo(sourcePosition.x));
                Assert.That(renderedPosition.x, Is.LessThan(destinationPosition.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_PushMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates()
        {
            var hostObject = new GameObject("GameplaySceneHost_PushMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_PushMotion_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        PlayerControlTiming = CreateImmediatePlayerControlTimingSettings(),
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot, Is.Not.Null);

                var renderedPosition = GetViewPosition(host, 20);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                Assert.That(renderedPosition.x, Is.GreaterThanOrEqualTo(sourcePosition.x));
                Assert.That(renderedPosition.x, Is.LessThan(destinationPosition.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_PushMotion_KeepsProjectileLayerQueriesOnCommittedDestinationWhileViewInterpolates()
        {
            var hostObject = new GameObject("GameplaySceneHost_PushMotion_KeepsProjectileLayerQueriesOnCommittedDestinationWhileViewInterpolates");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_PushMotionProjectile_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                            CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 2, 0), facing: Direction.Left),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        PlayerControlTiming = CreateImmediatePlayerControlTimingSettings(),
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot, Is.Not.Null);

                var renderedPosition = GetViewPosition(host, 20);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                Assert.That(renderedPosition.x, Is.GreaterThanOrEqualTo(sourcePosition.x));
                Assert.That(renderedPosition.x, Is.LessThan(destinationPosition.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_FlipAction_CommitsLandingCellAndKeepsPresentationActive()
        {
            var hostObject = new GameObject("GameplaySceneHost_FlipAction_CommitsLandingCellAndKeepsPresentationActive");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_FlipMotion_PlayerPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, -1, 0), facing: Direction.Left),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        PlayerControlTiming = CreateImmediatePlayerControlTimingSettings(),
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                host.InputHost.SetRawMoveInput(Vector2.left);
                host.InputHost.BufferFlip();
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot.TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, 1, 0), out var flippedBox), Is.True);
                Assert.That(flippedBox.entityId, Is.EqualTo(20));
                Assert.That(GetUnitIdsAt(snapshot, new SurfaceCell(FaceId.Floor, -1, 0)), Is.Empty);
                Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.True);
                Assert.That(host.Presenter.IsPresentationActive, Is.True);
                Assert.That(host.Presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_FlipAction_CampaignBoxPrefabReceivesFlipInteractionMotion()
        {
            var hostObject = new GameObject("GameplaySceneHost_FlipAction_CampaignBoxPrefabReceivesFlipInteractionMotion");
            var staticCatalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();

            try
            {
                const string boxPresentationId = "test-campaign-box";
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplaySceneHost_FlipInteraction_PlayerPrefab");
                var boxViewPrefab = CreateBoxFlipInteractionViewPrefab("GameplaySceneHost_FlipInteraction_BoxPrefab");
                playerViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                boxViewPrefab.transform.SetParent(hostObject.transform, worldPositionStays: false);
                ConfigureStaticPresentationCatalog(staticCatalog, boxPresentationId, boxViewPrefab);

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, -1, 0), facing: Direction.Left),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerViewPrefab = playerViewPrefab,
                        PlayerControlTiming = CreateImmediatePlayerControlTimingSettings(),
                        StaticEntityPresentationCatalog = staticCatalog,
                        StaticEntityPresentationBindings = new[]
                        {
                            new StaticEntityPresentationBinding
                            {
                                EntityId = 20,
                                PresentationId = boxPresentationId,
                            },
                        },
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                host.InputHost.SetRawMoveInput(Vector2.left);
                host.InputHost.BufferFlip();
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds * 0.5f);
                host.Presenter.UpdatePresentation(0f);

                Assert.That(host.ViewRegistry.TryGetView(20, out var boxView), Is.True);
                Assert.That(boxView.TryGetComponent<BoxFlipInteractionDriver>(out _), Is.True);
                Assert.That(Vector3.Distance(boxView.ModelRoot.localPosition, Vector3.zero), Is.GreaterThan(0.001f));
                Assert.That(Quaternion.Angle(boxView.ModelRoot.localRotation, Quaternion.identity), Is.GreaterThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staticCatalog);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_MoveMotion_MidpointInterpolatesBetweenSourceAndDestination()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_MoveMotion_MidpointInterpolatesBetweenSourceAndDestination");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    10,
                                    TickEntityMotionKind.Move,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 0));
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0));
                Assert.That(view.transform.position.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(destinationPosition.x));
                Assert.That(view.transform.position.y, Is.EqualTo(sourcePosition.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(sourcePosition.z).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_ProjectileMoveMotion_MidpointInterpolatesBetweenSourceAndDestination()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ProjectileMoveMotion_MidpointInterpolatesBetweenSourceAndDestination");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.ProjectileMove,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.ProjectileStepIntervalSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    EntityType.Projectile);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Projectile);
                Assert.That(view.transform.position.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(destinationPosition.x));
                Assert.That(view.transform.position.y, Is.EqualTo(sourcePosition.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(sourcePosition.z).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_DetachVisibility_KeepsTargetVisibleUntilTrackCompletes()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_DetachVisibility_KeepsTargetVisibleUntilTrackCompletes");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Up),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    10,
                                    TickEntityMotionKind.Move,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            },
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(
                                    20,
                                    TickVisibilityChangeKind.Detach,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    topology,
                                    Direction.Up),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var detachedView), Is.True);
                Assert.That(detachedView.gameObject.activeSelf, Is.True);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                Assert.That(detachedView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_RemoveVisibility_KeepsTargetVisibleUntilTrackCompletes()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_RemoveVisibility_KeepsTargetVisibleUntilTrackCompletes");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(
                                    30,
                                    TickVisibilityChangeKind.Remove,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    topology,
                                    Direction.Right),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var removedView), Is.True);
                Assert.That(removedView.gameObject.activeSelf, Is.True);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                Assert.That(removedView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_ItemConsumeSignal_HidesOriginalViewImmediatelyAndUsesDedicatedEffectDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ItemConsumeSignal_HidesOriginalViewImmediatelyAndUsesDedicatedEffectDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: 0.25f);
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var itemCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell),
                        CreateSurfaceBox(20, itemCell, facing: Direction.Up),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, destinationCell),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, destinationCell),
                            },
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, itemCell, topology, Direction.Up),
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Remove, itemCell, topology, Direction.Up),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    20,
                                    TickEntityExitCause.ItemConsume,
                                    itemCell,
                                    topology,
                                    Direction.Up,
                                    EntityType.Box,
                                    sourceActorEntityId: 10),
                            })));

                Assert.That(registry.TryGetView(20, out var itemView), Is.True);
                Assert.That(itemView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);
                Assert.That(itemView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(
                    timingProfile.ItemConsumeEffectDurationSeconds - timingProfile.PushMotionDurationSeconds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_BoxDestroyExitSignal_HidesOriginalViewImmediatelyAndUsesDedicatedEffectDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxDestroyExitSignal_HidesOriginalViewImmediatelyAndUsesDedicatedEffectDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: 0.25f,
                    boxDestroyEffectDurationSeconds: 0.22f);
                var topology = new CubeTopologyState(FaceId.Floor);
                var boxCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(20, boxCell, facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, boxCell, topology, Direction.Right),
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Remove, boxCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    20,
                                    TickEntityExitCause.BoxDestroy,
                                    boxCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    sourceActorEntityId: 10),
                            })));

                Assert.That(registry.TryGetView(20, out var boxView), Is.True);
                Assert.That(boxView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);
                Assert.That(boxView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(
                    timingProfile.BoxDestroyEffectDurationSeconds - timingProfile.PushMotionDurationSeconds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_EnemyDeathExitSignal_HidesOriginalViewImmediately_AndTransientCompletesAfterDedicatedDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_EnemyDeathExitSignal_HidesOriginalViewImmediately_AndTransientCompletesAfterDedicatedDuration");
            var cameraObject = new GameObject("GameplayTickViewPresenter_EnemyDeathExitSignal_OutputCamera");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var outputCamera = cameraObject.AddComponent<Camera>();
                outputCamera.transform.position = new Vector3(2f, 1f, -10f);
                outputCamera.transform.rotation = Quaternion.identity;
                outputCamera.orthographic = true;
                outputCamera.orthographicSize = 2.5f;
                outputCamera.nearClipPlane = 0.1f;
                outputCamera.farClipPlane = 50f;

                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: 0.25f,
                    boxDestroyEffectDurationSeconds: 0.22f,
                    enemyDeathEffectDurationSeconds: 0.28f);
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 1, 1);
                var enemyCell = new SurfaceCell(FaceId.Floor, 3, 1);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 2)),
                    topology,
                    1f,
                    timingProfile);
                presenter.AttachOutputCamera(outputCamera);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, playerCell, facing: Direction.Right),
                        CreateSurfaceUnit(40, enemyCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Left, unitRole: UnitRole.Enemy),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, playerCell, facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    40,
                                    TickEntityExitCause.EnemyDeath,
                                    enemyCell,
                                    topology,
                                    Direction.Left,
                                    EntityType.Unit,
                                    sourceActorEntityId: 10,
                                    presentationSeed: 123456789),
                            })));

                Assert.That(registry.TryGetView(40, out var enemyView), Is.True);
                Assert.That(enemyView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(timingProfile.EnemyDeathEffectDurationSeconds * 0.5f);
                Assert.That(enemyView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(timingProfile.EnemyDeathEffectDurationSeconds * 0.5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_ContactDelayedEnemyDeath_RetainsOriginalViewUntilVisualContactThenCleansUpAtHandoff()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ContactDelayedEnemyDeath_RetainsOriginalViewUntilVisualContactThenCleansUpAtHandoff");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 1f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    enemyDeathEffectDurationSeconds: 0.25f);
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var enemyCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, playerCell, facing: Direction.Right),
                        CreateSurfaceUnit(40, enemyCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
                    },
                    topology);

                Assert.That(registry.TryGetView(40, out var enemyView), Is.True);
                var originalInstanceId = enemyView.GetInstanceID();
                var driver = enemyView.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.name = "OriginalEnemyVisual";
                visual.transform.SetParent(enemyView.ModelRoot, worldPositionStays: false);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, playerCell, facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(40, TickVisibilityChangeKind.Remove, enemyCell, topology, Direction.Left),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    40,
                                    TickEntityExitCause.EnemyDeath,
                                    enemyCell,
                                    topology,
                                    Direction.Left,
                                    EntityType.Unit,
                                    sourceActorEntityId: 10,
                                    timing: EntityExitPresentationTiming.AtContactTime,
                                    visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime),
                            })));

                var flipDuration = timingProfile.FlipMotionDurationSeconds;
                var contactTime = GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime *
                                  flipDuration;
                const float epsilon = 0.001f;
                var oneFrame = 1f / timingProfile.SimulationTicksPerSecond;
                var elapsedSeconds = 0f;

                var startSnapshot = presenter.DebugCaptureEntityPresentationLifecycle(40, elapsedSeconds);
                AssertContactRetainedBeforeVisualContact(startSnapshot, originalInstanceId, elapsedSeconds);

                var interactionOnsetDelta =
                    GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime * flipDuration;
                presenter.UpdatePresentation(interactionOnsetDelta);
                elapsedSeconds += interactionOnsetDelta;
                var interactionOnsetSnapshot =
                    presenter.DebugCaptureEntityPresentationLifecycle(40, elapsedSeconds);
                AssertContactRetainedBeforeVisualContact(
                    interactionOnsetSnapshot,
                    originalInstanceId,
                    elapsedSeconds);

                var preSlamDelta =
                    (0.9f - GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime) *
                    flipDuration;
                presenter.UpdatePresentation(preSlamDelta);
                elapsedSeconds += preSlamDelta;
                var preSlamSnapshot = presenter.DebugCaptureEntityPresentationLifecycle(40, elapsedSeconds);
                AssertContactRetainedBeforeVisualContact(preSlamSnapshot, originalInstanceId, elapsedSeconds);

                var beforeContactDelta = contactTime - (0.9f * flipDuration) - epsilon;
                presenter.UpdatePresentation(beforeContactDelta);
                elapsedSeconds += beforeContactDelta;
                var beforeContactSnapshot = presenter.DebugCaptureEntityPresentationLifecycle(40, elapsedSeconds);
                AssertContactRetainedBeforeVisualContact(beforeContactSnapshot, originalInstanceId, elapsedSeconds);

                presenter.UpdatePresentation(epsilon);
                elapsedSeconds += epsilon;
                var contactSnapshot = presenter.DebugCaptureEntityPresentationLifecycle(40, elapsedSeconds);
                AssertOriginalEnemyCleanedUpAtDestroyVfxHandoff(contactSnapshot, originalInstanceId, elapsedSeconds);

                presenter.UpdatePresentation(oneFrame);
                elapsedSeconds += oneFrame;
                var oneFrameAfterContactSnapshot =
                    presenter.DebugCaptureEntityPresentationLifecycle(40, elapsedSeconds);
                AssertOriginalEnemyCleanedUpAtDestroyVfxHandoff(
                    oneFrameAfterContactSnapshot,
                    originalInstanceId,
                    elapsedSeconds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_PushMotion_MidpointInterpolatesBetweenSourceAndDestination()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PushMotion_MidpointInterpolatesBetweenSourceAndDestination");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 2, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    20,
                                    TickEntityMotionKind.Push,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    new SurfaceCell(FaceId.Floor, 2, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                Assert.That(view.transform.position.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(destinationPosition.x));
                Assert.That(view.transform.position.y, Is.EqualTo(sourcePosition.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(sourcePosition.z).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_BoxSlideMotion_MidpointUsesLinearInterpolation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxSlideMotion_MidpointUsesLinearInterpolation");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 2, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    20,
                                    TickEntityMotionKind.BoxSlide,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    new SurfaceCell(FaceId.Floor, 2, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(
                    boardBounds,
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    boardBounds,
                    topology,
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                var expectedMidpoint = (sourcePosition + destinationPosition) * 0.5f;

                Assert.That(view.transform.position.x, Is.EqualTo(expectedMidpoint.x).Within(0.001f));
                Assert.That(view.transform.position.y, Is.EqualTo(expectedMidpoint.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(expectedMidpoint.z).Within(0.001f));
                Assert.That(presenter.IsPresentationActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_QueuedPushMotions_PreserveSequentialStepsAcrossCatchUp()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_QueuedPushMotions_PreserveSequentialStepsAcrossCatchUp");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    20,
                                    TickEntityMotionKind.Push,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 2, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    20,
                                    TickEntityMotionKind.Push,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    new SurfaceCell(FaceId.Floor, 2, 0)),
                            })));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var firstSourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    EntityType.Box);
                var firstDestinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                Assert.That(view.transform.position.x, Is.GreaterThan(firstSourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(firstDestinationPosition.x));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                Assert.That(view.transform.position, Is.EqualTo(firstDestinationPosition));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                var secondDestinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                Assert.That(view.transform.position.x, Is.GreaterThan(firstDestinationPosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(secondDestinationPosition.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_FlipMotion_MidpointUsesSlamLiftMotion()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_FlipMotion_MidpointUsesSlamLiftMotion");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, -1, 0), facing: Direction.Left),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Flip,
                                    new SurfaceCell(FaceId.Floor, -1, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var boardBounds = new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1));
                var sourceCell = new SurfaceCell(FaceId.Floor, -1, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var expectedPose = BoxFlipSlamSampler.Sample(
                    new GameplayEntityPose(
                        GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Box),
                        GetProjectedEntityRotation(boardBounds, topology, sourceCell, Direction.Left, EntityType.Box)),
                    new GameplayEntityPose(
                        GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Box),
                        GetProjectedEntityRotation(boardBounds, topology, destinationCell, Direction.Right, EntityType.Box)),
                    0.5f,
                    1.4f);

                Assert.That(Vector3.Distance(view.transform.position, expectedPose.Position), Is.LessThanOrEqualTo(0.001f));
                Assert.That(Quaternion.Angle(view.transform.rotation, expectedPose.Rotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_FlipMotion_OnFrontFace_UsesFaceRelativeSlamLiftAndRotation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_FlipMotion_OnFrontFace_UsesFaceRelativeSlamLiftAndRotation");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);
                var boardBounds = new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 2));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(30, new SurfaceCell(FaceId.Front, -1, 0), facing: Direction.Left),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Front, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Flip,
                                    new SurfaceCell(FaceId.Front, -1, 0),
                                    new SurfaceCell(FaceId.Front, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);

                var sourcePosition = GetProjectedEntityPosition(
                    boardBounds,
                    topology,
                    new SurfaceCell(FaceId.Front, -1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    boardBounds,
                    topology,
                    new SurfaceCell(FaceId.Front, 1, 0),
                    EntityType.Box);
                var sourceCell = new SurfaceCell(FaceId.Front, -1, 0);
                var destinationCell = new SurfaceCell(FaceId.Front, 1, 0);
                var expectedPose = BoxFlipSlamSampler.Sample(
                    new GameplayEntityPose(
                        sourcePosition,
                        GetProjectedEntityRotation(boardBounds, topology, sourceCell, Direction.Left, EntityType.Box)),
                    new GameplayEntityPose(
                        destinationPosition,
                        GetProjectedEntityRotation(boardBounds, topology, destinationCell, Direction.Right, EntityType.Box)),
                    0.5f,
                    1.4f);

                Assert.That(Vector3.Distance(view.transform.position, expectedPose.Position), Is.LessThanOrEqualTo(0.001f));
                Assert.That(Quaternion.Angle(view.transform.rotation, expectedPose.Rotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_FlipMotion_CompletesAtLandingCellAndRotation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_FlipMotion_CompletesAtLandingCellAndRotation");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, -1, 0), facing: Direction.Left),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Flip,
                                    new SurfaceCell(FaceId.Floor, -1, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                Assert.That(
                    view.transform.position,
                    Is.EqualTo(GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                        topology,
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        EntityType.Box)));
                Assert.That(
                    Quaternion.Angle(
                        view.transform.rotation,
                        GetProjectedEntityRotation(
                            new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                            topology,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            Direction.Right,
                            EntityType.Box)),
                    Is.LessThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_PushAfterFlip_DoesNotInterpolateRotationWhenFacingChanges()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PushAfterFlip_DoesNotInterpolateRotationWhenFacingChanges");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Down),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Up),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Flip,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 0, 1)),
                            })));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.5f);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 1, 1), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Push,
                                    new SurfaceCell(FaceId.Floor, 0, 1),
                                    new SurfaceCell(FaceId.Floor, 1, 1)),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(2, 2)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(2, 2)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    EntityType.Box);
                Assert.That(view.transform.position.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(destinationPosition.x));
                Assert.That(view.transform.position.y, Is.EqualTo(sourcePosition.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(sourcePosition.z).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        view.transform.rotation,
                        GetProjectedEntityRotation(
                            new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(2, 2)),
                            topology,
                            new SurfaceCell(FaceId.Floor, 1, 1),
                            Direction.Right,
                            EntityType.Box)),
                    Is.LessThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static TickResult CreateTickResult(
            EntityState[] finalEntities,
            CubeTopologyState topology,
            TickPresentationData presentationData = null,
            MovementPhaseResult movementPhaseResult = null,
            AttackPhaseResult attackPhaseResult = null)
        {
            return new TickResult(
                1,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                movementPhaseResult ?? MovementPhaseResult.Empty,
                attackPhaseResult ?? AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                presentationData ?? TickPresentationData.Empty,
                string.Empty,
                TickTrace.Empty);
        }

        private static TickPlayerLocomotionPresentationSignal CreatePlayerLocomotionSignal(
            int entityId,
            bool shouldPlayWalkLoop,
            bool moveMotionGeneratedThisTick = false,
            bool waitingForNextMoveCadence = false,
            Direction inputDirection = Direction.Right,
            bool inputIsBuffered = false)
        {
            return new TickPlayerLocomotionPresentationSignal(
                entityId,
                shouldPlayWalkLoop,
                moveMotionGeneratedThisTick,
                waitingForNextMoveCadence,
                inputDirection,
                inputIsBuffered);
        }

        private static AttackPhaseResult CreateAttackPhaseResult(params ActionGroup[] selectedGroups)
        {
            return CanonicalPhaseResultFactory.CreateAttackPhaseResult(selectedGroups);
        }

        private static EntityState CreateSurfaceUnit(
            int entityId,
            SurfaceCell position,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            bool markedForDeath = false,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Up,
            UnitRole unitRole = UnitRole.None)
        {
            var resolvedUnitRole = unitRole != UnitRole.None
                ? unitRole
                : entityId == 10
                    ? UnitRole.Player
                    : aiMode != EnemyAiMode.None
                        ? UnitRole.Enemy
                        : UnitRole.None;
            var resolvedTeamId = resolvedUnitRole switch
            {
                UnitRole.Player => 1,
                UnitRole.Enemy => 2,
                _ => 0,
            };

            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = resolvedTeamId,
                type = EntityType.Unit,
                unitRole = resolvedUnitRole,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
                aiMode = aiMode,
            };
        }

        private static EntityState CreateSurfaceBox(int entityId, SurfaceCell position, Direction facing)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = facing,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }

        private static EntityState CreateSurfaceProjectile(int entityId, SurfaceCell position, Direction facing)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Projectile,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateSurfaceWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static Vector3 GetProjectedEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            EntityType entityType = EntityType.Unit,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose), Is.True);
            return projectedPose.LocalPosition;
        }

        private static Vector3 GetTransitionProjectedEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            EntityType entityType = EntityType.Unit,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    entityType,
                    out var projectedPose),
                Is.True);
            return projectedPose.LocalPosition;
        }

        private static Quaternion GetProjectedEntityRotation(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            Direction facing,
            EntityType entityType = EntityType.Unit,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(projector.TryResolveEntityRotation(cell, topology, facing, out var rotation), Is.True);
            return rotation;
        }

        private static Quaternion GetTransitionProjectedEntityRotation(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            Direction facing,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(
                projector.TryResolveTransitionEntityRotation(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    facing,
                    out var rotation),
                Is.True);
            return rotation;
        }

        private static Vector3 GetPresenterTransitionLocalPosition(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            EntityType entityType = EntityType.Unit,
            float cellSize = 1f)
        {
            return GetTransitionProjectedEntityPosition(
                boardBounds,
                sourceTopology,
                destinationTopology,
                cell,
                entityType,
                cellSize);
        }

        private static Quaternion GetPresenterTransitionLocalRotation(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            Direction facing,
            float cellSize = 1f)
        {
            return GetTransitionProjectedEntityRotation(
                boardBounds,
                sourceTopology,
                destinationTopology,
                cell,
                facing,
                cellSize);
        }

        private static Quaternion ResolveRestTopologyReferenceRotation(
            CubeTopologyState topology,
            TopologyRotationVisualMapping mapping = TopologyRotationVisualMapping.ForwardUsesPositiveX)
        {
            var forwardDegrees = mapping == TopologyRotationVisualMapping.ForwardUsesPositiveX
                ? 90f
                : -90f;

            return topology.BottomFace switch
            {
                FaceId.Floor => Quaternion.identity,
                FaceId.Front => Quaternion.Euler(forwardDegrees, 0f, 0f),
                FaceId.Ceiling => Quaternion.Euler(180f, 0f, 0f),
                FaceId.Back => Quaternion.Euler(-forwardDegrees, 0f, 0f),
                _ => Quaternion.identity,
            };
        }

        private static GameObject TryFindSurfaceTile(GameplayBoardSurfaceRenderer renderer, string tileName)
        {
            Assert.That(renderer, Is.Not.Null);
            var visibleTile = renderer.VisibleTilePoolRoot != null ? renderer.VisibleTilePoolRoot.Find(tileName) : null;
            var transitionTile = renderer.TransitionTilePoolRoot != null ? renderer.TransitionTilePoolRoot.Find(tileName) : null;
            if (transitionTile != null && transitionTile.gameObject.activeSelf)
            {
                return transitionTile.gameObject;
            }

            if (visibleTile != null && visibleTile.gameObject.activeSelf)
            {
                return visibleTile.gameObject;
            }

            if (transitionTile != null)
            {
                return transitionTile.gameObject;
            }

            return visibleTile != null ? visibleTile.gameObject : null;
        }

        private static GameObject FindSurfaceTile(GameplayBoardSurfaceRenderer renderer, string tileName)
        {
            var tile = TryFindSurfaceTile(renderer, tileName);
            Assert.That(tile, Is.Not.Null, $"Expected board surface tile '{tileName}' to exist.");
            return tile;
        }

        private static GameObject CreateActiveFaceCoverPrefab(string name)
        {
            var coverObject = new GameObject(name);
            CreateActiveFaceCoverVisual(coverObject.transform, "VisualBottom");
            CreateActiveFaceCoverVisual(coverObject.transform, "VisualFront");
            CreateActiveFaceCoverVisual(coverObject.transform, "VisualBack");
            return coverObject;
        }

        private static void CreateActiveFaceCoverVisual(Transform parent, string name)
        {
            var coverObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            coverObject.name = name;
            coverObject.transform.SetParent(parent, worldPositionStays: false);
            var collider = coverObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static GameObject AssertActiveFaceCover(
            GameplayBoardSurfaceRenderer renderer,
            string coverName)
        {
            Assert.That(renderer, Is.Not.Null);
            var cover = renderer.ActiveFaceCoverRoot.Find(coverName);
            Assert.That(cover, Is.Not.Null, $"Expected active face cover '{coverName}' to exist.");
            Assert.That(cover.gameObject.activeSelf, Is.True);
            return cover.gameObject;
        }

        private static void AssertActiveFaceCoverTiling(GameObject cover, float expectedXTiling, float expectedYTiling)
        {
            AssertActiveFaceCoverVisualTiling(cover, "VisualBottom", expectedXTiling, expectedYTiling);
            AssertActiveFaceCoverVisualTiling(cover, "VisualFront", expectedXTiling, 1f);
            AssertActiveFaceCoverVisualTiling(cover, "VisualBack", expectedXTiling, 1f);
        }

        private static void AssertActiveFaceCoverVisualTiling(
            GameObject cover,
            string visualName,
            float expectedXTiling,
            float expectedYTiling)
        {
            var visual = cover.transform.Find(visualName);
            Assert.That(visual, Is.Not.Null, $"Expected active face cover visual '{visualName}' to exist.");
            var visualRenderer = visual.GetComponent<Renderer>();
            Assert.That(visualRenderer, Is.Not.Null);

            var propertyBlock = new MaterialPropertyBlock();
            visualRenderer.GetPropertyBlock(propertyBlock);
            AssertScaleOffset(propertyBlock.GetVector(Shader.PropertyToID("_BaseMap_ST")), expectedXTiling, expectedYTiling);
            AssertScaleOffset(propertyBlock.GetVector(Shader.PropertyToID("_MainTex_ST")), expectedXTiling, expectedYTiling);
        }

        private static void AssertScaleOffset(Vector4 actual, float expectedXTiling, float expectedYTiling)
        {
            Assert.That(actual.x, Is.EqualTo(expectedXTiling).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expectedYTiling).Within(0.0001f));
            Assert.That(actual.z, Is.Zero);
            Assert.That(actual.w, Is.Zero);
        }

        private sealed class SurfaceTileLifecycleProbe : MonoBehaviour
        {
            public int EnabledCount { get; private set; }

            public int DisabledCount { get; private set; }

            private void OnEnable()
            {
                EnabledCount++;
            }

            private void OnDisable()
            {
                DisabledCount++;
            }

            public void ResetCounts()
            {
                EnabledCount = 0;
                DisabledCount = 0;
            }
        }

        private static void AssertSurfaceTileMatchesProjection(
            GameObject tile,
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            float cellSize = 1f)
        {
            Assert.That(tile, Is.Not.Null);

            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(projector.TryProjectSurfaceCell(cell, topology, out var projectedPose), Is.True);

            var tileTransform = tile.transform;
            var expectedPosition = projectedPose.LocalPosition - (projectedPose.Normal * (tileTransform.localScale.z * 0.5f));

            Assert.That(tileTransform.localPosition, Is.EqualTo(expectedPosition));
            Assert.That(Quaternion.Angle(tileTransform.localRotation, projectedPose.LocalRotation), Is.LessThan(0.001f));
            Assert.That(tileTransform.localScale.x, Is.EqualTo(cellSize).Within(0.001f));
            Assert.That(tileTransform.localScale.y, Is.EqualTo(cellSize).Within(0.001f));
            Assert.That(tileTransform.localScale.z, Is.EqualTo(cellSize * 0.25f).Within(0.001f));
        }

        private static float ResolveEntityOuterFaceDepth(GameplayEntityVisualProfile profile)
        {
            return profile.SurfaceOffsetFromFacePlane
                   - profile.ModelLocalPosition.z
                   - (profile.ModelLocalScale.z * 0.5f);
        }

        private static void AssertSurfaceTileMatchesRetainedTransitionProjection(
            GameObject tile,
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            float cellSize = 1f)
        {
            Assert.That(tile, Is.Not.Null);

            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(
                projector.TryProjectTransitionSurfaceCell(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    out var projectedPose),
                Is.True);

            var tileTransform = tile.transform;
            var expectedNormal = projectedPose.Normal;
            var expectedCenter = projectedPose.LocalPosition;
            var expectedPosition = expectedCenter - (expectedNormal * (tileTransform.localScale.z * 0.5f));
            var expectedRotation = projectedPose.LocalRotation;

            Assert.That(tileTransform.localPosition, Is.EqualTo(expectedPosition));
            Assert.That(Quaternion.Angle(tileTransform.localRotation, expectedRotation), Is.LessThan(0.001f));
            Assert.That(
                tileTransform.localScale.x,
                Is.EqualTo(cellSize * GameplayPresentationGeometry.TileCoverageMultiplier).Within(0.001f));
            Assert.That(
                tileTransform.localScale.y,
                Is.EqualTo(cellSize * GameplayPresentationGeometry.TileCoverageMultiplier).Within(0.001f));
            Assert.That(
                tileTransform.localScale.z,
                Is.EqualTo(cellSize * GameplayPresentationGeometry.TileThicknessMultiplier).Within(0.001f));
        }

        private static GameplayEntityPose GetProjectedSurfaceTileLocalPose(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(projector.TryProjectSurfaceCell(cell, topology, out var projectedPose), Is.True);
            var tileThickness = cellSize * GameplayPresentationGeometry.TileThicknessMultiplier;
            return new GameplayEntityPose(
                projectedPose.LocalPosition - (projectedPose.Normal * (tileThickness * 0.5f)),
                projectedPose.LocalRotation);
        }

        private static PlayerControlTimingSettings CreateImmediatePlayerControlTimingSettings()
        {
            return new PlayerControlTimingSettings
            {
                PushExecuteDelaySeconds = 0f,
                PushInputLockDurationSeconds = 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                FlipExecuteDelaySeconds = 0f,
                FlipInputLockDurationSeconds = 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
            };
        }

        private static Vector3 GetViewPosition(GameplaySceneHost host, int entityId)
        {
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            return view.transform.position;
        }

        private static GameplayEntityView CreateBoxFlipInteractionViewPrefab(string name)
        {
            var viewObject = new GameObject(name);
            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(20);
            view.ConfigureModelRoot(Vector3.zero, Quaternion.identity);

            var modelObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            modelObject.name = "Visual";
            modelObject.transform.SetParent(view.ModelRoot, worldPositionStays: false);
            modelObject.transform.localPosition = Vector3.zero;
            modelObject.transform.localRotation = Quaternion.identity;
            modelObject.transform.localScale = Vector3.one;

            var collider = modelObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var flipDriver = viewObject.AddComponent<BoxFlipInteractionDriver>();
            PlayerViewPrefabTestUtility.SetSerializedField(flipDriver, "visualRoot", view.ModelRoot);
            return view;
        }

        private static void ConfigureStaticPresentationCatalog(
            StaticEntityPresentationCatalog catalog,
            string presentationId,
            GameplayEntityView viewPrefab)
        {
            PlayerViewPrefabTestUtility.SetSerializedField(
                catalog,
                "entries",
                new[]
                {
                    new StaticEntityPresentationCatalogEntry
                    {
                        PresentationId = presentationId,
                        ViewPrefab = viewPrefab,
                    },
                });
        }

        private static void AssertVisualMatchesProfile(
            GameplayEntityView view,
            GameplayEntityVisualProfile expectedProfile,
            Color expectedColor)
        {
            Assert.That(view, Is.Not.Null);
            Assert.That(view.transform.parent, Is.Not.Null);
            Assert.That(view.GetComponent<Renderer>(), Is.Null);
            Assert.That(view.ModelRoot, Is.Not.Null);
            Assert.That(view.ModelRoot.parent, Is.EqualTo(view.transform));
            Assert.That(view.ModelRoot.localPosition, Is.EqualTo(expectedProfile.ModelLocalPosition));
            Assert.That(
                Quaternion.Angle(view.ModelRoot.localRotation, expectedProfile.ModelLocalRotation),
                Is.LessThan(0.001f));
            Assert.That(view.ModelRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(view.ModelRoot.childCount, Is.EqualTo(1));

            var visual = view.ModelRoot.GetChild(0);
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            Assert.That(visual.localScale, Is.EqualTo(expectedProfile.ModelLocalScale));
            Assert.That(visual.GetComponent<Collider>(), Is.Null);

            var meshFilter = visual.GetComponent<MeshFilter>();
            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh.name, Does.Contain("Cube").IgnoreCase);

            var renderer = visual.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null);
            AssertColorApproximately(renderer.sharedMaterial.color, expectedColor);
        }

        private static void AssertColorApproximately(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
        }

        private static void AssertTransformPoseApproximately(
            Transform actual,
            Vector3 expectedPosition,
            Quaternion expectedRotation)
        {
            Assert.That(Vector3.Distance(actual.position, expectedPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(actual.rotation, expectedRotation), Is.LessThan(0.001f));
        }

        private static void AssertTransformPoseApproximately(Transform actual, Transform expected)
        {
            AssertTransformPoseApproximately(actual, expected.position, expected.rotation);
        }

        private static void AssertVectorApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f));
        }

        private static TopologyTransitionVisualState CreateTopologyTransitionVisualState(
            float progress01,
            CubeRotationKind rotationKind,
            Quaternion presentedVisualRotation)
        {
            return new TopologyTransitionVisualState(
                isActive: true,
                progress01: progress01,
                sourceTopology: new CubeTopologyState(FaceId.Floor),
                destinationTopology: new CubeTopologyState(FaceId.Front),
                rotationKind: rotationKind,
                durationSeconds: 0.2f,
                presentedVisualRotation: presentedVisualRotation,
                angularVelocityNormalized: 1f);
        }

        private static int[] GetUnitIdsAt(WorldSnapshot snapshot, SurfaceCell cell)
        {
            var units = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, units);
            return units.Select(entity => entity.entityId).ToArray();
        }

        private static void AssertContactRetainedBeforeVisualContact(
            GameplayEntityPresentationLifecycleDebugSnapshot snapshot,
            int expectedInstanceId,
            float expectedTimelineTimeSeconds)
        {
            Assert.That(snapshot.TimelineTimeSeconds, Is.EqualTo(expectedTimelineTimeSeconds).Within(0.0001f));
            Assert.That(snapshot.EntityId, Is.EqualTo(40));
            Assert.That(snapshot.GameObjectName, Is.EqualTo("EntityView_40"));
            Assert.That(snapshot.InstanceId, Is.EqualTo(expectedInstanceId));
            Assert.That(snapshot.HasEnemyAnimatorDriver, Is.True);
            Assert.That(snapshot.HasEntityViewComponent, Is.True);
            Assert.That(snapshot.ViewsByEntityIdContainsEntityId, Is.True);
            Assert.That(snapshot.ContactDelayedRetainedEntityIdsContainsEntityId, Is.True);
            Assert.That(snapshot.DeathPresentationPlayingEntityIdsContainsEntityId, Is.False);
            Assert.That(snapshot.RetainedLocalTargetPosesContainsEntityId, Is.True);
            Assert.That(snapshot.PendingContactExitContainsEntityId, Is.True);
            Assert.That(snapshot.PendingContactExitRemainingSeconds, Is.GreaterThan(0f));
            Assert.That(snapshot.PendingDeathCleanupContainsEntityId, Is.False);
            Assert.That(snapshot.GameObjectActiveSelf, Is.True);
            Assert.That(snapshot.RendererEnabled, Is.True);
            Assert.That(snapshot.RendererActiveInHierarchy, Is.True);
            Assert.That(snapshot.IsVfxPooledInstance, Is.False);
            Assert.That(snapshot.DeathTriggerCount, Is.Zero);
        }

        private static void AssertOriginalEnemyCleanedUpAtDestroyVfxHandoff(
            GameplayEntityPresentationLifecycleDebugSnapshot snapshot,
            int expectedInstanceId,
            float expectedTimelineTimeSeconds)
        {
            Assert.That(snapshot.TimelineTimeSeconds, Is.EqualTo(expectedTimelineTimeSeconds).Within(0.0001f));
            Assert.That(snapshot.EntityId, Is.EqualTo(40));
            Assert.That(snapshot.GameObjectName, Is.EqualTo("EntityView_40"));
            Assert.That(snapshot.InstanceId, Is.EqualTo(expectedInstanceId));
            Assert.That(snapshot.HasEnemyAnimatorDriver, Is.True);
            Assert.That(snapshot.HasEntityViewComponent, Is.True);
            Assert.That(snapshot.ViewsByEntityIdContainsEntityId, Is.True);
            Assert.That(snapshot.ContactDelayedRetainedEntityIdsContainsEntityId, Is.False);
            Assert.That(snapshot.DeathPresentationPlayingEntityIdsContainsEntityId, Is.False);
            Assert.That(snapshot.RetainedLocalTargetPosesContainsEntityId, Is.False);
            Assert.That(snapshot.PendingContactExitContainsEntityId, Is.False);
            Assert.That(snapshot.PendingDeathCleanupContainsEntityId, Is.False);
            Assert.That(snapshot.GameObjectActiveSelf, Is.False);
            Assert.That(snapshot.RendererActiveInHierarchy, Is.False);
            Assert.That(snapshot.IsVfxPooledInstance, Is.False);
            Assert.That(snapshot.DeathTriggerCount, Is.Zero);
        }

        private sealed class TestViewFactory : IGameplayEntityViewFactory
        {
            private readonly bool _attachEnemyAnimatorDriver;
            private readonly bool _attachPlayerAnimatorDriver;
            private readonly Transform _parent;

            public TestViewFactory(
                Transform parent,
                bool attachEnemyAnimatorDriver = false,
                bool attachPlayerAnimatorDriver = false)
            {
                _parent = parent;
                _attachEnemyAnimatorDriver = attachEnemyAnimatorDriver;
                _attachPlayerAnimatorDriver = attachPlayerAnimatorDriver;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (_attachEnemyAnimatorDriver &&
                    EntityRolePolicy.IsEnemyUnit(entity))
                {
                    viewObject.AddComponent<EnemyAnimatorDriver>();
                }

                if (_attachPlayerAnimatorDriver &&
                    EntityRolePolicy.IsPlayerUnit(entity))
                {
                    viewObject.AddComponent<PlayerAnimatorDriver>();
                    viewObject.AddComponent<PlayerAnimationTimingAuthoring>();
                }

                return view;
            }
        }
    }
}
