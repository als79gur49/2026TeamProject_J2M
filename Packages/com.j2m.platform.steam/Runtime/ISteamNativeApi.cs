namespace Game.Platform.Steam
{
    public interface ISteamNativeApi
    {
        bool IsPacksizeCompatible();

        bool Initialize();

        void RunCallbacks();

        void Shutdown();

        uint GetAppId();

        bool IsSteamIdValid();

        bool IsLoggedOn();
    }
}
