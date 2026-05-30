using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.EnemyAudio;
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
        private static GameplayVfxCueId MarkerCueId =>
            GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellDangerMarker);
        private static GameplayVfxCueId ActiveCueId =>
            GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileActive);
        private static GameplayVfxCueId FlightCueId =>
            GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileFlight);
        private static GameplayVfxCueId ImpactCueId =>
            GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact);

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_WindupSignalDoesNotCreateMarkerRequest()
        {
            var plan = PlanProjectile(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() }));

            Assert.That(plan.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_ReleaseSignalCreatesActiveAndFlightRequests()
        {
            var plan = PlanProjectile(CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() }));

            Assert.That(plan.Requests, Has.Count.EqualTo(2));
            Assert.That(plan.Requests.Select(request => request.CueId).ToArray(), Is.EquivalentTo(new[]
            {
                ActiveCueId,
                FlightCueId,
            }));
            Assert.That(plan.Requests.Single(request => request.CueId == ActiveCueId).Anchor.Cell, Is.EqualTo(SourceCell));
            Assert.That(plan.Requests.Single(request => request.CueId == FlightCueId).CueId, Is.Not.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact)));
            Assert.That(plan.Requests.Any(request => request.CueId.Equals(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact))), Is.False);
        }

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_ArrivalSignalCreatesImpactVfx()
        {
            var data = CreatePresentationData(arrivalSignals: new[] { CreateArrivalSignal(PendingCellImpactResolutionKind.Hit, targetEntityId: 10) });
            var plan = PlanProjectile(data);
            var playerBuilder = new GameplayVfxRequestPlanBuilder();

            new PlayerVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(12, data, new CubeTopologyState(FaceId.Floor)),
                playerBuilder);

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            Assert.That(plan.Requests[0].CueId, Is.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact)));
            Assert.That(plan.Requests[0].Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(playerBuilder.Build().Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_MissArrivalCreatesImpactVfx()
        {
            var data = CreatePresentationData(arrivalSignals: new[] { CreateArrivalSignal(PendingCellImpactResolutionKind.Miss) });
            var plan = PlanProjectile(data);

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            Assert.That(plan.Requests[0].CueId, Is.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact)));
            Assert.That(plan.Requests[0].Anchor.Cell, Is.EqualTo(TargetCell));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_MissArrival_CreatesImpactVfxAndProjectileImpactSfx()
        {
            var data = CreatePresentationData(arrivalSignals: new[] { CreateArrivalSignal(PendingCellImpactResolutionKind.Miss) });
            var vfxPlan = PlanProjectile(data);
            var audioRequests = new EnemyAudioRequestPlanner().BuildRequests(CreateResult(data));

            Assert.That(
                vfxPlan.Requests.Count(request => request.CueId.Equals(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact))),
                Is.EqualTo(1));
            Assert.That(audioRequests.Count(request => request.Cue == EnemyAudioCue.ProjectileImpact), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_HitArrival_CreatesImpactVfxAndProjectileImpactSfxAndHitSignal()
        {
            var data = CreatePresentationData(
                forwardCellImpactSignals: new[] { CreateImpactSignal(hit: true, targetEntityId: 10) },
                arrivalSignals: new[] { CreateArrivalSignal(PendingCellImpactResolutionKind.Hit, targetEntityId: 10) });
            var vfxPlan = PlanProjectile(data);
            var audioRequests = new EnemyAudioRequestPlanner().BuildRequests(CreateResult(data));

            Assert.That(data.ForwardCellImpactSignals, Has.Count.EqualTo(1));
            Assert.That(
                vfxPlan.Requests.Count(request => request.CueId.Equals(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact))),
                Is.EqualTo(1));
            Assert.That(audioRequests.Count(request => request.Cue == EnemyAudioCue.ProjectileImpact), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_ExpiredTopologyInvalid_CreatesNoImpactVfxOrSfx()
        {
            AssertInvalidForwardCellProjectileArrivalCreatesNoImpactVfxOrSfx(
                PendingCellImpactResolutionKind.ExpiredTopologyInvalid);
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_CancelledTargetInvalid_CreatesNoImpactVfxOrSfx()
        {
            AssertInvalidForwardCellProjectileArrivalCreatesNoImpactVfxOrSfx(
                PendingCellImpactResolutionKind.CancelledTargetInvalid);
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_ProjectileImpactVfxAndSfx_ShareArrivalPolicy()
        {
            var audioPlanner = new EnemyAudioRequestPlanner();
            var validArrivalKinds = new[]
            {
                PendingCellImpactResolutionKind.Hit,
                PendingCellImpactResolutionKind.Miss,
            };

            for (var i = 0; i < validArrivalKinds.Length; i++)
            {
                var data = CreatePresentationData(arrivalSignals: new[] { CreateArrivalSignal(validArrivalKinds[i], targetEntityId: 10) });
                var vfxPlan = PlanProjectile(data);
                var audioRequests = audioPlanner.BuildRequests(CreateResult(data));

                Assert.That(
                    vfxPlan.Requests.Count(request => request.CueId.Equals(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact))),
                    Is.EqualTo(1));
                Assert.That(
                    audioRequests.Count(request => request.Cue == EnemyAudioCue.ProjectileImpact),
                    Is.EqualTo(1));
            }

            var impactOnlyData = CreatePresentationData(forwardCellImpactSignals: new[] { CreateImpactSignal(hit: true, targetEntityId: 10) });

            Assert.That(PlanProjectile(impactOnlyData).Requests, Is.Empty);
            Assert.That(
                audioPlanner.BuildRequests(CreateResult(impactOnlyData)).Select(request => request.Cue).ToArray(),
                Has.No.EqualTo(EnemyAudioCue.ProjectileImpact));
        }

        private static void AssertInvalidForwardCellProjectileArrivalCreatesNoImpactVfxOrSfx(
            PendingCellImpactResolutionKind resolutionKind)
        {
            var data = CreatePresentationData(arrivalSignals: new[] { CreateArrivalSignal(resolutionKind) });
            var vfxPlan = PlanProjectile(data);
            var audioRequests = new EnemyAudioRequestPlanner().BuildRequests(CreateResult(data));

            Assert.That(
                vfxPlan.Requests.Select(request => request.CueId).ToArray(),
                Has.No.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact)));
            Assert.That(audioRequests.Select(request => request.Cue).ToArray(), Has.No.EqualTo(EnemyAudioCue.ProjectileImpact));
        }

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_ImpactSignalWithoutArrivalDoesNotCreateImpactVfx()
        {
            var data = CreatePresentationData(forwardCellImpactSignals: new[] { CreateImpactSignal(hit: true, targetEntityId: 10) });
            var plan = PlanProjectile(data);

            Assert.That(plan.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ForwardCellProjectile_ImpactHitSeparatesImpactAndDamageFeedback()
        {
            var data = CreatePresentationData(
                playerDamageSignals: new[] { new TickPlayerDamagePresentationSignal(10, true, 1) },
                forwardCellImpactSignals: new[] { CreateImpactSignal(hit: true, targetEntityId: 10) },
                arrivalSignals: new[] { CreateArrivalSignal(PendingCellImpactResolutionKind.Hit, targetEntityId: 10) });
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
        public void ForwardCellProjectile_ReleaseAndImpactLifecycle()
        {
            var owner = new GameObject("ForwardCellProjectileLifecycle");
            var activePrefab = new GameObject("ForwardCellProjectileActivePrefab");
            var flightPrefab = new GameObject("ForwardCellProjectileFlightPrefab");
            var impactPrefab = new GameObject("ForwardCellProjectileImpactPrefab");
            VfxBindingDefinitionAsset activeBinding = null;
            VfxBindingDefinitionAsset flightBinding = null;
            VfxBindingDefinitionAsset impactBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                activeBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileActive, activePrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                flightBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileFlight, flightPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                impactBinding = CreateBinding(ProjectileVfxCue.ForwardCellImpact, impactPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(activeBinding, flightBinding, impactBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                var contextFactory = new PresentationContextFactory();

                runtime.Present(contextFactory.Create(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);

                runtime.Present(contextFactory.Create(CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(2));

                runtime.Present(contextFactory.Create(CreatePresentationData(arrivalSignals: new[] { CreateArrivalSignal(PendingCellImpactResolutionKind.Hit, targetEntityId: 10) })));
                Assert.That(runtime.GetActiveVfxInstanceCount(FlightCueId), Is.Zero);
                Assert.That(runtime.GetActiveVfxInstanceCount(ImpactCueId), Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, activeBinding, flightBinding, impactBinding, impactPrefab, flightPrefab, activePrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_ClearSignal_RemovesMarkerAndDoesNotSpawnFlight()
        {
            using var fixture = new ForwardCellProjectileRuntimeFixture("ForwardCellProjectileClear");
            var clearData = CreatePresentationData(clearSignals: new[] { CreateClearSignal() });
            var playerBuilder = new GameplayVfxRequestPlanBuilder();

            fixture.Present(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() }));
            AssertActiveMarkerKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(MarkerCueId), Is.Zero);

            new PlayerVfxRequestPlanner().Plan(
                new GameplayVfxPlanningContext(12, clearData, new CubeTopologyState(FaceId.Floor)),
                playerBuilder);
            Assert.That(PlanProjectile(clearData).Requests, Is.Empty);
            Assert.That(playerBuilder.Build().Requests, Is.Empty);

            Assert.DoesNotThrow(() => fixture.Present(clearData));
            AssertActiveMarkerKeys(fixture.Runtime);
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(MarkerCueId), Is.Zero);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.Zero);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(ImpactCueId), Is.Zero);
            Assert.That(fixture.Runtime.LastPlannedRequestCount, Is.Zero);

            Assert.DoesNotThrow(() => fixture.Present(clearData));
            AssertActiveMarkerKeys(fixture.Runtime);
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(MarkerCueId), Is.Zero);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.Zero);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(ImpactCueId), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_ImpactBeforeFlightCompletion_ReleasesCarrierOnce()
        {
            using var fixture = new ForwardCellProjectileRuntimeFixture("ForwardCellProjectileEarlyImpact");

            fixture.Present(CreatePresentationData(releaseSignals: new[]
            {
                CreateReleaseSignal(impactDelayTicks: 120),
            }));
            AssertActiveMarkerKeys(fixture.Runtime);
            AssertActiveCarrierKeys(fixture.Runtime, 4000001);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.EqualTo(1));
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(FlightCueId), Is.Zero);

            Assert.DoesNotThrow(() => fixture.Present(CreatePresentationData(arrivalSignals: new[]
            {
                CreateArrivalSignal(PendingCellImpactResolutionKind.Hit, targetEntityId: 10),
            })));
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(FlightCueId), Is.EqualTo(1));
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.Zero);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(ImpactCueId), Is.EqualTo(1));

            Assert.DoesNotThrow(() => fixture.Runtime.UpdatePresentation(10f));
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(FlightCueId), Is.EqualTo(1));
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_FlightDurationUsesImpactDelayTicks()
        {
            using var fixture = new ForwardCellProjectileRuntimeFixture("ForwardCellProjectileFlightDuration");

            fixture.Present(CreatePresentationData(releaseSignals: new[]
            {
                CreateReleaseSignal(impactDelayTicks: 96),
            }));
            AssertActiveCarrierKeys(fixture.Runtime, 4000001);

            fixture.Runtime.UpdatePresentation(1.5f);
            AssertActiveCarrierKeys(fixture.Runtime, 4000001);

            fixture.Runtime.UpdatePresentation(0.11f);
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(FlightCueId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_TwoOwnersSameTargetCell_DoNotCrossCleanup()
        {
            const int enemyA = 40;
            const int enemyB = 41;
            const int keyA = 4000001;
            const int keyB = 4100001;

            using var fixture = new ForwardCellProjectileRuntimeFixture("ForwardCellProjectileTwoOwners");

            fixture.Present(CreatePresentationData(windupSignals: new[]
            {
                CreateWindupSignal(presentationKey: keyA, ownerId: enemyA),
                CreateWindupSignal(presentationKey: keyB, ownerId: enemyB),
            }));
            AssertActiveMarkerKeys(fixture.Runtime);
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(MarkerCueId), Is.Zero);

            fixture.Present(CreatePresentationData(releaseSignals: new[]
            {
                CreateReleaseSignal(presentationKey: keyA, impactId: keyA, ownerId: enemyA),
            }));
            AssertActiveMarkerKeys(fixture.Runtime);
            AssertActiveCarrierKeys(fixture.Runtime, keyA);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(MarkerCueId), Is.Zero);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.EqualTo(1));

            fixture.Present(CreatePresentationData(arrivalSignals: new[]
            {
                CreateArrivalSignal(PendingCellImpactResolutionKind.Hit, targetEntityId: 10, presentationKey: keyA, impactId: keyA, ownerId: enemyA),
            }));
            AssertActiveMarkerKeys(fixture.Runtime);
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(MarkerCueId), Is.Zero);
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(FlightCueId), Is.EqualTo(1));
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(MarkerCueId), Is.Zero);
            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(ImpactCueId), Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void ProjectileVfx_InactiveTargetCell_Suppressed()
        {
            using var fixture = new ForwardCellProjectileRuntimeFixture(
                "ForwardCellProjectileInactiveTarget",
                GameplayVfxVisibilityMode.DefaultGameplay);

            fixture.Present(CreatePresentationData(releaseSignals: new[]
            {
                CreateReleaseSignal(targetCell: new SurfaceCell(FaceId.Ceiling, 1, 0)),
            }));

            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.Zero);
            AssertActiveCarrierKeys(fixture.Runtime);
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionStart_ClearsProjectileGameplayVfxImmediately()
        {
            using var fixture = new ForwardCellProjectileRuntimeFixture("ForwardCellProjectileTopologyClear");
            fixture.Present(CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() }));
            AssertActiveCarrierKeys(fixture.Runtime, 4000001);

            fixture.Present(CreatePresentationData(
                topologyMotion: new TickTopologyMotion(
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward)));

            AssertActiveMarkerKeys(fixture.Runtime);
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.ActiveVfxInstanceCount, Is.Zero);
            Assert.That(fixture.Runtime.GetReleaseToPoolCount(FlightCueId), Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionRunning_SuppressesProjectileStarts()
        {
            using var fixture = new ForwardCellProjectileRuntimeFixture("ForwardCellProjectileTopologySuppress");
            fixture.Present(CreatePresentationData(
                topologyMotion: new TickTopologyMotion(
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward)));

            fixture.Present(CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() }));

            AssertActiveMarkerKeys(fixture.Runtime);
            AssertActiveCarrierKeys(fixture.Runtime);
            Assert.That(fixture.Runtime.ActiveVfxInstanceCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void ProjectileVfx_VisibleSurfaceAllowedTarget_AllowedOnlyWhenOptIn()
        {
            using var defaultFixture = new ForwardCellProjectileRuntimeFixture(
                "ForwardCellProjectileVisibleSurfaceDefault",
                GameplayVfxVisibilityMode.DefaultGameplay);
            using var optInFixture = new ForwardCellProjectileRuntimeFixture(
                "ForwardCellProjectileVisibleSurfaceOptIn",
                GameplayVfxVisibilityMode.VisibleSurfaceAllowed);
            var data = CreatePresentationData(releaseSignals: new[]
            {
                CreateReleaseSignal(targetCell: new SurfaceCell(FaceId.Ceiling, 1, 0)),
            });

            defaultFixture.Present(data);
            optInFixture.Present(data);

            Assert.That(defaultFixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.Zero);
            Assert.That(optInFixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void ProjectileVfx_PresentationOnly_BypassesGameplayFaceGate()
        {
            using var fixture = new ForwardCellProjectileRuntimeFixture(
                "ForwardCellProjectilePresentationOnly",
                GameplayVfxVisibilityMode.PresentationOnly);

            fixture.Present(CreatePresentationData(releaseSignals: new[]
            {
                CreateReleaseSignal(targetCell: new SurfaceCell(FaceId.Ceiling, 1, 0)),
            }));

            Assert.That(fixture.Runtime.GetActiveVfxInstanceCount(FlightCueId), Is.EqualTo(1));
            AssertActiveCarrierKeys(fixture.Runtime, 4000001);
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
        public void ForwardCellProjectile_AuthoredProjectileMuzzleOverridesLegacySocket()
        {
            var owner = new GameObject("ForwardCellProjectileAttachPoint");
            var flightPrefab = new GameObject("ForwardCellProjectileAttachPointFlightPrefab");
            VfxBindingDefinitionAsset flightBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                var sourceViewObject = new GameObject("SourceViewWithProjectileMuzzle");
                sourceViewObject.transform.SetParent(owner.transform, worldPositionStays: false);
                var sourceView = sourceViewObject.AddComponent<GameplayEntityView>();
                sourceView.Initialize(40);
                var modelRoot = sourceView.EnsureModelRoot();
                sourceViewObject.AddComponent<EnemyForwardCellProjectileVfxAuthoring>();
                var muzzle = CreateAttachPoint(modelRoot, "ProjectileMuzzle");
                muzzle.localPosition = new Vector3(0.25f, 0.5f, 0.75f);
                var legacyEye = new GameObject("Eye");
                legacyEye.transform.SetParent(modelRoot, worldPositionStays: false);
                legacyEye.transform.localPosition = new Vector3(2f, 3f, 4f);

                flightBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileFlight, flightPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(flightBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() }),
                    sourceView));

                var flightInstance = FindPooledVfx(owner, flightPrefab.name);
                Assert.That(flightInstance, Is.Not.Null);
                Assert.That(flightInstance.localPosition.x, Is.EqualTo(muzzle.localPosition.x).Within(0.0001f));
                Assert.That(flightInstance.localPosition.y, Is.EqualTo(muzzle.localPosition.y).Within(0.0001f));
                Assert.That(flightInstance.localPosition.z, Is.EqualTo(muzzle.localPosition.z).Within(0.0001f));
            }
            finally
            {
                Destroy(cueMap, flightBinding, flightPrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_FlightMotionUsesMildEaseIn()
        {
            var owner = new GameObject("ForwardCellProjectileEaseIn");
            var flightPrefab = new GameObject("ForwardCellProjectileEaseInFlightPrefab");
            VfxBindingDefinitionAsset flightBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                flightBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileFlight, flightPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(flightBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreatePresentationData(releaseSignals: new[]
                {
                    CreateReleaseSignal(impactDelayTicks: 24),
                })));
                var flightInstance = FindPooledVfx(owner, flightPrefab.name);
                Assert.That(flightInstance, Is.Not.Null);

                var source = flightInstance.localPosition;
                var target = ResolveCellLocalPosition(TargetCell, VfxAnchorSlot.CellFloor);

                runtime.UpdatePresentation(0.2f);

                var distanceX = target.x - source.x;
                Assert.That(Mathf.Abs(distanceX), Is.GreaterThan(0.0001f));
                var progressX = (flightInstance.localPosition.x - source.x) / distanceX;
                Assert.That(progressX, Is.EqualTo(0.4375f).Within(0.0001f));
                Assert.That(progressX, Is.LessThan(0.5f));
            }
            finally
            {
                Destroy(cueMap, flightBinding, flightPrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_FlightTargetUsesCellFloor()
        {
            var owner = new GameObject("ForwardCellProjectileCellFloorTarget");
            var flightPrefab = new GameObject("ForwardCellProjectileCellFloorTargetFlightPrefab");
            VfxBindingDefinitionAsset flightBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                flightBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileFlight, flightPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(flightBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreatePresentationData(releaseSignals: new[]
                {
                    CreateReleaseSignal(impactDelayTicks: 240),
                })));
                var flightInstance = FindPooledVfx(owner, flightPrefab.name);
                Assert.That(flightInstance, Is.Not.Null);

                runtime.UpdatePresentation(3.996f);

                var expectedTarget = ResolveCellLocalPosition(TargetCell, VfxAnchorSlot.CellFloor);
                var centerTarget = ResolveCellLocalPosition(TargetCell, VfxAnchorSlot.CellCenter);
                Assert.That(Vector3.Distance(flightInstance.localPosition, expectedTarget), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(flightInstance.localPosition, centerTarget), Is.GreaterThan(0.2f));
            }
            finally
            {
                Destroy(cueMap, flightBinding, flightPrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_FlightArcUsesTargetSurfaceNormal()
        {
            var owner = new GameObject("ForwardCellProjectileSurfaceNormalArc");
            var flightPrefab = new GameObject("ForwardCellProjectileSurfaceNormalArcFlightPrefab");
            VfxBindingDefinitionAsset flightBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                var sourceCell = new SurfaceCell(FaceId.Ceiling, 0, 0);
                var targetCell = new SurfaceCell(FaceId.Ceiling, 1, 0);
                flightBinding = CreateBinding(
                    ProjectileVfxCue.ForwardCellProjectileFlight,
                    flightPrefab,
                    VfxPlaybackMode.OneShot,
                    VfxStopPolicy.AuthoredDuration,
                    GameplayVfxVisibilityMode.PresentationOnly);
                cueMap = CreateCueMap(flightBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreatePresentationData(releaseSignals: new[]
                {
                    CreateReleaseSignal(
                        sourceCell: sourceCell,
                        targetCell: targetCell,
                        impactDelayTicks: 24),
                })));
                var flightInstance = FindPooledVfx(owner, flightPrefab.name);
                Assert.That(flightInstance, Is.Not.Null);

                var source = flightInstance.localPosition;
                var target = ResolveCellLocalPosition(
                    targetCell,
                    VfxAnchorSlot.CellFloor,
                    GameplayVfxVisibilityMode.PresentationOnly);
                var targetNormal = ResolveSurfaceNormal(targetCell);

                runtime.UpdatePresentation(0.2f);

                var linearAtProgress = Vector3.Lerp(source, target, 0.4375f);
                var arcOffset = flightInstance.localPosition - linearAtProgress;
                Assert.That(Vector3.Dot(arcOffset, -targetNormal), Is.GreaterThan(0.3f));
                Assert.That(Vector3.Dot(arcOffset, Vector3.up), Is.LessThan(-0.3f));
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
                Assert.That(runtime.MissingBindingCount, Is.Zero);
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
            var flightPrefab = new GameObject("ForwardCellProjectileResetFlightPrefab");
            VfxBindingDefinitionAsset flightBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                flightBinding = CreateBinding(ProjectileVfxCue.ForwardCellProjectileFlight, flightPrefab, VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(flightBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                var contextFactory = new PresentationContextFactory();

                runtime.Present(contextFactory.Create(CreatePresentationData(windupSignals: new[] { CreateWindupSignal() })));
                runtime.Present(contextFactory.Create(CreatePresentationData(releaseSignals: new[] { CreateReleaseSignal() })));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));

                runtime.ResetSession();

                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(cueMap, flightBinding, flightPrefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellProjectile_VfxDoesNotAffectDeterminism()
        {
            var owner = new GameObject("ForwardCellProjectileDeterminism");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
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
                Destroy(owner);
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
            TickForwardCellProjectileArrivalPresentationSignal[] arrivalSignals = null,
            TickForwardCellProjectileWindupPresentationSignal[] windupSignals = null,
            TickForwardCellProjectileReleasePresentationSignal[] releaseSignals = null,
            TickForwardCellProjectileClearPresentationSignal[] clearSignals = null,
            TickTopologyMotion? topologyMotion = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: topologyMotion,
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
                forwardCellProjectileArrivalSignals: arrivalSignals,
                forwardCellProjectileWindupSignals: windupSignals,
                forwardCellProjectileReleaseSignals: releaseSignals,
                forwardCellProjectileClearSignals: clearSignals);
        }

        private static TickForwardCellProjectileWindupPresentationSignal CreateWindupSignal(
            int presentationKey = 4000001,
            int ownerId = 40,
            int sourceEnemyId = 0,
            SurfaceCell? targetCell = null,
            Direction direction = Direction.Right,
            int startedTick = 10,
            int expectedReleaseTick = 12,
            int expectedImpactTick = 13)
        {
            return new TickForwardCellProjectileWindupPresentationSignal(
                presentationKey: presentationKey,
                ownerId: ownerId,
                sourceEnemyId: sourceEnemyId != 0 ? sourceEnemyId : ownerId,
                targetCell: targetCell ?? TargetCell,
                direction: direction,
                startedTick: startedTick,
                expectedReleaseTick: expectedReleaseTick,
                expectedImpactTick: expectedImpactTick);
        }

        private static TickForwardCellProjectileReleasePresentationSignal CreateReleaseSignal(
            int presentationKey = 4000001,
            int impactId = 4000001,
            int ownerId = 40,
            int sourceEnemyId = 0,
            SurfaceCell? sourceCell = null,
            SurfaceCell? targetCell = null,
            Direction direction = Direction.Right,
            int releaseTick = 12,
            int impactTick = 13,
            int impactDelayTicks = 1)
        {
            return new TickForwardCellProjectileReleasePresentationSignal(
                presentationKey: presentationKey,
                impactId: impactId,
                ownerId: ownerId,
                sourceEnemyId: sourceEnemyId != 0 ? sourceEnemyId : ownerId,
                sourceCell: sourceCell ?? SourceCell,
                targetCell: targetCell ?? TargetCell,
                direction: direction,
                releaseTick: releaseTick,
                impactTick: impactTick,
                impactDelayTicks: impactDelayTicks);
        }

        private static TickForwardCellImpactPresentationSignal CreateImpactSignal(
            bool hit,
            int targetEntityId = 0,
            int presentationKey = 4000001,
            int impactId = 4000001,
            int ownerId = 40,
            int sourceEnemyId = 0,
            SurfaceCell? targetCell = null,
            Direction direction = Direction.Right)
        {
            return new TickForwardCellImpactPresentationSignal(
                impactId: impactId,
                presentationKey: presentationKey,
                ownerId: ownerId,
                sourceEnemyId: sourceEnemyId != 0 ? sourceEnemyId : ownerId,
                targetCell: targetCell ?? TargetCell,
                direction: direction,
                hit: hit,
                targetEntityId: targetEntityId);
        }

        private static TickForwardCellProjectileArrivalPresentationSignal CreateArrivalSignal(
            PendingCellImpactResolutionKind resolutionKind,
            int targetEntityId = 0,
            int presentationKey = 4000001,
            int impactId = 4000001,
            int ownerId = 40,
            int sourceEnemyId = 0,
            SurfaceCell? targetCell = null,
            Direction direction = Direction.Right,
            int impactTick = 13)
        {
            return new TickForwardCellProjectileArrivalPresentationSignal(
                impactId: impactId,
                presentationKey: presentationKey,
                ownerId: ownerId,
                sourceEnemyId: sourceEnemyId != 0 ? sourceEnemyId : ownerId,
                targetCell: targetCell ?? TargetCell,
                direction: direction,
                impactTick: impactTick,
                resolutionKind: resolutionKind,
                targetEntityId: targetEntityId);
        }

        private static TickForwardCellProjectileClearPresentationSignal CreateClearSignal(
            int presentationKey = 4000001,
            int ownerId = 40,
            int sourceEnemyId = 0,
            SurfaceCell? targetCell = null,
            Direction direction = Direction.Right,
            int startedTick = 10,
            ForwardCellProjectileClearReason reason = ForwardCellProjectileClearReason.Canceled)
        {
            return new TickForwardCellProjectileClearPresentationSignal(
                presentationKey: presentationKey,
                ownerId: ownerId,
                sourceEnemyId: sourceEnemyId != 0 ? sourceEnemyId : ownerId,
                targetCell: targetCell ?? TargetCell,
                direction: direction,
                startedTick: startedTick,
                reason: reason);
        }

        private static VfxBindingDefinitionAsset CreateBinding(
            ProjectileVfxCue cue,
            GameObject prefab,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            GameplayVfxVisibilityMode visibilityMode = GameplayVfxVisibilityMode.DefaultGameplay)
        {
            EnsureVfxPrefabModelRoot(prefab);
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            binding.name = visibilityMode == GameplayVfxVisibilityMode.PresentationOnly
                ? $"PresentationOnly_{cue}_Binding"
                : $"{cue}_Binding";
            SetField(binding, "family", GameplayVfxFamily.Projectile);
            SetField(binding, "cueCode", (int)cue);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", playbackMode);
            SetField(binding, "stopPolicy", stopPolicy);
            SetField(binding, "visibilityMode", visibilityMode);
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

        private static Transform CreateAttachPoint(Transform parent, string id)
        {
            var anchorObject = new GameObject("VfxAttach_" + id);
            anchorObject.transform.SetParent(parent, worldPositionStays: false);
            var attachPoint = anchorObject.AddComponent<GameplayVfxAttachPoint>();
            SetField(attachPoint, "id", id);
            return anchorObject.transform;
        }

        private static Transform FindPooledVfx(GameObject root, string prefabName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform != null && transform.name == $"{prefabName}_PooledVfx");
        }

        private static Vector3 ResolveCellLocalPosition(
            SurfaceCell cell,
            VfxAnchorSlot slot,
            GameplayVfxVisibilityMode visibilityMode = GameplayVfxVisibilityMode.DefaultGameplay)
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                1f);
            var cellProjector = new GameplayVfxHostCellAnchorProjector(projector);
            Assert.That(
                cellProjector.TryResolveCell(
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    slot,
                    visibilityMode,
                    out var anchor),
                Is.True);
            Assert.That(anchor.IsResolved, Is.True);
            Assert.That(anchor.HasLocalPose, Is.True);
            return anchor.LocalPosition;
        }

        private static Vector3 ResolveSurfaceNormal(SurfaceCell cell)
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                1f);
            Assert.That(
                projector.TryProjectSurfaceCell(
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    out var projectedPose),
                Is.True);
            return projectedPose.Normal;
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

        private static void AssertActiveMarkerKeys(
            GameplayVfxProductionRuntime runtime,
            params int[] expectedKeys)
        {
            Assert.That(runtime.ActiveForwardCellProjectileMarkerCount, Is.EqualTo(expectedKeys.Length));
            Assert.That(runtime.ActiveForwardCellProjectileMarkerKeys, Is.EquivalentTo(expectedKeys));
        }

        private static void AssertActiveCarrierKeys(
            GameplayVfxProductionRuntime runtime,
            params int[] expectedKeys)
        {
            Assert.That(runtime.ActiveForwardCellProjectileFlightCount, Is.EqualTo(expectedKeys.Length));
            Assert.That(runtime.ActiveForwardCellProjectileFlightKeys, Is.EquivalentTo(expectedKeys));
        }

        private sealed class ForwardCellProjectileRuntimeFixture : IDisposable
        {
            private readonly GameObject owner;
            private readonly GameObject markerPrefab;
            private readonly GameObject flightPrefab;
            private readonly GameObject impactPrefab;
            private readonly VfxBindingDefinitionAsset markerBinding;
            private readonly VfxBindingDefinitionAsset flightBinding;
            private readonly VfxBindingDefinitionAsset impactBinding;
            private readonly VfxCueMapAsset cueMap;
            private readonly PresentationContextFactory contextFactory = new();

            public ForwardCellProjectileRuntimeFixture(
                string name,
                GameplayVfxVisibilityMode flightVisibilityMode = GameplayVfxVisibilityMode.DefaultGameplay)
            {
                owner = new GameObject(name);
                markerPrefab = new GameObject($"{name}MarkerPrefab");
                flightPrefab = new GameObject($"{name}FlightPrefab");
                impactPrefab = new GameObject($"{name}ImpactPrefab");
                markerBinding = CreateBinding(
                    ProjectileVfxCue.ForwardCellDangerMarker,
                    markerPrefab,
                    VfxPlaybackMode.Loop,
                    VfxStopPolicy.StopEmittingThenRelease);
                flightBinding = CreateBinding(
                    ProjectileVfxCue.ForwardCellProjectileFlight,
                    flightPrefab,
                    VfxPlaybackMode.OneShot,
                    VfxStopPolicy.AuthoredDuration,
                    flightVisibilityMode);
                impactBinding = CreateBinding(
                    ProjectileVfxCue.ForwardCellImpact,
                    impactPrefab,
                    VfxPlaybackMode.OneShot,
                    VfxStopPolicy.AuthoredDuration);
                cueMap = CreateCueMap(markerBinding, flightBinding, impactBinding);
                Runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                Runtime.ConfigureHostDefaultMap(cueMap);
            }

            public GameplayVfxProductionRuntime Runtime { get; }

            public void Present(TickPresentationData presentationData)
            {
                Runtime.Present(contextFactory.Create(presentationData));
            }

            public void Dispose()
            {
                Destroy(cueMap, markerBinding, flightBinding, impactBinding, impactPrefab, flightPrefab, markerPrefab, owner);
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
