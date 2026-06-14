using System;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public enum ParameterizedMotionVfxFadeMode
    {
        None = 0,
        ScaleAndAlpha = 1,
        ScaleOnly = 2,
        AlphaOnly = 3,
        EnemyDeathFade = 4,
        DestroyShrinkEase = 5,
    }

    public enum ParameterizedMotionVfxCloneMode
    {
        PrefabOnly = 0,
        SourceViewClone = 1,
        PrefabWithSourceClone = 2,
        [Obsolete("Use PrefabWithSourceClone. This alias is kept for serialized/backward compatibility.")]
        SourceViewCloneWithPrefabFallback = PrefabWithSourceClone,
        SourceCloneMotion = 3,
    }

    public enum ParameterizedMotionVfxSamplerMode
    {
        FlipArc = 0,
        Linear = 1,
        EnemyDeathFlyAway = 2,
    }

    public readonly struct ParameterizedMotionVfxCommand
    {
        public ParameterizedMotionVfxCommand(
            GameplayVfxCueId cueId,
            int sourceEntityId,
            int sequenceId,
            int presentationSeed,
            Vector3 sourceLocalPosition,
            Quaternion sourceLocalRotation,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            float durationSeconds,
            float arcHeight,
            float breakStartSeconds,
            float fadeDurationSeconds,
            ParameterizedMotionVfxFadeMode fadeMode,
            ParameterizedMotionVfxCloneMode cloneMode,
            ParameterizedMotionVfxSamplerMode samplerMode = ParameterizedMotionVfxSamplerMode.FlipArc,
            Vector3 arcLocalDirection = default,
            float spinDegrees = 0f,
            Vector3 spinAxisLocal = default)
        {
            CueId = cueId;
            SourceEntityId = sourceEntityId;
            SequenceId = sequenceId;
            PresentationSeed = presentationSeed;
            SourceLocalPosition = sourceLocalPosition;
            SourceLocalRotation = sourceLocalRotation;
            TargetLocalPosition = targetLocalPosition;
            TargetLocalRotation = targetLocalRotation;
            DurationSeconds = Mathf.Max(0.0001f, durationSeconds);
            ArcHeight = Mathf.Max(0f, arcHeight);
            BreakStartSeconds = Mathf.Clamp(breakStartSeconds, 0f, DurationSeconds);
            FadeDurationSeconds = Mathf.Max(0f, fadeDurationSeconds);
            FadeMode = fadeMode;
            CloneMode = cloneMode;
            SamplerMode = samplerMode;
            ArcLocalDirection = arcLocalDirection;
            SpinDegrees = spinDegrees;
            SpinAxisLocal = spinAxisLocal;
        }

        public GameplayVfxCueId CueId { get; }

        public int SourceEntityId { get; }

        public int SequenceId { get; }

        public int PresentationSeed { get; }

        public Vector3 SourceLocalPosition { get; }

        public Quaternion SourceLocalRotation { get; }

        public Vector3 TargetLocalPosition { get; }

        public Quaternion TargetLocalRotation { get; }

        public float DurationSeconds { get; }

        public float ArcHeight { get; }

        public float BreakStartSeconds { get; }

        public float FadeDurationSeconds { get; }

        public ParameterizedMotionVfxFadeMode FadeMode { get; }

        public ParameterizedMotionVfxCloneMode CloneMode { get; }

        public ParameterizedMotionVfxSamplerMode SamplerMode { get; }

        public Vector3 ArcLocalDirection { get; }

        public float SpinDegrees { get; }

        public Vector3 SpinAxisLocal { get; }

        internal GameplayEntityPose SourcePose => new(SourceLocalPosition, SourceLocalRotation);

        internal GameplayEntityPose TargetPose => new(TargetLocalPosition, TargetLocalRotation);
    }

    public readonly struct ParameterizedMotionVfxSample
    {
        public ParameterizedMotionVfxSample(
            Vector3 localPosition,
            Quaternion localRotation,
            float normalizedTime,
            float fadeProgress)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            NormalizedTime = normalizedTime;
            FadeProgress = fadeProgress;
        }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public float NormalizedTime { get; }

        public float FadeProgress { get; }
    }

    public static class ParameterizedMotionVfxSampler
    {
        public static ParameterizedMotionVfxSample Sample(
            in ParameterizedMotionVfxCommand command,
            float elapsedSeconds)
        {
            var normalizedTime = command.DurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(elapsedSeconds / command.DurationSeconds);
            var pose = SamplePose(command, elapsedSeconds, normalizedTime);
            return new ParameterizedMotionVfxSample(
                pose.Position,
                pose.Rotation,
                normalizedTime,
                SampleFadeProgress(command, elapsedSeconds));
        }

        private static GameplayEntityPose SamplePose(
            in ParameterizedMotionVfxCommand command,
            float elapsedSeconds,
            float normalizedTime)
        {
            if (elapsedSeconds >= command.DurationSeconds &&
                command.SamplerMode != ParameterizedMotionVfxSamplerMode.EnemyDeathFlyAway)
            {
                return command.TargetPose;
            }

            switch (command.SamplerMode)
            {
                case ParameterizedMotionVfxSamplerMode.Linear:
                    return new GameplayEntityPose(
                        Vector3.Lerp(command.SourceLocalPosition, command.TargetLocalPosition, normalizedTime),
                        Quaternion.Slerp(command.SourceLocalRotation, command.TargetLocalRotation, normalizedTime));
                case ParameterizedMotionVfxSamplerMode.EnemyDeathFlyAway:
                    return SampleEnemyDeathFlyAway(command, normalizedTime);
                case ParameterizedMotionVfxSamplerMode.FlipArc:
                    return FlipArcSampler.Sample(
                        command.SourcePose,
                        command.TargetPose,
                        normalizedTime,
                        command.ArcHeight);
                default:
                    return FlipArcSampler.Sample(
                        command.SourcePose,
                        command.TargetPose,
                        normalizedTime,
                        command.ArcHeight);
            }
        }

        private static GameplayEntityPose SampleEnemyDeathFlyAway(
            in ParameterizedMotionVfxCommand command,
            float normalizedTime)
        {
            var easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);
            var arcLocalDirection = command.ArcLocalDirection.sqrMagnitude > 0.000001f
                ? command.ArcLocalDirection.normalized
                : Vector3.up;
            var spinAxisLocal = command.SpinAxisLocal.sqrMagnitude > 0.000001f
                ? command.SpinAxisLocal.normalized
                : Vector3.forward;
            var arcOffset = arcLocalDirection * (command.ArcHeight * Mathf.Sin(normalizedTime * Mathf.PI));
            return new GameplayEntityPose(
                Vector3.LerpUnclamped(command.SourceLocalPosition, command.TargetLocalPosition, easedTime) + arcOffset,
                Quaternion.AngleAxis(command.SpinDegrees * easedTime, spinAxisLocal) * command.SourceLocalRotation);
        }

        private static float SampleFadeProgress(
            in ParameterizedMotionVfxCommand command,
            float elapsedSeconds)
        {
            if (elapsedSeconds <= command.BreakStartSeconds)
            {
                return 0f;
            }

            if (command.FadeDurationSeconds <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01((elapsedSeconds - command.BreakStartSeconds) / command.FadeDurationSeconds);
        }
    }
}
