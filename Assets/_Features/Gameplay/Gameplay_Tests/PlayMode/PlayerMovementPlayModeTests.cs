using System.Collections;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Model.Phases;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    // Final presentation guardrail:
    // Keep the input/tick scenarios in this file and validate them through the
    // cube projector plus board-root world transform instead of strip-space literals.
    public sealed class PlayerMovementPlayModeTests : InputTestFixture
    {
        private Keyboard _keyboard;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator PlayerMove_PlayMode_PresenterRefreshesTransformAfterTick()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator PlayerMove_PlayMode_InputActionCallback_ProducesTickMove()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                actions: actions);

            Press(_keyboard.dKey);
            yield return null;

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);

            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        public IEnumerator PlayerMove_PlayMode_MoveIntoPushBox_DoesNotSlideWithoutPushInput()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                },
                actions: actions);

            Press(_keyboard.dKey);
            yield return null;

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        public IEnumerator PlayerMove_PlayMode_PushInputStartsSlidingBoxWithoutMovingPlayer()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_PushBufferedAtTickBoundary_PrioritizesPushOverMove()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            Assert.That(host.ViewRegistry.TryGetView(30, out var boxView), Is.True);
            Assert.That(boxView.gameObject.activeSelf, Is.False);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_FlipBufferedAtTickBoundary_PrioritizesFlipOverMove()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_PushBufferedDuringRepeatLock_IsNotDropped()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            host.InputHost.BufferPush();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_FlipBufferedDuringRepeatLock_IsNotDropped()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            host.InputHost.BufferFlip();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_NoSampledDirection_DropsBufferedPushAndFlip()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 31, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
            });

            host.InputHost.SetRawMoveInput(new Vector2(1f, 1f));
            host.InputHost.BufferPush();
            host.InputHost.BufferFlip();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);
            AssertViewMatchesProjectedState(host, entityId: 31);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_InteractionTick_DoesNotConsumePlainMoveCadence()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_PushBufferedDuringInitialDelay_UsesSampledDirection()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                    CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 4, 0)),
                },
                actions: null,
                staticEntityLogics: null,
                initialMoveDelayTicks: 2);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.BufferPush();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            for (var i = 0; i < host.TimingProfile.InitialMoveDelayTicks - 1; i++)
            {
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(0f);
                AssertViewMatchesProjectedState(host, entityId: 10);
            }

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_FlipBufferedDuringDirectionChangeDelay_UsesSampledDirection()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Flip),
                },
                actions: null,
                staticEntityLogics: null,
                initialMoveDelayTicks: 2,
                directionChangeConsumesDelay: true);

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.SetRawMoveInput(Vector2.left);
            host.InputHost.BufferFlip();
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);
            AssertViewMatchesProjectedState(host, entityId: 30);

            for (var i = 0; i < host.TimingProfile.InitialMoveDelayTicks - 1; i++)
            {
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(0f);
                AssertViewMatchesProjectedState(host, entityId: 10);
            }

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator GameplayInputHost_Reenable_RebindsInputActions()
        {
            var actions = CreateKeyboardMoveActions();
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                actions: actions);

            host.InputHost.enabled = false;
            yield return null;

            Press(_keyboard.dKey);
            yield return null;

            host.InputHost.enabled = true;
            yield return null;

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);

            AssertViewMatchesProjectedState(host, entityId: 10);

            Release(_keyboard.dKey);
            yield return DestroyHost(host, actions);
        }

        [UnityTest]
        public IEnumerator PlayerMove_PlayMode_HoldInputRepeatsAtConfiguredTickInterval()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            for (var i = 0; i < host.TimingProfile.RepeatedMoveIntervalTicks - 2; i++)
            {
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(0f);
            }

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds);
            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator PlayerMove_PlayMode_SpawnedEntity_BecomesVisibleAfterTick()
        {
            var host = CreateHost(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                actions: null,
                staticEntityLogics: new IEntityLogic[]
                {
                    new FireProjectileLogic(sourceId: 10, priority: 5),
                });

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);

            Assert.That(host.ViewRegistry.TryGetView(11, out var projectileView), Is.True);
            Assert.That(projectileView.gameObject.activeSelf, Is.True);
            AssertViewMatchesProjectedState(host, entityId: 11);

            yield return DestroyHost(host);
        }

        [UnityTest]
        public IEnumerator PlayerMove_PlayMode_BlockedCell_DoesNotVisuallyDrift()
        {
            var host = CreateHost(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 1, 0)),
            });

            host.InputHost.SetRawMoveInput(Vector2.right);
            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            host.InputHost.RunSingleTick();
            host.Presenter.UpdatePresentation(0f);
            AssertViewMatchesProjectedState(host, entityId: 10);

            yield return DestroyHost(host);
        }

        private static GameplaySceneHost CreateHost(EntityState[] initialEntities)
        {
            return CreateHost(initialEntities, actions: null, staticEntityLogics: null);
        }

        private static GameplaySceneHost CreateHost(
            EntityState[] initialEntities,
            InputActionAsset actions,
            IEntityLogic[] staticEntityLogics = null,
            int initialMoveDelayTicks = 0,
            int repeatedMoveIntervalTicks = 2,
            bool directionChangeConsumesDelay = false)
        {
            var hostObject = new GameObject("PlayModeGameplaySceneHost");
            var host = hostObject.AddComponent<GameplaySceneHost>();

            host.Initialize(
                new GameplaySceneHostConfiguration
                {
                    Actions = actions,
                    AutoAdvanceTicks = false,
                    AutoCreateViews = true,
                    BoxSlideStepIntervalSeconds = 0.2f,
                    CellSize = 1f,
                    DirectionChangeConsumesDelay = directionChangeConsumesDelay,
                    FlipArcHeightInCells = 0.65f,
                    FlipMotionDurationSeconds = 0.2f,
                    InitialBoardBounds = new BoardBounds(new Vector2Int(-8, -8), new Vector2Int(8, 8)),
                    InitialMoveDelaySeconds = -1f,
                    InitialMoveDelayTicks = initialMoveDelayTicks,
                    InitialEntities = initialEntities,
                    MaxTicksPerFrame = 8,
                    MoveDeadzone = 0.5f,
                    PlayerEntityId = 10,
                    PushMotionDurationSeconds = 0.2f,
                    RepeatedMoveIntervalSeconds = -1f,
                    RepeatedMoveIntervalTicks = repeatedMoveIntervalTicks,
                    SimulationTicksPerSecond = 60,
                    StaticEntityLogics = staticEntityLogics ?? System.Array.Empty<IEntityLogic>(),
                    TickIntervalSeconds = 0.2f,
                });

            return host;
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
            };
        }

        private static EntityState CreateWall(int entityId, Vector2Int position)
        {
            return CreateWall(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }

        private static EntityState CreateBox(int entityId, Vector2Int position, BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position), capabilities);
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = capabilities,
            };
        }

        private static IEnumerator DestroyHost(GameplaySceneHost host, Object ownedActions = null)
        {
            if (host != null)
            {
                Object.Destroy(host.gameObject);
            }

            if (ownedActions != null)
            {
                Object.Destroy(ownedActions);
            }

            yield return null;
        }

        // TODO(CubeSurface3D): Retire this helper once playmode tests stop using
        // projector-derived world positions as their primary oracle. Preserve the scenario
        // coverage in this file instead of deleting the tests.
        private static Vector3 GetViewPosition(GameplaySceneHost host, int entityId)
        {
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            return view.transform.position;
        }

        private static void AssertViewMatchesProjectedState(GameplaySceneHost host, int entityId)
        {
            var snapshot = host.WorldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            var projector = new GameplayCubeProjector(snapshot.BoardBounds, 1f);
            Assert.That(
                projector.TryProjectEntityCell(entity.position, snapshot.Topology, entity.type, out var projectedPose),
                Is.True);
            Assert.That(
                GetViewPosition(host, entityId),
                Is.EqualTo(host.BoardRoot.transform.TransformPoint(projectedPose.LocalPosition)));
        }

        private static InputActionAsset CreateKeyboardMoveActions()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            var move = map.AddAction("Move", InputActionType.Value);
            var pushAction = map.AddAction("Push", InputActionType.Button);
            var flipAction = map.AddAction("Flip", InputActionType.Button);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            pushAction.AddBinding("<Keyboard>/e");
            flipAction.AddBinding("<Keyboard>/q");
            actions.AddActionMap(map);
            return actions;
        }

        private sealed class FireProjectileLogic : IEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _priority;
            private readonly int _sourceId;

            public FireProjectileLogic(int sourceId, int priority)
            {
                _sourceId = sourceId;
                _priority = priority;
            }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
            }

            public void CollectAttackIntents(WorldSnapshot snapshot, in TickInput input, List<RawAttackIntent> buffer)
            {
                if (snapshot.TryGetEntity(_sourceId, out var source) && source.hp > 0 && !source.markedForDeath)
                {
                    buffer.Add(RawAttackIntent.CreateFireProjectile(_sourceId, _priority));
                }
            }

            public bool ControlsEntity(int entityId, TickPhase phase)
            {
                return phase == TickPhase.Attack && entityId == _sourceId;
            }
        }
    }
}
