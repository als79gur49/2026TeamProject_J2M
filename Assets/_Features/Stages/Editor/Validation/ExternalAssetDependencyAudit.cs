using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class ExternalAssetDependencyAudit
    {
        private static readonly string[] ExternalRoots =
        {
            "Assets/Kevin Iglesias",
            "Assets/Opsive",
        };

        private static readonly string[] OwnerRoots =
        {
            "Assets/_Features/Stages/Content",
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase",
            "Assets/_Features/Stages/Stage_TutorialScene",
            "Assets/_Features/Gameplay",
            "Assets/_Features/UI",
            "Assets/_Shared",
            "Assets/Scenes",
            "Assets/Resources",
            "Assets/StreamingAssets",
        };

        private static readonly string[] ProductionScenes =
        {
            "Assets/Scenes/CombinedGameplayShowcase.unity",
            "Assets/Scenes/TutorialScene.unity",
            "Assets/Scenes/UIAudioScene.unity",
        };

        private const string ReportDirectory = "TestResults";

        public static string ReportPath =>
            Path.Combine(GetProjectRoot(), ReportDirectory, "external-asset-dependency-audit.md");

        public static void RunFromCommandLine()
        {
            EditorApplication.Exit(Run());
        }

        public static int Run()
        {
            AssetDatabase.Refresh();

            var rows = new List<AuditRow>();
            AuditOwnerRoots(rows);
            AuditProductionScenes(rows);
            AuditStageCatalogGraph(rows);

            WriteReport(rows);

            var externalCount = rows.Count(row => row.ExternalDependencies.Count > 0);
            if (externalCount > 0)
            {
                Debug.LogError(
                    $"External asset dependency audit found {externalCount} owner(s) with Kevin/Opsive dependencies. See {ReportPath}");
                return 1;
            }

            Debug.Log($"External asset dependency audit passed. See {ReportPath}");
            return 0;
        }

        private static void AuditOwnerRoots(List<AuditRow> rows)
        {
            for (var i = 0; i < OwnerRoots.Length; i++)
            {
                var ownerRoot = OwnerRoots[i];
                if (!AssetDatabase.IsValidFolder(ownerRoot))
                {
                    rows.Add(AuditRow.Missing("OwnerRoot", ownerRoot));
                    continue;
                }

                foreach (var ownerPath in EnumerateAssets(ownerRoot))
                {
                    AddDependencyRow(rows, "OwnerRoot", ownerPath, ownerPath, recursive: true);
                }
            }
        }

        private static void AuditProductionScenes(List<AuditRow> rows)
        {
            for (var i = 0; i < ProductionScenes.Length; i++)
            {
                var scenePath = ProductionScenes[i];
                if (!File.Exists(ToAbsolutePath(scenePath)))
                {
                    rows.Add(AuditRow.Missing("ProductionScene", scenePath));
                    continue;
                }

                AddDependencyRow(rows, "ProductionSceneDirect", scenePath, scenePath, recursive: true);
            }
        }

        private static void AuditStageCatalogGraph(List<AuditRow> rows)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            if (catalog == null)
            {
                rows.Add(AuditRow.Missing("StageCatalogGraph", StageContentPaths.StageCatalogAssetPath));
                return;
            }

            var catalogPath = AssetDatabase.GetAssetPath(catalog);
            AddDependencyRow(rows, "StageCatalogGraph", catalogPath, catalogPath, recursive: true);

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    rows.Add(AuditRow.Missing("StageCatalogEntry", $"entries[{i}]"));
                    continue;
                }

                AddStageCompanion(rows, entry.StageId.Value, entry);
                AddStageCompanion(rows, entry.StageId.Value, entry.GameplayDefinition);
                AddStageCompanion(rows, entry.StageId.Value, entry.PresentationDefinition);
                AddStageCompanion(rows, entry.StageId.Value, entry.AudioDefinition);
                AddStageCompanion(rows, entry.StageId.Value, entry.ClearEvaluationDefinition);
                AddStageCompanion(rows, entry.StageId.Value, entry.RewardDefinition);
                AddStageCompanion(rows, entry.StageId.Value, entry.ProgressionDefinition);
                AddStageCompanion(rows, entry.StageId.Value, entry.AuthoringDefinition);
            }
        }

        private static void AddStageCompanion(List<AuditRow> rows, string stageId, UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            AddDependencyRow(rows, $"StageCatalogTransitive:{stageId}", path, path, recursive: true);
        }

        private static void AddDependencyRow(
            List<AuditRow> rows,
            string ownerKind,
            string ownerPath,
            string dependencySeedPath,
            bool recursive)
        {
            var dependencies = AssetDatabase.GetDependencies(dependencySeedPath, recursive);
            var externalDependencies = dependencies
                .Where(IsExternalDependency)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            if (externalDependencies.Length == 0)
            {
                return;
            }

            rows.Add(new AuditRow(ownerKind, ownerPath, externalDependencies));
        }

        private static IEnumerable<string> EnumerateAssets(string root)
        {
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { root });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    !string.IsNullOrWhiteSpace(path) &&
                    !AssetDatabase.IsValidFolder(path))
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        private static bool IsExternalDependency(string path)
        {
            for (var i = 0; i < ExternalRoots.Length; i++)
            {
                if (IsUnder(path, ExternalRoots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnder(string path, string root)
        {
            return string.Equals(path, root, StringComparison.Ordinal) ||
                   path.StartsWith(root + "/", StringComparison.Ordinal);
        }

        private static void WriteReport(IReadOnlyList<AuditRow> rows)
        {
            var reportRoot = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrWhiteSpace(reportRoot))
            {
                Directory.CreateDirectory(reportRoot);
            }

            using var writer = new StreamWriter(ReportPath, append: false);
            writer.WriteLine("# External Asset Dependency Audit");
            writer.WriteLine();
            writer.WriteLine($"External roots: `{string.Join("`, `", ExternalRoots)}`");
            writer.WriteLine($"Finding count: {rows.Count(row => row.ExternalDependencies.Count > 0)}");
            writer.WriteLine();
            writer.WriteLine("| Owner Kind | Owner Path | External Dependencies |");
            writer.WriteLine("| --- | --- | --- |");

            if (rows.Count == 0)
            {
                writer.WriteLine("| CLEAN | All scanned owners | None |");
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var dependencyText = row.ExternalDependencies.Count == 0
                    ? "None"
                    : string.Join("<br>", row.ExternalDependencies);
                writer.WriteLine($"| {Escape(row.OwnerKind)} | {Escape(row.OwnerPath)} | {Escape(dependencyText)} |");
            }
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|");
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(GetProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private readonly struct AuditRow
        {
            public AuditRow(string ownerKind, string ownerPath, IReadOnlyList<string> externalDependencies)
            {
                OwnerKind = ownerKind;
                OwnerPath = ownerPath;
                ExternalDependencies = externalDependencies;
            }

            public string OwnerKind { get; }

            public string OwnerPath { get; }

            public IReadOnlyList<string> ExternalDependencies { get; }

            public static AuditRow Missing(string ownerKind, string ownerPath)
            {
                return new AuditRow(ownerKind, ownerPath, Array.Empty<string>());
            }
        }
    }
}
