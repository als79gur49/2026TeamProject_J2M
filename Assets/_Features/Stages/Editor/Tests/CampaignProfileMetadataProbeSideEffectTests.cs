using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileMetadataProbeSideEffectTests
    {
        [Test]
        public void Probe_MissingProfile_DoesNotCreateFileOrDirectory()
        {
            using var harness = new ProfileHarness();

            var result = harness.Probe.Probe();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Missing));
            Assert.That(Directory.Exists(harness.SaveRootPath), Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
        }

        [Test]
        public void Probe_CorruptProfile_DoesNotQuarantineOrRename()
        {
            using var harness = new ProfileHarness();
            harness.WriteRawProfile("{\"SchemaVersion\":");
            var beforeFiles = harness.SnapshotFileNames();

            var result = harness.Probe.Probe();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Corrupt));
            Assert.That(harness.ReadRawProfile(), Is.EqualTo("{\"SchemaVersion\":"));
            Assert.That(harness.SnapshotFileNames(), Is.EquivalentTo(beforeFiles));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "*.corrupt.*"), Is.Empty);
        }

        [Test]
        public void Probe_ProfileWithBackup_DoesNotRestoreBackup()
        {
            using var harness = new ProfileHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.BackupPath, JsonUtility.ToJson(CreateProfile(1)));
            var beforeFiles = harness.SnapshotFileNames();

            var result = harness.Probe.Probe();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Missing));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(File.Exists(harness.BackupPath), Is.True);
            Assert.That(harness.SnapshotFileNames(), Is.EquivalentTo(beforeFiles));
        }

        [Test]
        public void Probe_DoesNotCreateTempFilesOrDeleteExistingTempFiles()
        {
            using var harness = new ProfileHarness();
            harness.WriteProfile(CreateProfile(1));
            File.WriteAllText(harness.TempPath, "pre-existing-temp");
            var beforeFiles = harness.SnapshotFileNames();

            var result = harness.Probe.Probe();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(File.ReadAllText(harness.TempPath), Is.EqualTo("pre-existing-temp"));
            Assert.That(harness.SnapshotFileNames(), Is.EquivalentTo(beforeFiles));
        }

        private static CampaignProfileDocument CreateProfile(int lastPlayedSlotNumber)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = "tests",
                SavedAtUtc = "2026-07-08T00:00:00.0000000Z",
                ProfileId = "profile-tests",
                LastPlayedSlotNumber = lastPlayedSlotNumber,
                Slots = new[]
                {
                    new CampaignSlotDocument
                    {
                GameMode = GameMode.Hardcore,
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
                    "CampaignProfileMetadataProbeSideEffectTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
                Probe = new CampaignProfileMetadataProbe(SaveRootPath);
            }

            public string SaveRootPath { get; }

            public CampaignProfileMetadataProbe Probe { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, CampaignProfileMetadataProbe.ProfileFileName);

            public string BackupPath => ProfilePath + ".bak";

            public string TempPath => ProfilePath + ".tmp";

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
