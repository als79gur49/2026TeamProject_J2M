using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Feature.Gameplay.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayPresentationOrchestrationArchitectureTests
    {
        private const string ContractsDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationContracts/Runtime";
        private const string PlanningDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationPlanning/Runtime";
        private const string PlaybackDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationPlayback/Runtime";
        private const string RuntimeDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationRuntime/Runtime";
        private const string HostRuntimeDirectory =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string CoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
        private const string TopologyExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationExecutor.cs";
        private const string BoxMotionExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs";
        private const string PlayerActionAnimationExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs";
        private const string EnemyPresentationExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs";
        private const string ActionAudioExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs";
        private const string EnemyAudioExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs";

        [Test]
        [Category("Core")]
        public void PresentationOrchestration_Assemblies_FollowReferenceDirection()
        {
            var contractsReferences = GetReferenceNames(typeof(PresentationFactFrame).Assembly);
            var planningReferences = GetReferenceNames(typeof(PresentationCueFrame).Assembly);
            var playbackReferences = GetReferenceNames(typeof(PresentationPlaybackPlan).Assembly);
            var runtimeReferences = GetReferenceNames(typeof(GameplayPresentationPipeline).Assembly);
            var hostReferences = GetReferenceNames(typeof(GameplayTickPresentationCoordinator).Assembly);

            Assert.That(contractsReferences, Does.Contain("Game.Feature.Gameplay"));
            Assert.That(contractsReferences, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(contractsReferences, Does.Not.Contain("Game.Feature.Gameplay.PresentationPlanning"));
            Assert.That(contractsReferences, Does.Not.Contain("Game.Feature.Gameplay.PresentationPlayback"));
            Assert.That(contractsReferences, Does.Not.Contain("Game.Shared.Audio"));
            Assert.That(contractsReferences, Does.Not.Contain("Game.Feature.UI.Application"));

            Assert.That(planningReferences, Does.Contain("Game.Feature.Gameplay.PresentationContracts"));
            Assert.That(planningReferences, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(planningReferences, Does.Not.Contain("Game.Shared.Audio"));
            Assert.That(planningReferences, Does.Not.Contain("Game.Feature.UI.Application"));

            Assert.That(playbackReferences, Does.Contain("Game.Feature.Gameplay.PresentationContracts"));
            Assert.That(playbackReferences, Does.Contain("Game.Feature.Gameplay.PresentationPlanning"));
            Assert.That(playbackReferences, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(playbackReferences, Does.Not.Contain("Game.Shared.Audio"));
            Assert.That(playbackReferences, Does.Not.Contain("Game.Feature.UI.Application"));

            Assert.That(runtimeReferences, Does.Contain("Game.Feature.Gameplay"));
            Assert.That(runtimeReferences, Does.Contain("Game.Feature.Gameplay.PresentationContracts"));
            Assert.That(runtimeReferences, Does.Contain("Game.Feature.Gameplay.PresentationPlanning"));
            Assert.That(runtimeReferences, Does.Contain("Game.Feature.Gameplay.PresentationPlayback"));
            Assert.That(runtimeReferences, Does.Not.Contain("Game.Feature.Gameplay.Host"));

            Assert.That(hostReferences, Does.Contain("Game.Feature.Gameplay.PresentationRuntime"));
        }

        [Test]
        [Category("Core")]
        public void ContractsPlanningPlayback_DoNotReferenceAuthoritativeOrRuntimeExecutionTypes()
        {
            var source = ReadDirectorySource(ContractsDirectory) + "\n" +
                         ReadDirectorySource(PlanningDirectory) + "\n" +
                         ReadDirectorySource(PlaybackDirectory);
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "FinalizationBatch",
                "Committer",
                "AudioManager",
                "GameplayTickPresentationCoordinator",
                "GameplayTopologyTransitionController",
                "GameplayAnimationSyncCoordinator",
                "PlayerAnimatorDriver",
                "GameplayInputHost",
                "UITickEventRouter",
                "UIStateMapper",
                "UIPresentationSnapshot",
                "AnimatorController",
                "AnimationClip",
                "GameObject",
                "Transform",
                "MonoBehaviour",
                "Animator",
                "AudioSource",
                "prefab",
                "pooled",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        [Test]
        [Category("Core")]
        public void FactCueAndPlan_PublicSurface_DoesNotExposeUnityRuntimeObjects()
        {
            var assemblies = new[]
            {
                typeof(PresentationFactFrame).Assembly,
                typeof(PresentationCueFrame).Assembly,
                typeof(PresentationPlaybackPlan).Assembly,
            };
            var forbiddenTypeNames = new HashSet<string>
            {
                "UnityEngine.GameObject",
                "UnityEngine.Transform",
                "UnityEngine.MonoBehaviour",
                "UnityEngine.Animator",
                "UnityEngine.AudioSource",
                "UnityEngine.Camera",
                "UnityEngine.Rendering.Volume",
            };

            var leakedMembers = assemblies
                .SelectMany(assembly => assembly.GetExportedTypes())
                .SelectMany(type => GetPublicSurface(type)
                    .Where(surfaceType => surfaceType != null && forbiddenTypeNames.Contains(surfaceType.FullName))
                    .Select(surfaceType => $"{type.FullName} -> {surfaceType.FullName}"))
                .ToArray();

            Assert.That(leakedMembers, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void UiApplication_DoesNotReferenceRawPresentationOrchestrationFrames()
        {
            var uiApplicationAsmdef = ReadRepoFile("Assets/_Features/UI/UI_Application/UI.Application.asmdef");
            var uiApplicationSource = ReadDirectorySource("Assets/_Features/UI/UI_Application/Runtime");

            Assert.That(uiApplicationAsmdef, Does.Not.Contain("Game.Feature.Gameplay.PresentationContracts"));
            Assert.That(uiApplicationAsmdef, Does.Not.Contain("Game.Feature.Gameplay.PresentationPlanning"));
            Assert.That(uiApplicationAsmdef, Does.Not.Contain("Game.Feature.Gameplay.PresentationPlayback"));
            Assert.That(uiApplicationSource, Does.Contain("GameplayUiPresentationSource"));
            Assert.That(uiApplicationSource, Does.Contain("UITickEventRouter"));
            Assert.That(uiApplicationSource, Does.Contain("UIStateMapper"));
            Assert.That(uiApplicationSource, Does.Contain("UIPresentationSnapshot"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PresentationFactFrame"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PresentationCueFrame"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PresentationPlaybackPlan"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PresentationPlaybackScheduler"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PresentationBlockingSnapshot"));
            Assert.That(uiApplicationSource, Does.Not.Contain("TopologyPresentationOwnershipDiagnostics"));
            Assert.That(uiApplicationSource, Does.Not.Contain("TopologyPresentationExecutionMode"));
            Assert.That(uiApplicationSource, Does.Not.Contain("GameplayMotionExecutorDiagnostics"));
            Assert.That(uiApplicationSource, Does.Not.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(uiApplicationSource, Does.Not.Contain("GameplayAnimationExecutorDiagnostics"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PlayerActionAnimationExecutionMode"));
            Assert.That(uiApplicationSource, Does.Not.Contain("EnemyPresentationExecutionMode"));
        }

        [Test]
        [Category("Core")]
        public void AudioAndBgmOwnership_RemainOutsidePresentationPlaybackSkeleton()
        {
            var playbackSource = ReadDirectorySource(PlaybackDirectory);
            var runtimeSource = ReadDirectorySource(RuntimeDirectory);
            var bgmSource = ReadDirectorySource("Assets/_Features/Flow/Flow_Audio/Runtime");

            Assert.That(playbackSource, Does.Contain("IPresentationSfxBridgeExecutor"));
            Assert.That(playbackSource, Does.Not.Contain("AudioManager"));
            Assert.That(playbackSource, Does.Not.Contain("IAudioService"));
            Assert.That(playbackSource, Does.Not.Contain("Play2D"));
            Assert.That(runtimeSource, Does.Not.Contain("AudioManager"));
            Assert.That(runtimeSource, Does.Not.Contain("PlayPlannedAudio"));
            Assert.That(bgmSource, Does.Contain("Bgm"));
            Assert.That(bgmSource, Does.Not.Contain("GameplayPresentationPipeline"));
        }

        [Test]
        [Category("Core")]
        public void TopologyAndInputLockExistingPath_RemainsOwnedByCoordinator()
        {
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var inputHostSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs");

            Assert.That(coordinatorSource, Does.Contain("GameplayTopologyTransitionController _topologyTransitionController"));
            Assert.That(coordinatorSource, Does.Contain("_topologyTransitionController.RefreshTopologyTrack"));
            Assert.That(coordinatorSource, Does.Contain("_topologyTransitionController.RefreshBoardSurfaceTransition"));
            Assert.That(coordinatorSource, Does.Contain("TopologyPresentationExecutionMode.LegacyCoordinator"));
            Assert.That(coordinatorSource, Does.Contain("TopologyPresentationExecutionMode.ExecutorBridge"));
            Assert.That(coordinatorSource, Does.Contain("ExecuteLegacyTopologyPath"));
            Assert.That(coordinatorSource, Does.Contain("ExecuteExecutorBridgeTopologyPath"));
            Assert.That(coordinatorSource, Does.Contain("public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;"));
            Assert.That(coordinatorSource, Does.Contain("_topologyTransitionController.HasActiveBoardRotationTween"));
            Assert.That(coordinatorSource, Does.Not.Contain("TopologyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("ITopologyTransitionPlaybackPort"));
            Assert.That(inputHostSource, Does.Contain("HasBlockingPresentation"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationPlaybackPlan"));
            Assert.That(inputHostSource, Does.Not.Contain("GameplayPresentationPipeline"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationPlaybackScheduler"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationBlockingSnapshot"));
            Assert.That(inputHostSource, Does.Not.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(inputHostSource, Does.Not.Contain("GameplayMotionPresentationExecutor"));
        }

        [Test]
        [Category("Core")]
        public void TopologyExecutorBoundary_StaysHostOnlyAndDoesNotBecomeDefaultRuntimeOwner()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var runtimeSource = ReadDirectorySource(RuntimeDirectory);
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var topologyExecutorSource = ReadRepoFile(TopologyExecutorPath);
            var coordinatorSource = ReadRepoFile(CoordinatorPath);

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayTopologyTransitionController"));
            Assert.That(runtimeSource, Does.Not.Contain("GameplayTopologyTransitionController"));
            Assert.That(runtimeSource, Does.Not.Contain("TopologyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("TopologyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("ITopologyTransitionPlaybackPort"));
            Assert.That(coordinatorSource, Does.Contain("GameplayHostPresentationPipelineFactory"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("ITopologyTransitionPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutionMode"));
            Assert.That(topologyExecutorSource, Does.Contain("GameplayTopologyTransitionPlaybackPort"));
            Assert.That(topologyExecutorSource, Does.Contain("GameplayTopologyTransitionController controller"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("new GameObject"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("TopologyTransitionPostFxController"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("GameplayCameraRig"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("AudioManager"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("Play2D"));
        }

        [Test]
        [Category("Core")]
        public void BoxMotionExecutorBoundary_StaysHostOnlyAndDoesNotLeakRuntimeObjectsToPlans()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var runtimeSource = ReadDirectorySource(RuntimeDirectory);
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var boxMotionExecutorSource = ReadRepoFile(BoxMotionExecutorPath);
            var coordinatorSource = ReadRepoFile(CoordinatorPath);

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayTrackPlanner"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("PresentationMotionTrack"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("BoxFlipInteractionDriver"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameObject"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("Transform"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(runtimeSource, Does.Not.Contain("GameplayTrackPlanner"));
            Assert.That(runtimeSource, Does.Not.Contain("PresentationMotionTrack"));
            Assert.That(runtimeSource, Does.Not.Contain("BoxFlipInteractionDriver"));
            Assert.That(coordinatorSource, Does.Not.Contain("BoxFlipInteractionDriver"));
            Assert.That(coordinatorSource, Does.Not.Contain("PresentationMotionTrack"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayMotionPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayMotionPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("BoxMotionExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(boxMotionExecutorSource, Does.Contain("GameplayTrackPlanner trackPlanner"));
            Assert.That(boxMotionExecutorSource, Does.Contain("BoxFlipInteractionDriver"));
            Assert.That(boxMotionExecutorSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(boxMotionExecutorSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(boxMotionExecutorSource, Does.Not.Contain("new GameObject"));
            Assert.That(boxMotionExecutorSource, Does.Not.Contain("AudioManager"));
            Assert.That(boxMotionExecutorSource, Does.Not.Contain("Play2D"));
            Assert.That(ReadRepoFile(TopologyExecutorPath), Does.Not.Contain("GameplayInputHost"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime"), Does.Not.Contain("GameplayMotionPresentationExecutor"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime"), Does.Not.Contain("BoxMotionPresentationExecutionMode"));
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimationExecutorBoundary_StaysHostOnlyAndDoesNotLeakRuntimeObjectsToPlans()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var runtimeSource = ReadDirectorySource(RuntimeDirectory);
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var animationExecutorSource = ReadRepoFile(PlayerActionAnimationExecutorPath);
            var coordinatorSource = ReadRepoFile(CoordinatorPath);

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayAnimationSyncCoordinator"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("PlayerAnimatorDriver"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AnimatorController"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AnimationClip"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameObject"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("Transform"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(runtimeSource, Does.Not.Contain("GameplayAnimationSyncCoordinator"));
            Assert.That(runtimeSource, Does.Not.Contain("PlayerAnimatorDriver"));
            Assert.That(coordinatorSource, Does.Contain("PlayerActionAnimationExecutionMode.LegacyAnimationSync"));
            Assert.That(coordinatorSource, Does.Contain("PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor"));
            Assert.That(coordinatorSource, Does.Contain("suppressLegacyPlayerActionAnimations"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayAnimationPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayAnimationPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("PlayerActionAnimationExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Contain("PlayerActionAnimationExecutionMode"));
            Assert.That(animationExecutorSource, Does.Contain("GameplayAnimationSyncPlaybackPort"));
            Assert.That(animationExecutorSource, Does.Contain("GameplayAnimationSyncCoordinator animationSync"));
            Assert.That(animationExecutorSource, Does.Contain("ExecuteCueMappedToLegacyCommand"));
            Assert.That(animationExecutorSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(animationExecutorSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(animationExecutorSource, Does.Not.Contain("new GameObject"));
            Assert.That(animationExecutorSource, Does.Not.Contain("AudioManager"));
            Assert.That(animationExecutorSource, Does.Not.Contain("Play2D"));
            Assert.That(animationExecutorSource, Does.Not.Contain("GameplayActionAudioPresentationController"));
            Assert.That(animationExecutorSource, Does.Not.Contain("GameplayMotionPresentationExecutor"));
            Assert.That(animationExecutorSource, Does.Not.Contain("TopologyPresentationExecutor"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime"), Does.Not.Contain("GameplayAnimationPresentationExecutor"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Audio/Runtime"), Does.Not.Contain("GameplayAnimationPresentationExecutor"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime"), Does.Not.Contain("PlayerActionAnimationExecutionMode"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime"), Does.Not.Contain("PresentationAnimationCueKey"));
        }

        [Test]
        [Category("Core")]
        public void ActionAudioPlanningBoundary_StaysPlanningOnlyAndDoesNotAbsorbPlaybackOwnership()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var planningSource = ReadDirectorySource(PlanningDirectory);
            var playbackSource = ReadDirectorySource(PlaybackDirectory);
            var uiSource = ReadDirectorySource("Assets/_Features/UI");

            Assert.That(contractsPlanningPlaybackSource, Does.Contain("PresentationActionAudioPayload"));
            Assert.That(planningSource, Does.Contain("PresentationActionAudioCueKey"));
            Assert.That(planningSource, Does.Contain("ActionAudioCuePlanner"));
            Assert.That(playbackSource, Does.Contain("ActionAudioNoPlaybackBecausePlanningOnlyCount"));

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayActionAudioPresentationController"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayActionAudioProfile"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayActionAudioAuthoring"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioManager"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioBinding"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioClip"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioSource"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("Play2D"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("PlayAttached"));

            Assert.That(planningSource, Does.Not.Contain("PresentationSfxCueKey.PlayerPush"));
            Assert.That(planningSource, Does.Not.Contain("PresentationSfxCueKey.PlayerFlip"));
            Assert.That(uiSource, Does.Not.Contain("PresentationActionAudioPayload"));
            Assert.That(uiSource, Does.Not.Contain("PresentationActionAudioCueKey"));
            Assert.That(uiSource, Does.Not.Contain("ActionAudioCuePlanner"));
            Assert.That(uiSource, Does.Not.Contain("ActionAudioExecutionMode"));
            Assert.That(uiSource, Does.Not.Contain("GameplayActionAudioExecutorDiagnostics"));
        }

        [Test]
        [Category("Core")]
        public void ActionAudioBridgeExecutorBoundary_StaysHostOnlyAndDoesNotBecomeDefaultOwner()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var runtimeSource = ReadDirectorySource(RuntimeDirectory);
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var actionAudioExecutorSource = ReadRepoFile(ActionAudioExecutorPath);
            var coordinatorSource = ReadRepoFile(CoordinatorPath);

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayActionAudioPresentationController"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayActionAudioProfile"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayActionAudioAuthoring"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioManager"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioBinding"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioClip"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioSource"));
            Assert.That(runtimeSource, Does.Not.Contain("GameplayActionAudioPresentationController"));
            Assert.That(runtimeSource, Does.Not.Contain("GameplayActionAudioPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Contain("ActionAudioExecutionMode.LegacyActionAudioController"));
            Assert.That(coordinatorSource, Does.Contain("ActionAudioExecutionMode.OrchestrationActionAudioBridge"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioPresentationController.ReplacePendingPlan"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayActionAudioPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayActionAudioPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("ActionAudioExecutionGuard"));
            Assert.That(actionAudioExecutorSource, Does.Contain("GameplayActionAudioPlaybackPortAdapter"));
            Assert.That(actionAudioExecutorSource, Does.Contain("GameplayActionAudioPresentationController controller"));
            Assert.That(actionAudioExecutorSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(actionAudioExecutorSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(actionAudioExecutorSource, Does.Not.Contain("new GameObject"));
            Assert.That(actionAudioExecutorSource, Does.Not.Contain("AudioManager"));
            Assert.That(actionAudioExecutorSource, Does.Not.Contain("IAudioService"));
            Assert.That(actionAudioExecutorSource, Does.Not.Contain("PresentationSfxCueKey"));
            Assert.That(actionAudioExecutorSource, Does.Not.Contain("PresentationAnimationCueKey"));
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationExecutorBoundary_StaysHostOnlyAndDoesNotLeakRuntimeObjectsToPlans()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var runtimeSource = ReadDirectorySource(RuntimeDirectory);
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var enemyExecutorSource = ReadRepoFile(EnemyPresentationExecutorPath);
            var coordinatorSource = ReadRepoFile(CoordinatorPath);

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("EnemyViewPresentationMapper"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("EnemyAnimatorDriver"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AnimatorController"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AnimationClip"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameObject"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("Transform"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(runtimeSource, Does.Not.Contain("EnemyViewPresentationMapper"));
            Assert.That(runtimeSource, Does.Not.Contain("EnemyAnimatorDriver"));
            Assert.That(coordinatorSource, Does.Contain("EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper"));
            Assert.That(coordinatorSource, Does.Contain("EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Contain("suppressLegacyEnemyPresentationAnimations"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayEnemyPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayEnemyPresentationPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("EnemyPresentationExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Contain("EnemyPresentationExecutionMode"));
            Assert.That(enemyExecutorSource, Does.Contain("GameplayEnemyPresentationSyncPlaybackPort"));
            Assert.That(enemyExecutorSource, Does.Contain("GameplayAnimationSyncCoordinator animationSync"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("new GameObject"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("AudioManager"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("Play2D"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("EnemyAudioPresentationController"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime"), Does.Not.Contain("EnemyPresentationExecutionMode"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime"), Does.Not.Contain("GameplayEnemyPresentationExecutor"));
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuildResult_DoesNotGainPresentationRuntimeBindings()
        {
            var buildResultSource = ReadRepoFile("Assets/_Features/Stages/Runtime/StageRuntimeBuildResult.cs");
            var presentationDefinitionSource =
                ReadRepoFile("Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs");
            var uiApplicationSource = ReadDirectorySource("Assets/_Features/UI/UI_Application/Runtime");

            Assert.That(buildResultSource, Does.Not.Contain("GameplayPresentationPipeline"));
            Assert.That(buildResultSource, Does.Not.Contain("PresentationPlaybackPlan"));
            Assert.That(buildResultSource, Does.Not.Contain("PresentationRuntime"));
            Assert.That(presentationDefinitionSource, Does.Contain("StagePresentationDefinition : StageCompanionDefinitionBase"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PresentationPlaybackPlan"));
        }

        [Test]
        [Category("Core")]
        public void EmptyPipeline_NoOpLifecycle_DoesNotThrowAndReportsAccept()
        {
            var facts = PresentationFactFrame.Empty(tickIndex: 12);
            var cueFrame = new PresentationCuePlannerSet().Plan(facts);
            var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var scheduler = new PresentationPlaybackScheduler();

            scheduler.Accept(playbackPlan);
            scheduler.Update(0f);
            scheduler.ResetSession();
            scheduler.Accept(playbackPlan);
            scheduler.HardCleanup();

            Assert.That(cueFrame.Cues, Is.Empty);
            Assert.That(playbackPlan.Cues, Is.Empty);
            Assert.That(playbackPlan.Tracks, Is.Empty);
            Assert.That(playbackPlan.Barriers, Is.Empty);
            Assert.That(scheduler.HasBlockingPresentation, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void TopologyFactExtraction_NormalizesTopologyMotionAsTypedSemanticFact()
        {
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var extractor = new TickPresentationFactExtractor();

            var emptyFrame = extractor.Extract(CreateDiagnosticTickResult(includeTopologyMotion: false));
            var topologyFrame = extractor.Extract(CreateDiagnosticTickResult(
                new TickTopologyMotion(sourceTopology, destinationTopology, CubeRotationKind.Forward)));

            Assert.That(emptyFrame.Diagnostics.TopologyFactCount, Is.Zero);
            Assert.That(emptyFrame.Facts.Any(fact => fact.Kind == PresentationFactKind.Topology), Is.False);

            var topologyFacts = topologyFrame.Facts
                .Where(fact => fact.Kind == PresentationFactKind.Topology)
                .ToArray();
            Assert.That(topologyFacts, Has.Length.EqualTo(1));
            Assert.That(topologyFrame.Diagnostics.TopologyFactCount, Is.EqualTo(1));

            var fact = topologyFacts[0];
            Assert.That(fact.Source.TickIndex, Is.EqualTo(topologyFrame.TickIndex));
            Assert.That(fact.Source.SemanticSource, Is.EqualTo(PresentationSemanticSource.TopologyMotion));
            Assert.That(fact.Target.Kind, Is.EqualTo(PresentationTargetKind.Topology));
            Assert.That(fact.TopologyPayload.SourceTopology, Is.EqualTo(sourceTopology));
            Assert.That(fact.TopologyPayload.DestinationTopology, Is.EqualTo(destinationTopology));
            Assert.That(fact.TopologyPayload.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(fact.TopologyPayload.SourceTickIndex, Is.EqualTo(topologyFrame.TickIndex));
            Assert.That(fact.TopologyPayload.HasSourceMetadata, Is.False);
        }

        [Test]
        [Category("Core")]
        public void TopologyCuePlanner_UsesDomainLocalTypedCueKeyAndSymbolicTopologyAnchor()
        {
            var factFrame = new TickPresentationFactExtractor().Extract(CreateDiagnosticTickResult(
                new TickTopologyMotion(
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward)));
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new TopologyCuePlanner(),
            }).Plan(factFrame);

            Assert.That(cueFrame.Cues, Has.Count.EqualTo(1));
            var cue = cueFrame.Cues[0];
            Assert.That(cue.Domain, Is.EqualTo(PresentationDomain.Topology));
            Assert.That(cue.Key.Domain, Is.EqualTo(PresentationDomain.Topology));
            Assert.That(cue.Key.LocalKey, Is.EqualTo((int)PresentationTopologyCueKey.Transition));
            Assert.That(cue.Key.VariantKey, Is.Zero);
            Assert.That(cue.Target.Kind, Is.EqualTo(PresentationTargetKind.Topology));
            Assert.That(cue.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.TopologyOrbit));
            Assert.That(cue.PolicyHint.Kind, Is.EqualTo(PresentationPlaybackPolicyHintKind.Track));
            Assert.That(cue.PolicyHint.Blocking, Is.True);
            Assert.That(cue.TopologyPayload.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
        }

        [Test]
        [Category("Core")]
        public void TopologyPlaybackPlanner_CreatesBlockingTrackAndTopologyOwnedBarrier()
        {
            var factFrame = new TickPresentationFactExtractor().Extract(CreateDiagnosticTickResult(
                new TickTopologyMotion(
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward)));
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new TopologyCuePlanner(),
            }).Plan(factFrame);
            var plan = new PresentationPlaybackPlanner().Plan(cueFrame);

            Assert.That(plan.Tracks, Has.Count.EqualTo(1));
            Assert.That(plan.Barriers, Has.Count.EqualTo(1));
            Assert.That(plan.Cues, Is.Empty);
            Assert.That(plan.Tracks[0].Cue.Domain, Is.EqualTo(PresentationDomain.Topology));
            Assert.That(plan.Tracks[0].Policy.Blocking, Is.True);
            Assert.That(plan.Tracks[0].Cue.TopologyPayload.SourceTopology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(plan.Tracks[0].Cue.TopologyPayload.DestinationTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(plan.Tracks[0].Cue.TopologyPayload.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(plan.Barriers[0].OwnerDomain, Is.EqualTo(PresentationDomain.Topology));
            Assert.That(plan.Barriers[0].Blocking, Is.True);
            Assert.That(plan.Barriers[0].BarrierKey, Is.EqualTo((int)PresentationTopologyCueKey.Transition));
            Assert.That(plan.Diagnostics.TopologyCueCount, Is.EqualTo(1));
            Assert.That(plan.Diagnostics.TopologyTrackCount, Is.EqualTo(1));
            Assert.That(plan.Diagnostics.TopologyBarrierCount, Is.EqualTo(1));
            Assert.That(plan.Diagnostics.BlockingBarrierCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TopologySchedulerDiagnostics_RemainNoOpAndDoNotOwnInputBlocking()
        {
            var pipeline = GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();
            var result = CreateDiagnosticTickResult(new TickTopologyMotion(
                new CubeTopologyState(FaceId.Floor),
                new CubeTopologyState(FaceId.Front),
                CubeRotationKind.Forward));

            pipeline.Present(result);
            pipeline.Update(0f);

            Assert.That(pipeline.LastFactFrame.Diagnostics.TopologyFactCount, Is.EqualTo(1));
            Assert.That(pipeline.LastCueFrame.Cues.Count(cue => cue.Domain == PresentationDomain.Topology), Is.EqualTo(1));
            Assert.That(pipeline.LastPlaybackPlan.Tracks.Count(track => track.Cue.Domain == PresentationDomain.Topology), Is.EqualTo(1));
            Assert.That(pipeline.LastPlaybackPlan.Barriers.Count(barrier => barrier.OwnerDomain == PresentationDomain.Topology), Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.TopologyCueCount, Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.TopologyTrackCount, Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.TopologyBarrierCount, Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.BlockingBarrierCount, Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.NoOpSchedulerAcceptCount, Is.EqualTo(1));
            Assert.That(pipeline.HasBlockingPresentation, Is.False);
            Assert.That(pipeline.BlockingSnapshot.HasPlannedBlockingBarrier, Is.True);
            Assert.That(pipeline.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            Assert.That(pipeline.BlockingSnapshot.PlannedBlockingBarrierCount, Is.EqualTo(1));
            Assert.That(pipeline.BlockingSnapshot.ActiveBlockingSourceCount, Is.Zero);
            Assert.That(pipeline.BlockingSnapshot.TopologyPlannedBarrierCount, Is.EqualTo(1));
            Assert.That(pipeline.BlockingSnapshot.TopologyActiveBlockingCount, Is.Zero);
            Assert.That(
                pipeline.BlockingSnapshot.LastReason,
                Is.EqualTo(PresentationBlockingReason.TopologyTransitionPlanned));
            Assert.That(pipeline.BlockingSnapshot.LastOwnerDomain, Is.EqualTo(PresentationDomain.Topology));
            Assert.That(pipeline.BlockingSnapshot.Sources, Has.Count.EqualTo(1));
            Assert.That(
                pipeline.BlockingSnapshot.Sources[0].Source,
                Is.EqualTo(PresentationBlockingSource.TopologyTransition));

            pipeline.ResetSession();
            Assert.That(pipeline.CurrentDiagnostics.NoOpSchedulerAcceptCount, Is.Zero);
            Assert.That(pipeline.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(pipeline.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            pipeline.HardCleanup();
            Assert.That(pipeline.HasBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void DiagnosticsOnlyPipeline_PreservesTickResultAuthoritativeOutputs()
        {
            var result = CreateDiagnosticTickResult();
            var initialHash = result.DeterminismHash;
            var initialEntities = result.FinalEntities.ToArray();
            var initialEventLog = result.EventLog.ToArray();
            var initialObjective = result.ObjectiveResult;
            var pipeline = GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();

            pipeline.Present(result);
            pipeline.Update(0f);

            Assert.That(result.DeterminismHash, Is.EqualTo(initialHash));
            Assert.That(result.FinalEntities, Is.EqualTo(initialEntities));
            Assert.That(result.EventLog, Is.EqualTo(initialEventLog));
            Assert.That(result.ObjectiveResult, Is.SameAs(initialObjective));
            Assert.That(pipeline.CurrentDiagnostics.ExtractedFactCount, Is.GreaterThan(0));
            Assert.That(pipeline.CurrentDiagnostics.TopologyCueCount, Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.TopologyTrackCount, Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.TopologyBarrierCount, Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.BlockingBarrierCount, Is.EqualTo(1));
            Assert.That(pipeline.CurrentDiagnostics.NoOpSchedulerAcceptCount, Is.EqualTo(1));
            Assert.That(pipeline.HasBlockingPresentation, Is.False);
            Assert.That(pipeline.BlockingSnapshot.HasPlannedBlockingBarrier, Is.True);
            Assert.That(pipeline.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);

            pipeline.ObserveTopologyActiveState(true, result.TickIndex);

            Assert.That(result.DeterminismHash, Is.EqualTo(initialHash));
            Assert.That(result.FinalEntities, Is.EqualTo(initialEntities));
            Assert.That(result.EventLog, Is.EqualTo(initialEventLog));
            Assert.That(result.ObjectiveResult, Is.SameAs(initialObjective));
            Assert.That(pipeline.BlockingSnapshot.HasPlannedBlockingBarrier, Is.True);
            Assert.That(pipeline.BlockingSnapshot.HasActiveBlockingPresentation, Is.True);
            Assert.That(pipeline.BlockingSnapshot.PlannedBlockingBarrierCount, Is.EqualTo(1));
            Assert.That(pipeline.BlockingSnapshot.ActiveBlockingSourceCount, Is.EqualTo(1));
            Assert.That(pipeline.BlockingSnapshot.TopologyPlannedBarrierCount, Is.EqualTo(1));
            Assert.That(pipeline.BlockingSnapshot.TopologyActiveBlockingCount, Is.EqualTo(1));
            Assert.That(
                pipeline.BlockingSnapshot.LastReason,
                Is.EqualTo(PresentationBlockingReason.TopologyTransitionActive));
            Assert.That(pipeline.HasBlockingPresentation, Is.True);

            pipeline.ObserveTopologyActiveState(false, result.TickIndex);

            Assert.That(pipeline.BlockingSnapshot.HasPlannedBlockingBarrier, Is.True);
            Assert.That(pipeline.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            Assert.That(pipeline.HasBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void TopologyExecutor_IsolatedRouting_PreservesTickResultAuthoritativeOutputs()
        {
            var result = CreateDiagnosticTickResult();
            var initialHash = result.DeterminismHash;
            var initialEntities = result.FinalEntities.ToArray();
            var initialEventLog = result.EventLog.ToArray();
            var initialObjective = result.ObjectiveResult;
            var pipeline = GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();
            var port = new RecordingTopologyTransitionPlaybackPort();
            var guard = new TopologyPresentationExecutionGuard(TopologyPresentationExecutionMode.ExecutorBridge);
            var executor = new TopologyPresentationExecutor(
                port,
                TopologyPresentationExecutionMode.ExecutorBridge,
                guard);

            pipeline.Present(result);
            executor.Play(pipeline.LastPlaybackPlan);

            Assert.That(port.BeginOrRefreshCallCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.RouteCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(result.DeterminismHash, Is.EqualTo(initialHash));
            Assert.That(result.FinalEntities, Is.EqualTo(initialEntities));
            Assert.That(result.EventLog, Is.EqualTo(initialEventLog));
            Assert.That(result.ObjectiveResult, Is.SameAs(initialObjective));
            Assert.That(pipeline.HasBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Coordinator_DiagnosticsPipeline_IsDisabledByDefault()
        {
            var coordinator = new GameplayTickPresentationCoordinator();

            Assert.That(coordinator.IsPresentationPipelineDiagnosticsEnabled, Is.False);
            Assert.That(coordinator.PresentationPipelineNoOpSchedulerAcceptCount, Is.Zero);
            Assert.That(coordinator.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.LegacyCoordinator));
            Assert.That(coordinator.TopologyPresentationOwnershipDiagnostics.Mode, Is.EqualTo(TopologyPresentationExecutionMode.LegacyCoordinator));
            Assert.That(coordinator.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
            Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.Mode, Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
            Assert.That(coordinator.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
            Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.Mode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
            Assert.That(new GameplaySceneHostConfiguration().TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.LegacyCoordinator));
        }

        [Test]
        [Category("Core")]
        public void TopologyExecutionSwitch_DoesNotPromoteSchedulerOrExecutorDiagnosticsToInputLock()
        {
            var inputHostSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs");
            var presenterSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs");
            var coordinatorSource = ReadRepoFile(CoordinatorPath);

            Assert.That(inputHostSource, Does.Contain("_presenter.HasBlockingPresentation"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationPlaybackScheduler"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationPlaybackPlan"));
            Assert.That(inputHostSource, Does.Not.Contain("GameplayPresentationPipeline"));
            Assert.That(inputHostSource, Does.Not.Contain("TopologyPresentationOwnershipDiagnostics"));
            Assert.That(inputHostSource, Does.Not.Contain("TopologyPresentationExecutionMode"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationBlockingSnapshot"));
            Assert.That(presenterSource, Does.Contain("HasBlockingPresentation => _presentationCoordinator.HasBlockingPresentation"));
            Assert.That(coordinatorSource, Does.Contain("_topologyTransitionController.HasActiveBoardRotationTween"));
            Assert.That(coordinatorSource, Does.Not.Contain("HasBlockingPresentation => _presentationPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("HasBlockingPresentation => _topologyExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("HasBlockingPresentation => PresentationPipelineBlockingSnapshot"));
            Assert.That(coordinatorSource, Does.Not.Contain("HasBlockingPresentation => TopologyExecutionPipelineBlockingSnapshot"));
        }

        [Test]
        [Category("Core")]
        public void VfxFactExtraction_ObservesDamageAndEnemyDeathAsSemanticFacts()
        {
            var frame = new TickPresentationFactExtractor().Extract(
                CreateDiagnosticTickResult(includeEnemyDeathExit: true));

            var enemyDamageFact = frame.Facts.Single(fact =>
                fact.Kind == PresentationFactKind.Combat &&
                fact.Source.SemanticSource == PresentationSemanticSource.EnemyDamage);
            var enemyDeathFact = frame.Facts.Single(fact =>
                fact.Kind == PresentationFactKind.EntityLifecycle &&
                fact.Source.SemanticSource == PresentationSemanticSource.EntityExit &&
                fact.Target.Kind == PresentationTargetKind.Entity &&
                fact.Target.EntityId == 30);

            Assert.That(enemyDamageFact.Source.TickIndex, Is.EqualTo(7));
            Assert.That(enemyDamageFact.Source.SourceEntityId, Is.EqualTo(20));
            Assert.That(enemyDamageFact.Target.Kind, Is.EqualTo(PresentationTargetKind.Entity));
            Assert.That(enemyDamageFact.Target.EntityId, Is.EqualTo(20));
            Assert.That(enemyDamageFact.Payload.PrimaryValue, Is.EqualTo(2));
            Assert.That(enemyDeathFact.Source.TickIndex, Is.EqualTo(7));
            Assert.That(enemyDeathFact.Source.SourceEntityId, Is.EqualTo(30));
            Assert.That(enemyDeathFact.Source.SourceActionKind, Is.EqualTo((int)TickEntityExitCause.Killed));
            Assert.That(enemyDeathFact.Payload.PrimaryValue, Is.EqualTo((int)TickEntityExitCause.Killed));
            Assert.That(enemyDeathFact.Payload.SecondaryValue, Is.EqualTo((int)EntityType.Unit));
            Assert.That(enemyDeathFact.Payload.HasPrimaryCell, Is.True);
            Assert.That(frame.Diagnostics.CombatFactCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(frame.Diagnostics.LifecycleFactCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void VfxCuePlanner_UsesTypedLocalKeysAndSymbolicAnchors()
        {
            var cueFrame = CreateVfxCueFrame(includeEnemyDeathExit: true);

            var damageCue = cueFrame.Cues.Single(cue =>
                cue.Domain == PresentationDomain.Vfx &&
                cue.Key.TryGetVfxCueKey(out var key) &&
                key == PresentationVfxCueKey.DamageHit);
            var deathCue = cueFrame.Cues.Single(cue =>
                cue.Domain == PresentationDomain.Vfx &&
                cue.Key.TryGetVfxCueKey(out var key) &&
                key == PresentationVfxCueKey.EnemyDeath);

            Assert.That(damageCue.Key.Domain, Is.EqualTo(PresentationDomain.Vfx));
            Assert.That(damageCue.Target.Kind, Is.EqualTo(PresentationTargetKind.Entity));
            Assert.That(damageCue.Target.EntityId, Is.EqualTo(20));
            Assert.That(damageCue.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityCenter));
            Assert.That(damageCue.PolicyHint.Kind, Is.EqualTo(PresentationPlaybackPolicyHintKind.OneShot));
            Assert.That(damageCue.PolicyHint.Blocking, Is.False);
            Assert.That(damageCue.PolicyHint.DedupeKey, Is.GreaterThan(0));

            Assert.That(deathCue.Key.Domain, Is.EqualTo(PresentationDomain.Vfx));
            Assert.That(deathCue.Target.Kind, Is.EqualTo(PresentationTargetKind.Entity));
            Assert.That(deathCue.Target.EntityId, Is.EqualTo(30));
            Assert.That(deathCue.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.SurfaceCellCenter));
            Assert.That(deathCue.Anchor.Target.Kind, Is.EqualTo(PresentationTargetKind.SurfaceCell));
            Assert.That(deathCue.PolicyHint.Kind, Is.EqualTo(PresentationPlaybackPolicyHintKind.OneShot));
            Assert.That(deathCue.PolicyHint.Blocking, Is.False);
        }

        [Test]
        [Category("Core")]
        public void VfxPlaybackPlanner_CreatesNonBlockingOneShotCues()
        {
            var plan = new PresentationPlaybackPlanner().Plan(CreateVfxCueFrame(includeEnemyDeathExit: true));

            Assert.That(
                plan.Cues.Count(cue => cue.Cue.Domain == PresentationDomain.Vfx),
                Is.EqualTo(2));
            Assert.That(plan.Tracks.Any(track => track.Cue.Domain == PresentationDomain.Vfx), Is.False);
            Assert.That(plan.Barriers.Any(barrier => barrier.OwnerDomain == PresentationDomain.Vfx), Is.False);
            Assert.That(plan.Cues.All(cue => !cue.Policy.Blocking), Is.True);
            Assert.That(plan.Cues.All(cue => cue.Policy.UnitKind == PresentationPlaybackUnitKind.OneShot), Is.True);
            Assert.That(plan.Diagnostics.BlockingBarrierCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void SfxCuePlanner_UsesTypedLocalKeys_ForCoreGameplayDamageAndExit()
        {
            var cueFrame = CreateAllCoreSfxCueFrame();

            var sfxKeys = cueFrame.Cues
                .Where(cue => cue.Domain == PresentationDomain.Sfx)
                .Select(cue =>
                {
                    Assert.That(cue.Key.TryGetSfxCueKey(out var key), Is.True);
                    Assert.That(cue.Key.TryGetVfxCueKey(out _), Is.False);
                    return key;
                })
                .OrderBy(key => key)
                .ToArray();

            Assert.That(sfxKeys, Is.EqualTo(new[]
            {
                PresentationSfxCueKey.PlayerDamage,
                PresentationSfxCueKey.EnemyDamage,
                PresentationSfxCueKey.EntityExitItemConsume,
                PresentationSfxCueKey.EntityExitBoxDestroy,
                PresentationSfxCueKey.EntityExitEnemyDeath,
                PresentationSfxCueKey.EntityExitOutOfBounds,
            }));
            Assert.That(cueFrame.Cues.All(cue => cue.PolicyHint.Kind == PresentationPlaybackPolicyHintKind.OneShot), Is.True);
            Assert.That(cueFrame.Cues.All(cue => !cue.PolicyHint.Blocking), Is.True);
            Assert.That(cueFrame.Cues.All(cue => cue.PolicyHint.DedupeKey > 0), Is.True);
            Assert.That(
                cueFrame.Cues.Single(cue => cue.Key.TryGetSfxCueKey(out var key) &&
                                            key == PresentationSfxCueKey.EntityExitBoxDestroy)
                    .SfxPayload.EntityType,
                Is.EqualTo((int)EntityType.Box));
            Assert.That(
                cueFrame.Cues.Single(cue => cue.Key.TryGetSfxCueKey(out var key) &&
                                            key == PresentationSfxCueKey.EntityExitEnemyDeath)
                    .SfxPayload.SourceActorEntityId,
                Is.EqualTo(10));
        }

        [Test]
        [Category("Core")]
        public void SfxPlaybackPlanner_CreatesNonBlockingOneShotCuesWithoutTracksOrBarriers()
        {
            var plan = new PresentationPlaybackPlanner().Plan(CreateAllCoreSfxCueFrame());

            Assert.That(plan.Cues.Count(cue => cue.Cue.Domain == PresentationDomain.Sfx), Is.EqualTo(6));
            Assert.That(plan.Tracks.Any(track => track.Cue.Domain == PresentationDomain.Sfx), Is.False);
            Assert.That(plan.Barriers.Any(barrier => barrier.OwnerDomain == PresentationDomain.Sfx), Is.False);
            Assert.That(plan.Cues.All(cue => !cue.Policy.Blocking), Is.True);
            Assert.That(plan.Cues.All(cue => cue.Policy.UnitKind == PresentationPlaybackUnitKind.OneShot), Is.True);
            Assert.That(plan.Cues.All(cue => cue.Policy.InterruptMode == PresentationPlaybackInterruptMode.AllowOverlap), Is.True);
            Assert.That(plan.Diagnostics.BlockingBarrierCount, Is.Zero);

            var scheduler = new PresentationPlaybackScheduler();
            scheduler.Accept(plan);
            Assert.That(scheduler.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void VfxExecutor_DefaultLegacyMode_DoesNotCallPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(CreateVfxCueFrame(includeEnemyDeathExit: true));
            var port = new RecordingGameplayVfxPlaybackPort();
            var executor = new GameplayVfxPresentationExecutor(port);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.Zero);
            Assert.That(executor.Diagnostics.ObservedCueCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.LegacyOwnerNoOpCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.Zero);
            Assert.That(executor.Diagnostics.DuplicateSuppressedCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void VfxExecutor_OrchestrationMode_RoutesOneCueToPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(CreateVfxCueFrame(includeEnemyDeathExit: false));
            var port = new RecordingGameplayVfxPlaybackPort();
            var guard = new DamageDeathVfxExecutionGuard(DamageDeathVfxExecutionMode.OrchestrationExecutor);
            var executor = new GameplayVfxPresentationExecutor(
                port,
                DamageDeathVfxExecutionMode.OrchestrationExecutor,
                guard);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
            Assert.That(port.LastRequest.CueKey, Is.EqualTo(PresentationVfxCueKey.DamageHit));
            Assert.That(port.LastRequest.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Damage)));
            Assert.That(port.LastRequest.TickIndex, Is.EqualTo(7));
            Assert.That(port.LastRequest.SourceEntityId, Is.EqualTo(20));
            Assert.That(port.LastRequest.Target.EntityId, Is.EqualTo(20));
            Assert.That(port.LastRequest.PresentationAnchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityCenter));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.PlaybackSucceededCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void VfxExecutionGuard_BlocksDuplicateOwnerAttemptForSameDamageDeathKey()
        {
            var key = new DamageDeathVfxPlaybackKey(
                7,
                PresentationSemanticSource.EnemyDamage,
                20,
                20,
                PresentationVfxCueKey.DamageHit);
            var guard = new DamageDeathVfxExecutionGuard(DamageDeathVfxExecutionMode.OrchestrationExecutor);

            Assert.That(
                guard.TryBeginExecution(DamageDeathVfxExecutionOwner.OrchestrationExecutor, key),
                Is.True);
            Assert.That(
                guard.TryBeginExecution(DamageDeathVfxExecutionOwner.LegacyExtension, key),
                Is.False);

            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.SkippedLegacyBecauseExecutorOwnerCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void VfxExecutor_DistinguishesMissingTargetAnchorAndBinding()
        {
            var validCue = CreateVfxCueFrame(includeEnemyDeathExit: false).Cues.Single();
            var targetMissingCue = new PresentationCue(
                PresentationDomain.Vfx,
                PresentationCueKey.ForVfx(PresentationVfxCueKey.DamageHit),
                validCue.Source,
                PresentationTarget.None(),
                validCue.Anchor,
                validCue.PolicyHint);
            var anchorMissingCue = new PresentationCue(
                PresentationDomain.Vfx,
                PresentationCueKey.ForVfx(PresentationVfxCueKey.DamageHit),
                new PresentationSource(8, PresentationSemanticSource.EnemyDamage, 21),
                PresentationTarget.Entity(21),
                PresentationAnchor.None(),
                PresentationPlaybackPolicyHint.OneShot(222));
            var frame = new PresentationCueFrame(
                8,
                new[] { targetMissingCue, anchorMissingCue, validCue },
                new PresentationCueFrameDiagnostics(3, 3, 1));
            var plan = new PresentationPlaybackPlanner().Plan(frame);
            var port = new RecordingGameplayVfxPlaybackPort(GameplayVfxPlaybackResultKind.BindingMissing);
            var executor = new GameplayVfxPresentationExecutor(
                port,
                DamageDeathVfxExecutionMode.OrchestrationExecutor,
                new DamageDeathVfxExecutionGuard(DamageDeathVfxExecutionMode.OrchestrationExecutor));

            executor.Play(plan);

            Assert.That(executor.Diagnostics.TargetMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.AnchorMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.BindingMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
            Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void VfxExecutor_ResetSessionAndHardCleanup_ClearDiagnosticsAndPortState()
        {
            var plan = new PresentationPlaybackPlanner().Plan(CreateVfxCueFrame(includeEnemyDeathExit: false));
            var port = new RecordingGameplayVfxPlaybackPort();
            var executor = new GameplayVfxPresentationExecutor(
                port,
                DamageDeathVfxExecutionMode.OrchestrationExecutor,
                new DamageDeathVfxExecutionGuard(DamageDeathVfxExecutionMode.OrchestrationExecutor));

            executor.Play(plan);
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(1));

            executor.ResetSession();
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.Zero);
            Assert.That(port.ResetSessionCallCount, Is.EqualTo(1));

            executor.Play(plan);
            executor.HardCleanup();
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.Zero);
            Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void VfxOrchestrationRoute_DoesNotMutateAuthoritativeTickResult()
        {
            var result = CreateDiagnosticTickResult(includeEnemyDeathExit: true);
            var determinismHash = result.DeterminismHash;
            var finalEntities = result.FinalEntities.ToArray();
            var eventLog = result.EventLog.ToArray();
            var objectiveResult = result.ObjectiveResult;
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new VfxCuePlanner(),
            }).Plan(factFrame);
            var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var executor = new GameplayVfxPresentationExecutor(
                new RecordingGameplayVfxPlaybackPort(),
                DamageDeathVfxExecutionMode.OrchestrationExecutor,
                new DamageDeathVfxExecutionGuard(DamageDeathVfxExecutionMode.OrchestrationExecutor));

            executor.Play(playbackPlan);

            Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
            Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
            Assert.That(result.EventLog, Is.EqualTo(eventLog));
            Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationFactExtraction_ObservesJumpChargeAndDeathAsSemanticFacts()
        {
            var frame = new TickPresentationFactExtractor().Extract(CreateEnemyPresentationDiagnosticTickResult());
            var facts = frame.Facts
                .Where(fact => fact.Kind == PresentationFactKind.EnemyPresentation)
                .OrderBy(fact => fact.Target.EntityId)
                .ToArray();

            Assert.That(facts, Has.Length.EqualTo(3));
            Assert.That(frame.Diagnostics.EnemyPresentationFactCount, Is.EqualTo(3));
            AssertEnemyFact(
                facts[0],
                40,
                PresentationEnemyPresentationKind.Jump,
                PresentationEnemyPresentationPhase.Windup,
                PresentationSemanticSource.EnemyJump,
                11);
            AssertEnemyFact(
                facts[1],
                41,
                PresentationEnemyPresentationKind.Charge,
                PresentationEnemyPresentationPhase.Active,
                PresentationSemanticSource.EnemyCharge,
                12);
            AssertEnemyFact(
                facts[2],
                42,
                PresentationEnemyPresentationKind.Death,
                PresentationEnemyPresentationPhase.Death,
                PresentationSemanticSource.EntityExit,
                9042);
            Assert.That(facts.All(fact => fact.AnimationPayload.Kind == PresentationAnimationFactKind.EnemyPresentation), Is.True);
            Assert.That(facts.All(fact => fact.Target.Kind == PresentationTargetKind.Entity), Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioFactExtraction_ObservesOneShotSourceSignalsAsSemanticFacts()
        {
            var frame = new TickPresentationFactExtractor().Extract(CreateEnemyAudioDiagnosticTickResult());
            var facts = frame.Facts
                .Where(fact => fact.Kind == PresentationFactKind.EnemyAudio)
                .OrderBy(fact => fact.EnemyAudioPayload.OwnerEntityId)
                .ThenBy(fact => fact.EnemyAudioPayload.CueKey)
                .ToArray();

            Assert.That(facts, Has.Length.EqualTo(8));
            Assert.That(frame.Diagnostics.EnemyAudioFactCount, Is.EqualTo(8));
            AssertEnemyAudioFact(
                facts[0],
                50,
                PresentationEnemyAudioCueKey.Windup,
                PresentationEnemyAudioOriginKind.Action,
                PresentationEnemyAudioPhase.Windup,
                PresentationSemanticSource.EnemyAction);
            AssertEnemyAudioFact(
                facts[1],
                50,
                PresentationEnemyAudioCueKey.Active,
                PresentationEnemyAudioOriginKind.Action,
                PresentationEnemyAudioPhase.Active,
                PresentationSemanticSource.EnemyAction);
            AssertEnemyAudioFact(
                facts[2],
                50,
                PresentationEnemyAudioCueKey.Recover,
                PresentationEnemyAudioOriginKind.Action,
                PresentationEnemyAudioPhase.Recover,
                PresentationSemanticSource.EnemyAction);
            AssertEnemyAudioFact(
                facts[3],
                51,
                PresentationEnemyAudioCueKey.PassiveContact,
                PresentationEnemyAudioOriginKind.Action,
                PresentationEnemyAudioPhase.Active,
                PresentationSemanticSource.EnemyAction);
            AssertEnemyAudioFact(
                facts[4],
                52,
                PresentationEnemyAudioCueKey.Landing,
                PresentationEnemyAudioOriginKind.Jump,
                PresentationEnemyAudioPhase.Landing,
                PresentationSemanticSource.EnemyJump);
            AssertEnemyAudioFact(
                facts[5],
                53,
                PresentationEnemyAudioCueKey.Active,
                PresentationEnemyAudioOriginKind.Charge,
                PresentationEnemyAudioPhase.Active,
                PresentationSemanticSource.EnemyCharge);
            AssertEnemyAudioFact(
                facts[6],
                54,
                PresentationEnemyAudioCueKey.ForwardCellImpact,
                PresentationEnemyAudioOriginKind.ForwardCellImpact,
                PresentationEnemyAudioPhase.Impact,
                PresentationSemanticSource.EnemyForwardCellImpact);
            AssertEnemyAudioFact(
                facts[7],
                55,
                PresentationEnemyAudioCueKey.Death,
                PresentationEnemyAudioOriginKind.Death,
                PresentationEnemyAudioPhase.Death,
                PresentationSemanticSource.EntityExit);
            Assert.That(facts.All(fact => fact.EnemyAudioPayload.IsValid), Is.True);
            Assert.That(facts.All(fact => fact.Target.Kind == PresentationTargetKind.Entity), Is.True);
            Assert.That(facts.All(fact => fact.ActionAudioPayload.IsValid), Is.False);
            Assert.That(facts.All(fact => fact.EnemyPayload.IsValid), Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioCuePlanner_UsesTypedEnemyAudioKeysAndSeparateVocabulary()
        {
            var factFrame = new TickPresentationFactExtractor().Extract(CreateEnemyAudioDiagnosticTickResult());
            var enemyAudioCueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyAudioCuePlanner(),
            }).Plan(factFrame);
            var coreSfxCueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(factFrame);
            var enemyPresentationCueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyPresentationCuePlanner(),
            }).Plan(factFrame);

            Assert.That(enemyAudioCueFrame.Cues, Has.Count.EqualTo(8));
            Assert.That(enemyAudioCueFrame.Cues.All(cue => cue.Domain == PresentationDomain.EnemyAudio), Is.True);
            Assert.That(enemyAudioCueFrame.Cues.All(cue => cue.Key.TryGetEnemyAudioCueKey(out _)), Is.True);
            Assert.That(enemyAudioCueFrame.Cues.Any(cue => cue.Key.TryGetSfxCueKey(out _)), Is.False);
            Assert.That(enemyAudioCueFrame.Cues.Any(cue => cue.Key.TryGetActionAudioCueKey(out _)), Is.False);
            Assert.That(enemyAudioCueFrame.Cues.Any(cue => cue.Key.TryGetAnimationCueKey(out _)), Is.False);
            Assert.That(
                enemyAudioCueFrame.Cues.Select(cue => (PresentationEnemyAudioCueKey)cue.EnemyAudioPayload.CueKey).ToArray(),
                Is.EqualTo(new[]
                {
                    PresentationEnemyAudioCueKey.Windup,
                    PresentationEnemyAudioCueKey.Active,
                    PresentationEnemyAudioCueKey.Recover,
                    PresentationEnemyAudioCueKey.PassiveContact,
                    PresentationEnemyAudioCueKey.Landing,
                    PresentationEnemyAudioCueKey.Active,
                    PresentationEnemyAudioCueKey.ForwardCellImpact,
                    PresentationEnemyAudioCueKey.Death,
                }));
            Assert.That(
                coreSfxCueFrame.Cues.Any(cue => cue.Domain == PresentationDomain.EnemyAudio),
                Is.False);
            Assert.That(
                enemyPresentationCueFrame.Cues.Any(cue => cue.Domain == PresentationDomain.EnemyAudio),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioPlaybackPlan_IsPlanningOnlyOneShotAndNonBlocking()
        {
            var pipeline = GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();

            pipeline.Present(CreateEnemyAudioDiagnosticTickResult());

            Assert.That(pipeline.LastFactFrame.Diagnostics.EnemyAudioFactCount, Is.EqualTo(8));
            Assert.That(
                pipeline.LastCueFrame.Cues.Count(cue => cue.Domain == PresentationDomain.EnemyAudio),
                Is.EqualTo(8));
            Assert.That(
                pipeline.LastPlaybackPlan.Cues.Count(cue => cue.Cue.Domain == PresentationDomain.EnemyAudio),
                Is.EqualTo(8));
            Assert.That(
                pipeline.LastPlaybackPlan.Cues
                    .Where(cue => cue.Cue.Domain == PresentationDomain.EnemyAudio)
                    .All(cue => cue.Policy.UnitKind == PresentationPlaybackUnitKind.OneShot && !cue.Policy.Blocking),
                Is.True);
            Assert.That(pipeline.LastPlaybackPlan.Tracks, Is.Empty);
            Assert.That(pipeline.LastPlaybackPlan.Barriers, Is.Empty);
            Assert.That(pipeline.LastPlaybackPlan.Diagnostics.EnemyAudioCueCount, Is.EqualTo(8));
            Assert.That(pipeline.LastPlaybackPlan.Diagnostics.EnemyAudioPlaybackCueCount, Is.EqualTo(8));
            Assert.That(pipeline.LastPlaybackPlan.Diagnostics.EnemyAudioObservedCount, Is.EqualTo(8));
            Assert.That(pipeline.LastPlaybackPlan.Diagnostics.EnemyAudioIgnoredBecauseLegacyOwnerCount, Is.EqualTo(8));
            Assert.That(pipeline.LastPlaybackPlan.Diagnostics.EnemyAudioNoPlaybackBecausePlanningOnlyCount, Is.EqualTo(8));
            Assert.That(pipeline.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(pipeline.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            Assert.That(pipeline.HasBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioPlanning_DoesNotMutateAuthoritativeTickResult()
        {
            var result = CreateEnemyAudioDiagnosticTickResult();
            var initialHash = result.DeterminismHash;
            var initialEntities = result.FinalEntities.ToArray();
            var initialEventLog = result.EventLog.ToArray();
            var initialObjective = result.ObjectiveResult;
            var pipeline = GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();

            pipeline.Present(result);
            pipeline.Update(0f);

            Assert.That(result.DeterminismHash, Is.EqualTo(initialHash));
            Assert.That(result.FinalEntities, Is.EqualTo(initialEntities));
            Assert.That(result.EventLog, Is.EqualTo(initialEventLog));
            Assert.That(result.ObjectiveResult, Is.SameAs(initialObjective));
            Assert.That(pipeline.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(pipeline.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioPlanningBoundary_StaysPlanningOnlyAndDoesNotAbsorbPlaybackOwnership()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var planningSource = ReadDirectorySource(PlanningDirectory);
            var playbackSource = ReadDirectorySource(PlaybackDirectory);
            var runtimeSource = ReadDirectorySource(RuntimeDirectory);
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var enemyAudioExecutorSource = ReadRepoFile(EnemyAudioExecutorPath);
            var uiSource = ReadDirectorySource("Assets/_Features/UI");

            Assert.That(contractsPlanningPlaybackSource, Does.Contain("PresentationEnemyAudioPayload"));
            Assert.That(planningSource, Does.Contain("PresentationEnemyAudioCueKey"));
            Assert.That(planningSource, Does.Contain("EnemyAudioCuePlanner"));
            Assert.That(playbackSource, Does.Contain("EnemyAudioNoPlaybackBecausePlanningOnlyCount"));

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("EnemyAudioPresentationController"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("EnemyAudioProfile"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("EnemyAudioAuthoring"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioManager"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioBinding"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioClip"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioSource"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("Play2D"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("PlayAttached"));
            Assert.That(runtimeSource, Does.Not.Contain("EnemyAudioPresentationController"));
            Assert.That(runtimeSource, Does.Not.Contain("EnemyAudioProfile"));
            Assert.That(runtimeSource, Does.Not.Contain("AudioManager"));

            Assert.That(coordinatorSource, Does.Contain("EnemyAudioExecutionMode.LegacyEnemyAudioController"));
            Assert.That(coordinatorSource, Does.Contain("EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge"));
            Assert.That(coordinatorSource, Does.Contain("ConfigureEnemyAudioExecution"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayEnemyAudioPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayEnemyAudioPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("EnemyAudioExecutionGuard"));
            Assert.That(enemyAudioExecutorSource, Does.Contain("GameplayEnemyAudioPlaybackPortAdapter"));
            Assert.That(enemyAudioExecutorSource, Does.Contain("EnemyAudioPresentationController controller"));
            Assert.That(enemyAudioExecutorSource, Does.Not.Contain("AudioManager"));
            Assert.That(enemyAudioExecutorSource, Does.Not.Contain("PresentationSfxCueKey"));
            Assert.That(enemyAudioExecutorSource, Does.Not.Contain("PresentationActionAudioCueKey"));

            Assert.That(planningSource, Does.Not.Contain("PresentationSfxCueKey.ForwardCellImpact"));
            Assert.That(planningSource, Does.Not.Contain("PresentationActionAudioCueKey.Enemy"));
            Assert.That(uiSource, Does.Not.Contain("PresentationEnemyAudioPayload"));
            Assert.That(uiSource, Does.Not.Contain("PresentationEnemyAudioCueKey"));
            Assert.That(uiSource, Does.Not.Contain("EnemyAudioCuePlanner"));
            Assert.That(uiSource, Does.Not.Contain("EnemyAudioExecutionMode"));
            Assert.That(uiSource, Does.Not.Contain("GameplayEnemyAudioExecutorDiagnostics"));
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationCuePlanner_UsesTypedAnimationKeysAndSymbolicAnchors()
        {
            var cueFrame = CreateEnemyPresentationCueFrame();
            var cues = cueFrame.Cues
                .Where(cue => cue.Domain == PresentationDomain.Animation && cue.EnemyPayload.IsValid)
                .OrderBy(cue => cue.Target.EntityId)
                .ToArray();

            Assert.That(cues, Has.Length.EqualTo(3));
            AssertEnemyCue(cues[0], 40, PresentationAnimationCueKey.EnemyJumpWindup);
            AssertEnemyCue(cues[1], 41, PresentationAnimationCueKey.EnemyChargeActive);
            AssertEnemyCue(cues[2], 42, PresentationAnimationCueKey.EnemyDeath);
            Assert.That(cues.All(cue => cue.Anchor.Kind == PresentationAnchorKind.EntityVisualRoot), Is.True);
            Assert.That(cues.All(cue => cue.PolicyHint.Kind == PresentationPlaybackPolicyHintKind.OneShot), Is.True);
            Assert.That(cues.All(cue => !cue.PolicyHint.Blocking), Is.True);
            Assert.That(cues.All(cue => cue.PolicyHint.DedupeKey > 0), Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationPlaybackPlanner_CreatesNonBlockingCuesWithoutBarriers()
        {
            var plan = CreateEnemyPresentationPlaybackPlan();
            var scheduler = new PresentationPlaybackScheduler();

            scheduler.Accept(plan);
            scheduler.Update(0f);

            Assert.That(plan.Cues.Count(cue => cue.Cue.Domain == PresentationDomain.Animation), Is.EqualTo(3));
            Assert.That(plan.Tracks.Any(track => track.Cue.Domain == PresentationDomain.Animation), Is.False);
            Assert.That(plan.Barriers, Is.Empty);
            Assert.That(plan.Cues.All(cue => !cue.Policy.Blocking), Is.True);
            Assert.That(plan.Cues.All(cue => cue.Policy.UnitKind == PresentationPlaybackUnitKind.OneShot), Is.True);
            Assert.That(plan.Cues.All(cue => cue.Policy.DedupeKey > 0), Is.True);
            Assert.That(scheduler.HasBlockingPresentation, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(scheduler.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationExecutor_DefaultLegacyMode_DoesNotCallPlaybackPort()
        {
            var plan = CreateEnemyPresentationPlaybackPlan();
            var port = new RecordingEnemyPresentationPlaybackPort();
            var executor = new GameplayEnemyPresentationExecutor(port);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.Zero);
            Assert.That(executor.Diagnostics.ObservedCueCount, Is.EqualTo(3));
            Assert.That(executor.Diagnostics.LegacyOwnerNoOpCount, Is.EqualTo(3));
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.Zero);
            Assert.That(executor.Diagnostics.DuplicateSuppressedCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationExecutor_OrchestrationMode_RoutesJumpChargeDeathToPlaybackPort()
        {
            var plan = CreateEnemyPresentationPlaybackPlan();
            var port = new RecordingEnemyPresentationPlaybackPort(GameplayEnemyPresentationPlaybackResultKind.Applied);
            var guard = new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor);
            var executor = new GameplayEnemyPresentationExecutor(
                port,
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                guard);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.EqualTo(3));
            Assert.That(port.Requests.Select(request => request.CueKey), Is.EquivalentTo(new[]
            {
                PresentationAnimationCueKey.EnemyJumpWindup,
                PresentationAnimationCueKey.EnemyChargeActive,
                PresentationAnimationCueKey.EnemyDeath,
            }));
            Assert.That(port.Requests.All(request => request.TickIndex == 7), Is.True);
            Assert.That(port.Requests.All(request => request.Anchor.Kind == PresentationAnchorKind.EntityVisualRoot), Is.True);
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(3));
            Assert.That(executor.Diagnostics.CommandAppliedCount, Is.EqualTo(3));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(3));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationExecutionGuard_BlocksDuplicateOwnerAttemptForSameKey()
        {
            var key = new EnemyPresentationPlaybackKey(
                7,
                PresentationSemanticSource.EnemyJump,
                40,
                PresentationAnimationCueKey.EnemyJumpWindup,
                PresentationEnemyPresentationKind.Jump,
                PresentationEnemyPresentationPhase.Windup,
                11);
            var guard = new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor);

            Assert.That(
                guard.TryBeginExecution(EnemyPresentationExecutionOwner.OrchestrationEnemyPresentationExecutor, key),
                Is.True);
            Assert.That(
                guard.TryBeginExecution(EnemyPresentationExecutionOwner.LegacyEnemyPresentationMapper, key),
                Is.False);

            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.SkippedLegacyBecauseExecutorOwnerCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationExecutor_DistinguishesMissingTargetAnchorPortAndRuntimeBindings()
        {
            var cue = CreateEnemyPresentationCueFrame().Cues.Single(cue =>
                cue.Key.TryGetAnimationCueKey(out var key) &&
                key == PresentationAnimationCueKey.EnemyJumpWindup);
            var targetMissingCue = CreateEnemyPresentationCue(cue, PresentationTarget.None(), cue.Anchor);
            var anchorMissingCue = CreateEnemyPresentationCue(
                cue,
                PresentationTarget.Entity(51),
                PresentationAnchor.None(),
                51,
                51);
            var validCue = CreateEnemyPresentationCue(
                cue,
                PresentationTarget.Entity(52),
                PresentationAnchor.ForEntityVisualRoot(52),
                52,
                52);
            var frame = new PresentationCueFrame(
                7,
                new[] { targetMissingCue, anchorMissingCue, validCue },
                new PresentationCueFrameDiagnostics(3, 3, 1));
            var plan = new PresentationPlaybackPlanner().Plan(frame);
            var port = new RecordingEnemyPresentationPlaybackPort(GameplayEnemyPresentationPlaybackResultKind.MapperMissing);
            var executor = new GameplayEnemyPresentationExecutor(
                port,
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));

            executor.Play(plan);

            Assert.That(executor.Diagnostics.TargetMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.AnchorMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.MapperMissingCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(1));

            AssertEnemyMissingRuntimeResult(GameplayEnemyPresentationPlaybackResultKind.BindingMissing, diagnostics => diagnostics.BindingMissingCount);
            AssertEnemyMissingRuntimeResult(GameplayEnemyPresentationPlaybackResultKind.DriverMissing, diagnostics => diagnostics.DriverMissingCount);
            AssertEnemyMissingRuntimeResult(GameplayEnemyPresentationPlaybackResultKind.AnimatorMissing, diagnostics => diagnostics.AnimatorMissingCount);

            var missingPortExecutor = new GameplayEnemyPresentationExecutor(
                null,
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
            missingPortExecutor.Play(new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                7,
                new[] { validCue },
                new PresentationCueFrameDiagnostics(1, 1, 1))));
            Assert.That(missingPortExecutor.Diagnostics.MissingPortCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationExecutor_ResetSessionAndHardCleanup_ClearDiagnosticsAndPortState()
        {
            var plan = CreateEnemyPresentationPlaybackPlan();
            var port = new RecordingEnemyPresentationPlaybackPort();
            var guard = new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor);
            var executor = new GameplayEnemyPresentationExecutor(
                port,
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                guard);

            executor.Play(plan);
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(3));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(3));

            executor.ResetSession();
            guard.ResetSession();
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.Zero);
            Assert.That(port.ResetSessionCallCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.Zero);

            executor.Play(plan);
            executor.HardCleanup();
            guard.ResetSession();
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.Zero);
            Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void EnemyPresentationOrchestrationRoute_DoesNotMutateAuthoritativeTickResult()
        {
            var result = CreateEnemyPresentationDiagnosticTickResult();
            var determinismHash = result.DeterminismHash;
            var finalEntities = result.FinalEntities.ToArray();
            var eventLog = result.EventLog.ToArray();
            var objectiveResult = result.ObjectiveResult;
            var playbackPlan = new PresentationPlaybackPlanner().Plan(CreateEnemyPresentationCueFrame(result));
            var executor = new GameplayEnemyPresentationExecutor(
                new RecordingEnemyPresentationPlaybackPort(),
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));

            executor.Play(playbackPlan);

            Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
            Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
            Assert.That(result.EventLog, Is.EqualTo(eventLog));
            Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
        }

        private static TickResult CreateDiagnosticTickResult(
            TickTopologyMotion? topologyMotion = null,
            bool includeTopologyMotion = true,
            bool includeEnemyDeathExit = false)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var resolvedTopologyMotion = includeTopologyMotion
                ? topologyMotion ?? new TickTopologyMotion(topology, destinationTopology, CubeRotationKind.Forward)
                : (TickTopologyMotion?)null;
            var entityExitSignals = includeEnemyDeathExit
                ? new[]
                {
                    new TickEntityExitPresentationSignal(
                        exitedEntityId: 30,
                        TickEntityExitCause.Killed,
                        cell,
                        topology,
                        Direction.Right,
                        EntityType.Unit,
                        sourceActorEntityId: 10,
                        presentationSeed: 3030),
                }
                : new[]
                {
                    new TickEntityExitPresentationSignal(
                        exitedEntityId: 30,
                        TickEntityExitCause.BoxDestroy,
                        cell,
                        topology,
                        Direction.Right,
                        EntityType.Box),
                };
            var presentationData = new TickPresentationData(
                new[]
                {
                    new TickEntityMotion(
                        entityId: 10,
                        TickEntityMotionKind.Move,
                        cell,
                        destinationCell),
                },
                resolvedTopologyMotion,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                },
                new[]
                {
                    new TickPlayerDeathPresentationSignal(
                        10,
                        didDieThisTick: true,
                        sourceEntityId: 20,
                        Direction.Left,
                        resolvedDamageSourceAvailable: true,
                        damageAmountAtFatalHit: 1,
                        DeathDirectionHintKind.AttackerReverse),
                },
                new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 2),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: new[]
                {
                    new TilePresentationEvent(
                        TilePresentationEventKind.ButtonActivated,
                        tileId: 100,
                        cell,
                        TileFeatureKind.Button,
                        sourceEntityId: 10,
                        ownerEntityId: 0,
                        teamId: 1),
                },
                gravityFieldEvents: new[]
                {
                    new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.Activated,
                        emitterEntityId: 20,
                        cell),
                });

            return new TickResult(
                tickIndex: 7,
                completedPhases: Array.Empty<TickPhase>(),
                phaseTrace: Array.Empty<string>(),
                movementPhaseResult: MovementPhaseResult.Empty,
                attackPhaseResult: AttackPhaseResult.Empty,
                finalEntities: Array.Empty<EntityState>(),
                eventLog: new[] { "AuthoritativeEvent" },
                finalTopology: destinationTopology,
                presentationData: presentationData,
                determinismHash: "ABCDEF",
                trace: TickTrace.Empty,
                objectiveResult: new StageObjectiveTickResult(
                    hasObjective: true,
                    goalReached: true,
                    allConditionsSatisfied: true,
                    clearedThisTick: true,
                    isCleared: true,
                    conditionStatuses: Array.Empty<StageConditionStatus>()));
        }

        private static PresentationCueFrame CreateVfxCueFrame(bool includeEnemyDeathExit)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(
                CreateDiagnosticTickResult(
                    includeTopologyMotion: false,
                    includeEnemyDeathExit: includeEnemyDeathExit));
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new VfxCuePlanner(),
            }).Plan(factFrame);
        }

        private static PresentationCueFrame CreateSfxCueFrame(bool includeEnemyDeathExit)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(
                CreateDiagnosticTickResult(
                    includeTopologyMotion: false,
                    includeEnemyDeathExit: includeEnemyDeathExit));
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(factFrame);
        }

        private static PresentationCueFrame CreateAllCoreSfxCueFrame()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var presentationData = new TickPresentationData(
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
                new[]
                {
                    new TickPlayerDeathPresentationSignal(
                        10,
                        didDieThisTick: false,
                        sourceEntityId: 0,
                        Direction.None,
                        resolvedDamageSourceAvailable: false,
                        damageAmountAtFatalHit: 0,
                        DeathDirectionHintKind.Unknown),
                },
                new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 2),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        30,
                        TickEntityExitCause.ItemConsume,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        topology,
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        40,
                        TickEntityExitCause.BoxDestroy,
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        topology,
                        Direction.Up,
                        EntityType.Box,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        50,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 1),
                        topology,
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        60,
                        TickEntityExitCause.OutOfBounds,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        topology,
                        Direction.Up,
                        EntityType.Unit),
                });
            var result = new TickResult(
                tickIndex: 9,
                completedPhases: Array.Empty<TickPhase>(),
                phaseTrace: Array.Empty<string>(),
                movementPhaseResult: MovementPhaseResult.Empty,
                attackPhaseResult: AttackPhaseResult.Empty,
                finalEntities: Array.Empty<EntityState>(),
                eventLog: Array.Empty<string>(),
                finalTopology: topology,
                presentationData: presentationData,
                determinismHash: "SFX",
                trace: TickTrace.Empty,
                objectiveResult: StageObjectiveTickResult.NoObjective);
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(factFrame);
        }

        private static TickResult CreateEnemyPresentationDiagnosticTickResult()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                new[]
                {
                    new TickEnemyJumpPresentationSignal(
                        entityId: 40,
                        sequence: 11,
                        phase: EnemyJumpPhase.Windup,
                        startedWindupThisTick: true,
                        startedAirborneThisTick: false,
                        landedThisTick: false,
                        retryThisTick: false,
                        sourceCell: cell,
                        lockedTargetCell: targetCell,
                        presentationTargetCell: targetCell,
                        facing: Direction.Right,
                        windupTicks: 2,
                        landingTick: 9,
                        remainingAirborneTicks: 0,
                        retryCount: 0,
                        TickEnemyJumpPresentationOutcome.WindupStarted),
                },
                new[]
                {
                    new TickEnemyChargePresentationSignal(
                        entityId: 41,
                        sequence: 12,
                        phase: EnemyChargePhase.Active,
                        startedWindupThisTick: false,
                        startedActiveThisTick: true,
                        startedRecoverThisTick: false,
                        lockedDirection: Direction.Down),
                },
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        exitedEntityId: 42,
                        exitCause: TickEntityExitCause.Killed,
                        sourceCell: cell,
                        topology: topology,
                        facing: Direction.Left,
                        entityType: EntityType.Unit,
                        sourceActorEntityId: 10,
                        presentationSeed: 9042,
                        timing: EntityExitPresentationTiming.Immediate,
                        hasPresentationTargetCell: true,
                        presentationTargetCell: cell),
                },
                Array.Empty<FlipImpactPresentationSignal>());

            return new TickResult(
                tickIndex: 7,
                completedPhases: Array.Empty<TickPhase>(),
                phaseTrace: Array.Empty<string>(),
                movementPhaseResult: MovementPhaseResult.Empty,
                attackPhaseResult: AttackPhaseResult.Empty,
                finalEntities: Array.Empty<EntityState>(),
                eventLog: new[] { "AuthoritativeEvent" },
                finalTopology: topology,
                presentationData: presentationData,
                determinismHash: "ENEMY-PRESENTATION-HASH",
                trace: TickTrace.Empty,
                objectiveResult: new StageObjectiveTickResult(
                    hasObjective: false,
                    goalReached: false,
                    allConditionsSatisfied: false,
                    clearedThisTick: false,
                    isCleared: false,
                    conditionStatuses: Array.Empty<StageConditionStatus>()));
        }

        private static TickResult CreateEnemyAudioDiagnosticTickResult()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                new[]
                {
                    new TickEnemyActionPresentationSignal(
                        entityId: 50,
                        activeActionKind: EnemyActionKind.Melee,
                        activeActionSequence: 21,
                        startedThisTick: true,
                        canceledThisTick: false,
                        executedThisTick: true,
                        startedRecoveryThisTick: true,
                        EnemyActionPresentationSource.Combat,
                        EnemyActionPresentationOutcome.Executed),
                    new TickEnemyActionPresentationSignal(
                        entityId: 51,
                        activeActionKind: EnemyActionKind.Melee,
                        activeActionSequence: 22,
                        startedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        startedRecoveryThisTick: false,
                        EnemyActionPresentationSource.PassiveContact,
                        EnemyActionPresentationOutcome.Executed),
                },
                new[]
                {
                    new TickEnemyJumpPresentationSignal(
                        entityId: 52,
                        sequence: 23,
                        phase: EnemyJumpPhase.Cooldown,
                        startedWindupThisTick: false,
                        startedAirborneThisTick: false,
                        landedThisTick: true,
                        retryThisTick: false,
                        sourceCell: cell,
                        lockedTargetCell: targetCell,
                        presentationTargetCell: targetCell,
                        facing: Direction.Right,
                        windupTicks: 2,
                        landingTick: 9,
                        remainingAirborneTicks: 0,
                        retryCount: 0,
                        TickEnemyJumpPresentationOutcome.Landed),
                },
                new[]
                {
                    new TickEnemyChargePresentationSignal(
                        entityId: 53,
                        sequence: 24,
                        phase: EnemyChargePhase.Active,
                        startedWindupThisTick: false,
                        startedActiveThisTick: true,
                        startedRecoverThisTick: false,
                        lockedDirection: Direction.Down),
                },
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        exitedEntityId: 55,
                        exitCause: TickEntityExitCause.Killed,
                        sourceCell: cell,
                        topology: topology,
                        facing: Direction.Left,
                        entityType: EntityType.Unit,
                        sourceActorEntityId: 10,
                        presentationSeed: 9055,
                        timing: EntityExitPresentationTiming.Immediate,
                        hasPresentationTargetCell: true,
                        presentationTargetCell: cell),
                },
                Array.Empty<FlipImpactPresentationSignal>(),
                forwardCellProjectileArrivalSignals: new[]
                {
                    new TickForwardCellProjectileArrivalPresentationSignal(
                        impactId: 31,
                        presentationKey: 9101,
                        ownerId: 54,
                        sourceEnemyId: 54,
                        targetCell: targetCell,
                        direction: Direction.Up,
                        impactTick: 7,
                        PendingCellImpactResolutionKind.Miss,
                        targetEntityId: 10),
                });

            return new TickResult(
                tickIndex: 7,
                completedPhases: Array.Empty<TickPhase>(),
                phaseTrace: Array.Empty<string>(),
                movementPhaseResult: MovementPhaseResult.Empty,
                attackPhaseResult: AttackPhaseResult.Empty,
                finalEntities: Array.Empty<EntityState>(),
                eventLog: new[] { "AuthoritativeEvent" },
                finalTopology: topology,
                presentationData: presentationData,
                determinismHash: "ENEMY-AUDIO-HASH",
                trace: TickTrace.Empty,
                objectiveResult: StageObjectiveTickResult.NoObjective);
        }

        private static PresentationCueFrame CreateEnemyPresentationCueFrame(TickResult result = null)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(
                result ?? CreateEnemyPresentationDiagnosticTickResult());
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyPresentationCuePlanner(),
            }).Plan(factFrame);
        }

        private static PresentationPlaybackPlan CreateEnemyPresentationPlaybackPlan()
        {
            return new PresentationPlaybackPlanner().Plan(CreateEnemyPresentationCueFrame());
        }

        private static void AssertEnemyFact(
            PresentationFact fact,
            int enemyEntityId,
            PresentationEnemyPresentationKind kind,
            PresentationEnemyPresentationPhase phase,
            PresentationSemanticSource source,
            int sequence)
        {
            Assert.That(fact.Source.TickIndex, Is.EqualTo(7));
            Assert.That(fact.Source.SemanticSource, Is.EqualTo(source));
            Assert.That(fact.Source.SourceEntityId, Is.EqualTo(enemyEntityId));
            Assert.That(fact.Source.SourceSequence, Is.EqualTo(sequence));
            Assert.That(fact.Target.EntityId, Is.EqualTo(enemyEntityId));
            Assert.That(fact.EnemyPayload.IsValid, Is.True);
            Assert.That(fact.EnemyPayload.EnemyEntityId, Is.EqualTo(enemyEntityId));
            Assert.That(fact.EnemyPayload.Kind, Is.EqualTo(kind));
            Assert.That(fact.EnemyPayload.Phase, Is.EqualTo(phase));
            Assert.That(fact.EnemyPayload.SourceTickIndex, Is.EqualTo(7));
            Assert.That(fact.EnemyPayload.SourceSequenceId, Is.EqualTo(sequence));
        }

        private static void AssertEnemyAudioFact(
            PresentationFact fact,
            int enemyEntityId,
            PresentationEnemyAudioCueKey cueKey,
            PresentationEnemyAudioOriginKind originKind,
            PresentationEnemyAudioPhase phase,
            PresentationSemanticSource source)
        {
            Assert.That(fact.Source.TickIndex, Is.EqualTo(7));
            Assert.That(fact.Source.SemanticSource, Is.EqualTo(source));
            Assert.That(fact.Source.SourceEntityId, Is.EqualTo(enemyEntityId));
            Assert.That(fact.Target.EntityId, Is.EqualTo(enemyEntityId));
            Assert.That(fact.EnemyAudioPayload.IsValid, Is.True);
            Assert.That(fact.EnemyAudioPayload.OwnerEntityId, Is.EqualTo(enemyEntityId));
            Assert.That(fact.EnemyAudioPayload.CueKey, Is.EqualTo((int)cueKey));
            Assert.That(fact.EnemyAudioPayload.SourceTickIndex, Is.EqualTo(7));
            Assert.That(fact.EnemyAudioPayload.OriginKind, Is.EqualTo(originKind));
            Assert.That(fact.EnemyAudioPayload.Phase, Is.EqualTo(phase));
        }

        private static void AssertEnemyCue(
            PresentationCue cue,
            int enemyEntityId,
            PresentationAnimationCueKey expectedKey)
        {
            Assert.That(cue.Key.Domain, Is.EqualTo(PresentationDomain.Animation));
            Assert.That(cue.Key.TryGetAnimationCueKey(out var key), Is.True);
            Assert.That(key, Is.EqualTo(expectedKey));
            Assert.That(cue.Target.Kind, Is.EqualTo(PresentationTargetKind.Entity));
            Assert.That(cue.Target.EntityId, Is.EqualTo(enemyEntityId));
            Assert.That(cue.EnemyPayload.EnemyEntityId, Is.EqualTo(enemyEntityId));
            Assert.That(cue.AnimationPayload.Kind, Is.EqualTo(PresentationAnimationFactKind.EnemyPresentation));
        }

        private static PresentationCue CreateEnemyPresentationCue(
            PresentationCue source,
            PresentationTarget target,
            PresentationAnchor anchor,
            int enemyEntityId = 0,
            int sourceSequence = 0)
        {
            var resolvedEnemyEntityId = enemyEntityId > 0
                ? enemyEntityId
                : source.EnemyPayload.EnemyEntityId;
            var resolvedSequence = sourceSequence > 0
                ? sourceSequence
                : source.EnemyPayload.SourceSequenceId;
            var enemyPayload = new PresentationEnemyPayload(
                source.EnemyPayload.Kind,
                source.EnemyPayload.Phase,
                resolvedEnemyEntityId,
                source.EnemyPayload.SourceTickIndex,
                resolvedSequence,
                source.EnemyPayload.Outcome,
                source.EnemyPayload.SourceCell,
                source.EnemyPayload.TargetCell,
                source.EnemyPayload.HasSourceCell,
                source.EnemyPayload.HasTargetCell,
                source.EnemyPayload.Direction,
                source.EnemyPayload.SourceCause,
                source.EnemyPayload.Timing);
            var animationPayload = new PresentationAnimationPayload(
                source.AnimationPayload.Kind,
                resolvedEnemyEntityId,
                source.AnimationPayload.ActionKind,
                source.AnimationPayload.PhaseKind,
                source.AnimationPayload.OutcomeKind,
                source.AnimationPayload.SourceTickIndex,
                resolvedSequence,
                source.AnimationPayload.SourceActionPlanId,
                source.AnimationPayload.TargetEntityId,
                source.AnimationPayload.Direction);
            return new PresentationCue(
                source.Domain,
                source.Key,
                new PresentationSource(
                    source.Source.TickIndex,
                    source.Source.SemanticSource,
                    resolvedEnemyEntityId,
                    source.Source.SourceActionKind,
                    resolvedSequence),
                target,
                anchor,
                PresentationPlaybackPolicyHint.OneShot(source.PolicyHint.DedupeKey + resolvedEnemyEntityId),
                animationPayload: animationPayload,
                enemyPayload: enemyPayload);
        }

        private static void AssertEnemyMissingRuntimeResult(
            GameplayEnemyPresentationPlaybackResultKind resultKind,
            Func<GameplayEnemyPresentationExecutorDiagnostics, int> selector)
        {
            var plan = CreateEnemyPresentationPlaybackPlan();
            var executor = new GameplayEnemyPresentationExecutor(
                new RecordingEnemyPresentationPlaybackPort(resultKind),
                EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor,
                new EnemyPresentationExecutionGuard(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));

            executor.Play(plan);

            Assert.That(selector(executor.Diagnostics), Is.EqualTo(3));
            Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(3));
        }

        private sealed class RecordingTopologyTransitionPlaybackPort : ITopologyTransitionPlaybackPort
        {
            public int BeginOrRefreshCallCount { get; private set; }

            public bool IsTransitionActive { get; private set; }

            public void BeginOrRefreshTopologyTransition(TopologyTransitionPlaybackRequest request)
            {
                BeginOrRefreshCallCount++;
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void ResetSession()
            {
                IsTransitionActive = false;
            }

            public void HardCleanup()
            {
                IsTransitionActive = false;
            }
        }

        private sealed class RecordingEnemyPresentationPlaybackPort : IGameplayEnemyPresentationPlaybackPort
        {
            private readonly GameplayEnemyPresentationPlaybackResultKind _resultKind;

            public RecordingEnemyPresentationPlaybackPort(
                GameplayEnemyPresentationPlaybackResultKind resultKind = GameplayEnemyPresentationPlaybackResultKind.Applied)
            {
                _resultKind = resultKind;
            }

            public int TryPlayCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public List<GameplayEnemyPresentationPlaybackRequest> Requests { get; } = new();

            public bool TryPlayEnemyPresentation(
                in GameplayEnemyPresentationPlaybackRequest request,
                out GameplayEnemyPresentationPlaybackResult result)
            {
                TryPlayCallCount++;
                Requests.Add(request);
                result = new GameplayEnemyPresentationPlaybackResult(_resultKind);
                return _resultKind == GameplayEnemyPresentationPlaybackResultKind.Applied ||
                       _resultKind == GameplayEnemyPresentationPlaybackResultKind.Requested;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                Requests.Clear();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                Requests.Clear();
            }
        }

        private sealed class RecordingGameplayVfxPlaybackPort : IDamageDeathVfxPlaybackPort
        {
            private readonly GameplayVfxPlaybackResultKind _resultKind;

            public RecordingGameplayVfxPlaybackPort(
                GameplayVfxPlaybackResultKind resultKind = GameplayVfxPlaybackResultKind.Succeeded)
            {
                _resultKind = resultKind;
            }

            public int TryPlayCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public GameplayVfxPlaybackRequest LastRequest { get; private set; }

            public bool TryPlayDamageDeathVfx(
                in GameplayVfxPlaybackRequest request,
                out GameplayVfxPlaybackResult result)
            {
                TryPlayCallCount++;
                LastRequest = request;
                result = new GameplayVfxPlaybackResult(_resultKind);
                return _resultKind == GameplayVfxPlaybackResultKind.Succeeded;
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                LastRequest = default;
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                LastRequest = default;
            }
        }

        private static string[] GetReferenceNames(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .OrderBy(name => name)
                .ToArray();
        }

        private static IEnumerable<Type> GetPublicSurface(Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                yield return NormalizeType(field.FieldType);
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                yield return NormalizeType(property.PropertyType);
            }

            foreach (var constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    yield return NormalizeType(parameter.ParameterType);
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                yield return NormalizeType(method.ReturnType);
                foreach (var parameter in method.GetParameters())
                {
                    yield return NormalizeType(parameter.ParameterType);
                }
            }
        }

        private static Type NormalizeType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsByRef || type.IsPointer || type.IsArray)
            {
                return NormalizeType(type.GetElementType());
            }

            if (type.IsGenericType)
            {
                return type.GetGenericArguments()
                    .Select(NormalizeType)
                    .FirstOrDefault(argument => argument != null &&
                                                argument.Namespace != null &&
                                                argument.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal));
            }

            return type;
        }

        private static string ReadDirectorySource(string relativeDirectory)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativeDirectory));
            return string.Join(
                "\n",
                Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path)
                    .Select(File.ReadAllText));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(fullPath);
        }
    }
}
