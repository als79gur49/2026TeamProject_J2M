using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Exhibition.Integration;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Platform.Steam;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements;
using Game.Product.Achievements.Composition;
using NUnit.Framework;
using UnityEngine;

namespace Game.Exhibition.Tests
{
    // Production startup latch, native lifecycle, publication registration and earned-ledger
    // reconciliation are real. Only native SDK calls and the local document store are fakes.
    public sealed class ParticipantPublicationStartupTests
    {
        private SteamPlatformRuntime runtime;
        private ProductAchievementCoordinator coordinator;
        private ProductAchievementPublicationSessionController controller;
        private Native lifecycle;
        private Achievements achievements;
        private Repository repository;

        [SetUp]
        public void SetUp()
        {
            runtime = null; coordinator = null; controller = null;
            ResetStatics();
        }

        [TearDown]
        public void TearDown()
        {
            runtime?.Shutdown();
            ProductAchievementPublicationSessionHandoff.ClearController(controller);
            controller?.Dispose();
            coordinator?.Dispose();
            ParticipantResetMenuAccess.Register(null);
            ResetStatics();
        }

        private static void ResetStatics()
        {
            ProductAchievementPublicationSessionHandoff.ResetForTests();
            InvokeReset(typeof(SteamAchievementMaintenanceAccess), "Reset");
            InvokeReset(typeof(ProductAchievementStartupControl), "Reset");
            InvokeReset(typeof(CampaignSaveCompositionProvider), "ResetProductionSession");
            InvokeReset(typeof(Game.Platform.Runtime.PlatformRuntimeRegistry), "ResetForSubsystemRegistration");
        }

        private static void InvokeReset(Type type, string method)
            => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinaryStartupReproducesEarnedLedgerPublication(bool pending)
        {
            var args = new[] { "game", "-j2mPlatformProvider", "steam" };
            ExhibitionApplication.InspectProcessStartup(args, false, () => { });
            AssertAutomaticBootstrapCalls(expectedStart: 1, expectedReconcile: 1);
            StartPublicationComposition(pending);
            Assert.That(achievements.SetCount, Is.EqualTo(1));
            Assert.That(achievements.StoreCount, Is.EqualTo(1));
            Assert.That(achievements.GetCount, Is.GreaterThan(0));
            Assert.That(ProductAchievementStartupControl.IsDeferred, Is.False);
        }

        [Test]
        public void BatchModeSuppressesAutomaticCampaignReconciliationInOrdinaryStartup()
        {
            int starts = 0, reconciliations = 0;
            ExhibitionApplication.InspectProcessStartup(new[] { "game" }, true, () => Assert.Fail("Batch participant composition"));
            ProductAchievementRuntimeBootstrap.RunAutomaticStart(() => starts++);
            ProductAchievementRuntimeBootstrap.RunAutomaticReconciliation(true, () => reconciliations++);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(reconciliations, Is.Zero);
        }

        [Test]
        public void RuntimeCallbacksPutParticipantInspectionBeforeAutomaticProductAndPlatformStartup()
        {
            AssertCallbackPhase(typeof(ExhibitionApplication), "InspectJournal", RuntimeInitializeLoadType.AfterAssembliesLoaded);
            AssertCallbackPhase(typeof(ProductAchievementRuntimeBootstrap), "InitializeBeforeFirstScene", RuntimeInitializeLoadType.BeforeSceneLoad);
            AssertCallbackPhase(typeof(ProductAchievementRuntimeBootstrap), "ReconcileCampaignStageAchievementsAfterFirstSceneLoad", RuntimeInitializeLoadType.AfterSceneLoad);
            var platformBootstrap = typeof(Game.Platform.Runtime.PlatformProviderSelection).Assembly.GetType("Game.Platform.Runtime.PlatformRuntimeBootstrap", true);
            AssertCallbackPhase(platformBootstrap, "RunAutomaticBootstrap", RuntimeInitializeLoadType.BeforeSceneLoad);
        }

        private static void AssertCallbackPhase(Type type, string method, RuntimeInitializeLoadType phase)
        {
            var callback = type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            var attribute = callback.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.loadType, Is.EqualTo(phase));
        }

        private static void AssertAutomaticBootstrapCalls(int expectedStart, int expectedReconcile)
        {
            int starts = 0, reconciliations = 0;
            // These are the actual policies called from BeforeSceneLoad/AfterSceneLoad.
            // Injecting the operation prevents any access to the user's real save root.
            ProductAchievementRuntimeBootstrap.RunAutomaticStart(() => starts++);
            ProductAchievementRuntimeBootstrap.RunAutomaticReconciliation(false, () => reconciliations++);
            Assert.That(starts, Is.EqualTo(expectedStart));
            Assert.That(reconciliations, Is.EqualTo(expectedReconcile));
        }


