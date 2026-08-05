namespace Game.Platform.Runtime
{
    public sealed class LocalPlatformRuntime : IPlatformRuntime
    {
        private bool initialized;
        private bool shutDown;

        public PlatformProviderId ProviderId => PlatformProviderId.Local;

        public PlatformAvailability Availability => PlatformAvailability.Available;

        public PlatformInitializationResult Initialize()
        {
            if (shutDown)
            {
                return PlatformInitializationResult.Failure(
                    "Local platform runtime cannot initialize after shutdown.");
            }

            if (!initialized)
            {
                initialized = true;
            }

            return PlatformInitializationResult.Success;
        }

        public void Tick()
        {
        }

        public void Shutdown()
        {
            if (shutDown)
            {
                return;
            }

            shutDown = true;
        }
    }
}
