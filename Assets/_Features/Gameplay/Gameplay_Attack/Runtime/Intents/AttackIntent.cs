using System;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Phases;

namespace Game.Feature.Gameplay.Attack.Intents
{
    public sealed class AttackIntent : Intent
    {
        public AttackIntent(int sourceId, int priority, int targetId)
            : this(
                sourceId,
                priority,
                targetId,
                AttackCommandKind.Attack,
                AttackInputKind.EntityIntent,
                0,
                null,
                null)
        {
        }

        private AttackIntent(
            int sourceId,
            int priority,
            int targetId,
            AttackCommandKind commandKind,
            AttackInputKind inputKind,
            int localSequence,
            ImpactReservation? impactReservation,
            DelayedAttackEffectRecord? delayedAttackEffect)
            : base(sourceId, priority, TickPhase.Attack)
        {
            ValidateContract(targetId, commandKind, inputKind, impactReservation, delayedAttackEffect);

            TargetId = targetId;
            CommandKind = commandKind;
            InputKind = inputKind;
            LocalSequence = localSequence;
            ImpactReservation = impactReservation;
            DelayedAttackEffect = delayedAttackEffect;
        }

        public int TargetId { get; }

        public AttackCommandKind CommandKind { get; }

        public AttackInputKind InputKind { get; }

        public int LocalSequence { get; }

        public ImpactReservation? ImpactReservation { get; }

        internal DelayedAttackEffectRecord? DelayedAttackEffect { get; }

        public bool IsSynthetic => InputKind != AttackInputKind.EntityIntent;

        protected internal override int GetTypeSortKey()
        {
            return 0;
        }

        protected internal override int CompareSameType(Intent other)
        {
            var otherAttack = (AttackIntent)other;

            var result = ((int)InputKind).CompareTo((int)otherAttack.InputKind);
            if (result != 0)
            {
                return result;
            }

            result = LocalSequence.CompareTo(otherAttack.LocalSequence);
            if (result != 0)
            {
                return result;
            }

            result = ((int)CommandKind).CompareTo((int)otherAttack.CommandKind);
            if (result != 0)
            {
                return result;
            }

            result = TargetId.CompareTo(otherAttack.TargetId);
            if (result != 0)
            {
                return result;
            }

            if (ImpactReservation.HasValue && otherAttack.ImpactReservation.HasValue)
            {
                return ImpactReservationComparer.Instance.Compare(
                    ImpactReservation.Value,
                    otherAttack.ImpactReservation.Value);
            }

            if (DelayedAttackEffect.HasValue && otherAttack.DelayedAttackEffect.HasValue)
            {
                return DelayedAttackEffectRecordComparer.Instance.Compare(
                    DelayedAttackEffect.Value,
                    otherAttack.DelayedAttackEffect.Value);
            }

            return 0;
        }

        public static AttackIntent FromImpactReservation(ImpactReservation reservation)
        {
            return new AttackIntent(
                reservation.SourceId,
                0,
                reservation.TargetId,
                AttackCommandKind.ImpactReservation,
                AttackInputKind.ImpactReservation,
                reservation.ReservationSequence,
                reservation,
                null);
        }

        internal static AttackIntent FromDelayedAttackEffect(DelayedAttackEffectRecord effectRecord)
        {
            return new AttackIntent(
                effectRecord.SourceId,
                effectRecord.Priority,
                effectRecord.TargetId,
                AttackCommandKind.DelayedEffect,
                AttackInputKind.DelayedEffect,
                effectRecord.EffectSequence,
                null,
                effectRecord);
        }

        public static AttackIntent FromRawIntent(RawAttackIntent rawIntent)
        {
            switch (rawIntent.CommandKind)
            {
                case AttackCommandKind.Attack:
                    return new AttackIntent(rawIntent.SourceId, rawIntent.Priority, rawIntent.TargetId);

                case AttackCommandKind.FireProjectile:
                    return CreateFireProjectile(rawIntent.SourceId, rawIntent.Priority);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(rawIntent),
                        rawIntent.CommandKind,
                        "Raw attack intent contained an unsupported entity command.");
            }
        }

