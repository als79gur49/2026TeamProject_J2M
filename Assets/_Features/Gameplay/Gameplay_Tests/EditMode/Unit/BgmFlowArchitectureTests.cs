using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class BgmFlowArchitectureTests
    {
        [Test]
        [Category("Extended")]
        public void FlowAudioAssembly_RemainsSeparateFromGameplayOneShotPlanningAssemblies()
        {
            var references = typeof(BgmFlowCoordinator).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(typeof(GameplayAudioMap).Assembly.GetName().Name));
            Assert.That(references, Does.Not.Contain(typeof(GameplayTickViewPresenter).Assembly.GetName().Name));
        }

        [Test]
        [Category("Core")]
        public void IBgmPlaybackPort_PublicSurface_UsesRequestBasedPlayStop()
        {
            var methods = typeof(IBgmPlaybackPort)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => !method.IsSpecialName)
                .ToArray();
            var methodNames = methods.Select(method => method.Name).OrderBy(name => name).ToArray();

            Assert.That(methodNames, Is.EqualTo(new[] { "Play", "Stop" }));
            Assert.That(
                methods.Single(method => method.Name == nameof(IBgmPlaybackPort.Play)).GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(BgmPlaybackRequest)));
            Assert.That(
                methods.Single(method => method.Name == nameof(IBgmPlaybackPort.Stop)).GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(BgmStopRequest)));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioSemanticId_PublicSurface_RemainsOneShotOnly_WithoutBgmIds()
        {
            Assert.That(
                Enum.GetNames(typeof(GameplayAudioSemanticId)),
                Is.EqualTo(new[]
                {
                    "None",
                    "PlayerDamage",
                    "EnemyDamage",
                    "EntityExitItemConsume",
                    "EntityExitBoxDestroy",
                    "EntityExitEnemyDeath",
                    "EntityExitOutOfBounds",
                }));
        }

        [Test]
        [Category("Core")]
        public void BgmRequestSourceKind_PublicSurface_IsSceneDefaultAndStageGameplayOnly()
        {
            Assert.That(
                Enum.GetNames(typeof(BgmRequestSourceKind)),
                Is.EqualTo(new[] { "SceneDefault", "StageGameplay" }));
            Assert.That(
                Enum.GetNames(typeof(BgmRequestPriority)),
                Is.EqualTo(new[] { "SceneDefault", "StageGameplay" }));
            Assert.That((int)BgmRequestPriority.SceneDefault, Is.EqualTo(100));
            Assert.That((int)BgmRequestPriority.StageGameplay, Is.EqualTo(300));
        }

        [Test]
        [Category("Extended")]
        public void BgmRequestRegistration_PublicSurface_IsLeaseOnlyWithoutUnscopedClear()
        {
            var methods = typeof(BgmRequestRouter)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .ToArray();

            Assert.That(
                methods.Select(method => method.Name).OrderBy(name => name).ToArray(),
                Is.EqualTo(new[] { "Acquire", "BeginPlaybackSuppression" }));
            Assert.That(
                methods.Single(method => method.Name == "Acquire").ReturnType,
                Is.EqualTo(typeof(BgmRequestLease)));
            Assert.That(
                typeof(StageAudioRuntimeRequestSource).GetMethod("Apply")?.ReturnType,
                Is.EqualTo(typeof(BgmRequestLease)));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRequestOwners_RetainAndDisposeAcquiredLeases()
        {
            var sceneSource = ReadRepoFile(
                "Assets/_Features/Flow/Flow_Audio/Runtime/SceneBgmRequestSource.cs");
            var stageSource = ReadRepoFile(
                "Assets/_Features/Flow/Flow_Audio/Runtime/StageAudioRuntimeRequestSource.cs");
            var stageOwner = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");

            Assert.That(sceneSource, Does.Contain("BgmRequestLease requestLease"));
            Assert.That(sceneSource, Does.Contain("private void OnDestroy()"));
            Assert.That(stageSource, Does.Contain("public BgmRequestLease Apply("));
            Assert.That(stageSource, Does.Contain("return router.Acquire("));
            Assert.That(stageOwner, Does.Contain("BgmRequestLease _stageBgmRequestLease"));
            Assert.That(stageOwner, Does.Contain("ReleaseStageBgmRequestLease();"));
            Assert.That(sceneSource + stageSource + stageOwner, Does.Not.Contain(".Submit(BgmFlowRequest"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioCatalog_RequiredSet_RemainsOneShotOnly_WithoutBgmFlow()
        {
            Assert.That(
                GameplayAudioSemanticCatalog.RequiredOneShotV1.ToArray(),
                Is.EqualTo(new[]
                {
                    GameplayAudioSemanticId.PlayerDamage,
                    GameplayAudioSemanticId.EnemyDamage,
                    GameplayAudioSemanticId.EntityExitItemConsume,
                    GameplayAudioSemanticId.EntityExitBoxDestroy,
                    GameplayAudioSemanticId.EntityExitEnemyDeath,
                    GameplayAudioSemanticId.EntityExitOutOfBounds,
                }));
            Assert.That(
                GameplayAudioSemanticCatalog.ApprovedHostOneShotFamiliesV1.ToArray(),
                Is.EqualTo(new[]
                {
                    GameplayAudioSemanticFamily.DamageOneShot,
                    GameplayAudioSemanticFamily.EntityExitOneShot,
                }));
            Assert.That(
                GameplayAudioSemanticCatalog.ApprovedHostOneShotFamiliesV1.Any(family => family == GameplayAudioSemanticFamily.BgmFlow),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_Source_DoesNotGainBgmOwnership()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Audio/Runtime/GameplayAudioMap.cs");

            Assert.That(source, Does.Not.Contain("BgmProfile"));
            Assert.That(source, Does.Not.Contain("IBgmFlowCoordinator"));
            Assert.That(source, Does.Not.Contain("PlayBgm"));
            Assert.That(source, Does.Not.Contain("BgmTransitionMode"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioPresentationController_Source_DoesNotGainBgmOwnership()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAudioPresentationController.cs");

            Assert.That(source, Does.Not.Contain("BgmProfile"));
            Assert.That(source, Does.Not.Contain("IBgmFlowCoordinator"));
            Assert.That(source, Does.Not.Contain("GlobalAudioFlow"));
            Assert.That(source, Does.Not.Contain("PlayBgm("));
            Assert.That(source, Does.Not.Contain("StopBgm("));
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHostConfiguration_DoesNotExposeBgmFlowOwnershipFields()
        {
            var fields = typeof(GameplaySceneHostConfiguration)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .ToArray();

            Assert.That(fields.Any(field => field.FieldType == typeof(BgmProfile)), Is.False);
            Assert.That(fields.Any(field => field.FieldType == typeof(GlobalAudioFlowBootstrap)), Is.False);
            Assert.That(fields.Any(field => field.Name.Contains("Bgm")), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SceneBgmRequestSource_Source_DoesNotReferenceIAudioServiceDirectly()
        {
            var source = ReadRepoFile("Assets/_Features/Flow/Flow_Audio/Runtime/SceneBgmRequestSource.cs");

            Assert.That(source, Does.Not.Contain("IAudioService"));
            Assert.That(source, Does.Not.Contain("PlayBgm("));
            Assert.That(source, Does.Not.Contain("StopBgm("));
            Assert.That(source, Does.Not.Contain("FadeOutSeconds"));
            Assert.That(source, Does.Not.Contain("FadeInSeconds"));
        }

        [Test]
        [Category("Extended")]
        public void StageVisualRuntimeAdapter_DoesNotReferenceBgm()
        {
            var source = ReadRepoFile("Assets/_Features/Flow/Flow_Audio/Runtime/StageVisualRuntimeAdapter.cs");

            Assert.That(source, Does.Not.Contain("BgmProfile"));
            Assert.That(source, Does.Not.Contain("IBgmFlowCoordinator"));
            Assert.That(source, Does.Not.Contain("BgmRequestRouter"));
            Assert.That(source, Does.Not.Contain("FadeOutSeconds"));
            Assert.That(source, Does.Not.Contain("FadeInSeconds"));
            Assert.That(source, Does.Not.Contain("PlayBgm("));
        }

        [Test]
        [Category("Extended")]
        public void StageResultFlow_DoesNotOwnBgm()
        {
            var sources = new[]
            {
                ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/StageResultAutoNextDriver.cs"),
                ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs"),
                ReadRepoFile("Assets/_Features/UI/UI_Flow/Runtime/UIFlowCoordinator.cs"),
            };

            for (var i = 0; i < sources.Length; i++)
            {
                Assert.That(sources[i], Does.Not.Contain("BgmProfile"));
                Assert.That(sources[i], Does.Not.Contain("BgmRequestRouter"));
                Assert.That(sources[i], Does.Not.Contain("IBgmFlowCoordinator"));
                Assert.That(sources[i], Does.Not.Contain("PlayBgm("));
            }
        }

        [Test]
        [Category("Extended")]
        public void StagePresentationDefinition_PublicSurface_DoesNotOwnBgm()
        {
            var source = ReadRepoFile("Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs");
            var propertyNames = typeof(StagePresentationDefinition)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            Assert.That(source, Does.Not.Contain("Bgm" + "Reference"));
            Assert.That(source, Does.Not.Contain("BgmProfile"));
            Assert.That(propertyNames.Any(name => name.Contains("Bgm")), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void MainMenuUiFlowInstaller_Source_DoesNotReadBgmTransitionPolicy()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(source, Does.Not.Contain("BgmTransitionMode"));
            Assert.That(source, Does.Not.Contain("FadeOutSeconds"));
            Assert.That(source, Does.Not.Contain("FadeInSeconds"));
            Assert.That(source, Does.Not.Contain("PlayBgm"));
        }

        [Test]
        [Category("Extended")]
        public void ComicSequenceAudioFocusController_UsesRouterPlaybackSuppression()
        {
            var source = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/ComicSequenceAudioFocusController.cs");

            Assert.That(source, Does.Contain("GetRequestRouterOrThrow()"));
            Assert.That(source, Does.Contain("BeginPlaybackSuppression()"));
            Assert.That(source, Does.Not.Contain("GetCoordinatorOrThrow()"));
            Assert.That(source, Does.Not.Contain("StopCurrent()"));
            Assert.That(source, Does.Not.Contain("BgmFlowRequest"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAndUiComposition_Sources_RemainSameRootInstallerBased_WithoutGlobalCoordinatorLookup()
        {
            var gameplaySource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs");
            var uiSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");

            Assert.That(gameplaySource, Does.Contain("GetComponent<AudioRuntimeInstaller>()"));
            Assert.That(gameplaySource, Does.Not.Contain("GlobalAudioFlowBootstrap"));
            Assert.That(gameplaySource, Does.Not.Contain("GlobalAudioFlowRoot"));
            Assert.That(gameplaySource, Does.Not.Contain("IBgmFlowCoordinator"));
            Assert.That(uiSource, Does.Contain("GetComponent<AudioRuntimeInstaller>()"));
            Assert.That(uiSource, Does.Not.Contain("GlobalAudioFlowBootstrap"));
            Assert.That(uiSource, Does.Not.Contain("GlobalAudioFlowRoot"));
            Assert.That(uiSource, Does.Not.Contain("IBgmFlowCoordinator"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
