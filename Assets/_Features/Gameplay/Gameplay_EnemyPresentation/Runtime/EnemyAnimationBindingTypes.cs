using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum EnemyAnimationCue
    {
        None = 0,
        ActionWindup = 10,
        ActionExecute = 11,
        ActionRecovery = 12,
        JumpWindup = 20,
        JumpAirborne = 21,
        JumpLanding = 22,
        ChargeWindup = 30,
        ChargeActive = 31,
        ChargeRecovery = 32,
        GlideWindup = 40,
        GlideActive = 41,
        GlideRecovery = 42,
        UtilityWindup = 50,
        UtilityRecovery = 51,
        Hit = 90,
        Death = 91,
    }

    public enum EnemyAnimationDispatchMode
    {
        None = 0,
        Trigger = 1,
        State = 2,
    }

    internal enum EnemyAnimationDispatchResult
    {
        Unsupported = 0,
        Applied = 1,
        Queued = 2,
        AnimatorUnavailable = 3,
    }

    [Serializable]
    public struct EnemyAnimationCueBinding
    {
        [SerializeField] private EnemyAnimationCue cue;
        [SerializeField] private EnemyAnimationDispatchMode primaryDispatchMode;
        [SerializeField] private string targetName;
        [SerializeField] private string sustainedStateName;
        [SerializeField] private float animatorDurationSeconds;
        [SerializeField] private AnimationClip referenceClip;

        public EnemyAnimationCue Cue => cue;

        public EnemyAnimationDispatchMode PrimaryDispatchMode => primaryDispatchMode;

        public string TargetName => targetName;

        public string SustainedStateName => sustainedStateName;

        public float AnimatorDurationSeconds => animatorDurationSeconds;

        public AnimationClip ReferenceClip => referenceClip;

        internal static EnemyAnimationCueBinding CreateForTests(
            EnemyAnimationCue cue,
            EnemyAnimationDispatchMode primaryDispatchMode,
            string targetName,
            string sustainedStateName = "",
            float animatorDurationSeconds = EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel,
            AnimationClip referenceClip = null)
        {
            return new EnemyAnimationCueBinding
            {
                cue = cue,
                primaryDispatchMode = primaryDispatchMode,
                targetName = targetName,
                sustainedStateName = sustainedStateName,
                animatorDurationSeconds = animatorDurationSeconds,
                referenceClip = referenceClip,
            };
        }
    }

    public readonly struct EnemyAnimationRuntimeBinding
    {
        internal EnemyAnimationRuntimeBinding(
            EnemyAnimationCue cue,
            EnemyAnimationDispatchMode primaryDispatchMode,
            string targetName,
            string sustainedStateName,
            float animatorDurationSeconds,
            AnimationClip referenceClip,
            float referenceClipLengthSeconds)
        {
            Cue = cue;
            PrimaryDispatchMode = primaryDispatchMode;
            TargetName = targetName;
            SustainedStateName = sustainedStateName;
            AnimatorDurationSeconds = animatorDurationSeconds;
            ReferenceClip = referenceClip;
            ReferenceClipLengthSeconds = referenceClipLengthSeconds;
        }

        public EnemyAnimationCue Cue { get; }

        public EnemyAnimationDispatchMode PrimaryDispatchMode { get; }

        public string TargetName { get; }

        public string SustainedStateName { get; }

        public float AnimatorDurationSeconds { get; }

        public AnimationClip ReferenceClip { get; }

        public float ReferenceClipLengthSeconds { get; }

        public bool HasReferenceClipLength => ReferenceClipLengthSeconds > 0f;
    }

    internal readonly struct EnemyAnimationPendingStateCommand
    {
        public EnemyAnimationPendingStateCommand(
            EnemyAnimationCue cue,
            string stateName,
            float crossFadeSeconds)
        {
            Cue = cue;
            StateName = stateName;
            CrossFadeSeconds = crossFadeSeconds;
            HasValue = true;
        }

        public EnemyAnimationCue Cue { get; }

        public string StateName { get; }

        public float CrossFadeSeconds { get; }

        public bool HasValue { get; }
    }
}
