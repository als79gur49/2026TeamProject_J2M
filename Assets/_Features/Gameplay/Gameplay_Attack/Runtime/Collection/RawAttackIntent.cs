using System;

namespace Game.Feature.Gameplay.Attack.Collection
{
    public readonly struct RawAttackIntent
    {
        public RawAttackIntent(int sourceId, int priority, int targetId)
            : this(sourceId, priority, targetId, AttackCommandKind.Attack)
        {
        }

        private RawAttackIntent(int sourceId, int priority, int targetId, AttackCommandKind commandKind)
        {
            ValidateContract(targetId, commandKind);

            SourceId = sourceId;
            Priority = priority;
            TargetId = targetId;
            CommandKind = commandKind;
        }

        public int SourceId { get; }

        public int Priority { get; }

        public int TargetId { get; }

        public AttackCommandKind CommandKind { get; }

        public static RawAttackIntent CreateFireProjectile(int sourceId, int priority)
        {
            return new RawAttackIntent(sourceId, priority, 0, AttackCommandKind.FireProjectile);
        }

        private static void ValidateContract(int targetId, AttackCommandKind commandKind)
        {
            switch (commandKind)
            {
                case AttackCommandKind.Attack:
                    if (targetId <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(targetId), "Attack intents require a positive target ID.");
                    }

                    return;

                case AttackCommandKind.FireProjectile:
                    if (targetId != 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(targetId), "FireProjectile intents must not carry a target ID.");
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
