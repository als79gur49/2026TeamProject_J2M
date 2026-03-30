using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class BoxInteractionShowcaseInstaller : GameplayShowcaseSceneInstallerBase
    {
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
                    cell => cell.x == 7 &&
                            (cell.y == boardBounds.MinInclusive.y || cell.y == boardBounds.MaxInclusive.y));
            }

            entities.Add(CreatePlayer(PlayerEntityId, new SurfaceCell(FaceId.Floor, 1, 1), Direction.Right));

            entities.Add(CreateBox(30, new SurfaceCell(FaceId.Floor, 3, 1), BoxCapabilities.Push));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 7, 1)));

            entities.Add(CreateBox(31, new SurfaceCell(FaceId.Floor, 3, 3), BoxCapabilities.Push | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 4, 3)));

            entities.Add(CreateBox(32, new SurfaceCell(FaceId.Floor, 3, 5), BoxCapabilities.Flip, Direction.Left));

            entities.Add(CreateBox(33, new SurfaceCell(FaceId.Floor, 10, 1), BoxCapabilities.Push | BoxCapabilities.Flip));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 14, 1)));

            entities.Add(CreateBox(34, new SurfaceCell(FaceId.Floor, 10, 3), BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 11, 3)));
        }

        protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
        {
            return new GameplayShowcaseOverlayContent(
                "Box Interaction Showcase",
                "Compare the box capability combinations lane by lane on a single active floor.",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "Left lanes cover Push, Push + Destroy, and Flip in isolation.",
                    "Right lanes combine Push + Flip and Push + Flip + Destroy for cross-mechanic checks.",
                    "Use the overlay labels as scenario notes; the board itself stays clear for 3D presentation.",
                });
        }
    }
}
