using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Timing;
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
        private const string CombinedEnemyPresentationCatalogAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Catalogs/EnemyPresentationCatalog_CombinedGameplayShowcase.asset";
        private const string DefaultSimulationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplaySimulationTimingPreset_DefaultShowcase.asset";
        private const string DefaultPresentationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplayPresentationTimingPreset_DefaultShowcase.asset";
        private const int ConfiguredShowcaseEnemyId = 54;
        private const int WallFollowerShowcaseEnemyId = 56;
        private const int JumpShowcaseEnemyId = 57;
        private const string AttackingEnemyPresentationId = "Attacking_showcase";
        private const string NonAttackingEnemyPresentationId = "nonAttacking_showcase";
        private const string JumpEnemyPresentationId = "Jump_showcase";

        [Test]
        public void CombinedGameplayStage_DoesNotAutoGeneratePerimeterWalls()
        {
            var buildResult = BuildCombinedStage();
            var boardBounds = buildResult.BoardBounds;
            var entities = buildResult.InitialEntities;

            Assert.That(
                HasWallAt(entities, new SurfaceCell(FaceId.Floor, 2, boardBounds.MaxInclusive.y)),
                Is.False,
                "Automatic perimeter walls should not be synthesized on edge cells.");
            Assert.That(
                HasWallAt(entities, new SurfaceCell(FaceId.Floor, 4, boardBounds.MaxInclusive.y)),
                Is.False,
                "Automatic perimeter walls should not be synthesized on edge cells.");
            Assert.That(
                HasWallAt(entities, new SurfaceCell(FaceId.Floor, 10, boardBounds.MinInclusive.y)),
                Is.False,
                "Automatic perimeter walls should not be synthesized on edge cells.");
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
                "Front should retain its authored pushable box placement.");
            Assert.That(
                HasPushableBoxAt(entities, new SurfaceCell(FaceId.Ceiling, 7, 1)),
                Is.True,
                "Ceiling should retain its authored pushable box placement.");
            Assert.That(
                HasPushableBoxAt(entities, new SurfaceCell(FaceId.Back, 3, 5)),
                Is.True,
                "Back should retain its authored pushable box placement.");
        }

        [Test]
        public void CombinedGameplayStage_PlacesConfiguredShowcaseEnemy()
        {
            var buildResult = BuildCombinedStage();
            var entities = buildResult.InitialEntities;

            Assert.That(
                TryGetUnitAt(entities, new SurfaceCell(FaceId.Floor, 6, 2), out var showcaseEnemy),
                Is.True,
                "The current combined showcase should place its configured enemy on the floor face.");
            Assert.That(showcaseEnemy.entityId, Is.EqualTo(ConfiguredShowcaseEnemyId));
            Assert.That(showcaseEnemy.teamId, Is.EqualTo(2));
            Assert.That(showcaseEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(showcaseEnemy.hp, Is.EqualTo(3));
            Assert.That(
                GetPlanarDistance(new SurfaceCell(FaceId.Floor, 1, 1), showcaseEnemy.position),
                Is.EqualTo(6),
                "The authored showcase enemy should remain visible from the spawn lane without spawning adjacent to the player.");
        }

        [Test]
        public void CombinedGameplayStage_BuildsEnemyProfileOverrideForConfiguredShowcaseEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(buildResult.EnemyAiProfileOverrides, Is.Not.Null);
            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(4));

            Assert.That(TryGetProfileOverride(buildResult, ConfiguredShowcaseEnemyId, out var showcaseProfile), Is.True);
            Assert.That(showcaseProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(showcaseProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
            Assert.That(showcaseProfile.AttackTimingSettings.WindupSeconds, Is.EqualTo(1f));
            Assert.That(showcaseProfile.LocomotionTimingSettings.MoveCooldownSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void CombinedGameplayStage_BuildsEnemyPresentationBindingForConfiguredShowcaseEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(buildResult.EnemyPresentationBindings, Is.Not.Null);
            Assert.That(buildResult.EnemyPresentationBindings.Length, Is.EqualTo(4));
            Assert.That(TryGetPresentationBinding(buildResult, ConfiguredShowcaseEnemyId, out var configuredBinding), Is.True);
            Assert.That(configuredBinding.PresentationId, Is.EqualTo(AttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(buildResult, WallFollowerShowcaseEnemyId, out var wallFollowerBinding), Is.True);
            Assert.That(wallFollowerBinding.PresentationId, Is.EqualTo(NonAttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(buildResult, JumpShowcaseEnemyId, out var jumpBinding), Is.True);
            Assert.That(jumpBinding.PresentationId, Is.EqualTo(JumpEnemyPresentationId));
        }

        [Test]
        public void CombinedGameplayStage_PlacesWallFollowerShowcaseEnemyAtConfiguredPatrolLane()
        {
            var buildResult = BuildCombinedStage();
            var entities = buildResult.InitialEntities;

            Assert.That(TryGetUnitAt(entities, new SurfaceCell(FaceId.Floor, 7, 8), out var wallFollowerEnemy), Is.True);
            Assert.That(wallFollowerEnemy.entityId, Is.EqualTo(WallFollowerShowcaseEnemyId));
            Assert.That(wallFollowerEnemy.teamId, Is.EqualTo(2));
            Assert.That(wallFollowerEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(wallFollowerEnemy.facing, Is.EqualTo(Direction.Up));
        }

        [Test]
        public void CombinedGameplayStage_BuildsWallFollowerProfileOverride()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, WallFollowerShowcaseEnemyId, out var wallFollowerProfile), Is.True);
            Assert.That(wallFollowerProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(wallFollowerProfile.DetectionStrategyKind, Is.EqualTo(DetectionStrategyKind.None));
            Assert.That(wallFollowerProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(wallFollowerProfile.PatrolSettings.TurnPreference, Is.EqualTo(WallFollowTurnPreference.Left));
        }

        [Test]
        public void CombinedGameplayStage_PlacesJumpShowcaseEnemyOnFarFloorLane()
        {
            var buildResult = BuildCombinedStage();
            var entities = buildResult.InitialEntities;

            Assert.That(TryGetUnitAt(entities, new SurfaceCell(FaceId.Floor, 10, 2), out var jumpEnemy), Is.True);
            Assert.That(jumpEnemy.entityId, Is.EqualTo(JumpShowcaseEnemyId));
            Assert.That(jumpEnemy.teamId, Is.EqualTo(2));
            Assert.That(jumpEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(jumpEnemy.facing, Is.EqualTo(Direction.Left));
        }

        [Test]
        public void CombinedGameplayStage_BuildsJumpShowcaseProfileOverride()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, JumpShowcaseEnemyId, out var jumpProfile), Is.True);
            Assert.That(jumpProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(jumpProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
            Assert.That(jumpProfile.JumpTimingSettings.WindupSeconds, Is.EqualTo(0.35f));
            Assert.That(jumpProfile.JumpTimingSettings.AirborneSeconds, Is.EqualTo(0.35f));
            Assert.That(jumpProfile.JumpTimingSettings.CooldownSeconds, Is.EqualTo(0.8f));
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

                var factory = CreateViewFactory(installer, boardRoot);
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
        public void GameplayBoxCapabilityLabelViewFactory_PlayerPrefabMissingAnimationTimingAuthoring_ThrowsWhenCreatingPlayerView()
        {
            var parentObject = new GameObject("GameplayBoxCapabilityLabelViewFactory_PlayerPrefabMissingAnimationTimingAuthoring_ThrowsWhenCreatingPlayerView");
            var playerPrefabObject = new GameObject("GameplayBoxCapabilityLabelViewFactory_PlayerPrefab");

            try
            {
                var playerPrefabView = playerPrefabObject.AddComponent<GameplayEntityView>();
                playerPrefabObject.AddComponent<PlayerAnimatorDriver>();

                var factory = new GameplayBoxCapabilityLabelViewFactory(
                    parentObject.transform,
                    1f,
                    playerEntityId: 10,
                    playerViewPrefab: playerPrefabView);

                Assert.Throws<System.InvalidOperationException>(
                    () => factory.CreateView(
                        new EntityState
                        {
                            entityId = 10,
                            position = new SurfaceCell(FaceId.Floor, 0, 0),
                            hp = 3,
                            maxHp = 3,
                            teamId = 1,
                            type = EntityType.Unit,
                            state = EntityPhaseState.Idle,
                            facing = Direction.Right,
                        }));
            }
            finally
            {
                Object.DestroyImmediate(playerPrefabObject);
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator()
        {
            var parentObject = new GameObject("GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator");
            var prefabObject = new GameObject("GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab");

            try
            {
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();
                new GameObject("PrefabMarker").transform.SetParent(prefabObject.transform, worldPositionStays: false);

                var factory = new GameplayBoxCapabilityLabelViewFactory(
                    parentObject.transform,
                    1f,
                    playerEntityId: 10,
                    staticViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        { 30, prefabView },
                    });

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
                        boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Item,
                    });

                Assert.That(boxView.transform.Find("PrefabMarker"), Is.Not.Null);
                var label = boxView.transform.Find("CapabilityLabel");
                Assert.That(label, Is.Not.Null);
                Assert.That(label.GetComponent<TextMesh>().text, Is.EqualTo("Push\nItem"));
            }
            finally
            {
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy");
            var boardRootObject = new GameObject("CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy_BoardRoot");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var factory = CreateViewFactory(installer, boardRoot);
                var buildResult = BuildCombinedStage();

                Assert.That(TryGetUnitAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 6, 2), out var showcaseEnemy), Is.True);

                var showcaseView = factory.CreateView(showcaseEnemy);

                var authoring = showcaseView.GetComponent<EnemyAnimationTimingAuthoring>();
                Assert.That(authoring, Is.Not.Null);
                Assert.That(showcaseView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(showcaseView.GetComponent<Animator>(), Is.Not.Null);

                var snapshot = authoring.CreateSnapshot();
                Assert.That(snapshot.TryGetAttackWindupAnimatorDurationOverride(out var windupDurationSeconds), Is.True);
                Assert.That(windupDurationSeconds, Is.EqualTo(1f));
                Assert.That(
                    snapshot.TryGetAttackWindupReferenceClipLengthSeconds(
                        out var windupReferenceClipLengthSeconds),
                    Is.True);
                Assert.That(windupReferenceClipLengthSeconds, Is.GreaterThan(0f));
                Assert.That(snapshot.TryGetRecoverAnimatorDurationOverride(out var recoverDurationSeconds), Is.True);
                Assert.That(recoverDurationSeconds, Is.EqualTo(1f));
                Assert.That(
                    snapshot.TryGetRecoverReferenceClipLengthSeconds(
                        out var recoverReferenceClipLengthSeconds),
                    Is.True);
                Assert.That(recoverReferenceClipLengthSeconds, Is.GreaterThan(0f));
                Assert.That(snapshot.TryGetStateTransitionCrossFadeDurationOverride(out var crossFadeDurationSeconds), Is.True);
                Assert.That(crossFadeDurationSeconds, Is.EqualTo(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver");
            var boardRootObject = new GameObject("CombinedGameplayShowcaseInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver_BoardRoot");
            var presenterObject = new GameObject("CombinedGameplayShowcaseInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver_Presenter");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var factory = CreateViewFactory(installer, boardRoot);
                var buildResult = BuildCombinedStage();
                Assert.That(TryGetUnitAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 6, 2), out var showcaseEnemy), Is.True);

                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                var registry = presenterObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, factory);
                presenter.Initialize(
                    binder,
                    buildResult.BoardBounds,
                    buildResult.InitialTopology,
                    cellSize: 1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(new[] { showcaseEnemy }, buildResult.InitialTopology);

                Assert.That(registry.TryGetView(showcaseEnemy.entityId, out var enemyView), Is.True);
                var driver = enemyView.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);
                var timingSnapshot = enemyView.GetComponent<EnemyAnimationTimingAuthoring>().CreateSnapshot();
                Assert.That(
                    timingSnapshot.TryGetAttackWindupReferenceClipLengthSeconds(
                        out var windupReferenceClipLengthSeconds),
                    Is.True);
                Assert.That(
                    timingSnapshot.TryGetRecoverReferenceClipLengthSeconds(
                        out var recoverReferenceClipLengthSeconds),
                    Is.True);

                presenter.Present(CreateTickResult(
                    tickIndex: 1,
                    new[] { WithAiMode(showcaseEnemy, EnemyAiMode.Attack) },
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
                                showcaseEnemy.entityId,
                                EnemyActionKind.Melee,
                                activeActionSequence: 1,
                                startedThisTick: true,
                                canceledThisTick: false,
                                executedThisTick: false,
                                startedRecoveryThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Attack));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    driver.CurrentAnimatorSpeed,
                    Is.EqualTo(windupReferenceClipLengthSeconds / 1f).Within(0.0001f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.001f).Within(0.0001f));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Windup"));
                Assert.That(driver.WindupSignalCount, Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    tickIndex: 2,
                    new[] { WithAiMode(showcaseEnemy, EnemyAiMode.Recover) },
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
                                showcaseEnemy.entityId,
                                EnemyActionKind.Melee,
                                activeActionSequence: 1,
                                startedThisTick: false,
                                canceledThisTick: false,
                                executedThisTick: true,
                                startedRecoveryThisTick: true),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    driver.CurrentAnimatorSpeed,
                    Is.EqualTo(recoverReferenceClipLengthSeconds / 1f).Within(0.0001f));
                Assert.That(driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.001f).Within(0.0001f));
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

        private static StageRuntimeBuildResult BuildCombinedStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");
            return StageRuntimeBuilder.Build(stage);
        }

        private static void AssignEnemyPresentationCatalog(CombinedGameplayShowcaseInstaller installer)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(CombinedEnemyPresentationCatalogAssetPath);
            Assert.That(catalog, Is.Not.Null, $"Missing enemy presentation catalog asset at '{CombinedEnemyPresentationCatalogAssetPath}'.");

            var field = typeof(CombinedGameplayShowcaseInstaller).GetField(
                "enemyPresentationCatalog",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(installer, catalog);
        }

        private static void AssignStageDefinition(CombinedGameplayShowcaseInstaller installer)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");

            var field = typeof(StageBackedGameplayShowcaseInstallerBase).GetField(
                "stageDefinition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(installer, stage);
        }

        private static void AssignTimingPresets(CombinedGameplayShowcaseInstaller installer)
        {
            var simulationPreset = AssetDatabase.LoadAssetAtPath<GameplaySimulationTimingPreset>(
                DefaultSimulationTimingPresetAssetPath);
            Assert.That(
                simulationPreset,
                Is.Not.Null,
                $"Missing simulation timing preset asset at '{DefaultSimulationTimingPresetAssetPath}'.");

            var presentationPreset = AssetDatabase.LoadAssetAtPath<GameplayPresentationTimingPreset>(
                DefaultPresentationTimingPresetAssetPath);
            Assert.That(
                presentationPreset,
                Is.Not.Null,
                $"Missing presentation timing preset asset at '{DefaultPresentationTimingPresetAssetPath}'.");

            var simulationField = typeof(GameplayShowcaseSceneInstallerBase).GetField(
                "simulationTimingPreset",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(simulationField, Is.Not.Null);
            simulationField.SetValue(installer, simulationPreset);

            var presentationField = typeof(GameplayShowcaseSceneInstallerBase).GetField(
                "presentationTimingPreset",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(presentationField, Is.Not.Null);
            presentationField.SetValue(installer, presentationPreset);
        }

        private static IGameplayEntityViewFactory CreateViewFactory(
            CombinedGameplayShowcaseInstaller installer,
            GameplayBoardRoot boardRoot)
        {
            AssignStageDefinition(installer);
            AssignEnemyPresentationCatalog(installer);
            AssignTimingPresets(installer);
            var initialState = BuildInitialGameplayState(installer);

            var factoryMethod = installer.GetType().GetMethod(
                "CreateViewFactory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(factoryMethod, Is.Not.Null);
            return (IGameplayEntityViewFactory)factoryMethod.Invoke(installer, new[] { (object)boardRoot, initialState });
        }

        private static object BuildInitialGameplayState(CombinedGameplayShowcaseInstaller installer)
        {
            var buildInitialStateMethod = typeof(StageBackedGameplayShowcaseInstallerBase).GetMethod(
                "BuildInitialGameplayState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildInitialStateMethod, Is.Not.Null);
            return buildInitialStateMethod.Invoke(installer, Array.Empty<object>());
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

        private static bool TryGetPresentationBinding(
            StageRuntimeBuildResult buildResult,
            int entityId,
            out EnemyPresentationBinding binding)
        {
            for (var i = 0; i < buildResult.EnemyPresentationBindings.Length; i++)
            {
                var entry = buildResult.EnemyPresentationBindings[i];
                if (entry.EntityId != entityId)
                {
                    continue;
                }

                binding = entry;
                return true;
            }

            binding = default;
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
                new[] { TickPhase.Plan, TickPhase.Resolve, TickPhase.Finalize, TickPhase.Cleanup, TickPhase.Respawn },
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
