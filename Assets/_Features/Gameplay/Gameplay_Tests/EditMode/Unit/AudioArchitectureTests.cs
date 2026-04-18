using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class AudioArchitectureTests
    {
        [Test]
        [Category("Extended")]
        public void AudioDefinition_OnValidate_AndResolve_ShareReservedMasterCategoryRule()
        {
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var clip = AudioClip.Create("ReservedMaster", 4410, 1, 44100, false);
            try
            {
                SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Master);
                SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);

                Assert.That(
                    AudioDefinitionCategoryRules.TryGetReservedCategoryMessage(
                        AudioCategory.Master,
                        $"AudioDefinition '{definition.name}'",
                        out var expectedMessage),
                    Is.True);

                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(expectedMessage)));
                InvokeOnValidate(definition);

                var exception = Assert.Throws<InvalidOperationException>(() => definition.Resolve(default));
                Assert.That(exception.Message, Is.EqualTo(expectedMessage));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Extended")]
        public void SharedAudioAssembly_PublicSurface_IsTwoDimensionalOnly()
        {
            var methods = typeof(IAudioService)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => !method.IsSpecialName)
                .OrderBy(method => method.Name)
                .ToArray();
            var methodNames = methods
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(
                methodNames,
                Is.EqualTo(new[]
                {
                    "Play2D",
                    "PlayAttached",
                    "PlayBgm",
                    "Stop",
                    "StopBgm",
                }));

            Assert.That(methodNames, Does.Not.Contain("Play3D"));
            Assert.That(typeof(IAudioService).GetMethod(nameof(IAudioService.PlayBgm))?.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(typeof(IAudioService).GetMethod(nameof(IAudioService.Stop))?.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(typeof(IAudioService).GetMethod(nameof(IAudioService.StopBgm))?.GetParameters(), Has.Length.EqualTo(0));
            Assert.That(typeof(AudioPlaybackHandle).GetMethod(nameof(AudioPlaybackHandle.Stop))?.GetParameters(), Has.Length.EqualTo(0));

            var contextProperties = typeof(AudioPlaybackContext)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(
                contextProperties,
                Is.EqualTo(new[]
                {
                    "DebugTag",
                    "OwnerEntityId",
                    "PitchMultiplier",
                    "VolumeMultiplier",
                }));

            var playbackDataProperties = typeof(AudioPlaybackData)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(
                playbackDataProperties,
                Is.EqualTo(new[]
                {
                    "Category",
                    "Clip",
                    "Loop",
                    "Pitch",
                    "Volume",
                }));

            var deprecatedTokens = new[]
            {
                "Spatial",
                "Distance",
                "Attenuation",
                "Follow",
                "Position",
            };
            var leakedNames = typeof(AudioDefinition).Assembly
                .GetExportedTypes()
                .SelectMany(GetPublicNames)
                .Where(name => deprecatedTokens.Any(token => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                .Distinct()
                .ToArray();

            Assert.That(leakedNames, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void SharedAudioAssembly_DoesNotReferenceGameplayOrUiMappedSeamAssemblies()
        {
            var references = typeof(IAudioService).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.UIAccess"));
            Assert.That(references, Does.Not.Contain("Game.Feature.UI.Application"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioAssembly_DoesNotReferenceUiMappedSeamAssemblies()
        {
            var references = typeof(GameplayAudioPresenter).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.UIAccess"));
            Assert.That(references, Does.Not.Contain("Game.Feature.UI.Application"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAssembly_DoesNotReferenceSharedAudioAssembly()
        {
            var references = typeof(TickResult).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(typeof(IAudioService).Assembly.GetName().Name));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_ResolveOrThrow_FailsFastOnMissingSemantic()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => map.ResolveOrThrow("player.move"));
                StringAssert.Contains("missing gameplay audio semantic 'player.move'", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_ValidateOrThrow_FailsOnDuplicateSemantic()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                SetEntriesDirect(
                    map,
                    ("player.move", definition, default),
                    ("player.move", definition, default));

                var exception = Assert.Throws<InvalidOperationException>(() => map.ValidateOrThrow());
                StringAssert.Contains("duplicate gameplay audio semantic 'player.move'", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_ValidateOrThrow_FailsOnEmptySemantic()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                SetEntriesDirect(map, (string.Empty, definition, default));

                var exception = Assert.Throws<InvalidOperationException>(() => map.ValidateOrThrow());
                StringAssert.Contains("contains an empty gameplay audio semantic", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioPresenter_ProjectsPresentationData_WithoutUiMappedSeamReuse()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var oneShotDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var attachedDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var ownerObject = new GameObject("AudioPresenterOwner");
            var owner = ownerObject.AddComponent<TestAudioOwner>();

            try
            {
                PopulateMap(
                    map,
                    ("player.move", oneShotDefinition, default),
                    ("player.charge", attachedDefinition, AudioAttachmentSlot.FromId("charge")));

                var projector = new RecordingProjector(new[]
                {
                    new GameplayAudioCue("player.move", new AudioPlaybackContext(debugTag: "oneshot")),
                    new GameplayAudioCue(
                        "player.charge",
                        owner,
                        default,
                        new AudioPlaybackContext(debugTag: "attached")),
                });
                var service = new RecordingAudioService();
                var presenter = new GameplayAudioPresenter(service, map, projector);
                var result = new TickResult(1, new[] { TickPhase.Plan }, Array.Empty<string>());

                presenter.Present(result);

                Assert.That(projector.LastPresentationData, Is.SameAs(TickPresentationData.Empty));
                Assert.That(projector.LastFinalTopology, Is.EqualTo(result.FinalTopology));
                Assert.That(service.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(service.AttachedCalls, Has.Count.EqualTo(1));
                Assert.That(service.TwoDCalls[0].Definition, Is.SameAs(oneShotDefinition));
                Assert.That(service.AttachedCalls[0].Definition, Is.SameAs(attachedDefinition));
                Assert.That(service.AttachedCalls[0].Owner, Is.SameAs(owner));
                Assert.That(service.AttachedCalls[0].Slot.Id, Is.EqualTo("charge"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(oneShotDefinition);
                UnityEngine.Object.DestroyImmediate(attachedDefinition);
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioValidation_UsesSharedBindingRule_ForMissingDefinition()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            const string semanticId = "player.move";
            try
            {
                var binding = CreateBinding(null, default, null);
                SetBindingsDirect(map, (semanticId, binding));
                var expectedMessage = GetSingleBindingValidationError(binding, map.name, semanticId);

                var bindingException = Assert.Throws<InvalidOperationException>(
                    () => binding.ValidateOrThrow(map.name, semanticId));
                Assert.That(bindingException.Message, Is.EqualTo(expectedMessage));

                var validateException = Assert.Throws<InvalidOperationException>(() => map.ValidateOrThrow());
                Assert.That(validateException.Message, Is.EqualTo(expectedMessage));

                var resolveException = Assert.Throws<InvalidOperationException>(() => map.ResolveOrThrow(semanticId));
                Assert.That(resolveException.Message, Is.EqualTo(expectedMessage));

                var presenterException = Assert.Throws<InvalidOperationException>(
                    () => new GameplayAudioPresenter(new RecordingAudioService(), map, new RecordingProjector(Array.Empty<GameplayAudioCue>())));
                Assert.That(presenterException.Message, Is.EqualTo(expectedMessage));

                LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(expectedMessage)));
                InvokeOnValidate(map);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_OnValidate_LogsAuthoringError_ForDuplicateSemantic()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                SetEntriesDirect(
                    map,
                    ("player.move", definition, default),
                    ("player.move", definition, default));

                LogAssert.Expect(LogType.Error, new Regex("duplicate gameplay audio semantic 'player\\.move'"));
                InvokeOnValidate(map);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioValidation_UsesSharedBindingRule_ForUnsupportedPolicy()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            const string semanticId = "player.move";
            try
            {
                var binding = CreateBinding(definition, default, new TestAudioPolicy());
                SetBindingsDirect(map, (semanticId, binding));
                var expectedMessage = GetSingleBindingValidationError(binding, map.name, semanticId);

                var bindingException = Assert.Throws<InvalidOperationException>(
                    () => binding.ValidateOrThrow(map.name, semanticId));
                Assert.That(bindingException.Message, Is.EqualTo(expectedMessage));

                var validateException = Assert.Throws<InvalidOperationException>(() => map.ValidateOrThrow());
                Assert.That(validateException.Message, Is.EqualTo(expectedMessage));

                var resolveException = Assert.Throws<InvalidOperationException>(() => map.ResolveOrThrow(semanticId));
                Assert.That(resolveException.Message, Is.EqualTo(expectedMessage));

                var presenterException = Assert.Throws<InvalidOperationException>(
                    () => new GameplayAudioPresenter(new RecordingAudioService(), map, new RecordingProjector(Array.Empty<GameplayAudioCue>())));
                Assert.That(presenterException.Message, Is.EqualTo(expectedMessage));

                LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(expectedMessage)));
                InvokeOnValidate(map);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static IEnumerable<string> GetPublicNames(Type type)
        {
            yield return type.Name;

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                yield return property.Name;
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!method.IsSpecialName)
                {
                    yield return method.Name;
                }
            }
        }

        private static void PopulateMap(
            GameplayAudioMap map,
            params (string semanticId, AudioDefinition definition, AudioAttachmentSlot slot)[] entries)
        {
            PopulateMap(
                map,
                entries.Select(entry => (entry.semanticId, entry.definition, entry.slot, (AudioPlaybackPolicy)null)).ToArray());
        }

        private static void PopulateMap(
            GameplayAudioMap map,
            params (string semanticId, AudioDefinition definition, AudioAttachmentSlot slot, AudioPlaybackPolicy policy)[] entries)
        {
            var serializedObject = new SerializedObject(map);
            var entriesProperty = serializedObject.FindProperty("entries");
            entriesProperty.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var element = entriesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("SemanticId").stringValue = entries[i].semanticId;
                var binding = element.FindPropertyRelative("Binding");
                binding.FindPropertyRelative("definition").objectReferenceValue = entries[i].definition;
                binding.FindPropertyRelative("attachmentSlot").FindPropertyRelative("id").stringValue = entries[i].slot.Id;
                binding.FindPropertyRelative("policy").managedReferenceValue = entries[i].policy;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEntriesDirect(
            GameplayAudioMap map,
            params (string semanticId, AudioDefinition definition, AudioAttachmentSlot slot)[] entries)
        {
            SetEntriesDirect(
                map,
                entries.Select(entry => (entry.semanticId, entry.definition, entry.slot, (AudioPlaybackPolicy)null)).ToArray());
        }

        private static void SetEntriesDirect(
            GameplayAudioMap map,
            params (string semanticId, AudioDefinition definition, AudioAttachmentSlot slot, AudioPlaybackPolicy policy)[] entries)
        {
            var entryType = typeof(GameplayAudioMap).GetNestedType("Entry", BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null, "Missing GameplayAudioMap.Entry type.");

            var semanticIdField = entryType.GetField("SemanticId", BindingFlags.Instance | BindingFlags.Public);
            var bindingField = entryType.GetField("Binding", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(semanticIdField, Is.Not.Null);
            Assert.That(bindingField, Is.Not.Null);

            var entryArray = Array.CreateInstance(entryType, entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entryValue = Activator.CreateInstance(entryType);
                semanticIdField.SetValue(entryValue, entries[i].semanticId);
                bindingField.SetValue(entryValue, CreateBinding(entries[i].definition, entries[i].slot, entries[i].policy));
                entryArray.SetValue(entryValue, i);
            }

            SetSerializedField(typeof(GameplayAudioMap), map, "entries", entryArray);
        }

        private static void SetBindingsDirect(
            GameplayAudioMap map,
            params (string semanticId, AudioBinding binding)[] entries)
        {
            var entryType = typeof(GameplayAudioMap).GetNestedType("Entry", BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null, "Missing GameplayAudioMap.Entry type.");

            var semanticIdField = entryType.GetField("SemanticId", BindingFlags.Instance | BindingFlags.Public);
            var bindingField = entryType.GetField("Binding", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(semanticIdField, Is.Not.Null);
            Assert.That(bindingField, Is.Not.Null);

            var entryArray = Array.CreateInstance(entryType, entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entryValue = Activator.CreateInstance(entryType);
                semanticIdField.SetValue(entryValue, entries[i].semanticId);
                bindingField.SetValue(entryValue, entries[i].binding);
                entryArray.SetValue(entryValue, i);
            }

            SetSerializedField(typeof(GameplayAudioMap), map, "entries", entryArray);
        }

        private static AudioBinding CreateBinding(
            AudioDefinition definition,
            AudioAttachmentSlot slot,
            AudioPlaybackPolicy policy)
        {
            var binding = new AudioBinding();
            SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
            SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", slot);
            SetSerializedField(typeof(AudioBinding), binding, "policy", policy);
            return binding;
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private static void InvokeOnValidate(ScriptableObject target)
        {
            var method = target.GetType().GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing OnValidate on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private static string GetSingleBindingValidationError(
            AudioBinding binding,
            string ownerDescription,
            string semanticId)
        {
            var method = typeof(AudioBinding).GetMethod("AppendValidationErrors", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing AudioBinding.AppendValidationErrors.");

            var errors = new List<string>();
            method.Invoke(binding, new object[] { ownerDescription, semanticId, errors });

            Assert.That(errors, Has.Count.EqualTo(1), "Expected exactly one binding validation error.");
            return errors[0];
        }

        private sealed class RecordingProjector : IGameplayAudioCueProjector
        {
            private readonly IReadOnlyList<GameplayAudioCue> cues;

            public RecordingProjector(IReadOnlyList<GameplayAudioCue> cues)
            {
                this.cues = cues;
            }

            public TickPresentationData LastPresentationData { get; private set; }

            public Game.Feature.Gameplay.BoardState.CubeTopologyState LastFinalTopology { get; private set; }

            public IReadOnlyList<GameplayAudioCue> Project(
                TickPresentationData presentationData,
                Game.Feature.Gameplay.BoardState.CubeTopologyState finalTopology)
            {
                LastPresentationData = presentationData;
                LastFinalTopology = finalTopology;
                return cues;
            }
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public readonly List<(AudioDefinition Definition, AudioPlaybackContext Context)> TwoDCalls = new();
            public readonly List<(AudioDefinition Definition, Component Owner, AudioAttachmentSlot Slot, AudioPlaybackContext Context)> AttachedCalls = new();

            public AudioPlaybackHandle Play2D(AudioDefinition definition, in AudioPlaybackContext context = default)
            {
                TwoDCalls.Add((definition, context));
                return AudioPlaybackHandle.Invalid;
            }

            public AudioPlaybackHandle PlayAttached(AudioDefinition definition, Component owner, AudioAttachmentSlot slot, in AudioPlaybackContext context = default)
            {
                AttachedCalls.Add((definition, owner, slot, context));
                return AudioPlaybackHandle.Invalid;
            }

            public AudioPlaybackHandle PlayBgm(AudioDefinition definition)
            {
                return AudioPlaybackHandle.Invalid;
            }

            public void Stop(AudioPlaybackHandle handle)
            {
            }

            public void StopBgm()
            {
            }
        }

        private sealed class TestAudioOwner : MonoBehaviour
        {
        }

        [Serializable]
        private sealed class TestAudioPolicy : AudioPlaybackPolicy
        {
        }
    }
}
