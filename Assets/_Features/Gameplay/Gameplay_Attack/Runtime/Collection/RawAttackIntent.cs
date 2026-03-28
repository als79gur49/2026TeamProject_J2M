using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Attack.Collection
{
    public readonly struct RawAttackIntent
    {
        public RawAttackIntent(int sourceId, int priority, int targetId)
            : this(sourceId, priority, targetId, AttackCommandKind.Attack, default, hasTargetCell: false, localSequence: 0)
        {
        }

        private RawAttackIntent(
            int sourceId,
            int priority,
            int targetId,
            AttackCommandKind commandKind,
            Vector2Int targetCell,
            bool hasTargetCell,
            int localSequence)
        {
            ValidateContract(targetId, commandKind, hasTargetCell);

            SourceId = sourceId;
            Priority = priority;
            TargetId = targetId;
            CommandKind = commandKind;
            TargetCell = targetCell;
            HasTargetCell = hasTargetCell;
            LocalSequence = localSequence;
        }

        public int SourceId { get; }

        public int Priority { get; }

        public int TargetId { get; }

        public AttackCommandKind CommandKind { get; }

        public Vector2Int TargetCell { get; }

        public bool HasTargetCell { get; }

        public int LocalSequence { get; }

        public static RawAttackIntent CreateFireProjectile(int sourceId, int priority)
        {
            return new RawAttackIntent(
                sourceId,
                priority,
                0,
                AttackCommandKind.FireProjectile,
                default,
                hasTargetCell: false,
                localSequence: 0);
        }

        private static void ValidateContract(int targetId, AttackCommandKind commandKind, bool hasTargetCell)
        {
            switch (commandKind)
            {
                case AttackCommandKind.Attack:
                    if (targetId <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(targetId), "Attack intents require a positive target ID.");
                    }

                    if (hasTargetCell)
                    {
                        throw new ArgumentException("Attack intents must not carry a target cell.", nameof(hasTargetCell));
                    }

                    return;

                case AttackCommandKind.FireProjectile:
                    if (targetId != 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(targetId), "FireProjectile intents must not carry a target ID.");
                    }

                    if (hasTargetCell)
                    {
                        throw new ArgumentException("FireProjectile intents must not carry a target cell.", nameof(hasTargetCell));
                    }

                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(commandKind),
                        commandKind,
                        "Raw attack intents only support entity-generated Attack and FireProjectile commands.");
            }
        }
    }
}
