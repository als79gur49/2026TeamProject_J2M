using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    [Category("Full")]
    public sealed class EnemyGlideExecutorStructureTests
    {
        [Test]
        public void ExecutorIsStatelessAndReceivesCurrentDependenciesPerCommit()
        {
            var type = typeof(EnemyGlideExecutor);
            Assert.That(type.IsAbstract && type.IsSealed, Is.True);
            Assert.That(type.GetFields(BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic), Is.Empty);
            var commit = type.GetMethod("Commit", BindingFlags.Public | BindingFlags.Static);
            Assert.That(commit, Is.Not.Null);
            var parameters = commit.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Assert.That(parameters, Does.Contain(typeof(IReadOnlyList<TileFeatureRuntimeDefinition>)));
            Assert.That(parameters, Does.Contain(typeof(IDetectionStrategy)));
            Assert.That(parameters, Does.Contain(typeof(IChaseStrategy)));
            Assert.That(parameters, Does.Contain(typeof(IPreMovementStateCommitContext)));
            Assert.That(parameters.Any(parameter => parameter == typeof(EnemyLogic)), Is.False);

            var query = type.GetMethod("ShouldSuppressMovement", BindingFlags.Public | BindingFlags.Static);
            Assert.That(query, Is.Not.Null);
            Assert.That(query.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(WorldSnapshot), typeof(int), typeof(EnemyGlideBehaviorRuntime) }));
        }

        [Test]
        public void CoordinatorOnlyDelegatesExtractedPolicyAndKeepsExistingConsumers()
        {
            var logic = ReadSource("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs");
            var executor = ReadSource("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyGlideExecutor.cs");
            Assert.That(logic, Does.Contain("EnemyGlideExecutor.Commit("));
            Assert.That(logic, Does.Contain("EnemyGlideExecutor.ShouldSuppressMovement("));
            foreach (var moved in new[] { "CommitGlideState(", "TryAdvanceGlideLifecycle(",
                         "TryResolveGlideStartLockedStep(", "TryResolveGlideActiveStartTarget(",
                         "AppendGlideUpdate(", "ShouldSuppressMovementForGlide(" })
            {
                Assert.That(logic, Does.Not.Contain(moved), moved);
                if (moved != "CommitGlideState(" && moved != "ShouldSuppressMovementForGlide(")
                    Assert.That(executor, Does.Contain(moved), moved);
            }
            Assert.That(logic, Does.Contain("TryResolveUnsettledGlideKinematicTerminal("));
            Assert.That(logic, Does.Contain("ResolveBaselineGroundLocomotion("));
            Assert.That(logic, Does.Contain("ResolveAfterAttack("));
            Assert.That(executor, Does.Not.Contain("EnemyLogic logic"));
            Assert.That(executor, Does.Not.Contain("WorldState"));
            Assert.That(executor, Does.Not.Contain("TickPipeline"));
            Assert.That(executor, Does.Not.Contain("CameraShake"));
        }

        private static string ReadSource(string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.That(File.Exists(path), Is.True, path);
            return File.ReadAllText(path);
        }
    }
}
