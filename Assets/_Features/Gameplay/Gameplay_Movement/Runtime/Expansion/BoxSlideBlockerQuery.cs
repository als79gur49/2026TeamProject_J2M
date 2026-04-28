using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Expansion
{
    internal enum BoxSlideMovementKind
    {
        PushStart = 0,
        SlidingContinuation = 1,
    }

    internal enum BoxSlideBlockerReason
    {
        None = 0,
        FrontFaceShield = 1,
    }

    internal readonly struct BoxSlideBlockerResult
    {
        public BoxSlideBlockerResult(
            BoxSlideBlockerReason reason,
            int blockerEntityId,
            SurfaceCell blockerSourceCell)
        {
            Reason = reason;
            BlockerEntityId = blockerEntityId;
            BlockerSourceCell = blockerSourceCell;
        }

        public BoxSlideBlockerReason Reason { get; }

        public int BlockerEntityId { get; }

        public SurfaceCell BlockerSourceCell { get; }
    }

    internal static class BoxSlideBlockerQuery
    {
        public static bool TryResolveBlocker(
            WorldSnapshot snapshot,
            IReadOnlyList<FrontFaceSupportContributor> contributors,
            int tickIndex,
            in EntityState movingBox,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            BoxSlideMovementKind movementKind,
            out BoxSlideBlockerResult result)
        {
            _ = tickIndex;
            _ = movementKind;

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (movingBox.type != EntityType.Box ||
                contributors == null ||
                contributors.Count == 0 ||
                destinationCell.face != snapshot.Topology.FrontFace)
            {
                result = default;
                return false;
            }

            for (var i = 0; i < contributors.Count; i++)
            {
                var contributor = contributors[i];
                if (!CanBlockDestination(contributor, sourceCell, destinationCell))
                {
                    continue;
                }

                result = new BoxSlideBlockerResult(
                    BoxSlideBlockerReason.FrontFaceShield,
                    contributor.SourceEntityId,
                    contributor.SourceCell);
                return true;
            }

            result = default;
            return false;
        }

        private static bool CanBlockDestination(
            in FrontFaceSupportContributor contributor,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
        {
            if (destinationCell.face != contributor.SourceCell.face ||
                sourceCell == destinationCell)
            {
                return false;
            }

            return contributor.EffectRuntime.Kind switch
            {
                EnemyFrontFaceSupportEffectKind.BoxSlideShield => IsShieldedCell(
                    contributor.SourceCell,
                    destinationCell,
                    contributor.EffectRuntime.BoxSlideShield),
                _ => false,
            };
        }

        private static bool IsShieldedCell(
            SurfaceCell shieldSourceCell,
            SurfaceCell candidateCell,
            in BoxSlideShieldRuntime shieldRuntime)
        {
            var offset = candidateCell - shieldSourceCell;
            if (!shieldRuntime.IncludeSourceCell && offset == Vector2Int.zero)
            {
                return false;
            }

            return shieldRuntime.TargetPattern switch
            {
                FrontFaceShieldTargetPattern.OrthogonalAdjacent4 => Mathf.Abs(offset.x) + Mathf.Abs(offset.y) == 1 ||
                                                                   (shieldRuntime.IncludeSourceCell && offset == Vector2Int.zero),
                FrontFaceShieldTargetPattern.ManhattanRadius => IsWithinManhattanRadius(offset, shieldRuntime.Radius),
                FrontFaceShieldTargetPattern.SquareRadius => IsWithinSquareRadius(offset, shieldRuntime.Radius),
                _ => false,
            };
        }

        private static bool IsWithinManhattanRadius(Vector2Int offset, int radius)
        {
            return Mathf.Abs(offset.x) + Mathf.Abs(offset.y) <= radius;
        }

        private static bool IsWithinSquareRadius(Vector2Int offset, int radius)
        {
            return Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) <= radius;
        }
    }
}
