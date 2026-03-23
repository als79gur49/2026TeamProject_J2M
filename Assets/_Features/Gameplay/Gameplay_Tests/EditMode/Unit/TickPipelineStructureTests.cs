using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineStructureTests
    {
        [Test]
        public void RunTick_CompletesMovementAttackCleanup()
        {
            var pipeline = new TickPipeline(new WorldState());

            var result = pipeline.RunTick(new TickInput(7));

            Assert.That(result.TickIndex, Is.EqualTo(7));
            Assert.That(result.CompletedAllPhases, Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    TickPhase.Movement,
                    TickPhase.Attack,
                    TickPhase.Cleanup,
                },
                result.CompletedPhases);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Movement:Enter",
                    "Movement:Exit",
                    "Attack:Enter",
                    "Attack:Exit",
                    "Cleanup:Enter",
                    "Cleanup:Exit",
                },
                result.PhaseTrace);
        }

        [Test]
        public void TickResult_DoesNotExposePhaseDiagnosticsInPublicApi()
        {
            var publicPropertyNames = typeof(TickResult)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(publicPropertyNames, Does.Not.Contain("MovementPhaseResult"));
            Assert.That(publicPropertyNames, Does.Not.Contain("AttackPhaseResult"));
            Assert.That(publicPropertyNames, Does.Not.Contain("CleanupPhaseResult"));
        }

        [Test]
        public void IdAllocator_IsNotExposedAsPublicRuntimeApi()
        {
            var exportedTypes = typeof(TickPipeline).Assembly
                .GetExportedTypes()
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(exportedTypes, Does.Not.Contain(typeof(IdAllocator).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(EntityIdAllocator).FullName));
        }

        [Test]
        public void AttackCommitter_DoesNotDependOnCentralAllocators()
        {
            var commitMethod = typeof(AttackCommitter).GetMethod(
                "Commit",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(commitMethod, Is.Not.Null);

            var parameterTypes = commitMethod
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.That(parameterTypes.Contains(typeof(IdAllocator)), Is.False);
            Assert.That(parameterTypes.Contains(typeof(EntityIdAllocator)), Is.False);
        }

        [Test]
        public void WorldState_DoesNotExposeDirectPublicMutationApi()
        {
            var publicInstanceMembers = typeof(WorldState)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(member =>
                    member.MemberType == MemberTypes.Method ||
                    member.MemberType == MemberTypes.Property ||
                    member.MemberType == MemberTypes.Field)
                .Select(member => member.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                publicInstanceMembers,
                Is.Empty,
                "WorldState mutation must remain behind IWorldWriteContext capabilities.");
        }

        [Test]
        public void WorldState_HidesConcreteWriteContext_And_PrivateMutationHelpers()
        {
            var assembly = typeof(WorldState).Assembly;
            var worldStateWriteContextType = assembly.GetType("Game.Feature.Gameplay.BoardState.WorldStateWriteContext");
            var worldStateMethods = typeof(WorldState)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => method.Name is
                    "AddNewEntity" or
                    "ClearOccupancy" or
                    "RemoveEntity" or
                    "SetOccupancy" or
                    "TryGetEntity" or
                    "UpdateEntity")
                .ToArray();

            Assert.That(worldStateWriteContextType, Is.Null);
            Assert.That(worldStateMethods, Is.Not.Empty);
            Assert.That(worldStateMethods.All(method => method.IsPrivate), Is.True);
        }

        [Test]
        public void IEntityLogic_ExposesPhaseSpecificRawIntentCollectionContract()
        {
            var methods = typeof(IEntityLogic)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(method => method.Name)
                .ToArray();

            Assert.That(methods.Select(method => method.Name), Is.EqualTo(new[]
            {
                "CollectAttackIntents",
                "CollectMovementIntents",
            }));

            var attackParameters = methods[0].GetParameters();
            Assert.That(attackParameters.Length, Is.EqualTo(2));
            Assert.That(attackParameters[0].ParameterType, Is.EqualTo(typeof(WorldSnapshot)));
            Assert.That(attackParameters[1].ParameterType, Is.EqualTo(typeof(List<RawAttackIntent>)));

            var movementParameters = methods[1].GetParameters();
            Assert.That(movementParameters.Length, Is.EqualTo(3));
            Assert.That(movementParameters[0].ParameterType, Is.EqualTo(typeof(WorldSnapshot)));
            Assert.That(movementParameters[1].ParameterType, Is.EqualTo(typeof(TickInput).MakeByRefType()));
            Assert.That(movementParameters[2].ParameterType, Is.EqualTo(typeof(List<RawMovementIntent>)));
        }
    }
}
