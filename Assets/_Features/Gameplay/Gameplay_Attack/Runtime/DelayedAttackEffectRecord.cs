using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Attack
{
    internal readonly struct DelayedAttackEffectRecord
    {
        public DelayedAttackEffectRecord(
            int sourceId,
            int targetId,
            int damage,
            int priority,
            int tickGenerated,
            int executeAtTick,
            int sourceActionGroupId,
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

            if (sourceActionGroupId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceActionGroupId), "Delayed attack effects require a positive source group ID.");
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
            SourceActionGroupId = sourceActionGroupId;
            EffectSequence = effectSequence;
        }

        public int SourceId { get; }

        public int TargetId { get; }

        public int Damage { get; }

        public int Priority { get; }

        public int TickGenerated { get; }

        public int ExecuteAtTick { get; }

        public int SourceActionGroupId { get; }

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

            result = left.SourceActionGroupId.CompareTo(right.SourceActionGroupId);
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
