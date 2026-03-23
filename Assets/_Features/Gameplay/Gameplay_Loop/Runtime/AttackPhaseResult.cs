using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Model.Groups;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class AttackPhaseResult
    {
        public static readonly AttackPhaseResult Empty = new(
            Array.Empty<RawAttackIntent>(),
            Array.Empty<ImpactReservation>(),
            Array.Empty<AttackIntent>(),
            Array.Empty<ActionGroup>(),
            Array.Empty<ActionGroup>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        private readonly ReadOnlyCollection<string> _commitEvents;
        private readonly ReadOnlyCollection<ImpactReservation> _drainedImpactReservations;
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
        {
            if (rawIntents == null)
            {
                throw new ArgumentNullException(nameof(rawIntents));
            }

            if (drainedImpactReservations == null)
            {
                throw new ArgumentNullException(nameof(drainedImpactReservations));
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

            if (rejectedReasons == null)
            {
                throw new ArgumentNullException(nameof(rejectedReasons));
            }

            _rawIntents = new ReadOnlyCollection<RawAttackIntent>(new List<RawAttackIntent>(rawIntents));
            _drainedImpactReservations = new ReadOnlyCollection<ImpactReservation>(new List<ImpactReservation>(drainedImpactReservations));
            _sortedInputs = new ReadOnlyCollection<AttackIntent>(new List<AttackIntent>(sortedInputs));
            _expandedCandidates = new ReadOnlyCollection<ActionGroup>(new List<ActionGroup>(expandedCandidates));
            _selectedGroups = new ReadOnlyCollection<ActionGroup>(new List<ActionGroup>(selectedGroups));
            _commitEvents = new ReadOnlyCollection<string>(new List<string>(commitEvents));
            _rejectedReasons = new ReadOnlyCollection<string>(new List<string>(rejectedReasons));
        }

        public IReadOnlyList<RawAttackIntent> RawIntents => _rawIntents;

        public IReadOnlyList<ImpactReservation> DrainedImpactReservations => _drainedImpactReservations;

        public IReadOnlyList<AttackIntent> SortedInputs => _sortedInputs;

        public IReadOnlyList<ActionGroup> ExpandedCandidates => _expandedCandidates;

        public IReadOnlyList<ActionGroup> SelectedGroups => _selectedGroups;

        public IReadOnlyList<string> CommitEvents => _commitEvents;

        public IReadOnlyList<string> RejectedReasons => _rejectedReasons;
    }
}
