using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct FlipImpactContactVfxAnchor
    {
        public FlipImpactContactVfxAnchor(
            int sourceActionPlanId,
            int boxEntityId,
            int actorEntityId,
            int impactTargetEntityId,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            CubeTopologyState topology,
            Direction sourceFacing,
            Direction impactFacing,
            FlipImpactPresentationDisposition disposition,
            float contactNormalizedTime)
        {
            SourceActionPlanId = sourceActionPlanId;
            BoxEntityId = boxEntityId;
            ActorEntityId = actorEntityId;
            ImpactTargetEntityId = impactTargetEntityId;
            SourceCell = sourceCell;
            ImpactCell = impactCell;
            Topology = topology;
            SourceFacing = sourceFacing;
            ImpactFacing = impactFacing;
            Disposition = disposition;
            ContactNormalizedTime = contactNormalizedTime;
        }

        public int SourceActionPlanId { get; }

        public int BoxEntityId { get; }

        public int ActorEntityId { get; }

        public int ImpactTargetEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ImpactCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction SourceFacing { get; }

        public Direction ImpactFacing { get; }

        public FlipImpactPresentationDisposition Disposition { get; }

        public float ContactNormalizedTime { get; }
    }
}
