using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class AudioLaneMapRepositorySmokeTests
    {
        private const string GameplayPresentationAudioConfigPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Authoring/GameplayPresentationAudioConfig_CampaignV1.asset";
        private const string GameplayPresentationAudioConfigGuid =
            "7f24fd347d594ac8ad2d33539da6b9ec";
        private const string GameplayAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_CampaignV1.asset";
        private const string GameplayAudioMapGuid =
            "2e17653afa1ba264a950b76bcd5ccc56";
        private const string BlockAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_BlockAudio/Maps/BlockAudioMap_PlayerSounds.asset";
        private const string BlockAudioMapGuid =
            "5a6bb3f9bcde4e6ca7487767ab9ba305";
        private const string PlayerLocomotionAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_PlayerLocomotionAudio/Maps/PlayerLocomotionAudioMap_PlayerSounds.asset";
        private const string PlayerLocomotionAudioMapGuid =
            "6bf2bb925f794fe98b6b8e5a406e1f3d";
        private const string TopologyAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_TopologyAudio/Maps/TopologyAudioMap_ObjectSounds.asset";
        private const string TopologyAudioMapGuid =
            "66d7af338d3f4e75b4f5c15d544fd731";
        private const string GravityFieldAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Maps/GravityFieldAudioMap_ObjectSounds.asset";
        private const string GravityFieldAudioMapGuid =
            "20311d81a3644b22b830a914b49455d5";
        private const string TileFeatureAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Maps/TileFeatureAudioMap_ObjectSounds.asset";
        private const string TileFeatureAudioMapGuid =
            "b4f6650dfd7e4aecb59dcad3cabe0489";
        private const string ProductionEnemyAudioRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/";

        private static readonly string[] ProductionRoots =
        {
            "Assets/_Features/Gameplay/",
            "Assets/_Features/Stages/",
            "Assets/_Shared/Audio/",
            "Assets/_Features/UI/",
        };

        [Test]
        [Category("Core")]
        public void ProductionAudioLaneMapsAndProfiles_RepositoryAssets_ValidateAll()
        {
            var failures = new List<string>();

            AppendValidationFailures(
                LoadProductionAssets<BlockAudioMap>(),
                map => map.ValidateRequiredCuesOrThrow(BlockAudioCueCatalog.RequiredOneShotV1),
                failures);
            AppendValidationFailures(
                LoadProductionAssets<PlayerLocomotionAudioMap>(),
                map => map.ValidateRequiredCuesOrThrow(PlayerLocomotionAudioCueCatalog.RequiredOneShotV1),
                failures);
            AppendValidationFailures(
                LoadProductionAssets<TopologyAudioMap>(),
                map => map.ValidateRequiredCuesOrThrow(TopologyAudioCueCatalog.RequiredOneShotV1),
                failures);
            AppendValidationFailures(
                LoadProductionAssets<GravityFieldAudioMap>(),
                map => map.ValidateRequiredCuesOrThrow(GravityFieldAudioCueCatalog.RequiredOneShotV1),
                failures);
            AppendValidationFailures(
                LoadProductionAssets<TileFeatureAudioMap>(),
                map => map.ValidateRequiredCuesOrThrow(TileFeatureAudioCueCatalog.RequiredOneShotV1),
                failures);
            AppendValidationFailures(
                LoadProductionAssets<GameplayPresentationAudioConfig>(),
                config => config.ValidateOrThrow(),
                failures);
            AppendValidationFailures(
                LoadProductionAssets<EnemyAudioProfile>(),
                profile => profile.ValidateOrThrow(),
                failures);
            AppendEnemyAudioRequirementValidationFailures(failures);

            Assert.That(
                failures,
                Is.Empty,
                "Production audio lane map/profile repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void GameplayPresentationAudioConfig_CampaignV1_ReferencesCanonicalLaneMaps()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(GameplayPresentationAudioConfigPath), Is.EqualTo(GameplayPresentationAudioConfigGuid));
            var config = AssetDatabase.LoadAssetAtPath<GameplayPresentationAudioConfig>(
                GameplayPresentationAudioConfigPath);

            Assert.That(config, Is.Not.Null);
            Assert.DoesNotThrow(() => config.ValidateOrThrow());
            AssertCanonical(config.GameplayAudioMap, GameplayAudioMapPath, GameplayAudioMapGuid);
            AssertCanonical(config.BlockAudioMap, BlockAudioMapPath, BlockAudioMapGuid);
            AssertCanonical(
                config.PlayerLocomotionAudioMap,
                PlayerLocomotionAudioMapPath,
                PlayerLocomotionAudioMapGuid);
            AssertCanonical(config.TopologyAudioMap, TopologyAudioMapPath, TopologyAudioMapGuid);
            AssertCanonical(config.GravityFieldAudioMap, GravityFieldAudioMapPath, GravityFieldAudioMapGuid);
            AssertCanonical(config.TileFeatureAudioMap, TileFeatureAudioMapPath, TileFeatureAudioMapGuid);
        }

        private static IReadOnlyList<T> LoadProductionAssets<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            Array.Sort(guids, StringComparer.Ordinal);
            var assets = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsProductionAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                assets,
                Is.Not.Empty,
                $"Repository scan found no production {typeof(T).Name} assets.");
            return assets;
        }

        private static void AppendEnemyAudioRequirementValidationFailures(ICollection<string> failures)
        {
            var profiles = LoadProductionEnemyAudioAssets<EnemyAudioProfile>();
            var policies = LoadProductionEnemyAudioAssets<EnemyAudioRequirementPolicy>();
            var bindings = LoadProductionEnemyAudioAssets<EnemyAudioRequirementBinding>();
            var profileCoverage = new Dictionary<EnemyAudioProfile, List<EnemyAudioRequirementBinding>>();

            AppendValidationFailures(policies, policy => policy.ValidateOrThrow(), failures);
            AppendValidationFailures(bindings, binding => binding.ValidateOrThrow(), failures);
            AppendLegacyEnemyAudioRequirementProfileFailures(failures);

            foreach (var profile in profiles)
            {
                profileCoverage[profile] = new List<EnemyAudioRequirementBinding>();
            }

            foreach (var binding in bindings)
            {
                if (binding.TargetProfile != null &&
                    profileCoverage.TryGetValue(binding.TargetProfile, out var coveredProfiles))
                {
                    coveredProfiles.Add(binding);
                }
                else if (binding.TargetProfile != null)
                {
                    failures.Add(
                        $"{Describe(binding)} targets non-production {nameof(EnemyAudioProfile)} {Describe(binding.TargetProfile)}.");
                }
            }

            foreach (var profile in profiles)
            {
                var coveredProfiles = profileCoverage[profile];
                if (coveredProfiles.Count == 0)
                {
                    failures.Add($"{Describe(profile)} has no production {nameof(EnemyAudioRequirementBinding)}.");
                }
                else if (coveredProfiles.Count > 1)
                {
                    failures.Add(
                        $"{Describe(profile)} has duplicate production {nameof(EnemyAudioRequirementBinding)} assets: " +
                        string.Join(", ", coveredProfiles.Select(Describe)));
                }
            }
        }

        private static IReadOnlyList<T> LoadProductionEnemyAudioAssets<T>() where T : UnityEngine.Object
        {
            var assets = LoadProductionAssets<T>()
                .Where(asset => AssetDatabase.GetAssetPath(asset).StartsWith(ProductionEnemyAudioRoot, StringComparison.Ordinal))
                .ToArray();

            Assert.That(
                assets,
                Is.Not.Empty,
                $"Repository scan found no production enemy audio {typeof(T).Name} assets.");
            return assets;
        }

        private static void AppendLegacyEnemyAudioRequirementProfileFailures(ICollection<string> failures)
        {
            var legacyPaths = AssetDatabase.FindAssets("EnemyAudioRequirementProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith(ProductionEnemyAudioRoot, StringComparison.Ordinal))
                .Where(path => path.EndsWith(".asset", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            for (var i = 0; i < legacyPaths.Length; i++)
            {
                failures.Add($"Legacy full-matrix enemy audio requirement asset remains in production roots: {legacyPaths[i]}");
            }
        }

        private static void AppendValidationFailures<T>(
            IEnumerable<T> assets,
            Action<T> validate,
            ICollection<string> failures)
            where T : UnityEngine.Object
        {
            foreach (var asset in assets)
            {
                try
                {
                    validate(asset);
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(asset)}: {exception.GetType().Name}: {exception.Message}");
                }
            }
        }

        private static bool IsProductionAssetPath(string path)
        {
            return ProductionRoots.Any(root => path.StartsWith(root, StringComparison.Ordinal)) &&
                   !ContainsExcludedPathSegment(path);
        }

        private static bool ContainsExcludedPathSegment(string path)
        {
            var segments = path.Split('/');
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var segment = segments[i];
                if (segment.Contains("Tests", StringComparison.Ordinal) ||
                    segment.Contains("TestSupport", StringComparison.Ordinal) ||
                    segment.Contains("Fixtures", StringComparison.Ordinal) ||
                    segment.Contains("Samples", StringComparison.Ordinal) ||
                    string.Equals(segment, "Docs", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Describe(UnityEngine.Object asset)
        {
            return asset == null
                ? "<null>"
                : $"{asset.GetType().Name} '{asset.name}' Path='{AssetDatabase.GetAssetPath(asset)}'";
        }

        private static void AssertCanonical(UnityEngine.Object asset, string expectedPath, string expectedGuid)
        {
            Assert.That(asset, Is.Not.Null, expectedPath);
            var actualPath = AssetDatabase.GetAssetPath(asset);
            Assert.That(actualPath, Is.EqualTo(expectedPath));
            Assert.That(AssetDatabase.AssetPathToGUID(actualPath), Is.EqualTo(expectedGuid));
        }
    }
}
