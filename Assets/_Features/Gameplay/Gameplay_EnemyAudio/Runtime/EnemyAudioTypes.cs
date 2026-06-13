using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.EnemyAudio
{
    public enum EnemyAudioCue
    {
        None = 0,
        Move = 1,
        Death = 2,
        Windup = 3,
        Landing = 4,
        Active = 5,
        Recover = 6,
        ForwardCellImpact = 7,
        ChargeActiveLoop = 8,
        StationaryActive = 9,
        PassiveContact = 10,
    }

    public readonly struct EnemyAudioRequest
    {
        public EnemyAudioRequest(
            int ownerEntityId,
            EnemyAudioCue cue,
            in AudioPlaybackContext context,
            float delaySeconds = 0f,
            EnemyAudioRequestIdentity identity = default)
        {
            OwnerEntityId = ownerEntityId;
            Cue = cue;
            Context = context;
            DelaySeconds = Math.Max(0f, delaySeconds);
            Identity = identity;
        }

        public int OwnerEntityId { get; }

        public EnemyAudioCue Cue { get; }

        public AudioPlaybackContext Context { get; }

        public float DelaySeconds { get; }

        public EnemyAudioRequestIdentity Identity { get; }
    }

    public readonly struct EnemyAudioRequestIdentity : IEquatable<EnemyAudioRequestIdentity>
    {
        public EnemyAudioRequestIdentity(
            bool isValid,
            int sourceEntityId,
            SurfaceCell targetCell,
            int impactTick,
            int impactId,
            int presentationKey)
        {
            IsValid = isValid;
            SourceEntityId = sourceEntityId;
            TargetCell = targetCell;
            ImpactTick = impactTick;
            ImpactId = impactId;
            PresentationKey = presentationKey;
        }

        public bool IsValid { get; }

        public int SourceEntityId { get; }

        public SurfaceCell TargetCell { get; }

        public int ImpactTick { get; }

        public int ImpactId { get; }

        public int PresentationKey { get; }

        public bool Equals(EnemyAudioRequestIdentity other)
        {
            return IsValid == other.IsValid &&
                   SourceEntityId == other.SourceEntityId &&
                   TargetCell.Equals(other.TargetCell) &&
                   ImpactTick == other.ImpactTick &&
                   ImpactId == other.ImpactId &&
                   PresentationKey == other.PresentationKey;
        }

        public override bool Equals(object obj)
        {
            return obj is EnemyAudioRequestIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = IsValid ? 1 : 0;
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ TargetCell.GetHashCode();
                hash = (hash * 397) ^ ImpactTick;
                hash = (hash * 397) ^ ImpactId;
                hash = (hash * 397) ^ PresentationKey;
                return hash;
            }
        }
    }

    public static class EnemyAudioCueCatalog
    {
        private static readonly EnemyAudioCue[] RuntimeCueValues =
        {
            EnemyAudioCue.Move,
            EnemyAudioCue.Death,
            EnemyAudioCue.Windup,
            EnemyAudioCue.Landing,
            EnemyAudioCue.Active,
            EnemyAudioCue.Recover,
            EnemyAudioCue.ForwardCellImpact,
            EnemyAudioCue.ChargeActiveLoop,
            EnemyAudioCue.StationaryActive,
            EnemyAudioCue.PassiveContact,
        };

        public static IReadOnlyList<EnemyAudioCue> RuntimeCues => RuntimeCueValues;

        public static string Format(EnemyAudioCue cue)
        {
            return cue switch
            {
                EnemyAudioCue.None => nameof(EnemyAudioCue.None),
                EnemyAudioCue.Move => nameof(EnemyAudioCue.Move),
                EnemyAudioCue.Death => nameof(EnemyAudioCue.Death),
                EnemyAudioCue.Windup => nameof(EnemyAudioCue.Windup),
                EnemyAudioCue.Landing => nameof(EnemyAudioCue.Landing),
                EnemyAudioCue.Active => nameof(EnemyAudioCue.Active),
                EnemyAudioCue.Recover => nameof(EnemyAudioCue.Recover),
                EnemyAudioCue.ForwardCellImpact => nameof(EnemyAudioCue.ForwardCellImpact),
                EnemyAudioCue.ChargeActiveLoop => nameof(EnemyAudioCue.ChargeActiveLoop),
                EnemyAudioCue.StationaryActive => nameof(EnemyAudioCue.StationaryActive),
                EnemyAudioCue.PassiveContact => nameof(EnemyAudioCue.PassiveContact),
                _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unsupported enemy audio cue."),
            };
        }
    }
}
