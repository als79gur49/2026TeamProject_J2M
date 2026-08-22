using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileMetadataProbeConsumerBoundaryTests
    {
        private const string ProbePath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileMetadataProbe.cs";

        [TestCase("WriteAllText")]
        [TestCase("WriteAllTextAtomic")]
        [TestCase("Directory.CreateDirectory")]
        [TestCase("File.Delete")]
        [TestCase("Directory.Delete")]
        [TestCase("File.Move")]
        [TestCase("TryRestoreBackup")]
        [TestCase("TryQuarantine")]
        [TestCase("CleanupTempFiles")]
        [TestCase("PlayerPrefs.Set")]
        [TestCase("PlayerPrefs.Delete")]
        public void Probe_SourceDoesNotContainWriteRestoreQuarantineOrMarkerMutation(string forbiddenToken)
        {
            Assert.That(File.ReadAllText(ProbePath), Does.Not.Contain(forbiddenToken), forbiddenToken);
        }

        [Test]
        public void Probe_DoesNotCallMigrationCoordinator()
        {
            var source = File.ReadAllText(ProbePath);

            Assert.That(source, Does.Not.Contain("CampaignSaveMigrationCoordinator"));
            Assert.That(source, Does.Not.Contain(".Migrate"));
        }

        [Test]
        public void Probe_DoesNotReferenceCampaignSaveService()
        {
            var source = File.ReadAllText(ProbePath);

            Assert.That(source, Does.Not.Contain("CampaignSaveService"));
            Assert.That(source, Does.Not.Contain("CampaignSaveServiceFactory"));
            Assert.That(source, Does.Not.Contain("CampaignSaveSlotStoreAdapter"));
        }

        [Test]
        public void Probe_DoesNotReferenceFileCampaignProfileRepositoryLoad()
        {
            var source = File.ReadAllText(ProbePath);

            Assert.That(source, Does.Not.Contain("FileCampaignProfileRepository"));
            Assert.That(source, Does.Not.Contain("ICampaignProfileRepository"));
            Assert.That(source, Does.Not.Contain("CampaignProfileLoadResult"));
            Assert.That(source, Does.Not.Contain("TryRestoreBackup"));
            Assert.That(source, Does.Not.Contain("TryQuarantine"));
        }

        [Test]
        public void RepositoryLoad_MayRestoreBackupButDoesNotQuarantineCorruptPrimary()
        {
            var repositorySource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/FileCampaignProfileRepository.cs");

            Assert.That(repositorySource, Does.Contain("public CampaignProfileLoadResult Load()"));
            Assert.That(repositorySource, Does.Contain("TryRestoreBackup"));
            Assert.That(repositorySource, Does.Not.Contain("TryQuarantine"));
            Assert.That(File.ReadAllText(ProbePath), Does.Not.Contain("new FileCampaignProfileRepository"));
        }
    }
}
