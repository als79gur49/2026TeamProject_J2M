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
    public sealed class ResetOverlayStartupTests
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
            foreach (var panel in Resources.FindObjectsOfTypeAll<ResetOverlayTrialPresentation>())
                UnityEngine.Object.DestroyImmediate(panel.gameObject);
            ParticipantResetMenuAccess.Register(null);
            ResetStatics();
        }

        private static void ResetStatics()
        {
            ProductAchievementPublicationSessionHandoff.ResetForTests();
            InvokeReset(typeof(SteamAchievementMaintenanceAccess), "Reset");
            InvokeReset(typeof(ProductAchievementStartupControl), "Reset");
            InvokeReset(typeof(CampaignSaveCompositionProvider), "ResetProductionSession");
            InvokeReset(typeof(SteamOverlayObservationAccess), "Reset");
            InvokeReset(typeof(Game.Platform.Runtime.PlatformRuntimeRegistry), "ResetForSubsystemRegistration");
        }

        private static void InvokeReset(Type type, string method)
            => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinaryStartupReproducesEarnedLedgerPublication(bool pending)
        {
            var args = new[] { "game", "-j2mPlatformProvider", "steam" };
            ResetOverlayTrialOptions selected = null;
            ExhibitionApplication.InspectStartup(args, true, options => selected = options);
            Assert.That(selected, Is.Null);
            AssertAutomaticBootstrapCalls(expectedStart: 1, expectedReconcile: 1);
            StartPublicationComposition(pending);
            Assert.That(achievements.SetCount, Is.EqualTo(1));
            Assert.That(achievements.StoreCount, Is.EqualTo(1));
            Assert.That(achievements.GetCount, Is.GreaterThan(0));
            Assert.That(ProductAchievementStartupControl.IsDeferred, Is.False);
        }

        private static IEnumerable TrialArguments()
        {
            yield return new TestCaseData((object)InitialArgs()).SetName("InitiatorDefersBeforeComposition");
            yield return new TestCaseData((object)new[] { "game", "-j2mResetOverlayTrial" }).SetName("MissingTrialValueDefersBeforeComposition");
            yield return new TestCaseData((object)new[] { "game", "-j2mResetOverlayTrial", "config", "-j2mResetOverlayTrial", "duplicate" }).SetName("DuplicateTrialDefersBeforeComposition");
            yield return new TestCaseData((object)new[] { "game", "-j2mResetOverlayTrial", "config", "-j2mRestartExperiment", "FullCycle" }).SetName("MixedTrialDefersBeforeComposition");
            yield return new TestCaseData((object)new[] { "game", "-j2mResetOverlayPhase", "wrong" }).SetName("InvalidRoleDefersBeforeComposition");
            foreach (var role in new[] { "ResetWorker", "FinalObserver" })
                yield return new TestCaseData((object)new[] { "game", "-j2mResetOverlayContext", "context", "-j2mResetOverlayPhase", role,
                    "-j2mRestartObservation", "request", "-j2mPlatformProvider", "steam" }).SetName(role + "DefersBeforeComposition");
        }

        [TestCaseSource(nameof(TrialArguments))]
        public void TrialArguments_BlockProductionPublisherEvenWithRegisteredEarnedController(string[] args)
        {
            bool deferredInsideCompose = false;
            ResetOverlayTrialOptions selected = null;
            ExhibitionApplication.InspectStartup(args, true, options =>
            {
                selected = options;
                deferredInsideCompose = AllDeferred();
            });
            if (ResetOverlayTrialOptions.Parse(args, true).Error == null)
            {
                Assert.That(selected, Is.Not.Null);
                Assert.That(deferredInsideCompose, Is.True, "Deferral must precede journal/config constructors.");
            }
            else
            {
                Assert.That(selected, Is.Null);
                Assert.That(ParticipantResetMenuAccess.Current, Is.TypeOf<ResetOverlayTrial>());
                Assert.That(((ResetOverlayTrial)ParticipantResetMenuAccess.Current).Stage, Is.EqualTo(ResetOverlayStage.Failed));
            }
            StartPublicationComposition(true);
            AssertDeferredAndNativeReadOnly();
        }

        [TestCase("config")]
        [TestCase("journal")]
        [TestCase("constructor")]
        [TestCase("diagnostics")]
        public void CompositionFailureBeforeRuntimeRegistration_CannotReenablePublication(string failure)
        {
            bool deferredAtFailure = false;
            ExhibitionApplication.InspectStartup(InitialArgs(), true, _ =>
            {
                deferredAtFailure = AllDeferred();
                // Inject at the real pre-runtime composition boundary, not in a fake trial runtime.
                throw new IOException(failure + " failed before native registration");
            });
            Assert.That(deferredAtFailure, Is.True);
            Assert.That(ParticipantResetMenuAccess.Current, Is.TypeOf<ResetOverlayTrial>());
            var failed = (ResetOverlayTrial)ParticipantResetMenuAccess.Current;
            Assert.That(failed.Stage, Is.EqualTo(ResetOverlayStage.Failed));
            Assert.That(failed.Error, Does.Contain(failure));
            StartPublicationComposition(true);
            AssertDeferredAndNativeReadOnly();
        }

        [Test]
        public void LateTrialInspection_RejectsAlreadyStartedRuntimeInsteadOfClaimingBaselineWasProtected()
        {
            StartPublicationComposition(true);
            Assert.That(achievements.SetCount, Is.EqualTo(1));
            bool composed = false;
            ExhibitionApplication.InspectStartup(InitialArgs(), true, _ => composed = true);
            Assert.That(composed, Is.False);
            Assert.That(ParticipantResetMenuAccess.Current, Is.TypeOf<ResetOverlayTrial>());
            Assert.That(((ResetOverlayTrial)ParticipantResetMenuAccess.Current).Stage, Is.EqualTo(ResetOverlayStage.StartupAlreadyStarted));
            Assert.That(((ResetOverlayTrial)ParticipantResetMenuAccess.Current).Error, Does.Contain("StartupAlreadyStarted"));
            runtime.Tick();
            Assert.That(achievements.SetCount, Is.EqualTo(1));
        }

        [Test]
        public void BatchModeSuppressesAutomaticCampaignReconciliationInOrdinaryStartup()
        {
            int starts = 0, reconciliations = 0;
            ExhibitionApplication.InspectStartup(new[] { "game" }, true, _ => { });
            ProductAchievementRuntimeBootstrap.RunAutomaticStart(() => starts++);
            ProductAchievementRuntimeBootstrap.RunAutomaticReconciliation(true, () => reconciliations++);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(reconciliations, Is.Zero);
        }

        [Test]
        public void TrialCannotReleaseServicesOrProductionCampaignAccess()
        {
            ExhibitionApplication.InspectStartup(InitialArgs(), true, _ => { });
            Assert.That(ProductAchievementStartupControl.StartDeferredServices(), Is.False);
            Assert.That(typeof(ProductAchievementStartupControl).GetMethod("ReconcileCampaign").Invoke(null, null).ToString(), Is.EqualTo("NotAttempted"));
            CampaignSaveCompositionProvider.ReleaseProductionAccess();
            Assert.Throws<InvalidOperationException>(() => CampaignSaveCompositionProvider.CreateProductionProfileBacked());
            StartPublicationComposition(true);
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
            AssertDeferredAndNativeReadOnly();
        }
        [TestCase(false)][TestCase(true)]
        public void UnsupportedAndBatchTrialCannotEnterOrdinaryStartup(bool batch)
        {
            bool composed = false;
            ExhibitionApplication.InspectProcessStartup(InitialArgs(), batch, batch, _ => composed = true);
            Assert.That(composed, Is.False);
            Assert.That(ParticipantResetMenuAccess.Current, Is.TypeOf<ResetOverlayTrial>());
            StartPublicationComposition(true); AssertDeferredAndNativeReadOnly();
        }

        [Test]
        public void RuntimeCallbacksPutTrialInspectionBeforeAutomaticProductAndPlatformStartup()
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

        private static string[] InitialArgs() => new[] { "game", "-j2mPlatformProvider", "steam", "-j2mResetOverlayTrial", "config" };

        private static bool AllDeferred()
            => ProductAchievementStartupControl.IsDeferred && SteamAchievementMaintenanceAccess.IsDeferred;

        private void AssertDeferredAndNativeReadOnly()
        {
            Assert.That(AllDeferred(), Is.True);
            AssertAutomaticBootstrapCalls(expectedStart: 0, expectedReconcile: 0);
            Assert.Throws<InvalidOperationException>(() => CampaignSaveCompositionProvider.CreateProductionProfileBacked());
            Assert.That(lifecycle.InitializeCount, Is.EqualTo(1));
            Assert.That(lifecycle.CallbackCount, Is.EqualTo(2));
            Assert.That(SteamAchievementMaintenanceAccess.IsAvailable, Is.True);
            Assert.That(achievements.SetCount, Is.Zero);
            Assert.That(achievements.StoreCount, Is.Zero);
            Assert.That(achievements.GetCount, Is.Zero, "Automatic reconciliation must not consume the baseline.");
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(coordinator.GetSnapshot().EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(coordinator.GetSnapshot().PendingAchievementPublicationIds.Count, Is.EqualTo(1));
            using (var lease = SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { }))
            {
                Assert.That(lease.Api.GetAchievement(achievements.Name, out bool earned), Is.True);
                Assert.That(earned, Is.False, "Read-only native baseline remains unearned.");
                bool recorded = false;
                var baseline = new ResetOverlayBaseline(() =>
                {
                    return achievements.Names.Select(name =>
                    {
                        if (!lease.Api.GetAchievement(name, out bool value))
                            throw new IOException("baseline query failed");
                        return value;
                    }).ToArray();
                }, values => recorded = true);
                Assert.Throws<InvalidOperationException>(() => baseline.Prepare(),
                    "The real baseline policy must reject all-false Steam state despite local earned data.");
                Assert.That(recorded, Is.True);
            }
            Assert.That(achievements.SetCount + achievements.StoreCount, Is.Zero);
        }

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
                smokeRequested: false, achievementSmokeRequested: false,
                monotonicSeconds: () => 0d, smokeLogger: _ => { });
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
            public SteamDllCheckObservation ObserveDllCheck() => SteamDllCheckObservation.UpstreamDisabled(true);
            public bool Initialize() { InitializeCount++; return true; }
            public void RunCallbacks() => CallbackCount++;
            public void Shutdown() { }
            public uint GetAppId() => 5218360;
            public bool IsSteamIdValid() => true;
            public bool IsLoggedOn() => true;
            public bool IsOverlayEnabled() => true;
            public void RegisterOverlayActivationCallback(Action<bool> observer) { }
            public void DisposeOverlayActivationCallback() { }
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
