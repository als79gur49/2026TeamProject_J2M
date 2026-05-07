using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum ExitGoalZoneStatusKind
    {
        None,
        NoExitSelected,
        SelectedTileFeatureIsNotExit,
        ObjectiveDisabled,
        CompletionPolicyUnsupported,
        MissingPrimaryGoal,
        MultiplePrimaryGoals,
        PrimaryGoalNotPlayerAtAnyZone,
        PrimaryGoalReferencesNoZone,
        PrimaryGoalReferencesMultipleZones,
        ReferencedZoneMissing,
        ReferencedZoneShared,
        ZoneNotSingleCell,
        ZoneCellMismatch,
        Valid,
    }

    internal readonly struct ExitGoalZoneStatus
    {
        public ExitGoalZoneStatus(
            ExitGoalZoneStatusKind kind,
            string message,
            int exitTileId,
            SurfaceCell exitCell,
            string primaryGoalZoneId,
            int referencedZoneIndex,
            bool hasCurrentZoneCell,
            SurfaceCell currentZoneCell,
            bool canSync,
            StageConditionAsset primaryGoalCondition)
        {
            Kind = kind;
            Message = message ?? string.Empty;
            ExitTileId = exitTileId;
            ExitCell = exitCell;
            PrimaryGoalZoneId = primaryGoalZoneId ?? string.Empty;
            ReferencedZoneIndex = referencedZoneIndex;
            HasCurrentZoneCell = hasCurrentZoneCell;
            CurrentZoneCell = currentZoneCell;
            CanSync = canSync;
            PrimaryGoalCondition = primaryGoalCondition;
        }

        public ExitGoalZoneStatusKind Kind { get; }

        public string Message { get; }

        public int ExitTileId { get; }

        public SurfaceCell ExitCell { get; }

        public string PrimaryGoalZoneId { get; }

        public int ReferencedZoneIndex { get; }

        public bool HasCurrentZoneCell { get; }

        public SurfaceCell CurrentZoneCell { get; }

        public bool CanSync { get; }

        public StageConditionAsset PrimaryGoalCondition { get; }
    }

    internal static class StageAuthoringExitGoalHelperCommands
    {
        public static bool TryGetExitGoalZoneStatus(
            StageAuthoringDefinition authoring,
            int exitTileId,
            out ExitGoalZoneStatus status)
        {
            status = CreateStatus(
                ExitGoalZoneStatusKind.None,
                "Exit goal zone status has not been evaluated.");

            if (authoring == null)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.NoExitSelected,
                    "StageAuthoringDefinition is missing.");
                return false;
            }

            if (exitTileId <= 0)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.NoExitSelected,
                    "Select an Exit TileFeature to inspect its PrimaryGoal zone.");
                return false;
            }

            if (!TryFindTileFeature(authoring, exitTileId, out var exitFeature) ||
                exitFeature.Kind != TileFeatureKind.Exit)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.SelectedTileFeatureIsNotExit,
                    $"TileFeature TileId {exitTileId} is not an Exit.",
                    exitTileId);
                return false;
            }

            return TryGetExitGoalZoneStatus(authoring, exitFeature, out status);
        }

        public static bool TryEnsureExitPrimaryGoalZone(
            StageAuthoringDefinition authoring,
            int exitTileId,
            out string error)
        {
            error = string.Empty;
            if (!TryGetExitGoalZoneStatus(authoring, exitTileId, out var status))
            {
                error = status.Message;
                return false;
            }

            if (!TryFindTileFeature(authoring, exitTileId, out var exitFeature))
            {
                error = $"TileFeature TileId {exitTileId} was not found.";
                return false;
            }

            return TryCreateOrReplaceGoalZoneForExit(authoring, exitFeature, out _, out error);
        }

        public static bool TryCreateOrReplaceGoalZoneForExit(
            StageAuthoringDefinition authoring,
            StageTileFeatureDefinition exitFeature,
            out string zoneId,
            out string error)
        {
            zoneId = string.Empty;
            error = string.Empty;

            if (!TryGetExitGoalZoneStatus(authoring, exitFeature, out var status))
            {
                error = status.Message;
                return false;
            }

            zoneId = status.PrimaryGoalZoneId;
            if (!status.CanSync && status.Kind != ExitGoalZoneStatusKind.Valid)
            {
                error = status.Message;
                return false;
            }

            if (status.Kind == ExitGoalZoneStatusKind.Valid)
            {
                return true;
            }

            var nextZones = new List<StageZoneDefinition>(authoring.Zones);
            var syncedZone = CreateSingleCellZone(zoneId, exitFeature.Cell);
            if (status.ReferencedZoneIndex >= 0)
            {
                nextZones[status.ReferencedZoneIndex] = syncedZone;
            }
            else
            {
                nextZones.Add(syncedZone);
            }

            Undo.RecordObject(authoring, "Sync Exit Primary Goal Zone");
            authoring.SetZones(nextZones);
            EditorUtility.SetDirty(authoring);
            return true;
        }

        private static bool TryGetExitGoalZoneStatus(
            StageAuthoringDefinition authoring,
            StageTileFeatureDefinition exitFeature,
            out ExitGoalZoneStatus status)
        {
            var exitTileId = exitFeature.TileId;
            var exitCell = exitFeature.Cell;
            var objective = authoring.Objective;

            if (objective.CompletionPolicy == StageCompletionPolicy.Disabled)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.ObjectiveDisabled,
                    "Objective is disabled. Enable objective authoring before syncing the Exit goal zone.",
                    exitTileId,
                    exitCell);
                return false;
            }

            if (objective.CompletionPolicy != StageCompletionPolicy.RequireAllConditions)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.CompletionPolicyUnsupported,
                    $"Exit objective sync requires {StageCompletionPolicy.RequireAllConditions}.",
                    exitTileId,
                    exitCell);
                return false;
            }

            var entries = objective.GetConditionEntriesOrEmpty();
            var primaryGoalIndex = -1;
            var primaryGoalCount = 0;
            var requiredPrimaryGoalCount = 0;
            StageObjectiveConditionEntry primaryGoalEntry = default;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Role != StageObjectiveConditionRole.PrimaryGoal)
                {
                    continue;
                }

                primaryGoalCount++;
                if (!entries[i].Required)
                {
                    continue;
                }

                primaryGoalIndex = i;
                primaryGoalEntry = entries[i];
                requiredPrimaryGoalCount++;
            }

            if (requiredPrimaryGoalCount == 0)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.MissingPrimaryGoal,
                    "Exit objective requires exactly one required PrimaryGoal condition.",
                    exitTileId,
                    exitCell);
                return false;
            }

            if (primaryGoalCount > 1 || requiredPrimaryGoalCount > 1)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.MultiplePrimaryGoals,
                    $"Exit objective has multiple PrimaryGoal conditions ({primaryGoalCount}).",
                    exitTileId,
                    exitCell);
                return false;
            }

            if (primaryGoalEntry.Condition is not PlayerAtAnyZoneConditionAsset playerAtAnyZone)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.PrimaryGoalNotPlayerAtAnyZone,
                    "Exit PrimaryGoal condition must be PlayerAtAnyZoneConditionAsset.",
                    exitTileId,
                    exitCell,
                    primaryGoalCondition: primaryGoalEntry.Condition);
                return false;
            }

            var zoneIds = playerAtAnyZone.ZoneIds
                .Select(zoneId => zoneId?.Trim() ?? string.Empty)
                .Where(zoneId => !string.IsNullOrWhiteSpace(zoneId))
                .ToArray();
            if (zoneIds.Length == 0)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.PrimaryGoalReferencesNoZone,
                    "Exit PrimaryGoal condition must reference exactly one goal zone.",
                    exitTileId,
                    exitCell,
                    primaryGoalCondition: primaryGoalEntry.Condition);
                return false;
            }

            if (zoneIds.Length > 1)
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.PrimaryGoalReferencesMultipleZones,
                    $"Exit PrimaryGoal condition references multiple goal zones ({zoneIds.Length}).",
                    exitTileId,
                    exitCell,
                    primaryGoalCondition: primaryGoalEntry.Condition);
                return false;
            }

            var primaryGoalZoneId = zoneIds[0];
            if (IsZoneReferencedByOtherCondition(entries, primaryGoalIndex, primaryGoalZoneId))
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.ReferencedZoneShared,
                    $"Goal zone '{primaryGoalZoneId}' is referenced by another objective condition and cannot be overwritten safely.",
                    exitTileId,
                    exitCell,
                    primaryGoalZoneId,
                    primaryGoalCondition: primaryGoalEntry.Condition);
                return false;
            }

            if (!TryFindZone(authoring, primaryGoalZoneId, out var zoneIndex, out var zone))
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.ReferencedZoneMissing,
                    $"Goal zone '{primaryGoalZoneId}' is missing and can be created at the Exit center.",
                    exitTileId,
                    exitCell,
                    primaryGoalZoneId,
                    referencedZoneIndex: -1,
                    canSync: true,
                    primaryGoalCondition: primaryGoalEntry.Condition);
                return true;
            }

            if (!TryGetSingleCell(zone, out var zoneCell))
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.ZoneNotSingleCell,
                    $"Goal zone '{primaryGoalZoneId}' must be exactly one cell and can be replaced with the Exit center.",
                    exitTileId,
                    exitCell,
                    primaryGoalZoneId,
                    zoneIndex,
                    canSync: true,
                    primaryGoalCondition: primaryGoalEntry.Condition);
                return true;
            }

            if (!zoneCell.Equals(exitCell))
            {
                status = CreateStatus(
                    ExitGoalZoneStatusKind.ZoneCellMismatch,
                    $"Exit center {exitCell} does not match PrimaryGoal zone '{primaryGoalZoneId}' cell {zoneCell}.",
                    exitTileId,
                    exitCell,
                    primaryGoalZoneId,
                    zoneIndex,
                    hasCurrentZoneCell: true,
                    currentZoneCell: zoneCell,
                    canSync: true,
                    primaryGoalCondition: primaryGoalEntry.Condition);
                return true;
            }

            status = CreateStatus(
                ExitGoalZoneStatusKind.Valid,
                $"Exit center matches PrimaryGoal zone '{primaryGoalZoneId}'.",
                exitTileId,
                exitCell,
                primaryGoalZoneId,
                zoneIndex,
                hasCurrentZoneCell: true,
                currentZoneCell: zoneCell,
                primaryGoalCondition: primaryGoalEntry.Condition);
            return true;
        }

        private static bool TryFindTileFeature(
            StageAuthoringDefinition authoring,
            int tileId,
            out StageTileFeatureDefinition feature)
        {
            var features = authoring.TileFeatures;
            for (var i = 0; i < features.Count; i++)
            {
                if (features[i].TileId == tileId)
                {
                    feature = features[i];
                    return true;
                }
            }

            feature = default;
            return false;
        }

        private static bool TryFindZone(
            StageAuthoringDefinition authoring,
            string zoneId,
            out int zoneIndex,
            out StageZoneDefinition zone)
        {
            var zones = authoring.Zones;
            for (var i = 0; i < zones.Count; i++)
            {
                if (string.Equals(zones[i].ZoneId?.Trim(), zoneId, StringComparison.Ordinal))
                {
                    zoneIndex = i;
                    zone = zones[i];
                    return true;
                }
            }

            zoneIndex = -1;
            zone = default;
            return false;
        }

        private static bool TryGetSingleCell(StageZoneDefinition zone, out SurfaceCell cell)
        {
            var regions = zone.GetRegionsOrEmpty();
            if (regions.Length == 1 &&
                regions[0].MinInclusive == regions[0].MaxInclusive)
            {
                cell = new SurfaceCell(zone.FaceId, regions[0].MinInclusive.x, regions[0].MinInclusive.y);
                return true;
            }

            cell = default;
            return false;
        }

        private static StageZoneDefinition CreateSingleCellZone(string zoneId, SurfaceCell cell)
        {
            return new StageZoneDefinition
            {
                ZoneId = zoneId,
                FaceId = cell.face,
                Regions = new[]
                {
                    new StageZoneRegionDefinition
                    {
                        MinInclusive = new Vector2Int(cell.x, cell.y),
                        MaxInclusive = new Vector2Int(cell.x, cell.y),
                    },
                },
            };
        }

        private static bool IsZoneReferencedByOtherCondition(
            IReadOnlyList<StageObjectiveConditionEntry> entries,
            int primaryGoalIndex,
            string primaryGoalZoneId)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (i == primaryGoalIndex)
                {
                    continue;
                }

                var condition = entries[i].Condition;
                if (condition == null)
                {
                    continue;
                }

                if (!TryReadSerializedZoneIds(condition, out var zoneIds))
                {
                    continue;
                }

                if (zoneIds.Any(zoneId => string.Equals(zoneId, primaryGoalZoneId, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadSerializedZoneIds(StageConditionAsset condition, out string[] zoneIds)
        {
            zoneIds = Array.Empty<string>();
            var serializedObject = new SerializedObject(condition);
            var property = serializedObject.FindProperty("zoneIds");
            if (property == null || !property.isArray)
            {
                return false;
            }

            var next = new List<string>();
            for (var i = 0; i < property.arraySize; i++)
            {
                var zoneId = property.GetArrayElementAtIndex(i).stringValue?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(zoneId))
                {
                    next.Add(zoneId);
                }
            }

            zoneIds = next.ToArray();
            return true;
        }

        private static ExitGoalZoneStatus CreateStatus(
            ExitGoalZoneStatusKind kind,
            string message,
            int exitTileId = 0,
            SurfaceCell exitCell = default,
            string primaryGoalZoneId = "",
            int referencedZoneIndex = -1,
            bool hasCurrentZoneCell = false,
            SurfaceCell currentZoneCell = default,
            bool canSync = false,
            StageConditionAsset primaryGoalCondition = null)
        {
            return new ExitGoalZoneStatus(
                kind,
                message,
                exitTileId,
                exitCell,
                primaryGoalZoneId,
                referencedZoneIndex,
                hasCurrentZoneCell,
                currentZoneCell,
                canSync,
                primaryGoalCondition);
        }
    }
}
