using System;

namespace Game.Platform.Runtime
{
    public readonly struct PlatformAvailability : IEquatable<PlatformAvailability>
    {
        private const string UnspecifiedReason = "Platform availability was not specified.";

        private readonly bool isAvailable;
        private readonly string reason;

        private PlatformAvailability(bool isAvailable, string reason)
        {
            this.isAvailable = isAvailable;
            this.reason = reason;
        }

        public static PlatformAvailability Available { get; } =
            new PlatformAvailability(true, string.Empty);

        public bool IsAvailable => isAvailable;

        public string Reason => isAvailable ? string.Empty : reason ?? UnspecifiedReason;

        public static PlatformAvailability Unavailable(string reason)
        {
            return new PlatformAvailability(false, RequireReason(reason));
        }

        public bool Equals(PlatformAvailability other)
        {
            return isAvailable == other.isAvailable &&
                   string.Equals(Reason, other.Reason, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlatformAvailability other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (isAvailable.GetHashCode() * 397) ^
                       StringComparer.Ordinal.GetHashCode(Reason);
            }
        }

        private static string RequireReason(string failureReason)
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                throw new ArgumentException(
                    "Unavailable platform state requires a non-empty reason.",
                    nameof(failureReason));
            }

            return failureReason;
        }
    }
}
