using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class SampleSceneInstaller : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private bool autoAdvanceTicks = true;
        [SerializeField] private bool autoCreateViews = true;
        [SerializeField] private bool configureMainCamera = true;
        [SerializeField] private float cellSize = 1.2f;
        [SerializeField] private bool directionChangeConsumesDelay;
        [SerializeField] private int initialMoveDelayTicks;
        [SerializeField] private float moveDeadzone = 0.5f;
        [SerializeField] private int playerEntityId = 10;
        [SerializeField] private int repeatedMoveIntervalTicks = 2;
        [SerializeField] private float tickIntervalSeconds = 0.2f;

        private void Awake()
        {
            if (actions == null)
            {
                throw new InvalidOperationException("SampleSceneInstaller requires an InputActionAsset reference.");
            }

            if (configureMainCamera)
            {
                ConfigureCamera();
            }

            var host = GetComponent<GameplaySceneHost>() ?? gameObject.AddComponent<GameplaySceneHost>();
            host.Initialize(CreateConfiguration());
        }

        private GameplaySceneHostConfiguration CreateConfiguration()
        {
            return new GameplaySceneHostConfiguration
            {
                Actions = actions,
                AutoAdvanceTicks = autoAdvanceTicks,
                AutoCreateViews = autoCreateViews,
                CellSize = cellSize,
                DirectionChangeConsumesDelay = directionChangeConsumesDelay,
                InitialBoardBounds = CreateBoardBounds(),
                InitialMoveDelayTicks = initialMoveDelayTicks,
                InitialEntities = CreateInitialEntities(),
                InitialTerrain = GameplayTerrainData.Empty,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                MoveDeadzone = moveDeadzone,
                PlayerEntityId = playerEntityId,
                RepeatedMoveIntervalTicks = repeatedMoveIntervalTicks,
                SnapViewCameraToTarget = configureMainCamera,
                TickIntervalSeconds = tickIntervalSeconds,
                ViewCamera = configureMainCamera ? Camera.main : null,
            };
        }

        private static BoardBounds CreateBoardBounds()
        {
            return new BoardBounds(
                minInclusive: new Vector2Int(-3, -2),
                maxInclusive: new Vector2Int(6, 4));
        }

        private EntityState[] CreateInitialEntities()
        {
            var entities = new List<EntityState>
            {
                CreatePlayer(entityId: playerEntityId, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0)),
                CreateBox(entityId: 31, position: new Vector2Int(3, 0)),
            };

            var nextWallId = 100;

            for (var y = -2; y <= 4; y++)
            {
                entities.Add(CreateWall(nextWallId++, new Vector2Int(-3, y)));
                entities.Add(CreateWall(nextWallId++, new Vector2Int(6, y)));
            }

            for (var x = -2; x <= 5; x++)
            {
                entities.Add(CreateWall(nextWallId++, new Vector2Int(x, -2)));
                entities.Add(CreateWall(nextWallId++, new Vector2Int(x, 4)));
            }

            return entities.ToArray();
        }

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.transform.position = new Vector3(0.75f, 3.5f, 8.5f);
            camera.transform.rotation = Quaternion.Euler(24f, 152f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.92f, 0.94f, 0.98f);
        }

        private static EntityState CreatePlayer(int entityId, Vector2Int position)
        {
            return CreatePlayer(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position)
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
                facing = Direction.Right,
            };
        }

        private static EntityState CreateWall(int entityId, Vector2Int position)
        {
            return CreateWall(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
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

        private static EntityState CreateBox(int entityId, Vector2Int position)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
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
                facing = Direction.Right,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }
    }
}
