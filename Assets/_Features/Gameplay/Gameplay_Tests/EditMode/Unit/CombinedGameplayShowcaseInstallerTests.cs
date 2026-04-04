using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class CombinedGameplayShowcaseInstallerTests
    {
        private const string CombinedStageAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Stage_CombinedGameplayShowcase.asset";

        [Test]
        public void CombinedGameplayStage_PopulatesSharedEdgeOpeningsAcrossAllFourFaces()
        {
            var buildResult = BuildCombinedStage();
            var boardBounds = buildResult.BoardBounds;
            var entities = buildResult.InitialEntities;
            var sharedEdgeOpeningColumns = new[] { 1, 3, 7, 9 };

            foreach (var face in new[] { FaceId.Floor, FaceId.Front, FaceId.Ceiling, FaceId.Back })
            {
                for (var i = 0; i < sharedEdgeOpeningColumns.Length; i++)
                {
                    var column = sharedEdgeOpeningColumns[i];
                    Assert.That(
                        HasWallAt(entities, new SurfaceCell(face, column, boardBounds.MinInclusive.y)),
                        Is.False,
                        $"{face} bottom shared-edge opening at x={column} should remain open.");
                    Assert.That(
                        HasWallAt(entities, new SurfaceCell(face, column, boardBounds.MaxInclusive.y)),
                        Is.False,
                        $"{face} top shared-edge opening at x={column} should remain open.");
                }
            }

            Assert.That(
                HasWallAt(entities, new SurfaceCell(FaceId.Floor, 2, boardBounds.MaxInclusive.y)),
                Is.True,
                "Columns between the traversal lane and left box lane should stay walled.");
            Assert.That(
                HasWallAt(entities, new SurfaceCell(FaceId.Floor, 4, boardBounds.MaxInclusive.y)),
                Is.True,
                "Columns outside the designated shared-edge cutouts should stay walled.");
            Assert.That(
                HasWallAt(entities, new SurfaceCell(FaceId.Floor, 10, boardBounds.MinInclusive.y)),
                Is.True,
                "Only the configured box lanes and traversal lanes should open shared edges.");
        }

        [Test]
        public void CombinedGameplayStage_PlacesPushableBoxesOnEveryRotatingFace()
        {
            var buildResult = BuildCombinedStage();
            var boardBounds = buildResult.BoardBounds;
            var entities = buildResult.InitialEntities;

            Assert.That(
                HasPushableBoxAt(entities, new SurfaceCell(FaceId.Floor, 9, 6)),
                Is.True,
                "Floor should retain the elevated push box in the expanded layout.");
            Assert.That(
                HasPushableBoxAt(entities, new SurfaceCell(FaceId.Front, 3, 1)),
                Is.True,
                "Front should start with a pushable box near a shared edge opening.");
            Assert.That(
                HasPushableBoxAt(entities, new SurfaceCell(FaceId.Ceiling, 7, 1)),
                Is.True,
                "Ceiling should start with a pushable box near a shared edge opening.");
            Assert.That(
                HasPushableBoxAt(entities, new SurfaceCell(FaceId.Back, 3, 5)),
                Is.True,
                "Back should start with a pushable box near a shared edge opening.");
        }

        [Test]
        public void CombinedGameplayStage_PlacesThreeEnemyVariantsForAiShowcase()
        {
            var buildResult = BuildCombinedStage();
            var entities = buildResult.InitialEntities;

            Assert.That(
                TryGetUnitAt(entities, new SurfaceCell(FaceId.Floor, 1, 5), out var floorEnemy),
                Is.True,
                "Floor face should include an immediately reachable charge enemy.");
            Assert.That(floorEnemy.teamId, Is.EqualTo(2));
            Assert.That(floorEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));

            Assert.That(
                TryGetUnitAt(entities, new SurfaceCell(FaceId.Front, 2, 2), out var frontEnemy),
                Is.True,
                "Front face should include a non-attacking scout after a surface transition.");
            Assert.That(frontEnemy.teamId, Is.EqualTo(2));
            Assert.That(frontEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));

            Assert.That(
                TryGetUnitAt(entities, new SurfaceCell(FaceId.Floor, 2, 2), out var floorWindupEnemy),
                Is.True,
                "Floor face should include a nearby wind-up enemy so attack telegraph presentation is visible in the showcase start area.");
            Assert.That(floorWindupEnemy.teamId, Is.EqualTo(2));
            Assert.That(floorWindupEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));

            Assert.That(
                GetPlanarDistance(new SurfaceCell(FaceId.Floor, 1, 1), floorEnemy.position),
                Is.EqualTo(4),
                "The floor enemy should start inside sense range but outside attack range so Chase appears before Charge.");
            Assert.That(
                GetPlanarDistance(new SurfaceCell(FaceId.Floor, 1, 1), floorWindupEnemy.position),
                Is.EqualTo(2),
                "The wind-up enemy should start close enough to telegraph quickly without spawning directly in the charger lane.");
        }

        [Test]
        public void CombinedGameplayStage_BuildsEnemyProfileOverridesForChargeScoutAndWindupVariants()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(buildResult.EnemyAiProfileOverrides, Is.Not.Null);
            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(3));

            Assert.That(TryGetProfileOverride(buildResult, 50, out var floorChargingProfile), Is.True);
            Assert.That(floorChargingProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Charge));
            Assert.That(floorChargingProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));

            Assert.That(TryGetProfileOverride(buildResult, 51, out var frontScoutProfile), Is.True);
            Assert.That(frontScoutProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(frontScoutProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));

            Assert.That(TryGetProfileOverride(buildResult, 52, out var floorWindupProfile), Is.True);
            Assert.That(floorWindupProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(floorWindupProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
            Assert.That(floorWindupProfile.AttackTimingSettings.WindupTicks, Is.EqualTo(2));
        }

        [Test]
        public void CombinedGameplayStage_DoesNotPlaceMultipleEntitiesOnTheSameCell()
        {
            var buildResult = BuildCombinedStage();
            var occupiedCells = new HashSet<SurfaceCell>();

            for (var i = 0; i < buildResult.InitialEntities.Length; i++)
            {
                Assert.That(
                    occupiedCells.Add(buildResult.InitialEntities[i].position),
                    Is.True,
                    $"Duplicate entity placement detected at {buildResult.InitialEntities[i].position}.");
            }
        }

        [Test]
        public void GameplayBoxCapabilityLabelViewFactory_AddsCapabilityTextOnlyToBoxes()
        {
            var parentObject = new GameObject("GameplayBoxCapabilityLabelViewFactoryTests");

            try
            {
                var factory = new GameplayBoxCapabilityLabelViewFactory(parentObject.transform, 1f, playerEntityId: 10);

                var boxView = factory.CreateView(
                    new EntityState
                    {
                        entityId = 30,
                        position = new SurfaceCell(FaceId.Floor, 0, 0),
                        hp = 1,
                        maxHp = 1,
                        teamId = 0,
                        type = EntityType.Box,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Right,
                        boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                    });

                var label = boxView.transform.Find("CapabilityLabel");
                Assert.That(label, Is.Not.Null);
                Assert.That(label.GetComponent<GameplayFloatingTextBillboard>(), Is.Not.Null);

                var textMesh = label.GetComponent<TextMesh>();
                Assert.That(textMesh, Is.Not.Null);
                Assert.That(textMesh.text, Is.EqualTo("Push\nFlip\nDestroy"));

                var unitView = factory.CreateView(
                    new EntityState
                    {
                        entityId = 31,
                        position = new SurfaceCell(FaceId.Floor, 1, 0),
                        hp = 3,
                        maxHp = 3,
                        teamId = 1,
                        type = EntityType.Unit,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Up,
                    });

                Assert.That(unitView.transform.Find("CapabilityLabel"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_PlayerViewPrefabFactory_UsesPrefabOnlyForPlayerAndKeepsBoxLabels()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_PlayerViewPrefabFactory");
            var boardRootObject = new GameObject("CombinedGameplayShowcaseInstaller_PlayerViewPrefabFactory_BoardRoot");
            var playerPrefabObject = new GameObject("CombinedGameplayShowcaseInstaller_PlayerPrefab");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();

                var playerPrefabView = playerPrefabObject.AddComponent<GameplayEntityView>();
                playerPrefabObject.AddComponent<PlayerAnimationTimingAuthoring>();
                playerPrefabObject.AddComponent<PlayerAnimatorDriver>();
                new GameObject("PrefabMarker").transform.SetParent(playerPrefabObject.transform, worldPositionStays: false);

                var prefabField = typeof(CombinedGameplayShowcaseInstaller).GetField(
                    "playerViewPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(prefabField, Is.Not.Null);
                prefabField.SetValue(installer, playerPrefabView);

                var factoryMethod = installer.GetType().GetMethod(
                    "CreateViewFactory",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(factoryMethod, Is.Not.Null);
                var factory = (IGameplayEntityViewFactory)factoryMethod.Invoke(installer, new object[] { boardRoot });
                Assert.That(factory, Is.Not.Null);

                var playerView = factory.CreateView(
                    new EntityState
                    {
                        entityId = 10,
                        position = new SurfaceCell(FaceId.Floor, 1, 1),
                        hp = 3,
                        maxHp = 3,
                        teamId = 1,
                        type = EntityType.Unit,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Right,
                    });

                Assert.That(playerView, Is.Not.SameAs(playerPrefabView));
                Assert.That(playerView.transform.parent, Is.EqualTo(boardRoot.EntityRoot));
                Assert.That(playerView.GetComponent<PlayerAnimatorDriver>(), Is.Not.Null);
                Assert.That(playerView.GetComponent<PlayerAnimationTimingAuthoring>(), Is.Not.Null);
                Assert.That(playerView.transform.Find("PrefabMarker"), Is.Not.Null);
                Assert.That(playerView.transform.Find("CapabilityLabel"), Is.Null);

                var boxView = factory.CreateView(
                    new EntityState
                    {
                        entityId = 30,
                        position = new SurfaceCell(FaceId.Floor, 3, 1),
                        hp = 1,
                        maxHp = 1,
                        teamId = 0,
                        type = EntityType.Box,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Right,
                        boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
                    });

                Assert.That(boxView.transform.Find("CapabilityLabel"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(playerPrefabObject);
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcasePlayerPrefabViewFactory_PlayerPrefabMissingAnimationTimingAuthoring_Throws()
        {
            var parentObject = new GameObject("CombinedGameplayShowcasePlayerPrefabViewFactory_PlayerPrefabMissingAnimationTimingAuthoring_Throws");
            var playerPrefabObject = new GameObject("CombinedGameplayShowcasePlayerPrefabViewFactory_PlayerPrefab");

            try
            {
                var playerPrefabView = playerPrefabObject.AddComponent<GameplayEntityView>();
                playerPrefabObject.AddComponent<PlayerAnimatorDriver>();

                Assert.Throws<System.InvalidOperationException>(
                    () => new CombinedGameplayShowcasePlayerPrefabViewFactory(
                        parentObject.transform,
                        playerEntityId: 10,
                        playerViewPrefab: playerPrefabView,
                        cellSize: 1f));
            }
            finally
            {
                Object.DestroyImmediate(playerPrefabObject);
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringOnlyToWindupDemoEnemy()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringOnlyToWindupDemoEnemy");
            var boardRootObject = new GameObject("CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringOnlyToWindupDemoEnemy_BoardRoot");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();

                var factory = CreateViewFactory(installer, boardRoot);
                var buildResult = BuildCombinedStage();

                Assert.That(TryGetUnitAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 2, 2), out var windupEnemy), Is.True);
                Assert.That(TryGetUnitAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 1, 5), out var chargingEnemy), Is.True);

                var windupView = factory.CreateView(windupEnemy);
                var chargingView = factory.CreateView(chargingEnemy);

                var authoring = windupView.GetComponent<EnemyAnimationTimingAuthoring>();
                Assert.That(authoring, Is.Not.Null);
                Assert.That(windupView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(windupView.GetComponent<Animator>(), Is.Not.Null);

                var snapshot = authoring.CreateSnapshot();
                Assert.That(snapshot.TryGetAttackWindupAnimatorDurationOverride(out var windupDurationSeconds), Is.True);
                Assert.That(windupDurationSeconds, Is.EqualTo(0.35f));
                Assert.That(snapshot.TryGetRecoverAnimatorDurationOverride(out var recoverDurationSeconds), Is.True);
                Assert.That(recoverDurationSeconds, Is.EqualTo(0.5f));
                Assert.That(snapshot.TryGetStateTransitionCrossFadeDurationOverride(out var crossFadeDurationSeconds), Is.True);
                Assert.That(crossFadeDurationSeconds, Is.EqualTo(0.08f));

                Assert.That(chargingView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(chargingView.GetComponent<EnemyAnimationTimingAuthoring>(), Is.Null);
                Assert.That(chargingView.GetComponent<Animator>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_WindupDemoEnemy_TimingAuthoringFeedsPresenterDriver()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_WindupDemoEnemy_TimingAuthoringFeedsPresenterDriver");
            var boardRootObject = new GameObject("CombinedGameplayShowcaseInstaller_WindupDemoEnemy_TimingAuthoringFeedsPresenterDriver_BoardRoot");
            var presenterObject = new GameObject("CombinedGameplayShowcaseInstaller_WindupDemoEnemy_TimingAuthoringFeedsPresenterDriver_Presenter");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();

                var factory = CreateViewFactory(installer, boardRoot);
                var buildResult = BuildCombinedStage();
                Assert.That(TryGetUnitAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 2, 2), out var windupEnemy), Is.True);

                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                var registry = presenterObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, factory);
                presenter.Initialize(
                    binder,
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    cellSize: 1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(new[] { windupEnemy }, buildResult.InitialTopology);

                Assert.That(registry.TryGetView(windupEnemy.entityId, out var enemyView), Is.True);
                var driver = enemyView.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    tickIndex: 1,
                    new[] { WithAiMode(windupEnemy, EnemyAiMode.Attack) },
                    buildResult.InitialTopology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            new TickEnemyActionPresentationSignal(
                                windupEnemy.entityId,
                                EnemyActionKind.Melee,
                                activeActionSequence: 1,
                                startedThisTick: true,
                                canceledThisTick: false,
                                executedThisTick: false,
                                startedRecoveryThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Attack));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(1f / 0.35f).Within(0.0001f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    tickIndex: 2,
                    new[] { WithAiMode(windupEnemy, EnemyAiMode.Recover) },
                    buildResult.InitialTopology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        new[]
                        {
                            new TickEnemyActionPresentationSignal(
                                windupEnemy.entityId,
                                EnemyActionKind.Melee,
                                activeActionSequence: 1,
                                startedThisTick: false,
                                canceledThisTick: false,
                                executedThisTick: true,
                                startedRecoveryThisTick: true),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.CurrentAnimatorSpeed, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Recover"));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));
                Assert.That(driver.AttackSignalCount, Is.EqualTo(1));
                Assert.That(driver.RecoverySignalCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_OverlayMentionsWindupDemoLane()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_OverlayMentionsWindupDemoLane");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var overlay = installer.GetShowcaseOverlayContent();

                Assert.That(overlay.Title, Is.EqualTo("Combined Gameplay Showcase"));
                Assert.That(overlay.Highlights, Has.Some.Contains("2-tick wind-up melee profile"));
                Assert.That(overlay.Highlights, Has.Some.Contains("charger lane"));
            }
            finally
            {
                Object.DestroyImmediate(installerObject);
            }
        }

        private static StageRuntimeBuildResult BuildCombinedStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");
            return StageRuntimeBuilder.Build(stage);
        }

        private static IGameplayEntityViewFactory CreateViewFactory(
            CombinedGameplayShowcaseInstaller installer,
            GameplayBoardRoot boardRoot)
        {
            var factoryMethod = installer.GetType().GetMethod(
                "CreateViewFactory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(factoryMethod, Is.Not.Null);
            return (IGameplayEntityViewFactory)factoryMethod.Invoke(installer, new object[] { boardRoot });
        }

        private static bool HasWallAt(IReadOnlyList<EntityState> entities, SurfaceCell cell)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.type == EntityType.None && entity.position.Equals(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPushableBoxAt(IReadOnlyList<EntityState> entities, SurfaceCell cell)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.type == EntityType.Box &&
                    entity.position.Equals(cell) &&
                    (entity.boxCapabilities & BoxCapabilities.Push) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetUnitAt(IReadOnlyList<EntityState> entities, SurfaceCell cell, out EntityState unit)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.type == EntityType.Unit &&
                    entity.position.Equals(cell))
                {
                    unit = entity;
                    return true;
                }
            }

            unit = default;
            return false;
        }

        private static int? GetPlanarDistance(SurfaceCell source, SurfaceCell target)
        {
            if (source.face != target.face)
            {
                return null;
            }

            var delta = target - source;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        }

        private static bool TryGetProfileOverride(
            StageRuntimeBuildResult buildResult,
            int entityId,
            out EnemyAiProfile profile)
        {
            for (var i = 0; i < buildResult.EnemyAiProfileOverrides.Length; i++)
            {
                var entry = buildResult.EnemyAiProfileOverrides[i];
                if (entry.EntityId != entityId)
                {
                    continue;
                }

                profile = entry.Profile;
                return profile != null;
            }

            profile = null;
            return false;
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            IReadOnlyList<EntityState> finalEntities,
            CubeTopologyState topology,
            TickPresentationData presentationData)
        {
            return new TickResult(
                tickIndex,
                new[] { TickPhase.Movement, TickPhase.Attack, TickPhase.Cleanup },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                CleanupPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                presentationData,
                string.Empty,
                TickTrace.Empty);
        }

        private static EntityState WithAiMode(EntityState entity, EnemyAiMode aiMode)
        {
            entity.aiMode = aiMode;
            return entity;
        }
    }
}
