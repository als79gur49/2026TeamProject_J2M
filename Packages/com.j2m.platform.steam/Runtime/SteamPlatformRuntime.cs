using System;
using Game.Platform.Runtime;

namespace Game.Platform.Steam
{
    public sealed class SteamPlatformRuntime : IPlatformRuntime
    {
        private const string NotInitializedDetail = "Steam provider has not been initialized.";

        private readonly ISteamNativeApi nativeApi;

        private SteamPlatformRuntimeState state = SteamPlatformRuntimeState.NotInitialized;
        private SteamPlatformAvailability steamAvailability =
            SteamPlatformAvailability.Unavailable(
                SteamPlatformFailureReason.None,
                NotInitializedDetail);
        private PlatformInitializationResult initializationResult;
        private SteamNativeInitializationResult nativeInitializationResult;
        private SteamDllCheckObservation dllCheckObservation;
        private bool initializationAttempted;
        private bool initializationSucceeded;
        private bool nativeInitialized;
        private bool shutdownAttempted;
        private uint observedAppId;
        private bool steamIdentityValid;
        private bool overlayObservationAvailable;
        private bool overlayEnabled;
        private int callbackPumpCount;
        private int callbackAttemptCount;
        private int shutdownCallCount;
        private SteamPlatformFailureReason lastFailureReason;
        private string lastExceptionType = string.Empty;

        public SteamPlatformRuntime(ISteamNativeApi nativeApi)
        {
            this.nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        }

        public static PlatformProviderId ProviderId { get; } = new PlatformProviderId("steam");

        PlatformProviderId IPlatformRuntime.ProviderId => ProviderId;

        public PlatformAvailability Availability => steamAvailability.ToPlatformAvailability();

        public SteamPlatformAvailability SteamAvailability => steamAvailability;

        public SteamPlatformDiagnostics Diagnostics => new SteamPlatformDiagnostics(
            state,
            initializationAttempted,
            initializationSucceeded,
            nativeInitializationResult,
            observedAppId,
            steamIdentityValid,
            overlayObservationAvailable,
            overlayEnabled,
            callbackPumpCount,
            callbackAttemptCount,
            shutdownCallCount,
            lastFailureReason,
            lastExceptionType,
            dllCheckObservation);

        public PlatformInitializationResult Initialize()
        {
            if (shutdownAttempted)
            {
                return PlatformInitializationResult.Failure(
                    "Steam provider cannot initialize after shutdown.");
            }

            if (initializationAttempted)
            {
                return initializationResult;
            }

            initializationAttempted = true;
            state = SteamPlatformRuntimeState.Initializing;

            try
            {
                if (!nativeApi.IsPacksizeCompatible())
                {
                    return FailInitialization(
                        SteamPlatformFailureReason.PacksizeMismatch,
                        "Steamworks managed/native pack size compatibility check failed.");
                }

                dllCheckObservation = nativeApi.ObserveDllCheck();

                bool initialized;
                try
                {
                    initialized = nativeApi.Initialize();
                }
                catch (Exception exception)
                {
                    nativeInitializationResult = SteamNativeInitializationResult.Threw;
                    return FailInitialization(
                        MapNativeLoadFailure(exception, SteamPlatformFailureReason.InitializationException),
                        FormatException(exception),
                        exception);
                }

                if (!initialized)
                {
                    nativeInitializationResult = SteamNativeInitializationResult.ReturnedFalse;
                    return FailInitialization(
                        SteamPlatformFailureReason.InitializationReturnedFalse,
                        "Native Steam initialization returned false.");
                }

                nativeInitialized = true;
                nativeInitializationResult = SteamNativeInitializationResult.Succeeded;
                observedAppId = nativeApi.GetAppId();
                if (observedAppId == 0)
                {
                    return FailInitialization(
                        SteamPlatformFailureReason.AppIdUnavailable,
                        "SteamAPI initialized but returned AppID 0.");
                }

                ObserveOptionalRuntimeDiagnostics();

                state = SteamPlatformRuntimeState.Available;
                initializationSucceeded = true;
                lastFailureReason = SteamPlatformFailureReason.None;
                steamAvailability = SteamPlatformAvailability.Available();
                initializationResult = PlatformInitializationResult.Success;
                return initializationResult;
            }
            catch (Exception exception)
            {
                return FailInitialization(
                    MapNativeLoadFailure(exception, SteamPlatformFailureReason.InitializationException),
                    FormatException(exception),
                    exception);
            }
        }

