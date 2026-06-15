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
        private const string CoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";

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
            Assert.That(inputHostSource, Does.Contain("HasBlockingPresentation"));
            Assert.That(inputHostSource, Does.Not.Contain("PresentationPlaybackPlan"));
            Assert.That(inputHostSource, Does.Not.Contain("GameplayPresentationPipeline"));
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
            Assert.That(pipeline.CurrentDiagnostics.PlannedCueCount, Is.Zero);
            Assert.That(pipeline.CurrentDiagnostics.PlannedTrackCount, Is.Zero);
            Assert.That(pipeline.CurrentDiagnostics.PlannedBarrierCount, Is.Zero);
            Assert.That(pipeline.CurrentDiagnostics.NoOpSchedulerAcceptCount, Is.EqualTo(1));
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

        private static TickResult CreateDiagnosticTickResult()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var presentationData = new TickPresentationData(
                new[]
                {
                    new TickEntityMotion(
                        entityId: 10,
                        TickEntityMotionKind.Move,
                        cell,
                        destinationCell),
                },
                new TickTopologyMotion(topology, destinationTopology, CubeRotationKind.Forward),
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
