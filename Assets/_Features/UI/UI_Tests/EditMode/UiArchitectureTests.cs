using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
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
        public void NonCompositionUiAssemblies_DoNotReferenceGameplayHostAssembly()
        {
            var nonCompositionAssemblies = new[]
            {
                typeof(GameplayHudPresenter).Assembly,
                typeof(HUDController).Assembly,
                typeof(GameplayHudView).Assembly,
                typeof(GameplayScreenView).Assembly,
                typeof(PausePopupView).Assembly,
            };

            foreach (var assembly in nonCompositionAssemblies.Distinct())
            {
                var references = assembly
                    .GetReferencedAssemblies()
                    .Select(reference => reference.Name)
                    .ToArray();

                Assert.That(references, Does.Not.Contain(typeof(GameplaySceneHost).Assembly.GetName().Name), assembly.GetName().Name);
            }
        }

        [Test]
        public void CompositionAssembly_IsTheOnlyUiAssemblyReferencingGameplayHost()
        {
            var references = typeof(GameplayUiFlowInstaller).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Contain(typeof(GameplaySceneHost).Assembly.GetName().Name));
        }

        [Test]
        public void NonCompositionUiAssemblies_DoNotExposeForbiddenGameplayTypes()
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
                typeof(GameplayHudPresenter).Assembly,
                typeof(HUDController).Assembly,
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
                                (type.Namespace != null && type.Namespace.StartsWith("Game.Feature.Gameplay.Host", StringComparison.Ordinal))))
                .Distinct()
                .ToArray();

            Assert.That(leakedTypes, Is.Empty);
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
    }
}
