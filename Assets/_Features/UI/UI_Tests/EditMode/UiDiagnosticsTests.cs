using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UiDiagnosticsTests
    {
        private static readonly string[] DiagnosticsResidueTokens =
        {
            "UiArchitectureDiagnostics",
            "DiagnosticsOverlay",
            "DiagnosticsLayer",
            "UiDiagnostics",
            "WasF3PressedThisFrame",
            "WasF4PressedThisFrame",
            "f3Key",
            "f4Key",
        };

        [Test]
        public void ProductionUiRuntime_DiagnosticsOverlayResidue_IsAbsent()
        {
            var runtimeDirectory = GetRepoPath("Assets/_Features/UI");
            var productionSources = Directory.GetFiles(runtimeDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}UI_Tests{Path.DirectorySeparatorChar}"))
                .OrderBy(path => path)
                .ToArray();

            foreach (var sourcePath in productionSources)
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var token in DiagnosticsResidueTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), sourcePath);
                }
            }
        }

        [Test]
        public void GameplayUiCanvasRootShell_DiagnosticsLayerAndOverlayScript_AreAbsent()
        {
            var prefab = ReadRepoFile("Assets/_Features/UI/UI_Composition/Resources/UI/GameplayUiCanvasRootShell.prefab");

            Assert.That(prefab, Does.Not.Contain("DiagnosticsLayer"));
            Assert.That(prefab, Does.Not.Contain("UiDiagnosticsOverlay"));
            Assert.That(prefab, Does.Not.Contain("UiArchitectureDiagnosticsOverlayView"));
            Assert.That(prefab, Does.Not.Contain("4f1df27cab6e4a7a8f6fcf0f86960af1"));
            Assert.That(prefab, Does.Not.Contain("6cbda0f3d43c4a6b8c17121c5227f1d0"));
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
