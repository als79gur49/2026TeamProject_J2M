using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests
{
    public sealed class EnemySummonExecutorContractTests
    {
        [Test]
        [Category("Extended")]
        public void NullRuntimeSuppressionQueriesReturnFalseBeforeSnapshotAccess()
        {
            Assert.That(EnemySummonExecutor.ShouldSuppressActive(
                null, default, 40, null, 1), Is.False);
            Assert.That(EnemySummonExecutor.ShouldSuppressImminent(
                null, default, 40, null, 1), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void RepeatedActiveQueryReadsSameStateWithoutChangingWorld()
        {
            var world = CreateWorld();
            var initial = new EnemySummonBehaviorRuntimeState
            {
                phase = EnemySummonBehaviorPhase.Windup,
                windupStartTick = 1,
                windupEndTick = 3,
                activationSequence = 7,
                movementSuppressionUntilTickInclusive = 3,
            };
            world.CreateWriteContext().SetEnemySummonBehaviorState(40, initial);
            var snapshot = world.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(40, out var source), Is.True);
            var runtime = CreateRuntime();

            Assert.That(EnemySummonExecutor.ShouldSuppressActive(snapshot, source, 40, runtime, 2), Is.True);
            Assert.That(EnemySummonExecutor.ShouldSuppressActive(snapshot, source, 40, runtime, 2), Is.True);
            Assert.That(world.CreateSnapshot().TryGetEnemySummonBehaviorState(40, out var after), Is.True);
            Assert.That(after.phase, Is.EqualTo(initial.phase));
            Assert.That(after.windupEndTick, Is.EqualTo(initial.windupEndTick));
            Assert.That(after.activationSequence, Is.EqualTo(initial.activationSequence));
            Assert.That(after.movementSuppressionUntilTickInclusive,
                Is.EqualTo(initial.movementSuppressionUntilTickInclusive));
        }

        [Test]
        [Category("Extended")]
        public void CancelUsesExistingStateWithoutAValidSourceAndKeepsInactiveNoOp()
        {
            var world = CreateWorld();
            var runtime = CreateRuntime();
            var updates = new List<string>();
            var windup = new EnemySummonBehaviorRuntimeState
            {
                phase = EnemySummonBehaviorPhase.Windup,
                windupStartTick = 1,
                windupEndTick = 4,
                activationSequence = 3,
                movementSuppressionUntilTickInclusive = 4,
            };
            world.CreateWriteContext().SetEnemySummonBehaviorState(40, windup);
            EnemySummonExecutor.Cancel(windup, 40, runtime, world.CreateWriteContext(), updates);
            Assert.That(world.CreateSnapshot().TryGetEnemySummonBehaviorState(40, out var canceled), Is.True);
            Assert.That(canceled.phase, Is.EqualTo(EnemySummonBehaviorPhase.None));
            Assert.That(canceled.cooldownTicksRemaining, Is.EqualTo(5));
            Assert.That(canceled.activationSequence, Is.EqualTo(3));
            Assert.That(canceled.movementSuppressionUntilTickInclusive, Is.Zero);
            Assert.That(updates, Has.Count.EqualTo(1));
            Assert.That(updates[0], Does.StartWith("EnemySummonBehaviorWindupCanceled|E=40|Effect=0|Sequence=3|Cooldown=5"));

            updates.Clear();
            EnemySummonExecutor.Cancel(canceled, 40, runtime, world.CreateWriteContext(), updates);
            Assert.That(updates, Is.Empty);
            Assert.That(world.CreateSnapshot().TryGetEnemySummonBehaviorState(40, out var afterNoOp), Is.True);
            Assert.That(afterNoOp.cooldownTicksRemaining, Is.EqualTo(5));
        }

        [Test]
        [Category("Extended")]
        public void ExecutorOwnsSummonPolicyWhileCoordinatorOnlyDelegates()
        {
            var type = typeof(EnemySummonExecutor);
            Assert.That(type.IsAbstract && type.IsSealed, Is.True);
            Assert.That(type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .All(field => field.IsLiteral), Is.True, "Executor must not own mutable static state.");
            foreach (var name in new[] { "ShouldSuppressActive", "ShouldSuppressImminent" })
            {
                var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
                Assert.That(method, Is.Not.Null, name);
                Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
                Assert.That(method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(EnemyLogic) ||
                    typeof(IPreMovementStateCommitContext).IsAssignableFrom(parameter.ParameterType) ||
                    parameter.ParameterType == typeof(List<string>) ||
                    parameter.ParameterType == typeof(IEnemyUtilityTriggerSink)), Is.False, name);
            }

            var logic = ReadSource("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs");
            var executor = ReadSource("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemySummonExecutor.cs");
            Assert.That(logic, Does.Contain("EnemySummonExecutor.Commit("));
            Assert.That(logic, Does.Contain("EnemySummonExecutor.Cancel("));
            Assert.That(logic, Does.Contain("EnemySummonExecutor.ShouldSuppressActive("));
            Assert.That(logic, Does.Contain("EnemySummonExecutor.ShouldSuppressImminent("));
            Assert.That(logic, Does.Not.Contain("EnemySummonBehaviorPhase."));
            Assert.That(logic, Does.Not.Contain("SummonBehaviorCompatibilitySourceEffectIndex"));
            Assert.That(logic, Does.Not.Contain("EnemySummonBehaviorWindupStarted|"));
            Assert.That(executor, Does.Contain("EnemySummonBehaviorWindupStarted|"));
            Assert.That(executor, Does.Not.Contain("SpawnEntity("));
            Assert.That(executor, Does.Not.Contain("EnemyLogic logic"));
        }

        private static WorldState CreateWorld()
        {
            return GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                new EntityState
                {
                    entityId = 40,
                    type = EntityType.Unit,
                    unitRole = UnitRole.Enemy,
                    teamId = 2,
                    hp = 3,
                    maxHp = 3,
                    aiMode = EnemyAiMode.Patrol,
                    position = new SurfaceCell(FaceId.Floor, 0, 0),
                    boardPresence = EntityBoardPresence.Occupying,
                    state = EntityPhaseState.Idle,
                    facing = Direction.Right,
                },
            });
        }

        private static EnemySummonBehaviorRuntime CreateRuntime()
        {
            return new EnemySummonBehaviorRuntime(0, 5,
                new EnemySummonCompiledConfig(1, SummonCandidatePattern.OrthogonalAdjacent4,
                    true, true, 3, new EnemyUnitArchetypeId("BasicMinion"),
                    windupTicks: 2, suppressMovementDuringWindup: true));
        }

        private static string ReadSource(string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.That(File.Exists(path), Is.True, path);
            return File.ReadAllText(path);
        }
    }
}
