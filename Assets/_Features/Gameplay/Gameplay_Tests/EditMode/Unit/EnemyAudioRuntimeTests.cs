using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyAudioRuntimeTests
    {
        private const string EnemyPrefabRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs";

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_MapsEnemyPresentationSignalsToDocumentedCues()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, targetCell),
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    },
                    enemyActionSignals: new[]
                    {
                        new TickEnemyActionPresentationSignal(
                            20,
                            EnemyActionKind.Melee,
                            activeActionSequence: 1,
                            startedThisTick: true,
                            canceledThisTick: false,
                            executedThisTick: true,
                            startedRecoveryThisTick: false),
                    },
                    enemyJumpSignals: new[]
                    {
                        new TickEnemyJumpPresentationSignal(
                            23,
                            sequence: 1,
                            EnemyJumpPhase.Cooldown,
                            startedWindupThisTick: false,
                            startedAirborneThisTick: false,
                            landedThisTick: true,
                            retryThisTick: false),
                    },
                    enemyUtilitySignals: new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            21,
                            EnemyUtilityPresentationKind.LockNearbyBoxes,
                            EnemyUtilityPresentationPhase.WindupStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 3),
                        new TickEnemyUtilityPresentationSignal(
                            21,
                            EnemyUtilityPresentationKind.LockNearbyBoxes,
                            EnemyUtilityPresentationPhase.RecoverStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 3),
                    },
                    summonWindupWarnings: new[]
                    {
                        new TickSummonWindupWarningSignal(
                            sourceEntityId: 22,
                            effectIndex: 0,
                            sourceCell,
                            topology,
                            Direction.Right,
                            windupStartTick: 1,
                            windupEndTick: 2,
                            activationSequence: 1,
                            tickIndex: 1,
                            presentationSeed: 0),
                    },
                    entityExitSignals: new[]
                    {
                        new TickEntityExitPresentationSignal(
                            24,
                            TickEntityExitCause.EnemyDeath,
                            sourceCell,
                            topology,
                            Direction.Left,
                            EntityType.Unit),
                    }),
                new[]
                {
                    CreateUnit(10, UnitRole.Player),
                    CreateUnit(20, UnitRole.Enemy),
                    CreateUnit(21, UnitRole.Enemy),
                    CreateUnit(22, UnitRole.Enemy),
                    CreateUnit(23, UnitRole.Enemy),
                });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[]
                {
                    (20, EnemyAudioCue.Move),
                    (20, EnemyAudioCue.Act),
                    (20, EnemyAudioCue.Plasma),
                    (21, EnemyAudioCue.Act),
                    (21, EnemyAudioCue.GravityField),
                    (22, EnemyAudioCue.Act),
                    (23, EnemyAudioCue.Landing),
                    (24, EnemyAudioCue.Death),
                }));
        }

        [Test]
        [Category("Extended")]
        public void LegacyEntityMotionMove_StillPlansEnemyMoveCue()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    }),
                new[] { CreateUnit(20, UnitRole.Enemy) });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.Move) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyKinematicMotionTrack_PlansEnemyMoveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(
                            20,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            startedTick: 1,
                            elapsedTicks: 1,
                            totalTicks: 16),
                    }),
                new[] { CreateUnit(20, UnitRole.Enemy) });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (20, EnemyAudioCue.Move) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyKinematicMotionTrack_ContinuationTick_DoesNotPlanMoveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(
                            20,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            startedTick: 1,
                            elapsedTicks: 2,
                            totalTicks: 16),
                    }),
                new[] { CreateUnit(20, UnitRole.Enemy) },
                tickIndex: 2);

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void KinematicHeldOrTerminalTrack_DoesNotPlanMoveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(
                            20,
                            MotionMode.Held,
                            ForcedMotionOp.None,
                            sourceLocalOffset: new KinematicOffset2(KinematicFixed.FromRaw(512), KinematicFixed.Zero),
                            destinationLocalOffset: new KinematicOffset2(KinematicFixed.FromRaw(512), KinematicFixed.Zero)),
                        CreateKinematicTrack(
                            21,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            terminalKind: TickKinematicMotionTerminalKind.Interrupted),
                        CreateKinematicTrack(22, MotionMode.Settled, ForcedMotionOp.None),
                    }),
                new[]
                {
                    CreateUnit(20, UnitRole.Enemy),
                    CreateUnit(21, UnitRole.Enemy),
                    CreateUnit(22, UnitRole.Enemy),
                });

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void LegacyAndKinematicSameEntity_DedupesMoveCue()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    entityMotions: new[]
                    {
                        new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, targetCell),
                    },
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(
                            20,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            startedTick: 1,
                            elapsedTicks: 1,
                            totalTicks: 16),
                    }),
                new[] { CreateUnit(20, UnitRole.Enemy) });

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Count(request => request.OwnerEntityId == 20 && request.Cue == EnemyAudioCue.Move),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void NonEnemyKinematicTrack_DoesNotPlanEnemyMoveCue()
        {
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(
                CreatePresentationData(
                    kinematicMotionTracks: new[]
                    {
                        CreateKinematicTrack(10, MotionMode.Voluntary, ForcedMotionOp.None),
                        CreateKinematicTrack(
                            30,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            entityType: EntityType.Box),
                        CreateKinematicTrack(
                            40,
                            MotionMode.Voluntary,
                            ForcedMotionOp.None,
                            entityType: EntityType.Projectile),
                    }),
                new[]
                {
                    CreateUnit(10, UnitRole.Player),
                    CreateEntity(30, EntityType.Box),
                    CreateEntity(40, EntityType.Projectile),
                });

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void SunwheelKinematicMove_WithMoveProfile_ProducesMoveRequest()
        {
            var rootObject = new GameObject(nameof(SunwheelKinematicMove_WithMoveProfile_ProducesMoveRequest));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        kinematicMotionTracks: new[]
                        {
                            CreateKinematicTrack(
                                20,
                                MotionMode.Voluntary,
                                ForcedMotionOp.None,
                                startedTick: 1,
                                elapsedTicks: 1,
                                totalTicks: 16),
                        }),
                    new[] { enemy }));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Move" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_DefaultTiming_UsesDocumentedTwentyTpsIntervals()
        {
            var gate = new EnemyMoveCadenceGate();

            gate.Configure(simulationTicksPerSecond: 20);

            Assert.That(gate.PerEntityMoveMinIntervalTicks, Is.EqualTo(140));
            Assert.That(gate.GlobalMoveMinIntervalTicks, Is.EqualTo(60));
            Assert.That(gate.JitterTicks, Is.EqualTo(20));
            Assert.That(gate.MaxMoveRequestsPerTick, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_PerEntityInterval_ThrottlesSameEnemyMove()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 16,
                globalMoveMinIntervalTicks: 0,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 10);

            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 1), Is.True);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 16), Is.False);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 17), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_GlobalInterval_ThrottlesDifferentEnemyMoves()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 0,
                globalMoveMinIntervalTicks: 4,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 10);

            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 1), Is.True);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 21, tickIndex: 4), Is.False);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 21, tickIndex: 5), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_TickBudget_AllowsOnlyConfiguredMovesPerTick()
        {
            var gate = new EnemyMoveCadenceGate(
                perEntityMoveMinIntervalTicks: 0,
                globalMoveMinIntervalTicks: 0,
                jitterTicks: 0,
                maxMoveRequestsPerTick: 1);

            Assert.That(gate.ShouldPlayMove(ownerEntityId: 20, tickIndex: 1), Is.True);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 21, tickIndex: 1), Is.False);
            Assert.That(gate.ShouldPlayMove(ownerEntityId: 21, tickIndex: 2), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyMoveCadenceGate_Jitter_IsDeterministicAndBounded()
        {
            var first = EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(20, 10, jitterTicks: 4);
            var second = EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(20, 10, jitterTicks: 4);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.InRange(0, 4));
            Assert.That(EnemyMoveCadenceGate.ComputeDeterministicJitterTicks(20, 10, jitterTicks: 0), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_EnemyMoveCadence_ThrottlesMoveButNotActionCue()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_EnemyMoveCadence_ThrottlesMoveButNotActionCue));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()),
                new EnemyAudioEntrySpec(EnemyAudioCue.Act, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var enemy = CreateUnit(20, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        entityMotions: new[]
                        {
                            new TickEntityMotion(enemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                        },
                        enemyActionSignals: new[] { CreateEnemyActionStartedSignal(enemy.entityId) }),
                    new[] { enemy },
                    tickIndex: 1));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        entityMotions: new[]
                        {
                            new TickEntityMotion(enemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                        },
                        enemyActionSignals: new[] { CreateEnemyActionStartedSignal(enemy.entityId) }),
                    new[] { enemy },
                    tickIndex: 2));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Move", "Act", "Act" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_EnemyMoveCadence_AppliesGlobalTickBudget()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_EnemyMoveCadence_AppliesGlobalTickBudget));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var firstEnemy = CreateUnit(20, UnitRole.Enemy);
                var secondEnemy = CreateUnit(21, UnitRole.Enemy);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { firstEnemy, secondEnemy }, new CubeTopologyState(FaceId.Floor));
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        entityMotions: new[]
                        {
                            new TickEntityMotion(firstEnemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                            new TickEntityMotion(secondEnemy.entityId, TickEntityMotionKind.Move, sourceCell, targetCell),
                        }),
                    new[] { firstEnemy, secondEnemy },
                    tickIndex: 1));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Move" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_SummonWindupStartTick_EmitsSingleActCue()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                summonWindupWarnings: new[]
                {
                    new TickSummonWindupWarningSignal(
                        sourceEntityId: 22,
                        effectIndex: 0,
                        sourceCell,
                        topology,
                        Direction.Right,
                        windupStartTick: 10,
                        windupEndTick: 14,
                        activationSequence: 1,
                        tickIndex: 10,
                        presentationSeed: 0),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(
                requests.Select(request => (request.OwnerEntityId, request.Cue)).ToArray(),
                Is.EqualTo(new[] { (22, EnemyAudioCue.Act) }));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequestPlanner_SummonWindupActiveTick_DoesNotEmitActCue()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var planner = new EnemyAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                summonWindupWarnings: new[]
                {
                    new TickSummonWindupWarningSignal(
                        sourceEntityId: 22,
                        effectIndex: 0,
                        sourceCell,
                        topology,
                        Direction.Right,
                        windupStartTick: 10,
                        windupEndTick: 14,
                        activationSequence: 1,
                        tickIndex: 12,
                        presentationSeed: 0),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioProfile_ValidateOrThrow_RejectsInvalidEntries()
        {
            using var duplicateProfile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()),
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, CreateDefinitionSpec()));
            using var emptyCueProfile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.None, CreateDefinitionSpec()));
            using var loopingProfile = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Death, CreateDefinitionSpec(loop: true)));

            Assert.That(
                Assert.Throws<InvalidOperationException>(() => duplicateProfile.Profile.ValidateOrThrow()).Message,
                Does.Contain("duplicate enemy audio cue 'Move'"));
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => emptyCueProfile.Profile.ValidateOrThrow()).Message,
                Does.Contain("empty enemy audio cue"));
            Assert.That(
                Assert.Throws<InvalidOperationException>(() => loopingProfile.Profile.ValidateOrThrow()).Message,
                Does.Contain("only allows one-shot definitions"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_EnemyAudioDeathProfileSuppressesGenericEnemyDeath()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_Present_EnemyAudioDeathProfileSuppressesGenericEnemyDeath));
            using var mapBundle = CreateGameplayAudioMap();
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Death, CreateDefinitionSpec()));
            try
            {
                var presenter = CreatePresenter(rootObject, new EnemyAudioViewFactory(rootObject.transform, profileBundle.Profile));
                var playbackPort = new RecordingGameplayAudioPlaybackPort();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.AttachGameplayAudioRuntime(playbackPort, mapBundle.Map);
                presenter.PresentInitial(new[] { CreateUnit(20, UnitRole.Enemy) }, topology);
                presenter.Present(CreateTickResult(
                    CreatePresentationData(
                        entityExitSignals: new[]
                        {
                            new TickEntityExitPresentationSignal(
                                20,
                                TickEntityExitCause.EnemyDeath,
                                sourceCell,
                                topology,
                                Direction.Left,
                                EntityType.Unit),
                        }),
                    finalEntities: Array.Empty<EntityState>()));

                Assert.That(
                    playbackPort.TwoDCalls.Select(call => call.Context.DebugTag).ToArray(),
                    Is.EqualTo(new[] { "Death" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void EnemyPrefabs_HaveEnemyAudioAuthoring_WithExpectedMonsterSoundCoverage()
        {
            foreach (var expectation in PrefabExpectations())
            {
                var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(expectation.Path);
                Assert.That(view, Is.Not.Null, $"Missing enemy view prefab at '{expectation.Path}'.");

                var authoring = view.GetComponent<EnemyAudioAuthoring>();
                Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EnemyAudioAuthoring)} on '{expectation.Path}'.");
                Assert.That(authoring.Profile, Is.Not.Null, $"Missing profile on '{expectation.Path}'.");
                Assert.DoesNotThrow(() => authoring.Validate());

                foreach (var cue in expectation.Cues)
                {
                    Assert.That(
                        authoring.Profile.HasCue(cue),
                        Is.True,
                        $"Expected '{expectation.Path}' to define '{EnemyAudioCueCatalog.Format(cue)}'.");
                }
            }
        }

        private static IReadOnlyList<EnemyPrefabExpectation> PrefabExpectations()
        {
            return new[]
            {
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_Sunwheel.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_Astreton.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Landing,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_BlackEye.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Act,
                    EnemyAudioCue.Plasma,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_DrSaturn.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Act,
                    EnemyAudioCue.GravityField,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_JPeter.prefab",
                    EnemyAudioCue.Act,
                    EnemyAudioCue.Death),
                new EnemyPrefabExpectation(
                    $"{EnemyPrefabRoot}/EnemyView_Startis.prefab",
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Death),
            };
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
            IReadOnlyList<EntityState> finalEntities = null,
            int tickIndex = 1)
        {
            return new TickResult(
                tickIndex,
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
            IReadOnlyList<TickEntityMotion> entityMotions = null,
            IReadOnlyList<TickEnemyActionPresentationSignal> enemyActionSignals = null,
            IReadOnlyList<TickEnemyJumpPresentationSignal> enemyJumpSignals = null,
            IReadOnlyList<TickEnemyUtilityPresentationSignal> enemyUtilitySignals = null,
            IReadOnlyList<TickSummonWindupWarningSignal> summonWindupWarnings = null,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals = null,
            IReadOnlyList<TickKinematicMotionTrack> kinematicMotionTracks = null)
        {
            return new TickPresentationData(
                entityMotions ?? Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals ?? Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals ?? Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                summonWindupWarnings: summonWindupWarnings ?? Array.Empty<TickSummonWindupWarningSignal>(),
                kinematicMotionTracks: kinematicMotionTracks ?? Array.Empty<TickKinematicMotionTrack>(),
                enemyUtilitySignals: enemyUtilitySignals ?? Array.Empty<TickEnemyUtilityPresentationSignal>());
        }

        private static TickKinematicMotionTrack CreateKinematicTrack(
            int entityId,
            MotionMode motionMode,
            ForcedMotionOp forcedMotionOp,
            EntityType entityType = EntityType.Unit,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None,
            KinematicOffset2? sourceLocalOffset = null,
            KinematicOffset2? destinationLocalOffset = null,
            int startedTick = 0,
            int elapsedTicks = 0,
            int totalTicks = 0)
        {
            return new TickKinematicMotionTrack(
                entityId,
                new SurfaceCell(FaceId.Floor, 0, 0),
                sourceLocalOffset ?? KinematicOffset2.Zero,
                new SurfaceCell(FaceId.Floor, 1, 0),
                destinationLocalOffset ?? KinematicOffset2.Zero,
                motionMode,
                forcedMotionOp,
                entityType,
                sourceTopology: null,
                destinationTopology: null,
                sourceFacing: Direction.Right,
                destinationFacing: Direction.Right,
                terminalKind: terminalKind,
                startedTick: startedTick,
                elapsedTicks: elapsedTicks,
                totalTicks: totalTicks);
        }

        private static TickEnemyActionPresentationSignal CreateEnemyActionStartedSignal(int entityId)
        {
            return new TickEnemyActionPresentationSignal(
                entityId,
                EnemyActionKind.Melee,
                activeActionSequence: 1,
                startedThisTick: true,
                canceledThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false);
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

        private static EntityState CreateEntity(int entityId, EntityType entityType)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = entityType,
                unitRole = UnitRole.None,
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

        private static EnemyAudioProfileBundle CreateEnemyAudioProfile(params EnemyAudioEntrySpec[] entrySpecs)
        {
            var profile = ScriptableObject.CreateInstance<EnemyAudioProfile>();
            var trackedObjects = new List<UnityEngine.Object> { profile };
            var entries = new EnemyAudioEntry[entrySpecs.Length];

            for (var i = 0; i < entrySpecs.Length; i++)
            {
                AudioBinding binding = null;
                if (entrySpecs[i].DefinitionSpec.HasValue)
                {
                    var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                    definition.name = entrySpecs[i].Cue.ToString();
                    var clip = AudioClip.Create(definition.name, 4410, 1, 44100, false);
                    trackedObjects.Add(clip);
                    trackedObjects.Add(definition);
                    SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                    SetSerializedField(typeof(AudioDefinition), definition, "category", entrySpecs[i].DefinitionSpec.Value.Category);
                    SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
                    SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
                    SetSerializedField(typeof(AudioDefinition), definition, "loop", entrySpecs[i].DefinitionSpec.Value.Loop);

                    binding = new AudioBinding();
                    SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                    SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
                    SetSerializedField(typeof(AudioBinding), binding, "policy", null);
                }

                entries[i] = new EnemyAudioEntry
                {
                    Cue = entrySpecs[i].Cue,
                    Binding = binding,
                    IsOptional = entrySpecs[i].IsOptional,
                };
            }

            SetSerializedField(typeof(EnemyAudioProfile), profile, "entries", entries);
            return new EnemyAudioProfileBundle(profile, trackedObjects);
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
                SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
                SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
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

        private readonly struct EnemyAudioEntrySpec
        {
            public EnemyAudioEntrySpec(
                EnemyAudioCue cue,
                DefinitionSpec? definitionSpec,
                bool isOptional = false)
            {
                Cue = cue;
                DefinitionSpec = definitionSpec;
                IsOptional = isOptional;
            }

            public EnemyAudioCue Cue { get; }

            public DefinitionSpec? DefinitionSpec { get; }

            public bool IsOptional { get; }
        }

        private readonly struct EnemyPrefabExpectation
        {
            public EnemyPrefabExpectation(string path, params EnemyAudioCue[] cues)
            {
                Path = path;
                Cues = cues;
            }

            public string Path { get; }

            public IReadOnlyList<EnemyAudioCue> Cues { get; }
        }

        private sealed class EnemyAudioProfileBundle : IDisposable
        {
            private readonly List<UnityEngine.Object> _trackedObjects;

            public EnemyAudioProfileBundle(EnemyAudioProfile profile, List<UnityEngine.Object> trackedObjects)
            {
                Profile = profile;
                _trackedObjects = trackedObjects;
            }

            public EnemyAudioProfile Profile { get; }

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
                Play2D(definition, context);
            }
        }

        private sealed class EnemyAudioViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;
            private readonly EnemyAudioProfile _profile;

            public EnemyAudioViewFactory(Transform parent, EnemyAudioProfile profile)
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
                viewObject.AddComponent<EntityEffectPresentationAuthoring>();

                if (_profile != null &&
                    entity.unitRole == UnitRole.Enemy)
                {
                    var authoring = viewObject.AddComponent<EnemyAudioAuthoring>();
                    SetSerializedField(typeof(EnemyAudioAuthoring), authoring, "profile", _profile);
                }

                return view;
            }
        }
    }
}
