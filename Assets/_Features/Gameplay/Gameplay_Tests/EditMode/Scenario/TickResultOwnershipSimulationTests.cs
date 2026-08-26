using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class TickResultOwnershipSimulationTests
    {
        [Test]
        [Category("Extended")]
        public void BuilderOwnedResult_RemainsImmutableAfterWorldChangesAndNextTick()
        {
            var worldState = GameplayCompositionRoot.CreateWorldState(
                new[] { CreateEntity(10, 0) },
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(4, 4)),
                new CubeTopologyState(FaceId.Floor));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
            var first = pipeline.RunTick(new TickInput(1));

            var writeContext = worldState.CreateWriteContext();
            writeContext.RemoveEntity(10);
            writeContext.SpawnEntity(CreateEntity(20, 1));
            var second = pipeline.RunTick(new TickInput(2));

            CollectionAssert.AreEqual(new[] { 10 }, first.FinalEntities.Select(entity => entity.entityId));
            CollectionAssert.AreEqual(new[] { 20 }, second.FinalEntities.Select(entity => entity.entityId));
        }

        [Test]
        [Category("Extended")]
        public void OwnedAndGeneralCopyPaths_HaveCanonicalFinalEntityEventHashAndTraceParity()
        {
            var world = GameplayCompositionRoot.CreateWorldState(
                new[] { CreateEntity(20, 1), CreateEntity(10, 0) },
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(4, 4)),
                new CubeTopologyState(FaceId.Floor));
            var snapshot = world.CreateSnapshot();
            var ordered = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(ordered);
            var ownedWrapper = new ReadOnlyCollection<EntityState>(ordered);
            var events = new[] { "event-a", "event-b" };
            var owned = TickResultData.CreateFromOwnedFinalEntities(
                ownedWrapper,
                Array.Empty<DelayedAttackEffectRecord>(),
                events,
                TickPresentationData.Empty);
            var copied = new TickResultData(
                ordered,
                Array.Empty<DelayedAttackEffectRecord>(),
                events,
                TickPresentationData.Empty);
            var hashBuilder = new DeterminismHashBuilder();
            var ownedHash = hashBuilder.Build(7, snapshot, owned);
            var copiedHash = hashBuilder.Build(7, snapshot, copied);
            var traceBuilder = new TickTraceBuilder();
            var enemyAi = new EnemyAiPhaseResult(new List<string>(), new List<string>(), new List<string>());
            var enemyAction = new EnemyActionPhaseResult(new(), new());
            var preMovement = new PreMovementStatePhaseResult(new List<string>());
            var ownedTrace = traceBuilder.Build(
                7,
                snapshot,
                enemyAi,
                enemyAction,
                preMovement,
                MovementPhaseResult.Empty,
                snapshot,
                AttackPhaseResult.Empty,
                CleanupPhaseResult.Empty,
                RespawnPhaseResult.Empty,
                snapshot,
                owned,
                ownedHash);
            var copiedTrace = traceBuilder.Build(
                7,
                snapshot,
                enemyAi,
                enemyAction,
                preMovement,
                MovementPhaseResult.Empty,
                snapshot,
                AttackPhaseResult.Empty,
                CleanupPhaseResult.Empty,
                RespawnPhaseResult.Empty,
                snapshot,
                copied,
                copiedHash);

            CollectionAssert.AreEqual(copied.FinalEntities, owned.FinalEntities);
            CollectionAssert.AreEqual(copied.EventLog, owned.EventLog);
            Assert.That(ownedHash, Is.EqualTo(copiedHash));
            Assert.That(ownedTrace.Text, Is.EqualTo(copiedTrace.Text));
        }

        private static EntityState CreateEntity(int entityId, int x)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, x, 0),
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Unit,
                boardPresence = EntityBoardPresence.Occupying,
                facing = Direction.Right,
            };
        }
    }
}
