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
            bool overlayObservationAvailable,
            bool overlayEnabled,
            int callbackPumpCount,
            int callbackAttemptCount,
            int shutdownCallCount,
            SteamPlatformFailureReason lastFailureReason,
            string lastExceptionType,
            SteamDllCheckObservation dllCheckObservation)
        {
            State = state;
            InitializationAttempted = initializationAttempted;
            InitializationSucceeded = initializationSucceeded;
            NativeInitializationResult = nativeInitializationResult;
            ObservedAppId = observedAppId;
            SteamIdentityValid = steamIdentityValid;
            OverlayObservationAvailable = overlayObservationAvailable;
            OverlayEnabled = overlayEnabled;
            CallbackPumpCount = callbackPumpCount;
            CallbackAttemptCount = callbackAttemptCount;
            ShutdownCallCount = shutdownCallCount;
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

        public bool OverlayObservationAvailable { get; }

        public bool OverlayEnabled { get; }

        public int CallbackPumpCount { get; }

        public int CallbackAttemptCount { get; }

        public int ShutdownCallCount { get; }

        public SteamPlatformFailureReason LastFailureReason { get; }

        public string LastExceptionType { get; }

        public SteamDllCheckObservation DllCheckObservation { get; }
    }
}
