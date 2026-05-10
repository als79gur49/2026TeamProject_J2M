using System;
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

    public readonly struct GameplayVfxPlanningContext
    {
        public GameplayVfxPlanningContext(int tickIndex)
            : this(tickIndex, null, default)
        {
        }

        public GameplayVfxPlanningContext(
            int tickIndex,
            TickPresentationData presentationData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile = null)
        {
            TickIndex = tickIndex;
            PresentationData = presentationData;
            Topology = topology;
            TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
        }

        public int TickIndex { get; }

        public TickPresentationData PresentationData { get; }

        public CubeTopologyState Topology { get; }

        public GameplayTimingProfile TimingProfile { get; }
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
                        timing: VfxTimingKind.ImmediateOnTickPresentation,
                        isPersistent: false,
                        persistentKey: VfxPersistentKey.None));
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

            var jumpSignals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < jumpSignals.Count; i++)
            {
                var signal = jumpSignals[i];
                if (IsJumperLandingTargetCueSource(signal))
                {
                    builder.Add(
                        new GameplayVfxRequest(
                            tickIndex: context.TickIndex,
                            sequenceId: ResolveSequenceId(signal),
                            presentationSeed: signal.EntityId,
                            // PresentationSeed remains a visual variation seed; SourceEntityId is the source identity.
                            sourceEntityId: signal.EntityId,
                            cueId: GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget),
                            anchor: VfxAnchor.ForCell(
                                signal.PresentationTargetCell,
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

        private static bool IsJumperLandingTargetCueSource(in TickEnemyJumpPresentationSignal signal)
        {
            return signal.StartedWindupThisTick ||
                   signal.Outcome == TickEnemyJumpPresentationOutcome.WindupStarted;
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
        public GameplayVfxFamily Family => GameplayVfxFamily.TileFeature;

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

            var events = presentationData.TileEvents;
            for (var i = 0; i < events.Count; i++)
            {
                var tileEvent = events[i];
                if (!TryResolveCue(tileEvent, out var cue))
                {
                    continue;
                }

                var sequenceId = ResolveSequenceId(context.TickIndex, i, tileEvent);
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
                        timing: VfxTimingKind.ImmediateOnTickPresentation));
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
                return hash != 0 ? hash : eventIndex + 1;
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
                if (!TryResolveCue(fieldEvent.EventKind, out var cue))
                {
                    continue;
                }

                var sequenceId = ResolveEventSequenceId(context.TickIndex, i, fieldEvent);
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
                    persistentKey: key));
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
                    cue = GravityFieldVfxCue.Activated;
                    return true;
                case GravityFieldPresentationEventKind.Expired:
                    cue = GravityFieldVfxCue.Expired;
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
            in GravityFieldPresentationEvent fieldEvent)
        {
            unchecked
            {
                var hash = tickIndex;
                hash = (hash * 397) ^ eventIndex;
                hash = (hash * 397) ^ (int)fieldEvent.EventKind;
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
