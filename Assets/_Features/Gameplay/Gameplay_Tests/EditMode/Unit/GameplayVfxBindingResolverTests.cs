using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxBindingResolverTests
    {
        [Test]
        [Category("Extended")]
        public void BindingResolver_UsesFamilyProfileBeforeHostDefault()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.Damage);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.SkipOptional);
            var profilePolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.FailFast);
            var resolver = new CompositeVfxBindingResolver(
                new VfxCueMap(new[] { hostPolicy }),
                new Dictionary<GameplayVfxFamily, VfxProfile>
                {
                    { GameplayVfxFamily.Player, new VfxProfile(GameplayVfxFamily.Player, new[] { profilePolicy }) },
                });

            Assert.That(resolver.TryResolve(CreateRequest(cueId), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(profilePolicy));
        }

        [Test]
        [Category("Extended")]
        public void BindingResolver_FallsBackToHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Spawn);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic);
            var resolver = new CompositeVfxBindingResolver(
                new VfxCueMap(new[] { hostPolicy }),
                new Dictionary<GameplayVfxFamily, VfxProfile>());

            Assert.That(resolver.TryResolve(CreateRequest(cueId), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_ProfileBeatsHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.SkipOptional);
            var profilePolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.FailFast);
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 10,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { profilePolicy })),
                new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 10), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(profilePolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_SourceProfileBeatsFamilyProfile()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var sourcePolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.FailFast, maxConcurrentInstances: 7);
            var familyPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic, maxConcurrentInstances: 3);
            var fallback = new CompositeVfxBindingResolver(
                VfxCueMap.Empty,
                new Dictionary<GameplayVfxFamily, VfxProfile>
                {
                    { GameplayVfxFamily.Enemy, new VfxProfile(GameplayVfxFamily.Enemy, new[] { familyPolicy }) },
                });
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 10,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { sourcePolicy })),
                fallback);

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 10), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(sourcePolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_MissingProfileFallsBackToHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic);
            var provider = new FakeProfileProvider(sourceEntityId: 10, profile: null);
            var resolver = new ProfileAwareVfxBindingResolver(provider, new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 10), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
            Assert.That(provider.CallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_MissingProfileFallsBackToFamilyProfile()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var familyPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic, maxConcurrentInstances: 4);
            var fallback = new CompositeVfxBindingResolver(
                VfxCueMap.Empty,
                new Dictionary<GameplayVfxFamily, VfxProfile>
                {
                    { GameplayVfxFamily.Enemy, new VfxProfile(GameplayVfxFamily.Enemy, new[] { familyPolicy }) },
                });
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(sourceEntityId: 99, profile: null),
                fallback);

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 10), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(familyPolicy));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPresentationVfxProfileProvider_BuildsEntityMapFromCatalogProfile()
        {
            var prefab = new GameObject("CatalogProfileVfxPrefab");
            var binding = CreateBindingAsset(
                GameplayVfxFamily.Enemy,
                (int)EnemyVfxCue.JumperLandingTarget,
                prefab,
                maxConcurrentInstances: 8);
            var profile = CreateProfileAsset(GameplayVfxFamily.Enemy, binding);
            var catalog = CreateEnemyCatalog("jumper", profile);

            try
            {
                var provider = EnemyPresentationVfxProfileMapBuilder.Build(
                    catalog,
                    new[] { new EnemyPresentationBinding { EntityId = 40, PresentationId = "jumper" } },
                    "test");

                Assert.That(provider.Count, Is.EqualTo(1));
                Assert.That(provider.TryResolveProfileForRequest(
                    CreateRequest(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget), sourceEntityId: 40),
                    out var runtimeProfile), Is.True);
                Assert.That(runtimeProfile.TryResolve(
                    GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget),
                    out var policy), Is.True);
                Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(8));
                Assert.That(provider.TryResolveProfileAssetForSourceEntity(40, out var profileAsset), Is.True);
                Assert.That(profileAsset, Is.SameAs(profile));
            }
            finally
            {
                Destroy(catalog, profile, binding, prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPresentationVfxProfileProvider_NullCatalogProfileReturnsNoSourceProfile()
        {
            var catalog = CreateEnemyCatalog("jumper", null);

            try
            {
                var provider = EnemyPresentationVfxProfileMapBuilder.Build(
                    catalog,
                    new[] { new EnemyPresentationBinding { EntityId = 40, PresentationId = "jumper" } },
                    "test");

                Assert.That(provider.Count, Is.Zero);
                Assert.That(provider.TryResolveProfileForRequest(
                    CreateRequest(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget), sourceEntityId: 40),
                    out _), Is.False);
            }
            finally
            {
                Destroy(catalog);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_SourceEntityIdMissing_UsesHostDefaultOnly()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic);
            var provider = new FakeProfileProvider(
                sourceEntityId: 10,
                profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { CreatePolicy(cueId, VfxMissingAnchorPolicy.FailFast) }));
            var resolver = new ProfileAwareVfxBindingResolver(provider, new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 0), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
            Assert.That(provider.CallCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_ProfileWithoutCueFallsBackToHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic);
            var profileOnlyPolicy = CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.Spawn), VfxMissingAnchorPolicy.FailFast);
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 10,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { profileOnlyPolicy })),
                new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 10), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_NoProfileNoHostDefault_ReturnsFalse()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(sourceEntityId: 10, profile: null),
                VfxCueMap.Empty);

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 10), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Controller_RejectsPersistentRequestWithOneShotPolicy()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Spawn);
            var request = CreateRequest(
                cueId,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(cueId, VfxAnchorKind.Entity, entityId: 3));
            var controller = CreateController(CreatePolicy(cueId, playbackMode: VfxPlaybackMode.OneShot));

            Assert.Throws<InvalidOperationException>(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { request })));
            Assert.That(controller.CompatibilityFailureCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Controller_RejectsTransientRequestWithLoopPolicy()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.Damage);
            var controller = CreateController(CreatePolicy(cueId, playbackMode: VfxPlaybackMode.Loop));

            Assert.Throws<InvalidOperationException>(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { CreateRequest(cueId) })));
            Assert.That(controller.CompatibilityFailureCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Controller_RejectsPersistentRequestWithoutKey()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Spawn);
            var request = CreateRequest(cueId, isPersistent: true);
            var controller = CreateController(CreatePolicy(cueId, playbackMode: VfxPlaybackMode.Loop));

            Assert.Throws<InvalidOperationException>(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { request })));
            Assert.That(controller.CompatibilityFailureCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Controller_UsesFallbackCellWhenBindingPolicyAllowsIt()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.Damage);
            var fallbackCell = new SurfaceCell(FaceId.Floor, 2, 3);
            var request = new GameplayVfxRequest(
                1,
                1,
                17,
                cueId,
                VfxAnchor.ForEntity(
                    3,
                    VfxAnchorSlot.EntityCenter,
                    fallbackCell,
                    new CubeTopologyState(FaceId.Floor),
                    hasFallbackCell: true),
                VfxTimingKind.ImmediateOnTickPresentation);
            var pool = new FakeVfxPool();
            var controller = CreateController(
                CreatePolicy(cueId, missingAnchorPolicy: VfxMissingAnchorPolicy.UseFallbackCell),
                pool,
                new FakeVfxAnchorResolver { ResolveSuccess = false });

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(controller.MissingAnchorCount, Is.EqualTo(1));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(1));
            Assert.That(pool.LastCommand.Anchor.UsedFallback, Is.True);
            Assert.That(pool.LastCommand.Anchor.Cell, Is.EqualTo(fallbackCell));
        }

        private static GameplayVfxPresentationController CreateController(
            VfxBindingRuntimePolicy policy,
            FakeVfxPool pool = null,
            FakeVfxAnchorResolver anchorResolver = null)
        {
            return new GameplayVfxPresentationController(
                pool ?? new FakeVfxPool(),
                anchorResolver ?? new FakeVfxAnchorResolver(),
                new SinglePolicyResolver(policy),
                new VfxPersistentHandleRegistry(),
                new VfxLifetimeRunner());
        }

        private static GameplayVfxRequest CreateRequest(
            GameplayVfxCueId cueId,
            int sourceEntityId = 0,
            bool isPersistent = false,
            VfxPersistentKey persistentKey = default)
        {
            return new GameplayVfxRequest(
                1,
                1,
                17,
                sourceEntityId,
                cueId,
                VfxAnchor.ForEntity(3),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent,
                persistentKey);
        }

        private static VfxBindingRuntimePolicy CreatePolicy(
            GameplayVfxCueId cueId,
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.SkipOptional,
            VfxPlaybackMode playbackMode = VfxPlaybackMode.OneShot,
            int maxConcurrentInstances = 0)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Optional,
                missingAnchorPolicy,
                playbackMode,
                VfxStopPolicy.AuthoredDuration,
                maxConcurrentInstances: maxConcurrentInstances);
        }

        private static EnemyPresentationCatalog CreateEnemyCatalog(
            string presentationId,
            VfxProfileAsset profile)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyPresentationCatalog>();
            SetField(catalog, "entries", new[]
            {
                new EnemyPresentationCatalogEntry
                {
                    PresentationId = presentationId,
                    VfxProfileAsset = profile,
                },
            });
            return catalog;
        }

        private static VfxProfileAsset CreateProfileAsset(
            GameplayVfxFamily family,
            params VfxBindingDefinitionAsset[] bindings)
        {
            var profile = ScriptableObject.CreateInstance<VfxProfileAsset>();
            profile.name = $"TestVfxProfile_{family}";
            SetField(profile, "family", family);
            SetField(profile, "bindings", bindings);
            return profile;
        }

        private static VfxBindingDefinitionAsset CreateBindingAsset(
            GameplayVfxFamily family,
            int cueCode,
            GameObject prefab,
            int maxConcurrentInstances = 0)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            binding.name = $"TestVfxBinding_{family}_{cueCode}";
            new GameObject(VfxPrefabValidationDiagnostics.ModelRootName).transform.SetParent(prefab.transform, worldPositionStays: false);
            SetField(binding, "family", family);
            SetField(binding, "cueCode", cueCode);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.Optional);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.SkipOptional);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "maxConcurrentInstances", maxConcurrentInstances);
            return binding;
        }

        private static void SetField<T>(T target, string fieldName, object value)
        {
            typeof(T)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static void Destroy(params UnityEngine.Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private sealed class SinglePolicyResolver : IVfxBindingResolver
        {
            private readonly VfxBindingRuntimePolicy policy;

            public SinglePolicyResolver(VfxBindingRuntimePolicy policy)
            {
                this.policy = policy;
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                resolvedPolicy = policy;
                return true;
            }
        }

        private sealed class FakeProfileProvider : IGameplayVfxProfileProvider
        {
            private readonly int sourceEntityId;
            private readonly VfxProfile profile;

            public FakeProfileProvider(int sourceEntityId, VfxProfile profile)
            {
                this.sourceEntityId = sourceEntityId;
                this.profile = profile;
            }

            public int CallCount { get; private set; }

            public bool TryResolveProfileForRequest(
                in GameplayVfxRequest request,
                out VfxProfile resolvedProfile)
            {
                CallCount++;
                if (request.SourceEntityId == sourceEntityId && profile != null)
                {
                    resolvedProfile = profile;
                    return true;
                }

                resolvedProfile = null;
                return false;
            }
        }

        private sealed class FakeVfxAnchorResolver : IVfxAnchorResolver
        {
            public bool ResolveSuccess { get; set; } = true;

            public bool TryResolve(
                in GameplayVfxRequest request,
                VfxBindingRuntimePolicy policy,
                out VfxResolvedAnchor resolvedAnchor)
            {
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

        private sealed class FakeVfxPool : IVfxPool
        {
            public int PlayTransientCallCount { get; private set; }

            public ResolvedVfxPlaybackCommand LastCommand { get; private set; }

            public IVfxPlaybackHandle PlayTransient(in ResolvedVfxPlaybackCommand command)
            {
                PlayTransientCallCount++;
                LastCommand = command;
                return null;
            }

            public IVfxPlaybackHandle StartPersistent(in ResolvedVfxPlaybackCommand command)
            {
                LastCommand = command;
                return null;
            }

            public void Release(IVfxPlaybackHandle handle)
            {
            }

            public void HardClearActiveForTopologyTransition()
            {
            }

            public void HardCleanupAll()
            {
            }

            public void HardCleanupFamily(GameplayVfxFamily family)
            {
            }
        }
    }
}
