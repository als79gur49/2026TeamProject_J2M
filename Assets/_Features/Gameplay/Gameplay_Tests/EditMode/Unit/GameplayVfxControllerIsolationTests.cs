using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxControllerIsolationTests
    {
        private static readonly string[] ProductionPresenterPaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTransientEffectPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayFrontFaceShieldVfxPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayUtilityWindupVfxPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactTrack.cs",
        };

        [Test]
        [Category("Extended")]
        public void ControllerExecution_DoesNotCreateSnapshots()
        {
            var controller = CreateController(new FakeVfxPool(), new FakeVfxAnchorResolver());
            var plan = new GameplayVfxRequestPlan(new[] { CreateRequest() });

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                controller.Refresh(plan);
                counts = capture.Counts;
            }

            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void ControllerPublicSurface_DoesNotExposeForbiddenTypes()
        {
            var forbiddenTypes = new HashSet<string>
            {
                typeof(WorldState).FullName,
                typeof(WorldSnapshot).FullName,
                typeof(TickPipeline).FullName,
                "UnityEngine.GameObject",
                "UnityEngine.MonoBehaviour",
                "UnityEngine.ParticleSystem",
            };
            var checkedTypes = new[]
            {
                typeof(GameplayVfxPresentationController),
                typeof(VfxPersistentHandleRegistry),
                typeof(VfxLifetimeRunner),
                typeof(IVfxPool),
                typeof(IVfxAnchorResolver),
            };

            foreach (var type in checkedTypes)
            {
                AssertPublicSurfaceDoesNotExposeForbiddenType(type, forbiddenTypes);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionCoordinatorAndTickViewPresenter_RemainUnconnected()
        {
            var coordinatorSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs");
            var tickViewPresenterSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs");

            Assert.That(coordinatorSource, Does.Not.Contain(nameof(GameplayVfxPresentationController)));
            Assert.That(coordinatorSource, Does.Not.Contain("Game.Feature.Gameplay.Vfx"));
            Assert.That(tickViewPresenterSource, Does.Not.Contain(nameof(GameplayVfxPresentationController)));
            Assert.That(tickViewPresenterSource, Does.Not.Contain("Game.Feature.Gameplay.Vfx"));
        }

        [Test]
        [Category("Extended")]
        public void ExistingPresenterFiles_DoNotReferenceGameplayVfxNamespace()
        {
            foreach (var path in ProductionPresenterPaths)
            {
                var source = ReadRepoFile(path);

                Assert.That(source, Does.Not.Contain("Game.Feature.Gameplay.Vfx"), path);
                Assert.That(source, Does.Not.Contain("GameplayVfx"), path);
            }
        }

        [Test]
        [Category("Extended")]
        public void MissingAnchorSkipOptional_SkipsPoolWithoutException()
        {
            var pool = new FakeVfxPool();
            var resolver = new FakeVfxAnchorResolver { ResolveSuccess = false };
            var controller = CreateController(pool, resolver);
            var request = CreateRequest(missingAnchorPolicy: VfxMissingAnchorPolicy.SkipOptional);

            Assert.DoesNotThrow(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { request })));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(0));
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(0));
            Assert.That(resolver.TryResolveCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void MissingAnchorFailFast_ThrowsAndSkipsPool()
        {
            var pool = new FakeVfxPool();
            var resolver = new FakeVfxAnchorResolver { ResolveSuccess = false };
            var controller = CreateController(pool, resolver);
            var request = CreateRequest(missingAnchorPolicy: VfxMissingAnchorPolicy.FailFast);

            Assert.Throws<InvalidOperationException>(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { request })));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(0));
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(0));
        }

        private static GameplayVfxPresentationController CreateController(
            FakeVfxPool pool,
            FakeVfxAnchorResolver resolver)
        {
            return new GameplayVfxPresentationController(
                pool,
                resolver,
                new VfxPersistentHandleRegistry(),
                new VfxLifetimeRunner());
        }

        private static GameplayVfxRequest CreateRequest(
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.SkipOptional)
        {
            return new GameplayVfxRequest(
                1,
                1,
                17,
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
                VfxAnchor.ForEntity(3),
                VfxTimingKind.ImmediateOnTickPresentation,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                missingAnchorPolicy: missingAnchorPolicy);
        }

        private static void AssertPublicSurfaceDoesNotExposeForbiddenType(Type type, HashSet<string> forbiddenTypes)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(ContainsForbiddenType(field.FieldType, forbiddenTypes), Is.False, $"{type.FullName}.{field.Name}");
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(ContainsForbiddenType(property.PropertyType, forbiddenTypes), Is.False, $"{type.FullName}.{property.Name}");
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                         .Where(method => !method.IsSpecialName))
            {
                Assert.That(ContainsForbiddenType(method.ReturnType, forbiddenTypes), Is.False, $"{type.FullName}.{method.Name} return");
                foreach (var parameter in method.GetParameters())
                {
                    Assert.That(ContainsForbiddenType(parameter.ParameterType, forbiddenTypes), Is.False, $"{type.FullName}.{method.Name} {parameter.Name}");
                }
            }
        }

        private static bool ContainsForbiddenType(Type type, HashSet<string> forbiddenTypes)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type != null && forbiddenTypes.Contains(type.FullName))
            {
                return true;
            }

            return type != null
                && type.IsGenericType
                && type.GetGenericArguments().Any(argument => ContainsForbiddenType(argument, forbiddenTypes));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(relativePath)).Replace("\r\n", "\n");
        }

        private sealed class FakeVfxPool : IVfxPool
        {
            public int PlayTransientCallCount { get; private set; }

            public int StartPersistentCallCount { get; private set; }

            public IVfxPlaybackHandle PlayTransient(in GameplayVfxRequest request, in VfxResolvedAnchor anchor)
            {
                PlayTransientCallCount++;
                return new FakeVfxPlaybackHandle(request);
            }

            public IVfxPlaybackHandle StartPersistent(in GameplayVfxRequest request, in VfxResolvedAnchor anchor)
            {
                StartPersistentCallCount++;
                return new FakeVfxPlaybackHandle(request);
            }

            public void Release(IVfxPlaybackHandle handle)
            {
            }

            public void HardCleanupAll()
            {
            }
        }

        private sealed class FakeVfxPlaybackHandle : IVfxPlaybackHandle
        {
            public FakeVfxPlaybackHandle(in GameplayVfxRequest request)
            {
                CueId = request.CueId;
                PersistentKey = request.PersistentKey;
                IsPersistent = request.IsPersistent;
                State = VfxLifetimeState.Active;
            }

            public int HandleId => 1;

            public GameplayVfxCueId CueId { get; }

            public VfxPersistentKey PersistentKey { get; }

            public bool IsPersistent { get; }

            public VfxLifetimeState State { get; private set; }

            public void MarkSpawned()
            {
                State = VfxLifetimeState.Spawned;
            }

            public void MarkActive()
            {
                State = VfxLifetimeState.Active;
            }

            public void StopEmitting()
            {
                State = VfxLifetimeState.StopEmitting;
            }

            public void Detach()
            {
                State = VfxLifetimeState.Detached;
            }

            public void MarkTailPlaying()
            {
                State = VfxLifetimeState.TailPlaying;
            }

            public void ReleaseToPool()
            {
                State = VfxLifetimeState.ReleasedToPool;
            }

            public void HardCleanup()
            {
                State = VfxLifetimeState.HardCleanup;
            }
        }

        private sealed class FakeVfxAnchorResolver : IVfxAnchorResolver
        {
            public bool ResolveSuccess { get; set; } = true;

            public int TryResolveCallCount { get; private set; }

            public bool TryResolve(in GameplayVfxRequest request, out VfxResolvedAnchor resolvedAnchor)
            {
                TryResolveCallCount++;
                if (!ResolveSuccess)
                {
                    resolvedAnchor = VfxResolvedAnchor.Unresolved(request.MissingAnchorPolicy);
                    return false;
                }

                resolvedAnchor = VfxResolvedAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter);
                return true;
            }
        }
    }
}
