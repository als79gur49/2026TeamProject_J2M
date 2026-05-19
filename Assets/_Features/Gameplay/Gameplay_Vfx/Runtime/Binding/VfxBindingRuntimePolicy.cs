using System;
using Game.Feature.Gameplay;

namespace Game.Feature.Gameplay.Vfx
{
    public readonly struct VfxBindingRuntimePolicy : IEquatable<VfxBindingRuntimePolicy>
    {
        public VfxBindingRuntimePolicy(
            GameplayVfxCueId cueId,
            VfxBindingRequirement requirement,
            VfxMissingAnchorPolicy missingAnchorPolicy,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int maxConcurrentInstances = 0,
            VfxStyleKey styleKey = default,
            GameplayVfxVisibilityMode visibilityMode = GameplayVfxVisibilityMode.DefaultGameplay)
        {
            CueId = cueId;
            StyleKey = styleKey;
            Requirement = requirement;
            MissingAnchorPolicy = missingAnchorPolicy;
            PlaybackMode = playbackMode;
            StopPolicy = stopPolicy;
            DefaultLifetimeSeconds = defaultLifetimeSeconds;
            TailSeconds = tailSeconds;
            MaxConcurrentInstances = maxConcurrentInstances;
            VisibilityMode = visibilityMode;
        }

        public GameplayVfxCueId CueId { get; }

        public VfxStyleKey StyleKey { get; }

        public VfxBindingRequirement Requirement { get; }

        public VfxMissingAnchorPolicy MissingAnchorPolicy { get; }

        public VfxPlaybackMode PlaybackMode { get; }

        public VfxStopPolicy StopPolicy { get; }

        public float DefaultLifetimeSeconds { get; }

        public float TailSeconds { get; }

        public int MaxConcurrentInstances { get; }

        public GameplayVfxVisibilityMode VisibilityMode { get; }

        public bool IsValid => TryGetValidationError(out _) == false;

        public static VfxBindingRuntimePolicy Optional(
            GameplayVfxCueId cueId,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.SkipOptional,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int maxConcurrentInstances = 0,
            VfxStyleKey styleKey = default,
            GameplayVfxVisibilityMode visibilityMode = GameplayVfxVisibilityMode.DefaultGameplay)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Optional,
                missingAnchorPolicy,
                playbackMode,
                stopPolicy,
                defaultLifetimeSeconds,
                tailSeconds,
                maxConcurrentInstances,
                styleKey,
                visibilityMode);
        }

        public static VfxBindingRuntimePolicy Required(
            GameplayVfxCueId cueId,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.FailFast,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int maxConcurrentInstances = 0,
            VfxStyleKey styleKey = default,
            GameplayVfxVisibilityMode visibilityMode = GameplayVfxVisibilityMode.DefaultGameplay)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Required,
                missingAnchorPolicy,
                playbackMode,
                stopPolicy,
                defaultLifetimeSeconds,
                tailSeconds,
                maxConcurrentInstances,
                styleKey,
                visibilityMode);
        }

        public void ValidateOrThrow()
        {
            if (TryGetValidationError(out var error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public bool Equals(VfxBindingRuntimePolicy other)
        {
            return CueId.Equals(other.CueId)
                && StyleKey.Equals(other.StyleKey)
                && Requirement == other.Requirement
                && MissingAnchorPolicy == other.MissingAnchorPolicy
                && PlaybackMode == other.PlaybackMode
                && StopPolicy == other.StopPolicy
                && DefaultLifetimeSeconds.Equals(other.DefaultLifetimeSeconds)
                && TailSeconds.Equals(other.TailSeconds)
                && MaxConcurrentInstances == other.MaxConcurrentInstances
                && VisibilityMode == other.VisibilityMode;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxBindingRuntimePolicy other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CueId.GetHashCode();
                hash = (hash * 397) ^ StyleKey.GetHashCode();
                hash = (hash * 397) ^ (int)Requirement;
                hash = (hash * 397) ^ (int)MissingAnchorPolicy;
                hash = (hash * 397) ^ (int)PlaybackMode;
                hash = (hash * 397) ^ (int)StopPolicy;
                hash = (hash * 397) ^ DefaultLifetimeSeconds.GetHashCode();
                hash = (hash * 397) ^ TailSeconds.GetHashCode();
                hash = (hash * 397) ^ MaxConcurrentInstances;
                hash = (hash * 397) ^ (int)VisibilityMode;
                return hash;
            }
        }

        public static bool operator ==(VfxBindingRuntimePolicy left, VfxBindingRuntimePolicy right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VfxBindingRuntimePolicy left, VfxBindingRuntimePolicy right)
        {
            return !left.Equals(right);
        }

        private bool TryGetValidationError(out string error)
        {
            if (CueId.IsNone)
            {
                error = "VFX binding policy cue id cannot be None.";
                return true;
            }

            if (!IsFiniteNonNegative(DefaultLifetimeSeconds))
            {
                error = "VFX binding policy default lifetime seconds must be finite and non-negative.";
                return true;
            }

            if (!IsFiniteNonNegative(TailSeconds))
            {
                error = "VFX binding policy tail seconds must be finite and non-negative.";
                return true;
            }

            if (MaxConcurrentInstances < 0)
            {
                error = "VFX binding policy max concurrent instances cannot be negative.";
                return true;
            }

            error = null;
            return false;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
