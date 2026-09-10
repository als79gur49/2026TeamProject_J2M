using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Platform.Runtime
{
    internal static class PlatformRuntimeBootstrap
    {
        private static bool bootstrapInProgress;
        private static bool testOverrideConfigured;
        private static int automaticBootstrapSuppressionLeaseCount;
        private static IPlatformRuntimeFactory testFactory;

        internal static bool AutomaticBootstrapSuppressedForTests =>
            automaticBootstrapSuppressionLeaseCount > 0;

        internal static bool HasTestFactory => testFactory != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubsystemState()
        {
            PlatformRuntimeRegistry.ResetForSubsystemRegistration();
            PlatformStartupDeferral.Reset();
            PlatformProviderSelection.ResetFromArguments(Environment.GetCommandLineArgs());
            PlatformRuntimeApplicationHost.ResetStaticOwnerForSubsystemRegistration();
            bootstrapInProgress = false;
            testOverrideConfigured = false;
            automaticBootstrapSuppressionLeaseCount = 0;
            testFactory = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunAutomaticBootstrap()
        {
            if (PlatformStartupDeferral.IsDeferred)
            {
                PlatformRuntimeRegistry.Seal();
                PlatformStartupDeferral.RegistrationFinished();
                return;
            }
            PlatformStartupDeferral.RegistrationFinished();
            BootstrapNow();
        }

        internal static bool TryConfigureTestOverride(
            bool suppressAutomaticBootstrap,
            IPlatformRuntimeFactory fakeFactory,
            out string failureReason)
        {
            if (suppressAutomaticBootstrap)
            {
                return RejectTestOverride(
                    "Platform test bootstrap suppression requires an explicit test session.",
                    out failureReason);
            }

            if (AutomaticBootstrapSuppressedForTests && fakeFactory != null)
            {
                return RejectTestOverride(
                    "Platform test factory injection cannot be combined with bootstrap suppression.",
                    out failureReason);
            }

            if (PlatformRuntimeRegistry.IsSealed)
            {
                return RejectTestOverride(
                    "Platform test bootstrap override cannot be configured after registry seal.",
                    out failureReason);
            }

            if (PlatformRuntimeApplicationHost.HasCanonicalHost)
            {
                return RejectTestOverride(
                    "Platform test bootstrap override cannot be configured after host creation.",
                    out failureReason);
            }

            if (testOverrideConfigured)
            {
                return RejectTestOverride(
                    "Platform test bootstrap override has already been configured.",
                    out failureReason);
            }

            testOverrideConfigured = true;
            testFactory = fakeFactory;
            failureReason = string.Empty;
            return true;
        }

        internal static IDisposable BeginAutomaticBootstrapSuppressionForTests()
        {
            if (PlatformRuntimeRegistry.IsSealed)
            {
                throw new InvalidOperationException(
                    "Platform test bootstrap suppression cannot begin after registry seal.");
            }

            if (PlatformRuntimeApplicationHost.HasCanonicalHost)
            {
                throw new InvalidOperationException(
                    "Platform test bootstrap suppression cannot begin after host creation.");
            }

            if (testOverrideConfigured && testFactory != null)
            {
                throw new InvalidOperationException(
                    "Platform test bootstrap suppression cannot be combined with factory injection.");
            }

            automaticBootstrapSuppressionLeaseCount++;
            return new AutomaticBootstrapSuppressionLease();
        }

        internal static PlatformRuntimeApplicationHost BootstrapNowForTests()
        {
            return BootstrapNow();
        }

        internal static void ResetSubsystemStateForTests()
        {
            ResetSubsystemState();
        }

        internal static void ResetSubsystemStateForTests(IReadOnlyList<string> arguments)
        {
            ResetSubsystemState();
            PlatformProviderSelection.ResetFromArguments(arguments);
        }

        internal static PlatformRuntimeApplicationHost ReleaseDeferredStartup() => BootstrapNow(true);

        private static PlatformRuntimeApplicationHost BootstrapNow(bool releaseDeferred = false)
        {
            if (PlatformStartupDeferral.IsDeferred && !releaseDeferred) return null;
            if (AutomaticBootstrapSuppressedForTests)
            {
                return null;
            }

            if (PlatformRuntimeApplicationHost.HasCanonicalHost)
            {
                return PlatformRuntimeApplicationHost.CurrentForTests;
            }

            if (bootstrapInProgress)
            {
                Debug.LogError("Re-entrant platform runtime bootstrap was rejected.");
                return PlatformRuntimeApplicationHost.CurrentForTests;
            }

            bootstrapInProgress = true;
            try
            {
                PlatformRuntimeSelectionResult selection;
                var request = PlatformProviderSelection.CurrentRequest;
                if (testFactory != null)
                {
                    var registration = PlatformRuntimeRegistry.RegisterFactory(testFactory);
                    if (!registration.IsSuccess)
                    {
                        PlatformRuntimeRegistry.Seal();
                        selection = PlatformRuntimeSelectionResult.Failure(
                            PlatformRuntimeSelectionStatus.RegistrationFailed,
                            registration.ProviderId,
                            "Platform test factory registration failed: " +
                            registration.FailureReason);
                        return PlatformRuntimeApplicationHost.CreateOrGet(selection);
                    }

                    request = PlatformProviderSelectionRequest.Explicit(
                        registration.ProviderId,
                        PlatformProviderSelection.TestOverrideSource);
                }

                PlatformRuntimeRegistry.Seal();
                selection = PlatformRuntimeRegistry.Select(request);
                return PlatformRuntimeApplicationHost.CreateOrGet(selection);
            }
            finally
            {
                bootstrapInProgress = false;
            }
        }

        private static bool RejectTestOverride(string reason, out string failureReason)
        {
            failureReason = reason;
            Debug.LogError(reason);
            return false;
        }

        private sealed class AutomaticBootstrapSuppressionLease : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                if (automaticBootstrapSuppressionLeaseCount > 0)
                {
                    automaticBootstrapSuppressionLeaseCount--;
                }
            }
        }
    }
}
