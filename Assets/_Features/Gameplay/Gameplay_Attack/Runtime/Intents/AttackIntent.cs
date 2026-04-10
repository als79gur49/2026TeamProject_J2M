using System;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Phases;
using UnityEngine;

namespace Game.Feature.Gameplay.Attack.Intents
{
    public class AttackIntent : Intent
    {
        public AttackIntent(int sourceId, int priority, int targetId)
            : this(
                sourceId,
                priority,
                targetId,
                AttackCommandKind.Attack,
                AttackSourceKind.Combat,
                AttackInputKind.EntityIntent,
                0,
                default,
                hasTargetCell: false,
                null,
                null)
        {
        }

        internal AttackIntent(
            int sourceId,
            int priority,
            int targetId,
            AttackCommandKind commandKind,
            AttackSourceKind sourceKind,
            AttackInputKind inputKind,
            int localSequence,
            Vector2Int targetCell,
            bool hasTargetCell,
            ImpactReservation? impactReservation,
            DelayedAttackEffectRecord? delayedAttackEffect)
            : base(sourceId, priority, TickPhase.Attack)
        {
            ValidateContract(
                targetId,
                commandKind,
                sourceKind,
                inputKind,
                targetCell,
                hasTargetCell,
                impactReservation,
                delayedAttackEffect);

            TargetId = targetId;
            CommandKind = commandKind;
            SourceKind = sourceKind;
            InputKind = inputKind;
            LocalSequence = localSequence;
            TargetCell = targetCell;
            HasTargetCell = hasTargetCell;
            ImpactReservation = impactReservation;
            DelayedAttackEffect = delayedAttackEffect;
        }

        public int TargetId { get; }

        public AttackCommandKind CommandKind { get; }

        public AttackSourceKind SourceKind { get; }

        public AttackInputKind InputKind { get; }

        public int LocalSequence { get; }

        public Vector2Int TargetCell { get; }

        public bool HasTargetCell { get; }

        public ImpactReservation? ImpactReservation { get; }

        internal DelayedAttackEffectRecord? DelayedAttackEffect { get; }

        public bool IsSynthetic => InputKind == AttackInputKind.ImpactReservation || InputKind == AttackInputKind.DelayedEffect;

        protected internal override int GetTypeSortKey()
        {
            return 3;
        }

        protected internal override bool TryGetTargetCell(out Vector2Int targetCell)
        {
            targetCell = TargetCell;
            return HasTargetCell;
        }

        protected internal override int GetLocalSequence()
        {
            return LocalSequence;
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

            result = ((int)SourceKind).CompareTo((int)otherAttack.SourceKind);
            if (result != 0)
            {
                return result;
            }

            result = ((int)CommandKind).CompareTo((int)otherAttack.CommandKind);
            if (result != 0)
            {
                return result;
            }

            result = HasTargetCell.CompareTo(otherAttack.HasTargetCell);
            if (result != 0)
            {
                return result;
            }

            if (HasTargetCell && otherAttack.HasTargetCell)
            {
                result = TargetCell.x.CompareTo(otherAttack.TargetCell.x);
                if (result != 0)
                {
                    return result;
                }

                result = TargetCell.y.CompareTo(otherAttack.TargetCell.y);
                if (result != 0)
                {
                    return result;
                }
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
                AttackSourceKind.ImpactReservation,
                AttackInputKind.ImpactReservation,
                reservation.ReservationSequence,
                default,
                hasTargetCell: false,
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
                AttackSourceKind.DelayedEffect,
                AttackInputKind.DelayedEffect,
                effectRecord.EffectSequence,
                default,
                hasTargetCell: false,
                null,
                effectRecord);
        }

        public static AttackIntent FromRawIntent(RawAttackIntent rawIntent)
        {
            switch (rawIntent.CommandKind)
            {
                case AttackCommandKind.Attack:
                    return new AttackIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.TargetId,
                        AttackCommandKind.Attack,
                        rawIntent.SourceKind,
                        AttackInputKind.EntityIntent,
                        rawIntent.LocalSequence,
                        default,
                        hasTargetCell: false,
                        null,
                        null);

                case AttackCommandKind.FireProjectile:
                    return CreateFireProjectile(rawIntent.SourceId, rawIntent.Priority, rawIntent.LocalSequence);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(rawIntent),
                        rawIntent.CommandKind,
                        "Raw attack intent contained an unsupported entity command.");
            }
        }

        public static AttackIntent CreateFireProjectile(int sourceId, int priority, int localSequence = 0)
        {
            return new AttackIntent(
                sourceId,
                priority,
                0,
                AttackCommandKind.FireProjectile,
                AttackSourceKind.Combat,
                AttackInputKind.EntityIntent,
                localSequence,
                default,
                hasTargetCell: false,
                null,
                null);
        }

        private static void ValidateContract(
            int targetId,
            AttackCommandKind commandKind,
            AttackSourceKind sourceKind,
            AttackInputKind inputKind,
            Vector2Int targetCell,
            bool hasTargetCell,
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

                    if (sourceKind == AttackSourceKind.ImpactReservation ||
                        sourceKind == AttackSourceKind.DelayedEffect)
                    {
                        throw new ArgumentException("Direct attack commands require a direct attack source kind.", nameof(sourceKind));
                    }

                    if (impactReservation.HasValue)
                    {
                        throw new ArgumentException("Direct attack commands must not carry an impact reservation.", nameof(impactReservation));
                    }

                    if (delayedAttackEffect.HasValue)
                    {
                        throw new ArgumentException("Direct attack commands must not carry delayed attack effect data.", nameof(delayedAttackEffect));
                    }

                    if (hasTargetCell)
                    {
                        throw new ArgumentException("Direct attack commands must not carry a target cell.", nameof(hasTargetCell));
                    }

                    if (targetId <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(targetId), "Direct attack commands require a positive target ID.");
                    }

                    return;

                case AttackCommandKind.FireProjectile:
                    if (sourceKind != AttackSourceKind.Combat)
                    {
                        throw new ArgumentException("FireProjectile commands must use combat source kind.", nameof(sourceKind));
                    }

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

                    if (hasTargetCell)
                    {
                        throw new ArgumentException("FireProjectile commands must not carry a target cell.", nameof(hasTargetCell));
                    }

                    if (targetId != 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(targetId), "FireProjectile commands must not carry a target ID.");
                    }

                    return;

                case AttackCommandKind.ImpactReservation:
                    if (sourceKind != AttackSourceKind.ImpactReservation)
                    {
                        throw new ArgumentException("ImpactReservation commands must use impact reservation source kind.", nameof(sourceKind));
                    }

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

                    if (hasTargetCell)
                    {
                        throw new ArgumentException("ImpactReservation commands must not carry a target cell.", nameof(hasTargetCell));
                    }

                    if (targetId != impactReservation.Value.TargetId)
                    {
                        throw new ArgumentException("ImpactReservation commands must mirror the reserved target ID.", nameof(targetId));
                    }

                    return;

                case AttackCommandKind.DelayedEffect:
                    if (sourceKind != AttackSourceKind.DelayedEffect)
                    {
                        throw new ArgumentException("DelayedEffect commands must use delayed effect source kind.", nameof(sourceKind));
                    }

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

                    if (hasTargetCell)
                    {
                        throw new ArgumentException("DelayedEffect commands must not carry a target cell.", nameof(hasTargetCell));
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
