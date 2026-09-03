using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    [Category("Full")]
    public sealed class EnemyAnimationSparseBindingAssetCharacterizationTests
    {
        private const string DriverScriptGuid = "2b6f35d89b0440b0a3897d9c5c63f8a4";
        private const string TimingScriptGuid = "221aa3bf1f4e4fec8fe376442cc63a61";
        private const string ProductionRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy";
        private const string PrefabRoot = ProductionRoot + "/Prefabs";
        private const string JumpingPrefabPath = PrefabRoot + "/EnemyView_Jumping.prefab";
        private const string DrSaturnProfileGuid = "de83e764216982c1751b31cd7cc75d22";
        private const string GravityFieldAuraCapabilityGuid = "97bbf56322f7f4c260e47dbe5fb697de";

        private static readonly PrefabContract[] ProductionContracts =
        {
            Production("black_eye", "EnemyView_BlackEye.prefab", true, 5272011485980291436L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Attacking.controller"),
            Production("startis", "EnemyView_Startis.prefab", true, 5480558589221225980L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_NonAttack.controller"),
            Production("rocket_face", "EnemyView_RocketFace.prefab", false, 7156270818889048741L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Charge.controller"),
            Production("astreton", "EnemyView_Astreton.prefab", true, 8501000000000003001L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_JumpChaserAstra.controller"),
            Production("dr_saturn", "EnemyView_DrSaturn.prefab", true, 8501000000000008001L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_DrSaturn_GravityField.controller"),
            Production("j_peter", "EnemyView_JPeter.prefab", false, 4338433049514668751L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_JPeter_Fix.controller"),
            Production("sunwheel", "EnemyView_Sunwheel.prefab", true, 8501000000000002001L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_WallFollowerSun.controller"),
            Production("kali", "EnemyView_Kali.prefab", false, 3191573246195697018L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Kali.controller"),
            Production("secbot", "EnemyView_SecBot.prefab", false, 1872103958095815267L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Secbot.controller"),
            Production("nebulous", "EnemyView_Nebulous.prefab", false, 8883044015288239240L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Glider.controller"),
        };

        private static readonly PrefabContract[] NonProductionContracts =
        {
            new(
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_Attacking.prefab",
                true,
                1973374220910874260L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Attacking.controller"),
            new(
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_NonAttacking.prefab",
                true,
                1973374220910874260L,
                string.Empty),
            new(JumpingPrefabPath, true, 1973374220910874260L,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Jump.controller"),
            new(PrefabRoot + "/EnemyView_PrototypeGravityFieldChaser.prefab", true,
                1973374220910874260L, string.Empty),
        };

        private static readonly TimingContract[] ProductionTimingMigrationBaseline =
        {
            Timing("EnemyView_BlackEye.prefab", 1f, -1f, -1f, 1f, 0.001f),
            Timing("EnemyView_Startis.prefab", -1f, -1f, -1f, -1f, -1f),
            Timing("EnemyView_RocketFace.prefab", 0.4f, -1f, -1f, 0.4f, 0.001f),
            Timing("EnemyView_Astreton.prefab", -1f, 0.5f, 1.35f, -1f, 0.001f),
            Timing("EnemyView_DrSaturn.prefab", 0.5f, -1f, -1f, 0.5f, -1f),
            Timing("EnemyView_JPeter.prefab", -1f, -1f, -1f, -1f, -1f),
            Timing("EnemyView_Sunwheel.prefab", -1f, -1f, -1f, -1f, 0.001f),
            Timing("EnemyView_Nebulous.prefab", 0.25f, -1f, -1f, 0.25f, 0f),
        };

        [Test]
        public void ProductionAnimatorWiring_LocksExactResolvedAnimatorAndControllerForTenViewInventory()
        {
            foreach (var contract in ProductionContracts)
            {
                AssertPrefabWiring(contract);
            }

            Assert.That(ProductionContracts.Count(contract => contract.HasExplicitAnimator), Is.EqualTo(5));
            Assert.That(ProductionContracts.Count(contract => !contract.HasExplicitAnimator), Is.EqualTo(5));
        }

        [Test]
        public void DriverAndTimingGuids_MatchFourteenAndTwelvePrefabInventory_WithNoSceneOrAssetReference()
        {
            var allContracts = ProductionContracts.Concat(NonProductionContracts).ToArray();
            var driverPaths = FindYamlReferences("*.prefab", DriverScriptGuid);
            var timingPaths = FindYamlReferences("*.prefab", TimingScriptGuid);

            CollectionAssert.AreEquivalent(
                allContracts.Select(contract => contract.PrefabPath),
                driverPaths,
                "The Driver migration boundary is the exact ten production and four non-production prefabs.");
            Assert.That(driverPaths, Has.Count.EqualTo(14));

            var expectedTimingPaths = allContracts
                .Where(contract => !contract.PrefabPath.EndsWith("EnemyView_Kali.prefab", StringComparison.Ordinal) &&
                                   !contract.PrefabPath.EndsWith("EnemyView_SecBot.prefab", StringComparison.Ordinal))
                .Select(contract => contract.PrefabPath)
                .ToArray();
            CollectionAssert.AreEquivalent(expectedTimingPaths, timingPaths);
            Assert.That(timingPaths, Has.Count.EqualTo(12));

            var directNonPrefabReferences = FindYamlReferences("*.unity", DriverScriptGuid)
                .Concat(FindYamlReferences("*.asset", DriverScriptGuid))
                .Concat(FindYamlReferences("*.unity", TimingScriptGuid))
                .Concat(FindYamlReferences("*.asset", TimingScriptGuid))
                .ToArray();
            Assert.That(directNonPrefabReferences, Is.Empty,
                "Driver and Timing authoring currently have no direct Scene or ScriptableObject references.");

            foreach (var contract in NonProductionContracts)
            {
                AssertPrefabWiring(contract);
            }
        }

        [Test]
        public void ProductionTimingValues_AreMigrationParityBaseline_NotLongTermTuningPolicy()
        {
            CollectionAssert.AreEquivalent(
                ProductionTimingMigrationBaseline.Select(contract => contract.PrefabPath),
                FindYamlReferences("*.prefab", TimingScriptGuid)
                    .Where(path => path.StartsWith(PrefabRoot + "/", StringComparison.Ordinal) &&
                                   !path.EndsWith("EnemyView_Jumping.prefab", StringComparison.Ordinal) &&
                                   !path.EndsWith("EnemyView_PrototypeGravityFieldChaser.prefab", StringComparison.Ordinal)));

            foreach (var contract in ProductionTimingMigrationBaseline)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(contract.PrefabPath);
                Assert.That(prefab, Is.Not.Null, contract.PrefabPath);
                var timing = prefab.GetComponent<EnemyAnimationTimingAuthoring>();
                Assert.That(timing, Is.Not.Null, contract.PrefabPath);

                Assert.That(timing.AttackWindupAnimatorDurationSeconds,
                    Is.EqualTo(contract.AttackWindup).Within(0.0001f), contract.PrefabPath);
                Assert.That(timing.JumpWindupAnimatorDurationSeconds,
                    Is.EqualTo(contract.JumpWindup).Within(0.0001f), contract.PrefabPath);
                Assert.That(timing.JumpAirborneAnimatorDurationSeconds,
                    Is.EqualTo(contract.JumpAirborne).Within(0.0001f), contract.PrefabPath);
                Assert.That(timing.RecoverAnimatorDurationSeconds,
                    Is.EqualTo(contract.Recover).Within(0.0001f), contract.PrefabPath);
                Assert.That(timing.StateTransitionCrossFadeDurationSeconds,
                    Is.EqualTo(contract.CrossFade).Within(0.0001f), contract.PrefabPath);
            }

            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/EnemyView_Kali.prefab")
                    .GetComponent<EnemyAnimationTimingAuthoring>(),
                Is.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/EnemyView_SecBot.prefab")
                    .GetComponent<EnemyAnimationTimingAuthoring>(),
                Is.Null);
        }

        [Test]
        public void JumpingPrefab_InvalidTimingAuthoring_IsDetectedWithoutRepairingPersistentAsset()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JumpingPrefabPath);
            Assert.That(prefab, Is.Not.Null, JumpingPrefabPath);
            var driver = prefab.GetComponent<EnemyAnimatorDriver>();
            var timing = prefab.GetComponent<EnemyAnimationTimingAuthoring>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(timing, Is.Not.Null);

            var serializedDriver = new SerializedObject(driver);
            var serializedTiming = new SerializedObject(timing);
            Assert.That(serializedDriver.FindProperty("jumpAirborneTriggerName").stringValue,
                Is.EqualTo("JumpAirborne"));
            Assert.That(serializedDriver.FindProperty("jumpAirborneStateName").stringValue,
                Is.EqualTo("JumpAirborne"));
            Assert.That(serializedTiming.FindProperty("jumpWindupAnimatorDurationSeconds").floatValue,
                Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(serializedTiming.FindProperty("jumpAirborneAnimatorDurationSeconds").floatValue,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(serializedTiming.FindProperty("jumpWindupReferenceClip").objectReferenceValue, Is.Null);
            Assert.That(serializedTiming.FindProperty("jumpAirborneReferenceClip").objectReferenceValue, Is.Null);
            Assert.That(serializedTiming.FindProperty("stateTransitionCrossFadeDurationSeconds").floatValue,
                Is.EqualTo(-1f));

            var exception = Assert.Throws<InvalidOperationException>(() => timing.Validate());
            Assert.That(exception.Message, Does.Contain("jumpWindupReferenceClip"));
            Assert.That(exception.Message, Does.Contain("jumpWindupAnimatorDurationSeconds"));
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

        private static void AssertPrefabWiring(PrefabContract contract)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(contract.PrefabPath);
            Assert.That(prefab, Is.Not.Null, contract.PrefabPath);
            var drivers = prefab.GetComponents<EnemyAnimatorDriver>();
            Assert.That(drivers, Has.Length.EqualTo(1), contract.PrefabPath);
            var explicitAnimator = new SerializedObject(drivers[0]).FindProperty("animator").objectReferenceValue as Animator;
            Assert.That(explicitAnimator != null, Is.EqualTo(contract.HasExplicitAnimator), contract.PrefabPath);

            var animatorCandidates = prefab.GetComponentsInChildren<Animator>(includeInactive: true);
            if (!contract.HasExplicitAnimator)
            {
                Assert.That(animatorCandidates, Has.Length.EqualTo(1),
                    $"Fallback resolution must remain unambiguous for '{contract.PrefabPath}'.");
            }

            var resolvedAnimator = explicitAnimator != null ? explicitAnimator : animatorCandidates.Single();
            Assert.That(resolvedAnimator, Is.Not.Null, contract.PrefabPath);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    resolvedAnimator,
                    out string resolvedPrefabGuid,
                    out long resolvedLocalId),
                Is.True,
                contract.PrefabPath);
            Assert.That(resolvedPrefabGuid, Is.Not.Empty, contract.PrefabPath);
            Assert.That(resolvedLocalId, Is.EqualTo(contract.AnimatorLocalId), contract.PrefabPath);
            var actualControllerPath = AssetDatabase.GetAssetPath(resolvedAnimator.runtimeAnimatorController);
            Assert.That(actualControllerPath, Is.EqualTo(contract.ControllerPath), contract.PrefabPath);
        }

        private static List<string> FindYamlReferences(string pattern, string guid)
        {
            return Directory.EnumerateFiles("Assets", pattern, SearchOption.AllDirectories)
                .Select(NormalizePath)
                .Where(path => File.ReadAllText(path).Contains("guid: " + guid, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static PrefabContract Production(
            string presentationId,
            string prefabName,
            bool hasExplicitAnimator,
            long animatorLocalId,
            string controllerPath)
        {
            return new PrefabContract(
                PrefabRoot + "/" + prefabName,
                hasExplicitAnimator,
                animatorLocalId,
                controllerPath,
                presentationId);
        }

        private static TimingContract Timing(
            string prefabName,
            float attackWindup,
            float jumpWindup,
            float jumpAirborne,
            float recover,
            float crossFade)
        {
            return new TimingContract(
                PrefabRoot + "/" + prefabName,
                attackWindup,
                jumpWindup,
                jumpAirborne,
                recover,
                crossFade);
        }

        private readonly struct PrefabContract
        {
            public PrefabContract(
                string prefabPath,
                bool hasExplicitAnimator,
                long animatorLocalId,
                string controllerPath,
                string presentationId = "")
            {
                PrefabPath = prefabPath;
                HasExplicitAnimator = hasExplicitAnimator;
                AnimatorLocalId = animatorLocalId;
                ControllerPath = controllerPath;
                PresentationId = presentationId;
            }

            public string PresentationId { get; }
            public string PrefabPath { get; }
            public bool HasExplicitAnimator { get; }
            public long AnimatorLocalId { get; }
            public string ControllerPath { get; }
        }

        private readonly struct TimingContract
        {
            public TimingContract(
                string prefabPath,
                float attackWindup,
                float jumpWindup,
                float jumpAirborne,
                float recover,
                float crossFade)
            {
                PrefabPath = prefabPath;
                AttackWindup = attackWindup;
                JumpWindup = jumpWindup;
                JumpAirborne = jumpAirborne;
                Recover = recover;
                CrossFade = crossFade;
            }

            public string PrefabPath { get; }
            public float AttackWindup { get; }
            public float JumpWindup { get; }
            public float JumpAirborne { get; }
            public float Recover { get; }
            public float CrossFade { get; }
        }
    }
}
