using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayActionAudioRuntimeTests
    {
        private const string PlayerPrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";
        private const string PlayerActionAudioProfilePath =
            "Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset";

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioProfile_ValidateOrThrow_AllowsEmptyAndPartialProfiles()
        {
            var emptyProfile = ScriptableObject.CreateInstance<GameplayActionAudioProfile>();
            using var partialProfile = CreateActionAudioProfile(
                new ActionAudioEntrySpec(GameplayActionKind.Push, GameplayActionAudioMoment.Windup, CreateDefinitionSpec()));

            try
            {
                Assert.DoesNotThrow(() => emptyProfile.ValidateOrThrow());
                Assert.DoesNotThrow(() => partialProfile.Profile.ValidateOrThrow());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(emptyProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioProfile_ValidateOrThrow_RejectsDuplicateActionMomentEntries()
        {
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(GameplayActionKind.Push, GameplayActionAudioMoment.Windup, CreateDefinitionSpec()),
                new ActionAudioEntrySpec(GameplayActionKind.Push, GameplayActionAudioMoment.Windup, CreateDefinitionSpec()));

            var exception = Assert.Throws<InvalidOperationException>(() => profileBundle.Profile.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("duplicate gameplay action audio entry 'Push/Windup'"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioProfile_ValidateOrThrow_RejectsNonOptionalNullBinding()
        {
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    definitionSpec: null,
                    isOptional: false));

            var exception = Assert.Throws<InvalidOperationException>(() => profileBundle.Profile.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("entry 'Push/Windup' is missing an AudioBinding"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioProfile_ValidateOrThrow_AllowsExplicitOptionalNullBinding()
        {
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    definitionSpec: null,
                    isOptional: true));

            Assert.DoesNotThrow(() => profileBundle.Profile.ValidateOrThrow());
            Assert.That(
                profileBundle.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Windup, out _),
                Is.False);
            Assert.That(
                profileBundle.Profile.CollectDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == GameplayActionAudioProfileDiagnosticSeverity.Warning)
                    .Select(diagnostic => diagnostic.Message)
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    $"{profileBundle.Profile.name} optional entry 'Push/Windup' has no assigned AudioBinding.",
                }));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Category("Core")]
        public void GameplayActionAudioProfile_ValidateOrThrow_RejectsRemovedMomentValues(int removedMomentValue)
        {
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    (GameplayActionAudioMoment)removedMomentValue,
                    CreateDefinitionSpec()));

            var exception = Assert.Throws<InvalidOperationException>(() => profileBundle.Profile.ValidateOrThrow());
            Assert.That(
                exception.Message,
                Does.Contain($"unsupported gameplay action audio moment value '{removedMomentValue}'"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioProfile_ValidateOrThrow_RejectsBindingWithNullDefinition()
        {
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    definitionSpec: null,
                    isOptional: true,
                    createBindingWithNullDefinition: true));

            var exception = Assert.Throws<InvalidOperationException>(() => profileBundle.Profile.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("entry 'Push/Windup' is missing an AudioDefinition binding"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioProfile_ValidateOrThrow_RejectsNonSfxDefinitions()
        {
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec(category: AudioCategory.Ui)));

            var exception = Assert.Throws<InvalidOperationException>(() => profileBundle.Profile.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("only [Sfx] are allowed"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioProfile_ValidateOrThrow_RejectsLoopingDefinitions()
        {
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec(loop: true)));

            var exception = Assert.Throws<InvalidOperationException>(() => profileBundle.Profile.ValidateOrThrow());
            Assert.That(exception.Message, Does.Contain("only allows one-shot definitions"));
        }

        [Test]
        [Category("Full")]
        public void PlayerViewPrefabRequirements_OptionalGameplayActionAudioAuthoring_RejectsNullProfile()
        {
            var prefabObject = new GameObject(nameof(PlayerViewPrefabRequirements_OptionalGameplayActionAudioAuthoring_RejectsNullProfile));
            try
            {
                var view = prefabObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);
                prefabObject.AddComponent<PlayerAnimatorDriver>();
                prefabObject.AddComponent<PlayerAnimationTimingAuthoring>();
                prefabObject.AddComponent<GameplayActionAudioAuthoring>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => PlayerViewPrefabRequirements.ValidatePlayerViewPrefab(view, nameof(GameplayActionAudioRuntimeTests)));
                Assert.That(exception.Message, Does.Contain("requires a non-null GameplayActionAudioProfile"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayActionAudioPrefabRequirements_OptionalEnemyAuthoring_RejectsNullProfile()
        {
            var prefabObject = new GameObject(nameof(GameplayActionAudioPrefabRequirements_OptionalEnemyAuthoring_RejectsNullProfile));
            try
            {
                var view = prefabObject.AddComponent<GameplayEntityView>();
                view.Initialize(20);
                prefabObject.AddComponent<EnemyAnimatorDriver>();
                prefabObject.AddComponent<GameplayActionAudioAuthoring>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => GameplayActionAudioPrefabRequirements.GetOptionalValidatedAuthoring(view, nameof(GameplayActionAudioRuntimeTests)));
                Assert.That(exception.Message, Does.Contain("requires a non-null GameplayActionAudioProfile"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultGameplayEntityViewFactory_PrimitivePlayerFallback_DoesNotAutoAddGameplayActionAudioAuthoring()
        {
            var rootObject = new GameObject(nameof(DefaultGameplayEntityViewFactory_PrimitivePlayerFallback_DoesNotAutoAddGameplayActionAudioAuthoring));
            try
            {
                var factory = new DefaultGameplayEntityViewFactory(rootObject.transform, 1f, playerEntityId: 10);
                var view = factory.CreateView(CreateUnit(10, UnitRole.Player));

                Assert.That(view.GetComponent<GameplayActionAudioAuthoring>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void PlayerS1Prefab_HasGameplayActionAudioAuthoring_WithRetainedV1Coverage()
        {
            var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(PlayerPrefabPath);
            Assert.That(view, Is.Not.Null, $"Missing prefab at '{PlayerPrefabPath}'.");

            var authoring = view.GetComponent<GameplayActionAudioAuthoring>();
            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(GameplayActionAudioAuthoring)} on '{PlayerPrefabPath}'.");
            Assert.That(authoring.gameObject, Is.SameAs(view.gameObject));
            Assert.DoesNotThrow(() => authoring.Validate());

            Assert.That(authoring.Profile, Is.Not.Null);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Windup, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.AssistOutOfRange, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.NoTarget, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Invalid, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Flip, GameplayActionAudioMoment.Windup, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Flip, GameplayActionAudioMoment.AssistOutOfRange, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Flip, GameplayActionAudioMoment.NoTarget, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Flip, GameplayActionAudioMoment.Invalid, out _), Is.True);

            Assert.That(AssetDatabase.GetAssetPath(authoring.Profile), Is.EqualTo(PlayerActionAudioProfilePath));
            Assert.That(
                authoring.Profile.CollectDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == GameplayActionAudioProfileDiagnosticSeverity.Warning)
                    .Select(diagnostic => diagnostic.Message)
                    .ToArray(),
                Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GameplayActionAudioMoment_PublicSurface_ExcludesRemovedLifecycleMoments()
        {
            var names = Enum.GetNames(typeof(GameplayActionAudioMoment));

            Assert.That(names, Does.Not.Contain("Execute"));
            Assert.That(names, Does.Not.Contain("Recovery"));
            Assert.That(names, Does.Not.Contain("Contact"));
            Assert.That(names, Does.Not.Contain("ImpactEnemy"));
            Assert.That(names, Does.Not.Contain("Blocked"));
            Assert.That(names, Does.Contain(nameof(GameplayActionAudioMoment.Windup)));
            Assert.That(names, Does.Contain(nameof(GameplayActionAudioMoment.AssistOutOfRange)));
            Assert.That(names, Does.Contain(nameof(GameplayActionAudioMoment.NoTarget)));
            Assert.That(names, Does.Contain(nameof(GameplayActionAudioMoment.Invalid)));
        }

        [Test]
        [Category("Core")]
        public void PlayerS1GameplayActionAudioProfile_DoesNotAuthorRemovedMoments()
        {
            var profileYaml = File.ReadAllText(PlayerActionAudioProfilePath);

            Assert.That(profileYaml, Does.Not.Contain("Moment: 1"));
            Assert.That(profileYaml, Does.Not.Contain("Moment: 2"));
            Assert.That(profileYaml, Does.Not.Contain("Moment: 3"));
            Assert.That(profileYaml, Does.Not.Contain("Moment: 4"));
            Assert.That(profileYaml, Does.Not.Contain("Moment: 5"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioRequestPlanner_PushImpactRecovery_EmitsWindupOnly()
        {
            var planner = new GameplayActionAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.Push,
                    activeActionSequence: 1,
                    startedThisTick: true,
                    completedThisTick: false,
                    canceledThisTick: false,
                    executedThisTick: true,
                    isRecoveryPhase: true,
                    resolutionKind: TickPlayerActionResolutionKind.Impact)));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => request.Moment).ToArray(),
                Is.EqualTo(new[]
                {
                    GameplayActionAudioMoment.Windup,
                }));
            Assert.That(requests.All(request => request.Action == GameplayActionKind.Push), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioRequestPlanner_BlockedPush_EmitsNoActionAudio()
        {
            var planner = new GameplayActionAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.Push,
                    activeActionSequence: 1,
                    startedThisTick: false,
                    completedThisTick: false,
                    canceledThisTick: false,
                    executedThisTick: true,
                    resolutionKind: TickPlayerActionResolutionKind.Blocked)));

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioRequestPlanner_FlipContactTiming_DoesNotEmitRemovedContactMoment()
        {
            var planner = new GameplayActionAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.Flip,
                    activeActionSequence: 1,
                    startedThisTick: true,
                    completedThisTick: false,
                    canceledThisTick: false,
                    hasFlipImpactContactTiming: true)));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => request.Moment).ToArray(),
                Is.EqualTo(new[]
                {
                    GameplayActionAudioMoment.Windup,
                }));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioRequestPlanner_SameTickDuplicates_ArePreserved()
        {
            var planner = new GameplayActionAudioRequestPlanner();
            var signal = new TickPlayerActionPresentationSignal(
                entityId: 10,
                activeActionKind: PlayerActionKind.Push,
                activeActionSequence: 1,
                startedThisTick: true,
                completedThisTick: false,
                canceledThisTick: false);

            var requests = planner.BuildRequests(CreateTickResult(CreatePresentationData(signal, signal)));

            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests.All(request => request.Moment == GameplayActionAudioMoment.Windup), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioRequestPlanner_IgnoresSignalsWithoutPushOrFlip()
        {
            var planner = new GameplayActionAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.None,
                    activeActionSequence: 0,
                    startedThisTick: true,
                    completedThisTick: false,
                    canceledThisTick: false)));

            Assert.That(planner.BuildRequests(result), Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GameplayActionAudioRequestPlanner_AttemptFailures_MapToFailureMomentsOnly()
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
            Assert.That(requests.Any(request => request.Moment == GameplayActionAudioMoment.Windup), Is.False);
            Assert.That(requests.Select(request => request.Context.DebugTag).ToArray(), Does.Not.Contain("Action:Push:Execute"));
            Assert.That(requests.Select(request => request.Context.DebugTag).ToArray(), Does.Not.Contain("Action:Push:Recovery"));
            Assert.That(requests.Select(request => request.Context.DebugTag).ToArray(), Does.Not.Contain("Action:Flip:Execute"));
            Assert.That(requests.Select(request => request.Context.DebugTag).ToArray(), Does.Not.Contain("Action:Flip:Recovery"));
        }

        [Test]
        [Category("Core")]
        public void GameplayActionAudioRequestPlanner_DoesNotEmitRemovedMoments()
        {
            var planner = new GameplayActionAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(new[]
            {
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.Push,
                    activeActionSequence: 1,
                    startedThisTick: true,
                    completedThisTick: false,
                    canceledThisTick: false,
                    executedThisTick: true,
                    isRecoveryPhase: true,
                    resolutionKind: TickPlayerActionResolutionKind.Impact),
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.Push,
                    activeActionSequence: 1,
                    startedThisTick: false,
                    completedThisTick: false,
                    canceledThisTick: false,
                    executedThisTick: true,
                    resolutionKind: TickPlayerActionResolutionKind.Blocked),
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.Flip,
                    activeActionSequence: 1,
                    startedThisTick: true,
                    completedThisTick: false,
                    canceledThisTick: false,
                    hasFlipImpactContactTiming: true),
            }));

            var requestDebugTags = planner.BuildRequests(result)
                .Select(request => request.Context.DebugTag)
                .ToArray();

            Assert.That(requestDebugTags, Is.EqualTo(new[] { "Action:Push:Windup", "Action:Flip:Windup" }));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Push:Execute"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Push:Recovery"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Push:Contact"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Push:ImpactEnemy"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Push:Blocked"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Flip:Execute"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Flip:Recovery"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Flip:Contact"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Flip:ImpactEnemy"));
            Assert.That(requestDebugTags, Does.Not.Contain("Action:Flip:Blocked"));
        }

        [Test]
        [Category("Core")]
        public void ActionAudioFactExtraction_ObservesCurrentV1MomentsAsSemanticFacts()
        {
            var result = CreateTickResult(CreatePresentationData(
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 11,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        targetEntityId: 20,
                        direction: Direction.Right,
                        actionPlanId: 101),
                    new TickPlayerActionPresentationSignal(
                        entityId: 12,
                        activeActionKind: PlayerActionKind.Flip,
                        activeActionSequence: 12,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        targetEntityId: 30,
                        direction: Direction.Left,
                        actionPlanId: 102),
                    new TickPlayerActionPresentationSignal(
                        entityId: 14,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 13,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Blocked),
                },
                new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        Direction.Up,
                        PlayerActionAttemptFeedbackKind.AssistOutOfRange),
                    new TickPlayerActionAttemptPresentationSignal(
                        12,
                        PlayerActionKind.Flip,
                        Direction.Down,
                        PlayerActionAttemptFeedbackKind.Invalid,
                        targetEntityId: 30,
                        hasTarget: true),
                }));

            var frame = new TickPresentationFactExtractor().Extract(result);
            var facts = frame.Facts
                .Where(fact => fact.Kind == PresentationFactKind.ActionAudio)
                .ToArray();

            Assert.That(frame.Diagnostics.ActionAudioFactCount, Is.EqualTo(4));
            Assert.That(facts.Select(fact => fact.Source.SemanticSource).ToArray(), Is.EqualTo(new[]
            {
                PresentationSemanticSource.PlayerActionAudio,
                PresentationSemanticSource.PlayerActionAudio,
                PresentationSemanticSource.PlayerActionAttemptAudio,
                PresentationSemanticSource.PlayerActionAttemptAudio,
            }));
            AssertActionAudioPayload(
                facts[0].ActionAudioPayload,
                10,
                GameplayActionKind.Push,
                GameplayActionAudioMoment.Windup,
                sourceSequenceId: 11,
                sourceActionPlanId: 101,
                targetEntityId: 20,
                Direction.Right,
                PresentationActionAudioOutcomeKind.Started);
            AssertActionAudioPayload(
                facts[1].ActionAudioPayload,
                12,
                GameplayActionKind.Flip,
                GameplayActionAudioMoment.Windup,
                sourceSequenceId: 12,
                sourceActionPlanId: 102,
                targetEntityId: 30,
                Direction.Left,
                PresentationActionAudioOutcomeKind.Started);
            AssertActionAudioPayload(
                facts[2].ActionAudioPayload,
                10,
                GameplayActionKind.Push,
                GameplayActionAudioMoment.AssistOutOfRange,
                sourceSequenceId: 1,
                sourceActionPlanId: 0,
                targetEntityId: 0,
                Direction.Up,
                PresentationActionAudioOutcomeKind.AttemptFeedback);
            AssertActionAudioPayload(
                facts[3].ActionAudioPayload,
                12,
                GameplayActionKind.Flip,
                GameplayActionAudioMoment.Invalid,
                sourceSequenceId: 2,
                sourceActionPlanId: 0,
                targetEntityId: 30,
                Direction.Down,
                PresentationActionAudioOutcomeKind.AttemptFeedback);
            Assert.That(
                facts.Select(fact => (GameplayActionAudioMoment)fact.ActionAudioPayload.Moment).ToArray(),
                Has.No.Member((GameplayActionAudioMoment)1));
        }

        [Test]
        [Category("Core")]
        public void ActionAudioCuePlanning_UsesTypedActionAudioVocabularySeparateFromCoreSfx()
        {
            var result = CreateTickResult(CreatePresentationData(
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false),
                    new TickPlayerActionPresentationSignal(
                        entityId: 20,
                        activeActionKind: PlayerActionKind.Flip,
                        activeActionSequence: 2,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false),
                },
                new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        Direction.Right,
                        PlayerActionAttemptFeedbackKind.NoTarget),
                    new TickPlayerActionAttemptPresentationSignal(
                        20,
                        PlayerActionKind.Flip,
                        Direction.Left,
                        PlayerActionAttemptFeedbackKind.AssistOutOfRange),
                }));
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var actionAudioCueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new ActionAudioCuePlanner(),
            }).Plan(factFrame);
            var coreSfxCueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(factFrame);

            Assert.That(actionAudioCueFrame.Cues.Select(cue => cue.Key.LocalKey).ToArray(), Is.EqualTo(new[]
            {
                (int)PresentationActionAudioCueKey.PlayerPushWindup,
                (int)PresentationActionAudioCueKey.PlayerFlipWindup,
                (int)PresentationActionAudioCueKey.PlayerPushNoTarget,
                (int)PresentationActionAudioCueKey.PlayerFlipAssistOutOfRange,
            }));
            Assert.That(actionAudioCueFrame.Cues.All(cue => cue.Domain == PresentationDomain.ActionAudio), Is.True);
            Assert.That(actionAudioCueFrame.Cues.All(cue => cue.Key.TryGetActionAudioCueKey(out _)), Is.True);
            Assert.That(actionAudioCueFrame.Cues.Any(cue => cue.Key.TryGetSfxCueKey(out _)), Is.False);
            Assert.That(coreSfxCueFrame.Cues, Is.Empty);
            Assert.That(
                actionAudioCueFrame.Cues.Select(cue => (GameplayActionAudioMoment)cue.ActionAudioPayload.Moment).ToArray(),
                Is.EqualTo(new[]
                {
                    GameplayActionAudioMoment.Windup,
                    GameplayActionAudioMoment.Windup,
                    GameplayActionAudioMoment.NoTarget,
                    GameplayActionAudioMoment.AssistOutOfRange,
                }));
        }

        [Test]
        [Category("Core")]
        public void ActionAudioPlaybackPlan_IsPlanningOnlyOneShotAndNonBlocking()
        {
            var result = CreateTickResult(CreatePresentationData(
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.Push,
                    activeActionSequence: 1,
                    startedThisTick: true,
                    completedThisTick: false,
                    canceledThisTick: false)));
            var pipeline = GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();

            pipeline.Present(result);

            Assert.That(pipeline.LastFactFrame.Diagnostics.ActionAudioFactCount, Is.EqualTo(1));
            Assert.That(
                pipeline.LastCueFrame.Cues.Count(cue => cue.Domain == PresentationDomain.ActionAudio),
                Is.EqualTo(1));
            var actionAudioPlaybackCue = pipeline.LastPlaybackPlan.Cues
                .Single(cue => cue.Cue.Domain == PresentationDomain.ActionAudio);
            Assert.That(actionAudioPlaybackCue.Policy.UnitKind, Is.EqualTo(PresentationPlaybackUnitKind.OneShot));
            Assert.That(actionAudioPlaybackCue.Policy.Blocking, Is.False);
            Assert.That(pipeline.LastPlaybackPlan.Tracks, Is.Empty);
            Assert.That(pipeline.LastPlaybackPlan.Barriers, Is.Empty);
            Assert.That(pipeline.LastPlaybackPlan.Diagnostics.ActionAudioCueCount, Is.EqualTo(1));
            Assert.That(pipeline.LastPlaybackPlan.Diagnostics.ActionAudioPlaybackCueCount, Is.EqualTo(1));
            Assert.That(
                pipeline.LastPlaybackPlan.Diagnostics.ActionAudioNoPlaybackBecausePlanningOnlyCount,
                Is.EqualTo(1));
            Assert.That(pipeline.BlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(pipeline.BlockingSnapshot.HasActiveBlockingPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void ActionAudioPlanning_LegacyRequestPlannerParity_UsesCurrentV1Moments()
        {
            var result = CreateTickResult(CreatePresentationData(
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 4,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false),
                    new TickPlayerActionPresentationSignal(
                        entityId: 20,
                        activeActionKind: PlayerActionKind.Flip,
                        activeActionSequence: 5,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false),
                },
                new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        Direction.Right,
                        PlayerActionAttemptFeedbackKind.AssistOutOfRange),
                    new TickPlayerActionAttemptPresentationSignal(
                        20,
                        PlayerActionKind.Flip,
                        Direction.Left,
                        PlayerActionAttemptFeedbackKind.Invalid),
                }));
            var legacyRequests = new GameplayActionAudioRequestPlanner().BuildRequests(result);
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new ActionAudioCuePlanner(),
            }).Plan(factFrame);

            Assert.That(cueFrame.Cues, Has.Count.EqualTo(legacyRequests.Count));
            for (var i = 0; i < legacyRequests.Count; i++)
            {
                Assert.That(
                    (GameplayActionKind)cueFrame.Cues[i].ActionAudioPayload.ActionKind,
                    Is.EqualTo(legacyRequests[i].Action));
                Assert.That(
                    (GameplayActionAudioMoment)cueFrame.Cues[i].ActionAudioPayload.Moment,
                    Is.EqualTo(legacyRequests[i].Moment));
                Assert.That(cueFrame.Cues[i].ActionAudioPayload.OwnerEntityId, Is.EqualTo(legacyRequests[i].OwnerEntityId));
                Assert.That(cueFrame.Cues[i].Target, Is.EqualTo(PresentationTarget.Entity(legacyRequests[i].OwnerEntityId)));
                Assert.That(cueFrame.Cues[i].Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
                Assert.That(cueFrame.Cues[i].Source.TickIndex, Is.EqualTo(result.TickIndex));
                Assert.That(cueFrame.Cues[i].PolicyHint.Kind, Is.EqualTo(PresentationPlaybackPolicyHintKind.OneShot));
                Assert.That(cueFrame.Cues[i].PolicyHint.Blocking, Is.False);
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudioPlanningOnly_DoesNotChangeLegacyPlaybackCount()
        {
            var rootObject = new GameObject(nameof(ActionAudioPlanningOnly_DoesNotChangeLegacyPlaybackCount));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var result = CreateTickResult(
                    CreatePresentationData(
                        new TickPlayerActionPresentationSignal(
                            entityId: 10,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 1,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false)),
                    new[] { CreateUnit(10, UnitRole.Player) });
                var diagnosticsPipeline = GameplayPresentationPipelineInstaller.CreateDiagnosticsOnly();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { CreateUnit(10, UnitRole.Player) }, new CubeTopologyState(FaceId.Floor));
                diagnosticsPipeline.Present(result);
                presenter.Present(result);

                Assert.That(diagnosticsPipeline.LastPlaybackPlan.Diagnostics.ActionAudioPlaybackCueCount, Is.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.TwoDCalls[0].Context.DebugTag, Is.EqualTo("Action:Push:Windup"));
                Assert.That(playbackPort.AttachedCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_MissingGameplayActionAudioAuthoring_IsRuntimeNoOp()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_MissingGameplayActionAudioAuthoring_IsRuntimeNoOp));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { CreateUnit(10, UnitRole.Player) }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    new[] { CreateUnit(10, UnitRole.Player) }));

                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_OptionalUnassignedActionCue_IsRuntimeNoOp()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_OptionalUnassignedActionCue_IsRuntimeNoOp));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    definitionSpec: null,
                    isOptional: true));
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { CreateUnit(10, UnitRole.Player) }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    new[] { CreateUnit(10, UnitRole.Player) }));

                Assert.That(
                    profileBundle.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Windup, out _),
                    Is.False);
                Assert.That(playbackPort.AttachedCalls, Is.Empty);
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_ActionAudio_AttachesWhenProfileUsesAttachmentSlot()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_ActionAudio_AttachesWhenProfileUsesAttachmentSlot));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec(),
                    attachmentSlot: AudioAttachmentSlot.FromId("body")));
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { CreateUnit(10, UnitRole.Player) }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    new[] { CreateUnit(10, UnitRole.Player) }));

                Assert.That(playbackPort.AttachedCalls, Has.Count.EqualTo(1));
                Assert.That(playbackPort.AttachedCalls[0].Owner.EntityId, Is.EqualTo(10));
                Assert.That(playbackPort.AttachedCalls[0].Slot.Id, Is.EqualTo("body"));
                Assert.That(playbackPort.AttachedCalls[0].Context.DebugTag, Is.EqualTo("Action:Push:Windup"));
                Assert.That(playbackPort.TwoDCalls, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_PushImpact_PreservesCoreEnemyDamageWithoutActionImpact()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_PushImpact_PreservesCoreEnemyDamageWithoutActionImpact));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { CreateUnit(10, UnitRole.Player) }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(
                                entityId: 10,
                                activeActionKind: PlayerActionKind.Push,
                                activeActionSequence: 1,
                                startedThisTick: false,
                                completedThisTick: false,
                                canceledThisTick: false,
                                executedThisTick: true,
                                resolutionKind: TickPlayerActionResolutionKind.Impact),
                        },
                        new[]
                        {
                            new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 1),
                        }),
                    new[] { CreateUnit(10, UnitRole.Player) }));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[]
                    {
                        "EnemyDamage",
                    }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayActionAudio_Windup_PlaysDuringTopologyAudioGate()
        {
            var rootObject = new GameObject(nameof(GameplayActionAudio_Windup_PlaysDuringTopologyAudioGate));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var player = CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Front, 0, 0));

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player }, sourceTopology);
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(
                                entityId: player.entityId,
                                activeActionKind: PlayerActionKind.Push,
                                activeActionSequence: 1,
                                startedThisTick: true,
                                completedThisTick: false,
                                canceledThisTick: false,
                                resolutionKind: TickPlayerActionResolutionKind.Impact),
                        },
                        Array.Empty<TickEnemyDamagePresentationSignal>(),
                        new TickTopologyMotion(
                            sourceTopology,
                            destinationTopology,
                            CubeRotationKind.Forward)),
                    new[] { player },
                    destinationTopology));

                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Action:Push:Windup" }));

                presenter.UpdatePresentation(0.2f);

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Action:Push:Windup" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayActionAudio_PlayerActionAnimationExecutorMode_DoesNotChangeActionAudioLane()
        {
            var legacyRoot = new GameObject(nameof(GameplayActionAudio_PlayerActionAnimationExecutorMode_DoesNotChangeActionAudioLane) + "_Legacy");
            var executorRoot = new GameObject(nameof(GameplayActionAudio_PlayerActionAnimationExecutorMode_DoesNotChangeActionAudioLane) + "_Executor");
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec()));
            try
            {
                var legacyPresenter = CreatePresenter(
                    legacyRoot,
                    new ActionAudioViewFactory(legacyRoot.transform, profileBundle.Profile));
                var executorPresenter = CreatePresenter(
                    executorRoot,
                    new ActionAudioViewFactory(executorRoot.transform, profileBundle.Profile));
                var legacyAudioPort = new RecordingGameplayAudioPlaybackPort();
                var executorAudioPort = new RecordingGameplayAudioPlaybackPort();
                var legacyAnimationPort = new RecordingGameplayAnimationPlaybackPort();
                var executorAnimationPort = new RecordingGameplayAnimationPlaybackPort();
                var player = CreateUnit(10, UnitRole.Player);
                var tick = CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    new[] { player });

                legacyPresenter.AttachGameplayAudioRuntime(legacyAudioPort, mapBundle.Map);
                executorPresenter.AttachGameplayAudioRuntime(executorAudioPort, mapBundle.Map);
                legacyPresenter.ConfigurePlayerActionAnimationExecution(
                    PlayerActionAnimationExecutionMode.LegacyAnimationSync,
                    legacyAnimationPort);
                executorPresenter.ConfigurePlayerActionAnimationExecution(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    executorAnimationPort);
                legacyPresenter.PresentInitial(new[] { player }, new CubeTopologyState(FaceId.Floor));
                executorPresenter.PresentInitial(new[] { player }, new CubeTopologyState(FaceId.Floor));

                legacyPresenter.Present(tick);
                executorPresenter.Present(tick);

                Assert.That(legacyAudioPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[] { "Action:Push:Windup" }));
                Assert.That(executorAudioPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[] { "Action:Push:Windup" }));
                Assert.That(legacyAudioPort.AttachedCalls, Is.Empty);
                Assert.That(executorAudioPort.AttachedCalls, Is.Empty);
                Assert.That(legacyAnimationPort.TryPlayCallCount, Is.Zero);
                Assert.That(executorAnimationPort.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(Enum.GetNames(typeof(GameplayActionAudioMoment)), Does.Contain(nameof(GameplayActionAudioMoment.Windup)));
                Assert.That(Enum.GetNames(typeof(GameplayActionAudioMoment)), Does.Not.Contain("Execute"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacyRoot);
                UnityEngine.Object.DestroyImmediate(executorRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_DefaultMode_UsesLegacyControllerAndDoesNotCallBridgePort()
        {
            var rootObject = new GameObject(nameof(ActionAudio_DefaultMode_UsesLegacyControllerAndDoesNotCallBridgePort));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var legacyPort = new RecordingGameplayAudioPlaybackPort();
                var bridgePort = new RecordingGameplayActionAudioPlaybackPort();
                var player = CreateUnit(10, UnitRole.Player);

                presenter.ConfigureActionAudioExecution(ActionAudioExecutionMode.LegacyActionAudioController, bridgePort);
                presenter.AttachGameplayAudioRuntime(legacyPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 7,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        targetEntityId: 20,
                        direction: Direction.Right,
                        actionPlanId: 70)),
                    new[] { player },
                    tickIndex: 17));

                Assert.That(presenter.ActionAudioExecutionMode, Is.EqualTo(ActionAudioExecutionMode.LegacyActionAudioController));
                Assert.That(bridgePort.Requests, Is.Empty);
                Assert.That(legacyPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "Action:Push:Windup",
                }));
                Assert.That(presenter.ActionAudioOwnershipDiagnostics.ExecutedByLegacyCount, Is.EqualTo(1));
                Assert.That(presenter.ActionAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(presenter.ActionAudioOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(presenter.ActionAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_OrchestrationBridgeMode_RoutesPublicMomentsToInjectedPort()
        {
            var rootObject = new GameObject(nameof(ActionAudio_OrchestrationBridgeMode_RoutesPublicMomentsToInjectedPort));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform));
                var legacyPort = new RecordingGameplayAudioPlaybackPort();
                var bridgePort = new RecordingGameplayActionAudioPlaybackPort();

                presenter.ConfigureActionAudioExecution(ActionAudioExecutionMode.OrchestrationActionAudioBridge, bridgePort);
                presenter.AttachGameplayAudioRuntime(legacyPort, mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    new[]
                    {
                        new TickPlayerActionPresentationSignal(
                            entityId: 10,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 11,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false,
                            targetEntityId: 20,
                            direction: Direction.Right,
                            actionPlanId: 101),
                        new TickPlayerActionPresentationSignal(
                            entityId: 12,
                            activeActionKind: PlayerActionKind.Flip,
                            activeActionSequence: 12,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false,
                            targetEntityId: 30,
                            direction: Direction.Left,
                            actionPlanId: 102),
                    },
                    new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            10,
                            PlayerActionKind.Push,
                            Direction.Up,
                            PlayerActionAttemptFeedbackKind.NoTarget),
                        new TickPlayerActionAttemptPresentationSignal(
                            12,
                            PlayerActionKind.Flip,
                            Direction.Down,
                            PlayerActionAttemptFeedbackKind.AssistOutOfRange,
                            targetEntityId: 30,
                            hasTarget: true),
                    }),
                    tickIndex: 23));

                Assert.That(legacyPort.TwoDCalls, Is.Empty);
                Assert.That(legacyPort.AttachedCalls, Is.Empty);
                Assert.That(bridgePort.Requests.Select(request => request.CueKey).ToArray(), Is.EqualTo(new[]
                {
                    PresentationActionAudioCueKey.PlayerPushWindup,
                    PresentationActionAudioCueKey.PlayerFlipWindup,
                    PresentationActionAudioCueKey.PlayerPushNoTarget,
                    PresentationActionAudioCueKey.PlayerFlipAssistOutOfRange,
                }));
                AssertActionAudioBridgeRequest(
                    bridgePort.Requests[0],
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    ownerEntityId: 10,
                    targetEntityId: 20,
                    sourceTick: 23,
                    sourceSequenceId: 11,
                    sourceActionPlanId: 101,
                    Direction.Right,
                    PresentationActionAudioOutcomeKind.Started);
                AssertActionAudioBridgeRequest(
                    bridgePort.Requests[3],
                    GameplayActionKind.Flip,
                    GameplayActionAudioMoment.AssistOutOfRange,
                    ownerEntityId: 12,
                    targetEntityId: 30,
                    sourceTick: 23,
                    sourceSequenceId: 2,
                    sourceActionPlanId: 0,
                    Direction.Down,
                    PresentationActionAudioOutcomeKind.AttemptFeedback);
                Assert.That(presenter.ActionAudioOwnershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount, Is.EqualTo(4));
                Assert.That(presenter.ActionAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(4));
                Assert.That(presenter.ActionAudioOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(presenter.ActionAudioExecutorDiagnostics.RequestPlannedCount, Is.EqualTo(4));
                Assert.That(presenter.ActionAudioExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(4));
                Assert.That(presenter.ActionAudioExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(presenter.ActionAudioExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues()
        {
            var rootObject = new GameObject(nameof(ActionAudio_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues));
            using var mapBundle = CreateGameplayAudioMap();
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform));
                var bridgePort = new RecordingGameplayActionAudioPlaybackPort();

                presenter.ConfigureActionAudioExecution(ActionAudioExecutionMode.OrchestrationActionAudioBridge, bridgePort);
                presenter.AttachGameplayAudioRuntime(new RecordingGameplayAudioPlaybackPort(), mapBundle.Map);
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    new[]
                    {
                        new TickPlayerActionPresentationSignal(
                            entityId: 10,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 11,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false,
                            targetEntityId: 20,
                            direction: Direction.Right,
                            actionPlanId: 101),
                    },
                    new[]
                    {
                        new TickPlayerActionAttemptPresentationSignal(
                            12,
                            PlayerActionKind.Flip,
                            Direction.Down,
                            PlayerActionAttemptFeedbackKind.AssistOutOfRange,
                            targetEntityId: 30,
                            hasTarget: true),
                    }),
                    tickIndex: 24));

                var telemetry = presenter.ActionAudioProductionTelemetrySnapshot;
                Assert.That(telemetry.CurrentMode, Is.EqualTo(ActionAudioExecutionMode.OrchestrationActionAudioBridge));
                Assert.That(telemetry.IsProductionDefaultOwner, Is.True);
                Assert.That(telemetry.ProductionDefaultMode, Is.EqualTo(ActionAudioExecutionMode.OrchestrationActionAudioBridge));
                Assert.That(telemetry.RollbackMode, Is.EqualTo(ActionAudioExecutionMode.LegacyActionAudioController));
                Assert.That(telemetry.LastTickIndex, Is.EqualTo(24));
                Assert.That(telemetry.LastCueKey, Is.EqualTo(PresentationActionAudioCueKey.PlayerFlipAssistOutOfRange));
                Assert.That(telemetry.LastOwnerEntityId, Is.EqualTo(12));
                Assert.That(telemetry.LastAction, Is.EqualTo(GameplayActionKind.Flip));
                Assert.That(telemetry.LastMoment, Is.EqualTo(GameplayActionAudioMoment.AssistOutOfRange));
                Assert.That(telemetry.LastOutcome, Is.EqualTo(PresentationActionAudioOutcomeKind.AttemptFeedback));
                Assert.That(telemetry.LastFailureReason, Is.EqualTo(ActionAudioTelemetryFailureReason.None));
                Assert.That(telemetry.LegacyOwnerAttemptCount, Is.EqualTo(2));
                Assert.That(telemetry.LegacyOwnerSkippedByPolicyCount, Is.EqualTo(2));
                Assert.That(telemetry.ExecutorOwnerAttemptCount, Is.EqualTo(2));
                Assert.That(telemetry.ExecutorOwnerExecutedCount, Is.EqualTo(2));
                Assert.That(telemetry.ObservedCueCount, Is.EqualTo(2));
                Assert.That(telemetry.RequestPlannedCount, Is.EqualTo(2));
                Assert.That(telemetry.PlaybackRequestedCount, Is.EqualTo(2));
                Assert.That(telemetry.PlaybackSucceededCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_OrchestrationBridgeMode_DefaultAdapterUsesControllerProfileResolution()
        {
            var rootObject = new GameObject(nameof(ActionAudio_OrchestrationBridgeMode_DefaultAdapterUsesControllerProfileResolution));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var player = CreateUnit(10, UnitRole.Player);

                presenter.ConfigureActionAudioExecution(ActionAudioExecutionMode.OrchestrationActionAudioBridge);
                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { player }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    new[] { player }));

                Assert.That(playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(), Is.EqualTo(new[]
                {
                    "Action:Push:Windup",
                }));
                Assert.That(presenter.ActionAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(presenter.ActionAudioExecutorDiagnostics.OwnerViewMissingCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_LegacyAndBridgeRequests_AreSemanticallyEquivalent()
        {
            var result = CreateTickResult(CreatePresentationData(
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 4,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        targetEntityId: 20,
                        direction: Direction.Right,
                        actionPlanId: 40),
                    new TickPlayerActionPresentationSignal(
                        entityId: 20,
                        activeActionKind: PlayerActionKind.Flip,
                        activeActionSequence: 5,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        targetEntityId: 30,
                        direction: Direction.Left,
                        actionPlanId: 50),
                },
                new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        Direction.Up,
                        PlayerActionAttemptFeedbackKind.AssistOutOfRange),
                    new TickPlayerActionAttemptPresentationSignal(
                        20,
                        PlayerActionKind.Flip,
                        Direction.Down,
                        PlayerActionAttemptFeedbackKind.Invalid,
                        targetEntityId: 30,
                        hasTarget: true),
                }),
                tickIndex: 31);
            var legacyRequests = new GameplayActionAudioRequestPlanner().BuildRequests(result);
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new ActionAudioCuePlanner(),
            }).Plan(factFrame);
            var playbackPlan = new PresentationPlaybackPlanner().Plan(cueFrame);
            var bridgePort = new RecordingGameplayActionAudioPlaybackPort();
            var executor = new GameplayActionAudioPresentationExecutor(
                bridgePort,
                ActionAudioExecutionMode.OrchestrationActionAudioBridge,
                new ActionAudioExecutionGuard(ActionAudioExecutionMode.OrchestrationActionAudioBridge));

            executor.Play(playbackPlan);

            Assert.That(bridgePort.Requests, Has.Count.EqualTo(legacyRequests.Count));
            for (var i = 0; i < legacyRequests.Count; i++)
            {
                Assert.That(bridgePort.Requests[i].Action, Is.EqualTo(legacyRequests[i].Action));
                Assert.That(bridgePort.Requests[i].Moment, Is.EqualTo(legacyRequests[i].Moment));
                Assert.That(bridgePort.Requests[i].OwnerEntityId, Is.EqualTo(legacyRequests[i].OwnerEntityId));
                Assert.That(bridgePort.Requests[i].Context.DebugTag, Is.EqualTo(legacyRequests[i].Context.DebugTag));
                Assert.That(bridgePort.Requests[i].TickIndex, Is.EqualTo(result.TickIndex));
                Assert.That(playbackPlan.Cues[i].Policy.UnitKind, Is.EqualTo(PresentationPlaybackUnitKind.OneShot));
                Assert.That(playbackPlan.Cues[i].Policy.Blocking, Is.False);
                Assert.That(bridgePort.Requests[i].Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
                Assert.That(bridgePort.Requests[i].OwnershipKey.SourceSequenceId, Is.EqualTo(cueFrame.Cues[i].ActionAudioPayload.SourceSequenceId));
                Assert.That(bridgePort.Requests[i].OwnershipKey.SourceActionPlanId, Is.EqualTo(cueFrame.Cues[i].ActionAudioPayload.SourceActionPlanId));
                Assert.That(bridgePort.Requests[i].OwnershipKey.TargetEntityId, Is.EqualTo(cueFrame.Cues[i].ActionAudioPayload.TargetEntityId));
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_OrchestrationMode_ForcedDuplicateExecutorAttemptBlocksSecondPlayback()
        {
            var rootObject = new GameObject(nameof(ActionAudio_OrchestrationMode_ForcedDuplicateExecutorAttemptBlocksSecondPlayback));
            try
            {
                var bridgePort = new RecordingGameplayActionAudioPlaybackPort();
                var coordinator = CreateInitializedActionAudioCoordinator(
                    rootObject,
                    ActionAudioExecutionMode.OrchestrationActionAudioBridge,
                    bridgePort,
                    duplicateExecutors: true);

                coordinator.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    tickIndex: 41));

                Assert.That(bridgePort.Requests, Has.Count.EqualTo(1));
                Assert.That(coordinator.ActionAudioOwnershipDiagnostics.ExecutorAttemptCount, Is.EqualTo(2));
                Assert.That(coordinator.ActionAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(coordinator.ActionAudioOwnershipDiagnostics.DuplicateAttemptCount, Is.EqualTo(1));
                Assert.That(coordinator.ActionAudioExecutorDiagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(coordinator.ActionAudioProductionTelemetrySnapshot.DuplicateOwnerAttemptCount, Is.EqualTo(1));
                Assert.That(coordinator.ActionAudioProductionTelemetrySnapshot.DuplicateSuppressedCount, Is.EqualTo(1));
                Assert.That(coordinator.ActionAudioProductionTelemetrySnapshot.LastFailureReason, Is.EqualTo(ActionAudioTelemetryFailureReason.DuplicateSuppressed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_OrchestrationMode_DistinguishesMissingDiagnostics()
        {
            AssertActionAudioExecutorDiagnostic(
                new[] { CreateUnsupportedActionAudioCue() },
                new RecordingGameplayActionAudioPlaybackPort(),
                diagnostics => diagnostics.UnsupportedMomentCount,
                expectedCount: 1);
            AssertActionAudioExecutorDiagnostic(
                CreateActionAudioCueFrame().Cues,
                playbackPort: null,
                diagnostics => diagnostics.PortMissingCount,
                expectedCount: 1,
                ActionAudioTelemetryFailureReason.PortMissing);
            AssertActionAudioExecutorDiagnostic(
                CreateActionAudioCueFrame().Cues,
                new RecordingGameplayActionAudioPlaybackPort(GameplayActionAudioPlaybackResultKind.OwnerViewMissing),
                diagnostics => diagnostics.OwnerViewMissingCount,
                expectedCount: 1,
                ActionAudioTelemetryFailureReason.OwnerViewMissing);
            AssertActionAudioExecutorDiagnostic(
                CreateActionAudioCueFrame().Cues,
                new RecordingGameplayActionAudioPlaybackPort(GameplayActionAudioPlaybackResultKind.AuthoringMissing),
                diagnostics => diagnostics.AuthoringMissingCount,
                expectedCount: 1,
                ActionAudioTelemetryFailureReason.AuthoringMissing);
            AssertActionAudioExecutorDiagnostic(
                CreateActionAudioCueFrame().Cues,
                new RecordingGameplayActionAudioPlaybackPort(GameplayActionAudioPlaybackResultKind.ProfileMissing),
                diagnostics => diagnostics.ProfileMissingCount,
                expectedCount: 1,
                ActionAudioTelemetryFailureReason.ProfileMissing);
            AssertActionAudioExecutorDiagnostic(
                CreateActionAudioCueFrame().Cues,
                new RecordingGameplayActionAudioPlaybackPort(GameplayActionAudioPlaybackResultKind.BindingMissing),
                diagnostics => diagnostics.BindingMissingCount,
                expectedCount: 1,
                ActionAudioTelemetryFailureReason.BindingMissing);
            AssertActionAudioExecutorDiagnostic(
                CreateActionAudioCueFrame().Cues,
                new RecordingGameplayActionAudioPlaybackPort(GameplayActionAudioPlaybackResultKind.OptionalProfileEntryMissing),
                diagnostics => diagnostics.OptionalProfileEntryMissingNoOpCount,
                expectedCount: 1,
                ActionAudioTelemetryFailureReason.OptionalProfileEntryMissing);
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_OrchestrationMode_AdapterSeparatesOwnerAuthoringProfileBindingAndOptionalEntryNoOps()
        {
            using var mapBundle = CreateGameplayAudioMap();
            AssertActionAudioAdapterDiagnostic(
                new ActionAudioViewFactory(new GameObject("ActionAudioMissingOwnerRoot").transform),
                Array.Empty<EntityState>(),
                diagnostics => diagnostics.OwnerViewMissingCount,
                expectedCount: 1,
                mapBundle.Map);
            AssertActionAudioAdapterDiagnostic(
                new ActionAudioViewFactory(new GameObject("ActionAudioMissingAuthoringRoot").transform),
                new[] { CreateUnit(10, UnitRole.Player) },
                diagnostics => diagnostics.AuthoringMissingCount,
                expectedCount: 1,
                mapBundle.Map);
            AssertActionAudioAdapterDiagnostic(
                new ActionAudioViewFactory(
                    new GameObject("ActionAudioProfileMissingRoot").transform,
                    addAuthoringWithMissingProfile: true),
                new[] { CreateUnit(10, UnitRole.Player) },
                diagnostics => diagnostics.ProfileMissingCount,
                expectedCount: 1,
                mapBundle.Map);

            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.AssistOutOfRange,
                    CreateDefinitionSpec()));
            AssertActionAudioAdapterDiagnostic(
                new ActionAudioViewFactory(new GameObject("ActionAudioOptionalEntryRoot").transform, profileBundle.Profile),
                new[] { CreateUnit(10, UnitRole.Player) },
                diagnostics => diagnostics.OptionalProfileEntryMissingNoOpCount,
                expectedCount: 1,
                mapBundle.Map);

            using var optionalNullProfile = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.Windup,
                    definitionSpec: null,
                    isOptional: true));
            AssertActionAudioAdapterDiagnostic(
                new ActionAudioViewFactory(new GameObject("ActionAudioBindingMissingRoot").transform, optionalNullProfile.Profile),
                new[] { CreateUnit(10, UnitRole.Player) },
                diagnostics => diagnostics.BindingMissingCount,
                expectedCount: 1,
                mapBundle.Map);
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState()
        {
            var rootObject = new GameObject(nameof(ActionAudio_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState));
            try
            {
                var bridgePort = new RecordingGameplayActionAudioPlaybackPort();
                var coordinator = CreateInitializedActionAudioCoordinator(
                    rootObject,
                    ActionAudioExecutionMode.OrchestrationActionAudioBridge,
                    bridgePort);

                coordinator.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    tickIndex: 61));
                Assert.That(bridgePort.Requests, Has.Count.EqualTo(1));
                Assert.That(coordinator.ActionAudioExecutorDiagnostics.ObservedCueCount, Is.EqualTo(1));

                coordinator.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                Assert.That(coordinator.ActionAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);
                Assert.That(coordinator.ActionAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(bridgePort.ResetSessionCallCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(coordinator.ActionAudioProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(ActionAudioTelemetryCleanupReason.ResetSession));

                coordinator.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    tickIndex: 62));
                coordinator.HardCleanupPresentationExtensions();
                Assert.That(coordinator.ActionAudioExecutorDiagnostics.ObservedCueCount, Is.Zero);
                Assert.That(coordinator.ActionAudioOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(bridgePort.HardCleanupCallCount, Is.EqualTo(1));
                Assert.That(coordinator.ActionAudioProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(ActionAudioTelemetryCleanupReason.HardCleanupPresentationExtensions));
                Assert.That(
                    typeof(PresentationCue).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
                Assert.That(
                    typeof(PresentationPlaybackPlan).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ActionAudio_OrchestrationRoute_IsNonBlockingAndDoesNotMutateAuthoritativeTickResult()
        {
            var rootObject = new GameObject(nameof(ActionAudio_OrchestrationRoute_IsNonBlockingAndDoesNotMutateAuthoritativeTickResult));
            try
            {
                var presenter = CreatePresenter(rootObject, new ActionAudioViewFactory(rootObject.transform));
                var result = CreateTickResult(
                    CreatePresentationData(
                        new TickPlayerActionPresentationSignal(
                            entityId: 10,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 1,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false)),
                    finalEntities: new[] { CreateUnit(10, UnitRole.Player) },
                    tickIndex: 71,
                    eventLog: new[] { "BeforePresentation" },
                    determinismHash: "hash-before");
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objectiveResult = result.ObjectiveResult;

                presenter.ConfigureActionAudioExecution(
                    ActionAudioExecutionMode.OrchestrationActionAudioBridge,
                    new RecordingGameplayActionAudioPlaybackPort());
                presenter.PresentInitial(Array.Empty<EntityState>(), new CubeTopologyState(FaceId.Floor));
                presenter.Present(result);

                Assert.That(presenter.ActionAudioExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(presenter.ActionAudioExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);
                Assert.That(result.DeterminismHash, Is.EqualTo("hash-before"));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioPresentationController_RemainsOneShotOnly_WithoutPlaybackHandles()
        {
            var fieldTypes = typeof(GameplayActionAudioPresentationController)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();

            Assert.That(fieldTypes, Has.No.Member(typeof(AudioPlaybackHandle)));
        }

        private static void AssertActionAudioPayload(
            PresentationActionAudioPayload payload,
            int ownerEntityId,
            GameplayActionKind actionKind,
            GameplayActionAudioMoment moment,
            int sourceSequenceId,
            int sourceActionPlanId,
            int targetEntityId,
            Direction direction,
            PresentationActionAudioOutcomeKind outcomeKind)
        {
            Assert.That(payload.IsValid, Is.True);
            Assert.That(payload.OwnerEntityId, Is.EqualTo(ownerEntityId));
            Assert.That((GameplayActionKind)payload.ActionKind, Is.EqualTo(actionKind));
            Assert.That((GameplayActionAudioMoment)payload.Moment, Is.EqualTo(moment));
            Assert.That(payload.SourceTickIndex, Is.EqualTo(1));
            Assert.That(payload.SourceSequenceId, Is.EqualTo(sourceSequenceId));
            Assert.That(payload.SourceActionPlanId, Is.EqualTo(sourceActionPlanId));
            Assert.That(payload.TargetEntityId, Is.EqualTo(targetEntityId));
            Assert.That(payload.Direction, Is.EqualTo(direction));
            Assert.That(payload.OutcomeKind, Is.EqualTo(outcomeKind));
        }

        private static void AssertActionAudioBridgeRequest(
            in GameplayActionAudioPlaybackRequest request,
            GameplayActionKind expectedAction,
            GameplayActionAudioMoment expectedMoment,
            int ownerEntityId,
            int targetEntityId,
            int sourceTick,
            int sourceSequenceId,
            int sourceActionPlanId,
            Direction direction,
            PresentationActionAudioOutcomeKind outcomeKind)
        {
            Assert.That(request.Action, Is.EqualTo(expectedAction));
            Assert.That(request.Moment, Is.EqualTo(expectedMoment));
            Assert.That(request.OwnerEntityId, Is.EqualTo(ownerEntityId));
            Assert.That(request.TickIndex, Is.EqualTo(sourceTick));
            Assert.That(request.Source.TickIndex, Is.EqualTo(sourceTick));
            Assert.That(request.Target, Is.EqualTo(PresentationTarget.Entity(ownerEntityId)));
            Assert.That(request.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
            Assert.That(request.ActionAudioPayload.OwnerEntityId, Is.EqualTo(ownerEntityId));
            Assert.That(request.ActionAudioPayload.TargetEntityId, Is.EqualTo(targetEntityId));
            Assert.That(request.ActionAudioPayload.SourceSequenceId, Is.EqualTo(sourceSequenceId));
            Assert.That(request.ActionAudioPayload.SourceActionPlanId, Is.EqualTo(sourceActionPlanId));
            Assert.That(request.ActionAudioPayload.Direction, Is.EqualTo(direction));
            Assert.That(request.ActionAudioPayload.OutcomeKind, Is.EqualTo(outcomeKind));
            Assert.That(request.OwnershipKey.OwnerEntityId, Is.EqualTo(ownerEntityId));
            Assert.That(request.OwnershipKey.SourceSequenceId, Is.EqualTo(sourceSequenceId));
            Assert.That(request.OwnershipKey.SourceActionPlanId, Is.EqualTo(sourceActionPlanId));
            Assert.That(request.OwnershipKey.TargetEntityId, Is.EqualTo(targetEntityId));
            Assert.That(request.Context.DebugTag, Is.EqualTo($"Action:{expectedAction}:{expectedMoment}"));
        }

        private static GameplayTickPresentationCoordinator CreateInitializedActionAudioCoordinator(
            GameObject rootObject,
            ActionAudioExecutionMode mode,
            IGameplayActionAudioPlaybackPort playbackPort,
            bool duplicateExecutors = false)
        {
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreatePlayerActionAnimationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateEnemyPresentationExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateCoreGameplaySfxExecutionPipeline,
                (requestedMode, requestedPort, executionGuard) => CreateActionAudioTestPipeline(
                    requestedMode,
                    requestedPort,
                    executionGuard,
                    duplicateExecutors));
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new ActionAudioViewFactory(rootObject.transform));

            coordinator.ConfigureActionAudioExecution(mode, playbackPort);
            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                1f,
                CreateTimingProfile());
            return coordinator;
        }

        private static GameplayPresentationPipeline CreateActionAudioTestPipeline(
            ActionAudioExecutionMode mode,
            IGameplayActionAudioPlaybackPort playbackPort,
            ActionAudioExecutionGuard executionGuard,
            bool duplicateExecutors)
        {
            if (mode != ActionAudioExecutionMode.OrchestrationActionAudioBridge)
            {
                return null;
            }

            var executors = duplicateExecutors
                ? new IPresentationExecutor[]
                {
                    new GameplayActionAudioPresentationExecutor(playbackPort, mode, executionGuard),
                    new GameplayActionAudioPresentationExecutor(playbackPort, mode, executionGuard),
                }
                : new IPresentationExecutor[]
                {
                    new GameplayActionAudioPresentationExecutor(playbackPort, mode, executionGuard),
                };
            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new ActionAudioCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                executors);
        }

        private static void AssertActionAudioExecutorDiagnostic(
            IReadOnlyList<PresentationCue> cues,
            IGameplayActionAudioPlaybackPort playbackPort,
            Func<GameplayActionAudioExecutorDiagnostics, int> selector,
            int expectedCount,
            ActionAudioTelemetryFailureReason expectedFailureReason =
                ActionAudioTelemetryFailureReason.UnsupportedMoment)
        {
            var plan = new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                tickIndex: 51,
                cues,
                new PresentationCueFrameDiagnostics(cues.Count, cues.Count, 1)));
            var executor = new GameplayActionAudioPresentationExecutor(
                playbackPort,
                ActionAudioExecutionMode.OrchestrationActionAudioBridge,
                new ActionAudioExecutionGuard(ActionAudioExecutionMode.OrchestrationActionAudioBridge));

            executor.Play(plan);

            Assert.That(selector(executor.Diagnostics), Is.EqualTo(expectedCount));
            Assert.That(executor.Diagnostics.LastFailureReason, Is.EqualTo(expectedFailureReason));
        }

        private static void AssertActionAudioAdapterDiagnostic(
            IGameplayEntityViewFactory viewFactory,
            IReadOnlyList<EntityState> entities,
            Func<GameplayActionAudioExecutorDiagnostics, int> selector,
            int expectedCount,
            GameplayAudioMap map)
        {
            var rootObject = new GameObject(nameof(AssertActionAudioAdapterDiagnostic));
            try
            {
                var presenter = CreatePresenter(rootObject, viewFactory);
                presenter.ConfigureActionAudioExecution(ActionAudioExecutionMode.OrchestrationActionAudioBridge);
                presenter.AttachGameplayAudioRuntime(new RecordingGameplayAudioPlaybackPort(), map);
                presenter.PresentInitial(entities, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(CreatePresentationData(
                    new TickPlayerActionPresentationSignal(
                        entityId: 10,
                        activeActionKind: PlayerActionKind.Push,
                        activeActionSequence: 1,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false)),
                    entities));

                Assert.That(selector(presenter.ActionAudioExecutorDiagnostics), Is.EqualTo(expectedCount));
            }
            finally
            {
                if (viewFactory is ActionAudioViewFactory actionAudioViewFactory)
                {
                    actionAudioViewFactory.DestroyParentIfOwned();
                }

                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static PresentationCueFrame CreateActionAudioCueFrame()
        {
            return new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new ActionAudioCuePlanner(),
            }).Plan(new TickPresentationFactExtractor().Extract(CreateTickResult(CreatePresentationData(
                new TickPlayerActionPresentationSignal(
                    entityId: 10,
                    activeActionKind: PlayerActionKind.Push,
                    activeActionSequence: 1,
                    startedThisTick: true,
                    completedThisTick: false,
                    canceledThisTick: false)))));
        }

        private static PresentationCue CreateUnsupportedActionAudioCue()
        {
            return new PresentationCue(
                PresentationDomain.ActionAudio,
                new PresentationCueKey(PresentationDomain.ActionAudio, localKey: 999),
                new PresentationSource(51, PresentationSemanticSource.PlayerActionAudio, 10),
                PresentationTarget.Entity(10),
                PresentationAnchor.ForEntityVisualRoot(10),
                PresentationPlaybackPolicyHint.OneShot(1),
                actionAudioPayload: new PresentationActionAudioPayload(
                    10,
                    (int)GameplayActionKind.Push,
                    (int)GameplayActionAudioMoment.Windup,
                    51,
                    sourceSequenceId: 1));
        }

        private static GameplayTickViewPresenter CreatePresenter(GameObject rootObject, IGameplayEntityViewFactory viewFactory)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, viewFactory);
            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                1f,
                CreateTimingProfile());
            return presenter;
        }

        private static GameplayTimingProfile CreateTimingProfile()
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 20,
                initialMoveDelaySeconds: 0.1f,
                repeatedMoveIntervalSeconds: 0.1f,
                boxSlideStepIntervalSeconds: 0.1f,
                projectileStepIntervalSeconds: 0.1f,
                moveMotionDurationSeconds: 0.1f,
                pushMotionDurationSeconds: 0.1f,
                topologyMotionDurationSeconds: 0.1f,
                flipMotionDurationSeconds: 0.1f,
                flipArcHeightInCells: 1f,
                maxTicksPerFrame: 4,
                itemConsumeEffectDurationSeconds: 0.1f,
                boxDestroyEffectDurationSeconds: 0.1f,
                enemyDeathEffectDurationSeconds: 0.1f);
        }

        private static TickResult CreateTickResult(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities = null,
            CubeTopologyState? finalTopology = null,
            int tickIndex = 1,
            IEnumerable<string> eventLog = null,
            string determinismHash = "")
        {
            return new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities ?? Array.Empty<EntityState>(),
                eventLog ?? Array.Empty<string>(),
                finalTopology ?? new CubeTopologyState(FaceId.Floor),
                presentationData,
                determinismHash,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static TickPresentationData CreatePresentationData(
            params TickPlayerActionPresentationSignal[] playerActionSignals)
        {
            return CreatePresentationData(playerActionSignals, Array.Empty<TickEnemyDamagePresentationSignal>());
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

        private static TickPresentationData CreatePresentationData(
            TickPlayerActionPresentationSignal[] playerActionSignals,
            TickEnemyDamagePresentationSignal[] enemyDamageSignals,
            TickTopologyMotion? topologyMotion = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals ?? Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                enemyDamageSignals ?? Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static EntityState CreateUnit(
            int entityId,
            UnitRole unitRole,
            SurfaceCell? cell = null)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell ?? new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 1,
                maxHp = 1,
                teamId = unitRole == UnitRole.Player ? 1 : 2,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static DefinitionSpec CreateDefinitionSpec(
            AudioCategory category = AudioCategory.Sfx,
            bool loop = false)
        {
            return new DefinitionSpec(category, loop);
        }

        private static ActionAudioProfileBundle CreateActionAudioProfile(params ActionAudioEntrySpec[] entrySpecs)
        {
            var profile = ScriptableObject.CreateInstance<GameplayActionAudioProfile>();
            var trackedObjects = new List<UnityEngine.Object> { profile };
            var entries = new GameplayActionAudioEntry[entrySpecs.Length];

            for (var i = 0; i < entrySpecs.Length; i++)
            {
                AudioBinding binding = null;
                if (entrySpecs[i].DefinitionSpec.HasValue ||
                    entrySpecs[i].CreateBindingWithNullDefinition)
                {
                    SingleAudioDefinition definition = null;
                    if (entrySpecs[i].DefinitionSpec.HasValue)
                    {
                        definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                        definition.name = $"{entrySpecs[i].Action}_{entrySpecs[i].Moment}";
                        var clip = AudioClip.Create(definition.name, 4410, 1, 44100, false);
                        trackedObjects.Add(clip);
                        trackedObjects.Add(definition);
                        SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                        SetSerializedField(typeof(AudioDefinition), definition, "category", entrySpecs[i].DefinitionSpec.Value.Category);
                        SetSerializedField(typeof(AudioDefinition), definition, "loop", entrySpecs[i].DefinitionSpec.Value.Loop);
                    }

                    binding = new AudioBinding();
                    SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                    SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", entrySpecs[i].AttachmentSlot);
                    SetSerializedField(typeof(AudioBinding), binding, "policy", null);
                }

                entries[i] = new GameplayActionAudioEntry
                {
                    Action = entrySpecs[i].Action,
                    Moment = entrySpecs[i].Moment,
                    Binding = binding,
                    IsOptional = entrySpecs[i].IsOptional,
                };
            }

            SetSerializedField(typeof(GameplayActionAudioProfile), profile, "entries", entries);
            return new ActionAudioProfileBundle(profile, trackedObjects);
        }

        private static GameplayAudioMapBundle CreateGameplayAudioMap()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definitions = new List<UnityEngine.Object>();
            var serializedObject = new SerializedObject(map);
            var entries = serializedObject.FindProperty("entries");
            entries.arraySize = GameplayAudioSemanticCatalog.RequiredOneShotV1.Count;

            for (var i = 0; i < GameplayAudioSemanticCatalog.RequiredOneShotV1.Count; i++)
            {
                var semanticId = GameplayAudioSemanticCatalog.RequiredOneShotV1[i];
                var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                definition.name = semanticId.ToString();
                var clip = AudioClip.Create(definition.name, 4410, 1, 44100, false);
                definitions.Add(clip);
                definitions.Add(definition);
                SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Sfx);
                SetSerializedField(typeof(AudioDefinition), definition, "loop", false);

                var element = entries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("SemanticId").enumValueIndex = (int)semanticId;
                var binding = element.FindPropertyRelative("Binding");
                binding.FindPropertyRelative("definition").objectReferenceValue = definition;
                binding.FindPropertyRelative("attachmentSlot").FindPropertyRelative("id").stringValue = string.Empty;
                binding.FindPropertyRelative("policy").managedReferenceValue = null;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return new GameplayAudioMapBundle(map, definitions);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private readonly struct DefinitionSpec
        {
            public DefinitionSpec(AudioCategory category, bool loop)
            {
                Category = category;
                Loop = loop;
            }

            public AudioCategory Category { get; }

            public bool Loop { get; }
        }

        private readonly struct ActionAudioEntrySpec
        {
            public ActionAudioEntrySpec(
                GameplayActionKind action,
                GameplayActionAudioMoment moment,
                DefinitionSpec? definitionSpec,
                bool isOptional = false,
                AudioAttachmentSlot attachmentSlot = default,
                bool createBindingWithNullDefinition = false)
            {
                Action = action;
                Moment = moment;
                DefinitionSpec = definitionSpec;
                IsOptional = isOptional;
                AttachmentSlot = attachmentSlot;
                CreateBindingWithNullDefinition = createBindingWithNullDefinition;
            }

            public GameplayActionKind Action { get; }

            public GameplayActionAudioMoment Moment { get; }

            public DefinitionSpec? DefinitionSpec { get; }

            public bool IsOptional { get; }

            public AudioAttachmentSlot AttachmentSlot { get; }

            public bool CreateBindingWithNullDefinition { get; }
        }

        private sealed class ActionAudioProfileBundle : IDisposable
        {
            private readonly List<UnityEngine.Object> _trackedObjects;

            public ActionAudioProfileBundle(
                GameplayActionAudioProfile profile,
                List<UnityEngine.Object> trackedObjects)
            {
                Profile = profile;
                _trackedObjects = trackedObjects;
            }

            public GameplayActionAudioProfile Profile { get; }

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
        }

        private sealed class GameplayAudioMapBundle : IDisposable
        {
            private readonly List<UnityEngine.Object> _definitions;

            public GameplayAudioMapBundle(GameplayAudioMap map, List<UnityEngine.Object> definitions)
            {
                Map = map;
                _definitions = definitions;
            }

            public GameplayAudioMap Map { get; }

            public void Dispose()
            {
                for (var i = _definitions.Count - 1; i >= 0; i--)
                {
                    if (_definitions[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_definitions[i]);
                    }
                }

                if (Map != null)
                {
                    UnityEngine.Object.DestroyImmediate(Map);
                }
            }
        }

        private sealed class RecordingGameplayAnimationPlaybackPort : IGameplayAnimationPlaybackPort
        {
            public int TryPlayCallCount { get; private set; }

            public bool TryPlayPlayerActionAnimation(
                in GameplayAnimationPlaybackRequest request,
                out GameplayAnimationPlaybackResult result)
            {
                TryPlayCallCount++;
                result = new GameplayAnimationPlaybackResult(GameplayAnimationPlaybackResultKind.Applied);
                return true;
            }

            public void ResetSession()
            {
                TryPlayCallCount = 0;
            }

            public void HardCleanup()
            {
                TryPlayCallCount = 0;
            }
        }

        private sealed class RecordingGameplayAudioPlaybackPort : IGameplayAudioPlaybackPort
        {
            public readonly List<(AudioDefinition Definition, AudioPlaybackContext Context)> TwoDCalls = new();
            public readonly List<(AudioDefinition Definition, GameplayEntityView Owner, AudioAttachmentSlot Slot, AudioPlaybackContext Context)> AttachedCalls = new();

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
                AttachedCalls.Add((definition, (GameplayEntityView)owner, slot, context));
            }
        }

        private sealed class RecordingGameplayActionAudioPlaybackPort : IGameplayActionAudioPlaybackPort
        {
            private readonly GameplayActionAudioPlaybackResultKind _resultKind;

            public RecordingGameplayActionAudioPlaybackPort(
                GameplayActionAudioPlaybackResultKind resultKind = GameplayActionAudioPlaybackResultKind.Succeeded)
            {
                _resultKind = resultKind;
            }

            public readonly List<GameplayActionAudioPlaybackRequest> Requests = new();

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public bool TryPlayActionAudio(
                in GameplayActionAudioPlaybackRequest request,
                out GameplayActionAudioPlaybackResult result)
            {
                Requests.Add(request);
                result = new GameplayActionAudioPlaybackResult(_resultKind);
                return _resultKind == GameplayActionAudioPlaybackResultKind.Succeeded ||
                       _resultKind == GameplayActionAudioPlaybackResultKind.Requested;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                Requests.Clear();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                Requests.Clear();
            }
        }

        private sealed class ActionAudioViewFactory : IGameplayEntityViewFactory
        {
            private readonly GameplayActionAudioProfile _profile;
            private readonly Transform _parent;
            private readonly bool _addAuthoringWithMissingProfile;

            public ActionAudioViewFactory(
                Transform parent,
                GameplayActionAudioProfile profile = null,
                bool addAuthoringWithMissingProfile = false)
            {
                _parent = parent;
                _profile = profile;
                _addAuthoringWithMissingProfile = addAuthoringWithMissingProfile;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if ((_profile != null || _addAuthoringWithMissingProfile) &&
                    entity.unitRole == UnitRole.Player)
                {
                    var authoring = viewObject.AddComponent<GameplayActionAudioAuthoring>();
                    SetSerializedField(typeof(GameplayActionAudioAuthoring), authoring, "profile", _profile);
                }

                return view;
            }

            public void DestroyParentIfOwned()
            {
                if (_parent != null &&
                    _parent.gameObject.name.StartsWith("ActionAudio", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(_parent.gameObject);
                }
            }
        }
    }
}
