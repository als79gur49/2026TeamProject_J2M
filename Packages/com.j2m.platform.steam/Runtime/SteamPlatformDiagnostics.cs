namespace Game.Platform.Steam
{
    public readonly struct SteamPlatformDiagnostics
    {
        internal SteamPlatformDiagnostics(
            SteamPlatformRuntimeState state,
            bool initializationAttempted,
            bool initializationSucceeded,
            SteamNativeInitializationResult nativeInitializationResult,
            uint observedAppId,
            bool steamIdentityValid,
            bool loggedOn,
            bool overlayObservationAvailable,
            bool overlayEnabled,
            bool overlayEnabledEverObserved,
            int overlayActiveCount,
            int overlayInactiveCount,
            bool lastOverlayActive,
            int callbackPumpCount,
            int callbackAttemptCount,
            int shutdownCallCount,
            SteamPlatformFailureReason lastFailureReason,
            string lastExceptionType,
            SteamDllCheckObservation dllCheckObservation,
            bool shutdownReturned = false)
        {
            State = state;
            InitializationAttempted = initializationAttempted;
            InitializationSucceeded = initializationSucceeded;
            NativeInitializationResult = nativeInitializationResult;
            ObservedAppId = observedAppId;
            SteamIdentityValid = steamIdentityValid;
            LoggedOn = loggedOn;
            OverlayObservationAvailable = overlayObservationAvailable;
            OverlayEnabled = overlayEnabled;
            OverlayEnabledEverObserved = overlayEnabledEverObserved;
            OverlayActiveCount = overlayActiveCount;
            OverlayInactiveCount = overlayInactiveCount;
            LastOverlayActive = lastOverlayActive;
            CallbackPumpCount = callbackPumpCount;
            CallbackAttemptCount = callbackAttemptCount;
            ShutdownCallCount = shutdownCallCount;
            ShutdownReturned = shutdownReturned;
            LastFailureReason = lastFailureReason;
            LastExceptionType = lastExceptionType ?? string.Empty;
            DllCheckObservation = dllCheckObservation;
        }

        public string ProviderId => SteamPlatformRuntime.ProviderId.Value;

        public SteamPlatformRuntimeState State { get; }

        public bool InitializationAttempted { get; }

        public bool InitializationSucceeded { get; }

        public SteamNativeInitializationResult NativeInitializationResult { get; }

        public uint ObservedAppId { get; }

        public bool SteamIdentityValid { get; }

        public bool LoggedOn { get; }

        public bool OverlayObservationAvailable { get; }

        public bool OverlayEnabled { get; }

        public bool OverlayEnabledEverObserved { get; }

        public int OverlayActiveCount { get; }

        public int OverlayInactiveCount { get; }

        public bool LastOverlayActive { get; }

        public int CallbackPumpCount { get; }

        public int CallbackAttemptCount { get; }

        public int ShutdownCallCount { get; }

        public bool ShutdownReturned { get; }

        public SteamPlatformFailureReason LastFailureReason { get; }

        public string LastExceptionType { get; }

        public SteamDllCheckObservation DllCheckObservation { get; }
    }
}
