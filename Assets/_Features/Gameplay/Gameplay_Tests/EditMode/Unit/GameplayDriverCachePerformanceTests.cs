using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    // Explicit opt-in: CPU samples are evidence, never a wall-clock CI assertion.
    public sealed class GameplayDriverCachePerformanceTests
    {
        internal const string CatalogPath = "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Catalogs/EnemyPresentationCatalog_CampaignMain.asset";
        internal const string PlayerPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";

        [TestCase(1)]
        [TestCase(10)]
        [Category("Full")]
        public void MeasureFixedCohort(int copies)
        {
            if (Environment.GetEnvironmentVariable("J2M_DRIVER_CACHE_PERFORMANCE") != "1")
                Assert.Ignore("Opt in with J2M_DRIVER_CACHE_PERFORMANCE=1 through run_tests.sh.");

            var root = new GameObject(nameof(GameplayDriverCachePerformanceTests));
            var sync = new GameplayAnimationSyncCoordinator();
            try
            {
                var registry = root.AddComponent<GameplayEntityViewRegistry>();
                var catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(CatalogPath);
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(PlayerPath);
                Assert.That(catalog, Is.Not.Null);
                Assert.That(catalog.Entries.Length, Is.EqualTo(10));
                Assert.That(playerPrefab, Is.Not.Null);
                var entities = new List<EntityState>();
                var views = new List<GameplayEntityView>();
                for (var copy = 0; copy < copies; copy++)
                {
                    var prefabs = new Dictionary<int, GameplayEntityView>();
                    for (var i = 0; i < 10; i++) prefabs.Add(copy * 12 + i + 1, catalog.Entries[i].ViewPrefab);
                    var factory = new DefaultGameplayEntityViewFactory(root.transform, 1f,
                        playerEntityId: copy * 12 + 11, playerViewPrefab: playerPrefab,
                        enemyViewPrefabsByEntityId: prefabs);
                    for (var i = 0; i < 12; i++)
                    {
                        var id = copy * 12 + i + 1;
                        var entity = new EntityState
                        {
                            entityId = id, type = i == 11 ? EntityType.Box : EntityType.Unit,
                            unitRole = i < 10 ? UnitRole.Enemy : i == 10 ? UnitRole.Player : default,
                            hp = 1, maxHp = 1, boardPresence = EntityBoardPresence.Occupying,
                            position = new SurfaceCell(FaceId.Floor, id - 1, 0), facing = Direction.Right,
                        };
                        GameplayEntityView view;
                        if (i == 11)
                        {
                            var box = new GameObject("DriverlessBox");
                            box.transform.SetParent(root.transform, false);
                            view = box.AddComponent<GameplayEntityView>();
                            view.Initialize(id);
                        }
                        else view = factory.CreateView(entity);
                        registry.Register(view);
                        views.Add(view);
                        entities.Add(entity);
                    }
                }

                var store = new GameplayPresentationStateStore();
                var builder = new GameplayCommittedFrameBuilder(store,
                    new GameplayPoseResolver(store, new GameplayPresentationTrackState()), sync);
                var topology = new CubeTopologyState(FaceId.Floor);
                var projector = new GameplayCubeProjector(new BoardBounds(Vector2Int.zero,
                    new Vector2Int(entities.Count - 1, 0)), 1f);
                var binder = new GameplayEntityViewBinder(registry, null);
                Action cache = () => { for (var i = 0; i < views.Count; i++) sync.CacheDrivers(views[i].EntityId, views[i]); };
                Action committed = () => builder.StoreCommittedFrame(entities, topology, projector, binder, null);
                // Resolve once, then measure the identical workload with all dictionaries warm.
                Sample("first-resolve", copies, cache, 1);
                for (var i = 0; i < 200; i++) { cache(); committed(); }
                Sample("cache-hit", copies, cache, 2000);
                Sample("committed-frame", copies, committed, 2000);
                Sample("reset", copies, sync.Reset, 1);
                Sample("resolve-after-reset", copies, cache, 1);
                if (Environment.GetEnvironmentVariable("J2M_DRIVER_CACHE_ALLOCATION") == "1")
                {
                    // GC.GetAllocatedBytesForCurrentThread can be inert in this Editor.
                    // GC.Alloc must prove both a live positive and an empty control.
                    Action positive = () => GC.KeepAlive(new byte[4096]);
                    Action empty = () => { };
                    RecordAllocationCount("warmup-control", copies, positive, 1);
                    for (var sample = 0; sample < 5; sample++)
                    {
                        Assert.That(RecordAllocationCount("positive-control", copies, positive, 1), Is.GreaterThan(0));
                        Assert.That(RecordAllocationCount("empty-control", copies, empty, 2000), Is.Zero);
                        Assert.That(RecordAllocationCount("cache-hit", copies, cache, 2000), Is.Zero);
                        RecordAllocationCount("committed-frame", copies, committed, 2000);
                    }
                }
            }
            finally
            {
                sync.Reset();
                Object.DestroyImmediate(root);
            }
        }

        private static long RecordAllocationCount(string phase, int copies, Action action, int iterations)
        {
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 65536,
                       ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                for (var i = 0; i < iterations; i++) action();
                recorder.Stop();
                Assert.That(recorder.Valid, Is.True, "GC.Alloc recorder must be available.");
                Assert.That(recorder.Count, Is.LessThan(recorder.Capacity), "Reject truncated allocation samples.");
                // GC.Alloc is a marker: Value is duration, not allocation bytes.
                // Count measures allocation events. Do not label it as byte volume.
                TestContext.Out.WriteLine($"DRIVER_CACHE_GC phase={phase} entities={copies * 12} iterations={iterations} allocation_events={recorder.Count}");
                return recorder.Count;
            }
        }

        private static void Sample(string phase, int copies, Action action, int iterations)
        {
            // JIT and formatting are outside the timed/allocated region.
            GC.GetAllocatedBytesForCurrentThread();
            Stopwatch.GetTimestamp();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++) action();
            var ticks = Stopwatch.GetTimestamp() - started;
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            TestContext.Out.WriteLine($"DRIVER_CACHE_PERF phase={phase} entities={copies * 12} iterations={iterations} elapsed_ms={ticks * 1000.0 / Stopwatch.Frequency:F6} managed_counter_raw_delta={allocated}");
        }
    }
}
