using System;

namespace Game.Feature.Gameplay.Host
{
    public static class EnemyViewPrefabRequirements
    {
        public static EnemyAnimatorDriver GetAnimatorDriver(
            GameplayEntityView enemyViewPrefab,
            string ownerDescription)
        {
            if (enemyViewPrefab == null)
            {
                throw new ArgumentNullException(nameof(enemyViewPrefab));
            }

            if (!enemyViewPrefab.TryGetComponent<EnemyAnimatorDriver>(out var driver) ||
                driver == null)
            {
                throw new InvalidOperationException(
                    $"{ownerDescription} requires {nameof(EnemyAnimatorDriver)} on the enemy prefab root.");
            }

            return driver;
        }

        public static void ValidateEnemyViewPrefab(GameplayEntityView enemyViewPrefab, string ownerDescription)
        {
            GetAnimatorDriver(enemyViewPrefab, ownerDescription);
            EnemyAnimationTimingAuthoring.GetOptionalValidatedAuthoring(enemyViewPrefab);
            EnemyJumpMotionPresentationAuthoring.GetOptionalValidatedAuthoring(enemyViewPrefab);
            UnitLocomotionPresentationAuthoring.GetOptionalValidatedAuthoring(enemyViewPrefab);
            EntityMotionPresentationAuthoring.GetOptionalValidatedAuthoring(enemyViewPrefab);
            EntityEffectPresentationAuthoring.GetOptionalValidatedAuthoring(enemyViewPrefab);
        }

        public static void ValidateEnemyViewInstance(GameplayEntityView enemyViewInstance, string ownerDescription)
        {
            ValidateEnemyViewPrefab(enemyViewInstance, ownerDescription);
        }
    }
}
