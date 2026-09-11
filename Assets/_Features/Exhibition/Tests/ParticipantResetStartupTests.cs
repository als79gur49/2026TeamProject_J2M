using System;
using System.Collections.Generic;
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
    public sealed class ParticipantResetStartupTests
    {
        private SteamPlatformRuntime runtime;
        private Native native;
        private ProductAchievementCoordinator coordinator;
        private ProductAchievementPublicationSessionController controller;
        private Repository repository;
        private double clock;

        [SetUp]
        public void SetUp() { Reset(); clock = 0; }
        [TearDown]
        public void TearDown()
        {
            runtime?.Shutdown();
            controller?.Dispose(); coordinator?.Dispose();
            ParticipantResetMenuAccess.Register(null); Reset();
        }
        private static void Reset()
        {
            ProductAchievementPublicationSessionHandoff.ResetForTests();
            foreach (var pair in new[] { (typeof(SteamAchievementMaintenanceAccess), "Reset"),
                (typeof(ProductAchievementStartupControl), "Reset"), (typeof(CampaignSaveCompositionProvider), "ResetProductionSession") })
                pair.Item1.GetMethod(pair.Item2, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            var registry = typeof(Game.Platform.Runtime.PlatformRuntimeRegistry);
            registry.GetMethod("ResetForSubsystemRegistration", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }

        private void ComposeRuntime(bool publicationController = false)
        {
            if (publicationController)
            {
                repository = new Repository();
                var sink = new SwitchableAchievementPublicationSink();
                coordinator = new ProductAchievementCoordinator(repository, GameAchievementCatalog.Production, sink);
                Assert.That(coordinator.Initialize(), Is.True);
                controller = new ProductAchievementPublicationSessionController(sink, coordinator);
                Assert.That(ProductAchievementPublicationSessionHandoff.TryRegisterController(controller), Is.True);
            }
            native = new Native();
            runtime = new SteamPlatformRuntime(new SteamRuntimeDependencies(native, native), () => clock);
            Assert.That(runtime.Initialize().IsSuccess, Is.True);
        }

        [Test, Category("Integration")]
        public void PartialCompletedResetReturnFailsClosedBeforeOrdinaryComposition()
        {
            int ordinary = 0, completed = 0; Exception failure = null;
            ExhibitionApplication.InspectProcessStartup(new[] { "game", "-j2mCompletedParticipantReset", "request.json" },
                false, () => ordinary++, (value, error) => { completed++; failure = error; });
            Assert.That(ordinary, Is.Zero);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(failure, Is.Not.Null);
            Assert.That(ProductAchievementStartupControl.IsDeferred, Is.True);
        }

        [Test, Category("Integration")]
        public void OrdinaryStartupStillPublishesThroughSameBoundary()
        {
            ExhibitionApplication.InspectProcessStartup(new[] { "game" }, false, () => ComposeRuntime(true));
            runtime.Tick();
            Assert.That(native.SetCount, Is.EqualTo(1)); Assert.That(native.StoreCount, Is.EqualTo(1));
        }

        private sealed class Repository : IAchievementDocumentRepository
        {
            public int Saves;
            public AchievementDocumentLoadResult Load() => new AchievementDocumentLoadResult(AchievementDocumentLoadStatus.Loaded,
                new ProductAchievementDocument { EarnedAchievementIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
                    PendingAchievementPublicationIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value } }, "");
            public AchievementDocumentSaveResult Save(ProductAchievementDocument document) { Saves++; return AchievementDocumentSaveResult.Saved(); }
        }
        private sealed class Native : ISteamNativeApi, ISteamAchievementApi
        {
            public int InitCount, CallbackCount, SetCount, StoreCount, Queries, AchievementQueries;
            public bool CallbackError;
            private readonly string[] names = SteamAchievementMapping.Production.Entries.Select(e => e.ExpectedSteamApiName.Value).ToArray();
            public bool IsPacksizeCompatible() => true;
            public bool Initialize() { InitCount++; return true; }
            public void Shutdown() { }
            public void RunCallbacks() { CallbackCount++; if (CallbackError) throw new InvalidOperationException("callback-error"); }
            public uint GetAppId() => 5218360;
            public ulong GetSteamId() => 76561198000000000UL;
            public bool IsSteamIdValid() => true;
            public bool IsLoggedOn() => true;
            public uint GetNumAchievements() => (uint)names.Length;
            public string GetAchievementName(uint index) => names[index];
            public bool GetAchievement(string name, out bool achieved) { AchievementQueries++; achieved = false; return names.Contains(name); }
            public bool SetAchievement(string name) { SetCount++; return true; }
            public bool StoreStats() { StoreCount++; return true; }
            public void RegisterAchievementStoreCallbacks(Action<SteamStatsStoredObservation> stats, Action<SteamAchievementStoredObservation> achievement) { }
            public void DisposeAchievementStoreCallbacks() { }
        }
    }
}
