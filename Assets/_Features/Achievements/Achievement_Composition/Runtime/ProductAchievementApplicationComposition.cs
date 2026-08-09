using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Product.Achievements.Composition
{
    internal sealed class ProductAchievementApplicationLifetimeOwner : IDisposable
    {
        private readonly ISavePathProvider _savePathProvider;
        private readonly IAchievementPublicationSink _publicationSink;
        private readonly Func<string, IAchievementPublicationSink, IProductAchievementHostLifetime>
            _hostFactory;

        private IProductAchievementHostLifetime _host;
        private bool _initializeAttempted;
        private bool _initializeResult;
        private bool _disposed;

        public ProductAchievementApplicationLifetimeOwner(
            ISavePathProvider savePathProvider,
            IAchievementPublicationSink publicationSink = null,
            Func<string, IAchievementPublicationSink, IProductAchievementHostLifetime> hostFactory = null)
        {
            _savePathProvider = savePathProvider ??
                throw new ArgumentNullException(nameof(savePathProvider));
            _publicationSink = publicationSink ?? new UnavailableAchievementPublicationSink();
            _hostFactory = hostFactory ?? CreateDefaultHost;
        }

        public IProductAchievementEarningSink EarningSink => _host?.EarningSink;

        internal IProductAchievementHostLifetime Host => _host;

        public bool Initialize()
        {
            if (_disposed)
            {
                return false;
            }

            if (_initializeAttempted)
            {
                return _initializeResult;
            }

            _initializeAttempted = true;
            _host = _hostFactory(_savePathProvider.SaveRootPath, _publicationSink) ??
                throw new InvalidOperationException(
                    "Product achievement composition returned a null application host.");
            _initializeResult = _host.Initialize();
            return _initializeResult;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _host?.Dispose();
        }

        private static IProductAchievementHostLifetime CreateDefaultHost(
            string saveRootPath,
            IAchievementPublicationSink publicationSink)
        {
            return ProductAchievementApplicationHost.CreateForSaveRoot(
                saveRootPath,
                publicationSink);
        }
    }

    internal static class ProductAchievementRuntimeBootstrap
    {
        private static ProductAchievementApplicationLifetimeOwner _owner;
        private static bool _quitHandlerRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            UnregisterQuitHandler();
            _owner?.Dispose();
            _owner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeFirstScene()
        {
            if (_owner != null)
            {
                return;
            }

            _owner = new ProductAchievementApplicationLifetimeOwner(
                new ApplicationPersistentDataSavePathProvider());
            RegisterQuitHandler();
            _owner.Initialize();
        }

        private static void RegisterQuitHandler()
        {
            if (_quitHandlerRegistered)
            {
                return;
            }

            Application.quitting += DisposeAtApplicationQuit;
            _quitHandlerRegistered = true;
        }

        private static void UnregisterQuitHandler()
        {
            if (!_quitHandlerRegistered)
            {
                return;
            }

            Application.quitting -= DisposeAtApplicationQuit;
            _quitHandlerRegistered = false;
        }

        private static void DisposeAtApplicationQuit()
        {
            UnregisterQuitHandler();
            _owner?.Dispose();
        }
    }
}
