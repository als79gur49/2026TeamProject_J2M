using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessReportTests
    {
        private const string ReportBuilderPath =
            "Assets/_Features/Stages/Editor/Validation/CampaignProfileReadinessReportBuilder.cs";
        private const string EditorAsmdefPath =
            "Assets/_Features/Stages/Editor/Game.Feature.Stages.Editor.asmdef";

        [Test]
        public void ReportBuilder_IsEditorOnlySource()
        {
            Assert.That(File.Exists(ReportBuilderPath), Is.True);
            Assert.That(ReportBuilderPath, Does.StartWith("Assets/_Features/Stages/Editor/"));
            Assert.That(File.ReadAllText(EditorAsmdefPath), Does.Contain("\"includePlatforms\""));
            Assert.That(File.ReadAllText(EditorAsmdefPath), Does.Contain("\"Editor\""));
        }

        [Test]
        public void ReportBuilder_UsesMetadataProbeAsOnlyProfileReadConsumer()
        {
            var source = File.ReadAllText(ReportBuilderPath);

            Assert.That(source, Does.Contain("new CampaignProfileMetadataProbe"));
            Assert.That(source, Does.Contain(".Probe()"));
            Assert.That(source, Does.Not.Contain("FileCampaignProfileRepository"));
            Assert.That(source, Does.Not.Contain("ICampaignProfileRepository"));
            Assert.That(source, Does.Not.Contain("CampaignProfileLoadResult"));
        }

        [Test]
        public void ReportBuilder_LoadedProfileMapsDiagnosticsInventory()
        {
            using var harness = new ProfileHarness();
            harness.WriteProfile(CreateProfile());

            var report = new CampaignProfileReadinessReportBuilder().Build(
                new CampaignProfileReadinessReportOptions(
                    harness.SaveRootPath,
                    new DateTime(2026, 7, 8, 1, 2, 3, DateTimeKind.Utc)));

            Assert.That(report.ProfileExistsForDiagnostics, Is.True);
            Assert.That(report.MetadataLoadStatus, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(report.SchemaVersion, Is.EqualTo(14));
            Assert.That(report.SavedAtUtc, Is.EqualTo("2026-07-08T01:02:03.0000000Z"));
            Assert.That(report.DiagnosticLastPlayedSlotNumber, Is.EqualTo(2));
            Assert.That(report.HasImportedSourceHash, Is.True);
            Assert.That(report.ImportedSourceHash, Is.EqualTo("legacy-source"));
            Assert.That(report.ImportDisabledMarkerMetadata, Is.True);
            Assert.That(report.HasResetTombstoneMarkerMetadata, Is.True);
            Assert.That(report.DeletedSlotGuardCount, Is.EqualTo(1));
            Assert.That(report.ProfileSlotDocumentCount, Is.EqualTo(3));
            Assert.That(report.ValidSlotDocumentCount, Is.EqualTo(2));
        }

        [Test]
        public void ReportWriter_DefaultPathIsUnderTestLogsSaveReadiness()
        {
            var outputPath = CampaignProfileReadinessReportWriter.ResolveOutputPath();
            var normalized = outputPath.Replace('\\', '/');

            Assert.That(normalized, Does.Contain("/TestLogs/SaveReadiness/"));
            Assert.That(Path.GetFileName(outputPath), Is.EqualTo("campaign-profile-readiness-report.md"));
        }

        private static CampaignProfileDocument CreateProfile()
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = 14,
                ProductVersion = "tests",
                SavedAtUtc = "2026-07-08T01:02:03.0000000Z",
                ProfileId = "profile-tests",
                LastPlayedSlotNumber = 2,
                LegacyImport = new CampaignLegacyImportDocument
                {
                    ImportedSourceHash = "legacy-source",
                    ImportDisabled = true,
                    ResetTombstoneUtc = "2026-07-08T02:03:04.0000000Z",
                    DeletedSlotGuards = new[]
                    {
                        new CampaignLegacyDeletedSlotGuardDocument
                        {
                            SlotNumber = 1,
                            ImportedSourceHash = "legacy-source",
                            DeletedAtUtc = "2026-07-08T03:04:05.0000000Z",
                            Reason = "delete-slot",
                        },
                    },
                },
                Slots = new[]
                {
                    CreateSlot(1),
                    CreateSlot(2),
                    CreateSlot(99),
                },
            };
        }

        private static CampaignSlotDocument CreateSlot(int slotNumber)
        {
            return new CampaignSlotDocument
            {
                SlotNumber = slotNumber,
                StageId = $"stage-{slotNumber}-1",
                LevelGroupId = $"level-{slotNumber}",
                RemainingChances = 3,
                LastPlayedAtUtc = "2026-07-08T00:00:00.0000000Z",
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
            };
        }

        private sealed class ProfileHarness : IDisposable
        {
            private readonly string _testRootPath;

            public ProfileHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignProfileReadinessReportTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
            }

            public string SaveRootPath { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, CampaignProfileMetadataProbe.ProfileFileName);

            public void WriteProfile(CampaignProfileDocument document)
            {
                Directory.CreateDirectory(SaveRootPath);
                File.WriteAllText(ProfilePath, JsonUtility.ToJson(document));
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
