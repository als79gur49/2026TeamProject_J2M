using System;
using System.Collections.Generic;
using Game.Platform.Runtime;

namespace Game.Platform.Steam
{
    public static class SteamPlatformRegistration
    {
        public const string SmokeArgument = "-j2mSteamSmoke";

        public static PlatformRuntimeRegistrationResult RegisterFactory(
            Func<ISteamNativeApi> nativeApiFactory)
        {
            return RegisterFactory(
                nativeApiFactory,
                IsSmokeRequested(Environment.GetCommandLineArgs()));
        }

        internal static PlatformRuntimeRegistrationResult RegisterFactory(
            Func<ISteamNativeApi> nativeApiFactory,
            bool smokeRequested)
        {
            return PlatformRuntimeRegistry.RegisterFactory(
                new SteamPlatformRuntimeFactory(nativeApiFactory, smokeRequested));
        }

        internal static bool IsSmokeRequested(IReadOnlyList<string> arguments)
        {
            return PlatformProviderSelection.HasExactOptInFlag(arguments, SmokeArgument);
        }
    }
}
