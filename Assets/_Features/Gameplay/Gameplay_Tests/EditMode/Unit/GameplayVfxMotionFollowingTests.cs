using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxMotionFollowingTests
    {
        private const string StayTrailPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FlipImpactStayTrailVfx.prefab";
        private const string StayTrailBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FlipImpactStayTrail_Binding.asset";
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_FlagDefaultTrue()
        {
            var owner = new GameObject("FlipImpactStayTrailDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxFlipImpactStayTrail, Is.True);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_AttachesWhenStayMotionStarts()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);

                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.GetChild(0).name, Does.Contain("PooledVfx"));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_RemainsAttachedAcrossPoolAdvanceWhileDesired()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);
                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);
                var instance = fixture.View.ModelRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 1f;
                fixture.Pool.Advance(1f);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(instance.parent, Is.EqualTo(fixture.View.ModelRoot));
                Assert.That(fixture.Root.TailRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.PoolRoot.childCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_DoesNotSpawnWithoutStayMotion()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.Zero);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_DoesNotSpawnForNonStayTrackKind()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(
                    entityId: 30,
                    kind: PresentationMotionKind.None);

                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.Zero);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_DoesNotSpawnWhenFlagOff()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);

                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: false);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.Zero);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlagOff_DetachesControllerManagedAttachedFollower()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);
                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);

                fixture.Controller.Refresh(
                    11,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: false);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_MissingBinding_DiagnosticNoMotionSuppression()
        {
            var fixture = CreateFixture(resolveBinding: false);
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);

                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);
                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);

                Assert.That(fixture.Controller.MissingBindingCount, Is.EqualTo(1));
                Assert.That(fixture.TrackState.OriginalViewMotionTracks, Does.ContainKey(30));
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_MissingOwnerView_DiagnosticNoCrash()
        {
            var fixture = CreateFixture(registerView: false);
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);

                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);

                Assert.That(fixture.Controller.MissingOwnerViewCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void MissingOwner_DetachesExistingFollower()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);
                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);

                fixture.StateStore.ViewsByEntityId.Remove(30);
                fixture.Controller.Refresh(
                    11,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: true);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_StopsAndDetachesWhenMotionCompletes()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);
                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);

                fixture.TrackState.OriginalViewMotionTracks.Clear();
                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_DetachedTailReleases()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);
                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);
                fixture.TrackState.OriginalViewMotionTracks.Clear();
                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);

                fixture.TimeProvider.TimeSeconds = 0.30f;
                fixture.Pool.Advance(0.30f);

                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
                Assert.That(fixture.Root.PoolRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_DoesNotRespawnEveryFrame()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);

                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);
                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_NewMotionKey_ReplacesOldHandle()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30, correlationId: 100);
                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);

                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30, correlationId: 101);
                fixture.Controller.Refresh(11, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(2));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_HardCleanup_ReleasesAll()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);
                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);

                fixture.Controller.HardCleanup();

                Assert.That(fixture.Controller.ActiveHandleCount, Is.Zero);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionStart_ClearsFollowerGameplayVfxImmediately()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TrackState.OriginalViewMotionTracks[30] = CreateTrack(entityId: 30);
                fixture.Controller.Refresh(10, fixture.TrackState, fixture.StateStore, fixture.Pool, fixture.BindingResolver, enabled: true);

                fixture.Controller.ClearForTopologyTransitionStart(fixture.Pool);

                Assert.That(fixture.Controller.ActiveHandleCount, Is.Zero);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrailPrefab_PassesValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StayTrailPrefabPath);

            Assert.That(prefab, Is.Not.Null, StayTrailPrefabPath);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrailBinding_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(StayTrailBindingPath);

            Assert.That(binding, Is.Not.Null, StayTrailBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail)));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesFlipImpactStayTrail()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            var composition = GameplayVfxBindingComposition.Compose(cueMap, System.Array.Empty<VfxProfileAsset>());
            var request = new GameplayVfxRequest(
                1,
                7,
                7,
                sourceEntityId: 30,
                GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail),
                VfxAnchor.ForEntity(30),
                VfxTimingKind.DuringMotion);

            Assert.That(composition.Succeeded, Is.True, string.Join("\n", composition.Validation.Messages));
            Assert.That(composition.Resolver.TryResolve(request, out var policy), Is.True);
            Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail)));
        }

        private static MotionFollowerFixture CreateFixture(
            bool resolveBinding = true,
            bool registerView = true,
            float tailSeconds = 0.25f)
        {
            var owner = new GameObject("MotionFollowerFixture");
            var root = GameplayVfxRuntimeRoot.CreateUnder(owner.transform);
            var prefab = GameplayVfxParameterizedMotionRuntimeTests.CreateRuntimePrefab("FlipImpactStayTrailRuntimePrefab");
            var timeProvider = new GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider();
            var pool = new GameplayVfxGameObjectPool(
                root,
                new GameplayVfxParameterizedMotionRuntimeTests.SinglePrefabProvider(prefab),
                timeProvider);
            var stateStore = new GameplayPresentationStateStore();
            var viewObject = new GameObject("BoxView");
            viewObject.transform.SetParent(owner.transform, worldPositionStays: false);
            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(30);
            view.EnsureModelRoot();
            if (registerView)
            {
                stateStore.ViewsByEntityId[30] = view;
            }

            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail);
            var policy = new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic,
                VfxPlaybackMode.Follow,
                VfxStopPolicy.DetachThenStopEmittingThenRelease,
                defaultLifetimeSeconds: 0f,
                tailSeconds: tailSeconds,
                maxConcurrentInstances: 8);
            return new MotionFollowerFixture(
                owner,
                prefab,
                root,
                pool,
                timeProvider,
                stateStore,
                view,
                new GameplayPresentationTrackState(),
                new PresentationMotionFollowingVfxController(),
                new SinglePolicyResolver(resolveBinding, policy));
        }

        private static PresentationMotionTrack CreateTrack(
            int entityId,
            PresentationMotionKind kind = PresentationMotionKind.FlipImpactStay,
            int correlationId = 100)
        {
            var sourcePose = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            var contactPose = new GameplayEntityPose(new Vector3(0.4f, 0.1f, 0f), Quaternion.identity);
            var phases = PresentationMotionPhaseSet.CreateFlipImpactStay(
                sourcePose,
                contactPose,
                contactNormalizedTime: 0.45f,
                postContactHoldNormalizedDuration: 0.2f,
                arcHeightWorld: 0.1f,
                returnArcMultiplier: 0.5f);
            var command = new PresentationMotionCommand(
                entityId,
                kind,
                new PresentationMotionInstanceKey(kind, correlationId, entityId, usesTickFallback: false),
                sourcePose,
                contactPose,
                sourcePose,
                durationSeconds: 0.5f,
                phases,
                PresentationMotionScalePolicy.None,
                PresentationMotionRotationPolicy.SamplePhase,
                PresentationMotionCompletionPolicy.ResetToCompletionPose,
                PresentationMotionInteractionPolicy.None,
                presentationSeed: correlationId,
                hasRequiredFinalCell: false,
                requiredFinalCell: default);
            return PresentationMotionTrack.Create(command);
        }

        private static void Destroy(params Object[] unityObjects)
        {
            for (var i = 0; i < unityObjects.Length; i++)
            {
                if (unityObjects[i] != null)
                {
                    Object.DestroyImmediate(unityObjects[i]);
                }
            }
        }

        private readonly struct MotionFollowerFixture
        {
            public MotionFollowerFixture(
                GameObject owner,
                GameObject prefab,
                GameplayVfxRuntimeRoot root,
                GameplayVfxGameObjectPool pool,
                GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider timeProvider,
                GameplayPresentationStateStore stateStore,
                GameplayEntityView view,
                GameplayPresentationTrackState trackState,
                PresentationMotionFollowingVfxController controller,
                IVfxBindingResolver bindingResolver)
            {
                Owner = owner;
                Prefab = prefab;
                Root = root;
                Pool = pool;
                TimeProvider = timeProvider;
                StateStore = stateStore;
                View = view;
                TrackState = trackState;
                Controller = controller;
                BindingResolver = bindingResolver;
            }

            public GameObject Owner { get; }

            public GameObject Prefab { get; }

            public GameplayVfxRuntimeRoot Root { get; }

            public GameplayVfxGameObjectPool Pool { get; }

            public GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider TimeProvider { get; }

            public GameplayPresentationStateStore StateStore { get; }

            public GameplayEntityView View { get; }

            public GameplayPresentationTrackState TrackState { get; }

            public PresentationMotionFollowingVfxController Controller { get; }

            public IVfxBindingResolver BindingResolver { get; }

            public void Destroy()
            {
                Pool?.HardCleanupAll();
                GameplayVfxMotionFollowingTests.Destroy(Prefab, Owner);
            }
        }

        private sealed class SinglePolicyResolver : IVfxBindingResolver
        {
            private readonly bool resolve;
            private readonly VfxBindingRuntimePolicy policy;

            public SinglePolicyResolver(bool resolve, VfxBindingRuntimePolicy policy)
            {
                this.resolve = resolve;
                this.policy = policy;
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                resolvedPolicy = policy;
                return resolve && request.CueId == policy.CueId;
            }
        }
    }
}
