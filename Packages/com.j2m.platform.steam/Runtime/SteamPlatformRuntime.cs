using System;
using Game.Platform.Runtime;
using Game.Platform.Steam.ProductAchievements;

namespace Game.Platform.Steam
{
    public sealed class SteamPlatformRuntime : IPlatformRuntime
    {
        private const string NotInitializedDetail = "Steam provider has not been initialized.";

        private readonly ISteamNativeApi nativeApi;
        private readonly ISteamAchievementApi achievementApi;
        private bool maintenanceOwned;
        private SteamAchievementMaintenanceLease maintenanceLease;
        private bool maintenanceFailed;
        private bool publicationStarted;

        private readonly ISteamProductAchievementPublicationFeature
            productAchievementPublicationFeature;

        private SteamPlatformRuntimeState state = SteamPlatformRuntimeState.NotInitialized;
        private SteamPlatformAvailability steamAvailability =
            SteamPlatformAvailability.Unavailable(
                SteamPlatformFailureReason.None,
                NotInitializedDetail);
        private PlatformInitializationResult initializationResult;
        private SteamNativeInitializationResult nativeInitializationResult;
        private bool initializationAttempted;
        private bool initializationSucceeded;
        private bool nativeInitialized;
        private bool shutdownAttempted;
        private uint observedAppId;
        private bool steamIdentityValid;
        private bool loggedOn;
        private SteamPlatformFailureReason lastFailureReason;
        private string lastExceptionType = string.Empty;

        public SteamPlatformRuntime(ISteamNativeApi nativeApi)
            : this(new SteamRuntimeDependencies(nativeApi, achievements: null), monotonicSeconds: null)
        {
        }

        internal SteamPlatformRuntime(
            SteamRuntimeDependencies dependencies,
            Func<double> monotonicSeconds)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            nativeApi = dependencies.Lifecycle;
            achievementApi = dependencies.Achievements;
            productAchievementPublicationFeature =
                new SteamProductAchievementPublicationFeature(dependencies, monotonicSeconds);
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
            loggedOn,
            lastFailureReason,
            lastExceptionType);

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

                ObserveSessionIdentity();

                state = SteamPlatformRuntimeState.Available;
                initializationSucceeded = true;
                lastFailureReason = SteamPlatformFailureReason.None;
                steamAvailability = SteamPlatformAvailability.Available();
                initializationResult = PlatformInitializationResult.Success;
                SteamAchievementMaintenanceAccess.Register(this);
                if (!SteamAchievementMaintenanceAccess.IsDeferred)
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
            if (state != SteamPlatformRuntimeState.Available ||
                !nativeInitialized ||
                shutdownAttempted)
            {
                return;
            }

            try
            {
                nativeApi.RunCallbacks();
                productAchievementPublicationFeature.Tick();
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

        internal bool MaintenanceAvailable =>
            state == SteamPlatformRuntimeState.Available && nativeInitialized && !shutdownAttempted;

        internal SteamAchievementMaintenanceLease AcquireMaintenance(
            Action<SteamStatsStoredObservation> stats,
            Action<SteamAchievementStoredObservation> achievements)
        {
            if (!MaintenanceAvailable || publicationStarted || maintenanceOwned || maintenanceFailed ||
                achievementApi == null)
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
            if (!MaintenanceAvailable || maintenanceOwned || maintenanceFailed) return false;
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
            productAchievementPublicationFeature.OnRuntimeFaulted();
        }

        public void Shutdown()
        {
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
            productAchievementPublicationFeature.DisposeBeforeNativeShutdown();
            if (!nativeInitialized)
            {
                state = SteamPlatformRuntimeState.Shutdown;
                return;
            }

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
            productAchievementPublicationFeature.OnSteamInitialized(
                initializationSucceeded: false,
                observedAppId,
                steamIdentityValid,
                loggedOn);
            initializationResult = PlatformInitializationResult.Failure(
                failureReason + ": " + detail);
            return initializationResult;
        }

        private void ObserveSessionIdentity()
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
