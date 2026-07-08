using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadOnlyMetadataPolicyTests
    {
        private const string ProbePath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileMetadataProbe.cs";

        [Test]
        public void DiagnosticsOnlyMetadataProbe_ExposesNoSaveWriteApi()
        {
            var publicMethods = typeof(CampaignProfileMetadataProbe)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(publicMethods, Is.EquivalentTo(new[] { "Probe" }));
            AssertNoMethodNameContains(publicMethods, "Save");
            AssertNoMethodNameContains(publicMethods, "Write");
            AssertNoMethodNameContains(publicMethods, "Restore");
            AssertNoMethodNameContains(publicMethods, "Quarantine");
            AssertNoMethodNameContains(publicMethods, "Migrate");
        }

        [Test]
        public void DiagnosticsOnlyMetadataProbe_SourceHasNoRecoveryMarkerMigrationOrWriteCalls()
        {
            var source = File.ReadAllText(ProbePath);

            Assert.That(source, Does.Not.Contain("WriteAllText"));
            Assert.That(source, Does.Not.Contain("WriteAllTextAtomic"));
            Assert.That(source, Does.Not.Contain("Save("));
            Assert.That(source, Does.Not.Contain("TryRestoreBackup"));
            Assert.That(source, Does.Not.Contain("TryQuarantine"));
            Assert.That(source, Does.Not.Contain("CleanupTempFiles"));
            Assert.That(source, Does.Not.Contain("CampaignSaveMigrationCoordinator"));
            Assert.That(source, Does.Not.Contain("CampaignLegacyImportMarkerStore"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs.Set"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs.Delete"));
        }

        [Test]
        public void DiagnosticsOnlyMetadataProbe_MissingProfileDoesNotCreateDirectoryOrFiles()
        {
            using var harness = new ProfileHarness();

            var result = harness.Probe.Probe();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Missing));
            Assert.That(result.HasProfileMetadata, Is.False);
            Assert.That(Directory.Exists(harness.SaveRootPath), Is.False);
        }

        [Test]
        public void DiagnosticsOnlyMetadataProbe_CorruptProfileDoesNotRestoreBackupOrQuarantineCanonical()
        {
            using var harness = new ProfileHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "{\"SchemaVersion\":");
            File.WriteAllText(harness.BackupPath, JsonUtility.ToJson(CreateProfile(lastPlayedSlotNumber: 1)));
            var beforeFiles = harness.SnapshotFileNames();

            var result = harness.Probe.Probe();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Corrupt));
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo("{\"SchemaVersion\":"));
            Assert.That(File.Exists(harness.BackupPath), Is.True);
            Assert.That(harness.SnapshotFileNames(), Is.EquivalentTo(beforeFiles));
            Assert.That(Directory.GetFiles(harness.SaveRootPath, "*.corrupt.*"), Is.Empty);
        }

        [Test]
        public void DiagnosticsOnlyMetadataProbe_LoadedProfileExposesMetadataForReadinessOnly()
        {
            using var harness = new ProfileHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            var document = CreateProfile(lastPlayedSlotNumber: 3);
            document.SchemaVersion = 7;
            document.SavedAtUtc = "2026-07-08T01:02:03.0000000Z";
            document.LegacyImport = new CampaignLegacyImportDocument
            {
                ImportedSourceHash = "legacy-hash",
                ImportDisabled = true,
                ResetTombstoneUtc = "2026-07-08T03:04:05.0000000Z",
                DeletedSlotGuards = new[]
                {
                    new CampaignLegacyDeletedSlotGuardDocument
                    {
                        SlotNumber = 2,
                        ImportedSourceHash = "legacy-hash",
                        DeletedAtUtc = "2026-07-08T05:06:07.0000000Z",
                        Reason = "delete-slot",
                    },
                },
            };
            File.WriteAllText(harness.ProfilePath, JsonUtility.ToJson(document));

            var result = harness.Probe.Probe();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(result.HasProfileMetadata, Is.True);
            Assert.That(result.SchemaVersion, Is.EqualTo(7));
            Assert.That(result.SavedAtUtc, Is.EqualTo("2026-07-08T01:02:03.0000000Z"));
            Assert.That(result.LastPlayedSlotNumber, Is.EqualTo(3));
            Assert.That(result.ImportedSourceHash, Is.EqualTo("legacy-hash"));
            Assert.That(result.ImportDisabled, Is.True);
            Assert.That(result.HasResetTombstone, Is.True);
            Assert.That(result.DeletedSlotGuardCount, Is.EqualTo(1));
            Assert.That(result.ValidSlotDocumentCount, Is.EqualTo(1));
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

        private static void AssertNoMethodNameContains(string[] methodNames, string token)
        {
            Assert.That(
                methodNames.Any(name => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                token);
        }

        private sealed class ProfileHarness : IDisposable
        {
            private readonly string _testRootPath;

            public ProfileHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignProfileReadOnlyMetadataPolicyTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
                Probe = new CampaignProfileMetadataProbe(SaveRootPath);
            }

            public string SaveRootPath { get; }

            public CampaignProfileMetadataProbe Probe { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, CampaignProfileMetadataProbe.ProfileFileName);

            public string BackupPath => ProfilePath + ".bak";

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
