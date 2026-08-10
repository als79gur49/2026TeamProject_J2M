using System;
using System.IO;
using System.Linq;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements;
using NUnit.Framework;
using UnityEditor;

namespace Game.Release.Steamworks.Editor.Tests
{
    public sealed class SteamworksConfigurationExpectationTests
    {
        private const string Head =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Tree =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [Test]
        public void Builder_DerivesCanonicalProductLaunchAndAchievementContracts()
        {
            var report = SteamworksConfigurationExpectationBuilder.Build(Head, Tree);

            Assert.That(report.schemaVersion, Is.EqualTo(1));
            Assert.That(report.classification,
                Is.EqualTo(SteamworksExpectationVocabulary.Classification));
            Assert.That(report.publicationStatus,
                Is.EqualTo(SteamworksExpectationVocabulary.ExpectedNotPublished));
            Assert.That(report.actualIdentityStatus,
                Is.EqualTo(SteamworksExpectationVocabulary.ActualIdentityNotConfigured));
            Assert.That(report.source.head, Is.EqualTo(Head));
            Assert.That(report.source.tree, Is.EqualTo(Tree));
            Assert.That(report.product.companyName, Is.EqualTo(PlayerSettings.companyName));
            Assert.That(report.product.productName, Is.EqualTo(PlayerSettings.productName));

            var canonicalDistribution = WindowsDistributionTargetPolicy.SteamWindows;
            Assert.That(report.distribution.targetId,
                Is.EqualTo(canonicalDistribution.TargetId));
            Assert.That(report.distribution.executable,
                Is.EqualTo(WindowsDistributionTargetPolicy.ExecutableName));
            Assert.That(report.distribution.expectedProvider,
                Is.EqualTo(canonicalDistribution.ExpectedProviderId));
            Assert.That(report.distribution.arguments,
                Is.EqualTo(canonicalDistribution.ExpectedLaunchArguments));
            Assert.That(report.distribution.requiredArtifacts,
                Is.EqualTo(canonicalDistribution.RequiredArtifacts));
            Assert.That(report.distribution.forbiddenArtifacts,
                Is.EqualTo(canonicalDistribution.ForbiddenArtifacts));

            Assert.That(report.achievements, Has.Length.EqualTo(1));
            var expectedMapping = SteamAchievementMapping.Production.Entries.Single();
            Assert.That(report.achievements[0].gameAchievementId,
                Is.EqualTo(expectedMapping.GameAchievementId.Value));
            Assert.That(report.achievements[0].expectedSteamApiName,
                Is.EqualTo(expectedMapping.ExpectedSteamApiName.Value));
            Assert.That(report.achievements[0].publicationStatus,
                Is.EqualTo(SteamworksExpectationVocabulary.ExpectedNotPublished));
        }

        [Test]
        public void ProductionMapping_IsCompleteAndEnumerableWithoutSecondSource()
        {
            Assert.DoesNotThrow(
                SteamworksConfigurationExpectationBuilder
                    .ValidateProductionAchievementCompleteness);
            Assert.That(SteamAchievementMapping.Production.Entries.Count,
                Is.EqualTo(GameAchievementCatalog.Production.Definitions.Count));
            Assert.That(
                SteamAchievementMapping.Production.Entries
                    .Select(entry => entry.GameAchievementId),
                Is.EqualTo(GameAchievementCatalog.Production.Definitions
                    .Select(definition => definition.Id)));
        }

        [Test]
        public void CanonicalJson_ContainsOneExpectedMappingAndNoActualIdentity()
        {
            var json = SteamworksConfigurationExpectationSerializer.Serialize(
                SteamworksConfigurationExpectationBuilder.Build(Head, Tree));

            Assert.That(Count(json, "campaign.complete"), Is.EqualTo(1));
            Assert.That(Count(json, "VQ_CAMPAIGN_COMPLETE"), Is.EqualTo(1));
            Assert.That(Count(json, "EXPECTED_NOT_PUBLISHED"), Is.EqualTo(2));
            Assert.That(json, Does.Contain("ACTUAL_IDENTITY_NOT_CONFIGURED"));
            Assert.That(json, Does.Not.Contain("\"appId\""));
            Assert.That(json, Does.Not.Contain("\"depotId\""));
            Assert.That(json, Does.Not.Contain("ACH_WIN_ONE_GAME"));
            Assert.That(json, Does.Not.Contain("Spacewar"));
            Assert.That(json, Does.Not.Contain("SteamID"));
            Assert.That(json, Does.Not.Contain("account"));
        }

