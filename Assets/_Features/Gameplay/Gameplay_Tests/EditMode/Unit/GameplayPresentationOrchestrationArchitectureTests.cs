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
                "GameplayInputHost",
                "UITickEventRouter",
                "UIStateMapper",
                "UIPresentationSnapshot",
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
            Assert.That(coordinatorSource, Does.Contain("public bool IsTopologyTransitionActive => CurrentPresentationPhase == GameplayPresentationPhase.TopologyTransition;"));
            Assert.That(coordinatorSource, Does.Contain("_topologyTransitionController.HasActiveBoardRotationTween"));
            Assert.That(coordinatorSource, Does.Not.Contain("TopologyPresentationExecutor"));
            Assert.That(coordinatorSource, Does.Not.Contain("ITopologyTransitionPlaybackPort"));
            Assert.That(inputHostSource, Does.Contain("HasBlockingPresentation"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationPlaybackPlan"));
            Assert.That(inputHostSource, Does.Not.Contain("GameplayPresentationPipeline"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationPlaybackScheduler"));
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
            Assert.That(hostRuntimeSource, Does.Contain("TopologyPresentationExecutor"));
            Assert.That(hostRuntimeSource, Does.Contain("ITopologyTransitionPlaybackPort"));
            Assert.That(topologyExecutorSource, Does.Contain("GameplayTopologyTransitionPlaybackPort"));
            Assert.That(topologyExecutorSource, Does.Contain("GameplayTopologyTransitionController controller"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("FindObjectOfType"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("FindObjectsByType"));
            Assert.That(topologyExecutorSource, Does.Not.Contain("new GameObject"));
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

            pipeline.ResetSession();
            Assert.That(pipeline.CurrentDiagnostics.NoOpSchedulerAcceptCount, Is.Zero);
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
            var executor = new TopologyPresentationExecutor(
                port,
                TopologyPresentationExecutorMode.EnabledForTests);

            pipeline.Present(result);
            executor.Play(pipeline.LastPlaybackPlan);

            Assert.That(port.BeginOrRefreshCallCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.RouteCount, Is.EqualTo(1));
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
        }

        private static TickResult CreateDiagnosticTickResult(
            TickTopologyMotion? topologyMotion = null,
            bool includeTopologyMotion = true)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var resolvedTopologyMotion = includeTopologyMotion
                ? topologyMotion ?? new TickTopologyMotion(topology, destinationTopology, CubeRotationKind.Forward)
                : (TickTopologyMotion?)null;
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
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        exitedEntityId: 30,
                        TickEntityExitCause.BoxDestroy,
                        cell,
                        topology,
                        Direction.Right,
                        EntityType.Box),
                },
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
