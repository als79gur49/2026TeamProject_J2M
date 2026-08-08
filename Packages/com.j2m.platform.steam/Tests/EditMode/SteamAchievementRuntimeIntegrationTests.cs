using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Platform.Steam.Tests.EditMode
{
    public sealed class SteamAchievementRuntimeIntegrationTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void MissingAnySmokeOptIn_PerformsNoAchievementMutation(
            bool baseSmokeRequested,
            bool achievementSmokeRequested)
        {
            var lifecycle = new FakeSteamNativeApi();
            var achievements = new FakeSteamAchievementApi();
            var runtime = CreateRuntime(
                lifecycle,
                achievements,
                baseSmokeRequested,
                achievementSmokeRequested);

            runtime.Initialize();
            runtime.Tick();
            runtime.Shutdown();

            Assert.That(achievements.SetAchievementCount, Is.Zero);
            Assert.That(achievements.StoreStatsCount, Is.Zero);
            Assert.That(lifecycle.InitializeCount, Is.EqualTo(1));
            Assert.That(lifecycle.CallbackCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void CanonicalRuntimeTick_PumpsCallbacksAndAdvancesCoordinatorWithoutSecondPump()
        {
            var lifecycle = new FakeSteamNativeApi();
            var achievements = new FakeSteamAchievementApi();
            lifecycle.CallbackAction = () =>
            {
                achievements.RaiseAchievementStored();
                achievements.RaiseStatsStored();
            };
            var runtime = CreateRuntime(
                lifecycle,
                achievements,
                baseSmokeRequested: true,
                achievementSmokeRequested: true);

            runtime.Initialize();
            runtime.Tick();

            Assert.That(runtime.AchievementSmokeDiagnostics.TerminalOutcome,
                Is.EqualTo(SteamAchievementSmokeOutcome.Succeeded));
            Assert.That(lifecycle.CallbackCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.CallbackPumpCount, Is.EqualTo(1));
            Assert.That(achievements.GetAchievementCount, Is.EqualTo(2));

            runtime.Shutdown();
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void Shutdown_DisposesAchievementThenOverlayThenNativeExactlyOnce()
        {
            var order = new List<string>();
            var lifecycle = new FakeSteamNativeApi { CallOrder = order };
            var achievements = new FakeSteamAchievementApi { CallOrder = order };
            var runtime = CreateRuntime(
                lifecycle,
                achievements,
                baseSmokeRequested: true,
                achievementSmokeRequested: true);
            runtime.Initialize();

            runtime.Shutdown();
            runtime.Shutdown();

            Assert.That(order, Is.EqualTo(new[]
            {
                "achievement-dispose",
                "overlay-dispose",
                "native-shutdown",
            }));
            Assert.That(achievements.DisposalCount, Is.EqualTo(1));
            Assert.That(lifecycle.OverlayCallbackDisposeCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void AchievementCallbackDisposalException_DoesNotBlockNativeShutdown()
        {
            var lifecycle = new FakeSteamNativeApi();
            var achievements = new FakeSteamAchievementApi
            {
                DisposalException = new InvalidOperationException("dispose"),
            };
            var runtime = CreateRuntime(
                lifecycle,
                achievements,
                baseSmokeRequested: true,
                achievementSmokeRequested: true);
            runtime.Initialize();

            Assert.DoesNotThrow(() => runtime.Shutdown());

            Assert.That(achievements.DisposalCount, Is.EqualTo(1));
            Assert.That(lifecycle.OverlayCallbackDisposeCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
            Assert.That(runtime.AchievementSmokeDiagnostics.FailureKind,
                Is.EqualTo(SteamAchievementSmokeFailureKind.Exception));
        }

        [Test]
        public void AchievementSmokeLoggerException_DoesNotBlockOwnedShutdownSequence()
        {
            var order = new List<string>();
            var lifecycle = new FakeSteamNativeApi { CallOrder = order };
            var achievements = new FakeSteamAchievementApi { CallOrder = order };
            var runtime = new SteamPlatformRuntime(
                new SteamRuntimeDependencies(lifecycle, achievements),
                smokeRequested: true,
                achievementSmokeRequested: true,
                monotonicSeconds: () => 0d,
                smokeLogger: message =>
                {
                    if (message.StartsWith(
                        SteamAchievementSmokeCoordinator.ResultPrefix,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("achievement logger failed");
                    }
                });
            runtime.Initialize();

            Assert.DoesNotThrow(() => runtime.Shutdown());

            Assert.That(order, Is.EqualTo(new[]
            {
                "achievement-dispose",
                "overlay-dispose",
                "native-shutdown",
            }));
            Assert.That(achievements.DisposalCount, Is.EqualTo(1));
            Assert.That(lifecycle.OverlayCallbackDisposeCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownCount, Is.EqualTo(1));
        }

        private static SteamPlatformRuntime CreateRuntime(
            ISteamNativeApi lifecycle,
            ISteamAchievementApi achievements,
            bool baseSmokeRequested,
            bool achievementSmokeRequested)
        {
            return new SteamPlatformRuntime(
                new SteamRuntimeDependencies(lifecycle, achievements),
                baseSmokeRequested,
                achievementSmokeRequested,
                monotonicSeconds: () => 0d,
                smokeLogger: _ => { });
        }
    }
}
