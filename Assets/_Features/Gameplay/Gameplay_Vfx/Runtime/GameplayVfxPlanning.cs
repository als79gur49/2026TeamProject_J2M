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
            }
        }

        private static void AddExitRequest(
            GameplayVfxPlanningContext context,
            GameplayVfxRequestPlanBuilder builder,
            in TickEntityExitPresentationSignal signal,
            BoxVfxCue cue)
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
                        VfxAnchorSlot.CellFloor),
                    timing: VfxTimingKind.ImmediateOnTickPresentation,
                    isPersistent: false,
                    persistentKey: VfxPersistentKey.None));
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
