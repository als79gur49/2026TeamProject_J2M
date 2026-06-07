using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyAiProfileAssetContractTests
    {
        private static readonly string[] ExpectedPublicSerializedFields =
        {
            "brainAuthoring",
            "capabilityAssets",
            "coreAuthoring",
        };

        private static readonly string[] LegacyInlineKeys =
        {
            "stateResolverKind",
            "patrolStrategyKind",
            "detectionStrategyKind",
            "chaseStrategyKind",
            "attackDecisionStrategyKind",
            "movementSkillStrategyKind",
            "commonSettings",
            "patrolSettings",
            "detectionSettings",
            "chaseSettings",
            "attackDecisionSettings",
            "attackTimingSettings",
            "locomotionTimingSettings",
            "jumpTimingSettings",
        };

        private static readonly string[] RequiredCanonicalAssetPaths =
        {
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WindupMelee/EnemyAi_WindupMelee.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WindupMelee/EnemyAi_WindupMelee_RandomWalkPilot.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_NonAttacking/EnemyAi_NonAttacking.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WallFollower/EnemyAi_WallFollower.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_Charge/EnemyAi_Charge.asset",
        };

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileAssets_RepositoryProfiles_UseCanonicalAuthoringContract()
        {
            var assetPaths = AssetDatabase.FindAssets("t:EnemyAiProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            var violations = new List<string>();

            if (assetPaths.Length == 0)
            {
                Assert.Fail("AssetDatabase.FindAssets(\"t:EnemyAiProfile\") returned no EnemyAiProfile assets.");
            }

            var missingRequiredAssets = RequiredCanonicalAssetPaths
                .Except(assetPaths, System.StringComparer.Ordinal)
                .ToArray();

            if (missingRequiredAssets.Length > 0)
            {
                violations.Add(
                    $"Repository scan missed required canonical EnemyAiProfile assets: {string.Join(", ", missingRequiredAssets)}.");
            }

            foreach (var assetPath in assetPaths)
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(assetPath);

                if (profile == null)
                {
                    violations.Add($"{assetPath} could not be loaded as {nameof(EnemyAiProfile)}.");
                    continue;
                }

                var assetIssues = new List<string>();
                var publicSerializedFields = GetVisibleSerializedFieldNames(profile);
                var unexpectedPublicFields = publicSerializedFields
                    .Except(ExpectedPublicSerializedFields, System.StringComparer.Ordinal)
                    .ToArray();
                var missingPublicFields = ExpectedPublicSerializedFields
                    .Except(publicSerializedFields, System.StringComparer.Ordinal)
                    .ToArray();

                if (unexpectedPublicFields.Length > 0 || missingPublicFields.Length > 0)
                {
                    assetIssues.Add(
                        $"public serialized fields [{string.Join(", ", publicSerializedFields)}] do not match expected canonical contract [{string.Join(", ", ExpectedPublicSerializedFields)}]");
                }

                var yaml = File.ReadAllText(GetAbsoluteAssetPath(assetPath));
                var presentLegacyKeys = LegacyInlineKeys
                    .Where(key => ContainsRootLevelYamlKey(yaml, key))
                    .ToArray();

                if (presentLegacyKeys.Length > 0)
                {
                    assetIssues.Add($"legacy YAML keys present [{string.Join(", ", presentLegacyKeys)}]");
                }

                if (profile.CoreAuthoring == null)
                {
                    assetIssues.Add("coreAuthoring is null");
                }

                if (profile.BrainAuthoring == null)
                {
                    assetIssues.Add("brainAuthoring is null");
                }

                if (assetIssues.Count > 0)
                {
                    violations.Add($"{assetPath} violates canonical EnemyAiProfile authoring contract: {string.Join("; ", assetIssues)}.");
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "EnemyAiProfile asset contract violations:\n" + string.Join("\n", violations));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolAssets_ForwardAsset_StillResolvesForwardKind_AndSettingsContract()
        {
            const string forwardAssetPath = StageContentPaths.SharedEnemyAiRoot + "/Brain/Enemy_Common/EnemyPatrol_Forward.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ForwardPatrolAsset>(forwardAssetPath);

            Assert.That(asset, Is.Not.Null, $"Missing forward patrol asset at '{forwardAssetPath}'.");
            Assert.That(asset.Kind, Is.EqualTo(PatrolStrategyKind.Forward));
            Assert.That(asset.Settings.BlockedMovementResponse, Is.EqualTo(PatrolBlockedMovementResponse.Stop));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolAssets_WallFollowAsset_StillResolvesWallFollowKind_AndSettingsContract()
        {
            const string wallFollowAssetPath = StageContentPaths.SharedEnemyAiRoot + "/Brain/Enemy_WallFollower/EnemyPatrol_WallFollow_Left.asset";
            var asset = AssetDatabase.LoadAssetAtPath<WallFollowPatrolAsset>(wallFollowAssetPath);

            Assert.That(asset, Is.Not.Null, $"Missing wall-follow patrol asset at '{wallFollowAssetPath}'.");
            Assert.That(asset.Kind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(asset.Settings.BlockedMovementResponse, Is.EqualTo(PatrolBlockedMovementResponse.Stop));
            Assert.That(asset.Settings.TurnPreference, Is.EqualTo(WallFollowTurnPreference.Left));
            Assert.That(asset.Settings.FollowWalls, Is.True);
            Assert.That(asset.Settings.FollowBoxes, Is.True);
            Assert.That(asset.Settings.TreatBoardEdgeAsObstacleBoundary, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolAssets_WindupRandomWalkPilotAsset_UsesLockedMeleePreset()
        {
            const string windupRandomWalkPilotAssetPath = StageContentPaths.SharedEnemyAiRoot + "/Brain/Enemy_WindupMelee/EnemyPatrol_RandomWalk_WindupMelee.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RandomWalkPatrolAsset>(windupRandomWalkPilotAssetPath);

            Assert.That(asset, Is.Not.Null, $"Missing random-walk patrol asset at '{windupRandomWalkPilotAssetPath}'.");
            Assert.That(asset.Kind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(asset.Settings.LeashRadius, Is.EqualTo(1));
            Assert.That(asset.Settings.ForwardWeight, Is.EqualTo(6));
            Assert.That(asset.Settings.SideWeight, Is.EqualTo(1));
            Assert.That(asset.Settings.BackwardWeight, Is.EqualTo(1));
            Assert.That(asset.Settings.PreventImmediateBacktrack, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Startis_ProfileBinding_UsesNonAttackingGameplayProfile()
        {
            var bindings = FindCampaignEnemyPresentationProfileBindings("startis");

            Assert.That(bindings, Is.Not.Empty, "No campaign stage binds presentation id 'startis'.");
            Assert.That(
                bindings.Select(binding => binding.ProfilePath).Distinct().ToArray(),
                Is.EquivalentTo(new[] { NonAttackingProfilePath }),
                "Startis is a presentation/prefab id. Every campaign spawn using it must bind the NonAttacking gameplay profile.");
            AssertCatalogEntryUsesPrefab("startis", "EnemyView_Startis.prefab");
        }

        [Test]
        [Category("Extended")]
        public void Startis_ProfileCompiles_WithGroundMovementAndPassiveContact()
        {
            var profile = LoadRequiredProfile(NonAttackingProfilePath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond);

            Assert.That(definition.Brain.Patrol.Strategy, Is.Not.Null, "NonAttacking runtime must keep a ground movement patrol strategy.");
            Assert.That(definition.Core.LocomotionTimingSettings.MoveCooldownTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                definition.Capabilities.TryGetCombat(out var combat),
                Is.False,
                $"EnemyAi_NonAttacking.asset compiled Combat={combat?.Kind.ToString() ?? "<null>"}; ContactSameCell must live in PassiveContact, not Combat.");
            Assert.That(definition.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
            Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));
        }

        [Test]
        [Category("Core")]
        public void TutorialPassiveContact_Profile_PhasedSameCellFlip_DoesNotDispatchRandomWalkStrategyDirectly()
        {
            var profile = LoadRequiredProfile(TutorialPassiveContactProfilePath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond);
            var playerCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateSameCellFlipWorld(
                playerCell,
                new[]
                {
                    CreatePlayer(10, playerCell),
                    CreateEnemy(5, playerCell, EnemyAiMode.Patrol),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 1), BoxCapabilities.Flip),
                });
            worldState.CreateWriteContext().SetPhasedState(
                5,
                PhasedRuntimeStateQueries.ForceDebug(default, tickIndex: 1));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                    new EnemyLogic(5, definition),
                });

            Assert.That(definition.Brain.Patrol.Kind, Is.EqualTo(PatrolStrategyKind.RandomWalk));
            Assert.That(definition.Brain.Patrol.Strategy, Is.TypeOf<RandomWalkPatrolStrategy>());
            Assert.That(definition.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
            Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));

            TickResult executeResult = null;
            Assert.DoesNotThrow(() => pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left))));
            Assert.DoesNotThrow(() => executeResult = pipeline.RunTick(new TickInput(2)));

            Assert.That(
                executeResult.MovementPhaseResult.RawIntents,
                Has.Exactly(1).Matches<RawMovementIntent>(
                    intent => intent.SourceId == 10 && intent.CommandKind == MovementCommandKind.Flip));
            Assert.That(
                executeResult.MovementPhaseResult.RawIntents,
                Has.None.Matches<RawMovementIntent>(
                    intent => intent.SourceId == 5 && intent.CommandKind == MovementCommandKind.Move));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ProfileBinding_DoesNotAssumeSingleGameplayProfile()
        {
            var bindings = FindCampaignEnemyPresentationProfileBindings("black_eye");
            var profilePaths = bindings.Select(binding => binding.ProfilePath).Distinct().OrderBy(path => path).ToArray();

            Assert.That(bindings, Is.Not.Empty, "No campaign stage binds presentation id 'black_eye'.");
            Assert.That(
                profilePaths,
                Has.Length.GreaterThan(1),
                "BlackEye is a presentation/prefab id and must not be globally asserted as a single gameplay profile.");
            Assert.That(profilePaths, Does.Contain(WindupMeleeProfilePath));
            Assert.That(profilePaths, Does.Contain(WindupProjectileProfilePath));
            AssertCatalogEntryUsesPrefab("black_eye", "EnemyView_BlackEye.prefab");
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_WindupProjectileProfileCompiles_WithForwardCellProjectileCapability()
        {
            var profile = LoadRequiredProfile(WindupProjectileProfilePath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond);

            Assert.That(definition.Capabilities.TryGetCombat(out var combat), Is.True);
            Assert.That(combat.Kind, Is.EqualTo(AttackDecisionStrategyKind.WindupForwardCellProjectile));
            Assert.That(combat.AttackTimingSettings.WindupTicks, Is.GreaterThan(0));
            Assert.That(definition.Core.CommonSettings.RecoverTicks, Is.GreaterThan(0));
            Assert.That(combat.WindupForwardCellProjectileSettings.ImpactDelayTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(combat.WindupForwardCellProjectileSettings.Damage, Is.GreaterThan(0));
        }

        [Test]
        [Category("Extended")]
        public void CombinedGameplay_AstretonBinding_UsesJumpChaserMovementSkillProfile()
        {
            var binding = GetSingleStageBinding(MechanicsShowcaseStagePath, "astreton");

            Assert.That(binding.EntityId, Is.EqualTo(61));
            Assert.That(binding.ProfilePath, Is.EqualTo(JumpChaserProfilePath));
            AssertJumpChaserProfile(binding.ProfilePath);
            AssertCatalogEntryUsesPrefab("astreton", "EnemyView_Astreton.prefab");
        }

        [Test]
        [Category("Extended")]
        public void CombinedGameplay_JPeterBinding_UsesArchetypeSummonerUtilityProfile()
        {
            var binding = GetSingleStageBinding(MechanicsShowcaseStagePath, "j_peter");

            Assert.That(binding.EntityId, Is.EqualTo(59));
            Assert.That(binding.ProfilePath, Is.EqualTo(ArchetypeSummonerProfilePath));
            AssertArchetypeSummonerProfile(binding.ProfilePath);
            AssertCatalogEntryUsesPrefab("j_peter", "EnemyView_JPeter.prefab");
        }

        [Test]
        [Category("Extended")]
        public void CampaignStages_AstretonBindings_AllUseExpectedJumpChaserProfile()
        {
            var bindings = FindCampaignEnemyPresentationProfileBindings("astreton");

            Assert.That(bindings, Is.Not.Empty, "No campaign stage binds presentation id 'astreton'.");
            Assert.That(
                bindings.Select(binding => binding.ProfilePath).Distinct().ToArray(),
                Is.EquivalentTo(new[] { JumpChaserProfilePath }),
                "Astreton is a presentation id; every campaign spawn using it must bind the JumpChaser gameplay profile.");
            Assert.That(bindings.Select(binding => binding.EntityId), Has.Member(61));
            AssertJumpChaserProfile(JumpChaserProfilePath);
            AssertCatalogEntryUsesPrefab("astreton", "EnemyView_Astreton.prefab");
        }

        [Test]
        [Category("Extended")]
        public void CampaignStages_JPeterBindings_AllUseExpectedArchetypeSummonerProfile()
        {
            var bindings = FindCampaignEnemyPresentationProfileBindings("j_peter");

            Assert.That(bindings, Is.Not.Empty, "No campaign stage binds presentation id 'j_peter'.");
            Assert.That(
                bindings.Select(binding => binding.ProfilePath).Distinct().ToArray(),
                Is.EquivalentTo(new[] { ArchetypeSummonerProfilePath }),
                "Jpeter is a presentation id; every campaign spawn using it must bind the ArchetypeSummoner gameplay profile.");
            Assert.That(bindings.Select(binding => binding.EntityId), Has.Member(59));
            AssertArchetypeSummonerProfile(ArchetypeSummonerProfilePath);
            AssertCatalogEntryUsesPrefab("j_peter", "EnemyView_JPeter.prefab");
        }

        private static string[] GetVisibleSerializedFieldNames(EnemyAiProfile profile)
        {
            var serializedObject = new SerializedObject(profile);
            var iterator = serializedObject.GetIterator();
            var fieldNames = new List<string>();
            var enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.depth != 0 || iterator.name.StartsWith("m_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                fieldNames.Add(iterator.name);
            }

            fieldNames.Sort(System.StringComparer.Ordinal);
            return fieldNames.ToArray();
        }

        private static bool ContainsRootLevelYamlKey(string yaml, string key)
        {
            return Regex.IsMatch(
                yaml,
                $"^  {Regex.Escape(key)}:",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty, "Unable to resolve Unity project root from Application.dataPath.");

            return Path.Combine(projectRoot, assetPath);
        }

        private const string NonAttackingProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_NonAttacking/EnemyAi_NonAttacking.asset";

        private const string TutorialPassiveContactProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_Common/EnemyAi_TutorialPassiveContact.asset";

        private const string WindupMeleeProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WindupMelee/EnemyAi_WindupMelee.asset";

        private const string WindupProjectileProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WindupProjectile/EnemyAi_WindupProjectile.asset";

        private const string JumpChaserProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset";

        private const string ArchetypeSummonerProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_UtilitySummoner/EnemyAi_ArchetypeSummoner.asset";

        private const string MechanicsShowcaseStagePath =
            StageContentPaths.CampaignLevel01StagesRoot + "/mechanics-showcase/mechanics-showcase.asset";

        private const string CampaignEnemyPresentationCatalogPath =
            StageContentPaths.CampaignRoot + "/_Shared/Presentation/Enemy/Catalogs/EnemyPresentationCatalog_CampaignMain.asset";

        private static EnemyAiProfile LoadRequiredProfile(string assetPath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(assetPath);
            Assert.That(profile, Is.Not.Null, $"Missing EnemyAiProfile asset at '{assetPath}'.");
            return profile;
        }

        private static WorldState CreateSameCellFlipWorld(SurfaceCell playerCell, IEnumerable<EntityState> entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(5, 5)),
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(playerCell.face));
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position)
        {
            return CreateUnit(entityId, position, teamId: 1, UnitRole.Player, EnemyAiMode.None);
        }

        private static EntityState CreateEnemy(int entityId, SurfaceCell position, EnemyAiMode aiMode)
        {
            return CreateUnit(entityId, position, teamId: 2, UnitRole.Enemy, aiMode);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int teamId,
            UnitRole role,
            EnemyAiMode aiMode)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = role,
                aiMode = aiMode,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                boxCapabilities = capabilities,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static IReadOnlyList<PresentationProfileBinding> FindCampaignEnemyPresentationProfileBindings(
            string presentationId)
        {
            var normalizedPresentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId);
            var result = new List<PresentationProfileBinding>();
            var stagePaths = AssetDatabase.FindAssets("t:StageDefinition", new[] { StageContentPaths.CampaignRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal);

            foreach (var stagePath in stagePaths)
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
                if (stage == null)
                {
                    continue;
                }

                var presentation = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(
                    Path.ChangeExtension(stagePath, null) + "_Presentation.asset");
                if (presentation == null)
                {
                    continue;
                }

                var bindingsByEntityId = StagePresentationAssembler.Resolve(stage, presentation)
                    .EnemyPresentationBindings
                    .ToDictionary(binding => binding.EntityId);

                foreach (var spawn in stage.EnemySpawns)
                {
                    if (!bindingsByEntityId.TryGetValue(spawn.EntityId, out var binding) ||
                        !string.Equals(
                            EnemyPresentationCatalogResolver.NormalizePresentationId(binding.PresentationId),
                            normalizedPresentationId,
                            System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.That(
                        spawn.EnemyAiProfile,
                        Is.Not.Null,
                        $"{stagePath} enemy EntityId={spawn.EntityId} uses presentation '{presentationId}' without an EnemyAiProfile override.");

                    result.Add(
                        new PresentationProfileBinding(
                            stagePath,
                            spawn.EntityId,
                            AssetDatabase.GetAssetPath(spawn.EnemyAiProfile)));
                }
            }

            return result;
        }

        private static PresentationProfileBinding GetSingleStageBinding(string stagePath, string presentationId)
        {
            var bindings = FindStageEnemyPresentationProfileBindings(stagePath, presentationId);

            Assert.That(
                bindings,
                Has.Count.EqualTo(1),
                $"{stagePath} should bind exactly one enemy spawn to presentation id '{presentationId}'.");

            return bindings[0];
        }

        private static IReadOnlyList<PresentationProfileBinding> FindStageEnemyPresentationProfileBindings(
            string stagePath,
            string presentationId)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
            Assert.That(stage, Is.Not.Null, $"Missing stage asset at '{stagePath}'.");

            var presentationPath = Path.ChangeExtension(stagePath, null) + "_Presentation.asset";
            var presentation = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(presentationPath);
            Assert.That(presentation, Is.Not.Null, $"Missing stage presentation asset at '{presentationPath}'.");

            var normalizedPresentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId);
            var bindingsByEntityId = StagePresentationAssembler.Resolve(stage, presentation)
                .EnemyPresentationBindings
                .ToDictionary(binding => binding.EntityId);
            var result = new List<PresentationProfileBinding>();

            foreach (var spawn in stage.EnemySpawns)
            {
                if (!bindingsByEntityId.TryGetValue(spawn.EntityId, out var binding) ||
                    !string.Equals(
                        EnemyPresentationCatalogResolver.NormalizePresentationId(binding.PresentationId),
                        normalizedPresentationId,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.That(
                    spawn.EnemyAiProfile,
                    Is.Not.Null,
                    $"{stagePath} enemy EntityId={spawn.EntityId} uses presentation '{presentationId}' without an EnemyAiProfile override.");

                result.Add(
                    new PresentationProfileBinding(
                        stagePath,
                        spawn.EntityId,
                        AssetDatabase.GetAssetPath(spawn.EnemyAiProfile)));
            }

            return result;
        }

        private static void AssertJumpChaserProfile(string profilePath)
        {
            var profile = LoadRequiredProfile(profilePath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond);

            Assert.That(definition.Capabilities.TryGetCombat(out var combat), Is.False, $"Astreton must not bind Combat={combat?.Kind.ToString() ?? "<null>"}.");
            Assert.That(definition.Capabilities.TryGetMovementSkill(out var movementSkill), Is.True);
            Assert.That(movementSkill.Kind, Is.EqualTo(MovementSkillStrategyKind.JumpToLockedTarget));
            Assert.That(movementSkill.JumpTimingSettings.WindupTicks, Is.GreaterThan(0));
            Assert.That(movementSkill.JumpTimingSettings.AirborneTicks, Is.GreaterThan(0));
            Assert.That(definition.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
            Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));
        }

        private static void AssertArchetypeSummonerProfile(string profilePath)
        {
            var profile = LoadRequiredProfile(profilePath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond);

            Assert.That(definition.Capabilities.TryGetCombat(out var combat), Is.False, $"Jpeter must not be inferred as Combat={combat?.Kind.ToString() ?? "<null>"}.");
            Assert.That(definition.Capabilities.TryGetUtility(out var utility), Is.True);
            Assert.That(utility.Effects, Has.Count.EqualTo(1));
            Assert.That(utility.Effects[0].Kind, Is.EqualTo(EnemyUtilityEffectKind.SummonMinion));
            Assert.That(
                utility.Effects[0].Summon.SummonedArchetypeId,
                Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));
            Assert.That(definition.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
            Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));
        }

        private static void AssertCatalogEntryUsesPrefab(string presentationId, string expectedPrefabFileName)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(CampaignEnemyPresentationCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing campaign enemy presentation catalog at '{CampaignEnemyPresentationCatalogPath}'.");

            var entry = catalog.Entries.SingleOrDefault(candidate =>
                string.Equals(
                    EnemyPresentationCatalogResolver.NormalizePresentationId(candidate.PresentationId),
                    presentationId,
                    System.StringComparison.Ordinal));

            Assert.That(entry.ViewPrefab, Is.Not.Null, $"Missing catalog entry or prefab for presentation id '{presentationId}'.");
            Assert.That(
                Path.GetFileName(AssetDatabase.GetAssetPath(entry.ViewPrefab)),
                Is.EqualTo(expectedPrefabFileName),
                $"Presentation id '{presentationId}' must remain bound to its expected prefab.");
        }

        private readonly struct PresentationProfileBinding
        {
            public PresentationProfileBinding(string stagePath, int entityId, string profilePath)
            {
                StagePath = stagePath;
                EntityId = entityId;
                ProfilePath = profilePath;
            }

            public string StagePath { get; }

            public int EntityId { get; }

            public string ProfilePath { get; }
        }
    }
}
