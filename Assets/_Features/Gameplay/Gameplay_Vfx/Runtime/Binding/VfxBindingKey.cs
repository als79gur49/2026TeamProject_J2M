using System;
using Game.Feature.Gameplay;

namespace Game.Feature.Gameplay.Vfx
{
    public readonly struct VfxBindingKey : IEquatable<VfxBindingKey>, IComparable<VfxBindingKey>
    {
        public VfxBindingKey(GameplayVfxCueId cueId, VfxStyleKey styleKey = default)
        {
            CueId = cueId;
            StyleKey = styleKey;
        }

        public GameplayVfxCueId CueId { get; }

        public VfxStyleKey StyleKey { get; }

        public int CompareTo(VfxBindingKey other)
        {
            var cueCompare = CueId.CompareTo(other.CueId);
            return cueCompare != 0 ? cueCompare : StyleKey.CompareTo(other.StyleKey);
        }

        public bool Equals(VfxBindingKey other)
        {
            return CueId.Equals(other.CueId) && StyleKey.Equals(other.StyleKey);
        }

        public override bool Equals(object obj)
        {
            return obj is VfxBindingKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (CueId.GetHashCode() * 397) ^ StyleKey.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{CueId}/{StyleKey}";
        }
    }
}
