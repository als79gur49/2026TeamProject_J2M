using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    public static class GameplayPresentationTimingConstants
    {
        // Stay: contact/recoil branch threshold.
        // DestroySelf: break/release onset threshold.
        public const float FlipImpactInteractionOnsetNormalizedTime = 0.62f;

        // Ordinary Flip box motion visual slam contact. This is the point where the
        // visual sampler reaches the floor, not the earlier interaction onset.
        public const float FlipVisualSlamContactNormalizedTime = 0.936f;

        // B-1 due contact is already authoritative at the due tick; this short
        // screen gate only keeps stage-result UI from covering the contact frame.
        public const float FlipB1DueContactStageClearBarrierSeconds = 0.12f;
    }

    public static class GameplayFlipMotionTiming
    {
        public const float VisualSlamContactNormalizedTime =
            GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;

        public static int ResolveB1VisualImpactDelayTicks(GameplayTimingProfile timingProfile)
        {
            var profile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            var playerControlTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                profile.SimulationTicksPerSecond,
                profile.RepeatedMoveIntervalSeconds);
            return ResolveB1VisualImpactDelayTicks(playerControlTiming);
        }

        public static int ResolveB1VisualImpactDelayTicks(PlayerControlTimingAuthoritativeSnapshot playerControlTiming)
        {
            return ResolveB1VisualImpactDelayTicks(playerControlTiming.FlipInputLockDurationTicks);
        }

        public static int ResolveB1VisualImpactDelayTicks(int flipInputLockDurationTicks)
        {
            return Mathf.Max(
                1,
                Mathf.RoundToInt(flipInputLockDurationTicks * VisualSlamContactNormalizedTime));
        }

        public static float ResolveB1VisualImpactDelaySeconds(GameplayTimingProfile timingProfile)
        {
            var profile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            return ResolveB1VisualImpactDelayTicks(profile) * profile.SimulationTickIntervalSeconds;
        }
    }
}
