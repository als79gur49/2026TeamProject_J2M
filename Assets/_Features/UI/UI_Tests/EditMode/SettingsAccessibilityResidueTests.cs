using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsAccessibilityResidueTests
    {
        private static readonly string[] AccessibilityTypeTokens =
        {
            "AccessibilitySettingsStore",
        };

        private static readonly string[] SettingsToggleTokens =
        {
            "AreTooltipsEnabled",
            "IsLargeTextEnabled",
            "ToggleTooltips",
            "ToggleLargeText",
            "TooltipsEnabled",
            "LargeText",
        };

        [Test]
        public void SettingsPresenter_DoesNotReferenceAccessibilitySettingsStore()
        {
            Assert.That(FindLoadedType("AccessibilitySettingsStore"), Is.Null);

            var presenterSource = ReadRepoFile(
                "Assets/_Features/UI/UI_Application/Runtime/Settings/SettingsScreenPresenters.cs");
            foreach (var token in AccessibilityTypeTokens)
            {
                Assert.That(presenterSource, Does.Not.Contain(token), token);
            }
        }

        [Test]
        public void SettingsPayloadAndState_DoNotContainTooltipOrLargeTextFields()
        {
            Assert.That(FindLoadedType("SettingsScreenState"), Is.Null);

            var boundedTypes = new[]
            {
                typeof(SettingsScreenPayload),
                typeof(SettingsScreenViewModel),
                typeof(SettingsScreenPresenter),
            };

            foreach (var type in boundedTypes)
            {
                var memberNames = type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(member => member.Name)
                    .ToArray();
                foreach (var token in SettingsToggleTokens)
                {
                    Assert.That(memberNames, Has.No.Member(token), type.FullName);
                }
            }

            AssertProductionUiSourcesDoNotContain(SettingsToggleTokens);
        }

        [Test]
        public void SettingsRuntimeComposition_DoesNotCreateAccessibilitySettingsStore()
        {
            var forbiddenTokens = new[]
            {
                "new AccessibilitySettingsStore()",
                "_accessibilitySettingsStore",
                "AccessibilitySettingsStore accessibilitySettingsStore",
                "SettingsScreenRuntimeBuildContext.AccessibilitySettingsStore",
            };
            var runtimeSources = new[]
            {
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/SettingsScreenRuntimeBuilder.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuSettingsRuntime.cs",
            };

            foreach (var sourcePath in runtimeSources)
            {
                var source = ReadRepoFile(sourcePath);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), sourcePath);
                }
            }
        }

        [Test]
        public void SettingsPrefab_DoesNotContainLegacyAccessibilityToggleObjects()
        {
            var prefab = ReadRepoFile(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var forbiddenObjectNames = new[]
            {
                "SettingsAccessibilityRows",
                "TooltipAccessibilityRow",
                "TooltipToggle",
                "TooltipStatus",
                "LargeTextAccessibilityRow",
                "LargeTextToggle",
                "LargeTextStatus",
            };

            foreach (var objectName in forbiddenObjectNames)
            {
                Assert.That(prefab, Does.Not.Contain($"m_Name: {objectName}"), objectName);
            }
        }

        [Test]
        public void TooltipPopup_IsNotTreatedAsAccessibilityToggleResidue()
        {
            Assert.That(Enum.GetNames(typeof(PopupId)), Does.Contain(nameof(PopupId.Tooltip)));
            Assert.That(typeof(TooltipPopupView), Is.Not.Null);

            var popupFactorySource = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayPopupRuntimeFactory.cs");
            Assert.That(popupFactorySource, Does.Contain("case PopupId.Tooltip:"));
            Assert.That(popupFactorySource, Does.Contain("CreateTooltipPopup"));
        }

        private static void AssertProductionUiSourcesDoNotContain(string[] forbiddenTokens)
        {
            foreach (var sourcePath in EnumerateProductionUiSources())
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), sourcePath);
                }
            }
        }

        private static IEnumerable<string> EnumerateProductionUiSources()
        {
            var root = GetRepoPath("Assets/_Features/UI");
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}UI_Tests{Path.DirectorySeparatorChar}"))
                .OrderBy(path => path);
        }

        private static Type FindLoadedType(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .FirstOrDefault(type => type.Name == typeName);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        private static string GetRepoPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetRepoPath(relativePath));
        }
    }
}
