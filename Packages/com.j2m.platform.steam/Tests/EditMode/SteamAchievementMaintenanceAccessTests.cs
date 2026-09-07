using System;
using System.Reflection;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements;
using Game.Product.Achievements.Composition;
using NUnit.Framework;

namespace Game.Platform.Steam.Tests.EditMode
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class SteamAchievementMaintenanceAccessTests
    {
        private const uint AppId = 4242;
        private SteamPlatformRuntime runtime;
        private FakeSteamNativeApi lifecycle;
        private FakeSteamAchievementApi achievements;
        private SteamAchievementMaintenanceLease lease;
        private ProductAchievementCoordinator coordinator;
        private ProductAchievementPublicationSessionController controller;

        [SetUp]
        public void SetUp()
        {
            runtime = null;
            lease = null;
            ResetMaintenanceAccess();
            ProductAchievementPublicationSessionHandoff.ResetForTests();
            SteamAchievementMaintenanceAccess.DeferAutomaticPublication();
            lifecycle = new FakeSteamNativeApi { AppId = AppId };
            achievements = new FakeSteamAchievementApi();
            achievements.AchievementNames.Clear();
            SteamAchievementMapping.Production.TryGetExpectedSteamApiName(
                GameAchievementIds.CampaignLevel4Clear, out var expectedName);
            achievements.AchievementNames.Add(expectedName.Value);

            var sink = new SwitchableAchievementPublicationSink();
            coordinator = new ProductAchievementCoordinator(
                new EarnedRepository(), GameAchievementCatalog.Production, sink);
            Assert.That(coordinator.Initialize(), Is.True);
            controller = new ProductAchievementPublicationSessionController(sink, coordinator);
            Assert.That(ProductAchievementPublicationSessionHandoff.TryRegisterController(
                controller), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            achievements.DisposalException = null;
            lease?.Dispose();
            runtime?.Shutdown();
            ProductAchievementPublicationSessionHandoff.ClearController(controller);
            controller?.Dispose();
            coordinator?.Dispose();
            ProductAchievementPublicationSessionHandoff.ResetForTests();
            ResetMaintenanceAccess();
        }

        [Test]
        public void DeferredStartup_PumpsNativeWithoutPublishingExistingEarnedLedger()
        {
            InitializeRuntime();
            runtime.Tick();
            runtime.Tick();

            Assert.That(lifecycle.CallbackCount, Is.EqualTo(2));
            Assert.That(achievements.RegistrationCount, Is.Zero);
            Assert.That(achievements.GetAchievementCount, Is.Zero);
            Assert.That(achievements.SetAchievementCount, Is.Zero);
            Assert.That(achievements.StoreStatsCount, Is.Zero);
        }

        [Test]
        public void OwnedLease_ReceivesPumpedCallbacksAndRejectsCompetingOwners()
        {
            InitializeRuntime();
            var observations = 0;
            lease = SteamAchievementMaintenanceAccess.Acquire(_ => observations++, _ => { });
            lifecycle.CallbackAction = () => achievements.RaiseStatsStored(AppId);

            Assert.Throws<InvalidOperationException>(() =>
                SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { }));
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
            runtime.Tick();

            Assert.That(observations, Is.EqualTo(1));
            Assert.That(achievements.RegistrationCount, Is.EqualTo(1));
            Assert.That(achievements.SetAchievementCount, Is.Zero);
        }

        [Test]
        public void ReleasedLease_StartsPublisherOnceAndReconcilesExistingEarnedLedger()
        {
            InitializeRuntime();
            lease = SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { });
            lease.Dispose();
            lease.Dispose();

            Assert.That(achievements.DisposalCount, Is.EqualTo(1));
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.True);
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.True);
            Assert.That(achievements.RegistrationCount, Is.EqualTo(2));
            Assert.That(achievements.SetAchievementCount, Is.EqualTo(1));
            Assert.That(achievements.StoreStatsCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() =>
                SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { }));
        }

        [Test]
        public void FailedLease_QuarantinesSessionAfterReleaseInsteadOfStartingPublication()
        {
            InitializeRuntime();
            lease = SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { });
            lease.MarkFailed();
            lease.Dispose();

            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { }));
            runtime.Tick();
            Assert.That(lifecycle.CallbackCount, Is.EqualTo(1));
            Assert.That(achievements.RegistrationCount, Is.EqualTo(1));
            Assert.That(achievements.SetAchievementCount, Is.Zero);
        }

        [Test]
        public void CallbackDisposalFailure_QuarantinesSessionEvenWithoutExplicitMarkFailed()
        {
            InitializeRuntime();
            lease = SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { });
            achievements.DisposalException = new InvalidOperationException("dispose failed");

            Assert.Throws<InvalidOperationException>(() => lease.Dispose());
            achievements.DisposalException = null;

            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { }));
            Assert.That(achievements.SetAchievementCount, Is.Zero);
        }

        [Test]
        public void NativePumpFault_BlocksPublicationAndAnotherMaintenanceLease()
        {
            InitializeRuntime();
            lease = SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { });
            lifecycle.CallbackException = new InvalidOperationException("pump failed");
            Assert.DoesNotThrow(() => runtime.Tick());
            lease.Dispose();

            Assert.That(SteamAchievementMaintenanceAccess.IsAvailable, Is.False);
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { }));
            Assert.That(achievements.SetAchievementCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShutdownWithOwnedLease_DisposesCallbacksBeforeNativeAndLateReleaseIsHarmless(
            bool disposalThrows)
        {
            InitializeRuntime();
            var order = new System.Collections.Generic.List<string>();
            achievements.CallOrder = order;
            lifecycle.CallOrder = order;
            lease = SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { });
            if (disposalThrows)
                achievements.DisposalException = new InvalidOperationException("dispose failed");

            Assert.DoesNotThrow(() => runtime.Shutdown());
            lease.Dispose();
            runtime.Shutdown();

            Assert.That(order, Is.EqualTo(new[]
            {
                "achievement-dispose", "overlay-dispose", "native-shutdown",
            }));
            Assert.That(achievements.DisposalCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
            Assert.That(SteamAchievementMaintenanceAccess.IsAvailable, Is.False);
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
        }

        [Test]
        public void AchievementSmokeSession_CannotBeClaimedByMaintenance()
        {
            lifecycle.AppId = SpacewarAchievementSmokePolicy.AppId;
            achievements.AchievementNames.Clear();
            achievements.AchievementNames.Add(SpacewarAchievementSmokePolicy.TargetAchievement);
            InitializeRuntime(achievementSmoke: true);

            Assert.Throws<InvalidOperationException>(() =>
                SteamAchievementMaintenanceAccess.Acquire(_ => { }, _ => { }));
            Assert.That(SteamAchievementMaintenanceAccess.StartPublication(), Is.False);
            Assert.That(achievements.RegistrationCount, Is.EqualTo(1));
            Assert.That(achievements.SetAchievementCount, Is.EqualTo(1));
        }

        private void InitializeRuntime(bool achievementSmoke = false)
        {
            runtime = new SteamPlatformRuntime(
                new SteamRuntimeDependencies(lifecycle, achievements),
                smokeRequested: achievementSmoke,
                achievementSmokeRequested: achievementSmoke,
                monotonicSeconds: () => 0d,
                smokeLogger: _ => { });
            Assert.That(runtime.Initialize().IsSuccess, Is.True);
        }

        private static void ResetMaintenanceAccess()
        {
            typeof(SteamAchievementMaintenanceAccess)
                .GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
        }

        private sealed class EarnedRepository : IAchievementDocumentRepository
        {
            public AchievementDocumentLoadResult Load()
            {
                return new AchievementDocumentLoadResult(
                    AchievementDocumentLoadStatus.Loaded,
                    new ProductAchievementDocument
                    {
                        EarnedAchievementIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
                        PendingAchievementPublicationIds = Array.Empty<string>(),
                    }, string.Empty);
            }

            public AchievementDocumentSaveResult Save(ProductAchievementDocument document)
                => AchievementDocumentSaveResult.Saved();
        }
    }
}
