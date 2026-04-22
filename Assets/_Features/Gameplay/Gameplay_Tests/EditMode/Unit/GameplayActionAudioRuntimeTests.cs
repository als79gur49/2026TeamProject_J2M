using System;
using System.Collections.Generic;
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
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayActionAudioRuntimeTests
    {
        private const string PlayerPrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";

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
                    GameplayActionAudioMoment.Recovery,
                    definitionSpec: null,
                    isOptional: true));

            Assert.DoesNotThrow(() => profileBundle.Profile.ValidateOrThrow());
            Assert.That(
                profileBundle.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Recovery, out _),
                Is.False);
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
        public void PlayerS1Prefab_HasGameplayActionAudioAuthoring_WithRequiredV1Coverage()
        {
            var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(PlayerPrefabPath);
            Assert.That(view, Is.Not.Null, $"Missing prefab at '{PlayerPrefabPath}'.");

            var authoring = view.GetComponent<GameplayActionAudioAuthoring>();
            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(GameplayActionAudioAuthoring)} on '{PlayerPrefabPath}'.");
            Assert.That(authoring.gameObject, Is.SameAs(view.gameObject));
            Assert.DoesNotThrow(() => authoring.Validate());

            Assert.That(authoring.Profile, Is.Not.Null);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Windup, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Contact, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Blocked, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.ImpactEnemy, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Flip, GameplayActionAudioMoment.Windup, out _), Is.True);
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Flip, GameplayActionAudioMoment.Blocked, out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioRequestPlanner_PushImpactRecovery_MapsToDocumentedOrderedMomentSet()
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
                    GameplayActionAudioMoment.Execute,
                    GameplayActionAudioMoment.Contact,
                    GameplayActionAudioMoment.ImpactEnemy,
                    GameplayActionAudioMoment.Recovery,
                }));
            Assert.That(requests.All(request => request.Action == GameplayActionKind.Push), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioRequestPlanner_BlockedPush_EmitsExecuteThenBlocked_WithoutContact()
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

            Assert.That(
                requests.Select(request => request.Moment).ToArray(),
                Is.EqualTo(new[]
                {
                    GameplayActionAudioMoment.Execute,
                    GameplayActionAudioMoment.Blocked,
                }));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioRequestPlanner_FlipContact_UsesContactTimingSignal()
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
                    GameplayActionAudioMoment.Contact,
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
        public void GameplayTickViewPresenter_Present_ActionImpact_LayersWithCoreEnemyDamage()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_ActionImpact_LayersWithCoreEnemyDamage));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateActionAudioProfile(
                new ActionAudioEntrySpec(
                    GameplayActionKind.Push,
                    GameplayActionAudioMoment.ImpactEnemy,
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
                        "Action:Push:ImpactEnemy",
                    }));
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

        private static GameplayTickViewPresenter CreatePresenter(GameObject rootObject, IGameplayEntityViewFactory viewFactory)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
            IReadOnlyList<EntityState> finalEntities = null)
        {
            return new TickResult(
                1,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities ?? Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
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
            TickEnemyDamagePresentationSignal[] enemyDamageSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
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

        private static EntityState CreateUnit(int entityId, UnitRole unitRole)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
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
                if (entrySpecs[i].DefinitionSpec.HasValue)
                {
                    var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                    definition.name = $"{entrySpecs[i].Action}_{entrySpecs[i].Moment}";
                    var clip = AudioClip.Create(definition.name, 4410, 1, 44100, false);
                    trackedObjects.Add(clip);
                    trackedObjects.Add(definition);
                    SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                    SetSerializedField(typeof(AudioDefinition), definition, "category", entrySpecs[i].DefinitionSpec.Value.Category);
                    SetSerializedField(typeof(AudioDefinition), definition, "loop", entrySpecs[i].DefinitionSpec.Value.Loop);

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
                AudioAttachmentSlot attachmentSlot = default)
            {
                Action = action;
                Moment = moment;
                DefinitionSpec = definitionSpec;
                IsOptional = isOptional;
                AttachmentSlot = attachmentSlot;
            }

            public GameplayActionKind Action { get; }

            public GameplayActionAudioMoment Moment { get; }

            public DefinitionSpec? DefinitionSpec { get; }

            public bool IsOptional { get; }

            public AudioAttachmentSlot AttachmentSlot { get; }
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

        private sealed class ActionAudioViewFactory : IGameplayEntityViewFactory
        {
            private readonly GameplayActionAudioProfile _profile;
            private readonly Transform _parent;

            public ActionAudioViewFactory(Transform parent, GameplayActionAudioProfile profile = null)
            {
                _parent = parent;
                _profile = profile;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (_profile != null &&
                    entity.unitRole == UnitRole.Player)
                {
                    var authoring = viewObject.AddComponent<GameplayActionAudioAuthoring>();
                    SetSerializedField(typeof(GameplayActionAudioAuthoring), authoring, "profile", _profile);
                }

                return view;
            }
        }
    }
}
