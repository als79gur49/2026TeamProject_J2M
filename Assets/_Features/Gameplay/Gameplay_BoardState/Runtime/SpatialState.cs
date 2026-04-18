using System;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    internal enum SpatialState
    {
        Anchored = 0,
        Airborne = 1,
        Phased = 2,
        Attached = 3,
    }

    internal enum ResolvedSpatialStateSource
    {
        DefaultAnchored = 0,
        AnchoredHiddenByTopology = 1,
        JumpAirborne = 2,
        DetachedNonAirborne = 3,
        InvalidSourceCombination = 4,
    }

    internal readonly struct ResolvedSpatialState
    {
        public ResolvedSpatialState(
            SpatialState kind,
            bool claimsAuthoritativeOccupancy,
            bool isGameplayVisible,
            ResolvedSpatialStateSource source)
        {
            Kind = kind;
            ClaimsAuthoritativeOccupancy = claimsAuthoritativeOccupancy;
            IsGameplayVisible = isGameplayVisible;
            Source = source;
        }

        public SpatialState Kind { get; }

        public bool ClaimsAuthoritativeOccupancy { get; }

        public bool IsGameplayVisible { get; }

        public ResolvedSpatialStateSource Source { get; }
    }

    internal static class SpatialStateResolver
    {
        public static ResolvedSpatialState Resolve(
            in EntityState entity,
            CubeTopologyState topology,
            EnemyJumpRuntimeState? jumpState)
        {
            return Resolve(
                entity.boardPresence,
                jumpState,
                topology.IsFaceActive(entity.position.face));
        }

        public static ResolvedSpatialState Resolve(
            EntityBoardPresence boardPresence,
            EnemyJumpRuntimeState? jumpState,
            bool isFaceActive)
        {
            var phase = jumpState?.phase ?? EnemyJumpPhase.None;
            switch (boardPresence)
            {
                case EntityBoardPresence.Occupying:
                    if (phase == EnemyJumpPhase.Airborne)
                    {
                        throw CreateInvalidSourceCombination(boardPresence, phase);
                    }

                    return new ResolvedSpatialState(
                        SpatialState.Anchored,
                        claimsAuthoritativeOccupancy: true,
                        isGameplayVisible: isFaceActive,
                        isFaceActive
                            ? ResolvedSpatialStateSource.DefaultAnchored
                            : ResolvedSpatialStateSource.AnchoredHiddenByTopology);

                case EntityBoardPresence.Detached:
                    if (phase == EnemyJumpPhase.Airborne)
                    {
                        return new ResolvedSpatialState(
                            SpatialState.Airborne,
                            claimsAuthoritativeOccupancy: false,
                            isGameplayVisible: false,
                            ResolvedSpatialStateSource.JumpAirborne);
                    }

                    if (phase != EnemyJumpPhase.None)
                    {
                        throw CreateInvalidSourceCombination(boardPresence, phase);
                    }

                    return new ResolvedSpatialState(
                        SpatialState.Anchored,
                        claimsAuthoritativeOccupancy: false,
                        isGameplayVisible: false,
                        ResolvedSpatialStateSource.DetachedNonAirborne);

                default:
                    throw new ArgumentOutOfRangeException(nameof(boardPresence), boardPresence, "Unsupported board presence.");
            }
        }

        private static InvalidOperationException CreateInvalidSourceCombination(
            EntityBoardPresence boardPresence,
            EnemyJumpPhase jumpPhase)
        {
            return new InvalidOperationException(
                $"Invalid spatial state source combination: Presence={boardPresence}|JumpPhase={jumpPhase}|Source={ResolvedSpatialStateSource.InvalidSourceCombination}");
        }
    }

    internal static class SpatialStateSemantics
    {
        public static bool ClaimsAuthoritativeOccupancy(in ResolvedSpatialState spatialState)
        {
            EnsureProductionSupported(spatialState.Kind);
            return spatialState.ClaimsAuthoritativeOccupancy;
        }

        public static bool ParticipatesInGameplayQueries(in ResolvedSpatialState spatialState)
        {
            EnsureProductionSupported(spatialState.Kind);
            return spatialState.Kind == SpatialState.Anchored &&
                   spatialState.ClaimsAuthoritativeOccupancy &&
                   spatialState.IsGameplayVisible;
        }

        public static bool ParticipatesInTraversalBlocking(in ResolvedSpatialState spatialState)
        {
            return ParticipatesInGameplayQueries(spatialState);
        }

        public static bool ParticipatesInSettlementBlocking(in ResolvedSpatialState spatialState)
        {
            EnsureProductionSupported(spatialState.Kind);
            return spatialState.Kind == SpatialState.Anchored &&
                   spatialState.ClaimsAuthoritativeOccupancy;
        }

        public static bool ParticipatesInTargetSelection(in ResolvedSpatialState spatialState)
        {
            return ParticipatesInGameplayQueries(spatialState);
        }

        public static void EnsureProductionSupported(SpatialState kind)
        {
            if (kind == SpatialState.Phased || kind == SpatialState.Attached)
            {
                throw new InvalidOperationException(
                    $"SpatialState {kind} is reserved for future runtime support and must not be consumed by production legality/query code.");
            }
        }
    }
}
