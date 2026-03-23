using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Model.Groups;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class MovementPhaseResult
    {
        public static readonly MovementPhaseResult Empty = new(
            Array.Empty<RawMovementIntent>(),
            Array.Empty<MoveIntent>(),
            Array.Empty<ActionGroup>(),
            Array.Empty<ActionGroup>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        private readonly ReadOnlyCollection<string> _commitEvents;
        private readonly ReadOnlyCollection<ActionGroup> _expandedCandidates;
        private readonly ReadOnlyCollection<RawMovementIntent> _rawIntents;
        private readonly ReadOnlyCollection<string> _rejectedReasons;
        private readonly ReadOnlyCollection<ActionGroup> _selectedGroups;
        private readonly ReadOnlyCollection<MoveIntent> _sortedIntents;

        public MovementPhaseResult(
            IEnumerable<RawMovementIntent> rawIntents,
            IEnumerable<MoveIntent> sortedIntents,
            IEnumerable<ActionGroup> expandedCandidates,
            IEnumerable<ActionGroup> selectedGroups,
            IEnumerable<string> commitEvents,
            IEnumerable<string> rejectedReasons)
        {
            if (rawIntents == null)
            {
                throw new ArgumentNullException(nameof(rawIntents));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
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

            _rawIntents = new ReadOnlyCollection<RawMovementIntent>(new List<RawMovementIntent>(rawIntents));
            _sortedIntents = new ReadOnlyCollection<MoveIntent>(new List<MoveIntent>(sortedIntents));
            _expandedCandidates = new ReadOnlyCollection<ActionGroup>(new List<ActionGroup>(expandedCandidates));
            _selectedGroups = new ReadOnlyCollection<ActionGroup>(new List<ActionGroup>(selectedGroups));
            _commitEvents = new ReadOnlyCollection<string>(new List<string>(commitEvents));
            _rejectedReasons = new ReadOnlyCollection<string>(new List<string>(rejectedReasons));
        }

        public IReadOnlyList<RawMovementIntent> RawIntents => _rawIntents;

        public IReadOnlyList<MoveIntent> SortedIntents => _sortedIntents;

        public IReadOnlyList<ActionGroup> ExpandedCandidates => _expandedCandidates;

        public IReadOnlyList<ActionGroup> SelectedGroups => _selectedGroups;

        public IReadOnlyList<string> CommitEvents => _commitEvents;

        public IReadOnlyList<string> RejectedReasons => _rejectedReasons;
    }
}
