using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Serializable]
    public readonly struct TileFeatureRuntimeDefinition : IEquatable<TileFeatureRuntimeDefinition>
    {
        public TileFeatureRuntimeDefinition(
            int tileId,
            TileFeatureActivationRule activationRule,
            Direction2D direction,
            TileFeatureBoxSelector boxSelector,
            int boundEntityId,
            string presentationKey)
        {
            TileId = tileId;
            ActivationRule = activationRule;
            Direction = direction;
            BoxSelector = boxSelector;
            BoundEntityId = boundEntityId;
            PresentationKey = presentationKey ?? string.Empty;
        }

        public int TileId { get; }

        public TileFeatureActivationRule ActivationRule { get; }

        public Direction2D Direction { get; }

        public TileFeatureBoxSelector BoxSelector { get; }

        public int BoundEntityId { get; }

        public string PresentationKey { get; }

        public bool Equals(TileFeatureRuntimeDefinition other)
        {
            return TileId == other.TileId &&
                   ActivationRule == other.ActivationRule &&
                   Direction == other.Direction &&
                   BoxSelector == other.BoxSelector &&
                   BoundEntityId == other.BoundEntityId &&
                   string.Equals(PresentationKey, other.PresentationKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TileFeatureRuntimeDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TileId;
                hash = (hash * 397) ^ (int)ActivationRule;
                hash = (hash * 397) ^ (int)Direction;
                hash = (hash * 397) ^ (int)BoxSelector;
                hash = (hash * 397) ^ BoundEntityId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(PresentationKey ?? string.Empty);
                return hash;
            }
        }
    }
}
