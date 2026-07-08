using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessReportSideEffectTests
    {
        private const string ReportBuilderPath =
            "Assets/_Features/Stages/Editor/Validation/CampaignProfileReadinessReportBuilder.cs";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportDisabledKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey);
            PlayerPrefs.Save();
        }

        [TestCase("WriteAllText")]
        [TestCase("File.Move")]
        [TestCase("TryRestoreBackup")]
        [TestCase("TryQuarantine")]
        [TestCase("CampaignSaveMigrationCoordinator")]
        [TestCase("CampaignSaveService")]
        [TestCase("CampaignLegacyImportMarkerStore")]
        [TestCase("PlayerPrefs.Set")]
        [TestCase("PlayerPrefs.Delete")]
        public void ReportBuilder_SourceDoesNotContainWriteRestoreQuarantineMarkerOrMigrationPath(string forbiddenToken)
        {
            Assert.That(File.ReadAllText(ReportBuilderPath), Does.Not.Contain(forbiddenToken), forbiddenToken);
        }

        [Test]
        public void ReportBuilder_MissingProfile_DoesNotCreateFileOrDirectory()
        {
            using var harness = new ProfileHarness();

            var report = harness.BuildReport();

            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Missing));
            Assert.That(report.ProfileExistsForDiagnostics, Is.False);
            Assert.That(Directory.Exists(harness.SaveRootPath), Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
        }

        [Test]
        public void ReportBuilder_CorruptProfile_DoesNotQuarantineOrRename()
        {
            using var harness = new ProfileHarness();
            harness.WriteRawProfile("{\"SchemaVersion\":");
            var beforeFiles = harness.SnapshotFileNames();

            var report = harness.BuildReport();

            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Corrupt));
            Assert.That(harness.ReadRawProfile(), Is.EqualTo("{\"SchemaVersion\":"));
            Assert.That(harness.SnapshotFileNames(), Is.EquivalentTo(beforeFiles));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "*.corrupt.*"), Is.Empty);
        }

        [Test]
        public void ReportBuilder_ProfileWithBackup_DoesNotRestoreBackup()
        {
            using var harness = new ProfileHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.BackupPath, JsonUtility.ToJson(CreateProfile(1)));
            var beforeFiles = harness.SnapshotFileNames();

            var report = harness.BuildReport();

            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Missing));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(File.Exists(harness.BackupPath), Is.True);
            Assert.That(harness.SnapshotFileNames(), Is.EquivalentTo(beforeFiles));
        }

        [Test]
        public void ReportBuilder_DoesNotWritePlayerPrefsMarkers()
        {
            using var harness = new ProfileHarness();
            harness.WriteProfile(CreateProfile(2));
            PlayerPrefs.SetInt(CampaignLegacyImportMarkerStore.ImportDisabledKey, 1);
            PlayerPrefs.SetString(CampaignLegacyImportMarkerStore.ImportedSourceHashKey, "marker-source");
            PlayerPrefs.SetString(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey, "2026-07-08T01:02:03.0000000Z");
            PlayerPrefs.SetString(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey, "marker-guards");
            PlayerPrefs.Save();

            var report = harness.BuildReport();

            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(PlayerPrefs.GetInt(CampaignLegacyImportMarkerStore.ImportDisabledKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetString(CampaignLegacyImportMarkerStore.ImportedSourceHashKey), Is.EqualTo("marker-source"));
            Assert.That(PlayerPrefs.GetString(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey), Is.EqualTo("2026-07-08T01:02:03.0000000Z"));
            Assert.That(PlayerPrefs.GetString(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey), Is.EqualTo("marker-guards"));
        }

        [Test]
        public void ReportBuilder_DoesNotRewriteProfileSchema()
        {
            using var harness = new ProfileHarness();
            harness.WriteProfile(CreateProfile(3));
            var before = harness.ReadRawProfile();

            var report = harness.BuildReport();

            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(harness.ReadRawProfile(), Is.EqualTo(before));
        }

        [Test]
        public void ReportWriter_OnlyWritesUnderTestLogsSaveReadiness()
        {
            using var harness = new ProfileHarness();
            var report = harness.BuildReport();
            var outputDirectory = Path.Combine(
                CampaignProfileReadinessReportOptions.DefaultOutputDirectory,
                nameof(CampaignProfileReadinessReportSideEffectTests),
                Guid.NewGuid().ToString("N"));

            var outputPath = CampaignProfileReadinessReportWriter.Write(report, outputDirectory);

            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(outputPath.Replace('\\', '/'), Does.Contain("/TestLogs/SaveReadiness/"));
            Directory.Delete(Path.GetDirectoryName(outputPath), recursive: true);
        }

        [Test]
        public void ReportWriter_RejectsOutputOutsideTestLogsSaveReadiness()
        {
            using var harness = new ProfileHarness();
            var report = harness.BuildReport();

            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportWriter.Write(
                    report,
                    Path.Combine("Temp", "SaveReadiness")));
        }

        private static CampaignProfileDocument CreateProfile(int lastPlayedSlotNumber)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = 1,
                ProductVersion = "tests",
                SavedAtUtc = "2026-07-08T00:00:00.0000000Z",
                ProfileId = "profile-tests",
                LastPlayedSlotNumber = lastPlayedSlotNumber,
                LegacyImport = new CampaignLegacyImportDocument(),
                Slots = new[]
                {
                    new CampaignSlotDocument
                    {
                        SlotNumber = 1,
                        StageId = "stage-1-1",
                        LevelGroupId = "level-1",
                        RemainingChances = 3,
                        LastPlayedAtUtc = "2026-07-08T00:00:00.0000000Z",
                        StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
                    },
                },
            };
        }

        private sealed class ProfileHarness : IDisposable
        {
            private readonly string _testRootPath;

            public ProfileHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignProfileReadinessReportSideEffectTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
            }

            public string SaveRootPath { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, CampaignProfileMetadataProbe.ProfileFileName);

            public string BackupPath => ProfilePath + ".bak";

            public CampaignProfileReadinessReport BuildReport()
            {
                return new CampaignProfileReadinessReportBuilder()
                    .Build(new CampaignProfileReadinessReportOptions(SaveRootPath));
            }

            public void WriteProfile(CampaignProfileDocument document)
            {
                WriteRawProfile(JsonUtility.ToJson(document));
            }

            public void WriteRawProfile(string rawProfile)
            {
                Directory.CreateDirectory(SaveRootPath);
                File.WriteAllText(ProfilePath, rawProfile);
            }

            public string ReadRawProfile()
            {
                return File.ReadAllText(ProfilePath);
            }

            public string[] SnapshotFileNames()
            {
                return Directory.Exists(SaveRootPath)
                    ? Directory.GetFiles(SaveRootPath).Select(Path.GetFileName).OrderBy(name => name).ToArray()
                    : Array.Empty<string>();
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_testRootPath))
                    {
                        Directory.Delete(_testRootPath, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
