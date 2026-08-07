using System;
using Steamworks;

namespace Game.Platform.Steam.SteamworksNet
{
    public sealed class SteamworksNetNativeApi : ISteamNativeApi
    {
        private Callback<GameOverlayActivated_t> overlayActivatedCallback;
        private Action<bool> overlayActivationObserver;

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

        public bool IsLoggedOn()
        {
            return SteamUser.BLoggedOn();
        }

        public bool IsOverlayEnabled()
        {
            return SteamUtils.IsOverlayEnabled();
        }

        public void RegisterOverlayActivationCallback(Action<bool> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            if (overlayActivatedCallback != null)
            {
                throw new InvalidOperationException(
                    "Steam overlay activation callback is already registered.");
            }

            overlayActivationObserver = observer;
            try
            {
                overlayActivatedCallback =
                    Callback<GameOverlayActivated_t>.Create(OnOverlayActivated);
            }
            catch
            {
                overlayActivationObserver = null;
                throw;
            }
        }

        public void DisposeOverlayActivationCallback()
        {
            var callback = overlayActivatedCallback;
            overlayActivatedCallback = null;
            overlayActivationObserver = null;
            callback?.Dispose();
        }

        private void OnOverlayActivated(GameOverlayActivated_t observation)
        {
            overlayActivationObserver?.Invoke(observation.m_bActive != 0);
        }
    }
}
