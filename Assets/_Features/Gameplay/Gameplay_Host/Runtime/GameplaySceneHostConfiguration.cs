using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    public enum TopologyRotationVisualMapping
    {
        ForwardUsesNegativeX = 0,
        ForwardUsesPositiveX = 1,
    }

    [Serializable]
    public sealed class GameplaySceneHostConfiguration
    {
        public bool AutoAdvanceTicks = true;
        public bool AutoCreateViews = true;
        public float CellSize = 1f;
        public bool DirectionChangeConsumesDelay;
        public EnemyAiProfile DefaultEnemyAiProfile;
        public EnemyAiProfileOverride[] EnemyAiProfileOverrides = Array.Empty<EnemyAiProfileOverride>();
        public EnemyPresentationCatalog EnemyPresentationCatalog;
        public EnemyPresentationBinding[] EnemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        public BoardBounds InitialBoardBounds = BoardBounds.Unbounded;
        public float InitialMoveDelaySeconds = -1f;
        public EntityState[] InitialEntities = Array.Empty<EntityState>();
        public GameplayTerrainData InitialTerrain = GameplayTerrainData.Empty;
        public CubeTopologyState InitialTopology = new(FaceId.Floor);
        public int MaxTicksPerFrame = GameplayTimingProfile.DefaultMaxTicksPerFrame;
        public float MoveDeadzone = 0.5f;
        public int PlayerEntityId = 1;
        public PlayerControlTimingSettings PlayerControlTiming = PlayerControlTimingSettings.CreateDefault();
        public float PlayerMoveCooldownSeconds = -1f;
        public float PlayerPushContactThresholdSeconds = -1f;
        public float MoveMotionDurationSeconds = -1f;
        public float PushMotionDurationSeconds = -1f;
        public float TopologyMotionDurationSeconds = -1f;
        public TopologyRotationVisualMapping TopologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesNegativeX;
        public float FlipMotionDurationSeconds = -1f;
        public float FlipArcHeightInCells = GameplayTimingProfile.DefaultFlipArcHeightInCells;
        public float BoxSlideStepIntervalSeconds = -1f;
        public float ProjectileStepIntervalSeconds = -1f;
        public float RepeatedMoveIntervalSeconds = -1f;
        public int SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        public GameplayCameraSettings CameraSettings = GameplayCameraSettings.CreateRuntimeDefault();
        public bool SnapViewCameraToTarget;
        public InputActionAsset Actions;
        public IEntityLogic[] StaticEntityLogics = Array.Empty<IEntityLogic>();
        public GameplayEntityView PlayerViewPrefab;
        public Camera ViewCamera;
        public IGameplayEntityViewFactory ViewFactory;

        public GameplayTimingProfile CreateTimingProfile()
        {
            var initialMoveDelaySeconds = ResolveInitialMoveDelaySeconds();
            var repeatedMoveIntervalSeconds = ResolveRepeatedMoveIntervalSeconds();
            var boxSlideStepIntervalSeconds = ResolveBoxSlideStepIntervalSeconds();
            var projectileStepIntervalSeconds = ResolveProjectileStepIntervalSeconds();
            var pushMotionDurationSeconds = ResolvePushMotionDurationSeconds();
            var moveMotionDurationSeconds = ResolveMoveMotionDurationSeconds(pushMotionDurationSeconds);
            var topologyMotionDurationSeconds = ResolveTopologyMotionDurationSeconds(pushMotionDurationSeconds);
            var flipMotionDurationSeconds = ResolveFlipMotionDurationSeconds();
            var playerControlTiming = CreatePlayerControlTimingSnapshot(repeatedMoveIntervalSeconds);

            return new GameplayTimingProfile(
                SimulationTicksPerSecond,
                initialMoveDelaySeconds,
                repeatedMoveIntervalSeconds,
                boxSlideStepIntervalSeconds,
                projectileStepIntervalSeconds,
                moveMotionDurationSeconds,
                pushMotionDurationSeconds,
                topologyMotionDurationSeconds,
                flipMotionDurationSeconds,
                FlipArcHeightInCells > 0f
                    ? FlipArcHeightInCells
                    : GameplayTimingProfile.DefaultFlipArcHeightInCells,
                MaxTicksPerFrame > 0
                    ? MaxTicksPerFrame
                    : GameplayTimingProfile.DefaultMaxTicksPerFrame,
                playerControlTiming.MoveCooldownSeconds,
                playerControlTiming.PushContactThresholdSeconds);
        }

        public PlayerControlTimingAuthoritativeSnapshot CreatePlayerControlTimingSnapshot()
        {
            return CreatePlayerControlTimingSnapshot(ResolveRepeatedMoveIntervalSeconds());
        }

        private float ResolveInitialMoveDelaySeconds()
        {
            return InitialMoveDelaySeconds >= 0f
                ? InitialMoveDelaySeconds
                : GameplayTimingProfile.DefaultInitialMoveDelaySeconds;
        }

        private float ResolveRepeatedMoveIntervalSeconds()
        {
            return RepeatedMoveIntervalSeconds > 0f
                ? RepeatedMoveIntervalSeconds
                : GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds;
        }

        private float ResolveBoxSlideStepIntervalSeconds()
        {
            return BoxSlideStepIntervalSeconds > 0f
                ? BoxSlideStepIntervalSeconds
                : GameplayTimingProfile.DefaultBoxSlideStepIntervalSeconds;
        }

        private float ResolveProjectileStepIntervalSeconds()
        {
            return ProjectileStepIntervalSeconds > 0f
                ? ProjectileStepIntervalSeconds
                : GameplayTimingProfile.DefaultProjectileStepIntervalSeconds;
        }

        private float ResolvePushMotionDurationSeconds()
        {
            return PushMotionDurationSeconds > 0f
                ? PushMotionDurationSeconds
                : GameplayTimingProfile.DefaultPushMotionDurationSeconds;
        }

        private float ResolveMoveMotionDurationSeconds(float pushMotionDurationSeconds)
        {
            return MoveMotionDurationSeconds > 0f
                ? MoveMotionDurationSeconds
                : pushMotionDurationSeconds;
        }

        private float ResolveTopologyMotionDurationSeconds(float pushMotionDurationSeconds)
        {
            return TopologyMotionDurationSeconds > 0f
                ? TopologyMotionDurationSeconds
                : pushMotionDurationSeconds;
        }

        private float ResolveFlipMotionDurationSeconds()
        {
            return FlipMotionDurationSeconds > 0f
                ? FlipMotionDurationSeconds
                : GameplayTimingProfile.DefaultFlipMotionDurationSeconds;
        }

        private PlayerControlTimingAuthoritativeSnapshot CreatePlayerControlTimingSnapshot(
            float repeatedMoveIntervalSeconds)
        {
            return ResolvePlayerControlTimingSettings().CreateAuthoritativeSnapshot(
                SimulationTicksPerSecond,
                repeatedMoveIntervalSeconds);
        }

        private PlayerControlTimingSettings ResolvePlayerControlTimingSettings()
        {
            var resolvedSettings = PlayerControlTiming?.Clone() ?? PlayerControlTimingSettings.CreateDefault();

            // Legacy fallback while scene assets and tests migrate to PlayerControlTiming.
            if (resolvedSettings.MoveCooldownSeconds < 0f &&
                PlayerMoveCooldownSeconds >= 0f)
            {
                resolvedSettings.MoveCooldownSeconds = PlayerMoveCooldownSeconds;
            }

            if (resolvedSettings.PushContactThresholdSeconds < 0f &&
                PlayerPushContactThresholdSeconds >= 0f)
            {
                resolvedSettings.PushContactThresholdSeconds = PlayerPushContactThresholdSeconds;
            }

            return resolvedSettings;
        }
    }
}
