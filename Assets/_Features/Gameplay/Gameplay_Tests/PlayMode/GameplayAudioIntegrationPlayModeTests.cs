using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class GameplayAudioIntegrationPlayModeTests
    {
        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_GameplaySfx_RespectsMasterAndSfxMix_AndIgnoresBgmMix()
        {
            var context = CreateHostContext(nameof(GameplaySceneHost_GameplaySfx_RespectsMasterAndSfxMix_AndIgnoresBgmMix));
            try
            {
                var settings = context.Installer.AudioSettingsService;

                settings.SetChannelVolume(AudioChannel.Master, 0.8f);
                settings.SetChannelVolume(AudioChannel.Sfx, 0.5f);
                settings.SetChannelVolume(AudioChannel.Bgm, 0.1f);

                context.Host.Presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));
                yield return null;

                var snapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Sfx));
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.4f).Within(0.0001f));

                settings.SetChannelVolume(AudioChannel.Bgm, 0f);
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.4f).Within(0.0001f));

                settings.SetChannelMuted(AudioChannel.Sfx, true);
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));

                settings.SetChannelMuted(AudioChannel.Sfx, false);
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.4f).Within(0.0001f));

                settings.SetChannelVolume(AudioChannel.Master, 0.25f);
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.125f).Within(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_GameplaySfx_SettingsStayBounded_AndDoNotReplayConsumedPlan()
        {
            var persistenceStore = new RecordingAudioSettingsPersistenceStore();
            var context = CreateHostContext(
                nameof(GameplaySceneHost_GameplaySfx_SettingsStayBounded_AndDoNotReplayConsumedPlan),
                persistenceStore);
            try
            {
                var settings = context.Installer.AudioSettingsService;

                context.Host.Presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));
                yield return null;

                var initialSnapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(initialSnapshots, Has.Length.EqualTo(1));
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
                Assert.That(persistenceStore.SaveCallCount, Is.Zero);

                settings.SetChannelVolume(AudioChannel.Sfx, 0.3f);
                Assert.That(initialSnapshots[0].Source.volume, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.Zero);

                settings.SetChannelMuted(AudioChannel.Sfx, true);
                Assert.That(initialSnapshots[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.Zero);

                settings.SetChannelMuted(AudioChannel.Sfx, false);
                Assert.That(initialSnapshots[0].Source.volume, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.Zero);

                context.Host.Presenter.Present(CreateTickResult(TickPresentationData.Empty));
                yield return null;

                var replayGuardSnapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(replayGuardSnapshots, Has.Length.EqualTo(1));
                Assert.That(replayGuardSnapshots[0].Source, Is.SameAs(initialSnapshots[0].Source));
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                settings.FlushSettings();
                Assert.That(persistenceStore.SaveCallCount, Is.EqualTo(1));

                settings.SetChannelVolume(AudioChannel.Master, 0.5f);
                Assert.That(initialSnapshots[0].Source.volume, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        private static HostAudioIntegrationContext CreateHostContext(
            string rootName,
            RecordingAudioSettingsPersistenceStore persistenceStore = null)
        {
            persistenceStore ??= new RecordingAudioSettingsPersistenceStore();

            var hostObject = new GameObject(rootName);
            hostObject.SetActive(false);

            var installer = hostObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);

            var runtimeRootObject = new GameObject("AudioRuntimeRoot");
            runtimeRootObject.transform.SetParent(hostObject.transform, worldPositionStays: false);
            var runtimeRoot = runtimeRootObject.AddComponent<AudioRuntimeRoot>();
            var manager = runtimeRootObject.AddComponent<AudioManager>();
            manager.SetPersistenceStoreOverrideForTesting(persistenceStore);

            var host = hostObject.AddComponent<GameplaySceneHost>();
            var mapBundle = CreateGameplayAudioMapBundle();

            hostObject.SetActive(true);
            host.Initialize(CreateHostConfiguration(mapBundle.Map));

            Assert.That(installer.RuntimeRoot, Is.SameAs(runtimeRoot));
            Assert.That(installer.AudioService, Is.Not.Null);
            Assert.That(installer.AudioSettingsService, Is.Not.Null);

            return new HostAudioIntegrationContext(hostObject, host, installer, manager, mapBundle, persistenceStore);
        }

        private static GameplaySceneHostConfiguration CreateHostConfiguration(GameplayAudioMap gameplayAudioMap)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                InitialEntities = Array.Empty<EntityState>(),
                InitialTerrain = GameplayTerrainData.Empty,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                GameplayAudioMap = gameplayAudioMap,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
            };
        }

        private static TickPresentationData CreatePlayerDamagePresentationData(int entityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(entityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickResult CreateTickResult(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities = null)
        {
            var result = new TickResult(
                1,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetSerializedField(typeof(TickResult), result, "<PresentationData>k__BackingField", presentationData);
            SetSerializedField(typeof(TickResult), result, "<FinalTopology>k__BackingField", new CubeTopologyState(FaceId.Floor));
            SetSerializedField(
                typeof(TickResult),
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities ?? Array.Empty<EntityState>())));
            return result;
        }

        private static GameplayAudioMapBundle CreateGameplayAudioMapBundle()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definitions = new List<UnityEngine.Object>();
            var entryType = typeof(GameplayAudioMap).GetNestedType("Entry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null, "GameplayAudioMap.Entry type is required for runtime-authored map setup.");
            var entries = Array.CreateInstance(entryType, GameplayAudioSemanticCatalog.RequiredOneShotV1.Count);

            for (var i = 0; i < GameplayAudioSemanticCatalog.RequiredOneShotV1.Count; i++)
            {
                var semanticId = GameplayAudioSemanticCatalog.RequiredOneShotV1[i];
                var clip = AudioClip.Create($"{semanticId}_Loop", 4410, 1, 44100, false);
                var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                definition.name = semanticId.ToString();
                ConfigureDefinition(definition, clip, loop: true, AudioCategory.Sfx);
                definitions.Add(clip);
                definitions.Add(definition);

                var binding = new AudioBinding();
                SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
                SetSerializedField(typeof(AudioBinding), binding, "policy", null);

                var entry = Activator.CreateInstance(entryType);
                SetSerializedField(entryType, entry, "SemanticId", semanticId);
                SetSerializedField(entryType, entry, "Binding", binding);
                entries.SetValue(entry, i);
            }

            SetSerializedField(typeof(GameplayAudioMap), map, "entries", entries);
            return new GameplayAudioMapBundle(map, definitions.ToArray());
        }

        private static void ConfigureDefinition(
            SingleAudioDefinition definition,
            AudioClip clip,
            bool loop,
            AudioCategory category)
        {
            SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
            SetSerializedField(typeof(AudioDefinition), definition, "loop", loop);
            SetSerializedField(typeof(AudioDefinition), definition, "category", category);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private sealed class HostAudioIntegrationContext : IDisposable
        {
            private readonly GameplayAudioMapBundle _mapBundle;
            private readonly GameObject _rootObject;

            public HostAudioIntegrationContext(
                GameObject rootObject,
                GameplaySceneHost host,
                AudioRuntimeInstaller installer,
                AudioManager manager,
                GameplayAudioMapBundle mapBundle,
                RecordingAudioSettingsPersistenceStore persistenceStore)
            {
                _rootObject = rootObject;
                Host = host;
                Installer = installer;
                Manager = manager;
                _mapBundle = mapBundle;
                PersistenceStore = persistenceStore;
            }

            public GameplaySceneHost Host { get; }

            public AudioRuntimeInstaller Installer { get; }

            public AudioManager Manager { get; }

            public RecordingAudioSettingsPersistenceStore PersistenceStore { get; }

            public void Dispose()
            {
                _mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class GameplayAudioMapBundle : IDisposable
        {
            private readonly UnityEngine.Object[] _ownedObjects;

            public GameplayAudioMapBundle(GameplayAudioMap map, UnityEngine.Object[] ownedObjects)
            {
                Map = map;
                _ownedObjects = ownedObjects;
            }

            public GameplayAudioMap Map { get; }

            public void Dispose()
            {
                for (var i = 0; i < _ownedObjects.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
                }

                UnityEngine.Object.DestroyImmediate(Map);
            }
        }

        private sealed class RecordingAudioSettingsPersistenceStore : IAudioSettingsPersistenceStore
        {
            public int SaveCallCount { get; private set; }

            public AudioSettingsSnapshot Snapshot { get; private set; } = AudioSettingsSnapshot.Default;

            public AudioSettingsSnapshot Load()
            {
                return Snapshot;
            }

            public void Save(AudioSettingsSnapshot snapshot)
            {
                SaveCallCount++;
                Snapshot = snapshot;
            }
        }
    }
}
