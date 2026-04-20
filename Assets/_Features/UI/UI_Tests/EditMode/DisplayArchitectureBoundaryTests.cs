using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class DisplayArchitectureBoundaryTests
    {
        [Test]
        public void SettingsPresenterAndView_Sources_DoNotCallScreenOrPlayerPrefs()
        {
            var presenterSource = ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/ScreenPresenters.cs");
            var viewSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SettingsScreenView.cs");

            Assert.That(presenterSource, Does.Not.Contain("Screen.SetResolution"));
            Assert.That(presenterSource, Does.Not.Contain("Screen.fullScreenMode"));
            Assert.That(presenterSource, Does.Not.Contain("Screen.resolutions"));
            Assert.That(presenterSource, Does.Not.Contain("PlayerPrefs"));
            Assert.That(viewSource, Does.Not.Contain("Screen.SetResolution"));
            Assert.That(viewSource, Does.Not.Contain("Screen.fullScreenMode"));
            Assert.That(viewSource, Does.Not.Contain("Screen.resolutions"));
            Assert.That(viewSource, Does.Not.Contain("PlayerPrefs"));
        }

        [Test]
        public void GameplayUiFlowInstaller_Source_RequiresSameRootDisplayInstaller_WithoutSceneGlobalFallback()
        {
            var installerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");

            Assert.That(installerSource, Does.Contain("GetComponent<DisplayRuntimeInstaller>()"));
            Assert.That(installerSource, Does.Contain("GameplayUiFlowInstaller requires a co-located DisplayRuntimeInstaller on the canonical bootstrap root for SettingsScreen display controls."));
            Assert.That(installerSource, Does.Not.Contain("FindObjectOfType<DisplayRuntimeInstaller>"));
            Assert.That(installerSource, Does.Not.Contain("FindFirstObjectByType<DisplayRuntimeInstaller>"));
            Assert.That(installerSource, Does.Not.Contain("FindAnyObjectByType<DisplayRuntimeInstaller>"));
        }

        [Test]
        public void UiTestPrefabUtility_Source_AssignsDisplayInstaller_OnCanonicalRoot()
        {
            var utilitySource = ReadRepoFile("Assets/_Features/UI/UI_Tests/EditMode/UiTestPrefabAssetUtility.cs");

            Assert.That(utilitySource, Does.Contain("GetComponent<DisplayRuntimeInstaller>()"));
            Assert.That(utilitySource, Does.Contain("AddComponent<DisplayRuntimeInstaller>()"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath)));
        }
    }
}
