using System;
using Game.Feature.Gameplay.Model.Phases;

namespace Game.Feature.Gameplay.Model.Intents
{
    public abstract class Intent
    {
        protected Intent(int sourceId, int priority, TickPhase phase)
        {
            SourceId = sourceId;
            Priority = priority;
            Phase = phase;
        }

        public int IntentId { get; private set; }

        public int SourceId { get; }

        public int Priority { get; }

        public TickPhase Phase { get; }

        protected internal abstract int GetTypeSortKey();

        protected internal virtual int CompareSameType(Intent other)
        {
            return 0;
        }

        internal void AssignIntentId(int intentId)
        {
            if (intentId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intentId), "Intent IDs must be positive.");
            }

            if (IntentId != 0)
            {
                throw new InvalidOperationException("Intent ID has already been assigned.");
            }

            IntentId = intentId;
        }
    }
}
