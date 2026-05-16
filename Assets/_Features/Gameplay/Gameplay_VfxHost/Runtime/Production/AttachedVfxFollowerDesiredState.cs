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
    }

    internal readonly struct AttachedVfxFollowerKey : IEquatable<AttachedVfxFollowerKey>
    {
        public AttachedVfxFollowerKey(
            GameplayVfxCueId cueId,
            int sourceEntityId,
            AttachedVfxFollowerStateKind stateKind,
            int sequenceId)
        {
            CueId = cueId;
            SourceEntityId = sourceEntityId;
            StateKind = stateKind;
            SequenceId = sequenceId;
        }

        public GameplayVfxCueId CueId { get; }

        public int SourceEntityId { get; }

        public AttachedVfxFollowerStateKind StateKind { get; }

        public int SequenceId { get; }

        public bool Equals(AttachedVfxFollowerKey other)
        {
            return CueId.Equals(other.CueId) &&
                   SourceEntityId == other.SourceEntityId &&
                   StateKind == other.StateKind &&
                   SequenceId == other.SequenceId;
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
                return hashCode;
            }
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
            Quaternion localRotation)
        {
            CueId = cueId;
            SourceEntityId = sourceEntityId;
            StateKind = stateKind;
            SequenceId = sequenceId;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
        }

        public GameplayVfxCueId CueId { get; }

        public int SourceEntityId { get; }

        public AttachedVfxFollowerStateKind StateKind { get; }

        public int SequenceId { get; }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public AttachedVfxFollowerKey Key =>
            new(CueId, SourceEntityId, StateKind, SequenceId);
    }
}
