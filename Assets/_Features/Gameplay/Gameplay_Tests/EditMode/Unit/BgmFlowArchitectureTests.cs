using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.Host;
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
        [Category("Extended")]
        public void IBgmPlaybackPort_PublicSurface_RemainsImmediateOnly()
        {
            var methodNames = typeof(IBgmPlaybackPort)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(methodNames, Is.EqualTo(new[] { "PlayImmediate", "StopImmediate" }));
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
