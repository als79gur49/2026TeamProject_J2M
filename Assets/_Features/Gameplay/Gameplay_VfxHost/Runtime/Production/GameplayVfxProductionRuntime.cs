using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx.Authoring;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxProductionRuntime : MonoBehaviour, IGameplayTickPresentationExtension, IGameplayInitialPresentationExtension, IGameplayOutputCameraPresentationExtension, IGameplayPresentationMotionVfxExtension, IGameplayTopologyTransitionCompletionPresentationExtension, IGameplayPresentationPausable
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
        [SerializeField] private bool enableGameplayVfxEnemyUtilityCooldownAura = true;
        [SerializeField] private bool enableGameplayVfxEnemyAttackCooldownFollow = true;
        [SerializeField] private bool enableGameplayVfxBoxSlideTrail = true;
        [SerializeField] private bool enableGameplayVfxBoxSlideSolidStop = true;
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
        [SerializeField] private bool enableGameplayVfxForwardCellProjectile = true;
        [SerializeField] private VfxProfileAsset[] familyProfiles = Array.Empty<VfxProfileAsset>();
        [SerializeField] private GameObject commonEmptyHostPrefab;

        private readonly PlayerVfxRequestPlanner playerPlanner = new();
        private readonly BoxVfxRequestPlanner boxPlanner = new();
        private readonly FlipImpactBurstVfxRequestPlanner flipImpactBurstPlanner = new();
        private readonly EnemyVfxRequestPlanner enemyPlanner = new();
        private readonly TileFeatureVfxRequestPlanner tileFeaturePlanner = new();
        private readonly GravityFieldVfxRequestPlanner gravityFieldPlanner = new();
        private readonly GameplayVfxRequestPlanBuilder planBuilder = new();
        private readonly HashSet<FlipDestroySelfMotionInstanceKey> playedFlipDestroySelfMotionKeys = new();
        private readonly HashSet<int> playedBoxSlideSolidStopKeys = new();
        private readonly HashSet<ImpactTransientBreakInstanceKey> playedImpactTransientBreakKeys = new();
        private readonly HashSet<OutOfBoundsExitInstanceKey> playedOutOfBoundsExitKeys = new();
        private readonly HashSet<DelayedBoxDestroyExitVfxKey> scheduledDelayedBoxDestroyExitVfxKeys = new();
        private readonly List<DelayedBoxDestroyExitVfx> pendingDelayedBoxDestroyExitVfx = new();
        private readonly List<DelayedBoxDestroyExitVfx> readyDelayedBoxDestroyExitVfx = new();
        private readonly HashSet<DelayedEnemyDeathMotionVfxKey> scheduledDelayedEnemyDeathMotionVfxKeys = new();
        private readonly List<DelayedEnemyDeathMotionVfx> pendingDelayedEnemyDeathMotionVfx = new();
        private readonly List<DelayedEnemyDeathMotionVfx> readyDelayedEnemyDeathMotionVfx = new();
        private readonly Dictionary<int, GameplayVfxEntityVisibilityState> visibilityEntityStates = new();
        private readonly EnemyMotionAttachedVfxFollowerPlanner enemyMotionAttachedFollowerPlanner = new();
        private readonly PresentationMotionFollowingVfxController motionFollowingVfxController = new();
        private readonly GameplayForwardCellProjectileVfxController forwardCellProjectileVfxController = new();

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
        private bool hasConfiguredEnemyPresentationProfiles;
        private VfxCueMapAsset hostDefaultCueMap;
        private int flipDestroySelfMotionMissingBindingCount;
        private int flipImpactStayTrailMissingBindingCount;
        private int flipImpactStayTrailMissingOwnerViewCount;
        private int enemyMotionAttachedMissingBindingCount;
        private int enemyMotionAttachedMissingOwnerViewCount;
        private int boxSlideSolidStopMissingBindingCount;
        private int boxSlideSolidStopMissingAnchorCount;
        private int boxDestroyShrinkMissingBindingCount;
        private int boxDestroyShrinkMissingAnchorCount;
        private int impactTransientBreakMissingBindingCount;
        private int impactTransientBreakMissingAnchorCount;
        private int outOfBoundsExitMissingBindingCount;
        private int outOfBoundsExitMissingAnchorCount;
        private int enemyDeathMotionMissingBindingCount;
        private int enemyDeathMotionMissingAnchorCount;
        private int mapNotConfiguredCount;
        private int initialRequestSkippedBecauseMapNotConfiguredCount;
        private int lastInitialPlannedRequestCount;
        private int lastInitialEntranceSpawnRequestCount;
        private int lastInitialActiveEntranceSpawnInstanceCount;
        private string lastMapNotConfiguredContext = string.Empty;
        private Camera outputCamera;
        private Transform localSpaceRoot;
        private bool isTopologyTransitionVfxSuppressed;
        private bool isPresentationPaused;
        private int topologyTransitionSuppressEpoch;
        private const float TopologyTransitionSoftSpawnDelaySeconds = 0.12f;

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
                if (!value)
                {
                    StopAttachedFollowerCue(GameplayVfxCueId.From(EnemyVfxCue.JumperWindupLoop), tail: true);
                }

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
                        visibilityContext: default,
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
                    StopAttachedFollowerCue(GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail), tail: true);
                    StopAttachedFollowerCue(GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop), tail: true);
                    StopAttachedFollowerCue(GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop), tail: true);
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

        public bool EnableGameplayVfxEnemyUtilityCooldownAura
        {
            get => enableGameplayVfxEnemyUtilityCooldownAura;
            set
            {
                if (enableGameplayVfxEnemyUtilityCooldownAura == value)
                {
                    return;
                }

                enableGameplayVfxEnemyUtilityCooldownAura = value;
                if (!value)
                {
                    StopAttachedFollowerCue(GameplayVfxCueId.From(EnemyVfxCue.UtilityCooldownAura), tail: true);
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
                if (!value)
                {
                    StopAttachedFollowerCue(GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop), tail: true);
                }

                ResetIfNoGameplayVfxEnabled();
            }
        }

        public bool EnableGameplayVfxBoxSlideSolidStop
        {
            get => enableGameplayVfxBoxSlideSolidStop;
            set
            {
                if (enableGameplayVfxBoxSlideSolidStop == value)
                {
                    return;
                }

                enableGameplayVfxBoxSlideSolidStop = value;
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

        public GameplayVfxVisibilityBlockReason LastVisibilityBlockReason =>
            controller?.LastVisibilityBlockReason ?? GameplayVfxVisibilityBlockReason.None;

        internal int ActiveForwardCellProjectileMarkerCount =>
            forwardCellProjectileVfxController.ActiveMarkerCount;

        internal int ActiveForwardCellProjectileFlightCount =>
            forwardCellProjectileVfxController.ActiveFlightCount;

        internal int[] ActiveForwardCellProjectileMarkerKeys =>
            forwardCellProjectileVfxController.ActiveMarkerKeys;

        internal int[] ActiveForwardCellProjectileFlightKeys =>
            forwardCellProjectileVfxController.ActiveFlightKeys;

        internal int PendingDelayedSpecialVfxCount =>
            scheduledDelayedBoxDestroyExitVfxKeys.Count +
            pendingDelayedBoxDestroyExitVfx.Count +
            readyDelayedBoxDestroyExitVfx.Count +
            scheduledDelayedEnemyDeathMotionVfxKeys.Count +
            pendingDelayedEnemyDeathMotionVfx.Count +
            readyDelayedEnemyDeathMotionVfx.Count;

        internal int GetActiveVfxInstanceCount(GameplayVfxCueId cueId)
        {
            return pool?.GetActiveCount(cueId) ?? 0;
        }

        internal int GetReleaseToPoolCount(GameplayVfxCueId cueId)
        {
            return pool?.GetReleaseToPoolCount(cueId) ?? 0;
        }

        public int MissingBindingCount =>
            (controller?.MissingBindingCount ?? 0) +
            flipDestroySelfMotionMissingBindingCount +
            flipImpactStayTrailMissingBindingCount +
            enemyMotionAttachedMissingBindingCount +
            boxDestroyShrinkMissingBindingCount +
            impactTransientBreakMissingBindingCount +
            outOfBoundsExitMissingBindingCount +
            enemyDeathMotionMissingBindingCount +
            forwardCellProjectileVfxController.MissingBindingCount;

        public int MissingAnchorCount =>
            (controller?.MissingAnchorCount ?? 0) +
            flipImpactStayTrailMissingOwnerViewCount +
            enemyMotionAttachedMissingOwnerViewCount +
            boxDestroyShrinkMissingAnchorCount +
            impactTransientBreakMissingAnchorCount +
            outOfBoundsExitMissingAnchorCount +
            enemyDeathMotionMissingAnchorCount +
            forwardCellProjectileVfxController.MissingAnchorCount;

        public int MissingPrefabCount => pool?.MissingPrefabCount ?? 0;

        public int MissingSourceViewCount => pool?.MissingSourceViewCount ?? 0;

        public int CommonHostUnavailableCount => pool?.CommonHostUnavailableCount ?? 0;

        public int InvalidPlaybackModePolicyCount => pool?.InvalidPlaybackModePolicyCount ?? 0;

        public bool IsRuntimeInitialized => controller != null;

        public bool IsHostDefaultMapConfigured => hostDefaultCueMap != null;

        public int MapNotConfiguredCount => mapNotConfiguredCount;

        public int InitialRequestSkippedBecauseMapNotConfiguredCount =>
            initialRequestSkippedBecauseMapNotConfiguredCount;

        public int LastInitialPlannedRequestCount => lastInitialPlannedRequestCount;

        public int LastInitialEntranceSpawnRequestCount => lastInitialEntranceSpawnRequestCount;

        public int LastInitialActiveEntranceSpawnInstanceCount => lastInitialActiveEntranceSpawnInstanceCount;

        public string LastMapNotConfiguredContext => lastMapNotConfiguredContext;

        public GameplayVfxCleanupReason LastCleanupReason { get; private set; }

        public GameplayVfxCleanupScope LastCleanupScope { get; private set; }

        public int HardCleanupAllCount { get; private set; }

        public int EnemyProfileFirstConfigureCount { get; private set; }

        public int EnemyProfileChangedCleanupCount { get; private set; }

        public int TileFeatureHardCleanupCount { get; private set; }

        public void ConfigureHostDefaultMap(VfxCueMapAsset cueMap)
        {
            if (hostDefaultCueMap == cueMap)
            {
                return;
            }

            hostDefaultCueMap = cueMap;
            RebuildBindingRuntime();
            ResetRuntimeComposition(GameplayVfxCleanupReason.HostDefaultMapReconfigured);
        }

        public void ConfigureCommonEmptyHostPrefab(GameObject prefab)
        {
            if (commonEmptyHostPrefab == prefab)
            {
                return;
            }

            commonEmptyHostPrefab = prefab;
            RebuildBindingRuntime();
            ApplyBindingRuntimeToExistingComposition();
        }

        public void ConfigureFamilyProfiles(VfxProfileAsset[] profiles)
        {
            familyProfiles = profiles ?? Array.Empty<VfxProfileAsset>();
            RebuildBindingRuntime();
            ResetRuntimeComposition(GameplayVfxCleanupReason.FamilyProfilesReconfigured);
        }

        public bool EnableGameplayVfxForwardCellProjectile
        {
            get => enableGameplayVfxForwardCellProjectile;
            set
            {
                if (enableGameplayVfxForwardCellProjectile == value)
                {
                    return;
                }

                enableGameplayVfxForwardCellProjectile = value;
                if (!value)
                {
                    forwardCellProjectileVfxController.HardCleanup(pool);
                }

                ResetIfNoGameplayVfxEnabled();
            }
        }

        public void ResetSession()
        {
            isPresentationPaused = false;
            LastPlannedRequestCount = 0;
            isTopologyTransitionVfxSuppressed = false;
            topologyTransitionSuppressEpoch = 0;
            flipDestroySelfMotionMissingBindingCount = 0;
            flipImpactStayTrailMissingBindingCount = 0;
            flipImpactStayTrailMissingOwnerViewCount = 0;
            enemyMotionAttachedMissingBindingCount = 0;
            enemyMotionAttachedMissingOwnerViewCount = 0;
            boxDestroyShrinkMissingBindingCount = 0;
            boxDestroyShrinkMissingAnchorCount = 0;
            impactTransientBreakMissingBindingCount = 0;
            impactTransientBreakMissingAnchorCount = 0;
            outOfBoundsExitMissingBindingCount = 0;
            outOfBoundsExitMissingAnchorCount = 0;
            enemyDeathMotionMissingBindingCount = 0;
            enemyDeathMotionMissingAnchorCount = 0;
            mapNotConfiguredCount = 0;
            initialRequestSkippedBecauseMapNotConfiguredCount = 0;
            lastInitialPlannedRequestCount = 0;
            lastInitialEntranceSpawnRequestCount = 0;
            lastInitialActiveEntranceSpawnInstanceCount = 0;
            lastMapNotConfiguredContext = string.Empty;
            playedFlipDestroySelfMotionKeys.Clear();
            playedBoxSlideSolidStopKeys.Clear();
            playedImpactTransientBreakKeys.Clear();
            playedOutOfBoundsExitKeys.Clear();
            scheduledDelayedBoxDestroyExitVfxKeys.Clear();
            pendingDelayedBoxDestroyExitVfx.Clear();
            readyDelayedBoxDestroyExitVfx.Clear();
            scheduledDelayedEnemyDeathMotionVfxKeys.Clear();
            pendingDelayedEnemyDeathMotionVfx.Clear();
            readyDelayedEnemyDeathMotionVfx.Clear();
            enemyMotionAttachedFollowerPlanner.Clear();
            motionFollowingVfxController.ResetSession();
            forwardCellProjectileVfxController.ResetSession(pool);
            RecordCleanup(GameplayVfxCleanupReason.SessionReset, GameplayVfxCleanupScope.AllFamilies);
            controller?.HardCleanupAll();
            planBuilder.Clear();
        }

        public void ConfigureOutputCamera(Camera outputCamera, Transform localSpaceRoot)
        {
            this.outputCamera = outputCamera;
            this.localSpaceRoot = localSpaceRoot;
        }

        public void PresentInitial(in GameplayInitialPresentationExtensionContext context)
        {
            LastPlannedRequestCount = 0;
            lastInitialPlannedRequestCount = 0;
            lastInitialEntranceSpawnRequestCount = 0;
            lastInitialActiveEntranceSpawnInstanceCount = 0;
            if (!AnyGameplayVfxEnabled ||
                context.PresentationData == null)
            {
                return;
            }

            var visibilityContext = BuildVisibilityContext(context.StateStore);
            planBuilder.Clear();
            var planningContext = GameplayVfxPlanningContext.ForInitial(
                context.PresentationData,
                context.Topology,
                context.TimingProfile,
                context.TileFeatureVfxStyleBindings,
                visibilityContext);
            if (enableGameplayVfxTileFeatureLane)
            {
                tileFeaturePlanner.Plan(planningContext, planBuilder);
            }

            var enabledPlan = FilterByEnabledCues(planBuilder.Build());
            lastInitialPlannedRequestCount = enabledPlan.Requests.Count;
            lastInitialEntranceSpawnRequestCount = CountEntranceSpawnRequests(enabledPlan);
            if (enabledPlan.Requests.Count == 0)
            {
                controller?.Refresh(GameplayVfxRequestPlan.Empty);
                return;
            }

            if (!IsHostDefaultMapConfigured)
            {
                RecordMapNotConfigured(
                    "PresentInitial",
                    enabledPlan.Requests.Count,
                    lastInitialEntranceSpawnRequestCount);
                LastPlannedRequestCount = enabledPlan.Requests.Count;
                return;
            }

            var plan = FilterByPlanningVisibility(
                enabledPlan,
                bindingResolver,
                visibilityContext);
            if (plan.Requests.Count == 0)
            {
                controller?.Refresh(GameplayVfxRequestPlan.Empty);
                return;
            }

            EnsureRuntime(context.Projector, context.StateStore);
            controller.SetVisibilityContext(visibilityContext);
            controller.Refresh(plan);
            LastPlannedRequestCount = plan.Requests.Count;
            lastInitialActiveEntranceSpawnInstanceCount =
                GetActiveVfxInstanceCount(GameplayVfxCueIds.EntranceSpawn);
        }

        private void RecordMapNotConfigured(
            string context,
            int requestCount,
            int entranceSpawnRequestCount)
        {
            mapNotConfiguredCount++;
            initialRequestSkippedBecauseMapNotConfiguredCount += requestCount;
            lastMapNotConfiguredContext = context ?? string.Empty;
            UnityEngine.Debug.LogWarning(
                $"{nameof(GameplayVfxProductionRuntime)} skipped {requestCount} initial VFX request(s) because the host default cue map is not configured. Context='{lastMapNotConfiguredContext}', EntranceSpawnRequests={entranceSpawnRequestCount}.",
                this);
        }

        private static int CountEntranceSpawnRequests(GameplayVfxRequestPlan plan)
        {
            if (plan == null || plan.Requests.Count == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < plan.Requests.Count; i++)
            {
                if (plan.Requests[i].CueId == GameplayVfxCueIds.EntranceSpawn)
                {
                    count++;
                }
            }

            return count;
        }
        public void Present(in GameplayTickPresentationExtensionContext context)
        {
            LastPlannedRequestCount = 0;
            if (IsTopologyTransitionStart(context))
            {
                LogForwardCellProjectileProductionGate(
                    context,
                    controllerWillRun: false,
                    "TopologyTransitionStart");
                ClearGameplayVfxForTopologyTransitionStart(context.TopologyTransitionEpoch);
                return;
            }

            if (isTopologyTransitionVfxSuppressed)
            {
                LogForwardCellProjectileProductionGate(
                    context,
                    controllerWillRun: false,
                    "TopologyTransitionSuppressed");
                return;
            }

            if (!AnyGameplayVfxEnabled)
            {
                LogForwardCellProjectileProductionGate(
                    context,
                    controllerWillRun: false,
                    "AllGameplayVfxDisabled");
                enemyMotionAttachedFollowerPlanner.Clear();
                return;
            }

            ConfigureEnemyPresentationProfiles(
                context.EnemyPresentationCatalog,
                context.EnemyPresentationBindings);
            var visibilityContext = BuildVisibilityContext(
                context.StateStore,
                context.Result?.PresentationData,
                context.Topology);
            enemyMotionAttachedFollowerPlanner.Build(
                context.Result.TickIndex,
                context.Result.PresentationData,
                enableGameplayVfxGlideWindTrail,
                enableGameplayVfxChargeBoosterTrail,
                enableGameplayVfxBoxSlideTrail,
                enableEnemyJumpTargetVfx,
                context.Result.FinalEntities,
                context.StateStore?.ViewsByEntityId,
                enableGameplayVfxEnemyUtilityCooldownAura,
                enableGameplayVfxEnemyAttackCooldownFollow);
            planBuilder.Clear();
            var planningContext = GameplayVfxPlanningContext.ForTick(
                context.Result.TickIndex,
                context.Result.PresentationData,
                context.Topology,
                context.TimingProfile,
                context.TileFeatureVfxStyleBindings,
                visibilityContext,
                BuildTopologyTransitionContext(context));
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

            var plan = FilterByPlanningVisibility(
                FilterByEnabledCues(planBuilder.Build()),
                bindingResolver,
                visibilityContext);
            var shouldPlayFlipDestroySelfMotion =
                enableGameplayVfxFlipDestroySelfMotionMigration &&
                HasDestroySelfFlipImpactSignal(context.Result.PresentationData);
            var shouldPlayBoxSlideSolidStop =
                enableGameplayVfxBoxSlideSolidStop &&
                HasBoxSlideSolidStopSignal(context.Result.PresentationData);
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
            var shouldPlayForwardCellProjectile =
                enableGameplayVfxForwardCellProjectile &&
                HasForwardCellProjectileSignal(context.Result.PresentationData);
            LogForwardCellProjectileProductionGate(
                context,
                shouldPlayForwardCellProjectile,
                shouldPlayForwardCellProjectile
                    ? ResolveForwardCellProjectileProductionGateReason(context.Result.PresentationData)
                    : "GateFalse");
            if (plan.Requests.Count == 0 &&
                !shouldPlayFlipDestroySelfMotion &&
                !shouldPlayBoxSlideSolidStop &&
                !shouldPlayBoxDestroyShrink &&
                !shouldScheduleAfterEntityMotionBoxDestroyExit &&
                !shouldPlayImpactTransientBreak &&
                !shouldPlayOutOfBoundsExit &&
                !shouldPlayEnemyDeathMotion &&
                !shouldPlayForwardCellProjectile)
            {
                controller?.Refresh(
                    GameplayVfxRequestPlan.Empty,
                    ResolveRefreshOptions(context));
                return;
            }

            EnsureRuntime(context);
            controller.SetVisibilityContext(visibilityContext);
            controller.Refresh(plan, ResolveRefreshOptions(context));
            var flipDestroySelfMotionCommandCount = shouldPlayFlipDestroySelfMotion
                ? PlayFlipDestroySelfMotionCommands(context)
                : 0;
            var boxSlideSolidStopCommandCount = shouldPlayBoxSlideSolidStop
                ? PlayBoxSlideSolidStopCommands(context)
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
            if (shouldPlayForwardCellProjectile)
            {
                forwardCellProjectileVfxController.Present(context, pool, bindingResolver, visibilityContext);
                LogForwardCellProjectileProductionGate(
                    context,
                    controllerWillRun: true,
                    "ControllerExecuted");
            }

            var forwardCellProjectileCommandCount = shouldPlayForwardCellProjectile
                ? forwardCellProjectileVfxController.PlayedThisTickCount
                : 0;
            LastPlannedRequestCount = plan.Requests.Count +
                                      flipDestroySelfMotionCommandCount +
                                      boxSlideSolidStopCommandCount +
                                      boxDestroyShrinkCommandCount +
                                      delayedBoxDestroyExitVfxCount +
                                      impactTransientBreakCommandCount +
                                      outOfBoundsExitCommandCount +
                                      enemyDeathMotionCommandCount +
                                      forwardCellProjectileCommandCount;
        }

        public void ReconcileTopologyTransitionCompleted(in GameplayTickPresentationExtensionContext context)
        {
            LastPlannedRequestCount = 0;
            EndTopologyTransitionSuppression();
            if (!AnyGameplayVfxEnabled ||
                context.Result == null ||
                context.Result.PresentationData == null)
            {
                return;
            }

            var visibilityContext = BuildVisibilityContext(
                context.StateStore,
                context.Result?.PresentationData,
                context.Topology);
            planBuilder.Clear();
            var planningContext = GameplayVfxPlanningContext.ForTick(
                context.Result.TickIndex,
                context.Result.PresentationData,
                context.Topology,
                context.TimingProfile,
                context.TileFeatureVfxStyleBindings,
                visibilityContext,
                BuildTopologyTransitionContext(context));
            playerPlanner.Plan(planningContext, planBuilder);
            boxPlanner.Plan(planningContext, planBuilder);
            flipImpactBurstPlanner.Plan(planningContext, planBuilder);
            enemyPlanner.Plan(planningContext, planBuilder);
            if (enableGameplayVfxTileFeatureLane)
            {
                tileFeaturePlanner.PlanPersistentLoops(planningContext, planBuilder);
            }

            if (enableGameplayVfxGravityFieldEvents ||
                enableGameplayVfxGravityFieldContinuous ||
                enableGameplayVfxGravityFieldLockedTarget)
            {
                gravityFieldPlanner.Plan(planningContext, planBuilder);
            }

            var enabledPlan = FilterByEnabledCues(planBuilder.Build());
            var visibilityFilteredPlan = FilterByPlanningVisibility(
                enabledPlan,
                bindingResolver,
                visibilityContext);
            var plan = AddTopologyTransitionSoftSpawnDelay(FilterPersistentOnly(visibilityFilteredPlan));
            if (plan.Requests.Count == 0 && controller == null)
            {
                return;
            }

            EnsureRuntime(context);
            controller.SetVisibilityContext(visibilityContext);
            controller.ValidatePendingTopologyTransitionVisibility(enabledPlan);
            controller.Refresh(plan, GameplayVfxRefreshOptions.TopologyTransitionCompletion());
            LastPlannedRequestCount = plan.Requests.Count;
        }

        public void UpdatePresentation(float deltaTime)
        {
            if (isPresentationPaused)
            {
                return;
            }

            if (isTopologyTransitionVfxSuppressed)
            {
                return;
            }

            AdvanceDelayedBoxDestroyExitVfx(deltaTime);
            AdvanceDelayedEnemyDeathMotionVfx(deltaTime);
            forwardCellProjectileVfxController.Update(deltaTime, pool);
            controller?.Update(deltaTime);
            pool?.Advance(deltaTime);
        }

        public void RefreshPresentationMotionVfx(in GameplayPresentationMotionVfxContext context)
        {
            if (isTopologyTransitionVfxSuppressed)
            {
                motionFollowingVfxController.ClearForTopologyTransitionStart(pool);
                return;
            }

            if (!AnyGameplayVfxEnabled)
            {
                enemyMotionAttachedFollowerPlanner.Clear();
                return;
            }

            EnsureRuntime(
                context.Projector,
                context.StateStore);
            var visibilityContext = BuildVisibilityContext(context.StateStore);
            motionFollowingVfxController.Refresh(
                context.TickIndex,
                context.TrackState as GameplayPresentationTrackState,
                context.StateStore,
                pool,
                bindingResolver,
                enabled: enableGameplayVfxFlipImpactStayTrail,
                visibilityContext: visibilityContext,
                attachedDesiredStates: enemyMotionAttachedFollowerPlanner.DesiredFollowers,
                explicitAttachedStopStates: enemyMotionAttachedFollowerPlanner.ExplicitStopKeys,
                attachedFollowersEnabled: enableGameplayVfxGlideWindTrail ||
                                          enableGameplayVfxChargeBoosterTrail ||
                                          enableGameplayVfxEnemyUtilityCooldownAura ||
                                          enableGameplayVfxEnemyAttackCooldownFollow ||
                                          enableGameplayVfxBoxSlideTrail ||
                                          enableEnemyJumpTargetVfx);
            flipImpactStayTrailMissingBindingCount = motionFollowingVfxController.MotionMissingBindingCount;
            flipImpactStayTrailMissingOwnerViewCount = motionFollowingVfxController.MotionMissingOwnerViewCount;
            enemyMotionAttachedMissingBindingCount = motionFollowingVfxController.AttachedMissingBindingCount;
            enemyMotionAttachedMissingOwnerViewCount = motionFollowingVfxController.AttachedMissingOwnerViewCount;
            PlayReadyDelayedBoxDestroyExitVfx();
            PlayReadyDelayedEnemyDeathMotionVfx();
        }

        public void HardCleanup()
        {
            isPresentationPaused = false;
            isTopologyTransitionVfxSuppressed = false;
            topologyTransitionSuppressEpoch = 0;
            motionFollowingVfxController.HardCleanup();
            forwardCellProjectileVfxController.HardCleanup(pool);
            RecordCleanup(GameplayVfxCleanupReason.ManualHardCleanup, GameplayVfxCleanupScope.AllFamilies);
            controller?.HardCleanupAll();
            LastPlannedRequestCount = 0;
            playedFlipDestroySelfMotionKeys.Clear();
            playedImpactTransientBreakKeys.Clear();
            playedOutOfBoundsExitKeys.Clear();
            scheduledDelayedBoxDestroyExitVfxKeys.Clear();
            pendingDelayedBoxDestroyExitVfx.Clear();
            readyDelayedBoxDestroyExitVfx.Clear();
            scheduledDelayedEnemyDeathMotionVfxKeys.Clear();
            pendingDelayedEnemyDeathMotionVfx.Clear();
            readyDelayedEnemyDeathMotionVfx.Clear();
            enemyMotionAttachedFollowerPlanner.Clear();
        }

        public void SetPresentationPaused(bool paused)
        {
            if (isPresentationPaused == paused)
            {
                return;
            }

            isPresentationPaused = paused;
            if (paused)
            {
                controller?.SuspendPresentation(VfxPresentationSuspendReason.GameplayPause);
                pool?.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);
                return;
            }

            controller?.ResumePresentation(VfxPresentationSuspendReason.GameplayPause);
            pool?.ResumeActivePresentation(VfxPresentationSuspendReason.GameplayPause);
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
            RecordCleanup(GameplayVfxCleanupReason.ProjectorOrStateStoreChanged, GameplayVfxCleanupScope.AllFamilies);
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
            if (isPresentationPaused)
            {
                controller.SuspendPresentation(VfxPresentationSuspendReason.GameplayPause);
                pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);
            }
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
                familyProfiles,
                commonEmptyHostPrefab);
        }

        private void ApplyBindingRuntimeToExistingComposition()
        {
            controller?.ConfigureBindingResolver(bindingResolver);
            pool?.ConfigurePrefabProvider(prefabProvider);
        }

        private bool ConfigureEnemyPresentationProfiles(
            EnemyPresentationCatalog catalog,
            EnemyPresentationBinding[] bindings)
        {
            var resolvedBindings = bindings ?? Array.Empty<EnemyPresentationBinding>();
            var catalogMatches = ReferenceEquals(configuredEnemyPresentationCatalog, catalog);
            var bindingsMatch = ReferenceEquals(configuredEnemyPresentationBindings, resolvedBindings);
            if (!hasConfiguredEnemyPresentationProfiles)
            {
                configuredEnemyPresentationCatalog = catalog;
                configuredEnemyPresentationBindings = resolvedBindings;
                hasConfiguredEnemyPresentationProfiles = true;
                enemyPresentationVfxProfileProvider = EnemyPresentationVfxProfileMapBuilder.Build(
                    configuredEnemyPresentationCatalog,
                    configuredEnemyPresentationBindings,
                    nameof(GameplayVfxProductionRuntime));
                RebuildBindingRuntime();
                ApplyBindingRuntimeToExistingComposition();
                EnemyProfileFirstConfigureCount++;
                return false;
            }

            if (catalogMatches && bindingsMatch)
            {
                return false;
            }

            configuredEnemyPresentationCatalog = catalog;
            configuredEnemyPresentationBindings = resolvedBindings;
            enemyPresentationVfxProfileProvider = EnemyPresentationVfxProfileMapBuilder.Build(
                configuredEnemyPresentationCatalog,
                configuredEnemyPresentationBindings,
                nameof(GameplayVfxProductionRuntime));
            RebuildBindingRuntime();
            ApplyBindingRuntimeToExistingComposition();
            ResetEnemyPresentationRuntimeComposition(GameplayVfxCleanupReason.EnemyProfileChanged);
            return true;
        }

        private void ResetEnemyPresentationRuntimeComposition(GameplayVfxCleanupReason reason)
        {
            RecordCleanup(reason, GameplayVfxCleanupScope.EnemyFamily);
            motionFollowingVfxController.HardCleanupFamily(GameplayVfxFamily.Enemy);
            enemyMotionAttachedFollowerPlanner.Clear();
            controller?.HardCleanupFamily(GameplayVfxFamily.Enemy, reason);
        }

        private void ResetRuntimeComposition(GameplayVfxCleanupReason reason)
        {
            RecordCleanup(reason, GameplayVfxCleanupScope.AllFamilies);
            isTopologyTransitionVfxSuppressed = false;
            topologyTransitionSuppressEpoch = 0;
            motionFollowingVfxController.HardCleanup();
            forwardCellProjectileVfxController.HardCleanup(pool);
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
            enableGameplayVfxEnemyUtilityCooldownAura ||
            enableGameplayVfxEnemyAttackCooldownFollow ||
            enableGameplayVfxBoxSlideTrail ||
            enableGameplayVfxBoxSlideSolidStop ||
            enableGameplayVfxImpactTransientBreakMigration ||
            enableGameplayVfxOutOfBoundsExitMigration ||
            enableGameplayVfxUtilityWindupMigration ||
            enableGameplayVfxFrontFaceShieldActiveMigration ||
            enableGameplayVfxFrontFaceShieldBlockMigration ||
            enableGameplayVfxFrontFaceShieldWindupMigration ||
            enableGameplayVfxTileFeatureLane ||
            enableGameplayVfxGravityFieldEvents ||
            enableGameplayVfxGravityFieldContinuous ||
            enableGameplayVfxGravityFieldLockedTarget ||
            enableGameplayVfxForwardCellProjectile;

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
            ResetRuntimeComposition(GameplayVfxCleanupReason.AllGameplayVfxDisabled);
        }

        private void RecordCleanup(GameplayVfxCleanupReason reason, GameplayVfxCleanupScope scope)
        {
            if (scope == GameplayVfxCleanupScope.AllFamilies &&
                controller == null &&
                pool == null)
            {
                return;
            }

            var activeTileFeaturesBefore = pool?.GetActiveCount(GameplayVfxCueIds.EntranceSpawn) ?? 0;
            LastCleanupReason = reason;
            LastCleanupScope = scope;
            if (scope == GameplayVfxCleanupScope.AllFamilies)
            {
                HardCleanupAllCount++;
                if (activeTileFeaturesBefore > 0)
                {
                    TileFeatureHardCleanupCount++;
                }
            }

            if (scope == GameplayVfxCleanupScope.EnemyFamily &&
                reason == GameplayVfxCleanupReason.EnemyProfileChanged)
            {
                EnemyProfileChangedCleanupCount++;
            }
        }

        private GameplayVfxVisibilityContext BuildVisibilityContext(
            GameplayPresentationStateStore stateStore,
            TickPresentationData presentationData = null,
            CubeTopologyState topology = default)
        {
            visibilityEntityStates.Clear();
            if (stateStore == null)
            {
                return new GameplayVfxVisibilityContext(visibilityEntityStates);
            }

            foreach (var pair in stateStore.ViewsByEntityId)
            {
                var entityId = pair.Key;
                var view = pair.Value;
                var hasSemanticState = stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(
                    entityId,
                    out var semanticState);
                var hasFacts = stateStore.EnemyVisualFactsByEntityId.TryGetValue(entityId, out var facts);
                visibilityEntityStates[entityId] = new GameplayVfxEntityVisibilityState(
                    hasView: view != null,
                    isViewActiveInHierarchy: view != null &&
                                             view.isActiveAndEnabled &&
                                             view.gameObject.activeInHierarchy,
                    hasSemanticState: hasSemanticState,
                    isFrontFaceInactive: hasSemanticState &&
                                         semanticState.ActivityState == EnemyVisualActivityState.FrontFaceInactive,
                    isGameplayAutonomySuppressed: hasFacts && facts.IsGameplayAutonomySuppressed,
                    isJumpTopologySuspended: IsJumpTopologySuspendedOwner(entityId, presentationData, topology));
            }

            foreach (var pair in stateStore.EnemyVisualSemanticStatesByEntityId)
            {
                var entityId = pair.Key;
                if (visibilityEntityStates.ContainsKey(entityId))
                {
                    continue;
                }

                var hasFacts = stateStore.EnemyVisualFactsByEntityId.TryGetValue(entityId, out var facts);
                visibilityEntityStates[entityId] = new GameplayVfxEntityVisibilityState(
                    hasView: false,
                    isViewActiveInHierarchy: false,
                    hasSemanticState: true,
                    isFrontFaceInactive: pair.Value.ActivityState == EnemyVisualActivityState.FrontFaceInactive,
                    isGameplayAutonomySuppressed: hasFacts && facts.IsGameplayAutonomySuppressed,
                    isJumpTopologySuspended: IsJumpTopologySuspendedOwner(entityId, presentationData, topology));
            }

            return new GameplayVfxVisibilityContext(visibilityEntityStates);
        }

        private static bool IsJumpTopologySuspendedOwner(
            int entityId,
            TickPresentationData presentationData,
            CubeTopologyState topology)
        {
            if (entityId <= 0 || presentationData == null)
            {
                return false;
            }

            var signals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId != entityId ||
                    signal.Phase != EnemyJumpPhase.Airborne)
                {
                    continue;
                }

                return signal.SourceCell.face != topology.BottomFace;
            }

            return false;
        }

        private static GameplayVfxTopologyTransitionContext BuildTopologyTransitionContext(
            in GameplayTickPresentationExtensionContext context)
        {
            var topologyMotion = context.Result?.PresentationData?.TopologyMotion;
            if (!topologyMotion.HasValue ||
                topologyMotion.Value.RotationKind == CubeRotationKind.None)
            {
                return GameplayVfxTopologyTransitionContext.None(context.Topology);
            }

            var motion = topologyMotion.Value;
            var isCompletion = context.IsTopologyTransitionCompletionReconcile;
            return new GameplayVfxTopologyTransitionContext(
                motion,
                motion.SourceTopology,
                motion.DestinationTopology,
                isCompletion ? context.Topology : motion.SourceTopology,
                context.TopologyTransitionEpoch,
                isTransitionStartTick: !isCompletion,
                isTransitionCompletionReconcile: isCompletion);
        }

        private static GameplayVfxRefreshOptions ResolveRefreshOptions(
            in GameplayTickPresentationExtensionContext context)
        {
            if (context.IsTopologyTransitionCompletionReconcile)
            {
                return GameplayVfxRefreshOptions.TopologyTransitionCompletion();
            }

            var topologyMotion = context.Result?.PresentationData?.TopologyMotion;
            if (topologyMotion.HasValue &&
                topologyMotion.Value.RotationKind != CubeRotationKind.None)
            {
                return GameplayVfxRefreshOptions.TopologyTransitionStart();
            }

            return default;
        }

        private void ClearGameplayVfxForTopologyTransitionStart(int epoch)
        {
            isTopologyTransitionVfxSuppressed = true;
            topologyTransitionSuppressEpoch = epoch;
            RecordCleanup(GameplayVfxCleanupReason.TopologyTransitionStarted, GameplayVfxCleanupScope.AllFamilies);
            controller?.ClearForTopologyTransitionStart(epoch);
            controller?.SetTopologyTransitionStartSuppression(true, epoch);
            motionFollowingVfxController.ClearForTopologyTransitionStart(pool);
            forwardCellProjectileVfxController.ClearForTopologyTransitionStart(pool);
            pool?.HardClearActiveForTopologyTransition();
            enemyMotionAttachedFollowerPlanner.Clear();
            scheduledDelayedBoxDestroyExitVfxKeys.Clear();
            pendingDelayedBoxDestroyExitVfx.Clear();
            readyDelayedBoxDestroyExitVfx.Clear();
            scheduledDelayedEnemyDeathMotionVfxKeys.Clear();
            pendingDelayedEnemyDeathMotionVfx.Clear();
            readyDelayedEnemyDeathMotionVfx.Clear();
            LastPlannedRequestCount = 0;
        }

        private void EndTopologyTransitionSuppression()
        {
            isTopologyTransitionVfxSuppressed = false;
            topologyTransitionSuppressEpoch = 0;
            controller?.SetTopologyTransitionStartSuppression(false, 0);
        }

        private static bool IsTopologyTransitionStart(in GameplayTickPresentationExtensionContext context)
        {
            if (context.IsTopologyTransitionCompletionReconcile)
            {
                return false;
            }

            var topologyMotion = context.Result?.PresentationData?.TopologyMotion;
            return topologyMotion.HasValue &&
                   topologyMotion.Value.RotationKind != CubeRotationKind.None;
        }

        private static GameplayVfxRequestPlan FilterPersistentOnly(GameplayVfxRequestPlan plan)
        {
            if (plan == null || plan.Requests.Count == 0)
            {
                return GameplayVfxRequestPlan.Empty;
            }

            var persistentRequests = new List<GameplayVfxRequest>(plan.Requests.Count);
            for (var i = 0; i < plan.Requests.Count; i++)
            {
                var request = plan.Requests[i];
                if (request.IsPersistent &&
                    request.CompletionReplayPolicy == GameplayVfxCompletionReplayPolicy.SteadyStatePersistentLoop)
                {
                    persistentRequests.Add(request);
                }
            }

            return persistentRequests.Count == 0
                ? GameplayVfxRequestPlan.Empty
                : new GameplayVfxRequestPlan(persistentRequests);
        }

        private static GameplayVfxRequestPlan AddTopologyTransitionSoftSpawnDelay(GameplayVfxRequestPlan plan)
        {
            if (plan == null || plan.Requests.Count == 0)
            {
                return GameplayVfxRequestPlan.Empty;
            }

            var delayedRequests = new List<GameplayVfxRequest>(plan.Requests.Count);
            for (var i = 0; i < plan.Requests.Count; i++)
            {
                delayedRequests.Add(plan.Requests[i].WithSoftSpawnDelay(TopologyTransitionSoftSpawnDelaySeconds));
            }

            return new GameplayVfxRequestPlan(delayedRequests);
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

        private static GameplayVfxRequestPlan FilterByPlanningVisibility(
            GameplayVfxRequestPlan plan,
            IVfxBindingResolver bindingResolver,
            in GameplayVfxVisibilityContext visibilityContext)
        {
            if (plan == null || plan.Requests.Count == 0)
            {
                return GameplayVfxRequestPlan.Empty;
            }

            var filteredRequests = new List<GameplayVfxRequest>(plan.Requests.Count);
            for (var i = 0; i < plan.Requests.Count; i++)
            {
                var request = plan.Requests[i];
                var decision = bindingResolver != null &&
                               bindingResolver.TryResolve(request, out var policy)
                    ? GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                        request,
                        policy,
                        visibilityContext)
                    : GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                        request,
                        GameplayVfxVisibilityMode.DefaultGameplay,
                        visibilityContext);
                if (decision.IsVisible ||
                    ShouldForwardVisibilityBlockedRequestToController(request, decision.BlockReason))
                {
                    filteredRequests.Add(request);
                }
            }

            return filteredRequests.Count == 0
                ? GameplayVfxRequestPlan.Empty
                : new GameplayVfxRequestPlan(filteredRequests);
        }

        private static bool ShouldForwardVisibilityBlockedRequestToController(
            in GameplayVfxRequest request,
            GameplayVfxVisibilityBlockReason blockReason)
        {
            return request.IsPersistent &&
                   request.CueId.Equals(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)) &&
                   (blockReason == GameplayVfxVisibilityBlockReason.JumpTopologySuspended ||
                    blockReason == GameplayVfxVisibilityBlockReason.JumpWindupSourceTopologyMismatch ||
                    blockReason == GameplayVfxVisibilityBlockReason.InactiveFace ||
                    blockReason == GameplayVfxVisibilityBlockReason.FrontFaceInactive ||
                    blockReason == GameplayVfxVisibilityBlockReason.EntityViewInactive ||
                    blockReason == GameplayVfxVisibilityBlockReason.MissingSemanticState);
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
                   (enableGameplayVfxUtilityWindupMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.GravityFieldAuraWindupArea)) ||
                   (enableGameplayVfxUtilityWindupMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.GravityFieldAuraActiveArea)) ||
                   (enableGameplayVfxUtilityWindupMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.GravityFieldAuraActiveStarted)) ||
                   (enableGameplayVfxBoxDestroySmokeMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.DestroySmoke)) ||
                   (enableGameplayVfxBoxDestroyShrinkMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.DestroyShrink)) ||
                   (enableGameplayVfxItemConsumeBurstMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.ItemConsume)) ||
                   (enableGameplayVfxFlipImpactBurstMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst)) ||
                   (enableGameplayVfxFlipDestroySelfMotionMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion)) ||
                   (enableGameplayVfxFlipImpactStayTrail && cueId == GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail)) ||
                   (enableGameplayVfxGlideWindTrail && cueId == GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail)) ||
                   (enableGameplayVfxGlideWindTrail && cueId == GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop)) ||
                   (enableGameplayVfxGlideWindTrail && cueId == GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop)) ||
                   (enableGameplayVfxChargeBoosterTrail && cueId == GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail)) ||
                   (enableGameplayVfxEnemyUtilityCooldownAura && cueId == GameplayVfxCueId.From(EnemyVfxCue.UtilityCooldownAura)) ||
                   (enableGameplayVfxEnemyAttackCooldownFollow && cueId == GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellAttackCooldownFollow)) ||
                   (enableGameplayVfxBoxSlideTrail && cueId == GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop)) ||
                   (enableGameplayVfxBoxSlideSolidStop && cueId == GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop)) ||
                   (enableGameplayVfxImpactTransientBreakMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak)) ||
                   (enableGameplayVfxOutOfBoundsExitMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.OutOfBoundsExit)) ||
                   (enableGameplayVfxOutOfBoundsExitMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit)) ||
                   (enableGameplayVfxTileFeatureLane && cueId.Family == GameplayVfxFamily.TileFeature) ||
                   IsGravityFieldCueEnabled(cueId) ||
                   (enableGameplayVfxUtilityWindupMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.UtilitySummonSpawn)) ||
                   (enableEnemyJumpTargetVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)) ||
                   (enableEnemyJumpTargetVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperJumpStart)) ||
                   (enableEnemyJumpTargetVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperWindupLoop)) ||
                   (enableEnemyJumpLandingDustVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust));
        }

        private void StopAttachedFollowerCue(GameplayVfxCueId cueId, bool tail)
        {
            enemyMotionAttachedFollowerPlanner.RemoveCue(cueId);
            motionFollowingVfxController.StopAttachedFollowersForCue(cueId, tail);
        }

        private bool IsGravityFieldCueEnabled(GameplayVfxCueId cueId)
        {
            if (cueId.Family != GameplayVfxFamily.GravityField)
            {
                return false;
            }

            if (enableGameplayVfxGravityFieldEvents &&
                (cueId == GameplayVfxCueId.From(GravityFieldVfxCue.ChargeStarted) ||
                 cueId == GameplayVfxCueId.From(GravityFieldVfxCue.ActiveStarted)))
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
                   cueId == GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop) ||
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

                if (signal.Timing == EntityExitPresentationTiming.AtContactTime &&
                    signal.VisualContactNormalizedTime > 0f)
                {
                    if (ScheduleDelayedEnemyDeathMotionVfx(
                            context.Result.TickIndex,
                            signal,
                            context.TimingProfile,
                            context.StateStore))
                    {
                        plannedCommandCount++;
                    }

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

        private bool ScheduleDelayedEnemyDeathMotionVfx(
            int tickIndex,
            in TickEntityExitPresentationSignal signal,
            GameplayTimingProfile timingProfile,
            GameplayPresentationStateStore stateStore)
        {
            var key = DelayedEnemyDeathMotionVfxKey.Create(tickIndex, signal);
            if (!scheduledDelayedEnemyDeathMotionVfxKeys.Add(key))
            {
                return false;
            }

            var resolvedTimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            var delaySeconds = resolvedTimingProfile.FlipMotionDurationSeconds * signal.VisualContactNormalizedTime;
            VfxRendererInactiveVisualSnapshotSet.TryCapture(
                stateStore,
                signal.ExitedEntityId,
                out var sourceVisualSnapshot);
            var delayed = new DelayedEnemyDeathMotionVfx(
                tickIndex,
                signal,
                resolvedTimingProfile,
                delaySeconds,
                sourceVisualSnapshot);
            if (delaySeconds <= 0.0001f)
            {
                PlayDelayedEnemyDeathMotionVfx(delayed);
                return true;
            }

            pendingDelayedEnemyDeathMotionVfx.Add(delayed);
            return true;
        }

        private void AdvanceDelayedEnemyDeathMotionVfx(float deltaTime)
        {
            if (pendingDelayedEnemyDeathMotionVfx.Count == 0)
            {
                return;
            }

            var advanceSeconds = Mathf.Max(0f, deltaTime);
            for (var i = pendingDelayedEnemyDeathMotionVfx.Count - 1; i >= 0; i--)
            {
                var pending = pendingDelayedEnemyDeathMotionVfx[i].Advance(advanceSeconds);
                if (pending.RemainingSeconds > 0.0001f)
                {
                    pendingDelayedEnemyDeathMotionVfx[i] = pending;
                    continue;
                }

                pendingDelayedEnemyDeathMotionVfx.RemoveAt(i);
                readyDelayedEnemyDeathMotionVfx.Add(pending);
            }
        }

        private void PlayReadyDelayedEnemyDeathMotionVfx()
        {
            if (readyDelayedEnemyDeathMotionVfx.Count == 0)
            {
                return;
            }

            for (var i = 0; i < readyDelayedEnemyDeathMotionVfx.Count; i++)
            {
                var delayed = readyDelayedEnemyDeathMotionVfx[i];
                PlayDelayedEnemyDeathMotionVfx(delayed);
            }

            readyDelayedEnemyDeathMotionVfx.Clear();
        }

        private void PlayDelayedEnemyDeathMotionVfx(in DelayedEnemyDeathMotionVfx delayed)
        {
            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(configuredStateStore, trackState);
            var targetResolver = new EnemyDeathMotionTargetResolver(
                localSpaceRoot,
                outputCamera,
                configuredStateStore,
                configuredProjector.CellSize);
            if (!EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    delayed.Signal,
                    delayed.TimingProfile,
                    poseResolver,
                    configuredProjector,
                    targetResolver,
                    out var command))
            {
                enemyDeathMotionMissingAnchorCount++;
                return;
            }

            TryPlayEnemyDeathMotionCommand(delayed.TickIndex, command, delayed.SourceVisualSnapshot);
        }

        private bool TryPlayEnemyDeathMotionCommand(
            int tickIndex,
            in EnemyDeathMotionVfxCommand command)
        {
            return TryPlayEnemyDeathMotionCommand(
                tickIndex,
                command,
                VfxRendererInactiveVisualSnapshotSet.Empty);
        }

        private bool TryPlayEnemyDeathMotionCommand(
            int tickIndex,
            in EnemyDeathMotionVfxCommand command,
            in VfxRendererInactiveVisualSnapshotSet sourceVisualSnapshot)
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
            return pool.PlayParameterizedMotion(
                playbackCommand,
                parameterizedCommand,
                sourceVisualSnapshot) != null;
        }

        private int PlayBoxSlideSolidStopCommands(in GameplayTickPresentationExtensionContext context)
        {
            var presentationData = context.Result.PresentationData;
            if (presentationData == null || pool == null || bindingResolver == null)
            {
                return 0;
            }

            var plannedCommandCount = 0;
            var signals = presentationData.BoxSlideStopSignals;
            var visibilityContext = BuildVisibilityContext(context.StateStore);
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!BoxSlideSolidStopVfxCommandBuilder.TryCreateRequest(
                        context.Result.TickIndex,
                        signal,
                        out var request))
                {
                    if (BoxSlideSolidStopVfxCommandBuilder.IsCandidate(signal))
                    {
                        boxSlideSolidStopMissingAnchorCount++;
                    }

                    continue;
                }

                if (!bindingResolver.TryResolve(request, out var policy))
                {
                    boxSlideSolidStopMissingBindingCount++;
                    continue;
                }

                policy.ValidateOrThrow();
                if (policy.CueId != request.CueId)
                {
                    throw new InvalidOperationException("Gameplay VFX binding cue does not match Box Slide solid stop request cue.");
                }

                if (!BoxSlideSolidStopVfxCommandBuilder.TryBuild(
                        context.Result.TickIndex,
                        signal,
                        context.Projector,
                        policy,
                        visibilityContext,
                        out request,
                        out var anchor))
                {
                    boxSlideSolidStopMissingAnchorCount++;
                    continue;
                }

                plannedCommandCount++;
                if (playedBoxSlideSolidStopKeys.Contains(request.SequenceId))
                {
                    continue;
                }

                if (TryPlayBoxSlideSolidStopCommand(request, policy, anchor, visibilityContext))
                {
                    playedBoxSlideSolidStopKeys.Add(request.SequenceId);
                }
            }

            return plannedCommandCount;
        }

        private bool TryPlayBoxSlideSolidStopCommand(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            in VfxResolvedAnchor anchor,
            in GameplayVfxVisibilityContext visibilityContext)
        {
            var preDecision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                policy,
                visibilityContext);
            if (!preDecision.IsVisible)
            {
                return false;
            }

            var postDecision = GameplayVfxVisibilityPolicy.EvaluateAfterAnchor(
                request,
                policy,
                anchor,
                visibilityContext);
            if (!postDecision.IsVisible)
            {
                return false;
            }

            var playbackCommand = new ResolvedVfxPlaybackCommand(request, policy, anchor);
            return pool.PlayTransient(playbackCommand) != null;
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

            var cellProjector = new GameplayVfxHostCellAnchorProjector(configuredProjector);
            if (!cellProjector.TryResolveCell(
                    signal.SourceCell,
                    signal.Topology,
                    VfxAnchorSlot.CellFloor,
                    policy.VisibilityMode,
                    out var anchor) ||
                !anchor.IsResolved)
            {
                return false;
            }

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

        private static bool HasBoxSlideSolidStopSignal(TickPresentationData presentationData)
        {
            if (presentationData == null)
            {
                return false;
            }

            var signals = presentationData.BoxSlideStopSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (BoxSlideSolidStopVfxCommandBuilder.IsCandidate(signals[i]))
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

        private static bool HasForwardCellProjectileSignal(TickPresentationData presentationData)
        {
            return presentationData != null &&
                   (presentationData.ForwardCellProjectileWindupSignals.Count > 0 ||
                    presentationData.ForwardCellProjectileReleaseSignals.Count > 0 ||
                    presentationData.ForwardCellProjectileClearSignals.Count > 0 ||
                    presentationData.ForwardCellProjectileArrivalSignals.Count > 0);
        }

        private static string ResolveForwardCellProjectileProductionGateReason(TickPresentationData presentationData)
        {
            if (presentationData != null &&
                presentationData.ForwardCellProjectileArrivalSignals.Count > 0)
            {
                return "ArrivalSignalPresent";
            }

            return "ForwardCellSignalPresent";
        }

        private static void LogForwardCellProjectileProductionGate(
            in GameplayTickPresentationExtensionContext context,
            bool controllerWillRun,
            string reason)
        {
            var presentationData = context.Result?.PresentationData;
            if (presentationData == null)
            {
                return;
            }

            ForwardCellProjectileDebugLog.MarkProductionGateForTick(
                context.Result.TickIndex,
                arrivalSignalCount: presentationData.ForwardCellProjectileArrivalSignals.Count,
                hitSignalCount: presentationData.ForwardCellImpactSignals.Count,
                releaseSignalCount: presentationData.ForwardCellProjectileReleaseSignals.Count,
                controllerWillRun,
                reason);
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

        private readonly struct DelayedEnemyDeathMotionVfxKey : IEquatable<DelayedEnemyDeathMotionVfxKey>
        {
            private DelayedEnemyDeathMotionVfxKey(int tickIndex, int entityId, int presentationSeed)
            {
                TickIndex = tickIndex;
                EntityId = entityId;
                PresentationSeed = presentationSeed;
            }

            private int TickIndex { get; }

            private int EntityId { get; }

            private int PresentationSeed { get; }

            public bool Equals(DelayedEnemyDeathMotionVfxKey other)
            {
                return TickIndex == other.TickIndex &&
                       EntityId == other.EntityId &&
                       PresentationSeed == other.PresentationSeed;
            }

            public override bool Equals(object obj)
            {
                return obj is DelayedEnemyDeathMotionVfxKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(TickIndex, EntityId, PresentationSeed);
            }

            public static DelayedEnemyDeathMotionVfxKey Create(
                int tickIndex,
                in TickEntityExitPresentationSignal signal)
            {
                return new DelayedEnemyDeathMotionVfxKey(
                    tickIndex,
                    signal.ExitedEntityId,
                    signal.PresentationSeed);
            }
        }

        private readonly struct DelayedEnemyDeathMotionVfx
        {
            public DelayedEnemyDeathMotionVfx(
                int tickIndex,
                TickEntityExitPresentationSignal signal,
                GameplayTimingProfile timingProfile,
                float remainingSeconds,
                in VfxRendererInactiveVisualSnapshotSet sourceVisualSnapshot)
            {
                TickIndex = tickIndex;
                Signal = signal;
                TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
                RemainingSeconds = Mathf.Max(0f, remainingSeconds);
                SourceVisualSnapshot = sourceVisualSnapshot;
            }

            public int TickIndex { get; }

            public TickEntityExitPresentationSignal Signal { get; }

            public GameplayTimingProfile TimingProfile { get; }

            public float RemainingSeconds { get; }

            public VfxRendererInactiveVisualSnapshotSet SourceVisualSnapshot { get; }

            public DelayedEnemyDeathMotionVfx Advance(float deltaTime)
            {
                return new DelayedEnemyDeathMotionVfx(
                    TickIndex,
                    Signal,
                    TimingProfile,
                    RemainingSeconds - Mathf.Max(0f, deltaTime),
                    SourceVisualSnapshot);
            }
        }

        private sealed class AuthoringPrefabProvider : IVfxPrefabProvider
        {
            private readonly GameObject commonEmptyHostPrefab;
            private readonly EnemyPresentationVfxProfileProvider enemyProfileProvider;
            private readonly VfxCueMapAsset hostDefaultMap;
            private readonly VfxProfileAsset[] profiles;

            public AuthoringPrefabProvider(
                EnemyPresentationVfxProfileProvider enemyProfileProvider,
                VfxCueMapAsset hostDefaultMap,
                VfxProfileAsset[] profiles,
                GameObject commonEmptyHostPrefab)
            {
                this.enemyProfileProvider = enemyProfileProvider;
                this.hostDefaultMap = hostDefaultMap;
                this.profiles = profiles ?? Array.Empty<VfxProfileAsset>();
                this.commonEmptyHostPrefab = commonEmptyHostPrefab;
            }

            public bool TryResolvePrefab(in ResolvedVfxPlaybackCommand command, out GameObject prefab)
            {
                if (enemyProfileProvider != null &&
                    enemyProfileProvider.TryResolveProfileAssetForSourceEntity(
                        command.Request.SourceEntityId,
                        out var sourceProfile) &&
                    TryResolvePrefabFromBinding(
                        sourceProfile,
                        command.CueId,
                        command.Request.StyleKey,
                        out prefab))
                {
                    return true;
                }

                for (var i = 0; i < profiles.Length; i++)
                {
                    var profile = profiles[i];
                    if (profile != null &&
                        TryResolvePrefabFromBinding(
                            profile,
                            command.CueId,
                            command.Request.StyleKey,
                            out prefab))
                    {
                        return true;
                    }
                }

                if (hostDefaultMap != null &&
                    TryResolvePrefabFromBinding(
                        hostDefaultMap,
                        command.CueId,
                        command.Request.StyleKey,
                        out prefab))
                {
                    return true;
                }

                prefab = null;
                return false;
            }

            private bool TryResolvePrefabFromBinding(
                VfxProfileAsset profile,
                GameplayVfxCueId cueId,
                VfxStyleKey styleKey,
                out GameObject prefab)
            {
                prefab = null;
                return profile.TryResolveBinding(cueId, styleKey, out var binding) &&
                       TryResolvePrefabFromBinding(binding, out prefab);
            }

            private bool TryResolvePrefabFromBinding(
                VfxCueMapAsset cueMap,
                GameplayVfxCueId cueId,
                VfxStyleKey styleKey,
                out GameObject prefab)
            {
                prefab = null;
                return cueMap.TryResolveBinding(cueId, styleKey, out var binding) &&
                       TryResolvePrefabFromBinding(binding, out prefab);
            }

            private bool TryResolvePrefabFromBinding(
                VfxBindingDefinitionAsset binding,
                out GameObject prefab)
            {
                prefab = binding.Prefab;
                if (prefab != null)
                {
                    return true;
                }

                if (binding.VisualSourceMode == VfxVisualSourceMode.SourceCloneMotion &&
                    binding.HostRequirement == GameplayVfxHostRequirement.CommonHostAllowed)
                {
                    prefab = commonEmptyHostPrefab;
                    return prefab != null;
                }

                return false;
            }
        }
    }
}
