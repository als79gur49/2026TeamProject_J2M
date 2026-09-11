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
            SteamPlatformFailureReason lastFailureReason,
            string lastExceptionType)
        {
            State = state;
            InitializationAttempted = initializationAttempted;
            InitializationSucceeded = initializationSucceeded;
            NativeInitializationResult = nativeInitializationResult;
            ObservedAppId = observedAppId;
            SteamIdentityValid = steamIdentityValid;
            LoggedOn = loggedOn;
            LastFailureReason = lastFailureReason;
            LastExceptionType = lastExceptionType ?? string.Empty;
        }

        public string ProviderId => SteamPlatformRuntime.ProviderId.Value;

        public SteamPlatformRuntimeState State { get; }

        public bool InitializationAttempted { get; }

        public bool InitializationSucceeded { get; }

        public SteamNativeInitializationResult NativeInitializationResult { get; }

        public uint ObservedAppId { get; }

        public bool SteamIdentityValid { get; }

        public bool LoggedOn { get; }

        public SteamPlatformFailureReason LastFailureReason { get; }

        public string LastExceptionType { get; }
    }
}
