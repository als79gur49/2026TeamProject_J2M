using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.AudioPolicy;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayAudioOverlapRefactorTests
    {
        [Test]
        [Category("Core")]
        public void RandomDefinition_DefaultVariantTrim_PreservesExistingVolume()
        {
            var definition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            var clip = AudioClip.Create("DefaultVariantTrim", 100, 1, 44100, false);
            try
            {
                SetField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 0.5f);
                SetRandomClips(definition, (clip, 1f, 1f, 1f));

                var playback = definition.Resolve(new AudioPlaybackContext(volumeMultiplier: 1f));

                Assert.That(playback.Clip, Is.SameAs(clip));
                Assert.That(playback.Volume, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void RandomDefinition_SelectedVariantTrim_AppliesToFinalVolume()
        {
            var definition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            var clip = AudioClip.Create("SelectedVariantTrim", 100, 1, 44100, false);
            try
            {
                SetField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 0.8f);
                SetRandomClips(definition, (clip, 1f, 0.25f, 1f));

                var playback = definition.Resolve(new AudioPlaybackContext(volumeMultiplier: 0.5f));

                Assert.That(playback.Volume, Is.EqualTo(0.1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void RandomDefinition_InvalidVariantTrim_IsDiagnosedOrSanitized()
        {
            var definition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            var clip = AudioClip.Create("InvalidVariantTrim", 100, 1, 44100, false);
            try
            {
                SetRandomClips(definition, (clip, 1f, 0f, float.NaN));
                var errors = new List<string>();

                AudioBindingDiagnostics.AppendValidationErrors(
                    CreateBinding(definition),
                    "Owner",
                    "binding",
                    errors,
                    AudioBindingValidationOptions.Default);
                var playback = definition.Resolve(new AudioPlaybackContext(volumeMultiplier: 1f));

                Assert.That(errors, Has.Some.Contains("invalid volume trim"));
                Assert.That(errors, Has.Some.Contains("invalid pitch trim"));
                Assert.That(playback.Volume, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void RandomDefinition_SelectedVariantPitchTrim_AppliesToFinalPitch()
        {
            var definition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            var clip = AudioClip.Create("SelectedVariantPitchTrim", 100, 1, 44100, false);
            try
            {
                SetRandomClips(definition, (clip, 1f, 1f, 1.25f));

                var playback = definition.Resolve(new AudioPlaybackContext(pitchMultiplier: 0.5f));

                Assert.That(playback.Pitch, Is.EqualTo(0.625f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void RandomDefinition_InvalidPitchTrim_IsDiagnosedOrSanitized()
        {
            var definition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            var clip = AudioClip.Create("InvalidPitchTrim", 100, 1, 44100, false);
            try
            {
                SetRandomClips(definition, (clip, 1f, 1f, float.PositiveInfinity));
                var errors = new List<string>();

                AudioBindingDiagnostics.AppendValidationErrors(
                    CreateBinding(definition),
                    "Owner",
                    "binding",
                    errors,
                    AudioBindingValidationOptions.Default);
                var playback = definition.Resolve(new AudioPlaybackContext(pitchMultiplier: 1f));

                Assert.That(errors, Has.Some.Contains("invalid pitch trim"));
                Assert.That(playback.Pitch, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void RandomDefinition_VolumeTrimAndPitchTrim_AreAppliedIndependently()
        {
            var definition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            var clip = AudioClip.Create("IndependentVariantTrims", 100, 1, 44100, false);
            try
            {
                SetField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 0.8f);
                SetRandomClips(definition, (clip, 1f, 0.5f, 1.5f));

                var playback = definition.Resolve(new AudioPlaybackContext(volumeMultiplier: 0.25f, pitchMultiplier: 2f));

                Assert.That(playback.Volume, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(playback.Pitch, Is.EqualTo(3f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void RandomDefinition_NullClipVariant_IsDiagnosed()
        {
            var definition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            try
            {
                SetRandomClips(definition, (null, 1f, 1f, 1f));
                var errors = new List<string>();

                AudioBindingDiagnostics.AppendValidationErrors(
                    CreateBinding(definition),
                    "Owner",
                    "binding",
                    errors,
                    AudioBindingValidationOptions.Default);

                Assert.That(errors, Has.Some.Contains("null AudioClip"));
                Assert.That(errors, Has.Some.Contains("no non-null random clip variants"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void RandomDefinition_WeightedRandomSelection_DoesNotDependOnTrim()
        {
            var first = AudioClip.Create("RandomWeightFirst", 100, 1, 44100, false);
            var second = AudioClip.Create("RandomWeightSecond", 100, 1, 44100, false);
            var neutralTrimDefinition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            var biasedTrimDefinition = ScriptableObject.CreateInstance<RandomAudioDefinition>();
            try
            {
                SetRandomClips(neutralTrimDefinition, (first, 1f, 1f, 1f), (second, 3f, 1f, 1f));
                SetRandomClips(biasedTrimDefinition, (first, 1f, 0.1f, 1f), (second, 3f, 10f, 1f));

                UnityEngine.Random.InitState(12345);
                var neutralSequence = ResolveClipSequence(neutralTrimDefinition, 32);
                UnityEngine.Random.InitState(12345);
                var biasedSequence = ResolveClipSequence(biasedTrimDefinition, 32);

                CollectionAssert.AreEqual(neutralSequence, biasedSequence);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(neutralTrimDefinition);
                UnityEngine.Object.DestroyImmediate(biasedTrimDefinition);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        [Category("Core")]
        public void SingleDefinition_ReturnsClipSelectionWithNeutralVariantTrim()
        {
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var clip = AudioClip.Create("SingleNeutral", 100, 1, 44100, false);
            try
            {
                SetField(typeof(SingleAudioDefinition), definition, "clip", clip);
                SetField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 0.75f);

                var playback = definition.Resolve(new AudioPlaybackContext(volumeMultiplier: 0.5f));

                Assert.That(playback.Clip, Is.SameAs(clip));
                Assert.That(playback.Volume, Is.EqualTo(0.375f).Within(0.0001f));
                Assert.That(playback.Pitch, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void SingleDefinition_ReturnsClipSelectionWithNeutralPitchTrim()
        {
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var clip = AudioClip.Create("SingleNeutralPitch", 100, 1, 44100, false);
            try
            {
                SetField(typeof(SingleAudioDefinition), definition, "clip", clip);

                var playback = definition.Resolve(new AudioPlaybackContext(pitchMultiplier: 1.75f));

                Assert.That(playback.Pitch, Is.EqualTo(1.75f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioPlanner_SingleEvent_ProducesSingleRequest()
        {
            var coalescer = new TileFeatureAudioCoalescer();
            var requests = coalescer.Coalesce(
                new[] { CreateTileRequest(TileFeatureAudioCue.DestroyTileActivated, 1) },
                tickIndex: 7);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.DestroyTileActivated));
            Assert.That(requests[0].BurstKind, Is.EqualTo(TileFeatureAudioBurstKind.None));
            Assert.That(requests[0].Count, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioPlanner_MultipleOnEvents_CoalescesToOneOnBurst()
        {
            var coalescer = new TileFeatureAudioCoalescer();
            var requests = coalescer.Coalesce(
                new[]
                {
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileActivated, 1),
                    CreateTileRequest(TileFeatureAudioCue.BarricadeActivated, 2),
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileActivated, 3),
                },
                tickIndex: 7);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.TileFeatureOnBurst));
            Assert.That(requests[0].BurstKind, Is.EqualTo(TileFeatureAudioBurstKind.On));
            Assert.That(requests[0].Count, Is.EqualTo(3));
            Assert.That(requests[0].TickIndex, Is.EqualTo(7));
            Assert.That(requests[0].Context.VolumeMultiplier, Is.LessThanOrEqualTo(1.05f));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioPlanner_MultipleOffEvents_CoalescesToOneOffBurst()
        {
            var coalescer = new TileFeatureAudioCoalescer();
            var requests = coalescer.Coalesce(
                new[]
                {
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileDeactivated, 1),
                    CreateTileRequest(TileFeatureAudioCue.BarricadeDeactivated, 2),
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileDeactivated, 3),
                    CreateTileRequest(TileFeatureAudioCue.BarricadeDeactivated, 4),
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileDeactivated, 5),
                },
                tickIndex: 8);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.TileFeatureOffBurst));
            Assert.That(requests[0].BurstKind, Is.EqualTo(TileFeatureAudioBurstKind.Off));
            Assert.That(requests[0].Count, Is.EqualTo(5));
            Assert.That(requests[0].Context.VolumeMultiplier, Is.LessThanOrEqualTo(1.1f));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioPlanner_MixedOnOffEvents_CoalescesByKind()
        {
            var coalescer = new TileFeatureAudioCoalescer();
            var requests = coalescer.Coalesce(
                new[]
                {
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileActivated, 1),
                    CreateTileRequest(TileFeatureAudioCue.BarricadeActivated, 2),
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileDeactivated, 3),
                    CreateTileRequest(TileFeatureAudioCue.BarricadeDeactivated, 4),
                },
                tickIndex: 11);

            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.TileFeatureOnBurst));
            Assert.That(requests[1].Cue, Is.EqualTo(TileFeatureAudioCue.TileFeatureOffBurst));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioController_DoesNotPlayNIndividualRequests()
        {
            var coalescer = new TileFeatureAudioCoalescer();
            var requests = coalescer.Coalesce(
                new[]
                {
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileActivated, 1),
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileActivated, 2),
                    CreateTileRequest(TileFeatureAudioCue.BarricadeActivated, 3),
                    CreateTileRequest(TileFeatureAudioCue.BarricadeActivated, 4),
                },
                tickIndex: 10);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.TileFeatureOnBurst));
            Assert.That(requests[0].Count, Is.EqualTo(4));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioCoalescer_MultipleOnEvents_WithBurstBinding_ProducesOnBurst()
        {
            var coalescer = new TileFeatureAudioCoalescer();

            var requests = coalescer.Coalesce(
                new[]
                {
                    CreateTileRequest(TileFeatureAudioCue.DestroyTileActivated, 1),
                    CreateTileRequest(TileFeatureAudioCue.BarricadeActivated, 2),
                },
                tickIndex: 12);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(TileFeatureAudioCue.TileFeatureOnBurst));
            Assert.That(requests[0].RepresentativeCue, Is.EqualTo(TileFeatureAudioCue.DestroyTileActivated));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioCoalescer_MultipleOnEvents_MissingBurstBinding_FallsBackToSingle()
        {
            var burstRequest = CreateBurstRequest(TileFeatureAudioCue.DestroyTileActivated, TileFeatureAudioCue.BarricadeActivated);
            var buttonDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var singleDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var map = ScriptableObject.CreateInstance<TileFeatureAudioMap>();
            try
            {
                SetTileFeatureEntries(
                    map,
                    (TileFeatureAudioCue.ButtonActivated, CreateBinding(buttonDefinition)),
                    (TileFeatureAudioCue.DestroyTileActivated, CreateBinding(singleDefinition)));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var diagnostics = new List<string>();
                var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
                controller.SetDiagnosticSink(diagnostics.Add);
                controller.AttachRuntime(playbackPort, map);
                controller.ReplacePendingPlan(new[] { burstRequest });

                controller.PlayPlannedAudio();

                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Definition, Is.SameAs(singleDefinition));
                Assert.That(diagnostics, Has.Some.Contains("On burst binding is missing"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(buttonDefinition);
                UnityEngine.Object.DestroyImmediate(singleDefinition);
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioCoalescer_MultipleOffEvents_MissingBurstBinding_FallsBackToSingle()
        {
            var burstRequest = CreateBurstRequest(TileFeatureAudioCue.DestroyTileDeactivated, TileFeatureAudioCue.BarricadeDeactivated);
            var buttonDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var singleDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var map = ScriptableObject.CreateInstance<TileFeatureAudioMap>();
            try
            {
                SetTileFeatureEntries(
                    map,
                    (TileFeatureAudioCue.ButtonActivated, CreateBinding(buttonDefinition)),
                    (TileFeatureAudioCue.DestroyTileDeactivated, CreateBinding(singleDefinition)));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var diagnostics = new List<string>();
                var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
                controller.SetDiagnosticSink(diagnostics.Add);
                controller.AttachRuntime(playbackPort, map);
                controller.ReplacePendingPlan(new[] { burstRequest });

                controller.PlayPlannedAudio();

                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Definition, Is.SameAs(singleDefinition));
                Assert.That(diagnostics, Has.Some.Contains("Off burst binding is missing"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(buttonDefinition);
                UnityEngine.Object.DestroyImmediate(singleDefinition);
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioCoalescer_MixedOnOff_MissingOneBurstBinding_OnlyThatKindFallsBack()
        {
            var onBurstRequest = CreateBurstRequest(TileFeatureAudioCue.DestroyTileActivated, TileFeatureAudioCue.BarricadeActivated);
            var offBurstRequest = CreateBurstRequest(TileFeatureAudioCue.DestroyTileDeactivated, TileFeatureAudioCue.BarricadeDeactivated);
            var buttonDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var onFallbackDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var offBurstDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var map = ScriptableObject.CreateInstance<TileFeatureAudioMap>();
            try
            {
                SetTileFeatureEntries(
                    map,
                    (TileFeatureAudioCue.ButtonActivated, CreateBinding(buttonDefinition)),
                    (TileFeatureAudioCue.DestroyTileActivated, CreateBinding(onFallbackDefinition)),
                    (TileFeatureAudioCue.TileFeatureOffBurst, CreateBinding(offBurstDefinition)));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var diagnostics = new List<string>();
                var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
                controller.SetDiagnosticSink(diagnostics.Add);
                controller.AttachRuntime(playbackPort, map);
                controller.ReplacePendingPlan(new[] { onBurstRequest, offBurstRequest });

                controller.PlayPlannedAudio();

                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(2));
                Assert.That(playbackPort.TwoDCalls[0].Definition, Is.SameAs(onFallbackDefinition));
                Assert.That(playbackPort.TwoDCalls[1].Definition, Is.SameAs(offBurstDefinition));
                Assert.That(diagnostics, Has.Count.EqualTo(1));
                Assert.That(diagnostics[0], Does.Contain("On burst binding is missing"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(buttonDefinition);
                UnityEngine.Object.DestroyImmediate(onFallbackDefinition);
                UnityEngine.Object.DestroyImmediate(offBurstDefinition);
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudioCoalescer_MissingBurstBinding_ReportsDiagnostic()
        {
            var burstRequest = CreateBurstRequest(TileFeatureAudioCue.DestroyTileActivated, TileFeatureAudioCue.BarricadeActivated);
            var buttonDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var singleDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var map = ScriptableObject.CreateInstance<TileFeatureAudioMap>();
            try
            {
                SetTileFeatureEntries(
                    map,
                    (TileFeatureAudioCue.ButtonActivated, CreateBinding(buttonDefinition)),
                    (TileFeatureAudioCue.DestroyTileActivated, CreateBinding(singleDefinition)));
                var diagnostics = new List<string>();
                var controller = new TileFeatureAudioPresentationController(new GameplayPresentationStateStore());
                controller.SetDiagnosticSink(diagnostics.Add);
                controller.AttachRuntime(new RecordingGameplayAudioPlaybackPort(), map);
                controller.ReplacePendingPlan(new[] { burstRequest });

                controller.PlayPlannedAudio();

                Assert.That(diagnostics, Has.Some.Contains("Falling back to one representative single request"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(buttonDefinition);
                UnityEngine.Object.DestroyImmediate(singleDefinition);
            }
        }

        [Test]
        [Category("Core")]
        public void AudioVoicePolicy_EnemyMovement_PerOwnerCooldown_Works()
        {
            var gate = new AudioVoicePolicyGate();
            var policy = new AudioVoicePolicy(
                AudioVoiceGroupId.EnemyMovement,
                priority: 15,
                maxVoicesGlobal: 3,
                maxVoicesPerOwner: 1,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0.5f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);

            Assert.That(gate.ShouldAccept(policy, 10, tickIndex: 1, simulationTicksPerSecond: 10), Is.True);
            Assert.That(gate.ShouldAccept(policy, 10, tickIndex: 2, simulationTicksPerSecond: 10), Is.False);
            Assert.That(gate.ShouldAccept(policy, 11, tickIndex: 2, simulationTicksPerSecond: 10), Is.True);
            Assert.That(gate.ShouldAccept(policy, 10, tickIndex: 6, simulationTicksPerSecond: 10), Is.True);
        }

        [Test]
        [Category("Core")]
        public void AudioVoicePolicy_EnemyMovement_GlobalCooldown_Works()
        {
            var gate = new AudioVoicePolicyGate();
            var policy = new AudioVoicePolicy(
                AudioVoiceGroupId.EnemyMovement,
                priority: 15,
                maxVoicesGlobal: 3,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0.25f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);

            Assert.That(gate.ShouldAccept(policy, 10, tickIndex: 1, simulationTicksPerSecond: 20), Is.True);
            Assert.That(gate.ShouldAccept(policy, 11, tickIndex: 5, simulationTicksPerSecond: 20), Is.False);
            Assert.That(gate.ShouldAccept(policy, 11, tickIndex: 6, simulationTicksPerSecond: 20), Is.True);
        }

        [Test]
        [Category("Core")]
        public void AudioVoicePolicy_EnemyMovement_MaxPerTick_Works()
        {
            var gate = new AudioVoicePolicyGate();
            var policy = new AudioVoicePolicy(
                AudioVoiceGroupId.EnemyMovement,
                priority: 15,
                maxVoicesGlobal: 2,
                maxVoicesPerOwner: 1,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);

            Assert.That(gate.ShouldAccept(policy, 10, tickIndex: 1, simulationTicksPerSecond: 20), Is.True);
            Assert.That(gate.ShouldAccept(policy, 10, tickIndex: 1, simulationTicksPerSecond: 20), Is.False);
            Assert.That(gate.ShouldAccept(policy, 11, tickIndex: 1, simulationTicksPerSecond: 20), Is.True);
            Assert.That(gate.ShouldAccept(policy, 12, tickIndex: 1, simulationTicksPerSecond: 20), Is.False);
            Assert.That(gate.ShouldAccept(policy, 12, tickIndex: 2, simulationTicksPerSecond: 20), Is.True);
        }

        [Test]
        [Category("Core")]
        public void AudioVoicePolicyDiagnostics_AttenuateThenDrop_ReportsReservedForV2Warning()
        {
            var diagnostics = new RecordingAudioPolicyDiagnostics();

            var normalized = AudioVoicePolicySupport.NormalizeForGameplaySfxArbiterV1(
                VoiceOverflowMode.AttenuateThenDrop,
                diagnostics,
                "TileFeatureToggle");

            Assert.That(normalized, Is.EqualTo(VoiceOverflowMode.DropNewest));
            Assert.That(diagnostics.Warnings, Has.Some.Contains("reserved for v2"));
            Assert.That(diagnostics.Errors, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void AudioVoicePolicyDiagnostics_StealLowerPriority_ReportsUnsupportedError()
        {
            var diagnostics = new RecordingAudioPolicyDiagnostics();

            var normalized = AudioVoicePolicySupport.NormalizeForGameplaySfxArbiterV1(
                VoiceOverflowMode.StealLowerPriority,
                diagnostics,
                "PlayerDamage");

            Assert.That(normalized, Is.EqualTo(VoiceOverflowMode.DropNewest));
            Assert.That(diagnostics.Errors, Has.Some.Contains("not supported"));
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_UnsupportedOverflowMode_DoesNotSilentlyPretendSupport()
        {
            var diagnostics = new RecordingAudioPolicyDiagnostics();
            var arbiter = new GameplaySfxArbiter(diagnostics);
            var policy = new AudioVoicePolicy(
                AudioVoiceGroupId.PlayerCritical,
                priority: 100,
                maxVoicesGlobal: 1,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.StealLowerPriority);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "PlayerDamage", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "PlayerDamage", 2, ownerEntityId: 11),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(1));
                Assert.That(diagnostics.Errors, Has.Some.Contains("StealLowerPriority"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_AttenuateThenDropV1_NormalizesToDropNewestOrReportsWarning()
        {
            var diagnostics = new RecordingAudioPolicyDiagnostics();
            var arbiter = new GameplaySfxArbiter(diagnostics);
            var policy = new AudioVoicePolicy(
                AudioVoiceGroupId.TileFeatureToggle,
                priority: 25,
                maxVoicesGlobal: 1,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.AttenuateThenDrop);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "TileFeatureOnBurst", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "TileFeatureOffBurst", 2, ownerEntityId: 11),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(1));
                Assert.That(diagnostics.Warnings, Has.Some.Contains("reserved for v2"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyMoveCadenceGate_ExistingBehavior_PreservedWithPolicy()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 3,
                globalMoveMinIntervalTicks: 0,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 2);

            Assert.That(gate.Policy.Group, Is.EqualTo(AudioVoiceGroupId.EnemyMovement));
            Assert.That(gate.ShouldPlayMove(10, 1), Is.True);
            Assert.That(gate.ShouldPlayMove(10, 2), Is.False);
            Assert.That(gate.ShouldPlayMove(10, 4), Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyMoveCadencePolicyAdapter_PreservesPerEntityCooldown()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 3,
                globalMoveMinIntervalTicks: 0,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 2);

            Assert.That(gate.ShouldPlayMove(10, 1), Is.True);
            Assert.That(gate.ShouldPlayMove(10, 2), Is.False);
            Assert.That(gate.ShouldPlayMove(10, 4), Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyMoveCadencePolicyAdapter_PreservesGlobalCooldown()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 0,
                globalMoveMinIntervalTicks: 3,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 2);

            Assert.That(gate.ShouldPlayMove(10, 1), Is.True);
            Assert.That(gate.ShouldPlayMove(11, 2), Is.False);
            Assert.That(gate.ShouldPlayMove(11, 4), Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyMoveCadencePolicyAdapter_PreservesMaxPerTick()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 0,
                globalMoveMinIntervalTicks: 0,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 2);

            Assert.That(gate.ShouldPlayMove(10, 1), Is.True);
            Assert.That(gate.ShouldPlayMove(11, 1), Is.True);
            Assert.That(gate.ShouldPlayMove(12, 1), Is.False);
            Assert.That(gate.ShouldPlayMove(12, 2), Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyMoveCadencePolicyAdapter_PreservesDeterministicJitter()
        {
            var first = EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(20, 10, jitterTicks: 4);
            var second = EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(20, 10, jitterTicks: 4);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.InRange(0, 4));
        }

        [Test]
        [Category("Core")]
        public void EnemyMoveCadencePolicyAdapter_MatchesLegacyGateForSameInputSequence()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 3,
                globalMoveMinIntervalTicks: 2,
                jitterTicks: 1,
                maxMoveRequestsPerTick: 2);
            var events = new[]
            {
                (owner: 10, tick: 1),
                (owner: 11, tick: 1),
                (owner: 12, tick: 1),
                (owner: 10, tick: 2),
                (owner: 11, tick: 3),
                (owner: 10, tick: 5),
                (owner: 12, tick: 5),
            };

            var actual = events.Select(item => gate.ShouldPlayMove(item.owner, item.tick)).ToArray();
            var expected = EvaluateLegacyEnemyMoveCadence(
                events,
                perEntityMoveMinIntervalTicks: 3,
                globalMoveMinIntervalTicks: 2,
                jitterTicks: 1,
                maxMoveRequestsPerTick: 2);

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_SameGroupSameTick_RespectsMaxVoices()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = new AudioVoicePolicy(
                AudioVoiceGroupId.TileFeatureToggle,
                priority: 25,
                maxVoicesGlobal: 2,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "TileFeatureOnBurst", 1, ownerEntityId: 1),
                        CreateSfxRequest(definition, policy, "TileFeatureOffBurst", 2, ownerEntityId: 2),
                        CreateSfxRequest(definition, policy, "TileFeatureOnBurst", 3, ownerEntityId: 3),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_SamePriority_PreservesOriginalSequence()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 40, maxGlobal: 0);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "First", 1),
                        CreateSfxRequest(definition, policy, "Second", 2),
                        CreateSfxRequest(definition, policy, "Third", 3),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted.Select(request => request.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "First", "Second", "Third" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_DifferentPriority_HigherPriorityWinsAdmissionBudget()
        {
            var arbiter = new GameplaySfxArbiter();
            var lowPolicy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 1, maxGlobal: 1);
            var highPolicy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 100, maxGlobal: 1);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, lowPolicy, "Low", 1),
                        CreateSfxRequest(definition, highPolicy, "High", 2),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(1));
                Assert.That(accepted[0].Context.DebugTag, Is.EqualTo("High"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_ReturnsAcceptedRequestsInOriginalSequence()
        {
            var arbiter = new GameplaySfxArbiter();
            var lowPolicy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 1, maxGlobal: 0);
            var highPolicy = CreatePolicy(AudioVoiceGroupId.PlayerCritical, priority: 100, maxGlobal: 0);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, lowPolicy, "LowFirst", 1),
                        CreateSfxRequest(definition, highPolicy, "HighSecond", 2),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted.Select(request => request.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "LowFirst", "HighSecond" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_ActionAudio_SamePriorityLayeringOrderPreserved()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.GameplayAction, priority: 60, maxGlobal: 0, duplicateWindow: 0.01f);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "Action:Push:Windup", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Action:Push:Contact", 2, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Action:Push:Execute", 3, ownerEntityId: 10),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted.Select(request => request.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Action:Push:Windup", "Action:Push:Contact", "Action:Push:Execute" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_HighPrioritySurvivesLowPrioritySpam()
        {
            var arbiter = new GameplaySfxArbiter();
            var lowPolicy = new AudioVoicePolicy(
                AudioVoiceGroupId.GenericGameplay,
                priority: 1,
                maxVoicesGlobal: 2,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);
            var highPolicy = new AudioVoicePolicy(
                AudioVoiceGroupId.GenericGameplay,
                priority: 100,
                maxVoicesGlobal: 2,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, lowPolicy, "LowA", 1),
                        CreateSfxRequest(definition, lowPolicy, "LowB", 2),
                        CreateSfxRequest(definition, highPolicy, "High", 3),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
                Assert.That(accepted, Has.Some.Property(nameof(GameplaySfxRequest.Context))
                    .Property(nameof(AudioPlaybackContext.DebugTag)).EqualTo("High"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_TileFeatureSpam_DoesNotDropPlayerDamage()
        {
            var arbiter = new GameplaySfxArbiter();
            var tilePolicy = new AudioVoicePolicy(
                AudioVoiceGroupId.TileFeatureToggle,
                priority: 25,
                maxVoicesGlobal: 1,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);
            var damagePolicy = new AudioVoicePolicy(
                AudioVoiceGroupId.PlayerCritical,
                priority: 100,
                maxVoicesGlobal: 2,
                maxVoicesPerOwner: 1,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.NeverDrop);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, tilePolicy, "TileFeatureOnBurst", 1),
                        CreateSfxRequest(definition, tilePolicy, "TileFeatureOnBurst", 2),
                        CreateSfxRequest(definition, damagePolicy, "PlayerDamage", 3),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
                Assert.That(accepted, Has.Some.Property(nameof(GameplaySfxRequest.Context))
                    .Property(nameof(AudioPlaybackContext.DebugTag)).EqualTo("PlayerDamage"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_Admission_PlayerDamageSurvivesTileFeatureSpam()
        {
            var arbiter = new GameplaySfxArbiter();
            var tilePolicy = CreatePolicy(AudioVoiceGroupId.TileFeatureToggle, priority: 25, maxGlobal: 2);
            var damagePolicy = CreatePolicy(AudioVoiceGroupId.PlayerCritical, priority: 100, maxGlobal: 2, maxPerOwner: 1);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var requests = new List<GameplaySfxRequest>();
                for (var i = 0; i < 20; i++)
                {
                    requests.Add(CreateSfxRequest(
                        definition,
                        tilePolicy,
                        $"TileFeatureOnBurst:{i}",
                        i + 1,
                        ownerEntityId: 100 + i));
                }

                requests.Add(CreateSfxRequest(definition, damagePolicy, "PlayerDamage", 21, ownerEntityId: 10));

                var accepted = arbiter.Filter(requests, tickIndex: 1, simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Some.Property(nameof(GameplaySfxRequest.Context))
                    .Property(nameof(AudioPlaybackContext.DebugTag)).EqualTo("PlayerDamage"));
                Assert.That(accepted.Count(request => request.Policy.Group == AudioVoiceGroupId.TileFeatureToggle),
                    Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_DoesNotDropProjectileImpactBecauseOfActiveOrPlayerDamage()
        {
            var arbiter = new GameplaySfxArbiter();
            var activePolicy = GameplaySfxPolicyCatalog.Resolve("Active");
            var impactPolicy = GameplaySfxPolicyCatalog.Resolve("ProjectileImpact");
            var damagePolicy = GameplaySfxPolicyCatalog.Resolve("PlayerDamage");
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, activePolicy, "Active", 1, ownerEntityId: 20),
                        CreateSfxRequest(definition, impactPolicy, "ProjectileImpact", 2, ownerEntityId: 20),
                        CreateSfxRequest(definition, damagePolicy, "PlayerDamage", 3, ownerEntityId: 10),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(
                    accepted.Select(request => request.Context.DebugTag).ToArray(),
                    Is.EquivalentTo(new[] { "Active", "ProjectileImpact", "PlayerDamage" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_AllowsSameOwnerActiveAndProjectileImpactInSameTick()
        {
            var arbiter = new GameplaySfxArbiter();
            var activePolicy = GameplaySfxPolicyCatalog.Resolve("Active");
            var impactPolicy = GameplaySfxPolicyCatalog.Resolve("ProjectileImpact");
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, activePolicy, "Active", 1, ownerEntityId: 20),
                        CreateSfxRequest(definition, impactPolicy, "ProjectileImpact", 2, ownerEntityId: 20),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(
                    accepted.Select(request => request.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Active", "ProjectileImpact" }));
                Assert.That(activePolicy.Group, Is.Not.EqualTo(impactPolicy.Group));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void ProjectileImpact_GroupCapStillBoundsMultipleImpacts()
        {
            var arbiter = new GameplaySfxArbiter();
            var impactPolicy = GameplaySfxPolicyCatalog.Resolve("ProjectileImpact");
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, impactPolicy, "ProjectileImpact", 1, ownerEntityId: 20),
                        CreateSfxRequest(definition, impactPolicy, "ProjectileImpact", 2, ownerEntityId: 21),
                        CreateSfxRequest(definition, impactPolicy, "ProjectileImpact", 3, ownerEntityId: 22),
                        CreateSfxRequest(definition, impactPolicy, "ProjectileImpact", 4, ownerEntityId: 23),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(impactPolicy.Group, Is.EqualTo(AudioVoiceGroupId.ProjectileImpact));
                Assert.That(impactPolicy.MaxVoicesGlobal, Is.EqualTo(3));
                Assert.That(accepted, Has.Count.EqualTo(3));
                Assert.That(accepted.All(request => request.Policy.Group == AudioVoiceGroupId.ProjectileImpact), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_HighPriorityCriticalFeedback_WinsAdmissionOverLowPrioritySpam()
        {
            var arbiter = new GameplaySfxArbiter();
            var lowPolicy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 1, maxGlobal: 1);
            var criticalPolicy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 100, maxGlobal: 1);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, lowPolicy, "LowSpamA", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, lowPolicy, "LowSpamB", 2, ownerEntityId: 11),
                        CreateSfxRequest(definition, criticalPolicy, "Critical", 3, ownerEntityId: 12),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(1));
                Assert.That(accepted[0].Context.DebugTag, Is.EqualTo("Critical"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_MaxVoicesGlobal_LimitsRequestsWithinBatch()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.BlockImpact, priority: 35, maxGlobal: 2);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "BlockA", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "BlockB", 2, ownerEntityId: 11),
                        CreateSfxRequest(definition, policy, "BlockC", 3, ownerEntityId: 12),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_MaxVoicesPerOwner_LimitsRequestsWithinBatchPerOwner()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.BlockImpact, priority: 35, maxGlobal: 0, maxPerOwner: 1);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "BlockA", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "BlockB", 2, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "BlockC", 3, ownerEntityId: 11),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
                Assert.That(accepted.Count(request => request.OwnerEntityId == 10), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_OwnerCooldown_IsIndependentFromGlobalCooldown()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = new AudioVoicePolicy(
                AudioVoiceGroupId.EnemyMovement,
                priority: 15,
                maxVoicesGlobal: 0,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0.5f,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var firstTick = arbiter.Filter(
                    new[] { CreateSfxRequest(definition, policy, "Move", 1, ownerEntityId: 10) },
                    tickIndex: 1,
                    simulationTicksPerSecond: 10);
                var secondTick = arbiter.Filter(
                    new[] { CreateSfxRequest(definition, policy, "Move", 2, ownerEntityId: 11) },
                    tickIndex: 2,
                    simulationTicksPerSecond: 10);
                var blockedOwner = arbiter.Filter(
                    new[] { CreateSfxRequest(definition, policy, "Move", 3, ownerEntityId: 10) },
                    tickIndex: 2,
                    simulationTicksPerSecond: 10);

                Assert.That(firstTick, Has.Count.EqualTo(1));
                Assert.That(secondTick, Has.Count.EqualTo(1));
                Assert.That(blockedOwner, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_ActionAudio_KeepsLayeringExceptExactDuplicates()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = new AudioVoicePolicy(
                AudioVoiceGroupId.GameplayAction,
                priority: 60,
                maxVoicesGlobal: 0,
                maxVoicesPerOwner: 0,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: 0.01f,
                overflowMode: VoiceOverflowMode.NeverDrop);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "Action:Push:Execute", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Action:Push:Execute", 2, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Action:Push:Contact", 3, ownerEntityId: 10),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
                Assert.That(accepted[0].Context.DebugTag, Is.EqualTo("Action:Push:Execute"));
                Assert.That(accepted[1].Context.DebugTag, Is.EqualTo("Action:Push:Contact"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_ExactDuplicate_SameOwnerSameBindingSameTick_IsRemoved()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 40, maxGlobal: 0);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "Impact", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Impact", 2, ownerEntityId: 10),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_SameBindingDifferentOwner_IsNotRemoved()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 40, maxGlobal: 0);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "Impact", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Impact", 2, ownerEntityId: 11),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_SameBindingDifferentActionMoment_IsNotRemoved()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.GameplayAction, priority: 60, maxGlobal: 0, duplicateWindow: 0.01f);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "Action:Push:Contact", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Action:Push:Execute", 2, ownerEntityId: 10),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_SameGroupDifferentBinding_IsNotRemoved()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.GenericGameplay, priority: 40, maxGlobal: 0);
            var firstDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var secondDefinition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(firstDefinition, policy, "Impact", 1, ownerEntityId: 10),
                        CreateSfxRequest(secondDefinition, policy, "Impact", 2, ownerEntityId: 10),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstDefinition);
                UnityEngine.Object.DestroyImmediate(secondDefinition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_ActionAudioLayering_IsNotTreatedAsDuplicate()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.GameplayAction, priority: 60, maxGlobal: 0, duplicateWindow: 0.01f);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "Action:Pull:Windup", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Action:Pull:Contact", 2, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "Action:Pull:Execute", 3, ownerEntityId: 10),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_TileFeatureOnAndOffBurst_AreNotDuplicates()
        {
            var arbiter = new GameplaySfxArbiter();
            var policy = CreatePolicy(AudioVoiceGroupId.TileFeatureToggle, priority: 25, maxGlobal: 0);
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                var accepted = arbiter.Filter(
                    new[]
                    {
                        CreateSfxRequest(definition, policy, "TileFeatureOnBurst", 1, ownerEntityId: 10),
                        CreateSfxRequest(definition, policy, "TileFeatureOffBurst", 2, ownerEntityId: 10),
                    },
                    tickIndex: 1,
                    simulationTicksPerSecond: 20);

                Assert.That(accepted, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplaySfxArbiter_DoesNotProcessUiOrBgm()
        {
            AssertFilesDoNotContain(
                EnumerateRepoFiles("Assets/_Features/UI", "*.cs")
                    .Concat(EnumerateRepoFiles("Assets/_Features/Flow/Flow_Audio", "*.cs")),
                "GameplaySfxArbiter");
        }

        [Test]
        [Category("Core")]
        public void AudioArchitecture_WorldStateTickPipelineEntityLogic_DoNotReferenceIAudioService()
        {
            AssertFilesDoNotContain(
                EnumerateRepoFiles("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime", "*.cs")
                    .Concat(EnumerateRepoFiles("Assets/_Features/Gameplay/Gameplay_Loop/Runtime", "*.cs"))
                    .Concat(EnumerateRepoFiles("Assets/_Features/Gameplay/Gameplay_Entities/Runtime", "*.cs"))
                    .Concat(EnumerateRepoFiles("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime", "*.cs")),
                "IAudioService");
        }

        [Test]
        [Category("Core")]
        public void AudioArchitecture_SharedAudio_DoesNotReferenceGameplayAssembly()
        {
            Assert.That(ReadRepoFile("Assets/_Shared/Audio/Game.Shared.Audio.asmdef"), Does.Not.Contain("Game.Feature.Gameplay"));
            AssertFilesDoNotContain(
                EnumerateRepoFiles("Assets/_Shared/Audio/Runtime", "*.cs"),
                "using Game.Feature.Gameplay");
        }

        [Test]
        [Category("Core")]
        public void AudioArchitecture_AudioBindingPolicy_RemainsReservedAndNullInV1()
        {
            var binding = new AudioBinding();
            SetField(typeof(AudioBinding), binding, "policy", new TestAudioPlaybackPolicy());
            var errors = new List<string>();

            AudioBindingDiagnostics.AppendValidationErrors(
                binding,
                "Owner",
                "binding",
                errors,
                AudioBindingValidationOptions.Default);

            Assert.That(errors, Has.Some.Contains("AudioBinding.Policy"));
            AssertFilesDoNotContain(
                new[] { "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySfxArbitratingPlaybackPort.cs" },
                ".Policy");
        }

        [Test]
        [Category("Core")]
        public void AudioArchitecture_GameplaySfxArbiter_LivesOnlyAtHostPresentationSeam()
        {
            var runtimeHits = EnumerateRepoFiles("Assets/_Features/Gameplay", "*.cs")
                .Where(path => !path.Contains("/Gameplay_Tests/", StringComparison.Ordinal))
                .Where(path => !path.Contains("/Gameplay_AudioPolicy/", StringComparison.Ordinal))
                .Where(path => ReadRepoFile(path).Contains("GameplaySfxArbiter", StringComparison.Ordinal))
                .ToArray();

            Assert.That(runtimeHits, Is.Not.Empty);
            Assert.That(runtimeHits, Is.All.Contains("/Gameplay_Host/Runtime/"));
        }

        [Test]
        [Category("Core")]
        public void AudioVoicePolicy_DocOrName_MakesBatchLocalSemanticsExplicit()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_AudioPolicy/Runtime/AudioVoicePolicy.cs");
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-Audio-Overlap-Refactor-2026-05-18.md");

            Assert.That(source, Does.Contain("max admission count within one presentation batch"));
            Assert.That(doc, Does.Contain("presentation batch admission filter"));
            Assert.That(doc, Does.Contain("does not count already-playing AudioSources"));
        }

        [Test]
        [Category("Core")]
        public void AudioArchitecture_GameplaySfxArbiter_NotReferencedByUiOrBgm()
        {
            AssertFilesDoNotContain(
                EnumerateRepoFiles("Assets/_Features/UI", "*.cs")
                    .Concat(EnumerateRepoFiles("Assets/_Features/Flow/Flow_Audio", "*.cs")),
                "GameplaySfxArbiter");
        }

        [Test]
        [Category("Core")]
        public void AudioArchitecture_SharedAudio_DoesNotReferenceGameplayAudioPolicy()
        {
            AssertFilesDoNotContain(
                EnumerateRepoFiles("Assets/_Shared/Audio/Runtime", "*.cs"),
                "Gameplay.AudioPolicy");
        }

        [Test]
        [Category("Core")]
        public void AudioArchitecture_AudioPlaybackService_DoesNotContainGameplaySemanticNames()
        {
            var source = ReadRepoFile("Assets/_Shared/Audio/Runtime/AudioPlaybackService.cs");

            Assert.That(source, Does.Not.Contain("TileFeature"));
            Assert.That(source, Does.Not.Contain("PlayerDamage"));
            Assert.That(source, Does.Not.Contain("EnemyMovement"));
        }

        [Test]
        [Category("Core")]
        public void AudioArchitecture_GameplayAuthoritativeLogic_DoesNotDirectlyUseIAudioService()
        {
            AssertFilesDoNotContain(
                EnumerateRepoFiles("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime", "*.cs")
                    .Concat(EnumerateRepoFiles("Assets/_Features/Gameplay/Gameplay_Loop/Runtime", "*.cs"))
                    .Concat(EnumerateRepoFiles("Assets/_Features/Gameplay/Gameplay_Entities/Runtime", "*.cs"))
                    .Concat(EnumerateRepoFiles("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime", "*.cs")),
                "IAudioService");
        }

        private static TileFeatureAudioRequest CreateTileRequest(TileFeatureAudioCue cue, int tileId)
        {
            return new TileFeatureAudioRequest(
                cue,
                tileId,
                new SurfaceCell(FaceId.Floor, tileId, 0),
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                new AudioPlaybackContext(debugTag: TileFeatureAudioCueCatalog.Format(cue)));
        }

        private static TileFeatureAudioRequest CreateBurstRequest(
            TileFeatureAudioCue representativeCue,
            TileFeatureAudioCue secondCue)
        {
            var coalescer = new TileFeatureAudioCoalescer();
            var requests = coalescer.Coalesce(
                new[]
                {
                    CreateTileRequest(representativeCue, 1),
                    CreateTileRequest(secondCue, 2),
                },
                tickIndex: 7);
            Assert.That(requests, Has.Count.EqualTo(1));
            return requests[0];
        }

        private static AudioVoicePolicy CreatePolicy(
            AudioVoiceGroupId group,
            int priority,
            int maxGlobal,
            int maxPerOwner = 0,
            float duplicateWindow = 0f)
        {
            return new AudioVoicePolicy(
                group,
                priority,
                maxGlobal,
                maxPerOwner,
                cooldownSecondsGlobal: 0f,
                cooldownSecondsPerOwner: 0f,
                duplicateWindowSeconds: duplicateWindow,
                overflowMode: VoiceOverflowMode.DropNewest);
        }

        private static GameplaySfxRequest CreateSfxRequest(
            AudioDefinition definition,
            AudioVoicePolicy policy,
            string debugTag,
            int sequence,
            int ownerEntityId = 1,
            int tickIndex = 1,
            Component attachOwner = null,
            AudioAttachmentSlot attachmentSlot = default,
            float volumeMultiplier = 1f,
            float pitchMultiplier = 1f)
        {
            return new GameplaySfxRequest(
                definition,
                policy,
                ownerEntityId,
                attachOwner,
                attachmentSlot,
                new AudioPlaybackContext(
                    volumeMultiplier,
                    pitchMultiplier,
                    ownerEntityId: ownerEntityId,
                    debugTag: debugTag),
                tickIndex,
                sequence);
        }

        private static AudioBinding CreateBinding(AudioDefinition definition)
        {
            var binding = new AudioBinding();
            SetField(typeof(AudioBinding), binding, "definition", definition);
            SetField(typeof(AudioBinding), binding, "policy", null);
            return binding;
        }

        private static void SetTileFeatureEntries(
            TileFeatureAudioMap map,
            params (TileFeatureAudioCue cue, AudioBinding binding)[] entries)
        {
            var entryType = typeof(TileFeatureAudioMap).GetNestedType("Entry", BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null);
            var array = Array.CreateInstance(entryType, entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = Activator.CreateInstance(entryType);
                SetField(entryType, entry, "Cue", entries[i].cue);
                SetField(entryType, entry, "Binding", entries[i].binding);
                array.SetValue(entry, i);
            }

            SetField(typeof(TileFeatureAudioMap), map, "entries", array);
        }

        private static bool[] EvaluateLegacyEnemyMoveCadence(
            IReadOnlyList<(int owner, int tick)> events,
            int perEntityMoveMinIntervalTicks,
            int globalMoveMinIntervalTicks,
            int jitterTicks,
            int maxMoveRequestsPerTick)
        {
            var output = new bool[events.Count];
            var nextAllowedTickByEntityId = new Dictionary<int, int>();
            var nextGlobalTick = int.MinValue;
            var currentBudgetTick = int.MinValue;
            var acceptedThisTick = 0;
            for (var i = 0; i < events.Count; i++)
            {
                var item = events[i];
                if (item.tick != currentBudgetTick)
                {
                    currentBudgetTick = item.tick;
                    acceptedThisTick = 0;
                }

                if (item.owner <= 0 ||
                    maxMoveRequestsPerTick <= 0 ||
                    acceptedThisTick >= maxMoveRequestsPerTick ||
                    item.tick < nextGlobalTick ||
                    (nextAllowedTickByEntityId.TryGetValue(item.owner, out var nextOwnerTick) &&
                     item.tick < nextOwnerTick))
                {
                    output[i] = false;
                    continue;
                }

                var jitter = EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(item.owner, item.tick, jitterTicks);
                acceptedThisTick++;
                if (globalMoveMinIntervalTicks > 0)
                {
                    nextGlobalTick = item.tick + globalMoveMinIntervalTicks + jitter;
                }

                nextAllowedTickByEntityId[item.owner] = item.tick + perEntityMoveMinIntervalTicks + jitter;
                output[i] = true;
            }

            return output;
        }

        private static void SetRandomClips(
            RandomAudioDefinition definition,
            params (AudioClip clip, float weight, float volumeTrim, float pitchTrim)[] variants)
        {
            var entryType = typeof(RandomAudioDefinition).GetNestedType("WeightedClip", BindingFlags.NonPublic);
            var array = Array.CreateInstance(entryType, variants.Length);
            for (var i = 0; i < variants.Length; i++)
            {
                var entry = Activator.CreateInstance(entryType);
                SetField(entryType, entry, "Clip", variants[i].clip);
                SetField(entryType, entry, "Weight", variants[i].weight);
                SetField(entryType, entry, "VolumeTrim", variants[i].volumeTrim);
                SetField(entryType, entry, "PitchTrim", variants[i].pitchTrim);
                array.SetValue(entry, i);
            }

            SetField(typeof(RandomAudioDefinition), definition, "clips", array);
        }

        private static void SetField(Type ownerType, object target, string fieldName, object value)
        {
            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{ownerType.Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static AudioClip[] ResolveClipSequence(RandomAudioDefinition definition, int count)
        {
            var sequence = new AudioClip[count];
            for (var i = 0; i < count; i++)
            {
                sequence[i] = definition.Resolve(new AudioPlaybackContext(volumeMultiplier: 1f)).Clip;
            }

            return sequence;
        }

        private static IEnumerable<string> EnumerateRepoFiles(string relativeDirectory, string pattern)
        {
            var root = RepoRoot;
            var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, pattern, SearchOption.AllDirectories)
                    .Select(path => NormalizeRepoPath(Path.GetRelativePath(root, path)))
                : Array.Empty<string>();
        }

        private static void AssertFilesDoNotContain(IEnumerable<string> relativePaths, string forbiddenText)
        {
            var hits = relativePaths
                .Where(path => ReadRepoFile(path).Contains(forbiddenText, StringComparison.Ordinal))
                .ToArray();
            Assert.That(hits, Is.Empty, $"{forbiddenText} found in: {string.Join(", ", hits)}");
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string NormalizeRepoPath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private sealed class RecordingAudioPolicyDiagnostics : IAudioPolicyDiagnostics
        {
            public readonly List<string> Warnings = new();
            public readonly List<string> Errors = new();

            public void Warning(string message)
            {
                Warnings.Add(message);
            }

            public void Error(string message)
            {
                Errors.Add(message);
            }
        }

        private sealed class RecordingGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort
        {
            public readonly List<TwoDCall> TwoDCalls = new();
            public readonly List<AttachedCall> AttachedCalls = new();

            public void Play2D(AudioDefinition definition, in AudioPlaybackContext context)
            {
                TwoDCalls.Add(new TwoDCall(definition, context));
            }

            public void PlayAttached(
                AudioDefinition definition,
                Component owner,
                AudioAttachmentSlot slot,
                in AudioPlaybackContext context)
            {
                AttachedCalls.Add(new AttachedCall(definition, owner, slot, context));
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

        private sealed class TestAudioPlaybackPolicy : AudioPlaybackPolicy
        {
        }
    }
}
