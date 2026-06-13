using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Vfx
{
    public readonly struct VfxPersistentKey : IEquatable<VfxPersistentKey>, IComparable<VfxPersistentKey>
    {
        public static readonly VfxPersistentKey None = new(
            GameplayVfxCueId.None,
            VfxAnchorKind.None,
            0,
            0,
            default,
            false,
            0,
            0,
            0);

        public VfxPersistentKey(
            GameplayVfxCueId cueId,
            VfxAnchorKind anchorKind,
            int entityId = 0,
            int tileId = 0,
            SurfaceCell cell = default,
            bool hasCell = false,
            int effectIndex = 0,
            int activationSequence = 0,
            int stageScopeId = 0)
        {
            CueId = cueId;
            AnchorKind = anchorKind;
            EntityId = entityId;
            TileId = tileId;
            Cell = cell;
            HasCell = hasCell;
            EffectIndex = effectIndex;
            ActivationSequence = activationSequence;
            StageScopeId = stageScopeId;
        }

        public GameplayVfxCueId CueId { get; }

        public VfxAnchorKind AnchorKind { get; }

        public int EntityId { get; }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public bool HasCell { get; }

        public int EffectIndex { get; }

        public int ActivationSequence { get; }

        public int StageScopeId { get; }

        public bool IsNone => CueId.IsNone;

        public int CompareTo(VfxPersistentKey other)
        {
            var cueCompare = CueId.CompareTo(other.CueId);
            if (cueCompare != 0)
            {
                return cueCompare;
            }

            var anchorCompare = AnchorKind.CompareTo(other.AnchorKind);
            if (anchorCompare != 0)
            {
                return anchorCompare;
            }

            var entityCompare = EntityId.CompareTo(other.EntityId);
            if (entityCompare != 0)
            {
                return entityCompare;
            }

            var tileCompare = TileId.CompareTo(other.TileId);
            if (tileCompare != 0)
            {
                return tileCompare;
            }

            var hasCellCompare = HasCell.CompareTo(other.HasCell);
            if (hasCellCompare != 0)
            {
                return hasCellCompare;
            }

            if (HasCell)
            {
                var cellCompare = VfxOrdering.CompareCell(Cell, other.Cell);
                if (cellCompare != 0)
                {
                    return cellCompare;
                }
            }

            var effectCompare = EffectIndex.CompareTo(other.EffectIndex);
            if (effectCompare != 0)
            {
                return effectCompare;
            }

            var activationCompare = ActivationSequence.CompareTo(other.ActivationSequence);
            return activationCompare != 0 ? activationCompare : StageScopeId.CompareTo(other.StageScopeId);
        }

        public bool Equals(VfxPersistentKey other)
        {
            return CueId.Equals(other.CueId)
                && AnchorKind == other.AnchorKind
                && EntityId == other.EntityId
                && TileId == other.TileId
                && HasCell == other.HasCell
                && (!HasCell || Cell.Equals(other.Cell))
                && EffectIndex == other.EffectIndex
                && ActivationSequence == other.ActivationSequence
                && StageScopeId == other.StageScopeId;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxPersistentKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CueId.GetHashCode();
                hash = (hash * 397) ^ (int)AnchorKind;
                hash = (hash * 397) ^ EntityId;
                hash = (hash * 397) ^ TileId;
                hash = (hash * 397) ^ HasCell.GetHashCode();
                if (HasCell)
                {
                    hash = (hash * 397) ^ Cell.GetHashCode();
                }

                hash = (hash * 397) ^ EffectIndex;
                hash = (hash * 397) ^ ActivationSequence;
                hash = (hash * 397) ^ StageScopeId;
                return hash;
            }
        }

        public static bool operator ==(VfxPersistentKey left, VfxPersistentKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VfxPersistentKey left, VfxPersistentKey right)
        {
            return !left.Equals(right);
        }
    }
}
