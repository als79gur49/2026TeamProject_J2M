using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(
        menuName = "Gameplay/AI/Behaviors/Charge Execution Profile",
        fileName = "EnemyChargeExecutionProfile")]
    public sealed class EnemyChargeExecutionProfile : ScriptableObject
    {
        [SerializeField] private EnemyChargeTimingAuthoringSettings timing = new(0.4f, 0.2f, 0.4f);

        public EnemyChargeTimingAuthoringSettings Timing => timing;

        internal EnemyChargeTimingSettings Compile(int simulationTicksPerSecond)
        {
            return timing.ToRuntimeSettings(simulationTicksPerSecond);
        }
    }
}
