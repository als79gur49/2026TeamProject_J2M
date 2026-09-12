using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class EnemyAnimationBindingSnapshot
    {
        private readonly IReadOnlyDictionary<EnemyAnimationCue, EnemyAnimationRuntimeBinding> _byCue;

        internal EnemyAnimationBindingSnapshot(
            IReadOnlyDictionary<EnemyAnimationCue, EnemyAnimationRuntimeBinding> byCue,
            float defaultStateCrossFadeDurationSeconds,
            bool hasPrimaryStateBinding)
        {
            _byCue = byCue;
            DefaultStateCrossFadeDurationSeconds = defaultStateCrossFadeDurationSeconds;
            HasPrimaryStateBinding = hasPrimaryStateBinding;
        }

        public int Count => _byCue.Count;

        public float DefaultStateCrossFadeDurationSeconds { get; }

        public bool HasPrimaryStateBinding { get; }

        public bool TryGetBinding(EnemyAnimationCue cue, out EnemyAnimationRuntimeBinding binding)
        {
            return _byCue.TryGetValue(cue, out binding);
        }

        internal IEnumerable<EnemyAnimationRuntimeBinding> Bindings => _byCue.Values;

        internal static EnemyAnimationBindingSnapshot CreateForTests(
            EnemyAnimationCueBinding[] bindings,
            float defaultStateCrossFadeDurationSeconds)
        {
            return Create(bindings, defaultStateCrossFadeDurationSeconds);
        }

        internal static EnemyAnimationBindingSnapshot Create(
            EnemyAnimationCueBinding[] bindings,
            float defaultStateCrossFadeDurationSeconds)
        {
            if (bindings == null || bindings.Length == 0)
            {
                throw new InvalidOperationException("Enemy animation binding component requires at least one binding.");
            }

            var byCue = new Dictionary<EnemyAnimationCue, EnemyAnimationRuntimeBinding>();
            var hasPrimaryStateBinding = false;
            for (var i = 0; i < bindings.Length; i++)
            {
                var source = bindings[i];
                var metadata = EnemyAnimationCueCatalog.GetRequired(source.Cue);
                if (!metadata.Allows(source.PrimaryDispatchMode))
                {
                    throw new InvalidOperationException(
                        $"{source.Cue} does not allow {source.PrimaryDispatchMode} dispatch.");
                }

                if (string.IsNullOrWhiteSpace(source.TargetName))
                {
                    throw new InvalidOperationException($"{source.Cue} requires a non-empty target name.");
                }

                ValidateSustainedState(source, metadata);
                ValidateReplacementState(source);
                var referenceClipLengthSeconds = ValidateAndResolveTiming(source, metadata);
                if (source.PrimaryDispatchMode == EnemyAnimationDispatchMode.State)
                {
                    hasPrimaryStateBinding = true;
                }

                var runtimeBinding = new EnemyAnimationRuntimeBinding(
                    source.Cue,
                    source.PrimaryDispatchMode,
                    source.TargetName,
                    source.SustainedStateName,
                    source.AnimatorDurationSeconds,
                    source.ReferenceClip,
                    referenceClipLengthSeconds,
                    source.ReplacementStateName);
                if (!byCue.TryAdd(source.Cue, runtimeBinding))
                {
                    throw new InvalidOperationException($"Duplicate enemy animation cue binding: {source.Cue}.");
                }
            }

            ValidateCrossFade(defaultStateCrossFadeDurationSeconds, hasPrimaryStateBinding);
            return new EnemyAnimationBindingSnapshot(
                new ReadOnlyDictionary<EnemyAnimationCue, EnemyAnimationRuntimeBinding>(byCue),
                defaultStateCrossFadeDurationSeconds,
                hasPrimaryStateBinding);
        }

        internal static bool AllowsReplacementState(EnemyAnimationCue cue, EnemyAnimationDispatchMode mode)
        {
            return mode == EnemyAnimationDispatchMode.Trigger &&
                   (cue == EnemyAnimationCue.UtilityWindup ||
                    cue == EnemyAnimationCue.UtilityRecovery ||
                    cue == EnemyAnimationCue.Death);
        }

        private static void ValidateReplacementState(in EnemyAnimationCueBinding binding)
        {
            if (!string.IsNullOrWhiteSpace(binding.ReplacementStateName) &&
                !AllowsReplacementState(binding.Cue, binding.PrimaryDispatchMode))
            {
                throw new InvalidOperationException(
                    $"{binding.Cue} does not allow a replacement state name for {binding.PrimaryDispatchMode} dispatch.");
            }
        }

        private static void ValidateSustainedState(
            in EnemyAnimationCueBinding binding,
            in EnemyAnimationCueMetadata metadata)
        {
            var requiresSustainedState =
                binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.Trigger &&
                metadata.RequiresSeparateSustainedStateWhenPrimaryTrigger;
            if (requiresSustainedState)
            {
                if (string.IsNullOrWhiteSpace(binding.SustainedStateName))
                {
                    throw new InvalidOperationException(
                        $"{binding.Cue} Trigger dispatch requires a sustained state name.");
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(binding.SustainedStateName))
            {
                throw new InvalidOperationException(
                    $"{binding.Cue} does not allow a separate sustained state name for {binding.PrimaryDispatchMode} dispatch.");
            }
        }

        private static float ValidateAndResolveTiming(
            in EnemyAnimationCueBinding binding,
            in EnemyAnimationCueMetadata metadata)
        {
            var durationSeconds = binding.AnimatorDurationSeconds;
            if (!metadata.SupportsTiming)
            {
                if (durationSeconds != EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel ||
                    binding.ReferenceClip != null)
                {
                    throw new InvalidOperationException($"{binding.Cue} does not allow animation timing authoring.");
                }

                return 0f;
            }

            if (!IsFinite(durationSeconds) ||
                (durationSeconds != EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel && durationSeconds <= 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(binding.AnimatorDurationSeconds),
                    durationSeconds,
                    "Animator duration must be finite and greater than zero, or exactly -1.");
            }

            if (binding.ReferenceClip == null)
            {
                if (durationSeconds > 0f)
                {
                    throw new InvalidOperationException(
                        $"{binding.Cue} requires a reference clip when animator duration is overridden.");
                }

                return 0f;
            }

            var clipLengthSeconds = binding.ReferenceClip.length;
            if (!IsFinite(clipLengthSeconds) || clipLengthSeconds <= 0f)
            {
                throw new InvalidOperationException(
                    $"{binding.Cue} reference clip must have a finite positive length.");
            }

            return clipLengthSeconds;
        }

        private static void ValidateCrossFade(float crossFadeSeconds, bool hasPrimaryStateBinding)
        {
            if (!IsFinite(crossFadeSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(crossFadeSeconds),
                    crossFadeSeconds,
                    "State cross-fade duration must be finite.");
            }

            if (hasPrimaryStateBinding)
            {
                if (crossFadeSeconds < 0f)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(crossFadeSeconds),
                        crossFadeSeconds,
                        "A nonnegative state cross-fade duration is required when a primary State binding exists.");
                }

                return;
            }

            if (crossFadeSeconds != EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel)
            {
                throw new InvalidOperationException(
                    "State cross-fade duration must be exactly -1 when no primary State binding exists.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [DisallowMultipleComponent]
    public sealed class EnemyAnimationBindingAuthoring : MonoBehaviour
    {
        [SerializeField] private float defaultStateCrossFadeDurationSeconds =
            EnemyAnimationTimingAuthoring.UseDriverDefaultSentinel;
        [SerializeField] private EnemyAnimationCueBinding[] bindings =
            Array.Empty<EnemyAnimationCueBinding>();

        public float DefaultStateCrossFadeDurationSeconds => defaultStateCrossFadeDurationSeconds;

        public int BindingCount => bindings?.Length ?? 0;

        public void Validate()
        {
            EnemyAnimationBindingSnapshot.Create(bindings, defaultStateCrossFadeDurationSeconds);
        }

        public EnemyAnimationBindingSnapshot CreateSnapshot()
        {
            return EnemyAnimationBindingSnapshot.Create(bindings, defaultStateCrossFadeDurationSeconds);
        }

        internal void ConfigureForTests(
            EnemyAnimationCueBinding[] sourceBindings,
            float stateCrossFadeDurationSeconds)
        {
            bindings = sourceBindings == null
                ? null
                : (EnemyAnimationCueBinding[])sourceBindings.Clone();
            defaultStateCrossFadeDurationSeconds = stateCrossFadeDurationSeconds;
        }

        internal static EnemyAnimationBindingAuthoring GetOptionalValidatedRoot(Component rootOwner)
        {
            if (rootOwner == null)
            {
                throw new ArgumentNullException(nameof(rootOwner));
            }

            var candidates = rootOwner.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(includeInactive: true);
            EnemyAnimationBindingAuthoring rootAuthoring = null;
            var rootCount = 0;
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null)
                {
                    throw new InvalidOperationException("Enemy animation binding hierarchy contains a missing component.");
                }

                if (candidate.transform != rootOwner.transform)
                {
                    throw new InvalidOperationException(
                        $"{nameof(EnemyAnimationBindingAuthoring)} must be placed on the enemy View root.");
                }

                rootCount++;
                rootAuthoring = candidate;
            }

            if (rootCount == 0)
            {
                return null;
            }

            if (rootCount != 1)
            {
                throw new InvalidOperationException(
                    $"Enemy View root requires at most one {nameof(EnemyAnimationBindingAuthoring)}, but found {rootCount}.");
            }

            if (!rootAuthoring.enabled)
            {
                throw new InvalidOperationException(
                    $"{nameof(EnemyAnimationBindingAuthoring)} must be enabled when present.");
            }

            rootAuthoring.Validate();
            return rootAuthoring;
        }
    }
}
