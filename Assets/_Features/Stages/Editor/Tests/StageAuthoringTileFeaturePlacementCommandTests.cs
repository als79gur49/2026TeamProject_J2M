using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEditor;
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
        public void AddEntrance_DefaultsBottomFaceNoneSelectorNone()
        {
            var feature = StageAuthoringPlacementCommands.CreateTileFeaturePreset(
                TileFeatureKind.Entrance,
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
        public void AddEntrance_RequiresPlayerPlacementCell()
        {
            WithAuthoring(authoring =>
            {
                var entrance = CreateFeature(0, TileFeatureKind.Entrance, new SurfaceCell(FaceId.Floor, 0, 0));

                var added = StageAuthoringPlacementCommands.TryAddTileFeature(
                    authoring,
                    entrance.Cell,
                    entrance,
                    out _);

                Assert.That(added, Is.False);
            });
        }

        [Test]
        public void AddEntrance_RejectsMismatchedPlayerPlacementCell()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetPlacements(new[] { CreatePlayerPlacement(new SurfaceCell(FaceId.Floor, 0, 0)) });
                var entrance = CreateFeature(0, TileFeatureKind.Entrance, new SurfaceCell(FaceId.Floor, 1, 0));

                var added = StageAuthoringPlacementCommands.TryAddTileFeature(
                    authoring,
                    entrance.Cell,
                    entrance,
                    out var error);

                Assert.That(added, Is.False);
                Assert.That(error, Does.Contain("player placement cell"));
            });
        }

        [Test]
        public void AddEntrance_RejectsSecondEntrance()
        {
            WithAuthoring(authoring =>
            {
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);
                authoring.SetPlacements(new[] { CreatePlayerPlacement(cell) });
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Entrance, cell),
                });

                var added = StageAuthoringPlacementCommands.TryAddTileFeature(
                    authoring,
                    cell,
                    CreateFeature(0, TileFeatureKind.Entrance, cell),
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

        [Test]
        public void DuplicateSelectedPlacementToTarget_CopiesSelectedEntityWithNewStableGuid()
        {
            var profile = ScriptableObject.CreateInstance<EnemyAiProfile>();
            try
            {
                WithAuthoring(authoring =>
                {
                    var original = new StagePlacedEntityAuthoring
                    {
                        StableGuid = "original-guid",
                        DisplayName = "Enemy Source",
                        Kind = StageAuthoringEntityKind.Enemy,
                        Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                        Facing = Direction.Right,
                        Hp = 7,
                        UnitMobilityKind = UnitMobilityKind.Air,
                        UnitStackGroup = "stack-a",
                        BoxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
                        BoxArchetype = BoxArchetype.GravityField,
                        EnemyAiMode = EnemyAiMode.Chase,
                        EnemyAiStateTimer = 12,
                        EnemyAiProfileOverride = profile,
                        PresentationId = "enemy-presentation",
                    };
                    authoring.SetPlacements(new[] { original });
                    var selection = new StageAuthoringGridSelectionState();
                    selection.SelectPlacement(0, authoring.Placements);
                    selection.SetTargetCell(FaceId.Front, new Vector2Int(2, 1));
                    var serialized = new SerializedObject(authoring);

                    var result = StageAuthoringPlacementCommands.DuplicateSelectedPlacementToTarget(
                        serialized,
                        authoring,
                        selection,
                        0);
                    serialized.ApplyModifiedProperties();
                    serialized.Update();

                    Assert.That(result.Changed, Is.True);
                    Assert.That(result.HasPlacementSelection, Is.True);
                    Assert.That(result.SelectPlacementIndex, Is.EqualTo(1));
                    Assert.That(authoring.Placements, Has.Count.EqualTo(2));
                    var duplicate = authoring.Placements[1];
                    Assert.That(duplicate.Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 2, 1)));
                    Assert.That(duplicate.StableGuid, Is.Not.EqualTo(original.StableGuid));
                    Assert.That(duplicate.StableGuid, Is.Not.Empty);
                    Assert.That(duplicate.Kind, Is.EqualTo(original.Kind));
                    Assert.That(duplicate.Facing, Is.EqualTo(original.Facing));
                    Assert.That(duplicate.Hp, Is.EqualTo(original.Hp));
                    Assert.That(duplicate.UnitMobilityKind, Is.EqualTo(original.UnitMobilityKind));
                    Assert.That(duplicate.UnitStackGroup, Is.EqualTo(original.UnitStackGroup));
                    Assert.That(duplicate.BoxCapabilities, Is.EqualTo(original.BoxCapabilities));
                    Assert.That(duplicate.BoxArchetype, Is.EqualTo(original.BoxArchetype));
                    Assert.That(duplicate.EnemyAiMode, Is.EqualTo(original.EnemyAiMode));
                    Assert.That(duplicate.EnemyAiStateTimer, Is.EqualTo(original.EnemyAiStateTimer));
                    Assert.That(duplicate.EnemyAiProfileOverride, Is.SameAs(original.EnemyAiProfileOverride));
                    Assert.That(duplicate.PresentationId, Is.EqualTo(original.PresentationId));

                    selection.SelectPlacement(result.SelectPlacementIndex, authoring.Placements);
                    Assert.That(selection.ResolveSelectedPlacementIndex(authoring.Placements), Is.EqualTo(1));
                });
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void DuplicateSelectedPlacementToTarget_RejectsOccupiedEntityTarget()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetPlacements(new[]
                {
                    new StagePlacedEntityAuthoring
                    {
                        StableGuid = "source",
                        DisplayName = "Source",
                        Kind = StageAuthoringEntityKind.Box,
                        Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                        Hp = 1,
                    },
                    new StagePlacedEntityAuthoring
                    {
                        StableGuid = "occupied",
                        DisplayName = "Occupied",
                        Kind = StageAuthoringEntityKind.Box,
                        Cell = new SurfaceCell(FaceId.Floor, 1, 0),
                        Hp = 1,
                    },
                });
                var selection = new StageAuthoringGridSelectionState();
                selection.SelectPlacement(0, authoring.Placements);
                selection.SetTargetCell(FaceId.Floor, new Vector2Int(1, 0));
                var serialized = new SerializedObject(authoring);

                var result = StageAuthoringPlacementCommands.DuplicateSelectedPlacementToTarget(
                    serialized,
                    authoring,
                    selection,
                    0);
                serialized.ApplyModifiedProperties();
                serialized.Update();

                Assert.That(result.Changed, Is.False);
                Assert.That(authoring.Placements, Has.Count.EqualTo(2));
                Assert.That(authoring.Placements[0].StableGuid, Is.EqualTo("source"));
                Assert.That(authoring.Placements[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            });
        }

        [Test]
        public void DuplicateSelectedTileFeatureToTarget_CopiesSelectedTileFeatureWithNewTileId()
        {
            WithAuthoring(authoring =>
            {
                var original = CreateFeature(3, TileFeatureKind.Button, new SurfaceCell(FaceId.Floor, 0, 0));
                original.ActivationRule = TileFeatureActivationRule.ActiveFaceOnly;
                original.Direction = Direction2D.Left;
                original.BoxSelector = TileFeatureBoxSelector.BoundEntity;
                original.BoundEntityId = 42;
                original.PresentationKey = "button-key";
                authoring.SetTileFeatures(new[] { original });

                var duplicated = StageAuthoringPlacementCommands.TryDuplicateSelectedTileFeatureToTarget(
                    authoring,
                    new SurfaceCell(FaceId.Front, 1, 1),
                    0,
                    out var duplicatedTileId,
                    out var error);

                Assert.That(duplicated, Is.True, error);
                Assert.That(duplicatedTileId, Is.Not.EqualTo(original.TileId));
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(2));
                var duplicate = authoring.TileFeatures.Single(feature => feature.TileId == duplicatedTileId);
                Assert.That(duplicate.Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 1)));
                Assert.That(duplicate.Kind, Is.EqualTo(original.Kind));
                Assert.That(duplicate.ActivationRule, Is.EqualTo(original.ActivationRule));
                Assert.That(duplicate.Direction, Is.EqualTo(original.Direction));
                Assert.That(duplicate.BoxSelector, Is.EqualTo(original.BoxSelector));
                Assert.That(duplicate.BoundEntityId, Is.EqualTo(original.BoundEntityId));
                Assert.That(duplicate.PresentationKey, Is.EqualTo(original.PresentationKey));

                var selection = new StageAuthoringGridSelectionState();
                selection.SelectTileFeatureById(duplicatedTileId, authoring.TileFeatures);
                Assert.That(selection.ResolveSelectedTileFeatureIndex(authoring.TileFeatures), Is.EqualTo(1));
            });
        }

        [Test]
        public void DuplicateSelectedTileFeatureToTarget_RejectsGlobalDuplicateKinds()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Exit, new SurfaceCell(FaceId.Floor, 0, 0)),
                });

                var duplicated = StageAuthoringPlacementCommands.TryDuplicateSelectedTileFeatureToTarget(
                    authoring,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    0,
                    out _,
                    out _);

                Assert.That(duplicated, Is.False);
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
            });

            WithAuthoring(authoring =>
            {
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);
                authoring.SetPlacements(new[] { CreatePlayerPlacement(cell) });
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Entrance, cell),
                    CreateFeature(2, TileFeatureKind.MoonBlockGenerator, new SurfaceCell(FaceId.Floor, 1, 0)),
                });

                var duplicated = StageAuthoringPlacementCommands.TryDuplicateSelectedTileFeatureToTarget(
                    authoring,
                    cell,
                    0,
                    out _,
                    out _);

                Assert.That(duplicated, Is.False);
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(2));
            });

            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.MoonBlockGenerator, new SurfaceCell(FaceId.Floor, 0, 0)),
                });

                var duplicated = StageAuthoringPlacementCommands.TryDuplicateSelectedTileFeatureToTarget(
                    authoring,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    0,
                    out _,
                    out _);

                Assert.That(duplicated, Is.False);
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void DuplicateSelectedTileFeatureToTarget_RejectsSameCellSlideAndBarricadeDuplicates()
        {
            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Slide, new SurfaceCell(FaceId.Floor, 0, 0)),
                });

                var duplicated = StageAuthoringPlacementCommands.TryDuplicateSelectedTileFeatureToTarget(
                    authoring,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    0,
                    out _,
                    out _);

                Assert.That(duplicated, Is.False);
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
            });

            WithAuthoring(authoring =>
            {
                authoring.SetTileFeatures(new[]
                {
                    CreateFeature(1, TileFeatureKind.Barricade, new SurfaceCell(FaceId.Floor, 0, 0)),
                });

                var duplicated = StageAuthoringPlacementCommands.TryDuplicateSelectedTileFeatureToTarget(
                    authoring,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    0,
                    out _,
                    out _);

                Assert.That(duplicated, Is.False);
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void DuplicateSelectedTileFeatureToTarget_DoesNotCopyDirectVisualBinding()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var prefab = new GameObject("DirectTileFeatureBindingPrefab");
            try
            {
                WithAuthoring(authoring =>
                {
                    var original = CreateFeature(1, TileFeatureKind.Button, new SurfaceCell(FaceId.Floor, 0, 0));
                    original.PresentationKey = "button-key";
                    authoring.SetTileFeatures(new[] { original });
                    SetPrivateField(
                        presentation,
                        "tileFeaturePresentationBindings",
                        new[] { new TileFeaturePresentationBinding { TileId = original.TileId, VisualPrefab = prefab } });

                    var duplicated = StageAuthoringPlacementCommands.TryDuplicateSelectedTileFeatureToTarget(
                        authoring,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        0,
                        out var duplicatedTileId,
                        out var error);

                    Assert.That(duplicated, Is.True, error);
                    Assert.That(authoring.TileFeatures.Single(feature => feature.TileId == duplicatedTileId).PresentationKey, Is.EqualTo("button-key"));
                    Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                    Assert.That(presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(original.TileId));
                    Assert.That(
                        presentation.TileFeaturePresentationBindings.Any(binding => binding.TileId == duplicatedTileId),
                        Is.False);
                });
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        public void DuplicateSelectedTileFeatureToTarget_DoesNotRewriteObjectiveConditionTileId()
        {
            var condition = ScriptableObject.CreateInstance<ButtonActivatedConditionAsset>();
            try
            {
                WithAuthoring(authoring =>
                {
                    SetPrivateField(condition, "tileId", 1);
                    authoring.SetTileFeatures(new[]
                    {
                        CreateFeature(1, TileFeatureKind.Button, new SurfaceCell(FaceId.Floor, 0, 0)),
                    });
                    authoring.SetObjective(new StageObjectiveAuthoring
                    {
                        CompletionPolicy = Game.Feature.Gameplay.Objectives.StageCompletionPolicy.RequireAllConditions,
                        ConditionEntries = new[]
                        {
                            new StageObjectiveConditionEntry
                            {
                                Condition = condition,
                                Required = true,
                                Role = Game.Feature.Gameplay.Objectives.StageObjectiveConditionRole.SecondaryGoal,
                                StableConditionId = "button-condition",
                            },
                        },
                    });

                    var duplicated = StageAuthoringPlacementCommands.TryDuplicateSelectedTileFeatureToTarget(
                        authoring,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        0,
                        out var duplicatedTileId,
                        out var error);

                    Assert.That(duplicated, Is.True, error);
                    Assert.That(duplicatedTileId, Is.Not.EqualTo(condition.TileId));
                    Assert.That(condition.TileId, Is.EqualTo(1));
                    Assert.That(authoring.Objective.GetConditionEntriesOrEmpty().Single().Condition, Is.SameAs(condition));
                    Assert.That(
                        ((ButtonActivatedConditionAsset)authoring.Objective.GetConditionEntriesOrEmpty().Single().Condition).TileId,
                        Is.EqualTo(1));
                });
            }
            finally
            {
                Object.DestroyImmediate(condition);
            }
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
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

        private static StagePlacedEntityAuthoring CreatePlayerPlacement(SurfaceCell cell)
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = "player",
                DisplayName = "Player",
                Kind = StageAuthoringEntityKind.Player,
                Cell = cell,
                Facing = Direction.Right,
                Hp = 1,
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
