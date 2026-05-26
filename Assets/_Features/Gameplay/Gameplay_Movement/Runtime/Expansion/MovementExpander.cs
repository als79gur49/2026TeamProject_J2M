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
        private const int BoxImpactDamageAmount = 1;
        private readonly int _projectileStateTimerTicks;
        private readonly int _slidingStateTimerTicks;
        private readonly int _flipContactDelayTicks;

        public MovementExpander()
            : this(GameplayTimingProfile.CreateDefault())
        {
        }

        public MovementExpander(GameplayTimingProfile timingProfile)
        {
            var resolvedTimingProfile = timingProfile ?? throw new ArgumentNullException(nameof(timingProfile));
            _projectileStateTimerTicks = resolvedTimingProfile.ProjectileStepIntervalTicks;
            _slidingStateTimerTicks = resolvedTimingProfile.BoxSlideStepIntervalTicks;
            _flipContactDelayTicks = resolvedTimingProfile.FlipContactDelayTicks;
        }

        public void Expand(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            Expand(snapshot, tickIndex: 0, sortedIntents, null, null, buffer, rejectedReasons);
        }

        public void Expand(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<MoveIntent> sortedIntents,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            Expand(snapshot, tickIndex, sortedIntents, null, null, buffer, rejectedReasons);
        }

        public void Expand(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            ISet<int> playerTraversalSourceIds,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            Expand(snapshot, tickIndex: 0, sortedIntents, playerTraversalSourceIds, null, buffer, rejectedReasons);
        }

        public void Expand(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<MoveIntent> sortedIntents,
            ISet<int> playerTraversalSourceIds,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            Expand(snapshot, tickIndex, sortedIntents, playerTraversalSourceIds, null, buffer, rejectedReasons);
        }

        public void Expand(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<MoveIntent> sortedIntents,
            ISet<int> playerTraversalSourceIds,
            IReadOnlyList<FrontFaceSupportContributor> frontFaceSupportContributors,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<FrontFaceShieldBlockPresentationExport> frontFaceShieldBlockExports = null,
            List<BarricadeBlockFact> barricadeBlockFacts = null,
            ISet<int> forbiddenLegacyUnitOrdinaryIntentIds = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            List<BoxSlideStopResult> boxSlideStops = null)
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

                if (forbiddenLegacyUnitOrdinaryIntentIds != null &&
                    forbiddenLegacyUnitOrdinaryIntentIds.Contains(intent.IntentId) &&
                    entity.type == EntityType.Unit &&
                    intent.CommandKind == MovementCommandKind.Move)
                {
                    rejectedReasons.Add(
                        $"LegacyUnitOrdinaryMovementDetected|Stage=Expand|E={intent.SourceId}|EntityType={entity.type}|Intent={intent.CommandKind}|Reason=ForbiddenCoveredLocomotionReachedMovementExpander|I={intent.IntentId}");
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

                if (entity.type == EntityType.Projectile)
                {
                    ExpandProjectileMove(snapshot, entity, intent, buffer, rejectedReasons);
                    continue;
                }

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
                            frontFaceSupportContributors,
                            tileFeatureDefinitions,
                            buffer,
                            rejectedReasons,
                            frontFaceShieldBlockExports,
                            barricadeBlockFacts,
                            boxSlideStops);
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
            IReadOnlyList<FrontFaceSupportContributor> frontFaceSupportContributors,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<FrontFaceShieldBlockPresentationExport> frontFaceShieldBlockExports,
            List<BarricadeBlockFact> barricadeBlockFacts,
            List<BoxSlideStopResult> boxSlideStops)
        {
            if (IsSlidingPushBox(source))
            {
                ExpandSlidingPushBoxMove(
                    snapshot,
                    source,
                    intent,
                    tickIndex,
                    frontFaceSupportContributors,
                    tileFeatureDefinitions,
                    buffer,
                    rejectedReasons,
                    frontFaceShieldBlockExports,
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
                    out var updatedTopology))
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
                        frontFaceSupportContributors,
                        tileFeatureDefinitions,
                        buffer,
                        rejectedReasons,
                        frontFaceShieldBlockExports,
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
                else if (TryGetNonProjectileOccupantForDiagnostics(snapshot, movementTopology, destinationCell, out var target))
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
                updatedTopology,
                tileFeatureDefinitions);
            var movementLegality = RuntimeTraversalLegalityPolicy.EvaluateDestination(movementContext);
            if (movementLegality.Verdict == LegalityVerdict.Blocked)
            {
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
            out CubeTopologyState updatedTopology)
        {
            if (!usesPlayerTraversal && !snapshot.Topology.IsFaceActive(source.position.face))
            {
                destinationCell = default;
                rotationKind = CubeRotationKind.None;
                updatedTopology = snapshot.Topology;
                return false;
            }

            var hasResolvedStep = usesPlayerTraversal
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
            if (!hasResolvedStep)
            {
                destinationCell = source.position + delta;
                rotationKind = CubeRotationKind.None;
                updatedTopology = snapshot.Topology;
            }

            return usesPlayerTraversal || rotationKind == CubeRotationKind.None;
        }

        private void ExpandFlip(
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
                TryGetNonProjectileOccupantForDiagnostics(snapshot, snapshot.Topology, targetCell, out diagnosticTarget);
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
            var landingLegality = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(landingContext);
            if (landingLegality.Verdict == LegalityVerdict.Blocked)
            {
                if (TryExpandB1HostileFlipImpact(
                        snapshot,
                        source,
                        target,
                        intent,
                        tickIndex,
                        interactionFacing,
                        landingCell,
                        _flipContactDelayTicks,
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

        private static bool TryExpandB1HostileFlipImpact(
            WorldSnapshot snapshot,
            EntityState actorSource,
            EntityState sourceBox,
            MoveIntent intent,
            int tickIndex,
            Direction flipDirection,
            SurfaceCell contactCell,
            int flipContactDelayTicks,
            List<ActionGroup> buffer)
        {
            if (!TryResolveBoxImpactTeamId(actorSource, sourceBox, out var sourceTeamId))
            {
                return false;
            }

            var targets = new List<EntityState>();
            snapshot.EnumerateUnitImpactTargetsAt(contactCell, targets);

            var hasHostileTarget = false;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.teamId > 0 &&
                    target.teamId != sourceTeamId)
                {
                    hasHostileTarget = true;
                    break;
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
                ActionGroupKind.Flip);
            actionGroup.BoardPresenceChanges.Add(
                new BoardPresenceChangeAction(sourceBox.entityId, EntityBoardPresence.InFlight));
            actionGroup.AssignScheduledFlipContact(
                new ScheduledFlipContactDraft(
                    actorSource.entityId,
                    sourceBox.entityId,
                    sourceBox.position,
                    contactCell,
                    contactCell,
                    flipDirection,
                    sourceBox.position.face,
                    sourceBox.boxCapabilities,
                    BoxImpactDamageAmount,
                    actorSource.type == EntityType.Unit && actorSource.teamId > 0 ? actorSource.entityId : 0,
                    sourceTeamId,
                    tickIndex,
                    tickIndex + flipContactDelayTicks,
                    intent.LocalSequence,
                    FlipContactCancellationPolicy.SafeReturnOrDestroy,
                    FlipContactDispositionPolicy.DefaultB1HostileImpact));
            buffer.Add(actionGroup);
            return true;
        }

        private void ExpandProjectileMove(
            WorldSnapshot snapshot,
            EntityState entity,
            MoveIntent intent,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (intent.CommandKind != MovementCommandKind.Move)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=UnsupportedCommand|Command={intent.CommandKind}");
                return;
            }

            var delta = ResolveIntentDelta(entity.position, intent.Destination);
            var destinationCell = entity.position + delta;

            if (TryExpandProjectileImpact(snapshot, intent, entity.teamId, destinationCell, buffer, rejectedReasons))
            {
                return;
            }

            if (snapshot.TryGetProjectileAt(destinationCell, out _))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ProjectileDestinationBlocked|Cell={FormatCell(destinationCell)}");
                return;
            }

            var projectileContext = CreateTraverseContext(
                snapshot,
                entity,
                destinationCell,
                snapshot.Topology,
                CubeRotationKind.None,
                snapshot.Topology);
            var projectileLegality = RuntimeTraversalLegalityPolicy.EvaluateDestination(projectileContext);
            if (projectileLegality.Verdict == LegalityVerdict.Blocked)
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=BlockedDestination|Cell={FormatCell(projectileLegality.Cell)}|{LegalityDiagnosticsFormatter.FormatStableSummary(projectileLegality, projectileContext.Actor.SpatialState)}");
                return;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.Move);
            actionGroup.StateChanges.Add(
                new StateChangeAction(
                    entity.entityId,
                    entity.state,
                    _projectileStateTimerTicks));
            actionGroup.Moves.Add(
                new MoveAction(
                    intent.SourceId,
                    entity.position,
                    destinationCell,
                    ResolveCardinalFacing(delta, "Projectile movement requires an orthogonal single-cell direction.")));
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
            IReadOnlyList<FrontFaceSupportContributor> frontFaceSupportContributors,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<FrontFaceShieldBlockPresentationExport> frontFaceShieldBlockExports,
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
                        BoxSlideMovementKind.PushStart,
                        target.entityId,
                        target.position,
                        destination));
                return;
            }

            if (BoxSlideBlockerQuery.TryResolveBlocker(
                    snapshot,
                    frontFaceSupportContributors,
                    tickIndex,
                    target,
                    target.position,
                    destination,
                    BoxSlideMovementKind.PushStart,
                    out var blocker))
            {
                rejectedReasons.Add(
                    BuildFrontFaceShieldRejectedReason(
                        intent.SourceId,
                        intent.IntentId,
                        BoxSlideMovementKind.PushStart,
                        target.entityId,
                        target.position,
                        destination,
                        blocker));
                AddFrontFaceShieldBlockPresentationExport(
                    frontFaceShieldBlockExports,
                    snapshot.Topology,
                    tickIndex,
                    FrontFaceShieldBlockMovementKind.PushStart,
                    actorEntityId: intent.SourceId,
                    boxEntityId: target.entityId,
                    blockedCell: destination,
                    blocker);
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
            IReadOnlyList<FrontFaceSupportContributor> frontFaceSupportContributors,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            List<ActionGroup> buffer,
            List<string> rejectedReasons,
            List<FrontFaceShieldBlockPresentationExport> frontFaceShieldBlockExports,
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
                        BoxSlideMovementKind.SlidingContinuation,
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

            if (BoxSlideBlockerQuery.TryResolveBlocker(
                    snapshot,
                    frontFaceSupportContributors,
                    tickIndex,
                    source,
                    source.position,
                    destination,
                    BoxSlideMovementKind.SlidingContinuation,
                    out var blocker))
            {
                rejectedReasons.Add(
                    BuildFrontFaceShieldRejectedReason(
                        intent.SourceId,
                        intent.IntentId,
                        BoxSlideMovementKind.SlidingContinuation,
                        source.entityId,
                        source.position,
                        destination,
                        blocker));
                AddFrontFaceShieldBlockPresentationExport(
                    frontFaceShieldBlockExports,
                    snapshot.Topology,
                    tickIndex,
                    FrontFaceShieldBlockMovementKind.SlidingContinuation,
                    actorEntityId: source.kineticInstigatorEntityId > 0 ? source.kineticInstigatorEntityId : 0,
                    boxEntityId: source.entityId,
                    blockedCell: destination,
                    blocker);

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
            List<ActionGroup> buffer)
        {
            if (tickIndex <= 0 ||
                !TryResolveBoxImpactTeamId(source, source, out var sourceTeamId) ||
                !HasSameTickHostileJumpLandingAt(snapshot, impactCell, sourceTeamId, tickIndex))
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
            int tickIndex)
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
                    jumper.teamId <= 0 ||
                    jumper.teamId == sourceTeamId ||
                    jumper.hp <= 0 ||
                    jumper.markedForDeath ||
                    !EnemyJumpQueries.TryResolveLandingCell(snapshot, jumper, jumpState, out var landingCell, out _))
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

        private static string BuildFrontFaceShieldRejectedReason(
            int sourceId,
            int intentId,
            BoxSlideMovementKind movementKind,
            int boxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            in BoxSlideBlockerResult blocker)
        {
            return
                $"MovementRejected|Stage=Expand|Source={sourceId}|I={intentId}|Reason=BoxSlideBlockedByFrontFaceShield|MovementKind={movementKind}|Box={boxEntityId}|From={FormatCell(sourceCell)}|Cell={FormatCell(destinationCell)}|ShieldSource={blocker.BlockerEntityId}|ShieldCell={FormatCell(blocker.BlockerSourceCell)}";
        }

        private static string BuildBarricadeRejectedReason(
            int sourceId,
            int intentId,
            BoxSlideMovementKind movementKind,
            int boxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
        {
            return
                $"MovementRejected|Stage=Expand|Source={sourceId}|I={intentId}|Reason=BoxSlideBlockedByBarricade|MovementKind={movementKind}|Box={boxEntityId}|From={FormatCell(sourceCell)}|Cell={FormatCell(destinationCell)}";
        }

        private static void AddFrontFaceShieldBlockPresentationExport(
            List<FrontFaceShieldBlockPresentationExport> exports,
            CubeTopologyState topology,
            int tickIndex,
            FrontFaceShieldBlockMovementKind movementKind,
            int actorEntityId,
            int boxEntityId,
            SurfaceCell blockedCell,
            in BoxSlideBlockerResult blocker)
        {
            if (exports == null)
            {
                return;
            }

            exports.Add(
                new FrontFaceShieldBlockPresentationExport(
                    blocker.BlockerEntityId,
                    boxEntityId,
                    actorEntityId,
                    blockedCell,
                    blocker.BlockerSourceCell,
                    movementKind,
                    topology,
                    tickIndex,
                    BuildFrontFaceShieldPresentationSeed(
                        tickIndex,
                        blocker.BlockerEntityId,
                        boxEntityId,
                        blockedCell,
                        (int)movementKind)));
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

        internal static int BuildFrontFaceShieldPresentationSeed(
            int tickIndex,
            int sourceEntityId,
            int targetEntityId,
            SurfaceCell cell,
            int discriminator)
        {
            unchecked
            {
                var seed = 17;
                seed = (seed * 31) + tickIndex;
                seed = (seed * 31) + sourceEntityId;
                seed = (seed * 31) + targetEntityId;
                seed = (seed * 31) + (int)cell.face;
                seed = (seed * 31) + cell.x;
                seed = (seed * 31) + cell.y;
                seed = (seed * 31) + discriminator;
                return seed;
            }
        }

        private static string FormatStopper(SlideStopper stopper)
        {
            switch (stopper.Kind)
            {
                case SlideStopperKind.BoardEdge:
                    return $"StopperKind=BoardEdge|Cell={FormatCell(stopper.Cell)}";

                case SlideStopperKind.Terrain:
                    return $"StopperKind=Terrain|Cell={FormatCell(stopper.Cell)}";

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
            CubeTopologyState updatedTopology,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            return new TraverseContext(
                snapshot,
                BuildActorRef(snapshot, actor),
                actor.position,
                candidateCell,
                evaluationTopology,
                rotationKind == CubeRotationKind.None
                    ? TransitionRequirement.None
                    : TransitionRequirement.TopologyUpdate(rotationKind, updatedTopology),
                tileFeatureDefinitions: tileFeatureDefinitions);
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

        private static bool TryExpandProjectileImpact(
            WorldSnapshot snapshot,
            MoveIntent intent,
            int sourceTeamId,
            SurfaceCell destinationCell,
            List<ActionGroup> buffer,
            List<string> rejectedReasons)
        {
            if (!snapshot.TryPickImpactTargetAt(destinationCell, sourceTeamId, out var target))
            {
                return false;
            }

            if (!IsGameplayImpactBlocker(snapshot, target.entityId))
            {
                rejectedReasons.Add(
                    $"MovementRejected|Stage=Expand|Source={intent.SourceId}|I={intent.IntentId}|Reason=ImpactTargetNotBlocking|Target={target.entityId}|Cell={FormatCell(destinationCell)}");
                return true;
            }

            var actionGroup = new ActionGroup(
                intent.IntentId,
                intent.SourceId,
                intent.Priority,
                ActionGroupKind.ProjectileImpact);
            actionGroup.AssignProjectileImpactTarget(target.entityId);
            buffer.Add(actionGroup);
            return true;
        }

        private static bool TryGetNonProjectileOccupantForDiagnostics(
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

        private static bool IsGameplayImpactBlocker(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetEntity(entityId, out var entity) &&
                   entity.type != EntityType.Projectile &&
                   snapshot.TryGetResolvedSpatialState(entityId, out var spatialState) &&
                   GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(spatialState);
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
