using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplaySemanticQueryGovernanceDocumentationTests
    {
        [Test]
        [Category("Extended")]
        public void GameplayTestAutomationGuide_ListsSemanticQueryMigrationGovernanceArtifacts()
        {
            var guide = ReadRepoFile("Docs/Testing/Gameplay-Test-Automation-Guide.md");

            Assert.That(guide, Does.Contain("gameplay semantic query migration boundary"));
            Assert.That(guide, Does.Contain("Tools/check_gameplay_semantic_query_migration.py"));
            Assert.That(guide, Does.Contain("Tools/semantic_query_migration_allowlist.json"));
            Assert.That(guide, Does.Contain("semantic query migration source-scan gate"));
        }

        [Test]
        [Category("Extended")]
        public void SemanticQueryMigrationAllowlist_ListsCurrentQuarantineOwners_AndMetadataFields()
        {
            var allowlist = ReadRepoFile("Tools/semantic_query_migration_allowlist.json");

            Assert.That(allowlist, Does.Contain("\"reason\""));
            Assert.That(allowlist, Does.Contain("\"remove_by_stage\""));
            Assert.That(allowlist, Does.Contain("IsLegalJumpLandingCell"));
            Assert.That(allowlist, Does.Contain("CanOccupyStep"));
            Assert.That(allowlist, Does.Contain("IsChargeStoppingObstacle"));
            Assert.That(allowlist, Does.Contain("CanAcceptJumpLandingCell"));
            Assert.That(allowlist, Does.Contain("CanAcceptImpactFollowThrough"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
