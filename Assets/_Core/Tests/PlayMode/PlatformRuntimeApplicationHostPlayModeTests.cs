using System.Collections;
using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Platform.Tests.PlayMode
{
    public sealed class PlatformRuntimeApplicationHostPlayModeTests
    {
        private System.IDisposable bootstrapSuppressionSession;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return DestroyCanonicalHost();
            PlatformRuntimeBootstrap.ResetSubsystemStateForTests();
            bootstrapSuppressionSession = PlatformRuntimeTestBootstrap.BeginBootstrapSuppression();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyCanonicalHost();
            bootstrapSuppressionSession?.Dispose();
            bootstrapSuppressionSession = null;
            PlatformRuntimeBootstrap.ResetSubsystemStateForTests();
        }

        [UnityTest]
        public IEnumerator ExplicitSuppression_PreventsAutomaticHostCreation()
        {
            Assert.That(PlatformRuntimeBootstrap.AutomaticBootstrapSuppressedForTests, Is.True);

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            Assert.That(host, Is.Null);
            Assert.That(PlatformRuntimeApplicationHost.CurrentForTests, Is.Null);
        }

        [UnityTest]
        public IEnumerator CommandLine_NoSelection_SelectsDefaultLocal()
        {
            ResetWithArguments();

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            Assert.That(host, Is.Not.Null);
            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.DefaultLocalSelected));
            Assert.That(host.Selection.SelectionKind,
                Is.EqualTo(PlatformProviderSelectionKind.None));
            Assert.That(host.ProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(host.Selection.FallbackUsed, Is.True);
            Assert.That(host.InitializationResult.IsSuccess, Is.True);
        }

        [UnityTest]
        public IEnumerator CommandLine_ExplicitLocal_SelectsLocalExplicitly()
        {
            ResetWithArguments(
                PlatformProviderSelection.ProviderSelectionArgument,
                "local");

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ExplicitProviderSelected));
            Assert.That(host.Selection.RequestedProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(host.ProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(host.Selection.FallbackUsed, Is.False);
            Assert.That(host.InitializationResult.IsSuccess, Is.True);
        }

        [UnityTest]
        public IEnumerator CommandLine_ExplicitSteamWithoutFactory_FailsClosed()
        {
            var localRuntime = new CountingPlatformRuntime("local");
            var localFactory = new CountingPlatformRuntimeFactory(localRuntime);
            ResetWithArguments(
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam");
            Assert.That(PlatformRuntimeRegistry.RegisterFactory(localFactory).IsSuccess, Is.True);
            ExpectSelectionFailureLogs(
                "Requested platform provider 'steam' is not registered. " +
                "No fallback provider was selected because the request was explicit.");

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderNotRegistered));
            Assert.That(host.Selection.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId("steam")));
            Assert.That(host.Selection.HasSelectedProviderId, Is.False);
            Assert.That(host.Selection.FallbackUsed, Is.False);
            Assert.That(host.TickEnabled, Is.False);
            Assert.That(localFactory.CreateCount, Is.Zero);
            Assert.That(localRuntime.InitializeCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CommandLine_ExplicitUnknownProvider_FailsClosed()
        {
            ResetWithArguments(
                PlatformProviderSelection.ProviderSelectionArgument,
                "unknown-store");
            ExpectSelectionFailureLogs(
                "Requested platform provider 'unknown-store' is not registered. " +
                "No fallback provider was selected because the request was explicit.");

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderNotRegistered));
            Assert.That(host.Selection.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId("unknown-store")));
            Assert.That(host.ProviderId.IsValid, Is.False);
            Assert.That(host.TickEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator CommandLine_MissingProviderValue_IsInvalid()
        {
            ResetWithArguments(PlatformProviderSelection.ProviderSelectionArgument);
            ExpectSelectionFailureLogs(
                PlatformProviderSelection.ProviderSelectionArgument +
                " requires a provider ID value.");

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.InvalidProviderSelection));
            Assert.That(host.Selection.SelectionKind,
                Is.EqualTo(PlatformProviderSelectionKind.Invalid));
            Assert.That(host.Selection.FallbackUsed, Is.False);
            Assert.That(host.TickEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator CommandLine_SwitchInsteadOfProviderValue_IsInvalidWithoutLocalFallback()
        {
            ResetWithArguments(
                PlatformProviderSelection.ProviderSelectionArgument,
                "-batchmode");
            ExpectSelectionFailureLogs(
                PlatformProviderSelection.ProviderSelectionArgument +
                " requires a provider ID value.");

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.InvalidProviderSelection));
            Assert.That(host.Selection.HasRequestedProviderId, Is.False);
            Assert.That(host.Selection.FallbackUsed, Is.False);
            Assert.That(host.TickEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator CommandLine_ConflictingProviderValues_AreRejected()
        {
            ResetWithArguments(
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
                PlatformProviderSelection.ProviderSelectionArgument,
                "local");
            ExpectSelectionFailureLogs(
                "Multiple explicit platform provider selections were supplied.");

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ConflictingProviderSelection));
            Assert.That(host.Selection.SelectionKind,
                Is.EqualTo(PlatformProviderSelectionKind.Conflicting));
            Assert.That(host.ProviderId.IsValid, Is.False);
            Assert.That(host.TickEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator CommandLine_ExplicitSteamWithFactory_OwnsLifecycleExclusively()
        {
            var steamRuntime = new CountingPlatformRuntime("steam");
            var steamFactory = new CountingPlatformRuntimeFactory(steamRuntime);
            var localRuntime = new CountingPlatformRuntime("local");
            var localFactory = new CountingPlatformRuntimeFactory(localRuntime);
            ResetWithArguments(
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam");
            Assert.That(PlatformRuntimeRegistry.RegisterFactory(localFactory).IsSuccess, Is.True);
            Assert.That(PlatformRuntimeRegistry.RegisterFactory(steamFactory).IsSuccess, Is.True);

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;
            yield return null;
            host.ShutdownOnce();

            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ExplicitProviderSelected));
            Assert.That(host.ProviderId, Is.EqualTo(new PlatformProviderId("steam")));
            Assert.That(steamFactory.CreateCount, Is.EqualTo(1));
            Assert.That(steamRuntime.InitializeCount, Is.EqualTo(1));
            Assert.That(steamRuntime.TickCount, Is.GreaterThan(0));
            Assert.That(steamRuntime.ShutdownCount, Is.EqualTo(1));
            Assert.That(localFactory.CreateCount, Is.Zero);
            Assert.That(localRuntime.InitializeCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CommandLine_ExplicitSteamUnavailable_DoesNotFallbackToLocal()
        {
            var steamRuntime = new CountingPlatformRuntime("steam")
            {
                AvailabilityResult = PlatformAvailability.Unavailable("DllMissing"),
            };
            var steamFactory = new CountingPlatformRuntimeFactory(steamRuntime);
            var localRuntime = new CountingPlatformRuntime("local");
            var localFactory = new CountingPlatformRuntimeFactory(localRuntime);
            ResetWithArguments(
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam");
            Assert.That(PlatformRuntimeRegistry.RegisterFactory(localFactory).IsSuccess, Is.True);
            Assert.That(PlatformRuntimeRegistry.RegisterFactory(steamFactory).IsSuccess, Is.True);
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime 'steam' initialized but is unavailable: DllMissing");

            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;
            host.ShutdownOnce();

            Assert.That(host.Selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderUnavailable));
            Assert.That(host.Selection.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId("steam")));
            Assert.That(host.Selection.SelectedProviderId,
                Is.EqualTo(new PlatformProviderId("steam")));
            Assert.That(host.Selection.FailureReason,
                Is.EqualTo("Platform provider 'steam' was selected but is unavailable: DllMissing"));
            Assert.That(host.Selection.FallbackUsed, Is.False);
            Assert.That(steamFactory.CreateCount, Is.EqualTo(1));
            Assert.That(steamRuntime.InitializeCount, Is.EqualTo(1));
            Assert.That(steamRuntime.TickCount, Is.Zero);
            Assert.That(steamRuntime.ShutdownCount, Is.EqualTo(1));
            Assert.That(host.TickEnabled, Is.False);
            Assert.That(localFactory.CreateCount, Is.Zero);
            Assert.That(localRuntime.InitializeCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FakeInjection_UsesNormalHostLifecycleAndTicks()
        {
            var runtime = new CountingPlatformRuntime("play-fake");
            var factory = new CountingPlatformRuntimeFactory(runtime);
            var host = BootstrapWithFactory(factory);

            yield return null;
            yield return null;

            Assert.That(host, Is.Not.Null);
            Assert.That(host.OwnsLifecycle, Is.True);
            Assert.That(host.InitializationResult.IsSuccess, Is.True);
            Assert.That(runtime.InitializeCount, Is.EqualTo(1));
            Assert.That(runtime.TickCount, Is.GreaterThan(0));
            Assert.That(factory.CreateCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesExactlyOnePersistentCanonicalHost()
        {
            var runtime = new CountingPlatformRuntime("single-host");
            var factory = new CountingPlatformRuntimeFactory(runtime);
            var first = BootstrapWithFactory(factory);

            var second = PlatformRuntimeBootstrap.BootstrapNowForTests();
            yield return null;

            var hosts = Object.FindObjectsByType<PlatformRuntimeApplicationHost>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(second, Is.SameAs(first));
            Assert.That(hosts, Has.Length.EqualTo(1));
            Assert.That(first.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(runtime.InitializeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DuplicateHost_NeverOwnsInitializeTickOrShutdown()
        {
            var canonicalRuntime = new CountingPlatformRuntime("canonical");
            var canonical = BootstrapWithFactory(
                new CountingPlatformRuntimeFactory(canonicalRuntime));
            var duplicateRuntime = new CountingPlatformRuntime("duplicate");
            var duplicateSelection = PlatformRuntimeSelectionResult.Success(
                duplicateRuntime.ProviderId,
                duplicateRuntime);
            LogAssert.Expect(
                LogType.Error,
                "Duplicate platform runtime application host was rejected; the canonical host retains lifecycle ownership.");
            var duplicateObject = new GameObject("DuplicatePlatformHost");
            var duplicate = duplicateObject.AddComponent<PlatformRuntimeApplicationHost>();

            Assert.That(duplicate.ConfigureAndInitialize(duplicateSelection), Is.False);
            yield return null;
            canonical.ShutdownOnce();

            Assert.That(duplicateRuntime.InitializeCount, Is.Zero);
            Assert.That(duplicateRuntime.TickCount, Is.Zero);
            Assert.That(duplicateRuntime.ShutdownCount, Is.Zero);
            Assert.That(canonicalRuntime.InitializeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ShutdownOnce_ConvergesManualAndDestroyTeardown()
        {
            var runtime = new CountingPlatformRuntime("shutdown-once");
            var host = BootstrapWithFactory(new CountingPlatformRuntimeFactory(runtime));

            host.ShutdownOnce();
            host.ShutdownOnce();
            Object.Destroy(host.gameObject);
            yield return null;

            Assert.That(runtime.ShutdownCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SuppressionAndFakeInjectionCombination_IsRejected()
        {
            PlatformRuntimeBootstrap.ResetSubsystemStateForTests();
            var runtime = new CountingPlatformRuntime("invalid-override");
            LogAssert.Expect(
                LogType.Error,
                "Platform test bootstrap suppression requires an explicit test session.");

            var accepted = PlatformRuntimeBootstrap.TryConfigureTestOverride(
                suppressAutomaticBootstrap: true,
                fakeFactory: new CountingPlatformRuntimeFactory(runtime),
                out var failureReason);
            yield return null;

            Assert.That(accepted, Is.False);
            Assert.That(failureReason, Is.Not.Empty);
            Assert.That(PlatformRuntimeApplicationHost.CurrentForTests, Is.Null);
        }

        [UnityTest]
        public IEnumerator OverrideAfterHostCreation_IsRejected()
        {
            var runtime = new CountingPlatformRuntime("created-host");
            BootstrapWithFactory(new CountingPlatformRuntimeFactory(runtime));
            LogAssert.Expect(
                LogType.Error,
                "Platform test bootstrap suppression requires an explicit test session.");

            var accepted = PlatformRuntimeBootstrap.TryConfigureTestOverride(
                suppressAutomaticBootstrap: true,
                fakeFactory: null,
                out var failureReason);
            yield return null;

            Assert.That(accepted, Is.False);
            Assert.That(failureReason, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator SubsystemReset_ClearsAllTestOverrideState()
        {
            Assert.That(PlatformRuntimeBootstrap.AutomaticBootstrapSuppressedForTests, Is.True);

            PlatformRuntimeBootstrap.ResetSubsystemStateForTests();
            yield return null;

            Assert.That(PlatformRuntimeBootstrap.AutomaticBootstrapSuppressedForTests, Is.False);
            Assert.That(PlatformRuntimeBootstrap.HasTestFactory, Is.False);
            Assert.That(PlatformRuntimeRegistry.IsSealed, Is.False);
        }

        [UnityTest]
        public IEnumerator NestedSuppressionSessions_RestoreDefaultOnlyAfterFinalDispose()
        {
            var nested = PlatformRuntimeTestBootstrap.BeginBootstrapSuppression();
            Assert.That(PlatformRuntimeBootstrap.AutomaticBootstrapSuppressedForTests, Is.True);

            nested.Dispose();
            Assert.That(PlatformRuntimeBootstrap.AutomaticBootstrapSuppressedForTests, Is.True);

            bootstrapSuppressionSession.Dispose();
            bootstrapSuppressionSession = null;
            yield return null;

            Assert.That(PlatformRuntimeBootstrap.AutomaticBootstrapSuppressedForTests, Is.False);
        }

        private static PlatformRuntimeApplicationHost BootstrapWithFactory(
            CountingPlatformRuntimeFactory factory)
        {
            PlatformRuntimeBootstrap.ResetSubsystemStateForTests();
            Assert.That(
                PlatformRuntimeBootstrap.TryConfigureTestOverride(
                    suppressAutomaticBootstrap: false,
                    fakeFactory: factory,
                    out var failureReason),
                Is.True,
                failureReason);
            return PlatformRuntimeBootstrap.BootstrapNowForTests();
        }

        private static void ResetWithArguments(params string[] arguments)
        {
            PlatformRuntimeBootstrap.ResetSubsystemStateForTests(arguments);
        }

        private static void ExpectSelectionFailureLogs(string failureReason)
        {
            LogAssert.Expect(LogType.Error, failureReason);
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime initialization was not attempted because selection failed: " +
                failureReason);
        }

        private static IEnumerator DestroyCanonicalHost()
        {
            var hosts = Object.FindObjectsByType<PlatformRuntimeApplicationHost>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                Object.Destroy(host.gameObject);
            }

            if (hosts.Length > 0)
            {
                yield return null;
            }
        }
    }
}
