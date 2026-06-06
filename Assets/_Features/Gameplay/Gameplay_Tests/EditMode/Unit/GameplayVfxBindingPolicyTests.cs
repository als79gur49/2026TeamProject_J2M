using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    [Category("Core")]
    [Category("Phase3BGate")]
    public sealed class GameplayVfxBindingPolicyTests
    {
        [Test]
        [Category("Extended")]
        public void BindingPolicy_OwnsExecutionPolicy()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.PushWindup);
            var policy = new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Required,
                VfxMissingAnchorPolicy.FailFast,
                VfxPlaybackMode.Follow,
                VfxStopPolicy.DetachThenStopEmittingThenRelease,
                defaultLifetimeSeconds: 1.25f,
                tailSeconds: 0.5f,
                maxConcurrentInstances: 3,
                visibilityMode: GameplayVfxVisibilityMode.VisibleSurfaceAllowed);

            Assert.That(policy.CueId, Is.EqualTo(cueId));
            Assert.That(policy.Requirement, Is.EqualTo(VfxBindingRequirement.Required));
            Assert.That(policy.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.FailFast));
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(policy.DefaultLifetimeSeconds, Is.EqualTo(1.25f));
            Assert.That(policy.TailSeconds, Is.EqualTo(0.5f));
            Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(3));
            Assert.That(policy.VisibilityMode, Is.EqualTo(GameplayVfxVisibilityMode.VisibleSurfaceAllowed));
            Assert.DoesNotThrow(() => new VfxBinding(policy));
        }

        [Test]
        [Category("Extended")]
        public void BindingPolicy_DefaultVisibilityMode_IsDefaultGameplay()
        {
            var policy = CreatePolicy();

            Assert.That(policy.VisibilityMode, Is.EqualTo(GameplayVfxVisibilityMode.DefaultGameplay));
        }

        [Test]
        [Category("Core")]
        public void ResolvedVisibilityPolicy_DistinguishesAuthoredDefaultFromFallbackDefault()
        {
            var request = CreateActiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));
            var authoredDefault = GameplayVfxVisibilityPolicy.ResolveFinalPolicy(
                request,
                hasBindingPolicy: true,
                policy: CreatePolicy(request.CueId));
            var fallbackDefault = GameplayVfxVisibilityPolicy.ResolveFinalPolicy(
                request,
                hasBindingPolicy: false,
                policy: default);

            Assert.That(authoredDefault.VisibilityMode, Is.EqualTo(GameplayVfxVisibilityMode.DefaultGameplay));
            Assert.That(fallbackDefault.VisibilityMode, Is.EqualTo(GameplayVfxVisibilityMode.DefaultGameplay));
            Assert.That(authoredDefault.EffectiveMode, Is.EqualTo(GameplayVfxVisibilityMode.ActiveGameplayFaceOnly));
            Assert.That(fallbackDefault.EffectiveMode, Is.EqualTo(GameplayVfxVisibilityMode.ActiveGameplayFaceOnly));
            Assert.That(authoredDefault.Source, Is.EqualTo(GameplayVfxVisibilityPolicySource.BindingRuntimePolicy));
            Assert.That(fallbackDefault.Source, Is.EqualTo(GameplayVfxVisibilityPolicySource.FallbackDefaultGameplay));
            Assert.That(authoredDefault.IsFallbackDefaultGameplay, Is.False);
            Assert.That(fallbackDefault.IsFallbackDefaultGameplay, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TopologyMotionVisualHelper_NotSuppressedByGameplayInactiveGate()
        {
            var request = new GameplayVfxRequest(
                1,
                1,
                1,
                GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail),
                VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Front, 0, 0),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter),
                VfxTimingKind.QueuedUntilTopologyTransitionEnd);

            var decision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                GameplayVfxVisibilityMode.PresentationOnly,
                default);

            Assert.That(decision.IsVisible, Is.True);
            Assert.That(decision.BlockReason, Is.EqualTo(GameplayVfxVisibilityBlockReason.None));
        }

        [Test]
        [Category("Core")]
        public void PlannerVisibility_UsesBindingVisibilityMode_NotDefaultGameplay()
        {
            var request = CreateInactiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));
            var plan = FilterByPlanningVisibility(
                new GameplayVfxRequestPlan(new[] { request }),
                new SinglePolicyResolver(CreatePolicy(
                    request.CueId,
                    visibilityMode: GameplayVfxVisibilityMode.InactiveFaceExplicitlyAllowed)));

            Assert.That(plan.Requests.Select(item => item.CueId), Is.EqualTo(new[] { request.CueId }));
        }

        [Test]
        [Category("Core")]
        public void PlannerVisibility_InactiveFaceExplicitCue_NotSuppressedByDefaultGameplay()
        {
            var request = CreateInactiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));

            var plan = FilterByPlanningVisibility(
                new GameplayVfxRequestPlan(new[] { request }),
                new SinglePolicyResolver(CreatePolicy(
                    request.CueId,
                    visibilityMode: GameplayVfxVisibilityMode.InactiveFaceExplicitlyAllowed)));

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void PlannerVisibility_VisibleSurfaceAllowedCue_NotSuppressedByDefaultGameplay()
        {
            var request = CreateInactiveFaceCellRequest(GameplayVfxCueId.From(TerrainVfxCue.TerrainChanged));

            var plan = FilterByPlanningVisibility(
                new GameplayVfxRequestPlan(new[] { request }),
                new SinglePolicyResolver(CreatePolicy(
                    request.CueId,
                    visibilityMode: GameplayVfxVisibilityMode.VisibleSurfaceAllowed)));

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void PlannerVisibility_PresentationOnlyCue_NotSuppressedByDefaultGameplay()
        {
            var request = CreateInactiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail));

            var plan = FilterByPlanningVisibility(
                new GameplayVfxRequestPlan(new[] { request }),
                new SinglePolicyResolver(CreatePolicy(
                    request.CueId,
                    visibilityMode: GameplayVfxVisibilityMode.PresentationOnly)));

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void PlannerVisibility_DefaultGameplayCellCue_SuppressedOnInactiveFace()
        {
            var request = CreateInactiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));

            var plan = FilterByPlanningVisibility(
                new GameplayVfxRequestPlan(new[] { request }),
                new SinglePolicyResolver(CreatePolicy(request.CueId)));

            Assert.That(plan.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxVisibilityPolicy_DoesNotMaskTopologyTransitionLifecycleMismatch()
        {
            var cueId = GameplayVfxCueId.From(TileFeatureVfxCue.BarricadeActiveLoop);
            var request = new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId: 1,
                presentationSeed: 1,
                cueId,
                VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor)),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    tileId: 10,
                    cell: new SurfaceCell(FaceId.Floor, 1, 1),
                    hasCell: true),
                topologyStopMode: GameplayVfxTopologyStopMode.HardClearAtTransitionStart,
                topologySpawnMode: GameplayVfxTopologySpawnMode.SuppressDuringTransition);

            var decision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                GameplayVfxVisibilityMode.ActiveGameplayFaceOnly,
                default);

            Assert.That(decision.IsVisible, Is.True);
            Assert.That(request.TopologyStopMode, Is.EqualTo(GameplayVfxTopologyStopMode.HardClearAtTransitionStart));
            Assert.That(request.TopologySpawnMode, Is.EqualTo(GameplayVfxTopologySpawnMode.SuppressDuringTransition));
        }

        [Test]
        [Category("Core")]
        public void PlannerVisibility_MissingBinding_UsesDocumentedFallback()
        {
            var request = CreateInactiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));

            var plan = FilterByPlanningVisibility(
                new GameplayVfxRequestPlan(new[] { request }),
                new MissingPolicyResolver());

            Assert.That(plan.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlannerVisibility_DiagnosticsDistinguishBindingDefaultFromFallbackDefault()
        {
            var request = CreateActiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));

            var bindingPlan = FilterByPlanningVisibility(
                new GameplayVfxRequestPlan(new[] { request }),
                new SinglePolicyResolver(CreatePolicy(request.CueId)),
                out var bindingPolicies);
            var fallbackPlan = FilterByPlanningVisibility(
                new GameplayVfxRequestPlan(new[] { request }),
                new MissingPolicyResolver(),
                out var fallbackPolicies);

            Assert.That(bindingPlan.Requests, Has.Count.EqualTo(1));
            Assert.That(fallbackPlan.Requests, Has.Count.EqualTo(1));
            Assert.That(bindingPolicies, Has.Count.EqualTo(1));
            Assert.That(fallbackPolicies, Has.Count.EqualTo(1));
            Assert.That(bindingPolicies[0].VisibilityMode, Is.EqualTo(GameplayVfxVisibilityMode.DefaultGameplay));
            Assert.That(fallbackPolicies[0].VisibilityMode, Is.EqualTo(GameplayVfxVisibilityMode.DefaultGameplay));
            Assert.That(bindingPolicies[0].Source, Is.EqualTo(GameplayVfxVisibilityPolicySource.BindingRuntimePolicy));
            Assert.That(fallbackPolicies[0].Source, Is.EqualTo(GameplayVfxVisibilityPolicySource.FallbackDefaultGameplay));
        }

        [Test]
        [Category("Core")]
        public void VisibleSurfaceAllowed_AllowsVisibleSurfaceProjection()
        {
            var decision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                CreateInactiveFaceCellRequest(GameplayVfxCueId.From(TerrainVfxCue.TerrainChanged)),
                GameplayVfxVisibilityMode.VisibleSurfaceAllowed,
                default);

            Assert.That(decision.IsVisible, Is.True);
            Assert.That(decision.AllowReason, Is.EqualTo(GameplayVfxVisibilityAllowReason.VisibleSurfaceProjectionOptIn));
        }

        [Test]
        [Category("Core")]
        public void InactiveFaceExplicitlyAllowed_AllowsInactiveFaceCue()
        {
            var decision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                CreateInactiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke)),
                GameplayVfxVisibilityMode.InactiveFaceExplicitlyAllowed,
                default);

            Assert.That(decision.IsVisible, Is.True);
            Assert.That(decision.AllowReason, Is.EqualTo(GameplayVfxVisibilityAllowReason.InactiveFaceExplicitOptIn));
        }

        [Test]
        [Category("Core")]
        public void InactiveFaceExplicitlyAllowed_DoesNotActAsGenericPresentationOnlyBypass()
        {
            var request = new GameplayVfxRequest(
                1,
                1,
                1,
                sourceEntityId: 40,
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                VfxAnchor.ForEntity(40),
                VfxTimingKind.ImmediateOnTickPresentation);
            var context = new GameplayVfxVisibilityContext(
                new System.Collections.Generic.Dictionary<int, GameplayVfxEntityVisibilityState>
                {
                    {
                        40,
                        new GameplayVfxEntityVisibilityState(
                            hasView: true,
                            isViewActiveInHierarchy: true,
                            hasSemanticState: true,
                            isFrontFaceInactive: true)
                    },
                });

            var decision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                GameplayVfxVisibilityMode.InactiveFaceExplicitlyAllowed,
                context);

            Assert.That(decision.IsVisible, Is.False);
            Assert.That(decision.BlockReason, Is.EqualTo(GameplayVfxVisibilityBlockReason.FrontFaceInactive));
        }

        [Test]
        [Category("Core")]
        public void VisibilityDecision_ReasonDistinguishesVisibleSurfaceFromInactiveFaceOptIn()
        {
            var request = CreateInactiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));

            var visibleSurface = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                GameplayVfxVisibilityMode.VisibleSurfaceAllowed,
                default);
            var inactiveFace = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                GameplayVfxVisibilityMode.InactiveFaceExplicitlyAllowed,
                default);

            Assert.That(visibleSurface.AllowReason, Is.EqualTo(GameplayVfxVisibilityAllowReason.VisibleSurfaceProjectionOptIn));
            Assert.That(inactiveFace.AllowReason, Is.EqualTo(GameplayVfxVisibilityAllowReason.InactiveFaceExplicitOptIn));
            Assert.That(visibleSurface.AllowReason, Is.Not.EqualTo(inactiveFace.AllowReason));
        }

        [Test]
        [Category("Core")]
        public void PresentationOnlyBinding_GameplayCue_ValidationFailsOrWarns()
        {
            var binding = CreateBindingAsset(
                "PlayerDamage_Binding",
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                GameplayVfxVisibilityMode.PresentationOnly);
            try
            {
                var result = binding.ValidateAuthoring();

                Assert.That(result.Messages.Any(message =>
                    message.Code == "VFX_BINDING_PRESENTATION_ONLY_REQUIRES_ALLOWLIST" &&
                    message.Severity == VfxAuthoringValidationSeverity.Error), Is.True);
            }
            finally
            {
                DestroyBindingAsset(binding);
            }
        }

        [Test]
        [Category("Core")]
        public void PresentationOnlyBinding_TopologyVisualHelper_Allowed()
        {
            var binding = CreateBindingAsset(
                "TopologyMotionVisualHelper_Binding",
                GameplayVfxFamily.Box,
                (int)BoxVfxCue.FlipImpactStayTrail,
                GameplayVfxVisibilityMode.PresentationOnly);
            try
            {
                var result = binding.ValidateAuthoring();

                Assert.That(result.Messages.Any(message =>
                    message.Code == "VFX_BINDING_PRESENTATION_ONLY_REQUIRES_ALLOWLIST"), Is.False);
            }
            finally
            {
                DestroyBindingAsset(binding);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyMotionVisualHelper_NotSuppressedByGameplayInactiveGate_RealBindingPath()
        {
            var binding = CreateBindingAsset(
                "TopologyMotionVisualHelper_Binding",
                GameplayVfxFamily.Box,
                (int)BoxVfxCue.FlipImpactStayTrail,
                GameplayVfxVisibilityMode.PresentationOnly);
            try
            {
                var request = CreateInactiveFaceCellRequest(GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail));
                var decision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                    request,
                    binding.BuildRuntimePolicy(),
                    default);

                Assert.That(decision.IsVisible, Is.True);
                Assert.That(decision.AllowReason, Is.EqualTo(GameplayVfxVisibilityAllowReason.PresentationOnly));
            }
            finally
            {
                DestroyBindingAsset(binding);
            }
        }

        [Test]
        [Category("Core")]
        public void VfxBindingDiagnostics_IncludesVisibilityMode()
        {
            var binding = CreateBindingAsset(
                "VisibleSurfaceBinding",
                GameplayVfxFamily.Terrain,
                (int)TerrainVfxCue.TerrainChanged,
                GameplayVfxVisibilityMode.VisibleSurfaceAllowed);
            try
            {
                var result = binding.ValidateAuthoring();

                Assert.That(result.Messages.Any(message =>
                    message.Code == "VFX_BINDING_VISIBILITY_MODE" &&
                    message.Message.Contains(nameof(GameplayVfxVisibilityMode.VisibleSurfaceAllowed))), Is.True);
            }
            finally
            {
                DestroyBindingAsset(binding);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyHelperExempt_CannotBeImplicitPresentationOnlyBypass()
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail);

            Assert.That(GameplayVfxTopologyHelperExemptionPolicy.AllowsStopExemption(
                cueId,
                GameplayVfxTopologyStopMode.Default), Is.False);
            Assert.That(GameplayVfxTopologyHelperExemptionPolicy.AllowsSpawnExemption(
                cueId,
                GameplayVfxTopologySpawnMode.Default), Is.False);
        }

        [Test]
        [Category("Core")]
        public void TopologyHelperExempt_GameplayCue_ValidationFails()
        {
            var binding = CreateBindingAsset(
                "PresentationOnly_PlayerDamage_Binding",
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                GameplayVfxVisibilityMode.PresentationOnly,
                allowTopologyHelperExempt: true);
            try
            {
                var result = binding.ValidateAuthoring();

                Assert.That(result.Messages.Any(message =>
                    message.Code == "VFX_BINDING_TOPOLOGY_HELPER_EXEMPT_REQUIRES_HELPER_CUE" &&
                    message.Severity == VfxAuthoringValidationSeverity.Error), Is.True);
            }
            finally
            {
                DestroyBindingAsset(binding);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyHelperExempt_ExplicitTopologyHelperCue_Allowed()
        {
            var binding = CreateBindingAsset(
                "TopologyMotionVisualHelper_Binding",
                GameplayVfxFamily.Box,
                (int)BoxVfxCue.FlipImpactStayTrail,
                GameplayVfxVisibilityMode.PresentationOnly,
                allowTopologyHelperExempt: true);
            try
            {
                var result = binding.ValidateAuthoring();

                Assert.That(result.Messages.Any(message =>
                    message.Code == "VFX_BINDING_TOPOLOGY_HELPER_EXEMPT_REQUIRES_HELPER_CUE"), Is.False);
                Assert.That(GameplayVfxTopologyHelperExemptionPolicy.AllowsStopExemption(
                    binding.CueId,
                    GameplayVfxTopologyStopMode.TopologyHelperExempt), Is.True);
                Assert.That(GameplayVfxTopologyHelperExemptionPolicy.AllowsSpawnExemption(
                    binding.CueId,
                    GameplayVfxTopologySpawnMode.TopologyHelperExempt), Is.True);
            }
            finally
            {
                DestroyBindingAsset(binding);
            }
        }

        [Test]
        [Category("Extended")]
        public void BindingPolicy_RejectsInvalidDurationsAndCounts()
        {
            AssertInvalid(CreatePolicy(defaultLifetimeSeconds: -0.1f));
            AssertInvalid(CreatePolicy(defaultLifetimeSeconds: float.NaN));
            AssertInvalid(CreatePolicy(defaultLifetimeSeconds: float.PositiveInfinity));
            AssertInvalid(CreatePolicy(tailSeconds: -0.1f));
            AssertInvalid(CreatePolicy(tailSeconds: float.NegativeInfinity));
            AssertInvalid(CreatePolicy(maxConcurrentInstances: -1));
            AssertInvalid(new VfxBindingRuntimePolicy(
                GameplayVfxCueId.None,
                VfxBindingRequirement.Optional,
                VfxMissingAnchorPolicy.SkipOptional,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration));
        }

        [Test]
        [Category("Extended")]
        public void BindingPolicy_AllowsZeroMaxConcurrentAsUnlimited()
        {
            var policy = CreatePolicy(maxConcurrentInstances: 0);

            Assert.That(policy.IsValid, Is.True);
            Assert.DoesNotThrow(() => policy.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void CueMap_RejectsDuplicateOrInvalidCue()
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.DestroySmoke);
            var policy = CreatePolicy(cueId: cueId);

            Assert.Throws<InvalidOperationException>(() => new VfxCueMap(new[] { policy, policy }));
            Assert.Throws<InvalidOperationException>(() => new VfxCueMap(new[]
            {
                new VfxBindingRuntimePolicy(
                    GameplayVfxCueId.None,
                    VfxBindingRequirement.Optional,
                    VfxMissingAnchorPolicy.SkipOptional,
                    VfxPlaybackMode.OneShot,
                    VfxStopPolicy.AuthoredDuration),
            }));

            var map = new VfxCueMap(new[] { policy });
            Assert.That(map.TryResolve(cueId, out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(policy));
            Assert.Throws<InvalidOperationException>(() => map.ResolveOrThrow(GameplayVfxCueId.From(BoxVfxCue.ItemConsume)));
        }

        [Test]
        [Category("Extended")]
        public void Profile_RejectsCrossFamilyCue()
        {
            var boxPolicy = CreatePolicy(cueId: GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));

            Assert.Throws<InvalidOperationException>(() => new VfxProfile(GameplayVfxFamily.Player, new[] { boxPolicy }));
        }

        [Test]
        [Category("Extended")]
        public void ResolvedCommand_CarriesRequestPolicyAndAnchor()
        {
            var request = new GameplayVfxRequest(
                1,
                2,
                3,
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                VfxAnchor.ForEntity(9),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(
                    GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                    VfxAnchorKind.Entity,
                    entityId: 9));
            var policy = CreatePolicy(
                cueId: request.CueId,
                playbackMode: VfxPlaybackMode.Loop,
                stopPolicy: VfxStopPolicy.StopEmittingThenRelease);
            var anchor = VfxResolvedAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 1, 1),
                new CubeTopologyState(FaceId.Floor),
                VfxAnchorSlot.CellCenter);

            var command = new ResolvedVfxPlaybackCommand(request, policy, anchor);

            Assert.That(command.Request, Is.EqualTo(request));
            Assert.That(command.Policy, Is.EqualTo(policy));
            Assert.That(command.Anchor, Is.EqualTo(anchor));
            Assert.That(command.CueId, Is.EqualTo(request.CueId));
            Assert.That(command.IsPersistent, Is.True);
            Assert.That(command.PersistentKey, Is.EqualTo(request.PersistentKey));
        }

        private static VfxBindingRuntimePolicy CreatePolicy(
            GameplayVfxCueId cueId = default,
            VfxPlaybackMode playbackMode = VfxPlaybackMode.OneShot,
            VfxStopPolicy stopPolicy = VfxStopPolicy.AuthoredDuration,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int maxConcurrentInstances = 0,
            GameplayVfxVisibilityMode visibilityMode = GameplayVfxVisibilityMode.DefaultGameplay)
        {
            return new VfxBindingRuntimePolicy(
                cueId.IsNone ? GameplayVfxCueId.From(PlayerVfxCue.Damage) : cueId,
                VfxBindingRequirement.Optional,
                VfxMissingAnchorPolicy.SkipOptional,
                playbackMode,
                stopPolicy,
                defaultLifetimeSeconds,
                tailSeconds,
                maxConcurrentInstances,
                visibilityMode: visibilityMode);
        }

        private static GameplayVfxRequest CreateInactiveFaceCellRequest(GameplayVfxCueId cueId)
        {
            return new GameplayVfxRequest(
                1,
                1,
                1,
                cueId,
                VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Back, 0, 0),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter),
                VfxTimingKind.ImmediateOnTickPresentation);
        }

        private static GameplayVfxRequest CreateActiveFaceCellRequest(GameplayVfxCueId cueId)
        {
            return new GameplayVfxRequest(
                1,
                1,
                1,
                cueId,
                VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter),
                VfxTimingKind.ImmediateOnTickPresentation);
        }

        private static GameplayVfxRequestPlan FilterByPlanningVisibility(
            GameplayVfxRequestPlan plan,
            IVfxBindingResolver resolver)
        {
            var method = typeof(GameplayVfxProductionRuntime)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(candidate =>
                    candidate.Name == "FilterByPlanningVisibility" &&
                    candidate.GetParameters().Length == 3);
            Assert.That(method, Is.Not.Null);
            return (GameplayVfxRequestPlan)method.Invoke(
                null,
                new object[]
                {
                    plan,
                    resolver,
                    default(GameplayVfxVisibilityContext),
                });
        }

        private static GameplayVfxRequestPlan FilterByPlanningVisibility(
            GameplayVfxRequestPlan plan,
            IVfxBindingResolver resolver,
            out List<GameplayVfxResolvedVisibilityPolicy> resolvedPolicies)
        {
            resolvedPolicies = new List<GameplayVfxResolvedVisibilityPolicy>();
            var capturedPolicies = resolvedPolicies;
            var method = typeof(GameplayVfxProductionRuntime)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(candidate =>
                    candidate.Name == "FilterByPlanningVisibility" &&
                    candidate.GetParameters().Length == 4);
            Assert.That(method, Is.Not.Null);
            return (GameplayVfxRequestPlan)method.Invoke(
                null,
                new object[]
                {
                    plan,
                    resolver,
                    default(GameplayVfxVisibilityContext),
                    new Action<GameplayVfxResolvedVisibilityPolicy>(capturedPolicies.Add),
                });
        }

        private static void AssertInvalid(VfxBindingRuntimePolicy policy)
        {
            Assert.That(policy.IsValid, Is.False);
            Assert.Throws<InvalidOperationException>(() => policy.ValidateOrThrow());
        }

        private static VfxBindingDefinitionAsset CreateBindingAsset(
            string assetName,
            GameplayVfxFamily family,
            int cueCode,
            GameplayVfxVisibilityMode visibilityMode,
            bool allowTopologyHelperExempt = false)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            binding.name = assetName;
            SetPrivateField(binding, "family", family);
            SetPrivateField(binding, "cueCode", cueCode);
            SetPrivateField(binding, "requirement", VfxBindingRequirement.Optional);
            SetPrivateField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.SkipOptional);
            SetPrivateField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetPrivateField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetPrivateField(binding, "visibilityMode", visibilityMode);
            SetPrivateField(binding, "allowTopologyHelperExempt", allowTopologyHelperExempt);
            var prefab = new GameObject($"{assetName}_Prefab");
            new GameObject("ModelRoot").transform.SetParent(prefab.transform, worldPositionStays: false);
            SetPrivateField(binding, "prefab", prefab);
            return binding;
        }

        private static void DestroyBindingAsset(VfxBindingDefinitionAsset binding)
        {
            if (binding == null)
            {
                return;
            }

            var prefab = binding.Prefab;
            if (prefab != null)
            {
                Object.DestroyImmediate(prefab);
            }

            Object.DestroyImmediate(binding);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class SinglePolicyResolver : IVfxBindingResolver
        {
            private readonly VfxBindingRuntimePolicy policy;

            public SinglePolicyResolver(VfxBindingRuntimePolicy policy)
            {
                this.policy = policy;
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                resolvedPolicy = policy;
                return true;
            }
        }

        private sealed class MissingPolicyResolver : IVfxBindingResolver
        {
            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy policy)
            {
                policy = default;
                return false;
            }
        }
    }
}
