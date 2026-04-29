using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
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

        internal StageAuthoringGenerationReport LastReportForTests => lastReport;

        internal FaceId TargetFaceForTests => selection.TargetFace;

        internal Vector2Int TargetCellForTests => selection.TargetCell;

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
            AddPlacementAtTargetCell();
        }

        internal void MoveSelectedPlacementToTargetCellForTests()
        {
            MoveSelectedPlacementToTargetCell(selection.ResolveSelectedPlacementIndex(authoring.Placements));
            serializedAuthoring.ApplyModifiedProperties();
            serializedAuthoring.Update();
        }

        internal void DeleteSelectedPlacementForTests()
        {
            DeleteSelectedPlacement(selection.ResolveSelectedPlacementIndex(authoring.Placements));
            serializedAuthoring.ApplyModifiedProperties();
            serializedAuthoring.Update();
        }

        internal int ResolveSelectedPlacementIndexForTests()
        {
            return selection.ResolveSelectedPlacementIndex(authoring.Placements);
        }

        internal void RotateSelectedClockwiseForTests()
        {
            RotateSelectedFacing(selection.ResolveSelectedPlacementIndex(authoring.Placements), clockwise: true);
        }

        internal void RotateSelectedCounterClockwiseForTests()
        {
            RotateSelectedFacing(selection.ResolveSelectedPlacementIndex(authoring.Placements), clockwise: false);
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
            HandleRotateHotkeys(selectedPlacementIndex);
            using (new EditorGUILayout.HorizontalScope())
            {
                var nextFace = (FaceId)EditorGUILayout.EnumPopup("Face", selection.TargetFace);
                if (nextFace != selection.TargetFace)
                {
                    selection.SetTargetFace(nextFace);
                }

                if (GUILayout.Button("Generate", GUILayout.Width(96)))
                {
                    GenerateAndStoreReport();
                }

                if (GUILayout.Button("Validate", GUILayout.Width(96)))
                {
                    ValidateAndStoreReport();
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawGrid();
            EditorGUILayout.Space();
            DrawSelectedCellTools(selectedPlacementIndex);
            EditorGUILayout.Space();
            DrawSelectedPlacementInspector(selectedPlacementIndex);
            DrawReport(lastReport);
            EditorGUILayout.EndScrollView();

            serializedAuthoring.ApplyModifiedProperties();
        }

        private void DrawGrid()
        {
            var board = authoring.Board;
            EditorGUILayout.LabelField(
                $"Board {board.MinInclusive.x},{board.MinInclusive.y} to {board.MaxInclusive.x},{board.MaxInclusive.y}");
            if (board.MaxInclusive.x < board.MinInclusive.x ||
                board.MaxInclusive.y < board.MinInclusive.y)
            {
                EditorGUILayout.HelpBox("Board bounds are invalid.", MessageType.Error);
                return;
            }

            for (var y = board.MaxInclusive.y; y >= board.MinInclusive.y; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var x = board.MinInclusive.x; x <= board.MaxInclusive.x; x++)
                    {
                        var index = selection.FindPlacementAt(authoring.Placements, selection.TargetFace, x, y);
                        var placementCount = selection.CountPlacementsAt(authoring.Placements, selection.TargetFace, x, y);
                        var marker = index >= 0
                            ? StageAuthoringGridRenderer.GetMarker(authoring.Placements[index])
                            : ".";
                        if (placementCount > 1)
                        {
                            marker = StageAuthoringGridMarkerBuilder.Build(authoring.Placements[index], duplicateCell: true);
                        }

                        if (selection.TargetCell.x == x && selection.TargetCell.y == y)
                        {
                            marker = $"[{marker}]";
                        }

                        if (GUILayout.Button(marker, GUILayout.Width(36), GUILayout.Height(28)))
                        {
                            selection.SelectCell(selection.TargetFace, new Vector2Int(x, y), authoring.Placements);
                        }
                    }
                }
            }
        }

        private void DrawSelectedCellTools(int selectedPlacementIndex)
        {
            var targetPlacementCount = selection.CountPlacementsAt(
                authoring.Placements,
                selection.TargetFace,
                selection.TargetCell.x,
                selection.TargetCell.y);
            var targetOccupied = targetPlacementCount > 0;
            var targetOccupiedByOther = selection.IsTargetOccupiedByOther(authoring.Placements, selectedPlacementIndex);
            var selectedAtTarget = selection.IsSelectedPlacementAtTarget(authoring.Placements, selectedPlacementIndex);

            EditorGUILayout.LabelField(
                "Target Cell",
                $"{selection.TargetFace}({selection.TargetCell.x},{selection.TargetCell.y})");
            if (targetPlacementCount > 1)
            {
                EditorGUILayout.HelpBox(
                    "Target cell contains multiple placements. Resolve the duplicate authoring data before moving or adding.",
                    MessageType.Warning);
            }
            else if (targetOccupiedByOther)
            {
                EditorGUILayout.HelpBox("Target cell already contains a placement.", MessageType.Warning);
            }
            else if (selectedAtTarget)
            {
                EditorGUILayout.HelpBox("Selected placement is already at the target cell.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(targetOccupied))
                {
                    if (GUILayout.Button("Add Placement"))
                    {
                        AddPlacementAtTargetCell();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           selectedPlacementIndex < 0 ||
                           targetOccupiedByOther ||
                           selectedAtTarget))
                {
                    if (GUILayout.Button("Move Selected Here"))
                    {
                        MoveSelectedPlacementToTargetCell(selectedPlacementIndex);
                    }
                }

                using (new EditorGUI.DisabledScope(selectedPlacementIndex < 0))
                {
                    if (GUILayout.Button("Delete Selected"))
                    {
                        DeleteSelectedPlacement(selectedPlacementIndex);
                    }
                }
            }
        }

        private void DrawSelectedPlacementInspector(int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= placementsProperty.arraySize)
            {
                EditorGUILayout.HelpBox("No placement selected.", MessageType.Info);
                return;
            }

            var element = placementsProperty.GetArrayElementAtIndex(selectedPlacementIndex);
            DrawSelectedPlacementHeader(element, authoring.Placements[selectedPlacementIndex]);
            DrawRotateControls(selectedPlacementIndex);
            EditorGUILayout.PropertyField(element, includeChildren: true);
            DrawPresentationDropdown(element);
            serializedAuthoring.ApplyModifiedProperties();
            serializedAuthoring.Update();

            if (selectedPlacementIndex >= authoring.Placements.Count)
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
            DrawPresentationPreview(presentationPreview);
            DrawGeneratedPreview(bindingPreview);
            DrawValidationIssueSummary(bindingPreview);

            if (GUILayout.Button("Clear Selection", GUILayout.Width(128)))
            {
                selection.ClearSelectedPlacement();
            }
        }

        private static void DrawSelectedPlacementHeader(
            SerializedProperty placementProperty,
            StagePlacedEntityAuthoring placement)
        {
            var displayName = placementProperty.FindPropertyRelative("DisplayName").stringValue;
            var kind = (StageAuthoringEntityKind)placementProperty.FindPropertyRelative("Kind").intValue;
            var cellProperty = placementProperty.FindPropertyRelative("Cell");
            var face = (FaceId)cellProperty.FindPropertyRelative("face").intValue;
            var x = cellProperty.FindPropertyRelative("x").intValue;
            var y = cellProperty.FindPropertyRelative("y").intValue;
            var facing = placement != null ? placement.Facing : Direction.None;
            var facingLabel = StageAuthoringFacingDisplayUtility.ToFacingLabel(facing);
            var facingArrow = StageAuthoringFacingDisplayUtility.ToFacingArrow(facing);
            EditorGUILayout.LabelField(
                "Selected Placement",
                $"{displayName} / {kind} / {face}({x},{y}) / Facing: {facingLabel} {facingArrow}");
        }

        private void DrawRotateControls(int selectedPlacementIndex)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedPlacementIndex < 0))
                {
                    if (GUILayout.Button("Rotate Left", GUILayout.Width(112)))
                    {
                        RotateSelectedFacing(selectedPlacementIndex, clockwise: false);
                    }

                    if (GUILayout.Button("Rotate Right", GUILayout.Width(112)))
                    {
                        RotateSelectedFacing(selectedPlacementIndex, clockwise: true);
                    }
                }
            }
        }

        private void DrawPresentationDropdown(SerializedProperty placementProperty)
        {
            var kind = (StageAuthoringEntityKind)placementProperty.FindPropertyRelative("Kind").intValue;
            if (kind == StageAuthoringEntityKind.Player)
            {
                return;
            }

            var presentationProperty = placementProperty.FindPropertyRelative("PresentationId");
            var model = StageAuthoringPresentationOptionModel.Build(
                kind,
                presentationProperty.stringValue,
                authoring.GeneratedPresentationDefinition);
            for (var i = 0; i < model.WarningMessages.Length; i++)
            {
                EditorGUILayout.HelpBox(model.WarningMessages[i], MessageType.Warning);
            }

            if (model.PopupLabels.Length == 0)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUILayout.Popup(
                "Presentation",
                model.SelectedPopupIndex,
                model.PopupLabels);
            if (EditorGUI.EndChangeCheck())
            {
                presentationProperty.stringValue = model.ResolvePresentationId(nextIndex);
            }
        }

        private void AddPlacementAtTargetCell()
        {
            if (selection.CountPlacementsAt(
                    authoring.Placements,
                    selection.TargetFace,
                    selection.TargetCell.x,
                    selection.TargetCell.y) > 0)
            {
                return;
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
            element.FindPropertyRelative("EnemyAiMode").intValue = 0;
            element.FindPropertyRelative("EnemyAiStateTimer").intValue = 0;
            element.FindPropertyRelative("EnemyAiProfileOverride").objectReferenceValue = null;
            element.FindPropertyRelative("PresentationId").stringValue = string.Empty;
            serializedAuthoring.ApplyModifiedProperties();
            selection.SelectPlacement(addedIndex, authoring.Placements);
            serializedAuthoring.Update();
        }

        private void MoveSelectedPlacementToTargetCell(int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return;
            }

            if (selection.IsTargetOccupiedByOther(authoring.Placements, selectedPlacementIndex) ||
                selection.IsSelectedPlacementAtTarget(authoring.Placements, selectedPlacementIndex))
            {
                return;
            }

            var cellProperty = placementsProperty
                .GetArrayElementAtIndex(selectedPlacementIndex)
                .FindPropertyRelative("Cell");
            cellProperty.FindPropertyRelative("face").intValue = (int)selection.TargetFace;
            cellProperty.FindPropertyRelative("x").intValue = selection.TargetCell.x;
            cellProperty.FindPropertyRelative("y").intValue = selection.TargetCell.y;
        }

        private void DeleteSelectedPlacement(int selectedPlacementIndex)
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return;
            }

            placementsProperty.DeleteArrayElementAtIndex(selectedPlacementIndex);
            selection.ClearSelectedPlacement();
        }

        private void RotateSelectedFacing(int selectedPlacementIndex, bool clockwise)
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return;
            }

            var element = placementsProperty.GetArrayElementAtIndex(selectedPlacementIndex);
            var facingProperty = element.FindPropertyRelative("Facing");
            var current = (Direction)facingProperty.intValue;
            var next = clockwise
                ? StageAuthoringFacingDisplayUtility.RotateClockwise(current)
                : StageAuthoringFacingDisplayUtility.RotateCounterClockwise(current);
            if (next == current)
            {
                return;
            }

            Undo.RecordObject(authoring, "Rotate Stage Placement Facing");
            facingProperty.intValue = (int)next;
            serializedAuthoring.ApplyModifiedProperties();
            EditorUtility.SetDirty(authoring);
            serializedAuthoring.Update();
            Repaint();
        }

        private void HandleRotateHotkeys(int selectedPlacementIndex)
        {
            var currentEvent = Event.current;
            if (currentEvent == null ||
                currentEvent.type != EventType.KeyDown ||
                currentEvent.keyCode != KeyCode.R ||
                EditorGUIUtility.editingTextField ||
                selectedPlacementIndex < 0)
            {
                return;
            }

            RotateSelectedFacing(selectedPlacementIndex, clockwise: !currentEvent.shift);
            currentEvent.Use();
        }

        private void GenerateAndStoreReport()
        {
            lastReport = StageAuthoringGenerator.Generate(authoring, StageAuthoringGenerateOptions.WriteAll);
        }

        private void ValidateAndStoreReport()
        {
            lastReport = StageAuthoringGenerator.Generate(authoring, StageAuthoringGenerateOptions.DryRunValidation);
        }

        private void DrawPresentationPreview(StageAuthoringPresentationPreviewModel model)
        {
            presentationPreviewFoldout = EditorGUILayout.Foldout(presentationPreviewFoldout, "Presentation Preview", toggleOnLabelClick: true);
            if (!presentationPreviewFoldout)
            {
                return;
            }

            if (model == null || !model.RequiresPresentation)
            {
                EditorGUILayout.HelpBox("Player presentation is not authored here.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Type", model.PresentationKindLabel);
            EditorGUILayout.LabelField("PresentationId", model.HasPresentationId ? model.PresentationId : "(None)");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Catalog", model.CatalogAsset, typeof(UnityEngine.Object), allowSceneObjects: false);
                EditorGUILayout.ObjectField("ViewPrefab", model.ViewPrefab, typeof(UnityEngine.Object), allowSceneObjects: false);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(model.CatalogAsset == null))
                {
                    if (GUILayout.Button("Ping Catalog"))
                    {
                        EditorGUIUtility.PingObject(model.CatalogAsset);
                    }

                    if (GUILayout.Button("Select Catalog"))
                    {
                        Selection.activeObject = model.CatalogAsset;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(model.ViewPrefab == null))
                {
                    if (GUILayout.Button("Ping Prefab"))
                    {
                        EditorGUIUtility.PingObject(model.ViewPrefab);
                    }

                    if (GUILayout.Button("Select Prefab"))
                    {
                        Selection.activeObject = model.ViewPrefab;
                    }
                }
            }

            EditorGUILayout.HelpBox(model.StatusLabel, model.StatusMessageType);
            for (var i = 0; i < model.WarningMessages.Length; i++)
            {
                if (model.WarningMessages[i] != model.StatusLabel)
                {
                    EditorGUILayout.HelpBox(model.WarningMessages[i], MessageType.Warning);
                }
            }
        }

        private void DrawGeneratedPreview(StageAuthoringGeneratedBindingPreviewModel model)
        {
            generatedPreviewFoldout = EditorGUILayout.Foldout(generatedPreviewFoldout, "Generated Preview", toggleOnLabelClick: true);
            if (!generatedPreviewFoldout)
            {
                return;
            }

            if (model == null)
            {
                return;
            }

            var entityLabel = model.HasMappedEntityId
                ? model.IsPreviewEntityId
                    ? $"Preview EntityId: {model.EntityId} (not persisted)"
                    : $"EntityId: {model.EntityId}"
                : "EntityId: (unavailable)";
            EditorGUILayout.LabelField("Generated Gameplay", entityLabel);
            EditorGUILayout.LabelField("Kind", model.Kind.ToString());
            EditorGUILayout.LabelField(
                "Cell",
                $"{model.Cell.face}({model.Cell.x},{model.Cell.y})");
            EditorGUILayout.LabelField(
                "Facing",
                $"{StageAuthoringFacingDisplayUtility.ToFacingLabel(model.Facing)} {StageAuthoringFacingDisplayUtility.ToFacingArrow(model.Facing)}");

            if (!model.RequiresBinding)
            {
                EditorGUILayout.HelpBox("No presentation binding required.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Binding Kind", model.BindingKindLabel);
            EditorGUILayout.LabelField(
                "Binding",
                model.HasMappedEntityId
                    ? $"{model.EntityId} -> {(string.IsNullOrEmpty(model.PresentationId) ? "(None)" : model.PresentationId)}"
                    : "(unavailable)");
            EditorGUILayout.HelpBox(model.StatusLabel, model.StatusMessageType);
        }

        private void DrawValidationIssueSummary(StageAuthoringGeneratedBindingPreviewModel model)
        {
            validationIssuesFoldout = EditorGUILayout.Foldout(validationIssuesFoldout, "Validation Issues", toggleOnLabelClick: true);
            if (!validationIssuesFoldout)
            {
                return;
            }

            if (model == null || model.RelatedIssueMessages.Length == 0)
            {
                EditorGUILayout.HelpBox("No selected placement issues in the last report.", MessageType.Info);
                return;
            }

            for (var i = 0; i < model.RelatedIssueMessages.Length; i++)
            {
                EditorGUILayout.HelpBox(model.RelatedIssueMessages[i], MessageType.Warning);
            }
        }

        private static void DrawReport(StageAuthoringGenerationReport report)
        {
            if (report == null)
            {
                return;
            }

            EditorGUILayout.Space();
            var messageType = report.HasErrors ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(
                report.HasErrors
                    ? "Stage authoring generation has errors."
                    : $"Stage authoring generation completed with {report.Issues.Count} issue(s).",
                messageType);

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                var issueType = issue.Severity switch
                {
                    StageValidationSeverity.Error => MessageType.Error,
                    StageValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info,
                };
                EditorGUILayout.HelpBox($"[{issue.Code}] {issue.Message}", issueType);
            }
        }
    }
}
