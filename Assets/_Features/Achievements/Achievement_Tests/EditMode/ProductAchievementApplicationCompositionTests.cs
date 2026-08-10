using System;
using System.IO;
using Game.Feature.Stages;
using Game.Product.Achievements.CampaignIntegration;
using Game.Product.Achievements.Composition;
using Game.Product.Achievements.Infrastructure;
using NUnit.Framework;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class ProductAchievementApplicationCompositionTests
    {
        private string _testRoot;
        private string _canonicalSavesRoot;

        [SetUp]
        public void SetUp()
        {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                "j2m-achievement-composition-" + Guid.NewGuid().ToString("N"));
            _canonicalSavesRoot = Path.Combine(_testRoot, "Saves");
            ProductAchievementEarningSinkHandoff.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ProductAchievementEarningSinkHandoff.ResetForTests();
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }

        [Test]
        public void CanonicalSaveProvider_InitializesMissingDocumentWithoutEagerWrite()
        {
            using var owner = new ProductAchievementApplicationLifetimeOwner(
                new TemporarySavePathProvider(_canonicalSavesRoot));

            var initialized = owner.Initialize();

            Assert.That(initialized, Is.True);
            Assert.That(owner.EarningSink, Is.Not.Null);
            Assert.That(Directory.Exists(_canonicalSavesRoot), Is.False);
            Assert.That(File.Exists(AchievementPath), Is.False);
        }

        [Test]
        public void ProductHost_UsesCanonicalSavesRootInsteadOfDirectPlayTempRoot()
        {
            var directPlayTempRoot = Path.Combine(_testRoot, "DirectPlay", "slot-1");
            using var owner = new ProductAchievementApplicationLifetimeOwner(
                new TemporarySavePathProvider(_canonicalSavesRoot));
            Assert.That(owner.Initialize(), Is.True);

            var earned = owner.EarningSink.Earn(GameAchievementIds.NormalCampaignComplete);

            Assert.That(earned, Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(File.Exists(AchievementPath), Is.True);
            Assert.That(
                File.Exists(Path.Combine(
                    directPlayTempRoot,
                    FileProductAchievementRepository.AchievementFileName)),
                Is.False);
            Assert.That(Directory.Exists(directPlayTempRoot), Is.False);
            Assert.That(AchievementPath, Does.Not.Contain(Path.Combine("Saves", "Saves")));
        }

        [Test]
        public void LifetimeOwner_RepeatedInitializeAndDispose_CreateInitializeAndDisposeOnce()
        {
            var host = new RecordingHostLifetime();
            var factoryCount = 0;
            var owner = new ProductAchievementApplicationLifetimeOwner(
                new TemporarySavePathProvider(_canonicalSavesRoot),
                hostFactory: (_, _) =>
                {
                    factoryCount++;
                    return host;
                });

            Assert.That(owner.Initialize(), Is.True);
            Assert.That(owner.Initialize(), Is.True);
            owner.Dispose();
            owner.Dispose();

            Assert.That(factoryCount, Is.EqualTo(1));
            Assert.That(host.InitializeCount, Is.EqualTo(1));
            Assert.That(host.DisposeCount, Is.EqualTo(1));
            Assert.That(owner.Initialize(), Is.False);
        }

        [Test]
        public void LifetimeOwner_DefaultPublisherIsUnavailable()
        {
            IAchievementPublicationSink capturedSink = null;
            var host = new RecordingHostLifetime();
            using var owner = new ProductAchievementApplicationLifetimeOwner(
                new TemporarySavePathProvider(_canonicalSavesRoot),
                hostFactory: (_, sink) =>
                {
                    capturedSink = sink;
                    return host;
                });

            owner.Initialize();

            Assert.That(capturedSink, Is.TypeOf<UnavailableAchievementPublicationSink>());
        }

        [Test]
        public void ExistingEarnedPending_StartupReconcilesOnceAndKeepsOutboxUnavailable()
        {
            Directory.CreateDirectory(_canonicalSavesRoot);
            File.WriteAllText(
                AchievementPath,
                "{\"SchemaVersion\":1,\"EarnedAchievementIds\":[\"campaign.complete\"],\"PendingAchievementPublicationIds\":[\"campaign.complete\"]}");
            using var owner = new ProductAchievementApplicationLifetimeOwner(
                new TemporarySavePathProvider(_canonicalSavesRoot));

            Assert.That(owner.Initialize(), Is.True);
            Assert.That(owner.Initialize(), Is.True);
            var host = owner.Host as ProductAchievementApplicationHost;
            var snapshot = host?.Coordinator.GetSnapshot();

            Assert.That(host, Is.Not.Null);
            Assert.That(snapshot.HasValue, Is.True);
            Assert.That(snapshot.Value.EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.Value.PendingAchievementPublicationIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.Value.InFlightCount, Is.Zero);
            Assert.That(
                File.ReadAllText(AchievementPath),
                Does.Contain("\"PendingAchievementPublicationIds\":[\"campaign.complete\"]"));
        }

        [Test]
        public void LifetimeOwner_RegistersExactSinkUntilDispose()
        {
            var owner = new ProductAchievementApplicationLifetimeOwner(
                new TemporarySavePathProvider(_canonicalSavesRoot));

            Assert.That(owner.Initialize(), Is.True);
            Assert.That(ProductAchievementEarningSinkHandoff.TryGet(out var observed), Is.True);
            Assert.That(observed, Is.SameAs(owner.EarningSink));

            owner.Dispose();

            Assert.That(ProductAchievementEarningSinkHandoff.TryGet(out _), Is.False);
        }

        [Test]
        public void LifetimeOwner_StartupReceiptReconciliation_RunsOnceAfterHostInitialization()
        {
            var campaignStore = new ReceiptCampaignStore(CreateValidReceiptSlot());
            var campaignFactoryCount = 0;
            using var owner = new ProductAchievementApplicationLifetimeOwner(
                new TemporarySavePathProvider(_canonicalSavesRoot),
                campaignSaveSlotStoreFactory: () =>
                {
                    campaignFactoryCount++;
                    return campaignStore;
                });

            Assert.That(owner.Initialize(), Is.True);
            var first = owner.ReconcileNormalCampaignCompletionReceipt();
            var second = owner.ReconcileNormalCampaignCompletionReceipt();
            var host = owner.Host as ProductAchievementApplicationHost;

            Assert.That(first, Is.EqualTo(NormalCampaignCompletionAchievementResult.EarnedNew));
            Assert.That(second, Is.EqualTo(NormalCampaignCompletionAchievementResult.AlreadyReconciled));
            Assert.That(campaignFactoryCount, Is.EqualTo(1));
            Assert.That(campaignStore.LoadCount, Is.EqualTo(1));
            Assert.That(host, Is.Not.Null);
            Assert.That(host.Coordinator.GetSnapshot().EarnedAchievementIds.Count, Is.EqualTo(1));
            Assert.That(host.Coordinator.GetSnapshot().PendingAchievementPublicationIds.Count, Is.EqualTo(1));
        }

        [TestCase(EditorDirectPlayMode.NonCampaign)]
        [TestCase(EditorDirectPlayMode.CampaignTempSlot)]
        [TestCase(EditorDirectPlayMode.CampaignProductionSlot)]
        public void LifetimeOwner_DirectPlayStartup_SkipsProfileRead(
            EditorDirectPlayMode mode)
        {
            var campaignStore = new ReceiptCampaignStore(CreateValidReceiptSlot());
            var campaignFactoryCount = 0;
            using var owner = new ProductAchievementApplicationLifetimeOwner(
                new TemporarySavePathProvider(_canonicalSavesRoot),
                campaignSaveSlotStoreFactory: () =>
                {
                    campaignFactoryCount++;
                    return campaignStore;
                },
                directPlayContextProvider: () => new EditorDirectPlayContext(
                    mode,
                    StageId.CreateOrThrow("stage-4-3"),
                    "direct-play-save",
                    "direct-play-active",
                    3,
                    suppressCampaignFlow: false));

            Assert.That(owner.Initialize(), Is.True);
            var result = owner.ReconcileNormalCampaignCompletionReceipt();

            Assert.That(result, Is.EqualTo(NormalCampaignCompletionAchievementResult.DirectPlayExcluded));
            Assert.That(campaignFactoryCount, Is.Zero);
            Assert.That(campaignStore.LoadCount, Is.Zero);
            Assert.That(File.Exists(AchievementPath), Is.False);
        }

        private string AchievementPath => Path.Combine(
            _canonicalSavesRoot,
            FileProductAchievementRepository.AchievementFileName);

        private sealed class RecordingHostLifetime : IProductAchievementHostLifetime
        {
            public int InitializeCount { get; private set; }

            public int DisposeCount { get; private set; }

            public IProductAchievementEarningSink EarningSink { get; } =
                new RecordingEarningSink();

            public bool Initialize()
            {
                InitializeCount++;
                return true;
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
            }
        }

        private sealed class RecordingEarningSink : IProductAchievementEarningSink
        {
            public AchievementEarnResult Earn(GameAchievementId achievementId)
            {
                return AchievementEarnResult.EarnedNew;
            }
        }

        private static SaveSlotData CreateValidReceiptSlot()
        {
            return new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-4-3"),
                CurrentLevelGroupId = "level-4",
                CampaignCompleted = true,
                HasNormalCampaignCompletionReceipt = true,
                NormalCampaignCompletionReceipt = new NormalCampaignCompletionReceipt
                {
                    Version = NormalCampaignCompletionReceipt.CurrentVersion,
                    CompletedStageId = "stage-4-3",
                    StageRunId = "composition-startup-run",
                    ClearSource = (int)StageClearSource.Objective,
                },
            };
        }

        private sealed class ReceiptCampaignStore : ICampaignSaveSlotStore
        {
            private readonly SaveSlotData _slot;

            public ReceiptCampaignStore(SaveSlotData slot)
            {
                _slot = slot;
            }

            public string DiagnosticsKey => nameof(ReceiptCampaignStore);

            public CampaignSaveLoadReport LastCampaignLoadReport =>
                CampaignSaveLoadReport.Loaded("receipt test profile", nameof(ReceiptCampaignStore));

            public int LoadCount { get; private set; }

            public SaveSlotData[] LoadAll()
            {
                return LoadAllWithReport().Slots;
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                LoadCount++;
                return new CampaignSaveLoadResult(
                    new[] { _slot.Clone() },
                    LastCampaignLoadReport);
            }

            public SaveSlotData LoadSlot(int slotNumber) => _slot.Clone();

            public void SaveSlot(SaveSlotData slot) => throw new NotSupportedException();

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt) => throw new NotSupportedException();

            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation) =>
                throw new NotSupportedException();

            public void DeleteSlot(int slotNumber) => throw new NotSupportedException();

            public void ClearAll() => throw new NotSupportedException();
        }
    }
}
