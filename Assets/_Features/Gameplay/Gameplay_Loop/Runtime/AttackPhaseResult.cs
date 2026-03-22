using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Model.Groups;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class AttackPhaseResult
    {
        public static readonly AttackPhaseResult Empty = new(
            Array.Empty<AttackIntent>(),
            Array.Empty<ActionGroup>(),
            Array.Empty<ActionGroup>(),
            Array.Empty<string>());

        private readonly ReadOnlyCollection<string> _commitEvents;
        private readonly ReadOnlyCollection<ActionGroup> _expandedCandidates;
        private readonly ReadOnlyCollection<ActionGroup> _selectedGroups;
        private readonly ReadOnlyCollection<AttackIntent> _sortedInputs;

        public AttackPhaseResult(
            IEnumerable<AttackIntent> sortedInputs,
            IEnumerable<ActionGroup> expandedCandidates,
            IEnumerable<ActionGroup> selectedGroups,
            IEnumerable<string> commitEvents)
        {
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

            _sortedInputs = new ReadOnlyCollection<AttackIntent>(new List<AttackIntent>(sortedInputs));
            _expandedCandidates = new ReadOnlyCollection<ActionGroup>(new List<ActionGroup>(expandedCandidates));
            _selectedGroups = new ReadOnlyCollection<ActionGroup>(new List<ActionGroup>(selectedGroups));
            _commitEvents = new ReadOnlyCollection<string>(new List<string>(commitEvents));
        }

        public IReadOnlyList<AttackIntent> SortedInputs => _sortedInputs;

        public IReadOnlyList<ActionGroup> ExpandedCandidates => _expandedCandidates;

        public IReadOnlyList<ActionGroup> SelectedGroups => _selectedGroups;

        public IReadOnlyList<string> CommitEvents => _commitEvents;
    }
}
