using System.Collections;
using System.Reflection;
using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Platform.Steam.Tests.PlayMode
{
    public sealed class SteamPlatformRuntimePlayModeTests
    {
        private static readonly BindingFlags InternalStatic =
            BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly BindingFlags InternalInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            ResetPlatformFoundation();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyCanonicalHost();
            ResetPlatformFoundation();
        }

        [UnityTest]
        public IEnumerator FoundationHost_InitializesTicksAndShutsDownExactlyOnce()
        {
            var native = new CountingNativeApi();
            var host = BootstrapWithFactory(new SteamPlatformRuntimeFactory(() => native));

            yield return null;
            yield return null;

            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.GreaterThan(0));
            Assert.That(GetHostProviderId(host), Is.EqualTo(SteamPlatformRuntime.ProviderId));

            InvokeHostShutdown(host);
            Object.Destroy(((Component)host).gameObject);
            yield return null;

            Assert.That(native.ShutdownCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailedProvider_DoesNotCrashFoundationLoopOrPumpCallbacks()
        {
            var native = new CountingNativeApi { InitializeResult = false };
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'steam' initialization failed: " +
                "InitializationReturnedFalse: Native Steam initialization returned false.");
            var host = BootstrapWithFactory(new SteamPlatformRuntimeFactory(() => native));

            yield return null;
            yield return null;

            Assert.That(native.InitializeCount, Is.EqualTo(1));
            Assert.That(native.CallbackCount, Is.Zero);

            Object.Destroy(((Component)host).gameObject);
            yield return null;

            Assert.That(native.ShutdownCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CallbackFault_ReleasesLifecycleAndDoesNotPumpOrShutdownTwice()
        {
            var native = new CountingNativeApi();
            var host = BootstrapWithFactory(new SteamPlatformRuntimeFactory(() => native));

            yield return null;
            Assert.That(native.CallbackCount, Is.GreaterThan(0));
            native.CallbackException = new System.InvalidOperationException("callback fault");
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'steam' became unavailable during tick: " +
                "CallbackException: InvalidOperationException with message 'callback fault'; further ticks are disabled.");

            yield return null;
            var callbacksAfterFault = native.CallbackCount;
            yield return null;
            InvokeHostShutdown(host);
            Object.Destroy(((Component)host).gameObject);
            yield return null;

            Assert.That(native.CallbackCount, Is.EqualTo(callbacksAfterFault));
            Assert.That(native.ShutdownCount, Is.EqualTo(1));
            Assert.That(GetHostTickEnabled(host), Is.False);
        }

        [UnityTest]
        public IEnumerator LocalFoundationHost_HasNoSteamCallbackOwnership()
        {
            var native = new CountingNativeApi();
            var host = BootstrapWithFactory(null);

            yield return null;
            yield return null;

            Assert.That(GetHostProviderId(host), Is.EqualTo(PlatformProviderId.Local));
            Assert.That(native.InitializeCount, Is.Zero);
            Assert.That(native.CallbackCount, Is.Zero);
            Assert.That(native.ShutdownCount, Is.Zero);

            Object.Destroy(((Component)host).gameObject);
            yield return null;
        }

        private static object BootstrapWithFactory(IPlatformRuntimeFactory factory)
        {
            if (factory != null)
            {
                var configureOverride = GetBootstrapType().GetMethod(
                    "TryConfigureTestOverride",
                    InternalStatic,
                    null,
                    new[]
                    {
                        typeof(bool),
                        typeof(IPlatformRuntimeFactory),
                        typeof(string).MakeByRefType(),
                    },
                    null);
                Assert.That(configureOverride, Is.Not.Null);
                var arguments = new object[] { false, factory, null };
                var accepted = (bool)configureOverride.Invoke(null, arguments);
                Assert.That(accepted, Is.True, arguments[2] as string);
            }

            var bootstrapType = GetBootstrapType();
            var bootstrap = bootstrapType.GetMethod("BootstrapNowForTests", InternalStatic);
            Assert.That(bootstrap, Is.Not.Null);
            var host = bootstrap.Invoke(null, null);
            Assert.That(host, Is.Not.Null);
            return host;
        }

        private static PlatformProviderId GetHostProviderId(object host)
        {
            var providerProperty = host.GetType().GetProperty("ProviderId", InternalInstance);
            Assert.That(providerProperty, Is.Not.Null);
            return (PlatformProviderId)providerProperty.GetValue(host);
        }

        private static void InvokeHostShutdown(object host)
        {
            var shutdown = host.GetType().GetMethod("ShutdownOnce", InternalInstance);
            Assert.That(shutdown, Is.Not.Null);
            shutdown.Invoke(host, null);
        }

        private static bool GetHostTickEnabled(object host)
        {
            var tickEnabled = host.GetType().GetProperty("TickEnabled", InternalInstance);
            Assert.That(tickEnabled, Is.Not.Null);
            return (bool)tickEnabled.GetValue(host);
        }

        private static void DestroyCanonicalHost()
        {
            var hostType = typeof(LocalPlatformRuntime).Assembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeApplicationHost",
                throwOnError: true);
            var current = hostType.GetProperty("CurrentForTests", InternalStatic)?.GetValue(null);
            if (current is Component component)
            {
                Object.DestroyImmediate(component.gameObject);
            }
        }

        private static void ResetPlatformFoundation()
        {
            var reset = GetBootstrapType().GetMethod(
                "ResetSubsystemStateForTests",
                InternalStatic,
                null,
                System.Type.EmptyTypes,
                null);
            Assert.That(reset, Is.Not.Null);
            reset.Invoke(null, null);
        }

        private static System.Type GetBootstrapType()
        {
            return typeof(LocalPlatformRuntime).Assembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeBootstrap",
                throwOnError: true);
        }

        private sealed class CountingNativeApi : ISteamNativeApi
        {
            internal bool InitializeResult { get; set; } = true;
            internal int InitializeCount { get; private set; }
            internal int CallbackCount { get; private set; }
            internal int ShutdownCount { get; private set; }

            internal System.Exception CallbackException { get; set; }

            public bool IsPacksizeCompatible() => true;

            public SteamDllCheckObservation ObserveDllCheck() =>
                SteamDllCheckObservation.UpstreamDisabled(returnedValue: true);

            public bool Initialize()
            {
                InitializeCount++;
                return InitializeResult;
            }

            public void RunCallbacks()
            {
                CallbackCount++;
                if (CallbackException != null)
                {
                    throw CallbackException;
                }
            }

            public void Shutdown()
            {
                ShutdownCount++;
            }

            public uint GetAppId() => 480;

            public bool IsSteamIdValid() => true;

            public bool IsOverlayEnabled() => false;
        }
    }
}
