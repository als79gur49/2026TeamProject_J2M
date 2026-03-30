using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal static class SessionStartEntityNormalizer
    {
        public static List<EntityState> Normalize(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            var normalizedEntities = new List<EntityState>();

            foreach (var entity in initialEntities)
            {
                var normalizedEntity = entity;

                if (normalizedEntity.type == EntityType.Projectile &&
                    normalizedEntity.spawnTick == 0 &&
                    normalizedEntity.stateTimer == 0 &&
                    normalizedEntity.hp > 0 &&
                    !normalizedEntity.markedForDeath)
                {
                    normalizedEntity.stateTimer = timingProfile.ProjectileStepIntervalTicks;
                }

                normalizedEntities.Add(normalizedEntity);
            }

            return normalizedEntities;
        }
    }
}
