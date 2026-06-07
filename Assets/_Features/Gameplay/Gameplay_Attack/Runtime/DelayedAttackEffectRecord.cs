using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Attack
{
    internal readonly struct DelayedAttackEffectRecord
    {
        private readonly int _sourceActionPlanId;

        public DelayedAttackEffectRecord(
            int sourceId,
            int targetId,
            int damage,
            int priority,
            int tickGenerated,
            int executeAtTick,
            int sourceActionPlanId,
            int effectSequence)
        {
            if (sourceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Delayed attack effects require a positive source ID.");
            }

            if (targetId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetId), "Delayed attack effects require a positive target ID.");
            }

            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Delayed attack effects require positive damage.");
            }

            if (tickGenerated < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickGenerated), "Generated tick cannot be negative.");
            }

            if (executeAtTick <= tickGenerated)
            {
                throw new ArgumentOutOfRangeException(nameof(executeAtTick), "Delayed attack effects must execute on a later tick.");
            }

            if (sourceActionPlanId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceActionPlanId), "Delayed attack effects require a positive source action plan ID.");
            }

            if (effectSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(effectSequence), "Delayed attack effects require a positive sequence.");
            }

            SourceId = sourceId;
            TargetId = targetId;
            Damage = damage;
            Priority = priority;
            TickGenerated = tickGenerated;
            ExecuteAtTick = executeAtTick;
            _sourceActionPlanId = sourceActionPlanId;
            EffectSequence = effectSequence;
        }

        public int SourceId { get; }

        public int TargetId { get; }

        public int Damage { get; }

        public int Priority { get; }

        public int TickGenerated { get; }

        public int ExecuteAtTick { get; }

        public int SourceActionPlanId => _sourceActionPlanId;

        public int EffectSequence { get; }
    }

    internal sealed class DelayedAttackEffectRecordComparer : IComparer<DelayedAttackEffectRecord>
    {
        internal static readonly DelayedAttackEffectRecordComparer Instance = new();

        public int Compare(DelayedAttackEffectRecord left, DelayedAttackEffectRecord right)
        {
            var result = left.ExecuteAtTick.CompareTo(right.ExecuteAtTick);
            if (result != 0)
            {
                return result;
            }

            result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            result = left.SourceActionPlanId.CompareTo(right.SourceActionPlanId);
            if (result != 0)
            {
                return result;
            }

            result = left.EffectSequence.CompareTo(right.EffectSequence);
            if (result != 0)
            {
                return result;
            }

            result = left.TargetId.CompareTo(right.TargetId);
            if (result != 0)
            {
                return result;
            }

            result = left.Damage.CompareTo(right.Damage);
            if (result != 0)
            {
                return result;
            }

            result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            return left.TickGenerated.CompareTo(right.TickGenerated);
        }
    }
}
