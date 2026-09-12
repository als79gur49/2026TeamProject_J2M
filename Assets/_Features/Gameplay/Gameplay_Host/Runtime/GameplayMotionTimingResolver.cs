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
    /// <summary>
    /// Centralized presentation-only timing shared by flip-impact box tracks and player interaction tracks.
    /// <para>
    /// <see cref="ContactNormalizedTime"/> has disposition-specific semantics:
    /// Stay uses it as the contact/recoil branch threshold, while DestroySelf uses it as the
    /// break/release onset threshold. DestroySelf final impact-pose arrival still happens at the
    /// end of the full flip flight duration.
    /// </para>
    /// </summary>
    internal readonly struct FlipImpactTimingSettings
    {
        public FlipImpactTimingSettings(
            float contactNormalizedTime,
            float stayReturnArcHeightMultiplier,
            float destroyBreakNormalizedDuration,
            float stayPostContactHoldNormalizedDuration)
        {
            ContactNormalizedTime = Mathf.Clamp01(contactNormalizedTime);
            StayReturnArcHeightMultiplier = Mathf.Max(0f, stayReturnArcHeightMultiplier);
            DestroyBreakNormalizedDuration = Mathf.Clamp01(destroyBreakNormalizedDuration);
            StayPostContactHoldNormalizedDuration = Mathf.Clamp01(stayPostContactHoldNormalizedDuration);
        }

        // Stay: contact/recoil branch threshold.
        // DestroySelf: break/release onset threshold, not final impact-pose arrival.
        public float ContactNormalizedTime { get; }

        public float StayReturnArcHeightMultiplier { get; }

        public float DestroyBreakNormalizedDuration { get; }

        public float StayPostContactHoldNormalizedDuration { get; }
    }

    internal sealed class GameplayMotionTimingResolver
    {
        private const float DefaultJumpArcHeightInCells = 0.75f;
        private const float DefaultFlipImpactContactNormalizedTime =
            GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime;
        private const float DefaultFlipImpactStayReturnArcHeightMultiplier = 0.55f;
        private const float DefaultFlipImpactDestroyBreakNormalizedDuration = 0.22f;
        private const float DefaultFlipImpactStayPostContactHoldNormalizedDuration = 0.18f;

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

        public float ResolvePlayerFlipResultTurnDurationSeconds(
            int entityId,
            GameplayTimingProfile timingProfile)
        {
            return ResolvePlayerPresentationPhaseDurationSeconds(
                entityId,
                PlayerPresentationPhase.FlipRecovery,
                timingProfile);
        }

        public float ResolvePlayerFlipResultTurnDelaySeconds(
            int entityId,
            GameplayTimingProfile timingProfile)
        {
            return ResolvePlayerPresentationPhaseDurationSeconds(
                entityId,
                PlayerPresentationPhase.FlipWindup,
                timingProfile);
        }

        public float ResolvePlayerPresentationPhaseDurationSeconds(
            int entityId,
            PlayerPresentationPhase phase,
            GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            var actionKind = ResolvePlayerActionKind(phase);
            var resolvedActionDurationSeconds = ResolvePlayerMotionDurationSeconds(entityId, actionKind, timingProfile);
            if (_stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) &&
                view != null &&
                view.TryGetComponent<PlayerAnimationTimingAuthoring>(out var authoring) &&
                authoring != null)
            {
                var snapshot = authoring.CreateSnapshot();
                if (snapshot.TryGetAnimatorDurationOverride(phase, out var phaseDurationSeconds))
                {
                    return phaseDurationSeconds;
                }

                if (actionKind != PlayerActionKind.None &&
                    snapshot.TryGetLegacyAnimatorDurationOverride(actionKind, out var legacyActionDurationSeconds))
                {
                    return Mathf.Max(0.0001f, legacyActionDurationSeconds * 0.5f);
                }
            }

            if (resolvedActionDurationSeconds > 0f)
            {
                return Mathf.Max(0.0001f, resolvedActionDurationSeconds * 0.5f);
            }

            return timingProfile.SimulationTickIntervalSeconds;
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
                TickEntityMotionKind.ForwardCellMove => timingProfile.ProjectileStepIntervalSeconds,
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

            // UnitLocomotionPresentationAuthoring only owns generic walk/move presentation.
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

        public float ResolveVisibilityDurationSeconds(
            int entityId,
            TickVisibilityChangeKind changeKind,
            GameplayTimingProfile timingProfile)
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

            // Player death still uses the generic visibility-remove track. Keep its authored
            // animation duration as the hide tail, while enemy exits remain owned by the
            // typed death cue and DeathMotion presentation contract.
            if (changeKind == TickVisibilityChangeKind.Remove &&
                _stateStore.EntityTypesByEntityId.TryGetValue(entityId, out var entityType) &&
                entityType == EntityType.Unit &&
                _stateStore.ViewsByEntityId.TryGetValue(entityId, out var view) &&
                view != null &&
                TryResolvePlayerDeathAnimatorDurationSeconds(view, out var playerDeathDurationSeconds))
            {
                durationSeconds = Mathf.Max(durationSeconds, playerDeathDurationSeconds);
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

        public FlipImpactTimingSettings ResolveFlipImpactTimingSettings(GameplayTimingProfile timingProfile)
        {
            return CreateFlipImpactTimingSettings(timingProfile);
        }

        public static FlipImpactTimingSettings CreateFlipImpactTimingSettings(GameplayTimingProfile timingProfile)
        {
            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            // Stay reads ContactNormalizedTime as the contact/recoil branch threshold.
            // DestroySelf reads the same threshold as break/release onset, while final impact-pose
            // arrival still uses the full resolved flip flight duration.
            return new FlipImpactTimingSettings(
                DefaultFlipImpactContactNormalizedTime,
                DefaultFlipImpactStayReturnArcHeightMultiplier,
                DefaultFlipImpactDestroyBreakNormalizedDuration,
                DefaultFlipImpactStayPostContactHoldNormalizedDuration);
        }

        private static bool TryResolvePlayerDeathAnimatorDurationSeconds(
            GameplayEntityView view,
            out float durationSeconds)
        {
            durationSeconds = 0f;
            if (!view.TryGetComponent<PlayerAnimatorDriver>(out var playerDriver) ||
                playerDriver == null)
            {
                return false;
            }

            durationSeconds = playerDriver.DeathPresentationDurationSeconds;
            return durationSeconds > 0f;
        }

        private static PlayerActionKind ResolvePlayerActionKind(PlayerPresentationPhase phase)
        {
            return phase switch
            {
                PlayerPresentationPhase.PushWindup => PlayerActionKind.Push,
                PlayerPresentationPhase.PushRecovery => PlayerActionKind.Push,
                PlayerPresentationPhase.FlipWindup => PlayerActionKind.Flip,
                PlayerPresentationPhase.FlipRecovery => PlayerActionKind.Flip,
                _ => PlayerActionKind.None,
            };
        }
    }
}
