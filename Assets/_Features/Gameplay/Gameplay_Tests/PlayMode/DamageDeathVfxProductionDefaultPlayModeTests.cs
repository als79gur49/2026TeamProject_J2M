using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class DamageDeathVfxProductionDefaultPlayModeTests
    {
        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_UsesOrchestrationOwner()
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var context = CreateHostContext(
                nameof(DamageDeathVfxProductionDefault_PlayMode_UsesOrchestrationOwner),
                port);
            try
            {
                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 12,
                    topology: context.Topology,
                    enemyDamageEntityId: 40));
                yield return null;

                var diagnostics = context.Host.Presenter.DamageDeathVfxExecutorDiagnostics;
                var ownership = context.Host.Presenter.DamageDeathVfxOwnershipDiagnostics;
                Assert.That(diagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(diagnostics.DamageCuePlannedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(diagnostics.DuplicateOmittedCount, Is.Zero);
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback()
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var context = CreateHostContext(
                nameof(DamageDeathVfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback),
                port);
            try
            {
                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 13,
                    topology: context.Topology,
                    enemyDamageEntityId: 40));
                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 14,
                    topology: context.Topology,
                    enemyDeathEntityId: 41,
                    enemyDeathCell: new SurfaceCell(FaceId.Floor, 1, 1),
                    presentationSeed: 9041));
                yield return null;

                var diagnostics = context.Host.Presenter.DamageDeathVfxExecutorDiagnostics;
                var ownership = context.Host.Presenter.DamageDeathVfxOwnershipDiagnostics;
                Assert.That(diagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(diagnostics.ObservedCueCount, Is.EqualTo(2));
                Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(2));
                Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(2));
                Assert.That(diagnostics.DuplicateOmittedCount, Is.Zero);
                Assert.That(ownership.DuplicateAttemptCount, Is.Zero);
                Assert.That(port.TryPlayCallCount, Is.EqualTo(2));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_ConcreteRuntimePortWiring_NoMissingPortAndNoExtensionDamageDeathDuplicate()
        {
            var context = CreateHostContext(
                nameof(DamageDeathVfxProductionDefault_PlayMode_ConcreteRuntimePortWiring_NoMissingPortAndNoExtensionDamageDeathDuplicate),
                playbackPort: null,
                attachVfxRuntime: true,
                configureManualPlaybackPort: false);
            try
            {
                Assert.That(context.VfxRuntime, Is.Not.Null);

                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 32,
                    topology: context.Topology,
                    enemyDamageEntityId: 40,
                    includeBoxDestroy: true));
                yield return null;

                var diagnostics = context.Host.Presenter.DamageDeathVfxExecutorDiagnostics;
                var ownership = context.Host.Presenter.DamageDeathVfxOwnershipDiagnostics;
                Assert.That(diagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PortMissingCount, Is.Zero);
                Assert.That(
                    diagnostics.PlaybackSucceededCount +
                    diagnostics.BindingMissingCount +
                    diagnostics.TargetMissingCount +
                    diagnostics.AnchorMissingCount,
                    Is.EqualTo(1));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(context.VfxRuntime.DamageDeathPlaybackRequestCount, Is.EqualTo(1));
                Assert.That(context.VfxRuntime.LastDamageDeathPlaybackCueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Damage)));
                Assert.That(context.VfxRuntime.LastPlannedRequestCount, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_HostDiscoveryBindsSameRuntimeAndExtensionOnce()
        {
            var presentationOrder = new List<string>();
            var hostObject = new GameObject(nameof(DamageDeathVfxProductionDefault_PlayMode_HostDiscoveryBindsSameRuntimeAndExtensionOnce));
            hostObject.SetActive(false);
            var firstExtension = hostObject.AddComponent<OrderedPresentationExtensionSpy>();
            firstExtension.Configure("first", presentationOrder);
            var runtime = hostObject.AddComponent<HostLocalDamageDeathVfxRuntimeSpy>();
            runtime.Configure("runtime", presentationOrder);
            var secondExtension = hostObject.AddComponent<OrderedPresentationExtensionSpy>();
            secondExtension.Configure("second", presentationOrder);
            var host = hostObject.AddComponent<GameplaySceneHost>();
            var topology = new CubeTopologyState(FaceId.Floor);

            try
            {
                hostObject.SetActive(true);
                host.Initialize(CreateHostConfiguration(topology));

                host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 33,
                    topology,
                    enemyDamageEntityId: 40));
                yield return null;

                Assert.That(host.Presenter.DamageDeathVfxExecutorDiagnostics.PortMissingCount, Is.Zero);
                Assert.That(host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(runtime.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(runtime.ExtensionPresentCallCount, Is.EqualTo(1));
                Assert.That(firstExtension.PresentCallCount, Is.EqualTo(1));
                Assert.That(secondExtension.PresentCallCount, Is.EqualTo(1));
                Assert.That(presentationOrder, Is.EqualTo(new[] { "first", "runtime", "second" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_MissingHostRuntimeKeepsProductionPortMissingContract()
        {
            var presentationOrder = new List<string>();
            var hostObject = new GameObject(nameof(DamageDeathVfxProductionDefault_PlayMode_MissingHostRuntimeKeepsProductionPortMissingContract));
            hostObject.SetActive(false);
            var extension = hostObject.AddComponent<OrderedPresentationExtensionSpy>();
            extension.Configure("unrelated", presentationOrder);
            var host = hostObject.AddComponent<GameplaySceneHost>();
            var topology = new CubeTopologyState(FaceId.Floor);

            try
            {
                hostObject.SetActive(true);
                host.Initialize(CreateHostConfiguration(topology));

                host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 34,
                    topology,
                    enemyDamageEntityId: 40));
                yield return null;

                Assert.That(host.Presenter.DamageDeathVfxExecutorDiagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(host.Presenter.DamageDeathVfxExecutorDiagnostics.PortMissingCount, Is.EqualTo(1));
                Assert.That(host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(host.Presenter.DamageDeathVfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(extension.PresentCallCount, Is.EqualTo(1));
                Assert.That(presentationOrder, Is.EqualTo(new[] { "unrelated" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_DuplicateHostRuntimeFailsBeforePartialAttachment()
        {
            var presentationOrder = new List<string>();
            var hostObject = new GameObject(nameof(DamageDeathVfxProductionDefault_PlayMode_DuplicateHostRuntimeFailsBeforePartialAttachment));
            hostObject.SetActive(false);
            var firstRuntime = hostObject.AddComponent<HostLocalDamageDeathVfxRuntimeSpy>();
            firstRuntime.Configure("first", presentationOrder);
            var secondRuntime = hostObject.AddComponent<HostLocalDamageDeathVfxRuntimeSpy>();
            secondRuntime.Configure("second", presentationOrder);
            var host = hostObject.AddComponent<GameplaySceneHost>();
            var topology = new CubeTopologyState(FaceId.Floor);

            try
            {
                hostObject.SetActive(true);
                var exception = Assert.Throws<InvalidOperationException>(() => host.Initialize(CreateHostConfiguration(topology)));
                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplaySceneHost requires exactly one host-local Damage/Death VFX playback runtime."));
                Assert.That(firstRuntime.ExtensionResetCallCount, Is.Zero);
                Assert.That(secondRuntime.ExtensionResetCallCount, Is.Zero);
                Assert.That(presentationOrder, Is.Empty);
                yield return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_RoutesDamageAndDeath()
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var context = CreateHostContext(
                nameof(DamageDeathVfxProductionDefault_PlayMode_RoutesDamageAndDeath),
                port);
            try
            {
                var deathCell = new SurfaceCell(FaceId.Floor, 2, 1);
                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 15,
                    topology: context.Topology,
                    enemyDamageEntityId: 40));
                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 16,
                    topology: context.Topology,
                    enemyDeathEntityId: 41,
                    enemyDeathCell: deathCell,
                    presentationSeed: 9141));
                yield return null;

                Assert.That(port.Requests, Has.Count.EqualTo(2));
                AssertDamageRequest(port.Requests[0], tickIndex: 15, entityId: 40);
                AssertDeathRequest(port.Requests[1], tickIndex: 16, entityId: 41, deathCell, presentationSeed: 9141);
                var diagnostics = context.Host.Presenter.DamageDeathVfxExecutorDiagnostics;
                Assert.That(diagnostics.DamageCuePlannedCount, Is.EqualTo(1));
                Assert.That(diagnostics.DeathCuePlannedCount, Is.EqualTo(1));
                Assert.That(diagnostics.DamagePlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(diagnostics.DeathPlaybackRequestedCount, Is.EqualTo(1));
                AssertSemanticTelemetry(
                    diagnostics,
                    PresentationVfxCueKey.DamageHit,
                    entityId: 40,
                    anchorKind: PresentationAnchorKind.EntityCenter);
                AssertSemanticTelemetry(
                    diagnostics,
                    PresentationVfxCueKey.EnemyDeath,
                    entityId: 41,
                    anchorKind: PresentationAnchorKind.SurfaceCellCenter);
                AssertNoVfxHandleStoredInOrchestrationContracts();
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_SameTickDeathSuppressesDamage()
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var context = CreateHostContext(
                nameof(DamageDeathVfxProductionDefault_PlayMode_SameTickDeathSuppressesDamage),
                port);
            try
            {
                var deathCell = new SurfaceCell(FaceId.Floor, 1, 1);
                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 17,
                    topology: context.Topology,
                    enemyDamageEntityId: 40,
                    enemyDeathEntityId: 40,
                    enemyDeathCell: deathCell,
                    presentationSeed: 9040,
                    determinismHash: "same-tick-hash"));
                yield return null;

                Assert.That(port.Requests, Has.Count.EqualTo(1));
                AssertDeathRequest(port.Requests[0], tickIndex: 17, entityId: 40, deathCell, presentationSeed: 9040);
                var diagnostics = context.Host.Presenter.DamageDeathVfxExecutorDiagnostics;
                Assert.That(diagnostics.DamageCuePlannedCount, Is.Zero);
                Assert.That(diagnostics.DamagePlaybackRequestedCount, Is.Zero);
                Assert.That(diagnostics.DeathCuePlannedCount, Is.EqualTo(1));
                Assert.That(diagnostics.DeathPlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(diagnostics.SameTickDamageHitOmittedByDeathCount, Is.EqualTo(1));
                Assert.That(diagnostics.DuplicateOmittedCount, Is.Zero);
                Assert.That(context.Host.Presenter.DamageDeathVfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfxProductionDefault_PlayMode_ExtensionPlannerDoesNotEmitDamageDeathDuplicates()
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var context = CreateHostContext(
                nameof(DamageDeathVfxProductionDefault_PlayMode_ExtensionPlannerDoesNotEmitDamageDeathDuplicates),
                port,
                attachVfxRuntime: true);
            try
            {
                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 18,
                    topology: context.Topology,
                    enemyDamageEntityId: 40,
                    enemyDeathEntityId: 41,
                    enemyDeathCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    includeBoxDestroy: true,
                    presentationSeed: 9041));
                yield return null;

                Assert.That(context.VfxRuntime, Is.Not.Null);
                Assert.That(context.VfxRuntime.DamageDeathPlaybackRequestCount, Is.Zero);
                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.DuplicateOmittedCount, Is.Zero);
                Assert.That(context.Host.Presenter.DamageDeathVfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfx_LegacyRollback_NotReachableAfterPlayModeSmoke()
        {
            Assert.That(ResolveType("Game.Feature.Gameplay.Host.DamageDeathVfxExecutionMode"), Is.Null);
            Assert.That(
                typeof(GameplayTickViewPresenter).GetMethod(
                    "ConfigureDamageDeathVfxExecution",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null);
            yield break;
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfx_PlayModeSmoke_IsNonAuthoritative()
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var context = CreateHostContext(
                nameof(DamageDeathVfx_PlayModeSmoke_IsNonAuthoritative),
                port);
            try
            {
                var finalEntities = new[]
                {
                    CreateUnit(40, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0)),
                };
                var eventLog = new[] { "BeforeDamageDeathVfx" };
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 20,
                    topology: context.Topology,
                    enemyDamageEntityId: 40,
                    finalEntities: finalEntities,
                    eventLog: eventLog,
                    determinismHash: "authoritative-hash");

                context.Host.Presenter.Present(result);
                yield return null;

                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(result.DeterminismHash, Is.EqualTo("authoritative-hash"));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.EqualTo(StageObjectiveTickResult.NoObjective));
                AssertBlockingSnapshotCleared(context.Host.Presenter.DamageDeathVfxExecutionPipelineBlockingSnapshot);
                Assert.That(context.Host.Presenter.HasBlockingPresentation, Is.False);
                Assert.That(context.Host.Presenter.IsTopologyTransitionActive, Is.False);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfx_PlayModeSmoke_KeepsCoreSfxAndAudioBoundaries()
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var context = CreateHostContext(
                nameof(DamageDeathVfx_PlayModeSmoke_KeepsCoreSfxAndAudioBoundaries),
                port);
            try
            {
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);

                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 21,
                    topology: context.Topology,
                    enemyDamageEntityId: 40));
                yield return null;

                Assert.That(context.Host.Presenter.DamageDeathVfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutorDiagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.Zero);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfx_PlayModeSmoke_ReportsMissingPortAndBindingWithoutThrowing()
        {
            var missingPortContext = CreateHostContext(
                nameof(DamageDeathVfx_PlayModeSmoke_ReportsMissingPortAndBindingWithoutThrowing) + "_MissingPort",
                playbackPort: null);
            try
            {
                missingPortContext.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 22,
                    topology: missingPortContext.Topology,
                    enemyDamageEntityId: 40,
                    determinismHash: "missing-port"));
                yield return null;

                Assert.That(missingPortContext.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PortMissingCount, Is.EqualTo(1));
                Assert.That(missingPortContext.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(missingPortContext.Host.Presenter.HasBlockingPresentation, Is.False);
            }
            finally
            {
                missingPortContext.Dispose();
            }

            var bindingPort = new RecordingDamageDeathVfxPlaybackPort(GameplayVfxPlaybackResultKind.BindingMissing);
            var bindingContext = CreateHostContext(
                nameof(DamageDeathVfx_PlayModeSmoke_ReportsMissingPortAndBindingWithoutThrowing) + "_Binding",
                bindingPort);
            try
            {
                bindingContext.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 23,
                    topology: bindingContext.Topology,
                    enemyDamageEntityId: 40,
                    determinismHash: "missing-binding"));
                yield return null;

                Assert.That(bindingContext.Host.Presenter.DamageDeathVfxExecutorDiagnostics.BindingMissingCount, Is.EqualTo(1));
                Assert.That(bindingContext.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PortMissingCount, Is.Zero);
                Assert.That(bindingPort.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(bindingContext.Host.Presenter.HasBlockingPresentation, Is.False);
            }
            finally
            {
                bindingContext.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics()
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var context = CreateHostContext(
                nameof(DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics),
                port);
            try
            {
                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 24,
                    topology: context.Topology,
                    enemyDamageEntityId: 40));
                yield return null;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));

                context.Host.Presenter.PresentInitial(Array.Empty<EntityState>(), context.Topology);
                Assert.That(port.ResetSessionCallCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(context.Host.Presenter.DamageDeathVfxOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(context.Host.Presenter.DamageDeathVfxExecutorDiagnostics.SemanticDiagnostics, Is.Empty);

                context.Host.Presenter.Present(CreateDamageDeathVfxResult(
                    tickIndex: 25,
                    topology: context.Topology,
                    enemyDamageEntityId: 40));
                yield return null;
            }
            finally
            {
                context.Dispose();
            }

            Assert.That(port.HardCleanupCallCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(port.TryPlayCallCount, Is.Zero);
        }

        private static HostVfxSmokeContext CreateHostContext(
            string rootName,
            RecordingDamageDeathVfxPlaybackPort playbackPort,
            bool attachVfxRuntime = false,
            bool configureManualPlaybackPort = true)
        {
            var hostObject = new GameObject(rootName);
            hostObject.SetActive(false);
            var runtime = attachVfxRuntime
                ? hostObject.AddComponent<GameplayVfxProductionRuntime>()
                : null;
            var host = hostObject.AddComponent<GameplaySceneHost>();
            var topology = new CubeTopologyState(FaceId.Floor);

            hostObject.SetActive(true);
            host.Initialize(CreateHostConfiguration(topology));
            if (configureManualPlaybackPort)
            {
                host.Presenter.ConfigureDamageDeathVfxPlaybackPort(playbackPort);
            }

            return new HostVfxSmokeContext(hostObject, host, runtime, topology);
        }

        private static GameplaySceneHostConfiguration CreateHostConfiguration(CubeTopologyState topology)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                InitialEntities = Array.Empty<EntityState>(),
                InitialTopology = topology,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
            };
        }

        private static TickResult CreateDamageDeathVfxResult(
            int tickIndex,
            CubeTopologyState topology,
            int enemyDamageEntityId = 0,
            int enemyDeathEntityId = 0,
            SurfaceCell enemyDeathCell = default,
            bool includeBoxDestroy = false,
            int presentationSeed = 0,
            IReadOnlyList<EntityState> finalEntities = null,
            IEnumerable<string> eventLog = null,
            string determinismHash = "")
        {
            var enemyDamageSignals = enemyDamageEntityId > 0
                ? new[]
                {
                    new TickEnemyDamagePresentationSignal(
                        enemyDamageEntityId,
                        tookDamageThisTick: true,
                        damageAmount: 2),
                }
                : Array.Empty<TickEnemyDamagePresentationSignal>();
            var exits = new List<TickEntityExitPresentationSignal>();
            if (enemyDeathEntityId > 0)
            {
                exits.Add(new TickEntityExitPresentationSignal(
                    enemyDeathEntityId,
                    TickEntityExitCause.Killed,
                    enemyDeathCell,
                    topology,
                    Direction.Right,
                    EntityType.Unit,
                    sourceActorEntityId: 10,
                    presentationSeed: presentationSeed));
            }

            if (includeBoxDestroy)
            {
                exits.Add(new TickEntityExitPresentationSignal(
                    80,
                    TickEntityExitCause.BoxDestroy,
                    new SurfaceCell(FaceId.Floor, 2, 2),
                    topology,
                    Direction.Up,
                    EntityType.Box,
                    sourceActorEntityId: 10,
                    presentationSeed: 9080));
            }

            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                enemyDamageSignals,
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                exits);

            return CreateTickResult(
                tickIndex,
                finalEntities ?? Array.Empty<EntityState>(),
                topology,
                presentationData,
                eventLog,
                determinismHash);
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            IReadOnlyList<EntityState> finalEntities,
            CubeTopologyState topology,
            TickPresentationData presentationData,
            IEnumerable<string> eventLog = null,
            string determinismHash = "")
        {
            var result = new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetSerializedField(typeof(TickResult), result, "<PresentationData>k__BackingField", presentationData);
            SetSerializedField(typeof(TickResult), result, "<FinalTopology>k__BackingField", topology);
            SetSerializedField(typeof(TickResult), result, "<DeterminismHash>k__BackingField", determinismHash);
            SetSerializedField(typeof(TickResult), result, "<Trace>k__BackingField", TickTrace.Empty);
            SetSerializedField(typeof(TickResult), result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetSerializedField(
                typeof(TickResult),
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities)));
            SetSerializedField(
                typeof(TickResult),
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string>(eventLog ?? Array.Empty<string>())));
            return result;
        }

        private static EntityState CreateUnit(int entityId, UnitRole role, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = role == UnitRole.Player ? 1 : 2,
                type = EntityType.Unit,
                unitRole = role,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static void AssertDamageRequest(
            in GameplayVfxPlaybackRequest request,
            int tickIndex,
            int entityId)
        {
            Assert.That(request.CueKey, Is.EqualTo(PresentationVfxCueKey.DamageHit));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Damage)));
            Assert.That(request.TickIndex, Is.EqualTo(tickIndex));
            Assert.That(request.SequenceId, Is.EqualTo(entityId));
            Assert.That(request.PresentationSeed, Is.EqualTo(entityId));
            Assert.That(request.SourceEntityId, Is.EqualTo(entityId));
            Assert.That(request.Target.EntityId, Is.EqualTo(entityId));
            Assert.That(request.PresentationAnchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityCenter));
            Assert.That(request.VfxAnchor.Kind, Is.EqualTo(VfxAnchorKind.Entity));
            Assert.That(request.VfxAnchor.EntityId, Is.EqualTo(entityId));
            Assert.That(request.VfxAnchor.Slot, Is.EqualTo(VfxAnchorSlot.EntityCenter));
        }

        private static void AssertDeathRequest(
            in GameplayVfxPlaybackRequest request,
            int tickIndex,
            int entityId,
            SurfaceCell deathCell,
            int presentationSeed)
        {
            Assert.That(request.CueKey, Is.EqualTo(PresentationVfxCueKey.EnemyDeath));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Death)));
            Assert.That(request.TickIndex, Is.EqualTo(tickIndex));
            Assert.That(request.SequenceId, Is.EqualTo(entityId));
            Assert.That(request.PresentationSeed, Is.EqualTo(presentationSeed));
            Assert.That(request.SourceEntityId, Is.EqualTo(entityId));
            Assert.That(request.Target.EntityId, Is.EqualTo(entityId));
            Assert.That(request.PresentationAnchor.Kind, Is.EqualTo(PresentationAnchorKind.SurfaceCellCenter));
            Assert.That(request.PresentationAnchor.Target.Cell, Is.EqualTo(deathCell));
            Assert.That(request.VfxAnchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.VfxAnchor.Cell, Is.EqualTo(deathCell));
            Assert.That(request.VfxAnchor.Topology, Is.EqualTo(new CubeTopologyState(deathCell.face)));
            Assert.That(request.VfxAnchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
        }

        private static void AssertSemanticTelemetry(
            DamageDeathVfxExecutorDiagnostics diagnostics,
            PresentationVfxCueKey cueKey,
            int entityId,
            PresentationAnchorKind anchorKind)
        {
            var semantic = diagnostics.SemanticDiagnostics.Single(candidate => candidate.CueKey == cueKey);
            Assert.That(semantic.PlannedCount, Is.EqualTo(1));
            Assert.That(semantic.RequestedCount, Is.EqualTo(1));
            Assert.That(semantic.SucceededCount, Is.EqualTo(1));
            Assert.That(semantic.DuplicateOmittedCount, Is.Zero);
            Assert.That(semantic.LastDedupeKey, Is.GreaterThan(0));
            Assert.That(semantic.LastTargetEntityId, Is.EqualTo(entityId));
            Assert.That(semantic.LastAnchorKind, Is.EqualTo(anchorKind));
        }

        private static void AssertBlockingSnapshotCleared(PresentationBlockingSnapshot snapshot)
        {
            Assert.That(snapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(snapshot.HasActiveBlockingPresentation, Is.False);
        }

        private static void AssertNoVfxHandleStoredInOrchestrationContracts()
        {
            var handleType = ResolveType("Game.Feature.Gameplay.Vfx.IVfxPlaybackHandle");
            Assert.That(handleType, Is.Not.Null, "The VFX playback handle contract should be available through the loaded VFX runtime assembly.");
            Assert.That(ContainsFieldAssignableTo(typeof(PresentationCue), handleType), Is.False);
            Assert.That(ContainsFieldAssignableTo(typeof(PresentationPlaybackPlan), handleType), Is.False);
            Assert.That(ContainsFieldAssignableTo(typeof(GameplayVfxPlaybackRequest), handleType), Is.False);
            Assert.That(ContainsFieldAssignableTo(typeof(DamageDeathVfxExecutorDiagnostics), handleType), Is.False);
        }

        private static Type ResolveType(string fullName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, throwOnError: false))
                .FirstOrDefault(type => type != null);
        }

        private static bool ContainsFieldAssignableTo(Type inspectedType, Type forbiddenType)
        {
            return inspectedType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => forbiddenType.IsAssignableFrom(field.FieldType));
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private sealed class HostVfxSmokeContext : IDisposable
        {
            private readonly GameObject _rootObject;

            public HostVfxSmokeContext(
                GameObject rootObject,
                GameplaySceneHost host,
                GameplayVfxProductionRuntime vfxRuntime,
                CubeTopologyState topology)
            {
                _rootObject = rootObject;
                Host = host;
                VfxRuntime = vfxRuntime;
                Topology = topology;
            }

            public GameplaySceneHost Host { get; }

            public GameplayVfxProductionRuntime VfxRuntime { get; }

            public CubeTopologyState Topology { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class RecordingDamageDeathVfxPlaybackPort : IDamageDeathVfxPlaybackPort
        {
            private readonly GameplayVfxPlaybackResultKind _resultKind;
            private readonly List<GameplayVfxPlaybackRequest> _requests = new();

            public RecordingDamageDeathVfxPlaybackPort(
                GameplayVfxPlaybackResultKind resultKind = GameplayVfxPlaybackResultKind.Succeeded)
            {
                _resultKind = resultKind;
            }

            public int TryPlayCallCount { get; private set; }

            public int UpdatePresentationCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public IReadOnlyList<GameplayVfxPlaybackRequest> Requests => _requests;

            public bool TryPlayDamageDeathVfx(
                in GameplayVfxPlaybackRequest request,
                out GameplayVfxPlaybackResult result)
            {
                TryPlayCallCount++;
                _requests.Add(request);
                result = new GameplayVfxPlaybackResult(_resultKind);
                return _resultKind == GameplayVfxPlaybackResultKind.Succeeded;
            }

            public void UpdatePresentation(float deltaTime)
            {
                UpdatePresentationCallCount++;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                _requests.Clear();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                _requests.Clear();
            }
        }

        private sealed class OrderedPresentationExtensionSpy : MonoBehaviour, IGameplayTickPresentationExtension
        {
            private List<string> _presentationOrder;
            private string _name;

            public int PresentCallCount { get; private set; }

            public void Configure(string name, List<string> presentationOrder)
            {
                _name = name;
                _presentationOrder = presentationOrder;
            }

            public void ResetSession()
            {
            }

            public void Present(in GameplayTickPresentationExtensionContext context)
            {
                PresentCallCount++;
                _presentationOrder.Add(_name);
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void HardCleanup()
            {
            }
        }

        private sealed class HostLocalDamageDeathVfxRuntimeSpy : MonoBehaviour, IGameplayTickPresentationExtension, IDamageDeathGameplayVfxPlaybackRuntime
        {
            private List<string> _presentationOrder;
            private string _name;

            public int TryPlayCallCount { get; private set; }

            public int ExtensionPresentCallCount { get; private set; }

            public int ExtensionResetCallCount { get; private set; }

            public void Configure(string name, List<string> presentationOrder)
            {
                _name = name;
                _presentationOrder = presentationOrder;
            }

            public bool TryPlayDamageDeathVfx(
                in GameplayVfxRequest request,
                out GameplayVfxPlaybackResult result)
            {
                TryPlayCallCount++;
                result = new GameplayVfxPlaybackResult(GameplayVfxPlaybackResultKind.Succeeded);
                return true;
            }

            public void ResetSession()
            {
                ExtensionResetCallCount++;
            }

            public void Present(in GameplayTickPresentationExtensionContext context)
            {
                ExtensionPresentCallCount++;
                _presentationOrder.Add(_name);
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void HardCleanup()
            {
            }
        }
    }
}
