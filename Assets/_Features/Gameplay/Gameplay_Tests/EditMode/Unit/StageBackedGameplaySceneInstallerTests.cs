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
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-4-2/stage-4-2.asset";
        private const string CombinedPresentationAssetPath =
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-4-2/stage-4-2_Presentation.asset";
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
            Assert.That(showcaseProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.GlideOverSolid));
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
            Assert.That(glideChaserProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.GlideOverSolid));
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

                Assert.That(flags.EnablePlayerFree2DLocalLocomotion, Is.True);
                Assert.That(flags.EnablePlayerFree2DActionAssist, Is.True);
                Assert.That(flags.EnablePlayerSameFaceContinuousLocomotion, Is.True);
                Assert.That(flags.EnablePlayerStoppableKinematicLocomotion, Is.True);
                Assert.That(flags.EnableEnemySameFaceContinuousLocomotion, Is.True);
                Assert.That(flags.EnableEnemyChargeKinematicLocomotion, Is.True);
                Assert.That(flags.EnableEnemyGlideKinematicLocomotion, Is.True);
                Assert.That(flags.RemovedLegacyFallbackDiagnosticsEnabled, Is.False);
                Assert.That(configuration.PlayerContinuousLocomotion.ActionAssistSettleWindowCells, Is.EqualTo(0.3125f));
                Assert.That(configuration.PlayerContinuousLocomotion.CollisionRadiusCells, Is.EqualTo(0.25f));
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
        public void StageBackedGameplaySceneInstaller_ProductionCampaignLaunch_InjectsChanceReadSource()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_ProductionCampaignLaunch_InjectsChanceReadSource");
            var saveKey = CreatePrefsKey(nameof(StageBackedGameplaySceneInstaller_ProductionCampaignLaunch_InjectsChanceReadSource));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
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

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntryForProductionLaunch(installer, launchStageId);
                AssignTimingPresets(installer);
                AssignCampaignStores(installer, saveStore, activeSlotProvider);

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.DisablePlayerRespawn, Is.True);
                Assert.That(configuration.CampaignChancesReadSource, Is.Not.Null);
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
        public void StageBackedGameplaySceneInstaller_StaleCampaignTempDirectPlayContext_SkipsProductionChanceSource()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_StaleCampaignTempDirectPlayContext_SkipsProductionChanceSource");
            var defaultActiveSlotKey = new ActiveSlotProvider().PlayerPrefsKey;
            var saveBackup = PlayerPrefsStringBackup.Capture(SaveSlotStore.DefaultPlayerPrefsKey);
            var activeBackup = PlayerPrefsIntBackup.Capture(defaultActiveSlotKey);
            var launchStageId = StageId.CreateOrThrow(CombinedLaunchStageId);

            try
            {
                CampaignChanceHudDiagnostics.Clear();
                CampaignChanceHudDiagnostics.IsEnabled = true;

                var productionSaveStore = new SaveSlotStore();
                var productionActiveSlotProvider = new ActiveSlotProvider();
                productionSaveStore.ClearAll();
                productionActiveSlotProvider.ClearActiveSlot();
                productionSaveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = launchStageId,
                    CurrentLevelGroupId = "level-01",
                    RemainingChances = 2,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                productionActiveSlotProvider.SetActiveSlot(1);

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                AssignStageContentEntryForProductionLaunch(installer, launchStageId);
                AssignTimingPresets(installer);
                EditorDirectPlayContextStore.ClearTempDirectPlaySave();
                EditorDirectPlayContextStore.SetCurrent(
                    EditorDirectPlayContext.CreateCampaignTempSlot(launchStageId, remainingChances: 2));

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.CampaignChancesReadSource, Is.Null);
                Assert.That(configuration.DisablePlayerRespawn, Is.False);
                var installerRecord = CampaignChanceHudDiagnostics.Snapshot()
                    .LastOrDefault(record => record.Kind == CampaignChanceHudDiagnosticKind.Installer);
                Assert.That(installerRecord, Is.Not.Null);
                Assert.That(installerRecord.EditorDirectPlayMode, Is.EqualTo(EditorDirectPlayMode.CampaignTempSlot));
                Assert.That(installerRecord.HasCustomSaveNamespace, Is.True);
                Assert.That(installerRecord.HasActiveSlot, Is.False);
                Assert.That(installerRecord.CampaignRuntimeActive, Is.False);
                Assert.That(installerRecord.SourceIsNull, Is.True);
                Assert.That(installerRecord.FailureReason, Is.EqualTo(CampaignChanceReadFailureReason.NoActiveSlot));
                Assert.That(installerRecord.ActiveSlotProviderKey, Is.EqualTo(EditorDirectPlayContextStore.TempActiveSlotProviderKey));
            }
            finally
            {
                CampaignChanceHudDiagnostics.IsEnabled = false;
                CampaignChanceHudDiagnostics.Clear();
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTempDirectPlaySave();
                saveBackup.Restore();
                activeBackup.Restore();
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void StageBackedGameplaySceneInstaller_StaleNonCampaignDirectPlayContext_SuppressesProductionChanceSource()
        {
            var installerObject = new GameObject(
                "StageBackedGameplaySceneInstaller_StaleNonCampaignDirectPlayContext_SuppressesProductionChanceSource");
            var defaultActiveSlotKey = new ActiveSlotProvider().PlayerPrefsKey;
            var saveBackup = PlayerPrefsStringBackup.Capture(SaveSlotStore.DefaultPlayerPrefsKey);
            var activeBackup = PlayerPrefsIntBackup.Capture(defaultActiveSlotKey);
            var launchStageId = StageId.CreateOrThrow(CombinedLaunchStageId);

            try
            {
                CampaignChanceHudDiagnostics.Clear();
                CampaignChanceHudDiagnostics.IsEnabled = true;

                var productionSaveStore = new SaveSlotStore();
                var productionActiveSlotProvider = new ActiveSlotProvider();
                productionSaveStore.ClearAll();
                productionActiveSlotProvider.ClearActiveSlot();
                productionSaveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = launchStageId,
                    CurrentLevelGroupId = "level-01",
                    RemainingChances = 2,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                productionActiveSlotProvider.SetActiveSlot(1);

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
                Assert.That(installerRecord.HasCustomSaveNamespace, Is.False);
                Assert.That(installerRecord.HasActiveSlot, Is.True);
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
                EditorDirectPlayContextStore.ClearTempDirectPlaySave();
                saveBackup.Restore();
                activeBackup.Restore();
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
                Assert.That(
                    configuration.TileFeaturePresentationBindings.Select(binding => binding.TileId).ToArray(),
                    Is.EqualTo(configuration.StageContentEntry.GameplayDefinition.TileFeatures.Select(feature => feature.TileId).ToArray()));
                Assert.That(
                    resolvedPresentation.TileFeatureBindings.Select(binding => binding.TileId).ToArray(),
                    Is.EqualTo(new[] { 905 }));
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

        private static void AssignStageContentEntry(StageBackedGameplaySceneInstaller installer)
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(StageCatalogProviderAssetPath);
            Assert.That(provider, Is.Not.Null, $"Missing stage catalog provider at '{StageCatalogProviderAssetPath}'.");

            var providerField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "stageCatalogProvider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            providerField.SetValue(installer, provider);

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

            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            StageLaunchContextStore.SetCurrent(launchStageId);
        }

        private static void AssignCampaignStores(
            StageBackedGameplaySceneInstaller installer,
            SaveSlotStore saveStore,
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
            AssignStageContentEntry(installer);
            AssignTimingPresets(installer);
            var initialState = BuildInitialGameplayState(installer);

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
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();

            var saveStore = new SaveSlotStore(EditorDirectPlayContextStore.TempSaveSlotStoreKey);
            var activeSlotProvider = new ActiveSlotProvider(EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = launchStageId,
                CurrentLevelGroupId = "level-01",
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            activeSlotProvider.SetActiveSlot(1);

            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateCampaignTempSlot(
                    launchStageId,
                    SaveSlotStore.DefaultRemainingChances));
            StageLaunchContextStore.SetCurrent(launchStageId);
        }

        private static void ClearCampaignLaunchContext()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
        }

        private static object BuildInitialGameplayState(StageBackedGameplaySceneInstaller installer)
        {
            var buildInitialStateMethod = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                "BuildInitialGameplayState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildInitialStateMethod, Is.Not.Null);
            return buildInitialStateMethod.Invoke(installer, Array.Empty<object>());
        }

        private static GameplaySceneHostConfiguration BuildConfiguration(StageBackedGameplaySceneInstaller installer)
        {
            EnsureCameraTopologyAuthoring(installer);

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

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
        }

        private readonly struct PlayerPrefsStringBackup
        {
            private readonly bool _hadValue;
            private readonly string _key;
            private readonly string _value;

            private PlayerPrefsStringBackup(string key, bool hadValue, string value)
            {
                _key = key;
                _hadValue = hadValue;
                _value = value;
            }

            public static PlayerPrefsStringBackup Capture(string key)
            {
                return new PlayerPrefsStringBackup(
                    key,
                    PlayerPrefs.HasKey(key),
                    PlayerPrefs.GetString(key, string.Empty));
            }

            public void Restore()
            {
                if (_hadValue)
                {
                    PlayerPrefs.SetString(_key, _value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(_key);
                }

                PlayerPrefs.Save();
            }
        }

        private readonly struct PlayerPrefsIntBackup
        {
            private readonly bool _hadValue;
            private readonly string _key;
            private readonly int _value;

            private PlayerPrefsIntBackup(string key, bool hadValue, int value)
            {
                _key = key;
                _hadValue = hadValue;
                _value = value;
            }

            public static PlayerPrefsIntBackup Capture(string key)
            {
                return new PlayerPrefsIntBackup(
                    key,
                    PlayerPrefs.HasKey(key),
                    PlayerPrefs.GetInt(key, 0));
            }

            public void Restore()
            {
                if (_hadValue)
                {
                    PlayerPrefs.SetInt(_key, _value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(_key);
                }

                PlayerPrefs.Save();
            }
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
}
