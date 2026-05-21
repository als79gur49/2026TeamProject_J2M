using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringGridWindowTests
    {
        [Test]
        public void EmptyTargetCellClick_KeepsSelectedPlacement()
        {
            var placements = new[]
            {
                Placement("a", FaceId.Floor, 0, 0),
                Placement("b", FaceId.Floor, 1, 0),
            };
            var state = new StageAuthoringGridSelectionState();

            state.SelectCell(FaceId.Floor, new Vector2Int(0, 0), placements);
            state.SelectCell(FaceId.Floor, new Vector2Int(3, 3), placements);

            Assert.That(state.TargetCell, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(state.ResolveSelectedPlacementIndex(placements), Is.EqualTo(0));
        }

        [Test]
        public void OccupiedCellClick_SelectsPlacementAtTarget()
        {
            var placements = new[]
            {
                Placement("a", FaceId.Floor, 0, 0),
                Placement("b", FaceId.Floor, 1, 0),
            };
            var state = new StageAuthoringGridSelectionState();

            state.SelectCell(FaceId.Floor, new Vector2Int(0, 0), placements);
            state.SelectCell(FaceId.Floor, new Vector2Int(1, 0), placements);

            Assert.That(state.TargetCell, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(state.ResolveSelectedPlacementIndex(placements), Is.EqualTo(1));
        }

        [Test]
        public void MoveSelectedHere_MovesSelectedPlacementToEmptyTarget()
        {
            WithWindow(
                new[]
                {
                    Placement("a", FaceId.Floor, 0, 0),
                    Placement("b", FaceId.Floor, 1, 0),
                },
                (window, authoring) =>
                {
                    window.SelectCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                    window.SelectCellForTests(FaceId.Floor, new Vector2Int(2, 2));

                    window.MoveSelectedPlacementToTargetCellForTests();

                    var moved = authoring.Placements.Single(placement => placement.StableGuid == "a");
                    Assert.That(moved.Cell.face, Is.EqualTo(FaceId.Floor));
                    Assert.That(moved.Cell.x, Is.EqualTo(2));
                    Assert.That(moved.Cell.y, Is.EqualTo(2));
                    Assert.That(window.ResolveSelectedPlacementIndexForTests(), Is.EqualTo(0));
                });
        }

        [Test]
        public void MoveSelectedHere_DoesNotMoveIntoOccupiedTarget()
        {
            WithWindow(
                new[]
                {
                    Placement("a", FaceId.Floor, 0, 0),
                    Placement("b", FaceId.Floor, 1, 0),
                },
                (window, authoring) =>
                {
                    window.SelectCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                    window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(1, 0));

                    window.MoveSelectedPlacementToTargetCellForTests();

                    var unmoved = authoring.Placements.Single(placement => placement.StableGuid == "a");
                    Assert.That(unmoved.Cell.x, Is.EqualTo(0));
                    Assert.That(unmoved.Cell.y, Is.EqualTo(0));
                });
        }

        [Test]
        public void AddPlacement_DoesNotAddIntoOccupiedTarget()
        {
            WithWindow(
                new[] { Placement("a", FaceId.Floor, 0, 0) },
                (window, authoring) =>
                {
                    window.SelectCellForTests(FaceId.Floor, new Vector2Int(0, 0));

                    window.AddPlacementAtTargetCellForTests();

                    Assert.That(authoring.Placements, Has.Count.EqualTo(1));
                });
        }

        [Test]
        public void AddPlacement_SelectsNewPlacementAtEmptyTarget()
        {
            WithWindow(
                new[] { Placement("a", FaceId.Floor, 0, 0) },
                (window, authoring) =>
                {
                    window.SelectCellForTests(FaceId.Floor, new Vector2Int(2, 2));

                    window.AddPlacementAtTargetCellForTests();

                    Assert.That(authoring.Placements, Has.Count.EqualTo(2));
                    Assert.That(window.ResolveSelectedPlacementIndexForTests(), Is.EqualTo(1));
                    Assert.That(authoring.Placements[1].Cell.x, Is.EqualTo(2));
                    Assert.That(authoring.Placements[1].Cell.y, Is.EqualTo(2));
                });
        }

        [Test]
        public void DeleteSelected_DeletesSelectionAndKeepsTargetCell()
        {
            WithWindow(
                new[]
                {
                    Placement("a", FaceId.Floor, 0, 0),
                    Placement("b", FaceId.Floor, 1, 0),
                },
                (window, authoring) =>
                {
                    window.SelectCellForTests(FaceId.Floor, new Vector2Int(0, 0));

                    window.DeleteSelectedPlacementForTests();

                    Assert.That(authoring.Placements.Select(placement => placement.StableGuid), Is.EquivalentTo(new[] { "b" }));
                    Assert.That(window.ResolveSelectedPlacementIndexForTests(), Is.EqualTo(-1));
                    Assert.That(window.TargetCellForTests, Is.EqualTo(new Vector2Int(0, 0)));
                });
        }

        [Test]
        public void FaceChange_DoesNotClearSelectedPlacement()
        {
            var placements = new[] { Placement("a", FaceId.Floor, 0, 0) };
            var state = new StageAuthoringGridSelectionState();

            state.SelectCell(FaceId.Floor, new Vector2Int(0, 0), placements);
            state.SetTargetFace(FaceId.Front);

            Assert.That(state.TargetFace, Is.EqualTo(FaceId.Front));
            Assert.That(state.ResolveSelectedPlacementIndex(placements), Is.EqualTo(0));
        }

        [Test]
        public void StableGuidSelection_SurvivesArrayReorder()
        {
            var original = new[]
            {
                Placement("a", FaceId.Floor, 0, 0),
                Placement("b", FaceId.Floor, 1, 0),
            };
            var reordered = new[]
            {
                Placement("b", FaceId.Floor, 1, 0),
                Placement("a", FaceId.Floor, 0, 0),
            };
            var state = new StageAuthoringGridSelectionState();

            state.SelectPlacement(1, original);

            Assert.That(state.ResolveSelectedPlacementIndex(reordered), Is.EqualTo(0));
        }

        [Test]
        public void GenerateButton_StoresGenerationReport()
        {
            WithWindow(
                new[] { Placement("a", FaceId.Floor, 0, 0) },
                (window, _) =>
                {
                    window.GenerateForTests();

                    Assert.That(window.LastReportForTests, Is.Not.Null);
                    Assert.That(window.LastReportForTests.HasErrors, Is.True);
                });
        }

        [Test]
        public void LegendFocusToggle_PreservesSelectionAndTargetCell()
        {
            WithWindow(
                new[]
                {
                    Placement("a", FaceId.Floor, 0, 0),
                    Placement("b", FaceId.Floor, 1, 0),
                },
                (window, _) =>
                {
                    window.SelectCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                    window.SetTargetCellForTests(FaceId.Front, new Vector2Int(9, 9));

                    window.ToggleGridFocusKindForTests(StageAuthoringEntityKind.Enemy);

                    Assert.That(window.FocusedGridKindForTests, Is.EqualTo(StageAuthoringEntityKind.Enemy));
                    Assert.That(window.ResolveSelectedPlacementIndexForTests(), Is.EqualTo(0));
                    Assert.That(window.TargetFaceForTests, Is.EqualTo(FaceId.Front));
                    Assert.That(window.TargetCellForTests, Is.EqualTo(new Vector2Int(9, 9)));

                    window.ToggleGridFocusKindForTests(StageAuthoringEntityKind.Enemy);

                    Assert.That(window.FocusedGridKindForTests, Is.Null);
                    Assert.That(window.ResolveSelectedPlacementIndexForTests(), Is.EqualTo(0));
                    Assert.That(window.TargetFaceForTests, Is.EqualTo(FaceId.Front));
                    Assert.That(window.TargetCellForTests, Is.EqualTo(new Vector2Int(9, 9)));
                });
        }

        [Test]
        public void TileFeatureMode_AddsTileFeatureIntoEntityOccupiedTarget()
        {
            WithWindow(
                new[] { Placement("a", FaceId.Floor, 0, 0) },
                (window, authoring) =>
                {
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));

                    window.AddTileFeatureAtTargetCellForTests();

                    Assert.That(authoring.Placements, Has.Count.EqualTo(1));
                    Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
                    Assert.That(authoring.TileFeatures[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                    Assert.That(window.SelectedTileFeatureIdForTests, Is.EqualTo(authoring.TileFeatures[0].TileId));
                });
        }

        [Test]
        public void TileFeatureMode_AddTileFeature_AllowedWhileAnotherTileFeatureIsSelected()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    authoring.SetTileFeatures(new[] { TileFeature(1, FaceId.Floor, 0, 0) });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(1);
                    window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(1, 1));
                    window.SetTileFeatureDraftForTests(
                        TileFeatureKind.Slide,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.Right,
                        TileFeatureBoxSelector.None,
                        0);

                    window.AddTileFeatureAtTargetCellForTests();

                    Assert.That(authoring.TileFeatures, Has.Count.EqualTo(2));
                    var added = authoring.TileFeatures.Single(feature => feature.TileId != 1);
                    Assert.That(added.Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
                    Assert.That(added.Kind, Is.EqualTo(TileFeatureKind.Button));
                    Assert.That(added.ActivationRule, Is.EqualTo(TileFeatureActivationRule.BottomFaceOnly));
                    Assert.That(added.BoxSelector, Is.EqualTo(TileFeatureBoxSelector.AnyPushableBox));
                    Assert.That(added.Direction, Is.EqualTo(Direction2D.None));
                    Assert.That(added.PresentationKey, Is.Empty);
                    Assert.That(window.SelectedTileFeatureIdForTests, Is.EqualTo(added.TileId));
                });
        }

        [Test]
        public void TileFeatureMode_AddTileFeature_UsesDefaultButtonTemplate()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SetTileFeatureDraftForTests(
                        TileFeatureKind.Slide,
                        TileFeatureActivationRule.FrontFaceOnly,
                        Direction2D.Right,
                        TileFeatureBoxSelector.None,
                        0,
                        "slide-key");
                    window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(1, 1));

                    window.AddTileFeatureAtTargetCellForTests();

                    var added = authoring.TileFeatures.Single();
                    Assert.That(added.Kind, Is.EqualTo(TileFeatureKind.Button));
                    Assert.That(added.ActivationRule, Is.EqualTo(TileFeatureActivationRule.BottomFaceOnly));
                    Assert.That(added.BoxSelector, Is.EqualTo(TileFeatureBoxSelector.AnyPushableBox));
                    Assert.That(added.Direction, Is.EqualTo(Direction2D.None));
                    Assert.That(added.PresentationKey, Is.Empty);
                    Assert.That(window.SelectedTileFeatureIdForTests, Is.EqualTo(added.TileId));
                });
        }

        [Test]
        public void TileFeatureMode_SelectedKindChangeSavesImmediately()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    authoring.SetTileFeatures(new[] { TileFeature(1, FaceId.Floor, 0, 0) });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(1);

                    var changed = window.SetSelectedTileFeatureKindForTests(TileFeatureKind.Slide);

                    Assert.That(changed, Is.True);
                    var updated = authoring.TileFeatures.Single();
                    Assert.That(updated.Kind, Is.EqualTo(TileFeatureKind.Slide));
                    Assert.That(updated.Direction, Is.EqualTo(Direction2D.Right));
                });
        }

        [Test]
        public void TileFeatureMode_CatalogPresentationKeyChangeDoesNotLoseSelectedKind()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    authoring.SetTileFeatures(new[] { TileFeature(1, FaceId.Floor, 0, 0) });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(1);
                    Assert.That(window.SetSelectedTileFeatureKindForTests(TileFeatureKind.Slide), Is.True);

                    var changed = window.SetSelectedTileFeatureCatalogPresentationKeyForTests("slide-key");

                    Assert.That(changed, Is.True);
                    var updated = authoring.TileFeatures.Single();
                    Assert.That(updated.Kind, Is.EqualTo(TileFeatureKind.Slide));
                    Assert.That(updated.Direction, Is.EqualTo(Direction2D.Right));
                    Assert.That(updated.PresentationKey, Is.EqualTo("slide-key"));
                });
        }

        [Test]
        public void TileFeatureMode_InvalidSelectedKindChangeRevertsStoredFeature()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    authoring.SetTileFeatures(new[]
                    {
                        TileFeature(1, FaceId.Floor, 0, 0),
                        TileFeature(
                            2,
                            FaceId.Floor,
                            0,
                            0,
                            TileFeatureKind.Slide,
                            TileFeatureActivationRule.FrontFaceOnly,
                            Direction2D.Right,
                            TileFeatureBoxSelector.None),
                    });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(1);

                    var changed = window.SetSelectedTileFeatureKindForTests(TileFeatureKind.Slide);

                    Assert.That(changed, Is.False);
                    var unchanged = authoring.TileFeatures.Single(feature => feature.TileId == 1);
                    Assert.That(unchanged.Kind, Is.EqualTo(TileFeatureKind.Button));
                    Assert.That(unchanged.ActivationRule, Is.EqualTo(TileFeatureActivationRule.BottomFaceOnly));
                    Assert.That(unchanged.BoxSelector, Is.EqualTo(TileFeatureBoxSelector.AnyPushableBox));
                });
        }

        [Test]
        public void TileFeatureMode_SourceDoesNotExposeEraseModeUi()
        {
            var windowSource = System.IO.File.ReadAllText(
                "Assets/_Features/Stages/Editor/Authoring/StageAuthoringGridWindow.cs");

            Assert.That(windowSource, Does.Not.Contain("Erase Mode"));
            Assert.That(windowSource, Does.Not.Contain("Erase Cell TileFeatures"));
            Assert.That(windowSource, Does.Not.Contain("tileFeatureEraseMode"));
            Assert.That(windowSource, Does.Not.Contain("New TileFeature"));
            Assert.That(windowSource, Does.Not.Contain("WithPreviewTileId"));
            Assert.That(windowSource, Does.Not.Contain("Apply Update"));
            Assert.That(windowSource, Does.Not.Contain("ApplySelectedTileFeatureUpdateForTests"));
        }

        [Test]
        public void ZoneMode_CreateSingleCellZone_UpdatesAuthoringDefinition()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    window.SetEditModeForTests(StageAuthoringGridEditMode.ZoneEditing);
                    window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(1, 1));
                    window.SetZoneCreateDraftForTests("goal", FaceId.Floor);

                    var changed = window.AddSingleCellZoneAtTargetForTests();

                    Assert.That(changed, Is.True);
                    Assert.That(authoring.Zones, Has.Count.EqualTo(1));
                    Assert.That(authoring.Zones[0].ZoneId, Is.EqualTo("goal"));
                    Assert.That(authoring.Zones[0].Regions.Single().MinInclusive, Is.EqualTo(new Vector2Int(1, 1)));
                    Assert.That(window.SelectedZoneIdForTests, Is.EqualTo("goal"));
                });
        }

        [Test]
        public void ZoneMode_CreateRectangleZone_UpdatesAuthoringDefinition()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 4, 4));
                    window.SetEditModeForTests(StageAuthoringGridEditMode.ZoneEditing);
                    window.SetZoneCreateDraftForTests("room", FaceId.Front);
                    window.SetZoneRectangleForTests(FaceId.Front, new Vector2Int(3, 3), new Vector2Int(1, 1));

                    var changed = window.AddRectangleZoneForTests();

                    Assert.That(changed, Is.True);
                    var region = authoring.Zones.Single().Regions.Single();
                    Assert.That(authoring.Zones.Single().FaceId, Is.EqualTo(FaceId.Front));
                    Assert.That(region.MinInclusive, Is.EqualTo(new Vector2Int(1, 1)));
                    Assert.That(region.MaxInclusive, Is.EqualTo(new Vector2Int(3, 3)));
                });
        }

        [Test]
        public void ZoneMode_SelectExistingZone_ShowsSelection()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetZones(new[]
                    {
                        Zone("a", FaceId.Floor, 0, 0, 0, 0),
                        Zone("b", FaceId.Floor, 1, 1, 1, 1),
                    });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.ZoneEditing);

                    window.SelectZoneByIdForTests("b");

                    Assert.That(window.ResolveSelectedZoneIndexForTests(), Is.EqualTo(1));
                    Assert.That(window.SelectedZoneIdForTests, Is.EqualTo("b"));
                });
        }

        [Test]
        public void ZoneMode_DeleteSelectedZone_RemovesZone()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetZones(new[] { Zone("goal", FaceId.Floor, 0, 0, 0, 0) });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.ZoneEditing);
                    window.SelectZoneByIdForTests("goal");

                    var changed = window.DeleteSelectedZoneForTests();

                    Assert.That(changed, Is.True);
                    Assert.That(authoring.Zones, Is.Empty);
                    Assert.That(window.SelectedZoneIdForTests, Is.Empty);
                });
        }

        [Test]
        public void ZoneMode_DuplicateSelectedZone_CreatesNewZoneId()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    authoring.SetZones(new[] { Zone("goal", FaceId.Floor, 0, 0, 1, 1) });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.ZoneEditing);
                    window.SelectZoneByIdForTests("goal");

                    var changed = window.DuplicateSelectedZoneForTests("goal-copy");

                    Assert.That(changed, Is.True);
                    Assert.That(authoring.Zones, Has.Count.EqualTo(2));
                    Assert.That(authoring.Zones[1].ZoneId, Is.EqualTo("goal-copy"));
                    Assert.That(authoring.Zones[1].Regions.Single().MaxInclusive, Is.EqualTo(new Vector2Int(1, 1)));
                });
        }

        [Test]
        public void ZoneMode_UpdateSelectedZone_ChangesFirstRegion()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 3, 3));
                    authoring.SetZones(new[] { Zone("goal", FaceId.Floor, 0, 0, 0, 0) });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.ZoneEditing);
                    window.SelectZoneByIdForTests("goal");

                    var changed = window.UpdateSelectedZoneFirstRegionForTests(
                        "goal-renamed",
                        FaceId.Front,
                        new Vector2Int(1, 1),
                        new Vector2Int(2, 2));

                    Assert.That(changed, Is.True);
                    var zone = authoring.Zones.Single();
                    Assert.That(zone.ZoneId, Is.EqualTo("goal-renamed"));
                    Assert.That(zone.FaceId, Is.EqualTo(FaceId.Front));
                    Assert.That(zone.Regions.Single().MinInclusive, Is.EqualTo(new Vector2Int(1, 1)));
                });
        }

        [Test]
        public void ZoneMode_SelectionDoesNotCorruptPlacementOrTileFeatureSelection()
        {
            WithWindow(
                new[] { Placement("p", FaceId.Floor, 0, 0) },
                (window, authoring) =>
                {
                    authoring.SetTileFeatures(new[] { TileFeature(1, FaceId.Floor, 0, 0) });
                    authoring.SetZones(new[] { Zone("z", FaceId.Floor, 0, 0, 0, 0) });
                    window.SelectCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                    window.SelectTileFeatureByIdForTests(1);
                    window.SetEditModeForTests(StageAuthoringGridEditMode.ZoneEditing);

                    window.SelectZoneByIdForTests("z");

                    Assert.That(window.ResolveSelectedPlacementIndexForTests(), Is.EqualTo(0));
                    Assert.That(window.ResolveSelectedTileFeatureIndexForTests(), Is.EqualTo(0));
                    Assert.That(window.ResolveSelectedZoneIndexForTests(), Is.EqualTo(0));
                });
        }

        [Test]
        public void TileFeatureMode_MoveSelectedHere_MovesSelectedTileFeatureToTargetAndPreservesData()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    authoring.SetTileFeatures(new[] { TileFeature(7, FaceId.Floor, 0, 0, "button-key") });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(7);
                    window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(2, 2));

                    window.MoveSelectedTileFeatureToTargetCellForTests();

                    Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
                    var moved = authoring.TileFeatures.Single();
                    Assert.That(moved.TileId, Is.EqualTo(7));
                    Assert.That(moved.Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 2)));
                    Assert.That(moved.Kind, Is.EqualTo(TileFeatureKind.Button));
                    Assert.That(moved.ActivationRule, Is.EqualTo(TileFeatureActivationRule.BottomFaceOnly));
                    Assert.That(moved.Direction, Is.EqualTo(Direction2D.None));
                    Assert.That(moved.BoxSelector, Is.EqualTo(TileFeatureBoxSelector.AnyPushableBox));
                    Assert.That(moved.PresentationKey, Is.EqualTo("button-key"));
                    Assert.That(window.SelectedTileFeatureIdForTests, Is.EqualTo(7));
                });
        }

        [Test]
        public void TileFeatureMode_MoveSelectedHere_PreservesDirectVisualBinding()
        {
            WithTileFeatureWindow((window, authoring, presentation, prefab) =>
            {
                authoring.SetBoard(Board(0, 0, 2, 2));
                window.SelectTileFeatureByIdForTests(1);
                window.SetSelectedTileFeatureVisualPrefabForTests(prefab);
                Assert.That(window.SetSelectedTileFeatureVisualBindingForTests(out var error), Is.True, error);
                window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(1, 1));

                window.MoveSelectedTileFeatureToTargetCellForTests();

                Assert.That(authoring.TileFeatures.Single(feature => feature.TileId == 1).Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
                Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                Assert.That(presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(1));
                Assert.That(presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(prefab));
            });
        }

        [Test]
        public void TileFeatureMode_MoveSelectedHere_AllowsEntityOccupiedTarget()
        {
            WithWindow(
                new[] { Placement("entity", FaceId.Floor, 1, 1) },
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    authoring.SetTileFeatures(new[] { TileFeature(1, FaceId.Floor, 0, 0) });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(1);
                    window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(1, 1));

                    window.MoveSelectedTileFeatureToTargetCellForTests();

                    Assert.That(authoring.Placements, Has.Count.EqualTo(1));
                    Assert.That(authoring.TileFeatures.Single().Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
                });
        }

        [Test]
        public void TileFeatureMode_DeleteSelected_RemovesOnlySelectedTileFeature()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetBoard(Board(0, 0, 2, 2));
                    authoring.SetTileFeatures(new[]
                    {
                        TileFeature(1, FaceId.Floor, 0, 0),
                        TileFeature(2, FaceId.Floor, 0, 0),
                    });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(1);

                    window.DeleteSelectedTileFeatureForTests();

                    Assert.That(authoring.TileFeatures.Select(feature => feature.TileId), Is.EqualTo(new[] { 2 }));
                    Assert.That(window.ResolveSelectedTileFeatureIndexForTests(), Is.EqualTo(-1));
                });
        }

        [Test]
        public void TileFeatureMode_SelectedCellListUsesCurrentFaceOnly()
        {
            WithWindow(
                new[] { Placement("a", FaceId.Floor, 0, 0) },
                (window, authoring) =>
                {
                    authoring.SetTileFeatures(new[]
                    {
                        new StageTileFeatureDefinition
                        {
                            TileId = 1,
                            Cell = new SurfaceCell(FaceId.Floor, 0, 0),
                            Kind = TileFeatureKind.Button,
                            ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                            Direction = Direction2D.None,
                            BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                        },
                        new StageTileFeatureDefinition
                        {
                            TileId = 2,
                            Cell = new SurfaceCell(FaceId.Front, 0, 0),
                            Kind = TileFeatureKind.Button,
                            ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                            Direction = Direction2D.None,
                            BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                        },
                    });

                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));

                    Assert.That(window.CountTileFeaturesAtTargetForTests(), Is.EqualTo(1));

                    window.SetTargetCellForTests(FaceId.Front, new Vector2Int(0, 0));

                    Assert.That(window.CountTileFeaturesAtTargetForTests(), Is.EqualTo(1));
                });
        }

        [Test]
        public void TileFeatureMode_SelectedTileFeatureShowsMissingBindingStatus()
        {
            WithTileFeatureWindow((window, authoring, presentation, prefab) =>
            {
                window.SelectTileFeatureByIdForTests(1);

                var status = window.GetSelectedTileFeatureVisualBindingStatusForTests();

                Assert.That(status.Kind, Is.EqualTo(TileFeatureVisualBindingStatusKind.MissingBinding));
                Assert.That(status.VisualPrefab, Is.Null);
                Assert.That(presentation.TileFeaturePresentationBindings, Is.Empty);
            });
        }

        [Test]
        public void TileFeatureMode_SelectedTileFeatureShowsBoundPrefabStatus()
        {
            WithTileFeatureWindow((window, authoring, presentation, prefab) =>
            {
                window.SelectTileFeatureByIdForTests(1);
                Assert.That(window.SetSelectedTileFeatureVisualBindingForTests(out var error), Is.False, error);
                window.SetSelectedTileFeatureVisualPrefabForTests(prefab);

                Assert.That(window.SetSelectedTileFeatureVisualBindingForTests(out error), Is.True, error);
                var status = window.GetSelectedTileFeatureVisualBindingStatusForTests();

                Assert.That(status.Kind, Is.EqualTo(TileFeatureVisualBindingStatusKind.Bound));
                Assert.That(status.VisualPrefab, Is.SameAs(prefab));
            });
        }

        [Test]
        public void TileFeatureMode_SetBindingButtonUpdatesStagePresentationDefinition()
        {
            WithTileFeatureWindow((window, authoring, presentation, prefab) =>
            {
                window.SelectTileFeatureByIdForTests(1);
                window.SetSelectedTileFeatureVisualPrefabForTests(prefab);

                var changed = window.SetSelectedTileFeatureVisualBindingForTests(out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(presentation.TileFeaturePresentationBindings, Has.Length.EqualTo(1));
                Assert.That(presentation.TileFeaturePresentationBindings[0].TileId, Is.EqualTo(1));
                Assert.That(presentation.TileFeaturePresentationBindings[0].VisualPrefab, Is.SameAs(prefab));
            });
        }

        [Test]
        public void TileFeatureMode_RemoveBindingButtonRemovesBinding()
        {
            WithTileFeatureWindow((window, authoring, presentation, prefab) =>
            {
                window.SelectTileFeatureByIdForTests(1);
                window.SetSelectedTileFeatureVisualPrefabForTests(prefab);
                Assert.That(window.SetSelectedTileFeatureVisualBindingForTests(out _), Is.True);

                var removed = window.RemoveSelectedTileFeatureVisualBindingForTests(out var error);

                Assert.That(removed, Is.True, error);
                Assert.That(presentation.TileFeaturePresentationBindings, Is.Empty);
            });
        }

        [Test]
        public void TileFeatureMode_NoPresentationDefinitionDisablesBindingEditorStatus()
        {
            WithWindow(
                System.Array.Empty<StagePlacedEntityAuthoring>(),
                (window, authoring) =>
                {
                    authoring.SetTileFeatures(new[] { TileFeature(1, FaceId.Floor, 0, 0) });
                    window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                    window.SelectTileFeatureByIdForTests(1);

                    var status = window.GetSelectedTileFeatureVisualBindingStatusForTests();

                    Assert.That(status.Kind, Is.EqualTo(TileFeatureVisualBindingStatusKind.NoPresentationDefinition));
                    Assert.That(status.Message, Is.EqualTo("No StagePresentationDefinition assigned; visual binding editing disabled."));
                });
        }

        [Test]
        public void TileFeatureMode_GameplayTileFeatureEditKeepsVisualBindingIndependent()
        {
            WithTileFeatureWindow((window, authoring, presentation, prefab) =>
            {
                window.SelectTileFeatureByIdForTests(1);
                window.SetSelectedTileFeatureVisualPrefabForTests(prefab);
                Assert.That(window.SetSelectedTileFeatureVisualBindingForTests(out _), Is.True);
                window.SetTileFeatureDraftForTests(
                    TileFeatureKind.Button,
                    TileFeatureActivationRule.BottomFaceOnly,
                    Direction2D.None,
                    TileFeatureBoxSelector.AnyPushableBox,
                    0,
                    "gameplay-key");

                var selected = authoring.TileFeatures.Single(feature => feature.TileId == 1);
                var updated = selected;
                updated.PresentationKey = "gameplay-key";
                Assert.That(StageAuthoringPlacementCommands.TryUpdateTileFeature(authoring, updated, out _), Is.True);

                Assert.That(authoring.TileFeatures.Single(feature => feature.TileId == 1).PresentationKey, Is.EqualTo("gameplay-key"));
                Assert.That(presentation.TileFeaturePresentationBindings.Single().VisualPrefab, Is.SameAs(prefab));
            });
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTileOverrideDropdown_WritesPresentationOverride()
        {
            WithBoardTileWindow((window, authoring, presentation, catalog, material) =>
            {
                window.SetEditModeForTests(StageAuthoringGridEditMode.BoardTilePrefabOverride);
                window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                window.SetBoardTilePresentationKeyForTests("cell-key");

                var changed = window.SetBoardTileOverrideForTests(out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(presentation.BoardTilePresentationOverrides.Count, Is.EqualTo(1));
                Assert.That(presentation.BoardTilePresentationOverrides[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(presentation.BoardTilePresentationOverrides[0].PresentationKey, Is.EqualTo("cell-key"));
            });
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTileOverride_ClearRemovesOnlySelectedCell()
        {
            WithBoardTileWindow((window, authoring, presentation, catalog, material) =>
            {
                authoring.SetBoard(new StageBoardDefinition
                {
                    MinInclusive = Vector2Int.zero,
                    MaxInclusive = new Vector2Int(1, 0),
                    InitialBottomFace = FaceId.Floor,
                });
                window.SetEditModeForTests(StageAuthoringGridEditMode.BoardTilePrefabOverride);
                window.SetBoardTilePresentationKeyForTests("cell-key");
                window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                Assert.That(window.SetBoardTileOverrideForTests(out _), Is.True);
                window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(1, 0));
                Assert.That(window.SetBoardTileOverrideForTests(out _), Is.True);

                window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                var cleared = window.ClearBoardTileOverrideForTests(out var error);

                Assert.That(cleared, Is.True, error);
                Assert.That(presentation.BoardTilePresentationOverrides.Count, Is.EqualTo(1));
                Assert.That(presentation.BoardTilePresentationOverrides[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            });
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTileOverride_AllowsEntityAndTileFeatureSameCell()
        {
            WithBoardTileWindow((window, authoring, presentation, catalog, material) =>
            {
                authoring.SetPlacements(new[] { Placement("occupied", FaceId.Floor, 0, 0) });
                authoring.SetTileFeatures(new[] { TileFeature(1, FaceId.Floor, 0, 0) });
                window.SetEditModeForTests(StageAuthoringGridEditMode.BoardTilePrefabOverride);
                window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                window.SetBoardTilePresentationKeyForTests("cell-key");

                var changed = window.SetBoardTileOverrideForTests(out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(authoring.Placements, Has.Count.EqualTo(1));
                Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
                Assert.That(presentation.BoardTilePresentationOverrides.Count, Is.EqualTo(1));
            });
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTileOverride_DisabledWhenNoCatalog()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            try
            {
                WithWindow(
                    System.Array.Empty<StagePlacedEntityAuthoring>(),
                    (window, authoring) =>
                    {
                        authoring.AssignGeneratedDefinitions(null, presentation);
                        window.SetEditModeForTests(StageAuthoringGridEditMode.BoardTilePrefabOverride);
                        window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                        window.SetBoardTilePresentationKeyForTests("cell-key");

                        var status = window.GetBoardTileOverrideStatusForTests();
                        var changed = window.SetBoardTileOverrideForTests(out _);

                        Assert.That(status.Kind, Is.EqualTo(BoardTilePresentationOverrideStatusKind.CatalogMissing));
                        Assert.That(changed, Is.False);
                        Assert.That(presentation.BoardTilePresentationOverrides, Is.Empty);
                    });
            }
            finally
            {
                Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTilePaint_NullProfileReportsCatalogMissingWithoutException()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            try
            {
                WithWindow(
                    System.Array.Empty<StagePlacedEntityAuthoring>(),
                    (window, authoring) =>
                    {
                        authoring.AssignGeneratedDefinitions(null, presentation);
                        window.SetEditModeForTests(StageAuthoringGridEditMode.BoardTilePaint);
                        window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));

                        var status = window.GetBoardTilePaintStatusForTests();
                        var options = window.GetBoardTileStyleOptionsForTests();

                        Assert.That(status.Kind, Is.EqualTo(BoardTilePaintOverrideStatusKind.CatalogMissing));
                        Assert.That(options, Is.Empty);
                    });
            }
            finally
            {
                Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTileOverlay_NullProfileReportsCatalogMissingWithoutException()
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            try
            {
                WithWindow(
                    System.Array.Empty<StagePlacedEntityAuthoring>(),
                    (window, authoring) =>
                    {
                        authoring.AssignGeneratedDefinitions(null, presentation);
                        window.SetEditModeForTests(StageAuthoringGridEditMode.BoardTileOverlay);
                        window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));

                        var status = window.GetBoardTileOverlayStatusForTests();
                        var options = window.GetBoardTileOverlayOptionsForTests();

                        Assert.That(status.Kind, Is.EqualTo(BoardTileOverlayCellStatusKind.CatalogMissing));
                        Assert.That(options, Is.Empty);
                    });
            }
            finally
            {
                Object.DestroyImmediate(presentation);
            }
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTilePaint_WritesPresentationOverride()
        {
            WithBoardTileStyleWindow((window, authoring, presentation, styleCatalog, profile) =>
            {
                window.SetEditModeForTests(StageAuthoringGridEditMode.BoardTilePaint);
                window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                window.SetBoardTileStyleKeyForTests("grass");

                var changed = window.SetBoardTilePaintOverrideForTests(out var error);

                Assert.That(changed, Is.True, error);
                Assert.That(presentation.BoardTilePaintOverrides.Count, Is.EqualTo(1));
                Assert.That(presentation.BoardTilePaintOverrides[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(presentation.BoardTilePaintOverrides[0].StyleKey, Is.EqualTo("grass"));
            });
        }

        [Test]
        public void StageAuthoringGridWindow_BoardTileOverlay_AddAndClearWritesPresentationOverrides()
        {
            WithBoardTileOverlayWindow((window, authoring, presentation, overlayCatalog, profile) =>
            {
                window.SetEditModeForTests(StageAuthoringGridEditMode.BoardTileOverlay);
                window.SetTargetCellForTests(FaceId.Floor, new Vector2Int(0, 0));
                window.SetBoardTileOverlayKeyForTests("guide");

                var added = window.AddBoardTileOverlayOverrideForTests(out var addError);
                var cleared = window.ClearBoardTileOverlayOverridesForTests(out var clearError);

                Assert.That(added, Is.True, addError);
                Assert.That(cleared, Is.True, clearError);
                Assert.That(presentation.BoardTileOverlayOverrides, Is.Empty);
            });
        }

        private static void WithWindow(
            StagePlacedEntityAuthoring[] placements,
            System.Action<StageAuthoringGridWindow, StageAuthoringDefinition> action)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                authoring.SetPlacements(placements);
                window.BindForTests(authoring);
                action(window, authoring);
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(authoring);
            }
        }

        private static void WithTileFeatureWindow(
            System.Action<StageAuthoringGridWindow, StageAuthoringDefinition, StagePresentationDefinition, GameObject> action)
        {
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var prefab = new GameObject("ButtonTileFeaturePrefab");
            prefab.AddComponent<TileFeatureVisualTargetView>();
            try
            {
                WithWindow(
                    System.Array.Empty<StagePlacedEntityAuthoring>(),
                    (window, authoring) =>
                    {
                        authoring.AssignGeneratedDefinitions(null, presentation);
                        authoring.SetTileFeatures(new[] { TileFeature(1, FaceId.Floor, 0, 0) });
                        window.SetEditModeForTests(StageAuthoringGridEditMode.TileFeaturePlacement);
                        action(window, authoring, presentation, prefab);
                    });
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(presentation);
            }
        }

        private static void WithBoardTileWindow(
            System.Action<StageAuthoringGridWindow, StageAuthoringDefinition, StagePresentationDefinition, BoardTilePresentationCatalog, Material> action)
        {
            var material = CreateMaterial("BoardTileGridWindowMaterial");
            var catalog = CreateBoardTileCatalog(Entry("cell-key", BoardTileVisualRole.ActiveBottom, null, material));
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentation, "boardTilePresentationCatalog", catalog);
            try
            {
                WithWindow(
                    System.Array.Empty<StagePlacedEntityAuthoring>(),
                    (window, authoring) =>
                    {
                        authoring.AssignGeneratedDefinitions(null, presentation);
                        action(window, authoring, presentation, catalog, material);
                    });
            }
            finally
            {
                Object.DestroyImmediate(presentation);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(material);
            }
        }

        private static void WithBoardTileStyleWindow(
            System.Action<StageAuthoringGridWindow, StageAuthoringDefinition, StagePresentationDefinition, BoardTileStyleCatalog, BoardPresentationProfile> action)
        {
            var styleCatalog = CreateStyleCatalog(StyleEntry("grass", "Grass", Color.green));
            var profile = CreateBoardPresentationProfile(styleCatalog, overlayCatalog: null);
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentation, "boardPresentationProfile", profile);
            try
            {
                WithWindow(
                    System.Array.Empty<StagePlacedEntityAuthoring>(),
                    (window, authoring) =>
                    {
                        authoring.AssignGeneratedDefinitions(null, presentation);
                        action(window, authoring, presentation, styleCatalog, profile);
                    });
            }
            finally
            {
                Object.DestroyImmediate(presentation);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(styleCatalog);
            }
        }

        private static void WithBoardTileOverlayWindow(
            System.Action<StageAuthoringGridWindow, StageAuthoringDefinition, StagePresentationDefinition, BoardTileOverlayCatalog, BoardPresentationProfile> action)
        {
            var overlayCatalog = CreateOverlayCatalog(
                OverlayEntry("guide", "Guide", BoardTileOverlayLayer.Guide, Color.yellow, 0.5f, 10));
            var profile = CreateBoardPresentationProfile(null, overlayCatalog);
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentation, "boardPresentationProfile", profile);
            try
            {
                WithWindow(
                    System.Array.Empty<StagePlacedEntityAuthoring>(),
                    (window, authoring) =>
                    {
                        authoring.AssignGeneratedDefinitions(null, presentation);
                        action(window, authoring, presentation, overlayCatalog, profile);
                    });
            }
            finally
            {
                Object.DestroyImmediate(presentation);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(overlayCatalog);
            }
        }

        private static StagePlacedEntityAuthoring Placement(
            string stableGuid,
            FaceId face,
            int x,
            int y)
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = stableGuid,
                DisplayName = stableGuid,
                Kind = StageAuthoringEntityKind.Box,
                Cell = new SurfaceCell(face, x, y),
                Facing = Direction.None,
                Hp = 1,
            };
        }

        private static StageBoardDefinition Board(
            int minX,
            int minY,
            int maxX,
            int maxY)
        {
            return new StageBoardDefinition
            {
                MinInclusive = new Vector2Int(minX, minY),
                MaxInclusive = new Vector2Int(maxX, maxY),
                InitialBottomFace = FaceId.Floor,
            };
        }

        private static StageTileFeatureDefinition TileFeature(
            int tileId,
            FaceId face,
            int x,
            int y,
            string presentationKey = "")
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = new SurfaceCell(face, x, y),
                Kind = TileFeatureKind.Button,
                ActivationRule = TileFeatureActivationRule.BottomFaceOnly,
                Direction = Direction2D.None,
                BoxSelector = TileFeatureBoxSelector.AnyPushableBox,
                PresentationKey = presentationKey,
            };
        }

        private static StageZoneDefinition Zone(
            string zoneId,
            FaceId face,
            int minX,
            int minY,
            int maxX,
            int maxY)
        {
            return new StageZoneDefinition
            {
                ZoneId = zoneId,
                FaceId = face,
                Regions = new[]
                {
                    new StageZoneRegionDefinition
                    {
                        MinInclusive = new Vector2Int(minX, minY),
                        MaxInclusive = new Vector2Int(maxX, maxY),
                    },
                },
            };
        }

        private static StageTileFeatureDefinition TileFeature(
            int tileId,
            FaceId face,
            int x,
            int y,
            TileFeatureKind kind,
            TileFeatureActivationRule activationRule,
            Direction2D direction,
            TileFeatureBoxSelector boxSelector,
            string presentationKey = "")
        {
            return new StageTileFeatureDefinition
            {
                TileId = tileId,
                Cell = new SurfaceCell(face, x, y),
                Kind = kind,
                ActivationRule = activationRule,
                Direction = direction,
                BoxSelector = boxSelector,
                PresentationKey = presentationKey,
            };
        }

        private static BoardTilePresentationCatalogEntry Entry(
            string presentationKey,
            BoardTileVisualRole role,
            GameObject tilePrefab,
            Material materialFallback)
        {
            var entry = new BoardTilePresentationCatalogEntry();
            SetPrivateField(entry, "presentationKey", presentationKey);
            SetPrivateField(entry, "displayName", presentationKey);
            SetPrivateField(entry, "role", role);
            SetPrivateField(entry, "tilePrefab", tilePrefab);
            SetPrivateField(entry, "materialFallback", materialFallback);
            return entry;
        }

        private static BoardTilePresentationCatalog CreateBoardTileCatalog(
            params BoardTilePresentationCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<BoardTilePresentationCatalog>();
            catalog.name = "StageAuthoringGridWindowTests_BoardTileCatalog";
            SetPrivateField(catalog, "entries", entries ?? System.Array.Empty<BoardTilePresentationCatalogEntry>());
            return catalog;
        }

        private static BoardTileStyleCatalogEntry StyleEntry(
            string styleKey,
            string displayName,
            Color tint)
        {
            var entry = new BoardTileStyleCatalogEntry();
            SetPrivateField(entry, "styleKey", styleKey);
            SetPrivateField(entry, "displayName", displayName);
            SetPrivateField(entry, "tint", tint);
            return entry;
        }

        private static BoardTileStyleCatalog CreateStyleCatalog(params BoardTileStyleCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<BoardTileStyleCatalog>();
            catalog.name = "StageAuthoringGridWindowTests_StyleCatalog";
            SetPrivateField(catalog, "entries", entries ?? System.Array.Empty<BoardTileStyleCatalogEntry>());
            return catalog;
        }

        private static BoardTileOverlayCatalogEntry OverlayEntry(
            string overlayKey,
            string displayName,
            BoardTileOverlayLayer layer,
            Color tint,
            float alpha,
            int order)
        {
            var entry = new BoardTileOverlayCatalogEntry();
            SetPrivateField(entry, "overlayKey", overlayKey);
            SetPrivateField(entry, "displayName", displayName);
            SetPrivateField(entry, "layer", layer);
            SetPrivateField(entry, "tint", tint);
            SetPrivateField(entry, "alpha", alpha);
            SetPrivateField(entry, "order", order);
            return entry;
        }

        private static BoardTileOverlayCatalog CreateOverlayCatalog(params BoardTileOverlayCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<BoardTileOverlayCatalog>();
            catalog.name = "StageAuthoringGridWindowTests_OverlayCatalog";
            SetPrivateField(catalog, "entries", entries ?? System.Array.Empty<BoardTileOverlayCatalogEntry>());
            return catalog;
        }

        private static BoardPresentationProfile CreateBoardPresentationProfile(
            BoardTileStyleCatalog styleCatalog,
            BoardTileOverlayCatalog overlayCatalog)
        {
            var profile = ScriptableObject.CreateInstance<BoardPresentationProfile>();
            profile.name = "StageAuthoringGridWindowTests_BoardProfile";
            SetPrivateField(profile, "defaultBoardTileStyleCatalog", styleCatalog);
            SetPrivateField(profile, "defaultBoardTileOverlayCatalog", overlayCatalog);
            return profile;
        }

        private static Material CreateMaterial(string materialName)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null, "Expected a test shader to be available.");
            return new Material(shader)
            {
                name = materialName,
            };
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
