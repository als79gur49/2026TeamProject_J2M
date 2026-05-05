using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx.Authoring;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxProductionRuntime : MonoBehaviour, IGameplayTickPresentationExtension, IGameplayPresentationMigrationGate
    {
        [SerializeField] private bool enableEnemyJumpTargetVfx;
        [SerializeField] private bool enableEnemyJumpLandingDustVfx;
        [SerializeField] private bool enableGameplayVfxDamageBurstMigration;
        [SerializeField] private bool enableGameplayVfxEnemyDamageBurstMigration;
        [SerializeField] private bool enableGameplayVfxBoxDestroySmokeMigration;
        [SerializeField] private bool enableGameplayVfxItemConsumeBurstMigration;
        [SerializeField] private VfxProfileAsset[] familyProfiles = Array.Empty<VfxProfileAsset>();

        private readonly PlayerVfxRequestPlanner playerPlanner = new();
        private readonly BoxVfxRequestPlanner boxPlanner = new();
        private readonly EnemyVfxRequestPlanner enemyPlanner = new();
        private readonly GameplayVfxRequestPlanBuilder planBuilder = new();

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

        public bool SuppressLegacyPlayerDamageHitEffects => enableGameplayVfxDamageBurstMigration;

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

        public bool SuppressLegacyBoxDestroySmokeEffects => enableGameplayVfxBoxDestroySmokeMigration;

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

        public bool SuppressLegacyItemConsumeEffects => enableGameplayVfxItemConsumeBurstMigration;

        public int LastPlannedRequestCount { get; private set; }

        public int ActiveVfxInstanceCount => pool?.ActiveCount ?? 0;

        public int MissingBindingCount => controller?.MissingBindingCount ?? 0;

        public int MissingAnchorCount => controller?.MissingAnchorCount ?? 0;

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
            controller?.HardCleanupAll();
            planBuilder.Clear();
        }

        public void Present(in GameplayTickPresentationExtensionContext context)
        {
            LastPlannedRequestCount = 0;
            if (!AnyGameplayVfxEnabled)
            {
                return;
            }

            ConfigureEnemyPresentationProfiles(
                context.EnemyPresentationCatalog,
                context.EnemyPresentationBindings);
            planBuilder.Clear();
            playerPlanner.Plan(
                new GameplayVfxPlanningContext(
                    context.Result.TickIndex,
                    context.Result.PresentationData,
                    context.Topology),
                planBuilder);
            boxPlanner.Plan(
                new GameplayVfxPlanningContext(
                    context.Result.TickIndex,
                    context.Result.PresentationData,
                    context.Topology),
                planBuilder);
            enemyPlanner.Plan(
                new GameplayVfxPlanningContext(
                    context.Result.TickIndex,
                    context.Result.PresentationData,
                    context.Topology),
                planBuilder);
            var plan = FilterByEnabledCues(planBuilder.Build());
            LastPlannedRequestCount = plan.Requests.Count;
            if (plan.Requests.Count == 0)
            {
                return;
            }

            EnsureRuntime(context);
            controller.Refresh(plan);
        }

        public void UpdatePresentation(float deltaTime)
        {
            pool?.Advance(deltaTime);
        }

        public void HardCleanup()
        {
            controller?.HardCleanupAll();
            LastPlannedRequestCount = 0;
        }

        private void EnsureRuntime(in GameplayTickPresentationExtensionContext context)
        {
            if (context.Projector == null)
            {
                throw new InvalidOperationException("Gameplay VFX production runtime requires a gameplay cube projector.");
            }

            if (context.StateStore == null)
            {
                throw new InvalidOperationException("Gameplay VFX production runtime requires a presentation state store.");
            }

            if (controller != null &&
                ReferenceEquals(configuredProjector, context.Projector) &&
                ReferenceEquals(configuredStateStore, context.StateStore))
            {
                return;
            }

            controller?.HardCleanupAll();
            configuredProjector = context.Projector;
            configuredStateStore = context.StateStore;
            runtimeRoot = runtimeRoot != null
                ? runtimeRoot
                : GameplayVfxRuntimeRoot.CreateUnder(transform);
            RebuildBindingRuntime();
            var anchorResolver = new GameplayVfxHostAnchorResolver(
                new GameplayVfxHostCellAnchorProjector(context.Projector),
                new GameplayVfxHostEntityAnchorProjector(context.StateStore));
            pool = new GameplayVfxGameObjectPool(runtimeRoot, prefabProvider);
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
            controller?.HardCleanupAll();
            controller = null;
            pool = null;
        }

        private bool AnyEnemyJumpVfxEnabled => enableEnemyJumpTargetVfx || enableEnemyJumpLandingDustVfx;

        private bool AnyGameplayVfxEnabled =>
            AnyEnemyJumpVfxEnabled ||
            enableGameplayVfxDamageBurstMigration ||
            enableGameplayVfxEnemyDamageBurstMigration ||
            enableGameplayVfxBoxDestroySmokeMigration ||
            enableGameplayVfxItemConsumeBurstMigration;

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
                if (IsCueEnabled(request.CueId))
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
                   (enableGameplayVfxBoxDestroySmokeMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.DestroySmoke)) ||
                   (enableGameplayVfxItemConsumeBurstMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.ItemConsume)) ||
                   (enableEnemyJumpTargetVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)) ||
                   (enableEnemyJumpLandingDustVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust));
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
