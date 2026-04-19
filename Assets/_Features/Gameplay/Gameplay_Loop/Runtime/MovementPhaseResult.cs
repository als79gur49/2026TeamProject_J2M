using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class MovementPhaseResult
    {
        public static readonly MovementPhaseResult Empty = new(
            Array.Empty<RawMovementIntent>(),
            Array.Empty<MoveIntent>(),
            Array.Empty<ResolutionRecord>(),
            Array.Empty<ImpactDispositionResolutionRecord>(),
            Array.Empty<FinalizationOperation>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        private readonly ReadOnlyCollection<string> _commitEvents;
        private readonly ReadOnlyCollection<ImpactDispositionResolutionRecord> _impactDispositionRecords;
        private readonly ReadOnlyCollection<RawMovementIntent> _rawIntents;
        private readonly ReadOnlyCollection<string> _rejectedReasons;
        private readonly ReadOnlyCollection<ResolutionRecord> _resolutionRecords;
        private readonly ReadOnlyCollection<FinalizationOperation> _resolvedOperations;
        private readonly ReadOnlyCollection<MoveIntent> _sortedIntents;

        public MovementPhaseResult(
            IEnumerable<RawMovementIntent> rawIntents,
            IEnumerable<MoveIntent> sortedIntents,
            IEnumerable<ResolutionRecord> resolutionRecords,
            IEnumerable<ImpactDispositionResolutionRecord> impactDispositionRecords,
            IEnumerable<FinalizationOperation> resolvedOperations,
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

            if (resolutionRecords == null)
            {
                throw new ArgumentNullException(nameof(resolutionRecords));
            }

            if (resolvedOperations == null)
            {
                throw new ArgumentNullException(nameof(resolvedOperations));
            }

            if (impactDispositionRecords == null)
            {
                throw new ArgumentNullException(nameof(impactDispositionRecords));
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
            _resolutionRecords = new ReadOnlyCollection<ResolutionRecord>(new List<ResolutionRecord>(resolutionRecords));
            _impactDispositionRecords = new ReadOnlyCollection<ImpactDispositionResolutionRecord>(
                new List<ImpactDispositionResolutionRecord>(impactDispositionRecords));
            _resolvedOperations = new ReadOnlyCollection<FinalizationOperation>(new List<FinalizationOperation>(resolvedOperations));
            _commitEvents = new ReadOnlyCollection<string>(new List<string>(commitEvents));
            _rejectedReasons = new ReadOnlyCollection<string>(new List<string>(rejectedReasons));
        }

        public IReadOnlyList<RawMovementIntent> RawIntents => _rawIntents;

        public IReadOnlyList<MoveIntent> SortedIntents => _sortedIntents;

        public IReadOnlyList<ResolutionRecord> ResolutionRecords => _resolutionRecords;

        internal IReadOnlyList<ImpactDispositionResolutionRecord> ImpactDispositionRecords => _impactDispositionRecords;

        // Includes accepted impact follow-through movement-visible writes such as
        // local vacate, box move, and contingent facing/state updates.
        public IReadOnlyList<FinalizationOperation> ResolvedOperations => _resolvedOperations;

        public IReadOnlyList<string> CommitEvents => _commitEvents;

        public IReadOnlyList<string> RejectedReasons => _rejectedReasons;
    }
}
