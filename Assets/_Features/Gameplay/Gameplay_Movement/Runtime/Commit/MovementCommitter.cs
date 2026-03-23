using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement.Intents;

namespace Game.Feature.Gameplay.Movement.Commit
{
    internal sealed class MovementCommitter
    {
        private const int ProjectileImpactDamageAmount = 1;

        public void Commit(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            int tickIndex,
            IWorldWriteContext writeContext,
            PhaseTransientBuffer transientBuffer,
            IReadOnlyList<ActionGroup> selectedGroups,
            List<string> commitEvents)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (transientBuffer == null)
            {
                throw new ArgumentNullException(nameof(transientBuffer));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            commitEvents.Clear();
            var reservationSequence = 1;

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                if (group.GroupKind == ActionGroupKind.ProjectileImpact)
                {
                    var reservation = CreateProjectileImpactReservation(snapshot, sortedIntents, tickIndex, group, reservationSequence);
                    transientBuffer.AddImpact(reservation);
                    commitEvents.Add(
                        $"ImpactReservationCreated|G={group.GroupId}|I={group.IntentId}|Source={reservation.SourceId}|Target={reservation.TargetId}|At=({reservation.Position.x},{reservation.Position.y})|Damage={reservation.Damage}|Sequence={reservation.ReservationSequence}");
                    reservationSequence++;
                    continue;
                }

                for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                {
                    var move = group.Moves[moveIndex];
                    writeContext.SetFacing(move.EntityId, move.Facing);
                    writeContext.MoveEntity(move.EntityId, move.Destination);
                    commitEvents.Add(
                        $"MoveCommitted|G={group.GroupId}|I={group.IntentId}|E={move.EntityId}|To=({move.Destination.x},{move.Destination.y})|Facing={move.Facing}");
                }
            }
        }

        private static ImpactReservation CreateProjectileImpactReservation(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            int tickIndex,
            ActionGroup group,
            int reservationSequence)
        {
            if (!snapshot.TryGetEntity(group.SourceId, out var source))
            {
                throw new InvalidOperationException(
                    $"Projectile impact group references a missing source entity. Source={group.SourceId}, Intent={group.IntentId}");
            }

            if (source.type != EntityType.Projectile)
            {
                throw new InvalidOperationException(
                    $"Projectile impact group must reference a projectile source. Source={group.SourceId}, Type={source.type}, Intent={group.IntentId}");
            }

            var intent = FindIntent(sortedIntents, group.IntentId);
            if (intent == null)
            {
                throw new InvalidOperationException(
                    $"Projectile impact group is missing its movement intent. Source={group.SourceId}, Intent={group.IntentId}");
            }

            if (!snapshot.TryGetUnitAt(intent.Destination, out var target))
            {
                throw new InvalidOperationException(
                    $"Projectile impact group requires a blocking target at the destination. Source={group.SourceId}, Intent={group.IntentId}, Destination=({intent.Destination.x},{intent.Destination.y})");
            }

            return new ImpactReservation(
                source.entityId,
                target.entityId,
                target.position,
                ProjectileImpactDamageAmount,
                tickIndex,
                group.GroupId,
                reservationSequence);
        }

        private static MoveIntent FindIntent(IReadOnlyList<MoveIntent> sortedIntents, int intentId)
        {
            for (var i = 0; i < sortedIntents.Count; i++)
            {
                if (sortedIntents[i].IntentId == intentId)
                {
                    return sortedIntents[i];
                }
            }

            return null;
        }
    }
}
