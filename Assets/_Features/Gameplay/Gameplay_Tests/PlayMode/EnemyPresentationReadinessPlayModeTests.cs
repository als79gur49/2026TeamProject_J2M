using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PresentationPlanning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Feature.Gameplay.Tests.PlayMode
{
#if UNITY_EDITOR
    public sealed class EnemyPresentationReadinessPlayModeTests
    {
        private const int JumpWindupEnemyId = 40;
        private const int JumpAirborneEnemyId = 41;
        private const int JumpLandEnemyId = 42;
        private const int ChargeWindupEnemyId = 43;
        private const int ChargeActiveEnemyId = 44;
        private const int ChargeRecoverEnemyId = 45;
        private const int DeathEnemyId = 46;
        private const string EnemyAnimatorControllerPath = "Assets/3DM/2BlackEye/BlackEye.controller";

        private static readonly CubeTopologyState Topology = new(FaceId.Floor);
        private static readonly BoardBounds Bounds = new(new Vector2Int(-1, -1), new Vector2Int(4, 3));
        private static readonly SurfaceCell SourceCell = new(FaceId.Floor, 0, 0);
        private static readonly SurfaceCell TargetCell = new(FaceId.Floor, 1, 0);

        [UnityTest]
        [Category("Core")]
        public IEnumerator EnemyPresentationReadiness_PlayMode_ProductionExecutorDrivesAnimatorCommandsAndDiagnostics()
        {
            var context = CreateHostContext(nameof(EnemyPresentationReadiness_PlayMode_ProductionExecutorDrivesAnimatorCommandsAndDiagnostics));
            try
            {
                var result = CreateEnemyPresentationTickResult(31);
                var determinismHash = result.DeterminismHash;
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();

                context.Host.Presenter.Present(result);
                yield return null;

                Assert.That(context.Host.Presenter.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
                Assert.That(context.Host.Presenter.EnemyPresentationOwnershipDiagnostics.ExecutedByLegacyCount, Is.Zero);
                Assert.That(context.Host.Presenter.EnemyPresentationOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(7));
                Assert.That(context.Host.Presenter.EnemyPresentationOwnershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount, Is.EqualTo(7));
                Assert.That(context.Host.Presenter.EnemyPresentationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);

                var diagnostics = context.Host.Presenter.EnemyPresentationExecutorDiagnostics;
                Assert.That(diagnostics.ObservedCueCount, Is.EqualTo(7));
                Assert.That(diagnostics.CommandRequestedCount, Is.EqualTo(7));
                Assert.That(diagnostics.CommandAppliedCount, Is.EqualTo(7));
                Assert.That(diagnostics.EnemyJumpCueMappedToLegacyCommandCount, Is.EqualTo(3));
                Assert.That(diagnostics.EnemyChargeCueMappedToLegacyCommandCount, Is.EqualTo(3));
                Assert.That(diagnostics.EnemyDeathCueMappedToLegacyCommandCount, Is.EqualTo(1));

                Assert.That(GetDriver(context, JumpWindupEnemyId).JumpWindupSignalCount, Is.EqualTo(1));
                Assert.That(GetDriver(context, JumpAirborneEnemyId).JumpAirborneSignalCount, Is.EqualTo(1));
                Assert.That(GetDriver(context, JumpLandEnemyId).LastCrossFadedStateName, Is.EqualTo("Move"));
                Assert.That(GetDriver(context, ChargeWindupEnemyId).WindupSignalCount, Is.EqualTo(1));
                Assert.That(GetDriver(context, ChargeActiveEnemyId).ChargeActiveSignalCount, Is.EqualTo(1));
                Assert.That(GetDriver(context, ChargeRecoverEnemyId).RecoverySignalCount, Is.EqualTo(1));
                Assert.That(GetDriver(context, DeathEnemyId).DeathSignalCount, Is.EqualTo(1));

                var telemetry = context.Host.Presenter.EnemyPresentationProductionTelemetrySnapshot;
                Assert.That(telemetry.IsProductionDefaultOwner, Is.True);
                Assert.That(telemetry.LastCueKey, Is.EqualTo(PresentationAnimationCueKey.EnemyDeath));
                Assert.That(telemetry.LastEnemyEntityId, Is.EqualTo(DeathEnemyId));
                Assert.That(telemetry.LastFailureReason, Is.EqualTo(EnemyPresentationTelemetryFailureReason.None));

                Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(context.Host.Presenter.HasBlockingPresentation, Is.False);
                Assert.That(context.Host.Presenter.IsTopologyTransitionActive, Is.False);

                context.Host.Presenter.PresentInitial(context.InitialEntities, Topology);
                yield return null;
                Assert.That(context.Host.Presenter.EnemyPresentationExecutorDiagnostics.CommandAppliedCount, Is.Zero);
                Assert.That(context.Host.Presenter.EnemyPresentationProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(EnemyPresentationTelemetryCleanupReason.ResetSession));
                Assert.That(GetDriver(context, DeathEnemyId).LastPresentationState.DidDie, Is.False);

                context.Host.Presenter.Present(CreateEnemyPresentationTickResult(32));
                context.Host.Presenter.DebugHardCleanupPresentationExtensions();
                Assert.That(context.Host.Presenter.EnemyPresentationExecutorDiagnostics.CommandAppliedCount, Is.Zero);
                Assert.That(context.Host.Presenter.EnemyPresentationProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(EnemyPresentationTelemetryCleanupReason.HardCleanupPresentationExtensions));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator EnemyPresentationReadiness_PlayMode_ExplicitLegacyRollbackBypassesExecutor()
        {
            var context = CreateHostContext(nameof(EnemyPresentationReadiness_PlayMode_ExplicitLegacyRollbackBypassesExecutor));
            try
            {
                context.Host.Presenter.ConfigureEnemyPresentationExecution(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper);
                context.Host.Presenter.Present(CreateEnemyPresentationTickResult(41));
                yield return null;

                Assert.That(context.Host.Presenter.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
                Assert.That(context.Host.Presenter.EnemyPresentationOwnershipDiagnostics.ExecutedByLegacyCount, Is.EqualTo(7));
                Assert.That(context.Host.Presenter.EnemyPresentationOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(context.Host.Presenter.EnemyPresentationExecutorDiagnostics.CommandAppliedCount, Is.Zero);
                Assert.That(GetDriver(context, DeathEnemyId).DeathSignalCount, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        private static EnemyPresentationSmokeContext CreateHostContext(string rootName)
        {
            var initialEntities = CreateInitialEntities();
            var hostObject = new GameObject(rootName);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<GameplaySceneHost>();
            hostObject.SetActive(true);
            host.Initialize(new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                CellSize = 1f,
                InitialBoardBounds = Bounds,
                InitialEntities = initialEntities,
                InitialTopology = Topology,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
                ViewFactory = new EnemyPresentationSmokeViewFactory(hostObject.transform),
            });

            return new EnemyPresentationSmokeContext(hostObject, host, initialEntities);
        }

        private static TickResult CreateEnemyPresentationTickResult(int tickIndex)
        {
            var result = new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetPrivateField(typeof(TickResult), result, "<PresentationData>k__BackingField", CreatePresentationData());
            SetPrivateField(typeof(TickResult), result, "<FinalTopology>k__BackingField", Topology);
            SetPrivateField(typeof(TickResult), result, "<DeterminismHash>k__BackingField", $"ENEMY-PRESENTATION-{tickIndex}");
            SetPrivateField(typeof(TickResult), result, "<Trace>k__BackingField", TickTrace.Empty);
            SetPrivateField(typeof(TickResult), result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetPrivateField(
                typeof(TickResult),
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState>(CreateInitialEntities())));
            SetPrivateField(
                typeof(TickResult),
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string> { $"AuthoritativeEnemyPresentation:{tickIndex}" }));
            return result;
        }

        private static TickPresentationData CreatePresentationData()
        {
            var enemyChargeSignals = new[]
            {
                new TickEnemyChargePresentationSignal(ChargeWindupEnemyId, 201, EnemyChargePhase.Windup, true, false, false, Direction.Down),
                new TickEnemyChargePresentationSignal(ChargeActiveEnemyId, 202, EnemyChargePhase.Active, false, true, false, Direction.Down),
                new TickEnemyChargePresentationSignal(ChargeRecoverEnemyId, 203, EnemyChargePhase.Recover, false, false, true, Direction.Down),
            };
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                new[]
                {
                    new TickEnemyJumpPresentationSignal(JumpWindupEnemyId, 101, EnemyJumpPhase.Windup, true, false, false, false, SourceCell, TargetCell, TargetCell, Direction.Right),
                    new TickEnemyJumpPresentationSignal(JumpAirborneEnemyId, 102, EnemyJumpPhase.Airborne, false, true, false, false, SourceCell, TargetCell, TargetCell, Direction.Right),
                    new TickEnemyJumpPresentationSignal(JumpLandEnemyId, 103, EnemyJumpPhase.Cooldown, false, false, true, false, SourceCell, TargetCell, TargetCell, Direction.Right),
                },
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        DeathEnemyId,
                        TickEntityExitCause.EnemyDeath,
                        SourceCell,
                        Topology,
                        Direction.Left,
                        EntityType.Unit,
                        sourceActorEntityId: 10,
                        anchorEntityId: null,
                        presentationSeed: 301,
                        timing: EntityExitPresentationTiming.Immediate,
                        hasPresentationTargetCell: true,
                        presentationTargetCell: SourceCell),
                },
                Array.Empty<FlipImpactPresentationSignal>());
            SetPrivateField(
                typeof(TickPresentationData),
                presentationData,
                "_enemyChargeSignals",
                new ReadOnlyCollection<TickEnemyChargePresentationSignal>(new List<TickEnemyChargePresentationSignal>(enemyChargeSignals)));
            return presentationData;
        }

        private static EntityState[] CreateInitialEntities()
        {
            return new[]
            {
                CreateEnemy(JumpWindupEnemyId, EnemyAiMode.Chase),
                CreateEnemy(JumpAirborneEnemyId, EnemyAiMode.Chase),
                CreateEnemy(JumpLandEnemyId, EnemyAiMode.Chase),
                CreateEnemy(ChargeWindupEnemyId, EnemyAiMode.Charge),
                CreateEnemy(ChargeActiveEnemyId, EnemyAiMode.Charge),
                CreateEnemy(ChargeRecoverEnemyId, EnemyAiMode.Charge),
                CreateEnemy(DeathEnemyId, EnemyAiMode.Chase),
            };
        }

        private static EntityState CreateEnemy(int entityId, EnemyAiMode aiMode)
        {
            return new EntityState
            {
                entityId = entityId,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                teamId = 2,
                position = CellForEnemy(entityId),
                facing = Direction.Right,
                hp = 3,
                maxHp = 3,
                aiMode = aiMode,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static SurfaceCell CellForEnemy(int entityId)
        {
            return entityId switch
            {
                JumpWindupEnemyId => new SurfaceCell(FaceId.Floor, 0, 0),
                JumpAirborneEnemyId => new SurfaceCell(FaceId.Floor, 1, 0),
                JumpLandEnemyId => new SurfaceCell(FaceId.Floor, 2, 0),
                ChargeWindupEnemyId => new SurfaceCell(FaceId.Floor, 0, 1),
                ChargeActiveEnemyId => new SurfaceCell(FaceId.Floor, 1, 1),
                ChargeRecoverEnemyId => new SurfaceCell(FaceId.Floor, 2, 1),
                DeathEnemyId => new SurfaceCell(FaceId.Floor, 3, 1),
                _ => SourceCell,
            };
        }

        private static EnemyAnimatorDriver GetDriver(EnemyPresentationSmokeContext context, int entityId)
        {
            Assert.That(context.Host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            var driver = view.GetComponent<EnemyAnimatorDriver>();
            Assert.That(driver, Is.Not.Null);
            return driver;
        }

        private static void SetPrivateField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private sealed class EnemyPresentationSmokeContext : IDisposable
        {
            private readonly GameObject _rootObject;

            public EnemyPresentationSmokeContext(
                GameObject rootObject,
                GameplaySceneHost host,
                EntityState[] initialEntities)
            {
                _rootObject = rootObject;
                Host = host;
                InitialEntities = initialEntities;
            }

            public GameplaySceneHost Host { get; }

            public EntityState[] InitialEntities { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class EnemyPresentationSmokeViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _hostRoot;

            public EnemyPresentationSmokeViewFactory(Transform hostRoot)
            {
                _hostRoot = hostRoot != null ? hostRoot : throw new ArgumentNullException(nameof(hostRoot));
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(ResolveEntityRoot(), worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                var animator = viewObject.AddComponent<Animator>();
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyAnimatorControllerPath);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null, EnemyAnimatorControllerPath);

                var driver = viewObject.AddComponent<EnemyAnimatorDriver>();
                SetPrivateField(typeof(EnemyAnimatorDriver), driver, "animator", animator);
                return view;
            }

            private Transform ResolveEntityRoot()
            {
                var boardRootTransform = _hostRoot.Find("GameplayBoardRoot");
                Assert.That(boardRootTransform, Is.Not.Null);
                var boardRoot = boardRootTransform.GetComponent<GameplayBoardRoot>();
                Assert.That(boardRoot, Is.Not.Null);
                Assert.That(boardRoot.EntityRoot, Is.Not.Null);
                return boardRoot.EntityRoot;
            }
        }
    }
#endif
}
