using UnityEngine;

namespace Game.Platform.Steam.SteamworksNet
{
    internal static class SteamworksNetPlatformRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterProviderFactory()
        {
            var registration = SteamPlatformRegistration.RegisterFactory(
                () => new SteamworksNetNativeApi());
            if (!registration.IsSuccess)
            {
                Debug.LogError(
                    "Steam provider registration failed: " +
                    registration.FailureReason);
            }
        }
    }
}
