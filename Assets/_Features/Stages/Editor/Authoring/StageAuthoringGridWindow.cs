using System.Linq;
using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum StageAuthoringGridEditMode
    {
        EntityPlacement,
        TileFeaturePlacement,
        BoardTilePresentation,
        ZoneEditing,
    }

    public sealed class StageAuthoringGridWindow : EditorWindow
    {
        private StageAuthoringDefinition authoring;
        private SerializedObject serializedAuthoring;
        private StageAuthoringGridSelectionState selection = new();
        private StageAuthoringGenerationReport lastReport;
        private Vector2 scroll;
        private bool presentationPreviewFoldout = true;
        private bool generatedPreviewFoldout = true;
        private bool validationIssuesFoldout = true;
        private StageAuthoringEntityKind? focusedGridKind;
        private StageAuthoringGridEditMode editMode = StageAuthoringGridEditMode.EntityPlacement;
        private TileFeatureKind selectedTileFeatureKind = TileFeatureKind.Button;
        private TileFeatureActivationRule selectedActivationRule = TileFeatureActivationRule.BottomFaceOnly;
        private Direction2D selectedDirection = Direction2D.Right;
        private TileFeatureBoxSelector selectedBoxSelector = TileFeatureBoxSelector.AnyPushableBox;
        private int selectedBoundEntityId;
        private string selectedPresentationKey = string.Empty;
        private bool tileFeatureEraseMode;
        private string tileFeatureFeedback = string.Empty;
        private MessageType tileFeatureFeedbackType = MessageType.Info;
        private string boardTilePresentationKey = string.Empty;
        private string boardTileFeedback = string.Empty;
        private MessageType boardTileFeedbackType = MessageType.Info;
        private int loadedTileFeatureId;
        private GameObject selectedTileFeatureVisualPrefab;
        private bool tileFeatureVisualBindingAdvancedFoldout;

        internal StageAuthoringGenerationReport LastReportForTests => lastReport;

        internal FaceId TargetFaceForTests => selection.TargetFace;

        internal Vector2Int TargetCellForTests => selection.TargetCell;

        internal StageAuthoringEntityKind? FocusedGridKindForTests => focusedGridKind;

        internal StageAuthoringGridEditMode EditModeForTests => editMode;

        internal int SelectedTileFeatureIdForTests => selection.SelectedTileFeatureId;

        public static void Open(StageAuthoringDefinition definition)
        {
            var window = GetWindow<StageAuthoringGridWindow>("Stage Grid");
            window.Bind(definition);
            window.Show();
        }

        private void OnEnable()
        {
            if (authoring != null)
            {
                Bind(authoring);
            }
        }

        private void Bind(StageAuthoringDefinition definition)
        {
            authoring = definition;
            serializedAuthoring = authoring != null ? new SerializedObject(authoring) : null;
            selection = new StageAuthoringGridSelectionState();
            lastReport = null;
            focusedGridKind = null;
            editMode = StageAuthoringGridEditMode.EntityPlacement;
            selectedTileFeatureKind = TileFeatureKind.Button;
            selectedActivationRule = TileFeatureActivationRule.BottomFaceOnly;
            selectedDirection = Direction2D.Right;
            selectedBoxSelector = TileFeatureBoxSelector.AnyPushableBox;
            selectedBoundEntityId = 0;
            selectedPresentationKey = string.Empty;
            tileFeatureEraseMode = false;
            tileFeatureFeedback = string.Empty;
            boardTilePresentationKey = string.Empty;
            boardTileFeedback = string.Empty;
            loadedTileFeatureId = 0;
            selectedTileFeatureVisualPrefab = null;
            tileFeatureVisualBindingAdvancedFoldout = false;
        }

        internal void BindForTests(StageAuthoringDefinition definition)
        {
            Bind(definition);
        }

        internal void GenerateForTests()
        {
            GenerateAndStoreReport();
        }

        internal void ValidateForTests()
        {
            ValidateAndStoreReport();
        }

        internal void SelectCellForTests(FaceId face, Vector2Int cell)
        {
            selection.SelectCell(face, cell, authoring.Placements);
        }

        internal void SetTargetCellForTests(FaceId face, Vector2Int cell)
        {
            selection.SetTargetCell(face, cell);
        }

        internal void AddPlacementAtTargetCellForTests()
        {
            ExecuteCommandResult(StageAuthoringPlacementCommands.AddPlacement(serializedAuthoring, authoring, selection));
        }

        internal void SetEditModeForTests(StageAuthoringGridEditMode value)
        {
            editMode = value;
        }

        internal void SetTileFeatureDraftForTests(
            TileFeatureKind kind,
            TileFeatureActivationRule activationRule,
            Direction2D direction,
            TileFeatureBoxSelector boxSelector,
            int boundEntityId,
            string presentationKey = "")
        {
            selectedTileFeatureKind = kind;
            selectedActivationRule = activationRule;
            selectedDirection = direction;
            selectedBoxSelector = boxSelector;
            selectedBoundEntityId = boundEntityId;
            selectedPresentationKey = presentationKey ?? string.Empty;
        }

        internal void AddTileFeatureAtTargetCellForTests()
        {
            AddTileFeatureAtTargetCell();
        }

        internal void RemoveTileFeaturesAtTargetCellForTests()
        {
            RemoveTileFeaturesAtTargetCell();
        }

        internal int ResolveSelectedTileFeatureIndexForTests()
        {
            return selection.ResolveSelectedTileFeatureIndex(authoring.TileFeatures);
        }

        internal int CountTileFeaturesAtTargetForTests()
        {
            return selection.CountTileFeaturesAt(
                authoring.TileFeatures,
                selection.TargetFace,
                selection.TargetCell.x,
                selection.TargetCell.y);
        }

        internal void SelectTileFeatureByIdForTests(int tileId)
        {
            selection.SelectTileFeatureById(tileId, authoring.TileFeatures);
            var index = selection.ResolveSelectedTileFeatureIndex(authoring.TileFeatures);
            if (index >= 0 && index < authoring.TileFeatures.Count)
            {
                LoadTileFeatureEditorState(authoring.TileFeatures[index]);
            }
        }

        internal TileFeatureVisualBindingStatus GetSelectedTileFeatureVisualBindingStatusForTests()
        {
            StageAuthoringPresentationBindingCommands.TryGetTileFeatureVisualBindingStatus(
                authoring != null ? authoring.GeneratedPresentationDefinition : null,
                authoring,
                selection.SelectedTileFeatureId,
                out var status);
            return status;
        }

        internal TileFeaturePresentationCatalogOption[] GetSelectedTileFeatureCatalogOptionsForTests()
        {
            var index = selection.ResolveSelectedTileFeatureIndex(authoring.TileFeatures);
            return index >= 0 && index < authoring.TileFeatures.Count
                ? StageAuthoringTileFeaturePresentationCatalogCommands.BuildOptions(
                    authoring.GeneratedPresentationDefinition,
                    authoring.TileFeatures[index].Kind)
                : System.Array.Empty<TileFeaturePresentationCatalogOption>();
        }

        internal TileFeaturePresentationCatalogStatus GetSelectedTileFeatureCatalogStatusForTests()
        {
            var index = selection.ResolveSelectedTileFeatureIndex(authoring.TileFeatures);
            if (index < 0 || index >= authoring.TileFeatures.Count)
            {
                return new TileFeaturePresentationCatalogStatus(
                    TileFeaturePresentationCatalogStatusKind.EmptyKeyUnresolved,
                    string.Empty,
                    "No TileFeature selected.");
            }

            StageAuthoringPresentationBindingCommands.TryGetTileFeatureVisualBindingStatus(
                authoring != null ? authoring.GeneratedPresentationDefinition : null,
                authoring,
                selection.SelectedTileFeatureId,
                out var bindingStatus);
            return StageAuthoringTileFeaturePresentationCatalogCommands.ResolveStatus(
                authoring.GeneratedPresentationDefinition,
                authoring.TileFeatures[index],
                IsDirectOverrideActive(bindingStatus));
        }

        internal bool SetSelectedTileFeatureCatalogPresentationKeyForTests(string presentationKey)
        {
            return SetSelectedTileFeaturePresentationKey(presentationKey);
        }

        internal void SetSelectedTileFeatureVisualPrefabForTests(GameObject visualPrefab)
        {
            selectedTileFeatureVisualPrefab = visualPrefab;
        }

        internal bool SetSelectedTileFeatureVisualBindingForTests(out string error)
        {
            return SetSelectedTileFeatureVisualBinding(out error);
        }

        internal bool RemoveSelectedTileFeatureVisualBindingForTests(out string error)
        {
            return RemoveSelectedTileFeatureVisualBinding(out error);
        }

        internal BoardTilePresentationCatalogOption[] GetBoardTileCatalogOptionsForTests()
        {
            return StageAuthoringPresentationBindingCommands.BuildBoardTilePresentationOptions(
                authoring != null ? authoring.GeneratedPresentationDefinition : null);
        }

        internal BoardTilePresentationOverrideStatus GetBoardTileOverrideStatusForTests()
        {
            var cell = GetTargetSurfaceCell();
            StageAuthoringPresentationBindingCommands.TryGetBoardTilePresentationOverrideStatus(
                authoring != null ? authoring.GeneratedPresentationDefinition : null,
                authoring,
                cell,
                out var status);
            return status;
        }

        internal void SetBoardTilePresentationKeyForTests(string presentationKey)
        {
            boardTilePresentationKey = presentationKey ?? string.Empty;
        }

        internal bool SetBoardTileOverrideForTests(out string error)
        {
            return SetBoardTileOverride(out error);
        }

        internal bool ClearBoardTileOverrideForTests(out string error)
        {
            return ClearBoardTileOverride(out error);
        }

        internal ExitGoalZoneStatus GetSelectedExitGoalZoneStatusForTests()
        {
            StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(
                authoring,
                selection.SelectedTileFeatureId,
                out var status);
            return status;
        }

        internal bool SyncSelectedExitGoalZoneForTests(out string error)
        {
            return SyncSelectedExitGoalZone(out error);
        }

        internal bool EnableSelectedExitObjectiveForTests(out string error)
        {
            return EnableSelectedExitObjective(out error);
        }

        internal bool CreateSelectedExitPrimaryGoalConditionForTests(out string error)
        {
            return CreateSelectedExitPrimaryGoalCondition(out error);
        }

        internal void MoveSelectedPlacementToTargetCellForTests()
        {
            ExecuteCommandResult(StageAuthoringPlacementCommands.MoveSelectedHere(
                serializedAuthoring,
                authoring,
                selection,
                selection.ResolveSelectedPlacementIndex(authoring.Placements)));
        }

        internal void DeleteSelectedPlacementForTests()
        {
            ExecuteCommandResult(StageAuthoringPlacementCommands.DeleteSelected(
                serializedAuthoring,
                selection.ResolveSelectedPlacementIndex(authoring.Placements)));
        }

        internal int ResolveSelectedPlacementIndexForTests()
        {
            return selection.ResolveSelectedPlacementIndex(authoring.Placements);
        }

        internal void RotateSelectedClockwiseForTests()
        {
            ExecuteCommandResult(StageAuthoringPlacementCommands.RotateSelectedClockwise(
                serializedAuthoring,
                authoring,
                selection.ResolveSelectedPlacementIndex(authoring.Placements)));
        }

        internal void RotateSelectedCounterClockwiseForTests()
        {
            ExecuteCommandResult(StageAuthoringPlacementCommands.RotateSelectedCounterClockwise(
                serializedAuthoring,
                authoring,
                selection.ResolveSelectedPlacementIndex(authoring.Placements)));
        }

        internal void ToggleGridFocusKindForTests(StageAuthoringEntityKind kind)
        {
            focusedGridKind = StageAuthoringGridCellStyleUtility.IsFocusedKind(kind, focusedGridKind)
                ? null
                : kind;
        }

        internal void ClearGridFocusForTests()
        {
            focusedGridKind = null;
        }

        private void OnGUI()
        {
            if (authoring == null)
            {
                EditorGUILayout.HelpBox("Select a StageAuthoringDefinition.", MessageType.Info);
                return;
            }

            serializedAuthoring.Update();
            var selectedPlacementIndex = selection.ResolveSelectedPlacementIndex(authoring.Placements);
            if (editMode == StageAuthoringGridEditMode.EntityPlacement)
            {
                ExecuteInspectorAction(
                    StageAuthoringGridHotkeyHandler.HandleRotateHotkeys(
                        selectedPlacementIndex,
                        SupportsSelectedFacingAuthoring(selectedPlacementIndex)),
                    selectedPlacementIndex);
            }

            var toolbarResult = StageAuthoringGridToolbarRenderer.DrawHeader(selection.TargetFace);
            if (toolbarResult.FaceChanged)
            {
                selection.SetTargetFace(toolbarResult.NextFace);
            }

            ExecuteToolbarAction(toolbarResult.Action, selectedPlacementIndex);
            DrawEditModeToolbar();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            focusedGridKind = StageAuthoringGridRenderer.DrawGrid(
                authoring,
                selection,
                focusedGridKind,
                editMode == StageAuthoringGridEditMode.TileFeaturePlacement);
            EditorGUILayout.Space();

            selectedPlacementIndex = selection.ResolveSelectedPlacementIndex(authoring.Placements);
            if (editMode == StageAuthoringGridEditMode.EntityPlacement)
            {
                ExecuteToolbarAction(
                    StageAuthoringGridToolbarRenderer.DrawSelectedCellTools(
                        authoring,
                        selection,
                        selectedPlacementIndex),
                    selectedPlacementIndex);
            }
            else if (editMode == StageAuthoringGridEditMode.TileFeaturePlacement)
            {
                DrawTileFeatureTools();
            }
            else if (editMode == StageAuthoringGridEditMode.BoardTilePresentation)
            {
                DrawBoardTilePresentationTools();
            }
            else
            {
                EditorGUILayout.HelpBox("Zone editing is not implemented in this authoring tool phase.", MessageType.Info);
            }

            EditorGUILayout.Space();
            if (editMode == StageAuthoringGridEditMode.EntityPlacement)
            {
                DrawSelectedPlacementInspector();
            }
            else if (editMode == StageAuthoringGridEditMode.TileFeaturePlacement)
            {
                DrawSelectedTileFeatureInspector();
            }

            StageAuthoringGridToolbarRenderer.DrawReport(lastReport);
            EditorGUILayout.EndScrollView();

            serializedAuthoring.ApplyModifiedProperties();
        }

        private void DrawEditModeToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Mode", GUILayout.Width(48));
                var nextMode = (StageAuthoringGridEditMode)GUILayout.Toolbar(
                    (int)editMode,
                    new[] { "Entity", "TileFeature", "Board Tile", "Zone" },
                    GUILayout.MaxWidth(420));
                if (nextMode != editMode)
                {
                    editMode = nextMode;
                    tileFeatureFeedback = string.Empty;
                    boardTileFeedback = string.Empty;
                }
            }
        }

        private void DrawSelectedPlacementInspector()
        {
            var selectedPlacementIndex = selection.ResolveSelectedPlacementIndex(authoring.Placements);
            var inspectorResult = StageAuthoringGridInspectorRenderer.DrawFields(
                serializedAuthoring,
                authoring,
                selectedPlacementIndex,
                SupportsSelectedFacingAuthoring(selectedPlacementIndex));
            if (!inspectorResult.HasSelection)
            {
                return;
            }

            ExecuteInspectorAction(inspectorResult.Action, selectedPlacementIndex);
            if (inspectorResult.SerializedFieldsChanged)
            {
                FlushSerializedInspectorChanges();
            }

            selectedPlacementIndex = selection.ResolveSelectedPlacementIndex(authoring.Placements);
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= authoring.Placements.Count)
            {
                return;
            }

            var selectedPlacement = authoring.Placements[selectedPlacementIndex];
            var presentationPreview = StageAuthoringPresentationPreviewResolver.Resolve(
                authoring,
                selectedPlacement,
                authoring.GeneratedPresentationDefinition);
            var bindingPreview = StageAuthoringGeneratedBindingPreviewResolver.Resolve(
                authoring,
                selectedPlacement,
                authoring.GeneratedPresentationDefinition,
                authoring.GeneratedGameplayDefinition,
                lastReport != null ? lastReport.Issues : null);
            StageAuthoringPresentationSectionRenderer.DrawPreview(presentationPreview, ref presentationPreviewFoldout);
            StageAuthoringGeneratedPreviewSectionRenderer.Draw(bindingPreview, ref generatedPreviewFoldout);
            StageAuthoringIssueSummaryRenderer.Draw(bindingPreview, ref validationIssuesFoldout);

            ExecuteInspectorAction(
                StageAuthoringGridInspectorRenderer.DrawClearSelectionButton(),
                selectedPlacementIndex);
        }

        private void ExecuteToolbarAction(
            StageAuthoringGridToolbarAction action,
            int selectedPlacementIndex)
        {
            switch (action)
            {
                case StageAuthoringGridToolbarAction.Generate:
                    GenerateAndStoreReport();
                    break;
                case StageAuthoringGridToolbarAction.Validate:
                    ValidateAndStoreReport();
                    break;
                case StageAuthoringGridToolbarAction.AddPlacement:
                    ExecuteCommandResult(StageAuthoringPlacementCommands.AddPlacement(serializedAuthoring, authoring, selection));
                    break;
                case StageAuthoringGridToolbarAction.MoveSelectedHere:
                    ExecuteCommandResult(StageAuthoringPlacementCommands.MoveSelectedHere(
                        serializedAuthoring,
                        authoring,
                        selection,
                        selectedPlacementIndex));
                    break;
                case StageAuthoringGridToolbarAction.DeleteSelected:
                    ExecuteCommandResult(StageAuthoringPlacementCommands.DeleteSelected(
                        serializedAuthoring,
                        selectedPlacementIndex));
                    break;
            }
        }

        private void ExecuteInspectorAction(
            StageAuthoringGridInspectorAction action,
            int selectedPlacementIndex)
        {
            switch (action)
            {
                case StageAuthoringGridInspectorAction.RotateLeft:
                    ExecuteCommandResult(StageAuthoringPlacementCommands.RotateSelectedCounterClockwise(
                        serializedAuthoring,
                        authoring,
                        selectedPlacementIndex));
                    break;
                case StageAuthoringGridInspectorAction.RotateRight:
                    ExecuteCommandResult(StageAuthoringPlacementCommands.RotateSelectedClockwise(
                        serializedAuthoring,
                        authoring,
                        selectedPlacementIndex));
                    break;
                case StageAuthoringGridInspectorAction.ClearSelection:
                    ExecuteCommandResult(StageAuthoringPlacementCommands.ClearSelection());
                    break;
            }
        }

        private void ExecuteCommandResult(StageAuthoringCommandResult result)
        {
            if (!result.Changed)
            {
                return;
            }

            if (result.RequiresApplyModifiedProperties)
            {
                serializedAuthoring.ApplyModifiedProperties();
            }

            if (result.ClearSelectedPlacement)
            {
                selection.ClearSelectedPlacement();
            }

            if (result.HasPlacementSelection)
            {
                selection.SelectPlacement(result.SelectPlacementIndex, authoring.Placements);
            }

            if (result.RequiresSetDirty)
            {
                EditorUtility.SetDirty(authoring);
            }

            if (result.RequiresSerializedObjectUpdate)
            {
                serializedAuthoring.Update();
            }

            if (result.ShouldRepaint)
            {
                Repaint();
            }
        }

        private void DrawTileFeatureTools()
        {
            var targetCell = GetTargetSurfaceCell();
            EditorGUILayout.LabelField("Target Cell", targetCell.ToString());

            tileFeatureEraseMode = EditorGUILayout.Toggle("Erase Mode", tileFeatureEraseMode);
            DrawTileFeatureDraftFields();

            var preview = BuildTileFeatureDraft(targetCell);
            if (!StageAuthoringPlacementCommands.ValidateTileFeatureForPlacement(
                    authoring,
                    WithPreviewTileId(preview),
                    ignoredTileId: 0,
                    out var validationError))
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(tileFeatureEraseMode))
                {
                    if (GUILayout.Button("Add TileFeature"))
                    {
                        AddTileFeatureAtTargetCell();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           !tileFeatureEraseMode ||
                           selection.CountTileFeaturesAt(authoring.TileFeatures, targetCell.face, targetCell.x, targetCell.y) == 0))
                {
                    if (GUILayout.Button("Erase Cell TileFeatures"))
                    {
                        RemoveTileFeaturesAtTargetCell();
                    }
                }
            }

            DrawTileFeatureFeedback();
            DrawTileFeatureListAtTarget(targetCell);
        }

        private void DrawBoardTilePresentationTools()
        {
            var targetCell = GetTargetSurfaceCell();
            EditorGUILayout.LabelField("Target Cell", targetCell.ToString());
            EditorGUILayout.LabelField("Board Tile Presentation", EditorStyles.boldLabel);

            var presentation = authoring.GeneratedPresentationDefinition;
            StageAuthoringPresentationBindingCommands.TryGetBoardTilePresentationOverrideStatus(
                presentation,
                authoring,
                targetCell,
                out var status);
            DrawBoardTileOverrideStatus(status);

            var options = StageAuthoringPresentationBindingCommands.BuildBoardTilePresentationOptions(presentation);
            var labels = options.Select(option => option.Label).ToArray();
            var activeKey = !string.IsNullOrWhiteSpace(boardTilePresentationKey)
                ? BoardTilePresentationCatalog.NormalizePresentationKey(boardTilePresentationKey)
                : status.PresentationKey;
            var selectedIndex = 0;
            for (var i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i].PresentationKey, activeKey, System.StringComparison.Ordinal))
                {
                    selectedIndex = i;
                    break;
                }
            }

            var editorDisabled = presentation == null ||
                                 presentation.BoardTilePresentationCatalog == null ||
                                 status.Kind == BoardTilePresentationOverrideStatusKind.InvalidCell ||
                                 status.Kind == BoardTilePresentationOverrideStatusKind.OutsideBounds ||
                                 options.Length == 0;
            using (new EditorGUI.DisabledScope(editorDisabled))
            {
                var nextIndex = EditorGUILayout.Popup("Catalog Entry", selectedIndex, labels);
                if (nextIndex >= 0 && nextIndex < options.Length)
                {
                    boardTilePresentationKey = options[nextIndex].PresentationKey;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(editorDisabled || string.IsNullOrWhiteSpace(boardTilePresentationKey)))
                {
                    if (GUILayout.Button("Set Override", GUILayout.Width(128)))
                    {
                        SetBoardTileOverride(out _);
                    }
                }

                using (new EditorGUI.DisabledScope(
                           presentation == null ||
                           status.Kind == BoardTilePresentationOverrideStatusKind.MissingOverride ||
                           status.Kind == BoardTilePresentationOverrideStatusKind.NoPresentationDefinition))
                {
                    if (GUILayout.Button("Clear Override", GUILayout.Width(128)))
                    {
                        ClearBoardTileOverride(out _);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var catalog = presentation != null ? presentation.BoardTilePresentationCatalog : null;
                using (new EditorGUI.DisabledScope(catalog == null))
                {
                    if (GUILayout.Button("Ping Catalog", GUILayout.Width(128)))
                    {
                        EditorGUIUtility.PingObject(catalog);
                    }
                }

                var prefab = status.CatalogEntry != null ? status.CatalogEntry.TilePrefab : null;
                using (new EditorGUI.DisabledScope(prefab == null))
                {
                    if (GUILayout.Button("Ping Prefab", GUILayout.Width(128)))
                    {
                        EditorGUIUtility.PingObject(prefab);
                    }
                }
            }

            DrawBoardTileFeedback();
        }

        private void DrawTileFeatureDraftFields()
        {
            EditorGUI.BeginChangeCheck();
            var nextKind = (TileFeatureKind)EditorGUILayout.EnumPopup("Kind", selectedTileFeatureKind);
            if (EditorGUI.EndChangeCheck() && nextKind != selectedTileFeatureKind)
            {
                selectedTileFeatureKind = nextKind;
                ApplyTileFeatureKindDefaults(nextKind);
            }

            if (selectedTileFeatureKind == TileFeatureKind.Button)
            {
                selectedActivationRule = (TileFeatureActivationRule)EditorGUILayout.EnumPopup(
                    "Activation Rule",
                    selectedActivationRule);
                selectedBoxSelector = (TileFeatureBoxSelector)EditorGUILayout.EnumPopup(
                    "Box Selector",
                    selectedBoxSelector);
            }
            else if (selectedTileFeatureKind == TileFeatureKind.Slide)
            {
                selectedDirection = (Direction2D)EditorGUILayout.EnumPopup("Direction", selectedDirection);
            }
            else if (selectedTileFeatureKind == TileFeatureKind.MoonBlockGenerator)
            {
                DrawMoonBoundEntityPicker();
            }

            selectedPresentationKey = EditorGUILayout.TextField(
                "Presentation Key",
                selectedPresentationKey ?? string.Empty);
        }

        private void DrawMoonBoundEntityPicker()
        {
            var moonOptions = BuildMoonBoxEntityOptions();
            if (moonOptions.Length == 0)
            {
                EditorGUILayout.HelpBox("No persisted Moon Box entity id is available.", MessageType.Warning);
                selectedBoundEntityId = EditorGUILayout.IntField("Bound Entity Id", selectedBoundEntityId);
                return;
            }

            var labels = new string[moonOptions.Length + 1];
            var ids = new int[moonOptions.Length + 1];
            labels[0] = "None";
            ids[0] = 0;
            for (var i = 0; i < moonOptions.Length; i++)
            {
                labels[i + 1] = moonOptions[i].Label;
                ids[i + 1] = moonOptions[i].EntityId;
            }

            var selectedIndex = 0;
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i] == selectedBoundEntityId)
                {
                    selectedIndex = i;
                    break;
                }
            }

            selectedBoundEntityId = ids[EditorGUILayout.Popup("Bound Moon Box", selectedIndex, labels)];
        }

        private void DrawTileFeatureListAtTarget(SurfaceCell targetCell)
        {
            var indices = selection.FindTileFeatureIndicesAt(
                authoring.TileFeatures,
                targetCell.face,
                targetCell.x,
                targetCell.y);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("TileFeatures At Cell", indices.Length.ToString());
            if (indices.Length == 0)
            {
                return;
            }

            for (var i = 0; i < indices.Length; i++)
            {
                var index = indices[i];
                var feature = authoring.TileFeatures[index];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"{StageAuthoringGridMarkerBuilder.BuildTileFeatureBadge(feature)} / {feature.Kind}",
                        GUILayout.MinWidth(180));
                    if (GUILayout.Button("Select", GUILayout.Width(72)))
                    {
                        selection.SelectTileFeature(index, authoring.TileFeatures);
                        LoadTileFeatureEditorState(feature);
                    }

                    if (GUILayout.Button("Delete", GUILayout.Width(72)))
                    {
                        RemoveTileFeature(feature.TileId);
                    }
                }
            }
        }

        private void DrawSelectedTileFeatureInspector()
        {
            var selectedIndex = selection.ResolveSelectedTileFeatureIndex(authoring.TileFeatures);
            if (selectedIndex < 0 || selectedIndex >= authoring.TileFeatures.Count)
            {
                EditorGUILayout.HelpBox("No TileFeature selected.", MessageType.Info);
                return;
            }

            var feature = authoring.TileFeatures[selectedIndex];
            if (loadedTileFeatureId != feature.TileId)
            {
                LoadTileFeatureEditorState(feature);
            }

            EditorGUILayout.LabelField(
                "Selected TileFeature",
                $"{feature.TileId} / {feature.Kind} / {feature.Cell}");
            EditorGUILayout.LabelField("TileId", feature.TileId.ToString());
            EditorGUILayout.LabelField("Cell", feature.Cell.ToString());

            DrawTileFeatureDraftFields();
            DrawTileFeatureCatalogSection(feature);
            DrawExitGoalZoneSection(feature);
            tileFeatureVisualBindingAdvancedFoldout = EditorGUILayout.Foldout(
                tileFeatureVisualBindingAdvancedFoldout,
                "Advanced Direct Visual Override",
                toggleOnLabelClick: true);
            if (tileFeatureVisualBindingAdvancedFoldout)
            {
                DrawTileFeatureVisualBindingSection(feature.TileId);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Update", GUILayout.Width(128)))
                {
                    var updated = BuildTileFeatureDraft(feature.Cell);
                    updated.TileId = feature.TileId;
                    UpdateTileFeature(updated);
                }

                if (GUILayout.Button("Delete", GUILayout.Width(96)))
                {
                    RemoveTileFeature(feature.TileId);
                }

                if (GUILayout.Button("Clear Selection", GUILayout.Width(128)))
                {
                    selection.ClearSelectedTileFeature();
                    loadedTileFeatureId = 0;
                }
            }
        }

        private void DrawTileFeatureVisualBindingSection(int tileId)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("TileFeature Visual Binding", EditorStyles.boldLabel);
            var presentation = authoring.GeneratedPresentationDefinition;
            StageAuthoringPresentationBindingCommands.TryGetTileFeatureVisualBindingStatus(
                presentation,
                authoring,
                tileId,
                out var status);
            DrawTileFeatureBindingStatus(status);

            selectedTileFeatureVisualPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Visual Prefab",
                selectedTileFeatureVisualPrefab,
                typeof(GameObject),
                allowSceneObjects: false);

            var bindingEditorDisabled = status.Kind == TileFeatureVisualBindingStatusKind.NoPresentationDefinition ||
                                        status.Kind == TileFeatureVisualBindingStatusKind.MissingTileFeature;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(bindingEditorDisabled || selectedTileFeatureVisualPrefab == null))
                {
                    if (GUILayout.Button("Set / Replace Binding", GUILayout.Width(160)))
                    {
                        SetSelectedTileFeatureVisualBinding(out _);
                    }
                }

                using (new EditorGUI.DisabledScope(
                           bindingEditorDisabled ||
                           status.Kind == TileFeatureVisualBindingStatusKind.MissingBinding))
                {
                    if (GUILayout.Button("Remove Binding", GUILayout.Width(128)))
                    {
                        RemoveSelectedTileFeatureVisualBinding(out _);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(presentation == null))
                {
                    if (GUILayout.Button("Ping PresentationDefinition", GUILayout.Width(192)))
                    {
                        EditorGUIUtility.PingObject(presentation);
                    }
                }

                using (new EditorGUI.DisabledScope(status.VisualPrefab == null))
                {
                    if (GUILayout.Button("Ping Bound Prefab", GUILayout.Width(144)))
                    {
                        EditorGUIUtility.PingObject(status.VisualPrefab);
                    }
                }
            }
        }

        private void DrawTileFeatureCatalogSection(StageTileFeatureDefinition feature)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("TileFeature Catalog Visual", EditorStyles.boldLabel);

            var presentation = authoring.GeneratedPresentationDefinition;
            StageAuthoringPresentationBindingCommands.TryGetTileFeatureVisualBindingStatus(
                presentation,
                authoring,
                feature.TileId,
                out var bindingStatus);
            var directOverrideActive = IsDirectOverrideActive(bindingStatus);
            var catalogStatus = StageAuthoringTileFeaturePresentationCatalogCommands.ResolveStatus(
                presentation,
                feature,
                directOverrideActive);
            DrawTileFeatureCatalogStatus(catalogStatus);

            var options = StageAuthoringTileFeaturePresentationCatalogCommands.BuildOptions(
                presentation,
                feature.Kind);
            var labels = options.Select(option => option.Label).ToArray();
            var currentKey = TileFeaturePresentationCatalog.NormalizePresentationKey(feature.PresentationKey);
            var selectedIndex = 0;
            for (var i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i].PresentationKey, currentKey, System.StringComparison.Ordinal))
                {
                    selectedIndex = i;
                    break;
                }
            }

            var catalogMissing = presentation == null ||
                                 presentation.TileFeaturePresentationCatalog == null;
            using (new EditorGUI.DisabledScope(catalogMissing || options.Length <= 1))
            {
                var nextIndex = EditorGUILayout.Popup("Catalog Visual", selectedIndex, labels);
                if (nextIndex != selectedIndex &&
                    nextIndex >= 0 &&
                    nextIndex < options.Length)
                {
                    SetSelectedTileFeaturePresentationKey(options[nextIndex].PresentationKey);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(presentation == null))
                {
                    if (GUILayout.Button("Ping PresentationDefinition", GUILayout.Width(192)))
                    {
                        EditorGUIUtility.PingObject(presentation);
                    }
                }

                var catalog = presentation != null ? presentation.TileFeaturePresentationCatalog : null;
                using (new EditorGUI.DisabledScope(catalog == null))
                {
                    if (GUILayout.Button("Ping TileFeature Catalog", GUILayout.Width(184)))
                    {
                        EditorGUIUtility.PingObject(catalog);
                    }
                }
            }
        }

        private static void DrawTileFeatureCatalogStatus(TileFeaturePresentationCatalogStatus status)
        {
            var messageType = status.Kind switch
            {
                TileFeaturePresentationCatalogStatusKind.NoPresentationDefinition => MessageType.Warning,
                TileFeaturePresentationCatalogStatusKind.CatalogMissing => MessageType.Warning,
                TileFeaturePresentationCatalogStatusKind.EmptyKeyResolvedByDefault => MessageType.Info,
                TileFeaturePresentationCatalogStatusKind.EmptyKeyUnresolved => MessageType.Warning,
                TileFeaturePresentationCatalogStatusKind.KeyResolved => MessageType.Info,
                TileFeaturePresentationCatalogStatusKind.KeyMissing => MessageType.Warning,
                TileFeaturePresentationCatalogStatusKind.KindMismatch => MessageType.Warning,
                TileFeaturePresentationCatalogStatusKind.DirectionHintMismatch => MessageType.Warning,
                TileFeaturePresentationCatalogStatusKind.DirectOverrideActive => MessageType.Info,
                _ => MessageType.Info,
            };
            EditorGUILayout.HelpBox(status.Message, messageType);
        }

        private static bool IsDirectOverrideActive(TileFeatureVisualBindingStatus status)
        {
            return status.Kind == TileFeatureVisualBindingStatusKind.Bound ||
                   status.Kind == TileFeatureVisualBindingStatusKind.DuplicateBinding ||
                   status.Kind == TileFeatureVisualBindingStatusKind.InvalidPrefab;
        }

        private static void DrawTileFeatureBindingStatus(TileFeatureVisualBindingStatus status)
        {
            var messageType = status.Kind switch
            {
                TileFeatureVisualBindingStatusKind.NoPresentationDefinition => MessageType.Warning,
                TileFeatureVisualBindingStatusKind.MissingTileFeature => MessageType.Warning,
                TileFeatureVisualBindingStatusKind.MissingBinding => MessageType.Warning,
                TileFeatureVisualBindingStatusKind.Bound => MessageType.Info,
                TileFeatureVisualBindingStatusKind.DuplicateBinding => MessageType.Warning,
                TileFeatureVisualBindingStatusKind.InvalidPrefab => MessageType.Error,
                _ => MessageType.Info,
            };
            EditorGUILayout.HelpBox(status.Message, messageType);
        }

        private static void DrawBoardTileOverrideStatus(BoardTilePresentationOverrideStatus status)
        {
            var messageType = status.Kind switch
            {
                BoardTilePresentationOverrideStatusKind.NoPresentationDefinition => MessageType.Warning,
                BoardTilePresentationOverrideStatusKind.CatalogMissing => MessageType.Warning,
                BoardTilePresentationOverrideStatusKind.MissingOverride => MessageType.Info,
                BoardTilePresentationOverrideStatusKind.Resolved => MessageType.Info,
                BoardTilePresentationOverrideStatusKind.MissingKey => MessageType.Warning,
                BoardTilePresentationOverrideStatusKind.DuplicateOverride => MessageType.Error,
                BoardTilePresentationOverrideStatusKind.InvalidCell => MessageType.Error,
                BoardTilePresentationOverrideStatusKind.OutsideBounds => MessageType.Warning,
                _ => MessageType.Info,
            };
            EditorGUILayout.HelpBox(status.Message, messageType);
        }

        private void DrawExitGoalZoneSection(StageTileFeatureDefinition feature)
        {
            EditorGUILayout.Space();
            StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(
                authoring,
                feature.TileId,
                out var status);

            if (feature.Kind != TileFeatureKind.Exit)
            {
                EditorGUILayout.LabelField("Exit Goal Zone", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(status.Message, MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Exit Goal Zone", EditorStyles.boldLabel);
            var hasMultipleExits = authoring.TileFeatures.Count(tileFeature => tileFeature.Kind == TileFeatureKind.Exit) > 1;
            if (hasMultipleExits)
            {
                EditorGUILayout.HelpBox(
                    "Stage has multiple Exit TileFeatures. Resolve the duplicate Exit before syncing the PrimaryGoal zone.",
                    MessageType.Error);
            }

            DrawExitGoalZoneStatus(status);
            EditorGUILayout.LabelField("Exit Cell", status.ExitCell.ToString());
            if (!string.IsNullOrWhiteSpace(status.PrimaryGoalZoneId))
            {
                EditorGUILayout.LabelField("PrimaryGoal Zone", status.PrimaryGoalZoneId);
            }

            if (status.HasCurrentZoneCell)
            {
                EditorGUILayout.LabelField("Current Zone Cell", status.CurrentZoneCell.ToString());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(status.Kind != ExitGoalZoneStatusKind.ObjectiveDisabled || hasMultipleExits))
                {
                    if (GUILayout.Button("Enable Objective", GUILayout.Width(128)))
                    {
                        EnableSelectedExitObjective(out _);
                    }
                }

                using (new EditorGUI.DisabledScope(status.Kind != ExitGoalZoneStatusKind.MissingPrimaryGoal || hasMultipleExits))
                {
                    if (GUILayout.Button("Create PrimaryGoal PlayerAtAnyZone Condition", GUILayout.Width(304)))
                    {
                        CreateSelectedExitPrimaryGoalCondition(out _);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!status.CanSync || hasMultipleExits))
                {
                    if (GUILayout.Button("Sync Primary Goal Zone To Exit", GUILayout.Width(224)))
                    {
                        SyncSelectedExitGoalZone(out _);
                    }
                }

                using (new EditorGUI.DisabledScope(status.PrimaryGoalCondition == null))
                {
                    if (GUILayout.Button("Ping PrimaryGoal Condition", GUILayout.Width(184)))
                    {
                        EditorGUIUtility.PingObject(status.PrimaryGoalCondition);
                    }
                }

                using (new EditorGUI.DisabledScope(!status.HasCurrentZoneCell))
                {
                    if (GUILayout.Button("Select Goal Cell", GUILayout.Width(128)))
                    {
                        selection.SetTargetCell(
                            status.CurrentZoneCell.face,
                            new Vector2Int(status.CurrentZoneCell.x, status.CurrentZoneCell.y));
                        Repaint();
                    }
                }
            }
        }

        private static void DrawExitGoalZoneStatus(ExitGoalZoneStatus status)
        {
            EditorGUILayout.HelpBox(status.Message, ToMessageType(status.Kind));
        }

        private static MessageType ToMessageType(ExitGoalZoneStatusKind kind)
        {
            return kind switch
            {
                ExitGoalZoneStatusKind.Valid => MessageType.Info,
                ExitGoalZoneStatusKind.ReferencedZoneMissing => MessageType.Warning,
                ExitGoalZoneStatusKind.ZoneNotSingleCell => MessageType.Warning,
                ExitGoalZoneStatusKind.ZoneCellMismatch => MessageType.Warning,
                ExitGoalZoneStatusKind.NoExitSelected => MessageType.Info,
                ExitGoalZoneStatusKind.SelectedTileFeatureIsNotExit => MessageType.Info,
                _ => MessageType.Error,
            };
        }

        private void AddTileFeatureAtTargetCell()
        {
            var cell = GetTargetSurfaceCell();
            var template = BuildTileFeatureDraft(cell);
            var expectedTileId = template.TileId > 0
                ? template.TileId
                : StageAuthoringPlacementCommands.AllocateNextTileId(authoring);
            if (!StageAuthoringPlacementCommands.TryAddTileFeature(authoring, cell, template, out var error))
            {
                SetTileFeatureFeedback(error, MessageType.Error);
                return;
            }

            selection.SelectTileFeatureById(expectedTileId, authoring.TileFeatures);
            serializedAuthoring.Update();
            SetTileFeatureFeedback($"Added TileFeature {expectedTileId}.", MessageType.Info);
            Repaint();
        }

        private void RemoveTileFeaturesAtTargetCell()
        {
            var cell = GetTargetSurfaceCell();
            if (!StageAuthoringPlacementCommands.TryRemoveTileFeaturesAt(authoring, cell, out var removedCount))
            {
                SetTileFeatureFeedback("No TileFeatures were removed.", MessageType.Info);
                return;
            }

            selection.ClearSelectedTileFeature();
            loadedTileFeatureId = 0;
            selectedTileFeatureVisualPrefab = null;
            serializedAuthoring.Update();
            SetTileFeatureFeedback($"Removed {removedCount} TileFeature(s).", MessageType.Info);
            Repaint();
        }

        private void RemoveTileFeature(int tileId)
        {
            if (!StageAuthoringPlacementCommands.TryRemoveTileFeature(authoring, tileId, out var error))
            {
                SetTileFeatureFeedback(error, MessageType.Error);
                return;
            }

            selection.ClearSelectedTileFeature();
            loadedTileFeatureId = 0;
            selectedTileFeatureVisualPrefab = null;
            serializedAuthoring.Update();
            SetTileFeatureFeedback($"Removed TileFeature {tileId}.", MessageType.Info);
            Repaint();
        }

        private void UpdateTileFeature(StageTileFeatureDefinition updated)
        {
            if (!StageAuthoringPlacementCommands.TryUpdateTileFeature(authoring, updated, out var error))
            {
                SetTileFeatureFeedback(error, MessageType.Error);
                return;
            }

            selection.SelectTileFeatureById(updated.TileId, authoring.TileFeatures);
            serializedAuthoring.Update();
            SetTileFeatureFeedback($"Updated TileFeature {updated.TileId}.", MessageType.Info);
            Repaint();
        }

        private StageTileFeatureDefinition BuildTileFeatureDraft(SurfaceCell cell)
        {
            return StageAuthoringPlacementCommands.CreateTileFeaturePreset(
                selectedTileFeatureKind,
                cell,
                selectedActivationRule,
                selectedDirection,
                selectedBoxSelector,
                selectedBoundEntityId,
                selectedPresentationKey);
        }

        private StageTileFeatureDefinition WithPreviewTileId(StageTileFeatureDefinition feature)
        {
            if (feature.TileId <= 0)
            {
                feature.TileId = StageAuthoringPlacementCommands.AllocateNextTileId(authoring);
            }

            return feature;
        }

        private void ApplyTileFeatureKindDefaults(TileFeatureKind kind)
        {
            selectedActivationRule = TileFeatureActivationRule.BottomFaceOnly;
            selectedDirection = Direction2D.None;
            selectedBoxSelector = TileFeatureBoxSelector.None;
            selectedBoundEntityId = 0;

            switch (kind)
            {
                case TileFeatureKind.Button:
                    selectedBoxSelector = TileFeatureBoxSelector.AnyPushableBox;
                    break;
                case TileFeatureKind.Slide:
                    selectedActivationRule = TileFeatureActivationRule.FrontFaceOnly;
                    selectedDirection = Direction2D.Right;
                    break;
                case TileFeatureKind.Barricade:
                    selectedActivationRule = TileFeatureActivationRule.FrontFaceOnly;
                    break;
            }
        }

        private void LoadTileFeatureEditorState(StageTileFeatureDefinition feature)
        {
            loadedTileFeatureId = feature.TileId;
            selectedTileFeatureKind = feature.Kind;
            selectedActivationRule = feature.ActivationRule;
            selectedDirection = feature.Direction;
            selectedBoxSelector = feature.BoxSelector;
            selectedBoundEntityId = feature.BoundEntityId;
            selectedPresentationKey = feature.PresentationKey ?? string.Empty;
            selectedTileFeatureVisualPrefab = ResolveTileFeatureVisualPrefab(feature.TileId);
        }

        private bool SetSelectedTileFeatureVisualBinding(out string error)
        {
            var changed = StageAuthoringPresentationBindingCommands.TrySetTileFeatureVisualBinding(
                authoring.GeneratedPresentationDefinition,
                authoring,
                selection.SelectedTileFeatureId,
                selectedTileFeatureVisualPrefab,
                out error);
            if (!changed)
            {
                SetTileFeatureFeedback(error, MessageType.Error);
                return false;
            }

            SetTileFeatureFeedback($"Updated TileFeature visual binding {selection.SelectedTileFeatureId}.", MessageType.Info);
            Repaint();
            return true;
        }

        private bool RemoveSelectedTileFeatureVisualBinding(out string error)
        {
            var changed = StageAuthoringPresentationBindingCommands.TryRemoveTileFeatureVisualBinding(
                authoring.GeneratedPresentationDefinition,
                authoring,
                selection.SelectedTileFeatureId,
                out error);
            if (!changed)
            {
                SetTileFeatureFeedback(error, MessageType.Error);
                return false;
            }

            selectedTileFeatureVisualPrefab = null;
            SetTileFeatureFeedback($"Removed TileFeature visual binding {selection.SelectedTileFeatureId}.", MessageType.Info);
            Repaint();
            return true;
        }

        private bool SetSelectedTileFeaturePresentationKey(string presentationKey)
        {
            var selectedIndex = selection.ResolveSelectedTileFeatureIndex(authoring.TileFeatures);
            if (selectedIndex < 0 || selectedIndex >= authoring.TileFeatures.Count)
            {
                SetTileFeatureFeedback("No TileFeature selected.", MessageType.Warning);
                return false;
            }

            var next = authoring.TileFeatures.ToArray();
            var updated = next[selectedIndex];
            updated.PresentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(presentationKey);
            next[selectedIndex] = updated;

            Undo.RecordObject(authoring, "Set TileFeature Presentation Key");
            authoring.SetTileFeatures(next);
            EditorUtility.SetDirty(authoring);
            serializedAuthoring.Update();
            selectedPresentationKey = updated.PresentationKey;
            selection.SelectTileFeatureById(updated.TileId, authoring.TileFeatures);
            SetTileFeatureFeedback($"Updated TileFeature {updated.TileId} PresentationKey.", MessageType.Info);
            Repaint();
            return true;
        }

        private bool EnableSelectedExitObjective(out string error)
        {
            var changed = StageAuthoringExitGoalHelperCommands.TryEnableExitObjective(
                authoring,
                selection.SelectedTileFeatureId,
                out error);
            if (!changed)
            {
                SetTileFeatureFeedback(error, MessageType.Error);
                return false;
            }

            serializedAuthoring.Update();
            SetTileFeatureFeedback($"Enabled objective for Exit TileFeature {selection.SelectedTileFeatureId}.", MessageType.Info);
            Repaint();
            return true;
        }

        private bool CreateSelectedExitPrimaryGoalCondition(out string error)
        {
            var changed = StageAuthoringExitGoalHelperCommands.TryCreatePrimaryGoalPlayerAtAnyZoneCondition(
                authoring,
                selection.SelectedTileFeatureId,
                out _,
                out error);
            if (!changed)
            {
                SetTileFeatureFeedback(error, MessageType.Error);
                return false;
            }

            serializedAuthoring.Update();
            SetTileFeatureFeedback($"Created Exit PrimaryGoal condition for TileFeature {selection.SelectedTileFeatureId}.", MessageType.Info);
            Repaint();
            return true;
        }

        private bool SyncSelectedExitGoalZone(out string error)
        {
            var changed = StageAuthoringExitGoalHelperCommands.TryEnsureExitPrimaryGoalZone(
                authoring,
                selection.SelectedTileFeatureId,
                out error);
            if (!changed)
            {
                SetTileFeatureFeedback(error, MessageType.Error);
                return false;
            }

            serializedAuthoring.Update();
            SetTileFeatureFeedback($"Synced Exit PrimaryGoal zone for TileFeature {selection.SelectedTileFeatureId}.", MessageType.Info);
            Repaint();
            return true;
        }

        private GameObject ResolveTileFeatureVisualPrefab(int tileId)
        {
            StageAuthoringPresentationBindingCommands.TryGetTileFeatureVisualBindingStatus(
                authoring != null ? authoring.GeneratedPresentationDefinition : null,
                authoring,
                tileId,
                out var status);
            return status.VisualPrefab;
        }

        private void SetTileFeatureFeedback(string message, MessageType messageType)
        {
            tileFeatureFeedback = message ?? string.Empty;
            tileFeatureFeedbackType = messageType;
        }

        private bool SetBoardTileOverride(out string error)
        {
            var cell = GetTargetSurfaceCell();
            var changed = StageAuthoringPresentationBindingCommands.TrySetBoardTilePresentationOverride(
                authoring.GeneratedPresentationDefinition,
                authoring,
                cell,
                boardTilePresentationKey,
                out error);
            if (!changed)
            {
                SetBoardTileFeedback(error, MessageType.Error);
                return false;
            }

            SetBoardTileFeedback($"Updated board tile override for {cell}.", MessageType.Info);
            Repaint();
            return true;
        }

        private bool ClearBoardTileOverride(out string error)
        {
            var cell = GetTargetSurfaceCell();
            var changed = StageAuthoringPresentationBindingCommands.TryClearBoardTilePresentationOverride(
                authoring.GeneratedPresentationDefinition,
                authoring,
                cell,
                out error);
            if (!changed)
            {
                SetBoardTileFeedback(error, MessageType.Error);
                return false;
            }

            boardTilePresentationKey = string.Empty;
            SetBoardTileFeedback($"Cleared board tile override for {cell}.", MessageType.Info);
            Repaint();
            return true;
        }

        private void SetBoardTileFeedback(string message, MessageType messageType)
        {
            boardTileFeedback = message ?? string.Empty;
            boardTileFeedbackType = messageType;
        }

        private void DrawTileFeatureFeedback()
        {
            if (string.IsNullOrWhiteSpace(tileFeatureFeedback))
            {
                return;
            }

            EditorGUILayout.HelpBox(tileFeatureFeedback, tileFeatureFeedbackType);
        }

        private void DrawBoardTileFeedback()
        {
            if (string.IsNullOrWhiteSpace(boardTileFeedback))
            {
                return;
            }

            EditorGUILayout.HelpBox(boardTileFeedback, boardTileFeedbackType);
        }

        private SurfaceCell GetTargetSurfaceCell()
        {
            return new SurfaceCell(selection.TargetFace, selection.TargetCell.x, selection.TargetCell.y);
        }

        private MoonBoxEntityOption[] BuildMoonBoxEntityOptions()
        {
            var options = new System.Collections.Generic.List<MoonBoxEntityOption>();
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null ||
                    placement.Kind != StageAuthoringEntityKind.Box ||
                    placement.BoxArchetype != BoxArchetype.Moon ||
                    !TryGetMappedEntityId(placement.StableGuid, out var entityId))
                {
                    continue;
                }

                options.Add(new MoonBoxEntityOption(
                    entityId,
                    $"{entityId} / {placement.DisplayName} / {placement.Cell}"));
            }

            return options.ToArray();
        }

        private bool TryGetMappedEntityId(string stableGuid, out int entityId)
        {
            var normalized = StageAuthoringGenerator.Normalize(stableGuid);
            var mappings = authoring.EntityIdMappings;
            for (var i = 0; i < mappings.Count; i++)
            {
                var mapping = mappings[i];
                if (!mapping.Retired &&
                    mapping.EntityId > 0 &&
                    StageAuthoringGenerator.Normalize(mapping.StableGuid) == normalized)
                {
                    entityId = mapping.EntityId;
                    return true;
                }
            }

            entityId = 0;
            return false;
        }

        private void FlushSerializedInspectorChanges()
        {
            serializedAuthoring.ApplyModifiedProperties();
            serializedAuthoring.Update();
        }

        private bool SupportsSelectedFacingAuthoring(int selectedPlacementIndex)
        {
            return StageAuthoringPlacementCommands.SupportsSelectedFacingAuthoring(
                serializedAuthoring,
                selectedPlacementIndex);
        }

        private void GenerateAndStoreReport()
        {
            lastReport = StageAuthoringGenerator.Generate(authoring, StageAuthoringGenerateOptions.WriteAll);
        }

        private void ValidateAndStoreReport()
        {
            lastReport = StageAuthoringGenerator.Generate(authoring, StageAuthoringGenerateOptions.DryRunValidation);
        }

        private readonly struct MoonBoxEntityOption
        {
            public MoonBoxEntityOption(int entityId, string label)
            {
                EntityId = entityId;
                Label = label;
            }

            public int EntityId { get; }

            public string Label { get; }
        }
    }
}
