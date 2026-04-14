using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UiArchitectureTests
    {
        [Test]
        public void OnlyCompositionUiAssemblyReferencesGameplayHostAssembly()
        {
            var hostAssemblyName = typeof(GameplaySceneHost).Assembly.GetName().Name;
            var runtimeUiAssemblies = GetRuntimeUiAssemblies();

            foreach (var assembly in runtimeUiAssemblies.Where(assembly => assembly != typeof(GameplayUiFlowInstaller).Assembly))
            {
                var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.That(references, Does.Not.Contain(hostAssemblyName), assembly.GetName().Name);
            }

            var compositionReferences = typeof(GameplayUiFlowInstaller).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(compositionReferences, Does.Contain(hostAssemblyName));
        }

        [Test]
        public void OnlyApplicationAndCompositionUiAssembliesReferenceGameplayUiAccessAssembly()
        {
            var uiAccessAssemblyName = typeof(IGameplayQueryFacade).Assembly.GetName().Name;
            var applicationAssembly = typeof(GameplayHudPresenter).Assembly;
            var compositionAssembly = typeof(GameplayUiFlowInstaller).Assembly;

            foreach (var assembly in GetRuntimeUiAssemblies().Where(assembly => assembly != applicationAssembly && assembly != compositionAssembly))
            {
                var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.That(references, Does.Not.Contain(uiAccessAssemblyName), assembly.GetName().Name);
            }

            var applicationReferences = applicationAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(applicationReferences, Does.Contain(uiAccessAssemblyName));

            var compositionReferences = compositionAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(compositionReferences, Does.Contain(uiAccessAssemblyName));
        }

        [Test]
        public void ApplicationUiAssembly_DoesNotReferenceGameplayAssembly()
        {
            var gameplayAssemblyName = typeof(WorldState).Assembly.GetName().Name;
            var references = typeof(GameplayHudPresenter).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(gameplayAssemblyName));
        }

        [Test]
        public void ApplicationUiAssembly_OnlyGameplayUiPresentationSourceDependsOnGameplayPresentationFeed()
        {
            var applicationAssembly = typeof(GameplayHudPresenter).Assembly;
            var feedType = typeof(IGameplayPresentationFeed);

            var dependentTypes = applicationAssembly
                .GetTypes()
                .Where(type => !type.IsNested && TypeDependsOn(type, feedType))
                .Select(type => type.FullName)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                dependentTypes,
                Is.EqualTo(new[]
                {
                    typeof(GameplayUiPresentationSource).FullName,
                }));
        }

        [Test]
        public void PresenterTypes_DoNotDependOnRawGameplayPresentationContracts()
        {
            var forbiddenTypes = new[]
            {
                typeof(IGameplayPresentationFeed),
                typeof(GameplayPresentationFrame),
                typeof(GameplayPresentationState),
            };
            var presenterTypes = new[]
            {
                typeof(GameplayHudPresenter),
                typeof(ObjectiveStatusPresenter),
            };

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(presenterType, forbiddenType),
                        Is.False,
                        $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void FeatureUiAssemblies_DoNotDefineRuntimeOnGuiMethods()
        {
            var offendingMethods = GetRuntimeUiAssemblies()
                .SelectMany(assembly => assembly.GetExportedTypes())
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(method => method.Name == "OnGUI")
                    .Select(method => $"{type.FullName}.{method.Name}"))
                .ToArray();

            Assert.That(offendingMethods, Is.Empty);
        }

        [Test]
        public void HudScreenAndPopupAssemblies_DoNotExposeForbiddenGameplayTypes()
        {
            var forbiddenTypes = new HashSet<Type>
            {
                typeof(WorldState),
                typeof(TickResult),
                typeof(TickRunner),
                typeof(GameplayInputHost),
                typeof(GameplayTickViewPresenter),
            };

            var assemblies = new[]
            {
                typeof(GameplayHudView).Assembly,
                typeof(GameplayScreenView).Assembly,
                typeof(PausePopupView).Assembly,
            };

            var leakedTypes = assemblies
                .Distinct()
                .SelectMany(assembly => assembly.GetExportedTypes())
                .SelectMany(GetPublicSurfaceTypes)
                .Select(NormalizeType)
                .Where(type => type != null &&
                               (forbiddenTypes.Contains(type) ||
                                (type.Namespace != null && type.Namespace.StartsWith("Game.Feature.Gameplay", StringComparison.Ordinal))))
                .Distinct()
                .ToArray();

            Assert.That(leakedTypes, Is.Empty);
        }

        [Test]
        public void GameplayQueryFacade_Surface_RemainsBoundedToStageOneReaders()
        {
            var propertyNames = typeof(IGameplayQueryFacade)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[] { "Objectives", "PlayerHud", "Session" }));
        }

        private static Assembly[] GetRuntimeUiAssemblies()
        {
            return new[]
            {
                typeof(GameplayHudPresenter).Assembly,
                typeof(HUDController).Assembly,
                typeof(GameplayHudView).Assembly,
                typeof(GameplayScreenView).Assembly,
                typeof(PausePopupView).Assembly,
                typeof(GameplayUiFlowInstaller).Assembly,
            }.Distinct().ToArray();
        }

        private static IEnumerable<Type> GetPublicSurfaceTypes(Type type)
        {
            yield return type;

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var surfacedType in ExpandType(property.PropertyType))
                {
                    yield return surfacedType;
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                foreach (var surfacedType in ExpandType(method.ReturnType))
                {
                    yield return surfacedType;
                }

                foreach (var parameter in method.GetParameters())
                {
                    foreach (var surfacedType in ExpandType(parameter.ParameterType))
                    {
                        yield return surfacedType;
                    }
                }
            }
        }

        private static IEnumerable<Type> ExpandType(Type type)
        {
            var normalizedType = NormalizeType(type);
            if (normalizedType == null)
            {
                yield break;
            }

            yield return normalizedType;

            if (!normalizedType.IsGenericType)
            {
                yield break;
            }

            foreach (var argument in normalizedType.GetGenericArguments())
            {
                foreach (var surfacedType in ExpandType(argument))
                {
                    yield return surfacedType;
                }
            }
        }

        private static Type NormalizeType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type.IsArray)
            {
                type = type.GetElementType();
            }

            return Nullable.GetUnderlyingType(type) ?? type;
        }

        private static bool TypeDependsOn(Type type, Type dependencyType)
        {
            if (type == null || dependencyType == null)
            {
                return false;
            }

            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(field => NormalizeType(field.FieldType) == dependencyType) ||
                   type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(property => NormalizeType(property.PropertyType) == dependencyType) ||
                   type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Any(ctor => ctor.GetParameters().Any(parameter => NormalizeType(parameter.ParameterType) == dependencyType)) ||
                   type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(method =>
                           NormalizeType(method.ReturnType) == dependencyType ||
                           method.GetParameters().Any(parameter => NormalizeType(parameter.ParameterType) == dependencyType));
        }
    }
}
