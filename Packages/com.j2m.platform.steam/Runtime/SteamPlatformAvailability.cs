using Game.Platform.Runtime;

namespace Game.Platform.Steam
{
    public readonly struct SteamPlatformAvailability
    {
        internal SteamPlatformAvailability(
            bool isAvailable,
            SteamPlatformFailureReason failureReason,
            string detail)
        {
            IsAvailable = isAvailable;
            FailureReason = failureReason;
            Detail = detail ?? string.Empty;
        }

        public bool IsAvailable { get; }

        public SteamPlatformFailureReason FailureReason { get; }

        public string Detail { get; }

        internal PlatformAvailability ToPlatformAvailability()
        {
            return IsAvailable
                ? PlatformAvailability.Available
                : PlatformAvailability.Unavailable(
                    FailureReason + (Detail.Length == 0 ? string.Empty : ": " + Detail));
        }

        internal static SteamPlatformAvailability Available()
        {
            return new SteamPlatformAvailability(true, SteamPlatformFailureReason.None, string.Empty);
        }

        internal static SteamPlatformAvailability Unavailable(
            SteamPlatformFailureReason failureReason,
            string detail)
        {
            return new SteamPlatformAvailability(false, failureReason, detail);
        }
    }
}
