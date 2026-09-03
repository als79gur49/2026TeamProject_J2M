using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    [Category("Full")]
    public sealed class EnemyAnimationBindingMigrationManifestTests
    {
        [Test]
        public void Manifest_PinsExactProductionDispositionAndTargetMatrix()
        {
            var rows = EnemyAnimationBindingMigrationManifest.Rows;
            Assert.That(EnemyAnimationBindingMigrationManifest.SchemaVersion, Is.GreaterThan(0));
            Assert.That(rows, Has.Count.EqualTo(10));
            Assert.That(rows.Select(row => row.PrefabPath).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(10));
            Assert.That(rows.Select(row => row.PrefabGuid).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(10));
            Assert.That(rows.Count(row => row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding),
                Is.EqualTo(8));
            Assert.That(rows.Count(row => row.Disposition == EnemyAnimationMigrationDisposition.ApprovedNoBinding),
                Is.EqualTo(2));

            CollectionAssert.AreEqual(
                new[]
                {
                    "BlackEye|ActionWindup:State:Windup:1,ActionRecovery:State:Recover:1,Hit:Trigger:Hit:-1,Death:Trigger:Death:-1|0.001",
                    "Startis|Hit:Trigger:Hit:-1,Death:Trigger:Death:-1|-1",
                    "RocketFace|ChargeWindup:State:Windup:0.4,ChargeActive:State:Charge:-1,ChargeRecovery:State:Recover:0.4,Hit:Trigger:Hit:-1,Death:Trigger:Death:-1|0.001",
                    "Astreton|JumpWindup:State:JumpWindup:0.5,JumpAirborne:State:JumpAirborne:1.35,JumpLanding:State:Move:-1,ActionExecute:Trigger:Attack:-1,Hit:Trigger:Hit:-1,Death:Trigger:Death:-1|0.001",
                    "DrSaturn|UtilityWindup:Trigger:Windup:0.5,UtilityRecovery:Trigger:Recover:0.5,Hit:Trigger:Hit:-1,Death:Trigger:Death:-1|-1",
                    "JPeter|Hit:Trigger:Hit:-1,Death:Trigger:Death:-1|-1",
                    "Sunwheel|Hit:Trigger:Hit:-1,Death:Trigger:Death:-1|-1",
                    "Kali||-1",
                    "SecBot||-1",
                    "Nebulous|GlideWindup:State:Fly_Start:0.25,GlideActive:State:Fly_Loop:-1,GlideRecovery:State:Fly_Done:0.25|0",
                },
                rows.Select(Signature).ToArray());

            CollectionAssert.AreEquivalent(
                new[] { "Kali", "SecBot" },
                rows.Where(row => row.Disposition == EnemyAnimationMigrationDisposition.ApprovedNoBinding)
                    .Select(row => row.Name));
            Assert.That(rows.SelectMany(row => row.Bindings).GroupBy(binding => binding.Cue)
                .Any(group => group.Key == EnemyAnimationCue.None), Is.False);
            Assert.That(rows.Single(row => row.Name == "JPeter").Bindings
                .Any(binding => binding.Cue == EnemyAnimationCue.UtilityRecovery), Is.False,
                "JPeter Summon recovery remains counter-only/no-visual.");
        }

        [Test]
        public void Manifest_ApprovalPinsOneCanonicalDigest()
        {
            var digest = EnemyAnimationBindingMigrationManifest.ApprovedDryRunSha256;
            Assert.That(EnemyAnimationBindingMigrationManifest.Approval,
                Is.EqualTo(EnemyAnimationMigrationApproval.Approved));
            Assert.That(digest, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void DispositionLedger_PinsTenLiveAndFourDeletedViewsWithoutLegacyBlockers()
        {
            var rows = EnemyAnimationViewDispositionLedger.Rows;
            Assert.That(EnemyAnimationViewDispositionLedger.SchemaVersion, Is.GreaterThan(0));
            Assert.That(rows, Has.Count.EqualTo(14));
            Assert.That(rows.Select(row => row.PrefabPath).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(14));
            Assert.That(rows.Select(row => row.PrefabGuid).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(14));
            Assert.That(rows.Count(row => row.Disposition == EnemyAnimationViewDisposition.MigratedBinding),
                Is.EqualTo(8));
            Assert.That(rows.Count(row => row.Disposition == EnemyAnimationViewDisposition.ApprovedNoBinding),
                Is.EqualTo(2));
            Assert.That(rows.Count(row => row.Disposition == EnemyAnimationViewDisposition.Deleted),
                Is.EqualTo(4));
            Assert.That(rows.Count(row => row.Disposition == EnemyAnimationViewDisposition.Archived), Is.Zero);
            Assert.That(rows.Count(row => row.Disposition == EnemyAnimationViewDisposition.LegacyBlocked), Is.Zero);

            var liveRows = rows.Where(row => row.ExpectedAssetExists).ToArray();
            Assert.That(liveRows, Has.Length.EqualTo(10));
            Assert.That(liveRows.All(row => row.IsProduction), Is.True);
            CollectionAssert.AreEqual(
                EnemyAnimationBindingMigrationManifest.Rows.Select(row => row.PrefabPath),
                liveRows.Select(row => row.PrefabPath));
            CollectionAssert.AreEqual(
                EnemyAnimationBindingMigrationManifest.Rows.Select(row => row.PrefabGuid),
                liveRows.Select(row => row.PrefabGuid));
            foreach (var row in liveRows)
            {
                Assert.That(File.Exists(row.PrefabPath), Is.True, row.PrefabPath);
                Assert.That(AssetDatabase.GUIDToAssetPath(row.PrefabGuid), Is.EqualTo(row.PrefabPath), row.Name);
                var manifestRow = EnemyAnimationBindingMigrationManifest.Rows.Single(candidate =>
                    string.Equals(candidate.PrefabPath, row.PrefabPath, StringComparison.Ordinal));
                var expectedDisposition = manifestRow.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding
                    ? EnemyAnimationViewDisposition.MigratedBinding
                    : EnemyAnimationViewDisposition.ApprovedNoBinding;
                Assert.That(row.Disposition, Is.EqualTo(expectedDisposition), row.Name);
            }

            var deletedRows = rows.Where(row => row.Disposition == EnemyAnimationViewDisposition.Deleted).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    "Attacking|83caa4e85bf10db439b3962f8682e4ce|BlackEye",
                    "NonAttacking|46a5570e50d3d91459ff0980ae576e65|Startis",
                    "Jumping|6f32c68c11dbede40b0bc721539b2af5|Astreton",
                    "PrototypeGravityFieldChaser|63df51ad8ec0533438680ec9b4db5298|DrSaturn",
                },
                deletedRows.Select(row => $"{row.Name}|{row.PrefabGuid}|{row.ReplacementName}"));
            foreach (var row in deletedRows)
            {
                Assert.That(row.IsProduction, Is.False, row.Name);
                Assert.That(row.ExpectedAssetExists, Is.False, row.Name);
                Assert.That(row.RetirementReason, Is.Not.Empty, row.Name);
                Assert.That(File.Exists(row.PrefabPath), Is.False, row.PrefabPath);
                Assert.That(File.Exists(row.PrefabPath + ".meta"), Is.False, row.PrefabPath);
                Assert.That(AssetDatabase.GUIDToAssetPath(row.PrefabGuid), Is.Empty, row.Name);
                Assert.That(FindSerializedGuidReferences(row.PrefabGuid), Is.Empty, row.Name);
            }

            const string jumpingInactiveMaterialFolder =
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/" +
                "Materials/InactiveCompatible/EnemyView_Jumping";
            Assert.That(Directory.Exists(jumpingInactiveMaterialFolder), Is.False);
            Assert.That(File.Exists(jumpingInactiveMaterialFolder + ".meta"), Is.False);
        }

        private static string Signature(EnemyAnimationMigrationRow row)
        {
            var bindings = string.Join(",", row.Bindings.Select(binding =>
                $"{binding.Cue}:{binding.Mode}:{binding.TargetName}:{binding.DurationSeconds:R}"));
            return $"{row.Name}|{bindings}|{row.CrossFadeSeconds:R}";
        }

        private static string[] FindSerializedGuidReferences(string guid)
        {
            return Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                .Where(path => File.ReadAllText(path).Contains("guid: " + guid, StringComparison.Ordinal))
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
    }

    [Category("Full")]
    public sealed class EnemyAnimationBindingMigrationDryRunTests
    {
        [Test]
        public void ProductionDryRun_IsDeterministicUniformAndDoesNotMutatePrefabs()
        {
            var before = ProductionHashes();
            var first = EnemyAnimationBindingMigrationService.DryRun();
            var second = EnemyAnimationBindingMigrationService.DryRun();
            var after = ProductionHashes();

            TestContext.WriteLine(EnemyAnimationBindingMigrationService.BuildHumanReport(first));
            foreach (var row in first.Rows)
            {
                TestContext.WriteLine($"ANIMATOR_PATH|{row.Row.Name}|{row.AnimatorPath}");
            }

            Assert.That(first.CanApply, Is.True, first.CanonicalText);
            var migrationStatuses = first.Rows
                .Where(row => row.Row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding)
                .Select(row => row.Status)
                .Distinct()
                .ToArray();
            Assert.That(migrationStatuses, Has.Length.EqualTo(1),
                "Production binding rows must be wholly legacy-ready before apply or wholly migrated afterward.");
            Assert.That(migrationStatuses[0], Is.EqualTo(EnemyAnimationMigrationRowStatus.AlreadyMigrated)
                .Or.EqualTo(EnemyAnimationMigrationRowStatus.LegacyReady));
            Assert.That(first.Rows.Count(row => row.Status == EnemyAnimationMigrationRowStatus.ApprovedNoBinding),
                Is.EqualTo(2));
            Assert.That(first.Sha256, Is.EqualTo(second.Sha256));
            Assert.That(first.CanonicalText, Is.EqualTo(second.CanonicalText));
            CollectionAssert.AreEqual(before, after);
            Assert.That(first.Rows.All(result => result.AnimatorPath.Length != 0), Is.True,
                "Every resolved Animator transform path must be pinned in the manifest after baseline capture.");
        }

        [Test]
        public void ApprovedApply_WhenProductionIsAlreadyMigrated_IsReadOnlyAndIdempotent()
        {
            var before = ProductionHashes();

            var report = EnemyAnimationBindingMigrationService.ApplyApprovedProductionMigration();
            var after = ProductionHashes();

            Assert.That(report.CanApply, Is.True, report.CanonicalText);
            Assert.That(report.Rows.Where(row =>
                    row.Row.Disposition == EnemyAnimationMigrationDisposition.MigratedBinding)
                .All(row => row.Status == EnemyAnimationMigrationRowStatus.AlreadyMigrated), Is.True);
            Assert.That(report.Rows.Where(row =>
                    row.Row.Disposition == EnemyAnimationMigrationDisposition.ApprovedNoBinding)
                .All(row => row.Status == EnemyAnimationMigrationRowStatus.ApprovedNoBinding), Is.True);
            CollectionAssert.AreEqual(before, after);
        }

        private static string[] ProductionHashes()
        {
            return EnemyAnimationBindingMigrationManifest.Rows
                .Select(row => row.PrefabPath + "=" + Sha256(File.ReadAllBytes(row.PrefabPath)))
                .ToArray();
        }

        private static string Sha256(byte[] bytes)
        {
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }
    }

    [Category("Full")]
    public sealed class EnemyAnimationSparseBindingProductionContractTests
    {
        [Test]
        public void ProductionViews_MatchExactManifestSnapshotAndDisposition()
        {
            foreach (var row in EnemyAnimationBindingMigrationManifest.Rows)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
                Assert.That(prefab, Is.Not.Null, row.PrefabPath);
                Assert.That(prefab.GetComponents<EnemyAnimatorDriver>(), Has.Length.EqualTo(1), row.Name);
                Assert.That(prefab.GetComponentsInChildren<EnemyAnimationTimingAuthoring>(true), Is.Empty, row.Name);

                var bindingAuthorings = prefab.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(true);
                if (row.Disposition == EnemyAnimationMigrationDisposition.ApprovedNoBinding)
                {
                    Assert.That(bindingAuthorings, Is.Empty, row.Name);
                    continue;
                }

                Assert.That(bindingAuthorings, Has.Length.EqualTo(1), row.Name);
                Assert.That(bindingAuthorings[0].transform, Is.SameAs(prefab.transform), row.Name);
                var snapshot = bindingAuthorings[0].CreateSnapshot();
                Assert.That(snapshot.Count, Is.EqualTo(row.Bindings.Count), row.Name);
                Assert.That(snapshot.DefaultStateCrossFadeDurationSeconds,
                    Is.EqualTo(row.CrossFadeSeconds).Within(0.000001f), row.Name);

                var serializedBindings = new SerializedObject(bindingAuthorings[0]).FindProperty("bindings");
                Assert.That(serializedBindings.arraySize, Is.EqualTo(row.Bindings.Count), row.Name);
                for (var index = 0; index < row.Bindings.Count; index++)
                {
                    var expected = row.Bindings[index];
                    Assert.That(snapshot.TryGetBinding(expected.Cue, out var actual), Is.True,
                        $"{row.Name}/{expected.Cue}");
                    Assert.That(actual.PrimaryDispatchMode, Is.EqualTo(expected.Mode), $"{row.Name}/{expected.Cue}");
                    Assert.That(actual.TargetName, Is.EqualTo(expected.TargetName), $"{row.Name}/{expected.Cue}");
                    Assert.That(actual.SustainedStateName, Is.EqualTo(expected.SustainedStateName),
                        $"{row.Name}/{expected.Cue}");
                    Assert.That(actual.AnimatorDurationSeconds,
                        Is.EqualTo(expected.DurationSeconds).Within(0.000001f), $"{row.Name}/{expected.Cue}");
                    AssertClipIdentity(actual.ReferenceClip, expected.Clip, $"{row.Name}/{expected.Cue}");

                    var serializedCue = serializedBindings.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("cue").intValue;
                    Assert.That(serializedCue, Is.EqualTo((int)expected.Cue),
                        $"{row.Name} binding order at index {index}");
                }
            }
        }

        [Test]
        public void ProductionViews_ReloadWithoutMissingScriptsAndRemainAlreadyMigrated()
        {
            foreach (var row in EnemyAnimationBindingMigrationManifest.Rows)
            {
                AssetDatabase.ImportAsset(row.PrefabPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var root = PrefabUtility.LoadPrefabContents(row.PrefabPath);
                try
                {
                    var missingCount = root.GetComponentsInChildren<Transform>(true)
                        .Sum(transform => transform.GetComponents<Component>().Count(component => component == null));
                    Assert.That(missingCount, Is.Zero, row.Name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var report = EnemyAnimationBindingMigrationService.DryRun();
            Assert.That(report.CanApply, Is.True, report.CanonicalText);
            Assert.That(report.Rows.Count(result =>
                    result.Status == EnemyAnimationMigrationRowStatus.AlreadyMigrated),
                Is.EqualTo(8), report.CanonicalText);
            Assert.That(report.Rows.Count(result =>
                    result.Status == EnemyAnimationMigrationRowStatus.ApprovedNoBinding),
                Is.EqualTo(2), report.CanonicalText);
        }

        private static void AssertClipIdentity(
            AnimationClip actual,
            EnemyAnimationMigrationClipIdentity expected,
            string context)
        {
            if (expected.IsEmpty)
            {
                Assert.That(actual, Is.Null, context);
                return;
            }

            Assert.That(actual, Is.Not.Null, context);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                actual, out string guid, out long localFileId), Is.True, context);
            Assert.That(guid, Is.EqualTo(expected.Guid), context);
            Assert.That(localFileId, Is.EqualTo(expected.LocalFileId), context);
        }
    }

    [Category("Full")]
    public sealed class EnemyAnimationBindingMigrationSerializationTests
    {
        private const string TemporaryRoot = "Assets/__EnemyAnimationBindingMigrationTests";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TemporaryRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void SyntheticPrefab_MigratesReloadsAndSecondInspectionIsAlreadyMigrated()
        {
            var source = EnemyAnimationBindingMigrationManifest.Rows.Single(row => row.Name == "BlackEye");
            var synthetic = CopyAsSynthetic(source, "BlackEye.prefab");

            EnemyAnimationBindingMigrationService.ApplyRowForTests(synthetic);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(synthetic.PrefabPath);
            Assert.That(prefab.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponent<EnemyAnimationTimingAuthoring>(), Is.Null);
            Assert.That(new SerializedObject(prefab.GetComponent<EnemyAnimatorDriver>())
                .FindProperty("animationTimingAuthoring").objectReferenceValue, Is.Null);
            Assert.That(prefab.GetComponent<EnemyAnimationBindingAuthoring>().CreateSnapshot().Count,
                Is.EqualTo(source.Bindings.Count));

            var second = EnemyAnimationBindingMigrationService.InspectRowForTests(synthetic);
            Assert.That(second.Status, Is.EqualTo(EnemyAnimationMigrationRowStatus.AlreadyMigrated),
                string.Join("; ", second.Errors));
        }

        [Test]
        public void SyntheticPartialMigration_IsHardFailure()
        {
            var source = EnemyAnimationBindingMigrationManifest.Rows.Single(row => row.Name == "BlackEye");
            var synthetic = CopyAsSynthetic(source, "Partial.prefab");
            var root = PrefabUtility.LoadPrefabContents(synthetic.PrefabPath);
            try
            {
                var binding = root.AddComponent<EnemyAnimationBindingAuthoring>();
                EnemyAnimationBindingMigrationService.ConfigureBindingWithSerializedObject(binding, synthetic);
                PrefabUtility.SaveAsPrefabAsset(root, synthetic.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var result = EnemyAnimationBindingMigrationService.InspectRowForTests(synthetic);
            Assert.That(result.Status, Is.EqualTo(EnemyAnimationMigrationRowStatus.Blocked));
            Assert.That(result.Errors, Has.Some.Contains("structure.partial"));
        }

        [Test]
        public void SyntheticInvalidClip_FailsBeforeAnyPrefabSave()
        {
            var source = EnemyAnimationBindingMigrationManifest.Rows.Single(row => row.Name == "BlackEye");
            var synthetic = CopyAsSynthetic(source, "InvalidClip.prefab");
            var invalidBindings = source.Bindings.Select((binding, index) => index == 0
                    ? new EnemyAnimationMigrationBinding(
                        binding.Cue, binding.Mode, binding.TargetName, binding.SustainedStateName,
                        binding.DurationSeconds,
                        new EnemyAnimationMigrationClipIdentity("00000000000000000000000000000000", 1L),
                        binding.EffectiveMotion)
                    : binding)
                .ToArray();
            var invalid = Clone(synthetic, synthetic.PrefabPath, synthetic.PrefabGuid, invalidBindings);
            var before = File.ReadAllBytes(invalid.PrefabPath);

            var exception = Assert.Throws<EnemyAnimationMigrationRowApplyException>(
                () => EnemyAnimationBindingMigrationService.ApplyRowForTests(invalid));

            Assert.That(exception.PrefabSaved, Is.False);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(invalid.PrefabPath));
        }

        [Test]
        public void SyntheticNebulous_StateOnlyMigration_ReloadsExactSnapshot()
        {
            var source = EnemyAnimationBindingMigrationManifest.Rows.Single(row => row.Name == "Nebulous");
            var synthetic = CopyAsSynthetic(source, "Nebulous.prefab");

            EnemyAnimationBindingMigrationService.ApplyRowForTests(synthetic);

            var result = EnemyAnimationBindingMigrationService.InspectRowForTests(synthetic);
            Assert.That(result.Status, Is.EqualTo(EnemyAnimationMigrationRowStatus.AlreadyMigrated),
                string.Join("; ", result.Errors));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(synthetic.PrefabPath);
            var snapshot = prefab.GetComponent<EnemyAnimationBindingAuthoring>().CreateSnapshot();
            Assert.That(snapshot.Bindings.All(binding =>
                binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.State), Is.True);
            Assert.That(snapshot.TryGetBinding(EnemyAnimationCue.GlideWindup, out var windup), Is.True);
            Assert.That(windup.AnimatorDurationSeconds, Is.EqualTo(0.25f));
            Assert.That(snapshot.TryGetBinding(EnemyAnimationCue.GlideRecovery, out var recovery), Is.True);
            Assert.That(recovery.AnimatorDurationSeconds, Is.EqualTo(0.25f));
        }

        [Test]
        public void SyntheticStartis_TriggerOnlyMigration_PreservesMinusOneCrossFade()
        {
            var source = EnemyAnimationBindingMigrationManifest.Rows.Single(row => row.Name == "Startis");
            var synthetic = CopyAsSynthetic(source, "Startis.prefab");

            EnemyAnimationBindingMigrationService.ApplyRowForTests(synthetic);

            var result = EnemyAnimationBindingMigrationService.InspectRowForTests(synthetic);
            Assert.That(result.Status, Is.EqualTo(EnemyAnimationMigrationRowStatus.AlreadyMigrated),
                string.Join("; ", result.Errors));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(synthetic.PrefabPath);
            var snapshot = prefab.GetComponent<EnemyAnimationBindingAuthoring>().CreateSnapshot();
            Assert.That(snapshot.DefaultStateCrossFadeDurationSeconds, Is.EqualTo(-1f));
            Assert.That(snapshot.Bindings.All(binding =>
                binding.PrimaryDispatchMode == EnemyAnimationDispatchMode.Trigger &&
                binding.ReferenceClip == null), Is.True);
        }

        [Test]
        public void SyntheticKali_ApprovedNoBinding_RemainsByteIdenticalAndExcludedFromMutation()
        {
            var source = EnemyAnimationBindingMigrationManifest.Rows.Single(row => row.Name == "Kali");
            var synthetic = CopyApprovedNoBindingAsSynthetic(source, "Kali.prefab");
            var before = File.ReadAllBytes(synthetic.PrefabPath);

            var result = EnemyAnimationBindingMigrationService.InspectRowForTests(synthetic);

            Assert.That(result.Status, Is.EqualTo(EnemyAnimationMigrationRowStatus.ApprovedNoBinding),
                string.Join("; ", result.Errors));
            Assert.That(EnemyAnimationBindingMigrationService.ShouldMutateForTests(result), Is.False);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(synthetic.PrefabPath));
        }

        [Test]
        public void SyntheticInvalidController_FailsBeforeAnyPrefabSave()
        {
            var source = EnemyAnimationBindingMigrationManifest.Rows.Single(row => row.Name == "BlackEye");
            var synthetic = CopyAsSynthetic(source, "InvalidController.prefab");
            var root = PrefabUtility.LoadPrefabContents(synthetic.PrefabPath);
            try
            {
                root.GetComponentsInChildren<Animator>(true).Single().runtimeAnimatorController = null;
                PrefabUtility.SaveAsPrefabAsset(root, synthetic.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var before = File.ReadAllBytes(synthetic.PrefabPath);
            var exception = Assert.Throws<EnemyAnimationMigrationRowApplyException>(
                () => EnemyAnimationBindingMigrationService.ApplyRowForTests(synthetic));
            Assert.That(exception.PrefabSaved, Is.False);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(synthetic.PrefabPath));
        }

        [Test]
        public void SyntheticSourceHashDrift_FailsBeforeAnyPrefabSave()
        {
            var source = EnemyAnimationBindingMigrationManifest.Rows.Single(row => row.Name == "BlackEye");
            var synthetic = CopyAsSynthetic(source, "SourceHashDrift.prefab");
            var before = File.ReadAllBytes(synthetic.PrefabPath);

            var exception = Assert.Throws<EnemyAnimationMigrationRowApplyException>(() =>
                EnemyAnimationBindingMigrationService.ApplyRowForTests(
                    synthetic,
                    new string('0', 64)));

            Assert.That(exception.PrefabSaved, Is.False);
            Assert.That(exception.Stage, Is.EqualTo("pre-save"));
            StringAssert.Contains("source SHA-256 changed after preflight", exception.InnerException?.Message);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(synthetic.PrefabPath));
        }

        private static EnemyAnimationMigrationRow CopyAsSynthetic(
            EnemyAnimationMigrationRow source,
            string fileName)
        {
            if (!AssetDatabase.IsValidFolder(TemporaryRoot))
            {
                AssetDatabase.CreateFolder("Assets", "__EnemyAnimationBindingMigrationTests");
            }

            var path = TemporaryRoot + "/" + fileName;
            Assert.That(AssetDatabase.CopyAsset(source.PrefabPath, path), Is.True);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var binding = root.GetComponent<EnemyAnimationBindingAuthoring>();
                Assert.That(binding, Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(binding);

                var timing = root.AddComponent<EnemyAnimationTimingAuthoring>();
                var expectedTiming = BuildExpectedLegacyTiming(source);
                var serializedTiming = new SerializedObject(timing);
                serializedTiming.FindProperty("attackWindupAnimatorDurationSeconds").floatValue =
                    expectedTiming[0].DurationSeconds;
                serializedTiming.FindProperty("jumpWindupAnimatorDurationSeconds").floatValue =
                    expectedTiming[1].DurationSeconds;
                serializedTiming.FindProperty("jumpAirborneAnimatorDurationSeconds").floatValue =
                    expectedTiming[2].DurationSeconds;
                serializedTiming.FindProperty("recoverAnimatorDurationSeconds").floatValue =
                    expectedTiming[3].DurationSeconds;
                serializedTiming.FindProperty("stateTransitionCrossFadeDurationSeconds").floatValue =
                    source.LegacyCrossFadeSeconds;
                serializedTiming.FindProperty("attackWindupReferenceClip").objectReferenceValue =
                    ResolveOptionalClipForSynthetic(expectedTiming[0].Clip);
                serializedTiming.FindProperty("jumpWindupReferenceClip").objectReferenceValue =
                    ResolveOptionalClipForSynthetic(expectedTiming[1].Clip);
                serializedTiming.FindProperty("jumpAirborneReferenceClip").objectReferenceValue =
                    ResolveOptionalClipForSynthetic(expectedTiming[2].Clip);
                serializedTiming.FindProperty("recoverReferenceClip").objectReferenceValue =
                    ResolveOptionalClipForSynthetic(expectedTiming[3].Clip);
                serializedTiming.ApplyModifiedPropertiesWithoutUndo();

                var serializedDriver = new SerializedObject(root.GetComponent<EnemyAnimatorDriver>());
                serializedDriver.FindProperty("animationTimingAuthoring").objectReferenceValue = timing;
                serializedDriver.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var persistentTiming = prefab.GetComponent<EnemyAnimationTimingAuthoring>();
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                persistentTiming, out _, out long timingLocalFileId), Is.True);
            return Clone(source, path, AssetDatabase.AssetPathToGUID(path), source.Bindings.ToArray(),
                timingLocalFileId);
        }

        private static EnemyAnimationMigrationRow CopyApprovedNoBindingAsSynthetic(
            EnemyAnimationMigrationRow source,
            string fileName)
        {
            if (!AssetDatabase.IsValidFolder(TemporaryRoot))
            {
                AssetDatabase.CreateFolder("Assets", "__EnemyAnimationBindingMigrationTests");
            }

            var path = TemporaryRoot + "/" + fileName;
            Assert.That(AssetDatabase.CopyAsset(source.PrefabPath, path), Is.True);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return Clone(source, path, AssetDatabase.AssetPathToGUID(path), Array.Empty<EnemyAnimationMigrationBinding>());
        }

        private static EnemyAnimationMigrationBinding[] BuildExpectedLegacyTiming(
            EnemyAnimationMigrationRow row)
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

        private static AnimationClip ResolveOptionalClipForSynthetic(
            EnemyAnimationMigrationClipIdentity identity)
        {
            return identity.IsEmpty ? null : ResolveClipForSynthetic(identity);
        }

        private static AnimationClip ResolveClipForSynthetic(EnemyAnimationMigrationClipIdentity identity)
        {
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(identity.Guid))
                .OfType<AnimationClip>()
                .Single(clip => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                    clip, out string guid, out long localFileId) &&
                                guid == identity.Guid && localFileId == identity.LocalFileId);
        }

        private static EnemyAnimationMigrationRow Clone(
            EnemyAnimationMigrationRow source,
            string path,
            string guid,
            EnemyAnimationMigrationBinding[] bindings,
            long? timingLocalFileId = null)
        {
            return new EnemyAnimationMigrationRow(
                source.Name,
                path,
                guid,
                source.Disposition,
                source.RootLocalFileId,
                source.DriverLocalFileId,
                timingLocalFileId ?? source.TimingLocalFileId,
                source.AnimatorWasExplicit,
                source.AnimatorTransformPath,
                source.AnimatorLocalFileId,
                source.ControllerGuid,
                source.ControllerLocalFileId,
                source.LegacyDriverValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                source.LegacyCrossFadeSeconds,
                source.CrossFadeSeconds,
                bindings);
        }
    }

    [Category("Full")]
    public sealed class EnemyAnimationBindingMigrationAssetCharacterizationTests
    {
        [Test]
        public void ProductionStateTargets_ReportExactEffectiveMotionIdentity()
        {
            foreach (var row in EnemyAnimationBindingMigrationManifest.Rows)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
                var animator = prefab.GetComponentsInChildren<Animator>(includeInactive: true).Single();
                Assert.That(
                    EnemyAnimationControllerBindingValidator.TryUnwrapController(
                        animator.runtimeAnimatorController, out var controller, out var error),
                    Is.True,
                    error);
                foreach (var binding in row.Bindings.Where(binding =>
                             binding.Mode == EnemyAnimationDispatchMode.State))
                {
                    var state = FindState(controller.layers[0].stateMachine, binding.TargetName);
                    Assert.That(state, Is.Not.Null, $"{row.Name}/{binding.Cue}/{binding.TargetName}");
                    var motionIdentity = state.motion is AnimationClip clip
                        ? Identity(clip)
                        : state.motion != null ? state.motion.GetType().Name + ":" + state.motion.name : "null";
                    TestContext.WriteLine(
                        $"STATE_MOTION|{row.Name}|{binding.Cue}|{binding.TargetName}|{motionIdentity}");
                    Assert.That(motionIdentity,
                        Is.EqualTo(binding.EffectiveMotion.Guid + ":" +
                                   binding.EffectiveMotion.LocalFileId.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        [Test]
        public void RocketFace_Windup_PinsDistinctTimingReferenceAndEffectiveRuntimeMotion()
        {
            var row = EnemyAnimationBindingMigrationManifest.Rows.Single(candidate => candidate.Name == "RocketFace");
            var windupBinding = row.Bindings.Single(binding => binding.Cue == EnemyAnimationCue.ChargeWindup);
            var timingReference = ResolveClip(windupBinding.Clip);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
            var animator = prefab.GetComponentsInChildren<Animator>(includeInactive: true).Single();
            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            var windupState = FindState(controller.layers[0].stateMachine, "Windup");
            Assert.That(windupState, Is.Not.Null);
            var effectiveMotion = windupState.motion as AnimationClip;
            Assert.That(effectiveMotion, Is.Not.Null);

            var timingIdentity = Identity(timingReference);
            var effectiveIdentity = Identity(effectiveMotion);
            var timingCurveDigest = CurveDigest(timingReference);
            var effectiveCurveDigest = CurveDigest(effectiveMotion);
            var sampledDifference = CompareSampledPoses(prefab, timingReference, effectiveMotion);
            var bindingRuntime = ResolveRuntime(row);

            TestContext.WriteLine($"TIMING_REFERENCE|name={timingReference.name}|identity={timingIdentity}|" +
                                  $"length={Float(timingReference.length)}|frameRate={Float(timingReference.frameRate)}|" +
                                  $"curves={AnimationUtility.GetCurveBindings(timingReference).Length}|" +
                                  $"objectCurves={AnimationUtility.GetObjectReferenceCurveBindings(timingReference).Length}|" +
                                  $"digest={timingCurveDigest}");
            TestContext.WriteLine($"EFFECTIVE_MOTION|state=Windup|name={effectiveMotion.name}|identity={effectiveIdentity}|" +
                                  $"length={Float(effectiveMotion.length)}|frameRate={Float(effectiveMotion.frameRate)}|" +
                                  $"curves={AnimationUtility.GetCurveBindings(effectiveMotion).Length}|" +
                                  $"objectCurves={AnimationUtility.GetObjectReferenceCurveBindings(effectiveMotion).Length}|" +
                                  $"digest={effectiveCurveDigest}");
            TestContext.WriteLine($"SAMPLED_POSE_DIFFERENCE|{sampledDifference}");
            TestContext.WriteLine($"BINDING_RUNTIME|speed={Float(bindingRuntime.Speed)}|" +
                                  $"duration={Float(bindingRuntime.Duration)}|state={bindingRuntime.State}|" +
                                  $"crossfade={Float(bindingRuntime.CrossFade)}|result={bindingRuntime.Result}|" +
                                  $"evaluatedClip={bindingRuntime.EvaluatedClipIdentity}");

            Assert.That(timingIdentity, Is.Not.EqualTo(effectiveIdentity),
                "The timing reference and effective controller motion are distinct assets.");
            Assert.That(timingReference.length, Is.EqualTo(effectiveMotion.length).Within(0.000001f));
            Assert.That(bindingRuntime.Speed,
                Is.EqualTo(timingReference.length / windupBinding.DurationSeconds).Within(0.000001f));
            Assert.That(bindingRuntime.Duration,
                Is.EqualTo(windupBinding.DurationSeconds).Within(0.000001f));
            Assert.That(bindingRuntime.State, Is.EqualTo(windupBinding.TargetName));
            Assert.That(bindingRuntime.CrossFade, Is.EqualTo(row.CrossFadeSeconds).Within(0.000001f));
            Assert.That(bindingRuntime.Result, Is.EqualTo(EnemyAnimationDispatchResult.Applied));
            Assert.That(bindingRuntime.EvaluatedClipIdentity, Is.EqualTo(effectiveIdentity));
        }

        private static RuntimeResult ResolveRuntime(EnemyAnimationMigrationRow row)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var driver = root.GetComponent<EnemyAnimatorDriver>();
                Assert.That(root.GetComponents<EnemyAnimationBindingAuthoring>(), Has.Length.EqualTo(1));

                var animator = driver.ResolveAnimatorForBindingValidation();
                animator.Rebind();
                animator.Update(0f);
                driver.ApplyAnimatorTimingForCue(EnemyAnimationCue.ChargeWindup);
                var dispatch = driver.DispatchCue(EnemyAnimationCue.ChargeWindup);
                animator.Update(0.02f);
                var evaluatedClips = animator.IsInTransition(0)
                    ? animator.GetNextAnimatorClipInfo(0)
                    : animator.GetCurrentAnimatorClipInfo(0);
                var evaluatedClipIdentity = evaluatedClips.Length == 1
                    ? Identity(evaluatedClips[0].clip)
                    : string.Join(",", evaluatedClips.Select(info => Identity(info.clip)));
                return new RuntimeResult(
                    driver.CurrentAnimatorSpeed,
                    driver.CurrentPresentationDurationSeconds,
                    driver.LastCrossFadedStateName,
                    driver.LastCrossFadeDurationSeconds,
                    dispatch,
                    evaluatedClipIdentity);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AnimationClip ResolveClip(EnemyAnimationMigrationClipIdentity identity)
        {
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(identity.Guid))
                .OfType<AnimationClip>()
                .Single(clip => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                    clip, out string guid, out long localId) &&
                                guid == identity.Guid && localId == identity.LocalFileId);
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (var child in stateMachine.states)
            {
                if (string.Equals(child.state.name, name, StringComparison.Ordinal))
                {
                    return child.state;
                }
            }

            foreach (var child in stateMachine.stateMachines)
            {
                var found = FindState(child.stateMachine, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string CurveDigest(AnimationClip clip)
        {
            var text = new StringBuilder();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip)
                         .OrderBy(BindingKey, StringComparer.Ordinal))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                text.Append(BindingKey(binding)).Append('|')
                    .Append(curve.preWrapMode).Append('|').Append(curve.postWrapMode).Append('\n');
                foreach (var key in curve.keys)
                {
                    text.Append(Float(key.time)).Append('|').Append(Float(key.value)).Append('|')
                        .Append(Float(key.inTangent)).Append('|').Append(Float(key.outTangent)).Append('|')
                        .Append(Float(key.inWeight)).Append('|').Append(Float(key.outWeight)).Append('|')
                        .Append((int)key.weightedMode).Append('\n');
                }
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)
                         .OrderBy(BindingKey, StringComparer.Ordinal))
            {
                text.Append("object|").Append(BindingKey(binding)).Append('\n');
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                {
                    text.Append(Float(key.time)).Append('|').Append(Identity(key.value)).Append('\n');
                }
            }

            foreach (var animationEvent in AnimationUtility.GetAnimationEvents(clip))
            {
                text.Append("event|").Append(Float(animationEvent.time)).Append('|')
                    .Append(animationEvent.functionName).Append('|').Append(animationEvent.stringParameter)
                    .Append('|').Append(animationEvent.intParameter).Append('|')
                    .Append(Float(animationEvent.floatParameter)).Append('\n');
            }

            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))
                .Select(value => value.ToString("x2")));
        }

        private static string CompareSampledPoses(
            GameObject prefab,
            AnimationClip timingReference,
            AnimationClip effectiveMotion)
        {
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var animator = root.GetComponentsInChildren<Animator>(includeInactive: true).Single();
                animator.enabled = false;
                var transforms = animator.GetComponentsInChildren<Transform>(includeInactive: true);
                var baseline = transforms.Select(Pose.Capture).ToArray();
                var maxPositionDelta = 0f;
                var maxRotationDegrees = 0f;
                var maxScaleDelta = 0f;
                var differentTransforms = new HashSet<string>(StringComparer.Ordinal);
                foreach (var normalizedTime in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                {
                    Restore(transforms, baseline);
                    timingReference.SampleAnimation(animator.gameObject, timingReference.length * normalizedTime);
                    var referencePose = transforms.Select(Pose.Capture).ToArray();
                    Restore(transforms, baseline);
                    effectiveMotion.SampleAnimation(animator.gameObject, effectiveMotion.length * normalizedTime);
                    var effectivePose = transforms.Select(Pose.Capture).ToArray();
                    for (var index = 0; index < transforms.Length; index++)
                    {
                        var positionDelta = Vector3.Distance(
                            referencePose[index].LocalPosition, effectivePose[index].LocalPosition);
                        var rotationDelta = Quaternion.Angle(
                            referencePose[index].LocalRotation, effectivePose[index].LocalRotation);
                        var scaleDelta = Vector3.Distance(
                            referencePose[index].LocalScale, effectivePose[index].LocalScale);
                        maxPositionDelta = Mathf.Max(maxPositionDelta, positionDelta);
                        maxRotationDegrees = Mathf.Max(maxRotationDegrees, rotationDelta);
                        maxScaleDelta = Mathf.Max(maxScaleDelta, scaleDelta);
                        if (positionDelta > 0.00001f || rotationDelta > 0.001f || scaleDelta > 0.00001f)
                        {
                            differentTransforms.Add(
                                AnimationUtility.CalculateTransformPath(transforms[index], animator.transform));
                        }
                    }
                }

                return $"differentTransforms={differentTransforms.Count}|" +
                       $"maxPosition={Float(maxPositionDelta)}|" +
                       $"maxRotationDegrees={Float(maxRotationDegrees)}|" +
                       $"maxScale={Float(maxScaleDelta)}|" +
                       $"paths={string.Join(",", differentTransforms.OrderBy(path => path, StringComparer.Ordinal))}";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Restore(IReadOnlyList<Transform> transforms, IReadOnlyList<Pose> poses)
        {
            for (var index = 0; index < transforms.Count; index++)
            {
                transforms[index].localPosition = poses[index].LocalPosition;
                transforms[index].localRotation = poses[index].LocalRotation;
                transforms[index].localScale = poses[index].LocalScale;
            }
        }

        private static string BindingKey(EditorCurveBinding binding)
        {
            return binding.path + "|" + binding.type?.FullName + "|" + binding.propertyName;
        }

        private static string Identity(UnityEngine.Object asset)
        {
            return asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                       asset, out string guid, out long localId)
                ? guid + ":" + localId.ToString(CultureInfo.InvariantCulture)
                : "null-or-unresolved";
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private readonly struct RuntimeResult
        {
            internal RuntimeResult(
                float speed,
                float duration,
                string state,
                float crossFade,
                EnemyAnimationDispatchResult result,
                string evaluatedClipIdentity)
            {
                Speed = speed;
                Duration = duration;
                State = state;
                CrossFade = crossFade;
                Result = result;
                EvaluatedClipIdentity = evaluatedClipIdentity;
            }

            internal float Speed { get; }
            internal float Duration { get; }
            internal string State { get; }
            internal float CrossFade { get; }
            internal EnemyAnimationDispatchResult Result { get; }
            internal string EvaluatedClipIdentity { get; }
        }

        private readonly struct Pose
        {
            private Pose(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
            {
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            internal Vector3 LocalPosition { get; }
            internal Quaternion LocalRotation { get; }
            internal Vector3 LocalScale { get; }

            internal static Pose Capture(Transform transform)
            {
                return new Pose(transform.localPosition, transform.localRotation, transform.localScale);
            }
        }
    }
}
