using System;
using Game.Platform.Runtime;

namespace Game.Platform.Steam
{
    public sealed class SteamPlatformRuntimeFactory : IPlatformRuntimeFactory
    {
        private readonly Func<ISteamNativeApi> nativeApiFactory;
        private readonly Func<SteamRuntimeDependencies> dependenciesFactory;

        public SteamPlatformRuntimeFactory(Func<ISteamNativeApi> nativeApiFactory)
        {
            this.nativeApiFactory = nativeApiFactory ??
                throw new ArgumentNullException(nameof(nativeApiFactory));
        }

        internal SteamPlatformRuntimeFactory(Func<SteamRuntimeDependencies> dependenciesFactory)
        {
            this.dependenciesFactory = dependenciesFactory ??
                throw new ArgumentNullException(nameof(dependenciesFactory));
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

            return new SteamPlatformRuntime(dependencies, monotonicSeconds: null);
        }
    }
}
