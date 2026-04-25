using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyLogicTests
    {
        private static EnemyUnitArchetypeAsset SharedSummonedArchetype;
        private static EnemyAiProfile SharedSummonedProfile;

        private readonly struct ForwardPatrolFixture
        {
            public ForwardPatrolFixture(string label, WorldState worldState, PatrolSettings settings)
            {
                Label = label;
                WorldState = worldState;
                Settings = settings;
            }

            public string Label { get; }

            public WorldState WorldState { get; }

            public PatrolSettings Settings { get; }
        }

        private readonly struct RandomWalkScorecardMetrics
        {
            public RandomWalkScorecardMetrics(
                int maxHomeDistance,
                int forwardCommittedMoves,
                int scoredMoves,
                int committedMoves,
                int immediateBacktrackCount,
                int avoidableImmediateBacktrackCount)
            {
                MaxHomeDistance = maxHomeDistance;
                ForwardCommittedMoves = forwardCommittedMoves;
                ScoredMoves = scoredMoves;
                CommittedMoves = committedMoves;
                ImmediateBacktrackCount = immediateBacktrackCount;
                AvoidableImmediateBacktrackCount = avoidableImmediateBacktrackCount;
            }

            public int MaxHomeDistance { get; }

            public int ForwardCommittedMoves { get; }

            public int ScoredMoves { get; }

            public int CommittedMoves { get; }

            public int ImmediateBacktrackCount { get; }

            public int AvoidableImmediateBacktrackCount { get; }

            public float ForwardCommittedShare => ScoredMoves == 0 ? 0f : (float)ForwardCommittedMoves / ScoredMoves;
        }

        private enum PatrolWriteTriageKind
        {
            None,
            ActualDuplicateWrite,
            TraceOnlyDuplication,
            SemanticCorrelationMismatch,
        }

        private readonly struct PatrolWriteTriageMetrics
        {
            public PatrolWriteTriageMetrics(
                int initializedWriteCount,
                int expectedCommittedMoveCount,
                int actualCommittedMoveWrites,
                int traceCommittedMoveCount,
                int semanticCommittedMoveCount,
                int movementSectionCommittedMoveCount,
                int eventLogSectionCommittedMoveCount)
            {
                InitializedWriteCount = initializedWriteCount;
                ExpectedCommittedMoveCount = expectedCommittedMoveCount;
                ActualCommittedMoveWrites = actualCommittedMoveWrites;
                TraceCommittedMoveCount = traceCommittedMoveCount;
                SemanticCommittedMoveCount = semanticCommittedMoveCount;
                MovementSectionCommittedMoveCount = movementSectionCommittedMoveCount;
                EventLogSectionCommittedMoveCount = eventLogSectionCommittedMoveCount;
            }

            public int InitializedWriteCount { get; }

            public int ExpectedCommittedMoveCount { get; }

            public int ActualCommittedMoveWrites { get; }

            public int TraceCommittedMoveCount { get; }

            public int SemanticCommittedMoveCount { get; }

            public int MovementSectionCommittedMoveCount { get; }

            public int EventLogSectionCommittedMoveCount { get; }

            public PatrolWriteTriageKind Classification
            {
                get
                {
                    if (ActualCommittedMoveWrites > ExpectedCommittedMoveCount)
                    {
                        return PatrolWriteTriageKind.ActualDuplicateWrite;
                    }

                    if (TraceCommittedMoveCount > ActualCommittedMoveWrites)
                    {
                        return PatrolWriteTriageKind.TraceOnlyDuplication;
                    }

                    if (SemanticCommittedMoveCount != ActualCommittedMoveWrites)
                    {
                        return PatrolWriteTriageKind.SemanticCorrelationMismatch;
                    }

                    return PatrolWriteTriageKind.None;
                }
            }
        }

        private readonly struct WindupContractMetrics
        {
            public WindupContractMetrics(
                int targetSensedTick,
                int targetInRangeTick,
                int attackEntryTick,
                int actionStartTargetResolvedTick,
                int actionStartBlockedByMoveLockTick,
                int actionStartActiveWithoutSignalTick,
                int actionStartTick,
                int attackExecuteRawIntentTick,
                int attackExecuteRejectedByExecutionLockTick,
                int attackExecuteTick,
                int attackExecuteActionStateActiveTick,
                int attackExecuteExecutionAttemptedTick,
                int attackCommittedTraceTick,
                int attackCommittedRecoverTick,
                int recoverEntryTick,
                int recoverCompleteTick,
                int recoverTickCount,
                int recoverPatrolWriteCount,
                int firstCombatDamageTick)
            {
                TargetSensedTick = targetSensedTick;
                TargetInRangeTick = targetInRangeTick;
                AttackEntryTick = attackEntryTick;
                ActionStartTargetResolvedTick = actionStartTargetResolvedTick;
                ActionStartBlockedByMoveLockTick = actionStartBlockedByMoveLockTick;
                ActionStartActiveWithoutSignalTick = actionStartActiveWithoutSignalTick;
                ActionStartTick = actionStartTick;
                AttackExecuteRawIntentTick = attackExecuteRawIntentTick;
                AttackExecuteRejectedByExecutionLockTick = attackExecuteRejectedByExecutionLockTick;
                AttackExecuteTick = attackExecuteTick;
                AttackExecuteActionStateActiveTick = attackExecuteActionStateActiveTick;
                AttackExecuteExecutionAttemptedTick = attackExecuteExecutionAttemptedTick;
                AttackCommittedTraceTick = attackCommittedTraceTick;
                AttackCommittedRecoverTick = attackCommittedRecoverTick;
                RecoverEntryTick = recoverEntryTick;
                RecoverCompleteTick = recoverCompleteTick;
                RecoverTickCount = recoverTickCount;
                RecoverPatrolWriteCount = recoverPatrolWriteCount;
                FirstCombatDamageTick = firstCombatDamageTick;
            }

            public int TargetSensedTick { get; }

            public int TargetInRangeTick { get; }

            public int AttackEntryTick { get; }

            public int ActionStartTargetResolvedTick { get; }

            public int ActionStartBlockedByMoveLockTick { get; }

            public int ActionStartActiveWithoutSignalTick { get; }

            public int ActionStartTick { get; }

            public int AttackExecuteRawIntentTick { get; }

            public int AttackExecuteRejectedByExecutionLockTick { get; }

            public int AttackExecuteTick { get; }

            public int AttackExecuteActionStateActiveTick { get; }

            public int AttackExecuteExecutionAttemptedTick { get; }

            public int AttackCommittedTraceTick { get; }

            public int AttackCommittedRecoverTick { get; }

            public int AttackCommittedTick => AttackCommittedTraceTick != 0
                ? AttackCommittedTraceTick
                : AttackCommittedRecoverTick;

            public int RecoverEntryTick { get; }

            public int RecoverCompleteTick { get; }

            public int RecoverTickCount { get; }

            public int RecoverPatrolWriteCount { get; }

            public int FirstCombatDamageTick { get; }
        }

        private readonly struct PatrolStateUpdateRecord
        {
            public PatrolStateUpdateRecord(string label, int sequence, string home, string lastDirection)
            {
                Label = label;
                Sequence = sequence;
                Home = home;
                LastDirection = lastDirection;
            }

            public string Label { get; }

            public int Sequence { get; }

            public string Home { get; }

            public string LastDirection { get; }
        }

        private readonly struct LockedTargetLostMetrics
        {
            public LockedTargetLostMetrics(
                int activeWindupEntryTick,
                int cancelOwnerTick,
                int cancelTraceTick,
                EnemyAiMode fallbackMode,
                IReadOnlyList<int> homeDistanceSeries)
            {
                ActiveWindupEntryTick = activeWindupEntryTick;
                CancelOwnerTick = cancelOwnerTick;
                CancelTraceTick = cancelTraceTick;
                FallbackMode = fallbackMode;
                HomeDistanceSeries = homeDistanceSeries ?? Array.Empty<int>();
            }

            public int ActiveWindupEntryTick { get; }

            public int CancelOwnerTick { get; }

            public int CancelTraceTick { get; }

            public EnemyAiMode FallbackMode { get; }

            public IReadOnlyList<int> HomeDistanceSeries { get; }
        }

        private readonly struct WindupParityComparisonMetrics
        {
            public WindupParityComparisonMetrics(
                in WindupContractMetrics baseline,
                in WindupContractMetrics pilot,
                int firstDivergentPositionOrFacingTick)
            {
                Baseline = baseline;
                Pilot = pilot;
                FirstDivergentPositionOrFacingTick = firstDivergentPositionOrFacingTick;
            }

            public WindupContractMetrics Baseline { get; }

            public WindupContractMetrics Pilot { get; }

            public int FirstDivergentPositionOrFacingTick { get; }
        }

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
        public void EnemyPatrolDecisionPlanner_Forward_OpenForward_ReturnsForwardProposal()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));

            var proposal = BuildPatrolDecisionProposal(
                worldState,
                PatrolStrategyKind.Forward,
                new PatrolSettings(PatrolBlockedMovementResponse.Stop));

            Assert.That(proposal.HasDirection, Is.True);
            Assert.That(proposal.PlannedDirection, Is.EqualTo(Direction.Right));
            Assert.That(proposal.PlannedFacing, Is.EqualTo(Direction.Right));
            Assert.That(proposal.CandidateMask, Is.EqualTo(1 << 1));
            Assert.That(proposal.ShouldInitializeState, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolDecisionPlanner_Forward_BlockedStop_ReturnsNoDirection_SameFacing_NoInit()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0)));

            var proposal = BuildPatrolDecisionProposal(
                worldState,
                PatrolStrategyKind.Forward,
                new PatrolSettings(PatrolBlockedMovementResponse.Stop));

            Assert.That(proposal.HasDirection, Is.False);
            Assert.That(proposal.PlannedDirection, Is.EqualTo(Direction.None));
            Assert.That(proposal.PlannedFacing, Is.EqualTo(Direction.Right));
            Assert.That(proposal.CandidateMask, Is.Zero);
            Assert.That(proposal.ShouldInitializeState, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolDecisionPlanner_Forward_BlockedBackward_ReturnsOppositeDirection_SameFacing_NoInit()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0)));

            var proposal = BuildPatrolDecisionProposal(
                worldState,
                PatrolStrategyKind.Forward,
                new PatrolSettings(PatrolBlockedMovementResponse.TryStepBackward));

            Assert.That(proposal.HasDirection, Is.True);
            Assert.That(proposal.PlannedDirection, Is.EqualTo(Direction.Left));
            Assert.That(proposal.PlannedFacing, Is.EqualTo(Direction.Right));
            Assert.That(proposal.CandidateMask, Is.EqualTo(1 << 3));
            Assert.That(proposal.ShouldInitializeState, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolDecisionPlanner_Forward_Boundary_ExcludesTopologyChangeCandidate()
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

            var proposal = BuildPatrolDecisionProposal(
                worldState,
                PatrolStrategyKind.Forward,
                new PatrolSettings(PatrolBlockedMovementResponse.Stop));

            Assert.That(proposal.HasDirection, Is.False);
            Assert.That(proposal.CandidateMask, Is.Zero);
            Assert.That(proposal.PlannedDirection, Is.EqualTo(Direction.None));
            Assert.That(proposal.PlannedFacing, Is.EqualTo(Direction.Up));
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
        public void EnemyPatrolDecisionPlanner_RandomWalkAdapter_PreservesLegacyPlanFields()
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
            var patrolState = new EnemyPatrolRuntimeState
            {
                sequence = 2,
                homeCell = sourceCell,
                lastCommittedDirection = Direction.Up,
            };

            var legacyPlan = EnemyRandomWalkPatrolPlanner.BuildPlan(snapshot, source, tickIndex: 5, patrolState, settings);
            var built = EnemyPatrolDecisionPlanner.TryBuildProposal(
                snapshot,
                source,
                tickIndex: 5,
                PatrolStrategyKind.RandomWalk,
                patrolState,
                settings,
                out var proposal);

            Assert.That(built, Is.True);
            Assert.That(proposal.HasDirection, Is.EqualTo(legacyPlan.HasDirection));
            Assert.That(proposal.PlannedDirection, Is.EqualTo(legacyPlan.PlannedDirection));
            Assert.That(proposal.PlannedFacing, Is.EqualTo(legacyPlan.PlannedFacing));
            Assert.That(proposal.CandidateMask, Is.EqualTo(legacyPlan.CandidateMask));
            Assert.That(proposal.ShouldInitializeState, Is.EqualTo(legacyPlan.ShouldInitializeState));
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
        public void EnemyLogic_WindupRandomWalkPilot_PatrolStateWrites_OccurOnlyOnInitAndCommittedPatrolMove()
        {
            var profile = CreateWindupRandomWalkPilotProfile(windupTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 4, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var tick1 = pipeline.RunTick(new TickInput(1));
                worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, 4, 0));
                var tick2 = pipeline.RunTick(new TickInput(2));
                var tick3 = pipeline.RunTick(new TickInput(3));
                var tick4 = pipeline.RunTick(new TickInput(4));
                var patrolState = GetEnemyPatrolState(worldState, 40);
                var tick1Updates = ParsePatrolStateUpdates(tick1.Trace.Text, 40);
                var movementSectionUpdates = ParsePatrolStateUpdatesFromSection(tick1.Trace.Text, "Movement.CommitEvents", 40);
                var eventLogSectionUpdates = ParsePatrolStateUpdatesFromSection(tick1.Trace.Text, "TickResult.EventLog", 40);
                var tick2Updates = ParsePatrolStateUpdates(tick2.Trace.Text, 40);
                var tick3Updates = ParsePatrolStateUpdates(tick3.Trace.Text, 40);
                var tick4Updates = ParsePatrolStateUpdates(tick4.Trace.Text, 40);
                var committedMoveSemanticCount = SemanticEventAssertions.FilterEvents(tick1.EventLog, "MoveCommitted")
                    .Count(evt => evt.Contains("|E=40|", StringComparison.Ordinal));
                var triageMetrics = BuildPatrolWriteTriageMetrics(
                    initialSequence: 0,
                    finalSequence: patrolState.sequence,
                    initializedWriteCount: tick1Updates.Count(update => update.Label == "Initialized"),
                    expectedCommittedMoveCount: 1,
                    traceCommittedMoveCount: tick1Updates.Count(update => update.Label == "CommittedMove"),
                    semanticCommittedMoveCount: committedMoveSemanticCount,
                    movementSectionCommittedMoveCount: movementSectionUpdates.Count(update => update.Label == "CommittedMove"),
                    eventLogSectionCommittedMoveCount: eventLogSectionUpdates.Count(update => update.Label == "CommittedMove"));

                TestContext.Progress.WriteLine($"PatrolTraceDiagnostic|Tick=1|Entity=40|RawCount={CountPatrolStateUpdates(tick1.Trace.Text, 40)}");
                TestContext.Progress.WriteLine($"PatrolWriteTriage|{BuildPatrolWriteTriageSummary(triageMetrics)}");
                Assert.That(
                    tick1Updates.All(update => update.Label == "Initialized" || update.Label == "CommittedMove"),
                    Is.True,
                    $"Unexpected patrol labels: {string.Join(", ", tick1Updates.Select(update => update.Label))}");
                Assert.That(tick1Updates.Count(update => update.Label == "Initialized"), Is.EqualTo(1), "duplicate Initialized patrol writes");
                Assert.That(tick1Updates.Count(update => update.Label == "CommittedMove"), Is.EqualTo(1), "duplicate CommittedMove patrol writes");
                Assert.That(tick1Updates.Select(update => update.Sequence).ToArray(), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(tick1Updates.Select(update => update.Home).Distinct().ToArray(), Is.EqualTo(new[] { "Floor(0,0)" }));
                Assert.That(tick1Updates.Select(update => update.LastDirection).ToArray(), Is.EqualTo(new[] { "None", "Right" }));
                Assert.That(triageMetrics.Classification, Is.EqualTo(PatrolWriteTriageKind.None), BuildPatrolWriteTriageSummary(triageMetrics));
                Assert.That(triageMetrics.ActualCommittedMoveWrites, Is.EqualTo(1), BuildPatrolWriteTriageSummary(triageMetrics));
                Assert.That(triageMetrics.TraceCommittedMoveCount, Is.EqualTo(1), BuildPatrolWriteTriageSummary(triageMetrics));
                Assert.That(triageMetrics.SemanticCommittedMoveCount, Is.EqualTo(1), BuildPatrolWriteTriageSummary(triageMetrics));
                Assert.That(triageMetrics.MovementSectionCommittedMoveCount, Is.EqualTo(1), BuildPatrolWriteTriageSummary(triageMetrics));
                Assert.That(triageMetrics.EventLogSectionCommittedMoveCount, Is.Zero, BuildPatrolWriteTriageSummary(triageMetrics));
                Assert.That(tick1.Trace.Text, Does.Contain("EnemyPatrolStateUpdated|E=40|Label=Initialized"));
                Assert.That(tick1.Trace.Text, Does.Contain("EnemyPatrolStateUpdated|"));
                Assert.That(tick1.Trace.Text, Does.Contain("Label=CommittedMove"));
                Assert.That(tick2Updates, Is.Empty);
                Assert.That(tick3Updates, Is.Empty);
                Assert.That(tick4Updates, Is.Empty);
                Assert.That(patrolState.IsInitialized, Is.True);
                Assert.That(patrolState.homeCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(patrolState.sequence, Is.EqualTo(2));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_WindupRandomWalkPilot_CapturesPatrolOrigin_WhenLeavingPatrolBeforeFirstCommittedMove()
        {
            var profile = CreateWindupRandomWalkPilotProfile(windupTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var logic = new EnemyLogic(entityId: 40, profile);
            var transitions = new List<string>();

            try
            {
                ((IEnemyAiStateLogic)logic).CommitAiTransitions(
                    worldState.CreateSnapshot(),
                    new TickInput(1),
                    EnemyAiTransitionStage.BeforeMovement,
                    worldState.CreateWriteContext(),
                    transitions);

                var enemy = GetEntity(worldState, 40);
                var patrolState = GetEnemyPatrolState(worldState, 40);

                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(patrolState.IsInitialized, Is.True);
                Assert.That(patrolState.homeCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(patrolState.sequence, Is.EqualTo(1));
                Assert.That(patrolState.lastCommittedDirection, Is.EqualTo(Direction.None));
                Assert.That(transitions, Has.Some.Contains("EnemyPatrolStateUpdated|E=40|Label=Initialized|Seq=1|Home=Floor(0,0)|LastDirection=None"));
                Assert.That(transitions, Has.Some.Contains("EnemyAiTransition|Stage=BeforeMovement|E=40|From=Patrol|FromTimer=0|To=Chase|ToTimer=0|Reason=TargetSensed"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_WindupRandomWalkPilot_DoesNotWritePatrolState_DuringChaseAttackRecover()
        {
            var profile = CreateWindupRandomWalkPilotProfile(windupTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var tick1 = pipeline.RunTick(new TickInput(1));
                var tick2 = pipeline.RunTick(new TickInput(2));
                var tick3 = pipeline.RunTick(new TickInput(3));
                var tick4 = pipeline.RunTick(new TickInput(4));
                var patrolState = GetEnemyPatrolState(worldState, 40);
                var tick1Updates = ParsePatrolStateUpdates(tick1.Trace.Text, 40);
                var tick2Updates = ParsePatrolStateUpdates(tick2.Trace.Text, 40);
                var tick3Updates = ParsePatrolStateUpdates(tick3.Trace.Text, 40);
                var tick4Updates = ParsePatrolStateUpdates(tick4.Trace.Text, 40);

                TestContext.Progress.WriteLine($"PatrolTraceDiagnostic|Tick=1|Entity=40|RawCount={CountPatrolStateUpdates(tick1.Trace.Text, 40)}");
                TestContext.Progress.WriteLine(
                    $"PatrolNoWriteZone|Tick2={tick2Updates.Length}|Tick3={tick3Updates.Length}|Tick4={tick4Updates.Length}");
                Assert.That(tick1Updates.All(update => update.Label == "Initialized"), Is.True);
                Assert.That(tick1Updates.Select(update => update.Sequence).ToArray(), Is.EqualTo(new[] { 1 }));
                Assert.That(tick1Updates.Select(update => update.Home).Distinct().ToArray(), Is.EqualTo(new[] { "Floor(0,0)" }));
                Assert.That(tick1Updates.Select(update => update.LastDirection).Distinct().ToArray(), Is.EqualTo(new[] { "None" }));
                Assert.That(tick1.Trace.Text, Does.Contain("EnemyPatrolStateUpdated|E=40|Label=Initialized"));
                Assert.That(tick1.Trace.Text, Does.Not.Contain("Label=CommittedMove"));
                Assert.That(tick2Updates, Is.Empty);
                Assert.That(tick3Updates, Is.Empty);
                Assert.That(tick4Updates, Is.Empty);
                Assert.That(patrolState.sequence, Is.EqualTo(1));
                Assert.That(patrolState.lastCommittedDirection, Is.EqualTo(Direction.None));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupForwardBaseline_AttackCommittedControlProbe_IsComplete()
        {
            var profile = CreateEnemyProfile(windupTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)));

            try
            {
                var metrics = RunWindupContractMetrics(worldState, profile, ticks: 4);
                TestContext.Progress.WriteLine($"WindupGateSummary|Label=forward baseline self-check|{BuildWindupMetricsSummary(metrics)}");
                AssertWindupAttackControlGreen(metrics, "forward baseline self-check", ResolveRecoverTicks(profile));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupForwardBaseline_LockedTargetLostControlProbe_IsComplete()
        {
            var profile = CreateEnemyProfile(windupTicks: 2);
            var metrics = RunLockedTargetLostControlProbe(profile);

            try
            {
                TestContext.Progress.WriteLine($"LockedTargetLostGateSummary|Label=forward locked-target-lost self-check|{BuildLockedTargetLostMetricsSummary(metrics)}");
                AssertLockedTargetLostControlGreen(metrics, "forward locked-target-lost self-check", EnemyAiMode.Patrol);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupRandomWalkPilot_AttackWindupRecoverContract_MatchesForwardBaseline()
        {
            var controlProfile = CreateEnemyProfile(windupTicks: 1);
            var baselineProfile = CreateEnemyProfile(windupTicks: 1);
            var pilotProfile = CreateWindupRandomWalkPilotProfile(windupTicks: 1);
            var controlWorld = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)));
            var baselineWorld = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));
            var pilotWorld = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));

            try
            {
                var expectedRecoverTicks = ResolveRecoverTicks(controlProfile);
                AssertWindupAttackControlGreen(
                    RunWindupContractMetrics(controlWorld, controlProfile, ticks: 4),
                    "forward baseline self-check",
                    expectedRecoverTicks);
                var comparison = RunWindupParityComparison(baselineWorld, baselineProfile, pilotWorld, pilotProfile, ticks: 6);
                var baselineMetrics = comparison.Baseline;
                var pilotMetrics = comparison.Pilot;
                TestContext.Progress.WriteLine($"WindupGateSummary|Label=baseline direct-lane|{BuildWindupMetricsSummary(baselineMetrics)}");
                TestContext.Progress.WriteLine($"WindupGateSummary|Label=pilot direct-lane|{BuildWindupMetricsSummary(pilotMetrics)}");
                AssertWindupProbeComplete(baselineMetrics, "baseline direct-lane", ResolveRecoverTicks(baselineProfile));
                AssertWindupProbeComplete(pilotMetrics, "pilot direct-lane", ResolveRecoverTicks(pilotProfile));

                Assert.That(pilotMetrics.TargetSensedTick, Is.EqualTo(baselineMetrics.TargetSensedTick));
                Assert.That(pilotMetrics.TargetInRangeTick, Is.EqualTo(baselineMetrics.TargetInRangeTick));
                Assert.That(pilotMetrics.AttackEntryTick, Is.EqualTo(baselineMetrics.AttackEntryTick));
                Assert.That(pilotMetrics.ActionStartTick, Is.EqualTo(baselineMetrics.ActionStartTick));
                Assert.That(pilotMetrics.AttackExecuteTick, Is.EqualTo(baselineMetrics.AttackExecuteTick));
                Assert.That(pilotMetrics.AttackExecuteActionStateActiveTick, Is.EqualTo(baselineMetrics.AttackExecuteActionStateActiveTick));
                Assert.That(pilotMetrics.AttackExecuteExecutionAttemptedTick, Is.EqualTo(baselineMetrics.AttackExecuteExecutionAttemptedTick));
                Assert.That(pilotMetrics.AttackCommittedTraceTick, Is.EqualTo(baselineMetrics.AttackCommittedTraceTick));
                Assert.That(pilotMetrics.AttackCommittedRecoverTick, Is.EqualTo(baselineMetrics.AttackCommittedRecoverTick));
                Assert.That(pilotMetrics.RecoverEntryTick, Is.EqualTo(baselineMetrics.RecoverEntryTick));
                Assert.That(pilotMetrics.RecoverCompleteTick, Is.EqualTo(baselineMetrics.RecoverCompleteTick));
                Assert.That(pilotMetrics.RecoverTickCount, Is.EqualTo(baselineMetrics.RecoverTickCount));
                Assert.That(pilotMetrics.RecoverPatrolWriteCount, Is.EqualTo(baselineMetrics.RecoverPatrolWriteCount));
                Assert.That(
                    pilotMetrics.AttackCommittedTick - pilotMetrics.AttackEntryTick,
                    Is.EqualTo(baselineMetrics.AttackCommittedTick - baselineMetrics.AttackEntryTick));
                Assert.That(
                    pilotMetrics.RecoverEntryTick - pilotMetrics.AttackCommittedTick,
                    Is.EqualTo(baselineMetrics.RecoverEntryTick - baselineMetrics.AttackCommittedTick));
                Assert.That(
                    pilotMetrics.RecoverCompleteTick - pilotMetrics.RecoverEntryTick,
                    Is.EqualTo(baselineMetrics.RecoverCompleteTick - baselineMetrics.RecoverEntryTick));
                Assert.That(pilotMetrics.FirstCombatDamageTick, Is.EqualTo(baselineMetrics.FirstCombatDamageTick));
                Assert.That(comparison.FirstDivergentPositionOrFacingTick, Is.Zero);
            }
            finally
            {
                DestroyProfile(controlProfile);
                DestroyProfile(baselineProfile);
                DestroyProfile(pilotProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyRandomWalkPatrolPlanner_WindupPilotPreset_MeetsMeleeScorecard()
        {
            var shippingSettings = EnemyAiProfileTestFactory.CreateWindupRandomWalkPilotPatrolSettings();
            var controlSettings = PatrolSettings.CreateDefaultRandomWalk();
            var rejectControlSettings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 8,
                sideWeight: 1,
                backwardWeight: 0,
                preventImmediateBacktrack: true);

            var shippingMetrics = SampleRandomWalkScorecardMetrics(shippingSettings);
            var controlMetrics = SampleRandomWalkScorecardMetrics(controlSettings);
            var rejectControlMetrics = SampleRandomWalkScorecardMetrics(rejectControlSettings);
            var baselineCombatTick = MeasureFirstCombatDamageTick(CreateEnemyProfile(windupTicks: 0));
            var shippingCombatTick = MeasureFirstCombatDamageTick(CreateWindupRandomWalkPilotProfile(windupTicks: 0, includePassiveContact: false));

            Assert.That(shippingMetrics.MaxHomeDistance, Is.LessThanOrEqualTo(1));
            Assert.That(shippingMetrics.ForwardCommittedShare, Is.GreaterThanOrEqualTo(0.6f));
            Assert.That(shippingMetrics.AvoidableImmediateBacktrackCount, Is.EqualTo(0));
            Assert.That(shippingCombatTick, Is.GreaterThanOrEqualTo(baselineCombatTick));
            Assert.That(shippingCombatTick, Is.LessThanOrEqualTo(baselineCombatTick + 1));
            Assert.That(controlMetrics.MaxHomeDistance, Is.GreaterThanOrEqualTo(shippingMetrics.MaxHomeDistance));
            TestContext.Progress.WriteLine(
                $"WindupPilotScorecard|ShippingCommitted={shippingMetrics.CommittedMoves}|RejectControlCommitted={rejectControlMetrics.CommittedMoves}|ShippingImmediateBacktracks={shippingMetrics.ImmediateBacktrackCount}|RejectImmediateBacktracks={rejectControlMetrics.ImmediateBacktrackCount}");
        }

        [Test]
        [Category("Extended")]
        public void EnemyAi_WindupRandomWalkPilot_SameCellCombatPassiveOrdering_IsExact()
        {
            var profile = CreateWindupRandomWalkPilotProfile(windupTicks: 1, includePassiveContact: true);
            var sharedCell = new SurfaceCell(FaceId.Floor, 2, 1);

            try
            {
                var passiveOnlyWorld = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                });
                var passiveOnlyLogic = new EnemyLogic(entityId: 40, profile);
                var passiveOnlyBuffer = new List<RawAttackIntent>();

                passiveOnlyLogic.CollectAttackIntents(passiveOnlyWorld.CreateSnapshot(), new TickInput(1), passiveOnlyBuffer);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceKind: AttackSourceKind.PassiveContact, LocalSequence: 1),
                    },
                    passiveOnlyBuffer.Select(intent => (intent.SourceKind, intent.LocalSequence)).ToArray());

                var primedWorld = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Attack, facing: Direction.Left),
                });
                primedWorld.CreateWriteContext().SetEnemyActionState(
                    40,
                    new EnemyActionRuntimeState
                    {
                        kind = EnemyActionKind.Melee,
                        sequence = 1,
                        lockedTargetEntityId = 10,
                        direction = Direction.Left,
                        startTick = 0,
                        executeTick = 1,
                    });
                var primedLogic = new EnemyLogic(entityId: 40, profile);
                var primedBuffer = new List<RawAttackIntent>();

                primedLogic.CollectAttackIntents(primedWorld.CreateSnapshot(), new TickInput(1), primedBuffer);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceKind: AttackSourceKind.Combat, LocalSequence: 0),
                        (SourceKind: AttackSourceKind.PassiveContact, LocalSequence: 1),
                    },
                    primedBuffer.Select(intent => (intent.SourceKind, intent.LocalSequence)).ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ForwardProposalPath_MatchesLegacyForwardStrategy_OnCanonicalFixtures()
        {
            var fixtures = new[]
            {
                new ForwardPatrolFixture(
                    "OpenForward",
                    CreateWorldState(
                        new[]
                        {
                            CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                        },
                        new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0))),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop)),
                new ForwardPatrolFixture(
                    "BlockedStop",
                    CreateWorldState(
                        new[]
                        {
                            CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                            CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                        },
                        new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0))),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop)),
                new ForwardPatrolFixture(
                    "BlockedBackward",
                    CreateWorldState(
                        new[]
                        {
                            CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                            CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                        },
                        new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0))),
                    new PatrolSettings(PatrolBlockedMovementResponse.TryStepBackward)),
                new ForwardPatrolFixture(
                    "Boundary",
                    CreateWorldState(
                        new[]
                        {
                            CreateUnit(
                                entityId: 40,
                                teamId: 2,
                                position: new SurfaceCell(FaceId.Floor, 1, 1),
                                aiMode: EnemyAiMode.Patrol,
                                facing: Direction.Up),
                        },
                        new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop)),
            };

            for (var i = 0; i < fixtures.Length; i++)
            {
                var fixture = fixtures[i];
                var snapshot = fixture.WorldState.CreateSnapshot();
                var source = GetEntity(fixture.WorldState, 40);
                var expectedHasIntent = ForwardPatrolStrategy.Instance.TryBuildMovementIntent(
                    snapshot,
                    source,
                    EnemyAiCommonSettings.CreateDefaultMelee(),
                    fixture.Settings,
                    out var expectedIntent);
                var profile = CreateForwardPatrolOnlyProfile(fixture.Settings);

                try
                {
                    var logic = new EnemyLogic(entityId: 40, profile);
                    var actualBuffer = new List<RawMovementIntent>();

                    logic.CollectMovementIntents(snapshot, new TickInput(1), actualBuffer);

                    Assert.That(actualBuffer.Count > 0, Is.EqualTo(expectedHasIntent), fixture.Label);
                    if (!expectedHasIntent)
                    {
                        continue;
                    }

                    Assert.That(actualBuffer, Has.Count.EqualTo(1), fixture.Label);
                    Assert.That(actualBuffer[0].Destination, Is.EqualTo(expectedIntent.Destination), fixture.Label);
                    Assert.That(actualBuffer[0].CommandKind, Is.EqualTo(expectedIntent.CommandKind), fixture.Label);
                }
                finally
                {
                    DestroyProfile(profile);
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_PatrolDecisionProposal_InitializesState_OnlyWhenProposalRequestsIt()
        {
            var forwardProfile = CreateForwardPatrolOnlyProfile(new PatrolSettings(PatrolBlockedMovementResponse.Stop));
            var randomWalkProfile = CreateNonAttackingEnemyProfile();
            var forwardWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var randomWalkWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(2, 2), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));

            try
            {
                CommitPreMovementState(forwardWorldState, forwardProfile, out var forwardUpdates);
                CommitPreMovementState(randomWalkWorldState, randomWalkProfile, out var randomWalkUpdates);

                Assert.That(forwardWorldState.CreateSnapshot().TryGetEnemyPatrolState(40, out _), Is.False);
                Assert.That(forwardUpdates.Any(update => update.Contains("EnemyPatrolStateUpdated|E=40", StringComparison.Ordinal)), Is.False);
                Assert.That(randomWalkWorldState.CreateSnapshot().TryGetEnemyPatrolState(40, out var patrolState), Is.True);
                Assert.That(patrolState.IsInitialized, Is.True);
                Assert.That(randomWalkUpdates.Any(update => update.Contains("EnemyPatrolStateUpdated|E=40", StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                DestroyProfile(forwardProfile);
                DestroyProfile(randomWalkProfile);
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
        public void EnemyMovementStrategyShared_WallFollowDirection_UsesBoardEdgeWeakAnchorFallback_WhenNoStrongAnchorExists()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right);

            Assert.That(EnemyMovementStrategyShared.HasWallFollowAnchor(snapshot, source, settings), Is.False);
            Assert.That(
                EnemyMovementStrategyShared.TryChooseWallFollowDirection(snapshot, source, settings, out var direction),
                Is.True);
            Assert.That(direction, Is.EqualTo(Direction.Right));
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
        public void EnemyMovementStrategyShared_WallFollowRotateOnlyFacing_UsesTurnPreferenceSymmetry()
        {
            Assert.That(
                EnemyMovementStrategyShared.TryChooseWallFollowRotateOnlyFacing(
                    Direction.Up,
                    WallFollowTurnPreference.Right,
                    out var rightFacing),
                Is.True);
            Assert.That(rightFacing, Is.EqualTo(Direction.Right));

            Assert.That(
                EnemyMovementStrategyShared.TryChooseWallFollowRotateOnlyFacing(
                    Direction.Up,
                    WallFollowTurnPreference.Left,
                    out var leftFacing),
                Is.True);
            Assert.That(leftFacing, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_WallFollowBeforeAttackStage_DeadEnd_CommitsRotateOnlyFacing()
        {
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Right);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 2)),
                CreateWall(entityId: 91, position: new Vector2Int(2, 1)),
                CreateWall(entityId: 92, position: new Vector2Int(0, 1)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var transitions = new List<string>();

            try
            {
                ((IEnemyAiStateLogic)logic).CommitAiTransitions(
                    worldState.CreateSnapshot(),
                    new TickInput(1),
                    EnemyAiTransitionStage.BeforeAttack,
                    worldState.CreateWriteContext(),
                    transitions);

                var enemy = GetEntity(worldState, 40);
                Assert.That(enemy.position.PlanarPosition, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Right));
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                DestroyProfile(profile);
            }
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
                var worldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                    entity,
                });
                var logic = factory.Create(new EntityLogicCreationContext(worldState.CreateSnapshot(), entity));
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
        public void EnemyLogic_WallFollowPassiveContact_SameCellHold_SuppressesMovementIntent_AndStillProducesPassiveContact()
        {
            var profile = CreateWallFollowerProfile(WallFollowTurnPreference.Left, includePassiveContact: true);
            var sharedCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
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
        [Category("Core")]
        public void EnemyAiProfile_CreateRuntimeDefinition_UtilityCapability_CompilesSummonEffectAndTicks()
        {
            var profile = CreateUtilitySummonerProfile(
                CreateSummonUtilityEffect(
                    initialDelaySeconds: 0.2f,
                    intervalSeconds: 0.5f,
                    spawnCountPerTrigger: 2,
                    maxAliveChildren: 4,
                    overrideHp: true,
                    hpOverride: 3));

            try
            {
                var definition = profile.CreateRuntimeDefinition(10);

                Assert.That(definition.Capabilities.TryGetUtility(out var utility), Is.True);
                Assert.That(utility.Effects, Has.Count.EqualTo(1));
                Assert.That(utility.Effects[0].Kind, Is.EqualTo(EnemyUtilityEffectKind.SummonMinion));
                Assert.That(utility.Effects[0].InitialDelayTicks, Is.EqualTo(2));
                Assert.That(utility.Effects[0].IntervalTicks, Is.EqualTo(5));
                Assert.That(utility.Effects[0].Summon.SpawnCountPerTrigger, Is.EqualTo(2));
                Assert.That(utility.Effects[0].Summon.MaxAliveChildren, Is.EqualTo(4));
                Assert.That(utility.Effects[0].Summon.SummonedArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("BasicMinion")));
                Assert.That(utility.Effects[0].Summon.OverrideHp, Is.True);
                Assert.That(utility.Effects[0].Summon.HpOverride, Is.EqualTo(3));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfile_CreateRuntimeDefinition_UtilityCapability_CompilesLockNearbyBoxesEffectAndTicks()
        {
            var profile = CreateUtilitySummonerProfile(
                CreateLockNearbyBoxesUtilityEffect(
                    initialDelaySeconds: 0.2f,
                    intervalSeconds: 0.5f,
                    radius: 2,
                    durationSeconds: 0.3f,
                    blocksPush: true,
                    blocksFlip: false,
                    includeSourceCell: true,
                    targetPattern: BoxLockTargetPattern.OrthogonalAdjacent4));

            try
            {
                var definition = profile.CreateRuntimeDefinition(10);

                Assert.That(definition.Capabilities.TryGetUtility(out var utility), Is.True);
                Assert.That(utility.Effects, Has.Count.EqualTo(1));
                Assert.That(utility.Effects[0].Kind, Is.EqualTo(EnemyUtilityEffectKind.LockNearbyBoxes));
                Assert.That(utility.Effects[0].InitialDelayTicks, Is.EqualTo(2));
                Assert.That(utility.Effects[0].IntervalTicks, Is.EqualTo(5));
                Assert.That(utility.Effects[0].LockNearbyBoxes.Radius, Is.EqualTo(2));
                Assert.That(utility.Effects[0].LockNearbyBoxes.DurationTicks, Is.EqualTo(3));
                Assert.That(utility.Effects[0].LockNearbyBoxes.BlocksPush, Is.True);
                Assert.That(utility.Effects[0].LockNearbyBoxes.BlocksFlip, Is.False);
                Assert.That(utility.Effects[0].LockNearbyBoxes.IncludeSourceCell, Is.True);
                Assert.That(utility.Effects[0].LockNearbyBoxes.TargetPattern, Is.EqualTo(BoxLockTargetPattern.OrthogonalAdjacent4));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityLogicProviderFactory_UtilityOnlyProfile_OmitsCombatLogicsFromEntitySet()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect());

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
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_LockNearbyBoxes_NullPayload_Throws()
        {
            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.LockNearbyBoxes);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", 0f);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "intervalSeconds", 1f);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "lockNearbyBoxes", null);
            var profile = CreateUtilitySummonerProfile(effect);

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_LockNearbyBoxes_NonPositiveRadius_Throws()
        {
            var profile = CreateUtilitySummonerProfile(
                CreateLockNearbyBoxesUtilityEffect(radius: 0));

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_LockNearbyBoxes_NonPositiveDuration_Throws()
        {
            var profile = CreateUtilitySummonerProfile(
                CreateLockNearbyBoxesUtilityEffect(durationSeconds: 0f));

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_LockNearbyBoxes_MustBlockPushOrFlip()
        {
            var profile = CreateUtilitySummonerProfile(
                CreateLockNearbyBoxesUtilityEffect(blocksPush: false, blocksFlip: false));

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfile_CreateRuntimeDefinition_UtilityCapability_CompilesSummonAndLockNearbyBoxesTogether()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
                UtilityEffects = new[]
                {
                    CreateSummonUtilityEffect(intervalSeconds: 2f),
                    CreateLockNearbyBoxesUtilityEffect(initialDelaySeconds: 0.1f, intervalSeconds: 0.4f, radius: 1, durationSeconds: 0.2f),
                },
            });

            try
            {
                var definition = profile.CreateRuntimeDefinition(10);

                Assert.That(definition.Capabilities.TryGetUtility(out var utility), Is.True);
                Assert.That(utility.Effects, Has.Count.EqualTo(2));
                Assert.That(utility.Effects[0].Kind, Is.EqualTo(EnemyUtilityEffectKind.SummonMinion));
                Assert.That(utility.Effects[1].Kind, Is.EqualTo(EnemyUtilityEffectKind.LockNearbyBoxes));
                Assert.That(utility.Effects[1].LockNearbyBoxes.DurationTicks, Is.EqualTo(2));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void SummonMinionAuthoring_Compile_StoresStableArchetypePayloadOnly()
        {
            var profile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset("HeavyMinion", profile, hp: 7, initialAiMode: EnemyAiMode.Patrol);

            try
            {
                var authoring = CreateSummonMinionAuthoring(
                    spawnCountPerTrigger: 2,
                    maxAliveChildren: 4,
                    summonedArchetype: archetype,
                    overrideHp: true,
                    hpOverride: 5);

                var runtime = authoring.Compile();
                var runtimeFields = typeof(SummonMinionRuntime).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                Assert.That(runtime.SpawnCountPerTrigger, Is.EqualTo(2));
                Assert.That(runtime.MaxAliveChildren, Is.EqualTo(4));
                Assert.That(runtime.SummonedArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("HeavyMinion")));
                Assert.That(runtime.OverrideHp, Is.True);
                Assert.That(runtime.HpOverride, Is.EqualTo(5));
                Assert.That(runtimeFields.Any(field => field.Name == "DefinitionMode" || field.Name == "MinionHp"), Is.False);
                Assert.That(
                    runtimeFields.Any(field =>
                        typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) ||
                        field.FieldType == typeof(EnemyAiProfile) ||
                        field.FieldType == typeof(EnemyAiRuntimeDefinition)),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void SummonMinionRuntime_DoesNotExposeLegacyDefinitionModeOrMinionHpFields()
        {
            var runtimeFields = typeof(SummonMinionRuntime).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            Assert.That(runtimeFields.Any(field => field.Name == "DefinitionMode"), Is.False);
            Assert.That(runtimeFields.Any(field => field.Name == "MinionHp"), Is.False);
        }

        [Test]
        [Category("Core")]
        public void SummonMinionAuthoring_Compile_NullSummonedArchetype_Throws()
        {
            var authoring = CreateSummonMinionAuthoring(includeSummonedArchetype: false);

            Assert.Throws<ArgumentException>(() => authoring.Compile());
        }

        [Test]
        [Category("Core")]
        public void SummonMinionAuthoring_Compile_EmptyArchetypeId_Throws()
        {
            var profile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset(string.Empty, profile, hp: 3, initialAiMode: EnemyAiMode.Patrol);

            try
            {
                var authoring = CreateSummonMinionAuthoring(
                    summonedArchetype: archetype);

                Assert.Throws<ArgumentException>(() => authoring.Compile());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void SummonMinionAuthoring_Compile_InvalidHpOverride_Throws()
        {
            var profile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", profile, hp: 3, initialAiMode: EnemyAiMode.Patrol);

            try
            {
                var authoring = CreateSummonMinionAuthoring(
                    summonedArchetype: archetype,
                    overrideHp: true,
                    hpOverride: 0);

                Assert.Throws<ArgumentException>(() => authoring.Compile());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHostConfiguration_CreateEnemyAiRuntimeSnapshot_CompilesArchetypeRegistryAndSpawnDefaults()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var archetypeProfile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var catalog = CreateEnemyUnitArchetypeCatalog(archetype);

            try
            {
                var snapshot = new GameplaySceneHostConfiguration
                {
                    SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    DefaultEnemyAiProfile = defaultProfile,
                    EnemyUnitArchetypeCatalog = catalog,
                }.CreateEnemyAiRuntimeSnapshot();

                Assert.That(snapshot.DefinitionsByArchetypeId, Is.Not.Null);
                Assert.That(snapshot.SpawnDefaultsByArchetypeId, Is.Not.Null);
                Assert.That(snapshot.DefinitionsByArchetypeId.ContainsKey(new EnemyUnitArchetypeId("BasicMinion")), Is.True);
                Assert.That(snapshot.SpawnDefaultsByArchetypeId[new EnemyUnitArchetypeId("BasicMinion")].Hp, Is.EqualTo(4));
                Assert.That(snapshot.SpawnDefaultsByArchetypeId[new EnemyUnitArchetypeId("BasicMinion")].InitialAiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(archetypeProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHostConfiguration_CreateEnemyAiRuntimeSnapshot_DuplicateArchetypeId_Throws()
        {
            var firstProfile = CreateNonAttackingEnemyProfile();
            var secondProfile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect());
            var first = CreateEnemyUnitArchetypeAsset("DuplicateMinion", firstProfile, hp: 3, initialAiMode: EnemyAiMode.Patrol);
            var second = CreateEnemyUnitArchetypeAsset("DuplicateMinion", secondProfile, hp: 5, initialAiMode: EnemyAiMode.Chase);
            var catalog = CreateEnemyUnitArchetypeCatalog(first, second);

            try
            {
                Assert.Throws<ArgumentException>(
                    () => new GameplaySceneHostConfiguration
                    {
                        SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                        EnemyUnitArchetypeCatalog = catalog,
                    }.CreateEnemyAiRuntimeSnapshot());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                DestroyProfile(firstProfile);
                DestroyProfile(secondProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHostConfiguration_CreateEnemyAiRuntimeSnapshot_MissingReferencedArchetype_Throws()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var orphanedArchetypeProfile = CreateNonAttackingEnemyProfile();
            var orphanedArchetype = CreateEnemyUnitArchetypeAsset("OrphanedMinion", orphanedArchetypeProfile, hp: 3, initialAiMode: EnemyAiMode.Patrol);
            var summonerProfile = CreateUtilitySummonerProfile(
                CreateArchetypeSummonUtilityEffect(orphanedArchetype));

            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => new GameplaySceneHostConfiguration
                    {
                        SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                        DefaultEnemyAiProfile = defaultProfile,
                        EnemyAiProfileOverrides = new[]
                        {
                            new EnemyAiProfileOverride
                            {
                                EntityId = 40,
                                Profile = summonerProfile,
                            },
                        },
                    }.CreateEnemyAiRuntimeSnapshot());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(orphanedArchetype);
                DestroyProfile(orphanedArchetypeProfile);
                DestroyProfile(summonerProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHostConfiguration_CreateEnemyPresentationArchetypeRegistry_MissingSummonedMapping_Throws()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var archetypeProfile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var gameplayCatalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var config = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = defaultProfile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = 40,
                        Profile = CreateUtilitySummonerProfile(CreateArchetypeSummonUtilityEffect(archetype)),
                    },
                },
                EnemyUnitArchetypeCatalog = gameplayCatalog,
            };

            try
            {
                var runtime = config.CreateEnemyAiRuntimeSnapshot();

                Assert.Throws<InvalidOperationException>(() => config.CreateEnemyPresentationArchetypeRegistry(runtime));
            }
            finally
            {
                DestroyProfile(config.EnemyAiProfileOverrides[0].Profile);
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(archetypeProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHostConfiguration_CreateEnemyPresentationArchetypeRegistry_DuplicateArchetypeId_Throws()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var archetypeProfile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var gameplayCatalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var firstPrefab = CreateEnemyViewPrefab("FirstPresentationPrefab");
            var secondPrefab = CreateEnemyViewPrefab("SecondPresentationPrefab");
            var firstEntry = CreateEnemyPresentationArchetypeAsset("BasicMinion", firstPrefab);
            var secondEntry = CreateEnemyPresentationArchetypeAsset("BasicMinion", secondPrefab);
            var presentationCatalog = CreateEnemyPresentationArchetypeCatalog(firstEntry, secondEntry);
            var config = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = defaultProfile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = 40,
                        Profile = CreateUtilitySummonerProfile(CreateArchetypeSummonUtilityEffect(archetype)),
                    },
                },
                EnemyUnitArchetypeCatalog = gameplayCatalog,
                EnemyPresentationArchetypeCatalog = presentationCatalog,
            };

            try
            {
                var runtime = config.CreateEnemyAiRuntimeSnapshot();

                Assert.Throws<InvalidOperationException>(() => config.CreateEnemyPresentationArchetypeRegistry(runtime));
            }
            finally
            {
                DestroyProfile(config.EnemyAiProfileOverrides[0].Profile);
                UnityEngine.Object.DestroyImmediate(presentationCatalog);
                UnityEngine.Object.DestroyImmediate(firstEntry);
                UnityEngine.Object.DestroyImmediate(secondEntry);
                UnityEngine.Object.DestroyImmediate(firstPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(secondPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(archetypeProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHostConfiguration_CreateEnemyPresentationArchetypeRegistry_NullPrefab_Throws()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var archetypeProfile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var gameplayCatalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var entry = CreateEnemyPresentationArchetypeAsset("BasicMinion", viewPrefab: null);
            var presentationCatalog = CreateEnemyPresentationArchetypeCatalog(entry);
            var config = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = defaultProfile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = 40,
                        Profile = CreateUtilitySummonerProfile(CreateArchetypeSummonUtilityEffect(archetype)),
                    },
                },
                EnemyUnitArchetypeCatalog = gameplayCatalog,
                EnemyPresentationArchetypeCatalog = presentationCatalog,
            };

            try
            {
                var runtime = config.CreateEnemyAiRuntimeSnapshot();

                Assert.Throws<InvalidOperationException>(() => config.CreateEnemyPresentationArchetypeRegistry(runtime));
            }
            finally
            {
                DestroyProfile(config.EnemyAiProfileOverrides[0].Profile);
                UnityEngine.Object.DestroyImmediate(presentationCatalog);
                UnityEngine.Object.DestroyImmediate(entry);
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(archetypeProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySceneHostConfiguration_CreateEnemyPresentationArchetypeRegistry_PresentationArchetypeMissingFromGameplayRegistry_Throws()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var archetypeProfile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var gameplayCatalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var prefab = CreateEnemyViewPrefab("UnexpectedPresentationPrefab");
            var entry = CreateEnemyPresentationArchetypeAsset("UnexpectedMinion", prefab);
            var presentationCatalog = CreateEnemyPresentationArchetypeCatalog(entry);
            var config = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = defaultProfile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = 40,
                        Profile = CreateUtilitySummonerProfile(CreateArchetypeSummonUtilityEffect(archetype)),
                    },
                },
                EnemyUnitArchetypeCatalog = gameplayCatalog,
                EnemyPresentationArchetypeCatalog = presentationCatalog,
            };

            try
            {
                var runtime = config.CreateEnemyAiRuntimeSnapshot();

                Assert.Throws<InvalidOperationException>(() => config.CreateEnemyPresentationArchetypeRegistry(runtime));
            }
            finally
            {
                DestroyProfile(config.EnemyAiProfileOverrides[0].Profile);
                UnityEngine.Object.DestroyImmediate(presentationCatalog);
                UnityEngine.Object.DestroyImmediate(entry);
                UnityEngine.Object.DestroyImmediate(prefab.gameObject);
                UnityEngine.Object.DestroyImmediate(gameplayCatalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                DestroyProfile(archetypeProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyEntityLogicFactory_ResolveDefinition_EntityOverrideWinsOverArchetypeBinding()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var overrideProfile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect());
            var archetypeProfile = CreateNonAttackingEnemyProfile();

            try
            {
                var defaultDefinition = defaultProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var overrideDefinition = overrideProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var archetypeDefinition = archetypeProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var archetypeId = new EnemyUnitArchetypeId("BoundMinion");
                var worldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol),
                });
                worldState.SetEnemyDefinitionBindingState(41, new EnemyDefinitionBindingState(archetypeId));

                var factory = new EnemyEntityLogicFactory(
                    defaultDefinition,
                    new Dictionary<int, EnemyAiRuntimeDefinition>
                    {
                        { 41, overrideDefinition },
                    },
                    new Dictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition>(EnemyUnitArchetypeId.EqualityComparer)
                    {
                        { archetypeId, archetypeDefinition },
                    });

                var resolved = factory.ResolveDefinition(worldState.CreateSnapshot(), GetEntity(worldState, 41));

                Assert.That(resolved.Capabilities.TryGetUtility(out _), Is.True);
                Assert.That(resolved.Capabilities.TryGetCombat(out _), Is.False);
            }
            finally
            {
                DestroyProfile(archetypeProfile);
                DestroyProfile(overrideProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyEntityLogicFactory_ResolveDefinition_ArchetypeBindingWinsOverDefault()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var archetypeProfile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect());

            try
            {
                var defaultDefinition = defaultProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var archetypeDefinition = archetypeProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var archetypeId = new EnemyUnitArchetypeId("UtilityMinion");
                var worldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol),
                });
                worldState.SetEnemyDefinitionBindingState(41, new EnemyDefinitionBindingState(archetypeId));

                var factory = new EnemyEntityLogicFactory(
                    defaultDefinition,
                    definitionsByArchetypeId: new Dictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition>(EnemyUnitArchetypeId.EqualityComparer)
                    {
                        { archetypeId, archetypeDefinition },
                    });

                var resolved = factory.ResolveDefinition(worldState.CreateSnapshot(), GetEntity(worldState, 41));

                Assert.That(resolved.Capabilities.TryGetUtility(out _), Is.True);
                Assert.That(resolved.Capabilities.TryGetCombat(out _), Is.False);
            }
            finally
            {
                DestroyProfile(archetypeProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyEntityLogicFactory_ResolveDefinition_NoBindingFallsBackToDefault()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var archetypeProfile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect());

            try
            {
                var defaultDefinition = defaultProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var archetypeDefinition = archetypeProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var archetypeId = new EnemyUnitArchetypeId("UtilityMinion");
                var worldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol),
                });

                var factory = new EnemyEntityLogicFactory(
                    defaultDefinition,
                    definitionsByArchetypeId: new Dictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition>(EnemyUnitArchetypeId.EqualityComparer)
                    {
                        { archetypeId, archetypeDefinition },
                    });

                var resolved = factory.ResolveDefinition(worldState.CreateSnapshot(), GetEntity(worldState, 41));

                Assert.That(resolved.Capabilities.TryGetCombat(out _), Is.True);
                Assert.That(resolved.Capabilities.TryGetUtility(out _), Is.False);
            }
            finally
            {
                DestroyProfile(archetypeProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyEntityLogicFactory_ResolveDefinition_UnknownBindingThrows()
        {
            var defaultProfile = CreateDefaultMeleeProfile();

            try
            {
                var defaultDefinition = defaultProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var worldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol),
                });
                worldState.SetEnemyDefinitionBindingState(41, new EnemyDefinitionBindingState(new EnemyUnitArchetypeId("MissingMinion")));

                var factory = new EnemyEntityLogicFactory(defaultDefinition);

                Assert.Throws<InvalidOperationException>(
                    () => factory.ResolveDefinition(worldState.CreateSnapshot(), GetEntity(worldState, 41)));
            }
            finally
            {
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityLogicProviderFactory_ArchetypeBoundUtilityOnlyEntity_OmitsCombatLanes()
        {
            var defaultProfile = CreateDefaultMeleeProfile();
            var utilityProfile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect());

            try
            {
                var defaultDefinition = defaultProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var utilityDefinition = utilityProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                var archetypeId = new EnemyUnitArchetypeId("UtilityMinion");
                var worldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol),
                });
                worldState.SetEnemyDefinitionBindingState(41, new EnemyDefinitionBindingState(archetypeId));

                var provider = GameplayEntityLogicProviderFactory.CreateDefault(
                    defaultDefinition,
                    definitionsByArchetypeId: new Dictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition>(EnemyUnitArchetypeId.EqualityComparer)
                    {
                        { archetypeId, utilityDefinition },
                    });
                var logicSet = provider.Build(worldState.CreateSnapshot(), Array.Empty<IEntityLogic>());

                Assert.That(logicSet.AiStateLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.PreMovementStateLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.MovementLogics, Has.Count.EqualTo(1));
                Assert.That(logicSet.EnemyActionStateLogics, Is.Empty);
                Assert.That(logicSet.AttackLogics, Is.Empty);
            }
            finally
            {
                DestroyProfile(utilityProfile);
                DestroyProfile(defaultProfile);
            }
        }

        [Test]
        [Category("Core")]
        public void WorldState_RemoveEntity_RemovesEnemyDefinitionBindingState()
        {
            var archetypeId = new EnemyUnitArchetypeId("CleanupMinion");
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol),
            });
            worldState.SetEnemyDefinitionBindingState(41, new EnemyDefinitionBindingState(archetypeId));

            Assert.That(worldState.CreateSnapshot().TryGetEnemyDefinitionBindingState(41, out _), Is.True);

            worldState.CreateWriteContext().RemoveEntity(41);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(41, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetEnemyDefinitionBindingState(41, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_CreateSnapshot_RehydratesEnemyDefinitionBindingState()
        {
            var archetypeId = new EnemyUnitArchetypeId("ProjectedMinion");
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol),
            });
            worldState.SetEnemyDefinitionBindingState(41, new EnemyDefinitionBindingState(archetypeId));

            var projectedSnapshot = new ProjectedWorld(worldState.CreateSnapshot()).CreateSnapshot();

            Assert.That(projectedSnapshot.TryGetEnemyDefinitionBindingState(41, out var bindingState), Is.True);
            Assert.That(bindingState.ArchetypeId, Is.EqualTo(archetypeId));
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_NullEffect_Throws()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
                UtilityEffects = new EnemyUtilityEffectAuthoring[] { null },
            });

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_NegativeInitialDelay_Throws()
        {
            var profile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect(initialDelaySeconds: -0.1f));

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_NonPositiveInterval_Throws()
        {
            var profile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect(intervalSeconds: 0f));

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_NonPositiveSpawnCount_Throws()
        {
            var profile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect(spawnCountPerTrigger: 0));

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_NonPositiveMaxAliveChildren_Throws()
        {
            var profile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect(maxAliveChildren: 0));

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_MissingSummonedArchetype_Throws()
        {
            var effect = new EnemyUtilityEffectAuthoring();
            var summon = CreateSummonMinionAuthoring(includeSummonedArchetype: false);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.SummonMinion);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", 0f);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "intervalSeconds", 1f);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "summon", summon);
            var profile = CreateUtilitySummonerProfile(effect);

            try
            {
                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAiProfileCompiler_UtilityCapability_DuplicateFamily_Throws()
        {
            var profile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect());
            var duplicateUtility = ScriptableObject.CreateInstance<EnemyUtilityCapabilityAsset>();
            duplicateUtility.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(
                duplicateUtility,
                "effects",
                new[] { CreateSummonUtilityEffect(intervalSeconds: 2f) });

            try
            {
                var capabilities = EnemyAiProfileTestFactory.GetSerializedField<List<EnemyCapabilityAsset>>(profile, "capabilityAssets");
                EnemyAiProfileTestFactory.SetSerializedField(
                    profile,
                    "capabilityAssets",
                    new List<EnemyCapabilityAsset>
                    {
                        capabilities[0],
                        duplicateUtility,
                    });

                Assert.Throws<ArgumentException>(() => profile.CreateRuntimeDefinition(60));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicateUtility);
                DestroyProfile(profile);
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

        private static EnemyPatrolDecisionProposal BuildPatrolDecisionProposal(
            WorldState worldState,
            PatrolStrategyKind patrolKind,
            PatrolSettings settings,
            int tickIndex = 1)
        {
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            snapshot.TryGetEnemyPatrolState(40, out var patrolState);

            var built = EnemyPatrolDecisionPlanner.TryBuildProposal(
                snapshot,
                source,
                tickIndex,
                patrolKind,
                patrolState,
                settings,
                out var proposal);

            Assert.That(built, Is.True);
            return proposal;
        }

        private static void CommitPreMovementState(
            WorldState worldState,
            EnemyAiProfile profile,
            out List<string> updates)
        {
            var logic = new EnemyLogic(entityId: 40, profile);
            updates = new List<string>();

            ((IPreMovementStateLogic)logic).CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1),
                worldState.CreateWriteContext(),
                updates,
                new List<PlayerActionTransition>());
        }

        private static EnemyAiProfile CreateEnemyProfile(int windupTicks, int moveCooldownTicks = 0)
        {
            return EnemyAiProfileTestFactory.CreateDefaultMelee(windupTicks, moveCooldownTicks);
        }

        private static EnemyAiProfile CreateDefaultMeleeProfile(
            int moveCooldownTicks = 0,
            int recoverTicks = 1,
            bool includePassiveContact = false)
        {
            return EnemyAiProfileTestFactory.CreateDefaultMelee(
                windupTicks: 0,
                moveCooldownTicks: moveCooldownTicks,
                recoverTicks: recoverTicks,
                includePassiveContact: includePassiveContact);
        }

        private static EnemyAiProfile CreateChargingEnemyProfile(int moveCooldownTicks)
        {
            return EnemyAiProfileTestFactory.CreateCharging(moveCooldownTicks);
        }

        private static EnemyAiProfile CreateNonAttackingEnemyProfile(int moveCooldownTicks = 0)
        {
            return EnemyAiProfileTestFactory.CreateNonAttacking(moveCooldownTicks);
        }

        private static EnemyAiProfile CreateWindupRandomWalkPilotProfile(
            int windupTicks = 1,
            int moveCooldownTicks = 0,
            int recoverTicks = 1,
            bool includePassiveContact = true)
        {
            return EnemyAiProfileTestFactory.CreateWindupRandomWalkPilot(
                windupTicks,
                moveCooldownTicks,
                recoverTicks,
                includePassiveContact);
        }

        private static EnemyAiProfile CreateForwardPatrolOnlyProfile(PatrolSettings patrolSettings)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.Forward,
                PatrolSettings = patrolSettings,
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
            });
        }

        private static EnemyAiProfile CreateWallFollowerProfile(
            WallFollowTurnPreference turnPreference,
            int moveCooldownTicks = 0,
            bool includePassiveContact = false)
        {
            return EnemyAiProfileTestFactory.CreateWallFollower(turnPreference, moveCooldownTicks, includePassiveContact);
        }

        private static EnemyAiProfile CreateJumpPatrolProfile()
        {
            return EnemyAiProfileTestFactory.CreateJumpPatrol(
                new EnemyJumpTimingSettings(windupTicks: 1, airborneTicks: 1, cooldownTicks: 1));
        }

        private static int CountPatrolStateUpdates(string traceText, int entityId)
        {
            return ParsePatrolStateUpdates(traceText, entityId).Length;
        }

        private static PatrolStateUpdateRecord[] ParsePatrolStateUpdates(string traceText, int entityId)
        {
            if (string.IsNullOrEmpty(traceText))
            {
                return Array.Empty<PatrolStateUpdateRecord>();
            }

            return ParsePatrolStateUpdateLines(
                traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                entityId);
        }

        private static PatrolStateUpdateRecord[] ParsePatrolStateUpdatesFromSection(
            string traceText,
            string sectionTitle,
            int entityId)
        {
            return ParsePatrolStateUpdateLines(GetTraceSectionLines(traceText, sectionTitle), entityId);
        }

        private static PatrolStateUpdateRecord[] ParsePatrolStateUpdateLines(
            IEnumerable<string> lines,
            int entityId)
        {
            return lines
                .Where(line => line.Contains("EnemyPatrolStateUpdated|", StringComparison.Ordinal) &&
                               line.Contains($"|E={entityId}|", StringComparison.Ordinal))
                .Select(line => new PatrolStateUpdateRecord(
                    ReadDelimitedField(line, "Label"),
                    int.Parse(ReadDelimitedField(line, "Seq")),
                    ReadDelimitedField(line, "Home"),
                    ReadDelimitedField(line, "LastDirection")))
                .ToArray();
        }

        private static IEnumerable<string> GetTraceSectionLines(string traceText, string sectionTitle)
        {
            if (string.IsNullOrEmpty(traceText) || string.IsNullOrEmpty(sectionTitle))
            {
                return Array.Empty<string>();
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sectionLines = new List<string>();
            var sectionPrefix = "  ";
            var inSection = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!inSection)
                {
                    if (string.Equals(line, sectionTitle, StringComparison.Ordinal))
                    {
                        inSection = true;
                    }

                    continue;
                }

                if (!line.StartsWith(sectionPrefix, StringComparison.Ordinal))
                {
                    break;
                }

                sectionLines.Add(line.Substring(sectionPrefix.Length));
            }

            return sectionLines;
        }

        private static RandomWalkScorecardMetrics SampleRandomWalkScorecardMetrics(PatrolSettings patrolSettings)
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.RandomWalk,
                PatrolSettings = patrolSettings,
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
            });
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(2, 2), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var homeCell = new SurfaceCell(FaceId.Floor, 2, 2);
                var maxHomeDistance = 0;
                var forwardMoves = 0;
                var scoredMoves = 0;
                var committedMoves = 0;
                var immediateBacktracks = 0;
                var avoidableImmediateBacktracks = 0;
                var lastCommittedDirection = Direction.None;

                for (var tickIndex = 1; tickIndex <= 20; tickIndex++)
                {
                    var snapshot = worldState.CreateSnapshot();
                    var enemyBeforeTick = GetEntity(worldState, 40);
                    snapshot.TryGetEnemyPatrolState(40, out var patrolStateBeforeTick);
                    var builtProposal = EnemyPatrolDecisionPlanner.TryBuildProposal(
                        snapshot,
                        enemyBeforeTick,
                        tickIndex,
                        PatrolStrategyKind.RandomWalk,
                        patrolStateBeforeTick,
                        patrolSettings,
                        out var proposal);
                    var forwardCandidateAvailable = builtProposal &&
                                                    (proposal.CandidateMask & GetCandidateMaskBit(enemyBeforeTick.facing)) != 0;
                    var tick = pipeline.RunTick(new TickInput(tickIndex));
                    var enemy = GetEntity(worldState, 40);
                    var patrolState = GetEnemyPatrolState(worldState, 40);
                    var homeDistance = Mathf.Abs(enemy.position.x - homeCell.x) + Mathf.Abs(enemy.position.y - homeCell.y);

                    maxHomeDistance = Mathf.Max(maxHomeDistance, homeDistance);

                    if (!SemanticEventAssertions.ContainsEvent(tick.EventLog, "MoveCommitted", "E=40"))
                    {
                        continue;
                    }

                    committedMoves++;
                    var committedDirection = patrolState.lastCommittedDirection;
                    if (forwardCandidateAvailable)
                    {
                        scoredMoves++;
                        if (committedDirection == enemyBeforeTick.facing)
                        {
                            forwardMoves++;
                        }
                    }

                    if (lastCommittedDirection != Direction.None &&
                        committedDirection == ResolveOppositeDirection(lastCommittedDirection))
                    {
                        immediateBacktracks++;
                        if (builtProposal &&
                            HasAlternativeCandidateDirection(proposal.CandidateMask, committedDirection))
                        {
                            avoidableImmediateBacktracks++;
                        }
                    }

                    lastCommittedDirection = committedDirection;
                }

                return new RandomWalkScorecardMetrics(
                    maxHomeDistance,
                    forwardMoves,
                    scoredMoves,
                    committedMoves,
                    immediateBacktracks,
                    avoidableImmediateBacktracks);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        private static bool HasAlternativeCandidateDirection(int candidateMask, Direction chosenDirection)
        {
            return (candidateMask & ~GetCandidateMaskBit(chosenDirection)) != 0;
        }

        private static int MeasureFirstCombatDamageTick(EnemyAiProfile profile)
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));

            try
            {
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);

                for (var tickIndex = 1; tickIndex <= 6; tickIndex++)
                {
                    var tick = pipeline.RunTick(new TickInput(tickIndex));
                    if (tick.AttackPhaseResult.DamageResolutions.Any(record =>
                            record.Accepted &&
                            record.SourceId == 40 &&
                            record.TargetId == 10 &&
                            record.SourceKind == AttackSourceKind.Combat))
                    {
                        return tickIndex;
                    }
                }

                Assert.Fail("Expected a combat damage tick within the bounded scorecard window.");
                return -1;
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        private static LockedTargetLostMetrics RunLockedTargetLostControlProbe(EnemyAiProfile profile)
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None, hp: 3),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            var activeWindupEntryTick = 0;
            var cancelOwnerTick = 0;
            var cancelTraceTick = 0;

            var windupTick = pipeline.RunTick(new TickInput(1));
            var windupSignals = windupTick.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40).ToArray();
            if (windupSignals.Any(signal => signal.StartedThisTick) || GetEnemyActionState(worldState, 40).IsActive)
            {
                activeWindupEntryTick = 1;
            }

            worldState.CreateWriteContext().ApplyDamage(10, 3);
            var cancelTick = pipeline.RunTick(new TickInput(2));
            var enemyAfterCancelTick = GetEntity(worldState, 40);
            var actionStateAfterCancelTick = GetEnemyActionState(worldState, 40);
            var cancelSignals = cancelTick.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40).ToArray();
            if (cancelSignals.Any(signal => signal.CanceledThisTick) &&
                !actionStateAfterCancelTick.IsActive &&
                enemyAfterCancelTick.aiMode == EnemyAiMode.Patrol)
            {
                cancelOwnerTick = 2;
            }

            if (cancelTick.Trace.Text.Contains("LockedTargetLost", StringComparison.Ordinal))
            {
                cancelTraceTick = 2;
            }

            return new LockedTargetLostMetrics(
                activeWindupEntryTick,
                cancelOwnerTick,
                cancelTraceTick,
                enemyAfterCancelTick.aiMode,
                Array.Empty<int>());
        }

        private static WindupParityComparisonMetrics RunWindupParityComparison(
            WorldState baselineWorldState,
            EnemyAiProfile baselineProfile,
            WorldState pilotWorldState,
            EnemyAiProfile pilotProfile,
            int ticks)
        {
            var baselinePipeline = GameplayCompositionRoot.CreateTickPipeline(baselineWorldState, baselineProfile);
            var pilotPipeline = GameplayCompositionRoot.CreateTickPipeline(pilotWorldState, pilotProfile);
            var baselineDefinition = baselineProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            var pilotDefinition = pilotProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            var baselineMetrics = default(WindupContractMetrics);
            var pilotMetrics = default(WindupContractMetrics);
            var firstDivergentPositionOrFacingTick = 0;

            for (var tickIndex = 1; tickIndex <= ticks; tickIndex++)
            {
                var baselineTick = baselinePipeline.RunTick(new TickInput(tickIndex));
                var pilotTick = pilotPipeline.RunTick(new TickInput(tickIndex));

                baselineMetrics = UpdateWindupContractMetrics(
                    baselineMetrics,
                    baselineWorldState,
                    baselineDefinition,
                    baselineTick,
                    tickIndex);
                pilotMetrics = UpdateWindupContractMetrics(
                    pilotMetrics,
                    pilotWorldState,
                    pilotDefinition,
                    pilotTick,
                    tickIndex);

                if (firstDivergentPositionOrFacingTick == 0)
                {
                    var baselineEnemy = GetEntityAfterTick(baselineTick, 40);
                    var pilotEnemy = GetEntityAfterTick(pilotTick, 40);
                    if (baselineEnemy.position != pilotEnemy.position ||
                        baselineEnemy.facing != pilotEnemy.facing)
                    {
                        firstDivergentPositionOrFacingTick = tickIndex;
                    }
                }
            }

            return new WindupParityComparisonMetrics(baselineMetrics, pilotMetrics, firstDivergentPositionOrFacingTick);
        }

        private static WindupContractMetrics RunWindupContractMetrics(
            WorldState worldState,
            EnemyAiProfile profile,
            int ticks)
        {
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
            var runtimeDefinition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            var metrics = default(WindupContractMetrics);

            for (var tickIndex = 1; tickIndex <= ticks; tickIndex++)
            {
                var tick = pipeline.RunTick(new TickInput(tickIndex));
                metrics = UpdateWindupContractMetrics(metrics, worldState, runtimeDefinition, tick, tickIndex);
            }

            return metrics;
        }

        private static WindupContractMetrics UpdateWindupContractMetrics(
            WindupContractMetrics metrics,
            WorldState worldState,
            EnemyAiRuntimeDefinition runtimeDefinition,
            TickResult tick,
            int tickIndex)
        {
            var trace = tick.Trace.Text;
            var actionSignals = tick.PresentationData.EnemyActionSignals.Where(signal => signal.EntityId == 40).ToArray();
            var snapshot = worldState.CreateSnapshot();
            snapshot.TryGetEntity(40, out var source);
            snapshot.TryGetEnemyActionState(40, out var currentActionState);
            var targetSensedTick = CaptureFirstTransitionTick(trace, 40, "TargetSensed", tickIndex, metrics.TargetSensedTick);
            var targetInRangeTick = CaptureFirstTransitionTick(trace, 40, "TargetInRange", tickIndex, metrics.TargetInRangeTick);
            var attackEntryTick = CaptureFirstTransitionIntoModeTick(trace, 40, EnemyAiMode.Attack, tickIndex, metrics.AttackEntryTick);
            var actionStartTargetResolvedTick = metrics.ActionStartTargetResolvedTick;
            var actionStartBlockedByMoveLockTick = metrics.ActionStartBlockedByMoveLockTick;
            var actionStartActiveWithoutSignalTick = metrics.ActionStartActiveWithoutSignalTick;
            var actionStartTick = metrics.ActionStartTick;

            if (source.entityId == 40 &&
                source.aiMode == EnemyAiMode.Attack &&
                !currentActionState.IsActive)
            {
                var canResolveStart = EnemyActionStateTargeting.TryResolveStartAction(
                    snapshot,
                    source,
                    runtimeDefinition.DetectionStrategy,
                    runtimeDefinition.AttackDecisionStrategy,
                    runtimeDefinition.DetectionSettings,
                    runtimeDefinition.AttackDecisionSettings,
                    out _,
                    out _);

                if (actionStartTargetResolvedTick == 0 && canResolveStart)
                {
                    actionStartTargetResolvedTick = tickIndex;
                }

                if (actionStartBlockedByMoveLockTick == 0 &&
                    canResolveStart &&
                    !snapshot.CanStartAction(40, tickIndex) &&
                    snapshot.TryGetEntityExecutionLockState(40, out var executionLockState) &&
                    executionLockState.phase == EntityExecutionPhase.Move &&
                    EntityExecutionLockQueries.IsLocked(executionLockState, tickIndex))
                {
                    actionStartBlockedByMoveLockTick = tickIndex;
                }
            }

            if (actionStartTick == 0 && actionSignals.Any(signal => signal.StartedThisTick))
            {
                actionStartTick = tickIndex;
            }

            if (actionStartActiveWithoutSignalTick == 0 &&
                actionStartTick == 0 &&
                currentActionState.IsActive &&
                !actionSignals.Any(signal => signal.StartedThisTick))
            {
                actionStartActiveWithoutSignalTick = tickIndex;
            }

            var attackExecuteRawIntentTick = metrics.AttackExecuteRawIntentTick;
            var attackExecuteRejectedByExecutionLockTick = metrics.AttackExecuteRejectedByExecutionLockTick;
            var hasCombatRawIntent = tick.AttackPhaseResult.RawIntents.Any(intent =>
                intent.SourceId == 40 &&
                intent.TargetId == 10 &&
                intent.SourceKind == AttackSourceKind.Combat);
            if (attackExecuteRawIntentTick == 0 && hasCombatRawIntent)
            {
                attackExecuteRawIntentTick = tickIndex;
            }

            if (attackExecuteRejectedByExecutionLockTick == 0 &&
                hasCombatRawIntent &&
                tick.AttackPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("AttackRejected|Stage=ExecutionLock|", StringComparison.Ordinal) &&
                    reason.Contains("Source=40|", StringComparison.Ordinal)))
            {
                attackExecuteRejectedByExecutionLockTick = tickIndex;
            }

            var attackExecuteTick = metrics.AttackExecuteTick;
            var attackExecuteActionStateActiveTick = metrics.AttackExecuteActionStateActiveTick;
            var attackExecuteExecutionAttemptedTick = metrics.AttackExecuteExecutionAttemptedTick;
            if (attackExecuteTick == 0 &&
                (actionSignals.Any(signal => signal.ExecutedThisTick) ||
                 tick.AttackPhaseResult.DamageResolutions.Any(record =>
                     record.Accepted &&
                     record.SourceId == 40 &&
                     record.TargetId == 10 &&
                     record.SourceKind == AttackSourceKind.Combat)))
                {
                    attackExecuteTick = tickIndex;
                var actionState = GetEnemyActionState(worldState, 40);
                if (actionState.IsActive)
                {
                    attackExecuteActionStateActiveTick = tickIndex;
                }

                if (actionState.executionAttempted)
                {
                    attackExecuteExecutionAttemptedTick = tickIndex;
                }
            }

            var attackCommittedTraceTick = CaptureFirstTransitionTick(trace, 40, "AttackCommitted", tickIndex, metrics.AttackCommittedTraceTick);
            var attackCommittedRecoverTick = CaptureFirstTransitionExactTick(
                trace,
                40,
                EnemyAiMode.Attack,
                EnemyAiMode.Recover,
                tickIndex,
                metrics.AttackCommittedRecoverTick);
            var recoverEntryTick = CaptureFirstTransitionIntoModeTick(trace, 40, EnemyAiMode.Recover, tickIndex, metrics.RecoverEntryTick);
            var recoverCompleteTick = CaptureFirstTransitionReasonPrefixTick(
                trace,
                40,
                "RecoverComplete",
                tickIndex,
                metrics.RecoverCompleteTick);
            var recoverTickCount = metrics.RecoverCompleteTick == 0
                ? metrics.RecoverTickCount + CountTransitionReasonsWithPrefix(trace, 40, "RecoverTick")
                : metrics.RecoverTickCount;
            var recoverPatrolWriteCount = metrics.RecoverPatrolWriteCount;
            if (metrics.RecoverCompleteTick == 0 && IsRecoverLaneTick(trace, 40))
            {
                recoverPatrolWriteCount += CountPatrolStateUpdates(trace, 40);
            }
            var firstCombatDamageTick = metrics.FirstCombatDamageTick;

            if (firstCombatDamageTick == 0 &&
                tick.AttackPhaseResult.DamageResolutions.Any(record =>
                    record.Accepted &&
                    record.SourceId == 40 &&
                    record.TargetId == 10 &&
                    record.SourceKind == AttackSourceKind.Combat))
            {
                firstCombatDamageTick = tickIndex;
            }

            return new WindupContractMetrics(
                targetSensedTick,
                targetInRangeTick,
                attackEntryTick,
                actionStartTargetResolvedTick,
                actionStartBlockedByMoveLockTick,
                actionStartActiveWithoutSignalTick,
                actionStartTick,
                attackExecuteRawIntentTick,
                attackExecuteRejectedByExecutionLockTick,
                attackExecuteTick,
                attackExecuteActionStateActiveTick,
                attackExecuteExecutionAttemptedTick,
                attackCommittedTraceTick,
                attackCommittedRecoverTick,
                recoverEntryTick,
                recoverCompleteTick,
                recoverTickCount,
                recoverPatrolWriteCount,
                firstCombatDamageTick);
        }

        private static int CaptureFirstTransitionTick(string traceText, int entityId, string reason, int tickIndex, int currentTick)
        {
            if (currentTick != 0 || string.IsNullOrEmpty(traceText))
            {
                return currentTick;
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("EnemyAiTransition|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|E={entityId}|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|Reason={reason}", StringComparison.Ordinal))
                {
                    return tickIndex;
                }
            }

            return currentTick;
        }

        private static int CaptureFirstTransitionReasonPrefixTick(
            string traceText,
            int entityId,
            string reasonPrefix,
            int tickIndex,
            int currentTick)
        {
            if (currentTick != 0 || string.IsNullOrEmpty(traceText))
            {
                return currentTick;
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("EnemyAiTransition|", StringComparison.Ordinal) ||
                    !lines[i].Contains($"|E={entityId}|", StringComparison.Ordinal))
                {
                    continue;
                }

                var reason = ReadDelimitedField(lines[i], "Reason");
                if (reason.StartsWith(reasonPrefix, StringComparison.Ordinal))
                {
                    return tickIndex;
                }
            }

            return currentTick;
        }

        private static int CaptureFirstTransitionIntoModeTick(
            string traceText,
            int entityId,
            EnemyAiMode targetMode,
            int tickIndex,
            int currentTick)
        {
            if (currentTick != 0 || string.IsNullOrEmpty(traceText))
            {
                return currentTick;
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("EnemyAiTransition|", StringComparison.Ordinal) ||
                    !lines[i].Contains($"|E={entityId}|", StringComparison.Ordinal) ||
                    !lines[i].Contains($"|To={targetMode}", StringComparison.Ordinal))
                {
                    continue;
                }

                if (lines[i].Contains($"|From={targetMode}|", StringComparison.Ordinal))
                {
                    continue;
                }

                return tickIndex;
            }

            return currentTick;
        }

        private static int CaptureFirstTransitionExactTick(
            string traceText,
            int entityId,
            EnemyAiMode fromMode,
            EnemyAiMode toMode,
            int tickIndex,
            int currentTick)
        {
            if (currentTick != 0 || string.IsNullOrEmpty(traceText))
            {
                return currentTick;
            }

            var lines = traceText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("EnemyAiTransition|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|E={entityId}|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|From={fromMode}|", StringComparison.Ordinal) &&
                    lines[i].Contains($"|To={toMode}", StringComparison.Ordinal))
                {
                    return tickIndex;
                }
            }

            return currentTick;
        }

        private static int CountTransitionReasonsWithPrefix(string traceText, int entityId, string reasonPrefix)
        {
            if (string.IsNullOrEmpty(traceText))
            {
                return 0;
            }

            return traceText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line =>
                    line.Contains("EnemyAiTransition|", StringComparison.Ordinal) &&
                    line.Contains($"|E={entityId}|", StringComparison.Ordinal) &&
                    ReadDelimitedField(line, "Reason").StartsWith(reasonPrefix, StringComparison.Ordinal));
        }

        private static bool IsRecoverLaneTick(string traceText, int entityId)
        {
            if (string.IsNullOrEmpty(traceText))
            {
                return false;
            }

            return traceText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(line =>
                    line.Contains("EnemyAiTransition|", StringComparison.Ordinal) &&
                    line.Contains($"|E={entityId}|", StringComparison.Ordinal) &&
                    (line.Contains("|From=Recover|", StringComparison.Ordinal) ||
                     line.Contains("|To=Recover|", StringComparison.Ordinal)));
        }

        private static void AssertWindupAttackControlGreen(in WindupContractMetrics metrics, string label, int expectedRecoverTicks)
        {
            AssertCombatStartGateComplete(metrics, label, requireTargetSensed: false);
            AssertExecuteGateComplete(metrics, label);
            AssertCommitTraceGateComplete(metrics, label);
            AssertRecoverGateComplete(metrics, label, expectedRecoverTicks);
            AssertWindupDiagnosticsClean(metrics, label);
        }

        private static void AssertWindupProbeComplete(in WindupContractMetrics metrics, string label, int expectedRecoverTicks)
        {
            AssertCombatStartGateComplete(metrics, label, requireTargetSensed: true);
            AssertExecuteGateComplete(metrics, label);
            AssertCommitTraceGateComplete(metrics, label);
            AssertRecoverGateComplete(metrics, label, expectedRecoverTicks);
            AssertWindupDiagnosticsClean(metrics, label);
        }

        private static void AssertCombatStartGateComplete(in WindupContractMetrics metrics, string label, bool requireTargetSensed)
        {
            if (requireTargetSensed)
            {
                Assert.That(metrics.TargetSensedTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            }

            Assert.That(metrics.TargetInRangeTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackEntryTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.ActionStartTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertExecuteGateComplete(in WindupContractMetrics metrics, string label)
        {
            Assert.That(metrics.AttackExecuteTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackExecuteActionStateActiveTick, Is.EqualTo(metrics.AttackExecuteTick), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackExecuteExecutionAttemptedTick, Is.EqualTo(metrics.AttackExecuteTick), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.FirstCombatDamageTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertCommitTraceGateComplete(in WindupContractMetrics metrics, string label)
        {
            Assert.That(metrics.AttackCommittedTraceTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackCommittedRecoverTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackCommittedTraceTick, Is.EqualTo(metrics.AttackCommittedRecoverTick), $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertRecoverGateComplete(in WindupContractMetrics metrics, string label, int expectedRecoverTicks)
        {
            Assert.That(metrics.RecoverEntryTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.RecoverCompleteTick, Is.GreaterThan(0), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.RecoverTickCount, Is.EqualTo(expectedRecoverTicks), $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.RecoverPatrolWriteCount, Is.Zero, $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertWindupDiagnosticsClean(in WindupContractMetrics metrics, string label)
        {
            Assert.That(metrics.ActionStartActiveWithoutSignalTick, Is.Zero, $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.ActionStartBlockedByMoveLockTick, Is.Zero, $"{label}: {BuildWindupMetricsSummary(metrics)}");
            Assert.That(metrics.AttackExecuteRejectedByExecutionLockTick, Is.Zero, $"{label}: {BuildWindupMetricsSummary(metrics)}");
        }

        private static void AssertLockedTargetLostControlGreen(
            in LockedTargetLostMetrics metrics,
            string label,
            EnemyAiMode expectedFallbackMode)
        {
            AssertLockedTargetLostEntryGreen(metrics, label);
            AssertLockedTargetLostCancelOwnerGreen(metrics, label, expectedFallbackMode);
            AssertLockedTargetLostWordingGreen(metrics, label);
        }

        private static void AssertLockedTargetLostEntryGreen(in LockedTargetLostMetrics metrics, string label)
        {
            Assert.That(metrics.ActiveWindupEntryTick, Is.GreaterThan(0), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
        }

        private static void AssertLockedTargetLostCancelOwnerGreen(
            in LockedTargetLostMetrics metrics,
            string label,
            EnemyAiMode expectedFallbackMode)
        {
            Assert.That(metrics.CancelOwnerTick, Is.GreaterThan(0), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            Assert.That(metrics.FallbackMode, Is.EqualTo(expectedFallbackMode), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
        }

        private static void AssertLockedTargetLostWordingGreen(in LockedTargetLostMetrics metrics, string label)
        {
            Assert.That(metrics.CancelTraceTick, Is.GreaterThan(0), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            Assert.That(metrics.CancelTraceTick, Is.EqualTo(metrics.CancelOwnerTick), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
        }

        private static void AssertLockedTargetLostHomeReturnGreen(in LockedTargetLostMetrics metrics, string label, int leashRadius)
        {
            Assert.That(metrics.HomeDistanceSeries, Is.Not.Empty, $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            var firstHomeIndex = -1;
            for (var i = 0; i < metrics.HomeDistanceSeries.Count; i++)
            {
                if (metrics.HomeDistanceSeries[i] == 0)
                {
                    firstHomeIndex = i;
                    break;
                }
            }

            Assert.That(firstHomeIndex, Is.GreaterThanOrEqualTo(0), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            for (var i = 1; i <= firstHomeIndex; i++)
            {
                Assert.That(
                    metrics.HomeDistanceSeries[i],
                    Is.LessThanOrEqualTo(metrics.HomeDistanceSeries[i - 1]),
                    $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            }

            Assert.That(metrics.HomeDistanceSeries[firstHomeIndex], Is.LessThanOrEqualTo(leashRadius), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
            Assert.That(metrics.HomeDistanceSeries[^1], Is.LessThanOrEqualTo(leashRadius), $"{label}: {BuildLockedTargetLostMetricsSummary(metrics)}");
        }

        private static string BuildWindupMetricsSummary(in WindupContractMetrics metrics)
        {
            return $"TargetSensed={metrics.TargetSensedTick}, TargetInRange={metrics.TargetInRangeTick}, AttackEntry={metrics.AttackEntryTick}, ActionStartResolved={metrics.ActionStartTargetResolvedTick}, ActionStartBlockedByMoveLock={metrics.ActionStartBlockedByMoveLockTick}, ActionStartActiveWithoutSignal={metrics.ActionStartActiveWithoutSignalTick}, ActionStart={metrics.ActionStartTick}, AttackRawIntent={metrics.AttackExecuteRawIntentTick}, AttackRejectedByExecutionLock={metrics.AttackExecuteRejectedByExecutionLockTick}, AttackExecute={metrics.AttackExecuteTick}, ExecuteActionStateActive={metrics.AttackExecuteActionStateActiveTick}, ExecuteAttempted={metrics.AttackExecuteExecutionAttemptedTick}, AttackCommittedTrace={metrics.AttackCommittedTraceTick}, AttackCommittedRecover={metrics.AttackCommittedRecoverTick}, RecoverEntry={metrics.RecoverEntryTick}, RecoverComplete={metrics.RecoverCompleteTick}, RecoverTickCount={metrics.RecoverTickCount}, RecoverPatrolWrites={metrics.RecoverPatrolWriteCount}, FirstCombatDamage={metrics.FirstCombatDamageTick}";
        }

        private static string BuildLockedTargetLostMetricsSummary(in LockedTargetLostMetrics metrics)
        {
            var homeDistanceSeries = metrics.HomeDistanceSeries.Count == 0
                ? "<none>"
                : string.Join(">", metrics.HomeDistanceSeries);
            return $"ActiveWindupEntry={metrics.ActiveWindupEntryTick}, CancelOwner={metrics.CancelOwnerTick}, CancelTrace={metrics.CancelTraceTick}, FallbackMode={metrics.FallbackMode}, HomeDistanceSeries={homeDistanceSeries}";
        }

        private static PatrolWriteTriageMetrics BuildPatrolWriteTriageMetrics(
            int initialSequence,
            int finalSequence,
            int initializedWriteCount,
            int expectedCommittedMoveCount,
            int traceCommittedMoveCount,
            int semanticCommittedMoveCount,
            int movementSectionCommittedMoveCount,
            int eventLogSectionCommittedMoveCount)
        {
            var actualCommittedMoveWrites = Math.Max(0, finalSequence - initialSequence - initializedWriteCount);
            return new PatrolWriteTriageMetrics(
                initializedWriteCount,
                expectedCommittedMoveCount,
                actualCommittedMoveWrites,
                traceCommittedMoveCount,
                semanticCommittedMoveCount,
                movementSectionCommittedMoveCount,
                eventLogSectionCommittedMoveCount);
        }

        private static string BuildPatrolWriteTriageSummary(in PatrolWriteTriageMetrics metrics)
        {
            return $"InitializedWrites={metrics.InitializedWriteCount}, ExpectedCommittedMoveWrites={metrics.ExpectedCommittedMoveCount}, ActualCommittedMoveWrites={metrics.ActualCommittedMoveWrites}, TraceCommittedMoveWrites={metrics.TraceCommittedMoveCount}, SemanticCommittedMoves={metrics.SemanticCommittedMoveCount}, MovementSectionCommittedMoves={metrics.MovementSectionCommittedMoveCount}, EventLogSectionCommittedMoves={metrics.EventLogSectionCommittedMoveCount}, Classification={metrics.Classification}";
        }

        private static string ReadDelimitedField(string line, string fieldName)
        {
            var fieldPrefix = $"{fieldName}=";
            var startIndex = line.IndexOf(fieldPrefix, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return string.Empty;
            }

            startIndex += fieldPrefix.Length;
            var endIndex = line.IndexOf('|', startIndex);
            return endIndex >= 0
                ? line.Substring(startIndex, endIndex - startIndex)
                : line.Substring(startIndex);
        }

        private static int ResolveRecoverTicks(EnemyAiProfile profile)
        {
            return profile
                .CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .CommonSettings
                .RecoverTicks;
        }

        private static int GetCandidateMaskBit(Direction direction)
        {
            return direction switch
            {
                Direction.Up => 1 << 0,
                Direction.Right => 1 << 1,
                Direction.Down => 1 << 2,
                Direction.Left => 1 << 3,
                _ => 0,
            };
        }

        private static Direction ResolveOppositeDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            };
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

        private static EnemyAiProfile CreateUtilitySummonerProfile(EnemyUtilityEffectAuthoring effect)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
                UtilityEffects = new[] { effect },
            });
        }

        private static EnemyUtilityEffectAuthoring CreateSummonUtilityEffect(
            float initialDelaySeconds = 0f,
            float intervalSeconds = 1f,
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            EnemyUnitArchetypeAsset summonedArchetype = null,
            bool overrideHp = false,
            int hpOverride = 1,
            bool requireNoUnitAtSpawnCell = true,
            bool requireNoSolidAtSpawnCell = true)
        {
            var summon = new SummonMinionAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(summon, "spawnCountPerTrigger", spawnCountPerTrigger);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "maxAliveChildren", maxAliveChildren);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "candidatePattern", SummonCandidatePattern.OrthogonalAdjacent4);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoUnitAtSpawnCell", requireNoUnitAtSpawnCell);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoSolidAtSpawnCell", requireNoSolidAtSpawnCell);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "summonedArchetype", summonedArchetype != null ? summonedArchetype : GetSharedSummonedArchetype());
            EnemyAiProfileTestFactory.SetSerializedField(summon, "overrideHp", overrideHp);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "hpOverride", hpOverride);

            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.SummonMinion);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", initialDelaySeconds);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "intervalSeconds", intervalSeconds);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "summon", summon);
            return effect;
        }

        private static EnemyUtilityEffectAuthoring CreateArchetypeSummonUtilityEffect(
            EnemyUnitArchetypeAsset summonedArchetype,
            float initialDelaySeconds = 0f,
            float intervalSeconds = 1f,
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            bool overrideHp = false,
            int hpOverride = 1,
            bool requireNoUnitAtSpawnCell = true,
            bool requireNoSolidAtSpawnCell = true)
        {
            return CreateSummonUtilityEffect(
                initialDelaySeconds,
                intervalSeconds,
                spawnCountPerTrigger,
                maxAliveChildren,
                summonedArchetype,
                overrideHp,
                hpOverride,
                requireNoUnitAtSpawnCell,
                requireNoSolidAtSpawnCell);
        }

        private static SummonMinionAuthoring CreateSummonMinionAuthoring(
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            bool requireNoUnitAtSpawnCell = true,
            bool requireNoSolidAtSpawnCell = true,
            EnemyUnitArchetypeAsset summonedArchetype = null,
            bool includeSummonedArchetype = true,
            bool overrideHp = false,
            int hpOverride = 1)
        {
            var summon = new SummonMinionAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(summon, "spawnCountPerTrigger", spawnCountPerTrigger);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "maxAliveChildren", maxAliveChildren);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "candidatePattern", SummonCandidatePattern.OrthogonalAdjacent4);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoUnitAtSpawnCell", requireNoUnitAtSpawnCell);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoSolidAtSpawnCell", requireNoSolidAtSpawnCell);
            EnemyAiProfileTestFactory.SetSerializedField(
                summon,
                "summonedArchetype",
                includeSummonedArchetype
                    ? summonedArchetype != null ? summonedArchetype : GetSharedSummonedArchetype()
                    : null);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "overrideHp", overrideHp);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "hpOverride", hpOverride);
            return summon;
        }

        private static EnemyPresentationArchetypeAsset CreateEnemyPresentationArchetypeAsset(
            string archetypeId,
            GameplayEntityView viewPrefab)
        {
            var asset = ScriptableObject.CreateInstance<EnemyPresentationArchetypeAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(asset, "archetypeId", new EnemyUnitArchetypeId(archetypeId));
            EnemyAiProfileTestFactory.SetSerializedField(asset, "viewPrefab", viewPrefab);
            return asset;
        }

        private static EnemyPresentationArchetypeCatalog CreateEnemyPresentationArchetypeCatalog(
            params EnemyPresentationArchetypeAsset[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyPresentationArchetypeCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(catalog, "entries", entries ?? Array.Empty<EnemyPresentationArchetypeAsset>());
            return catalog;
        }

        private static GameplayEntityView CreateEnemyViewPrefab(string name)
        {
            var prefabObject = new GameObject(name);
            prefabObject.hideFlags = HideFlags.HideAndDontSave;
            var view = prefabObject.AddComponent<GameplayEntityView>();
            prefabObject.AddComponent<EnemyAnimatorDriver>();
            return view;
        }

        private static EnemyUnitArchetypeAsset CreateEnemyUnitArchetypeAsset(
            string archetypeId,
            EnemyAiProfile profile,
            int hp,
            EnemyAiMode initialAiMode)
        {
            var asset = ScriptableObject.CreateInstance<EnemyUnitArchetypeAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(asset, "archetypeId", new EnemyUnitArchetypeId(archetypeId));
            EnemyAiProfileTestFactory.SetSerializedField(asset, "aiProfile", profile);
            EnemyAiProfileTestFactory.SetSerializedField(asset, "spawnDefaults", CreateEnemyUnitSpawnDefaults(hp, initialAiMode));
            return asset;
        }

        private static EnemyUnitArchetypeCatalog CreateEnemyUnitArchetypeCatalog(params EnemyUnitArchetypeAsset[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyUnitArchetypeCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(catalog, "entries", entries ?? Array.Empty<EnemyUnitArchetypeAsset>());
            return catalog;
        }

        private static EnemyUnitArchetypeAsset GetSharedSummonedArchetype()
        {
            if (SharedSummonedArchetype == null)
            {
                SharedSummonedProfile = CreateNonAttackingEnemyProfile();
                SharedSummonedArchetype = CreateEnemyUnitArchetypeAsset(
                    "BasicMinion",
                    SharedSummonedProfile,
                    hp: 1,
                    initialAiMode: EnemyAiMode.Patrol);
            }

            return SharedSummonedArchetype;
        }

        private static EnemyUnitSpawnDefaults CreateEnemyUnitSpawnDefaults(int hp, EnemyAiMode initialAiMode)
        {
            object boxed = EnemyUnitSpawnDefaults.CreateDefault();
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "hp", hp);
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "initialAiMode", initialAiMode);
            return (EnemyUnitSpawnDefaults)boxed;
        }

        private static EnemyUtilityEffectAuthoring CreateLockNearbyBoxesUtilityEffect(
            float initialDelaySeconds = 0f,
            float intervalSeconds = 1f,
            int radius = 1,
            float durationSeconds = 2f,
            bool blocksPush = true,
            bool blocksFlip = true,
            bool includeSourceCell = false,
            BoxLockTargetPattern targetPattern = BoxLockTargetPattern.ManhattanRadius)
        {
            var lockNearbyBoxes = new LockNearbyBoxesAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "radius", radius);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "durationSeconds", durationSeconds);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "blocksPush", blocksPush);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "blocksFlip", blocksFlip);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "includeSourceCell", includeSourceCell);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "targetPattern", targetPattern);

            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.LockNearbyBoxes);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", initialDelaySeconds);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "intervalSeconds", intervalSeconds);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "lockNearbyBoxes", lockNearbyBoxes);
            return effect;
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
