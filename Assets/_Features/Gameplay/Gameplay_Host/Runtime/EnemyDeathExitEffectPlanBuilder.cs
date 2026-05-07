using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct EnemyDeathExitEffectPlan
    {
        public EnemyDeathExitEffectPlan(
            Vector3 targetLocalPosition,
            Vector3 arcLocalDirection,
            float arcHeight,
            float spinDegrees,
            Vector3 spinAxisLocal)
        {
            TargetLocalPosition = targetLocalPosition;
            ArcLocalDirection = arcLocalDirection;
            ArcHeight = arcHeight;
            SpinDegrees = spinDegrees;
            SpinAxisLocal = spinAxisLocal;
        }

        public Vector3 TargetLocalPosition { get; }

        public Vector3 ArcLocalDirection { get; }

        public float ArcHeight { get; }

        public float SpinDegrees { get; }

        public Vector3 SpinAxisLocal { get; }
    }

    internal static class EnemyDeathExitEffectPlanBuilder
    {
        private const float MinimumCellSize = 0.0001f;
        private const float CameraNearPlanePaddingInCells = 0.12f;
        private const float CameraPlaneJitterInCells = 0.18f;
        private const float CameraPlaneBiasWeight = 0.2f;
        private const float CameraPlaneBiasMaxInCells = 0.18f;
        private const float FallbackForwardDistanceInCells = 2.6f;
        private const float FallbackForwardDistanceJitterInCells = 0.6f;
        private const float FallbackPlaneJitterInCells = 0.18f;

        public static EnemyDeathExitEffectPlan Build(
            Transform parent,
            GameplayEntityPose sourceLocalPose,
            GameplayEntityPose? targetLocalPose,
            Camera outputCamera,
            float cellSize,
            int presentationSeed)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var seededValues = new SeededValueSequence(presentationSeed);
            var resolvedCellSize = Mathf.Max(MinimumCellSize, cellSize);
            var planeJitter = new Vector2(
                seededValues.NextRange(-CameraPlaneJitterInCells, CameraPlaneJitterInCells),
                seededValues.NextRange(-CameraPlaneJitterInCells, CameraPlaneJitterInCells)) * resolvedCellSize;
            var arcHeight = seededValues.NextRange(0.1f, 0.2f) * resolvedCellSize;
            var spinDegrees = seededValues.NextSignedRange(240f, 420f);

            if (outputCamera != null &&
                TryResolveCameraForwardTargetLocalPosition(
                    parent,
                    sourceLocalPose,
                    targetLocalPose,
                    outputCamera,
                    resolvedCellSize,
                    planeJitter,
                    out var cameraForwardTargetLocalPosition,
                    out var cameraUpLocalDirection,
                    out var cameraForwardLocalDirection))
            {
                return new EnemyDeathExitEffectPlan(
                    cameraForwardTargetLocalPosition,
                    cameraUpLocalDirection,
                    arcHeight,
                    spinDegrees,
                    cameraForwardLocalDirection);
            }

            var fallbackForwardDirection = sourceLocalPose.Rotation * Vector3.back;
            if (fallbackForwardDirection.sqrMagnitude <= 0.000001f)
            {
                fallbackForwardDirection = Vector3.back;
            }

            var fallbackRightDirection = sourceLocalPose.Rotation * Vector3.right;
            var fallbackUpDirection = sourceLocalPose.Rotation * Vector3.up;
            var fallbackPlaneOffset = ResolveFallbackPlaneOffset(
                sourceLocalPose,
                targetLocalPose,
                fallbackRightDirection,
                fallbackUpDirection,
                resolvedCellSize,
                planeJitter);
            var fallbackDistance = (FallbackForwardDistanceInCells + seededValues.NextRange(0f, FallbackForwardDistanceJitterInCells)) *
                                   resolvedCellSize;
            var fallbackTargetLocalPosition =
                sourceLocalPose.Position +
                (fallbackForwardDirection.normalized * fallbackDistance) +
                fallbackPlaneOffset;
            return new EnemyDeathExitEffectPlan(
                fallbackTargetLocalPosition,
                fallbackUpDirection,
                arcHeight,
                spinDegrees,
                fallbackForwardDirection);
        }

        private static bool TryResolveCameraForwardTargetLocalPosition(
            Transform parent,
            GameplayEntityPose sourceLocalPose,
            GameplayEntityPose? targetLocalPose,
            Camera outputCamera,
            float cellSize,
            Vector2 planeJitter,
            out Vector3 targetLocalPosition,
            out Vector3 arcLocalDirection,
            out Vector3 spinAxisLocal)
        {
            var startWorldPosition = parent.TransformPoint(sourceLocalPose.Position);
            var startCameraLocalPosition = outputCamera.transform.InverseTransformPoint(startWorldPosition);
            if (!IsValidCameraLocalPoint(startCameraLocalPosition))
            {
                targetLocalPosition = default;
                arcLocalDirection = default;
                spinAxisLocal = default;
                return false;
            }

            var targetCameraLocalPosition = startCameraLocalPosition;
            targetCameraLocalPosition.z = outputCamera.nearClipPlane + Mathf.Max(0.05f, CameraNearPlanePaddingInCells * cellSize);
            var planeOffset = ResolveCameraPlaneOffset(
                parent,
                targetLocalPose,
                outputCamera,
                startCameraLocalPosition,
                cellSize,
                planeJitter);
            targetCameraLocalPosition.x += planeOffset.x;
            targetCameraLocalPosition.y += planeOffset.y;

            var targetWorldPosition = outputCamera.transform.TransformPoint(targetCameraLocalPosition);
            if (!IsFinite(targetWorldPosition))
            {
                targetLocalPosition = default;
                arcLocalDirection = default;
                spinAxisLocal = default;
                return false;
            }

            targetLocalPosition = parent.InverseTransformPoint(targetWorldPosition);
            arcLocalDirection = parent.InverseTransformDirection(outputCamera.transform.up).normalized;
            spinAxisLocal = parent.InverseTransformDirection(outputCamera.transform.forward).normalized;
            return true;
        }

        private static Vector2 ResolveCameraPlaneOffset(
            Transform parent,
            GameplayEntityPose? targetLocalPose,
            Camera outputCamera,
            Vector3 startCameraLocalPosition,
            float cellSize,
            Vector2 planeJitter)
        {
            var planeOffset = planeJitter;
            if (!targetLocalPose.HasValue)
            {
                return planeOffset;
            }

            var targetWorldPosition = parent.TransformPoint(targetLocalPose.Value.Position);
            var targetCameraLocalPosition = outputCamera.transform.InverseTransformPoint(targetWorldPosition);
            if (!IsFinite(targetCameraLocalPosition))
            {
                return planeOffset;
            }

            var cameraPlaneDirection = new Vector2(
                targetCameraLocalPosition.x - startCameraLocalPosition.x,
                targetCameraLocalPosition.y - startCameraLocalPosition.y);
            if (cameraPlaneDirection.sqrMagnitude <= 0.000001f)
            {
                return planeOffset;
            }

            var biasMagnitude = Mathf.Min(cameraPlaneDirection.magnitude * CameraPlaneBiasWeight, CameraPlaneBiasMaxInCells * cellSize);
            return planeOffset + (cameraPlaneDirection.normalized * biasMagnitude);
        }

        private static Vector3 ResolveFallbackPlaneOffset(
            GameplayEntityPose sourceLocalPose,
            GameplayEntityPose? targetLocalPose,
            Vector3 fallbackRightDirection,
            Vector3 fallbackUpDirection,
            float cellSize,
            Vector2 planeJitter)
        {
            var playerBias = Vector2.zero;
            if (targetLocalPose.HasValue)
            {
                var toTarget = targetLocalPose.Value.Position - sourceLocalPose.Position;
                playerBias = new Vector2(
                    Vector3.Dot(toTarget, fallbackRightDirection.normalized),
                    Vector3.Dot(toTarget, fallbackUpDirection.normalized));
                if (playerBias.sqrMagnitude > 0.000001f)
                {
                    playerBias = playerBias.normalized * Mathf.Min(playerBias.magnitude * 0.2f, FallbackPlaneJitterInCells * cellSize);
                }
            }

            var combinedPlaneOffset = playerBias + planeJitter;
            return (fallbackRightDirection.normalized * combinedPlaneOffset.x) +
                   (fallbackUpDirection.normalized * combinedPlaneOffset.y);
        }

        private static bool IsValidCameraLocalPoint(Vector3 point)
        {
            return point.z > 0.0001f && IsFinite(point);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }
    }

    internal struct SeededValueSequence
    {
        private uint _state;

        public SeededValueSequence(int seed)
        {
            _state = seed != 0
                ? unchecked((uint)seed)
                : 0x9E3779B9u;
        }

        public float NextFloat01()
        {
            return (NextState() & 0x00FFFFFFu) / 16777215f;
        }

        public float NextRange(float min, float max)
        {
            return Mathf.Lerp(min, max, NextFloat01());
        }

        public float NextSignedRange(float minMagnitude, float maxMagnitude)
        {
            var magnitude = NextRange(minMagnitude, maxMagnitude);
            return NextFloat01() < 0.5f
                ? -magnitude
                : magnitude;
        }

        private uint NextState()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value != 0u ? value : 0xA511E9B3u;
            return _state;
        }
    }
}
