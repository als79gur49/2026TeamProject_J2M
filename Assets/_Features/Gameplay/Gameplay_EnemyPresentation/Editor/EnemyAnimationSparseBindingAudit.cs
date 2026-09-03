using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    internal readonly struct EnemyAnimationResolvedPrefabInventoryRow
    {
        internal EnemyAnimationResolvedPrefabInventoryRow(
            string prefabPath,
            int driverCount,
            int rootDriverCount,
            int bindingCount,
            int rootBindingCount,
            int timingCount)
        {
            PrefabPath = prefabPath ?? string.Empty;
            DriverCount = driverCount;
            RootDriverCount = rootDriverCount;
            BindingCount = bindingCount;
            RootBindingCount = rootBindingCount;
            TimingCount = timingCount;
        }

        internal string PrefabPath { get; }
        internal int DriverCount { get; }
        internal int RootDriverCount { get; }
        internal int BindingCount { get; }
        internal int RootBindingCount { get; }
        internal int TimingCount { get; }
    }

    internal static class EnemyAnimationSparseBindingAudit
    {
        private static readonly string[] SerializedAssetExtensions = { ".prefab", ".unity", ".asset" };
        private static readonly IReadOnlyDictionary<string, string> DeletedReplacementNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Attacking", "BlackEye" },
                { "NonAttacking", "Startis" },
                { "Jumping", "Astreton" },
                { "PrototypeGravityFieldChaser", "DrSaturn" },
            };

        internal static IReadOnlyList<string> FindAllPrefabAssetPaths()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        internal static IReadOnlyList<EnemyAnimationResolvedPrefabInventoryRow> ScanResolvedPrefabInventory(
            IEnumerable<string> prefabPaths)
        {
            if (prefabPaths == null)
            {
                throw new ArgumentNullException(nameof(prefabPaths));
            }

            return prefabPaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(ScanResolvedPrefab)
                .ToArray();
        }

        internal static IReadOnlyList<string> ValidateResolvedPrefabInventory(
            IReadOnlyList<EnemyAnimationResolvedPrefabInventoryRow> inventory,
            IReadOnlyList<EnemyAnimationMigrationRow> manifestRows)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (manifestRows == null)
            {
                throw new ArgumentNullException(nameof(manifestRows));
            }

            var errors = new List<string>();
            var expectedDriverPaths = new HashSet<string>(
                manifestRows.Select(row => row.PrefabPath), StringComparer.Ordinal);
            var expectedBindingPaths = new HashSet<string>(
                manifestRows
                    .Where(row => row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding)
                    .Select(row => row.PrefabPath),
                StringComparer.Ordinal);
            var actualDriverPaths = new HashSet<string>(
                inventory.Where(row => row.DriverCount > 0).Select(row => row.PrefabPath), StringComparer.Ordinal);
            var actualBindingPaths = new HashSet<string>(
                inventory.Where(row => row.BindingCount > 0).Select(row => row.PrefabPath), StringComparer.Ordinal);
            var actualTimingPaths = inventory
                .Where(row => row.TimingCount > 0)
                .Select(row => row.PrefabPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            AddSetDifference(errors, "driver.missing", expectedDriverPaths, actualDriverPaths);
            AddSetDifference(errors, "driver.unexpected", actualDriverPaths, expectedDriverPaths);
            AddSetDifference(errors, "binding.missing", expectedBindingPaths, actualBindingPaths);
            AddSetDifference(errors, "binding.unexpected", actualBindingPaths, expectedBindingPaths);
            foreach (var path in actualTimingPaths)
            {
                errors.Add($"timing.unexpected|{path}");
            }

            var rowsByPath = inventory.ToDictionary(row => row.PrefabPath, StringComparer.Ordinal);
            foreach (var manifestRow in manifestRows)
            {
                if (!rowsByPath.TryGetValue(manifestRow.PrefabPath, out var actual))
                {
                    continue;
                }

                if (actual.DriverCount != 1 || actual.RootDriverCount != 1)
                {
                    errors.Add(
                        $"driver.structure|{actual.PrefabPath}|all={actual.DriverCount}|root={actual.RootDriverCount}");
                }

                var expectedBindingCount =
                    manifestRow.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding ? 1 : 0;
                if (actual.BindingCount != expectedBindingCount || actual.RootBindingCount != expectedBindingCount)
                {
                    errors.Add(
                        $"binding.structure|{actual.PrefabPath}|all={actual.BindingCount}|" +
                        $"root={actual.RootBindingCount}|expected={expectedBindingCount}");
                }

                if (actual.TimingCount != 0)
                {
                    errors.Add($"timing.structure|{actual.PrefabPath}|all={actual.TimingCount}");
                }
            }

            return errors.OrderBy(error => error, StringComparer.Ordinal).ToArray();
        }

        internal static IReadOnlyList<string> ValidateManifestAndLedger(
            IReadOnlyList<EnemyAnimationMigrationRow> manifestRows,
            IReadOnlyList<EnemyAnimationViewDispositionRow> ledgerRows)
        {
            if (manifestRows == null)
            {
                throw new ArgumentNullException(nameof(manifestRows));
            }

            if (ledgerRows == null)
            {
                throw new ArgumentNullException(nameof(ledgerRows));
            }

            var errors = new List<string>();
            if (manifestRows.Count != 10)
            {
                errors.Add($"manifest.count|expected=10|actual={manifestRows.Count}");
            }

            if (ledgerRows.Count != 14)
            {
                errors.Add($"ledger.count|expected=14|actual={ledgerRows.Count}");
            }

            AddDuplicateErrors(errors, "manifest.name", manifestRows.Select(row => row.Name));
            AddDuplicateErrors(errors, "manifest.path", manifestRows.Select(row => row.PrefabPath));
            AddDuplicateErrors(errors, "manifest.guid", manifestRows.Select(row => row.PrefabGuid));
            AddDuplicateErrors(errors, "ledger.name", ledgerRows.Select(row => row.Name));
            AddDuplicateErrors(errors, "ledger.path", ledgerRows.Select(row => row.PrefabPath));
            AddDuplicateErrors(errors, "ledger.guid", ledgerRows.Select(row => row.PrefabGuid));

            if (ledgerRows.Count(row => row.Disposition == EnemyAnimationViewDisposition.MigratedBinding) != 8 ||
                ledgerRows.Count(row => row.Disposition == EnemyAnimationViewDisposition.ApprovedNoBinding) != 2 ||
                ledgerRows.Count(row => row.Disposition == EnemyAnimationViewDisposition.Deleted) != 4 ||
                ledgerRows.Any(row => row.Disposition == EnemyAnimationViewDisposition.Archived) ||
                ledgerRows.Any(row => row.Disposition == EnemyAnimationViewDisposition.LegacyBlocked))
            {
                errors.Add("ledger.disposition|expected=8/2/4/0/0");
            }

            foreach (var manifestRow in manifestRows)
            {
                var matchingRows = ledgerRows.Where(row => row.Name == manifestRow.Name).ToArray();
                if (matchingRows.Length != 1)
                {
                    errors.Add($"ledger.live.match|{manifestRow.Name}|actual={matchingRows.Length}");
                    continue;
                }

                var ledgerRow = matchingRows[0];
                var expectedDisposition = manifestRow.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding
                    ? EnemyAnimationViewDisposition.MigratedBinding
                    : EnemyAnimationViewDisposition.ApprovedNoBinding;
                if (!string.Equals(ledgerRow.PrefabPath, manifestRow.PrefabPath, StringComparison.Ordinal) ||
                    !string.Equals(ledgerRow.PrefabGuid, manifestRow.PrefabGuid, StringComparison.Ordinal) ||
                    ledgerRow.Disposition != expectedDisposition ||
                    !ledgerRow.IsProduction ||
                    !ledgerRow.ExpectedAssetExists ||
                    !string.Equals(ledgerRow.ReplacementName, manifestRow.Name, StringComparison.Ordinal))
                {
                    errors.Add($"ledger.live.identity|{manifestRow.Name}");
                }
            }

            foreach (var deletedRow in ledgerRows.Where(row =>
                         row.Disposition == EnemyAnimationViewDisposition.Deleted))
            {
                var hasExpectedReplacement = DeletedReplacementNames.TryGetValue(
                    deletedRow.Name, out var expectedReplacementName);
                if (deletedRow.IsProduction || deletedRow.ExpectedAssetExists ||
                    !hasExpectedReplacement ||
                    !string.Equals(
                        deletedRow.ReplacementName, expectedReplacementName, StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(deletedRow.RetirementReason))
                {
                    errors.Add($"ledger.deleted.contract|{deletedRow.Name}");
                }
            }

            return errors.OrderBy(error => error, StringComparer.Ordinal).ToArray();
        }

        internal static IReadOnlyList<string> FindSerializedAssetPaths(
            string root,
            params string[] extensions)
        {
            if (string.IsNullOrEmpty(root))
            {
                throw new ArgumentException("A serialized asset root is required.", nameof(root));
            }

            var allowedExtensions = extensions == null || extensions.Length == 0
                ? SerializedAssetExtensions
                : extensions;
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => allowedExtensions.Any(extension =>
                    path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                .Select(NormalizePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        internal static IReadOnlyList<string> FindSerializedGuidReferences(
            IEnumerable<string> assetPaths,
            IEnumerable<string> guids)
        {
            if (assetPaths == null)
            {
                throw new ArgumentNullException(nameof(assetPaths));
            }

            if (guids == null)
            {
                throw new ArgumentNullException(nameof(guids));
            }

            var normalizedGuids = guids
                .Where(guid => !string.IsNullOrEmpty(guid))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(guid => guid, StringComparer.Ordinal)
                .ToArray();
            var references = new List<string>();
            foreach (var path in assetPaths
                         .Where(path => !string.IsNullOrEmpty(path))
                         .Select(NormalizePath)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var text = File.ReadAllText(path);
                foreach (var guid in normalizedGuids)
                {
                    if (text.Contains("guid: " + guid, StringComparison.Ordinal))
                    {
                        references.Add(path + "|" + guid);
                    }
                }
            }

            return references;
        }

        private static EnemyAnimationResolvedPrefabInventoryRow ScanResolvedPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Prefab '{path}' could not be loaded as a GameObject.");
            }

            return new EnemyAnimationResolvedPrefabInventoryRow(
                path,
                prefab.GetComponentsInChildren<EnemyAnimatorDriver>(includeInactive: true).Length,
                prefab.GetComponents<EnemyAnimatorDriver>().Length,
                prefab.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(includeInactive: true).Length,
                prefab.GetComponents<EnemyAnimationBindingAuthoring>().Length,
                prefab.GetComponentsInChildren<EnemyAnimationTimingAuthoring>(includeInactive: true).Length);
        }

        private static void AddSetDifference(
            ICollection<string> errors,
            string errorCode,
            IEnumerable<string> left,
            IEnumerable<string> right)
        {
            foreach (var value in left.Except(right, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                errors.Add(errorCode + "|" + value);
            }
        }

        private static void AddDuplicateErrors(
            ICollection<string> errors,
            string errorCode,
            IEnumerable<string> values)
        {
            foreach (var value in values.GroupBy(item => item, StringComparer.Ordinal)
                         .Where(group => group.Count() != 1)
                         .Select(group => group.Key)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                errors.Add(errorCode + "|" + value);
            }
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
