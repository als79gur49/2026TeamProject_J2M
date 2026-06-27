using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Audio;
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
        private const string ContractsPath =
            "Assets/_Features/Gameplay/Gameplay_PresentationContracts/Runtime/PresentationContracts.cs";
        private const string PlanningDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationPlanning/Runtime";
        private const string PlaybackDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationPlayback/Runtime";
        private const string RuntimeDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationRuntime/Runtime";
        private const string HostRuntimeDirectory =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string ApplierPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityPresentationApplier.cs";
        private const string CoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
        private const string PlayerViewPresentationMapperPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerViewPresentationMapper.cs";
        private const string PlayerLocomotionAudioPresentationControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerLocomotionAudioPresentationController.cs";
        private const string GameplayVfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string EnemyMotionAttachedVfxFollowerPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyMotionAttachedVfxFollowerPlanner.cs";
        private const string GameplayAnimationSyncCoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs";
        private const string ExitPresentationControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs";
        private const string TrackStatePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationTrackState.cs";
        private const string TickResultBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs";
        private const string PresenterPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs";
        private const string HostFactoryPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs";
        private const string CompositionPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeComposition.cs";
        private const string CompositionMetaPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeComposition.cs.meta";
        private const string CompositionFactoryPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeCompositionFactory.cs";
        private const string CompositionFactoryMetaPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeCompositionFactory.cs.meta";
        private const string TopologyExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationExecutor.cs";
        private const string TopologyBridgeVisibilityControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyVisualBridgeVisibilityController.cs";
        private const string TopologyLaneRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationLaneRuntime.cs";
        private const string DamageDeathVfxLaneRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs";
        private const string DamageDeathVfxLaneRuntimeMetaPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs.meta";
        private const string DamageDeathVfxExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationExecutor.cs";
        private const string BoxMotionExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs";
        private const string BoxMotionLaneRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs";
        private const string BoxMotionRuntimeCleanupAdapterPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionRuntimeCleanupAdapter.cs";
        private const string PlayerActionAnimationExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs";
        private const string PlayerActionAnimationLaneRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationLaneRuntime.cs";
        private const string EnemyPresentationExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs";
        private const string EnemyPresentationLaneRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationLaneRuntime.cs";
        private const string EnemyAnimatorDriverPath =
            "Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyAnimatorDriver.cs";
        private const string ActionAudioExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs";
        private const string ActionAudioLaneRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioLaneRuntime.cs";
        private const string EnemyAudioExecutorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs";
        private const string EnemyOneShotAudioLaneRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyOneShotAudioLaneRuntime.cs";
        private const string CoreGameplaySfxLaneRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs";

        [Test]
        [Category("Core")]
        public void PresentationRuntimeComposition_FinalizesFactoryBoundary()
        {
            var compositionFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CompositionPath));
            var compositionMetaFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CompositionMetaPath));
            var compositionFactoryFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CompositionFactoryPath));
            var compositionFactoryMetaFullPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", CompositionFactoryMetaPath));
            var compositionSource = ReadRepoFile(CompositionPath);
            var compositionFactorySource = ReadRepoFile(CompositionFactoryPath);
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var presenterSource = ReadRepoFile(PresenterPath);
            var hostFactorySource = ReadRepoFile(HostFactoryPath);
            var hostConstructionBlock = ExtractSourceBetween(
                hostFactorySource,
                "var presentationDependencies = DiscoverPresentationHostDependencies(hostObject);",
                "presenter.Initialize(");

            Assert.That(File.Exists(compositionFullPath), Is.True);
            Assert.That(File.Exists(compositionMetaFullPath), Is.True);
            Assert.That(File.Exists(compositionFactoryFullPath), Is.True);
            Assert.That(File.Exists(compositionFactoryMetaFullPath), Is.True);

            Assert.That(compositionSource, Does.Contain("internal sealed class GameplayPresentationRuntimeComposition"));
            Assert.That(compositionSource, Does.Contain("EnemyOneShotAudioLaneRuntime EnemyOneShotAudioLane"));
            Assert.That(compositionSource, Does.Contain("GameplayActionAudioLaneRuntime GameplayActionAudioLane"));
            Assert.That(compositionSource, Does.Contain("CoreGameplaySfxLaneRuntime CoreGameplaySfxLane"));
            Assert.That(compositionSource, Does.Contain("PlayerActionAnimationLaneRuntime PlayerActionAnimationLane"));
            Assert.That(compositionSource, Does.Contain("EnemyPresentationLaneRuntime EnemyPresentationLane"));
            Assert.That(compositionSource, Does.Contain("BoxMotionPresentationLaneRuntime BoxMotionLane"));
            Assert.That(compositionSource, Does.Contain("TopologyPresentationLaneRuntime TopologyLane"));
            Assert.That(compositionSource, Does.Contain("DamageDeathVfxPresentationLaneRuntime DamageDeathVfxLane"));
            Assert.That(compositionSource, Does.Not.Contain("Dictionary<string"));
            Assert.That(compositionSource, Does.Not.Contain("Dictionary<Type"));
            Assert.That(compositionSource, Does.Not.Contain("IServiceProvider"));
            Assert.That(compositionSource, Does.Not.Contain("GetService"));
            Assert.That(compositionSource, Does.Not.Contain("Resolve<"));
            Assert.That(compositionSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(compositionSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(compositionSource, Does.Not.Contain("Object.Find"));
            Assert.That(compositionSource, Does.Not.Contain("GameObject.Find"));
            foreach (var forbiddenPassiveMethod in new[]
                     {
                         " Present(",
                         " Update(",
                         " UpdatePresentation(",
                         " ResetSession(",
                         " HardCleanup(",
                         " Dispose(",
                         " Normalize(",
                         " ShouldUseProduction",
                         " ShouldSuppress",
                         " TryBeginExecution",
                         " RecordSkippedByPolicy",
                     })
            {
                Assert.That(compositionSource, Does.Not.Contain(forbiddenPassiveMethod), forbiddenPassiveMethod);
            }

            foreach (var forbiddenCoordinatorToken in new[]
                     {
                         "new EnemyOneShotAudioLaneRuntime",
                         "new GameplayActionAudioLaneRuntime",
                         "new CoreGameplaySfxLaneRuntime",
                         "new PlayerActionAnimationLaneRuntime",
                         "new EnemyPresentationLaneRuntime",
                         "new BoxMotionPresentationLaneRuntime",
                         "new TopologyPresentationLaneRuntime",
                         "new DamageDeathVfxPresentationLaneRuntime",
                         "GameplayHostPresentationPipelineFactory",
                         "TopologyExecutionPipelineFactory ",
                         "DamageDeathVfxExecutionPipelineFactory ",
                         "BoxMotionExecutionPipelineFactory ",
                         "public GameplayTickPresentationCoordinator(IDamageDeathVfxPlaybackPort",
                         "TopologyPresentationExecutionMode topologyPresentationExecutionMode)",
                         "GameplayPresentationRuntimeCompositionFactory.Create",
                         "public GameplayTickPresentationCoordinator()",
                     })
            {
                Assert.That(coordinatorSource, Does.Not.Contain(forbiddenCoordinatorToken), forbiddenCoordinatorToken);
            }

            Assert.That(coordinatorSource, Does.Contain(
                "internal GameplayTickPresentationCoordinator(GameplayPresentationRuntimeComposition composition)"));
            Assert.That(presenterSource, Does.Contain("BindCoordinator(GameplayTickPresentationCoordinator coordinator)"));
            Assert.That(presenterSource, Does.Not.Contain("new GameplayTickPresentationCoordinator"));
            Assert.That(presenterSource, Does.Not.Contain("GameplayPresentationRuntimeCompositionFactory"));
            Assert.That(hostConstructionBlock, Does.Contain("DiscoverPresentationHostDependencies(hostObject)"));
            Assert.That(hostConstructionBlock, Does.Contain("GameplayPresentationRuntimeCompositionFactory.Create("));
            Assert.That(hostConstructionBlock, Does.Contain("DamageDeathVfxPlaybackPort = presentationDependencies.DamageDeathVfxPlaybackPort"));
            Assert.That(hostConstructionBlock, Does.Contain("new GameplayTickPresentationCoordinator(presentationComposition)"));
            Assert.That(hostConstructionBlock, Does.Contain("presenter.BindCoordinator(presentationCoordinator)"));
            Assert.That(hostFactorySource, Does.Not.Contain("presenter.ConfigureDamageDeathVfxExecution("));
            Assert.That(hostFactorySource, Does.Contain("AttachPresentationExtensions(presenter, presentationDependencies.PresentationExtensions)"));
            Assert.That(CountOccurrences(hostFactorySource, "GetComponents<MonoBehaviour>()"), Is.EqualTo(1));
            Assert.That(compositionFactorySource, Does.Contain("public IDamageDeathVfxPlaybackPort DamageDeathVfxPlaybackPort { get; set; }"));
            Assert.That(compositionFactorySource, Does.Contain("options.DamageDeathVfxPlaybackPort"));
            Assert.That(compositionFactorySource, Does.Not.Contain("damageDeathVfxLane.ConfigureExecution("));
            Assert.That(compositionFactorySource, Does.Not.Contain("DamageDeathVfxExecutionPolicy.ProductionDefault,"));
            Assert.That(compositionFactorySource, Does.Contain("options.DamageDeathVfxPlaybackPort"));
            Assert.That(compositionSource, Does.Not.Contain("GameplayVfxProductionRuntime"));
            Assert.That(compositionSource, Does.Not.Contain("GameplayVfxGameObjectPool"));
            Assert.That(compositionSource, Does.Not.Contain("GameplayVfxPresentationController"));
            Assert.That(presenterSource, Does.Not.Contain("GameplayVfxProductionRuntime"));
            Assert.That(presenterSource, Does.Not.Contain("IDamageDeathGameplayVfxPlaybackRuntime"));
            Assert.That(presenterSource, Does.Not.Contain("GameplayVfxGameObjectPool"));

            foreach (var forbiddenFactoryPolicyToken in new[]
                     {
                         "TryBeginExecution",
                         "RecordSkippedByPolicy",
                         "ShouldSuppress",
                         "Present(",
                         "Update(",
                         "ResetSession(",
                         "HardCleanup(",
                         "DamageHitOmittedByEnemyDeathCount",
                     })
            {
                Assert.That(compositionFactorySource, Does.Not.Contain(forbiddenFactoryPolicyToken), forbiddenFactoryPolicyToken);
                Assert.That(hostConstructionBlock, Does.Not.Contain(forbiddenFactoryPolicyToken), forbiddenFactoryPolicyToken);
            }

            foreach (var forbiddenDiscoveryToken in new[]
                     {
                         "GameObject.Find",
                         "FindObjectOfType",
                         "FindObjectsByType",
                         "Resources.Load",
                     })
            {
                Assert.That(hostFactorySource, Does.Not.Contain(forbiddenDiscoveryToken), forbiddenDiscoveryToken);
            }
        }

        [Test]
        [Category("Core")]
        public void CoordinatorFinalZeroGuard_UsesCompletedLanesWithoutDomainResidue()
        {
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var presenterSource = ReadRepoFile(PresenterPath);
            var hostFactorySource = ReadRepoFile(HostFactoryPath);
            var compositionSource = ReadRepoFile(CompositionPath);
            var compositionFactorySource = ReadRepoFile(CompositionFactoryPath);
            var coordinatorFields = typeof(GameplayTickPresentationCoordinator)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var coordinatorFieldTypes = coordinatorFields.Select(field => field.FieldType).ToArray();
            var compositionProperties = typeof(GameplayPresentationRuntimeComposition)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            var constructor = typeof(GameplayTickPresentationCoordinator)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Single();
            Assert.That(constructor.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[]
            {
                typeof(GameplayPresentationRuntimeComposition),
            }));

            foreach (var laneType in new[]
                     {
                         typeof(TopologyPresentationLaneRuntime),
                         typeof(DamageDeathVfxPresentationLaneRuntime),
                         typeof(BoxMotionPresentationLaneRuntime),
                         typeof(PlayerActionAnimationLaneRuntime),
                         typeof(EnemyPresentationLaneRuntime),
                         typeof(CoreGameplaySfxLaneRuntime),
                         typeof(GameplayActionAudioLaneRuntime),
                         typeof(EnemyOneShotAudioLaneRuntime),
                     })
            {
                Assert.That(coordinatorFieldTypes, Has.Member(laneType), laneType.Name);
            }

            foreach (var forbiddenModeType in new[]
                     {
                         typeof(TopologyPresentationExecutionMode),
                         typeof(PlayerActionAnimationExecutionMode),
                         typeof(EnemyPresentationExecutionMode),
                         typeof(CoreGameplaySfxRoute),
                     })
            {
                Assert.That(coordinatorFieldTypes, Has.No.Member(forbiddenModeType), forbiddenModeType.Name);
            }

            foreach (var field in coordinatorFields)
            {
                var typeName = field.FieldType.Name;
                var isAllowedSharedAudioSeam =
                    field.FieldType == typeof(IGameplayAudioPlaybackPort) ||
                    field.FieldType == typeof(GameplaySfxArbitratingPlaybackPort);
                Assert.That(typeName, Does.Not.Contain("ExecutionGuard"), field.Name);
                Assert.That(typeName, Does.Not.Contain("PipelineFactory"), field.Name);
                Assert.That(typeName, Does.Not.Contain("ExecutionPipeline"), field.Name);
                Assert.That(typeName, Does.Not.Contain("PlaybackPortAdapter"), field.Name);
                if (!isAllowedSharedAudioSeam)
                {
                    Assert.That(typeName, Does.Not.Contain("PlaybackPort"), field.Name);
                    Assert.That(typeName, Does.Not.Contain("CleanupPort"), field.Name);
                }

                Assert.That(field.Name, Does.Not.Contain("ExecutionMode"), field.Name);
                Assert.That(field.Name, Does.Not.Contain("ExecutionPolicy"), field.Name);
                Assert.That(field.Name, Does.Not.Contain("Normalize"), field.Name);
            }

            foreach (var property in compositionProperties)
            {
                var typeName = property.PropertyType.Name;
                Assert.That(typeName, Does.Not.Contain("ExecutionMode"), property.Name);
                Assert.That(typeName, Does.Not.Contain("ExecutionPolicy"), property.Name);
                Assert.That(typeName, Does.Not.Contain("PipelineFactory"), property.Name);
                Assert.That(typeName, Does.Not.Contain("ExecutionGuard"), property.Name);
                Assert.That(typeName, Does.Not.Contain("PlaybackPort"), property.Name);
                Assert.That(typeName, Does.Not.Contain("CleanupPort"), property.Name);
            }

            var composition = GameplayPresentationRuntimeCompositionFactory.Create();
            Assert.That(composition.TopologyLane.ExecutionMode, Is.EqualTo(TopologyPresentationExecutionDefaults.ProductionDefault));
            Assert.That(composition.DamageDeathVfxLane, Is.Not.Null);
            Assert.That(composition.DamageDeathVfxLane.ExecutorDiagnostics.IsProductionDefaultOwner, Is.False);
            Assert.That(composition.BoxMotionLane, Is.Not.Null);
            Assert.That(composition.BoxMotionLane.ExecutorDiagnostics.IsCurrentProductionOwner, Is.False);
            Assert.That(composition.PlayerActionAnimationLane.ExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionPolicy.ProductionDefault));
            Assert.That(composition.EnemyPresentationLane.ExecutionMode, Is.EqualTo(EnemyPresentationExecutionPolicy.ProductionDefault));
            Assert.That(composition.CoreGameplaySfxLane, Is.Not.Null);
            Assert.That(composition.CoreGameplaySfxLane.ExecutorDiagnostics.IsProductionDefaultOwner, Is.True);
            Assert.That(composition.GameplayActionAudioLane, Is.Not.Null);
            Assert.That(composition.GameplayActionAudioLane.ExecutorDiagnostics.ObservedCueCount, Is.Zero);
            Assert.That(composition.EnemyOneShotAudioLane, Is.Not.Null);
            Assert.That(composition.EnemyOneShotAudioLane.ExecutorDiagnostics.ObservedCueCount, Is.Zero);

            foreach (var forbiddenCoordinatorToken in new[]
                     {
                         "ExecutionPolicy.Normalize",
                         "new GameplayPresentationPipeline",
                         "CreateDamageDeathVfxExecutionPipeline",
                         "CreateBoxMotionExecutionPipeline",
                         "CreatePlayerActionAnimationExecutionPipeline",
                         "CreateEnemyPresentationExecutionPipeline",
                         "CreateCoreGameplaySfxExecutionPipeline",
                         "CreateActionAudioExecutionPipeline",
                         "CreateEnemyAudioExecutionPipeline",
                         "BuildDamageDeathVfxPlaybackKeys",
                         "BuildBoxMotionPlaybackKeys",
                         "BuildPlayerActionAnimationPlaybackKeys",
                         "BuildEnemyPresentationPlaybackKeys",
                         "BuildCoreGameplaySfxPlaybackKeys",
                         "BuildActionAudioPlaybackKeys",
                         "BuildEnemyAudioPlaybackKeys",
                         "RecordSkippedByPolicy(",
                         "TryBeginExecution(",
                         "RecordSameTickDamageHitOmittedByDeath",
                         "SuppressLethalEnemyDamageRequests",
                         "ConfigureEnemyDeathCueSuppression",
                         "DamageDeathGameplayVfxPlaybackPortAdapter",
                         "GameplayVfxGameObjectPool",
                         "ParticleSystem.",
                         "AudioSource.",
                         "Animator.Set",
                         "Animator.Play",
                     })
            {
                Assert.That(coordinatorSource, Does.Not.Contain(forbiddenCoordinatorToken), forbiddenCoordinatorToken);
            }

            Assert.That(hostFactorySource, Does.Not.Contain("presenter.ConfigureDamageDeathVfxExecution("));
            Assert.That(hostFactorySource, Does.Not.Contain("presentationCoordinator.ConfigureDamageDeathVfxExecution("));
            Assert.That(CountOccurrences(hostFactorySource, "ConfigureDamageDeathVfxExecution("), Is.Zero);
            Assert.That(CountOccurrences(hostFactorySource, "ConfigureBoxMotionPresentationExecution("), Is.Zero);
            Assert.That(CountOccurrences(hostFactorySource, "ConfigurePlayerActionAnimationExecution("), Is.Zero);
            Assert.That(CountOccurrences(hostFactorySource, "ConfigureEnemyPresentationExecution("), Is.Zero);
            Assert.That(CountOccurrences(hostFactorySource, "ConfigureCoreGameplaySfxExecution("), Is.Zero);
            Assert.That(CountOccurrences(hostFactorySource, "ConfigureActionAudioExecution("), Is.Zero);
            Assert.That(CountOccurrences(hostFactorySource, "ConfigureEnemyAudioExecution("), Is.Zero);
            Assert.That(presenterSource, Does.Not.Contain("GameplayPresentationRuntimeCompositionFactory"));
            Assert.That(presenterSource, Does.Not.Contain("new GameplayTickPresentationCoordinator"));
            Assert.That(compositionSource, Does.Not.Contain("IDamageDeathVfxPlaybackPort"));
            Assert.That(compositionFactorySource, Does.Contain("ConfigureProductionDefaultExecutionGuards("));
            Assert.That(compositionFactorySource, Does.Contain("new DamageDeathVfxPresentationLaneRuntime("));
            Assert.That(compositionFactorySource, Does.Not.Contain("damageDeathVfxLane.ConfigureExecution("));
            Assert.That(compositionFactorySource, Does.Not.Contain("TryBeginExecution("));
            Assert.That(compositionFactorySource, Does.Not.Contain("RecordSkippedByPolicy("));
            Assert.That(compositionFactorySource, Does.Not.Contain("ShouldSuppress"));
        }

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
        public void ResolvedVisibility_UsesPresentationVisibilitySourceKind_NotPoseSourceKind()
        {
            var visibilityBlock = ExtractSourceBetween(
                ReadRepoFile(ContractsPath),
                "public enum PresentationVisibilitySourceKind",
                "public sealed class PresentationFactFrame");
            var provenancePropertyTypes = typeof(PresentationVisibilityProvenance)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType)
                .ToArray();
            var visibilityPropertyTypes = typeof(ResolvedEntityPresentationVisibility)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType)
                .ToArray();

            Assert.That(typeof(PresentationVisibilitySourceKind).IsEnum, Is.True);
            Assert.That(
                typeof(PresentationVisibilityProvenance)
                    .GetProperty(nameof(PresentationVisibilityProvenance.SourceKind))
                    ?.PropertyType,
                Is.EqualTo(typeof(PresentationVisibilitySourceKind)));
            Assert.That(provenancePropertyTypes, Has.Member(typeof(PresentationVisibilitySourceKind)));
            Assert.That(provenancePropertyTypes, Has.No.Member(typeof(PresentationPoseSourceKind)));
            Assert.That(visibilityPropertyTypes, Has.No.Member(typeof(PresentationPoseSourceKind)));
            Assert.That(visibilityBlock, Does.Contain("PresentationVisibilitySourceKind"));
            Assert.That(visibilityBlock, Does.Not.Contain("PresentationPoseSourceKind"));
        }

        [Test]
        [Category("Core")]
        public void ResolvedVisibility_DoesNotIncludeTopologyBridgeBinding()
        {
            var visibilityBlock = ExtractSourceBetween(
                ReadRepoFile(ContractsPath),
                "public enum PresentationVisibilitySourceKind",
                "public sealed class PresentationFactFrame");

            Assert.That(visibilityBlock, Does.Not.Contain("TopologyVisualBridgeBinding"));
            Assert.That(visibilityBlock, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
            Assert.That(
                typeof(ResolvedEntityPresentationVisibility).Assembly,
                Is.Not.EqualTo(typeof(TopologyVisualBridgeVisibilityController).Assembly));
        }

        [Test]
        [Category("Core")]
        public void PresentationVisibilitySourceKind_Tombstones_AreRetainedForCompatibilityOnly()
        {
            const string committedName = "CommittedMotionFallback";
            const string genericName = "GenericVisibility";
            var committedToken = "PresentationVisibilitySourceKind." + committedName;
            var genericToken = "PresentationVisibilitySourceKind." + genericName;
            var contractsSource = ReadRepoFile(ContractsPath);
            var visibilityEnumBlock = ExtractSourceBetween(
                contractsSource,
                "public enum PresentationVisibilitySourceKind",
                "public readonly struct PresentationVisibilityProvenance");
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateCollector",
                "internal sealed class PresentationResolvedVisibilityResolver");
            var committedKind = (PresentationVisibilitySourceKind)Enum.Parse(
                typeof(PresentationVisibilitySourceKind),
                committedName);
            var genericKind = (PresentationVisibilitySourceKind)Enum.Parse(
                typeof(PresentationVisibilitySourceKind),
                genericName);

            Assert.That(committedKind, Is.EqualTo((PresentationVisibilitySourceKind)8));
            Assert.That(genericKind, Is.EqualTo((PresentationVisibilitySourceKind)10));
            Assert.That(visibilityEnumBlock, Does.Contain("Reserved tombstone"));
            Assert.That(visibilityEnumBlock, Does.Contain("Retained for public enum/numeric compatibility"));
            Assert.That(visibilityEnumBlock, Does.Contain("do not use for new visibility candidates"));
            Assert.That(visibilityEnumBlock, Does.Contain("CommittedMotionFallback = 8"));
            Assert.That(visibilityEnumBlock, Does.Contain("GenericVisibility = 10"));
            Assert.That(collectorBlock, Does.Not.Match(ExactSourceKindPattern(committedToken)));
            Assert.That(collectorBlock, Does.Not.Match(ExactSourceKindPattern(genericToken)));
            Assert.That(
                FindGameplayTestFilesContainingExactToken(committedToken),
                Is.Empty,
                "Normal gameplay tests must not keep the committed-motion tombstone as fixture vocabulary.");
            Assert.That(
                FindGameplayTestFilesContainingExactToken(genericToken),
                Is.Empty,
                "Normal gameplay tests must not use the generic visibility umbrella tombstone as fixture vocabulary.");
        }

        [Test]
        [Category("Core")]
        public void EntityVisibilityResolver_DoesNotReadTopologyVisualBridgeController()
        {
            var hostRuntimeFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", HostRuntimeDirectory));
            var offenders = Directory.GetFiles(hostRuntimeFullPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path) != "TopologyVisualBridgeVisibilityController.cs")
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return (source.Contains("ResolvedEntityPresentationVisibility") ||
                            source.Contains("ResolvedPresentationVisibilitySet")) &&
                           source.Contains("TopologyVisualBridgeVisibilityController");
                })
                .Select(path => Path.GetRelativePath(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), path))
                .ToArray();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void TopologyBridgeVisibility_DoesNotConsumeResolvedEntityPresentationVisibility()
        {
            var source = ReadRepoFile(TopologyBridgeVisibilityControllerPath);

            Assert.That(source, Does.Not.Contain("ResolvedEntityPresentationVisibility"));
            Assert.That(source, Does.Not.Contain("ResolvedPresentationVisibilitySet"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateSet"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilitySourceKind"));
        }

        [Test]
        [Category("Core")]
        public void ResolvedVisibilityPayload_CarriesLifetimeOrCleanupSignature()
        {
            var provenance = new PresentationVisibilityProvenance(
                PresentationVisibilitySourceKind.JumpDetached,
                PresentationOwnerRole.Enemy,
                new SurfaceCell(FaceId.Floor, 1, 2),
                FaceId.Floor,
                lifetimeToken: 77);
            var visibility = new ResolvedEntityPresentationVisibility(
                new PresentationEntityKey(12),
                isVisible: false,
                provenance);
            var visibilitySet = new ResolvedPresentationVisibilitySet();

            visibilitySet.SetVisibility(visibility);

            Assert.That(
                typeof(PresentationVisibilityProvenance)
                    .GetProperty(nameof(PresentationVisibilityProvenance.LifetimeToken))
                    ?.PropertyType,
                Is.EqualTo(typeof(int)));
            Assert.That(provenance.LifetimeToken, Is.EqualTo(77));
            Assert.That(visibility.EntityKey.EntityId, Is.EqualTo(12));
            Assert.That(visibility.IsVisible, Is.False);
            Assert.That(visibility.IsValid, Is.True);
            Assert.That(visibilitySet.EntityIds, Is.EqualTo(new[] { 12 }));
            Assert.That(visibilitySet.TryGetVisibility(12, out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(visibility));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateSet_PreservesSourcePriorityFlagsAndTrackMetadata()
        {
            var entityKey = new PresentationEntityKey(12);
            var trackProvenance = new PresentationVisibilityProvenance(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                PresentationOwnerRole.Enemy,
                new SurfaceCell(FaceId.Floor, 1, 2),
                FaceId.Floor,
                lifetimeToken: 77);
            var fallbackProvenance = new PresentationVisibilityProvenance(
                PresentationVisibilitySourceKind.JumpDetached,
                PresentationOwnerRole.Enemy,
                null,
                null,
                lifetimeToken: 77);
            var suppressionProvenance = new PresentationVisibilityProvenance(
                PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression,
                PresentationOwnerRole.Enemy,
                null,
                FaceId.Floor,
                lifetimeToken: 77);
            var set = new PresentationVisibilityCandidateSet();

            set.AddCandidate(new PresentationVisibilityCandidate(
                entityKey,
                isVisible: true,
                trackProvenance,
                priority: 500,
                isFallback: false,
                isStatefulTrackSample: true,
                isHighPrioritySuppressionSource: false));
            set.AddCandidate(new PresentationVisibilityCandidate(
                entityKey,
                isVisible: true,
                fallbackProvenance,
                priority: 0,
                isFallback: true,
                isStatefulTrackSample: false,
                isHighPrioritySuppressionSource: false));
            set.AddCandidate(new PresentationVisibilityCandidate(
                entityKey,
                isVisible: false,
                suppressionProvenance,
                priority: 900,
                isFallback: false,
                isStatefulTrackSample: false,
                isHighPrioritySuppressionSource: true));

            Assert.That(set.Count, Is.EqualTo(1));
            Assert.That(set.CandidateCount, Is.EqualTo(3));
            Assert.That(set.EntityIds, Is.EqualTo(new[] { 12 }));
            Assert.That(set.TryGetCandidates(12, out var candidates), Is.True);
            Assert.That(candidates, Has.Count.EqualTo(3));
            Assert.That(candidates[0].Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
            Assert.That(candidates[0].IsStatefulTrackSample, Is.True);
            Assert.That(candidates[1].Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(candidates[1].IsFallback, Is.True);
            Assert.That(candidates[2].Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression));
            Assert.That(candidates[2].IsHighPrioritySuppressionSource, Is.True);
            Assert.That(candidates[2].Priority, Is.GreaterThan(candidates[0].Priority));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateSet_CanRepresentGenericRemoveDetachSpawnPriority()
        {
            var sourceKinds = Enum.GetValues(typeof(PresentationVisibilitySourceKind))
                .Cast<PresentationVisibilitySourceKind>()
                .ToArray();

            Assert.That(sourceKinds, Has.Member(PresentationVisibilitySourceKind.GenericVisibilityRemove));
            Assert.That(sourceKinds, Has.Member(PresentationVisibilitySourceKind.GenericVisibilityDetach));
            Assert.That(sourceKinds, Has.Member(PresentationVisibilitySourceKind.GenericVisibilitySpawn));

            var set = new PresentationVisibilityCandidateSet();
            set.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                priority: 100));
            set.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityDetach,
                priority: 200));
            set.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityRemove,
                priority: 300));

            Assert.That(set.TryGetCandidates(31, out var candidates), Is.True);
            Assert.That(candidates.Select(candidate => candidate.Provenance.SourceKind), Is.EqualTo(new[]
            {
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                PresentationVisibilitySourceKind.GenericVisibilityDetach,
                PresentationVisibilitySourceKind.GenericVisibilityRemove,
            }));
            Assert.That(
                candidates.Single(candidate =>
                    candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityRemove)
                    .Priority,
                Is.GreaterThan(candidates.Single(candidate =>
                    candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityDetach)
                    .Priority));
            Assert.That(
                candidates.Single(candidate =>
                    candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityDetach)
                    .Priority,
                Is.GreaterThan(candidates.Single(candidate =>
                    candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilitySpawn)
                    .Priority));
        }

        [Test]
        [Category("Core")]
        public void ResolvedVisibilityFinalSet_RemainsSingleWinnerContract()
        {
            var visibilitySet = new ResolvedPresentationVisibilitySet();
            var entityKey = new PresentationEntityKey(12);
            var first = new ResolvedEntityPresentationVisibility(
                entityKey,
                isVisible: true,
                new PresentationVisibilityProvenance(
                    PresentationVisibilitySourceKind.JumpDetached,
                    PresentationOwnerRole.Enemy,
                    null,
                    null,
                    lifetimeToken: 77));
            var winner = new ResolvedEntityPresentationVisibility(
                entityKey,
                isVisible: false,
                new PresentationVisibilityProvenance(
                    PresentationVisibilitySourceKind.VisibilityTrackSample,
                    PresentationOwnerRole.Enemy,
                    null,
                    null,
                    lifetimeToken: 77));

            visibilitySet.SetVisibility(first);
            visibilitySet.SetVisibility(winner);

            Assert.That(visibilitySet.Count, Is.EqualTo(1));
            Assert.That(visibilitySet.EntityIds, Is.EqualTo(new[] { 12 }));
            Assert.That(visibilitySet.TryGetVisibility(12, out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(winner));
        }

        [Test]
        [Category("Core")]
        public void VisibilityFallbackHelper_MatchesExistingApplierFallback_ForCommittedPose()
        {
            var inputs = CreateVisibilityFallbackInputs(hasCommittedLocalTargetPose: true);

            Assert.That(PresentationVisibilityFallbackResolver.Resolve(inputs), Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityFallbackHelper_MatchesExistingApplierFallback_ForResolvedJumpDetached()
        {
            var visibleInputs = CreateVisibilityFallbackInputs(
                hasResolvedVisibility: true,
                isResolvedVisible: true);
            var hiddenInputs = CreateVisibilityFallbackInputs(
                hasResolvedVisibility: true,
                isResolvedVisible: false);

            Assert.That(PresentationVisibilityFallbackResolver.Resolve(visibleInputs), Is.True);
            Assert.That(PresentationVisibilityFallbackResolver.Resolve(hiddenInputs), Is.False);
        }

        [Test]
        [Category("Core")]
        public void VisibilityFallbackHelper_MatchesExistingApplierFallback_ForTransitionVisibility()
        {
            var inputs = CreateVisibilityFallbackInputs(hasTransitionVisibility: true);

            Assert.That(PresentationVisibilityFallbackResolver.Resolve(inputs), Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityFallbackHelper_MatchesExistingApplierFallback_ForRetainedDeathExit()
        {
            Assert.That(
                PresentationVisibilityFallbackResolver.Resolve(
                    CreateVisibilityFallbackInputs(isDeferredExitRetained: true)),
                Is.True);
            Assert.That(
                PresentationVisibilityFallbackResolver.Resolve(
                    CreateVisibilityFallbackInputs(isContactDelayedRetained: true)),
                Is.True);
            Assert.That(
                PresentationVisibilityFallbackResolver.Resolve(
                    CreateVisibilityFallbackInputs(isDeathPresentationPlaying: true)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityFallbackHelper_DoesNotSampleOrAdvanceVisibilityTrack()
        {
            var source = ReadRepoFile(TrackStatePath);
            var helperBlock = ExtractSourceBetween(
                source,
                "internal static class PresentationVisibilityFallbackResolver",
                "internal sealed class PresentationVisibilityCandidateCollector");

            Assert.That(helperBlock, Does.Not.Contain("VisibilityTracks"));
            Assert.That(helperBlock, Does.Not.Contain("SampleAndAdvance"));
            Assert.That(helperBlock, Does.Not.Contain("SampleWithoutAdvance"));
            Assert.That(helperBlock, Does.Not.Contain("CompletedVisibilityTrackIds"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityFallbackHelper_DoesNotConsumePresentationVisibilityCandidateSet()
        {
            var source = ReadRepoFile(TrackStatePath);
            var helperBlock = ExtractSourceBetween(
                source,
                "internal readonly struct PresentationVisibilityFallbackInputs",
                "internal sealed class PresentationVisibilityCandidateCollector");

            Assert.That(helperBlock, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(helperBlock, Does.Not.Contain("PresentationVisibilityCandidateSet"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityFallbackHelper_DoesNotReadTopologyBridgeBindings()
        {
            var source = ReadRepoFile(TrackStatePath);
            var helperBlock = ExtractSourceBetween(
                source,
                "internal readonly struct PresentationVisibilityFallbackInputs",
                "internal sealed class PresentationVisibilityCandidateCollector");

            Assert.That(helperBlock, Does.Not.Contain("TopologyVisualBridgeBinding"));
            Assert.That(helperBlock, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityFallbackHelper_DoesNotOwnCleanupLifecycle()
        {
            var source = ReadRepoFile(TrackStatePath);
            var helperBlock = ExtractSourceBetween(
                source,
                "internal readonly struct PresentationVisibilityFallbackInputs",
                "internal sealed class PresentationVisibilityCandidateCollector");

            Assert.That(helperBlock, Does.Not.Contain("ClearEntityPresentationMetadataIfFullyHidden"));
            Assert.That(helperBlock, Does.Not.Contain("RetainedLocalTargetPoses.Remove"));
            Assert.That(helperBlock, Does.Not.Contain("TickVisibilityChange"));
            Assert.That(helperBlock, Does.Not.Contain("EntityExitSignals"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_UsesVisibilityFallbackHelperBeforeVisibilityTrackAdvance()
        {
            var source = ReadRepoFile(ApplierPath);
            var helperIndex = source.IndexOf(
                "PresentationVisibilityFallbackResolver.Resolve",
                StringComparison.Ordinal);
            var advanceIndex = source.IndexOf(
                "visibilityTrack.AdvanceAndReportCompletion(deltaTime)",
                StringComparison.Ordinal);

            Assert.That(helperIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(advanceIndex, Is.GreaterThan(helperIndex));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotConsumeVisibilityCandidateSet()
        {
            var source = ReadRepoFile(ApplierPath);

            Assert.That(source, Does.Contain("ResolvedPresentationVisibilitySet"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateSet"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateSet_ClearsEveryPresentationUpdate()
        {
            var source = ReadRepoFile(CoordinatorPath);

            var candidateClearIndex = source.IndexOf(
                "_presentationVisibilityCandidates.Clear();",
                StringComparison.Ordinal);
            var finalClearIndex = source.IndexOf(
                "_resolvedPresentationVisibility.Clear();",
                StringComparison.Ordinal);
            var collectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectJumpDetachedVisibility",
                StringComparison.Ordinal);
            var resolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var shadowCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectVisibilityTrackSamples",
                StringComparison.Ordinal);
            var applyIndex = source.IndexOf(
                "_entityPresentationApplier.Apply",
                StringComparison.Ordinal);

            Assert.That(source, Does.Contain("PresentationVisibilityCandidateSet _presentationVisibilityCandidates"));
            Assert.That(candidateClearIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(finalClearIndex, Is.GreaterThan(candidateClearIndex));
            Assert.That(collectIndex, Is.GreaterThan(finalClearIndex));
            Assert.That(shadowCollectIndex, Is.GreaterThan(collectIndex));
            Assert.That(resolveIndex, Is.GreaterThan(shadowCollectIndex));
            Assert.That(applyIndex, Is.GreaterThan(shadowCollectIndex));
        }

        [Test]
        [Category("Core")]
        public void PresentationResolvedVisibilityResolver_UsesCandidateSetBeforeFinalSet()
        {
            var source = ReadRepoFile(TrackStatePath);
            var resolverBlock = ExtractSourceBetween(
                source,
                "internal sealed class PresentationResolvedVisibilityResolver",
                "internal sealed class PresentationPoseCandidateCollector");

            Assert.That(resolverBlock, Does.Contain("ResolveCandidates("));
            Assert.That(resolverBlock, Does.Contain("PresentationVisibilityCandidateSet candidateSet"));
            Assert.That(resolverBlock, Does.Contain("ResolvedPresentationVisibilitySet visibilitySet"));
            Assert.That(resolverBlock, Does.Contain("PresentationVisibilityCandidateWinnerResolver"));
            Assert.That(resolverBlock, Does.Contain("_winnerResolver.ResolveWinner(candidates)"));
            Assert.That(resolverBlock, Does.Contain("visibilitySet.SetVisibility"));
            Assert.That(resolverBlock, Does.Not.Contain("JumpDetachedVisibilityStates"));
            Assert.That(resolverBlock, Does.Not.Contain("VisibilityTracks"));
            Assert.That(resolverBlock, Does.Not.Contain("CompletedVisibilityTrackIds"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateSet_DoesNotOwnCleanupLifecycle()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateCollector",
                "internal sealed class PresentationResolvedVisibilityResolver");
            var resolverBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationResolvedVisibilityResolver",
                "internal sealed class PresentationPoseCandidateCollector");
            var candidateLifecycleSource = collectorBlock + "\n" + resolverBlock;

            Assert.That(candidateLifecycleSource, Does.Not.Contain("CompletedVisibilityTrackIds"));
            Assert.That(candidateLifecycleSource, Does.Not.Contain("ClearEntityPresentationMetadataIfFullyHidden"));
            Assert.That(candidateLifecycleSource, Does.Not.Contain("RetainedLocalTargetPoses.Remove"));
            Assert.That(candidateLifecycleSource, Does.Not.Contain("EntityExitSignals"));
            Assert.That(candidateLifecycleSource, Does.Not.Contain("Audio"));
            Assert.That(candidateLifecycleSource, Does.Not.Contain("VFX"));
            Assert.That(candidateLifecycleSource, Does.Not.Contain("Mapper"));
        }

        [Test]
        [Category("Core")]
        public void PresentationVisibilityCandidateCollector_CollectsPostResolveVisibilityCarriers()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateCollector",
                "internal sealed class PresentationResolvedVisibilityResolver");

            Assert.That(collectorBlock, Does.Contain("CollectPostResolveVisibilityCarriers"));
            Assert.That(collectorBlock, Does.Contain("IReadOnlyList<TickVisibilityChange>"));
            Assert.That(collectorBlock, Does.Contain("PresentationVisibilitySourceKind.GenericVisibilitySpawn"));
            Assert.That(collectorBlock, Does.Contain("PresentationVisibilitySourceKind.GenericVisibilityDetach"));
            Assert.That(collectorBlock, Does.Contain("PresentationVisibilitySourceKind.GenericVisibilityRemove"));
            Assert.That(collectorBlock, Does.Contain("TickVisibilityChangeKind.Spawn => 200"));
            Assert.That(collectorBlock, Does.Contain("TickVisibilityChangeKind.Detach => 300"));
            Assert.That(collectorBlock, Does.Contain("TickVisibilityChangeKind.Remove => 400"));
        }

        [Test]
        [Category("Core")]
        public void PresentationVisibilityCandidateCollector_CollectsGenericVisibilitySpawnOnly()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var spawnOnlyBlock = ExtractSourceBetween(
                trackStateSource,
                "public void CollectGenericVisibilitySpawnOnly",
                "public void CollectRetainedDeathOrExitVisibility");

            Assert.That(spawnOnlyBlock, Does.Contain("IReadOnlyList<TickVisibilityChange>"));
            Assert.That(spawnOnlyBlock, Does.Contain("change.ChangeKind != TickVisibilityChangeKind.Spawn"));
            Assert.That(spawnOnlyBlock, Does.Contain("TryCreateGenericVisibilityCandidate"));
            Assert.That(spawnOnlyBlock, Does.Contain("suppressedSpawnEntityIds"));
            Assert.That(spawnOnlyBlock, Does.Contain("TickVisibilityChangeKind.Detach"));
            Assert.That(spawnOnlyBlock, Does.Contain("TickVisibilityChangeKind.Remove"));
            Assert.That(spawnOnlyBlock, Does.Not.Contain("CollectVisibilityTrackSamples"));
            Assert.That(spawnOnlyBlock, Does.Not.Contain("ResolvedPresentationVisibilitySet"));
        }

        [Test]
        [Category("Core")]
        public void PresentationVisibilityCandidateCollector_CollectsTransitionEntityVisibility()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateCollector",
                "internal sealed class PresentationResolvedVisibilityResolver");

            Assert.That(collectorBlock, Does.Contain("CollectTransitionEntityVisibility"));
            Assert.That(collectorBlock, Does.Contain("_stateStore.TransitionVisibilityStates"));
            Assert.That(collectorBlock, Does.Contain("PresentationVisibilitySourceKind.TransitionEntityVisibility"));
            Assert.That(collectorBlock, Does.Contain("state.SurfaceFace"));
            Assert.That(collectorBlock, Does.Contain("priority: 600"));
            Assert.That(collectorBlock, Does.Contain("isHighPrioritySuppressionSource: false"));
        }

        [Test]
        [Category("Core")]
        public void PresentationVisibilityCandidateCollector_DoesNotRemoveTransitionVisibilityStates()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "public void CollectTransitionEntityVisibility",
                "public void CollectVisibilityTrackSamples");

            Assert.That(collectorBlock, Does.Contain("TransitionVisibilityStates"));
            Assert.That(collectorBlock, Does.Not.Contain("TransitionVisibilityStates.Remove"));
            Assert.That(collectorBlock, Does.Not.Contain("TransitionVisibilityStates.Clear"));
            Assert.That(collectorBlock, Does.Not.Contain("CompletedTransitionVisibilityStateIds"));
        }

        [Test]
        [Category("Core")]
        public void PresentationVisibilityCandidateCollector_DoesNotRemoveTickVisibilityChangeEvents()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "public void CollectPostResolveVisibilityCarriers",
                "public void CollectVisibilityTrackSamples");

            Assert.That(collectorBlock, Does.Contain("TickVisibilityChange"));
            Assert.That(collectorBlock, Does.Not.Contain("visibilityChanges.Remove"));
            Assert.That(collectorBlock, Does.Not.Contain("visibilityChanges.Clear"));
            Assert.That(collectorBlock, Does.Not.Contain("presentationData.VisibilityChanges"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateCollector_DoesNotCallSampleAndAdvance()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateCollector",
                "internal sealed class PresentationResolvedVisibilityResolver");

            Assert.That(collectorBlock, Does.Not.Contain("SampleAndAdvance"));
        }

        [Test]
        [Category("Core")]
        public void PresentationVisibilityCandidateCollector_UsesSampleWithoutAdvanceForVisibilityTracks()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateCollector",
                "internal sealed class PresentationResolvedVisibilityResolver");

            Assert.That(collectorBlock, Does.Contain("CollectVisibilityTrackSamples"));
            Assert.That(collectorBlock, Does.Contain("visibilityTrack.SampleWithoutAdvance"));
            Assert.That(collectorBlock, Does.Contain("PresentationVisibilityFallbackResolver.Resolve"));
            Assert.That(collectorBlock, Does.Contain("PresentationVisibilitySourceKind.VisibilityTrackSample"));
            Assert.That(collectorBlock, Does.Contain("priority: 500"));
            Assert.That(collectorBlock, Does.Contain("isStatefulTrackSample: true"));
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_SelectedSourcesResolveBeforeApplier()
        {
            var source = ReadRepoFile(CoordinatorPath);

            var jumpCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectJumpDetachedVisibility",
                StringComparison.Ordinal);
            var retainedCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectRetainedDeathOrExitVisibility",
                StringComparison.Ordinal);
            var transitionCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectTransitionEntityVisibility",
                StringComparison.Ordinal);
            var spawnOnlyCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectGenericVisibilitySpawnOnly",
                StringComparison.Ordinal);
            var resolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var genericCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectPostResolveVisibilityCarriers",
                StringComparison.Ordinal);
            var trackCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectVisibilityTrackSamples",
                StringComparison.Ordinal);
            var applyIndex = source.IndexOf(
                "_entityPresentationApplier.Apply",
                StringComparison.Ordinal);

            Assert.That(jumpCollectIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(retainedCollectIndex, Is.GreaterThan(jumpCollectIndex));
            Assert.That(transitionCollectIndex, Is.GreaterThan(retainedCollectIndex));
            Assert.That(spawnOnlyCollectIndex, Is.GreaterThan(transitionCollectIndex));
            Assert.That(trackCollectIndex, Is.GreaterThan(spawnOnlyCollectIndex));
            Assert.That(resolveIndex, Is.GreaterThan(trackCollectIndex));
            Assert.That(genericCollectIndex, Is.GreaterThan(resolveIndex));
            Assert.That(applyIndex, Is.GreaterThan(trackCollectIndex));
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_UsesSingleResolveCandidatesCall()
        {
            var source = ReadRepoFile(CoordinatorPath);

            var jumpCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectJumpDetachedVisibility",
                StringComparison.Ordinal);
            var retainedCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectRetainedDeathOrExitVisibility",
                StringComparison.Ordinal);
            var transitionCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectTransitionEntityVisibility",
                StringComparison.Ordinal);
            var spawnOnlyCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectGenericVisibilitySpawnOnly",
                StringComparison.Ordinal);
            var resolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var genericCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectPostResolveVisibilityCarriers",
                StringComparison.Ordinal);
            var trackCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectVisibilityTrackSamples",
                StringComparison.Ordinal);
            var applyIndex = source.IndexOf(
                "_entityPresentationApplier.Apply",
                StringComparison.Ordinal);
            var secondResolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                resolveIndex + 1,
                StringComparison.Ordinal);

            Assert.That(jumpCollectIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(retainedCollectIndex, Is.GreaterThan(jumpCollectIndex));
            Assert.That(transitionCollectIndex, Is.GreaterThan(retainedCollectIndex));
            Assert.That(spawnOnlyCollectIndex, Is.GreaterThan(transitionCollectIndex));
            Assert.That(trackCollectIndex, Is.GreaterThan(spawnOnlyCollectIndex));
            Assert.That(resolveIndex, Is.GreaterThan(trackCollectIndex));
            Assert.That(genericCollectIndex, Is.GreaterThan(resolveIndex));
            Assert.That(applyIndex, Is.GreaterThan(genericCollectIndex));
            Assert.That(secondResolveIndex, Is.EqualTo(-1));
            Assert.That(source, Does.Contain("Post-resolve carrier/shadow collection only"));
            Assert.That(source, Does.Contain("must not feed the"));
            Assert.That(source, Does.Contain("production final visibility resolver"));
            Assert.That(source, Does.Contain("VisibilityTrackSample"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_GenericDetachRemoveRemainPostResolveCarriers()
        {
            var source = ReadRepoFile(CoordinatorPath);

            var resolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var spawnOnlyCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectGenericVisibilitySpawnOnly",
                StringComparison.Ordinal);
            var genericCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectPostResolveVisibilityCarriers",
                StringComparison.Ordinal);
            var transitionCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectTransitionEntityVisibility",
                StringComparison.Ordinal);
            var trackCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectVisibilityTrackSamples",
                StringComparison.Ordinal);
            var applyIndex = source.IndexOf(
                "_entityPresentationApplier.Apply",
                StringComparison.Ordinal);
            var secondResolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                resolveIndex + 1,
                StringComparison.Ordinal);

            Assert.That(resolveIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(transitionCollectIndex, Is.LessThan(resolveIndex));
            Assert.That(spawnOnlyCollectIndex, Is.GreaterThan(transitionCollectIndex));
            Assert.That(spawnOnlyCollectIndex, Is.LessThan(resolveIndex));
            Assert.That(trackCollectIndex, Is.GreaterThan(spawnOnlyCollectIndex));
            Assert.That(trackCollectIndex, Is.LessThan(resolveIndex));
            Assert.That(genericCollectIndex, Is.GreaterThan(resolveIndex));
            Assert.That(applyIndex, Is.GreaterThan(genericCollectIndex));
            Assert.That(secondResolveIndex, Is.EqualTo(-1));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_SpawnOnlyCollectedBeforeResolve()
        {
            var source = ReadRepoFile(CoordinatorPath);

            var spawnOnlyCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectGenericVisibilitySpawnOnly",
                StringComparison.Ordinal);
            var resolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var genericCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectPostResolveVisibilityCarriers",
                StringComparison.Ordinal);
            var trackCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectVisibilityTrackSamples",
                StringComparison.Ordinal);

            Assert.That(spawnOnlyCollectIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(resolveIndex, Is.GreaterThan(spawnOnlyCollectIndex));
            Assert.That(genericCollectIndex, Is.GreaterThan(resolveIndex));
            Assert.That(trackCollectIndex, Is.GreaterThan(spawnOnlyCollectIndex));
            Assert.That(trackCollectIndex, Is.LessThan(resolveIndex));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_UsesSingleResolveCandidatesCall()
        {
            var source = ReadRepoFile(CoordinatorPath);

            var resolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var secondResolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                resolveIndex + 1,
                StringComparison.Ordinal);

            Assert.That(resolveIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(secondResolveIndex, Is.EqualTo(-1));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_GenericDetachRemoveRemainPostResolveCarriers()
        {
            var source = ReadRepoFile(CoordinatorPath);

            var spawnOnlyCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectGenericVisibilitySpawnOnly",
                StringComparison.Ordinal);
            var resolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var genericCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectPostResolveVisibilityCarriers",
                StringComparison.Ordinal);

            Assert.That(spawnOnlyCollectIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(spawnOnlyCollectIndex, Is.LessThan(resolveIndex));
            Assert.That(genericCollectIndex, Is.GreaterThan(resolveIndex));
        }

        [Test]
        [Category("Core")]
        public void GeneralVisibilityFinalWriteLimited_DoesNotWriteGenericRemoveToFinalSet()
        {
            AssertGenericSourceRemainsAfterResolve("GenericVisibilityRemove");
        }

        [Test]
        [Category("Core")]
        public void GeneralVisibilityFinalWriteLimited_DoesNotWriteGenericDetachToFinalSet()
        {
            AssertGenericSourceRemainsAfterResolve("GenericVisibilityDetach");
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_WritesVisibilityTrackSampleToFinalSet()
        {
            var source = ReadRepoFile(CoordinatorPath);

            var resolveIndex = source.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var trackCollectIndex = source.IndexOf(
                "_visibilityCandidateCollector.CollectVisibilityTrackSamples",
                StringComparison.Ordinal);

            Assert.That(resolveIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(trackCollectIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(trackCollectIndex, Is.LessThan(resolveIndex));
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_RemovesApplierTrackSampleOverride()
        {
            var source = ReadRepoFile(ApplierPath);

            Assert.That(source, Does.Contain("_trackState.VisibilityTracks.TryGetValue"));
            Assert.That(source, Does.Not.Contain("visibilityTrack.SampleWithoutAdvance(deltaTime, isVisible)"));
            Assert.That(source, Does.Contain("visibilityTrack.AdvanceAndReportCompletion(deltaTime)"));
            Assert.That(source, Does.Contain("_trackState.CompletedVisibilityTrackIds.Add(entityId)"));
            Assert.That(source, Does.Contain("viewBinder.HideViewsExcept(_trackState.VisibleEntityIds)"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_RemainsVisibilityTrackAdvanceOwner()
        {
            var source = ReadRepoFile(ApplierPath);
            var advanceIndex = source.IndexOf(
                "visibilityTrack.AdvanceAndReportCompletion(deltaTime)",
                StringComparison.Ordinal);
            var completedIndex = source.IndexOf(
                "_trackState.CompletedVisibilityTrackIds.Add(entityId)",
                StringComparison.Ordinal);
            var cleanupIndex = source.IndexOf(
                "private void CleanupCompletedVisibilityTracks()",
                StringComparison.Ordinal);

            Assert.That(source, Does.Not.Contain("visibilityTrack.SampleWithoutAdvance(deltaTime, isVisible)"));
            Assert.That(advanceIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(completedIndex, Is.GreaterThan(advanceIndex));
            Assert.That(cleanupIndex, Is.GreaterThan(completedIndex));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateSet_DoesNotIncludeTopologyBridgeBindings()
        {
            var visibilityBlock = ExtractSourceBetween(
                ReadRepoFile(ContractsPath),
                "public enum PresentationVisibilitySourceKind",
                "public sealed class PresentationFactFrame");

            Assert.That(visibilityBlock, Does.Contain("PresentationVisibilityCandidate"));
            Assert.That(visibilityBlock, Does.Not.Contain("TopologyVisualBridgeBinding"));
            Assert.That(visibilityBlock, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateCollection_DoesNotReadTopologyBridgeBindings()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateCollector",
                "internal sealed class PresentationResolvedVisibilityResolver");

            Assert.That(collectorBlock, Does.Not.Contain("TopologyVisualBridgeBinding"));
            Assert.That(collectorBlock, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
            Assert.That(collectorBlock, Does.Not.Contain("ResolvedPresentationVisibilitySet.Topology"));
        }

        [Test]
        [Category("Core")]
        public void TransitionEntityVisibilityCandidate_DoesNotCollectTopologyBridgeBinding()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "public void CollectTransitionEntityVisibility",
                "public void CollectVisibilityTrackSamples");

            Assert.That(collectorBlock, Does.Not.Contain("TopologyVisualBridgeBinding"));
            Assert.That(collectorBlock, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
            Assert.That(collectorBlock, Does.Not.Contain("TopologyTransitionVisual"));
            Assert.That(collectorBlock, Does.Not.Contain("BridgeVisibility"));
        }

        [Test]
        [Category("Core")]
        public void PresentationVisibilityCandidateCollector_DoesNotReferenceTopologyVisualBridgeController()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var collectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateCollector",
                "internal sealed class PresentationResolvedVisibilityResolver");

            Assert.That(collectorBlock, Does.Not.Contain("TopologyVisualBridgeBinding"));
            Assert.That(collectorBlock, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_DoesNotReadTopologyBridgeBindings()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var resolverBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateWinnerResolver",
                "internal sealed class PresentationPoseCandidateCollector");

            Assert.That(resolverBlock, Does.Not.Contain("TopologyVisualBridgeBinding"));
            Assert.That(resolverBlock, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
            Assert.That(resolverBlock, Does.Not.Contain("ResolvedPresentationVisibilitySet.Topology"));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_DoesNotOwnCleanupLifecycle()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var resolverBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationVisibilityCandidateWinnerResolver",
                "internal sealed class PresentationPoseCandidateCollector");

            Assert.That(resolverBlock, Does.Not.Contain("CompletedVisibilityTrackIds"));
            Assert.That(resolverBlock, Does.Not.Contain("CleanupCompletedVisibilityTracks"));
            Assert.That(resolverBlock, Does.Not.Contain("RetainedLocalTargetPoses.Remove"));
            Assert.That(resolverBlock, Does.Not.Contain("TickVisibilityChange"));
            Assert.That(resolverBlock, Does.Not.Contain("EntityExitSignals"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotOwnCandidatePriorityResolution()
        {
            var source = ReadRepoFile(ApplierPath);

            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateWinnerResolver"));
            Assert.That(source, Does.Not.Contain("ResolveCandidateWinner"));
            Assert.That(source, Does.Not.Contain("ResolveSourceRank"));
            Assert.That(source, Does.Not.Contain("GenericVisibilitySpawn"));
            Assert.That(source, Does.Not.Contain("GenericVisibilityDetach"));
            Assert.That(source, Does.Not.Contain("GenericVisibilityRemove"));
            Assert.That(source, Does.Not.Contain("VisibilityTrackSample"));
            Assert.That(source, Does.Not.Contain("RetainedDeathOrExit"));
            Assert.That(source, Does.Not.Contain("TerminalDeathOrExitSuppression"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotConsumeRetainedDeathExitCandidates()
        {
            var source = ReadRepoFile(ApplierPath);

            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateSet"));
            Assert.That(source, Does.Not.Contain("RetainedDeathOrExit"));
            Assert.That(source, Does.Not.Contain("TerminalDeathOrExitSuppression"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotConsumeTransitionEntityVisibilityCandidates()
        {
            var source = ReadRepoFile(ApplierPath);

            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateSet"));
            Assert.That(source, Does.Not.Contain("TransitionEntityVisibility"));
            Assert.That(source, Does.Contain("TransitionVisibilityStates"));
        }

        [Test]
        [Category("Core")]
        public void GameplayExitPresentationController_DoesNotCreateVisibilityCandidates()
        {
            var source = ReadRepoFile(ExitPresentationControllerPath);

            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateSet"));
            Assert.That(source, Does.Not.Contain("RetainedDeathOrExit"));
            Assert.That(source, Does.Not.Contain("TerminalDeathOrExitSuppression"));
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitVisibilityCandidate_DoesNotBecomeBasePoseCandidate()
        {
            var trackStateSource = ReadRepoFile(TrackStatePath);
            var poseCollectorBlock = ExtractSourceBetween(
                trackStateSource,
                "internal sealed class PresentationPoseCandidateCollector",
                "internal sealed class PresentationBasePoseFrameResolver");

            Assert.That(poseCollectorBlock, Does.Not.Contain("PresentationVisibilitySourceKind"));
            Assert.That(poseCollectorBlock, Does.Not.Contain("RetainedDeathOrExit"));
            Assert.That(poseCollectorBlock, Does.Not.Contain("TerminalDeathOrExitSuppression"));
        }

        [Test]
        [Category("Core")]
        public void TopologyBridge_DoesNotConsumeRetainedDeathExitVisibilityCandidates()
        {
            var source = ReadRepoFile(TopologyBridgeVisibilityControllerPath);

            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateSet"));
            Assert.That(source, Does.Not.Contain("RetainedDeathOrExit"));
            Assert.That(source, Does.Not.Contain("TerminalDeathOrExitSuppression"));
        }

        [Test]
        [Category("Core")]
        public void TopologyVisualBridgeVisibilityController_DoesNotConsumePresentationVisibilityCandidateSet()
        {
            var source = ReadRepoFile(TopologyBridgeVisibilityControllerPath);

            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateSet"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilitySourceKind"));
            Assert.That(source, Does.Not.Contain("TransitionEntityVisibility"));
        }

        [Test]
        [Category("Core")]
        public void TickResultBuilder_DoesNotCreatePresentationVisibilityCandidates()
        {
            var source = ReadRepoFile(TickResultBuilderPath);

            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidate"));
            Assert.That(source, Does.Not.Contain("PresentationVisibilityCandidateSet"));
            Assert.That(source, Does.Not.Contain("GenericVisibilitySpawn"));
            Assert.That(source, Does.Not.Contain("GenericVisibilityDetach"));
            Assert.That(source, Does.Not.Contain("GenericVisibilityRemove"));
            Assert.That(source, Does.Not.Contain("RetainedDeathOrExit"));
            Assert.That(source, Does.Not.Contain("TerminalDeathOrExitSuppression"));
            Assert.That(source, Does.Not.Contain("TransitionEntityVisibility"));
        }

        [Test]
        [Category("Core")]
        public void RemoveConsumerContractGuard_RemoveCarrierRemainsConsumerEventAndNotFinalVisibilitySource()
        {
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var spawnOnlyCollectIndex = coordinatorSource.IndexOf(
                "_visibilityCandidateCollector.CollectGenericVisibilitySpawnOnly",
                StringComparison.Ordinal);
            var trackSampleCollectIndex = coordinatorSource.IndexOf(
                "_visibilityCandidateCollector.CollectVisibilityTrackSamples",
                StringComparison.Ordinal);
            var resolveIndex = coordinatorSource.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var genericCollectIndex = coordinatorSource.IndexOf(
                "_visibilityCandidateCollector.CollectPostResolveVisibilityCarriers",
                StringComparison.Ordinal);

            Assert.That(spawnOnlyCollectIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(trackSampleCollectIndex, Is.GreaterThan(spawnOnlyCollectIndex));
            Assert.That(trackSampleCollectIndex, Is.LessThan(resolveIndex));
            Assert.That(genericCollectIndex, Is.GreaterThan(resolveIndex));
            Assert.That(CountOccurrences(coordinatorSource, "_resolvedVisibilityResolver.ResolveCandidates"), Is.EqualTo(1));

            var trackStateSource = ReadRepoFile(TrackStatePath);
            var spawnOnlyBlock = ExtractSourceBetween(
                trackStateSource,
                "public void CollectGenericVisibilitySpawnOnly",
                "public void CollectRetainedDeathOrExitVisibility");
            var genericCollectorBlock = ExtractSourceBetween(
                trackStateSource,
                "public void CollectPostResolveVisibilityCarriers",
                "public void CollectGenericVisibilitySpawnOnly");
            Assert.That(spawnOnlyBlock, Does.Not.Contain("PresentationVisibilitySourceKind.GenericVisibilityRemove"));
            Assert.That(spawnOnlyBlock, Does.Not.Contain("PresentationVisibilitySourceKind.GenericVisibilityDetach"));
            Assert.That(spawnOnlyBlock, Does.Contain("TickVisibilityChangeKind.Remove"));
            Assert.That(genericCollectorBlock, Does.Contain("TryCreateGenericVisibilityCandidate"));

            var playerMapperSource = ReadRepoFile(PlayerViewPresentationMapperPath);
            var mapperRemovalBlock = ExtractSourceBetween(
                playerMapperSource,
                "private void CollectRemovalSignals(TickPresentationData presentationData)",
                "private static bool HasPlayerDriver");
            Assert.That(mapperRemovalBlock, Does.Contain("presentationData.VisibilityChanges"));
            Assert.That(mapperRemovalBlock, Does.Contain("TickVisibilityChangeKind.Remove"));
            Assert.That(mapperRemovalBlock, Does.Contain("_removedEntityIds.Add(entityId)"));
            Assert.That(mapperRemovalBlock, Does.Not.Contain("ResolvedPresentationVisibilitySet"));

            var playerAudioSource = ReadRepoFile(PlayerLocomotionAudioPresentationControllerPath);
            var playerAudioTerminalBlock = ExtractSourceBetween(
                playerAudioSource,
                "var visibilityChanges = presentationData.VisibilityChanges;",
                "var finalEntities = result.FinalEntities;");
            Assert.That(playerAudioTerminalBlock, Does.Contain("TickVisibilityChangeKind.Remove"));
            Assert.That(playerAudioTerminalBlock, Does.Contain("AddTerminalEntityThisTick(change.EntityId)"));
            Assert.That(playerAudioTerminalBlock, Does.Not.Contain("ResolvedPresentationVisibilitySet"));

            var vfxPlanningSource = ReadRepoFile(GameplayVfxPlanningPath);
            Assert.That(vfxPlanningSource, Does.Contain("presentationData.VisibilityChanges"));
            Assert.That(vfxPlanningSource, Does.Contain("TickVisibilityChangeKind.Spawn"));

            var vfxFollowerSource = ReadRepoFile(EnemyMotionAttachedVfxFollowerPlannerPath);
            var vfxFollowerRemoveBlock = ExtractSourceBetween(
                vfxFollowerSource,
                "private void CollectRemovedEntityIds(TickPresentationData presentationData)",
                "private void AddDesiredFollower");
            Assert.That(vfxFollowerRemoveBlock, Does.Contain("presentationData.VisibilityChanges"));
            Assert.That(vfxFollowerRemoveBlock, Does.Contain("TickVisibilityChangeKind.Remove"));
            Assert.That(vfxFollowerRemoveBlock, Does.Contain("removedEntityIds.Add(change.EntityId)"));
            Assert.That(vfxFollowerRemoveBlock, Does.Not.Contain("ResolvedPresentationVisibilitySet"));

            var animationSyncSource = ReadRepoFile(GameplayAnimationSyncCoordinatorPath);
            var animationRespawnBlock = ExtractSourceBetween(
                animationSyncSource,
                "private void ReleasePlayerDeathOverridesForRespawnSpawns",
                "private readonly struct EnemyUtilityAnimationPlaybackTrack");
            Assert.That(animationRespawnBlock, Does.Contain("presentationData.VisibilityChanges"));
            Assert.That(animationRespawnBlock, Does.Contain("TickVisibilityChangeKind.Spawn"));
            Assert.That(animationRespawnBlock, Does.Contain("ResetDeathPresentationForRespawn"));
            Assert.That(animationSyncSource, Does.Not.Contain("ResolvedPresentationVisibilitySet"));

            var applierSource = ReadRepoFile(ApplierPath);
            Assert.That(applierSource, Does.Contain("visibilityTrack.AdvanceAndReportCompletion(deltaTime)"));
            Assert.That(applierSource, Does.Contain("private void CleanupCompletedVisibilityTracks()"));
            Assert.That(applierSource, Does.Contain("ClearEntityPresentationMetadataIfFullyHidden(entityId)"));
            Assert.That(applierSource, Does.Contain("viewBinder.HideViewsExcept(_trackState.VisibleEntityIds)"));
            Assert.That(applierSource, Does.Not.Contain("PresentationVisibilityCandidateSet"));
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
            Assert.That(uiApplicationSource, Does.Not.Contain("TopologyProductionTelemetrySnapshot"));
            Assert.That(uiApplicationSource, Does.Not.Contain("TopologyPresentationExecutionMode"));
            Assert.That(uiApplicationSource, Does.Not.Contain("GameplayMotionExecutorDiagnostics"));
            Assert.That(uiApplicationSource, Does.Not.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(uiApplicationSource, Does.Not.Contain("GameplayAnimationExecutorDiagnostics"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PlayerActionAnimationProductionTelemetrySnapshot"));
            Assert.That(uiApplicationSource, Does.Not.Contain("PlayerActionAnimationExecutionMode"));
            Assert.That(uiApplicationSource, Does.Not.Contain("EnemyPresentationExecutionMode"));
            Assert.That(uiApplicationSource, Does.Not.Contain("EnemyPresentationProductionTelemetrySnapshot"));
            Assert.That(uiApplicationSource, Does.Not.Contain("ActionAudioProductionTelemetrySnapshot"));
            Assert.That(uiApplicationSource, Does.Not.Contain("EnemyAudioProductionTelemetrySnapshot"));
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
        public void TopologyLaneExtraction_KeepsInputLockAuthorityOnController()
        {
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var topologyLaneSource = ReadRepoFile(TopologyLaneRuntimePath);
            var inputHostSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs");

            Assert.That(coordinatorSource, Does.Contain("GameplayTopologyTransitionController _topologyTransitionController"));
            Assert.That(coordinatorSource, Does.Contain("TopologyPresentationLaneRuntime _topologyLane"));
            Assert.That(coordinatorSource, Does.Contain("_topologyLane.Present(result)"));
            Assert.That(coordinatorSource, Does.Contain("_topologyLane.ObserveControllerActivity"));
            Assert.That(coordinatorSource, Does.Not.Contain("_topologyTransitionController.RefreshTopologyTrack"));
            Assert.That(coordinatorSource, Does.Not.Contain("_topologyTransitionController.RefreshBoardSurfaceTransition"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutionMode.LegacyCoordinator"));
            Assert.That(topologyLaneSource, Does.Contain("TopologyPresentationExecutionDefaults.ProductionDefault"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutionMode.ExecutorBridge"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayPresentationExecutionRouter.UseTopologyExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("UseTopologyExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("ExecuteLegacyTopologyPath"));
            Assert.That(coordinatorSource, Does.Not.Contain("ExecuteExecutorBridgeTopologyPath"));
            Assert.That(coordinatorSource, Does.Not.Contain("_topologyExecutionGuard"));
            Assert.That(coordinatorSource, Does.Not.Contain("_topologyExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Contain("public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;"));
            Assert.That(coordinatorSource, Does.Contain("_topologyTransitionController.HasActiveBoardRotationTween"));
            Assert.That(coordinatorSource, Does.Not.Contain("TopologyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("ITopologyTransitionPlaybackPort"));
            Assert.That(topologyLaneSource, Does.Contain("TopologyPresentationExecutionGuard"));
            Assert.That(topologyLaneSource, Does.Contain("TopologyExecutionPipelineFactory"));
            Assert.That(topologyLaneSource, Does.Contain("ITopologyLegacyTransitionPort"));
            Assert.That(topologyLaneSource, Does.Contain("ITopologyTransitionCleanupPort"));
            Assert.That(topologyLaneSource, Does.Not.Contain("WorldState"));
            Assert.That(topologyLaneSource, Does.Not.Contain("TickPipeline"));
            Assert.That(topologyLaneSource, Does.Not.Contain("GameplayBoardRoot"));
            Assert.That(topologyLaneSource, Does.Not.Contain("GameplayCameraRig"));
            Assert.That(topologyLaneSource, Does.Not.Contain("GameplayBoardSurfaceRenderer"));
            Assert.That(topologyLaneSource, Does.Not.Contain("TopologyVisualBridgeVisibilityController"));
            Assert.That(topologyLaneSource, Does.Not.Contain("TopologyTransitionPostFxController"));
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
            var topologyLaneSource = ReadRepoFile(TopologyLaneRuntimePath);
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var compositionFactorySource = ReadRepoFile(CompositionFactoryPath);

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayTopologyTransitionController"));
            Assert.That(runtimeSource, Does.Not.Contain("GameplayTopologyTransitionController"));
            Assert.That(runtimeSource, Does.Not.Contain("TopologyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("TopologyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("ITopologyTransitionPlaybackPort"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayHostPresentationPipelineFactory"));
            Assert.That(compositionFactorySource, Does.Contain("GameplayHostPresentationPipelineFactory"));
            Assert.That(coordinatorSource, Does.Contain("TopologyPresentationLaneRuntime"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationLaneRuntime"));
            Assert.That(hostRuntimeSource, Does.Contain("ITopologyTransitionPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutionMode"));
            Assert.That(hostRuntimeSource, Does.Contain("TopologyProductionTelemetrySnapshot"));
            Assert.That(topologyExecutorSource, Does.Contain("GameplayTopologyTransitionPlaybackPort"));
            Assert.That(topologyExecutorSource, Does.Contain("GameplayTopologyLegacyTransitionPort"));
            Assert.That(topologyExecutorSource, Does.Contain("GameplayTopologyTransitionCleanupPort"));
            Assert.That(topologyLaneSource, Does.Contain("Present(TickResult result)"));
            Assert.That(topologyLaneSource, Does.Not.Contain("UpdatePresentation(float"));
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
        public void DamageDeathVfxLaneBoundary_OwnsRouteAndKeepsConcreteVfxOutOfLane()
        {
            var lanePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DamageDeathVfxLaneRuntimePath));
            var laneMetaPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DamageDeathVfxLaneRuntimeMetaPath));
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var laneSource = ReadRepoFile(DamageDeathVfxLaneRuntimePath);
            var hostFactorySource = ReadRepoFile(HostFactoryPath);
            var executorSource = ReadRepoFile(DamageDeathVfxExecutorPath);
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var vfxRuntimeSource = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs");

            Assert.That(File.Exists(lanePath), Is.True);
            Assert.That(File.Exists(laneMetaPath), Is.True);

            Assert.That(coordinatorSource, Does.Contain("DamageDeathVfxPresentationLaneRuntime _damageDeathVfxLane"));
            Assert.That(coordinatorSource, Does.Contain("_damageDeathVfxLane.Present(result)"));
            Assert.That(coordinatorSource, Does.Contain("_damageDeathVfxLane.Update(deltaTime)"));
            Assert.That(coordinatorSource, Does.Contain("_damageDeathVfxLane.ResetSession()"));
            Assert.That(coordinatorSource, Does.Contain("_damageDeathVfxLane.HardCleanup()"));
            Assert.That(coordinatorSource, Does.Contain("_damageDeathVfxLane.ConfigurePlaybackPort(playbackPort)"));
            Assert.That(coordinatorSource, Does.Contain("_damageDeathVfxLane.ExecutorDiagnostics"));
            Assert.That(coordinatorSource, Does.Contain("_damageDeathVfxLane.BlockingSnapshot"));
            Assert.That(coordinatorSource, Does.Contain("PresentExtensions(result)"));
            Assert.That(coordinatorSource, Does.Not.Contain("damageDeathVfxExtensionPolicy:"));
            Assert.That(coordinatorSource, Does.Not.Contain("damageDeathVfxExecutionMode:"));

            foreach (var forbiddenCoordinatorToken in new[]
                     {
                         "_damageDeathVfxExecutionGuard",
                         "_damageDeathVfxExecutionPipelineFactory",
                         "_damageDeathVfxExecutionPipeline",
                         "_damageDeathVfxPlaybackPort",
                         "BuildDamageDeathVfxPlaybackKeys",
                         "RefreshDamageDeathVfxExecution",
                         "ApplyDamageDeathVfxPlannerOmissionDiagnostics",
                         "GameplayPresentationExecutionRouter.UseDamageDeathVfxExecutor",
                         "new VfxCuePlanner()",
                         "DamageDeathVfxExecutionPolicy.Normalize",
                         "RecordSkippedByPolicy",
                         "DamageHitOmittedByEnemyDeathCount",
                         "RecordSameTickDamageHitOmittedByDeath",
                         "DamageDeathVfxExecutionMode.OrchestrationExecutor",
                         "DamageDeathGameplayVfxPlaybackPortAdapter",
                         "IDamageDeathGameplayVfxPlaybackRuntime",
                     })
            {
                Assert.That(coordinatorSource, Does.Not.Contain(forbiddenCoordinatorToken), forbiddenCoordinatorToken);
            }

            Assert.That(laneSource, Does.Contain("internal sealed class DamageDeathVfxPresentationLaneRuntime"));
            Assert.That(laneSource, Does.Contain("DamageDeathVfxExecutionGuard"));
            Assert.That(laneSource, Does.Contain("BuildDamageDeathVfxPlaybackKeys"));
            Assert.That(laneSource, Does.Contain("new VfxCuePlanner()"));
            Assert.That(laneSource, Does.Contain("DamageHitOmittedByEnemyDeathCount"));
            Assert.That(laneSource, Does.Not.Contain("DamageHitSuppressedByEnemyDeathCount"));
            Assert.That(laneSource, Does.Contain("RecordSameTickDamageHitOmittedByDeath"));
            Assert.That(laneSource, Does.Contain("IDamageDeathVfxPlaybackPort"));
            Assert.That(laneSource, Does.Not.Contain("DamageDeathVfxExecutionPolicy.Normalize"));
            Assert.That(laneSource, Does.Not.Contain("DamageDeathVfxExecutionOwner.LegacyExtension"));
            Assert.That(laneSource, Does.Not.Contain("RecordSkippedByPolicy"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayVfxPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("DamageDeathVfxPresentationLaneRuntime"));

            foreach (var forbiddenLaneToken in new[]
                     {
                         "UnityEngine",
                         "GameObject",
                         "Transform",
                         "ParticleSystem",
                         "FindObjectOfType",
                         "FindObjectsByType",
                         "Object.Find",
                         "GetComponent",
                         "GameplayVfxGameObjectPool",
                         "GameplayVfxPooledInstance",
                         "IGameplayTickPresentationExtension",
                         "GameplayVfxPresentationController",
                     })
            {
                Assert.That(laneSource, Does.Not.Contain(forbiddenLaneToken), forbiddenLaneToken);
            }

            foreach (var forbiddenSuppressionToken in new[]
                     {
                         "suppressVfx",
                         "suppressAllExtensions",
                         "skipAllVfx",
                         "global effect registry",
                     })
            {
                Assert.That(coordinatorSource, Does.Not.Contain(forbiddenSuppressionToken), forbiddenSuppressionToken);
                Assert.That(laneSource, Does.Not.Contain(forbiddenSuppressionToken), forbiddenSuppressionToken);
            }

            Assert.That(vfxRuntimeSource, Does.Contain("EnemyVfxCue.Damage"));
            Assert.That(vfxRuntimeSource, Does.Contain("EnemyVfxCue.Death"));
            Assert.That(vfxRuntimeSource, Does.Not.Contain("ShouldFilterDamageDeathExecutorOwnedRequests"));
            Assert.That(vfxRuntimeSource, Does.Not.Contain("LegacyDamageDeathUnrelatedCueRetainedCount"));
            Assert.That(vfxRuntimeSource, Does.Contain("IDamageDeathGameplayVfxPlaybackRuntime"));
            Assert.That(vfxRuntimeSource, Does.Contain("TryPlayDamageDeathVfx"));
            Assert.That(hostFactorySource, Does.Contain("new DamageDeathGameplayVfxPlaybackPortAdapter(damageDeathVfxRuntime)"));
            Assert.That(hostFactorySource, Does.Contain("presenter.AttachPresentationExtension(presentationExtensions[i])"));
            Assert.That(hostFactorySource, Does.Not.Contain("presenter.ConfigureDamageDeathVfxExecution("));
            Assert.That(hostFactorySource, Does.Not.Contain("AddComponent<GameplayVfxProductionRuntime>"));
            Assert.That(executorSource, Does.Not.Contain("GameObject.Find"));
            Assert.That(executorSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(executorSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(executorSource, Does.Not.Contain("Resources.Load"));
            Assert.That(executorSource, Does.Not.Contain("GameplayVfxGameObjectPool"));
            Assert.That(executorSource, Does.Not.Contain("HardCleanupAll"));
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfxPlaybackPortAdapter_DelegatesToConcreteRuntimeAndPreservesFailure()
        {
            var runtime = new RecordingDamageDeathGameplayVfxRuntime(
                GameplayVfxPlaybackResultKind.AnchorMissing);
            var adapter = new DamageDeathGameplayVfxPlaybackPortAdapter(runtime);

            var damageSucceeded = adapter.TryPlayDamageDeathVfx(
                CreateDamageDeathGameplayVfxPlaybackRequest(GameplayVfxCueId.From(EnemyVfxCue.Damage), tickIndex: 3),
                out var damageResult);
            var deathSucceeded = adapter.TryPlayDamageDeathVfx(
                CreateDamageDeathGameplayVfxPlaybackRequest(GameplayVfxCueId.From(EnemyVfxCue.Death), tickIndex: 4),
                out var deathResult);
            adapter.UpdatePresentation(0.25f);
            adapter.ResetSession();
            adapter.HardCleanup();

            Assert.That(damageSucceeded, Is.False);
            Assert.That(deathSucceeded, Is.False);
            Assert.That(damageResult.Kind, Is.EqualTo(GameplayVfxPlaybackResultKind.AnchorMissing));
            Assert.That(deathResult.Kind, Is.EqualTo(GameplayVfxPlaybackResultKind.AnchorMissing));
            Assert.That(runtime.TryPlayCallCount, Is.EqualTo(2));
            Assert.That(runtime.Requests[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Damage)));
            Assert.That(runtime.Requests[1].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Death)));
            Assert.That(runtime.Requests[0].TickIndex, Is.EqualTo(3));
            Assert.That(runtime.Requests[1].TickIndex, Is.EqualTo(4));
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
            var boxMotionLaneSource = ReadRepoFile(BoxMotionLaneRuntimePath);
            var boxMotionCleanupAdapterSource = ReadRepoFile(BoxMotionRuntimeCleanupAdapterPath);
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
            Assert.That(coordinatorSource, Does.Contain("BoxMotionPresentationLaneRuntime _boxMotionLane"));
            Assert.That(coordinatorSource, Does.Contain("_boxMotionLane.Prepare"));
            Assert.That(coordinatorSource, Does.Contain("_boxMotionLane.PresentPrepared"));
            Assert.That(coordinatorSource, Does.Contain("_boxMotionLane.Update"));
            Assert.That(coordinatorSource, Does.Contain("_boxMotionLane.ResetSession"));
            Assert.That(coordinatorSource, Does.Contain("_boxMotionLane.HardCleanup"));
            Assert.That(coordinatorSource, Does.Not.Contain("boxMotionPreparation.LegacySuppression"));
            Assert.That(coordinatorSource, Does.Not.Contain("_boxMotionExecutionGuard"));
            Assert.That(coordinatorSource, Does.Not.Contain("_boxMotionExecutionPipelineFactory"));
            Assert.That(coordinatorSource, Does.Not.Contain("_boxMotionExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("_boxMotionPlaybackPort"));
            Assert.That(coordinatorSource, Does.Not.Contain("_boxMotionTrackPlannerPlaybackPort"));
            Assert.That(coordinatorSource, Does.Not.Contain("_boxMotionUseDefaultPlaybackPort"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayPresentationExecutionRouter.UseBoxMotionExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("BoxMotionExecutionPolicy.Normalize"));
            Assert.That(coordinatorSource, Does.Not.Contain("BuildBoxMotionPlaybackKeys"));
            Assert.That(coordinatorSource, Does.Not.Contain("RecordBoxMotionLegacyOwnership"));
            Assert.That(coordinatorSource, Does.Not.Contain("RecordBoxMotionLegacySkippedByPolicy"));
            Assert.That(coordinatorSource, Does.Not.Contain("ClearBoxMotionPresentationRuntimeState"));
            Assert.That(coordinatorSource, Does.Not.Contain("suppressBoxMotionTracks"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayMotionPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayMotionPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("BoxMotionExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(boxMotionLaneSource, Does.Contain("internal sealed class BoxMotionPresentationLaneRuntime"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("BoxMotionLegacySuppression"));
            Assert.That(boxMotionLaneSource, Does.Contain("BoxMotionExecutionGuard"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("BoxMotionExecutionPolicy.Normalize"));
            Assert.That(boxMotionLaneSource, Does.Contain("BuildBoxMotionPlaybackKeys"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("RecordSkippedByPolicy"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("BoxMotionPresentationExecutionOwner.LegacyTrackPlanner"));
            Assert.That(boxMotionLaneSource, Does.Contain("CreateBoxMotionExecutionPipeline"));
            Assert.That(boxMotionLaneSource, Does.Contain("IGameplayMotionPlaybackPort"));
            Assert.That(boxMotionLaneSource, Does.Contain("IBoxMotionRuntimeCleanupPort"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("UnityEngine.Transform"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("UnityEngine.GameObject"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("BoxFlipInteractionDriver"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("GameObject.Find"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("GetComponent"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("WorldState"));
            Assert.That(boxMotionLaneSource, Does.Not.Contain("TickPipeline"));
            Assert.That(boxMotionCleanupAdapterSource, Does.Contain("GameplayEntityPresentationApplier"));
            Assert.That(boxMotionCleanupAdapterSource, Does.Contain("ResetBoxFlipInteractionsForKnownViews"));
            Assert.That(boxMotionCleanupAdapterSource, Does.Not.Contain("GameObject.Find"));
            Assert.That(boxMotionCleanupAdapterSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(boxMotionCleanupAdapterSource, Does.Not.Contain("FindObjectsByType"));
            foreach (var forbiddenSuppression in new[]
                     {
                         "SuppressAllMotion",
                         "SuppressPlayerMotion",
                         "SuppressEnemyMotion",
                         "SuppressGenericKinematicTrack",
                         "SuppressTrackUpdate",
                         "SuppressTrackCompletion",
                         "SuppressViewBinding",
                         "SuppressEntityCleanup",
                         "SuppressTopologyMotion",
                         "SuppressVfx",
                     })
            {
                Assert.That(boxMotionLaneSource, Does.Not.Contain(forbiddenSuppression), forbiddenSuppression);
                Assert.That(boxMotionExecutorSource, Does.Not.Contain(forbiddenSuppression), forbiddenSuppression);
            }

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
        public void ArchitectureBoundary_AfterBoxMotionReadiness_RemainsSeparated()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var uiSource = ReadDirectorySource("Assets/_Features/UI");
            var vfxSource = ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime");
            var topologyControllerAndInputSource =
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTopologyTransitionController.cs") + "\n" +
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs");
            var authoritativeSource = ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Model/Runtime") + "\n" +
                                      ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Loop/Runtime") + "\n" +
                                      ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime") + "\n" +
                                      ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Entities/Runtime");

            foreach (var token in new[]
                     {
                         "GameObject",
                         "Transform",
                         "MonoBehaviour",
                         "PresentationMotionTrack",
                         "GameplayTrackPlanner",
                         "BoxFlipInteractionDriver",
                     })
            {
                Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain(token), token);
            }

            Assert.That(uiSource, Does.Not.Contain("GameplayMotionExecutorDiagnostics"));
            Assert.That(uiSource, Does.Not.Contain("BoxMotionProductionTelemetrySnapshot"));
            Assert.That(uiSource, Does.Not.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(vfxSource, Does.Not.Contain("GameplayMotionPresentationExecutor"));
            Assert.That(vfxSource, Does.Not.Contain("BoxMotionProductionTelemetrySnapshot"));
            Assert.That(vfxSource, Does.Not.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(topologyControllerAndInputSource, Does.Not.Contain("GameplayMotionPresentationExecutor"));
            Assert.That(topologyControllerAndInputSource, Does.Not.Contain("BoxMotionProductionTelemetrySnapshot"));
            Assert.That(topologyControllerAndInputSource, Does.Not.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(authoritativeSource, Does.Not.Contain("BoxMotionPresentationExecutionMode"));
            Assert.That(authoritativeSource, Does.Not.Contain("GameplayMotionPresentationExecutor"));
            Assert.That(authoritativeSource, Does.Not.Contain("BoxMotionProductionTelemetrySnapshot"));
            Assert.That(authoritativeSource, Does.Not.Contain("PlayerActionAnimationProductionTelemetrySnapshot"));
            Assert.That(authoritativeSource, Does.Not.Contain("EnemyPresentationProductionTelemetrySnapshot"));
            Assert.That(authoritativeSource, Does.Not.Contain("ActionAudioProductionTelemetrySnapshot"));
            Assert.That(authoritativeSource, Does.Not.Contain("EnemyAudioProductionTelemetrySnapshot"));
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
            var playerActionAnimationLaneSource = ReadRepoFile(PlayerActionAnimationLaneRuntimePath);
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
            Assert.That(hostRuntimeSource, Does.Not.Contain("PlayerActionAnimationExecutionDefaults.LegacyFallback"));
            Assert.That(coordinatorSource, Does.Contain("PlayerActionAnimationLaneRuntime"));
            Assert.That(coordinatorSource, Does.Contain("_playerActionAnimationLane.Prepare"));
            Assert.That(coordinatorSource, Does.Contain("_playerActionAnimationLane.PresentPrepared"));
            Assert.That(coordinatorSource, Does.Contain("_playerActionAnimationLane.Update"));
            Assert.That(coordinatorSource, Does.Contain("_playerActionAnimationLane.ResetSession"));
            Assert.That(coordinatorSource, Does.Contain("_playerActionAnimationLane.HardCleanup"));
            Assert.That(coordinatorSource, Does.Contain("SuppressPlayerActionFieldsInSharedSync"));
            Assert.That(coordinatorSource, Does.Not.Contain("_playerActionAnimationExecutionGuard"));
            Assert.That(coordinatorSource, Does.Not.Contain("_playerActionAnimationExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayPresentationExecutionRouter.UsePlayerActionAnimationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("BuildPlayerActionAnimationPlaybackKeys"));
            Assert.That(coordinatorSource, Does.Not.Contain("RecordPlayerActionAnimationLegacyOwnership"));
            Assert.That(coordinatorSource, Does.Not.Contain("RecordPlayerActionAnimationLegacySkippedByPolicy"));
            Assert.That(coordinatorSource, Does.Not.Contain("PlayerActionAnimationExecutionPolicy.Normalize"));
            Assert.That(playerActionAnimationLaneSource, Does.Contain("internal sealed class PlayerActionAnimationLaneRuntime"));
            Assert.That(playerActionAnimationLaneSource, Does.Not.Contain("MonoBehaviour"));
            Assert.That(playerActionAnimationLaneSource, Does.Contain("PlayerActionAnimationExecutionGuard"));
            Assert.That(playerActionAnimationLaneSource, Does.Contain("PlayerActionAnimationExecutionPolicy.Normalize"));
            Assert.That(playerActionAnimationLaneSource, Does.Contain("BuildPlayerActionAnimationPlaybackKeys"));
            Assert.That(playerActionAnimationLaneSource, Does.Not.Contain("PlayerActionAnimationExecutionOwner.LegacyAnimationSync"));
            Assert.That(hostRuntimeSource, Does.Contain("PlayerActionAnimationExecutionPolicy.ProductionDefault"));
            Assert.That(playerActionAnimationLaneSource, Does.Contain("CreatePlayerActionAnimationExecutionPipeline"));
            Assert.That(playerActionAnimationLaneSource, Does.Contain("IGameplayAnimationPlaybackPort"));
            Assert.That(playerActionAnimationLaneSource, Does.Not.Contain("PlayerAnimatorDriver"));
            Assert.That(playerActionAnimationLaneSource, Does.Not.Contain("AnimatorController"));
            Assert.That(playerActionAnimationLaneSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(playerActionAnimationLaneSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(playerActionAnimationLaneSource, Does.Not.Contain("new GameObject"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayAnimationPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayAnimationPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("PlayerActionAnimationExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Contain("PlayerActionAnimationExecutionMode"));
            Assert.That(animationExecutorSource, Does.Contain("GameplayAnimationSyncPlaybackPort"));
            Assert.That(animationExecutorSource, Does.Contain("GameplayAnimationSyncCoordinator animationSync"));
            Assert.That(animationExecutorSource, Does.Contain("ExecuteCueMappedToRecoveryCommand"));
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
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime"), Does.Not.Contain("PlayerActionAnimationProductionTelemetrySnapshot"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime"), Does.Not.Contain("EnemyPresentationProductionTelemetrySnapshot"));
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
            Assert.That(uiSource, Does.Not.Contain("ActionAudioProductionTelemetrySnapshot"));
            Assert.That(uiSource, Does.Not.Contain("EnemyAudioProductionTelemetrySnapshot"));
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
            var actionAudioLaneSource = ReadRepoFile(ActionAudioLaneRuntimePath);
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
            Assert.That(hostRuntimeSource, Does.Not.Contain("ActionAudioExecutionMode"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("ActionAudioExecutionPolicy"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("ActionAudioExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("ActionAudioExecutionOwner"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("LegacyActionAudioController"));
            Assert.That(coordinatorSource, Does.Contain("GameplayActionAudioLaneRuntime"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioLane.RefreshPlan"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioLane.PresentPrepared"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioLane.CompletePrepared"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioLane.Update"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioLane.AttachRuntime"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioLane.DetachRuntime"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioLane.ResetSession"));
            Assert.That(coordinatorSource, Does.Contain("_actionAudioLane.HardCleanup"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayPresentationExecutionRouter.UseActionAudioExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("BuildActionAudioPlaybackKeys"));
            Assert.That(coordinatorSource, Does.Not.Contain("TryMapActionAudioPayload"));
            Assert.That(coordinatorSource, Does.Not.Contain("_actionAudioRequestPlanner"));
            Assert.That(coordinatorSource, Does.Not.Contain("_actionAudioPresentationController"));
            Assert.That(coordinatorSource, Does.Not.Contain("_actionAudioExecutionGuard"));
            Assert.That(coordinatorSource, Does.Not.Contain("_actionAudioExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("_actionAudioPlaybackPortAdapter"));
            Assert.That(coordinatorSource, Does.Not.Contain("ActionAudioExecutionOwner.LegacyActionAudioController"));
            Assert.That(coordinatorSource, Does.Not.Contain("_actionAudioLane.PresentProduction"));
            Assert.That(coordinatorSource, Does.Not.Contain("_actionAudioLane.PlayLegacyPending"));
            Assert.That(coordinatorSource, Does.Not.Contain("RefreshActionAudioExecution"));
            Assert.That(typeof(GameplayActionAudioLaneRuntime).IsSealed, Is.True);
            Assert.That(typeof(GameplayActionAudioLaneRuntime).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
            Assert.That(actionAudioLaneSource, Does.Contain("GameplayActionAudioPresentationController"));
            Assert.That(actionAudioLaneSource, Does.Contain("GameplayActionAudioPlaybackPortAdapter"));
            Assert.That(actionAudioLaneSource, Does.Contain("GameplayHostPresentationPipelineFactory.CreateActionAudioExecutionPipeline"));
            Assert.That(actionAudioLaneSource, Does.Not.Contain("GameplayActionAudioRequestPlanner"));
            Assert.That(actionAudioLaneSource, Does.Not.Contain("BuildActionAudioPlaybackKeys"));
            Assert.That(actionAudioLaneSource, Does.Not.Contain("ActionAudioExecutionOwner"));
            Assert.That(actionAudioLaneSource, Does.Not.Contain("ActionAudioExecutionDefaults"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayActionAudioPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayActionAudioPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("ActionAudioProductionTelemetrySnapshot"));
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
        public void CoreGameplaySfxLaneRuntime_OwnsCoreSfxRoutePlanningAndPlaybackState()
        {
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var coreLaneSource = ReadRepoFile(CoreGameplaySfxLaneRuntimePath);
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var coordinatorFieldTypes = typeof(GameplayTickPresentationCoordinator)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();
            var coreGameplaySfxFieldTypes = typeof(GameplayTickPresentationCoordinator)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.Name.IndexOf("coreGameplaySfx", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(field => field.FieldType)
                .ToArray();

            Assert.That(typeof(CoreGameplaySfxLaneRuntime).IsSealed, Is.True);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(CoreGameplaySfxLaneRuntime)), Is.False);
            Assert.That(coordinatorFieldTypes, Has.Member(typeof(CoreGameplaySfxLaneRuntime)));
            Assert.That(coreGameplaySfxFieldTypes, Is.EqualTo(new[] { typeof(CoreGameplaySfxLaneRuntime) }));
            Assert.That(coordinatorFieldTypes, Has.No.Member(typeof(GameplayAudioRequestPlanner)));
            Assert.That(coordinatorFieldTypes, Has.No.Member(typeof(CoreGameplaySfxExecutionGuard)));
            Assert.That(coordinatorFieldTypes, Has.No.Member(typeof(GameplaySfxPlaybackPortAdapter)));

            Assert.That(coordinatorSource, Does.Contain("CoreGameplaySfxLaneRuntime"));
            Assert.That(coordinatorSource, Does.Contain("_coreGameplaySfxLane.PrepareCurrentRoute"));
            Assert.That(coordinatorSource, Does.Contain("_coreGameplaySfxLane.PresentPrepared"));
            Assert.That(coordinatorSource, Does.Contain("_coreGameplaySfxLane.CompletePrepared"));
            Assert.That(coordinatorSource, Does.Contain("_coreGameplaySfxLane.Update"));
            Assert.That(coordinatorSource, Does.Contain("_coreGameplaySfxLane.AttachRuntime"));
            Assert.That(coordinatorSource, Does.Contain("_coreGameplaySfxLane.DetachRuntime"));
            Assert.That(coordinatorSource, Does.Contain("_coreGameplaySfxLane.ResetSession"));
            Assert.That(coordinatorSource, Does.Contain("_coreGameplaySfxLane.HardCleanup"));

            Assert.That(coordinatorSource, Does.Not.Contain("_audioRequestPlanner"));
            Assert.That(coordinatorSource, Does.Not.Contain("_audioPresentationController"));
            Assert.That(coordinatorSource, Does.Not.Contain("_coreGameplaySfxExecutionGuard"));
            Assert.That(coordinatorSource, Does.Not.Contain("_coreGameplaySfxExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("_coreGameplaySfxPlaybackPortAdapter"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayPresentationExecutionRouter.UseCoreGameplaySfxExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("BuildCoreGameplaySfxPlaybackKeys"));
            Assert.That(coordinatorSource, Does.Not.Contain("SuppressLethalEnemyDamageRequests"));
            Assert.That(coordinatorSource, Does.Not.Contain("ConfigureEnemyDeathCueSuppression"));
            Assert.That(coordinatorSource, Does.Not.Contain("CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController"));
            Assert.That(coordinatorSource, Does.Not.Contain("CreateCoreGameplaySfxExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayAudioPresentationController"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplaySfxPlaybackPortAdapter"));
            Assert.That(coordinatorSource, Does.Not.Contain("_coreGameplaySfxLane.PresentProduction"));
            Assert.That(coordinatorSource, Does.Not.Contain("_coreGameplaySfxLane.PlayLegacyPending"));
            Assert.That(coordinatorSource, Does.Not.Contain("_coreGameplaySfxLane.UpdateProductionPipeline"));

            Assert.That(coreLaneSource, Does.Not.Contain("GameplayAudioRequestPlanner"));
            Assert.That(coreLaneSource, Does.Not.Contain("GameplayAudioPresentationController"));
            Assert.That(coreLaneSource, Does.Contain("CoreGameplaySfxExecutionGuard"));
            Assert.That(coreLaneSource, Does.Contain("CoreGameplaySfxExecutionPipelineFactory"));
            Assert.That(coreLaneSource, Does.Contain("GameplaySfxPlaybackPortAdapter"));
            Assert.That(coreLaneSource, Does.Not.Contain("BuildCoreGameplaySfxPlaybackKeys"));
            Assert.That(coreLaneSource, Does.Not.Contain("SuppressLethalEnemyDamageRequests"));
            Assert.That(coreLaneSource, Does.Contain("ConfigureEnemyDeathCueSuppression"));
            Assert.That(coreLaneSource, Does.Not.Contain("CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController"));
            Assert.That(coreLaneSource, Does.Contain("PresentPrepared"));
            Assert.That(coreLaneSource, Does.Contain("CompletePrepared"));
            Assert.That(coreLaneSource, Does.Contain("public void Update(float deltaTime)"));
            Assert.That(coreLaneSource, Does.Contain("HashSet<int> _playableEnemyDeathCueEntityIds"));
            Assert.That(coreLaneSource, Does.Contain("CopyPlayableEnemyDeathCueEntityIds"));
            Assert.That(coreLaneSource, Does.Not.Contain("AudioManager"));
            Assert.That(coreLaneSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(coreLaneSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(coreLaneSource, Does.Not.Contain("new GameObject"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioManager"));
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
            var enemyLaneSource = ReadRepoFile(EnemyPresentationLaneRuntimePath);
            var enemyDriverSource = ReadRepoFile(EnemyAnimatorDriverPath);
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
            Assert.That(hostRuntimeSource, Does.Contain("EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper"));
            Assert.That(coordinatorSource, Does.Contain("EnemyPresentationLaneRuntime _enemyPresentationLane"));
            Assert.That(coordinatorSource, Does.Contain("_enemyPresentationLane.Prepare"));
            Assert.That(coordinatorSource, Does.Contain("_enemyPresentationLane.PresentPrepared"));
            Assert.That(coordinatorSource, Does.Contain("_enemyPresentationLane.Update"));
            Assert.That(coordinatorSource, Does.Contain("_enemyPresentationLane.ResetSession"));
            Assert.That(coordinatorSource, Does.Contain("_enemyPresentationLane.HardCleanup"));
            Assert.That(coordinatorSource, Does.Contain("enemyPresentationPreparation.LegacyOneShotSuppression"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayPresentationExecutionRouter.UseEnemyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyPresentationExecutionGuard"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyPresentationExecutionPipelineFactory"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyPresentationExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyPresentationPlaybackPort"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyPresentationSyncPlaybackPort"));
            Assert.That(coordinatorSource, Does.Not.Contain("suppressLegacyEnemyPresentationAnimations"));
            Assert.That(coordinatorSource, Does.Not.Contain("BuildEnemyPresentationPlaybackKeys"));
            Assert.That(hostRuntimeSource, Does.Contain("GameplayEnemyPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayEnemyPresentationPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Contain("EnemyPresentationExecutionGuard"));
            Assert.That(hostRuntimeSource, Does.Contain("EnemyPresentationExecutionMode"));
            Assert.That(hostRuntimeSource, Does.Contain("EnemyPresentationProductionTelemetrySnapshot"));
            Assert.That(enemyLaneSource, Does.Contain("EnemyPresentationExecutionPolicy.Normalize"));
            Assert.That(enemyLaneSource, Does.Contain("EnemyPresentationExecutionGuard"));
            Assert.That(enemyLaneSource, Does.Contain("EnemyPresentationExecutionPipelineFactory"));
            Assert.That(enemyLaneSource, Does.Contain("BuildEnemyPresentationPlaybackKeys"));
            Assert.That(enemyLaneSource, Does.Contain("BuildLegacyOneShotSuppression"));
            Assert.That(enemyLaneSource, Does.Not.Contain("EnemyAnimatorDriver"));
            Assert.That(enemyLaneSource, Does.Not.Contain("Animator"));
            Assert.That(enemyLaneSource, Does.Not.Contain("GameObject"));
            Assert.That(enemyLaneSource, Does.Not.Contain("Transform"));
            Assert.That(enemyLaneSource, Does.Not.Contain("EnemyAi"));
            Assert.That(enemyLaneSource, Does.Not.Contain("WorldState"));
            Assert.That(enemyDriverSource, Does.Contain("enum EnemyPresentationLegacyOneShotSuppression"));
            Assert.That(enemyDriverSource, Does.Contain("JumpWindup"));
            Assert.That(enemyDriverSource, Does.Contain("JumpAirborneStartOrRetry"));
            Assert.That(enemyDriverSource, Does.Contain("ChargeActiveStart"));
            Assert.That(enemyDriverSource, Does.Contain("DeathTrigger"));
            Assert.That(enemyDriverSource, Does.Not.Contain("SuppressJumpPhase"));
            Assert.That(enemyDriverSource, Does.Not.Contain("SuppressAirborneState"));
            Assert.That(enemyDriverSource, Does.Not.Contain("SuppressChargePhase"));
            Assert.That(enemyDriverSource, Does.Not.Contain("SuppressChargeActiveState"));
            Assert.That(enemyDriverSource, Does.Not.Contain("SuppressDeathHold"));
            Assert.That(enemyExecutorSource, Does.Contain("GameplayEnemyPresentationSyncPlaybackPort"));
            Assert.That(enemyExecutorSource, Does.Contain("GameplayAnimationSyncCoordinator animationSync"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("new GameObject"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("AudioManager"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("Play2D"));
            Assert.That(enemyExecutorSource, Does.Not.Contain("EnemyAudioPresentationController"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime"), Does.Not.Contain("EnemyPresentationExecutionMode"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime"), Does.Not.Contain("EnemyPresentationProductionTelemetrySnapshot"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime"), Does.Not.Contain("GameplayEnemyPresentationExecutor"));
            Assert.That(ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime"), Does.Not.Contain("EnemyPresentationProductionTelemetrySnapshot"));
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
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();

            Assert.That(coordinator.IsPresentationPipelineDiagnosticsEnabled, Is.False);
            Assert.That(coordinator.PresentationPipelineNoOpSchedulerAcceptCount, Is.Zero);
            Assert.That(coordinator.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
            Assert.That(coordinator.TopologyPresentationOwnershipDiagnostics.Mode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
            Assert.That(coordinator.TopologyProductionTelemetrySnapshot.CurrentMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
            Assert.That(coordinator.TopologyProductionTelemetrySnapshot.RollbackMode, Is.EqualTo(TopologyPresentationExecutionMode.LegacyCoordinator));
            Assert.That(coordinator.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
            Assert.That(coordinator.EnemyPresentationOwnershipDiagnostics.Mode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
            Assert.That(coordinator.CoreGameplaySfxRoute, Is.EqualTo(CoreGameplaySfxRoute.CurrentExecutor));
            Assert.That(coordinator.CoreGameplaySfxOwnershipDiagnostics.LastExecutionOwner, Is.EqualTo(CoreGameplaySfxExecutionOwner.None));
            Assert.That(new GameplaySceneHostConfiguration().TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
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
            Assert.That(presenterSource, Does.Contain("HasBlockingPresentation => PresentationCoordinator.HasBlockingPresentation"));
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
        public void VfxCuePlanner_RecordsSameTickDeathDamageSuppressionDiagnostics()
        {
            var factFrame = new TickPresentationFactExtractor().Extract(
                CreateDiagnosticTickResult(
                    includeEnemyDeathExit: true,
                    enemyDamageEntityId: 20,
                    enemyDeathEntityId: 20));
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new VfxCuePlanner(),
            }).Plan(factFrame);

            Assert.That(
                cueFrame.Cues,
                Has.None.Matches<PresentationCue>(cue =>
                    cue.Key.TryGetVfxCueKey(out var key) &&
                    key == PresentationVfxCueKey.DamageHit));
            Assert.That(
                cueFrame.Cues,
                Has.Exactly(1).Matches<PresentationCue>(cue =>
                    cue.Key.TryGetVfxCueKey(out var key) &&
                    key == PresentationVfxCueKey.EnemyDeath));
            Assert.That(cueFrame.Diagnostics.SuppressedCueCount, Is.EqualTo(1));
            Assert.That(cueFrame.Diagnostics.DamageHitSuppressedByEnemyDeathCount, Is.EqualTo(1));
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
        public void VfxExecutor_DefaultRoute_RoutesDamageDeathCuesToPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(CreateVfxCueFrame(includeEnemyDeathExit: true));
            var port = new RecordingGameplayVfxPlaybackPort();
            var executor = new GameplayVfxPresentationExecutor(port);

            executor.Play(plan);

            Assert.That(port.TryPlayCallCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.ObservedCueCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.PlaybackRequestedCount, Is.EqualTo(2));
            Assert.That(executor.Diagnostics.DuplicateOmittedCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void VfxExecutor_OrchestrationMode_RoutesOneCueToPlaybackPort()
        {
            var plan = new PresentationPlaybackPlanner().Plan(CreateVfxCueFrame(includeEnemyDeathExit: false));
            var port = new RecordingGameplayVfxPlaybackPort();
            var guard = new DamageDeathVfxExecutionGuard();
            var executor = new GameplayVfxPresentationExecutor(
                port,
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
        public void VfxExecutionGuard_BlocksDuplicateExecutorAttemptForSameDamageDeathKey()
        {
            var key = new DamageDeathVfxPlaybackKey(
                7,
                PresentationSemanticSource.EnemyDamage,
                20,
                20,
                PresentationVfxCueKey.DamageHit);
            var guard = new DamageDeathVfxExecutionGuard();

            Assert.That(
                guard.TryBeginExecution(DamageDeathVfxExecutionOwner.OrchestrationExecutor, key),
                Is.True);
            Assert.That(
                guard.TryBeginExecution(DamageDeathVfxExecutionOwner.OrchestrationExecutor, key),
                Is.False);

            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
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
                new DamageDeathVfxExecutionGuard());

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
                new DamageDeathVfxExecutionGuard());

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
                new DamageDeathVfxExecutionGuard());

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
            var enemyOneShotLaneSource = ReadRepoFile(EnemyOneShotAudioLaneRuntimePath);
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

            Assert.That(hostRuntimeSource, Does.Not.Contain("EnemyAudioExecutionMode"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("EnemyAudioExecutionPolicy"));
            Assert.That(enemyOneShotLaneSource, Does.Not.Contain("EnemyAudioExecutionPolicy"));
            Assert.That(coordinatorSource, Does.Not.Contain("ConfigureEnemyAudioExecution"));
            Assert.That(coordinatorSource, Does.Contain("EnemyOneShotAudioLaneRuntime"));
            Assert.That(coordinatorSource, Does.Not.Contain("GameplayPresentationExecutionRouter.UseEnemyAudioExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("BuildEnemyAudioPlaybackKeys"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyAudioExecutionGuard"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyAudioExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyAudioRequestPlanner"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyAudioPlaybackPortAdapter"));
            Assert.That(coordinatorSource, Does.Not.Contain("RecordSkippedByPolicy(\n                        EnemyAudioExecutionOwner"));
            Assert.That(coordinatorSource, Does.Not.Contain("TryBeginExecution(\n                        EnemyAudioExecutionOwner"));
            Assert.That(coordinatorSource, Does.Not.Contain("CreateEnemyAudioExecutionPipeline"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyOneShotAudioLane.PresentProduction"));
            Assert.That(coordinatorSource, Does.Not.Contain("_enemyOneShotAudioLane.PlayLegacyPending"));
            Assert.That(coordinatorSource, Does.Not.Contain("RefreshEnemyAudioExecution"));
            Assert.That(coordinatorSource, Does.Contain("_enemyOneShotAudioLane.PresentPrepared"));
            Assert.That(coordinatorSource, Does.Contain("_enemyOneShotAudioLane.CompletePrepared"));
            Assert.That(enemyOneShotLaneSource, Does.Contain("BuildEnemyAudioPlaybackKeys"));
            Assert.That(enemyOneShotLaneSource, Does.Not.Contain("EnemyAudioExecutionGuard"));
            Assert.That(enemyOneShotLaneSource, Does.Contain("GameplayEnemyAudioPlaybackPortAdapter"));
            Assert.That(enemyOneShotLaneSource, Does.Contain("GameplayHostPresentationPipelineFactory.CreateEnemyAudioExecutionPipeline"));
            Assert.That(typeof(EnemyOneShotAudioLaneRuntime).IsSealed, Is.True);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(EnemyOneShotAudioLaneRuntime)), Is.False);
            Assert.That(hostRuntimeSource, Does.Contain("GameplayEnemyAudioPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("IGameplayEnemyAudioPlaybackPort"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("EnemyAudioExecutionGuard"));
            Assert.That(enemyAudioExecutorSource, Does.Contain("GameplayEnemyAudioPlaybackPortAdapter"));
            Assert.That(enemyAudioExecutorSource, Does.Contain("EnemyAudioPresentationController controller"));
            Assert.That(enemyAudioExecutorSource, Does.Not.Contain("AudioManager"));
            Assert.That(enemyAudioExecutorSource, Does.Not.Contain("PresentationSfxCueKey"));
            Assert.That(enemyAudioExecutorSource, Does.Not.Contain("PresentationActionAudioCueKey"));
            Assert.That(hostRuntimeSource, Does.Contain("EnemyAudioProductionTelemetrySnapshot"));
            Assert.That(uiSource, Does.Not.Contain("EnemyAudioProductionTelemetrySnapshot"));

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

        [Test]
        [Category("Core")]
        public void Boundary_AfterDamageDeathVfxTelemetry_RemainsSeparated()
        {
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var runtimeSource = ReadDirectorySource(RuntimeDirectory);
            var uiSource = ReadDirectorySource("Assets/_Features/UI");
            var topologyExecutorSource = ReadRepoFile(TopologyExecutorPath);
            var simulationSource = ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Model/Runtime") + "\n" +
                                   ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Loop/Runtime") + "\n" +
                                   ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Entities/Runtime");

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameplayVfxProductionRuntime"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("GameObject"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("Transform"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("ParticleSystem"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("Pooled"));
            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioManager"));
            Assert.That(runtimeSource, Does.Not.Contain("GameplayVfxProductionRuntime"));
            Assert.That(runtimeSource, Does.Not.Contain("AudioManager"));
            Assert.That(uiSource, Does.Not.Contain("PresentationVfxCueKey"));
            Assert.That(uiSource, Does.Not.Contain("DamageDeathVfxExecutorDiagnostics"));
            Assert.That(uiSource, Does.Not.Contain("DamageDeathVfxSemanticDiagnostics"));
            Assert.That(uiSource, Does.Not.Contain("DamageDeathVfxOmissionReason"));
            Assert.That(uiSource, Does.Not.Contain("DamageHitSuppressedByEnemyDeathCount"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("GameplayVfxPresentationExecutor executor"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("IsTopologyTransitionActive = DamageDeath"));
            Assert.That(simulationSource, Does.Not.Contain("DamageDeathVfxExecutionMode"));
            Assert.That(simulationSource, Does.Not.Contain("GameplayVfxPresentationExecutor"));
        }

        private static TickResult CreateDiagnosticTickResult(
            TickTopologyMotion? topologyMotion = null,
            bool includeTopologyMotion = true,
            bool includeEnemyDeathExit = false,
            int enemyDamageEntityId = 20,
            int enemyDeathEntityId = 30)
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
                        exitedEntityId: enemyDeathEntityId,
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
                    new TickEnemyDamagePresentationSignal(enemyDamageEntityId, tookDamageThisTick: true, damageAmount: 2),
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

        private static GameplayVfxPlaybackRequest CreateDamageDeathGameplayVfxPlaybackRequest(
            GameplayVfxCueId cueId,
            int tickIndex)
        {
            return new GameplayVfxPlaybackRequest(
                default,
                default,
                cueId,
                tickIndex,
                sequenceId: tickIndex + 10,
                presentationSeed: 100 + tickIndex,
                sourceEntityId: 40,
                default,
                default,
                default);
        }

        private sealed class RecordingDamageDeathGameplayVfxRuntime : IDamageDeathGameplayVfxPlaybackRuntime
        {
            private readonly GameplayVfxPlaybackResultKind _resultKind;
            private readonly List<GameplayVfxRequest> _requests = new();

            public RecordingDamageDeathGameplayVfxRuntime(GameplayVfxPlaybackResultKind resultKind)
            {
                _resultKind = resultKind;
            }

            public int TryPlayCallCount { get; private set; }

            public IReadOnlyList<GameplayVfxRequest> Requests => _requests;

            public bool TryPlayDamageDeathVfx(
                in GameplayVfxRequest request,
                out GameplayVfxPlaybackResult result)
            {
                TryPlayCallCount++;
                _requests.Add(request);
                result = new GameplayVfxPlaybackResult(_resultKind);
                return _resultKind == GameplayVfxPlaybackResultKind.Succeeded;
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

        private static PresentationVisibilityCandidate CreateVisibilityCandidate(
            PresentationVisibilitySourceKind sourceKind,
            int priority)
        {
            return new PresentationVisibilityCandidate(
                new PresentationEntityKey(31),
                isVisible: sourceKind == PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                new PresentationVisibilityProvenance(
                    sourceKind,
                    PresentationOwnerRole.Enemy,
                    null,
                    null,
                    lifetimeToken: 77),
                priority,
                isFallback: false,
                isStatefulTrackSample: false,
                isHighPrioritySuppressionSource: false);
        }

        private static PresentationVisibilityFallbackInputs CreateVisibilityFallbackInputs(
            bool hasPresentationPoseOverride = false,
            bool hasPlayerDeathHoldPose = false,
            bool hasCommittedLocalTargetPose = false,
            bool hasActiveLocalMotion = false,
            bool hasActiveOriginalViewMotion = false,
            bool isDeferredExitRetained = false,
            bool isContactDelayedRetained = false,
            bool isDeathPresentationPlaying = false,
            bool hasResolvedVisibility = false,
            bool isResolvedVisible = false,
            bool hasTransitionVisibility = false)
        {
            return new PresentationVisibilityFallbackInputs(
                hasPresentationPoseOverride,
                hasPlayerDeathHoldPose,
                hasCommittedLocalTargetPose,
                hasActiveLocalMotion,
                hasActiveOriginalViewMotion,
                isDeferredExitRetained,
                isContactDelayedRetained,
                isDeathPresentationPlaying,
                hasResolvedVisibility,
                isResolvedVisible,
                hasTransitionVisibility);
        }

        private static void AssertGenericSourceRemainsAfterResolve(string sourceKindName)
        {
            var coordinatorSource = ReadRepoFile(CoordinatorPath);
            var spawnOnlyCollectIndex = coordinatorSource.IndexOf(
                "_visibilityCandidateCollector.CollectGenericVisibilitySpawnOnly",
                StringComparison.Ordinal);
            var resolveIndex = coordinatorSource.IndexOf(
                "_resolvedVisibilityResolver.ResolveCandidates",
                StringComparison.Ordinal);
            var genericCollectIndex = coordinatorSource.IndexOf(
                "_visibilityCandidateCollector.CollectPostResolveVisibilityCarriers",
                StringComparison.Ordinal);

            Assert.That(spawnOnlyCollectIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(spawnOnlyCollectIndex, Is.LessThan(resolveIndex));
            Assert.That(genericCollectIndex, Is.GreaterThan(resolveIndex));

            var trackStateSource = ReadRepoFile(TrackStatePath);
            var spawnOnlyBlock = ExtractSourceBetween(
                trackStateSource,
                "public void CollectGenericVisibilitySpawnOnly",
                "public void CollectRetainedDeathOrExitVisibility");
            var genericCollectorBlock = ExtractSourceBetween(
                trackStateSource,
                "public void CollectPostResolveVisibilityCarriers",
                "public void CollectGenericVisibilitySpawnOnly");

            Assert.That(spawnOnlyBlock, Does.Not.Contain($"PresentationVisibilitySourceKind.{sourceKindName}"));
            Assert.That(genericCollectorBlock, Does.Contain("CollectPostResolveVisibilityCarriers"));
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

        private static string ExtractSourceBetween(string source, string startToken, string endToken)
        {
            var start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing start token: {startToken}");
            var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), $"Missing end token after {startToken}: {endToken}");
            return source.Substring(start, end - start);
        }

        private static string ExactSourceKindPattern(string sourceKindToken)
        {
            return $@"{System.Text.RegularExpressions.Regex.Escape(sourceKindToken)}(?![A-Za-z0-9_])";
        }

        private static string[] FindGameplayTestFilesContainingExactToken(string token)
        {
            var testRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "_Features/Gameplay/Gameplay_Tests"));
            return Directory.GetFiles(testRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => System.Text.RegularExpressions.Regex.IsMatch(
                    File.ReadAllText(path),
                    ExactSourceKindPattern(token)))
                .Select(path => Path.GetRelativePath(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                    path))
                .ToArray();
        }

        private static int CountOccurrences(string source, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
