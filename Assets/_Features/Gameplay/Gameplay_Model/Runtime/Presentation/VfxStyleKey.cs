using System;

namespace Game.Feature.Gameplay
{
    [Serializable]
    public struct VfxStyleKey : IEquatable<VfxStyleKey>, IComparable<VfxStyleKey>
    {
        public static readonly VfxStyleKey Default = new(nameof(Default));
        public static readonly VfxStyleKey Green = new(nameof(Green));
        public static readonly VfxStyleKey Yellow = new(nameof(Yellow));
        public static readonly VfxStyleKey Red = new(nameof(Red));
        public static readonly VfxStyleKey Blue = new(nameof(Blue));

        [UnityEngine.SerializeField] private string value;

        public VfxStyleKey(string value)
        {
            this.value = Normalize(value);
        }

        public string Value => Normalize(value);

        public bool IsDefault => string.Equals(Value, Default.Value, StringComparison.Ordinal);

        public int CompareTo(VfxStyleKey other)
        {
            return string.Compare(Value, other.Value, StringComparison.Ordinal);
        }

        public bool Equals(VfxStyleKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is VfxStyleKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(VfxStyleKey left, VfxStyleKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VfxStyleKey left, VfxStyleKey right)
        {
            return !left.Equals(right);
        }

        private static string Normalize(string source)
        {
            return string.IsNullOrWhiteSpace(source)
                ? nameof(Default)
                : source.Trim();
        }
    }
}