        public void Tick()
        {
            if (state != SteamPlatformRuntimeState.Available ||
                !nativeInitialized ||
                shutdownAttempted)
            {
                return;
            }

            callbackAttemptCount++;
            try
            {
                nativeApi.RunCallbacks();
                callbackPumpCount++;
            }
            catch (Exception exception)
            {
                state = SteamPlatformRuntimeState.Faulted;
                SetFailure(
                    SteamPlatformFailureReason.CallbackException,
                    FormatException(exception),
                    exception);
            }
        }

        public void Shutdown()
        {
            if (shutdownAttempted)
            {
                return;
            }

            shutdownAttempted = true;
            state = SteamPlatformRuntimeState.ShuttingDown;
            if (!nativeInitialized)
            {
                state = SteamPlatformRuntimeState.Shutdown;
                return;
            }

            shutdownCallCount++;
            try
            {
                nativeApi.Shutdown();
                state = SteamPlatformRuntimeState.Shutdown;
            }
            catch (Exception exception)
            {
                state = SteamPlatformRuntimeState.Faulted;
                SetFailure(
                    SteamPlatformFailureReason.ShutdownException,
                    FormatException(exception),
                    exception);
            }
        }

        private PlatformInitializationResult FailInitialization(
            SteamPlatformFailureReason failureReason,
            string detail,
            Exception exception = null)
        {
            state = IsNativeLoadFailure(failureReason)
                ? SteamPlatformRuntimeState.Faulted
                : SteamPlatformRuntimeState.Unavailable;
            SetFailure(failureReason, detail, exception);
            initializationResult = PlatformInitializationResult.Failure(
                failureReason + ": " + detail);
            return initializationResult;
        }

        private void ObserveOptionalRuntimeDiagnostics()
        {
            try
            {
                steamIdentityValid = nativeApi.IsSteamIdValid();
            }
            catch (Exception exception)
            {
                lastExceptionType = exception.GetType().Name;
            }

            try
            {
                overlayEnabled = nativeApi.IsOverlayEnabled();
                overlayObservationAvailable = true;
            }
            catch (Exception exception)
            {
                lastExceptionType = exception.GetType().Name;
                overlayObservationAvailable = false;
            }
        }

        private void SetFailure(
            SteamPlatformFailureReason failureReason,
            string detail,
            Exception exception)
        {
            lastFailureReason = failureReason;
            lastExceptionType = exception == null ? string.Empty : exception.GetType().Name;
            steamAvailability = SteamPlatformAvailability.Unavailable(failureReason, detail);
        }

        private static SteamPlatformFailureReason MapNativeLoadFailure(
            Exception exception,
            SteamPlatformFailureReason fallback)
        {
            if (exception is DllNotFoundException)
            {
                return SteamPlatformFailureReason.DllMissing;
            }

            if (exception is BadImageFormatException)
            {
                return SteamPlatformFailureReason.BadImageFormat;
            }

            if (exception is EntryPointNotFoundException)
            {
                return SteamPlatformFailureReason.EntryPointMissing;
            }

            return fallback;
        }

        private static bool IsNativeLoadFailure(SteamPlatformFailureReason failureReason)
        {
            return failureReason == SteamPlatformFailureReason.DllMissing ||
                   failureReason == SteamPlatformFailureReason.BadImageFormat ||
                   failureReason == SteamPlatformFailureReason.EntryPointMissing ||
                   failureReason == SteamPlatformFailureReason.InitializationException;
        }

        private static string FormatException(Exception exception)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message)
                ? "without a message"
                : "with message '" + exception.Message + "'";
            return exception.GetType().Name + " " + message;
        }
    }
}
