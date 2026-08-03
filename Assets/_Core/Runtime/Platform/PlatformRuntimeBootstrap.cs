using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Platform.Runtime
{
    internal static class PlatformRuntimeBootstrap
    {
        private static bool bootstrapInProgress;
        private static bool testOverrideConfigured;
        private static bool automaticBootstrapSuppressedForTests;
        private static IPlatformRuntimeFactory testFactory;

        internal static bool AutomaticBootstrapSuppressedForTests =>
            automaticBootstrapSuppressedForTests;

        internal static bool HasTestFactory => testFactory != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubsystemState()
        {
            PlatformRuntimeRegistry.ResetForSubsystemRegistration();
            PlatformProviderSelection.ResetFromArguments(Environment.GetCommandLineArgs());
            PlatformRuntimeApplicationHost.ResetStaticOwnerForSubsystemRegistration();
            bootstrapInProgress = false;
            testOverrideConfigured = false;
            automaticBootstrapSuppressedForTests = false;
            testFactory = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunAutomaticBootstrap()
        {
            BootstrapNow();
        }

        internal static bool TryConfigureTestOverride(
            bool suppressAutomaticBootstrap,
            IPlatformRuntimeFactory fakeFactory,
            out string failureReason)
        {
            if (suppressAutomaticBootstrap && fakeFactory != null)
            {
                return RejectTestOverride(
                    "Platform test bootstrap suppression and fake factory injection cannot be combined.",
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
            automaticBootstrapSuppressedForTests = suppressAutomaticBootstrap;
            testFactory = fakeFactory;
            failureReason = string.Empty;
            return true;
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

        private static PlatformRuntimeApplicationHost BootstrapNow()
        {
            if (automaticBootstrapSuppressedForTests)
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
    }
}
