using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualPresentationControllerTests
    {
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

                Assert.That(target.DebugPlayBarricadeBlockedCount, Is.EqualTo(1));
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

                Assert.That(target.DebugPlayBarricadeCrushedCount, Is.EqualTo(1));
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

                Assert.That(target.DebugPlayExitOpenedCount, Is.EqualTo(1));
                Assert.That(target.DebugPlayExitEnteredCount, Is.EqualTo(1));
                Assert.That(target.DebugLastExitEnteredPlayerEntityId, Is.EqualTo(10));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
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
                CreateBarricadeBlockedRequest(100, cell, Direction.Right, targetEntityId: 20),
                CreateBarricadeCrushedRequest(100, cell, targetEntityId: 20),
            });

            Assert.That(target.PlayCount, Is.Zero);
            Assert.That(diagnostics, Has.Count.EqualTo(2));
            Assert.That(diagnostics[0], Does.Contain("unsupported BarricadeBlocked"));
            Assert.That(diagnostics[1], Does.Contain("unsupported BarricadeCrushed"));
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

        private static void InvokeStageTileFeatureVisualInstantiation(
            IReadOnlyList<TileFeaturePresentationResolvedBinding> bindings,
            IReadOnlyList<TileFeatureState> initialTileFeatures,
            Transform parent,
            TileFeatureVisualRegistry registry)
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
                    parent,
                    registry,
                });
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
    }
}
