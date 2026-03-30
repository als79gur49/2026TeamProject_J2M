using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class CombinedGameplayShowcaseInstaller : GameplayShowcaseSceneInstallerBase
    {
        private const int TraversalColumn = 1;
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
                var perimeterFace = face;
                AddFacePerimeterWalls(
                    entities,
                    perimeterFace,
                    boardBounds,
                    ref nextEntityId,
                    cell => ShouldSkipPerimeterWall(perimeterFace, cell, boardBounds));
            }

            entities.Add(CreatePlayer(PlayerEntityId, new SurfaceCell(FaceId.Floor, 1, 1), Direction.Right));

            entities.Add(CreateBox(30, new SurfaceCell(FaceId.Floor, 3, 1), BoxCapabilities.Push));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 7, 1)));

            entities.Add(CreateBox(31, new SurfaceCell(FaceId.Floor, 3, 3), BoxCapabilities.Item));
            entities.Add(CreateBox(36, new SurfaceCell(FaceId.Floor, 6, 3), BoxCapabilities.Push | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 7, 3)));

            entities.Add(CreateBox(32, new SurfaceCell(FaceId.Floor, 3, 5), BoxCapabilities.Flip, Direction.Left));

            entities.Add(CreateBox(33, new SurfaceCell(FaceId.Floor, 10, 1), BoxCapabilities.Push | BoxCapabilities.Flip));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 14, 1)));

            entities.Add(CreateBox(34, new SurfaceCell(FaceId.Floor, 10, 3), BoxCapabilities.Flip | BoxCapabilities.Item, Direction.Left));
            entities.Add(CreateBox(37, new SurfaceCell(FaceId.Floor, 13, 3), BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 14, 3)));
            entities.Add(CreateBox(35, new SurfaceCell(FaceId.Floor, 10, 5), BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Item));

            entities.Add(CreateBox(40, new SurfaceCell(FaceId.Floor, SurfaceSlideColumn, boardBounds.MaxInclusive.y), BoxCapabilities.Push, Direction.Up));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Front, SurfaceSlideColumn, 3)));
        }

        private static bool ShouldSkipPerimeterWall(FaceId face, SurfaceCell cell, BoardBounds boardBounds)
        {
            var isSharedEdgeOpening = cell.x == TraversalColumn || cell.x == SurfaceSlideColumn;
            if (!isSharedEdgeOpening)
            {
                return false;
            }

            return (face == FaceId.Floor && cell.y == boardBounds.MaxInclusive.y) ||
                   (face == FaceId.Front && cell.y == boardBounds.MinInclusive.y);
        }

        protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
        {
            return new GameplayShowcaseOverlayContent(
                "Combined Gameplay Showcase",
                "Traverse shared edges on the left, then move into the box interaction lanes on the right without leaving the same 3D cube presentation.",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "The left opening demonstrates player-led topology rotation across visible faces.",
                    "Center and right lanes stack Push, Flip, Destroy, and Item box capabilities for mixed scenarios.",
                    "A top-edge box near the shared opening checks cross-face push presentation while the board rotates.",
                });
        }
    }
}
