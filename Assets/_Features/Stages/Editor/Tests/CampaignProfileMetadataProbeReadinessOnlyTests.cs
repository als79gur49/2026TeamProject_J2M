using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileMetadataProbeReadinessOnlyTests
    {
        private const string ProbePath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileMetadataProbe.cs";

        [Test]
        public void Probe_PublicApi_RemainsSingleReadinessMethod()
        {
            var publicMethods = typeof(CampaignProfileMetadataProbe)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(publicMethods, Is.EquivalentTo(new[] { "Probe" }));
        }

        [Test]
        public void Probe_ResultMetadata_IsDiagnosticsOnlyShape()
        {
            var properties = typeof(CampaignProfileMetadataProbeResult)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(properties, Is.EquivalentTo(new[]
            {
                "HasProfileMetadata",
                "LastPlayedSlotNumber",
                "Message",
                "SavedAtUtc",
                "SchemaVersion",
                "SlotDocumentCount",
                "Status",
                "ValidSlotDocumentCount",
            }));
        }

        [Test]
        public void Probe_LoadedProfileReportsReadinessMetadataOnly()
        {
            using var harness = new ProfileHarness();
            harness.WriteProfile(CreateProfile(
                schemaVersion: CampaignProfileDocument.CurrentSchemaVersion,
                savedAtUtc: "2026-07-08T02:03:04.0000000Z",
                lastPlayedSlotNumber: 3,
                slots: new[]
                {
                    CreateSlot(1, "stage-1-1"),
                    CreateSlot(3, "stage-3-1"),
                }));

            var result = harness.Probe.Probe();

            Assert.That(result.Status, Is.EqualTo(CampaignProfileMetadataProbeStatus.Loaded));
            Assert.That(result.HasProfileMetadata, Is.True);
            Assert.That(result.SchemaVersion, Is.EqualTo(CampaignProfileDocument.CurrentSchemaVersion));
            Assert.That(result.SavedAtUtc, Is.EqualTo("2026-07-08T02:03:04.0000000Z"));
            Assert.That(result.LastPlayedSlotNumber, Is.EqualTo(3));
            Assert.That(result.SlotDocumentCount, Is.EqualTo(2));
            Assert.That(result.ValidSlotDocumentCount, Is.EqualTo(2));
        }

        [TestCase("SetPendingLaunchSlot")]
        [TestCase("TryGetPendingLaunchSlot")]
        [TestCase("TryGetActiveSlotNumber")]
        [TestCase("CampaignRunningSlotContext")]
        [TestCase("MainMenu")]
        [TestCase("QuickContinue")]
        [TestCase("DefaultFocus")]
        [TestCase("HUD")]
        [TestCase("DemoStageControl")]
        public void Probe_SourceDoesNotExposeRuntimeUxDecisionHooks(string forbiddenToken)
        {
            Assert.That(File.ReadAllText(ProbePath), Does.Not.Contain(forbiddenToken), forbiddenToken);
        }

        private static CampaignProfileDocument CreateProfile(
            int schemaVersion,
            string savedAtUtc,
            int lastPlayedSlotNumber,
            CampaignSlotDocument[] slots)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = schemaVersion,
                ProductVersion = "tests",
                SavedAtUtc = savedAtUtc,
                ProfileId = "profile-tests",
                LastPlayedSlotNumber = lastPlayedSlotNumber,
                Slots = slots ?? Array.Empty<CampaignSlotDocument>(),
            };
        }

        private static CampaignSlotDocument CreateSlot(int slotNumber, string stageId)
        {
            return new CampaignSlotDocument
            {
                GameMode = GameMode.Hardcore,
                SlotNumber = slotNumber,
                StageId = stageId,
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
                    "CampaignProfileMetadataProbeReadinessOnlyTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
                Probe = new CampaignProfileMetadataProbe(SaveRootPath);
            }

            public string SaveRootPath { get; }

            public CampaignProfileMetadataProbe Probe { get; }

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
