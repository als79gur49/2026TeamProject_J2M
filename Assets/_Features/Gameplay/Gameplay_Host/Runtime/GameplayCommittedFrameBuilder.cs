using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
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

        public GameplayCommittedFrameBuilder(
            GameplayPresentationStateStore stateStore,
            GameplayPoseResolver poseResolver,
            GameplayAnimationSyncCoordinator animationSync)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _poseResolver = poseResolver ?? throw new ArgumentNullException(nameof(poseResolver));
            _animationSync = animationSync ?? throw new ArgumentNullException(nameof(animationSync));
        }

        public void StoreCommittedFrame(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            GameplayCubeProjector projector,
            GameplayEntityViewBinder viewBinder,
            Action<CubeTopologyState> topologyCommitted)
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

            StoreCommittedEntityTargets(entities, topology, projector, viewBinder);
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
            GameplayEntityViewBinder viewBinder)
        {
            _stateStore.BeginCommittedFrame(topology);
            var presentableTargets = new List<PresentableEntityTarget>(entities.Count);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                _stateStore.EntityTypesByEntityId[entity.entityId] = entity.type;
                _stateStore.EnemyAiModesByEntityId[entity.entityId] = entity.aiMode;

                if (!ShouldPresent(entity, topology) ||
                    !projector.TryProjectEntityCell(entity.position, topology, entity.type, out var projectedPose))
                {
                    continue;
                }

                var view = viewBinder.ResolveOrCreate(entity);
                if (view == null)
                {
                    continue;
                }

                _stateStore.ViewsByEntityId[entity.entityId] = view;
                _animationSync.CacheDrivers(entity.entityId, view);
                presentableTargets.Add(new PresentableEntityTarget(entity, projectedPose));
            }

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

                _stateStore.CommittedLocalTargetPoses[target.Entity.entityId] = _poseResolver.CreateEntityPose(
                    projector,
                    target.Entity.position,
                    topology,
                    target.ProjectedPose,
                    target.Entity.facing,
                    presentationPlaneOffset);

                if (projector.TryGetProjectedEntitySlot(target.Entity.position, topology, out var projectedSlot))
                {
                    // Projected slots now describe the entity's physical face slot after topology visibility gating.
                    _stateStore.CommittedProjectedSlotsByEntityId[target.Entity.entityId] = projectedSlot;
                }
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

        private readonly struct PresentableEntityTarget
        {
            public PresentableEntityTarget(EntityState entity, ProjectedCellPose projectedPose)
            {
                Entity = entity;
                ProjectedPose = projectedPose;
            }

            public EntityState Entity { get; }

            public ProjectedCellPose ProjectedPose { get; }
        }
    }
}
