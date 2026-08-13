using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum CameraShakeSemantic
    {
        PushSlideLaunch = 0,
        FlipFloorLanding = 1,
        FlipHostileImpact = 2,
        PlayerDamageImpact = 3,
        PlayerLethalImpact = 4,
        HeavyEnemyJumpLanding = 5,
    }

    public enum CameraShakeVariant
    {
        Default = 0,
        FlipHostileStay = 1,
        FlipHostileDestroySelf = 2,
        FlipHostileFollowThrough = 3,
    }

    public readonly struct CameraShakeProfileKey : IEquatable<CameraShakeProfileKey>
    {
        public CameraShakeProfileKey(
            CameraShakeSemantic semantic,
            CameraShakeVariant variant = CameraShakeVariant.Default)
        {
            Semantic = semantic;
            Variant = variant;
        }

        public CameraShakeSemantic Semantic { get; }

        public CameraShakeVariant Variant { get; }

        public bool Equals(CameraShakeProfileKey other)
        {
            return Semantic == other.Semantic && Variant == other.Variant;
        }

        public override bool Equals(object obj)
        {
            return obj is CameraShakeProfileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Semantic * 397) ^ (int)Variant;
            }
        }

        public override string ToString()
        {
            return $"{Semantic} / {Variant}";
        }
    }

    public enum CameraShakePriority
    {
        Light = 100,
        Medium = 200,
        Heavy = 300,
        Global = 400,
    }

    public enum CameraShakeSourceKind
    {
        GameplayImpulse = 0,
        TopologyContinuous = 1,
    }

    public enum CameraMotionLevel
    {
        Off = 0,
        Reduced = 1,
        Full = 2,
    }

    public readonly struct CameraShakeImpulseRequest
    {
        public CameraShakeImpulseRequest(
            int tickIndex,
            CameraShakeSemantic semantic,
            int sourceEntityId,
            int sequenceOrActionPlanId,
            CameraShakePriority priority,
            SurfaceCell anchorCell = default,
            bool hasAnchor = false,
            Direction semanticDirection = Direction.None,
            bool hasDirection = false,
            CameraShakeVariant variant = CameraShakeVariant.Default)
        {
            TickIndex = tickIndex;
            Semantic = semantic;
            SourceEntityId = sourceEntityId;
            SequenceOrActionPlanId = sequenceOrActionPlanId;
            Priority = priority;
            AnchorCell = anchorCell;
            HasAnchor = hasAnchor;
            SemanticDirection = semanticDirection;
            HasDirection = hasDirection;
            Variant = variant;
        }

        public int TickIndex { get; }

        public CameraShakeSemantic Semantic { get; }

        public int SourceEntityId { get; }

        public int SequenceOrActionPlanId { get; }

        public CameraShakePriority Priority { get; }

        public CameraShakeVariant Variant { get; }

        public SurfaceCell AnchorCell { get; }

        public bool HasAnchor { get; }

        public Direction SemanticDirection { get; }

        public bool HasDirection { get; }

        internal CameraShakeRequestIdentity Identity => new(
            TickIndex,
            Semantic,
            SourceEntityId,
            SequenceOrActionPlanId);

        internal CameraShakeProfileKey ProfileKey => new(Semantic, Variant);
    }

    public interface ICameraShakeImpulseSink
    {
        bool Submit(in CameraShakeImpulseRequest request);
    }

    /// <summary>
    /// Semantic-free presentation query against the camera's unshaken pose.
    /// The returned z coordinate is camera-forward depth and may be non-positive.
    /// </summary>
    public interface IGameplayCameraVisibilityPort
    {
        bool TryProjectUnshakenWorldPoint(Vector3 worldPosition, out Vector3 viewportPoint);
    }

    internal readonly struct CameraShakeRequestIdentity : IEquatable<CameraShakeRequestIdentity>
    {
        public CameraShakeRequestIdentity(
            int tickIndex,
            CameraShakeSemantic semantic,
            int sourceEntityId,
            int sequenceOrActionPlanId)
        {
            TickIndex = tickIndex;
            Semantic = semantic;
            SourceEntityId = sourceEntityId;
            SequenceOrActionPlanId = sequenceOrActionPlanId;
        }

        public int TickIndex { get; }

        public CameraShakeSemantic Semantic { get; }

        public int SourceEntityId { get; }

        public int SequenceOrActionPlanId { get; }

        public bool Equals(CameraShakeRequestIdentity other)
        {
            return TickIndex == other.TickIndex &&
                   Semantic == other.Semantic &&
                   SourceEntityId == other.SourceEntityId &&
                   SequenceOrActionPlanId == other.SequenceOrActionPlanId;
        }

        public override bool Equals(object obj)
        {
            return obj is CameraShakeRequestIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ (int)Semantic;
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ SequenceOrActionPlanId;
                return hash;
            }
        }
    }

    internal readonly struct CameraShakeCooldownKey : IEquatable<CameraShakeCooldownKey>
    {
        public CameraShakeCooldownKey(CameraShakeSemantic semantic, int sourceEntityId)
        {
            Semantic = semantic;
            SourceEntityId = sourceEntityId;
        }

        public CameraShakeSemantic Semantic { get; }

        public int SourceEntityId { get; }

        public bool Equals(CameraShakeCooldownKey other)
        {
            return Semantic == other.Semantic && SourceEntityId == other.SourceEntityId;
        }

        public override bool Equals(object obj)
        {
            return obj is CameraShakeCooldownKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Semantic * 397) ^ SourceEntityId;
            }
        }
    }

    public readonly struct CameraShakeContribution
    {
        private CameraShakeContribution(
            CameraShakeSourceKind sourceKind,
            CameraShakePriority priority,
            Vector3 localPosition,
            Vector3 localRotationDegrees,
            Quaternion localRotation,
            bool isActive)
        {
            SourceKind = sourceKind;
            Priority = priority;
            LocalPosition = localPosition;
            LocalRotationDegrees = localRotationDegrees;
            LocalRotation = localRotation;
            IsActive = isActive;
        }

        public CameraShakeSourceKind SourceKind { get; }

        public CameraShakePriority Priority { get; }

        public Vector3 LocalPosition { get; }

        public Vector3 LocalRotationDegrees { get; }

        public Quaternion LocalRotation { get; }

        public bool IsActive { get; }

        public static CameraShakeContribution Gameplay(
            CameraShakePriority priority,
            Vector3 localPosition,
            Vector3 localRotationDegrees,
            bool isActive)
        {
            return new CameraShakeContribution(
                CameraShakeSourceKind.GameplayImpulse,
                priority,
                localPosition,
                localRotationDegrees,
                localRotationDegrees.sqrMagnitude <= 0.000000000001f
                    ? Quaternion.identity
                    : Quaternion.Euler(localRotationDegrees),
                isActive);
        }

        internal static CameraShakeContribution Topology(
            Vector3 localPosition,
            Vector3 localRotationDegrees,
            Quaternion exactLocalRotation,
            bool isActive)
        {
            return new CameraShakeContribution(
                CameraShakeSourceKind.TopologyContinuous,
                CameraShakePriority.Global,
                localPosition,
                localRotationDegrees,
                exactLocalRotation,
                isActive);
        }

        public static CameraShakeContribution Inactive => new(
            CameraShakeSourceKind.GameplayImpulse,
            CameraShakePriority.Light,
            Vector3.zero,
            Vector3.zero,
            Quaternion.identity,
            false);
    }

    public readonly struct CameraShakeMixResult
    {
        public CameraShakeMixResult(
            Vector3 localPosition,
            Vector3 localRotationDegrees,
            Quaternion localRotation,
            bool isActive)
        {
            LocalPosition = localPosition;
            LocalRotationDegrees = localRotationDegrees;
            LocalRotation = localRotation;
            IsActive = isActive;
        }

        public Vector3 LocalPosition { get; }

        public Vector3 LocalRotationDegrees { get; }

        public Quaternion LocalRotation { get; }

        public bool IsActive { get; }

        public static CameraShakeMixResult Zero => new(
            Vector3.zero,
            Vector3.zero,
            Quaternion.identity,
            false);
    }

    public static class TopologyCameraShakeContributionAdapter
    {
        public static CameraShakeContribution Create(
            in TopologyTransitionCameraShakeResult result,
            bool isActive)
        {
            return CameraShakeContribution.Topology(
                result.LocalPosition,
                ToSignedEulerDegrees(result.LocalRotation),
                result.LocalRotation,
                isActive);
        }

        private static Vector3 ToSignedEulerDegrees(Quaternion rotation)
        {
            var euler = rotation.eulerAngles;
            return new Vector3(
                ToSignedDegrees(euler.x),
                ToSignedDegrees(euler.y),
                ToSignedDegrees(euler.z));
        }

        private static float ToSignedDegrees(float degrees)
        {
            return degrees > 180f ? degrees - 360f : degrees;
        }
    }
}
