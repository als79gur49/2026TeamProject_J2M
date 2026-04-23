using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Entities;
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
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_WindupMelee/EnemyAi_WindupMelee.asset",
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_NonAttacking/EnemyAi_NonAttacking.asset",
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_WallFollower/EnemyAi_WallFollower.asset",
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset",
        };

        private static readonly (string AssetPath, PatrolStrategyKind PatrolKind)[] ExpectedPilotPatrolKinds =
        {
            ("Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_Common/EnemyAi_TutorialPassiveContact.asset", PatrolStrategyKind.Stationary),
            ("Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_NonAttacking/EnemyAi_NonAttacking.asset", PatrolStrategyKind.RandomWalk),
            ("Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_WindupMelee/EnemyAi_WindupMelee.asset", PatrolStrategyKind.Forward),
            ("Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_WallFollower/EnemyAi_WallFollower.asset", PatrolStrategyKind.WallFollow),
            ("Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset", PatrolStrategyKind.Forward),
            ("Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_Charge/EnemyAi_Charge.asset", PatrolStrategyKind.Forward),
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
        public void EnemyAiProfileAssets_PatrolPilotRollout_MatchesExpectedPatrolKinds()
        {
            foreach (var expectation in ExpectedPilotPatrolKinds)
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(expectation.AssetPath);

                Assert.That(profile, Is.Not.Null, $"Missing enemy AI profile at '{expectation.AssetPath}'.");
                Assert.That(
                    profile.PatrolStrategyKind,
                    Is.EqualTo(expectation.PatrolKind),
                    $"{expectation.AssetPath} patrol kind drifted from the bounded rollout contract.");
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiProfileAssets_ForwardArchetypes_RetainForwardPatrolKind()
        {
            foreach (var expectation in ExpectedPilotPatrolKinds.Where(expectation => expectation.PatrolKind == PatrolStrategyKind.Forward))
            {
                var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(expectation.AssetPath);

                Assert.That(profile, Is.Not.Null, $"Missing enemy AI profile at '{expectation.AssetPath}'.");
                Assert.That(profile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.Forward));
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolAssets_ForwardAsset_StillResolvesForwardKind_AndSettingsContract()
        {
            const string forwardAssetPath = "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_Common/EnemyPatrol_Forward.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ForwardPatrolAsset>(forwardAssetPath);

            Assert.That(asset, Is.Not.Null, $"Missing forward patrol asset at '{forwardAssetPath}'.");
            Assert.That(asset.Kind, Is.EqualTo(PatrolStrategyKind.Forward));
            Assert.That(asset.Settings.BlockedMovementResponse, Is.EqualTo(PatrolBlockedMovementResponse.Stop));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPatrolAssets_WallFollowAsset_StillResolvesWallFollowKind_AndSettingsContract()
        {
            const string wallFollowAssetPath = "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Profiles/Enemy_WallFollower/EnemyPatrol_WallFollow_Left.asset";
            var asset = AssetDatabase.LoadAssetAtPath<WallFollowPatrolAsset>(wallFollowAssetPath);

            Assert.That(asset, Is.Not.Null, $"Missing wall-follow patrol asset at '{wallFollowAssetPath}'.");
            Assert.That(asset.Kind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(asset.Settings.BlockedMovementResponse, Is.EqualTo(PatrolBlockedMovementResponse.Stop));
            Assert.That(asset.Settings.TurnPreference, Is.EqualTo(WallFollowTurnPreference.Left));
            Assert.That(asset.Settings.FollowWalls, Is.True);
            Assert.That(asset.Settings.FollowBoxes, Is.True);
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
