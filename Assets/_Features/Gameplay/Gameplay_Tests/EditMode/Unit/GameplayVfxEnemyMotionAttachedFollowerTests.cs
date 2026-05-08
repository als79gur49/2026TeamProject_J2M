using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxEnemyMotionAttachedFollowerTests
    {
        private const string GlidePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/GlideWindTrailVfx.prefab";
        private const string ChargePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/ChargeBoosterTrailVfx.prefab";
        private const string GlideBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/GlideWindTrail_Binding.asset";
        private const string ChargeBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ChargeBoosterTrail_Binding.asset";
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string ProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string FollowerPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyMotionAttachedVfxFollowerPlanner.cs";

        [Test]
        [Category("Extended")]
        public void ChargeActive_AttachesBoosterTrail()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                CreatePresentationData(chargeSignals: new[] { CreateChargeSignal(EnemyChargePhase.Active) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true);

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
            Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail)));
        }

        [Test]
        [Category("Extended")]
        public void ChargeWindup_DoesNotAttachBoosterTrail()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                CreatePresentationData(chargeSignals: new[] { CreateChargeSignal(EnemyChargePhase.Windup) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true);

            Assert.That(planner.DesiredFollowers, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void ChargeRecover_DetachesBoosterTrail()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_AttachesWindTrail()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                CreatePresentationData(glideSignals: new[] { CreateGlideSignal(EnemyGlidePhase.Active) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true);

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
            Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail)));
        }

        [Test]
        [Category("Extended")]
        public void GlideWindupRecoveryOrLandingPending_DoesNotAttachWindTrail()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            var phases = new[]
            {
                EnemyGlidePhase.Windup,
                EnemyGlidePhase.LandingPending,
                EnemyGlidePhase.Recovery,
                EnemyGlidePhase.Cooldown,
            };

            for (var i = 0; i < phases.Length; i++)
            {
                planner.Build(
                    CreatePresentationData(glideSignals: new[] { CreateGlideSignal(phases[i]) }),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true);

                Assert.That(planner.DesiredFollowers, Is.Empty, phases[i].ToString());
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_DoesNotRespawnEveryFrame()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
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
        public void ChargeBoosterTrail_RemainsAttachedAcrossPoolAdvanceWhileActive()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                var instance = fixture.View.ModelRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 1f;
                fixture.Pool.Advance(1f);

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
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
        public void GlideWindTrail_RemainsAttachedAcrossPoolAdvanceWhileActive()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredGlide());
                var instance = fixture.View.ModelRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 1f;
                fixture.Pool.Advance(1f);

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
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
        public void Follower_DefaultLifetimeZero_DoesNotImmediateStopWhenControllerManaged()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                fixture.TimeProvider.TimeSeconds = 0.01f;
                fixture.Pool.Advance(0.01f);

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Root.TailRoot.childCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_DetachesAndTailsOnStateEnd()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
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
        public void Follower_ReleasesAfterTail()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached();

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
        public void Follower_HardCleanupReleasesAll()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                fixture.Controller.HardCleanup();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_MissingOwnerView_DiagnosticNoMotionSuppression()
        {
            var fixture = CreateFixture(registerView: false);
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.AttachedMissingOwnerViewCount, Is.EqualTo(1));
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
                fixture.RefreshAttached(DesiredCharge());

                fixture.StateStore.ViewsByEntityId.Remove(40);
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
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
        public void Follower_MissingBinding_DiagnosticNoMotionSuppression()
        {
            var fixture = CreateFixture(resolveBinding: false);
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.AttachedMissingBindingCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlagOff_DisablesTrailOnly()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge(), DesiredGlide());
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitedOrRemovedEntity_DoesNotAttachFollower()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                CreatePresentationData(
                    chargeSignals: new[] { CreateChargeSignal(EnemyChargePhase.Active) },
                    glideSignals: new[] { CreateGlideSignal(EnemyGlidePhase.Active) },
                    exitSignals: new[] { CreateExitSignal(40) },
                    visibilityChanges: new[] { CreateRemoveVisibilityChange(41) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true);

            Assert.That(planner.DesiredFollowers, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void ChargeMotion_NotOwnedByVfxRuntime()
        {
            var runtimeSource = File.ReadAllText(ProductionRuntimePath);
            var plannerSource = File.ReadAllText(FollowerPlannerPath);

            Assert.That(runtimeSource, Does.Not.Contain("ParameterizedMotionVfxCommandBuilder"));
            Assert.That(plannerSource, Does.Not.Contain("ParameterizedMotionVfxCommand"));
            Assert.That(plannerSource, Does.Not.Contain("WorldState"));
            Assert.That(plannerSource, Does.Not.Contain("WorldSnapshot"));
        }

        [Test]
        [Category("Extended")]
        public void GlideMotion_NotOwnedByVfxRuntime()
        {
            var plannerSource = File.ReadAllText(FollowerPlannerPath);

            Assert.That(plannerSource, Does.Contain("EnemyGlidePhase.Active"));
            Assert.That(plannerSource, Does.Not.Contain("MotionTrack"));
            Assert.That(plannerSource, Does.Not.Contain("ParameterizedMotionVfxCommand"));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_FlipImpactStayTrailRegression()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveMotionHandleCount, Is.Zero);
                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeFlags_DefaultTrue()
        {
            var owner = new GameObject("EnemyMotionAttachedFollowerFlags");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxGlideWindTrail, Is.True);
                Assert.That(runtime.EnableGameplayVfxChargeBoosterTrail, Is.True);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideWindTrailPrefab_PassesValidation()
        {
            AssertPrefabValid(GlidePrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void ChargeBoosterTrailPrefab_PassesValidation()
        {
            AssertPrefabValid(ChargePrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void GlideWindTrailBinding_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(GlideBindingPath);

            Assert.That(binding, Is.Not.Null, GlideBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail)));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.30f).Within(0.0001f));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ChargeBoosterTrailBinding_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(ChargeBindingPath);

            Assert.That(binding, Is.Not.Null, ChargeBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail)));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesBoth()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);
            var composition = GameplayVfxBindingComposition.Compose(cueMap, Array.Empty<VfxProfileAsset>());

            Assert.That(composition.Succeeded, Is.True, string.Join("\n", composition.Validation.Messages));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail));
        }

        private static TickPresentationData CreatePresentationData(
            IEnumerable<TickEnemyChargePresentationSignal> chargeSignals = null,
            IEnumerable<TickEnemyGlidePresentationSignal> glideSignals = null,
            IEnumerable<TickEntityExitPresentationSignal> exitSignals = null,
            IEnumerable<TickVisibilityChange> visibilityChanges = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                null,
                visibilityChanges ?? Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                chargeSignals ?? Array.Empty<TickEnemyChargePresentationSignal>(),
                exitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                enemyGlideSignals: glideSignals ?? Array.Empty<TickEnemyGlidePresentationSignal>());
        }

        private static TickEnemyChargePresentationSignal CreateChargeSignal(
            EnemyChargePhase phase,
            int entityId = 40,
            int sequence = 7)
        {
            return new TickEnemyChargePresentationSignal(
                entityId,
                sequence,
                phase,
                startedWindupThisTick: phase == EnemyChargePhase.Windup,
                startedActiveThisTick: phase == EnemyChargePhase.Active,
                startedRecoverThisTick: phase == EnemyChargePhase.Recover,
                lockedDirection: Direction.Right);
        }

        private static TickEnemyGlidePresentationSignal CreateGlideSignal(
            EnemyGlidePhase phase,
            int entityId = 41,
            int sequence = 9)
        {
            return new TickEnemyGlidePresentationSignal(
                entityId,
                default,
                phase,
                sequence,
                phaseElapsedTicks: 0,
                phaseTotalTicks: 3,
                normalizedPhaseProgress: 0f,
                liftHeightUnits: 1,
                recoveryDipHeightUnits: 0,
                currentHeightUnits: phase == EnemyGlidePhase.Active ? 1 : 0,
                isAirborneVisual: phase == EnemyGlidePhase.Active,
                isLandingPending: phase == EnemyGlidePhase.LandingPending,
                isTerminalZero: false);
        }

        private static TickEntityExitPresentationSignal CreateExitSignal(int entityId)
        {
            return new TickEntityExitPresentationSignal(
                entityId,
                TickEntityExitCause.Killed,
                default,
                default,
                Direction.Up,
                EntityType.Unit);
        }

        private static TickVisibilityChange CreateRemoveVisibilityChange(int entityId)
        {
            return new TickVisibilityChange(
                entityId,
                TickVisibilityChangeKind.Remove,
                default,
                default,
                Direction.Up);
        }

        private static AttachedVfxFollowerDesiredState DesiredCharge()
        {
            return new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail),
                40,
                AttachedVfxFollowerStateKind.EnemyChargeActive,
                7,
                Vector3.zero,
                Quaternion.identity);
        }

        private static AttachedVfxFollowerDesiredState DesiredGlide()
        {
            return new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail),
                40,
                AttachedVfxFollowerStateKind.EnemyGlideActive,
                9,
                Vector3.zero,
                Quaternion.identity);
        }

        private static AttachedFollowerFixture CreateFixture(
            bool resolveBinding = true,
            bool registerView = true,
            float tailSeconds = 0.25f)
        {
            var owner = new GameObject("EnemyMotionAttachedFollowerFixture");
            var root = GameplayVfxRuntimeRoot.CreateUnder(owner.transform);
            var prefab = GameplayVfxParameterizedMotionRuntimeTests.CreateRuntimePrefab("EnemyMotionAttachedFollowerRuntimePrefab");
            var timeProvider = new GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider();
            var pool = new GameplayVfxGameObjectPool(
                root,
                new GameplayVfxParameterizedMotionRuntimeTests.SinglePrefabProvider(prefab),
                timeProvider);
            var stateStore = new GameplayPresentationStateStore();
            var viewObject = new GameObject("EnemyView");
            viewObject.transform.SetParent(owner.transform, worldPositionStays: false);
            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(40);
            view.EnsureModelRoot();
            if (registerView)
            {
                stateStore.ViewsByEntityId[40] = view;
            }

            var policies = new[]
            {
                CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail), tailSeconds),
                CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail), tailSeconds),
            };
            return new AttachedFollowerFixture(
                owner,
                prefab,
                root,
                pool,
                timeProvider,
                stateStore,
                view,
                new GameplayPresentationTrackState(),
                new PresentationMotionFollowingVfxController(),
                new MultiPolicyResolver(resolveBinding, policies));
        }

        private static VfxBindingRuntimePolicy CreatePolicy(GameplayVfxCueId cueId, float tailSeconds)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic,
                VfxPlaybackMode.Follow,
                VfxStopPolicy.DetachThenStopEmittingThenRelease,
                defaultLifetimeSeconds: 0f,
                tailSeconds: tailSeconds,
                maxConcurrentInstances: 8);
        }

        private static void AssertPrefabValid(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true), Is.Empty);
        }

        private static void AssertResolves(IVfxBindingResolver resolver, GameplayVfxCueId cueId)
        {
            var request = new GameplayVfxRequest(
                1,
                7,
                7,
                sourceEntityId: 40,
                cueId,
                VfxAnchor.ForEntity(40),
                VfxTimingKind.DuringMotion);

            Assert.That(resolver.TryResolve(request, out var policy), Is.True);
            Assert.That(policy.CueId, Is.EqualTo(cueId));
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

        private readonly struct AttachedFollowerFixture
        {
            public AttachedFollowerFixture(
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

            public void RefreshAttached(params AttachedVfxFollowerDesiredState[] desiredStates)
            {
                Controller.Refresh(
                    10,
                    TrackState,
                    StateStore,
                    Pool,
                    BindingResolver,
                    enabled: false,
                    attachedDesiredStates: desiredStates,
                    attachedFollowersEnabled: true);
            }

            public void Destroy()
            {
                Pool?.HardCleanupAll();
                GameplayVfxEnemyMotionAttachedFollowerTests.Destroy(Prefab, Owner);
            }
        }

        private sealed class MultiPolicyResolver : IVfxBindingResolver
        {
            private readonly Dictionary<GameplayVfxCueId, VfxBindingRuntimePolicy> policiesByCueId = new();
            private readonly bool resolve;

            public MultiPolicyResolver(bool resolve, IEnumerable<VfxBindingRuntimePolicy> policies)
            {
                this.resolve = resolve;
                foreach (var policy in policies)
                {
                    policiesByCueId[policy.CueId] = policy;
                }
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                if (resolve && policiesByCueId.TryGetValue(request.CueId, out resolvedPolicy))
                {
                    return true;
                }

                resolvedPolicy = default;
                return false;
            }
        }
    }
}
