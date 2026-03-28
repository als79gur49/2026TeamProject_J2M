using System;
using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    public static class GameplayShowcaseSceneBuilder
    {
        private const string ActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string TraversalScenePath = "Assets/Scenes/CubeSurfaceTraversalShowcase.unity";
        private const string BoxScenePath = "Assets/Scenes/BoxInteractionShowcase.unity";

        [MenuItem("Tools/Gameplay/Build Showcase Scenes")]
        public static void BuildAllScenes()
        {
            BuildTraversalScene();
            BuildBoxInteractionScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildTraversalScene()
        {
            BuildScene<CubeSurfaceTraversalShowcaseInstaller>(
                TraversalScenePath,
                "Cube Surface Traversal Showcase",
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1.15f,
                CreateTraversalAnnotations);
        }

        public static void BuildBoxInteractionScene()
        {
            BuildScene<BoxInteractionShowcaseInstaller>(
                BoxScenePath,
                "Box Interaction Showcase",
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(15, 6)),
                1.15f,
                CreateBoxInteractionAnnotations);
        }

        private static void BuildScene<TInstaller>(
            string scenePath,
            string rootObjectName,
            BoardBounds boardBounds,
            float cellSize,
            Action<BoardBounds, float> addAnnotations)
            where TInstaller : MonoBehaviour
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var installerObject = new GameObject(rootObjectName);
            var installer = installerObject.AddComponent<TInstaller>();
            AssignActions(installer);

            addAnnotations?.Invoke(boardBounds, cellSize);

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void AssignActions(Component installer)
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
            if (actions == null)
            {
                throw new InvalidOperationException($"Missing InputActionAsset at '{ActionsAssetPath}'.");
            }

            var serializedObject = new SerializedObject(installer);
            serializedObject.FindProperty("actions").objectReferenceValue = actions;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTraversalAnnotations(BoardBounds boardBounds, float cellSize)
        {
            var gridOrigin = GameplayShowcaseSceneInstallerBase.CalculateCenteredGridOrigin(boardBounds, cellSize);
            CreateWorldLabel(
                "Move: WASD / Push: E / Flip: Q",
                new Vector3(0f, 6.4f, -0.25f),
                0.18f,
                72,
                TextAnchor.MiddleCenter);
            CreateWorldLabel(
                "Start on the top edge and press Up to rotate onto Front.",
                new Vector3(0f, 5.6f, -0.25f),
                0.14f,
                56,
                TextAnchor.MiddleCenter);
            CreateWorldLabel(
                "Traversal corridor",
                ProjectFloorCell(gridOrigin, boardBounds, cellSize, 2, 2) + new Vector3(0f, 0.9f, -0.25f),
                0.12f,
                48,
                TextAnchor.MiddleCenter);
        }

        private static void CreateBoxInteractionAnnotations(BoardBounds boardBounds, float cellSize)
        {
            var gridOrigin = GameplayShowcaseSceneInstallerBase.CalculateCenteredGridOrigin(boardBounds, cellSize);

            CreateWorldLabel(
                "Move: WASD / Push: E / Flip: Q",
                new Vector3(0f, 8.6f, -0.25f),
                0.18f,
                72,
                TextAnchor.MiddleCenter);
            CreateWorldLabel(
                "Push",
                ProjectFloorCell(gridOrigin, boardBounds, cellSize, 3, 1) + new Vector3(0f, 0.9f, -0.25f),
                0.14f,
                56,
                TextAnchor.MiddleCenter);
            CreateWorldLabel(
                "Push + Destroy",
                ProjectFloorCell(gridOrigin, boardBounds, cellSize, 3, 3) + new Vector3(0f, 0.9f, -0.25f),
                0.14f,
                56,
                TextAnchor.MiddleCenter);
            CreateWorldLabel(
                "Flip",
                ProjectFloorCell(gridOrigin, boardBounds, cellSize, 3, 5) + new Vector3(0f, 0.9f, -0.25f),
                0.14f,
                56,
                TextAnchor.MiddleCenter);
            CreateWorldLabel(
                "Push + Flip",
                ProjectFloorCell(gridOrigin, boardBounds, cellSize, 10, 1) + new Vector3(0f, 0.9f, -0.25f),
                0.14f,
                56,
                TextAnchor.MiddleCenter);
            CreateWorldLabel(
                "Push + Flip + Destroy",
                ProjectFloorCell(gridOrigin, boardBounds, cellSize, 10, 3) + new Vector3(0f, 0.9f, -0.25f),
                0.14f,
                56,
                TextAnchor.MiddleCenter);
        }

        private static Vector3 ProjectFloorCell(
            Vector3 gridOrigin,
            BoardBounds boardBounds,
            float cellSize,
            int x,
            int y)
        {
            return new Vector3(
                gridOrigin.x + ((x - boardBounds.MinInclusive.x) * cellSize),
                gridOrigin.y + ((y - boardBounds.MinInclusive.y) * cellSize),
                0f);
        }

        private static void CreateWorldLabel(
            string text,
            Vector3 position,
            float characterSize,
            int fontSize,
            TextAnchor anchor)
        {
            var labelObject = new GameObject($"Label_{text}");
            var textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = anchor;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = fontSize;
            textMesh.color = new Color(0.14f, 0.17f, 0.23f);

            labelObject.transform.position = position;
        }
    }
}
