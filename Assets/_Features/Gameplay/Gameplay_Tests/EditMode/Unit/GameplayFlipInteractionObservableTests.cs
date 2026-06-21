using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayFlipInteractionObservableTests
    {
        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_FlipInteraction_PreservesCommittedRootsAndResetsChildVisuals()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_FlipInteraction_PreservesCommittedRootsAndResetsChildVisuals");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var viewFactory = new FlipInteractionObservableViewFactory(registry.transform);
                var binder = new GameplayEntityViewBinder(registry, viewFactory);
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var topology = new CubeTopologyState(FaceId.Floor);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var boxCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var finalEntities = new[]
                {
                    CreatePlayerUnit(10, playerCell),
                    CreateBox(20, boxCell),
                };

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(finalEntities, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(registry.TryGetView(20, out var boxView), Is.True);
                Assert.That(viewFactory.BoxVisualRoot, Is.Not.Null);

                var playerRootBaseline = playerView.transform.localPosition;
                var boxRootBaseline = boxView.transform.localPosition;
                var boxVisualBaseLocalPosition = viewFactory.BoxVisualRoot.localPosition;
                var boxVisualBaseLocalRotation = viewFactory.BoxVisualRoot.localRotation;

                AssertPositionApproximately(viewFactory.BoxVisualRoot.localPosition, boxVisualBaseLocalPosition);
                AssertRotationApproximately(viewFactory.BoxVisualRoot.localRotation, boxVisualBaseLocalRotation);

                presenter.Present(CreateTickResult(
                    tickIndex: 1,
                    finalEntities,
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(
                                10,
                                PlayerActionKind.Flip,
                                1,
                                startedThisTick: true,
                                completedThisTick: false,
                                canceledThisTick: false,
                                executedThisTick: true,
                                isRecoveryPhase: true,
                                targetEntityId: 20,
                                direction: Direction.Right),
                        })));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
                Assert.That(presenter.IsPresentationActive, Is.True);
                AssertPositionApproximately(playerView.transform.localPosition, playerRootBaseline);
                AssertPositionApproximately(boxView.transform.localPosition, boxRootBaseline);
                Assert.That(
                    Vector3.Distance(viewFactory.BoxVisualRoot.localPosition, boxVisualBaseLocalPosition),
                    Is.GreaterThan(0.001f));
                Assert.That(
                    Quaternion.Angle(viewFactory.BoxVisualRoot.localRotation, boxVisualBaseLocalRotation),
                    Is.GreaterThan(0.001f));

                presenter.UpdatePresentation(0.05f);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
                Assert.That(presenter.IsPresentationActive, Is.True);
                AssertPositionApproximately(playerView.transform.localPosition, playerRootBaseline);
                AssertPositionApproximately(boxView.transform.localPosition, boxRootBaseline);
                Assert.That(
                    Vector3.Distance(viewFactory.BoxVisualRoot.localPosition, boxVisualBaseLocalPosition),
                    Is.GreaterThan(0.001f));
                Assert.That(
                    Quaternion.Angle(viewFactory.BoxVisualRoot.localRotation, boxVisualBaseLocalRotation),
                    Is.GreaterThan(0.001f));

                presenter.Present(CreateTickResult(
                    tickIndex: 2,
                    finalEntities,
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(
                                10,
                                PlayerActionKind.None,
                                0,
                                startedThisTick: false,
                                completedThisTick: true,
                                canceledThisTick: false,
                                direction: Direction.Right),
                        })));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(presenter.IsPresentationActive, Is.False);
                AssertPositionApproximately(playerView.transform.localPosition, playerRootBaseline);
                AssertPositionApproximately(boxView.transform.localPosition, boxRootBaseline);
                AssertPositionApproximately(viewFactory.BoxVisualRoot.localPosition, boxVisualBaseLocalPosition);
                AssertRotationApproximately(viewFactory.BoxVisualRoot.localRotation, boxVisualBaseLocalRotation);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
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

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            EntityState[] finalEntities,
            CubeTopologyState topology,
            TickPresentationData presentationData)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                presentationData,
                string.Empty,
                TickTrace.Empty);
        }

        private static void AssertPositionApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f));
        }

        private static void AssertRotationApproximately(Quaternion actual, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.001f));
        }

        private sealed class FlipInteractionObservableViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;

            public FlipInteractionObservableViewFactory(Transform parent)
            {
                _parent = parent;
            }

            public Transform BoxVisualRoot { get; private set; }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                viewObject.transform.localPosition = Vector3.zero;
                viewObject.transform.localRotation = Quaternion.identity;
                viewObject.transform.localScale = Vector3.one;

                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (entity.type == EntityType.Unit &&
                    entity.unitRole == UnitRole.Player)
                {
                    return view;
                }

                BoxVisualRoot = new GameObject("BoxVisualRoot").transform;
                BoxVisualRoot.SetParent(view.ModelRoot, worldPositionStays: false);
                BoxVisualRoot.localPosition = Vector3.zero;
                BoxVisualRoot.localRotation = Quaternion.identity;

                var gripPoint = new GameObject("GripPoint").transform;
                gripPoint.SetParent(BoxVisualRoot, worldPositionStays: false);
                gripPoint.localPosition = new Vector3(0.25f, 0f, 0f);

                var boxDriver = viewObject.AddComponent<BoxFlipInteractionDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(boxDriver, "visualRoot", BoxVisualRoot);
                PlayerViewPrefabTestUtility.SetSerializedField(boxDriver, "gripPoint", gripPoint);
                return view;
            }
        }
    }
}
