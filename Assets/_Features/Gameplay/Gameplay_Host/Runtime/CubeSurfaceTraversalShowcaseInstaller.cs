using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class CubeSurfaceTraversalShowcaseInstaller : GameplayShowcaseSceneInstallerBase
    {
        protected override BoardBounds CreateBoardBounds()
        {
            return new BoardBounds(
                minInclusive: new Vector2Int(0, 0),
                maxInclusive: new Vector2Int(4, 4));
        }

        protected override void PopulateInitialEntities(List<EntityState> entities, BoardBounds boardBounds)
        {
            var nextEntityId = 100;
            var traversalColumn = boardBounds.MinInclusive.x + ((boardBounds.MaxInclusive.x - boardBounds.MinInclusive.x) / 2);

            for (var face = FaceId.Floor; face <= FaceId.Back; face++)
            {
                AddFacePerimeterWalls(
                    entities,
                    face,
                    boardBounds,
                    ref nextEntityId,
                    cell => cell.x == traversalColumn &&
                            (cell.y == boardBounds.MinInclusive.y || cell.y == boardBounds.MaxInclusive.y));
            }

            entities.Add(CreatePlayer(
                PlayerEntityId,
                new SurfaceCell(FaceId.Floor, traversalColumn, boardBounds.MaxInclusive.y),
                Direction.Up));

            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Front, boardBounds.MinInclusive.x + 1, boardBounds.MinInclusive.y + 2)));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Ceiling, boardBounds.MaxInclusive.x - 1, boardBounds.MinInclusive.y + 2)));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Back, boardBounds.MinInclusive.x + 1, boardBounds.MinInclusive.y + 1)));
        }

        protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
        {
            return new GameplayShowcaseOverlayContent(
                "Cube Surface Traversal",
                "Walk the shared edge openings to watch the board roll across Floor, Front, Ceiling, and Back.",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "Start at the top opening on Floor and press Up to trigger the first topology rotation.",
                    "The corridor stays open across the visible faces while the rest of the perimeter remains sealed.",
                    "This scene isolates traversal, board rotation, and active-face visibility without box rules layered on top.",
                });
        }
    }
}
