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
    public sealed class OverlayObservationStartupTests
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
            foreach (var panel in Resources.FindObjectsOfTypeAll<OverlayHandoffObservationPresentation>()) UnityEngine.Object.DestroyImmediate(panel.gameObject);
            ParticipantResetMenuAccess.Register(null); Reset();
        }
        private static void Reset()
        {
            ProductAchievementPublicationSessionHandoff.ResetForTests();
            foreach (var pair in new[] { (typeof(SteamOverlayObservationAccess), "Reset"), (typeof(SteamAchievementMaintenanceAccess), "Reset"),
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
            runtime = new SteamPlatformRuntime(new SteamRuntimeDependencies(native, native), false, false, () => clock, _ => { });
            Assert.That(runtime.Initialize().IsSuccess, Is.True);
        }

        [TestCase(false), TestCase(true), Category("Integration")]
        public void V3MissingOrMalformedInputBlocksBeforeNativeAndNeverResumes(bool malformed)
        {
            string[] args = malformed ? new[] { "game", "-j2mOverlayV3Role", "bad" } : new[] { "game" };
            Assert.That(ExhibitionApplication.InspectV3Startup(args, true, true, false), Is.True);
            int starts = 0;
            ProductAchievementRuntimeBootstrap.RunAutomaticStart(() => starts++);
            Assert.That(starts, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => CampaignSaveCompositionProvider.RequireProductionWritesAllowed());
            native = new Native();
            runtime = new SteamPlatformRuntime(new SteamRuntimeDependencies(native, native), false, false, () => clock, _ => { });
            Assert.Throws<InvalidOperationException>(() => runtime.Initialize());
            Assert.That(native.InitCount + native.SetCount + native.StoreCount, Is.Zero);
            Assert.That(ProductAchievementStartupControl.StartDeferredServices(), Is.False);
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
        }

        [Test, Category("Integration")]
        public void ValidV3InOrdinaryBuildStopsBeforeFilesOrNative()
        {
            string[] args = { "game", "-j2mOverlayV3Role", "OriginObserver", "-j2mOverlayV3Owner", "SteamDelegated",
                "-j2mPlatformProvider", "steam" };
            Assert.That(ExhibitionApplication.InspectV3Startup(args, false, true, false), Is.True);
            Assert.That(((OverlayHandoffObservation)ParticipantResetMenuAccess.Current).Error, Does.Contain("CompiledObservationBuildRequired"));
            Assert.That(SteamOverlayObservationAccess.NativeStartupAttempted, Is.False);
        }

        [Test, Category("Integration")]
        public void V3LateEntryPreservesNativeHistoryAndRevokesWriters()
        {
            ComposeRuntime(true); runtime.Tick();
            int writes = native.SetCount + native.StoreCount + repository.Saves;
            Assert.That(ExhibitionApplication.InspectV3Startup(new string[0], true, true, false), Is.True);
            Assert.That(((OverlayHandoffObservation)ParticipantResetMenuAccess.Current).Error, Does.Contain("StartupAlreadyStarted"));
            Assert.That(SteamOverlayObservationAccess.NativeStartupAttempted, Is.True);
            runtime.Tick();
            Assert.That(native.SetCount + native.StoreCount + repository.Saves, Is.EqualTo(writes));
        }

        private static string[] Args(bool child = false) => child
            ? new[] { "game", OverlayHandoffObservationOptions.Context, "context", OverlayHandoffObservationOptions.Request, "request", "-j2mPlatformProvider", "steam" }
            : new[] { "game", OverlayHandoffObservationOptions.Initial, "config", "-j2mPlatformProvider", "steam" };

        [Test, Category("Integration")]
        public void PartialCompletedResetReturnFailsClosedBeforeOrdinaryComposition()
        {
            int ordinary = 0, completed = 0; Exception failure = null;
            ExhibitionApplication.InspectProcessStartup(new[] { "game", "-j2mCompletedParticipantReset", "request.json" },
                true, false, _ => ordinary++, (value, error) => { completed++; failure = error; });
            Assert.That(ordinary, Is.Zero);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(failure, Is.Not.Null);
            Assert.That(ProductAchievementStartupControl.IsDeferred, Is.True);
        }

        [TestCase(false), TestCase(true), Category("Integration")]
        public void BatchEntryRejectsObservationBeforeOrdinaryStartupWithoutFileComposition(bool mixed)
        {
            var args = mixed ? Args().Concat(new[] { "-j2mResetOverlayTrial", "conflict" }).ToArray() : Args();
            int composed = 0, starts = 0;
            ExhibitionApplication.InspectProcessStartup(args, true, true, _ => composed++);
            ProductAchievementRuntimeBootstrap.RunAutomaticStart(() => starts++);
            Assert.That(composed + starts, Is.Zero);
            Assert.That(ProductAchievementStartupControl.ObservationOnly && SteamOverlayObservationAccess.Requested, Is.True);
            Assert.That(ParticipantResetMenuAccess.Current, Is.TypeOf<OverlayHandoffObservation>());
            Assert.That(((OverlayHandoffObservation)ParticipantResetMenuAccess.Current).Stage, Is.EqualTo(OverlayObservationStage.Failed));
            Assert.Throws<InvalidOperationException>(() => CampaignSaveCompositionProvider.RequireProductionWritesAllowed());
            ComposeRuntime(true); runtime.Tick();
            Assert.That(native.SetCount + native.StoreCount + repository.Saves, Is.Zero);
        }

        [Test, Category("Integration")]
        public async System.Threading.Tasks.Task CallbackFaultStopsFlowAndBlocksHandoff()
        {
            ExhibitionApplication.InspectObservationStartup(Args(), true, _ => { });
            ComposeRuntime(); runtime.Tick();
            // Read the real runtime failure property without constructing its Windows/save/filesystem owners.
            var observation = (OverlayHandoffObservationRuntime)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(OverlayHandoffObservationRuntime));
            var bridge = new OverlayHandoffObservationTests.Runtime { ReadFailure = () => observation.ObservationFailure };
            var flow = new OverlayHandoffObservation(bridge, Game.Exhibition.RestartExperiment.OverlayObservationRole.OriginObserver);
            await flow.PrepareMenuAsync();
            native.CallbackError = true; runtime.Tick(); flow.Tick();
            Assert.That(flow.Stage, Is.EqualTo(OverlayObservationStage.Failed));
            Assert.That(flow.Error, Does.Contain("CallbackException"));
            Assert.That(flow.CanReport || flow.CanReplace, Is.False);
            int queries = native.Queries; runtime.Tick(); Assert.That(native.Queries, Is.EqualTo(queries));
        }

        [TestCase(false), TestCase(true), Category("Integration")]
        public void EarlyOriginAndChildLatchBlocksActualPublicationAndReadsIdentity(bool child)
        {
            bool composed = false;
            Assert.That(ExhibitionApplication.InspectObservationStartup(Args(child), true, _ =>
            {
                composed = true;
                Assert.That(ProductAchievementStartupControl.ObservationOnly && SteamOverlayObservationAccess.Requested, Is.True);
                ComposeRuntime(true);
            }), Is.True);
            runtime.Tick(); runtime.Tick();
            Assert.That(composed, Is.True);
            Assert.That(native.InitCount, Is.EqualTo(1)); Assert.That(native.CallbackCount, Is.EqualTo(2));
            Assert.That(native.SetCount + native.StoreCount + repository.Saves, Is.Zero);
            Assert.That(runtime.ReadObservationIdentity().SteamId, Is.EqualTo(76561198000000000UL));
            Assert.That(ProductAchievementStartupControl.StartDeferredServices(), Is.False);
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
            Assert.Throws<InvalidOperationException>(() => SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { }));
            CampaignSaveCompositionProvider.ReleaseProductionAccess();
            Assert.Throws<InvalidOperationException>(() => CampaignSaveCompositionProvider.CreateProductionProfileBacked());
            Assert.That(native.SetCount + native.StoreCount + repository.Saves, Is.Zero);
        }

        [TestCase(false), TestCase(true), Category("Integration")]
        public void MalformedOrUnsupportedOptInNeverComposesOrdinaryServices(bool enabled)
        {
            var args = Args().Concat(new[] { OverlayHandoffObservationOptions.Initial, "duplicate" }).ToArray();
            int compose = 0, starts = 0, reconciles = 0;
            ExhibitionApplication.InspectObservationStartup(args, enabled, _ => compose++);
            ProductAchievementRuntimeBootstrap.RunAutomaticStart(() => starts++);
            ProductAchievementRuntimeBootstrap.RunAutomaticReconciliation(false, () => reconciles++);
            Assert.That(compose + starts + reconciles, Is.Zero);
            Assert.That(ParticipantResetMenuAccess.Current, Is.TypeOf<OverlayHandoffObservation>());
            Assert.That(ProductAchievementStartupControl.ObservationOnly && SteamOverlayObservationAccess.Requested, Is.True);
        }

        [Test, Category("Integration")]
        public void ConfigurationCompositionFailureRetainsAllInhibitions()
        {
            ExhibitionApplication.InspectObservationStartup(Args(), true, _ => throw new System.IO.IOException("configuration read"));
            ComposeRuntime(true); runtime.Tick();
            Assert.That(native.SetCount + native.StoreCount + repository.Saves, Is.Zero);
            Assert.That(ParticipantResetMenuAccess.Current.Error, Does.Contain("configuration read"));
        }

        [Test, Category("Integration")]
        public void AlreadyStartedRuntimeRetainsPriorWriteHistoryAndRejectsObservation()
        {
            ComposeRuntime(true); runtime.Tick();
            Assert.That(native.SetCount, Is.GreaterThan(0), "Positive control must have actual prior publication.");
            int sets = native.SetCount, stores = native.StoreCount, saves = repository.Saves, compose = 0;
            ExhibitionApplication.InspectObservationStartup(Args(), true, _ => compose++);
            runtime.Tick(); runtime.Tick();
            var flow = (OverlayHandoffObservation)ParticipantResetMenuAccess.Current;
            Assert.That(flow.Stage, Is.EqualTo(OverlayObservationStage.StartupAlreadyStarted));
            Assert.That(compose, Is.Zero); Assert.That(flow.CanReplace, Is.False);
            Assert.That(native.SetCount, Is.EqualTo(sets)); Assert.That(native.StoreCount, Is.EqualTo(stores)); Assert.That(repository.Saves, Is.EqualTo(saves));
        }

        [Test, Category("Integration")]
        public void OrdinaryStartupStillPublishesThroughSameBoundary()
        {
            Assert.That(ExhibitionApplication.InspectObservationStartup(new[] { "game" }, true, _ => Assert.Fail()), Is.False);
            ComposeRuntime(true); runtime.Tick();
            Assert.That(native.SetCount, Is.EqualTo(1)); Assert.That(native.StoreCount, Is.EqualTo(1));
        }

        [Test, Category("Integration")]
        public void ObservationDoesNotQueryOverlayOrAchievementsAndKeepsCallbacks()
        {
            SteamOverlayObservationAccess.InhibitWrites(); ComposeRuntime();
            for (int i = 0; i < 5; i++) runtime.Tick();
            Assert.That(native.Queries + native.AchievementQueries, Is.Zero);
            Assert.That(native.CallbackCount, Is.EqualTo(5));
            Assert.That(native.SetCount + native.StoreCount, Is.Zero);
        }

        private sealed class Repository : IAchievementDocumentRepository
        {
            public int Saves;
            public AchievementDocumentLoadResult Load() => new AchievementDocumentLoadResult(AchievementDocumentLoadStatus.Loaded,
                new ProductAchievementDocument { EarnedAchievementIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
                    PendingAchievementPublicationIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value } }, "");
            public AchievementDocumentSaveResult Save(ProductAchievementDocument document) { Saves++; return AchievementDocumentSaveResult.Saved(); }
        }
        private sealed class Native : ISteamNativeApi, ISteamAchievementApi, ISteamObservationIdentityApi
        {
            public int InitCount, CallbackCount, SetCount, StoreCount, Queries, AchievementQueries;
            public bool CallbackError;
            public Action<bool> Activate;
            private readonly string[] names = SteamAchievementMapping.Production.Entries.Select(e => e.ExpectedSteamApiName.Value).ToArray();
            public bool IsPacksizeCompatible() => true;
            public SteamDllCheckObservation ObserveDllCheck() => SteamDllCheckObservation.UpstreamDisabled(true);
            public bool Initialize() { InitCount++; return true; }
            public void Shutdown() { }
            public void RunCallbacks() { CallbackCount++; if (CallbackError) throw new InvalidOperationException("callback-error"); }
            public uint GetAppId() => 5218360;
            public ulong GetSteamId() => 76561198000000000UL;
            public bool IsSteamIdValid() => true;
            public bool IsLoggedOn() => true;
            public bool IsOverlayEnabled() { Queries++; return false; }
            public void RegisterOverlayActivationCallback(Action<bool> observer) => Activate = observer;
            public void DisposeOverlayActivationCallback() => Activate = null;
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
