using System;

namespace Game.Platform.Runtime
{
    public enum PlatformRuntimeSelectionStatus
    {
        DefaultLocalSelected = 0,
        ExplicitProviderSelected = 1,
        RequestedProviderNotRegistered = 2,
        RequestedProviderUnavailable = 3,
        InvalidProviderSelection = 4,
        ConflictingProviderSelection = 5,
        RegistryNotSealed = 6,
        FactoryCreationFailed = 7,
        NullRuntime = 8,
        ProviderIdMismatch = 9,
        InvalidRuntime = 10,
        RegistrationFailed = 11,
    }

    public readonly struct PlatformRuntimeSelectionResult
    {
        private readonly string failureReason;

        private PlatformRuntimeSelectionResult(
            PlatformRuntimeSelectionStatus status,
            PlatformProviderSelectionRequest request,
            PlatformProviderId selectedProviderId,
            IPlatformRuntime runtime,
            int registeredProviderCount,
            bool requestedProviderRegistered,
            bool fallbackUsed,
            string failureReason)
        {
            Status = status;
            SelectionKind = request.Kind;
            SelectionSource = request.Source;
            RequestedProviderId = request.RequestedProviderId;
            SelectedProviderId = selectedProviderId;
            Runtime = runtime;
            RegisteredProviderCount = registeredProviderCount;
            RequestedProviderRegistered = requestedProviderRegistered;
            FallbackUsed = fallbackUsed;
            this.failureReason = failureReason;
        }

        public PlatformRuntimeSelectionStatus Status { get; }

        public bool IsSuccess =>
            (Status == PlatformRuntimeSelectionStatus.DefaultLocalSelected ||
             Status == PlatformRuntimeSelectionStatus.ExplicitProviderSelected) &&
            SelectedProviderId.IsValid &&
            Runtime != null;

        public PlatformProviderSelectionKind SelectionKind { get; }

        public string SelectionSource { get; }

        public PlatformProviderId RequestedProviderId { get; }

        public bool HasRequestedProviderId => RequestedProviderId.IsValid;

        public PlatformProviderId SelectedProviderId { get; }

        public bool HasSelectedProviderId => SelectedProviderId.IsValid;

        public IPlatformRuntime Runtime { get; }

        public int RegisteredProviderCount { get; }

        public bool RequestedProviderRegistered { get; }

        public bool FallbackUsed { get; }

        public string FailureReason => IsSuccess
            ? string.Empty
            : failureReason ?? "Platform runtime selection failed without a reason.";

        internal static PlatformRuntimeSelectionResult Success(
            PlatformProviderId providerId,
            IPlatformRuntime runtime)
        {
            return Explicit(
                PlatformProviderSelectionRequest.Explicit(
                    providerId,
                    PlatformProviderSelection.TestOverrideSource),
                providerId,
                runtime,
                1,
                true);
        }

        internal static PlatformRuntimeSelectionResult DefaultLocal(
            PlatformProviderSelectionRequest request,
            IPlatformRuntime runtime,
            int registeredProviderCount)
        {
            if (request.Kind != PlatformProviderSelectionKind.None)
            {
                throw new ArgumentException(
                    "Default Local selection requires no explicit request.",
                    nameof(request));
            }

            return SuccessfulSelection(
                PlatformRuntimeSelectionStatus.DefaultLocalSelected,
                request,
                PlatformProviderId.Local,
                runtime,
                registeredProviderCount,
                false,
                true);
        }

        internal static PlatformRuntimeSelectionResult Explicit(
            PlatformProviderSelectionRequest request,
            PlatformProviderId providerId,
            IPlatformRuntime runtime,
            int registeredProviderCount,
            bool requestedProviderRegistered)
        {
            if (request.Kind != PlatformProviderSelectionKind.Explicit ||
                request.RequestedProviderId != providerId)
            {
                throw new ArgumentException(
                    "Explicit selection request must match the selected provider ID.",
                    nameof(request));
            }

            return SuccessfulSelection(
                PlatformRuntimeSelectionStatus.ExplicitProviderSelected,
                request,
                providerId,
                runtime,
                registeredProviderCount,
                requestedProviderRegistered,
                false);
        }

        internal static PlatformRuntimeSelectionResult Unavailable(
            PlatformRuntimeSelectionResult selected,
            string reason)
        {
            if (!selected.IsSuccess ||
                selected.SelectionKind != PlatformProviderSelectionKind.Explicit)
            {
                throw new ArgumentException(
                    "Unavailable resolution requires a successful explicit selection.",
                    nameof(selected));
            }

            return new PlatformRuntimeSelectionResult(
                PlatformRuntimeSelectionStatus.RequestedProviderUnavailable,
                PlatformProviderSelectionRequest.Explicit(
                    selected.RequestedProviderId,
                    selected.SelectionSource),
                selected.SelectedProviderId,
                selected.Runtime,
                selected.RegisteredProviderCount,
                selected.RequestedProviderRegistered,
                false,
                "Platform provider '" + selected.SelectedProviderId +
                "' was selected but is unavailable: " +
                RequireFailureReason(reason));
        }

        private static PlatformRuntimeSelectionResult SuccessfulSelection(
            PlatformRuntimeSelectionStatus status,
            PlatformProviderSelectionRequest request,
            PlatformProviderId providerId,
            IPlatformRuntime runtime,
            int registeredProviderCount,
            bool requestedProviderRegistered,
            bool fallbackUsed)
        {
            if (!providerId.IsValid)
            {
                throw new ArgumentException("Successful selection requires a valid provider ID.", nameof(providerId));
            }

            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            return new PlatformRuntimeSelectionResult(
                status,
                request,
                providerId,
                runtime,
                registeredProviderCount,
                requestedProviderRegistered,
                fallbackUsed,
                string.Empty);
        }

        internal static PlatformRuntimeSelectionResult Failure(
            PlatformRuntimeSelectionStatus status,
            PlatformProviderId selectedProviderId,
            string reason)
        {
            return Failure(
                status,
                PlatformProviderSelectionRequest.None(
                    PlatformProviderSelection.TestOverrideSource),
                selectedProviderId,
                0,
                false,
                reason);
        }

        internal static PlatformRuntimeSelectionResult Failure(
            PlatformRuntimeSelectionStatus status,
            PlatformProviderSelectionRequest request,
            PlatformProviderId selectedProviderId,
            int registeredProviderCount,
            bool requestedProviderRegistered,
            string reason)
        {
            if (status == PlatformRuntimeSelectionStatus.DefaultLocalSelected ||
                status == PlatformRuntimeSelectionStatus.ExplicitProviderSelected)
            {
                throw new ArgumentException("Failure result cannot use the success status.", nameof(status));
            }

            return new PlatformRuntimeSelectionResult(
                status,
                request,
                selectedProviderId,
                null,
                registeredProviderCount,
                requestedProviderRegistered,
                false,
                RequireFailureReason(reason));
        }

        private static string RequireFailureReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Selection failure requires a non-empty reason.", nameof(reason));
            }

            return reason;
        }
    }
}
