using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class CameraShakeProfileEntry
    {
        public CameraShakeSemantic Semantic;
        public CameraShakeVariant Variant;
        public CameraShakePriority Priority;
        public float DurationSeconds;
        public int OscillationCycles;
        public Vector3 LocalPositionAmplitude;
        public Vector3 LocalRotationAmplitudeDegrees;
        public float AttackSeconds;
        public AnimationCurve Decay = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public float CooldownSeconds;

        internal CameraShakeProfileEntry Clone()
        {
            return new CameraShakeProfileEntry
            {
                Semantic = Semantic,
                Variant = Variant,
                Priority = Priority,
                DurationSeconds = DurationSeconds,
                OscillationCycles = OscillationCycles,
                LocalPositionAmplitude = LocalPositionAmplitude,
                LocalRotationAmplitudeDegrees = LocalRotationAmplitudeDegrees,
                AttackSeconds = AttackSeconds,
                Decay = Decay == null ? null : new AnimationCurve(Decay.keys),
                CooldownSeconds = CooldownSeconds,
            };
        }
    }

    [CreateAssetMenu(
        fileName = "GameplayCameraShakeProfile",
        menuName = "Game/Gameplay/Camera/Gameplay Camera Shake Profile")]
    public sealed class GameplayCameraShakeProfile : ScriptableObject
    {
        [SerializeField]
        private CameraShakeProfileEntry[] entries = Array.Empty<CameraShakeProfileEntry>();

        public void ValidateOrThrow()
        {
            CreateValidatedEntryMap();
        }

        internal IReadOnlyDictionary<CameraShakeProfileKey, CameraShakeProfileEntry> CreateValidatedEntryMap()
        {
            var result = new Dictionary<CameraShakeProfileKey, CameraShakeProfileEntry>();
            var resolvedEntries = entries ?? Array.Empty<CameraShakeProfileEntry>();
            for (var index = 0; index < resolvedEntries.Length; index++)
            {
                var entry = resolvedEntries[index] ?? throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry {index} is null.");
                ValidateEntry(entry, index);
                var key = new CameraShakeProfileKey(entry.Semantic, entry.Variant);
                if (!result.TryAdd(key, entry.Clone()))
                {
                    throw new InvalidOperationException(
                        $"Gameplay camera shake profile contains duplicate key '{key}'.");
                }
            }

            var requiredKeys = GetRequiredKeys();
            for (var index = 0; index < requiredKeys.Length; index++)
            {
                var requiredKey = requiredKeys[index];
                if (!result.ContainsKey(requiredKey))
                {
                    throw new InvalidOperationException(
                        $"Gameplay camera shake profile is missing required key '{requiredKey}'.");
                }
            }

            return result;
        }

        internal void SetEntriesForTests(params CameraShakeProfileEntry[] configuredEntries)
        {
            entries = configuredEntries ?? Array.Empty<CameraShakeProfileEntry>();
        }

        private static void ValidateEntry(CameraShakeProfileEntry entry, int index)
        {
            if (!Enum.IsDefined(typeof(CameraShakeSemantic), entry.Semantic))
            {
                throw new InvalidOperationException($"Gameplay camera shake profile entry {index} has an invalid semantic.");
            }

            if (!Enum.IsDefined(typeof(CameraShakeVariant), entry.Variant) ||
                !IsSupportedKey(new CameraShakeProfileKey(entry.Semantic, entry.Variant)))
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry {index} has an invalid semantic/variant key.");
            }

            if (!Enum.IsDefined(typeof(CameraShakePriority), entry.Priority))
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{entry.Semantic} / {entry.Variant}' has an invalid priority.");
            }

            if (!IsFinite(entry.DurationSeconds) || entry.DurationSeconds <= 0f)
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{entry.Semantic} / {entry.Variant}' requires a finite positive duration.");
            }

            if (entry.OscillationCycles <= 0)
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{entry.Semantic} / {entry.Variant}' requires positive oscillation cycles.");
            }

            if (!IsFinite(entry.LocalPositionAmplitude) ||
                !IsFinite(entry.LocalRotationAmplitudeDegrees))
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{entry.Semantic} / {entry.Variant}' requires finite amplitudes.");
            }

            if (!IsFinite(entry.AttackSeconds) ||
                entry.AttackSeconds < 0f ||
                entry.AttackSeconds > entry.DurationSeconds)
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{entry.Semantic} / {entry.Variant}' has an invalid attack duration.");
            }

            if (!IsFinite(entry.CooldownSeconds) || entry.CooldownSeconds < 0f)
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{entry.Semantic} / {entry.Variant}' requires a finite non-negative cooldown.");
            }

            ValidateCurve(entry.Semantic, entry.Decay);
        }

        private static CameraShakeProfileKey[] GetRequiredKeys()
        {
            return new[]
            {
                new CameraShakeProfileKey(CameraShakeSemantic.PushSlideLaunch),
                new CameraShakeProfileKey(CameraShakeSemantic.FlipFloorLanding),
                new CameraShakeProfileKey(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakeVariant.FlipHostileStay),
                new CameraShakeProfileKey(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakeVariant.FlipHostileDestroySelf),
                new CameraShakeProfileKey(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakeVariant.FlipHostileFollowThrough),
                new CameraShakeProfileKey(CameraShakeSemantic.PlayerDamageImpact),
                new CameraShakeProfileKey(CameraShakeSemantic.PlayerLethalImpact),
                new CameraShakeProfileKey(CameraShakeSemantic.HeavyEnemyJumpLanding),
            };
        }

        private static bool IsSupportedKey(CameraShakeProfileKey key)
        {
            if (key.Semantic == CameraShakeSemantic.FlipHostileImpact)
            {
                return key.Variant == CameraShakeVariant.FlipHostileStay ||
                       key.Variant == CameraShakeVariant.FlipHostileDestroySelf ||
                       key.Variant == CameraShakeVariant.FlipHostileFollowThrough;
            }

            return key.Variant == CameraShakeVariant.Default;
        }

        private static void ValidateCurve(CameraShakeSemantic semantic, AnimationCurve curve)
        {
            if (curve == null || curve.length < 2)
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{semantic}' requires a decay curve with at least two keys.");
            }

            var keys = curve.keys;
            if (keys[0].time > 0f || keys[keys.Length - 1].time < 1f)
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{semantic}' decay curve must cover normalized time 0 through 1.");
            }

            for (var index = 0; index < keys.Length; index++)
            {
                var key = keys[index];
                if (!IsFinite(key.time) ||
                    !IsFinite(key.value) ||
                    !IsFinite(key.inTangent) ||
                    !IsFinite(key.outTangent))
                {
                    throw new InvalidOperationException(
                        $"Gameplay camera shake profile entry '{semantic}' decay curve contains non-finite data.");
                }
            }

            if (!IsFinite(curve.Evaluate(0f)) ||
                !IsFinite(curve.Evaluate(0.5f)) ||
                !IsFinite(curve.Evaluate(1f)))
            {
                throw new InvalidOperationException(
                    $"Gameplay camera shake profile entry '{semantic}' decay curve evaluates to non-finite data.");
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
