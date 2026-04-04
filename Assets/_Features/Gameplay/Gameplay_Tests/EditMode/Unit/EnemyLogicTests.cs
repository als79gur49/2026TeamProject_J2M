using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Model.Groups;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyLogicTests
    {
        [Test]
        public void EnemyLogic_ImplementsMovementAndAttackContracts()
        {
            var logic = new EnemyLogic(entityId: 40);

            Assert.That(logic, Is.InstanceOf<IMovementEntityLogic>());
            Assert.That(logic, Is.InstanceOf<IAttackEntityLogic>());
            Assert.That(logic.ControlledEntityId, Is.EqualTo(40));
        }

        [Test]
        public void EnemyLogic_InvalidConfig_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(() => new EnemyLogic(entityId: 40, default(EnemyAiRuntimeDefinition)));

            Assert.That(exception.ParamName, Is.EqualTo("aiDefinition"));
        }

        [Test]
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
        public void EnemyEntityLogicFactory_ProfileDrivenAssembly_UsesInjectedProfileSettings()
        {
            var profile = EnemyAiProfile.CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                new DetectionSettings(senseRange: 2, requireSameFace: true, canTargetMarkedForDeath: false),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee());
            var factory = new EnemyEntityLogicFactory(profile);
            var entity = CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right);
            var logic = (EnemyLogic)factory.Create(entity);
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

        [Test]
        public void EnemyAiProfile_CreateRuntimeDefinition_UsesDefaultZeroWindup()
        {
            var profile = EnemyAiProfile.CreateRuntimeDefault();

            var definition = profile.CreateRuntimeDefinition();

            Assert.That(definition.AttackTimingSettings.WindupTicks, Is.Zero);
        }

        [Test]
        public void EnemyAiProfile_CreateRuntimeDefinition_PreservesConfiguredWindupTicks()
        {
            var profile = EnemyAiProfile.CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                new EnemyAttackTimingSettings(windupTicks: 3));

            var definition = profile.CreateRuntimeDefinition();

            Assert.That(definition.AttackTimingSettings.WindupTicks, Is.EqualTo(3));
        }

        [Test]
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
        public void DefaultEntityLogicProvider_PatrolEnemy_IsMaterializedDuringTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, Destination: new Vector2Int(1, 0)),
                },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.Destination))
                    .ToArray());
            Assert.That(GetEntityPosition(worldState, 40), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void DefaultEntityLogicProvider_PatrolEnemy_SensesOpponent_TransitionsToChaseAndMovesInSameTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Patrol"));
            Assert.That(GetEntityPosition(worldState, 40), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Chase));
        }

        [Test]
        public void DefaultEntityLogicProvider_AttackEnemy_IsMaterializedDuringTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, TargetId: 10),
                },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(GetEntityHp(worldState, 10), Is.EqualTo(2));
        }

        [Test]
        public void DefaultEntityLogicProvider_AttackEnemy_WithWindup_StartTick_ArmsActionWithoutAttack()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Attack, facing: Direction.Up),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, CreateEnemyProfile(windupTicks: 2));

            var result = pipeline.RunTick(new TickInput(1));
            var enemy = GetEntity(worldState, 40);
            var actionState = GetEnemyActionState(worldState, 40);

            Assert.That(result.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(result.AttackPhaseResult.SortedInputs, Is.Empty);
            Assert.That(GetEntityHp(worldState, 10), Is.EqualTo(3));
            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Attack));
            Assert.That(actionState.IsActive, Is.True);
            Assert.That(actionState.executeTick, Is.EqualTo(3));
            Assert.That(actionState.executionAttempted, Is.False);
            Assert.That(enemy.facing, Is.EqualTo(Direction.Right));
            Assert.That(result.Trace.Text, Does.Contain("EnemyAction.BeforeAttackCollectionTransitions"));
        }

        [Test]
        public void DefaultEntityLogicProvider_AttackEnemy_WithWindup_ExecutesOnlyOnExecuteTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, CreateEnemyProfile(windupTicks: 1));

            var windupResult = pipeline.RunTick(new TickInput(1));
            var executeResult = pipeline.RunTick(new TickInput(2));
            var enemy = GetEntity(worldState, 40);
            var actionState = GetEnemyActionState(worldState, 40);

            Assert.That(windupResult.AttackPhaseResult.SortedInputs, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, TargetId: 10),
                },
                executeResult.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(GetEntityHp(worldState, 10), Is.EqualTo(2));
            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemy.aiStateTimer, Is.EqualTo(1));
            Assert.That(actionState.IsActive, Is.True);
            Assert.That(actionState.executionAttempted, Is.True);
        }

        [Test]
        public void DefaultEntityLogicProvider_WindupEnemy_LosesLockedTarget_CancelsActionAndFallsBackToPatrol()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, CreateEnemyProfile(windupTicks: 2));

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().ApplyDamage(10, 3);

            var result = pipeline.RunTick(new TickInput(2));
            var enemy = GetEntity(worldState, 40);
            var actionState = GetEnemyActionState(worldState, 40);

            Assert.That(result.AttackPhaseResult.SortedInputs, Is.Empty);
            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(actionState.IsActive, Is.False);
            Assert.That(result.Trace.Text, Does.Contain("LockedTargetLost"));
        }

        [Test]
        public void DefaultEntityLogicProvider_ChaseEnemy_InAttackRange_TransitionsToRecoverAfterAttack()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1));
            var enemy = GetEntity(worldState, 40);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, TargetId: 10),
                },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.SourceId, intent.TargetId))
                    .ToArray());
            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemy.aiStateTimer, Is.EqualTo(1));
            Assert.That(result.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Chase"));
            Assert.That(result.Trace.Text, Does.Contain("EnemyAiTransition|Stage=AfterAttack|E=40|From=Attack"));
        }

        [Test]
        public void DefaultEntityLogicProvider_RecoverEnemy_CountsDownThenReturnsToChase()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Recover, aiStateTimer: 1, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var firstResult = pipeline.RunTick(new TickInput(1));
            var firstTickEnemy = GetEntity(worldState, 40);

            Assert.That(firstResult.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(firstResult.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(firstTickEnemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(firstTickEnemy.aiStateTimer, Is.EqualTo(0));

            var secondResult = pipeline.RunTick(new TickInput(2));
            var secondTickEnemy = GetEntity(worldState, 40);

            Assert.That(secondTickEnemy.aiMode, Is.EqualTo(EnemyAiMode.Chase));
            Assert.That(secondTickEnemy.aiStateTimer, Is.EqualTo(0));
            Assert.That(GetEntityPosition(worldState, 40), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(secondResult.Trace.Text, Does.Contain("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Recover|FromTimer=0|To=Chase"));
        }

        [Test]
        public void DefaultEntityLogicProvider_ChaseEnemy_LosesTarget_RevertsToPatrolAndPatrolMoves()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            pipeline.RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, 40).aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(GetEntityPosition(worldState, 40), Is.EqualTo(new Vector2Int(1, 0)));
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

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EnemyAiProfile CreateEnemyProfile(int windupTicks)
        {
            return EnemyAiProfile.CreateRuntimeInstance(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                new EnemyAttackTimingSettings(windupTicks));
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            EnemyAiMode aiMode,
            Direction facing = Direction.Right,
            int hp = 3,
            int aiStateTimer = 0)
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
                aiStateTimer = aiStateTimer,
            };
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            EnemyAiMode aiMode,
            Direction facing = Direction.Right,
            int hp = 3,
            int aiStateTimer = 0)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
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
                aiStateTimer = aiStateTimer,
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
