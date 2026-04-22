using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
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

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_ActionAudioProfile_PlaybackReachesAudioService()
        {
            var trackedObjects = new List<UnityEngine.Object>();
            var actionProfile = CreateActionAudioProfile(trackedObjects);
            var playerEntity = CreatePlayerEntityState();
            var context = CreateActionAudioHostContext(
                nameof(GameplaySceneHost_ActionAudioProfile_PlaybackReachesAudioService),
                new RuntimeActionAudioViewFactory(actionProfile),
                playerEntity);
            try
            {
                context.Host.Presenter.Present(CreateTickResult(
                    CreatePlayerActionPresentationData(
                        new TickPlayerActionPresentationSignal(
                            entityId: playerEntity.entityId,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 1,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false)),
                    new[] { playerEntity }));
                var snapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Sfx));
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                // One-shot action SFX may naturally complete before the next frame in batchmode.
                yield return null;
            }
            finally
            {
                context.Dispose();
                for (var i = trackedObjects.Count - 1; i >= 0; i--)
                {
                    if (trackedObjects[i] != null)
                    {
                        UnityEngine.Object.Destroy(trackedObjects[i]);
                    }
                }
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

        private static HostAudioIntegrationContext CreateActionAudioHostContext(
            string rootName,
            IGameplayEntityViewFactory viewFactory,
            EntityState playerEntity)
        {
            var hostObject = new GameObject(rootName);
            hostObject.SetActive(false);

            var installer = hostObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);

            var runtimeRootObject = new GameObject("AudioRuntimeRoot");
            runtimeRootObject.transform.SetParent(hostObject.transform, worldPositionStays: false);
            var runtimeRoot = runtimeRootObject.AddComponent<AudioRuntimeRoot>();
            var manager = runtimeRootObject.AddComponent<AudioManager>();

            var host = hostObject.AddComponent<GameplaySceneHost>();
            var mapBundle = CreateGameplayAudioMapBundle();

            hostObject.SetActive(true);
            host.Initialize(new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                InitialEntities = new[] { playerEntity },
                InitialTerrain = GameplayTerrainData.Empty,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                GameplayAudioMap = mapBundle.Map,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
                ViewFactory = viewFactory,
            });

            Assert.That(installer.RuntimeRoot, Is.SameAs(runtimeRoot));
            return new HostAudioIntegrationContext(hostObject, host, installer, manager, mapBundle, new RecordingAudioSettingsPersistenceStore());
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

        private static TickPresentationData CreatePlayerActionPresentationData(params TickPlayerActionPresentationSignal[] playerActionSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals ?? Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
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

        private static GameplayActionAudioProfile CreateActionAudioProfile(ICollection<UnityEngine.Object> trackedObjects)
        {
            var profile = Track(ScriptableObject.CreateInstance<GameplayActionAudioProfile>(), trackedObjects);
            var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>(), trackedObjects);
            var clip = Track(AudioClip.Create("ActionAudio", 4410, 1, 44100, false), trackedObjects);
            definition.name = "ActionAudioPushWindup";
            ConfigureDefinition(definition, clip, loop: false, AudioCategory.Sfx);

            var binding = new AudioBinding();
            SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
            SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
            SetSerializedField(typeof(AudioBinding), binding, "policy", null);

            var entries = new[]
            {
                new GameplayActionAudioEntry
                {
                    Action = GameplayActionKind.Push,
                    Moment = GameplayActionAudioMoment.Windup,
                    Binding = binding,
                    IsOptional = false,
                },
            };
            SetSerializedField(typeof(GameplayActionAudioProfile), profile, "entries", entries);
            return profile;
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

        private static EntityState CreatePlayerEntityState()
        {
            return new EntityState
            {
                entityId = 10,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static T Track<T>(T unityObject, ICollection<UnityEngine.Object> trackedObjects)
            where T : UnityEngine.Object
        {
            trackedObjects.Add(unityObject);
            return unityObject;
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

        private sealed class RuntimeActionAudioViewFactory : IGameplayEntityViewFactory
        {
            private readonly GameplayActionAudioProfile _profile;

            public RuntimeActionAudioViewFactory(GameplayActionAudioProfile profile)
            {
                _profile = profile;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (entity.unitRole == UnitRole.Player)
                {
                    var authoring = viewObject.AddComponent<GameplayActionAudioAuthoring>();
                    SetSerializedField(typeof(GameplayActionAudioAuthoring), authoring, "profile", _profile);
                }

                return view;
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
