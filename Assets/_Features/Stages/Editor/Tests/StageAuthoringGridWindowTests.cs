using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
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
                    window.SetTileFeatureDraftForTests(
                        TileFeatureKind.Button,
                        TileFeatureActivationRule.BottomFaceOnly,
                        Direction2D.None,
                        TileFeatureBoxSelector.AnyPushableBox,
                        0);

                    window.AddTileFeatureAtTargetCellForTests();

                    Assert.That(authoring.Placements, Has.Count.EqualTo(1));
                    Assert.That(authoring.TileFeatures, Has.Count.EqualTo(1));
                    Assert.That(authoring.TileFeatures[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                    Assert.That(window.SelectedTileFeatureIdForTests, Is.EqualTo(authoring.TileFeatures[0].TileId));
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
    }
}
