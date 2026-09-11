using System;
using Game.Platform.Runtime;

namespace Game.Platform.Steam
{
    public static class SteamPlatformRegistration
    {
        public static PlatformRuntimeRegistrationResult RegisterFactory(
            Func<ISteamNativeApi> nativeApiFactory)
        {
            return PlatformRuntimeRegistry.RegisterFactory(
                new SteamPlatformRuntimeFactory(nativeApiFactory));
        }

        public static PlatformRuntimeRegistrationResult RegisterDependenciesFactory(
            Func<SteamRuntimeDependencies> dependenciesFactory)
        {
            return PlatformRuntimeRegistry.RegisterFactory(
                new SteamPlatformRuntimeFactory(dependenciesFactory));
        }
    }
}
