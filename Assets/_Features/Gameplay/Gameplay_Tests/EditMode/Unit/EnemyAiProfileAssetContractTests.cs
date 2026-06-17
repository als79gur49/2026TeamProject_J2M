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
            "behaviorModuleAssets",
            "brainAuthoring",
            "capabilityAssets",
            "coreAuthoring",
        };

        private static readonly string[] ExpectedCoreSerializedFields =
        {
            "commonSettings",
            "locomotionTimingSettings",
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
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_PassiveContactPatroller/EnemyAi_PassiveContactPatroller.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WallFollower/EnemyAi_WallFollower.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_Charger/EnemyAi_Charger.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WindupProjectile/EnemyAi_WindupProjectile.asset",
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_ArchetypeSummoner/EnemyAi_ArchetypeSummoner.asset",
        };

        private const string StandardChargeExecutionProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/BehaviorModules/Enemy_Charge/EnemyChargeExecutionProfile_Standard.asset";

        private const string StandardChargeBehaviorModulePath =
            StageContentPaths.SharedEnemyAiRoot + "/BehaviorModules/Enemy_Charge/EnemyChargeBehaviorModule_Standard.asset";

        private const string ChargerProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_Charger/EnemyAi_Charger.asset";

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
        public void EnemyCoreAuthoringAssets_RepositoryCores_HaveOnlyCommonAndLocomotion()
        {
            var assetPaths = AssetDatabase.FindAssets("t:EnemyCoreAuthoring")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            var violations = new List<string>();

            Assert.That(assetPaths, Is.Not.Empty, "AssetDatabase.FindAssets(\"t:EnemyCoreAuthoring\") returned no assets.");

            foreach (var assetPath in assetPaths)
            {
                var core = AssetDatabase.LoadAssetAtPath<EnemyCoreAuthoring>(assetPath);
                if (core == null)
                {
                    violations.Add($"{assetPath} could not be loaded as {nameof(EnemyCoreAuthoring)}.");
                    continue;
                }

                var fields = GetVisibleSerializedFieldNames(core);
                var unexpectedFields = fields
                    .Except(ExpectedCoreSerializedFields, System.StringComparer.Ordinal)
                    .ToArray();
                var missingFields = ExpectedCoreSerializedFields
                    .Except(fields, System.StringComparer.Ordinal)
                    .ToArray();
                if (unexpectedFields.Length > 0 || missingFields.Length > 0)
                {
                    violations.Add(
                        $"{assetPath} fields [{string.Join(", ", fields)}] do not match [{string.Join(", ", ExpectedCoreSerializedFields)}].");
                }

                var yaml = File.ReadAllText(GetAbsoluteAssetPath(assetPath));
                if (yaml.Contains("chargeTimingSettings:"))
                {
                    violations.Add($"{assetPath} still contains chargeTimingSettings YAML.");
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "EnemyCoreAuthoring asset contract violations:\n" + string.Join("\n", violations));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileAssets_ChargeBehaviorModules_MatchChargeResolverUsage()
        {
            var assetPaths = AssetDatabase.FindAssets("t:EnemyAiProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            var violations = new List<string>();

            Assert.That(assetPaths, Is.Not.Empty, "AssetDatabase.FindAssets(\"t:EnemyAiProfile\") returned no EnemyAiProfile assets.");

            foreach (var assetPath in assetPaths)
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(assetPath);
                if (profile == null)
                {
                    violations.Add($"{assetPath} could not be loaded as {nameof(EnemyAiProfile)}.");
                    continue;
                }

                EnemyAiRuntimeDefinition definition;
                try
                {
                    definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
                }
                catch (System.Exception exception)
                {
                    violations.Add($"{assetPath} failed compile: {exception.GetType().Name}: {exception.Message}");
                    continue;
                }

                var hasChargeResolver = profile.StateResolverKind == EnemyAiStateResolverKind.Charge;
                var hasChargeBehavior = definition.TryGetChargeBehavior(out var charge);

                if (hasChargeResolver && !hasChargeBehavior)
                {
                    violations.Add($"{assetPath} uses Charge resolver but has no Charge behavior module.");
                }

                if (!hasChargeResolver && hasChargeBehavior)
                {
                    violations.Add($"{assetPath} declares unused Charge behavior module.");
                }

                if (hasChargeBehavior)
                {
                    Assert.That(charge.Timing.WindupTicks, Is.GreaterThanOrEqualTo(0), assetPath);
                    Assert.That(charge.Timing.ActiveStepCooldownTicks, Is.GreaterThanOrEqualTo(0), assetPath);
                    Assert.That(charge.Timing.RecoverTicks, Is.GreaterThanOrEqualTo(0), assetPath);
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "EnemyAiProfile Charge behavior module contract violations:\n" + string.Join("\n", violations));
        }

        [Test]
        [Category("Extended")]
        public void EnemyChargeExecutionProfileAssets_StandardProductionProfile_CompilesToExpectedTicks()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyChargeExecutionProfile>(StandardChargeExecutionProfilePath);

            Assert.That(profile, Is.Not.Null, StandardChargeExecutionProfilePath);
            Assert.That(
                profile.Timing.WindupSeconds,
                Is.EqualTo(0.4f).Within(0.0001f),
                StandardChargeExecutionProfilePath);
            Assert.That(
                profile.Timing.ActiveStepCooldownSeconds,
                Is.EqualTo(0.2f).Within(0.0001f),
                StandardChargeExecutionProfilePath);
            Assert.That(
                profile.Timing.RecoverSeconds,
                Is.EqualTo(0.4f).Within(0.0001f),
                StandardChargeExecutionProfilePath);

            var timing = profile.Timing.ToRuntimeSettings(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
            Assert.That(timing.WindupTicks, Is.EqualTo(24), StandardChargeExecutionProfilePath);
            Assert.That(timing.ActiveStepCooldownTicks, Is.EqualTo(12), StandardChargeExecutionProfilePath);
            Assert.That(timing.RecoverTicks, Is.EqualTo(24), StandardChargeExecutionProfilePath);
        }

        [Test]
        [Category("Extended")]
        public void EnemyChargeBehaviorModuleAssets_StandardModule_PointsToStandardChargeExecutionProfile()
        {
            var module = AssetDatabase.LoadAssetAtPath<EnemyChargeBehaviorModuleAsset>(StandardChargeBehaviorModulePath);

            Assert.That(module, Is.Not.Null, StandardChargeBehaviorModulePath);
            Assert.That(module.Key, Is.EqualTo(EnemyBehaviorModuleKey.Charge), StandardChargeBehaviorModulePath);
            Assert.That(module.ChargeExecutionProfile, Is.Not.Null, StandardChargeBehaviorModulePath);
            Assert.That(AssetDatabase.GetAssetPath(module.ChargeExecutionProfile), Is.EqualTo(StandardChargeExecutionProfilePath));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileAssets_ChargerProfile_PointsToStandardChargeBehaviorModule()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(ChargerProfilePath);
            var module = AssetDatabase.LoadAssetAtPath<EnemyChargeBehaviorModuleAsset>(StandardChargeBehaviorModulePath);

            Assert.That(profile, Is.Not.Null, ChargerProfilePath);
            Assert.That(module, Is.Not.Null, StandardChargeBehaviorModulePath);
            Assert.That(profile.BehaviorModuleAssets, Has.Count.EqualTo(1), ChargerProfilePath);
            Assert.That(profile.BehaviorModuleAssets[0], Is.SameAs(module), ChargerProfilePath);
        }

        [Test]
        [Category("Extended")]
        public void MigratedSummon_ProfileHasBehaviorModuleOnly()
        {
            var profile = LoadRequiredProfile(ArchetypeSummonerProfilePath);
            var module = AssetDatabase.LoadAssetAtPath<EnemySummonBehaviorModuleAsset>(
                ArchetypeSummonerSummonBehaviorModulePath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(module, Is.Not.Null, ArchetypeSummonerSummonBehaviorModulePath);
            Assert.That(profile.BehaviorModuleAssets, Has.Count.EqualTo(1), ArchetypeSummonerProfilePath);
            Assert.That(profile.BehaviorModuleAssets[0], Is.SameAs(module), ArchetypeSummonerProfilePath);
            Assert.That(definition.TryGetSummonBehavior(out var summon), Is.True, ArchetypeSummonerProfilePath);
            Assert.That(summon.SummonedArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));
            Assert.That(definition.Capabilities.TryGetUtility(out var utility), Is.True, ArchetypeSummonerProfilePath);
            Assert.That(utility.Effects, Is.Empty, ArchetypeSummonerProfilePath);
            Assert.That(definition.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
            Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));
            Assert.That(
                File.ReadAllText(GetAbsoluteAssetPath(ArchetypeSummonerProfilePath)),
                Does.Not.Contain("logicModuleAssets"),
                ArchetypeSummonerProfilePath);
        }

        [Test]
        [Category("Extended")]
        public void MigratedSummon_ProfileHasNoUtilitySummonResidue()
        {
            var profile = LoadRequiredProfile(ArchetypeSummonerProfilePath);
            var capability = AssetDatabase.LoadAssetAtPath<EnemyUtilityCapabilityAsset>(
                ArchetypeSummonerUtilityCapabilityPath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(capability, Is.Not.Null, ArchetypeSummonerUtilityCapabilityPath);
            Assert.That(capability.Effects.Count, Is.EqualTo(0), ArchetypeSummonerUtilityCapabilityPath);
            Assert.That(definition.Capabilities.TryGetUtility(out var utility), Is.True, ArchetypeSummonerProfilePath);
            Assert.That(utility.Effects, Is.Empty, ArchetypeSummonerProfilePath);
            Assert.That(definition.TryGetSummonBehavior(out _), Is.True, ArchetypeSummonerProfilePath);
            Assert.That(
                File.ReadAllText(GetAbsoluteAssetPath(ArchetypeSummonerUtilityCapabilityPath)),
                Does.Not.Contain("kind: 0"),
                ArchetypeSummonerUtilityCapabilityPath);
        }

        [Test]
        [Category("Extended")]
        public void MigratedSummon_FieldMappingMatchesUtilityBaseline()
        {
            var module = AssetDatabase.LoadAssetAtPath<EnemySummonBehaviorModuleAsset>(
                ArchetypeSummonerSummonBehaviorModulePath);
            var definition = LoadRequiredProfile(ArchetypeSummonerProfilePath)
                .CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(module, Is.Not.Null, ArchetypeSummonerSummonBehaviorModulePath);
            Assert.That(module.Key, Is.EqualTo(EnemyBehaviorModuleKey.Summon));
            Assert.That(module.InitialDelaySeconds, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(module.CooldownSeconds, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(module.Summon.SpawnCountPerTrigger, Is.EqualTo(1));
            Assert.That(module.Summon.MaxAliveChildren, Is.EqualTo(2));
            Assert.That(module.Summon.CandidatePattern, Is.EqualTo(SummonCandidatePattern.OrthogonalAdjacent4));
            Assert.That(module.Summon.RequireNoUnitAtSpawnCell, Is.True);
            Assert.That(module.Summon.RequireNoSolidAtSpawnCell, Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(module.Summon.SummonedArchetype)), Is.EqualTo("8da265900dc94540a0e150fa08ff4c7f"));
            Assert.That(module.Summon.OverrideHp, Is.True);
            Assert.That(module.Summon.HpOverride, Is.EqualTo(1));
            Assert.That(module.Summon.WindupSeconds, Is.EqualTo(1.7f).Within(0.0001f));
            Assert.That(module.Summon.SuppressMovementDuringWindup, Is.True);
            Assert.That(module.Summon.RecoverySeconds, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(module.Summon.SuppressMovementDuringRecover, Is.True);
            Assert.That(definition.TryGetSummonBehavior(out var summon), Is.True);
            Assert.That(summon.InitialDelayTicks, Is.EqualTo(600));
            Assert.That(summon.CooldownTicks, Is.EqualTo(600));
            Assert.That(summon.WindupTicks, Is.EqualTo(102));
            Assert.That(summon.RecoveryTicks, Is.EqualTo(42));
            Assert.That(summon.MaxAliveChildren, Is.EqualTo(2));
            Assert.That(summon.HpOverride, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemySummonBehaviorModuleAssets_ProductionAllowlistContainsOnlyArchetypeSummoner()
        {
            var assetPaths = AssetDatabase.FindAssets("t:EnemySummonBehaviorModuleAsset", new[] { StageContentPaths.CampaignRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                assetPaths,
                Is.EquivalentTo(new[] { ArchetypeSummonerSummonBehaviorModulePath }),
                "Production EnemySummonBehaviorModuleAsset instances must remain asset-scoped to the ArchetypeSummoner migration.");
        }

        [Test]
        [Category("Extended")]
        public void RetiredLockNearbyBoxes_GuardUnchanged()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty, "Unable to resolve Unity project root from Application.dataPath.");

            var forbiddenTokens = new[]
            {
                "EnemyUtilityEffectKind." + "LockNearbyBoxes",
                "EnemyUtilityPresentationKind." + "LockNearbyBoxes",
                "LockNearbyBoxes" + "Authoring",
                "LockNearbyBoxes" + "Runtime",
                "Resolve" + "LockNearbyBoxes",
                "lock" + "NearbyBoxes",
            };
            var scanRoots = new[]
            {
                Path.Combine(projectRoot, "Assets/_Features/Gameplay"),
                Path.Combine(projectRoot, "Assets/_Features/Stages"),
            };
            var violations = new List<string>();

            foreach (var filePath in scanRoots.SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)))
            {
                var relativePath = filePath.Substring(projectRoot.Length + 1).Replace('\\', '/');
                var text = File.ReadAllText(filePath);
                foreach (var token in forbiddenTokens)
                {
                    if (text.Contains(token))
                    {
                        violations.Add($"{relativePath} contains retired active utility token '{token}'.");
                    }
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "Retired LockNearbyBoxes active symbol inventory violations:\n" + string.Join("\n", violations));
        }

        [Test]
        [Category("Extended")]
        public void GravityFieldAura_Unchanged()
        {
            var capability = AssetDatabase.LoadAssetAtPath<EnemyUtilityCapabilityAsset>(GravityFieldAuraCapabilityPath);

            Assert.That(capability, Is.Not.Null, GravityFieldAuraCapabilityPath);
            Assert.That(capability.Effects.Count, Is.EqualTo(1), GravityFieldAuraCapabilityPath);
            Assert.That(capability.Effects[0].Kind, Is.EqualTo(EnemyUtilityEffectKind.GravityFieldAura), GravityFieldAuraCapabilityPath);
            Assert.That(capability.Effects[0].InitialDelaySeconds, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(capability.Effects[0].CooldownSeconds, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(capability.Effects[0].GravityFieldAura.Radius, Is.EqualTo(1));
            Assert.That(capability.Effects[0].GravityFieldAura.WindupSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(capability.Effects[0].GravityFieldAura.FieldDurationSeconds, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(capability.Effects[0].GravityFieldAura.RecoverSeconds, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtilityCapabilityAssets_DoNotAuthorRetiredLockNearbyBoxes_AndKeepGravityFieldAura()
        {
            var assetPaths = AssetDatabase.FindAssets("t:EnemyUtilityCapabilityAsset", new[] { StageContentPaths.CampaignRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            var violations = new List<string>();
            var foundGravityFieldAuraCapability = false;

            Assert.That(assetPaths, Is.Not.Empty, "Campaign scan returned no EnemyUtilityCapabilityAsset assets.");

            foreach (var assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<EnemyUtilityCapabilityAsset>(assetPath);
                if (asset == null)
                {
                    violations.Add($"{assetPath} did not load as {nameof(EnemyUtilityCapabilityAsset)}.");
                    continue;
                }

                var yaml = File.ReadAllText(GetAbsoluteAssetPath(assetPath));
                if (yaml.Contains("lock" + "NearbyBoxes:"))
                {
                    violations.Add($"{assetPath} still contains inactive retired utility serialized residue.");
                }

                for (var effectIndex = 0; effectIndex < asset.Effects.Count; effectIndex++)
                {
                    var effect = asset.Effects[effectIndex];
                    if (effect.Kind == EnemyUtilityEffectKind.RetiredLockNearbyBoxes)
                    {
                        violations.Add($"{assetPath} effects[{effectIndex}] authors retired utility kind 1.");
                    }

                    if (effect.Kind == EnemyUtilityEffectKind.GravityFieldAura)
                    {
                        foundGravityFieldAuraCapability = true;
                    }
                }
            }

            Assert.That(foundGravityFieldAuraCapability, Is.True, "Campaign utility assets must preserve active GravityFieldAura kind 2.");
            Assert.That(
                violations,
                Is.Empty,
                "Enemy utility capability asset retirement violations:\n" + string.Join("\n", violations));
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
        public void Startis_ProfileBinding_UsesPassiveContactPatrollerGameplayProfile()
        {
            var bindings = FindCampaignEnemyPresentationProfileBindings("startis");

            Assert.That(bindings, Is.Not.Empty, "No campaign stage binds presentation id 'startis'.");
            Assert.That(
                bindings.Select(binding => binding.ProfilePath).Distinct().ToArray(),
                Is.EquivalentTo(new[] { PassiveContactPatrollerProfilePath }),
                "Startis is a presentation/prefab id. Every campaign spawn using it must bind the PassiveContactPatroller gameplay profile.");
            AssertCatalogEntryUsesPrefab("startis", "EnemyView_Startis.prefab");
        }

        [Test]
        [Category("Extended")]
        public void Startis_ProfileCompiles_WithGroundMovementAndPassiveContact()
        {
            var profile = LoadRequiredProfile(PassiveContactPatrollerProfilePath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond);

            Assert.That(definition.Brain.Patrol.Strategy, Is.Not.Null, "PassiveContactPatroller runtime must keep a ground movement patrol strategy.");
            Assert.That(definition.Core.LocomotionTimingSettings.MoveCooldownTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                definition.Capabilities.TryGetCombat(out var combat),
                Is.False,
                $"EnemyAi_PassiveContactPatroller.asset compiled Combat={combat?.Kind.ToString() ?? "<null>"}; ContactSameCell must live in PassiveContact, not Combat.");
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
            var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            Assert.That(definition.Brain.Patrol.Kind, Is.EqualTo(PatrolStrategyKind.Stationary));
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
        public void BlackEye_ProfileBinding_KeepsStageReachableProfiles_AndExcludesRetiredWindupMelee()
        {
            var bindings = FindCampaignEnemyPresentationProfileBindings("black_eye");
            var profilePaths = bindings.Select(binding => binding.ProfilePath).Distinct().OrderBy(path => path).ToArray();

            Assert.That(bindings, Is.Not.Empty, "No campaign stage binds presentation id 'black_eye'.");
            Assert.That(
                profilePaths,
                Does.Contain(WindupProjectileProfilePath),
                "Campaign BlackEye spawns must keep the Stage-reachable WindupProjectile gameplay profile.");
            Assert.That(
                profilePaths,
                Does.Not.Contain(RetiredWindupMeleeProfilePath),
                "Campaign BlackEye presentation bindings must not keep the retired WindupMelee repository profile.");
            AssertCatalogEntryUsesPrefab("black_eye", "EnemyView_BlackEye.prefab");
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_WindupProjectileProfileCompiles_WithForwardCellProjectileCapability()
        {
            var profile = LoadRequiredProfile(WindupProjectileProfilePath);
            var capabilityAsset = AssetDatabase.LoadAssetAtPath<WindupForwardCellProjectileCapabilityAsset>(
                WindupProjectileCapabilityPath);
            var definition = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond);

            Assert.That(capabilityAsset, Is.Not.Null, WindupProjectileCapabilityPath);
            Assert.That(definition.Capabilities.TryGetCombat(out var combat), Is.True);
            Assert.That(combat.Kind, Is.EqualTo(AttackDecisionStrategyKind.WindupForwardCellProjectile));
            Assert.That(combat.AttackTimingSettings.WindupTicks, Is.GreaterThan(0));
            Assert.That(definition.Core.CommonSettings.RecoverTicks, Is.GreaterThan(0));
            Assert.That(combat.WindupForwardCellProjectileSettings.ImpactDelayTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(combat.WindupForwardCellProjectileSettings.Damage, Is.GreaterThan(0));
        }

        [Test]
        [Category("Extended")]
        public void AdvancedCampaignStage_AstretonBindings_UseJumpChaserMovementSkillProfile()
        {
            var bindings = FindStageEnemyPresentationProfileBindings(AdvancedCampaignStagePath, "astreton");

            Assert.That(bindings, Is.Not.Empty);
            Assert.That(
                bindings.Select(binding => binding.ProfilePath).Distinct().ToArray(),
                Is.EquivalentTo(new[] { JumpChaserProfilePath }));
            AssertJumpChaserProfile(JumpChaserProfilePath);
            AssertCatalogEntryUsesPrefab("astreton", "EnemyView_Astreton.prefab");
        }

        [Test]
        [Category("Extended")]
        public void AdvancedCampaignStage_JPeterBindings_UseArchetypeSummonerProfile()
        {
            var bindings = FindStageEnemyPresentationProfileBindings(AdvancedCampaignStagePath, "j_peter");

            Assert.That(bindings, Is.Not.Empty);
            Assert.That(
                bindings.Select(binding => binding.ProfilePath).Distinct().ToArray(),
                Is.EquivalentTo(new[] { ArchetypeSummonerProfilePath }));
            AssertArchetypeSummonerProfile(ArchetypeSummonerProfilePath);
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
            Assert.That(bindings.Select(binding => binding.EntityId), Is.Not.Empty);
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

        private static string[] GetVisibleSerializedFieldNames(UnityEngine.Object asset)
        {
            var serializedObject = new SerializedObject(asset);
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

        private const string PassiveContactPatrollerProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_PassiveContactPatroller/EnemyAi_PassiveContactPatroller.asset";

        private const string TutorialPassiveContactProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_Common/EnemyAi_TutorialPassiveContact.asset";

        private const string RetiredWindupMeleeProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WindupMelee/EnemyAi_WindupMelee.asset";

        private const string WindupProjectileProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WindupProjectile/EnemyAi_WindupProjectile.asset";
        private const string WindupProjectileCapabilityPath =
            StageContentPaths.SharedEnemyAiRoot + "/Capabilities/Enemy_WindupProjectile/EnemyCapability_WindupForwardCellProjectile.asset";

        private const string JumpChaserProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset";

        private const string ArchetypeSummonerProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_ArchetypeSummoner/EnemyAi_ArchetypeSummoner.asset";

        private const string ArchetypeSummonerUtilityCapabilityPath =
            StageContentPaths.SharedEnemyAiRoot + "/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset";

        private const string ArchetypeSummonerSummonBehaviorModulePath =
            StageContentPaths.SharedEnemyAiRoot + "/BehaviorModules/Enemy_Summon/EnemySummonBehaviorModule_ArchetypeSummoner.asset";

        private const string GravityFieldAuraCapabilityPath =
            StageContentPaths.SharedEnemyAiRoot + "/Capabilities/GravityFieldAura/EnemyCapability_GravityFieldAura.asset";

        private const string AdvancedCampaignStagePath =
            StageContentPaths.CampaignLevel01StagesRoot + "/stage-4-2/stage-4-2.asset";

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
            Assert.That(utility.Effects, Is.Empty);
            Assert.That(definition.TryGetSummonBehavior(out var summon), Is.True);
            Assert.That(summon.SummonedArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId("PassiveContactMinion")));
            Assert.That(summon.MaxAliveChildren, Is.EqualTo(2));
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
