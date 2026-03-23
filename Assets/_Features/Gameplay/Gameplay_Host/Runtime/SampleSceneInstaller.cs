using System;
using Game.Feature.Gameplay.BoardState;
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
        [SerializeField] private Vector3 gridOrigin = new(-1.2f, -0.6f, 0f);
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
                GridOrigin = gridOrigin,
                InitialMoveDelayTicks = initialMoveDelayTicks,
                InitialEntities = new[]
                {
                    CreatePlayer(entityId: playerEntityId, position: new Vector2Int(0, 0)),
                    CreateWall(entityId: 90, position: new Vector2Int(2, 0)),
                    CreateWall(entityId: 91, position: new Vector2Int(2, 1)),
                    CreateWall(entityId: 92, position: new Vector2Int(2, -1)),
                },
                MoveDeadzone = moveDeadzone,
                PlayerEntityId = playerEntityId,
                RepeatedMoveIntervalTicks = repeatedMoveIntervalTicks,
                TickIntervalSeconds = tickIntervalSeconds,
            };
        }

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.transform.position = new Vector3(0.75f, 0.25f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.92f, 0.94f, 0.98f);
        }

        private static EntityState CreatePlayer(int entityId, Vector2Int position)
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
    }
}
