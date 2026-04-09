using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayMotionTimingResolver
    {
        private const float DefaultJumpArcHeightInCells = 0.75f;

        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

        public GameplayMotionTimingResolver(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
        }

        public float ResolveMotionDurationSeconds(
            int entityId,
            TickEntityMotionKind motionKind,
            GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            if (TryResolveUnitMotionOverrideDurationSeconds(entityId, motionKind, out var durationSeconds) ||
                TryResolveEntityMotionOverrideDurationSeconds(entityId, motionKind, out durationSeconds))
            {
                return durationSeconds;
            }

            return ResolveGlobalMotionDurationSeconds(motionKind, timingProfile);
        }

        public float ResolvePlayerAnimationStateMotionDurationSeconds(
            int entityId,
            PlayerViewAnimationState animationState,
            GameplayTimingProfile timingProfile)
        {
            return animationState switch
            {
                PlayerViewAnimationState.Push => ResolveMotionDurationSeconds(entityId, TickEntityMotionKind.Push, timingProfile),
                PlayerViewAnimationState.Flip => ResolveMotionDurationSeconds(entityId, TickEntityMotionKind.Flip, timingProfile),
                _ => 0f,
            };
        }

        public float ResolvePlayerMotionDurationSeconds(
            int entityId,
            PlayerActionKind actionKind,
            GameplayTimingProfile timingProfile)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => ResolveMotionDurationSeconds(entityId, TickEntityMotionKind.Push, timingProfile),
                PlayerActionKind.Flip => ResolveMotionDurationSeconds(entityId, TickEntityMotionKind.Flip, timingProfile),
                _ => 0f,
            };
        }

        public float ResolveGlobalMotionDurationSeconds(
            TickEntityMotionKind motionKind,
            GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            return motionKind switch
            {
                TickEntityMotionKind.Move => timingProfile.MoveMotionDurationSeconds,
                TickEntityMotionKind.Flip => timingProfile.FlipMotionDurationSeconds,
                TickEntityMotionKind.Push => timingProfile.PushMotionDurationSeconds,
                TickEntityMotionKind.BoxSlide => timingProfile.BoxSlideStepIntervalSeconds,
                TickEntityMotionKind.ProjectileMove => timingProfile.ProjectileStepIntervalSeconds,
                _ => timingProfile.PushMotionDurationSeconds,
            };
        }

        public bool TryResolveEntityMotionOverrideDurationSeconds(
            int entityId,
            TickEntityMotionKind motionKind,
            out float durationSeconds)
        {
            durationSeconds = 0f;

            if (!_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) ||
                view == null)
            {
                return false;
            }

            var authoring = EntityMotionPresentationAuthoring.GetOptionalValidatedAuthoring(view);
            return authoring != null &&
                   authoring.TryGetMotionDurationOverride(motionKind, out durationSeconds);
        }

        public bool TryResolveUnitMotionOverrideDurationSeconds(
            int entityId,
            TickEntityMotionKind motionKind,
            out float durationSeconds)
        {
            durationSeconds = 0f;

            if (motionKind != TickEntityMotionKind.Move ||
                !_stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType) ||
                entityType != EntityType.Unit ||
                !_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) ||
                view == null)
            {
                return false;
            }

            var authoring = UnitLocomotionPresentationAuthoring.GetOptionalValidatedAuthoring(view);
            return authoring != null &&
                   authoring.TryGetMotionDurationOverride(motionKind, out durationSeconds);
        }

        public float ResolveTopologyMotionDurationSeconds(GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            return timingProfile.TopologyMotionDurationSeconds;
        }

        public float ResolveVisibilityDurationSeconds(int entityId, GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            // Legacy detach/remove visibility tracks borrow push presentation timing as a
            // minimum hide tail. Exit-owned removals must not route through this fallback.
            var durationSeconds = timingProfile.PushMotionDurationSeconds;
            if (_trackState.LocalMotionTracks.TryGetValue(entityId, out var track))
            {
                durationSeconds = Mathf.Max(durationSeconds, track.TotalRemainingSeconds);
            }

            if (_trackState.JumpTracks.TryGetValue(entityId, out var jumpTrack))
            {
                durationSeconds = Mathf.Max(durationSeconds, jumpTrack.RemainingSeconds);
            }

            return durationSeconds;
        }

        public float ResolveJumpDurationSeconds(
            TickEnemyJumpPresentationSignal signal,
            GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            return Mathf.Max(
                0.0001f,
                Mathf.Max(0, signal.RemainingAirborneTicks) * timingProfile.SimulationTickIntervalSeconds);
        }

        public float ResolveJumpArcHeightWorld(int entityId, GameplayCubeProjector projector)
        {
            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            return DefaultJumpArcHeightInCells * projector.CellSize;
        }
    }
}
