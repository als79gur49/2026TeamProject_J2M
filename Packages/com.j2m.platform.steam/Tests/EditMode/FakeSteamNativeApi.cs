using System;

namespace Game.Platform.Steam.Tests.EditMode
{
    internal sealed class FakeSteamNativeApi : ISteamNativeApi
    {
        internal bool PacksizeCompatible { get; set; } = true;
        internal SteamDllCheckObservation DllCheckObservation { get; set; } =
            SteamDllCheckObservation.UpstreamDisabled(returnedValue: true);
        internal bool InitializeResult { get; set; } = true;
        internal uint AppId { get; set; } = 480;
        internal bool SteamIdValid { get; set; } = true;
        internal bool LoggedOn { get; set; } = true;
        internal bool OverlayEnabled { get; set; }
        internal Exception PacksizeException { get; set; }
        internal Exception InitializeException { get; set; }
        internal Exception CallbackException { get; set; }
        internal Exception ShutdownException { get; set; }

        internal int PacksizeCount { get; private set; }
        internal int DllCheckCount { get; private set; }
        internal int InitializeCount { get; private set; }
        internal int CallbackCount { get; private set; }
        internal int ShutdownCount { get; private set; }
        internal int AppIdCount { get; private set; }
        internal int IdentityCount { get; private set; }
        internal int LoggedOnCount { get; private set; }
        internal int OverlayCallbackRegistrationCount { get; private set; }
        internal int OverlayCallbackDisposeCount { get; private set; }

        private Action<bool> overlayObserver;

        public bool IsPacksizeCompatible()
        {
            PacksizeCount++;
            if (PacksizeException != null)
            {
                throw PacksizeException;
            }

            return PacksizeCompatible;
        }

        public SteamDllCheckObservation ObserveDllCheck()
        {
            DllCheckCount++;
            return DllCheckObservation;
        }

        public bool Initialize()
        {
            InitializeCount++;
            if (InitializeException != null)
            {
                throw InitializeException;
            }

            return InitializeResult;
        }

        public void RunCallbacks()
        {
            CallbackCount++;
            if (CallbackException != null)
            {
                throw CallbackException;
            }
        }

        public void Shutdown()
        {
            ShutdownCount++;
            if (ShutdownException != null)
            {
                throw ShutdownException;
            }
        }

        public uint GetAppId()
        {
            AppIdCount++;
            return AppId;
        }

        public bool IsSteamIdValid()
        {
            IdentityCount++;
            return SteamIdValid;
        }

        public bool IsOverlayEnabled()
        {
            return OverlayEnabled;
        }

        public bool IsLoggedOn()
        {
            LoggedOnCount++;
            return LoggedOn;
        }

        public void RegisterOverlayActivationCallback(Action<bool> observer)
        {
            OverlayCallbackRegistrationCount++;
            overlayObserver = observer;
        }

        public void DisposeOverlayActivationCallback()
        {
            OverlayCallbackDisposeCount++;
            overlayObserver = null;
        }

        internal void RaiseOverlayActivation(bool active)
        {
            overlayObserver?.Invoke(active);
        }
    }
}
