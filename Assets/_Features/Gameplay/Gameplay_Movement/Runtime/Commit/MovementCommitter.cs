using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement.Intents;
using UnityEngine;

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
                        $"ImpactReservationCreated|G={group.GroupId}|I={group.IntentId}|Source={reservation.SourceId}|Target={reservation.TargetId}|At={FormatCell(reservation.Position)}|Damage={reservation.Damage}|Sequence={reservation.ReservationSequence}");
                    reservationSequence++;
                    continue;
                }

                for (var presenceIndex = 0; presenceIndex < group.BoardPresenceChanges.Count; presenceIndex++)
                {
                    var boardPresenceChange = group.BoardPresenceChanges[presenceIndex];
                    writeContext.SetBoardPresence(boardPresenceChange.EntityId, boardPresenceChange.BoardPresence);
                    commitEvents.Add(
                        $"BoardPresenceCommitted|G={group.GroupId}|I={group.IntentId}|E={boardPresenceChange.EntityId}|Presence={boardPresenceChange.BoardPresence}");
                }

                for (var topologyIndex = 0; topologyIndex < group.TopologyChanges.Count; topologyIndex++)
                {
                    var topologyChange = group.TopologyChanges[topologyIndex];
                    writeContext.SetTopology(topologyChange.UpdatedTopology);
                    commitEvents.Add(
                        $"TopologyCommitted|G={group.GroupId}|I={group.IntentId}|Rotation={topologyChange.RotationKind}|Bottom={topologyChange.UpdatedTopology.BottomFace}|Front={topologyChange.UpdatedTopology.FrontFace}");
                }

                if (group.GroupKind == ActionGroupKind.Flip)
                {
                    var interactionSourceFacing = ResolveInteractionSourceFacing(snapshot, sortedIntents, group);
                    writeContext.SetFacing(group.SourceId, interactionSourceFacing);
                    commitEvents.Add(
                        $"FacingCommitted|G={group.GroupId}|I={group.IntentId}|E={group.SourceId}|Facing={interactionSourceFacing}");
                }

                for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                {
                    var move = group.Moves[moveIndex];
                    writeContext.MoveEntity(move.EntityId, move.DestinationCell);
                    writeContext.SetFacing(move.EntityId, move.Facing);
                    commitEvents.Add(
                        $"MoveCommitted|G={group.GroupId}|I={group.IntentId}|E={move.EntityId}|To={FormatCell(move.DestinationCell)}|Facing={move.Facing}");
                }

                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    writeContext.MarkDestroy(destroy.TargetId);
                    commitEvents.Add(
                        $"DestroyMarked|G={group.GroupId}|I={group.IntentId}|Target={destroy.TargetId}|Condition={destroy.Condition}");
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

            var destination = ResolveIntentTargetCell(source.position, intent.Destination);
            if (!snapshot.TryGetUnitAt(destination, out var target))
            {
                throw new InvalidOperationException(
                    $"Projectile impact group requires a blocking target at the destination. Source={group.SourceId}, Intent={group.IntentId}, Destination={destination}");
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

        private static Direction ResolveInteractionSourceFacing(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            ActionGroup group)
        {
            if (!snapshot.TryGetEntity(group.SourceId, out var source))
            {
                throw new InvalidOperationException(
                    $"Interaction group references a missing source entity. Source={group.SourceId}, Intent={group.IntentId}");
            }

            var intent = FindIntent(sortedIntents, group.IntentId);
            if (intent == null)
            {
                throw new InvalidOperationException(
                    $"Interaction group is missing its movement intent. Source={group.SourceId}, Intent={group.IntentId}");
            }

            var delta = intent.Destination - source.position;
            if (delta.x == 0 && delta.y == 1)
            {
                return Direction.Up;
            }

            if (delta.x == 1 && delta.y == 0)
            {
                return Direction.Right;
            }

            if (delta.x == 0 && delta.y == -1)
            {
                return Direction.Down;
            }

            if (delta.x == -1 && delta.y == 0)
            {
                return Direction.Left;
            }

            throw new InvalidOperationException(
                $"Interaction group requires an orthogonal adjacent interaction direction. Source={group.SourceId}, Intent={group.IntentId}");
        }

        private static SurfaceCell ResolveIntentTargetCell(SurfaceCell source, Vector2Int destination)
        {
            var delta = destination - source.PlanarPosition;
            return source + delta;
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
        }
    }
}
