using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.BoardState
{
    internal enum ImpactFollowThroughSettlementOutcomeKind
    {
        Ordinary = 0,
        BarricadeReassertCrush = 1,
    }

    internal readonly struct ImpactFollowThroughSettlementEvaluation
    {
        public ImpactFollowThroughSettlementEvaluation(
            LegalityResult legalityResult,
            ImpactFollowThroughSettlementOutcomeKind outcomeKind,
            TileFeatureState barricade)
        {
            LegalityResult = legalityResult;
            OutcomeKind = outcomeKind;
            Barricade = barricade;
        }

        public LegalityResult LegalityResult { get; }

        public ImpactFollowThroughSettlementOutcomeKind OutcomeKind { get; }

        public TileFeatureState Barricade { get; }
    }

    internal static class RuntimeSettlementLegalityPolicy
    {
        public static LegalityResult EvaluateLandingPlacement(
            SettlementContext context,
            TileFeatureSettlementEvidence tileFeatureEvidence)
        {
            var tileFeatureDefinitions = tileFeatureEvidence.Definitions;
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            SpatialStateSemantics.EnsureProductionSupported(context.RequestedTerminalState);

            if (ReservationQuery.BlocksSettlement(context.ReservationStatus))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    context.ReservationStatus);
            }

            if (TryGetUnitTileFeatureSettlementBlocker(
                    context,
                    tileFeatureDefinitions,
                    out var tileFeatureBlocker))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.CreateTileFeature(tileFeatureBlocker),
                    context.ReservationStatus);
            }

            if (!SpatialStateSemantics.UsesAuthoritativeSettlementOccupancy(context.RequestedTerminalState))
            {
                if (!context.OccupancySnapshot.TryGetPlacementBlocker(
                        context.TerminalTopology,
                        context.Actor.EntityType,
                        context.TerminalCell,
                        context.Actor.EntityId,
                        out var gameplayBlocker))
                {
                    return LegalityResult.Allowed(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        context.ReservationStatus);
                }

                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(context.OccupancySnapshot.EntitiesById, gameplayBlocker),
                    context.ReservationStatus);
            }

            if (context.OccupancySnapshot.TryGetAuthoritativePlacementBlocker(
                    context.Actor.EntityType,
                    context.TerminalCell,
                    context.Actor.EntityId,
                    out var blocker))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(context.OccupancySnapshot.EntitiesById, blocker),
                    context.ReservationStatus);
            }

            if (!ShouldIgnoreUnitSettlementOccupants(context) &&
                TryGetSettlementBlockingOccupant(
                    context,
                    shouldIgnoreOccupant: null,
                    shouldTreatAsBlockingOccupant: null,
                    out var blockingOccupant))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(blockingOccupant),
                    context.ReservationStatus);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Settlement,
                context.TerminalCell,
                context.TerminalTopology,
                context.ReservationStatus);
        }

        public static LegalityResult EvaluateLandingPlacement(
            WorldSnapshot snapshot,
            EntityType entityType,
            SurfaceCell cell,
            int ignoredEntityId,
            TileFeatureSettlementEvidence tileFeatureEvidence,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return EvaluateLandingPlacement(
                new SettlementContext(
                    snapshot,
                    BuildActorRef(snapshot, ignoredEntityId, entityType),
                    cell,
                    snapshot.Topology,
                    SpatialState.Anchored,
                    reservationStatus),
                tileFeatureEvidence);
        }

        public static LegalityResult EvaluateBoxFlipLandingPlacement(SettlementContext context)
        {
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            SpatialStateSemantics.EnsureProductionSupported(context.RequestedTerminalState);

            if (ReservationQuery.BlocksSettlement(context.ReservationStatus))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    context.ReservationStatus);
            }

            if (context.OccupancySnapshot.TryGetBoxFlipPlacementBlocker(
                    context.TerminalTopology,
                    context.TerminalCell,
                    out var blocker))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(context.OccupancySnapshot.EntitiesById, blocker),
                    context.ReservationStatus);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Settlement,
                context.TerminalCell,
                context.TerminalTopology,
                context.ReservationStatus);
        }

        public static LegalityResult EvaluateJumpLandingCell(
            SettlementContext context,
            JumpLandingEvidence evidence,
            TileFeatureSettlementEvidence tileFeatureEvidence)
        {
            var tileFeatureDefinitions = tileFeatureEvidence.Definitions;
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            SpatialStateSemantics.EnsureProductionSupported(context.RequestedTerminalState);

            if (ReservationQuery.BlocksSettlement(context.ReservationStatus))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                    context.ReservationStatus);
            }

            if (TryGetUnitTileFeatureSettlementBlocker(
                    context,
                    tileFeatureDefinitions,
                    out var tileFeatureBlocker))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.CreateTileFeature(tileFeatureBlocker),
                    context.ReservationStatus);
            }

            if (context.OccupancySnapshot.TryGetAuthoritativePlacementBlocker(
                    context.Actor.EntityType,
                    context.TerminalCell,
                    context.Actor.EntityId,
                    out var blocker))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(context.OccupancySnapshot.EntitiesById, blocker),
                    context.ReservationStatus);
            }

            if (!ShouldIgnoreUnitSettlementOccupants(context) &&
                TryGetSettlementBlockingOccupant(
                        context,
                        shouldIgnoreOccupant: null,
                        shouldTreatAsBlockingOccupant: occupant => IsImpactTargetSurviving(
                            evidence.DamageProjectionSnapshot,
                            occupant.entityId),
                        out var blockingOccupant))
            {
                return LegalityResult.Blocked(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    RuntimeLegalityBlockerFactory.Create(blockingOccupant),
                    context.ReservationStatus);
            }

            return LegalityResult.Allowed(
                LegalityDomain.Settlement,
                context.TerminalCell,
                context.TerminalTopology,
                context.ReservationStatus);
        }

        public static JumpCrushLandingEvaluation EvaluateJumpCrushLandingCell(
            SettlementContext context,
            JumpLandingEvidence evidence,
            TileFeatureSettlementEvidence tileFeatureEvidence)
        {
            var tileFeatureDefinitions = tileFeatureEvidence.Definitions;
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            SpatialStateSemantics.EnsureProductionSupported(context.RequestedTerminalState);

            if (context.TerminalCell != evidence.LockedTargetCell)
            {
                return new JumpCrushLandingEvaluation(
                    EvaluateJumpLandingCell(context, evidence, tileFeatureEvidence),
                    crushedBoxEntityId: 0);
            }

            if (ReservationQuery.BlocksSettlement(context.ReservationStatus))
            {
                return new JumpCrushLandingEvaluation(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                        context.ReservationStatus),
                    crushedBoxEntityId: 0);
            }

            if (TryGetUnitTileFeatureSettlementBlocker(
                    context,
                    tileFeatureDefinitions,
                    out var tileFeatureBlocker))
            {
                return new JumpCrushLandingEvaluation(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.CreateTileFeature(tileFeatureBlocker),
                        context.ReservationStatus),
                    crushedBoxEntityId: 0);
            }

            if (!context.OccupancySnapshot.TryGetAuthoritativePlacementBlocker(
                    context.Actor.EntityType,
                    context.TerminalCell,
                    context.Actor.EntityId,
                    out var blocker))
            {
                return new JumpCrushLandingEvaluation(
                    EvaluateJumpLandingCell(context, evidence, tileFeatureEvidence),
                    crushedBoxEntityId: 0);
            }

            if (blocker.Kind != SlideStopperKind.Entity ||
                blocker.EntityType != EntityType.Box ||
                !context.OccupancySnapshot.TryGetEntity(blocker.EntityId, out var box) ||
                !HasBoxCapability(box, BoxCapabilities.JumpCrushable))
            {
                return new JumpCrushLandingEvaluation(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.Create(context.OccupancySnapshot.EntitiesById, blocker),
                        context.ReservationStatus),
                    crushedBoxEntityId: 0);
            }

            if (!ShouldIgnoreUnitSettlementOccupants(context) &&
                TryGetSettlementBlockingOccupant(
                        context,
                        shouldIgnoreOccupant: null,
                        shouldTreatAsBlockingOccupant: occupant => IsImpactTargetSurviving(
                            evidence.DamageProjectionSnapshot,
                            occupant.entityId),
                        out var blockingOccupant))
            {
                return new JumpCrushLandingEvaluation(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.Create(blockingOccupant),
                        context.ReservationStatus),
                    crushedBoxEntityId: 0);
            }

            return new JumpCrushLandingEvaluation(
                LegalityResult.Allowed(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    context.ReservationStatus),
                box.entityId);
        }

        public static LegalityResult EvaluateJumpLandingCell(
            WorldSnapshot movementSnapshot,
            WorldSnapshot damageProjectionSnapshot,
            SurfaceCell destinationCell,
            int sourceId,
            int ignoredDeadTargetId,
            TileFeatureSettlementEvidence tileFeatureEvidence,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (movementSnapshot == null)
            {
                throw new ArgumentNullException(nameof(movementSnapshot));
            }

            if (damageProjectionSnapshot == null)
            {
                throw new ArgumentNullException(nameof(damageProjectionSnapshot));
            }

            return EvaluateJumpLandingCell(
                new SettlementContext(
                    movementSnapshot,
                    BuildActorRef(movementSnapshot, sourceId, EntityType.Unit),
                    destinationCell,
                    movementSnapshot.Topology,
                    SpatialState.Anchored,
                    reservationStatus),
                new JumpLandingEvidence(damageProjectionSnapshot, destinationCell),
                tileFeatureEvidence);
        }

        public static LegalityResult EvaluateImpactFollowThrough(
            SettlementContext context,
            ImpactFollowThroughEvidence evidence,
            TileFeatureSettlementEvidence tileFeatureEvidence)
        {
            return EvaluateImpactFollowThroughDetailed(context, evidence, tileFeatureEvidence).LegalityResult;
        }

        public static ImpactFollowThroughSettlementEvaluation EvaluateImpactFollowThroughDetailed(
            SettlementContext context,
            ImpactFollowThroughEvidence evidence,
            TileFeatureSettlementEvidence tileFeatureEvidence)
        {
            var tileFeatureDefinitions = tileFeatureEvidence.Definitions;
            SpatialStateSemantics.EnsureProductionSupported(context.Actor.SpatialState.Kind);
            SpatialStateSemantics.EnsureProductionSupported(context.RequestedTerminalState);
            var modifiers = ModifierQuery.GetImpactFollowThroughModifiers(evidence);
            ImpactFollowThroughSettlementEvaluation Ordinary(LegalityResult result)
            {
                return new ImpactFollowThroughSettlementEvaluation(
                    result,
                    ImpactFollowThroughSettlementOutcomeKind.Ordinary,
                    default);
            }

            if (ReservationQuery.BlocksSettlement(context.ReservationStatus))
            {
                return Ordinary(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                        context.ReservationStatus));
            }

            if (!context.OccupancySnapshot.TryGetEntity(context.Actor.EntityId, out _) ||
                !HasAnyExistingTarget(context.OccupancySnapshot, evidence.TargetIds))
            {
                return Ordinary(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                        context.ReservationStatus));
            }

            if (!modifiers.Has(LegalityModifierId.AcceptedDestroyVacatesTarget))
            {
                return Ordinary(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.CreateReservationConflict(),
                        context.ReservationStatus));
            }

            if (TryGetImpactFollowThroughBarricadeReassertCrush(
                    context,
                    evidence,
                    tileFeatureDefinitions,
                    out var reassertingBarricade))
            {
                return new ImpactFollowThroughSettlementEvaluation(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.CreateTileFeature(reassertingBarricade),
                        context.ReservationStatus),
                    ImpactFollowThroughSettlementOutcomeKind.BarricadeReassertCrush,
                    reassertingBarricade);
            }

            if (TryGetImpactFollowThroughTileFeatureSettlementBlocker(
                    context,
                    tileFeatureDefinitions,
                    out var tileFeatureBlocker))
            {
                return Ordinary(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.CreateTileFeature(tileFeatureBlocker),
                        context.ReservationStatus));
            }

            if (context.OccupancySnapshot.TryGetSolidSemanticAt(context.TerminalCell, out var solidOccupant) &&
                !ContainsTargetId(evidence.TargetIds, solidOccupant.Entity.entityId))
            {
                return Ordinary(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.Create(solidOccupant.Entity),
                        context.ReservationStatus));
            }

            if (TryGetSettlementBlockingOccupant(
                    context,
                    shouldIgnoreOccupant: occupant =>
                        ContainsTargetId(evidence.TargetIds, occupant.entityId) ||
                        (evidence.IgnoreActiveGlideOccupants &&
                         context.OccupancySnapshot.TryGetActiveEnemyGlideState(occupant.entityId, out _)),
                    shouldTreatAsBlockingOccupant: null,
                    out var blockingOccupant))
            {
                return Ordinary(
                    LegalityResult.Blocked(
                        LegalityDomain.Settlement,
                        context.TerminalCell,
                        context.TerminalTopology,
                        RuntimeLegalityBlockerFactory.Create(blockingOccupant),
                        context.ReservationStatus));
            }

            return Ordinary(
                LegalityResult.Allowed(
                    LegalityDomain.Settlement,
                    context.TerminalCell,
                    context.TerminalTopology,
                    context.ReservationStatus));
        }

        public static LegalityResult EvaluateImpactFollowThrough(
            WorldSnapshot attackSnapshot,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            MovementImpactReservationPayload payload,
            TileFeatureSettlementEvidence tileFeatureEvidence,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            if (attackSnapshot == null)
            {
                throw new ArgumentNullException(nameof(attackSnapshot));
            }

            if (destroyResolutions == null)
            {
                throw new ArgumentNullException(nameof(destroyResolutions));
            }

            return EvaluateImpactFollowThrough(
                new SettlementContext(
                    attackSnapshot,
                    BuildActorRef(attackSnapshot, payload.Participants.ImpactSourceEntityId, EntityType.Box),
                    payload.Travel.FollowThroughCell,
                    attackSnapshot.Topology,
                    SpatialState.Anchored,
                    reservationStatus),
                new ImpactFollowThroughEvidence(
                    payload.Attack.AttackSourceEntityId,
                    payload.Participants.TargetEntityIds,
                    destroyResolutions),
                tileFeatureEvidence);
        }

        private static bool HasAnyExistingTarget(WorldSnapshot snapshot, IReadOnlyList<int> targetIds)
        {
            if (targetIds == null)
            {
                return false;
            }

            for (var i = 0; i < targetIds.Count; i++)
            {
                if (snapshot.TryGetEntity(targetIds[i], out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTargetId(IReadOnlyList<int> targetIds, int entityId)
        {
            if (targetIds == null)
            {
                return false;
            }

            for (var i = 0; i < targetIds.Count; i++)
            {
                if (targetIds[i] == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsImpactTargetSurviving(WorldSnapshot snapshot, int targetEntityId)
        {
            return snapshot.TryGetEntity(targetEntityId, out var targetEntity) &&
                   targetEntity.hp > 0 &&
                   !targetEntity.markedForDeath;
        }

        private static bool HasBoxCapability(EntityState entity, BoxCapabilities capability)
        {
            return entity.type == EntityType.Box &&
                   (entity.boxCapabilities & capability) == capability;
        }

        private static bool ShouldIgnoreUnitSettlementOccupants(SettlementContext context)
        {
            return context.Actor.EntityType == EntityType.Unit;
        }

        private static bool TryGetUnitTileFeatureSettlementBlocker(
            SettlementContext context,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out TileFeatureState tileFeatureBlocker)
        {
            if (context.Actor.EntityType != EntityType.Unit)
            {
                tileFeatureBlocker = default;
                return false;
            }

            return TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                context.OccupancySnapshot,
                tileFeatureDefinitions,
                context.TerminalCell,
                TileFeatureBlockerSubject.Unit,
                TileFeatureMovementKind.UnitSettlement,
                out tileFeatureBlocker,
                context.TerminalTopology,
                context.Actor.EntityId);
        }

        private static bool TryGetImpactFollowThroughTileFeatureSettlementBlocker(
            SettlementContext context,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out TileFeatureState tileFeatureBlocker)
        {
            return TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                context.OccupancySnapshot,
                tileFeatureDefinitions,
                context.TerminalCell,
                TileFeatureBlockerSubject.Box,
                TileFeatureMovementKind.ImpactFollowThrough,
                out tileFeatureBlocker,
                context.TerminalTopology);
        }

        private static bool TryGetImpactFollowThroughBarricadeReassertCrush(
            SettlementContext context,
            ImpactFollowThroughEvidence evidence,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out TileFeatureState barricade)
        {
            barricade = default;
            if (context.Actor.EntityType != EntityType.Box ||
                tileFeatureDefinitions.Count == 0)
            {
                return false;
            }

            var tileFeatures = new List<TileFeatureState>();
            context.OccupancySnapshot.EnumerateTileFeaturesAt(context.TerminalCell, tileFeatures);
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                if (tileFeature.Kind != TileFeatureKind.Barricade ||
                    !TryFindTileFeatureDefinition(tileFeatureDefinitions, tileFeature.TileId, out var definition))
                {
                    continue;
                }

                var activation = BarricadeEffectiveActivationPolicy.Evaluate(
                    context.OccupancySnapshot,
                    context.TerminalTopology,
                    tileFeature,
                    definition);
                if (activation.GameplayStateKind != BarricadeGameplayStateKind.ActiveSuppressedByUnit ||
                    !ContainsTargetId(evidence.TargetIds, activation.BlockingUnitId) ||
                    !HasAcceptedImpactDestroy(
                        evidence.DestroyResolutions,
                        evidence.AttackSourceId,
                        activation.BlockingUnitId))
                {
                    continue;
                }

                barricade = tileFeature;
                return true;
            }

            return false;
        }

        private static bool HasAcceptedImpactDestroy(
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            int sourceEntityId,
            int targetEntityId)
        {
            if (destroyResolutions == null || targetEntityId <= 0)
            {
                return false;
            }

            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                if (destroyResolutions[i].Accepted &&
                    destroyResolutions[i].SourceId == sourceEntityId &&
                    destroyResolutions[i].TargetId == targetEntityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindTileFeatureDefinition(
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            if (definitions != null)
            {
                for (var i = 0; i < definitions.Count; i++)
                {
                    if (definitions[i].TileId == tileId)
                    {
                        definition = definitions[i];
                        return true;
                    }
                }
            }

            definition = default;
            return false;
        }

        private static bool TryGetSettlementBlockingOccupant(
            SettlementContext context,
            Predicate<EntityState> shouldIgnoreOccupant,
            Predicate<EntityState> shouldTreatAsBlockingOccupant,
            out EntityState blockingOccupant)
        {
            var occupants = new List<EntityState>();
            context.OccupancySnapshot.EnumerateUnitsAt(context.TerminalCell, occupants);
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.entityId == context.Actor.EntityId ||
                    (shouldIgnoreOccupant != null && shouldIgnoreOccupant(occupant)) ||
                    !context.OccupancySnapshot.TryGetResolvedSpatialState(occupant.entityId, out var spatialState) ||
                    !ModifierQuery.ShouldParticipateInSettlementBlocking(spatialState) ||
                    (shouldTreatAsBlockingOccupant != null && !shouldTreatAsBlockingOccupant(occupant)))
                {
                    continue;
                }

                blockingOccupant = occupant;
                return true;
            }

            blockingOccupant = default;
            return false;
        }


        private static LegalityActorRef BuildActorRef(
            WorldSnapshot snapshot,
            int entityId,
            EntityType entityType)
        {
            return StateQuery.BuildActorRef(snapshot, entityId, entityType);
        }
    }
}
