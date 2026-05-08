using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Serializable]
    public readonly struct MoonBlockRespawnDefinition : IEquatable<MoonBlockRespawnDefinition>
    {
        public MoonBlockRespawnDefinition(
            int generatorTileId,
            int moonBlockEntityId,
            SurfaceCell spawnCell,
            EntityState template)
        {
            GeneratorTileId = generatorTileId;
            MoonBlockEntityId = moonBlockEntityId;
            SpawnCell = spawnCell;
            Template = template;
        }

        public int GeneratorTileId { get; }

        public int MoonBlockEntityId { get; }

        public SurfaceCell SpawnCell { get; }

        public EntityState Template { get; }

        public bool Equals(MoonBlockRespawnDefinition other)
        {
            return GeneratorTileId == other.GeneratorTileId &&
                   MoonBlockEntityId == other.MoonBlockEntityId &&
                   SpawnCell.Equals(other.SpawnCell) &&
                   Template.Equals(other.Template);
        }

        public override bool Equals(object obj)
        {
            return obj is MoonBlockRespawnDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = GeneratorTileId;
                hash = (hash * 397) ^ MoonBlockEntityId;
                hash = (hash * 397) ^ SpawnCell.GetHashCode();
                hash = (hash * 397) ^ Template.GetHashCode();
                return hash;
            }
        }
    }
}
