using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct FlipImpactStayMotionCommand
    {
        public FlipImpactStayMotionCommand(
            int sourceActionPlanId,
            int boxEntityId,
            int actorEntityId,
            int impactTargetEntityId,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            CubeTopologyState topology,
            Direction sourceFacing,
            Direction impactFacing,
            Vector3 sourceLocalPosition,
            Quaternion sourceLocalRotation,
            Vector3 impactLocalPosition,
            Quaternion impactLocalRotation,
            float durationSeconds,
            float contactNormalizedTime,
            float postContactHoldNormalizedDuration,
            float returnArcMultiplier,
            float arcHeightWorld,
            int presentationSeed)
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
            SourceLocalPosition = sourceLocalPosition;
            SourceLocalRotation = sourceLocalRotation;
            ImpactLocalPosition = impactLocalPosition;
            ImpactLocalRotation = impactLocalRotation;
            DurationSeconds = Mathf.Max(0.0001f, durationSeconds);
            ContactNormalizedTime = Mathf.Clamp01(contactNormalizedTime);
            PostContactHoldNormalizedDuration = Mathf.Clamp01(postContactHoldNormalizedDuration);
            ReturnArcMultiplier = Mathf.Max(0f, returnArcMultiplier);
            ArcHeightWorld = Mathf.Max(0f, arcHeightWorld);
            PresentationSeed = presentationSeed;
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

        public Vector3 SourceLocalPosition { get; }

        public Quaternion SourceLocalRotation { get; }

        public Vector3 ImpactLocalPosition { get; }

        public Quaternion ImpactLocalRotation { get; }

        public float DurationSeconds { get; }

        public float ContactNormalizedTime { get; }

        public float PostContactHoldNormalizedDuration { get; }

        public float ReturnArcMultiplier { get; }

        public float ArcHeightWorld { get; }

        public int PresentationSeed { get; }

        internal GameplayEntityPose SourcePose => new(SourceLocalPosition, SourceLocalRotation);

        internal GameplayEntityPose ImpactPose => new(ImpactLocalPosition, ImpactLocalRotation);
    }
}
