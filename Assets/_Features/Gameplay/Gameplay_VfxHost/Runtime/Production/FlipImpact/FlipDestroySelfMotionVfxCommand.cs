using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public readonly struct FlipDestroySelfMotionVfxCommand
    {
        public FlipDestroySelfMotionVfxCommand(
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
            float flightDurationSeconds,
            float contactNormalizedTime,
            float breakStartSeconds,
            float fadeDurationSeconds,
            float arcHeight,
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
            FlightDurationSeconds = Mathf.Max(0.0001f, flightDurationSeconds);
            ContactNormalizedTime = Mathf.Clamp01(contactNormalizedTime);
            BreakStartSeconds = Mathf.Clamp(breakStartSeconds, 0f, FlightDurationSeconds);
            FadeDurationSeconds = Mathf.Max(0f, fadeDurationSeconds);
            ArcHeight = Mathf.Max(0f, arcHeight);
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

        public float FlightDurationSeconds { get; }

        public float ContactNormalizedTime { get; }

        public float BreakStartSeconds { get; }

        public float FadeDurationSeconds { get; }

        public float ArcHeight { get; }

        public int PresentationSeed { get; }

        internal GameplayEntityPose SourcePose => new(SourceLocalPosition, SourceLocalRotation);

        internal GameplayEntityPose ImpactPose => new(ImpactLocalPosition, ImpactLocalRotation);

        public ParameterizedMotionVfxCommand ToParameterizedMotionVfxCommand()
        {
            return new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion),
                BoxEntityId,
                SourceActionPlanId > 0 ? SourceActionPlanId : BoxEntityId,
                PresentationSeed,
                SourceLocalPosition,
                SourceLocalRotation,
                ImpactLocalPosition,
                ImpactLocalRotation,
                FlightDurationSeconds,
                ArcHeight,
                BreakStartSeconds,
                FadeDurationSeconds,
                ParameterizedMotionVfxFadeMode.ScaleAndAlpha,
                ParameterizedMotionVfxCloneMode.SourceCloneMotion,
                ParameterizedMotionVfxSamplerMode.FlipArc);
        }
    }

}
