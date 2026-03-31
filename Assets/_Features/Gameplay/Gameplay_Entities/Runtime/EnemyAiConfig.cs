using System;

namespace Game.Feature.Gameplay.Entities
{
    public readonly struct EnemyAiConfig
    {
        public EnemyAiConfig(
            int senseRange,
            int attackRange,
            int movementPriority,
            int attackPriority,
            int recoverTicks)
        {
            if (senseRange <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(senseRange), "Enemy AI sense range must be positive.");
            }

            if (attackRange <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackRange), "Enemy AI attack range must be positive.");
            }

            if (recoverTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(recoverTicks), "Enemy AI recover ticks cannot be negative.");
            }

            SenseRange = senseRange;
            AttackRange = attackRange;
            MovementPriority = movementPriority;
            AttackPriority = attackPriority;
            RecoverTicks = recoverTicks;
        }

        public int SenseRange { get; }

        public int AttackRange { get; }

        public int MovementPriority { get; }

        public int AttackPriority { get; }

        public int RecoverTicks { get; }

        public static EnemyAiConfig CreateDefaultMelee()
        {
            return new EnemyAiConfig(
                senseRange: 8,
                attackRange: 1,
                movementPriority: 50,
                attackPriority: 50,
                recoverTicks: 1);
        }
    }
}
