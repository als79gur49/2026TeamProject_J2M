using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy Brain Authoring", fileName = "EnemyBrainAuthoring")]
    public sealed class EnemyBrainAuthoring : ScriptableObject
    {
        [SerializeField] private EnemyStateResolverAsset stateResolver;
        [SerializeField] private EnemyPatrolStrategyAsset patrolStrategy;
        [SerializeField] private EnemyDetectionStrategyAsset detectionStrategy;
        [SerializeField] private EnemyChaseStrategyAsset chaseStrategy;

        public EnemyStateResolverAsset StateResolver => stateResolver;

        public EnemyPatrolStrategyAsset PatrolStrategy => patrolStrategy;

        public EnemyDetectionStrategyAsset DetectionStrategy => detectionStrategy;

        public EnemyChaseStrategyAsset ChaseStrategy => chaseStrategy;

        internal EnemyBrainRuntime Compile()
        {
            return new EnemyBrainRuntime(
                (stateResolver ?? throw new ArgumentException("Enemy brain authoring requires a state resolver asset.", nameof(stateResolver))).Compile(),
                (patrolStrategy ?? throw new ArgumentException("Enemy brain authoring requires a patrol strategy asset.", nameof(patrolStrategy))).Compile(),
                (detectionStrategy ?? throw new ArgumentException("Enemy brain authoring requires a detection strategy asset.", nameof(detectionStrategy))).Compile(),
                (chaseStrategy ?? throw new ArgumentException("Enemy brain authoring requires a chase strategy asset.", nameof(chaseStrategy))).Compile());
        }
    }
}
