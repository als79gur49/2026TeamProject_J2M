using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayUiAccessArchitectureTests
    {
        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PublicSurface_DoesNotExposeForbiddenGameplayInternals()
        {
            var forbiddenTypes = new HashSet<Type>
            {
                typeof(WorldState),
                typeof(WorldSnapshot),
                typeof(TickResult),
                typeof(TickInputBuffer),
                typeof(TickInput),
                typeof(PlayerTickCommand),
                typeof(TickPresentationData),
                typeof(PlayerControlState),
                typeof(EntityExecutionLockState),
                typeof(EnemyActionRuntimeState),
                typeof(EnemyJumpRuntimeState),
                typeof(PlayerDamageState),
                typeof(IWorldWriteContext),
            };
            var forbiddenTypeNames = new HashSet<string>
            {
                "Game.Feature.Gameplay.Loop.MovementPhaseResult",
                "Game.Feature.Gameplay.Loop.AttackPhaseResult",
                "Game.Feature.Gameplay.Debug.TickTrace",
            };
            var assembly = typeof(IGameplayCommandGateway).Assembly;
            var leakedTypes = assembly
                .GetExportedTypes()
                .SelectMany(GetPublicSurfaceTypes)
                .Select(NormalizeType)
                .Where(type => type != null &&
                               (forbiddenTypes.Contains(type) ||
                               forbiddenTypeNames.Contains(type.FullName) ||
                               (type.Namespace != null && type.Namespace.StartsWith("Game.Feature.Gameplay.Host", StringComparison.Ordinal))))
                .Distinct()
                .ToArray();

            Assert.That(leakedTypes, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_Assembly_DoesNotReferenceGameplayAssembly()
        {
            var gameplayAssemblyName = typeof(WorldState).Assembly.GetName().Name;
            var references = typeof(IGameplayCommandGateway).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(gameplayAssemblyName));
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PublicNames_DoNotLeakForbiddenVocabulary()
        {
            var forbiddenTokens = new[]
            {
                "Snapshot",
                "RuntimeState",
                "Occupancy",
                "PhaseResult",
                "Commit",
                "InputBuffer",
                "Host",
            };
            var assembly = typeof(IGameplayCommandGateway).Assembly;
            var leakedNames = assembly
                .GetExportedTypes()
                .SelectMany(type => GetPublicNames(type))
                .Where(name => forbiddenTokens.Any(token => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                .Distinct()
                .ToArray();

            Assert.That(leakedNames, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_QueryFacade_OnlyExposesBoundedReaderProperties()
        {
            var facadeType = typeof(IGameplayQueryFacade);
            var publicInstanceMethods = facadeType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .ToArray();
            var properties = facadeType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .OrderBy(property => property.Name)
                .ToArray();

            Assert.That(publicInstanceMethods, Is.Empty);
            CollectionAssert.AreEqual(
                new[] { "Objectives", "PlayerHud", "Session" },
                properties.Select(property => property.Name).ToArray());
            Assert.That(properties.All(property => property.PropertyType.IsInterface), Is.True);
            Assert.That(properties.All(property => property.PropertyType.Name.EndsWith("Query", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiRecoveryCooldown_PublicSurface_RemainsMinimal()
        {
            var propertyNames = typeof(GameplayUiRecoveryCooldown)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[]
            {
                "ActionKind",
                "RemainingRecoveryTicks",
                "TotalRecoveryTicks",
            }));
        }

        private static IEnumerable<string> GetPublicNames(Type type)
        {
            yield return type.Name;

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                yield return property.Name;
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!method.IsSpecialName)
                {
                    yield return method.Name;
                }
            }
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

            foreach (var constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    foreach (var surfacedType in ExpandType(parameter.ParameterType))
                    {
                        yield return surfacedType;
                    }
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!method.IsSpecialName)
                {
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
        }

        private static IEnumerable<Type> ExpandType(Type type)
        {
            var normalizedType = NormalizeType(type);
            if (normalizedType == null)
            {
                yield break;
            }

            yield return normalizedType;

            if (normalizedType.IsGenericType)
            {
                foreach (var argument in normalizedType.GetGenericArguments())
                {
                    foreach (var surfacedType in ExpandType(argument))
                    {
                        yield return surfacedType;
                    }
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
