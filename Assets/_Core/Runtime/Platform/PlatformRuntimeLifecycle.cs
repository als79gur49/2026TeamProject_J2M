using System;
using UnityEngine;

namespace Game.Platform.Runtime
{
    internal sealed class PlatformRuntimeLifecycle
    {
        private PlatformRuntimeSelectionResult selection;
        private readonly IPlatformRuntime candidateRuntime;
        private IPlatformRuntime activeRuntime;

        private bool initializationAttempted;
        private bool tickEnabled;
        private bool shutdownAttempted;
        private PlatformInitializationResult initializationResult;
        private PlatformAvailability availability;
        private string tickFailureReason = string.Empty;
        private string shutdownFailureReason = string.Empty;

        internal PlatformRuntimeLifecycle(PlatformRuntimeSelectionResult selection)
        {
            this.selection = selection;
            candidateRuntime = selection.Runtime;
            availability = ResolveAvailability(selection, candidateRuntime);
        }

        internal PlatformRuntimeSelectionResult Selection => selection;

        internal PlatformProviderId ProviderId => selection.SelectedProviderId;

        internal PlatformAvailability Availability => availability;

        internal PlatformInitializationResult InitializationResult => initializationResult;

        internal bool InitializationAttempted => initializationAttempted;

        internal bool TickEnabled => tickEnabled && !shutdownAttempted;

        internal bool ShutdownAttempted => shutdownAttempted;

        internal bool HasActiveRuntime => activeRuntime != null;

        internal string TickFailureReason => tickFailureReason;

        internal string ShutdownFailureReason => shutdownFailureReason;

        internal void InitializeOnce()
        {
            if (initializationAttempted)
            {
                return;
            }

            initializationAttempted = true;
            var candidate = candidateRuntime;
            if (!selection.IsSuccess || candidate == null)
            {
                initializationResult = PlatformInitializationResult.Failure(
                    "Platform runtime initialization was not attempted because selection failed: " +
                    selection.FailureReason);
                Debug.LogError(initializationResult.FailureReason);
                return;
            }

            try
            {
                initializationResult = candidate.Initialize();
                availability = ResolveAvailability(selection, candidate);
                if (!initializationResult.IsSuccess)
                {
                    if (selection.SelectionKind == PlatformProviderSelectionKind.Explicit)
                    {
                        selection = PlatformRuntimeSelectionResult.Unavailable(
                            selection,
                            initializationResult.FailureReason);
                    }

                    Debug.LogError(
                        "Platform runtime '" + selection.SelectedProviderId +
                        "' initialization failed: " + initializationResult.FailureReason);
                    ShutdownCandidateOnce(candidate);
                    return;
                }

                if (!availability.IsAvailable)
                {
                    tickEnabled = false;
                    if (selection.SelectionKind == PlatformProviderSelectionKind.Explicit)
                    {
                        selection = PlatformRuntimeSelectionResult.Unavailable(
                            selection,
                            availability.Reason);
                    }

                    Debug.LogError(
                        "Platform runtime '" + selection.SelectedProviderId +
                        "' initialized but is unavailable: " + availability.Reason);
                    ShutdownCandidateOnce(candidate);
                    return;
                }

                activeRuntime = candidate;
                tickEnabled = true;
            }
            catch (Exception exception)
            {
                initializationResult = PlatformInitializationResult.Failure(
                    "Platform runtime '" + selection.SelectedProviderId +
                    "' initialization threw " + FormatException(exception) + ".");
                tickEnabled = false;
                if (selection.SelectionKind == PlatformProviderSelectionKind.Explicit)
                {
                    selection = PlatformRuntimeSelectionResult.Unavailable(
                        selection,
                        initializationResult.FailureReason);
                }

                Debug.LogError(initializationResult.FailureReason);
                ShutdownCandidateOnce(candidate);
            }
        }

        internal void TickOnce()
        {
            if (!TickEnabled)
            {
                return;
            }

            try
            {
                var runtime = activeRuntime;
                runtime.Tick();
                availability = ResolveAvailability(selection, runtime);
                if (!availability.IsAvailable)
                {
                    FailClosedAfterRuntimeDegradation();
                }
            }
            catch (Exception exception)
            {
                var failureReason =
                    "Platform runtime '" + selection.SelectedProviderId +
                    "' tick threw " + FormatException(exception) +
                    "; further ticks are disabled.";
                FailClosedActiveRuntime(
                    PlatformAvailability.Unavailable(failureReason),
                    failureReason);
            }
        }

        internal void ShutdownOnce()
        {
            tickEnabled = false;
            var runtime = activeRuntime;
            activeRuntime = null;
            ShutdownRuntimeOnce(runtime);
        }

        private void FailClosedAfterRuntimeDegradation()
        {
            FailClosedActiveRuntime(
                availability,
                "Platform runtime '" + selection.SelectedProviderId +
                "' became unavailable during tick: " + availability.Reason +
                "; further ticks are disabled.");
        }

        private void FailClosedActiveRuntime(
            PlatformAvailability unavailableAvailability,
            string diagnosticReason)
        {
            tickEnabled = false;
            var runtime = activeRuntime;
            activeRuntime = null;
            availability = unavailableAvailability;
            if (selection.SelectionKind == PlatformProviderSelectionKind.Explicit)
            {
                selection = PlatformRuntimeSelectionResult.Unavailable(selection, availability.Reason);
            }

            tickFailureReason = diagnosticReason;
            Debug.LogError(tickFailureReason);
            ShutdownRuntimeOnce(runtime);
        }

        private void ShutdownCandidateOnce(IPlatformRuntime candidate)
        {
            ShutdownRuntimeOnce(candidate);
        }

        private void ShutdownRuntimeOnce(IPlatformRuntime runtime)
        {
            if (shutdownAttempted)
            {
                return;
            }

            shutdownAttempted = true;
            if (runtime == null)
            {
                return;
            }

            try
            {
                runtime.Shutdown();
            }
            catch (Exception exception)
            {
                shutdownFailureReason =
                    "Platform runtime '" + selection.SelectedProviderId +
                    "' shutdown threw " + FormatException(exception) + ".";
                Debug.LogError(shutdownFailureReason);
            }
        }

        private static PlatformAvailability ResolveAvailability(
            PlatformRuntimeSelectionResult selection,
            IPlatformRuntime runtime)
        {
            if (!selection.IsSuccess || runtime == null)
            {
                return PlatformAvailability.Unavailable(
                    "No platform runtime is available because selection failed: " +
                    selection.FailureReason);
            }

            try
            {
                return runtime.Availability;
            }
            catch (Exception exception)
            {
                return PlatformAvailability.Unavailable(
                    "Platform runtime '" + selection.SelectedProviderId +
                    "' availability query threw " + FormatException(exception) + ".");
            }
        }

        private static string FormatException(Exception exception)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message)
                ? "without a message"
                : "with message '" + exception.Message + "'";
            return exception.GetType().Name + " " + message;
        }
    }
}
