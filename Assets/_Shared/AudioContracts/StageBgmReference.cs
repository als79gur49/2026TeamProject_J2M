using System;
using UnityEngine;

namespace Game.Shared.AudioContracts
{
    [Serializable]
    public struct StageBgmReference : IEquatable<StageBgmReference>
    {
        public static readonly StageBgmReference None = new(string.Empty);

        [SerializeField] private string bgmKey;

        public StageBgmReference(string bgmKey)
        {
            this.bgmKey = string.IsNullOrWhiteSpace(bgmKey)
                ? string.Empty
                : bgmKey.Trim();
        }

        public string BgmKey => bgmKey ?? string.Empty;

        public bool HasValue => !string.IsNullOrEmpty(BgmKey);

        public bool Equals(StageBgmReference other)
        {
            return string.Equals(BgmKey, other.BgmKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StageBgmReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(BgmKey);
        }

        public override string ToString()
        {
            return BgmKey;
        }
    }
}
