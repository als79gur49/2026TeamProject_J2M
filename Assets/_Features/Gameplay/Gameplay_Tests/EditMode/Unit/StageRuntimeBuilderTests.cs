using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StageRuntimeBuilderTests
    {
        private const string CombinedStageAssetPath =
            "Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase.asset";
        private const string CombinedPresentationAssetPath =
            "Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase_Presentation.asset";
        private const string TutorialStageAssetPath =
            "Assets/_Features/Stages/Content/tutorial-scene/tutorial-scene.asset";
        private const string TutorialEnemyProfileAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_Common/EnemyAi_TutorialPassiveContact.asset";
        private const int ConfiguredShowcaseEnemyId = 54;
        private const int NonAttackingShowcaseEnemyId = 55;
        private const int WallFollowerShowcaseEnemyId = 56;
        private const int JumpShowcaseEnemyId = 57;
        private const int ChargeShowcaseEnemyId = 58;
        private const int UtilitySummonerShowcaseEnemyId = 59;
        private const int TutorialEnemyId = 101;
        private const string AttackingEnemyPresentationId = "Attacking_showcase";
        private const string NonAttackingEnemyPresentationId = "nonAttacking_showcase";
        private const string WallFollowerEnemyPresentationId = "wallFollower_sun";
        private const string JumpChaserEnemyPresentationId = "jumpChaser_astra";
        private const string ChargeEnemyPresentationId = "Charge_showcase";
        private const string WindupEnemyPresentationId = "windup_melee_showcase";
        private const string UtilitySummonerEnemyPresentationId = "utility_summoner_prefab";

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_DuplicateEntityIdRejects()
        {
            var stage = CreateStage(
                "DuplicateEntityId",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(10, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 2), hp: 1));

            AssertBuildThrows(stage, "duplicate entity id 10");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_InitialTileFeatures_DefaultsToEmpty()
        {
            var stage = CreateStage(
                "InitialTileFeaturesEmpty",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.InitialTileFeatures, Is.Empty);
                Assert.That(buildResult.TileFeatureDefinitions, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_DefaultBoxArchetype_IsNormal()
        {
            var stage = CreateStage(
                "DefaultBoxArchetype",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(20, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 2), hp: 1));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(TryGetEntity(buildResult.InitialEntities, 20, out var box), Is.True);
                Assert.That(box.boxArchetype, Is.EqualTo(BoxArchetype.Normal));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_MoonBoxArchetype_Materializes()
        {
            var stage = CreateStage(
                "MoonBoxArchetype",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(
                    20,
                    StageSpawnKind.Box,
                    new SurfaceCell(FaceId.Floor, 1, 2),
                    hp: 1,
                    boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                    boxArchetype: BoxArchetype.Moon));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(TryGetEntity(buildResult.InitialEntities, 20, out var box), Is.True);
                Assert.That(box.boxArchetype, Is.EqualTo(BoxArchetype.Moon));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_TileFeatureAuthoring_MaterializesStateAndStaticDefinitions()
        {
            var tileFeature = CreateTileFeature(
                100,
                new SurfaceCell(FaceId.Floor, 1, 2),
                TileFeatureKind.Slide,
                TileFeatureActivationRule.ActiveFaceOnly,
                Direction2D.Right,
                TileFeatureBoxSelector.BoundEntity,
                boundEntityId: 30,
                presentationKey: "slide-east");
            var stage = CreateStage(
                "TileFeatureAuthoring",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up));
            SetPrivateField(stage, "tileFeatures", new[] { tileFeature });

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.InitialTileFeatures.Length, Is.EqualTo(1));
                Assert.That(buildResult.InitialTileFeatures[0].TileId, Is.EqualTo(tileFeature.TileId));
                Assert.That(buildResult.InitialTileFeatures[0].Cell, Is.EqualTo(tileFeature.Cell));
                Assert.That(buildResult.InitialTileFeatures[0].Kind, Is.EqualTo(tileFeature.Kind));
                Assert.That(buildResult.InitialTileFeatures[0].Flags, Is.EqualTo(TileFeatureFlags.None));
                Assert.That(buildResult.InitialTileFeatures[0].Charges, Is.EqualTo(0));

                Assert.That(buildResult.TileFeatureDefinitions.Length, Is.EqualTo(1));
                Assert.That(buildResult.TileFeatureDefinitions[0].TileId, Is.EqualTo(tileFeature.TileId));
                Assert.That(buildResult.TileFeatureDefinitions[0].ActivationRule, Is.EqualTo(tileFeature.ActivationRule));
                Assert.That(buildResult.TileFeatureDefinitions[0].Direction, Is.EqualTo(tileFeature.Direction));
                Assert.That(buildResult.TileFeatureDefinitions[0].BoxSelector, Is.EqualTo(tileFeature.BoxSelector));
                Assert.That(buildResult.TileFeatureDefinitions[0].BoundEntityId, Is.EqualTo(tileFeature.BoundEntityId));
                Assert.That(buildResult.TileFeatureDefinitions[0].PresentationKey, Is.EqualTo(tileFeature.PresentationKey));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_DuplicateTileIdRejects()
        {
            var stage = CreateStageWithTileFeatures(
                "DuplicateTileId",
                new[]
                {
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button),
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 2), TileFeatureKind.Destroy),
                });

            AssertBuildThrows(stage, "duplicate tile feature id 100");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_NonPositiveTileIdRejects()
        {
            var stage = CreateStageWithTileFeatures(
                "NonPositiveTileId",
                new[] { CreateTileFeature(0, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button) });

            AssertBuildThrows(stage, "must use a positive tile id");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_OutOfBoundsTileFeatureRejects()
        {
            var stage = CreateStageWithTileFeatures(
                "OutOfBoundsTileFeature",
                new[] { CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 5, 1), TileFeatureKind.Button) });

            AssertBuildThrows(stage, "outside the configured board bounds");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_UnknownTileFeatureKindRejects()
        {
            var stage = CreateStageWithTileFeatures(
                "UnknownTileFeatureKind",
                new[] { CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Unknown) });

            AssertBuildThrows(stage, "must use a known TileFeatureKind");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_TileFeatureWallLikeSolidOverlap_RejectsUntilPolicyExists()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 2);
            var stage = CreateStage(
                "TileFeatureWallOverlap",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(20, StageSpawnKind.Wall, cell, hp: 1));
            SetPrivateField(stage, "tileFeatures", new[]
            {
                CreateTileFeature(100, cell, TileFeatureKind.Barricade),
            });

            AssertBuildThrows(stage, "overlaps a wall-like solid occupant");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_SameCellMultipleTileFeatures_StorageSupportOnly_MaterializesBoth()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 2);
            var stage = CreateStageWithTileFeatures(
                "SameCellTileFeatureStorage",
                new[]
                {
                    CreateTileFeature(100, cell, TileFeatureKind.Button),
                    CreateTileFeature(101, cell, TileFeatureKind.Destroy),
                });

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                CollectionAssert.AreEqual(
                    new[] { 100, 101 },
                    Array.ConvertAll(buildResult.InitialTileFeatures, feature => feature.TileId));
                Assert.That(buildResult.InitialTileFeatures[0].Cell, Is.EqualTo(cell));
                Assert.That(buildResult.InitialTileFeatures[1].Cell, Is.EqualTo(cell));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_ButtonActivatedCondition_CompilesRuntimeCondition()
        {
            var condition = CreateButtonActivatedCondition(100);
            var stage = CreateStageWithButtonObjective(
                "ButtonActivatedCondition",
                condition,
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Button,
                        boxSelector: TileFeatureBoxSelector.AnyPushableBox),
                });

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.ObjectiveRuntimeDefinition.ConditionEntries.Count, Is.EqualTo(1));
                var runtime = buildResult.ObjectiveRuntimeDefinition.ConditionEntries[0].Condition.CreateRuntime();
                Assert.That(runtime.CreateStatus().ConditionType, Is.EqualTo(nameof(ButtonActivatedConditionAsset)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ButtonActivatedCondition_NonPositiveTileIdRejects()
        {
            var condition = CreateButtonActivatedCondition(0);
            var stage = CreateStageWithButtonObjective(
                "ButtonActivatedConditionNonPositive",
                condition,
                new[]
                {
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button),
                });

            AssertBuildThrows(stage, "requires a positive tile id", condition);
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ButtonActivatedCondition_MissingTileIdRejects()
        {
            var condition = CreateButtonActivatedCondition(200);
            var stage = CreateStageWithButtonObjective(
                "ButtonActivatedConditionMissing",
                condition,
                new[]
                {
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button),
                });

            AssertBuildThrows(stage, "references unknown button TileId 200", condition);
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ButtonActivatedCondition_NonButtonTileRejects()
        {
            var condition = CreateButtonActivatedCondition(100);
            var stage = CreateStageWithButtonObjective(
                "ButtonActivatedConditionNonButton",
                condition,
                new[]
                {
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Exit),
                });

            AssertBuildThrows(stage, "instead of Button", condition);
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ButtonActivatedCondition_DuplicateTileIdRejects()
        {
            var first = CreateButtonActivatedCondition(100);
            var second = CreateButtonActivatedCondition(100);
            var stage = CreateStageWithObjective(
                "ButtonActivatedConditionDuplicate",
                new[]
                {
                    CreateConditionEntry(first, "button-1"),
                    CreateConditionEntry(second, "button-2"),
                },
                new[]
                {
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button),
                });

            AssertBuildThrows(stage, "duplicate ButtonActivatedCondition for TileId 100", first, second);
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ButtonActivatedCondition_MoonBlockOnlyRejectsWithoutMoonBlockSpawn()
        {
            var condition = CreateButtonActivatedCondition(100);
            var stage = CreateStageWithButtonObjective(
                "ButtonActivatedConditionMoonBlockOnlyNoMoon",
                condition,
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Button,
                        boxSelector: TileFeatureBoxSelector.MoonBlockOnly),
                });

            AssertBuildThrows(stage, "stage has no Moon box spawn", condition);
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_ButtonActivatedCondition_MoonBlockOnly_AllowsMoonBlockSpawn()
        {
            var condition = CreateButtonActivatedCondition(100);
            var stage = CreateStageWithObjective(
                "ButtonActivatedConditionMoonBlockOnlyWithMoon",
                new[] { CreateConditionEntry(condition, "button-activated") },
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Button,
                        boxSelector: TileFeatureBoxSelector.MoonBlockOnly),
                },
                CreateSpawn(
                    20,
                    StageSpawnKind.Box,
                    new SurfaceCell(FaceId.Floor, 2, 1),
                    hp: 1,
                    boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                    boxArchetype: BoxArchetype.Moon));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.ObjectiveRuntimeDefinition.ConditionEntries.Count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_MoonBoxValidation_RejectsInvalidShape()
        {
            AssertBuildThrows(
                CreateStage(
                    "InvalidBoxArchetype",
                    CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                    CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                    CreateSpawn(20, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 0), hp: 1, boxArchetype: (BoxArchetype)999)),
                "invalid BoxArchetype value 999");

            AssertBuildThrows(
                CreateStage(
                    "NonBoxMoonArchetype",
                    CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                    CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, boxArchetype: BoxArchetype.Moon)),
                "box archetypes are only valid on Box spawns");

            AssertBuildThrows(
                CreateStage(
                    "DuplicateMoonBoxes",
                    CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                    CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                    CreateSpawn(20, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 0), hp: 1, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy, boxArchetype: BoxArchetype.Moon),
                    CreateSpawn(21, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 2, 0), hp: 1, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy, boxArchetype: BoxArchetype.Moon)),
                "more than one Moon box spawn");

            AssertBuildThrows(
                CreateStage(
                    "MoonMissingCapabilities",
                    CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                    CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                    CreateSpawn(20, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 0), hp: 1, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip, boxArchetype: BoxArchetype.Moon)),
                "Moon boxes require BoxCapabilities");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_ExistingObjectiveConditionAssets_StillCompile()
        {
            var condition = ScriptableObject.CreateInstance<AllEnemiesDefeatedConditionAsset>();
            var stage = CreateStageWithObjective(
                "ExistingConditionStillCompiles",
                new[] { CreateConditionEntry(condition, "all-enemies") },
                Array.Empty<StageTileFeatureDefinition>());

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.ObjectiveRuntimeDefinition.ConditionEntries.Count, Is.EqualTo(1));
                Assert.That(
                    buildResult.ObjectiveRuntimeDefinition.ConditionEntries[0].Condition.CreateRuntime().CreateStatus().ConditionType,
                    Is.EqualTo(nameof(AllEnemiesDefeatedConditionAsset)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_AuthoredButtonInitialFlags_RemainNone()
        {
            var stage = CreateStageWithTileFeatures(
                "AuthoredButtonInitialFlags",
                new[]
                {
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button),
                });

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.InitialTileFeatures.Length, Is.EqualTo(1));
                Assert.That(buildResult.InitialTileFeatures[0].Kind, Is.EqualTo(TileFeatureKind.Button));
                Assert.That(buildResult.InitialTileFeatures[0].Flags, Is.EqualTo(TileFeatureFlags.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_DuplicateCellRejects()
        {
            var stage = CreateStage(
                "DuplicateCell",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(30, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1));

            AssertBuildThrows(stage, "duplicate occupied cell");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_StackedUnitAuthoring_AllowsExplicitOptInGroup()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStage(
                "StackedUnitOptIn",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(
                    10,
                    StageSpawnKind.Player,
                    sharedCell,
                    hp: 3,
                    facing: Direction.Up,
                    unitStackGroup: "spawn-stack"),
                CreateSpawn(
                    20,
                    StageSpawnKind.Enemy,
                    sharedCell,
                    hp: 2,
                    enemyAiMode: EnemyAiMode.Patrol,
                    unitStackGroup: "spawn-stack"));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);
                var snapshot = GameplayCompositionRoot.CreateWorldState(
                        buildResult.InitialEntities,
                        buildResult.BoardBounds,
                        buildResult.InitialTerrain,
                        buildResult.InitialTopology)
                    .CreateSnapshot();
                var units = new System.Collections.Generic.List<EntityState>();

                snapshot.EnumerateUnitsAt(sharedCell, units);

                CollectionAssert.AreEqual(new[] { 10, 20 }, units.ConvertAll(entity => entity.entityId));
                Assert.That(snapshot.TryGetPrimaryUnitAt(sharedCell, out var primaryUnit), Is.True);
                Assert.That(primaryUnit.entityId, Is.EqualTo(10));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_StackedUnitAuthoring_RejectsMissingOrDifferentOptInGroup()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var missingGroupStage = CreateStage(
                "StackedUnitMissingGroup",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, sharedCell, hp: 3, facing: Direction.Up, unitStackGroup: "spawn-stack"),
                CreateSpawn(20, StageSpawnKind.Enemy, sharedCell, hp: 2, enemyAiMode: EnemyAiMode.Patrol));
            var differentGroupStage = CreateStage(
                "StackedUnitDifferentGroup",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, sharedCell, hp: 3, facing: Direction.Up, unitStackGroup: "spawn-stack"),
                CreateSpawn(20, StageSpawnKind.Enemy, sharedCell, hp: 2, enemyAiMode: EnemyAiMode.Patrol, unitStackGroup: "other-stack"));

            AssertBuildThrows(missingGroupStage, "duplicate occupied cell");
            AssertBuildThrows(differentGroupStage, "duplicate occupied cell");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_OutOfBoundsRejects()
        {
            var stage = CreateStage(
                "OutOfBounds",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 3, 3), hp: 3, facing: Direction.Up));

            AssertBuildThrows(stage, "outside the configured board bounds");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ZeroPlayerRejects()
        {
            var stage = CreateStage(
                "ZeroPlayer",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(30, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1));

            AssertBuildThrows(stage, "must contain exactly one player spawn, but found none");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_MultiplePlayersRejects()
        {
            var stage = CreateStage(
                "MultiplePlayers",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, facing: Direction.Up),
                CreateSpawn(11, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 2, 2), hp: 3, facing: Direction.Right));

            AssertBuildThrows(stage, "must contain exactly one player spawn, but found 2");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_GroupKindMismatchRejects()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();

            try
            {
                stage.name = "GroupKindMismatch";
                SetPrivateField(stage, "board", CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)));
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
        [Category("Full")]
        public void StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");
            Assert.That(stage.PlayerSpawns.Length, Is.EqualTo(1));
            Assert.That(stage.BoxSpawns.Length, Is.EqualTo(12));
            Assert.That(stage.EnemySpawns.Length, Is.EqualTo(6));
            Assert.That(stage.WallSpawns.Length, Is.EqualTo(5));

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.BoardBounds.MinInclusive, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(buildResult.BoardBounds.MaxInclusive, Is.EqualTo(new Vector2Int(14, 7)));
            Assert.That(buildResult.InitialTopology.BottomFace, Is.EqualTo(FaceId.Floor));
            Assert.That(buildResult.PlayerEntityId, Is.EqualTo(10));
            Assert.That(buildResult.InitialTerrain, Is.SameAs(Game.Feature.Gameplay.BoardState.TerrainData.Empty));
            Assert.That(buildResult.InitialTileFeatures, Is.Empty);
            Assert.That(buildResult.TileFeatureDefinitions, Is.Empty);
            Assert.That(
                buildResult.InitialEntities.Length,
                Is.EqualTo(stage.PlayerSpawns.Length + stage.BoxSpawns.Length + stage.EnemySpawns.Length + stage.WallSpawns.Length));
            AssertEntityIdsAreSorted(buildResult.InitialEntities);

            Assert.That(TryGetEntity(buildResult.InitialEntities, 10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(player.facing, Is.EqualTo(Direction.Right));
            Assert.That(player.unitRole, Is.EqualTo(UnitRole.Player));

            Assert.That(TryGetEntity(buildResult.InitialEntities, 196, out var floorWall), Is.True);
            Assert.That(floorWall.type, Is.EqualTo(EntityType.None));
            Assert.That(floorWall.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 5, 1)));
            Assert.That(floorWall.unitRole, Is.EqualTo(UnitRole.None));

            Assert.That(TryGetEntity(buildResult.InitialEntities, 200, out var frontWall), Is.True);
            Assert.That(frontWall.type, Is.EqualTo(EntityType.None));
            Assert.That(frontWall.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 9, 3)));
            Assert.That(frontWall.unitRole, Is.EqualTo(UnitRole.None));

            var snapshot = GameplayCompositionRoot.CreateWorldState(
                    buildResult.InitialEntities,
                    buildResult.BoardBounds,
                    buildResult.InitialTerrain,
                    buildResult.InitialTopology)
                .CreateSnapshot();
            Assert.That(snapshot.TryGetUnitTraversalBlocker(floorWall.position, out var floorWallBlocker), Is.True);
            Assert.That(floorWallBlocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(floorWallBlocker.EntityId, Is.EqualTo(floorWall.entityId));
            Assert.That(snapshot.TryGetUnitTraversalBlocker(frontWall.position, out var frontWallBlocker), Is.True);
            Assert.That(frontWallBlocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(frontWallBlocker.EntityId, Is.EqualTo(frontWall.entityId));

            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 1, 0)), Is.False);
            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Front, 7, buildResult.BoardBounds.MaxInclusive.y)), Is.False);
            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Ceiling, 2, buildResult.BoardBounds.MaxInclusive.y)), Is.False);
            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 2, buildResult.BoardBounds.MaxInclusive.y)), Is.False);

            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 9, 6)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Front, 3, 1)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Ceiling, 7, 1)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Back, 3, 5)), Is.True);

            Assert.That(TryGetEntity(buildResult.InitialEntities, ConfiguredShowcaseEnemyId, out var showcaseEnemy), Is.True);
            Assert.That(showcaseEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 6, 2)));
            Assert.That(showcaseEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(showcaseEnemy.hp, Is.EqualTo(3));
            Assert.That(showcaseEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, NonAttackingShowcaseEnemyId, out var nonAttackingEnemy), Is.True);
            Assert.That(nonAttackingEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 7, 2)));
            Assert.That(nonAttackingEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(nonAttackingEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, WallFollowerShowcaseEnemyId, out var wallFollowerEnemy), Is.True);
            Assert.That(wallFollowerEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 7, 7)));
            Assert.That(wallFollowerEnemy.facing, Is.EqualTo(Direction.Up));
            Assert.That(wallFollowerEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(wallFollowerEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, JumpShowcaseEnemyId, out var jumpEnemy), Is.True);
            Assert.That(jumpEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 10, 2)));
            Assert.That(jumpEnemy.facing, Is.EqualTo(Direction.Left));
            Assert.That(jumpEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(jumpEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, ChargeShowcaseEnemyId, out var chargeEnemy), Is.True);
            Assert.That(chargeEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Back, 7, 7)));
            Assert.That(chargeEnemy.facing, Is.EqualTo(Direction.Left));
            Assert.That(chargeEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(chargeEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, UtilitySummonerShowcaseEnemyId, out var utilitySummonerEnemy), Is.True);
            Assert.That(utilitySummonerEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Back, 4, 5)));
            Assert.That(utilitySummonerEnemy.facing, Is.EqualTo(Direction.Left));
            Assert.That(utilitySummonerEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(utilitySummonerEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(6));
            Assert.That(TryGetProfileOverride(buildResult, ConfiguredShowcaseEnemyId, out var showcaseProfile), Is.True);
            Assert.That(showcaseProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(showcaseProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(showcaseProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
            Assert.That(showcaseProfile.AttackTimingSettings.WindupSeconds, Is.EqualTo(1f));
            Assert.That(showcaseProfile.LocomotionTimingSettings.MoveCooldownSeconds, Is.EqualTo(1f));
            Assert.That(showcaseProfile.PatrolSettings.LeashRadius, Is.EqualTo(1));
            Assert.That(showcaseProfile.PatrolSettings.ForwardWeight, Is.EqualTo(6));
            Assert.That(showcaseProfile.PatrolSettings.SideWeight, Is.EqualTo(1));
            Assert.That(showcaseProfile.PatrolSettings.BackwardWeight, Is.EqualTo(1));
            Assert.That(showcaseProfile.PatrolSettings.PreventImmediateBacktrack, Is.True);

            Assert.That(TryGetProfileOverride(buildResult, NonAttackingShowcaseEnemyId, out var nonAttackingProfile), Is.True);
            Assert.That(nonAttackingProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(nonAttackingProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(nonAttackingProfile.PatrolSettings.LeashRadius, Is.EqualTo(2));
            Assert.That(nonAttackingProfile.PatrolSettings.PreventImmediateBacktrack, Is.True);

            Assert.That(TryGetProfileOverride(buildResult, WallFollowerShowcaseEnemyId, out var wallFollowerProfile), Is.True);
            Assert.That(wallFollowerProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(wallFollowerProfile.DetectionStrategyKind, Is.EqualTo(DetectionStrategyKind.None));
            Assert.That(wallFollowerProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));

            Assert.That(TryGetProfileOverride(buildResult, JumpShowcaseEnemyId, out var jumpProfile), Is.True);
            Assert.That(jumpProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(jumpProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
            Assert.That(jumpProfile.JumpTimingSettings.WindupSeconds, Is.EqualTo(0.35f));
            Assert.That(jumpProfile.JumpTimingSettings.AirborneSeconds, Is.EqualTo(1f));
            Assert.That(jumpProfile.JumpTimingSettings.CooldownSeconds, Is.EqualTo(3f));

            Assert.That(TryGetProfileOverride(buildResult, ChargeShowcaseEnemyId, out var chargeProfile), Is.True);
            Assert.That(chargeProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Charge));
            Assert.That(chargeProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.Forward));

            Assert.That(TryGetProfileOverride(buildResult, UtilitySummonerShowcaseEnemyId, out var utilitySummonerProfile), Is.True);
            var utilitySummonerRuntimeDefinition =
                utilitySummonerProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            Assert.That(utilitySummonerRuntimeDefinition.Capabilities.TryGetUtility(out var utility), Is.True);
            Assert.That(utility.Effects, Is.Not.Empty);
            Assert.That(utility.Effects[0].Kind, Is.EqualTo(EnemyUtilityEffectKind.SummonMinion));
            Assert.That(
                utility.Effects[0].Summon.SummonedArchetypeId,
                Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));
            Assert.That(utility.Effects[0].Summon.OverrideHp, Is.False);

            var presentationDefinition = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(CombinedPresentationAssetPath);
            Assert.That(
                presentationDefinition,
                Is.Not.Null,
                $"Missing stage presentation asset at '{CombinedPresentationAssetPath}'.");

            var presentation = StagePresentationAssembler.Resolve(presentationDefinition);
            Assert.That(presentation.EnemyPresentationBindings.Length, Is.EqualTo(6));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, ConfiguredShowcaseEnemyId, out var presentationBinding), Is.True);
            Assert.That(presentationBinding.PresentationId, Is.EqualTo(AttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, NonAttackingShowcaseEnemyId, out var nonAttackingBinding), Is.True);
            Assert.That(nonAttackingBinding.PresentationId, Is.EqualTo(NonAttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, WallFollowerShowcaseEnemyId, out var wallFollowerBinding), Is.True);
            Assert.That(wallFollowerBinding.PresentationId, Is.EqualTo(WallFollowerEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, JumpShowcaseEnemyId, out var jumpBinding), Is.True);
            Assert.That(jumpBinding.PresentationId, Is.EqualTo(JumpChaserEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, ChargeShowcaseEnemyId, out var chargeBinding), Is.True);
            Assert.That(chargeBinding.PresentationId, Is.EqualTo(ChargeEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, UtilitySummonerShowcaseEnemyId, out var utilitySummonerBinding), Is.True);
            Assert.That(utilitySummonerBinding.PresentationId, Is.EqualTo(UtilitySummonerEnemyPresentationId));
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_TutorialEnemySpawnWithPassiveContactProfile_BuildsOverridesAndKeepsPatrolMode()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(TutorialStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{TutorialStageAssetPath}'.");
            Assert.That(stage.EnemySpawns.Length, Is.GreaterThan(0));
            var tutorialEnemyProfile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(TutorialEnemyProfileAssetPath);
            Assert.That(tutorialEnemyProfile, Is.Not.Null, $"Missing tutorial enemy profile asset at '{TutorialEnemyProfileAssetPath}'.");

            for (var i = 0; i < stage.EnemySpawns.Length; i++)
            {
                Assert.That(
                    stage.EnemySpawns[i].EnemyAiProfile,
                    Is.SameAs(tutorialEnemyProfile),
                    $"Tutorial enemy spawn {stage.EnemySpawns[i].EntityId} should reference the tutorial passive contact profile.");
                Assert.That(stage.EnemySpawns[i].EnemyAiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(stage.EnemySpawns.Length));
            Assert.That(TryGetEntity(buildResult.InitialEntities, TutorialEnemyId, out var tutorialEnemy), Is.True);
            Assert.That(tutorialEnemy.type, Is.EqualTo(EntityType.Unit));
            Assert.That(tutorialEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));
            Assert.That(tutorialEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(TryGetProfileOverride(buildResult, TutorialEnemyId, out var tutorialOverride), Is.True);
            Assert.That(tutorialOverride, Is.SameAs(tutorialEnemyProfile));
        }

        [Test]
        [Category("Extended")]
        public void StagePresentationDefinition_DefaultsTileFeaturePresentationBindingsToEmpty()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                Assert.That(presentation.TileFeaturePresentationBindings, Is.Not.Null);
                Assert.That(presentation.TileFeaturePresentationBindings, Is.Empty);

                var resolved = StagePresentationAssembler.Resolve(presentation);
                Assert.That(resolved.TileFeatureBindings, Is.Not.Null);
                Assert.That(resolved.TileFeatureBindings, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        [Category("Extended")]
        public void StagePresentationAssembler_ResolvesTileFeaturePresentationBindingsAsReadOnlyData()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var prefab = new GameObject("ButtonTileVisualPrefab");

            try
            {
                SetPrivateField(
                    presentation,
                    "tileFeaturePresentationBindings",
                    new[]
                    {
                        new TileFeaturePresentationBinding
                        {
                            TileId = 100,
                            VisualPrefab = prefab,
                        },
                    });

                var resolved = StagePresentationAssembler.Resolve(presentation);

                Assert.That(resolved.TileFeatureBindings, Is.InstanceOf<ReadOnlyCollection<TileFeaturePresentationResolvedBinding>>());
                Assert.That(resolved.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(resolved.TileFeatureBindings[0].TileId, Is.EqualTo(100));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(prefab));
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<TileFeaturePresentationResolvedBinding>)resolved.TileFeatureBindings).Add(
                        new TileFeaturePresentationResolvedBinding(200, prefab)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        [Category("Extended")]
        public void StagePresentationDefinition_ApplyResolvedData_PreservesTileFeaturePresentationBindings()
        {
            var source = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var copy = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var prefab = new GameObject("ButtonTileVisualPrefab");

            try
            {
                SetPrivateField(
                    source,
                    "tileFeaturePresentationBindings",
                    new[]
                    {
                        new TileFeaturePresentationBinding
                        {
                            TileId = 100,
                            VisualPrefab = prefab,
                        },
                    });

                copy.ApplyResolvedData(StagePresentationAssembler.Resolve(source));
                var roundTrip = StagePresentationAssembler.Resolve(copy);

                Assert.That(roundTrip.TileFeatureBindings, Has.Count.EqualTo(1));
                Assert.That(roundTrip.TileFeatureBindings[0].TileId, Is.EqualTo(100));
                Assert.That(roundTrip.TileFeatureBindings[0].VisualPrefab, Is.SameAs(prefab));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(source);
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

        private static StageDefinition CreateStageWithTileFeatures(
            string stageName,
            StageTileFeatureDefinition[] tileFeatures)
        {
            var stage = CreateStage(
                stageName,
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up));
            SetPrivateField(stage, "tileFeatures", tileFeatures);
            return stage;
        }

        private static StageDefinition CreateStageWithButtonObjective(
            string stageName,
            ButtonActivatedConditionAsset condition,
            StageTileFeatureDefinition[] tileFeatures)
        {
            return CreateStageWithObjective(
                stageName,
                new[] { CreateConditionEntry(condition, "button-activated") },
                tileFeatures);
        }

        private static StageDefinition CreateStageWithObjective(
            string stageName,
            StageObjectiveConditionEntry[] conditionEntries,
            StageTileFeatureDefinition[] tileFeatures,
            params StageSpawnDefinition[] additionalSpawns)
        {
            var spawns = new List<StageSpawnDefinition>
            {
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
            };
            if (additionalSpawns != null)
            {
                spawns.AddRange(additionalSpawns);
            }

            var stage = CreateStage(stageName, CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)), spawns.ToArray());
            SetPrivateField(stage, "tileFeatures", tileFeatures);
            SetPrivateField(stage, "objective", new StageObjectiveAuthoring
            {
                CompletionPolicy = StageCompletionPolicy.RequireAllConditions,
                ObjectiveTitle = string.Empty,
                ObjectiveSummary = string.Empty,
                ConditionEntries = conditionEntries ?? Array.Empty<StageObjectiveConditionEntry>(),
            });
            return stage;
        }

        private static StageObjectiveConditionEntry CreateConditionEntry(
            StageConditionAsset condition,
            string stableConditionId)
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = true,
                Role = StageObjectiveConditionRole.None,
                StableConditionId = stableConditionId,
                DisplayText = string.Empty,
                SortOrder = 0,
            };
        }

        private static ButtonActivatedConditionAsset CreateButtonActivatedCondition(int tileId)
        {
            var condition = ScriptableObject.CreateInstance<ButtonActivatedConditionAsset>();
            SetPrivateField(condition, "tileId", tileId);
            return condition;
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
            BoxArchetype boxArchetype = BoxArchetype.Normal,
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
                BoxArchetype = boxArchetype,
                EnemyAiMode = enemyAiMode,
                EnemyAiStateTimer = enemyAiStateTimer,
                EnemyAiProfile = enemyAiProfile,
                PresentationId = presentationId,
                UnitStackGroup = unitStackGroup,
            };
        }

        private static StageTileFeatureDefinition CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            TileFeatureActivationRule activationRule = TileFeatureActivationRule.Always,
            Direction2D direction = Direction2D.None,
            TileFeatureBoxSelector boxSelector = TileFeatureBoxSelector.None,
            int boundEntityId = 0,
            string presentationKey = null)
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = cell,
                Kind = kind,
                ActivationRule = activationRule,
                Direction = direction,
                BoxSelector = boxSelector,
                BoundEntityId = boundEntityId,
                PresentationKey = presentationKey,
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

        private static void AssertBuildThrows(
            StageDefinition stage,
            string expectedMessage,
            params UnityEngine.Object[] additionalObjectsToDestroy)
        {
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(stage));
                StringAssert.Contains(expectedMessage, exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                if (additionalObjectsToDestroy != null)
                {
                    for (var i = 0; i < additionalObjectsToDestroy.Length; i++)
                    {
                        UnityEngine.Object.DestroyImmediate(additionalObjectsToDestroy[i]);
                    }
                }
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
            IReadOnlyList<EnemyPresentationBinding> bindings,
            int entityId,
            out EnemyPresentationBinding binding)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                var entry = bindings[i];
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
