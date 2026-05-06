using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
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
                ParameterizedMotionVfxCloneMode.SourceViewCloneWithPrefabFallback,
                ParameterizedMotionVfxSamplerMode.LegacyEnemyDeathFlyAway,
                ArcLocalDirection,
                SpinDegrees,
                SpinAxisLocal);
        }
    }

    public readonly struct EnemyDeathMotionTarget
    {
        public EnemyDeathMotionTarget(
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            Vector3 arcLocalDirection,
            float arcHeight,
            float spinDegrees,
            Vector3 spinAxisLocal)
        {
            TargetLocalPosition = targetLocalPosition;
            TargetLocalRotation = targetLocalRotation;
            ArcLocalDirection = arcLocalDirection;
            ArcHeight = arcHeight;
            SpinDegrees = spinDegrees;
            SpinAxisLocal = spinAxisLocal;
        }

        public Vector3 TargetLocalPosition { get; }

        public Quaternion TargetLocalRotation { get; }

        public Vector3 ArcLocalDirection { get; }

        public float ArcHeight { get; }

        public float SpinDegrees { get; }

        public Vector3 SpinAxisLocal { get; }
    }

    public interface IEnemyDeathMotionTargetResolver
    {
        bool TryResolveDeathMotionTarget(
            in TickEntityExitPresentationSignal signal,
            in GameplayEntityPose sourceLocalPose,
            int presentationSeed,
            out EnemyDeathMotionTarget target);
    }

    public sealed class EnemyDeathMotionTargetResolver : IEnemyDeathMotionTargetResolver
    {
        private readonly Transform localSpaceRoot;
        private readonly Camera outputCamera;
        private readonly GameplayPresentationStateStore stateStore;
        private readonly float cellSize;

        public EnemyDeathMotionTargetResolver(
            Transform localSpaceRoot,
            Camera outputCamera,
            GameplayPresentationStateStore stateStore,
            float cellSize)
        {
            this.localSpaceRoot = localSpaceRoot;
            this.outputCamera = outputCamera;
            this.stateStore = stateStore;
            this.cellSize = cellSize;
        }

        public bool TryResolveDeathMotionTarget(
            in TickEntityExitPresentationSignal signal,
            in GameplayEntityPose sourceLocalPose,
            int presentationSeed,
            out EnemyDeathMotionTarget target)
        {
            target = default;
            if (localSpaceRoot == null ||
                outputCamera == null ||
                stateStore == null)
            {
                return false;
            }

            var targetLocalPose = TryResolvePlayerLocalPose(out var playerLocalPose)
                ? playerLocalPose
                : ResolveActorLocalPose(signal);
            var plan = EnemyDeathExitEffectPlanBuilder.Build(
                localSpaceRoot,
                sourceLocalPose,
                targetLocalPose,
                outputCamera,
                cellSize,
                presentationSeed);
            target = new EnemyDeathMotionTarget(
                plan.TargetLocalPosition,
                sourceLocalPose.Rotation,
                plan.ArcLocalDirection,
                plan.ArcHeight,
                plan.SpinDegrees,
                plan.SpinAxisLocal);
            return true;
        }

        private GameplayEntityPose? ResolveActorLocalPose(in TickEntityExitPresentationSignal signal)
        {
            return signal.SourceActorEntityId.HasValue &&
                   stateStore.CommittedLocalTargetPoses.TryGetValue(signal.SourceActorEntityId.Value, out var actorLocalPose)
                ? actorLocalPose
                : null;
        }

        private bool TryResolvePlayerLocalPose(out GameplayEntityPose localPose)
        {
            var bestPlayerEntityId = int.MaxValue;
            localPose = default;

            foreach (var pair in stateStore.UnitRolesByEntityId)
            {
                if (pair.Value != UnitRole.Player ||
                    !stateStore.CommittedLocalTargetPoses.TryGetValue(pair.Key, out var candidateLocalPose) ||
                    pair.Key >= bestPlayerEntityId)
                {
                    continue;
                }

                bestPlayerEntityId = pair.Key;
                localPose = candidateLocalPose;
            }

            return bestPlayerEntityId != int.MaxValue;
        }
    }

    internal static class EnemyDeathMotionVfxCommandBuilder
    {
        private const float LegacyFadeStartNormalizedTime = 0.12f;

        public static bool TryBuild(
            in TickEntityExitPresentationSignal signal,
            GameplayTimingProfile timingProfile,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector,
            IEnemyDeathMotionTargetResolver targetResolver,
            out EnemyDeathMotionVfxCommand command)
        {
            command = default;
            if (timingProfile == null ||
                poseResolver == null ||
                projector == null ||
                targetResolver == null ||
                signal.EntityType != EntityType.Unit ||
                signal.ExitedEntityId <= 0 ||
                !IsEnemyDeathExitCause(signal.ExitCause))
            {
                return false;
            }

            if (!poseResolver.TryResolveEntityExitSignalLocalPose(projector, signal, out var sourceLocalPose))
            {
                return false;
            }

            var presentationSeed = signal.PresentationSeed != 0
                ? signal.PresentationSeed
                : signal.ExitedEntityId;
            if (!targetResolver.TryResolveDeathMotionTarget(
                    signal,
                    sourceLocalPose,
                    presentationSeed,
                    out var target))
            {
                return false;
            }

            var durationSeconds = Mathf.Max(0.0001f, timingProfile.EnemyDeathEffectDurationSeconds);
            var fadeStartSeconds = durationSeconds * LegacyFadeStartNormalizedTime;
            command = new EnemyDeathMotionVfxCommand(
                signal.ExitedEntityId,
                signal.SourceActorEntityId,
                signal.SourceCell,
                signal.Topology,
                signal.Facing,
                sourceLocalPose.Position,
                sourceLocalPose.Rotation,
                target.TargetLocalPosition,
                target.TargetLocalRotation,
                target.ArcLocalDirection,
                durationSeconds,
                fadeStartSeconds,
                durationSeconds - fadeStartSeconds,
                target.ArcHeight,
                target.SpinDegrees,
                target.SpinAxisLocal,
                presentationSeed);
            return true;
        }

        public static bool IsEnemyDeathExitCandidate(in TickEntityExitPresentationSignal signal)
        {
            return signal.EntityType == EntityType.Unit &&
                   signal.ExitedEntityId > 0 &&
                   IsEnemyDeathExitCause(signal.ExitCause);
        }

        private static bool IsEnemyDeathExitCause(TickEntityExitCause exitCause)
        {
            return exitCause == TickEntityExitCause.Killed;
        }
    }
}
