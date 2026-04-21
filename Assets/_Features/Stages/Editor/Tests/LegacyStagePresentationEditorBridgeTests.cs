using System;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class LegacyStagePresentationEditorBridgeTests
    {
        [Test]
        public void Resolve_BuildsEnemyPresentationBindingsOnlyForEnemySpawnsWithIds()
        {
            var enemyProfile = ScriptableObject.CreateInstance<EnemyAiProfile>();
            try
            {
                var stage = CreateStage(
                    "EnemyPresentationBindings",
                    CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, facing: Direction.Right),
                    CreateSpawn(
                        20,
                        StageSpawnKind.Enemy,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        hp: 2,
                        enemyAiMode: EnemyAiMode.Patrol,
                        enemyAiProfile: enemyProfile,
                        presentationId: "  windup_enemy  "),
                    CreateSpawn(
                        21,
                        StageSpawnKind.Enemy,
                        new SurfaceCell(FaceId.Floor, 2, 1),
                        hp: 2,
                        enemyAiMode: EnemyAiMode.Patrol,
                        enemyAiProfile: enemyProfile,
                        presentationId: " "),
                    CreateSpawn(
                        30,
                        StageSpawnKind.Box,
                        new SurfaceCell(FaceId.Floor, 1, 2),
                        hp: 1,
                        presentationId: "ignored_box_presentation"));

                try
                {
                    var presentation = LegacyStagePresentationEditorBridge.Resolve(stage);
                    Assert.That(presentation.EnemyPresentationBindings.Length, Is.EqualTo(1));
                    Assert.That(presentation.EnemyPresentationBindings[0].EntityId, Is.EqualTo(20));
                    Assert.That(presentation.EnemyPresentationBindings[0].PresentationId, Is.EqualTo("windup_enemy"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(stage);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyProfile);
            }
        }

        [Test]
        public void Resolve_BuildsStaticPresentationBindingsOnlyForBoxAndWallSpawnsWithIds()
        {
            var stage = CreateStage(
                "StaticPresentationBindings",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, facing: Direction.Right),
                CreateSpawn(30, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1, presentationId: "  box_variant  "),
                CreateSpawn(40, StageSpawnKind.Wall, new SurfaceCell(FaceId.Front, 2, 1), hp: 1, facing: Direction.None, presentationId: "wall_variant"),
                CreateSpawn(50, StageSpawnKind.Box, new SurfaceCell(FaceId.Ceiling, 1, 2), hp: 1, presentationId: " "),
                CreateSpawn(60, StageSpawnKind.Enemy, new SurfaceCell(FaceId.Floor, 2, 2), hp: 2, enemyAiMode: EnemyAiMode.Patrol, presentationId: "enemy_variant"));

            try
            {
                var presentation = LegacyStagePresentationEditorBridge.Resolve(stage);
                Assert.That(presentation.StaticEntityPresentationBindings.Length, Is.EqualTo(2));
                Assert.That(presentation.StaticEntityPresentationBindings[0].EntityId, Is.EqualTo(30));
                Assert.That(presentation.StaticEntityPresentationBindings[0].PresentationId, Is.EqualTo("box_variant"));
                Assert.That(presentation.StaticEntityPresentationBindings[1].EntityId, Is.EqualTo(40));
                Assert.That(presentation.StaticEntityPresentationBindings[1].PresentationId, Is.EqualTo("wall_variant"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static StageDefinition CreateStage(
            string stageName,
            StageBoardDefinition board,
            params StageSpawnDefinition[] spawns)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.name = stageName;
            SetPrivateField(stage, "board", board);
            SetPrivateField(stage, "playerSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Player));
            SetPrivateField(stage, "boxSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Box));
            SetPrivateField(stage, "enemySpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Enemy));
            SetPrivateField(stage, "wallSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Wall));
            return stage;
        }

        private static StageBoardDefinition CreateBoard(
            Vector2Int minInclusive,
            Vector2Int maxInclusive,
            FaceId initialBottomFace = FaceId.Floor)
        {
            return new StageBoardDefinition
            {
                MinInclusive = minInclusive,
                MaxInclusive = maxInclusive,
                InitialBottomFace = initialBottomFace,
            };
        }

        private static StageSpawnDefinition CreateSpawn(
            int entityId,
            StageSpawnKind kind,
            SurfaceCell cell,
            int hp,
            Direction facing = Direction.Right,
            BoxCapabilities boxCapabilities = BoxCapabilities.None,
            EnemyAiMode enemyAiMode = EnemyAiMode.None,
            int enemyAiStateTimer = 0,
            EnemyAiProfile enemyAiProfile = null,
            string presentationId = null,
            string unitStackGroup = null)
        {
            return new StageSpawnDefinition
            {
                EntityId = entityId,
                Kind = kind,
                Cell = cell,
                Facing = facing,
                Hp = hp,
                BoxCapabilities = boxCapabilities,
                EnemyAiMode = enemyAiMode,
                EnemyAiStateTimer = enemyAiStateTimer,
                EnemyAiProfile = enemyAiProfile,
                PresentationId = presentationId,
                UnitStackGroup = unitStackGroup,
            };
        }

        private static StageSpawnDefinition[] FilterSpawnsByKind(StageSpawnDefinition[] spawns, StageSpawnKind kind)
        {
            if (spawns == null || spawns.Length == 0)
            {
                return Array.Empty<StageSpawnDefinition>();
            }

            var filtered = new System.Collections.Generic.List<StageSpawnDefinition>();
            for (var i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].Kind == kind)
                {
                    filtered.Add(spawns[i]);
                }
            }

            return filtered.ToArray();
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