        [Test]
        public void Serialization_IsByteAndHashDeterministicForSameSource()
        {
            var first = SteamworksConfigurationExpectationSerializer.Serialize(
                SteamworksConfigurationExpectationBuilder.Build(Head, Tree));
            var second = SteamworksConfigurationExpectationSerializer.Serialize(
                SteamworksConfigurationExpectationBuilder.Build(Head, Tree));

            Assert.That(second, Is.EqualTo(first));
            Assert.That(
                SteamworksConfigurationExpectationSerializer.ComputeSha256(second),
                Is.EqualTo(
                    SteamworksConfigurationExpectationSerializer.ComputeSha256(first)));
            Assert.That(first, Does.EndWith("\n"));
            Assert.That(first, Does.Not.Contain("\r"));
        }

        [Test]
        public void Cli_WritesReportAndRecordedHashOutsideRepository()
        {
            var output = Path.Combine(
                Path.GetTempPath(),
                "j2m-steamworks-expectation-" + Guid.NewGuid().ToString("N"));
            try
            {
                var reportPath = SteamworksConfigurationExpectationCli.Export(new[]
                {
                    SteamworksConfigurationExpectationCli.OutputDirectoryArgument,
                    output,
                    SteamworksConfigurationExpectationCli.SourceHeadArgument,
                    Head,
                    SteamworksConfigurationExpectationCli.SourceTreeArgument,
                    Tree,
                });

                var json = File.ReadAllText(reportPath);
                var expectedHash =
                    SteamworksConfigurationExpectationSerializer.ComputeSha256(json);
                var hashPath = Path.Combine(
                    output,
                    SteamworksConfigurationExpectationCli.HashFileName);
                Assert.That(File.ReadAllText(hashPath),
                    Is.EqualTo(expectedHash + "  " +
                        SteamworksConfigurationExpectationCli.ReportFileName + "\n"));
            }
            finally
            {
                if (Directory.Exists(output))
                {
                    Directory.Delete(output, true);
                }
            }
        }

        [Test]
        public void Cli_RejectsRepositoryOutputAndInvalidSourceIdentity()
        {
            Assert.Throws<ArgumentException>(() =>
                SteamworksConfigurationExpectationCli.ValidateOutputDirectory(
                    Path.Combine(Directory.GetCurrentDirectory(), "Artifacts"),
                    Directory.GetCurrentDirectory()));
            Assert.Throws<ArgumentException>(() =>
                SteamworksConfigurationExpectationBuilder.Build("short", Tree));
            Assert.Throws<ArgumentException>(() =>
                SteamworksConfigurationExpectationBuilder.Build(Head, "NOT_A_TREE"));
        }

        [Test]
        public void Exporter_UsesTypedContractsWithoutCanonicalLiteralDuplicationOrSourceParsing()
        {
            var sourceRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Release/Steamworks/Editor");
            var source = string.Join(
                "\n",
                Directory.EnumerateFiles(sourceRoot, "*.cs")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));

            Assert.That(source, Does.Contain("WindowsDistributionTargetPolicy.SteamWindows"));
            Assert.That(source, Does.Contain("SteamAchievementMapping.Production"));
            Assert.That(source, Does.Contain("GameAchievementCatalog.Production"));
            Assert.That(source, Does.Not.Contain("campaign.complete"));
            Assert.That(source, Does.Not.Contain("VQ_CAMPAIGN_COMPLETE"));
            Assert.That(source, Does.Not.Contain("VectorQuake.exe"));
            Assert.That(source, Does.Not.Contain("-j2mPlatformProvider"));
            Assert.That(source, Does.Not.Contain("SteamAchievementMapping.cs"));
            Assert.That(source, Does.Not.Contain("WindowsDistributionTargetPolicy.cs"));
            Assert.That(source, Does.Not.Contain("Regex"));
        }

        private static int Count(string value, string token)
        {
            var count = 0;
            var offset = 0;
            while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }

            return count;
        }
    }
}
