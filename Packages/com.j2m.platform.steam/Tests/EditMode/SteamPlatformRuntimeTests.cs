using System;
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
