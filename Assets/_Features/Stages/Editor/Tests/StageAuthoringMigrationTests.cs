using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringMigrationTests
    {
        [Test]
        public void MigrationPreservesExistingEntityIds()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();

            try
            {
                SetStage(
                    stage,
                    CreateSpawn(10, StageSpawnKind.Player, 0, 0),
                    CreateSpawn(20, StageSpawnKind.Enemy, 1, 0),
                    CreateSpawn(30, StageSpawnKind.Box, 2, 0));

                StageAuthoringMigrationTool.PopulateFromOutputs(
                    authoring,
                    StageId.CreateOrThrow("migration-stage"),
                    stage,
                    presentation,
                    overwriteGeneratedReferences: true);

                CollectionAssert.AreEquivalent(
                    new[] { 10, 20, 30 },
                    authoring.EntityIdMappings.Select(mapping => mapping.EntityId).ToArray());

                var report = StageAuthoringGenerator.Generate(authoring, stage, presentation, StageAuthoringGenerateOptions.WriteAll);
                Assert.That(report.HasErrors, Is.False, FormatIssues(report));
                CollectionAssert.AreEquivalent(new[] { 10, 20, 30 }, stage.Spawns.Select(spawn => spawn.EntityId).ToArray());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(presentation);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void MigrationCopiesPresentationBindingByEntityId()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();

            try
            {
                SetStage(
                    stage,
                    CreateSpawn(10, StageSpawnKind.Player, 0, 0),
                    CreateSpawn(20, StageSpawnKind.Enemy, 1, 0));
                SetEnemyBindings(presentation, new EnemyPresentationBinding
                {
                    EntityId = 20,
                    PresentationId = "enemy-view",
                });

                StageAuthoringMigrationTool.PopulateFromOutputs(
                    authoring,
                    StageId.CreateOrThrow("migration-stage"),
                    stage,
                    presentation,
                    overwriteGeneratedReferences: true);

                var enemyPlacement = authoring.Placements.Single(placement => placement.Kind == StageAuthoringEntityKind.Enemy);
                Assert.That(enemyPlacement.PresentationId, Is.EqualTo("enemy-view"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(presentation);
                UnityEngine.Object.DestroyImmediate(authoring);
            }
        }

        private static StageSpawnDefinition CreateSpawn(
            int entityId,
            StageSpawnKind kind,
            int x,
            int y)
        {
            return new StageSpawnDefinition
            {
                EntityId = entityId,
                Kind = kind,
                Cell = new SurfaceCell(FaceId.Floor, x, y),
                Facing = Direction.Right,
                Hp = 1,
                BoxCapabilities = BoxCapabilities.Push,
                EnemyAiMode = kind == StageSpawnKind.Enemy ? EnemyAiMode.Patrol : EnemyAiMode.None,
            };
        }

        private static void SetStage(StageDefinition stage, params StageSpawnDefinition[] spawns)
        {
            var serializedObject = new SerializedObject(stage);
            var board = serializedObject.FindProperty("board");
            board.FindPropertyRelative("MinInclusive").vector2IntValue = new Vector2Int(0, 0);
            board.FindPropertyRelative("MaxInclusive").vector2IntValue = new Vector2Int(4, 4);
            board.FindPropertyRelative("InitialBottomFace").intValue = (int)FaceId.Floor;
            SetSpawnArray(serializedObject.FindProperty("playerSpawns"), spawns.Where(spawn => spawn.Kind == StageSpawnKind.Player).ToArray());
            SetSpawnArray(serializedObject.FindProperty("enemySpawns"), spawns.Where(spawn => spawn.Kind == StageSpawnKind.Enemy).ToArray());
            SetSpawnArray(serializedObject.FindProperty("boxSpawns"), spawns.Where(spawn => spawn.Kind == StageSpawnKind.Box).ToArray());
            SetSpawnArray(serializedObject.FindProperty("wallSpawns"), spawns.Where(spawn => spawn.Kind == StageSpawnKind.Wall).ToArray());
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSpawnArray(SerializedProperty property, StageSpawnDefinition[] spawns)
        {
            property.arraySize = spawns.Length;
            for (var i = 0; i < spawns.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EntityId").intValue = spawns[i].EntityId;
                element.FindPropertyRelative("Kind").intValue = (int)spawns[i].Kind;
                var cell = element.FindPropertyRelative("Cell");
                cell.FindPropertyRelative("face").intValue = (int)spawns[i].Cell.face;
                cell.FindPropertyRelative("x").intValue = spawns[i].Cell.x;
                cell.FindPropertyRelative("y").intValue = spawns[i].Cell.y;
                element.FindPropertyRelative("Facing").intValue = (int)spawns[i].Facing;
                element.FindPropertyRelative("Hp").intValue = spawns[i].Hp;
                element.FindPropertyRelative("BoxCapabilities").intValue = (int)spawns[i].BoxCapabilities;
                element.FindPropertyRelative("EnemyAiMode").intValue = (int)spawns[i].EnemyAiMode;
                element.FindPropertyRelative("EnemyAiStateTimer").intValue = spawns[i].EnemyAiStateTimer;
                element.FindPropertyRelative("EnemyAiProfile").objectReferenceValue = spawns[i].EnemyAiProfile;
                element.FindPropertyRelative("PresentationId").stringValue = string.Empty;
                element.FindPropertyRelative("UnitStackGroup").stringValue = string.Empty;
            }
        }

        private static void SetEnemyBindings(
            StagePresentationDefinition presentation,
            params EnemyPresentationBinding[] bindings)
        {
            var serializedObject = new SerializedObject(presentation);
            var property = serializedObject.FindProperty("enemyPresentationBindings");
            property.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EntityId").intValue = bindings[i].EntityId;
                element.FindPropertyRelative("PresentationId").stringValue = bindings[i].PresentationId;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string FormatIssues(StageAuthoringGenerationReport report)
        {
            return string.Join(Environment.NewLine, report.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }
    }
}
