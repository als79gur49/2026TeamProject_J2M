using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public interface IEnemyPresentationParticleEffectOwner
    {
        void CollectManagedParticleSystems(ICollection<ParticleSystem> particleSystems);
    }
}
