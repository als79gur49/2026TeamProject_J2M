using System;
using Game.Feature.Gameplay.Vfx.Authoring;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxRuntimeInstaller : MonoBehaviour
    {
        private const string MissingProductionRuntimeMessage =
            "GameplayVfxRuntimeInstaller requires a co-located GameplayVfxProductionRuntime on the canonical host root.";

        [SerializeField] private bool installOnAwake = true;
        [SerializeField] private VfxCueMapAsset hostDefaultCueMap;

        public VfxCueMapAsset HostDefaultCueMap => hostDefaultCueMap;

        private void Awake()
        {
            if (installOnAwake)
            {
                Install();
            }
        }

        public void Install()
        {
            var productionRuntime = GetComponent<GameplayVfxProductionRuntime>();
            if (productionRuntime == null)
            {
                throw new InvalidOperationException(MissingProductionRuntimeMessage);
            }

            productionRuntime.ConfigureHostDefaultMap(hostDefaultCueMap);
        }
    }
}
