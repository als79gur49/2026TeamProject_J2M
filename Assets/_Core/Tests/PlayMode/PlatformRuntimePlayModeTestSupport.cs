using Game.Platform.Runtime;

namespace Game.Platform.Tests.PlayMode
{
    internal sealed class CountingPlatformRuntime : IPlatformRuntime
    {
        internal CountingPlatformRuntime(string providerId)
        {
            ProviderId = new PlatformProviderId(providerId);
        }

        public PlatformProviderId ProviderId { get; }

        public PlatformAvailability Availability => AvailabilityResult;

        internal PlatformAvailability AvailabilityResult { get; set; } =
            PlatformAvailability.Available;

        internal PlatformInitializationResult InitializationResult { get; set; } =
            PlatformInitializationResult.Success;

        internal int InitializeCount { get; private set; }

        internal int TickCount { get; private set; }

        internal int ShutdownCount { get; private set; }

        public PlatformInitializationResult Initialize()
        {
            InitializeCount++;
            return InitializationResult;
        }

        public void Tick()
        {
            TickCount++;
        }

        public void Shutdown()
        {
            ShutdownCount++;
        }
    }

    internal sealed class CountingPlatformRuntimeFactory : IPlatformRuntimeFactory
    {
        private readonly CountingPlatformRuntime runtime;

        internal CountingPlatformRuntimeFactory(CountingPlatformRuntime runtime)
        {
            this.runtime = runtime;
        }

        public PlatformProviderId ProviderId => runtime.ProviderId;

        internal int CreateCount { get; private set; }

        public IPlatformRuntime Create()
        {
            CreateCount++;
            return runtime;
        }
    }
}
