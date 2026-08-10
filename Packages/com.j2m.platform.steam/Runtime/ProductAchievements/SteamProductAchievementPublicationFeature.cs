using System;
using Game.Product.Achievements.Composition;

namespace Game.Platform.Steam.ProductAchievements
{
    internal interface ISteamProductAchievementPublicationFeature
    {
        void OnSteamInitialized(
            bool initializationSucceeded,
            uint observedAppId,
            bool steamIdValid,
            bool loggedOn);

        void Tick();

        void DisposeBeforeNativeShutdown();
    }

    internal sealed class SteamProductAchievementPublicationFeature :
        ISteamProductAchievementPublicationFeature
    {
        private readonly SteamRuntimeDependencies _dependencies;
        private readonly bool _achievementSmokeRequested;
        private readonly Func<double> _monotonicSeconds;

        private SteamAchievementPublisher _publisher;
        private bool _initializationObserved;
        private bool _registeredWithProduct;
        private bool _disposed;

        internal SteamProductAchievementPublicationFeature(
            SteamRuntimeDependencies dependencies,
            bool achievementSmokeRequested,
            Func<double> monotonicSeconds)
        {
            _dependencies = dependencies ??
                throw new ArgumentNullException(nameof(dependencies));
            _achievementSmokeRequested = achievementSmokeRequested;
            _monotonicSeconds = monotonicSeconds ??
                (() => UnityEngine.Time.realtimeSinceStartupAsDouble);
        }

        public void OnSteamInitialized(
            bool initializationSucceeded,
            uint observedAppId,
            bool steamIdValid,
            bool loggedOn)
        {
            if (_disposed || _initializationObserved)
            {
                return;
            }

            _initializationObserved = true;
            if (_achievementSmokeRequested || _dependencies.Achievements == null)
            {
                return;
            }

            var publisher = new SteamAchievementPublisher(
                _dependencies.Lifecycle,
                _dependencies.Achievements,
                SteamAchievementMapping.Production,
                _monotonicSeconds);
            if (!publisher.BeginSession(
                new SteamAchievementSessionPrerequisites(
                    initializationSucceeded,
                    observedAppId,
                    steamIdValid,
                    loggedOn)))
            {
                publisher.Dispose();
                return;
            }

            if (!ProductAchievementPublicationSessionHandoff.TryRegisterSteamSession(
                this,
                publisher))
            {
                publisher.Dispose();
                return;
            }

            _publisher = publisher;
            _registeredWithProduct = true;
        }

        public void Tick()
        {
            _publisher?.Tick();
        }

        public void DisposeBeforeNativeShutdown()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var publisher = _publisher;
            _publisher = null;
            publisher?.Dispose();
            if (_registeredWithProduct)
            {
                _registeredWithProduct = false;
                ProductAchievementPublicationSessionHandoff.ClearSteamSession(
                    this,
                    publisher);
            }
        }
    }
}
