using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Platform.Steam.Tests.EditMode
{
    public sealed class SteamPlatformRuntimeTests
    {
        [Test]
        public void PacksizeMismatch_SkipsNativeInitializeAndIsUnavailable()
        {
            var native = new FakeSteamNativeApi { PacksizeCompatible = false };
            var runtime = new SteamPlatformRuntime(native);

            var result = runtime.Initialize();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(native.InitializeCount, Is.Zero);
            Assert.That(runtime.SteamAvailability.IsAvailable, Is.False);
            Assert.That(runtime.Diagnostics.LastFailureReason,
                Is.EqualTo(SteamPlatformFailureReason.PacksizeMismatch));
        }

        [Test]
        public void InitializeReturnedFalse_SkipsIdentityCallbacksAndShutdown()
        {
            var native = new FakeSteamNativeApi { InitializeResult = false };
            var runtime = new SteamPlatformRuntime(native);

            runtime.Initialize();
            runtime.Tick();
            runtime.Shutdown();

            Assert.That(native.IdentityCount, Is.Zero);
            Assert.That(native.CallbackCount, Is.Zero);
            Assert.That(native.ShutdownCount, Is.Zero);
            Assert.That(runtime.Diagnostics.NativeInitializationResult,
                Is.EqualTo(SteamNativeInitializationResult.ReturnedFalse));
        }

        [TestCaseSource(nameof(NativeInitializationExceptions))]
        public void InitializeException_IsContainedWithExactDiagnostic(
            Exception exception,
            SteamPlatformFailureReason expectedReason)
        {
            var native = new FakeSteamNativeApi { InitializeException = exception };
            var runtime = new SteamPlatformRuntime(native);

            Assert.DoesNotThrow(() => runtime.Initialize());

            Assert.That(runtime.Diagnostics.LastFailureReason, Is.EqualTo(expectedReason));
            Assert.That(runtime.Diagnostics.LastExceptionType, Is.EqualTo(exception.GetType().Name));
            Assert.That(runtime.Diagnostics.State, Is.EqualTo(SteamPlatformRuntimeState.Faulted));
        }

        [Test]
        public void InitializeSuccess_IsAvailableAndIdempotent()
        {
            var native = new FakeSteamNativeApi();
            var runtime = new SteamPlatformRuntime(native);

            var first = runtime.Initialize();
            var second = runtime.Initialize();

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.ObservedAppId, Is.EqualTo(480));
            Assert.That(runtime.Diagnostics.SteamIdentityValid, Is.True);
            Assert.That(runtime.Diagnostics.DllCheckObservation.IndependentCompatibilitySignal, Is.False);
            Assert.That(runtime.Diagnostics.DllCheckObservation.LimitationReason,
                Is.EqualTo(SteamDllCheckObservation.UpstreamDisabledLimitation));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LoggedOnObservation_FlowsFromNativeSeamToDiagnostics(bool loggedOn)
        {
            var native = new FakeSteamNativeApi { LoggedOn = loggedOn };
            var runtime = new SteamPlatformRuntime(native);

            runtime.Initialize();

            Assert.That(runtime.Diagnostics.LoggedOn, Is.EqualTo(loggedOn));
            Assert.That(native.LoggedOnCount, Is.EqualTo(1));
        }

        [Test]
        public void OverlayCallback_IsRegisteredOnceObservedAndDisposedOnce()
        {
            var native = new FakeSteamNativeApi();
            var runtime = new SteamPlatformRuntime(native);

            runtime.Initialize();
            native.RaiseOverlayActivation(true);
            native.RaiseOverlayActivation(false);

            Assert.That(native.OverlayCallbackRegistrationCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.OverlayActiveCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.OverlayInactiveCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.LastOverlayActive, Is.False);

            runtime.Shutdown();
            runtime.Shutdown();
            native.RaiseOverlayActivation(true);

            Assert.That(native.OverlayCallbackDisposeCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.OverlayActiveCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.OverlayInactiveCount, Is.EqualTo(1));
        }

        [Test]
        public void SmokeTick_DelaysOverlayEnabledObservationUntilItBecomesTrue()
        {
            var native = new FakeSteamNativeApi { OverlayEnabled = false };
            var runtime = new SteamPlatformRuntime(
                native,
                smokeRequested: true,
                smokeLogger: _ => { });

            runtime.Initialize();
            Assert.That(runtime.Diagnostics.OverlayEnabledEverObserved, Is.False);

            native.OverlayEnabled = true;
            runtime.Tick();

            Assert.That(runtime.Diagnostics.OverlayEnabledEverObserved, Is.True);
        }

        [Test]
        public void ProbeDefaultOff_ProducesNoSmokeOutputOrExtraLifecycleCalls()
        {
            var logs = new List<string>();
            var native = new FakeSteamNativeApi();
            var runtime = new SteamPlatformRuntime(
                native,
                smokeRequested: false,
                smokeLogger: logs.Add);

            runtime.Initialize();
            runtime.Tick();
            runtime.Shutdown();
            runtime.Shutdown();

            Assert.That(logs, Is.Empty);
            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.EqualTo(1));
            Assert.That(native.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void SmokeOptIn_EmitsOneStructuredPrivateResultFromCurrentRuntime()
        {
            var logs = new List<string>();
            var native = new FakeSteamNativeApi
            {
                OverlayEnabled = true,
                LoggedOn = true,
            };
            var runtime = new SteamPlatformRuntime(
                native,
                smokeRequested: true,
                smokeLogger: logs.Add);

            runtime.Initialize();
            native.RaiseOverlayActivation(true);
            native.RaiseOverlayActivation(false);
            runtime.Tick();
            runtime.Shutdown();
            runtime.Shutdown();

            Assert.That(logs, Has.Count.EqualTo(1));
            var result = logs[0];
            Assert.That(result, Does.StartWith(SteamPlatformRuntime.SmokeResultPrefix + " "));
            Assert.That(result, Does.Contain("\"requestedProvider\":\"steam\""));
            Assert.That(result, Does.Contain("\"selectedProvider\":\"steam\""));
            Assert.That(result, Does.Contain(
                "\"selectionStatus\":\"ExplicitProviderSelected\""));
            Assert.That(result, Does.Contain("\"fallbackUsed\":false"));
            Assert.That(result, Does.Contain("\"initSucceeded\":true"));
            Assert.That(result, Does.Contain("\"observedAppId\":480"));
            Assert.That(result, Does.Contain("\"steamIdValid\":true"));
            Assert.That(result, Does.Contain("\"loggedOn\":true"));
            Assert.That(result, Does.Contain("\"callbackPumpSuccessCount\":1"));
            Assert.That(result, Does.Contain(
                "\"overlayEnabledEverObserved\":true"));
            Assert.That(result, Does.Contain("\"overlayActiveCount\":1"));
            Assert.That(result, Does.Contain("\"overlayInactiveCount\":1"));
            Assert.That(result, Does.Contain("\"lastOverlayActive\":false"));
            Assert.That(result, Does.Contain("\"nativeExceptionType\":\"none\""));
            Assert.That(result, Does.Contain("\"shutdownNativeCallCount\":1"));
            Assert.That(result, Does.Not.Contain("\"steamId\":"));
            Assert.That(result, Does.Not.Contain("persona").IgnoreCase);
            Assert.That(result, Does.Not.Contain("account").IgnoreCase);
            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.EqualTo(1));
            Assert.That(native.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void SmokeInitFailure_StillEmitsOneStructuredResult()
        {
            var logs = new List<string>();
            var native = new FakeSteamNativeApi { InitializeResult = false };
            var runtime = new SteamPlatformRuntime(
                native,
                smokeRequested: true,
                smokeLogger: logs.Add);

            runtime.Initialize();
            runtime.Shutdown();

            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0], Does.Contain("\"initSucceeded\":false"));
            Assert.That(logs[0], Does.Contain(
                "\"initializationFailureKind\":\"InitializationReturnedFalse\""));
            Assert.That(logs[0], Does.Contain(
                "\"selectionStatus\":\"RequestedProviderUnavailable\""));
            Assert.That(logs[0], Does.Contain("\"callbackPumpSuccessCount\":0"));
            Assert.That(logs[0], Does.Contain("\"shutdownNativeCallCount\":0"));
        }

        [Test]
        public void AppIdZero_LifecycleImmediatelyReleasesNativeStateAndNeverShutsDownTwice()
        {
            var native = new FakeSteamNativeApi { AppId = 0 };
            var runtime = new SteamPlatformRuntime(native);
            var lifecycle = CreateLifecycle(runtime);
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'steam' initialization failed: " +
                "AppIdUnavailable: SteamAPI initialized but returned AppID 0.");

            InvokeLifecycle(lifecycle, "InitializeOnce");

            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(native.ShutdownCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.Zero);
            Assert.That(GetLifecycleProperty<bool>(lifecycle, "HasActiveRuntime"), Is.False);
            Assert.That(GetLifecycleProperty<bool>(lifecycle, "TickEnabled"), Is.False);
            var availability = GetLifecycleProperty<PlatformAvailability>(
                lifecycle,
                "Availability");
            Assert.That(availability.IsAvailable, Is.False);
            Assert.That(availability.Reason,
                Is.EqualTo("AppIdUnavailable: SteamAPI initialized but returned AppID 0."));
            Assert.That(
                GetLifecycleProperty<PlatformInitializationResult>(
                    lifecycle,
                    "InitializationResult").FailureReason,
                Is.EqualTo("AppIdUnavailable: SteamAPI initialized but returned AppID 0."));
            var selection = GetLifecycleProperty<PlatformRuntimeSelectionResult>(
                lifecycle,
                "Selection");
            Assert.That(
                selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderUnavailable));
            Assert.That(selection.FallbackUsed, Is.False);

            Assert.That(runtime.Diagnostics.LastFailureReason,
                Is.EqualTo(SteamPlatformFailureReason.AppIdUnavailable));
            Assert.That(runtime.Diagnostics.State, Is.EqualTo(SteamPlatformRuntimeState.Shutdown));

            InvokeLifecycle(lifecycle, "ShutdownOnce");
            InvokeLifecycle(lifecycle, "ShutdownOnce");
            runtime.Tick();

            Assert.That(native.ShutdownCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.Zero);
        }

        [Test]
        public void Tick_OnlyPumpsWhileAvailableAndStopsAfterShutdown()
        {
            var native = new FakeSteamNativeApi();
            var runtime = new SteamPlatformRuntime(native);

            runtime.Tick();
            runtime.Initialize();
            runtime.Tick();
            runtime.Tick();
            runtime.Shutdown();
            runtime.Tick();

            Assert.That(native.CallbackCount, Is.EqualTo(2));
            Assert.That(runtime.Diagnostics.CallbackPumpCount, Is.EqualTo(2));
        }

        [Test]
        public void CallbackException_FaultsProviderAndStopsFutureCallbacks()
        {
            var native = new FakeSteamNativeApi
            {
                CallbackException = new InvalidOperationException("callback failed"),
            };
            var runtime = new SteamPlatformRuntime(native);
            runtime.Initialize();

            Assert.DoesNotThrow(() => runtime.Tick());
            runtime.Tick();

            Assert.That(native.CallbackCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.State, Is.EqualTo(SteamPlatformRuntimeState.Faulted));
            Assert.That(runtime.Diagnostics.LastFailureReason,
                Is.EqualTo(SteamPlatformFailureReason.CallbackException));
        }

        [Test]
        public void Shutdown_SucceedsExactlyOnceAfterNativeInitialization()
        {
            var native = new FakeSteamNativeApi();
            var runtime = new SteamPlatformRuntime(native);
            runtime.Initialize();

            runtime.Shutdown();
            runtime.Shutdown();

            Assert.That(native.ShutdownCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.ShutdownCallCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.State, Is.EqualTo(SteamPlatformRuntimeState.Shutdown));
        }

        [Test]
        public void ShutdownException_IsContainedAndNeverRetried()
        {
            var native = new FakeSteamNativeApi
            {
                ShutdownException = new InvalidOperationException("shutdown failed"),
            };
            var runtime = new SteamPlatformRuntime(native);
            runtime.Initialize();

            Assert.DoesNotThrow(() => runtime.Shutdown());
            runtime.Shutdown();

            Assert.That(native.ShutdownCount, Is.EqualTo(1));
            Assert.That(runtime.Diagnostics.LastFailureReason,
                Is.EqualTo(SteamPlatformFailureReason.ShutdownException));
        }

        [Test]
        public void Diagnostics_StoreNoRawAccountIdentifier()
        {
            var propertyNames = typeof(SteamPlatformDiagnostics).GetProperties();

            Assert.That(propertyNames,
                Has.None.Matches<System.Reflection.PropertyInfo>(property =>
                    property.Name.IndexOf("SteamId", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    property.PropertyType != typeof(bool)));
        }

        private static object[] NativeInitializationExceptions => new object[]
        {
            new object[] { new DllNotFoundException("missing"), SteamPlatformFailureReason.DllMissing },
            new object[] { new BadImageFormatException("bad image"), SteamPlatformFailureReason.BadImageFormat },
            new object[] { new EntryPointNotFoundException("missing entry"), SteamPlatformFailureReason.EntryPointMissing },
            new object[] { new InvalidOperationException("init"), SteamPlatformFailureReason.InitializationException },
        };

        private static object CreateLifecycle(IPlatformRuntime runtime)
        {
            var selectionFactory = typeof(PlatformRuntimeSelectionResult).GetMethod(
                "Success",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(selectionFactory, Is.Not.Null);
            var selection = selectionFactory.Invoke(
                null,
                new object[] { runtime.ProviderId, runtime });
            var lifecycleType = typeof(IPlatformRuntime).Assembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeLifecycle",
                throwOnError: true);
            return Activator.CreateInstance(
                lifecycleType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new[] { selection },
                culture: null);
        }

        private static void InvokeLifecycle(object lifecycle, string methodName)
        {
            var method = lifecycle.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(lifecycle, null);
        }

        private static T GetLifecycleProperty<T>(object lifecycle, string propertyName)
        {
            var property = lifecycle.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(lifecycle);
        }
    }
}
