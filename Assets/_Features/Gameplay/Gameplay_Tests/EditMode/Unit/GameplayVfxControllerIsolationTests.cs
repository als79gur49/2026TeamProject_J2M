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

        private static readonly string[] ProductionBindingBoundaryPaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs",
            "Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs",
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
                typeof(IVfxBindingResolver),
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
        public void ProductionBindingOwners_DoNotReferenceVfxAuthoringOrController()
        {
            foreach (var path in ProductionBindingBoundaryPaths)
            {
                var source = ReadRepoFile(path);

                Assert.That(source, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Authoring"), path);
                Assert.That(source, Does.Not.Contain("VfxBindingDefinitionAsset"), path);
                Assert.That(source, Does.Not.Contain("VfxCueMapAsset"), path);
                Assert.That(source, Does.Not.Contain("VfxProfileAsset"), path);
                Assert.That(source, Does.Not.Contain(nameof(GameplayVfxPresentationController)), path);
            }
        }

        [Test]
        [Category("Extended")]
        public void Controller_ResolvesBindingBeforeAnchor()
        {
            var callOrder = new List<string>();
            var pool = new FakeVfxPool();
            var anchorResolver = new FakeVfxAnchorResolver { CallOrder = callOrder };
            var bindingResolver = new FakeVfxBindingResolver { CallOrder = callOrder };
            var controller = CreateController(pool, anchorResolver, bindingResolver);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { CreateRequest() }));

            Assert.That(callOrder, Is.EqualTo(new[] { "binding", "anchor" }));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void MissingBinding_SkipsAnchorAndPool()
        {
            var pool = new FakeVfxPool();
            var anchorResolver = new FakeVfxAnchorResolver();
            var bindingResolver = new FakeVfxBindingResolver { ResolveSuccess = false };
            var controller = CreateController(pool, anchorResolver, bindingResolver);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { CreateRequest() }));

            Assert.That(controller.MissingBindingCount, Is.EqualTo(1));
            Assert.That(anchorResolver.TryResolveCallCount, Is.EqualTo(0));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(0));
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void MissingAnchorSkipOptional_SkipsPoolWithoutException()
        {
            var pool = new FakeVfxPool();
            var resolver = new FakeVfxAnchorResolver { ResolveSuccess = false };
            var bindingResolver = new FakeVfxBindingResolver
            {
                Policy = CreatePolicy(VfxMissingAnchorPolicy.SkipOptional),
            };
            var controller = CreateController(pool, resolver, bindingResolver);
            var request = CreateRequest();

            Assert.DoesNotThrow(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { request })));
            Assert.That(controller.MissingAnchorCount, Is.EqualTo(1));
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
            var bindingResolver = new FakeVfxBindingResolver
            {
                Policy = CreatePolicy(VfxMissingAnchorPolicy.FailFast),
            };
            var controller = CreateController(pool, resolver, bindingResolver);
            var request = CreateRequest();

            Assert.Throws<InvalidOperationException>(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { request })));
            Assert.That(controller.MissingAnchorCount, Is.EqualTo(1));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(0));
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(0));
        }

        private static GameplayVfxPresentationController CreateController(
            FakeVfxPool pool,
            FakeVfxAnchorResolver resolver,
            FakeVfxBindingResolver bindingResolver = null)
        {
            return new GameplayVfxPresentationController(
                pool,
                resolver,
                bindingResolver ?? new FakeVfxBindingResolver(),
                new VfxPersistentHandleRegistry(),
                new VfxLifetimeRunner());
        }

        private static GameplayVfxRequest CreateRequest()
        {
            return new GameplayVfxRequest(
                1,
                1,
                17,
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
                VfxAnchor.ForEntity(3),
                VfxTimingKind.ImmediateOnTickPresentation);
        }

        private static VfxBindingRuntimePolicy CreatePolicy(VfxMissingAnchorPolicy missingAnchorPolicy)
        {
            return new VfxBindingRuntimePolicy(
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
                VfxBindingRequirement.Optional,
                missingAnchorPolicy,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration);
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

            public IVfxPlaybackHandle PlayTransient(in ResolvedVfxPlaybackCommand command)
            {
                PlayTransientCallCount++;
                return new FakeVfxPlaybackHandle(command.Request);
            }

            public IVfxPlaybackHandle StartPersistent(in ResolvedVfxPlaybackCommand command)
            {
                StartPersistentCallCount++;
                return new FakeVfxPlaybackHandle(command.Request);
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

            public List<string> CallOrder { get; set; }

            public int TryResolveCallCount { get; private set; }

            public bool TryResolve(in GameplayVfxRequest request, out VfxResolvedAnchor resolvedAnchor)
            {
                TryResolveCallCount++;
                CallOrder?.Add("anchor");
                if (!ResolveSuccess)
                {
                    resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                    return false;
                }

                resolvedAnchor = VfxResolvedAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter);
                return true;
            }
        }

        private sealed class FakeVfxBindingResolver : IVfxBindingResolver
        {
            public bool ResolveSuccess { get; set; } = true;

            public VfxBindingRuntimePolicy? Policy { get; set; }

            public List<string> CallOrder { get; set; }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy policy)
            {
                CallOrder?.Add("binding");
                if (!ResolveSuccess)
                {
                    policy = default;
                    return false;
                }

                policy = Policy ?? new VfxBindingRuntimePolicy(
                    request.CueId,
                    VfxBindingRequirement.Optional,
                    VfxMissingAnchorPolicy.SkipOptional,
                    request.IsPersistent ? VfxPlaybackMode.Loop : VfxPlaybackMode.OneShot,
                    VfxStopPolicy.AuthoredDuration);
                return true;
            }
        }
    }
}
