using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public sealed class StageAuthoringGridWindow : EditorWindow
    {
        private StageAuthoringDefinition authoring;
        private SerializedObject serializedAuthoring;
        private FaceId selectedFace = FaceId.Floor;
        private Vector2Int selectedCell;
        private int selectedPlacementIndex = -1;
        private Vector2 scroll;

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
            selectedPlacementIndex = -1;
        }

        private void OnGUI()
        {
            if (authoring == null)
            {
                EditorGUILayout.HelpBox("Select a StageAuthoringDefinition.", MessageType.Info);
                return;
            }

            serializedAuthoring.Update();
            using (new EditorGUILayout.HorizontalScope())
            {
                selectedFace = (FaceId)EditorGUILayout.EnumPopup("Face", selectedFace);
                if (GUILayout.Button("Generate", GUILayout.Width(96)))
                {
                    StageAuthoringGenerator.Generate(authoring, StageAuthoringGenerateOptions.WriteAll);
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawGrid();
            EditorGUILayout.Space();
            DrawSelectedCellTools();
            EditorGUILayout.Space();
            DrawSelectedPlacementInspector();
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
                        var index = FindPlacementAt(selectedFace, x, y);
                        var marker = index >= 0
                            ? StageAuthoringGridRenderer.GetMarker(authoring.Placements[index])
                            : ".";
                        if (selectedCell.x == x && selectedCell.y == y)
                        {
                            marker = $"[{marker}]";
                        }

                        if (GUILayout.Button(marker, GUILayout.Width(36), GUILayout.Height(28)))
                        {
                            selectedCell = new Vector2Int(x, y);
                            selectedPlacementIndex = index;
                        }
                    }
                }
            }
        }

        private void DrawSelectedCellTools()
        {
            EditorGUILayout.LabelField("Selected Cell", $"{selectedFace}({selectedCell.x},{selectedCell.y})");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Placement"))
                {
                    AddPlacementAtSelectedCell();
                }

                using (new EditorGUI.DisabledScope(selectedPlacementIndex < 0))
                {
                    if (GUILayout.Button("Move Selected Here"))
                    {
                        MoveSelectedPlacementToSelectedCell();
                    }

                    if (GUILayout.Button("Delete Selected"))
                    {
                        DeleteSelectedPlacement();
                    }
                }
            }
        }

        private void DrawSelectedPlacementInspector()
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= placementsProperty.arraySize)
            {
                EditorGUILayout.HelpBox("No placement selected.", MessageType.Info);
                return;
            }

            var element = placementsProperty.GetArrayElementAtIndex(selectedPlacementIndex);
            EditorGUILayout.PropertyField(element, includeChildren: true);
            DrawPresentationDropdown(element);
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

        private int FindPlacementAt(FaceId face, int x, int y)
        {
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
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

        private void AddPlacementAtSelectedCell()
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            placementsProperty.InsertArrayElementAtIndex(placementsProperty.arraySize);
            selectedPlacementIndex = placementsProperty.arraySize - 1;
            var element = placementsProperty.GetArrayElementAtIndex(selectedPlacementIndex);
            element.FindPropertyRelative("StableGuid").stringValue = System.Guid.NewGuid().ToString("N");
            element.FindPropertyRelative("DisplayName").stringValue = "Placement";
            element.FindPropertyRelative("Kind").intValue = (int)StageAuthoringEntityKind.Box;
            var cellProperty = element.FindPropertyRelative("Cell");
            cellProperty.FindPropertyRelative("face").intValue = (int)selectedFace;
            cellProperty.FindPropertyRelative("x").intValue = selectedCell.x;
            cellProperty.FindPropertyRelative("y").intValue = selectedCell.y;
            element.FindPropertyRelative("Facing").intValue = (int)Direction.None;
            element.FindPropertyRelative("Hp").intValue = 1;
            element.FindPropertyRelative("UnitStackGroup").stringValue = string.Empty;
            element.FindPropertyRelative("BoxCapabilities").intValue = (int)BoxCapabilities.None;
            element.FindPropertyRelative("EnemyAiMode").intValue = 0;
            element.FindPropertyRelative("EnemyAiStateTimer").intValue = 0;
            element.FindPropertyRelative("EnemyAiProfileOverride").objectReferenceValue = null;
            element.FindPropertyRelative("PresentationId").stringValue = string.Empty;
        }

        private void MoveSelectedPlacementToSelectedCell()
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return;
            }

            var cellProperty = placementsProperty
                .GetArrayElementAtIndex(selectedPlacementIndex)
                .FindPropertyRelative("Cell");
            cellProperty.FindPropertyRelative("face").intValue = (int)selectedFace;
            cellProperty.FindPropertyRelative("x").intValue = selectedCell.x;
            cellProperty.FindPropertyRelative("y").intValue = selectedCell.y;
        }

        private void DeleteSelectedPlacement()
        {
            var placementsProperty = serializedAuthoring.FindProperty("placements");
            if (selectedPlacementIndex < 0 || selectedPlacementIndex >= placementsProperty.arraySize)
            {
                return;
            }

            placementsProperty.DeleteArrayElementAtIndex(selectedPlacementIndex);
            selectedPlacementIndex = -1;
        }
    }
}
