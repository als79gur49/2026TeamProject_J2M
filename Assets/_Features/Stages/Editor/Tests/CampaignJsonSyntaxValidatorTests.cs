using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignJsonSyntaxValidatorTests
    {
        private const string ProfileRepositoryPath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/FileCampaignProfileRepository.cs";
        private const string LocalStateRepositoryPath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignLocalLaunchStateRepository.cs";
        private const string MetadataProbePath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileMetadataProbe.cs";

        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("true")]
        [TestCase("-12.5e+2")]
        [TestCase("{\"value\":\"\\\"\\\\\\/\\b\\f\\n\\r\\t\\u0041\"}")]
        [TestCase(" { \"items\" : [0, 1, false, null] } \n")]
        public void IsValid_AcceptsValidJson(string json)
        {
            Assert.That(CampaignJsonSyntaxValidator.IsValid(json), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{\"value\":")]
        [TestCase("{} trailing")]
        [TestCase("{\"value\":01}")]
        [TestCase("{\"value\":1.}")]
        [TestCase("{\"value\":1e}")]
        [TestCase("{\"value\":\"\\q\"}")]
        [TestCase("{\"value\":\"\\u12G4\"}")]
        [TestCase("{\"value\":\"line\nbreak\"}")]
        [TestCase("{\"value\":1}\u00A0")]
        public void IsValid_RejectsInvalidJson(string json)
        {
            Assert.That(CampaignJsonSyntaxValidator.IsValid(json), Is.False);
        }

        [Test]
        public void CampaignJsonConsumers_UseSingleSyntaxValidatorOwner()
        {
            var profileRepository = File.ReadAllText(ProfileRepositoryPath);
            var localStateRepository = File.ReadAllText(LocalStateRepositoryPath);
            var metadataProbe = File.ReadAllText(MetadataProbePath);

            Assert.That(profileRepository, Does.Contain("CampaignJsonSyntaxValidator.IsValid"));
            Assert.That(profileRepository, Does.Not.Contain("class JsonSyntaxValidator"));
            Assert.That(localStateRepository, Does.Contain("CampaignJsonSyntaxValidator.IsValid"));
            Assert.That(metadataProbe, Does.Contain("CampaignJsonSyntaxValidator.IsValid"));
            Assert.That(metadataProbe, Does.Not.Contain("LooksLikeJsonObject"));
        }
    }
}
