using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Intents;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Expansion
{
    internal sealed class MovementExpander
    {
        private readonly int _projectileStateTimerTicks;
        private readonly int _slidingStateTimerTicks;

        public MovementExpander()
            : this(GameplayTimingProfile.CreateDefault())
        {
        }

        public MovementExpander(GameplayTimingProfile timingProfile)
        {
            var resolvedTimingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _projectileStateTimerTicks = resolvedTimingProfile.ProjectileStepIntervalTicks;
            _slidingStateTimerTicks = resolvedTimingProfile.BoxSlideStepIntervalTicks;
        }

        public void Expand(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            Expand(snapshot, tickIndex: 0, sortedIntents, null, buffer, rejectedReasons);
        }

        public void Expand(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<MoveIntent> sortedIntents,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            Expand(snapshot, tickIndex, sortedIntents, null, buffer, rejectedReasons);
        }

        public void Expand(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            ISet<int> playerTraversalSourceIds,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            Expand(snapshot, tickIndex: 0, sortedIntents, playerTraversalSourceIds, buffer, rejectedReasons);
        }

        public void Expand(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<MoveIntent> sortedIntents,
            ISet<int> playerTraversalSourceIds,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            Expand(
                snapshot,
                tickIndex,
                sortedIntents,
                playerTraversalSourceIds,
                buffer,
                rejectedReasons,
                barricadeBlockFacts: null,
                tileFeatureDefinitions: null,
                boxSlideStops: null,
                playerTopologyTransitionBlockedSignals: null);
        }

        public void Expand(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<MoveIntent> sortedIntents,
            ISet<int> playerTraversalSourceIds,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<BarricadeBlockFact> barricadeBlockFacts = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            List<BoxSlideStopResult> boxSlideStops = null,
            List<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (rejectedReasons == null)
            {
                throw new ArgumentNullException(nameof(rejectedReasons));
            }

            tileFeatureDefinitions ??= Array.Empty<TileFeatureRuntimeDefinition>();

            buffer.Clear();
            rejectedReasons.Clear();

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];

                if (!snapshot.TryGetEntity(intent.SourceId, out var entity))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=MissingSource");
                    continue;
                }

                if (entity.type == EntityType.Unit &&
                    !UnitSpatialQuery.IsSettledAtAnchor(snapshot, entity.entityId))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=UnitKinematicNotSettled|Anchor={FormatCell(entity.position)}");
                    continue;
                }

                ValidateSingleStepMove(entity.position.PlanarPosition, intent.Destination, intent.SourceId);

                switch (intent.CommandKind)
                {
                    case MovementCommandKind.Push:
                    case MovementCommandKind.Move:
                        ExpandMoveLike(
                            snapshot,
                            entity,
                            intent,
                            tickIndex,
                            playerTraversalSourceIds,
                            tileFeatureDefinitions,
                            buffer,
                            rejectedReasons,
                            barricadeBlockFacts,
                            boxSlideStops,
                            playerTopologyTransitionBlockedSignals);
                        break;

                    case MovementCommandKind.Flip:
                        ExpandFlip(
                            snapshot,
                            entity,
                            intent,
                            tickIndex,
                            tileFeatureDefinitions,
                            buffer,
                            rejectedReasons,
                            barricadeBlockFacts);
                        break;

                    default:
                        rejectedReasons.Add(
                            $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=UnsupportedCommand|Command={intent.CommandKind}");
                        break;
                }
            }
        }

        private void ExpandMoveLike(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            int tickIndex,
            ISet<int> playerTraversalSourceIds,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<BarricadeBlockFact> barricadeBlockFacts,
            List<BoxSlideStopResult> boxSlideStops,
            List<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals)
        {
            if (IsSlidingPushBox(source))
            {
                ExpandSlidingPushBoxMove(
                    snapshot,
                    source,
                    intent,
                    tickIndex,
                    tileFeatureDefinitions,
                    buffer,
                    rejectedReasons,
                    barricadeBlockFacts,
                    boxSlideStops);
                return;
            }

            var delta = ResolveIntentDelta(source.position, intent.Destination);
            var stepFacing = ResolveCardinalFacing(
                delta,
                "Movement intents must remain orthogonal single-step commands.");
            var usesPlayerTraversal = playerTraversalSourceIds != null && playerTraversalSourceIds.Contains(source.entityId);
            if (!TryResolveTraversalStep(
                    snapshot,
                    source,
                    delta,
                    usesPlayerTraversal,
                    out var destinationCell,
                    out var rotationKind,
                    out var updatedTopology,
                    out var traversalStepResolved))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=TraversalRejected|Origin={FormatCell(source.position)}");
                return;
            }

            var movementTopology = rotationKind == CubeRotationKind.None
                ? snapshot.Topology
                : updatedTopology;

            if (intent.CommandKind == MovementCommandKind.Move &&
                usesPlayerTraversal &&
                EntityRolePolicy.IsPlayerUnit(source) &&
                rotationKind != CubeRotationKind.None)
            {
                var transitionContext = CreateTraverseContext(
                    snapshot,
                    source,
                    destinationCell,
                    movementTopology,
                    rotationKind,
                    updatedTopology);
                var transitionLegality = RuntimeTraversalLegalityPolicy.EvaluateTopologyTransitionTileFeatureGuard(
                    transitionContext,
                    new TileFeatureTraversalEvidence(tileFeatureDefinitions));
                if (transitionLegality.Verdict == LegalityVerdict.Blocked)
                {
                    AddPlayerTopologyTransitionBlockedSignalIfNeeded(
                        playerTopologyTransitionBlockedSignals,
                        snapshot,
                        source,
                        intent,
                        stepFacing,
                        destinationCell,
                        rotationKind,
                        updatedTopology,
                        traversalStepResolved,
                        transitionLegality);
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=BlockedDestination|Cell={FormatCell(destinationCell)}|{LegalityDiagnosticsFormatter.FormatStableSummary(transitionLegality, transitionContext.Actor.SpatialState)}");
                    return;
                }
            }

            if (intent.CommandKind == MovementCommandKind.Move &&
                usesPlayerTraversal &&
                EntityRolePolicy.IsPlayerUnit(source) &&
                rotationKind == CubeRotationKind.None &&
                TileFeatureHazardQueries.IsDestroyTileLethalForUnit(source) &&
                TileFeatureAccessQueries.IsActiveDestroyTile(
                    snapshot,
                    tileFeatureDefinitions,
                    destinationCell,
                    movementTopology))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PlayerVoluntaryDestroyTileEntryBlocked|Cell={FormatCell(destinationCell)}");
                return;
            }

            var hasSolidOccupant = snapshot.TryGetSolidSemanticAt(
                movementTopology,
                destinationCell,
                out var solidOccupantSemantic);
            var hasTargetBox = hasSolidOccupant && solidOccupantSemantic.Kind == SolidKind.Box;
            var targetBox = hasTargetBox ? solidOccupantSemantic.Entity : default;
            var solidOccupant = hasSolidOccupant ? solidOccupantSemantic.Entity : default;

            if (hasTargetBox)
            {
                if (!IsSameFaceInteraction(source.position, destinationCell) &&
                    HasBoxCapability(targetBox, BoxCapabilities.Item))
                {
                    rejectedReasons.Add(
                        BuildInteractionCrossesFaceBoundaryRejectedReason(
                            intent.SourceId,
                            intent.IntentId,
                            intent.CommandKind,
                            source.position,
                            destinationCell,
                            targetBox.entityId));
                    return;
                }

                if (intent.CommandKind == MovementCommandKind.Push &&
                    !IsSameFaceInteraction(source.position, destinationCell))
                {
                    rejectedReasons.Add(
                        BuildInteractionCrossesFaceBoundaryRejectedReason(
                            intent.SourceId,
                            intent.IntentId,
                            intent.CommandKind,
                            source.position,
                            destinationCell,
                            targetBox.entityId));
                    return;
                }

                if (intent.CommandKind == MovementCommandKind.Push &&
                    TryGetBlockingBoxInteractionLock(snapshot, targetBox.entityId, tickIndex, blocksPush: true, out var pushLockState))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushTargetLocked|Cell={FormatCell(targetBox.position)}|Target={targetBox.entityId}|Expires={pushLockState.ExpiresTickExclusive}");
                    return;
                }

                if (HasBoxCapability(targetBox, BoxCapabilities.Item))
                {
                    ExpandItem(source, targetBox, intent, destinationCell, stepFacing, rotationKind, updatedTopology, buffer);
                    return;
                }

                if (intent.CommandKind == MovementCommandKind.Push &&
                    snapshot.Topology.IsFaceActive(targetBox.position.face) &&
                    HasBoxCapability(targetBox, BoxCapabilities.Push))
                {
                    TryExpandPush(
                        snapshot,
                        source,
                        targetBox,
                        intent,
                        tickIndex,
                        delta,
                        stepFacing,
                        tileFeatureDefinitions,
                        buffer,
                        rejectedReasons,
                        barricadeBlockFacts,
                        boxSlideStops);
                    return;
                }

                if (intent.CommandKind == MovementCommandKind.Push)
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushTargetNotPushBox|Cell={FormatCell(targetBox.position)}|Target={targetBox.entityId}|Capabilities={targetBox.boxCapabilities}");
                    return;
                }
            }
            else if (intent.CommandKind == MovementCommandKind.Push)
            {
                if (hasSolidOccupant)
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushTargetNotBox|Cell={FormatCell(destinationCell)}|Target={solidOccupant.entityId}|Type={solidOccupant.type}");
                }
                else if (TryGetOccupantForDiagnostics(snapshot, movementTopology, destinationCell, out var target))
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushTargetNotBox|Cell={FormatCell(destinationCell)}|Target={target.entityId}|Type={target.type}");
                }
                else
                {
                    rejectedReasons.Add(
                        $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=PushTargetNotBox|Cell={FormatCell(destinationCell)}|Target=0|Type=None");
                }

                return;
            }

            var movementContext = CreateTraverseContext(
                snapshot,
                source,
                destinationCell,
                movementTopology,
                rotationKind,
                updatedTopology);
            var movementLegality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                movementContext,
                new TileFeatureTraversalEvidence(tileFeatureDefinitions));
            if (movementLegality.Verdict == LegalityVerdict.Blocked)
            {
                AddPlayerTopologyTransitionBlockedSignalIfNeeded(
                    playerTopologyTransitionBlockedSignals,
                    snapshot,
                    source,
                    intent,
                    stepFacing,
                    destinationCell,
                    rotationKind,
                    updatedTopology,
                    traversalStepResolved,
                    movementLegality);
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=BlockedDestination|Cell={FormatCell(movementLegality.Cell)}|{LegalityDiagnosticsFormatter.FormatStableSummary(movementLegality, movementContext.Actor.SpatialState)}");
                return;
            }

            ExpandMove(source, intent, destinationCell, stepFacing, rotationKind, updatedTopology, buffer);
        }

        private static bool TryResolveTraversalStep(
            WorldSnapshot snapshot,
            EntityState source,
            Vector2Int delta,
            bool usesPlayerTraversal,
            out SurfaceCell destinationCell,
            out CubeRotationKind rotationKind,
            out CubeTopologyState updatedTopology,
            out bool traversalStepResolved)
        {
            if (!usesPlayerTraversal && !snapshot.Topology.IsFaceActive(source.position.face))
            {
                destinationCell = default;
                rotationKind = CubeRotationKind.None;
                updatedTopology = snapshot.Topology;
                traversalStepResolved = false;
                return false;
            }

            traversalStepResolved = usesPlayerTraversal
                ? snapshot.TryResolvePlayerStep(
                    source.position,
                    delta,
                    out destinationCell,
                    out rotationKind,
                    out updatedTopology)
                : snapshot.TryResolveUnitStep(
                    source.position,
                    delta,
                    out destinationCell,
                    out rotationKind,
                    out updatedTopology);
            if (!traversalStepResolved)
            {
                destinationCell = source.position + delta;
                rotationKind = CubeRotationKind.None;
                updatedTopology = snapshot.Topology;
            }

            return usesPlayerTraversal || rotationKind == CubeRotationKind.None;
        }

        private static void AddPlayerTopologyTransitionBlockedSignalIfNeeded(
            List<TickPlayerTopologyTransitionBlockedSignal> signals,
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            Direction direction,
            SurfaceCell destinationCell,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology,
            bool traversalStepResolved,
            in LegalityResult movementLegality)
        {
            if (signals == null ||
                intent.CommandKind != MovementCommandKind.Move ||
                !EntityRolePolicy.IsPlayerUnit(source) ||
                !traversalStepResolved ||
                rotationKind == CubeRotationKind.None ||
                movementLegality.Verdict != LegalityVerdict.Blocked ||
                movementLegality.TransitionRequirement.Kind != TransitionRequirementKind.TopologyUpdate ||
                movementLegality.Blockers.Count == 0 ||
                !TryResolvePrimaryNonBoardEdgeBlockerKind(
                    movementLegality.Blockers,
                    out var primaryBlockerKind))
            {
                return;
            }

            signals.Add(
                new TickPlayerTopologyTransitionBlockedSignal(
                    source.entityId,
                    direction,
                    source.position,
                    destinationCell,
                    snapshot.Topology,
                    updatedTopology,
                    rotationKind,
                    primaryBlockerKind));
        }

        private static bool TryResolvePrimaryNonBoardEdgeBlockerKind(
            IReadOnlyList<LegalityBlocker> blockers,
            out TickTraversalBlockerKind primaryBlockerKind)
        {
            for (var i = 0; i < blockers.Count; i++)
            {
                var blockerKind = blockers[i].Kind;
                if (blockerKind == LegalityBlockerKind.BoardEdge)
                {
                    continue;
                }

                primaryBlockerKind = ToTickTraversalBlockerKind(blockerKind);
                return primaryBlockerKind != TickTraversalBlockerKind.None;
            }

            primaryBlockerKind = TickTraversalBlockerKind.None;
            return false;
        }

        internal static TickTraversalBlockerKind ToTickTraversalBlockerKind(LegalityBlockerKind blockerKind)
        {
            return blockerKind switch
            {
                LegalityBlockerKind.BoardEdge => TickTraversalBlockerKind.BoardEdge,
                LegalityBlockerKind.Solid => TickTraversalBlockerKind.Solid,
                LegalityBlockerKind.Unit => TickTraversalBlockerKind.Unit,
                LegalityBlockerKind.Reservation => TickTraversalBlockerKind.Reservation,
                LegalityBlockerKind.TileFeature => TickTraversalBlockerKind.TileFeature,
                _ => TickTraversalBlockerKind.None,
            };
        }

        private static void ExpandFlip(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            int tickIndex,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<BarricadeBlockFact> barricadeBlockFacts)
        {
            var delta = ResolveIntentDelta(source.position, intent.Destination);
            var interactionFacing = ResolveCardinalFacing(
                delta,
                "Flip commands require an orthogonal adjacent direction.");
            if (!snapshot.TryResolveLocalFlipCells(source.position, delta, out var targetCell, out var landingCell))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipCrossesBoundary|Origin={FormatCell(source.position)}|Direction={interactionFacing}");
                return;
            }

            if (!snapshot.TryGetSolidSemanticAt(targetCell, out var targetSemantic) ||
                targetSemantic.Kind != SolidKind.Box ||
                !HasBoxCapability(targetSemantic.Entity, BoxCapabilities.Flip))
            {
                EntityState diagnosticTarget;
                TryGetOccupantForDiagnostics(snapshot, snapshot.Topology, targetCell, out diagnosticTarget);
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipTargetNotFlippableBox|Cell={FormatCell(targetCell)}|Target={diagnosticTarget.entityId}|Type={diagnosticTarget.type}|Capabilities={diagnosticTarget.boxCapabilities}");
                return;
            }

            var target = targetSemantic.Entity;
            if (TryGetBlockingBoxInteractionLock(snapshot, target.entityId, tickIndex, blocksPush: false, out var flipLockState))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipTargetLocked|Cell={FormatCell(target.position)}|Target={target.entityId}|Expires={flipLockState.ExpiresTickExclusive}");
                return;
            }

            if (target.position != landingCell &&
                TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                    snapshot,
                    tileFeatureDefinitions,
                    landingCell,
                    TileFeatureBlockerSubject.Box,
                    TileFeatureMovementKind.FlipLanding,
                    out var barricade) &&
                targetCell != barricade.Cell)
            {
                AddBarricadeBlockFact(
                    barricadeBlockFacts,
                    barricade,
                    target.entityId,
                    ResolvePlanarDirectionOrNone(target.position, landingCell));
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipLandingBlockedByBarricade|Box={target.entityId}|From={FormatCell(target.position)}|Cell={FormatCell(landingCell)}");
                return;
            }

            var landingContext = CreateSettlementContext(snapshot, target, landingCell);
            var landingLegality = RuntimeSettlementLegalityPolicy.EvaluateBoxFlipLandingPlacement(landingContext);
            if (landingLegality.Verdict == LegalityVerdict.Blocked)
            {
                if (TryExpandBoxImpact(
                        snapshot,
                        source,
                        target,
                        intent,
                        landingCell,
                        stopSliding: false,
                        assignKineticOwner: true,
                        skipActiveGlideTargets: true,
                        buffer))
                {
                    return;
                }

                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=FlipLandingBlocked|Cell={FormatCell(landingLegality.Cell)}|{LegalityDiagnosticsFormatter.FormatStableSummary(landingLegality, landingContext.Actor.SpatialState)}");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Flip);
            actionGroup.AssignBoxKineticOwner(target.entityId, source.entityId, source.teamId);
            actionGroup.Moves.Add(
                new MoveAction(
                    target.entityId,
                    target.position,
                    landingCell,
                    ResolveCardinalFacing(-delta, "Flip landing requires an orthogonal adjacent interaction direction.")));
            buffer.Add(actionGroup);
        }

        private static void ExpandMove(
            EntityState source,
            MoveIntent intent,
            SurfaceCell destinationCell,
            Direction facing,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology,
            List<ActionGroup> buffer)
        {
            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Move);
            actionGroup.Moves.Add(
                new MoveAction(
                    intent.SourceId,
                    source.position,
                    destinationCell,
                    facing));

            if (rotationKind != CubeRotationKind.None)
            {
                actionGroup.TopologyChanges.Add(new TopologyChangeAction(rotationKind, updatedTopology));
            }

            buffer.Add(actionGroup);
        }

        private static void ExpandItem(
            EntityState source,
            EntityState target,
            MoveIntent intent,
            SurfaceCell destinationCell,
            Direction facing,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology,
            List<ActionGroup> buffer)
        {
            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Item);
            AddDetachAndMarkForDestroy(actionGroup, target);
            actionGroup.Moves.Add(
                new MoveAction(
                    source.entityId,
                    source.position,
                    destinationCell,
                    facing));

            if (rotationKind != CubeRotationKind.None)
            {
                actionGroup.TopologyChanges.Add(new TopologyChangeAction(rotationKind, updatedTopology));
            }

            buffer.Add(actionGroup);
        }

        private void TryExpandPush(
            WorldSnapshot snapshot,
            EntityState source,
            EntityState target,
            MoveIntent intent,
            int tickIndex,
            Vector2Int delta,
            Direction stepFacing,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<BarricadeBlockFact> barricadeBlockFacts,
            List<BoxSlideStopResult> boxSlideStops)
        {
            var destinationResolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                snapshot.Topology,
                target.position,
                delta,
                out var destination,
                out var stopper);
            if (!destinationResolved)
            {
                if (TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                        snapshot,
                        tileFeatureDefinitions,
                        stopper.Cell,
                        TileFeatureBlockerSubject.Box,
                        TileFeatureMovementKind.PushStart,
                        out var stopperBarricade))
                {
                    AddBarricadeBlockFact(barricadeBlockFacts, stopperBarricade, target.entityId, stepFacing);
                    if (HasBoxCapability(target, BoxCapabilities.Destroy) &&
                        !TryGetDestroyBlockingBoxInteractionLock(snapshot, target.entityId, tickIndex, out _))
                    {
                        var barricadeDestroyGroup = new ActionGroup(
                            intent.IntentId,
                            intent.SourceId,
                            intent.Priority,
                            ActionGroupKind.Push);
                        AddDetachAndMarkForDestroy(barricadeDestroyGroup, target);
                        buffer.Add(barricadeDestroyGroup);
                        return;
                    }

                    rejectedReasons.Add(
                        BuildBarricadeRejectedReason(
                            intent.SourceId,
                            intent.IntentId,
                            TileFeatureMovementKind.PushStart,
                            target.entityId,
                            target.position,
                            stopper.Cell));
                    return;
                }

                if (TryExpandBoxImpact(
                        snapshot,
                        source,
                        target,
                        intent,
                        stopper.Cell,
                        stopSliding: false,
                        assignKineticOwner: true,
                        skipActiveGlideTargets: true,
                        buffer))
                {
                    return;
                }

                if (HasBoxCapability(target, BoxCapabilities.Destroy) &&
                    !TryGetDestroyBlockingBoxInteractionLock(snapshot, target.entityId, tickIndex, out _))
                {
                    var destroyGroup = new ActionGroup(
                        intent.IntentId,
                        intent.SourceId,
                        intent.Priority,
                        ActionGroupKind.Push);
                    AddDetachAndMarkForDestroy(destroyGroup, target);
                    buffer.Add(destroyGroup);
                    return;
                }

                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=SlideStopperAdjacent|Target={target.entityId}|{FormatStopper(stopper)}");
                return;
            }

            if (TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                    snapshot,
                    tileFeatureDefinitions,
                    destination,
                    TileFeatureBlockerSubject.Box,
                    TileFeatureMovementKind.PushStart,
                    out var barricade))
            {
                AddBarricadeBlockFact(barricadeBlockFacts, barricade, target.entityId, stepFacing);
                if (HasBoxCapability(target, BoxCapabilities.Destroy) &&
                    !TryGetDestroyBlockingBoxInteractionLock(snapshot, target.entityId, tickIndex, out _))
                {
                    var destroyGroup = new ActionGroup(
                        intent.IntentId,
                        intent.SourceId,
                        intent.Priority,
                        ActionGroupKind.Push);
                    AddDetachAndMarkForDestroy(destroyGroup, target);
                    buffer.Add(destroyGroup);
                    return;
                }

                rejectedReasons.Add(
                    BuildBarricadeRejectedReason(
                        intent.SourceId,
                        intent.IntentId,
                        TileFeatureMovementKind.PushStart,
                        target.entityId,
                        target.position,
                        destination));
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Push);
            actionGroup.AssignBoxKineticOwner(target.entityId, source.entityId, source.teamId);
            actionGroup.StateChanges.Add(
                new StateChangeAction(
                    target.entityId,
                    EntityPhaseState.Sliding,
                    _slidingStateTimerTicks));
            actionGroup.Moves.Add(
                new MoveAction(
                    target.entityId,
                    target.position,
                    destination,
                    stepFacing));
            buffer.Add(actionGroup);
        }

        private void ExpandSlidingPushBoxMove(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            int tickIndex,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<BarricadeBlockFact> barricadeBlockFacts,
            List<BoxSlideStopResult> boxSlideStops)
        {
            var delta = ResolveIntentDelta(source.position, intent.Destination);
            var stepFacing = ResolveCardinalFacing(
                delta,
                "Sliding push boxes require an orthogonal single-step direction.");

            if (!snapshot.TryResolveNextSurfaceBoxSlideStep(
                    snapshot.Topology,
                    source.position,
                    delta,
                    out var destination,
                    out var stopper))
            {
                if (TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                        snapshot,
                        tileFeatureDefinitions,
                        stopper.Cell,
                        TileFeatureBlockerSubject.Box,
                        TileFeatureMovementKind.SlidingContinuation,
                        out var stopperBarricade))
                {
                    AddBarricadeBlockFact(barricadeBlockFacts, stopperBarricade, source.entityId, stepFacing);
                    AddBarricadeBoxSlideStop(
                        boxSlideStops,
                        intent.IntentId,
                        source,
                        stopperBarricade,
                        stepFacing,
                        snapshot.Topology);
                    rejectedReasons.Add(
                        BuildBarricadeRejectedReason(
                            intent.SourceId,
                            intent.IntentId,
                            TileFeatureMovementKind.SlidingContinuation,
                            source.entityId,
                            source.position,
                            stopper.Cell));

                    var barricadeStopGroup = new ActionGroup(
                        intent.IntentId,
                        intent.SourceId,
                        intent.Priority,
                        ActionGroupKind.Stop);
                    barricadeStopGroup.StateChanges.Add(
                        new StateChangeAction(
                            source.entityId,
                            EntityPhaseState.Idle,
                            stateTimer: 0));
                    buffer.Add(barricadeStopGroup);
                    return;
                }

                if (TryExpandBoxImpact(
                        snapshot,
                        source,
                        source,
                        intent,
                        stopper.Cell,
                        stopSliding: true,
                        assignKineticOwner: false,
                        skipActiveGlideTargets: true,
                        buffer))
                {
                    return;
                }

                if (TryExpandDeferredSlidingImpact(
                        snapshot,
                        source,
                        intent,
                        stopper.Cell,
                        tickIndex,
                        tileFeatureDefinitions,
                        buffer))
                {
                    return;
                }

                var stopGroup = new ActionGroup(
                    intent.IntentId,
                    intent.SourceId,
                    intent.Priority,
                    ActionGroupKind.Stop);
                stopGroup.StateChanges.Add(
                    new StateChangeAction(
                        source.entityId,
                        EntityPhaseState.Idle,
                        stateTimer: 0));
                buffer.Add(stopGroup);
                AddSolidEntityBoxSlideStop(
                    boxSlideStops,
                    snapshot,
                    intent.IntentId,
                    source,
                    stopper,
                    stepFacing);
                return;
            }

            if (TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                    snapshot,
                    tileFeatureDefinitions,
                    destination,
                    TileFeatureBlockerSubject.Box,
                    TileFeatureMovementKind.SlidingContinuation,
                    out var barricade))
            {
                AddBarricadeBlockFact(barricadeBlockFacts, barricade, source.entityId, stepFacing);
                AddBarricadeBoxSlideStop(
                    boxSlideStops,
                    intent.IntentId,
                    source,
                    barricade,
                    stepFacing,
                    snapshot.Topology);
                rejectedReasons.Add(
                    BuildBarricadeRejectedReason(
                        intent.SourceId,
                        intent.IntentId,
                        TileFeatureMovementKind.SlidingContinuation,
                        source.entityId,
                        source.position,
                        destination));

                var stopGroup = new ActionGroup(
                    intent.IntentId,
                    intent.SourceId,
                    intent.Priority,
                    ActionGroupKind.Stop);
                stopGroup.StateChanges.Add(
                    new StateChangeAction(
                        source.entityId,
                        EntityPhaseState.Idle,
                        stateTimer: 0));
                buffer.Add(stopGroup);
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Push);
            actionGroup.StateChanges.Add(
                new StateChangeAction(
                    source.entityId,
                    EntityPhaseState.Sliding,
                    _slidingStateTimerTicks));
            actionGroup.Moves.Add(
                new MoveAction(
                    source.entityId,
                    source.position,
                    destination,
                    stepFacing));
            buffer.Add(actionGroup);
        }

        private static bool TryExpandDeferredSlidingImpact(
            WorldSnapshot snapshot,
            EntityState source,
            MoveIntent intent,
            SurfaceCell impactCell,
            int tickIndex,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            List<ActionGroup> buffer)
        {
            if (tickIndex <= 0 ||
                !TryResolveBoxImpactTeamId(source, source, out var sourceTeamId) ||
                !HasSameTickHostileJumpLandingAt(
                    snapshot,
                    impactCell,
                    sourceTeamId,
                    tickIndex,
                    tileFeatureDefinitions))
            {
                return false;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Stop);
            actionGroup.StateChanges.Add(
                new StateChangeAction(
                    source.entityId,
                    EntityPhaseState.Idle,
                    stateTimer: 0));
            actionGroup.AssignDeferredImpact(source.entityId, impactCell);
            buffer.Add(actionGroup);
            return true;
        }

        private static bool HasSameTickHostileJumpLandingAt(
            WorldSnapshot snapshot,
            SurfaceCell impactCell,
            int sourceTeamId,
            int tickIndex,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            var jumpEntries = new List<EnemyJumpSnapshotEntry>();
            snapshot.EnumerateEnemyJumpStatesOrdered(jumpEntries);

            for (var i = 0; i < jumpEntries.Count; i++)
            {
                var jumpEntry = jumpEntries[i];
                var jumpState = jumpEntry.State;
                if (jumpState.phase != EnemyJumpPhase.Airborne ||
                    tickIndex < jumpState.landingTick ||
                    !snapshot.TryGetEntity(jumpEntry.EntityId, out var jumper) ||
                    jumper.position.face != snapshot.Topology.BottomFace ||
                    jumper.teamId <= 0 ||
                    jumper.teamId == sourceTeamId ||
                    jumper.hp <= 0 ||
                    jumper.markedForDeath ||
                    !EnemyJumpQueries.TryResolveLandingCell(
                        snapshot,
                        jumper,
                        jumpState,
                        out var landingCell,
                        out _,
                        tileFeatureDefinitions))
                {
                    continue;
                }

                if (landingCell == impactCell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryExpandBoxImpact(
            WorldSnapshot snapshot,
            EntityState actorSource,
            EntityState impactSourceBox,
            MoveIntent intent,
            SurfaceCell impactCell,
            bool stopSliding,
            bool assignKineticOwner,
            bool skipActiveGlideTargets,
            List<ActionGroup> buffer)
        {
            if (!TryResolveBoxImpactTeamId(actorSource, impactSourceBox, out var sourceTeamId))
            {
                return false;
            }

            var targets = new List<EntityState>();
            snapshot.EnumerateUnitImpactTargetsAt(impactCell, targets);

            var targetIds = new List<int>(targets.Count);
            var hasHostileTarget = false;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (skipActiveGlideTargets &&
                    snapshot.TryGetActiveEnemyGlideState(target.entityId, out _))
                {
                    continue;
                }

                targetIds.Add(target.entityId);
                if (target.teamId > 0 &&
                    target.teamId != sourceTeamId)
                {
                    hasHostileTarget = true;
                }
            }

            if (!hasHostileTarget)
            {
                return false;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.BoxImpact);
            actionGroup.AssignImpactReservation(impactSourceBox.entityId, targetIds);

            if (stopSliding)
            {
                actionGroup.StateChanges.Add(
                    new StateChangeAction(
                        impactSourceBox.entityId,
                        EntityPhaseState.Idle,
                        stateTimer: 0));
            }

            if (assignKineticOwner &&
                actorSource.type == EntityType.Unit &&
                actorSource.teamId > 0)
            {
                actionGroup.AssignBoxKineticOwner(impactSourceBox.entityId, actorSource.entityId, actorSource.teamId);
            }

            buffer.Add(actionGroup);
            return true;
        }

        private static void AddDetachAndMarkForDestroy(ActionGroup actionGroup, EntityState target)
        {
            actionGroup.BoardPresenceChanges.Add(
                new BoardPresenceChangeAction(target.entityId, EntityBoardPresence.Detached));
            actionGroup.Destroys.Add(new DestroyAction(target.entityId, DestroyCondition.AlwaysMark));
        }

        private static bool HasBoxCapability(EntityState entity, BoxCapabilities capability)
        {
            return entity.type == EntityType.Box && (entity.boxCapabilities & capability) == capability;
        }

        private static bool IsSameFaceInteraction(SurfaceCell sourceCell, SurfaceCell targetCell)
        {
            return sourceCell.face == targetCell.face;
        }

        private static string BuildInteractionCrossesFaceBoundaryRejectedReason(
            int sourceId,
            int intentId,
            MovementCommandKind commandKind,
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            int targetEntityId)
        {
            return $"MovementRejected|Stage=Expand|Source={sourceId}|I={intentId}|Reason=InteractionCrossesFaceBoundary|Command={commandKind}|From={FormatCell(sourceCell)}|Cell={FormatCell(targetCell)}|Target={targetEntityId}";
        }

        private static bool TryGetBlockingBoxInteractionLock(
            WorldSnapshot snapshot,
            int boxEntityId,
            int tickIndex,
            bool blocksPush,
            out BoxInteractionLockState lockState)
        {
            if (!snapshot.TryGetActiveBoxInteractionLockState(boxEntityId, tickIndex, out lockState))
            {
                return false;
            }

            return blocksPush
                ? lockState.BlocksPush
                : lockState.BlocksFlip;
        }

        private static bool TryGetDestroyBlockingBoxInteractionLock(
            WorldSnapshot snapshot,
            int boxEntityId,
            int tickIndex,
            out BoxInteractionLockState lockState)
        {
            if (!snapshot.TryGetActiveBoxInteractionLockState(boxEntityId, tickIndex, out lockState))
            {
                return false;
            }

            return lockState.BlocksDestroy;
        }

        private static bool IsSlidingPushBox(EntityState entity)
        {
            return entity.type == EntityType.Box &&
                   entity.state == EntityPhaseState.Sliding &&
                   HasBoxCapability(entity, BoxCapabilities.Push);
        }

        private static bool TryResolveBoxImpactTeamId(
            EntityState actorSource,
            EntityState impactSourceBox,
            out int sourceTeamId)
        {
            if (impactSourceBox.kineticInstigatorTeamId > 0)
            {
                sourceTeamId = impactSourceBox.kineticInstigatorTeamId;
                return true;
            }

            if (actorSource.type == EntityType.Unit &&
                actorSource.teamId > 0)
            {
                sourceTeamId = actorSource.teamId;
                return true;
            }

            sourceTeamId = 0;
            return false;
        }

        private static string BuildBarricadeRejectedReason(
            int sourceId,
            int intentId,
            TileFeatureMovementKind movementKind,
            int boxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
        {
            return
                $"MovementRejected|Stage=Expand|Source={sourceId}|I={intentId}|Reason=BoxSlideBlockedByBarricade|MovementKind={movementKind}|Box={boxEntityId}|From={FormatCell(sourceCell)}|Cell={FormatCell(destinationCell)}";
        }

        private static void AddBarricadeBlockFact(
            List<BarricadeBlockFact> facts,
            TileFeatureState barricade,
            int boxEntityId,
            Direction attemptedDirection)
        {
            facts?.Add(
                new BarricadeBlockFact(
                    barricade.TileId,
                    barricade.Cell,
                    boxEntityId,
                    attemptedDirection));
        }

        private static void AddBarricadeBoxSlideStop(
            List<BoxSlideStopResult> stops,
            int intentId,
            EntityState box,
            TileFeatureState barricade,
            Direction slideDirection,
            CubeTopologyState topology)
        {
            if (stops == null || barricade.TileId <= 0)
            {
                return;
            }

            stops.Add(
                new BoxSlideStopResult(
                    intentId,
                    box.entityId,
                    box.position,
                    barricade.Cell,
                    slideDirection,
                    BoxSlideStopperKind.Barricade,
                    stopperEntityId: 0,
                    solidKind: SolidKind.Box,
                    topology: topology,
                    cause: BoxSlideStopCause.SlidingContinuationBlocked,
                    stopperTileId: barricade.TileId));
        }

        private static void AddSolidEntityBoxSlideStop(
            List<BoxSlideStopResult> stops,
            WorldSnapshot snapshot,
            int intentId,
            EntityState box,
            SlideStopper stopper,
            Direction slideDirection)
        {
            if (stops == null ||
                stopper.Kind != SlideStopperKind.Entity ||
                stopper.EntityId <= 0 ||
                !snapshot.TryGetSolidSemanticAt(stopper.Cell, out var semantic) ||
                semantic.Entity.entityId != stopper.EntityId)
            {
                return;
            }

            stops.Add(
                new BoxSlideStopResult(
                    intentId,
                    box.entityId,
                    box.position,
                    stopper.Cell,
                    slideDirection,
                    BoxSlideStopperKind.SolidEntity,
                    stopper.EntityId,
                    semantic.Kind,
                    snapshot.Topology,
                    BoxSlideStopCause.SlidingContinuationBlocked));
        }

        private static string FormatStopper(SlideStopper stopper)
        {
            switch (stopper.Kind)
            {
                case SlideStopperKind.BoardEdge:
                    return $"StopperKind=BoardEdge|Cell={FormatCell(stopper.Cell)}";

                case SlideStopperKind.Entity:
                    return $"StopperKind=Entity|Stopper={stopper.EntityId}|StopperType={stopper.EntityType}|Cell={FormatCell(stopper.Cell)}";

                default:
                    return $"StopperKind=None|Cell={FormatCell(stopper.Cell)}";
            }
        }

        private static TraverseContext CreateTraverseContext(
            WorldSnapshot snapshot,
            in EntityState actor,
            SurfaceCell candidateCell,
            CubeTopologyState evaluationTopology,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology)
        {
            return new TraverseContext(
                snapshot,
                BuildActorRef(snapshot, actor),
                actor.position,
                candidateCell,
                evaluationTopology,
                rotationKind == CubeRotationKind.None
                    ? TransitionRequirement.None
                    : TransitionRequirement.TopologyUpdate(rotationKind, updatedTopology));
        }

        private static SettlementContext CreateSettlementContext(
            WorldSnapshot snapshot,
            in EntityState actor,
            SurfaceCell terminalCell,
            ReservationStatus reservationStatus = ReservationStatus.None)
        {
            return new SettlementContext(
                snapshot,
                BuildActorRef(snapshot, actor),
                terminalCell,
                snapshot.Topology,
                SpatialState.Anchored,
                reservationStatus);
        }

        private static LegalityActorRef BuildActorRef(WorldSnapshot snapshot, in EntityState actor)
        {
            return StateQuery.BuildActorRef(snapshot, actor);
        }

        private static bool IsGameplayImpactBlocker(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetEntity(entityId, out var entity) &&
                   snapshot.TryGetResolvedSpatialState(entityId, out var spatialState) &&
                   GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(spatialState);
        }

        private static bool TryGetOccupantForDiagnostics(
            WorldSnapshot snapshot,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState entity)
        {
            if (snapshot.TryGetSolidSemanticAt(topology, cell, out var solidSemantic))
            {
                entity = solidSemantic.Entity;
                return true;
            }

            var occupants = new List<EntityState>();
            snapshot.EnumerateUnitsAt(topology, cell, occupants);
            if (occupants.Count > 0)
            {
                entity = occupants[0];
                return true;
            }

            entity = default;
            return false;
        }

        private static Vector2Int ResolveIntentDelta(SurfaceCell source, Vector2Int destination)
        {
            return destination - source.PlanarPosition;
        }

        private static Direction ResolveCardinalFacing(Vector2Int delta, string errorMessage)
        {
            if (delta == Vector2Int.up)
            {
                return Direction.Up;
            }

            if (delta == Vector2Int.right)
            {
                return Direction.Right;
            }

            if (delta == Vector2Int.down)
            {
                return Direction.Down;
            }

            if (delta == Vector2Int.left)
            {
                return Direction.Left;
            }

            throw new InvalidOperationException(errorMessage);
        }

        private static Direction ResolvePlanarDirectionOrNone(SurfaceCell source, SurfaceCell destination)
        {
            if (source.face != destination.face)
            {
                return Direction.None;
            }

            var delta = destination.PlanarPosition - source.PlanarPosition;
            if (delta.x == 0 && delta.y > 0)
            {
                return Direction.Up;
            }

            if (delta.x > 0 && delta.y == 0)
            {
                return Direction.Right;
            }

            if (delta.x == 0 && delta.y < 0)
            {
                return Direction.Down;
            }

            if (delta.x < 0 && delta.y == 0)
            {
                return Direction.Left;
            }

            return Direction.None;
        }

        private static void ValidateSingleStepMove(Vector2Int source, Vector2Int destination, int sourceId)
        {
            var delta = destination - source;
            if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1)
            {
                throw new InvalidOperationException(
                    $"Entity {sourceId} emitted an invalid Stage2 MoveIntent. Only orthogonal single-cell moves are allowed.");
            }
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
        }
    }
}
