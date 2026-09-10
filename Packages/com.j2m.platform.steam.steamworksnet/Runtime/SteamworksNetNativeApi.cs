using System;
using Steamworks;

namespace Game.Platform.Steam.SteamworksNet
{
    public sealed class SteamworksNetNativeApi : ISteamNativeApi, ISteamAchievementApi, ISteamObservationIdentityApi
    {
        private Callback<GameOverlayActivated_t> overlayActivatedCallback;
        private Action<bool> overlayActivationObserver;
        private readonly Func<Action<UserStatsStored_t>, IDisposable>
            statsStoredCallbackFactory;
        private readonly Func<Action<UserAchievementStored_t>, IDisposable>
            achievementStoredCallbackFactory;
        private IDisposable statsStoredCallback;
        private IDisposable achievementStoredCallback;
        private Action<SteamStatsStoredObservation> statsStoredObserver;
        private Action<SteamAchievementStoredObservation> achievementStoredObserver;

        public SteamworksNetNativeApi()
            : this(
                observer => Callback<UserStatsStored_t>.Create(observer.Invoke),
                observer => Callback<UserAchievementStored_t>.Create(observer.Invoke))
        {
        }

        internal SteamworksNetNativeApi(
            Func<Action<UserStatsStored_t>, IDisposable> statsStoredCallbackFactory,
            Func<Action<UserAchievementStored_t>, IDisposable>
                achievementStoredCallbackFactory)
        {
            this.statsStoredCallbackFactory = statsStoredCallbackFactory ??
                throw new ArgumentNullException(nameof(statsStoredCallbackFactory));
            this.achievementStoredCallbackFactory = achievementStoredCallbackFactory ??
                throw new ArgumentNullException(nameof(achievementStoredCallbackFactory));
        }

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

        public ulong GetSteamId() => SteamUser.GetSteamID().m_SteamID;

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

        public uint GetNumAchievements()
        {
            return SteamUserStats.GetNumAchievements();
        }

        public string GetAchievementName(uint index)
        {
            return SteamUserStats.GetAchievementName(index);
        }

        public bool GetAchievement(string achievementName, out bool achieved)
        {
            return SteamUserStats.GetAchievement(achievementName, out achieved);
        }

        public bool SetAchievement(string achievementName)
        {
            SteamOverlayObservationAccess.RequireWritesAllowed();
            return SteamUserStats.SetAchievement(achievementName);
        }

        public bool StoreStats()
        {
            SteamOverlayObservationAccess.RequireWritesAllowed();
            return SteamUserStats.StoreStats();
        }

        public void RegisterAchievementStoreCallbacks(
            Action<SteamStatsStoredObservation> statsObserver,
            Action<SteamAchievementStoredObservation> achievementObserver)
        {
            if (statsObserver == null)
            {
                throw new ArgumentNullException(nameof(statsObserver));
            }

            if (achievementObserver == null)
            {
                throw new ArgumentNullException(nameof(achievementObserver));
            }

            if (statsStoredCallback != null || achievementStoredCallback != null)
            {
                throw new InvalidOperationException(
                    "Steam achievement store callbacks are already registered.");
            }

            IDisposable localStatsStoredCallback = null;
            IDisposable localAchievementStoredCallback = null;
            try
            {
                localStatsStoredCallback = statsStoredCallbackFactory(OnStatsStored);
                localAchievementStoredCallback =
                    achievementStoredCallbackFactory(OnAchievementStored);

                statsStoredObserver = statsObserver;
                achievementStoredObserver = achievementObserver;
                statsStoredCallback = localStatsStoredCallback;
                achievementStoredCallback = localAchievementStoredCallback;
            }
            catch
            {
                localAchievementStoredCallback?.Dispose();
                localStatsStoredCallback?.Dispose();
                statsStoredObserver = null;
                achievementStoredObserver = null;
                statsStoredCallback = null;
                achievementStoredCallback = null;
                throw;
            }
        }

        public void DisposeAchievementStoreCallbacks()
        {
            var achievementCallback = achievementStoredCallback;
            var statsCallback = statsStoredCallback;
            achievementStoredCallback = null;
            statsStoredCallback = null;
            achievementStoredObserver = null;
            statsStoredObserver = null;

            Exception firstException = null;
            try
            {
                achievementCallback?.Dispose();
            }
            catch (Exception exception)
            {
                firstException = exception;
            }

            try
            {
                statsCallback?.Dispose();
            }
            catch (Exception exception)
            {
                if (firstException == null)
                {
                    firstException = exception;
                }
            }

            if (firstException != null)
            {
                throw firstException;
            }
        }

        private void OnOverlayActivated(GameOverlayActivated_t observation)
        {
            overlayActivationObserver?.Invoke(observation.m_bActive != 0);
        }

        private void OnStatsStored(UserStatsStored_t observation)
        {
            statsStoredObserver?.Invoke(new SteamStatsStoredObservation(
                NormalizeAppId(observation.m_nGameID),
                NormalizeResult(observation.m_eResult)));
        }

        private void OnAchievementStored(UserAchievementStored_t observation)
        {
            achievementStoredObserver?.Invoke(new SteamAchievementStoredObservation(
                NormalizeAppId(observation.m_nGameID),
                observation.m_rgchAchievementName,
                observation.m_nCurProgress == 0 && observation.m_nMaxProgress == 0));
        }

        private static uint NormalizeAppId(ulong gameId)
        {
            return new CGameID(gameId).AppID().m_AppId;
        }

        private static SteamCallbackResult NormalizeResult(EResult result)
        {
            if (result == EResult.k_EResultOK)
            {
                return SteamCallbackResult.Ok;
            }

            return result == EResult.k_EResultNone
                ? SteamCallbackResult.Unknown
                : SteamCallbackResult.Failure;
        }
    }
}
