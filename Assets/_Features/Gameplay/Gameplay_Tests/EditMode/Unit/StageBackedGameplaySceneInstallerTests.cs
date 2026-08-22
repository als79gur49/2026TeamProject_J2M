using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Timing;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StageBackedGameplaySceneInstallerTests
    {
        private const string CombinedStageAssetPath =
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-4-3/stage-4-3.asset";
        private const string CombinedPresentationAssetPath =
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-4-3/stage-4-3_Presentation.asset";
        private const string StageCatalogProviderAssetPath =
            StageContentPaths.StageCatalogProviderAssetPath;
        private const string CombinedLaunchStageId = "stage-1-1";
        private const string DefaultSimulationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplaySimulationTimingPreset_DefaultShowcase.asset";
        private const string DefaultPresentationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplayPresentationTimingPreset_DefaultShowcase.asset";
        private const string ArchetypeSummonerProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_ArchetypeSummoner/EnemyAi_ArchetypeSummoner.asset";
        private const int ConfiguredShowcaseEnemyId = 60;
        private const int WallFollowerShowcaseEnemyId = 56;
        private const int JumpShowcaseEnemyId = 61;
        private const int ChargeShowcaseEnemyId = 58;
        private const int UtilitySummonerShowcaseEnemyId = 59;
        private const int CombinedShowcaseEnemyCount = 5;
        private const string AttackingEnemyPresentationId = "black_eye";
        private const string WallFollowerEnemyPresentationId = "sunwheel";
        private const string JumpChaserEnemyPresentationId = "astreton";
        private const string ChargeEnemyPresentationId = "rocket_face";

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_DoesNotAutoGeneratePerimeterWalls()
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
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_PlacesPushableBoxesOnEveryRotatingFace()
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
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_PlacesConfiguredShowcaseEnemy()
        {
            var buildResult = BuildCombinedStage();
            var entities = buildResult.InitialEntities;

            Assert.That(
                TryGetUnitAt(entities, new SurfaceCell(FaceId.Ceiling, 4, 5), out var showcaseEnemy),
                Is.True,
                "The current combined showcase should place its configured BlackEye enemy on the ceiling face.");
            Assert.That(showcaseEnemy.entityId, Is.EqualTo(ConfiguredShowcaseEnemyId));
            Assert.That(showcaseEnemy.teamId, Is.EqualTo(2));
            Assert.That(showcaseEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(showcaseEnemy.hp, Is.EqualTo(3));
            Assert.That(showcaseEnemy.facing, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_BuildsEnemyProfileOverrideForConfiguredShowcaseEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(buildResult.EnemyAiProfileOverrides, Is.Not.Null);
            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(CombinedShowcaseEnemyCount));

            Assert.That(TryGetProfileOverride(buildResult, ConfiguredShowcaseEnemyId, out var showcaseProfile), Is.True);
            Assert.That(showcaseProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(showcaseProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.None));
            Assert.That(showcaseProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond).TryGetGlideBehavior(out _), Is.True);
            Assert.That(showcaseProfile.name, Is.EqualTo("EnemyAi_GlideChaser"));
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_BuildsGlideChaserProfileOverrideForConfiguredEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, ConfiguredShowcaseEnemyId, out var glideChaserProfile), Is.True);
            Assert.That(glideChaserProfile.name, Is.EqualTo("EnemyAi_GlideChaser"));
            Assert.That(glideChaserProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(glideChaserProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.None));
            Assert.That(glideChaserProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond).TryGetGlideBehavior(out _), Is.True);
            Assert.That(glideChaserProfile.LocomotionTimingSettings.MoveCooldownSeconds, Is.GreaterThan(0f));
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_BuildsJumpChaserProfileOverrideForAstretonEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, JumpShowcaseEnemyId, out var jumpProfile), Is.True);
            Assert.That(jumpProfile.name, Is.EqualTo("EnemyAi_JumpChaser"));
            Assert.That(jumpProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(jumpProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_BuildsArchetypeSummonerProfileOverrideForUtilitySummonerEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, UtilitySummonerShowcaseEnemyId, out var summonerProfile), Is.True);
            Assert.That(summonerProfile, Is.Not.Null);
            Assert.That(summonerProfile.name, Is.EqualTo("EnemyAi_ArchetypeSummoner"));

            var runtimeDefinition = summonerProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(runtimeDefinition.Capabilities.TryGetUtility(out _), Is.False);
            Assert.That(runtimeDefinition.TryGetSummonBehavior(out var summon), Is.True);
            Assert.That(summon.SummonedArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));
            Assert.That(summon.OverrideHp, Is.True);
            Assert.That(summon.HpOverride, Is.EqualTo(1));
            Assert.That(
                summon.WindupTicks,
                Is.EqualTo(GameplayTimingProfile.SecondsToTicks(
                    1.7f,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond)));
            Assert.That(
                summon.RecoveryTicks,
                Is.EqualTo(GameplayTimingProfile.SecondsToTicks(
                    0.7f,
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    allowZero: true)));
            Assert.That(summon.SuppressMovementDuringWindup, Is.True);
            Assert.That(summon.SuppressMovementDuringRecover, Is.True);
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_BuildsEnemyPresentationBindingForConfiguredShowcaseEnemy()
        {
            var presentationDefinition = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(CombinedPresentationAssetPath);
            Assert.That(
                presentationDefinition,
                Is.Not.Null,
                $"Missing stage presentation asset at '{CombinedPresentationAssetPath}'.");

            var presentation = StagePresentationAssembler.Resolve(presentationDefinition);

            Assert.That(presentation.EnemyPresentationBindings, Is.Not.Null);
            Assert.That(presentation.EnemyPresentationBindings.Length, Is.EqualTo(CombinedShowcaseEnemyCount));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, ConfiguredShowcaseEnemyId, out var configuredBinding), Is.True);
            Assert.That(configuredBinding.PresentationId, Is.EqualTo(AttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, WallFollowerShowcaseEnemyId, out var wallFollowerBinding), Is.True);
            Assert.That(wallFollowerBinding.PresentationId, Is.EqualTo(WallFollowerEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, JumpShowcaseEnemyId, out var jumpBinding), Is.True);
            Assert.That(jumpBinding.PresentationId, Is.EqualTo(JumpChaserEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, ChargeShowcaseEnemyId, out var chargeBinding), Is.True);
            Assert.That(chargeBinding.PresentationId, Is.EqualTo(ChargeEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, UtilitySummonerShowcaseEnemyId, out var utilitySummonerBinding), Is.True);
            Assert.That(utilitySummonerBinding.PresentationId, Is.EqualTo("j_peter"));
        }

        [Test]
        [Category("Full")]
        public void StagePresentationResolvedData_RoundTrip_IncludesEnemyPresentationArchetypeCatalog()
        {
            var presentationDefinition = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(CombinedPresentationAssetPath);
            Assert.That(
                presentationDefinition,
                Is.Not.Null,
                $"Missing stage presentation asset at '{CombinedPresentationAssetPath}'.");

            var resolved = StagePresentationAssembler.Resolve(presentationDefinition);
            var copy = ScriptableObject.CreateInstance<StagePresentationDefinition>();

            try
            {
                copy.ApplyResolvedData(resolved);
                var roundTrip = StagePresentationAssembler.Resolve(copy);

                Assert.That(roundTrip.EnemyPresentationCatalog, Is.SameAs(resolved.EnemyPresentationCatalog));
                Assert.That(
                    roundTrip.EnemyPresentationArchetypeCatalog,
                    Is.SameAs(resolved.EnemyPresentationArchetypeCatalog));
                Assert.That(roundTrip.EnemyPresentationArchetypeCatalog, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_PlacesWallFollowerShowcaseEnemyAtConfiguredPatrolLane()
        {
            var buildResult = BuildCombinedStage();
            var entities = buildResult.InitialEntities;

            Assert.That(TryGetUnitAt(entities, new SurfaceCell(FaceId.Floor, 7, 7), out var wallFollowerEnemy), Is.True);
            Assert.That(wallFollowerEnemy.entityId, Is.EqualTo(WallFollowerShowcaseEnemyId));
            Assert.That(wallFollowerEnemy.teamId, Is.EqualTo(2));
            Assert.That(wallFollowerEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(wallFollowerEnemy.facing, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_BuildsWallFollowerProfileOverride()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, WallFollowerShowcaseEnemyId, out var wallFollowerProfile), Is.True);
            Assert.That(wallFollowerProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(wallFollowerProfile.DetectionStrategyKind, Is.EqualTo(DetectionStrategyKind.None));
            Assert.That(wallFollowerProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(wallFollowerProfile.PatrolSettings.TurnPreference, Is.EqualTo(WallFollowTurnPreference.Left));
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_PlacesJumpShowcaseEnemyOnFarFloorLane()
        {
            var buildResult = BuildCombinedStage();
            var entities = buildResult.InitialEntities;

            Assert.That(TryGetUnitAt(entities, new SurfaceCell(FaceId.Floor, 12, 3), out var jumpEnemy), Is.True);
            Assert.That(jumpEnemy.entityId, Is.EqualTo(JumpShowcaseEnemyId));
            Assert.That(jumpEnemy.teamId, Is.EqualTo(2));
            Assert.That(jumpEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(jumpEnemy.facing, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void MechanicsShowcaseStage_BuildsJumpShowcaseProfileOverride()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, JumpShowcaseEnemyId, out var jumpProfile), Is.True);
            Assert.That(jumpProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(jumpProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
        }

        [Test]
        [Category("Full")]
        public void MechanicsShowcaseStage_DoesNotPlaceMultipleEntitiesOnTheSameCell()
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
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_PlayerViewPrefabFactory_UsesPrefabOnlyForPlayerAndDoesNotCreateBoxLabels()
        {
            var installerObject = new GameObject("StageBackedGameplaySceneInstaller_PlayerViewPrefabFactory");
            var boardRootObject = new GameObject("StageBackedGameplaySceneInstaller_PlayerViewPrefabFactory_BoardRoot");
            var playerPrefabObject = new GameObject("StageBackedGameplaySceneInstaller_PlayerPrefab");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();

                var playerPrefabView = playerPrefabObject.AddComponent<GameplayEntityView>();
                playerPrefabObject.AddComponent<PlayerAnimationTimingAuthoring>();
                playerPrefabObject.AddComponent<PlayerAnimatorDriver>();
                new GameObject("PrefabMarker").transform.SetParent(playerPrefabObject.transform, worldPositionStays: false);

                var prefabField = typeof(StageBackedGameplaySceneInstaller).GetField(
                    "playerViewPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(prefabField, Is.Not.Null);
                prefabField.SetValue(installer, playerPrefabView);

                var factory = CreateViewFactory(installer, boardRoot, out var playerEntityId);
                Assert.That(factory, Is.Not.Null);

                var playerView = factory.CreateView(
                    new EntityState
                    {
                        entityId = playerEntityId,
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

                Assert.That(boxView.GetComponent<PlayerAnimatorDriver>(), Is.Null);
                Assert.That(boxView.transform.Find("PrefabMarker"), Is.Null);
                Assert.That(boxView.transform.Find("CapabilityLabel"), Is.Null);
            }
            finally
            {
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(playerPrefabObject);
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_StaticBoxPrefab_DoesNotCreateCapabilityLabel()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_StaticBoxPrefab_DoesNotCreateCapabilityLabel");
            var prefabObject = new GameObject("DefaultGameplayEntityViewFactory_StaticBoxPrefab");

            try
            {
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();
                new GameObject("PrefabMarker").transform.SetParent(prefabObject.transform, worldPositionStays: false);
                var visualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visualObject.name = "PrefabVisual";
                visualObject.transform.SetParent(prefabObject.transform, worldPositionStays: false);
                var collider = visualObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }

                var factory = new DefaultGameplayEntityViewFactory(
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
                Assert.That(boxView.transform.Find("CapabilityLabel"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void StageBackedGameplaySceneInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy()
        {
            var installerObject = new GameObject("StageBackedGameplaySceneInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy");
            var boardRootObject = new GameObject("StageBackedGameplaySceneInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy_BoardRoot");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var factory = CreateViewFactory(installer, boardRoot);
                var buildResult = BuildCombinedStage();

                Assert.That(
                    TryGetUnitAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Ceiling, 4, 5), out var showcaseEnemy),
                    Is.True);

                var showcaseView = factory.CreateView(showcaseEnemy);

                var authoring = showcaseView.GetComponentInChildren<EnemyAnimationTimingAuthoring>(includeInactive: true);
                Assert.That(authoring, Is.Not.Null);
                Assert.That(showcaseView.GetComponentInChildren<EnemyAnimatorDriver>(includeInactive: true), Is.Not.Null);

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
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        [Ignore("Deleted stage-specific fixture; covered by remaining campaign stage content tests.")]
        public void StageBackedGameplaySceneInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver()
        {
            var installerObject = new GameObject("StageBackedGameplaySceneInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver");
            var boardRootObject = new GameObject("StageBackedGameplaySceneInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver_BoardRoot");
            var presenterObject = new GameObject("StageBackedGameplaySceneInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver_Presenter");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                var factory = CreateViewFactory(installer, boardRoot);
                var buildResult = BuildCombinedStage();
                Assert.That(
                    TryGetUnitAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Ceiling, 4, 5), out var showcaseEnemy),
                    Is.True);
                showcaseEnemy.position = new SurfaceCell(FaceId.Floor, 4, 5);

                var presenter = presenterObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
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
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(presenterObject);
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_DefaultBundle_IncludesGlideKinematic()
        {
            var installerObject = new GameObject("StageBackedGameplaySceneInstaller_DefaultBundle_IncludesGlideKinematic");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntry(installer);
                AssignTimingPresets(installer);

                var configuration = BuildConfiguration(installer);
                var flags = configuration.CreateRuntimeFeatureFlags();

                Assert.That(flags.EnablePlayerFree2DActionAssist, Is.True);
                Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.True);
                Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.True);
                Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.True);
                Assert.That(
                    configuration.PlayerContinuousLocomotion.ActionAssistSettleWindowCells,
                    Is.EqualTo(0.421875f));
                Assert.That(
                    configuration.PlayerContinuousLocomotion.CollisionRadiusCells,
                    Is.EqualTo(0.28125f));
            }
            finally
            {
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_Configuration_UsesStageDefinitionEnemyUnitArchetypeCatalog()
        {
            var installerObject = new GameObject("StageBackedGameplaySceneInstaller_Configuration_UsesEnemyUnitArchetypeCatalog");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntry(installer);
                AssignTimingPresets(installer);

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.EnemyUnitArchetypeCatalog, Is.Not.Null);
                Assert.That(
                    configuration.EnemyUnitArchetypeCatalog,
                    Is.SameAs(configuration.StageContentEntry.GameplayDefinition.EnemyUnitArchetypeCatalog));
                Assert.That(configuration.EnemyUnitArchetypeCatalog.name, Is.EqualTo("EnemyUnitArchetypeCatalog_CampaignMainEnemy"));
                Assert.That(configuration.EnemyUnitArchetypeCatalog.Entries.Count, Is.EqualTo(1));
                Assert.That(configuration.EnemyUnitArchetypeCatalog.Entries[0].ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));
                Assert.That(configuration.EnemyUnitArchetypeCatalog.Entries[0].SpawnDefaults.UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            }
            finally
            {
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_CommittedActiveRetryWithoutPending_InjectsChanceReadSource()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_CommittedActiveRetryWithoutPending_InjectsChanceReadSource");
            var saveKey = CreateTransientNamespace(nameof(StageBackedGameplaySceneInstaller_CommittedActiveRetryWithoutPending_InjectsChanceReadSource));
            var activeKey = saveKey + ".active";
            var saveStore = new TransientCampaignSaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(new TransientActiveSlotStorage(activeKey));
            var launchStageId = StageId.CreateOrThrow(CombinedLaunchStageId);

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = launchStageId,
                    CurrentLevelGroupId = "level-01",
                    RemainingChances = 2,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                activeSlotProvider.SetActiveSlot(1);
                Assert.That(CampaignLaunchHandoffSessionStore.Instance.TryPeek(out _), Is.False);

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntryForProductionLaunch(installer, launchStageId);
                AssignTimingPresets(installer);
                AssignCampaignStores(installer, saveStore, activeSlotProvider);

                var configuration = BuildConfiguration(installer);
                activeSlotProvider.SetActiveSlot(2);

                Assert.That(configuration.DisablePlayerRespawn, Is.True);
                Assert.That(configuration.CampaignChancesReadSource, Is.Not.Null);
                var runningSlotContext = ReadInstallerPrivateField<CampaignRunningSlotContext>(
                    installer,
                    "_runningSlotContext");
                Assert.That(runningSlotContext, Is.Not.Null);
                Assert.That(runningSlotContext.SlotNumber, Is.EqualTo(1));
                Assert.That(
                    configuration.CampaignChancesReadSource.TryReadChances(
                        out var remainingChances,
                        out _,
                        out _),
                    Is.True);
                Assert.That(remainingChances, Is.EqualTo(2));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_ValidatedPendingLaunch_CommitsActiveAndPinsRunningContext()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_ValidatedPendingLaunch_CommitsActiveAndPinsRunningContext");
            var saveKey = CreateTransientNamespace(
                nameof(StageBackedGameplaySceneInstaller_ValidatedPendingLaunch_CommitsActiveAndPinsRunningContext));
            var activeKey = saveKey + ".active";
            var saveStore = new TransientCampaignSaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(new TransientActiveSlotStorage(activeKey));
            var launchStageId = StageId.CreateOrThrow(CombinedLaunchStageId);
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;

            try
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = launchStageId,
                    CurrentLevelGroupId = "level-01",
                    RemainingChances = 2,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                Assert.That(
                    handoffStore.TryBegin(
                        1,
                        launchStageId,
                        StageNavigationKind.Continue,
                        "main-menu-continue",
                        out _),
                    Is.True);

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntryForProductionLaunch(installer, launchStageId);
                AssignTimingPresets(installer);
                AssignCampaignStores(installer, saveStore, activeSlotProvider);

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.CampaignChancesReadSource, Is.Not.Null);
                Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(1));
                Assert.That(
                    ReadInstallerPrivateField<CampaignRunningSlotContext>(
                        installer,
                        "_runningSlotContext").SlotNumber,
                    Is.EqualTo(1));
                Assert.That(handoffStore.TryPeek(out _), Is.False);
                Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_PendingStageMismatch_DoesNotCommitActiveOrRunningContext()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_PendingStageMismatch_DoesNotCommitActiveOrRunningContext");
            var saveKey = CreateTransientNamespace(
                nameof(StageBackedGameplaySceneInstaller_PendingStageMismatch_DoesNotCommitActiveOrRunningContext));
            var activeKey = saveKey + ".active";
            var saveStore = new TransientCampaignSaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(new TransientActiveSlotStorage(activeKey));
            var launchStageId = StageId.CreateOrThrow(CombinedLaunchStageId);
            var mismatchedStageId = StageId.CreateOrThrow("stage-0-1");
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;

            try
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = mismatchedStageId,
                    CurrentLevelGroupId = "level-0",
                    RemainingChances = 2,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 2,
                    CurrentStageId = launchStageId,
                    CurrentLevelGroupId = "level-01",
                    RemainingChances = 3,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                activeSlotProvider.SetActiveSlot(2);
                Assert.That(
                    handoffStore.TryBegin(
                        1,
                        launchStageId,
                        StageNavigationKind.Continue,
                        "main-menu-continue",
                        out _),
                    Is.True);

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntryForProductionLaunch(installer, launchStageId);
                AssignTimingPresets(installer);
                AssignCampaignStores(installer, saveStore, activeSlotProvider);

                Assert.Throws<TargetInvocationException>(() => BuildConfiguration(installer));

                Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(2));
                Assert.That(
                    ReadInstallerPrivateField<CampaignRunningSlotContext>(
                        installer,
                        "_runningSlotContext"),
                    Is.Null);
                Assert.That(handoffStore.TryPeek(out _), Is.False);
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_Success_ConsumesPendingAndContextAndPinsRunning()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);

            var running = harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId);

            Assert.That(running.SlotNumber, Is.EqualTo(1));
            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
            Assert.That(harness.HandoffStore.ConsumeCount, Is.EqualTo(1));
            Assert.That(harness.ContextStore.ConsumeCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_RunningCreationFailure_RestoresPreviousActive()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.RunningFactory.Fail = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_PendingConsumeFailure_RollsBackActiveAndRunning()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.HandoffStore.FailConsume = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_PendingConsumeException_RollsBackActiveAndRunning()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.HandoffStore.ThrowOnConsume = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_ContextConsumeFailure_RollsBackActiveAndRunning()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.ContextStore.FailConsume = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_ContextConsumeException_RollsBackActiveAndRunning()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.ContextStore.ThrowOnConsume = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_ActiveWriteFailure_LeavesNoRunningAndCleansMatchingState()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.ActiveStorage.FailSetOnCall = 1;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void ActiveStorage_WritesThenThrows_RestoresPreviousActive()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.ActiveStorage.WriteThenThrowSetOnCall = 1;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(2));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void ActiveStorage_WritesThenThrows_WithNoPreviousActive_RestoresEmpty()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: false);
            harness.ActiveStorage.WriteThenThrowSetOnCall = 1;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.TryGetActiveSlot(out _), Is.False);
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void WriteAfterThrow_DoesNotAffectNewerOperation()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            var newerHandoff = new CampaignLaunchHandoff(
                2,
                harness.StageId,
                StageNavigationKind.Continue,
                "newer-operation",
                Guid.NewGuid());
            var newerContext = StageLaunchContext.FromHandoff(newerHandoff);
            harness.ActiveStorage.WriteThenThrowSetOnCall = 1;
            harness.ActiveStorage.AfterWriteBeforeThrow = () =>
            {
                harness.HandoffStore.Set(newerHandoff);
                harness.ContextStore.Set(newerContext);
            };

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.HandoffStore.TryPeek(out var currentHandoff), Is.True);
            Assert.That(currentHandoff, Is.SameAs(newerHandoff));
            Assert.That(harness.ContextStore.IsCurrent(newerContext), Is.True);
        }

        [Test]
        [Category("Full")]
        public void WriteAfterThrow_RestoreFailureSurfacesAggregateFailure()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.ActiveStorage.WriteThenThrowSetOnCall = 1;
            harness.ActiveStorage.FailSetOnCall = 2;

            var exception = Assert.Throws<AggregateException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(exception.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(exception.InnerExceptions[0].Message, Does.Contain("active set failed after write"));
            Assert.That(exception.InnerExceptions[1].Message, Does.Contain("active set failed"));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_PreviousActiveReadFailure_CleansMatchingStateWithoutWriting()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.ActiveStorage.FailRead = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_RollbackWithoutPreviousActive_RestoresEmptyActive()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: false);
            harness.RunningFactory.Fail = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.TryGetActiveSlot(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_RollbackFailure_IsSurfaced()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            harness.RunningFactory.Fail = true;
            harness.ActiveStorage.FailSetOnCall = 2;

            var exception = Assert.Throws<AggregateException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(exception.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(1));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_ContextMismatch_DoesNotCommitOrClearDifferentOwner()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            var newerContext = new StageLaunchContext(
                Guid.NewGuid(),
                2,
                harness.StageId,
                StageNavigationKind.Continue,
                "newer");
            harness.ContextStore.Set(newerContext);

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.HandoffStore.TryPeek(out _), Is.False);
            Assert.That(harness.ContextStore.IsCurrent(newerContext), Is.True);
        }

        [Test]
        [Category("Full")]
        public void SameTokenDifferentSlot_EarlyCleanupPreservesCurrentOwner()
        {
            AssertSameTokenDifferentOwnerPreserved(
                handoff => new CampaignLaunchHandoff(
                    2,
                    handoff.StageId,
                    handoff.NavigationKind,
                    handoff.Source,
                    handoff.Token));
        }

        [Test]
        [Category("Full")]
        public void SameTokenDifferentStage_ProfileFailurePreservesCurrentOwner()
        {
            AssertSameTokenDifferentOwnerPreserved(
                handoff => new CampaignLaunchHandoff(
                    handoff.SlotNumber,
                    StageId.CreateOrThrow("stage-0-1"),
                    handoff.NavigationKind,
                    handoff.Source,
                    handoff.Token));
        }

        [Test]
        [Category("Full")]
        public void SameTokenDifferentNavigation_TransactionCatchPreservesCurrentOwner()
        {
            AssertSameTokenDifferentOwnerPreserved(
                handoff => new CampaignLaunchHandoff(
                    handoff.SlotNumber,
                    handoff.StageId,
                    StageNavigationKind.Retry,
                    handoff.Source,
                    handoff.Token));
        }

        [Test]
        [Category("Full")]
        public void SameTokenDifferentSource_RollbackPreservesCurrentOwner()
        {
            AssertSameTokenDifferentOwnerPreserved(
                handoff => new CampaignLaunchHandoff(
                    handoff.SlotNumber,
                    handoff.StageId,
                    handoff.NavigationKind,
                    "newer-source",
                    handoff.Token));
        }

        [Test]
        [Category("Full")]
        public void MismatchedExpectedHandoffAndContext_DoesNotClearEitherStore()
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            var mismatchedContext = new StageLaunchContext(
                harness.Handoff.Token,
                2,
                harness.Handoff.StageId,
                harness.Handoff.NavigationKind,
                harness.Handoff.Source);
            harness.ContextStore.Set(mismatchedContext);

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                mismatchedContext,
                harness.StageId));

            Assert.That(harness.HandoffStore.TryPeek(out var currentHandoff), Is.True);
            Assert.That(currentHandoff, Is.SameAs(harness.Handoff));
            Assert.That(harness.ContextStore.IsCurrent(mismatchedContext), Is.True);
            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
        }

        [Test]
        [Category("Full")]
        public void CampaignLaunchCommitTransaction_PendinglessRetry_UsesActiveAndConsumesContext()
        {
            var stageId = StageId.CreateOrThrow(CombinedLaunchStageId);
            var saveStore = new TransientCampaignSaveSlotStore(CreateTransientNamespace("pendingless-transaction"));
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "level-01",
            });
            var activeStorage = new FaultingActiveSlotStorage(1);
            var context = StageLaunchContext.CreatePendinglessReload(
                new StageNavigationRequest(stageId, StageNavigationKind.Retry, "stage-result-retry"));
            var contextStore = new RecordingStageLaunchContextCommitStore(context);
            var transaction = new CampaignLaunchCommitTransaction(
                saveStore,
                new ActiveSlotProvider(activeStorage),
                new RecordingCampaignLaunchHandoffStoreForCommit(null),
                contextStore,
                new RecordingRunningSlotContextFactory());

            var running = transaction.CommitPendingless(context, stageId);

            Assert.That(running.SlotNumber, Is.EqualTo(1));
            Assert.That(activeStorage.SetCallCount, Is.EqualTo(0));
            Assert.That(contextStore.TryPeek(out _), Is.False);
        }

        [Test]
        [Category("Full")]
        public void PendinglessNextStage_UsesCommittedActiveAndConsumesContext()
        {
            var harness = CreatePendinglessHarness(
                StageNavigationKind.NextStage,
                "campaign-auto-next");

            var running = harness.Transaction.CommitPendingless(harness.Context, harness.StageId);

            Assert.That(running.SlotNumber, Is.EqualTo(1));
            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessRetry_PinsRunningSlot()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.Retry, "pause-retry");

            var running = harness.Transaction.CommitPendingless(harness.Context, harness.StageId);

            Assert.That(running.SlotNumber, Is.EqualTo(1));
        }

        [Test]
        [Category("Full")]
        public void PendinglessNextStage_PinsRunningSlot()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.NextStage, "campaign-auto-next");

            var running = harness.Transaction.CommitPendingless(harness.Context, harness.StageId);

            Assert.That(running.SlotNumber, Is.EqualTo(1));
        }

        [Test]
        [Category("Full")]
        public void PendinglessSuccess_DoesNotRewritePersistentActive()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.Retry, "campaign-death-retry");

            harness.Transaction.CommitPendingless(harness.Context, harness.StageId);

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(1));
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessContinue_IsRejected()
        {
            AssertPendinglessRejectedWithoutActiveWrite(StageNavigationKind.Continue, "main-menu");
        }

        [Test]
        [Category("Full")]
        public void PendinglessArbitrarySource_IsRejected()
        {
            AssertPendinglessRejectedWithoutActiveWrite(StageNavigationKind.Retry, "arbitrary-source");
        }

        [Test]
        [Category("Full")]
        public void PendinglessStartupWithoutAllowlistedNavigation_IsRejected()
        {
            AssertPendinglessRejectedWithoutActiveWrite(StageNavigationKind.Continue, "startup");
        }

        [Test]
        [Category("Full")]
        public void PendinglessStageMismatch_IsRejected()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.Retry, "stage-result-retry");

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                StageId.CreateOrThrow("stage-0-1")));

            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessMissingActive_IsRejected()
        {
            var harness = CreatePendinglessHarness(
                StageNavigationKind.Retry,
                "stage-result-retry",
                activeSlot: 0);

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessInvalidActive_IsRejected()
        {
            var harness = CreatePendinglessHarness(
                StageNavigationKind.Retry,
                "stage-result-retry",
                activeSlot: CampaignSaveSlotPolicy.SlotCount + 1);

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessEmptyProfile_IsRejected()
        {
            var harness = CreatePendinglessHarness(
                StageNavigationKind.Retry,
                "stage-result-retry",
                saveActiveSlot: false);

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessProfileStageMismatch_IsRejected()
        {
            var harness = CreatePendinglessHarness(
                StageNavigationKind.Retry,
                "stage-result-retry",
                profileStageId: StageId.CreateOrThrow("stage-0-1"));

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessContextConsumeFalse_DoesNotPublishRunning()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.Retry, "stage-result-retry");
            harness.ContextStore.FailConsume = true;
            CampaignRunningSlotContext running = null;

            Assert.Throws<InvalidOperationException>(() =>
                running = harness.Transaction.CommitPendingless(harness.Context, harness.StageId));

            Assert.That(running, Is.Null);
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessContextConsumeThrows_DoesNotPublishRunning()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.Retry, "stage-result-retry");
            harness.ContextStore.ThrowOnConsume = true;
            CampaignRunningSlotContext running = null;

            Assert.Throws<InvalidOperationException>(() =>
                running = harness.Transaction.CommitPendingless(harness.Context, harness.StageId));

            Assert.That(running, Is.Null);
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessProfileLoadThrows_CleansOnlyExactContext()
        {
            var harness = CreatePendinglessHarness(
                StageNavigationKind.Retry,
                "stage-result-retry",
                saveSlotStore: new ThrowingCampaignSaveSlotStore());

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessRunningCreationThrows_CleansOnlyExactContext()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.Retry, "stage-result-retry");
            harness.RunningFactory.Fail = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ContextStore.TryPeek(out _), Is.False);
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessFailure_DoesNotModifyPersistentActive()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.Retry, "stage-result-retry");
            harness.RunningFactory.Fail = true;

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(1));
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void PendinglessOldFailure_DoesNotClearNewerContext()
        {
            var harness = CreatePendinglessHarness(StageNavigationKind.Retry, "stage-result-retry");
            var newerContext = StageLaunchContext.CreatePendinglessReload(
                new StageNavigationRequest(
                    harness.StageId,
                    StageNavigationKind.Retry,
                    "pause-retry"));
            harness.ContextStore.FailConsume = true;
            harness.ContextStore.OnConsume = () => harness.ContextStore.Set(newerContext);

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ContextStore.IsCurrent(newerContext), Is.True);
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_EarlyResolveFailure_ClearsMatchingPendingAndContext()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
            StageLaunchContextStore.Clear();
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_EarlyResolveFailure_ClearsMatchingPendingAndContext");
            try
            {
                var stageId = StageId.CreateOrThrow(CombinedLaunchStageId);
                Assert.That(
                    CampaignLaunchHandoffSessionStore.Instance.TryBegin(
                        1,
                        stageId,
                        StageNavigationKind.Continue,
                        "early-resolve",
                        out var handoff),
                    Is.True);
                Assert.That(
                    StageLaunchContextStore.TrySetCurrent(StageLaunchContext.FromHandoff(handoff)),
                    Is.True);
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();

                Assert.Throws<TargetInvocationException>(() => BuildInitialGameplayState(installer));

                Assert.That(CampaignLaunchHandoffSessionStore.Instance.TryPeek(out _), Is.False);
                Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                StageLaunchContextStore.Clear();
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_CampaignTempContextWithoutTempState_SkipsChanceSource()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_CampaignTempContextWithoutTempState_SkipsChanceSource");
            var launchStageId = StageId.CreateOrThrow(CombinedLaunchStageId);

            try
            {
                CampaignChanceHudDiagnostics.Clear();
                CampaignChanceHudDiagnostics.IsEnabled = true;

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntryForProductionLaunch(installer, launchStageId);
                AssignTimingPresets(installer);
                EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateCampaignTempSlot(launchStageId, remainingChances: 2));

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.CampaignChancesReadSource, Is.Null);
                Assert.That(configuration.DisablePlayerRespawn, Is.False);
                var installerRecord = CampaignChanceHudDiagnostics.Snapshot()
                    .LastOrDefault(record => record.Kind == CampaignChanceHudDiagnosticKind.Installer);
                Assert.That(installerRecord, Is.Not.Null);
                Assert.That(installerRecord.EditorDirectPlayMode, Is.EqualTo(EditorDirectPlayMode.CampaignTempSlot));
                Assert.That(installerRecord.UsesTemporaryCampaignState, Is.True);
                Assert.That(installerRecord.CampaignRuntimeActive, Is.False);
                Assert.That(installerRecord.SourceIsNull, Is.True);
                Assert.That(installerRecord.FailureReason, Is.EqualTo(CampaignChanceReadFailureReason.NoActiveSlot));
                Assert.That(installerRecord.ActiveSlotDiagnosticsKey, Is.EqualTo(CampaignLocalLaunchStateRepository.FileName));
            }
            finally
            {
                CampaignChanceHudDiagnostics.IsEnabled = false;
                CampaignChanceHudDiagnostics.Clear();
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_NonCampaignDirectPlayContext_SuppressesChanceSource()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_NonCampaignDirectPlayContext_SuppressesChanceSource");
            var launchStageId = StageId.CreateOrThrow(CombinedLaunchStageId);

            try
            {
                CampaignChanceHudDiagnostics.Clear();
                CampaignChanceHudDiagnostics.IsEnabled = true;

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntryForProductionLaunch(installer, launchStageId);
                AssignTimingPresets(installer);
                EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(launchStageId));

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.CampaignChancesReadSource, Is.Null);
                Assert.That(configuration.DisablePlayerRespawn, Is.False);
                var installerRecord = CampaignChanceHudDiagnostics.Snapshot()
                    .LastOrDefault(record => record.Kind == CampaignChanceHudDiagnosticKind.Installer);
                Assert.That(installerRecord, Is.Not.Null);
                Assert.That(installerRecord.EditorDirectPlayMode, Is.EqualTo(EditorDirectPlayMode.NonCampaign));
                Assert.That(installerRecord.SuppressCampaignFlow, Is.True);
                Assert.That(installerRecord.UsesTemporaryCampaignState, Is.False);
                Assert.That(installerRecord.CampaignRuntimeActive, Is.False);
                Assert.That(installerRecord.SourceIsNull, Is.True);
                Assert.That(installerRecord.FailureReason, Is.EqualTo(CampaignChanceReadFailureReason.EditorDirectPlaySuppressed));
            }
            finally
            {
                CampaignChanceHudDiagnostics.IsEnabled = false;
                CampaignChanceHudDiagnostics.Clear();
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DirectPlayWithNormalPending_RemainsFailClosed()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
            StageLaunchContextStore.Clear();
            var installerObject = new GameObject("DirectPlayWithNormalPending_RemainsFailClosed");
            var stageId = StageId.CreateOrThrow(CombinedLaunchStageId);
            try
            {
                Assert.That(
                    CampaignLaunchHandoffSessionStore.Instance.TryBegin(
                        1,
                        stageId,
                        StageNavigationKind.Continue,
                        "normal-pending",
                        out var handoff),
                    Is.True);
                Assert.That(
                    StageLaunchContextStore.TrySetCurrent(StageLaunchContext.FromHandoff(handoff)),
                    Is.True);
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntryForProductionLaunch(installer, stageId);
                AssignTimingPresets(installer);
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateNonCampaign(stageId));

                var exception = Assert.Throws<TargetInvocationException>(() => BuildConfiguration(installer));

                Exception rootFailure = exception;
                while (rootFailure.InnerException != null)
                {
                    rootFailure = rootFailure.InnerException;
                }

                Assert.That(rootFailure, Is.TypeOf<InvalidOperationException>());
                Assert.That(rootFailure.Message, Does.Contain("DirectPlay cannot start"));
                Assert.That(
                    ReadInstallerPrivateField<CampaignRunningSlotContext>(installer, "_runningSlotContext"),
                    Is.Null);
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_ConfigurationAndProviderShareSequenceResolver()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_ConfigurationAndProviderShareSequenceResolver");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntry(installer);
                AssignTimingPresets(installer);

                var configuration = BuildConfiguration(installer);
                var provider = (ICampaignStageSequenceResolverProvider)installer;

                Assert.That(configuration.CampaignStageSequenceResolver, Is.Not.Null);
                Assert.That(provider.TryCreateCampaignStageSequenceResolver(out var provided), Is.True);
                Assert.That(provided, Is.SameAs(configuration.CampaignStageSequenceResolver));
            }
            finally
            {
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_Configuration_CarriesStageTileFeatureDefinitions()
        {
            var installerObject = new GameObject("StageBackedGameplaySceneInstaller_Configuration_CarriesStageTileFeatureDefinitions");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntry(installer);
                AssignTimingPresets(installer);
                DisableCampaignFlow(installer);

                var configuration = BuildConfiguration(installer);
                var buildResult = StageRuntimeBuilder.Build(configuration.StageContentEntry.GameplayDefinition);

                CollectionAssert.AreEqual(buildResult.TileFeatureDefinitions, configuration.TileFeatureDefinitions);
            }
            finally
            {
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_Configuration_CarriesStageTileFeatureVisualBindings()
        {
            var installerObject = new GameObject("StageBackedGameplaySceneInstaller_Configuration_CarriesStageTileFeatureVisualBindings");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntry(installer);
                AssignTimingPresets(installer);
                DisableCampaignFlow(installer);

                var configuration = BuildConfiguration(installer);
                var resolvedPresentation =
                    StagePresentationAssembler.Resolve(configuration.StageContentEntry.PresentationDefinition);
                Assert.That(configuration.TileFeaturePresentationBindings, Is.Not.Null);
                var gameplayTileIds = configuration.StageContentEntry.GameplayDefinition.TileFeatures
                    .Select(feature => feature.TileId)
                    .ToArray();
                var configurationTileIds = configuration.TileFeaturePresentationBindings
                    .Select(binding => binding.TileId)
                    .ToArray();
                var resolvedTileIds = resolvedPresentation.TileFeatureBindings
                    .Select(binding => binding.TileId)
                    .ToArray();

                Assert.That(gameplayTileIds, Has.Length.EqualTo(23));
                Assert.That(configurationTileIds, Is.EqualTo(gameplayTileIds));
                Assert.That(resolvedTileIds, Is.EqualTo(gameplayTileIds));
                Assert.That(resolvedTileIds.Distinct().Count(), Is.EqualTo(resolvedTileIds.Length));
            }
            finally
            {
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_Configuration_UsesStagePresentationDefinitionEnemyPresentationArchetypeCatalog()
        {
            var installerObject = new GameObject("StageBackedGameplaySceneInstaller_Configuration_UsesEnemyPresentationArchetypeCatalog");

            try
            {
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntry(installer);
                AssignTimingPresets(installer);

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.EnemyPresentationArchetypeCatalog, Is.Not.Null);
                Assert.That(
                    configuration.EnemyPresentationArchetypeCatalog,
                    Is.SameAs(configuration.StageContentEntry.PresentationDefinition.EnemyPresentationArchetypeCatalog));
                Assert.That(
                    configuration.EnemyPresentationArchetypeCatalog.name,
                    Is.EqualTo("EnemyPresentationArchetypeCatalog_CampaignMainEnemy"));
                Assert.That(configuration.EnemyPresentationArchetypeCatalog.Entries.Length, Is.EqualTo(1));
                Assert.That(
                    configuration.EnemyPresentationArchetypeCatalog.Entries[0].ArchetypeId,
                    Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));

                var enemyAiRuntime = configuration.CreateEnemyAiRuntimeSnapshot();
                var registry = configuration.CreateEnemyPresentationArchetypeRegistry(enemyAiRuntime);

                Assert.That(registry, Is.Not.Null);
                Assert.That(
                    registry.TryGetRuntime(new EnemyUnitArchetypeId("PassiveContactMinion"), out var runtime),
                    Is.True);
                Assert.That(runtime.ViewPrefab, Is.Not.Null);
            }
            finally
            {
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void CampaignStageAssets_WithSummonArchetypes_HaveRuntimeAndPresentationArchetypeCatalogs()
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(StageCatalogProviderAssetPath);
            Assert.That(provider, Is.Not.Null, $"Missing stage catalog provider at '{StageCatalogProviderAssetPath}'.");

            var checkedSummonStages = 0;
            var entries = provider.LoadEntries();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.GameplayDefinition == null || !StageUsesSummonArchetype(entry.GameplayDefinition))
                {
                    continue;
                }

                checkedSummonStages++;
                Assert.That(
                    entry.GameplayDefinition.EnemyUnitArchetypeCatalog,
                    Is.Not.Null,
                    $"{entry.StageId.Value} uses summon archetypes and must assign a runtime enemy unit archetype catalog.");
                Assert.That(
                    entry.PresentationDefinition?.EnemyPresentationArchetypeCatalog,
                    Is.Not.Null,
                    $"{entry.StageId.Value} uses summon archetypes and must assign a presentation enemy archetype catalog.");
            }

            Assert.That(checkedSummonStages, Is.GreaterThan(0));
        }

        [Test]
        [Category("Full")]
        public void StageUsesSummonArchetype_RecognizesProductionBehaviorSummon()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(ArchetypeSummonerProfilePath);

            Assert.That(profile, Is.Not.Null, $"Missing EnemyAiProfile asset at '{ArchetypeSummonerProfilePath}'.");
            Assert.That(profile.BehaviorModuleAssets.OfType<EnemySummonBehaviorModuleAsset>(), Is.Not.Empty);
            Assert.That(
                profile.CapabilityAssets
                    .OfType<EnemyUtilityCapabilityAsset>(),
                Is.Empty);
            Assert.That(ProfileUsesSummonArchetype(profile), Is.True);
        }

        [Test]
        [Category("Full")]
        public void StageUsesSummonArchetype_IgnoresEmptyUtilityCapability()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                UtilityEffects = Array.Empty<EnemyUtilityEffectAuthoring>(),
            });

            Assert.That(ProfileUsesSummonArchetype(profile), Is.False);
        }

        [Test]
        [Category("Full")]
        public void StageUsesSummonArchetype_IgnoresGravityFieldAuraUtilityEffect()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                UtilityEffects = new[] { CreateUtilityEffect(EnemyUtilityEffectKind.GravityFieldAura) },
            });

            Assert.That(ProfileUsesSummonArchetype(profile), Is.False);
        }

        [Test]
        [Category("Full")]
        public void StageUsesSummonArchetype_IgnoresNonSummonAndNullInputs()
        {
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking();

            Assert.That(StageUsesSummonArchetype(null), Is.False);
            Assert.That(ProfileUsesSummonArchetype(null), Is.False);
            Assert.That(ProfileUsesSummonArchetype(profile), Is.False);
        }

        private static StageRuntimeBuildResult BuildCombinedStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");
            return StageRuntimeBuilder.Build(stage);
        }

        private static bool StageUsesSummonArchetype(StageDefinition stage)
        {
            if (stage == null)
            {
                return false;
            }

            var enemySpawns = stage.EnemySpawns;
            for (var spawnIndex = 0; spawnIndex < enemySpawns.Length; spawnIndex++)
            {
                if (ProfileUsesSummonArchetype(enemySpawns[spawnIndex].EnemyAiProfile))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ProfileUsesSummonArchetype(EnemyAiProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var behaviorModules = profile.BehaviorModuleAssets;
            if (behaviorModules != null)
            {
                for (var moduleIndex = 0; moduleIndex < behaviorModules.Count; moduleIndex++)
                {
                    if (behaviorModules[moduleIndex] is EnemySummonBehaviorModuleAsset)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static EnemyUtilityEffectAuthoring CreateUtilityEffect(EnemyUtilityEffectKind kind)
        {
            var effect = new EnemyUtilityEffectAuthoring();
            EnemyAiProfileTestFactory.SetSerializedField(effect, "kind", kind);
            return effect;
        }

        [Test]
        public void CampaignSequence_MissingSerializedSource_FailsAtGameplayBootstrap()
        {
            var root = new GameObject("gameplay-missing-campaign-sequence");
            try
            {
                var installer = root.AddComponent<StageBackedGameplaySceneInstaller>();
                var method = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                    "BuildInitialGameplayState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);

                var exception = Assert.Throws<TargetInvocationException>(() =>
                    method.Invoke(installer, Array.Empty<object>()));

                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception.InnerException.Message, Does.Contain("StageBackedGameplaySceneInstallerBase"));
                Assert.That(exception.InnerException.Message, Does.Contain("authoritative serialized"));
                Assert.That(exception.InnerException.Message, Does.Contain("Component='StageBackedGameplaySceneInstaller'"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampaignSequence_ProviderReturnsCompositionResolverInstance()
        {
            var root = new GameObject("gameplay-shared-campaign-sequence");
            var definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            try
            {
                definition.SetEntries(new[]
                {
                    CreateSequenceEntry("fixture-a", "group-a"),
                    CreateSequenceEntry("fixture-c", "group-b"),
                    CreateSequenceEntry("fixture-b", "group-b"),
                });
                var installer = root.AddComponent<StageBackedGameplaySceneInstaller>();
                SetInstallerField(installer, "campaignStageSequenceDefinition", definition);
                SetInstallerField(installer, "_campaignRuntimeActive", true);
                var provider = (ICampaignStageSequenceResolverProvider)installer;

                Assert.That(provider.TryCreateCampaignStageSequenceResolver(out var first), Is.True);
                Assert.That(provider.TryCreateCampaignStageSequenceResolver(out var second), Is.True);

                Assert.That(second, Is.SameAs(first));
                Assert.That(first.TryGetNext(StageId.CreateOrThrow("fixture-a"), out var next), Is.True);
                Assert.That(next, Is.EqualTo(StageId.CreateOrThrow("fixture-c")));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }

        private static void AssignStageContentEntry(StageBackedGameplaySceneInstaller installer)
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(StageCatalogProviderAssetPath);
            Assert.That(provider, Is.Not.Null, $"Missing stage catalog provider at '{StageCatalogProviderAssetPath}'.");

            var providerField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "stageCatalogProvider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            providerField.SetValue(installer, provider);
            AssignProductionCampaignSequence(installer);

            PrimeCampaignLaunchContext(StageId.CreateOrThrow(CombinedLaunchStageId));
        }

        private static void AssignStageContentEntryForProductionLaunch(
            StageBackedGameplaySceneInstaller installer,
            StageId launchStageId)
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(StageCatalogProviderAssetPath);
            Assert.That(provider, Is.Not.Null, $"Missing stage catalog provider at '{StageCatalogProviderAssetPath}'.");

            var providerField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "stageCatalogProvider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            providerField.SetValue(installer, provider);
            AssignProductionCampaignSequence(installer);

            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
            StageLaunchContextStore.Clear();
            if (CampaignLaunchHandoffSessionStore.Instance.TryPeek(out var handoff))
            {
                Assert.That(StageLaunchContextStore.TrySetCurrent(StageLaunchContext.FromHandoff(handoff)), Is.True);
            }
            else
            {
                Assert.That(
                    StageLaunchContextStore.TrySetCurrent(
                        StageLaunchContext.CreatePendinglessReload(
                            new StageNavigationRequest(
                                launchStageId,
                                StageNavigationKind.Retry,
                                "stage-result-retry"))),
                    Is.True);
            }
        }

        private static void AssignCampaignStores(
            StageBackedGameplaySceneInstaller installer,
            TransientCampaignSaveSlotStore saveStore,
            ActiveSlotProvider activeSlotProvider)
        {
            var saveStoreField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "_saveSlotStore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(saveStoreField, Is.Not.Null);
            saveStoreField.SetValue(installer, saveStore);

            var activeSlotProviderField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "_activeSlotProvider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activeSlotProviderField, Is.Not.Null);
            activeSlotProviderField.SetValue(installer, activeSlotProvider);
        }

        private static void AssignTimingPresets(StageBackedGameplaySceneInstaller installer)
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

        private static void DisableCampaignFlow(StageBackedGameplaySceneInstaller installer)
        {
            var campaignFlowField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "enableCampaignFlow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(campaignFlowField, Is.Not.Null);
            campaignFlowField.SetValue(installer, false);
        }

        private static IGameplayEntityViewFactory CreateViewFactory(
            StageBackedGameplaySceneInstaller installer,
            GameplayBoardRoot boardRoot)
        {
            return CreateViewFactory(installer, boardRoot, out _);
        }

        private static IGameplayEntityViewFactory CreateViewFactory(
            StageBackedGameplaySceneInstaller installer,
            GameplayBoardRoot boardRoot,
            out int playerEntityId)
        {
            AssignStageContentEntry(installer);
            AssignTimingPresets(installer);
            var initialState = BuildInitialGameplayState(installer);
            var playerEntityIdProperty = initialState.GetType().GetProperty(
                "PlayerEntityId",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(playerEntityIdProperty, Is.Not.Null);
            playerEntityId = (int)playerEntityIdProperty.GetValue(initialState);

            var factoryMethod = installer.GetType().GetMethod(
                "CreateViewFactory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(factoryMethod, Is.Not.Null);
            return (IGameplayEntityViewFactory)factoryMethod.Invoke(installer, new[] { (object)boardRoot, initialState });
        }

        private static void DestroyAssignedStageContent(GameObject installerObject)
        {
            ClearCampaignLaunchContext();

            if (installerObject == null)
            {
                return;
            }

            var installer = installerObject.GetComponent<StageBackedGameplaySceneInstaller>();
            if (installer == null)
            {
                return;
            }

            var field = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "stageContentEntry",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.GetValue(installer) is not StageContentEntry entry || entry == null)
            {
                return;
            }

            if (entry.PresentationDefinition != null)
            {
                Object.DestroyImmediate(entry.PresentationDefinition);
            }

            Object.DestroyImmediate(entry);
        }

        private static void PrimeCampaignLaunchContext(StageId launchStageId)
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();

            var saveStore = CampaignSaveCompositionProvider.CreateTemporaryProfileBacked();
            var activeSlotProvider = CampaignSaveCompositionProvider.CreateTemporaryActiveSlotProvider(saveStore);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = launchStageId,
                CurrentLevelGroupId = "level-01",
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            activeSlotProvider.SetActiveSlot(1);

            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateCampaignTempSlot(
                    launchStageId,
                    CampaignSaveSlotPolicy.DefaultRemainingChances));
            StageLaunchContextStore.SetCurrent(launchStageId);
        }

        private static void ClearCampaignLaunchContext()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
        }

        private static object BuildInitialGameplayState(StageBackedGameplaySceneInstaller installer)
        {
            AssignProductionCampaignSequence(installer);
            var buildInitialStateMethod = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                "BuildInitialGameplayState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildInitialStateMethod, Is.Not.Null);
            return buildInitialStateMethod.Invoke(installer, Array.Empty<object>());
        }

        private static void AssignProductionCampaignSequence(StageBackedGameplaySceneInstaller installer)
        {
            var field = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "campaignStageSequenceDefinition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            if (field.GetValue(installer) != null)
            {
                return;
            }

            var definition = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);
            Assert.That(
                definition,
                Is.Not.Null,
                $"Missing campaign sequence at '{StageContentPaths.CampaignStageSequenceAssetPath}'.");
            field.SetValue(installer, definition);
        }

        private static CampaignStageSequenceEntry CreateSequenceEntry(
            string stageId,
            string levelGroupId)
        {
            var entry = new CampaignStageSequenceEntry();
            entry.Set(
                StageId.CreateOrThrow(stageId),
                levelGroupId);
            return entry;
        }

        private static void SetInstallerField(
            StageBackedGameplaySceneInstaller installer,
            string fieldName,
            object value)
        {
            var field = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(installer, value);
        }

        private static GameplaySceneHostConfiguration BuildConfiguration(StageBackedGameplaySceneInstaller installer)
        {
            EnsureCameraTopologyAuthoring(installer);
            if (installer.GetComponent<TestTerminalSessionAuthorityProvider>() == null)
            {
                installer.gameObject.AddComponent<TestTerminalSessionAuthorityProvider>();
            }

            var initialState = BuildInitialGameplayState(installer);
            var createConfigurationMethod = typeof(GameplayShowcaseSceneInstallerBase).GetMethod(
                "CreateConfiguration",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { initialState.GetType(), typeof(GameplayCameraSettings) },
                modifiers: null);

            Assert.That(createConfigurationMethod, Is.Not.Null);
            return (GameplaySceneHostConfiguration)createConfigurationMethod.Invoke(
                installer,
                new object[] { initialState, installer.GetCameraSettings() });
        }

        private static GameplayCameraTopologyAuthoring EnsureCameraTopologyAuthoring(Component owner)
        {
            return owner.GetComponent<GameplayCameraTopologyAuthoring>() ??
                   owner.gameObject.AddComponent<GameplayCameraTopologyAuthoring>();
        }

        private static T ReadInstallerPrivateField<T>(
            StageBackedGameplaySceneInstaller installer,
            string fieldName)
        {
            var field = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(installer);
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

        private static string CreateTransientNamespace(string suffix)
        {
            return "Game.Feature.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
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

        private static CommitTransactionHarness CreateCommitTransactionHarness(bool hasPreviousActive)
        {
            var stageId = StageId.CreateOrThrow(CombinedLaunchStageId);
            var saveStore = new TransientCampaignSaveSlotStore(CreateTransientNamespace("commit-transaction"));
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "level-01",
            });
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 2,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "level-01",
            });
            var handoff = new CampaignLaunchHandoff(
                1,
                stageId,
                StageNavigationKind.Continue,
                "transaction-test",
                Guid.NewGuid());
            var context = StageLaunchContext.FromHandoff(handoff);
            var handoffStore = new RecordingCampaignLaunchHandoffStoreForCommit(handoff);
            var contextStore = new RecordingStageLaunchContextCommitStore(context);
            var activeStorage = new FaultingActiveSlotStorage(hasPreviousActive ? 2 : 0);
            var runningFactory = new RecordingRunningSlotContextFactory();
            var transaction = new CampaignLaunchCommitTransaction(
                saveStore,
                new ActiveSlotProvider(activeStorage),
                handoffStore,
                contextStore,
                runningFactory);
            return new CommitTransactionHarness(
                stageId,
                handoff,
                context,
                handoffStore,
                contextStore,
                activeStorage,
                runningFactory,
                transaction);
        }

        private static void AssertSameTokenDifferentOwnerPreserved(
            Func<CampaignLaunchHandoff, CampaignLaunchHandoff> createNewerHandoff)
        {
            var harness = CreateCommitTransactionHarness(hasPreviousActive: true);
            var newerHandoff = createNewerHandoff(harness.Handoff);
            Assert.That(newerHandoff.Matches(harness.Handoff), Is.False);
            var newerContext = StageLaunchContext.FromHandoff(newerHandoff);
            harness.HandoffStore.Set(newerHandoff);
            harness.ContextStore.Set(newerContext);

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPending(
                harness.Handoff,
                harness.Context,
                harness.StageId));

            Assert.That(harness.HandoffStore.TryPeek(out var currentHandoff), Is.True);
            Assert.That(currentHandoff, Is.SameAs(newerHandoff));
            Assert.That(harness.ContextStore.IsCurrent(newerContext), Is.True);
            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(2));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        private static PendinglessTransactionHarness CreatePendinglessHarness(
            StageNavigationKind navigationKind,
            string source,
            int activeSlot = 1,
            bool saveActiveSlot = true,
            StageId profileStageId = default,
            ICampaignSaveSlotStore saveSlotStore = null)
        {
            var stageId = StageId.CreateOrThrow(CombinedLaunchStageId);
            var store = saveSlotStore ?? new TransientCampaignSaveSlotStore(CreateTransientNamespace("pendingless-matrix"));
            if (saveActiveSlot && saveSlotStore == null)
            {
                store.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = profileStageId.IsValid ? profileStageId : stageId,
                    CurrentLevelGroupId = "level-01",
                });
            }

            var activeStorage = new FaultingActiveSlotStorage(activeSlot);
            var context = StageLaunchContext.CreatePendinglessReload(
                new StageNavigationRequest(stageId, navigationKind, source));
            var contextStore = new RecordingStageLaunchContextCommitStore(context);
            var runningFactory = new RecordingRunningSlotContextFactory();
            var transaction = new CampaignLaunchCommitTransaction(
                store,
                new ActiveSlotProvider(activeStorage),
                new RecordingCampaignLaunchHandoffStoreForCommit(null),
                contextStore,
                runningFactory);
            return new PendinglessTransactionHarness(
                stageId,
                context,
                contextStore,
                activeStorage,
                runningFactory,
                transaction);
        }

        private static void AssertPendinglessRejectedWithoutActiveWrite(
            StageNavigationKind navigationKind,
            string source)
        {
            var harness = CreatePendinglessHarness(navigationKind, source);

            Assert.Throws<InvalidOperationException>(() => harness.Transaction.CommitPendingless(
                harness.Context,
                harness.StageId));

            Assert.That(harness.ActiveStorage.CurrentSlot, Is.EqualTo(1));
            Assert.That(harness.ActiveStorage.SetCallCount, Is.EqualTo(0));
            Assert.That(harness.RunningFactory.CreateCount, Is.EqualTo(0));
        }

        private sealed class CommitTransactionHarness
        {
            public CommitTransactionHarness(
                StageId stageId,
                CampaignLaunchHandoff handoff,
                StageLaunchContext context,
                RecordingCampaignLaunchHandoffStoreForCommit handoffStore,
                RecordingStageLaunchContextCommitStore contextStore,
                FaultingActiveSlotStorage activeStorage,
                RecordingRunningSlotContextFactory runningFactory,
                CampaignLaunchCommitTransaction transaction)
            {
                StageId = stageId;
                Handoff = handoff;
                Context = context;
                HandoffStore = handoffStore;
                ContextStore = contextStore;
                ActiveStorage = activeStorage;
                RunningFactory = runningFactory;
                Transaction = transaction;
            }

            public StageId StageId { get; }
            public CampaignLaunchHandoff Handoff { get; }
            public StageLaunchContext Context { get; }
            public RecordingCampaignLaunchHandoffStoreForCommit HandoffStore { get; }
            public RecordingStageLaunchContextCommitStore ContextStore { get; }
            public FaultingActiveSlotStorage ActiveStorage { get; }
            public RecordingRunningSlotContextFactory RunningFactory { get; }
            public CampaignLaunchCommitTransaction Transaction { get; }
        }

        private sealed class PendinglessTransactionHarness
        {
            public PendinglessTransactionHarness(
                StageId stageId,
                StageLaunchContext context,
                RecordingStageLaunchContextCommitStore contextStore,
                FaultingActiveSlotStorage activeStorage,
                RecordingRunningSlotContextFactory runningFactory,
                CampaignLaunchCommitTransaction transaction)
            {
                StageId = stageId;
                Context = context;
                ContextStore = contextStore;
                ActiveStorage = activeStorage;
                RunningFactory = runningFactory;
                Transaction = transaction;
            }

            public StageId StageId { get; }
            public StageLaunchContext Context { get; }
            public RecordingStageLaunchContextCommitStore ContextStore { get; }
            public FaultingActiveSlotStorage ActiveStorage { get; }
            public RecordingRunningSlotContextFactory RunningFactory { get; }
            public CampaignLaunchCommitTransaction Transaction { get; }
        }

        private sealed class FaultingActiveSlotStorage : IActiveSlotStorage
        {
            public FaultingActiveSlotStorage(int currentSlot)
            {
                CurrentSlot = currentSlot;
            }

            public string DiagnosticsKey => "faulting-active";
            public int CurrentSlot { get; private set; }
            public int SetCallCount { get; private set; }
            public int FailSetOnCall { get; set; }
            public int WriteThenThrowSetOnCall { get; set; }
            public Action AfterWriteBeforeThrow { get; set; }
            public bool FailRead { get; set; }
            public bool FailClear { get; set; }

            public bool TryGetActiveSlot(out int slotNumber)
            {
                if (FailRead)
                {
                    throw new InvalidOperationException("active read failed");
                }

                slotNumber = CurrentSlot;
                return CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber);
            }

            public void SetActiveSlot(int slotNumber)
            {
                SetCallCount++;
                if (FailSetOnCall == SetCallCount)
                {
                    throw new InvalidOperationException("active set failed");
                }

                CurrentSlot = slotNumber;
                if (WriteThenThrowSetOnCall == SetCallCount)
                {
                    AfterWriteBeforeThrow?.Invoke();
                    throw new InvalidOperationException("active set failed after write");
                }
            }

            public void ClearActiveSlot()
            {
                if (FailClear)
                {
                    throw new InvalidOperationException("active clear failed");
                }

                CurrentSlot = 0;
            }
        }

        private sealed class RecordingCampaignLaunchHandoffStoreForCommit : ICampaignLaunchHandoffStore
        {
            private CampaignLaunchHandoff _pending;

            public RecordingCampaignLaunchHandoffStoreForCommit(CampaignLaunchHandoff pending)
            {
                _pending = pending;
            }

            public bool FailConsume { get; set; }
            public bool ThrowOnConsume { get; set; }
            public int ConsumeCount { get; private set; }

            public void Set(CampaignLaunchHandoff handoff)
            {
                _pending = handoff;
            }

            public bool TryBegin(
                int slotNumber,
                StageId stageId,
                StageNavigationKind navigationKind,
                string source,
                out CampaignLaunchHandoff handoff)
            {
                handoff = _pending;
                return false;
            }

            public bool TryPeek(out CampaignLaunchHandoff handoff)
            {
                handoff = _pending;
                return handoff != null;
            }

            public bool TryClear(Guid token)
            {
                if (_pending == null || _pending.Token != token)
                {
                    return false;
                }

                _pending = null;
                return true;
            }

            public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
            {
                ConsumeCount++;
                if (ThrowOnConsume)
                {
                    throw new InvalidOperationException("pending consume failed");
                }

                if (FailConsume || _pending == null || _pending.Token != token)
                {
                    handoff = null;
                    return false;
                }

                handoff = _pending;
                _pending = null;
                return true;
            }
        }

        private sealed class RecordingStageLaunchContextCommitStore : IStageLaunchContextCommitStore
        {
            private StageLaunchContext _current;

            public RecordingStageLaunchContextCommitStore(StageLaunchContext current)
            {
                _current = current;
            }

            public bool FailConsume { get; set; }
            public bool ThrowOnConsume { get; set; }
            public Action OnConsume { get; set; }
            public int ConsumeCount { get; private set; }

            public void Set(StageLaunchContext context)
            {
                _current = context;
            }

            public bool TryPeek(out StageLaunchContext context)
            {
                context = _current;
                return context != null;
            }

            public bool IsCurrent(StageLaunchContext expected)
            {
                return _current != null && _current.Equals(expected);
            }

            public bool TryClear(StageLaunchContext expected)
            {
                if (!IsCurrent(expected))
                {
                    return false;
                }

                _current = null;
                return true;
            }

            public bool TryConsume(StageLaunchContext expected, out StageLaunchContext consumed)
            {
                ConsumeCount++;
                OnConsume?.Invoke();
                if (ThrowOnConsume)
                {
                    throw new InvalidOperationException("context consume failed");
                }

                if (FailConsume || !IsCurrent(expected))
                {
                    consumed = null;
                    return false;
                }

                consumed = _current;
                _current = null;
                return true;
            }
        }

        private sealed class ThrowingCampaignSaveSlotStore : ICampaignSaveSlotStore
        {
            public string DiagnosticsKey => "throwing-profile";
            public CampaignSaveLoadReport LastCampaignLoadReport =>
                CampaignSaveLoadReport.Missing("profile load throws");

            public SaveSlotData[] LoadAll() => throw new NotSupportedException();
            public CampaignSaveLoadResult LoadAllWithReport() => throw new NotSupportedException();
            public SaveSlotData LoadSlot(int slotNumber) =>
                throw new InvalidOperationException("profile load failed");
            public void SaveSlot(SaveSlotData slot) => throw new NotSupportedException();
            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt) => throw new NotSupportedException();
            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation) =>
                throw new NotSupportedException();
            public void DeleteSlot(int slotNumber) => throw new NotSupportedException();
            public void ClearAll() => throw new NotSupportedException();
        }

        private sealed class RecordingRunningSlotContextFactory : ICampaignRunningSlotContextFactory
        {
            public bool Fail { get; set; }
            public int CreateCount { get; private set; }

            public CampaignRunningSlotContext Create(int slotNumber)
            {
                CreateCount++;
                if (Fail)
                {
                    throw new InvalidOperationException("running context creation failed");
                }

                return new CampaignRunningSlotContext(slotNumber);
            }
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
            IReadOnlyList<EnemyPresentationBinding> bindings,
            int entityId,
            out EnemyPresentationBinding binding)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                var entry = bindings[i];
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

    internal sealed class TestTerminalSessionAuthorityProvider : MonoBehaviour,
        ITerminalSessionAuthorityProvider
    {
        public bool TryGetTerminalSessionAuthority(
            out ITerminalSessionReadModel readModel,
            out ITerminalSessionAuthority authority)
        {
            authority = TerminalSessionRegistry.Authority;
            readModel = authority;
            return true;
        }
    }
}
