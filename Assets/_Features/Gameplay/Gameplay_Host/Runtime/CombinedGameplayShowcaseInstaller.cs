using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class CombinedGameplayShowcaseInstaller : GameplayShowcaseSceneInstallerBase
    {
        private const int TraversalColumn = 1;
        private const int LeftBoxLaneColumn = 3;
        private const int RightBoxLaneColumn = 10;
        private const int SurfaceSlideColumn = 14;

        protected override BoardBounds CreateBoardBounds()
        {
            return new BoardBounds(
                minInclusive: new Vector2Int(0, 0),
                maxInclusive: new Vector2Int(15, 6));
        }

        protected override void PopulateInitialEntities(List<EntityState> entities, BoardBounds boardBounds)
        {
            var nextEntityId = 100;

            for (var face = FaceId.Floor; face <= FaceId.Back; face++)
            {
                AddFacePerimeterWalls(
                    entities,
                    face,
                    boardBounds,
                    ref nextEntityId,
                    cell => ShouldSkipPerimeterWall(cell, boardBounds));
            }

            entities.Add(CreatePlayer(PlayerEntityId, new SurfaceCell(FaceId.Floor, 1, 1), Direction.Right));

            entities.Add(CreateBox(30, new SurfaceCell(FaceId.Floor, LeftBoxLaneColumn, 1), BoxCapabilities.Push));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 7, 1)));

            entities.Add(CreateBox(31, new SurfaceCell(FaceId.Floor, LeftBoxLaneColumn, 3), BoxCapabilities.Item));
            entities.Add(CreateBox(36, new SurfaceCell(FaceId.Floor, 6, 3), BoxCapabilities.Push | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 7, 3)));

            entities.Add(CreateBox(32, new SurfaceCell(FaceId.Floor, LeftBoxLaneColumn, 5), BoxCapabilities.Flip, Direction.Left));

            entities.Add(CreateBox(33, new SurfaceCell(FaceId.Floor, RightBoxLaneColumn, 1), BoxCapabilities.Push | BoxCapabilities.Flip));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 14, 1)));

            entities.Add(CreateBox(34, new SurfaceCell(FaceId.Floor, RightBoxLaneColumn, 3), BoxCapabilities.Flip | BoxCapabilities.Item, Direction.Left));
            entities.Add(CreateBox(37, new SurfaceCell(FaceId.Floor, 13, 3), BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 14, 3)));
            entities.Add(CreateBox(35, new SurfaceCell(FaceId.Floor, RightBoxLaneColumn, 5), BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Item));

            entities.Add(CreateBox(40, new SurfaceCell(FaceId.Floor, SurfaceSlideColumn, boardBounds.MaxInclusive.y), BoxCapabilities.Push, Direction.Up));
            entities.Add(CreateBox(41, new SurfaceCell(FaceId.Front, LeftBoxLaneColumn, 1), BoxCapabilities.Push | BoxCapabilities.Item));
            entities.Add(CreateBox(42, new SurfaceCell(FaceId.Ceiling, RightBoxLaneColumn, 1), BoxCapabilities.Push | BoxCapabilities.Flip, Direction.Left));
            entities.Add(CreateBox(43, new SurfaceCell(FaceId.Back, LeftBoxLaneColumn, 5), BoxCapabilities.Push | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Front, SurfaceSlideColumn, 3)));
        }

        protected override IGameplayEntityViewFactory CreateViewFactory(GameplayBoardRoot boardRoot)
        {
            if (!AutoCreateViews || boardRoot == null)
            {
                return null;
            }

            return new GameplayBoxCapabilityLabelViewFactory(boardRoot.EntityRoot, CellSize, PlayerEntityId);
        }

        private static bool ShouldSkipPerimeterWall(SurfaceCell cell, BoardBounds boardBounds)
        {
            return IsSharedEdgeOpeningColumn(cell.x) &&
                   (cell.y == boardBounds.MinInclusive.y || cell.y == boardBounds.MaxInclusive.y);
        }

        private static bool IsSharedEdgeOpeningColumn(int x)
        {
            return x == TraversalColumn ||
                   x == LeftBoxLaneColumn ||
                   x == RightBoxLaneColumn ||
                   x == SurfaceSlideColumn;
        }

        protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
        {
            return new GameplayShowcaseOverlayContent(
                "Box Slide Test Scene",
                "",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "",
                });
        }
    }
}
