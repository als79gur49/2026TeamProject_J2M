using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using Game.Shared.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyAiRuntimeCollectionSnapshot
    {
        public EnemyAiRuntimeCollectionSnapshot(
            EnemyAiRuntimeDefinition defaultDefinition,
            bool hasDefaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            DefaultDefinition = defaultDefinition;
            HasDefaultDefinition = hasDefaultDefinition;
            DefinitionsByEntityId = definitionsByEntityId;
            DefinitionsByArchetypeId = definitionsByArchetypeId;
            SpawnDefaultsByArchetypeId = spawnDefaultsByArchetypeId;
        }

        public EnemyAiRuntimeDefinition DefaultDefinition { get; }

        public bool HasDefaultDefinition { get; }

        public IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> DefinitionsByEntityId { get; }

        public IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> DefinitionsByArchetypeId { get; }

        public IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> SpawnDefaultsByArchetypeId { get; }
    }

    [Serializable]
    public sealed class GameplaySceneHostConfiguration
    {
        public bool AutoAdvanceTicks = true;
        public bool AutoCreateViews = true;
        public float CellSize = 1f;
        public float FaceSeamGap = -1f;
        public bool DirectionChangeConsumesDelay;
        public bool EnablePlayerFree2DActionAssist;
        public bool EnableEnemySameFaceContinuousLocomotion;
        public bool EnableEnemyChargeKinematicLocomotion;
        public bool EnableEnemyGlideKinematicLocomotion;
        public EnemyAiProfile DefaultEnemyAiProfile;
        public EnemyAiProfileOverride[] EnemyAiProfileOverrides = Array.Empty<EnemyAiProfileOverride>();
        public EnemyUnitArchetypeCatalog EnemyUnitArchetypeCatalog;
        public StageContentEntry StageContentEntry;
        public EnemyPresentationArchetypeCatalog EnemyPresentationArchetypeCatalog;
        public EnemyPresentationCatalog EnemyPresentationCatalog;
        public EnemyPresentationBinding[] EnemyPresentationBindings = Array.Empty<EnemyPresentationBinding>();
        public EnemyInactiveVisualSettings EnemyInactiveVisualSettings;
        public StaticEntityPresentationCatalog StaticEntityPresentationCatalog;
        public StaticEntityPresentationBinding[] StaticEntityPresentationBindings = Array.Empty<StaticEntityPresentationBinding>();
        public BoardPresentationProfile BoardPresentationProfile;
        public BoardTilePresentationCatalog BoardTilePresentationCatalog;
        public BoardTileStyleCatalog BoardTileStyleCatalog;
        public IReadOnlyList<BoardTilePaintOverride> BoardTilePaintOverrides =
            Array.Empty<BoardTilePaintOverride>();
        public IReadOnlyList<TileFeaturePresentationResolvedBinding> TileFeaturePresentationBindings =
            Array.Empty<TileFeaturePresentationResolvedBinding>();
        public StageWorldGuideCatalog WorldGuideCatalog;
        public IReadOnlyList<StageWorldGuideInstructionResolved> WorldGuideInstructions =
            Array.Empty<StageWorldGuideInstructionResolved>();
        public IReadOnlyList<SurfaceCell> SuppressedBaseTileCells =
            Array.Empty<SurfaceCell>();
        public BoardBounds InitialBoardBounds = BoardBounds.Unbounded;
        public float InitialMoveDelaySeconds = -1f;
        public EntityState[] InitialEntities = Array.Empty<EntityState>();
        public TileFeatureState[] InitialTileFeatures = Array.Empty<TileFeatureState>();
        public TileFeatureRuntimeDefinition[] TileFeatureDefinitions = Array.Empty<TileFeatureRuntimeDefinition>();
        public MoonBlockRespawnDefinition[] MoonBlockRespawnDefinitions = Array.Empty<MoonBlockRespawnDefinition>();
        public CubeTopologyState InitialTopology = new(FaceId.Floor);
        public int MaxTicksPerFrame = GameplayTimingProfile.DefaultMaxTicksPerFrame;
        public float MoveDeadzone = 0.5f;
        public StageObjectiveRuntimeDefinition ObjectiveRuntimeDefinition = StageObjectiveRuntimeDefinition.Disabled;
        public int PlayerEntityId = 1;
        public PlayerControlTimingSettings PlayerControlTiming = PlayerControlTimingSettings.CreateDefault();
        public UnitKinematicLocomotionTimingSettings UnitKinematicLocomotionTiming =
            UnitKinematicLocomotionTimingSettings.CreateDefault();
        public PlayerContinuousLocomotionSettings PlayerContinuousLocomotion =
            PlayerContinuousLocomotionSettings.CreateDefault();
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
        public GameplayCameraBaselineAuthoringPolicy CameraBaselineAuthoringPolicy =
            GameplayCameraBaselineAuthoringPolicy.CreateRuntimeDefault();
        public TopologyTransitionCameraShakeProfile TopologyTransitionCameraShakeProfile = TopologyTransitionCameraShakeProfile.CreateDefault();
        public GameplayCameraShakeProfile GameplayCameraShakeProfile;
        public TopologyTransitionPostFxProfile TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault();
        public Texture2D BoardSurfaceTexture;
        public bool SnapViewCameraToTarget;
        public InputActionAsset Actions;
        [NonSerialized] public IKeyboardBindingStore KeyboardBindingStore;
        public ITerminalSessionReadModel TerminalSessionReadModel = TerminalSessionRegistry.ReadModel;
        public ISceneEntryPresentationReadModel SceneEntryPresentationReadModel = SceneEntryPresentationRegistry.ReadModel;
        public GameplayPresentationAudioConfig GameplayPresentationAudioConfig;
        public ICampaignChancesReadSource CampaignChancesReadSource;
        public CampaignStageSequenceResolver CampaignStageSequenceResolver;
        public IEntityLogic[] StaticEntityLogics = Array.Empty<IEntityLogic>();
        public GameplayEntityView PlayerViewPrefab;
        public Camera ViewCamera;
        public IGameplayEntityViewFactory ViewFactory;
        internal StageStaticWallPresentationProvenance StaticWallPresentationProvenance =
            StageStaticWallPresentationProvenance.Empty;

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

        public UnitKinematicLocomotionTimingSnapshot CreateUnitKinematicLocomotionTimingSnapshot()
        {
            return ResolveUnitKinematicLocomotionTimingSettings()
                .CreateAuthoritativeSnapshot(SimulationTicksPerSecond);
        }

        public PlayerContinuousLocomotionSnapshot CreatePlayerContinuousLocomotionSnapshot()
        {
            return ResolvePlayerContinuousLocomotionSettings()
                .CreateAuthoritativeSnapshot(SimulationTicksPerSecond);
        }

        public GameplayRuntimeFeatureFlags CreateRuntimeFeatureFlags()
        {
            return new GameplayRuntimeFeatureFlags(
                enableEnemySameFaceContinuousLocomotion: EnableEnemySameFaceContinuousLocomotion,
                enableEnemyChargeKinematicLocomotion: EnableEnemyChargeKinematicLocomotion,
                enableEnemyGlideKinematicLocomotion: EnableEnemyGlideKinematicLocomotion,
                enablePlayerFree2DActionAssist: EnablePlayerFree2DActionAssist);
        }

        public void ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags flags)
        {
            EnableEnemySameFaceContinuousLocomotion = flags.EnableEnemySameFaceContinuousLocomotion;
            EnableEnemyChargeKinematicLocomotion = flags.EnableEnemyChargeKinematicLocomotion;
            EnableEnemyGlideKinematicLocomotion = flags.EnableEnemyGlideKinematicLocomotion;
            EnablePlayerFree2DActionAssist = flags.EnablePlayerFree2DActionAssist;
        }

        public EnemyAiRuntimeCollectionSnapshot CreateEnemyAiRuntimeSnapshot()
        {
            var hasDefaultDefinition = DefaultEnemyAiProfile != null;
            var defaultDefinition = ResolveDefaultEnemyAiRuntimeDefinition(hasDefaultDefinition);
            var definitionsByEntityId = CreateEnemyAiDefinitionOverrides();
            CreateEnemyAiArchetypeRuntimeCollections(
                out var definitionsByArchetypeId,
                out var spawnDefaultsByArchetypeId);
            ValidateSummonArchetypeReferences(
                defaultDefinition,
                definitionsByEntityId,
                definitionsByArchetypeId,
                spawnDefaultsByArchetypeId);
            return new EnemyAiRuntimeCollectionSnapshot(
                defaultDefinition,
                hasDefaultDefinition,
                definitionsByEntityId,
                definitionsByArchetypeId,
                spawnDefaultsByArchetypeId);
        }

        public EnemyPresentationArchetypeRegistry CreateEnemyPresentationArchetypeRegistry(
            in EnemyAiRuntimeCollectionSnapshot enemyAiRuntime)
        {
            return EnemyPresentationArchetypeCatalogResolver.BuildRegistry(
                EnemyPresentationArchetypeCatalog,
                CollectSummonArchetypeReferences(enemyAiRuntime),
                enemyAiRuntime.DefinitionsByArchetypeId,
                nameof(GameplaySceneHostConfiguration));
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

        private EnemyAiRuntimeDefinition ResolveDefaultEnemyAiRuntimeDefinition(bool hasDefaultDefinition)
        {
            return hasDefaultDefinition
                ? DefaultEnemyAiProfile.CreateRuntimeDefinition(SimulationTicksPerSecond)
                : default;
        }

        private PlayerControlTimingSettings ResolvePlayerControlTimingSettings()
        {
            return PlayerControlTiming?.Clone() ?? PlayerControlTimingSettings.CreateDefault();
        }

        private UnitKinematicLocomotionTimingSettings ResolveUnitKinematicLocomotionTimingSettings()
        {
            return UnitKinematicLocomotionTiming?.Clone() ??
                   UnitKinematicLocomotionTimingSettings.CreateDefault();
        }

        private PlayerContinuousLocomotionSettings ResolvePlayerContinuousLocomotionSettings()
        {
            return PlayerContinuousLocomotion?.Clone() ??
                   PlayerContinuousLocomotionSettings.CreateDefault();
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

        private void CreateEnemyAiArchetypeRuntimeCollections(
            out IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId,
            out IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            definitionsByArchetypeId = null;
            spawnDefaultsByArchetypeId = null;

            if (EnemyUnitArchetypeCatalog == null)
            {
                return;
            }

            var orderedEntries = new List<EnemyUnitArchetypeAsset>(EnemyUnitArchetypeCatalog.Entries.Count);
            for (var i = 0; i < EnemyUnitArchetypeCatalog.Entries.Count; i++)
            {
                var entry = EnemyUnitArchetypeCatalog.Entries[i];
                if (entry == null)
                {
                    throw new ArgumentException("Enemy unit archetype catalogs cannot contain null entries.", nameof(EnemyUnitArchetypeCatalog));
                }

                orderedEntries.Add(entry);
            }

            orderedEntries.Sort((left, right) => EnemyUnitArchetypeId.OrderingComparer.Compare(left.ArchetypeId, right.ArchetypeId));

            var compiledDefinitions = new Dictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition>(EnemyUnitArchetypeId.EqualityComparer);
            var compiledSpawnDefaults = new Dictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime>(EnemyUnitArchetypeId.EqualityComparer);
            for (var i = 0; i < orderedEntries.Count; i++)
            {
                var entry = orderedEntries[i];
                entry.ValidateConfiguration(nameof(EnemyUnitArchetypeCatalog));

                var archetypeId = entry.ArchetypeId;
                if (compiledDefinitions.ContainsKey(archetypeId))
                {
                    throw new ArgumentException(
                        $"Enemy unit archetype catalogs cannot contain duplicate archetype IDs ('{archetypeId}').",
                        nameof(EnemyUnitArchetypeCatalog));
                }

                compiledDefinitions.Add(archetypeId, entry.AiProfile.CreateRuntimeDefinition(SimulationTicksPerSecond));
                compiledSpawnDefaults.Add(archetypeId, entry.SpawnDefaults.ToRuntime());
            }

            definitionsByArchetypeId = compiledDefinitions.Count > 0 ? compiledDefinitions : null;
            spawnDefaultsByArchetypeId = compiledSpawnDefaults.Count > 0 ? compiledSpawnDefaults : null;
        }

        private void ValidateSummonArchetypeReferences(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            ValidateSummonArchetypeReferences(
                defaultDefinition,
                definitionsByArchetypeId,
                spawnDefaultsByArchetypeId,
                "Default enemy AI definition");

            if (definitionsByEntityId == null)
            {
                definitionsByEntityId = null;
            }
            else
            {
                var orderedEntityIds = new List<int>(definitionsByEntityId.Keys);
                orderedEntityIds.Sort();
                for (var i = 0; i < orderedEntityIds.Count; i++)
                {
                    var entityId = orderedEntityIds[i];
                    ValidateSummonArchetypeReferences(
                        definitionsByEntityId[entityId],
                        definitionsByArchetypeId,
                        spawnDefaultsByArchetypeId,
                        $"Enemy AI definition override for entity {entityId}");
                }
            }

            if (definitionsByArchetypeId == null)
            {
                return;
            }

            var orderedArchetypeIds = new List<EnemyUnitArchetypeId>(definitionsByArchetypeId.Keys);
            orderedArchetypeIds.Sort(EnemyUnitArchetypeId.OrderingComparer);
            for (var i = 0; i < orderedArchetypeIds.Count; i++)
            {
                var archetypeId = orderedArchetypeIds[i];
                ValidateSummonArchetypeReferences(
                    definitionsByArchetypeId[archetypeId],
                    definitionsByArchetypeId,
                    spawnDefaultsByArchetypeId,
                    $"Enemy unit archetype '{archetypeId}'");
            }
        }

        private static void ValidateSummonArchetypeReferences(
            EnemyAiRuntimeDefinition definition,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId,
            string definitionLabel)
        {
            if (!definition.TryGetSummonBehavior(out var summon))
            {
                return;
            }

            var archetypeId = summon.SummonedArchetypeId;
            if (definitionsByArchetypeId == null ||
                !definitionsByArchetypeId.ContainsKey(archetypeId))
            {
                throw new InvalidOperationException(
                    $"{definitionLabel} references missing enemy unit archetype definition '{archetypeId}' at behavior module '{EnemyBehaviorModuleKey.Summon}'.");
            }

            if (spawnDefaultsByArchetypeId == null ||
                !spawnDefaultsByArchetypeId.ContainsKey(archetypeId))
            {
                throw new InvalidOperationException(
                    $"{definitionLabel} references missing enemy unit spawn defaults '{archetypeId}' at behavior module '{EnemyBehaviorModuleKey.Summon}'.");
            }
        }

        private static IReadOnlyList<EnemyUnitArchetypeId> CollectSummonArchetypeReferences(
            in EnemyAiRuntimeCollectionSnapshot enemyAiRuntime)
        {
            var orderedReferences = new List<EnemyUnitArchetypeId>();
            var seenReferences = new HashSet<EnemyUnitArchetypeId>(EnemyUnitArchetypeId.EqualityComparer);

            if (enemyAiRuntime.HasDefaultDefinition)
            {
                CollectSummonArchetypeReferences(enemyAiRuntime.DefaultDefinition, seenReferences, orderedReferences);
            }

            if (enemyAiRuntime.DefinitionsByEntityId != null)
            {
                var orderedEntityIds = new List<int>(enemyAiRuntime.DefinitionsByEntityId.Keys);
                orderedEntityIds.Sort();
                for (var i = 0; i < orderedEntityIds.Count; i++)
                {
                    CollectSummonArchetypeReferences(
                        enemyAiRuntime.DefinitionsByEntityId[orderedEntityIds[i]],
                        seenReferences,
                        orderedReferences);
                }
            }

            if (enemyAiRuntime.DefinitionsByArchetypeId != null)
            {
                var orderedArchetypeIds = new List<EnemyUnitArchetypeId>(enemyAiRuntime.DefinitionsByArchetypeId.Keys);
                orderedArchetypeIds.Sort(EnemyUnitArchetypeId.OrderingComparer);
                for (var i = 0; i < orderedArchetypeIds.Count; i++)
                {
                    CollectSummonArchetypeReferences(
                        enemyAiRuntime.DefinitionsByArchetypeId[orderedArchetypeIds[i]],
                        seenReferences,
                        orderedReferences);
                }
            }

            return orderedReferences;
        }

        private static void CollectSummonArchetypeReferences(
            EnemyAiRuntimeDefinition definition,
            ISet<EnemyUnitArchetypeId> seenReferences,
            List<EnemyUnitArchetypeId> orderedReferences)
        {
            if (!definition.TryGetSummonBehavior(out var summon))
            {
                return;
            }

            var archetypeId = summon.SummonedArchetypeId;
            if (seenReferences.Add(archetypeId))
            {
                orderedReferences.Add(archetypeId);
            }
        }
    }
}
