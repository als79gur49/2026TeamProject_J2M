using System;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    [Category("Full")]
    public sealed class EnemyAnimationSparseBindingAssetCharacterizationTests
    {
        private const string TemporaryRoot = "Assets/__EnemyAnimationSparseBindingAuditTests";
        private const string DrSaturnProfileGuid = "de83e764216982c1751b31cd7cc75d22";
        private const string GravityFieldAuraCapabilityGuid = "97bbf56322f7f4c260e47dbe5fb697de";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TemporaryRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void ProductionAnimatorWiring_LocksExactResolvedAnimatorAndControllerForTenViewInventory()
        {
            foreach (var row in EnemyAnimationBindingMigrationManifest.Rows)
            {
                AssertPrefabWiring(row);
            }

            Assert.That(EnemyAnimationBindingMigrationManifest.Rows.Count(row => row.AnimatorWasExplicit),
                Is.EqualTo(5));
            Assert.That(EnemyAnimationBindingMigrationManifest.Rows.Count(row => !row.AnimatorWasExplicit),
                Is.EqualTo(5));
        }

        [Test]
        public void ResolvedPrefabInventory_MatchesExactTenEightAndZeroProductionContract()
        {
            var inventory = EnemyAnimationSparseBindingAudit.ScanResolvedPrefabInventory(
                EnemyAnimationSparseBindingAudit.FindAllPrefabAssetPaths());
            var errors = EnemyAnimationSparseBindingAudit.ValidateResolvedPrefabInventory(
                inventory, EnemyAnimationBindingMigrationManifest.Rows);
            Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));

            Assert.That(inventory.Count(row => row.DriverCount > 0), Is.EqualTo(10));
            Assert.That(inventory.Count(row => row.BindingCount > 0), Is.EqualTo(8));
            Assert.That(inventory.Count(row => row.TimingCount > 0), Is.Zero);

            var serializedPaths = EnemyAnimationSparseBindingAudit.FindSerializedAssetPaths(
                "Assets", ".unity", ".asset");
            var directNonPrefabReferences = EnemyAnimationSparseBindingAudit.FindSerializedGuidReferences(
                serializedPaths,
                new[]
                {
                    EnemyAnimationBindingMigrationManifest.DriverScriptGuid,
                    EnemyAnimationBindingMigrationManifest.BindingScriptGuid,
                    EnemyAnimationBindingMigrationManifest.TimingScriptGuid,
                });
            Assert.That(directNonPrefabReferences, Is.Empty,
                "Driver, Binding, and Timing authoring have no direct Scene or ScriptableObject references.");
        }

        [Test]
        public void ResolvedPrefabInventory_RejectsVariantThatInheritsUnexpectedDriver()
        {
            EnsureTemporaryRoot();
            var basePath = TemporaryRoot + "/UnexpectedDriverBase.prefab";
            var variantPath = TemporaryRoot + "/UnexpectedDriverVariant.prefab";
            var baseObject = new GameObject("UnexpectedDriverBase");
            try
            {
                baseObject.AddComponent<EnemyAnimatorDriver>();
                PrefabUtility.SaveAsPrefabAsset(baseObject, basePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseObject);
            }

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            var variantInstance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            Assert.That(variantInstance, Is.Not.Null);
            try
            {
                variantInstance.name = "UnexpectedDriverVariant";
                PrefabUtility.SaveAsPrefabAsset(variantInstance, variantPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(variantInstance);
            }

            var variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(variantPrefab), Is.EqualTo(PrefabAssetType.Variant));
            var paths = EnemyAnimationBindingMigrationManifest.Rows.Select(row => row.PrefabPath)
                .Append(variantPath);
            var inventory = EnemyAnimationSparseBindingAudit.ScanResolvedPrefabInventory(paths);
            var variantRow = inventory.Single(row => row.PrefabPath == variantPath);
            Assert.That(variantRow.DriverCount, Is.EqualTo(1), "The inherited Driver must be visible after load.");

            var errors = EnemyAnimationSparseBindingAudit.ValidateResolvedPrefabInventory(
                inventory, EnemyAnimationBindingMigrationManifest.Rows);
            Assert.That(errors, Has.Some.EqualTo("driver.unexpected|" + variantPath));
        }

        [Test]
        public void DeletedGuidResidueAudit_RejectsTemporarySerializedAssetReference()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "enemy-animation-deleted-guid-" + Guid.NewGuid().ToString("N") + ".asset");
            try
            {
                File.WriteAllText(
                    path,
                    "reference: {fileID: 100100000, guid: " +
                    EnemyAnimationViewDispositionLedger.DeletedJumpingInactiveMaterialGuid +
                    ", type: 3}");
                var references = EnemyAnimationSparseBindingAudit.FindSerializedGuidReferences(
                    new[] { path },
                    new[] { EnemyAnimationViewDispositionLedger.DeletedJumpingInactiveMaterialGuid });
                Assert.That(references, Has.Count.EqualTo(1));
                Assert.That(references[0], Does.EndWith(
                    "|" + EnemyAnimationViewDispositionLedger.DeletedJumpingInactiveMaterialGuid));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void ProductionViews_HaveValidRootBindingsOrApprovedNoBindingAndNoLegacyTiming()
        {
            foreach (var row in EnemyAnimationBindingMigrationManifest.Rows)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
                Assert.That(prefab, Is.Not.Null, row.PrefabPath);
                Assert.That(prefab.GetComponentsInChildren<EnemyAnimationTimingAuthoring>(true), Is.Empty,
                    row.PrefabPath);

                var isApprovedNoBinding =
                    row.Disposition == EnemyAnimationMigrationDisposition.ApprovedNoBinding;
                var bindings = prefab.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(true);
                Assert.That(bindings, Has.Length.EqualTo(isApprovedNoBinding ? 0 : 1), row.PrefabPath);
                if (!isApprovedNoBinding)
                {
                    Assert.That(bindings[0].transform, Is.SameAs(prefab.transform), row.PrefabPath);
                    Assert.DoesNotThrow(() => bindings[0].CreateSnapshot(), row.PrefabPath);
                }
            }
        }

        [Test]
        public void DrSaturnPresentationIdentity_ComesFromCatalogProfileAndGravityFieldRuntimeCarrierTogether()
        {
            var profilePath = AssetDatabase.GUIDToAssetPath(DrSaturnProfileGuid);
            var capabilityPath = AssetDatabase.GUIDToAssetPath(GravityFieldAuraCapabilityGuid);
            Assert.That(profilePath, Is.Not.Empty, "GravityFieldChaser profile GUID must resolve.");
            Assert.That(capabilityPath, Is.Not.Empty, "GravityFieldAura capability GUID must resolve.");
            Assert.That(Path.GetFileName(profilePath), Is.EqualTo("EnemyAi_GravityFieldChaser.asset"));
            Assert.That(Path.GetFileName(capabilityPath), Is.EqualTo("EnemyCapability_GravityFieldAura.asset"));
            Assert.That(File.ReadAllText(profilePath), Does.Contain(GravityFieldAuraCapabilityGuid));

            var stageLinks = Directory.EnumerateFiles("Assets/_Features/Stages/Content", "*.asset",
                    SearchOption.AllDirectories)
                .Select(NormalizePath)
                .Where(path =>
                {
                    var lines = File.ReadAllLines(path);
                    return Enumerable.Range(0, Math.Max(0, lines.Length - 1)).Any(index =>
                        lines[index].Contains(DrSaturnProfileGuid, StringComparison.Ordinal) &&
                        string.Equals(lines[index + 1].Trim(), "PresentationId: dr_saturn",
                            StringComparison.Ordinal));
                })
                .ToArray();
            Assert.That(stageLinks, Is.Not.Empty,
                "Production stage authoring must pair GravityFieldChaser gameplay with dr_saturn presentation.");

            var driverFields = typeof(EnemyAnimatorDriver)
                .GetFields(System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.Public |
                           System.Reflection.BindingFlags.NonPublic)
                .Select(field => field.FieldType.Name)
                .ToArray();
            Assert.That(driverFields, Does.Not.Contain("EnemyAiProfile"),
                "Animator presentation binding is not inferred from EnemyAiProfile.");
        }

        private static void AssertPrefabWiring(EnemyAnimationMigrationRow row)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
            Assert.That(prefab, Is.Not.Null, row.PrefabPath);
            var drivers = prefab.GetComponents<EnemyAnimatorDriver>();
            Assert.That(drivers, Has.Length.EqualTo(1), row.PrefabPath);
            var explicitAnimator = new SerializedObject(drivers[0]).FindProperty("animator").objectReferenceValue as Animator;
            Assert.That(explicitAnimator != null, Is.EqualTo(row.AnimatorWasExplicit), row.PrefabPath);

            var animatorCandidates = prefab.GetComponentsInChildren<Animator>(includeInactive: true);
            if (!row.AnimatorWasExplicit)
            {
                Assert.That(animatorCandidates, Has.Length.EqualTo(1),
                    $"Fallback resolution must remain unambiguous for '{row.PrefabPath}'.");
            }

            var resolvedAnimator = explicitAnimator != null ? explicitAnimator : animatorCandidates.Single();
            Assert.That(resolvedAnimator, Is.Not.Null, row.PrefabPath);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    resolvedAnimator,
                    out string resolvedPrefabGuid,
                    out long resolvedLocalId),
                Is.True,
                row.PrefabPath);
            Assert.That(resolvedPrefabGuid, Is.EqualTo(row.PrefabGuid), row.PrefabPath);
            Assert.That(resolvedLocalId, Is.EqualTo(row.AnimatorLocalFileId), row.PrefabPath);
            Assert.That(
                AnimationUtility.CalculateTransformPath(resolvedAnimator.transform, prefab.transform),
                Is.EqualTo(row.AnimatorTransformPath),
                row.PrefabPath);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    resolvedAnimator.runtimeAnimatorController,
                    out string controllerGuid,
                    out long controllerLocalId),
                Is.True,
                row.PrefabPath);
            Assert.That(controllerGuid, Is.EqualTo(row.ControllerGuid), row.PrefabPath);
            Assert.That(controllerLocalId, Is.EqualTo(row.ControllerLocalFileId), row.PrefabPath);
        }

        private static void EnsureTemporaryRoot()
        {
            AssetDatabase.DeleteAsset(TemporaryRoot);
            Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(TemporaryRoot)), Is.Not.Empty);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

    }
}
