using System;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal enum AttachedVfxFollowerStateKind
    {
        None = 0,
        EnemyGlideActive = 1,
        EnemyChargeActive = 2,
        BoxSlideFollow = 3,
        EnemyJumpWindup = 4,
        EnemyGlideWindup = 5,
        EnemyGlideRecover = 6,
        EnemyUtilityCooldownAura = 8,
        EnemyAttackCooldownFollow = 9,
    }

    internal enum AttachedVfxFollowerRetentionPolicy
    {
        RefreshDesiredOnly = 0,
        RetainUntilExplicitStop = 1,
    }

    internal readonly struct AttachedVfxFollowerKey : IEquatable<AttachedVfxFollowerKey>
    {
        public AttachedVfxFollowerKey(
            GameplayVfxCueId cueId,
            int sourceEntityId,
            AttachedVfxFollowerStateKind stateKind,
            int sequenceId,
            string attachPointId = null)
        {
            CueId = cueId;
            SourceEntityId = sourceEntityId;
            StateKind = stateKind;
            SequenceId = sequenceId;
            AttachPointId = NormalizeAttachPointId(attachPointId);
        }

        public GameplayVfxCueId CueId { get; }

        public int SourceEntityId { get; }

        public AttachedVfxFollowerStateKind StateKind { get; }

        public int SequenceId { get; }

        public string AttachPointId { get; }

        public bool Equals(AttachedVfxFollowerKey other)
        {
            return CueId.Equals(other.CueId) &&
                   SourceEntityId == other.SourceEntityId &&
                   StateKind == other.StateKind &&
                   SequenceId == other.SequenceId &&
                   string.Equals(AttachPointId, other.AttachPointId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AttachedVfxFollowerKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = CueId.GetHashCode();
                hashCode = (hashCode * 397) ^ SourceEntityId;
                hashCode = (hashCode * 397) ^ (int)StateKind;
                hashCode = (hashCode * 397) ^ SequenceId;
                hashCode = (hashCode * 397) ^ (AttachPointId != null ? AttachPointId.GetHashCode() : 0);
                return hashCode;
            }
        }

        internal static string NormalizeAttachPointId(string attachPointId)
        {
            return string.IsNullOrWhiteSpace(attachPointId)
                ? null
                : attachPointId.Trim();
        }
    }

    internal readonly struct AttachedVfxFollowerDesiredState
    {
        public AttachedVfxFollowerDesiredState(
            GameplayVfxCueId cueId,
            int sourceEntityId,
            AttachedVfxFollowerStateKind stateKind,
            int sequenceId,
            Vector3 localPosition,
            Quaternion localRotation,
            AttachedVfxFollowerRetentionPolicy retentionPolicy = AttachedVfxFollowerRetentionPolicy.RefreshDesiredOnly,
            string attachPointId = null)
        {
            CueId = cueId;
            SourceEntityId = sourceEntityId;
            StateKind = stateKind;
            SequenceId = sequenceId;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            RetentionPolicy = retentionPolicy;
            AttachPointId = AttachedVfxFollowerKey.NormalizeAttachPointId(attachPointId);
        }

        public GameplayVfxCueId CueId { get; }

        public int SourceEntityId { get; }

        public AttachedVfxFollowerStateKind StateKind { get; }

        public int SequenceId { get; }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public AttachedVfxFollowerRetentionPolicy RetentionPolicy { get; }

        public string AttachPointId { get; }

        public AttachedVfxFollowerKey Key =>
            new(CueId, SourceEntityId, StateKind, SequenceId, AttachPointId);
    }
}
