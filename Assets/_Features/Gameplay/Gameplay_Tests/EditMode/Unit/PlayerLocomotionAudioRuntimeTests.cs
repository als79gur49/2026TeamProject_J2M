using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
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
            SetEntries(map, CreateRequiredEntries(scope, definition));
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
        [Category("Core")]
        public void Controller_StageClearVictory_StopsActiveWalkSteps_AndSuppressesHoldRefresh()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(map, CreateRequiredEntries(scope, definition));
            var playbackPort = new FakeGameplayAudioPlaybackPort();
            var controller = new PlayerLocomotionAudioPresentationController(new GameplayPresentationStateStore());
            controller.AttachRuntime(playbackPort, map);

            controller.RefreshSignals(
                CreateTickResult(
                    1,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal(10) }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            Assert.That(playbackPort.Play2DCalls, Is.EqualTo(1));

            controller.RefreshSignals(
                CreateTickResult(
                    2,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal(10) },
                    playerOutcomeSignals: new[]
                    {
                        new TickPlayerOutcomePresentationSignal(
                            10,
                            TickPlayerOutcomePresentationKind.StageClearVictory,
                            sourceTileId: 30,
                            sourceCell: new SurfaceCell(FaceId.Floor, 1, 0)),
                    }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            controller.Update(0.2f);
            Assert.That(playbackPort.Play2DCalls, Is.EqualTo(1));

            controller.RefreshSignals(
                CreateTickResult(
                    3,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal(10) }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            controller.Update(0.2f);
            Assert.That(playbackPort.Play2DCalls, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void Controller_TerminalDeathAndRemovalSignals_DoNotRegisterWalkSteps()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(map, CreateRequiredEntries(scope, definition));
            var playbackPort = new FakeGameplayAudioPlaybackPort();
            var controller = new PlayerLocomotionAudioPresentationController(new GameplayPresentationStateStore());
            controller.AttachRuntime(playbackPort, map);

            controller.RefreshSignals(
                CreateTickResult(
                    1,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal(10) },
                    playerDeathSignals: new[]
                    {
                        new TickPlayerDeathPresentationSignal(
                            10,
                            didDieThisTick: true,
                            sourceEntityId: 20,
                            fallbackFacing: Direction.Right,
                            resolvedDamageSourceAvailable: true,
                            damageAmountAtFatalHit: 1,
                            deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse),
                    }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();

            controller.RefreshSignals(
                CreateTickResult(
                    2,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal(10) },
                    playerDeathHoldSignals: new[]
                    {
                        new TickPlayerDeathHoldPresentationSignal(
                            10,
                            startTick: 1,
                            eligibleTick: 3,
                            remainingTicks: 1,
                            startedThisTick: false),
                    }),
                stepIntervalSeconds: 0.2f);
            controller.Update(0.2f);

            controller.RefreshSignals(
                CreateTickResult(
                    3,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal(10) },
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(
                            10,
                            TickVisibilityChangeKind.Remove,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            new CubeTopologyState(FaceId.Floor),
                            Direction.Right),
                    }),
                stepIntervalSeconds: 0.2f);
            controller.Update(0.2f);

            controller.RefreshSignals(
                CreateTickResult(
                    4,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal(10) },
                    entityExitSignals: new[]
                    {
                        new TickEntityExitPresentationSignal(
                            10,
                            TickEntityExitCause.Killed,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            new CubeTopologyState(FaceId.Floor),
                            Direction.Right,
                            EntityType.Unit),
                    }),
                stepIntervalSeconds: 0.2f);
            controller.Update(0.2f);

            controller.RefreshSignals(
                CreateTickResult(
                    5,
                    playerLocomotionSignals: new[] { CreateWalkLoopSignal(10) },
                    finalEntities: new[] { CreatePlayerEntity(hp: 0) }),
                stepIntervalSeconds: 0.2f);
            controller.Update(0.2f);

            Assert.That(playbackPort.Play2DCalls, Is.Zero);
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

        [Test]
        public void Planner_BuildsTopologyTransitionBlockedCue_ForBottomToBackBlockedSignal()
        {
            var planner = new PlayerLocomotionAudioRequestPlanner();
            var signal = CreateBlockedSignal();

            var requests = planner.BuildRequests(
                CreateTickResult(
                    1,
                    playerTopologyTransitionBlockedSignals: new[] { signal }));

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(PlayerLocomotionAudioCue.TopologyTransitionBlocked));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(signal.EntityId));
            Assert.That(requests[0].DelaySeconds, Is.Zero);
            Assert.That(requests[0].Context.DebugTag, Is.EqualTo("TopologyTransitionBlocked"));
        }

        [Test]
        public void Planner_DoesNotBuildTopologyTransitionBlockedCue_ForBottomToFrontBlockedSignal()
        {
            var planner = new PlayerLocomotionAudioRequestPlanner();
            var signal = CreateBlockedSignal(
                direction: Direction.Up,
                candidateFace: FaceId.Front,
                requiredBottomFace: FaceId.Front,
                rotationKind: CubeRotationKind.Forward);

            var requests = planner.BuildRequests(
                CreateTickResult(
                    1,
                    playerTopologyTransitionBlockedSignals: new[] { signal }));

            Assert.That(requests, Is.Empty);
        }

        [Test]
        public void Planner_DoesNotBuildTopologyTransitionBlockedCue_ForBackwardRotationToNonBackDestination()
        {
            var planner = new PlayerLocomotionAudioRequestPlanner();
            var signal = CreateBlockedSignal(
                candidateFace: FaceId.Ceiling,
                sourceBottomFace: FaceId.Front,
                requiredBottomFace: FaceId.Ceiling);

            var requests = planner.BuildRequests(
                CreateTickResult(
                    1,
                    playerTopologyTransitionBlockedSignals: new[] { signal }));

            Assert.That(requests, Is.Empty);
        }

        [Test]
        public void Planner_DoesNotBuildTopologyTransitionBlockedCue_ForBoardEdgeOnlySignal()
        {
            var planner = new PlayerLocomotionAudioRequestPlanner();
            var signal = CreateBlockedSignal(primaryBlockerKind: TickTraversalBlockerKind.BoardEdge);

            var requests = planner.BuildRequests(
                CreateTickResult(
                    1,
                    playerTopologyTransitionBlockedSignals: new[] { signal }));

            Assert.That(requests, Is.Empty);
        }

        [Test]
        public void Planner_DedupesTopologyTransitionBlockedSignals_InSameTick()
        {
            var planner = new PlayerLocomotionAudioRequestPlanner();
            var signal = CreateBlockedSignal();

            var requests = planner.BuildRequests(
                CreateTickResult(
                    1,
                    playerTopologyTransitionBlockedSignals: new[] { signal, signal }));

            Assert.That(requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void Planner_DedupeDoesNotPersistAcrossBuildRequestsCalls()
        {
            var planner = new PlayerLocomotionAudioRequestPlanner();
            var signal = CreateBlockedSignal();

            var firstRequests = planner.BuildRequests(
                CreateTickResult(
                    1,
                    playerTopologyTransitionBlockedSignals: new[] { signal }));
            var secondRequests = planner.BuildRequests(
                CreateTickResult(
                    2,
                    playerTopologyTransitionBlockedSignals: new[] { signal }));

            Assert.That(firstRequests, Has.Count.EqualTo(1));
            Assert.That(secondRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public void Planner_DoesNotBuildTopologyTransitionBlockedCue_WhenNoBlockedSignalExists()
        {
            var planner = new PlayerLocomotionAudioRequestPlanner();

            var requests = planner.BuildRequests(CreateTickResult(1));

            Assert.That(requests, Is.Empty);
        }

        [Test]
        public void Controller_TopologyTransitionBlockedCue_UsesCooldown_ForHeldInputSpam()
        {
            using var scope = new TestAssetScope();
            var clock = new FakeClock();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(map, CreateRequiredEntries(scope, definition));
            var playbackPort = new FakeGameplayAudioPlaybackPort();
            var controller = new PlayerLocomotionAudioPresentationController(
                new GameplayPresentationStateStore(),
                clock.Now);
            controller.AttachRuntime(playbackPort, map);
            var signal = CreateBlockedSignal();

            controller.RefreshSignals(
                CreateTickResult(1, playerTopologyTransitionBlockedSignals: new[] { signal }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            controller.RefreshSignals(
                CreateTickResult(2, playerTopologyTransitionBlockedSignals: new[] { signal }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            controller.RefreshSignals(
                CreateTickResult(3, playerTopologyTransitionBlockedSignals: new[] { signal }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.Play2DCalls, Is.EqualTo(1));
            Assert.That(playbackPort.LastDefinition, Is.SameAs(definition));
            Assert.That(playbackPort.LastContext.DebugTag, Is.EqualTo("TopologyTransitionBlocked"));
        }

        [Test]
        public void Controller_TopologyTransitionBlockedCooldown_Expires()
        {
            using var scope = new TestAssetScope();
            var clock = new FakeClock();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(map, CreateRequiredEntries(scope, definition));
            var playbackPort = new FakeGameplayAudioPlaybackPort();
            var controller = new PlayerLocomotionAudioPresentationController(
                new GameplayPresentationStateStore(),
                clock.Now);
            controller.AttachRuntime(playbackPort, map);
            var signal = CreateBlockedSignal();

            controller.RefreshSignals(
                CreateTickResult(1, playerTopologyTransitionBlockedSignals: new[] { signal }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            clock.Advance(0.15d);
            controller.RefreshSignals(
                CreateTickResult(2, playerTopologyTransitionBlockedSignals: new[] { signal }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            clock.Advance(0.151d);
            controller.RefreshSignals(
                CreateTickResult(3, playerTopologyTransitionBlockedSignals: new[] { signal }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.Play2DCalls, Is.EqualTo(2));
        }

        [Test]
        public void Controller_TopologyTransitionBlockedCooldown_ClearOnResetAllowsReplay()
        {
            using var scope = new TestAssetScope();
            var clock = new FakeClock();
            var map = scope.CreateMap();
            var definition = scope.CreateDefinition(AudioCategory.Sfx, loop: false);
            SetEntries(map, CreateRequiredEntries(scope, definition));
            var playbackPort = new FakeGameplayAudioPlaybackPort();
            var controller = new PlayerLocomotionAudioPresentationController(
                new GameplayPresentationStateStore(),
                clock.Now);
            controller.AttachRuntime(playbackPort, map);
            var signal = CreateBlockedSignal();

            controller.RefreshSignals(
                CreateTickResult(1, playerTopologyTransitionBlockedSignals: new[] { signal }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();
            controller.ResetSession();
            controller.RefreshSignals(
                CreateTickResult(2, playerTopologyTransitionBlockedSignals: new[] { signal }),
                stepIntervalSeconds: 0.2f);
            controller.PlayPlannedAudio();

            Assert.That(playbackPort.Play2DCalls, Is.EqualTo(2));
        }

        [Test]
        public void Map_RejectsMissingTopologyTransitionBlockedBinding_WhenRequired()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            SetEntries(
                map,
                (PlayerLocomotionAudioCue.WalkStep,
                    scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))));

            var exception = Assert.Throws<InvalidOperationException>(
                () => map.ValidateRequiredCuesOrThrow(PlayerLocomotionAudioCueCatalog.RequiredOneShotV1));

            Assert.That(exception.Message, Does.Contain("TopologyTransitionBlocked"));
        }

        [Test]
        public void Map_RejectsLoopingTopologyTransitionBlockedDefinition()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            SetEntries(
                map,
                (PlayerLocomotionAudioCue.WalkStep,
                    scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))),
                (PlayerLocomotionAudioCue.TopologyTransitionBlocked,
                    scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: true))));

            var exception = Assert.Throws<InvalidOperationException>(
                () => map.ValidateRequiredCuesOrThrow(PlayerLocomotionAudioCueCatalog.RequiredOneShotV1));

            Assert.That(exception.Message, Does.Contain("only allows one-shot definitions"));
        }

        [Test]
        public void Map_RejectsUiCategoryTopologyTransitionBlockedDefinition()
        {
            using var scope = new TestAssetScope();
            var map = scope.CreateMap();
            SetEntries(
                map,
                (PlayerLocomotionAudioCue.WalkStep,
                    scope.CreateBinding(scope.CreateDefinition(AudioCategory.Sfx, loop: false))),
                (PlayerLocomotionAudioCue.TopologyTransitionBlocked,
                    scope.CreateBinding(scope.CreateDefinition(AudioCategory.Ui, loop: false))));

            var exception = Assert.Throws<InvalidOperationException>(
                () => map.ValidateRequiredCuesOrThrow(PlayerLocomotionAudioCueCatalog.RequiredOneShotV1));

            Assert.That(exception.Message, Does.Contain("category"));
        }

        private static TickPlayerLocomotionPresentationSignal CreateWalkLoopSignal(int entityId)
        {
            return new TickPlayerLocomotionPresentationSignal(
                entityId,
                shouldPlayWalkLoop: true,
                moveMotionGeneratedThisTick: true,
                waitingForNextMoveCadence: false,
                Direction.Right);
        }

        private static EntityState CreatePlayerEntity(int hp = 3)
        {
            return new EntityState
            {
                entityId = 10,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals = null,
            IEnumerable<TickPlayerOutcomePresentationSignal> playerOutcomeSignals = null,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals = null,
            IEnumerable<TickPlayerDeathHoldPresentationSignal> playerDeathHoldSignals = null,
            IEnumerable<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals = null,
            IEnumerable<TickVisibilityChange> visibilityChanges = null,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals = null,
            IEnumerable<EntityState> finalEntities = null)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges ?? Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals ?? Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals ?? Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                playerDeathHoldSignals: playerDeathHoldSignals,
                playerOutcomeSignals: playerOutcomeSignals,
                playerTopologyTransitionBlockedSignals: playerTopologyTransitionBlockedSignals);

            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities ?? Array.Empty<EntityState>(),
                Array.Empty<string>(),
                topology,
                presentationData,
                string.Empty,
                TickTrace.Empty);
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

        private static (PlayerLocomotionAudioCue cue, AudioBinding binding)[] CreateRequiredEntries(
            TestAssetScope scope,
            AudioDefinition sharedDefinition)
        {
            return new[]
            {
                (PlayerLocomotionAudioCue.WalkStep, scope.CreateBinding(sharedDefinition)),
                (PlayerLocomotionAudioCue.TopologyTransitionBlocked, scope.CreateBinding(sharedDefinition)),
            };
        }

        private static TickPlayerTopologyTransitionBlockedSignal CreateBlockedSignal(
            Direction direction = Direction.Down,
            FaceId originFace = FaceId.Floor,
            FaceId candidateFace = FaceId.Back,
            FaceId sourceBottomFace = FaceId.Floor,
            FaceId requiredBottomFace = FaceId.Back,
            CubeRotationKind rotationKind = CubeRotationKind.Backward,
            TickTraversalBlockerKind primaryBlockerKind = TickTraversalBlockerKind.Terrain)
        {
            return new TickPlayerTopologyTransitionBlockedSignal(
                entityId: 10,
                direction: direction,
                originCell: new SurfaceCell(originFace, 0, 0),
                candidateCell: new SurfaceCell(candidateFace, 0, 1),
                sourceTopology: new CubeTopologyState(sourceBottomFace),
                requiredTopology: new CubeTopologyState(requiredBottomFace),
                rotationKind: rotationKind,
                primaryBlockerKind: primaryBlockerKind);
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

        private sealed class FakeClock
        {
            private double _now;

            public double Now()
            {
                return _now;
            }

            public void Advance(double seconds)
            {
                _now += seconds;
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
