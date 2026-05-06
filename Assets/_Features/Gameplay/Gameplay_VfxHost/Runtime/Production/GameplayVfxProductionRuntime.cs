using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx.Authoring;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxProductionRuntime : MonoBehaviour, IGameplayTickPresentationExtension, IGameplayPresentationMigrationGate, IGameplayOutputCameraPresentationExtension
    {
        [SerializeField] private bool enableEnemyJumpTargetVfx = true;
        [SerializeField] private bool enableEnemyJumpLandingDustVfx = true;
        [SerializeField] private bool enableGameplayVfxDamageBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxEnemyDamageBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxEnemyDeathBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxEnemyDeathMotionMigration = true;
        [SerializeField] private bool enableGameplayVfxBoxDestroySmokeMigration = true;
        [SerializeField] private bool enableGameplayVfxItemConsumeBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxFlipImpactBurstMigration = true;
        [SerializeField] private bool enableGameplayVfxFlipDestroySelfMotionMigration = true;
        [SerializeField] private bool enableGameplayVfxBoxSlideTrail = true;
        [SerializeField] private bool enableGameplayVfxUtilityWindupMigration = true;
        [SerializeField] private bool enableGameplayVfxFrontFaceShieldActiveMigration = true;
        [SerializeField] private bool enableGameplayVfxFrontFaceShieldBlockMigration = true;
        [SerializeField] private VfxProfileAsset[] familyProfiles = Array.Empty<VfxProfileAsset>();

        private readonly PlayerVfxRequestPlanner playerPlanner = new();
        private readonly BoxVfxRequestPlanner boxPlanner = new();
        private readonly FlipImpactBurstVfxRequestPlanner flipImpactBurstPlanner = new();
        private readonly EnemyVfxRequestPlanner enemyPlanner = new();
        private readonly GameplayVfxRequestPlanBuilder planBuilder = new();
        private readonly HashSet<FlipDestroySelfMotionInstanceKey> playedFlipDestroySelfMotionKeys = new();
        private readonly HashSet<BoxSlideTrailMotionInstanceKey> playedBoxSlideTrailMotionKeys = new();

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
        private int boxSlideTrailMissingBindingCount;
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

        public bool SuppressLegacyEnemyDeathEffects => enableGameplayVfxEnemyDeathMotionMigration;

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

        public bool SuppressLegacyFlipDestroySelfEffects => enableGameplayVfxFlipDestroySelfMotionMigration;

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

        public bool SuppressLegacyUtilityWindupVfx => enableGameplayVfxUtilityWindupMigration;

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

        public bool SuppressLegacyFrontFaceShieldActiveVfx => enableGameplayVfxFrontFaceShieldActiveMigration;

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

        public bool SuppressLegacyFrontFaceShieldBlockVfx => enableGameplayVfxFrontFaceShieldBlockMigration;

        public int LastPlannedRequestCount { get; private set; }

        public int ActiveVfxInstanceCount => pool?.ActiveCount ?? 0;

        public int MissingBindingCount =>
            (controller?.MissingBindingCount ?? 0) +
            flipDestroySelfMotionMissingBindingCount +
            boxSlideTrailMissingBindingCount +
            enemyDeathMotionMissingBindingCount;

        public int MissingAnchorCount => (controller?.MissingAnchorCount ?? 0) + enemyDeathMotionMissingAnchorCount;

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
            boxSlideTrailMissingBindingCount = 0;
            enemyDeathMotionMissingBindingCount = 0;
            enemyDeathMotionMissingAnchorCount = 0;
            playedFlipDestroySelfMotionKeys.Clear();
            playedBoxSlideTrailMotionKeys.Clear();
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
                    context.Topology,
                    context.TimingProfile),
                planBuilder);
            boxPlanner.Plan(
                new GameplayVfxPlanningContext(
                    context.Result.TickIndex,
                    context.Result.PresentationData,
                    context.Topology,
                    context.TimingProfile),
                planBuilder);
            flipImpactBurstPlanner.Plan(
                new GameplayVfxPlanningContext(
                    context.Result.TickIndex,
                    context.Result.PresentationData,
                    context.Topology,
                    context.TimingProfile),
                planBuilder);
            enemyPlanner.Plan(
                new GameplayVfxPlanningContext(
                    context.Result.TickIndex,
                    context.Result.PresentationData,
                    context.Topology,
                    context.TimingProfile),
                planBuilder);
            var plan = FilterByEnabledCues(planBuilder.Build());
            var shouldPlayFlipDestroySelfMotion =
                enableGameplayVfxFlipDestroySelfMotionMigration &&
                HasDestroySelfFlipImpactSignal(context.Result.PresentationData);
            var shouldPlayBoxSlideTrail =
                enableGameplayVfxBoxSlideTrail &&
                HasBoxSlideMotion(context.Result.PresentationData);
            var shouldPlayEnemyDeathMotion =
                enableGameplayVfxEnemyDeathMotionMigration &&
                HasEnemyDeathExitSignal(context.Result.PresentationData);
            if (plan.Requests.Count == 0 &&
                !shouldPlayFlipDestroySelfMotion &&
                !shouldPlayBoxSlideTrail &&
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
            var enemyDeathMotionCommandCount = shouldPlayEnemyDeathMotion
                ? PlayEnemyDeathMotionCommands(context)
                : 0;
            LastPlannedRequestCount = plan.Requests.Count +
                                      flipDestroySelfMotionCommandCount +
                                      boxSlideTrailCommandCount +
                                      enemyDeathMotionCommandCount;
        }

        public void UpdatePresentation(float deltaTime)
        {
            pool?.Advance(deltaTime);
        }

        public void HardCleanup()
        {
            controller?.HardCleanupAll();
            LastPlannedRequestCount = 0;
            playedFlipDestroySelfMotionKeys.Clear();
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
            pool = new GameplayVfxGameObjectPool(
                runtimeRoot,
                prefabProvider,
                cloneSourceProvider: new GameplayVfxStateStoreCloneSourceProvider(context.StateStore));
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
            enableGameplayVfxEnemyDeathBurstMigration ||
            enableGameplayVfxEnemyDeathMotionMigration ||
            enableGameplayVfxBoxDestroySmokeMigration ||
            enableGameplayVfxItemConsumeBurstMigration ||
            enableGameplayVfxFlipImpactBurstMigration ||
            enableGameplayVfxFlipDestroySelfMotionMigration ||
            enableGameplayVfxBoxSlideTrail ||
            enableGameplayVfxUtilityWindupMigration ||
            enableGameplayVfxFrontFaceShieldActiveMigration ||
            enableGameplayVfxFrontFaceShieldBlockMigration;

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
                   (enableGameplayVfxEnemyDeathBurstMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.Death)) ||
                   (enableGameplayVfxEnemyDeathMotionMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)) ||
                   (enableGameplayVfxUtilityWindupMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup)) ||
                   (enableGameplayVfxFrontFaceShieldActiveMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive)) ||
                   (enableGameplayVfxFrontFaceShieldBlockMigration && cueId == GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock)) ||
                   (enableGameplayVfxBoxDestroySmokeMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.DestroySmoke)) ||
                   (enableGameplayVfxItemConsumeBurstMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.ItemConsume)) ||
                   (enableGameplayVfxFlipImpactBurstMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.FlipImpactBurst)) ||
                   (enableGameplayVfxFlipDestroySelfMotionMigration && cueId == GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion)) ||
                   (enableGameplayVfxBoxSlideTrail && cueId == GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail)) ||
                   (enableEnemyJumpTargetVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)) ||
                   (enableEnemyJumpLandingDustVfx && cueId == GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust));
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
