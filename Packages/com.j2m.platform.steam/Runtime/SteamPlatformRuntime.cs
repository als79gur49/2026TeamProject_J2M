using System;
using Game.Platform.Runtime;
using Game.Platform.Steam.ProductAchievements;

namespace Game.Platform.Steam
{
    public sealed class SteamPlatformRuntime : IPlatformRuntime, ISteamObservationAchievementsReader
    {
        private const string NotInitializedDetail = "Steam provider has not been initialized.";
        internal const string SmokeResultPrefix = "J2M_STEAM_SMOKE_RESULT";
        private const int MaximumOverlayEnabledSmokeObservations = 300;

        private readonly ISteamNativeApi nativeApi;
        private readonly ISteamAchievementApi achievementApi;
        private int ownerThread;
        private bool maintenanceOwned;
        private SteamAchievementMaintenanceLease maintenanceLease;
        private bool maintenanceFailed;
        private bool publicationStarted;
        private readonly bool maintenanceSmokeExcluded;
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
        private bool shutdownReturned;
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
            achievementApi = dependencies.Achievements;
            maintenanceSmokeExcluded = achievementSmokeRequested;
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
            dllCheckObservation, shutdownReturned);

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

            SteamOverlayObservationAccess.Starting(this);
            ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
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
                if (!SteamOverlayObservationAccess.Requested && !SteamAchievementMaintenanceAccess.ResetTrial) achievementSmokeCoordinator.BeginSession(
                    initializationSucceeded: true,
                    observedAppId,
                    steamIdentityValid,
                    loggedOn);
                SteamAchievementMaintenanceAccess.Register(this);
                if (!SteamOverlayObservationAccess.Requested && !SteamAchievementMaintenanceAccess.IsDeferred)
                    StartDeferredPublication(refreshIdentity: false);
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
            if (SteamOverlayObservationAccess.ValidatedOwner) SteamOverlayObservationAccess.RequireMainThread();
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
                if (!SteamOverlayObservationAccess.Requested && !SteamAchievementMaintenanceAccess.ResetTrial)
                {
                    productAchievementPublicationFeature.Tick();
                    achievementSmokeCoordinator.Tick();
                    ObserveDelayedOverlayEnabledForSmoke();
                }
            }
            catch (Exception exception)
            {
                state = SteamPlatformRuntimeState.Faulted;
                SetFailure(
                    SteamPlatformFailureReason.CallbackException,
                    FormatException(exception),
                    exception);
                if (!SteamOverlayObservationAccess.Requested) productAchievementPublicationFeature.OnRuntimeFaulted();
            }
        }

        public SteamObservationAchievement ReadAchievement(string target)
        {
            RequireObservationThread();
            if (achievementApi == null || string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("Observation achievement getter unavailable.");
            bool achieved;
            bool succeeded = achievementApi.GetAchievement(target, out achieved);
            return new SteamObservationAchievement { Target = target, ReadSucceeded = succeeded, Achieved = succeeded ? (bool?)achieved : null };
        }

        public SteamObservationIdentity ReadObservationIdentity()
        {
            RequireObservationThread();
            if (!(nativeApi is ISteamObservationIdentityApi identity))
                throw new InvalidOperationException("Native identity getter unavailable.");
            return new SteamObservationIdentity { AppId = nativeApi.GetAppId(), SteamId = identity.GetSteamId(),
                Valid = nativeApi.IsSteamIdValid(), LoggedOn = nativeApi.IsLoggedOn() };
        }

        private void RequireObservationThread()
        {
            if (!SteamOverlayObservationAccess.Requested || !MaintenanceAvailable ||
                ownerThread != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Read-only observation requires the available runtime owner thread.");
        }

        internal bool MaintenanceAvailable =>
            state == SteamPlatformRuntimeState.Available && nativeInitialized && !shutdownAttempted;

        internal SteamAchievementMaintenanceLease AcquireMaintenance(
            Action<SteamStatsStoredObservation> stats,
            Action<SteamAchievementStoredObservation> achievements)
        {
            SteamOverlayObservationAccess.RequireWritesAllowed();
            if (!MaintenanceAvailable || publicationStarted || maintenanceOwned || maintenanceFailed ||
                achievementApi == null || maintenanceSmokeExcluded)
                throw new InvalidOperationException("Steam achievement maintenance is unavailable.");
            achievementApi.RegisterAchievementStoreCallbacks(stats, achievements);
            maintenanceOwned = true;
            maintenanceLease = new SteamAchievementMaintenanceLease(achievementApi, failed =>
            {
                try { achievementApi.DisposeAchievementStoreCallbacks(); }
                catch { maintenanceFailed = true; throw; }
                finally { maintenanceOwned = false; maintenanceFailed |= failed; maintenanceLease = null; }
            });
            return maintenanceLease;
        }

        internal bool StartDeferredPublication(bool refreshIdentity = true)
        {
            if (SteamOverlayObservationAccess.Requested || SteamAchievementMaintenanceAccess.ResetTrial || !MaintenanceAvailable || maintenanceOwned || maintenanceFailed) return false;
            if (!publicationStarted)
            {
                publicationStarted = true;
                productAchievementPublicationFeature.OnSteamInitialized(true,
                    observedAppId,
                    refreshIdentity ? nativeApi.IsSteamIdValid() : steamIdentityValid,
                    refreshIdentity ? nativeApi.IsLoggedOn() : loggedOn);
            }
            return productAchievementPublicationFeature.IsAttached;
        }

        internal void StopPublication()
        {
            if (!SteamOverlayObservationAccess.ValidatedOwner || publicationStarted) productAchievementPublicationFeature.OnRuntimeFaulted();
        }

        public void Shutdown()
        {
            if (SteamOverlayObservationAccess.ValidatedOwner) SteamOverlayObservationAccess.RequireMainThread();
            if (shutdownAttempted)
            {
                return;
            }

            SteamAchievementMaintenanceAccess.Unregister(this);
            shutdownAttempted = true;
            state = SteamPlatformRuntimeState.ShuttingDown;
            try { maintenanceLease?.Dispose(); }
            catch (Exception exception)
            {
                SetFailure(SteamPlatformFailureReason.ShutdownException, FormatException(exception), exception);
            }
            if (!SteamOverlayObservationAccess.Requested || publicationStarted)
                productAchievementPublicationFeature.DisposeBeforeNativeShutdown();
            if (!SteamOverlayObservationAccess.Requested) achievementSmokeCoordinator.Shutdown();
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
                shutdownReturned = true;
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
            if (!SteamOverlayObservationAccess.Requested && !SteamAchievementMaintenanceAccess.ResetTrial) achievementSmokeCoordinator.BeginSession(
                initializationSucceeded: false,
                observedAppId,
                steamIdentityValid,
                loggedOn);
            if (!SteamOverlayObservationAccess.Requested) productAchievementPublicationFeature.OnSteamInitialized(
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

            if (!SteamOverlayObservationAccess.Requested && !SteamAchievementMaintenanceAccess.ResetTrial) ObserveOverlayEnabled();
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
                if (SteamOverlayObservationAccess.Requested) SetFailure(SteamPlatformFailureReason.ShutdownException, FormatException(exception), exception);
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

            if (!SteamOverlayObservationAccess.Requested && !SteamAchievementMaintenanceAccess.ResetTrial) ObserveOverlayEnabled();
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
