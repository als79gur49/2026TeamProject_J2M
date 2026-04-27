using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class UiAudioSfxContractTests
    {
        [Test]
        public void UiAudioCueId_PublicSurface_IsFrozenToV1()
        {
            Assert.That(
                Enum.GetNames(typeof(UiAudioCueId)),
                Is.EqualTo(new[]
                {
                    "NavigateForward",
                    "NavigateBack",
                    "Confirm",
                    "Cancel",
                    "Select",
                    "Toggle",
                    "AdjustValueCommit",
                }));
        }

        [Test]
        public void UiAudioCueMap_RejectsNullBinding()
        {
            using var scope = new TestAssetScope();
            var cueMap = scope.CreateCueMap();
            var entries = scope.CreateValidEntries();
            entries[0] = scope.CreateEntry(UiAudioCueId.NavigateForward, null);
            scope.SetEntries(cueMap, entries);

            var exception = Assert.Throws<InvalidOperationException>(() => cueMap.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("is missing an AudioBinding"));
        }

        [Test]
        public void UiAudioCueMap_RejectsInvalidCategory()
        {
            using var scope = new TestAssetScope();
            var cueMap = scope.CreateCueMap();
            var entries = scope.CreateValidEntries();
            entries[0] = scope.CreateEntry(
                UiAudioCueId.NavigateForward,
                scope.CreateBinding(scope.CreateDefinition("WrongCategory", AudioCategory.Sfx, loop: false)));
            scope.SetEntries(cueMap, entries);

            var exception = Assert.Throws<InvalidOperationException>(() => cueMap.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("only [Ui] are allowed"));
        }

        [Test]
        public void UiAudioCueMap_RejectsLoopingDefinitions()
        {
            using var scope = new TestAssetScope();
            var cueMap = scope.CreateCueMap();
            var entries = scope.CreateValidEntries();
            entries[0] = scope.CreateEntry(
                UiAudioCueId.NavigateForward,
                scope.CreateBinding(scope.CreateDefinition("LoopingUi", AudioCategory.Ui, loop: true)));
            scope.SetEntries(cueMap, entries);

            var exception = Assert.Throws<InvalidOperationException>(() => cueMap.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("only allows one-shot definitions"));
        }

        [Test]
        public void UiAudioCueMap_RejectsNonEmptyAttachmentSlot()
        {
            using var scope = new TestAssetScope();
            var cueMap = scope.CreateCueMap();
            var entries = scope.CreateValidEntries();
            entries[0] = scope.CreateEntry(
                UiAudioCueId.NavigateForward,
                scope.CreateBinding(
                    scope.CreateDefinition("AttachedUi", AudioCategory.Ui, loop: false),
                    AudioAttachmentSlot.FromId("HudButton")));
            scope.SetEntries(cueMap, entries);

            var exception = Assert.Throws<InvalidOperationException>(() => cueMap.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("Play2D only"));
            Assert.That(exception.Message, Does.Contain("must remain empty"));
        }

        [Test]
        public void UiAudioCueMap_RejectsNonNullPolicy()
        {
            using var scope = new TestAssetScope();
            var cueMap = scope.CreateCueMap();
            var entries = scope.CreateValidEntries();
            entries[0] = scope.CreateEntry(
                UiAudioCueId.NavigateForward,
                scope.CreateBinding(
                    scope.CreateDefinition("PolicyUi", AudioCategory.Ui, loop: false),
                    default,
                    new DummyAudioPlaybackPolicy()));
            scope.SetEntries(cueMap, entries);

            var exception = Assert.Throws<InvalidOperationException>(() => cueMap.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("AudioBinding.Policy"));
            Assert.That(exception.Message, Does.Contain("must remain null"));
        }

        [Test]
        public void UiAudioCueMap_RejectsDuplicateCueEntries()
        {
            using var scope = new TestAssetScope();
            var cueMap = scope.CreateCueMap();
            var entries = scope.CreateValidEntries();
            entries[1] = scope.CreateEntry(
                UiAudioCueId.NavigateForward,
                scope.CreateBinding(scope.CreateDefinition("DuplicateForward", AudioCategory.Ui, loop: false)));
            scope.SetEntries(cueMap, entries);

            var exception = Assert.Throws<InvalidOperationException>(() => cueMap.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("duplicate cue entry"));
        }

        [Test]
        public void UiAudioCueMap_CanonicalAuthoredMap_IsExplicitAndComplete()
        {
            var cueMap = UiTestPrefabAssetUtility.LoadUiAudioCueMap();

            cueMap.ValidateOrThrow();

            Assert.That(cueMap.Entries.Count, Is.EqualTo(7));
        }

        [Test]
        public void UiAudioPortAdapter_PlaysTwoDOnly_OnUiLeafChannel_WithStableDebugTag()
        {
            using var scope = new TestAssetScope();
            var cueMap = scope.CreateCueMap();
            scope.SetEntries(cueMap, scope.CreateValidEntries());
            var audioService = new RecordingAudioService();
            var adapter = new UiAudioPortAdapter(audioService, cueMap);

            adapter.Play(UiAudioCueId.Toggle);

            Assert.That(audioService.TwoDCalls, Has.Count.EqualTo(1));
            Assert.That(audioService.AttachedCallCount, Is.EqualTo(0));
            Assert.That(audioService.BgmCallCount, Is.EqualTo(0));
            Assert.That(audioService.TwoDCalls[0].Context.DebugTag, Is.EqualTo("UI/Toggle"));
            Assert.That(
                AudioDefinitionCategoryRules.ToLeafChannel(
                    audioService.TwoDCalls[0].Definition.Category,
                    "UiAudioPortAdapter test"),
                Is.EqualTo(AudioChannel.Ui));
        }

        [Serializable]
        private sealed class DummyAudioPlaybackPolicy : AudioPlaybackPolicy
        {
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public readonly List<(AudioDefinition Definition, AudioPlaybackContext Context)> TwoDCalls = new();

            public int AttachedCallCount { get; private set; }

            public int BgmCallCount { get; private set; }

            public AudioPlaybackHandle Play2D(AudioDefinition definition, in AudioPlaybackContext context = default)
            {
                TwoDCalls.Add((definition, context));
                return default;
            }

            public AudioPlaybackHandle PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context = default)
            {
                AttachedCallCount++;
                return default;
            }

            public AudioPlaybackHandle PlayBgm(AudioBgmPlaybackRequest request)
            {
                BgmCallCount++;
                return default;
            }

            public void Stop(AudioPlaybackHandle handle)
            {
            }

            public void StopBgm(AudioBgmStopRequest request)
            {
            }
        }

        private sealed class TestAssetScope : IDisposable
        {
            private readonly List<UnityEngine.Object> _trackedObjects = new();

            public UiAudioCueMap CreateCueMap()
            {
                var cueMap = Track(ScriptableObject.CreateInstance<UiAudioCueMap>());
                cueMap.name = "UiAudioCueMap_Test";
                return cueMap;
            }

            public UiAudioCueMap.Entry[] CreateValidEntries()
            {
                var cueIds = (UiAudioCueId[])Enum.GetValues(typeof(UiAudioCueId));
                var entries = new UiAudioCueMap.Entry[cueIds.Length];
                for (var i = 0; i < cueIds.Length; i++)
                {
                    entries[i] = CreateEntry(
                        cueIds[i],
                        CreateBinding(CreateDefinition($"Ui_{cueIds[i]}", AudioCategory.Ui, loop: false)));
                }

                return entries;
            }

            public UiAudioCueMap.Entry CreateEntry(UiAudioCueId cueId, AudioBinding binding)
            {
                object boxed = new UiAudioCueMap.Entry();
                SetPrivateField(boxed, "cueId", cueId);
                SetPrivateField(boxed, "binding", binding);
                return (UiAudioCueMap.Entry)boxed;
            }

            public AudioBinding CreateBinding(
                AudioDefinition definition,
                AudioAttachmentSlot attachmentSlot = default,
                AudioPlaybackPolicy policy = null)
            {
                var binding = new AudioBinding();
                SetPrivateField(binding, "definition", definition);
                SetPrivateField(binding, "attachmentSlot", attachmentSlot);
                SetPrivateField(binding, "policy", policy);
                return binding;
            }

            public SingleAudioDefinition CreateDefinition(string name, AudioCategory category, bool loop)
            {
                var clip = Track(AudioClip.Create($"{name}_Clip", 1, 1, 44100, false));
                var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>());
                definition.name = name;
                SetPrivateField(definition, "clip", clip);
                SetPrivateField(typeof(AudioDefinition), definition, "category", category);
                SetPrivateField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
                SetPrivateField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
                SetPrivateField(typeof(AudioDefinition), definition, "loop", loop);
                return definition;
            }

            public void SetEntries(UiAudioCueMap cueMap, UiAudioCueMap.Entry[] entries)
            {
                SetPrivateField(typeof(UiAudioCueMap), cueMap, "entries", entries);
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

            private static void SetPrivateField(object instance, string fieldName, object value)
            {
                SetPrivateField(instance.GetType(), instance, fieldName, value);
            }

            private static void SetPrivateField(Type type, object instance, string fieldName, object value)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, fieldName);
                field.SetValue(instance, value);
            }
        }
    }
}
