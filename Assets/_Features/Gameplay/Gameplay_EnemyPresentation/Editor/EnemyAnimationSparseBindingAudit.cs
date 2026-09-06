using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    internal enum EnemyAnimationDriverYamlAuditStatus
    {
        Clean = 0,
        RetiredPropertiesFound = 1,
        UnsupportedTextAsset = 2,
    }

    internal readonly struct EnemyAnimationDriverYamlAuditRow
    {
        internal EnemyAnimationDriverYamlAuditRow(
            string assetPath,
            EnemyAnimationDriverYamlAuditStatus status,
            int driverBlockCount,
            IReadOnlyList<string> retiredPropertyKeys,
            string diagnostic)
        {
            AssetPath = assetPath ?? string.Empty;
            Status = status;
            DriverBlockCount = driverBlockCount;
            RetiredPropertyKeys = retiredPropertyKeys ?? Array.Empty<string>();
            Diagnostic = diagnostic ?? string.Empty;
        }

        internal string AssetPath { get; }
        internal EnemyAnimationDriverYamlAuditStatus Status { get; }
        internal int DriverBlockCount { get; }
        internal IReadOnlyList<string> RetiredPropertyKeys { get; }
        internal string Diagnostic { get; }
    }

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
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private static readonly Regex YamlDirectiveHeader = new(
            @"\A%YAML [0-9]+\.[0-9]+\r?(?:\n|\z)",
            RegexOptions.CultureInvariant);
        private static readonly Regex UnityDocumentHeader = new(
            @"(?m)^--- !u!(?<classId>[0-9]+) &-?[0-9]+(?: stripped)?\r?$",
            RegexOptions.CultureInvariant);
        private static readonly Regex AnyColumnZeroDocumentDelimiter = new(
            @"(?m)^---[^\r\n]*\r?$",
            RegexOptions.CultureInvariant);
        private static readonly Regex MonoBehaviourBodyHeader = new(
            @"\A--- !u!114 &-?[0-9]+(?: stripped)?\r?\nMonoBehaviour:\r?(?:\n|\z)",
            RegexOptions.CultureInvariant);
        internal static readonly IReadOnlyList<string> RetiredDriverSerializedPropertyNames =
            Array.AsReadOnly(new[]
            {
                "animationTimingAuthoring",
                "windupStateName",
                "jumpWindupStateName",
                "jumpAirborneStateName",
                "chargeActiveStateName",
                "recoveryStateName",
                "glideWindupStateName",
                "glideActiveStateName",
                "glideRecoveryStateName",
                "windupTriggerName",
                "jumpWindupTriggerName",
                "jumpAirborneTriggerName",
                "attackTriggerName",
                "recoveryTriggerName",
                "hitTriggerName",
                "deathTriggerName",
            });
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

        internal static EnemyAnimationDriverYamlAuditRow AuditDriverYamlBytes(
            string assetPath,
            byte[] bytes)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new ArgumentException("An asset path is required.", nameof(assetPath));
            }

            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length == 0 || Array.IndexOf(bytes, (byte)0) >= 0)
            {
                return Unsupported(assetPath, "empty-or-binary");
            }

            string text;
            try
            {
                text = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Unsupported(assetPath, "invalid-utf8");
            }

            if (text.Length > 0 && text[0] == '\ufeff')
            {
                text = text.Substring(1);
            }

            if (!YamlDirectiveHeader.IsMatch(text))
            {
                return Unsupported(assetPath, "missing-or-malformed-yaml-header");
            }

            var documentMatches = UnityDocumentHeader.Matches(text);
            if (documentMatches.Count == 0 ||
                AnyColumnZeroDocumentDelimiter.Matches(text).Count != documentMatches.Count)
            {
                return Unsupported(assetPath, "missing-or-malformed-document-header");
            }

            var retiredKeys = new HashSet<string>(StringComparer.Ordinal);
            var driverBlockCount = 0;
            for (var index = 0; index < documentMatches.Count; index++)
            {
                var match = documentMatches[index];
                var end = index + 1 < documentMatches.Count
                    ? documentMatches[index + 1].Index
                    : text.Length;
                var block = text.Substring(match.Index, end - match.Index);
                if (!string.Equals(match.Groups["classId"].Value, "114", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!MonoBehaviourBodyHeader.IsMatch(block))
                {
                    return Unsupported(assetPath, "malformed-monobehaviour-block");
                }

                if (!Regex.IsMatch(
                        block,
                        @"(?m)^  m_Script: \{[^\r\n}]*\bguid: " +
                        Regex.Escape(EnemyAnimationBindingMigrationManifest.DriverScriptGuid) +
                        @"(?:,|\s|})",
                        RegexOptions.CultureInvariant))
                {
                    continue;
                }

                driverBlockCount++;
                foreach (var propertyName in RetiredDriverSerializedPropertyNames)
                {
                    if (Regex.IsMatch(
                            block,
                            @"(?m)^  " + Regex.Escape(propertyName) + @":",
                            RegexOptions.CultureInvariant))
                    {
                        retiredKeys.Add(propertyName);
                    }
                }
            }

            var orderedKeys = retiredKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            return new EnemyAnimationDriverYamlAuditRow(
                assetPath,
                orderedKeys.Length == 0
                    ? EnemyAnimationDriverYamlAuditStatus.Clean
                    : EnemyAnimationDriverYamlAuditStatus.RetiredPropertiesFound,
                driverBlockCount,
                orderedKeys,
                string.Empty);
        }

        internal static IReadOnlyList<EnemyAnimationDriverYamlAuditRow> ScanDriverYamlResidue(
            IEnumerable<string> assetPaths)
        {
            if (assetPaths == null)
            {
                throw new ArgumentNullException(nameof(assetPaths));
            }

            return assetPaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(NormalizePath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => AuditDriverYamlBytes(path, File.ReadAllBytes(path)))
                .ToArray();
        }

        internal static IReadOnlyList<string> ValidateDriverYamlResidue(
            IReadOnlyList<EnemyAnimationDriverYamlAuditRow> rows,
            IReadOnlyCollection<string> productionPrefabPaths)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (productionPrefabPaths == null)
            {
                throw new ArgumentNullException(nameof(productionPrefabPaths));
            }

            var normalizedProductionPaths = new HashSet<string>(
                productionPrefabPaths.Select(NormalizePath),
                StringComparer.Ordinal);
            var errors = new List<string>();
            foreach (var row in rows)
            {
                if (row.Status == EnemyAnimationDriverYamlAuditStatus.RetiredPropertiesFound)
                {
                    foreach (var key in row.RetiredPropertyKeys)
                    {
                        errors.Add($"driver.yaml.retired|{row.AssetPath}|{key}");
                    }

                    continue;
                }

                if (row.Status != EnemyAnimationDriverYamlAuditStatus.UnsupportedTextAsset)
                {
                    continue;
                }

                var extension = Path.GetExtension(row.AssetPath);
                if (normalizedProductionPaths.Contains(row.AssetPath) ||
                    string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"driver.yaml.unsupported|{row.AssetPath}|{row.Diagnostic}");
                }
            }

            return errors.OrderBy(error => error, StringComparer.Ordinal).ToArray();
        }

        private static EnemyAnimationDriverYamlAuditRow Unsupported(string assetPath, string diagnostic)
        {
            return new EnemyAnimationDriverYamlAuditRow(
                NormalizePath(assetPath),
                EnemyAnimationDriverYamlAuditStatus.UnsupportedTextAsset,
                0,
                Array.Empty<string>(),
                diagnostic);
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
