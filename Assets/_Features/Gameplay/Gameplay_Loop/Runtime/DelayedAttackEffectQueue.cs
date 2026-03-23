using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;

namespace Game.Feature.Gameplay.Loop
{
    internal interface IDelayedAttackEffectSink
    {
        void Enqueue(DelayedAttackEffectRecord effectRecord);
    }

    internal sealed class DelayedAttackEffectQueue : IDelayedAttackEffectSink
    {
        private readonly List<DelayedAttackEffectRecord> _pendingEffects = new();

        public void Enqueue(DelayedAttackEffectRecord effectRecord)
        {
            _pendingEffects.Add(effectRecord);
        }

        public List<DelayedAttackEffectRecord> Drain(int tickIndex)
        {
            if (_pendingEffects.Count == 0)
            {
                return new List<DelayedAttackEffectRecord>();
            }

            var drainedEffects = new List<DelayedAttackEffectRecord>();

            for (var i = _pendingEffects.Count - 1; i >= 0; i--)
            {
                var effectRecord = _pendingEffects[i];
                if (effectRecord.ExecuteAtTick != tickIndex)
                {
                    continue;
                }

                drainedEffects.Add(effectRecord);
                _pendingEffects.RemoveAt(i);
            }

            drainedEffects.Sort(DelayedAttackEffectRecordComparer.Instance);
            return drainedEffects;
        }

        public List<DelayedAttackEffectRecord> Snapshot()
        {
            if (_pendingEffects.Count == 0)
            {
                return new List<DelayedAttackEffectRecord>();
            }

            var snapshot = new List<DelayedAttackEffectRecord>(_pendingEffects);
            snapshot.Sort(DelayedAttackEffectRecordComparer.Instance);
            return snapshot;
        }
    }
}
