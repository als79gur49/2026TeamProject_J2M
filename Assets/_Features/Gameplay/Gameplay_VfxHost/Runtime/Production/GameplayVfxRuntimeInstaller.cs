using System;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx.Authoring;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxRuntimeInstaller : MonoBehaviour, IGameplayBootstrapInstaller, IGameplayBootstrapReadiness
    {
        private const string MissingProductionRuntimeMessage =
            "GameplayVfxRuntimeInstaller requires a co-located GameplayVfxProductionRuntime on the canonical host root.";

        [SerializeField] private bool installOnAwake = true;
        [SerializeField] private VfxCueMapAsset hostDefaultCueMap;

        private GameplayVfxProductionRuntime productionRuntime;

        public VfxCueMapAsset HostDefaultCueMap => hostDefaultCueMap;

        public GameplayVfxProductionRuntime ProductionRuntime => ResolveProductionRuntimeOrNull();

        public bool IsReady => ResolveProductionRuntimeOrNull()?.IsHostDefaultMapConfigured == true;

        private void Awake()
        {
            if (installOnAwake)
            {
                Install();
            }
        }

        public void Install()
        {
            var productionRuntime = ResolveProductionRuntimeOrNull();
            if (productionRuntime == null)
            {
                throw new InvalidOperationException(MissingProductionRuntimeMessage);
            }

            productionRuntime.ConfigureHostDefaultMap(hostDefaultCueMap);
        }

        public string DescribeReadiness()
        {
            var runtime = ResolveProductionRuntimeOrNull();
            if (runtime == null)
            {
                return MissingProductionRuntimeMessage;
            }

            if (hostDefaultCueMap == null)
            {
                return "GameplayVfxRuntimeInstaller requires a host default cue map before gameplay host initialization.";
            }

            return runtime.IsHostDefaultMapConfigured
                ? "GameplayVfxRuntimeInstaller is ready."
                : "GameplayVfxRuntimeInstaller did not configure the host default cue map before gameplay host initialization.";
        }

        private GameplayVfxProductionRuntime ResolveProductionRuntimeOrNull()
        {
            if (productionRuntime != null)
            {
                return productionRuntime;
            }

            productionRuntime = GetComponent<GameplayVfxProductionRuntime>();
            return productionRuntime;
        }
    }
}
