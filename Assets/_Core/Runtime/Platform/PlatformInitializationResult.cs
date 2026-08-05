using System;

namespace Game.Platform.Runtime
{
    public readonly struct PlatformInitializationResult : IEquatable<PlatformInitializationResult>
    {
        private const string UnspecifiedReason = "Platform initialization result was not specified.";

        private readonly bool isSuccess;
        private readonly string failureReason;

        private PlatformInitializationResult(bool isSuccess, string failureReason)
        {
            this.isSuccess = isSuccess;
            this.failureReason = failureReason;
        }

        public static PlatformInitializationResult Success { get; } =
            new PlatformInitializationResult(true, string.Empty);

        public bool IsSuccess => isSuccess;

        public string FailureReason => isSuccess ? string.Empty : failureReason ?? UnspecifiedReason;

        public static PlatformInitializationResult Failure(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "Failed platform initialization requires a non-empty reason.",
                    nameof(reason));
            }

            return new PlatformInitializationResult(false, reason);
        }

        public bool Equals(PlatformInitializationResult other)
        {
            return isSuccess == other.isSuccess &&
                   string.Equals(FailureReason, other.FailureReason, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlatformInitializationResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (isSuccess.GetHashCode() * 397) ^
                       StringComparer.Ordinal.GetHashCode(FailureReason);
            }
        }
    }
}
