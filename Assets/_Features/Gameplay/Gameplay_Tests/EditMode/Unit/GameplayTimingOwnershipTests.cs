using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTimingOwnershipTests
    {
        private const string EnemyJumpAnimatorControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Jump.controller";

        [Test]
        [Category("Full")]
        public void GameplaySceneHost_Initialize_WithoutPlayerPrefab_AutoCreatesPrimitivePlayerViewWithMotionFallbackDefaults()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_WithoutPlayerPrefab_AutoCreatesPrimitivePlayerViewWithMotionFallbackDefaults");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreatePlayerEntity(),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PushMotionDurationSeconds = 0.35f,
                        FlipMotionDurationSeconds = 0.6f,
                    });

                Assert.That(host.ViewRegistry.TryGetView(10, out var playerView), Is.True);
                var authoring = playerView.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                var snapshot = authoring.CreateSnapshot();
                Assert.That(snapshot.TryGetAnimatorDurationOverride(PlayerPresentationPhase.PushWindup, out _), Is.False);
                Assert.That(snapshot.TryGetAnimatorDurationOverride(PlayerPresentationPhase.PushRecovery, out _), Is.False);
                Assert.That(snapshot.TryGetAnimatorDurationOverride(PlayerPresentationPhase.FlipWindup, out _), Is.False);
                Assert.That(snapshot.TryGetAnimatorDurationOverride(PlayerPresentationPhase.FlipRecovery, out _), Is.False);
                Assert.That(
                    driver.GetPresentationDurationSeconds(PlayerActionKind.Push, host.TimingProfile.PushMotionDurationSeconds),
                    Is.EqualTo(host.TimingProfile.PushMotionDurationSeconds));
                Assert.That(
                    driver.GetPresentationDurationSeconds(PlayerActionKind.Flip, host.TimingProfile.FlipMotionDurationSeconds),
                    Is.EqualTo(host.TimingProfile.FlipMotionDurationSeconds));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_DoesNotAddLegacyStageClearOverlayComponent()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_DoesNotAddLegacyStageClearOverlayComponent");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = false,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreatePlayerEntity(),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        ObjectiveRuntimeDefinition = CreateSingleCellObjective(new SurfaceCell(FaceId.Floor, 0, 0)),
                        PlayerEntityId = 10,
                    });

                Assert.That(hostObject.GetComponent<GameplayStageClearOverlay>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayInputHost_RunSingleTick_RaisesStageClearedEvent_WithoutLegacyOverlayPath()
        {
            var hostObject = new GameObject("GameplayInputHost_RunSingleTick_RaisesStageClearedEvent_WithoutLegacyOverlayPath");
            StageContentEntry stageContentEntry = null;

            try
            {
                stageContentEntry = CreateStageContentEntry("stage-test-clear");
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = false,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreatePlayerEntity(),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        StageContentEntry = stageContentEntry,
                        ObjectiveRuntimeDefinition = CreateSingleCellObjective(new SurfaceCell(FaceId.Floor, 0, 0)),
                        PlayerEntityId = 10,
                    });

                var stageClearedCallCount = 0;
                host.InputHost.StageCleared += () => stageClearedCallCount++;

                var result = host.InputHost.RunSingleTick();
                Assert.That(result, Is.Not.Null);
                Assert.That(result.ObjectiveResult.ClearedThisTick, Is.True);
                Assert.That(stageClearedCallCount, Is.EqualTo(1));
                Assert.That(host.CurrentObjectiveResult.IsCleared, Is.True);
                Assert.That(hostObject.GetComponent<GameplayStageClearOverlay>(), Is.Null);
            }
            finally
            {
                if (stageContentEntry != null)
                {
                    UnityEngine.Object.DestroyImmediate(stageContentEntry);
                }

                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Full")]
        public void MechanicsShowcase_NonAttackingEnemyProfileAndStartisPrefab_ShareMoveCadence()
        {
            const string enemyProfilePath =
                StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_NonAttacking/EnemyAi_NonAttacking.asset";
            const string enemyPrefabPath =
                StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Startis.prefab";

            var enemyProfile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(enemyProfilePath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(enemyPrefabPath);

            Assert.That(enemyProfile, Is.Not.Null, $"Missing enemy AI profile at '{enemyProfilePath}'.");
            Assert.That(enemyPrefab, Is.Not.Null, $"Missing enemy prefab at '{enemyPrefabPath}'.");

            var locomotionAuthoring = enemyPrefab.GetComponent<UnitLocomotionPresentationAuthoring>();

            Assert.That(locomotionAuthoring, Is.Not.Null);
            Assert.That(
                locomotionAuthoring.MoveMotionDurationSeconds,
                Is.EqualTo(enemyProfile.LocomotionTimingSettings.MoveCooldownSeconds).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_WithoutAnimatorOverride_UsesResolvedMotionDuration()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_WithoutAnimatorOverride_UsesResolvedMotionDuration");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(
                    authoring,
                    "pushWindupAnimatorDurationSeconds",
                    PlayerAnimationTimingAuthoring.UseResolvedMotionDurationSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    authoring,
                    "pushRecoveryAnimatorDurationSeconds",
                    PlayerAnimationTimingAuthoring.UseResolvedMotionDurationSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    authoring,
                    "flipWindupAnimatorDurationSeconds",
                    PlayerAnimationTimingAuthoring.UseResolvedMotionDurationSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    authoring,
                    "flipRecoveryAnimatorDurationSeconds",
                    PlayerAnimationTimingAuthoring.UseResolvedMotionDurationSentinel);

                Assert.That(driver.GetPresentationDurationSeconds(PlayerActionKind.Push, 0.25f), Is.EqualTo(0.25f));
                Assert.That(driver.GetPresentationDurationSeconds(PlayerActionKind.Flip, 0.5f), Is.EqualTo(0.5f));

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push, resolvedMotionDurationSeconds: 0.25f);
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushWindup));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(8f).Within(0.0001f));

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip, resolvedMotionDurationSeconds: 0.5f);
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipWindup));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_WithAnimatorOverride_PrefersAnimatorDurationOverResolvedMotionDuration()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_WithAnimatorOverride_PrefersAnimatorDurationOverResolvedMotionDuration");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.4f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.8f);

                Assert.That(driver.GetPresentationDurationSeconds(PlayerActionKind.Push, 0.25f), Is.EqualTo(0.4f));
                Assert.That(driver.GetPresentationDurationSeconds(PlayerActionKind.Flip, 0.5f), Is.EqualTo(0.8f));

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push, resolvedMotionDurationSeconds: 0.25f);
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushWindup));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2.5f).Within(0.0001f));

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip, resolvedMotionDurationSeconds: 0.5f);
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipWindup));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1.25f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayMotionTimingResolver_FlipResultTurn_UsesFlipRecoveryDurationInsteadOfEntityFlipMotion()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayMotionTimingResolver_FlipResultTurn_UsesFlipRecoveryDurationInsteadOfEntityFlipMotion");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var motionAuthoring = rootObject.GetComponent<EntityMotionPresentationAuthoring>();
                var animationAuthoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = new GameplayMotionTimingResolver(stateStore, new GameplayPresentationTrackState());
                var timingProfile = GameplayTimingProfile.CreateDefault();

                PlayerViewPrefabTestUtility.SetSerializedField(
                    motionAuthoring,
                    "flipMotionDurationSeconds",
                    2f);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    animationAuthoring,
                    "flipWindupAnimatorDurationSeconds",
                    0.38333333f);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    animationAuthoring,
                    "flipRecoveryAnimatorDurationSeconds",
                    0.56666666f);
                stateStore.ViewsByEntityId[10] = view;

                Assert.That(
                    resolver.ResolvePlayerMotionDurationSeconds(10, PlayerActionKind.Flip, timingProfile),
                    Is.EqualTo(2f));
                Assert.That(
                    resolver.ResolvePlayerFlipResultTurnDurationSeconds(10, timingProfile),
                    Is.EqualTo(0.56666666f).Within(0.0000001f));
                Assert.That(
                    resolver.ResolvePlayerFlipResultTurnDelaySeconds(10, timingProfile),
                    Is.EqualTo(0.38333333f).Within(0.0000001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_PushStart_CrossFadesToWindup()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_PushStart_CrossFadesToWindup");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.25f);

                driver.Apply(new PlayerViewPresentationState(10, 1, PlayerActionKind.Push, 1, startedThisTick: true, executedThisTick: false, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushWindup));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Windup"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerHiddenDriverSync_DoesNotCrossFadeInactiveAnimator()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject(
                nameof(PlayerHiddenDriverSync_DoesNotCrossFadeInactiveAnimator));

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                AttachPlayerRuntimeAnimator(rootObject, driver);
                var coordinator = new GameplayAnimationSyncCoordinator();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                coordinator.CacheDrivers(10, view);

                driver.Apply(new PlayerViewPresentationState(
                    10,
                    1,
                    PlayerActionKind.Push,
                    1,
                    startedThisTick: true,
                    executedThisTick: false,
                    completedThisTick: false,
                    canceledThisTick: false));
                rootObject.SetActive(false);

                coordinator.SyncHiddenDrivers(
                    new HashSet<int>(),
                    _ => PlayerViewAnimationState.Push,
                    (_, _) => 0f,
                    viewsByEntityId);

                Assert.That(driver.LastCrossFadedStateName, Is.Empty);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
                LogAssert.NoUnexpectedReceived();

                rootObject.SetActive(true);
                driver.SyncRuntimeState(isVisible: true, PlayerViewAnimationState.Push);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushWindup));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Windup"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_ActionAttemptPush_CrossFadesToPushWindup()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_ActionAttemptPush_CrossFadesToPushWindup");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.25f);

                driver.Apply(new PlayerViewPresentationState(
                    10,
                    1,
                    PlayerActionKind.None,
                    0,
                    startedThisTick: false,
                    executedThisTick: false,
                    completedThisTick: false,
                    canceledThisTick: false,
                    hasActionAttempt: true,
                    actionAttemptKind: PlayerActionKind.Push,
                    actionAttemptDirection: Direction.Right,
                    actionAttemptFeedbackKind: PlayerActionAttemptFeedbackKind.NoTarget));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);

                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushWindup));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Windup"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_PushExecute_CrossFadesToRecovery()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_PushExecute_CrossFadesToRecovery");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.5f);

                driver.Apply(new PlayerViewPresentationState(10, 1, PlayerActionKind.Push, 1, startedThisTick: false, executedThisTick: true, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushRecovery));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Recovery"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_FlipStart_CrossFadesToWindup()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_FlipStart_CrossFadesToWindup");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.5f);

                driver.Apply(new PlayerViewPresentationState(10, 1, PlayerActionKind.Flip, 1, startedThisTick: true, executedThisTick: false, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipWindup));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Flip_Windup"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_ActionAttemptFlip_CrossFadesToFlipWindup()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_ActionAttemptFlip_CrossFadesToFlipWindup");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.5f);

                driver.Apply(new PlayerViewPresentationState(
                    10,
                    1,
                    PlayerActionKind.None,
                    0,
                    startedThisTick: false,
                    executedThisTick: false,
                    completedThisTick: false,
                    canceledThisTick: false,
                    hasActionAttempt: true,
                    actionAttemptKind: PlayerActionKind.Flip,
                    actionAttemptDirection: Direction.Right,
                    actionAttemptFeedbackKind: PlayerActionAttemptFeedbackKind.NoTarget));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip);

                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipWindup));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Flip_Windup"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_FlipExecute_CrossFadesToRecovery()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_FlipExecute_CrossFadesToRecovery");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipRecoveryAnimatorDurationSeconds", 0.75f);

                driver.Apply(new PlayerViewPresentationState(10, 1, PlayerActionKind.Flip, 1, startedThisTick: false, executedThisTick: true, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipRecovery));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Flip_Recovery"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_Execute_DoesNotReplayWindup()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_Execute_DoesNotReplayWindup");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.25f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.5f);

                driver.Apply(new PlayerViewPresentationState(10, 1, PlayerActionKind.Push, 1, startedThisTick: true, executedThisTick: false, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Windup"));

                driver.Apply(new PlayerViewPresentationState(10, 2, PlayerActionKind.Push, 1, startedThisTick: false, executedThisTick: true, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);

                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushRecovery));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Recovery"));
                Assert.That(driver.LastCrossFadedStateName, Is.Not.EqualTo("Push_Windup"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_PhaseDurations_DriveDistinctAnimatorSpeeds()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_PhaseDurations_DriveDistinctAnimatorSpeeds");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.25f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.5f);

                driver.Apply(new PlayerViewPresentationState(10, 1, PlayerActionKind.Push, 1, startedThisTick: true, executedThisTick: false, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);
                var windupSpeed = driver.CurrentAnimatorSpeed;

                driver.Apply(new PlayerViewPresentationState(10, 2, PlayerActionKind.Push, 1, startedThisTick: false, executedThisTick: true, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Push);
                var recoverySpeed = driver.CurrentAnimatorSpeed;

                Assert.That(windupSpeed, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(recoverySpeed, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(recoverySpeed, Is.LessThan(windupSpeed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_FlipRecovery_UsesRecoveryClipLengthForSpeed()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_FlipRecovery_UsesRecoveryClipLengthForSpeed");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var animator = AttachEnemyRuntimeAnimator(rootObject);
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3DM/1Player/Player_S1.controller");

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);
                Assert.That(controller, Is.Not.Null);

                animator.runtimeAnimatorController = controller;

                PlayerViewPrefabTestUtility.SetSerializedField(driver, "animator", animator);
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "flipWindupStateName", "Flip_Windup");
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "flipRecoveryStateName", "Flip_Recovery");
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipRecoveryAnimatorDurationSeconds", 2f);

                var expectedReferenceLengthSeconds = controller.animationClips
                    .Where(clip => clip != null && string.Equals(clip.name, "Flip_Recovery", StringComparison.Ordinal))
                    .Select(clip => clip.length)
                    .Single();

                driver.Apply(new PlayerViewPresentationState(10, 1, PlayerActionKind.Flip, 1, startedThisTick: false, executedThisTick: true, completedThisTick: false, canceledThisTick: false));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip);

                Assert.That(expectedReferenceLengthSeconds, Is.GreaterThan(0f));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipRecovery));
                Assert.That(
                    driver.CurrentAnimatorSpeed,
                    Is.EqualTo(expectedReferenceLengthSeconds / 2f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimationTimingAuthoring_CreateSnapshot_PreservesDeathAnimatorDurationOverride()
        {
            var rootObject = new GameObject("PlayerAnimationTimingAuthoring_CreateSnapshot_PreservesDeathAnimatorDurationOverride");

            try
            {
                var authoring = rootObject.AddComponent<PlayerAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathAnimatorDurationSeconds", 1.75f);

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.TryGetDeathAnimatorDurationOverride(out var durationSeconds), Is.True);
                Assert.That(durationSeconds, Is.EqualTo(1.75f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimationTimingAuthoring_CreateSnapshot_PreservesStageClearVictoryAnimatorDurationOverride()
        {
            var rootObject = new GameObject("PlayerAnimationTimingAuthoring_CreateSnapshot_PreservesStageClearVictoryAnimatorDurationOverride");

            try
            {
                var authoring = rootObject.AddComponent<PlayerAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "stageClearVictoryAnimatorDurationSeconds", 1.25f);

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.TryGetStageClearVictoryAnimatorDurationOverride(out var durationSeconds), Is.True);
                Assert.That(durationSeconds, Is.EqualTo(1.25f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_DeathDurationOverride_UsesInspectorValue()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_DeathDurationOverride_UsesInspectorValue");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var animator = AttachEnemyRuntimeAnimator(rootObject);
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3DM/1Player/Player_S1.controller");

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);
                Assert.That(controller, Is.Not.Null);

                animator.runtimeAnimatorController = controller;

                PlayerViewPrefabTestUtility.SetSerializedField(driver, "animator", animator);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathAnimatorDurationSeconds", 2f);

                var expectedReferenceLengthSeconds = controller.animationClips
                    .Where(clip => clip != null && string.Equals(clip.name, "Death", StringComparison.Ordinal))
                    .Select(clip => clip.length)
                    .Single();

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Death);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.None));
                Assert.That(driver.DeathPresentationDurationSeconds, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(expectedReferenceLengthSeconds / 2f).Within(0.0001f));
                Assert.That(animator.speed, Is.EqualTo(expectedReferenceLengthSeconds / 2f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_StageClearVictoryDurationOverride_UsesInspectorValue()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_StageClearVictoryDurationOverride_UsesInspectorValue");

            try
            {
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var animator = rootObject.AddComponent<Animator>();
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3DM/1Player/Player_S1.controller");

                Assert.That(authoring, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);
                Assert.That(controller, Is.Not.Null);

                animator.runtimeAnimatorController = controller;

                PlayerViewPrefabTestUtility.SetSerializedField(driver, "animator", animator);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "stageClearVictoryAnimatorDurationSeconds", 1.5f);

                var expectedReferenceLengthSeconds = controller.animationClips
                    .Where(clip => clip != null && string.Equals(clip.name, "Item", StringComparison.Ordinal))
                    .Select(clip => clip.length)
                    .Single();

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.StageClearVictory);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.StageClearVictory));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.None));
                Assert.That(driver.StageClearVictoryPresentationDurationSeconds, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(expectedReferenceLengthSeconds / 1.5f).Within(0.0001f));
                Assert.That(animator.speed, Is.EqualTo(expectedReferenceLengthSeconds / 1.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_WalkLoop_UsesSingleStateForEnterAndExit()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_WalkLoop_UsesSingleStateForEnterAndExit");

            try
            {
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(driver, Is.Not.Null);

                PlayerViewPrefabTestUtility.SetSerializedField(driver, "walkStateName", "Walk_Loop");
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "walkExitStateName", string.Empty);

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.WalkLoop);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Walk_Loop"));

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.WalkLoop);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.WalkLoop));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Walk_Loop"));

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Idle);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Idle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerViewPresentationMapper_PlayerRemovedThisTick_SetsDidDie()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerViewPresentationMapper_PlayerRemovedThisTick_SetsDidDie");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var mapper = new PlayerViewPresentationMapper();
                var buffer = new Dictionary<int, PlayerViewPresentationState>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                mapper.Build(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    sourceCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Unit),
                            }),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    buffer);

                Assert.That(buffer.ContainsKey(10), Is.True);
                Assert.That(buffer[10].DidDie, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerViewPresentationMapper_AcceptedDamageSignal_SetsTookDamageThisTick()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerViewPresentationMapper_AcceptedDamageSignal_SetsTookDamageThisTick");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var mapper = new PlayerViewPresentationMapper();
                var buffer = new Dictionary<int, PlayerViewPresentationState>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                mapper.Build(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = sourceCell,
                                hp = 2,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                unitRole = UnitRole.Player,
                                facing = Direction.Right,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                            },
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    buffer);

                Assert.That(buffer.ContainsKey(10), Is.True);
                Assert.That(buffer[10].DidDie, Is.False);
                Assert.That(buffer[10].TookDamageThisTick, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerViewPresentationMapper_ActionAttemptSignal_SetsFakeAttemptStateWithoutActiveAction()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerViewPresentationMapper_ActionAttemptSignal_SetsFakeAttemptStateWithoutActiveAction");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var mapper = new PlayerViewPresentationMapper();
                var buffer = new Dictionary<int, PlayerViewPresentationState>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                mapper.Build(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = sourceCell,
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                unitRole = UnitRole.Player,
                                facing = Direction.Right,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            Array.Empty<TickPlayerDamagePresentationSignal>(),
                            Array.Empty<TickEnemyDamagePresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerActionAttemptPresentationSignal(
                                    10,
                                    PlayerActionKind.Push,
                                    Direction.Right,
                                    PlayerActionAttemptFeedbackKind.NoTarget),
                            }),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    buffer);

                Assert.That(buffer.ContainsKey(10), Is.True);
                Assert.That(buffer[10].ActiveActionKind, Is.EqualTo(PlayerActionKind.None));
                Assert.That(buffer[10].HasActionAttempt, Is.True);
                Assert.That(buffer[10].ActionAttemptKind, Is.EqualTo(PlayerActionKind.Push));
                Assert.That(buffer[10].ActionAttemptDirection, Is.EqualTo(Direction.Right));
                Assert.That(buffer[10].ActionAttemptFeedbackKind, Is.EqualTo(PlayerActionAttemptFeedbackKind.NoTarget));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerViewPresentationMapper_StageClearVictoryOutcomeSignal_SetsPlayerOutcome()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerViewPresentationMapper_StageClearVictoryOutcomeSignal_SetsPlayerOutcome");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var mapper = new PlayerViewPresentationMapper();
                var buffer = new Dictionary<int, PlayerViewPresentationState>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };

                mapper.Build(
                    CreatePlayerPresentationTickResult(
                        tickIndex: 1,
                        playerOutcomeSignals: new[]
                        {
                            new TickPlayerOutcomePresentationSignal(
                                10,
                                TickPlayerOutcomePresentationKind.StageClearVictory,
                                sourceTileId: 100,
                                new SurfaceCell(FaceId.Floor, 1, 1)),
                            new TickPlayerOutcomePresentationSignal(
                                11,
                                TickPlayerOutcomePresentationKind.StageClearVictory,
                                sourceTileId: 101,
                                new SurfaceCell(FaceId.Floor, 2, 2)),
                        }),
                    viewsByEntityId,
                    buffer);

                Assert.That(buffer.ContainsKey(10), Is.True);
                Assert.That(buffer[10].HasPlayerOutcome, Is.True);
                Assert.That(buffer[10].PlayerOutcomeKind, Is.EqualTo(TickPlayerOutcomePresentationKind.StageClearVictory));
                Assert.That(buffer.ContainsKey(11), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_ActionAttempt_PrioritizesAttemptOverWalkLoop()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_ActionAttempt_PrioritizesAttemptOverWalkLoop");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                coordinator.ApplyTickPresentation(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[] { CreatePlayerEntity() },
                        Array.Empty<string>(),
                        new CubeTopologyState(FaceId.Floor),
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerLocomotionPresentationSignal(
                                    10,
                                    shouldPlayWalkLoop: true,
                                    moveMotionGeneratedThisTick: true,
                                    waitingForNextMoveCadence: false),
                            },
                            Array.Empty<TickPlayerDamagePresentationSignal>(),
                            Array.Empty<TickEnemyDamagePresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerActionAttemptPresentationSignal(
                                    10,
                                    PlayerActionKind.Push,
                                    Direction.Right,
                                    PlayerActionAttemptFeedbackKind.NoTarget),
                            }),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    (_, _) => 0.25f);

                Assert.That(
                    coordinator.ResolvePlayerAnimationState(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true),
                    Is.EqualTo(PlayerViewAnimationState.Push));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_StageClearVictory_PrioritizesOverActionAndWalk_AndHolds()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_StageClearVictory_PrioritizesOverActionAndWalk_AndHolds");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "stageClearVictoryAnimatorDurationSeconds", 0.5f);
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerActionSignals: new[]
                    {
                        new TickPlayerActionPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            activeActionSequence: 1,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false),
                    },
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() },
                    playerOutcomeSignals: new[]
                    {
                        new TickPlayerOutcomePresentationSignal(
                            10,
                            TickPlayerOutcomePresentationKind.StageClearVictory,
                            sourceTileId: 100,
                            new SurfaceCell(FaceId.Floor, 1, 1)),
                    });

                var playback = coordinator.ResolvePlayerAnimationPlayback(
                    10,
                    shouldPlayWalkLoop: true,
                    hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.StageClearVictory));
                Assert.That(coordinator.LastStageClearPlayerPresentationDelaySeconds, Is.EqualTo(0.5f).Within(0.0001f));

                coordinator.AdvancePlayerPresentation(0.25f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 2,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() });
                playback = coordinator.ResolvePlayerAnimationPlayback(
                    10,
                    shouldPlayWalkLoop: true,
                    hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.StageClearVictory));

                coordinator.AdvancePlayerPresentation(0.3f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 3,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() });
                playback = coordinator.ResolvePlayerAnimationPlayback(
                    10,
                    shouldPlayWalkLoop: true,
                    hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.WalkLoop));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_DeathSuppressesStageClearVictoryDelay()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_DeathSuppressesStageClearVictoryDelay");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "stageClearVictoryAnimatorDurationSeconds", 0.5f);
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    finalEntities: new[] { CreatePlayerEntity(hp: 0) },
                    playerOutcomeSignals: new[]
                    {
                        new TickPlayerOutcomePresentationSignal(
                            10,
                            TickPlayerOutcomePresentationKind.StageClearVictory,
                            sourceTileId: 100,
                            new SurfaceCell(FaceId.Floor, 1, 1)),
                    });

                var playback = coordinator.ResolvePlayerAnimationPlayback(
                    10,
                    shouldPlayWalkLoop: false,
                    hasActiveWalkMotion: false);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(coordinator.LastStageClearPlayerPresentationDelaySeconds, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostPresentationFeed_StageClearVictoryDelay_DefersStageClearedFrame()
        {
            var rootObject = new GameObject("GameplayHostPresentationFeed_StageClearVictoryDelay_DefersStageClearedFrame");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayHostPresentationFeed_Victory_PlayerPrefab");

            try
            {
                var authoring = playerViewPrefab.GetComponent<PlayerAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "stageClearVictoryAnimatorDurationSeconds", 0.5f);

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var inputHost = rootObject.AddComponent<GameplayInputHost>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var topology = new CubeTopologyState(FaceId.Floor);
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
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(new[] { CreatePlayerEntity() }, topology);

                var feed = new GameplayHostPresentationFeed(inputHost, presenter);
                var frames = new List<GameplayPresentationFrame>();
                feed.FramePublished += frames.Add;
                var result = CreateStageClearVictoryTickResult(tickIndex: 7);

                presenter.Present(result);
                InvokePresentationFeedTickCompleted(feed, result);

                Assert.That(feed.HasPendingStageClearPresentation, Is.True);
                Assert.That(frames, Has.Count.EqualTo(1));
                Assert.That(frames[0].StageEvent.HasValue, Is.False);

                presenter.UpdatePresentation(0.25f);
                Assert.That(feed.HasPendingStageClearPresentation, Is.True);
                Assert.That(frames, Has.Count.EqualTo(1));

                presenter.UpdatePresentation(0.3f);
                Assert.That(feed.HasPendingStageClearPresentation, Is.False);
                Assert.That(frames, Has.Count.EqualTo(2));
                Assert.That(frames[1].StageEvent.HasValue, Is.True);
                Assert.That(frames[1].StageEvent.Value.EventKind, Is.EqualTo(GameplayStageEventKind.Cleared));
                feed.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_ActionAttemptPush_HoldsUntilPlaybackComplete_EvenWhenNextTickHasWalk()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_ActionAttemptPush_HoldsUntilPlaybackComplete_EvenWhenNextTickHasWalk");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.2f);

                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerLocomotionSignals: new[]
                    {
                        CreateWalkLoopSignal(),
                    },
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.NoTarget),
                    });

                var playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.PushWindup));
                coordinator.SyncPlayerRuntimeState(10, isVisible: true, playback, resolvedMotionDurationSeconds: 0f, viewsByEntityId);
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Windup"));

                coordinator.AdvancePlayerPresentation(0.1f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 2,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() });
                playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.PushWindup));

                coordinator.AdvancePlayerPresentation(0.15f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 3,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() });
                playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.PushRecovery));
                coordinator.SyncPlayerRuntimeState(10, isVisible: true, playback, resolvedMotionDurationSeconds: 0f, viewsByEntityId);
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Recovery"));

                coordinator.AdvancePlayerPresentation(0.2f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 4,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() });
                playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.WalkLoop));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_ActionAttemptFlip_HoldsUntilPlaybackComplete_EvenWhenNextTickHasWalk()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_ActionAttemptFlip_HoldsUntilPlaybackComplete_EvenWhenNextTickHasWalk");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.15f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipRecoveryAnimatorDurationSeconds", 0.25f);

                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() },
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Flip,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.NoTarget),
                    });

                var playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.FlipWindup));
                coordinator.SyncPlayerRuntimeState(10, isVisible: true, playback, resolvedMotionDurationSeconds: 0f, viewsByEntityId);
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Flip_Windup"));

                coordinator.AdvancePlayerPresentation(0.2f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 2,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() });
                playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.FlipRecovery));
                coordinator.SyncPlayerRuntimeState(10, isVisible: true, playback, resolvedMotionDurationSeconds: 0f, viewsByEntityId);
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Flip_Recovery"));

                coordinator.AdvancePlayerPresentation(0.25f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 3,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() });
                playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.WalkLoop));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayAnimationSyncCoordinator_ActionAttemptWithoutVisualFeedback_DoesNotHoldAnimation()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_ActionAttemptWithoutVisualFeedback_DoesNotHoldAnimation");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal() },
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Flip,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.Invalid,
                            emitsVisualFeedback: false),
                    });

                var playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);

                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.WalkLoop));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.None));
                Assert.That(coordinator.IsPlayerActionAttemptHoldActive(10), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_RealActionStart_ClearsActionAttemptHold()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_RealActionStart_ClearsActionAttemptHold");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.2f);

                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.NoTarget),
                    });

                coordinator.AdvancePlayerPresentation(0.1f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 2,
                    playerActionSignals: new[]
                    {
                        new TickPlayerActionPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            7,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false),
                    });

                var playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_Death_ClearsActionAttemptHold()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_Death_ClearsActionAttemptHold");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Flip,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.NoTarget),
                    });

                coordinator.AdvancePlayerPresentation(0.1f);
                coordinator.ApplyTickPresentation(
                    new TickResult(
                        2,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                            },
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    (_, _) => 0.4f);

                var playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_NewActionAttempt_ReplacesExistingAttemptHold()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_NewActionAttempt_ReplacesExistingAttemptHold");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipRecoveryAnimatorDurationSeconds", 0.2f);

                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.NoTarget),
                    });
                Assert.That(
                    coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: false, hasActiveWalkMotion: false).State,
                    Is.EqualTo(PlayerViewAnimationState.Push));

                coordinator.AdvancePlayerPresentation(0.1f);
                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 2,
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Flip,
                            Direction.Left,
                            PlayerActionAttemptFeedbackKind.AssistOutOfRange),
                    });

                var playback = coordinator.ResolvePlayerAnimationPlayback(10, shouldPlayWalkLoop: false, hasActiveWalkMotion: false);
                Assert.That(playback.State, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(playback.PhaseOverride, Is.EqualTo(PlayerPresentationPhase.FlipWindup));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_ActionAttemptHoldQuery_TracksOnlyFakeAttemptHold()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_ActionAttemptHoldQuery_TracksOnlyFakeAttemptHold");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var authoring = rootObject.GetComponent<PlayerAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.2f);

                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.NoTarget),
                    });
                Assert.That(coordinator.IsPlayerActionAttemptHoldActive(10), Is.True);

                ApplyPlayerPresentationTick(
                    coordinator,
                    viewsByEntityId,
                    tickIndex: 2,
                    playerActionSignals: new[]
                    {
                        new TickPlayerActionPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            7,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false),
                    });
                Assert.That(coordinator.IsPlayerActionAttemptHoldActive(10), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayInputHost_ActionAttemptPlaybackActive_SuppressesMoveCommandAndPendingActions()
        {
            var playerViewObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayInputHost_ActionAttemptPlaybackActive_PlayerView");
            var presenterObject = new GameObject("GameplayInputHost_ActionAttemptPlaybackActive_Presenter");
            var inputHostObject = new GameObject("GameplayInputHost_ActionAttemptPlaybackActive_InputHost");

            try
            {
                var view = playerViewObject.GetComponent<GameplayEntityView>();
                var authoring = playerViewObject.GetComponent<PlayerAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushWindupAnimatorDurationSeconds", 0.2f);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushRecoveryAnimatorDurationSeconds", 0.2f);

                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                var coordinator = ReadPrivateField<GameplayTickPresentationCoordinator>(
                    presenter,
                    "_presentationCoordinator");
                var animationSync = ReadPrivateField<GameplayAnimationSyncCoordinator>(
                    coordinator,
                    "_animationSync");
                var viewsByEntityId = new Dictionary<int, GameplayEntityView> { [10] = view };
                animationSync.CacheDrivers(10, view);
                animationSync.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());
                ApplyPlayerPresentationTick(
                    animationSync,
                    viewsByEntityId,
                    tickIndex: 1,
                    playerActionAttemptSignals: new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.NoTarget),
                    });

                var inputHost = inputHostObject.AddComponent<GameplayInputHost>();
                InitializeInputHostForCommandTest(inputHost, presenter);
                inputHost.SetRawMoveInput(Vector2.right);
                inputHost.BufferPush();
                inputHost.BufferFlip();

                var command = (PlayerTickCommand)InvokeInstanceMethod(inputHost, "BuildPlayerCommand");

                Assert.That(presenter.IsPlayerActionAttemptPlaybackActive(10), Is.True);
                Assert.That(command.MoveDirection, Is.EqualTo(Direction.None));
                Assert.That(command.PushPressed, Is.False);
                Assert.That(command.FlipPressed, Is.False);
                Assert.That(ReadPrivateField<bool>(inputHost, "_hasBufferedPush"), Is.False);
                Assert.That(ReadPrivateField<bool>(inputHost, "_hasBufferedFlip"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewObject);
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(inputHostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayInputHost_ActionAttemptSignal_ClearsBufferedMoveIntent()
        {
            var inputHostObject = new GameObject("GameplayInputHost_ActionAttemptSignal_ClearsBufferedMoveIntent");

            try
            {
                var inputHost = inputHostObject.AddComponent<GameplayInputHost>();
                InitializeInputHostForCommandTest(inputHost, presenter: null);
                inputHost.SetRawMoveInput(Vector2.right);
                inputHost.SetRawMoveInput(Vector2.zero);

                var bufferedCommand = (PlayerTickCommand)InvokeInstanceMethod(inputHost, "BuildPlayerCommand");
                Assert.That(bufferedCommand.MoveDirection, Is.EqualTo(Direction.Right));

                InvokeInstanceMethod(
                    inputHost,
                    "ApplyAcceptedBufferedInput",
                    CreateActionAttemptTickResult(
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            Direction.Right,
                            PlayerActionAttemptFeedbackKind.NoTarget)));

                var clearedCommand = (PlayerTickCommand)InvokeInstanceMethod(inputHost, "BuildPlayerCommand");
                Assert.That(clearedCommand.MoveDirection, Is.EqualTo(Direction.None));
                Assert.That(clearedCommand.PushPressed, Is.False);
                Assert.That(clearedCommand.FlipPressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inputHostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerViewPresentationMapper_DeathSignal_CopiesFatalSourcePresentationFields()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerViewPresentationMapper_DeathSignal_CopiesFatalSourcePresentationFields");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var mapper = new PlayerViewPresentationMapper();
                var buffer = new Dictionary<int, PlayerViewPresentationState>();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                mapper.Build(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            Array.Empty<TickPlayerDamagePresentationSignal>(),
                            new[]
                            {
                                new TickPlayerDeathPresentationSignal(
                                    10,
                                    didDieThisTick: true,
                                    sourceEntityId: 20,
                                    fallbackFacing: Direction.Right,
                                    resolvedDamageSourceAvailable: true,
                                    damageAmountAtFatalHit: 2,
                                    deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse),
                            },
                            Array.Empty<TickEnemyDamagePresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    buffer);

                Assert.That(buffer.ContainsKey(10), Is.True);
                var state = buffer[10];
                Assert.That(state.DidDie, Is.True);
                Assert.That(state.DidDieThisTick, Is.True);
                Assert.That(state.DeathSourceEntityId, Is.EqualTo(20));
                Assert.That(state.ResolvedDamageSourceAvailable, Is.True);
                Assert.That(state.DamageAmountAtFatalHit, Is.EqualTo(2));
                Assert.That(state.DeathDirectionHintKind, Is.EqualTo(DeathDirectionHintKind.AttackerReverse));
                Assert.That(state.DeathFallbackFacing, Is.EqualTo(Direction.Right));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_PlayerDidDie_PrioritizesDeathOverActionAndWalk()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_PlayerDidDie_PrioritizesDeathOverActionAndWalk");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                coordinator.ApplyTickPresentation(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.Push,
                                    1,
                                    startedThisTick: true,
                                    completedThisTick: false,
                                    canceledThisTick: false),
                            },
                            new[]
                            {
                                new TickPlayerLocomotionPresentationSignal(
                                    10,
                                    shouldPlayWalkLoop: true,
                                    moveMotionGeneratedThisTick: true,
                                    waitingForNextMoveCadence: false),
                            },
                            new[]
                            {
                                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                            },
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    (_, _) => 0.25f);

                Assert.That(
                    coordinator.ResolvePlayerAnimationState(10, shouldPlayWalkLoop: true, hasActiveWalkMotion: true),
                    Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(driver.HitSignalCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_PlayerHitSignal_TriggersAnimatorWithoutChangingBaseState()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_PlayerHitSignal_TriggersAnimatorWithoutChangingBaseState");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                coordinator.ApplyTickPresentation(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = sourceCell,
                                hp = 2,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                unitRole = UnitRole.Player,
                                facing = Direction.Right,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                            },
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    (_, _) => 0f);

                var resolvedState = coordinator.ResolvePlayerAnimationState(10, shouldPlayWalkLoop: false, hasActiveWalkMotion: false);
                coordinator.SyncPlayerRuntimeState(10, isVisible: true, resolvedState, 0f, viewsByEntityId);

                Assert.That(driver.HitSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_PlayerRespawnSpawn_ClearsDeathOverride()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_PlayerRespawnSpawn_ClearsDeathOverride");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                coordinator.ApplyTickPresentation(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    (_, _) => 0f);

                var deathState = coordinator.ResolvePlayerAnimationState(10, shouldPlayWalkLoop: false, hasActiveWalkMotion: false);
                coordinator.SyncPlayerRuntimeState(10, isVisible: true, deathState, 0f, viewsByEntityId);

                Assert.That(deathState, Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));

                coordinator.ApplyTickPresentation(
                    new TickResult(
                        2,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = sourceCell,
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                unitRole = UnitRole.Player,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Right,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    (_, _) => 0f);

                var respawnState = coordinator.ResolvePlayerAnimationState(10, shouldPlayWalkLoop: false, hasActiveWalkMotion: false);
                coordinator.SyncPlayerRuntimeState(10, isVisible: true, respawnState, 0f, viewsByEntityId);

                Assert.That(respawnState, Is.EqualTo(PlayerViewAnimationState.Idle));
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Idle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAnimationSyncCoordinator_FlipOutcomeCarryOver_DoesNotModifyCollectionDuringEnumeration()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("GameplayAnimationSyncCoordinator_FlipOutcomeCarryOver_DoesNotModifyCollectionDuringEnumeration");

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [10] = view,
                };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(10, view);
                coordinator.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>());

                coordinator.ApplyTickPresentation(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = sourceCell,
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                unitRole = UnitRole.Player,
                                facing = Direction.Right,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.Flip,
                                    7,
                                    startedThisTick: false,
                                    completedThisTick: false,
                                    canceledThisTick: false,
                                    executedThisTick: true,
                                    isRecoveryPhase: true,
                                    resolutionKind: TickPlayerActionResolutionKind.Impact,
                                    targetEntityId: 20,
                                    direction: Direction.Right,
                                    actionPlanId: 91,
                                    flipOutcome: TickPlayerFlipOutcomeKind.DestroySelf,
                                    hasFlipImpactContactTiming: true,
                                    flipTargetBoxEntityId: 20),
                            },
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty),
                    viewsByEntityId,
                    (_, _) => 0.5f);

                Assert.That(driver.LastPresentationState.FlipOutcome, Is.EqualTo(TickPlayerFlipOutcomeKind.DestroySelf));

                Assert.DoesNotThrow(
                    () => coordinator.ApplyTickPresentation(
                        new TickResult(
                            2,
                            Array.Empty<TickPhase>(),
                            Array.Empty<string>(),
                            MovementPhaseResult.Empty,
                            AttackPhaseResult.Empty,
                            new[]
                            {
                                new EntityState
                                {
                                    entityId = 10,
                                    position = sourceCell,
                                    hp = 3,
                                    maxHp = 3,
                                    teamId = 1,
                                    type = EntityType.Unit,
                                    unitRole = UnitRole.Player,
                                    facing = Direction.Right,
                                    boardPresence = EntityBoardPresence.Occupying,
                                },
                            },
                            Array.Empty<string>(),
                            topology,
                            new TickPresentationData(
                                Array.Empty<TickEntityMotion>(),
                                topologyMotion: null,
                                Array.Empty<TickVisibilityChange>(),
                                Array.Empty<TickTransitionVisibilityChange>(),
                                new[]
                                {
                                    new TickPlayerActionPresentationSignal(
                                        10,
                                        PlayerActionKind.Flip,
                                        7,
                                        startedThisTick: false,
                                        completedThisTick: false,
                                        canceledThisTick: false,
                                        executedThisTick: false,
                                        isRecoveryPhase: true,
                                        resolutionKind: TickPlayerActionResolutionKind.None,
                                        targetEntityId: 20,
                                        direction: Direction.Right,
                                        actionPlanId: 91,
                                        flipOutcome: TickPlayerFlipOutcomeKind.None,
                                        hasFlipImpactContactTiming: false,
                                        flipTargetBoxEntityId: 20),
                                },
                                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                                Array.Empty<TickEnemyActionPresentationSignal>(),
                                Array.Empty<TickEnemyJumpPresentationSignal>(),
                                Array.Empty<TickEntityExitPresentationSignal>()),
                            string.Empty,
                            TickTrace.Empty),
                        viewsByEntityId,
                        (_, _) => 0.5f));

                Assert.That(driver.LastPresentationState.FlipOutcome, Is.EqualTo(TickPlayerFlipOutcomeKind.DestroySelf));
                Assert.That(driver.LastPresentationState.HasFlipImpactContactTiming, Is.True);
                Assert.That(driver.LastPresentationState.FlipTargetBoxEntityId, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_DeathState_CrossFadesToDeath()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_DeathState_CrossFadesToDeath");

            try
            {
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(driver, Is.Not.Null);

                driver.Apply(new PlayerViewPresentationState(
                    10,
                    1,
                    PlayerActionKind.None,
                    0,
                    startedThisTick: false,
                    executedThisTick: false,
                    completedThisTick: false,
                    canceledThisTick: false,
                    shouldPlayWalkLoop: false,
                    isRecoveryPhase: false,
                    didDie: true));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Death);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.None));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Death"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerAnimatorDriver_StageClearVictoryState_CrossFadesToItem()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_StageClearVictoryState_CrossFadesToItem");

            try
            {
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();

                Assert.That(driver, Is.Not.Null);

                driver.Apply(new PlayerViewPresentationState(
                    10,
                    1,
                    PlayerActionKind.None,
                    0,
                    startedThisTick: false,
                    executedThisTick: false,
                    completedThisTick: false,
                    canceledThisTick: false,
                    hasPlayerOutcome: true,
                    playerOutcomeKind: TickPlayerOutcomePresentationKind.StageClearVictory));
                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.StageClearVictory);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.StageClearVictory));
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.None));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Item"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerDeath_DoesNotRequireNewPlayerActionKind()
        {
            CollectionAssert.AreEqual(
                new[] { "None", "Push", "Flip" },
                Enum.GetNames(typeof(PlayerActionKind)));
        }

        [Test]
        [Category("Extended")]
        public void PlayerS1Controller_WalkLoopConfiguration_RemovesLegacyStartAndDoneStates()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/3DM/1Player/Player_S1.controller");

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Has.Length.GreaterThanOrEqualTo(1));

            var stateMachine = controller.layers[0].stateMachine;
            var idleState = FindState(stateMachine, "Idle");
            var walkLoopState = FindState(stateMachine, "Walk_Loop");
            var walkStartState = FindState(stateMachine, "Walk_Start");
            var walkDoneState = FindState(stateMachine, "Walk_Done");

            Assert.That(idleState, Is.Not.Null);
            Assert.That(walkLoopState, Is.Not.Null);
            Assert.That(walkStartState, Is.Null);
            Assert.That(walkDoneState, Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void PlayerDeath_UsesUnifiedDeathClip_NotSplitRuntimeChain()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/3DM/1Player/Player_S1.controller");

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Has.Length.GreaterThanOrEqualTo(1));

            var stateMachine = controller.layers[0].stateMachine;
            var deathState = FindState(stateMachine, "Death");
            var dieStartState = FindState(stateMachine, "Die_Start");
            var dieDoneState = FindState(stateMachine, "Die_Done");
            var deathMotion = deathState?.motion as AnimationClip;

            Assert.That(deathState, Is.Not.Null);
            Assert.That(dieStartState, Is.Null);
            Assert.That(dieDoneState, Is.Null);
            Assert.That(deathState.transitions, Is.Empty);
            Assert.That(deathMotion, Is.Not.Null);
            Assert.That(deathMotion.name, Is.EqualTo("Death"));
            Assert.That(controller.animationClips.Any(clip => clip != null && clip.name == "Death"), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerS1Controller_StageClearVictory_UsesItemStateAndClip()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/3DM/1Player/Player_S1.controller");

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Has.Length.GreaterThanOrEqualTo(1));

            var stateMachine = controller.layers[0].stateMachine;
            var itemState = FindState(stateMachine, "Item");
            var itemMotion = itemState?.motion as AnimationClip;

            Assert.That(itemState, Is.Not.Null);
            Assert.That(itemState.transitions, Is.Empty);
            Assert.That(itemMotion, Is.Not.Null);
            Assert.That(itemMotion.name, Is.EqualTo("Item"));
            Assert.That(itemMotion.isLooping, Is.False);
            Assert.That(AnimationUtility.GetAnimationEvents(itemMotion), Is.Empty);
            Assert.That(controller.animationClips.Any(clip => clip != null && clip.name == "Item"), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PlayerS1Controller_PlayerActionStates_ArePhaseSplitWithoutAutoTransitions()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/3DM/1Player/Player_S1.controller");

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Has.Length.GreaterThanOrEqualTo(1));

            var stateMachine = controller.layers[0].stateMachine;
            var pushWindupState = FindState(stateMachine, "Push_Windup");
            var pushRecoveryState = FindState(stateMachine, "Push_Recovery");
            var flipWindupState = FindState(stateMachine, "Flip_Windup");
            var flipRecoveryState = FindState(stateMachine, "Flip_Recovery");
            var legacyKickState = FindState(stateMachine, "Kick");
            var legacyChangeStartState = FindState(stateMachine, "Change_Start");
            var legacyChangeStopState = FindState(stateMachine, "Change_Stop");

            Assert.That(pushWindupState, Is.Not.Null);
            Assert.That(pushRecoveryState, Is.Not.Null);
            Assert.That(flipWindupState, Is.Not.Null);
            Assert.That(flipRecoveryState, Is.Not.Null);
            Assert.That(pushWindupState.transitions, Is.Empty);
            Assert.That(pushRecoveryState.transitions, Is.Empty);
            Assert.That(flipWindupState.transitions, Is.Empty);
            Assert.That(flipRecoveryState.transitions, Is.Empty);
            Assert.That(legacyKickState, Is.Null);
            Assert.That(legacyChangeStartState, Is.Null);
            Assert.That(legacyChangeStopState, Is.Null);
        }

        [Test]
        [Category("Full")]
        public void PlayerS1Prefab_PlayerAnimatorDriver_MapsWalkSequenceStates()
        {
            var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab");

            Assert.That(prefabObject, Is.Not.Null);

            var driver = prefabObject.GetComponent<PlayerAnimatorDriver>();
            var authoring = prefabObject.GetComponent<PlayerAnimationTimingAuthoring>();
            var view = prefabObject.GetComponent<GameplayEntityView>();

            Assert.That(driver, Is.Not.Null);
            Assert.That(authoring, Is.Not.Null);
            Assert.That(view, Is.Not.Null);
            Assert.That(view.ModelRoot, Is.Not.Null);
            Assert.That(GetPrivateInstanceField<string>(driver, "walkStateName"), Is.EqualTo("Walk_Loop"));
            Assert.That(GetPrivateInstanceField<string>(driver, "walkExitStateName"), Is.Empty);
            Assert.That(GetPrivateInstanceField<string>(driver, "pushWindupStateName"), Is.EqualTo("Push_Windup"));
            Assert.That(GetPrivateInstanceField<string>(driver, "pushRecoveryStateName"), Is.EqualTo("Push_Recovery"));
            Assert.That(GetPrivateInstanceField<string>(driver, "flipWindupStateName"), Is.EqualTo("Flip_Windup"));
            Assert.That(GetPrivateInstanceField<string>(driver, "flipRecoveryStateName"), Is.EqualTo("Flip_Recovery"));
            Assert.That(GetPrivateInstanceField<string>(driver, "deathStateName"), Is.EqualTo("Death"));
            Assert.That(GetPrivateInstanceField<string>(driver, "stageClearVictoryStateName"), Is.EqualTo("Item"));
            Assert.That(GetPrivateInstanceField<float>(driver, "stateTransitionCrossFadeDurationSeconds"), Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "pushWindupAnimatorDurationSeconds"), Is.EqualTo(0.18333334f).Within(0.0000001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "pushRecoveryAnimatorDurationSeconds"), Is.EqualTo(0.3f).Within(0.0000001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "flipWindupAnimatorDurationSeconds"), Is.EqualTo(0.38333333f).Within(0.0000001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "flipRecoveryAnimatorDurationSeconds"), Is.EqualTo(0.56666666f).Within(0.0000001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "stageClearVictoryAnimatorDurationSeconds"), Is.EqualTo(-1f));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimationTimingAuthoring_CreateSnapshot_UsesOptionalOverrides()
        {
            var rootObject = new GameObject("EnemyAnimationTimingAuthoring_CreateSnapshot_UsesOptionalOverrides");
            var windupReferenceClip = CreateReferenceClip("HumanM@Attack1H04_R_Windup", 0.53333336f);
            var jumpWindupReferenceClip = CreateReferenceClip("EnemyJumpWindupReference", 0.8f);
            var jumpAirborneReferenceClip = CreateReferenceClip("EnemyJumpAirborneReference", 1.1f);
            var recoverReferenceClip = CreateReferenceClip("HumanM@Attack1H04_R_Recover", 0.6333333f);

            try
            {
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: 0.35f,
                    jumpWindupAnimatorDurationSeconds: 0.45f,
                    jumpAirborneAnimatorDurationSeconds: 0.9f,
                    recoverAnimatorDurationSeconds: 0.6f,
                    stateTransitionCrossFadeDurationSeconds: 0.12f,
                    attackWindupReferenceClip: windupReferenceClip,
                    jumpWindupReferenceClip: jumpWindupReferenceClip,
                    jumpAirborneReferenceClip: jumpAirborneReferenceClip,
                    recoverReferenceClip: recoverReferenceClip);

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.TryGetAttackWindupAnimatorDurationOverride(out var windupDurationSeconds), Is.True);
                Assert.That(windupDurationSeconds, Is.EqualTo(0.35f));
                Assert.That(
                    snapshot.TryGetAttackWindupReferenceClipLengthSeconds(
                        out var windupReferenceClipLengthSeconds),
                    Is.True);
                Assert.That(windupReferenceClipLengthSeconds, Is.EqualTo(windupReferenceClip.length).Within(0.0001f));
                Assert.That(snapshot.TryGetJumpWindupAnimatorDurationOverride(out var jumpWindupDurationSeconds), Is.True);
                Assert.That(jumpWindupDurationSeconds, Is.EqualTo(0.45f));
                Assert.That(
                    snapshot.TryGetJumpWindupReferenceClipLengthSeconds(
                        out var jumpWindupReferenceClipLengthSeconds),
                    Is.True);
                Assert.That(jumpWindupReferenceClipLengthSeconds, Is.EqualTo(jumpWindupReferenceClip.length).Within(0.0001f));
                Assert.That(snapshot.TryGetJumpAirborneAnimatorDurationOverride(out var jumpAirborneDurationSeconds), Is.True);
                Assert.That(jumpAirborneDurationSeconds, Is.EqualTo(0.9f));
                Assert.That(
                    snapshot.TryGetJumpAirborneReferenceClipLengthSeconds(
                        out var jumpAirborneReferenceClipLengthSeconds),
                    Is.True);
                Assert.That(jumpAirborneReferenceClipLengthSeconds, Is.EqualTo(jumpAirborneReferenceClip.length).Within(0.0001f));
                Assert.That(snapshot.TryGetRecoverAnimatorDurationOverride(out var recoverDurationSeconds), Is.True);
                Assert.That(recoverDurationSeconds, Is.EqualTo(0.6f));
                Assert.That(
                    snapshot.TryGetRecoverReferenceClipLengthSeconds(
                        out var recoverReferenceClipLengthSeconds),
                    Is.True);
                Assert.That(recoverReferenceClipLengthSeconds, Is.EqualTo(recoverReferenceClip.length).Within(0.0001f));
                Assert.That(snapshot.TryGetDeathAnimatorDurationOverride(out _), Is.False);
                Assert.That(snapshot.TryGetDeathReferenceClipLengthSeconds(out _), Is.False);
                Assert.That(snapshot.TryGetStateTransitionCrossFadeDurationOverride(out var crossFadeDurationSeconds), Is.True);
                Assert.That(crossFadeDurationSeconds, Is.EqualTo(0.12f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(jumpWindupReferenceClip);
                UnityEngine.Object.DestroyImmediate(jumpAirborneReferenceClip);
                UnityEngine.Object.DestroyImmediate(recoverReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimationTimingAuthoring_CreateSnapshot_InvalidOverride_Throws()
        {
            var rootObject = new GameObject("EnemyAnimationTimingAuthoring_CreateSnapshot_InvalidOverride_Throws");

            try
            {
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();

                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "attackWindupAnimatorDurationSeconds", 0f);
                var invalidWindupException = Assert.Throws<ArgumentOutOfRangeException>(() => authoring.CreateSnapshot());
                Assert.That(invalidWindupException.ParamName, Is.EqualTo("attackWindupAnimatorDurationSeconds"));

                PlayerViewPrefabTestUtility.SetSerializedField(
                    authoring,
                    "attackWindupAnimatorDurationSeconds",
                    EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "jumpWindupAnimatorDurationSeconds", 0f);
                var invalidJumpWindupException = Assert.Throws<ArgumentOutOfRangeException>(() => authoring.CreateSnapshot());
                Assert.That(invalidJumpWindupException.ParamName, Is.EqualTo("jumpWindupAnimatorDurationSeconds"));

                PlayerViewPrefabTestUtility.SetSerializedField(
                    authoring,
                    "jumpWindupAnimatorDurationSeconds",
                    EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "stateTransitionCrossFadeDurationSeconds", -2f);
                var invalidCrossFadeException = Assert.Throws<ArgumentOutOfRangeException>(() => authoring.CreateSnapshot());
                Assert.That(invalidCrossFadeException.ParamName, Is.EqualTo("stateTransitionCrossFadeDurationSeconds"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimationTimingAuthoring_CreateSnapshot_DurationOverrideWithoutReferenceClip_Throws()
        {
            var rootObject = new GameObject("EnemyAnimationTimingAuthoring_CreateSnapshot_DurationOverrideWithoutReferenceClip_Throws");

            try
            {
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "attackWindupAnimatorDurationSeconds", 1f);

                var attackException = Assert.Throws<InvalidOperationException>(() => authoring.CreateSnapshot());
                StringAssert.Contains("attackWindupReferenceClip", attackException.Message);
                StringAssert.Contains("attackWindupAnimatorDurationSeconds", attackException.Message);

                PlayerViewPrefabTestUtility.SetSerializedField(
                    authoring,
                    "attackWindupAnimatorDurationSeconds",
                    EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel);
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "jumpAirborneAnimatorDurationSeconds", 1f);

                var jumpException = Assert.Throws<InvalidOperationException>(() => authoring.CreateSnapshot());
                StringAssert.Contains("jumpAirborneReferenceClip", jumpException.Message);
                StringAssert.Contains("jumpAirborneAnimatorDurationSeconds", jumpException.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimationTimingAuthoring_CreateSnapshot_PreservesDeathAnimatorDurationAndReferenceClip()
        {
            var rootObject = new GameObject("EnemyAnimationTimingAuthoring_CreateSnapshot_PreservesDeathAnimatorDurationAndReferenceClip");
            var deathReferenceClip = CreateReferenceClip("EnemyDeathReference", 0.85f);

            try
            {
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    recoverAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    stateTransitionCrossFadeDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    deathAnimatorDurationSeconds: 1.6f,
                    deathReferenceClip: deathReferenceClip);

                var snapshot = authoring.CreateSnapshot();

                Assert.That(snapshot.TryGetDeathAnimatorDurationOverride(out var durationSeconds), Is.True);
                Assert.That(durationSeconds, Is.EqualTo(1.6f));
                Assert.That(snapshot.TryGetDeathReferenceClipLengthSeconds(out var referenceClipLengthSeconds), Is.True);
                Assert.That(referenceClipLengthSeconds, Is.EqualTo(deathReferenceClip.length).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(deathReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimationTimingAuthoring_DeathDurationOverrideWithoutReferenceClip_Throws()
        {
            var rootObject = new GameObject("EnemyAnimationTimingAuthoring_DeathDurationOverrideWithoutReferenceClip_Throws");

            try
            {
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "deathAnimatorDurationSeconds", 1f);

                var exception = Assert.Throws<InvalidOperationException>(() => authoring.CreateSnapshot());
                StringAssert.Contains("deathReferenceClip", exception.Message);
                StringAssert.Contains("deathAnimatorDurationSeconds", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimationTimingAuthoring_PublicApi_IsLimitedToTimingSnapshotAndValidation()
        {
            var publicMethodNames = typeof(EnemyAnimationTimingAuthoring)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "CreateSnapshot",
                    "Validate",
                    "get_AttackWindupAnimatorDurationSeconds",
                    "get_DeathAnimatorDurationSeconds",
                    "get_JumpAirborneAnimatorDurationSeconds",
                    "get_JumpWindupAnimatorDurationSeconds",
                    "get_RecoverAnimatorDurationSeconds",
                    "get_StateTransitionCrossFadeDurationSeconds",
                },
                publicMethodNames);
            Assert.That(publicMethodNames, Does.Not.Contain("ApplyOverrides"));
            Assert.That(publicMethodNames, Does.Not.Contain("TryGetAttackWindupAnimatorDurationOverride"));
            Assert.That(publicMethodNames, Does.Not.Contain("TryGetRecoverAnimatorDurationOverride"));
            Assert.That(publicMethodNames, Does.Not.Contain("TryGetStateTransitionCrossFadeDurationOverride"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_PublicApi_DoesNotExposeTimingQueryOverrides()
        {
            var publicMethodNames = typeof(EnemyAnimatorDriver)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(publicMethodNames, Does.Not.Contain("TryGetAttackWindupAnimatorDurationOverride"));
            Assert.That(publicMethodNames, Does.Not.Contain("TryGetRecoverAnimatorDurationOverride"));
            Assert.That(publicMethodNames, Does.Not.Contain("TryGetStateTransitionCrossFadeDurationOverride"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_OptionalAnimationTimingHook_StaysPresentationOnly()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_OptionalAnimationTimingHook_StaysPresentationOnly");
            var windupReferenceClip = CreateReferenceClip("WindupReference", 1f);
            var recoverReferenceClip = CreateReferenceClip("RecoverReference", 1f);

            try
            {
                var animator = rootObject.AddComponent<Animator>();
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: 0.4f,
                    recoverAnimatorDurationSeconds: 0.5f,
                    stateTransitionCrossFadeDurationSeconds: 0.08f,
                    attackWindupReferenceClip: windupReferenceClip,
                    recoverReferenceClip: recoverReferenceClip);

                var snapshot = authoring.CreateSnapshot();
                Assert.That(snapshot.TryGetAttackWindupAnimatorDurationOverride(out var windupDurationSeconds), Is.True);
                Assert.That(windupDurationSeconds, Is.EqualTo(0.4f));
                Assert.That(snapshot.TryGetRecoverAnimatorDurationOverride(out var recoverDurationSeconds), Is.True);
                Assert.That(recoverDurationSeconds, Is.EqualTo(0.5f));
                Assert.That(snapshot.TryGetStateTransitionCrossFadeDurationOverride(out var crossFadeDurationSeconds), Is.True);
                Assert.That(crossFadeDurationSeconds, Is.EqualTo(0.08f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Attack,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2.5f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.4f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.08f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));
                Assert.That(animator.speed, Is.EqualTo(2.5f).Within(0.0001f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: true,
                    startedRecoveryThisTick: true,
                    tookDamage: true,
                    didDie: false));

                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.08f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Recover"));
                Assert.That(animator.speed, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.AttackSignalCount, Is.EqualTo(1));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
                Assert.That(driver.HitSignalCount, Is.EqualTo(1));
                Assert.That(driver.DeathSignalCount, Is.EqualTo(0));
                Assert.That(driver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(driver.CurrentActiveActionKind, Is.EqualTo(EnemyActionKind.Melee));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(recoverReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayAnimationSyncCoordinator_UtilityRecoverTrack_UsesViewDurationAfterLogicRecoverOutlivesView()
        {
            var rootObject = new GameObject("GameplayAnimationSyncCoordinator_UtilityRecoverTrack_UsesViewDurationAfterLogicRecoverOutlivesView");
            var recoverReferenceClip = CreateReferenceClip("RecoverReference", 0.3f);

            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                var coordinator = new GameplayAnimationSyncCoordinator();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [40] = view,
                };
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    recoverAnimatorDurationSeconds: 0.5f,
                    stateTransitionCrossFadeDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    recoverReferenceClip: recoverReferenceClip);
                coordinator.CacheDrivers(40, view);

                coordinator.ApplyTickPresentation(
                    CreateEnemyUtilityAnimationTick(
                        tickIndex: 1,
                        new[]
                        {
                            new TickEnemyUtilityPresentationSignal(
                                40,
                                EnemyUtilityPresentationKind.LockNearbyBoxes,
                                EnemyUtilityPresentationPhase.RecoverStarted,
                                startTick: 1,
                                executeTick: 9,
                                durationTicks: 8,
                                effectIndex: 0,
                                activationSequence: 7),
                        }),
                    viewsByEntityId,
                    (_, _) => 0f);

                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(recoverReferenceClip.length / 0.5f).Within(0.0001f));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));

                coordinator.ApplyTickPresentation(
                    CreateEnemyUtilityAnimationTick(
                        tickIndex: 2,
                        utilitySignals: Array.Empty<TickEnemyUtilityPresentationSignal>(),
                        utilityPhaseStates: new[]
                        {
                            new TickEnemyUtilityPhasePresentationState(
                                40,
                                EnemyUtilityPresentationKind.LockNearbyBoxes,
                                EnemyUtilityEffectPhase.Recover,
                                phaseElapsedTicks: 1,
                                phaseDurationTicks: 8,
                                effectIndex: 0,
                                activationSequence: 7),
                        }),
                    viewsByEntityId,
                    (_, _) => 0f);

                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(recoverReferenceClip.length / 0.5f).Within(0.0001f));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));

                coordinator.AdvancePlayerPresentation(0.49f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));

                coordinator.AdvancePlayerPresentation(0.02f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recoverReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayAnimationSyncCoordinator_UtilityWindupTrack_UsesViewDurationAfterLogicWindupEnds()
        {
            var rootObject = new GameObject("GameplayAnimationSyncCoordinator_UtilityWindupTrack_UsesViewDurationAfterLogicWindupEnds");
            var windupReferenceClip = CreateReferenceClip("WindupReference", 0.3f);

            try
            {
                var view = rootObject.AddComponent<GameplayEntityView>();
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                var coordinator = new GameplayAnimationSyncCoordinator();
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [40] = view,
                };
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: 0.5f,
                    recoverAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    stateTransitionCrossFadeDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    attackWindupReferenceClip: windupReferenceClip);
                coordinator.CacheDrivers(40, view);

                coordinator.ApplyTickPresentation(
                    CreateEnemyUtilityAnimationTick(
                        tickIndex: 1,
                        new[]
                        {
                            new TickEnemyUtilityPresentationSignal(
                                40,
                                EnemyUtilityPresentationKind.LockNearbyBoxes,
                                EnemyUtilityPresentationPhase.WindupStarted,
                                startTick: 1,
                                executeTick: 3,
                                durationTicks: 2,
                                effectIndex: 0,
                                activationSequence: 5),
                        }),
                    viewsByEntityId,
                    (_, _) => 0f);

                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(windupReferenceClip.length / 0.5f).Within(0.0001f));
                Assert.That(driver.UtilityWindupSignalCount, Is.EqualTo(1));

                coordinator.ApplyTickPresentation(
                    CreateEnemyUtilityAnimationTick(
                        tickIndex: 2,
                        utilitySignals: Array.Empty<TickEnemyUtilityPresentationSignal>()),
                    viewsByEntityId,
                    (_, _) => 0f);

                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(windupReferenceClip.length / 0.5f).Within(0.0001f));
                Assert.That(driver.UtilityWindupSignalCount, Is.EqualTo(1));

                coordinator.AdvancePlayerPresentation(0.5f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_CrossFadeOverride_SuppressesWindupAndRecoveryTriggerFallbacks()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_CrossFadeOverride_SuppressesWindupAndRecoveryTriggerFallbacks");
            var windupReferenceClip = CreateReferenceClip("WindupReference", 1f);
            var recoverReferenceClip = CreateReferenceClip("RecoverReference", 1f);

            try
            {
                AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: 0.4f,
                    recoverAnimatorDurationSeconds: 0.5f,
                    stateTransitionCrossFadeDurationSeconds: 0.08f,
                    attackWindupReferenceClip: windupReferenceClip,
                    recoverReferenceClip: recoverReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Attack,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2.5f).Within(0.0001f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(GetPrivateInstanceField<int>(driver, "_windupTriggerDispatchCount"), Is.Zero);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: true,
                    startedRecoveryThisTick: true,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Recover"));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
                Assert.That(GetPrivateInstanceField<int>(driver, "_recoveryTriggerDispatchCount"), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(recoverReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_GlidePhaseSignals_RequestDedicatedAnimatorStates()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_GlidePhaseSignals_RequestDedicatedAnimatorStates");
            var windupReferenceClip = CreateReferenceClip("GlideWindupReference", 1f);
            var recoverReferenceClip = CreateReferenceClip("GlideRecoverReference", 1f);

            try
            {
                AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: 0.25f,
                    recoverAnimatorDurationSeconds: 0.25f,
                    stateTransitionCrossFadeDurationSeconds: 0f,
                    attackWindupReferenceClip: windupReferenceClip,
                    recoverReferenceClip: recoverReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Chase,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.None,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false,
                    glidePhase: EnemyGlidePhase.Windup,
                    startedGlideWindupThisTick: true));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Fly_Start"));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.GlideWindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(GetPrivateInstanceField<int>(driver, "_windupTriggerDispatchCount"), Is.Zero);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Chase,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.None,
                    isMoving: true,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false,
                    glidePhase: EnemyGlidePhase.Active,
                    startedGlideActiveThisTick: true));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Fly_Loop"));
                Assert.That(driver.GlideActiveSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 3,
                    aiMode: EnemyAiMode.Chase,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.None,
                    isMoving: true,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false,
                    glidePhase: EnemyGlidePhase.Active));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Fly_Loop"));
                Assert.That(driver.GlideActiveSignalCount, Is.EqualTo(1));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 4,
                    aiMode: EnemyAiMode.Chase,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: true,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false,
                    glidePhase: EnemyGlidePhase.Recovery,
                    startedGlideRecoverThisTick: true));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Fly_Done"));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
                Assert.That(driver.GlideRecoverySignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(GetPrivateInstanceField<int>(driver, "_recoveryTriggerDispatchCount"), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(recoverReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_ChargePhaseSignals_RequestWindupBeforeChargeActive()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_ChargePhaseSignals_RequestWindupBeforeChargeActive");
            var windupReferenceClip = CreateReferenceClip("ChargeWindupReference", 1f);
            var recoverReferenceClip = CreateReferenceClip("ChargeRecoverReference", 1f);

            try
            {
                AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: 1f,
                    recoverAnimatorDurationSeconds: 1f,
                    stateTransitionCrossFadeDurationSeconds: 0.001f,
                    attackWindupReferenceClip: windupReferenceClip,
                    recoverReferenceClip: recoverReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 58,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Charge,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.Windup,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: true,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.ChargeActiveSignalCount, Is.Zero);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 58,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Charge,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.Active,
                    isMoving: true,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: true,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Charge"));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.ChargeActiveSignalCount, Is.EqualTo(1));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 58,
                    tickIndex: 3,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.Recover,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: true,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: true,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Recover"));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(recoverReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpAirborneTopologySuspendPreservesAnimatorState()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyJumpAirborneTopologySuspendPreservesAnimatorState));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Animator.Update(0.02f);

                fixture.Driver.PreserveJumpAirborneAnimatorForTopologySuspend();
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: true);
                fixture.Animator.Update(0.25f);

                var stateInfo = fixture.Animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(stateInfo.shortNameHash, Is.EqualTo(Animator.StringToHash("JumpAirborne")));
                Assert.That(fixture.Driver.HasJumpAirborneTopologySuspendSnapshot, Is.True);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpAirborneActivePauseResumeRestoresAnimatorWithoutVisibilityToggle()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyJumpAirborneActivePauseResumeRestoresAnimatorWithoutVisibilityToggle));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Animator.Update(0.25f);
                var before = fixture.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

                fixture.Driver.PreserveJumpAirborneAnimatorForTopologySuspend();
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: true);
                fixture.Animator.Update(0.75f);
                var suspended = fixture.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);
                fixture.Animator.Update(0f);
                var restored = fixture.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

                Assert.That(suspended, Is.EqualTo(before).Within(0.0001f));
                Assert.That(restored, Is.EqualTo(before).Within(0.0001f));
                Assert.That(fixture.Driver.JumpAirborneRestoreCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpFrontFaceInactiveDoesNotOverrideAirborneAnimator()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyJumpFrontFaceInactiveDoesNotOverrideAirborneAnimator));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Animator.Update(0.02f);

                fixture.Driver.PreserveJumpAirborneAnimatorForTopologySuspend();
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: true);

                Assert.That(fixture.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("JumpAirborne")));
                Assert.That(fixture.Animator.speed, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpAirborneHiddenRebindKeepsPresentationSnapshot()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyJumpAirborneHiddenRebindKeepsPresentationSnapshot));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Animator.Update(0.3f);
                var before = fixture.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

                fixture.Driver.PreserveJumpAirborneAnimatorForTopologySuspend();
                Assert.That(fixture.Driver.HasJumpAirborneTopologySuspendSnapshot, Is.True);
                fixture.Root.SetActive(false);
                fixture.Root.SetActive(true);
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);
                fixture.Animator.Update(0f);

                var restoredState = fixture.Animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(restoredState.shortNameHash, Is.EqualTo(Animator.StringToHash("JumpAirborne")));
                Assert.That(restoredState.normalizedTime, Is.EqualTo(before).Within(0.0001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyHiddenDriverSync_DoesNotMutateInactiveAnimator()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyHiddenDriverSync_DoesNotMutateInactiveAnimator));

            try
            {
                var view = fixture.Root.AddComponent<GameplayEntityView>();
                view.Initialize(40);
                var viewsByEntityId = new Dictionary<int, GameplayEntityView>
                {
                    [40] = view,
                };
                var coordinator = new GameplayAnimationSyncCoordinator();
                coordinator.CacheDrivers(40, view);

                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Animator.Update(0.2f);
                fixture.Root.SetActive(false);

                coordinator.SyncHiddenDrivers(
                    new HashSet<int>(),
                    _ => PlayerViewAnimationState.Idle,
                    (_, _) => 0f,
                    viewsByEntityId);

                Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
                Assert.That(fixture.Driver.JumpAirborneRestoreCount, Is.Zero);
                Assert.That(fixture.Driver.HasJumpAirborneTopologySuspendSnapshot, Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyApplyTickPresentation_InactiveLandingDoesNotCommitMoveCrossFade()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyApplyTickPresentation_InactiveLandingDoesNotCommitMoveCrossFade));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));

                fixture.Root.SetActive(false);
                fixture.Driver.Apply(CreateJumpLandingPresentationState());

                Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
                Assert.That(fixture.Driver.LastCrossFadedStateName, Is.Not.EqualTo("Move"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyApplyTickPresentation_WhenAnimatorBecomesActive_AppliesExpectedState()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyApplyTickPresentation_WhenAnimatorBecomesActive_AppliesExpectedState));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Root.SetActive(false);
                fixture.Driver.Apply(CreateJumpLandingPresentationState());
                Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));

                fixture.Root.SetActive(true);
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);

                Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("Move"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpAirborneRestoreNotOverwrittenByIdleApply()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyJumpAirborneRestoreNotOverwrittenByIdleApply));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Animator.Update(0.35f);
                var before = fixture.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

                fixture.Driver.PreserveJumpAirborneAnimatorForTopologySuspend();
                fixture.Root.SetActive(false);
                fixture.Root.SetActive(true);
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(tickIndex: 2, startedAirborne: false));
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);
                fixture.Animator.Update(0f);

                Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));
                Assert.That(fixture.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    Is.EqualTo(before).Within(0.0001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpAirborneSustainedTickEnsuresJumpAirborneState()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyJumpAirborneSustainedTickEnsuresJumpAirborneState));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Animator.Update(0.2f);
                var beforeSignalCount = fixture.Driver.JumpAirborneSignalCount;

                fixture.Animator.Rebind();
                fixture.Animator.Update(0f);
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(tickIndex: 2, startedAirborne: false));
                fixture.Animator.Update(0f);

                Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(beforeSignalCount));
                Assert.That(fixture.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("JumpAirborne")));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpAirborneRestoreCalledFromSyncRuntimeStateResume()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyJumpAirborneRestoreCalledFromSyncRuntimeStateResume));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Animator.Update(0.2f);
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: true);

                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);

                Assert.That(fixture.Driver.JumpAirborneRestoreCount, Is.EqualTo(1));
                Assert.That(fixture.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("JumpAirborne")));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpAirborneAfterAllApplyFinalStateIsAirborne()
        {
            var fixture = CreateEnemyJumpAnimatorFixture(nameof(EnemyJumpAirborneAfterAllApplyFinalStateIsAirborne));

            try
            {
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(startedAirborne: true));
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: true);
                fixture.Animator.Rebind();
                fixture.Animator.Update(0f);
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);
                fixture.Driver.Apply(CreateJumpAirbornePresentationState(tickIndex: 2, startedAirborne: false));
                fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);
                fixture.Animator.Update(0f);

                Assert.That(fixture.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("JumpAirborne")));
                Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_ResyncFromLastPresentation_ReentersSustainedStatesWithoutSignals()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_ResyncFromLastPresentation_ReentersSustainedStatesWithoutSignals");
            var jumpAirborneReferenceClip = CreateReferenceClip("JumpAirborneReference", 1f);

            try
            {
                AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    recoverAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    stateTransitionCrossFadeDurationSeconds: 0.001f,
                    jumpAirborneAnimatorDurationSeconds: 0.5f,
                    jumpAirborneReferenceClip: jumpAirborneReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 58,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Charge,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.Active,
                    isMoving: true,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: true,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Charge"));
                Assert.That(driver.ChargeActiveSignalCount, Is.EqualTo(1));

                Assert.That(driver.ResyncAnimatorStateFromLastPresentation(), Is.True);

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Charge"));
                Assert.That(driver.ChargeActiveSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 58,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Chase,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.None,
                    isMoving: true,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false,
                    glidePhase: EnemyGlidePhase.Active,
                    startedGlideActiveThisTick: true));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Fly_Loop"));
                Assert.That(driver.GlideActiveSignalCount, Is.EqualTo(1));

                Assert.That(driver.ResyncAnimatorStateFromLastPresentation(), Is.True);

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Fly_Loop"));
                Assert.That(driver.GlideActiveSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 58,
                    tickIndex: 3,
                    aiMode: EnemyAiMode.Patrol,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.Airborne,
                    chargePhase: EnemyChargePhase.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: true,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: false,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));

                Assert.That(driver.ResyncAnimatorStateFromLastPresentation(), Is.True);

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(jumpAirborneReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyAnimatorDriver_ChargePrefabWindupSignal_EntersWindupAnimatorState()
        {
            const string prefabPath = StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_RocketFace.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab at '{prefabPath}'.");

            var instance = UnityEngine.Object.Instantiate(prefab.gameObject);

            try
            {
                var driver = instance.GetComponent<EnemyAnimatorDriver>();
                var animator = instance.GetComponentInChildren<Animator>();

                Assert.That(driver, Is.Not.Null);
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 58,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Charge,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.None,
                    chargePhase: EnemyChargePhase.Windup,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    startedChargeWindupThisTick: true,
                    startedChargeActiveThisTick: false,
                    startedChargeRecoverThisTick: false,
                    tookDamage: false,
                    didDie: false));

                animator.Update(0.02f);

                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).IsName("Windup"),
                    Is.True,
                    "Charge Windup presentation must start the actual Windup animator state.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_StateNameAndClipNameMismatch_UsesReferenceClipLengthForAnimatorSpeed()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_StateNameAndClipNameMismatch_UsesReferenceClipLengthForAnimatorSpeed");
            var windupReferenceClip = CreateReferenceClip("HumanM@Attack1H04_R_Windup", 0.53333336f);
            var recoverReferenceClip = CreateReferenceClip("HumanM@Attack1H04_R_Recover", 0.6333333f);

            try
            {
                var animator = AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(driver, "animator", animator);
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: 1f,
                    recoverAnimatorDurationSeconds: 1f,
                    stateTransitionCrossFadeDurationSeconds: 0.01f,
                    attackWindupReferenceClip: windupReferenceClip,
                    recoverReferenceClip: recoverReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Attack,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(windupReferenceClip.length).Within(0.0001f));
                Assert.That(animator.speed, Is.EqualTo(windupReferenceClip.length).Within(0.0001f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: true,
                    startedRecoveryThisTick: true,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Recover"));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(recoverReferenceClip.length).Within(0.0001f));
                Assert.That(animator.speed, Is.EqualTo(recoverReferenceClip.length).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(recoverReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_DeathDurationOverride_UsesReferenceClipAndInspectorValue()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_DeathDurationOverride_UsesReferenceClipAndInspectorValue");
            var deathReferenceClip = CreateReferenceClip("EnemyDeathReference", 0.75f);

            try
            {
                var animator = AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    recoverAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    stateTransitionCrossFadeDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    deathAnimatorDurationSeconds: 1.5f,
                    deathReferenceClip: deathReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 3,
                    aiMode: EnemyAiMode.Dead,
                    activeActionKind: EnemyActionKind.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: true));

                Assert.That(driver.DeathSignalCount, Is.EqualTo(1));
                Assert.That(driver.DeathPresentationDurationSeconds, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(deathReferenceClip.length / 1.5f).Within(0.0001f));
                Assert.That(animator.speed, Is.EqualTo(deathReferenceClip.length / 1.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(deathReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_DeathPhase_PersistsAcrossVisibilityTailWithoutResettingToDefaultSpeed()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_DeathPhase_PersistsAcrossVisibilityTailWithoutResettingToDefaultSpeed");
            var deathReferenceClip = CreateReferenceClip("EnemyDeathReference", 0.75f);

            try
            {
                var animator = AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    recoverAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    stateTransitionCrossFadeDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    deathAnimatorDurationSeconds: 1.5f,
                    deathReferenceClip: deathReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 3,
                    aiMode: EnemyAiMode.Dead,
                    activeActionKind: EnemyActionKind.None,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: true));

                var initialSpeed = driver.CurrentAnimatorSpeed;
                var initialDuration = driver.CurrentPresentationDurationSeconds;

                driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);

                Assert.That(driver.DeathSignalCount, Is.EqualTo(1));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(initialSpeed).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.Not.EqualTo(1f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(initialDuration).Within(0.0001f));
                Assert.That(animator.speed, Is.EqualTo(initialSpeed).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(deathReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_PlaybackSuppression_OverridesAnimatorSpeedWithoutResettingPresentationState()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_PlaybackSuppression_OverridesAnimatorSpeedWithoutResettingPresentationState");
            var windupReferenceClip = CreateReferenceClip("WindupReference", 0.2f);

            try
            {
                var animator = AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: 0.4f,
                    recoverAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    stateTransitionCrossFadeDurationSeconds: 0.08f,
                    attackWindupReferenceClip: windupReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Attack,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(animator.speed, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));

                driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: true);

                Assert.That(driver.IsPlaybackSuppressed, Is.True);
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(animator.speed, Is.Zero);
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));
                Assert.That(driver.LastPresentationState.TickIndex, Is.EqualTo(1));

                driver.SyncRuntimeState(isVisible: true, isMoving: false, playbackSuppressed: false);

                Assert.That(driver.IsPlaybackSuppressed, Is.False);
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.4f).Within(0.0001f));
                Assert.That(animator.speed, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));
                Assert.That(driver.LastPresentationState.TickIndex, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAnimatorDriver_JumpSignals_UseSeparatePresentationPhases()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_JumpSignals_UseSeparatePresentationPhases");
            var jumpWindupReferenceClip = CreateReferenceClip("EnemyJumpWindupReference", 0.5f);
            var jumpAirborneReferenceClip = CreateReferenceClip("EnemyJumpAirborneReference", 1.5f);

            try
            {
                var animator = AttachEnemyRuntimeAnimator(rootObject);
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    authoring,
                    attackWindupAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    recoverAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                    stateTransitionCrossFadeDurationSeconds: 0.05f,
                    jumpWindupAnimatorDurationSeconds: 0.25f,
                    jumpAirborneAnimatorDurationSeconds: 0.75f,
                    jumpWindupReferenceClip: jumpWindupReferenceClip,
                    jumpAirborneReferenceClip: jumpAirborneReferenceClip);

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Patrol,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.Windup,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: true,
                    startedJumpAirborneThisTick: false,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.JumpWindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.JumpAirborneSignalCount, Is.Zero);
                Assert.That(driver.WindupSignalCount, Is.Zero);
                Assert.That(driver.AttackSignalCount, Is.Zero);
                Assert.That(driver.RecoverySignalCount, Is.Zero);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("JumpWindup"));
                Assert.That(animator.speed, Is.EqualTo(2f).Within(0.0001f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 40,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Patrol,
                    activeActionKind: EnemyActionKind.None,
                    jumpPhase: EnemyJumpPhase.Airborne,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    startedJumpWindupThisTick: false,
                    startedJumpAirborneThisTick: true,
                    landedFromJumpThisTick: false,
                    retryingJumpAirborneThisTick: false,
                    tookDamage: false,
                    didDie: false));

                Assert.That(driver.JumpWindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(1));
                Assert.That(driver.WindupSignalCount, Is.Zero);
                Assert.That(driver.AttackSignalCount, Is.Zero);
                Assert.That(driver.RecoverySignalCount, Is.Zero);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("JumpAirborne"));
                Assert.That(animator.speed, Is.EqualTo(2f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(jumpWindupReferenceClip);
                UnityEngine.Object.DestroyImmediate(jumpAirborneReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultGameplayEntityViewFactory_AiControlledUnit_KeepsEnemyAnimationTimingHookOptional()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_AiControlledUnit_KeepsEnemyAnimationTimingHookOptional");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(parentObject.transform, 1f, playerEntityId: 10);
                var enemyView = factory.CreateView(CreateEnemyEntity());

                Assert.That(enemyView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(enemyView.GetComponent<EnemyAnimationTimingAuthoring>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_WithEnemyPresentationBindingButMissingCatalog_Throws()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_WithEnemyPresentationBindingButMissingCatalog_Throws");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(
                        new GameplaySceneHostConfiguration
                        {
                            AutoAdvanceTicks = false,
                            AutoCreateViews = true,
                            InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                            InitialEntities = new[]
                            {
                                CreatePlayerEntity(),
                                CreateEnemyEntity(),
                            },
                            InitialTopology = new CubeTopologyState(FaceId.Floor),
                            PlayerEntityId = 10,
                            EnemyPresentationBindings = new[]
                            {
                                new EnemyPresentationBinding
                                {
                                    EntityId = 40,
                                    PresentationId = "windup_melee_showcase",
                                },
                            },
                        }));

                StringAssert.Contains(nameof(EnemyPresentationCatalog), exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHost_Initialize_WithEnemyPresentationCatalog_UsesBoundPrefabForConfiguredEnemy()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_WithEnemyPresentationCatalog_UsesBoundPrefabForConfiguredEnemy");
            var enemyPrefabObject = new GameObject("EnemyPresentationPrefab");
            var enemyCatalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            var windupReferenceClip = CreateReferenceClip("WindupReference", 1f);
            var recoverReferenceClip = CreateReferenceClip("RecoverReference", 1f);

            try
            {
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                AttachEnemyRuntimeAnimator(enemyPrefabObject);
                var timingAuthoring = enemyPrefabObject.AddComponent<EnemyAnimationTimingAuthoring>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                ConfigureEnemyAnimationTimingAuthoring(
                    timingAuthoring,
                    attackWindupAnimatorDurationSeconds: 0.35f,
                    recoverAnimatorDurationSeconds: 0.5f,
                    stateTransitionCrossFadeDurationSeconds: 0.08f,
                    attackWindupReferenceClip: windupReferenceClip,
                    recoverReferenceClip: recoverReferenceClip);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    enemyCatalog,
                    "entries",
                    new[]
                    {
                        new EnemyPresentationCatalogEntry
                        {
                            PresentationId = "windup_melee_showcase",
                            ViewPrefab = enemyPrefabView,
                        },
                    });

                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 1)),
                        InitialEntities = new[]
                        {
                            CreatePlayerEntity(),
                            CreateEnemyEntity(entityId: 40, position: new SurfaceCell(FaceId.Floor, 1, 0)),
                            CreateEnemyEntity(entityId: 41, position: new SurfaceCell(FaceId.Floor, 2, 0)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        EnemyPresentationCatalog = enemyCatalog,
                        EnemyPresentationBindings = new[]
                        {
                            new EnemyPresentationBinding
                            {
                                EntityId = 40,
                                PresentationId = "windup_melee_showcase",
                            },
                        },
                    });

                Assert.That(host.ViewRegistry.TryGetView(40, out var boundEnemyView), Is.True);
                Assert.That(boundEnemyView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(boundEnemyView.GetComponent<EnemyAnimationTimingAuthoring>(), Is.Not.Null);
                Assert.That(boundEnemyView.GetComponent<Animator>(), Is.Not.Null);

                var boundTiming = boundEnemyView.GetComponent<EnemyAnimationTimingAuthoring>().CreateSnapshot();
                Assert.That(boundTiming.TryGetAttackWindupAnimatorDurationOverride(out var windupDurationSeconds), Is.True);
                Assert.That(windupDurationSeconds, Is.EqualTo(0.35f));
                Assert.That(
                    boundTiming.TryGetAttackWindupReferenceClipLengthSeconds(
                        out var windupReferenceClipLengthSeconds),
                    Is.True);
                Assert.That(windupReferenceClipLengthSeconds, Is.EqualTo(1f).Within(0.0001f));

                Assert.That(host.ViewRegistry.TryGetView(41, out var fallbackEnemyView), Is.True);
                Assert.That(fallbackEnemyView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(fallbackEnemyView.GetComponent<EnemyAnimationTimingAuthoring>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(windupReferenceClip);
                UnityEngine.Object.DestroyImmediate(recoverReferenceClip);
                UnityEngine.Object.DestroyImmediate(enemyCatalog);
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_WithStaticPresentationBindingButMissingCatalog_Throws()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_WithStaticPresentationBindingButMissingCatalog_Throws");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(
                        new GameplaySceneHostConfiguration
                        {
                            AutoAdvanceTicks = false,
                            AutoCreateViews = true,
                            InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 1)),
                            InitialEntities = new[]
                            {
                                CreatePlayerEntity(),
                                CreateBoxEntity(),
                            },
                            InitialTopology = new CubeTopologyState(FaceId.Floor),
                            PlayerEntityId = 10,
                            StaticEntityPresentationBindings = new[]
                            {
                                new StaticEntityPresentationBinding
                                {
                                    EntityId = 20,
                                    PresentationId = "crate",
                                },
                            },
                        }));

                StringAssert.Contains(nameof(StaticEntityPresentationCatalog), exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHost_Initialize_WithStaticPresentationCatalog_UsesBoundPrefabsAndFallbacks()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_WithStaticPresentationCatalog_UsesBoundPrefabsAndFallbacks");
            var boxPrefabObject = new GameObject("StaticBoxPresentationPrefab");
            var wallPrefabObject = new GameObject("StaticWallPresentationPrefab");
            var staticCatalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();

            try
            {
                var boxPrefabView = boxPrefabObject.AddComponent<GameplayEntityView>();
                boxPrefabObject.AddComponent<BoxCollider>();
                boxPrefabObject.AddComponent<Rigidbody>();
                var boxMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boxMarker.name = "BoxPrefabMarker";
                boxMarker.transform.SetParent(boxPrefabObject.transform, worldPositionStays: false);

                var wallPrefabView = wallPrefabObject.AddComponent<GameplayEntityView>();
                wallPrefabObject.AddComponent<BoxCollider>();
                wallPrefabObject.AddComponent<Rigidbody>();
                var wallMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallMarker.name = "WallPrefabMarker";
                wallMarker.transform.SetParent(wallPrefabObject.transform, worldPositionStays: false);

                PlayerViewPrefabTestUtility.SetSerializedField(
                    staticCatalog,
                    "entries",
                    new[]
                    {
                        new StaticEntityPresentationCatalogEntry
                        {
                            PresentationId = "crate",
                            ViewPrefab = boxPrefabView,
                        },
                        new StaticEntityPresentationCatalogEntry
                        {
                            PresentationId = "wall_block",
                            ViewPrefab = wallPrefabView,
                        },
                    });

                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreatePlayerEntity(),
                            CreateBoxEntity(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0)),
                            CreateWallEntity(entityId: 30, position: new SurfaceCell(FaceId.Floor, 2, 0)),
                            CreateBoxEntity(entityId: 21, position: new SurfaceCell(FaceId.Floor, 3, 0)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityPresentationCatalog = staticCatalog,
                        StaticEntityPresentationBindings = new[]
                        {
                            new StaticEntityPresentationBinding
                            {
                                EntityId = 20,
                                PresentationId = "crate",
                            },
                            new StaticEntityPresentationBinding
                            {
                                EntityId = 30,
                                PresentationId = "wall_block",
                            },
                        },
                    });

                Assert.That(host.ViewRegistry.TryGetView(20, out var boundBoxView), Is.True);
                Assert.That(boundBoxView.transform.Find("BoxPrefabMarker"), Is.Not.Null);
                Assert.That(boundBoxView.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty);
                Assert.That(boundBoxView.GetComponentsInChildren<Rigidbody>(includeInactive: true), Is.Empty);

                Assert.That(host.ViewRegistry.TryGetView(30, out var boundWallView), Is.True);
                Assert.That(boundWallView.transform.Find("WallPrefabMarker"), Is.Not.Null);
                Assert.That(boundWallView.GetComponentsInChildren<Collider>(includeInactive: true), Is.Empty);
                Assert.That(boundWallView.GetComponentsInChildren<Rigidbody>(includeInactive: true), Is.Empty);

                Assert.That(host.ViewRegistry.TryGetView(21, out var fallbackBoxView), Is.True);
                Assert.That(fallbackBoxView.transform.Find("BoxPrefabMarker"), Is.Null);
                Assert.That(fallbackBoxView.ModelRoot.Find("Visual"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staticCatalog);
                UnityEngine.Object.DestroyImmediate(wallPrefabObject);
                UnityEngine.Object.DestroyImmediate(boxPrefabObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_SerializedFields_RemainLogicOnlyContract()
        {
            var serializedFieldNames = typeof(EnemyAiProfile)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field =>
                    (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null) &&
                    field.GetCustomAttribute<HideInInspector>() == null)
                .Select(field => field.Name)
                .OrderBy(name => name)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "brainAuthoring",
                    "capabilityAssets",
                    "coreAuthoring",
                },
                serializedFieldNames);
        }

        [Test]
        [Category("Extended")]
        public void DeathAnimatorDuration_AllowsKnownActionKindsOnly()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(PlayerActionKind.None),
                    nameof(PlayerActionKind.Push),
                    nameof(PlayerActionKind.Flip),
                },
                Enum.GetNames(typeof(PlayerActionKind)));
            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(EnemyActionKind.None),
                    nameof(EnemyActionKind.Melee),
                    nameof(EnemyActionKind.ForwardCellProjectile),
                },
                Enum.GetNames(typeof(EnemyActionKind)));
        }

        private static EntityState CreatePlayerEntity(int hp = 3)
        {
            return new EntityState
            {
                entityId = 10,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = hp,
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

        private static TickPlayerLocomotionPresentationSignal CreateWalkLoopSignal()
        {
            return new TickPlayerLocomotionPresentationSignal(
                10,
                shouldPlayWalkLoop: true,
                moveMotionGeneratedThisTick: true,
                waitingForNextMoveCadence: false);
        }

        private static TickResult CreateEnemyUtilityAnimationTick(
            int tickIndex,
            IEnumerable<TickEnemyUtilityPresentationSignal> utilitySignals,
            IEnumerable<TickEnemyUtilityPhasePresentationState> utilityPhaseStates = null)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { CreateEnemyEntity() },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickPlayerDeathPresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    Array.Empty<FlipImpactPresentationSignal>(),
                    enemyUtilitySignals: utilitySignals,
                    enemyUtilityPhaseStates: utilityPhaseStates),
                string.Empty,
                TickTrace.Empty);
        }

        private static void ApplyPlayerPresentationTick(
            GameplayAnimationSyncCoordinator coordinator,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            int tickIndex,
            IEnumerable<EntityState> finalEntities = null,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals = null,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals = null,
            IEnumerable<TickPlayerActionAttemptPresentationSignal> playerActionAttemptSignals = null,
            IEnumerable<TickPlayerOutcomePresentationSignal> playerOutcomeSignals = null)
        {
            coordinator.ApplyTickPresentation(
                CreatePlayerPresentationTickResult(
                    tickIndex,
                    finalEntities,
                    playerActionSignals,
                    playerLocomotionSignals,
                    playerActionAttemptSignals,
                    playerOutcomeSignals),
                viewsByEntityId,
                (_, _) => 0.4f);
        }

        private static TickResult CreatePlayerPresentationTickResult(
            int tickIndex,
            IEnumerable<EntityState> finalEntities = null,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals = null,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals = null,
            IEnumerable<TickPlayerActionAttemptPresentationSignal> playerActionAttemptSignals = null,
            IEnumerable<TickPlayerOutcomePresentationSignal> playerOutcomeSignals = null)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities ?? new[] { CreatePlayerEntity() },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    playerActionSignals ?? Array.Empty<TickPlayerActionPresentationSignal>(),
                    playerLocomotionSignals ?? Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    playerActionAttemptSignals: playerActionAttemptSignals,
                    playerOutcomeSignals: playerOutcomeSignals),
                string.Empty,
                TickTrace.Empty);
        }

        private static TickResult CreateStageClearVictoryTickResult(int tickIndex)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { CreatePlayerEntity() },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    playerOutcomeSignals: new[]
                    {
                        new TickPlayerOutcomePresentationSignal(
                            10,
                            TickPlayerOutcomePresentationKind.StageClearVictory,
                            sourceTileId: 100,
                            new SurfaceCell(FaceId.Floor, 1, 1)),
                    }),
                string.Empty,
                TickTrace.Empty,
                new StageObjectiveTickResult(
                    hasObjective: true,
                    goalReached: true,
                    allConditionsSatisfied: true,
                    clearedThisTick: true,
                    isCleared: true,
                    Array.Empty<StageConditionStatus>()));
        }

        private static void InvokePresentationFeedTickCompleted(
            GameplayHostPresentationFeed feed,
            TickResult result)
        {
            var method = typeof(GameplayHostPresentationFeed)
                .GetMethod("HandleTickCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(feed, new object[] { result });
        }

        private static void InitializeInputHostForCommandTest(
            GameplayInputHost inputHost,
            GameplayTickViewPresenter presenter)
        {
            var bufferType = typeof(GameplayInputHost).Assembly.GetType("Game.Feature.Gameplay.Host.PlayerMoveIntentBuffer");
            Assert.That(bufferType, Is.Not.Null);

            SetPrivateField(inputHost, "_isInitialized", true);
            SetPrivateField(inputHost, "_playerEntityId", 10);
            SetPrivateField(inputHost, "_presenter", presenter);
            SetPrivateField(inputHost, "_moveIntentBuffer", Activator.CreateInstance(bufferType, 1f));
        }

        private static TickResult CreateActionAttemptTickResult(
            TickPlayerActionAttemptPresentationSignal playerActionAttemptSignal)
        {
            return new TickResult(
                1,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { CreatePlayerEntity() },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    new[] { playerActionAttemptSignal }),
                string.Empty,
                TickTrace.Empty);
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static object InvokeInstanceMethod(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, args);
        }

        private static StageContentEntry CreateStageContentEntry(string stageId)
        {
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.AssignStageId(StageId.CreateOrThrow(stageId));
            return entry;
        }

        private static StageObjectiveRuntimeDefinition CreateSingleCellObjective(SurfaceCell goalCell)
        {
            var zone = new StageZoneRuntimeDefinition(
                "goal",
                goalCell.face,
                new[]
                {
                    new StageZoneRuntimeRegion(goalCell.PlanarPosition, goalCell.PlanarPosition),
                });

            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequireAllConditions,
                10,
                new[] { zone },
                new[]
                {
                    new StageObjectiveConditionRuntimeDefinitionEntry(
                        new PlayerAtAnyZoneConditionRuntimeDefinition(
                            "primary-goal",
                            "Primary Goal",
                            10,
                            new[] { zone },
                            requireAlive: true),
                        required: true,
                        StageObjectiveConditionRole.PrimaryGoal,
                        "primary-goal"),
                });
        }

        private static EntityState CreateEnemyEntity(
            int entityId = 40,
            SurfaceCell? position = null)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position ?? new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Patrol,
                aiStateTimer = 0,
            };
        }

        private static EntityState CreateBoxEntity(
            int entityId = 20,
            SurfaceCell? position = null)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position ?? new SurfaceCell(FaceId.Floor, 1, 0),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.Push,
            };
        }

        private static EntityState CreateWallEntity(
            int entityId = 30,
            SurfaceCell? position = null)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position ?? new SurfaceCell(FaceId.Floor, 2, 0),
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

        private static T GetPrivateInstanceField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void ConfigureEnemyAnimationTimingAuthoring(
            EnemyAnimationTimingAuthoring authoring,
            float attackWindupAnimatorDurationSeconds,
            float recoverAnimatorDurationSeconds,
            float stateTransitionCrossFadeDurationSeconds,
            float jumpWindupAnimatorDurationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
            float jumpAirborneAnimatorDurationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
            float deathAnimatorDurationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
            AnimationClip attackWindupReferenceClip = null,
            AnimationClip recoverReferenceClip = null,
            AnimationClip jumpWindupReferenceClip = null,
            AnimationClip jumpAirborneReferenceClip = null,
            AnimationClip deathReferenceClip = null)
        {
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "attackWindupAnimatorDurationSeconds",
                attackWindupAnimatorDurationSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "jumpWindupAnimatorDurationSeconds",
                jumpWindupAnimatorDurationSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "jumpAirborneAnimatorDurationSeconds",
                jumpAirborneAnimatorDurationSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "recoverAnimatorDurationSeconds",
                recoverAnimatorDurationSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "deathAnimatorDurationSeconds",
                deathAnimatorDurationSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "jumpWindupReferenceClip",
                jumpWindupReferenceClip);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "jumpAirborneReferenceClip",
                jumpAirborneReferenceClip);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "stateTransitionCrossFadeDurationSeconds",
                stateTransitionCrossFadeDurationSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "attackWindupReferenceClip",
                attackWindupReferenceClip);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "recoverReferenceClip",
                recoverReferenceClip);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "deathReferenceClip",
                deathReferenceClip);
        }

        private static AnimationClip CreateReferenceClip(string clipName, float lengthSeconds)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f,
            };

            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, lengthSeconds, 1f));
            return clip;
        }

        private static EnemyJumpAnimatorFixture CreateEnemyJumpAnimatorFixture(string name)
        {
            var root = new GameObject(name);
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyJumpAnimatorControllerPath);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null, EnemyJumpAnimatorControllerPath);

            var referenceClip = CreateReferenceClip($"{name}_JumpAirborneReference", 1f);
            var authoring = root.AddComponent<EnemyAnimationTimingAuthoring>();
            ConfigureEnemyAnimationTimingAuthoring(
                authoring,
                attackWindupAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                recoverAnimatorDurationSeconds: EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
                stateTransitionCrossFadeDurationSeconds: 0f,
                jumpAirborneAnimatorDurationSeconds: 1f,
                jumpAirborneReferenceClip: referenceClip);

            var driver = root.AddComponent<EnemyAnimatorDriver>();
            return new EnemyJumpAnimatorFixture(root, animator, driver, referenceClip);
        }

        private static EnemyViewPresentationState CreateJumpAirbornePresentationState(
            int tickIndex = 1,
            bool startedAirborne = false)
        {
            return new EnemyViewPresentationState(
                entityId: 40,
                tickIndex: tickIndex,
                aiMode: EnemyAiMode.Patrol,
                activeActionKind: EnemyActionKind.None,
                jumpPhase: EnemyJumpPhase.Airborne,
                chargePhase: EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: startedAirborne,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
                tookDamage: false,
                didDie: false);
        }

        private static EnemyViewPresentationState CreateJumpLandingPresentationState(int tickIndex = 2)
        {
            return new EnemyViewPresentationState(
                entityId: 40,
                tickIndex: tickIndex,
                aiMode: EnemyAiMode.Patrol,
                activeActionKind: EnemyActionKind.None,
                jumpPhase: EnemyJumpPhase.None,
                chargePhase: EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: true,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
                tookDamage: false,
                didDie: false);
        }

        private static Animator AttachPlayerRuntimeAnimator(GameObject rootObject, PlayerAnimatorDriver driver)
        {
            var animator = rootObject.AddComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3DM/1Player/Player_S1.controller");
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            PlayerViewPrefabTestUtility.SetSerializedField(driver, "animator", animator);
            return animator;
        }

        private static Animator AttachEnemyRuntimeAnimator(GameObject rootObject)
        {
            return PlayerViewPrefabTestUtility.AttachTestAnimator(
                rootObject,
                $"{rootObject.name}_EnemyAnimator",
                "Move",
                "Windup",
                "Recover",
                "JumpWindup",
                "JumpAirborne",
                "Charge",
                "Fly_Start",
                "Fly_Loop",
                "Fly_Done",
                "Death");
        }

        private readonly struct EnemyJumpAnimatorFixture
        {
            public EnemyJumpAnimatorFixture(
                GameObject root,
                Animator animator,
                EnemyAnimatorDriver driver,
                AnimationClip referenceClip)
            {
                Root = root;
                Animator = animator;
                Driver = driver;
                ReferenceClip = referenceClip;
            }

            public GameObject Root { get; }

            public Animator Animator { get; }

            public EnemyAnimatorDriver Driver { get; }

            public AnimationClip ReferenceClip { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(ReferenceClip);
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (var childState in stateMachine.states)
            {
                if (childState.state != null &&
                    string.Equals(childState.state.name, stateName, StringComparison.Ordinal))
                {
                    return childState.state;
                }
            }

            return null;
        }

        private static bool HasTransition(AnimatorState state, string destinationStateName)
        {
            if (state == null)
            {
                return false;
            }

            foreach (var transition in state.transitions)
            {
                if (transition.destinationState != null &&
                    string.Equals(transition.destinationState.name, destinationStateName, StringComparison.Ordinal) &&
                    transition.hasExitTime)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
