using Game.Platform.Runtime;
using UnityEngine;

namespace Game.Platform.Tests.PlayMode
{
    internal static class PlatformRuntimeTestBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void SuppressProductionBootstrap()
        {
            PlatformRuntimeBootstrap.TryConfigureTestOverride(
                suppressAutomaticBootstrap: true,
                fakeFactory: null,
                out _);
        }
    }
}
