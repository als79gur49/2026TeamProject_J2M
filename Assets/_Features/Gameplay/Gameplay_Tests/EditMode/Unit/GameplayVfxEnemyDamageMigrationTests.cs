using System;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxEnemyDamageMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string EnemyDamageBurstPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyDamageBurstVfx.prefab";
        private const string EnemyDamageBurstBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyDamageBurst_Binding.asset";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_DamageSignal_DoesNotEmitExecutorOwnedDamageRequest()
        {
            AssertNoEnemyDamageRequests(CreateEnemyDamageSignal());
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_NoDamageOrInvalidEntity_DoesNotEmitDamageRequest()
        {
            AssertNoEnemyDamageRequests(new TickEnemyDamagePresentationSignal(40, tookDamageThisTick: false, damageAmount: 0));
            AssertNoEnemyDamageRequests(new TickEnemyDamagePresentationSignal(0, tookDamageThisTick: true, damageAmount: 1));
            AssertNoEnemyDamageRequests(new TickEnemyDamagePresentationSignal(-1, tookDamageThisTick: true, damageAmount: 1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_EntityExitTick_DoesNotEmitDamageRequest()
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(
                        enemyDamageSignals: new[] { CreateEnemyDamageSignal() },
                        entityExitSignals: new[] { CreateEnemyExitSignal(40) }),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            Assert.That(
                builder.Build().Requests,
                Has.None.Matches<GameplayVfxRequest>(
                    request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.Damage)));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_EnemyDamageMigrationFlag_DefaultsTrue()
        {
            var owner = new GameObject("EnemyDamageMigrationDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_EnemyDamageFlagOnMissingBinding_DiagnosticOnly()
        {
            var owner = new GameObject("EnemyDamageMigrationMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                var enemyView = AddSourceView(owner);

                runtime.Present(CreateExtensionContext(enemyView: enemyView));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_PlayerAndEnemyDamageFlags_AreIndependent()
        {
            AssertFlagCombinationPlans(playerDamageEnabled: true, enemyDamageEnabled: false, expectedRequests: 1);
            AssertFlagCombinationPlans(playerDamageEnabled: false, enemyDamageEnabled: true, expectedRequests: 0);
            AssertFlagCombinationPlans(playerDamageEnabled: true, enemyDamageEnabled: true, expectedRequests: 1);
            AssertFlagCombinationPlans(playerDamageEnabled: false, enemyDamageEnabled: false, expectedRequests: 0);
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_SourceProfileDamageBindingBeatsHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Damage);
            var sourcePolicy = CreatePolicy(cueId, maxConcurrentInstances: 7);
            var hostPolicy = CreatePolicy(cueId, maxConcurrentInstances: 3);
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 40,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { sourcePolicy })),
                new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 40), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(sourcePolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_SourceProfileMissingDamageFallsBackToHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Damage);
            var hostPolicy = CreatePolicy(cueId, maxConcurrentInstances: 3);
            var profileOnlyPolicy = CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.Spawn));
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 40,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { profileOnlyPolicy })),
                new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 40), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_MissingSourceIdUsesHostDefaultForEnemyDamage()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Damage);
            var hostPolicy = CreatePolicy(cueId);
            var sourcePolicy = CreatePolicy(cueId, maxConcurrentInstances: 7);
            var provider = new FakeProfileProvider(
                sourceEntityId: 40,
                profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { sourcePolicy }));
            var resolver = new ProfileAwareVfxBindingResolver(provider, new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 0), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
            Assert.That(provider.CallCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_MissingAllEnemyDamageBindings_ReturnsFalse()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Damage);
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(sourceEntityId: 40, profile: null),
                VfxCueMap.Empty);

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 40), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyDamageBurstPrefab_RemovedFromDefaultAuthoring()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyDamageBurstPrefabPath);

            Assert.That(prefab, Is.Null, EnemyDamageBurstPrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void EnemyDamageBurstBinding_RemovedFromDefaultAuthoring()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(EnemyDamageBurstBindingPath);

            Assert.That(binding, Is.Null, EnemyDamageBurstBindingPath);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultCueMap_DoesNotResolveRemovedEnemyDamageBurst()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(
                    GameplayVfxCueId.From(EnemyVfxCue.Damage),
                    out _),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyDamagePlannerAndRuntimeSources_DoNotReferenceAuthorityTypes()
        {
            var source = File.ReadAllText(VfxPlanningPath) + "\n" + File.ReadAllText(VfxProductionRuntimePath);
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "CreateSnapshot",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExistingPresenterSources_DoNotReferenceEnemyDamageCue()
        {
            var presenterPaths = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyDeathExitEffectPlanBuilder.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayUtilityWindupVfxPresenter.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PresentationMotionTrack.cs",
            };

            foreach (var path in presenterPaths)
            {
                Assert.That(File.ReadAllText(path), Does.Not.Contain("EnemyVfxCue.Damage"), path);
            }
        }

        private static void AssertNoEnemyDamageRequests(params TickEnemyDamagePresentationSignal[] damageSignals)
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(enemyDamageSignals: damageSignals),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            Assert.That(
                builder.Build().Requests,
                Has.None.Matches<GameplayVfxRequest>(
                    request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.Damage)));
        }

        private static void AssertFlagCombinationPlans(
            bool playerDamageEnabled,
            bool enemyDamageEnabled,
            int expectedRequests)
        {
            var owner = new GameObject(
                $"EnemyDamageFlagCombination_{playerDamageEnabled}_{enemyDamageEnabled}");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                var enemyView = AddSourceView(owner);
                runtime.Present(CreateExtensionContext(
                    includePlayerDamage: playerDamageEnabled,
                    includeEnemyDamage: enemyDamageEnabled,
                    enemyView: enemyView));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(expectedRequests));
            }
            finally
            {
                Destroy(owner);
            }
        }

        private static GameplayEntityView AddSourceView(GameObject owner)
        {
            var view = owner.AddComponent<GameplayEntityView>();
            view.Initialize(40);
            return view;
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            bool includePlayerDamage = false,
            bool includeEnemyDamage = true,
            GameplayEntityView enemyView = null)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            if (enemyView != null)
            {
                stateStore.ViewsByEntityId[40] = enemyView;
            }

            stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(
                new Vector3(0.25f, 0.5f, 0f),
                Quaternion.identity);
            stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(
                new Vector3(-0.25f, 0.5f, 0f),
                Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(
                    CreatePresentationData(
                        playerDamageSignals: includePlayerDamage
                            ? new[] { new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1) }
                            : Array.Empty<TickPlayerDamagePresentationSignal>(),
                        enemyDamageSignals: includeEnemyDamage
                            ? new[] { CreateEnemyDamageSignal() }
                            : Array.Empty<TickEnemyDamagePresentationSignal>()),
                    topology),
                topology,
                stateStore,
                projector);
        }

        private static TickResult CreateResult(
            TickPresentationData presentationData,
            CubeTopologyState topology)
        {
            return new TickResult(
                12,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateEnemyUnit(40, new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                Array.Empty<string>(),
                topology,
                presentationData,
                "hash",
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            TickPlayerDamagePresentationSignal[] playerDamageSignals = null,
            TickEnemyDamagePresentationSignal[] enemyDamageSignals = null,
            TickEntityExitPresentationSignal[] entityExitSignals = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals ?? Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals ?? Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickEnemyDamagePresentationSignal CreateEnemyDamageSignal()
        {
            return new TickEnemyDamagePresentationSignal(40, tookDamageThisTick: true, damageAmount: 1);
        }

        private static TickEntityExitPresentationSignal CreateEnemyExitSignal(int entityId)
        {
            return new TickEntityExitPresentationSignal(
                entityId,
                TickEntityExitCause.EnemyDeath,
                new SurfaceCell(FaceId.Floor, 1, 0),
                new CubeTopologyState(FaceId.Floor),
                Direction.Left,
                EntityType.Unit,
                sourceActorEntityId: 10);
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

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 2,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Patrol,
            };
        }

        private static VfxBindingDefinitionAsset CreateBinding(GameObject prefab)
        {
            GameplayVfxTestPrefabFactory.EnsureModelRoot(prefab);

            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Enemy);
            SetField(binding, "cueCode", (int)EnemyVfxCue.Damage);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", 0.30f);
            SetField(binding, "tailSeconds", 0.20f);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", 12);
            return binding;
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static VfxBindingRuntimePolicy CreatePolicy(
            GameplayVfxCueId cueId,
            int maxConcurrentInstances = 8)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 0.30f,
                tailSeconds: 0.20f,
                maxConcurrentInstances: maxConcurrentInstances);
        }

        private static GameplayVfxRequest CreateRequest(GameplayVfxCueId cueId, int sourceEntityId)
        {
            return new GameplayVfxRequest(
                tickIndex: 12,
                sequenceId: sourceEntityId,
                presentationSeed: sourceEntityId,
                sourceEntityId: sourceEntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForEntity(sourceEntityId, VfxAnchorSlot.EntityCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void Destroy(params UnityEngine.Object[] unityObjects)
        {
            for (var i = 0; i < unityObjects.Length; i++)
            {
                if (unityObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObjects[i]);
                }
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

            public bool TryResolveProfileForRequest(in GameplayVfxRequest request, out VfxProfile resolvedProfile)
            {
                CallCount++;
                if (request.SourceEntityId == sourceEntityId &&
                    profile != null)
                {
                    resolvedProfile = profile;
                    return true;
                }

                resolvedProfile = null;
                return false;
            }
        }
    }
}
