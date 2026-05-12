using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GravityFieldAudioRuntimeTests
    {
        [Test]
        [Category("Core")]
        public void GravityFieldAudioRequestPlanner_MapsActivatedExpiredLockedBox()
        {
            var planner = new GravityFieldAudioRequestPlanner();
            var lockedBoxPayload = new GravityFieldLockedBoxPayload(
                30,
                20,
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0));
            var requests = planner.BuildRequests(new[]
            {
                new GravityFieldPresentationRequest(
                    GravityFieldPresentationRequestKind.Activated,
                    30,
                    new SurfaceCell(FaceId.Floor, 0, 0)),
                new GravityFieldPresentationRequest(
                    GravityFieldPresentationRequestKind.Expired,
                    31,
                    new SurfaceCell(FaceId.Floor, 1, 0)),
                new GravityFieldPresentationRequest(
                    GravityFieldPresentationRequestKind.LockedBox,
                    30,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    targetEntityId: 20,
                    lockedBoxPayload: lockedBoxPayload),
            });

            Assert.That(requests, Has.Count.EqualTo(3));
            Assert.That(requests[0].Cue, Is.EqualTo(GravityFieldAudioCue.Activated));
            Assert.That(requests[1].Cue, Is.EqualTo(GravityFieldAudioCue.Expired));
            Assert.That(requests[2].Cue, Is.EqualTo(GravityFieldAudioCue.LockedBox));
            Assert.That(requests[0].EmitterEntityId, Is.EqualTo(30));
            Assert.That(requests[0].Context.OwnerEntityId, Is.EqualTo(30));
            Assert.That(requests[2].TargetEntityId, Is.EqualTo(20));
            Assert.That(requests[2].LockedBoxPayload, Is.EqualTo(lockedBoxPayload));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldAudioRequestPlanner_EmptyPresentationRequests_ProducesNoAudio()
        {
            var planner = new GravityFieldAudioRequestPlanner();

            var requests = planner.BuildRequests(Array.Empty<GravityFieldPresentationRequest>());

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityFieldAudioPresentationController_NoRuntimeOrMissingOptionalBinding_NoOps()
        {
            using var scope = new TestAssetScope();
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var controller = new GravityFieldAudioPresentationController(new GameplayPresentationStateStore());
            var request = CreateRequest(GravityFieldAudioCue.Activated);

            controller.ReplacePendingPlan(new[] { request });
            controller.PlayPlannedAudio();
            Assert.That(playbackPort.TwoDCalls, Is.Empty);

            controller.AttachRuntime(playbackPort, scope.CreateMap());
            controller.ReplacePendingPlan(new[] { request });
            controller.PlayPlannedAudio();
            Assert.That(playbackPort.TwoDCalls, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityFieldAudioPresentationController_ValidSfxOneShotBinding_Plays2D()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(map, (GravityFieldAudioCue.Activated, scope.CreateBinding(definition)));
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var controller = new GravityFieldAudioPresentationController(new GameplayPresentationStateStore());

            controller.AttachRuntime(playbackPort, map);
            controller.ReplacePendingPlan(new[] { CreateRequest(GravityFieldAudioCue.Activated) });
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
            Assert.That(playbackPort.TwoDCalls[0].Definition, Is.SameAs(definition));
            Assert.That(playbackPort.AttachedCalls, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityFieldAudioPresentationController_LockedBoxUsesTargetAttachedPlaybackOr2DFallback()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            var slot = AudioAttachmentSlot.FromId("Center");
            SetEntries(map, (GravityFieldAudioCue.LockedBox, scope.CreateBinding(definition, slot)));
            var playbackPort = new RecordingGameplayAudioPlaybackPort();
            var stateStore = new GameplayPresentationStateStore();
            var targetObject = scope.Track(new GameObject("GravityFieldLockedBoxAudioTarget"));
            var targetView = targetObject.AddComponent<GameplayEntityView>();
            targetView.Initialize(20);
            stateStore.ViewsByEntityId[20] = targetView;
            var controller = new GravityFieldAudioPresentationController(stateStore);
            var payload = new GravityFieldLockedBoxPayload(
                30,
                20,
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0));

            controller.AttachRuntime(playbackPort, map);
            controller.ReplacePendingPlan(new[] { CreateRequest(GravityFieldAudioCue.LockedBox, 20, payload) });
            controller.PlayPlannedAudio();
            stateStore.ViewsByEntityId.Clear();
            controller.ReplacePendingPlan(new[] { CreateRequest(GravityFieldAudioCue.LockedBox, 20, payload) });
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.AttachedCalls, Has.Count.EqualTo(1));
            Assert.That(playbackPort.AttachedCalls[0].Owner, Is.SameAs(targetView));
            Assert.That(playbackPort.AttachedCalls[0].Slot, Is.EqualTo(slot));
            Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
            Assert.That(playbackPort.TwoDCalls[0].Definition, Is.SameAs(definition));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldAudioMap_RejectsInvalidEntries()
        {
            using var scope = new TestAssetScope();

            Assert.Throws<InvalidOperationException>(() =>
            {
                var map = scope.CreateMap();
                SetEntries(map, (GravityFieldAudioCue.None, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));
                map.ValidateOrThrow();
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                var map = scope.CreateMap();
                SetEntries(map, (GravityFieldAudioCue.Activated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Ui, loop: false))));
                map.ValidateOrThrow();
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                var map = scope.CreateMap();
                SetEntries(map, (GravityFieldAudioCue.Activated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: true))));
                map.ValidateOrThrow();
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                var map = scope.CreateMap();
                SetEntries(map, (GravityFieldAudioCue.Activated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false), policy: new DummyAudioPlaybackPolicy())));
                map.ValidateOrThrow();
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                var map = scope.CreateMap();
                SetEntries(
                    map,
                    (GravityFieldAudioCue.Activated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))),
                    (GravityFieldAudioCue.Activated, scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));
                map.ValidateOrThrow();
            });
        }

        private static GravityFieldAudioRequest CreateRequest(
            GravityFieldAudioCue cue,
            int targetEntityId = 0,
            GravityFieldLockedBoxPayload lockedBoxPayload = default)
        {
            return new GravityFieldAudioRequest(
                cue,
                30,
                new SurfaceCell(FaceId.Floor, 0, 0),
                new AudioPlaybackContext(ownerEntityId: 30, debugTag: GravityFieldAudioCueCatalog.Format(cue)),
                targetEntityId,
                lockedBoxPayload);
        }

        private static void SetEntries(
            GravityFieldAudioMap map,
            params (GravityFieldAudioCue cue, AudioBinding binding)[] entries)
        {
            var entryType = typeof(GravityFieldAudioMap).GetNestedType("Entry", BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null);
            var array = Array.CreateInstance(entryType, entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = Activator.CreateInstance(entryType);
                SetSerializedField(entryType, entry, "Cue", entries[i].cue);
                SetSerializedField(entryType, entry, "Binding", entries[i].binding);
                array.SetValue(entry, i);
            }

            SetSerializedField(typeof(GravityFieldAudioMap), map, "entries", array);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private sealed class RecordingGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort
        {
            private readonly List<TwoDCall> _twoDCalls = new();
            private readonly List<AttachedCall> _attachedCalls = new();

            public IReadOnlyList<TwoDCall> TwoDCalls => _twoDCalls;

            public IReadOnlyList<AttachedCall> AttachedCalls => _attachedCalls;

            public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
            {
                _twoDCalls.Add(new TwoDCall(definition, context));
            }

            public void PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                _attachedCalls.Add(new AttachedCall(definition, owner, slot, context));
            }
        }

        private readonly struct TwoDCall
        {
            public TwoDCall(AudioDefinition definition, AudioPlaybackContext context)
            {
                Definition = definition;
                Context = context;
            }

            public AudioDefinition Definition { get; }

            public AudioPlaybackContext Context { get; }
        }

        private readonly struct AttachedCall
        {
            public AttachedCall(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                AudioPlaybackContext context)
            {
                Definition = definition;
                Owner = owner;
                Slot = slot;
                Context = context;
            }

            public AudioDefinition Definition { get; }

            public Component Owner { get; }

            public AudioAttachmentSlot Slot { get; }

            public AudioPlaybackContext Context { get; }
        }

        private sealed class TestAssetScope : IDisposable
        {
            private readonly List<UnityEngine.Object> _trackedObjects = new();

            public GravityFieldAudioMap CreateMap()
            {
                var map = Track(ScriptableObject.CreateInstance<GravityFieldAudioMap>());
                map.name = "GravityFieldAudioMap_Test";
                return map;
            }

            public AudioBinding CreateBinding(
                AudioDefinition definition,
                AudioAttachmentSlot attachmentSlot = default,
                AudioPlaybackPolicy policy = null)
            {
                var binding = new AudioBinding();
                SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", attachmentSlot);
                SetSerializedField(typeof(AudioBinding), binding, "policy", policy);
                return binding;
            }

            public SingleAudioDefinition CreateDefinition(AudioCategory category, bool loop)
            {
                var clip = Track(AudioClip.Create($"{category}_{loop}_GravityFieldClip", 1, 1, 44100, false));
                var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>());
                definition.name = $"{category}_{loop}_GravityFieldDef";
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

            public T Track<T>(T obj)
                where T : UnityEngine.Object
            {
                _trackedObjects.Add(obj);
                return obj;
            }
        }

        private sealed class DummyAudioPlaybackPolicy : AudioPlaybackPolicy
        {
        }
    }
}
