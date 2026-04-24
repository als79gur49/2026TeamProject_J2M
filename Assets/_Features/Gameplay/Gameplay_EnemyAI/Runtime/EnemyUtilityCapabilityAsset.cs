using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Capabilities/Utility/Timed Effects", fileName = "EnemyUtilityCapability")]
    public sealed class EnemyUtilityCapabilityAsset : EnemyCapabilityAsset
    {
        [SerializeField] private EnemyUtilityEffectAuthoring[] effects = Array.Empty<EnemyUtilityEffectAuthoring>();

        public sealed override EnemyCapabilityFamily Family => EnemyCapabilityFamily.Utility;

        public IReadOnlyList<EnemyUtilityEffectAuthoring> Effects => effects;

        internal sealed override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
        {
            var compiledEffects = new List<EnemyUtilityEffectRuntime>();
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    throw new ArgumentException("Enemy utility capability contains a null effect entry.", nameof(effects));
                }

                compiledEffects.Add(effect.Compile(simulationTicksPerSecond));
            }

            return new EnemyUtilityCapabilityRuntime(compiledEffects);
        }
    }
}
