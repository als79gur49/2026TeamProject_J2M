using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Platform.Runtime
{
    public static class PlatformRuntimeRegistry
    {
        private static readonly Dictionary<PlatformProviderId, IPlatformRuntimeFactory> Factories =
            new Dictionary<PlatformProviderId, IPlatformRuntimeFactory>();

        private static readonly HashSet<string> ReportedDiagnostics =
            new HashSet<string>(StringComparer.Ordinal);

        private static bool isSealed;
        private static bool hasSelection;
        private static PlatformRuntimeSelectionResult selection;

        internal static bool IsSealed => isSealed;

        internal static int RegisteredFactoryCount => Factories.Count;

        public static PlatformRuntimeRegistrationResult RegisterFactory(IPlatformRuntimeFactory factory)
        {
            if (factory == null)
            {
                return ReportRegistrationFailure(
                    PlatformRuntimeRegistrationStatus.InvalidFactory,
                    default,
                    "Platform runtime factory cannot be null.");
            }

            PlatformProviderId providerId;
            try
            {
                providerId = factory.ProviderId;
            }
            catch (Exception exception)
            {
                return ReportRegistrationFailure(
                    PlatformRuntimeRegistrationStatus.InvalidFactory,
                    default,
                    "Platform runtime factory provider ID threw " + FormatException(exception) + ".");
            }

            if (!providerId.IsValid)
            {
                return ReportRegistrationFailure(
                    PlatformRuntimeRegistrationStatus.InvalidProviderId,
                    providerId,
                    "Platform runtime factory returned an invalid provider ID.");
            }

            if (isSealed)
            {
                return ReportRegistrationFailure(
                    PlatformRuntimeRegistrationStatus.RegistrySealed,
                    providerId,
                    "Platform runtime factory registration is closed after registry seal.");
            }

            if (Factories.ContainsKey(providerId))
            {
                return ReportRegistrationFailure(
                    PlatformRuntimeRegistrationStatus.DuplicateProvider,
                    providerId,
                    "Duplicate platform runtime factory registration for provider '" + providerId + "'.");
            }

            Factories.Add(providerId, factory);
            return PlatformRuntimeRegistrationResult.Success(providerId);
        }

        internal static void Seal()
        {
            isSealed = true;
        }

        internal static PlatformRuntimeSelectionResult Select()
        {
            return Select(PlatformProviderSelection.CurrentRequest);
        }

        internal static PlatformRuntimeSelectionResult Select(
            PlatformProviderSelectionRequest request)
        {
            if (hasSelection)
            {
                return selection;
            }

            if (!isSealed)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Failure(
                    PlatformRuntimeSelectionStatus.RegistryNotSealed,
                    request,
                    default,
                    Factories.Count,
                    false,
                    "Platform runtime registry must be sealed before selection."));
            }

            if (request.Kind == PlatformProviderSelectionKind.Invalid)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Failure(
                    PlatformRuntimeSelectionStatus.InvalidProviderSelection,
                    request,
                    default,
                    Factories.Count,
                    false,
                    request.FailureReason));
            }

            if (request.Kind == PlatformProviderSelectionKind.Conflicting)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Failure(
                    PlatformRuntimeSelectionStatus.ConflictingProviderSelection,
                    request,
                    default,
                    Factories.Count,
                    false,
                    request.FailureReason));
            }

            if (request.Kind == PlatformProviderSelectionKind.None)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.DefaultLocal(
                    request,
                    new LocalPlatformRuntime(),
                    Factories.Count));
            }

            var requestedProviderId = request.RequestedProviderId;
            if (requestedProviderId == PlatformProviderId.Local)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Explicit(
                    request,
                    PlatformProviderId.Local,
                    new LocalPlatformRuntime(),
                    Factories.Count,
                    true));
            }

            if (!Factories.TryGetValue(requestedProviderId, out var factory))
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Failure(
                    PlatformRuntimeSelectionStatus.RequestedProviderNotRegistered,
                    request,
                    default,
                    Factories.Count,
                    false,
                    "Requested platform provider '" + requestedProviderId +
                    "' is not registered. No fallback provider was selected because the request was explicit."));
            }

            IPlatformRuntime runtime;
            try
            {
                runtime = factory.Create();
            }
            catch (Exception exception)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Failure(
                    PlatformRuntimeSelectionStatus.FactoryCreationFailed,
                    request,
                    requestedProviderId,
                    Factories.Count,
                    true,
                    "Platform runtime factory for '" + requestedProviderId + "' threw " +
                    FormatException(exception) + "."));
            }

            if (runtime == null)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Failure(
                    PlatformRuntimeSelectionStatus.NullRuntime,
                    request,
                    requestedProviderId,
                    Factories.Count,
                    true,
                    "Platform runtime factory for '" + requestedProviderId + "' returned null."));
            }

            PlatformProviderId runtimeProviderId;
            try
            {
                runtimeProviderId = runtime.ProviderId;
            }
            catch (Exception exception)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Failure(
                    PlatformRuntimeSelectionStatus.InvalidRuntime,
                    request,
                    requestedProviderId,
                    Factories.Count,
                    true,
                    "Platform runtime provider ID for '" + requestedProviderId + "' threw " +
                    FormatException(exception) + "."));
            }

            if (!runtimeProviderId.IsValid || runtimeProviderId != requestedProviderId)
            {
                return CacheSelection(PlatformRuntimeSelectionResult.Failure(
                    PlatformRuntimeSelectionStatus.ProviderIdMismatch,
                    request,
                    requestedProviderId,
                    Factories.Count,
                    true,
                    "Platform runtime provider ID '" + runtimeProviderId +
                    "' does not match factory provider ID '" + requestedProviderId + "'."));
            }

            return CacheSelection(PlatformRuntimeSelectionResult.Explicit(
                request,
                requestedProviderId,
                runtime,
                Factories.Count,
                true));
        }

        internal static void ResetForSubsystemRegistration()
        {
            Factories.Clear();
            ReportedDiagnostics.Clear();
            isSealed = false;
            hasSelection = false;
            selection = default;
        }

        private static PlatformRuntimeSelectionResult CacheSelection(
            PlatformRuntimeSelectionResult result)
        {
            selection = result;
            hasSelection = true;
            if (!result.IsSuccess)
            {
                ReportOnce("selection:" + result.Status + ":" + result.FailureReason, result.FailureReason);
            }

            return result;
        }

        private static PlatformRuntimeRegistrationResult ReportRegistrationFailure(
            PlatformRuntimeRegistrationStatus status,
            PlatformProviderId providerId,
            string reason)
        {
            ReportOnce("registration:" + status + ":" + providerId + ":" + reason, reason);
            return PlatformRuntimeRegistrationResult.Failure(status, providerId, reason);
        }

        private static void ReportOnce(string key, string message)
        {
            if (ReportedDiagnostics.Add(key))
            {
                Debug.LogError(message);
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
