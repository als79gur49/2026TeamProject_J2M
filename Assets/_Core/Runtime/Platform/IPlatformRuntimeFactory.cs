namespace Game.Platform.Runtime
{
    public interface IPlatformRuntimeFactory
    {
        PlatformProviderId ProviderId { get; }

        IPlatformRuntime Create();
    }
}
