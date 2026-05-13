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

        public int SelectedTileFeatureId { get; private set; }

        public int SelectedTileFeatureIndexHint { get; private set; } = -1;

        public string SelectedZoneId { get; private set; } = string.Empty;

        public int SelectedZoneIndexHint { get; private set; } = -1;

        public bool HasSelectedZone => !string.IsNullOrEmpty(SelectedZoneId);

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

        public void SelectTileFeatureCell(
            FaceId face,
            Vector2Int cell,
            IReadOnlyList<StageTileFeatureDefinition> tileFeatures)
        {
            SetTargetCell(face, cell);
            var index = FindTileFeatureAt(tileFeatures, face, cell.x, cell.y);
            if (index >= 0)
            {
                SelectTileFeature(index, tileFeatures);
            }
        }

        public void SelectZoneCell(
            FaceId face,
            Vector2Int cell,
            IReadOnlyList<StageZoneDefinition> zones)
        {
            SetTargetCell(face, cell);
            var indices = FindZoneIndicesAt(zones, face, cell.x, cell.y);
            if (indices.Length == 0)
            {
                ClearZoneSelection();
                return;
            }

            var selectedAtCellIndex = -1;
            for (var i = 0; i < indices.Length; i++)
            {
                if (indices[i] == SelectedZoneIndexHint)
                {
                    selectedAtCellIndex = i;
                    break;
                }
            }

            var nextIndex = selectedAtCellIndex >= 0
                ? indices[(selectedAtCellIndex + 1) % indices.Length]
                : indices[0];
            SelectZone(nextIndex, zones);
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

        public void SelectTileFeature(
            int index,
            IReadOnlyList<StageTileFeatureDefinition> tileFeatures)
        {
            if (tileFeatures == null || index < 0 || index >= tileFeatures.Count)
            {
                ClearSelectedTileFeature();
                return;
            }

            SelectedTileFeatureIndexHint = index;
            SelectedTileFeatureId = tileFeatures[index].TileId;
        }

        public void SelectTileFeatureById(
            int tileId,
            IReadOnlyList<StageTileFeatureDefinition> tileFeatures)
        {
            if (tileFeatures == null || tileId <= 0)
            {
                ClearSelectedTileFeature();
                return;
            }

            for (var i = 0; i < tileFeatures.Count; i++)
            {
                if (tileFeatures[i].TileId == tileId)
                {
                    SelectTileFeature(i, tileFeatures);
                    return;
                }
            }

            ClearSelectedTileFeature();
        }

        public void ClearSelectedTileFeature()
        {
            SelectedTileFeatureId = 0;
            SelectedTileFeatureIndexHint = -1;
        }

        public void SelectZone(
            int index,
            IReadOnlyList<StageZoneDefinition> zones)
        {
            if (zones == null || index < 0 || index >= zones.Count)
            {
                ClearZoneSelection();
                return;
            }

            SelectedZoneIndexHint = index;
            SelectedZoneId = NormalizeZoneId(zones[index].ZoneId);
        }

        public void SelectZoneById(
            string zoneId,
            IReadOnlyList<StageZoneDefinition> zones)
        {
            var normalizedZoneId = NormalizeZoneId(zoneId);
            if (zones == null || string.IsNullOrEmpty(normalizedZoneId))
            {
                ClearZoneSelection();
                return;
            }

            for (var i = 0; i < zones.Count; i++)
            {
                if (NormalizeZoneId(zones[i].ZoneId) == normalizedZoneId)
                {
                    SelectZone(i, zones);
                    return;
                }
            }

            ClearZoneSelection();
        }

        public void ClearZoneSelection()
        {
            SelectedZoneId = string.Empty;
            SelectedZoneIndexHint = -1;
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

        public int ResolveSelectedTileFeatureIndex(IReadOnlyList<StageTileFeatureDefinition> tileFeatures)
        {
            if (tileFeatures == null || tileFeatures.Count == 0)
            {
                ClearSelectedTileFeature();
                return -1;
            }

            if (SelectedTileFeatureId > 0)
            {
                for (var i = 0; i < tileFeatures.Count; i++)
                {
                    if (tileFeatures[i].TileId == SelectedTileFeatureId)
                    {
                        SelectedTileFeatureIndexHint = i;
                        return i;
                    }
                }
            }

            if (SelectedTileFeatureIndexHint >= 0 && SelectedTileFeatureIndexHint < tileFeatures.Count)
            {
                SelectedTileFeatureId = tileFeatures[SelectedTileFeatureIndexHint].TileId;
                return SelectedTileFeatureIndexHint;
            }

            ClearSelectedTileFeature();
            return -1;
        }

        public int ResolveSelectedZoneIndex(IReadOnlyList<StageZoneDefinition> zones)
        {
            if (zones == null || zones.Count == 0)
            {
                ClearZoneSelection();
                return -1;
            }

            if (!string.IsNullOrEmpty(SelectedZoneId))
            {
                for (var i = 0; i < zones.Count; i++)
                {
                    if (NormalizeZoneId(zones[i].ZoneId) == SelectedZoneId)
                    {
                        SelectedZoneIndexHint = i;
                        return i;
                    }
                }
            }

            if (SelectedZoneIndexHint >= 0 && SelectedZoneIndexHint < zones.Count)
            {
                SelectedZoneId = NormalizeZoneId(zones[SelectedZoneIndexHint].ZoneId);
                return SelectedZoneIndexHint;
            }

            ClearZoneSelection();
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

        public int FindTileFeatureAt(
            IReadOnlyList<StageTileFeatureDefinition> tileFeatures,
            FaceId face,
            int x,
            int y)
        {
            if (tileFeatures == null)
            {
                return -1;
            }

            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var feature = tileFeatures[i];
                if (feature.Cell.face == face &&
                    feature.Cell.x == x &&
                    feature.Cell.y == y)
                {
                    return i;
                }
            }

            return -1;
        }

        public int CountTileFeaturesAt(
            IReadOnlyList<StageTileFeatureDefinition> tileFeatures,
            FaceId face,
            int x,
            int y)
        {
            if (tileFeatures == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var feature = tileFeatures[i];
                if (feature.Cell.face == face &&
                    feature.Cell.x == x &&
                    feature.Cell.y == y)
                {
                    count++;
                }
            }

            return count;
        }

        public int FindZoneAt(
            IReadOnlyList<StageZoneDefinition> zones,
            FaceId face,
            int x,
            int y)
        {
            var indices = FindZoneIndicesAt(zones, face, x, y);
            return indices.Length > 0 ? indices[0] : -1;
        }

        public int CountZonesAt(
            IReadOnlyList<StageZoneDefinition> zones,
            FaceId face,
            int x,
            int y)
        {
            return FindZoneIndicesAt(zones, face, x, y).Length;
        }

        public int[] FindZoneIndicesAt(
            IReadOnlyList<StageZoneDefinition> zones,
            FaceId face,
            int x,
            int y)
        {
            if (zones == null)
            {
                return System.Array.Empty<int>();
            }

            var indices = new List<int>();
            var cell = new Vector2Int(x, y);
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone.FaceId != face)
                {
                    continue;
                }

                var regions = zone.GetRegionsOrEmpty();
                for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                {
                    var region = regions[regionIndex];
                    if (cell.x >= region.MinInclusive.x &&
                        cell.x <= region.MaxInclusive.x &&
                        cell.y >= region.MinInclusive.y &&
                        cell.y <= region.MaxInclusive.y)
                    {
                        indices.Add(i);
                        break;
                    }
                }
            }

            return indices.ToArray();
        }

        public int[] FindTileFeatureIndicesAt(
            IReadOnlyList<StageTileFeatureDefinition> tileFeatures,
            FaceId face,
            int x,
            int y)
        {
            if (tileFeatures == null)
            {
                return System.Array.Empty<int>();
            }

            var indices = new List<int>();
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var feature = tileFeatures[i];
                if (feature.Cell.face == face &&
                    feature.Cell.x == x &&
                    feature.Cell.y == y)
                {
                    indices.Add(i);
                }
            }

            return indices.ToArray();
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

        private static string NormalizeZoneId(string zoneId)
        {
            return zoneId?.Trim() ?? string.Empty;
        }
    }
}
