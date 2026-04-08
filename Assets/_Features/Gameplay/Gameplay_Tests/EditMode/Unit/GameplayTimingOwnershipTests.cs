using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTimingOwnershipTests
    {
        [Test]
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
        public void CombinedGameplayShowcase_NonAttackingEnemyProfileAndStartisPrefab_ShareMoveCadence()
        {
            const string enemyProfilePath =
                "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/EnemyAi_NonAttacking.asset";
            const string enemyPrefabPath =
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_Startis.prefab";

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
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(4f).Within(0.0001f));

                driver.SyncRuntimeState(isVisible: true, resolvedState: PlayerViewAnimationState.Flip, resolvedMotionDurationSeconds: 0.5f);
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipWindup));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
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
        public void PlayerAnimatorDriver_FlipRecovery_UsesRecoveryClipLengthForSpeed()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject("PlayerAnimatorDriver_FlipRecovery_UsesRecoveryClipLengthForSpeed");

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
            Assert.That(GetPrivateInstanceField<float>(driver, "stateTransitionCrossFadeDurationSeconds"), Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "pushWindupAnimatorDurationSeconds"), Is.EqualTo(0.18333334f).Within(0.0000001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "pushRecoveryAnimatorDurationSeconds"), Is.EqualTo(0.3f).Within(0.0000001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "flipWindupAnimatorDurationSeconds"), Is.EqualTo(0.38333333f).Within(0.0000001f));
            Assert.That(GetPrivateInstanceField<float>(authoring, "flipRecoveryAnimatorDurationSeconds"), Is.EqualTo(0.56666666f).Within(0.0000001f));
        }

        [Test]
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
        public void EnemyAnimatorDriver_CrossFadeOverride_SuppressesWindupAndRecoveryTriggerFallbacks()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_CrossFadeOverride_SuppressesWindupAndRecoveryTriggerFallbacks");
            var windupReferenceClip = CreateReferenceClip("WindupReference", 1f);
            var recoverReferenceClip = CreateReferenceClip("RecoverReference", 1f);

            try
            {
                rootObject.AddComponent<Animator>();
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
        public void EnemyAnimatorDriver_StateNameAndClipNameMismatch_UsesReferenceClipLengthForAnimatorSpeed()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_StateNameAndClipNameMismatch_UsesReferenceClipLengthForAnimatorSpeed");
            var windupReferenceClip = CreateReferenceClip("HumanM@Attack1H04_R_Windup", 0.53333336f);
            var recoverReferenceClip = CreateReferenceClip("HumanM@Attack1H04_R_Recover", 0.6333333f);

            try
            {
                var animator = rootObject.AddComponent<Animator>();
                var authoring = rootObject.AddComponent<EnemyAnimationTimingAuthoring>();
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
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
        public void EnemyAnimatorDriver_JumpSignals_UseSeparatePresentationPhases()
        {
            var rootObject = new GameObject("EnemyAnimatorDriver_JumpSignals_UseSeparatePresentationPhases");
            var jumpWindupReferenceClip = CreateReferenceClip("EnemyJumpWindupReference", 0.5f);
            var jumpAirborneReferenceClip = CreateReferenceClip("EnemyJumpAirborneReference", 1.5f);

            try
            {
                var animator = rootObject.AddComponent<Animator>();
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
                enemyPrefabObject.AddComponent<Animator>();
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

        private static EntityState CreatePlayerEntity()
        {
            return new EntityState
            {
                entityId = 10,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
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
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Patrol,
                aiStateTimer = 0,
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
            AnimationClip attackWindupReferenceClip = null,
            AnimationClip recoverReferenceClip = null,
            AnimationClip jumpWindupReferenceClip = null,
            AnimationClip jumpAirborneReferenceClip = null)
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
