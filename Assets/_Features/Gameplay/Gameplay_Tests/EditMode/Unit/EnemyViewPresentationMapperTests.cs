using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyViewPresentationMapperTests
    {
        [Test]
        [Category("Extended")]
        public void EnemyViewPresentationMapper_MapsJumpWindupAndAirborneWithoutUsingAttackPhase()
        {
            const int enemyId = 40;
            var mapper = new EnemyViewPresentationMapper();
            var states = new Dictionary<int, EnemyViewPresentationState>();
            var viewsByEntityId = new Dictionary<int, GameplayEntityView>();
            var enemy = CreateEnemy(enemyId, EnemyAiMode.Patrol);

            mapper.Build(
                CreateResult(
                    tickIndex: 1,
                    enemy,
                    new TickEnemyJumpPresentationSignal(
                        enemyId,
                        sequence: 2,
                        phase: EnemyJumpPhase.Windup,
                        startedWindupThisTick: true,
                        startedAirborneThisTick: false,
                        landedThisTick: false,
                        retryThisTick: false)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var windupState), Is.True);
            Assert.That(windupState.JumpPhase, Is.EqualTo(EnemyJumpPhase.Windup));
            Assert.That(windupState.StartedJumpWindupThisTick, Is.True);
            Assert.That(windupState.StartedJumpAirborneThisTick, Is.False);
            Assert.That(windupState.StartedWindupThisTick, Is.False);
            Assert.That(windupState.ExecutedThisTick, Is.False);
            Assert.That(windupState.StartedRecoveryThisTick, Is.False);
            Assert.That(windupState.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));

            mapper.Build(
                CreateResult(
                    tickIndex: 2,
                    enemy,
                    new TickEnemyJumpPresentationSignal(
                        enemyId,
                        sequence: 2,
                        phase: EnemyJumpPhase.Airborne,
                        startedWindupThisTick: false,
                        startedAirborneThisTick: true,
                        landedThisTick: false,
                        retryThisTick: false)),
                viewsByEntityId,
                states);

            Assert.That(states.TryGetValue(enemyId, out var airborneState), Is.True);
            Assert.That(airborneState.JumpPhase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(airborneState.StartedJumpWindupThisTick, Is.False);
            Assert.That(airborneState.StartedJumpAirborneThisTick, Is.True);
            Assert.That(airborneState.StartedWindupThisTick, Is.False);
            Assert.That(airborneState.ExecutedThisTick, Is.False);
            Assert.That(airborneState.StartedRecoveryThisTick, Is.False);
            Assert.That(airborneState.ActiveActionKind, Is.EqualTo(EnemyActionKind.None));
        }

        [Test]
        [Category("Extended")]
        public void EnemyViewPresentationMapper_TryMapInitial_RoleEnemyWithNoneAiMode_StillMapsEnemyViewState()
        {
            var mapper = new EnemyViewPresentationMapper();
            var enemy = CreateEnemy(entityId: 40, EnemyAiMode.None);

            var mapped = mapper.TryMapInitial(enemy, out var state);

            Assert.That(mapped, Is.True);
            Assert.That(state.EntityId, Is.EqualTo(40));
            Assert.That(state.AiMode, Is.EqualTo(EnemyAiMode.None));
            Assert.That(state.DidDie, Is.False);
        }

        private static TickResult CreateResult(
            int tickIndex,
            EntityState enemy,
            TickEnemyJumpPresentationSignal jumpSignal)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { enemy },
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    new[] { jumpSignal },
                    Array.Empty<TickEntityExitPresentationSignal>()),
                string.Empty,
                TickTrace.Empty);
        }

        private static EntityState CreateEnemy(int entityId, EnemyAiMode aiMode)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 1, 1),
                hp = 1,
                maxHp = 1,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }
    }
}
