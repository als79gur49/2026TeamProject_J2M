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
            in TickPresentationBuildContext presentationBuildContext)
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
                movementPhaseResult.CommitEvents.Count +
                attackPhaseResult.EventLogEntries.Count +
                cleanupPhaseResult.RemovedEntityIds.Count +
                cleanupPhaseResult.TimerChanges.Count +
                cleanupPhaseResult.StateTransitions.Count +
                cleanupPhaseResult.EventLogEntries.Count +
                respawnPhaseResult.EventLogEntries.Count);

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
            Array.Empty<string>());

        private readonly ReadOnlyCollection<string> _eventLogEntries;
        private readonly ReadOnlyCollection<EntityState> _respawnedEntities;

        public RespawnPhaseResult(
            IEnumerable<EntityState> respawnedEntities,
            IEnumerable<string> eventLogEntries,
            RespawnTopologyResetRequest? topologyResetRequest = null)
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
            TopologyResetRequest = topologyResetRequest;
        }

        public IReadOnlyList<EntityState> RespawnedEntities => _respawnedEntities;

        public IReadOnlyList<string> EventLogEntries => _eventLogEntries;

        public RespawnTopologyResetRequest? TopologyResetRequest { get; }
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
            IReadOnlyList<ResolutionRecord> resolutionRecords = null)
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
                resolutionRecords)
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
            IReadOnlyList<ResolutionRecord> resolutionRecords = null)
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
                resolutionRecords)
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
            IReadOnlyList<ResolutionRecord> resolutionRecords = null)
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
                resolutionRecords)
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
            IReadOnlyList<ResolutionRecord> resolutionRecords = null)
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
            CurrentTickIndex = currentTickIndex;
            JumpBaselineSnapshot = jumpBaselineSnapshot ?? PreMovementSnapshot;
            PlayerCommand = playerCommand;
            ResolutionRecords = resolutionRecords ?? Array.Empty<ResolutionRecord>();
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

        public int CurrentTickIndex { get; }

        public WorldSnapshot JumpBaselineSnapshot { get; }

        public PlayerTickCommand PlayerCommand { get; }

        public IReadOnlyList<ResolutionRecord> ResolutionRecords { get; }
    }

    internal sealed class TickPresentationDataBuilder
    {
        public TickPresentationData Build(in TickPresentationBuildContext context)
        {
            var entityMotions = new List<TickEntityMotion>();
            var entityExitSignals = new List<TickEntityExitPresentationSignal>();
            var impactTransientSignals = new List<TickImpactTransientPresentationSignal>();
            var flipImpactSignals = new List<FlipImpactPresentationSignal>();
            var enemyActionSignals = new List<TickEnemyActionPresentationSignal>();
            var enemyDamageSignals = new List<TickEnemyDamagePresentationSignal>();
            var enemyJumpSignals = new List<TickEnemyJumpPresentationSignal>();
            var enemyChargeSignals = new List<TickEnemyChargePresentationSignal>();
            var frontFaceShieldSourceSignals = new List<TickFrontFaceShieldSourceSignal>();
            var frontFaceShieldBlockSignals = new List<TickFrontFaceShieldBlockSignal>();
            var summonWindupWarnings = new List<TickSummonWindupWarningSignal>();
            var frontFaceShieldWindupWarnings = new List<TickFrontFaceShieldWindupWarningSignal>();
            var playerActionSignals = new List<TickPlayerActionPresentationSignal>();
            var playerDamageSignals = new List<TickPlayerDamagePresentationSignal>();
            var playerDeathSignals = new List<TickPlayerDeathPresentationSignal>();
            var playerLocomotionSignals = new List<TickPlayerLocomotionPresentationSignal>();
            var summonedEnemyPresentationBindings = new List<TickSummonedEnemyPresentationBinding>();
            var visibilityChanges = new List<TickVisibilityChange>();
            var transitionVisibilityChanges = new List<TickTransitionVisibilityChange>();
            var exitOwnedEntityIds = new HashSet<int>();

            BuildEntityExitPresentation(context, entityExitSignals, exitOwnedEntityIds);
            BuildFlipImpactPresentation(context, flipImpactSignals);
            BuildImpactTransientPresentation(context, impactTransientSignals);
            BuildMovementPresentation(context, entityMotions, visibilityChanges, exitOwnedEntityIds);
            BuildAttackPresentation(context, visibilityChanges);
            BuildCleanupPresentation(context, visibilityChanges, exitOwnedEntityIds);
            BuildRespawnPresentation(context, visibilityChanges);
            BuildPlayerPresentation(context, playerActionSignals);
            BuildPlayerDamagePresentation(context, playerDamageSignals);
            BuildPlayerDeathPresentation(context, playerDeathSignals);
            BuildPlayerLocomotionPresentation(context, playerLocomotionSignals);
            BuildEnemyDamagePresentation(context, enemyDamageSignals);
            BuildEnemyPresentation(context, enemyActionSignals);
            BuildEnemyJumpPresentation(context, enemyJumpSignals);
            BuildEnemyChargePresentation(context, enemyChargeSignals);
            BuildFrontFaceShieldPresentation(context, frontFaceShieldSourceSignals, frontFaceShieldBlockSignals);
            BuildEnemyUtilityWindupPresentation(context, summonWindupWarnings, frontFaceShieldWindupWarnings);
            BuildSummonedEnemyPresentationBindings(context, summonedEnemyPresentationBindings);

            var topologyMotion = BuildTopologyMotion(context);
            BuildTransitionVisibilityPresentation(context, visibilityChanges, entityExitSignals, transitionVisibilityChanges);

            return entityMotions.Count == 0 &&
                   enemyActionSignals.Count == 0 &&
                   enemyDamageSignals.Count == 0 &&
                   enemyJumpSignals.Count == 0 &&
                   enemyChargeSignals.Count == 0 &&
                   frontFaceShieldSourceSignals.Count == 0 &&
                   frontFaceShieldBlockSignals.Count == 0 &&
                   summonWindupWarnings.Count == 0 &&
                   frontFaceShieldWindupWarnings.Count == 0 &&
                   entityExitSignals.Count == 0 &&
                   impactTransientSignals.Count == 0 &&
                   flipImpactSignals.Count == 0 &&
                   playerActionSignals.Count == 0 &&
                   playerDamageSignals.Count == 0 &&
                   playerDeathSignals.Count == 0 &&
                   playerLocomotionSignals.Count == 0 &&
                   summonedEnemyPresentationBindings.Count == 0 &&
                   visibilityChanges.Count == 0 &&
                   transitionVisibilityChanges.Count == 0 &&
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
                    frontFaceShieldWindupWarnings);
        }

        private static void BuildEnemyUtilityWindupPresentation(
            in TickPresentationBuildContext context,
            List<TickSummonWindupWarningSignal> summonWindupWarnings,
            List<TickFrontFaceShieldWindupWarningSignal> frontFaceShieldWindupWarnings)
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
                    if (effectState.phase != EnemyUtilityEffectPhase.Windup)
                    {
                        continue;
                    }

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
            // Exit-owned removals bypass generic detach/remove visibility tracks. The
            // authoritative entity view disappears immediately; only transient echoes linger.
            var signaledEntityIds = new HashSet<int>();
            BuildMovementOwnedExitPresentation(
                context,
                entityExitSignals,
                exitOwnedEntityIds,
                signaledEntityIds);
            BuildAttackOwnedExitPresentation(
                context,
                entityExitSignals,
                exitOwnedEntityIds,
                signaledEntityIds);
        }

        private static void BuildMovementOwnedExitPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityExitPresentationSignal> entityExitSignals,
            ISet<int> exitOwnedEntityIds,
            ISet<int> signaledEntityIds)
        {
            var removedEntityIds = new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds);
            var operations = context.MovementPhaseResult.ResolvedOperations;

            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Metadata.ExitCauseHint == TickEntityExitCause.None ||
                    !removedEntityIds.Contains(operation.EntityId) ||
                    !signaledEntityIds.Add(operation.EntityId) ||
                    !context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity))
                {
                    continue;
                }

                var isExitSignal =
                    (operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                     operation.BoardPresence == EntityBoardPresence.Detached) ||
                    operation.Kind == FinalizationOperationKind.MarkDestroy;
                if (!isExitSignal)
                {
                    signaledEntityIds.Remove(operation.EntityId);
                    continue;
                }

                var exitCause = operation.Metadata.ExitCauseHint;
                entityExitSignals.Add(
                    new TickEntityExitPresentationSignal(
                        operation.EntityId,
                        exitCause,
                        sourceEntity.position,
                        context.PreMovementSnapshot.Topology,
                        sourceEntity.facing,
                        sourceEntity.type,
                        sourceActorEntityId: operation.Metadata.SourceActorEntityId,
                        presentationSeed: BuildStablePresentationSeed(
                            context.CurrentTickIndex,
                            operation.EntityId,
                            operation.Metadata.SourceActorEntityId,
                            exitCause)));
                exitOwnedEntityIds.Add(operation.EntityId);
            }
        }

        private static void BuildAttackOwnedExitPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityExitPresentationSignal> entityExitSignals,
            ISet<int> exitOwnedEntityIds,
            ISet<int> signaledEntityIds)
        {
            var removedEntityIds = context.CleanupPhaseResult.RemovedEntityIds;
            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                var entityId = removedEntityIds[i];
                if (exitOwnedEntityIds.Contains(entityId) ||
                    !signaledEntityIds.Add(entityId) ||
                    !context.PostAttackSnapshot.TryGetEntity(entityId, out var removedEntity) ||
                    !IsEnemyUnit(context.PostAttackSnapshot, entityId) ||
                    !TryFindAttackDestroyOperation(context.AttackPhaseResult.ResolvedOperations, entityId, out var destroyOperation))
                {
                    continue;
                }

                entityExitSignals.Add(
                    new TickEntityExitPresentationSignal(
                        entityId,
                        destroyOperation.Metadata.ExitCauseHint,
                        removedEntity.position,
                        context.PostAttackSnapshot.Topology,
                        removedEntity.facing,
                        removedEntity.type,
                        sourceActorEntityId: destroyOperation.Metadata.SourceActorEntityId,
                        presentationSeed: BuildStablePresentationSeed(
                            context.CurrentTickIndex,
                            entityId,
                            destroyOperation.Metadata.SourceActorEntityId,
                            destroyOperation.Metadata.ExitCauseHint)));
                exitOwnedEntityIds.Add(entityId);
            }
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
            List<TickVisibilityChange> visibilityChanges)
        {
            var respawnedEntities = context.RespawnPhaseResult.RespawnedEntities;

            for (var i = 0; i < respawnedEntities.Count; i++)
            {
                var entity = respawnedEntities[i];
                visibilityChanges.Add(
                    new TickVisibilityChange(
                        entity.entityId,
                        TickVisibilityChangeKind.Spawn,
                        entity.position,
                        context.FinalAuthoritativeSnapshot.Topology,
                        entity.facing));
            }
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
                        hasBinding ? bindingState.ArchetypeId : EnemyUnitArchetypeId.None));
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
                    !context.PlayerCommand.FlipPressed &&
                    (moveMotionGeneratedThisTick || waitingForNextMoveCadence);

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

            if (fatalSignalsByPlayerId.Count == 0)
            {
                return;
            }

            var removedPlayerIds = context.CleanupPhaseResult.RemovedEntityIds.Count > 0
                ? new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds)
                : null;
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

                var facing = ResolveJumpPresentationFacing(context, entityId);
                var remainingAirborneTicks = resolvedState.phase == EnemyJumpPhase.Airborne
                    ? Math.Max(0, resolvedState.landingTick - context.CurrentTickIndex)
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
            var preMovementEntries = new List<EnemyChargeSnapshotEntry>();
            var postMovementEntries = new List<EnemyChargeSnapshotEntry>();
            var finalEntries = new List<EnemyChargeSnapshotEntry>();

            context.PreMovementSnapshot.EnumerateEnemyChargeStatesOrdered(preMovementEntries);
            context.PostMovementSnapshot.EnumerateEnemyChargeStatesOrdered(postMovementEntries);
            context.FinalAuthoritativeSnapshot.EnumerateEnemyChargeStatesOrdered(finalEntries);

            CollectEnemyChargeCandidateIds(preMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyChargeCandidateIds(postMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyChargeCandidateIds(finalEntries, seenEntityIds, candidateEntityIds);

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                var hasPreviousState = context.PreMovementSnapshot.TryGetEnemyChargeState(entityId, out var previousChargeState);
                var hasPostMovementState = context.PostMovementSnapshot.TryGetEnemyChargeState(entityId, out var postMovementChargeState);
                var hasFinalState = context.FinalAuthoritativeSnapshot.TryGetEnemyChargeState(entityId, out var finalChargeState);

                var resolvedState = ResolvePresentationChargeState(
                    hasPreviousState,
                    previousChargeState,
                    hasPostMovementState,
                    postMovementChargeState,
                    hasFinalState,
                    finalChargeState);
                if (!ShouldEmitChargeSignal(
                        hasPreviousState,
                        previousChargeState,
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
                                               (!hasPreviousState || previousChargeState.phase != EnemyChargePhase.Windup),
                        startedActiveThisTick: resolvedState.phase == EnemyChargePhase.Active &&
                                               (!hasPreviousState || previousChargeState.phase != EnemyChargePhase.Active),
                        startedRecoverThisTick: resolvedState.phase == EnemyChargePhase.Recover &&
                                                (!hasPreviousState || previousChargeState.phase != EnemyChargePhase.Recover),
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

        private static TickTopologyMotion? BuildTopologyMotion(in TickPresentationBuildContext context)
        {
            var sourceTopology = context.PreMovementSnapshot.Topology;
            var destinationTopology = context.PostMovementSnapshot.Topology;
            if (sourceTopology.Equals(destinationTopology))
            {
                return BuildRespawnTopologyMotion(context);
            }

            return new TickTopologyMotion(
                sourceTopology,
                destinationTopology,
                ResolveRotationKind(
                    context.MovementPhaseResult.ResolvedOperations,
                    sourceTopology,
                    destinationTopology));
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

        private static bool TryResolveMotionKind(
            in TickPresentationBuildContext context,
            FinalizationOperation operation,
            MovementSemanticKind semanticKind,
            out TickEntityMotionKind motionKind)
        {
            motionKind = semanticKind switch
            {
                MovementSemanticKind.Move => TickEntityMotionKind.Move,
                MovementSemanticKind.Item => TickEntityMotionKind.Move,
                MovementSemanticKind.Push => TickEntityMotionKind.Push,
                MovementSemanticKind.Flip => TickEntityMotionKind.Flip,
                MovementSemanticKind.Slide => TickEntityMotionKind.BoxSlide,
                MovementSemanticKind.ProjectileMove => TickEntityMotionKind.ProjectileMove,
                _ => TickEntityMotionKind.None,
            };

            if (motionKind == TickEntityMotionKind.Move &&
                semanticKind == MovementSemanticKind.Move &&
                ShouldUseChargeMovePresentation(context, operation))
            {
                motionKind = TickEntityMotionKind.ChargeMove;
            }

            return motionKind != TickEntityMotionKind.None;
        }

        private static bool ShouldUseChargeMovePresentation(
            in TickPresentationBuildContext context,
            FinalizationOperation operation)
        {
            // ChargeMove is presentation-only and must be tied to an actual committed move op.
            // Active charge ticks without movement (cooldown pause, blocked, recover) never route here.
            return IsEnemyUnit(context.PostMovementSnapshot, operation.EntityId) &&
                   context.PostMovementSnapshot.TryGetEnemyChargeState(operation.EntityId, out var chargeState) &&
                   chargeState.phase == EnemyChargePhase.Active;
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
