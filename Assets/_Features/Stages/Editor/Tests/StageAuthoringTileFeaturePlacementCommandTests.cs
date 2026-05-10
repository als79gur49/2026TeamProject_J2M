using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringTileFeaturePlacementCommandTests
    {
        [Test]
        public void StageAuthoringDefinition_DefaultTileFeatures_Empty()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                Assert.That(authoring.TileFeatures, Is.Not.Null);
                Assert.That(authoring.TileFeatures, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void StageAuthoringDefinition_SetTileFeatures_NormalizesNullToEmpty()
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                authoring.SetTileFeatures(null);

                Assert.That(authoring.TileFeatures, Is.Not.Null);
                Assert.That(authoring.TileFeatures, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(authoring);
            }
        }

        [Test]
        public void StageAuthoringDefinition_CustomInspector_ExposesTileFeaturesOrToolEntry()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Editor/Authoring/StageAuthoringDefinitionEditor.cs");

            Assert.That(source, Does.Contain("FindProperty(\"tileFeatures\")"));
            Assert.That(source, Does.Contain("Open Grid / TileFeature Editor"));
        }

        [Test]
        public void StageAuthoringDefinition_CustomInspector_OpenGeneratedAssetButtons_AreNotDuplicated()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Editor/Authoring/StageAuthoringDefinitionEditor.cs");

            Assert.That(CountOccurrences(source, "Open Presentation Definition"), Is.EqualTo(1));
            Assert.That(CountOccurrences(source, "Open BoardTile Catalog"), Is.EqualTo(1));
            Assert.That(CountOccurrences(source, "Open TileFeature Catalog"), Is.EqualTo(1));
        }

        [Test]
        public void AddTileFeature_AllocatesPositiveUniqueTileId()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(10, TileFeatureKind.Button, new SurfaceCell(FaceId.Floor, 0, 0)),
                });

                var added = TryAddButton(authoring, new SurfaceCell(FaceId.Floor, 1, 0));

                Assert.That(added, Is.True);
                Assert.That(authoring.TileFeatures.Select(feature => feature.TileId), Does.Contain(11));
            });
        }

        [Test]
        public void AddTileFeature_UsesSurfaceCellFace()
        {
            WithAuthoring(authoring =>
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 0);

                Assert.That(TryAddButton(authoring, cell), Is.True);

                Assert.That(authoring.TileFeatures.Single().Cell, Is.EqualTo(cell));
            });
        }

        [Test]
        public void AddTileFeature_AllowsSameCellAsEntity()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetPlacements(new[]
                {
                    new StagePlacedEntityAuthoring
                    {
                        StableGuid = "box",
                        DisplayName = "Box",
                        Kind = StageAuthoringEntityKind.Box,
                        Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                        Hp = 1,
                    },
                });

                Assert.That(TryAddButton(authoring, new SurfaceCell(FaceId.Floor, 0, 0)), Is.True);
                Assert.That(authoring.Placements, Has.Count.EqualTo(1));
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void AddTileFeature_RejectsDuplicateTileId()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Button, new SurfaceCell(FaceId.Floor, 0, 0)),
                });
                var duplicate = CreateFeature(1, TileFeatureKind.Button, new SurfaceCell(FaceId.Floor, 1, 0));

                var added = StageAuthoringPlacementCommands.TryAddTileFeature(
                    authoring,
                    duplicate.Cell,
                    duplicate,
                    out _);

                Assert.That(added, Is.False);
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void AddTileFeature_RejectsBoundsOutside()
        {
            WithAuthoring(authoring =>
            {
                var added = TryAddButton(authoring, new SurfaceCell(FaceId.Floor, 5, 5));

                Assert.That(added, Is.False);
                Assert.That(authoring.TileFeatures, Is.Empty);
            });
        }

        [Test]
        public void RemoveTileFeature_ByTileId()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Button, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateFeature(2, TileFeatureKind.Destroy, new SurfaceCell(FaceId.Floor, 1, 0)),
                });

                var removed = StageAuthoringPlacementCommands.TryRemoveTileFeature(authoring, 1, out _);

                Assert.That(removed, Is.True);
                Assert.That(authoring.TileFeatures.Select(feature => feature.TileId), Is.EquivalentTo(new[] { 2 }));
            });
        }

        [Test]
        public void RemoveTileFeaturesAt_RemovesOnlyTileFeaturesNotEntities()
        {
            WithAuthoring(authoring =>
            {
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);
                authoring.SetPlacements(new[]
                {
                    new StagePlacedEntityAuthoring
                    {
                        StableGuid = "box",
                        DisplayName = "Box",
                        Kind = StageAuthoringEntityKind.Box,
                        Cell = cell,
                        Hp = 1,
                    },
                });
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Button, cell),
                });

                var removed = StageAuthoringPlacementCommands.TryRemoveTileFeaturesAt(
                    authoring,
                    cell,
                    out var removedCount);

                Assert.That(removed, Is.True);
                Assert.That(removedCount, Is.EqualTo(1));
                Assert.That(authoring.Placements, Has.Count.EqualTo(1));
                Assert.That(authoring.TileFeatures, Is.Empty);
            });
        }

        [Test]
        public void UpdateTileFeature_PreservesTileId()
        {
            WithAuthoring(authoring =>
            {
                var original = CreateFeature(1, TileFeatureKind.Button, new SurfaceCell(FaceId.Floor, 0, 0));
                authoring.SetTileFeatures(new[] { original });
                var updated = original;
                updated.PresentationKey = "updated-key";

                var changed = StageAuthoringPlacementCommands.TryUpdateTileFeature(authoring, updated, out _);

                Assert.That(changed, Is.True);
                Assert.That(authoring.TileFeatures.Single().TileId, Is.EqualTo(1));
                Assert.That(authoring.TileFeatures.Single().PresentationKey, Is.EqualTo("updated-key"));
            });
        }

        [Test]
        public void AddDestroyTile_DefaultsBottomFaceNoneSelectorNone()
        {
            var feature = StageAuthoringPlacementCommands.CreateTileFeaturePreset(
                TileFeatureKind.Destroy,
                new SurfaceCell(FaceId.Floor, 0, 0),
                TileFeatureActivationRule.Always,
                Direction2D.Left,
                TileFeatureBoxSelector.AnyPushableBox,
                99,
                string.Empty);

            Assert.That(feature.ActivationRule, Is.EqualTo(TileFeatureActivationRule.BottomFaceOnly));
            Assert.That(feature.Direction, Is.EqualTo(Direction2D.None));
            Assert.That(feature.BoxSelector, Is.EqualTo(TileFeatureBoxSelector.None));
            Assert.That(feature.BoundEntityId, Is.EqualTo(0));
        }

        [Test]
        public void AddDestroyTile_PreservesSupportedFrontFaceActivation()
        {
            var feature = StageAuthoringPlacementCommands.CreateTileFeaturePreset(
                TileFeatureKind.Destroy,
                new SurfaceCell(FaceId.Front, 0, 0),
                TileFeatureActivationRule.FrontFaceOnly,
                Direction2D.Left,
                TileFeatureBoxSelector.AnyPushableBox,
                99,
                string.Empty);

            Assert.That(feature.ActivationRule, Is.EqualTo(TileFeatureActivationRule.FrontFaceOnly));
            Assert.That(feature.Direction, Is.EqualTo(Direction2D.None));
            Assert.That(feature.BoxSelector, Is.EqualTo(TileFeatureBoxSelector.None));
            Assert.That(feature.BoundEntityId, Is.EqualTo(0));
        }

        [Test]
        public void UpdateDestroyTile_AllowsFrontFaceActivation()
        {
            WithAuthoring(authoring =>
            {
                var original = CreateFeature(1, TileFeatureKind.Destroy, new SurfaceCell(FaceId.Floor, 0, 0));
                authoring.SetTileFeatures(new[] { original });
                var updated = original;
                updated.ActivationRule = TileFeatureActivationRule.FrontFaceOnly;

                var changed = StageAuthoringPlacementCommands.TryUpdateTileFeature(authoring, updated, out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(authoring.TileFeatures.Single().ActivationRule, Is.EqualTo(TileFeatureActivationRule.FrontFaceOnly));
            });
        }

        [Test]
        public void AddSlideTile_RequiresCardinalDirection()
        {
            WithAuthoring(authoring =>
            {
                var feature = StageAuthoringPlacementCommands.CreateTileFeaturePreset(
                    TileFeatureKind.Slide,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    TileFeatureActivationRule.BottomFaceOnly,
                    Direction2D.None,
                    TileFeatureBoxSelector.None,
                    0,
                    string.Empty);

                var added = StageAuthoringPlacementCommands.TryAddTileFeature(
                    authoring,
                    feature.Cell,
                    feature,
                    out _);

                Assert.That(added, Is.False);
            });
        }

        [Test]
        public void AddBarricade_DefaultsFrontFaceNoneSelectorNone()
        {
            var feature = StageAuthoringPlacementCommands.CreateTileFeaturePreset(
                TileFeatureKind.Barricade,
                new SurfaceCell(FaceId.Floor, 0, 0),
                TileFeatureActivationRule.BottomFaceOnly,
                Direction2D.Left,
                TileFeatureBoxSelector.AnyPushableBox,
                99,
                string.Empty);

            Assert.That(feature.ActivationRule, Is.EqualTo(TileFeatureActivationRule.FrontFaceOnly));
            Assert.That(feature.Direction, Is.EqualTo(Direction2D.None));
            Assert.That(feature.BoxSelector, Is.EqualTo(TileFeatureBoxSelector.None));
            Assert.That(feature.BoundEntityId, Is.EqualTo(0));
        }

        [Test]
        public void AddExit_RejectsSecondExit()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Exit, new SurfaceCell(FaceId.Floor, 0, 0)),
                });

                var added = StageAuthoringPlacementCommands.TryAddTileFeature(
                    authoring,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    CreateFeature(0, TileFeatureKind.Exit, new SurfaceCell(FaceId.Floor, 1, 0)),
                    out _);

                Assert.That(added, Is.False);
            });
        }

        [Test]
        public void AddMoonBlockGenerator_RequiresMoonBoundEntity()
        {
            WithAuthoring(authoring =>
            {
                var generator = CreateFeature(
                    0,
                    TileFeatureKind.MoonBlockGenerator,
                    new SurfaceCell(FaceId.Floor, 0, 0));
                generator.BoundEntityId = 0;

                var added = StageAuthoringPlacementCommands.TryAddTileFeature(
                    authoring,
                    generator.Cell,
                    generator,
                    out _);

                Assert.That(added, Is.False);
            });
        }

        private static bool TryAddButton(StageAuthoringDefinition authoring, SurfaceCell cell)
        {
            return StageAuthoringPlacementCommands.TryAddTileFeature(
                authoring,
                cell,
                StageAuthoringPlacementCommands.CreateTileFeaturePreset(
                    TileFeatureKind.Button,
                    cell,
                    TileFeatureActivationRule.BottomFaceOnly,
                    Direction2D.None,
                    TileFeatureBoxSelector.AnyPushableBox,
                    0,
                    string.Empty),
                out _);
        }

        private static int CountOccurrences(string source, string value)
        {
            return source.Split(new[] { value }, System.StringSplitOptions.None).Length - 1;
        }

        private static StageTileFeatureDefinition CreateFeature(
            int tileId,
            TileFeatureKind kind,
            SurfaceCell cell)
        {
            var activationRule = kind == TileFeatureKind.Slide || kind == TileFeatureKind.Barricade
                ? TileFeatureActivationRule.FrontFaceOnly
                : TileFeatureActivationRule.BottomFaceOnly;
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = cell,
                Kind = kind,
                ActivationRule = activationRule,
                Direction = kind == TileFeatureKind.Slide ? Direction2D.Right : Direction2D.None,
                BoxSelector = kind == TileFeatureKind.Button ? TileFeatureBoxSelector.AnyPushableBox : TileFeatureBoxSelector.None,
                BoundEntityId = kind == TileFeatureKind.MoonBlockGenerator ? 100 : 0,
                PresentationKey = string.Empty,
            };
        }

        private static void WithAuthoring(System.Action<StageAuthoringDefinition> action)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            try
            {
                authoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = new Vector2Int(0, 0),
                    MaxInclusive = new Vector2Int(2, 2),
                    InitialBottomFace = FaceId.Floor,
                });
                action(authoring);
            }
            finally
            {
                Object.DestroyImmediate(authoring);
            }
        }
    }
}
