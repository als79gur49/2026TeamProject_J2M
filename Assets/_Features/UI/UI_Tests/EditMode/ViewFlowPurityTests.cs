using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.UI.Tests
{
    public sealed class ViewFlowPurityTests
    {
        [Test]
        public void ScreenViews_DoNotDependOnFlowControllers()
        {
            AssertViewTypesDoNotDependOn(new[]
            {
                typeof(ScreenController),
                typeof(PopupController),
                typeof(HUDController),
                typeof(UIFlowCoordinator),
            });
        }

        [Test]
        public void PopupViews_DoNotManipulatePopupStack()
        {
            AssertViewTypesDoNotDependOn(new[]
            {
                typeof(PopupController),
                typeof(PopupRequest),
                typeof(PopupEntry),
            });
        }

        [Test]
        public void Views_DoNotCallSceneOrAudioRuntime()
        {
            AssertViewTypesDoNotDependOn(new[]
            {
                typeof(IUiAudioPort),
                typeof(GameplaySceneHost),
            });

            var viewSources = Directory.GetFiles("Assets/_Features/UI", "*View.cs", SearchOption.AllDirectories)
                .Where(path => path.Contains("/UI_Screens/", StringComparison.Ordinal) ||
                               path.Contains("/UI_Popups/", StringComparison.Ordinal) ||
                               path.Contains("/UI_HUD/", StringComparison.Ordinal))
                .ToArray();
            foreach (var sourcePath in viewSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain(nameof(SceneManager)), sourcePath);
                Assert.That(source, Does.Not.Contain("Game.Shared.Audio"), sourcePath);
                Assert.That(source, Does.Not.Contain("Game.Shared.Display"), sourcePath);
            }
        }

        [Test]
        public void Views_MayExposeCallbacksOnly()
        {
            var forbiddenSurfaceTypes = new[]
            {
                typeof(ScreenAction),
                typeof(ScreenRequest),
                typeof(PopupRequest),
                typeof(IUiAudioPort),
                typeof(UIFlowCoordinator),
            };

            foreach (var viewType in GetViewTypes())
            {
                var publicSurfaceTypes = viewType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(method => !method.IsSpecialName)
                    .SelectMany(method => method.GetParameters().Select(parameter => NormalizeType(parameter.ParameterType))
                        .Append(NormalizeType(method.ReturnType)))
                    .Concat(viewType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                        .Select(property => NormalizeType(property.PropertyType)))
                    .Where(type => type != null)
                    .ToArray();

                foreach (var forbiddenType in forbiddenSurfaceTypes)
                {
                    Assert.That(publicSurfaceTypes, Has.No.Member(forbiddenType), viewType.FullName);
                }
            }
        }

        private static void AssertViewTypesDoNotDependOn(Type[] forbiddenTypes)
        {
            foreach (var viewType in GetViewTypes())
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(TypeDependsOn(viewType, forbiddenType), Is.False, $"{viewType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        private static Type[] GetViewTypes()
        {
            return new[]
            {
                typeof(SettingsScreenView).Assembly,
                typeof(PausePopupView).Assembly,
                typeof(HUDRootView).Assembly,
            }
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract &&
                           typeof(MonoBehaviour).IsAssignableFrom(type) &&
                           type.Name.EndsWith("View", StringComparison.Ordinal))
            .ToArray();
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
