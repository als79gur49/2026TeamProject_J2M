using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class AudioRepositoryAssetSmokeCoreTests
    {
        [Test]
        [Category("Core")]
        public void AudioDefinitions_RepositoryAssets_ValidateAll()
        {
            var definitions = LoadAllAssets<AudioDefinition>();
            Assert.That(definitions, Is.Not.Empty, "Repository scan found no AudioDefinition assets.");

            var failures = new List<string>();
            foreach (var definition in definitions)
            {
                try
                {
                    AssertDefinitionContract(definition);
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(definition)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "AudioDefinition repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void AudioBindings_RepositoryAssets_ValidateAll()
        {
            var failures = new List<string>();
            AppendValidationFailures(LoadAllAssets<GameplayAudioMap>(), map => map.ValidateOrThrow(), failures);
            AppendValidationFailures(LoadAllAssets<GameplayActionAudioProfile>(), profile => profile.ValidateOrThrow(), failures);
            AppendValidationFailures(LoadAllAssets<BgmProfile>(), profile => profile.ValidateOrThrow(), failures);

            Assert.That(failures, Is.Empty, "AudioBinding owner repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioMaps_RepositoryAssets_AllRequiredSemanticsValidate()
        {
            var maps = LoadAllAssets<GameplayAudioMap>();
            Assert.That(maps, Is.Not.Empty, "Repository scan found no GameplayAudioMap assets.");

            var failures = new List<string>();
            foreach (var map in maps)
            {
                try
                {
                    map.ValidateRequiredSemanticsOrThrow(GameplayAudioSemanticCatalog.RequiredOneShotV1);
                    foreach (var semanticId in GameplayAudioSemanticCatalog.RequiredOneShotV1)
                    {
                        var binding = map.ResolveOrThrow(semanticId);
                        Assert.That(binding.Definition.Category, Is.EqualTo(AudioCategory.Sfx), Describe(map));
                        Assert.That(binding.Definition.Loop, Is.False, Describe(map));
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(map)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "GameplayAudioMap repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void GameplayActionAudioProfiles_RepositoryAssets_ValidateAll()
        {
            var profiles = LoadAllAssets<GameplayActionAudioProfile>();
            Assert.That(profiles, Is.Not.Empty, "Repository scan found no GameplayActionAudioProfile assets.");

            var failures = new List<string>();
            foreach (var profile in profiles)
            {
                try
                {
                    profile.ValidateOrThrow();
                    Assert.That(
                        profile.CollectDiagnostics()
                            .Where(diagnostic => diagnostic.Severity == GameplayActionAudioProfileDiagnosticSeverity.Error)
                            .Select(diagnostic => diagnostic.Message)
                            .ToArray(),
                        Is.Empty,
                        Describe(profile));
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(profile)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "GameplayActionAudioProfile repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void BgmProfiles_RepositoryAssets_ValidateAll()
        {
            var profiles = LoadAllAssets<BgmProfile>();
            Assert.That(profiles, Is.Not.Empty, "Repository scan found no BgmProfile assets.");

            var failures = new List<string>();
            foreach (var profile in profiles)
            {
                try
                {
                    profile.ValidateOrThrow();
                    Assert.That(profile.LoopDefinition.Category, Is.EqualTo(AudioCategory.Bgm), Describe(profile));
                    Assert.That(profile.LoopDefinition.Loop, Is.True, Describe(profile));
                    Assert.That(
                        new[] { BgmTransitionMode.Immediate, BgmTransitionMode.FadeOutIn, BgmTransitionMode.Crossfade },
                        Does.Contain(profile.TransitionMode),
                        Describe(profile));
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(profile)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "BgmProfile repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void GameplayActionAudio_AttemptOnlyBlockedCancelFrames_NoException()
        {
            var planner = new GameplayActionAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                Array.Empty<TickPlayerActionPresentationSignal>(),
                new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        Direction.Right,
                        PlayerActionAttemptFeedbackKind.AssistOutOfRange),
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        Direction.Right,
                        PlayerActionAttemptFeedbackKind.NoTarget),
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        Direction.Right,
                        PlayerActionAttemptFeedbackKind.Invalid,
                        emitsVisualFeedback: false),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => request.Moment).ToArray(),
                Is.EqualTo(new[]
                {
                    GameplayActionAudioMoment.AssistOutOfRange,
                    GameplayActionAudioMoment.NoTarget,
                    GameplayActionAudioMoment.Invalid,
                }));
        }

        private static void AssertDefinitionContract(AudioDefinition definition)
        {
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Category, Is.Not.EqualTo(AudioCategory.Master), Describe(definition));
            Assert.That(definition.DefaultVolumeTrim, Is.InRange(0f, 1f), Describe(definition));
            Assert.That(IsPositiveFinite(definition.PitchRange.x), Is.True, Describe(definition));
            Assert.That(IsPositiveFinite(definition.PitchRange.y), Is.True, Describe(definition));

            if (definition is SingleAudioDefinition)
            {
                var clip = new SerializedObject(definition).FindProperty("clip").objectReferenceValue;
                Assert.That(clip, Is.Not.Null, Describe(definition));
            }

            if (definition is RandomAudioDefinition)
            {
                AssertRandomDefinitionVariants(definition);
            }

            var playbackData = definition.Resolve(default);
            Assert.That(playbackData.Clip, Is.Not.Null, Describe(definition));
        }

        private static void AssertRandomDefinitionVariants(AudioDefinition definition)
        {
            var clipsProperty = new SerializedObject(definition).FindProperty("clips");
            Assert.That(clipsProperty, Is.Not.Null, Describe(definition));
            Assert.That(clipsProperty.arraySize, Is.GreaterThan(0), Describe(definition));
            for (var i = 0; i < clipsProperty.arraySize; i++)
            {
                var entry = clipsProperty.GetArrayElementAtIndex(i);
                Assert.That(entry.FindPropertyRelative("Clip").objectReferenceValue, Is.Not.Null, $"{Describe(definition)} variant {i}");
                Assert.That(IsPositiveFinite(entry.FindPropertyRelative("Weight").floatValue), Is.True, $"{Describe(definition)} variant {i}");
                Assert.That(IsPositiveFinite(entry.FindPropertyRelative("VolumeTrim").floatValue), Is.True, $"{Describe(definition)} variant {i}");
                Assert.That(IsPositiveFinite(entry.FindPropertyRelative("PitchTrim").floatValue), Is.True, $"{Describe(definition)} variant {i}");
            }
        }

        private static TickResult CreateTickResult(TickPresentationData presentationData)
        {
            return new TickResult(
                1,
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

        private static TickPresentationData CreatePresentationData(
            TickPlayerActionPresentationSignal[] playerActionSignals,
            TickPlayerActionAttemptPresentationSignal[] playerActionAttemptSignals)
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
                Array.Empty<TickEntityExitPresentationSignal>(),
                playerActionAttemptSignals ?? Array.Empty<TickPlayerActionAttemptPresentationSignal>());
        }

        private static IReadOnlyList<T> LoadAllAssets<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            Array.Sort(guids, StringComparer.Ordinal);
            return guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToArray();
        }

        private static void AppendValidationFailures<T>(
            IEnumerable<T> assets,
            Action<T> validate,
            ICollection<string> failures)
            where T : UnityEngine.Object
        {
            foreach (var asset in assets)
            {
                try
                {
                    validate(asset);
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(asset)}: {exception.GetType().Name}: {exception.Message}");
                }
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string Describe(UnityEngine.Object asset)
        {
            return asset == null
                ? "<null>"
                : $"{asset.GetType().Name} '{asset.name}' Path='{AssetDatabase.GetAssetPath(asset)}'";
        }
    }
}
