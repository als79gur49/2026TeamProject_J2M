using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public class CombinedGameplayShowcaseInstaller : GameplayShowcaseSceneInstallerBase
    {
        private const int TraversalColumn = 1;
        private const int LeftBoxLaneColumn = 3;
        private const int RightBoxLaneColumn = 7;
        private const int SurfaceSlideColumn = 9;
        private const int FloorChargingEnemyEntityId = 50;
        private const int FrontScoutEnemyEntityId = 51;

        [SerializeField] private GameplayEntityView playerViewPrefab;

        private EnemyAiProfile floorChargingEnemyProfile;
        private EnemyAiProfile frontScoutEnemyProfile;

        protected override BoardBounds CreateBoardBounds()
        {
            return new BoardBounds(
                minInclusive: new Vector2Int(0, 0),
                maxInclusive: new Vector2Int(10, 6));
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
            entities.Add(CreateEnemy(
                FloorChargingEnemyEntityId,
                new SurfaceCell(FaceId.Floor, 1, 5),
                EnemyAiMode.Patrol,
                Direction.Down,
                hp: 3));
            entities.Add(CreateEnemy(
                FrontScoutEnemyEntityId,
                new SurfaceCell(FaceId.Front, 2, 2),
                EnemyAiMode.Patrol,
                Direction.Down));

            entities.Add(CreateBox(30, new SurfaceCell(FaceId.Floor, LeftBoxLaneColumn, 1), BoxCapabilities.Push));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 5, 1)));

            entities.Add(CreateBox(31, new SurfaceCell(FaceId.Floor, LeftBoxLaneColumn, 3), BoxCapabilities.Item));
            entities.Add(CreateBox(36, new SurfaceCell(FaceId.Floor, 5, 3), BoxCapabilities.Push | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 6, 3)));

            entities.Add(CreateBox(32, new SurfaceCell(FaceId.Floor, LeftBoxLaneColumn, 5), BoxCapabilities.Flip, Direction.Left));

            entities.Add(CreateBox(33, new SurfaceCell(FaceId.Floor, RightBoxLaneColumn, 1), BoxCapabilities.Push | BoxCapabilities.Flip));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 9, 1)));

            entities.Add(CreateBox(34, new SurfaceCell(FaceId.Floor, RightBoxLaneColumn, 3), BoxCapabilities.Flip | BoxCapabilities.Item, Direction.Left));
            entities.Add(CreateBox(37, new SurfaceCell(FaceId.Floor, 8, 3), BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy));
            entities.Add(CreateWall(nextEntityId++, new SurfaceCell(FaceId.Floor, 9, 3)));
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

            return playerViewPrefab != null
                ? new CombinedGameplayShowcasePlayerPrefabViewFactory(
                    boardRoot.EntityRoot,
                    PlayerEntityId,
                    playerViewPrefab,
                    CellSize)
                : new GameplayBoxCapabilityLabelViewFactory(boardRoot.EntityRoot, CellSize, PlayerEntityId);
        }

        protected override GameplayEntityView ResolvePlayerViewPrefab()
        {
            return playerViewPrefab;
        }

        protected override EnemyAiProfileOverride[] CreateEnemyAiProfileOverrides(
            IReadOnlyList<EntityState> entities,
            BoardBounds boardBounds)
        {
            floorChargingEnemyProfile ??= EnemyAiProfile.CreateRuntimeCharging();
            frontScoutEnemyProfile ??= EnemyAiProfile.CreateRuntimeNonAttacking();

            return new[]
            {
                new EnemyAiProfileOverride
                {
                    EntityId = FloorChargingEnemyEntityId,
                    Profile = floorChargingEnemyProfile,
                },
                new EnemyAiProfileOverride
                {
                    EntityId = FrontScoutEnemyEntityId,
                    Profile = frontScoutEnemyProfile,
                },
            };
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
                "Combined Gameplay Showcase",
                "Boxes + Enemy Variants",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "Floor charger starts in the traversal lane to demo Patrol -> Chase -> Charge immediately.",
                    "Front-face scout uses the non-attacking profile so surface-transition chase behavior stays visible.",
                });
        }

        private void OnDestroy()
        {
            DestroyRuntimeProfile(ref floorChargingEnemyProfile);
            DestroyRuntimeProfile(ref frontScoutEnemyProfile);
        }

        private static void DestroyRuntimeProfile(ref EnemyAiProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(profile);
            }
            else
            {
                DestroyImmediate(profile);
            }

            profile = null;
        }
    }
}
