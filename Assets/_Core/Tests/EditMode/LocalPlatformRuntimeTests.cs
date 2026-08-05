using Game.Platform.Runtime;
using NUnit.Framework;

namespace Game.Platform.Tests.EditMode
{
    public sealed class LocalPlatformRuntimeTests
    {
        [Test]
        public void LocalRuntime_ReportsProductionAvailabilityAndInitializationSuccess()
        {
            var runtime = new LocalPlatformRuntime();

            Assert.That(runtime.ProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(runtime.Availability.IsAvailable, Is.True);
            Assert.That(runtime.Availability.Reason, Is.Empty);
            Assert.That(runtime.Initialize().IsSuccess, Is.True);
        }

        [Test]
        public void Initialize_IsIdempotentBeforeShutdown()
        {
            var runtime = new LocalPlatformRuntime();

            Assert.That(runtime.Initialize().IsSuccess, Is.True);
            Assert.That(runtime.Initialize().IsSuccess, Is.True);
            Assert.That(runtime.Initialize().IsSuccess, Is.True);
        }

        [Test]
        public void Tick_IsSafeAndShutdownIsIdempotent()
        {
            var runtime = new LocalPlatformRuntime();
            runtime.Initialize();

            Assert.DoesNotThrow(runtime.Tick);
            Assert.DoesNotThrow(runtime.Shutdown);
            Assert.DoesNotThrow(runtime.Shutdown);
        }

        [Test]
        public void InitializeAfterShutdown_FailsWithoutChangingProviderIdentity()
        {
            var runtime = new LocalPlatformRuntime();
            runtime.Shutdown();

            var result = runtime.Initialize();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(runtime.ProviderId, Is.EqualTo(PlatformProviderId.Local));
        }

        [Test]
        public void FailureValueModels_RejectEmptyReasons()
        {
            Assert.That(
                () => PlatformAvailability.Unavailable(string.Empty),
                Throws.ArgumentException);
            Assert.That(
                () => PlatformInitializationResult.Failure(" "),
                Throws.ArgumentException);
            Assert.That(default(PlatformAvailability).Reason, Is.Not.Empty);
            Assert.That(default(PlatformInitializationResult).FailureReason, Is.Not.Empty);
        }
    }
}
