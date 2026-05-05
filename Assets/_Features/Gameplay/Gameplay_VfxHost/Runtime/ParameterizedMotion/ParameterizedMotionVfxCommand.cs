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
    }

    public enum ParameterizedMotionVfxCloneMode
    {
        PrefabOnly = 0,
        SourceViewClone = 1,
        SourceViewCloneWithPrefabFallback = 2,
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
            ParameterizedMotionVfxCloneMode cloneMode)
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

        internal GameplayEntityPose SourcePose => new(SourceLocalPosition, SourceLocalRotation);

        internal GameplayEntityPose TargetPose => new(TargetLocalPosition, TargetLocalRotation);
    }
}
