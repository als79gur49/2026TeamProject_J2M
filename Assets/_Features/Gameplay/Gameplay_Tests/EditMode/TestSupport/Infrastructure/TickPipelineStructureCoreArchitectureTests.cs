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
                typeof(PlayerFree2DLocomotionSettings),
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
                    typeof(PlayerFree2DLocomotionSettings),
                },
                parameters.Take(6).Select(parameter => parameter.ParameterType).ToArray());
            Assert.That(parameters[5].IsOptional, Is.False);
            Assert.That(parameters.Skip(6).All(parameter => parameter.IsOptional), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void GameplayCompositionRoot_DoesNotExposeDefaultPipelineAssemblyApi()
        {
            var defaultBootstrapperFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateDefaultBootstrapper",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            var defaultBootstrapperWithProfileFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateDefaultBootstrapper",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(EnemyAiProfile) },
                modifiers: null);
            var worldOnlyFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateTickPipeline",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState) },
                modifiers: null);
            var worldAndLogicFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateTickPipeline",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(IEnumerable<IEntityLogic>) },
                modifiers: null);
            var runnerFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateTickRunner",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(TickInputBuffer) },
                modifiers: null);
            var runnerWithLogicFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateTickRunner",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(IEnumerable<IEntityLogic>), typeof(TickInputBuffer), typeof(int) },
                modifiers: null);

            Assert.That(defaultBootstrapperFactory, Is.Null);
            Assert.That(defaultBootstrapperWithProfileFactory, Is.Null);
            Assert.That(worldOnlyFactory, Is.Null);
            Assert.That(worldAndLogicFactory, Is.Null);
            Assert.That(runnerFactory, Is.Null);
            Assert.That(runnerWithLogicFactory, Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void GameplayBootstrapper_RequiresPlayerFree2DSettingsForRunnerCreationApi()
        {
            var worldOnlyRunnerFactory = typeof(GameplayBootstrapper).GetMethod(
                nameof(GameplayBootstrapper.CreateTickRunner),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(TickInputBuffer) },
                modifiers: null);
            var runnerWithLogicOnlyFactory = typeof(GameplayBootstrapper).GetMethod(
                nameof(GameplayBootstrapper.CreateTickRunner),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(WorldState), typeof(IEnumerable<IEntityLogic>), typeof(TickInputBuffer), typeof(int) },
                modifiers: null);
            var explicitRunnerFactory = FindMethodWithLeadingParameterTypes(
                typeof(GameplayBootstrapper),
                nameof(GameplayBootstrapper.CreateTickRunner),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(WorldState),
                typeof(IEnumerable<IEntityLogic>),
                typeof(TickInputBuffer),
                typeof(GameplayTimingProfile),
                typeof(PlayerControlTimingAuthoritativeSnapshot),
                typeof(PlayerFree2DLocomotionSettings));

            Assert.That(worldOnlyRunnerFactory, Is.Null);
            Assert.That(runnerWithLogicOnlyFactory, Is.Null);
            Assert.That(explicitRunnerFactory, Is.Not.Null);
            Assert.That(explicitRunnerFactory.ReturnType, Is.EqualTo(typeof(TickRunner)));
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
        public void GameplayBootstrapper_ExposesExplicitPlayerFree2DSettingsOverloads()
        {
            var bootstrapperPipelineFactory = FindMethodWithLeadingParameterTypes(
                typeof(GameplayBootstrapper),
                nameof(GameplayBootstrapper.CreateTickPipeline),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(WorldState),
                typeof(IEnumerable<IEntityLogic>),
                typeof(GameplayTimingProfile),
                typeof(PlayerControlTimingAuthoritativeSnapshot),
                typeof(PlayerFree2DLocomotionSettings));
            var bootstrapperRunnerFactory = FindMethodWithLeadingParameterTypes(
                typeof(GameplayBootstrapper),
                nameof(GameplayBootstrapper.CreateTickRunner),
                BindingFlags.Instance | BindingFlags.Public,
                typeof(WorldState),
                typeof(IEnumerable<IEntityLogic>),
                typeof(TickInputBuffer),
                typeof(GameplayTimingProfile),
                typeof(PlayerControlTimingAuthoritativeSnapshot),
                typeof(PlayerFree2DLocomotionSettings));

            Assert.That(bootstrapperPipelineFactory, Is.Not.Null);
            Assert.That(bootstrapperRunnerFactory, Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void GameplayCompositionRoot_AndBootstrapper_DoNotExposeGeneralTimingOnlyOverloads()
        {
            var compositionRootPipelineFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateTickPipeline",
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
                "CreateTickRunner",
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
