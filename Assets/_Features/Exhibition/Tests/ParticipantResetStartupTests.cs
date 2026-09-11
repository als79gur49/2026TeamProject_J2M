using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Exhibition.RestartExperiment;
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

        [TestCase("Pending", "unknown-mapping")]
        [TestCase("Ready", "unknown-mapping")]
        public void UnsupportedJournalBlocksOrdinaryStartupBeforePublication(string state, string mapping)
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "j2m-reset-startup-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(root);
            var path = System.IO.Path.Combine(root, "exhibition-reset.json");
            var record = new ResetRecord { OperationId = Guid.NewGuid().ToString("N"),
                State = state, AppId = 123, SteamId = 456, MappingVersion = mapping };
            var original = JsonUtility.ToJson(record);
            try
            {
                System.IO.File.WriteAllText(path, original); int composed = 0;
                Assert.Throws<System.IO.IOException>(() => ExhibitionApplication.InspectProcessStartup(new[] { "game" }, false, () =>
                {
                    new FileExhibitionResetJournal(path).Load();
                    composed++; ComposeRuntime(true);
                }));
                Assert.That(composed, Is.Zero);
                Assert.That(ProductAchievementStartupControl.IsDeferred, Is.True);
                Assert.That(SteamAchievementMaintenanceAccess.IsDeferred, Is.True);
                Assert.That(System.IO.File.ReadAllText(path), Is.EqualTo(original));
            }
            finally { System.IO.Directory.Delete(root, true); }
        }

        [Test, Category("Integration")]
        public void KnownPreviousReadyIsArchivedBeforeOrdinaryPublicationWithoutReset()
        {
            WithLegacyJournal(ResetRecord.Ready, (journal, path, original) =>
            {
                var steam = new ResetSteam(); var progress = new ResetProgress();
                var reset = new ExhibitionResetCoordinator(journal, steam, progress);
                ResetRecord startup = null;
                ExhibitionApplication.InspectProcessStartup(new[] { "game" }, false, () =>
                {
                    startup = reset.ReadStartupRecord();
                    ComposeRuntime(true);
                });
                Assert.That(startup.MappingVersion, Is.EqualTo("level-clear-v1"));
                Assert.That(startup.State, Is.EqualTo(ResetRecord.Ready));
                Assert.That(journal.Load(), Is.Null);
                Assert.That(journal.HasArchivedRecord, Is.True);
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*", SearchOption.AllDirectories)
                    .Any(file => File.ReadAllText(file) == original), Is.True, "Archive must preserve the exact previous record.");
                runtime.Tick();
                Assert.That(native.SetCount, Is.EqualTo(1));
                Assert.That(native.StoreCount, Is.EqualTo(1));
                Assert.That(ProductAchievementStartupControl.IsDeferred, Is.False);
                Assert.That(steam.IdentityCalls + steam.ResetCalls + progress.Calls, Is.Zero);
                // A following ordinary startup must not restore the archived record.
                var following = reset.ReadStartupRecord();
                Assert.That(following, Is.Null);
                var nextService = new ParticipantResetService(reset, new ResetRestart(), () => true,
                    () => { }, () => { }, () => { }, following, suppressSaveSeedImport: journal.HasArchivedRecord);
                Assert.That(nextService.SuppressSaveSeedImport, Is.True);
                Assert.That(nextService.BlocksMenu, Is.False);
            });
        }

        [Test, Category("Integration")]
        public void KnownPreviousPendingStartupPreservesRecordAndBlocksUntilExplicitRequest()
        {
            WithLegacyJournal(ResetRecord.Pending, (journal, path, original) =>
            {
                var steam = new ResetSteam(); var progress = new ResetProgress(); var restart = new ResetRestart();
                var reset = new ExhibitionResetCoordinator(journal, steam, progress);
                var startup = reset.ReadStartupRecord();
                int started = 0, reconciled = 0;
                var service = new ParticipantResetService(reset, restart, () => true, () => { },
                    () => started++, () => reconciled++, startup);
                service.PrepareMenuAsync().GetAwaiter().GetResult();
                service.CompleteMenuInitialization();
                Assert.That(service.BlocksMenu, Is.True);
                Assert.That(service.RequiresLegacyRecovery, Is.True);
                Assert.That(service.CanRestartAfterFailure, Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                Assert.That(steam.ResetCalls + progress.Calls + restart.Calls + started + reconciled, Is.Zero);
            });
        }

        [TestCase("{}")]
        [TestCase("not-json")]
        public void CorruptJournalBlocksOrdinaryStartupAndPreservesBytes(string contents)
        {
            var root = Path.Combine(Path.GetTempPath(), "j2m-reset-corrupt-startup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "exhibition-reset.json");
            try
            {
                File.WriteAllText(path, contents); int composed = 0;
                Assert.Throws<IOException>(() => ExhibitionApplication.InspectProcessStartup(new[] { "game" }, false, () =>
                {
                    new ExhibitionResetCoordinator(new FileExhibitionResetJournal(path), new ResetSteam(), new ResetProgress())
                        .ReadStartupRecord();
                    composed++; ComposeRuntime(true);
                }));
                Assert.That(composed, Is.Zero);
                Assert.That(ProductAchievementStartupControl.IsDeferred, Is.True);
                Assert.That(SteamAchievementMaintenanceAccess.IsDeferred, Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo(contents));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test, Category("Integration")]
        public void CompletedReturnRejectsPreviousMappingObservedByCoordinatorWithoutConsumingRequest()
        {
            WithLegacyJournal(ResetRecord.Ready, (journal, path, original) =>
            {
                var previous = JsonUtility.FromJson<ResetRecord>(original);
                var current = previous.Copy(); current.MappingVersion = ExhibitionResetCoordinator.MappingVersion;
                File.WriteAllText(path, JsonUtility.ToJson(current));
                var request = new ExperimentRequest { CompletedResetProduct = true, Trial = Trial.FullCycle,
                    OperationId = current.OperationId, AppId = current.AppId, SteamId = current.SteamId,
                    ReadyJournalPath = WindowsIdentityCapture.CanonicalPath(path),
                    ReadyJournalSha256 = ExperimentFiles.Hash(path) };
                var handoff = Path.Combine(Path.GetDirectoryName(path), "participant-reset-handoff", current.OperationId);
                Directory.CreateDirectory(handoff); request.EvidenceDirectory = handoff;
                var requestPath = Path.Combine(handoff, "request.json");
                File.WriteAllText(requestPath, ExperimentFiles.Json(request));
                var options = CompletedParticipantResetOptions.Parse(new[] { "game", "-j2mCompletedParticipantReset", requestPath,
                    "-j2mCompletedParticipantResetHash", ExperimentFiles.Hash(requestPath) });
                // Model a changed coordinator read after the separate wire-file integrity check.
                var reset = new ExhibitionResetCoordinator(new ReadOnlyResetJournal(previous), new ResetSteam(), new ResetProgress());
                Assert.Throws<InvalidOperationException>(() => options.Validate(reset));
                Assert.That(File.Exists(requestPath + ".consumed"), Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo(JsonUtility.ToJson(current)));
            });
        }

        private sealed class ReadOnlyResetJournal : IExhibitionResetJournal
        {
            private readonly ResetRecord record;
            public ReadOnlyResetJournal(ResetRecord record) { this.record = record; }
            public ResetRecord Load() => record.Copy();
            public void Save(ResetRecord value) { Assert.Fail("Completed-return validation must not rewrite reset records."); }
        }

        private static void WithLegacyJournal(string state, Action<FileExhibitionResetJournal, string, string> action)
        {
            var root = Path.Combine(Path.GetTempPath(), "j2m-reset-legacy-startup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "exhibition-reset.json");
            var record = new ResetRecord { OperationId = Guid.NewGuid().ToString("N"), State = state,
                AppId = 123, SteamId = 456, MappingVersion = "level-clear-v1" };
            // Preserve original bytes rather than testing only serializer-normalized input.
            var original = "\n  " + JsonUtility.ToJson(record) + "\n";
            try { File.WriteAllText(path, original); action(new FileExhibitionResetJournal(path), path, original); }
            finally { Directory.Delete(root, true); }
        }

        private sealed class ResetSteam : IExhibitionSteamReset
        {
            public int IdentityCalls, ResetCalls;
            public ResetIdentity GetIdentity() { IdentityCalls++; return new ResetIdentity(123, 456); }
            public Task ResetAsync(ResetIdentity identity) { ResetCalls++; return Task.CompletedTask; }
        }
        private sealed class ResetProgress : IParticipantProgressReset
        {
            public int Calls;
            public void Reset() { Calls++; }
        }
        private sealed class ResetRestart : IParticipantRestart
        {
            public int Calls;
            public void ValidateAvailable() { }
            public void Restart(ResetIdentity identity) { Calls++; }
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
