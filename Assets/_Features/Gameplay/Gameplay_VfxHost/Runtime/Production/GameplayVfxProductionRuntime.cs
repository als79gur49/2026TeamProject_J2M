using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx.Authoring;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxProductionRuntime : MonoBehaviour, IGameplayTickPresentationExtension, IGameplayOutputCameraPresentationExtension, IGameplayPresentationMotionVfxExtension
    {
        [SerializeField] private bool enableEnemyJumpTargetVfx = true;
        [SerializeField] private bool enableEnemyJumpLandingDustVfx = true;
        [SerializeField] private bool enableGameplayVfxDamageBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxEnemyDamageBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxEnemyDeathBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxEnemyDeathMotionMigration = true;
        [SerializeField] private bool enableGameplayVfxBoxDestroySmokeMigration = true;
        [SerializeField] private bool enableGameplayVfxBoxDestroyShrinkMigration = true;
        [SerializeField] private bool enableGameplayVfxItemConsumeBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxFlipImpactBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxFlipDestroySelfMotionMigration = true;
        [SerializeField] private bool enableGameplayVfxFlipImpactStayTrail = true;
        [SerializeField] private bool enableGameplayVfxGlideWindTrail = true;
        [SerializeField] private bool enableGameplayVfxChargeBoosterTrail = true;
        [SerializeField] private bool enableGameplayVfxBoxSlideTrail = true;
        [SerializeField] private bool enableGameplayVfxImpactTransientBreakMigration = true;
        [SerializeField] private bool enableGameplayVfxOutOfBoundsExitMigration = true;
        [SerializeField] private bool enableGameplayVfxUtilityWindupMigration = true;
        [SerializeField] private bool enableGameplayVfxFrontFaceShieldActiveMigration = true;
        [SerializeField] private bool enableGameplayVfxFrontFaceShieldBlockMigration = true;
        [SerializeField] private bool enableGameplayVfxFrontFaceShieldWindupMigration = true;
        [SerializeField] private bool enableGameplayVfxTileFeatureLane = true;
        [SerializeField] private bool enableGameplayVfxGravityFieldEvents = true;
        [SerializeField] private bool enableGameplayVfxGravityFieldContinuous = true;
        [SerializeField] private bool enableGameplayVfxGravityFieldLockedTarget = true;
        [SerializeField] private VfxProfileAsset[] familyProfiles = Array.Empty<VfxProfileAsset>();

        private readonly PlayerVfxRequestPlanner playerPlanner = new();
        private readonly BoxVfxRequestPlanner boxPlanner = new();
        private readonly FlipImpactBurstVfxRequestPlanner flipImpactBurstPlanner = new();
        private readonly EnemyVfxRequestPlanner enemyPlanner = new();
        private readonly TileFeatureVfxRequestPlanner tileFeaturePlanner = new();
        private readonly GravityFieldVfxRequestPlanner gravityFieldPlanner = new();
        private readonly GameplayVfxRequestPlanBuilder planBuilder = new();
        private readonly HashSet<FlipDestroySelfMotionInstanceKey> playedFlipDestroySelfMotionKeys = new();
        private readonly HashSet<BoxSlideTrailMotionInstanceKey> playedBoxSlideTrailMotionKeys = new();
        private readonly HashSet<ImpactTransientBreakInstanceKey> playedImpactTransientBreakKeys = new();
        private readonly HashSet<OutOfBoundsExitInstanceKey> playedOutOfBoundsExitKeys = new();
        private readonly HashSet<DelayedBoxDestroyExitVfxKey> scheduledDelayedBoxDestroyExitVfxKeys = new();
        private readonly List<DelayedBoxDestroyExitVfx> pendingDelayedBoxDestroyExitVfx = new();
        private readonly List<DelayedBoxDestroyExitVfx> readyDelayedBoxDestroyExitVfx = new();
        private readonly EnemyMotionAttachedVfxFollowerPlanner enemyMotionAttachedFollowerPlanner = new();
        private readonly PresentationMotionFollowingVfxController motionFollowingVfxController = new();

        private AuthoringPrefabProvider prefabProvider;
        private GameplayVfxGameObjectPool pool;
        private GameplayVfxPresentationController controller;
        private GameplayVfxRuntimeRoot runtimeRoot;
        private IVfxBindingResolver bindingResolver;
        private GameplayCubeProjector configuredProjector;
        private GameplayPresentationStateStore configuredStateStore;
        private EnemyPresentationCatalog configuredEnemyPresentationCatalog;
        private EnemyPresentationBinding[] configuredEnemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        private EnemyPresentationVfxProfileProvider enemyPresentationVfxProfileProvider;
        private VfxCueMapAsset hostDefaultCueMap;
        private int flipDestroySelfMotionMissingBindingCount;
        private int flipImpactStayTrailMissingBindingCount;
        private int flipImpactStayTrailMissingOwnerViewCount;
        private int enemyMotionAttachedMissingBindingCount;
        private int enemyMotionAttachedMissingOwnerViewCount;
        private int boxSlideTrailMissingBindingCount;
        private int boxDestroyShrinkMissingBindingCount;
        private int boxDestroyShrinkMissingAnchorCount;
        private int impactTransientBreakMissingBindingCount;
        private int impactTransientBreakMissingAnchorCount;
        private int outOfBoundsExitMissingBindingCount;
        private int outOfBoundsExitMissingAnchorCount;
        private int enemyDeathMotionMissingBindingCount;
        private int enemyDeathMotionMissingAnchorCount;
        private Camera outputCamera;
        private Transform localSpaceRoot;

        public bool EnableEnemyJumpTargetVfx
        {
            get => enableEnemyJumpTargetVfx;
            set
            {
                if (enableEnemyJumpTargetVfx == value)
                {
                    return;
                }

                enableEnemyJumpTargetVfx = value;
                ResetIfNoEnemyJumpVfxEnabled();
            }
        }

        public bool EnableEnemyJumpLandingDustVfx
        {
            get => enableEnemyJumpLandingDustVfx;
            set
            {
                if (enableEnemyJumpLandingDustVfx == value)
                {
                    return;
                }

                enableEnemyJumpLandingDustVfx = value;
                ResetIfNoEnemyJumpVfxEnabled();
            }
        }

        public bool EnableGameplayVfxDamageBurstMigration
        {
            get => enableGameplayVfxDamageBurstMigration;
            set
            {
                if (enableGameplayVfxDamageBurstMigration == value)
                {
                    return;
                }

                enableGameplayVfxDamageBurstMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxEnemyDamageBurstMigration
        {
            get => enableGameplayVfxEnemyDamageBurstMigration;
            set
            {
                if (enableGameplayVfxEnemyDamageBurstMigration == value)
                {
                    return;
                }

                enableGameplayVfxEnemyDamageBurstMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxBoxDestroySmokeMigration
        {
            get => enableGameplayVfxBoxDestroySmokeMigration;
            set
            {
                if (enableGameplayVfxBoxDestroySmokeMigration == value)
                {
                    return;
                }

                enableGameplayVfxBoxDestroySmokeMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxBoxDestroyShrinkMigration
        {
            get => enableGameplayVfxBoxDestroyShrinkMigration;
            set
            {
                if (enableGameplayVfxBoxDestroyShrinkMigration == value)
                {
                    return;
                }

                enableGameplayVfxBoxDestroyShrinkMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxEnemyDeathBurstMigration
        {
            get => enableGameplayVfxEnemyDeathBurstMigration;
            set
            {
                if (enableGameplayVfxEnemyDeathBurstMigration == value)
                {
                    return;
                }

                enableGameplayVfxEnemyDeathBurstMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxEnemyDeathMotionMigration
        {
            get => enableGameplayVfxEnemyDeathMotionMigration;
            set
            {
                if (enableGameplayVfxEnemyDeathMotionMigration == value)
                {
                    return;
                }

                enableGameplayVfxEnemyDeathMotionMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxItemConsumeBurstMigration
        {
            get => enableGameplayVfxItemConsumeBurstMigration;
            set
            {
                if (enableGameplayVfxItemConsumeBurstMigration == value)
                {
                    return;
                }

                enableGameplayVfxItemConsumeBurstMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxFlipImpactBurstMigration
        {
            get => enableGameplayVfxFlipImpactBurstMigration;
            set
            {
                if (enableGameplayVfxFlipImpactBurstMigration == value)
                {
                    return;
                }

                enableGameplayVfxFlipImpactBurstMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxFlipDestroySelfMotionMigration
        {
            get => enableGameplayVfxFlipDestroySelfMotionMigration;
            set
            {
                if (enableGameplayVfxFlipDestroySelfMotionMigration == value)
                {
                    return;
                }

                enableGameplayVfxFlipDestroySelfMotionMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxFlipImpactStayTrail
        {
            get => enableGameplayVfxFlipImpactStayTrail;
            set
            {
                if (enableGameplayVfxFlipImpactStayTrail == value)
                {
                    return;
                }

                enableGameplayVfxFlipImpactStayTrail = value;
                if (!value)
                {
                    motionFollowingVfxController.Refresh(
                        0,
                        null,
                        null,
                        pool,
                        bindingResolver,
                        enabled: false);
                }

                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxGlideWindTrail
        {
            get => enableGameplayVfxGlideWindTrail;
            set
            {
                if (enableGameplayVfxGlideWindTrail == value)
                {
                    return;
                }

                enableGameplayVfxGlideWindTrail = value;
                if (!value)
                {
                    var cueId = GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail);
                    enemyMotionAttachedFollowerPlanner.RemoveCue(cueId);
                    motionFollowingVfxController.StopAttachedFollowersForCue(cueId, tail: true);
                }

                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxChargeBoosterTrail
        {
            get => enableGameplayVfxChargeBoosterTrail;
            set
            {
                if (enableGameplayVfxChargeBoosterTrail == value)
                {
                    return;
                }

                enableGameplayVfxChargeBoosterTrail = value;
                if (!value)
                {
                    var cueId = GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail);
                    enemyMotionAttachedFollowerPlanner.RemoveCue(cueId);
                    motionFollowingVfxController.StopAttachedFollowersForCue(cueId, tail: true);
                }

                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxBoxSlideTrail
        {
            get => enableGameplayVfxBoxSlideTrail;
            set
            {
                if (enableGameplayVfxBoxSlideTrail == value)
                {
                    return;
                }

                enableGameplayVfxBoxSlideTrail = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxTileFeatureLane
        {
            get => enableGameplayVfxTileFeatureLane;
            set
            {
                if (enableGameplayVfxTileFeatureLane == value)
                {
                    return;
                }

                enableGameplayVfxTileFeatureLane = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxGravityFieldEvents
        {
            get => enableGameplayVfxGravityFieldEvents;
            set
            {
                if (enableGameplayVfxGravityFieldEvents == value)
                {
                    return;
                }

                enableGameplayVfxGravityFieldEvents = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxGravityFieldContinuous
        {
            get => enableGameplayVfxGravityFieldContinuous;
            set
            {
                if (enableGameplayVfxGravityFieldContinuous == value)
                {
                    return;
                }

                enableGameplayVfxGravityFieldContinuous = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxGravityFieldLockedTarget
        {
            get => enableGameplayVfxGravityFieldLockedTarget;
            set
            {
                if (enableGameplayVfxGravityFieldLockedTarget == value)
                {
                    return;
                }

                enableGameplayVfxGravityFieldLockedTarget = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxImpactTransientBreakMigration
        {
            get => enableGameplayVfxImpactTransientBreakMigration;
            set
            {
                if (enableGameplayVfxImpactTransientBreakMigration == value)
                {
                    return;
                }

                enableGameplayVfxImpactTransientBreakMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxOutOfBoundsExitMigration
        {
            get => enableGameplayVfxOutOfBoundsExitMigration;
            set
            {
                if (enableGameplayVfxOutOfBoundsExitMigration == value)
                {
                    return;
                }

                enableGameplayVfxOutOfBoundsExitMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxUtilityWindupMigration
        {
            get => enableGameplayVfxUtilityWindupMigration;
            set
            {
                if (enableGameplayVfxUtilityWindupMigration == value)
                {
                    return;
                }

                enableGameplayVfxUtilityWindupMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxFrontFaceShieldActiveMigration
        {
            get => enableGameplayVfxFrontFaceShieldActiveMigration;
            set
            {
                if (enableGameplayVfxFrontFaceShieldActiveMigration == value)
                {
                    return;
                }

                enableGameplayVfxFrontFaceShieldActiveMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxFrontFaceShieldBlockMigration
        {
            get => enableGameplayVfxFrontFaceShieldBlockMigration;
            set
            {
                if (enableGameplayVfxFrontFaceShieldBlockMigration == value)
                {
                    return;
                }

                enableGameplayVfxFrontFaceShieldBlockMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxFrontFaceShieldWindupMigration
        {
            get => enableGameplayVfxFrontFaceShieldWindupMigration;
            set
            {
                if (enableGameplayVfxFrontFaceShieldWindupMigration == value)
                {
                    return;
                }

                enableGameplayVfxFrontFaceShieldWindupMigration = value;
                ResetIfNoGameplayVfxEnabled();
            }
        }

        public int LastPlannedRequestCount { get; private set; }

        public int ActiveVfxInstanceCount => pool?.ActiveCount ?? 0;

        public int MissingBindingCount =>
            (controller?.MissingBindingCount ?? 0) +
            flipDestroySelfMotionMissingBindingCount +
            flipImpactStayTrailMissingBindingCount +
            enemyMotionAttachedMissingBindingCount +
            boxSlideTrailMissingBindingCount +
            boxDestroyShrinkMissingBindingCount +
            impactTransientBreakMissingBindingCount +
            outOfBoundsExitMissingBindingCount +
            enemyDeathMotionMissingBindingCount;

        public int MissingAnchorCount =>
            (controller?.MissingAnchorCount ?? 0) +
            flipImpactStayTrailMissingOwnerViewCount +
            enemyMotionAttachedMissingOwnerViewCount +
            boxDestroyShrinkMissingAnchorCount +
            impactTransientBreakMissingAnchorCount +
            outOfBoundsExitMissingAnchorCount +
            enemyDeathMotionMissingAnchorCount;

        public int MissingPrefabCount => pool?.MissingPrefabCount ?? 0;

        public bool IsRuntimeInitialized => controller != null;

        public void ConfigureHostDefaultMap(VfxCueMapAsset cueMap)
        {
            if (hostDefaultCueMap == cueMap)
            {
                return;
            }

            hostDefaultCueMap = cueMap;
            RebuildBindingRuntime();
            ResetRuntimeComposition();
        }

        public void ConfigureFamilyProfiles(VfxProfileAsset[] profiles)
        {
            familyProfiles = profiles ?? Array.Empty<VfxProfileAsset>();
            RebuildBindingRuntime();
            ResetRuntimeComposition();
        }

        public void ResetSession()
        {
            LastPlannedRequestCount = 0;
            flipDestroySelfMotionMissingBindingCount = 0;
            flipImpactStayTrailMissingBindingCount = 0;
            flipImpactStayTrailMissingOwnerViewCount = 0;
            enemyMotionAttachedMissingBindingCount = 0;
            enemyMotionAttachedMissingOwnerViewCount = 0;
            boxSlideTrailMissingBindingCount = 0;
            boxDestroyShrinkMissingBindingCount = 0;
            boxDestroyShrinkMissingAnchorCount = 0;
            impactTransientBreakMissingBindingCount = 0;
            impactTransientBreakMissingAnchorCount = 0;
            outOfBoundsExitMissingBindingCount = 0;
            outOfBoundsExitMissingAnchorCount = 0;
            enemyDeathMotionMissingBindingCount = 0;
            enemyDeathMotionMissingAnchorCount = 0;
            playedFlipDestroySelfMotionKeys.Clear();
            playedBoxSlideTrailMotionKeys.Clear();
            playedImpactTransientBreakKeys.Clear();
            playedOutOfBoundsExitKeys.Clear();
            enemyMotionAttachedFollowerPlanner.Clear();
            motionFollowingVfxController.ResetSession();
            controller?.HardCleanupAll();
            planBuilder.Clear();
        }

        public void ConfigureOutputCamera(Camera outputCamera, Transform localSpaceRoot)
        {
            this.outputCamera = outputCamera;
            this.localSpaceRoot = localSpaceRoot;
        }

        public void Present(in GameplayTickPresentationExtensionContext context)
        {
            LastPlannedRequestCount = 0;
            if (!AnyGameplayVfxEnabled)
            {
                enemyMotionAttachedFollowerPlanner.Clear();
                return;
            }

            ConfigureEnemyPresentationProfiles(
                context.EnemyPresentationCatalog,
                context.EnemyPresentationBindings);
            enemyMotionAttachedFollowerPlanner.Build(
                context.Result.PresentationData,
                enableGameplayVfxGlideWindTrail,
                enableGameplayVfxChargeBoosterTrail);
            planBuilder.Clear();
            var planningContext = new GameplayVfxPlanningContext(
                context.Result.TickIndex,
                context.Result.PresentationData,
                context.Topology,
                context.TimingProfile);
            playerPlanner.Plan(planningContext, planBuilder);
            boxPlanner.Plan(planningContext, planBuilder);
            flipImpactBurstPlanner.Plan(planningContext, planBuilder);
            enemyPlanner.Plan(planningContext, planBuilder);
            if (enableGameplayVfxTileFeatureLane)
            {
                tileFeaturePlanner.Plan(planningContext, planBuilder);
            }

            if (enableGameplayVfxGravityFieldEvents ||
                enableGameplayVfxGravityFieldContinuous ||
                enableGameplayVfxGravityFieldLockedTarget)
            {
                gravityFieldPlanner.Plan(planningContext, planBuilder);
            }

            var plan = FilterByEnabledCues(planBuilder.Build());
            var shouldPlayFlipDestroySelfMotion =
                enableGameplayVfxFlipDestroySelfMotionMigration &&
                HasDestroySelfFlipImpactSignal(context.Result.PresentationData);
            var shouldPlayBoxSlideTrail =
                enableGameplayVfxBoxSlideTrail &&
                HasBoxSlideMotion(context.Result.PresentationData);
            var shouldPlayBoxDestroyShrink =
                enableGameplayVfxBoxDestroyShrinkMigration &&
                HasBoxDestroyExitSignal(context.Result.PresentationData);
            var shouldScheduleAfterEntityMotionBoxDestroyExit =
                (enableGameplayVfxBoxDestroySmokeMigration || enableGameplayVfxBoxDestroyShrinkMigration) &&
                HasAfterEntityMotionBoxDestroyExitSignal(context.Result.PresentationData);
            var shouldPlayImpactTransientBreak =
                enableGameplayVfxImpactTransientBreakMigration &&
                HasImpactTransientBreakSignal(context.Result.PresentationData);
            var shouldPlayOutOfBoundsExit =
                enableGameplayVfxOutOfBoundsExitMigration &&
                HasOutOfBoundsExitSignal(context.Result.PresentationData);
            var shouldPlayEnemyDeathMotion =
                enableGameplayVfxEnemyDeathMotionMigration &&
                HasEnemyDeathExitSignal(context.Result.PresentationData);
            if (plan.Requests.Count == 0 &&
                !shouldPlayFlipDestroySelfMotion &&
                !shouldPlayBoxSlideTrail &&
                !shouldPlayBoxDestroyShrink &&
                !shouldScheduleAfterEntityMotionBoxDestroyExit &&
                !shouldPlayImpactTransientBreak &&
                !shouldPlayOutOfBoundsExit &&
                !shouldPlayEnemyDeathMotion)
            {
                controller?.Refresh(GameplayVfxRequestPlan.Empty);
                return;
            }

            EnsureRuntime(context);
            controller.Refresh(plan);
            var flipDestroySelfMotionCommandCount = shouldPlayFlipDestroySelfMotion
                ? PlayFlipDestroySelfMotionCommands(context)
                : 0;
            var boxSlideTrailCommandCount = shouldPlayBoxSlideTrail
                ? PlayBoxSlideTrailCommands(context)
                : 0;
            var boxDestroyShrinkCommandCount = shouldPlayBoxDestroyShrink
                ? PlayBoxDestroyShrinkCommands(context)
                : 0;
            var delayedBoxDestroyExitVfxCount = shouldScheduleAfterEntityMotionBoxDestroyExit
                ? ScheduleAfterEntityMotionBoxDestroyExitVfx(context)
                : 0;
            var impactTransientBreakCommandCount = shouldPlayImpactTransientBreak
                ? PlayImpactTransientBreakCommands(context)
                : 0;
            var outOfBoundsExitCommandCount = shouldPlayOutOfBoundsExit
                ? PlayOutOfBoundsExitCommands(context)
                : 0;
            var enemyDeathMotionCommandCount = shouldPlayEnemyDeathMotion
                ? PlayEnemyDeathMotionCommands(context)
                : 0;
            LastPlannedRequestCount = plan.Requests.Count +
                                      flipDestroySelfMotionCommandCount +
                                      boxSlideTrailCommandCount +
                                      boxDestroyShrinkCommandCount +
                                      delayedBoxDestroyExitVfxCount +
                                      impactTransientBreakCommandCount +
                                      outOfBoundsExitCommandCount +
                                      enemyDeathMotionCommandCount;
        }

        public void UpdatePresentation(float deltaTime)
        {
            AdvanceDelayedBoxDestroyExitVfx(deltaTime);
            pool?.Advance(deltaTime);
        }

        public void RefreshPresentationMotionVfx(in GameplayPresentationMotionVfxContext context)
        {
            if (!AnyGameplayVfxEnabled)
            {
                enemyMotionAttachedFollowerPlanner.Clear();
                return;
            }

            EnsureRuntime(
                context.Projector,
                context.StateStore);
            motionFollowingVfxController.Refresh(
                context.TickIndex,
                context.TrackState as GameplayPresentationTrackState,
                context.StateStore,
                pool,
                bindingResolver,
                enabled: enableGameplayVfxFlipImpactStayTrail,
                enemyMotionAttachedFollowerPlanner.DesiredFollowers,
                attachedFollowersEnabled: enableGameplayVfxGlideWindTrail || enableGameplayVfxChargeBoosterTrail);
            flipImpactStayTrailMissingBindingCount = motionFollowingVfxController.MotionMissingBindingCount;
            flipImpactStayTrailMissingOwnerViewCount = motionFollowingVfxController.MotionMissingOwnerViewCount;
            enemyMotionAttachedMissingBindingCount = motionFollowingVfxController.AttachedMissingBindingCount;
            enemyMotionAttachedMissingOwnerViewCount = motionFollowingVfxController.AttachedMissingOwnerViewCount;
            PlayReadyDelayedBoxDestroyExitVfx();
        }

        public void HardCleanup()
        {
            motionFollowingVfxController.HardCleanup();
            controller?.HardCleanupAll();
            LastPlannedRequestCount = 0;
            playedFlipDestroySelfMotionKeys.Clear();
            playedBoxSlideTrailMotionKeys.Clear();
            playedImpactTransientBreakKeys.Clear();
            playedOutOfBoundsExitKeys.Clear();
            scheduledDelayedBoxDestroyExitVfxKeys.Clear();
            pendingDelayedBoxDestroyExitVfx.Clear();
            readyDelayedBoxDestroyExitVfx.Clear();
            enemyMotionAttachedFollowerPlanner.Clear();
        }

        private void EnsureRuntime(in GameplayTickPresentationExtensionContext context)
        {
            EnsureRuntime(context.Projector, context.StateStore);
        }

        private void EnsureRuntime(
            GameplayCubeProjector projector,
            GameplayPresentationStateStore stateStore)
        {
            if (projector == null)
            {
                throw new InvalidOperationException("Gameplay VFX production runtime requires a gameplay cube projector.");
            }

            if (stateStore == null)
            {
                throw new InvalidOperationException("Gameplay VFX production runtime requires a presentation state store.");
            }

            if (controller != null &&
                ReferenceEquals(configuredProjector, projector) &&
                ReferenceEquals(configuredStateStore, stateStore))
            {
                return;
            }

            motionFollowingVfxController.HardCleanup();
            controller?.HardCleanupAll();
            configuredProjector = projector;
            configuredStateStore = stateStore;
            runtimeRoot = runtimeRoot != null
                ? runtimeRoot
                : GameplayVfxRuntimeRoot.CreateUnder(transform);
            RebuildBindingRuntime();
            var anchorResolver = new GameplayVfxHostAnchorResolver(
                new GameplayVfxHostCellAnchorProjector(projector),
                new GameplayVfxHostEntityAnchorProjector(stateStore));
            pool = new GameplayVfxGameObjectPool(
                runtimeRoot,
                prefabProvider,
                cloneSourceProvider: new GameplayVfxStateStoreCloneSourceProvider(stateStore));
            controller = new GameplayVfxPresentationController(
                pool,
                anchorResolver,
                bindingResolver,
                new VfxPersistentHandleRegistry(),
                new VfxLifetimeRunner());
        }

        private void RebuildBindingRuntime()
        {
            var composition = GameplayVfxBindingComposition.Compose(
                hostDefaultCueMap,
                familyProfiles ?? Array.Empty<VfxProfileAsset>());
            if (!composition.Succeeded)
            {
                throw new InvalidOperationException("Gameplay VFX binding composition failed.");
            }

            var profileProvider = enemyPresentationVfxProfileProvider;
            bindingResolver = profileProvider != null && profileProvider.Count > 0
                ? new ProfileAwareVfxBindingResolver(profileProvider, composition.Resolver)
                : composition.Resolver;
            prefabProvider = new AuthoringPrefabProvider(
                profileProvider,
                hostDefaultCueMap,
                familyProfiles);
        }

        private void ConfigureEnemyPresentationProfiles(
            EnemyPresentationCatalog catalog,
            EnemyPresentationBinding[] bindings)
        {
            var resolvedBindings = bindings ?? Array.Empty<EnemyPresentationBinding>();
            if (ReferenceEquals(configuredEnemyPresentationCatalog, catalog) &&
                ReferenceEquals(configuredEnemyPresentationBindings, resolvedBindings))
            {
                return;
            }

            configuredEnemyPresentationCatalog = catalog;
            configuredEnemyPresentationBindings = resolvedBindings;
            enemyPresentationVfxProfileProvider = EnemyPresentationVfxProfileMapBuilder.Build(
                configuredEnemyPresentationCatalog,
                configuredEnemyPresentationBindings,
                nameof(GameplayVfxProductionRuntime));
            RebuildBindingRuntime();
            ResetRuntimeComposition();
        }

        private void ResetRuntimeComposition()
        {
            motionFollowingVfxController.HardCleanup();
            enemyMotionAttachedFollowerPlanner.Clear();
            controller?.HardCleanupAll();
            controller = null;
            pool = null;
        }

        private bool AnyEnemyJumpVfxEnabled => enableEnemyJumpTargetVfx || enableEnemyJumpLandingDustVfx;

        private bool AnyGameplayVfxEnabled =>
            AnyEnemyJumpVfxEnabled ||
            enableGameplayVfxDamageBurstMigration ||
            enableGameplayVfxEnemyDamageBurstMigration ||
            enableGameplayVfxEnemyDeathBurstMigration ||
            enableGameplayVfxEnemyDeathMotionMigration ||
            enableGameplayVfxBoxDestroySmokeMigration ||
            enableGameplayVfxBoxDestroyShrinkMigration ||
            enableGameplayVfxItemConsumeBurstMigration ||
            enableGameplayVfxFlipImpactBurstMigration ||
            enableGameplayVfxFlipDestroySelfMotionMigration ||
            enableGameplayVfxFlipImpactStayTrail ||
            enableGameplayVfxGlideWindTrail ||
            enableGameplayVfxChargeBoosterTrail ||
            enableGameplayVfxBoxSlideTrail ||
            enableGameplayVfxImpactTransientBreakMigration ||
            enableGameplayVfxOutOfBoundsExitMigration ||
            enableGameplayVfxUtilityWindupMigration ||
            enableGameplayVfxFrontFaceShieldActiveMigration ||
            enableGameplayVfxFrontFaceShieldBlockMigration ||
            enableGameplayVfxFrontFaceShieldWindupMigration ||
            enableGameplayVfxTileFeatureLane ||
            enableGameplayVfxGravityFieldEvents ||
            enableGameplayVfxGravityFieldContinuous ||
            enableGameplayVfxGravityFieldLockedTarget;

        private void ResetIfNoEnemyJumpVfxEnabled()
        {
            ResetIfNoGameplayVfxEnabled();
        }

        private void ResetIfNoGameplayVfxEnabled()
        {
            if (AnyGameplayVfxEnabled)
            {
                return;
            }

            LastPlannedRequestCount = 0;
            ResetRuntimeComposition();
        }

        private GameplayVfxRequestPlan FilterByEnabledCues(GameplayVfxRequestPlan plan)
        {
            if (plan == null || plan.Requests.Count == 0)
            {
                return GameplayVfxRequestPlan.Empty;
            }

            var filteredRequests = new List<GameplayVfxRequest>(plan.Requests.Count);
            for (var i = 0; i < plan.Requests.Count; i++)
            {
                var request = plan.Requests[i];
                if (IsCueEnabled(request.CueId) &&
                    !IsParameterizedCommandOwnedCue(request.CueId))
                {
                    filteredRequests.Add(request);
                }
            }

            return filteredRequests.Count == 0
                ? GameplayVfxRequestPlan.Empty
                : new GameplayVfxRequestPlan(filteredRequests);
        }

        private bool IsCueEnabled(GameplayVfxCueId cueId)
        {
            return (enableGameplayVfxDamageBurstMigration && cueId == GameplayVfxCueId.From(PlayerVfxCue.Damage)) ||
                   (enableGameplayVfxEnemyDamageBurstMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.Damage)) ||
                   (enableGameplayVfxEnemyDeathBurstMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.Death)) ||
                   (enableGameplayVfxEnemyDeathMotionMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)) ||
                   (enableGameplayVfxUtilityWindupMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup)) ||
                   (enableGameplayVfxFrontFaceShieldActiveMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive)) ||
                   (enableGameplayVfxFrontFaceShieldBlockMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock)) ||
                   (enableGameplayVfxFrontFaceShieldWindupMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldWindup)) ||
                   (enableGameplayVfxBoxDestroySmokeMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.DestroySmoke)) ||
                   (enableGameplayVfxBoxDestroyShrinkMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.DestroyShrink)) ||
                   (enableGameplayVfxItemConsumeBurstMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.ItemConsume)) ||
                   (enableGameplayVfxFlipImpactBurstMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst)) ||
                   (enableGameplayVfxFlipDestroySelfMotionMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion)) ||
                   (enableGameplayVfxFlipImpactStayTrail && cueId == GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail)) ||
                   (enableGameplayVfxGlideWindTrail && cueId == GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail)) ||
                   (enableGameplayVfxChargeBoosterTrail && cueId == GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail)) ||
                   (enableGameplayVfxBoxSlideTrail && cueId == GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail)) ||
                   (enableGameplayVfxImpactTransientBreakMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak)) ||
                   (enableGameplayVfxOutOfBoundsExitMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.OutOfBoundsExit)) ||
                   (enableGameplayVfxOutOfBoundsExitMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit)) ||
                   (enableGameplayVfxTileFeatureLane && cueId.Family == GameplayVfxFamily.TileFeature) ||
                   IsGravityFieldCueEnabled(cueId) ||
                   (enableEnemyJumpTargetVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)) ||
                   (enableEnemyJumpLandingDustVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust));
        }

        private bool IsGravityFieldCueEnabled(GameplayVfxCueId cueId)
        {
            if (cueId.Family != GameplayVfxFamily.GravityField)
            {
                return false;
            }

            if (enableGameplayVfxGravityFieldEvents &&
                (cueId == GameplayVfxCueId.From(GravityFieldVfxCue.Activated) ||
                 cueId == GameplayVfxCueId.From(GravityFieldVfxCue.Expired)))
            {
                return true;
            }

            if (enableGameplayVfxGravityFieldContinuous &&
                (cueId == GameplayVfxCueId.From(GravityFieldVfxCue.ChargingArea) ||
                 cueId == GameplayVfxCueId.From(GravityFieldVfxCue.ActiveArea)))
            {
                return true;
            }

            return enableGameplayVfxGravityFieldLockedTarget &&
                   cueId == GameplayVfxCueId.From(GravityFieldVfxCue.LockedTarget);
        }

        private static bool IsParameterizedCommandOwnedCue(GameplayVfxCueId cueId)
        {
            return cueId == GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak) ||
                   cueId == GameplayVfxCueId.From(BoxVfxCue.OutOfBoundsExit) ||
                   cueId == GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit);
        }

        private int PlayImpactTransientBreakCommands(in GameplayTickPresentationExtensionContext context)
        {
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return 0;
            }

            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(context.StateStore, trackState);
            var plannedCommandCount = 0;
            var signals = presentationData.ImpactTransientSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!ImpactTransientBreakVfxCommandBuilder.IsImpactTransientBreakCandidate(signal))
                {
                    continue;
                }

                if (!ImpactTransientBreakVfxCommandBuilder.TryBuild(
                        context.Result.TickIndex,
                        signal,
                        context.TimingProfile,
                        poseResolver,
                        context.Projector,
                        out var command))
                {
                    impactTransientBreakMissingAnchorCount++;
                    continue;
                }

                plannedCommandCount++;
                var key = ImpactTransientBreakInstanceKey.Create(context.Result.TickIndex, command);
                if (playedImpactTransientBreakKeys.Contains(key))
                {
                    continue;
                }

                if (TryPlayImpactTransientBreakCommand(context.Result.TickIndex, signal, command))
                {
                    playedImpactTransientBreakKeys.Add(key);
                }
            }

            return plannedCommandCount;
        }

        private bool TryPlayImpactTransientBreakCommand(
            int tickIndex,
            in TickImpactTransientPresentationSignal signal,
            in ParameterizedMotionVfxCommand command)
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak);
            var request = new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: command.SequenceId,
                presentationSeed: command.PresentationSeed,
                sourceEntityId: signal.EntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    signal.SourceCell,
                    signal.Topology,
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                impactTransientBreakMissingBindingCount++;
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match ImpactTransientBreak request cue.");
            }

            var anchor = VfxResolvedAnchor.ForCell(
                signal.SourceCell,
                signal.Topology,
                VfxAnchorSlot.CellCenter,
                command.SourceLocalPosition,
                command.SourceLocalRotation);
            var playbackCommand = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return pool.PlayParameterizedMotion(playbackCommand, command) != null;
        }

        private int PlayOutOfBoundsExitCommands(in GameplayTickPresentationExtensionContext context)
        {
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return 0;
            }

            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(context.StateStore, trackState);
            var plannedCommandCount = 0;
            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!OutOfBoundsExitVfxCommandBuilder.IsOutOfBoundsExitCandidate(signal) ||
                    !OutOfBoundsExitVfxCommandBuilder.TryResolveCue(signal, out var cueId))
                {
                    continue;
                }

                if (!OutOfBoundsExitVfxCommandBuilder.TryBuild(
                        context.Result.TickIndex,
                        signal,
                        cueId,
                        context.TimingProfile,
                        poseResolver,
                        context.Projector,
                        out var command))
                {
                    outOfBoundsExitMissingAnchorCount++;
                    continue;
                }

                plannedCommandCount++;
                var key = OutOfBoundsExitInstanceKey.Create(context.Result.TickIndex, command);
                if (playedOutOfBoundsExitKeys.Contains(key))
                {
                    continue;
                }

                if (TryPlayOutOfBoundsExitCommand(context.Result.TickIndex, signal, cueId, command))
                {
                    playedOutOfBoundsExitKeys.Add(key);
                }
            }

            return plannedCommandCount;
        }

        private bool TryPlayOutOfBoundsExitCommand(
            int tickIndex,
            in TickEntityExitPresentationSignal signal,
            GameplayVfxCueId cueId,
            in ParameterizedMotionVfxCommand command)
        {
            var request = new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: command.SequenceId,
                presentationSeed: command.PresentationSeed,
                sourceEntityId: signal.ExitedEntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    signal.SourceCell,
                    signal.Topology,
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                outOfBoundsExitMissingBindingCount++;
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match OutOfBoundsExit request cue.");
            }

            var anchor = VfxResolvedAnchor.ForCell(
                signal.SourceCell,
                signal.Topology,
                VfxAnchorSlot.CellCenter,
                command.SourceLocalPosition,
                command.SourceLocalRotation);
            var playbackCommand = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return pool.PlayParameterizedMotion(playbackCommand, command) != null;
        }

        private int PlayEnemyDeathMotionCommands(in GameplayTickPresentationExtensionContext context)
        {
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return 0;
            }

            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(context.StateStore, trackState);
            var targetResolver = new EnemyDeathMotionTargetResolver(
                localSpaceRoot,
                outputCamera,
                context.StateStore,
                context.Projector.CellSize);
            var plannedCommandCount = 0;
            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!EnemyDeathMotionVfxCommandBuilder.IsEnemyDeathExitCandidate(signal))
                {
                    continue;
                }

                if (!EnemyDeathMotionVfxCommandBuilder.TryBuild(
                        signal,
                        context.TimingProfile,
                        poseResolver,
                        context.Projector,
                        targetResolver,
                        out var command))
                {
                    enemyDeathMotionMissingAnchorCount++;
                    continue;
                }

                plannedCommandCount++;
                TryPlayEnemyDeathMotionCommand(context.Result.TickIndex, command);
            }

            return plannedCommandCount;
        }

        private bool TryPlayEnemyDeathMotionCommand(
            int tickIndex,
            in EnemyDeathMotionVfxCommand command)
        {
            var parameterizedCommand = command.ToParameterizedMotionVfxCommand();
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.DeathMotion);
            var request = new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: parameterizedCommand.SequenceId,
                presentationSeed: parameterizedCommand.PresentationSeed,
                sourceEntityId: command.EntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    command.SourceCell,
                    command.Topology,
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                enemyDeathMotionMissingBindingCount++;
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match Enemy Death motion request cue.");
            }

            var anchor = VfxResolvedAnchor.ForCell(
                command.SourceCell,
                command.Topology,
                VfxAnchorSlot.CellCenter,
                command.SourceLocalPosition,
                command.SourceLocalRotation);
            var playbackCommand = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return pool.PlayParameterizedMotion(playbackCommand, parameterizedCommand) != null;
        }

        private int PlayBoxSlideTrailCommands(in GameplayTickPresentationExtensionContext context)
        {
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return 0;
            }

            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(context.StateStore, trackState);
            var motionTimingResolver = new GameplayMotionTimingResolver(context.StateStore, trackState);
            var plannedCommandCount = 0;
            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (!BoxSlideTrailVfxCommandBuilder.TryBuild(
                        context.Result.TickIndex,
                        motion,
                        context.TimingProfile,
                        motionTimingResolver,
                        poseResolver,
                        context.Projector,
                        context.Topology,
                        out var command))
                {
                    continue;
                }

                plannedCommandCount++;
                var key = BoxSlideTrailVfxCommandBuilder.CreateInstanceKey(
                    context.Result.TickIndex,
                    motion);
                if (playedBoxSlideTrailMotionKeys.Contains(key))
                {
                    continue;
                }

                if (TryPlayBoxSlideTrailCommand(context.Result.TickIndex, motion, command))
                {
                    playedBoxSlideTrailMotionKeys.Add(key);
                }
            }

            return plannedCommandCount;
        }

        private bool TryPlayBoxSlideTrailCommand(
            int tickIndex,
            TickEntityMotion motion,
            in ParameterizedMotionVfxCommand command)
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail);
            var sourceTopology = motion.SourceTopology ??
                                 motion.DestinationTopology ??
                                 configuredStateStore?.CommittedTopology ??
                                 default;
            var request = new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: command.SequenceId,
                presentationSeed: command.PresentationSeed,
                sourceEntityId: motion.EntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    motion.SourceCell,
                    sourceTopology,
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                boxSlideTrailMissingBindingCount++;
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match Box Slide trail request cue.");
            }

            var anchor = VfxResolvedAnchor.ForCell(
                motion.SourceCell,
                sourceTopology,
                VfxAnchorSlot.CellCenter,
                command.SourceLocalPosition,
                command.SourceLocalRotation);
            var playbackCommand = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return pool.PlayParameterizedMotion(playbackCommand, command) != null;
        }

        private int PlayBoxDestroyShrinkCommands(in GameplayTickPresentationExtensionContext context)
        {
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return 0;
            }

            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(context.StateStore, trackState);
            var plannedCommandCount = 0;
            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!BoxDestroyShrinkVfxCommandBuilder.IsBoxDestroyExitCandidate(signal) ||
                    signal.Timing == EntityExitPresentationTiming.AfterEntityMotion ||
                    BoxDestroyShrinkVfxCommandBuilder.IsDuplicateOwnedExit(presentationData, signal.ExitedEntityId))
                {
                    continue;
                }

                if (!BoxDestroyShrinkVfxCommandBuilder.TryBuild(
                        context.Result.TickIndex,
                        signal,
                        context.TimingProfile,
                        poseResolver,
                        context.Projector,
                        out var command))
                {
                    boxDestroyShrinkMissingAnchorCount++;
                    continue;
                }

                plannedCommandCount++;
                TryPlayBoxDestroyShrinkCommand(context.Result.TickIndex, signal, command);
            }

            return plannedCommandCount;
        }

        private int ScheduleAfterEntityMotionBoxDestroyExitVfx(in GameplayTickPresentationExtensionContext context)
        {
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return 0;
            }

            var scheduledCount = 0;
            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!BoxDestroyShrinkVfxCommandBuilder.IsBoxDestroyExitCandidate(signal) ||
                    signal.Timing != EntityExitPresentationTiming.AfterEntityMotion ||
                    BoxDestroyShrinkVfxCommandBuilder.IsDuplicateOwnedExit(presentationData, signal.ExitedEntityId))
                {
                    continue;
                }

                var playSmoke = enableGameplayVfxBoxDestroySmokeMigration;
                var playShrink = enableGameplayVfxBoxDestroyShrinkMigration;
                if (!playSmoke && !playShrink)
                {
                    continue;
                }

                var key = DelayedBoxDestroyExitVfxKey.Create(context.Result.TickIndex, signal);
                if (!scheduledDelayedBoxDestroyExitVfxKeys.Add(key))
                {
                    continue;
                }

                var delaySeconds = ResolveEntityMotionDelaySeconds(
                    presentationData,
                    signal.ExitedEntityId,
                    context.TimingProfile);
                if (delaySeconds <= 0.0001f)
                {
                    PlayDelayedBoxDestroyExitVfx(
                        new DelayedBoxDestroyExitVfx(
                            context.Result.TickIndex,
                            signal,
                            remainingSeconds: 0f,
                            playSmoke,
                            playShrink,
                            context.TimingProfile));
                    scheduledCount++;
                    continue;
                }

                pendingDelayedBoxDestroyExitVfx.Add(
                    new DelayedBoxDestroyExitVfx(
                        context.Result.TickIndex,
                        signal,
                        delaySeconds,
                        playSmoke,
                        playShrink,
                        context.TimingProfile));
                scheduledCount++;
            }

            return scheduledCount;
        }

        private void AdvanceDelayedBoxDestroyExitVfx(float deltaTime)
        {
            if (pendingDelayedBoxDestroyExitVfx.Count == 0)
            {
                return;
            }

            var advanceSeconds = Mathf.Max(0f, deltaTime);
            for (var i = pendingDelayedBoxDestroyExitVfx.Count - 1; i >= 0; i--)
            {
                var pending = pendingDelayedBoxDestroyExitVfx[i].Advance(advanceSeconds);
                if (pending.RemainingSeconds > 0.0001f)
                {
                    pendingDelayedBoxDestroyExitVfx[i] = pending;
                    continue;
                }

                pendingDelayedBoxDestroyExitVfx.RemoveAt(i);
                readyDelayedBoxDestroyExitVfx.Add(pending);
            }
        }

        private void PlayReadyDelayedBoxDestroyExitVfx()
        {
            if (readyDelayedBoxDestroyExitVfx.Count == 0)
            {
                return;
            }

            for (var i = 0; i < readyDelayedBoxDestroyExitVfx.Count; i++)
            {
                PlayDelayedBoxDestroyExitVfx(readyDelayedBoxDestroyExitVfx[i]);
            }

            readyDelayedBoxDestroyExitVfx.Clear();
        }

        private void PlayDelayedBoxDestroyExitVfx(in DelayedBoxDestroyExitVfx delayed)
        {
            if (delayed.PlaySmoke)
            {
                TryPlayBoxDestroySmokeRequest(delayed.TickIndex, delayed.Signal);
            }

            if (!delayed.PlayShrink)
            {
                return;
            }

            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(configuredStateStore, trackState);
            if (!BoxDestroyShrinkVfxCommandBuilder.TryBuild(
                    delayed.TickIndex,
                    delayed.Signal,
                    delayed.TimingProfile,
                    poseResolver,
                    configuredProjector,
                    out var command))
            {
                boxDestroyShrinkMissingAnchorCount++;
                return;
            }

            TryPlayBoxDestroyShrinkCommand(delayed.TickIndex, delayed.Signal, command);
        }

        private bool TryPlayBoxDestroySmokeRequest(int tickIndex, in TickEntityExitPresentationSignal signal)
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.DestroySmoke);
            var request = new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: signal.PresentationSeed != 0 ? signal.PresentationSeed : signal.ExitedEntityId,
                presentationSeed: signal.PresentationSeed != 0 ? signal.PresentationSeed : signal.ExitedEntityId,
                sourceEntityId: signal.ExitedEntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    signal.SourceCell,
                    signal.Topology,
                    VfxAnchorSlot.CellFloor),
                timing: VfxTimingKind.AtMotionEnd,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match Box DestroySmoke request cue.");
            }

            var anchor = VfxResolvedAnchor.ForCell(
                signal.SourceCell,
                signal.Topology,
                VfxAnchorSlot.CellFloor);
            var playbackCommand = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return pool.PlayTransient(playbackCommand) != null;
        }

        private static float ResolveEntityMotionDelaySeconds(
            TickPresentationData presentationData,
            int entityId,
            GameplayTimingProfile timingProfile)
        {
            if (presentationData == null || timingProfile == null)
            {
                return 0f;
            }

            var delaySeconds = 0f;
            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion.EntityId != entityId)
                {
                    continue;
                }

                delaySeconds += ResolveGlobalMotionDurationSeconds(motion.MotionKind, timingProfile);
            }

            return delaySeconds;
        }

        private static float ResolveGlobalMotionDurationSeconds(
            TickEntityMotionKind motionKind,
            GameplayTimingProfile timingProfile)
        {
            return motionKind switch
            {
                TickEntityMotionKind.Move => timingProfile.MoveMotionDurationSeconds,
                TickEntityMotionKind.Flip => timingProfile.FlipMotionDurationSeconds,
                TickEntityMotionKind.Push => timingProfile.PushMotionDurationSeconds,
                TickEntityMotionKind.BoxSlide => timingProfile.BoxSlideStepIntervalSeconds,
                TickEntityMotionKind.ProjectileMove => timingProfile.ProjectileStepIntervalSeconds,
                _ => timingProfile.PushMotionDurationSeconds,
            };
        }

        private bool TryPlayBoxDestroyShrinkCommand(
            int tickIndex,
            in TickEntityExitPresentationSignal signal,
            in ParameterizedMotionVfxCommand command)
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.DestroyShrink);
            var request = new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: command.SequenceId,
                presentationSeed: command.PresentationSeed,
                sourceEntityId: signal.ExitedEntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    signal.SourceCell,
                    signal.Topology,
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                boxDestroyShrinkMissingBindingCount++;
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match Box DestroyShrink request cue.");
            }

            var anchor = VfxResolvedAnchor.ForCell(
                signal.SourceCell,
                signal.Topology,
                VfxAnchorSlot.CellCenter,
                command.SourceLocalPosition,
                command.SourceLocalRotation);
            var playbackCommand = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return pool.PlayParameterizedMotion(playbackCommand, command) != null;
        }

        private int PlayFlipDestroySelfMotionCommands(in GameplayTickPresentationExtensionContext context)
        {
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return 0;
            }

            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(context.StateStore, trackState);
            var motionTimingResolver = new GameplayMotionTimingResolver(context.StateStore, trackState);
            var plannedCommandCount = 0;
            var signals = presentationData.FlipImpactSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!FlipDestroySelfMotionVfxCommandBuilder.TryBuild(
                        signal,
                        context.TimingProfile,
                        motionTimingResolver,
                        poseResolver,
                        context.Projector,
                        out var command))
                {
                    continue;
                }

                plannedCommandCount++;
                var key = FlipDestroySelfMotionInstanceKey.Create(command, context.Result.TickIndex);
                if (playedFlipDestroySelfMotionKeys.Contains(key))
                {
                    continue;
                }

                if (TryPlayFlipDestroySelfMotionCommand(context.Result.TickIndex, command))
                {
                    playedFlipDestroySelfMotionKeys.Add(key);
                }
            }

            return plannedCommandCount;
        }

        private bool TryPlayFlipDestroySelfMotionCommand(
            int tickIndex,
            in FlipDestroySelfMotionVfxCommand command)
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion);
            var request = new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: command.SourceActionPlanId > 0 ? command.SourceActionPlanId : command.BoxEntityId,
                presentationSeed: command.PresentationSeed,
                sourceEntityId: command.BoxEntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    command.SourceCell,
                    command.Topology,
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!bindingResolver.TryResolve(request, out var policy))
            {
                flipDestroySelfMotionMissingBindingCount++;
                return false;
            }

            policy.ValidateOrThrow();
            if (policy.CueId != request.CueId)
            {
                throw new InvalidOperationException("Gameplay VFX binding cue does not match Flip DestroySelf motion request cue.");
            }

            var anchor = VfxResolvedAnchor.ForCell(
                command.SourceCell,
                command.Topology,
                VfxAnchorSlot.CellCenter,
                command.SourceLocalPosition,
                command.SourceLocalRotation);
            var playbackCommand = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return pool.PlayParameterizedMotion(playbackCommand, command.ToParameterizedMotionVfxCommand()) != null;
        }

        private static bool HasDestroySelfFlipImpactSignal(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                return false;
            }

            var signals = presentationData.FlipImpactSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (signals[i].Disposition == FlipImpactPresentationDisposition.DestroySelf)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBoxSlideMotion(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                return false;
            }

            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                if (motions[i].MotionKind == TickEntityMotionKind.BoxSlide &&
                    motions[i].EntityId > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBoxDestroyExitSignal(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                return false;
            }

            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (BoxDestroyShrinkVfxCommandBuilder.IsBoxDestroyExitCandidate(signal) &&
                    !BoxDestroyShrinkVfxCommandBuilder.IsDuplicateOwnedExit(presentationData, signal.ExitedEntityId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAfterEntityMotionBoxDestroyExitSignal(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                return false;
            }

            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.Timing == EntityExitPresentationTiming.AfterEntityMotion &&
                    BoxDestroyShrinkVfxCommandBuilder.IsBoxDestroyExitCandidate(signal) &&
                    !BoxDestroyShrinkVfxCommandBuilder.IsDuplicateOwnedExit(presentationData, signal.ExitedEntityId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasImpactTransientBreakSignal(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                return false;
            }

            var signals = presentationData.ImpactTransientSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (ImpactTransientBreakVfxCommandBuilder.IsImpactTransientBreakCandidate(signals[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasOutOfBoundsExitSignal(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                return false;
            }

            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (OutOfBoundsExitVfxCommandBuilder.IsOutOfBoundsExitCandidate(signals[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEnemyDeathExitSignal(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                return false;
            }

            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (EnemyDeathMotionVfxCommandBuilder.IsEnemyDeathExitCandidate(signals[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct ImpactTransientBreakInstanceKey : IEquatable<ImpactTransientBreakInstanceKey>
        {
            private ImpactTransientBreakInstanceKey(int sequenceId, int sourceEntityId)
            {
                SequenceId = sequenceId;
                SourceEntityId = sourceEntityId;
            }

            private int SequenceId { get; }

            private int SourceEntityId { get; }

            public bool Equals(ImpactTransientBreakInstanceKey other)
            {
                return SequenceId == other.SequenceId &&
                       SourceEntityId == other.SourceEntityId;
            }

            public override bool Equals(object obj)
            {
                return obj is ImpactTransientBreakInstanceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(SequenceId, SourceEntityId);
            }

            public static ImpactTransientBreakInstanceKey Create(
                int tickIndexFallback,
                in ParameterizedMotionVfxCommand command)
            {
                return command.PresentationSeed != 0
                    ? new ImpactTransientBreakInstanceKey(command.PresentationSeed, command.SourceEntityId)
                    : new ImpactTransientBreakInstanceKey(tickIndexFallback, command.SourceEntityId);
            }
        }

        private readonly struct OutOfBoundsExitInstanceKey : IEquatable<OutOfBoundsExitInstanceKey>
        {
            private OutOfBoundsExitInstanceKey(int sequenceId, int sourceEntityId, GameplayVfxCueId cueId)
            {
                SequenceId = sequenceId;
                SourceEntityId = sourceEntityId;
                CueId = cueId;
            }

            private int SequenceId { get; }

            private int SourceEntityId { get; }

            private GameplayVfxCueId CueId { get; }

            public bool Equals(OutOfBoundsExitInstanceKey other)
            {
                return SequenceId == other.SequenceId &&
                       SourceEntityId == other.SourceEntityId &&
                       CueId.Equals(other.CueId);
            }

            public override bool Equals(object obj)
            {
                return obj is OutOfBoundsExitInstanceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(SequenceId, SourceEntityId, CueId);
            }

            public static OutOfBoundsExitInstanceKey Create(
                int tickIndexFallback,
                in ParameterizedMotionVfxCommand command)
            {
                var sequence = command.PresentationSeed != 0
                    ? command.PresentationSeed
                    : tickIndexFallback;
                return new OutOfBoundsExitInstanceKey(sequence, command.SourceEntityId, command.CueId);
            }
        }

        private readonly struct FlipDestroySelfMotionInstanceKey : IEquatable<FlipDestroySelfMotionInstanceKey>
        {
            private FlipDestroySelfMotionInstanceKey(int correlationId, int boxEntityId, bool usesTickFallback)
            {
                CorrelationId = correlationId;
                BoxEntityId = boxEntityId;
                UsesTickFallback = usesTickFallback;
            }

            private int CorrelationId { get; }

            private int BoxEntityId { get; }

            private bool UsesTickFallback { get; }

            public bool Equals(FlipDestroySelfMotionInstanceKey other)
            {
                return CorrelationId == other.CorrelationId &&
                       BoxEntityId == other.BoxEntityId &&
                       UsesTickFallback == other.UsesTickFallback;
            }

            public override bool Equals(object obj)
            {
                return obj is FlipDestroySelfMotionInstanceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(CorrelationId, BoxEntityId, UsesTickFallback);
            }

            public static FlipDestroySelfMotionInstanceKey Create(
                in FlipDestroySelfMotionVfxCommand command,
                int tickIndexFallback)
            {
                return command.SourceActionPlanId > 0
                    ? new FlipDestroySelfMotionInstanceKey(command.SourceActionPlanId, command.BoxEntityId, usesTickFallback: false)
                    : new FlipDestroySelfMotionInstanceKey(tickIndexFallback, command.BoxEntityId, usesTickFallback: true);
            }
        }

        private readonly struct DelayedBoxDestroyExitVfxKey : IEquatable<DelayedBoxDestroyExitVfxKey>
        {
            private DelayedBoxDestroyExitVfxKey(int tickIndex, int entityId, int presentationSeed)
            {
                TickIndex = tickIndex;
                EntityId = entityId;
                PresentationSeed = presentationSeed;
            }

            private int TickIndex { get; }

            private int EntityId { get; }

            private int PresentationSeed { get; }

            public bool Equals(DelayedBoxDestroyExitVfxKey other)
            {
                return TickIndex == other.TickIndex &&
                       EntityId == other.EntityId &&
                       PresentationSeed == other.PresentationSeed;
            }

            public override bool Equals(object obj)
            {
                return obj is DelayedBoxDestroyExitVfxKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(TickIndex, EntityId, PresentationSeed);
            }

            public static DelayedBoxDestroyExitVfxKey Create(
                int tickIndex,
                in TickEntityExitPresentationSignal signal)
            {
                return new DelayedBoxDestroyExitVfxKey(
                    tickIndex,
                    signal.ExitedEntityId,
                    signal.PresentationSeed);
            }
        }

        private readonly struct DelayedBoxDestroyExitVfx
        {
            public DelayedBoxDestroyExitVfx(
                int tickIndex,
                TickEntityExitPresentationSignal signal,
                float remainingSeconds,
                bool playSmoke,
                bool playShrink,
                GameplayTimingProfile timingProfile)
            {
                TickIndex = tickIndex;
                Signal = signal;
                RemainingSeconds = Mathf.Max(0f, remainingSeconds);
                PlaySmoke = playSmoke;
                PlayShrink = playShrink;
                TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            }

            public int TickIndex { get; }

            public TickEntityExitPresentationSignal Signal { get; }

            public float RemainingSeconds { get; }

            public bool PlaySmoke { get; }

            public bool PlayShrink { get; }

            public GameplayTimingProfile TimingProfile { get; }

            public DelayedBoxDestroyExitVfx Advance(float deltaTime)
            {
                return new DelayedBoxDestroyExitVfx(
                    TickIndex,
                    Signal,
                    RemainingSeconds - Mathf.Max(0f, deltaTime),
                    PlaySmoke,
                    PlayShrink,
                    TimingProfile);
            }
        }

        private sealed class AuthoringPrefabProvider : IVfxPrefabProvider
        {
            private readonly EnemyPresentationVfxProfileProvider enemyProfileProvider;
            private readonly VfxCueMapAsset hostDefaultMap;
            private readonly VfxProfileAsset[] profiles;

            public AuthoringPrefabProvider(
                EnemyPresentationVfxProfileProvider enemyProfileProvider,
                VfxCueMapAsset hostDefaultMap,
                VfxProfileAsset[] profiles)
            {
                this.enemyProfileProvider = enemyProfileProvider;
                this.hostDefaultMap = hostDefaultMap;
                this.profiles = profiles ?? Array.Empty<VfxProfileAsset>();
            }

            public bool TryResolvePrefab(in ResolvedVfxPlaybackCommand command, out GameObject prefab)
            {
                if (enemyProfileProvider != null &&
                    enemyProfileProvider.TryResolveProfileAssetForSourceEntity(
                        command.Request.SourceEntityId,
                        out var sourceProfile) &&
                    sourceProfile.TryResolvePrefab(command.CueId, out prefab))
                {
                    return true;
                }

                for (var i = 0; i < profiles.Length; i++)
                {
                    var profile = profiles[i];
                    if (profile != null &&
                        profile.TryResolvePrefab(command.CueId, out prefab))
                    {
                        return true;
                    }
                }

                if (hostDefaultMap != null &&
                    hostDefaultMap.TryResolvePrefab(command.CueId, out prefab))
                {
                    return true;
                }

                prefab = null;
                return false;
            }
        }
    }
}
