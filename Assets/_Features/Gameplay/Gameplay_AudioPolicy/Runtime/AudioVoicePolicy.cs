using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.AudioPolicy
{
    public enum AudioVoiceGroupId
    {
        None = 0,
        EnemyMovement = 1,
        TileFeatureToggle = 2,
        PlayerCritical = 3,
        EnemyDamage = 4,
        GameplayAction = 5,
        BlockImpact = 6,
        GravityField = 7,
        PlayerLocomotion = 8,
        GenericGameplay = 9,
        ForwardCellImpact = 10,
    }

    public enum VoiceOverflowMode
    {
        DropNewest = 0,
        AttenuateThenDrop = 1,
        StealLowerPriority = 2,
        NeverDrop = 3,
    }

    public interface IAudioPolicyDiagnostics
    {
        void Warning(string message);

        void Error(string message);
    }

    public static class AudioVoicePolicySupport
    {
        public static bool IsSupportedByGameplaySfxArbiterV1(VoiceOverflowMode mode)
        {
            return mode == VoiceOverflowMode.DropNewest;
        }

        public static VoiceOverflowMode NormalizeForGameplaySfxArbiterV1(
            VoiceOverflowMode mode,
            IAudioPolicyDiagnostics diagnostics,
            string policyNameOrContext)
        {
            var context = string.IsNullOrWhiteSpace(policyNameOrContext)
                ? "AudioVoicePolicy"
                : policyNameOrContext;
            switch (mode)
            {
                case VoiceOverflowMode.DropNewest:
                    return mode;

                case VoiceOverflowMode.AttenuateThenDrop:
                    diagnostics?.Warning(
                        $"{context}: AttenuateThenDrop is reserved for v2. " +
                        "GameplaySfxArbiter v1 treats this as DropNewest unless attenuation is implemented.");
                    return VoiceOverflowMode.DropNewest;

                case VoiceOverflowMode.StealLowerPriority:
                    diagnostics?.Error(
                        $"{context}: StealLowerPriority is not supported by GameplaySfxArbiter v1. " +
                        "Do not author this mode until live voice stealing is implemented.");
                    return VoiceOverflowMode.DropNewest;

                case VoiceOverflowMode.NeverDrop:
                    diagnostics?.Warning(
                        $"{context}: NeverDrop is not a supported grouped overflow mode in GameplaySfxArbiter v1. " +
                        "Use DropNewest with no batch caps for unlimited v1 admission.");
                    return VoiceOverflowMode.DropNewest;

                default:
                    diagnostics?.Warning($"{context}: Unknown overflow mode. Falling back to DropNewest.");
                    return VoiceOverflowMode.DropNewest;
            }
        }
    }

    public readonly struct BurstGainStep
    {
        public BurstGainStep(int index, float gain)
        {
            Index = Math.Max(0, index);
            Gain = SanitizeGain(gain);
        }

        public int Index { get; }

        public float Gain { get; }

        internal static float SanitizeGain(float gain)
        {
            return gain > 0f && !float.IsNaN(gain) && !float.IsInfinity(gain)
                ? gain
                : 1f;
        }
    }

    public readonly struct AudioVoicePolicy
    {
        public AudioVoicePolicy(
            AudioVoiceGroupId group,
            int priority,
            int maxVoicesGlobal,
            int maxVoicesPerOwner,
            float cooldownSecondsGlobal,
            float cooldownSecondsPerOwner,
            float duplicateWindowSeconds,
            VoiceOverflowMode overflowMode,
            IReadOnlyList<BurstGainStep> burstGainSteps = null)
        {
            Group = group;
            Priority = priority;
            MaxVoicesGlobal = Math.Max(0, maxVoicesGlobal);
            MaxVoicesPerOwner = Math.Max(0, maxVoicesPerOwner);
            CooldownSecondsGlobal = SanitizeSeconds(cooldownSecondsGlobal);
            CooldownSecondsPerOwner = SanitizeSeconds(cooldownSecondsPerOwner);
            DuplicateWindowSeconds = SanitizeSeconds(duplicateWindowSeconds);
            OverflowMode = overflowMode;
            BurstGainSteps = burstGainSteps ?? Array.Empty<BurstGainStep>();
        }

        public AudioVoiceGroupId Group { get; }

        public int Priority { get; }

        /// <summary>
        /// In GameplaySfxArbiter v1 this is a max admission count within one presentation batch.
        /// It does not count live AudioSources that are already playing.
        /// </summary>
        public int MaxVoicesGlobal { get; }

        /// <summary>
        /// In GameplaySfxArbiter v1 this is a max per-owner admission count within one presentation batch.
        /// It does not count live AudioSources that are already playing.
        /// </summary>
        public int MaxVoicesPerOwner { get; }

        public float CooldownSecondsGlobal { get; }

        public float CooldownSecondsPerOwner { get; }

        public float DuplicateWindowSeconds { get; }

        public VoiceOverflowMode OverflowMode { get; }

        public IReadOnlyList<BurstGainStep> BurstGainSteps { get; }

        public bool HasGroup => Group != AudioVoiceGroupId.None;

        public AudioVoicePolicy WithOverflowMode(VoiceOverflowMode overflowMode)
        {
            return new AudioVoicePolicy(
                Group,
                Priority,
                MaxVoicesGlobal,
                MaxVoicesPerOwner,
                CooldownSecondsGlobal,
                CooldownSecondsPerOwner,
                DuplicateWindowSeconds,
                overflowMode,
                BurstGainSteps);
        }

        public static AudioVoicePolicy None => new(
            AudioVoiceGroupId.None,
            priority: 0,
            maxVoicesGlobal: 0,
            maxVoicesPerOwner: 0,
            cooldownSecondsGlobal: 0f,
            cooldownSecondsPerOwner: 0f,
            duplicateWindowSeconds: 0f,
            overflowMode: VoiceOverflowMode.NeverDrop);

        private static float SanitizeSeconds(float seconds)
        {
            return seconds > 0f && !float.IsNaN(seconds) && !float.IsInfinity(seconds)
                ? seconds
                : 0f;
        }
    }
}
