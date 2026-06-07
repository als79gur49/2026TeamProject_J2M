using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests
{
    public sealed class TopologyAudioRuntimeTests
    {
        private const string GameplayAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_CampaignV1.asset";
        private const string BlockAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_BlockAudio/Maps/BlockAudioMap_PlayerSounds.asset";
        private const string PlayerLocomotionAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_PlayerLocomotionAudio/Maps/PlayerLocomotionAudioMap_PlayerSounds.asset";
        private const string GravityFieldAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Maps/GravityFieldAudioMap_ObjectSounds.asset";
        private const string TileFeatureAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Maps/TileFeatureAudioMap_ObjectSounds.asset";

        [Test]
        [Category("Extended")]
        public void Planner_BuildsMapRotateStartedCue_ForTopologyMotion()
        {
            var planner = new TopologyAudioRequestPlanner();
            var result = CreateTickResult(
                7,
                CreatePresentationData(new TickTopologyMotion(
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward)));

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TopologyAudioCue.MapRotateStarted));
            Assert.That(requests[0].SequenceId, Is.Not.Zero);
            Assert.That(requests[0].Context.OwnerEntityId, Is.Null);
            Assert.That(requests[0].Context.DebugTag, Is.EqualTo(nameof(TopologyAudioCue.MapRotateStarted)));
        }

        [Test]
        [Category("Extended")]
        public void Planner_IgnoresMissingOrNoneTopologyMotion()
        {
            var planner = new TopologyAudioRequestPlanner();

            Assert.That(
                planner.BuildRequests(CreateTickResult(1, CreatePresentationData(null))),
                Is.Empty);
            Assert.That(
                planner.BuildRequests(CreateTickResult(
                    1,
                    CreatePresentationData(new TickTopologyMotion(
                        new CubeTopologyState(FaceId.Floor),
                        new CubeTopologyState(FaceId.Floor),
                        CubeRotationKind.None)))),
                Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void PresentationController_PlaysMapRotateStartedOncePerSequence_As2D()
        {
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var mapBundle = CreateTopologyAudioMap();
            try
            {
                var controller = new TopologyAudioPresentationController();
                var request = new TopologyAudioRequest(
                    TopologyAudioCue.MapRotateStarted,
                    sequenceId: 100,
                    new AudioPlaybackContext(debugTag: nameof(TopologyAudioCue.MapRotateStarted)));

                controller.AttachRuntime(playbackPort, mapBundle.Map);
                controller.ReplacePendingPlan(new[] { request, request });
                controller.PlayPlannedAudio();
                controller.ReplacePendingPlan(new[] { request });
                controller.PlayPlannedAudio();

                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo(nameof(TopologyAudioCue.MapRotateStarted)));
                Assert.That(playbackPort.AttachedCalls, Is.Empty);
            }
            finally
            {
                mapBundle.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyAudioMap_ValidatesRequiredCueAndOneShotSfxPolicy()
        {
            var validBundle = CreateTopologyAudioMap();
            var missingMap = ScriptableObject.CreateInstance<TopologyAudioMap>();
            var duplicateBundle = CreateTopologyAudioMap(
                (TopologyAudioCue.MapRotateStarted, CreateBinding(CreateDefinition(AudioCategory.Sfx))),
                (TopologyAudioCue.MapRotateStarted, CreateBinding(CreateDefinition(AudioCategory.Sfx))));
            var nullBindingMap = CreateTopologyAudioMapWithNullBinding();
            var bgmBundle = CreateTopologyAudioMap(
                (TopologyAudioCue.MapRotateStarted, CreateBinding(CreateDefinition(AudioCategory.Bgm))));
            var loopBundle = CreateTopologyAudioMap(
                (TopologyAudioCue.MapRotateStarted, CreateBinding(CreateDefinition(AudioCategory.Sfx, loop: true))));
            try
            {
                Assert.DoesNotThrow(
                    () => validBundle.Map.ValidateRequiredCuesOrThrow(TopologyAudioCueCatalog.RequiredOneShotV1));
                Assert.That(
                    Assert.Throws<InvalidOperationException>(
                        () => missingMap.ValidateRequiredCuesOrThrow(TopologyAudioCueCatalog.RequiredOneShotV1))?.Message,
                    Does.Contain("missing required topology audio cues: MapRotateStarted"));
                Assert.That(
                    Assert.Throws<InvalidOperationException>(() => duplicateBundle.Map.ValidateOrThrow())?.Message,
                    Does.Contain("duplicate topology audio cue 'MapRotateStarted'"));
                Assert.That(
                    Assert.Throws<InvalidOperationException>(() => nullBindingMap.ValidateOrThrow())?.Message,
                    Does.Contain("is missing an AudioBinding"));
                Assert.That(
                    Assert.Throws<InvalidOperationException>(() => bgmBundle.Map.ValidateOrThrow())?.Message,
                    Does.Contain("only [Sfx] are allowed"));
                Assert.That(
                    Assert.Throws<InvalidOperationException>(() => loopBundle.Map.ValidateOrThrow())?.Message,
                    Does.Contain("only allows one-shot definitions"));
            }
            finally
            {
                validBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(missingMap);
                duplicateBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(nullBindingMap);
                bgmBundle.Dispose();
                loopBundle.Dispose();
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_RequiresCoLocatedAudioRuntimeInstaller_WhenGameplayPresentationAudioConfigIsAssigned()
        {
            var hostObject = new GameObject(nameof(GameplaySceneHost_Initialize_RequiresCoLocatedAudioRuntimeInstaller_WhenGameplayPresentationAudioConfigIsAssigned));
            var mapBundle = CreateTopologyAudioMap();
            var audioConfig = CreateGameplayPresentationAudioConfig(mapBundle.Map);
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(CreateHostConfiguration(audioConfig)));

                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplaySceneHost requires a co-located AudioRuntimeInstaller on the canonical host root when GameplayPresentationAudioConfig is assigned."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioConfig);
                mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        private static TickPresentationData CreatePresentationData(TickTopologyMotion? topologyMotion)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion,
                Array.Empty<TickVisibilityChange>());
        }

        private static TickResult CreateTickResult(int tickIndex, TickPresentationData presentationData)
        {
            return new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static GameplaySceneHostConfiguration CreateHostConfiguration(GameplayPresentationAudioConfig audioConfig)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                InitialEntities = Array.Empty<EntityState>(),
                InitialTerrain = GameplayTerrainData.Empty,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                GameplayPresentationAudioConfig = audioConfig,
            };
        }

        private static GameplayPresentationAudioConfig CreateGameplayPresentationAudioConfig(
            TopologyAudioMap topologyAudioMap)
        {
            var config = ScriptableObject.CreateInstance<GameplayPresentationAudioConfig>();
            config.name = "GameplayPresentationAudioConfig_TopologyTest";
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "gameplayAudioMap",
                LoadCanonical<GameplayAudioMap>(GameplayAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "blockAudioMap",
                LoadCanonical<BlockAudioMap>(BlockAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "playerLocomotionAudioMap",
                LoadCanonical<PlayerLocomotionAudioMap>(PlayerLocomotionAudioMapPath));
            SetSerializedField(typeof(GameplayPresentationAudioConfig), config, "topologyAudioMap", topologyAudioMap);
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "gravityFieldAudioMap",
                LoadCanonical<GravityFieldAudioMap>(GravityFieldAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "tileFeatureAudioMap",
                LoadCanonical<TileFeatureAudioMap>(TileFeatureAudioMapPath));
            return config;
        }

        private static T LoadCanonical<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static TopologyAudioMapBundle CreateTopologyAudioMap(
            params (TopologyAudioCue Cue, AudioBinding Binding)[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                entries = new[]
                {
                    (TopologyAudioCue.MapRotateStarted, CreateBinding(CreateDefinition(AudioCategory.Sfx))),
                };
            }

            var map = ScriptableObject.CreateInstance<TopologyAudioMap>();
            SetEntries(map, entries);
            var trackedObjects = new List<UnityEngine.Object>();
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Binding?.Definition != null)
                {
                    trackedObjects.Add(entries[i].Binding.Definition);
                }
            }

            return new TopologyAudioMapBundle(map, trackedObjects.ToArray());
        }

        private static TopologyAudioMap CreateTopologyAudioMapWithNullBinding()
        {
            var map = ScriptableObject.CreateInstance<TopologyAudioMap>();
            SetEntries(map, (TopologyAudioCue.MapRotateStarted, null));
            return map;
        }

        private static void SetEntries(
            TopologyAudioMap map,
            params (TopologyAudioCue Cue, AudioBinding Binding)[] entries)
        {
            var entryType = typeof(TopologyAudioMap).GetNestedType("Entry", BindingFlags.NonPublic);
            var cueField = entryType.GetField("Cue", BindingFlags.Public | BindingFlags.Instance);
            var bindingField = entryType.GetField("Binding", BindingFlags.Public | BindingFlags.Instance);
            var array = Array.CreateInstance(entryType, entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = Activator.CreateInstance(entryType);
                cueField.SetValue(entry, entries[i].Cue);
                bindingField.SetValue(entry, entries[i].Binding);
                array.SetValue(entry, i);
            }

            SetSerializedField(typeof(TopologyAudioMap), map, "entries", array);
        }

        private static AudioBinding CreateBinding(AudioDefinition definition)
        {
            var binding = new AudioBinding();
            SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
            SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
            SetSerializedField(typeof(AudioBinding), binding, "policy", null);
            return binding;
        }

        private static SingleAudioDefinition CreateDefinition(AudioCategory category, bool loop = false)
        {
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            definition.name = $"{category}_TopologyAudio_TestDefinition";
            SetSerializedField(typeof(AudioDefinition), definition, "category", category);
            SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
            SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
            SetSerializedField(typeof(AudioDefinition), definition, "loop", loop);
            return definition;
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(declaringType.FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private sealed class TopologyAudioMapBundle : IDisposable
        {
            private readonly UnityEngine.Object[] _definitions;

            public TopologyAudioMapBundle(TopologyAudioMap map, UnityEngine.Object[] definitions)
            {
                Map = map;
                _definitions = definitions;
            }

            public TopologyAudioMap Map { get; }

            public void Dispose()
            {
                for (var i = 0; i < _definitions.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(_definitions[i]);
                }

                UnityEngine.Object.DestroyImmediate(Map);
            }
        }

        private sealed class RecordingGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort
        {
            public readonly List<(AudioDefinition Definition, AudioPlaybackContext Context)> TwoDCalls = new();
            public readonly List<(AudioDefinition Definition, Component Owner, AudioAttachmentSlot Slot, AudioPlaybackContext Context)> AttachedCalls = new();

            public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
            {
                TwoDCalls.Add((definition, context));
            }

            public void PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                AttachedCalls.Add((definition, owner, slot, context));
            }
        }
    }
}
