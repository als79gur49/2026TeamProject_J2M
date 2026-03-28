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
    }
}
