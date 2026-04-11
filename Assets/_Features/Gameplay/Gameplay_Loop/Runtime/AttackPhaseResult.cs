using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct DamageResolutionRecord
    {
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
            GroupId = groupId;
            IntentId = intentId;
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

        public int GroupId { get; }

        public int IntentId { get; }

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
            Array.Empty<AttackIntent>(),
            Array.Empty<ActionGroup>(),
            Array.Empty<ActionGroup>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        private readonly ReadOnlyCollection<string> _commitEvents;
        private readonly ReadOnlyCollection<DamageResolutionRecord> _damageResolutions;
        private readonly ReadOnlyCollection<DelayedAttackEffectRecord> _drainedDelayedAttackEffects;
        private readonly ReadOnlyCollection<ImpactReservation> _drainedImpactReservations;
        private readonly ReadOnlyCollection<string> _eventLogEntries;
        private readonly ReadOnlyCollection<ActionGroup> _expandedCandidates;
        private readonly ReadOnlyCollection<RawAttackIntent> _rawIntents;
        private readonly ReadOnlyCollection<string> _rejectedReasons;
        private readonly ReadOnlyCollection<ActionGroup> _selectedGroups;
        private readonly ReadOnlyCollection<AttackIntent> _sortedInputs;

        public AttackPhaseResult(
            IEnumerable<RawAttackIntent> rawIntents,
            IEnumerable<ImpactReservation> drainedImpactReservations,
            IEnumerable<AttackIntent> sortedInputs,
            IEnumerable<ActionGroup> expandedCandidates,
            IEnumerable<ActionGroup> selectedGroups,
            IEnumerable<string> commitEvents,
            IEnumerable<string> rejectedReasons)
            : this(
                rawIntents,
                drainedImpactReservations,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<DamageResolutionRecord>(),
                sortedInputs,
                expandedCandidates,
                selectedGroups,
                commitEvents,
                commitEvents,
                rejectedReasons)
        {
        }

        public AttackPhaseResult(
            IEnumerable<RawAttackIntent> rawIntents,
            IEnumerable<ImpactReservation> drainedImpactReservations,
            IEnumerable<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            IEnumerable<DamageResolutionRecord> damageResolutions,
            IEnumerable<AttackIntent> sortedInputs,
            IEnumerable<ActionGroup> expandedCandidates,
            IEnumerable<ActionGroup> selectedGroups,
            IEnumerable<string> commitEvents,
            IEnumerable<string> eventLogEntries,
            IEnumerable<string> rejectedReasons)
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

            if (sortedInputs == null)
            {
                throw new ArgumentNullException(nameof(sortedInputs));
            }

            if (expandedCandidates == null)
            {
                throw new ArgumentNullException(nameof(expandedCandidates));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
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

            _rawIntents = new ReadOnlyCollection<RawAttackIntent>(new List<RawAttackIntent>(rawIntents));
            _drainedImpactReservations = new ReadOnlyCollection<ImpactReservation>(new List<ImpactReservation>(drainedImpactReservations));
            _drainedDelayedAttackEffects = new ReadOnlyCollection<DelayedAttackEffectRecord>(new List<DelayedAttackEffectRecord>(drainedDelayedAttackEffects));
            _damageResolutions = new ReadOnlyCollection<DamageResolutionRecord>(new List<DamageResolutionRecord>(damageResolutions));
            _sortedInputs = new ReadOnlyCollection<AttackIntent>(new List<AttackIntent>(sortedInputs));
            _expandedCandidates = new ReadOnlyCollection<ActionGroup>(new List<ActionGroup>(expandedCandidates));
            _selectedGroups = new ReadOnlyCollection<ActionGroup>(new List<ActionGroup>(selectedGroups));
            _commitEvents = new ReadOnlyCollection<string>(new List<string>(commitEvents));
            _eventLogEntries = new ReadOnlyCollection<string>(new List<string>(eventLogEntries));
            _rejectedReasons = new ReadOnlyCollection<string>(new List<string>(rejectedReasons));
        }

        public IReadOnlyList<RawAttackIntent> RawIntents => _rawIntents;

        public IReadOnlyList<ImpactReservation> DrainedImpactReservations => _drainedImpactReservations;

        public IReadOnlyList<DelayedAttackEffectRecord> DrainedDelayedAttackEffects => _drainedDelayedAttackEffects;

        public IReadOnlyList<DamageResolutionRecord> DamageResolutions => _damageResolutions;

        public IReadOnlyList<AttackIntent> SortedInputs => _sortedInputs;

        public IReadOnlyList<ActionGroup> ExpandedCandidates => _expandedCandidates;

        public IReadOnlyList<ActionGroup> SelectedGroups => _selectedGroups;

        public IReadOnlyList<string> CommitEvents => _commitEvents;

        public IReadOnlyList<string> EventLogEntries => _eventLogEntries;

        public IReadOnlyList<string> RejectedReasons => _rejectedReasons;
    }
}
