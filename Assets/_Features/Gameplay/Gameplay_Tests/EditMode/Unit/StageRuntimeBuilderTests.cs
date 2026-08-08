using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-4-3/stage-4-3.asset";
        private const string CombinedPresentationAssetPath =
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-4-3/stage-4-3_Presentation.asset";
        private const string Stage32StageAssetPath =
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-3-2/stage-3-2.asset";
        private const string Stage32PresentationAssetPath =
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-3-2/stage-3-2_Presentation.asset";
        private const string OnboardingStageAssetPath =
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-0-1/stage-0-1.asset";
        private const string TutorialEnemyProfileAssetPath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_Common/EnemyAi_TutorialPassiveContact.asset";
        private const int ConfiguredShowcaseEnemyId = 60;
        private const int WallFollowerShowcaseEnemyId = 56;
        private const int JumpShowcaseEnemyId = 61;
        private const int ChargeShowcaseEnemyId = 58;
        private const int UtilitySummonerShowcaseEnemyId = 59;
        private const int CombinedShowcaseEnemyCount = 5;
        private const int Stage32PlayerId = 10;
        private const int Stage32WindupMeleeEnemyId = 54;
        private const int Stage32NonAttackingEnemyId = 55;
        private const int Stage32WallFollowerEnemyId = 56;
        private const int Stage32JumpEnemyId = 57;
        private const int Stage32ChargeEnemyId = 58;
        private const int Stage32UtilitySummonerEnemyId = 59;
        private const int Stage32GlideEnemyId = 241;
        private const int Stage32GlideWallId = 238;
        private const int TutorialEnemyId = 165;
        private const string AttackingEnemyPresentationId = "black_eye";
        private const string NonAttackingEnemyPresentationId = "startis";
        private const string WallFollowerEnemyPresentationId = "sunwheel";
        private const string JumpChaserEnemyPresentationId = "astreton";
        private const string ChargeEnemyPresentationId = "rocket_face";
        private const string UtilitySummonerEnemyPresentationId = "j_peter";

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
        public void StageRuntimeBuilder_UnitMobility_DefaultsGround_AndAuthoredAirMaterializesForUnits()
        {
            var stage = CreateStage(
                "UnitMobilityMaterialization",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateSpawn(20, StageSpawnKind.Enemy, new SurfaceCell(FaceId.Floor, 1, 0), hp: 2, enemyAiMode: EnemyAiMode.Patrol),
                CreateSpawn(30, StageSpawnKind.Enemy, new SurfaceCell(FaceId.Floor, 2, 0), hp: 2, enemyAiMode: EnemyAiMode.Patrol, unitMobilityKind: UnitMobilityKind.Air),
                CreateSpawn(40, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 3, 0), hp: 1, unitMobilityKind: UnitMobilityKind.Air),
                CreateSpawn(50, StageSpawnKind.Wall, new SurfaceCell(FaceId.Floor, 4, 0), hp: 1, unitMobilityKind: UnitMobilityKind.Air));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(TryGetEntity(buildResult.InitialEntities, 10, out var player), Is.True);
                Assert.That(player.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));
                Assert.That(TryGetEntity(buildResult.InitialEntities, 20, out var groundEnemy), Is.True);
                Assert.That(groundEnemy.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));
                Assert.That(TryGetEntity(buildResult.InitialEntities, 30, out var airEnemy), Is.True);
                Assert.That(airEnemy.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
                Assert.That(TryGetEntity(buildResult.InitialEntities, 40, out var box), Is.True);
                Assert.That(box.type, Is.EqualTo(EntityType.Box));
                Assert.That(TryGetEntity(buildResult.InitialEntities, 50, out var wall), Is.True);
                Assert.That(wall.type, Is.EqualTo(EntityType.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_ActiveEnemySpawnWithoutExplicitProfile_Rejects()
        {
            var missingProfileEnemy = CreateSpawn(
                20,
                StageSpawnKind.Enemy,
                new SurfaceCell(FaceId.Floor, 1, 0),
                hp: 2);
            missingProfileEnemy.EnemyAiMode = EnemyAiMode.Patrol;
            missingProfileEnemy.EnemyAiProfile = null;
            var stage = CreateStage(
                "MissingEnemyAiProfile",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                missingProfileEnemy);

            AssertBuildThrows(stage, "must assign an explicit EnemyAiProfile");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_WallFacing_UsesAuthoredFacing()
        {
            var stage = CreateStage(
                "WallFacing",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                CreateSpawn(20, StageSpawnKind.Wall, new SurfaceCell(FaceId.Floor, 1, 0), hp: 1, facing: Direction.Left),
                CreateSpawn(21, StageSpawnKind.Wall, new SurfaceCell(FaceId.Floor, 2, 0), hp: 1, facing: Direction.None));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(TryGetEntity(buildResult.InitialEntities, 20, out var authoredWall), Is.True);
                Assert.That(authoredWall.type, Is.EqualTo(EntityType.None));
                Assert.That(authoredWall.facing, Is.EqualTo(Direction.Left));
                Assert.That(TryGetEntity(buildResult.InitialEntities, 21, out var defaultWall), Is.True);
                Assert.That(defaultWall.type, Is.EqualTo(EntityType.None));
                Assert.That(defaultWall.facing, Is.EqualTo(Direction.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
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
                Assert.That(box.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.None));
                Assert.That(box.gravityFieldTimerTicks, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_GravityFieldBoxArchetype_MaterializesChargingRuntime()
        {
            var stage = CreateStage(
                "GravityFieldBoxArchetype",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(
                    20,
                    StageSpawnKind.Box,
                    new SurfaceCell(FaceId.Floor, 1, 2),
                    hp: 1,
                    boxArchetype: BoxArchetype.GravityField));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(TryGetEntity(buildResult.InitialEntities, 20, out var box), Is.True);
                Assert.That(box.boxArchetype, Is.EqualTo(BoxArchetype.GravityField));
                Assert.That(box.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Charging));
                Assert.That(box.gravityFieldTimerTicks, Is.EqualTo(8 * GameplayTimingProfile.DefaultSimulationTicksPerSecond));
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
        public void StageRuntimeBuilder_MoonBlockGenerator_BuildsRespawnDefinition()
        {
            var moonSpawn = CreateSpawn(
                20,
                StageSpawnKind.Box,
                new SurfaceCell(FaceId.Floor, 2, 1),
                hp: 2,
                facing: Direction.Left,
                boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                boxArchetype: BoxArchetype.Moon);
            var generator = CreateTileFeature(
                100,
                new SurfaceCell(FaceId.Floor, 1, 1),
                TileFeatureKind.MoonBlockGenerator,
                TileFeatureActivationRule.BottomFaceOnly,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 20);
            var stage = CreateStage(
                "MoonBlockGeneratorBuildsRespawnDefinition",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
                moonSpawn);
            SetPrivateField(stage, "tileFeatures", new[] { generator });

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.MoonBlockRespawnDefinitions.Length, Is.EqualTo(1));
                var definition = buildResult.MoonBlockRespawnDefinitions[0];
                Assert.That(definition.GeneratorTileId, Is.EqualTo(100));
                Assert.That(definition.MoonBlockEntityId, Is.EqualTo(20));
                Assert.That(definition.SpawnCell, Is.EqualTo(generator.Cell));
                Assert.That(definition.Template.entityId, Is.EqualTo(20));
                Assert.That(definition.Template.type, Is.EqualTo(EntityType.Box));
                Assert.That(definition.Template.boxArchetype, Is.EqualTo(BoxArchetype.Moon));
                Assert.That(definition.Template.boxCapabilities, Is.EqualTo(moonSpawn.BoxCapabilities));
                Assert.That(definition.Template.facing, Is.EqualTo(Direction.Left));
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
                TileFeatureKind.Button,
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
        [Category("Core")]
        public void StageRuntimeBuilder_DestroyTile_FaceOnlyActivationAccepted()
        {
            var acceptedRules = new[]
            {
                TileFeatureActivationRule.BottomFaceOnly,
                TileFeatureActivationRule.FrontFaceOnly,
                TileFeatureActivationRule.ActiveFaceOnly,
                TileFeatureActivationRule.InactiveFaceOnly,
            };

            for (var i = 0; i < acceptedRules.Length; i++)
            {
                var stage = CreateStageWithTileFeatures(
                    $"DestroyTileFaceOnly{i}",
                    new[]
                    {
                        CreateTileFeature(
                            100,
                            new SurfaceCell(FaceId.Floor, 1, 1),
                            TileFeatureKind.Destroy,
                            acceptedRules[i]),
                    });

                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.TileFeatureDefinitions[0].ActivationRule, Is.EqualTo(acceptedRules[i]));
                Assert.That(buildResult.InitialTileFeatures[0].Kind, Is.EqualTo(TileFeatureKind.Destroy));
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_DestroyTile_AlwaysActivationRejects()
        {
            var stage = CreateStageWithTileFeatures(
                "DestroyTileAlwaysActivation",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Destroy,
                        TileFeatureActivationRule.Always),
                });

            AssertBuildThrows(stage, "DestroyTile must use a FaceOnly activation rule");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_SlideTile_FrontFaceCardinalDirectionAndNoSelectorAccepted()
        {
            var stage = CreateStageWithTileFeatures(
                "SlideTileValid",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Slide,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.Right,
                        TileFeatureBoxSelector.None),
                });

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.TileFeatureDefinitions[0].ActivationRule, Is.EqualTo(TileFeatureActivationRule.FrontFaceOnly));
            Assert.That(buildResult.TileFeatureDefinitions[0].Direction, Is.EqualTo(Direction2D.Right));
            Assert.That(buildResult.TileFeatureDefinitions[0].BoxSelector, Is.EqualTo(TileFeatureBoxSelector.None));
            Assert.That(buildResult.InitialTileFeatures[0].Kind, Is.EqualTo(TileFeatureKind.Slide));
            UnityEngine.Object.DestroyImmediate(stage);
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_SlideTile_UnsupportedActivationRejects()
        {
            var unsupportedRules = new[]
            {
                TileFeatureActivationRule.BottomFaceOnly,
                TileFeatureActivationRule.Always,
                TileFeatureActivationRule.ActiveFaceOnly,
                TileFeatureActivationRule.InactiveFaceOnly,
            };

            for (var i = 0; i < unsupportedRules.Length; i++)
            {
                var stage = CreateStageWithTileFeatures(
                    $"SlideTileUnsupportedActivation{i}",
                    new[]
                    {
                        CreateTileFeature(
                            100,
                            new SurfaceCell(FaceId.Floor, 1, 1),
                            TileFeatureKind.Slide,
                            unsupportedRules[i],
                            Direction2D.Right,
                            TileFeatureBoxSelector.None),
                    });

                AssertBuildThrows(stage, "SlideTile must use FrontFaceOnly activation");
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_SlideTile_InvalidDirectionRejects()
        {
            var noneDirectionStage = CreateStageWithTileFeatures(
                "SlideTileNoneDirection",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Slide,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });
            AssertBuildThrows(noneDirectionStage, "SlideTile must use a cardinal Direction2D");

            var invalidEnumStage = CreateStageWithTileFeatures(
                "SlideTileInvalidDirection",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Slide,
                        TileFeatureActivationRule.FrontFaceOnly,
                        (Direction2D)99,
                        TileFeatureBoxSelector.None),
                });
            AssertBuildThrows(invalidEnumStage, "invalid Direction2D value 99");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_SlideTile_UnsupportedBoxSelectorRejects()
        {
            var stage = CreateStageWithTileFeatures(
                "SlideTileUnsupportedSelector",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Slide,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.Right,
                        TileFeatureBoxSelector.AnyPushableBox),
                });

            AssertBuildThrows(stage, "SlideTile must use TileFeatureBoxSelector.None");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_SlideTile_DuplicateSameCellRejects()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStageWithTileFeatures(
                "SlideTileDuplicateCell",
                new[]
                {
                    CreateTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Slide,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.Right,
                        TileFeatureBoxSelector.None),
                    CreateTileFeature(
                        101,
                        cell,
                        TileFeatureKind.Slide,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.Up,
                        TileFeatureBoxSelector.None),
                });

            AssertBuildThrows(stage, "duplicate SlideTile");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Barricade_FrontFaceNoDirectionAndNoSelectorAccepted()
        {
            var stage = CreateStageWithTileFeatures(
                "BarricadeValid",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.TileFeatureDefinitions[0].ActivationRule, Is.EqualTo(TileFeatureActivationRule.FrontFaceOnly));
            Assert.That(buildResult.TileFeatureDefinitions[0].Direction, Is.EqualTo(Direction2D.None));
            Assert.That(buildResult.TileFeatureDefinitions[0].BoxSelector, Is.EqualTo(TileFeatureBoxSelector.None));
            Assert.That(buildResult.InitialTileFeatures[0].Kind, Is.EqualTo(TileFeatureKind.Barricade));
            UnityEngine.Object.DestroyImmediate(stage);
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Barricade_UnsupportedActivationRejects()
        {
            var unsupportedRules = new[]
            {
                TileFeatureActivationRule.BottomFaceOnly,
                TileFeatureActivationRule.Always,
                TileFeatureActivationRule.ActiveFaceOnly,
                TileFeatureActivationRule.InactiveFaceOnly,
            };

            for (var i = 0; i < unsupportedRules.Length; i++)
            {
                var stage = CreateStageWithTileFeatures(
                    $"BarricadeUnsupportedActivation{i}",
                    new[]
                    {
                        CreateTileFeature(
                            100,
                            new SurfaceCell(FaceId.Floor, 1, 1),
                            TileFeatureKind.Barricade,
                            unsupportedRules[i],
                            Direction2D.None,
                            TileFeatureBoxSelector.None),
                    });

                AssertBuildThrows(stage, "Barricade must use FrontFaceOnly activation");
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Barricade_InvalidDirectionRejects()
        {
            var nonNoneDirectionStage = CreateStageWithTileFeatures(
                "BarricadeNonNoneDirection",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.Right,
                        TileFeatureBoxSelector.None),
                });
            AssertBuildThrows(nonNoneDirectionStage, "Barricade must use Direction2D.None");

            var invalidEnumStage = CreateStageWithTileFeatures(
                "BarricadeInvalidDirection",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        (Direction2D)99,
                        TileFeatureBoxSelector.None),
                });
            AssertBuildThrows(invalidEnumStage, "invalid Direction2D value 99");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Barricade_UnsupportedBoxSelectorRejects()
        {
            var unsupportedSelectorStage = CreateStageWithTileFeatures(
                "BarricadeUnsupportedSelector",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.AnyPushableBox),
                });
            AssertBuildThrows(unsupportedSelectorStage, "Barricade must use TileFeatureBoxSelector.None");

            var invalidEnumStage = CreateStageWithTileFeatures(
                "BarricadeInvalidSelector",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.None,
                        (TileFeatureBoxSelector)99),
                });
            AssertBuildThrows(invalidEnumStage, "invalid TileFeatureBoxSelector value 99");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Barricade_DuplicateSameCellRejects()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStageWithTileFeatures(
                "BarricadeDuplicateCell",
                new[]
                {
                    CreateTileFeature(
                        100,
                        cell,
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                    CreateTileFeature(
                        101,
                        cell,
                        TileFeatureKind.Barricade,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });

            AssertBuildThrows(stage, "duplicate Barricade");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Exit_BottomFaceActivationRejects()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var condition = CreatePlayerAtAnyZoneCondition("goal");
            var stage = CreateStageWithExitObjective(
                "ExitBottomFaceInvalid",
                exitCell,
                CreateRegion(1, 1, 1, 1),
                condition,
                activationRule: TileFeatureActivationRule.BottomFaceOnly);

            AssertBuildThrows(stage, "Exit must use ActiveFaceOnly activation", condition);
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Exit_ActiveFaceOnlyNoDirectionAndNoSelectorAccepted()
        {
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var condition = CreatePlayerAtAnyZoneCondition("goal");
            var stage = CreateStageWithExitObjective(
                "ExitActiveFaceValid",
                exitCell,
                CreateRegion(1, 1, 1, 1),
                condition,
                activationRule: TileFeatureActivationRule.ActiveFaceOnly);

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);

                Assert.That(buildResult.TileFeatureDefinitions[0].ActivationRule, Is.EqualTo(TileFeatureActivationRule.ActiveFaceOnly));
                Assert.That(buildResult.TileFeatureDefinitions[0].Direction, Is.EqualTo(Direction2D.None));
                Assert.That(buildResult.TileFeatureDefinitions[0].BoxSelector, Is.EqualTo(TileFeatureBoxSelector.None));
                Assert.That(buildResult.InitialTileFeatures[0].Kind, Is.EqualTo(TileFeatureKind.Exit));
                Assert.That(
                    buildResult.ObjectiveRuntimeDefinition.ConditionEntries[0].Condition.CreateRuntime().CreateStatus().ConditionType,
                    Is.EqualTo(PlayerAtActiveExitConditionRuntimeDefinition.RuntimeConditionType));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(condition);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Exit_UnsupportedActivationRejects()
        {
            var unsupportedRules = new[]
            {
                TileFeatureActivationRule.Always,
                TileFeatureActivationRule.BottomFaceOnly,
                TileFeatureActivationRule.FrontFaceOnly,
                TileFeatureActivationRule.InactiveFaceOnly,
            };

            for (var i = 0; i < unsupportedRules.Length; i++)
            {
                var condition = CreatePlayerAtAnyZoneCondition("goal");
                var stage = CreateStageWithExitObjective(
                    $"ExitUnsupportedActivation{i}",
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    CreateRegion(1, 1, 1, 1),
                    condition,
                    activationRule: unsupportedRules[i]);

                AssertBuildThrows(stage, "Exit must use ActiveFaceOnly activation", condition);
            }
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Exit_InvalidDirectionRejects()
        {
            var nonNoneCondition = CreatePlayerAtAnyZoneCondition("goal");
            var nonNoneDirectionStage = CreateStageWithExitObjective(
                "ExitNonNoneDirection",
                new SurfaceCell(FaceId.Floor, 1, 1),
                CreateRegion(1, 1, 1, 1),
                nonNoneCondition,
                direction: Direction2D.Right);
            AssertBuildThrows(nonNoneDirectionStage, "Exit must use Direction2D.None", nonNoneCondition);

            var invalidCondition = CreatePlayerAtAnyZoneCondition("goal");
            var invalidEnumStage = CreateStageWithExitObjective(
                "ExitInvalidDirection",
                new SurfaceCell(FaceId.Floor, 1, 1),
                CreateRegion(1, 1, 1, 1),
                invalidCondition,
                direction: (Direction2D)99);
            AssertBuildThrows(invalidEnumStage, "invalid Direction2D value 99", invalidCondition);
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Exit_UnsupportedBoxSelectorRejects()
        {
            var unsupportedCondition = CreatePlayerAtAnyZoneCondition("goal");
            var unsupportedSelectorStage = CreateStageWithExitObjective(
                "ExitUnsupportedSelector",
                new SurfaceCell(FaceId.Floor, 1, 1),
                CreateRegion(1, 1, 1, 1),
                unsupportedCondition,
                boxSelector: TileFeatureBoxSelector.AnyPushableBox);
            AssertBuildThrows(unsupportedSelectorStage, "Exit must use TileFeatureBoxSelector.None", unsupportedCondition);

            var invalidCondition = CreatePlayerAtAnyZoneCondition("goal");
            var invalidEnumStage = CreateStageWithExitObjective(
                "ExitInvalidSelector",
                new SurfaceCell(FaceId.Floor, 1, 1),
                CreateRegion(1, 1, 1, 1),
                invalidCondition,
                boxSelector: (TileFeatureBoxSelector)99);
            AssertBuildThrows(invalidEnumStage, "invalid TileFeatureBoxSelector value 99", invalidCondition);
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Exit_DuplicateRejects()
        {
            var condition = CreatePlayerAtAnyZoneCondition("goal");
            var exitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStageWithExitObjective(
                "ExitDuplicate",
                exitCell,
                CreateRegion(1, 1, 1, 1),
                condition,
                extraTileFeatures: new[]
                {
                    CreateTileFeature(
                        101,
                        new SurfaceCell(FaceId.Floor, 2, 1),
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.ActiveFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });

            AssertBuildThrows(stage, "more than one Exit TileFeature", condition);
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Exit_MissingPrimaryGoalRejects()
        {
            var stage = CreateStageWithTileFeatures(
                "ExitMissingPrimaryGoal",
                new[]
                {
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.ActiveFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
                });

            AssertBuildThrows(stage, "must use objective policy RequireAllConditions");
        }

        [Test]
        [Category("Core")]
        public void StageRuntimeBuilder_Exit_GoalZoneMustBeExactlyExitCenter()
        {
            var multiCellCondition = CreatePlayerAtAnyZoneCondition("goal");
            var multiCellStage = CreateStageWithExitObjective(
                "ExitMultiCellGoal",
                new SurfaceCell(FaceId.Floor, 1, 1),
                CreateRegion(0, 0, 2, 2),
                multiCellCondition);
            AssertBuildThrows(multiCellStage, "must be exactly one center cell", multiCellCondition);

            var mismatchedCondition = CreatePlayerAtAnyZoneCondition("goal");
            var mismatchedStage = CreateStageWithExitObjective(
                "ExitMismatchedGoal",
                new SurfaceCell(FaceId.Floor, 1, 1),
                CreateRegion(2, 1, 2, 1),
                mismatchedCondition);
            AssertBuildThrows(mismatchedStage, "must match goal zone", mismatchedCondition);
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
                CreateTileFeature(
                    100,
                    cell,
                    TileFeatureKind.Barricade,
                    TileFeatureActivationRule.FrontFaceOnly,
                    Direction2D.None,
                    TileFeatureBoxSelector.None),
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
                    CreateTileFeature(
                        101,
                        cell,
                        TileFeatureKind.Destroy,
                        TileFeatureActivationRule.BottomFaceOnly),
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
                var entry = buildResult.ObjectiveRuntimeDefinition.ConditionEntries[0];
                Assert.That(entry.PresentationId, Is.EqualTo(StageObjectiveConditionPresentationIds.ActivateButton));
                Assert.That(entry.StableGroupKey, Is.EqualTo("activate-button|role-0"));
                var runtime = entry.Condition.CreateRuntime();
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
                    CreateTileFeature(
                        100,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        TileFeatureKind.Exit,
                        TileFeatureActivationRule.ActiveFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.None),
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

            AssertBuildThrows(stage, "stage has no MoonBlock source", condition);
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
                var entry = buildResult.ObjectiveRuntimeDefinition.ConditionEntries[0];
                Assert.That(entry.PresentationId, Is.EqualTo(StageObjectiveConditionPresentationIds.ActivateMoonButton));
                Assert.That(entry.StableGroupKey, Is.EqualTo("activate-moon-button|role-0"));
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
                    "NonBoxGravityFieldArchetype",
                    CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                    CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, boxArchetype: BoxArchetype.GravityField)),
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
        public void StageRuntimeBuilder_MoonBlockGeneratorValidation_RejectsInvalidShape()
        {
            var moonSpawn = CreateSpawn(
                20,
                StageSpawnKind.Box,
                new SurfaceCell(FaceId.Floor, 2, 1),
                hp: 1,
                boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                boxArchetype: BoxArchetype.Moon);

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorInvalidActivation",
                    new[]
                    {
                        CreateTileFeature(
                            100,
                            new SurfaceCell(FaceId.Floor, 1, 1),
                            TileFeatureKind.MoonBlockGenerator,
                            TileFeatureActivationRule.Always,
                            boundEntityId: 20),
                    },
                    moonSpawn),
                "MoonBlockGenerator must use BottomFaceOnly activation");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorInvalidDirection",
                    new[]
                    {
                        CreateTileFeature(
                            100,
                            new SurfaceCell(FaceId.Floor, 1, 1),
                            TileFeatureKind.MoonBlockGenerator,
                            TileFeatureActivationRule.BottomFaceOnly,
                            Direction2D.Right,
                            boundEntityId: 20),
                    },
                    moonSpawn),
                "MoonBlockGenerator must use Direction2D.None");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorInvalidSelector",
                    new[]
                    {
                        CreateTileFeature(
                            100,
                            new SurfaceCell(FaceId.Floor, 1, 1),
                            TileFeatureKind.MoonBlockGenerator,
                            TileFeatureActivationRule.BottomFaceOnly,
                            Direction2D.None,
                            TileFeatureBoxSelector.BoundEntity,
                            boundEntityId: 20),
                    },
                    moonSpawn),
                "MoonBlockGenerator must use TileFeatureBoxSelector.None");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorDuplicateSameCell",
                    new[]
                    {
                        CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly, boundEntityId: 20),
                        CreateTileFeature(101, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly, boundEntityId: 20),
                    },
                    moonSpawn),
                "duplicate MoonBlockGenerator");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorMoreThanOne",
                    new[]
                    {
                        CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly, boundEntityId: 20),
                        CreateTileFeature(101, new SurfaceCell(FaceId.Floor, 1, 2), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly, boundEntityId: 20),
                    },
                    moonSpawn),
                "more than one MoonBlockGenerator");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorBoundIdNonPositive",
                    new[]
                    {
                        CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly),
                    },
                    moonSpawn),
                "must bind a positive BoundEntityId");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorBoundIdMissing",
                    new[]
                    {
                        CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly, boundEntityId: 99),
                    },
                    moonSpawn),
                "must reference an existing StageSpawnDefinition");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorBoundIdNonBox",
                    new[]
                    {
                        CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly, boundEntityId: 10),
                    },
                    moonSpawn),
                "must reference a Box spawn");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorBoundIdNormalBox",
                    new[]
                    {
                        CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly, boundEntityId: 21),
                    },
                    moonSpawn,
                    CreateSpawn(21, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 2, 2), hp: 1)),
                "must reference a Moon box spawn");

            AssertBuildThrows(
                CreateMoonBlockGeneratorStage(
                    "MoonGeneratorBoundMoonMissingCapabilities",
                    new[]
                    {
                        CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.MoonBlockGenerator, TileFeatureActivationRule.BottomFaceOnly, boundEntityId: 20),
                    },
                    CreateSpawn(20, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 2, 1), hp: 1, boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Flip, boxArchetype: BoxArchetype.Moon)),
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
        public void StageRuntimeBuilder_AdvancedCampaignStageBuild_ReflectsCurrentConfiguredContract()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");
            Assert.That(stage.PlayerSpawns.Length, Is.EqualTo(1));
            Assert.That(stage.BoxSpawns.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(stage.EnemySpawns.Length, Is.GreaterThanOrEqualTo(5));
            Assert.That(stage.WallSpawns.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(stage.TileFeatures.Length, Is.GreaterThanOrEqualTo(1));

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.BoardBounds.MinInclusive, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(buildResult.BoardBounds.MaxInclusive, Is.EqualTo(new Vector2Int(14, 7)));
            Assert.That(buildResult.InitialTopology.BottomFace, Is.EqualTo(FaceId.Floor));
            var playerSpawn = stage.PlayerSpawns.Single();
            Assert.That(buildResult.PlayerEntityId, Is.EqualTo(playerSpawn.EntityId));
            Assert.That(buildResult.InitialTileFeatures.Length, Is.EqualTo(stage.TileFeatures.Length));
            Assert.That(buildResult.TileFeatureDefinitions.Length, Is.EqualTo(stage.TileFeatures.Length));
            Assert.That(
                buildResult.InitialEntities.Length,
                Is.EqualTo(stage.PlayerSpawns.Length + stage.BoxSpawns.Length + stage.EnemySpawns.Length + stage.WallSpawns.Length));
            AssertEntityIdsAreSorted(buildResult.InitialEntities);

            Assert.That(TryGetEntity(buildResult.InitialEntities, playerSpawn.EntityId, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(playerSpawn.Cell));
            Assert.That(player.facing, Is.EqualTo(playerSpawn.Facing));
            Assert.That(player.unitRole, Is.EqualTo(UnitRole.Player));

            var wallSpawn = stage.WallSpawns.First();
            Assert.That(TryGetEntity(buildResult.InitialEntities, wallSpawn.EntityId, out var wall), Is.True);
            Assert.That(wall.type, Is.EqualTo(EntityType.None));
            Assert.That(wall.position, Is.EqualTo(wallSpawn.Cell));
            Assert.That(wall.unitRole, Is.EqualTo(UnitRole.None));

            var snapshot = GameplayCompositionRoot.CreateWorldState(
                    buildResult.InitialEntities,
                    buildResult.BoardBounds,
                    buildResult.InitialTopology)
                .CreateSnapshot();
            Assert.That(snapshot.TryGetUnitTraversalBlocker(wall.position, out var wallBlocker), Is.True);
            Assert.That(wallBlocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(wallBlocker.EntityId, Is.EqualTo(wall.entityId));

            Assert.That(
                buildResult.InitialEntities.Any(entity =>
                    entity.type == EntityType.Box &&
                    (entity.boxCapabilities & BoxCapabilities.Push) != 0),
                Is.True);
            Assert.That(
                buildResult.InitialEntities.Any(entity =>
                    entity.type == EntityType.Box &&
                    entity.boxArchetype == BoxArchetype.Moon),
                Is.True);

            foreach (var enemySpawn in stage.EnemySpawns)
            {
                Assert.That(TryGetEntity(buildResult.InitialEntities, enemySpawn.EntityId, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(enemySpawn.Cell));
                Assert.That(enemy.facing, Is.EqualTo(enemySpawn.Facing));
                Assert.That(enemy.aiMode, Is.EqualTo(enemySpawn.EnemyAiMode));
                Assert.That(enemy.unitRole, Is.EqualTo(UnitRole.Enemy));
            }

            var expectedProfileOverrideIds = stage.EnemySpawns
                .Where(spawn => spawn.EnemyAiProfile != null)
                .Select(spawn => spawn.EntityId)
                .OrderBy(entityId => entityId)
                .ToArray();
            var actualProfileOverrideIds = buildResult.EnemyAiProfileOverrides
                .Select(profileOverride => profileOverride.EntityId)
                .OrderBy(entityId => entityId)
                .ToArray();
            CollectionAssert.AreEqual(expectedProfileOverrideIds, actualProfileOverrideIds);

            var actualProfileOverrideExports = buildResult.EnemyAiProfileOverrides
                .OrderBy(profileOverride => profileOverride.EntityId)
                .Select(profileOverride => $"{profileOverride.EntityId}:{profileOverride.Profile.name}")
                .ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    "55:EnemyAi_PassiveContactPatroller",
                    "56:EnemyAi_Charger",
                    "57:EnemyAi_WindupProjectile",
                    "58:EnemyAi_GlideChaser",
                    "59:EnemyAi_ArchetypeSummoner",
                    "60:EnemyAi_WallFollower",
                    "84:EnemyAi_JumpChaser",
                    "85:EnemyAi_Charger",
                    "143:EnemyAi_Charger",
                    "144:EnemyAi_JumpChaser",
                    "145:EnemyAi_GravityFieldChaser",
                    "146:EnemyAi_JumpChaser",
                    "147:EnemyAi_GravityFieldChaser",
                    "148:EnemyAi_GlideChaser",
                    "149:EnemyAi_ArchetypeSummoner",
                    "150:EnemyAi_WindupProjectile",
                    "151:EnemyAi_PassiveContactPatroller",
                },
                actualProfileOverrideExports);

            var utilitySummonerProfile = buildResult.EnemyAiProfileOverrides
                .Select(profileOverride => profileOverride.Profile)
                .First(profile => profile.name == "EnemyAi_ArchetypeSummoner");
            var summonBehaviorModule = utilitySummonerProfile.BehaviorModuleAssets.Single();
            Assert.That(summonBehaviorModule, Is.TypeOf<EnemySummonBehaviorModuleAsset>());
            Assert.That(summonBehaviorModule.name, Is.EqualTo("EnemySummonBehaviorModule_ArchetypeSummoner"));
            var utilitySummonerRuntimeDefinition =
                utilitySummonerProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            Assert.That(utilitySummonerRuntimeDefinition.TryGetSummonBehavior(out _), Is.True);
            Assert.That(utilitySummonerRuntimeDefinition.Capabilities.TryGetUtility(out _), Is.False);
            Assert.That(utilitySummonerRuntimeDefinition.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
            Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));
            Assert.That(utilitySummonerProfile.CapabilityAssets, Has.Count.EqualTo(1));
            Assert.That(utilitySummonerProfile.CapabilityAssets[0], Is.AssignableTo<EnemyPassiveContactCapabilityAsset>());
            Assert.That(utilitySummonerProfile.CapabilityAssets[0].name, Is.EqualTo("EnemyCapability_PassiveContact_Common"));
            Assert.That(
                File.ReadAllText(AssetDatabase.GetAssetPath(utilitySummonerProfile)),
                Does.Not.Contain("logicModuleAssets"));

            var presentationDefinition = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(CombinedPresentationAssetPath);
            Assert.That(
                presentationDefinition,
                Is.Not.Null,
                $"Missing stage presentation asset at '{CombinedPresentationAssetPath}'.");

            var presentation = StagePresentationAssembler.Resolve(presentationDefinition);
            var enemySpawnIds = stage.EnemySpawns.Select(spawn => spawn.EntityId).ToHashSet();
            Assert.That(
                presentation.EnemyPresentationBindings.All(binding => enemySpawnIds.Contains(binding.EntityId)),
                Is.True);
            Assert.That(
                presentation.EnemyPresentationBindings.Select(binding => binding.PresentationId).ToArray(),
                Does.Contain(AttackingEnemyPresentationId));
            Assert.That(
                presentation.EnemyPresentationBindings.Select(binding => binding.PresentationId).ToArray(),
                Does.Contain(WallFollowerEnemyPresentationId));
            Assert.That(
                presentation.EnemyPresentationBindings.Select(binding => binding.PresentationId).ToArray(),
                Does.Contain(JumpChaserEnemyPresentationId));
            Assert.That(
                presentation.EnemyPresentationBindings.Select(binding => binding.PresentationId).ToArray(),
                Does.Contain(ChargeEnemyPresentationId));
            Assert.That(
                presentation.EnemyPresentationBindings.Select(binding => binding.PresentationId).ToArray(),
                Does.Contain(UtilitySummonerEnemyPresentationId));
        }

        [Test]
        [Category("Full")]
        public void StageRuntimeBuilder_Stage32Build_ReflectsVfxSfxStageContract()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Stage32StageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{Stage32StageAssetPath}'.");
            Assert.That(stage.PlayerSpawns.Length, Is.EqualTo(1));
            Assert.That(stage.BoxSpawns.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(stage.EnemySpawns.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(stage.WallSpawns.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(stage.TileFeatures, Is.Not.Null);

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.BoardBounds.MinInclusive, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(buildResult.BoardBounds.MaxInclusive, Is.EqualTo(new Vector2Int(14, 7)));
            Assert.That(buildResult.InitialTopology.BottomFace, Is.EqualTo(FaceId.Floor));
            Assert.That(buildResult.PlayerEntityId, Is.EqualTo(Stage32PlayerId));
            Assert.That(
                buildResult.InitialEntities.Length,
                Is.EqualTo(stage.PlayerSpawns.Length + stage.BoxSpawns.Length + stage.EnemySpawns.Length + stage.WallSpawns.Length));
            AssertEntityIdsAreSorted(buildResult.InitialEntities);
            Assert.That(buildResult.InitialTileFeatures.Length, Is.EqualTo(stage.TileFeatures.Length));
            Assert.That(buildResult.TileFeatureDefinitions.Length, Is.EqualTo(stage.TileFeatures.Length));

            Assert.That(TryGetEntity(buildResult.InitialEntities, Stage32PlayerId, out var player), Is.True);
            var playerSpawn = stage.PlayerSpawns.Single(spawn => spawn.EntityId == Stage32PlayerId);
            Assert.That(player.position, Is.EqualTo(playerSpawn.Cell));
            Assert.That(player.facing, Is.EqualTo(playerSpawn.Facing));
            Assert.That(player.unitRole, Is.EqualTo(UnitRole.Player));
            Assert.That(player.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));

            Assert.That(
                buildResult.InitialEntities.Any(entity =>
                    entity.type == EntityType.Box &&
                    (entity.boxCapabilities & BoxCapabilities.Push) != 0),
                Is.True);

            foreach (var enemySpawn in stage.EnemySpawns)
            {
                Assert.That(TryGetEntity(buildResult.InitialEntities, enemySpawn.EntityId, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(enemySpawn.Cell));
                Assert.That(enemy.facing, Is.EqualTo(enemySpawn.Facing));
                Assert.That(enemy.aiMode, Is.EqualTo(enemySpawn.EnemyAiMode));
                Assert.That(enemy.unitRole, Is.EqualTo(UnitRole.Enemy));
                Assert.That(enemy.unitMobilityKind, Is.EqualTo(enemySpawn.UnitMobilityKind));
            }

            Assert.That(buildResult.InitialEntities.Any(entity => entity.type == EntityType.None), Is.True);

            var expectedProfileOverrideIds = stage.EnemySpawns
                .Where(spawn => spawn.EnemyAiProfile != null)
                .Select(spawn => spawn.EntityId)
                .OrderBy(entityId => entityId)
                .ToArray();
            var actualProfileOverrideIds = buildResult.EnemyAiProfileOverrides
                .Select(profileOverride => profileOverride.EntityId)
                .OrderBy(entityId => entityId)
                .ToArray();
            CollectionAssert.AreEqual(expectedProfileOverrideIds, actualProfileOverrideIds);

            var presentationDefinition = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(Stage32PresentationAssetPath);
            Assert.That(
                presentationDefinition,
                Is.Not.Null,
                $"Missing stage presentation asset at '{Stage32PresentationAssetPath}'.");

            var presentation = StagePresentationAssembler.Resolve(presentationDefinition);
            var enemySpawnIds = stage.EnemySpawns.Select(spawn => spawn.EntityId).ToHashSet();
            Assert.That(
                presentation.EnemyPresentationBindings.All(binding => enemySpawnIds.Contains(binding.EntityId)),
                Is.True);
        }

        [Test]
        [Category("Full")]
        public void StageRuntimeBuilder_Stage32Build_MaterializesGlider241AndWall238Contract()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(Stage32StageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{Stage32StageAssetPath}'.");

            var wallCell = new SurfaceCell(FaceId.Floor, 3, 7);
            var gliderSpawn = stage.EnemySpawns.Single(spawn => spawn.EntityId == Stage32GlideEnemyId);
            var wallSpawn = stage.WallSpawns.Single(spawn => spawn.EntityId == Stage32GlideWallId);
            Assert.That(wallSpawn.Cell, Is.EqualTo(wallCell));
            Assert.That(gliderSpawn.EnemyAiProfile, Is.Not.Null);
            Assert.That(gliderSpawn.EnemyAiProfile.name, Is.EqualTo("EnemyAi_GlideChaser"));

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(TryGetEntity(buildResult.InitialEntities, Stage32GlideEnemyId, out var glider), Is.True);
            Assert.That(glider.position, Is.EqualTo(gliderSpawn.Cell));
            Assert.That(glider.unitRole, Is.EqualTo(UnitRole.Enemy));
            Assert.That(glider.unitMobilityKind, Is.EqualTo(gliderSpawn.UnitMobilityKind));
            Assert.That(glider.aiMode, Is.EqualTo(gliderSpawn.EnemyAiMode));

            Assert.That(TryGetEntity(buildResult.InitialEntities, Stage32GlideWallId, out var wall), Is.True);
            Assert.That(wall.position, Is.EqualTo(wallCell));
            Assert.That(wall.type, Is.EqualTo(EntityType.None));

            var worldState = GameplayCompositionRoot.CreateWorldState(
                buildResult.InitialEntities,
                buildResult.BoardBounds,
                buildResult.InitialTopology,
                buildResult.InitialTileFeatures);
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetSolidOccupantAt(wallCell, out var solidOccupant), Is.True);
            Assert.That(solidOccupant.entityId, Is.EqualTo(Stage32GlideWallId));

            Assert.That(TryGetProfileOverride(buildResult, Stage32GlideEnemyId, out var glideProfile), Is.True);
            Assert.That(glideProfile.name, Is.EqualTo("EnemyAi_GlideChaser"));
            Assert.That(glideProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.None));
            var glideDefinition = glideProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            Assert.That(glideDefinition.Capabilities.TryGetMovementSkill(out _), Is.False);
            Assert.That(glideDefinition.TryGetGlideBehavior(out var glide), Is.True);
            Assert.That(glide.Key, Is.EqualTo(EnemyBehaviorModuleKey.Glide));
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_FirstCampaignStagePassiveContactEnemies_BuildsOverridesAndKeepsPatrolMode()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(OnboardingStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{OnboardingStageAssetPath}'.");
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
        public void StagePresentationBindings_AreDeterministicallyOrderedByEntityId()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var enemyBindings = new[]
            {
                new EnemyPresentationBinding { EntityId = 30, PresentationId = "enemy-30" },
                new EnemyPresentationBinding { EntityId = 10, PresentationId = "enemy-10" },
                new EnemyPresentationBinding { EntityId = 20, PresentationId = "enemy-20" },
            };
            var staticBindings = new[]
            {
                new StaticEntityPresentationBinding { EntityId = 50, PresentationId = "static-50" },
                new StaticEntityPresentationBinding { EntityId = 40, PresentationId = "static-40" },
                new StaticEntityPresentationBinding { EntityId = 60, PresentationId = "static-60" },
            };

            try
            {
                SetPrivateField(presentation, "enemyPresentationBindings", enemyBindings);
                SetPrivateField(presentation, "staticEntityPresentationBindings", staticBindings);

                var resolved = StagePresentationAssembler.Resolve(presentation);

                Assert.That(resolved.EnemyPresentationBindings, Is.Not.SameAs(enemyBindings));
                Assert.That(resolved.StaticEntityPresentationBindings, Is.Not.SameAs(staticBindings));
                CollectionAssert.AreEqual(
                    new[] { 10, 20, 30 },
                    Array.ConvertAll(resolved.EnemyPresentationBindings, binding => binding.EntityId));
                CollectionAssert.AreEqual(
                    new[] { 40, 50, 60 },
                    Array.ConvertAll(resolved.StaticEntityPresentationBindings, binding => binding.EntityId));
                Assert.That(resolved.EnemyPresentationBindings[0].PresentationId, Is.EqualTo("enemy-10"));
                Assert.That(resolved.StaticEntityPresentationBindings[0].PresentationId, Is.EqualTo("static-40"));

                enemyBindings[1].PresentationId = "mutated";
                staticBindings[1].PresentationId = "mutated";

                Assert.That(resolved.EnemyPresentationBindings[0].PresentationId, Is.EqualTo("enemy-10"));
                Assert.That(resolved.StaticEntityPresentationBindings[0].PresentationId, Is.EqualTo("static-40"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        public void StagePresentationTileFeatureDirectBindings_AreClonedAndPreserveAuthoredOrder()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var firstPrefab = new GameObject("TileFeatureVisualPrefab_300");
            var secondPrefab = new GameObject("TileFeatureVisualPrefab_100");
            var replacementPrefab = new GameObject("TileFeatureVisualPrefab_Replacement");
            var directBindings = new[]
            {
                new TileFeaturePresentationBinding
                {
                    TileId = 300,
                    VisualPrefab = firstPrefab,
                },
                new TileFeaturePresentationBinding
                {
                    TileId = 100,
                    VisualPrefab = secondPrefab,
                },
            };

            try
            {
                SetPrivateField(presentation, "tileFeaturePresentationBindings", directBindings);

                var resolved = StagePresentationAssembler.Resolve(presentation);

                Assert.That(resolved.TileFeatureBindings, Is.InstanceOf<ReadOnlyCollection<TileFeaturePresentationResolvedBinding>>());
                CollectionAssert.AreEqual(
                    new[] { 300, 100 },
                    ToTileIds(resolved.TileFeatureBindings));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(firstPrefab));
                Assert.That(resolved.TileFeatureBindings[1].VisualPrefab, Is.SameAs(secondPrefab));

                directBindings[0].TileId = 10;
                directBindings[0].VisualPrefab = replacementPrefab;

                CollectionAssert.AreEqual(
                    new[] { 300, 100 },
                    ToTileIds(resolved.TileFeatureBindings));
                Assert.That(resolved.TileFeatureBindings[0].VisualPrefab, Is.SameAs(firstPrefab));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacementPrefab);
                UnityEngine.Object.DestroyImmediate(secondPrefab);
                UnityEngine.Object.DestroyImmediate(firstPrefab);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        public void StagePresentationAssembler_ResolveOverloads_UseSameBindingNormalizationPolicy()
        {
            var stage = CreateStageWithTileFeatures(
                "ResolveOverloadParity",
                new[]
                {
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button),
                    CreateTileFeature(200, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Button),
                });
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var firstPrefab = new GameObject("TileFeatureVisualPrefab_100");
            var secondPrefab = new GameObject("TileFeatureVisualPrefab_200");
            var enemyBindings = new[]
            {
                new EnemyPresentationBinding { EntityId = 30, PresentationId = "enemy-30" },
                new EnemyPresentationBinding { EntityId = 10, PresentationId = "enemy-10" },
                new EnemyPresentationBinding { EntityId = 20, PresentationId = "enemy-20" },
            };
            var staticBindings = new[]
            {
                new StaticEntityPresentationBinding { EntityId = 50, PresentationId = "static-50" },
                new StaticEntityPresentationBinding { EntityId = 40, PresentationId = "static-40" },
            };
            var tileBindings = new[]
            {
                new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = firstPrefab },
                new TileFeaturePresentationBinding { TileId = 200, VisualPrefab = secondPrefab },
            };

            try
            {
                SetPrivateField(presentation, "enemyPresentationBindings", enemyBindings);
                SetPrivateField(presentation, "staticEntityPresentationBindings", staticBindings);
                SetPrivateField(presentation, "tileFeaturePresentationBindings", tileBindings);

                var directOnly = StagePresentationAssembler.Resolve(presentation);
                var gameplayAware = StagePresentationAssembler.Resolve(stage, presentation);

                CollectionAssert.AreEqual(
                    ToEnemyBindingPairs(directOnly.EnemyPresentationBindings),
                    ToEnemyBindingPairs(gameplayAware.EnemyPresentationBindings));
                CollectionAssert.AreEqual(
                    ToStaticBindingPairs(directOnly.StaticEntityPresentationBindings),
                    ToStaticBindingPairs(gameplayAware.StaticEntityPresentationBindings));
                CollectionAssert.AreEqual(
                    ToTileIds(directOnly.TileFeatureBindings),
                    ToTileIds(gameplayAware.TileFeatureBindings));
                Assert.That(directOnly.EnemyPresentationBindings, Is.Not.SameAs(enemyBindings));
                Assert.That(gameplayAware.EnemyPresentationBindings, Is.Not.SameAs(enemyBindings));
                Assert.That(directOnly.StaticEntityPresentationBindings, Is.Not.SameAs(staticBindings));
                Assert.That(gameplayAware.StaticEntityPresentationBindings, Is.Not.SameAs(staticBindings));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondPrefab);
                UnityEngine.Object.DestroyImmediate(firstPrefab);
                UnityEngine.Object.DestroyImmediate(presentation);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void StagePresentationAssembler_GameplayAwareResolve_OrdersTileFeatureBindingsByTileId()
        {
            var stage = CreateStageWithTileFeatures(
                "GameplayAwareTileFeatureOrdering",
                new[]
                {
                    CreateTileFeature(300, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button),
                    CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Button),
                    CreateTileFeature(200, new SurfaceCell(FaceId.Floor, 3, 1), TileFeatureKind.Button),
                });
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var prefab = new GameObject("TileFeatureVisualPrefab");

            try
            {
                SetPrivateField(
                    presentation,
                    "tileFeaturePresentationBindings",
                    new[]
                    {
                        new TileFeaturePresentationBinding { TileId = 300, VisualPrefab = prefab },
                        new TileFeaturePresentationBinding { TileId = 100, VisualPrefab = prefab },
                        new TileFeaturePresentationBinding { TileId = 200, VisualPrefab = prefab },
                    });

                var resolved = StagePresentationAssembler.Resolve(stage, presentation);

                CollectionAssert.AreEqual(
                    new[] { 100, 200, 300 },
                    ToTileIds(resolved.TileFeatureBindings));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(presentation);
                UnityEngine.Object.DestroyImmediate(stage);
            }
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
        public void StagePresentationDefinition_DefaultsBoardPresentationProfileToNull()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                Assert.That(presentation.BoardPresentationProfile, Is.Null);
                Assert.That(presentation.BoardTileStyleCatalog, Is.Null);
                Assert.That(presentation.BoardTilePaintOverrides, Is.Not.Null);
                Assert.That(presentation.BoardTilePaintOverrides, Is.Empty);

                var resolved = StagePresentationAssembler.Resolve(presentation);
                Assert.That(resolved.BoardPresentationProfile, Is.Null);
                Assert.That(resolved.BoardTileStyleCatalog, Is.Null);
                Assert.That(resolved.BoardTilePaintOverrides, Is.Not.Null);
                Assert.That(resolved.BoardTilePaintOverrides, Is.Empty);
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

        [Test]
        [Category("Extended")]
        public void StagePresentationDefinition_ApplyResolvedData_PreservesBoardPresentationProfile()
        {
            var source = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var copy = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var profile = ScriptableObject.CreateInstance<BoardPresentationProfile>();

            try
            {
                SetPrivateField(source, "boardPresentationProfile", profile);

                copy.ApplyResolvedData(StagePresentationAssembler.Resolve(source));
                var roundTrip = StagePresentationAssembler.Resolve(copy);

                Assert.That(roundTrip.BoardPresentationProfile, Is.SameAs(profile));
                Assert.That(copy.BoardPresentationProfile, Is.SameAs(profile));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        [Category("Extended")]
        public void StagePresentationAssembler_ResolvesBoardTileStyleCatalogFromProfileAndPaintOverrides()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var profile = ScriptableObject.CreateInstance<BoardPresentationProfile>();
            var styleCatalog = ScriptableObject.CreateInstance<BoardTileStyleCatalog>();

            try
            {
                SetPrivateField(profile, "defaultBoardTileStyleCatalog", styleCatalog);
                SetPrivateField(presentation, "boardPresentationProfile", profile);
                SetPrivateField(
                    presentation,
                    "boardTilePaintOverrides",
                    new[]
                    {
                        new BoardTilePaintOverride(new SurfaceCell(FaceId.Floor, 0, 0), "paint-key"),
                    });

                var resolved = StagePresentationAssembler.Resolve(presentation);

                Assert.That(resolved.BoardPresentationProfile, Is.SameAs(profile));
                Assert.That(resolved.BoardTileStyleCatalog, Is.SameAs(styleCatalog));
                Assert.That(resolved.BoardTilePaintOverrides, Is.InstanceOf<ReadOnlyCollection<BoardTilePaintOverride>>());
                Assert.That(resolved.BoardTilePaintOverrides, Has.Count.EqualTo(1));
                Assert.That(resolved.BoardTilePaintOverrides[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(resolved.BoardTilePaintOverrides[0].StyleKey, Is.EqualTo("paint-key"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(styleCatalog);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        [Category("Extended")]
        public void StagePresentationDefinition_ApplyResolvedData_PreservesBoardTilePaintOverrides()
        {
            var source = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var copy = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                SetPrivateField(
                    source,
                    "boardTilePaintOverrides",
                    new[]
                    {
                        new BoardTilePaintOverride(new SurfaceCell(FaceId.Front, 1, 0), "front-style"),
                    });

                copy.ApplyResolvedData(StagePresentationAssembler.Resolve(source));
                var roundTrip = StagePresentationAssembler.Resolve(copy);

                Assert.That(roundTrip.BoardTilePaintOverrides, Has.Count.EqualTo(1));
                Assert.That(roundTrip.BoardTilePaintOverrides[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
                Assert.That(roundTrip.BoardTilePaintOverrides[0].StyleKey, Is.EqualTo("front-style"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void AssertRuntimeBuildResultsMatch(
            StageRuntimeBuildResult expected,
            StageRuntimeBuildResult actual)
        {
            Assert.That(actual.BoardBounds.IsBounded, Is.EqualTo(expected.BoardBounds.IsBounded));
            Assert.That(actual.BoardBounds.MinInclusive, Is.EqualTo(expected.BoardBounds.MinInclusive));
            Assert.That(actual.BoardBounds.MaxInclusive, Is.EqualTo(expected.BoardBounds.MaxInclusive));
            Assert.That(actual.InitialTopology, Is.EqualTo(expected.InitialTopology));
            CollectionAssert.AreEqual(expected.InitialEntities, actual.InitialEntities);
            CollectionAssert.AreEqual(expected.InitialTileFeatures, actual.InitialTileFeatures);
            CollectionAssert.AreEqual(expected.TileFeatureDefinitions, actual.TileFeatureDefinitions);
            CollectionAssert.AreEqual(expected.MoonBlockRespawnDefinitions, actual.MoonBlockRespawnDefinitions);
            Assert.That(actual.PlayerEntityId, Is.EqualTo(expected.PlayerEntityId));
            AssertObjectiveRuntimeDefinitionsMatch(
                expected.ObjectiveRuntimeDefinition,
                actual.ObjectiveRuntimeDefinition);
            Assert.That(actual.EnemyAiProfileOverrides.Length, Is.EqualTo(expected.EnemyAiProfileOverrides.Length));
            for (var i = 0; i < expected.EnemyAiProfileOverrides.Length; i++)
            {
                Assert.That(
                    actual.EnemyAiProfileOverrides[i].EntityId,
                    Is.EqualTo(expected.EnemyAiProfileOverrides[i].EntityId));
                Assert.That(
                    actual.EnemyAiProfileOverrides[i].Profile,
                    Is.SameAs(expected.EnemyAiProfileOverrides[i].Profile));
            }
        }

        private static void AssertObjectiveRuntimeDefinitionsMatch(
            StageObjectiveRuntimeDefinition expected,
            StageObjectiveRuntimeDefinition actual)
        {
            Assert.That(actual.CompletionPolicy, Is.EqualTo(expected.CompletionPolicy));
            Assert.That(actual.PlayerEntityId, Is.EqualTo(expected.PlayerEntityId));
            Assert.That(actual.Zones.Count, Is.EqualTo(expected.Zones.Count));
            Assert.That(actual.ConditionEntries.Count, Is.EqualTo(expected.ConditionEntries.Count));
            for (var i = 0; i < expected.ConditionEntries.Count; i++)
            {
                Assert.That(
                    actual.ConditionEntries[i].StableConditionId,
                    Is.EqualTo(expected.ConditionEntries[i].StableConditionId));
                Assert.That(
                    actual.ConditionEntries[i].PresentationId,
                    Is.EqualTo(expected.ConditionEntries[i].PresentationId));
                Assert.That(
                    actual.ConditionEntries[i].StableGroupKey,
                    Is.EqualTo(expected.ConditionEntries[i].StableGroupKey));
                Assert.That(
                    actual.ConditionEntries[i].SortOrder,
                    Is.EqualTo(expected.ConditionEntries[i].SortOrder));
                Assert.That(
                    actual.ConditionEntries[i].AuthoringOrder,
                    Is.EqualTo(expected.ConditionEntries[i].AuthoringOrder));
            }
        }

        private static int[] ToTileIds(IReadOnlyList<TileFeaturePresentationResolvedBinding> bindings)
        {
            var tileIds = new int[bindings.Count];
            for (var i = 0; i < bindings.Count; i++)
            {
                tileIds[i] = bindings[i].TileId;
            }

            return tileIds;
        }

        private static string[] ToEnemyBindingPairs(IReadOnlyList<EnemyPresentationBinding> bindings)
        {
            var pairs = new string[bindings.Count];
            for (var i = 0; i < bindings.Count; i++)
            {
                pairs[i] = $"{bindings[i].EntityId}:{bindings[i].PresentationId}";
            }

            return pairs;
        }

        private static string[] ToStaticBindingPairs(IReadOnlyList<StaticEntityPresentationBinding> bindings)
        {
            var pairs = new string[bindings.Count];
            for (var i = 0; i < bindings.Count; i++)
            {
                pairs[i] = $"{bindings[i].EntityId}:{bindings[i].PresentationId}";
            }

            return pairs;
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

        private static StageDefinition CreateMoonBlockGeneratorStage(
            string stageName,
            StageTileFeatureDefinition[] tileFeatures,
            params StageSpawnDefinition[] additionalSpawns)
        {
            var spawns = new List<StageSpawnDefinition>
            {
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3),
            };
            if (additionalSpawns != null)
            {
                spawns.AddRange(additionalSpawns);
            }

            var stage = CreateStage(
                stageName,
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                spawns.ToArray());
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

        private static StageDefinition CreateStageWithExitObjective(
            string stageName,
            SurfaceCell exitCell,
            StageZoneRegionDefinition goalRegion,
            PlayerAtAnyZoneConditionAsset primaryGoalCondition,
            TileFeatureActivationRule activationRule = TileFeatureActivationRule.ActiveFaceOnly,
            Direction2D direction = Direction2D.None,
            TileFeatureBoxSelector boxSelector = TileFeatureBoxSelector.None,
            StageTileFeatureDefinition[] extraTileFeatures = null)
        {
            var tileFeatures = new List<StageTileFeatureDefinition>
            {
                CreateTileFeature(
                    100,
                    exitCell,
                    TileFeatureKind.Exit,
                    activationRule,
                    direction,
                    boxSelector),
            };
            if (extraTileFeatures != null)
            {
                tileFeatures.AddRange(extraTileFeatures);
            }

            var stage = CreateStageWithObjective(
                stageName,
                new[]
                {
                    CreateConditionEntry(
                        primaryGoalCondition,
                        required: true,
                        StageObjectiveConditionRole.PrimaryGoal,
                        "primary-goal"),
                },
                tileFeatures.ToArray());
            SetPrivateField(stage, "zones", new[]
            {
                new StageZoneDefinition
                {
                    ZoneId = "goal",
                    FaceId = exitCell.face,
                    Regions = new[] { goalRegion },
                },
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
                AuthoringLabel = string.Empty,
                SortOrder = 0,
            };
        }

        private static StageObjectiveConditionEntry CreateConditionEntry(
            StageConditionAsset condition,
            bool required,
            StageObjectiveConditionRole role,
            string stableConditionId)
        {
            return new StageObjectiveConditionEntry
            {
                Condition = condition,
                Required = required,
                Role = role,
                StableConditionId = stableConditionId,
                AuthoringLabel = string.Empty,
                SortOrder = 0,
            };
        }

        private static PlayerAtAnyZoneConditionAsset CreatePlayerAtAnyZoneCondition(string zoneId)
        {
            var condition = ScriptableObject.CreateInstance<PlayerAtAnyZoneConditionAsset>();
            SetPrivateField(condition, "zoneIds", new[] { zoneId });
            SetPrivateField(condition, "requireAlive", true);
            return condition;
        }

        private static StageZoneRegionDefinition CreateRegion(int minX, int minY, int maxX, int maxY)
        {
            return new StageZoneRegionDefinition
            {
                MinInclusive = new Vector2Int(minX, minY),
                MaxInclusive = new Vector2Int(maxX, maxY),
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
            string unitStackGroup = null,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return new StageSpawnDefinition
            {
                EntityId = entityId,
                Kind = kind,
                Cell = cell,
                Facing = facing,
                Hp = hp,
                UnitMobilityKind = unitMobilityKind,
                BoxCapabilities = boxCapabilities,
                BoxArchetype = boxArchetype,
                EnemyAiMode = enemyAiMode,
                EnemyAiStateTimer = enemyAiStateTimer,
                EnemyAiProfile = ResolveTestEnemyAiProfile(kind, enemyAiMode, enemyAiProfile),
                UnitStackGroup = unitStackGroup,
            };
        }

        private static EnemyAiProfile ResolveTestEnemyAiProfile(
            StageSpawnKind kind,
            EnemyAiMode enemyAiMode,
            EnemyAiProfile enemyAiProfile)
        {
            if (kind != StageSpawnKind.Enemy ||
                enemyAiMode == EnemyAiMode.None ||
                enemyAiProfile != null)
            {
                return enemyAiProfile;
            }

            return AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(TutorialEnemyProfileAssetPath);
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

        private static bool TryGetTileFeature(
            IReadOnlyList<TileFeatureState> features,
            int tileId,
            out TileFeatureState feature)
        {
            for (var i = 0; i < features.Count; i++)
            {
                var entry = features[i];
                if (entry.TileId != tileId)
                {
                    continue;
                }

                feature = entry;
                return true;
            }

            feature = default;
            return false;
        }

        private static bool TryGetTileFeatureDefinition(
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                var entry = definitions[i];
                if (entry.TileId != tileId)
                {
                    continue;
                }

                definition = entry;
                return true;
            }

            definition = default;
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
