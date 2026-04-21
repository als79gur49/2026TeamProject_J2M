using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyAiRuntimeCollectionSnapshot
    {
        public EnemyAiRuntimeCollectionSnapshot(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId)
        {
            DefaultDefinition = defaultDefinition;
            DefinitionsByEntityId = definitionsByEntityId;
        }

        public EnemyAiRuntimeDefinition DefaultDefinition { get; }

        public IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> DefinitionsByEntityId { get; }
    }

    public enum TopologyRotationVisualMapping
    {
        ForwardUsesNegativeX = 0,
        ForwardUsesPositiveX = 1,
    }

    public readonly struct PlayerRespawnTimingAuthoritativeSnapshot
    {
        public PlayerRespawnTimingAuthoritativeSnapshot(
            float respawnDelaySeconds,
            int respawnDelayTicks)
        {
            RespawnDelaySeconds = respawnDelaySeconds;
            RespawnDelayTicks = respawnDelayTicks;
        }

        public float RespawnDelaySeconds { get; }

        public int RespawnDelayTicks { get; }
    }

    [Serializable]
    public sealed class PlayerRespawnTimingSettings
    {
        public float RespawnDelaySeconds;

        public static PlayerRespawnTimingSettings CreateDefault()
        {
            return new PlayerRespawnTimingSettings();
        }

        public PlayerRespawnTimingSettings Clone()
        {
            return new PlayerRespawnTimingSettings
            {
                RespawnDelaySeconds = RespawnDelaySeconds,
            };
        }

        public void Validate()
        {
            if (RespawnDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(RespawnDelaySeconds),
                    "Respawn delay must be zero or greater.");
            }
        }

        public PlayerRespawnTimingAuthoritativeSnapshot CreateAuthoritativeSnapshot(
            int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            Validate();

            var configuredDelayTicks = GameplayTimingProfile.SecondsToCeilTicks(
                RespawnDelaySeconds,
                simulationTicksPerSecond,
                allowZero: true);

            return new PlayerRespawnTimingAuthoritativeSnapshot(
                RespawnDelaySeconds,
                Mathf.Max(1, configuredDelayTicks));
        }
    }

    [Serializable]
    public sealed class GameplaySceneHostConfiguration
    {
        public bool AutoAdvanceTicks = true;
        public bool AutoCreateViews = true;
        public float CellSize = 1f;
        public float FaceSeamGap = -1f;
        public bool DirectionChangeConsumesDelay;
        public EnemyAiProfile DefaultEnemyAiProfile;
        public EnemyAiProfileOverride[] EnemyAiProfileOverrides = Array.Empty<EnemyAiProfileOverride>();
        public StageContentEntry StageContentEntry;
        public EnemyPresentationCatalog EnemyPresentationCatalog;
        public EnemyPresentationBinding[] EnemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        public StaticEntityPresentationCatalog StaticEntityPresentationCatalog;
        public StaticEntityPresentationBinding[] StaticEntityPresentationBindings = Array.Empty<StaticEntityPresentationBinding>();
        public BoardBounds InitialBoardBounds = BoardBounds.Unbounded;
        public float InitialMoveDelaySeconds = -1f;
        public EntityState[] InitialEntities = Array.Empty<EntityState>();
        public GameplayTerrainData InitialTerrain = GameplayTerrainData.Empty;
        public CubeTopologyState InitialTopology = new(FaceId.Floor);
        public int MaxTicksPerFrame = GameplayTimingProfile.DefaultMaxTicksPerFrame;
        public float MoveDeadzone = 0.5f;
        public StageObjectiveRuntimeDefinition ObjectiveRuntimeDefinition = StageObjectiveRuntimeDefinition.Disabled;
        public int PlayerEntityId = 1;
        public PlayerControlTimingSettings PlayerControlTiming = PlayerControlTimingSettings.CreateDefault();
        public PlayerRespawnTimingSettings PlayerRespawnTiming = PlayerRespawnTimingSettings.CreateDefault();
        public float MoveMotionDurationSeconds = -1f;
        public float PushMotionDurationSeconds = -1f;
        public float TopologyMotionDurationSeconds = -1f;
        public TopologyRotationVisualMapping TopologyRotationVisualMapping = TopologyRotationVisualMapping.ForwardUsesPositiveX;
        public TopologyRotationTweenSettings TopologyRotationTween = TopologyRotationTweenSettings.CreateDefault();
        public float FlipMotionDurationSeconds = -1f;
        public float ItemConsumeEffectDurationSeconds = -1f;
        public float BoxDestroyEffectDurationSeconds = -1f;
        public float EnemyDeathEffectDurationSeconds = -1f;
        public float FlipArcHeightInCells = GameplayTimingProfile.DefaultFlipArcHeightInCells;
        public float BoxSlideStepIntervalSeconds = -1f;
        public float ProjectileStepIntervalSeconds = -1f;
        public float RepeatedMoveIntervalSeconds = -1f;
        public int SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond;
        public GameplayCameraSettings CameraSettings = GameplayCameraSettings.CreateRuntimeDefault();
        public TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault();
        public TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault();
        public Texture2D BoardSurfaceTexture;
        public bool SnapViewCameraToTarget;
        public InputActionAsset Actions;
        public GameplayAudioMap GameplayAudioMap;
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
            var itemConsumeEffectDurationSeconds = ResolveItemConsumeEffectDurationSeconds();
            var boxDestroyEffectDurationSeconds = ResolveBoxDestroyEffectDurationSeconds();
            var enemyDeathEffectDurationSeconds = ResolveEnemyDeathEffectDurationSeconds();

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
                itemConsumeEffectDurationSeconds,
                boxDestroyEffectDurationSeconds,
                enemyDeathEffectDurationSeconds: enemyDeathEffectDurationSeconds);
        }

        public PlayerControlTimingAuthoritativeSnapshot CreatePlayerControlTimingSnapshot()
        {
            return CreatePlayerControlTimingSnapshot(ResolveRepeatedMoveIntervalSeconds());
        }

        public PlayerRespawnTimingAuthoritativeSnapshot CreatePlayerRespawnTimingSnapshot()
        {
            return ResolvePlayerRespawnTimingSettings().CreateAuthoritativeSnapshot(SimulationTicksPerSecond);
        }

        public EnemyAiRuntimeCollectionSnapshot CreateEnemyAiRuntimeSnapshot()
        {
            return new EnemyAiRuntimeCollectionSnapshot(
                ResolveDefaultEnemyAiRuntimeDefinition(),
                CreateEnemyAiDefinitionOverrides());
        }

        public float ResolveFaceSeamGap()
        {
            if (CellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(CellSize), "Cell size must be greater than zero.");
            }

            if (FaceSeamGap < 0f)
            {
                return CellSize;
            }

            return FaceSeamGap;
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

        private float ResolveItemConsumeEffectDurationSeconds()
        {
            return ItemConsumeEffectDurationSeconds > 0f
                ? ItemConsumeEffectDurationSeconds
                : GameplayTimingProfile.DefaultItemConsumeEffectDurationSeconds;
        }

        private float ResolveBoxDestroyEffectDurationSeconds()
        {
            return BoxDestroyEffectDurationSeconds > 0f
                ? BoxDestroyEffectDurationSeconds
                : GameplayTimingProfile.DefaultBoxDestroyEffectDurationSeconds;
        }

        private float ResolveEnemyDeathEffectDurationSeconds()
        {
            return EnemyDeathEffectDurationSeconds > 0f
                ? EnemyDeathEffectDurationSeconds
                : GameplayTimingProfile.DefaultEnemyDeathEffectDurationSeconds;
        }

        private PlayerControlTimingAuthoritativeSnapshot CreatePlayerControlTimingSnapshot(
            float repeatedMoveIntervalSeconds)
        {
            return ResolvePlayerControlTimingSettings().CreateAuthoritativeSnapshot(
                SimulationTicksPerSecond,
                repeatedMoveIntervalSeconds);
        }

        private EnemyAiRuntimeDefinition ResolveDefaultEnemyAiRuntimeDefinition()
        {
            return DefaultEnemyAiProfile != null
                ? DefaultEnemyAiProfile.CreateRuntimeDefinition(SimulationTicksPerSecond)
                : EnemyAiRuntimeDefinition.CreateDefaultMelee();
        }

        private PlayerControlTimingSettings ResolvePlayerControlTimingSettings()
        {
            return PlayerControlTiming?.Clone() ?? PlayerControlTimingSettings.CreateDefault();
        }

        private PlayerRespawnTimingSettings ResolvePlayerRespawnTimingSettings()
        {
            return PlayerRespawnTiming?.Clone() ?? PlayerRespawnTimingSettings.CreateDefault();
        }

        private IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> CreateEnemyAiDefinitionOverrides()
        {
            if (EnemyAiProfileOverrides == null ||
                EnemyAiProfileOverrides.Length == 0)
            {
                return null;
            }

            var definitionsByEntityId = new Dictionary<int, EnemyAiRuntimeDefinition>();
            for (var i = 0; i < EnemyAiProfileOverrides.Length; i++)
            {
                var overrideEntry = EnemyAiProfileOverrides[i];
                if (overrideEntry.EntityId <= 0)
                {
                    throw new ArgumentException("Enemy AI profile overrides require a positive entity ID.", nameof(EnemyAiProfileOverrides));
                }

                if (overrideEntry.Profile == null)
                {
                    throw new ArgumentException("Enemy AI profile overrides require a non-null profile.", nameof(EnemyAiProfileOverrides));
                }

                if (definitionsByEntityId.ContainsKey(overrideEntry.EntityId))
                {
                    throw new ArgumentException("Enemy AI profile overrides cannot contain duplicate entity IDs.", nameof(EnemyAiProfileOverrides));
                }

                definitionsByEntityId.Add(
                    overrideEntry.EntityId,
                    overrideEntry.Profile.CreateRuntimeDefinition(SimulationTicksPerSecond));
            }

            return definitionsByEntityId;
        }
    }
}
