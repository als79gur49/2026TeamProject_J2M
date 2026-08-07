using System;

namespace Game.Platform.Steam
{
    public interface ISteamNativeApi
    {
        bool IsPacksizeCompatible();

        SteamDllCheckObservation ObserveDllCheck();

        bool Initialize();

        void RunCallbacks();

        void Shutdown();

        uint GetAppId();

        bool IsSteamIdValid();

        bool IsLoggedOn();

        bool IsOverlayEnabled();

        void RegisterOverlayActivationCallback(Action<bool> observer);

        void DisposeOverlayActivationCallback();
    }
}
