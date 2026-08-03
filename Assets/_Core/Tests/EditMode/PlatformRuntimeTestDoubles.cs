using System;
using Game.Platform.Runtime;

namespace Game.Platform.Tests.EditMode
{
    internal sealed class FakePlatformRuntime : IPlatformRuntime
    {
        internal FakePlatformRuntime(string providerId)
        {
            ProviderId = new PlatformProviderId(providerId);
        }

        public PlatformProviderId ProviderId { get; set; }

        public PlatformAvailability Availability
        {
            get
            {
                if (ThrowOnAvailability)
                {
                    throw new InvalidOperationException("availability failure");
                }

                return AvailabilityResult;
            }
        }

        internal PlatformAvailability AvailabilityResult { get; set; } =
            PlatformAvailability.Available;

        internal PlatformInitializationResult InitializationResult { get; set; } =
            PlatformInitializationResult.Success;

        internal bool ThrowOnAvailability { get; set; }

        internal bool ThrowOnInitialize { get; set; }

        internal bool ThrowOnTick { get; set; }

        internal bool ThrowOnShutdown { get; set; }

        internal bool ResourceAcquired { get; private set; }

        internal int InitializeCount { get; private set; }

        internal int TickCount { get; private set; }

        internal int ShutdownCount { get; private set; }

        public PlatformInitializationResult Initialize()
        {
            InitializeCount++;
            ResourceAcquired = true;
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("initialize failure");
            }

            return InitializationResult;
        }

        public void Tick()
        {
            TickCount++;
            if (ThrowOnTick)
            {
                throw new InvalidOperationException("tick failure");
            }
        }

        public void Shutdown()
        {
            ShutdownCount++;
            ResourceAcquired = false;
            if (ThrowOnShutdown)
            {
                throw new InvalidOperationException("shutdown failure");
            }
        }
    }

    internal sealed class FakePlatformRuntimeFactory : IPlatformRuntimeFactory
    {
        private readonly Func<IPlatformRuntime> create;

        internal FakePlatformRuntimeFactory(string providerId, Func<IPlatformRuntime> create)
        {
            ProviderId = new PlatformProviderId(providerId);
            this.create = create;
        }

        public PlatformProviderId ProviderId { get; }

        internal int CreateCount { get; private set; }

        public IPlatformRuntime Create()
        {
            CreateCount++;
            return create();
        }
    }
}
