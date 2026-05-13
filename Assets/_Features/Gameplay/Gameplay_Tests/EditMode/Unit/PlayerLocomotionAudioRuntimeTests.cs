using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests
{
    public sealed class PlayerLocomotionAudioRuntimeTests
    {
        [Test]
        public void Controller_PlaySteps_WhileShouldPlayWalkLoopIsTrue()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(map, (PlayerLocomotionAudioCue.WalkStep, scope.CreateBinding(definition)));
            var playbackPort = new FakeGameplayAudioPlaybackPort();
            var controller = new PlayerLocomotionAudioPresentationController(new GameplayPresentationStateStore());
            controller.AttachRuntime(playbackPort, map);

            controller.RefreshSignals(
                new[]
                {
                    new TickPlayerLocomotionPresentationSignal(
                        entityId: 10,
                        shouldPlayWalkLoop: true,
                        moveMotionGeneratedThisTick: false,
                        waitingForNextMoveCadence: false,
                        Direction.Right),
                },
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            controller.Update(0.1f);
            controller.Update(0.1f);

            Assert.That(playbackPort.Play2DCalls, Is.EqualTo(2));
            Assert.That(playbackPort.LastDefinition, Is.SameAs(definition));
            Assert.That(playbackPort.LastContext.OwnerEntityId, Is.EqualTo(10));
            Assert.That(playbackPort.LastContext.DebugTag, Is.EqualTo("WalkStep"));

            controller.RefreshSignals(
                new[]
                {
                    new TickPlayerLocomotionPresentationSignal(
                        entityId: 10,
                        shouldPlayWalkLoop: false,
                        moveMotionGeneratedThisTick: false,
                        waitingForNextMoveCadence: false,
                        Direction.None),
                },
                stepIntervalSeconds: 0.2f);
            controller.Update(0.2f);

            Assert.That(playbackPort.Play2DCalls, Is.EqualTo(2));
        }

        [Test]
        public void Map_RejectsLoopingWalkDefinition()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            SetEntries(
                map,
                (PlayerLocomotionAudioCue.WalkStep,
                    scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: true))));

            var exception = Assert.Throws<InvalidOperationException>(
                () => map.ValidateRequiredCuesOrThrow(PlayerLocomotionAudioCueCatalog.RequiredOneShotV1));

            Assert.That(exception.Message, Does.Contain("only allows one-shot definitions"));
        }

        private static void SetEntries(
            PlayerLocomotionAudioMap map,
            params (PlayerLocomotionAudioCue cue, AudioBinding binding)[] entries)
        {
            var entryType = typeof(PlayerLocomotionAudioMap).GetNestedType("Entry", BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null);
            var array = Array.CreateInstance(entryType, entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = Activator.CreateInstance(entryType);
                entryType.GetField("Cue").SetValue(entry, entries[i].cue);
                entryType.GetField("Binding").SetValue(entry, entries[i].binding);
                array.SetValue(entry, i);
            }

            SetSerializedField(typeof(PlayerLocomotionAudioMap), map, "entries", array);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private sealed class FakeGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort
        {
            public int Play2DCalls { get; private set; }

            public AudioDefinition LastDefinition { get; private set; }

            public AudioPlaybackContext LastContext { get; private set; }

            public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
            {
                Play2DCalls++;
                LastDefinition = definition;
                LastContext = context;
            }

            public void PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                Play2D(definition, context);
            }
        }

        private sealed class TestAssetScope : IDisposable
        {
            private readonly List<UnityEngine.Object> _trackedObjects = new();

            public PlayerLocomotionAudioMap CreateMap()
            {
                var map = Track(ScriptableObject.CreateInstance<PlayerLocomotionAudioMap>());
                map.name = "PlayerLocomotionAudioMap_Test";
                return map;
            }

            public AudioBinding CreateBinding(AudioDefinition definition)
            {
                var binding = new AudioBinding();
                SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
                SetSerializedField(typeof(AudioBinding), binding, "policy", null);
                return binding;
            }

            public SingleAudioDefinition CreateDefinition(AudioCategory category, bool loop)
            {
                var clip = Track(AudioClip.Create($"{category}_{loop}_Clip", 1, 1, 44100, false));
                var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>());
                definition.name = $"{category}_{loop}_Def";
                SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                SetSerializedField(typeof(AudioDefinition), definition, "category", category);
                SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
                SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
                SetSerializedField(typeof(AudioDefinition), definition, "loop", loop);
                return definition;
            }

            public void Dispose()
            {
                for (var i = _trackedObjects.Count - 1; i >= 0; i--)
                {
                    if (_trackedObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_trackedObjects[i]);
                    }
                }
            }

            private T Track<T>(T unityObject) where T : UnityEngine.Object
            {
                _trackedObjects.Add(unityObject);
                return unityObject;
            }
        }
    }
}
