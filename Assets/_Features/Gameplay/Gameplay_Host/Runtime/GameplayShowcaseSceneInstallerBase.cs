using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class GameplayShowcaseSceneInstallerBase : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private bool autoAdvanceTicks = true;
        [SerializeField] private bool autoCreateViews = true;
        [SerializeField] private bool configureMainCamera = true;
        [SerializeField] private float cellSize = 1.15f;
        [SerializeField] private bool directionChangeConsumesDelay;
        [SerializeField] private int initialMoveDelayTicks;
        [SerializeField] private float moveDeadzone = 0.5f;
        [SerializeField] private int playerEntityId = 10;
        [SerializeField] private int repeatedMoveIntervalTicks = 2;
        [SerializeField] private float tickIntervalSeconds = 0.2f;

        protected int PlayerEntityId => playerEntityId;

        protected virtual CubeTopologyState InitialTopology => new(FaceId.Floor);

        protected float CellSize => cellSize;

        protected virtual void Awake()
        {
            if (actions == null)
            {
                throw new InvalidOperationException($"{GetType().Name} requires an InputActionAsset reference.");
            }

            var boardBounds = CreateBoardBounds();
            var cameraSettings = CreateCameraSettings();
            GameplayShowcaseSceneScaffold.EnsureInstallerScaffold(
                gameObject,
                GetShowcaseOverlayContent(),
                cameraSettings);

            if (configureMainCamera)
            {
                ConfigureCamera(boardBounds);
            }

            var host = GetComponent<GameplaySceneHost>() ?? gameObject.AddComponent<GameplaySceneHost>();
            host.Initialize(CreateConfiguration(boardBounds, cameraSettings));
        }

        protected abstract BoardBounds CreateBoardBounds();

        protected abstract void PopulateInitialEntities(List<EntityState> entities, BoardBounds boardBounds);

        public GameplayShowcaseOverlayContent GetShowcaseOverlayContent()
        {
            return CreateShowcaseOverlayContent();
        }

        protected virtual GameplayTerrainData CreateTerrainData(BoardBounds boardBounds)
        {
            return GameplayTerrainData.Empty;
        }

        protected virtual GameplayCameraSettings CreateCameraSettings()
        {
            return GameplayCameraSettings.CreateShowcaseDefault();
        }

        protected abstract GameplayShowcaseOverlayContent CreateShowcaseOverlayContent();

        public GameplayCameraSettings GetCameraSettings()
        {
            return CreateCameraSettings();
        }

        public void ConfigureBootstrapCamera(Camera camera)
        {
            ConfigureSceneCamera(camera, CreateBoardBounds(), CreateCameraSettings());
        }

        protected virtual void ConfigureCamera(BoardBounds boardBounds)
        {
            var camera = Camera.main;
            ConfigureSceneCamera(camera, boardBounds, CreateCameraSettings());
        }

        protected static EntityState CreatePlayer(int entityId, SurfaceCell position, Direction facing = Direction.Up)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = facing,
            };
        }

        protected static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }

        protected static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = facing,
                boxCapabilities = capabilities,
            };
        }

        protected static void AddFacePerimeterWalls(
            List<EntityState> entities,
            FaceId face,
            BoardBounds boardBounds,
            ref int nextEntityId,
            Predicate<SurfaceCell> shouldSkip = null)
        {
            for (var x = boardBounds.MinInclusive.x; x <= boardBounds.MaxInclusive.x; x++)
            {
                AddWallIfNeeded(entities, face, x, boardBounds.MinInclusive.y, ref nextEntityId, shouldSkip);
                AddWallIfNeeded(entities, face, x, boardBounds.MaxInclusive.y, ref nextEntityId, shouldSkip);
            }

            for (var y = boardBounds.MinInclusive.y + 1; y < boardBounds.MaxInclusive.y; y++)
            {
                AddWallIfNeeded(entities, face, boardBounds.MinInclusive.x, y, ref nextEntityId, shouldSkip);
                AddWallIfNeeded(entities, face, boardBounds.MaxInclusive.x, y, ref nextEntityId, shouldSkip);
            }
        }

        private GameplaySceneHostConfiguration CreateConfiguration(BoardBounds boardBounds)
        {
            return CreateConfiguration(boardBounds, CreateCameraSettings());
        }

        private GameplaySceneHostConfiguration CreateConfiguration(
            BoardBounds boardBounds,
            GameplayCameraSettings cameraSettings)
        {
            var entities = new List<EntityState>();
            PopulateInitialEntities(entities, boardBounds);

            return new GameplaySceneHostConfiguration
            {
                Actions = actions,
                AutoAdvanceTicks = autoAdvanceTicks,
                AutoCreateViews = autoCreateViews,
                CameraSettings = cameraSettings,
                CellSize = cellSize,
                DirectionChangeConsumesDelay = directionChangeConsumesDelay,
                InitialBoardBounds = boardBounds,
                InitialMoveDelayTicks = initialMoveDelayTicks,
                InitialEntities = entities.ToArray(),
                InitialTerrain = CreateTerrainData(boardBounds),
                InitialTopology = InitialTopology,
                MoveDeadzone = moveDeadzone,
                PlayerEntityId = playerEntityId,
                RepeatedMoveIntervalTicks = repeatedMoveIntervalTicks,
                SnapViewCameraToTarget = configureMainCamera,
                TickIntervalSeconds = tickIntervalSeconds,
                ViewCamera = configureMainCamera ? Camera.main : null,
            };
        }

        private void ConfigureSceneCamera(
            Camera camera,
            BoardBounds boardBounds,
            GameplayCameraSettings cameraSettings)
        {
            if (camera == null)
            {
                return;
            }

            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            GameplayShowcaseSceneScaffold.ConfigureDefaultSceneCamera(
                camera,
                cameraSettings,
                Vector3.zero,
                projector.GetVisibleCubeBounds(InitialTopology));
        }

        private static void AddWallIfNeeded(
            List<EntityState> entities,
            FaceId face,
            int x,
            int y,
            ref int nextEntityId,
            Predicate<SurfaceCell> shouldSkip)
        {
            var cell = new SurfaceCell(face, x, y);
            if (shouldSkip != null && shouldSkip(cell))
            {
                return;
            }

            entities.Add(CreateWall(nextEntityId++, cell));
        }
    }
}
