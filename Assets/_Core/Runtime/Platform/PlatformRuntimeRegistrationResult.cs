using System;

namespace Game.Platform.Runtime
{
    public enum PlatformRuntimeRegistrationStatus
    {
        Success = 0,
        InvalidFactory = 1,
        InvalidProviderId = 2,
        DuplicateProvider = 3,
        RegistrySealed = 4,
    }

    public readonly struct PlatformRuntimeRegistrationResult
    {
        private readonly string failureReason;

        private PlatformRuntimeRegistrationResult(
            PlatformRuntimeRegistrationStatus status,
            PlatformProviderId providerId,
            string failureReason)
        {
            Status = status;
            ProviderId = providerId;
            this.failureReason = failureReason;
        }

        public PlatformRuntimeRegistrationStatus Status { get; }

        public bool IsSuccess =>
            Status == PlatformRuntimeRegistrationStatus.Success &&
            ProviderId.IsValid;

        public PlatformProviderId ProviderId { get; }

        public string FailureReason => IsSuccess
            ? string.Empty
            : failureReason ?? "Platform runtime factory registration failed without a reason.";

        internal static PlatformRuntimeRegistrationResult Success(PlatformProviderId providerId)
        {
            return new PlatformRuntimeRegistrationResult(
                PlatformRuntimeRegistrationStatus.Success,
                providerId,
                string.Empty);
        }

        internal static PlatformRuntimeRegistrationResult Failure(
            PlatformRuntimeRegistrationStatus status,
            PlatformProviderId providerId,
            string reason)
        {
            if (status == PlatformRuntimeRegistrationStatus.Success)
            {
                throw new ArgumentException("Failure result cannot use the success status.", nameof(status));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Registration failure requires a non-empty reason.", nameof(reason));
            }

            return new PlatformRuntimeRegistrationResult(status, providerId, reason);
        }
    }
}
