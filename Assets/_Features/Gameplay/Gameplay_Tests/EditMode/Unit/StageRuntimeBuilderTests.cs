using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StageRuntimeBuilderTests
    {
        private const string CombinedStageAssetPath =
            "Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase.asset";
        private const string CombinedPresentationAssetPath =
            "Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase_Presentation.asset";
        private const string TutorialStageAssetPath =
            "Assets/_Features/Stages/Content/tutorial-scene/tutorial-scene.asset";
        private const string TutorialEnemyProfileAssetPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_Common/EnemyAi_TutorialPassiveContact.asset";
        private const int ConfiguredShowcaseEnemyId = 54;
        private const int NonAttackingShowcaseEnemyId = 55;
        private const int WallFollowerShowcaseEnemyId = 56;
        private const int JumpShowcaseEnemyId = 57;
        private const int ChargeShowcaseEnemyId = 58;
        private const int TutorialEnemyId = 101;
        private const string AttackingEnemyPresentationId = "Attacking_showcase";
        private const string NonAttackingEnemyPresentationId = "nonAttacking_showcase";
        private const string JumpEnemyPresentationId = "Jump_showcase";
        private const string ChargeEnemyPresentationId = "Charge_showcase";
        private const string WindupEnemyPresentationId = "windup_melee_showcase";

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_DuplicateEntityIdRejects()
        {
            var stage = CreateStage(
                "DuplicateEntityId",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(10, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 2), hp: 1));

            AssertBuildThrows(stage, "duplicate entity id 10");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_DuplicateCellRejects()
        {
            var stage = CreateStage(
                "DuplicateCell",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 1, 1), hp: 3, facing: Direction.Up),
                CreateSpawn(30, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1));

            AssertBuildThrows(stage, "duplicate occupied cell");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_StackedUnitAuthoring_AllowsExplicitOptInGroup()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var stage = CreateStage(
                "StackedUnitOptIn",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(
                    10,
                    StageSpawnKind.Player,
                    sharedCell,
                    hp: 3,
                    facing: Direction.Up,
                    unitStackGroup: "spawn-stack"),
                CreateSpawn(
                    20,
                    StageSpawnKind.Enemy,
                    sharedCell,
                    hp: 2,
                    enemyAiMode: EnemyAiMode.Patrol,
                    unitStackGroup: "spawn-stack"));

            try
            {
                var buildResult = StageRuntimeBuilder.Build(stage);
                var snapshot = GameplayCompositionRoot.CreateWorldState(
                        buildResult.InitialEntities,
                        buildResult.BoardBounds,
                        buildResult.InitialTerrain,
                        buildResult.InitialTopology)
                    .CreateSnapshot();
                var units = new System.Collections.Generic.List<EntityState>();

                snapshot.EnumerateUnitsAt(sharedCell, units);

                CollectionAssert.AreEqual(new[] { 10, 20 }, units.ConvertAll(entity => entity.entityId));
                Assert.That(snapshot.TryGetPrimaryUnitAt(sharedCell, out var primaryUnit), Is.True);
                Assert.That(primaryUnit.entityId, Is.EqualTo(10));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_StackedUnitAuthoring_RejectsMissingOrDifferentOptInGroup()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var missingGroupStage = CreateStage(
                "StackedUnitMissingGroup",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, sharedCell, hp: 3, facing: Direction.Up, unitStackGroup: "spawn-stack"),
                CreateSpawn(20, StageSpawnKind.Enemy, sharedCell, hp: 2, enemyAiMode: EnemyAiMode.Patrol));
            var differentGroupStage = CreateStage(
                "StackedUnitDifferentGroup",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, sharedCell, hp: 3, facing: Direction.Up, unitStackGroup: "spawn-stack"),
                CreateSpawn(20, StageSpawnKind.Enemy, sharedCell, hp: 2, enemyAiMode: EnemyAiMode.Patrol, unitStackGroup: "other-stack"));

            AssertBuildThrows(missingGroupStage, "duplicate occupied cell");
            AssertBuildThrows(differentGroupStage, "duplicate occupied cell");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_OutOfBoundsRejects()
        {
            var stage = CreateStage(
                "OutOfBounds",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 3, 3), hp: 3, facing: Direction.Up));

            AssertBuildThrows(stage, "outside the configured board bounds");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_ZeroPlayerRejects()
        {
            var stage = CreateStage(
                "ZeroPlayer",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(30, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1));

            AssertBuildThrows(stage, "must contain exactly one player spawn, but found none");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_MultiplePlayersRejects()
        {
            var stage = CreateStage(
                "MultiplePlayers",
                CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                CreateSpawn(10, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 0, 0), hp: 3, facing: Direction.Up),
                CreateSpawn(11, StageSpawnKind.Player, new SurfaceCell(FaceId.Floor, 2, 2), hp: 3, facing: Direction.Right));

            AssertBuildThrows(stage, "must contain exactly one player spawn, but found 2");
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_GroupKindMismatchRejects()
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();

            try
            {
                stage.name = "GroupKindMismatch";
                SetPrivateField(stage, "board", CreateBoard(new Vector2Int(0, 0), new Vector2Int(2, 2)));
                SetPrivateField(stage, "playerSpawns", new[]
                {
                    CreateSpawn(10, StageSpawnKind.Box, new SurfaceCell(FaceId.Floor, 1, 1), hp: 1),
                });

                var exception = Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(stage));
                StringAssert.Contains("playerSpawns[0] must use Kind Player", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        [Category("Full")]
        public void StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(CombinedStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{CombinedStageAssetPath}'.");
            Assert.That(stage.PlayerSpawns.Length, Is.EqualTo(1));
            Assert.That(stage.BoxSpawns.Length, Is.EqualTo(12));
            Assert.That(stage.EnemySpawns.Length, Is.EqualTo(5));
            Assert.That(stage.WallSpawns.Length, Is.EqualTo(5));

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.BoardBounds.MinInclusive, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(buildResult.BoardBounds.MaxInclusive, Is.EqualTo(new Vector2Int(14, 7)));
            Assert.That(buildResult.InitialTopology.BottomFace, Is.EqualTo(FaceId.Floor));
            Assert.That(buildResult.PlayerEntityId, Is.EqualTo(10));
            Assert.That(buildResult.InitialTerrain, Is.SameAs(Game.Feature.Gameplay.BoardState.TerrainData.Empty));
            Assert.That(
                buildResult.InitialEntities.Length,
                Is.EqualTo(stage.PlayerSpawns.Length + stage.BoxSpawns.Length + stage.EnemySpawns.Length + stage.WallSpawns.Length));
            AssertEntityIdsAreSorted(buildResult.InitialEntities);

            Assert.That(TryGetEntity(buildResult.InitialEntities, 10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(player.facing, Is.EqualTo(Direction.Right));
            Assert.That(player.unitRole, Is.EqualTo(UnitRole.Player));

            Assert.That(TryGetEntity(buildResult.InitialEntities, 196, out var floorWall), Is.True);
            Assert.That(floorWall.type, Is.EqualTo(EntityType.None));
            Assert.That(floorWall.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 5, 1)));
            Assert.That(floorWall.unitRole, Is.EqualTo(UnitRole.None));

            Assert.That(TryGetEntity(buildResult.InitialEntities, 200, out var frontWall), Is.True);
            Assert.That(frontWall.type, Is.EqualTo(EntityType.None));
            Assert.That(frontWall.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 9, 3)));
            Assert.That(frontWall.unitRole, Is.EqualTo(UnitRole.None));

            var snapshot = GameplayCompositionRoot.CreateWorldState(
                    buildResult.InitialEntities,
                    buildResult.BoardBounds,
                    buildResult.InitialTerrain,
                    buildResult.InitialTopology)
                .CreateSnapshot();
            Assert.That(snapshot.TryGetUnitTraversalBlocker(floorWall.position, out var floorWallBlocker), Is.True);
            Assert.That(floorWallBlocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(floorWallBlocker.EntityId, Is.EqualTo(floorWall.entityId));
            Assert.That(snapshot.TryGetUnitTraversalBlocker(frontWall.position, out var frontWallBlocker), Is.True);
            Assert.That(frontWallBlocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(frontWallBlocker.EntityId, Is.EqualTo(frontWall.entityId));

            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 1, 0)), Is.False);
            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Front, 7, buildResult.BoardBounds.MaxInclusive.y)), Is.False);
            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Ceiling, 2, buildResult.BoardBounds.MaxInclusive.y)), Is.False);
            Assert.That(HasWallAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 2, buildResult.BoardBounds.MaxInclusive.y)), Is.False);

            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Floor, 9, 6)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Front, 3, 1)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Ceiling, 7, 1)), Is.True);
            Assert.That(HasPushableBoxAt(buildResult.InitialEntities, new SurfaceCell(FaceId.Back, 3, 5)), Is.True);

            Assert.That(TryGetEntity(buildResult.InitialEntities, ConfiguredShowcaseEnemyId, out var showcaseEnemy), Is.True);
            Assert.That(showcaseEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 6, 2)));
            Assert.That(showcaseEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(showcaseEnemy.hp, Is.EqualTo(3));
            Assert.That(showcaseEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, NonAttackingShowcaseEnemyId, out var nonAttackingEnemy), Is.True);
            Assert.That(nonAttackingEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 7, 2)));
            Assert.That(nonAttackingEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(nonAttackingEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, WallFollowerShowcaseEnemyId, out var wallFollowerEnemy), Is.True);
            Assert.That(wallFollowerEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 7, 7)));
            Assert.That(wallFollowerEnemy.facing, Is.EqualTo(Direction.Up));
            Assert.That(wallFollowerEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(wallFollowerEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, JumpShowcaseEnemyId, out var jumpEnemy), Is.True);
            Assert.That(jumpEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 10, 2)));
            Assert.That(jumpEnemy.facing, Is.EqualTo(Direction.Left));
            Assert.That(jumpEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(jumpEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(TryGetEntity(buildResult.InitialEntities, ChargeShowcaseEnemyId, out var chargeEnemy), Is.True);
            Assert.That(chargeEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Back, 7, 7)));
            Assert.That(chargeEnemy.facing, Is.EqualTo(Direction.Left));
            Assert.That(chargeEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(chargeEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));

            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(5));
            Assert.That(TryGetProfileOverride(buildResult, ConfiguredShowcaseEnemyId, out var showcaseProfile), Is.True);
            Assert.That(showcaseProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(showcaseProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(showcaseProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.Melee));
            Assert.That(showcaseProfile.AttackTimingSettings.WindupSeconds, Is.EqualTo(1f));
            Assert.That(showcaseProfile.LocomotionTimingSettings.MoveCooldownSeconds, Is.EqualTo(1f));
            Assert.That(showcaseProfile.PatrolSettings.LeashRadius, Is.EqualTo(1));
            Assert.That(showcaseProfile.PatrolSettings.ForwardWeight, Is.EqualTo(6));
            Assert.That(showcaseProfile.PatrolSettings.SideWeight, Is.EqualTo(1));
            Assert.That(showcaseProfile.PatrolSettings.BackwardWeight, Is.EqualTo(1));
            Assert.That(showcaseProfile.PatrolSettings.PreventImmediateBacktrack, Is.True);

            Assert.That(TryGetProfileOverride(buildResult, NonAttackingShowcaseEnemyId, out var nonAttackingProfile), Is.True);
            Assert.That(nonAttackingProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(nonAttackingProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(nonAttackingProfile.PatrolSettings.LeashRadius, Is.EqualTo(2));
            Assert.That(nonAttackingProfile.PatrolSettings.PreventImmediateBacktrack, Is.True);

            Assert.That(TryGetProfileOverride(buildResult, WallFollowerShowcaseEnemyId, out var wallFollowerProfile), Is.True);
            Assert.That(wallFollowerProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(wallFollowerProfile.DetectionStrategyKind, Is.EqualTo(DetectionStrategyKind.None));
            Assert.That(wallFollowerProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));

            Assert.That(TryGetProfileOverride(buildResult, JumpShowcaseEnemyId, out var jumpProfile), Is.True);
            Assert.That(jumpProfile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(jumpProfile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
            Assert.That(jumpProfile.JumpTimingSettings.WindupSeconds, Is.EqualTo(0.35f));
            Assert.That(jumpProfile.JumpTimingSettings.AirborneSeconds, Is.EqualTo(1f));
            Assert.That(jumpProfile.JumpTimingSettings.CooldownSeconds, Is.EqualTo(3f));

            Assert.That(TryGetProfileOverride(buildResult, ChargeShowcaseEnemyId, out var chargeProfile), Is.True);
            Assert.That(chargeProfile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Charge));
            Assert.That(chargeProfile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.Forward));

            var presentationDefinition = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(CombinedPresentationAssetPath);
            Assert.That(
                presentationDefinition,
                Is.Not.Null,
                $"Missing stage presentation asset at '{CombinedPresentationAssetPath}'.");

            var presentation = StagePresentationAssembler.Resolve(presentationDefinition);
            Assert.That(presentation.EnemyPresentationBindings.Length, Is.EqualTo(5));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, ConfiguredShowcaseEnemyId, out var presentationBinding), Is.True);
            Assert.That(presentationBinding.PresentationId, Is.EqualTo(AttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, NonAttackingShowcaseEnemyId, out var nonAttackingBinding), Is.True);
            Assert.That(nonAttackingBinding.PresentationId, Is.EqualTo(NonAttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, WallFollowerShowcaseEnemyId, out var wallFollowerBinding), Is.True);
            Assert.That(wallFollowerBinding.PresentationId, Is.EqualTo(NonAttackingEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, JumpShowcaseEnemyId, out var jumpBinding), Is.True);
            Assert.That(jumpBinding.PresentationId, Is.EqualTo(JumpEnemyPresentationId));
            Assert.That(TryGetPresentationBinding(presentation.EnemyPresentationBindings, ChargeShowcaseEnemyId, out var chargeBinding), Is.True);
            Assert.That(chargeBinding.PresentationId, Is.EqualTo(ChargeEnemyPresentationId));
        }

        [Test]
        [Category("Extended")]
        public void StageRuntimeBuilder_TutorialEnemySpawnWithPassiveContactProfile_BuildsOverridesAndKeepsPatrolMode()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(TutorialStageAssetPath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{TutorialStageAssetPath}'.");
            Assert.That(stage.EnemySpawns.Length, Is.GreaterThan(0));
            var tutorialEnemyProfile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(TutorialEnemyProfileAssetPath);
            Assert.That(tutorialEnemyProfile, Is.Not.Null, $"Missing tutorial enemy profile asset at '{TutorialEnemyProfileAssetPath}'.");

            for (var i = 0; i < stage.EnemySpawns.Length; i++)
            {
                Assert.That(
                    stage.EnemySpawns[i].EnemyAiProfile,
                    Is.SameAs(tutorialEnemyProfile),
                    $"Tutorial enemy spawn {stage.EnemySpawns[i].EntityId} should reference the tutorial passive contact profile.");
                Assert.That(stage.EnemySpawns[i].EnemyAiMode, Is.EqualTo(EnemyAiMode.Patrol));
            }

            var buildResult = StageRuntimeBuilder.Build(stage);

            Assert.That(buildResult.EnemyAiProfileOverrides.Length, Is.EqualTo(stage.EnemySpawns.Length));
            Assert.That(TryGetEntity(buildResult.InitialEntities, TutorialEnemyId, out var tutorialEnemy), Is.True);
            Assert.That(tutorialEnemy.type, Is.EqualTo(EntityType.Unit));
            Assert.That(tutorialEnemy.unitRole, Is.EqualTo(UnitRole.Enemy));
            Assert.That(tutorialEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(TryGetProfileOverride(buildResult, TutorialEnemyId, out var tutorialOverride), Is.True);
            Assert.That(tutorialOverride, Is.SameAs(tutorialEnemyProfile));
        }

        private static StageDefinition CreateStage(
            string stageName,
            StageBoardDefinition board,
            params StageSpawnDefinition[] spawns)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.name = stageName;
            SetPrivateField(stage, "board", board);
            SetPrivateField(stage, "playerSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Player));
            SetPrivateField(stage, "boxSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Box));
            SetPrivateField(stage, "enemySpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Enemy));
            SetPrivateField(stage, "wallSpawns", FilterSpawnsByKind(spawns, StageSpawnKind.Wall));
            return stage;
        }

        private static StageBoardDefinition CreateBoard(
            Vector2Int minInclusive,
            Vector2Int maxInclusive,
            FaceId initialBottomFace = FaceId.Floor)
        {
            return new StageBoardDefinition
            {
                MinInclusive = minInclusive,
                MaxInclusive = maxInclusive,
                InitialBottomFace = initialBottomFace,
            };
        }

        private static StageSpawnDefinition CreateSpawn(
            int entityId,
            StageSpawnKind kind,
            SurfaceCell cell,
            int hp,
            Direction facing = Direction.Right,
            BoxCapabilities boxCapabilities = BoxCapabilities.None,
            EnemyAiMode enemyAiMode = EnemyAiMode.None,
            int enemyAiStateTimer = 0,
            EnemyAiProfile enemyAiProfile = null,
            string presentationId = null,
            string unitStackGroup = null)
        {
            return new StageSpawnDefinition
            {
                EntityId = entityId,
                Kind = kind,
                Cell = cell,
                Facing = facing,
                Hp = hp,
                BoxCapabilities = boxCapabilities,
                EnemyAiMode = enemyAiMode,
                EnemyAiStateTimer = enemyAiStateTimer,
                EnemyAiProfile = enemyAiProfile,
                PresentationId = presentationId,
                UnitStackGroup = unitStackGroup,
            };
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static StageSpawnDefinition[] FilterSpawnsByKind(
            StageSpawnDefinition[] spawns,
            StageSpawnKind kind)
        {
            if (spawns == null || spawns.Length == 0)
            {
                return Array.Empty<StageSpawnDefinition>();
            }

            var filtered = new System.Collections.Generic.List<StageSpawnDefinition>();
            for (var i = 0; i < spawns.Length; i++)
            {
                if (spawns[i].Kind == kind)
                {
                    filtered.Add(spawns[i]);
                }
            }

            return filtered.ToArray();
        }

        private static void AssertBuildThrows(StageDefinition stage, string expectedMessage)
        {
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => StageRuntimeBuilder.Build(stage));
                StringAssert.Contains(expectedMessage, exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static void AssertEntityIdsAreSorted(EntityState[] entities)
        {
            for (var i = 1; i < entities.Length; i++)
            {
                Assert.That(
                    entities[i - 1].entityId,
                    Is.LessThan(entities[i].entityId),
                    $"Entity list must be sorted by entity id ascending, but {entities[i - 1].entityId} appears before {entities[i].entityId}.");
            }
        }

        private static bool TryGetEntity(EntityState[] entities, int entityId, out EntityState entity)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                if (entities[i].entityId != entityId)
                {
                    continue;
                }

                entity = entities[i];
                return true;
            }

            entity = default;
            return false;
        }

        private static bool HasWallAt(EntityState[] entities, SurfaceCell cell)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                if (entities[i].type == EntityType.None && entities[i].position == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPushableBoxAt(EntityState[] entities, SurfaceCell cell)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                if (entities[i].type == EntityType.Box &&
                    entities[i].position == cell &&
                    (entities[i].boxCapabilities & BoxCapabilities.Push) != 0)
                {
                    return true;
                }
            }

            return false;
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
    }
}
