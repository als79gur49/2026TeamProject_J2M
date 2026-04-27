using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Flow.Audio;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using Game.Shared.Display;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuAudioSceneContractTests
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        private const string MainMenuBgmProfilePath = "Assets/_Shared/Audio/Definitions/Bgm/MainMenu_BgmProfile.asset";
        private const string MainMenuBgmDefinitionPath = "Assets/_Shared/Audio/Definitions/Bgm/MainMenu_BgmDef.asset";
        private const string UiAudioCueMapPath = "Assets/_Features/UI/UI_Composition/Authoring/UiAudioCueMap_V1.asset";
        private const string MainMenuUiFlowInstallerPath =
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs";
        private const string MainMenuUiAudioFeedbackControllerPath =
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiAudioFeedbackController.cs";

        private static readonly string[] MainMenuAudioSourcePaths =
        {
            MainMenuUiFlowInstallerPath,
            MainMenuUiAudioFeedbackControllerPath,
        };

        [Test]
        public void MainMenuScene_HasAudioRuntimeInstaller_WithPreferRegisteredPersistentRuntime()
        {
            var root = OpenMainMenuRoot();
            var audioInstaller = root.GetComponent<AudioRuntimeInstaller>();

            Assert.That(audioInstaller, Is.Not.Null);
            Assert.That(audioInstaller.BindingMode, Is.EqualTo(AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime));
        }

        [Test]
        public void MainMenuScene_HasGlobalAudioFlowBootstrap_CoLocatedWithAudioRuntimeInstaller()
        {
            var root = OpenMainMenuRoot();
            var audioInstaller = root.GetComponent<AudioRuntimeInstaller>();
            var bootstrap = root.GetComponent<GlobalAudioFlowBootstrap>();

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.AudioRuntimeInstaller, Is.SameAs(audioInstaller));
        }

        [Test]
        public void MainMenuScene_HasSceneBgmRequestSource_WithMenuBgmProfile()
        {
            var root = OpenMainMenuRoot();
            var bootstrap = root.GetComponent<GlobalAudioFlowBootstrap>();
            var requestSource = root.GetComponent<SceneBgmRequestSource>();

            Assert.That(requestSource, Is.Not.Null);
            Assert.That(requestSource.Bootstrap, Is.SameAs(bootstrap));
            Assert.That(requestSource.Profile, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(requestSource.Profile), Is.EqualTo(MainMenuBgmProfilePath));
            Assert.That(AssetDatabase.GetAssetPath(requestSource.Profile.LoopDefinition), Is.EqualTo(MainMenuBgmDefinitionPath));
            Assert.DoesNotThrow(() => requestSource.Profile.ValidateOrThrow());
        }

        [Test]
        public void MainMenuBgm_DoesNotCallPlayBgmDirectlyFromMainMenuUiFlowInstaller()
        {
            var source = ReadRepoFile(MainMenuUiFlowInstallerPath);

            Assert.That(source, Does.Not.Contain("PlayBgm"));
            Assert.That(source, Does.Contain("UiAudioPortAdapter"));
        }

        [Test]
        public void MainMenuScene_BgmRequestSource_IsOnlyBgmRequestOwner()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            var requestSources = FindSceneComponents<SceneBgmRequestSource>(scene);

            Assert.That(requestSources, Has.Length.EqualTo(1));
            Assert.That(ReadRepoFile(MainMenuScenePath), Does.Not.Contain("stageBgmProfileCatalog"));
        }

        [Test]
        public void MainMenuSettings_UsesSameAudioRuntimeAsMainMenuBgm()
        {
            var root = OpenMainMenuRoot();
            var installer = root.GetComponent<MainMenuUiFlowInstaller>();
            var audioInstaller = root.GetComponent<AudioRuntimeInstaller>();
            var bootstrap = root.GetComponent<GlobalAudioFlowBootstrap>();

            Assert.That(installer, Is.Not.Null);
            Assert.That(audioInstaller, Is.Not.Null);
            Assert.That(bootstrap.AudioRuntimeInstaller, Is.SameAs(audioInstaller));
            Assert.That(root.GetComponent<DisplayRuntimeInstaller>(), Is.Not.Null);
        }

        [Test]
        public void MainMenuUiFlowInstaller_OnDestroy_FlushesAudioSettings()
        {
            var root = new GameObject(nameof(MainMenuUiFlowInstaller_OnDestroy_FlushesAudioSettings));
            try
            {
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                var relay = root.AddComponent<AudioSettingsLifecycleRelay>();
                var audioPort = new RecordingAudioSettingsPort();
                relay.Initialize(audioPort);
                SetPrivateField(installer, "_audioSettingsLifecycleRelay", relay);

                InvokePrivate(installer, "OnDestroy");

                Assert.That(audioPort.FlushCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UiAudioCueMap_AssignedAndValid_ForMainMenu()
        {
            var root = OpenMainMenuRoot();
            var installer = root.GetComponent<MainMenuUiFlowInstaller>();
            var serializedInstaller = new SerializedObject(installer);
            var cueMapProperty = serializedInstaller.FindProperty("_uiAudioCueMap");

            Assert.That(cueMapProperty, Is.Not.Null);
            Assert.That(cueMapProperty.objectReferenceValue, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(cueMapProperty.objectReferenceValue), Is.EqualTo(UiAudioCueMapPath));

            var cueMap = (UiAudioCueMap)cueMapProperty.objectReferenceValue;
            Assert.DoesNotThrow(() => cueMap.ValidateOrThrow());
            Assert.That(cueMap.Entries.Count, Is.EqualTo(7));
        }

        [Test]
        public void MainMenuAudio_SourceGuard_DoesNotReferenceGameplayAudioMap()
        {
            AssertMainMenuAudioSourcesDoNotContain("GameplayAudioMap");
        }

        [Test]
        public void MainMenuAudio_SourceGuard_DoesNotReferenceGameplayAudioPresentationController()
        {
            AssertMainMenuAudioSourcesDoNotContain("GameplayAudioPresentationController");
        }

        [Test]
        public void MainMenuAudio_SourceGuard_DoesNotReferenceGameplayActionAudioPresentationController()
        {
            AssertMainMenuAudioSourcesDoNotContain("GameplayActionAudioPresentationController");
        }

        [Test]
        public void MainMenuAudio_SourceGuard_DoesNotReferenceGameplaySceneHost()
        {
            AssertMainMenuAudioSourcesDoNotContain("GameplaySceneHost");
        }

        [Test]
        public void MainMenuAudio_SourceGuard_DoesNotReferenceUIFlowCoordinator()
        {
            AssertMainMenuAudioSourcesDoNotContain("UIFlowCoordinator");
        }

        private static GameObject OpenMainMenuRoot()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects()
                .Where(root => root.GetComponent<MainMenuUiFlowInstaller>() != null)
                .ToArray();

            Assert.That(roots, Has.Length.EqualTo(1));
            return roots[0];
        }

        private static T[] FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void AssertMainMenuAudioSourcesDoNotContain(string token)
        {
            foreach (var path in MainMenuAudioSourcePaths)
            {
                Assert.That(ReadRepoFile(path), Does.Not.Contain(token), $"{path} must not reference {token}");
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName}");
            method.Invoke(target, null);
        }

        private sealed class RecordingAudioSettingsPort : IAudioSettingsPort
        {
            public int FlushCount { get; private set; }

            public AudioSettingsPortSnapshot Read()
            {
                var state = new AudioSettingsPortChannelState(1f, false);
                return new AudioSettingsPortSnapshot(state, state, state);
            }

            public void SetVolume(AudioSettingsChannel channel, float volume)
            {
            }

            public void SetMuted(AudioSettingsChannel channel, bool isMuted)
            {
            }

            public void Flush()
            {
                FlushCount++;
            }
        }
    }
}
