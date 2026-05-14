using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal readonly struct StageAuthoringZoneCommandResult
    {
        private StageAuthoringZoneCommandResult(
            bool succeeded,
            string message,
            MessageType messageType,
            int zoneIndex)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            MessageType = messageType;
            ZoneIndex = zoneIndex;
        }

        public bool Succeeded { get; }

        public string Message { get; }

        public MessageType MessageType { get; }

        public int ZoneIndex { get; }

        public static StageAuthoringZoneCommandResult Success(int zoneIndex, string message = null)
        {
            return new StageAuthoringZoneCommandResult(
                true,
                message,
                MessageType.Info,
                zoneIndex);
        }

        public static StageAuthoringZoneCommandResult Failure(string message)
        {
            return new StageAuthoringZoneCommandResult(
                false,
                message,
                MessageType.Error,
                -1);
        }
    }

    internal static class StageAuthoringZoneCommands
    {
        public static StageAuthoringZoneCommandResult TryAddZone(
            StageAuthoringDefinition definition,
            string zoneId,
            FaceId face,
            IReadOnlyList<StageZoneRegionDefinition> regions)
        {
            var validation = ValidateZoneForAuthoring(definition, null, zoneId, face, regions);
            if (!validation.Succeeded)
            {
                return validation;
            }

            var next = new List<StageZoneDefinition>(definition.Zones)
            {
                CreateZone(zoneId, face, regions),
            };
            var addedIndex = next.Count - 1;
            Undo.RecordObject(definition, "Add Stage Zone");
            definition.SetZones(next);
            EditorUtility.SetDirty(definition);
            return StageAuthoringZoneCommandResult.Success(addedIndex, $"Added Zone '{NormalizeZoneId(zoneId)}'.");
        }

        public static StageAuthoringZoneCommandResult TryUpdateZone(
            StageAuthoringDefinition definition,
            int zoneIndex,
            string zoneId,
            FaceId face,
            IReadOnlyList<StageZoneRegionDefinition> regions)
        {
            if (definition == null)
            {
                return StageAuthoringZoneCommandResult.Failure("StageAuthoringDefinition is missing.");
            }

            if (zoneIndex < 0 || zoneIndex >= definition.Zones.Count)
            {
                return StageAuthoringZoneCommandResult.Failure("No Zone selected.");
            }

            var validation = ValidateZoneForAuthoring(definition, zoneIndex, zoneId, face, regions);
            if (!validation.Succeeded)
            {
                return validation;
            }

            var next = new List<StageZoneDefinition>(definition.Zones);
            next[zoneIndex] = CreateZone(zoneId, face, regions);
            Undo.RecordObject(definition, "Update Stage Zone");
            definition.SetZones(next);
            EditorUtility.SetDirty(definition);
            return StageAuthoringZoneCommandResult.Success(zoneIndex, $"Updated Zone '{NormalizeZoneId(zoneId)}'.");
        }

        public static StageAuthoringZoneCommandResult TryRemoveZone(
            StageAuthoringDefinition definition,
            int zoneIndex)
        {
            if (definition == null)
            {
                return StageAuthoringZoneCommandResult.Failure("StageAuthoringDefinition is missing.");
            }

            if (zoneIndex < 0 || zoneIndex >= definition.Zones.Count)
            {
                return StageAuthoringZoneCommandResult.Failure("No Zone selected.");
            }

            var zoneId = definition.Zones[zoneIndex].ZoneId;
            var next = new List<StageZoneDefinition>(definition.Zones);
            next.RemoveAt(zoneIndex);
            Undo.RecordObject(definition, "Remove Stage Zone");
            definition.SetZones(next);
            EditorUtility.SetDirty(definition);
            return StageAuthoringZoneCommandResult.Success(-1, $"Removed Zone '{NormalizeZoneId(zoneId)}'.");
        }

        public static StageAuthoringZoneCommandResult TryDuplicateZone(
            StageAuthoringDefinition definition,
            int sourceZoneIndex,
            string newZoneId)
        {
            if (definition == null)
            {
                return StageAuthoringZoneCommandResult.Failure("StageAuthoringDefinition is missing.");
            }

            if (sourceZoneIndex < 0 || sourceZoneIndex >= definition.Zones.Count)
            {
                return StageAuthoringZoneCommandResult.Failure("No Zone selected.");
            }

            var source = definition.Zones[sourceZoneIndex];
            return TryAddZone(definition, newZoneId, source.FaceId, CloneRegions(source.GetRegionsOrEmpty()));
        }

        public static StageAuthoringZoneCommandResult ValidateZoneForAuthoring(
            StageAuthoringDefinition definition,
            int? existingZoneIndex,
            string zoneId,
            FaceId face,
            IReadOnlyList<StageZoneRegionDefinition> regions)
        {
            if (definition == null)
            {
                return StageAuthoringZoneCommandResult.Failure("StageAuthoringDefinition is missing.");
            }

            var normalizedZoneId = NormalizeZoneId(zoneId);
            if (string.IsNullOrEmpty(normalizedZoneId))
            {
                return StageAuthoringZoneCommandResult.Failure("ZoneId must be non-empty.");
            }

            for (var i = 0; i < definition.Zones.Count; i++)
            {
                if (existingZoneIndex.HasValue && existingZoneIndex.Value == i)
                {
                    continue;
                }

                if (string.Equals(NormalizeZoneId(definition.Zones[i].ZoneId), normalizedZoneId, StringComparison.Ordinal))
                {
                    return StageAuthoringZoneCommandResult.Failure($"ZoneId '{normalizedZoneId}' already exists.");
                }
            }

            if (!Enum.IsDefined(typeof(FaceId), face))
            {
                return StageAuthoringZoneCommandResult.Failure($"Zone face value {(int)face} is invalid.");
            }

            if (regions == null || regions.Count == 0)
            {
                return StageAuthoringZoneCommandResult.Failure("Zone must contain at least one region.");
            }

            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                if (region.MaxInclusive.x < region.MinInclusive.x ||
                    region.MaxInclusive.y < region.MinInclusive.y)
                {
                    return StageAuthoringZoneCommandResult.Failure($"Zone region[{i}] has inverted min/max bounds.");
                }

                if (!Contains(definition.Board, region.MinInclusive) ||
                    !Contains(definition.Board, region.MaxInclusive))
                {
                    return StageAuthoringZoneCommandResult.Failure($"Zone region[{i}] is outside the board bounds.");
                }
            }

            return StageAuthoringZoneCommandResult.Success(existingZoneIndex ?? -1);
        }

        public static StageZoneRegionDefinition CreateSingleCellRegion(Vector2Int cell)
        {
            return new StageZoneRegionDefinition
            {
                MinInclusive = cell,
                MaxInclusive = cell,
            };
        }

        public static StageZoneRegionDefinition CreateNormalizedRegion(Vector2Int a, Vector2Int b)
        {
            return new StageZoneRegionDefinition
            {
                MinInclusive = new Vector2Int(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)),
                MaxInclusive = new Vector2Int(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)),
            };
        }

        private static StageZoneDefinition CreateZone(
            string zoneId,
            FaceId face,
            IReadOnlyList<StageZoneRegionDefinition> regions)
        {
            return new StageZoneDefinition
            {
                ZoneId = NormalizeZoneId(zoneId),
                FaceId = face,
                Regions = CloneRegions(regions),
            };
        }

        private static StageZoneRegionDefinition[] CloneRegions(IReadOnlyList<StageZoneRegionDefinition> regions)
        {
            return regions != null
                ? regions.ToArray()
                : Array.Empty<StageZoneRegionDefinition>();
        }

        private static string NormalizeZoneId(string zoneId)
        {
            return zoneId?.Trim() ?? string.Empty;
        }

        private static bool Contains(StageBoardDefinition board, Vector2Int cell)
        {
            return cell.x >= board.MinInclusive.x &&
                   cell.x <= board.MaxInclusive.x &&
                   cell.y >= board.MinInclusive.y &&
                   cell.y <= board.MaxInclusive.y;
        }
    }
}
