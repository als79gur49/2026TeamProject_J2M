using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Capabilities/Front Face Support", fileName = "EnemyFrontFaceSupportCapability")]
    public sealed class EnemyFrontFaceSupportCapabilityAsset : EnemyCapabilityAsset
    {
        [SerializeField] private EnemyFrontFaceSupportEffectAuthoring[] effects = Array.Empty<EnemyFrontFaceSupportEffectAuthoring>();

        public sealed override EnemyCapabilityFamily Family => EnemyCapabilityFamily.FrontFaceSupport;

        public IReadOnlyList<EnemyFrontFaceSupportEffectAuthoring> Effects => effects;

        internal sealed override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
        {
            var compiledEffects = new List<EnemyFrontFaceSupportEffectRuntime>();
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    throw new ArgumentException("Enemy front-face support capability contains a null effect entry.", nameof(effects));
                }

                compiledEffects.Add(effect.Compile(simulationTicksPerSecond));
            }

            return new EnemyFrontFaceSupportCapabilityRuntime(compiledEffects);
        }
    }
}
