using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class EnemyViewIsolationTests
    {
        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentingEnemyFrames_DoesNotChangeLaterTickAuthoritativeResults()
        {
            var initialEntities = new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            };
            var baselineWorld = CreateWorldState(initialEntities);
            var presentedWorld = CreateWorldState(initialEntities);
            var baselinePipeline = GameplayCompositionRoot.CreateTickPipeline(baselineWorld);
            var presentedPipeline = GameplayCompositionRoot.CreateTickPipeline(presentedWorld);
            var rootObject = new GameObject("EnemyViewIsolationTests_Presenter");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var initialSnapshot = presentedWorld.CreateSnapshot();

                presenter.Initialize(
                    binder,
                    initialSnapshot.BoardBounds,
                    initialSnapshot.Topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(initialEntities, initialSnapshot.Topology);

                var baselineFirstTick = baselinePipeline.RunTick(new TickInput(1));
                var presentedFirstTick = presentedPipeline.RunTick(new TickInput(1));

                presenter.Present(presentedFirstTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedFirstTick.DeterminismHash, Is.EqualTo(baselineFirstTick.DeterminismHash));

                var baselineSecondTick = baselinePipeline.RunTick(new TickInput(2));
                var presentedSecondTick = presentedPipeline.RunTick(new TickInput(2));

                presenter.Present(presentedSecondTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedSecondTick.DeterminismHash, Is.EqualTo(baselineSecondTick.DeterminismHash));
                CollectionAssert.AreEqual(
                    SummarizeEntities(baselineSecondTick.FinalEntities),
                    SummarizeEntities(presentedSecondTick.FinalEntities));
                CollectionAssert.AreEqual(
                    baselineSecondTick.EventLog.ToArray(),
                    presentedSecondTick.EventLog.ToArray());

                var baselineSnapshotAfter = baselineWorld.CreateSnapshot();
                var presentedSnapshotAfter = presentedWorld.CreateSnapshot();
                Assert.That(baselineSnapshotAfter.TryGetEntity(40, out var baselineEnemy), Is.True);
                Assert.That(presentedSnapshotAfter.TryGetEntity(40, out var presentedEnemy), Is.True);
                Assert.That(presentedEnemy.position, Is.EqualTo(baselineEnemy.position));
                Assert.That(presentedEnemy.aiMode, Is.EqualTo(baselineEnemy.aiMode));
                Assert.That(presentedEnemy.aiStateTimer, Is.EqualTo(baselineEnemy.aiStateTimer));
                Assert.That(registry.TryGetView(40, out var enemyView), Is.True);
                Assert.That(enemyView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentingEnemyWindupSignals_DoesNotChangeLaterTickAuthoritativeResults()
        {
            var initialEntities = new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            };
            var baselineWorld = CreateWorldState(initialEntities);
            var presentedWorld = CreateWorldState(initialEntities);
            var baselinePipeline = GameplayCompositionRoot.CreateTickPipeline(baselineWorld, CreateEnemyProfile(windupTicks: 1));
            var presentedPipeline = GameplayCompositionRoot.CreateTickPipeline(presentedWorld, CreateEnemyProfile(windupTicks: 1));
            var rootObject = new GameObject("EnemyViewIsolationTests_Presenter_Windup");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var initialSnapshot = presentedWorld.CreateSnapshot();

                presenter.Initialize(
                    binder,
                    initialSnapshot.BoardBounds,
                    initialSnapshot.Topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(initialEntities, initialSnapshot.Topology);

                var baselineWindupTick = baselinePipeline.RunTick(new TickInput(1));
                var presentedWindupTick = presentedPipeline.RunTick(new TickInput(1));

                Assert.That(presentedWindupTick.PresentationData.EnemyActionSignals.Single().StartedThisTick, Is.True);

                presenter.Present(presentedWindupTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedWindupTick.DeterminismHash, Is.EqualTo(baselineWindupTick.DeterminismHash));

                var baselineExecuteTick = baselinePipeline.RunTick(new TickInput(2));
                var presentedExecuteTick = presentedPipeline.RunTick(new TickInput(2));

                Assert.That(presentedExecuteTick.PresentationData.EnemyActionSignals.Single().ExecutedThisTick, Is.True);
                Assert.That(presentedExecuteTick.PresentationData.EnemyActionSignals.Single().StartedRecoveryThisTick, Is.True);

                presenter.Present(presentedExecuteTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedExecuteTick.DeterminismHash, Is.EqualTo(baselineExecuteTick.DeterminismHash));
                CollectionAssert.AreEqual(
                    SummarizeEntities(baselineExecuteTick.FinalEntities),
                    SummarizeEntities(presentedExecuteTick.FinalEntities));
                CollectionAssert.AreEqual(
                    baselineExecuteTick.EventLog.ToArray(),
                    presentedExecuteTick.EventLog.ToArray());

                var presentedSnapshotAfter = presentedWorld.CreateSnapshot();
                Assert.That(presentedSnapshotAfter.TryGetEntity(40, out var presentedEnemy), Is.True);
                Assert.That(presentedEnemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(presentedEnemy.aiStateTimer, Is.EqualTo(1));
                Assert.That(registry.TryGetView(40, out var enemyView), Is.True);

                var driver = enemyView.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.AttackSignalCount, Is.EqualTo(1));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentingEnemyMotionAuthoring_DoesNotChangeLocomotionCooldownAuthority()
        {
            var initialEntities = new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(4, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            };
            var baselineWorld = CreateWorldState(initialEntities);
            var presentedWorld = CreateWorldState(initialEntities);
            var baselineProfile = CreateChargingEnemyProfile(moveCooldownTicks: 2);
            var presentedProfile = CreateChargingEnemyProfile(moveCooldownTicks: 2);
            var baselinePipeline = GameplayCompositionRoot.CreateTickPipeline(baselineWorld, baselineProfile);
            var presentedPipeline = GameplayCompositionRoot.CreateTickPipeline(presentedWorld, presentedProfile);
            var rootObject = new GameObject("EnemyViewIsolationTests_Presenter_MotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(
                        registry.transform,
                        attachEnemyAnimatorDriver: true,
                        enemyMoveOverrideSeconds: 0.6f));
                var initialSnapshot = presentedWorld.CreateSnapshot();

                presenter.Initialize(
                    binder,
                    initialSnapshot.BoardBounds,
                    initialSnapshot.Topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(initialEntities, initialSnapshot.Topology);

                var baselineFirstTick = baselinePipeline.RunTick(new TickInput(1));
                var presentedFirstTick = presentedPipeline.RunTick(new TickInput(1));

                presenter.Present(presentedFirstTick);
                presenter.UpdatePresentation(0.2f);

                var baselineSecondTick = baselinePipeline.RunTick(new TickInput(2));
                var presentedSecondTick = presentedPipeline.RunTick(new TickInput(2));

                presenter.Present(presentedSecondTick);
                presenter.UpdatePresentation(0.2f);

                CollectionAssert.AreEqual(
                    SummarizeEntities(baselineSecondTick.FinalEntities),
                    SummarizeEntities(presentedSecondTick.FinalEntities));
                CollectionAssert.AreEqual(
                    baselineSecondTick.EventLog.ToArray(),
                    presentedSecondTick.EventLog.ToArray());

                var baselineSnapshotAfter = baselineWorld.CreateSnapshot();
                var presentedSnapshotAfter = presentedWorld.CreateSnapshot();
                Assert.That(baselineSnapshotAfter.TryGetEntity(40, out var baselineEnemy), Is.True);
                Assert.That(presentedSnapshotAfter.TryGetEntity(40, out var presentedEnemy), Is.True);
                Assert.That(presentedEnemy.position, Is.EqualTo(baselineEnemy.position));
                Assert.That(
                    presentedEnemy.enemyLocomotionCooldownTicks,
                    Is.EqualTo(baselineEnemy.enemyLocomotionCooldownTicks));
                Assert.That(registry.TryGetView(40, out var enemyView), Is.True);
                Assert.That(enemyView.GetComponent<UnitLocomotionPresentationAuthoring>(), Is.Not.Null);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(baselineProfile);
                EnemyAiProfileTestFactory.Destroy(presentedProfile);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentingSummonedEnemyArchetypeViews_DoesNotChangeLaterTickAuthoritativeResults()
        {
            var initialEntities = new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            };
            var baselineWorld = CreateWorldState(initialEntities);
            var presentedWorld = CreateWorldState(initialEntities);
            var defaultProfile = CreateUtilityProfile();
            var archetypeProfile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Forward,
            });
            var archetype = CreateEnemyUnitArchetypeAsset("BasicMinion", archetypeProfile, hp: 4, initialAiMode: EnemyAiMode.Patrol);
            var archetypeCatalog = CreateEnemyUnitArchetypeCatalog(archetype);
            var summonerProfile = CreateUtilitySummonProfile(initialDelayTicks: 0, cooldownTicks: 10, summonedArchetype: archetype);
            var baselinePipeline = CreateArchetypeBootstrapper(defaultProfile, summonerProfile, archetypeCatalog).CreateTickPipeline(baselineWorld);
            var presentedPipeline = CreateArchetypeBootstrapper(defaultProfile, summonerProfile, archetypeCatalog).CreateTickPipeline(presentedWorld);
            var rootObject = new GameObject("EnemyViewIsolationTests_SummonedPresenter");
            var prefabObject = new GameObject("EnemyViewIsolationTests_SummonedPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();
                prefabObject.AddComponent<EnemyAnimatorDriver>();
                new GameObject("SummonedMarker").transform.SetParent(prefabObject.transform, worldPositionStays: false);
                var presentationRegistry = new EnemyPresentationArchetypeRegistry(
                    new Dictionary<EnemyUnitArchetypeId, EnemyPresentationArchetypeRuntime>(EnemyUnitArchetypeId.EqualityComparer)
                    {
                        { new EnemyUnitArchetypeId("BasicMinion"), new EnemyPresentationArchetypeRuntime(new EnemyUnitArchetypeId("BasicMinion"), prefabView) },
                    });
                var initialSnapshot = presentedWorld.CreateSnapshot();

                presenter.Initialize(
                    binder,
                    initialSnapshot.BoardBounds,
                    initialSnapshot.Topology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    enemyPresentationArchetypeRegistry: presentationRegistry);
                presenter.PresentInitial(initialEntities, initialSnapshot.Topology);

                var baselineFirstTick = baselinePipeline.RunTick(new TickInput(1));
                var presentedFirstTick = presentedPipeline.RunTick(new TickInput(1));

                presenter.Present(presentedFirstTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedFirstTick.DeterminismHash, Is.EqualTo(baselineFirstTick.DeterminismHash));
                Assert.That(registry.TryGetView(41, out _), Is.False);

                var baselineSecondTick = baselinePipeline.RunTick(new TickInput(2));
                var presentedSecondTick = presentedPipeline.RunTick(new TickInput(2));

                presenter.Present(presentedSecondTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedSecondTick.DeterminismHash, Is.EqualTo(baselineSecondTick.DeterminismHash));
                Assert.That(registry.TryGetView(41, out var spawnedView), Is.True);
                Assert.That(spawnedView.transform.Find("SummonedMarker"), Is.Not.Null);

                var baselineThirdTick = baselinePipeline.RunTick(new TickInput(3));
                var presentedThirdTick = presentedPipeline.RunTick(new TickInput(3));

                presenter.Present(presentedThirdTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedThirdTick.DeterminismHash, Is.EqualTo(baselineThirdTick.DeterminismHash));
                CollectionAssert.AreEqual(
                    SummarizeEntities(baselineThirdTick.FinalEntities),
                    SummarizeEntities(presentedThirdTick.FinalEntities));
                CollectionAssert.AreEqual(
                    baselineThirdTick.EventLog.ToArray(),
                    presentedThirdTick.EventLog.ToArray());

                var baselineSnapshotAfter = baselineWorld.CreateSnapshot();
                var presentedSnapshotAfter = presentedWorld.CreateSnapshot();
                Assert.That(
                    baselineSnapshotAfter.TryGetEnemyDefinitionBindingState(41, out var baselineBindingState),
                    Is.True);
                Assert.That(presentedSnapshotAfter.TryGetEnemyDefinitionBindingState(41, out var bindingState), Is.True);
                Assert.That(bindingState.ArchetypeId, Is.EqualTo(baselineBindingState.ArchetypeId));
                Assert.That(bindingState.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("BasicMinion")));
                Assert.That(baselineSnapshotAfter.TryGetEntity(41, out var baselineChild), Is.True);
                Assert.That(presentedSnapshotAfter.TryGetEntity(41, out var child), Is.True);
                Assert.That(child.position, Is.EqualTo(baselineChild.position));
                Assert.That(child.aiMode, Is.EqualTo(baselineChild.aiMode));
                Assert.That(child.enemyLocomotionCooldownTicks, Is.EqualTo(baselineChild.enemyLocomotionCooldownTicks));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(archetypeCatalog);
                UnityEngine.Object.DestroyImmediate(archetype);
                EnemyAiProfileTestFactory.Destroy(archetypeProfile);
                EnemyAiProfileTestFactory.Destroy(summonerProfile);
                EnemyAiProfileTestFactory.Destroy(defaultProfile);
            }
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static GameplayBootstrapper CreateArchetypeBootstrapper(
            EnemyAiProfile defaultProfile,
            EnemyAiProfile summonerProfile,
            EnemyUnitArchetypeCatalog archetypeCatalog)
        {
            var runtimeSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = defaultProfile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = 40,
                        Profile = summonerProfile,
                    },
                },
                EnemyUnitArchetypeCatalog = archetypeCatalog,
            }.CreateEnemyAiRuntimeSnapshot();

            return new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    runtimeSnapshot.DefaultDefinition,
                    runtimeSnapshot.DefinitionsByEntityId,
                    runtimeSnapshot.DefinitionsByArchetypeId),
                runtimeSnapshot.SpawnDefaultsByArchetypeId);
        }

        private static (int EntityId, SurfaceCell Position, int Hp, EnemyAiMode AiMode, int AiTimer)[] SummarizeEntities(IReadOnlyList<EntityState> entities)
        {
            return entities
                .Select(entity => (entity.entityId, entity.position, entity.hp, entity.aiMode, entity.aiStateTimer))
                .ToArray();
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            int hp,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right)
        {
            var unitRole = teamId switch
            {
                1 => UnitRole.Player,
                2 => UnitRole.Enemy,
                _ => UnitRole.None,
            };

            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
                aiStateTimer = 0,
            };
        }

        private static EnemyAiProfile CreateEnemyProfile(int windupTicks)
        {
            return EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(windupTicks: windupTicks);
        }

        private static EnemyAiProfile CreateChargingEnemyProfile(int moveCooldownTicks)
        {
            return EnemyAiProfileTestFactory.CreateCharging(moveCooldownTicks);
        }

        private static EnemyAiProfile CreateUtilityProfile()
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
            });
        }

        private static EnemyAiProfile CreateUtilitySummonProfile(
            int initialDelayTicks,
            int cooldownTicks,
            EnemyUnitArchetypeAsset summonedArchetype)
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                DetectionStrategyKind = DetectionStrategyKind.None,
                PatrolStrategyKind = PatrolStrategyKind.Stationary,
                UtilityEffects = new[]
                {
                    CreateSummonUtilityEffect(initialDelayTicks, cooldownTicks, summonedArchetype),
                },
            });
        }

        private static EnemyUtilityEffectAuthoring CreateSummonUtilityEffect(
            int initialDelayTicks,
            int cooldownTicks,
            EnemyUnitArchetypeAsset summonedArchetype)
        {
            var summon = new SummonMinionAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(summon, "spawnCountPerTrigger", 1);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "maxAliveChildren", 3);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "candidatePattern", SummonCandidatePattern.OrthogonalAdjacent4);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoUnitAtSpawnCell", true);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "requireNoSolidAtSpawnCell", true);
            EnemyAiProfileTestFactory.SetSerializedField(summon, "summonedArchetype", summonedArchetype);
            EnemyAiProfileTestFactory.SetSerializedField(
                summon,
                "windupSeconds",
                1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", EnemyUtilityEffectKind.SummonMinion);
            EnemyAiProfileTestFactory.SetSerializedField(
                effect,
                "initialDelaySeconds",
                initialDelayTicks / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            EnemyAiProfileTestFactory.SetSerializedField(
                effect,
                "cooldownSeconds",
                cooldownTicks / (float)GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            EnemyAiProfileTestFactory.SetSerializedField(effect, "summon", summon);
            return effect;
        }

        private static EnemyUnitArchetypeAsset CreateEnemyUnitArchetypeAsset(
            string archetypeId,
            EnemyAiProfile profile,
            int hp,
            EnemyAiMode initialAiMode)
        {
            var asset = ScriptableObject.CreateInstance<EnemyUnitArchetypeAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(asset, "archetypeId", new EnemyUnitArchetypeId(archetypeId));
            EnemyAiProfileTestFactory.SetSerializedField(asset, "aiProfile", profile);
            EnemyAiProfileTestFactory.SetSerializedField(asset, "spawnDefaults", CreateEnemyUnitSpawnDefaults(hp, initialAiMode));
            return asset;
        }

        private static EnemyUnitArchetypeCatalog CreateEnemyUnitArchetypeCatalog(params EnemyUnitArchetypeAsset[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyUnitArchetypeCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            EnemyAiProfileTestFactory.SetSerializedField(catalog, "entries", entries ?? Array.Empty<EnemyUnitArchetypeAsset>());
            return catalog;
        }

        private static EnemyUnitSpawnDefaults CreateEnemyUnitSpawnDefaults(int hp, EnemyAiMode initialAiMode)
        {
            object boxed = EnemyUnitSpawnDefaults.CreateDefault();
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "hp", hp);
            EnemyAiProfileTestFactory.SetSerializedField(boxed, "initialAiMode", initialAiMode);
            return (EnemyUnitSpawnDefaults)boxed;
        }

        private sealed class TestViewFactory : global::Game.Feature.Gameplay.Host.IGameplayEntityViewFactory
        {
            private readonly bool _attachEnemyAnimatorDriver;
            private readonly float? _enemyMoveOverrideSeconds;
            private readonly Transform _parent;

            public TestViewFactory(
                Transform parent,
                bool attachEnemyAnimatorDriver,
                float? enemyMoveOverrideSeconds = null)
            {
                _parent = parent;
                _attachEnemyAnimatorDriver = attachEnemyAnimatorDriver;
                _enemyMoveOverrideSeconds = enemyMoveOverrideSeconds;
            }

            public global::Game.Feature.Gameplay.Host.GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);

                var view = viewObject.AddComponent<global::Game.Feature.Gameplay.Host.GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (_attachEnemyAnimatorDriver &&
                    EntityRolePolicy.IsEnemyUnit(entity))
                {
                    viewObject.AddComponent<EnemyAnimatorDriver>();

                    if (_enemyMoveOverrideSeconds.HasValue)
                    {
                        var authoring = viewObject.AddComponent<UnitLocomotionPresentationAuthoring>();
                        SetSerializedField(
                            authoring,
                            "moveMotionDurationSeconds",
                            _enemyMoveOverrideSeconds.Value);
                    }
                }

                return view;
            }

            private static void SetSerializedField(object target, string fieldName, object value)
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
                field.SetValue(target, value);
            }
        }
    }
}
