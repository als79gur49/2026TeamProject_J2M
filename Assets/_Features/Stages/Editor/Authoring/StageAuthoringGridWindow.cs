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
        private StageAuthoringEntityKind? focusedGridKind;

        internal StageAuthoringGenerationReport LastReportForTests => lastReport;

        internal FaceId TargetFaceForTests => selection.TargetFace;

        internal Vector2Int TargetCellForTests => selection.TargetCell;

        internal StageAuthoringEntityKind? FocusedGridKindForTests => focusedGridKind;

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
            ExecuteInspectorAction(
                StageAuthoringGridHotkeyHandler.HandleRotateHotkeys(
                    selectedPlacementIndex,
                    SupportsSelectedFacingAuthoring(selectedPlacementIndex)),
                selectedPlacementIndex);

            var toolbarResult = StageAuthoringGridToolbarRenderer.DrawHeader(selection.TargetFace);
            if (toolbarResult.FaceChanged)
            {
                selection.SetTargetFace(toolbarResult.NextFace);
            }

            ExecuteToolbarAction(toolbarResult.Action, selectedPlacementIndex);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            focusedGridKind = StageAuthoringGridRenderer.DrawGrid(authoring, selection, focusedGridKind);
            EditorGUILayout.Space();

            selectedPlacementIndex = selection.ResolveSelectedPlacementIndex(authoring.Placements);
            ExecuteToolbarAction(
                StageAuthoringGridToolbarRenderer.DrawSelectedCellTools(
                    authoring,
                    selection,
                    selectedPlacementIndex),
                selectedPlacementIndex);

            EditorGUILayout.Space();
            DrawSelectedPlacementInspector();
            StageAuthoringGridToolbarRenderer.DrawReport(lastReport);
            EditorGUILayout.EndScrollView();

            serializedAuthoring.ApplyModifiedProperties();
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
    }
}
