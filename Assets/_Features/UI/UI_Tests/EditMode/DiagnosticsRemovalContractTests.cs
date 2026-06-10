using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class DiagnosticsRemovalContractTests
    {
        private static readonly string[] DiagnosticsTokens =
        {
            "UiArchitectureDiagnostics",
            "UiArchitectureDiagnosticsOverlayView",
            "DiagnosticsOverlay",
            "DiagnosticsLayer",
            "WasF3PressedThisFrame",
            "WasF4PressedThisFrame",
            "f3Key",
            "f4Key",
        };

        [Test]
        public void UiComposition_DoesNotContainArchitectureDiagnosticsTypes()
        {
            foreach (var token in DiagnosticsTokens)
            {
                Assert.That(FindLoadedType(token), Is.Null, token);
            }

            AssertProductionUiSourcesDoNotContain(DiagnosticsTokens);
        }

        [Test]
        public void RootShell_DoesNotContainDiagnosticsLayer()
        {
            var prefab = ReadRepoFile("Assets/_Features/UI/UI_Composition/Resources/UI/GameplayUiCanvasRootShell.prefab");

            Assert.That(prefab, Does.Contain("m_Name: HudLayer"));
            Assert.That(prefab, Does.Contain("m_Name: ScreenLayer"));
            Assert.That(prefab, Does.Contain("m_Name: PopupLayer"));
            Assert.That(prefab, Does.Not.Contain("m_Name: DiagnosticsLayer"));
        }

        [Test]
        public void Installer_DoesNotWireDiagnosticsOverlay()
        {
            var installerSource = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            foreach (var token in DiagnosticsTokens)
            {
                Assert.That(installerSource, Does.Not.Contain(token), token);
            }
        }

        [Test]
        public void F3F4DiagnosticsHotkeys_AreRemoved_AndF10DemoRemains()
        {
            var installerSource = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");

            Assert.That(installerSource, Does.Not.Contain("WasF3PressedThisFrame"));
            Assert.That(installerSource, Does.Not.Contain("WasF4PressedThisFrame"));
            Assert.That(installerSource, Does.Contain("WasF10PressedThisFrame"));
            Assert.That(installerSource, Does.Contain("BackQuoteKeyProperty"));
            Assert.That(installerSource, Does.Contain("TryToggleDemoStageControlPanel"));
            Assert.That(installerSource, Does.Contain("Coordinator.RequestDemoStageControlPopup"));
            Assert.That(Enum.GetNames(typeof(DemoStageControlOpenKey)), Does.Contain(nameof(DemoStageControlOpenKey.F10)));
            Assert.That(Enum.GetNames(typeof(DemoStageControlOpenKey)), Does.Contain(nameof(DemoStageControlOpenKey.BackQuote)));
        }

        [Test]
        public void DemoStageControl_DoesNotDependOnUiArchitectureDiagnostics()
        {
            Assert.That(typeof(DemoStageControlPanelRuntime), Is.Not.Null);
            Assert.That(typeof(DemoStageControlPanelView), Is.Not.Null);
            Assert.That(Enum.GetNames(typeof(PopupId)), Does.Contain(nameof(PopupId.DemoStageControl)));

            var demoSources = Directory.GetFiles(
                    GetRepoPath("Assets/_Features/DemoStageControl"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .OrderBy(path => path)
                .ToArray();

            foreach (var sourcePath in demoSources)
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var token in DiagnosticsTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), sourcePath);
                }
            }
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
