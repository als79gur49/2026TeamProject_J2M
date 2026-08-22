using System;
using Game.Feature.Stages;
using Game.Product.Achievements.CampaignIntegration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Product.Achievements.Composition
{
    internal sealed class ProductAchievementApplicationLifetimeOwner : IDisposable
    {
        private readonly ISavePathProvider _savePathProvider;
        private readonly SwitchableAchievementPublicationSink _publicationSink;
        private readonly Func<string, IAchievementPublicationSink, IProductAchievementHostLifetime>
            _hostFactory;
        private readonly Func<ICampaignSaveSlotStore> _campaignSaveSlotStoreFactory;
        private readonly Func<EditorDirectPlayContext> _directPlayContextProvider;
        private readonly Func<CampaignStageSequenceResolver> _sequenceResolverFactory;

        private IProductAchievementHostLifetime _host;
        private IProductAchievementEarningSink _registeredEarningSink;
        private NormalCampaignCompletionAchievementStartupReconciler _startupReconciler;
        private ProductAchievementPublicationSessionController _publicationSessionController;
        private bool _initializeAttempted;
        private bool _initializeResult;
        private bool _startupReconciliationAttempted;
        private bool _disposed;

        public ProductAchievementApplicationLifetimeOwner(
            ISavePathProvider savePathProvider,
            IAchievementPublicationSink publicationSink = null,
            Func<string, IAchievementPublicationSink, IProductAchievementHostLifetime> hostFactory = null,
            Func<ICampaignSaveSlotStore> campaignSaveSlotStoreFactory = null,
            Func<EditorDirectPlayContext> directPlayContextProvider = null,
            Func<CampaignStageSequenceResolver> sequenceResolverFactory = null)
        {
            _savePathProvider = savePathProvider ??
                throw new ArgumentNullException(nameof(savePathProvider));
            _publicationSink = new SwitchableAchievementPublicationSink(publicationSink);
            _hostFactory = hostFactory ?? CreateDefaultHost;
            _campaignSaveSlotStoreFactory = campaignSaveSlotStoreFactory;
            _directPlayContextProvider = directPlayContextProvider ??
                EditorDirectPlayContextStore.GetCurrentOrNone;
            _sequenceResolverFactory = sequenceResolverFactory;
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
            _registeredEarningSink = _host.EarningSink ??
                throw new InvalidOperationException(
                    "Product achievement composition returned a host without an earning sink.");
            if (!ProductAchievementEarningSinkHandoff.TryRegister(_registeredEarningSink))
            {
                _initializeResult = false;
                return false;
            }

            _initializeResult = _host.Initialize();
            if (_initializeResult && _host is ProductAchievementApplicationHost applicationHost)
            {
                _publicationSessionController = new ProductAchievementPublicationSessionController(
                    _publicationSink,
                    applicationHost.Coordinator);
                if (!ProductAchievementPublicationSessionHandoff.TryRegisterController(
                    _publicationSessionController))
                {
                    _publicationSessionController.Dispose();
                    _publicationSessionController = null;
                    _initializeResult = false;
                }
            }

            return _initializeResult;
        }

        public NormalCampaignCompletionAchievementResult ReconcileNormalCampaignCompletionReceipt()
        {
            if (_startupReconciliationAttempted)
            {
                return NormalCampaignCompletionAchievementResult.AlreadyReconciled;
            }

            _startupReconciliationAttempted = true;
            if (_disposed || !_initializeResult || _campaignSaveSlotStoreFactory == null)
            {
                return NormalCampaignCompletionAchievementResult.ProductUnavailable;
            }

            try
            {
                var directPlayContext = _directPlayContextProvider();
                if (directPlayContext.Mode != EditorDirectPlayMode.None)
                {
                    return NormalCampaignCompletionAchievementResult.DirectPlayExcluded;
                }

                var integration = new NormalCampaignCompletionAchievementIntegration(
                    _registeredEarningSink);
                _startupReconciler = new NormalCampaignCompletionAchievementStartupReconciler(
                    integration,
                    new CampaignStageAchievementIntegration(_registeredEarningSink));
                var sequenceResolver = _sequenceResolverFactory?.Invoke();
                if (sequenceResolver == null)
                {
                    return NormalCampaignCompletionAchievementResult.ProfileUnavailable;
                }

                return _startupReconciler.Reconcile(
                    _campaignSaveSlotStoreFactory(),
                    sequenceResolver,
                    directPlayContext);
            }
            catch
            {
                return NormalCampaignCompletionAchievementResult.ExceptionContained;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ProductAchievementPublicationSessionHandoff.ClearController(
                _publicationSessionController);
            _publicationSessionController?.Dispose();
            ProductAchievementEarningSinkHandoff.Clear(_registeredEarningSink);
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

    internal static class ActiveSceneCampaignStageSequenceResolverFactory
    {
        internal static CampaignStageSequenceResolver ResolveOrNull()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogWarning(
                    "Product achievement startup reconciliation skipped: active scene is unavailable.");
                return null;
            }

            ICampaignStageSequenceResolverProvider candidate = null;
            var providerCount = 0;
            var roots = activeScene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    if (behaviours[behaviourIndex] is not ICampaignStageSequenceResolverProvider provider)
                    {
                        continue;
                    }

                    providerCount++;
                    candidate = provider;
                }
            }

            return ResolveCandidateOrNull(candidate, providerCount, Debug.LogWarning);
        }

        internal static CampaignStageSequenceResolver ResolveCandidateOrNull(
            ICampaignStageSequenceResolverProvider candidate,
            int providerCount,
            Action<string> warningSink)
        {
            warningSink ??= _ => { };
            if (providerCount != 1)
            {
                warningSink(
                    providerCount == 0
                        ? "Product achievement startup reconciliation skipped: active scene has no Campaign sequence resolver provider."
                        : $"Product achievement startup reconciliation skipped: active scene has {providerCount} Campaign sequence resolver providers; exactly one is required.");
                return null;
            }

            if (candidate == null ||
                !candidate.TryCreateCampaignStageSequenceResolver(out var resolver) ||
                resolver == null)
            {
                warningSink(
                    "Product achievement startup reconciliation skipped: the active-scene Campaign sequence provider could not supply its serialized resolver.");
                return null;
            }

            return resolver;
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
            ProductAchievementPublicationSessionHandoff.ResetForSubsystemRegistration();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeFirstScene()
        {
            if (_owner != null)
            {
                return;
            }

            _owner = new ProductAchievementApplicationLifetimeOwner(
                new ApplicationPersistentDataSavePathProvider(),
                campaignSaveSlotStoreFactory:
                    CampaignSaveCompositionProvider.CreateProductionProfileBacked,
                sequenceResolverFactory:
                    ActiveSceneCampaignStageSequenceResolverFactory.ResolveOrNull);
            RegisterQuitHandler();
            try
            {
                _owner.Initialize();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Product achievement initialization was contained: {exception.Message}");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ReconcileNormalCampaignCompletionAfterFirstSceneLoad()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            try
            {
                _owner?.ReconcileNormalCampaignCompletionReceipt();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Product achievement receipt reconciliation was contained: {exception.Message}");
            }
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
