using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageDefinitionMobilityValidationTests
    {
        [Test]
        public void StageValidation_InvalidPlayerMobility_ReportsError()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            try
            {
                SetStage(stage, CreateSpawn(10, StageSpawnKind.Player, 0, 0, (UnitMobilityKind)99));

                var exception = Assert.Throws<InvalidOperationException>(() => StageDefinitionValidator.Validate(stage));

                Assert.That(exception.Message, Does.Contain("invalid UnitMobilityKind"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void StageValidation_InvalidEnemyMobility_ReportsError()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            try
            {
                SetStage(
                    stage,
                    CreateSpawn(10, StageSpawnKind.Player, 0, 0),
                    CreateSpawn(20, StageSpawnKind.Enemy, 1, 0, (UnitMobilityKind)99));

                var exception = Assert.Throws<InvalidOperationException>(() => StageDefinitionValidator.Validate(stage));

                Assert.That(exception.Message, Does.Contain("invalid UnitMobilityKind"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void StageValidation_MissingMobility_DefaultGround_IsValid()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            try
            {
                SetStage(
                    stage,
                    CreateSpawn(10, StageSpawnKind.Player, 0, 0),
                    CreateSpawn(20, StageSpawnKind.Enemy, 1, 0));

                Assert.DoesNotThrow(() => StageDefinitionValidator.Validate(stage));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static StageSpawnDefinition CreateSpawn(
            int entityId,
            StageSpawnKind kind,
            int x,
            int y,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return new StageSpawnDefinition
            {
                EntityId = entityId,
                Kind = kind,
                Cell = new SurfaceCell(FaceId.Floor, x, y),
                Facing = Direction.Right,
                Hp = 1,
                UnitMobilityKind = unitMobilityKind,
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
            SetSpawnArray(serializedObject.FindProperty("playerSpawns"), spawns, StageSpawnKind.Player);
            SetSpawnArray(serializedObject.FindProperty("enemySpawns"), spawns, StageSpawnKind.Enemy);
            SetSpawnArray(serializedObject.FindProperty("boxSpawns"), spawns, StageSpawnKind.Box);
            SetSpawnArray(serializedObject.FindProperty("wallSpawns"), spawns, StageSpawnKind.Wall);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSpawnArray(
            SerializedProperty property,
            StageSpawnDefinition[] spawns,
            StageSpawnKind kind)
        {
            var count = 0;
            for (var i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].Kind == kind)
                {
                    count++;
                }
            }

            property.arraySize = count;
            var writeIndex = 0;
            for (var i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].Kind != kind)
                {
                    continue;
                }

                var element = property.GetArrayElementAtIndex(writeIndex++);
                element.FindPropertyRelative("EntityId").intValue = spawns[i].EntityId;
                element.FindPropertyRelative("Kind").intValue = (int)spawns[i].Kind;
                var cell = element.FindPropertyRelative("Cell");
                cell.FindPropertyRelative("face").intValue = (int)spawns[i].Cell.face;
                cell.FindPropertyRelative("x").intValue = spawns[i].Cell.x;
                cell.FindPropertyRelative("y").intValue = spawns[i].Cell.y;
                element.FindPropertyRelative("Facing").intValue = (int)spawns[i].Facing;
                element.FindPropertyRelative("Hp").intValue = spawns[i].Hp;
                element.FindPropertyRelative("UnitMobilityKind").intValue = (int)spawns[i].UnitMobilityKind;
                element.FindPropertyRelative("BoxCapabilities").intValue = (int)spawns[i].BoxCapabilities;
                element.FindPropertyRelative("EnemyAiMode").intValue = (int)spawns[i].EnemyAiMode;
            }
        }
    }
}
