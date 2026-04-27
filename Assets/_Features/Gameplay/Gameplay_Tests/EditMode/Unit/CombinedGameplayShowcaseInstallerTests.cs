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
            "Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase.asset";
        private const string CombinedPresentationAssetPath =
            "Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase_Presentation.asset";
        private const string StageCatalogProviderAssetPath =
            "Assets/_Features/Stages/Content/StageCatalogProvider.asset";
        private const string DefaultSimulationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplaySimulationTimingPreset_DefaultShowcase.asset";
        private const string DefaultPresentationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplayPresentationTimingPreset_DefaultShowcase.asset";
        private const int ConfiguredShowcaseEnemyId = 54;
        private const int NonAttackingShowcaseEnemyId = 55;
        private const int WallFollowerShowcaseEnemyId = 56;
        private const int JumpShowcaseEnemyId = 57;
        private const int ChargeShowcaseEnemyId = 58;
        private const int UtilitySummonerShowcaseEnemyId = 59;
        private const string AttackingEnemyPresentationId = "Attacking_showcase";
        private const string NonAttackingEnemyPresentationId = "nonAttacking_showcase";
        private const string WallFollowerEnemyPresentationId = "wallFollower_sun";
        private const string JumpChaserEnemyPresentationId = "jumpChaser_astra";
        private const string ChargeEnemyPresentationId = "Charge_showcase";

        [Test]
        [Category("Full")]
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
        [Category("Full")]
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
        [Category("Full")]
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
        [Category("Full")]
        public void CombinedGameplayStage_BuildsEnemyProfileOverrideForConfiguredShowcaseEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(buildResult.EnemyAiProfileOverrides, Is.Not.Null);
            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(6));

            Assert.That(TryGetProfileOverride(buildResult, ConfiguredShowcaseEnemyId, out var showcaseProfile), Is.True);
            Assert.That(showcaseProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(showcaseProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(showcaseProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
            Assert.That(showcaseProfile.AttackTimingSettings.WindupSeconds, Is.EqualTo(1f));
            Assert.That(showcaseProfile.LocomotionTimingSettings.MoveCooldownSeconds, Is.EqualTo(1f));
        }

        [Test]
        [Category("Full")]
        public void CombinedGameplayStage_BuildsRandomWalkPilotProfileOverrideForWindupMeleeEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, ConfiguredShowcaseEnemyId, out var windupPilotProfile), Is.True);
            Assert.That(windupPilotProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(windupPilotProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
            Assert.That(windupPilotProfile.PatrolSettings.LeashRadius, Is.EqualTo(1));
            Assert.That(windupPilotProfile.PatrolSettings.ForwardWeight, Is.EqualTo(6));
            Assert.That(windupPilotProfile.PatrolSettings.SideWeight, Is.EqualTo(1));
            Assert.That(windupPilotProfile.PatrolSettings.BackwardWeight, Is.EqualTo(1));
            Assert.That(windupPilotProfile.PatrolSettings.PreventImmediateBacktrack, Is.True);
            Assert.That(windupPilotProfile.AttackTimingSettings.WindupSeconds, Is.EqualTo(1f));
            Assert.That(windupPilotProfile.LocomotionTimingSettings.MoveCooldownSeconds, Is.EqualTo(1f));
        }

        [Test]
        [Category("Full")]
        public void CombinedGameplayStage_BuildsRandomWalkPilotProfileOverrideForNonAttackingEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, NonAttackingShowcaseEnemyId, out var nonAttackingProfile), Is.True);
            Assert.That(nonAttackingProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(nonAttackingProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(nonAttackingProfile.PatrolSettings.LeashRadius, Is.EqualTo(2));
            Assert.That(nonAttackingProfile.PatrolSettings.ForwardWeight, Is.EqualTo(4));
            Assert.That(nonAttackingProfile.PatrolSettings.SideWeight, Is.EqualTo(2));
            Assert.That(nonAttackingProfile.PatrolSettings.BackwardWeight, Is.EqualTo(1));
            Assert.That(nonAttackingProfile.PatrolSettings.PreventImmediateBacktrack, Is.True);
        }

        [Test]
        [Category("Full")]
        public void CombinedGameplayStage_BuildsArchetypeSummonerProfileOverrideForUtilitySummonerEnemy()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, UtilitySummonerShowcaseEnemyId, out var summonerProfile), Is.True);
            Assert.That(summonerProfile, Is.Not.Null);
            Assert.That(summonerProfile.name, Is.EqualTo("EnemyAi_ArchetypeSummoner"));

            var runtimeDefinition = summonerProfile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(runtimeDefinition.Capabilities.TryGetUtility(out var utility), Is.True);
            Assert.That(utility.Effects, Is.Not.Empty);
            Assert.That(utility.Effects[0].Kind, Is.EqualTo(EnemyUtilityEffectKind.SummonMinion));
            Assert.That(
                utility.Effects[0].Summon.SummonedArchetypeId,
                Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));
            Assert.That(utility.Effects[0].Summon.OverrideHp, Is.False);
        }

        [Test]
        [Category("Full")]
        public void CombinedGameplayStage_BuildsEnemyPresentationBindingForConfiguredShowcaseEnemy()
        {
            var presentationDefinition = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(CombinedPresentationAssetPath);
            Assert.That(
                presentationDefinition,
                Is.Not.Null,
                $"Missing stage presentation asset at '{CombinedPresentationAssetPath}'.");

            var presentation = StagePresentationAssembler.Resolve(presentationDefinition);

            Assert.That(presentation.EnemyPresentationBindings, Is.Not.Null);
            Assert.That(presentation.EnemyPresentationBindings.Length, Is.EqualTo(6));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, ConfiguredShowcaseEnemyId, out var configuredBinding), Is.True);
            Assert.That(configuredBinding.PresentationId, Is.EqualTo(AttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, WallFollowerShowcaseEnemyId, out var wallFollowerBinding), Is.True);
            Assert.That(wallFollowerBinding.PresentationId, Is.EqualTo(WallFollowerEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, JumpShowcaseEnemyId, out var jumpBinding), Is.True);
            Assert.That(jumpBinding.PresentationId, Is.EqualTo(JumpChaserEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, ChargeShowcaseEnemyId, out var chargeBinding), Is.True);
            Assert.That(chargeBinding.PresentationId, Is.EqualTo(ChargeEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, UtilitySummonerShowcaseEnemyId, out var utilitySummonerBinding), Is.True);
            Assert.That(utilitySummonerBinding.PresentationId, Is.EqualTo("utility_summoner_prefab"));
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
        public void CombinedGameplayStage_PlacesWallFollowerShowcaseEnemyAtConfiguredPatrolLane()
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
        [Category("Full")]
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
        [Category("Full")]
        public void CombinedGameplayStage_BuildsJumpShowcaseProfileOverride()
        {
            var buildResult = BuildCombinedStage();

            Assert.That(TryGetProfileOverride(buildResult, JumpShowcaseEnemyId, out var jumpProfile), Is.True);
            Assert.That(jumpProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(jumpProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
            Assert.That(jumpProfile.JumpTimingSettings.WindupSeconds, Is.EqualTo(0.35f));
            Assert.That(jumpProfile.JumpTimingSettings.AirborneSeconds, Is.EqualTo(1f));
            Assert.That(jumpProfile.JumpTimingSettings.CooldownSeconds, Is.EqualTo(3f));
        }

        [Test]
        [Category("Full")]
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
        [Category("Full")]
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
        [Category("Full")]
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
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(playerPrefabObject);
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
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
        [Category("Full")]
        public void GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator()
        {
            var parentObject = new GameObject("GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator");
            var prefabObject = new GameObject("GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab");

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
        [Category("Full")]
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
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(presenterObject);
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void CombinedGameplayShowcaseInstaller_Configuration_UsesStageDefinitionEnemyUnitArchetypeCatalog()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_Configuration_UsesEnemyUnitArchetypeCatalog");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                AssignStageContentEntry(installer);
                AssignTimingPresets(installer);

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.EnemyUnitArchetypeCatalog, Is.Not.Null);
                Assert.That(
                    configuration.EnemyUnitArchetypeCatalog,
                    Is.SameAs(configuration.StageContentEntry.GameplayDefinition.EnemyUnitArchetypeCatalog));
                Assert.That(configuration.EnemyUnitArchetypeCatalog.name, Is.EqualTo("EnemyUnitArchetypeCatalog_CombinedGameplayShowcase"));
                Assert.That(configuration.EnemyUnitArchetypeCatalog.Entries.Count, Is.EqualTo(1));
                Assert.That(configuration.EnemyUnitArchetypeCatalog.Entries[0].ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));
            }
            finally
            {
                DestroyAssignedStageContent(installerObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        [Category("Full")]
        public void CombinedGameplayShowcaseInstaller_Configuration_UsesStagePresentationDefinitionEnemyPresentationArchetypeCatalog()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_Configuration_UsesEnemyPresentationArchetypeCatalog");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                AssignStageContentEntry(installer);
                AssignTimingPresets(installer);

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.EnemyPresentationArchetypeCatalog, Is.Not.Null);
                Assert.That(
                    configuration.EnemyPresentationArchetypeCatalog,
                    Is.SameAs(configuration.StageContentEntry.PresentationDefinition.EnemyPresentationArchetypeCatalog));
                Assert.That(
                    configuration.EnemyPresentationArchetypeCatalog.name,
                    Is.EqualTo("EnemyPresentationArchetypeCatalog_CombinedGameplayShowcase"));
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

        private static StageRuntimeBuildResult BuildCombinedStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");
            return StageRuntimeBuilder.Build(stage);
        }

        private static bool StageUsesSummonArchetype(StageDefinition stage)
        {
            var enemySpawns = stage.EnemySpawns;
            for (var spawnIndex = 0; spawnIndex < enemySpawns.Length; spawnIndex++)
            {
                var profile = enemySpawns[spawnIndex].EnemyAiProfile;
                if (profile == null)
                {
                    continue;
                }

                var runtimeDefinition = profile.CreateRuntimeDefinition(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                if (!runtimeDefinition.Capabilities.TryGetUtility(out var utility))
                {
                    continue;
                }

                for (var effectIndex = 0; effectIndex < utility.Effects.Count; effectIndex++)
                {
                    if (utility.Effects[effectIndex].Kind == EnemyUtilityEffectKind.SummonMinion)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AssignStageContentEntry(CombinedGameplayShowcaseInstaller installer)
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(StageCatalogProviderAssetPath);
            Assert.That(provider, Is.Not.Null, $"Missing stage catalog provider at '{StageCatalogProviderAssetPath}'.");

            var providerField = typeof(StageBackedGameplayShowcaseInstallerBase).GetField(
                "stageCatalogProvider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            providerField.SetValue(installer, provider);

            StageLaunchContextStore.Clear();
            StageLaunchContextStore.SetCurrent(StageId.CreateOrThrow("stage-1-1"));
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
            if (installerObject == null)
            {
                return;
            }

            var installer = installerObject.GetComponent<CombinedGameplayShowcaseInstaller>();
            if (installer == null)
            {
                return;
            }

            var field = typeof(StageBackedGameplayShowcaseInstallerBase).GetField(
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

        private static object BuildInitialGameplayState(CombinedGameplayShowcaseInstaller installer)
        {
            var buildInitialStateMethod = typeof(StageBackedGameplayShowcaseInstallerBase).GetMethod(
                "BuildInitialGameplayState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildInitialStateMethod, Is.Not.Null);
            return buildInitialStateMethod.Invoke(installer, Array.Empty<object>());
        }

        private static GameplaySceneHostConfiguration BuildConfiguration(CombinedGameplayShowcaseInstaller installer)
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
