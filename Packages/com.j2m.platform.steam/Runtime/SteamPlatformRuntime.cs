using System;
using Game.Platform.Runtime;
using Game.Platform.Steam.ProductAchievements;

namespace Game.Platform.Steam
{
    public sealed class SteamPlatformRuntime : IPlatformRuntime
    {
        private const string NotInitializedDetail = "Steam provider has not been initialized.";
        internal const string SmokeResultPrefix = "J2M_STEAM_SMOKE_RESULT";
        private const int MaximumOverlayEnabledSmokeObservations = 300;

        private readonly ISteamNativeApi nativeApi;
        private readonly bool smokeRequested;
        private readonly Action<string> smokeLogger;
        private readonly SteamAchievementSmokeCoordinator achievementSmokeCoordinator;
        private readonly ISteamProductAchievementPublicationFeature
            productAchievementPublicationFeature;

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
        private bool loggedOn;
        private bool overlayObservationAvailable;
        private bool overlayEnabled;
        private bool overlayEnabledEverObserved;
        private int overlayEnabledSmokeObservationCount;
        private bool overlayCallbackRegistered;
        private int overlayActiveCount;
        private int overlayInactiveCount;
        private bool lastOverlayActive;
        private int callbackPumpCount;
        private int callbackAttemptCount;
        private int shutdownCallCount;
        private SteamPlatformFailureReason initializationFailureReason =
            SteamPlatformFailureReason.None;
        private SteamPlatformFailureReason lastFailureReason;
        private string lastExceptionType = string.Empty;
        private bool smokeResultEmitted;

        public SteamPlatformRuntime(ISteamNativeApi nativeApi)
            : this(nativeApi, smokeRequested: false, smokeLogger: null)
        {
        }

        internal SteamPlatformRuntime(
            ISteamNativeApi nativeApi,
            bool smokeRequested,
            Action<string> smokeLogger)
            : this(
                new SteamRuntimeDependencies(nativeApi, achievements: null),
                smokeRequested,
                achievementSmokeRequested: false,
                monotonicSeconds: null,
                smokeLogger)
        {
        }

        internal SteamPlatformRuntime(
            SteamRuntimeDependencies dependencies,
            bool smokeRequested,
            bool achievementSmokeRequested,
            Func<double> monotonicSeconds,
            Action<string> smokeLogger)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            nativeApi = dependencies.Lifecycle;
            this.smokeRequested = smokeRequested;
            this.smokeLogger = smokeLogger;
            achievementSmokeCoordinator = new SteamAchievementSmokeCoordinator(
                dependencies.Achievements,
                smokeRequested,
                achievementSmokeRequested,
                monotonicSeconds,
                smokeLogger);
            productAchievementPublicationFeature =
                new SteamProductAchievementPublicationFeature(
                    dependencies,
                    achievementSmokeRequested,
                    monotonicSeconds);
        }

        public static PlatformProviderId ProviderId { get; } = new PlatformProviderId("steam");

        PlatformProviderId IPlatformRuntime.ProviderId => ProviderId;

        public PlatformAvailability Availability => steamAvailability.ToPlatformAvailability();

        public SteamPlatformAvailability SteamAvailability => steamAvailability;

        public SteamAchievementSmokeDiagnostics AchievementSmokeDiagnostics =>
            achievementSmokeCoordinator.Diagnostics;

        public SteamPlatformDiagnostics Diagnostics => new SteamPlatformDiagnostics(
            state,
            initializationAttempted,
            initializationSucceeded,
            nativeInitializationResult,
            observedAppId,
            steamIdentityValid,
            loggedOn,
            overlayObservationAvailable,
            overlayEnabled,
            overlayEnabledEverObserved,
            overlayActiveCount,
            overlayInactiveCount,
            lastOverlayActive,
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

                RegisterOverlayCallback();
                ObserveOptionalRuntimeDiagnostics();

                state = SteamPlatformRuntimeState.Available;
                initializationSucceeded = true;
                lastFailureReason = SteamPlatformFailureReason.None;
                steamAvailability = SteamPlatformAvailability.Available();
                initializationResult = PlatformInitializationResult.Success;
                achievementSmokeCoordinator.BeginSession(
                    initializationSucceeded: true,
                    observedAppId,
                    steamIdentityValid,
                    loggedOn);
                productAchievementPublicationFeature.OnSteamInitialized(
                    initializationSucceeded: true,
                    observedAppId,
                    steamIdentityValid,
                    loggedOn);
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
                productAchievementPublicationFeature.Tick();
                achievementSmokeCoordinator.Tick();
                ObserveDelayedOverlayEnabledForSmoke();
            }
            catch (Exception exception)
            {
                state = SteamPlatformRuntimeState.Faulted;
                SetFailure(
                    SteamPlatformFailureReason.CallbackException,
                    FormatException(exception),
                    exception);
                productAchievementPublicationFeature.OnRuntimeFaulted();
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
            productAchievementPublicationFeature.DisposeBeforeNativeShutdown();
            achievementSmokeCoordinator.Shutdown();
            if (!nativeInitialized)
            {
                state = SteamPlatformRuntimeState.Shutdown;
                EmitSmokeResultOnce();
                return;
            }

            DisposeOverlayCallback();
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
            finally
            {
                EmitSmokeResultOnce();
            }
        }

