using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    public readonly struct EnemyDeathMotionVfxCommand
    {
        public EnemyDeathMotionVfxCommand(
            int entityId,
            int? sourceActorEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            Direction facing,
            Vector3 sourceLocalPosition,
            Quaternion sourceLocalRotation,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            Vector3 arcLocalDirection,
            float flightDurationSeconds,
            float fadeStartSeconds,
            float fadeDurationSeconds,
            float arcHeight,
            float spinDegrees,
            Vector3 spinAxisLocal,
            int presentationSeed)
        {
            EntityId = entityId;
            SourceActorEntityId = sourceActorEntityId;
            SourceCell = sourceCell;
            Topology = topology;
            Facing = facing;
            SourceLocalPosition = sourceLocalPosition;
            SourceLocalRotation = sourceLocalRotation;
            TargetLocalPosition = targetLocalPosition;
            TargetLocalRotation = targetLocalRotation;
            ArcLocalDirection = arcLocalDirection;
            FlightDurationSeconds = Mathf.Max(0.0001f, flightDurationSeconds);
            FadeStartSeconds = Mathf.Clamp(fadeStartSeconds, 0f, FlightDurationSeconds);
            FadeDurationSeconds = Mathf.Max(0f, fadeDurationSeconds);
            ArcHeight = Mathf.Max(0f, arcHeight);
            SpinDegrees = spinDegrees;
            SpinAxisLocal = spinAxisLocal;
            PresentationSeed = presentationSeed;
        }

        public int EntityId { get; }

        public int? SourceActorEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public Vector3 SourceLocalPosition { get; }

        public Quaternion SourceLocalRotation { get; }

        public Vector3 TargetLocalPosition { get; }

        public Quaternion TargetLocalRotation { get; }

        public Vector3 ArcLocalDirection { get; }

        public float FlightDurationSeconds { get; }

        public float FadeStartSeconds { get; }

        public float FadeDurationSeconds { get; }

        public float ArcHeight { get; }

        public float SpinDegrees { get; }

        public Vector3 SpinAxisLocal { get; }

        public int PresentationSeed { get; }

        public ParameterizedMotionVfxCommand ToParameterizedMotionVfxCommand()
        {
            return new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(EnemyVfxCue.DeathMotion),
                EntityId,
                PresentationSeed != 0 ? PresentationSeed : EntityId,
                PresentationSeed != 0 ? PresentationSeed : EntityId,
                SourceLocalPosition,
                SourceLocalRotation,
                TargetLocalPosition,
                TargetLocalRotation,
                FlightDurationSeconds,
                ArcHeight,
                FadeStartSeconds,
                FadeDurationSeconds,
                ParameterizedMotionVfxFadeMode.LegacyEnemyDeath,
                ParameterizedMotionVfxCloneMode.PrefabWithSourceClone,
                ParameterizedMotionVfxSamplerMode.LegacyEnemyDeathFlyAway,
                ArcLocalDirection,
                SpinDegrees,
                SpinAxisLocal);
        }
    }

}
