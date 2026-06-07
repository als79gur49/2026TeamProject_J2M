using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
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
                LoadProductionAssets<EnemyAudioProfile>(),
                profile => profile.ValidateOrThrow(),
                failures);

            Assert.That(
                failures,
                Is.Empty,
                "Production audio lane map/profile repository smoke failures:\n" + string.Join("\n", failures));
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
    }
}