        private PlatformInitializationResult FailInitialization(
            SteamPlatformFailureReason failureReason,
            string detail,
            Exception exception = null)
        {
            CaptureInitializationFailure(failureReason);
            state = IsNativeLoadFailure(failureReason)
                ? SteamPlatformRuntimeState.Faulted
                : SteamPlatformRuntimeState.Unavailable;
            SetFailure(failureReason, detail, exception);
            achievementSmokeCoordinator.BeginSession(
                initializationSucceeded: false,
                observedAppId,
                steamIdentityValid,
                loggedOn);
            productAchievementPublicationFeature.OnSteamInitialized(
                initializationSucceeded: false,
                observedAppId,
                steamIdentityValid,
                loggedOn);
            initializationResult = PlatformInitializationResult.Failure(
                failureReason + ": " + detail);
            return initializationResult;
        }

        private void CaptureInitializationFailure(
            SteamPlatformFailureReason failureReason)
        {
            if (initializationFailureReason == SteamPlatformFailureReason.None)
            {
                initializationFailureReason = failureReason;
            }
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
                loggedOn = nativeApi.IsLoggedOn();
            }
            catch (Exception exception)
            {
                lastExceptionType = exception.GetType().Name;
            }

            ObserveOverlayEnabled();
        }

        private void RegisterOverlayCallback()
        {
            try
            {
                nativeApi.RegisterOverlayActivationCallback(ObserveOverlayActivation);
                overlayCallbackRegistered = true;
            }
            catch (Exception exception)
            {
                lastExceptionType = exception.GetType().Name;
                overlayCallbackRegistered = false;
            }
        }

        private void DisposeOverlayCallback()
        {
            if (!overlayCallbackRegistered)
            {
                return;
            }

            overlayCallbackRegistered = false;
            try
            {
                nativeApi.DisposeOverlayActivationCallback();
            }
            catch (Exception exception)
            {
                lastExceptionType = exception.GetType().Name;
            }
        }

        private void ObserveOverlayActivation(bool active)
        {
            lastOverlayActive = active;
            if (active)
            {
                overlayActiveCount++;
            }
            else
            {
                overlayInactiveCount++;
            }
        }

        private void ObserveDelayedOverlayEnabledForSmoke()
        {
            if (!smokeRequested ||
                overlayEnabledEverObserved ||
                overlayEnabledSmokeObservationCount >= MaximumOverlayEnabledSmokeObservations)
            {
                return;
            }

            ObserveOverlayEnabled();
        }

        private void ObserveOverlayEnabled()
        {
            if (smokeRequested)
            {
                overlayEnabledSmokeObservationCount++;
            }

            try
            {
                overlayEnabled = nativeApi.IsOverlayEnabled();
                overlayEnabledEverObserved |= overlayEnabled;
                overlayObservationAvailable = true;
            }
            catch (Exception exception)
            {
                lastExceptionType = exception.GetType().Name;
                overlayObservationAvailable = false;
            }
        }

        private void EmitSmokeResultOnce()
        {
            if (!smokeRequested || smokeResultEmitted)
            {
                return;
            }

            smokeResultEmitted = true;
            var initializationFailureKind = initializationSucceeded
                ? SteamPlatformFailureReason.None
                : initializationFailureReason;
            var finalFailureKind = lastFailureReason;
            var finalProviderAvailable = initializationSucceeded &&
                finalFailureKind == SteamPlatformFailureReason.None &&
                steamAvailability.IsAvailable;
            var selectionStatus = finalProviderAvailable
                ? PlatformRuntimeSelectionStatus.ExplicitProviderSelected
                : PlatformRuntimeSelectionStatus.RequestedProviderUnavailable;
            var exceptionType = string.IsNullOrEmpty(lastExceptionType)
                ? "none"
                : lastExceptionType;
            var result = SmokeResultPrefix + " {" +
                "\"requestedProvider\":\"steam\"," +
                "\"selectedProvider\":\"steam\"," +
                "\"selectionStatus\":\"" + selectionStatus + "\"," +
                "\"fallbackUsed\":false," +
                "\"initSucceeded\":" + ToJsonBoolean(initializationSucceeded) + "," +
                "\"initializationFailureKind\":\"" + initializationFailureKind + "\"," +
                "\"finalFailureKind\":\"" + finalFailureKind + "\"," +
                "\"observedAppId\":" + observedAppId + "," +
                "\"steamIdValid\":" + ToJsonBoolean(steamIdentityValid) + "," +
                "\"loggedOn\":" + ToJsonBoolean(loggedOn) + "," +
                "\"callbackPumpAttemptCount\":" + callbackAttemptCount + "," +
                "\"callbackPumpSuccessCount\":" + callbackPumpCount + "," +
                "\"overlayEnabledEverObserved\":" +
                    ToJsonBoolean(overlayEnabledEverObserved) + "," +
                "\"overlayActiveCount\":" + overlayActiveCount + "," +
                "\"overlayInactiveCount\":" + overlayInactiveCount + "," +
                "\"lastOverlayActive\":" + ToJsonBoolean(lastOverlayActive) + "," +
                "\"nativeExceptionType\":\"" + exceptionType + "\"," +
                "\"shutdownNativeCallCount\":" + shutdownCallCount +
                "}";
            (smokeLogger ?? UnityEngine.Debug.Log)(result);
        }

        private static string ToJsonBoolean(bool value)
        {
            return value ? "true" : "false";
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
