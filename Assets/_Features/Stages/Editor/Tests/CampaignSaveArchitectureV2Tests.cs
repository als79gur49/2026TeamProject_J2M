using System;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveArchitectureV2Tests
    {
        [Test]
        public void CampaignProfileDocument_CanBeDefaultConstructed()
        {
            var document = new CampaignProfileDocument();

            Assert.That(document.SchemaVersion, Is.EqualTo(0));
            Assert.That(document.LegacyImport, Is.Not.Null);
            Assert.That(document.Slots, Is.Not.Null);
            Assert.That(document.Slots, Is.Empty);
        }

        [Test]
        public void CampaignProfileDocument_RoundTripsThroughJsonUtility()
        {
            var document = new CampaignProfileDocument
            {
                SchemaVersion = 1,
                ProductVersion = "test-product",
                SavedAtUtc = "2026-07-06T09:00:00Z",
                ProfileId = "profile-a",
                LastPlayedSlotNumber = 2,
                LegacyImport = new CampaignLegacyImportDocument
                {
                    ImportedSourceHash = "legacy-hash",
                    ImportDisabled = true,
                    ResetTombstoneUtc = "2026-07-06T10:00:00Z",
                },
                Slots = new[]
                {
                    new CampaignSlotDocument
                    {
                        SlotNumber = 2,
                        StageId = "stage-1-1",
                        LevelGroupId = "level-1",
                        RemainingChances = 3,
                        CampaignCompleted = false,
                        LastPlayedAtUtc = "2026-07-06T11:00:00Z",
                    },
                },
            };

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignProfileDocument>(json);

            Assert.That(roundTripped.SchemaVersion, Is.EqualTo(1));
            Assert.That(roundTripped.ProductVersion, Is.EqualTo("test-product"));
            Assert.That(roundTripped.SavedAtUtc, Is.EqualTo("2026-07-06T09:00:00Z"));
            Assert.That(roundTripped.ProfileId, Is.EqualTo("profile-a"));
            Assert.That(roundTripped.LastPlayedSlotNumber, Is.EqualTo(2));
            Assert.That(roundTripped.LegacyImport.ImportedSourceHash, Is.EqualTo("legacy-hash"));
            Assert.That(roundTripped.LegacyImport.ImportDisabled, Is.True);
            Assert.That(roundTripped.LegacyImport.ResetTombstoneUtc, Is.EqualTo("2026-07-06T10:00:00Z"));
            Assert.That(roundTripped.Slots, Has.Length.EqualTo(1));
            Assert.That(roundTripped.Slots[0].SlotNumber, Is.EqualTo(2));
            Assert.That(roundTripped.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(roundTripped.Slots[0].LevelGroupId, Is.EqualTo("level-1"));
            Assert.That(roundTripped.Slots[0].RemainingChances, Is.EqualTo(3));
            Assert.That(roundTripped.Slots[0].CampaignCompleted, Is.False);
            Assert.That(roundTripped.Slots[0].LastPlayedAtUtc, Is.EqualTo("2026-07-06T11:00:00Z"));
        }

        [Test]
        public void CampaignLegacyImportDocument_RoundTripsThroughJsonUtility()
        {
            var document = new CampaignLegacyImportDocument
            {
                ImportedSourceHash = "source-hash",
                ImportDisabled = true,
                ResetTombstoneUtc = "2026-07-06T12:00:00Z",
            };

            var json = JsonUtility.ToJson(document);
            var roundTripped = JsonUtility.FromJson<CampaignLegacyImportDocument>(json);

            Assert.That(roundTripped.ImportedSourceHash, Is.EqualTo("source-hash"));
            Assert.That(roundTripped.ImportDisabled, Is.True);
            Assert.That(roundTripped.ResetTombstoneUtc, Is.EqualTo("2026-07-06T12:00:00Z"));
        }

        [Test]
        public void CampaignProfileLoadStatus_CoversExpectedValues()
        {
            Assert.That(
                Enum.GetValues(typeof(CampaignProfileLoadStatus)),
                Is.EquivalentTo(new[]
                {
                    CampaignProfileLoadStatus.Missing,
                    CampaignProfileLoadStatus.Loaded,
                    CampaignProfileLoadStatus.BackupRecovered,
                    CampaignProfileLoadStatus.CorruptQuarantined,
                    CampaignProfileLoadStatus.CorruptNoFallback,
                    CampaignProfileLoadStatus.Unauthorized,
                    CampaignProfileLoadStatus.IoFailed,
                    CampaignProfileLoadStatus.SchemaInvalid,
                }));
        }

        [Test]
        public void CampaignProfileRepositoryAndAtomicTextFileStore_AreCompileTargets()
        {
            ICampaignProfileRepository repository = new CompileOnlyCampaignProfileRepository();
            IAtomicTextFileStore textFileStore = new CompileOnlyAtomicTextFileStore();

            Assert.That(repository.Load().Status, Is.EqualTo(CampaignProfileLoadStatus.Missing));
            Assert.That(textFileStore.Exists("profile.json"), Is.False);
        }

        private sealed class CompileOnlyCampaignProfileRepository : ICampaignProfileRepository
        {
            public CampaignProfileLoadResult Load()
            {
                return new CampaignProfileLoadResult(
                    CampaignProfileLoadStatus.Missing,
                    null,
                    "compile-only");
            }

            public void Save(CampaignProfileDocument document)
            {
            }
        }

        private sealed class CompileOnlyAtomicTextFileStore : IAtomicTextFileStore
        {
            public bool Exists(string fileName)
            {
                return false;
            }

            public string ReadAllText(string fileName)
            {
                return string.Empty;
            }

            public void WriteAllTextAtomic(string fileName, string contents)
            {
            }
        }
    }
}
