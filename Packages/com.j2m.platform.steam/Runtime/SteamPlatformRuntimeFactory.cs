using System;
using Game.Platform.Runtime;

namespace Game.Platform.Steam
{
    public sealed class SteamPlatformRuntimeFactory : IPlatformRuntimeFactory
    {
        private readonly Func<ISteamNativeApi> nativeApiFactory;
        private readonly Func<SteamRuntimeDependencies> dependenciesFactory;
        private readonly bool smokeRequested;
        private readonly bool achievementSmokeRequested;

        public SteamPlatformRuntimeFactory(Func<ISteamNativeApi> nativeApiFactory)
            : this(
                nativeApiFactory,
                smokeRequested: false,
                achievementSmokeRequested: false)
        {
        }

        internal SteamPlatformRuntimeFactory(
            Func<ISteamNativeApi> nativeApiFactory,
            bool smokeRequested)
            : this(nativeApiFactory, smokeRequested, achievementSmokeRequested: false)
        {
        }

        internal SteamPlatformRuntimeFactory(
            Func<ISteamNativeApi> nativeApiFactory,
            bool smokeRequested,
            bool achievementSmokeRequested)
        {
            this.nativeApiFactory = nativeApiFactory ??
                throw new ArgumentNullException(nameof(nativeApiFactory));
            this.smokeRequested = smokeRequested;
            this.achievementSmokeRequested = achievementSmokeRequested;
        }

        internal SteamPlatformRuntimeFactory(
            Func<SteamRuntimeDependencies> dependenciesFactory,
            bool smokeRequested,
            bool achievementSmokeRequested)
        {
            this.dependenciesFactory = dependenciesFactory ??
                throw new ArgumentNullException(nameof(dependenciesFactory));
            this.smokeRequested = smokeRequested;
            this.achievementSmokeRequested = achievementSmokeRequested;
        }

        public PlatformProviderId ProviderId => SteamPlatformRuntime.ProviderId;

        public IPlatformRuntime Create()
        {
            SteamRuntimeDependencies dependencies;
            if (dependenciesFactory != null)
            {
                dependencies = dependenciesFactory();
                if (dependencies == null)
                {
                    throw new InvalidOperationException(
                        "Steam runtime dependencies factory returned null.");
                }
            }
            else
            {
                var nativeApi = nativeApiFactory();
                if (nativeApi == null)
                {
                    throw new InvalidOperationException(
                        "Steam native API factory returned null.");
                }

                dependencies = new SteamRuntimeDependencies(nativeApi, achievements: null);
            }

            return new SteamPlatformRuntime(
                dependencies,
                smokeRequested,
                achievementSmokeRequested,
                monotonicSeconds: null,
                UnityEngine.Debug.Log);
        }
    }
}
