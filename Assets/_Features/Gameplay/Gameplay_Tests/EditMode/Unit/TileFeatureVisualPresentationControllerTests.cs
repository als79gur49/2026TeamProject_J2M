using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualPresentationControllerTests
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicPropertyId = Shader.PropertyToID("_Metallic");

        [Test]
        [Category("Extended")]
        public void ButtonActivatedRequest_WithRegisteredTargetView_CallsPlayButtonActivatedOnce()
        {
            var rootObject = new GameObject(nameof(ButtonActivatedRequest_WithRegisteredTargetView_CallsPlayButtonActivatedOnce));
            var targetObject = new GameObject("ButtonVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayButtonActivatedRequests(new[] { CreateRequest(100, cell) });

                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));
                Assert.That(registry.TryGetTileVisual(100, out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(target));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ButtonActivatedRequest_WithMotionContactTiming_WaitsUntilDelayElapses()
        {
            var rootObject = new GameObject(nameof(ButtonActivatedRequest_WithMotionContactTiming_WaitsUntilDelayElapses));
            var targetObject = new GameObject("ButtonVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);
                var barrierKey = PresentationBarrierKey.ButtonActivated(100);
                var request = new TilePresentationRequest(
                    TilePresentationRequestKind.ButtonActivated,
                    100,
                    cell,
                    TileFeatureKind.Button,
                    sourceEntityId: 20,
                    ownerEntityId: 0,
                    teamId: 1,
                    timingAnchor: PresentationTimingAnchor.MotionContact(
                        sourceEntityId: 20,
                        targetEntityId: 0,
                        actionPlanId: 45,
                        localActionIndex: 0,
                        movementSemanticKind: MovementSemanticKind.Flip,
                        visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime,
                        barrierKey: barrierKey),
                    barrierKey: barrierKey);
                var delaySeconds = GameplayTimingProfile.CreateDefault().FlipMotionDurationSeconds *
                                   GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;

                controller.PlayRequests(new[] { request });
                controller.Update(delaySeconds - 0.001f);
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);

                controller.Update(0.001f);
                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DestroyTileTriggeredRequest_WithSupportedTargetView_CallsPlayDestroyTileTriggeredOnce()
        {
            var rootObject = new GameObject(nameof(DestroyTileTriggeredRequest_WithSupportedTargetView_CallsPlayDestroyTileTriggeredOnce));
            var targetObject = new GameObject("DestroyTileVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[] { CreateDestroyRequest(100, cell) });

                Assert.That(target.DebugPlayDestroyTileTriggeredCount, Is.EqualTo(1));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DestroyTileActiveStateRequests_WithSupportedTargetView_CallActiveStateHooks()
        {
            var rootObject = new GameObject(nameof(DestroyTileActiveStateRequests_WithSupportedTargetView_CallActiveStateHooks));
            var targetObject = new GameObject("DestroyTileVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var rendererObject = new GameObject("DestroyTileRenderer");
            rendererObject.transform.SetParent(targetObject.transform, worldPositionStays: false);
            var targetRenderer = rendererObject.AddComponent<MeshRenderer>();
            var sharedMaterial = CreateSharedColorMaterial(Color.black);
            targetRenderer.sharedMaterials = new[] { sharedMaterial };

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                ConfigureInactiveVisualOverride(
                    target,
                    TileFeatureKind.Destroy,
                    targetRenderer,
                    inactiveColor: Color.white,
                    inactiveMetallic: 1f);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[]
                {
                    CreateDestroyActivatedRequest(100, cell),
                });

                Assert.That(target.DebugPlayDestroyTileActivatedCount, Is.EqualTo(1));
                Assert.That(target.DebugDestroyTileActive, Is.True);
                AssertDestroyTileMaterialCleared(targetRenderer);

                controller.PlayRequests(new[]
                {
                    CreateDestroyDeactivatedRequest(100, cell),
                });

                Assert.That(target.DebugPlayDestroyTileDeactivatedCount, Is.EqualTo(1));
                Assert.That(target.DebugDestroyTileActive, Is.False);
                AssertDestroyTileInactiveMaterial(targetRenderer);
                Assert.That(target.DebugPlayDestroyTileTriggeredCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(sharedMaterial);
            }
        }

        [Test]
        [Category("Extended")]
        public void SlideTileRedirectedRequest_WithSupportedTargetView_CallsPlaySlideTileRedirectedOnce()
        {
            var rootObject = new GameObject(nameof(SlideTileRedirectedRequest_WithSupportedTargetView_CallsPlaySlideTileRedirectedOnce));
            var targetObject = new GameObject("SlideTileVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[] { CreateSlideRequest(100, cell, Direction.Up, targetEntityId: 20) });

                Assert.That(target.DebugPlaySlideTileRedirectedCount, Is.EqualTo(1));
                Assert.That(target.DebugLastSlideTileDirection, Is.EqualTo(Direction.Up));
                Assert.That(target.DebugLastSlideTileTargetEntityId, Is.EqualTo(20));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
                Assert.That(target.DebugPlayDestroyTileTriggeredCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void BarricadeBlockedRequest_WithSupportedTargetView_CallsPlayBarricadeBlockedOnce()
        {
            var rootObject = new GameObject(nameof(BarricadeBlockedRequest_WithSupportedTargetView_CallsPlayBarricadeBlockedOnce));
            var targetObject = new GameObject("BarricadeVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[] { CreateBarricadeBlockedRequest(100, cell, Direction.Right, targetEntityId: 20) });

                Assert.That(target.DebugBarricadeBlockedCount, Is.EqualTo(1));
                Assert.That(target.DebugLastBarricadeBlockedDirection, Is.EqualTo(Direction.Right));
                Assert.That(target.DebugLastBarricadeBlockedTargetEntityId, Is.EqualTo(20));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void BarricadeCrushedRequest_WithSupportedTargetView_CallsPlayBarricadeCrushedOnce()
        {
            var rootObject = new GameObject(nameof(BarricadeCrushedRequest_WithSupportedTargetView_CallsPlayBarricadeCrushedOnce));
            var targetObject = new GameObject("BarricadeVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[] { CreateBarricadeCrushedRequest(100, cell, targetEntityId: 20) });

                Assert.That(target.DebugBarricadeCrushedCount, Is.EqualTo(1));
                Assert.That(target.DebugLastBarricadeCrushedTargetEntityId, Is.EqualTo(20));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void BarricadeActiveStateRequests_WithSupportedTargetView_CallActiveStateHooks()
        {
            var rootObject = new GameObject(nameof(BarricadeActiveStateRequests_WithSupportedTargetView_CallActiveStateHooks));
            var targetObject = new GameObject("BarricadeVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[]
                {
                    CreateBarricadeActivatedRequest(100, cell),
                    CreateBarricadeDeactivatedRequest(100, cell),
                });

                Assert.That(target.DebugBarricadeActivatedCount, Is.EqualTo(1));
                Assert.That(target.DebugBarricadeDeactivatedCount, Is.EqualTo(1));
                Assert.That(target.DebugBarricadeBlockedCount, Is.Zero);
                Assert.That(target.DebugBarricadeCrushedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void BarricadeActiveImmediateState_WithSameState_DoesNotReplayIdleState()
        {
            var targetObject = new GameObject(nameof(BarricadeActiveImmediateState_WithSameState_DoesNotReplayIdleState));
            var controller = CreateBarricadeAnimatorController(nameof(BarricadeActiveImmediateState_WithSameState_DoesNotReplayIdleState));

            try
            {
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Front, 1, 1));
                AttachAnimator(targetObject, controller);
                var adapter = EnsureLegacyAdapter(target);

                adapter.SetBarricadeActiveImmediate(true);
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(1));
                Assert.That(adapter.DebugLastBarricadeActiveAnimatorStateHash, Is.EqualTo(Animator.StringToHash("RaisedIdle")));

                adapter.SetBarricadeActiveImmediate(true);
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(1));

                adapter.SetBarricadeActiveImmediate(false);
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(2));
                Assert.That(adapter.DebugLastBarricadeActiveAnimatorStateHash, Is.EqualTo(Animator.StringToHash("LoweredIdle")));

                adapter.SetBarricadeActiveImmediate(false);
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(2));

                adapter.SetBarricadeActiveImmediate(true);
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(3));
                Assert.That(adapter.DebugLastBarricadeActiveAnimatorStateHash, Is.EqualTo(Animator.StringToHash("RaisedIdle")));

                Assert.That(target.DebugBarricadeActiveImmediateStatePlayCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(controller);
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void BarricadeBlockedRequest_WithActiveStateRefresh_DoesNotReplayRaisedIdle()
        {
            var rootObject = new GameObject(nameof(BarricadeBlockedRequest_WithActiveStateRefresh_DoesNotReplayRaisedIdle));
            var targetObject = new GameObject("BarricadeVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var animatorController = CreateBarricadeAnimatorController(nameof(BarricadeBlockedRequest_WithActiveStateRefresh_DoesNotReplayRaisedIdle));

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                var animator = AttachAnimator(targetObject, animatorController);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);
                var activeState = new TileFeatureVisualState(
                    100,
                    cell,
                    TileFeatureKind.Barricade,
                    isActive: true,
                    sourceEntityId: 0,
                    ownerEntityId: 0,
                    teamId: 0);
                var adapter = EnsureLegacyAdapter(target);

                adapter.SetBarricadeActiveImmediate(true);
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(1));
                controller.PlayRequests(new[] { CreateBarricadeBlockedRequest(100, cell, Direction.Right, targetEntityId: 20) });
                controller.RefreshContinuousStates(new[] { activeState });

                Assert.That(target.DebugBarricadeBlockedCount, Is.EqualTo(1));
                Assert.That(target.DebugBarricadeActiveImmediateStatePlayCount, Is.EqualTo(1));
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(1));
                Assert.That(animator.GetBool("BarricadeActive"), Is.True);
                Assert.That(adapter.DebugLastBarricadeActiveAnimatorStateHash, Is.EqualTo(Animator.StringToHash("RaisedIdle")));
            }
            finally
            {
                Object.DestroyImmediate(animatorController);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void BarricadeCrushedRequest_WithActiveStateRefresh_DoesNotReplayIdle()
        {
            var rootObject = new GameObject(nameof(BarricadeCrushedRequest_WithActiveStateRefresh_DoesNotReplayIdle));
            var targetObject = new GameObject("BarricadeVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var animatorController = CreateBarricadeAnimatorController(nameof(BarricadeCrushedRequest_WithActiveStateRefresh_DoesNotReplayIdle));

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                var animator = AttachAnimator(targetObject, animatorController);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);
                var activeState = new TileFeatureVisualState(
                    100,
                    cell,
                    TileFeatureKind.Barricade,
                    isActive: false,
                    sourceEntityId: 0,
                    ownerEntityId: 0,
                    teamId: 0);
                var adapter = EnsureLegacyAdapter(target);

                adapter.SetBarricadeActiveImmediate(false);
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(1));
                controller.PlayRequests(new[] { CreateBarricadeCrushedRequest(100, cell, targetEntityId: 20) });
                controller.RefreshContinuousStates(new[] { activeState });

                Assert.That(target.DebugBarricadeCrushedCount, Is.EqualTo(1));
                Assert.That(target.DebugBarricadeActiveImmediateStatePlayCount, Is.EqualTo(1));
                Assert.That(adapter.DebugBarricadeActiveAnimatorStatePlayCount, Is.EqualTo(1));
                Assert.That(animator.GetBool("BarricadeActive"), Is.False);
                Assert.That(adapter.DebugLastBarricadeActiveAnimatorStateHash, Is.EqualTo(Animator.StringToHash("LoweredIdle")));
            }
            finally
            {
                Object.DestroyImmediate(animatorController);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void BarricadeActivatedRequest_WithActiveStateRefresh_DoesNotReplayRaisedIdle()
        {
            var rootObject = new GameObject(nameof(BarricadeActivatedRequest_WithActiveStateRefresh_DoesNotReplayRaisedIdle));
            var targetObject = new GameObject("BarricadeVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);
                var activeState = new TileFeatureVisualState(
                    100,
                    cell,
                    TileFeatureKind.Barricade,
                    isActive: true,
                    sourceEntityId: 0,
                    ownerEntityId: 0,
                    teamId: 0);

                controller.PlayRequests(new[] { CreateBarricadeActivatedRequest(100, cell) });
                controller.RefreshContinuousStates(new[] { activeState });

                Assert.That(target.DebugBarricadeActivatedCount, Is.EqualTo(1));
                Assert.That(target.DebugBarricadeActiveImmediateStatePlayCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitRequests_WithSupportedTargetView_CallExitVisualHooks()
        {
            var rootObject = new GameObject(nameof(ExitRequests_WithSupportedTargetView_CallExitVisualHooks));
            var targetObject = new GameObject("ExitVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[]
                {
                    CreateExitOpenedRequest(100, cell),
                    CreateExitEnteredRequest(100, cell, playerEntityId: 10),
                });

                Assert.That(target.DebugExitOpenedCount, Is.EqualTo(1));
                Assert.That(target.DebugExitEnteredCount, Is.EqualTo(1));
                Assert.That(target.DebugLastExitEnteredPlayerEntityId, Is.EqualTo(10));
                Assert.That(target.DebugExitOpen, Is.True);
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitVisualState_WithSupportedTargetView_SyncsOpenStateImmediate()
        {
            var rootObject = new GameObject(nameof(ExitVisualState_WithSupportedTargetView_SyncsOpenStateImmediate));
            var targetObject = new GameObject("ExitVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Exit,
                        isActive: false,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0),
                });
                Assert.That(target.DebugExitOpen, Is.False);

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Exit,
                        isActive: true,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0),
                });

                Assert.That(target.DebugExitOpen, Is.True);
                Assert.That(target.DebugExitOpenedCount, Is.Zero);
                Assert.That(target.DebugExitEnteredCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitOpenedRequest_DefersOpenStateImmediateSyncUntilExitDeactivates()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingExitOpenStateTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);
            var openState = new TileFeatureVisualState(
                100,
                cell,
                TileFeatureKind.Exit,
                isActive: true,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0);
            var closedState = new TileFeatureVisualState(
                100,
                cell,
                TileFeatureKind.Exit,
                isActive: false,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0);

            controller.PlayRequests(new[] { CreateExitOpenedRequest(100, cell) });
            controller.RefreshContinuousStates(new[] { openState });

            Assert.That(target.OpenedPlayCount, Is.EqualTo(1));
            Assert.That(target.ImmediateSyncCount, Is.Zero);

            controller.RefreshContinuousStates(new[] { openState });

            Assert.That(target.ImmediateSyncCount, Is.Zero);

            controller.RefreshContinuousStates(new[] { closedState });

            Assert.That(target.ImmediateSyncCount, Is.EqualTo(1));
            Assert.That(target.LastImmediateOpen, Is.False);

            controller.RefreshContinuousStates(new[] { openState });

            Assert.That(target.ImmediateSyncCount, Is.EqualTo(2));
            Assert.That(target.LastImmediateOpen, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void ExitVisualState_WithVisibilityGate_DoesNotSyncOpenImmediate()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingExitOpenStateTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);
            var barrierKey = PresentationBarrierKey.ButtonActivated(10);
            var timingAnchor = PresentationTimingAnchor.MotionContact(
                sourceEntityId: 20,
                targetEntityId: 100,
                actionPlanId: 77,
                localActionIndex: 0,
                movementSemanticKind: MovementSemanticKind.Flip,
                visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime,
                barrierKey: barrierKey);

            controller.RefreshContinuousStates(new[]
            {
                new TileFeatureVisualState(
                    100,
                    cell,
                    TileFeatureKind.Exit,
                    isActive: true,
                    sourceEntityId: 0,
                    ownerEntityId: 0,
                    teamId: 0,
                    visibilityGate: new PresentationVisibilityGate(timingAnchor, barrierKey)),
            });

            Assert.That(target.ImmediateSyncCount, Is.Zero);
            Assert.That(target.OpenedPlayCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void DelayedExitOpenedRequest_BlocksImmediateOpenSyncUntilRequestPlays()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingExitOpenStateTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);
            var barrierKey = PresentationBarrierKey.ButtonActivated(10);
            var timingAnchor = PresentationTimingAnchor.MotionContact(
                sourceEntityId: 20,
                targetEntityId: 100,
                actionPlanId: 77,
                localActionIndex: 0,
                movementSemanticKind: MovementSemanticKind.Flip,
                visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime,
                barrierKey: barrierKey);
            var gatedOpenState = new TileFeatureVisualState(
                100,
                cell,
                TileFeatureKind.Exit,
                isActive: true,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                visibilityGate: new PresentationVisibilityGate(timingAnchor, barrierKey));
            var ungatedOpenState = new TileFeatureVisualState(
                100,
                cell,
                TileFeatureKind.Exit,
                isActive: true,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0);
            var request = new TilePresentationRequest(
                TilePresentationRequestKind.ExitOpened,
                100,
                cell,
                TileFeatureKind.Exit,
                sourceEntityId: 20,
                ownerEntityId: 0,
                teamId: 1,
                timingAnchor: timingAnchor,
                barrierKey: barrierKey);
            var delaySeconds = GameplayTimingProfile.CreateDefault().FlipMotionDurationSeconds *
                               GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;

            controller.PlayRequests(new[] { request });
            controller.RefreshContinuousStates(new[] { gatedOpenState });
            controller.Update(delaySeconds - 0.001f);

            Assert.That(target.ImmediateSyncCount, Is.Zero);
            Assert.That(target.OpenedPlayCount, Is.Zero);

            controller.Update(0.001f);
            controller.RefreshContinuousStates(new[] { ungatedOpenState });

            Assert.That(target.OpenedPlayCount, Is.EqualTo(1));
            Assert.That(target.ImmediateSyncCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void DestroyTileVisualState_WithSupportedTargetView_SyncsActiveStateImmediate()
        {
            var rootObject = new GameObject(nameof(DestroyTileVisualState_WithSupportedTargetView_SyncsActiveStateImmediate));
            var targetObject = new GameObject("DestroyTileVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var rendererObject = new GameObject("DestroyTileRenderer");
            rendererObject.transform.SetParent(targetObject.transform, worldPositionStays: false);
            var targetRenderer = rendererObject.AddComponent<MeshRenderer>();
            var sharedMaterial = CreateSharedColorMaterial(Color.black);
            targetRenderer.sharedMaterials = new[] { sharedMaterial };

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                ConfigureInactiveVisualOverride(
                    target,
                    TileFeatureKind.Destroy,
                    targetRenderer,
                    inactiveColor: Color.white,
                    inactiveMetallic: 1f);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Destroy,
                        isActive: true,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0),
                });
                Assert.That(target.DebugDestroyTileActive, Is.True);

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Destroy,
                        isActive: false,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0),
                });

                Assert.That(target.DebugDestroyTileActive, Is.False);
                AssertDestroyTileInactiveMaterial(targetRenderer);
                Assert.That(target.DebugPlayDestroyTileActivatedCount, Is.Zero);
                Assert.That(target.DebugPlayDestroyTileDeactivatedCount, Is.Zero);

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Destroy,
                        isActive: true,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0),
                });

                Assert.That(target.DebugDestroyTileActive, Is.True);
                AssertDestroyTileMaterialCleared(targetRenderer);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(sharedMaterial);
            }
        }

        [Test]
        [Category("Extended")]
        public void SlideTileVisualState_WithSupportedTargetView_AppliesInactiveOverridesPerMaterialIndexAndClearsWhenActive()
        {
            var rootObject = new GameObject(nameof(SlideTileVisualState_WithSupportedTargetView_AppliesInactiveOverridesPerMaterialIndexAndClearsWhenActive));
            var targetObject = new GameObject("SlideTileVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var rendererObject = new GameObject("SlideTileRenderer");
            rendererObject.transform.SetParent(targetObject.transform, worldPositionStays: false);
            var targetRenderer = rendererObject.AddComponent<MeshRenderer>();
            var firstSharedMaterial = CreateSharedColorMaterial(Color.black);
            var secondSharedMaterial = CreateSharedColorMaterial(Color.white);
            targetRenderer.sharedMaterials = new[] { firstSharedMaterial, secondSharedMaterial };

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                ConfigureInactiveVisualTargets(
                    target,
                    TileFeatureKind.Slide,
                    (targetRenderer, 0, Color.gray, 1f),
                    (targetRenderer, 1, Color.cyan, 0.5f));
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Slide,
                        isActive: false,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0),
                });

                Assert.That(target.DebugSlideTileActive, Is.False);
                AssertTileFeatureMaterialState(targetRenderer, Color.gray, 1f, materialIndex: 0);
                AssertTileFeatureMaterialState(targetRenderer, Color.cyan, 0.5f, materialIndex: 1);
                Assert.That(firstSharedMaterial.color, Is.EqualTo(Color.black));
                Assert.That(secondSharedMaterial.color, Is.EqualTo(Color.white));

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Slide,
                        isActive: true,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0),
                });

                Assert.That(target.DebugSlideTileActive, Is.True);
                AssertTileFeatureMaterialCleared(targetRenderer, Color.gray, 1f, materialIndex: 0);
                AssertTileFeatureMaterialCleared(targetRenderer, Color.cyan, 0.5f, materialIndex: 1);
                Assert.That(firstSharedMaterial.color, Is.EqualTo(Color.black));
                Assert.That(secondSharedMaterial.color, Is.EqualTo(Color.white));
                Assert.That(target.DebugDestroyTileActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(firstSharedMaterial);
                Object.DestroyImmediate(secondSharedMaterial);
            }
        }

        [Test]
        [Category("Extended")]
        public void SlideTileVisualState_WithEmptyTargetList_DoesNotApplyInactiveOverride()
        {
            var rootObject = new GameObject(nameof(SlideTileVisualState_WithEmptyTargetList_DoesNotApplyInactiveOverride));
            var targetObject = new GameObject("SlideTileVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var rendererObject = new GameObject("SlideTileRenderer");
            rendererObject.transform.SetParent(targetObject.transform, worldPositionStays: false);
            var targetRenderer = rendererObject.AddComponent<MeshRenderer>();

            try
            {
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Slide,
                        isActive: false,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0),
                });

                Assert.That(target.DebugSlideTileActive, Is.False);
                AssertTileFeatureMaterialCleared(targetRenderer, Color.white, 1f);
                Assert.That(target.DebugDestroyTileActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureActiveVisualStates_DispatchDestroyAndSlideThroughCommonActiveStatePath()
        {
            var destroyCell = new SurfaceCell(FaceId.Front, 1, 1);
            var slideCell = new SurfaceCell(FaceId.Front, 2, 1);
            var destroyTarget = new RecordingActiveStateTarget(100, destroyCell);
            var slideTarget = new RecordingActiveStateTarget(101, slideCell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(destroyTarget, slideTarget));

            controller.RefreshContinuousStates(new[]
            {
                new TileFeatureVisualState(
                    100,
                    destroyCell,
                    TileFeatureKind.Destroy,
                    isActive: true,
                    sourceEntityId: 0,
                    ownerEntityId: 0,
                    teamId: 0),
                new TileFeatureVisualState(
                    101,
                    slideCell,
                    TileFeatureKind.Slide,
                    isActive: false,
                    sourceEntityId: 0,
                    ownerEntityId: 0,
                    teamId: 0),
            });

            Assert.That(destroyTarget.ActiveStateCalls, Is.EqualTo(1));
            Assert.That(destroyTarget.LastActiveStateKind, Is.EqualTo(TileFeatureKind.Destroy));
            Assert.That(destroyTarget.LastActiveState, Is.True);
            Assert.That(slideTarget.ActiveStateCalls, Is.EqualTo(1));
            Assert.That(slideTarget.LastActiveStateKind, Is.EqualTo(TileFeatureKind.Slide));
            Assert.That(slideTarget.LastActiveState, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SlideTileVisualState_WithUnsupportedTarget_NoOpsWithOptionalDiagnostic()
        {
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var target = new RecordingTarget(100, cell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(target));
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.RefreshContinuousStates(new[]
            {
                new TileFeatureVisualState(
                    100,
                    cell,
                    TileFeatureKind.Slide,
                    isActive: true,
                    sourceEntityId: 0,
                    ownerEntityId: 0,
                    teamId: 0),
            });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0], Does.Contain("unsupported Slide visual state"));
        }

        [Test]
        [Category("Extended")]
        public void ButtonVisualState_WithVisibilityGate_DoesNotPlayPressedVisualFromContinuousSync()
        {
            var rootObject = new GameObject(nameof(ButtonVisualState_WithVisibilityGate_DoesNotPlayPressedVisualFromContinuousSync));
            var targetObject = new GameObject("ButtonVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);
                var barrierKey = PresentationBarrierKey.ButtonActivated(100);
                var timingAnchor = PresentationTimingAnchor.MotionContact(
                    sourceEntityId: 20,
                    targetEntityId: 0,
                    actionPlanId: 77,
                    localActionIndex: 0,
                    movementSemanticKind: MovementSemanticKind.Flip,
                    visualContactNormalizedTime: GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime,
                    barrierKey: barrierKey);

                controller.RefreshContinuousStates(new[]
                {
                    new TileFeatureVisualState(
                        100,
                        cell,
                        TileFeatureKind.Button,
                        isActive: true,
                        sourceEntityId: 20,
                        ownerEntityId: 0,
                        teamId: 1,
                        visibilityGate: new PresentationVisibilityGate(timingAnchor, barrierKey)),
                });

                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void MoonBlockGeneratedRequest_WithSupportedTargetView_CallsPlayMoonBlockGeneratedOnce()
        {
            var rootObject = new GameObject(nameof(MoonBlockGeneratedRequest_WithSupportedTargetView_CallsPlayMoonBlockGeneratedOnce));
            var targetObject = new GameObject("MoonBlockGeneratorVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[] { CreateMoonBlockGeneratedRequest(100, cell, moonBlockEntityId: 20) });

                Assert.That(target.DebugMoonBlockGeneratedCount, Is.EqualTo(1));
                Assert.That(target.DebugLastMoonBlockGeneratedEntityId, Is.EqualTo(20));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void MoonBlockGeneratorBlockedRequest_WithSupportedTargetView_CallsPlayMoonBlockGeneratorBlockedOnce()
        {
            var rootObject = new GameObject(nameof(MoonBlockGeneratorBlockedRequest_WithSupportedTargetView_CallsPlayMoonBlockGeneratorBlockedOnce));
            var targetObject = new GameObject("MoonBlockGeneratorVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[]
                {
                    CreateMoonBlockGeneratorBlockedRequest(
                        100,
                        cell,
                        blockerEntityId: 20,
                        MoonBlockGeneratorBlockedReason.UnitOccupant),
                });

                Assert.That(target.DebugMoonBlockGeneratorBlockedCount, Is.EqualTo(1));
                Assert.That(target.DebugLastMoonBlockGeneratorBlockedEntityId, Is.EqualTo(20));
                Assert.That(target.DebugLastMoonBlockGeneratorBlockedReason, Is.EqualTo(MoonBlockGeneratorBlockedReason.UnitOccupant));
                Assert.That(target.DebugLastMoonBlockGeneratorBlockedPayload.BlockedCell, Is.EqualTo(cell));
                Assert.That(target.DebugMoonBlockGeneratorBlockedUnitCount, Is.EqualTo(1));
                Assert.That(target.DebugMoonBlockGeneratedCount, Is.Zero);
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void MoonBlockGeneratorBlockedRequest_ReasonSpecificDebugCounts_KeepGenericFallback()
        {
            var rootObject = new GameObject(nameof(MoonBlockGeneratorBlockedRequest_ReasonSpecificDebugCounts_KeepGenericFallback));
            var targetObject = new GameObject("MoonBlockGeneratorVisualTarget");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayRequests(new[]
                {
                    CreateMoonBlockGeneratorBlockedRequest(100, cell, 20, MoonBlockGeneratorBlockedReason.UnitOccupant),
                    CreateMoonBlockGeneratorBlockedRequest(100, cell, 21, MoonBlockGeneratorBlockedReason.WallLikeSolid),
                    CreateMoonBlockGeneratorBlockedRequest(100, cell, 0, MoonBlockGeneratorBlockedReason.PlacementBlocked),
                });

                Assert.That(target.DebugMoonBlockGeneratorBlockedCount, Is.EqualTo(3));
                Assert.That(target.DebugMoonBlockGeneratorBlockedUnitCount, Is.EqualTo(1));
                Assert.That(target.DebugMoonBlockGeneratorBlockedWallLikeSolidCount, Is.EqualTo(1));
                Assert.That(target.DebugMoonBlockGeneratorBlockedPlacementCount, Is.EqualTo(1));
                Assert.That(target.DebugLastMoonBlockGeneratorBlockedReason, Is.EqualTo(MoonBlockGeneratorBlockedReason.PlacementBlocked));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ButtonActivatedRequest_MissingTarget_NoOpsWithOptionalDiagnostic()
        {
            var diagnostics = new List<string>();
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry());
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.PlayButtonActivatedRequests(new[] { CreateRequest(404, new SurfaceCell(FaceId.Floor, 1, 1)) });

            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0], Does.Contain("404"));
        }

        [Test]
        [Category("Extended")]
        public void DestroyTileTriggeredRequest_UnsupportedTarget_NoOpsWithOptionalDiagnostic()
        {
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingTarget(100, cell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(target));
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.PlayRequests(new[] { CreateDestroyRequest(100, cell) });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0], Does.Contain("unsupported DestroyTileTriggered"));
        }

        [Test]
        [Category("Extended")]
        public void DestroyTileActiveStateRequests_UnsupportedTarget_NoOpsWithOptionalDiagnostics()
        {
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var target = new RecordingTarget(100, cell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(target));
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.PlayRequests(new[]
            {
                CreateDestroyActivatedRequest(100, cell),
                CreateDestroyDeactivatedRequest(100, cell),
            });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(2));
            Assert.That(diagnostics[0], Does.Contain("unsupported DestroyTileActivated"));
            Assert.That(diagnostics[1], Does.Contain("unsupported DestroyTileDeactivated"));
        }

        [Test]
        [Category("Extended")]
        public void SlideTileRedirectedRequest_UnsupportedTarget_NoOpsWithOptionalDiagnostic()
        {
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var target = new RecordingTarget(100, cell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(target));
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.PlayRequests(new[] { CreateSlideRequest(100, cell, Direction.Up, targetEntityId: 20) });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0], Does.Contain("unsupported SlideTileRedirected"));
        }

        [Test]
        [Category("Extended")]
        public void BarricadeRequests_UnsupportedTargets_NoOpWithOptionalDiagnostics()
        {
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var target = new RecordingTarget(100, cell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(target));
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.PlayRequests(new[]
            {
                CreateBarricadeActivatedRequest(100, cell),
                CreateBarricadeBlockedRequest(100, cell, Direction.Right, targetEntityId: 20),
                CreateBarricadeCrushedRequest(100, cell, targetEntityId: 20),
                CreateBarricadeDeactivatedRequest(100, cell),
            });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(4));
            Assert.That(diagnostics[0], Does.Contain("unsupported BarricadeActivated"));
            Assert.That(diagnostics[1], Does.Contain("unsupported BarricadeBlocked"));
            Assert.That(diagnostics[2], Does.Contain("unsupported BarricadeCrushed"));
            Assert.That(diagnostics[3], Does.Contain("unsupported BarricadeDeactivated"));
        }

        [Test]
        [Category("Extended")]
        public void ExitRequests_UnsupportedTargets_NoOpWithOptionalDiagnostics()
        {
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingTarget(100, cell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(target));
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.PlayRequests(new[]
            {
                CreateExitOpenedRequest(100, cell),
                CreateExitEnteredRequest(100, cell, playerEntityId: 10),
            });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(2));
            Assert.That(diagnostics[0], Does.Contain("unsupported ExitOpened"));
            Assert.That(diagnostics[1], Does.Contain("unsupported ExitEntered"));
        }

        [Test]
        [Category("Extended")]
        public void MoonBlockGeneratedRequest_UnsupportedTarget_NoOpWithOptionalDiagnostic()
        {
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingTarget(100, cell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(target));
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.PlayRequests(new[] { CreateMoonBlockGeneratedRequest(100, cell, moonBlockEntityId: 20) });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0], Does.Contain("unsupported MoonBlockGenerated"));
        }

        [Test]
        [Category("Extended")]
        public void MoonBlockGeneratorBlockedRequest_UnsupportedTarget_NoOpWithOptionalDiagnostic()
        {
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingTarget(100, cell);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(new RecordingRegistry(target));
            controller.SetDiagnosticSink(diagnostics.Add);

            controller.PlayRequests(new[] { CreateMoonBlockGeneratorBlockedRequest(100, cell, blockerEntityId: 20) });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics[0], Does.Contain("unsupported MoonBlockGeneratorBlocked"));
        }

        [Test]
        [Category("Extended")]
        public void EmptyRequestList_DoesNothing()
        {
            var registry = new RecordingRegistry();
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayButtonActivatedRequests(System.Array.Empty<TilePresentationRequest>());

            Assert.That(registry.TryGetCallCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void NonButtonRequestKind_IsSkipped()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayButtonActivatedRequests(new[]
            {
                new TilePresentationRequest(
                    (TilePresentationRequestKind)999,
                    100,
                    cell,
                    TileFeatureKind.Button,
                    0,
                    0,
                    0),
            });

            Assert.That(registry.TryGetCallCount, Is.Zero);
            Assert.That(target.PlayCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void MultipleRequests_AreProcessedInOrder()
        {
            var calls = new List<int>();
            var first = new RecordingTarget(30, new SurfaceCell(FaceId.Floor, 0, 0), calls);
            var second = new RecordingTarget(10, new SurfaceCell(FaceId.Floor, 1, 0), calls);
            var third = new RecordingTarget(20, new SurfaceCell(FaceId.Front, 0, 1), calls);
            var registry = new RecordingRegistry(first, second, third);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayButtonActivatedRequests(new[]
            {
                CreateRequest(first.TileId, first.Cell),
                CreateRequest(second.TileId, second.Cell),
                CreateRequest(third.TileId, third.Cell),
            });

            Assert.That(calls.ToArray(), Is.EqualTo(new[] { 30, 10, 20 }));
            Assert.That(registry.LookupOrder.ToArray(), Is.EqualTo(new[] { 30, 10, 20 }));
        }

        [Test]
        [Category("Extended")]
        public void DuplicateRequests_AreNotDeduped()
        {
            var target = new RecordingTarget(100, new SurfaceCell(FaceId.Floor, 1, 1));
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayButtonActivatedRequests(new[]
            {
                CreateRequest(target.TileId, target.Cell),
                CreateRequest(target.TileId, target.Cell),
            });

            Assert.That(target.PlayCount, Is.EqualTo(2));
            Assert.That(registry.LookupOrder.ToArray(), Is.EqualTo(new[] { 100, 100 }));
        }

        [Test]
        [Category("Extended")]
        public void DuplicateDestroyTileRequests_AreNotDeduped()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingDestroyTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayRequests(new[]
            {
                CreateDestroyRequest(target.TileId, target.Cell),
                CreateDestroyRequest(target.TileId, target.Cell),
            });

            Assert.That(target.DestroyPlayCount, Is.EqualTo(2));
            Assert.That(registry.LookupOrder.ToArray(), Is.EqualTo(new[] { 100, 100 }));
        }

        [Test]
        [Category("Extended")]
        public void DuplicateSlideTileRequests_AreNotDeduped()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var target = new RecordingSlideTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayRequests(new[]
            {
                CreateSlideRequest(target.TileId, target.Cell, Direction.Up, targetEntityId: 20),
                CreateSlideRequest(target.TileId, target.Cell, Direction.Left, targetEntityId: 30),
            });

            Assert.That(target.SlidePlayCount, Is.EqualTo(2));
            Assert.That(target.LastDirection, Is.EqualTo(Direction.Left));
            Assert.That(target.LastTargetEntityId, Is.EqualTo(30));
            Assert.That(registry.LookupOrder.ToArray(), Is.EqualTo(new[] { 100, 100 }));
        }

        [Test]
        [Category("Extended")]
        public void DuplicateBarricadeRequests_AreNotDeduped()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var target = new RecordingBarricadeTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayRequests(new[]
            {
                CreateBarricadeBlockedRequest(target.TileId, target.Cell, Direction.Right, targetEntityId: 20),
                CreateBarricadeBlockedRequest(target.TileId, target.Cell, Direction.Left, targetEntityId: 30),
                CreateBarricadeCrushedRequest(target.TileId, target.Cell, targetEntityId: 40),
                CreateBarricadeCrushedRequest(target.TileId, target.Cell, targetEntityId: 50),
            });

            Assert.That(target.BlockedPlayCount, Is.EqualTo(2));
            Assert.That(target.CrushedPlayCount, Is.EqualTo(2));
            Assert.That(target.LastBlockedDirection, Is.EqualTo(Direction.Left));
            Assert.That(target.LastBlockedTargetEntityId, Is.EqualTo(30));
            Assert.That(target.LastCrushedTargetEntityId, Is.EqualTo(50));
            Assert.That(registry.LookupOrder.ToArray(), Is.EqualTo(new[] { 100, 100, 100, 100 }));
        }

        [Test]
        [Category("Extended")]
        public void DuplicateExitRequests_AreNotDeduped()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingExitTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayRequests(new[]
            {
                CreateExitOpenedRequest(target.TileId, target.Cell),
                CreateExitOpenedRequest(target.TileId, target.Cell),
                CreateExitEnteredRequest(target.TileId, target.Cell, playerEntityId: 10),
                CreateExitEnteredRequest(target.TileId, target.Cell, playerEntityId: 11),
            });

            Assert.That(target.OpenedPlayCount, Is.EqualTo(2));
            Assert.That(target.EnteredPlayCount, Is.EqualTo(2));
            Assert.That(target.LastPlayerEntityId, Is.EqualTo(11));
            Assert.That(registry.LookupOrder.ToArray(), Is.EqualTo(new[] { 100, 100, 100, 100 }));
        }

        [Test]
        [Category("Extended")]
        public void DuplicateMoonBlockGeneratedRequests_AreNotDeduped()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingMoonBlockGeneratedTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayRequests(new[]
            {
                CreateMoonBlockGeneratedRequest(target.TileId, target.Cell, moonBlockEntityId: 20),
                CreateMoonBlockGeneratedRequest(target.TileId, target.Cell, moonBlockEntityId: 21),
            });

            Assert.That(target.GeneratedPlayCount, Is.EqualTo(2));
            Assert.That(target.LastMoonBlockEntityId, Is.EqualTo(21));
            Assert.That(registry.LookupOrder.ToArray(), Is.EqualTo(new[] { 100, 100 }));
        }

        [Test]
        [Category("Extended")]
        public void DuplicateMoonBlockGeneratorBlockedRequests_AreNotDeduped()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var target = new RecordingMoonBlockGeneratorBlockedTarget(100, cell);
            var registry = new RecordingRegistry(target);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayRequests(new[]
            {
                CreateMoonBlockGeneratorBlockedRequest(
                    target.TileId,
                    target.Cell,
                    20,
                    MoonBlockGeneratorBlockedReason.UnitOccupant),
                CreateMoonBlockGeneratorBlockedRequest(
                    target.TileId,
                    target.Cell,
                    21,
                    MoonBlockGeneratorBlockedReason.WallLikeSolid),
            });

            Assert.That(target.BlockedPlayCount, Is.EqualTo(2));
            Assert.That(target.LastPayload.Reason, Is.EqualTo(MoonBlockGeneratorBlockedReason.WallLikeSolid));
            Assert.That(target.LastPayload.BlockingEntityId, Is.EqualTo(21));
            Assert.That(registry.LookupOrder.ToArray(), Is.EqualTo(new[] { 100, 100 }));
        }

        [Test]
        [Category("Extended")]
        public void Consumer_DoesNotMutateRequestCache()
        {
            var first = new RecordingTarget(30, new SurfaceCell(FaceId.Floor, 0, 0));
            var second = new RecordingTarget(10, new SurfaceCell(FaceId.Floor, 1, 0));
            var requests = new List<TilePresentationRequest>
            {
                CreateRequest(first.TileId, first.Cell),
                CreateRequest(second.TileId, second.Cell),
            };
            var beforeTileIds = requests.Select(request => request.TileId).ToArray();
            var registry = new RecordingRegistry(first, second);
            var controller = new TileFeatureVisualPresentationController();
            controller.AttachRegistry(registry);

            controller.PlayButtonActivatedRequests(requests);

            Assert.That(requests.Select(request => request.TileId).ToArray(), Is.EqualTo(beforeTileIds));
            Assert.That(requests, Has.Count.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void StageTileFeatureVisualBinding_InstantiatesConfiguresRegistersAndConsumesButtonRequest()
        {
            var rootObject = new GameObject(nameof(StageTileFeatureVisualBinding_InstantiatesConfiguresRegistersAndConsumesButtonRequest));
            var prefab = new GameObject("ButtonTileVisualPrefab");

            try
            {
                prefab.AddComponent<TileFeatureVisualTargetView>();
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var cell = new SurfaceCell(FaceId.Floor, 2, 1);

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(100, prefab),
                    },
                    new[]
                    {
                        new TileFeatureState(
                            100,
                            cell,
                            TileFeatureKind.Button,
                            TileFeatureFlags.None,
                            sourceEntityId: 0,
                            ownerEntityId: 0,
                            teamId: 0,
                            lifetimeTicks: 0,
                            charges: 0),
                    },
                    rootObject.transform,
                    registry);

                Assert.That(registry.TryGetTileVisual(100, out var target), Is.True);
                Assert.That(target.TileId, Is.EqualTo(100));
                Assert.That(target.Cell, Is.EqualTo(cell));
                Assert.That(rootObject.transform.childCount, Is.EqualTo(1));
                Assert.That(rootObject.transform.GetChild(0).localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(rootObject.transform.GetChild(0).localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(rootObject.transform.GetChild(0).localScale, Is.EqualTo(Vector3.one));

                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);
                controller.PlayButtonActivatedRequests(new[] { CreateRequest(100, cell) });

                Assert.That(((TileFeatureVisualTargetView)target).DebugPlayButtonActivatedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageTileFeatureVisualBinding_BarricadeInitialState_SyncsImmediateActiveState()
        {
            var rootObject = new GameObject(nameof(StageTileFeatureVisualBinding_BarricadeInitialState_SyncsImmediateActiveState));
            var prefab = new GameObject("BarricadeTileVisualPrefab");

            try
            {
                prefab.AddComponent<RecordingBarricadeActiveStateTarget>();
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(100, prefab),
                    },
                    new[]
                    {
                        new TileFeatureState(
                            100,
                            cell,
                            TileFeatureKind.Barricade,
                            TileFeatureFlags.None,
                            sourceEntityId: 0,
                            ownerEntityId: 0,
                            teamId: 0,
                            lifetimeTicks: 0,
                            charges: 0),
                    },
                    rootObject.transform,
                    registry,
                    tileFeatureDefinitions: new[]
                    {
                        CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
                    },
                    initialTopology: new CubeTopologyState(FaceId.Front));

                Assert.That(registry.TryGetTileVisual(100, out var target), Is.True);
                var barricadeTarget = (RecordingBarricadeActiveStateTarget)target;
                Assert.That(barricadeTarget.ImmediateSyncCount, Is.EqualTo(1));
                Assert.That(barricadeTarget.LastImmediateActive, Is.True);
                Assert.That(barricadeTarget.Cell, Is.EqualTo(cell));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageTileFeatureVisualBinding_DestroyAndSlideInitialState_SyncsImmediateActiveState()
        {
            var rootObject = new GameObject(nameof(StageTileFeatureVisualBinding_DestroyAndSlideInitialState_SyncsImmediateActiveState));
            var destroyPrefab = new GameObject("DestroyTileVisualPrefab");
            var slidePrefab = new GameObject("SlideTileVisualPrefab");

            try
            {
                destroyPrefab.AddComponent<RecordingComponentActiveStateTarget>();
                slidePrefab.AddComponent<RecordingComponentActiveStateTarget>();
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var destroyCell = new SurfaceCell(FaceId.Ceiling, 1, 1);
                var slideCell = new SurfaceCell(FaceId.Front, 2, 1);

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(100, destroyPrefab),
                        new TileFeaturePresentationResolvedBinding(101, slidePrefab),
                    },
                    new[]
                    {
                        CreateTileFeatureState(100, destroyCell, TileFeatureKind.Destroy),
                        CreateTileFeatureState(101, slideCell, TileFeatureKind.Slide),
                    },
                    rootObject.transform,
                    registry,
                    tileFeatureDefinitions: new[]
                    {
                        CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
                        CreateTileFeatureDefinition(101, TileFeatureActivationRule.FrontFaceOnly),
                    },
                    initialTopology: new CubeTopologyState(FaceId.Floor));

                Assert.That(registry.TryGetTileVisual(100, out var destroyTarget), Is.True);
                Assert.That(registry.TryGetTileVisual(101, out var slideTarget), Is.True);
                var destroyRecording = (RecordingComponentActiveStateTarget)destroyTarget;
                var slideRecording = (RecordingComponentActiveStateTarget)slideTarget;
                Assert.That(destroyRecording.ActiveStateCalls, Is.EqualTo(1));
                Assert.That(destroyRecording.LastActiveStateKind, Is.EqualTo(TileFeatureKind.Destroy));
                Assert.That(destroyRecording.LastActiveState, Is.False);
                Assert.That(slideRecording.ActiveStateCalls, Is.EqualTo(1));
                Assert.That(slideRecording.LastActiveStateKind, Is.EqualTo(TileFeatureKind.Slide));
                Assert.That(slideRecording.LastActiveState, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(destroyPrefab);
                Object.DestroyImmediate(slidePrefab);
            }
        }

        [Test]
        [Category("Full")]
        public void ExitInitialOpenState_ProductionPrefab_AppliesBeforeFirstTick()
        {
            const string PrefabPath =
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Exit_3x3.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);

            AssertProductionExitInitialState(prefab, expectedOpen: true);
            AssertProductionExitInitialState(prefab, expectedOpen: false);
        }

        private static void AssertProductionExitInitialState(GameObject prefab, bool expectedOpen)
        {
            var rootObject = new GameObject($"{nameof(ExitInitialOpenState_ProductionPrefab_AppliesBeforeFirstTick)}_{expectedOpen}");

            try
            {
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var cell = new SurfaceCell(FaceId.Ceiling, 2, 3);

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(100, prefab),
                    },
                    new[]
                    {
                        CreateTileFeatureState(100, cell, TileFeatureKind.Exit),
                    },
                    rootObject.transform,
                    registry,
                    tileFeatureDefinitions: new[]
                    {
                        CreateTileFeatureDefinition(100, TileFeatureActivationRule.Always),
                    },
                    initialTopology: new CubeTopologyState(FaceId.Floor),
                    initialObjectiveResult: CreateInitialExitObjectiveResult(expectedOpen));

                Assert.That(registry.TryGetTileVisual(100, out var target), Is.True);
                Assert.That(target.TileId, Is.EqualTo(100));
                Assert.That(target.Cell, Is.EqualTo(cell));
                var targetView = (TileFeatureVisualTargetView)target;
                var adapter = targetView.GetComponent<LegacyTileFeatureVisualCueAdapter>();
                Assert.That(adapter, Is.Not.Null);
                Assert.That(targetView.DebugExitOpen, Is.EqualTo(expectedOpen));
                Assert.That(targetView.DebugExitOpenedCount, Is.Zero);
                Assert.That(targetView.DebugExitEnteredCount, Is.Zero);
                Assert.That(adapter.DebugExitProfileOpenStatePlayCount, Is.EqualTo(1));
                Assert.That(adapter.DebugExitLegacyAnimatorFallbackCount, Is.Zero);
                Assert.That(adapter.DebugLegacyAnimatorFallbackCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageTileFeatureVisualBinding_ProductionDestroyPrefabInitialInactiveState_AppliesToInstancedRenderer()
        {
            const string PrefabPath =
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Destroy_Bottom.prefab";
            var rootObject = new GameObject(nameof(StageTileFeatureVisualBinding_ProductionDestroyPrefabInitialInactiveState_AppliesToInstancedRenderer));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);

            try
            {
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);
                var expectedColor = new Color(0.7411765f, 0.75294125f, 0.7725491f, 1f);

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(100, prefab),
                    },
                    new[]
                    {
                        CreateTileFeatureState(100, cell, TileFeatureKind.Destroy),
                    },
                    rootObject.transform,
                    registry,
                    tileFeatureDefinitions: new[]
                    {
                        CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
                    },
                    initialTopology: new CubeTopologyState(FaceId.Floor));

                Assert.That(registry.TryGetTileVisual(100, out _), Is.True);
                AssertAnyRendererMaterialState(rootObject, expectedColor, 1f);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageTileFeatureVisualBinding_BarricadeInitialState_SuppressedByUnitStartsLowered()
        {
            var rootObject = new GameObject(nameof(StageTileFeatureVisualBinding_BarricadeInitialState_SuppressedByUnitStartsLowered));
            var prefab = new GameObject("BarricadeTileVisualPrefab");

            try
            {
                prefab.AddComponent<RecordingBarricadeActiveStateTarget>();
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var cell = new SurfaceCell(FaceId.Ceiling, 1, 1);
                var tileFeatures = new[]
                {
                    new TileFeatureState(
                        100,
                        cell,
                        TileFeatureKind.Barricade,
                        TileFeatureFlags.None,
                        sourceEntityId: 0,
                        ownerEntityId: 0,
                        teamId: 0,
                        lifetimeTicks: 0,
                        charges: 0),
                };
                var topology = new CubeTopologyState(FaceId.Front);
                var initialSnapshot = GameplayWorldStateTestFactory.CreateBounded(
                        new[]
                        {
                            new EntityState
                            {
                                entityId = 30,
                                position = cell,
                                hp = 1,
                                maxHp = 1,
                                teamId = 2,
                                type = EntityType.Unit,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        new BoardBounds(Vector2Int.zero, new Vector2Int(3, 3)),
                        Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                        topology,
                        GameplayTimingProfile.CreateDefault(),
                        tileFeatures)
                    .CreateSnapshot();

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(100, prefab),
                    },
                    tileFeatures,
                    rootObject.transform,
                    registry,
                    tileFeatureDefinitions: new[]
                    {
                        CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
                    },
                    initialTopology: topology,
                    initialSnapshot: initialSnapshot);

                Assert.That(registry.TryGetTileVisual(100, out var target), Is.True);
                var barricadeTarget = (RecordingBarricadeActiveStateTarget)target;
                Assert.That(barricadeTarget.ImmediateSyncCount, Is.EqualTo(1));
                Assert.That(barricadeTarget.LastImmediateActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageTileFeatureVisualBinding_DirectOverrideResolvedBinding_AppliesResolvedLocalPose()
        {
            AssertStageTileFeatureVisualBindingAppliesResolvedLocalPose(
                nameof(StageTileFeatureVisualBinding_DirectOverrideResolvedBinding_AppliesResolvedLocalPose),
                tileId: 101,
                prefabName: "DirectOverrideTileVisualPrefab");
        }

        [Test]
        [Category("Extended")]
        public void StageTileFeatureVisualBinding_CatalogResolvedBinding_AppliesResolvedLocalPose()
        {
            AssertStageTileFeatureVisualBindingAppliesResolvedLocalPose(
                nameof(StageTileFeatureVisualBinding_CatalogResolvedBinding_AppliesResolvedLocalPose),
                tileId: 102,
                prefabName: "CatalogResolvedTileVisualPrefab");
        }

        [Test]
        [Category("Extended")]
        public void StageTileFeatureVisualBinding_PoseResolveFailure_RegistersInactiveTarget()
        {
            var rootObject = new GameObject(nameof(StageTileFeatureVisualBinding_PoseResolveFailure_RegistersInactiveTarget));
            var prefab = new GameObject("UnprojectableTileVisualPrefab");

            try
            {
                prefab.AddComponent<TileFeatureVisualTargetView>();
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var cell = new SurfaceCell(FaceId.Ceiling, 0, 0);

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(100, prefab),
                    },
                    new[]
                    {
                        CreateTileFeatureState(100, cell),
                    },
                    rootObject.transform,
                    registry,
                    new FixedPoseResolver(false, default));

                Assert.That(rootObject.transform.childCount, Is.EqualTo(1));
                var instance = rootObject.transform.GetChild(0).gameObject;
                Assert.That(instance.activeSelf, Is.False);
                Assert.That(registry.TryGetTileVisual(100, out var target), Is.True);
                Assert.That(target.TileId, Is.EqualTo(100));
                Assert.That(target.Cell, Is.EqualTo(cell));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureVisualPoseSynchronizer_RefreshAll_ReusesTargetsAndTogglesVisibility()
        {
            var rootObject = new GameObject(nameof(TileFeatureVisualPoseSynchronizer_RefreshAll_ReusesTargetsAndTogglesVisibility));
            var floorObject = new GameObject("FloorTarget");
            var ceilingObject = new GameObject("CeilingTarget");

            try
            {
                floorObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                ceilingObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var floorCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var ceilingCell = new SurfaceCell(FaceId.Ceiling, 0, 0);
                var floorTarget = floorObject.AddComponent<TileFeatureVisualTargetView>();
                var ceilingTarget = ceilingObject.AddComponent<TileFeatureVisualTargetView>();
                floorTarget.ConfigureTileFeature(100, floorCell);
                ceilingTarget.ConfigureTileFeature(101, ceilingCell);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var resolver = new MutablePoseResolver();
                var floorPose = new SurfaceCellPresentationPose(
                    new Vector3(1f, 2f, 3f),
                    Quaternion.Euler(0f, 45f, 0f),
                    Vector3.one);
                var ceilingPose = new SurfaceCellPresentationPose(
                    new Vector3(4f, 5f, 6f),
                    Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(2f, 2f, 2f));
                resolver.Set(floorCell, true, floorPose);
                resolver.Set(ceilingCell, false, default);
                var synchronizer = new TileFeatureVisualPoseSynchronizer(registry, resolver);

                synchronizer.RefreshAll();

                Assert.That(registry.Targets.Count, Is.EqualTo(2));
                Assert.That(floorObject.activeSelf, Is.True);
                Assert.That(ceilingObject.activeSelf, Is.False);
                Assert.That(Vector3.Distance(floorObject.transform.localPosition, floorPose.LocalPosition), Is.LessThan(0.001f));

                resolver.Set(floorCell, false, default);
                resolver.Set(ceilingCell, true, ceilingPose);

                synchronizer.RefreshAll();

                Assert.That(registry.Targets.Count, Is.EqualTo(2));
                Assert.That(floorObject.activeSelf, Is.False);
                Assert.That(ceilingObject.activeSelf, Is.True);
                Assert.That(Vector3.Distance(ceilingObject.transform.localPosition, ceilingPose.LocalPosition), Is.LessThan(0.001f));
                Assert.That(ceilingObject.transform.localScale, Is.EqualTo(ceilingPose.LocalScale));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureVisualPoseSynchronizer_RefreshAllWithTopology_UsesDestinationActiveFaces()
        {
            var rootObject = new GameObject(nameof(TileFeatureVisualPoseSynchronizer_RefreshAllWithTopology_UsesDestinationActiveFaces));
            var floorObject = new GameObject("FloorTarget");
            var ceilingObject = new GameObject("CeilingTarget");

            try
            {
                floorObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                ceilingObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var floorCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var ceilingCell = new SurfaceCell(FaceId.Ceiling, 0, 0);
                floorObject.AddComponent<TileFeatureVisualTargetView>().ConfigureTileFeature(100, floorCell);
                ceilingObject.AddComponent<TileFeatureVisualTargetView>().ConfigureTileFeature(101, ceilingCell);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var resolver = new BoardSurfaceCellPresentationPoseResolver(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    1f);
                var synchronizer = new TileFeatureVisualPoseSynchronizer(registry, resolver);

                synchronizer.RefreshAll();

                Assert.That(floorObject.activeSelf, Is.True);
                Assert.That(ceilingObject.activeSelf, Is.False);

                synchronizer.RefreshAll(new CubeTopologyState(FaceId.Front));

                Assert.That(floorObject.activeSelf, Is.False);
                Assert.That(ceilingObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageTileFeatureVisualBinding_DuplicateManualTarget_FirstRegisteredTargetWins()
        {
            var rootObject = new GameObject(nameof(StageTileFeatureVisualBinding_DuplicateManualTarget_FirstRegisteredTargetWins));
            var manualObject = new GameObject("ManualTarget");
            var prefab = new GameObject("StageTargetPrefab");

            try
            {
                manualObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var manualTarget = manualObject.AddComponent<TileFeatureVisualTargetView>();
                manualTarget.ConfigureTileFeature(100, cell);
                prefab.AddComponent<TileFeatureVisualTargetView>();
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(100, prefab),
                    },
                    new[]
                    {
                        new TileFeatureState(
                            100,
                            cell,
                            TileFeatureKind.Button,
                            TileFeatureFlags.None,
                            sourceEntityId: 0,
                            ownerEntityId: 0,
                            teamId: 0,
                            lifetimeTicks: 0,
                            charges: 0),
                    },
                    rootObject.transform,
                    registry);

                Assert.That(registry.TryGetTileVisual(100, out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(manualTarget));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(prefab);
            }
        }

        private static void AssertDestroyTileInactiveMaterial(Renderer targetRenderer)
        {
            AssertTileFeatureMaterialState(targetRenderer, Color.white, 1f);
        }

        private static void AssertDestroyTileMaterialCleared(Renderer targetRenderer)
        {
            AssertTileFeatureMaterialCleared(targetRenderer, Color.white, 1f);
        }

        private static void AssertTileFeatureMaterialState(
            Renderer targetRenderer,
            Color expectedColor,
            float expectedMetallic,
            int materialIndex = 0)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

            Assert.That(propertyBlock.GetColor(BaseColorPropertyId), Is.EqualTo(expectedColor));
            Assert.That(propertyBlock.GetColor(ColorPropertyId), Is.EqualTo(expectedColor));
            Assert.That(propertyBlock.GetColor(EmissionColorPropertyId), Is.EqualTo(expectedColor));
            Assert.That(propertyBlock.GetFloat(MetallicPropertyId), Is.EqualTo(expectedMetallic));
        }

        private static void AssertAnyRendererMaterialState(
            GameObject rootObject,
            Color expectedColor,
            float expectedMetallic)
        {
            var renderers = rootObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            var propertyBlock = new MaterialPropertyBlock();
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                for (var materialIndex = 0; materialIndex < renderer.sharedMaterials.Length; materialIndex++)
                {
                    propertyBlock.Clear();
                    renderer.GetPropertyBlock(propertyBlock, materialIndex);
                    if (propertyBlock.GetColor(BaseColorPropertyId) == expectedColor &&
                        propertyBlock.GetFloat(MetallicPropertyId) == expectedMetallic)
                    {
                        return;
                    }
                }
            }

            Assert.Fail($"No instantiated renderer had inactive material state {expectedColor} / {expectedMetallic}.");
        }

        private static void AssertTileFeatureMaterialCleared(
            Renderer targetRenderer,
            Color inactiveColor,
            float inactiveMetallic,
            int materialIndex = 0)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

            Assert.That(propertyBlock.GetColor(BaseColorPropertyId), Is.Not.EqualTo(inactiveColor));
            Assert.That(propertyBlock.GetColor(ColorPropertyId), Is.Not.EqualTo(inactiveColor));
            Assert.That(propertyBlock.GetColor(EmissionColorPropertyId), Is.Not.EqualTo(inactiveColor));
            Assert.That(propertyBlock.GetFloat(MetallicPropertyId), Is.Not.EqualTo(inactiveMetallic));
        }

        private static Material CreateSharedColorMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = color;
            return material;
        }

        private static void ConfigureInactiveVisualOverride(
            TileFeatureVisualTargetView target,
            TileFeatureKind kind,
            Renderer targetRenderer,
            Color inactiveColor,
            float inactiveMetallic)
        {
            ConfigureInactiveVisualTargets(
                target,
                kind,
                (targetRenderer, 0, inactiveColor, inactiveMetallic));
        }

        private static void ConfigureInactiveVisualTargets(
            TileFeatureVisualTargetView target,
            TileFeatureKind kind,
            params (Renderer Renderer, int MaterialIndex, Color InactiveColor, float InactiveMetallic)[] targets)
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("featureKind").enumValueIndex = (int)kind;
            var targetProperties = serializedProfile.FindProperty("inactiveMaterialTargets");
            Assert.That(targetProperties, Is.Not.Null);
            targetProperties.arraySize = targets.Length;
            for (var i = 0; i < targets.Length; i++)
            {
                var targetProperty = targetProperties.GetArrayElementAtIndex(i);
                targetProperty.FindPropertyRelative("Renderer").objectReferenceValue = targets[i].Renderer;
                targetProperty.FindPropertyRelative("MaterialIndex").intValue = targets[i].MaterialIndex;
                targetProperty.FindPropertyRelative("InactiveColor").colorValue = targets[i].InactiveColor;
                targetProperty.FindPropertyRelative("InactiveMetallic").floatValue = targets[i].InactiveMetallic;
            }

            serializedProfile.ApplyModifiedPropertiesWithoutUndo();

            var provider = target.GetComponent<TileFeatureVisualProfileProvider>() ??
                           target.gameObject.AddComponent<TileFeatureVisualProfileProvider>();
            var serializedProvider = new SerializedObject(provider);
            var profilesProperty = serializedProvider.FindProperty("profiles");
            Assert.That(profilesProperty, Is.Not.Null);
            profilesProperty.arraySize = 1;
            profilesProperty.GetArrayElementAtIndex(0).objectReferenceValue = profile;
            serializedProvider.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Animator AttachAnimator(GameObject targetObject, RuntimeAnimatorController controller)
        {
            var animator = targetObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            return animator;
        }

        private static AnimatorController CreateBarricadeAnimatorController(string name)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = $"{name}_StateMachine",
            };
            var loweredState = stateMachine.AddState("LoweredIdle");
            var raisedState = stateMachine.AddState("RaisedIdle");
            stateMachine.defaultState = loweredState;

            var controller = new AnimatorController
            {
                name = $"{name}_Controller",
                layers = new[]
                {
                    new AnimatorControllerLayer
                    {
                        name = "Base Layer",
                        defaultWeight = 1f,
                        stateMachine = stateMachine,
                    },
                },
            };
            controller.AddParameter("BarricadeBlocked", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("BarricadeCrushed", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("BarricadeActivated", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("BarricadeDeactivated", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("BarricadeActive", AnimatorControllerParameterType.Bool);
            Assert.That(raisedState, Is.Not.Null);
            return controller;
        }

#pragma warning disable CS0618
        private static LegacyTileFeatureVisualCueAdapter EnsureLegacyAdapter(TileFeatureVisualTargetView target)
        {
            var adapter = target.GetComponent<LegacyTileFeatureVisualCueAdapter>();
            if (adapter == null)
            {
                adapter = target.gameObject.AddComponent<LegacyTileFeatureVisualCueAdapter>();
            }

            adapter.ConfigureTarget(target);
            return adapter;
        }
#pragma warning restore CS0618

        private static TilePresentationRequest CreateRequest(int tileId, SurfaceCell cell)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.ButtonActivated,
                tileId,
                cell,
                TileFeatureKind.Button,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationRequest CreateDestroyRequest(int tileId, SurfaceCell cell)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.DestroyTileTriggered,
                tileId,
                cell,
                TileFeatureKind.Destroy,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: tileId + 4);
        }

        private static TilePresentationRequest CreateDestroyActivatedRequest(int tileId, SurfaceCell cell)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.DestroyTileActivated,
                tileId,
                cell,
                TileFeatureKind.Destroy,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationRequest CreateDestroyDeactivatedRequest(int tileId, SurfaceCell cell)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.DestroyTileDeactivated,
                tileId,
                cell,
                TileFeatureKind.Destroy,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationRequest CreateSlideRequest(
            int tileId,
            SurfaceCell cell,
            Direction direction,
            int targetEntityId)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.SlideTileRedirected,
                tileId,
                cell,
                TileFeatureKind.Slide,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: targetEntityId,
                direction: direction);
        }

        private static TilePresentationRequest CreateBarricadeBlockedRequest(
            int tileId,
            SurfaceCell cell,
            Direction direction,
            int targetEntityId)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.BarricadeBlocked,
                tileId,
                cell,
                TileFeatureKind.Barricade,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: targetEntityId,
                direction: direction);
        }

        private static TilePresentationRequest CreateBarricadeActivatedRequest(int tileId, SurfaceCell cell)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.BarricadeActivated,
                tileId,
                cell,
                TileFeatureKind.Barricade,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationRequest CreateBarricadeDeactivatedRequest(int tileId, SurfaceCell cell)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.BarricadeDeactivated,
                tileId,
                cell,
                TileFeatureKind.Barricade,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationRequest CreateBarricadeCrushedRequest(
            int tileId,
            SurfaceCell cell,
            int targetEntityId)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.BarricadeCrushed,
                tileId,
                cell,
                TileFeatureKind.Barricade,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: targetEntityId);
        }

        private static TilePresentationRequest CreateExitOpenedRequest(int tileId, SurfaceCell cell)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.ExitOpened,
                tileId,
                cell,
                TileFeatureKind.Exit,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationRequest CreateExitEnteredRequest(
            int tileId,
            SurfaceCell cell,
            int playerEntityId)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.ExitEntered,
                tileId,
                cell,
                TileFeatureKind.Exit,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: playerEntityId);
        }

        private static TilePresentationRequest CreateMoonBlockGeneratedRequest(
            int tileId,
            SurfaceCell cell,
            int moonBlockEntityId)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.MoonBlockGenerated,
                tileId,
                cell,
                TileFeatureKind.MoonBlockGenerator,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: moonBlockEntityId);
        }

        private static TilePresentationRequest CreateMoonBlockGeneratorBlockedRequest(
            int tileId,
            SurfaceCell cell,
            int blockerEntityId,
            MoonBlockGeneratorBlockedReason reason = MoonBlockGeneratorBlockedReason.UnitOccupant)
        {
            var payload = new MoonBlockGeneratorBlockedPayload(reason, blockerEntityId, cell);
            return new TilePresentationRequest(
                TilePresentationRequestKind.MoonBlockGeneratorBlocked,
                tileId,
                cell,
                TileFeatureKind.MoonBlockGenerator,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: blockerEntityId,
                moonBlockGeneratorBlockedPayload: payload);
        }

        private static void AssertStageTileFeatureVisualBindingAppliesResolvedLocalPose(
            string objectName,
            int tileId,
            string prefabName)
        {
            var rootObject = new GameObject(objectName);
            var prefab = new GameObject(prefabName);

            try
            {
                prefab.AddComponent<TileFeatureVisualTargetView>();
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(rootObject.transform);
                var cell = new SurfaceCell(FaceId.Floor, 1, 0);
                var expectedPose = new SurfaceCellPresentationPose(
                    new Vector3(1.25f, -0.5f, 2.75f),
                    Quaternion.Euler(20f, 45f, 10f),
                    new Vector3(1f, 1.5f, 0.75f));
                var resolver = new FixedPoseResolver(canResolve: true, expectedPose);

                InvokeStageTileFeatureVisualInstantiation(
                    new[]
                    {
                        new TileFeaturePresentationResolvedBinding(tileId, prefab),
                    },
                    new[]
                    {
                        CreateTileFeatureState(tileId, cell),
                    },
                    rootObject.transform,
                    registry,
                    resolver);

                Assert.That(resolver.RequestedCells, Is.EqualTo(new[] { cell }));
                Assert.That(rootObject.transform.childCount, Is.EqualTo(1));
                var instance = rootObject.transform.GetChild(0);
                Assert.That(Vector3.Distance(instance.localPosition, expectedPose.LocalPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(instance.localRotation, expectedPose.LocalRotation), Is.LessThan(0.001f));
                Assert.That(instance.localScale, Is.EqualTo(expectedPose.LocalScale));

                Assert.That(registry.TryGetTileVisual(tileId, out var target), Is.True);
                Assert.That(target.TileId, Is.EqualTo(tileId));
                Assert.That(target.Cell, Is.EqualTo(cell));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(prefab);
            }
        }

        private static TileFeatureState CreateTileFeatureState(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind = TileFeatureKind.Button)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateTileFeatureDefinition(
            int tileId,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: string.Empty);
        }

        private static StageObjectiveTickResult CreateInitialExitObjectiveResult(bool requiredNonPrimarySatisfied)
        {
            return new StageObjectiveTickResult(
                hasObjective: true,
                goalReached: false,
                allConditionsSatisfied: false,
                clearedThisTick: false,
                isCleared: false,
                hasRequiredNonPrimaryConditions: true,
                requiredNonPrimaryConditionsSatisfied: requiredNonPrimarySatisfied,
                requiredNonPrimaryConditionsSatisfiedThisTick: false,
                conditionStatuses: System.Array.Empty<StageConditionStatus>());
        }

        private static void InvokeStageTileFeatureVisualInstantiation(
            IReadOnlyList<TileFeaturePresentationResolvedBinding> bindings,
            IReadOnlyList<TileFeatureState> initialTileFeatures,
            Transform parent,
            TileFeatureVisualRegistry registry,
            ISurfaceCellPresentationPoseResolver poseResolver = null,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            CubeTopologyState? initialTopology = null,
            WorldSnapshot initialSnapshot = null,
            StageObjectiveTickResult initialObjectiveResult = null)
        {
            var method = typeof(GameplayHostRuntimeFactory).GetMethod(
                "InstantiateStageTileFeatureVisuals",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(
                null,
                new object[]
                {
                    bindings,
                    initialTileFeatures,
                    tileFeatureDefinitions ?? System.Array.Empty<TileFeatureRuntimeDefinition>(),
                    initialTopology ?? new CubeTopologyState(FaceId.Floor),
                    parent,
                    registry,
                    poseResolver,
                    null,
                    initialSnapshot,
                    initialObjectiveResult,
                });
        }

        private sealed class FixedPoseResolver : ISurfaceCellPresentationPoseResolver
        {
            private readonly bool _canResolve;
            private readonly SurfaceCellPresentationPose _pose;

            public FixedPoseResolver(bool canResolve, SurfaceCellPresentationPose pose)
            {
                _canResolve = canResolve;
                _pose = pose;
            }

            public List<SurfaceCell> RequestedCells { get; } = new();

            public bool TryResolvePose(SurfaceCell cell, out SurfaceCellPresentationPose pose)
            {
                RequestedCells.Add(cell);
                pose = _canResolve ? _pose : default;
                return _canResolve;
            }
        }

        private sealed class MutablePoseResolver : ISurfaceCellPresentationPoseResolver
        {
            private readonly Dictionary<SurfaceCell, (bool CanResolve, SurfaceCellPresentationPose Pose)> _posesByCell = new();

            public void Set(SurfaceCell cell, bool canResolve, SurfaceCellPresentationPose pose)
            {
                _posesByCell[cell] = (canResolve, pose);
            }

            public bool TryResolvePose(SurfaceCell cell, out SurfaceCellPresentationPose pose)
            {
                if (_posesByCell.TryGetValue(cell, out var result) &&
                    result.CanResolve)
                {
                    pose = result.Pose;
                    return true;
                }

                pose = default;
                return false;
            }
        }

        private sealed class RecordingRegistry : ITileFeatureVisualRegistry
        {
            private readonly Dictionary<int, ITileFeatureVisualTarget> _targetsByTileId = new();

            public RecordingRegistry(params ITileFeatureVisualTarget[] targets)
            {
                for (var i = 0; i < targets.Length; i++)
                {
                    _targetsByTileId[targets[i].TileId] = targets[i];
                }
            }

            public int TryGetCallCount { get; private set; }

            public List<int> LookupOrder { get; } = new();

            public bool TryGetTileVisual(int tileId, out ITileFeatureVisualTarget target)
            {
                TryGetCallCount++;
                LookupOrder.Add(tileId);
                return _targetsByTileId.TryGetValue(tileId, out target);
            }
        }

        private sealed class RecordingTarget : ITileFeatureVisualTarget
        {
            private readonly List<int> _calls;

            public RecordingTarget(int tileId, SurfaceCell cell, List<int> calls = null)
            {
                TileId = tileId;
                Cell = cell;
                _calls = calls;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int PlayCount { get; private set; }

            public void PlayButtonActivated()
            {
                PlayCount++;
                _calls?.Add(TileId);
            }
        }

        private sealed class RecordingDestroyTarget : ITileFeatureVisualTarget, IDestroyTileVisualTarget
        {
            public RecordingDestroyTarget(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int ButtonPlayCount { get; private set; }

            public int DestroyPlayCount { get; private set; }

            public void PlayButtonActivated()
            {
                ButtonPlayCount++;
            }

            public void PlayDestroyTileTriggered()
            {
                DestroyPlayCount++;
            }
        }

        private sealed class RecordingSlideTarget : ITileFeatureVisualTarget, ISlideTileVisualTarget
        {
            public RecordingSlideTarget(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int ButtonPlayCount { get; private set; }

            public int SlidePlayCount { get; private set; }

            public Direction LastDirection { get; private set; }

            public int LastTargetEntityId { get; private set; }

            public void PlayButtonActivated()
            {
                ButtonPlayCount++;
            }

            public void PlaySlideTileRedirected(Direction direction, int targetEntityId)
            {
                SlidePlayCount++;
                LastDirection = direction;
                LastTargetEntityId = targetEntityId;
            }
        }

        private sealed class RecordingActiveStateTarget : ITileFeatureVisualTarget, ITileFeatureActiveStateVisualTarget
        {
            public RecordingActiveStateTarget(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int ButtonPlayCount { get; private set; }

            public int ActiveStateCalls { get; private set; }

            public TileFeatureKind LastActiveStateKind { get; private set; }

            public bool LastActiveState { get; private set; }

            public void PlayButtonActivated()
            {
                ButtonPlayCount++;
            }

            public void SetTileFeatureActiveImmediate(TileFeatureKind kind, bool active)
            {
                ActiveStateCalls++;
                LastActiveStateKind = kind;
                LastActiveState = active;
            }
        }

        private sealed class RecordingBarricadeTarget :
            ITileFeatureVisualTarget,
            IBarricadeBlockedVisualTarget,
            IBarricadeCrushedVisualTarget
        {
            public RecordingBarricadeTarget(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int ButtonPlayCount { get; private set; }

            public int BlockedPlayCount { get; private set; }

            public int CrushedPlayCount { get; private set; }

            public Direction LastBlockedDirection { get; private set; }

            public int LastBlockedTargetEntityId { get; private set; }

            public int LastCrushedTargetEntityId { get; private set; }

            public void PlayButtonActivated()
            {
                ButtonPlayCount++;
            }

            public void PlayBarricadeBlocked(Direction direction, int targetEntityId)
            {
                BlockedPlayCount++;
                LastBlockedDirection = direction;
                LastBlockedTargetEntityId = targetEntityId;
            }

            public void PlayBarricadeCrushed(int targetEntityId)
            {
                CrushedPlayCount++;
                LastCrushedTargetEntityId = targetEntityId;
            }
        }

        private sealed class RecordingBarricadeActiveStateTarget :
            MonoBehaviour,
            ITileFeatureVisualTarget,
            ITileFeatureVisualTargetConfigurator,
            IBarricadeActiveStateVisualTarget
        {
            public int TileId { get; private set; }

            public SurfaceCell Cell { get; private set; }

            public bool? LastImmediateActive { get; private set; }

            public int ImmediateSyncCount { get; private set; }

            public void ConfigureTileFeature(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public void PlayButtonActivated()
            {
            }

            public void SetBarricadeActiveImmediate(bool active)
            {
                ImmediateSyncCount++;
                LastImmediateActive = active;
            }
        }

        private sealed class RecordingComponentActiveStateTarget :
            MonoBehaviour,
            ITileFeatureVisualTarget,
            ITileFeatureVisualTargetConfigurator,
            ITileFeatureActiveStateVisualTarget
        {
            public int TileId { get; private set; }

            public SurfaceCell Cell { get; private set; }

            public int ActiveStateCalls { get; private set; }

            public TileFeatureKind LastActiveStateKind { get; private set; }

            public bool LastActiveState { get; private set; }

            public void ConfigureTileFeature(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public void SetTileFeatureActiveImmediate(TileFeatureKind kind, bool active)
            {
                ActiveStateCalls++;
                LastActiveStateKind = kind;
                LastActiveState = active;
            }
        }

        private sealed class RecordingExitTarget :
            ITileFeatureVisualTarget,
            IExitOpenedVisualTarget,
            IExitEnteredVisualTarget
        {
            public RecordingExitTarget(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int ButtonPlayCount { get; private set; }

            public int OpenedPlayCount { get; private set; }

            public int EnteredPlayCount { get; private set; }

            public int LastPlayerEntityId { get; private set; }

            public void PlayButtonActivated()
            {
                ButtonPlayCount++;
            }

            public void PlayExitOpened()
            {
                OpenedPlayCount++;
            }

            public void PlayExitEntered(int playerEntityId)
            {
                EnteredPlayCount++;
                LastPlayerEntityId = playerEntityId;
            }
        }

        private sealed class RecordingExitOpenStateTarget :
            ITileFeatureVisualTarget,
            IExitOpenedVisualTarget,
            IExitOpenStateVisualTarget
        {
            public RecordingExitOpenStateTarget(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int ButtonPlayCount { get; private set; }

            public int OpenedPlayCount { get; private set; }

            public int ImmediateSyncCount { get; private set; }

            public bool? LastImmediateOpen { get; private set; }

            public void PlayButtonActivated()
            {
                ButtonPlayCount++;
            }

            public void PlayExitOpened()
            {
                OpenedPlayCount++;
            }

            public void SetExitOpenImmediate(bool open)
            {
                ImmediateSyncCount++;
                LastImmediateOpen = open;
            }
        }

        private sealed class RecordingMoonBlockGeneratedTarget :
            ITileFeatureVisualTarget,
            IMoonBlockGeneratedVisualTarget
        {
            public RecordingMoonBlockGeneratedTarget(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int ButtonPlayCount { get; private set; }

            public int GeneratedPlayCount { get; private set; }

            public int LastMoonBlockEntityId { get; private set; }

            public void PlayButtonActivated()
            {
                ButtonPlayCount++;
            }

            public void PlayMoonBlockGenerated(int moonBlockEntityId)
            {
                GeneratedPlayCount++;
                LastMoonBlockEntityId = moonBlockEntityId;
            }
        }

        private sealed class RecordingMoonBlockGeneratorBlockedTarget :
            ITileFeatureVisualTarget,
            IMoonBlockGeneratorBlockedVisualTarget
        {
            public RecordingMoonBlockGeneratorBlockedTarget(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public int TileId { get; }

            public SurfaceCell Cell { get; }

            public int ButtonPlayCount { get; private set; }

            public int BlockedPlayCount { get; private set; }

            public MoonBlockGeneratorBlockedPayload LastPayload { get; private set; }

            public void PlayButtonActivated()
            {
                ButtonPlayCount++;
            }

            public void PlayMoonBlockGeneratorBlocked(MoonBlockGeneratorBlockedPayload payload)
            {
                BlockedPlayCount++;
                LastPayload = payload;
            }
        }
    }
}
