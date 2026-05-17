using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxForwardCellProjectileTests
    {
        private static readonly SurfaceCell SourceCell = new(FaceId.Floor, 0, 0);
        private static readonly SurfaceCell TargetCell = new(FaceId.Floor, 1, 0);

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_WindupSignalCreatesMarkerRequest()
        {
            var plan = PlanProjectile(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() }));
            var request = plan.Requests.Single();

            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellDangerMarker)));
            Assert.That(request.IsPersistent, Is.True);
            Assert.That(request.SequenceId, Is.EqualTo(4000001));
            Assert.That(request.SourceEntityId, Is.EqualTo(40));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Cell, Is.EqualTo(TargetCell));
        }

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_ReleaseSignalCreatesFlightRequestOnly()
        {
            var plan = PlanProjectile(CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() }));

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            Assert.That(plan.Requests[0].CueId, Is.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileFlight)));
            Assert.That(plan.Requests[0].CueId, Is.Not.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact)));
        }

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_ImpactSignalCreatesImpactVfx()
        {
            var data = CreatePresentationData(forwardCellImpactSignals: new[] { CreateImpactSignal(hit: false) });
            var plan = PlanProjectile(data);
            var playerBuilder = new GameplayVfxRequestPlanBuilder();

            new PlayerVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(12, data, new CubeTopologyState(FaceId.Floor)),
                playerBuilder);

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            Assert.That(plan.Requests[0].CueId, Is.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact)));
            Assert.That(playerBuilder.Build().Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_ImpactHitSeparatesImpactAndDamageFeedback()
        {
            var data = CreatePresentationData(
                playerDamageSignals: new[] { new TickPlayerDamagePresentationSignal(10, true, 1) },
                forwardCellImpactSignals: new[] { CreateImpactSignal(hit: true, targetEntityId: 10) });
            var builder = new GameplayVfxRequestPlanBuilder();

            new ProjectileVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(12, data, new CubeTopologyState(FaceId.Floor)),
                builder);
            new PlayerVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(12, data, new CubeTopologyState(FaceId.Floor)),
                builder);
            var plan = builder.Build();

            Assert.That(plan.Requests.Select(request => request.CueId).ToArray(), Is.EquivalentTo(new[]
            {
                GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact),
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
            }));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_MarkerLifecycle()
        {
            var owner = new GameObject("ForwardCellProjectileLifecycle");
            var markerPrefab = new GameObject("ForwardCellProjectileMarkerPrefab");
            var flightPrefab = new GameObject("ForwardCellProjectileFlightPrefab");
            var impactPrefab = new GameObject("ForwardCellProjectileImpactPrefab");
            VfxBindingDefinitionAsset markerBinding = null;
            VfxBindingDefinitionAsset flightBinding = null;
            VfxBindingDefinitionAsset impactBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                markerBinding = CreateBinding(ProjectileVfxCue.ForwardCellDangerMarker, markerPrefab, VfxPlaybackMode.Loop, VfxStopPolicy.StopEmittingThenRelease);
                flightBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileFlight, flightPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                impactBinding = CreateBinding(ProjectileVfxCue.ForwardCellImpact, impactPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(markerBinding, flightBinding, impactBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                var contextFactory = new PresentationContextFactory();

                runtime.Present(contextFactory.Create(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));

                runtime.Present(contextFactory.Create(CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(2));

                runtime.Present(contextFactory.Create(CreatePresentationData(forwardCellImpactSignals: new[] { CreateImpactSignal(hit: false) })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, markerBinding, flightBinding, impactBinding, impactPrefab, flightPrefab, markerPrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_MissingSocketFallsBack()
        {
            var owner = new GameObject("ForwardCellProjectileMissingSocket");
            var flightPrefab = new GameObject("ForwardCellProjectileMissingSocketFlightPrefab");
            VfxBindingDefinitionAsset flightBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                var sourceViewObject = new GameObject("SourceViewWithoutSocket");
                sourceViewObject.transform.SetParent(owner.transform, worldPositionStays: false);
                sourceViewObject.transform.localPosition = new Vector3(0.2f, 0.3f, 0.4f);
                var sourceView = sourceViewObject.AddComponent<GameplayEntityView>();
                sourceView.Initialize(40);
                sourceView.EnsureModelRoot();
                flightBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileFlight, flightPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(flightBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() }),
                    sourceView));

                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, flightBinding, flightPrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_MissingPrefabNoFatal()
        {
            var owner = new GameObject("ForwardCellProjectileMissingPrefab");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.DoesNotThrow(() =>
                    runtime.Present(CreateExtensionContext(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() }))));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_ResetCleansActiveVfx()
        {
            var owner = new GameObject("ForwardCellProjectileReset");
            var markerPrefab = new GameObject("ForwardCellProjectileResetMarkerPrefab");
            var flightPrefab = new GameObject("ForwardCellProjectileResetFlightPrefab");
            VfxBindingDefinitionAsset markerBinding = null;
            VfxBindingDefinitionAsset flightBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                markerBinding = CreateBinding(ProjectileVfxCue.ForwardCellDangerMarker, markerPrefab, VfxPlaybackMode.Loop, VfxStopPolicy.StopEmittingThenRelease);
                flightBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileFlight, flightPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(markerBinding, flightBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                var contextFactory = new PresentationContextFactory();

                runtime.Present(contextFactory.Create(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() })));
                runtime.Present(contextFactory.Create(CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(2));

                runtime.ResetSession();

                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(cueMap, markerBinding, flightBinding, flightPrefab, markerPrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_VfxDoesNotAffectDeterminism()
        {
            var owner = new GameObject("ForwardCellProjectileDeterminism");
            var markerPrefab = new GameObject("ForwardCellProjectileDeterminismMarkerPrefab");
            VfxBindingDefinitionAsset markerBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                markerBinding = CreateBinding(ProjectileVfxCue.ForwardCellDangerMarker, markerPrefab, VfxPlaybackMode.Loop, VfxStopPolicy.StopEmittingThenRelease);
                cueMap = CreateCueMap(markerBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                var result = CreateResult(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() }));

                runtime.EnableGameplayVfxForwardCellProjectile = true;
                runtime.Present(CreateExtensionContext(result));
                runtime.EnableGameplayVfxForwardCellProjectile = false;
                runtime.Present(CreateExtensionContext(result));

                Assert.That(result.DeterminismHash, Is.EqualTo("hash"));
                Assert.That(result.FinalEntities, Is.Empty);
                Assert.That(result.PresentationData.ForwardCellProjectileWindupSignals, Has.Count.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, markerBinding, markerPrefab, owner);
            }
        }

        private static GameplayVfxRequestPlan PlanProjectile(TickPresentationData data)
        {
            var builder = new GameplayVfxRequestPlanBuilder();
            new ProjectileVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(12, data, new CubeTopologyState(FaceId.Floor)),
                builder);
            return builder.Build();
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickPresentationData presentationData,
            GameplayEntityView sourceView = null)
        {
            return CreateExtensionContext(CreateResult(presentationData), sourceView);
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickResult result,
            GameplayEntityView sourceView = null)
        {
            return new PresentationContextFactory(sourceView).Create(result.PresentationData);
        }

        private static TickResult CreateResult(TickPresentationData presentationData)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            return new TickResult(
                12,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                topology,
                presentationData,
                "hash",
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            TickPlayerDamagePresentationSignal[] playerDamageSignals = null,
            TickForwardCellImpactPresentationSignal[] forwardCellImpactSignals = null,
            TickForwardCellProjectileWindupPresentationSignal[] windupSignals = null,
            TickForwardCellProjectileReleasePresentationSignal[] releaseSignals = null,
            TickForwardCellProjectileClearPresentationSignal[] clearSignals = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals ?? Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                forwardCellImpactSignals: forwardCellImpactSignals,
                forwardCellProjectileWindupSignals: windupSignals,
                forwardCellProjectileReleaseSignals: releaseSignals,
                forwardCellProjectileClearSignals: clearSignals);
        }

        private static TickForwardCellProjectileWindupPresentationSignal CreateWindupSignal()
        {
            return new TickForwardCellProjectileWindupPresentationSignal(
                presentationKey: 4000001,
                ownerId: 40,
                sourceEnemyId: 40,
                targetCell: TargetCell,
                direction: Direction.Right,
                startedTick: 10,
                expectedReleaseTick: 12,
                expectedImpactTick: 13);
        }

        private static TickForwardCellProjectileReleasePresentationSignal CreateReleaseSignal()
        {
            return new TickForwardCellProjectileReleasePresentationSignal(
                presentationKey: 4000001,
                impactId: 4000001,
                ownerId: 40,
                sourceEnemyId: 40,
                sourceCell: SourceCell,
                targetCell: TargetCell,
                direction: Direction.Right,
                releaseTick: 12,
                impactTick: 13,
                impactDelayTicks: 1);
        }

        private static TickForwardCellImpactPresentationSignal CreateImpactSignal(
            bool hit,
            int targetEntityId = 0)
        {
            return new TickForwardCellImpactPresentationSignal(
                impactId: 4000001,
                presentationKey: 4000001,
                ownerId: 40,
                sourceEnemyId: 40,
                targetCell: TargetCell,
                direction: Direction.Right,
                hit: hit,
                targetEntityId: targetEntityId);
        }

        private static VfxBindingDefinitionAsset CreateBinding(
            ProjectileVfxCue cue,
            GameObject prefab,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy)
        {
            EnsureVfxPrefabModelRoot(prefab);
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Projectile);
            SetField(binding, "cueCode", (int)cue);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", playbackMode);
            SetField(binding, "stopPolicy", stopPolicy);
            SetField(binding, "defaultLifetimeSeconds", 0.5f);
            SetField(binding, "tailSeconds", 0.05f);
            SetField(binding, "initialPoolSize", 0);
            SetField(binding, "maxConcurrentInstances", 8);
            return binding;
        }

        private static void EnsureVfxPrefabModelRoot(GameObject prefab)
        {
            if (prefab == null || prefab.transform.Find("ModelRoot") != null)
            {
                return;
            }

            var modelRoot = new GameObject("ModelRoot");
            modelRoot.transform.SetParent(prefab.transform, worldPositionStays: false);
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void Destroy(params UnityEngine.Object[] unityObjects)
        {
            for (var i = 0; i < unityObjects.Length; i++)
            {
                if (unityObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObjects[i]);
                }
            }
        }

        private sealed class PresentationContextFactory
        {
            private readonly CubeTopologyState topology = new(FaceId.Floor);
            private readonly GameplayPresentationStateStore stateStore = new();
            private readonly GameplayCubeProjector projector = new(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                1f);

            public PresentationContextFactory(GameplayEntityView sourceView = null)
            {
                stateStore.ResetSession(topology);
                if (sourceView != null)
                {
                    stateStore.ViewsByEntityId[40] = sourceView;
                }
            }

            public GameplayTickPresentationExtensionContext Create(TickPresentationData presentationData)
            {
                return new GameplayTickPresentationExtensionContext(
                    CreateResult(presentationData),
                    topology,
                    stateStore,
                    projector);
            }
        }
    }
}
