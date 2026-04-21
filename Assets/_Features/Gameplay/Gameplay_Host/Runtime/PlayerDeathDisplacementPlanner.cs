using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class PlayerDeathDisplacementPlanner
    {
        private const float DirectionEpsilon = 0.000001f;

        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayPresentationTrackState _trackState;

        private Camera _outputCamera;
        private Transform _outputCameraLocalSpaceRoot;

        public PlayerDeathDisplacementPlanner(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            GameplayPoseResolver poseResolver)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _poseResolver = poseResolver ?? throw new ArgumentNullException(nameof(poseResolver));
        }

        public void ConfigureOutputCamera(Camera outputCamera, Transform localSpaceRoot)
        {
            _outputCamera = outputCamera;
            _outputCameraLocalSpaceRoot = localSpaceRoot;
        }

        public void RefreshTracks(
            TickPresentationData presentationData,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            if (presentationData == null)
            {
                throw new ArgumentNullException(nameof(presentationData));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            ReleaseTracksForRespawnSpawns(presentationData.VisibilityChanges);

            var playerDeathSignals = presentationData.PlayerDeathSignals;
            for (var i = 0; i < playerDeathSignals.Count; i++)
            {
                var signal = playerDeathSignals[i];
                if (!signal.DidDieThisTick ||
                    _trackState.PlayerDeathDisplacementTracks.ContainsKey(signal.EntityId) ||
                    !_poseResolver.TryResolveFallbackLocalPose(signal.EntityId, out var playerPose))
                {
                    continue;
                }

                var finalDirection = ResolveDeathDirection(playerPose, signal, timingProfile);
                var targetOffsetLocal = finalDirection * (projector.CellSize * timingProfile.PlayerDeathDisplacementDistanceInCells);
                _trackState.PlayerDeathDisplacementTracks[signal.EntityId] = new PlayerDeathDisplacementTrack(
                    targetOffsetLocal,
                    timingProfile.PlayerDeathDisplacementDurationSeconds);
            }
        }

        private void ReleaseTracksForRespawnSpawns(IReadOnlyList<TickVisibilityChange> visibilityChanges)
        {
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.ChangeKind != TickVisibilityChangeKind.Spawn)
                {
                    continue;
                }

                ClearTrack(change.EntityId);
            }
        }

        private Vector3 ResolveDeathDirection(
            in GameplayEntityPose playerPose,
            in TickPlayerDeathPresentationSignal signal,
            GameplayTimingProfile timingProfile)
        {
            var surfaceNormal = ResolveSurfaceNormal(playerPose);
            var fallbackFacingReverse = ResolveProjectedFallbackFacingReverse(playerPose, signal.FallbackFacing, surfaceNormal);
            var allowCameraBias = TryResolveBaseDirection(
                playerPose,
                signal,
                surfaceNormal,
                fallbackFacingReverse,
                out var baseDirection);
            if (!allowCameraBias ||
                !TryResolveCameraBias(playerPose.Position, surfaceNormal, out var cameraBias))
            {
                return baseDirection;
            }

            var mixedDirection = baseDirection * (1f - timingProfile.PlayerDeathDisplacementCameraBiasWeight) +
                                 (cameraBias * timingProfile.PlayerDeathDisplacementCameraBiasWeight);
            if (TryNormalize(mixedDirection, out var normalizedMixedDirection))
            {
                return normalizedMixedDirection;
            }

            return baseDirection;
        }

        private bool TryResolveBaseDirection(
            in GameplayEntityPose playerPose,
            in TickPlayerDeathPresentationSignal signal,
            Vector3 surfaceNormal,
            Vector3 fallbackFacingReverse,
            out Vector3 baseDirection)
        {
            baseDirection = Vector3.zero;

            if (signal.ResolvedDamageSourceAvailable &&
                signal.SourceEntityId > 0 &&
                TryResolveAttackerPose(signal.SourceEntityId, out var attackerPose))
            {
                var rawBaseDirection = playerPose.Position - attackerPose.Position;
                var projectedBaseDirection = ProjectOntoTangent(rawBaseDirection, surfaceNormal);
                if (TryNormalize(projectedBaseDirection, out baseDirection))
                {
                    return true;
                }

                baseDirection = fallbackFacingReverse;
                return false;
            }

            baseDirection = fallbackFacingReverse;
            return true;
        }

        private bool TryResolveAttackerPose(int entityId, out GameplayEntityPose attackerPose)
        {
            if (_stateStore.PresentedLocalPosesByEntityId.TryGetValue(entityId, out attackerPose))
            {
                return true;
            }

            return _poseResolver.TryResolveFallbackLocalPose(entityId, out attackerPose);
        }

        private bool TryResolveCameraBias(
            Vector3 playerLocalPosition,
            Vector3 surfaceNormal,
            out Vector3 cameraBias)
        {
            cameraBias = Vector3.zero;
            if (_outputCamera == null ||
                _outputCameraLocalSpaceRoot == null)
            {
                return false;
            }

            var outputCameraLocalPosition = _outputCameraLocalSpaceRoot.InverseTransformPoint(_outputCamera.transform.position);
            var rawCameraBias = outputCameraLocalPosition - playerLocalPosition;
            var projectedCameraBias = ProjectOntoTangent(rawCameraBias, surfaceNormal);
            return TryNormalize(projectedCameraBias, out cameraBias);
        }

        private static Vector3 ResolveSurfaceNormal(in GameplayEntityPose playerPose)
        {
            var surfaceNormal = -(playerPose.Rotation * Vector3.forward);
            if (surfaceNormal.sqrMagnitude > DirectionEpsilon)
            {
                return surfaceNormal.normalized;
            }

            return Vector3.back;
        }

        private static Vector3 ResolveProjectedFallbackFacingReverse(
            in GameplayEntityPose playerPose,
            Direction fallbackFacing,
            Vector3 surfaceNormal)
        {
            var rawFallbackFacingReverse = fallbackFacing switch
            {
                Direction.None => playerPose.Rotation * Vector3.down,
                _ => playerPose.Rotation * Vector3.down,
            };
            var projectedFallbackFacingReverse = ProjectOntoTangent(rawFallbackFacingReverse, surfaceNormal);
            if (TryNormalize(projectedFallbackFacingReverse, out var fallbackFacingReverse))
            {
                return fallbackFacingReverse;
            }

            var finalFallback = ProjectOntoTangent(playerPose.Rotation * Vector3.down, surfaceNormal);
            if (TryNormalize(finalFallback, out fallbackFacingReverse))
            {
                return fallbackFacingReverse;
            }

            return TryNormalize(playerPose.Rotation * Vector3.down, out fallbackFacingReverse)
                ? fallbackFacingReverse
                : Vector3.down;
        }

        private static Vector3 ProjectOntoTangent(Vector3 vector, Vector3 surfaceNormal)
        {
            return vector - (Vector3.Dot(vector, surfaceNormal) * surfaceNormal);
        }

        private static bool TryNormalize(Vector3 vector, out Vector3 normalized)
        {
            if (vector.sqrMagnitude <= DirectionEpsilon)
            {
                normalized = Vector3.zero;
                return false;
            }

            normalized = vector.normalized;
            return true;
        }

        private void ClearTrack(int entityId)
        {
            if (_trackState.PlayerDeathDisplacementTracks.TryGetValue(entityId, out var track) &&
                track != null)
            {
                track.Clear();
            }

            _trackState.PlayerDeathDisplacementTracks.Remove(entityId);
        }
    }
}
