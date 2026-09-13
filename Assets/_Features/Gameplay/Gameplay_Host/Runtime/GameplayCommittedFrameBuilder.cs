using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayCommittedFrameBuilder
    {
        private static readonly bool EnableUnitPresentationPlaneOffsets = false;
        private const float UnitPresentationOffsetRadiusInCells = 0.2f;
        private const float UnitPresentationSquareHalfExtentInCells = 0.14f;

        private readonly GameplayAnimationSyncCoordinator _animationSync;
        private readonly GameplayPoseResolver _poseResolver;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly List<PresentableEntityTarget> _presentableTargetsBuffer = new();
        private readonly List<EntityState> _orderedEntitiesBuffer = new();
        private readonly HashSet<int> _duplicateEntityIdsBuffer = new();
        private readonly HashSet<int> _inputEntityIdsBuffer = new();
        private readonly HashSet<int> _removedOrExitedEntityIdsBuffer = new();
        private readonly HashSet<int> _spawnedEntityIdsBuffer = new();
        private readonly Dictionary<int, StaticWallCommittedTarget> _staticWallCommittedTargets = new();
        private readonly Dictionary<int, StaticWallCandidate> _staticWallCandidates = new();
        private readonly List<int> _missingCandidateIdsBuffer = new();
        private StageStaticWallPresentationProvenance _provenance = StageStaticWallPresentationProvenance.Empty;
        private long _sessionId;
        private int _frameOrdinal;
        private int _topologyRevision;
        private int _projectorProfileRevision;
        private CubeTopologyState _observedTopology;
        private bool _hasObservedTopology;
        private GameplayCubeProjector _observedProjector;
        private StaticWallTargetInvalidation _pendingGlobalInvalidation;
        private StaticWallTargetInvalidation _frameGlobalInvalidation;
        private int _staticWallCommittedTargetHighWaterMark;

        public GameplayCommittedFrameBuilder(
            GameplayPresentationStateStore stateStore,
            GameplayPoseResolver poseResolver,
            GameplayAnimationSyncCoordinator animationSync)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _poseResolver = poseResolver ?? throw new ArgumentNullException(nameof(poseResolver));
            _animationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));
        }

        internal void ResetSession(
            StageStaticWallPresentationProvenance provenance,
            GameplayCubeProjector projector)
        {
            var previousStaticRevision = _provenance.StaticRevision;
            var hadPreviousSession = _sessionId > 0;
            _provenance = provenance ?? StageStaticWallPresentationProvenance.Empty;
            _sessionId++;
            _frameOrdinal = -1;
            _topologyRevision = 0;
            _projectorProfileRevision = 1;
            _hasObservedTopology = false;
            _observedProjector = projector;
            _frameGlobalInvalidation = StaticWallTargetInvalidation.None;
            _pendingGlobalInvalidation = StaticWallTargetInvalidation.Session;
            if (hadPreviousSession && !string.Equals(
                    previousStaticRevision,
                    _provenance.StaticRevision,
                    StringComparison.Ordinal))
            {
                _pendingGlobalInvalidation |= StaticWallTargetInvalidation.StaticRevision;
            }

            _staticWallCommittedTargets.Clear();
            _staticWallCommittedTargetHighWaterMark = 0;
            _staticWallCandidates.Clear();
            _presentableTargetsBuffer.Clear();
            _orderedEntitiesBuffer.Clear();
            _duplicateEntityIdsBuffer.Clear();
            _inputEntityIdsBuffer.Clear();
            _removedOrExitedEntityIdsBuffer.Clear();
            _spawnedEntityIdsBuffer.Clear();
            foreach (var entityId in _provenance.EntityIds)
            {
                if (_provenance.TryGetEntry(entityId, out var entry))
                {
                    if (entry.SourceKind != StageStaticWallProvenanceSourceKind.StageAuthoredStaticWall ||
                        entry.InitialWall.type != EntityType.Wall)
                    {
                        throw new InvalidOperationException(
                            $"Static Wall provenance {entityId} does not carry an authored Wall source and normalized Wall entity.");
                    }

                    _staticWallCandidates.Add(entityId, new StaticWallCandidate(entry));
                }
            }
        }

        public void StoreCommittedFrame(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            GameplayCubeProjector projector,
            GameplayEntityViewBinder viewBinder,
            Action<CubeTopologyState> topologyCommitted,
            TickPresentationData presentationData = null,
            int tickIndex = -1,
            CommittedFrameStoreReason reason = CommittedFrameStoreReason.Tick)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            StoreCommittedEntityTargets(
                entities,
                topology,
                projector,
                viewBinder,
                presentationData,
                tickIndex,
                reason);
            _stateStore.HasAnyCommittedFrame = true;
            topologyCommitted?.Invoke(_stateStore.CommittedTopology);
        }

        public GameplayProjectedFaceSlot? ResolveProjectedSlot(
            int entityId,
            bool isCommittedVisible,
            bool isTransitionVisible,
            TransitionVisibilityState transitionVisibilityState)
        {
            if (isCommittedVisible &&
                _stateStore.CommittedProjectedSlotsByEntityId.TryGetValue(entityId, out var committedSlot))
            {
                return committedSlot;
            }

            return isTransitionVisible
                ? transitionVisibilityState.ProjectedSlot
                : null;
        }

        private void StoreCommittedEntityTargets(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            GameplayCubeProjector projector,
            GameplayEntityViewBinder viewBinder,
            TickPresentationData presentationData,
            int tickIndex,
            CommittedFrameStoreReason reason)
        {
            _frameOrdinal++;
            _frameGlobalInvalidation = _pendingGlobalInvalidation;
            _pendingGlobalInvalidation = StaticWallTargetInvalidation.None;
            ObserveProjector(projector);
            ObserveTopology(topology);
            ObserveLifecycle(presentationData);
            _stateStore.BeginCommittedFrame(topology);
            _presentableTargetsBuffer.Clear();
            _inputEntityIdsBuffer.Clear();
            BuildOrderedEntities(entities, _orderedEntitiesBuffer, _duplicateEntityIdsBuffer);
            var presentableTargets = _presentableTargetsBuffer;
            var orderedEntities = _orderedEntitiesBuffer;
            _missingCandidateIdsBuffer.Clear();
            _missingCandidateIdsBuffer.AddRange(_duplicateEntityIdsBuffer);
            _missingCandidateIdsBuffer.Sort();
            for (var duplicateIndex = 0; duplicateIndex < _missingCandidateIdsBuffer.Count; duplicateIndex++)
            {
                var duplicateEntityId = _missingCandidateIdsBuffer[duplicateIndex];
                var duplicateCandidate = ResolveCandidate(duplicateEntityId);
                if (duplicateCandidate != null && !duplicateCandidate.Retired)
                {
                    duplicateCandidate.Generation++;
                    Retire(duplicateCandidate, StaticWallTargetRetireReason.UnknownGeneration);
                }
            }

            var diagnosticsEnabled = GameplayCommittedFrameDiagnostics.IsEnabled;
            var diagnostics = diagnosticsEnabled ? new FrameDiagnosticsAccumulator() : null;

            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var entity = orderedEntities[i];
                _inputEntityIdsBuffer.Add(entity.entityId);
                var candidate = ResolveCandidate(entity.entityId);
                var isProvenanceCandidate = candidate != null;
                var retireReason = StaticWallTargetRetireReason.None;
                var invalidations = candidate != null
                    ? _frameGlobalInvalidation
                    : StaticWallTargetInvalidation.None;
                if (candidate != null && !candidate.Retired)
                {
                    if (!StageStaticWallProvenanceSignature.SemanticallyEquals(candidate.InitialWall, entity))
                    {
                        candidate.Generation++;
                        Retire(candidate, StaticWallTargetRetireReason.SignatureChanged);
                    }
                }

                if (candidate != null && candidate.RetiredFrameOrdinal == _frameOrdinal)
                {
                    retireReason = candidate.RetireReason;
                    invalidations |= retireReason == StaticWallTargetRetireReason.SignatureChanged
                        ? StaticWallTargetInvalidation.Signature
                        : StaticWallTargetInvalidation.Generation;
                }

                var isAdmittedStaticWall = candidate != null && !candidate.Retired;
                var isPolicyPresentable = ShouldPresent(entity, topology);
                var row = diagnosticsEnabled
                    ? new EntityObservationAccumulator(
                        _sessionId,
                        _frameOrdinal,
                        entity.entityId,
                        StageStaticWallProvenanceSignature.CanonicalizeEntityState(entity),
                        isProvenanceCandidate,
                        candidate?.EntrySignature ?? string.Empty,
                        candidate?.Generation ?? -1,
                        _provenance.StaticRevision,
                        _topologyRevision,
                        _projectorProfileRevision,
                        isAdmittedStaticWall,
                        isPolicyPresentable,
                        retireReason,
                        invalidations)
                    : null;
                if (diagnosticsEnabled)
                {
                    diagnostics.EntityObservations.Add(row);
                }

                _stateStore.EntityTypesByEntityId[entity.entityId] = entity.type;
                _stateStore.UnitRolesByEntityId[entity.entityId] = entity.unitRole;
                _stateStore.EnemyAiModesByEntityId[entity.entityId] = entity.aiMode;

                if (!isPolicyPresentable)
                {
                    continue;
                }

                var cachedTarget = default(StaticWallCommittedTarget);
                var hasCachedTarget = isAdmittedStaticWall &&
                                      TryResolveStaticWallTarget(candidate, out cachedTarget);
                var projectedPose = default(ProjectedCellPose);
                if (!hasCachedTarget)
                {
                    if (diagnosticsEnabled)
                    {
                        row.DidRunProjection = true;
                    }

                    if (!projector.TryProjectEntityCell(entity.position, topology, entity.type, out projectedPose))
                    {
                        continue;
                    }
                }

                if (diagnosticsEnabled)
                {
                    row.DidRunViewResolve = true;
                }
                var view = viewBinder.ResolveOrCreate(entity);
                if (view == null)
                {
                    continue;
                }

                if (diagnosticsEnabled)
                {
                    row.ViewResolved = true;
                }

                _stateStore.ViewsByEntityId[entity.entityId] = view;
                _animationSync.CacheDrivers(entity.entityId, view);
                if (hasCachedTarget)
                {
                    if (diagnosticsEnabled)
                    {
                        row.SelectionKind = StaticWallTargetSelectionKind.Hit;
                    }

                    presentableTargets.Add(new PresentableEntityTarget(entity, cachedTarget, row));
                    continue;
                }

                if (diagnosticsEnabled)
                {
                    row.SelectionKind = isAdmittedStaticWall
                        ? StaticWallTargetSelectionKind.Rebuild
                        : StaticWallTargetSelectionKind.FallbackWithoutCache;
                    row.DidUseFallbackValuePath = !isAdmittedStaticWall;
                }

                presentableTargets.Add(new PresentableEntityTarget(
                    entity,
                    projectedPose,
                    candidate,
                    isAdmittedStaticWall,
                    row));
            }

            RecordMissingCandidates(_inputEntityIdsBuffer, diagnostics);

            Dictionary<int, Vector2> unitPresentationPlaneOffsetsByEntityId = null;
            if (EnableUnitPresentationPlaneOffsets)
            {
                var stackedUnitEntityIdsByCell = new Dictionary<SurfaceCell, List<int>>();
                for (var i = 0; i < presentableTargets.Count; i++)
                {
                    var target = presentableTargets[i];
                    if (target.Entity.type != EntityType.Unit)
                    {
                        continue;
                    }

                    if (!stackedUnitEntityIdsByCell.TryGetValue(target.Entity.position, out var stackedEntityIds))
                    {
                        stackedEntityIds = new List<int>();
                        stackedUnitEntityIdsByCell[target.Entity.position] = stackedEntityIds;
                    }

                    stackedEntityIds.Add(target.Entity.entityId);
                }

                unitPresentationPlaneOffsetsByEntityId =
                    BuildUnitPresentationPlaneOffsetsByEntityId(stackedUnitEntityIdsByCell, projector);
            }

            for (var i = 0; i < presentableTargets.Count; i++)
            {
                var target = presentableTargets[i];
                var presentationPlaneOffset = EnableUnitPresentationPlaneOffsets &&
                                              target.Entity.type == EntityType.Unit &&
                                              unitPresentationPlaneOffsetsByEntityId != null &&
                                              unitPresentationPlaneOffsetsByEntityId.TryGetValue(
                                                  target.Entity.entityId,
                                                  out var resolvedPresentationPlaneOffset)
                    ? resolvedPresentationPlaneOffset
                    : Vector2.zero;

                if (target.HasCachedTarget)
                {
                    InsertCommittedTarget(target.CachedTarget);
                }
                else
                {
                    var committedPose = _poseResolver.CreateEntityPose(
                        projector,
                        target.Entity.position,
                        topology,
                        target.ProjectedPose,
                        target.Entity.facing,
                        presentationPlaneOffset);
                    if (diagnosticsEnabled)
                    {
                        target.Observation.DidRunPose = true;
                    }

                    if (diagnosticsEnabled)
                    {
                        target.Observation.DidRunSlot = true;
                    }

                    GameplayProjectedFaceSlot? projectedSlot = null;
                    if (projector.TryGetProjectedEntitySlot(target.Entity.position, topology, out var resolvedProjectedSlot))
                    {
                        // Projected slots now describe the entity's physical face slot after topology visibility gating.
                        projectedSlot = resolvedProjectedSlot;
                    }

                    var committedTarget = new StaticWallCommittedTarget(
                        target.Entity.entityId,
                        committedPose,
                        target.Entity.position.face,
                        projectedSlot,
                        _sessionId,
                        target.Candidate?.Generation ?? -1,
                        _provenance.StaticRevision,
                        _topologyRevision,
                        _projectorProfileRevision,
                        target.Candidate?.EntrySignature ?? string.Empty);
                    InsertCommittedTarget(committedTarget);
                    if (target.ShouldPopulateCache && target.Candidate != null && !target.Candidate.Retired)
                    {
                        _staticWallCommittedTargets[target.Entity.entityId] = committedTarget;
                        _staticWallCommittedTargetHighWaterMark = Math.Max(
                            _staticWallCommittedTargetHighWaterMark,
                            _staticWallCommittedTargets.Count);
                    }
                }

                if (diagnosticsEnabled)
                {
                    target.Observation.InsertOrdinal = diagnostics.OutputEntityIds.Count;
                    diagnostics.OutputEntityIds.Add(target.Entity.entityId);
                }
            }

            if (diagnosticsEnabled)
            {
                RecordFrameDiagnostics(
                    orderedEntities,
                    topology,
                    projector,
                    tickIndex,
                    reason,
                    diagnostics);
            }

            // Presentable targets can hold candidate and diagnostics references; release them after the
            // synchronous frame build while retaining the list capacity for the next committed frame.
            _presentableTargetsBuffer.Clear();
        }

        private void ObserveTopology(CubeTopologyState topology)
        {
            if (_hasObservedTopology && !_observedTopology.Equals(topology))
            {
                _topologyRevision++;
                _frameGlobalInvalidation |= StaticWallTargetInvalidation.Topology;
                _staticWallCommittedTargets.Clear();
            }

            _observedTopology = topology;
            _hasObservedTopology = true;
        }

        private void ObserveProjector(GameplayCubeProjector projector)
        {
            if (_observedProjector == null)
            {
                _observedProjector = projector;
                return;
            }

            if (ReferenceEquals(_observedProjector, projector) &&
                _observedProjector.BoardBounds.Equals(projector.BoardBounds) &&
                _observedProjector.CellSize.Equals(projector.CellSize) &&
                _observedProjector.FaceSeamGap.Equals(projector.FaceSeamGap))
            {
                return;
            }

            _observedProjector = projector;
            _projectorProfileRevision++;
            _frameGlobalInvalidation |= StaticWallTargetInvalidation.ProjectorProfile;
            _staticWallCommittedTargets.Clear();
        }

        private void ObserveLifecycle(TickPresentationData presentationData)
        {
            _removedOrExitedEntityIdsBuffer.Clear();
            _spawnedEntityIdsBuffer.Clear();
            if (presentationData == null)
            {
                return;
            }

            var removedOrExitedIds = _removedOrExitedEntityIdsBuffer;
            var spawnedIds = _spawnedEntityIdsBuffer;
            for (var i = 0; i < presentationData.VisibilityChanges.Count; i++)
            {
                var change = presentationData.VisibilityChanges[i];
                if (change.ChangeKind == TickVisibilityChangeKind.Spawn)
                {
                    spawnedIds.Add(change.EntityId);
                }
                else if (change.ChangeKind == TickVisibilityChangeKind.Remove)
                {
                    removedOrExitedIds.Add(change.EntityId);
                }
            }

            for (var i = 0; i < presentationData.EntityExitSignals.Count; i++)
            {
                removedOrExitedIds.Add(presentationData.EntityExitSignals[i].ExitedEntityId);
            }

            for (var i = 0; i < presentationData.EntitySpawnSignals.Count; i++)
            {
                spawnedIds.Add(presentationData.EntitySpawnSignals[i].EntityId);
            }

            removedOrExitedIds.UnionWith(spawnedIds);
            _missingCandidateIdsBuffer.Clear();
            _missingCandidateIdsBuffer.AddRange(removedOrExitedIds);
            _missingCandidateIdsBuffer.Sort();
            for (var i = 0; i < _missingCandidateIdsBuffer.Count; i++)
            {
                var entityId = _missingCandidateIdsBuffer[i];
                var candidate = ResolveCandidate(entityId);
                if (candidate != null)
                {
                    candidate.Generation++;
                    Retire(
                        candidate,
                        spawnedIds.Contains(entityId)
                            ? StaticWallTargetRetireReason.SpawnOrGeneration
                            : StaticWallTargetRetireReason.RemovedOrExit);
                }
            }
        }

        private StaticWallCandidate ResolveCandidate(int entityId)
        {
            return _staticWallCandidates.TryGetValue(entityId, out var candidate) ? candidate : null;
        }

        private void Retire(StaticWallCandidate candidate, StaticWallTargetRetireReason reason)
        {
            if (candidate == null || candidate.Retired)
            {
                return;
            }

            candidate.Retired = true;
            candidate.RetireReason = reason;
            candidate.RetiredFrameOrdinal = _frameOrdinal;
            _staticWallCommittedTargets.Remove(candidate.EntityId);
        }

        private bool TryResolveStaticWallTarget(
            StaticWallCandidate candidate,
            out StaticWallCommittedTarget target)
        {
            if (candidate != null &&
                !candidate.Retired &&
                _staticWallCommittedTargets.TryGetValue(candidate.EntityId, out target) &&
                target.Matches(
                    _sessionId,
                    candidate.Generation,
                    _provenance.StaticRevision,
                    _topologyRevision,
                    _projectorProfileRevision,
                    candidate.EntrySignature))
            {
                return true;
            }

            target = default;
            return false;
        }

        private void InsertCommittedTarget(StaticWallCommittedTarget target)
        {
            _stateStore.CommittedLocalTargetPoses[target.EntityId] = target.Pose;
            _stateStore.CommittedFacesByEntityId[target.EntityId] = target.Face;
            if (target.ProjectedSlot.HasValue)
            {
                _stateStore.CommittedProjectedSlotsByEntityId[target.EntityId] = target.ProjectedSlot.Value;
            }
        }

        private void RecordMissingCandidates(HashSet<int> inputIds, FrameDiagnosticsAccumulator diagnostics)
        {
            _missingCandidateIdsBuffer.Clear();
            foreach (var pair in _staticWallCandidates)
            {
                var candidate = pair.Value;
                if ((candidate.Retired && candidate.RetiredFrameOrdinal != _frameOrdinal) || inputIds.Contains(pair.Key))
                {
                    continue;
                }

                _missingCandidateIdsBuffer.Add(pair.Key);
            }

            _missingCandidateIdsBuffer.Sort();
            for (var i = 0; i < _missingCandidateIdsBuffer.Count; i++)
            {
                var entityId = _missingCandidateIdsBuffer[i];
                var candidate = _staticWallCandidates[entityId];

                if (!candidate.Retired)
                {
                    candidate.Generation++;
                    Retire(candidate, StaticWallTargetRetireReason.RemovedOrExit);
                }

                var retireReason = candidate.RetireReason;
                if (diagnostics == null)
                {
                    continue;
                }

                diagnostics.EntityObservations.Add(new EntityObservationAccumulator(
                    _sessionId,
                    _frameOrdinal,
                    entityId,
                    canonicalInputEntityState: string.Empty,
                    isProvenanceCandidate: true,
                    candidate.EntrySignature,
                    candidate.Generation,
                    _provenance.StaticRevision,
                    _topologyRevision,
                    _projectorProfileRevision,
                    isAdmittedStaticWall: false,
                    isPolicyPresentable: false,
                    retireReason,
                    StaticWallTargetInvalidation.Generation | _frameGlobalInvalidation,
                    StaticWallTargetObservationKind.MissingLifecycle));
            }
        }

        private void RecordFrameDiagnostics(
            IReadOnlyList<EntityState> orderedEntities,
            CubeTopologyState topology,
            GameplayCubeProjector projector,
            int tickIndex,
            CommittedFrameStoreReason reason,
            FrameDiagnosticsAccumulator diagnostics)
        {
            var observations = new List<StaticWallTargetEntityObservation>(diagnostics.EntityObservations.Count);
            var eligibleCount = 0;
            var hitCount = 0;
            var missCount = 0;
            var rebuildCount = 0;
            var fallbackEntityScanCount = 0;
            var dynamicEntityProjectCount = 0;
            var tryProjectEntityCellCount = 0;
            var createEntityPoseCount = 0;
            var tryGetProjectedEntitySlotCount = 0;
            var viewResolveCount = 0;
            var insertCount = 0;
            var removedOrExitCount = 0;
            var signatureChangedCount = 0;
            var spawnOrGenerationCount = 0;
            var unknownGenerationCount = 0;
            var sessionInvalidationCount = 0;
            var generationInvalidationCount = 0;
            var signatureInvalidationCount = 0;
            var staticRevisionInvalidationCount = 0;
            var topologyInvalidationCount = 0;
            var projectorInvalidationCount = 0;
            for (var i = 0; i < diagnostics.EntityObservations.Count; i++)
            {
                var observation = diagnostics.EntityObservations[i].Build();
                observations.Add(observation);
                if (observation.HasInputEntityState)
                {
                    fallbackEntityScanCount++;
                }

                var isEligibleSelection = observation.IsAdmittedStaticWall &&
                                          observation.SelectionKind != StaticWallTargetSelectionKind.NotPresentable;
                if (isEligibleSelection)
                {
                    eligibleCount++;
                    if (observation.SelectionKind == StaticWallTargetSelectionKind.Hit)
                    {
                        hitCount++;
                    }
                    else
                    {
                        missCount++;
                    }
                }

                if (observation.IsAdmittedStaticWall &&
                    observation.SelectionKind == StaticWallTargetSelectionKind.Rebuild)
                {
                    rebuildCount++;
                }

                if (!observation.IsProvenanceCandidate && observation.DidRunProjection)
                {
                    dynamicEntityProjectCount++;
                }

                if (observation.DidRunProjection) tryProjectEntityCellCount++;
                if (observation.DidRunPose) createEntityPoseCount++;
                if (observation.DidRunSlot) tryGetProjectedEntitySlotCount++;
                if (observation.DidRunViewResolve) viewResolveCount++;
                if (observation.DidInsert) insertCount++;

                switch (observation.RetireReason)
                {
                    case StaticWallTargetRetireReason.RemovedOrExit: removedOrExitCount++; break;
                    case StaticWallTargetRetireReason.SignatureChanged: signatureChangedCount++; break;
                    case StaticWallTargetRetireReason.SpawnOrGeneration: spawnOrGenerationCount++; break;
                    case StaticWallTargetRetireReason.UnknownGeneration: unknownGenerationCount++; break;
                }

                if ((observation.Invalidations & StaticWallTargetInvalidation.Session) != 0) sessionInvalidationCount++;
                if ((observation.Invalidations & StaticWallTargetInvalidation.Generation) != 0) generationInvalidationCount++;
                if ((observation.Invalidations & StaticWallTargetInvalidation.Signature) != 0) signatureInvalidationCount++;
                if ((observation.Invalidations & StaticWallTargetInvalidation.StaticRevision) != 0) staticRevisionInvalidationCount++;
                if ((observation.Invalidations & StaticWallTargetInvalidation.Topology) != 0) topologyInvalidationCount++;
                if ((observation.Invalidations & StaticWallTargetInvalidation.ProjectorProfile) != 0) projectorInvalidationCount++;
            }

            var canonicalInputDump = BuildCanonicalInputDump(orderedEntities, topology, projector, reason, tickIndex);
            GameplayCommittedFrameDiagnostics.Record(new GameplayCommittedFrameObservation(
                _sessionId,
                _frameOrdinal,
                tickIndex,
                reason,
                orderedEntities.Count,
                _provenance.Count,
                eligibleCount,
                hitCount,
                missCount,
                rebuildCount,
                new StaticWallTargetRetireHistogram(
                    removedOrExitCount,
                    signatureChangedCount,
                    spawnOrGenerationCount,
                    unknownGenerationCount),
                new StaticWallTargetInvalidationHistogram(
                    sessionInvalidationCount,
                    generationInvalidationCount,
                    signatureInvalidationCount,
                    staticRevisionInvalidationCount,
                    topologyInvalidationCount,
                    projectorInvalidationCount),
                fallbackEntityScanCount,
                dynamicEntityProjectCount,
                tryProjectEntityCellCount,
                createEntityPoseCount,
                tryGetProjectedEntitySlotCount,
                viewResolveCount,
                insertCount,
                diagnostics.OutputEntityIds,
                ComputeSha256(canonicalInputDump),
                canonicalInputDump,
                _staticWallCommittedTargets.Count,
                _staticWallCommittedTargetHighWaterMark,
                observations));
        }

        private string BuildCanonicalInputDump(
            IReadOnlyList<EntityState> orderedEntities,
            CubeTopologyState topology,
            GameplayCubeProjector projector,
            CommittedFrameStoreReason reason,
            int tickIndex)
        {
            var builder = new StringBuilder();
            builder.Append("fixture=package4-static-wall-target-v2\n");
            builder.Append("selectionSchema=")
                .Append(GameplayCommittedFrameObservation.SelectionSchemaDigest).Append('\n');
            builder.Append("session=").Append(_sessionId.ToString(CultureInfo.InvariantCulture))
                .Append("|frame=").Append(_frameOrdinal.ToString(CultureInfo.InvariantCulture))
                .Append("|tick=").Append(tickIndex.ToString(CultureInfo.InvariantCulture))
                .Append("|reason=").Append(((int)reason).ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("topology=").Append(((int)topology.BottomFace).ToString(CultureInfo.InvariantCulture))
                .Append("|topologyRevision=").Append(_topologyRevision.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("projector=")
                .Append(projector.BoardBounds.MinInclusive.x.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(projector.BoardBounds.MinInclusive.y.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(projector.BoardBounds.MaxInclusive.x.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(projector.BoardBounds.MaxInclusive.y.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(projector.CellSize.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(projector.FaceSeamGap.ToString("R", CultureInfo.InvariantCulture))
                .Append("|projectorRevision=").Append(_projectorProfileRevision.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("provenanceRevision=").Append(_provenance.StaticRevision).Append('\n');
            builder.Append("provenanceCount=").Append(_provenance.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            for (var i = 0; i < _provenance.EntityIds.Count; i++)
            {
                var entityId = _provenance.EntityIds[i];
                if (!_provenance.TryGetEntry(entityId, out var entry))
                {
                    throw new InvalidOperationException(
                        $"Static Wall provenance {entityId} is missing its canonical signature.");
                }

                builder.Append("provenance=").Append(entityId.ToString(CultureInfo.InvariantCulture))
                    .Append("|sourceKind=").Append(((int)entry.SourceKind).ToString(CultureInfo.InvariantCulture))
                    .Append("|signature=").Append(entry.Signature).Append('\n');
            }

            for (var i = 0; i < orderedEntities.Count; i++)
            {
                AppendEntityState(builder, orderedEntities[i]);
            }

            return builder.ToString();
        }

        private static void AppendEntityState(StringBuilder builder, EntityState entity)
        {
            builder.Append("entity=");
            StageStaticWallProvenanceSignature.AppendEntityState(builder, entity);
            builder.Append('\n');
        }

        private static string ComputeSha256(string value)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void BuildOrderedEntities(
            IReadOnlyList<EntityState> entities,
            List<EntityState> ordered,
            ISet<int> duplicateEntityIds)
        {
            ordered.Clear();
            duplicateEntityIds.Clear();
            for (var i = 0; i < entities.Count; i++)
            {
                ordered.Add(entities[i]);
            }

            ordered.Sort((left, right) =>
            {
                var idComparison = left.entityId.CompareTo(right.entityId);
                return idComparison != 0
                    ? idComparison
                    : string.CompareOrdinal(
                        StageStaticWallProvenanceSignature.CanonicalizeEntityState(left),
                        StageStaticWallProvenanceSignature.CanonicalizeEntityState(right));
            });
            var writeIndex = 0;
            for (var readIndex = 0; readIndex < ordered.Count; readIndex++)
            {
                if (readIndex > 0 && ordered[readIndex - 1].entityId == ordered[readIndex].entityId)
                {
                    duplicateEntityIds.Add(ordered[readIndex].entityId);
                    continue;
                }

                ordered[writeIndex++] = ordered[readIndex];
            }

            if (writeIndex < ordered.Count)
            {
                ordered.RemoveRange(writeIndex, ordered.Count - writeIndex);
            }
        }

        private static bool ShouldPresent(EntityState entity, CubeTopologyState topology)
        {
            return entity.boardPresence == EntityBoardPresence.Occupying &&
                   topology.IsFaceActive(entity.position.face);
        }

        private Dictionary<int, Vector2> BuildUnitPresentationPlaneOffsetsByEntityId(
            Dictionary<SurfaceCell, List<int>> stackedUnitEntityIdsByCell,
            GameplayCubeProjector projector)
        {
            var planeOffsetsByEntityId = new Dictionary<int, Vector2>();

            foreach (var pair in stackedUnitEntityIdsByCell)
            {
                var entityIds = pair.Value;
                entityIds.Sort();

                for (var slotIndex = 0; slotIndex < entityIds.Count; slotIndex++)
                {
                    planeOffsetsByEntityId[entityIds[slotIndex]] = ResolveUnitPresentationPlaneOffset(
                        slotIndex,
                        entityIds.Count,
                        projector);
                }
            }

            return planeOffsetsByEntityId;
        }

        private static Vector2 ResolveUnitPresentationPlaneOffset(
            int slotIndex,
            int slotCount,
            GameplayCubeProjector projector)
        {
            if (slotCount <= 1)
            {
                return Vector2.zero;
            }

            var circleRadius = UnitPresentationOffsetRadiusInCells * projector.CellSize;
            var squareHalfExtent = UnitPresentationSquareHalfExtentInCells * projector.CellSize;

            return slotCount switch
            {
                2 => new Vector2(slotIndex == 0 ? -circleRadius : circleRadius, 0f),
                3 => slotIndex switch
                {
                    0 => new Vector2(0f, circleRadius),
                    1 => new Vector2(-circleRadius * 0.8660254f, -circleRadius * 0.5f),
                    _ => new Vector2(circleRadius * 0.8660254f, -circleRadius * 0.5f),
                },
                4 => slotIndex switch
                {
                    0 => new Vector2(-squareHalfExtent, squareHalfExtent),
                    1 => new Vector2(squareHalfExtent, squareHalfExtent),
                    2 => new Vector2(-squareHalfExtent, -squareHalfExtent),
                    _ => new Vector2(squareHalfExtent, -squareHalfExtent),
                },
                _ => ResolveCircularPresentationPlaneOffset(slotIndex, slotCount, circleRadius),
            };
        }

        private static Vector2 ResolveCircularPresentationPlaneOffset(
            int slotIndex,
            int slotCount,
            float radius)
        {
            var angle = ((Mathf.PI * 2f) / slotCount * slotIndex) + (Mathf.PI * 0.5f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private sealed class StaticWallCandidate
        {
            internal StaticWallCandidate(StageStaticWallProvenanceEntry entry)
            {
                EntityId = entry.EntityId;
                InitialWall = entry.InitialWall;
                EntrySignature = entry.Signature;
            }

            internal int EntityId { get; }
            internal EntityState InitialWall { get; }
            internal string EntrySignature { get; }
            internal int Generation { get; set; }
            internal bool Retired { get; set; }
            internal StaticWallTargetRetireReason RetireReason { get; set; }
            internal int RetiredFrameOrdinal { get; set; } = -1;
        }

        private readonly struct StaticWallCommittedTarget
        {
            internal StaticWallCommittedTarget(
                int entityId,
                GameplayEntityPose pose,
                FaceId face,
                GameplayProjectedFaceSlot? projectedSlot,
                long sessionId,
                int generation,
                string staticRevision,
                int topologyRevision,
                int projectorProfileRevision,
                string committedSignature)
            {
                EntityId = entityId;
                Pose = pose;
                Face = face;
                ProjectedSlot = projectedSlot;
                SessionId = sessionId;
                Generation = generation;
                StaticRevision = staticRevision ?? string.Empty;
                TopologyRevision = topologyRevision;
                ProjectorProfileRevision = projectorProfileRevision;
                CommittedSignature = committedSignature ?? string.Empty;
            }

            internal int EntityId { get; }
            internal GameplayEntityPose Pose { get; }
            internal FaceId Face { get; }
            internal GameplayProjectedFaceSlot? ProjectedSlot { get; }
            private long SessionId { get; }
            private int Generation { get; }
            private string StaticRevision { get; }
            private int TopologyRevision { get; }
            private int ProjectorProfileRevision { get; }
            private string CommittedSignature { get; }

            internal bool Matches(
                long sessionId,
                int generation,
                string staticRevision,
                int topologyRevision,
                int projectorProfileRevision,
                string committedSignature)
            {
                return SessionId == sessionId &&
                       Generation == generation &&
                       string.Equals(StaticRevision, staticRevision, StringComparison.Ordinal) &&
                       TopologyRevision == topologyRevision &&
                       ProjectorProfileRevision == projectorProfileRevision &&
                       string.Equals(CommittedSignature, committedSignature, StringComparison.Ordinal);
            }
        }

        private sealed class FrameDiagnosticsAccumulator
        {
            internal List<int> OutputEntityIds { get; } = new();
            internal List<EntityObservationAccumulator> EntityObservations { get; } = new();
        }

        private sealed class EntityObservationAccumulator
        {
            internal EntityObservationAccumulator(
                long sessionId,
                int frameOrdinal,
                int entityId,
                string canonicalInputEntityState,
                bool isProvenanceCandidate,
                string provenanceSignature,
                int presentationGeneration,
                string staticRevision,
                int topologyRevision,
                int projectorProfileRevision,
                bool isAdmittedStaticWall,
                bool isPolicyPresentable,
                StaticWallTargetRetireReason retireReason,
                StaticWallTargetInvalidation invalidations,
                StaticWallTargetObservationKind observationKind = StaticWallTargetObservationKind.InputSelection)
            {
                SessionId = sessionId;
                FrameOrdinal = frameOrdinal;
                EntityId = entityId;
                ObservationKind = observationKind;
                HasInputEntityState = observationKind == StaticWallTargetObservationKind.InputSelection;
                CanonicalInputEntityState = canonicalInputEntityState ?? string.Empty;
                IsProvenanceCandidate = isProvenanceCandidate;
                ProvenanceSignature = provenanceSignature ?? string.Empty;
                PresentationGeneration = presentationGeneration;
                StaticRevision = staticRevision ?? string.Empty;
                TopologyRevision = topologyRevision;
                ProjectorProfileRevision = projectorProfileRevision;
                IsAdmittedStaticWall = isAdmittedStaticWall;
                IsPolicyPresentable = isPolicyPresentable;
                RetireReason = retireReason;
                Invalidations = invalidations;
                SelectionKind = StaticWallTargetSelectionKind.NotPresentable;
                InsertOrdinal = -1;
            }

            internal long SessionId { get; }
            internal int FrameOrdinal { get; }
            internal int EntityId { get; }
            internal StaticWallTargetObservationKind ObservationKind { get; }
            internal bool HasInputEntityState { get; }
            internal string CanonicalInputEntityState { get; }
            internal bool IsProvenanceCandidate { get; }
            internal string ProvenanceSignature { get; }
            internal int PresentationGeneration { get; }
            internal string StaticRevision { get; }
            internal int TopologyRevision { get; }
            internal int ProjectorProfileRevision { get; }
            internal bool IsAdmittedStaticWall { get; }
            internal bool IsPolicyPresentable { get; }
            internal StaticWallTargetSelectionKind SelectionKind { get; set; }
            internal StaticWallTargetRetireReason RetireReason { get; }
            internal StaticWallTargetInvalidation Invalidations { get; }
            internal bool DidRunProjection { get; set; }
            internal bool DidRunPose { get; set; }
            internal bool DidRunSlot { get; set; }
            internal bool DidRunViewResolve { get; set; }
            internal bool ViewResolved { get; set; }
            internal bool DidUseFallbackValuePath { get; set; }
            internal int InsertOrdinal { get; set; }

            internal StaticWallTargetEntityObservation Build()
            {
                return new StaticWallTargetEntityObservation(
                    SessionId,
                    FrameOrdinal,
                    EntityId,
                    ObservationKind,
                    HasInputEntityState,
                    CanonicalInputEntityState,
                    IsProvenanceCandidate,
                    ProvenanceSignature,
                    PresentationGeneration,
                    StaticRevision,
                    TopologyRevision,
                    ProjectorProfileRevision,
                    IsAdmittedStaticWall,
                    IsPolicyPresentable,
                    SelectionKind,
                    RetireReason,
                    Invalidations,
                    DidRunProjection,
                    DidRunPose,
                    DidRunSlot,
                    DidRunViewResolve,
                    ViewResolved,
                    DidUseFallbackValuePath,
                    InsertOrdinal);
            }
        }

        private readonly struct PresentableEntityTarget
        {
            public PresentableEntityTarget(
                EntityState entity,
                ProjectedCellPose projectedPose,
                StaticWallCandidate candidate,
                bool shouldPopulateCache,
                EntityObservationAccumulator observation)
            {
                Entity = entity;
                ProjectedPose = projectedPose;
                Candidate = candidate;
                ShouldPopulateCache = shouldPopulateCache;
                CachedTarget = default;
                HasCachedTarget = false;
                Observation = observation;
            }

            public PresentableEntityTarget(
                EntityState entity,
                StaticWallCommittedTarget cachedTarget,
                EntityObservationAccumulator observation)
            {
                Entity = entity;
                ProjectedPose = default;
                Candidate = null;
                ShouldPopulateCache = false;
                CachedTarget = cachedTarget;
                HasCachedTarget = true;
                Observation = observation;
            }

            public EntityState Entity { get; }

            public ProjectedCellPose ProjectedPose { get; }

            public StaticWallCandidate Candidate { get; }

            public bool ShouldPopulateCache { get; }

            public StaticWallCommittedTarget CachedTarget { get; }

            public bool HasCachedTarget { get; }

            public EntityObservationAccumulator Observation { get; }
        }
    }
}
