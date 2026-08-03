using System;

namespace Game.Platform.Runtime
{
    public readonly struct PlatformProviderId : IEquatable<PlatformProviderId>
    {
        private readonly string value;

        public PlatformProviderId(string value)
        {
            Validate(value);
            this.value = value;
        }

        public static PlatformProviderId Local { get; } = new PlatformProviderId("local");

        public bool IsValid => value != null;

        public string Value
        {
            get
            {
                if (!IsValid)
                {
                    throw new InvalidOperationException(
                        "The default PlatformProviderId is invalid and has no value.");
                }

                return value;
            }
        }

        public bool Equals(PlatformProviderId other)
        {
            return string.Equals(value, other.value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlatformProviderId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        public override string ToString()
        {
            return value ?? "<invalid>";
        }

        public static bool operator ==(PlatformProviderId left, PlatformProviderId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlatformProviderId left, PlatformProviderId right)
        {
            return !left.Equals(right);
        }

        private static void Validate(string candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (candidate.Length == 0)
            {
                throw new ArgumentException("Platform provider ID cannot be empty.", nameof(candidate));
            }

            for (var index = 0; index < candidate.Length; index++)
            {
                var character = candidate[index];
                var isLowerAscii = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                var isSeparator = character == '.' || character == '_' || character == '-';
                if (!isLowerAscii && !isDigit && !isSeparator)
                {
                    throw new ArgumentException(
                        "Platform provider ID may contain only lowercase ASCII letters, digits, '.', '_', or '-'.",
                        nameof(candidate));
                }
            }
        }
    }
}
