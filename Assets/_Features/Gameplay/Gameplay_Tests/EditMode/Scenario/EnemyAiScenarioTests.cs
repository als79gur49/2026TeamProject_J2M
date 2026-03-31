using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class EnemyAiScenarioTests
    {
        [Test]
        public void EnemyAi_MultiTick_FollowsPatrolChaseAttackRecoverSequence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var firstTick = pipeline.RunTick(new TickInput(1));
            var enemyAfterFirstTick = GetEntity(worldState, 40);

            Assert.That(enemyAfterFirstTick.position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(enemyAfterFirstTick.aiMode, Is.EqualTo(EnemyAiMode.Chase));
            Assert.That(firstTick.AttackPhaseResult.SortedInputs, Is.Empty);
            Assert.That(firstTick.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Patrol|FromTimer=0|To=Chase|ToTimer=0|Reason=TargetSensed"));

            var secondTick = pipeline.RunTick(new TickInput(2));
            var enemyAfterSecondTick = GetEntity(worldState, 40);
            var playerAfterSecondTick = GetEntity(worldState, 10);

            Assert.That(enemyAfterSecondTick.position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(enemyAfterSecondTick.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemyAfterSecondTick.aiStateTimer, Is.EqualTo(1));
            Assert.That(playerAfterSecondTick.hp, Is.EqualTo(2));
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, TargetId: 10),
                },
                secondTick.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(secondTick.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeAttack|E=40|From=Chase|FromTimer=0|To=Attack|ToTimer=0|Reason=TargetInRange"));
            Assert.That(secondTick.Trace.Text, Does.Contain("EnemyAiTransition|Stage=AfterAttack|E=40|From=Attack|FromTimer=0|To=Recover|ToTimer=1|Reason=AttackCommitted"));

            var thirdTick = pipeline.RunTick(new TickInput(3));
            var enemyAfterThirdTick = GetEntity(worldState, 40);

            Assert.That(thirdTick.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(thirdTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(enemyAfterThirdTick.position.PlanarPosition, Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(enemyAfterThirdTick.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemyAfterThirdTick.aiStateTimer, Is.Zero);
            Assert.That(thirdTick.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Recover|FromTimer=1|To=Recover|ToTimer=0|Reason=RecoverTick"));
        }

        [Test]
        public void EnemyAi_FatalDamage_IsRemovedByCleanupAtTickEnd()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), hp: 1, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedAttackLogic(10, 40),
                });

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEquivalent(
                new[]
                {
                    (SourceId: 10, TargetId: 40),
                    (SourceId: 40, TargetId: 10),
                },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(new[] { 40 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(40, out _), Is.False);
            Assert.That(GetEntity(worldState, 10).hp, Is.EqualTo(2));
            Assert.That(result.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Patrol|FromTimer=0|To=Attack|ToTimer=0|Reason=TargetInRange"));
            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=40"));
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            int hp,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
                aiStateTimer = 0,
            };
        }

        private sealed class ScriptedAttackLogic : IAttackEntityLogic
        {
            private readonly int _sourceId;
            private readonly int _targetId;

            public ScriptedAttackLogic(int sourceId, int targetId)
            {
                _sourceId = sourceId;
                _targetId = targetId;
            }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (!snapshot.TryGetEntity(_sourceId, out var source) ||
                    !snapshot.TryGetEntity(_targetId, out var target) ||
                    source.hp <= 0 ||
                    target.hp <= 0 ||
                    source.markedForDeath ||
                    target.markedForDeath)
                {
                    return;
                }

                buffer.Add(new RawAttackIntent(_sourceId, 100, _targetId));
            }
        }
    }
}
