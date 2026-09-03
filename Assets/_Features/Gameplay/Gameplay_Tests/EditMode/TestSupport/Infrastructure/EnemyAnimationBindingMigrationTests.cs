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

            Assert.That(
                EnemyAnimationSparseBindingAudit.ValidateManifestAndLedger(
                    rows, EnemyAnimationViewDispositionLedger.Rows),
                Is.Empty);
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
                    "Attacking|Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_Attacking.prefab|83caa4e85bf10db439b3962f8682e4ce|BlackEye",
                    "NonAttacking|Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_NonAttacking.prefab|46a5570e50d3d91459ff0980ae576e65|Startis",
                    "Jumping|Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Jumping.prefab|6f32c68c11dbede40b0bc721539b2af5|Astreton",
                    "PrototypeGravityFieldChaser|Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_PrototypeGravityFieldChaser.prefab|63df51ad8ec0533438680ec9b4db5298|DrSaturn",
                },
                deletedRows.Select(row =>
                    $"{row.Name}|{row.PrefabPath}|{row.PrefabGuid}|{row.ReplacementName}"));
            foreach (var row in deletedRows)
            {
                Assert.That(row.IsProduction, Is.False, row.Name);
                Assert.That(row.ExpectedAssetExists, Is.False, row.Name);
                Assert.That(row.RetirementReason, Is.Not.Empty, row.Name);
                Assert.That(File.Exists(row.PrefabPath), Is.False, row.PrefabPath);
                Assert.That(File.Exists(row.PrefabPath + ".meta"), Is.False, row.PrefabPath);
                Assert.That(AssetDatabase.GUIDToAssetPath(row.PrefabGuid), Is.Empty, row.Name);
            }

            var serializedPaths = EnemyAnimationSparseBindingAudit.FindSerializedAssetPaths("Assets");
            var deletedGuids = deletedRows.Select(row => row.PrefabGuid)
                .Append(EnemyAnimationViewDispositionLedger.DeletedJumpingInactiveMaterialGuid);
            Assert.That(
                EnemyAnimationSparseBindingAudit.FindSerializedGuidReferences(serializedPaths, deletedGuids),
                Is.Empty);

            var jumpingInactiveMaterialFolder = Path.GetDirectoryName(
                    EnemyAnimationViewDispositionLedger.DeletedJumpingInactiveMaterialPath)
                ?.Replace('\\', '/');
            Assert.That(Directory.Exists(jumpingInactiveMaterialFolder), Is.False);
            Assert.That(File.Exists(jumpingInactiveMaterialFolder + ".meta"), Is.False);
            Assert.That(File.Exists(EnemyAnimationViewDispositionLedger.DeletedJumpingInactiveMaterialPath), Is.False);
            Assert.That(File.Exists(EnemyAnimationViewDispositionLedger.DeletedJumpingInactiveMaterialPath + ".meta"),
                Is.False);
            Assert.That(
                AssetDatabase.GUIDToAssetPath(EnemyAnimationViewDispositionLedger.DeletedJumpingInactiveMaterialGuid),
                Is.Empty);
        }

        [Test]
        public void ManifestAndLedgerAudit_RejectsLiveIdentityAndDeletedContractDrift()
        {
            var manifestRows = EnemyAnimationBindingMigrationManifest.Rows;
            var originalLedgerRows = EnemyAnimationViewDispositionLedger.Rows;

            var liveDrift = originalLedgerRows.ToArray();
            var live = liveDrift.First(row => row.ExpectedAssetExists);
            liveDrift[Array.IndexOf(liveDrift, live)] = CloneLedgerRow(
                live,
                prefabPath: live.PrefabPath + ".drift");
            Assert.That(
                EnemyAnimationSparseBindingAudit.ValidateManifestAndLedger(manifestRows, liveDrift),
                Has.Some.EqualTo("ledger.live.identity|" + live.Name));

            var deleted = originalLedgerRows.First(row =>
                row.Disposition == EnemyAnimationViewDisposition.Deleted);
            AssertDeletedDriftRejected(manifestRows, originalLedgerRows, deleted,
                CloneLedgerRow(deleted, isProduction: true));
            AssertDeletedDriftRejected(manifestRows, originalLedgerRows, deleted,
                CloneLedgerRow(deleted, expectedAssetExists: true));
            AssertDeletedDriftRejected(manifestRows, originalLedgerRows, deleted,
                CloneLedgerRow(deleted, replacementName: "SecBot"));
        }

        private static void AssertDeletedDriftRejected(
            IReadOnlyList<EnemyAnimationMigrationRow> manifestRows,
            IReadOnlyList<EnemyAnimationViewDispositionRow> originalLedgerRows,
            EnemyAnimationViewDispositionRow original,
            EnemyAnimationViewDispositionRow replacement)
        {
            var driftedRows = originalLedgerRows.ToArray();
            driftedRows[Array.IndexOf(driftedRows, original)] = replacement;
            Assert.That(
                EnemyAnimationSparseBindingAudit.ValidateManifestAndLedger(manifestRows, driftedRows),
                Has.Some.EqualTo("ledger.deleted.contract|" + original.Name));
        }

        private static string Signature(EnemyAnimationMigrationRow row)
        {
            var bindings = string.Join(",", row.Bindings.Select(binding =>
                $"{binding.Cue}:{binding.Mode}:{binding.TargetName}:{binding.DurationSeconds:R}"));
            return $"{row.Name}|{bindings}|{row.CrossFadeSeconds:R}";
        }

        private static EnemyAnimationViewDispositionRow CloneLedgerRow(
            EnemyAnimationViewDispositionRow source,
            string prefabPath = null,
            string replacementName = null,
            bool? isProduction = null,
            bool? expectedAssetExists = null)
        {
            return new EnemyAnimationViewDispositionRow(
                source.Name,
                prefabPath ?? source.PrefabPath,
                source.PrefabGuid,
                isProduction ?? source.IsProduction,
                source.Disposition,
                source.RetirementReason,
                replacementName ?? source.ReplacementName,
                expectedAssetExists ?? source.ExpectedAssetExists);
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
                AssertAssetIdentity(prefab, row.PrefabGuid, row.RootLocalFileId, row.Name + "/root");
                var drivers = prefab.GetComponentsInChildren<EnemyAnimatorDriver>(true);
                Assert.That(drivers, Has.Length.EqualTo(1), row.Name);
                Assert.That(drivers[0].transform, Is.SameAs(prefab.transform), row.Name);
                AssertAssetIdentity(drivers[0], row.PrefabGuid, row.DriverLocalFileId, row.Name + "/driver");
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
        public void ProductionViews_ReloadWithoutMissingScriptsAndKeepCurrentSparseComposition()
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

            var inventory = EnemyAnimationSparseBindingAudit.ScanResolvedPrefabInventory(
                EnemyAnimationBindingMigrationManifest.Rows.Select(row => row.PrefabPath));
            Assert.That(
                EnemyAnimationSparseBindingAudit.ValidateResolvedPrefabInventory(
                    inventory, EnemyAnimationBindingMigrationManifest.Rows),
                Is.Empty);
        }

        private static void AssertAssetIdentity(
            UnityEngine.Object asset,
            string expectedGuid,
            long expectedLocalFileId,
            string context)
        {
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId),
                Is.True,
                context);
            Assert.That(guid, Is.EqualTo(expectedGuid), context);
            Assert.That(localFileId, Is.EqualTo(expectedLocalFileId), context);
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
