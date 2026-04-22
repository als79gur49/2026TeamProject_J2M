using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public enum StageAliasUsageCategory
    {
        RuntimeCode = 0,
        EditorTooling = 1,
        SerializedAsset = 2,
        DocsOrExamples = 3,
    }

    public readonly struct StageAliasUsageHit
    {
        public StageAliasUsageHit(string aliasId, string assetPath, StageAliasUsageCategory category)
        {
            AliasId = aliasId ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Category = category;
        }

        public string AliasId { get; }

        public string AssetPath { get; }

        public StageAliasUsageCategory Category { get; }
    }

    public sealed class StageAliasUsageScanResult
    {
        public StageAliasUsageScanResult(StageAliasUsageHit[] hits)
        {
            Hits = hits ?? Array.Empty<StageAliasUsageHit>();
        }

        public IReadOnlyList<StageAliasUsageHit> Hits { get; }

        public int RuntimeCodeHitCount => Count(StageAliasUsageCategory.RuntimeCode);

        public int EditorToolingHitCount => Count(StageAliasUsageCategory.EditorTooling);

        public int SerializedAssetHitCount => Count(StageAliasUsageCategory.SerializedAsset);

        public int DocsOrExamplesHitCount => Count(StageAliasUsageCategory.DocsOrExamples);

        private int Count(StageAliasUsageCategory category)
        {
            return Hits.Count(hit => hit.Category == category);
        }
    }

    public sealed class StageAliasUsageScanner
    {
        private static readonly string[] SupportedExtensions =
        {
            ".asset",
            ".asmdef",
            ".cs",
            ".csproj",
            ".json",
            ".md",
            ".prefab",
            ".txt",
            ".unity",
            ".yaml",
            ".yml",
        };

        public static readonly string[] P3HistoricalAliasIds =
        {
            "Stage_CombinedGameplayShowcase",
            "Stage_TutorialSecne",
            "tutorial-secne",
        };

        public StageAliasUsageScanResult Scan(IEnumerable<string> aliasIds = null)
        {
            var trackedAliases = (aliasIds ?? P3HistoricalAliasIds)
                .Where(aliasId => !string.IsNullOrWhiteSpace(aliasId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(aliasId => aliasId, StringComparer.Ordinal)
                .ToArray();
            if (trackedAliases.Length == 0)
            {
                return new StageAliasUsageScanResult(Array.Empty<StageAliasUsageHit>());
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var hits = new List<StageAliasUsageHit>();
            ScanDirectory(Path.Combine(projectRoot, "Assets"), trackedAliases, hits, projectRoot);
            ScanDirectory(Path.Combine(projectRoot, "Docs"), trackedAliases, hits, projectRoot);
            ScanTopLevelProjectFiles(projectRoot, trackedAliases, hits);

            return new StageAliasUsageScanResult(
                hits
                    .OrderBy(hit => hit.AliasId, StringComparer.Ordinal)
                    .ThenBy(hit => hit.AssetPath, StringComparer.Ordinal)
                    .ToArray());
        }

        public StageValidationReport ValidateNoHits(IEnumerable<string> aliasIds = null)
        {
            var report = new StageValidationReport();
            var result = Scan(aliasIds);
            for (var i = 0; i < result.Hits.Count; i++)
            {
                var hit = result.Hits[i];
                report.Add(
                    StageValidationSeverity.Error,
                    $"alias-usage.{hit.Category.ToString().ToLowerInvariant()}",
                    $"Deprecated alias '{hit.AliasId}' is still referenced by '{hit.AssetPath}'.",
                    assetPath: hit.AssetPath,
                    timing: StageValidationTiming.TestOrCi);
            }

            return report;
        }

        private static void ScanDirectory(
            string absoluteDirectory,
            IReadOnlyList<string> trackedAliases,
            ICollection<StageAliasUsageHit> hits,
            string projectRoot)
        {
            if (!Directory.Exists(absoluteDirectory))
            {
                return;
            }

            var filePaths = Directory.GetFiles(absoluteDirectory, "*", SearchOption.AllDirectories);
            for (var i = 0; i < filePaths.Length; i++)
            {
                if (ShouldSkipPath(filePaths[i]))
                {
                    continue;
                }

                if (!IsSupportedTextFile(filePaths[i]))
                {
                    continue;
                }

                AddHits(filePaths[i], trackedAliases, hits, projectRoot);
            }
        }

        private static void ScanTopLevelProjectFiles(
            string projectRoot,
            IReadOnlyList<string> trackedAliases,
            ICollection<StageAliasUsageHit> hits)
        {
            var topLevelFiles = Directory.GetFiles(projectRoot, "*.*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedTextFile)
                .ToArray();
            for (var i = 0; i < topLevelFiles.Length; i++)
            {
                AddHits(topLevelFiles[i], trackedAliases, hits, projectRoot);
            }
        }

        private static void AddHits(
            string absoluteFilePath,
            IReadOnlyList<string> trackedAliases,
            ICollection<StageAliasUsageHit> hits,
            string projectRoot)
        {
            var relativePath = GetRelativePath(projectRoot, absoluteFilePath);
            var lines = File.ReadAllLines(absoluteFilePath);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                if (ShouldIgnoreLine(line))
                {
                    continue;
                }

                for (var i = 0; i < trackedAliases.Count; i++)
                {
                    if (!line.Contains(trackedAliases[i], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    hits.Add(new StageAliasUsageHit(
                        trackedAliases[i],
                        relativePath,
                        Classify(relativePath)));
                }
            }
        }

        private static string GetRelativePath(string projectRoot, string absoluteFilePath)
        {
            var relative = Path.GetRelativePath(projectRoot, absoluteFilePath);
            return relative.Replace('\\', '/');
        }

        private static bool IsSupportedTextFile(string absoluteFilePath)
        {
            if (absoluteFilePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var extension = Path.GetExtension(absoluteFilePath);
            return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipPath(string absoluteFilePath)
        {
            return absoluteFilePath.Contains($"{Path.DirectorySeparatorChar}Docs{Path.DirectorySeparatorChar}Archive{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                   absoluteFilePath.EndsWith($"{Path.DirectorySeparatorChar}StageAliasUsageScanner.cs", StringComparison.Ordinal);
        }

        private static bool ShouldIgnoreLine(string line)
        {
            return line.Contains("Assets/_Features/Stages/Stage_", StringComparison.Ordinal);
        }

        private static StageAliasUsageCategory Classify(string assetPath)
        {
            if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return StageAliasUsageCategory.SerializedAsset;
            }

            if (assetPath.Contains("/Editor/", StringComparison.Ordinal) ||
                assetPath.Contains("\\Editor\\", StringComparison.Ordinal) ||
                assetPath.Contains("/Tests/", StringComparison.Ordinal) ||
                assetPath.Contains("\\Tests\\", StringComparison.Ordinal))
            {
                return StageAliasUsageCategory.EditorTooling;
            }

            if (assetPath.StartsWith("Docs/", StringComparison.Ordinal) ||
                assetPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                assetPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return StageAliasUsageCategory.DocsOrExamples;
            }

            return StageAliasUsageCategory.RuntimeCode;
        }
    }
}
