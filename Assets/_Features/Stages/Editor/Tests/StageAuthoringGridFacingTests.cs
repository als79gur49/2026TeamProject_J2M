using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringGridFacingTests
    {
        [Test]
        public void GridMarker_BoxFacingRight_ShowsBoxRightArrow()
        {
            var placement = Placement("box", StageAuthoringEntityKind.Box, Direction.Right);

            Assert.That(StageAuthoringGridMarkerBuilder.Build(placement), Is.EqualTo("B→"));
        }

        [Test]
        public void GridMarker_WallFacingUp_ShowsWallUpArrow()
        {
            var placement = Placement("wall", StageAuthoringEntityKind.Wall, Direction.Up);

            Assert.That(StageAuthoringGridMarkerBuilder.Build(placement), Is.EqualTo("W↑"));
        }

        [Test]
        public void GridMarker_DuplicateCell_AppendsPlus()
        {
            var placement = Placement("wall", StageAuthoringEntityKind.Wall, Direction.Up);

            Assert.That(StageAuthoringGridMarkerBuilder.Build(placement, duplicateCell: true), Is.EqualTo("W↑+"));
        }

        [Test]
        public void GridMarker_NoneFacing_ShowsDash()
        {
            var placement = Placement("wall", StageAuthoringEntityKind.Wall, Direction.None);

            Assert.That(StageAuthoringGridMarkerBuilder.Build(placement), Is.EqualTo("W-"));
        }

        [Test]
        public void GridMarker_UnknownFacing_UsesFallback()
        {
            var placement = Placement("box", StageAuthoringEntityKind.Box, (Direction)99);

            Assert.That(StageAuthoringGridMarkerBuilder.Build(placement), Is.EqualTo("B?"));
        }

        [Test]
        public void GridMarker_EntranceTileFeatureBadge_UsesN()
        {
            var feature = new StageTileFeatureDefinition
            {
                TileId = 7,
                Kind = TileFeatureKind.Entrance,
            };

            Assert.That(StageAuthoringGridMarkerBuilder.BuildTileFeatureBadge(feature), Is.EqualTo("N7"));
        }

        [Test]
        public void GridCellTint_Player_ReturnsPlayerTint()
        {
            Assert.That(StageAuthoringGridCellStyleUtility.TryGetTint(StageAuthoringEntityKind.Player, out var tint), Is.True);
            Assert.That(tint, Is.EqualTo(StageAuthoringGridCellStyleUtility.PlayerTint));
        }

        [Test]
        public void GridCellTint_Enemy_ReturnsEnemyTint()
        {
            Assert.That(StageAuthoringGridCellStyleUtility.TryGetTint(StageAuthoringEntityKind.Enemy, out var tint), Is.True);
            Assert.That(tint, Is.EqualTo(StageAuthoringGridCellStyleUtility.EnemyTint));
        }

        [Test]
        public void GridCellTint_Box_ReturnsBoxTint()
        {
            Assert.That(StageAuthoringGridCellStyleUtility.TryGetTint(StageAuthoringEntityKind.Box, out var tint), Is.True);
            Assert.That(tint, Is.EqualTo(StageAuthoringGridCellStyleUtility.BoxTint));
        }

        [Test]
        public void GridCellTint_Wall_ReturnsWallTint()
        {
            Assert.That(StageAuthoringGridCellStyleUtility.TryGetTint(StageAuthoringEntityKind.Wall, out var tint), Is.True);
            Assert.That(tint, Is.EqualTo(StageAuthoringGridCellStyleUtility.WallTint));
        }

        [Test]
        public void GridCellTint_UnknownKind_ReturnsNoTint()
        {
            Assert.That(StageAuthoringGridCellStyleUtility.TryGetTint((StageAuthoringEntityKind)99, out var tint), Is.False);
            Assert.That(tint, Is.EqualTo(Color.white));
        }

        [Test]
        public void GridCellTint_NullPlacement_ReturnsNoTint()
        {
            Assert.That(StageAuthoringGridCellStyleUtility.TryGetTint(null, out var tint), Is.False);
            Assert.That(tint, Is.EqualTo(Color.white));
        }

        [Test]
        public void GridCellTint_FocusedMatchingKind_ReturnsKindTint()
        {
            var placement = Placement("enemy", StageAuthoringEntityKind.Enemy, Direction.Left);

            Assert.That(
                StageAuthoringGridCellStyleUtility.TryGetTint(
                    placement,
                    StageAuthoringEntityKind.Enemy,
                    out var tint),
                Is.True);
            Assert.That(tint, Is.EqualTo(StageAuthoringGridCellStyleUtility.EnemyTint));
        }

        [Test]
        public void GridCellTint_FocusedDifferentKind_ReturnsDimTint()
        {
            var placement = Placement("box", StageAuthoringEntityKind.Box, Direction.Right);

            Assert.That(
                StageAuthoringGridCellStyleUtility.TryGetTint(
                    placement,
                    StageAuthoringEntityKind.Enemy,
                    out var tint),
                Is.True);
            Assert.That(tint, Is.EqualTo(StageAuthoringGridCellStyleUtility.DimTint(StageAuthoringGridCellStyleUtility.BoxTint)));
        }

        [Test]
        public void GridCellTint_FocusedNullPlacement_ReturnsNoTint()
        {
            Assert.That(
                StageAuthoringGridCellStyleUtility.TryGetTint(
                    null,
                    StageAuthoringEntityKind.Box,
                    out var tint),
                Is.False);
            Assert.That(tint, Is.EqualTo(Color.white));
        }

        [Test]
        public void GridCellTint_IsFocusedKind_RequiresMatchingFocus()
        {
            Assert.That(StageAuthoringGridCellStyleUtility.IsFocusedKind(StageAuthoringEntityKind.Box, null), Is.False);
            Assert.That(
                StageAuthoringGridCellStyleUtility.IsFocusedKind(
                    StageAuthoringEntityKind.Box,
                    StageAuthoringEntityKind.Enemy),
                Is.False);
            Assert.That(
                StageAuthoringGridCellStyleUtility.IsFocusedKind(
                    StageAuthoringEntityKind.Box,
                    StageAuthoringEntityKind.Box),
                Is.True);
        }

        [Test]
        public void FacingRotateClockwise_UsesDirectionOrder()
        {
            Assert.That(StageAuthoringFacingDisplayUtility.RotateClockwise(Direction.Up), Is.EqualTo(Direction.Right));
            Assert.That(StageAuthoringFacingDisplayUtility.RotateClockwise(Direction.Right), Is.EqualTo(Direction.Down));
            Assert.That(StageAuthoringFacingDisplayUtility.RotateClockwise(Direction.Down), Is.EqualTo(Direction.Left));
            Assert.That(StageAuthoringFacingDisplayUtility.RotateClockwise(Direction.Left), Is.EqualTo(Direction.Up));
            Assert.That(StageAuthoringFacingDisplayUtility.RotateClockwise(Direction.None), Is.EqualTo(Direction.Right));
        }

        [Test]
        public void FacingRotateCounterClockwise_UsesDirectionOrder()
        {
            Assert.That(StageAuthoringFacingDisplayUtility.RotateCounterClockwise(Direction.Up), Is.EqualTo(Direction.Left));
            Assert.That(StageAuthoringFacingDisplayUtility.RotateCounterClockwise(Direction.Left), Is.EqualTo(Direction.Down));
            Assert.That(StageAuthoringFacingDisplayUtility.RotateCounterClockwise(Direction.Down), Is.EqualTo(Direction.Right));
            Assert.That(StageAuthoringFacingDisplayUtility.RotateCounterClockwise(Direction.Right), Is.EqualTo(Direction.Up));
            Assert.That(StageAuthoringFacingDisplayUtility.RotateCounterClockwise(Direction.None), Is.EqualTo(Direction.Left));
        }

        [Test]
        public void RotateSelectedClockwise_ChangesFacingAndPreservesSelectionTargetPresentationAndMapping()
        {
            WithWindow((window, authoring) =>
            {
                window.SelectCellForTests(FaceId.Floor, new Vector2Int(1, 2));
                window.SetTargetCellForTests(FaceId.Front, new Vector2Int(9, 9));

                window.RotateSelectedClockwiseForTests();

                var placement = authoring.Placements.Single(item => item.StableGuid == "box");
                Assert.That(placement.Facing, Is.EqualTo(Direction.Right));
                Assert.That(placement.PresentationId, Is.EqualTo("box_showcase"));
                Assert.That(authoring.EntityIdMappings.Single().EntityId, Is.EqualTo(42));
                Assert.That(window.ResolveSelectedPlacementIndexForTests(), Is.EqualTo(0));
                Assert.That(window.TargetFaceForTests, Is.EqualTo(FaceId.Front));
                Assert.That(window.TargetCellForTests, Is.EqualTo(new Vector2Int(9, 9)));
            });
        }

        [Test]
        public void RotateSelectedCounterClockwise_ChangesFacing()
        {
            WithWindow((window, authoring) =>
            {
                window.SelectCellForTests(FaceId.Floor, new Vector2Int(1, 2));

                window.RotateSelectedCounterClockwiseForTests();

                Assert.That(authoring.Placements[0].Facing, Is.EqualTo(Direction.Left));
            });
        }

        [Test]
        public void RotateSelectedClockwise_FromNone_InitializesRight()
        {
            WithWindow(Direction.None, (window, authoring) =>
            {
                window.SelectCellForTests(FaceId.Floor, new Vector2Int(1, 2));

                window.RotateSelectedClockwiseForTests();

                Assert.That(authoring.Placements[0].Facing, Is.EqualTo(Direction.Right));
            });
        }

        [Test]
        public void RotateSelectedCounterClockwise_FromNone_InitializesLeft()
        {
            WithWindow(Direction.None, (window, authoring) =>
            {
                window.SelectCellForTests(FaceId.Floor, new Vector2Int(1, 2));

                window.RotateSelectedCounterClockwiseForTests();

                Assert.That(authoring.Placements[0].Facing, Is.EqualTo(Direction.Left));
            });
        }

        [Test]
        public void RotateSelected_NoSelection_NoOp()
        {
            WithWindow((window, authoring) =>
            {
                window.RotateSelectedClockwiseForTests();

                Assert.That(authoring.Placements[0].Facing, Is.EqualTo(Direction.Up));
            });
        }

        private static void WithWindow(System.Action<StageAuthoringGridWindow, StageAuthoringDefinition> action)
        {
            WithWindow(Direction.Up, action);
        }

        private static void WithWindow(
            Direction initialFacing,
            System.Action<StageAuthoringGridWindow, StageAuthoringDefinition> action)
        {
            var authoring = ScriptableObject.CreateInstance<StageAuthoringDefinition>();
            var window = ScriptableObject.CreateInstance<StageAuthoringGridWindow>();
            try
            {
                authoring.SetPlacements(new[] { Placement("box", StageAuthoringEntityKind.Box, initialFacing) });
                authoring.SetEntityIdMappings(new[]
                {
                    new StageAuthoringIdMapping
                    {
                        StableGuid = "box",
                        EntityId = 42,
                    },
                });
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
            StageAuthoringEntityKind kind,
            Direction facing)
        {
            return new StagePlacedEntityAuthoring
            {
                StableGuid = stableGuid,
                DisplayName = stableGuid,
                Kind = kind,
                Cell = new SurfaceCell(FaceId.Floor, 1, 2),
                Facing = facing,
                Hp = 1,
                BoxCapabilities = BoxCapabilities.Push,
                PresentationId = "box_showcase",
            };
        }
    }
}
