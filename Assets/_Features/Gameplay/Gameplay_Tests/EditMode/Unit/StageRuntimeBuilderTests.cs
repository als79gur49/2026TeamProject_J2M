using System;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StageRuntimeBuilderTests
    {
        private const string CombinedStageAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Stage_CombinedGameplayShowcase.asset";
        private const int ConfiguredShowcaseEnemyId = 54;
        private const int NonAttackingShowcaseEnemyId = 55;
        private const int WallFollowerShowcaseEnemyId = 56;
        private const string AttackingEnemyPresentationId = "Attacking_showcase";
        private const string NonAttackingEnemyPresentationId = "nonAttacking_showcase";
        private const string WindupEnemyPresentationId = "windup_melee_showcase";

        [Test]
        public void StageRuntimeBuilder_DuplicateEntityIdRejects()
        {
            var stage = CreateStage(
                "DuplicateEntityId",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2), perimeterFaces: Array.Empty<FaceId>()),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(10, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 2), hp: 1));

            AssertBuildThrows(stage, "duplicate entity id 10");
        }

        [Test]
        public void StageRuntimeBuilder_DuplicateCellRejects()
        {
            var stage = CreateStage(
                "DuplicateCell",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2), perimeterFaces: Array.Empty<FaceId>()),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(30, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1));

            AssertBuildThrows(stage, "duplicate occupied cell");
        }

        [Test]
        public void StageRuntimeBuilder_OutOfBoundsRejects()
        {
            var stage = CreateStage(
                "OutOfBounds",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(1, 1), perimeterFaces: Array.Empty<FaceId>()),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 3, 3), hp: 3, facing: Direction.Up));

            AssertBuildThrows(stage, "outside the configured board bounds");
        }

        [Test]
        public void StageRuntimeBuilder_ZeroPlayerRejects()
        {
            var stage = CreateStage(
                "ZeroPlayer",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2), perimeterFaces: Array.Empty<FaceId>()),
                CreateSpawn(30, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1));

            AssertBuildThrows(stage, "must contain exactly one player spawn, but found none");
        }

        [Test]
        public void StageRuntimeBuilder_MultiplePlayersRejects()
        {
            var stage = CreateStage(
                "MultiplePlayers",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2), perimeterFaces: Array.Empty<FaceId>()),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, facing: Direction.Up),
                CreateSpawn(11, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 2, 2), hp: 3, facing: Direction.Right));

            AssertBuildThrows(stage, "must contain exactly one player spawn, but found 2");
        }

        [Test]
        public void StageRuntimeBuilder_GeneratedWallIdCollisionRejects()
        {
            var stage = CreateStage(
                "GeneratedWallCollision",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2), perimeterFaces: new[] { FaceId.Floor }, generatedPerimeterWallEntityIdStart: 100),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(100, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 2), hp: 1));

            AssertBuildThrows(stage, "collides with a generated perimeter wall entity id");
        }

        [Test]
        public void StageRuntimeBuilder_GroupKindMismatchRejects()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();

            try
            {
                stage.name = "GroupKindMismatch";
                SetPrivateField(stage, "board", CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2), perimeterFaces: Array.Empty<FaceId>()));
                SetPrivateField(stage, "playerSpawns", new[]
                {
                    CreateSpawn(10, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1),
                });

                var exception = Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(stage));
                StringAssert.Contains("playerSpawns[0] must use Kind Player", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");
            Assert.That(stage.PlayerSpawns.Length, Is.EqualTo(1));
            Assert.That(stage.BoxSpawns.Length, Is.EqualTo(12));
            Assert.That(stage.EnemySpawns.Length, Is.EqualTo(3));
            Assert.That(stage.WallSpawns.Length, Is.EqualTo(5));

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.BoardBounds.MinInclusive, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(buildResult.BoardBounds.MaxInclusive, Is.EqualTo(new Vector2Int(15, 8)));
            Assert.That(buildResult.InitialTopology.BottomFace, Is.EqualTo(FaceId.Floor));
            Assert.That(buildResult.PlayerEntityId, Is.EqualTo(10));
            Assert.That(buildResult.InitialTerrain, Is.SameAs(Game.Feature.Gameplay.BoardState.TerrainData.Empty));
            AssertEntityIdsAreSorted(buildResult.InitialEntities);

            Assert.That(TryGetEntity(buildResult.InitialEntities, 10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(player.facing, Is.EqualTo(Direction.Right));

            Assert.That(TryGetEntity(buildResult.InitialEntities, 196, out var floorWall), Is.True);
            Assert.That(floorWall.type, Is.EqualTo(EntityType.None));
            Assert.That(floorWall.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 5, 1)));

            Assert.That(TryGetEntity(buildResult.InitialEntities, 200, out var frontWall), Is.True);
            Assert.That(frontWall.type, Is.EqualTo(EntityType.None));
            Assert.That(frontWall.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 9, 3)));

            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 1, 0)), Is.False);
            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Front, 7, buildResult.BoardBounds.MaxInclusive.y)), Is.False);
            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Ceiling, 2, buildResult.BoardBounds.MaxInclusive.y)), Is.True);

            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 9, 6)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Front, 3, 1)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Ceiling, 7, 1)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Back, 3, 5)), Is.True);

            Assert.That(TryGetEntity(buildResult.InitialEntities, ConfiguredShowcaseEnemyId, out var showcaseEnemy), Is.True);
            Assert.That(showcaseEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 6, 2)));
            Assert.That(showcaseEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(showcaseEnemy.hp, Is.EqualTo(3));

            Assert.That(TryGetEntity(buildResult.InitialEntities, NonAttackingShowcaseEnemyId, out var nonAttackingEnemy), Is.True);
            Assert.That(nonAttackingEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 7, 2)));
            Assert.That(nonAttackingEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));

            Assert.That(TryGetEntity(buildResult.InitialEntities, WallFollowerShowcaseEnemyId, out var wallFollowerEnemy), Is.True);
            Assert.That(wallFollowerEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 10, 6)));
            Assert.That(wallFollowerEnemy.facing, Is.EqualTo(Direction.Up));
            Assert.That(wallFollowerEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));

            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(3));
            Assert.That(TryGetProfileOverride(buildResult, ConfiguredShowcaseEnemyId, out var showcaseProfile), Is.True);
            Assert.That(showcaseProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(showcaseProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
            Assert.That(showcaseProfile.AttackTimingSettings.WindupSeconds, Is.EqualTo(1f));
            Assert.That(showcaseProfile.LocomotionTimingSettings.MoveCooldownSeconds, Is.EqualTo(1f));

            Assert.That(TryGetProfileOverride(buildResult, NonAttackingShowcaseEnemyId, out var nonAttackingProfile), Is.True);
            Assert.That(nonAttackingProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));

            Assert.That(TryGetProfileOverride(buildResult, WallFollowerShowcaseEnemyId, out var wallFollowerProfile), Is.True);
            Assert.That(wallFollowerProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(wallFollowerProfile.DetectionStrategyKind, Is.EqualTo(DetectionStrategyKind.None));
            Assert.That(wallFollowerProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));

            Assert.That(buildResult.EnemyPresentationBindings.Length, Is.EqualTo(3));
            Assert.That(TryGetPresentationBinding(buildResult, ConfiguredShowcaseEnemyId, out var presentationBinding), Is.True);
            Assert.That(presentationBinding.PresentationId, Is.EqualTo(AttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(buildResult, NonAttackingShowcaseEnemyId, out var nonAttackingBinding), Is.True);
            Assert.That(nonAttackingBinding.PresentationId, Is.EqualTo(NonAttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(buildResult, WallFollowerShowcaseEnemyId, out var wallFollowerBinding), Is.True);
            Assert.That(wallFollowerBinding.PresentationId, Is.EqualTo(NonAttackingEnemyPresentationId));
        }

        [Test]
        public void StageRuntimeBuilder_BuildsEnemyPresentationBindingsOnlyForEnemySpawnsWithIds()
        {
            var enemyProfile = ScriptableObject.CreateInstance<EnemyAiProfile>();

            try
            {
                var stage = CreateStage(
                    "EnemyPresentationBindings",
                    CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2), perimeterFaces: Array.Empty<FaceId>()),
                    CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, facing: Direction.Right),
                    CreateSpawn(
                        20,
                        StageSpawnKind.Enemy,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        hp: 2,
                        enemyAiMode: EnemyAiMode.Patrol,
                        enemyAiProfile: enemyProfile,
                        enemyPresentationId: $"  {WindupEnemyPresentationId}  "),
                    CreateSpawn(
                        21,
                        StageSpawnKind.Enemy,
                        new SurfaceCell(FaceId.Floor, 2, 1),
                        hp: 2,
                        enemyAiMode: EnemyAiMode.Patrol,
                        enemyAiProfile: enemyProfile,
                        enemyPresentationId: " "),
                    CreateSpawn(
                        30,
                        StageSpawnKind.Box,
                        new SurfaceCell(FaceId.Floor, 1, 2),
                        hp: 1,
                        enemyPresentationId: "ignored_box_presentation"));

                try
                {
                    var buildResult = StageRuntimeBuilder.Build(stage);
                    Assert.That(buildResult.EnemyPresentationBindings.Length, Is.EqualTo(1));
                    Assert.That(buildResult.EnemyPresentationBindings[0].EntityId, Is.EqualTo(20));
                    Assert.That(buildResult.EnemyPresentationBindings[0].PresentationId, Is.EqualTo(WindupEnemyPresentationId));
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
            FaceId[] perimeterFaces = null,
            int[] sharedEdgeOpeningColumns = null,
            int generatedPerimeterWallEntityIdStart = 100,
            FaceId initialBottomFace = FaceId.Floor)
        {
            return new StageBoardDefinition
            {
                MinInclusive = minInclusive,
                MaxInclusive = maxInclusive,
                InitialBottomFace = initialBottomFace,
                PerimeterFaces = perimeterFaces,
                SharedEdgeOpeningColumns = sharedEdgeOpeningColumns,
                GeneratedPerimeterWallEntityIdStart = generatedPerimeterWallEntityIdStart,
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
            string enemyPresentationId = null)
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
                EnemyPresentationId = enemyPresentationId,
            };
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static StageSpawnDefinition[] FilterSpawnsByKind(
            StageSpawnDefinition[] spawns,
            StageSpawnKind kind)
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

        private static void AssertBuildThrows(StageDefinition stage, string expectedMessage)
        {
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(stage));
                StringAssert.Contains(expectedMessage, exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static void AssertEntityIdsAreSorted(EntityState[] entities)
        {
            for (var i = 1; i < entities.Length; i++)
            {
                Assert.That(
                    entities[i - 1].entityId,
                    Is.LessThan(entities[i].entityId),
                    $"Entity list must be sorted by entity id ascending, but {entities[i - 1].entityId} appears before {entities[i].entityId}.");
            }
        }

        private static bool TryGetEntity(EntityState[] entities, int entityId, out EntityState entity)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                if (entities[i].entityId != entityId)
                {
                    continue;
                }

                entity = entities[i];
                return true;
            }

            entity = default;
            return false;
        }

        private static bool HasWallAt(EntityState[] entities, SurfaceCell cell)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                if (entities[i].type == EntityType.None && entities[i].position == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPushableBoxAt(EntityState[] entities, SurfaceCell cell)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                if (entities[i].type == EntityType.Box &&
                    entities[i].position == cell &&
                    (entities[i].boxCapabilities & BoxCapabilities.Push) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetProfileOverride(
            StageRuntimeBuildResult buildResult,
            int entityId,
            out EnemyAiProfile profile)
        {
            for (var i = 0; i < buildResult.EnemyAiProfileOverrides.Length; i++)
            {
                var entry = buildResult.EnemyAiProfileOverrides[i];
                if (entry.EntityId != entityId)
                {
                    continue;
                }

                profile = entry.Profile;
                return profile != null;
            }

            profile = null;
            return false;
        }

        private static bool TryGetPresentationBinding(
            StageRuntimeBuildResult buildResult,
            int entityId,
            out EnemyPresentationBinding binding)
        {
            for (var i = 0; i < buildResult.EnemyPresentationBindings.Length; i++)
            {
                var entry = buildResult.EnemyPresentationBindings[i];
                if (entry.EntityId != entityId)
                {
                    continue;
                }

                binding = entry;
                return true;
            }

            binding = default;
            return false;
        }
    }
}
