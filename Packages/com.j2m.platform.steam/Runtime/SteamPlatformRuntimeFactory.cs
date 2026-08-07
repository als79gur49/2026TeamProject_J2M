using System;
using Game.Platform.Runtime;

namespace Game.Platform.Steam
{
    public sealed class SteamPlatformRuntimeFactory : IPlatformRuntimeFactory
    {
        private readonly Func<ISteamNativeApi> nativeApiFactory;
        private readonly bool smokeRequested;

        public SteamPlatformRuntimeFactory(Func<ISteamNativeApi> nativeApiFactory)
            : this(nativeApiFactory, smokeRequested: false)
        {
        }

        internal SteamPlatformRuntimeFactory(
            Func<ISteamNativeApi> nativeApiFactory,
            bool smokeRequested)
        {
            this.nativeApiFactory = nativeApiFactory ??
                throw new ArgumentNullException(nameof(nativeApiFactory));
            this.smokeRequested = smokeRequested;
        }

        public PlatformProviderId ProviderId => SteamPlatformRuntime.ProviderId;

        public IPlatformRuntime Create()
        {
            var nativeApi = nativeApiFactory();
            if (nativeApi == null)
            {
                throw new InvalidOperationException("Steam native API factory returned null.");
            }

            return new SteamPlatformRuntime(
                nativeApi,
                smokeRequested,
                UnityEngine.Debug.Log);
        }
    }
}
