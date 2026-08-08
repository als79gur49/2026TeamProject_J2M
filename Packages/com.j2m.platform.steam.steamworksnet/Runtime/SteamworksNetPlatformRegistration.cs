using UnityEngine;

namespace Game.Platform.Steam.SteamworksNet
{
    internal static class SteamworksNetPlatformRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterProviderFactory()
        {
            var registration = SteamPlatformRegistration.RegisterDependenciesFactory(
                CreateRuntimeDependencies);
            if (!registration.IsSuccess)
            {
                Debug.LogError(
                    "Steam provider registration failed: " +
                    registration.FailureReason);
            }
        }

        internal static SteamRuntimeDependencies CreateRuntimeDependencies()
        {
            var adapter = new SteamworksNetNativeApi();
            return new SteamRuntimeDependencies(adapter, adapter);
        }
    }
}
