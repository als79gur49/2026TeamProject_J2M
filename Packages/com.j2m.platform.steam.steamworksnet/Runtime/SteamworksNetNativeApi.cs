using Steamworks;

namespace Game.Platform.Steam.SteamworksNet
{
    public sealed class SteamworksNetNativeApi : ISteamNativeApi
    {
        public bool IsPacksizeCompatible()
        {
            return Packsize.Test();
        }

        public SteamDllCheckObservation ObserveDllCheck()
        {
            var returnedValue = DllCheck.Test();
            return SteamDllCheckObservation.UpstreamDisabled(returnedValue);
        }

        public bool Initialize()
        {
            return SteamAPI.Init();
        }

        public void RunCallbacks()
        {
            SteamAPI.RunCallbacks();
        }

        public void Shutdown()
        {
            SteamAPI.Shutdown();
        }

        public uint GetAppId()
        {
            return SteamUtils.GetAppID().m_AppId;
        }

        public bool IsSteamIdValid()
        {
            return SteamUser.GetSteamID().IsValid();
        }

        public bool IsOverlayEnabled()
        {
            return SteamUtils.IsOverlayEnabled();
        }
    }
}
