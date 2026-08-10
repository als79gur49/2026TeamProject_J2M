using System;

namespace Game.Product.Achievements.Composition
{
    internal interface IProductAchievementPublicationSessionController
    {
        bool TryAttach(
            object sessionIdentity,
            IAchievementPublicationSink publicationSink);

        void Detach(
            object sessionIdentity,
            IAchievementPublicationSink publicationSink);
    }

    internal sealed class ProductAchievementPublicationSessionController :
        IProductAchievementPublicationSessionController,
        IDisposable
    {
        private readonly object _gate = new();
        private readonly SwitchableAchievementPublicationSink _router;
        private readonly ProductAchievementCoordinator _coordinator;

        private object _sessionIdentity;
        private IAchievementPublicationSink _publicationSink;
        private bool _disposed;

        internal ProductAchievementPublicationSessionController(
            SwitchableAchievementPublicationSink router,
            ProductAchievementCoordinator coordinator)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public bool TryAttach(
            object sessionIdentity,
            IAchievementPublicationSink publicationSink)
        {
            if (sessionIdentity == null)
            {
                throw new ArgumentNullException(nameof(sessionIdentity));
            }

            if (publicationSink == null)
            {
                throw new ArgumentNullException(nameof(publicationSink));
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return false;
                }

                if (_sessionIdentity != null)
                {
                    return ReferenceEquals(_sessionIdentity, sessionIdentity) &&
                           ReferenceEquals(_publicationSink, publicationSink);
                }

                if (!_router.TryAttach(sessionIdentity, publicationSink))
                {
                    return false;
                }

                _sessionIdentity = sessionIdentity;
                _publicationSink = publicationSink;
            }

            if (_coordinator.ReconcileAllEarnedForNewPublicationSession())
            {
                return true;
            }

            lock (_gate)
            {
                if (ReferenceEquals(_sessionIdentity, sessionIdentity) &&
                    ReferenceEquals(_publicationSink, publicationSink))
                {
                    _router.TryDetach(sessionIdentity, publicationSink);
                    _sessionIdentity = null;
                    _publicationSink = null;
                }
            }

            return false;
        }

        public void Detach(
            object sessionIdentity,
            IAchievementPublicationSink publicationSink)
        {
            lock (_gate)
            {
                if (_disposed ||
                    !ReferenceEquals(_sessionIdentity, sessionIdentity) ||
                    !ReferenceEquals(_publicationSink, publicationSink))
                {
                    return;
                }

                _router.TryDetach(sessionIdentity, publicationSink);
                _sessionIdentity = null;
                _publicationSink = null;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _sessionIdentity = null;
                _publicationSink = null;
            }
        }
    }

    internal static class ProductAchievementPublicationSessionHandoff
    {
        private static readonly object Gate = new();

        private static IProductAchievementPublicationSessionController _controller;
        private static object _steamSessionIdentity;
        private static IAchievementPublicationSink _steamPublicationSink;

        internal static bool TryRegisterController(
            IProductAchievementPublicationSessionController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            lock (Gate)
            {
                if (_controller != null)
                {
                    return ReferenceEquals(_controller, controller);
                }

                _controller = controller;
                if (_steamSessionIdentity == null)
                {
                    return true;
                }

                if (controller.TryAttach(_steamSessionIdentity, _steamPublicationSink))
                {
                    return true;
                }

                _controller = null;
                return false;
            }
        }

        internal static bool TryRegisterSteamSession(
            object sessionIdentity,
            IAchievementPublicationSink publicationSink)
        {
            if (sessionIdentity == null)
            {
                throw new ArgumentNullException(nameof(sessionIdentity));
            }

            if (publicationSink == null)
            {
                throw new ArgumentNullException(nameof(publicationSink));
            }

            lock (Gate)
            {
                if (_steamSessionIdentity != null)
                {
                    return ReferenceEquals(_steamSessionIdentity, sessionIdentity) &&
                           ReferenceEquals(_steamPublicationSink, publicationSink);
                }

                _steamSessionIdentity = sessionIdentity;
                _steamPublicationSink = publicationSink;
                if (_controller == null)
                {
                    return true;
                }

                if (_controller.TryAttach(sessionIdentity, publicationSink))
                {
                    return true;
                }

                _steamSessionIdentity = null;
                _steamPublicationSink = null;
                return false;
            }
        }

        internal static void ClearController(
            IProductAchievementPublicationSessionController controller)
        {
            lock (Gate)
            {
                if (ReferenceEquals(_controller, controller))
                {
                    _controller = null;
                }
            }
        }

        internal static void ClearSteamSession(
            object sessionIdentity,
            IAchievementPublicationSink publicationSink)
        {
            lock (Gate)
            {
                if (!ReferenceEquals(_steamSessionIdentity, sessionIdentity) ||
                    !ReferenceEquals(_steamPublicationSink, publicationSink))
                {
                    return;
                }

                _controller?.Detach(sessionIdentity, publicationSink);
                _steamSessionIdentity = null;
                _steamPublicationSink = null;
            }
        }

        internal static void ResetForSubsystemRegistration()
        {
            lock (Gate)
            {
                _controller = null;
                _steamSessionIdentity = null;
                _steamPublicationSink = null;
            }
        }

        internal static void ResetForTests()
        {
            ResetForSubsystemRegistration();
        }
    }
}
