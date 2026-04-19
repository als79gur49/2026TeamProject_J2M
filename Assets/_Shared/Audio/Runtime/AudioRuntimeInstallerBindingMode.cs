namespace Game.Shared.Audio
{
    public enum AudioRuntimeInstallerBindingMode
    {
        // Default scene-local runtime creation. Non-canonical scenes keep this behavior in v1.
        LocalOnly = 0,
        // Explicit canonical bootstrap seam: bind to the registered persistent runtime when it
        // exists, otherwise fall back to the historical local-runtime creation path.
        PreferRegisteredPersistentRuntime = 1,
    }
}