        public static AttackIntent CreateFireProjectile(int sourceId, int priority)
        {
            return new AttackIntent(
                sourceId,
                priority,
                0,
                AttackCommandKind.FireProjectile,
                AttackInputKind.EntityIntent,
                0,
                null,
                null);
        }

        private static void ValidateContract(
            int targetId,
            AttackCommandKind commandKind,
            AttackInputKind inputKind,
            ImpactReservation? impactReservation,
            DelayedAttackEffectRecord? delayedAttackEffect)
        {
            switch (commandKind)
            {
                case AttackCommandKind.Attack:
                    if (inputKind != AttackInputKind.EntityIntent)
                    {
                        throw new ArgumentException("Direct attack commands must be entity-generated inputs.", nameof(inputKind));
                    }

                    if (impactReservation.HasValue)
                    {
                        throw new ArgumentException("Direct attack commands must not carry an impact reservation.", nameof(impactReservation));
                    }

                    if (delayedAttackEffect.HasValue)
                    {
                        throw new ArgumentException("Direct attack commands must not carry delayed attack effect data.", nameof(delayedAttackEffect));
                    }

                    if (targetId <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(targetId), "Direct attack commands require a positive target ID.");
                    }

                    return;

                case AttackCommandKind.FireProjectile:
                    if (inputKind != AttackInputKind.EntityIntent)
                    {
                        throw new ArgumentException("FireProjectile commands must be entity-generated inputs.", nameof(inputKind));
                    }

                    if (impactReservation.HasValue)
                    {
                        throw new ArgumentException("FireProjectile commands must not carry an impact reservation.", nameof(impactReservation));
                    }

                    if (delayedAttackEffect.HasValue)
                    {
                        throw new ArgumentException("FireProjectile commands must not carry delayed attack effect data.", nameof(delayedAttackEffect));
                    }

                    if (targetId != 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(targetId), "FireProjectile commands must not carry a target ID.");
                    }

                    return;

                case AttackCommandKind.ImpactReservation:
                    if (inputKind != AttackInputKind.ImpactReservation)
                    {
                        throw new ArgumentException("ImpactReservation commands must use the synthetic input kind.", nameof(inputKind));
                    }

                    if (!impactReservation.HasValue)
                    {
                        throw new ArgumentNullException(nameof(impactReservation), "ImpactReservation commands require reservation data.");
                    }

                    if (delayedAttackEffect.HasValue)
                    {
                        throw new ArgumentException("ImpactReservation commands must not carry delayed attack effect data.", nameof(delayedAttackEffect));
                    }

                    if (targetId != impactReservation.Value.TargetId)
                    {
                        throw new ArgumentException("ImpactReservation commands must mirror the reserved target ID.", nameof(targetId));
                    }

                    return;

                case AttackCommandKind.DelayedEffect:
                    if (inputKind != AttackInputKind.DelayedEffect)
                    {
                        throw new ArgumentException("DelayedEffect commands must use the delayed synthetic input kind.", nameof(inputKind));
                    }

                    if (impactReservation.HasValue)
                    {
                        throw new ArgumentException("DelayedEffect commands must not carry an impact reservation.", nameof(impactReservation));
                    }

                    if (!delayedAttackEffect.HasValue)
                    {
                        throw new ArgumentNullException(nameof(delayedAttackEffect), "DelayedEffect commands require delayed attack effect data.");
                    }

                    if (targetId != delayedAttackEffect.Value.TargetId)
                    {
                        throw new ArgumentException("DelayedEffect commands must mirror the delayed target ID.", nameof(targetId));
                    }

                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(commandKind), commandKind, "Unsupported attack command kind.");
            }
        }
    }
}
