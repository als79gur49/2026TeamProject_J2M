using System;

namespace Game.Platform.Runtime
{
    public enum PlatformProviderSelectionKind
    {
        None = 0,
        Explicit = 1,
        Invalid = 2,
        Conflicting = 3,
    }

    public readonly struct PlatformProviderSelectionRequest
    {
        private readonly string source;
        private readonly string failureReason;

        private PlatformProviderSelectionRequest(
            PlatformProviderSelectionKind kind,
            PlatformProviderId requestedProviderId,
            string source,
            string failureReason)
        {
            Kind = kind;
            RequestedProviderId = requestedProviderId;
            this.source = source;
            this.failureReason = failureReason;
        }

        public PlatformProviderSelectionKind Kind { get; }

        public PlatformProviderId RequestedProviderId { get; }

        public bool HasRequestedProviderId => RequestedProviderId.IsValid;

        public string Source => source ?? string.Empty;

        public string FailureReason =>
            Kind == PlatformProviderSelectionKind.Invalid ||
            Kind == PlatformProviderSelectionKind.Conflicting
                ? failureReason ?? "Platform provider selection is invalid without a reason."
                : string.Empty;

        internal static PlatformProviderSelectionRequest None(string source)
        {
            return new PlatformProviderSelectionRequest(
                PlatformProviderSelectionKind.None,
                default,
                RequireSource(source),
                string.Empty);
        }

        internal static PlatformProviderSelectionRequest Explicit(
            PlatformProviderId requestedProviderId,
            string source)
        {
            if (!requestedProviderId.IsValid)
            {
                throw new ArgumentException(
                    "Explicit platform selection requires a valid provider ID.",
                    nameof(requestedProviderId));
            }

            return new PlatformProviderSelectionRequest(
                PlatformProviderSelectionKind.Explicit,
                requestedProviderId,
                RequireSource(source),
                string.Empty);
        }

        internal static PlatformProviderSelectionRequest Invalid(string source, string reason)
        {
            return Failure(
                PlatformProviderSelectionKind.Invalid,
                source,
                reason);
        }

        internal static PlatformProviderSelectionRequest Conflicting(string source, string reason)
        {
            return Failure(
                PlatformProviderSelectionKind.Conflicting,
                source,
                reason);
        }

        private static PlatformProviderSelectionRequest Failure(
            PlatformProviderSelectionKind kind,
            string source,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "Failed platform selection requires a non-empty reason.",
                    nameof(reason));
            }

            return new PlatformProviderSelectionRequest(
                kind,
                default,
                RequireSource(source),
                reason);
        }

        private static string RequireSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException(
                    "Platform selection requires a non-empty source.",
                    nameof(source));
            }

            return source;
        }
    }
}
