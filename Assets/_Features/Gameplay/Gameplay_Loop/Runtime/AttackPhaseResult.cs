using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct DamageResolutionRecord
    {
        private readonly int _actionPlanId;
        private readonly int _intentId;

        public DamageResolutionRecord(
            int groupId,
            int intentId,
            int sourceId,
            AttackSourceKind sourceKind,
            int targetId,
            int amount,
            bool accepted,
            DamageRejectReason rejectReason,
            int localActionIndex = 0,
            bool hasPlayerDamageState = false,
            PlayerDamageState playerDamageState = default)
        {
            _actionPlanId = groupId;
            _intentId = intentId;
            SourceId = sourceId;
            SourceKind = sourceKind;
            TargetId = targetId;
            Amount = amount;
            Accepted = accepted;
            RejectReason = rejectReason;
            LocalActionIndex = localActionIndex;
            HasPlayerDamageState = hasPlayerDamageState;
            PlayerDamageState = playerDamageState;
        }

        public int ActionPlanId => _actionPlanId;

        [Obsolete("Legacy alias for ActionPlanId. Prefer ActionPlanId for correlation and semantic fields such as SourceId, SourceKind, TargetId, Amount, Accepted, and RejectReason.")]
        public int GroupId => _actionPlanId;

        [Obsolete("IR metadata only. Prefer ActionPlanId for plan correlation and semantic fields such as SourceId, SourceKind, TargetId, Amount, Accepted, and RejectReason.")]
        public int IntentId => _intentId;

        public int SourceId { get; }

        public AttackSourceKind SourceKind { get; }

        public int TargetId { get; }

        public int Amount { get; }

        public bool Accepted { get; }

        public DamageRejectReason RejectReason { get; }

        public int LocalActionIndex { get; }

        public bool HasPlayerDamageState { get; }

        public PlayerDamageState PlayerDamageState { get; }
    }

    internal sealed class AttackPhaseResult
    {
        public static readonly AttackPhaseResult Empty = new(
            Array.Empty<RawAttackIntent>(),
            Array.Empty<ImpactReservation>(),
            Array.Empty<DelayedAttackEffectRecord>(),
            Array.Empty<DamageResolutionRecord>(),
            Array.Empty<ResolutionRecord>(),
            Array.Empty<FinalizationOperation>(),
            Array.Empty<DelayedAttackEffectRecord>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            FrozenMovementReservationExport.Empty,
            Array.Empty<MotionInterruptRecord>());

        private readonly ReadOnlyCollection<string> _commitEvents;
        private readonly ReadOnlyCollection<DamageResolutionRecord> _damageResolutions;
        private readonly ReadOnlyCollection<DelayedAttackEffectRecord> _drainedDelayedAttackEffects;
        private readonly ReadOnlyCollection<ImpactReservation> _drainedImpactReservations;
        private readonly ReadOnlyCollection<string> _eventLogEntries;
        private readonly FrozenMovementReservationExport _frozenMovementReservationExport;
        private readonly ReadOnlyCollection<MotionInterruptRecord> _motionInterruptRecords;
        private readonly ReadOnlyCollection<DelayedAttackEffectRecord> _queuedDelayedAttackEffects;
        private readonly ReadOnlyCollection<RawAttackIntent> _rawIntents;
        private readonly ReadOnlyCollection<string> _rejectedReasons;
        private readonly ReadOnlyCollection<ResolutionRecord> _resolutionRecords;
        private readonly ReadOnlyCollection<FinalizationOperation> _resolvedOperations;

        public AttackPhaseResult(
            IEnumerable<RawAttackIntent> rawIntents,
            IEnumerable<ImpactReservation> drainedImpactReservations,
            IEnumerable<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            IEnumerable<DamageResolutionRecord> damageResolutions,
            IEnumerable<ResolutionRecord> resolutionRecords,
            IEnumerable<FinalizationOperation> resolvedOperations,
            IEnumerable<DelayedAttackEffectRecord> queuedDelayedAttackEffects,
            IEnumerable<string> commitEvents,
            IEnumerable<string> eventLogEntries,
            IEnumerable<string> rejectedReasons,
            IEnumerable<MotionInterruptRecord> motionInterruptRecords = null)
            : this(
                rawIntents,
                drainedImpactReservations,
                drainedDelayedAttackEffects,
                damageResolutions,
                resolutionRecords,
                resolvedOperations,
                queuedDelayedAttackEffects,
                commitEvents,
                eventLogEntries,
                rejectedReasons,
                FrozenMovementReservationExport.Empty,
                motionInterruptRecords)
        {
        }

        public AttackPhaseResult(
            IEnumerable<RawAttackIntent> rawIntents,
            IEnumerable<ImpactReservation> drainedImpactReservations,
            IEnumerable<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            IEnumerable<DamageResolutionRecord> damageResolutions,
            IEnumerable<ResolutionRecord> resolutionRecords,
            IEnumerable<FinalizationOperation> resolvedOperations,
            IEnumerable<DelayedAttackEffectRecord> queuedDelayedAttackEffects,
            IEnumerable<string> commitEvents,
            IEnumerable<string> eventLogEntries,
            IEnumerable<string> rejectedReasons,
            FrozenMovementReservationExport frozenMovementReservationExport,
            IEnumerable<MotionInterruptRecord> motionInterruptRecords = null)
        {
            if (rawIntents == null)
            {
                throw new ArgumentNullException(nameof(rawIntents));
            }

            if (drainedImpactReservations == null)
            {
                throw new ArgumentNullException(nameof(drainedImpactReservations));
            }

            if (drainedDelayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(drainedDelayedAttackEffects));
            }

            if (damageResolutions == null)
            {
                throw new ArgumentNullException(nameof(damageResolutions));
            }

            if (resolutionRecords == null)
            {
                throw new ArgumentNullException(nameof(resolutionRecords));
            }

            if (resolvedOperations == null)
            {
                throw new ArgumentNullException(nameof(resolvedOperations));
            }

            if (queuedDelayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(queuedDelayedAttackEffects));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            if (eventLogEntries == null)
            {
                throw new ArgumentNullException(nameof(eventLogEntries));
            }

            if (rejectedReasons == null)
            {
                throw new ArgumentNullException(nameof(rejectedReasons));
            }

            if (frozenMovementReservationExport == null)
            {
                throw new ArgumentNullException(nameof(frozenMovementReservationExport));
            }

            _rawIntents = new ReadOnlyCollection<RawAttackIntent>(new List<RawAttackIntent>(rawIntents));
            _drainedImpactReservations = new ReadOnlyCollection<ImpactReservation>(new List<ImpactReservation>(drainedImpactReservations));
            _drainedDelayedAttackEffects = new ReadOnlyCollection<DelayedAttackEffectRecord>(new List<DelayedAttackEffectRecord>(drainedDelayedAttackEffects));
            _damageResolutions = new ReadOnlyCollection<DamageResolutionRecord>(new List<DamageResolutionRecord>(damageResolutions));
            _resolutionRecords = new ReadOnlyCollection<ResolutionRecord>(new List<ResolutionRecord>(resolutionRecords));
            _resolvedOperations = new ReadOnlyCollection<FinalizationOperation>(new List<FinalizationOperation>(resolvedOperations));
            _queuedDelayedAttackEffects = new ReadOnlyCollection<DelayedAttackEffectRecord>(new List<DelayedAttackEffectRecord>(queuedDelayedAttackEffects));
            _commitEvents = new ReadOnlyCollection<string>(new List<string>(commitEvents));
            _eventLogEntries = new ReadOnlyCollection<string>(new List<string>(eventLogEntries));
            _rejectedReasons = new ReadOnlyCollection<string>(new List<string>(rejectedReasons));
            _frozenMovementReservationExport = frozenMovementReservationExport;
            _motionInterruptRecords = new ReadOnlyCollection<MotionInterruptRecord>(
                new List<MotionInterruptRecord>(
                    motionInterruptRecords ?? Array.Empty<MotionInterruptRecord>()));
        }

        public IReadOnlyList<RawAttackIntent> RawIntents => _rawIntents;

        public IReadOnlyList<ImpactReservation> DrainedImpactReservations => _drainedImpactReservations;

        internal FrozenMovementReservationExport FrozenMovementReservationExport => _frozenMovementReservationExport;

        public IReadOnlyList<DelayedAttackEffectRecord> DrainedDelayedAttackEffects => _drainedDelayedAttackEffects;

        public IReadOnlyList<DamageResolutionRecord> DamageResolutions => _damageResolutions;

        public IReadOnlyList<ResolutionRecord> ResolutionRecords => _resolutionRecords;

        public IReadOnlyList<FinalizationOperation> ResolvedOperations => _resolvedOperations;

        public IReadOnlyList<DelayedAttackEffectRecord> QueuedDelayedAttackEffects => _queuedDelayedAttackEffects;

        public IReadOnlyList<string> CommitEvents => _commitEvents;

        public IReadOnlyList<string> EventLogEntries => _eventLogEntries;

        public IReadOnlyList<string> RejectedReasons => _rejectedReasons;

        public IReadOnlyList<MotionInterruptRecord> MotionInterruptRecords => _motionInterruptRecords;
    }
}
