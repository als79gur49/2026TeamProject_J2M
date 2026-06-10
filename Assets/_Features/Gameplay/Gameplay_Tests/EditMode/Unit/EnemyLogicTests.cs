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

        private readonly struct WallFollowPath
        {
            public WallFollowPath(IReadOnlyList<SurfaceCell> positions, IReadOnlyList<Direction> directions)
            {
                Positions = positions;
                Directions = directions;
            }

            public IReadOnlyList<SurfaceCell> Positions { get; }

            public IReadOnlyList<Direction> Directions { get; }
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
        [Category("Core")]
        public void EnemyLogic_ImplementsMovementAndAttackContracts()
        {
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());

            Assert.That(logic, Is.InstanceOf<IMovementEntityLogic>());
            Assert.That(logic, Is.InstanceOf<IAttackEntityLogic>());
            Assert.That(logic.ControlledEntityId, Is.EqualTo(40));
        }

        [Test]
        [Category("Core")]
        public void EnemyLogic_InvalidConfig_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(() => new EnemyLogic(entityId: 40, default(EnemyAiRuntimeDefinition)));

            Assert.That(exception.ParamName, Is.EqualTo("aiDefinition"));
        }

        [Test]
        [Category("Core")]
        public void EnemyLogic_PatrolMode_ProducesForwardMovementIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());
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
        [Category("Core")]
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
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());
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
        public void EnemyPatrol_MoveIntoEnemyOccupiedCell_SucceedsAndStacks()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);

            var builtIntent = ForwardPatrolStrategy.Instance.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);
            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(1, 0)));

            worldState.CreateWriteContext().MoveEntity(40, new SurfaceCell(FaceId.Floor, 1, 0));
            var stackedUnits = new List<EntityState>();
            worldState.CreateSnapshot().EnumerateUnitsAt(new SurfaceCell(FaceId.Floor, 1, 0), stackedUnits);

            Assert.That(stackedUnits.Select(unit => unit.entityId), Is.EquivalentTo(new[] { 30, 40 }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolDecisionPlanner_Forward_BlockedStop_ReturnsNoDirection_SameFacing_NoInit()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
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
                    CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
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

            var initialPlan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 7,
                default,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>());
            var initializedState = EnemyPatrolQueries.Initialize(default, source.position);
            var initializedPlan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 7,
                initializedState,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>());

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

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 5,
                patrolState,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>());

            Assert.That((plan.CandidateMask & (1 << 2)) == 0, Is.True, "Immediate reverse direction should be excluded when alternatives exist.");
            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.Not.EqualTo(Direction.Down));
        }

        [Test]
        [Category("Extended")]
        public void EnemyRandomWalk_UnitOccupiedCandidate_IsNotRejectedBecauseOfUnit()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 2);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(3, 2), aiMode: EnemyAiMode.None),
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
                lastCommittedDirection = Direction.None,
            };

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 5,
                patrolState,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>());

            Assert.That((plan.CandidateMask & GetCandidateMaskBit(Direction.Right)) != 0, Is.True);
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

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 9,
                patrolState,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>());

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Left));
            Assert.That(plan.CandidateMask, Is.EqualTo(1 << 3));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_OutsideLeash_DetoursAroundBox()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
            var blockers = new[] { CreateBox(entityId: 30, position: new Vector2Int(3, 1)) };
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                blockers,
                tickIndex: 9);

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Up));
            Assert.That(plan.PlannedDirection, Is.Not.EqualTo(Direction.Left));

            var simulatedCell = sourceCell;
            var patrolState = new EnemyPatrolRuntimeState
            {
                sequence = 3,
                homeCell = homeCell,
                lastCommittedDirection = Direction.Right,
            };
            for (var i = 0; i < 8 && GetPlanarDistanceForTest(simulatedCell, homeCell) > settings.LeashRadius; i++)
            {
                var tickPlan = BuildRandomWalkPlan(
                    simulatedCell,
                    homeCell,
                    settings,
                    blockers,
                    tickIndex: 20 + i,
                    patrolState);
                Assert.That(tickPlan.HasDirection, Is.True);
                simulatedCell += ResolveDeltaForTest(tickPlan.PlannedDirection);
                patrolState.lastCommittedDirection = tickPlan.PlannedDirection;
            }

            Assert.That(GetPlanarDistanceForTest(simulatedCell, homeCell), Is.LessThanOrEqualTo(settings.LeashRadius));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_OutsideLeash_DetoursAroundWall()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                new[] { CreateWall(entityId: 30, position: new Vector2Int(3, 1)) },
                tickIndex: 9);

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Up));
            Assert.That(plan.PlannedDirection, Is.Not.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_OutsideLeash_PrefersSafeDetourOverDestroyTile()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var riskyCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                Array.Empty<EntityState>(),
                tickIndex: 9,
                tileFeatures: new[] { CreateDestroyTile(100, riskyCell) },
                tileFeatureDefinitions: new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Up));
            Assert.That(plan.PlannedDirection, Is.Not.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_OutsideLeash_StopsWhenOnlyRouteUsesDestroyTile()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var riskyCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                Array.Empty<EntityState>(),
                new BoardBounds(new Vector2Int(0, 1), new Vector2Int(4, 1)),
                tickIndex: 9,
                tileFeatures: new[] { CreateDestroyTile(100, riskyCell) },
                tileFeatureDefinitions: new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            Assert.That(EnemyMovementStrategyShared.CanTraverseStep(
                CreateWorldState(
                    new[] { CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Left) },
                    new BoardBounds(new Vector2Int(0, 1), new Vector2Int(4, 1)),
                    new[] { CreateDestroyTile(100, riskyCell) }).CreateSnapshot(),
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
                Vector2Int.left,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) }),
                Is.True);
            Assert.That(plan.HasDirection, Is.False);
            Assert.That(plan.CandidateMask, Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_AirUnit_OutsideLeash_CanReturnThroughDestroyTile()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var riskyCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(
                        entityId: 40,
                        teamId: 2,
                        position: sourceCell,
                        aiMode: EnemyAiMode.Patrol,
                        facing: Direction.Left,
                        unitMobilityKind: UnitMobilityKind.Air),
                },
                new BoardBounds(new Vector2Int(0, 1), new Vector2Int(4, 1)),
                new[] { CreateDestroyTile(100, riskyCell) });
            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                worldState.CreateSnapshot(),
                GetEntity(worldState, 40),
                tickIndex: 9,
                new EnemyPatrolRuntimeState
                {
                    sequence = 3,
                    homeCell = homeCell,
                    lastCommittedDirection = Direction.Right,
                },
                settings,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_InsideLeash_KeepsExistingRandomWalkBehavior()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 3,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                new[] { CreateWall(entityId: 30, position: new Vector2Int(2, 0)) },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                tickIndex: 9);

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Left));
            Assert.That(plan.CandidateMask, Is.EqualTo(GetCandidateMaskBit(Direction.Left)));
        }

        [Test]
        [Category("Core")]
        public void RandomWalkLeash_StrictCandidateExists_DoesNotUseRelaxedLeash()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 10,
                backwardWeight: 1);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                Array.Empty<EntityState>(),
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                tickIndex: 9,
                new EnemyPatrolRuntimeState
                {
                    sequence = 3,
                    homeCell = homeCell,
                    lastCommittedDirection = Direction.None,
                });

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Left));
            Assert.That(plan.SelectedDestination, Is.EqualTo(homeCell));
            Assert.That(plan.SelectedPass, Is.EqualTo(RandomWalkLeashSelectionPass.StrictLeash));
            Assert.That(plan.StrictCandidateCount, Is.EqualTo(1));
            Assert.That(plan.RelaxedLeashCandidateCount, Is.EqualTo(1));
            Assert.That(plan.CandidateMask, Is.EqualTo(GetCandidateMaskBit(Direction.Left)));
        }

        [Test]
        [Category("Core")]
        public void RandomWalkLeash_StrictCandidatesEmpty_UsesRelaxedLeashCandidate()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 4);
            var sourceCell = new SurfaceCell(FaceId.Floor, 5, 4);
            var selectedCell = new SurfaceCell(FaceId.Floor, 6, 4);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 5,
                forwardWeight: 10,
                backwardWeight: 1);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                new[] { CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 4, 4)) },
                new BoardBounds(new Vector2Int(0, 4), new Vector2Int(6, 4)),
                tickIndex: 9,
                new EnemyPatrolRuntimeState
                {
                    sequence = 3,
                    homeCell = homeCell,
                    lastCommittedDirection = Direction.None,
                });

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Right));
            Assert.That(plan.SelectedDestination, Is.EqualTo(selectedCell));
            Assert.That(GetPlanarDistanceForTest(plan.SelectedDestination, homeCell), Is.GreaterThan(settings.LeashRadius));
            Assert.That(plan.SelectedPass, Is.EqualTo(RandomWalkLeashSelectionPass.RelaxedLeash));
            Assert.That(plan.StrictCandidateCount, Is.EqualTo(0));
            Assert.That(plan.RelaxedLeashCandidateCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void RandomWalkLeash_RelaxedPass_DoesNotReviveTraversalBlockedCandidate()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 4);
            var sourceCell = new SurfaceCell(FaceId.Floor, 5, 4);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 5,
                forwardWeight: 10,
                sideWeight: 1,
                backwardWeight: 1);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                new[]
                {
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 4, 4)),
                    CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, 6, 4)),
                },
                new BoardBounds(new Vector2Int(0, 4), new Vector2Int(6, 5)),
                tickIndex: 9,
                new EnemyPatrolRuntimeState
                {
                    sequence = 3,
                    homeCell = homeCell,
                    lastCommittedDirection = Direction.None,
                });

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Up));
            Assert.That(plan.SelectedDestination, Is.EqualTo(new SurfaceCell(FaceId.Floor, 5, 5)));
            Assert.That(plan.SelectedPass, Is.EqualTo(RandomWalkLeashSelectionPass.RelaxedLeash));
            Assert.That(plan.StrictCandidateCount, Is.EqualTo(0));
            Assert.That(plan.RelaxedLeashCandidateCount, Is.EqualTo(1));
            Assert.That((plan.CandidateMask & GetCandidateMaskBit(Direction.Right)) == 0, Is.True);
        }

        [Test]
        [Category("Core")]
        public void RandomWalkLeash_NoTraversalLegalCandidate_Stays()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 4);
            var sourceCell = new SurfaceCell(FaceId.Floor, 5, 4);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 5,
                forwardWeight: 10,
                sideWeight: 1,
                backwardWeight: 1);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                new[]
                {
                    CreateWall(entityId: 30, position: new SurfaceCell(FaceId.Floor, 4, 4)),
                    CreateWall(entityId: 31, position: new SurfaceCell(FaceId.Floor, 6, 4)),
                    CreateWall(entityId: 32, position: new SurfaceCell(FaceId.Floor, 5, 5)),
                    CreateWall(entityId: 33, position: new SurfaceCell(FaceId.Floor, 5, 3)),
                },
                new BoardBounds(new Vector2Int(0, 3), new Vector2Int(6, 5)),
                tickIndex: 9,
                new EnemyPatrolRuntimeState
                {
                    sequence = 3,
                    homeCell = homeCell,
                    lastCommittedDirection = Direction.None,
                });

            Assert.That(plan.HasDirection, Is.False);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.None));
            Assert.That(plan.SelectedPass, Is.EqualTo(RandomWalkLeashSelectionPass.None));
            Assert.That(plan.StrictCandidateCount, Is.EqualTo(0));
            Assert.That(plan.RelaxedLeashCandidateCount, Is.EqualTo(0));
            Assert.That(plan.CandidateMask, Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void RocketFaceRandomWalk_LeashBoundary_BoxedReturnPath_UsesRelaxedLeashInsteadOfStaying()
        {
            var homeCell = new SurfaceCell(FaceId.Front, 0, 4);
            var sourceCell = new SurfaceCell(FaceId.Front, 5, 4);
            var forwardDestroyTile = new SurfaceCell(FaceId.Front, 5, 5);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 5,
                forwardWeight: 10,
                sideWeight: 1,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Front, 4, 4)),
                    CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Front, 6, 4)),
                },
                new BoardBounds(new Vector2Int(0, 3), new Vector2Int(6, 5)),
                new[] { CreateDestroyTile(100, forwardDestroyTile) });
            var source = GetEntity(worldState, 40);
            var inactiveDestroyTileDefinitions = new[]
            {
                CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly),
            };

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                worldState.CreateSnapshot(),
                source,
                tickIndex: 9,
                new EnemyPatrolRuntimeState
                {
                    sequence = 3,
                    homeCell = homeCell,
                    lastCommittedDirection = Direction.Up,
                },
                settings,
                inactiveDestroyTileDefinitions);

            Assert.That(EnemyMovementStrategyShared.CanTraverseStep(
                    worldState.CreateSnapshot(),
                    source,
                    Vector2Int.up,
                    inactiveDestroyTileDefinitions),
                Is.True);
            Assert.That(worldState.CreateSnapshot().TryGetPlacementBlocker(
                    EntityType.Unit,
                    forwardDestroyTile,
                    source.entityId,
                    out _),
                Is.False);
            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Up));
            Assert.That(plan.SelectedDestination, Is.EqualTo(forwardDestroyTile));
            Assert.That(plan.SelectedPass, Is.EqualTo(RandomWalkLeashSelectionPass.RelaxedLeash));
            Assert.That(plan.StrictCandidateCount, Is.EqualTo(0));
            Assert.That(plan.RelaxedLeashCandidateCount, Is.GreaterThan(0));
            Assert.That((plan.CandidateMask & GetCandidateMaskBit(Direction.Left)) == 0, Is.True);
            Assert.That((plan.CandidateMask & GetCandidateMaskBit(Direction.Right)) == 0, Is.True);
        }

        [Test]
        [Category("Core")]
        public void RandomWalkLeash_ImmediateBackwardPolicy_IsNotChanged()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 4);
            var sourceCell = new SurfaceCell(FaceId.Floor, 5, 4);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 5,
                forwardWeight: 10,
                sideWeight: 1,
                backwardWeight: 10,
                preventImmediateBacktrack: true);
            var plan = BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                new[]
                {
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 4, 4)),
                    CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, 6, 4)),
                },
                new BoardBounds(new Vector2Int(0, 3), new Vector2Int(6, 5)),
                tickIndex: 9,
                new EnemyPatrolRuntimeState
                {
                    sequence = 3,
                    homeCell = homeCell,
                    lastCommittedDirection = Direction.Up,
                });

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.SelectedPass, Is.EqualTo(RandomWalkLeashSelectionPass.RelaxedLeash));
            Assert.That(plan.RelaxedLeashCandidateCount, Is.EqualTo(2));
            Assert.That((plan.CandidateMask & GetCandidateMaskBit(Direction.Down)) == 0, Is.True);
            Assert.That(plan.PlannedDirection, Is.Not.EqualTo(Direction.Down));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_OutsideLeash_NoPath_IsDeterministic()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 0,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);
            var blockers = new[]
            {
                CreateWall(entityId: 30, position: new Vector2Int(0, 1)),
                CreateWall(entityId: 31, position: new Vector2Int(1, 2)),
                CreateWall(entityId: 32, position: new Vector2Int(2, 1)),
                CreateWall(entityId: 33, position: new Vector2Int(1, 0)),
            };

            var first = BuildRandomWalkPlan(sourceCell, homeCell, settings, blockers, tickIndex: 9);
            var second = BuildRandomWalkPlan(sourceCell, homeCell, settings, blockers, tickIndex: 9);

            Assert.That(first.HasDirection, Is.False);
            Assert.That(first.PlannedDirection, Is.EqualTo(Direction.None));
            Assert.That(first.CandidateMask, Is.Zero);
            Assert.That(second.HasDirection, Is.EqualTo(first.HasDirection));
            Assert.That(second.PlannedDirection, Is.EqualTo(first.PlannedDirection));
            Assert.That(second.CandidateMask, Is.EqualTo(first.CandidateMask));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_EqualCostDetours_UsesCanonicalTieBreak()
        {
            var homeCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceCell = new SurfaceCell(FaceId.Floor, 4, 1);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);

            for (var i = 0; i < 4; i++)
            {
                var plan = BuildRandomWalkPlan(
                    sourceCell,
                    homeCell,
                    settings,
                    new[] { CreateWall(entityId: 30, position: new Vector2Int(3, 1)) },
                    tickIndex: 9 + i);

                Assert.That(plan.HasDirection, Is.True);
                Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Up));
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalkPatrolPlanner_OutsideLeash_SearchDepthUsesCurrentDistance()
        {
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 1,
                forwardWeight: 4,
                sideWeight: 2,
                backwardWeight: 1,
                preventImmediateBacktrack: true);

            var reachablePlan = BuildRandomWalkPlan(
                new SurfaceCell(FaceId.Floor, 15, 0),
                new SurfaceCell(FaceId.Floor, 0, 0),
                settings,
                Array.Empty<EntityState>(),
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(15, 0)),
                tickIndex: 9);
            var cappedPlan = BuildRandomWalkPlan(
                new SurfaceCell(FaceId.Floor, 60, 0),
                new SurfaceCell(FaceId.Floor, 0, 0),
                settings,
                Array.Empty<EntityState>(),
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(60, 0)),
                tickIndex: 9);

            Assert.That(reachablePlan.HasDirection, Is.True);
            Assert.That(reachablePlan.PlannedDirection, Is.EqualTo(Direction.Left));
            Assert.That(cappedPlan.HasDirection, Is.True);
            Assert.That(cappedPlan.PlannedDirection, Is.EqualTo(Direction.Left));
            Assert.That(cappedPlan.SelectedPass, Is.EqualTo(RandomWalkLeashSelectionPass.RelaxedLeash));
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

            var legacyPlan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 5,
                patrolState,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>());
            var built = EnemyPatrolDecisionPlanner.TryBuildProposal(
                snapshot,
                source,
                tickIndex: 5,
                PatrolStrategyKind.RandomWalk,
                patrolState,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
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

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 3,
                patrolState,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>());

            Assert.That((plan.CandidateMask & (1 << 0)) == 0, Is.True, "Topology-changing up-step must never be emitted as a patrol candidate.");
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalk_GroundUnit_ChoosesSafePoolBeforeDestroyTileCandidates()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var riskyCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 1), new Vector2Int(2, 1)),
                new[] { CreateDestroyTile(100, riskyCell) });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 3,
                forwardWeight: 10,
                backwardWeight: 1);

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 5,
                new EnemyPatrolRuntimeState { sequence = 1, homeCell = sourceCell },
                settings,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.PlannedDirection, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalk_GroundUnit_StopsWhenAllLegalCandidatesAreDestroyTiles()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 1), new Vector2Int(2, 1)),
                new[]
                {
                    CreateDestroyTile(100, new SurfaceCell(FaceId.Floor, 2, 1)),
                    CreateDestroyTile(101, new SurfaceCell(FaceId.Floor, 0, 1)),
                });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 3,
                forwardWeight: 10,
                backwardWeight: 1);

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 5,
                new EnemyPatrolRuntimeState { sequence = 1, homeCell = sourceCell },
                settings,
                new[]
                {
                    CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly),
                    CreateTileFeatureDefinition(101, TileFeatureActivationRule.BottomFaceOnly),
                });

            Assert.That(plan.HasDirection, Is.False);
            Assert.That(plan.CandidateMask, Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void EnemyRandomWalk_AirUnit_CanChooseDestroyTileCandidates()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(
                        entityId: 40,
                        teamId: 2,
                        position: sourceCell,
                        aiMode: EnemyAiMode.Patrol,
                        facing: Direction.Right,
                        unitMobilityKind: UnitMobilityKind.Air),
                },
                new BoardBounds(new Vector2Int(0, 1), new Vector2Int(2, 1)),
                new[]
                {
                    CreateDestroyTile(100, new SurfaceCell(FaceId.Floor, 2, 1)),
                    CreateDestroyTile(101, new SurfaceCell(FaceId.Floor, 0, 1)),
                });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                leashRadius: 3,
                forwardWeight: 10,
                backwardWeight: 1);

            var plan = EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex: 5,
                new EnemyPatrolRuntimeState { sequence = 1, homeCell = sourceCell },
                settings,
                new[]
                {
                    CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly),
                    CreateTileFeatureDefinition(101, TileFeatureActivationRule.BottomFaceOnly),
                });

            Assert.That(plan.HasDirection, Is.True);
            Assert.That(plan.CandidateMask, Is.EqualTo(GetCandidateMaskBit(Direction.Right) | GetCandidateMaskBit(Direction.Left)));
        }



        [Test]
        [Category("Extended")]
        public void EnemyLogic_RandomWalkPatrol_CapturesPatrolOrigin_WhenLeavingPatrolBeforeFirstCommittedMove()
        {
            var profile = CreateNonAttackingEnemyProfile();
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
        public void EnemyLogic_RandomWalkPatrolState_CommitsOnlyOnKinematicMovementCommit()
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
                var pipeline = CreateEnemyPipeline(worldState, profile);
                var tick = pipeline.RunTick(new TickInput(1));
                var patrolState = GetEnemyPatrolState(worldState, 40);

                Assert.That(tick.Trace.Text, Does.Contain("EnemyPatrolStateUpdated|E=40|Label=Initialized"));
                Assert.That(tick.Trace.Text, Does.Contain("EnemyPatrolStateUpdated|").And.Contain("|E=40|").And.Contain("Label=CommittedMove"));
                Assert.That(
                    tick.PresentationData.KinematicMotionTracks.Any(track =>
                        track.EntityId == 40 &&
                        track.MotionMode == MotionMode.Voluntary),
                    Is.True);
                Assert.That(patrolState.IsInitialized, Is.True);
                Assert.That(patrolState.homeCell, Is.EqualTo(homeCell));
                Assert.That(patrolState.sequence, Is.GreaterThanOrEqualTo(2));
                Assert.That(patrolState.lastCommittedDirection, Is.Not.EqualTo(Direction.None));
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
                            CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                            CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                        },
                        new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0))),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop)),
                new ForwardPatrolFixture(
                    "BlockedBackward",
                    CreateWorldState(
                        new[]
                        {
                            CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
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
                    EnemyAiCommonSettings.CreateStandard(),
                    fixture.Settings,
                    Array.Empty<TileFeatureRuntimeDefinition>(),
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
        public void EnemyLogic_PatrolDecisionProposal_SkipsInitializationBuild_WhenPatrolStateAlreadyInitialized()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.RandomWalk,
                PatrolSettings = new PatrolSettings(
                    PatrolBlockedMovementResponse.Stop,
                    leashRadius: -1,
                    forwardWeight: 1,
                    sideWeight: 0,
                    backwardWeight: 0),
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
            });
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(4, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(5, 1)));
            var existingState = new EnemyPatrolRuntimeState
            {
                sequence = 7,
                homeCell = new SurfaceCell(FaceId.Floor, 0, 0),
                lastCommittedDirection = Direction.Left,
            };
            worldState.CreateWriteContext().SetEnemyPatrolState(40, existingState);
            List<string> updates = null;

            try
            {
                Assert.DoesNotThrow(() => CommitPreMovementState(worldState, profile, out updates));

                Assert.That(worldState.CreateSnapshot().TryGetEnemyPatrolState(40, out var patrolState), Is.True);
                Assert.That(patrolState.sequence, Is.EqualTo(existingState.sequence));
                Assert.That(patrolState.homeCell, Is.EqualTo(existingState.homeCell));
                Assert.That(patrolState.lastCommittedDirection, Is.EqualTo(existingState.lastCommittedDirection));
                Assert.That(updates.Any(update => update.Contains("EnemyPatrolStateUpdated|E=40", StringComparison.Ordinal)), Is.False);
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
        public void EnemyDoesNotGenerateOrdinaryMovementFallbackWhenLocalEngagementHeld()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking(includePassiveContact: true);
            var logic = new EnemyLogic(entityId: 40, profile);
            var movementBuffer = new List<RawMovementIntent>();
            var debugEvents = new List<string>();
            ((IPhasedStateCommitContext)worldState.CreateWriteContext()).SetPhasedState(
                10,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), movementBuffer);
                ((IMovementEntityDebugLogic)logic).CollectMovementDebugEvents(
                    worldState.CreateSnapshot(),
                    new TickInput(1),
                    movementBuffer,
                    debugEvents);

                Assert.That(movementBuffer.Where(intent => intent.SourceId == 40), Is.Empty);
                Assert.That(debugEvents, Has.Some.Contains("OrdinaryMovementSuppressedByLocalEngagement"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDoesNotTransitionToPatrolWhenSameCellPlayerFlipPhases()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking(includePassiveContact: true);
            var logic = new EnemyLogic(entityId: 40, profile);
            var transitions = new List<string>();
            ((IPhasedStateCommitContext)worldState.CreateWriteContext()).SetPhasedState(
                10,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));

            try
            {
                ((IEnemyAiStateLogic)logic).CommitAiTransitions(
                    worldState.CreateSnapshot(),
                    new TickInput(1),
                    EnemyAiTransitionStage.BeforeMovement,
                    worldState.CreateWriteContext(),
                    transitions);

                var enemy = GetEntity(worldState, 40);
                Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(transitions, Has.Some.Contains("Reason=LocalEngagementHeldSameCell"));
                Assert.That(transitions, Has.None.Contains("To=Patrol"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void PassiveContactStillUsesSharedLocalContactCandidate()
        {
            var profile = EnemyAiProfileTestFactory.CreateStationaryPassiveContact();
            var sharedCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var attackBuffer = new List<RawAttackIntent>();
            ((IPhasedStateCommitContext)worldState.CreateWriteContext()).SetPhasedState(
                10,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));

            try
            {
                logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), attackBuffer);

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
        public void TruePatrolOnlyProfileDoesNotGainLocalHoldUnlessExplicitlyAllowed()
        {
            var profile = CreateForwardPatrolOnlyProfile(new PatrolSettings(PatrolBlockedMovementResponse.Stop));
            var sharedCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 2)));
            var logic = new EnemyLogic(entityId: 40, profile);
            var movementBuffer = new List<RawMovementIntent>();
            var attackBuffer = new List<RawAttackIntent>();
            ((IPhasedStateCommitContext)worldState.CreateWriteContext()).SetPhasedState(
                10,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));

            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), movementBuffer);
                logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), attackBuffer);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, Destination: new Vector2Int(3, 1), Command: MovementCommandKind.Move),
                    },
                    movementBuffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
                Assert.That(attackBuffer, Is.Empty);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void PhaseThroughExplicitMovementNotSuppressedByLocalEngagement()
        {
            var profile = CreatePhaseThroughEnemyProfile();
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: sourceCell, aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 20, teamId: 1, position: lockedTargetCell, aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Attack, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)));
            var logic = new EnemyLogic(entityId: 40, profile);
            worldState.CreateWriteContext().SetEnemyActionState(
                40,
                CreateExecutableMeleeActionState(
                    sourceCell,
                    targetEntityId: 20,
                    direction: Direction.Right,
                    startTick: 1,
                    executeTick: 5));
            ((IPhasedStateCommitContext)worldState.CreateWriteContext()).SetPhasedState(
                10,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 5));

            try
            {
                var updates = CommitPreMovementState(logic, worldState, tickIndex: 5);

                Assert.That(updates, Has.Some.Contains("PhaseEnter|Entity=40"));
                Assert.That(updates, Has.Some.Contains("Rule=LockedTargetCrossThrough"));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void JumpAndChargeProgressionNotSuppressedByLocalEngagement()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var jumpProfile = CreateJumpEnemyProfile();
            var jumpWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var jumpLogic = new EnemyLogic(entityId: 40, jumpProfile);
            jumpWorldState.CreateWriteContext().SetEnemyJumpState(
                40,
                CreateEnemyJumpState(
                    EnemyJumpPhase.Windup,
                    sourceCell: sharedCell,
                    lockedTargetCell: new SurfaceCell(FaceId.Floor, 2, 0),
                    landingTick: 5));
            ((IPhasedStateCommitContext)jumpWorldState.CreateWriteContext()).SetPhasedState(
                10,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 5));

            var chargeProfile = CreateChargingEnemyProfile(moveCooldownTicks: 0);
            var chargeWorldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: sharedCell, aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: sharedCell, aiMode: EnemyAiMode.Charge, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)));
            var chargeLogic = new EnemyLogic(entityId: 40, chargeProfile);
            chargeWorldState.CreateWriteContext().SetEnemyChargeState(
                40,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    windupEndTick = 1,
                    remainingActiveSteps = 1,
                    recoverRemainingTicks = 0,
                });
            ((IPhasedStateCommitContext)chargeWorldState.CreateWriteContext()).SetPhasedState(
                10,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 5));
            var chargeMovementBuffer = new List<RawMovementIntent>();

            try
            {
                var jumpUpdates = CommitPreMovementState(jumpLogic, jumpWorldState, tickIndex: 5);
                chargeLogic.CollectMovementIntents(chargeWorldState.CreateSnapshot(), new TickInput(5), chargeMovementBuffer);

                Assert.That(jumpUpdates, Has.Some.Contains("EnemyJumpStateUpdated|E=40|Label=Takeoff"));
                Assert.That(jumpWorldState.CreateSnapshot().TryGetEnemyJumpState(40, out var jumpState), Is.True);
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                    },
                    chargeMovementBuffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
            }
            finally
            {
                DestroyProfile(jumpProfile);
                DestroyProfile(chargeProfile);
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
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());
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
        public void EnemyChase_MoveIntoPlayerOccupiedCell_SucceedsAndStacks()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var target = GetEntity(worldState, 10);

            var builtIntent = AxisPriorityChaseStrategy.Instance.TryBuildMovementIntent(
                snapshot,
                source,
                target,
                EnemyAiCommonSettings.CreateStandard(),
                ChaseSettings.CreateDefault(),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Core")]
        public void EnemyChase_GroundUnit_PrefersSafeSecondaryAxisOverDestroyTilePrimary()
        {
            var riskyPrimary = new SurfaceCell(FaceId.Floor, 1, 0);
            var safeSecondary = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 1), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                new[] { CreateDestroyTile(100, riskyPrimary) });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var target = GetEntity(worldState, 10);

            var builtIntent = AxisPriorityChaseStrategy.Instance.TryBuildMovementIntent(
                snapshot,
                source,
                target,
                EnemyAiCommonSettings.CreateStandard(),
                ChaseSettings.CreateDefault(),
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(safeSecondary.PlanarPosition));
            Assert.That(EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, Vector2Int.right), Is.True);
        }

        [Test]
        [Category("Core")]
        public void GroundChase_WhenDirectStepIsDestroyTile_ChoosesNeutralPerpendicularAvoidance()
        {
            var directHazard = new SurfaceCell(FaceId.Floor, 2, 1);
            var expectedAvoidance = new SurfaceCell(FaceId.Floor, 3, 2);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(3, 1), aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                },
                new BoardBounds(new Vector2Int(1, 0), new Vector2Int(3, 2)),
                new[] { CreateDestroyTile(100, directHazard) });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var target = GetEntity(worldState, 10);

            var builtIntent = AxisPriorityChaseStrategy.Instance.TryBuildMovementIntent(
                snapshot,
                source,
                target,
                EnemyAiCommonSettings.CreateStandard(),
                ChaseSettings.CreateDefault(),
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(expectedAvoidance.PlanarPosition));
            Assert.That(intent.Destination, Is.Not.EqualTo(directHazard.PlanarPosition));
        }

        [Test]
        [Category("Core")]
        public void GroundChase_WhenDirectStepIsDestroyTileAndNoSafeAvoidance_StaysStill()
        {
            var directHazard = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(3, 1), aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                },
                new BoardBounds(new Vector2Int(1, 1), new Vector2Int(3, 1)),
                new[] { CreateDestroyTile(100, directHazard) });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var target = GetEntity(worldState, 10);

            var builtIntent = AxisPriorityChaseStrategy.Instance.TryBuildMovementIntent(
                snapshot,
                source,
                target,
                EnemyAiCommonSettings.CreateStandard(),
                ChaseSettings.CreateDefault(),
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                out _);

            Assert.That(builtIntent, Is.False);
            Assert.That(EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, Vector2Int.left), Is.True);
        }

        [Test]
        [Category("Core")]
        public void DestroyTile_RemainsTraversalLegal_ForGroundUnit()
        {
            var directHazard = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(3, 1), aiMode: EnemyAiMode.Chase, facing: Direction.Left),
                },
                new BoardBounds(new Vector2Int(2, 1), new Vector2Int(3, 1)),
                new[] { CreateDestroyTile(100, directHazard) });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);

            Assert.That(EnemyMovementStrategyShared.CanTraverseStep(snapshot, source, Vector2Int.left), Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyChase_AirUnit_KeepsPrimaryPriorityOverDestroyTile()
        {
            var riskyPrimary = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 1), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right, unitMobilityKind: UnitMobilityKind.Air),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                new[] { CreateDestroyTile(100, riskyPrimary) });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var target = GetEntity(worldState, 10);

            var builtIntent = AxisPriorityChaseStrategy.Instance.TryBuildMovementIntent(
                snapshot,
                source,
                target,
                EnemyAiCommonSettings.CreateStandard(),
                ChaseSettings.CreateDefault(),
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(riskyPrimary.PlanarPosition));
            Assert.That(
                TileFeatureHazardQueries.EvaluateTileApproachRisk(
                    snapshot,
                    new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) },
                    source,
                    riskyPrimary),
                Is.EqualTo(TileApproachRisk.Neutral));
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_ChaseMode_FallsBackToSecondaryAxisWhenPrimaryStepIsBlockedBySolid()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 1), aiMode: EnemyAiMode.None),
                CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());
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

        [Test]
        [Category("Extended")]
        public void EnemyLogic_JumpInitialDelay_BlocksFirstStartUntilDelayCompletes()
        {
            var profile = EnemyAiProfileTestFactory.CreateJumpChaser(
                new EnemyJumpTimingSettings(
                    initialDelayTicks: 2,
                    windupTicks: 1,
                    airborneTicks: 1,
                    cooldownTicks: 1));
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            var logic = new EnemyLogic(entityId: 40, profile);

            try
            {
                var delayedUpdates = CommitPreMovementState(logic, worldState, tickIndex: 1);

                Assert.That(delayedUpdates, Has.Some.Contains("Label=InitialDelayTick"));
                Assert.That(delayedUpdates, Has.None.Contains("Label=Start"));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(40, out var delayed), Is.True);
                Assert.That(delayed.phase, Is.EqualTo(EnemyJumpPhase.None));
                Assert.That(delayed.initialDelayInitialized, Is.True);
                Assert.That(delayed.initialDelayTicksRemaining, Is.EqualTo(1));

                var startUpdates = CommitPreMovementState(logic, worldState, tickIndex: 2);

                Assert.That(startUpdates, Has.Some.Contains("Label=InitialDelayReady"));
                Assert.That(startUpdates, Has.Some.Contains("Label=Start"));
                Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(40, out var started), Is.True);
                Assert.That(started.phase, Is.EqualTo(EnemyJumpPhase.Windup));
                Assert.That(started.initialDelayInitialized, Is.True);
                Assert.That(started.initialDelayTicksRemaining, Is.Zero);
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
                CreateExecutableMeleeActionState(
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    targetEntityId: 10,
                    direction: Direction.Right,
                    startTick: 1,
                    executeTick: 5));

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
        public void EnemyLogic_JumpCooldown_DoesNotSuppressMovement()
        {
            var profile = CreateJumpEnemyProfile();
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var movementBuffer = new List<RawMovementIntent>();
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                CreateEnemyJumpState(
                    EnemyJumpPhase.Cooldown,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell: new SurfaceCell(FaceId.Floor, 2, 0),
                    landingTick: 4,
                    cooldownRemainingTicks: 2));
            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(5), movementBuffer);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                    },
                    movementBuffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyLogic_JumpLandingTick_SuppressesMovement()
        {
            var profile = CreateJumpEnemyProfile();
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, profile);
            var movementBuffer = new List<RawMovementIntent>();
            worldState.CreateWriteContext().SetEnemyJumpState(
                40,
                CreateEnemyJumpState(
                    EnemyJumpPhase.Cooldown,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell: new SurfaceCell(FaceId.Floor, 2, 0),
                    landingTick: 5,
                    cooldownRemainingTicks: 2));
            try
            {
                logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(5), movementBuffer);

                Assert.That(movementBuffer, Is.Empty);
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
        public void EnemyLogic_AttackMode_WithoutActiveActionState_DoesNotProduceRawAttackIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());
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
            var settings = AttackDecisionSettings.CreateAdjacentRange();
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
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());
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
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());
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
            var logic = new EnemyLogic(entityId: 40, CreateWindupProjectileRuntimeDefinition());
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
        public void CrossLineOfSight_detects_target_on_same_row_without_blocker()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 3, out var target);

            Assert.That(detected, Is.True);
            Assert.That(target.entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_detects_target_on_same_column_without_blocker()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(0, 3), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Up),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 3, out var target);

            Assert.That(detected, Is.True);
            Assert.That(target.entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_does_not_detect_diagonal_target()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 2), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 4, out _);

            Assert.That(detected, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_does_not_detect_beyond_range()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 3, out _);

            Assert.That(detected, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_is_blocked_by_wall_solid()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), aiMode: EnemyAiMode.None),
                CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 2, out _);

            Assert.That(detected, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_is_blocked_by_box_solid()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), aiMode: EnemyAiMode.None),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 2, out _);

            Assert.That(detected, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_Default_BlockedBySolid()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 8, out _);

            Assert.That(detected, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void NonGlider_CrossLineOfSight_StillBlockedBySolid()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 8, out _);

            Assert.That(detected, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void DefaultDetectionUsersUnaffected()
        {
            var profile = CreateCrossLineNonGliderProfile();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                    CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)));

            try
            {
                var tick = CreateEnemyPipeline(worldState, profile).RunTick(new TickInput(1));

                Assert.That(tick.FinalEntities.Single(entity => entity.entityId == 40).aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_does_not_treat_target_cell_as_blocker()
        {
            var targetCell = new Vector2Int(2, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: targetCell, aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 2, out var target);

            Assert.That(detected, Is.True);
            Assert.That(target.entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_chooses_nearest_visible_target()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(0, 2), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Up),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 4, out var target);

            Assert.That(detected, Is.True);
            Assert.That(target.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_ignores_blocked_nearer_target_and_can_choose_farther_visible_target()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(0, 3), aiMode: EnemyAiMode.None),
                CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Up),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 4, out var target);

            Assert.That(detected, Is.True);
            Assert.That(target.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_tie_breaks_by_existing_entity_order()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(0, 2), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 2, out var target);

            Assert.That(detected, Is.True);
            Assert.That(target.entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
        public void CrossLineOfSight_rejects_different_face_target()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new SurfaceCell(FaceId.Front, 0, 3), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Up),
            });

            var detected = TryFindCrossLineOfSightTarget(worldState, sourceEntityId: 40, senseRange: 3, out _);

            Assert.That(detected, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void DetectionStrategyKind_cross_line_of_sight_compiles_to_new_strategy()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                DetectionStrategyKind = DetectionStrategyKind.CrossLineOfSightOpponent,
                DetectionSettings = new DetectionSettings(senseRange: 5, requireSameFace: false, canTargetMarkedForDeath: false),
            });

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(definition.Brain.Detection.Kind, Is.EqualTo(DetectionStrategyKind.CrossLineOfSightOpponent));
                Assert.That(definition.Brain.Detection.Strategy, Is.SameAs(CrossLineOfSightOpponentDetectionStrategy.Instance));
                Assert.That(definition.Brain.Detection.Settings.SenseRange, Is.EqualTo(5));
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void ForwardPatrolStrategy_BlockedMovementResponseSetting_ChangesMovementOutcome()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 30, position: new Vector2Int(1, 0)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var strategy = ForwardPatrolStrategy.Instance;

            var stopped = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out _);
            var steppedBackward = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.TryStepBackward),
                Array.Empty<TileFeatureRuntimeDefinition>(),
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
                EnemyAiCommonSettings.CreateStandard(),
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(facingChanged, Is.False);
            Assert.That(facing, Is.EqualTo(Direction.Left));
            Assert.That(EnemyMovementStrategyShared.HasWallFollowAnchor(snapshot, source, settings), Is.True);
            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_UnitOccupiedCandidate_IsNotRejectedBecauseOfUnit()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 1)),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
            });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateStandard(),
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_LeftHandRule_LeftOpenChoosesLeftBeforeForward()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var source = GetEntity(worldState, 40);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                worldState.CreateSnapshot(),
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(0, 1)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_LeftHandRule_LeftBlockedForwardOpenChoosesForward()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(0, 1)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var source = GetEntity(worldState, 40);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                worldState.CreateSnapshot(),
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(1, 2)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_LeftHandRule_LeftForwardBlockedRightOpenChoosesRightNotBack()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(0, 1)),
                CreateWall(entityId: 91, position: new Vector2Int(1, 2)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var source = GetEntity(worldState, 40);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                worldState.CreateSnapshot(),
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(intent.Destination, Is.Not.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_DeadEnd_BackOnlyWhenLeftForwardRightBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(0, 1)),
                CreateWall(entityId: 91, position: new Vector2Int(1, 2)),
                CreateWall(entityId: 92, position: new Vector2Int(2, 1)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var source = GetEntity(worldState, 40);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                worldState.CreateSnapshot(),
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_RightHandRule_MirrorsLeftHandOrder()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(2, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var source = GetEntity(worldState, 40);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                worldState.CreateSnapshot(),
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_RightHandRule_RightForwardLeftBackTruthTable()
        {
            var sourceCell = new Vector2Int(1, 1);
            var openBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3));

            AssertRightHandChoice(
                "right before forward",
                sourceCell,
                openBounds,
                new[] { CreateWall(entityId: 90, position: new Vector2Int(2, 0)) },
                Direction.Right,
                new Vector2Int(2, 1),
                new Vector2Int(1, 2));
            AssertRightHandChoice(
                "board edge blocks right before forward",
                new Vector2Int(2, 1),
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                Array.Empty<EntityState>(),
                Direction.Up,
                new Vector2Int(2, 2),
                new Vector2Int(2, 0));
            AssertRightHandChoice(
                "left before back",
                sourceCell,
                openBounds,
                new[]
                {
                    CreateWall(entityId: 91, position: new Vector2Int(2, 1)),
                    CreateWall(entityId: 92, position: new Vector2Int(1, 2)),
                    CreateWall(entityId: 93, position: new Vector2Int(0, 0)),
                    CreateWall(entityId: 94, position: new Vector2Int(2, 0)),
                },
                Direction.Left,
                new Vector2Int(0, 1),
                new Vector2Int(1, 0));
            AssertRightHandChoice(
                "back only after right forward left",
                sourceCell,
                openBounds,
                new[]
                {
                    CreateWall(entityId: 95, position: new Vector2Int(2, 1)),
                    CreateWall(entityId: 96, position: new Vector2Int(1, 2)),
                    CreateWall(entityId: 97, position: new Vector2Int(0, 1)),
                },
                Direction.Down,
                new Vector2Int(1, 0));

            static void AssertRightHandChoice(
                string label,
                Vector2Int sourcePosition,
                BoardBounds boardBounds,
                IEnumerable<EntityState> blockers,
                Direction expectedDirection,
                Vector2Int expectedDestination,
                params Vector2Int[] rejectedDestinations)
            {
                var entities = blockers.ToList();
                entities.Add(CreateUnit(
                    entityId: 40,
                    teamId: 2,
                    position: sourcePosition,
                    aiMode: EnemyAiMode.Patrol,
                    facing: Direction.Up));
                var worldState = CreateWorldState(entities, boardBounds);
                var snapshot = worldState.CreateSnapshot();
                var source = GetEntity(worldState, 40);
                var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right);

                Assert.That(
                    EnemyMovementStrategyShared.ChooseWallFollowDirection(
                        snapshot,
                        source,
                        settings,
                        Array.Empty<TileFeatureRuntimeDefinition>(),
                        out var direction),
                    Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection),
                    label);
                Assert.That(direction, Is.EqualTo(expectedDirection), label);

                var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                    snapshot,
                    source,
                    EnemyAiCommonSettings.CreateStandard(),
                    settings,
                    Array.Empty<TileFeatureRuntimeDefinition>(),
                    out var intent);

                Assert.That(builtIntent, Is.True, label);
                Assert.That(intent.Destination, Is.EqualTo(expectedDestination), label);
                foreach (var rejectedDestination in rejectedDestinations)
                {
                    Assert.That(intent.Destination, Is.Not.EqualTo(rejectedDestination), label);
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_LeftHandRule_HandBackDiagonalPreservesCandidateOrder()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(0, 1)),
                CreateWall(entityId: 91, position: new Vector2Int(1, 2)),
                CreateWall(entityId: 92, position: new Vector2Int(0, 0)),
                CreateWall(entityId: 93, position: new Vector2Int(2, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var source = GetEntity(worldState, 40);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                worldState.CreateSnapshot(),
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(intent.Destination, Is.Not.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_EmptySpace_SeeksForward_DoesNotLeftTurnLoop()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(8, 8));
            var path = SimulateWallFollowPath(
                WallFollowTurnPreference.Left,
                new SurfaceCell(FaceId.Floor, 3, 3),
                Direction.Up,
                boardBounds,
                steps: 4);

            Assert.That(path.Directions[0], Is.EqualTo(Direction.Up));
            Assert.That(path.Positions.Select(cell => cell.PlanarPosition).ToArray(), Is.EqualTo(new[]
            {
                new Vector2Int(3, 3),
                new Vector2Int(3, 4),
                new Vector2Int(3, 5),
                new Vector2Int(3, 6),
                new Vector2Int(3, 7),
            }));
            Assert.That(HasRepeatedFourCellCycle(path.Positions), Is.False);
            Assert.That(HasImmediateTwoCellBounce(path.Positions), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_EmptySpace_RightHand_SeeksForward_DoesNotRightTurnLoop()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(8, 8));
            var path = SimulateWallFollowPath(
                WallFollowTurnPreference.Right,
                new SurfaceCell(FaceId.Floor, 3, 3),
                Direction.Up,
                boardBounds,
                steps: 4);

            Assert.That(path.Directions[0], Is.EqualTo(Direction.Up));
            Assert.That(path.Positions.Select(cell => cell.PlanarPosition).ToArray(), Is.EqualTo(new[]
            {
                new Vector2Int(3, 3),
                new Vector2Int(3, 4),
                new Vector2Int(3, 5),
                new Vector2Int(3, 6),
                new Vector2Int(3, 7),
            }));
            Assert.That(HasRepeatedFourCellCycle(path.Positions), Is.False);
            Assert.That(HasImmediateTwoCellBounce(path.Positions), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_FrontBoundary_LeftHand_AcquiresBoundaryOnLeft()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 2)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left);
            var strategy = WallFollowPatrolStrategy.Instance;

            var chosen = EnemyMovementStrategyShared.ChooseWallFollowDirection(
                snapshot,
                source,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var direction);
            var builtIntent = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateStandard(),
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(chosen, Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection));
            Assert.That(direction, Is.EqualTo(Direction.Right));
            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_FrontBoundary_RightHand_AcquiresBoundaryOnRight()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 2)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right);
            var strategy = WallFollowPatrolStrategy.Instance;

            var chosen = EnemyMovementStrategyShared.TryChooseWallFollowDirection(snapshot, source, settings, out var direction);
            var builtIntent = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateStandard(),
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(chosen, Is.True);
            Assert.That(direction, Is.EqualTo(Direction.Left));
            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(0, 1)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_ConvexCorner_UsesTrailingHandCornerToTurnPreferred()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 90, position: new Vector2Int(0, 0)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left);
            var strategy = WallFollowPatrolStrategy.Instance;

            var facingChanged = ((IPatrolFacingStrategy)strategy).TryResolveFacing(snapshot, source, settings, out var facing);
            var builtIntent = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateStandard(),
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(EnemyMovementStrategyShared.HasWallFollowAnchor(snapshot, source, settings), Is.False);
            Assert.That(facingChanged, Is.True);
            Assert.That(facing, Is.EqualTo(Direction.Left));
            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(0, 1)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_LeftWall_PreservesClassicHandRule()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(0, 1)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var source = GetEntity(worldState, 40);

            var chosen = EnemyMovementStrategyShared.ChooseWallFollowDirection(
                worldState.CreateSnapshot(),
                source,
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var direction);

            Assert.That(chosen, Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection));
            Assert.That(direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_RightSideBoundary_DoesNotTriggerLeftHandTurn()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(entityId: 90, position: new Vector2Int(2, 1)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3)));
            var source = GetEntity(worldState, 40);

            var chosen = EnemyMovementStrategyShared.ChooseWallFollowDirection(
                worldState.CreateSnapshot(),
                source,
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var direction);

            Assert.That(chosen, Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection));
            Assert.That(direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_BetweenTwoWalls_DoesNotBounce()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(2, 6));
            var corridorWalls = Enumerable.Range(1, 5)
                .SelectMany(y => new[]
                {
                    CreateWall(entityId: 100 + y, position: new Vector2Int(0, y)),
                    CreateWall(entityId: 200 + y, position: new Vector2Int(2, y)),
                })
                .ToArray();

            foreach (var turnPreference in new[] { WallFollowTurnPreference.Left, WallFollowTurnPreference.Right })
            {
                var path = SimulateWallFollowPath(
                    turnPreference,
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    Direction.Up,
                    boardBounds,
                    steps: 4,
                    staticEntities: corridorWalls);

                Assert.That(path.Directions, Is.All.EqualTo(Direction.Up), turnPreference.ToString());
                Assert.That(HasImmediateTwoCellBounce(path.Positions), Is.False, turnPreference.ToString());
            }
        }

        [Test]
        [Category("Extended")]
        public void WallFollow_TerrainDoesNotBecomeBoundaryAnchor()
        {
            var terrainData = new Game.Feature.Gameplay.BoardState.TerrainData(new[]
            {
                new TerrainCellState(
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    TerrainKind.Generic,
                    TerrainFlags.BlocksGroundTraversal),
            });
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3)),
                terrainData);
            var source = GetEntity(worldState, 40);

            var chosen = EnemyMovementStrategyShared.ChooseWallFollowDirection(
                worldState.CreateSnapshot(),
                source,
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var direction);

            Assert.That(chosen, Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection));
            Assert.That(direction, Is.EqualTo(Direction.Up));
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
        public void EnemyMovement_UnitOccupiedDestination_DoesNotBypassSolid()
        {
            var unitOnlyWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var solidWorld = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });

            Assert.That(
                EnemyMovementStrategyShared.TryBuildMoveIntent(
                    unitOnlyWorld.CreateSnapshot(),
                    GetEntity(unitOnlyWorld, 40),
                    EnemyAiCommonSettings.CreateStandard(),
                    Vector2Int.right,
                    out _),
                Is.True);
            Assert.That(
                EnemyMovementStrategyShared.TryBuildMoveIntent(
                    solidWorld.CreateSnapshot(),
                    GetEntity(solidWorld, 40),
                    EnemyAiCommonSettings.CreateStandard(),
                    Vector2Int.right,
                    out _),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowRule_DoesNotRankForwardByDestinationBoxBoundary()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateBox(entityId: 50, position: new Vector2Int(1, 2)),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3)));

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
        public void EnemyMovementStrategyShared_WallFollowAnchor_DoesNotTreatBoardEdgeAsAnchor()
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
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowBoundaryContext_TreatsAdjacentBoardEdgeAsTrackableBoundary()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left);

            Assert.That(EnemyMovementStrategyShared.HasWallFollowAnchor(snapshot, source, settings), Is.False);
            Assert.That(
                EnemyMovementStrategyShared.ChooseWallFollowDirection(
                    snapshot,
                    source,
                    settings,
                    Array.Empty<TileFeatureRuntimeDefinition>(),
                    out var direction),
                Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection));
            Assert.That(direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowBoundaryContext_BoardEdgeOptOutSeeksForward()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(
                PatrolBlockedMovementResponse.Stop,
                WallFollowTurnPreference.Left,
                treatBoardEdgeAsObstacleBoundary: false);

            Assert.That(
                EnemyMovementStrategyShared.ChooseWallFollowDirection(
                    snapshot,
                    source,
                    settings,
                    Array.Empty<TileFeatureRuntimeDefinition>(),
                    out var direction),
                Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection));
            Assert.That(direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_TrackableBoundaryButNoLegalMove_ReturnsNoLegalMove()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(entityId: 90, position: new Vector2Int(2, 1)),
                CreateWall(entityId: 91, position: new Vector2Int(1, 2)),
                CreateWall(entityId: 92, position: new Vector2Int(0, 1)),
                CreateWall(entityId: 93, position: new Vector2Int(1, 0)),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
            });
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right);
            var strategy = WallFollowPatrolStrategy.Instance;

            var outcome = EnemyMovementStrategyShared.ChooseWallFollowDirection(
                snapshot,
                source,
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var direction);
            var builtIntent = strategy.TryBuildMovementIntent(
                snapshot,
                source,
                EnemyAiCommonSettings.CreateStandard(),
                settings,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out _);
            var facingChanged = ((IPatrolFacingStrategy)strategy).TryResolveFacing(snapshot, source, settings, out var facing);

            Assert.That(outcome, Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.NoLegalMove));
            Assert.That(direction, Is.EqualTo(Direction.None));
            Assert.That(builtIntent, Is.False);
            Assert.That(facingChanged, Is.False);
            Assert.That(facing, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_BoardEdgeOnlyStraight_LeftHand_MovesForward()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var source = GetEntity(worldState, 40);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                worldState.CreateSnapshot(),
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(0, 2)));
        }

        [Test]
        [Category("Extended")]
        public void WallFollowPatrolStrategy_BoardEdgeOnlyCorner_LeftHand_ChoosesRightNotBack()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 2), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
            var source = GetEntity(worldState, 40);

            var builtIntent = WallFollowPatrolStrategy.Instance.TryBuildMovementIntent(
                worldState.CreateSnapshot(),
                source,
                EnemyAiCommonSettings.CreateStandard(),
                new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Left),
                Array.Empty<TileFeatureRuntimeDefinition>(),
                out var intent);

            Assert.That(builtIntent, Is.True);
            Assert.That(intent.Destination, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(intent.Destination, Is.Not.EqualTo(new Vector2Int(0, 1)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyMovementStrategyShared_WallFollowBoundaryContext_IgnoresUnitsIncludingPlayers_AndSeeksForward()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 2), aiMode: EnemyAiMode.None),
                    CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(1, 1), aiMode: EnemyAiMode.Patrol, facing: Direction.Up),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3)));

            Assert.That(
                EnemyMovementStrategyShared.ChooseWallFollowDirection(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, 40),
                    new PatrolSettings(PatrolBlockedMovementResponse.Stop, WallFollowTurnPreference.Right),
                    Array.Empty<TileFeatureRuntimeDefinition>(),
                    out var direction),
                Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection));
            Assert.That(direction, Is.EqualTo(Direction.Up));
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
                CreateWall(entityId: 93, position: new Vector2Int(1, 0)),
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
        public void EnemyAiProfile_CreateRuntimeDefinition_UsesWindupProjectileTimingAndDefaultZeroMoveCooldown()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(windupTicks: 1);

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(definition.AttackTimingSettings.WindupTicks, Is.EqualTo(1));
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
                EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(),
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
                Assert.That(combat.Kind, Is.EqualTo(AttackDecisionStrategyKind.WindupForwardCellProjectile));
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
                Assert.That(combat.Kind, Is.EqualTo(AttackDecisionStrategyKind.WindupForwardCellProjectile));
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
                    cooldownSeconds: 0.5f,
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
                Assert.That(utility.Effects[0].CooldownTicks, Is.EqualTo(5));
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
                    cooldownSeconds: 0.5f,
                    radius: 2,
                    durationSeconds: 0.3f,
                    activationDelaySeconds: 0.4f,
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
                Assert.That(utility.Effects[0].CooldownTicks, Is.EqualTo(5));
                Assert.That(utility.Effects[0].LockNearbyBoxes.Radius, Is.EqualTo(2));
                Assert.That(utility.Effects[0].LockNearbyBoxes.DurationTicks, Is.EqualTo(3));
                Assert.That(utility.Effects[0].LockNearbyBoxes.ActivationDelayTicks, Is.EqualTo(4));
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
        public void EnemyAiProfile_CreateRuntimeDefinition_UtilityCapability_LockNearbyBoxes_DefaultActivationDelayIsImmediate()
        {
            var profile = CreateUtilitySummonerProfile(CreateLockNearbyBoxesUtilityEffect());

            try
            {
                var definition = profile.CreateRuntimeDefinition(10);

                Assert.That(definition.Capabilities.TryGetUtility(out var utility), Is.True);
                Assert.That(utility.Effects[0].LockNearbyBoxes.ActivationDelayTicks, Is.Zero);
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
            EnemyAiProfileTestFactory.SetSerializedField(effect, "cooldownSeconds", 1f);
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
        public void EnemyAiProfileCompiler_UtilityCapability_LockNearbyBoxes_NegativeActivationDelay_Throws()
        {
            var profile = CreateUtilitySummonerProfile(
                CreateLockNearbyBoxesUtilityEffect(activationDelaySeconds: -0.1f));

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
                    CreateSummonUtilityEffect(cooldownSeconds: 2f),
                    CreateLockNearbyBoxesUtilityEffect(initialDelaySeconds: 0.1f, cooldownSeconds: 0.4f, radius: 1, durationSeconds: 0.2f),
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
            var defaultProfile = CreateNonAttackingEnemyProfile();
            var archetypeProfile = CreateNonAttackingEnemyProfile();
            var archetype = CreateEnemyUnitArchetypeAsset(
                "BasicMinion",
                archetypeProfile,
                hp: 4,
                initialAiMode: EnemyAiMode.Patrol,
                unitMobilityKind: UnitMobilityKind.Air);
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
                Assert.That(snapshot.SpawnDefaultsByArchetypeId[new EnemyUnitArchetypeId("BasicMinion")].UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
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
            var defaultProfile = CreateNonAttackingEnemyProfile();
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
            var defaultProfile = CreateNonAttackingEnemyProfile();
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
            var defaultProfile = CreateNonAttackingEnemyProfile();
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
            var defaultProfile = CreateNonAttackingEnemyProfile();
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
            var defaultProfile = CreateNonAttackingEnemyProfile();
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
            var defaultProfile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile();
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
            var defaultProfile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile();
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
            var defaultProfile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile();
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
        public void EnemyEntityLogicFactory_ResolveDefinition_NoBindingAndNoDefault_Throws()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 41, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.Patrol),
            });
            var factory = new EnemyEntityLogicFactory(default, hasDefaultDefinition: false);

            var exception = Assert.Throws<InvalidOperationException>(
                () => factory.ResolveDefinition(worldState.CreateSnapshot(), GetEntity(worldState, 41)));

            Assert.That(exception.Message, Does.Contain("no explicit EnemyAiProfile override"));
        }

        [Test]
        [Category("Core")]
        public void EnemyEntityLogicFactory_ResolveDefinition_UnknownBindingThrows()
        {
            var defaultProfile = CreateNonAttackingEnemyProfile();

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
            var defaultProfile = CreateNonAttackingEnemyProfile();
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
        public void EnemyAiProfileCompiler_UtilityCapability_NonPositiveCooldown_Throws()
        {
            var profile = CreateUtilitySummonerProfile(CreateSummonUtilityEffect(cooldownSeconds: 0f));

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
            EnemyAiProfileTestFactory.SetSerializedField(effect, "cooldownSeconds", 1f);
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
                new[] { CreateSummonUtilityEffect(cooldownSeconds: 2f) });

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
            var duplicateCombat = ScriptableObject.CreateInstance<WindupForwardCellProjectileCapabilityAsset>();
            createdAssets.Add(duplicateCombat);
            SetSerializedField(duplicateCombat, "attackDecisionSettings", new AttackDecisionSettings(attackRange: 1));
            SetSerializedField(duplicateCombat, "attackTimingSettings", new EnemyAttackTimingAuthoringSettings(windupSeconds: 0f));
            SetSerializedField(
                duplicateCombat,
                "windupForwardCellProjectileSettings",
                WindupForwardCellProjectileSettings.CreateDefault());
            SetSerializedField(
                profile,
                "capabilityAssets",
                new List<EnemyCapabilityAsset>
                {
                    (EnemyCapabilityAsset)createdAssets.OfType<WindupForwardCellProjectileCapabilityAsset>().First(),
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
        public void GameplayEntityLogicProviderFactory_NullProfile_RequiresExplicitEnemyAiRuntimeDefinition()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => GameplayEntityLogicProviderFactory.CreateDefault((EnemyAiProfile)null));

            Assert.That(exception.ParamName, Is.EqualTo("enemyAiProfile"));
            Assert.That(exception.Message, Does.Contain("Enemy AI profile must be explicit"));
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
                    initialDelaySeconds: 0.05f,
                    windupSeconds: 0.1f,
                    airborneSeconds: 0.2f,
                    cooldownSeconds: 0.3f),
            });

            try
            {
                var definition = profile.CreateRuntimeDefinition(60);

                Assert.That(profile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
                Assert.That(profile.JumpTimingSettings.InitialDelaySeconds, Is.EqualTo(0.05f).Within(0.0001f));
                Assert.That(profile.JumpTimingSettings.WindupSeconds, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(profile.JumpTimingSettings.AirborneSeconds, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(profile.JumpTimingSettings.CooldownSeconds, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(definition.JumpTimingSettings.InitialDelayTicks, Is.EqualTo(3));
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
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.WindupForwardCellProjectile,
                LocomotionTimingSettings = new EnemyLocomotionTimingAuthoringSettings(
                    moveCooldownSeconds: 4f / GameplayTimingProfile.DefaultSimulationTicksPerSecond),
            });

            try
            {
                var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

                Assert.That(definition.CommonSettings.RecoverTicks, Is.EqualTo(1));
                Assert.That(definition.AttackTimingSettings.WindupTicks, Is.EqualTo(3));
                Assert.That(definition.LocomotionTimingSettings.MoveCooldownTicks, Is.EqualTo(4));
                Assert.That(definition.LocomotionTimingSettings.OrdinaryKinematicMoveTicks, Is.EqualTo(4));
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
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.WindupForwardCellProjectile,
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
                Assert.That(sixtyTpsDefinition.LocomotionTimingSettings.OrdinaryKinematicMoveTicks, Is.EqualTo(2));
                Assert.That(thirtyTpsDefinition.LocomotionTimingSettings.OrdinaryKinematicMoveTicks, Is.EqualTo(2));
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
                Assert.That(definition.LocomotionTimingSettings.OrdinaryKinematicMoveTicks, Is.Zero);
            }
            finally
            {
                DestroyProfile(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyLocomotionTimingAuthoring_ExplicitOrdinaryKinematicDuration_UsesEvenCeilTicks()
        {
            var settings = new EnemyLocomotionTimingAuthoringSettings(
                moveCooldownSeconds: 0.8f,
                ordinaryKinematicMoveDurationSeconds: 0.35f);

            var runtime = settings.ToRuntimeSettings(60);

            Assert.That(runtime.MoveCooldownTicks, Is.EqualTo(48));
            Assert.That(runtime.OrdinaryKinematicMoveTicks, Is.EqualTo(22));
        }

        [Test]
        [Category("Core")]
        public void EnemyLocomotionTimingAuthoring_UnsetOrdinaryKinematicDuration_FallsBackToMoveCooldown()
        {
            var settings = new EnemyLocomotionTimingAuthoringSettings(
                moveCooldownSeconds: 0.8f,
                ordinaryKinematicMoveDurationSeconds: 0f);

            var runtime = settings.ToRuntimeSettings(60);

            Assert.That(runtime.MoveCooldownTicks, Is.EqualTo(48));
            Assert.That(runtime.OrdinaryKinematicMoveTicks, Is.EqualTo(48));
        }

        [Test]
        [Category("Core")]
        public void EnemyLocomotionTimingAuthoring_InvalidOrdinaryKinematicDuration_Throws()
        {
            var invalidDurations = new[] { -0.1f, float.NaN, float.PositiveInfinity };

            for (var i = 0; i < invalidDurations.Length; i++)
            {
                var settings = new EnemyLocomotionTimingAuthoringSettings(
                    moveCooldownSeconds: 0f,
                    ordinaryKinematicMoveDurationSeconds: invalidDurations[i]);

                Assert.Throws<ArgumentException>(() => settings.ToRuntimeSettings(60));
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
                    EnemyAiCommonSettings.CreateStandard(),
                    PatrolSettings.CreateDefault(),
                    DetectionSettings.CreateStandardEnemyDetection(),
                    ChaseSettings.CreateDefault(),
                    AttackDecisionSettings.CreateAdjacentRange(),
                    new EnemyAttackTimingSettings(windupTicks: -1),
                    ForwardPatrolStrategy.Instance,
                    NearestOpponentDetectionStrategy.Instance,
                    AxisPriorityChaseStrategy.Instance,
                    WindupForwardCellProjectileAttackDecisionStrategy.Instance,
                    DefaultEnemyAiStateResolver.Instance));

            Assert.That(exception.ParamName, Is.EqualTo("EnemyCombatCapabilityRuntime"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiRuntimeDefinition_NegativeMoveCooldownTicks_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new EnemyAiRuntimeDefinition(
                    EnemyAiCommonSettings.CreateStandard(),
                    PatrolSettings.CreateDefault(),
                    DetectionSettings.CreateStandardEnemyDetection(),
                    ChaseSettings.CreateDefault(),
                    AttackDecisionSettings.CreateAdjacentRange(),
                    EnemyAttackTimingSettings.CreateImmediate(),
                    new EnemyLocomotionTimingSettings(moveCooldownTicks: -1),
                    ForwardPatrolStrategy.Instance,
                    NearestOpponentDetectionStrategy.Instance,
                    AxisPriorityChaseStrategy.Instance,
                    NoAttackDecisionStrategy.Instance,
                    DefaultEnemyAiStateResolver.Instance));

            Assert.That(exception.ParamName, Is.EqualTo("EnemyCoreRuntime"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiRuntimeDefinition_NegativeDesiredChaseDistance_ThrowsArgumentException()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new EnemyAiRuntimeDefinition(
                    EnemyAiCommonSettings.CreateStandard(),
                    PatrolSettings.CreateDefault(),
                    DetectionSettings.CreateStandardEnemyDetection(),
                    new ChaseSettings(
                        ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak,
                        trySecondaryAxisWhenBlocked: true,
                        desiredChaseDistance: -1),
                    AttackDecisionSettings.CreateAdjacentRange(),
                    EnemyAttackTimingSettings.CreateImmediate(),
                    EnemyLocomotionTimingSettings.CreateImmediate(),
                    ForwardPatrolStrategy.Instance,
                    NearestOpponentDetectionStrategy.Instance,
                    AxisPriorityChaseStrategy.Instance,
                    NoAttackDecisionStrategy.Instance,
                    DefaultEnemyAiStateResolver.Instance));

            Assert.That(exception.ParamName, Is.EqualTo("EnemyChaseRuntime"));
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
            BoardBounds boardBounds,
            Game.Feature.Gameplay.BoardState.TerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, terrainData);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                boardBounds,
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                initialTileFeatures);
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

        private static bool TryFindCrossLineOfSightTarget(
            WorldState worldState,
            int sourceEntityId,
            int senseRange,
            out EntityState target)
        {
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(sourceEntityId, out var source), Is.True);

            return CrossLineOfSightOpponentDetectionStrategy.Instance.TryFindTarget(
                snapshot,
                source,
                new DetectionSettings(senseRange, requireSameFace: false, canTargetMarkedForDeath: false),
                out target);
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
                Array.Empty<TileFeatureRuntimeDefinition>(),
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

        private static List<string> CommitPreMovementState(
            EnemyLogic logic,
            WorldState worldState,
            int tickIndex)
        {
            var updates = new List<string>();
            ((IPreMovementStateLogic)logic).CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(tickIndex),
                worldState.CreateWriteContext(),
                updates,
                new List<PlayerActionTransition>());
            return updates;
        }

        private static EnemyAiProfile CreateEnemyProfile(int windupTicks, int moveCooldownTicks = 0)
        {
            return EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(windupTicks: windupTicks);
        }

        private static EnemyAiProfile CreateChargingEnemyProfile(int moveCooldownTicks)
        {
            return EnemyAiProfileTestFactory.CreateCharging(moveCooldownTicks);
        }

        private static EnemyAiProfile CreateNonAttackingEnemyProfile(int moveCooldownTicks = 0)
        {
            return EnemyAiProfileTestFactory.CreateNonAttacking(moveCooldownTicks);
        }

        private static EnemyAiProfile CreateCrossLineNonGliderProfile()
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                DetectionStrategyKind = DetectionStrategyKind.CrossLineOfSightOpponent,
                DetectionSettings = new DetectionSettings(
                    senseRange: 8,
                    requireSameFace: true,
                    canTargetMarkedForDeath: false),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
            });
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

        private static WallFollowPath SimulateWallFollowPath(
            WallFollowTurnPreference turnPreference,
            SurfaceCell startCell,
            Direction startFacing,
            BoardBounds boardBounds,
            int steps,
            IEnumerable<EntityState> staticEntities = null)
        {
            var positions = new List<SurfaceCell> { startCell };
            var directions = new List<Direction>();
            var currentCell = startCell;
            var currentFacing = startFacing;
            var settings = new PatrolSettings(PatrolBlockedMovementResponse.Stop, turnPreference);

            for (var i = 0; i < steps; i++)
            {
                var entities = staticEntities?.ToList() ?? new List<EntityState>();
                entities.Add(CreateUnit(
                    entityId: 40,
                    teamId: 2,
                    position: currentCell,
                    aiMode: EnemyAiMode.Patrol,
                    facing: currentFacing));
                var worldState = CreateWorldState(entities, boardBounds);
                var source = GetEntity(worldState, 40);

                Assert.That(
                    EnemyMovementStrategyShared.ChooseWallFollowDirection(
                        worldState.CreateSnapshot(),
                        source,
                        settings,
                        Array.Empty<TileFeatureRuntimeDefinition>(),
                        out var direction),
                    Is.EqualTo(EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection),
                    $"step {i}");
                Assert.That(EnemyMovementStrategyShared.TryResolveDelta(direction, out var delta), Is.True, $"step {i}");

                directions.Add(direction);
                currentCell = new SurfaceCell(currentCell.face, currentCell.x + delta.x, currentCell.y + delta.y);
                currentFacing = direction;
                positions.Add(currentCell);
            }

            return new WallFollowPath(positions, directions);
        }

        private static bool HasRepeatedFourCellCycle(IReadOnlyList<SurfaceCell> positions)
        {
            for (var i = 4; i < positions.Count; i++)
            {
                if (positions[i] == positions[i - 4])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasImmediateTwoCellBounce(IReadOnlyList<SurfaceCell> positions)
        {
            for (var i = 3; i < positions.Count; i++)
            {
                if (positions[i] == positions[i - 2] &&
                    positions[i - 1] == positions[i - 3])
                {
                    return true;
                }
            }

            return false;
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
                var pipeline = CreateEnemyPipeline(worldState, profile);
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
                        Array.Empty<TileFeatureRuntimeDefinition>(),
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
                var pipeline = CreateEnemyPipeline(worldState, profile);

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
            var pipeline = CreateEnemyPipeline(worldState, profile);
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
            var baselinePipeline = CreateEnemyPipeline(baselineWorldState, baselineProfile);
            var pilotPipeline = CreateEnemyPipeline(pilotWorldState, pilotProfile);
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
            var pipeline = CreateEnemyPipeline(worldState, profile);
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
                (tick.AttackPhaseResult.DamageResolutions.Any(record =>
                    record.Accepted &&
                    record.SourceId == 40 &&
                    record.TargetId == 10 &&
                    record.SourceKind == AttackSourceKind.Combat) ||
                 worldState.CreateSnapshot().CountPendingCellImpactsForOwner(40) > 0))
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

        private static EnemyRandomWalkPatrolPlan BuildRandomWalkPlan(
            SurfaceCell sourceCell,
            SurfaceCell homeCell,
            PatrolSettings settings,
            IEnumerable<EntityState> blockers,
            int tickIndex,
            EnemyPatrolRuntimeState? patrolState = null,
            IEnumerable<TileFeatureState> tileFeatures = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            return BuildRandomWalkPlan(
                sourceCell,
                homeCell,
                settings,
                blockers,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 2)),
                tickIndex,
                patrolState,
                tileFeatures,
                tileFeatureDefinitions);
        }

        private static EnemyRandomWalkPatrolPlan BuildRandomWalkPlan(
            SurfaceCell sourceCell,
            SurfaceCell homeCell,
            PatrolSettings settings,
            IEnumerable<EntityState> blockers,
            BoardBounds boardBounds,
            int tickIndex,
            EnemyPatrolRuntimeState? patrolState = null,
            IEnumerable<TileFeatureState> tileFeatures = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            var entities = new List<EntityState>
            {
                CreateUnit(entityId: 40, teamId: 2, position: sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Left),
            };
            entities.AddRange(blockers);
            var worldState = tileFeatures == null
                ? CreateWorldState(entities, boardBounds)
                : CreateWorldState(entities, boardBounds, tileFeatures);
            var snapshot = worldState.CreateSnapshot();
            var source = GetEntity(worldState, 40);
            var resolvedPatrolState = patrolState ?? new EnemyPatrolRuntimeState
            {
                sequence = 3,
                homeCell = homeCell,
                lastCommittedDirection = Direction.Right,
            };

            return EnemyRandomWalkPatrolPlanner.BuildPlan(
                snapshot,
                source,
                tickIndex,
                resolvedPatrolState,
                settings,
                tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>());
        }

        private static Vector2Int ResolveDeltaForTest(Direction direction)
        {
            Assert.That(EnemyMovementStrategyShared.TryResolveDelta(direction, out var delta), Is.True);
            return delta;
        }

        private static int GetPlanarDistanceForTest(SurfaceCell sourceCell, SurfaceCell targetCell)
        {
            return Math.Abs(sourceCell.x - targetCell.x) + Math.Abs(sourceCell.y - targetCell.y);
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

        private static EnemyAiRuntimeDefinition CreateWindupProjectileRuntimeDefinition(int windupTicks = 0)
        {
            return new EnemyAiRuntimeDefinition(
                EnemyAiCommonSettings.CreateStandard(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateStandardEnemyDetection(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateAdjacentRange(),
                new EnemyAttackTimingSettings(windupTicks),
                EnemyLocomotionTimingSettings.CreateImmediate(),
                MovementSkillStrategyKind.None,
                EnemyJumpTimingSettings.CreateDefault(),
                ForwardPatrolStrategy.Instance,
                NearestOpponentDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                WindupForwardCellProjectileAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        private static EnemyAiProfile CreateJumpEnemyProfile(
            AttackDecisionStrategyKind attackDecisionStrategyKind = AttackDecisionStrategyKind.WindupForwardCellProjectile)
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

        private static EnemyAiProfile CreatePhaseThroughEnemyProfile()
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.WindupForwardCellProjectile,
                MovementSkillStrategyKind = MovementSkillStrategyKind.PhaseThroughLockedTarget,
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
            float cooldownSeconds = 1f,
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
            EnemyAiProfileTestFactory.SetSerializedField(effect, "cooldownSeconds", cooldownSeconds);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "summon", summon);
            return effect;
        }

        private static EnemyUtilityEffectAuthoring CreateArchetypeSummonUtilityEffect(
            EnemyUnitArchetypeAsset summonedArchetype,
            float initialDelaySeconds = 0f,
            float cooldownSeconds = 1f,
            int spawnCountPerTrigger = 1,
            int maxAliveChildren = 3,
            bool overrideHp = false,
            int hpOverride = 1,
            bool requireNoUnitAtSpawnCell = true,
            bool requireNoSolidAtSpawnCell = true)
        {
            return CreateSummonUtilityEffect(
                initialDelaySeconds,
                cooldownSeconds,
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
            EnemyAiMode initialAiMode,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            var asset = ScriptableObject.CreateInstance<EnemyUnitArchetypeAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(asset, "archetypeId", new EnemyUnitArchetypeId(archetypeId));
            EnemyAiProfileTestFactory.SetSerializedField(asset, "aiProfile", profile);
            EnemyAiProfileTestFactory.SetSerializedField(asset, "spawnDefaults", CreateEnemyUnitSpawnDefaults(hp, initialAiMode, unitMobilityKind));
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

        private static EnemyUnitSpawnDefaults CreateEnemyUnitSpawnDefaults(
            int hp,
            EnemyAiMode initialAiMode,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            object boxed = EnemyUnitSpawnDefaults.CreateDefault();
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "hp", hp);
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "initialAiMode", initialAiMode);
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "unitMobilityKind", unitMobilityKind);
            return (EnemyUnitSpawnDefaults)boxed;
        }

        private static EnemyUtilityEffectAuthoring CreateLockNearbyBoxesUtilityEffect(
            float initialDelaySeconds = 0f,
            float cooldownSeconds = 1f,
            int radius = 1,
            float durationSeconds = 2f,
            bool blocksPush = true,
            bool blocksFlip = true,
            bool includeSourceCell = false,
            BoxLockTargetPattern targetPattern = BoxLockTargetPattern.ManhattanRadius,
            float activationDelaySeconds = 0f)
        {
            var lockNearbyBoxes = new LockNearbyBoxesAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "radius", radius);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "durationSeconds", durationSeconds);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "activationDelaySeconds", activationDelaySeconds);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "blocksPush", blocksPush);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "blocksFlip", blocksFlip);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "includeSourceCell", includeSourceCell);
            EnemyAiProfileTestFactory.SetSerializedField(lockNearbyBoxes, "targetPattern", targetPattern);

            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.LockNearbyBoxes);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "initialDelaySeconds", initialDelaySeconds);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "cooldownSeconds", cooldownSeconds);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "lockNearbyBoxes", lockNearbyBoxes);
            return effect;
        }

        private static float TicksToSeconds(int ticks)
        {
            return ticks / 10f;
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

        private static EnemyActionRuntimeState CreateExecutableMeleeActionState(
            SurfaceCell sourceCell,
            int targetEntityId,
            Direction direction,
            int startTick,
            int executeTick)
        {
            return new EnemyActionRuntimeState
            {
                kind = EnemyActionKind.Melee,
                sequence = 1,
                lockedTargetEntityId = targetEntityId,
                direction = direction,
                startTick = startTick,
                executeTick = executeTick,
                hasLockedCombatAnchor = true,
                lockedCombatAnchor = CreateCombatOriginAnchor(sourceCell, direction),
            };
        }

        private static CombatOriginAnchor CreateCombatOriginAnchor(SurfaceCell sourceCell, Direction facing)
        {
            return new CombatOriginAnchor(
                sourceCell,
                KinematicOffset2.Zero,
                sourceCell.x * KinematicFixed.UnitsPerCell,
                sourceCell.y * KinematicFixed.UnitsPerCell,
                facing);
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
                var projectile = ScriptableObject.CreateInstance<WindupForwardCellProjectileCapabilityAsset>();
                SetSerializedField(projectile, "attackDecisionSettings", new AttackDecisionSettings(attackRange: 1));
                SetSerializedField(projectile, "attackTimingSettings", new EnemyAttackTimingAuthoringSettings(windupSeconds: 0.15f));
                SetSerializedField(
                    projectile,
                    "windupForwardCellProjectileSettings",
                    WindupForwardCellProjectileSettings.CreateDefault());
                capabilities.Add(projectile);
                createdAssets.Add(projectile);
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

        private static TickPipeline CreateEnemyPipeline(WorldState worldState, EnemyAiProfile profile)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
            var kinematicTiming = new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);

            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                playerTiming,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                playerKinematicLocomotionTiming: kinematicTiming);
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
            UnitRole unitRole = UnitRole.None,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
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
                unitMobilityKind = unitMobilityKind,
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
            UnitRole unitRole = UnitRole.None,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
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
                unitMobilityKind = unitMobilityKind,
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

        private static TileFeatureState CreateDestroyTile(int tileId, SurfaceCell cell)
        {
            return new TileFeatureState(
                tileId,
                cell,
                TileFeatureKind.Destroy,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateTileFeatureDefinition(
            int tileId,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: string.Empty);
        }

        private static EntityState CreateBox(int entityId, Vector2Int position)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
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
