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

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
