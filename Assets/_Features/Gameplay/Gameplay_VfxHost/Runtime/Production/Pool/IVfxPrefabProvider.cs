using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public interface IVfxPrefabProvider
    {
        bool TryResolvePrefab(in ResolvedVfxPlaybackCommand command, out GameObject prefab);
    }
}
