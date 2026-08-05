using System;

namespace Game.Platform.Steam
{
    public readonly struct SteamDllCheckObservation : IEquatable<SteamDllCheckObservation>
    {
        public const string UpstreamDisabledLimitation =
            "UPSTREAM_IMPLEMENTATION_COMPILED_WITH_SUBSTANTIVE_CHECK_DISABLED";

        public SteamDllCheckObservation(
            bool callSucceeded,
            bool returnedValue,
            bool independentCompatibilitySignal,
            string limitationReason)
        {
            if (!independentCompatibilitySignal && string.IsNullOrWhiteSpace(limitationReason))
            {
                throw new ArgumentException(
                    "A non-independent DLL check observation requires a limitation reason.",
                    nameof(limitationReason));
            }

            CallSucceeded = callSucceeded;
            ReturnedValue = returnedValue;
            IndependentCompatibilitySignal = independentCompatibilitySignal;
            LimitationReason = limitationReason ?? string.Empty;
        }

        public bool CallSucceeded { get; }

        public bool ReturnedValue { get; }

        public bool IndependentCompatibilitySignal { get; }

        public string LimitationReason { get; }

        public static SteamDllCheckObservation UpstreamDisabled(bool returnedValue)
        {
            return new SteamDllCheckObservation(
                callSucceeded: true,
                returnedValue: returnedValue,
                independentCompatibilitySignal: false,
                limitationReason: UpstreamDisabledLimitation);
        }

        public bool Equals(SteamDllCheckObservation other)
        {
            return CallSucceeded == other.CallSucceeded &&
                   ReturnedValue == other.ReturnedValue &&
                   IndependentCompatibilitySignal == other.IndependentCompatibilitySignal &&
                   string.Equals(LimitationReason, other.LimitationReason, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SteamDllCheckObservation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = CallSucceeded.GetHashCode();
                hashCode = (hashCode * 397) ^ ReturnedValue.GetHashCode();
                hashCode = (hashCode * 397) ^ IndependentCompatibilitySignal.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(LimitationReason);
                return hashCode;
            }
        }
    }
}
