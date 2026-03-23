using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public sealed class GameplaySceneHostConfiguration
    {
        public bool AutoAdvanceTicks = true;
        public bool AutoCreateViews = true;
        public float CellSize = 1f;
        public bool DirectionChangeConsumesDelay;
        public Vector3 GridOrigin = Vector3.zero;
        public int InitialMoveDelayTicks = 0;
        public EntityState[] InitialEntities = Array.Empty<EntityState>();
        public float MoveDeadzone = 0.5f;
        public int PlayerEntityId = 1;
        public int RepeatedMoveIntervalTicks = 2;
        public float TickIntervalSeconds = 0.2f;
        public InputActionAsset Actions;
        public IEntityLogic[] StaticEntityLogics = Array.Empty<IEntityLogic>();
        public IGameplayEntityViewFactory ViewFactory;
    }
}
