using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringPlacementCommands
    {
        public static StageAuthoringCommandResult AddPlacement(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection)
        {
            if (serializedAuthoring == null ||
                authoring == null ||
                selection == null ||
                selection.CountPlacementsAt(
                    authoring.Placements,
                    selection.TargetFace,
                    selection.TargetCell.x,
                    selection.TargetCell.y) > 0)
            {
                return StageAuthoringCommandResult.NoOp();
            }

            var placementsProperty = serializedAuthoring.FindProperty("placements");
            placementsProperty.InsertArrayElementAtIndex(placementsProperty.arraySize);
            var addedIndex = placementsProperty.arraySize - 1;
            var element = placementsProperty.GetArrayElementAtIndex(addedIndex);
            element.FindPropertyRelative("StableGuid").stringValue = System.Guid.NewGuid().ToString("N");
            element.FindPropertyRelative("DisplayName").stringValue = "Placement";
            element.FindPropertyRelative("Kind").intValue = (int)StageAuthoringEntityKind.Box;
            var cellProperty = element.FindPropertyRelative("Cell");
            cellProperty.FindPropertyRelative("face").intValue = (int)selection.TargetFace;
            cellProperty.FindPropertyRelative("x").intValue = selection.TargetCell.x;
            cellProperty.FindPropertyRelative("y").intValue = selection.TargetCell.y;
            element.FindPropertyRelative("Facing").intValue = (int)Direction.None;
            element.FindPropertyRelative("Hp").intValue = 1;
            element.FindPropertyRelative("UnitStackGroup").stringValue = string.Empty;
            element.FindPropertyRelative("BoxCapabilities").intValue = (int)BoxCapabilities.None;
            element.FindPropertyRelative("BoxArchetype").intValue = (int)BoxArchetype.Normal;
            element.FindPropertyRelative("EnemyAiMode").intValue = 0;
            element.FindPropertyRelative("EnemyAiStateTimer").intValue = 0;
            element.FindPropertyRelative("EnemyAiProfileOverride").objectReferenceValue = null;
            element.FindPropertyRelative("PresentationId").stringValue = string.Empty;
            return StageAuthoringCommandResult.ChangedResult(selectPlacementIndex: addedIndex);
        }

        public static StageAuthoringCommandResult MoveSelectedHere(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection,
            int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                authoring == null ||
                selection == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize ||
                selection.IsTargetOccupiedByOther(authoring.Placements, selectedPlacementIndex) ||
                selection.IsSelectedPlacementAtTarget(authoring.Placements, selectedPlacementIndex))
            {
                return StageAuthoringCommandResult.NoOp();
            }

            var cellProperty = placementsProperty
                .GetArrayElementAtIndex(selectedPlacementIndex)
                .FindPropertyRelative("Cell");
            cellProperty.FindPropertyRelative("face").intValue = (int)selection.TargetFace;
            cellProperty.FindPropertyRelative("x").intValue = selection.TargetCell.x;
            cellProperty.FindPropertyRelative("y").intValue = selection.TargetCell.y;
            return StageAuthoringCommandResult.ChangedResult();
        }

        public static StageAuthoringCommandResult DuplicateSelectedPlacementToTarget(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            StageAuthoringGridSelectionState selection,
            int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                authoring == null ||
                selection == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize ||
                selectedPlacementIndex >= authoring.Placements.Count ||
                selection.CountPlacementsAt(
                    authoring.Placements,
                    selection.TargetFace,
                    selection.TargetCell.x,
                    selection.TargetCell.y) > 0)
            {
                return StageAuthoringCommandResult.NoOp();
            }

            var clone = authoring.Placements[selectedPlacementIndex]?.Clone();
            if (clone == null)
            {
                return StageAuthoringCommandResult.NoOp();
            }

            clone.StableGuid = System.Guid.NewGuid().ToString("N");
            clone.Cell = new SurfaceCell(
                selection.TargetFace,
                selection.TargetCell.x,
                selection.TargetCell.y);

            placementsProperty.InsertArrayElementAtIndex(placementsProperty.arraySize);
            var addedIndex = placementsProperty.arraySize - 1;
            WritePlacement(placementsProperty.GetArrayElementAtIndex(addedIndex), clone);
            return StageAuthoringCommandResult.ChangedResult(selectPlacementIndex: addedIndex);
        }

        public static StageAuthoringCommandResult DeleteSelected(
            SerializedObject serializedAuthoring,
            int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return StageAuthoringCommandResult.NoOp();
            }

            placementsProperty.DeleteArrayElementAtIndex(selectedPlacementIndex);
            return StageAuthoringCommandResult.ChangedResult(clearSelectedPlacement: true);
        }

        public static StageAuthoringCommandResult ClearSelection()
        {
            return StageAuthoringCommandResult.ChangedResult(
                requiresApply: false,
                requiresUpdate: false,
                shouldRepaint: true,
                clearSelectedPlacement: true);
        }

        public static StageAuthoringCommandResult RotateSelectedClockwise(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            int selectedPlacementIndex)
        {
            return RotateSelectedFacing(serializedAuthoring, authoring, selectedPlacementIndex, clockwise: true);
        }

        public static StageAuthoringCommandResult RotateSelectedCounterClockwise(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            int selectedPlacementIndex)
        {
            return RotateSelectedFacing(serializedAuthoring, authoring, selectedPlacementIndex, clockwise: false);
        }

        public static bool SupportsSelectedFacingAuthoring(
            SerializedObject serializedAuthoring,
            int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return false;
            }

            var kind = (StageAuthoringEntityKind)placementsProperty
                .GetArrayElementAtIndex(selectedPlacementIndex)
                .FindPropertyRelative("Kind")
                .intValue;
            return StageAuthoringKindRegistry.TryGet(kind, out var descriptor) &&
                   descriptor.SupportsFacingAuthoring;
        }

        public static int AllocateNextTileId(StageAuthoringDefinition authoring)
        {
            if (authoring == null || authoring.TileFeatures.Count == 0)
            {
                return 1;
            }

            return authoring.TileFeatures
                .Select(feature => feature.TileId)
                .Where(id => id > 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
        }

        public static StageTileFeatureDefinition CreateTileFeaturePreset(
            TileFeatureKind kind,
            SurfaceCell cell,
            TileFeatureActivationRule selectedActivationRule,
            Direction2D selectedDirection,
            TileFeatureBoxSelector selectedBoxSelector,
            int selectedBoundEntityId,
            string selectedPresentationKey)
        {
            var feature = new StageTileFeatureDefinition
            {
                TileId = 0,
                Cell = cell,
                Kind = kind,
                ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                Direction = Direction2D.None,
                BoxSelector = TileFeatureBoxSelector.None,
                BoundEntityId = 0,
                PresentationKey = selectedPresentationKey ?? string.Empty,
            };

            switch (kind)
            {
                case TileFeatureKind.Button:
                    feature.ActivationRule = selectedActivationRule;
                    feature.Direction = Direction2D.None;
                    feature.BoxSelector = selectedBoxSelector == TileFeatureBoxSelector.None
                        ? TileFeatureBoxSelector.AnyPushableBox
                        : selectedBoxSelector;
                    break;
                case TileFeatureKind.Destroy:
                    feature.ActivationRule = TileFeatureActivationQueries.IsSupportedDestroyActivation(selectedActivationRule)
                        ? selectedActivationRule
                        : TileFeatureActivationRule.BottomFaceOnly;
                    break;
                case TileFeatureKind.Slide:
                    feature.ActivationRule = TileFeatureActivationRule.FrontFaceOnly;
                    feature.Direction = selectedDirection;
                    break;
                case TileFeatureKind.Barricade:
                    feature.ActivationRule = TileFeatureActivationRule.FrontFaceOnly;
                    break;
                case TileFeatureKind.Exit:
                    feature.ActivationRule = TileFeatureActivationRule.ActiveFaceOnly;
                    break;
                case TileFeatureKind.Entrance:
                    feature.ActivationRule = TileFeatureActivationRule.BottomFaceOnly;
                    break;
                case TileFeatureKind.MoonBlockGenerator:
                    feature.ActivationRule = TileFeatureActivationRule.BottomFaceOnly;
                    feature.BoundEntityId = selectedBoundEntityId;
                    break;
            }

            return feature;
        }

        public static bool TryAddTileFeature(
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            StageTileFeatureDefinition template,
            out string error)
        {
            error = string.Empty;
            if (authoring == null)
            {
                error = "StageAuthoringDefinition is missing.";
                return false;
            }

            var feature = template;
            feature.Cell = cell;
            if (feature.TileId <= 0)
            {
                feature.TileId = AllocateNextTileId(authoring);
            }

            if (!ValidateTileFeatureForPlacement(authoring, feature, ignoredTileId: 0, out error))
            {
                return false;
            }

            var next = new List<StageTileFeatureDefinition>(authoring.TileFeatures)
            {
                feature,
            };
            Undo.RecordObject(authoring, "Add Stage TileFeature");
            authoring.SetTileFeatures(next);
            EditorUtility.SetDirty(authoring);
            return true;
        }

        public static bool TryRemoveTileFeature(
            StageAuthoringDefinition authoring,
            int tileId,
            out string error)
        {
            error = string.Empty;
            if (authoring == null)
            {
                error = "StageAuthoringDefinition is missing.";
                return false;
            }

            var next = new List<StageTileFeatureDefinition>(authoring.TileFeatures);
            var removed = next.RemoveAll(feature => feature.TileId == tileId);
            if (removed <= 0)
            {
                error = $"TileFeature TileId {tileId} was not found.";
                return false;
            }

            Undo.RecordObject(authoring, "Remove Stage TileFeature");
            authoring.SetTileFeatures(next);
            EditorUtility.SetDirty(authoring);
            return true;
        }

        public static bool TryRemoveTileFeaturesAt(
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            out int removedCount)
        {
            removedCount = 0;
            if (authoring == null)
            {
                return false;
            }

            var next = new List<StageTileFeatureDefinition>(authoring.TileFeatures);
            removedCount = next.RemoveAll(feature => feature.Cell == cell);
            if (removedCount <= 0)
            {
                return false;
            }

            Undo.RecordObject(authoring, "Remove Stage TileFeatures At Cell");
            authoring.SetTileFeatures(next);
            EditorUtility.SetDirty(authoring);
            return true;
        }

        public static bool TryUpdateTileFeature(
            StageAuthoringDefinition authoring,
            StageTileFeatureDefinition updated,
            out string error)
        {
            error = string.Empty;
            if (authoring == null)
            {
                error = "StageAuthoringDefinition is missing.";
                return false;
            }

            var features = authoring.TileFeatures;
            var matchedIndex = -1;
            for (var i = 0; i < features.Count; i++)
            {
                if (features[i].TileId != updated.TileId)
                {
                    continue;
                }

                if (matchedIndex >= 0)
                {
                    error = $"TileFeature TileId {updated.TileId} is duplicated and cannot be updated safely.";
                    return false;
                }

                matchedIndex = i;
            }

            if (matchedIndex < 0)
            {
                error = $"TileFeature TileId {updated.TileId} was not found.";
                return false;
            }

            if (!ValidateTileFeatureForPlacement(authoring, updated, ignoredTileId: updated.TileId, out error))
            {
                return false;
            }

            var next = new List<StageTileFeatureDefinition>(features);
            next[matchedIndex] = updated;
            Undo.RecordObject(authoring, "Update Stage TileFeature");
            authoring.SetTileFeatures(next);
            EditorUtility.SetDirty(authoring);
            return true;
        }

        public static bool TryDuplicateSelectedTileFeatureToTarget(
            StageAuthoringDefinition authoring,
            SurfaceCell cell,
            int selectedTileFeatureIndex,
            out int duplicatedTileId,
            out string error)
        {
            duplicatedTileId = 0;
            error = string.Empty;
            if (authoring == null)
            {
                error = "StageAuthoringDefinition is missing.";
                return false;
            }

            if (selectedTileFeatureIndex < 0 ||
                selectedTileFeatureIndex >= authoring.TileFeatures.Count)
            {
                error = "No TileFeature selected.";
                return false;
            }

            var duplicate = authoring.TileFeatures[selectedTileFeatureIndex];
            duplicate.TileId = AllocateNextTileId(authoring);
            duplicate.Cell = cell;
            if (!ValidateTileFeatureForPlacement(authoring, duplicate, ignoredTileId: 0, out error))
            {
                return false;
            }

            var next = new List<StageTileFeatureDefinition>(authoring.TileFeatures)
            {
                duplicate,
            };
            Undo.RecordObject(authoring, "Duplicate Stage TileFeature");
            authoring.SetTileFeatures(next);
            EditorUtility.SetDirty(authoring);
            duplicatedTileId = duplicate.TileId;
            return true;
        }

        public static bool ValidateTileFeatureForPlacement(
            StageAuthoringDefinition authoring,
            StageTileFeatureDefinition feature,
            int ignoredTileId,
            out string error)
        {
            error = string.Empty;
            if (authoring == null)
            {
                error = "StageAuthoringDefinition is missing.";
                return false;
            }

            if (!IsWithinBoard(authoring.Board, feature.Cell))
            {
                error = $"TileFeature cell {feature.Cell} is outside the board bounds.";
                return false;
            }

            if (feature.TileId <= 0)
            {
                error = "TileFeature must use a positive TileId.";
                return false;
            }

            if (authoring.TileFeatures.Any(existing =>
                    existing.TileId == feature.TileId &&
                    existing.TileId != ignoredTileId))
            {
                error = $"TileFeature TileId {feature.TileId} already exists.";
                return false;
            }

            if (!Enum.IsDefined(typeof(FaceId), feature.Cell.face))
            {
                error = $"TileFeature face value {(int)feature.Cell.face} is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(TileFeatureKind), feature.Kind) ||
                feature.Kind == TileFeatureKind.Unknown)
            {
                error = "TileFeature must use a known kind.";
                return false;
            }

            if (!Enum.IsDefined(typeof(TileFeatureActivationRule), feature.ActivationRule))
            {
                error = $"TileFeature activation rule value {(int)feature.ActivationRule} is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(Direction2D), feature.Direction))
            {
                error = $"TileFeature direction value {(int)feature.Direction} is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(TileFeatureBoxSelector), feature.BoxSelector))
            {
                error = $"TileFeature box selector value {(int)feature.BoxSelector} is invalid.";
                return false;
            }

            if (!ValidateFixedPolicy(feature, out error))
            {
                return false;
            }

            for (var i = 0; i < authoring.TileFeatures.Count; i++)
            {
                var existing = authoring.TileFeatures[i];
                if (existing.TileId == ignoredTileId ||
                    existing.Cell != feature.Cell)
                {
                    continue;
                }

                if (feature.Kind == TileFeatureKind.Slide &&
                    existing.Kind == TileFeatureKind.Slide)
                {
                    error = $"Cell {feature.Cell} already has a SlideTile.";
                    return false;
                }

                if (feature.Kind == TileFeatureKind.Barricade &&
                    existing.Kind == TileFeatureKind.Barricade)
                {
                    error = $"Cell {feature.Cell} already has a Barricade.";
                    return false;
                }
            }

            if (feature.Kind == TileFeatureKind.Exit &&
                authoring.TileFeatures.Any(existing =>
                    existing.TileId != ignoredTileId &&
                    existing.Kind == TileFeatureKind.Exit))
            {
                error = "Only one Exit TileFeature is supported per stage.";
                return false;
            }

            if (feature.Kind == TileFeatureKind.Entrance)
            {
                if (authoring.TileFeatures.Any(existing =>
                        existing.TileId != ignoredTileId &&
                        existing.Kind == TileFeatureKind.Entrance))
                {
                    error = "Only one Entrance TileFeature is supported per stage.";
                    return false;
                }

                if (!TryGetPlayerPlacementCell(authoring, out var playerCell))
                {
                    error = "Entrance requires exactly one player placement.";
                    return false;
                }

                if (!feature.Cell.Equals(playerCell))
                {
                    error = $"Entrance cell must match the player placement cell {playerCell}.";
                    return false;
                }
            }

            if (feature.Kind == TileFeatureKind.MoonBlockGenerator)
            {
                if (authoring.TileFeatures.Any(existing =>
                        existing.TileId != ignoredTileId &&
                        existing.Kind == TileFeatureKind.MoonBlockGenerator))
                {
                    error = "Only one MoonBlockGenerator TileFeature is supported per stage.";
                    return false;
                }

                if (feature.BoundEntityId <= 0)
                {
                    error = "MoonBlockGenerator must bind a positive Moon Box entity id.";
                    return false;
                }
            }

            return true;
        }

        private static void WritePlacement(
            SerializedProperty element,
            StagePlacedEntityAuthoring placement)
        {
            element.FindPropertyRelative("StableGuid").stringValue = placement.StableGuid ?? string.Empty;
            element.FindPropertyRelative("DisplayName").stringValue = placement.DisplayName ?? string.Empty;
            element.FindPropertyRelative("Kind").intValue = (int)placement.Kind;
            var cellProperty = element.FindPropertyRelative("Cell");
            cellProperty.FindPropertyRelative("face").intValue = (int)placement.Cell.face;
            cellProperty.FindPropertyRelative("x").intValue = placement.Cell.x;
            cellProperty.FindPropertyRelative("y").intValue = placement.Cell.y;
            element.FindPropertyRelative("Facing").intValue = (int)placement.Facing;
            element.FindPropertyRelative("Hp").intValue = placement.Hp;
            element.FindPropertyRelative("UnitMobilityKind").intValue = (int)placement.UnitMobilityKind;
            element.FindPropertyRelative("UnitStackGroup").stringValue = placement.UnitStackGroup ?? string.Empty;
            element.FindPropertyRelative("BoxCapabilities").intValue = (int)placement.BoxCapabilities;
            element.FindPropertyRelative("BoxArchetype").intValue = (int)placement.BoxArchetype;
            element.FindPropertyRelative("EnemyAiMode").intValue = (int)placement.EnemyAiMode;
            element.FindPropertyRelative("EnemyAiStateTimer").intValue = placement.EnemyAiStateTimer;
            element.FindPropertyRelative("EnemyAiProfileOverride").objectReferenceValue = placement.EnemyAiProfileOverride;
            element.FindPropertyRelative("PresentationId").stringValue = placement.PresentationId ?? string.Empty;
        }

        private static StageAuthoringCommandResult RotateSelectedFacing(
            SerializedObject serializedAuthoring,
            StageAuthoringDefinition authoring,
            int selectedPlacementIndex,
            bool clockwise)
        {
            var placementsProperty = serializedAuthoring != null
                ? serializedAuthoring.FindProperty("placements")
                : null;
            if (placementsProperty == null ||
                authoring == null ||
                selectedPlacementIndex < 0 ||
                selectedPlacementIndex >= placementsProperty.arraySize ||
                !SupportsSelectedFacingAuthoring(serializedAuthoring, selectedPlacementIndex))
            {
                return StageAuthoringCommandResult.NoOp();
            }

            var element = placementsProperty.GetArrayElementAtIndex(selectedPlacementIndex);
            var facingProperty = element.FindPropertyRelative("Facing");
            var current = (Direction)facingProperty.intValue;
            var next = clockwise
                ? StageAuthoringFacingDisplayUtility.RotateClockwise(current)
                : StageAuthoringFacingDisplayUtility.RotateCounterClockwise(current);
            if (next == current)
            {
                return StageAuthoringCommandResult.NoOp();
            }

            Undo.RecordObject(authoring, "Rotate Stage Placement Facing");
            facingProperty.intValue = (int)next;
            return StageAuthoringCommandResult.ChangedResult(
                requiresSetDirty: true,
                shouldRepaint: true);
        }

        private static bool ValidateFixedPolicy(
            StageTileFeatureDefinition feature,
            out string error)
        {
            error = string.Empty;
            switch (feature.Kind)
            {
                case TileFeatureKind.Destroy:
                    if (!TileFeatureActivationQueries.IsSupportedDestroyActivation(feature.ActivationRule))
                    {
                        error = "DestroyTile must use a FaceOnly activation rule.";
                        return false;
                    }

                    if (feature.Direction != Direction2D.None ||
                        feature.BoxSelector != TileFeatureBoxSelector.None ||
                        feature.BoundEntityId != 0)
                    {
                        error = "DestroyTile must use Direction None, BoxSelector None, and BoundEntityId 0.";
                        return false;
                    }

                    break;
                case TileFeatureKind.Slide:
                    if (feature.ActivationRule != TileFeatureActivationRule.FrontFaceOnly)
                    {
                        error = "SlideTile must use FrontFaceOnly activation.";
                        return false;
                    }

                    if (!IsCardinalDirection(feature.Direction))
                    {
                        error = "SlideTile must use a cardinal Direction2D.";
                        return false;
                    }

                    if (feature.BoxSelector != TileFeatureBoxSelector.None ||
                        feature.BoundEntityId != 0)
                    {
                        error = "SlideTile must use BoxSelector None and BoundEntityId 0.";
                        return false;
                    }

                    break;
                case TileFeatureKind.Barricade:
                    if (feature.ActivationRule != TileFeatureActivationRule.FrontFaceOnly ||
                        feature.Direction != Direction2D.None ||
                        feature.BoxSelector != TileFeatureBoxSelector.None ||
                        feature.BoundEntityId != 0)
                    {
                        error = "Barricade must use FrontFaceOnly, Direction None, BoxSelector None, and BoundEntityId 0.";
                        return false;
                    }

                    break;
                case TileFeatureKind.Exit:
                    if (feature.ActivationRule != TileFeatureActivationRule.ActiveFaceOnly ||
                        feature.Direction != Direction2D.None ||
                        feature.BoxSelector != TileFeatureBoxSelector.None ||
                        feature.BoundEntityId != 0)
                    {
                        error = "Exit must use ActiveFaceOnly, Direction None, BoxSelector None, and BoundEntityId 0.";
                        return false;
                    }

                    break;
                case TileFeatureKind.Entrance:
                    if (feature.ActivationRule != TileFeatureActivationRule.BottomFaceOnly ||
                        feature.Direction != Direction2D.None ||
                        feature.BoxSelector != TileFeatureBoxSelector.None ||
                        feature.BoundEntityId != 0)
                    {
                        error = "Entrance must use BottomFaceOnly, Direction None, BoxSelector None, and BoundEntityId 0.";
                        return false;
                    }

                    break;
                case TileFeatureKind.MoonBlockGenerator:
                    if (feature.ActivationRule != TileFeatureActivationRule.BottomFaceOnly ||
                        feature.Direction != Direction2D.None ||
                        feature.BoxSelector != TileFeatureBoxSelector.None)
                    {
                        error = "MoonBlockGenerator must use BottomFaceOnly, Direction None, and BoxSelector None.";
                        return false;
                    }

                    break;
            }

            return true;
        }

        private static bool TryGetPlayerPlacementCell(StageAuthoringDefinition authoring, out SurfaceCell cell)
        {
            var found = false;
            cell = default;
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null ||
                    placement.Kind != StageAuthoringEntityKind.Player)
                {
                    continue;
                }

                if (found)
                {
                    return false;
                }

                found = true;
                cell = placement.Cell;
            }

            return found;
        }

        private static bool IsCardinalDirection(Direction2D direction)
        {
            return direction == Direction2D.Up ||
                   direction == Direction2D.Right ||
                   direction == Direction2D.Down ||
                   direction == Direction2D.Left;
        }

        private static bool IsWithinBoard(StageBoardDefinition board, SurfaceCell cell)
        {
            return cell.x >= board.MinInclusive.x &&
                   cell.x <= board.MaxInclusive.x &&
                   cell.y >= board.MinInclusive.y &&
                   cell.y <= board.MaxInclusive.y;
        }
    }
}
