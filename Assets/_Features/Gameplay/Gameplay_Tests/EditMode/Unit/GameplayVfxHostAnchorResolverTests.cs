using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxHostAnchorResolverTests
    {
        [Test]
        [Category("Extended")]
        public void CellAnchor_ResolvesSurfaceCellAndPreservesFace()
        {
            var cell = new SurfaceCell(FaceId.Front, 2, 3);
            var topology = new CubeTopologyState(FaceId.Floor);
            var resolver = CreateResolver();

            var result = resolver.TryResolve(CreateRequest(VfxAnchor.ForCell(cell, topology)), out var resolved);

            Assert.That(result, Is.True);
            Assert.That(resolved.HasCell, Is.True);
            Assert.That(resolved.Cell, Is.EqualTo(cell));
            Assert.That(resolved.Cell.face, Is.EqualTo(FaceId.Front));
            Assert.That(resolved.Topology, Is.EqualTo(topology));
            Assert.That(resolved.UsedFallback, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void CellAnchor_ProjectionFailure_ReturnsFalse()
        {
            var resolver = CreateResolver(cellProjector: new FakeCellAnchorProjector { ResolveSuccess = false });

            var result = resolver.TryResolve(
                CreateRequest(VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Front, 2, 3),
                    new CubeTopologyState(FaceId.Floor))),
                out var resolved);

            Assert.That(result, Is.False);
            Assert.That(resolved.IsResolved, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EntityAnchor_UsesLiveEntityWhenAvailable()
        {
            var entityProjector = new FakeEntityAnchorProjector();
            entityProjector.AddLiveEntity(10);
            var resolver = CreateResolver(entityProjector: entityProjector);

            var result = resolver.TryResolve(CreateRequest(VfxAnchor.ForEntity(10)), out var resolved);

            Assert.That(result, Is.True);
            Assert.That(resolved.HasEntity, Is.True);
            Assert.That(resolved.EntityId, Is.EqualTo(10));
            Assert.That(resolved.Slot, Is.EqualTo(VfxAnchorSlot.EntityCenter));
            Assert.That(resolved.UsedFallback, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EntityAnchor_UsesFallbackCellWhenEntityMissing()
        {
            var fallbackCell = new SurfaceCell(FaceId.Back, 1, 2);
            var fallbackTopology = new CubeTopologyState(FaceId.Floor);
            var resolver = CreateResolver();

            var result = resolver.TryResolve(
                CreateRequest(VfxAnchor.ForEntity(
                    10,
                    fallbackCell: fallbackCell,
                    fallbackTopology: fallbackTopology,
                    hasFallbackCell: true)),
                out var resolved);

            Assert.That(result, Is.True);
            Assert.That(resolved.HasCell, Is.True);
            Assert.That(resolved.Cell, Is.EqualTo(fallbackCell));
            Assert.That(resolved.Topology, Is.EqualTo(fallbackTopology));
            Assert.That(resolved.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            Assert.That(resolved.UsedFallback, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EntityAnchor_MissingWithoutFallback_ReturnsFalse()
        {
            var resolver = CreateResolver();

            var result = resolver.TryResolve(CreateRequest(VfxAnchor.ForEntity(10)), out var resolved);

            Assert.That(result, Is.False);
            Assert.That(resolved.IsResolved, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EntitySlot_UnsupportedSlot_UsesFallbackOrFails()
        {
            var fallbackCell = new SurfaceCell(FaceId.Floor, 4, 5);
            var topology = new CubeTopologyState(FaceId.Floor);
            var resolver = CreateResolver();

            var withFallback = resolver.TryResolve(
                CreateRequest(VfxAnchor.ForEntitySlot(
                    10,
                    VfxAnchorSlot.EntityHead,
                    fallbackCell,
                    topology,
                    true)),
                out var fallbackResolved);
            var withoutFallback = resolver.TryResolve(
                CreateRequest(VfxAnchor.ForEntitySlot(10, VfxAnchorSlot.EntityHead)),
                out var missingResolved);

            Assert.That(withFallback, Is.True);
            Assert.That(fallbackResolved.HasCell, Is.True);
            Assert.That(fallbackResolved.Cell, Is.EqualTo(fallbackCell));
            Assert.That(fallbackResolved.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            Assert.That(fallbackResolved.UsedFallback, Is.True);
            Assert.That(withoutFallback, Is.False);
            Assert.That(missingResolved.IsResolved, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void CellToEntity_ValidatesSourceCellAndTargetEntity()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 2);
            var topology = new CubeTopologyState(FaceId.Floor);
            var cellProjector = new FakeCellAnchorProjector();
            var entityProjector = new FakeEntityAnchorProjector();
            entityProjector.AddLiveEntity(20);
            var resolver = CreateResolver(cellProjector, entityProjector);

            var result = resolver.TryResolve(
                CreateRequest(VfxAnchor.FromCellToEntity(cell, topology, 20)),
                out var resolved);

            Assert.That(result, Is.True);
            Assert.That(resolved.HasCell, Is.True);
            Assert.That(resolved.Cell, Is.EqualTo(cell));
            Assert.That(resolved.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            Assert.That(cellProjector.LastCell, Is.EqualTo(cell));
            Assert.That(entityProjector.LastEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void EntityToCell_UsesEntityOrFallback()
        {
            var targetCell = new SurfaceCell(FaceId.Back, 2, 1);
            var topology = new CubeTopologyState(FaceId.Floor);
            var liveEntityProjector = new FakeEntityAnchorProjector();
            liveEntityProjector.AddLiveEntity(10);
            var liveResolver = CreateResolver(entityProjector: liveEntityProjector);
            var fallbackResolver = CreateResolver();

            var liveResult = liveResolver.TryResolve(
                CreateRequest(VfxAnchor.FromEntityToCell(10, targetCell, topology)),
                out var liveResolved);
            var fallbackResult = fallbackResolver.TryResolve(
                CreateRequest(VfxAnchor.FromEntityToCell(10, targetCell, topology)),
                out var fallbackResolved);

            Assert.That(liveResult, Is.True);
            Assert.That(liveResolved.HasEntity, Is.True);
            Assert.That(liveResolved.EntityId, Is.EqualTo(10));
            Assert.That(liveResolved.UsedFallback, Is.False);
            Assert.That(fallbackResult, Is.True);
            Assert.That(fallbackResolved.HasCell, Is.True);
            Assert.That(fallbackResolved.Cell, Is.EqualTo(targetCell));
            Assert.That(fallbackResolved.UsedFallback, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void UnsupportedMotionTrack_ReturnsFalse()
        {
            var resolver = CreateResolver();

            var result = resolver.TryResolve(CreateRequest(VfxAnchor.ForMotionTrack(30)), out var resolved);

            Assert.That(result, Is.False);
            Assert.That(resolved.IsResolved, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostCellProjector_UsesGameplayCubeProjectorAndPreservesCell()
        {
            var projector = new Game.Feature.Gameplay.Host.GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            var cellProjector = new GameplayVfxHostCellAnchorProjector(projector);
            var cell = new SurfaceCell(FaceId.Front, 1, 1);
            var topology = new CubeTopologyState(FaceId.Floor);

            var result = cellProjector.TryResolveCell(
                cell,
                topology,
                VfxAnchorSlot.CellAboveOccupant,
                out var resolved);

            Assert.That(result, Is.True);
            Assert.That(resolved.HasCell, Is.True);
            Assert.That(resolved.Cell, Is.EqualTo(cell));
            Assert.That(resolved.Slot, Is.EqualTo(VfxAnchorSlot.CellAboveOccupant));
        }

        [Test]
        [Category("Extended")]
        public void HostCellProjector_CellFloor_ResolvesVisibleTileFace()
        {
            var projector = new Game.Feature.Gameplay.Host.GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                2f);
            var cellProjector = new GameplayVfxHostCellAnchorProjector(projector);
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);

            var result = cellProjector.TryResolveCell(
                cell,
                topology,
                VfxAnchorSlot.CellFloor,
                out var resolved);

            Assert.That(result, Is.True);
            Assert.That(projector.TryProjectSurfaceCell(cell, topology, out var projectedPose), Is.True);
            var expectedPosition = projectedPose.LocalPosition -
                                   (projectedPose.Normal * projector.SurfaceTileThickness);
            Assert.That(Vector3.Distance(resolved.LocalPosition, expectedPosition), Is.LessThan(0.0001f));
            Assert.That(Vector3.Angle(resolved.LocalRotation * Vector3.forward, -projectedPose.Normal), Is.LessThan(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void HostEntityProjector_UsesPresentationStateAndRejectsUnsupportedSlots()
        {
            var stateStore = new Game.Feature.Gameplay.Host.GameplayPresentationStateStore();
            stateStore.PresentedLocalPosesByEntityId[10] =
                new Game.Feature.Gameplay.Host.GameplayEntityPose(Vector3.zero, Quaternion.identity);
            var entityProjector = new GameplayVfxHostEntityAnchorProjector(stateStore);

            var centerResult = entityProjector.TryResolveEntity(
                10,
                VfxAnchorSlot.EntityCenter,
                out var centerResolved);
            var headResult = entityProjector.TryResolveEntity(
                10,
                VfxAnchorSlot.EntityHead,
                out var headResolved);

            Assert.That(centerResult, Is.True);
            Assert.That(centerResolved.HasEntity, Is.True);
            Assert.That(centerResolved.EntityId, Is.EqualTo(10));
            Assert.That(headResult, Is.False);
            Assert.That(headResolved.IsResolved, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Resolver_DoesNotCreateSnapshots()
        {
            var entityProjector = new FakeEntityAnchorProjector();
            entityProjector.AddLiveEntity(10);
            var resolver = CreateResolver(entityProjector: entityProjector);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                resolver.TryResolve(CreateRequest(VfxAnchor.ForEntity(10)), out _);
                counts = capture.Counts;
            }

            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
        }

        private static GameplayVfxHostAnchorResolver CreateResolver(
            FakeCellAnchorProjector cellProjector = null,
            FakeEntityAnchorProjector entityProjector = null)
        {
            return new GameplayVfxHostAnchorResolver(
                cellProjector ?? new FakeCellAnchorProjector(),
                entityProjector ?? new FakeEntityAnchorProjector());
        }

        private static GameplayVfxRequest CreateRequest(VfxAnchor anchor)
        {
            return new GameplayVfxRequest(
                1,
                1,
                17,
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);
        }

        private sealed class FakeCellAnchorProjector : IGameplayVfxCellAnchorProjector
        {
            public bool ResolveSuccess { get; set; } = true;

            public SurfaceCell LastCell { get; private set; }

            public bool TryResolveCell(
                SurfaceCell cell,
                CubeTopologyState topology,
                VfxAnchorSlot slot,
                out VfxResolvedAnchor resolvedAnchor)
            {
                LastCell = cell;
                if (!ResolveSuccess)
                {
                    resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                    return false;
                }

                resolvedAnchor = VfxResolvedAnchor.ForCell(cell, topology, slot);
                return true;
            }
        }

        private sealed class FakeEntityAnchorProjector : IGameplayVfxEntityAnchorProjector
        {
            private readonly System.Collections.Generic.HashSet<int> liveEntityIds = new();

            public int LastEntityId { get; private set; }

            public void AddLiveEntity(int entityId)
            {
                liveEntityIds.Add(entityId);
            }

            public bool TryResolveEntity(
                int entityId,
                VfxAnchorSlot slot,
                out VfxResolvedAnchor resolvedAnchor)
            {
                LastEntityId = entityId;
                if (slot == VfxAnchorSlot.EntityCenter && liveEntityIds.Contains(entityId))
                {
                    resolvedAnchor = VfxResolvedAnchor.ForEntity(entityId, slot, default, default);
                    return true;
                }

                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }
        }
    }
}
