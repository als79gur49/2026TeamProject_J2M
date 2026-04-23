using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyLogicTests
    {
        [Test]
        [Category("Extended")]
        public void EnemyLogic_ImplementsMovementAndAttackContracts()
        {
            var logic = new EnemyLogic(entityId: 40);

            Assert.That(logic, Is.InstanceOf<IMovementEntityLogic>());
            Assert.That(logic, Is.InstanceOf<IAttackEntityLogic>());
            Assert.That(logic.ControlledEntityId, Is.EqualTo(40));
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_InvalidConfig_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(() => new EnemyLogic(entityId: 40, default(EnemyAiRuntimeDefinition)));

            Assert.That(exception.ParamName, Is.EqualTo("aiDefinition"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_PatrolMode_ProducesForwardMovementIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_PatrolMode_BottomFaceBoundary_DoesNotProduceMovementIntent()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(
                        entityId: 40,
                        teamId: 2,
                        position: new SurfaceCell(FaceId.Floor, 1, 1),
                        aiMode: EnemyAiMode.Patrol,
                        facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)));
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void EnemyRandomWalkPatrolPlanner_UninitializedState_RequestsInitialization_AndKeepsSelectionStableAfterInit()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 2);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = PatrolSettings.CreateDefaultRandomWalk();

            var initialPlan = EnemyRandomWalkPatrolPlanner.BuildPlan(snapshot, source, tickIndex: 7, default, settings);
            var initializedState = EnemyPatrolQueries.Initialize(default, source.position);
            var initializedPlan = EnemyRandomWalkPatrolPlanner.BuildPlan(snapshot, source, tickIndex: 7, initializedState, settings);

            Assert.That(initialPlan.ShouldInitializeState, Is.True);
            Assert.That(initialPlan.HasDirection, Is.True);
            Assert.That(initialPlan.PlannedFacing, Is.EqualTo(initialPlan.PlannedDirection));
            Assert.That(initialPlan.CandidateMask, Is.Not.EqualTo(0));
            Assert.That(initialPlan.PlannedDirection, Is.EqualTo(initializedPlan.PlannedDirection));
            Assert.That(initialPlan.CandidateMask, Is.EqualTo(initializedPlan.CandidateMask));
        }

        [Test]
        [Category("Extended")]
        public void EnemyRandomWalkPatrolPlanner_PreventsImmediateBacktrack_WhenAlternativeExists()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 2);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = PatrolSettings.CreateDefaultRandomWalk();
            var patrolState = new EnemyPatrolRuntimeState
            {
                sequence = 2,
                homeCell = sourceCell,
                lastCommittedDirection = Direction.Up,
            };

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(snapshot, source, tickIndex: 5, patrolState, settings);

            Assert.That((plan.CandidateMask & (1 << 2)) == 0, Is.True, "Immediate reverse direction should be excluded when alternatives exist.");
            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.Not.EqualTo(Direction.Down));
        }

        [Test]
        [Category("Extended")]
        public void EnemyRandomWalkPatrolPlanner_OutsideLeash_ChoosesOnlyDistanceReducingCandidate()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = PatrolSettings.CreateDefaultRandomWalk();
            var patrolState = new EnemyPatrolRuntimeState
            {
                sequence = 3,
                homeCell = homeCell,
                lastCommittedDirection = Direction.Right,
            };

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(snapshot, source, tickIndex: 9, patrolState, settings);

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Left));
            Assert.That(plan.CandidateMask, Is.EqualTo(1 << 3));
        }

        [Test]
        [Category("Extended")]
        public void EnemyRandomWalkPatrolPlanner_BoundaryCandidateMask_ExcludesTopologyChangeStep()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = PatrolSettings.CreateDefaultRandomWalk();
            var patrolState = new EnemyPatrolRuntimeState
            {
                sequence = 2,
                homeCell = sourceCell,
                lastCommittedDirection = Direction.Left,
            };

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(snapshot, source, tickIndex: 3, patrolState, settings);

            Assert.That((plan.CandidateMask & (1 << 0)) == 0, Is.True, "Topology-changing up-step must never be emitted as a patrol candidate.");
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_NonAttackingRandomWalkPatrol_InitializesAndCommitsPatrolState()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 2, 2);
            var profile = CreateNonAttackingEnemyProfile();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: homeCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));

                var enemy = GetEntity(worldState, 40);
                var patrolState = GetEnemyPatrolState(worldState, 40);

                Assert.That(patrolState.IsInitialized, Is.True);
                Assert.That(patrolState.sequence, Is.EqualTo(2));
                Assert.That(patrolState.homeCell, Is.EqualTo(homeCell));
                Assert.That(patrolState.lastCommittedDirection, Is.EqualTo(enemy.facing));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_StationaryPassiveContactProfile_DoesNotMove_AndProducesSameCellContactIntent()
        {
            var profile = EnemyAiProfileTestFactory.CreateStationaryPassiveContact();
            var sharedCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var movementBuffer = new List<RawMovementIntent>();
            var attackBuffer = new List<RawAttackIntent>();

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), movementBuffer);
                logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), attackBuffer);

                Assert.That(movementBuffer, Is.Empty);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, TargetId: 10, SourceKind: AttackSourceKind.PassiveContact),
                    },
                    attackBuffer.Select(intent => (intent.SourceId, intent.TargetId, intent.SourceKind)).ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ChaseMode_ProducesMovementTowardNearestOpponent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(0, 4), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Up),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ChaseMode_FallsBackToSecondaryAxisWhenPrimaryStepIsBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 1), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, Destination: new Vector2Int(0, 1), Command: MovementCommandKind.Move),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_JumpCapablePatrol_OpenGround_UsesPatrolMovementIntent()
        {
            var profile = CreateJumpPatrolProfile();
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var buffer = new List<RawMovementIntent>();

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                    },
                    buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
                Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(40, out _), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_JumpCapableChase_OpenGround_UsesGroundChaseMovementIntent()
        {
            var profile = CreateJumpEnemyProfile(attackDecisionStrategyKind: AttackDecisionStrategyKind.None);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var buffer = new List<RawMovementIntent>();

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                    },
                    buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
                Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(40, out _), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [TestCase(EnemyJumpPhase.Windup)]
        [TestCase(EnemyJumpPhase.Airborne)]
        public void EnemyLogic_JumpActivePhases_SuppressMovementAndAttack(EnemyJumpPhase phase)
        {
            var profile = CreateJumpEnemyProfile();
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var movementBuffer = new List<RawMovementIntent>();
            var attackBuffer = new List<RawAttackIntent>();
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                CreateEnemyJumpState(
                    phase,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell: new SurfaceCell(FaceId.Floor, 2, 0),
                    landingTick: 5));
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 1,
                    lockedTargetEntityId = 10,
                    direction = Direction.Right,
                    startTick = 1,
                    executeTick = 5,
                    executionAttempted = false,
                });

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(5), movementBuffer);
                logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(5), attackBuffer);

                Assert.That(movementBuffer, Is.Empty);
                Assert.That(attackBuffer, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_JumpCooldown_DoesNotSuppressMovementOrAttack()
        {
            var profile = CreateJumpEnemyProfile();
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var movementBuffer = new List<RawMovementIntent>();
            var attackBuffer = new List<RawAttackIntent>();
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                CreateEnemyJumpState(
                    EnemyJumpPhase.Cooldown,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell: new SurfaceCell(FaceId.Floor, 2, 0),
                    landingTick: 4,
                    cooldownRemainingTicks: 2));
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 1,
                    lockedTargetEntityId = 10,
                    direction = Direction.Right,
                    startTick = 1,
                    executeTick = 5,
                    executionAttempted = false,
                });

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(5), movementBuffer);
                logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(5), attackBuffer);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                    },
                    movementBuffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, TargetId: 10),
                    },
                    attackBuffer.Select(intent => (intent.SourceId, intent.TargetId)).ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_JumpLandingTick_SuppressesMovementButNotAttack()
        {
            var profile = CreateJumpEnemyProfile();
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var movementBuffer = new List<RawMovementIntent>();
            var attackBuffer = new List<RawAttackIntent>();
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                CreateEnemyJumpState(
                    EnemyJumpPhase.Cooldown,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell: new SurfaceCell(FaceId.Floor, 2, 0),
                    landingTick: 5,
                    cooldownRemainingTicks: 2));
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 1,
                    lockedTargetEntityId = 10,
                    direction = Direction.Right,
                    startTick = 1,
                    executeTick = 5,
                    executionAttempted = false,
                });

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(5), movementBuffer);
                logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(5), attackBuffer);

                Assert.That(movementBuffer, Is.Empty);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, TargetId: 10),
                    },
                    attackBuffer.Select(intent => (intent.SourceId, intent.TargetId)).ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ChaseMode_BoundaryStep_DoesNotCreateTopologyChangingMovementGroup()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(
                        entityId: 10,
                        teamId: 1,
                        position: new SurfaceCell(FaceId.Floor, 1, 0),
                        aiMode: EnemyAiMode.None),
                    CreateUnit(
                        entityId: 40,
                        teamId: 2,
                        position: new SurfaceCell(FaceId.Floor, 1, 1),
                        aiMode: EnemyAiMode.Chase,
                        facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)));
            var snapshot = worldState.CreateSnapshot();
            var sortedIntents = new List<MoveIntent>
            {
                CreateMoveIntent(sourceId: 40, priority: 50, destination: new Vector2Int(1, 2), intentId: 1),
            };
            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();

            new MovementExpander().Expand(snapshot, sortedIntents, null, expandedCandidates, rejectedReasons);

            Assert.That(expandedCandidates, Is.Empty);
            Assert.That(rejectedReasons, Has.Some.Contains("Reason=BlockedDestination"));
            Assert.That(rejectedReasons, Has.None.Contains("TopologyCommitted"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ExecuteTick_ProducesRawAttackIntentForLockedTarget()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawAttackIntent>();
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 1,
                    lockedTargetEntityId = 10,
                    direction = Direction.Right,
                    startTick = 1,
                    executeTick = 1,
                    executionAttempted = false,
                });

            logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, TargetId: 10),
                },
                buffer.Select(intent => (intent.SourceId, intent.TargetId)).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ExecuteTickActionState_DoesNotRequireAttackModeToProduceRawAttackIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawAttackIntent>();
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 1,
                    lockedTargetEntityId = 10,
                    direction = Direction.Right,
                    startTick = 1,
                    executeTick = 1,
                    executionAttempted = false,
                });

            logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, TargetId: 10),
                },
                buffer.Select(intent => (intent.SourceId, intent.TargetId)).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_AttackMode_WithoutActiveActionState_DoesNotProduceRawAttackIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawAttackIntent>();

            logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void EnemyActionStateTargeting_ResolveFacing_SameCell_KeepsSourceFacing()
        {
            var source = CreateUnit(
                entityId: 40,
                teamId: 2,
                position: new SurfaceCell(FaceId.Floor, 1, 1),
                aiMode: EnemyAiMode.Attack,
                facing: Direction.Left);
            var target = CreateUnit(
                entityId: 10,
                teamId: 1,
                position: new SurfaceCell(FaceId.Floor, 1, 1),
                aiMode: EnemyAiMode.None,
                facing: Direction.Up);

            var resolvedFacing = EnemyActionStateTargeting.ResolveFacing(source, target);

            Assert.That(resolvedFacing, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Extended")]
        public void ContactSameCellAttackDecisionStrategy_RequiresExactSameCell()
        {
            var strategy = ContactSameCellAttackDecisionStrategy.Instance;
            var settings = AttackDecisionSettings.CreateDefaultMelee();
            var source = CreateUnit(
                entityId: 40,
                teamId: 2,
                position: new SurfaceCell(FaceId.Floor, 1, 1),
                aiMode: EnemyAiMode.Chase,
                facing: Direction.Left);
            var sameCellTarget = CreateUnit(
                entityId: 10,
                teamId: 1,
                position: new SurfaceCell(FaceId.Floor, 1, 1),
                aiMode: EnemyAiMode.None);
            var adjacentTarget = CreateUnit(
                entityId: 20,
                teamId: 1,
                position: new SurfaceCell(FaceId.Floor, 2, 1),
                aiMode: EnemyAiMode.None);

            Assert.That(strategy.IsTargetInRange(source, sameCellTarget, settings), Is.True);
            Assert.That(strategy.IsTargetInRange(source, adjacentTarget, settings), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_RecoverMode_DoesNotProduceMovementOrAttackIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Recover, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var movementBuffer = new List<RawMovementIntent>();
            var attackBuffer = new List<RawAttackIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), movementBuffer);
            logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), attackBuffer);

            Assert.That(movementBuffer, Is.Empty);
            Assert.That(attackBuffer, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_BeforeAttackStage_ReevaluatesPostMovementSnapshot_AndCommitsAttackMode()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var transitions = new List<string>();

            ((IEnemyAiStateLogic)logic).CommitAiTransitions(
                worldState.CreateSnapshot(),
                new TickInput(1),
                EnemyAiTransitionStage.BeforeAttack,
                worldState.CreateWriteContext(),
                transitions);

            var enemy = GetEntity(worldState, 40);

            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Attack));
            Assert.That(enemy.aiStateTimer, Is.Zero);
            CollectionAssert.AreEqual(
                new[]
                {
                    "EnemyAiTransition|Stage=BeforeAttack|E=40|From=Chase|FromTimer=0|To=Attack|ToTimer=0|Reason=TargetInRange|Facing=Right",
                },
                transitions);
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_DeadSource_CommitsDeadMode()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, hp: 0),
            });
            var logic = new EnemyLogic(entityId: 40);
            var transitions = new List<string>();

            ((IEnemyAiStateLogic)logic).CommitAiTransitions(
                worldState.CreateSnapshot(),
                new TickInput(1),
                EnemyAiTransitionStage.BeforeMovement,
                worldState.CreateWriteContext(),
                transitions);

            var enemy = GetEntity(worldState, 40);

            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Dead));
            Assert.That(enemy.aiStateTimer, Is.Zero);
            CollectionAssert.AreEqual(
                new[]
                {
                    "EnemyAiTransition|Stage=BeforeMovement|E=40|From=Chase|FromTimer=0|To=Dead|ToTimer=0|Reason=Dead|Facing=Right",
                },
                transitions);
        }

        [Test]
        [Category("Extended")]
        public void NearestOpponentDetectionStrategy_SenseRangeSetting_ChangesSelectionOutcome()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var strategy = NearestOpponentDetectionStrategy.Instance;

            var shortRangeDetected = strategy.TryFindTarget(
                snapshot,
                source,
                new DetectionSettings(senseRange: 2, requireSameFace: true, canTargetMarkedForDeath: false),
                out _);
            var longRangeDetected = strategy.TryFindTarget(
                snapshot,
                source,
                new DetectionSettings(senseRange: 3, requireSameFace: true, canTargetMarkedForDeath: false),
                out var detectedTarget);

            Assert.That(shortRangeDetected, Is.False);
            Assert.That(longRangeDetected, Is.True);
            Assert.That(detectedTarget.entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void ForwardPatrolStrategy_BlockedMovementResponseSetting_ChangesMovementOutcome()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var strategy = ForwardPatrolStrategy.Instance;

            var stopped = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateDefaultMelee(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop),
                out _);
            var steppedBackward = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateDefaultMelee(),
                new PatrolSettings(PatrolBlockedMovementResponse.TryStepBackward),
                out var backwardIntent);

            Assert.That(stopped, Is.False);
            Assert.That(steppedBackward, Is.True);
            Assert.That(backwardIntent.Destination, Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_RightHandWall_PrefersForwardWhileHandAnchorExists()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
            });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right);
            var strategy = WallFollowPatrolStrategy.Instance;

            var facingChanged = ((IPatrolFacingStrategy)strategy).TryResolveFacing(snapshot, source, settings, out var facing);
            var builtIntent = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateDefaultMelee(),
                settings,
                out var intent);

            Assert.That(facingChanged, Is.False);
            Assert.That(facing, Is.EqualTo(Direction.Left));
            Assert.That(EnemyMovementStrategyShared.HasWallFollowAnchor(snapshot, source, settings), Is.True);
            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_ResultAnchorForward_ReacquiresHandAnchorAfterStep()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right);
            var strategy = WallFollowPatrolStrategy.Instance;

            var chosen = EnemyMovementStrategyShared.TryChooseWallFollowDirection(snapshot, source, settings, out var direction);
            var builtIntent = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateDefaultMelee(),
                settings,
                out var intent);

            Assert.That(EnemyMovementStrategyShared.HasWallFollowAnchor(snapshot, source, settings), Is.False);
            Assert.That(chosen, Is.True);
            Assert.That(direction, Is.EqualTo(Direction.Up));
            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(0, 1)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_ResultAnchorPreferredTurn_RoundsConvexCorner()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 2), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right);
            var strategy = WallFollowPatrolStrategy.Instance;

            var facingChanged = ((IPatrolFacingStrategy)strategy).TryResolveFacing(snapshot, source, settings, out var facing);
            var builtIntent = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateDefaultMelee(),
                settings,
                out var intent);

            Assert.That(EnemyMovementStrategyShared.HasWallFollowAnchor(snapshot, source, settings), Is.False);
            Assert.That(facingChanged, Is.True);
            Assert.That(facing, Is.EqualTo(Direction.Right));
            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(1, 2)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowAnchor_TreatsBoxAsAnchor()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateBox(entityId: 50, position: new Vector2Int(1, 1)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            Assert.That(
                EnemyMovementStrategyShared.HasWallFollowAnchor(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, 40),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowResultAnchor_TreatsBoxAsAnchor()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateBox(entityId: 50, position: new Vector2Int(1, 1)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));

            Assert.That(
                EnemyMovementStrategyShared.TryChooseWallFollowDirection(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, 40),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right),
                    out var direction),
                Is.True);
            Assert.That(direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowAnchor_IgnoresUnitsIncludingPlayers()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            Assert.That(
                EnemyMovementStrategyShared.HasWallFollowAnchor(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, 40),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left)),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowAnchor_TreatsBoardEdgeAsAnchor()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));

            Assert.That(
                EnemyMovementStrategyShared.HasWallFollowAnchor(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, 40),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowResultAnchor_IgnoresUnitsIncludingPlayers()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));

            Assert.That(
                EnemyMovementStrategyShared.TryChooseWallFollowDirection(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, 40),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right),
                    out var direction),
                Is.True);
            Assert.That(direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Extended")]
        public void EnemyEntityLogicFactory_ProfileDrivenAssembly_UsesInjectedProfileSettings()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                DetectionSettings = new DetectionSettings(senseRange: 2, requireSameFace: true, canTargetMarkedForDeath: false),
            });

            try
            {
                var factory = new EnemyEntityLogicFactory(
                    profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
                var entity = CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right);
                var logic = factory.Create(entity);
                var worldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                    entity,
                });
                var transitions = new List<string>();

                ((IEnemyAiStateLogic)logic).CommitAiTransitions(
                    worldState.CreateSnapshot(),
                    new TickInput(1),
                    EnemyAiTransitionStage.BeforeMovement,
                    worldState.CreateWriteContext(),
                    transitions);

                Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Patrol));
                Assert.That(transitions, Has.Count.EqualTo(0));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_UsesDefaultZeroWindupAndMoveCooldown()
        {
            var profile = EnemyAiProfileTestFactory.CreateDefaultMelee();

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(definition.AttackTimingSettings.WindupTicks, Is.Zero);
                Assert.That(definition.LocomotionTimingSettings.MoveCooldownTicks, Is.Zero);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CanonicalFactoryProfiles_UseDefaultZeroMoveCooldown()
        {
            var profiles = new[]
            {
                EnemyAiProfileTestFactory.CreateDefaultMelee(),
                EnemyAiProfileTestFactory.CreateNonAttacking(),
                EnemyAiProfileTestFactory.CreateCharging(),
                EnemyAiProfileTestFactory.CreateWallFollower(),
            };

            try
            {
                foreach (var profile in profiles)
                {
                    Assert.That(profile.LocomotionTimingSettings.MoveCooldownSeconds, Is.Zero);
                    Assert.That(
                        profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond).LocomotionTimingSettings.MoveCooldownTicks,
                        Is.Zero);
                }
            }
            finally
            {
                foreach (var profile in profiles)
                {
                    DestroyProfile(profile);
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_ChargingFactoryProfile_UsesPassiveContactWithoutCombat()
        {
            var profile = EnemyAiProfileTestFactory.CreateCharging();

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(profile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Charge));
                Assert.That(profile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
                Assert.That(definition.Capabilities.TryGetCombat(out _), Is.False);
                Assert.That(definition.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
                Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_ChargingFactoryProfile_CanExplicitlyDisablePassiveContact()
        {
            var profile = EnemyAiProfileTestFactory.CreateCharging(includePassiveContact: false);

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(profile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Charge));
                Assert.That(profile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
                Assert.That(definition.Capabilities.TryGetCombat(out _), Is.False);
                Assert.That(definition.Capabilities.TryGetPassiveContact(out _), Is.False);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileCompiler_HybridAuthoring_CompilesTypedRuntimeAndCapabilities()
        {
            var profile = CreateHybridAuthoringProfile(includeCombat: true, includeJump: true, out var createdAssets);

            try
            {
                var definition = profile.CreateRuntimeDefinition(60);

                Assert.That(definition.Core.CommonSettings.RecoverTicks, Is.EqualTo(12));
                Assert.That(definition.Core.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(6));
                Assert.That(definition.Brain.StateResolver.Kind, Is.EqualTo(EnemyAiStateResolverKind.Default));
                Assert.That(definition.Brain.Patrol.Kind, Is.EqualTo(PatrolStrategyKind.Forward));
                Assert.That(definition.Brain.Detection.Kind, Is.EqualTo(DetectionStrategyKind.NearestOpponent));
                Assert.That(definition.Brain.Chase.Kind, Is.EqualTo(ChaseStrategyKind.AxisPriority));
                Assert.That(definition.Capabilities.TryGetCombat(out var combat), Is.True);
                Assert.That(combat.Kind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
                Assert.That(combat.AttackTimingSettings.WindupTicks, Is.EqualTo(9));
                Assert.That(definition.Capabilities.TryGetMovementSkill(out var movementSkill), Is.True);
                Assert.That(movementSkill.Kind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
                Assert.That(movementSkill.JumpTimingSettings.WindupTicks, Is.EqualTo(6));
                Assert.That(movementSkill.JumpTimingSettings.AirborneTicks, Is.EqualTo(12));
                Assert.That(movementSkill.JumpTimingSettings.CooldownTicks, Is.EqualTo(18));
            }
            finally
            {
                DestroyAuthoringObjects(profile, createdAssets);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileCompiler_HybridAuthoring_CompilesPassiveContactAlongsideCombatAndJump()
        {
            var profile = CreateHybridAuthoringProfile(
                includeCombat: true,
                includeJump: true,
                out var createdAssets,
                includePassiveContact: true);

            try
            {
                var definition = profile.CreateRuntimeDefinition(60);

                Assert.That(definition.Capabilities.TryGetCombat(out var combat), Is.True);
                Assert.That(combat.Kind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
                Assert.That(definition.Capabilities.TryGetMovementSkill(out var movementSkill), Is.True);
                Assert.That(movementSkill.Kind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
                Assert.That(definition.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
                Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));
            }
            finally
            {
                DestroyAuthoringObjects(profile, createdAssets);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileCompiler_HybridAuthoring_WithoutCombatCapability_DoesNotRequireCombatData()
        {
            var profile = CreateHybridAuthoringProfile(includeCombat: false, includeJump: false, out var createdAssets);

            try
            {
                var definition = profile.CreateRuntimeDefinition(60);

                Assert.That(definition.Capabilities.TryGetCombat(out _), Is.False);
                Assert.That(definition.Capabilities.TryGetMovementSkill(out _), Is.False);
                Assert.That(definition.Brain.Detection.Kind, Is.EqualTo(DetectionStrategyKind.NearestOpponent));
            }
            finally
            {
                DestroyAuthoringObjects(profile, createdAssets);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileCompiler_MissingCoreAuthoring_ThrowsClearException()
        {
            var profile = ScriptableObject.CreateInstance<EnemyAiProfile>();

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                Assert.That(exception.Message, Does.Contain(nameof(EnemyCoreAuthoring)));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileCompiler_MissingBrainAuthoring_ThrowsClearException()
        {
            var profile = CreateHybridAuthoringProfile(includeCombat: false, includeJump: false, out var createdAssets);
            SetSerializedField(profile, "brainAuthoring", null);

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                Assert.That(exception.Message, Does.Contain(nameof(EnemyBrainAuthoring)));
            }
            finally
            {
                DestroyAuthoringObjects(profile, createdAssets);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileCompiler_HybridAuthoring_DuplicateCombatCapabilities_ThrowsClearException()
        {
            var profile = CreateHybridAuthoringProfile(includeCombat: true, includeJump: false, out var createdAssets);
            var duplicateCombat = ScriptableObject.CreateInstance<MeleeCombatCapabilityAsset>();
            createdAssets.Add(duplicateCombat);
            SetSerializedField(duplicateCombat, "attackDecisionSettings", new AttackDecisionSettings(attackRange: 1));
            SetSerializedField(duplicateCombat, "attackTimingSettings", new EnemyAttackTimingAuthoringSettings(windupSeconds: 0f));
            SetSerializedField(
                profile,
                "capabilityAssets",
                new List<EnemyCapabilityAsset>
                {
                    (EnemyCapabilityAsset)createdAssets.OfType<MeleeCombatCapabilityAsset>().First(),
                    duplicateCombat,
                });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                Assert.That(exception.Message, Does.Contain("multiple combat capabilities"));
            }
            finally
            {
                DestroyAuthoringObjects(profile, createdAssets);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileCompiler_HybridAuthoring_DuplicatePassiveContactCapabilities_ThrowsClearException()
        {
            var profile = CreateHybridAuthoringProfile(
                includeCombat: false,
                includeJump: false,
                out var createdAssets,
                includePassiveContact: true);
            var duplicatePassiveContact = ScriptableObject.CreateInstance<EnemyPassiveContactCapabilityAsset>();
            createdAssets.Add(duplicatePassiveContact);
            SetSerializedField(
                profile,
                "capabilityAssets",
                new List<EnemyCapabilityAsset>
                {
                    (EnemyCapabilityAsset)createdAssets.OfType<EnemyPassiveContactCapabilityAsset>().First(),
                    duplicatePassiveContact,
                });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                Assert.That(exception.Message, Does.Contain("multiple passive contact capabilities"));
            }
            finally
            {
                DestroyAuthoringObjects(profile, createdAssets);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileCompiler_HybridAuthoring_DuplicateMovementSkillCapabilities_ThrowsClearException()
        {
            var profile = CreateHybridAuthoringProfile(includeCombat: false, includeJump: true, out var createdAssets);
            var duplicateJump = ScriptableObject.CreateInstance<JumpToLockedTargetCapabilityAsset>();
            createdAssets.Add(duplicateJump);
            SetSerializedField(duplicateJump, "jumpTimingSettings", new EnemyJumpTimingAuthoringSettings(0.1f, 0.2f, 0.3f));
            SetSerializedField(
                profile,
                "capabilityAssets",
                new List<EnemyCapabilityAsset>
                {
                    (EnemyCapabilityAsset)createdAssets.OfType<JumpToLockedTargetCapabilityAsset>().First(),
                    duplicateJump,
                });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                Assert.That(exception.Message, Does.Contain("multiple movement skill capabilities"));
            }
            finally
            {
                DestroyAuthoringObjects(profile, createdAssets);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_JumpChaserProfile_OnlyCompilesJumpCapability()
        {
            var profile = EnemyAiProfileTestFactory.CreateJumpChaser(
                new EnemyJumpTimingSettings(windupTicks: 1, airborneTicks: 2, cooldownTicks: 3));

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(definition.Capabilities.TryGetCombat(out _), Is.False);
                Assert.That(definition.Capabilities.TryGetMovementSkill(out var movementSkill), Is.True);
                Assert.That(movementSkill.Kind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
                Assert.That(movementSkill.JumpTimingSettings.WindupTicks, Is.EqualTo(1));
                Assert.That(movementSkill.JumpTimingSettings.AirborneTicks, Is.EqualTo(2));
                Assert.That(movementSkill.JumpTimingSettings.CooldownTicks, Is.EqualTo(3));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayEntityLogicProviderFactory_NonAttackingProfile_OmitsCombatLogicsFromEntitySet()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking();

            try
            {
                var provider = GameplayEntityLogicProviderFactory.CreateDefault(profile);
                var logicSet = provider.Build(worldState.CreateSnapshot(), Array.Empty<IEntityLogic>());

                Assert.That(logicSet.AiStateLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.PreMovementStateLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.MovementLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.EnemyActionStateLogics, Is.Empty);
                Assert.That(logicSet.AttackLogics, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayEntityLogicProviderFactory_PassiveContactOnlyProfile_ArmsAttackLogicWithoutEnemyActionState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking(includePassiveContact: true);

            try
            {
                var provider = GameplayEntityLogicProviderFactory.CreateDefault(profile);
                var logicSet = provider.Build(worldState.CreateSnapshot(), Array.Empty<IEntityLogic>());

                Assert.That(logicSet.AiStateLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.PreMovementStateLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.MovementLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.EnemyActionStateLogics, Is.Empty);
                Assert.That(logicSet.AttackLogics, Has.Count.EqualTo(1));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayEntityLogicProviderFactory_NullProfile_UsesDefaultMeleeRuntimeDefinition()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var provider = GameplayEntityLogicProviderFactory.CreateDefault((EnemyAiProfile)null);
            var logicSet = provider.Build(worldState.CreateSnapshot(), Array.Empty<IEntityLogic>());

            Assert.That(logicSet.AiStateLogics, Has.Count.EqualTo(1));
            Assert.That(logicSet.PreMovementStateLogics, Has.Count.EqualTo(1));
            Assert.That(logicSet.MovementLogics, Has.Count.EqualTo(1));
            Assert.That(logicSet.EnemyActionStateLogics, Has.Count.EqualTo(1));
            Assert.That(logicSet.AttackLogics, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpProfile_InspectorTimings_AreSeconds_AndConvertToTicks()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(movementPriority: 50, attackPriority: 50, recoverSeconds: 0f),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                AttackTimingSettings = new EnemyAttackTimingAuthoringSettings(windupSeconds: 0f),
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(moveCooldownSeconds: 0f),
                MovementSkillStrategyKind = MovementSkillStrategyKind.JumpToLockedTarget,
                JumpTimingSettings = new EnemyJumpTimingAuthoringSettings(
                    windupSeconds: 0.1f,
                    airborneSeconds: 0.2f,
                    cooldownSeconds: 0.3f),
            });

            try
            {
                var definition = profile.CreateRuntimeDefinition(60);

                Assert.That(profile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
                Assert.That(profile.JumpTimingSettings.WindupSeconds, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(profile.JumpTimingSettings.AirborneSeconds, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(profile.JumpTimingSettings.CooldownSeconds, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(definition.JumpTimingSettings.WindupTicks, Is.EqualTo(6));
                Assert.That(definition.JumpTimingSettings.AirborneTicks, Is.EqualTo(12));
                Assert.That(definition.JumpTimingSettings.CooldownTicks, Is.EqualTo(18));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_JumpProfile_SerializedFields_RemainLogicOnlyContract()
        {
            var serializedFieldNames = typeof(EnemyAiProfile)
                .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Where(field =>
                    (field.IsPublic || field.GetCustomAttributes(typeof(SerializeField), inherit: false).Length > 0) &&
                    field.GetCustomAttributes(typeof(HideInInspector), inherit: false).Length == 0)
                .Select(field => field.Name)
                .OrderBy(name => name)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "brainAuthoring",
                    "capabilityAssets",
                    "coreAuthoring",
                },
                serializedFieldNames);
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_AtDefaultSimulationRate_PreservesAuthoringSecondsSemantics()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(
                    movementPriority: 50,
                    attackPriority: 50,
                    recoverSeconds: 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                AttackTimingSettings = new EnemyAttackTimingAuthoringSettings(
                    windupSeconds: 3f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(
                    moveCooldownSeconds: 4f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(definition.CommonSettings.RecoverTicks, Is.EqualTo(1));
                Assert.That(definition.AttackTimingSettings.WindupTicks, Is.EqualTo(3));
                Assert.That(definition.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(4));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_ChangingSimulationTicksPerSecond_PreservesAuthoringTimeMeaning()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(
                    movementPriority: 50,
                    attackPriority: 50,
                    recoverSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                AttackTimingSettings = new EnemyAttackTimingAuthoringSettings(
                    windupSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(
                    moveCooldownSeconds: 2f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });

            try
            {
                var sixtyTpsDefinition = profile.CreateRuntimeDefinition(60);
                var thirtyTpsDefinition = profile.CreateRuntimeDefinition(30);

                Assert.That(sixtyTpsDefinition.CommonSettings.RecoverTicks, Is.EqualTo(2));
                Assert.That(thirtyTpsDefinition.CommonSettings.RecoverTicks, Is.EqualTo(1));
                Assert.That(sixtyTpsDefinition.AttackTimingSettings.WindupTicks, Is.EqualTo(2));
                Assert.That(thirtyTpsDefinition.AttackTimingSettings.WindupTicks, Is.EqualTo(1));
                Assert.That(sixtyTpsDefinition.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(2));
                Assert.That(thirtyTpsDefinition.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(1));
                Assert.That(sixtyTpsDefinition.CommonSettings.RecoverTicks / 60f, Is.EqualTo(thirtyTpsDefinition.CommonSettings.RecoverTicks / 30f).Within(0.0001f));
                Assert.That(sixtyTpsDefinition.AttackTimingSettings.WindupTicks / 60f, Is.EqualTo(thirtyTpsDefinition.AttackTimingSettings.WindupTicks / 30f).Within(0.0001f));
                Assert.That(
                    sixtyTpsDefinition.LocomotionTimingSettings.MoveCooldownTicks / 60f,
                    Is.EqualTo(thirtyTpsDefinition.LocomotionTimingSettings.MoveCooldownTicks / 30f).Within(0.0001f));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_ZeroSeconds_AllowsZeroWindupRecoverAndMoveCooldownTicks()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(movementPriority: 50, attackPriority: 50, recoverSeconds: 0f),
                AttackTimingSettings = new EnemyAttackTimingAuthoringSettings(windupSeconds: 0f),
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(moveCooldownSeconds: 0f),
            });

            try
            {
                var definition = profile.CreateRuntimeDefinition(30);

                Assert.That(definition.CommonSettings.RecoverTicks, Is.Zero);
                Assert.That(definition.AttackTimingSettings.WindupTicks, Is.Zero);
                Assert.That(definition.LocomotionTimingSettings.MoveCooldownTicks, Is.Zero);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_ChargeProfile_ConvertsWindupStepCooldownAndRecoverSecondsToTicks()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(movementPriority: 50, attackPriority: 50, recoverSeconds: 0f),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(moveCooldownSeconds: 5f / 60f),
                ChargeTimingSettings = new EnemyChargeTimingAuthoringSettings(
                    windupSeconds: 2f / 60f,
                    activeStepCooldownSeconds: 3f / 60f,
                    recoverSeconds: 4f / 60f),
                StateResolverKind = EnemyAiStateResolverKind.Charge,
            });

            try
            {
                var definition = profile.CreateRuntimeDefinition(60);

                Assert.That(definition.ChargeTimingSettings.WindupTicks, Is.EqualTo(2));
                Assert.That(definition.ChargeTimingSettings.ActiveStepCooldownTicks, Is.EqualTo(3));
                Assert.That(definition.ChargeTimingSettings.RecoverTicks, Is.EqualTo(4));
                Assert.That(definition.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(5));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_NegativeSeconds_ThrowsArgumentException()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(movementPriority: 50, attackPriority: 50, recoverSeconds: -0.1f),
                AttackTimingSettings = new EnemyAttackTimingAuthoringSettings(windupSeconds: 0f),
            });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                Assert.That(exception.ParamName, Is.EqualTo("EnemyAiCommonAuthoringSettings"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_NegativeMoveCooldownSeconds_ThrowsArgumentException()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(movementPriority: 50, attackPriority: 50, recoverSeconds: 0f),
                AttackTimingSettings = new EnemyAttackTimingAuthoringSettings(windupSeconds: 0f),
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(moveCooldownSeconds: -0.1f),
            });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                Assert.That(exception.ParamName, Is.EqualTo("EnemyLocomotionTimingAuthoringSettings"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfile_CreateRuntimeDefinition_NegativeChargeStepCooldownSeconds_ThrowsArgumentException()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                CommonSettings = new EnemyAiCommonAuthoringSettings(movementPriority: 50, attackPriority: 50, recoverSeconds: 0f),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                ChargeTimingSettings = new EnemyChargeTimingAuthoringSettings(
                    windupSeconds: 0f,
                    activeStepCooldownSeconds: -0.1f,
                    recoverSeconds: 0f),
                StateResolverKind = EnemyAiStateResolverKind.Charge,
            });

            try
            {
                var exception = Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));

                Assert.That(exception.ParamName, Is.EqualTo("EnemyChargeTimingAuthoringSettings"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiRuntimeDefinition_NegativeWindupTicks_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new EnemyAiRuntimeDefinition(
                    EnemyAiCommonSettings.CreateDefaultMelee(),
                    PatrolSettings.CreateDefault(),
                    DetectionSettings.CreateDefaultMelee(),
                    ChaseSettings.CreateDefault(),
                    AttackDecisionSettings.CreateDefaultMelee(),
                    new EnemyAttackTimingSettings(windupTicks: -1),
                    ForwardPatrolStrategy.Instance,
                    NearestOpponentDetectionStrategy.Instance,
                    AxisPriorityChaseStrategy.Instance,
                    MeleeAttackDecisionStrategy.Instance,
                    DefaultEnemyAiStateResolver.Instance));

            Assert.That(exception.ParamName, Is.EqualTo("EnemyAiRuntimeDefinition"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiRuntimeDefinition_NegativeMoveCooldownTicks_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new EnemyAiRuntimeDefinition(
                    EnemyAiCommonSettings.CreateDefaultMelee(),
                    PatrolSettings.CreateDefault(),
                    DetectionSettings.CreateDefaultMelee(),
                    ChaseSettings.CreateDefault(),
                    AttackDecisionSettings.CreateDefaultMelee(),
                    EnemyAttackTimingSettings.CreateDefaultMelee(),
                    new EnemyLocomotionTimingSettings(moveCooldownTicks: -1),
                    ForwardPatrolStrategy.Instance,
                    NearestOpponentDetectionStrategy.Instance,
                    AxisPriorityChaseStrategy.Instance,
                    MeleeAttackDecisionStrategy.Instance,
                    DefaultEnemyAiStateResolver.Instance));

            Assert.That(exception.ParamName, Is.EqualTo("EnemyAiRuntimeDefinition"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiRuntimeDefinition_NegativeDesiredChaseDistance_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new EnemyAiRuntimeDefinition(
                    EnemyAiCommonSettings.CreateDefaultMelee(),
                    PatrolSettings.CreateDefault(),
                    DetectionSettings.CreateDefaultMelee(),
                    new ChaseSettings(
                        ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak,
                        trySecondaryAxisWhenBlocked: true,
                        desiredChaseDistance: -1),
                    AttackDecisionSettings.CreateDefaultMelee(),
                    EnemyAttackTimingSettings.CreateDefaultMelee(),
                    EnemyLocomotionTimingSettings.CreateDefaultMelee(),
                    ForwardPatrolStrategy.Instance,
                    NearestOpponentDetectionStrategy.Instance,
                    AxisPriorityChaseStrategy.Instance,
                    MeleeAttackDecisionStrategy.Instance,
                    DefaultEnemyAiStateResolver.Instance));

            Assert.That(exception.ParamName, Is.EqualTo("EnemyAiRuntimeDefinition"));
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, Game.Feature.Gameplay.BoardState.TerrainData.Empty);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32)),
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                topology);
        }

        private static Vector2Int GetEntityPosition(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity.position.PlanarPosition;
        }

        private static int GetEntityHp(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity.hp;
        }

        private static EnemyActionRuntimeState GetEnemyActionState(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(entityId, out var actionState), Is.True);
            return actionState;
        }

        private static EnemyPatrolRuntimeState GetEnemyPatrolState(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyPatrolState(entityId, out var patrolState), Is.True);
            return patrolState;
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EntityState GetEntityAfterTick(TickResult tickResult, int entityId)
        {
            return tickResult.FinalEntities.Single(entity => entity.entityId == entityId);
        }

        private static EnemyAiProfile CreateEnemyProfile(int windupTicks, int moveCooldownTicks = 0)
        {
            return EnemyAiProfileTestFactory.CreateDefaultMelee(windupTicks, moveCooldownTicks);
        }

        private static EnemyAiProfile CreateChargingEnemyProfile(int moveCooldownTicks)
        {
            return EnemyAiProfileTestFactory.CreateCharging(moveCooldownTicks);
        }

        private static EnemyAiProfile CreateNonAttackingEnemyProfile(int moveCooldownTicks = 0)
        {
            return EnemyAiProfileTestFactory.CreateNonAttacking(moveCooldownTicks);
        }

        private static EnemyAiProfile CreateWallFollowerProfile(
            WallFollowTurnPreference turnPreference,
            int moveCooldownTicks = 0)
        {
            return EnemyAiProfileTestFactory.CreateWallFollower(turnPreference, moveCooldownTicks);
        }

        private static EnemyAiProfile CreateJumpPatrolProfile()
        {
            return EnemyAiProfileTestFactory.CreateJumpPatrol(
                new EnemyJumpTimingSettings(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1));
        }

        private static EnemyAiProfile CreateJumpEnemyProfile(
            AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.Melee)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = attackDecisionStrategyKind,
                MovementSkillStrategyKind = MovementSkillStrategyKind.JumpToLockedTarget,
                JumpTimingSettings = EnemyJumpTimingAuthoringSettings.FromRuntimeSettings(
                    new EnemyJumpTimingSettings(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1),
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });
        }

        private static void DestroyProfile(EnemyAiProfile profile)
        {
            EnemyAiProfileTestFactory.Destroy(profile);
        }

        private static EnemyJumpRuntimeState CreateEnemyJumpState(
            EnemyJumpPhase phase,
            SurfaceCell sourceCell,
            SurfaceCell lockedTargetCell,
            int landingTick,
            int cooldownRemainingTicks = 0)
        {
            return new EnemyJumpRuntimeState
            {
                phase = phase,
                sequence = 1,
                sourceCell = sourceCell,
                lockedTargetCell = lockedTargetCell,
                windupEndTick = landingTick - 1,
                landingTick = landingTick,
                cooldownRemainingTicks = cooldownRemainingTicks,
                retryCount = 0,
            };
        }

        private static EnemyAiProfile CreateHybridAuthoringProfile(
            bool includeCombat,
            bool includeJump,
            out List<ScriptableObject> createdAssets,
            bool includePassiveContact = false)
        {
            createdAssets = new List<ScriptableObject>();

            var profile = ScriptableObject.CreateInstance<EnemyAiProfile>();
            var core = ScriptableObject.CreateInstance<EnemyCoreAuthoring>();
            var brain = ScriptableObject.CreateInstance<EnemyBrainAuthoring>();
            var stateResolver = ScriptableObject.CreateInstance<DefaultEnemyStateResolverAsset>();
            var patrol = ScriptableObject.CreateInstance<ForwardPatrolAsset>();
            var detection = ScriptableObject.CreateInstance<NearestOpponentDetectionAsset>();
            var chase = ScriptableObject.CreateInstance<AxisPriorityChaseAsset>();
            createdAssets.AddRange(new ScriptableObject[] { core, brain, stateResolver, patrol, detection, chase });

            SetSerializedField(
                core,
                "commonSettings",
                new EnemyAiCommonAuthoringSettings(
                    movementPriority: 50,
                    attackPriority: 75,
                    recoverSeconds: 0.2f));
            SetSerializedField(
                core,
                "locomotionTimingSettings",
                new EnemyLocomotionTimingAuthoringSettings(moveCooldownSeconds: 0.1f));
            SetSerializedField(brain, "stateResolver", stateResolver);
            SetSerializedField(brain, "patrolStrategy", patrol);
            SetSerializedField(brain, "detectionStrategy", detection);
            SetSerializedField(brain, "chaseStrategy", chase);
            SetSerializedField(detection, "senseRange", 8);
            SetSerializedField(detection, "requireSameFace", true);
            SetSerializedField(detection, "canTargetMarkedForDeath", false);
            SetSerializedField(chase, "axisPriority", ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak);
            SetSerializedField(chase, "trySecondaryAxisWhenBlocked", true);
            SetSerializedField(chase, "desiredChaseDistance", 0);

            var capabilities = new List<EnemyCapabilityAsset>();
            if (includeCombat)
            {
                var melee = ScriptableObject.CreateInstance<MeleeCombatCapabilityAsset>();
                SetSerializedField(melee, "attackDecisionSettings", new AttackDecisionSettings(attackRange: 1));
                SetSerializedField(melee, "attackTimingSettings", new EnemyAttackTimingAuthoringSettings(windupSeconds: 0.15f));
                capabilities.Add(melee);
                createdAssets.Add(melee);
            }

            if (includeJump)
            {
                var jump = ScriptableObject.CreateInstance<JumpToLockedTargetCapabilityAsset>();
                SetSerializedField(jump, "jumpTimingSettings", new EnemyJumpTimingAuthoringSettings(0.1f, 0.2f, 0.3f));
                capabilities.Add(jump);
                createdAssets.Add(jump);
            }

            if (includePassiveContact)
            {
                var passiveContact = ScriptableObject.CreateInstance<EnemyPassiveContactCapabilityAsset>();
                capabilities.Add(passiveContact);
                createdAssets.Add(passiveContact);
            }

            SetSerializedField(profile, "coreAuthoring", core);
            SetSerializedField(profile, "brainAuthoring", brain);
            SetSerializedField(profile, "capabilityAssets", capabilities);
            return profile;
        }

        private static void DestroyAuthoringObjects(
            EnemyAiProfile profile,
            IEnumerable<ScriptableObject> createdAssets)
        {
            if (createdAssets != null)
            {
                foreach (var asset in createdAssets)
                {
                    if (asset != null)
                    {
                        UnityEngine.Object.DestroyImmediate(asset);
                    }
                }
            }

            if (profile != null)
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static void SetSerializedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            EnemyAiMode aiMode,
            Direction facing = Direction.Right,
            int hp = 3,
            int aiStateTimer = 0,
            int enemyLocomotionCooldownTicks = 0,
            UnitRole unitRole = UnitRole.None)
        {
            var resolvedUnitRole = unitRole != UnitRole.None
                ? unitRole
                : teamId switch
                {
                    1 => UnitRole.Player,
                    2 => UnitRole.Enemy,
                    _ => UnitRole.None,
                };

            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = resolvedUnitRole,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
                aiStateTimer = aiStateTimer,
                enemyLocomotionCooldownTicks = enemyLocomotionCooldownTicks,
            };
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            EnemyAiMode aiMode,
            Direction facing = Direction.Right,
            int hp = 3,
            int aiStateTimer = 0,
            int enemyLocomotionCooldownTicks = 0,
            UnitRole unitRole = UnitRole.None)
        {
            var resolvedUnitRole = unitRole != UnitRole.None
                ? unitRole
                : teamId switch
                {
                    1 => UnitRole.Player,
                    2 => UnitRole.Enemy,
                    _ => UnitRole.None,
                };

            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = resolvedUnitRole,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
                aiStateTimer = aiStateTimer,
                enemyLocomotionCooldownTicks = enemyLocomotionCooldownTicks,
            };
        }

        private static EntityState CreateBox(int entityId, Vector2Int position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                boxCapabilities = BoxCapabilities.None,
                aiMode = EnemyAiMode.None,
                aiStateTimer = 0,
            };
        }

        private static EntityState CreateWall(int entityId, Vector2Int position)
        {
            return CreateWall(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = EnemyAiMode.None,
                aiStateTimer = 0,
            };
        }

        private static MoveIntent CreateMoveIntent(int sourceId, int priority, Vector2Int destination, int intentId)
        {
            var intent = new MoveIntent(sourceId, priority, destination);
            intent.AssignIntentId(intentId);
            return intent;
        }
    }
}
