using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal readonly struct EnemyDeathMotionTarget
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

    internal interface IEnemyDeathMotionTargetResolver
    {
        bool TryResolveDeathMotionTarget(
            in TickEntityExitPresentationSignal signal,
            in GameplayEntityPose sourceLocalPose,
            int presentationSeed,
            out EnemyDeathMotionTarget target);
    }

    internal sealed class EnemyDeathMotionTargetResolver : IEnemyDeathMotionTargetResolver
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

            var hasContactDelay =
                signal.Timing == EntityExitPresentationTiming.AtContactTime &&
                signal.TimingMode != GameplayPresentationTimingMode.DueContactImmediate &&
                signal.VisualContactNormalizedTime > 0f;
            GameplayEntityPose sourceLocalPose;
            var hasSourcePose = hasContactDelay
                ? poseResolver.TryResolveContactDelayedEntityExitSignalLocalPose(projector, signal, out sourceLocalPose)
                : poseResolver.TryResolveEntityExitSignalLocalPose(projector, signal, out sourceLocalPose);
            if (!hasSourcePose)
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
