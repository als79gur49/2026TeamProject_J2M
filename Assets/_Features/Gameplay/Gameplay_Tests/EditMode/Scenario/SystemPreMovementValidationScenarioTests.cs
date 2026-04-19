using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class SystemPreMovementValidationScenarioTests
    {
        [Test]
        [Category("Extended")]
        public void SystemPreMovementValidationOwner_EntersAtPlanSnapshot_And_IsVisibleToMovementCollection_SameTick()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateSystemPreMovementValidationLogic(entityId: 10, enterTick: 1, exitTickExclusive: 3),
                    new ValidationPhaseMovementProbeLogic(entityId: 10, destination: new Vector2Int(1, 0)),
                });

            var result = pipeline.RunTick(new TickInput(1));
            var finalSnapshot = worldState.CreateSnapshot();
            var finalEntity = result.FinalEntities.Single(entity => entity.entityId == 10);

            Assert.That(finalEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetPhasedState(10, out var phasedState), Is.True);
            Assert.That(phasedState.ownerKind, Is.EqualTo(PhasedRuntimeStateOwnerKind.SystemPreMovementValidation));
            Assert.That(phasedState.enteredTick, Is.EqualTo(1));
            Assert.That(finalSnapshot.CanBeTargetedForNewSelection(10), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("PhaseEnter|Entity=10|Tick=1|Owner=SystemPreMovementValidation|Rule=SystemPreMovementValidationWindow"));
            Assert.That(result.Trace.Text, Does.Contain("Timing=SystemPreMovementValidation"));
            Assert.That(result.Trace.Text, Does.Contain("ReservationRead=None"));
            Assert.That(result.Trace.Text, Does.Contain("ExistingEnemyLock=Deferred"));
        }

        private static IPreMovementStateLogic CreateSystemPreMovementValidationLogic(
            int entityId,
            int enterTick,
            int exitTickExclusive)
        {
            var type = typeof(WorldSnapshot).Assembly.GetType(
                "Game.Feature.Gameplay.Entities.SystemPreMovementValidationLogic",
                throwOnError: true);
            var instance = Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { entityId, enterTick, exitTickExclusive },
                culture: null);
            if (instance is not IPreMovementStateLogic logic)
            {
                throw new InvalidOperationException("Failed to construct SystemPreMovementValidationLogic as an IPreMovementStateLogic.");
            }

            return logic;
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, int teamId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private sealed class ValidationPhaseMovementProbeLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly Vector2Int _destination;
            private readonly int _entityId;

            public ValidationPhaseMovementProbeLogic(int entityId, Vector2Int destination)
            {
                _entityId = entityId;
                _destination = destination;
            }

            public int ControlledEntityId => _entityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (snapshot.TryGetResolvedSpatialState(_entityId, out var spatialState) &&
                    spatialState.Kind == SpatialState.Phased)
                {
                    buffer.Add(new RawMovementIntent(_entityId, priority: 100, destination: _destination));
                }
            }
        }
    }
}
