using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class TickPipelineStructureCoreTests
    {
        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
            Assert.That(typeof(IRespawnCommitContext).IsAssignableFrom(typeof(WorldStateWriteContext)), Is.True);
            Assert.That(createWriteContextMethod, Is.Not.Null);
            Assert.That(createWriteContextMethod.IsAssembly, Is.True);
            Assert.That(createWriteContextMethod.ReturnType, Is.EqualTo(typeof(IWorldWriteContext)));
            Assert.That(worldStateMethods, Is.Not.Empty);
            Assert.That(worldStateMethods.All(method => method.IsPrivate), Is.True);
            Assert.That(leakedMutationMethods, Is.Empty);
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
        public void TickPipeline_DelegatesDynamicEntityMaterializationToProvider()
        {
            var fieldTypes = typeof(TickPipeline)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.FieldType)
                .ToArray();

            Assert.That(fieldTypes, Has.Member(typeof(ISnapshotEntityLogicProvider)));
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_CanBeExtendedWithInjectedEntityLogicProvider()
        {
            var requiredParameters = new[]
            {
                typeof(WorldState),
                typeof(IEnumerable<IEntityLogic>),
                typeof(ISnapshotEntityLogicProvider),
                typeof(GameplayTimingProfile),
                typeof(PlayerControlTimingAuthoritativeSnapshot),
            };
            var constructor = typeof(TickPipeline)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SingleOrDefault(candidate =>
                {
                    var parameters = candidate.GetParameters();
                    return parameters.Length >= requiredParameters.Length &&
                           parameters.Take(requiredParameters.Length).Select(parameter => parameter.ParameterType)
                               .SequenceEqual(requiredParameters) &&
                           parameters.Skip(requiredParameters.Length).All(parameter => parameter.IsOptional);
                });

            Assert.That(constructor, Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_DoesNotExposeDefaultCompositionConstructors()
        {
            var constructors = typeof(TickPipeline)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.That(constructors.Length, Is.EqualTo(1));
            var parameters = constructors[0].GetParameters();
            CollectionAssert.AreEqual(
                new[]
                {
                    typeof(WorldState),
                    typeof(IEnumerable<IEntityLogic>),
                    typeof(ISnapshotEntityLogicProvider),
                    typeof(GameplayTimingProfile),
                    typeof(PlayerControlTimingAuthoritativeSnapshot),
                },
                parameters.Take(5).Select(parameter => parameter.ParameterType).ToArray());
            Assert.That(parameters.Skip(5).All(parameter => parameter.IsOptional), Is.True);
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
        public void MovementCommitter_ConsumesCanonicalPlayerControlTimingSnapshot()
        {
            var constructors = typeof(MovementCommitter)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(
                constructors.Any(constructor =>
                    HasParameterTypes(
                        constructor,
                        typeof(PlayerControlTimingAuthoritativeSnapshot),
                        typeof(GameplayTimingProfile))),
                Is.True);
            Assert.That(
                constructors.Any(constructor =>
                    HasParameterTypes(
                        constructor,
                        typeof(GameplayTimingProfile))),
                Is.False);
            Assert.That(
                constructors.Any(constructor => HasParameterTypes(constructor)),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void GameplayCompositionRoot_AndBootstrapper_ExposeExplicitGeneralAndPlayerTimingOverloads()
        {
            var compositionRootPipelineFactory = FindMethodWithLeadingParameterTypes(
                typeof(GameplayCompositionRoot),
                nameof(GameplayCompositionRoot.CreateTickPipeline),
                BindingFlags.Static | BindingFlags.Public,
                typeof(WorldState),
                typeof(IEnumerable<IEntityLogic>),
                typeof(GameplayTimingProfile),
                typeof(PlayerControlTimingAuthoritativeSnapshot));
            var compositionRootRunnerFactory = FindMethodWithLeadingParameterTypes(
                typeof(GameplayCompositionRoot),
                nameof(GameplayCompositionRoot.CreateTickRunner),
                BindingFlags.Static | BindingFlags.Public,
                typeof(WorldState),
                typeof(IEnumerable<IEntityLogic>),
                typeof(TickInputBuffer),
                typeof(GameplayTimingProfile),
                typeof(PlayerControlTimingAuthoritativeSnapshot));
            var bootstrapperPipelineFactory = FindMethodWithLeadingParameterTypes(
                typeof(GameplayBootstrapper),
                nameof(GameplayBootstrapper.CreateTickPipeline),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(WorldState),
                typeof(IEnumerable<IEntityLogic>),
                typeof(GameplayTimingProfile),
                typeof(PlayerControlTimingAuthoritativeSnapshot));
            var bootstrapperRunnerFactory = FindMethodWithLeadingParameterTypes(
                typeof(GameplayBootstrapper),
                nameof(GameplayBootstrapper.CreateTickRunner),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(WorldState),
                typeof(IEnumerable<IEntityLogic>),
                typeof(TickInputBuffer),
                typeof(GameplayTimingProfile),
                typeof(PlayerControlTimingAuthoritativeSnapshot));

            Assert.That(compositionRootPipelineFactory, Is.Not.Null);
            Assert.That(compositionRootRunnerFactory, Is.Not.Null);
            Assert.That(bootstrapperPipelineFactory, Is.Not.Null);
            Assert.That(bootstrapperRunnerFactory, Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void GameplayCompositionRoot_AndBootstrapper_DoNotExposeGeneralTimingOnlyOverloads()
        {
            var compositionRootPipelineFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateTickPipeline),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[]
                {
                    typeof(WorldState),
                    typeof(IEnumerable<IEntityLogic>),
                    typeof(GameplayTimingProfile),
                },
                modifiers: null);
            var compositionRootRunnerFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateTickRunner),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[]
                {
                    typeof(WorldState),
                    typeof(IEnumerable<IEntityLogic>),
                    typeof(TickInputBuffer),
                    typeof(GameplayTimingProfile),
                    typeof(int),
                },
                modifiers: null);
            var bootstrapperPipelineFactory = typeof(GameplayBootstrapper).GetMethod(
                nameof(GameplayBootstrapper.CreateTickPipeline),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[]
                {
                    typeof(WorldState),
                    typeof(IEnumerable<IEntityLogic>),
                    typeof(GameplayTimingProfile),
                },
                modifiers: null);
            var bootstrapperRunnerFactory = typeof(GameplayBootstrapper).GetMethod(
                nameof(GameplayBootstrapper.CreateTickRunner),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[]
                {
                    typeof(WorldState),
                    typeof(IEnumerable<IEntityLogic>),
                    typeof(TickInputBuffer),
                    typeof(GameplayTimingProfile),
                    typeof(int),
                },
                modifiers: null);

            Assert.That(compositionRootPipelineFactory, Is.Null);
            Assert.That(compositionRootRunnerFactory, Is.Null);
            Assert.That(bootstrapperPipelineFactory, Is.Null);
            Assert.That(bootstrapperRunnerFactory, Is.Null);
        }

        private static MethodInfo FindMethodWithLeadingParameterTypes(
            Type type,
            string methodName,
            BindingFlags bindingFlags,
            params Type[] parameterTypes)
        {
            return type
                .GetMethods(bindingFlags)
                .FirstOrDefault(method =>
                    method.Name == methodName &&
                    HasLeadingParameterTypes(method, parameterTypes));
        }

        private static bool HasParameterTypes(
            MethodBase methodBase,
            params Type[] parameterTypes)
        {
            return methodBase
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .SequenceEqual(parameterTypes);
        }

        private static bool HasLeadingParameterTypes(
            MethodBase methodBase,
            params Type[] parameterTypes)
        {
            var actualParameterTypes = methodBase
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            return actualParameterTypes.Length >= parameterTypes.Length &&
                   actualParameterTypes
                       .Take(parameterTypes.Length)
                       .SequenceEqual(parameterTypes);
        }
    }
}
