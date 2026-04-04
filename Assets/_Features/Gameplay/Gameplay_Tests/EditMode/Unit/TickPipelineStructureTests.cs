using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineStructureTests
    {
        [Test]
        public void RunTick_CompletesMovementAttackCleanup()
        {
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()));

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
            Assert.That(exportedTypes, Does.Not.Contain(typeof(IWorldWriteContext).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(IMovementCommitContext).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(IAttackCommitContext).FullName));
            Assert.That(exportedTypes, Does.Not.Contain(typeof(ICleanupCommitContext).FullName));
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
        public void Committers_DependOnPhaseSpecificWriteCapabilities()
        {
            var movementCommitMethod = typeof(MovementCommitter).GetMethod(
                "Commit",
                BindingFlags.Instance | BindingFlags.Public);
            var attackCommitMethod = typeof(AttackCommitter).GetMethod(
                "Commit",
                BindingFlags.Instance | BindingFlags.Public);
            var cleanupProcessMethod = typeof(CleanupProcessor).GetMethod(
                "Process",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(movementCommitMethod, Is.Not.Null);
            Assert.That(attackCommitMethod, Is.Not.Null);
            Assert.That(cleanupProcessMethod, Is.Not.Null);

            var movementParameterTypes = movementCommitMethod
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
            var attackParameterTypes = attackCommitMethod
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
            var cleanupParameterTypes = cleanupProcessMethod
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.That(movementParameterTypes, Has.Member(typeof(IMovementCommitContext)));
            Assert.That(movementParameterTypes, Has.No.Member(typeof(IWorldWriteContext)));
            Assert.That(attackParameterTypes, Has.Member(typeof(IAttackCommitContext)));
            Assert.That(attackParameterTypes, Has.No.Member(typeof(IWorldWriteContext)));
            Assert.That(cleanupParameterTypes, Has.Member(typeof(ICleanupCommitContext)));
            Assert.That(cleanupParameterTypes, Has.No.Member(typeof(IWorldWriteContext)));
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
                "WorldState mutation must remain behind aggregate world write capabilities.");
        }

        [Test]
        public void WorldState_UsesInternalConcreteWriteContext_And_PrivateMutationHelpers()
        {
            var createWriteContextMethod = typeof(WorldState).GetMethod(
                "CreateWriteContext",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var worldStateMethods = typeof(WorldState)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => method.Name is
                    "ApplyDamage" or
                    "ApplyStateChange" or
                    "MarkDestroy" or
                    "MoveEntityTo" or
                    "RemoveEntity" or
                    "SetFacing" or
                    "SpawnEntity" or
                    "TryGetEntity" or
                    "UpdateStoredEntity")
                .ToArray();
            var leakedMutationMethods = typeof(WorldState)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => method.Name is
                    "ApplyDamage" or
                    "ApplyStateChange" or
                    "MarkDestroy" or
                    "MoveEntityTo" or
                    "RemoveEntity" or
                    "SetFacing" or
                    "SpawnEntity" or
                    "TryGetEntity" or
                    "UpdateStoredEntity")
                .Where(method => method.IsPublic || method.IsAssembly || method.IsFamily || method.IsFamilyOrAssembly)
                .ToArray();

            Assert.That(typeof(WorldStateWriteContext).IsNotPublic, Is.True);
            Assert.That(typeof(IWorldWriteContext).IsAssignableFrom(typeof(WorldStateWriteContext)), Is.True);
            Assert.That(typeof(IMovementCommitContext).IsAssignableFrom(typeof(WorldStateWriteContext)), Is.True);
            Assert.That(typeof(IAttackCommitContext).IsAssignableFrom(typeof(WorldStateWriteContext)), Is.True);
            Assert.That(typeof(ICleanupCommitContext).IsAssignableFrom(typeof(WorldStateWriteContext)), Is.True);
            Assert.That(createWriteContextMethod, Is.Not.Null);
            Assert.That(createWriteContextMethod.IsAssembly, Is.True);
            Assert.That(createWriteContextMethod.ReturnType, Is.EqualTo(typeof(IWorldWriteContext)));
            Assert.That(worldStateMethods, Is.Not.Empty);
            Assert.That(worldStateMethods.All(method => method.IsPrivate), Is.True);
            Assert.That(leakedMutationMethods, Is.Empty);
        }

        [Test]
        public void DelayedEventQueue_IsOwnedByTickPipeline_NotWorldState()
        {
            var tickPipelineFieldTypes = typeof(TickPipeline)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.FieldType)
                .ToArray();
            var worldStateFieldTypes = typeof(WorldState)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.FieldType)
                .ToArray();

            Assert.That(tickPipelineFieldTypes, Has.Member(typeof(DelayedAttackEffectQueue)));
            Assert.That(worldStateFieldTypes, Has.No.Member(typeof(DelayedAttackEffectQueue)));
        }

        [Test]
        public void TickPipeline_DelegatesDynamicEntityMaterializationToProvider()
        {
            var fieldTypes = typeof(TickPipeline)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.FieldType)
                .ToArray();

            Assert.That(fieldTypes, Has.Member(typeof(ISnapshotEntityLogicProvider)));
            Assert.That(
                fieldTypes.Any(fieldType =>
                    fieldType == typeof(ProjectileLogic) ||
                    (fieldType.IsGenericType && fieldType.GetGenericArguments().Contains(typeof(ProjectileLogic)))),
                Is.False);
        }

        [Test]
        public void TickPipeline_CanBeExtendedWithInjectedEntityLogicProvider()
        {
            var constructor = typeof(TickPipeline).GetConstructor(
                new[]
                {
                    typeof(WorldState),
                    typeof(IEnumerable<IEntityLogic>),
                    typeof(ISnapshotEntityLogicProvider),
                });

            Assert.That(constructor, Is.Not.Null);
        }

        [Test]
        public void TickPipeline_DoesNotExposeDefaultCompositionConstructors()
        {
            var constructors = typeof(TickPipeline)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray())
                .ToArray();

            Assert.That(constructors.Length, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[]
                {
                    typeof(WorldState),
                    typeof(IEnumerable<IEntityLogic>),
                    typeof(ISnapshotEntityLogicProvider),
                },
                constructors[0]);
        }

        [Test]
        public void GameplayCompositionRoot_ExposesDefaultPipelineAssemblyApi()
        {
            var defaultBootstrapperFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateDefaultBootstrapper),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            var defaultBootstrapperWithProfileFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateDefaultBootstrapper),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(EnemyAiProfile) },
                modifiers: null);
            var worldOnlyFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateTickPipeline),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState) },
                modifiers: null);
            var worldAndLogicFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateTickPipeline),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(IEnumerable<IEntityLogic>) },
                modifiers: null);
            var runnerFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateTickRunner),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(TickInputBuffer) },
                modifiers: null);
            var runnerWithLogicFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateTickRunner),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(IEnumerable<IEntityLogic>), typeof(TickInputBuffer), typeof(int) },
                modifiers: null);

            Assert.That(defaultBootstrapperFactory, Is.Not.Null);
            Assert.That(defaultBootstrapperFactory.ReturnType, Is.EqualTo(typeof(GameplayBootstrapper)));
            Assert.That(defaultBootstrapperWithProfileFactory, Is.Not.Null);
            Assert.That(defaultBootstrapperWithProfileFactory.ReturnType, Is.EqualTo(typeof(GameplayBootstrapper)));
            Assert.That(worldOnlyFactory, Is.Not.Null);
            Assert.That(worldOnlyFactory.ReturnType, Is.EqualTo(typeof(TickPipeline)));
            Assert.That(worldAndLogicFactory, Is.Not.Null);
            Assert.That(worldAndLogicFactory.ReturnType, Is.EqualTo(typeof(TickPipeline)));
            Assert.That(runnerFactory, Is.Not.Null);
            Assert.That(runnerFactory.ReturnType, Is.EqualTo(typeof(TickRunner)));
            Assert.That(runnerWithLogicFactory, Is.Not.Null);
            Assert.That(runnerWithLogicFactory.ReturnType, Is.EqualTo(typeof(TickRunner)));
        }

        [Test]
        public void GameplayBootstrapper_ExposesRunnerCreationApi()
        {
            var runnerFactory = typeof(GameplayBootstrapper).GetMethod(
                nameof(GameplayBootstrapper.CreateTickRunner),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(TickInputBuffer) },
                modifiers: null);
            var runnerWithLogicFactory = typeof(GameplayBootstrapper).GetMethod(
                nameof(GameplayBootstrapper.CreateTickRunner),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(IEnumerable<IEntityLogic>), typeof(TickInputBuffer), typeof(int) },
                modifiers: null);

            Assert.That(runnerFactory, Is.Not.Null);
            Assert.That(runnerFactory.ReturnType, Is.EqualTo(typeof(TickRunner)));
            Assert.That(runnerWithLogicFactory, Is.Not.Null);
            Assert.That(runnerWithLogicFactory.ReturnType, Is.EqualTo(typeof(TickRunner)));
        }

        [Test]
        public void IEntityLogic_IsMarkerInterface()
        {
            var methods = typeof(IEntityLogic)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .ToArray();

            Assert.That(methods, Is.Empty);
        }

        [Test]
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
