using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(
        menuName = "Gameplay/AI/Behaviors/Charge Module",
        fileName = "EnemyChargeBehaviorModule")]
    public sealed class EnemyChargeBehaviorModuleAsset : EnemyBehaviorModuleAsset
    {
        [SerializeField] private EnemyChargeExecutionProfile chargeExecutionProfile;

        public override EnemyBehaviorModuleKey Key => EnemyBehaviorModuleKey.Charge;

        public EnemyChargeExecutionProfile ChargeExecutionProfile => chargeExecutionProfile;

        internal override EnemyBehaviorModuleRuntime Compile(in EnemyBehaviorModuleCompileContext context)
        {
            if (chargeExecutionProfile == null)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{context.ProfileName}' charge behavior module '{name}' requires a charge execution profile.",
                    nameof(chargeExecutionProfile));
            }

            return new EnemyChargeBehaviorRuntime(chargeExecutionProfile.Compile(context.SimulationTicksPerSecond));
        }
    }
}
