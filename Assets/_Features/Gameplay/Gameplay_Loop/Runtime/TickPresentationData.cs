using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public enum TickEntityMotionKind
    {
        None = 0,
        Push = 1,
        Flip = 2,
    }

    public readonly struct TickEntityMotion
    {
        public TickEntityMotion(
            int entityId,
            TickEntityMotionKind motionKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
        {
            EntityId = entityId;
            MotionKind = motionKind;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
        }

        public int EntityId { get; }

        public TickEntityMotionKind MotionKind { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }
    }

    public sealed class TickPresentationData
    {
        public static readonly TickPresentationData Empty = new(Array.Empty<TickEntityMotion>());

        private readonly ReadOnlyCollection<TickEntityMotion> _entityMotions;

        public TickPresentationData(IEnumerable<TickEntityMotion> entityMotions)
        {
            if (entityMotions == null)
            {
                throw new ArgumentNullException(nameof(entityMotions));
            }

            _entityMotions = new ReadOnlyCollection<TickEntityMotion>(new List<TickEntityMotion>(entityMotions));
        }

        public IReadOnlyList<TickEntityMotion> EntityMotions => _entityMotions;
    }
}
