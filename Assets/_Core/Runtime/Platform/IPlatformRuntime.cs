namespace Game.Platform.Runtime
{
    public interface IPlatformRuntime
    {
        PlatformProviderId ProviderId { get; }

        PlatformAvailability Availability { get; }

        PlatformInitializationResult Initialize();

        void Tick();

        void Shutdown();
    }
}
