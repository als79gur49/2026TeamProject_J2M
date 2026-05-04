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
            CubeTopologyState topology)
        {
            TickIndex = tickIndex;
            PresentationData = presentationData;
            Topology = topology;
        }

        public int TickIndex { get; }

        public TickPresentationData PresentationData { get; }

        public CubeTopologyState Topology { get; }
    }

    public sealed class PlayerVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Player;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
        }
    }

    public sealed class BoxVfxRequestPlanner : IGameplayVfxFamilyRequestPlanner
    {
        public GameplayVfxFamily Family => GameplayVfxFamily.Box;

        public void Plan(GameplayVfxPlanningContext context, GameplayVfxRequestPlanBuilder builder)
        {
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

            var jumpSignals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < jumpSignals.Count; i++)
            {
                var signal = jumpSignals[i];
                if (!IsJumperLandingTargetCueSource(signal))
                {
                    continue;
                }

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
        }

        private static bool IsJumperLandingTargetCueSource(in TickEnemyJumpPresentationSignal signal)
        {
            return signal.StartedWindupThisTick ||
                   signal.Outcome == TickEnemyJumpPresentationOutcome.WindupStarted;
        }

        private static int ResolveSequenceId(in TickEnemyJumpPresentationSignal signal)
        {
            return signal.Sequence > 0 ? signal.Sequence : signal.EntityId;
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
