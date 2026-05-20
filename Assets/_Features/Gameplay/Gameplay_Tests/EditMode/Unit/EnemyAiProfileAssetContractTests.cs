using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Entities;
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
    }
}
