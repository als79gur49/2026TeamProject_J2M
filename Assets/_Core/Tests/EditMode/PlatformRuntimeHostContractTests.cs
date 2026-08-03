using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Platform.Tests.EditMode
{
    public sealed class PlatformRuntimeHostContractTests
    {
        [Test]
        public void Initialize_IsAttemptedExactlyOnceAndEnablesTick()
        {
            var runtime = new FakePlatformRuntime("fake");
            var lifecycle = CreateLifecycle(runtime);

            lifecycle.InitializeOnce();
            lifecycle.InitializeOnce();
            lifecycle.TickOnce();

            Assert.That(runtime.InitializeCount, Is.EqualTo(1));
            Assert.That(runtime.TickCount, Is.EqualTo(1));
            Assert.That(lifecycle.InitializationResult.IsSuccess, Is.True);
        }

        [Test]
        public void FailedInitialize_DisablesTickAndStillShutsDownOnce()
        {
            var runtime = new FakePlatformRuntime("failed")
            {
                InitializationResult = PlatformInitializationResult.Failure("not available"),
            };
            var lifecycle = CreateLifecycle(runtime);
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'failed' initialization failed: not available");

            lifecycle.InitializeOnce();
            lifecycle.TickOnce();

            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
            Assert.That(runtime.ResourceAcquired, Is.False);
            Assert.That(lifecycle.HasActiveRuntime, Is.False);
            Assert.That(lifecycle.TickEnabled, Is.False);

            lifecycle.ShutdownOnce();
            lifecycle.ShutdownOnce();

            Assert.That(runtime.InitializeCount, Is.EqualTo(1));
            Assert.That(runtime.TickCount, Is.Zero);
            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
            Assert.That(lifecycle.ProviderId, Is.EqualTo(new PlatformProviderId("failed")));
            Assert.That(
                lifecycle.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderUnavailable));
            Assert.That(lifecycle.Selection.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId("failed")));
            Assert.That(lifecycle.Selection.SelectedProviderId,
                Is.EqualTo(new PlatformProviderId("failed")));
            Assert.That(lifecycle.Selection.FallbackUsed, Is.False);
            Assert.That(lifecycle.Selection.FailureReason,
                Is.EqualTo("Platform provider 'failed' was selected but is unavailable: not available"));
        }

        [Test]
        public void SuccessfulInitializeWithUnavailableRuntime_FailsClosedAndCleansUpOnce()
        {
            var runtime = new FakePlatformRuntime("unavailable-after-initialize")
            {
                AvailabilityResult = PlatformAvailability.Unavailable("service unavailable"),
            };
            var lifecycle = CreateLifecycle(runtime);
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'unavailable-after-initialize' initialized but is unavailable: " +
                "service unavailable");

            lifecycle.InitializeOnce();
            lifecycle.TickOnce();

            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
            Assert.That(runtime.ResourceAcquired, Is.False);
            Assert.That(lifecycle.HasActiveRuntime, Is.False);
            Assert.That(lifecycle.TickEnabled, Is.False);

            lifecycle.ShutdownOnce();

            Assert.That(lifecycle.InitializationResult.IsSuccess, Is.True);
            Assert.That(lifecycle.Availability.IsAvailable, Is.False);
            Assert.That(lifecycle.TickEnabled, Is.False);
            Assert.That(lifecycle.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderUnavailable));
            Assert.That(lifecycle.Selection.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId("unavailable-after-initialize")));
            Assert.That(lifecycle.Selection.FallbackUsed, Is.False);
            Assert.That(lifecycle.Selection.FailureReason,
                Is.EqualTo("Platform provider 'unavailable-after-initialize' was selected but is unavailable: " +
                    "service unavailable"));
            Assert.That(runtime.InitializeCount, Is.EqualTo(1));
            Assert.That(runtime.TickCount, Is.Zero);
            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownAttempted, Is.True);
        }

        [Test]
        public void InitializeException_IsContainedWithoutChangingProviderIdentity()
        {
            var runtime = new FakePlatformRuntime("throw-init")
            {
                ThrowOnInitialize = true,
            };
            var lifecycle = CreateLifecycle(runtime);
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'throw-init' initialization threw InvalidOperationException with message 'initialize failure'.");

            lifecycle.InitializeOnce();
            lifecycle.TickOnce();

            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
            Assert.That(runtime.ResourceAcquired, Is.False);
            Assert.That(lifecycle.HasActiveRuntime, Is.False);
            Assert.That(lifecycle.TickEnabled, Is.False);

            lifecycle.ShutdownOnce();

            Assert.That(lifecycle.InitializationResult.IsSuccess, Is.False);
            Assert.That(lifecycle.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderUnavailable));
            Assert.That(lifecycle.ProviderId, Is.EqualTo(new PlatformProviderId("throw-init")));
            Assert.That(runtime.TickCount, Is.Zero);
            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void TickException_FailsClosedAndShutsDownExactlyOnce()
        {
            var runtime = new FakePlatformRuntime("throw-tick")
            {
                ThrowOnTick = true,
            };
            var lifecycle = CreateLifecycle(runtime);
            lifecycle.InitializeOnce();
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'throw-tick' tick threw InvalidOperationException with message 'tick failure'; further ticks are disabled.");

            lifecycle.TickOnce();
            lifecycle.TickOnce();
            lifecycle.ShutdownOnce();

            Assert.That(runtime.TickCount, Is.EqualTo(1));
            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
            Assert.That(lifecycle.Availability.IsAvailable, Is.False);
            Assert.That(lifecycle.HasActiveRuntime, Is.False);
            Assert.That(lifecycle.TickEnabled, Is.False);
            Assert.That(lifecycle.ShutdownAttempted, Is.True);
            Assert.That(lifecycle.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderUnavailable));
            Assert.That(lifecycle.Selection.FallbackUsed, Is.False);
            Assert.That(lifecycle.TickFailureReason, Does.Contain("tick failure"));
        }

        [Test]
        public void RuntimeBecomesUnavailableDuringTick_FailsClosedAndShutsDownOnce()
        {
            var runtime = new FakePlatformRuntime("degrades-on-tick");
            var lifecycle = CreateLifecycle(runtime);
            lifecycle.InitializeOnce();
            runtime.AvailabilityResult = PlatformAvailability.Unavailable("callback fault");
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'degrades-on-tick' became unavailable during tick: callback fault; further ticks are disabled.");

            lifecycle.TickOnce();
            lifecycle.TickOnce();
            lifecycle.ShutdownOnce();

            Assert.That(runtime.TickCount, Is.EqualTo(1));
            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
            Assert.That(lifecycle.Availability.IsAvailable, Is.False);
            Assert.That(lifecycle.HasActiveRuntime, Is.False);
            Assert.That(lifecycle.TickEnabled, Is.False);
            Assert.That(lifecycle.ShutdownAttempted, Is.True);
            Assert.That(lifecycle.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderUnavailable));
            Assert.That(lifecycle.Selection.FallbackUsed, Is.False);
            Assert.That(lifecycle.TickFailureReason, Does.Contain("callback fault"));
        }

        [Test]
        public void ShutdownException_IsContainedAndNotRetried()
        {
            var runtime = new FakePlatformRuntime("throw-shutdown")
            {
                ThrowOnShutdown = true,
            };
            var lifecycle = CreateLifecycle(runtime);
            lifecycle.InitializeOnce();
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'throw-shutdown' shutdown threw InvalidOperationException with message 'shutdown failure'.");

            lifecycle.ShutdownOnce();
            lifecycle.ShutdownOnce();

            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
            Assert.That(lifecycle.ShutdownAttempted, Is.True);
            Assert.That(lifecycle.ShutdownFailureReason, Is.Not.Empty);
        }

        [Test]
        public void SelectionFailure_NeverTicksAndHasNoFallbackRuntime()
        {
            var selection = PlatformRuntimeSelectionResult.Failure(
                PlatformRuntimeSelectionStatus.FactoryCreationFailed,
                new PlatformProviderId("failed-selection"),
                "factory did not create a runtime");
            var lifecycle = new PlatformRuntimeLifecycle(selection);
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime initialization was not attempted because selection failed: factory did not create a runtime");

            lifecycle.InitializeOnce();
            lifecycle.TickOnce();
            lifecycle.ShutdownOnce();

            Assert.That(lifecycle.ProviderId, Is.EqualTo(new PlatformProviderId("failed-selection")));
            Assert.That(lifecycle.InitializationResult.IsSuccess, Is.False);
            Assert.That(selection.Runtime, Is.Null);
        }

        private static PlatformRuntimeLifecycle CreateLifecycle(FakePlatformRuntime runtime)
        {
            return new PlatformRuntimeLifecycle(
                PlatformRuntimeSelectionResult.Success(runtime.ProviderId, runtime));
        }
    }
}
