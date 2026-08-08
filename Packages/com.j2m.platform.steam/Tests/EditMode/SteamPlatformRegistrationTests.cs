using System;
using System.Reflection;
using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Platform.Steam.Tests.EditMode
{
    public sealed class SteamPlatformRegistrationTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            ResetRegistry();
        }

        [Test]
        public void RegisterFactory_RegistersSteamProviderWithoutParsingSelection()
        {
            var registration = SteamPlatformRegistration.RegisterFactory(
                () => new FakeSteamNativeApi());

            Assert.That(registration.IsSuccess, Is.True);
            Assert.That(registration.ProviderId, Is.EqualTo(SteamPlatformRuntime.ProviderId));
            Assert.That(GetRegisteredFactoryCount(), Is.EqualTo(1));
        }

        [Test]
        public void SmokeArgument_IsExplicitOptInAndDefaultOff()
        {
            Assert.That(
                SteamPlatformRegistration.IsSmokeRequested(Array.Empty<string>()),
                Is.False);
            Assert.That(
                SteamPlatformRegistration.IsSmokeRequested(new[]
                {
                    "VectorQuake.exe",
                    SteamPlatformRegistration.SmokeArgument,
                }),
                Is.True);
            Assert.That(
                SteamPlatformRegistration.IsSmokeRequested(new[]
                {
                    SteamPlatformRegistration.SmokeArgument + "=true",
                }),
                Is.False);
        }

        [Test]
        public void AchievementSmokeArgument_IsExactExplicitOptInAndDefaultOff()
        {
            Assert.That(
                SteamPlatformRegistration.IsAchievementSmokeRequested(
                    Array.Empty<string>()),
                Is.False);
            Assert.That(
                SteamPlatformRegistration.IsAchievementSmokeRequested(new[]
                {
                    "VectorQuake.exe",
                    SteamPlatformRegistration.AchievementSmokeArgument,
                }),
                Is.True);
            Assert.That(
                SteamPlatformRegistration.IsAchievementSmokeRequested(new[]
                {
                    SteamPlatformRegistration.AchievementSmokeArgument + "=true",
                }),
                Is.False);
        }

        [Test]
        public void DependenciesFactory_IsInvokedOnceOnlyForSelectedSteamRuntime()
        {
            var dependencyFactoryCount = 0;
            var adapter = new FakeSteamAdapter();
            Assert.That(
                SteamPlatformRegistration.RegisterDependenciesFactory(
                    () =>
                    {
                        dependencyFactoryCount++;
                        return new SteamRuntimeDependencies(adapter, adapter);
                    },
                    smokeRequested: true,
                    achievementSmokeRequested: true).IsSuccess,
                Is.True);
            InvokeRegistryMember("Seal");

            var selection = Select(ParseSelection("steam"));

            Assert.That(selection.Runtime, Is.TypeOf<SteamPlatformRuntime>());
            Assert.That(dependencyFactoryCount, Is.EqualTo(1));
        }

        [Test]
        public void RegisterFactory_RejectsNullNativeApiFactory()
        {
            Assert.That(
                () => SteamPlatformRegistration.RegisterFactory(null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(GetRegisteredFactoryCount(), Is.Zero);
        }

        [Test]
        public void NoSelection_WithSteamFactoryRegistered_PreservesLocalDefault()
        {
            Assert.That(
                SteamPlatformRegistration.RegisterFactory(() => new FakeSteamNativeApi()).IsSuccess,
                Is.True);
            InvokeRegistryMember("Seal");

            var selection = (PlatformRuntimeSelectionResult)InvokeRegistryMember("Select");

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.DefaultLocalSelected));
            Assert.That(selection.SelectedProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(selection.Runtime, Is.TypeOf<LocalPlatformRuntime>());
            Assert.That(selection.FallbackUsed, Is.True);
        }

        [Test]
        public void ExplicitSteam_WithRegisteredFactory_ResolvesSteam()
        {
            var nativeFactoryCount = 0;
            Assert.That(
                SteamPlatformRegistration.RegisterFactory(() =>
                {
                    nativeFactoryCount++;
                    return new FakeSteamNativeApi();
                }).IsSuccess,
                Is.True);
            InvokeRegistryMember("Seal");

            var selection = Select(ParseSelection("steam"));

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ExplicitProviderSelected));
            Assert.That(selection.SelectedProviderId, Is.EqualTo(SteamPlatformRuntime.ProviderId));
            Assert.That(selection.Runtime, Is.TypeOf<SteamPlatformRuntime>());
            Assert.That(selection.FallbackUsed, Is.False);
            Assert.That(nativeFactoryCount, Is.EqualTo(1));
        }

        [Test]
        public void SmokeOptIn_UsesTheSingleSelectedSteamRuntime()
        {
            var nativeFactoryCount = 0;
            Assert.That(
                SteamPlatformRegistration.RegisterFactory(
                    () =>
                    {
                        nativeFactoryCount++;
                        return new FakeSteamNativeApi();
                    },
                    smokeRequested: true).IsSuccess,
                Is.True);
            InvokeRegistryMember("Seal");

            var selection = Select(ParseSelection("steam"));

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ExplicitProviderSelected));
            Assert.That(selection.Runtime, Is.TypeOf<SteamPlatformRuntime>());
            Assert.That(nativeFactoryCount, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitOtherProvider_DoesNotCreateRegisteredSteamFactory()
        {
            var nativeFactoryCount = 0;
            Assert.That(
                SteamPlatformRegistration.RegisterFactory(() =>
                {
                    nativeFactoryCount++;
                    return new FakeSteamNativeApi();
                }).IsSuccess,
                Is.True);
            InvokeRegistryMember("Seal");
            LogAssert.Expect(
                LogType.Error,
                "Requested platform provider 'other-store' is not registered. " +
                "No fallback provider was selected because the request was explicit.");

            var selection = Select(ParseSelection("other-store"));

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderNotRegistered));
            Assert.That(selection.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId("other-store")));
            Assert.That(selection.Runtime, Is.Null);
            Assert.That(selection.FallbackUsed, Is.False);
            Assert.That(nativeFactoryCount, Is.Zero);
        }

        [Test]
        public void ConflictingRequest_DoesNotCreateRegisteredSteamFactory()
        {
            var nativeFactoryCount = 0;
            Assert.That(
                SteamPlatformRegistration.RegisterFactory(() =>
                {
                    nativeFactoryCount++;
                    return new FakeSteamNativeApi();
                }).IsSuccess,
                Is.True);
            InvokeRegistryMember("Seal");
            var request = ParseSelection("steam", "local");
            LogAssert.Expect(
                LogType.Error,
                "Multiple explicit platform provider selections were supplied.");

            var selection = Select(request);

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ConflictingProviderSelection));
            Assert.That(selection.Runtime, Is.Null);
            Assert.That(selection.FallbackUsed, Is.False);
            Assert.That(nativeFactoryCount, Is.Zero);
        }

        [Test]
        public void DuplicateSteamRegistration_UsesRegistryDuplicatePolicy()
        {
            var first = SteamPlatformRegistration.RegisterFactory(() => new FakeSteamNativeApi());
            LogAssert.Expect(
                LogType.Error,
                "Duplicate platform runtime factory registration for provider 'steam'.");
            var second = SteamPlatformRegistration.RegisterFactory(() => new FakeSteamNativeApi());

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.Status,
                Is.EqualTo(PlatformRuntimeRegistrationStatus.DuplicateProvider));
        }

        private static void ResetRegistry()
        {
            InvokeRegistryMember("ResetForSubsystemRegistration");
            var resetSelection = typeof(PlatformProviderSelection).GetMethod(
                "ResetFromArguments",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resetSelection, Is.Not.Null);
            resetSelection.Invoke(null, new object[] { Array.Empty<string>() });
        }

        private static int GetRegisteredFactoryCount()
        {
            var property = typeof(PlatformRuntimeRegistry).GetProperty(
                "RegisteredFactoryCount",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (int)property.GetValue(null);
        }

        private static PlatformProviderSelectionRequest ParseSelection(params string[] providerIds)
        {
            var arguments = new string[providerIds.Length * 2];
            for (var index = 0; index < providerIds.Length; index++)
            {
                arguments[index * 2] = PlatformProviderSelection.ProviderSelectionArgument;
                arguments[(index * 2) + 1] = providerIds[index];
            }

            var parse = typeof(PlatformProviderSelection).GetMethod(
                "ParseArguments",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(parse, Is.Not.Null);
            return (PlatformProviderSelectionRequest)parse.Invoke(
                null,
                new object[] { arguments });
        }

        private static PlatformRuntimeSelectionResult Select(
            PlatformProviderSelectionRequest request)
        {
            var select = typeof(PlatformRuntimeRegistry).GetMethod(
                "Select",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(PlatformProviderSelectionRequest) },
                null);
            Assert.That(select, Is.Not.Null);
            return (PlatformRuntimeSelectionResult)select.Invoke(
                null,
                new object[] { request });
        }

        private static object InvokeRegistryMember(string name)
        {
            var method = typeof(PlatformRuntimeRegistry).GetMethod(
                name,
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, null);
        }

        private sealed class FakeSteamAdapter : ISteamNativeApi, ISteamAchievementApi
        {
            public bool IsPacksizeCompatible() => true;
            public SteamDllCheckObservation ObserveDllCheck() =>
                SteamDllCheckObservation.UpstreamDisabled(true);
            public bool Initialize() => true;
            public void RunCallbacks() { }
            public void Shutdown() { }
            public uint GetAppId() => 480;
            public bool IsSteamIdValid() => true;
            public bool IsLoggedOn() => true;
            public bool IsOverlayEnabled() => false;
            public void RegisterOverlayActivationCallback(Action<bool> observer) { }
            public void DisposeOverlayActivationCallback() { }
            public uint GetNumAchievements() => 0;
            public string GetAchievementName(uint index) => string.Empty;
            public bool GetAchievement(string achievementName, out bool achieved)
            {
                achieved = false;
                return false;
            }
            public bool SetAchievement(string achievementName) => false;
            public bool StoreStats() => false;
            public void RegisterAchievementStoreCallbacks(
                Action<SteamStatsStoredObservation> statsStoredObserver,
                Action<SteamAchievementStoredObservation> achievementStoredObserver) { }
            public void DisposeAchievementStoreCallbacks() { }
        }
    }
}
