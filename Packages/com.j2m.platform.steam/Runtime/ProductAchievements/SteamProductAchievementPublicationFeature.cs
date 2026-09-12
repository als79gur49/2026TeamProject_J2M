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

        void OnRuntimeFaulted();

        void DisposeBeforeNativeShutdown();
    }

    internal sealed class SteamProductAchievementPublicationFeature :
        ISteamProductAchievementPublicationFeature
    {
        private readonly SteamRuntimeDependencies _dependencies;
        private readonly Func<double> _monotonicSeconds;

        private SteamAchievementPublisher _publisher;
        private bool _initializationObserved;
        private bool _registeredWithProduct;
        private bool _publicationStopped;

        internal SteamProductAchievementPublicationFeature(
            SteamRuntimeDependencies dependencies,
            Func<double> monotonicSeconds)
        {
            _dependencies = dependencies ??
                throw new ArgumentNullException(nameof(dependencies));
            _monotonicSeconds = monotonicSeconds ??
                (() => UnityEngine.Time.realtimeSinceStartupAsDouble);
        }

        public void OnSteamInitialized(
            bool initializationSucceeded,
            uint observedAppId,
            bool steamIdValid,
            bool loggedOn)
        {
            if (_publicationStopped || _initializationObserved)
            {
                return;
            }

            _initializationObserved = true;
            if (_dependencies.Achievements == null)
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

        public void OnRuntimeFaulted()
        {
            StopPublication();
        }

        public void DisposeBeforeNativeShutdown()
        {
            StopPublication();
        }

        private void StopPublication()
        {
            if (_publicationStopped)
            {
                return;
            }

            _publicationStopped = true;
            var publisher = _publisher;
            _publisher = null;
            if (_registeredWithProduct)
            {
                _registeredWithProduct = false;
                ProductAchievementPublicationSessionHandoff.ClearSteamSession(
                    this,
                    publisher);
            }

            publisher?.Dispose();
        }
    }
}
