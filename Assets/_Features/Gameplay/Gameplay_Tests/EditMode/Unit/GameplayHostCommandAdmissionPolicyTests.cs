using System.Collections.Generic;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayHostCommandAdmissionPolicyTests
    {
        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_SeedsSnapshotCache_AndReusesSameReference_WithinCompletedTickWindow()
        {
            var hostObject = new GameObject("AdmissionPolicy_SeedsSnapshotCache_AndReusesSameReference_WithinCompletedTickWindow");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                using var policy = CreatePolicy(host);

                Assert.That(policy.TryCreateSnapshot(out var firstSnapshot), Is.True);
                Assert.That(policy.TryCreateSnapshot(out var secondSnapshot), Is.True);
                Assert.That(ReferenceEquals(firstSnapshot, secondSnapshot), Is.True);

                var freshSnapshot = GameplayCompositionRoot.CreateSnapshot(host.WorldState);
                Assert.That(BuildSnapshotSemanticDump(firstSnapshot), Is.EqualTo(BuildSnapshotSemanticDump(freshSnapshot)));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_TickCompletedRefresh_ReplacesCachedReference_AndMatchesFreshSnapshot()
        {
            var hostObject = new GameObject("AdmissionPolicy_TickCompletedRefresh_ReplacesCachedReference_AndMatchesFreshSnapshot");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                using var policy = CreatePolicy(host);

                Assert.That(policy.TryCreateSnapshot(out var beforeTickSnapshot), Is.True);
                Assert.That(host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Right).Accepted, Is.True);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryCreateSnapshot(out var afterTickSnapshot), Is.True);

                Assert.That(ReferenceEquals(beforeTickSnapshot, afterTickSnapshot), Is.False);
                Assert.That(BuildSnapshotSemanticDump(afterTickSnapshot), Is.EqualTo(BuildSnapshotSemanticDump(GameplayCompositionRoot.CreateSnapshot(host.WorldState))));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_CommittedControllableActorAccessor_ReusesSameWindowFact_AndRefreshesOnTickCompleted()
        {
            var hostObject = new GameObject("AdmissionPolicy_CommittedControllableActorAccessor_ReusesSameWindowFact_AndRefreshesOnTickCompleted");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                using var policy = CreatePolicy(host);

                Assert.That(policy.TryGetCommittedControllableActor(out var firstActor), Is.True);
                Assert.That(policy.TryGetCommittedControllableActor(out var secondActor), Is.True);

                var freshBeforeTick = GameplayCompositionRoot.CreateSnapshot(host.WorldState);
                Assert.That(freshBeforeTick.TryGetEntity(10, out var freshBeforeActor), Is.True);

                Assert.That(firstActor.entityId, Is.EqualTo(freshBeforeActor.entityId));
                Assert.That(firstActor.position, Is.EqualTo(freshBeforeActor.position));
                Assert.That(secondActor.entityId, Is.EqualTo(firstActor.entityId));
                Assert.That(secondActor.position, Is.EqualTo(firstActor.position));

                Assert.That(host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Right).Accepted, Is.True);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryGetCommittedControllableActor(out var refreshedActor), Is.True);

                var freshAfterTick = GameplayCompositionRoot.CreateSnapshot(host.WorldState);
                Assert.That(freshAfterTick.TryGetEntity(10, out var freshAfterActor), Is.True);

                Assert.That(refreshedActor.entityId, Is.EqualTo(freshAfterActor.entityId));
                Assert.That(refreshedActor.position, Is.EqualTo(freshAfterActor.position));
                Assert.That(refreshedActor.position, Is.Not.EqualTo(firstActor.position));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_Dispose_StopsTickCompletedRefresh_AndLeavesCachedReferenceUnchanged_WithoutFreshnessAssertions()
        {
            var hostObject = new GameObject("AdmissionPolicy_Dispose_StopsTickCompletedRefresh_AndLeavesCachedReferenceUnchanged_WithoutFreshnessAssertions");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                var policy = CreatePolicy(host);
                Assert.That(host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Right).Accepted, Is.True);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryCreateSnapshot(out var refreshedSnapshot), Is.True);

                policy.Dispose();

                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryCreateSnapshot(out var snapshotAfterDispose), Is.True);

                Assert.That(ReferenceEquals(refreshedSnapshot, snapshotAfterDispose), Is.True);
                Assert.That(BuildSnapshotSemanticDump(snapshotAfterDispose), Is.Not.EqualTo(BuildSnapshotSemanticDump(GameplayCompositionRoot.CreateSnapshot(host.WorldState))));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        private static GameplayHostCommandAdmissionPolicy CreatePolicy(GameplaySceneHost host)
        {
            return new GameplayHostCommandAdmissionPolicy(
                host.WorldState,
                host.TickRunner,
                host.InputHost,
                host.Presenter,
                (GameplayHostPauseService)host.UiAccess.PauseService);
        }

        private static string BuildSnapshotSemanticDump(WorldSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("Bounds=").Append(snapshot.BoardBounds).Append('\n');
            builder.Append("Topology=").Append(snapshot.Topology.BottomFace).Append('\n');

            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            builder.Append("Entities=").Append(entities.Count).Append('\n');
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                builder
                    .Append(entity.entityId).Append('|')
                    .Append(entity.position.face).Append('|')
                    .Append(entity.position.x).Append('|')
                    .Append(entity.position.y).Append('|')
                    .Append(entity.hp).Append('|')
                    .Append(entity.maxHp).Append('|')
                    .Append(entity.teamId).Append('|')
                    .Append(entity.type).Append('|')
                    .Append(entity.unitRole).Append('|')
                    .Append(entity.state).Append('|')
                    .Append(entity.stateTimer).Append('|')
                    .Append(entity.facing).Append('|')
                    .Append(entity.boardPresence).Append('|')
                    .Append(entity.markedForDeath).Append('|')
                    .Append(entity.spawnTick).Append('|')
                    .Append((int)entity.boxCapabilities).Append('|')
                    .Append(entity.kineticInstigatorEntityId).Append('|')
                    .Append(entity.kineticInstigatorTeamId).Append('|')
                    .Append(entity.aiMode).Append('|')
                    .Append(entity.aiStateTimer).Append('|')
                    .Append(entity.enemyLocomotionCooldownTicks).Append('\n');
            }

            AppendOccupancyEntries(builder, "Units", snapshot, static (source, buffer) => source.EnumerateUnitOccupancyOrdered(buffer));
            AppendOccupancyEntries(builder, "Solids", snapshot, static (source, buffer) => source.EnumerateSolidOccupancyOrdered(buffer));
            AppendOccupancyEntries(builder, "Projectiles", snapshot, static (source, buffer) => source.EnumerateProjectileOccupancyOrdered(buffer));
            AppendPlayerControlEntries(builder, snapshot);
            AppendUnitKinematicEntries(builder, snapshot);
            AppendPlayerDamageEntries(builder, snapshot);
            AppendEnemyActionEntries(builder, snapshot);
            AppendEnemyJumpEntries(builder, snapshot);
            AppendExecutionLockEntries(builder, snapshot);
            AppendTerrainEntries(builder, snapshot);

            return builder.ToString();
        }

        private static void AppendOccupancyEntries(
            StringBuilder builder,
            string label,
            WorldSnapshot snapshot,
            System.Action<WorldSnapshot, List<SnapshotOccupancyEntry>> populate)
        {
            var entries = new List<SnapshotOccupancyEntry>();
            populate(snapshot, entries);
            builder.Append(label).Append('=').Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append(entries[i].Cell.face).Append('|')
                    .Append(entries[i].Cell.x).Append('|')
                    .Append(entries[i].Cell.y).Append('|')
                    .Append(entries[i].EntityId).Append('\n');
            }
        }

        private static void AppendPlayerControlEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<PlayerControlSnapshotEntry>();
            snapshot.EnumeratePlayerControlStatesOrdered(entries);
            builder.Append("PlayerControl=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                var state = entries[i].State;
                var action = state.activeAction;
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(state.moveCooldownTicks).Append('|')
                    .Append(state.nextMoveAllowedTick).Append('|')
                    .Append(state.actionSequenceCounter).Append('|')
                    .Append(action.kind).Append('|')
                    .Append(action.sequence).Append('|')
                    .Append(action.direction).Append('|')
                    .Append(action.targetEntityId).Append('|')
                    .Append(action.startTick).Append('|')
                    .Append(action.executeTick).Append('|')
                    .Append(action.recoveryEndTick).Append('|')
                    .Append(action.executionAttempted).Append('\n');
            }
        }

        private static void AppendUnitKinematicEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<UnitKinematicSnapshotEntry>();
            snapshot.EnumerateUnitKinematicStatesOrdered(entries);
            builder.Append("UnitKinematic=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                var state = entries[i].State;
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(state.mode).Append('|')
                    .Append(state.localOffset.X.RawValue).Append('|')
                    .Append(state.localOffset.Y.RawValue).Append('|')
                    .Append(state.remainingTicks).Append('|')
                    .Append(state.elapsedTicks).Append('|')
                    .Append(state.totalTicks).Append('|')
                    .Append(state.startedTick).Append('|')
                    .Append(state.commitTick).Append('|')
                    .Append(state.stepDirectionX).Append('|')
                    .Append(state.stepDirectionY).Append('\n');
            }
        }

        private static void AppendPlayerDamageEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<PlayerDamageSnapshotEntry>();
            snapshot.EnumeratePlayerDamageStatesOrdered(entries);
            builder.Append("PlayerDamage=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(entries[i].State.nextDamageAllowedTick).Append('\n');
            }
        }

        private static void AppendEnemyActionEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<EnemyActionSnapshotEntry>();
            snapshot.EnumerateEnemyActionStatesOrdered(entries);
            builder.Append("EnemyAction=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                var state = entries[i].State;
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(state.kind).Append('|')
                    .Append(state.sequence).Append('|')
                    .Append(state.lockedTargetEntityId).Append('|')
                    .Append(state.direction).Append('|')
                    .Append(state.startTick).Append('|')
                    .Append(state.executeTick).Append('|')
                    .Append(state.executionAttempted).Append('\n');
            }
        }

        private static void AppendEnemyJumpEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<EnemyJumpSnapshotEntry>();
            snapshot.EnumerateEnemyJumpStatesOrdered(entries);
            builder.Append("EnemyJump=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                var state = entries[i].State;
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(state.phase).Append('|')
                    .Append(state.sequence).Append('|')
                    .Append(state.sourceCell.face).Append('|')
                    .Append(state.sourceCell.x).Append('|')
                    .Append(state.sourceCell.y).Append('|')
                    .Append(state.lockedTargetCell.face).Append('|')
                    .Append(state.lockedTargetCell.x).Append('|')
                    .Append(state.lockedTargetCell.y).Append('|')
                    .Append(state.windupEndTick).Append('|')
                    .Append(state.landingTick).Append('|')
                    .Append(state.cooldownRemainingTicks).Append('|')
                    .Append(state.retryCount).Append('\n');
            }
        }

        private static void AppendExecutionLockEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<EntityExecutionLockSnapshotEntry>();
            snapshot.EnumerateEntityExecutionLockStatesOrdered(entries);
            builder.Append("ExecutionLock=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(entries[i].State.phase).Append('|')
                    .Append(entries[i].State.sequence).Append('|')
                    .Append(entries[i].State.unlockTickExclusive).Append('\n');
            }
        }

        private static void AppendTerrainEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<TerrainCellState>();
            snapshot.EnumerateTerrainCellsOrdered(entries);
            builder.Append("Terrain=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append((int)entries[i].Cell.face).Append('|')
                    .Append(entries[i].Cell.x).Append('|')
                    .Append(entries[i].Cell.y).Append('|')
                    .Append((int)entries[i].Kind).Append('|')
                    .Append((int)entries[i].Flags).Append('\n');
            }
        }

        private static GameplaySceneHostConfiguration CreateConfiguration(
            EntityState[] initialEntities,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            BoardBounds? boardBounds = null,
            PlayerControlTimingSettings playerControlTiming = null)
        {
            var configuration = new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = boardBounds ?? new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = initialEntities,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                ObjectiveRuntimeDefinition = objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
                PlayerControlTiming = playerControlTiming ?? PlayerControlTimingSettings.CreateDefault(),
            };
            configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            return configuration;
        }

        private static EntityState CreatePlayerEntity(
            SurfaceCell position,
            Direction facing = Direction.Up)
        {
            return new EntityState
            {
                entityId = 10,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }
    }
}
