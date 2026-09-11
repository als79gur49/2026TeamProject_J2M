using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Platform.Runtime;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements;
using Game.Product.Achievements.Composition;
using Object = UnityEngine.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Platform.Steam.Tests.PlayMode
{
    public sealed class SteamPlatformRuntimePlayModeTests
    {
        private static readonly BindingFlags InternalStatic =
            BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly BindingFlags InternalInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private ProductAchievementCoordinator productCoordinator;
        private ProductAchievementPublicationSessionController productController;

        private void ResetProduct()
        {
            if (productController != null)
            {
                ProductAchievementPublicationSessionHandoff.ClearController(productController);
                productController.Dispose();
                productController = null;
            }
            productCoordinator?.Dispose();
            productCoordinator = null;
            ProductAchievementPublicationSessionHandoff.ResetForTests();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return DestroyPlatformHosts();
            ResetProduct();
            ResetPlatformFoundation();
            AssertNoPlatformHosts();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyPlatformHosts();
            AssertNoPlatformHosts();
            ResetProduct();
            ResetPlatformFoundation();
        }

        [UnityTest]
        public IEnumerator FoundationHost_InitializesTicksAndShutsDownExactlyOnce()
        {
            var native = new CountingNativeApi();
            var host = BootstrapWithFactory(new SteamPlatformRuntimeFactory(() => native));

            yield return null;
            yield return null;

            AssertSinglePlatformHost(host, SteamPlatformRuntime.ProviderId);
            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.GreaterThan(0));
            Assert.That(GetHostProviderId(host), Is.EqualTo(SteamPlatformRuntime.ProviderId));

            InvokeHostShutdown(host);
            Object.Destroy(((Component)host).gameObject);
            yield return null;

            Assert.That(native.ShutdownCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailedProvider_DoesNotCrashFoundationLoopOrPumpCallbacks()
        {
            var native = new CountingNativeApi { InitializeResult = false };
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'steam' initialization failed: " +
                "InitializationReturnedFalse: Native Steam initialization returned false.");
            var host = BootstrapWithFactory(new SteamPlatformRuntimeFactory(() => native));

            yield return null;
            yield return null;

            AssertSinglePlatformHost(host, SteamPlatformRuntime.ProviderId);
            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.Zero);

            Object.Destroy(((Component)host).gameObject);
            yield return null;

            Assert.That(native.ShutdownCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CallbackFault_ReleasesLifecycleAndDoesNotPumpOrShutdownTwice()
        {
            var native = new CountingNativeApi();
            var host = BootstrapWithFactory(new SteamPlatformRuntimeFactory(() => native));

            yield return null;
            AssertSinglePlatformHost(host, SteamPlatformRuntime.ProviderId);
            Assert.That(native.CallbackCount, Is.GreaterThan(0));
            native.CallbackException = new System.InvalidOperationException("callback fault");
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'steam' became unavailable during tick: " +
                "CallbackException: InvalidOperationException with message 'callback fault'; further ticks are disabled.");

            yield return null;
            var callbacksAfterFault = native.CallbackCount;
            yield return null;
            InvokeHostShutdown(host);
            Object.Destroy(((Component)host).gameObject);
            yield return null;

            Assert.That(native.CallbackCount, Is.EqualTo(callbacksAfterFault));
            Assert.That(native.ShutdownCount, Is.EqualTo(1));
            Assert.That(GetHostTickEnabled(host), Is.False);
        }

        [UnityTest]
        public IEnumerator LocalFoundationHost_HasNoSteamCallbackOwnership()
        {
            var native = new CountingNativeApi();
            var host = BootstrapWithFactory(null);

            yield return null;
            yield return null;

            AssertSinglePlatformHost(host, PlatformProviderId.Local);
            Assert.That(GetHostProviderId(host), Is.EqualTo(PlatformProviderId.Local));
            Assert.That(native.InitializeCount, Is.Zero);
            Assert.That(native.CallbackCount, Is.Zero);
            Assert.That(native.ShutdownCount, Is.Zero);

            Object.Destroy(((Component)host).gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyBeforeReset_RemovesLocalHostBeforeSteamBootstrap()
        {
            var localHost = BootstrapWithFactory(null);
            yield return null;
            AssertSinglePlatformHost(localHost, PlatformProviderId.Local);

            yield return DestroyPlatformHosts();
            AssertNoPlatformHosts();
            ResetPlatformFoundation();

            var native = new CountingNativeApi();
            var steamHost = BootstrapWithFactory(
                new SteamPlatformRuntimeFactory(() => native));
            yield return null;

            AssertSinglePlatformHost(steamHost, SteamPlatformRuntime.ProviderId);
            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator RepeatedSteamBootstrapCycles_DoNotAccumulateOrphanHosts()
        {
            for (var cycle = 0; cycle < 2; cycle++)
            {
                var native = new CountingNativeApi();
                var host = BootstrapWithFactory(
                    new SteamPlatformRuntimeFactory(() => native));
                yield return null;

                AssertSinglePlatformHost(host, SteamPlatformRuntime.ProviderId);
                Assert.That(native.InitializeCount, Is.EqualTo(1));

                yield return DestroyPlatformHosts();
                AssertNoPlatformHosts();
                Assert.That(native.ShutdownCount, Is.EqualTo(1));
                ResetPlatformFoundation();
            }
        }

        [UnityTest]
        public IEnumerator ProductPublication_ReusesHostCallbackPumpAndSingleAdapterLifecycle()
        {
            var repository = new MemoryRepository(new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
                PendingAchievementPublicationIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
            });
            var router = new SwitchableAchievementPublicationSink();
            productCoordinator = new ProductAchievementCoordinator(
                repository, GameAchievementCatalog.Production, router);
            Assert.That(productCoordinator.Initialize(), Is.True);
            productController = new ProductAchievementPublicationSessionController(router, productCoordinator);
            Assert.That(ProductAchievementPublicationSessionHandoff.TryRegisterController(productController), Is.True);

            var adapter = new CountingAchievementAdapter();
            var factory = new SteamPlatformRuntimeFactory(
                () => new SteamRuntimeDependencies(adapter, adapter));
            var host = BootstrapWithFactory(factory);
            Assert.That(productCoordinator.GetSnapshot().InFlightCount, Is.EqualTo(1));

            yield return null;
            yield return null;

            AssertSinglePlatformHost(host, SteamPlatformRuntime.ProviderId);
            Assert.That(adapter.InitializeCount, Is.EqualTo(1));
            Assert.That(adapter.CallbackCount, Is.GreaterThan(0));
            Assert.That(adapter.MaximumPumpsPerFrame, Is.EqualTo(1));
            Assert.That(adapter.AchievementCallbackRegistrationCount, Is.EqualTo(1));
            Assert.That(adapter.SetAchievementCount, Is.EqualTo(1));
            Assert.That(adapter.StoreStatsCount, Is.EqualTo(1));
            Assert.That(adapter.GetAchievementCount, Is.EqualTo(1));
            Assert.That(productCoordinator.GetSnapshot().InFlightCount, Is.Zero);
            Assert.That(productCoordinator.GetSnapshot().PendingAchievementPublicationIds.Count, Is.EqualTo(1));

            InvokeHostShutdown(host);
            Object.Destroy(((Component)host).gameObject);
            yield return null;

            Assert.That(adapter.AchievementCallbackDisposeCount, Is.EqualTo(1));
            Assert.That(adapter.ShutdownCount, Is.EqualTo(1));
            Assert.That(adapter.CallOrder, Is.EqualTo(new[] { "achievement-dispose", "native-shutdown" }));
        }

        private static object BootstrapWithFactory(IPlatformRuntimeFactory factory)
        {
            if (factory != null)
            {
                var configureOverride = GetBootstrapType().GetMethod(
                    "TryConfigureTestOverride",
                    InternalStatic,
                    null,
                    new[]
                    {
                        typeof(bool),
                        typeof(IPlatformRuntimeFactory),
                        typeof(string).MakeByRefType(),
                    },
                    null);
                Assert.That(configureOverride, Is.Not.Null);
                var arguments = new object[] { false, factory, null };
                var accepted = (bool)configureOverride.Invoke(null, arguments);
                Assert.That(accepted, Is.True, arguments[2] as string);
            }

            var bootstrapType = GetBootstrapType();
            var bootstrap = bootstrapType.GetMethod("BootstrapNowForTests", InternalStatic);
            Assert.That(bootstrap, Is.Not.Null);
            var host = bootstrap.Invoke(null, null);
            Assert.That(host, Is.Not.Null);
            return host;
        }

        private static PlatformProviderId GetHostProviderId(object host)
        {
            var providerProperty = host.GetType().GetProperty("ProviderId", InternalInstance);
            Assert.That(providerProperty, Is.Not.Null);
            return (PlatformProviderId)providerProperty.GetValue(host);
        }

        private static void InvokeHostShutdown(object host)
        {
            var shutdown = host.GetType().GetMethod("ShutdownOnce", InternalInstance);
            Assert.That(shutdown, Is.Not.Null);
            shutdown.Invoke(host, null);
        }

        private static bool GetHostTickEnabled(object host)
        {
            var tickEnabled = host.GetType().GetProperty("TickEnabled", InternalInstance);
            Assert.That(tickEnabled, Is.Not.Null);
            return (bool)tickEnabled.GetValue(host);
        }

        private static IEnumerator DestroyPlatformHosts()
        {
            var hostType = typeof(LocalPlatformRuntime).Assembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeApplicationHost",
                throwOnError: true);
            var hosts = Object.FindObjectsByType(
                hostType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                Object.Destroy(((Component)host).gameObject);
            }

            if (hosts.Length > 0)
            {
                yield return null;
            }
        }

        private static void AssertSinglePlatformHost(
            object expectedHost,
            PlatformProviderId expectedProviderId)
        {
            var hosts = FindPlatformHosts();
            Assert.That(hosts, Has.Length.EqualTo(1));
            Assert.That(hosts[0], Is.SameAs(expectedHost));
            Assert.That(GetHostProviderId(hosts[0]), Is.EqualTo(expectedProviderId));
        }

        private static void AssertNoPlatformHosts()
        {
            Assert.That(FindPlatformHosts(), Is.Empty);
        }

        private static Object[] FindPlatformHosts()
        {
            var hostType = typeof(LocalPlatformRuntime).Assembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeApplicationHost",
                throwOnError: true);
            return Object.FindObjectsByType(
                hostType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static void ResetPlatformFoundation()
        {
            var reset = GetBootstrapType().GetMethod(
                "ResetSubsystemStateForTests",
                InternalStatic,
                null,
                System.Type.EmptyTypes,
                null);
            Assert.That(reset, Is.Not.Null);
            reset.Invoke(null, null);
        }

        private static System.Type GetBootstrapType()
        {
            return typeof(LocalPlatformRuntime).Assembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeBootstrap",
                throwOnError: true);
        }

        private sealed class CountingNativeApi : ISteamNativeApi
        {
            internal bool InitializeResult { get; set; } = true;
            internal int InitializeCount { get; private set; }
            internal int CallbackCount { get; private set; }
            internal int ShutdownCount { get; private set; }

            internal System.Exception CallbackException { get; set; }

            public bool IsPacksizeCompatible() => true;

            public bool Initialize()
            {
                InitializeCount++;
                return InitializeResult;
            }

            public void RunCallbacks()
            {
                CallbackCount++;
                if (CallbackException != null)
                {
                    throw CallbackException;
                }
            }

            public void Shutdown()
            {
                ShutdownCount++;
            }

            public uint GetAppId() => 480;

            public bool IsSteamIdValid() => true;

            public bool IsLoggedOn() => true;
        }

        private sealed class CountingAchievementAdapter :
            ISteamNativeApi,
            ISteamAchievementApi
        {
            private System.Action<SteamStatsStoredObservation> statsObserver;
            private System.Action<SteamAchievementStoredObservation> achievementObserver;
            private bool callbacksRaised;
            private int pumpFrame = -1;
            private int pumpsThisFrame;
            private static string ExpectedName
            {
                get
                {
                    SteamAchievementMapping.Production.TryGetExpectedSteamApiName(
                        GameAchievementIds.CampaignLevel4Clear, out var name);
                    return name.Value;
                }
            }

            internal int MaximumPumpsPerFrame { get; private set; }
            internal int AchievementCallbackRegistrationCount { get; private set; }
            internal List<string> CallOrder { get; } = new List<string>();

            internal int InitializeCount { get; private set; }
            internal int CallbackCount { get; private set; }
            internal int ShutdownCount { get; private set; }
            internal int AchievementCallbackDisposeCount { get; private set; }
            internal int GetAchievementCount { get; private set; }
            internal int SetAchievementCount { get; private set; }
            internal int StoreStatsCount { get; private set; }

            public bool IsPacksizeCompatible() => true;

            public bool Initialize()
            {
                InitializeCount++;
                return true;
            }

            public void RunCallbacks()
            {
                CallbackCount++;
                if (pumpFrame != Time.frameCount)
                {
                    pumpFrame = Time.frameCount;
                    pumpsThisFrame = 0;
                }
                MaximumPumpsPerFrame = Math.Max(MaximumPumpsPerFrame, ++pumpsThisFrame);
                if (callbacksRaised)
                {
                    return;
                }

                callbacksRaised = true;
                achievementObserver?.Invoke(new SteamAchievementStoredObservation(
                    4242,
                    ExpectedName,
                    isFullUnlock: true));
                statsObserver?.Invoke(new SteamStatsStoredObservation(
                    4242,
                    SteamCallbackResult.Ok));
            }

            public void Shutdown()
            {
                ShutdownCount++;
                CallOrder.Add("native-shutdown");
            }

            public uint GetAppId() => 4242;
            public bool IsSteamIdValid() => true;
            public bool IsLoggedOn() => true;

            public uint GetNumAchievements() => 1;
            public string GetAchievementName(uint index) => ExpectedName;

            public bool GetAchievement(string achievementName, out bool achieved)
            {
                GetAchievementCount++;
                achieved = false;
                return true;
            }

            public bool SetAchievement(string achievementName)
            {
                SetAchievementCount++;
                return true;
            }

            public bool StoreStats()
            {
                StoreStatsCount++;
                return true;
            }

            public void RegisterAchievementStoreCallbacks(
                System.Action<SteamStatsStoredObservation> statsStoredObserver,
                System.Action<SteamAchievementStoredObservation> achievementStoredObserver)
            {
                AchievementCallbackRegistrationCount++;
                statsObserver = statsStoredObserver;
                achievementObserver = achievementStoredObserver;
            }

            public void DisposeAchievementStoreCallbacks()
            {
                AchievementCallbackDisposeCount++;
                CallOrder.Add("achievement-dispose");
                statsObserver = null;
                achievementObserver = null;
            }
        }

        private sealed class MemoryRepository : IAchievementDocumentRepository
        {
            private ProductAchievementDocument _document;

            internal MemoryRepository(ProductAchievementDocument document)
            {
                _document = Clone(document);
            }

            internal int SaveCount { get; private set; }

            public AchievementDocumentLoadResult Load()
            {
                return new AchievementDocumentLoadResult(
                    AchievementDocumentLoadStatus.Loaded,
                    Clone(_document),
                    string.Empty);
            }

            public AchievementDocumentSaveResult Save(ProductAchievementDocument document)
            {
                SaveCount++;
                _document = Clone(document);
                return AchievementDocumentSaveResult.Saved();
            }

            private static ProductAchievementDocument Clone(ProductAchievementDocument document)
            {
                return new ProductAchievementDocument
                {
                    SchemaVersion = document.SchemaVersion,
                    EarnedAchievementIds = (string[])document.EarnedAchievementIds.Clone(),
                    PendingAchievementPublicationIds =
                        (string[])document.PendingAchievementPublicationIds.Clone(),
                };
            }
        }
    }
}
