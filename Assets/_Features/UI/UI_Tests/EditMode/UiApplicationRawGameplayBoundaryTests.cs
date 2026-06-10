using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UiApplicationRawGameplayBoundaryTests
    {
        [Test]
        public void RawGameplayFrame_IsConsumedOnlyByAllowedSeams()
        {
            var allowedTypes = new[]
            {
                typeof(GameplayUiPresentationSource),
                typeof(UITickEventRouter),
                typeof(UIStateMapper),
            };
            var rawTypes = new[]
            {
                typeof(IGameplayPresentationFeed),
                typeof(GameplayPresentationFrame),
                typeof(GameplayPresentationState),
            };

            foreach (var rawType in rawTypes)
            {
                var dependentTypes = typeof(HUDRootPresenter).Assembly
                    .GetTypes()
                    .Where(type => TypeDependsOn(type, rawType))
                    .Except(allowedTypes)
                    .Select(type => type.FullName)
                    .OrderBy(name => name)
                    .ToArray();

                Assert.That(dependentTypes, Is.Empty, rawType.FullName);
            }
        }

        [Test]
        public void Presenters_DoNotConsumeTickResultOrWorldState()
        {
            var forbiddenTypes = new[] { typeof(TickResult), typeof(WorldState) };
            var presenterTypes = typeof(HUDRootPresenter).Assembly
                .GetTypes()
                .Where(type => type.Name.EndsWith("Presenter", StringComparison.Ordinal))
                .ToArray();

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(TypeDependsOn(presenterType, forbiddenType), Is.False, $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void Views_DoNotReceiveRawGameplayFrame()
        {
            var rawTypes = new[]
            {
                typeof(TickResult),
                typeof(WorldState),
                typeof(GameplayPresentationFrame),
                typeof(GameplayPresentationState),
            };
            var viewTypes = typeof(SettingsScreenView).Assembly
                .GetTypes()
                .Where(type => type.Name.EndsWith("View", StringComparison.Ordinal))
                .ToArray();

            foreach (var viewType in viewTypes)
            {
                foreach (var rawType in rawTypes)
                {
                    Assert.That(TypeDependsOn(viewType, rawType), Is.False, $"{viewType.FullName} depends on {rawType.FullName}");
                }
            }
        }

        private static bool TypeDependsOn(Type type, Type dependencyType)
        {
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

        private static Type NormalizeType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsByRef || type.IsArray)
            {
                type = type.GetElementType();
            }

            return Nullable.GetUnderlyingType(type) ?? type;
        }
    }
}
