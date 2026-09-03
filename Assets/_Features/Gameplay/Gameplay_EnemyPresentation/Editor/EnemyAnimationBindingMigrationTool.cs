using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    internal enum EnemyAnimationMigrationRowStatus
    {
        LegacyReady = 0,
        ApprovedNoBinding = 1,
        AlreadyMigrated = 2,
        Blocked = 3,
    }

    internal sealed class EnemyAnimationMigrationRowResult
    {
        internal EnemyAnimationMigrationRowResult(
            EnemyAnimationMigrationRow row,
            EnemyAnimationMigrationRowStatus status,
            string sourcePrefabSha256,
            string animatorPath,
            IReadOnlyList<string> errors)
        {
            Row = row;
            Status = status;
            SourcePrefabSha256 = sourcePrefabSha256;
            AnimatorPath = animatorPath;
            Errors = errors;
        }

        internal EnemyAnimationMigrationRow Row { get; }
        internal EnemyAnimationMigrationRowStatus Status { get; }
        internal string SourcePrefabSha256 { get; }
        internal string AnimatorPath { get; }
        internal IReadOnlyList<string> Errors { get; }
    }

    internal sealed class EnemyAnimationMigrationReport
    {
        internal EnemyAnimationMigrationReport(
            IReadOnlyList<EnemyAnimationMigrationRowResult> rows,
            IReadOnlyList<string> globalErrors,
            string canonicalText,
            string sha256)
        {
            Rows = rows;
            GlobalErrors = globalErrors;
            CanonicalText = canonicalText;
            Sha256 = sha256;
        }

        internal IReadOnlyList<EnemyAnimationMigrationRowResult> Rows { get; }
        internal IReadOnlyList<string> GlobalErrors { get; }
        internal string CanonicalText { get; }
        internal string Sha256 { get; }
        internal bool CanApply => GlobalErrors.Count == 0 && Rows.All(row => row.Errors.Count == 0);
    }

    internal sealed class EnemyAnimationMigrationRowApplyException : InvalidOperationException
    {
        internal EnemyAnimationMigrationRowApplyException(
            EnemyAnimationMigrationRow row,
            bool prefabSaved,
            string stage,
            Exception innerException)
            : base($"{row.Name} failed during {stage} (prefabSaved={prefabSaved}).", innerException)
        {
            Row = row;
            PrefabSaved = prefabSaved;
            Stage = stage;
        }

        internal EnemyAnimationMigrationRow Row { get; }
        internal bool PrefabSaved { get; }
        internal string Stage { get; }
    }

    internal static class EnemyAnimationBindingMigrationService
    {
        private const string DriverTimingProperty = "animationTimingAuthoring";
        private static readonly string[] TimingDurationProperties =
        {
            "attackWindupAnimatorDurationSeconds",
            "jumpWindupAnimatorDurationSeconds",
            "jumpAirborneAnimatorDurationSeconds",
            "recoverAnimatorDurationSeconds",
        };

        private static readonly string[] TimingClipProperties =
        {
            "attackWindupReferenceClip",
            "jumpWindupReferenceClip",
            "jumpAirborneReferenceClip",
            "recoverReferenceClip",
        };

        internal static EnemyAnimationMigrationReport DryRun()
        {
            var globalErrors = ValidateGlobalInventoryAndManifest();
            var rowResults = new List<EnemyAnimationMigrationRowResult>();
            foreach (var row in EnemyAnimationBindingMigrationManifest.Rows)
            {
                rowResults.Add(InspectRow(row));
            }

            if (rowResults.Count(result => result.Status == EnemyAnimationMigrationRowStatus.AlreadyMigrated) > 0 &&
                rowResults.Count(result => result.Status == EnemyAnimationMigrationRowStatus.LegacyReady) > 0)
            {
                globalErrors.Add("migration.partial: production prefabs contain a mixed legacy/migrated state.");
            }

            var canonicalText = BuildCanonicalReport(rowResults, globalErrors);
            return new EnemyAnimationMigrationReport(
                rowResults.AsReadOnly(),
                globalErrors.AsReadOnly(),
                canonicalText,
                Sha256(Encoding.UTF8.GetBytes(canonicalText)));
        }

        internal static EnemyAnimationMigrationReport ApplyApprovedProductionMigration()
        {
            var preflight = DryRun();
            if (!preflight.CanApply)
            {
                throw new InvalidOperationException(
                    "Production migration preflight is blocked.\n" + preflight.CanonicalText);
            }

            var migratedRows = preflight.Rows
                .Where(result => result.Row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding)
                .ToArray();
            if (migratedRows.All(result => result.Status == EnemyAnimationMigrationRowStatus.AlreadyMigrated))
            {
                return preflight;
            }

            if (migratedRows.Any(result => result.Status != EnemyAnimationMigrationRowStatus.LegacyReady))
            {
                throw new InvalidOperationException("Production migration is not wholly LegacyReady or AlreadyMigrated.");
            }

            if (EnemyAnimationBindingMigrationManifest.Approval != EnemyAnimationMigrationApproval.Approved ||
                !IsSha256(EnemyAnimationBindingMigrationManifest.ApprovedDryRunSha256))
            {
                throw new InvalidOperationException(
                    "Production migration requires an asset-owner-approved canonical dry-run SHA-256 digest.");
            }

            if (!string.Equals(
                    preflight.Sha256,
                    EnemyAnimationBindingMigrationManifest.ApprovedDryRunSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Approved dry-run digest mismatch. approved={EnemyAnimationBindingMigrationManifest.ApprovedDryRunSha256}, " +
                    $"actual={preflight.Sha256}.");
            }

            var saved = new List<string>();
            try
            {
                foreach (var result in migratedRows.Where(ShouldMutate))
                {
                    ApplyRow(result.Row, result.SourcePrefabSha256);
                    saved.Add(result.Row.PrefabPath);
                }
            }
            catch (Exception exception)
            {
                var rowFailure = exception as EnemyAnimationMigrationRowApplyException;
                var failedPath = rowFailure?.Row.PrefabPath;
                var savedIncludingFailed = saved.Concat(
                        rowFailure != null && rowFailure.PrefabSaved
                            ? new[] { rowFailure.Row.PrefabPath }
                            : Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var unattempted = migratedRows
                    .Select(result => result.Row.PrefabPath)
                    .Except(savedIncludingFailed, StringComparer.Ordinal)
                    .Where(path => !string.Equals(path, failedPath, StringComparison.Ordinal))
                    .ToArray();
                throw new InvalidOperationException(
                    "Production migration stopped after a prefab apply failure. " +
                    $"saved=[{string.Join(",", savedIncludingFailed)}], " +
                    $"failed=[{failedPath ?? "<global>"}], " +
                    $"failedStage=[{rowFailure?.Stage ?? "unknown"}], " +
                    $"unattempted=[{string.Join(",", unattempted)}]. " +
                    "Restore the complete saved target set from checkpoint commit " +
                    "2fb4a2d4e92cdb1443836eeccb19ec429489d847 before retrying.",
                    exception);
            }

            AssetDatabase.Refresh();
            var postflight = DryRun();
            if (!postflight.CanApply || postflight.Rows.Any(result =>
                    result.Row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding &&
                    result.Status != EnemyAnimationMigrationRowStatus.AlreadyMigrated))
            {
                throw new InvalidOperationException("Saved production prefabs failed post-migration reload validation.\n" +
                                                    postflight.CanonicalText);
            }

            return postflight;
        }

        internal static void ConfigureBindingWithSerializedObject(
            EnemyAnimationBindingAuthoring authoring,
            EnemyAnimationMigrationRow row)
        {
            var serialized = new SerializedObject(authoring);
            serialized.FindProperty("defaultStateCrossFadeDurationSeconds").floatValue = row.CrossFadeSeconds;
            var bindings = serialized.FindProperty("bindings");
            bindings.arraySize = row.Bindings.Count;
            for (var index = 0; index < row.Bindings.Count; index++)
            {
                var source = row.Bindings[index];
                var target = bindings.GetArrayElementAtIndex(index);
                target.FindPropertyRelative("cue").intValue = (int)source.Cue;
                target.FindPropertyRelative("primaryDispatchMode").intValue = (int)source.Mode;
                target.FindPropertyRelative("targetName").stringValue = source.TargetName;
                target.FindPropertyRelative("sustainedStateName").stringValue = source.SustainedStateName;
                target.FindPropertyRelative("animatorDurationSeconds").floatValue = source.DurationSeconds;
                target.FindPropertyRelative("referenceClip").objectReferenceValue = ResolveClip(source.Clip);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static EnemyAnimationMigrationRowResult InspectRowForTests(EnemyAnimationMigrationRow row)
        {
            return InspectRow(row);
        }

        internal static void ApplyRowForTests(EnemyAnimationMigrationRow row)
        {
            ApplyRow(row, expectedSourcePrefabSha256: null);
        }

        internal static void ApplyRowForTests(
            EnemyAnimationMigrationRow row,
            string expectedSourcePrefabSha256)
        {
            ApplyRow(row, expectedSourcePrefabSha256);
        }

        internal static bool ShouldMutateForTests(EnemyAnimationMigrationRowResult result)
        {
            return ShouldMutate(result);
        }

        internal static string BuildHumanReport(EnemyAnimationMigrationReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Enemy Animation Sparse Binding — Production Dry Run");
            builder.Append("schema=").AppendLine(EnemyAnimationBindingMigrationManifest.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append("sha256=").AppendLine(report.Sha256);
            builder.Append("canApply=").AppendLine(report.CanApply ? "true" : "false");
            builder.AppendLine();
            foreach (var row in report.Rows)
            {
                builder.Append(row.Row.Name).Append(": ").Append(row.Status)
                    .Append(" | ").Append(row.Row.PrefabPath).AppendLine();
                foreach (var binding in row.Row.Bindings)
                {
                    builder.Append("  ").Append(binding.Cue).Append(" -> ")
                        .Append(binding.Mode).Append(':').Append(binding.TargetName);
                    if (binding.DurationSeconds > 0f)
                    {
                        builder.Append(" | duration=").Append(Float(binding.DurationSeconds))
                            .Append(" | clip=").Append(binding.Clip.Guid).Append(':')
                            .Append(binding.Clip.LocalFileId.ToString(CultureInfo.InvariantCulture));
                    }

                    if (!binding.EffectiveMotion.IsEmpty)
                    {
                        builder.Append(" | effective=").Append(binding.EffectiveMotion.Guid).Append(':')
                            .Append(binding.EffectiveMotion.LocalFileId.ToString(CultureInfo.InvariantCulture));
                    }

                    builder.AppendLine();
                }

                foreach (var error in row.Errors)
                {
                    builder.Append("  BLOCKED: ").AppendLine(error);
                }
            }

            foreach (var error in report.GlobalErrors)
            {
                builder.Append("GLOBAL BLOCKED: ").AppendLine(error);
            }

            return builder.ToString();
        }

        private static List<string> ValidateGlobalInventoryAndManifest()
        {
            var errors = new List<string>();
            var rows = EnemyAnimationBindingMigrationManifest.Rows;
            if (EnemyAnimationBindingMigrationManifest.SchemaVersion <= 0)
            {
                errors.Add("manifest.schema: schema version must be positive.");
            }

            if (rows.Count != 10)
            {
                errors.Add($"manifest.production-count: expected 10, found {rows.Count}.");
            }

            if (rows.Count(row => row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding) != 8 ||
                rows.Count(row => row.Disposition == EnemyAnimationMigrationDisposition.ApprovedNoBinding) != 2)
            {
                errors.Add("manifest.disposition-count: expected 8 MigratedBinding and 2 ApprovedNoBinding rows.");
            }

            AddDuplicates(rows.Select(row => row.PrefabPath), "manifest.path-duplicate", errors);
            AddDuplicates(rows.Select(row => row.PrefabGuid), "manifest.guid-duplicate", errors);
            foreach (var row in rows)
            {
                ValidateManifestRow(row, errors);
            }

            var ledgerRows = EnemyAnimationViewDispositionLedger.Rows;
            var liveRows = ledgerRows
                .Where(row => row.ExpectedAssetExists)
                .ToArray();
            var expectedDriverPaths = liveRows
                .Select(row => row.PrefabPath)
                .ToArray();
            var driverPrefabs = FindYamlReferences("*.prefab", EnemyAnimationBindingMigrationManifest.DriverScriptGuid);
            if (!SetEquals(driverPrefabs, expectedDriverPaths))
            {
                errors.Add("inventory.driver: Driver prefab references differ from the ten live disposition rows.");
            }

            if (EnemyAnimationViewDispositionLedger.SchemaVersion <= 0 || ledgerRows.Count != 14)
            {
                errors.Add("ledger.schema: expected a positive schema version and exactly 14 disposition rows.");
            }

            var liveManifestPaths = rows.Select(row => row.PrefabPath).ToArray();
            if (!SetEquals(expectedDriverPaths, liveManifestPaths))
            {
                errors.Add("ledger.live-allowlist: live disposition rows differ from the production manifest.");
            }

            if (ledgerRows.Count(row => row.Disposition == EnemyAnimationViewDisposition.MigratedBinding) != 8 ||
                ledgerRows.Count(row => row.Disposition == EnemyAnimationViewDisposition.ApprovedNoBinding) != 2 ||
                ledgerRows.Count(row => row.Disposition == EnemyAnimationViewDisposition.Deleted) != 4 ||
                ledgerRows.Any(row => row.Disposition == EnemyAnimationViewDisposition.Archived) ||
                ledgerRows.Any(row => row.Disposition == EnemyAnimationViewDisposition.LegacyBlocked))
            {
                errors.Add("ledger.disposition-count: expected 8 MigratedBinding, 2 ApprovedNoBinding, " +
                           "4 Deleted, 0 Archived, and 0 LegacyBlocked rows.");
            }

            foreach (var row in rows)
            {
                var ledgerRow = liveRows.SingleOrDefault(candidate =>
                    string.Equals(candidate.PrefabPath, row.PrefabPath, StringComparison.Ordinal));
                var expectedDisposition = row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding
                    ? EnemyAnimationViewDisposition.MigratedBinding
                    : EnemyAnimationViewDisposition.ApprovedNoBinding;
                if (ledgerRow == null || !ledgerRow.IsProduction ||
                    ledgerRow.Disposition != expectedDisposition)
                {
                    errors.Add($"ledger.production-disposition: {row.Name} differs from the production manifest.");
                }
            }

            AddDuplicates(ledgerRows.Select(row => row.PrefabPath), "ledger.path-duplicate", errors);
            AddDuplicates(ledgerRows.Select(row => row.PrefabGuid), "ledger.guid-duplicate", errors);
            foreach (var deletedRow in ledgerRows.Where(row => row.Disposition == EnemyAnimationViewDisposition.Deleted))
            {
                if (File.Exists(deletedRow.PrefabPath) || File.Exists(deletedRow.PrefabPath + ".meta") ||
                    !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(deletedRow.PrefabGuid)))
                {
                    errors.Add($"ledger.deleted-residue: {deletedRow.Name} asset or GUID still resolves.");
                }
            }

            var productionMigrationPaths = rows
                .Where(row => row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding)
                .Select(row => row.PrefabPath)
                .ToArray();
            var timingPrefabs = FindYamlReferences(
                "*.prefab", EnemyAnimationBindingMigrationManifest.TimingScriptGuid);
            if (timingPrefabs.Count != 0)
            {
                errors.Add("inventory.timing: expected zero prefab references after legacy View retirement.");
            }

            var bindingPrefabs = FindYamlReferences(
                "*.prefab", EnemyAnimationBindingMigrationManifest.BindingScriptGuid);
            if (!SetEquals(bindingPrefabs, productionMigrationPaths))
            {
                errors.Add("inventory.binding: expected exactly the eight production migration prefabs.");
            }

            var directReferences = FindYamlReferences("*.unity", EnemyAnimationBindingMigrationManifest.DriverScriptGuid)
                .Concat(FindYamlReferences("*.asset", EnemyAnimationBindingMigrationManifest.DriverScriptGuid))
                .Concat(FindYamlReferences("*.unity", EnemyAnimationBindingMigrationManifest.TimingScriptGuid))
                .Concat(FindYamlReferences("*.asset", EnemyAnimationBindingMigrationManifest.TimingScriptGuid))
                .Concat(FindYamlReferences("*.unity", EnemyAnimationBindingMigrationManifest.BindingScriptGuid))
                .Concat(FindYamlReferences("*.asset", EnemyAnimationBindingMigrationManifest.BindingScriptGuid))
                .ToArray();
            if (directReferences.Length != 0)
            {
                errors.Add("inventory.direct-reference: Driver/Timing/Binding has a Scene or ScriptableObject " +
                           "direct reference: " +
                           string.Join(",", directReferences));
            }

            return errors;
        }

        private static bool SetEquals(IEnumerable<string> actual, IEnumerable<string> expected)
        {
            return new HashSet<string>(actual, StringComparer.Ordinal)
                .SetEquals(expected);
        }

        private static void ValidateManifestRow(EnemyAnimationMigrationRow row, ICollection<string> errors)
        {
            if (!row.PrefabPath.StartsWith(
                    EnemyAnimationBindingMigrationManifest.ProductionPrefabRoot + "/",
                    StringComparison.Ordinal))
            {
                errors.Add($"manifest.path: {row.Name} is outside the production allowlist root.");
            }

            if (row.LegacyDriverValues == null || row.LegacyDriverValues.Count != 15)
            {
                errors.Add($"manifest.legacy-values: {row.Name} must pin all 15 legacy scalar fields.");
            }

            if (row.Disposition == EnemyAnimationMigrationDisposition.ApprovedNoBinding)
            {
                if (row.Bindings.Count != 0 || row.TimingLocalFileId != 0)
                {
                    errors.Add($"manifest.no-binding: {row.Name} must have zero target bindings and no Timing component.");
                }

                return;
            }

            if (row.Bindings.Count == 0 || row.TimingLocalFileId == 0)
            {
                errors.Add($"manifest.binding: {row.Name} requires bindings and a legacy Timing identity.");
            }

            AddDuplicates(row.Bindings.Select(binding => binding.Cue.ToString()),
                $"manifest.cue-duplicate:{row.Name}", errors);
            var hasState = row.Bindings.Any(binding => binding.Mode == EnemyAnimationDispatchMode.State);
            if (hasState != (row.CrossFadeSeconds >= 0f) || !IsFinite(row.CrossFadeSeconds))
            {
                errors.Add($"manifest.crossfade: {row.Name} has an invalid state/crossfade combination.");
            }

            foreach (var binding in row.Bindings)
            {
                if (!EnemyAnimationCueCatalog.TryGet(binding.Cue, out var metadata) ||
                    !metadata.Allows(binding.Mode))
                {
                    errors.Add($"manifest.mode: {row.Name}/{binding.Cue} mode is invalid.");
                    continue;
                }

                var hasDuration = binding.DurationSeconds > 0f;
                if (!metadata.SupportsTiming && (hasDuration || !binding.Clip.IsEmpty))
                {
                    errors.Add($"manifest.timing-forbidden: {row.Name}/{binding.Cue} has timing.");
                }
                else if (metadata.SupportsTiming && hasDuration != !binding.Clip.IsEmpty)
                {
                    errors.Add($"manifest.timing-truth-table: {row.Name}/{binding.Cue} duration/clip mismatch.");
                }

                if (binding.Mode == EnemyAnimationDispatchMode.State && binding.EffectiveMotion.IsEmpty)
                {
                    errors.Add($"manifest.effective-motion: {row.Name}/{binding.Cue} requires an exact effective motion identity.");
                }
                else if (binding.Mode != EnemyAnimationDispatchMode.State && !binding.EffectiveMotion.IsEmpty)
                {
                    errors.Add($"manifest.effective-motion: {row.Name}/{binding.Cue} cannot declare a State motion.");
                }
            }
        }

        private static EnemyAnimationMigrationRowResult InspectRow(EnemyAnimationMigrationRow row)
        {
            var errors = new List<string>();
            if (!File.Exists(row.PrefabPath))
            {
                errors.Add("prefab.missing");
                return Result(row, EnemyAnimationMigrationRowStatus.Blocked, string.Empty, string.Empty, errors);
            }

            var sourceHash = Sha256(File.ReadAllBytes(row.PrefabPath));
            if (!string.Equals(AssetDatabase.AssetPathToGUID(row.PrefabPath), row.PrefabGuid,
                    StringComparison.Ordinal))
            {
                errors.Add("prefab.guid");
            }

            var rawYaml = File.ReadAllText(row.PrefabPath);
            GameObject root = null;
            var animatorPath = string.Empty;
            try
            {
                root = PrefabUtility.LoadPrefabContents(row.PrefabPath);
                var drivers = root.GetComponentsInChildren<EnemyAnimatorDriver>(includeInactive: true);
                var timings = root.GetComponentsInChildren<EnemyAnimationTimingAuthoring>(includeInactive: true);
                var bindings = root.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(includeInactive: true);
                if (drivers.Length != 1 || drivers[0].transform != root.transform)
                {
                    errors.Add($"structure.driver: expected one root Driver, found {drivers.Length}.");
                    return Result(row, EnemyAnimationMigrationRowStatus.Blocked, sourceHash, animatorPath, errors);
                }

                var driver = drivers[0];
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
                var persistentDriver = prefabAsset != null ? prefabAsset.GetComponent<EnemyAnimatorDriver>() : null;
                CheckIdentity(prefabAsset, row.PrefabGuid, row.RootLocalFileId, "identity.root", errors);
                CheckIdentity(persistentDriver, row.PrefabGuid, row.DriverLocalFileId, "identity.driver", errors);
                ValidateLegacyDriver(row, driver, rawYaml, errors);

                var serializedDriver = new SerializedObject(driver);
                var explicitAnimator = serializedDriver.FindProperty("animator").objectReferenceValue as Animator;
                if ((explicitAnimator != null) != row.AnimatorWasExplicit)
                {
                    errors.Add("animator.explicit-null");
                }

                var animatorCandidates = root.GetComponentsInChildren<Animator>(includeInactive: true);
                if (explicitAnimator == null && animatorCandidates.Length != 1)
                {
                    errors.Add($"animator.fallback-count: expected one, found {animatorCandidates.Length}.");
                    return Result(row, EnemyAnimationMigrationRowStatus.Blocked, sourceHash, animatorPath, errors);
                }

                var animator = explicitAnimator != null ? explicitAnimator : animatorCandidates.Single();
                animatorPath = AnimationUtility.CalculateTransformPath(animator.transform, root.transform);
                if (row.AnimatorTransformPath.Length != 0 &&
                    !string.Equals(animatorPath, row.AnimatorTransformPath, StringComparison.Ordinal))
                {
                    errors.Add($"animator.path: expected '{row.AnimatorTransformPath}', found '{animatorPath}'.");
                }

                var persistentExplicitAnimator = persistentDriver != null
                    ? new SerializedObject(persistentDriver).FindProperty("animator").objectReferenceValue as Animator
                    : null;
                var persistentAnimatorCandidates = prefabAsset != null
                    ? prefabAsset.GetComponentsInChildren<Animator>(includeInactive: true)
                    : Array.Empty<Animator>();
                var persistentAnimator = persistentExplicitAnimator != null
                    ? persistentExplicitAnimator
                    : persistentAnimatorCandidates.Length == 1 ? persistentAnimatorCandidates[0] : null;
                CheckIdentity(persistentAnimator, row.PrefabGuid, row.AnimatorLocalFileId, "identity.animator", errors);
                CheckIdentity(animator.runtimeAnimatorController, row.ControllerGuid, row.ControllerLocalFileId,
                    "identity.controller", errors);

                if (row.Disposition == EnemyAnimationMigrationDisposition.ApprovedNoBinding)
                {
                    if (timings.Length != 0 || bindings.Length != 0 ||
                        serializedDriver.FindProperty(DriverTimingProperty).objectReferenceValue != null)
                    {
                        errors.Add("no-binding.changed: Timing or Binding authoring is present.");
                    }

                    return Result(row,
                        errors.Count == 0 ? EnemyAnimationMigrationRowStatus.ApprovedNoBinding :
                            EnemyAnimationMigrationRowStatus.Blocked,
                        sourceHash, animatorPath, errors);
                }

                if (bindings.Length == 0 && timings.Length == 1)
                {
                    var persistentTiming = prefabAsset != null
                        ? prefabAsset.GetComponent<EnemyAnimationTimingAuthoring>()
                        : null;
                    CheckIdentity(persistentTiming, row.PrefabGuid, row.TimingLocalFileId, "identity.timing", errors);
                    if (serializedDriver.FindProperty(DriverTimingProperty).objectReferenceValue != timings[0])
                    {
                        errors.Add("timing.driver-reference");
                    }

                    ValidateLegacyTiming(row, timings[0], errors);
                    var temporaryBinding = root.AddComponent<EnemyAnimationBindingAuthoring>();
                    try
                    {
                        ConfigureBindingWithSerializedObject(temporaryBinding, row);
                        ValidateBinding(row, temporaryBinding, animator, errors);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(temporaryBinding);
                    }

                    return Result(row,
                        errors.Count == 0 ? EnemyAnimationMigrationRowStatus.LegacyReady :
                            EnemyAnimationMigrationRowStatus.Blocked,
                        sourceHash, animatorPath, errors);
                }

                if (bindings.Length == 1 && timings.Length == 0 && bindings[0].transform == root.transform &&
                    bindings[0].enabled &&
                    serializedDriver.FindProperty(DriverTimingProperty).objectReferenceValue == null)
                {
                    ValidateBinding(row, bindings[0], animator, errors);
                    return Result(row,
                        errors.Count == 0 ? EnemyAnimationMigrationRowStatus.AlreadyMigrated :
                            EnemyAnimationMigrationRowStatus.Blocked,
                        sourceHash, animatorPath, errors);
                }

                errors.Add($"structure.partial: bindings={bindings.Length}, timings={timings.Length}.");
                return Result(row, EnemyAnimationMigrationRowStatus.Blocked, sourceHash, animatorPath, errors);
            }
            catch (Exception exception)
            {
                errors.Add("exception: " + exception.GetType().Name + ": " + exception.Message);
                return Result(row, EnemyAnimationMigrationRowStatus.Blocked, sourceHash, animatorPath, errors);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static bool ShouldMutate(EnemyAnimationMigrationRowResult result)
        {
            return result.Row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding &&
                   result.Status == EnemyAnimationMigrationRowStatus.LegacyReady;
        }

        private static void ApplyRow(EnemyAnimationMigrationRow row, string expectedSourcePrefabSha256)
        {
            var prefabSaved = false;
            try
            {
                if (expectedSourcePrefabSha256 != null)
                {
                    var currentSha256 = Sha256(File.ReadAllBytes(row.PrefabPath));
                    if (!string.Equals(currentSha256, expectedSourcePrefabSha256, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{row.Name} source SHA-256 changed after preflight. " +
                            $"expected={expectedSourcePrefabSha256}, actual={currentSha256}.");
                    }
                }

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(row.PrefabPath);
                    var driver = root.GetComponent<EnemyAnimatorDriver>();
                    var timing = root.GetComponent<EnemyAnimationTimingAuthoring>();
                    if (driver == null || timing == null ||
                        root.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(true).Length != 0)
                    {
                        throw new InvalidOperationException($"{row.Name} changed after preflight.");
                    }

                    var binding = root.AddComponent<EnemyAnimationBindingAuthoring>();
                    ConfigureBindingWithSerializedObject(binding, row);
                    var animatorProperty = new SerializedObject(driver).FindProperty("animator");
                    var animator = animatorProperty.objectReferenceValue as Animator ??
                                   root.GetComponentsInChildren<Animator>(true).Single();
                    var errors = new List<string>();
                    ValidateBinding(row, binding, animator, errors);
                    if (errors.Count != 0)
                    {
                        throw new InvalidOperationException(string.Join("; ", errors));
                    }

                    var serializedDriver = new SerializedObject(driver);
                    serializedDriver.FindProperty(DriverTimingProperty).objectReferenceValue = null;
                    serializedDriver.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Object.DestroyImmediate(timing, allowDestroyingAssets: true);

                    PrefabUtility.SaveAsPrefabAsset(root, row.PrefabPath, out var success);
                    if (!success)
                    {
                        throw new InvalidOperationException($"Unity failed to save {row.PrefabPath}.");
                    }

                    prefabSaved = true;
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }

                var result = InspectRow(row);
                if (result.Status != EnemyAnimationMigrationRowStatus.AlreadyMigrated)
                {
                    throw new InvalidOperationException(
                        $"{row.Name} failed immediate reload: {string.Join("; ", result.Errors)}");
                }
            }
            catch (EnemyAnimationMigrationRowApplyException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new EnemyAnimationMigrationRowApplyException(
                    row,
                    prefabSaved,
                    prefabSaved ? "post-save-reload" : "pre-save",
                    exception);
            }
        }

        private static void ValidateLegacyDriver(
            EnemyAnimationMigrationRow row,
            EnemyAnimatorDriver driver,
            string rawYaml,
            ICollection<string> errors)
        {
            var serialized = new SerializedObject(driver);
            foreach (var expected in row.LegacyDriverValues.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var property = serialized.FindProperty(expected.Key);
                if (property == null)
                {
                    errors.Add($"legacy.resolved-missing:{expected.Key}");
                    continue;
                }

                if (!string.Equals(property.stringValue, expected.Value, StringComparison.Ordinal))
                {
                    errors.Add($"legacy.resolved:{expected.Key} expected='{expected.Value}' actual='{property.stringValue}'");
                }

                if (!RawYamlContainsScalar(rawYaml, expected.Key, expected.Value))
                {
                    errors.Add($"legacy.raw:{expected.Key}");
                }
            }
        }

        private static void ValidateLegacyTiming(
            EnemyAnimationMigrationRow row,
            EnemyAnimationTimingAuthoring timing,
            ICollection<string> errors)
        {
            var expected = BuildExpectedLegacyTiming(row);
            var serialized = new SerializedObject(timing);
            for (var index = 0; index < TimingDurationProperties.Length; index++)
            {
                var actual = serialized.FindProperty(TimingDurationProperties[index]).floatValue;
                if (Math.Abs(actual - expected[index].DurationSeconds) > 0.00001f)
                {
                    errors.Add($"timing.duration:{TimingDurationProperties[index]} expected={Float(expected[index].DurationSeconds)} " +
                               $"actual={Float(actual)}");
                }

                var actualClip = serialized.FindProperty(TimingClipProperties[index]).objectReferenceValue;
                CheckOptionalIdentity(actualClip, expected[index].Clip,
                    $"timing.clip:{TimingClipProperties[index]}", errors);
            }

            var crossFade = serialized.FindProperty("stateTransitionCrossFadeDurationSeconds").floatValue;
            if (Math.Abs(crossFade - row.LegacyCrossFadeSeconds) > 0.00001f)
            {
                errors.Add($"timing.crossfade expected={Float(row.LegacyCrossFadeSeconds)} actual={Float(crossFade)}");
            }
        }

        private static EnemyAnimationMigrationBinding[] BuildExpectedLegacyTiming(EnemyAnimationMigrationRow row)
        {
            return new[]
            {
                FirstTiming(row, EnemyAnimationCue.ActionWindup, EnemyAnimationCue.ChargeWindup,
                    EnemyAnimationCue.GlideWindup, EnemyAnimationCue.UtilityWindup),
                FirstTiming(row, EnemyAnimationCue.JumpWindup),
                FirstTiming(row, EnemyAnimationCue.JumpAirborne),
                FirstTiming(row, EnemyAnimationCue.ActionRecovery, EnemyAnimationCue.ChargeRecovery,
                    EnemyAnimationCue.GlideRecovery, EnemyAnimationCue.UtilityRecovery),
            };
        }

        private static EnemyAnimationMigrationBinding FirstTiming(
            EnemyAnimationMigrationRow row,
            params EnemyAnimationCue[] cues)
        {
            foreach (var cue in cues)
            {
                var match = row.Bindings.FirstOrDefault(binding => binding.Cue == cue);
                if (match.Cue != EnemyAnimationCue.None && match.DurationSeconds > 0f)
                {
                    return match;
                }
            }

            return new EnemyAnimationMigrationBinding(
                EnemyAnimationCue.None,
                EnemyAnimationDispatchMode.None,
                string.Empty,
                string.Empty,
                -1f,
                default,
                default);
        }

        private static void ValidateBinding(
            EnemyAnimationMigrationRow row,
            EnemyAnimationBindingAuthoring authoring,
            Animator animator,
            ICollection<string> errors)
        {
            try
            {
                authoring.CreateSnapshot();
            }
            catch (Exception exception)
            {
                errors.Add("binding.snapshot: " + exception.Message);
                return;
            }

            var serialized = new SerializedObject(authoring);
            var bindings = serialized.FindProperty("bindings");
            if (bindings.arraySize != row.Bindings.Count)
            {
                errors.Add($"binding.count expected={row.Bindings.Count} actual={bindings.arraySize}");
                return;
            }

            if (Math.Abs(serialized.FindProperty("defaultStateCrossFadeDurationSeconds").floatValue -
                         row.CrossFadeSeconds) > 0.00001f)
            {
                errors.Add("binding.crossfade");
            }

            for (var index = 0; index < row.Bindings.Count; index++)
            {
                var expected = row.Bindings[index];
                var actual = bindings.GetArrayElementAtIndex(index);
                if (actual.FindPropertyRelative("cue").intValue != (int)expected.Cue ||
                    actual.FindPropertyRelative("primaryDispatchMode").intValue != (int)expected.Mode ||
                    !string.Equals(actual.FindPropertyRelative("targetName").stringValue, expected.TargetName,
                        StringComparison.Ordinal) ||
                    !string.Equals(actual.FindPropertyRelative("sustainedStateName").stringValue,
                        expected.SustainedStateName, StringComparison.Ordinal) ||
                    Math.Abs(actual.FindPropertyRelative("animatorDurationSeconds").floatValue -
                             expected.DurationSeconds) > 0.00001f)
                {
                    errors.Add($"binding.row:{index}");
                }

                CheckOptionalIdentity(actual.FindPropertyRelative("referenceClip").objectReferenceValue,
                    expected.Clip, $"binding.clip:{index}", errors);
            }

            foreach (var diagnostic in EnemyAnimationControllerBindingValidator.Validate(authoring, animator))
            {
                if (diagnostic.Severity == EnemyAnimationBindingDiagnosticSeverity.Error)
                {
                    errors.Add($"controller.{diagnostic.Code}: {diagnostic.Message}");
                }
            }

            ValidateEffectiveStateMotions(row, animator, errors);
        }

        private static void ValidateEffectiveStateMotions(
            EnemyAnimationMigrationRow row,
            Animator animator,
            ICollection<string> errors)
        {
            if (!EnemyAnimationControllerBindingValidator.TryUnwrapController(
                    animator.runtimeAnimatorController, out var controller, out var unwrapError))
            {
                errors.Add("effective-motion.controller: " + unwrapError);
                return;
            }

            foreach (var binding in row.Bindings.Where(candidate =>
                         candidate.Mode == EnemyAnimationDispatchMode.State))
            {
                var state = FindState(controller.layers[0].stateMachine, binding.TargetName);
                if (state == null)
                {
                    errors.Add($"effective-motion.state-missing:{binding.Cue}:{binding.TargetName}");
                    continue;
                }

                CheckIdentity(state.motion, binding.EffectiveMotion.Guid, binding.EffectiveMotion.LocalFileId,
                    $"effective-motion.identity:{binding.Cue}:{binding.TargetName}", errors);
            }
        }

        private static UnityEditor.Animations.AnimatorState FindState(
            UnityEditor.Animations.AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (var child in stateMachine.states)
            {
                if (string.Equals(child.state.name, stateName, StringComparison.Ordinal))
                {
                    return child.state;
                }
            }

            foreach (var child in stateMachine.stateMachines)
            {
                var match = FindState(child.stateMachine, stateName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static AnimationClip ResolveClip(EnemyAnimationMigrationClipIdentity identity)
        {
            if (identity.IsEmpty)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(identity.Guid);
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException($"Clip GUID does not resolve: {identity.Guid}.");
            }

            var matches = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                   clip, out string guid, out long localId) &&
                               string.Equals(guid, identity.Guid, StringComparison.Ordinal) &&
                               localId == identity.LocalFileId)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one AnimationClip at {identity.Guid}:{identity.LocalFileId}, found {matches.Length}.");
            }

            return matches[0];
        }

        private static void CheckOptionalIdentity(
            UnityEngine.Object asset,
            EnemyAnimationMigrationClipIdentity expected,
            string code,
            ICollection<string> errors)
        {
            if (expected.IsEmpty)
            {
                if (asset != null)
                {
                    errors.Add(code + ": expected null.");
                }

                return;
            }

            CheckIdentity(asset, expected.Guid, expected.LocalFileId, code, errors);
        }

        private static void CheckIdentity(
            UnityEngine.Object asset,
            string expectedGuid,
            long expectedLocalFileId,
            string code,
            ICollection<string> errors)
        {
            if (asset == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId) ||
                !string.Equals(guid, expectedGuid, StringComparison.Ordinal) ||
                localFileId != expectedLocalFileId)
            {
                errors.Add($"{code}: expected={expectedGuid}:{expectedLocalFileId}, actual={DescribeIdentity(asset)}");
            }
        }

        private static string DescribeIdentity(UnityEngine.Object asset)
        {
            if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset, out string guid, out long localFileId))
            {
                return guid + ":" + localFileId.ToString(CultureInfo.InvariantCulture);
            }

            return "null-or-unresolved";
        }

        private static EnemyAnimationMigrationRowResult Result(
            EnemyAnimationMigrationRow row,
            EnemyAnimationMigrationRowStatus status,
            string hash,
            string animatorPath,
            List<string> errors)
        {
            return new EnemyAnimationMigrationRowResult(row, status, hash, animatorPath, errors.AsReadOnly());
        }

        private static string BuildCanonicalReport(
            IReadOnlyList<EnemyAnimationMigrationRowResult> results,
            IReadOnlyList<string> globalErrors)
        {
            var builder = new StringBuilder();
            builder.Append("schema=").Append(EnemyAnimationBindingMigrationManifest.SchemaVersion)
                .Append('\n');
            foreach (var result in results)
            {
                var row = result.Row;
                builder.Append("row|").Append(row.Name).Append('|').Append(row.PrefabPath).Append('|')
                    .Append(row.PrefabGuid).Append('|').Append(row.Disposition).Append('|').Append(result.Status)
                    .Append("|sourceSha256=").Append(result.SourcePrefabSha256)
                    .Append("|root=").Append(row.RootLocalFileId)
                    .Append("|driver=").Append(row.DriverLocalFileId)
                    .Append("|timing=").Append(row.TimingLocalFileId)
                    .Append("|animatorExplicit=").Append(row.AnimatorWasExplicit ? "true" : "false")
                    .Append("|animatorPath=").Append(result.AnimatorPath)
                    .Append("|animator=").Append(row.AnimatorLocalFileId)
                    .Append("|controller=").Append(row.ControllerGuid).Append(':').Append(row.ControllerLocalFileId)
                    .Append("|legacyCrossfade=").Append(Float(row.LegacyCrossFadeSeconds))
                    .Append("|targetCrossfade=").Append(Float(row.CrossFadeSeconds)).Append('\n');
                foreach (var legacy in row.LegacyDriverValues.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    builder.Append("legacy|").Append(row.Name).Append('|').Append(legacy.Key).Append('|')
                        .Append(Escape(legacy.Value)).Append('\n');
                }

                for (var index = 0; index < row.Bindings.Count; index++)
                {
                    var binding = row.Bindings[index];
                    builder.Append("binding|").Append(row.Name).Append('|').Append(index).Append('|')
                        .Append(binding.Cue).Append('|').Append(binding.Mode).Append('|')
                        .Append(Escape(binding.TargetName)).Append('|').Append(Escape(binding.SustainedStateName))
                        .Append('|').Append(Float(binding.DurationSeconds)).Append('|')
                        .Append(binding.Clip.Guid).Append(':').Append(binding.Clip.LocalFileId)
                        .Append("|effective=").Append(binding.EffectiveMotion.Guid).Append(':')
                        .Append(binding.EffectiveMotion.LocalFileId).Append('\n');
                }

                foreach (var error in result.Errors.OrderBy(value => value, StringComparer.Ordinal))
                {
                    builder.Append("error|").Append(row.Name).Append('|').Append(Escape(error)).Append('\n');
                }
            }

            foreach (var error in globalErrors.OrderBy(value => value, StringComparer.Ordinal))
            {
                builder.Append("global-error|").Append(Escape(error)).Append('\n');
            }

            return builder.ToString();
        }

        private static List<string> FindYamlReferences(string pattern, string guid)
        {
            return Directory.EnumerateFiles("Assets", pattern, SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => File.ReadAllText(path).Contains("guid: " + guid, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static bool RawYamlContainsScalar(string yaml, string propertyName, string expectedValue)
        {
            var expected = "  " + propertyName + ":" +
                           (expectedValue.Length == 0 ? string.Empty : " " + expectedValue);
            return yaml.Replace("\r\n", "\n").Split('\n')
                .Any(line => string.Equals(line.TrimEnd(), expected, StringComparison.Ordinal));
        }

        private static void AddDuplicates(
            IEnumerable<string> values,
            string code,
            ICollection<string> errors)
        {
            foreach (var duplicate in values.GroupBy(value => value, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                errors.Add($"{code}: {duplicate.Key}");
            }
        }

        private static string Sha256(byte[] bytes)
        {
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private static bool IsSha256(string value)
        {
            return value != null && value.Length == 64 &&
                   value.All(character => character >= '0' && character <= '9' ||
                                          character >= 'a' && character <= 'f');
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("%", "%25").Replace("|", "%7C")
                .Replace("\r", "%0D").Replace("\n", "%0A");
        }
    }

    internal static class EnemyAnimationBindingMigrationTool
    {
        [MenuItem("Tools/Enemy/Animation Sparse Binding/Dry Run Production Migration")]
        private static void DryRunProductionMigration()
        {
            var report = EnemyAnimationBindingMigrationService.DryRun();
            var path = CreateEvidencePath("02-dry-run");
            WriteEvidence(path, report);
            var human = EnemyAnimationBindingMigrationService.BuildHumanReport(report);
            UnityEngine.Debug.Log(human + "\nreport=" + path);
            if (!report.CanApply)
            {
                throw new InvalidOperationException("Production dry-run is blocked. See " + path);
            }
        }

        [MenuItem("Tools/Enemy/Animation Sparse Binding/Apply Approved Production Migration")]
        private static void ApplyProductionMigration()
        {
            var path = CreateEvidencePath("03-apply");
            File.WriteAllText(path,
                "status=STARTED\nutc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            try
            {
                var report = EnemyAnimationBindingMigrationService.ApplyApprovedProductionMigration();
                WriteEvidence(path, report);
                UnityEngine.Debug.Log(
                    EnemyAnimationBindingMigrationService.BuildHumanReport(report) + "\nreport=" + path);
            }
            catch (Exception exception)
            {
                File.WriteAllText(path,
                    "status=FAILED\nutc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                    "\nexception=\n" + exception + "\n",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                UnityEngine.Debug.LogError("Production migration failed. report=" + path + "\n" + exception);
                throw;
            }
        }

        private static string CreateEvidencePath(string phase)
        {
            var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var directory = Path.Combine(
                "D:/J2M/evidence/enemy-animation-sparse-binding-slice2",
                runId,
                phase);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "production-migration-report.txt").Replace('\\', '/');
        }

        private static void WriteEvidence(string path, EnemyAnimationMigrationReport report)
        {
            File.WriteAllText(path,
                EnemyAnimationBindingMigrationService.BuildHumanReport(report) + "\n--- canonical ---\n" +
                report.CanonicalText,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
