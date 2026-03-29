using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
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
        public BoardBounds InitialBoardBounds = BoardBounds.Unbounded;
        public float InitialMoveDelaySeconds = -1f;
        public int InitialMoveDelayTicks = 0;
        public EntityState[] InitialEntities = Array.Empty<EntityState>();
        public GameplayTerrainData InitialTerrain = GameplayTerrainData.Empty;
        public CubeTopologyState InitialTopology = new(FaceId.Floor);
        public int MaxTicksPerFrame = GameplayTimingProfile.DefaultMaxTicksPerFrame;
        public float MoveDeadzone = 0.5f;
        public int PlayerEntityId = 1;
        public float PushMotionDurationSeconds = -1f;
        public float FlipMotionDurationSeconds = -1f;
        public float FlipArcHeightInCells = GameplayTimingProfile.DefaultFlipArcHeightInCells;
        public float BoxSlideStepIntervalSeconds = -1f;
        public float ProjectileStepIntervalSeconds = -1f;
        public float RepeatedMoveIntervalSeconds = -1f;
        public int RepeatedMoveIntervalTicks = 2;
        public int SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        public bool SnapViewCameraToTarget;
        public float TickIntervalSeconds = 0.2f;
        public InputActionAsset Actions;
        public IEntityLogic[] StaticEntityLogics = Array.Empty<IEntityLogic>();
        public Camera ViewCamera;
        public IGameplayEntityViewFactory ViewFactory;

        public GameplayTimingProfile CreateTimingProfile()
        {
            var legacyTickIntervalSeconds = ResolveLegacyTickIntervalSeconds();
            return new GameplayTimingProfile(
                SimulationTicksPerSecond,
                ResolveInitialMoveDelaySeconds(legacyTickIntervalSeconds),
                ResolveRepeatedMoveIntervalSeconds(legacyTickIntervalSeconds),
                ResolveBoxSlideStepIntervalSeconds(legacyTickIntervalSeconds),
                ResolveProjectileStepIntervalSeconds(legacyTickIntervalSeconds),
                ResolvePushMotionDurationSeconds(legacyTickIntervalSeconds),
                ResolveFlipMotionDurationSeconds(legacyTickIntervalSeconds),
                FlipArcHeightInCells > 0f
                    ? FlipArcHeightInCells
                    : GameplayTimingProfile.DefaultFlipArcHeightInCells,
                MaxTicksPerFrame > 0
                    ? MaxTicksPerFrame
                    : GameplayTimingProfile.DefaultMaxTicksPerFrame);
        }

        private float ResolveInitialMoveDelaySeconds(float legacyTickIntervalSeconds)
        {
            return InitialMoveDelaySeconds >= 0f
                ? InitialMoveDelaySeconds
                : Mathf.Max(0, InitialMoveDelayTicks) * legacyTickIntervalSeconds;
        }

        private float ResolveRepeatedMoveIntervalSeconds(float legacyTickIntervalSeconds)
        {
            return RepeatedMoveIntervalSeconds > 0f
                ? RepeatedMoveIntervalSeconds
                : Mathf.Max(1, RepeatedMoveIntervalTicks) * legacyTickIntervalSeconds;
        }

        private float ResolveBoxSlideStepIntervalSeconds(float legacyTickIntervalSeconds)
        {
            return BoxSlideStepIntervalSeconds > 0f
                ? BoxSlideStepIntervalSeconds
                : legacyTickIntervalSeconds;
        }

        private float ResolveProjectileStepIntervalSeconds(float legacyTickIntervalSeconds)
        {
            return ProjectileStepIntervalSeconds > 0f
                ? ProjectileStepIntervalSeconds
                : legacyTickIntervalSeconds;
        }

        private float ResolvePushMotionDurationSeconds(float legacyTickIntervalSeconds)
        {
            return PushMotionDurationSeconds > 0f
                ? PushMotionDurationSeconds
                : legacyTickIntervalSeconds;
        }

        private float ResolveFlipMotionDurationSeconds(float legacyTickIntervalSeconds)
        {
            return FlipMotionDurationSeconds > 0f
                ? FlipMotionDurationSeconds
                : legacyTickIntervalSeconds;
        }

        private float ResolveLegacyTickIntervalSeconds()
        {
            return TickIntervalSeconds > 0f
                ? TickIntervalSeconds
                : GameplayTimingProfile.DefaultLegacyTickIntervalSeconds;
        }
    }
}
