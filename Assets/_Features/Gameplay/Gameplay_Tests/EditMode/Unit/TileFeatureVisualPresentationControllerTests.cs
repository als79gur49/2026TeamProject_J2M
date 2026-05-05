using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
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
    }
}
