using System;
using System.Collections.Generic;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements;
using Game.Product.Achievements.Composition;
using NUnit.Framework;

namespace Game.Platform.Steam.Tests.EditMode
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class SteamProductAchievementPublicationIntegrationTests
    {
        private const uint SessionAppId = 4242;

        [SetUp]
        public void SetUp()
        {
            ProductAchievementPublicationSessionHandoff.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ProductAchievementPublicationSessionHandoff.ResetForTests();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OrderingIndependentHandoff_AttachesOnceAndSubmittedKeepsPending(
            bool productFirst)
        {
            var product = CreateProduct(pending: true);
            var lifecycle = ProductLifecycle();
            var achievements = ProductApi();
            lifecycle.CallbackAction = () => RaiseProductSuccess(achievements);
            var runtime = CreateRuntime(lifecycle, achievements);

            if (productFirst)
            {
                Assert.That(
                    ProductAchievementPublicationSessionHandoff.TryRegisterController(
                        product.Controller),
                    Is.True);
            }

            Assert.That(runtime.Initialize().IsSuccess, Is.True);

            if (!productFirst)
            {
                Assert.That(achievements.SetAchievementCount, Is.Zero);
                Assert.That(
                    ProductAchievementPublicationSessionHandoff.TryRegisterController(
                        product.Controller),
                    Is.True);
            }

            runtime.Initialize();
            Assert.That(achievements.SetAchievementCount, Is.EqualTo(1));
            Assert.That(achievements.StoreStatsCount, Is.EqualTo(1));

            runtime.Tick();

            Assert.That(
                product.Coordinator.GetSnapshot().PendingAchievementPublicationIds.Count,
                Is.EqualTo(1));
            Assert.That(product.Repository.SaveCount, Is.Zero);
            Assert.That(achievements.RegistrationCount, Is.EqualTo(1));
            runtime.Shutdown();
        }

        [Test]
        public void SteamReady_ReconcilesEarnedEvenWhenPendingIsEmpty()
        {
            var product = CreateProduct(pending: false);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                product.Controller);
            var lifecycle = ProductLifecycle();
            var achievements = ProductApi();
            var runtime = CreateRuntime(lifecycle, achievements);

            runtime.Initialize();

            Assert.That(achievements.SetAchievementCount, Is.EqualTo(1));
            Assert.That(achievements.StoreStatsCount, Is.EqualTo(1));
            runtime.Shutdown();
        }

        [Test]
        public void SecondSteamRuntimeSession_IsRejectedWithinSameApplicationLifetime()
        {
            var product = CreateProduct(pending: false);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                product.Controller);
            var firstLifecycle = ProductLifecycle();
            var firstAchievements = ProductApi();
            firstAchievements.BeforeUnlocked = true;
            var firstRuntime = CreateRuntime(firstLifecycle, firstAchievements);

            firstRuntime.Initialize();
            firstRuntime.Shutdown();

            var secondLifecycle = ProductLifecycle();
            var secondAchievements = ProductApi();
            secondAchievements.BeforeUnlocked = true;
            var secondRuntime = CreateRuntime(secondLifecycle, secondAchievements);
            secondRuntime.Initialize();

            Assert.That(firstAchievements.GetAchievementCount, Is.EqualTo(1));
            Assert.That(secondAchievements.GetAchievementCount, Is.Zero);
            Assert.That(secondAchievements.SetAchievementCount, Is.Zero);
            Assert.That(secondAchievements.RegistrationCount, Is.EqualTo(1));
            Assert.That(secondAchievements.DisposalCount, Is.EqualTo(1));
            secondRuntime.Shutdown();
        }

        [Test]
        public void SubmittedPending_IsRemovedOnlyAfterFreshApplicationLifetimePreRead()
        {
            var repository = new MemoryRepository(new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
                PendingAchievementPublicationIds = new[]
                {
                    GameAchievementIds.CampaignLevel4Clear.Value,
                },
            });
            var firstProduct = CreateProduct(repository);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                firstProduct.Controller);
            var firstLifecycle = ProductLifecycle();
            var firstAchievements = ProductApi();
            firstLifecycle.CallbackAction = () => RaiseProductSuccess(firstAchievements);
            var firstRuntime = CreateRuntime(firstLifecycle, firstAchievements);

            firstRuntime.Initialize();
            firstRuntime.Tick();

            Assert.That(
                firstProduct.Coordinator.GetSnapshot().PendingAchievementPublicationIds.Count,
                Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.Zero);

            firstRuntime.Shutdown();
            ProductAchievementPublicationSessionHandoff.ClearController(
                firstProduct.Controller);
            firstProduct.Controller.Dispose();
            firstProduct.Coordinator.Dispose();
            ProductAchievementPublicationSessionHandoff.ResetForTests();

            var secondProduct = CreateProduct(repository);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                secondProduct.Controller);
            var secondLifecycle = ProductLifecycle();
            var secondAchievements = ProductApi();
            secondAchievements.BeforeUnlocked = true;
            var secondRuntime = CreateRuntime(secondLifecycle, secondAchievements);

            secondRuntime.Initialize();

            Assert.That(
                secondProduct.Coordinator.GetSnapshot().PendingAchievementPublicationIds.Count,
                Is.Zero);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(secondAchievements.SetAchievementCount, Is.Zero);
            secondRuntime.Shutdown();
        }

        [Test]
        public void AchievementSmoke_OwnsOnlyCallbackPairAndLeavesProductPending()
        {
            var product = CreateProduct(pending: true);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                product.Controller);
            var lifecycle = new FakeSteamNativeApi();
            var achievements = new FakeSteamAchievementApi();
            var runtime = new SteamPlatformRuntime(
                new SteamRuntimeDependencies(lifecycle, achievements),
                smokeRequested: true,
                achievementSmokeRequested: true,
                monotonicSeconds: () => 0d,
                smokeLogger: _ => { });

            runtime.Initialize();

            Assert.That(achievements.RegistrationCount, Is.EqualTo(1));
            Assert.That(achievements.SetAchievementCount, Is.EqualTo(1));
            Assert.That(
                product.Coordinator.GetSnapshot().PendingAchievementPublicationIds.Count,
                Is.EqualTo(1));
            Assert.That(product.Repository.SaveCount, Is.Zero);
            runtime.Shutdown();
        }

        [Test]
        public void BaseSteamSmokeWithoutAchievementSmoke_AllowsProductPublisherOwnership()
        {
            var product = CreateProduct(pending: true);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                product.Controller);
            var lifecycle = ProductLifecycle();
            var achievements = ProductApi();
            var runtime = new SteamPlatformRuntime(
                new SteamRuntimeDependencies(lifecycle, achievements),
                smokeRequested: true,
                achievementSmokeRequested: false,
                monotonicSeconds: () => 0d,
                smokeLogger: _ => { });

            runtime.Initialize();

            Assert.That(achievements.RegistrationCount, Is.EqualTo(1));
            Assert.That(achievements.SetAchievementCount, Is.EqualTo(1));
            runtime.Shutdown();
        }

        [Test]
        public void CallbackRegistrationFailure_DoesNotDisposeUnownedCallbacksAndLeavesProductUnavailable()
        {
            var product = CreateProduct(pending: true);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                product.Controller);
            var lifecycle = ProductLifecycle();
            var achievements = ProductApi();
            achievements.RegistrationException = new InvalidOperationException("register");
            var runtime = CreateRuntime(lifecycle, achievements);

            Assert.DoesNotThrow(() => runtime.Initialize());
            Assert.That(achievements.RegistrationCount, Is.EqualTo(1));
            Assert.That(achievements.DisposalCount, Is.Zero);
            Assert.That(achievements.SetAchievementCount, Is.Zero);
            Assert.That(
                product.Coordinator.GetSnapshot().PendingAchievementPublicationIds.Count,
                Is.EqualTo(1));

            runtime.Tick();
            runtime.Shutdown();
            Assert.That(lifecycle.CallbackCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void CallbackPumpFault_StopsPublicationClearsInFlightAndKeepsPending()
        {
            var product = CreateProduct(pending: true);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                product.Controller);
            var lifecycle = ProductLifecycle();
            var achievements = ProductApi(
                GameAchievementIds.CampaignLevel4Clear,
                GameAchievementIds.CampaignLevel1Clear,
                GameAchievementIds.CampaignLevel2Clear);
            var runtime = CreateRuntime(lifecycle, achievements);
            runtime.Initialize();

            Assert.That(product.Coordinator.GetSnapshot().InFlightCount, Is.EqualTo(1));
            Assert.That(
                product.Coordinator.Earn(GameAchievementIds.CampaignLevel1Clear),
                Is.EqualTo(AchievementEarnResult.EarnedNew));
            Assert.That(product.Coordinator.GetSnapshot().InFlightCount, Is.EqualTo(2));
            Assert.That(achievements.SetAchievementCount, Is.EqualTo(1));
            Assert.That(achievements.StoreStatsCount, Is.EqualTo(1));

            lifecycle.CallbackException = new InvalidOperationException("callback failed");
            Assert.DoesNotThrow(() => runtime.Tick());

            var afterFault = product.Coordinator.GetSnapshot();
            Assert.That(
                runtime.Diagnostics.State,
                Is.EqualTo(SteamPlatformRuntimeState.Faulted));
            Assert.That(runtime.SteamAvailability.IsAvailable, Is.False);
            Assert.That(product.Router.IsUnavailable, Is.True);
            Assert.That(afterFault.InFlightCount, Is.Zero);
            Assert.That(afterFault.PendingAchievementPublicationIds.Count, Is.EqualTo(2));
            Assert.That(achievements.DisposalCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.Zero);

            Assert.That(
                product.Coordinator.Earn(
                    GameAchievementIds.CampaignLevel2Clear),
                Is.EqualTo(AchievementEarnResult.EarnedNew));
            var afterUnavailableEarn = product.Coordinator.GetSnapshot();
            Assert.That(afterUnavailableEarn.InFlightCount, Is.Zero);
            Assert.That(
                afterUnavailableEarn.PendingAchievementPublicationIds.Count,
                Is.EqualTo(3));
            Assert.That(achievements.SetAchievementCount, Is.EqualTo(1));
            Assert.That(achievements.StoreStatsCount, Is.EqualTo(1));

            runtime.Shutdown();

            Assert.That(achievements.DisposalCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EitherShutdownOrder_DisposesCallbacksAndNativeExactlyOnce(
            bool steamFirst)
        {
            var order = new List<string>();
            var product = CreateProduct(pending: true);
            ProductAchievementPublicationSessionHandoff.TryRegisterController(
                product.Controller);
            var lifecycle = ProductLifecycle();
            lifecycle.CallOrder = order;
            var achievements = ProductApi();
            achievements.CallOrder = order;
            var runtime = CreateRuntime(lifecycle, achievements);
            runtime.Initialize();

            if (steamFirst)
            {
                runtime.Shutdown();
                ProductAchievementPublicationSessionHandoff.ClearController(
                    product.Controller);
                product.Controller.Dispose();
                product.Coordinator.Dispose();
            }
            else
            {
                ProductAchievementPublicationSessionHandoff.ClearController(
                    product.Controller);
                product.Controller.Dispose();
                product.Coordinator.Dispose();
                runtime.Shutdown();
            }

            achievements.RaiseStatsStored(SessionAppId);
            achievements.RaiseAchievementStored(SessionAppId, ExpectedName());
            runtime.Shutdown();

            Assert.That(achievements.DisposalCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
            Assert.That(order, Is.EqualTo(new[]
            {
                "achievement-dispose",
                "overlay-dispose",
                "native-shutdown",
            }));
        }

        private static ProductHarness CreateProduct(bool pending)
        {
            var repository = new MemoryRepository(new ProductAchievementDocument
            {
                EarnedAchievementIds = new[] { GameAchievementIds.CampaignLevel4Clear.Value },
                PendingAchievementPublicationIds = pending
                    ? new[] { GameAchievementIds.CampaignLevel4Clear.Value }
                    : Array.Empty<string>(),
            });
            return CreateProduct(repository);
        }

        private static ProductHarness CreateProduct(MemoryRepository repository)
        {
            var router = new SwitchableAchievementPublicationSink();
            var coordinator = new ProductAchievementCoordinator(
                repository,
                GameAchievementCatalog.Production,
                router);
            Assert.That(coordinator.Initialize(), Is.True);
            var controller = new ProductAchievementPublicationSessionController(
                router,
                coordinator);
            return new ProductHarness(repository, coordinator, controller, router);
        }

        private static FakeSteamNativeApi ProductLifecycle()
        {
            return new FakeSteamNativeApi { AppId = SessionAppId };
        }

        private static FakeSteamAchievementApi ProductApi(
            params GameAchievementId[] achievementIds)
        {
            var api = new FakeSteamAchievementApi();
            api.AchievementNames.Clear();
            if (achievementIds == null || achievementIds.Length == 0)
            {
                api.AchievementNames.Add(ExpectedName());
                return api;
            }

            for (var i = 0; i < achievementIds.Length; i++)
            {
                api.AchievementNames.Add(ExpectedName(achievementIds[i]));
            }

            return api;
        }

        private static SteamPlatformRuntime CreateRuntime(
            FakeSteamNativeApi lifecycle,
            FakeSteamAchievementApi achievements)
        {
            return new SteamPlatformRuntime(
                new SteamRuntimeDependencies(lifecycle, achievements),
                smokeRequested: false,
                achievementSmokeRequested: false,
                monotonicSeconds: () => 0d,
                smokeLogger: _ => { });
        }

        private static void RaiseProductSuccess(FakeSteamAchievementApi achievements)
        {
            achievements.RaiseStatsStored(SessionAppId);
            achievements.RaiseAchievementStored(SessionAppId, ExpectedName());
        }

        private static string ExpectedName()
        {
            return ExpectedName(GameAchievementIds.CampaignLevel4Clear);
        }

        private static string ExpectedName(GameAchievementId achievementId)
        {
            SteamAchievementMapping.Production.TryGetExpectedSteamApiName(
                achievementId,
                out var expectedName);
            return expectedName.Value;
        }

        private sealed class ProductHarness
        {
            internal ProductHarness(
                MemoryRepository repository,
                ProductAchievementCoordinator coordinator,
                ProductAchievementPublicationSessionController controller,
                SwitchableAchievementPublicationSink router)
            {
                Repository = repository;
                Coordinator = coordinator;
                Controller = controller;
                Router = router;
            }

            internal MemoryRepository Repository { get; }

            internal ProductAchievementCoordinator Coordinator { get; }

            internal ProductAchievementPublicationSessionController Controller { get; }

            internal SwitchableAchievementPublicationSink Router { get; }
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
