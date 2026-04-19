using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TraverseSettleDocumentationGovernanceTests
    {
        [Test]
        [Category("Extended")]
        public void CanonicalSpec_DescribesTraverseSettleSpatialState_And_ReservedFutureStates()
        {
            var spec = ReadRepoFile("Docs/Architecture/Tick-Simulation-Canonical-Spec.md");

            Assert.That(spec, Does.Contain("Traverse / Settle"));
            Assert.That(spec, Does.Contain("SpatialState"));
            Assert.That(spec, Does.Contain("TraverseContext"));
            Assert.That(spec, Does.Contain("SettlementContext"));
            Assert.That(spec, Does.Contain("orchestration-only"));
            Assert.That(spec, Does.Contain("reserved future state"));
            Assert.That(spec, Does.Contain("TS-01"));
            Assert.That(spec, Does.Contain("TS-09"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayRulesAppendix_DescribesTraverseSettleDistinction_And_AirborneLanding()
        {
            var appendix = ReadRepoFile("Docs/Architecture/Gameplay-Rules-Appendix.md");

            Assert.That(appendix, Does.Contain("Traverse vs Settle"));
            Assert.That(appendix, Does.Contain("Airborne"));
            Assert.That(appendix, Does.Contain("settlement"));
        }

        [Test]
        [Category("Extended")]
        public void PhasedDocs_DescribeStageDefaults_And_DeferredLockTaxonomy_AsClosedContracts()
        {
            var spec = ReadRepoFile("Docs/Architecture/Tick-Simulation-Canonical-Spec.md");
            var appendix = ReadRepoFile("Docs/Architecture/Gameplay-Rules-Appendix.md");

            Assert.That(spec, Does.Contain("current enemy lock retention is `EnemyActionStateTargeting` current lock path 전용 narrow hook"));
            Assert.That(spec, Does.Contain("current implementation default"));
            Assert.That(spec, Does.Contain("internal validation owner, not public scripted framework"));
            Assert.That(appendix, Does.Contain("validator, not a movement framework"));
            Assert.That(appendix, Does.Contain("Deferred lock taxonomy"));
            Assert.That(appendix, Does.Contain("generic lock framework의 seed"));
            Assert.That(appendix, Does.Contain("primary truth는 semantic contract"));
            Assert.That(appendix, Does.Contain("SystemPreMovementValidation"));
            Assert.That(appendix, Does.Contain("FreshSelectionSuppressedWithCurrentEnemyLockRetention"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
