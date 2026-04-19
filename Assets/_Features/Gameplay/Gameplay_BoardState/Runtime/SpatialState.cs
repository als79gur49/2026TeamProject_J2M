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
        PhasedVisible = 4,
        PhasedHiddenByTopology = 5,
        InvalidSourceCombination = 6,
    }

    internal enum PhasedRuntimeStateOwnerKind
    {
        None = 0,
        MovementPreMovement = 1,
        EnemyPreMovement = 2,
        DebugForced = 3,
    }

    internal struct PhasedRuntimeState
    {
        public PhasedRuntimeStateOwnerKind ownerKind;
        public int sequence;
        public int enteredTick;
        public int exitTickExclusive;

        public bool IsActive => ownerKind != PhasedRuntimeStateOwnerKind.None;
    }

    internal readonly struct PhasedSnapshotEntry
    {
        public PhasedSnapshotEntry(int entityId, PhasedRuntimeState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public PhasedRuntimeState State { get; }
    }

    internal static class PhasedRuntimeStateQueries
    {
        public static PhasedRuntimeState BeginMovementPreMovement(
            in PhasedRuntimeState previousState,
            int tickIndex,
            int exitTickExclusive = 0)
        {
            return CreateActive(
                PhasedRuntimeStateOwnerKind.MovementPreMovement,
                previousState,
                tickIndex,
                exitTickExclusive);
        }

        public static PhasedRuntimeState BeginEnemyPreMovement(
            in PhasedRuntimeState previousState,
            int tickIndex,
            int exitTickExclusive = 0)
        {
            return CreateActive(
                PhasedRuntimeStateOwnerKind.EnemyPreMovement,
                previousState,
                tickIndex,
                exitTickExclusive);
        }

        public static PhasedRuntimeState ForceDebug(
            in PhasedRuntimeState previousState,
            int tickIndex,
            int exitTickExclusive = 0)
        {
            return CreateActive(
                PhasedRuntimeStateOwnerKind.DebugForced,
                previousState,
                tickIndex,
                exitTickExclusive);
        }

        public static PhasedRuntimeState Clear()
        {
            return default;
        }

        private static PhasedRuntimeState CreateActive(
            PhasedRuntimeStateOwnerKind ownerKind,
            in PhasedRuntimeState previousState,
            int tickIndex,
            int exitTickExclusive)
        {
            if (ownerKind == PhasedRuntimeStateOwnerKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "Active phased state requires a concrete owner.");
            }

            if (tickIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickIndex), "Phase entered tick must be non-negative.");
            }

            if (exitTickExclusive > 0 &&
                exitTickExclusive <= tickIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exitTickExclusive),
                    "Phase exit tick must be greater than entered tick when provided.");
            }

            return new PhasedRuntimeState
            {
                ownerKind = ownerKind,
                sequence = Math.Max(1, previousState.sequence + 1),
                enteredTick = tickIndex,
                exitTickExclusive = exitTickExclusive,
            };
        }
    }

    internal enum PhasedSourceEmittingStage
    {
        None = 0,
        PreMovementState = 1,
        Debug = 2,
    }

    internal enum PhasedTargetabilityMode
    {
        FreshSelectionSuppressed = 0,
        FreshSelectionSuppressedWithCurrentEnemyLockRetention = 1,
    }

    internal enum PhasedRequestedTerminalSettleMode
    {
        AnchoredLikeDefault = 0,
    }

    internal enum PhasedReservationReadClass
    {
        None = 0,
        CellOnlyPreSettle = 1,
    }

    internal enum PhasedEarliestObservableSnapshot
    {
        None = 0,
        PlanSnapshot = 1,
        AuthoritativeWrite = 2,
    }

    internal readonly struct PhasedSourceMetadata
    {
        public PhasedSourceMetadata(
            PhasedRuntimeStateOwnerKind ownerKind,
            PhasedSourceEmittingStage emittingStage,
            string timingRow,
            PhasedTargetabilityMode targetabilityMode,
            PhasedRequestedTerminalSettleMode requestedTerminalSettleMode,
            PhasedReservationReadClass reservationReadClass,
            PhasedEarliestObservableSnapshot earliestObservableSnapshot,
            string cancelReplaceRule,
            string lifecycleRule)
        {
            if (ownerKind == PhasedRuntimeStateOwnerKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "Metadata rows require a concrete phased owner.");
            }

            OwnerKind = ownerKind;
            EmittingStage = emittingStage;
            TimingRow = timingRow ?? string.Empty;
            TargetabilityMode = targetabilityMode;
            RequestedTerminalSettleMode = requestedTerminalSettleMode;
            ReservationReadClass = reservationReadClass;
            EarliestObservableSnapshot = earliestObservableSnapshot;
            CancelReplaceRule = cancelReplaceRule ?? string.Empty;
            LifecycleRule = lifecycleRule ?? string.Empty;
        }

        public PhasedRuntimeStateOwnerKind OwnerKind { get; }

        public PhasedSourceEmittingStage EmittingStage { get; }

        public string TimingRow { get; }

        public PhasedTargetabilityMode TargetabilityMode { get; }

        public PhasedRequestedTerminalSettleMode RequestedTerminalSettleMode { get; }

        public PhasedReservationReadClass ReservationReadClass { get; }

        public PhasedEarliestObservableSnapshot EarliestObservableSnapshot { get; }

        public string CancelReplaceRule { get; }

        public string LifecycleRule { get; }
    }

    internal static class PhasedSourceMetadataCatalog
    {
        public static bool TryGet(
            PhasedRuntimeStateOwnerKind ownerKind,
            out PhasedSourceMetadata metadata)
        {
            switch (ownerKind)
            {
                case PhasedRuntimeStateOwnerKind.MovementPreMovement:
                    metadata = new PhasedSourceMetadata(
                        ownerKind,
                        PhasedSourceEmittingStage.PreMovementState,
                        "PlayerFlipWindup",
                        PhasedTargetabilityMode.FreshSelectionSuppressed,
                        PhasedRequestedTerminalSettleMode.AnchoredLikeDefault,
                        PhasedReservationReadClass.None,
                        PhasedEarliestObservableSnapshot.PlanSnapshot,
                        cancelReplaceRule: "ExplicitClearRequiredForExclusiveStateReplace",
                        lifecycleRule: "EnterDuringFlipWindup|SustainAcrossWindup|ExitOnExecuteOrCancel");
                    return true;

                case PhasedRuntimeStateOwnerKind.EnemyPreMovement:
                    metadata = new PhasedSourceMetadata(
                        ownerKind,
                        PhasedSourceEmittingStage.PreMovementState,
                        "EnemyLockedTargetCrossThroughValidator",
                        PhasedTargetabilityMode.FreshSelectionSuppressedWithCurrentEnemyLockRetention,
                        PhasedRequestedTerminalSettleMode.AnchoredLikeDefault,
                        PhasedReservationReadClass.CellOnlyPreSettle,
                        PhasedEarliestObservableSnapshot.PlanSnapshot,
                        cancelReplaceRule: "ExplicitClearRequiredForForeignOwnerReplace",
                        lifecycleRule: "EnterOnExecuteWindow|ResolveOneFixedCrossThroughAttempt|ExitWhenWindowCloses");
                    return true;

                case PhasedRuntimeStateOwnerKind.DebugForced:
                    metadata = new PhasedSourceMetadata(
                        ownerKind,
                        PhasedSourceEmittingStage.Debug,
                        "DebugForced",
                        PhasedTargetabilityMode.FreshSelectionSuppressed,
                        PhasedRequestedTerminalSettleMode.AnchoredLikeDefault,
                        PhasedReservationReadClass.None,
                        PhasedEarliestObservableSnapshot.AuthoritativeWrite,
                        cancelReplaceRule: "DebugWriteMayClearOnlyItsOwnOrInactiveState",
                        lifecycleRule: "ManualEnterExitOnly");
                    return true;

                default:
                    metadata = default;
                    return false;
            }
        }
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
            return Resolve(entity, topology, jumpState, phasedState: null);
        }

        public static ResolvedSpatialState Resolve(
            in EntityState entity,
            CubeTopologyState topology,
            EnemyJumpRuntimeState? jumpState,
            PhasedRuntimeState? phasedState)
        {
            return Resolve(
                entity.boardPresence,
                jumpState,
                phasedState,
                topology.IsFaceActive(entity.position.face));
        }

        public static ResolvedSpatialState Resolve(
            EntityBoardPresence boardPresence,
            EnemyJumpRuntimeState? jumpState,
            bool isFaceActive)
        {
            return Resolve(boardPresence, jumpState, phasedState: null, isFaceActive);
        }

        public static ResolvedSpatialState Resolve(
            EntityBoardPresence boardPresence,
            EnemyJumpRuntimeState? jumpState,
            PhasedRuntimeState? phasedState,
            bool isFaceActive)
        {
            var phase = jumpState?.phase ?? EnemyJumpPhase.None;
            var hasPhasedState = phasedState.HasValue && phasedState.Value.IsActive;
            switch (boardPresence)
            {
                case EntityBoardPresence.Occupying:
                    if (hasPhasedState)
                    {
                        if (phase != EnemyJumpPhase.None)
                        {
                            throw CreateInvalidSourceCombination(boardPresence, phase, phasedState);
                        }

                        return new ResolvedSpatialState(
                            SpatialState.Phased,
                            claimsAuthoritativeOccupancy: true,
                            isGameplayVisible: isFaceActive,
                            isFaceActive
                                ? ResolvedSpatialStateSource.PhasedVisible
                                : ResolvedSpatialStateSource.PhasedHiddenByTopology);
                    }

                    if (phase == EnemyJumpPhase.Airborne)
                    {
                        throw CreateInvalidSourceCombination(boardPresence, phase, phasedState);
                    }

                    return new ResolvedSpatialState(
                        SpatialState.Anchored,
                        claimsAuthoritativeOccupancy: true,
                        isGameplayVisible: isFaceActive,
                        isFaceActive
                            ? ResolvedSpatialStateSource.DefaultAnchored
                            : ResolvedSpatialStateSource.AnchoredHiddenByTopology);

                case EntityBoardPresence.Detached:
                    if (hasPhasedState)
                    {
                        throw CreateInvalidSourceCombination(boardPresence, phase, phasedState);
                    }

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
                        throw CreateInvalidSourceCombination(boardPresence, phase, phasedState);
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
            EnemyJumpPhase jumpPhase,
            PhasedRuntimeState? phasedState)
        {
            return new InvalidOperationException(
                $"Invalid spatial state source combination: Presence={boardPresence}|JumpPhase={jumpPhase}|PhasedOwner={phasedState?.ownerKind ?? PhasedRuntimeStateOwnerKind.None}|Source={ResolvedSpatialStateSource.InvalidSourceCombination}");
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
            return spatialState.ClaimsAuthoritativeOccupancy &&
                   spatialState.IsGameplayVisible;
        }

        public static bool ParticipatesInTraversalBlocking(in ResolvedSpatialState spatialState)
        {
            return ParticipatesInGameplayQueries(spatialState);
        }

        public static bool ParticipatesInSettlementBlocking(in ResolvedSpatialState spatialState)
        {
            EnsureProductionSupported(spatialState.Kind);
            return spatialState.ClaimsAuthoritativeOccupancy;
        }

        public static bool UsesAuthoritativeSettlementOccupancy(SpatialState requestedTerminalState)
        {
            EnsureProductionSupported(requestedTerminalState);
            return requestedTerminalState == SpatialState.Phased;
        }

        public static bool ParticipatesInTargetSelection(in ResolvedSpatialState spatialState)
        {
            EnsureProductionSupported(spatialState.Kind);
            return spatialState.Kind != SpatialState.Phased &&
                   ParticipatesInGameplayQueries(spatialState);
        }

        public static void EnsureProductionSupported(SpatialState kind)
        {
            if (kind == SpatialState.Attached)
            {
                throw new InvalidOperationException(
                    $"SpatialState {kind} is reserved for future runtime support and must not be consumed by current legality/query code.");
            }
        }

        public static void EnsureLiveProducerClosed(SpatialState kind)
        {
            if (kind == SpatialState.Attached)
            {
                throw new InvalidOperationException(
                    $"SpatialState {kind} is reserved for future runtime support and must not be emitted by live runtime producers yet.");
            }
        }
    }
}
