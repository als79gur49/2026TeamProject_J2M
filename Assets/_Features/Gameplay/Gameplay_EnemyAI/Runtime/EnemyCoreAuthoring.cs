using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy Core Authoring", fileName = "EnemyCoreAuthoring")]
    public sealed class EnemyCoreAuthoring : ScriptableObject
    {
        [SerializeField] private EnemyAiCommonAuthoringSettings commonSettings = new(50, 50, 1f / 6f);
        [SerializeField] private EnemyLocomotionTimingAuthoringSettings locomotionTimingSettings = new(0f);
        [SerializeField] private EnemyChargeTimingAuthoringSettings chargeTimingSettings = new(0f, 0f);

        public EnemyAiCommonAuthoringSettings CommonSettings => commonSettings;

        public EnemyLocomotionTimingAuthoringSettings LocomotionTimingSettings => locomotionTimingSettings;

        public EnemyChargeTimingAuthoringSettings ChargeTimingSettings => chargeTimingSettings;

        internal EnemyCoreRuntime Compile(int simulationTicksPerSecond)
        {
            return new EnemyCoreRuntime(
                commonSettings.ToRuntimeSettings(simulationTicksPerSecond),
                locomotionTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
                chargeTimingSettings.ToRuntimeSettings(simulationTicksPerSecond));
        }
    }
}
