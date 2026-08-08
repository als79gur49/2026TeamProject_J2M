using System;
using System.Collections.Generic;
using Game.Platform.Runtime;

namespace Game.Platform.Steam
{
    public static class SteamPlatformRegistration
    {
        public const string SmokeArgument = "-j2mSteamSmoke";
        public const string AchievementSmokeArgument = "-j2mSteamAchievementSmoke";

        public static PlatformRuntimeRegistrationResult RegisterFactory(
            Func<ISteamNativeApi> nativeApiFactory)
        {
            return RegisterFactory(
                nativeApiFactory,
                IsSmokeRequested(Environment.GetCommandLineArgs()),
                IsAchievementSmokeRequested(Environment.GetCommandLineArgs()));
        }

        public static PlatformRuntimeRegistrationResult RegisterDependenciesFactory(
            Func<SteamRuntimeDependencies> dependenciesFactory)
        {
            var arguments = Environment.GetCommandLineArgs();
            return RegisterDependenciesFactory(
                dependenciesFactory,
                IsSmokeRequested(arguments),
                IsAchievementSmokeRequested(arguments));
        }

        internal static PlatformRuntimeRegistrationResult RegisterFactory(
            Func<ISteamNativeApi> nativeApiFactory,
            bool smokeRequested)
        {
            return RegisterFactory(
                nativeApiFactory,
                smokeRequested,
                achievementSmokeRequested: false);
        }

        internal static PlatformRuntimeRegistrationResult RegisterFactory(
            Func<ISteamNativeApi> nativeApiFactory,
            bool smokeRequested,
            bool achievementSmokeRequested)
        {
            return PlatformRuntimeRegistry.RegisterFactory(
                new SteamPlatformRuntimeFactory(
                    nativeApiFactory,
                    smokeRequested,
                    achievementSmokeRequested));
        }

        internal static PlatformRuntimeRegistrationResult RegisterDependenciesFactory(
            Func<SteamRuntimeDependencies> dependenciesFactory,
            bool smokeRequested,
            bool achievementSmokeRequested)
        {
            return PlatformRuntimeRegistry.RegisterFactory(
                new SteamPlatformRuntimeFactory(
                    dependenciesFactory,
                    smokeRequested,
                    achievementSmokeRequested));
        }

        internal static bool IsSmokeRequested(IReadOnlyList<string> arguments)
        {
            return PlatformProviderSelection.HasExactOptInFlag(arguments, SmokeArgument);
        }

        internal static bool IsAchievementSmokeRequested(
            IReadOnlyList<string> arguments)
        {
            return PlatformProviderSelection.HasExactOptInFlag(
                arguments,
                AchievementSmokeArgument);
        }
    }
}
