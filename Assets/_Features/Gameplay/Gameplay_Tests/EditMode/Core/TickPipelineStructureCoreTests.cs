using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class TickPipelineStructureCoreTests
    {
        [Test]
        [Category("Core")]
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
        [Category("Core")]
        public void IdAllocator_IsNotExposedAsPublicRuntimeApi()
        {
            var exportedTypes = typeof(TickPipeline).Assembly
                .GetExportedTypes()
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(exportedTypes, Does.Not.Contain(typeof(IdAllocator).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(EntityIdAllocator).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(IWorldWriteContext).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(IMovementCommitContext).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(IAttackCommitContext).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(ICleanupCommitContext).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(IRespawnCommitContext).FullName));
        }

        [Test]
        [Category("Core")]
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
                "WorldState mutation must remain behind aggregate world write capabilities.");
        }

        [Test]
        [Category("Core")]
        public void IEntityLogic_IsMarkerInterface()
        {
            var methods = typeof(IEntityLogic)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .ToArray();

            Assert.That(methods, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PhaseSpecificEntityLogicInterfaces_ExposeRawIntentCollectionContracts()
        {
            var attackMethods = typeof(IAttackEntityLogic)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(method => method.Name)
                .ToArray();
            var movementMethods = typeof(IMovementEntityLogic)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(method => method.Name)
                .ToArray();

            Assert.That(attackMethods.Select(method => method.Name), Is.EqualTo(new[]
            {
                "CollectAttackIntents",
            }));
            Assert.That(movementMethods.Select(method => method.Name), Is.EqualTo(new[]
            {
                "CollectMovementIntents",
            }));

            var attackParameters = attackMethods[0].GetParameters();
            Assert.That(attackParameters.Length, Is.EqualTo(3));
            Assert.That(attackParameters[0].ParameterType, Is.EqualTo(typeof(WorldSnapshot)));
            Assert.That(attackParameters[1].ParameterType, Is.EqualTo(typeof(TickInput).MakeByRefType()));
            Assert.That(attackParameters[2].ParameterType, Is.EqualTo(typeof(List<RawAttackIntent>)));

            var movementParameters = movementMethods[0].GetParameters();
            Assert.That(movementParameters.Length, Is.EqualTo(3));
            Assert.That(movementParameters[0].ParameterType, Is.EqualTo(typeof(WorldSnapshot)));
            Assert.That(movementParameters[1].ParameterType, Is.EqualTo(typeof(TickInput).MakeByRefType()));
            Assert.That(movementParameters[2].ParameterType, Is.EqualTo(typeof(List<RawMovementIntent>)));
        }
    }
}
