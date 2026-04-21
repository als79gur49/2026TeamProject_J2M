using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public struct StageId : IEquatable<StageId>
    {
        public static readonly StageId None = new(string.Empty);

        [SerializeField] private string value;

        public StageId(string canonicalValue)
        {
            value = StageIdNormalizer.IsCanonical(canonicalValue)
                ? canonicalValue
                : string.Empty;
        }

        public string Value => value ?? string.Empty;

        public bool IsValid => StageIdNormalizer.IsCanonical(Value);

        public static bool TryCreate(string rawValue, out StageId stageId)
        {
            if (StageIdNormalizer.TryNormalize(rawValue, out var canonicalValue, out _))
            {
                stageId = new StageId(canonicalValue);
                return true;
            }

            stageId = None;
            return false;
        }

        public static StageId CreateOrThrow(string rawValue)
        {
            if (!TryCreate(rawValue, out var stageId))
            {
                throw new ArgumentException(
                    $"'{rawValue}' cannot be normalized into a canonical {nameof(StageId)}.",
                    nameof(rawValue));
            }

            return stageId;
        }

        public bool Equals(StageId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StageId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
