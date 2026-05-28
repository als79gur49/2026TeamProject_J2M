using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class TickResultBuilder
    {
        private readonly TickPresentationDataBuilder _presentationDataBuilder = new();

        public TickResultData Build(
            WorldSnapshot finalSnapshot,
            IReadOnlyList<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            RespawnPhaseResult respawnPhaseResult,
            StageObjectiveTickResult objectiveResult,
            in TickPresentationBuildContext presentationBuildContext,
            IReadOnlyList<string> prePlanEventLogEntries = null)
        {
            if (finalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(finalSnapshot));
            }

            if (pendingDelayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(pendingDelayedAttackEffects));
            }

            if (movementPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(movementPhaseResult));
            }

            if (attackPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(attackPhaseResult));
            }

            if (cleanupPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(cleanupPhaseResult));
            }

            if (respawnPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(respawnPhaseResult));
            }

            if (objectiveResult == null)
            {
                throw new ArgumentNullException(nameof(objectiveResult));
            }

            var finalEntities = new List<EntityState>();
            finalSnapshot.EnumerateEntitiesOrdered(finalEntities);

            var eventLog = new List<string>(
                (prePlanEventLogEntries?.Count ?? 0) +
                movementPhaseResult.CommitEvents.Count +
                attackPhaseResult.EventLogEntries.Count +
                cleanupPhaseResult.RemovedEntityIds.Count +
                cleanupPhaseResult.TimerChanges.Count +
                cleanupPhaseResult.StateTransitions.Count +
                cleanupPhaseResult.EventLogEntries.Count +
                respawnPhaseResult.EventLogEntries.Count);

            if (prePlanEventLogEntries != null)
            {
                AddRange(eventLog, prePlanEventLogEntries);
            }

            AddRange(eventLog, movementPhaseResult.CommitEvents);
            AddRange(eventLog, attackPhaseResult.EventLogEntries);

            for (var i = 0; i < cleanupPhaseResult.RemovedEntityIds.Count; i++)
            {
                eventLog.Add($"CleanupRemoved|E={cleanupPhaseResult.RemovedEntityIds[i]}");
            }

            AddRange(eventLog, cleanupPhaseResult.TimerChanges);
            AddRange(eventLog, cleanupPhaseResult.StateTransitions);
            AddRange(eventLog, cleanupPhaseResult.EventLogEntries);
            AddRange(eventLog, respawnPhaseResult.EventLogEntries);

            return new TickResultData(
                finalEntities,
                pendingDelayedAttackEffects,
                eventLog,
                _presentationDataBuilder.Build(presentationBuildContext),
                objectiveResult);
        }

        private static void AddRange(List<string> destination, IReadOnlyList<string> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }
    }

    internal sealed class TickResultData
    {
        private readonly ReadOnlyCollection<string> _eventLog;
        private readonly ReadOnlyCollection<EntityState> _finalEntities;
        private readonly ReadOnlyCollection<DelayedAttackEffectRecord> _pendingDelayedAttackEffects;
        private readonly TickPresentationData _presentationData;
        private readonly StageObjectiveTickResult _objectiveResult;

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog)
            : this(
                finalEntities,
                pendingDelayedAttackEffects,
                eventLog,
                TickPresentationData.Empty,
                StageObjectiveTickResult.NoObjective)
        {
        }

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog,
            TickPresentationData presentationData,
            StageObjectiveTickResult objectiveResult = null)
        {
            if (finalEntities == null)
            {
                throw new ArgumentNullException(nameof(finalEntities));
            }

            if (pendingDelayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(pendingDelayedAttackEffects));
            }

            if (eventLog == null)
            {
                throw new ArgumentNullException(nameof(eventLog));
            }

            _presentationData = presentationData ?? throw new ArgumentNullException(nameof(presentationData));
            _objectiveResult = objectiveResult ?? StageObjectiveTickResult.NoObjective;
            _finalEntities = new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities));
            _pendingDelayedAttackEffects = new ReadOnlyCollection<DelayedAttackEffectRecord>(new List<DelayedAttackEffectRecord>(pendingDelayedAttackEffects));
            _eventLog = new ReadOnlyCollection<string>(new List<string>(eventLog));
        }

        public IReadOnlyList<EntityState> FinalEntities => _finalEntities;

        public IReadOnlyList<DelayedAttackEffectRecord> PendingDelayedAttackEffects => _pendingDelayedAttackEffects;

        public IReadOnlyList<string> EventLog => _eventLog;

        public TickPresentationData PresentationData => _presentationData;

        public StageObjectiveTickResult ObjectiveResult => _objectiveResult;
    }

    internal readonly struct RespawnTopologyResetRequest
    {
        public RespawnTopologyResetRequest(
            int entityId,
            FaceId targetFace,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind)
        {
            EntityId = entityId;
            TargetFace = targetFace;
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
        }

        public int EntityId { get; }

        public FaceId TargetFace { get; }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }
    }

    internal sealed class RespawnPhaseResult
    {
        public static readonly RespawnPhaseResult Empty = new(
            Array.Empty<EntityState>(),
            Array.Empty<string>(),
            respawnPlacementRecords: Array.Empty<RespawnPlacementRecord>(),
            moonBlockGeneratorRespawnFacts: Array.Empty<MoonBlockGeneratorRespawnFact>(),
            moonBlockGeneratorBlockedFacts: Array.Empty<MoonBlockGeneratorBlockedFact>());

        private readonly ReadOnlyCollection<string> _eventLogEntries;
        private readonly ReadOnlyCollection<MoonBlockGeneratorBlockedFact> _moonBlockGeneratorBlockedFacts;
        private readonly ReadOnlyCollection<PlayerRespawnDelayRecord> _playerRespawnDelayRecords;
        private readonly ReadOnlyCollection<RespawnPlacementRecord> _respawnPlacementRecords;
        private readonly ReadOnlyCollection<MoonBlockGeneratorRespawnFact> _moonBlockGeneratorRespawnFacts;
        private readonly ReadOnlyCollection<EntityState> _respawnedEntities;

        public RespawnPhaseResult(
            IEnumerable<EntityState> respawnedEntities,
            IEnumerable<string> eventLogEntries,
            RespawnTopologyResetRequest? topologyResetRequest)
            : this(
                respawnedEntities,
                eventLogEntries,
                playerRespawnDelayRecords: null,
                respawnPlacementRecords: null,
                topologyResetRequest: topologyResetRequest,
                moonBlockGeneratorRespawnFacts: null)
        {
        }

        public RespawnPhaseResult(
            IEnumerable<EntityState> respawnedEntities,
            IEnumerable<string> eventLogEntries,
            IEnumerable<PlayerRespawnDelayRecord> playerRespawnDelayRecords = null,
            IEnumerable<RespawnPlacementRecord> respawnPlacementRecords = null,
            RespawnTopologyResetRequest? topologyResetRequest = null,
            IEnumerable<MoonBlockGeneratorRespawnFact> moonBlockGeneratorRespawnFacts = null,
            IEnumerable<MoonBlockGeneratorBlockedFact> moonBlockGeneratorBlockedFacts = null)
        {
            if (respawnedEntities == null)
            {
                throw new ArgumentNullException(nameof(respawnedEntities));
            }

            if (eventLogEntries == null)
            {
                throw new ArgumentNullException(nameof(eventLogEntries));
            }

            _respawnedEntities = new ReadOnlyCollection<EntityState>(new List<EntityState>(respawnedEntities));
            _eventLogEntries = new ReadOnlyCollection<string>(new List<string>(eventLogEntries));
            _playerRespawnDelayRecords = new ReadOnlyCollection<PlayerRespawnDelayRecord>(
                new List<PlayerRespawnDelayRecord>(
                    playerRespawnDelayRecords ?? Array.Empty<PlayerRespawnDelayRecord>()));
            _respawnPlacementRecords = new ReadOnlyCollection<RespawnPlacementRecord>(
                new List<RespawnPlacementRecord>(
                    respawnPlacementRecords ?? Array.Empty<RespawnPlacementRecord>()));
            _moonBlockGeneratorRespawnFacts = new ReadOnlyCollection<MoonBlockGeneratorRespawnFact>(
                new List<MoonBlockGeneratorRespawnFact>(
                    moonBlockGeneratorRespawnFacts ?? Array.Empty<MoonBlockGeneratorRespawnFact>()));
            _moonBlockGeneratorBlockedFacts = new ReadOnlyCollection<MoonBlockGeneratorBlockedFact>(
                new List<MoonBlockGeneratorBlockedFact>(
                    moonBlockGeneratorBlockedFacts ?? Array.Empty<MoonBlockGeneratorBlockedFact>()));
            TopologyResetRequest = topologyResetRequest;
        }

        public IReadOnlyList<EntityState> RespawnedEntities => _respawnedEntities;

        public IReadOnlyList<string> EventLogEntries => _eventLogEntries;

        public IReadOnlyList<PlayerRespawnDelayRecord> PlayerRespawnDelayRecords => _playerRespawnDelayRecords;

        public IReadOnlyList<RespawnPlacementRecord> RespawnPlacementRecords => _respawnPlacementRecords;

        public IReadOnlyList<MoonBlockGeneratorRespawnFact> MoonBlockGeneratorRespawnFacts => _moonBlockGeneratorRespawnFacts;

        public IReadOnlyList<MoonBlockGeneratorBlockedFact> MoonBlockGeneratorBlockedFacts => _moonBlockGeneratorBlockedFacts;

        public RespawnTopologyResetRequest? TopologyResetRequest { get; }
    }

    internal readonly struct MoonBlockGeneratorRespawnFact
    {
        public MoonBlockGeneratorRespawnFact(
            int generatorTileId,
            SurfaceCell cell,
            int moonBlockEntityId,
            int spawnTick,
            int spawnInteractionLockTicks,
            int sourceEntityId,
            int ownerEntityId,
            int teamId)
        {
            GeneratorTileId = generatorTileId;
            Cell = cell;
            MoonBlockEntityId = moonBlockEntityId;
            SpawnTick = spawnTick;
            SpawnInteractionLockTicks = spawnInteractionLockTicks;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
        }

        public int GeneratorTileId { get; }

        public SurfaceCell Cell { get; }

        public int MoonBlockEntityId { get; }

        public int SpawnTick { get; }

        public int SpawnInteractionLockTicks { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }
    }

    internal readonly struct MoonBlockGeneratorBlockedFact
    {
        public MoonBlockGeneratorBlockedFact(
            int generatorTileId,
            SurfaceCell cell,
            MoonBlockGeneratorBlockedPayload payload,
            int sourceEntityId,
            int ownerEntityId,
            int teamId)
        {
            GeneratorTileId = generatorTileId;
            Cell = cell;
            Payload = payload;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
        }

        public int GeneratorTileId { get; }

        public SurfaceCell Cell { get; }

        public int BlockingEntityId => Payload.BlockingEntityId;

        public MoonBlockGeneratorBlockedPayload Payload { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }
    }

    internal readonly struct RespawnPlacementRecord
    {
        public RespawnPlacementRecord(
            int entityId,
            SurfaceCell placementCell,
            MovementExecutionBoundaryKind boundaryKind,
            string boundaryReason)
        {
            EntityId = entityId;
            PlacementCell = placementCell;
            BoundaryKind = boundaryKind;
            BoundaryReason = boundaryReason ?? string.Empty;
        }

        public int EntityId { get; }

        public SurfaceCell PlacementCell { get; }

        public MovementExecutionBoundaryKind BoundaryKind { get; }

        public string BoundaryReason { get; }
    }

    internal readonly struct PlayerRespawnDelayRecord
    {
        public PlayerRespawnDelayRecord(
            int entityId,
            int startTick,
            int eligibleTick,
            int delayTicks,
            int currentTick,
            bool startedThisTick,
            bool elapsedThisTick)
        {
            EntityId = entityId;
            StartTick = startTick;
            EligibleTick = eligibleTick;
            DelayTicks = delayTicks;
            CurrentTick = currentTick;
            StartedThisTick = startedThisTick;
            ElapsedThisTick = elapsedThisTick;
        }

        public int EntityId { get; }

        public int StartTick { get; }

        public int EligibleTick { get; }

        public int DelayTicks { get; }

        public int CurrentTick { get; }

        public bool StartedThisTick { get; }

        public bool ElapsedThisTick { get; }

        public int RemainingTicks => Math.Max(0, EligibleTick - CurrentTick);

        public bool IsActive => CurrentTick < EligibleTick;
    }

    internal readonly struct TickPresentationBuildContext
    {
        public TickPresentationBuildContext(
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            WorldSnapshot finalAuthoritativeSnapshot,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            int currentTickIndex = 0,
            WorldSnapshot jumpBaselineSnapshot = null,
            PlayerTickCommand playerCommand = default,
            IReadOnlyList<ResolutionRecord> resolutionRecords = null,
            IEnemyGlidePresentationSettingsResolver enemyGlidePresentationSettingsResolver = null,
            IReadOnlyList<TilePresentationEvent> tileEvents = null,
            StageObjectiveTickResult objectiveResult = null,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<GravityFieldPresentationEvent> gravityFieldEvents = null,
            int gravityFieldChargeDurationTicks = 0,
            int gravityFieldActiveDurationTicks = 0,
            IReadOnlyList<GravityFieldLockedTargetFact> gravityFieldLockedTargetFacts = null,
            FinalizationBatch finalizationBatch = null)
            : this(
                preMovementSnapshot,
                postMovementSnapshot,
                postAttackSnapshot,
                finalAuthoritativeSnapshot,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                RespawnPhaseResult.Empty,
                currentTickIndex,
                jumpBaselineSnapshot,
                playerCommand,
                resolutionRecords,
                enemyGlidePresentationSettingsResolver,
                tileEvents,
                objectiveResult,
                objectiveDefinition,
                tileFeatureDefinitions,
                gravityFieldEvents,
                gravityFieldChargeDurationTicks,
                gravityFieldActiveDurationTicks,
                gravityFieldLockedTargetFacts,
                finalizationBatch)
        {
        }

        public TickPresentationBuildContext(
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            WorldSnapshot finalAuthoritativeSnapshot,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            RespawnPhaseResult respawnPhaseResult,
            int currentTickIndex = 0,
            WorldSnapshot jumpBaselineSnapshot = null,
            PlayerTickCommand playerCommand = default,
            IReadOnlyList<ResolutionRecord> resolutionRecords = null,
            IEnemyGlidePresentationSettingsResolver enemyGlidePresentationSettingsResolver = null,
            IReadOnlyList<TilePresentationEvent> tileEvents = null,
            StageObjectiveTickResult objectiveResult = null,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<GravityFieldPresentationEvent> gravityFieldEvents = null,
            int gravityFieldChargeDurationTicks = 0,
            int gravityFieldActiveDurationTicks = 0,
            IReadOnlyList<GravityFieldLockedTargetFact> gravityFieldLockedTargetFacts = null,
            FinalizationBatch finalizationBatch = null)
            : this(
                preMovementSnapshot,
                postMovementSnapshot,
                postAttackSnapshot,
                finalAuthoritativeSnapshot,
                new PreMovementStatePhaseResult(new List<string>(), new List<PlayerActionTransition>()),
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                respawnPhaseResult,
                currentTickIndex,
                jumpBaselineSnapshot,
                playerCommand,
                resolutionRecords,
                enemyGlidePresentationSettingsResolver,
                tileEvents,
                objectiveResult,
                objectiveDefinition,
                tileFeatureDefinitions,
                gravityFieldEvents,
                gravityFieldChargeDurationTicks,
                gravityFieldActiveDurationTicks,
                gravityFieldLockedTargetFacts,
                finalizationBatch)
        {
        }

        public TickPresentationBuildContext(
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            WorldSnapshot finalAuthoritativeSnapshot,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            int currentTickIndex = 0,
            WorldSnapshot jumpBaselineSnapshot = null,
            PlayerTickCommand playerCommand = default,
            IReadOnlyList<ResolutionRecord> resolutionRecords = null,
            IEnemyGlidePresentationSettingsResolver enemyGlidePresentationSettingsResolver = null,
            IReadOnlyList<TilePresentationEvent> tileEvents = null,
            StageObjectiveTickResult objectiveResult = null,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<GravityFieldPresentationEvent> gravityFieldEvents = null,
            int gravityFieldChargeDurationTicks = 0,
            int gravityFieldActiveDurationTicks = 0,
            IReadOnlyList<GravityFieldLockedTargetFact> gravityFieldLockedTargetFacts = null,
            FinalizationBatch finalizationBatch = null)
            : this(
                preMovementSnapshot,
                postMovementSnapshot,
                postAttackSnapshot,
                finalAuthoritativeSnapshot,
                preMovementStatePhaseResult,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                RespawnPhaseResult.Empty,
                currentTickIndex,
                jumpBaselineSnapshot,
                playerCommand,
                resolutionRecords,
                enemyGlidePresentationSettingsResolver,
                tileEvents,
                objectiveResult,
                objectiveDefinition,
                tileFeatureDefinitions,
                gravityFieldEvents,
                gravityFieldChargeDurationTicks,
                gravityFieldActiveDurationTicks,
                gravityFieldLockedTargetFacts,
                finalizationBatch)
        {
        }

        public TickPresentationBuildContext(
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            WorldSnapshot finalAuthoritativeSnapshot,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            RespawnPhaseResult respawnPhaseResult,
            int currentTickIndex = 0,
            WorldSnapshot jumpBaselineSnapshot = null,
            PlayerTickCommand playerCommand = default,
            IReadOnlyList<ResolutionRecord> resolutionRecords = null,
            IEnemyGlidePresentationSettingsResolver enemyGlidePresentationSettingsResolver = null,
            IReadOnlyList<TilePresentationEvent> tileEvents = null,
            StageObjectiveTickResult objectiveResult = null,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            IReadOnlyList<GravityFieldPresentationEvent> gravityFieldEvents = null,
            int gravityFieldChargeDurationTicks = 0,
            int gravityFieldActiveDurationTicks = 0,
            IReadOnlyList<GravityFieldLockedTargetFact> gravityFieldLockedTargetFacts = null,
            FinalizationBatch finalizationBatch = null,
            IReadOnlyList<PlayerActionAttemptResolution> playerActionAttemptResolutions = null,
            IReadOnlyList<FlipDueContactPresentationSignal> dueFlipContactSignals = null,
            IReadOnlyList<DamageResolutionRecord> dueDamageResolutions = null)
        {
            PreMovementSnapshot = preMovementSnapshot ?? throw new ArgumentNullException(nameof(preMovementSnapshot));
            PostMovementSnapshot = postMovementSnapshot ?? throw new ArgumentNullException(nameof(postMovementSnapshot));
            PostAttackSnapshot = postAttackSnapshot ?? throw new ArgumentNullException(nameof(postAttackSnapshot));
            FinalAuthoritativeSnapshot = finalAuthoritativeSnapshot ?? throw new ArgumentNullException(nameof(finalAuthoritativeSnapshot));
            PreMovementStatePhaseResult = preMovementStatePhaseResult ?? throw new ArgumentNullException(nameof(preMovementStatePhaseResult));
            MovementPhaseResult = movementPhaseResult ?? throw new ArgumentNullException(nameof(movementPhaseResult));
            AttackPhaseResult = attackPhaseResult ?? throw new ArgumentNullException(nameof(attackPhaseResult));
            CleanupPhaseResult = cleanupPhaseResult ?? throw new ArgumentNullException(nameof(cleanupPhaseResult));
            RespawnPhaseResult = respawnPhaseResult ?? throw new ArgumentNullException(nameof(respawnPhaseResult));
            FinalizationBatch = finalizationBatch ?? new FinalizationBatch();
            CurrentTickIndex = currentTickIndex;
            JumpBaselineSnapshot = jumpBaselineSnapshot ?? PreMovementSnapshot;
            PlayerCommand = playerCommand;
            ResolutionRecords = resolutionRecords ?? Array.Empty<ResolutionRecord>();
            EnemyGlidePresentationSettingsResolver = enemyGlidePresentationSettingsResolver;
            TileEvents = tileEvents ?? Array.Empty<TilePresentationEvent>();
            ObjectiveResult = objectiveResult ?? StageObjectiveTickResult.NoObjective;
            ObjectiveDefinition = objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled;
            TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
            GravityFieldEvents = gravityFieldEvents ?? Array.Empty<GravityFieldPresentationEvent>();
            GravityFieldLockedTargetFacts = gravityFieldLockedTargetFacts ?? Array.Empty<GravityFieldLockedTargetFact>();
            PlayerActionAttemptResolutions = playerActionAttemptResolutions ?? Array.Empty<PlayerActionAttemptResolution>();
            DueFlipContactSignals = dueFlipContactSignals ?? Array.Empty<FlipDueContactPresentationSignal>();
            DueDamageResolutions = dueDamageResolutions ?? Array.Empty<DamageResolutionRecord>();
            GravityFieldChargeDurationTicks = gravityFieldChargeDurationTicks > 0
                ? gravityFieldChargeDurationTicks
                : GameplayTimingProfile.SecondsToCeilTicks(
                    GravityFieldRuntimePolicy.ChargeDurationSeconds,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            GravityFieldActiveDurationTicks = gravityFieldActiveDurationTicks > 0
                ? gravityFieldActiveDurationTicks
                : GameplayTimingProfile.SecondsToCeilTicks(
                    GravityFieldRuntimePolicy.ActiveDurationSeconds,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        public WorldSnapshot PreMovementSnapshot { get; }

        // Movement-visible surface after resolve. This may include accepted impact
        // follow-through vacates/moves, but it is not the authoritative HP/destroy truth.
        public WorldSnapshot PostMovementSnapshot { get; }

        // Attack-authoritative surface after damage/destroy has been resolved.
        public WorldSnapshot PostAttackSnapshot { get; }

        public WorldSnapshot FinalAuthoritativeSnapshot { get; }

        public PreMovementStatePhaseResult PreMovementStatePhaseResult { get; }

        public MovementPhaseResult MovementPhaseResult { get; }

        public AttackPhaseResult AttackPhaseResult { get; }

        public CleanupPhaseResult CleanupPhaseResult { get; }

        public RespawnPhaseResult RespawnPhaseResult { get; }

        public FinalizationBatch FinalizationBatch { get; }

        public int CurrentTickIndex { get; }

        public WorldSnapshot JumpBaselineSnapshot { get; }

        public PlayerTickCommand PlayerCommand { get; }

        public IReadOnlyList<ResolutionRecord> ResolutionRecords { get; }

        public IReadOnlyList<TilePresentationEvent> TileEvents { get; }

        public IReadOnlyList<GravityFieldPresentationEvent> GravityFieldEvents { get; }

        public IReadOnlyList<GravityFieldLockedTargetFact> GravityFieldLockedTargetFacts { get; }

        public IReadOnlyList<PlayerActionAttemptResolution> PlayerActionAttemptResolutions { get; }

        public IReadOnlyList<FlipDueContactPresentationSignal> DueFlipContactSignals { get; }

        public IReadOnlyList<DamageResolutionRecord> DueDamageResolutions { get; }

        public int GravityFieldChargeDurationTicks { get; }

        public int GravityFieldActiveDurationTicks { get; }

        public StageObjectiveTickResult ObjectiveResult { get; }

        public StageObjectiveRuntimeDefinition ObjectiveDefinition { get; }

        public IReadOnlyList<TileFeatureRuntimeDefinition> TileFeatureDefinitions { get; }

        internal IEnemyGlidePresentationSettingsResolver EnemyGlidePresentationSettingsResolver { get; }
    }

    internal sealed class TickPresentationDataBuilder
    {
        private enum TickTopologyTransitionSource
        {
            None = 0,
            SnapshotDiff = 1,
            Free2DNative = 2,
        }

        private readonly struct TickTopologyTransitionFact
        {
            public TickTopologyTransitionFact(
                bool hasTransition,
                CubeTopologyState sourceTopology,
                CubeTopologyState destinationTopology,
                CubeRotationKind rotationKind,
                TickTopologyTransitionSource source)
            {
                HasTransition = hasTransition;
                SourceTopology = sourceTopology;
                DestinationTopology = destinationTopology;
                RotationKind = rotationKind;
                Source = source;
            }

            public bool HasTransition { get; }

            public CubeTopologyState SourceTopology { get; }

            public CubeTopologyState DestinationTopology { get; }

            public CubeRotationKind RotationKind { get; }

            public TickTopologyTransitionSource Source { get; }
        }

        public TickPresentationData Build(in TickPresentationBuildContext context)
        {
            var entityMotions = new List<TickEntityMotion>();
            var entityExitSignals = new List<TickEntityExitPresentationSignal>();
            var impactTransientSignals = new List<TickImpactTransientPresentationSignal>();
            var flipImpactSignals = new List<FlipImpactPresentationSignal>();
            var flipFloorImpactSignals = new List<FlipFloorImpactPresentationSignal>();
            var flipB1InFlightMotionSignals = new List<FlipB1InFlightMotionPresentationSignal>();
            var flipDueContactSignals = new List<FlipDueContactPresentationSignal>();
            var boxSlideStopSignals = new List<BoxSlideStopPresentationSignal>();
            var boxSlideStartSignals = new List<BoxSlideStartPresentationSignal>();
            var enemyActionSignals = new List<TickEnemyActionPresentationSignal>();
            var enemyDamageSignals = new List<TickEnemyDamagePresentationSignal>();
            var enemyJumpSignals = new List<TickEnemyJumpPresentationSignal>();
            var enemyChargeSignals = new List<TickEnemyChargePresentationSignal>();
            var enemyGlideSignals = new List<TickEnemyGlidePresentationSignal>();
            var enemyUtilitySignals = new List<TickEnemyUtilityPresentationSignal>();
            var enemyUtilityPhaseStates = new List<TickEnemyUtilityPhasePresentationState>();
            var enemyUtilityCooldownSignals = new List<TickEnemyUtilityCooldownPresentationSignal>();
            var enemyGravityFieldAuraVisualStates = new List<TickEnemyGravityFieldAuraVisualState>();
            var forwardCellProjectileWindupSignals =
                new List<TickForwardCellProjectileWindupPresentationSignal>();
            var forwardCellProjectileReleaseSignals =
                new List<TickForwardCellProjectileReleasePresentationSignal>();
            var forwardCellProjectileClearSignals =
                new List<TickForwardCellProjectileClearPresentationSignal>();
            var forwardCellImpactSignals = new List<TickForwardCellImpactPresentationSignal>();
            var frontFaceShieldSourceSignals = new List<TickFrontFaceShieldSourceSignal>();
            var frontFaceShieldBlockSignals = new List<TickFrontFaceShieldBlockSignal>();
            var summonWindupWarnings = new List<TickSummonWindupWarningSignal>();
            var frontFaceShieldWindupWarnings = new List<TickFrontFaceShieldWindupWarningSignal>();
            var playerActionSignals = new List<TickPlayerActionPresentationSignal>();
            var playerActionAttemptSignals = new List<TickPlayerActionAttemptPresentationSignal>();
            var playerFlipResultTurnSignals = new List<TickPlayerFlipResultTurnSignal>();
            var playerDamageSignals = new List<TickPlayerDamagePresentationSignal>();
            var playerDeathHoldSignals = new List<TickPlayerDeathHoldPresentationSignal>();
            var playerDeathSignals = new List<TickPlayerDeathPresentationSignal>();
            var playerLocomotionSignals = new List<TickPlayerLocomotionPresentationSignal>();
            var playerOutcomeSignals = new List<TickPlayerOutcomePresentationSignal>();
            var summonedEnemyPresentationBindings = new List<TickSummonedEnemyPresentationBinding>();
            var visibilityChanges = new List<TickVisibilityChange>();
            var transitionVisibilityChanges = new List<TickTransitionVisibilityChange>();
            var kinematicMotionTracks = new List<TickKinematicMotionTrack>();
            var continuousLocomotionTracks = new List<TickContinuousLocomotionTrack>();
            var entitySpawnSignals = new List<EntitySpawnPresentationSignal>();
            var tileEvents = new List<TilePresentationEvent>();
            var tileFeatureVisualStates = new List<TileFeatureVisualState>();
            var tileFeatureVisibleVisualStates = new List<TileFeatureVisualState>();
            var gravityFieldEvents = new List<GravityFieldPresentationEvent>();
            var gravityFieldVisualStates = new List<GravityFieldVisualState>();
            var tileFeatureActiveVisualStates = new List<TileFeatureActiveVisualState>();
            var exitOwnedEntityIds = new HashSet<int>();
            var topologyFact = ResolveTopologyTransitionFact(context);

            BuildTilePresentationEvents(context, topologyFact, tileEvents);
            BuildTileFeatureVisualStates(context, topologyFact, tileEvents, tileFeatureVisualStates);
            BuildTileFeatureVisibleVisualStates(context, topologyFact, tileFeatureVisibleVisualStates);
            BuildGravityFieldPresentationEvents(context, gravityFieldEvents);
            BuildGravityFieldVisualStates(context, gravityFieldVisualStates);
            BuildTileFeatureActiveVisualStates(context, topologyFact, tileFeatureActiveVisualStates);
            BuildFlipDueContactPresentation(context, flipDueContactSignals);
            BuildFlipB1InFlightMotionPresentation(context, flipB1InFlightMotionSignals);
            BuildEntityExitPresentation(context, entityExitSignals, exitOwnedEntityIds);
            BuildFlipImpactPresentation(context, flipImpactSignals);
            BuildFlipFloorImpactPresentation(context, flipFloorImpactSignals);
            BuildBoxSlideStopPresentation(context, boxSlideStopSignals);
            BuildBoxSlideStartPresentation(context, boxSlideStartSignals);
            BuildImpactTransientPresentation(context, impactTransientSignals);
            BuildMovementPresentation(context, entityMotions, visibilityChanges, exitOwnedEntityIds);
            BuildKinematicMotionPresentation(context, kinematicMotionTracks);
            BuildContinuousLocomotionPresentation(context, continuousLocomotionTracks);
            BuildAttackPresentation(context, visibilityChanges);
            BuildCleanupPresentation(context, visibilityChanges, exitOwnedEntityIds);
            BuildRespawnPresentation(context, visibilityChanges, entitySpawnSignals);
            BuildPlayerPresentation(context, playerActionSignals);
            BuildPlayerFlipResultTurnPresentation(context, playerFlipResultTurnSignals);
            BuildPlayerActionAttemptPresentation(context, playerActionAttemptSignals);
            BuildPlayerDamagePresentation(context, playerDamageSignals);
            BuildPlayerDeathPresentation(context, playerDeathSignals);
            BuildPlayerDeathHoldPresentation(context, playerDeathHoldSignals);
            BuildPlayerLocomotionPresentation(context, playerLocomotionSignals);
            BuildPlayerOutcomePresentation(context, playerOutcomeSignals);
            BuildEnemyDamagePresentation(context, enemyDamageSignals);
            BuildEnemyPresentation(context, enemyActionSignals);
            BuildForwardCellProjectilePresentation(
                context,
                forwardCellProjectileWindupSignals,
                forwardCellProjectileReleaseSignals,
                forwardCellProjectileClearSignals);
            BuildForwardCellImpactPresentation(context, forwardCellImpactSignals);
            BuildEnemyJumpPresentation(context, enemyJumpSignals);
            BuildEnemyChargePresentation(context, enemyChargeSignals);
            BuildEnemyGlidePresentation(context, enemyGlideSignals);
            BuildFrontFaceShieldPresentation(context, frontFaceShieldSourceSignals, frontFaceShieldBlockSignals);
            BuildEnemyUtilityWindupPresentation(
                context,
                summonWindupWarnings,
                frontFaceShieldWindupWarnings,
                enemyUtilitySignals,
                enemyUtilityPhaseStates,
                enemyUtilityCooldownSignals,
                enemyGravityFieldAuraVisualStates);
            BuildSummonedEnemyPresentationBindings(context, summonedEnemyPresentationBindings);

            var topologyMotion = BuildTopologyMotion(context, topologyFact);
            BuildTransitionVisibilityPresentation(context, visibilityChanges, entityExitSignals, transitionVisibilityChanges);

            return entityMotions.Count == 0 &&
                   enemyActionSignals.Count == 0 &&
                   enemyDamageSignals.Count == 0 &&
                   enemyJumpSignals.Count == 0 &&
                   enemyChargeSignals.Count == 0 &&
                   enemyGlideSignals.Count == 0 &&
                   enemyUtilitySignals.Count == 0 &&
                   enemyUtilityPhaseStates.Count == 0 &&
                   enemyUtilityCooldownSignals.Count == 0 &&
                   enemyGravityFieldAuraVisualStates.Count == 0 &&
                   forwardCellProjectileWindupSignals.Count == 0 &&
                   forwardCellProjectileReleaseSignals.Count == 0 &&
                   forwardCellProjectileClearSignals.Count == 0 &&
                   forwardCellImpactSignals.Count == 0 &&
                   frontFaceShieldSourceSignals.Count == 0 &&
                   frontFaceShieldBlockSignals.Count == 0 &&
                   summonWindupWarnings.Count == 0 &&
                   frontFaceShieldWindupWarnings.Count == 0 &&
                   entityExitSignals.Count == 0 &&
                   impactTransientSignals.Count == 0 &&
                   flipImpactSignals.Count == 0 &&
                   flipFloorImpactSignals.Count == 0 &&
                   flipB1InFlightMotionSignals.Count == 0 &&
                   flipDueContactSignals.Count == 0 &&
                   boxSlideStopSignals.Count == 0 &&
                   boxSlideStartSignals.Count == 0 &&
                   playerActionSignals.Count == 0 &&
                   playerActionAttemptSignals.Count == 0 &&
                   playerFlipResultTurnSignals.Count == 0 &&
                   playerDamageSignals.Count == 0 &&
                   playerDeathHoldSignals.Count == 0 &&
                   playerDeathSignals.Count == 0 &&
                   playerLocomotionSignals.Count == 0 &&
                   playerOutcomeSignals.Count == 0 &&
                   summonedEnemyPresentationBindings.Count == 0 &&
                   visibilityChanges.Count == 0 &&
                   transitionVisibilityChanges.Count == 0 &&
                   kinematicMotionTracks.Count == 0 &&
                   continuousLocomotionTracks.Count == 0 &&
                   entitySpawnSignals.Count == 0 &&
                   tileEvents.Count == 0 &&
                   tileFeatureVisualStates.Count == 0 &&
                   tileFeatureVisibleVisualStates.Count == 0 &&
                   gravityFieldEvents.Count == 0 &&
                   gravityFieldVisualStates.Count == 0 &&
                   tileFeatureActiveVisualStates.Count == 0 &&
                   !topologyMotion.HasValue
                ? TickPresentationData.Empty
                : new TickPresentationData(
                    entityMotions,
                    topologyMotion,
                    visibilityChanges,
                    transitionVisibilityChanges,
                    playerActionSignals,
                    playerLocomotionSignals,
                    playerDamageSignals,
                    playerDeathSignals,
                    enemyDamageSignals,
                    enemyActionSignals,
                    enemyJumpSignals,
                    enemyChargeSignals,
                    entityExitSignals,
                    impactTransientSignals,
                    flipImpactSignals,
                    summonedEnemyPresentationBindings,
                    frontFaceShieldSourceSignals,
                    frontFaceShieldBlockSignals,
                    summonWindupWarnings,
                    frontFaceShieldWindupWarnings,
                    kinematicMotionTracks,
                    playerDeathHoldSignals,
                    continuousLocomotionTracks,
                    enemyGlideSignals,
                    tileEvents,
                    gravityFieldEvents,
                    gravityFieldVisualStates,
                    playerActionAttemptSignals,
                    boxSlideStopSignals,
                    enemyUtilitySignals,
                    enemyUtilityCooldownSignals,
                    enemyGravityFieldAuraVisualStates,
                    boxSlideStartSignals,
                    tileFeatureVisualStates,
                    tileFeatureVisibleVisualStates,
                    tileFeatureActiveVisualStates,
                    playerFlipResultTurnSignals,
                    flipFloorImpactSignals,
                    forwardCellImpactSignals,
                    forwardCellProjectileWindupSignals,
                    forwardCellProjectileReleaseSignals,
                    forwardCellProjectileClearSignals,
                    entitySpawnSignals,
                    playerOutcomeSignals,
                    enemyUtilityPhaseStates,
                    flipDueContactSignals: flipDueContactSignals,
                    flipB1InFlightMotionSignals: flipB1InFlightMotionSignals);
        }

        private static void BuildFlipB1InFlightMotionPresentation(
            in TickPresentationBuildContext context,
            List<FlipB1InFlightMotionPresentationSignal> flipB1InFlightMotionSignals)
        {
            var operations = context.MovementPhaseResult.ResolvedOperations;
            var emittedActionIds = new HashSet<int>();
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.AddScheduledFlipResolution)
                {
                    continue;
                }

                var contact = operation.ScheduledFlipResolution;
                if (contact.ActionId <= 0 ||
                    contact.SourceBoxEntityId <= 0 ||
                    contact.ExecuteTick != context.CurrentTickIndex ||
                    !emittedActionIds.Add(contact.ActionId))
                {
                    continue;
                }

                flipB1InFlightMotionSignals.Add(
                    new FlipB1InFlightMotionPresentationSignal(
                        contact.ActionId,
                        contact.SourceBoxEntityId,
                        contact.ActorEntityId,
                        contact.SourceCell,
                        contact.ContactCell,
                        contact.LandingCell,
                        context.PreMovementSnapshot.Topology,
                        contact.FlipDirection,
                        contact.ExecuteTick,
                        contact.DueTick,
                        Math.Max(0, contact.DueTick - contact.ExecuteTick)));
            }
        }

        private static void BuildFlipDueContactPresentation(
            in TickPresentationBuildContext context,
            List<FlipDueContactPresentationSignal> flipDueContactSignals)
        {
            var signals = context.DueFlipContactSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                flipDueContactSignals.Add(signals[i]);
            }
        }

        private static void BuildForwardCellProjectilePresentation(
            in TickPresentationBuildContext context,
            List<TickForwardCellProjectileWindupPresentationSignal> windupSignals,
            List<TickForwardCellProjectileReleasePresentationSignal> releaseSignals,
            List<TickForwardCellProjectileClearPresentationSignal> clearSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var preMovementEntries = new List<EnemyActionSnapshotEntry>();
            var postAttackEntries = new List<EnemyActionSnapshotEntry>();

            context.PreMovementSnapshot.EnumerateEnemyActionStatesOrdered(preMovementEntries);
            context.PostAttackSnapshot.EnumerateEnemyActionStatesOrdered(postAttackEntries);
            CollectEnemyActionCandidateIds(preMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyActionCandidateIds(postAttackEntries, seenEntityIds, candidateEntityIds);

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                context.PreMovementSnapshot.TryGetEnemyActionState(entityId, out var previousAction);
                context.PostAttackSnapshot.TryGetEnemyActionState(entityId, out var currentAction);
                var transition = new EnemyActionTransition(entityId, previousAction, currentAction);

                if (transition.StartedThisTick &&
                    currentAction.kind == EnemyActionKind.ForwardCellProjectile &&
                    currentAction.hasLockedForwardCellImpact)
                {
                    windupSignals.Add(
                        new TickForwardCellProjectileWindupPresentationSignal(
                            ComputeForwardCellProjectilePresentationKey(entityId, currentAction.sequence),
                            entityId,
                            entityId,
                            currentAction.lockedTargetCell,
                            currentAction.lockedAttackDirection,
                            currentAction.startTick,
                            currentAction.executeTick));
                }

                if (transition.CanceledThisTick &&
                    previousAction.kind == EnemyActionKind.ForwardCellProjectile &&
                    previousAction.hasLockedForwardCellImpact)
                {
                    clearSignals.Add(
                        new TickForwardCellProjectileClearPresentationSignal(
                            ComputeForwardCellProjectilePresentationKey(entityId, previousAction.sequence),
                            entityId,
                            entityId,
                            previousAction.lockedTargetCell,
                            previousAction.lockedAttackDirection,
                            previousAction.startTick,
                            ForwardCellProjectileClearReason.Canceled));
                }
            }

            var operations = context.FinalizationBatch.Operations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.AddPendingCellImpact)
                {
                    continue;
                }

                var impact = operation.PendingCellImpact;
                var sourceCell = ResolveForwardCellProjectileSourceCell(context, impact);
                releaseSignals.Add(
                    new TickForwardCellProjectileReleasePresentationSignal(
                        impact.ImpactId,
                        impact.ImpactId,
                        impact.OwnerId,
                        impact.SourceEnemyId,
                        sourceCell,
                        impact.TargetCell,
                        impact.Direction,
                        impact.ReleaseTick,
                        impact.ImpactTick,
                        impact.ImpactTick - impact.ReleaseTick));
            }
        }

        private static void BuildForwardCellImpactPresentation(
            in TickPresentationBuildContext context,
            List<TickForwardCellImpactPresentationSignal> forwardCellImpactSignals)
        {
            var resolutions = context.AttackPhaseResult.PendingCellImpactResolutions;
            for (var i = 0; i < resolutions.Count; i++)
            {
                var resolution = resolutions[i];
                var impact = resolution.Impact;
                forwardCellImpactSignals.Add(
                    new TickForwardCellImpactPresentationSignal(
                        impact.ImpactId,
                        impact.ImpactId,
                        impact.OwnerId,
                        impact.SourceEnemyId,
                        impact.TargetCell,
                        impact.Direction,
                        resolution.Hit,
                        resolution.TargetEntityId));
            }
        }

        private static int ComputeForwardCellProjectilePresentationKey(int ownerId, int actionSequence)
        {
            return checked((ownerId * 100000) + Math.Max(1, actionSequence));
        }

        private static SurfaceCell ResolveForwardCellProjectileSourceCell(
            in TickPresentationBuildContext context,
            in PendingCellImpact impact)
        {
            if (TryResolveLockedProjectileSourceCell(context.PostAttackSnapshot, impact.SourceEnemyId, out var sourceCell) ||
                TryResolveLockedProjectileSourceCell(context.PreMovementSnapshot, impact.SourceEnemyId, out sourceCell))
            {
                return sourceCell;
            }

            return context.PostAttackSnapshot.TryGetEntity(impact.SourceEnemyId, out var sourceEntity)
                ? sourceEntity.position
                : impact.TargetCell;
        }

        private static bool TryResolveLockedProjectileSourceCell(
            WorldSnapshot snapshot,
            int sourceEnemyId,
            out SurfaceCell sourceCell)
        {
            if (snapshot != null &&
                snapshot.TryGetEnemyActionState(sourceEnemyId, out var action) &&
                action.kind == EnemyActionKind.ForwardCellProjectile &&
                action.hasLockedForwardCellImpact)
            {
                sourceCell = action.lockedAttackBaseCell;
                return true;
            }

            sourceCell = default;
            return false;
        }

        private static void BuildBoxSlideStartPresentation(
            in TickPresentationBuildContext context,
            List<BoxSlideStartPresentationSignal> boxSlideStartSignals)
        {
            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.MoveEntity ||
                    operation.Metadata.MovementSemanticKind != MovementSemanticKind.Slide ||
                    operation.Metadata.SourceActorEntityId <= 0 ||
                    operation.Metadata.SourceActorEntityId == operation.EntityId ||
                    !context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity) ||
                    sourceEntity.type != EntityType.Box)
                {
                    continue;
                }

                boxSlideStartSignals.Add(
                    new BoxSlideStartPresentationSignal(
                        operation.EntityId,
                        operation.Metadata.SourceActorEntityId,
                        sourceEntity.position,
                        operation.Destination,
                        context.PreMovementSnapshot.Topology));
            }
        }

        private static void BuildBoxSlideStopPresentation(
            in TickPresentationBuildContext context,
            List<BoxSlideStopPresentationSignal> boxSlideStopSignals)
        {
            var stops = context.MovementPhaseResult.BoxSlideStops;
            for (var i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                boxSlideStopSignals.Add(
                    new BoxSlideStopPresentationSignal(
                        stop.BoxEntityId,
                        stop.SourceCell,
                        stop.StopperCell,
                        stop.SlideDirection,
                        stop.StopperKind,
                        stop.StopperEntityId,
                        stop.SolidKind,
                        stop.Topology,
                        stop.Cause,
                        stop.StopperTileId));
            }
        }

        private static void BuildTileFeatureActiveVisualStates(
            in TickPresentationBuildContext context,
            in TickTopologyTransitionFact topologyFact,
            List<TileFeatureActiveVisualState> tileFeatureActiveVisualStates)
        {
            var finalTopology = topologyFact.HasTransition
                ? topologyFact.DestinationTopology
                : context.FinalAuthoritativeSnapshot.Topology;
            var finalTileFeatures = new List<TileFeatureState>();
            context.FinalAuthoritativeSnapshot.EnumerateTileFeaturesOrdered(finalTileFeatures);

            for (var i = 0; i < finalTileFeatures.Count; i++)
            {
                var finalTileFeature = finalTileFeatures[i];
                if (finalTileFeature.Kind != TileFeatureKind.Destroy ||
                    !TryGetTileFeatureDefinition(
                        context.TileFeatureDefinitions,
                        finalTileFeature.TileId,
                        out var definition) ||
                    !TileFeatureActivationQueries.IsActive(
                        finalTileFeature,
                        definition,
                        finalTopology))
                {
                    continue;
                }

                tileFeatureActiveVisualStates.Add(
                    new TileFeatureActiveVisualState(
                        finalTileFeature.TileId,
                        finalTileFeature.Cell,
                        finalTileFeature.Kind,
                        finalTileFeature.SourceEntityId,
                        finalTileFeature.OwnerEntityId,
                        finalTileFeature.TeamId));
            }
        }

        private static void BuildPlayerActionAttemptPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerActionAttemptPresentationSignal> playerActionAttemptSignals)
        {
            for (var i = 0; i < context.PlayerActionAttemptResolutions.Count; i++)
            {
                var resolution = context.PlayerActionAttemptResolutions[i];
                if (!resolution.HasAttempt || !resolution.EmitsFakePresentation)
                {
                    continue;
                }

                playerActionAttemptSignals.Add(
                    new TickPlayerActionAttemptPresentationSignal(
                        resolution.EntityId,
                        resolution.ActionKind,
                        resolution.Direction,
                        resolution.FeedbackKind,
                        resolution.TargetEntityId,
                        resolution.HasTarget,
                        resolution.EmitsVisualFeedback));
            }
        }

        private static void BuildGravityFieldVisualStates(
            in TickPresentationBuildContext context,
            List<GravityFieldVisualState> gravityFieldVisualStates)
        {
            var finalEntities = new List<EntityState>();
            var emittedEntityIds = new HashSet<int>();
            var lockedTargetIdsByEmitterId = BuildGravityFieldLockedTargetIdsByEmitterId(
                context.GravityFieldLockedTargetFacts);
            context.FinalAuthoritativeSnapshot.EnumerateEntitiesOrdered(finalEntities);
            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (entity.type != EntityType.Box ||
                    entity.boxArchetype != BoxArchetype.GravityField ||
                    entity.boardPresence != EntityBoardPresence.Occupying ||
                    entity.hp <= 0 ||
                    entity.markedForDeath ||
                    !emittedEntityIds.Add(entity.entityId))
                {
                    continue;
                }

                var presentationPhase = entity.position.face == context.FinalAuthoritativeSnapshot.Topology.BottomFace
                    ? entity.gravityFieldPhase
                    : GravityFieldPhase.None;
                var durationTicks = ResolveGravityFieldVisualDurationTicks(context, presentationPhase);
                var areaFootprint = presentationPhase == GravityFieldPhase.Active
                    ? GravityFieldAreaPolicy.BuildFootprint(context.FinalAuthoritativeSnapshot, entity.position)
                    : GravityFieldAreaFootprint.Empty;
                IEnumerable<int> lockedTargetEntityIds = presentationPhase == GravityFieldPhase.Active &&
                                                         lockedTargetIdsByEmitterId.TryGetValue(entity.entityId, out var targetIds)
                    ? targetIds
                    : Array.Empty<int>();
                gravityFieldVisualStates.Add(
                    new GravityFieldVisualState(
                        entity.entityId,
                        entity.position,
                        presentationPhase,
                        Math.Max(0, entity.gravityFieldTimerTicks),
                        durationTicks,
                        CalculateProgress01(entity.gravityFieldTimerTicks, durationTicks),
                        areaFootprint,
                        lockedTargetEntityIds));
            }
        }

        private static Dictionary<int, List<int>> BuildGravityFieldLockedTargetIdsByEmitterId(
            IReadOnlyList<GravityFieldLockedTargetFact> facts)
        {
            var targetIdsByEmitterId = new Dictionary<int, List<int>>();
            for (var i = 0; i < facts.Count; i++)
            {
                var fact = facts[i];
                if (fact.EmitterEntityId <= 0 ||
                    fact.TargetEntityId <= 0)
                {
                    continue;
                }

                if (!targetIdsByEmitterId.TryGetValue(fact.EmitterEntityId, out var targetIds))
                {
                    targetIds = new List<int>();
                    targetIdsByEmitterId.Add(fact.EmitterEntityId, targetIds);
                }

                if (!targetIds.Contains(fact.TargetEntityId))
                {
                    targetIds.Add(fact.TargetEntityId);
                }
            }

            foreach (var pair in targetIdsByEmitterId)
            {
                pair.Value.Sort();
            }

            return targetIdsByEmitterId;
        }

        private static int ResolveGravityFieldVisualDurationTicks(
            in TickPresentationBuildContext context,
            GravityFieldPhase phase)
        {
            switch (phase)
            {
                case GravityFieldPhase.Charging:
                    return context.GravityFieldChargeDurationTicks;
                case GravityFieldPhase.Active:
                    return context.GravityFieldActiveDurationTicks;
                default:
                    return 0;
            }
        }

        private static float CalculateProgress01(int timerTicks, int durationTicks)
        {
            if (durationTicks <= 0)
            {
                return 0f;
            }

            var progress = (durationTicks - Math.Max(0, timerTicks)) / (float)durationTicks;
            if (progress <= 0f)
            {
                return 0f;
            }

            return progress >= 1f ? 1f : progress;
        }

        private static void BuildGravityFieldPresentationEvents(
            in TickPresentationBuildContext context,
            List<GravityFieldPresentationEvent> gravityFieldEvents)
        {
            for (var i = 0; i < context.GravityFieldEvents.Count; i++)
            {
                gravityFieldEvents.Add(context.GravityFieldEvents[i]);
            }
        }

        private static void BuildTilePresentationEvents(
            in TickPresentationBuildContext context,
            in TickTopologyTransitionFact topologyFact,
            List<TilePresentationEvent> tileEvents)
        {
            for (var i = 0; i < context.TileEvents.Count; i++)
            {
                tileEvents.Add(context.TileEvents[i]);
            }

            var moonBlockGeneratorRespawnFacts = context.RespawnPhaseResult.MoonBlockGeneratorRespawnFacts;
            for (var i = 0; i < moonBlockGeneratorRespawnFacts.Count; i++)
            {
                var fact = moonBlockGeneratorRespawnFacts[i];
                tileEvents.Add(
                    new TilePresentationEvent(
                        TilePresentationEventKind.MoonBlockGenerated,
                        fact.GeneratorTileId,
                        fact.Cell,
                        TileFeatureKind.MoonBlockGenerator,
                        fact.SourceEntityId,
                        fact.OwnerEntityId,
                        fact.TeamId,
                        targetEntityId: fact.MoonBlockEntityId,
                        direction: Direction.None,
                        spawnTick: fact.SpawnTick,
                        spawnInteractionLockTicks: fact.SpawnInteractionLockTicks));
            }

            var moonBlockGeneratorBlockedFacts = context.RespawnPhaseResult.MoonBlockGeneratorBlockedFacts;
            for (var i = 0; i < moonBlockGeneratorBlockedFacts.Count; i++)
            {
                var fact = moonBlockGeneratorBlockedFacts[i];
                tileEvents.Add(
                    new TilePresentationEvent(
                        TilePresentationEventKind.MoonBlockGeneratorBlocked,
                        fact.GeneratorTileId,
                        fact.Cell,
                        TileFeatureKind.MoonBlockGenerator,
                        fact.SourceEntityId,
                        fact.OwnerEntityId,
                        fact.TeamId,
                        targetEntityId: fact.BlockingEntityId,
                        direction: Direction.None,
                        moonBlockGeneratorBlockedPayload: fact.Payload));
            }

            var barricadeBlockFacts = context.MovementPhaseResult.BarricadeBlockFacts;
            for (var i = 0; i < barricadeBlockFacts.Count; i++)
            {
                var fact = barricadeBlockFacts[i];
                var sourceEntityId = 0;
                var ownerEntityId = 0;
                var teamId = 0;
                if (context.FinalAuthoritativeSnapshot.TryGetTileFeature(fact.TileId, out var barricade))
                {
                    sourceEntityId = barricade.SourceEntityId;
                    ownerEntityId = barricade.OwnerEntityId;
                    teamId = barricade.TeamId;
                }

                tileEvents.Add(
                    new TilePresentationEvent(
                        TilePresentationEventKind.BarricadeBlocked,
                        fact.TileId,
                        fact.Cell,
                        TileFeatureKind.Barricade,
                        sourceEntityId,
                        ownerEntityId,
                        teamId,
                        targetEntityId: fact.BoxEntityId,
                        direction: fact.AttemptedDirection));
            }

            var finalTileFeatures = new List<TileFeatureState>();
            context.FinalAuthoritativeSnapshot.EnumerateTileFeaturesOrdered(finalTileFeatures);
            AddTileFeatureActivationEvents(context, topologyFact, finalTileFeatures, tileEvents);

            for (var i = 0; i < finalTileFeatures.Count; i++)
            {
                var finalTileFeature = finalTileFeatures[i];
                if (finalTileFeature.Kind != TileFeatureKind.Button ||
                    (finalTileFeature.Flags & TileFeatureFlags.Activated) == 0)
                {
                    continue;
                }

                if (!context.PreMovementSnapshot.TryGetTileFeature(finalTileFeature.TileId, out var preTileFeature) ||
                    preTileFeature.Kind != TileFeatureKind.Button ||
                    (preTileFeature.Flags & TileFeatureFlags.Activated) != 0 ||
                    ContainsTileEvent(
                        tileEvents,
                        TilePresentationEventKind.ButtonActivated,
                        finalTileFeature.TileId))
                {
                    continue;
                }

                tileEvents.Add(
                    new TilePresentationEvent(
                        TilePresentationEventKind.ButtonActivated,
                        finalTileFeature.TileId,
                        finalTileFeature.Cell,
                        finalTileFeature.Kind,
                        finalTileFeature.SourceEntityId,
                        finalTileFeature.OwnerEntityId,
                        finalTileFeature.TeamId,
                        barrierKey: PresentationBarrierKey.ButtonActivated(finalTileFeature.TileId)));
            }

            AddExitOpenedEvents(context, finalTileFeatures, tileEvents);
            AddExitObjectiveClearedEvents(context, finalTileFeatures, tileEvents);
            AddExitEnteredEvents(context, finalTileFeatures, tileEvents);

            tileEvents.Sort(CompareTilePresentationEvents);
        }

        private static void BuildTileFeatureVisualStates(
            in TickPresentationBuildContext context,
            in TickTopologyTransitionFact topologyFact,
            IReadOnlyList<TilePresentationEvent> tileEvents,
            List<TileFeatureVisualState> visualStates)
        {
            var finalTopology = topologyFact.HasTransition
                ? topologyFact.DestinationTopology
                : context.FinalAuthoritativeSnapshot.Topology;
            var finalTileFeatures = new List<TileFeatureState>();
            context.FinalAuthoritativeSnapshot.EnumerateTileFeaturesOrdered(finalTileFeatures);
            for (var i = 0; i < finalTileFeatures.Count; i++)
            {
                var tileFeature = finalTileFeatures[i];
                if (!TryGetTileFeatureDefinition(context.TileFeatureDefinitions, tileFeature.TileId, out var definition))
                {
                    continue;
                }

                if (tileFeature.Kind == TileFeatureKind.Button)
                {
                    if ((tileFeature.Flags & TileFeatureFlags.Activated) == 0)
                    {
                        continue;
                    }

                    var visibilityGate = TryResolveButtonVisibilityGate(
                        tileEvents,
                        tileFeature.TileId,
                        out var gate)
                        ? gate
                        : PresentationVisibilityGate.Immediate();

                    visualStates.Add(new TileFeatureVisualState(
                        tileFeature.TileId,
                        tileFeature.Cell,
                        tileFeature.Kind,
                        true,
                        tileFeature.SourceEntityId,
                        tileFeature.OwnerEntityId,
                        tileFeature.TeamId,
                        visibilityGate));
                    continue;
                }

                if (tileFeature.Kind == TileFeatureKind.Destroy)
                {
                    visualStates.Add(new TileFeatureVisualState(
                        tileFeature.TileId,
                        tileFeature.Cell,
                        tileFeature.Kind,
                        TileFeatureActivationQueries.IsActive(tileFeature, definition, finalTopology),
                        tileFeature.SourceEntityId,
                        tileFeature.OwnerEntityId,
                        tileFeature.TeamId));
                    continue;
                }

                if (tileFeature.Kind == TileFeatureKind.Barricade)
                {
                    if (!TileFeatureActivationQueries.IsActive(tileFeature, definition, finalTopology))
                    {
                        continue;
                    }

                    visualStates.Add(new TileFeatureVisualState(
                        tileFeature.TileId,
                        tileFeature.Cell,
                        tileFeature.Kind,
                        true,
                        tileFeature.SourceEntityId,
                        tileFeature.OwnerEntityId,
                        tileFeature.TeamId));
                    continue;
                }

                if (tileFeature.Kind == TileFeatureKind.Exit)
                {
                    var exitOpen =
                        context.ObjectiveResult != null &&
                        context.ObjectiveResult.HasObjective &&
                        context.ObjectiveResult.RequiredNonPrimaryConditionsSatisfied &&
                        TileFeatureActivationQueries.IsActive(tileFeature, definition, finalTopology);
                    var visibilityGate = exitOpen &&
                                         TryResolveObjectivePrerequisiteVisibilityGate(
                                             context,
                                             tileEvents,
                                             out var gate)
                        ? gate
                        : PresentationVisibilityGate.Immediate();

                    visualStates.Add(new TileFeatureVisualState(
                        tileFeature.TileId,
                        tileFeature.Cell,
                        tileFeature.Kind,
                        exitOpen,
                        tileFeature.SourceEntityId,
                        tileFeature.OwnerEntityId,
                        tileFeature.TeamId,
                        visibilityGate));
                }
            }
        }

        private static bool TryResolveButtonVisibilityGate(
            IReadOnlyList<TilePresentationEvent> tileEvents,
            int tileId,
            out PresentationVisibilityGate visibilityGate)
        {
            visibilityGate = default;
            if (tileEvents == null || tileId <= 0)
            {
                return false;
            }

            for (var i = 0; i < tileEvents.Count; i++)
            {
                var tileEvent = tileEvents[i];
                if (tileEvent.EventKind != TilePresentationEventKind.ButtonActivated ||
                    tileEvent.TileId != tileId ||
                    tileEvent.BarrierKey.Kind != PresentationBarrierKind.ButtonActivated ||
                    tileEvent.TimingAnchor.Kind != PresentationTimingKind.MotionContact ||
                    tileEvent.TimingAnchor.MovementSemanticKind != MovementSemanticKind.Flip)
                {
                    continue;
                }

                visibilityGate = new PresentationVisibilityGate(
                    tileEvent.TimingAnchor,
                    tileEvent.BarrierKey);
                return visibilityGate.HasGate;
            }

            return false;
        }

        private static bool TryResolveObjectivePrerequisiteVisibilityGate(
            in TickPresentationBuildContext context,
            IReadOnlyList<TilePresentationEvent> tileEvents,
            out PresentationVisibilityGate visibilityGate)
        {
            visibilityGate = default;
            if (context.ObjectiveResult == null ||
                !context.ObjectiveResult.RequiredNonPrimaryConditionsSatisfiedThisTick ||
                tileEvents == null ||
                tileEvents.Count == 0)
            {
                return false;
            }

            var statuses = context.ObjectiveResult.ConditionStatuses;
            var bestDelaySeconds = -1f;
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (!status.Required ||
                    status.Role == StageObjectiveConditionRole.PrimaryGoal ||
                    !status.IsSatisfied ||
                    !string.Equals(status.ConditionType, "ButtonActivatedConditionAsset", StringComparison.Ordinal) ||
                    !TryParseButtonTileId(status.Details, out var tileId) ||
                    !TryResolveButtonVisibilityGate(tileEvents, tileId, out var candidateGate))
                {
                    continue;
                }

                var delaySeconds = PresentationTimingResolver.ResolveDelaySeconds(
                    candidateGate.TimingAnchor,
                    GameplayTimingProfile.CreateDefault());
                if (delaySeconds < bestDelaySeconds)
                {
                    continue;
                }

                visibilityGate = candidateGate;
                bestDelaySeconds = delaySeconds;
            }

            return visibilityGate.HasGate;
        }

        private static bool TryParseButtonTileId(string details, out int tileId)
        {
            tileId = 0;
            if (string.IsNullOrWhiteSpace(details))
            {
                return false;
            }

            var parts = details.Split('|');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                const string prefix = "TileId=";
                if (!part.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                return int.TryParse(part.Substring(prefix.Length), out tileId);
            }

            return false;
        }

        private static void BuildTileFeatureVisibleVisualStates(
            in TickPresentationBuildContext context,
            in TickTopologyTransitionFact topologyFact,
            List<TileFeatureVisualState> visualStates)
        {
            var finalTopology = topologyFact.HasTransition
                ? topologyFact.DestinationTopology
                : context.FinalAuthoritativeSnapshot.Topology;
            var finalTileFeatures = new List<TileFeatureState>();
            context.FinalAuthoritativeSnapshot.EnumerateTileFeaturesOrdered(finalTileFeatures);
            for (var i = 0; i < finalTileFeatures.Count; i++)
            {
                var tileFeature = finalTileFeatures[i];
                if (tileFeature.Kind != TileFeatureKind.Button ||
                    !TryGetTileFeatureDefinition(context.TileFeatureDefinitions, tileFeature.TileId, out var definition) ||
                    !TileFeatureActivationQueries.IsActive(tileFeature, definition, finalTopology))
                {
                    continue;
                }

                visualStates.Add(new TileFeatureVisualState(
                    tileFeature.TileId,
                    tileFeature.Cell,
                    tileFeature.Kind,
                    true,
                    tileFeature.SourceEntityId,
                    tileFeature.OwnerEntityId,
                    tileFeature.TeamId));
            }
        }

        private static void AddTileFeatureActivationEvents(
            in TickPresentationBuildContext context,
            in TickTopologyTransitionFact topologyFact,
            IReadOnlyList<TileFeatureState> finalTileFeatures,
            List<TilePresentationEvent> tileEvents)
        {
            var previousTopology = topologyFact.HasTransition
                ? topologyFact.SourceTopology
                : context.PreMovementSnapshot.Topology;
            var finalTopology = topologyFact.HasTransition
                ? topologyFact.DestinationTopology
                : context.FinalAuthoritativeSnapshot.Topology;

            for (var i = 0; i < finalTileFeatures.Count; i++)
            {
                var finalTileFeature = finalTileFeatures[i];
                if (!IsActivationEventKind(finalTileFeature.Kind) ||
                    !TryGetTileFeatureDefinition(
                        context.TileFeatureDefinitions,
                        finalTileFeature.TileId,
                        out var definition))
                {
                    continue;
                }

                var finalActive = TileFeatureActivationQueries.IsActive(
                    finalTileFeature,
                    definition,
                    finalTopology);
                var previousActive =
                    context.PreMovementSnapshot.TryGetTileFeature(finalTileFeature.TileId, out var previousTileFeature) &&
                    previousTileFeature.Kind == finalTileFeature.Kind &&
                    TileFeatureActivationQueries.IsActive(
                        previousTileFeature,
                        definition,
                        previousTopology);

                if (previousActive == finalActive ||
                    !TryResolveActivationEventKind(finalTileFeature.Kind, finalActive, out var eventKind))
                {
                    continue;
                }

                tileEvents.Add(new TilePresentationEvent(
                    eventKind,
                    finalTileFeature.TileId,
                    finalTileFeature.Cell,
                    finalTileFeature.Kind,
                    finalTileFeature.SourceEntityId,
                    finalTileFeature.OwnerEntityId,
                    finalTileFeature.TeamId));
            }
        }

        private static bool IsActivationEventKind(TileFeatureKind kind)
        {
            return kind == TileFeatureKind.Destroy ||
                   kind == TileFeatureKind.Barricade;
        }

        private static bool TryResolveActivationEventKind(
            TileFeatureKind kind,
            bool isActive,
            out TilePresentationEventKind eventKind)
        {
            if (kind == TileFeatureKind.Destroy)
            {
                eventKind = isActive
                    ? TilePresentationEventKind.DestroyTileActivated
                    : TilePresentationEventKind.DestroyTileDeactivated;
                return true;
            }

            if (kind == TileFeatureKind.Barricade)
            {
                eventKind = isActive
                    ? TilePresentationEventKind.BarricadeActivated
                    : TilePresentationEventKind.BarricadeDeactivated;
                return true;
            }

            eventKind = default;
            return false;
        }

        private static void AddExitOpenedEvents(
            in TickPresentationBuildContext context,
            IReadOnlyList<TileFeatureState> finalTileFeatures,
            List<TilePresentationEvent> tileEvents)
        {
            if (!context.ObjectiveResult.HasObjective ||
                !context.ObjectiveResult.RequiredNonPrimaryConditionsSatisfiedThisTick)
            {
                return;
            }

            for (var i = 0; i < finalTileFeatures.Count; i++)
            {
                var exit = finalTileFeatures[i];
                if (!IsActiveExit(context, exit))
                {
                    continue;
                }

                tileEvents.Add(CreateExitTilePresentationEvent(
                    TilePresentationEventKind.ExitOpened,
                    exit,
                    targetEntityId: 0,
                    TryResolveObjectivePrerequisiteVisibilityGate(
                        context,
                        tileEvents,
                        out var visibilityGate)
                        ? visibilityGate
                        : PresentationVisibilityGate.Immediate()));
            }
        }

        private static void AddExitEnteredEvents(
            in TickPresentationBuildContext context,
            IReadOnlyList<TileFeatureState> finalTileFeatures,
            List<TilePresentationEvent> tileEvents)
        {
            if (!TryResolveExitClearPlayerOnActiveExit(
                    context,
                    finalTileFeatures,
                    out var playerEntityId,
                    out var exit))
            {
                return;
            }

            tileEvents.Add(CreateExitTilePresentationEvent(
                TilePresentationEventKind.ExitEntered,
                exit,
                playerEntityId));
        }

        private static void BuildPlayerOutcomePresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerOutcomePresentationSignal> playerOutcomeSignals)
        {
            if (playerOutcomeSignals == null)
            {
                throw new ArgumentNullException(nameof(playerOutcomeSignals));
            }

            var finalTileFeatures = new List<TileFeatureState>();
            context.FinalAuthoritativeSnapshot.EnumerateTileFeaturesOrdered(finalTileFeatures);
            if (!TryResolveExitClearPlayerOnActiveExit(
                    context,
                    finalTileFeatures,
                    out var playerEntityId,
                    out var exit))
            {
                return;
            }

            playerOutcomeSignals.Add(new TickPlayerOutcomePresentationSignal(
                playerEntityId,
                TickPlayerOutcomePresentationKind.StageClearVictory,
                exit.TileId,
                exit.Cell));
        }

        private static void AddExitObjectiveClearedEvents(
            in TickPresentationBuildContext context,
            IReadOnlyList<TileFeatureState> finalTileFeatures,
            List<TilePresentationEvent> tileEvents)
        {
            if (!context.ObjectiveResult.HasObjective ||
                !context.ObjectiveResult.ClearedThisTick)
            {
                return;
            }

            for (var i = 0; i < finalTileFeatures.Count; i++)
            {
                var exit = finalTileFeatures[i];
                if (!IsActiveExit(context, exit))
                {
                    continue;
                }

                tileEvents.Add(CreateExitTilePresentationEvent(
                    TilePresentationEventKind.ExitObjectiveCleared,
                    exit,
                    targetEntityId: 0));
            }
        }

        private static TilePresentationEvent CreateExitTilePresentationEvent(
            TilePresentationEventKind eventKind,
            TileFeatureState exit,
            int targetEntityId,
            PresentationVisibilityGate visibilityGate = default)
        {
            return new TilePresentationEvent(
                eventKind,
                exit.TileId,
                exit.Cell,
                TileFeatureKind.Exit,
                exit.SourceEntityId,
                exit.OwnerEntityId,
                exit.TeamId,
                targetEntityId,
                Direction.None,
                timingAnchor: visibilityGate.TimingAnchor,
                barrierKey: visibilityGate.BarrierKey);
        }

        private static bool IsActiveExit(
            in TickPresentationBuildContext context,
            TileFeatureState tileFeature)
        {
            return tileFeature.Kind == TileFeatureKind.Exit &&
                   TryGetTileFeatureDefinition(
                       context.TileFeatureDefinitions,
                       tileFeature.TileId,
                       out var definition) &&
                   TileFeatureActivationQueries.IsActive(
                       tileFeature,
                       definition,
                       context.FinalAuthoritativeSnapshot.Topology);
        }

        private static bool TryResolveExitClearPlayerOnActiveExit(
            in TickPresentationBuildContext context,
            IReadOnlyList<TileFeatureState> finalTileFeatures,
            out int playerEntityId,
            out TileFeatureState activeExit)
        {
            playerEntityId = context.ObjectiveDefinition.PlayerEntityId;
            activeExit = default;
            if (!context.ObjectiveResult.HasObjective ||
                !context.ObjectiveResult.ClearedThisTick ||
                !context.ObjectiveResult.RequiredNonPrimaryConditionsSatisfied ||
                playerEntityId <= 0 ||
                !context.FinalAuthoritativeSnapshot.TryGetEntity(playerEntityId, out var player) ||
                player.boardPresence != EntityBoardPresence.Occupying ||
                player.hp <= 0 ||
                player.markedForDeath)
            {
                return false;
            }

            for (var i = 0; i < finalTileFeatures.Count; i++)
            {
                var exit = finalTileFeatures[i];
                if (!IsActiveExit(context, exit) ||
                    !exit.Cell.Equals(player.position))
                {
                    continue;
                }

                activeExit = exit;
                return true;
            }

            return false;
        }

        private static bool TryGetTileFeatureDefinition(
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].TileId != tileId)
                {
                    continue;
                }

                definition = definitions[i];
                return true;
            }

            definition = default;
            return false;
        }

        private static bool ContainsTileEvent(
            IReadOnlyList<TilePresentationEvent> tileEvents,
            TilePresentationEventKind eventKind,
            int tileId)
        {
            for (var i = 0; i < tileEvents.Count; i++)
            {
                if (tileEvents[i].EventKind == eventKind &&
                    tileEvents[i].TileId == tileId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareTilePresentationEvents(TilePresentationEvent left, TilePresentationEvent right)
        {
            var cellCompare = CompareSurfaceCells(left.Cell, right.Cell);
            if (cellCompare != 0)
            {
                return cellCompare;
            }

            var tileCompare = left.TileId.CompareTo(right.TileId);
            if (tileCompare != 0)
            {
                return tileCompare;
            }

            var kindCompare = ResolveTilePresentationEventSortPriority(left.EventKind)
                .CompareTo(ResolveTilePresentationEventSortPriority(right.EventKind));
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            var actionPlanCompare = left.TimingAnchor.ActionPlanId.CompareTo(right.TimingAnchor.ActionPlanId);
            if (actionPlanCompare != 0)
            {
                return actionPlanCompare;
            }

            var localActionCompare = left.TimingAnchor.LocalActionIndex.CompareTo(right.TimingAnchor.LocalActionIndex);
            if (localActionCompare != 0)
            {
                return localActionCompare;
            }

            var targetCompare = left.TargetEntityId.CompareTo(right.TargetEntityId);
            if (targetCompare != 0)
            {
                return targetCompare;
            }

            var sourceCompare = left.SourceEntityId.CompareTo(right.SourceEntityId);
            if (sourceCompare != 0)
            {
                return sourceCompare;
            }

            var ownerCompare = left.OwnerEntityId.CompareTo(right.OwnerEntityId);
            if (ownerCompare != 0)
            {
                return ownerCompare;
            }

            var teamCompare = left.TeamId.CompareTo(right.TeamId);
            if (teamCompare != 0)
            {
                return teamCompare;
            }

            var directionCompare = left.Direction.CompareTo(right.Direction);
            if (directionCompare != 0)
            {
                return directionCompare;
            }

            var reasonCompare = left.MoonBlockGeneratorBlockedPayload.Reason
                .CompareTo(right.MoonBlockGeneratorBlockedPayload.Reason);
            if (reasonCompare != 0)
            {
                return reasonCompare;
            }

            return CompareSurfaceCells(
                left.MoonBlockGeneratorBlockedPayload.BlockedCell,
                right.MoonBlockGeneratorBlockedPayload.BlockedCell);
        }

        private static int ResolveTilePresentationEventSortPriority(TilePresentationEventKind eventKind)
        {
            switch (eventKind)
            {
                case TilePresentationEventKind.BarricadeActivated:
                    return 35;
                case TilePresentationEventKind.BarricadeBlocked:
                    return 40;
                case TilePresentationEventKind.BarricadeCrushed:
                    return 50;
                case TilePresentationEventKind.BarricadeDeactivated:
                    return 55;
                default:
                    return (int)eventKind * 10;
            }
        }

        private static int CompareSurfaceCells(SurfaceCell left, SurfaceCell right)
        {
            var faceCompare = left.face.CompareTo(right.face);
            if (faceCompare != 0)
            {
                return faceCompare;
            }

            var xCompare = left.x.CompareTo(right.x);
            return xCompare != 0 ? xCompare : left.y.CompareTo(right.y);
        }

        private static void BuildPlayerDeathHoldPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerDeathHoldPresentationSignal> playerDeathHoldSignals)
        {
            var records = context.RespawnPhaseResult.PlayerRespawnDelayRecords;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (!record.IsActive)
                {
                    continue;
                }

                playerDeathHoldSignals.Add(
                    new TickPlayerDeathHoldPresentationSignal(
                        record.EntityId,
                        record.StartTick,
                        record.EligibleTick,
                        record.RemainingTicks,
                        record.StartedThisTick));
            }
        }

        private static void BuildEnemyUtilityWindupPresentation(
            in TickPresentationBuildContext context,
            List<TickSummonWindupWarningSignal> summonWindupWarnings,
            List<TickFrontFaceShieldWindupWarningSignal> frontFaceShieldWindupWarnings,
            List<TickEnemyUtilityPresentationSignal> enemyUtilitySignals,
            List<TickEnemyUtilityPhasePresentationState> enemyUtilityPhaseStates,
            List<TickEnemyUtilityCooldownPresentationSignal> enemyUtilityCooldownSignals,
            List<TickEnemyGravityFieldAuraVisualState> enemyGravityFieldAuraVisualStates)
        {
            var utilityEntries = new List<EnemyUtilitySnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumerateEnemyUtilityStatesOrdered(utilityEntries);
            for (var i = 0; i < utilityEntries.Count; i++)
            {
                var entry = utilityEntries[i];
                if (!context.FinalAuthoritativeSnapshot.TryGetEntity(entry.EntityId, out var source) ||
                    !EntityRolePolicy.IsEnemyUnit(source))
                {
                    continue;
                }

                for (var effectIndex = 0; effectIndex < entry.State.EffectStates.Count; effectIndex++)
                {
                    var effectState = entry.State.EffectStates[effectIndex];
                    AddEnemyUtilityCanceledPresentationSignal(
                        context,
                        entry.EntityId,
                        effectIndex,
                        effectState,
                        enemyUtilitySignals);
                    AddEnemyUtilityCooldownPresentationSignal(
                        entry.EntityId,
                        effectIndex,
                        effectState,
                        enemyUtilityCooldownSignals);
                    AddEnemyUtilityPhasePresentationState(
                        entry.EntityId,
                        effectIndex,
                        effectState,
                        context.CurrentTickIndex,
                        enemyUtilityPhaseStates);

                    if (effectState.phase == EnemyUtilityEffectPhase.Recover)
                    {
                        if (effectState.effectKind == EnemyUtilityEffectKind.SummonMinion &&
                            context.CurrentTickIndex == effectState.recoverStartTick)
                        {
                            enemyUtilitySignals.Add(
                                new TickEnemyUtilityPresentationSignal(
                                    entry.EntityId,
                                    EnemyUtilityPresentationKind.SummonMinion,
                                    EnemyUtilityPresentationPhase.RecoverStarted,
                                    effectState.recoverStartTick,
                                    effectState.recoverEndTickExclusive,
                                    Math.Max(0, effectState.recoverEndTickExclusive - effectState.recoverStartTick),
                                    effectIndex,
                                    effectState.activationSequence));
                        }
                        else if (effectState.effectKind == EnemyUtilityEffectKind.LockNearbyBoxes &&
                            context.CurrentTickIndex == effectState.recoverStartTick)
                        {
                            enemyUtilitySignals.Add(
                                new TickEnemyUtilityPresentationSignal(
                                    entry.EntityId,
                                    EnemyUtilityPresentationKind.LockNearbyBoxes,
                                    EnemyUtilityPresentationPhase.RecoverStarted,
                                    effectState.recoverStartTick,
                                    effectState.recoverEndTickExclusive,
                                    Math.Max(0, effectState.recoverEndTickExclusive - effectState.recoverStartTick),
                                    effectIndex,
                                    effectState.activationSequence));
                        }
                        else if (effectState.effectKind == EnemyUtilityEffectKind.GravityFieldAura &&
                                 context.CurrentTickIndex == effectState.recoverStartTick)
                        {
                            var recoverDurationTicks = Math.Max(0, effectState.recoverEndTickExclusive - effectState.recoverStartTick);
                            enemyUtilitySignals.Add(
                                new TickEnemyUtilityPresentationSignal(
                                    entry.EntityId,
                                    EnemyUtilityPresentationKind.GravityFieldAura,
                                    EnemyUtilityPresentationPhase.AttackStarted,
                                    effectState.recoverStartTick,
                                    effectState.recoverStartTick,
                                    durationTicks: 0,
                                    effectIndex,
                                    effectState.activationSequence));
                            enemyUtilitySignals.Add(
                                new TickEnemyUtilityPresentationSignal(
                                    entry.EntityId,
                                    EnemyUtilityPresentationKind.GravityFieldAura,
                                    EnemyUtilityPresentationPhase.RecoverStarted,
                                    effectState.recoverStartTick,
                                    effectState.recoverEndTickExclusive,
                                    recoverDurationTicks,
                                    effectIndex,
                                    effectState.activationSequence));
                        }

                        continue;
                    }

                    if (effectState.effectKind == EnemyUtilityEffectKind.GravityFieldAura)
                    {
                        AddEnemyGravityFieldAuraPresentation(
                            context,
                            entry.EntityId,
                            source.position,
                            effectIndex,
                            effectState,
                            enemyUtilitySignals,
                            enemyGravityFieldAuraVisualStates);
                    }

                    if (effectState.phase != EnemyUtilityEffectPhase.Windup)
                    {
                        continue;
                    }

                    if (effectState.effectKind == EnemyUtilityEffectKind.SummonMinion)
                    {
                        summonWindupWarnings.Add(
                            new TickSummonWindupWarningSignal(
                                entry.EntityId,
                                effectIndex,
                                source.position,
                                context.FinalAuthoritativeSnapshot.Topology,
                                source.facing,
                                effectState.windupStartTick,
                                effectState.windupEndTick,
                                effectState.activationSequence,
                                context.CurrentTickIndex,
                                BuildUtilityWarningPresentationSeed(
                                    context.CurrentTickIndex,
                                    entry.EntityId,
                                    effectIndex,
                                    source.position,
                                    effectState.activationSequence)));
                        if (context.CurrentTickIndex == effectState.windupStartTick)
                        {
                            enemyUtilitySignals.Add(
                                new TickEnemyUtilityPresentationSignal(
                                    entry.EntityId,
                                    EnemyUtilityPresentationKind.SummonMinion,
                                    EnemyUtilityPresentationPhase.WindupStarted,
                                    effectState.windupStartTick,
                                    effectState.windupEndTick,
                                    Math.Max(0, effectState.windupEndTick - effectState.windupStartTick),
                                    effectIndex,
                                    effectState.activationSequence));
                        }
                    }
                    else if (effectState.effectKind == EnemyUtilityEffectKind.LockNearbyBoxes &&
                             context.CurrentTickIndex == effectState.windupStartTick)
                    {
                        enemyUtilitySignals.Add(
                            new TickEnemyUtilityPresentationSignal(
                                entry.EntityId,
                                EnemyUtilityPresentationKind.LockNearbyBoxes,
                                EnemyUtilityPresentationPhase.WindupStarted,
                                effectState.windupStartTick,
                                effectState.windupEndTick,
                                Math.Max(0, effectState.windupEndTick - effectState.windupStartTick),
                                effectIndex,
                                effectState.activationSequence));
                    }
                }
            }

            var frontFaceSupportEntries = new List<EnemyFrontFaceSupportSnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumerateEnemyFrontFaceSupportStatesOrdered(frontFaceSupportEntries);
            for (var i = 0; i < frontFaceSupportEntries.Count; i++)
            {
                var entry = frontFaceSupportEntries[i];
                if (!context.FinalAuthoritativeSnapshot.TryGetEntity(entry.EntityId, out var source) ||
                    !EntityRolePolicy.IsEnemyUnit(source))
                {
                    continue;
                }

                for (var effectIndex = 0; effectIndex < entry.State.EffectStates.Count; effectIndex++)
                {
                    var effectState = entry.State.EffectStates[effectIndex];
                    if (effectState.phase != EnemyFrontFaceSupportEffectPhase.Windup)
                    {
                        continue;
                    }

                    frontFaceShieldWindupWarnings.Add(
                        new TickFrontFaceShieldWindupWarningSignal(
                            entry.EntityId,
                            effectIndex,
                            source.position,
                            context.FinalAuthoritativeSnapshot.Topology,
                            effectState.radius,
                            effectState.includeSourceCell,
                            effectState.targetPattern,
                            effectState.windupStartTick,
                            effectState.windupEndTick,
                            effectState.activationSequence,
                            context.CurrentTickIndex,
                            BuildUtilityWarningPresentationSeed(
                                context.CurrentTickIndex,
                                entry.EntityId,
                                effectIndex,
                                source.position,
                                effectState.activationSequence)));
                }
            }

            AddEnemyGravityFieldAuraFieldPresentation(context, enemyGravityFieldAuraVisualStates);
        }

        private static void AddEnemyUtilityCanceledPresentationSignal(
            in TickPresentationBuildContext context,
            int entityId,
            int effectIndex,
            in EnemyUtilityEffectState effectState,
            List<TickEnemyUtilityPresentationSignal> enemyUtilitySignals)
        {
            if (effectState.phase != EnemyUtilityEffectPhase.None ||
                !WasEnemyUtilityCanceledThisTick(context, entityId, effectIndex) ||
                !TryResolveEnemyUtilityPresentationKind(effectState.effectKind, out var presentationKind))
            {
                return;
            }

            enemyUtilitySignals.Add(
                new TickEnemyUtilityPresentationSignal(
                    entityId,
                    presentationKind,
                    EnemyUtilityPresentationPhase.Canceled,
                    context.CurrentTickIndex,
                    context.CurrentTickIndex,
                    durationTicks: 0,
                    effectIndex,
                    effectState.activationSequence));
        }

        private static bool WasEnemyUtilityCanceledThisTick(
            in TickPresentationBuildContext context,
            int entityId,
            int effectIndex)
        {
            var prefix = $"EnemyUtilityWindupCanceled|E={entityId}|Effect={effectIndex}|";
            var updates = context.PreMovementStatePhaseResult.Updates;
            for (var i = 0; i < updates.Count; i++)
            {
                if (updates[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddEnemyUtilityCooldownPresentationSignal(
            int entityId,
            int effectIndex,
            in EnemyUtilityEffectState effectState,
            List<TickEnemyUtilityCooldownPresentationSignal> enemyUtilityCooldownSignals)
        {
            if (effectState.effectKind == EnemyUtilityEffectKind.SummonMinion)
            {
                return;
            }

            if (!TryResolveEnemyUtilityPresentationKind(effectState.effectKind, out var presentationKind) ||
                effectState.phase == EnemyUtilityEffectPhase.Windup ||
                effectState.phase == EnemyUtilityEffectPhase.Active ||
                effectState.cooldownTicksRemaining <= 0)
            {
                return;
            }

            enemyUtilityCooldownSignals.Add(
                new TickEnemyUtilityCooldownPresentationSignal(
                    entityId,
                    presentationKind,
                    effectState.cooldownTicksRemaining,
                    effectState.cooldownTicksRemaining,
                    effectIndex,
                    effectState.activationSequence));
        }

        private static void AddEnemyUtilityPhasePresentationState(
            int entityId,
            int effectIndex,
            in EnemyUtilityEffectState effectState,
            int tickIndex,
            List<TickEnemyUtilityPhasePresentationState> enemyUtilityPhaseStates)
        {
            if (effectState.phase == EnemyUtilityEffectPhase.None ||
                !TryResolveEnemyUtilityPresentationKind(effectState.effectKind, out var presentationKind))
            {
                return;
            }

            var phaseStartTick = 0;
            var phaseEndTickExclusive = 0;
            switch (effectState.phase)
            {
                case EnemyUtilityEffectPhase.Windup:
                    phaseStartTick = effectState.windupStartTick;
                    phaseEndTickExclusive = effectState.windupEndTick;
                    break;

                case EnemyUtilityEffectPhase.Active:
                    phaseStartTick = effectState.activeStartTick;
                    phaseEndTickExclusive = effectState.activeEndTickExclusive;
                    break;

                case EnemyUtilityEffectPhase.Recover:
                    phaseStartTick = effectState.recoverStartTick;
                    phaseEndTickExclusive = effectState.recoverEndTickExclusive;
                    break;

                default:
                    return;
            }

            var durationTicks = Math.Max(0, phaseEndTickExclusive - phaseStartTick);
            var elapsedTicks = durationTicks > 0
                ? Math.Min(durationTicks, Math.Max(0, tickIndex - phaseStartTick))
                : 0;
            enemyUtilityPhaseStates.Add(
                new TickEnemyUtilityPhasePresentationState(
                    entityId,
                    presentationKind,
                    effectState.phase,
                    elapsedTicks,
                    durationTicks,
                    effectIndex,
                    effectState.activationSequence));
        }

        private static void AddEnemyGravityFieldAuraPresentation(
            in TickPresentationBuildContext context,
            int entityId,
            SurfaceCell sourceCell,
            int effectIndex,
            in EnemyUtilityEffectState effectState,
            List<TickEnemyUtilityPresentationSignal> enemyUtilitySignals,
            List<TickEnemyGravityFieldAuraVisualState> enemyGravityFieldAuraVisualStates)
        {
            if (effectState.phase == EnemyUtilityEffectPhase.Windup)
            {
                var durationTicks = Math.Max(0, effectState.windupEndTick - effectState.windupStartTick);
                var timerTicks = Math.Max(0, effectState.windupEndTick - context.CurrentTickIndex);
                enemyGravityFieldAuraVisualStates.Add(
                    new TickEnemyGravityFieldAuraVisualState(
                        entityId,
                        sourceCell,
                        effectState.phase,
                        radius: 1,
                        timerTicks,
                        durationTicks,
                        CalculateProgress01(timerTicks, durationTicks),
                        effectIndex,
                        effectState.activationSequence,
                        BuildEnemyGravityFieldAuraFootprint(context, sourceCell, radius: 1),
                        startedThisTick: context.CurrentTickIndex == effectState.windupStartTick));

                if (context.CurrentTickIndex == effectState.windupStartTick)
                {
                    enemyUtilitySignals.Add(
                        new TickEnemyUtilityPresentationSignal(
                            entityId,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            EnemyUtilityPresentationPhase.WindupStarted,
                            effectState.windupStartTick,
                            effectState.windupEndTick,
                            durationTicks,
                            effectIndex,
                            effectState.activationSequence));
                }

                return;
            }

        }

        private static void AddEnemyGravityFieldAuraFieldPresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyGravityFieldAuraVisualState> enemyGravityFieldAuraVisualStates)
        {
            var fieldEntries = new List<EnemyGravityFieldAuraFieldSnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumerateEnemyGravityFieldAuraFieldStatesOrdered(fieldEntries);
            for (var i = 0; i < fieldEntries.Count; i++)
            {
                var field = fieldEntries[i].State;
                if (!field.IsActive(context.CurrentTickIndex))
                {
                    continue;
                }

                var activeDurationTicks = Math.Max(0, field.ExpiresTickExclusive - field.StartedTick);
                var activeTimerTicks = Math.Max(0, field.ExpiresTickExclusive - context.CurrentTickIndex);
                var startedThisTick = context.CurrentTickIndex == field.StartedTick;
                enemyGravityFieldAuraVisualStates.Add(
                    new TickEnemyGravityFieldAuraVisualState(
                        field.SourceEntityId,
                        field.OriginCell,
                        EnemyUtilityEffectPhase.Active,
                        field.Radius,
                        activeTimerTicks,
                        activeDurationTicks,
                        CalculateProgress01(activeTimerTicks, activeDurationTicks),
                        field.SourceEffectIndex,
                        field.ActivationSequence,
                        BuildEnemyGravityFieldAuraFootprint(context, field.OriginCell, field.Radius),
                        startedThisTick));
            }
        }

        private static GravityFieldAreaFootprint BuildEnemyGravityFieldAuraFootprint(
            in TickPresentationBuildContext context,
            SurfaceCell sourceCell,
            int radius)
        {
            return radius == 1
                ? GravityFieldAreaPolicy.BuildFootprint(context.FinalAuthoritativeSnapshot, sourceCell)
                : GravityFieldAreaFootprint.Empty;
        }

        private static bool TryResolveEnemyUtilityPresentationKind(
            EnemyUtilityEffectKind effectKind,
            out EnemyUtilityPresentationKind presentationKind)
        {
            switch (effectKind)
            {
                case EnemyUtilityEffectKind.LockNearbyBoxes:
                    presentationKind = EnemyUtilityPresentationKind.LockNearbyBoxes;
                    return true;
                case EnemyUtilityEffectKind.GravityFieldAura:
                    presentationKind = EnemyUtilityPresentationKind.GravityFieldAura;
                    return true;
                case EnemyUtilityEffectKind.SummonMinion:
                    presentationKind = EnemyUtilityPresentationKind.SummonMinion;
                    return true;
                default:
                    presentationKind = EnemyUtilityPresentationKind.None;
                    return false;
            }
        }

        private static int BuildUtilityWarningPresentationSeed(
            int tickIndex,
            int sourceEntityId,
            int effectIndex,
            SurfaceCell sourceCell,
            int activationSequence)
        {
            unchecked
            {
                var seed = 17;
                seed = (seed * 31) + tickIndex;
                seed = (seed * 31) + sourceEntityId;
                seed = (seed * 31) + effectIndex;
                seed = (seed * 31) + (int)sourceCell.face;
                seed = (seed * 31) + sourceCell.x;
                seed = (seed * 31) + sourceCell.y;
                seed = (seed * 31) + activationSequence;
                return seed;
            }
        }

        private static void BuildFrontFaceShieldPresentation(
            in TickPresentationBuildContext context,
            List<TickFrontFaceShieldSourceSignal> sourceSignals,
            List<TickFrontFaceShieldBlockSignal> blockSignals)
        {
            var sourceExports = context.MovementPhaseResult.FrontFaceShieldSourceExports;
            for (var i = 0; i < sourceExports.Count; i++)
            {
                var export = sourceExports[i];
                sourceSignals.Add(
                    new TickFrontFaceShieldSourceSignal(
                        export.SourceEntityId,
                        export.SourceCell,
                        export.Topology,
                        export.Radius,
                        export.IncludeSourceCell,
                        export.TargetPattern,
                        export.TickIndex,
                        export.PresentationSeed));
            }

            var blockExports = context.MovementPhaseResult.FrontFaceShieldBlockExports;
            for (var i = 0; i < blockExports.Count; i++)
            {
                var export = blockExports[i];
                blockSignals.Add(
                    new TickFrontFaceShieldBlockSignal(
                        export.ShieldSourceEntityId,
                        export.BoxEntityId,
                        export.ActorEntityId,
                        export.BlockedCell,
                        export.ShieldSourceCell,
                        export.MovementKind,
                        export.Topology,
                        export.TickIndex,
                        export.PresentationSeed));
            }
        }

        private static void BuildMovementPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityMotion> entityMotions,
            List<TickVisibilityChange> visibilityChanges,
            ISet<int> exitOwnedEntityIds)
        {
            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MoveEntity)
                {
                    AppendEntityMotion(context, operation, entityMotions);
                    continue;
                }

                if (operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                    operation.BoardPresence == EntityBoardPresence.Detached &&
                    !exitOwnedEntityIds.Contains(operation.EntityId) &&
                    context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity))
                {
                    visibilityChanges.Add(
                        new TickVisibilityChange(
                            operation.EntityId,
                            TickVisibilityChangeKind.Detach,
                            sourceEntity.position,
                            context.PreMovementSnapshot.Topology,
                            sourceEntity.facing));
                }
            }
        }

        private static void BuildKinematicMotionPresentation(
            in TickPresentationBuildContext context,
            List<TickKinematicMotionTrack> kinematicMotionTracks)
        {
            var terminalEntityIds = new HashSet<int>();
            for (var i = 0; i < context.CleanupPhaseResult.RemovedUnitKinematicPoses.Count; i++)
            {
                var record = context.CleanupPhaseResult.RemovedUnitKinematicPoses[i];
                terminalEntityIds.Add(record.EntityId);
            }

            var interruptedEntityIds = new List<int>();
            var seenInterruptedEntityIds = new HashSet<int>();
            for (var i = 0; i < context.AttackPhaseResult.MotionInterruptRecords.Count; i++)
            {
                var record = context.AttackPhaseResult.MotionInterruptRecords[i];
                if (terminalEntityIds.Contains(record.EntityId) ||
                    !seenInterruptedEntityIds.Add(record.EntityId))
                {
                    continue;
                }

                interruptedEntityIds.Add(record.EntityId);
                terminalEntityIds.Add(record.EntityId);
            }

            var operations = context.MovementPhaseResult.ResolvedOperations;
            var movementKinematicEntityIds = new HashSet<int>();
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.SetUnitKinematicState ||
                    terminalEntityIds.Contains(operation.EntityId) ||
                    !context.PreMovementSnapshot.TryGetUnitKinematicPose(operation.EntityId, out var sourcePose) ||
                    !context.PostMovementSnapshot.TryGetUnitKinematicPose(operation.EntityId, out var destinationPose) ||
                    !context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity) ||
                    !context.PostMovementSnapshot.TryGetEntity(operation.EntityId, out var destinationEntity))
                {
                    continue;
                }

                kinematicMotionTracks.Add(
                    new TickKinematicMotionTrack(
                        operation.EntityId,
                        sourcePose.AnchorCell,
                        sourcePose.LocalOffset,
                        destinationPose.AnchorCell,
                        destinationPose.LocalOffset,
                        operation.UnitKinematicState.mode,
                        operation.UnitKinematicState.forcedOp,
                        destinationEntity.type,
                        context.PreMovementSnapshot.Topology,
                        context.PostMovementSnapshot.Topology,
                        sourceEntity.facing,
                        destinationEntity.facing,
                        startedTick: destinationPose.State.startedTick,
                        elapsedTicks: destinationPose.State.elapsedTicks,
                        totalTicks: destinationPose.State.totalTicks));
                movementKinematicEntityIds.Add(operation.EntityId);
            }

            var finalKinematicEntries = new List<UnitKinematicSnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumerateUnitKinematicStatesOrdered(finalKinematicEntries);
            for (var i = 0; i < finalKinematicEntries.Count; i++)
            {
                var entry = finalKinematicEntries[i];
                if (movementKinematicEntityIds.Contains(entry.EntityId) ||
                    terminalEntityIds.Contains(entry.EntityId) ||
                    entry.State.mode != MotionMode.Held ||
                    !context.FinalAuthoritativeSnapshot.TryGetUnitKinematicPose(entry.EntityId, out var heldPose) ||
                    !context.FinalAuthoritativeSnapshot.TryGetEntity(entry.EntityId, out var heldEntity))
                {
                    continue;
                }

                kinematicMotionTracks.Add(
                    new TickKinematicMotionTrack(
                        entry.EntityId,
                        heldPose.AnchorCell,
                        heldPose.LocalOffset,
                        heldPose.AnchorCell,
                        heldPose.LocalOffset,
                        heldPose.Mode,
                        heldPose.State.forcedOp,
                        heldEntity.type,
                        context.FinalAuthoritativeSnapshot.Topology,
                        context.FinalAuthoritativeSnapshot.Topology,
                        heldEntity.facing,
                        heldEntity.facing,
                        startedTick: heldPose.State.startedTick,
                        elapsedTicks: heldPose.State.elapsedTicks,
                        totalTicks: heldPose.State.totalTicks));
            }

            for (var i = 0; i < interruptedEntityIds.Count; i++)
            {
                var entityId = interruptedEntityIds[i];
                if (!context.PreMovementSnapshot.TryGetUnitKinematicPose(entityId, out var sourcePose) ||
                    !context.PostAttackSnapshot.TryGetUnitKinematicPose(entityId, out var destinationPose) ||
                    !context.PreMovementSnapshot.TryGetEntity(entityId, out var sourceEntity) ||
                    !context.PostAttackSnapshot.TryGetEntity(entityId, out var destinationEntity))
                {
                    continue;
                }

                kinematicMotionTracks.Add(
                    new TickKinematicMotionTrack(
                        entityId,
                        sourcePose.AnchorCell,
                        sourcePose.LocalOffset,
                        destinationPose.AnchorCell,
                        destinationPose.LocalOffset,
                        destinationPose.Mode,
                        destinationPose.State.forcedOp,
                        destinationEntity.type,
                        context.PreMovementSnapshot.Topology,
                        context.PostAttackSnapshot.Topology,
                        sourceEntity.facing,
                        destinationEntity.facing,
                        TickKinematicMotionTerminalKind.Interrupted,
                        startedTick: destinationPose.State.startedTick,
                        elapsedTicks: destinationPose.State.elapsedTicks,
                        totalTicks: destinationPose.State.totalTicks));
            }

            for (var i = 0; i < context.CleanupPhaseResult.RemovedUnitKinematicPoses.Count; i++)
            {
                var record = context.CleanupPhaseResult.RemovedUnitKinematicPoses[i];
                var entityId = record.EntityId;
                var removedPose = record.Pose;
                if (!context.PreMovementSnapshot.TryGetUnitKinematicPose(entityId, out var sourcePose) ||
                    !context.PreMovementSnapshot.TryGetEntity(entityId, out var sourceEntity))
                {
                    continue;
                }

                var destinationEntity = context.PostAttackSnapshot.TryGetEntity(entityId, out var postAttackEntity)
                    ? postAttackEntity
                    : sourceEntity;
                kinematicMotionTracks.Add(
                    new TickKinematicMotionTrack(
                        entityId,
                        sourcePose.AnchorCell,
                        sourcePose.LocalOffset,
                        removedPose.AnchorCell,
                        removedPose.LocalOffset,
                        removedPose.Mode,
                        removedPose.State.forcedOp,
                        destinationEntity.type,
                        context.PreMovementSnapshot.Topology,
                        context.PostAttackSnapshot.Topology,
                        sourceEntity.facing,
                        destinationEntity.facing,
                        TickKinematicMotionTerminalKind.Removed,
                        startedTick: removedPose.State.startedTick,
                        elapsedTicks: removedPose.State.elapsedTicks,
                        totalTicks: removedPose.State.totalTicks));
            }
        }

        private static void BuildContinuousLocomotionPresentation(
            in TickPresentationBuildContext context,
            List<TickContinuousLocomotionTrack> continuousLocomotionTracks)
        {
            var terminalEntityIds = new HashSet<int>();
            for (var i = 0; i < context.CleanupPhaseResult.RemovedUnitContinuousLocomotionPoses.Count; i++)
            {
                terminalEntityIds.Add(context.CleanupPhaseResult.RemovedUnitContinuousLocomotionPoses[i].EntityId);
            }

            var interruptedEntityIds = new List<int>();
            var seenInterruptedEntityIds = new HashSet<int>();
            for (var i = 0; i < context.AttackPhaseResult.MotionInterruptRecords.Count; i++)
            {
                var record = context.AttackPhaseResult.MotionInterruptRecords[i];
                if (terminalEntityIds.Contains(record.EntityId) ||
                    !seenInterruptedEntityIds.Add(record.EntityId))
                {
                    continue;
                }

                interruptedEntityIds.Add(record.EntityId);
                terminalEntityIds.Add(record.EntityId);
            }

            var topologyMaterializedEntityIds = BuildTopologyMaterializedEntityIdSet(context.MovementPhaseResult);
            var continuousTrackEntityIds = new HashSet<int>();
            var entries = new List<UnitContinuousLocomotionSnapshotEntry>();
            context.PostMovementSnapshot.EnumerateUnitContinuousLocomotionStatesOrdered(entries);
            for (var i = 0; i < entries.Count; i++)
            {
                var entityId = entries[i].EntityId;
                if (terminalEntityIds.Contains(entityId) ||
                    topologyMaterializedEntityIds.Contains(entityId) ||
                    !context.PreMovementSnapshot.TryGetUnitContinuousLocomotionPose(entityId, out var sourcePose) ||
                    !context.PostMovementSnapshot.TryGetUnitContinuousLocomotionPose(entityId, out var destinationPose) ||
                    !context.PreMovementSnapshot.TryGetEntity(entityId, out var sourceEntity) ||
                    !context.PostMovementSnapshot.TryGetEntity(entityId, out var destinationEntity))
                {
                    continue;
                }

                if (!sourcePose.HasAuthoritativeState &&
                    !destinationPose.HasAuthoritativeState &&
                    destinationPose.Mode != ContinuousLocomotionMode.Moving)
                {
                    continue;
                }

                var hasTopologyTransitionMetadata = TryResolveFree2DTopologyTransitionMetadata(
                    context.MovementPhaseResult,
                    entityId,
                    out var topologyTransitionMetadata);
                var sourceTopology = hasTopologyTransitionMetadata
                    ? ResolveFree2DTopologyTransitionSourceTopology(
                        context.PostMovementSnapshot.Topology,
                        topologyTransitionMetadata.RotationKind)
                    : (CubeTopologyState?)null;
                continuousLocomotionTracks.Add(
                    new TickContinuousLocomotionTrack(
                        entityId,
                        sourcePose.AnchorCell,
                        sourcePose.LocalOffset,
                        destinationPose.AnchorCell,
                        destinationPose.LocalOffset,
                        sourceEntity.facing,
                        destinationEntity.facing,
                        sourcePose.Mode == ContinuousLocomotionMode.AlignToAnchor
                            ? ContinuousLocomotionMode.AlignToAnchor
                            : destinationPose.Mode,
                        TickKinematicMotionTerminalKind.None,
                        hasTopologyTransitionMetadata
                            ? sourceTopology
                            : null,
                        hasTopologyTransitionMetadata
                            ? context.PostMovementSnapshot.Topology
                            : null,
                        hasTopologyTransitionMetadata
                            ? topologyTransitionMetadata.RotationKind
                            : CubeRotationKind.None,
                        hasTopologyTransitionMetadata
                            ? topologyTransitionMetadata.BoundaryReason
                            : null));
                continuousTrackEntityIds.Add(entityId);
            }

            entries.Clear();
            context.PreMovementSnapshot.EnumerateUnitContinuousLocomotionStatesOrdered(entries);
            for (var i = 0; i < entries.Count; i++)
            {
                var entityId = entries[i].EntityId;
                if (terminalEntityIds.Contains(entityId) ||
                    topologyMaterializedEntityIds.Contains(entityId) ||
                    continuousTrackEntityIds.Contains(entityId) ||
                    !context.PreMovementSnapshot.TryGetUnitContinuousLocomotionPose(entityId, out var sourcePose) ||
                    !context.PostMovementSnapshot.TryGetUnitContinuousLocomotionPose(entityId, out var destinationPose) ||
                    !sourcePose.HasAuthoritativeState ||
                    destinationPose.HasAuthoritativeState ||
                    sourcePose.LocalOffset.Equals(destinationPose.LocalOffset) ||
                    !context.PreMovementSnapshot.TryGetEntity(entityId, out var sourceEntity) ||
                    !context.PostMovementSnapshot.TryGetEntity(entityId, out var destinationEntity))
                {
                    continue;
                }

                var hasTopologyTransitionMetadata = TryResolveFree2DTopologyTransitionMetadata(
                    context.MovementPhaseResult,
                    entityId,
                    out var topologyTransitionMetadata);
                var sourceTopology = hasTopologyTransitionMetadata
                    ? ResolveFree2DTopologyTransitionSourceTopology(
                        context.PostMovementSnapshot.Topology,
                        topologyTransitionMetadata.RotationKind)
                    : (CubeTopologyState?)null;
                continuousLocomotionTracks.Add(
                    new TickContinuousLocomotionTrack(
                        entityId,
                        sourcePose.AnchorCell,
                        sourcePose.LocalOffset,
                        destinationPose.AnchorCell,
                        destinationPose.LocalOffset,
                        sourceEntity.facing,
                        destinationEntity.facing,
                        destinationPose.Mode,
                        TickKinematicMotionTerminalKind.None,
                        hasTopologyTransitionMetadata
                            ? sourceTopology
                            : null,
                        hasTopologyTransitionMetadata
                            ? context.PostMovementSnapshot.Topology
                            : null,
                        hasTopologyTransitionMetadata
                            ? topologyTransitionMetadata.RotationKind
                            : CubeRotationKind.None,
                        hasTopologyTransitionMetadata
                            ? topologyTransitionMetadata.BoundaryReason
                            : null));
                continuousTrackEntityIds.Add(entityId);
            }

            for (var i = 0; i < interruptedEntityIds.Count; i++)
            {
                var entityId = interruptedEntityIds[i];
                if (!context.PreMovementSnapshot.TryGetUnitContinuousLocomotionPose(entityId, out var sourcePose) ||
                    !context.PostAttackSnapshot.TryGetUnitContinuousLocomotionPose(entityId, out var destinationPose) ||
                    !sourcePose.HasAuthoritativeState ||
                    !context.PreMovementSnapshot.TryGetEntity(entityId, out var sourceEntity) ||
                    !context.PostAttackSnapshot.TryGetEntity(entityId, out var destinationEntity))
                {
                    continue;
                }

                continuousLocomotionTracks.Add(
                    new TickContinuousLocomotionTrack(
                        entityId,
                        sourcePose.AnchorCell,
                        sourcePose.LocalOffset,
                        destinationPose.AnchorCell,
                        destinationPose.LocalOffset,
                        sourceEntity.facing,
                        destinationEntity.facing,
                        destinationPose.Mode,
                        TickKinematicMotionTerminalKind.Interrupted));
            }

            for (var i = 0; i < context.CleanupPhaseResult.RemovedUnitContinuousLocomotionPoses.Count; i++)
            {
                var record = context.CleanupPhaseResult.RemovedUnitContinuousLocomotionPoses[i];
                if (!context.PreMovementSnapshot.TryGetUnitContinuousLocomotionPose(record.EntityId, out var sourcePose) ||
                    !context.PreMovementSnapshot.TryGetEntity(record.EntityId, out var sourceEntity))
                {
                    continue;
                }

                var destinationEntity = context.PostAttackSnapshot.TryGetEntity(record.EntityId, out var postAttackEntity)
                    ? postAttackEntity
                    : sourceEntity;
                continuousLocomotionTracks.Add(
                    new TickContinuousLocomotionTrack(
                        record.EntityId,
                        sourcePose.AnchorCell,
                        sourcePose.LocalOffset,
                        record.Pose.AnchorCell,
                        record.Pose.LocalOffset,
                        sourceEntity.facing,
                        destinationEntity.facing,
                        record.Pose.Mode,
                        TickKinematicMotionTerminalKind.Removed));
            }
        }

        private static HashSet<int> BuildTopologyMaterializedEntityIdSet(MovementPhaseResult movementPhaseResult)
        {
            var entityIds = new HashSet<int>();
            var operations = movementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization &&
                    operation.EntityId != 0)
                {
                    entityIds.Add(operation.EntityId);
                }
            }

            return entityIds;
        }

        private static bool TryResolveFree2DTopologyTransitionMetadata(
            MovementPhaseResult movementPhaseResult,
            int entityId,
            out FinalizationOperationMetadata metadata)
        {
            var operations = movementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition)
                {
                    metadata = operation.Metadata;
                    return true;
                }
            }

            metadata = default;
            return false;
        }

        private static CubeTopologyState ResolveFree2DTopologyTransitionSourceTopology(
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind)
        {
            return rotationKind switch
            {
                CubeRotationKind.Forward => destinationTopology.Rotate(CubeRotationKind.Backward),
                CubeRotationKind.Backward => destinationTopology.Rotate(CubeRotationKind.Forward),
                _ => destinationTopology,
            };
        }

        private static void BuildFlipImpactPresentation(
            in TickPresentationBuildContext context,
            List<FlipImpactPresentationSignal> flipImpactSignals)
        {
            var dispositionRecords = context.MovementPhaseResult.ImpactDispositionRecords;
            var signaledKeys = new HashSet<long>();

            for (var i = 0; i < dispositionRecords.Count; i++)
            {
                var record = dispositionRecords[i];
                if (record.PolicyKind != ImpactDispositionPolicyKind.Flip ||
                    !TryResolveFlipImpactDisposition(record.DispositionKind, out var disposition) ||
                    !context.PreMovementSnapshot.TryGetEntity(record.ImpactSourceEntityId, out var sourceEntity) ||
                    sourceEntity.boardPresence != EntityBoardPresence.Occupying ||
                    sourceEntity.position == record.ImpactCell ||
                    !ImpactGeometryResolver.TryResolve(sourceEntity.position, record.ImpactCell, out var geometry) ||
                    !geometry.IsFlipImpact)
                {
                    continue;
                }

                var key = BuildFlipImpactSignalKey(record.ActionPlanId, record.ImpactSourceEntityId);
                if (!signaledKeys.Add(key))
                {
                    continue;
                }

                flipImpactSignals.Add(
                    new FlipImpactPresentationSignal(
                        record.ActionPlanId,
                        record.ImpactSourceEntityId,
                        record.ImpactTargetEntityId != 0 ? record.ImpactTargetEntityId : -1,
                        ResolveActionActorEntityId(context, record.ActionPlanId),
                        sourceEntity.position,
                        record.ImpactCell,
                        context.PreMovementSnapshot.Topology,
                        sourceEntity.facing,
                        geometry.MoveFacing,
                        disposition,
                        hasLandingCell: false));
            }
        }

        private static void BuildFlipFloorImpactPresentation(
            in TickPresentationBuildContext context,
            List<FlipFloorImpactPresentationSignal> flipFloorImpactSignals)
        {
            var signaledKeys = new HashSet<long>();
            BuildFlipFloorImpactPresentationFromDueContact(context, flipFloorImpactSignals, signaledKeys);
            BuildFlipFloorImpactPresentationFromDisposition(context, flipFloorImpactSignals, signaledKeys);
            BuildFlipFloorImpactPresentationFromMovement(context, flipFloorImpactSignals, signaledKeys);
        }

        private static void BuildFlipFloorImpactPresentationFromDueContact(
            in TickPresentationBuildContext context,
            List<FlipFloorImpactPresentationSignal> flipFloorImpactSignals,
            HashSet<long> signaledKeys)
        {
            var signals = context.DueFlipContactSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.BoxEntityId <= 0 ||
                    signal.BoxDisposition == FlipBoxDisposition.Cancelled ||
                    !TryResolveFlipDueFloorImpactKind(signal.BoxDisposition, out var kind))
                {
                    continue;
                }

                var key = BuildFlipImpactSignalKey(signal.SourceActionPlanId, signal.BoxEntityId);
                if (!signaledKeys.Add(key))
                {
                    continue;
                }

                flipFloorImpactSignals.Add(
                    new FlipFloorImpactPresentationSignal(
                        signal.SourceActionPlanId,
                        signal.BoxEntityId,
                        signal.ActorEntityId,
                        signal.SourceCell,
                        signal.ContactCell,
                        signal.Topology,
                        signal.SourceFacing,
                        signal.ContactFacing,
                        kind,
                        visualContactNormalizedTime: 0f,
                        timingMode: GameplayPresentationTimingMode.DueContactImmediate));
            }
        }

        private static bool TryResolveFlipDueFloorImpactKind(
            FlipBoxDisposition disposition,
            out FlipFloorImpactPresentationKind kind)
        {
            switch (disposition)
            {
                case FlipBoxDisposition.MaterializeAtLanding:
                    kind = FlipFloorImpactPresentationKind.FollowThrough;
                    return true;

                case FlipBoxDisposition.MaterializeAtSource:
                case FlipBoxDisposition.StayAtContact:
                    kind = FlipFloorImpactPresentationKind.Stay;
                    return true;

                case FlipBoxDisposition.DestroySelf:
                    kind = FlipFloorImpactPresentationKind.DestroySelf;
                    return true;

                default:
                    kind = default;
                    return false;
            }
        }

        private static void BuildFlipFloorImpactPresentationFromDisposition(
            in TickPresentationBuildContext context,
            List<FlipFloorImpactPresentationSignal> flipFloorImpactSignals,
            HashSet<long> signaledKeys)
        {
            var dispositionRecords = context.MovementPhaseResult.ImpactDispositionRecords;
            for (var i = 0; i < dispositionRecords.Count; i++)
            {
                var record = dispositionRecords[i];
                if (record.PolicyKind != ImpactDispositionPolicyKind.Flip ||
                    !TryResolveFlipFloorImpactKind(record.DispositionKind, out var kind) ||
                    !context.PreMovementSnapshot.TryGetEntity(record.ImpactSourceEntityId, out var sourceEntity) ||
                    sourceEntity.boardPresence != EntityBoardPresence.Occupying ||
                    sourceEntity.position == record.ImpactCell ||
                    !ImpactGeometryResolver.TryResolve(sourceEntity.position, record.ImpactCell, out var geometry) ||
                    !geometry.IsFlipImpact)
                {
                    continue;
                }

                var key = BuildFlipImpactSignalKey(record.ActionPlanId, record.ImpactSourceEntityId);
                if (!signaledKeys.Add(key))
                {
                    continue;
                }

                flipFloorImpactSignals.Add(
                    new FlipFloorImpactPresentationSignal(
                        record.ActionPlanId,
                        record.ImpactSourceEntityId,
                        ResolveActionActorEntityId(context, record.ActionPlanId),
                        sourceEntity.position,
                        record.ImpactCell,
                        context.PreMovementSnapshot.Topology,
                        sourceEntity.facing,
                        geometry.MoveFacing,
                        kind));
            }
        }

        private static void BuildFlipFloorImpactPresentationFromMovement(
            in TickPresentationBuildContext context,
            List<FlipFloorImpactPresentationSignal> flipFloorImpactSignals,
            HashSet<long> signaledKeys)
        {
            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.MoveEntity ||
                    operation.Metadata.MovementSemanticKind != MovementSemanticKind.Flip ||
                    !context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity) ||
                    !context.PostMovementSnapshot.TryGetEntity(operation.EntityId, out var destinationEntity) ||
                    destinationEntity.boardPresence != EntityBoardPresence.Occupying ||
                    sourceEntity.position == operation.Destination)
                {
                    continue;
                }

                var key = BuildFlipImpactSignalKey(operation.Metadata.ActionPlanId, operation.EntityId);
                if (!signaledKeys.Add(key))
                {
                    continue;
                }

                flipFloorImpactSignals.Add(
                    new FlipFloorImpactPresentationSignal(
                        operation.Metadata.ActionPlanId,
                        operation.EntityId,
                        operation.Metadata.SourceActorEntityId,
                        sourceEntity.position,
                        operation.Destination,
                        context.PostMovementSnapshot.Topology,
                        sourceEntity.facing,
                        destinationEntity.facing,
                        FlipFloorImpactPresentationKind.Landing));
            }
        }

        private static void BuildImpactTransientPresentation(
            in TickPresentationBuildContext context,
            List<TickImpactTransientPresentationSignal> impactTransientSignals)
        {
            impactTransientSignals.Clear();
        }

        private static void BuildAttackPresentation(
            in TickPresentationBuildContext context,
            List<TickVisibilityChange> visibilityChanges)
        {
            var operations = context.AttackPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.SpawnEntity)
                {
                    continue;
                }

                var spawnedEntity = operation.SpawnedEntity;
                visibilityChanges.Add(
                    new TickVisibilityChange(
                        spawnedEntity.entityId,
                        TickVisibilityChangeKind.Spawn,
                        spawnedEntity.position,
                        context.PostAttackSnapshot.Topology,
                        spawnedEntity.facing));
            }
        }

        private static void BuildEntityExitPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityExitPresentationSignal> entityExitSignals,
            ISet<int> exitOwnedEntityIds)
        {
            BuildDueContactEntityExitPresentation(context, entityExitSignals, exitOwnedEntityIds);
            var facts = BuildEntityExitPresentationFacts(context);
            for (var i = 0; i < facts.Count; i++)
            {
                var fact = facts[i];
                if (exitOwnedEntityIds.Contains(fact.EntityId))
                {
                    continue;
                }

                entityExitSignals.Add(
                    new TickEntityExitPresentationSignal(
                        fact.EntityId,
                        fact.ExitCause,
                        fact.AnchorCell,
                        fact.Topology,
                        fact.Facing,
                        fact.EntityType,
                        sourceActorEntityId: fact.SourceActorEntityId,
                        presentationSeed: BuildStablePresentationSeed(
                            context.CurrentTickIndex,
                            fact.EntityId,
                            fact.SourceActorEntityId,
                            fact.ExitCause),
                        timing: fact.Timing,
                        visualContactNormalizedTime: fact.VisualContactNormalizedTime));
                exitOwnedEntityIds.Add(fact.EntityId);
            }
        }

        private static void BuildDueContactEntityExitPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityExitPresentationSignal> entityExitSignals,
            ISet<int> exitOwnedEntityIds)
        {
            var removedEntityIds = new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds);
            if (removedEntityIds.Count == 0)
            {
                return;
            }

            var dueSignals = context.DueFlipContactSignals;
            for (var i = 0; i < dueSignals.Count; i++)
            {
                var dueSignal = dueSignals[i];
                if (dueSignal.HitEntityId > 0 &&
                    removedEntityIds.Contains(dueSignal.HitEntityId) &&
                    exitOwnedEntityIds.Add(dueSignal.HitEntityId))
                {
                    entityExitSignals.Add(
                        new TickEntityExitPresentationSignal(
                            dueSignal.HitEntityId,
                            TickEntityExitCause.EnemyDeath,
                            dueSignal.ContactCell,
                            dueSignal.Topology,
                            dueSignal.ContactFacing,
                            EntityType.Unit,
                            sourceActorEntityId: dueSignal.ActorEntityId,
                            presentationSeed: dueSignal.PresentationSeed,
                            timing: EntityExitPresentationTiming.Immediate,
                            visualContactNormalizedTime: 0f,
                            timingMode: GameplayPresentationTimingMode.DueContactImmediate));
                }

                if (dueSignal.BoxEntityId > 0 &&
                    dueSignal.BoxDisposition == FlipBoxDisposition.DestroySelf &&
                    removedEntityIds.Contains(dueSignal.BoxEntityId) &&
                    exitOwnedEntityIds.Add(dueSignal.BoxEntityId))
                {
                    entityExitSignals.Add(
                        new TickEntityExitPresentationSignal(
                            dueSignal.BoxEntityId,
                            TickEntityExitCause.BoxDestroy,
                            dueSignal.ContactCell,
                            dueSignal.Topology,
                            dueSignal.ContactFacing,
                            EntityType.Box,
                            sourceActorEntityId: dueSignal.ActorEntityId,
                            presentationSeed: dueSignal.PresentationSeed,
                            timing: EntityExitPresentationTiming.Immediate,
                            visualContactNormalizedTime: 0f,
                            timingMode: GameplayPresentationTimingMode.DueContactImmediate));
                }
            }
        }

        private static IReadOnlyList<EntityExitPresentationFact> BuildEntityExitPresentationFacts(
            in TickPresentationBuildContext context)
        {
            var removedEntityIds = new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds);
            if (removedEntityIds.Count == 0)
            {
                return Array.Empty<EntityExitPresentationFact>();
            }

            var factsByEntityId = new Dictionary<int, EntityExitPresentationFact>();
            var operations = BuildEntityExitCandidateOperations(context);
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Metadata.ExitCauseHint == TickEntityExitCause.None ||
                    !removedEntityIds.Contains(operation.EntityId) ||
                    !IsExitRelevantOperation(operation) ||
                    !TryResolveEntityExitState(context, operation.EntityId, out var sourceEntity, out var topology) ||
                    !IsExitPresentationSupportedEntity(sourceEntity))
                {
                    continue;
                }

                var exitTiming = ResolveEntityExitPresentationTiming(context, operation);
                var fact = new EntityExitPresentationFact(
                    operation.EntityId,
                    operation.Metadata.ExitCauseHint,
                    sourceEntity.type,
                    ResolveExitAnchorCell(context, operation, sourceEntity.position),
                    topology,
                    sourceEntity.facing,
                    operation.Metadata.SourceActorEntityId,
                    exitTiming,
                    operation.Metadata.BoundaryReason,
                    operation.Metadata.HasPresentationTargetCell,
                    exitTiming == EntityExitPresentationTiming.AtContactTime
                        ? GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime
                        : 0f);
                MergePreferMoreSpecificFact(factsByEntityId, fact);
            }

            if (factsByEntityId.Count == 0)
            {
                return Array.Empty<EntityExitPresentationFact>();
            }

            var facts = new List<EntityExitPresentationFact>(factsByEntityId.Values);
            facts.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            return facts;
        }

        private static IReadOnlyList<FinalizationOperation> BuildEntityExitCandidateOperations(
            in TickPresentationBuildContext context)
        {
            if (context.FinalizationBatch.Operations.Count > 0)
            {
                return context.FinalizationBatch.Operations;
            }

            var operations = new List<FinalizationOperation>(
                context.MovementPhaseResult.ResolvedOperations.Count +
                context.AttackPhaseResult.ResolvedOperations.Count);
            AppendOperations(operations, context.MovementPhaseResult.ResolvedOperations);
            AppendOperations(operations, context.AttackPhaseResult.ResolvedOperations);
            return operations;
        }

        private static void AppendOperations(
            List<FinalizationOperation> destination,
            IReadOnlyList<FinalizationOperation> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static bool IsExitRelevantOperation(FinalizationOperation operation)
        {
            return (operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                    operation.BoardPresence == EntityBoardPresence.Detached) ||
                   operation.Kind == FinalizationOperationKind.MarkDestroy;
        }

        private static bool IsExitPresentationSupportedEntity(EntityState entity)
        {
            return entity.type == EntityType.Box ||
                   EntityRolePolicy.IsEnemyUnit(entity);
        }

        private static EntityExitPresentationTiming ResolveEntityExitPresentationTiming(
            in TickPresentationBuildContext context,
            FinalizationOperation operation)
        {
            if (operation.Metadata.ExitPresentationTiming != EntityExitPresentationTiming.Immediate)
            {
                return operation.Metadata.ExitPresentationTiming;
            }

            return IsLethalFlipFollowThroughTargetExit(context, operation)
                ? EntityExitPresentationTiming.AtContactTime
                : operation.Metadata.ExitPresentationTiming;
        }

        private static bool IsLethalFlipFollowThroughTargetExit(
            in TickPresentationBuildContext context,
            FinalizationOperation operation)
        {
            if (operation.Kind != FinalizationOperationKind.MarkDestroy ||
                operation.Metadata.ExitCauseHint != TickEntityExitCause.Killed)
            {
                return false;
            }

            var dispositionRecords = context.MovementPhaseResult.ImpactDispositionRecords;
            for (var i = 0; i < dispositionRecords.Count; i++)
            {
                var record = dispositionRecords[i];
                if (record.PolicyKind == ImpactDispositionPolicyKind.Flip &&
                    record.DispositionKind == ImpactDispositionKind.FollowThrough &&
                    record.TargetDestroyed &&
                    record.ImpactTargetEntityId == operation.EntityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static SurfaceCell ResolveExitAnchorCell(
            in TickPresentationBuildContext context,
            FinalizationOperation operation,
            SurfaceCell fallbackCell)
        {
            if (operation.Metadata.HasPresentationTargetCell)
            {
                return operation.Metadata.PresentationTargetCell;
            }

            if (operation.Kind == FinalizationOperationKind.MoveEntity)
            {
                return operation.Destination;
            }

            if (context.PostAttackSnapshot.TryGetEntity(operation.EntityId, out var postAttackEntity))
            {
                return postAttackEntity.position;
            }

            if (context.PostMovementSnapshot.TryGetEntity(operation.EntityId, out var postMovementEntity))
            {
                return postMovementEntity.position;
            }

            return context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var preMovementEntity)
                ? preMovementEntity.position
                : fallbackCell;
        }

        private static bool TryResolveEntityExitState(
            in TickPresentationBuildContext context,
            int entityId,
            out EntityState entity,
            out CubeTopologyState topology)
        {
            if (context.PostAttackSnapshot.TryGetEntity(entityId, out entity))
            {
                topology = context.PostAttackSnapshot.Topology;
                return true;
            }

            if (context.PostMovementSnapshot.TryGetEntity(entityId, out entity))
            {
                topology = context.PostMovementSnapshot.Topology;
                return true;
            }

            if (context.PreMovementSnapshot.TryGetEntity(entityId, out entity))
            {
                topology = context.PreMovementSnapshot.Topology;
                return true;
            }

            topology = default;
            return false;
        }

        private static void MergePreferMoreSpecificFact(
            IDictionary<int, EntityExitPresentationFact> factsByEntityId,
            in EntityExitPresentationFact candidate)
        {
            if (!factsByEntityId.TryGetValue(candidate.EntityId, out var existing) ||
                GetEntityExitFactSpecificity(candidate) > GetEntityExitFactSpecificity(existing))
            {
                factsByEntityId[candidate.EntityId] = candidate;
            }
        }

        private static int GetEntityExitFactSpecificity(in EntityExitPresentationFact fact)
        {
            var score = 0;
            if (fact.HasExplicitAnchor)
            {
                score += 4;
            }

            if (fact.Timing != EntityExitPresentationTiming.Immediate)
            {
                score += 2;
            }

            if (!string.IsNullOrEmpty(fact.BoundaryReason))
            {
                score += 1;
            }

            return score;
        }

        private static void BuildCleanupPresentation(
            in TickPresentationBuildContext context,
            List<TickVisibilityChange> visibilityChanges,
            ISet<int> exitOwnedEntityIds)
        {
            var removedEntityIds = context.CleanupPhaseResult.RemovedEntityIds;

            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                var entityId = removedEntityIds[i];
                if (exitOwnedEntityIds.Contains(entityId))
                {
                    continue;
                }

                if (!context.PostAttackSnapshot.TryGetEntity(entityId, out var removedEntity))
                {
                    continue;
                }

                visibilityChanges.Add(
                    new TickVisibilityChange(
                        entityId,
                    TickVisibilityChangeKind.Remove,
                    removedEntity.position,
                    context.PostAttackSnapshot.Topology,
                    removedEntity.facing));
            }
        }

        private static void BuildRespawnPresentation(
            in TickPresentationBuildContext context,
            List<TickVisibilityChange> visibilityChanges,
            List<EntitySpawnPresentationSignal> entitySpawnSignals)
        {
            var respawnedEntities = context.RespawnPhaseResult.RespawnedEntities;
            var finalTopology = context.FinalAuthoritativeSnapshot.Topology;
            var tileFeaturesAtCell = new List<TileFeatureState>();

            for (var i = 0; i < respawnedEntities.Count; i++)
            {
                var entity = respawnedEntities[i];
                visibilityChanges.Add(
                    new TickVisibilityChange(
                        entity.entityId,
                        TickVisibilityChangeKind.Spawn,
                        entity.position,
                        finalTopology,
                        entity.facing));

                if (!EntityRolePolicy.IsPlayerUnit(entity))
                {
                    continue;
                }

                entitySpawnSignals.Add(
                    new EntitySpawnPresentationSignal(
                        entity.entityId,
                        EntityPresentationKind.Player,
                        EntitySpawnPresentationReason.PlayerRespawn,
                        entity.position,
                        finalTopology,
                        entity.facing,
                        TryResolveEntranceSource(
                            context.FinalAuthoritativeSnapshot,
                            entity.position,
                            tileFeaturesAtCell)));
            }
        }

        private static TileFeaturePresentationSource? TryResolveEntranceSource(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            List<TileFeatureState> tileFeaturesAtCell)
        {
            if (snapshot == null || tileFeaturesAtCell == null)
            {
                return null;
            }

            snapshot.EnumerateTileFeaturesAt(cell, tileFeaturesAtCell);
            return EntitySpawnPresentationSourceResolver.TryResolveEntranceSource(cell, tileFeaturesAtCell);
        }

        private static void BuildSummonedEnemyPresentationBindings(
            in TickPresentationBuildContext context,
            List<TickSummonedEnemyPresentationBinding> bindings)
        {
            var summonedEntries = new List<SummonedEntitySnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumerateSummonedEntityStatesOrdered(summonedEntries);

            for (var i = 0; i < summonedEntries.Count; i++)
            {
                var summonedEntry = summonedEntries[i];
                if (!context.FinalAuthoritativeSnapshot.TryGetEntity(summonedEntry.EntityId, out var entity) ||
                    !EntityRolePolicy.IsEnemyUnit(entity))
                {
                    continue;
                }

                var hasBinding = context.FinalAuthoritativeSnapshot.TryGetEnemyDefinitionBindingState(
                    summonedEntry.EntityId,
                    out var bindingState);
                bindings.Add(
                    new TickSummonedEnemyPresentationBinding(
                        summonedEntry.EntityId,
                        hasBinding,
                        hasBinding ? bindingState.ArchetypeId : EnemyUnitArchetypeId.None,
                        summonedEntry.State.SourceEntityId));
            }
        }

        private static void BuildPlayerPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerActionPresentationSignal> playerActionSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var transitionsByEntityId = new Dictionary<int, PlayerActionTransition>();
            var playerControlEntries = new List<PlayerControlSnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumeratePlayerControlStatesOrdered(playerControlEntries);

            for (var i = 0; i < playerControlEntries.Count; i++)
            {
                var entry = playerControlEntries[i];
                if (!entry.State.activeAction.IsActive ||
                    !seenEntityIds.Add(entry.EntityId))
                {
                    continue;
                }

                candidateEntityIds.Add(entry.EntityId);
            }

            var actionTransitions = context.PreMovementStatePhaseResult.PlayerActionTransitions;
            for (var i = 0; i < actionTransitions.Count; i++)
            {
                var transition = actionTransitions[i];
                if (!transition.StartedThisTick &&
                    !transition.CompletedThisTick &&
                    !transition.CanceledThisTick &&
                    transition.PreviousKind == PlayerActionKind.None &&
                    transition.CurrentKind == PlayerActionKind.None)
                {
                    continue;
                }

                transitionsByEntityId[transition.EntityId] = transition;
                if (seenEntityIds.Add(transition.EntityId))
                {
                    candidateEntityIds.Add(transition.EntityId);
                }
            }

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                var activeActionKind = PlayerActionKind.None;
                var activeActionSequence = 0;
                var startedThisTick = false;
                var executedThisTick = false;
                var completedThisTick = false;
                var canceledThisTick = false;
                var targetEntityId = 0;
                var direction = Direction.None;
                var actionPlanId = 0;

                if (transitionsByEntityId.TryGetValue(entityId, out var transition))
                {
                    activeActionKind = transition.CurrentKind;
                    activeActionSequence = transition.CurrentSequence;
                    if ((transition.CompletedThisTick || transition.CanceledThisTick) &&
                        transition.CurrentKind == PlayerActionKind.None)
                    {
                        activeActionKind = transition.PreviousKind;
                        activeActionSequence = transition.PreviousSequence;
                    }

                    startedThisTick = transition.StartedThisTick;
                    completedThisTick = transition.CompletedThisTick;
                    canceledThisTick = transition.CanceledThisTick;
                }

                if (context.FinalAuthoritativeSnapshot.TryGetPlayerControlState(entityId, out var controlState))
                {
                    activeActionKind = controlState.activeAction.kind;
                    activeActionSequence = controlState.activeAction.sequence;
                    executedThisTick = controlState.activeAction.IsActive &&
                                       controlState.activeAction.executeTick == context.CurrentTickIndex;
                    targetEntityId = controlState.activeAction.targetEntityId;
                    direction = controlState.activeAction.direction;
                }

                actionPlanId = ResolvePlayerActionPlanId(
                    context,
                    entityId,
                    activeActionKind,
                    targetEntityId,
                    direction);

                var isRecoveryPhase =
                    activeActionKind != PlayerActionKind.None &&
                    context.FinalAuthoritativeSnapshot.TryGetPlayerControlState(entityId, out var finalControlState) &&
                    finalControlState.activeAction.IsActive &&
                    finalControlState.activeAction.executeTick <= context.CurrentTickIndex;
                var resolutionKind = ResolvePlayerActionResolutionKind(
                    context,
                    entityId,
                    activeActionKind,
                    executedThisTick);
                var flipOutcome = ResolvePlayerFlipOutcome(
                    context,
                    entityId,
                    activeActionKind,
                    actionPlanId,
                    resolutionKind);
                var hasFlipImpactContactTiming =
                    flipOutcome == TickPlayerFlipOutcomeKind.DestroySelf ||
                    flipOutcome == TickPlayerFlipOutcomeKind.Stay;
                var flipTargetBoxEntityId = activeActionKind == PlayerActionKind.Flip
                    ? targetEntityId
                    : 0;

                playerActionSignals.Add(
                    new TickPlayerActionPresentationSignal(
                        entityId,
                        activeActionKind,
                        activeActionSequence,
                        startedThisTick,
                        completedThisTick,
                        canceledThisTick,
                        executedThisTick,
                        isRecoveryPhase,
                        resolutionKind,
                        targetEntityId,
                        direction,
                        actionPlanId,
                        flipOutcome,
                        hasFlipImpactContactTiming,
                        flipTargetBoxEntityId));
            }
        }

        private static void BuildPlayerLocomotionPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals)
        {
            var playerControlEntries = new List<PlayerControlSnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumeratePlayerControlStatesOrdered(playerControlEntries);

            for (var i = 0; i < playerControlEntries.Count; i++)
            {
                var entry = playerControlEntries[i];
                var moveMotionGeneratedThisTick = DidGeneratePlayerMoveMotionThisTick(context, entry.EntityId);
                var waitingForNextMoveCadence = ShouldWaitForNextMoveCadence(context, entry.State);
                var shouldPlayWalkLoop =
                    !entry.State.activeAction.IsActive &&
                    !context.PlayerCommand.PushPressed &&
                    !context.PlayerCommand.FlipPressed &&
                    context.PlayerCommand.MoveDirection != Direction.None;

                playerLocomotionSignals.Add(
                    new TickPlayerLocomotionPresentationSignal(
                        entry.EntityId,
                        shouldPlayWalkLoop,
                        moveMotionGeneratedThisTick,
                        waitingForNextMoveCadence,
                        context.PlayerCommand.MoveDirection,
                        context.PlayerCommand.IsMoveBuffered));
            }
        }

        private static void BuildPlayerFlipResultTurnPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerFlipResultTurnSignal> playerFlipResultTurnSignals)
        {
            var actionTransitions = context.PreMovementStatePhaseResult.PlayerActionTransitions;
            var emittedActionKeys = new HashSet<(int EntityId, int ActionSequence)>();
            for (var i = 0; i < actionTransitions.Count; i++)
            {
                var flipResultTurn = actionTransitions[i].FlipResultTurnTransition;
                if (!flipResultTurn.HasFlipResultTurn ||
                    !emittedActionKeys.Add((flipResultTurn.EntityId, flipResultTurn.ActionSequence)))
                {
                    continue;
                }

                playerFlipResultTurnSignals.Add(
                    new TickPlayerFlipResultTurnSignal(
                        flipResultTurn.EntityId,
                        flipResultTurn.ActionSequence,
                        flipResultTurn.ActionDirection,
                        flipResultTurn.ContactFacing,
                        flipResultTurn.ResultFacing,
                        flipResultTurn.StartTick,
                        flipResultTurn.Reason));
            }
        }

        private static void BuildPlayerDamagePresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerDamagePresentationSignal> playerDamageSignals)
        {
            var aggregatedDamageByPlayerId = new Dictionary<int, int>();

            for (var i = 0; i < context.AttackPhaseResult.DamageResolutions.Count; i++)
            {
                var resolution = context.AttackPhaseResult.DamageResolutions[i];
                if (!resolution.Accepted ||
                    !context.PostAttackSnapshot.TryGetEntity(resolution.TargetId, out var target) ||
                    !EntityRolePolicy.IsPlayerUnit(target))
                {
                    continue;
                }

                if (!aggregatedDamageByPlayerId.TryGetValue(resolution.TargetId, out var amount))
                {
                    amount = 0;
                }

                aggregatedDamageByPlayerId[resolution.TargetId] = amount + resolution.Amount;
            }

            foreach (var pair in aggregatedDamageByPlayerId)
            {
                playerDamageSignals.Add(
                    new TickPlayerDamagePresentationSignal(
                        pair.Key,
                        tookDamageThisTick: true,
                        pair.Value));
            }
        }

        private static void BuildPlayerDeathPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerDeathPresentationSignal> playerDeathSignals)
        {
            var remainingHpByPlayerId = new Dictionary<int, int>();
            var fatalSignalsByPlayerId = new Dictionary<int, TickPlayerDeathPresentationSignal>();

            // Damage resolutions are already emitted in runtime canonical order. Consume them
            // as-is so fatal-source presentation remains deterministic with simulation output.
            for (var i = 0; i < context.AttackPhaseResult.DamageResolutions.Count; i++)
            {
                var resolution = context.AttackPhaseResult.DamageResolutions[i];
                if (!resolution.Accepted ||
                    fatalSignalsByPlayerId.ContainsKey(resolution.TargetId) ||
                    !context.PostMovementSnapshot.TryGetEntity(resolution.TargetId, out var target) ||
                    !EntityRolePolicy.IsPlayerUnit(target))
                {
                    continue;
                }

                if (!remainingHpByPlayerId.TryGetValue(resolution.TargetId, out var remainingHp))
                {
                    remainingHp = target.hp;
                }

                remainingHp -= resolution.Amount;
                remainingHpByPlayerId[resolution.TargetId] = remainingHp;
                if (remainingHp > 0)
                {
                    continue;
                }

                var hasResolvedDamageSource = resolution.SourceId > 0;
                fatalSignalsByPlayerId[resolution.TargetId] = new TickPlayerDeathPresentationSignal(
                    resolution.TargetId,
                    didDieThisTick: true,
                    resolution.SourceId,
                    target.facing,
                    hasResolvedDamageSource,
                    resolution.Amount,
                    hasResolvedDamageSource
                        ? DeathDirectionHintKind.AttackerReverse
                        : DeathDirectionHintKind.FacingReverse);
            }

            var removedPlayerIds = context.CleanupPhaseResult.RemovedEntityIds.Count > 0
                ? new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds)
                : null;
            if (fatalSignalsByPlayerId.Count > 0)
            {
                foreach (var pair in fatalSignalsByPlayerId)
                {
                    var didDieThisTick =
                        (context.PostAttackSnapshot.TryGetEntity(pair.Key, out var postAttackPlayer) &&
                         postAttackPlayer.hp <= 0) ||
                        (removedPlayerIds != null && removedPlayerIds.Contains(pair.Key));
                    if (!didDieThisTick)
                    {
                        continue;
                    }

                    playerDeathSignals.Add(pair.Value);
                }
            }

            BuildMovementOwnedPlayerDeathPresentation(context, playerDeathSignals, fatalSignalsByPlayerId);
        }

        private static void BuildMovementOwnedPlayerDeathPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IDictionary<int, TickPlayerDeathPresentationSignal> existingSignalsByPlayerId)
        {
            var removedPlayerIds = context.CleanupPhaseResult.RemovedEntityIds.Count > 0
                ? new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds)
                : null;
            if (removedPlayerIds == null)
            {
                return;
            }

            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.MarkDestroy ||
                    operation.Metadata.DamageSourceType != DamageSourceType.Environmental ||
                    operation.Metadata.BoundaryReason != "DestroyTile" ||
                    existingSignalsByPlayerId.ContainsKey(operation.EntityId) ||
                    !removedPlayerIds.Contains(operation.EntityId) ||
                    !context.PostAttackSnapshot.TryGetEntity(operation.EntityId, out var player) ||
                    !EntityRolePolicy.IsPlayerUnit(player))
                {
                    continue;
                }

                var signal = new TickPlayerDeathPresentationSignal(
                    operation.EntityId,
                    didDieThisTick: true,
                    sourceEntityId: 0,
                    player.facing,
                    resolvedDamageSourceAvailable: false,
                    damageAmountAtFatalHit: Math.Max(1, player.hp),
                    DeathDirectionHintKind.FacingReverse);
                existingSignalsByPlayerId[operation.EntityId] = signal;
                playerDeathSignals.Add(signal);
            }
        }

        private static int ResolvePlayerActionPlanId(
            in TickPresentationBuildContext context,
            int entityId,
            PlayerActionKind actionKind,
            int targetEntityId,
            Direction direction)
        {
            var resolutionRecords = context.ResolutionRecords;
            for (var i = 0; i < resolutionRecords.Count; i++)
            {
                var record = resolutionRecords[i];
                if (!record.Accepted ||
                    record.Kind != ContestKind.Space ||
                    record.LocalActionIndex != 0 ||
                    record.SourceId != entityId)
                {
                    continue;
                }

                return record.ActionPlanId;
            }

            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var metadata = operations[i].Metadata;
                if (metadata.SourceActorEntityId != entityId ||
                    metadata.ActionPlanId <= 0 ||
                    !DoesOperationMatchPlayerAction(operations[i], actionKind, targetEntityId, direction))
                {
                    continue;
                }

                return metadata.ActionPlanId;
            }

            return 0;
        }

        private static TickPlayerFlipOutcomeKind ResolvePlayerFlipOutcome(
            in TickPresentationBuildContext context,
            int entityId,
            PlayerActionKind actionKind,
            int actionPlanId,
            TickPlayerActionResolutionKind resolutionKind)
        {
            if (actionKind != PlayerActionKind.Flip)
            {
                return TickPlayerFlipOutcomeKind.None;
            }

            var dispositionRecords = context.MovementPhaseResult.ImpactDispositionRecords;
            for (var i = 0; i < dispositionRecords.Count; i++)
            {
                var record = dispositionRecords[i];
                if (record.PolicyKind != ImpactDispositionPolicyKind.Flip ||
                    record.ImpactTargetEntityId == entityId)
                {
                    continue;
                }

                if ((actionPlanId > 0 && record.ActionPlanId == actionPlanId) ||
                    ResolveActionActorEntityId(context, record.ActionPlanId) == entityId)
                {
                    return record.DispositionKind switch
                    {
                        ImpactDispositionKind.DestroySelf => TickPlayerFlipOutcomeKind.DestroySelf,
                        ImpactDispositionKind.Stay => TickPlayerFlipOutcomeKind.Stay,
                        ImpactDispositionKind.FollowThrough => TickPlayerFlipOutcomeKind.FollowThrough,
                        _ => TickPlayerFlipOutcomeKind.None,
                    };
                }
            }

            return resolutionKind switch
            {
                TickPlayerActionResolutionKind.Success => TickPlayerFlipOutcomeKind.FollowThrough,
                TickPlayerActionResolutionKind.Blocked => TickPlayerFlipOutcomeKind.Blocked,
                _ => TickPlayerFlipOutcomeKind.None,
            };
        }

        private static TickPlayerActionResolutionKind ResolvePlayerActionResolutionKind(
            in TickPresentationBuildContext context,
            int entityId,
            PlayerActionKind actionKind,
            bool executedThisTick)
        {
            if (!executedThisTick ||
                actionKind == PlayerActionKind.None)
            {
                return TickPlayerActionResolutionKind.None;
            }

            if (DidResolvePlayerImpactThisTick(context.AttackPhaseResult, entityId))
            {
                return TickPlayerActionResolutionKind.Impact;
            }

            if (DidResolvePlayerActionMovementThisTick(context.MovementPhaseResult, entityId, actionKind))
            {
                return TickPlayerActionResolutionKind.Success;
            }

            return TickPlayerActionResolutionKind.Blocked;
        }

        private static int ResolveActionActorEntityId(
            in TickPresentationBuildContext context,
            int actionPlanId)
        {
            if (actionPlanId <= 0)
            {
                return 0;
            }

            var resolutionRecords = context.ResolutionRecords;
            for (var i = 0; i < resolutionRecords.Count; i++)
            {
                var record = resolutionRecords[i];
                if (record.Accepted &&
                    record.Kind == ContestKind.Space &&
                    record.ActionPlanId == actionPlanId &&
                    record.LocalActionIndex == 0)
                {
                    return record.SourceId;
                }
            }

            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var metadata = operations[i].Metadata;
                if (metadata.ActionPlanId == actionPlanId &&
                    metadata.SourceActorEntityId != 0)
                {
                    return metadata.SourceActorEntityId;
                }
            }

            return 0;
        }

        private static bool DoesOperationMatchPlayerAction(
            FinalizationOperation operation,
            PlayerActionKind actionKind,
            int targetEntityId,
            Direction direction)
        {
            if (actionKind == PlayerActionKind.None)
            {
                return false;
            }

            if (operation.Metadata.MovementSemanticKind == MovementSemanticKind.None)
            {
                return false;
            }

            if (actionKind == PlayerActionKind.Flip &&
                operation.Metadata.MovementSemanticKind == MovementSemanticKind.Flip)
            {
                return true;
            }

            if (actionKind == PlayerActionKind.Push &&
                operation.Metadata.MovementSemanticKind == MovementSemanticKind.Push)
            {
                return true;
            }

            if (targetEntityId != 0 && operation.EntityId == targetEntityId)
            {
                return true;
            }

            return direction != Direction.None &&
                   operation.Kind == FinalizationOperationKind.SetFacing &&
                   operation.Facing == direction;
        }

        private static bool TryResolveFlipImpactDisposition(
            ImpactDispositionKind dispositionKind,
            out FlipImpactPresentationDisposition disposition)
        {
            disposition = dispositionKind switch
            {
                ImpactDispositionKind.DestroySelf => FlipImpactPresentationDisposition.DestroySelf,
                ImpactDispositionKind.Stay => FlipImpactPresentationDisposition.Stay,
                _ => 0,
            };

            return disposition != 0;
        }

        private static bool TryResolveFlipFloorImpactKind(
            ImpactDispositionKind dispositionKind,
            out FlipFloorImpactPresentationKind kind)
        {
            kind = dispositionKind switch
            {
                ImpactDispositionKind.DestroySelf => FlipFloorImpactPresentationKind.DestroySelf,
                ImpactDispositionKind.Stay => FlipFloorImpactPresentationKind.Stay,
                ImpactDispositionKind.FollowThrough => FlipFloorImpactPresentationKind.FollowThrough,
                _ => 0,
            };

            return kind != 0;
        }

        private static long BuildFlipImpactSignalKey(int sourceActionPlanId, int boxEntityId)
        {
            var upper = (long)sourceActionPlanId << 32;
            return upper ^ (uint)boxEntityId;
        }

        private static bool DidResolvePlayerImpactThisTick(
            AttackPhaseResult attackPhaseResult,
            int entityId)
        {
            var reservations = attackPhaseResult.DrainedImpactReservations;
            for (var i = 0; i < reservations.Count; i++)
            {
                if (reservations[i].SourceId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DidResolvePlayerActionMovementThisTick(
            MovementPhaseResult movementPhaseResult,
            int entityId,
            PlayerActionKind actionKind)
        {
            var operations = movementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.MoveEntity ||
                    operation.Metadata.SourceActorEntityId != entityId)
                {
                    continue;
                }

                switch (actionKind)
                {
                    case PlayerActionKind.Push when operation.Metadata.SemanticKind == ResolvedActionSemanticKind.Push:
                    case PlayerActionKind.Flip when operation.Metadata.SemanticKind == ResolvedActionSemanticKind.Flip:
                        return true;
                }
            }

            return false;
        }

        private static void BuildEnemyPresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyActionPresentationSignal> enemyActionSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var executedEntityIds = new HashSet<int>();
            var preMovementEntries = new List<EnemyActionSnapshotEntry>();
            var postAttackEntries = new List<EnemyActionSnapshotEntry>();
            var finalEntries = new List<EnemyActionSnapshotEntry>();

            context.PreMovementSnapshot.EnumerateEnemyActionStatesOrdered(preMovementEntries);
            context.PostAttackSnapshot.EnumerateEnemyActionStatesOrdered(postAttackEntries);
            context.FinalAuthoritativeSnapshot.EnumerateEnemyActionStatesOrdered(finalEntries);

            CollectEnemyActionCandidateIds(preMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyActionCandidateIds(postAttackEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyActionCandidateIds(finalEntries, seenEntityIds, candidateEntityIds);

            for (var i = 0; i < context.AttackPhaseResult.ResolutionRecords.Count; i++)
            {
                var resolutionRecord = context.AttackPhaseResult.ResolutionRecords[i];
                if (resolutionRecord.Kind != ContestKind.Plan ||
                    !resolutionRecord.Accepted ||
                    !IsEnemyUnit(context.PostMovementSnapshot, resolutionRecord.SourceId))
                {
                    continue;
                }

                executedEntityIds.Add(resolutionRecord.SourceId);
                if (seenEntityIds.Add(resolutionRecord.SourceId))
                {
                    candidateEntityIds.Add(resolutionRecord.SourceId);
                }
            }

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                context.PreMovementSnapshot.TryGetEnemyActionState(entityId, out var previousAction);
                context.PostAttackSnapshot.TryGetEnemyActionState(entityId, out var currentAction);
                if (currentAction.IsActive &&
                    currentAction.executionAttempted &&
                    !previousAction.executionAttempted)
                {
                    executedEntityIds.Add(entityId);
                }

                var transition = new EnemyActionTransition(entityId, previousAction, currentAction);
                var activeActionKind = EnemyActionKind.None;
                var activeActionSequence = 0;

                if (context.FinalAuthoritativeSnapshot.TryGetEnemyActionState(entityId, out var finalAction))
                {
                    activeActionKind = finalAction.kind;
                    activeActionSequence = finalAction.sequence;
                }

                enemyActionSignals.Add(
                    new TickEnemyActionPresentationSignal(
                        entityId,
                        activeActionKind,
                        activeActionSequence,
                        transition.StartedThisTick,
                        transition.CanceledThisTick,
                        executedEntityIds.Contains(entityId),
                        DidStartEnemyRecovery(context.PostMovementSnapshot, context.PostAttackSnapshot, entityId)));
            }
        }

        private static void BuildEnemyDamagePresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyDamagePresentationSignal> enemyDamageSignals)
        {
            var acceptedDamageByEntityId = new Dictionary<int, int>();
            AddDueEnemyDamagePresentation(context, acceptedDamageByEntityId);
            var damageResolutions = context.AttackPhaseResult.DamageResolutions;

            for (var i = 0; i < damageResolutions.Count; i++)
            {
                var resolution = damageResolutions[i];
                if (!resolution.Accepted ||
                    !context.PostAttackSnapshot.TryGetEntity(resolution.TargetId, out var targetEntity) ||
                    !EntityRolePolicy.IsEnemyUnit(targetEntity))
                {
                    continue;
                }

                acceptedDamageByEntityId.TryGetValue(targetEntity.entityId, out var accumulatedDamage);
                acceptedDamageByEntityId[targetEntity.entityId] = accumulatedDamage + resolution.Amount;
            }

            foreach (var pair in acceptedDamageByEntityId)
            {
                enemyDamageSignals.Add(
                    new TickEnemyDamagePresentationSignal(
                        pair.Key,
                        tookDamageThisTick: true,
                        pair.Value));
            }
        }

        private static void AddDueEnemyDamagePresentation(
            in TickPresentationBuildContext context,
            IDictionary<int, int> acceptedDamageByEntityId)
        {
            if (context.DueDamageResolutions.Count == 0 ||
                context.DueFlipContactSignals.Count == 0)
            {
                return;
            }

            var dueHitEntityIds = new HashSet<int>();
            var dueSignals = context.DueFlipContactSignals;
            for (var i = 0; i < dueSignals.Count; i++)
            {
                if (dueSignals[i].HitEntityId > 0)
                {
                    dueHitEntityIds.Add(dueSignals[i].HitEntityId);
                }
            }

            var damageResolutions = context.DueDamageResolutions;
            for (var i = 0; i < damageResolutions.Count; i++)
            {
                var resolution = damageResolutions[i];
                if (!resolution.Accepted ||
                    !dueHitEntityIds.Contains(resolution.TargetId))
                {
                    continue;
                }

                acceptedDamageByEntityId.TryGetValue(resolution.TargetId, out var accumulatedDamage);
                acceptedDamageByEntityId[resolution.TargetId] = accumulatedDamage + resolution.Amount;
            }
        }

        private static void BuildEnemyJumpPresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyJumpPresentationSignal> enemyJumpSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var preMovementEntries = new List<EnemyJumpSnapshotEntry>();
            var postMovementEntries = new List<EnemyJumpSnapshotEntry>();
            var finalEntries = new List<EnemyJumpSnapshotEntry>();

            context.JumpBaselineSnapshot.EnumerateEnemyJumpStatesOrdered(preMovementEntries);
            context.PostMovementSnapshot.EnumerateEnemyJumpStatesOrdered(postMovementEntries);
            context.FinalAuthoritativeSnapshot.EnumerateEnemyJumpStatesOrdered(finalEntries);

            CollectEnemyJumpCandidateIds(preMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyJumpCandidateIds(postMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyJumpCandidateIds(finalEntries, seenEntityIds, candidateEntityIds);

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                var hasPreviousState = context.JumpBaselineSnapshot.TryGetEnemyJumpState(entityId, out var previousJumpState);
                var hasPostMovementState = context.PostMovementSnapshot.TryGetEnemyJumpState(entityId, out var postMovementJumpState);
                var hasFinalState = context.FinalAuthoritativeSnapshot.TryGetEnemyJumpState(entityId, out var finalJumpState);

                var resolvedState = ResolvePresentationJumpState(
                    hasPreviousState,
                    previousJumpState,
                    hasPostMovementState,
                    postMovementJumpState,
                    hasFinalState,
                    finalJumpState);
                if (!ShouldEmitJumpSignal(hasPreviousState, previousJumpState, hasPostMovementState, postMovementJumpState, hasFinalState, finalJumpState))
                {
                    continue;
                }

                var startedWindupThisTick = false;
                var startedAirborneThisTick = false;
                var landedThisTick = false;
                var retryThisTick = false;
                var presentationTargetCell = resolvedState.lockedTargetCell;
                var outcome = TickEnemyJumpPresentationOutcome.None;
                if (TryFindJumpPresentationOperation(context.MovementPhaseResult.ResolvedOperations, entityId, out var jumpOperation))
                {
                    startedWindupThisTick = jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.WindupStart;
                    startedAirborneThisTick = jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.AirborneStart;
                    landedThisTick = jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.LandingSuccess ||
                                      jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.CrushedBoxAndLanded;
                    retryThisTick = jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.LandingRetry;
                    outcome = ResolveJumpPresentationOutcome(jumpOperation.Metadata.JumpPresentationKind);
                    if (!jumpOperation.Metadata.PresentationTargetCell.Equals(default(SurfaceCell)))
                    {
                        presentationTargetCell = jumpOperation.Metadata.PresentationTargetCell;
                    }
                }

                var facing = ResolveJumpPresentationFacing(context, entityId, resolvedState);
                var remainingAirborneTicks = resolvedState.phase == EnemyJumpPhase.Airborne
                    ? Math.Max(0, resolvedState.landingTick - context.CurrentTickIndex)
                    : 0;
                var windupTicks = resolvedState.phase == EnemyJumpPhase.Windup
                    ? Math.Max(0, resolvedState.windupEndTick - context.CurrentTickIndex)
                    : 0;

                enemyJumpSignals.Add(
                    new TickEnemyJumpPresentationSignal(
                        entityId,
                        resolvedState.sequence,
                        resolvedState.phase,
                        startedWindupThisTick,
                        startedAirborneThisTick,
                        landedThisTick,
                        retryThisTick,
                        sourceCell: resolvedState.sourceCell,
                        lockedTargetCell: resolvedState.lockedTargetCell,
                        presentationTargetCell: presentationTargetCell,
                        facing: facing,
                        windupTicks: windupTicks,
                        landingTick: resolvedState.landingTick,
                        remainingAirborneTicks: remainingAirborneTicks,
                        retryCount: resolvedState.retryCount,
                        outcome: outcome));
            }
        }

        private static TickEnemyJumpPresentationOutcome ResolveJumpPresentationOutcome(
            JumpPresentationKind presentationKind)
        {
            return presentationKind switch
            {
                JumpPresentationKind.WindupStart => TickEnemyJumpPresentationOutcome.WindupStarted,
                JumpPresentationKind.AirborneStart => TickEnemyJumpPresentationOutcome.AirborneStarted,
                JumpPresentationKind.LandingSuccess => TickEnemyJumpPresentationOutcome.Landed,
                JumpPresentationKind.LandingRetry => TickEnemyJumpPresentationOutcome.Retried,
                JumpPresentationKind.CrushedBoxAndLanded => TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded,
                _ => TickEnemyJumpPresentationOutcome.None,
            };
        }

        private static void CollectEnemyActionCandidateIds(
            List<EnemyActionSnapshotEntry> entries,
            HashSet<int> seenEntityIds,
            List<int> candidateEntityIds)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.State.IsActive ||
                    !seenEntityIds.Add(entry.EntityId))
                {
                    continue;
                }

                candidateEntityIds.Add(entry.EntityId);
            }
        }

        private static void CollectEnemyJumpCandidateIds(
            List<EnemyJumpSnapshotEntry> entries,
            HashSet<int> seenEntityIds,
            List<int> candidateEntityIds)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.State.phase == EnemyJumpPhase.None ||
                    !seenEntityIds.Add(entry.EntityId))
                {
                    continue;
                }

                candidateEntityIds.Add(entry.EntityId);
            }
        }

        private static void BuildEnemyChargePresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyChargePresentationSignal> enemyChargeSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var baselineEntries = new List<EnemyChargeSnapshotEntry>();
            var postMovementEntries = new List<EnemyChargeSnapshotEntry>();
            var finalEntries = new List<EnemyChargeSnapshotEntry>();

            context.JumpBaselineSnapshot.EnumerateEnemyChargeStatesOrdered(baselineEntries);
            context.PostMovementSnapshot.EnumerateEnemyChargeStatesOrdered(postMovementEntries);
            context.FinalAuthoritativeSnapshot.EnumerateEnemyChargeStatesOrdered(finalEntries);

            CollectEnemyChargeCandidateIds(baselineEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyChargeCandidateIds(postMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyChargeCandidateIds(finalEntries, seenEntityIds, candidateEntityIds);

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                var hasBaselineState = context.JumpBaselineSnapshot.TryGetEnemyChargeState(entityId, out var baselineChargeState);
                var hasPostMovementState = context.PostMovementSnapshot.TryGetEnemyChargeState(entityId, out var postMovementChargeState);
                var hasFinalState = context.FinalAuthoritativeSnapshot.TryGetEnemyChargeState(entityId, out var finalChargeState);

                var resolvedState = ResolvePresentationChargeState(
                    hasBaselineState,
                    baselineChargeState,
                    hasPostMovementState,
                    postMovementChargeState,
                    hasFinalState,
                    finalChargeState);
                if (resolvedState.phase != EnemyChargePhase.None &&
                    !IsEnemyChargePresentationEligible(context.FinalAuthoritativeSnapshot, entityId))
                {
                    continue;
                }

                if (!ShouldEmitChargeSignal(
                        hasBaselineState,
                        baselineChargeState,
                        hasPostMovementState,
                        postMovementChargeState,
                        hasFinalState,
                        finalChargeState))
                {
                    continue;
                }

                enemyChargeSignals.Add(
                    new TickEnemyChargePresentationSignal(
                        entityId,
                        resolvedState.sequence,
                        resolvedState.phase,
                        startedWindupThisTick: resolvedState.phase == EnemyChargePhase.Windup &&
                                               (!hasBaselineState || baselineChargeState.phase != EnemyChargePhase.Windup),
                        startedActiveThisTick: resolvedState.phase == EnemyChargePhase.Active &&
                                               (!hasBaselineState || baselineChargeState.phase != EnemyChargePhase.Active),
                        startedRecoverThisTick: resolvedState.phase == EnemyChargePhase.Recover &&
                                                (!hasBaselineState || baselineChargeState.phase != EnemyChargePhase.Recover),
                        lockedDirection: resolvedState.lockedDirection));
            }
        }

        private static void CollectEnemyChargeCandidateIds(
            List<EnemyChargeSnapshotEntry> entries,
            HashSet<int> seenEntityIds,
            List<int> candidateEntityIds)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.State.phase == EnemyChargePhase.None ||
                    !seenEntityIds.Add(entry.EntityId))
                {
                    continue;
                }

                candidateEntityIds.Add(entry.EntityId);
            }
        }

        private static void BuildEnemyGlidePresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyGlidePresentationSignal> enemyGlideSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var preMovementEntries = new List<EnemyGlideSnapshotEntry>();
            var postMovementEntries = new List<EnemyGlideSnapshotEntry>();
            var finalEntries = new List<EnemyGlideSnapshotEntry>();

            context.PreMovementSnapshot.EnumerateEnemyGlideStatesOrdered(preMovementEntries);
            context.PostMovementSnapshot.EnumerateEnemyGlideStatesOrdered(postMovementEntries);
            context.FinalAuthoritativeSnapshot.EnumerateEnemyGlideStatesOrdered(finalEntries);

            CollectEnemyGlideCandidateIds(preMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyGlideCandidateIds(postMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyGlideCandidateIds(finalEntries, seenEntityIds, candidateEntityIds);

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                var hasPreviousState = context.PreMovementSnapshot.TryGetEnemyGlideState(entityId, out var previousGlideState);
                var hasPostMovementState = context.PostMovementSnapshot.TryGetEnemyGlideState(entityId, out var postMovementGlideState);
                var hasFinalState = context.FinalAuthoritativeSnapshot.TryGetEnemyGlideState(entityId, out var finalGlideState);
                var resolvedState = ResolvePresentationGlideState(
                    hasPreviousState,
                    previousGlideState,
                    hasPostMovementState,
                    postMovementGlideState,
                    hasFinalState,
                    finalGlideState);
                var previousWasVisual = IsGlideVisualPhase(previousGlideState.Phase) ||
                                        IsGlideVisualPhase(postMovementGlideState.Phase);
                var stateClearedBeforeFinal = previousWasVisual &&
                                              !hasFinalState;
                var isTerminalZero = stateClearedBeforeFinal ||
                                     (!IsGlideVisualPhase(resolvedState.Phase) && previousWasVisual);
                if (!IsGlideVisualPhase(resolvedState.Phase) && !isTerminalZero)
                {
                    continue;
                }

                if (!context.FinalAuthoritativeSnapshot.TryGetEntity(entityId, out var entity) ||
                    !EntityRolePolicy.IsEnemyUnit(entity))
                {
                    continue;
                }

                var presentationSettings = ResolveEnemyGlidePresentationSettings(context, entity);
                var progressInfo = ResolveGlidePhaseProgress(resolvedState, context.CurrentTickIndex);
                var currentHeightUnits = isTerminalZero
                    ? 0
                    : ResolveGlideHeightUnits(
                        resolvedState.Phase,
                        progressInfo.Progress,
                        presentationSettings.LiftHeightUnits,
                        presentationSettings.RecoveryDipHeightUnits);

                enemyGlideSignals.Add(
                    new TickEnemyGlidePresentationSignal(
                        entityId,
                        entity.position,
                        resolvedState.Phase,
                        resolvedState.Sequence,
                        progressInfo.ElapsedTicks,
                        progressInfo.TotalTicks,
                        progressInfo.Progress,
                        presentationSettings.LiftHeightUnits,
                        presentationSettings.RecoveryDipHeightUnits,
                        currentHeightUnits,
                        IsGlideVisualPhase(resolvedState.Phase) && currentHeightUnits != 0,
                        resolvedState.WantsRecover,
                        isTerminalZero));
            }
        }

        private static EnemyGlidePresentationSettings ResolveEnemyGlidePresentationSettings(
            in TickPresentationBuildContext context,
            in EntityState entity)
        {
            return context.EnemyGlidePresentationSettingsResolver != null &&
                   context.EnemyGlidePresentationSettingsResolver.TryResolveEnemyGlidePresentationSettings(
                       context.FinalAuthoritativeSnapshot,
                       entity,
                       out var settings)
                ? settings
                : EnemyGlidePresentationSettings.CreateDefault();
        }

        private static void CollectEnemyGlideCandidateIds(
            List<EnemyGlideSnapshotEntry> entries,
            HashSet<int> seenEntityIds,
            List<int> candidateEntityIds)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.State.Phase == EnemyGlidePhase.Ready ||
                    !seenEntityIds.Add(entry.EntityId))
                {
                    continue;
                }

                candidateEntityIds.Add(entry.EntityId);
            }
        }

        private static EnemyGlideRuntimeState ResolvePresentationGlideState(
            bool hasPreviousState,
            in EnemyGlideRuntimeState previousGlideState,
            bool hasPostMovementState,
            in EnemyGlideRuntimeState postMovementGlideState,
            bool hasFinalState,
            in EnemyGlideRuntimeState finalGlideState)
        {
            if (hasFinalState)
            {
                return finalGlideState;
            }

            if (hasPostMovementState)
            {
                return postMovementGlideState;
            }

            return hasPreviousState
                ? previousGlideState
                : default;
        }

        private static bool IsGlideVisualPhase(EnemyGlidePhase phase)
        {
            return phase == EnemyGlidePhase.Windup ||
                   phase == EnemyGlidePhase.Active ||
                   phase == EnemyGlidePhase.Recovery;
        }

        private static (int ElapsedTicks, int TotalTicks, float Progress) ResolveGlidePhaseProgress(
            in EnemyGlideRuntimeState state,
            int currentTickIndex)
        {
            var totalTicks = state.Phase switch
            {
                EnemyGlidePhase.Windup => state.WindupTicks,
                EnemyGlidePhase.Active => state.DurationTicks,
                EnemyGlidePhase.Recovery => state.RecoveryTicks,
                _ => 0,
            };
            var untilTickExclusive = state.Phase switch
            {
                EnemyGlidePhase.Windup => state.WindupUntilTickExclusive,
                EnemyGlidePhase.Active => state.ActiveUntilTickExclusive,
                EnemyGlidePhase.Recovery => state.RecoveryUntilTickExclusive,
                _ => currentTickIndex,
            };
            var elapsedTicks = totalTicks > 0
                ? Math.Max(0, totalTicks - Math.Max(0, untilTickExclusive - currentTickIndex))
                : 0;
            var progress = totalTicks > 0
                ? Math.Max(0f, Math.Min(1f, elapsedTicks / (float)totalTicks))
                : 1f;
            return (elapsedTicks, Math.Max(0, totalTicks), progress);
        }

        private static int ResolveGlideHeightUnits(
            EnemyGlidePhase phase,
            float progress,
            int liftHeightUnits,
            int recoveryDipHeightUnits)
        {
            switch (phase)
            {
                case EnemyGlidePhase.Windup:
                    return LerpUnits(0, liftHeightUnits, progress);

                case EnemyGlidePhase.Active:
                    return liftHeightUnits;

                case EnemyGlidePhase.Recovery:
                    if (recoveryDipHeightUnits <= 0)
                    {
                        return LerpUnits(liftHeightUnits, 0, progress);
                    }

                    if (progress <= 0.5f)
                    {
                        return LerpUnits(liftHeightUnits, -recoveryDipHeightUnits, progress * 2f);
                    }

                    return LerpUnits(-recoveryDipHeightUnits, 0, (progress - 0.5f) * 2f);

                default:
                    return 0;
            }
        }

        private static int LerpUnits(int fromUnits, int toUnits, float progress)
        {
            var clampedProgress = Math.Max(0f, Math.Min(1f, progress));
            return (int)Math.Round(fromUnits + ((toUnits - fromUnits) * clampedProgress), MidpointRounding.AwayFromZero);
        }

        private static EnemyChargeRuntimeState ResolvePresentationChargeState(
            bool hasPreviousState,
            in EnemyChargeRuntimeState previousChargeState,
            bool hasPostMovementState,
            in EnemyChargeRuntimeState postMovementChargeState,
            bool hasFinalState,
            in EnemyChargeRuntimeState finalChargeState)
        {
            if (hasFinalState)
            {
                return finalChargeState;
            }

            if (hasPostMovementState)
            {
                return postMovementChargeState;
            }

            return hasPreviousState
                ? previousChargeState
                : default;
        }

        private static bool IsEnemyChargePresentationEligible(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetEntity(entityId, out var entity) &&
                   EntityRolePolicy.IsEnemyUnit(entity) &&
                   entity.boardPresence == EntityBoardPresence.Occupying &&
                   entity.hp > 0 &&
                   !entity.markedForDeath &&
                   entity.aiMode != EnemyAiMode.None &&
                   entity.aiMode != EnemyAiMode.Dead &&
                   entity.position.face == snapshot.Topology.BottomFace;
        }

        private static bool ShouldEmitChargeSignal(
            bool hasPreviousState,
            in EnemyChargeRuntimeState previousChargeState,
            bool hasPostMovementState,
            in EnemyChargeRuntimeState postMovementChargeState,
            bool hasFinalState,
            in EnemyChargeRuntimeState finalChargeState)
        {
            return (hasPreviousState && previousChargeState.phase != EnemyChargePhase.None) ||
                   (hasPostMovementState && postMovementChargeState.phase != EnemyChargePhase.None) ||
                   (hasFinalState && finalChargeState.phase != EnemyChargePhase.None);
        }

        private static EnemyJumpRuntimeState ResolvePresentationJumpState(
            bool hasPreviousState,
            in EnemyJumpRuntimeState previousJumpState,
            bool hasPostMovementState,
            in EnemyJumpRuntimeState postMovementJumpState,
            bool hasFinalState,
            in EnemyJumpRuntimeState finalJumpState)
        {
            if (hasFinalState)
            {
                return finalJumpState;
            }

            if (hasPostMovementState)
            {
                return postMovementJumpState;
            }

            return hasPreviousState
                ? previousJumpState
                : default;
        }

        private static bool ShouldEmitJumpSignal(
            bool hasPreviousState,
            in EnemyJumpRuntimeState previousJumpState,
            bool hasPostMovementState,
            in EnemyJumpRuntimeState postMovementJumpState,
            bool hasFinalState,
            in EnemyJumpRuntimeState finalJumpState)
        {
            return (hasPreviousState && previousJumpState.phase != EnemyJumpPhase.None) ||
                   (hasPostMovementState && postMovementJumpState.phase != EnemyJumpPhase.None) ||
                   (hasFinalState && finalJumpState.phase != EnemyJumpPhase.None);
        }

        private static Direction ResolveJumpPresentationFacing(
            in TickPresentationBuildContext context,
            int entityId,
            in EnemyJumpRuntimeState jumpState)
        {
            var fallbackFacing = ResolveJumpPresentationEntityFacing(context, entityId);
            if (jumpState.phase != EnemyJumpPhase.Windup)
            {
                return fallbackFacing;
            }

            return EnemyJumpQueries.ResolveJumpBasisFacing(
                jumpState.sourceCell,
                jumpState.lockedTargetCell,
                fallbackFacing == Direction.None ? Direction.Up : fallbackFacing,
                ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak);
        }

        private static Direction ResolveJumpPresentationEntityFacing(
            in TickPresentationBuildContext context,
            int entityId)
        {
            return TryResolveJumpPresentationEntity(context, entityId, out var entity)
                ? entity.facing
                : Direction.Up;
        }

        private static bool TryResolveJumpPresentationEntity(
            in TickPresentationBuildContext context,
            int entityId,
            out EntityState entity)
        {
            return context.FinalAuthoritativeSnapshot.TryGetEntity(entityId, out entity) ||
                   context.PostMovementSnapshot.TryGetEntity(entityId, out entity) ||
                   context.JumpBaselineSnapshot.TryGetEntity(entityId, out entity) ||
                   context.PreMovementSnapshot.TryGetEntity(entityId, out entity);
        }

        private static bool DidStartEnemyRecovery(
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            int entityId)
        {
            return postMovementSnapshot.TryGetEntity(entityId, out var beforeAttackEntity) &&
                   beforeAttackEntity.type == EntityType.Unit &&
                   beforeAttackEntity.aiMode == EnemyAiMode.Attack &&
                   postAttackSnapshot.TryGetEntity(entityId, out var afterAttackEntity) &&
                   afterAttackEntity.aiMode == EnemyAiMode.Recover;
        }

        private static bool IsEnemyUnit(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetEntity(entityId, out var entity) &&
                   entity.type == EntityType.Unit &&
                   entity.aiMode != EnemyAiMode.None;
        }

        private static TickTopologyTransitionFact ResolveTopologyTransitionFact(
            in TickPresentationBuildContext context)
        {
            var sourceTopology = context.PreMovementSnapshot.Topology;
            var destinationTopology = context.PostMovementSnapshot.Topology;
            if (!sourceTopology.Equals(destinationTopology))
            {
                return new TickTopologyTransitionFact(
                    hasTransition: true,
                    sourceTopology,
                    destinationTopology,
                    ResolveRotationKind(
                        context.MovementPhaseResult.ResolvedOperations,
                        sourceTopology,
                        destinationTopology),
                    TickTopologyTransitionSource.SnapshotDiff);
            }

            if (!TryResolveFree2DTopologyTransitionMetadata(
                    context.MovementPhaseResult,
                    out var metadata))
            {
                return default;
            }

            sourceTopology = ResolveFree2DTopologyTransitionSourceTopology(
                destinationTopology,
                metadata.RotationKind);
            if (sourceTopology.Equals(destinationTopology))
            {
                return default;
            }

            return new TickTopologyTransitionFact(
                hasTransition: true,
                sourceTopology,
                destinationTopology,
                metadata.RotationKind,
                TickTopologyTransitionSource.Free2DNative);
        }

        private static TickTopologyMotion? BuildTopologyMotion(
            in TickPresentationBuildContext context,
            in TickTopologyTransitionFact topologyFact)
        {
            if (!topologyFact.HasTransition)
            {
                return BuildRespawnTopologyMotion(context);
            }

            return new TickTopologyMotion(
                topologyFact.SourceTopology,
                topologyFact.DestinationTopology,
                topologyFact.RotationKind);
        }

        private static bool TryResolveFree2DTopologyTransitionMetadata(
            MovementPhaseResult movementPhaseResult,
            out FinalizationOperationMetadata metadata)
        {
            var operations = movementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                    operation.Metadata.RotationKind != CubeRotationKind.None)
                {
                    metadata = operation.Metadata;
                    return true;
                }
            }

            metadata = default;
            return false;
        }

        private static TickTopologyMotion? BuildRespawnTopologyMotion(in TickPresentationBuildContext context)
        {
            var sourceTopology = context.PostAttackSnapshot.Topology;
            var destinationTopology = context.FinalAuthoritativeSnapshot.Topology;
            if (sourceTopology.Equals(destinationTopology))
            {
                return null;
            }

            if (context.RespawnPhaseResult.TopologyResetRequest.HasValue)
            {
                var topologyResetRequest = context.RespawnPhaseResult.TopologyResetRequest.Value;
                return new TickTopologyMotion(
                    topologyResetRequest.SourceTopology,
                    topologyResetRequest.DestinationTopology,
                    topologyResetRequest.RotationKind);
            }

            return new TickTopologyMotion(
                sourceTopology,
                destinationTopology,
                ResolveRotationKind(Array.Empty<FinalizationOperation>(), sourceTopology, destinationTopology));
        }

        private static void BuildTransitionVisibilityPresentation(
            in TickPresentationBuildContext context,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals,
            List<TickTransitionVisibilityChange> transitionVisibilityChanges)
        {
            var sourceTopology = context.PreMovementSnapshot.Topology;
            var destinationTopology = context.FinalAuthoritativeSnapshot.Topology;
            if (sourceTopology.Equals(destinationTopology))
            {
                return;
            }

            var excludedEntityIds = CollectTransitionVisibilityExcludedEntityIds(
                context.MovementPhaseResult.ResolvedOperations,
                visibilityChanges,
                entityExitSignals);
            var finalEntities = new List<EntityState>();
            context.FinalAuthoritativeSnapshot.EnumerateEntitiesOrdered(finalEntities);

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (excludedEntityIds.Contains(entity.entityId) ||
                    !context.FinalAuthoritativeSnapshot.TryGetResolvedSpatialState(entity.entityId, out var destinationSpatialState) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(destinationSpatialState))
                {
                    continue;
                }

                if (context.PreMovementSnapshot.TryGetEntity(entity.entityId, out var sourceEntity) &&
                    context.PreMovementSnapshot.TryGetResolvedSpatialState(sourceEntity.entityId, out var sourceSpatialState) &&
                    GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(sourceSpatialState))
                {
                    continue;
                }

                transitionVisibilityChanges.Add(
                    new TickTransitionVisibilityChange(
                        entity.entityId,
                        TickTransitionVisibilityMode.ShowAtTransitionStart,
                        entity.position,
                        destinationTopology,
                        entity.facing));
            }
        }

        private static void AppendEntityMotion(
            in TickPresentationBuildContext context,
            FinalizationOperation operation,
            List<TickEntityMotion> entityMotions)
        {
            if (operation.Kind != FinalizationOperationKind.MoveEntity ||
                ShouldSuppressLegacyMotionForLocomotion(operation) ||
                !TryResolveMotionKind(
                    context,
                    operation,
                    operation.Metadata.MovementSemanticKind,
                    out var motionKind) ||
                !context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity) ||
                !context.PostMovementSnapshot.TryGetEntity(operation.EntityId, out var destinationEntity) ||
                destinationEntity.boardPresence != EntityBoardPresence.Occupying)
            {
                return;
            }

            entityMotions.Add(
                new TickEntityMotion(
                    operation.EntityId,
                    motionKind,
                    sourceEntity.position,
                    operation.Destination,
                    context.PreMovementSnapshot.Topology,
                    context.PostMovementSnapshot.Topology,
                    sourceEntity.facing,
                    destinationEntity.facing));
        }

        private static bool ShouldSuppressLegacyMotionForLocomotion(FinalizationOperation operation)
        {
            if (operation.Kind != FinalizationOperationKind.MoveEntity)
            {
                return false;
            }

            // Ownership narrowing: only locomotion replacement boundaries suppress the legacy
            // entity Move presentation. Retained grid/generic transactions still own Move.
            return operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LocomotionAnchorCommit ||
                   operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.UnitOrdinaryLocomotion ||
                   operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition;
        }

        private static bool TryResolveMotionKind(
            in TickPresentationBuildContext context,
            FinalizationOperation operation,
            MovementSemanticKind semanticKind,
            out TickEntityMotionKind motionKind)
        {
            motionKind = semanticKind switch
            {
                MovementSemanticKind.Move => TickEntityMotionKind.Move,
                // Item movement intentionally shares generic Move presentation.
                MovementSemanticKind.Item => TickEntityMotionKind.Move,
                MovementSemanticKind.Push => TickEntityMotionKind.Push,
                MovementSemanticKind.Flip => TickEntityMotionKind.Flip,
                MovementSemanticKind.Slide => TickEntityMotionKind.BoxSlide,
                MovementSemanticKind.ProjectileMove => TickEntityMotionKind.ProjectileMove,
                _ => TickEntityMotionKind.None,
            };

            return motionKind != TickEntityMotionKind.None;
        }

        private static bool DidGeneratePlayerMoveMotionThisTick(
            in TickPresentationBuildContext context,
            int entityId)
        {
            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    !ShouldSuppressLegacyMotionForLocomotion(operation) &&
                    TryResolveMotionKind(context, operation, operation.Metadata.MovementSemanticKind, out var motionKind) &&
                    motionKind == TickEntityMotionKind.Move)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldWaitForNextMoveCadence(
            in TickPresentationBuildContext context,
            in PlayerControlState controlState)
        {
            return context.PlayerCommand.MoveDirection != Direction.None &&
                   !context.PlayerCommand.PushPressed &&
                   !context.PlayerCommand.FlipPressed &&
                   !context.PlayerCommand.IsMoveBuffered &&
                   !controlState.activeAction.IsActive &&
                   PlayerControlQueries.IsMoveOnCooldown(controlState, context.CurrentTickIndex);
        }

        private static HashSet<int> CollectTransitionVisibilityExcludedEntityIds(
            IReadOnlyList<FinalizationOperation> movementOperations,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals)
        {
            var excludedEntityIds = new HashSet<int>();

            for (var i = 0; i < movementOperations.Count; i++)
            {
                if (movementOperations[i].Kind == FinalizationOperationKind.MoveEntity)
                {
                    excludedEntityIds.Add(movementOperations[i].EntityId);
                }
            }

            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                excludedEntityIds.Add(visibilityChanges[i].EntityId);
            }

            for (var i = 0; i < entityExitSignals.Count; i++)
            {
                excludedEntityIds.Add(entityExitSignals[i].ExitedEntityId);
            }

            return excludedEntityIds;
        }

        private static bool TryFindAttackDestroyOperation(
            IReadOnlyList<FinalizationOperation> operations,
            int targetEntityId,
            out FinalizationOperation destroyOperation)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MarkDestroy &&
                    operation.EntityId == targetEntityId &&
                    operation.Metadata.ExitCauseHint != TickEntityExitCause.None)
                {
                    destroyOperation = operation;
                    return true;
                }
            }

            destroyOperation = default;
            return false;
        }

        private static bool TryFindJumpPresentationOperation(
            IReadOnlyList<FinalizationOperation> operations,
            int entityId,
            out FinalizationOperation jumpOperation)
        {
            for (var i = operations.Count - 1; i >= 0; i--)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.SetEnemyJumpState &&
                    operation.EntityId == entityId &&
                    operation.Metadata.JumpPresentationKind != JumpPresentationKind.None)
                {
                    jumpOperation = operation;
                    return true;
                }
            }

            jumpOperation = default;
            return false;
        }

        private static int BuildStablePresentationSeed(
            int currentTickIndex,
            int exitedEntityId,
            int sourceActorEntityId,
            TickEntityExitCause exitCause)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = MixStableSeed(hash, currentTickIndex);
                hash = MixStableSeed(hash, exitedEntityId);
                hash = MixStableSeed(hash, sourceActorEntityId);
                hash = MixStableSeed(hash, (int)exitCause);
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static int BuildStableImpactPresentationSeed(
            int currentTickIndex,
            int sourceEntityId,
            int targetEntityId)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = MixStableSeed(hash, currentTickIndex);
                hash = MixStableSeed(hash, sourceEntityId);
                hash = MixStableSeed(hash, targetEntityId);
                hash = MixStableSeed(hash, (int)TickEntityExitCause.DestroyedByImpact);
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static uint MixStableSeed(uint hash, int value)
        {
            unchecked
            {
                return (hash ^ (uint)value) * 16777619u;
            }
        }

        private static CubeRotationKind ResolveRotationKind(
            IReadOnlyList<FinalizationOperation> movementOperations,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology)
        {
            for (var i = 0; i < movementOperations.Count; i++)
            {
                var operation = movementOperations[i];
                if (operation.Kind == FinalizationOperationKind.SetTopology &&
                    operation.Topology.Equals(destinationTopology) &&
                    operation.Metadata.RotationKind != CubeRotationKind.None)
                {
                    return operation.Metadata.RotationKind;
                }
            }

            if (FaceIdUtility.GetNext(sourceTopology.BottomFace) == destinationTopology.BottomFace)
            {
                return CubeRotationKind.Forward;
            }

            if (FaceIdUtility.GetPrevious(sourceTopology.BottomFace) == destinationTopology.BottomFace)
            {
                return CubeRotationKind.Backward;
            }

            return CubeRotationKind.None;
        }
    }
}
