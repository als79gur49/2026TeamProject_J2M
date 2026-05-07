using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum StageAuthoringGridEditMode
    {
        EntityPlacement,
        TileFeaturePlacement,
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
        private int loadedTileFeatureId;

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
            loadedTileFeatureId = 0;
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
                    new[] { "Entity", "TileFeature", "Zone" },
                    GUILayout.MaxWidth(320));
                if (nextMode != editMode)
                {
                    editMode = nextMode;
                    tileFeatureFeedback = string.Empty;
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
            var targetCell = new SurfaceCell(
                selection.TargetFace,
                selection.TargetCell.x,
                selection.TargetCell.y);
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
            DrawTileFeatureBindingStatus(feature.TileId);

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

        private void DrawTileFeatureBindingStatus(int tileId)
        {
            var presentation = authoring.GeneratedPresentationDefinition;
            if (presentation == null)
            {
                EditorGUILayout.HelpBox("No presentation definition is assigned.", MessageType.Info);
                return;
            }

            var count = 0;
            var bindings = presentation.TileFeaturePresentationBindings;
            for (var i = 0; i < bindings.Length; i++)
            {
                if (bindings[i] != null && bindings[i].TileId == tileId)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                EditorGUILayout.HelpBox("TileFeature visual binding is missing.", MessageType.Warning);
            }
            else if (count == 1)
            {
                EditorGUILayout.HelpBox("TileFeature visual binding exists.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("TileFeature visual binding is duplicated.", MessageType.Warning);
            }
        }

        private void AddTileFeatureAtTargetCell()
        {
            var cell = new SurfaceCell(selection.TargetFace, selection.TargetCell.x, selection.TargetCell.y);
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
            var cell = new SurfaceCell(selection.TargetFace, selection.TargetCell.x, selection.TargetCell.y);
            if (!StageAuthoringPlacementCommands.TryRemoveTileFeaturesAt(authoring, cell, out var removedCount))
            {
                SetTileFeatureFeedback("No TileFeatures were removed.", MessageType.Info);
                return;
            }

            selection.ClearSelectedTileFeature();
            loadedTileFeatureId = 0;
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
        }

        private void SetTileFeatureFeedback(string message, MessageType messageType)
        {
            tileFeatureFeedback = message ?? string.Empty;
            tileFeatureFeedbackType = messageType;
        }

        private void DrawTileFeatureFeedback()
        {
            if (string.IsNullOrWhiteSpace(tileFeatureFeedback))
            {
                return;
            }

            EditorGUILayout.HelpBox(tileFeatureFeedback, tileFeatureFeedbackType);
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
