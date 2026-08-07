using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Platform.Steam.SteamworksNet.Tests.PlayMode
{
    public sealed class SteamworksNetNativeLoadPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return DestroyCanonicalHost();
            ResetSubsystemState();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyCanonicalHost();
            ResetSubsystemState();
        }

        [UnityTest]
        public IEnumerator ExplicitSteamWithoutAppId_LoadsNativeAndFailsTypedWithoutLocalFallback()
        {
            Assert.That(File.Exists("steam_appid.txt"), Is.False);
            ResetSubsystemState(new[]
            {
                "VectorQuake.exe",
                "-j2mPlatformProvider",
                "steam",
            });
            var registration = SteamPlatformRegistration.RegisterFactory(
                () => new SteamworksNetNativeApi());
            Assert.That(registration.IsSuccess, Is.True, registration.FailureReason);
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'steam' initialization failed: " +
                "InitializationReturnedFalse: Native Steam initialization returned false.");

            object host = null;
            Assert.DoesNotThrow(() => host = InvokeBootstrap());
            yield return null;

            Assert.That(host, Is.Not.Null);
            var selection = GetProperty<PlatformRuntimeSelectionResult>(host, "Selection");
            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderUnavailable));
            Assert.That(selection.FallbackUsed, Is.False);
            Assert.That(GetProperty<PlatformProviderId>(host, "ProviderId"),
                Is.EqualTo(new PlatformProviderId("steam")));
            Assert.That(GetProperty<bool>(host, "TickEnabled"), Is.False);
            Assert.That(GetProperty<PlatformAvailability>(host, "Availability").IsAvailable,
                Is.False);
            Assert.That(GetProperty<PlatformInitializationResult>(host, "InitializationResult")
                .IsSuccess, Is.False);

            var runtime = selection.Runtime as SteamPlatformRuntime;
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.Diagnostics.LastFailureReason,
                Is.EqualTo(SteamPlatformFailureReason.InitializationReturnedFalse));
            Assert.That(runtime.Diagnostics.LastExceptionType, Is.Empty);
            Assert.That(runtime.Diagnostics.NativeInitializationResult,
                Is.EqualTo(SteamNativeInitializationResult.ReturnedFalse));
            Assert.That(runtime.Diagnostics.CallbackAttemptCount, Is.Zero);
            Assert.That(runtime.Diagnostics.CallbackPumpCount, Is.Zero);
            Assert.That(runtime.Diagnostics.ShutdownCallCount, Is.Zero);
            Assert.That(File.Exists("steam_appid.txt"), Is.False);
        }

        private static IEnumerator DestroyCanonicalHost()
        {
            var hostType = PlatformRuntimeAssembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeApplicationHost",
                throwOnError: true);
            var hosts = Resources.FindObjectsOfTypeAll(hostType);
            foreach (var host in hosts)
            {
                var component = host as Component;
                if (component != null)
                {
                    Object.Destroy(component.gameObject);
                }
            }

            if (hosts.Length > 0)
            {
                yield return null;
            }
        }

        private static Assembly PlatformRuntimeAssembly => typeof(PlatformRuntimeRegistry).Assembly;

        private static void ResetSubsystemState(IReadOnlyList<string> arguments = null)
        {
            var bootstrapType = PlatformRuntimeAssembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeBootstrap",
                throwOnError: true);
            var parameterTypes = arguments == null
                ? System.Type.EmptyTypes
                : new[] { typeof(IReadOnlyList<string>) };
            var method = bootstrapType.GetMethod(
                "ResetSubsystemStateForTests",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, arguments == null ? null : new object[] { arguments });
        }

        private static object InvokeBootstrap()
        {
            var bootstrapType = PlatformRuntimeAssembly.GetType(
                "Game.Platform.Runtime.PlatformRuntimeBootstrap",
                throwOnError: true);
            var method = bootstrapType.GetMethod(
                "BootstrapNowForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, null);
        }

        private static T GetProperty<T>(object owner, string propertyName)
        {
            var property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(owner);
        }
    }
}
