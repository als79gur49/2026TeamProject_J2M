using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Vfx
{
    public interface IGameplayVfxFamilyRequestPlanner
    {
        GameplayVfxFamily Family { get; }

        void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder);
    }

    public readonly struct GameplayVfxTopologyTransitionContext
    {
        public GameplayVfxTopologyTransitionContext(
            TickTopologyMotion topologyMotion,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeTopologyState anchorTopology,
            int transitionEpoch,
            bool isTransitionStartTick,
            bool isTransitionCompletionReconcile)
        {
            HasTopologyMotion = true;
            TopologyMotion = topologyMotion;
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            AnchorTopology = anchorTopology;
            TransitionEpoch = transitionEpoch;
            IsTransitionStartTick = isTransitionStartTick;
            IsTransitionCompletionReconcile = isTransitionCompletionReconcile;
        }

        public bool HasTopologyMotion { get; }

        public TickTopologyMotion TopologyMotion { get; }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeTopologyState AnchorTopology { get; }

        public int TransitionEpoch { get; }

        public bool IsTransitionStartTick { get; }

        public bool IsTransitionCompletionReconcile { get; }

        public static GameplayVfxTopologyTransitionContext None(CubeTopologyState topology)
        {
            return new GameplayVfxTopologyTransitionContext(
                hasTopologyMotion: false,
                topologyMotion: default,
                sourceTopology: topology,
                destinationTopology: topology,
                anchorTopology: topology,
                transitionEpoch: 0,
                isTransitionStartTick: false,
                isTransitionCompletionReconcile: false);
        }

        private GameplayVfxTopologyTransitionContext(
            bool hasTopologyMotion,
            TickTopologyMotion topologyMotion,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeTopologyState anchorTopology,
            int transitionEpoch,
            bool isTransitionStartTick,
            bool isTransitionCompletionReconcile)
        {
            HasTopologyMotion = hasTopologyMotion;
            TopologyMotion = topologyMotion;
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            AnchorTopology = anchorTopology;
            TransitionEpoch = transitionEpoch;
            IsTransitionStartTick = isTransitionStartTick;
            IsTransitionCompletionReconcile = isTransitionCompletionReconcile;
        }
    }

    public readonly struct GameplayVfxPlanningContext
    {
        public GameplayVfxPlanningContext(int tickIndex)
            : this(tickIndex, (TickPresentationData)null, default)
        {
        }

        public GameplayVfxPlanningContext(
            int tickIndex,
            TickPresentationData presentationData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile = null,
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null,
            GameplayVfxVisibilityContext visibilityContext = default,
            GameplayVfxTopologyTransitionContext topologyTransition = default)
        {
            TickIndex = tickIndex;
            PresentationData = presentationData;
            EntitySpawnSignals = presentationData?.EntitySpawnSignals ?? Array.Empty<EntitySpawnPresentationSignal>();
            Topology = topology;
            TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            TileFeatureVfxStyleBindings = tileFeatureVfxStyleBindings ?? Array.Empty<TileFeatureVfxStyleBinding>();
            VisibilityContext = visibilityContext;
            TopologyTransition = topologyTransition.HasTopologyMotion
                ? topologyTransition
                : GameplayVfxTopologyTransitionContext.None(topology);
        }

        public GameplayVfxPlanningContext(
            int tickIndex,
            InitialPresentationData presentationData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile = null,
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null,
            GameplayVfxVisibilityContext visibilityContext = default,
            GameplayVfxTopologyTransitionContext topologyTransition = default)
        {
            TickIndex = tickIndex;
            PresentationData = null;
            EntitySpawnSignals = presentationData?.EntitySpawnSignals ?? Array.Empty<EntitySpawnPresentationSignal>();
            Topology = topology;
            TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            TileFeatureVfxStyleBindings = tileFeatureVfxStyleBindings ?? Array.Empty<TileFeatureVfxStyleBinding>();
            VisibilityContext = visibilityContext;
            TopologyTransition = topologyTransition.HasTopologyMotion
                ? topologyTransition
                : GameplayVfxTopologyTransitionContext.None(topology);
        }

        public static GameplayVfxPlanningContext ForTick(
            int tickIndex,
            TickPresentationData presentationData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile = null,
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null,
            GameplayVfxVisibilityContext visibilityContext = default,
            GameplayVfxTopologyTransitionContext topologyTransition = default)
        {
            return new GameplayVfxPlanningContext(
                tickIndex,
                presentationData,
                topology,
                timingProfile,
                tileFeatureVfxStyleBindings,
                visibilityContext,
                topologyTransition);
        }

        public static GameplayVfxPlanningContext ForInitial(
            InitialPresentationData presentationData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile = null,
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null,
            GameplayVfxVisibilityContext visibilityContext = default)
        {
            return new GameplayVfxPlanningContext(
                0,
                presentationData,
                topology,
                timingProfile,
                tileFeatureVfxStyleBindings,
                visibilityContext);
        }

        public int TickIndex { get; }

        public TickPresentationData PresentationData { get; }

        public IReadOnlyList<EntitySpawnPresentationSignal> EntitySpawnSignals { get; }

        public CubeTopologyState Topology { get; }

        public GameplayTimingProfile TimingProfile { get; }

        public IReadOnlyList<TileFeatureVfxStyleBinding> TileFeatureVfxStyleBindings { get; }

        public GameplayVfxVisibilityContext VisibilityContext { get; }

        public GameplayVfxTopologyTransitionContext TopologyTransition { get; }
    }

    public sealed class PlayerVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Player;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var presentationData = context.PresentationData;
            if (presentationData == null)
            {
                return;
            }

            var damageSignals = presentationData.PlayerDamageSignals;
            for (var i = 0; i < damageSignals.Count; i++)
            {
                var signal = damageSignals[i];
                if (!signal.TookDamageThisTick ||
                    DidPlayerDieThisTick(presentationData, signal.EntityId))
                {
                    continue;
                }

                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.EntityId,
                        presentationSeed: signal.EntityId,
                        sourceEntityId: signal.EntityId,
                        cueId: GameplayVfxCueId.From(PlayerVfxCue.Damage),
                        anchor: VfxAnchor.ForEntity(
                            signal.EntityId,
                            VfxAnchorSlot.EntityCenter),
                        timing: VfxTimingKind.ImmediateOnTickPresentation));
            }
        }

        private static bool DidPlayerDieThisTick(TickPresentationData presentationData, int entityId)
        {
            var deathSignals = presentationData.PlayerDeathSignals;
            for (var i = 0; i < deathSignals.Count; i++)
            {
                var signal = deathSignals[i];
                if (signal.EntityId == entityId && signal.DidDieThisTick)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class BoxVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Box;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var presentationData = context.PresentationData;
            if (presentationData == null)
            {
                return;
            }

            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                var signal = exitSignals[i];
                if (signal.EntityType != EntityType.Box ||
                    BoxVfxExitSignalGuards.IsDuplicateOwnedExit(presentationData, signal.ExitedEntityId))
                {
                    continue;
                }

                if (signal.ExitCause == TickEntityExitCause.BoxDestroy)
                {
                    if (signal.Timing == EntityExitPresentationTiming.AfterEntityMotion)
                    {
                        continue;
                    }

                    AddExitRequest(context, builder, signal, BoxVfxCue.DestroySmoke);
                }
                else if (signal.ExitCause == TickEntityExitCause.ItemConsume)
                {
                    AddExitRequest(context, builder, signal, BoxVfxCue.ItemConsume);
                }
                else if (signal.ExitCause == TickEntityExitCause.OutOfBounds)
                {
                    AddExitRequest(context, builder, signal, BoxVfxCue.OutOfBoundsExit, VfxAnchorSlot.CellCenter);
                }
            }

            var impactSignals = presentationData.ImpactTransientSignals;
            for (var i = 0; i < impactSignals.Count; i++)
            {
                var signal = impactSignals[i];
                if (signal.EntityType != EntityType.Box ||
                    signal.EntityId <= 0)
                {
                    continue;
                }

                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.EntityId,
                        presentationSeed: signal.PresentationSeed != 0
                            ? signal.PresentationSeed
                            : ComputeImpactTransientSequenceId(context.TickIndex, signal),
                        sourceEntityId: signal.EntityId,
                        cueId: GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak),
                        anchor: VfxAnchor.ForCell(
                            signal.SourceCell,
                            signal.Topology,
                            VfxAnchorSlot.CellCenter),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: false,
                        persistentKey: VfxPersistentKey.None));
            }

            var boxSlideStopSignals = presentationData.BoxSlideStopSignals;
            for (var i = 0; i < boxSlideStopSignals.Count; i++)
            {
                var signal = boxSlideStopSignals[i];
                if (signal.StopperKind != BoxSlideStopperKind.SolidEntity ||
                    signal.Cause != BoxSlideStopCause.SlidingContinuationBlocked ||
                    signal.BoxEntityId <= 0 ||
                    signal.StopperEntityId <= 0)
                {
                    continue;
                }

                var seed = ComputeBoxSlideSolidStopSequenceId(context.TickIndex, signal);
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: seed,
                        presentationSeed: seed,
                        sourceEntityId: signal.BoxEntityId,
                        cueId: GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop),
                        anchor: VfxAnchor.ForCell(
                            signal.SourceCell,
                            signal.Topology,
                            VfxAnchorSlot.CellCenter),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: false,
                        persistentKey: VfxPersistentKey.None));
            }
        }

        private static void AddExitRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in TickEntityExitPresentationSignal signal,
            BoxVfxCue cue,
            VfxAnchorSlot anchorSlot = VfxAnchorSlot.CellFloor)
        {
            builder.Add(
                new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: signal.ExitedEntityId,
                    presentationSeed: signal.PresentationSeed != 0 ? signal.PresentationSeed : signal.ExitedEntityId,
                    sourceEntityId: signal.ExitedEntityId,
                    cueId: GameplayVfxCueId.From(cue),
                    anchor: VfxAnchor.ForCell(
                        signal.SourceCell,
                        signal.Topology,
                        anchorSlot),
                    timing: VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: false,
                    persistentKey: VfxPersistentKey.None));
        }

        private static int ComputeImpactTransientSequenceId(
            int tickIndex,
            in TickImpactTransientPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.EntityId;
                hash = (hash * 31) + (int)BoxVfxCue.ImpactTransientBreak;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.ImpactCell.GetHashCode();
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

        internal static int ComputeBoxSlideSolidStopSequenceId(
            int tickIndex,
            in BoxSlideStopPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.BoxEntityId;
                hash = (hash * 31) + signal.StopperEntityId;
                hash = (hash * 31) + (int)BoxVfxCue.BoxSlideSolidStop;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.StopperCell.GetHashCode();
                hash = (hash * 31) + (int)signal.SlideDirection;
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

    }

    public static class BoxVfxExitSignalGuards
    {
        public static bool IsDuplicateOwnedExit(TickPresentationData presentationData, int entityId)
        {
            return IsImpactTransientSource(presentationData, entityId) ||
                   IsFlipImpactDestroySelfSource(presentationData, entityId);
        }

        private static bool IsFlipImpactDestroySelfSource(TickPresentationData presentationData, int entityId)
        {
            var flipImpactSignals = presentationData.FlipImpactSignals;
            for (var i = 0; i < flipImpactSignals.Count; i++)
            {
                var signal = flipImpactSignals[i];
                if (signal.BoxEntityId == entityId &&
                    signal.Disposition == FlipImpactPresentationDisposition.DestroySelf)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsImpactTransientSource(TickPresentationData presentationData, int entityId)
        {
            var impactSignals = presentationData.ImpactTransientSignals;
            for (var i = 0; i < impactSignals.Count; i++)
            {
                if (impactSignals[i].EntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

    }

    public sealed class EnemyVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Enemy;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var presentationData = context.PresentationData;
            if (presentationData == null)
            {
                return;
            }

            var damageSignals = presentationData.EnemyDamageSignals;
            for (var i = 0; i < damageSignals.Count; i++)
            {
                var signal = damageSignals[i];
                if (!signal.TookDamageThisTick ||
                    signal.EntityId <= 0 ||
                    DidEnemyExitThisTick(presentationData, signal.EntityId))
                {
                    continue;
                }

                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.EntityId,
                        presentationSeed: signal.EntityId,
                        sourceEntityId: signal.EntityId,
                        cueId: GameplayVfxCueId.From(EnemyVfxCue.Damage),
                        anchor: VfxAnchor.ForEntity(
                            signal.EntityId,
                            VfxAnchorSlot.EntityCenter),
                        timing: VfxTimingKind.ImmediateOnTickPresentation));
            }

            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                var signal = exitSignals[i];
                if (signal.EntityType != EntityType.Unit ||
                    signal.ExitedEntityId <= 0 ||
                    !IsEnemyDeathExitCause(signal.ExitCause))
                {
                    if (signal.EntityType == EntityType.Unit &&
                        signal.ExitedEntityId > 0 &&
                        signal.ExitCause == TickEntityExitCause.OutOfBounds)
                    {
                        builder.Add(
                            new GameplayVfxRequest(
                                tickIndex: context.TickIndex,
                                sequenceId: signal.ExitedEntityId,
                                presentationSeed: signal.PresentationSeed != 0 ? signal.PresentationSeed : signal.ExitedEntityId,
                                sourceEntityId: signal.ExitedEntityId,
                                cueId: GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit),
                                anchor: VfxAnchor.ForCell(
                                    signal.SourceCell,
                                    signal.Topology,
                                    VfxAnchorSlot.CellCenter),
                                timing: VfxTimingKind.ImmediateOnTickPresentation,
                                isPersistent: false,
                                persistentKey: VfxPersistentKey.None));
                    }

                    continue;
                }

                var delaySeconds = signal.Timing == EntityExitPresentationTiming.AtContactTime
                    ? context.TimingProfile.FlipMotionDurationSeconds * signal.VisualContactNormalizedTime
                    : 0f;
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.ExitedEntityId,
                        presentationSeed: signal.PresentationSeed != 0 ? signal.PresentationSeed : signal.ExitedEntityId,
                        sourceEntityId: signal.ExitedEntityId,
                        cueId: GameplayVfxCueId.From(EnemyVfxCue.Death),
                        anchor: VfxAnchor.ForCell(
                            signal.SourceCell,
                            signal.Topology,
                            VfxAnchorSlot.CellCenter),
                        timing: delaySeconds > 0f
                            ? VfxTimingKind.Delayed
                            : VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: false,
                        persistentKey: VfxPersistentKey.None,
                        delaySeconds: delaySeconds));
            }

            var summonWindupWarnings = presentationData.SummonWindupWarnings;
            for (var i = 0; i < summonWindupWarnings.Count; i++)
            {
                var signal = summonWindupWarnings[i];
                if (signal.SourceEntityId <= 0 ||
                    DidEnemyExitThisTick(presentationData, signal.SourceEntityId))
                {
                    continue;
                }

                var cueId = GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup);
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.SourceEntityId,
                        presentationSeed: signal.PresentationSeed != 0
                            ? signal.PresentationSeed
                            : signal.SourceEntityId,
                        sourceEntityId: signal.SourceEntityId,
                        cueId: cueId,
                        anchor: VfxAnchor.ForEntity(
                            signal.SourceEntityId,
                            VfxAnchorSlot.EntityCenter,
                            signal.SourceCell,
                            signal.Topology,
                            hasFallbackCell: true),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: true,
                        persistentKey: new VfxPersistentKey(
                            cueId,
                            VfxAnchorKind.Entity,
                            entityId: signal.SourceEntityId,
                            effectIndex: signal.EffectIndex,
                            activationSequence: signal.ActivationSequence)));
            }

            var frontFaceShieldWindupWarnings = presentationData.FrontFaceShieldWindupWarnings;
            for (var i = 0; i < frontFaceShieldWindupWarnings.Count; i++)
            {
                var signal = frontFaceShieldWindupWarnings[i];
                if (signal.SourceEntityId <= 0 ||
                    DidEnemyExitThisTick(presentationData, signal.SourceEntityId))
                {
                    continue;
                }

                var cueId = GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldWindup);
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.SourceEntityId,
                        presentationSeed: signal.PresentationSeed != 0
                            ? signal.PresentationSeed
                            : signal.SourceEntityId,
                        sourceEntityId: signal.SourceEntityId,
                        cueId: cueId,
                        anchor: VfxAnchor.ForEntity(
                            signal.SourceEntityId,
                            VfxAnchorSlot.EntityCenter,
                            signal.SourceCell,
                            signal.Topology,
                            hasFallbackCell: true),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: true,
                        persistentKey: new VfxPersistentKey(
                            cueId,
                            VfxAnchorKind.Entity,
                            entityId: signal.SourceEntityId,
                            effectIndex: signal.EffectIndex,
                            activationSequence: signal.ActivationSequence)));
            }

            var frontFaceShieldSources = presentationData.FrontFaceShieldSources;
            for (var i = 0; i < frontFaceShieldSources.Count; i++)
            {
                var signal = frontFaceShieldSources[i];
                if (signal.SourceEntityId <= 0 ||
                    DidEnemyExitThisTick(presentationData, signal.SourceEntityId))
                {
                    continue;
                }

                var cueId = GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldActive);
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.SourceEntityId,
                        presentationSeed: signal.PresentationSeed != 0
                            ? signal.PresentationSeed
                            : signal.SourceEntityId,
                        sourceEntityId: signal.SourceEntityId,
                        cueId: cueId,
                        anchor: VfxAnchor.ForEntity(
                            signal.SourceEntityId,
                            VfxAnchorSlot.EntityCenter,
                            signal.SourceCell,
                            signal.Topology,
                            hasFallbackCell: true),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: true,
                        persistentKey: new VfxPersistentKey(
                            cueId,
                            VfxAnchorKind.Entity,
                            entityId: signal.SourceEntityId)));
            }

            var frontFaceShieldBlocks = presentationData.FrontFaceShieldBlocks;
            for (var i = 0; i < frontFaceShieldBlocks.Count; i++)
            {
                var signal = frontFaceShieldBlocks[i];
                var sourceEntityId = signal.ShieldSourceEntityId > 0
                    ? signal.ShieldSourceEntityId
                    : signal.BoxEntityId;
                var seed = signal.PresentationSeed != 0
                    ? signal.PresentationSeed
                    : ResolveFrontFaceShieldBlockSeed(signal, context.TickIndex);
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: seed,
                        presentationSeed: seed,
                        sourceEntityId: sourceEntityId,
                        cueId: GameplayVfxCueId.From(EnemyVfxCue.FrontFaceShieldBlock),
                        anchor: VfxAnchor.ForCell(
                            signal.BlockedCell,
                            signal.Topology,
                            VfxAnchorSlot.CellFloor),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: false,
                        persistentKey: VfxPersistentKey.None));
            }

            var visibilityChanges = presentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var signal = visibilityChanges[i];
                if (signal.ChangeKind != TickVisibilityChangeKind.Spawn ||
                    signal.EntityId <= 0 ||
                    !IsSummonedEnemyPresentationEntity(presentationData, signal.EntityId))
                {
                    continue;
                }

                var seed = ResolveUtilitySummonSpawnSeed(context.TickIndex, signal);
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: seed,
                        presentationSeed: seed,
                        sourceEntityId: signal.EntityId,
                        cueId: GameplayVfxCueId.From(EnemyVfxCue.UtilitySummonSpawn),
                        anchor: VfxAnchor.ForCell(
                            signal.Cell,
                            signal.Topology,
                            VfxAnchorSlot.CellCenter),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: false,
                    persistentKey: VfxPersistentKey.None));
            }

            var gravityFieldAuraStates = presentationData.EnemyGravityFieldAuraVisualStates;
            for (var i = 0; i < gravityFieldAuraStates.Count; i++)
            {
                var state = gravityFieldAuraStates[i];
                if (state.EntityId <= 0)
                {
                    continue;
                }

                if (TryResolveGravityFieldAuraAreaCue(state.Phase, out var areaCue))
                {
                    AddGravityFieldAuraAreaRequest(context, builder, state, areaCue);
                }

                if (state.Phase == EnemyUtilityEffectPhase.Active &&
                    state.StartedThisTick)
                {
                    AddGravityFieldAuraActiveStartedRequest(context, builder, state);
                }
            }

            var jumpSignals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < jumpSignals.Count; i++)
            {
                var signal = jumpSignals[i];
                if (IsJumperLandingTargetCueSource(signal))
                {
                    var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
                    builder.Add(
                        new GameplayVfxRequest(
                            tickIndex: context.TickIndex,
                            sequenceId: ResolveSequenceId(signal),
                            presentationSeed: signal.EntityId,
                            // PresentationSeed remains a visual variation seed; SourceEntityId is the source identity.
                            sourceEntityId: signal.EntityId,
                            cueId: cueId,
                            anchor: VfxAnchor.ForCell(
                                signal.PresentationTargetCell,
                                context.Topology,
                                VfxAnchorSlot.CellFloor),
                            timing: VfxTimingKind.ImmediateOnTickPresentation,
                            isPersistent: true,
                            persistentKey: new VfxPersistentKey(
                                cueId,
                                VfxAnchorKind.Cell,
                                entityId: signal.EntityId,
                                cell: signal.PresentationTargetCell,
                                hasCell: true,
                                activationSequence: signal.Sequence),
                            topologyStopMode: GameplayVfxTopologyStopMode.TopologyHelperExempt));
                }

                if (IsJumperJumpStartCueSource(signal))
                {
                    builder.Add(
                        new GameplayVfxRequest(
                            tickIndex: context.TickIndex,
                            sequenceId: ResolveSequenceId(signal),
                            presentationSeed: signal.EntityId,
                            sourceEntityId: signal.EntityId,
                            cueId: GameplayVfxCueId.From(EnemyVfxCue.JumperJumpStart),
                            anchor: VfxAnchor.ForCell(
                                signal.SourceCell,
                                context.Topology,
                                VfxAnchorSlot.CellFloor),
                            timing: VfxTimingKind.ImmediateOnTickPresentation));
                }

                if (IsJumperLandingDustCueSource(signal))
                {
                    builder.Add(
                        new GameplayVfxRequest(
                            tickIndex: context.TickIndex,
                            sequenceId: ResolveSequenceId(signal),
                            presentationSeed: signal.EntityId,
                            sourceEntityId: signal.EntityId,
                            cueId: GameplayVfxCueId.From(EnemyVfxCue.JumperLandingDust),
                            anchor: VfxAnchor.ForCell(
                                signal.PresentationTargetCell,
                                context.Topology,
                                VfxAnchorSlot.CellFloor),
                            timing: VfxTimingKind.ImmediateOnTickPresentation));
                }
            }
        }

        private static bool TryResolveGravityFieldAuraAreaCue(
            EnemyUtilityEffectPhase phase,
            out EnemyVfxCue cue)
        {
            switch (phase)
            {
                case EnemyUtilityEffectPhase.Windup:
                    cue = EnemyVfxCue.GravityFieldAuraWindupArea;
                    return true;
                case EnemyUtilityEffectPhase.Active:
                    cue = EnemyVfxCue.GravityFieldAuraActiveArea;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        private static void AddGravityFieldAuraAreaRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in TickEnemyGravityFieldAuraVisualState state,
            EnemyVfxCue cue)
        {
            var cueId = GameplayVfxCueId.From(cue);
            var sequenceId = ResolveGravityFieldAuraSequenceId(context.TickIndex, state, cue);
            builder.Add(
                new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: sequenceId,
                    presentationSeed: sequenceId,
                    sourceEntityId: state.EntityId,
                    cueId: cueId,
                    anchor: VfxAnchor.ForCell(
                        state.Cell,
                        context.Topology,
                        VfxAnchorSlot.CellCenter),
                    timing: VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: true,
                    persistentKey: new VfxPersistentKey(
                        cueId,
                        VfxAnchorKind.Cell,
                        entityId: state.EntityId,
                        cell: state.Cell,
                        hasCell: true,
                        effectIndex: state.EffectIndex,
                        activationSequence: state.ActivationSequence)));
        }

        private static void AddGravityFieldAuraActiveStartedRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in TickEnemyGravityFieldAuraVisualState state)
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.GravityFieldAuraActiveStarted);
            var sequenceId = ResolveGravityFieldAuraSequenceId(context.TickIndex, state, EnemyVfxCue.GravityFieldAuraActiveStarted);
            builder.Add(
                new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: sequenceId,
                    presentationSeed: sequenceId,
                    sourceEntityId: state.EntityId,
                    cueId: cueId,
                    anchor: VfxAnchor.ForCell(
                        state.Cell,
                        context.Topology,
                        VfxAnchorSlot.CellCenter),
                    timing: VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: false,
                    persistentKey: VfxPersistentKey.None));
        }

        private static int ResolveGravityFieldAuraSequenceId(
            int tickIndex,
            in TickEnemyGravityFieldAuraVisualState state,
            EnemyVfxCue cue)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ state.EntityId;
                hash = (hash * 397) ^ state.EffectIndex;
                hash = (hash * 397) ^ state.ActivationSequence;
                hash = (hash * 397) ^ (int)state.Phase;
                hash = (hash * 397) ^ (int)cue;
                hash = (hash * 397) ^ state.Cell.GetHashCode();
                return hash != 0 ? hash : state.EntityId;
            }
        }

        private static bool IsJumperLandingTargetCueSource(in TickEnemyJumpPresentationSignal signal)
        {
            return signal.Phase == EnemyJumpPhase.Windup ||
                   signal.Phase == EnemyJumpPhase.Airborne ||
                   signal.StartedWindupThisTick ||
                   signal.Outcome == TickEnemyJumpPresentationOutcome.WindupStarted;
        }

        private static bool IsJumperJumpStartCueSource(in TickEnemyJumpPresentationSignal signal)
        {
            return signal.StartedAirborneThisTick ||
                   signal.Outcome == TickEnemyJumpPresentationOutcome.AirborneStarted;
        }

        private static bool IsJumperLandingDustCueSource(in TickEnemyJumpPresentationSignal signal)
        {
            return signal.Outcome == TickEnemyJumpPresentationOutcome.Landed ||
                   signal.Outcome == TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded;
        }

        private static bool IsEnemyDeathExitCause(TickEntityExitCause exitCause)
        {
            return exitCause == TickEntityExitCause.Killed;
        }

        private static int ResolveSequenceId(in TickEnemyJumpPresentationSignal signal)
        {
            return signal.Sequence > 0 ? signal.Sequence : signal.EntityId;
        }

        private static bool IsSummonedEnemyPresentationEntity(
            TickPresentationData presentationData,
            int entityId)
        {
            var bindings = presentationData.SummonedEnemyPresentationBindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].EntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveUtilitySummonSpawnSeed(
            int tickIndex,
            in TickVisibilityChange signal)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ signal.EntityId;
                hash = (hash * 397) ^ (int)EnemyVfxCue.UtilitySummonSpawn;
                hash = (hash * 397) ^ signal.Cell.GetHashCode();
                hash = (hash * 397) ^ signal.Topology.GetHashCode();
                return hash != 0 ? hash : signal.EntityId;
            }
        }

        private static bool DidEnemyExitThisTick(TickPresentationData presentationData, int entityId)
        {
            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                if (exitSignals[i].ExitedEntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveFrontFaceShieldBlockSeed(
            in TickFrontFaceShieldBlockSignal signal,
            int tickIndex)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ signal.ShieldSourceEntityId;
                hash = (hash * 397) ^ signal.BoxEntityId;
                hash = (hash * 397) ^ signal.ActorEntityId;
                hash = (hash * 397) ^ signal.BlockedCell.GetHashCode();
                hash = (hash * 397) ^ signal.ShieldSourceCell.GetHashCode();
                hash = (hash * 397) ^ (int)signal.MovementKind;
                return hash != 0 ? hash : tickIndex;
            }
        }
    }

    public sealed class TileFeatureVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        private const int ActiveLoopEffectIndex = 1;
        private const int VisibleLoopEffectIndex = 2;

        private readonly struct EntranceSpawnRequestKey : IEquatable<EntranceSpawnRequestKey>
        {
            public EntranceSpawnRequestKey(
                int entityId,
                EntitySpawnPresentationReason reason,
                int sourceTileId,
                SurfaceCell sourceCell)
            {
                EntityId = entityId;
                Reason = reason;
                SourceTileId = sourceTileId;
                SourceCell = sourceCell;
            }

            public int EntityId { get; }

            public EntitySpawnPresentationReason Reason { get; }

            public int SourceTileId { get; }

            public SurfaceCell SourceCell { get; }

            public bool Equals(EntranceSpawnRequestKey other)
            {
                return EntityId == other.EntityId &&
                       Reason == other.Reason &&
                       SourceTileId == other.SourceTileId &&
                       SourceCell.Equals(other.SourceCell);
            }

            public override bool Equals(object obj)
            {
                return obj is EntranceSpawnRequestKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = EntityId;
                    hash = (hash * 397) ^ (int)Reason;
                    hash = (hash * 397) ^ SourceTileId;
                    hash = (hash * 397) ^ SourceCell.GetHashCode();
                    return hash;
                }
            }
        }

        public GameplayVfxFamily Family => GameplayVfxFamily.TileFeature;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (context.PresentationData != null)
            {
                PlanEvents(context, builder, context.PresentationData);
                PlanVisualStates(context, builder, context.PresentationData);
                PlanVisibleVisualStates(context, builder, context.PresentationData);
            }

            PlanEntitySpawnSignals(context, builder);
        }

        public void PlanPersistentLoops(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var presentationData = context.PresentationData;
            if (presentationData == null)
            {
                return;
            }

            PlanVisualStates(context, builder, presentationData);
            PlanVisibleVisualStates(context, builder, presentationData);
            PlanActiveVisualStates(context, builder, presentationData);
        }

        private static void PlanEntitySpawnSignals(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder)
        {
            var spawnSignals = context.EntitySpawnSignals;
            if (spawnSignals.Count == 0)
            {
                return;
            }

            var emitted = new HashSet<EntranceSpawnRequestKey>();
            for (var i = 0; i < spawnSignals.Count; i++)
            {
                var signal = spawnSignals[i];
                if (!TryGetEntranceSourceTileFeature(signal, out var sourceTileFeature))
                {
                    continue;
                }

                var key = new EntranceSpawnRequestKey(
                    signal.EntityId,
                    signal.Reason,
                    sourceTileFeature.TileId,
                    sourceTileFeature.Cell);
                if (!emitted.Add(key))
                {
                    continue;
                }

                var sequenceId = ResolveEntranceSpawnSequenceId(context.TickIndex, key);
                var request = new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: sequenceId,
                    presentationSeed: sequenceId,
                    sourceEntityId: signal.EntityId,
                    cueId: GameplayVfxCueId.From(TileFeatureVfxCue.EntranceSpawn),
                    anchor: VfxAnchor.ForCell(
                        sourceTileFeature.Cell,
                        signal.Topology,
                        VfxAnchorSlot.CellFloor),
                    timing: VfxTimingKind.ImmediateOnTickPresentation);
                builder.Add(request);
            }
        }

        private static bool TryGetEntranceSourceTileFeature(
            EntitySpawnPresentationSignal signal,
            out TileFeaturePresentationSource sourceTileFeature)
        {
            if (signal.EntityKind == EntityPresentationKind.Player &&
                (signal.Reason == EntitySpawnPresentationReason.InitialStageStart ||
                 signal.Reason == EntitySpawnPresentationReason.PlayerRespawn) &&
                signal.SourceTileFeature.HasValue &&
                signal.SourceTileFeature.Value.FeatureKind == TileFeatureKind.Entrance)
            {
                sourceTileFeature = signal.SourceTileFeature.Value;
                return true;
            }

            sourceTileFeature = default;
            return false;
        }

        private static void PlanEvents(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            TickPresentationData presentationData)
        {
            var events = presentationData.TileEvents;
            for (var i = 0; i < events.Count; i++)
            {
                var tileEvent = events[i];
                if (!TryResolveCue(tileEvent, out var cue))
                {
                    continue;
                }

                var sequenceId = ResolveSequenceId(context.TickIndex, i, tileEvent);
                var delaySeconds = PresentationTimingResolver.ResolveDelaySeconds(
                    tileEvent.TimingAnchor,
                    context.TimingProfile);
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: sequenceId,
                        presentationSeed: sequenceId,
                        sourceEntityId: tileEvent.SourceEntityId,
                        cueId: GameplayVfxCueId.From(cue),
                        anchor: VfxAnchor.ForCell(
                            tileEvent.Cell,
                            context.Topology,
                            VfxAnchorSlot.CellCenter),
                        timing: delaySeconds > 0f
                            ? VfxTimingKind.Delayed
                            : VfxTimingKind.ImmediateOnTickPresentation,
                        delaySeconds: delaySeconds));
            }

            PlanActiveVisualStates(context, builder, presentationData);
        }

        private static void PlanActiveVisualStates(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            TickPresentationData presentationData)
        {
            var activeVisualStates = presentationData.TileFeatureActiveVisualStates;
            for (var i = 0; i < activeVisualStates.Count; i++)
            {
                var activeVisualState = activeVisualStates[i];
                if (activeVisualState.TileFeatureKind != TileFeatureKind.Destroy)
                {
                    continue;
                }

                var cueId = GameplayVfxCueId.From(TileFeatureVfxCue.DestroyTileLaserActive);
                var persistentKey = new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    tileId: activeVisualState.TileId,
                    cell: activeVisualState.Cell,
                    hasCell: true);
                var sequenceId = ResolveActiveVisualStateSequenceId(activeVisualState);
                builder.Add(CreateTopologySensitivePersistentRequest(
                    context,
                    sequenceId,
                    activeVisualState.SourceEntityId,
                    cueId,
                    activeVisualState.Cell,
                    VfxAnchorSlot.CellCenter,
                    persistentKey,
                    ResolveTileFeatureStyleKey(
                        context.TileFeatureVfxStyleBindings,
                        activeVisualState.TileId)));
            }
        }

        private static void PlanVisualStates(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            TickPresentationData presentationData)
        {
            var states = presentationData.TileFeatureVisualStates;
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!state.IsActive ||
                    !TryResolveActiveLoopCue(state.TileFeatureKind, out var cue))
                {
                    continue;
                }

                var cueId = GameplayVfxCueId.From(cue);
                var key = new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    tileId: state.TileId,
                    cell: state.Cell,
                    hasCell: true,
                    effectIndex: ActiveLoopEffectIndex);
                var sequenceId = ResolveStateSequenceId(context.TickIndex, i, state);
                var delaySeconds = state.VisibilityGate.HasGate
                    ? PresentationTimingResolver.ResolveDelaySeconds(
                        state.VisibilityGate.TimingAnchor,
                        context.TimingProfile)
                    : 0f;
                builder.Add(CreateTopologySensitivePersistentRequest(
                    context,
                    sequenceId,
                    state.SourceEntityId,
                    cueId,
                    state.Cell,
                    VfxAnchorSlot.CellCenter,
                    key,
                    ResolveTileFeatureStyleKey(context.TileFeatureVfxStyleBindings, state.TileId),
                    delaySeconds));
            }
        }

        private static void PlanVisibleVisualStates(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            TickPresentationData presentationData)
        {
            var states = presentationData.TileFeatureVisibleVisualStates;
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!state.IsActive || state.TileFeatureKind != TileFeatureKind.Button)
                {
                    continue;
                }

                var cueId = GameplayVfxCueId.From(TileFeatureVfxCue.ButtonVisibleLoop);
                var key = new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    tileId: state.TileId,
                    cell: state.Cell,
                    hasCell: true,
                    effectIndex: VisibleLoopEffectIndex);
                var sequenceId = ResolveVisibleStateSequenceId(context.TickIndex, i, state);
                builder.Add(CreateTopologySensitivePersistentRequest(
                    context,
                    sequenceId,
                    state.SourceEntityId,
                    cueId,
                    state.Cell,
                    VfxAnchorSlot.CellFloor,
                    key,
                    ResolveTileFeatureStyleKey(context.TileFeatureVfxStyleBindings, state.TileId)));
            }
        }

        private static GameplayVfxRequest CreateTopologySensitivePersistentRequest(
            GameplayVfxPlanningContext context,
            int sequenceId,
            int sourceEntityId,
            GameplayVfxCueId cueId,
            SurfaceCell cell,
            VfxAnchorSlot slot,
            VfxPersistentKey key,
            VfxStyleKey styleKey,
            float delaySeconds = 0f)
        {
            var transition = context.TopologyTransition;
            var anchorTopology = transition.HasTopologyMotion
                ? transition.AnchorTopology
                : context.Topology;
            var anchorMode = transition.HasTopologyMotion && !transition.IsTransitionCompletionReconcile
                ? GameplayVfxTopologyAnchorMode.SourceDuringTransition
                : GameplayVfxTopologyAnchorMode.DestinationAfterTransition;
            if (!transition.HasTopologyMotion)
            {
                anchorMode = GameplayVfxTopologyAnchorMode.Committed;
            }

            return new GameplayVfxRequest(
                tickIndex: context.TickIndex,
                sequenceId: sequenceId,
                presentationSeed: sequenceId,
                sourceEntityId: sourceEntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    cell,
                    anchorTopology,
                    slot),
                timing: delaySeconds > 0f
                    ? VfxTimingKind.Delayed
                    : VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: key,
                styleKey: styleKey,
                delaySeconds: delaySeconds,
                topologyAnchorMode: anchorMode,
                topologyStopMode: GameplayVfxTopologyStopMode.HardClearAtTransitionStart,
                topologySpawnMode: GameplayVfxTopologySpawnMode.SuppressDuringTransition,
                completionReplayPolicy: GameplayVfxCompletionReplayPolicy.SteadyStatePersistentLoop);
        }

        private static VfxStyleKey ResolveTileFeatureStyleKey(
            IReadOnlyList<TileFeatureVfxStyleBinding> bindings,
            int tileId)
        {
            if (bindings == null || tileId <= 0)
            {
                return VfxStyleKey.Default;
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.TileId == tileId)
                {
                    return binding.StyleKey;
                }
            }

            return VfxStyleKey.Default;
        }

        private static bool TryResolveActiveLoopCue(
            TileFeatureKind kind,
            out TileFeatureVfxCue cue)
        {
            switch (kind)
            {
                case TileFeatureKind.Button:
                    cue = TileFeatureVfxCue.ButtonActiveLoop;
                    return true;
                case TileFeatureKind.Barricade:
                    cue = TileFeatureVfxCue.BarricadeActiveLoop;
                    return true;
                case TileFeatureKind.Exit:
                    cue = TileFeatureVfxCue.ExitOpenLoop;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        private static bool TryResolveCue(
            in TilePresentationEvent tileEvent,
            out TileFeatureVfxCue cue)
        {
            switch (tileEvent.EventKind)
            {
                case TilePresentationEventKind.ButtonActivated:
                    cue = TileFeatureVfxCue.ButtonActivated;
                    return true;
                case TilePresentationEventKind.DestroyTileTriggered:
                    cue = TileFeatureVfxCue.DestroyTileTriggered;
                    return true;
                case TilePresentationEventKind.SlideTileRedirected:
                    return TryResolveSlideCue(tileEvent.Direction, out cue);
                case TilePresentationEventKind.BarricadeBlocked:
                    return TryResolveBarricadeBlockedCue(tileEvent.Direction, out cue);
                case TilePresentationEventKind.BarricadeCrushed:
                    cue = TileFeatureVfxCue.BarricadeCrushed;
                    return true;
                case TilePresentationEventKind.ExitOpened:
                    cue = TileFeatureVfxCue.ExitOpened;
                    return true;
                case TilePresentationEventKind.ExitObjectiveCleared:
                    cue = TileFeatureVfxCue.ExitObjectiveCleared;
                    return true;
                case TilePresentationEventKind.ExitEntered:
                    cue = TileFeatureVfxCue.ExitEntered;
                    return true;
                case TilePresentationEventKind.MoonBlockGenerated:
                    cue = TileFeatureVfxCue.MoonBlockGenerated;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        private static bool TryResolveSlideCue(Direction direction, out TileFeatureVfxCue cue)
        {
            switch (direction)
            {
                case Direction.Up:
                    cue = TileFeatureVfxCue.SlideTileRedirectedUp;
                    return true;
                case Direction.Right:
                    cue = TileFeatureVfxCue.SlideTileRedirectedRight;
                    return true;
                case Direction.Down:
                    cue = TileFeatureVfxCue.SlideTileRedirectedDown;
                    return true;
                case Direction.Left:
                    cue = TileFeatureVfxCue.SlideTileRedirectedLeft;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        private static bool TryResolveBarricadeBlockedCue(Direction direction, out TileFeatureVfxCue cue)
        {
            switch (direction)
            {
                case Direction.Up:
                    cue = TileFeatureVfxCue.BarricadeBlockedUp;
                    return true;
                case Direction.Right:
                    cue = TileFeatureVfxCue.BarricadeBlockedRight;
                    return true;
                case Direction.Down:
                    cue = TileFeatureVfxCue.BarricadeBlockedDown;
                    return true;
                case Direction.Left:
                    cue = TileFeatureVfxCue.BarricadeBlockedLeft;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        private static int ResolveSequenceId(
            int tickIndex,
            int eventIndex,
            in TilePresentationEvent tileEvent)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ eventIndex;
                hash = (hash * 397) ^ (int)tileEvent.EventKind;
                hash = (hash * 397) ^ tileEvent.TileId;
                hash = (hash * 397) ^ tileEvent.Cell.GetHashCode();
                hash = (hash * 397) ^ (int)tileEvent.Direction;
                hash = (hash * 397) ^ tileEvent.SourceEntityId;
                hash = (hash * 397) ^ tileEvent.OwnerEntityId;
                hash = (hash * 397) ^ tileEvent.TargetEntityId;
                hash = (hash * 397) ^ tileEvent.TimingAnchor.ActionPlanId;
                hash = (hash * 397) ^ tileEvent.TimingAnchor.LocalActionIndex;
                return hash != 0 ? hash : eventIndex + 1;
            }
        }

        private static int ResolveEntranceSpawnSequenceId(
            int tickIndex,
            EntranceSpawnRequestKey key)
        {
            unchecked
            {
                var hash = (int)TileFeatureVfxCue.EntranceSpawn;
                hash = (hash * 397) ^ tickIndex;
                hash = (hash * 397) ^ key.EntityId;
                hash = (hash * 397) ^ (int)key.Reason;
                hash = (hash * 397) ^ key.SourceTileId;
                hash = (hash * 397) ^ key.SourceCell.GetHashCode();
                return hash != 0 ? hash : key.EntityId != 0 ? key.EntityId : 1;
            }
        }

        private static int ResolveStateSequenceId(
            int tickIndex,
            int stateIndex,
            in TileFeatureVisualState state)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ stateIndex;
                hash = (hash * 397) ^ state.TileId;
                hash = (hash * 397) ^ state.Cell.GetHashCode();
                hash = (hash * 397) ^ (int)state.TileFeatureKind;
                hash = (hash * 397) ^ state.SourceEntityId;
                hash = (hash * 397) ^ state.OwnerEntityId;
                hash = (hash * 397) ^ state.TeamId;
                return hash != 0 ? hash : stateIndex + 1;
            }
        }

        private static int ResolveVisibleStateSequenceId(
            int tickIndex,
            int stateIndex,
            in TileFeatureVisualState state)
        {
            unchecked
            {
                var hash = (int)TileFeatureVfxCue.ButtonVisibleLoop;
                hash = (hash * 397) ^ tickIndex;
                hash = (hash * 397) ^ stateIndex;
                hash = (hash * 397) ^ state.TileId;
                hash = (hash * 397) ^ state.Cell.GetHashCode();
                hash = (hash * 397) ^ state.SourceEntityId;
                hash = (hash * 397) ^ state.OwnerEntityId;
                hash = (hash * 397) ^ state.TeamId;
                return hash != 0 ? hash : stateIndex + 1;
            }
        }

        private static int ResolveActiveVisualStateSequenceId(in TileFeatureActiveVisualState activeVisualState)
        {
            unchecked
            {
                var hash = (int)TileFeatureVfxCue.DestroyTileLaserActive;
                hash = (hash * 397) ^ activeVisualState.TileId;
                hash = (hash * 397) ^ activeVisualState.Cell.GetHashCode();
                hash = (hash * 397) ^ activeVisualState.SourceEntityId;
                return hash != 0 ? hash : activeVisualState.TileId != 0 ? activeVisualState.TileId : 1;
            }
        }
    }

    public sealed class GravityFieldVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        private const int ChargingAreaEffectIndex = 1;
        private const int ActiveAreaEffectIndex = 2;

        public GameplayVfxFamily Family => GameplayVfxFamily.GravityField;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var presentationData = context.PresentationData;
            if (presentationData == null)
            {
                return;
            }

            PlanEvents(context, builder, presentationData);
            PlanVisualStates(context, builder, presentationData);
        }

        private static void PlanEvents(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            TickPresentationData presentationData)
        {
            var events = presentationData.GravityFieldEvents;
            for (var i = 0; i < events.Count; i++)
            {
                var fieldEvent = events[i];
                if (TryResolveCue(fieldEvent.EventKind, out var cue))
                {
                    AddEventRequest(context, builder, fieldEvent, i, cue);
                }
            }
        }

        private static void AddEventRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in GravityFieldPresentationEvent fieldEvent,
            int eventIndex,
            GravityFieldVfxCue cue)
        {
            var sequenceId = ResolveEventSequenceId(context.TickIndex, eventIndex, fieldEvent, cue);
            builder.Add(
                new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: sequenceId,
                    presentationSeed: sequenceId,
                    sourceEntityId: fieldEvent.EmitterEntityId,
                    cueId: GameplayVfxCueId.From(cue),
                    anchor: VfxAnchor.ForCell(
                        fieldEvent.Cell,
                        context.Topology,
                        VfxAnchorSlot.CellCenter),
                    timing: VfxTimingKind.ImmediateOnTickPresentation));
        }

        private static void PlanVisualStates(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            TickPresentationData presentationData)
        {
            var states = presentationData.GravityFieldVisualStates;
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state.EmitterEntityId <= 0)
                {
                    continue;
                }

                if (TryResolveAreaCue(state.Phase, out var areaCue, out var effectIndex))
                {
                    AddAreaRequest(context, builder, state, areaCue, effectIndex);
                }

                var lockedTargets = state.LockedTargetEntityIds ?? Array.Empty<int>();
                for (var targetIndex = 0; targetIndex < lockedTargets.Count; targetIndex++)
                {
                    var targetEntityId = lockedTargets[targetIndex];
                    if (targetEntityId <= 0)
                    {
                        continue;
                    }

                    AddLockedTargetRequest(context, builder, state, targetEntityId, targetIndex);
                }
            }
        }

        private static void AddAreaRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in GravityFieldVisualState state,
            GravityFieldVfxCue cue,
            int effectIndex)
        {
            var cueId = GameplayVfxCueId.From(cue);
            var key = new VfxPersistentKey(
                cueId,
                VfxAnchorKind.Cell,
                entityId: state.EmitterEntityId,
                cell: state.Cell,
                hasCell: true,
                effectIndex: effectIndex);
            var sequenceId = ResolveStateSequenceId(context.TickIndex, state, effectIndex);
            builder.Add(
                new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: sequenceId,
                    presentationSeed: sequenceId,
                    sourceEntityId: state.EmitterEntityId,
                    cueId: cueId,
                    anchor: VfxAnchor.ForCell(
                        state.Cell,
                        context.Topology,
                        VfxAnchorSlot.CellCenter),
                    timing: VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: true,
                    persistentKey: key,
                    completionReplayPolicy: GameplayVfxCompletionReplayPolicy.SteadyStatePersistentLoop));
        }

        private static void AddLockedTargetRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in GravityFieldVisualState state,
            int targetEntityId,
            int targetIndex)
        {
            var cueId = GameplayVfxCueId.From(GravityFieldVfxCue.LockedTarget);
            var key = new VfxPersistentKey(
                cueId,
                VfxAnchorKind.Entity,
                entityId: targetEntityId,
                cell: state.Cell,
                hasCell: true,
                effectIndex: state.EmitterEntityId);
            var sequenceId = ResolveLockedTargetSequenceId(context.TickIndex, state, targetEntityId, targetIndex);
            builder.Add(
                new GameplayVfxRequest(
                    tickIndex: context.TickIndex,
                    sequenceId: sequenceId,
                    presentationSeed: sequenceId,
                    sourceEntityId: state.EmitterEntityId,
                    cueId: cueId,
                    anchor: VfxAnchor.ForEntity(
                        targetEntityId,
                        VfxAnchorSlot.EntityCenter,
                        state.Cell,
                        context.Topology,
                        hasFallbackCell: true),
                    timing: VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: true,
                    persistentKey: key));
        }

        private static bool TryResolveCue(
            GravityFieldPresentationEventKind kind,
            out GravityFieldVfxCue cue)
        {
            switch (kind)
            {
                case GravityFieldPresentationEventKind.Activated:
                    cue = GravityFieldVfxCue.ActiveStarted;
                    return true;
                case GravityFieldPresentationEventKind.Expired:
                    cue = GravityFieldVfxCue.ChargeStarted;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        private static bool TryResolveAreaCue(
            GravityFieldPhase phase,
            out GravityFieldVfxCue cue,
            out int effectIndex)
        {
            switch (phase)
            {
                case GravityFieldPhase.Charging:
                    cue = GravityFieldVfxCue.ChargingArea;
                    effectIndex = ChargingAreaEffectIndex;
                    return true;
                case GravityFieldPhase.Active:
                    cue = GravityFieldVfxCue.ActiveArea;
                    effectIndex = ActiveAreaEffectIndex;
                    return true;
                default:
                    cue = default;
                    effectIndex = 0;
                    return false;
            }
        }

        private static int ResolveEventSequenceId(
            int tickIndex,
            int eventIndex,
            in GravityFieldPresentationEvent fieldEvent,
            GravityFieldVfxCue cue)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ eventIndex;
                hash = (hash * 397) ^ (int)fieldEvent.EventKind;
                hash = (hash * 397) ^ (int)cue;
                hash = (hash * 397) ^ fieldEvent.EmitterEntityId;
                hash = (hash * 397) ^ fieldEvent.Cell.GetHashCode();
                hash = (hash * 397) ^ fieldEvent.TargetEntityId;
                return hash != 0 ? hash : eventIndex + 1;
            }
        }

        private static int ResolveStateSequenceId(
            int tickIndex,
            in GravityFieldVisualState state,
            int effectIndex)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ state.EmitterEntityId;
                hash = (hash * 397) ^ (int)state.Phase;
                hash = (hash * 397) ^ state.Cell.GetHashCode();
                hash = (hash * 397) ^ effectIndex;
                return hash != 0 ? hash : effectIndex;
            }
        }

        private static int ResolveLockedTargetSequenceId(
            int tickIndex,
            in GravityFieldVisualState state,
            int targetEntityId,
            int targetIndex)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ state.EmitterEntityId;
                hash = (hash * 397) ^ targetEntityId;
                hash = (hash * 397) ^ targetIndex;
                hash = (hash * 397) ^ state.Cell.GetHashCode();
                return hash != 0 ? hash : targetIndex + 1;
            }
        }
    }

    public sealed class TerrainVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Terrain;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }

    public sealed class ProjectileVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Projectile;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var presentationData = context.PresentationData;
            if (presentationData == null)
            {
                return;
            }

            var flightCueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileFlight);
            var activeCueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileActive);
            var releaseSignals = presentationData.ForwardCellProjectileReleaseSignals;
            for (var i = 0; i < releaseSignals.Count; i++)
            {
                var signal = releaseSignals[i];
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.PresentationKey,
                        presentationSeed: signal.PresentationKey,
                        sourceEntityId: signal.SourceEnemyId,
                        cueId: activeCueId,
                        anchor: VfxAnchor.ForCell(
                            signal.SourceCell,
                            context.Topology,
                            VfxAnchorSlot.CellCenter),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: false,
                        persistentKey: VfxPersistentKey.None));
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.PresentationKey,
                        presentationSeed: signal.PresentationKey,
                        sourceEntityId: signal.SourceEnemyId,
                        cueId: flightCueId,
                        anchor: VfxAnchor.FromEntityToCell(
                            signal.SourceEnemyId,
                            signal.TargetCell,
                            context.Topology),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: false,
                        persistentKey: VfxPersistentKey.None));
            }

            var impactCueId = GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellImpact);
            var impactSignals = presentationData.ForwardCellImpactSignals;
            for (var i = 0; i < impactSignals.Count; i++)
            {
                var signal = impactSignals[i];
                builder.Add(
                    new GameplayVfxRequest(
                        tickIndex: context.TickIndex,
                        sequenceId: signal.PresentationKey,
                        presentationSeed: signal.PresentationKey,
                        sourceEntityId: signal.SourceEnemyId,
                        cueId: impactCueId,
                        anchor: VfxAnchor.ForCell(
                            signal.TargetCell,
                            context.Topology,
                            VfxAnchorSlot.CellFloor),
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: false,
                        persistentKey: VfxPersistentKey.None));
            }
        }
    }

    public sealed class ObjectiveStageVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.ObjectiveStage;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }
}
