using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class FlipScheduledContactResolver
    {
        private readonly List<ScheduledFlipContact> _dueContacts = new();
        private readonly List<EntityState> _unitBuffer = new();

        public FlipScheduledContactDueResult ResolveDueScheduledFlipContacts(
            WorldSnapshot snapshot,
            int currentTick,
            bool stageAlreadyTerminal)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            snapshot.EnumerateDueScheduledFlipContactsOrdered(currentTick, _dueContacts);
            if (_dueContacts.Count == 0)
            {
                return FlipScheduledContactDueResult.Empty;
            }

            var batch = new FinalizationBatch();
            var postCleanupBatch = new FinalizationBatch();
            var eventLogEntries = new List<string>();
            var damageResolutions = new List<DamageResolutionRecord>();
            var contactResolutions = new List<FlipContactResolution>();
            var contactPresentationSignals = new List<FlipDueContactPresentationSignal>();
            for (var i = 0; i < _dueContacts.Count; i++)
            {
                ResolveSingleScheduledFlipContact(
                    snapshot,
                    _dueContacts[i],
                    currentTick,
                    stageAlreadyTerminal,
                    batch,
                    postCleanupBatch,
                    eventLogEntries,
                    damageResolutions,
                    contactResolutions,
                    contactPresentationSignals);
            }

            return new FlipScheduledContactDueResult(
                batch,
                postCleanupBatch,
                eventLogEntries,
                damageResolutions,
                contactResolutions,
                contactPresentationSignals);
        }

        private void ResolveSingleScheduledFlipContact(
            WorldSnapshot snapshot,
            in ScheduledFlipContact contact,
            int currentTick,
            bool stageAlreadyTerminal,
            FinalizationBatch batch,
            FinalizationBatch postCleanupBatch,
            List<string> eventLogEntries,
            List<DamageResolutionRecord> damageResolutions,
            List<FlipContactResolution> contactResolutions,
            List<FlipDueContactPresentationSignal> contactPresentationSignals)
        {
            switch (contact.Kind)
            {
                case ScheduledFlipContactKind.OrdinaryLanding:
                    return;
                case ScheduledFlipContactKind.HostileImpact:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contact), contact.Kind, "Unknown scheduled flip contact kind.");
            }

            if (stageAlreadyTerminal)
            {
                CancelWithSafeReturn(
                    snapshot,
                    contact,
                    currentTick,
                    FlipContactResolutionKind.CancelledStageTerminal,
                    "StageTerminal",
                    batch,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            if (!snapshot.TryGetEntity(contact.SourceBoxEntityId, out var sourceBox))
            {
                CancelNoSourceBox(
                    contact,
                    currentTick,
                    "BoxGone",
                    batch,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            if (sourceBox.boardPresence != EntityBoardPresence.InFlight)
            {
                CancelNoSourceBox(
                    contact,
                    currentTick,
                    "BoxNotInFlight",
                    batch,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            if (!snapshot.TryGetEntity(contact.ActorEntityId, out var actor) ||
                actor.hp <= 0 ||
                actor.markedForDeath)
            {
                CancelWithSafeReturn(
                    snapshot,
                    contact,
                    currentTick,
                    FlipContactResolutionKind.CancelledActorGone,
                    "ActorGone",
                    batch,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            if (!IsTopologyValid(snapshot, contact))
            {
                CancelWithSafeReturn(
                    snapshot,
                    contact,
                    currentTick,
                    FlipContactResolutionKind.CancelledTopologyChanged,
                    "TopologyChanged",
                    batch,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            if (!snapshot.IsInsideBoard(contact.ContactCell))
            {
                ResolveBlocked(
                    snapshot,
                    contact,
                    currentTick,
                    FlipContactResolutionKind.BlockedByBoardEdge,
                    "BlockedByBoardEdge",
                    batch,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            if (snapshot.IsTerrainBlockedForUnit(contact.ContactCell))
            {
                ResolveBlocked(
                    snapshot,
                    contact,
                    currentTick,
                    FlipContactResolutionKind.BlockedByTerrain,
                    "BlockedByTerrain",
                    batch,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            var occupant = ResolveFlipContactOccupant(snapshot, contact.ContactCell);
            switch (occupant.Kind)
            {
                case FlipContactOccupantKind.Unit:
                    ResolveUnitContact(
                        snapshot,
                        contact,
                        occupant.Entity,
                        currentTick,
                        batch,
                        postCleanupBatch,
                        eventLogEntries,
                        damageResolutions,
                        contactResolutions,
                        contactPresentationSignals);
                    return;

                case FlipContactOccupantKind.Solid:
                    ResolveBlocked(
                        snapshot,
                        contact,
                        currentTick,
                        FlipContactResolutionKind.BlockedBySolid,
                        "BlockedBySolid",
                        batch,
                        eventLogEntries,
                        contactResolutions,
                        contactPresentationSignals,
                        occupant.Entity.entityId);
                    return;

                case FlipContactOccupantKind.Projectile:
                case FlipContactOccupantKind.Empty:
                    ResolveEmptyOrProjectileContact(
                        snapshot,
                        contact,
                        currentTick,
                        batch,
                        eventLogEntries,
                        contactResolutions,
                        contactPresentationSignals);
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ResolveUnitContact(
            WorldSnapshot snapshot,
            in ScheduledFlipContact contact,
            in EntityState occupant,
            int currentTick,
            FinalizationBatch batch,
            FinalizationBatch postCleanupBatch,
            List<string> eventLogEntries,
            List<DamageResolutionRecord> damageResolutions,
            List<FlipContactResolution> contactResolutions,
            List<FlipDueContactPresentationSignal> contactPresentationSignals)
        {
            var isPlayer = EntityRolePolicy.IsPlayerUnit(occupant);
            var isFriendly = occupant.teamId == contact.KineticInstigatorTeamId;
            if (isPlayer || isFriendly)
            {
                ResolveBlocked(
                    snapshot,
                    contact,
                    currentTick,
                    FlipContactResolutionKind.BlockedByFriendly,
                    "BlockedByFriendly",
                    batch,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals,
                    occupant.entityId);
                return;
            }

            var projectedHp = occupant.hp - contact.DamageSpec.DamageAmount;
            var targetDies = projectedHp <= 0 || occupant.markedForDeath;
            var metadata = CreateDueMetadata(contact, occupant.entityId);
            batch.ApplyDamage(occupant.entityId, contact.DamageSpec.DamageAmount, metadata);
            damageResolutions.Add(
                new DamageResolutionRecord(
                    contact.ActionId,
                    contact.ActionId,
                    contact.KineticInstigatorEntityId,
                    contact.DamageSpec.SourceKind,
                    occupant.entityId,
                    contact.DamageSpec.DamageAmount,
                    accepted: true,
                    DamageRejectReason.None));

            if (!targetDies)
            {
                batch.MarkDestroy(contact.SourceBoxEntityId, metadata);
                batch.RemoveScheduledFlipContact(contact.ActionId, metadata);
                AddResolution(
                    contact,
                    currentTick,
                    FlipContactResolutionKind.HitHostileSurvived,
                    occupant.entityId,
                    null,
                    FlipBoxDisposition.DestroySelf,
                    "HitHostileSurvived",
                    snapshot.Topology,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            batch.MarkDestroy(occupant.entityId, metadata);
            var destroyResolution = new DestroyResolutionRecord(
                contact.ActionId,
                contact.ActionId,
                contact.KineticInstigatorEntityId,
                occupant.entityId,
                DestroyCondition.WhenHpDepleted,
                projectedHp,
                accepted: true,
                localActionIndex: 0);
            var followThroughAllowed = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, contact.SourceBoxEntityId, EntityType.Box),
                    contact.LandingCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new ImpactFollowThroughEvidence(
                    contact.KineticInstigatorEntityId,
                    new[] { occupant.entityId },
                    new[] { destroyResolution })).Verdict == LegalityVerdict.Allowed;

            if (followThroughAllowed)
            {
                MaterializeSourceBox(contact, contact.LandingCell, postCleanupBatch, metadata);
                batch.RemoveScheduledFlipContact(contact.ActionId, metadata);
                AddResolution(
                    contact,
                    currentTick,
                    FlipContactResolutionKind.HitHostileDiedSettlementAllowed,
                    occupant.entityId,
                    contact.LandingCell,
                    FlipBoxDisposition.MaterializeAtLanding,
                    "HitHostileDiedSettlementAllowed",
                    snapshot.Topology,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            var fallbackDisposition = SafeReturnOrDestroyInFlightBox(snapshot, contact, batch, metadata, out var materializeCell);
            batch.RemoveScheduledFlipContact(contact.ActionId, metadata);
            AddResolution(
                contact,
                currentTick,
                FlipContactResolutionKind.HitHostileDiedSettlementDenied,
                occupant.entityId,
                materializeCell,
                fallbackDisposition,
                "HitHostileDiedSettlementDenied",
                snapshot.Topology,
                eventLogEntries,
                contactResolutions,
                contactPresentationSignals);
        }

        private void ResolveEmptyOrProjectileContact(
            WorldSnapshot snapshot,
            in ScheduledFlipContact contact,
            int currentTick,
            FinalizationBatch batch,
            List<string> eventLogEntries,
            List<FlipContactResolution> contactResolutions,
            List<FlipDueContactPresentationSignal> contactPresentationSignals)
        {
            var metadata = CreateDueMetadata(contact);
            var landingLegality = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                snapshot,
                EntityType.Box,
                contact.LandingCell,
                contact.SourceBoxEntityId);

            if (landingLegality.Verdict == LegalityVerdict.Allowed)
            {
                MaterializeSourceBox(contact, contact.LandingCell, batch, metadata);
                batch.RemoveScheduledFlipContact(contact.ActionId, metadata);
                AddResolution(
                    contact,
                    currentTick,
                    FlipContactResolutionKind.EmptyLand,
                    null,
                    contact.LandingCell,
                    FlipBoxDisposition.MaterializeAtLanding,
                    "EmptyLand",
                    snapshot.Topology,
                    eventLogEntries,
                    contactResolutions,
                    contactPresentationSignals);
                return;
            }

            var disposition = SafeReturnOrDestroyInFlightBox(snapshot, contact, batch, metadata, out var materializeCell);
            batch.RemoveScheduledFlipContact(contact.ActionId, metadata);
            AddResolution(
                contact,
                currentTick,
                FlipContactResolutionKind.Whiff,
                null,
                materializeCell,
                disposition,
                "WhiffSettlementDenied",
                snapshot.Topology,
                eventLogEntries,
                contactResolutions,
                contactPresentationSignals);
        }

        private void ResolveBlocked(
            WorldSnapshot snapshot,
            in ScheduledFlipContact contact,
            int currentTick,
            FlipContactResolutionKind kind,
            string result,
            FinalizationBatch batch,
            List<string> eventLogEntries,
            List<FlipContactResolution> contactResolutions,
            List<FlipDueContactPresentationSignal> contactPresentationSignals,
            int hitEntityId = 0)
        {
            var metadata = CreateDueMetadata(contact, hitEntityId);
            var disposition = SafeReturnOrDestroyInFlightBox(snapshot, contact, batch, metadata, out var materializeCell);
            batch.RemoveScheduledFlipContact(contact.ActionId, metadata);
            AddResolution(
                contact,
                currentTick,
                kind,
                hitEntityId > 0 ? hitEntityId : null,
                materializeCell,
                disposition,
                result,
                snapshot.Topology,
                eventLogEntries,
                contactResolutions,
                contactPresentationSignals);
        }

        private void CancelWithSafeReturn(
            WorldSnapshot snapshot,
            in ScheduledFlipContact contact,
            int currentTick,
            FlipContactResolutionKind kind,
            string reason,
            FinalizationBatch batch,
            List<string> eventLogEntries,
            List<FlipContactResolution> contactResolutions,
            List<FlipDueContactPresentationSignal> contactPresentationSignals)
        {
            var metadata = CreateDueMetadata(contact);
            var disposition = SafeReturnOrDestroyInFlightBox(snapshot, contact, batch, metadata, out var materializeCell);
            batch.RemoveScheduledFlipContact(contact.ActionId, metadata);
            AddResolution(
                contact,
                currentTick,
                kind,
                null,
                materializeCell,
                disposition,
                reason,
                snapshot.Topology,
                eventLogEntries,
                contactResolutions,
                contactPresentationSignals);
            eventLogEntries.Add(
                $"FlipB1Cancelled|Tick={currentTick}|Action={contact.ActionId}|Box={contact.SourceBoxEntityId}|Reason={reason}");
        }

        private void CancelNoSourceBox(
            in ScheduledFlipContact contact,
            int currentTick,
            string reason,
            FinalizationBatch batch,
            List<string> eventLogEntries,
            List<FlipContactResolution> contactResolutions,
            List<FlipDueContactPresentationSignal> contactPresentationSignals)
        {
            var metadata = CreateDueMetadata(contact);
            batch.RemoveScheduledFlipContact(contact.ActionId, metadata);
            AddResolution(
                contact,
                currentTick,
                FlipContactResolutionKind.CancelledBoxGone,
                null,
                null,
                FlipBoxDisposition.Cancelled,
                reason,
                default,
                eventLogEntries,
                contactResolutions,
                contactPresentationSignals);
            eventLogEntries.Add(
                $"FlipB1Cancelled|Tick={currentTick}|Action={contact.ActionId}|Box={contact.SourceBoxEntityId}|Reason={reason}");
        }

        private FlipBoxDisposition SafeReturnOrDestroyInFlightBox(
            WorldSnapshot snapshot,
            in ScheduledFlipContact contact,
            FinalizationBatch batch,
            in FinalizationOperationMetadata metadata,
            out SurfaceCell? materializeCell)
        {
            materializeCell = null;
            if (!snapshot.TryGetEntity(contact.SourceBoxEntityId, out var sourceBox) ||
                sourceBox.boardPresence != EntityBoardPresence.InFlight)
            {
                return FlipBoxDisposition.Cancelled;
            }

            var sourceLegality = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                snapshot,
                EntityType.Box,
                contact.SourceCell,
                contact.SourceBoxEntityId);
            if (sourceLegality.Verdict == LegalityVerdict.Allowed)
            {
                materializeCell = contact.SourceCell;
                MaterializeSourceBox(contact, contact.SourceCell, batch, metadata);
                return FlipBoxDisposition.MaterializeAtSource;
            }

            batch.MarkDestroy(contact.SourceBoxEntityId, metadata);
            return FlipBoxDisposition.DestroySelf;
        }

        private FlipContactOccupant ResolveFlipContactOccupant(WorldSnapshot snapshot, SurfaceCell contactCell)
        {
            snapshot.EnumerateUnitsAt(contactCell, _unitBuffer);
            if (_unitBuffer.Count > 0)
            {
                return new FlipContactOccupant(FlipContactOccupantKind.Unit, _unitBuffer[0]);
            }

            if (snapshot.TryGetSolidSemanticAt(contactCell, out var solid))
            {
                return new FlipContactOccupant(FlipContactOccupantKind.Solid, solid.Entity);
            }

            if (snapshot.TryGetProjectileAt(contactCell, out var projectile))
            {
                return new FlipContactOccupant(FlipContactOccupantKind.Projectile, projectile);
            }

            return new FlipContactOccupant(FlipContactOccupantKind.Empty, default);
        }

        private static bool IsTopologyValid(WorldSnapshot snapshot, in ScheduledFlipContact contact)
        {
            return snapshot.Topology.IsFaceActive(contact.SourceFace) &&
                   snapshot.Topology.IsFaceActive(contact.SourceCell.face) &&
                   snapshot.Topology.IsFaceActive(contact.ContactCell.face) &&
                   snapshot.Topology.IsFaceActive(contact.LandingCell.face);
        }

        private static void MaterializeSourceBox(
            in ScheduledFlipContact contact,
            SurfaceCell destination,
            FinalizationBatch batch,
            in FinalizationOperationMetadata metadata)
        {
            batch.MoveEntity(contact.SourceBoxEntityId, destination, metadata);
            batch.SetBoardPresence(contact.SourceBoxEntityId, EntityBoardPresence.Occupying, metadata);
        }

        private static FinalizationOperationMetadata CreateDueMetadata(
            in ScheduledFlipContact contact,
            int hitEntityId = 0)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.Impact,
                contact.ActorEntityId,
                contact.ActionId,
                intentId: contact.ActionId,
                exitCauseHint: TickEntityExitCause.DestroyedByImpact,
                attackSourceKind: contact.DamageSpec.SourceKind,
                movementSemanticKind: MovementSemanticKind.Impact,
                damageSourceType: DamageSourceType.Impact,
                presentationTargetCell: contact.ContactCell,
                hasPresentationTargetCell: true,
                boundaryReason: hitEntityId > 0 ? $"FlipB1DueHit:{hitEntityId}" : "FlipB1Due");
        }

        private static void AddResolution(
            in ScheduledFlipContact contact,
            int currentTick,
            FlipContactResolutionKind kind,
            int? hitEntityId,
            SurfaceCell? materializeCell,
            FlipBoxDisposition disposition,
            string result,
            CubeTopologyState topology,
            List<string> eventLogEntries,
            List<FlipContactResolution> contactResolutions,
            List<FlipDueContactPresentationSignal> contactPresentationSignals)
        {
            contactResolutions.Add(
                new FlipContactResolution(
                    kind,
                    hitEntityId,
                    contact.SourceBoxEntityId,
                    contact.ContactCell,
                    materializeCell,
                    disposition));
            contactPresentationSignals.Add(
                new FlipDueContactPresentationSignal(
                    contact.ActionId,
                    contact.SourceBoxEntityId,
                    contact.ActorEntityId,
                    hitEntityId.GetValueOrDefault(),
                    contact.SourceCell,
                    contact.ContactCell,
                    contact.LandingCell,
                    topology,
                    contact.FlipDirection,
                    contact.FlipDirection,
                    kind,
                    disposition,
                    materializeCell.HasValue,
                    materializeCell.GetValueOrDefault(),
                    BuildStableDuePresentationSeed(currentTick, contact)));
            eventLogEntries.Add(
                $"FlipB1Due|currentTick={currentTick}|Tick={currentTick}|Action={contact.ActionId}|Box={contact.SourceBoxEntityId}|Contact={FormatCell(contact.ContactCell)}|Occupant={(hitEntityId.HasValue ? hitEntityId.Value.ToString() : "None")}|ActionNormAtDue={FormatActionNormAtDue(contact)}|DamagePath={contact.DamageSpec.SourceKind}|Result={result}");
            eventLogEntries.Add(
                $"FlipB1Disposition|Tick={currentTick}|Box={contact.SourceBoxEntityId}|Disposition={disposition}");
            eventLogEntries.Add(
                $"FlipB1Removed|Tick={currentTick}|Action={contact.ActionId}|Box={contact.SourceBoxEntityId}");
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return $"({cell.face},{cell.x},{cell.y})";
        }

        private static string FormatActionNormAtDue(in ScheduledFlipContact contact)
        {
            if (contact.FlipInputLockDurationTicks <= 0)
            {
                return "0";
            }

            var dueMinusActionStart = Math.Max(0, contact.DueTick - contact.ActionStartTick);
            return (dueMinusActionStart / (float)contact.FlipInputLockDurationTicks)
                .ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static int BuildStableDuePresentationSeed(int currentTick, in ScheduledFlipContact contact)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + currentTick;
                hash = (hash * 31) + contact.ActionId;
                hash = (hash * 31) + contact.SourceBoxEntityId;
                hash = (hash * 31) + contact.ContactCell.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

        private readonly struct FlipContactOccupant
        {
            public FlipContactOccupant(FlipContactOccupantKind kind, EntityState entity)
            {
                Kind = kind;
                Entity = entity;
            }

            public FlipContactOccupantKind Kind { get; }

            public EntityState Entity { get; }
        }

        private enum FlipContactOccupantKind
        {
            Empty = 0,
            Unit = 1,
            Solid = 2,
            Projectile = 3,
        }
    }

    internal sealed class FlipScheduledContactDueResult
    {
        public static readonly FlipScheduledContactDueResult Empty = new(
            new FinalizationBatch(),
            new FinalizationBatch(),
            Array.Empty<string>(),
            Array.Empty<DamageResolutionRecord>(),
            Array.Empty<FlipContactResolution>(),
            Array.Empty<FlipDueContactPresentationSignal>());

        public FlipScheduledContactDueResult(
            FinalizationBatch batch,
            FinalizationBatch postCleanupBatch,
            IReadOnlyList<string> eventLogEntries,
            IReadOnlyList<DamageResolutionRecord> damageResolutions,
            IReadOnlyList<FlipContactResolution> contactResolutions,
            IReadOnlyList<FlipDueContactPresentationSignal> contactPresentationSignals)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            PostCleanupBatch = postCleanupBatch ?? throw new ArgumentNullException(nameof(postCleanupBatch));
            EventLogEntries = eventLogEntries ?? throw new ArgumentNullException(nameof(eventLogEntries));
            DamageResolutions = damageResolutions ?? throw new ArgumentNullException(nameof(damageResolutions));
            ContactResolutions = contactResolutions ?? throw new ArgumentNullException(nameof(contactResolutions));
            ContactPresentationSignals = contactPresentationSignals ??
                                         throw new ArgumentNullException(nameof(contactPresentationSignals));
        }

        public FinalizationBatch Batch { get; }

        public FinalizationBatch PostCleanupBatch { get; }

        public IReadOnlyList<string> EventLogEntries { get; }

        public IReadOnlyList<DamageResolutionRecord> DamageResolutions { get; }

        public IReadOnlyList<FlipContactResolution> ContactResolutions { get; }

        public IReadOnlyList<FlipDueContactPresentationSignal> ContactPresentationSignals { get; }

        public bool HasWork =>
            Batch.Operations.Count > 0 ||
            Batch.TileFeatureOperations.Count > 0 ||
            PostCleanupBatch.Operations.Count > 0 ||
            PostCleanupBatch.TileFeatureOperations.Count > 0 ||
            EventLogEntries.Count > 0;
    }
}
