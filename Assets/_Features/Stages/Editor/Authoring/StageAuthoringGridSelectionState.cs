using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal sealed class StageAuthoringGridSelectionState
    {
        public FaceId TargetFace { get; private set; } = FaceId.Floor;

        public Vector2Int TargetCell { get; private set; }

        public string SelectedStableGuid { get; private set; } = string.Empty;

        public int SelectedIndexHint { get; private set; } = -1;

        public void SetTargetFace(FaceId face)
        {
            TargetFace = face;
        }

        public void SetTargetCell(FaceId face, Vector2Int cell)
        {
            TargetFace = face;
            TargetCell = cell;
        }

        public void SelectCell(
            FaceId face,
            Vector2Int cell,
            IReadOnlyList<StagePlacedEntityAuthoring> placements)
        {
            SetTargetCell(face, cell);
            var occupiedIndex = FindPlacementAt(placements, face, cell.x, cell.y);
            if (occupiedIndex >= 0)
            {
                SelectPlacement(occupiedIndex, placements);
            }
        }

        public void SelectPlacement(
            int index,
            IReadOnlyList<StagePlacedEntityAuthoring> placements)
        {
            if (placements == null || index < 0 || index >= placements.Count)
            {
                ClearSelectedPlacement();
                return;
            }

            SelectedIndexHint = index;
            SelectedStableGuid = NormalizeStableGuid(placements[index]?.StableGuid);
        }

        public void ClearSelectedPlacement()
        {
            SelectedStableGuid = string.Empty;
            SelectedIndexHint = -1;
        }

        public int ResolveSelectedPlacementIndex(IReadOnlyList<StagePlacedEntityAuthoring> placements)
        {
            if (placements == null || placements.Count == 0)
            {
                return -1;
            }

            if (!string.IsNullOrEmpty(SelectedStableGuid))
            {
                for (var i = 0; i < placements.Count; i++)
                {
                    if (NormalizeStableGuid(placements[i]?.StableGuid) == SelectedStableGuid)
                    {
                        SelectedIndexHint = i;
                        return i;
                    }
                }
            }

            if (SelectedIndexHint >= 0 && SelectedIndexHint < placements.Count)
            {
                return SelectedIndexHint;
            }

            ClearSelectedPlacement();
            return -1;
        }

        public int FindPlacementAt(
            IReadOnlyList<StagePlacedEntityAuthoring> placements,
            FaceId face,
            int x,
            int y,
            int ignoredIndex = -1)
        {
            if (placements == null)
            {
                return -1;
            }

            for (var i = 0; i < placements.Count; i++)
            {
                if (i == ignoredIndex)
                {
                    continue;
                }

                var placement = placements[i];
                if (StageAuthoringGridRenderer.IsOnFace(placement, face) &&
                    placement.Cell.x == x &&
                    placement.Cell.y == y)
                {
                    return i;
                }
            }

            return -1;
        }

        public int CountPlacementsAt(
            IReadOnlyList<StagePlacedEntityAuthoring> placements,
            FaceId face,
            int x,
            int y)
        {
            if (placements == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (StageAuthoringGridRenderer.IsOnFace(placement, face) &&
                    placement.Cell.x == x &&
                    placement.Cell.y == y)
                {
                    count++;
                }
            }

            return count;
        }

        public bool IsSelectedPlacementAtTarget(
            IReadOnlyList<StagePlacedEntityAuthoring> placements,
            int selectedIndex)
        {
            if (placements == null || selectedIndex < 0 || selectedIndex >= placements.Count)
            {
                return false;
            }

            var selected = placements[selectedIndex];
            return selected != null &&
                   selected.Cell.face == TargetFace &&
                   selected.Cell.x == TargetCell.x &&
                   selected.Cell.y == TargetCell.y;
        }

        public bool IsTargetOccupiedByOther(
            IReadOnlyList<StagePlacedEntityAuthoring> placements,
            int selectedIndex)
        {
            return FindPlacementAt(
                placements,
                TargetFace,
                TargetCell.x,
                TargetCell.y,
                selectedIndex) >= 0;
        }

        private static string NormalizeStableGuid(string stableGuid)
        {
            return StageAuthoringGenerator.Normalize(stableGuid);
        }
    }
}
