using System;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayCameraStartupPlanArchitectureTests
    {
        private const string StartupPlanRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap/GameplayResolvedCameraStartupPlan.cs";
        private const string StartupPlanComposerRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap/GameplayCameraStartupPlanComposer.cs";
        private const string HostRuntimeFactoryRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs";
        private const string HostBootstrapRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap/GameplayHostTopologyVisualRuntimeBootstrap.cs";
        private const string SceneBootstrapRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap/GameplayShowcaseSceneCameraBootstrap.cs";
        private const string InstallerBaseRelativePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs";

        [Test]
        [Category("Extended")]
        public void ResolvedStartupPlanType_Exists_AsInternalStandaloneDeclaration()
        {
            var startupPlanSource = ReadRepoFile(StartupPlanRelativePath);
            var runtimeAssembly = typeof(GameplaySceneHost).Assembly;
            var startupPlanType = runtimeAssembly.GetType("Game.Feature.Gameplay.Host.GameplayResolvedCameraStartupPlan");

            Assert.That(startupPlanSource, Does.Contain("internal readonly struct GameplayResolvedCameraStartupPlan"));
            Assert.That(startupPlanSource, Does.Contain("internal bool UsesDirectCameraPath { get; }"));
            Assert.That(startupPlanSource, Does.Contain("internal bool UsesHierarchyCinemachinePath { get; }"));
            Assert.That(startupPlanType, Is.Not.Null);
            Assert.That(startupPlanType?.IsNotPublic, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void StartupPlanComposer_CentralizesStartupResolution_AndFallbacks()
        {
            var startupPlanComposerSource = ReadRepoFile(StartupPlanComposerRelativePath);

            Assert.That(startupPlanComposerSource, Does.Contain("internal static class GameplayCameraStartupPlanComposer"));
            Assert.That(startupPlanComposerSource, Does.Contain("cameraRig.ResolveConfiguredSettings("));
            Assert.That(startupPlanComposerSource, Does.Contain("ResolveViewCamera("));
            Assert.That(startupPlanComposerSource, Does.Contain("ResolveOutputCamera("));
            Assert.That(startupPlanComposerSource, Does.Contain("ResolveOutputCameraBrain("));
            Assert.That(startupPlanComposerSource, Does.Contain("configuration.CameraSettings ?? GameplayCameraSettings.CreateRuntimeDefault()"));
        }

        [Test]
        [Category("Extended")]
        public void HostRuntimeFactory_ComposesStartupPlan_BeforeHostBootstrapAttach()
        {
            var factorySource = ReadRepoFile(HostRuntimeFactoryRelativePath);
            var composeIndex = factorySource.IndexOf(
                "var startupPlan = GameplayCameraStartupPlanComposer.Compose(",
                StringComparison.Ordinal);
            var lensApplyIndex = factorySource.IndexOf(
                "GameplayShowcaseSceneCameraBootstrap.ApplyResolvedStartupLens(",
                StringComparison.Ordinal);
            var attachIndex = factorySource.IndexOf(
                "GameplayHostTopologyVisualRuntimeBootstrap.Attach(",
                StringComparison.Ordinal);

            Assert.That(composeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(lensApplyIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(attachIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(composeIndex, Is.LessThan(lensApplyIndex));
            Assert.That(lensApplyIndex, Is.LessThan(attachIndex));
        }

        [Test]
        [Category("Extended")]
        public void HostBootstrap_ConsumesResolvedStartupPlan_WithoutSecondResolution()
        {
            var hostBootstrapSource = ReadRepoFile(HostBootstrapRelativePath);

            Assert.That(hostBootstrapSource, Does.Not.Contain("ResolveConfiguredSettings("));
            Assert.That(hostBootstrapSource, Does.Not.Contain("Camera.main"));
            Assert.That(hostBootstrapSource, Does.Not.Contain("GetComponent<CinemachineBrain>()"));
            Assert.That(hostBootstrapSource, Does.Contain("presenter.AttachOutputCamera(startupPlan.OutputCamera);"));
            Assert.That(hostBootstrapSource, Does.Contain("cameraRig.ConfigureTopologyTransitionCameraShake(startupPlan.TopologyTransitionCameraShakeProfile);"));
            Assert.That(hostBootstrapSource, Does.Contain("cameraRig.ApplySettings(startupPlan.ResolvedCameraSettings);"));
            Assert.That(hostBootstrapSource, Does.Contain("startupPlan.UsesDirectCameraPath ? startupPlan.ViewCamera : null"));
        }

        [Test]
        [Category("Extended")]
        public void SceneBootstrap_RemainsCaptureAndWiringOnly_AfterStartupPlanMigration()
        {
            var sceneBootstrapSource = ReadRepoFile(SceneBootstrapRelativePath);

            Assert.That(sceneBootstrapSource, Does.Contain("rig?.CaptureAuthoredSceneCameraPose("));
            Assert.That(sceneBootstrapSource, Does.Contain("cinemachineCamera.Target = new CameraTarget"));
            Assert.That(sceneBootstrapSource, Does.Contain("brains[j].DefaultBlend ="));
            Assert.That(sceneBootstrapSource, Does.Contain("internal static void ApplyResolvedStartupLens("));
            Assert.That(sceneBootstrapSource, Does.Not.Contain("rig.ApplySettings("));
            Assert.That(sceneBootstrapSource, Does.Not.Contain("ConfigureTopologyTransitionCameraShake("));
            Assert.That(sceneBootstrapSource, Does.Not.Contain("GetUniversalAdditionalCameraData("));
            Assert.That(sceneBootstrapSource, Does.Not.Contain("UseAuthoredSceneCameraLens"));
        }

        [Test]
        [Category("Extended")]
        public void InstallerAwake_NoLongerResolvesOrAppliesRuntimeStartupMeaning()
        {
            var installerSource = ReadRepoFile(InstallerBaseRelativePath);
            var awakeBody = ExtractMethodBody(installerSource, "protected virtual void Awake()");

            Assert.That(awakeBody, Does.Contain("GameplayShowcaseSceneScaffold.EnsureInstallerScaffold("));
            Assert.That(awakeBody, Does.Contain("host.Initialize(CreateConfiguration(initialState, baseCameraSettings));"));
            Assert.That(awakeBody, Does.Not.Contain("ResolveEffectiveCameraSettings("));
            Assert.That(awakeBody, Does.Not.Contain("rig?.ApplySettings("));
            Assert.That(awakeBody, Does.Not.Contain("ConfigureSceneCamera("));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), $"Missing method signature '{signature}'.");

            var bodyStart = source.IndexOf('{', signatureIndex);
            Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Missing body start for '{signature}'.");

            var braceDepth = 0;
            for (var i = bodyStart; i < source.Length; i++)
            {
                switch (source[i])
                {
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        if (braceDepth == 0)
                        {
                            return source.Substring(bodyStart + 1, i - bodyStart - 1);
                        }

                        break;
                }
            }

            throw new InvalidOperationException($"Could not extract method body for '{signature}'.");
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
