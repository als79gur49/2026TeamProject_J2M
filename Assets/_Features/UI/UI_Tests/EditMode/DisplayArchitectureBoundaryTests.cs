using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class DisplayArchitectureBoundaryTests
    {
        [Test]
        public void SettingsPresenterAndView_Sources_DoNotCallScreenOrPlayerPrefs()
        {
            var presenterSource = ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/ScreenPresenters.cs");
            var rootViewSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SettingsScreenView.cs");
            var audioViewSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SettingsAudioView.cs");
            var displayViewSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SettingsDisplayView.cs");

            Assert.That(presenterSource, Does.Not.Contain("Screen.SetResolution"));
            Assert.That(presenterSource, Does.Not.Contain("Screen.fullScreenMode"));
            Assert.That(presenterSource, Does.Not.Contain("Screen.resolutions"));
            Assert.That(presenterSource, Does.Not.Contain("PlayerPrefs"));
            AssertViewSourceHasNoScreenOrPrefsCalls(rootViewSource);
            AssertViewSourceHasNoScreenOrPrefsCalls(audioViewSource);
            AssertViewSourceHasNoScreenOrPrefsCalls(displayViewSource);
        }

        [Test]
        public void SettingsRootView_Source_RemainsShellOnly_WithTemporaryPassthroughHelpers()
        {
            var rootViewSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SettingsScreenView.cs");

            Assert.That(rootViewSource, Does.Contain("Temporary compatibility passthroughs only; no section-local logic belongs here."));
            Assert.That(rootViewSource, Does.Contain("DisplayView.ClickApply();"));
            Assert.That(rootViewSource, Does.Contain("DisplayView.ClickRevert();"));
            Assert.That(rootViewSource, Does.Contain("AudioView.CommitInteraction(channel);"));
            Assert.That(rootViewSource, Does.Contain("DisplayView.SelectResolution(index);"));
            Assert.That(rootViewSource, Does.Contain("AudioView.SetMuted(channel, isMuted);"));
            Assert.That(rootViewSource, Does.Contain("AudioView.SetVolume(channel, value);"));
            Assert.That(rootViewSource, Does.Contain("DisplayView.SetFullscreen(isFullscreen);"));
            Assert.That(rootViewSource, Does.Not.Contain("new GameObject("));
            Assert.That(rootViewSource, Does.Not.Contain("AddComponent<"));
            Assert.That(rootViewSource, Does.Not.Contain("transform.Find("));
            Assert.That(rootViewSource, Does.Not.Contain("GetComponentInChildren<"));
            Assert.That(rootViewSource, Does.Not.Contain("SettingsLayoutUtility."));
            Assert.That(rootViewSource, Does.Not.Contain("SetSiblingIndex("));
            Assert.That(rootViewSource, Does.Not.Contain("ResolutionHover"));
            Assert.That(rootViewSource, Does.Not.Contain("Slider"));
            Assert.That(rootViewSource, Does.Not.Contain("Dropdown"));
        }

        [Test]
        public void SettingsRootView_DeclaredFields_RemainShellOnly_AndChildRefsOnly()
        {
            var declaredFields = typeof(Game.Feature.UI.Screens.SettingsScreenView)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsStatic)
                .ToArray();
            var fieldNames = declaredFields.Select(field => field.Name).ToArray();
            var fieldTypes = declaredFields.Select(field => field.FieldType).ToArray();

            Assert.That(fieldNames, Does.Contain("_audioView"));
            Assert.That(fieldNames, Does.Contain("_displayView"));
            Assert.That(fieldNames, Does.Not.Contain("_mainRow"));
            Assert.That(fieldNames, Does.Not.Contain("_bgmRow"));
            Assert.That(fieldNames, Does.Not.Contain("_sfxRow"));
            Assert.That(fieldNames, Does.Not.Contain("_resolutionDropdown"));
            Assert.That(fieldNames, Does.Not.Contain("_applyButton"));
            Assert.That(fieldNames, Does.Not.Contain("_revertButton"));
            Assert.That(fieldTypes, Has.No.Member(typeof(Slider)));
            Assert.That(fieldTypes, Has.No.Member(typeof(Toggle)));
            Assert.That(fieldTypes, Has.No.Member(typeof(Dropdown)));
        }

        [Test]
        public void SettingsChildView_Sources_DoNotRebuildAuthoredControlsAtRuntime()
        {
            var audioViewSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SettingsAudioView.cs");
            var displayViewSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SettingsDisplayView.cs");
            var inputViewSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SettingsInputView.cs");

            AssertViewSourceDoesNotRebuildAuthoredControls(audioViewSource);
            AssertViewSourceDoesNotRebuildAuthoredControls(SanitizeRuntimeKeyboardResolutionList(displayViewSource));
            AssertViewSourceDoesNotRebuildAuthoredControls(inputViewSource);
            Assert.That(audioViewSource, Does.Not.Contain("Label.text"));
            Assert.That(displayViewSource, Does.Not.Contain("DisplaySectionTitle"));
            Assert.That(displayViewSource, Does.Not.Contain("CurrentDisplayLabel"));
            Assert.That(displayViewSource, Does.Not.Contain("ResolutionLabel"));
            Assert.That(displayViewSource, Does.Not.Contain("ResolutionHoverHintText"));
            Assert.That(displayViewSource, Does.Not.Contain("FullscreenLabel"));
            Assert.That(displayViewSource, Does.Not.Contain("DisplayApplyLabel"));
            Assert.That(displayViewSource, Does.Not.Contain("DisplayRevertLabel"));
        }

        private static void AssertViewSourceDoesNotRebuildAuthoredControls(string viewSource)
        {
            Assert.That(viewSource, Does.Not.Contain("new GameObject("));
            Assert.That(viewSource, Does.Not.Contain("transform.Find("));
            Assert.That(viewSource, Does.Not.Contain("GetComponentInChildren<"));
            Assert.That(viewSource, Does.Not.Contain("Resources.GetBuiltinResource"));
            Assert.That(viewSource, Does.Not.Contain("SettingsLayoutUtility.EnsureChildRect"));
            Assert.That(viewSource, Does.Not.Contain("SettingsLayoutUtility.MoveToParent"));
            Assert.That(viewSource, Does.Not.Contain("SettingsLayoutUtility.EnsureVerticalLayout"));
            Assert.That(viewSource, Does.Not.Contain("SettingsLayoutUtility.EnsureHorizontalLayout"));
            Assert.That(viewSource, Does.Not.Contain("SettingsLayoutUtility.EnsureLayoutElement"));
            Assert.That(viewSource, Does.Not.Contain("SettingsLayoutUtility.FillLayoutChild"));
            Assert.That(viewSource, Does.Not.Contain("SetSiblingIndex("));
        }

        private static string SanitizeRuntimeKeyboardResolutionList(string viewSource)
        {
            return viewSource
                .Replace("new GameObject(\"ResolutionKeyboardDropdownList\"", "RuntimeKeyboardList(")
                .Replace("new GameObject(\"Viewport\"", "RuntimeKeyboardList(")
                .Replace("new GameObject(\"Content\"", "RuntimeKeyboardList(")
                .Replace("new GameObject($\"Option_{index:00}\"", "RuntimeKeyboardList(")
                .Replace("new GameObject(\"Label\"", "RuntimeKeyboardList(");
        }

        private static void AssertViewSourceHasNoScreenOrPrefsCalls(string viewSource)
        {
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
