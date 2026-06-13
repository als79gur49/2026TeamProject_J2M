using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Attack.Collection
{
    public readonly struct RawAttackIntent
    {
        public RawAttackIntent(int sourceId, int priority, int targetId)
            : this(
                sourceId,
                priority,
                targetId,
                AttackCommandKind.Attack,
                AttackSourceKind.Combat,
                default,
                hasTargetCell: false,
                localSequence: 0)
        {
        }

        public RawAttackIntent(
            int sourceId,
            int priority,
            int targetId,
            AttackSourceKind sourceKind,
            int localSequence)
            : this(
                sourceId,
                priority,
                targetId,
                AttackCommandKind.Attack,
                sourceKind,
                default,
                hasTargetCell: false,
                localSequence)
        {
        }

        private RawAttackIntent(
            int sourceId,
            int priority,
            int targetId,
            AttackCommandKind commandKind,
            AttackSourceKind sourceKind,
            Vector2Int targetCell,
            bool hasTargetCell,
            int localSequence)
        {
            ValidateContract(targetId, commandKind, hasTargetCell);

            SourceId = sourceId;
            Priority = priority;
            TargetId = targetId;
            CommandKind = commandKind;
            SourceKind = sourceKind;
            TargetCell = targetCell;
            HasTargetCell = hasTargetCell;
            LocalSequence = localSequence;
        }

        public int SourceId { get; }

        public int Priority { get; }

        public int TargetId { get; }

        public AttackCommandKind CommandKind { get; }

        public AttackSourceKind SourceKind { get; }

        public Vector2Int TargetCell { get; }

        public bool HasTargetCell { get; }

        public int LocalSequence { get; }

        public static RawAttackIntent CreateFireProjectile(int sourceId, int priority)
        {
            throw new NotSupportedException(
                "FireProjectile is a retired legacy EntityType.Projectile path. Use PendingCellImpact-based forward-cell projectile runtime instead.");
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
                    throw new NotSupportedException(
                        "FireProjectile is a retired legacy EntityType.Projectile path. Use PendingCellImpact-based forward-cell projectile runtime instead.");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(commandKind),
                        commandKind,
                        "Raw attack intents only support entity-generated Attack and FireProjectile commands.");
            }
        }
    }
}
