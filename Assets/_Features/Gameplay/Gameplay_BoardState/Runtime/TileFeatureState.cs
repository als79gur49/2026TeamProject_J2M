using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Serializable]
    public readonly struct TileFeatureState : IEquatable<TileFeatureState>
    {
        public TileFeatureState(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            TileFeatureFlags flags,
            int sourceEntityId,
            int ownerEntityId,
            int teamId,
            int lifetimeTicks,
            int charges)
        {
            TileId = tileId;
            Cell = cell;
            Kind = kind;
            Flags = flags;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
            LifetimeTicks = lifetimeTicks;
            Charges = charges;
        }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public TileFeatureKind Kind { get; }

        public TileFeatureFlags Flags { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }

        public int LifetimeTicks { get; }

        public int Charges { get; }

        public bool Equals(TileFeatureState other)
        {
            return TileId == other.TileId &&
                   Cell.Equals(other.Cell) &&
                   Kind == other.Kind &&
                   Flags == other.Flags &&
                   SourceEntityId == other.SourceEntityId &&
                   OwnerEntityId == other.OwnerEntityId &&
                   TeamId == other.TeamId &&
                   LifetimeTicks == other.LifetimeTicks &&
                   Charges == other.Charges;
        }

        public override bool Equals(object obj)
        {
            return obj is TileFeatureState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TileId;
                hash = (hash * 397) ^ Cell.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (int)Flags;
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ OwnerEntityId;
                hash = (hash * 397) ^ TeamId;
                hash = (hash * 397) ^ LifetimeTicks;
                hash = (hash * 397) ^ Charges;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Cell}|TileId={TileId}|Kind={Kind}|Flags={Flags}";
        }
    }
}
