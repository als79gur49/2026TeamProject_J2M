using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageCatalogMigrationOverrideTests
    {
        [Test]
        public void Analyze_WithManualOverride_MarksOverrideAndUsesOverrideDisposition()
        {
            var baseline = StageCatalogMigrationTool.Analyze();
            var combinedPrimary = baseline.items.Single(item =>
                item.sourceAssetPath == "Assets/_Features/Stages/Content/combined-gameplay-showcase/combined-gameplay-showcase.asset");
            var plan = CreatePlan(new StageCatalogMigrationPlanEntry
            {
                SourceAssetGuid = combinedPrimary.sourceAssetGuid,
                Disposition = StageCatalogMigrationDisposition.AliasOnly,
                CanonicalStageId = "combined-gameplay-showcase",
                AliasSourceIds = new[] { "combined-gameplay-showcase-copy" },
                ForcePrimary = false,
            });

            try
            {
                var overridden = StageCatalogMigrationTool.Analyze(plan).items.Single(item =>
                    item.sourceAssetGuid == combinedPrimary.sourceAssetGuid);

                Assert.That(overridden.overrideApplied, Is.True);
                Assert.That(overridden.disposition, Is.EqualTo(StageCatalogMigrationDisposition.AliasOnly.ToString()));
                Assert.That(overridden.chosenStageId, Is.EqualTo("combined-gameplay-showcase"));
                Assert.That(overridden.aliasPlan, Is.EqualTo(new[] { "combined-gameplay-showcase-copy" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plan);
            }
        }

        [Test]
        public void EnsureApplyAllowed_WithDryRunHashMismatch_Throws()
        {
            var report = new StageCatalogMigrationReport
            {
                dryRunHash = "expected-dry-run-hash",
                summary = new StageCatalogMigrationReportSummary(),
            };
            var plan = CreatePlan(new StageCatalogMigrationPlanEntry
            {
                SourceAssetGuid = "synthetic-guid",
                Disposition = StageCatalogMigrationDisposition.Migrate,
                CanonicalStageId = "combined-gameplay-showcase",
                ApprovedBy = "reviewer",
                DryRunHash = "different-hash",
            });

            try
            {
                var exception = Assert.Throws<TargetInvocationException>(() => InvokeEnsureApplyAllowed(report, plan));
                Assert.That(exception?.InnerException, Is.TypeOf<InvalidOperationException>());
                StringAssert.Contains("DryRunHash", exception?.InnerException?.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plan);
            }
        }

        private static StageCatalogMigrationPlan CreatePlan(params StageCatalogMigrationPlanEntry[] entries)
        {
            var plan = ScriptableObject.CreateInstance<StageCatalogMigrationPlan>();
            var entriesField = typeof(StageCatalogMigrationPlan)
                .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            entriesField?.SetValue(plan, entries ?? Array.Empty<StageCatalogMigrationPlanEntry>());
            return plan;
        }

        private static void InvokeEnsureApplyAllowed(StageCatalogMigrationReport report, StageCatalogMigrationPlan plan)
        {
            var method = typeof(StageCatalogMigrationTool)
                .GetMethod("EnsureApplyAllowed", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { report, plan });
        }
    }
}