        [Test]
        public void CompositionFailureBeforeRuntimeHoldsPublicationAndCampaignAccess()
        {
            Assert.Throws<IOException>(() => ExhibitionApplication.InspectProcessStartup(new[] { "game" }, false,
                () => throw new IOException("composition failed")));
            Assert.That(AllDeferred(), Is.True);
            Assert.Throws<InvalidOperationException>(() => CampaignSaveCompositionProvider.CreateProductionProfileBacked());
            AssertAutomaticBootstrapCalls(0, 0);
            StartPublicationComposition(true);
            Assert.That(lifecycle.InitializeCount, Is.EqualTo(1));
            Assert.That(lifecycle.CallbackCount, Is.EqualTo(2));
            Assert.That(achievements.SetCount + achievements.StoreCount + achievements.GetCount, Is.Zero);
        }

        private static bool AllDeferred()
            => ProductAchievementStartupControl.IsDeferred && SteamAchievementMaintenanceAccess.IsDeferred;

        private void StartPublicationComposition(bool pending)
        {
            repository = new Repository(pending);
            var sink = new SwitchableAchievementPublicationSink();
            coordinator = new ProductAchievementCoordinator(repository, GameAchievementCatalog.Production, sink);
            Assert.That(coordinator.Initialize(), Is.True);
            controller = new ProductAchievementPublicationSessionController(sink, coordinator);
            // Registering even while product startup is deferred stress-tests the separate
            // Steam publication gate; this deliberately cannot hide a missing Steam latch.
            Assert.That(ProductAchievementPublicationSessionHandoff.TryRegisterController(controller), Is.True);
            lifecycle = new Native();
            achievements = new Achievements();
            runtime = new SteamPlatformRuntime(new SteamRuntimeDependencies(lifecycle, achievements),
                monotonicSeconds: () => 0d);
            Assert.That(runtime.Initialize().IsSuccess, Is.True);
            runtime.Tick();
            runtime.Tick();
        }

        private sealed class Repository : IAchievementDocumentRepository
        {
            private readonly bool pending;
            public int SaveCount;
            public Repository(bool pending) => this.pending = pending;
            public AchievementDocumentLoadResult Load() => new AchievementDocumentLoadResult(
                AchievementDocumentLoadStatus.Loaded, new ProductAchievementDocument
                {
                    EarnedAchievementIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
                    PendingAchievementPublicationIds = pending ? new[] { GameAchievementIds.CampaignLevel4Clear.Value } : Array.Empty<string>()
                }, string.Empty);
            public AchievementDocumentSaveResult Save(ProductAchievementDocument document)
            { SaveCount++; return AchievementDocumentSaveResult.Saved(); }
        }

        private sealed class Native : ISteamNativeApi
        {
            public int InitializeCount, CallbackCount;
            public bool IsPacksizeCompatible() => true;
            public bool Initialize() { InitializeCount++; return true; }
            public void RunCallbacks() => CallbackCount++;
            public void Shutdown() { }
            public uint GetAppId() => 5218360;
            public bool IsSteamIdValid() => true;
            public bool IsLoggedOn() => true;
        }

        private sealed class Achievements : ISteamAchievementApi
        {
            public int SetCount, StoreCount, GetCount;
            public readonly string Name;
            public readonly string[] Names = SteamAchievementMapping.Production.Entries.Select(entry => entry.ExpectedSteamApiName.Value).ToArray();
            public Achievements()
            {
                SteamAchievementMapping.Production.TryGetExpectedSteamApiName(GameAchievementIds.CampaignLevel4Clear, out var name);
                Name = name.Value;
            }
            public uint GetNumAchievements() => (uint)Names.Length;
            public string GetAchievementName(uint index) => Names[index];
            public bool GetAchievement(string name, out bool achieved) { GetCount++; achieved = false; return Array.IndexOf(Names, name) >= 0; }
            public bool SetAchievement(string name) { SetCount++; return name == Name; }
            public bool StoreStats() { StoreCount++; return true; }
            public void RegisterAchievementStoreCallbacks(Action<SteamStatsStoredObservation> stats, Action<SteamAchievementStoredObservation> achievement) { }
            public void DisposeAchievementStoreCallbacks() { }
        }
    }
}
