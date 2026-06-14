using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public interface IGameplayVfxTimeProvider
    {
        float TimeSeconds { get; }
    }

    public sealed class UnityGameplayVfxTimeProvider : IGameplayVfxTimeProvider
    {
        public float TimeSeconds => Time.time;
    }
}
