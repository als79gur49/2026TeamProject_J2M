using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TopologyVisualBridgeVisibilityControllerTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        [Category("Extended")]
        public void EvaluateBindingVisibility_UsesV1LogicalFacePairRule()
        {
            var outgoingBinding = new TopologyVisualBridgeBinding
            {
                FirstFace = FaceId.Floor,
                SecondFace = FaceId.Front,
            };
            var incomingBinding = new TopologyVisualBridgeBinding
            {
                FirstFace = FaceId.Front,
                SecondFace = FaceId.Ceiling,
            };
            var steadyTopology = new CubeTopologyState(FaceId.Floor);
            var inactiveTransition = TopologyTransitionVisualState.Inactive(steadyTopology, Quaternion.identity);
            var activeTransition = new TopologyTransitionVisualState(
                isActive: true,
                progress01: 0.5f,
                sourceTopology: new CubeTopologyState(FaceId.Floor),
                destinationTopology: new CubeTopologyState(FaceId.Front),
                rotationKind: CubeRotationKind.Forward,
                durationSeconds: 0.2f,
                presentedVisualRotation: Quaternion.identity,
                angularVelocityNormalized: 1f);

            Assert.That(
                TopologyVisualBridgeVisibilityController.EvaluateBindingVisibility(outgoingBinding, steadyTopology, inactiveTransition),
                Is.True);
            Assert.That(
                TopologyVisualBridgeVisibilityController.EvaluateBindingVisibility(outgoingBinding, steadyTopology, activeTransition),
                Is.False);
            Assert.That(
                TopologyVisualBridgeVisibilityController.EvaluateBindingVisibility(incomingBinding, steadyTopology, inactiveTransition),
                Is.False);
            Assert.That(
                TopologyVisualBridgeVisibilityController.EvaluateBindingVisibility(incomingBinding, steadyTopology, activeTransition),
                Is.True);
        }

        [Test]
        [Category("Full")]
        public void TopologyVisualBridgeVisibilityController_UsesTransitionUnionAndAvoidsRepeatedSetActiveChurn()
        {
            var hostObject = new GameObject("TopologyVisualBridgeVisibilityController_Host");
            var controllerObject = new GameObject("TopologyVisualBridgeVisibilityController_Controller");
            var outgoingBridge = new GameObject("OutgoingBridge");
            var incomingBridge = new GameObject("IncomingBridge");
            var hiddenBridge = new GameObject("HiddenBridge");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("TopologyVisualBridgeVisibilityController_PlayerPrefab");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateHostConfiguration(playerViewPrefab));
                SetPlayerForwardTopologyOvershootPose(host, entityId: 10);

                outgoingBridge.SetActive(false);
                incomingBridge.SetActive(false);
                hiddenBridge.SetActive(false);

                var controller = controllerObject.AddComponent<TopologyVisualBridgeVisibilityController>();
                PlayerViewPrefabTestUtility.SetSerializedField(controller, "sceneHost", host);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    controller,
                    "bindings",
                    new[]
                    {
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = outgoingBridge,
                            FirstFace = FaceId.Floor,
                            SecondFace = FaceId.Front,
                        },
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = incomingBridge,
                            FirstFace = FaceId.Front,
                            SecondFace = FaceId.Ceiling,
                        },
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = hiddenBridge,
                            FirstFace = FaceId.Floor,
                            SecondFace = FaceId.Ceiling,
                        },
                    });

                InvokePrivate(controller, "RefreshAuthoringState", false);
                InvokePrivate(controller, "LateUpdate");

                Assert.That(outgoingBridge.activeSelf, Is.True);
                Assert.That(incomingBridge.activeSelf, Is.False);
                Assert.That(hiddenBridge.activeSelf, Is.False);
                Assert.That(controller.DebugSetActiveApplyCount, Is.EqualTo(1));

                InvokePrivate(controller, "LateUpdate");
                Assert.That(controller.DebugSetActiveApplyCount, Is.EqualTo(1));

                host.InputHost.SetRawMoveInput(Vector2.up);
                host.InputHost.RunSingleTick();
                Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);

                InvokePrivate(controller, "LateUpdate");

                Assert.That(outgoingBridge.activeSelf, Is.False);
                Assert.That(incomingBridge.activeSelf, Is.True);
                Assert.That(hiddenBridge.activeSelf, Is.False);
                Assert.That(controller.DebugSetActiveApplyCount, Is.EqualTo(3));

                InvokePrivate(controller, "LateUpdate");
                Assert.That(controller.DebugSetActiveApplyCount, Is.EqualTo(3));

                host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);
                Assert.That(host.Presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);

                InvokePrivate(controller, "LateUpdate");

                Assert.That(outgoingBridge.activeSelf, Is.False);
                Assert.That(incomingBridge.activeSelf, Is.True);
                Assert.That(hiddenBridge.activeSelf, Is.False);
                Assert.That(controller.DebugSetActiveApplyCount, Is.EqualTo(3));

                InvokePrivate(controller, "LateUpdate");
                Assert.That(controller.DebugSetActiveApplyCount, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(outgoingBridge);
                UnityEngine.Object.DestroyImmediate(incomingBridge);
                UnityEngine.Object.DestroyImmediate(hiddenBridge);
                UnityEngine.Object.DestroyImmediate(hostObject);
                if (playerViewPrefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyVisualBridgeVisibilityController_NullTargetBinding_WarnsAndSkips()
        {
            var hostObject = new GameObject("TopologyVisualBridgeVisibilityController_NullTarget_Host");
            var controllerObject = new GameObject("TopologyVisualBridgeVisibilityController_NullTarget_Controller");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var controller = controllerObject.AddComponent<TopologyVisualBridgeVisibilityController>();

                PlayerViewPrefabTestUtility.SetSerializedField(controller, "sceneHost", host);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    controller,
                    "bindings",
                    new[]
                    {
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = null,
                            FirstFace = FaceId.Floor,
                            SecondFace = FaceId.Front,
                        },
                    });

                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("has no target object and will be skipped")));
                InvokePrivate(controller, "RefreshAuthoringState", true);

                Assert.That(controller.DebugValidatedBindingCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyVisualBridgeVisibilityController_SameFaceBinding_WarnsAndSkips()
        {
            var hostObject = new GameObject("TopologyVisualBridgeVisibilityController_SameFace_Host");
            var controllerObject = new GameObject("TopologyVisualBridgeVisibilityController_SameFace_Controller");
            var bridge = new GameObject("SameFaceBridge");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var controller = controllerObject.AddComponent<TopologyVisualBridgeVisibilityController>();

                PlayerViewPrefabTestUtility.SetSerializedField(controller, "sceneHost", host);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    controller,
                    "bindings",
                    new[]
                    {
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = bridge,
                            FirstFace = FaceId.Floor,
                            SecondFace = FaceId.Floor,
                        },
                    });

                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("uses the same logical face")));
                InvokePrivate(controller, "RefreshAuthoringState", true);

                Assert.That(controller.DebugValidatedBindingCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridge);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyVisualBridgeVisibilityController_DuplicateTargetBinding_WarnsAndUsesFirstWin()
        {
            var hostObject = new GameObject("TopologyVisualBridgeVisibilityController_Duplicate_Host");
            var controllerObject = new GameObject("TopologyVisualBridgeVisibilityController_Duplicate_Controller");
            var bridge = new GameObject("DuplicateBridge");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var controller = controllerObject.AddComponent<TopologyVisualBridgeVisibilityController>();

                PlayerViewPrefabTestUtility.SetSerializedField(controller, "sceneHost", host);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    controller,
                    "bindings",
                    new[]
                    {
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = bridge,
                            FirstFace = FaceId.Floor,
                            SecondFace = FaceId.Front,
                        },
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = bridge,
                            FirstFace = FaceId.Floor,
                            SecondFace = FaceId.Ceiling,
                        },
                    });

                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("registered more than once")));
                InvokePrivate(controller, "RefreshAuthoringState", true);

                Assert.That(controller.DebugValidatedBindingCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridge);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyVisualBridgeVisibilityController_HierarchyOverlap_WarnsButKeepsBindings()
        {
            var hostObject = new GameObject("TopologyVisualBridgeVisibilityController_Overlap_Host");
            var controllerObject = new GameObject("TopologyVisualBridgeVisibilityController_Overlap_Controller");
            var parentBridge = new GameObject("ParentBridge");
            var childBridge = new GameObject("ChildBridge");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                childBridge.transform.SetParent(parentBridge.transform, worldPositionStays: false);

                var controller = controllerObject.AddComponent<TopologyVisualBridgeVisibilityController>();
                PlayerViewPrefabTestUtility.SetSerializedField(controller, "sceneHost", host);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    controller,
                    "bindings",
                    new[]
                    {
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = parentBridge,
                            FirstFace = FaceId.Floor,
                            SecondFace = FaceId.Front,
                        },
                        new TopologyVisualBridgeBinding
                        {
                            TargetObject = childBridge,
                            FirstFace = FaceId.Front,
                            SecondFace = FaceId.Ceiling,
                        },
                    });

                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("overlaps hierarchy")));
                InvokePrivate(controller, "RefreshAuthoringState", true);

                Assert.That(controller.DebugValidatedBindingCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentBridge);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyVisualBridgeVisibilityController_MissingSceneHost_WarnsAndDisables()
        {
            var controllerObject = new GameObject("TopologyVisualBridgeVisibilityController_MissingSceneHost");

            try
            {
                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("could not find a GameplaySceneHost")));
                var controller = controllerObject.AddComponent<TopologyVisualBridgeVisibilityController>();
                InvokePrivate(controller, "LateUpdate");

                Assert.That(controller.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TopologyVisualBridgeVisibilityController_MultipleSceneHosts_WarnsAndDisables()
        {
            var firstHostObject = new GameObject("TopologyVisualBridgeVisibilityController_MultipleSceneHosts_A");
            var secondHostObject = new GameObject("TopologyVisualBridgeVisibilityController_MultipleSceneHosts_B");
            var controllerObject = new GameObject("TopologyVisualBridgeVisibilityController_MultipleSceneHosts_Controller");

            try
            {
                firstHostObject.AddComponent<GameplaySceneHost>();
                secondHostObject.AddComponent<GameplaySceneHost>();

                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape("Expected exactly one")));
                var controller = controllerObject.AddComponent<TopologyVisualBridgeVisibilityController>();
                InvokePrivate(controller, "LateUpdate");

                Assert.That(controller.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(secondHostObject);
                UnityEngine.Object.DestroyImmediate(firstHostObject);
            }
        }

        private static GameplaySceneHostConfiguration CreateHostConfiguration(GameplayEntityView playerViewPrefab)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                CellSize = 1f,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = new[]
                {
                    CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                },
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                PlayerEntityId = 10,
                PlayerViewPrefab = playerViewPrefab,
                StaticEntityLogics = Array.Empty<IEntityLogic>(),
            };
        }

        private static EntityState CreateSurfaceUnit(int entityId, SurfaceCell position)
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
                facing = Direction.Up,
            };
        }

        private static void SetPlayerForwardTopologyOvershootPose(GameplaySceneHost host, int entityId)
        {
            var speed = PlayerContinuousLocomotionSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .SpeedUnitsPerTick;
            host.WorldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                entityId,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(
                        SimulationFixed.Zero,
                        SimulationFixed.FromRaw(SimulationFixed.MaxPositiveLocalOffset)),
                    velocity = SimulationVelocity2.Zero,
                    facing = Direction.Up,
                    lastMoveDirection = Direction.Up,
                    speedUnitsPerTick = speed,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
