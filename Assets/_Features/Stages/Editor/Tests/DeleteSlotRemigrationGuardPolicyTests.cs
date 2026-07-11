using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class DeleteSlotRemigrationGuardPolicyTests
    {
        private const string PolicyPath = "Docs/Architecture/Save-Architecture-V2-Phase4-Policy-Closeout.md";

        [Test]
        public void PolicyCloseout_DocumentsSelectedDeleteSlotRemigrationGuardPolicy()
        {
            var source = File.ReadAllText(PolicyPath);

            Assert.That(source, Does.Contain("DeleteSlot"));
            Assert.That(source, Does.Contain("records a deleted-slot legacy guard"));
            Assert.That(source, Does.Contain("`CampaignLegacyDeletedSlotGuardDocument`"));
            Assert.That(source, Does.Contain("`SlotNumber`"));
            Assert.That(source, Does.Contain("`ImportedSourceHash`"));
            Assert.That(source, Does.Contain("`DeletedAtUtc`"));
            Assert.That(source, Does.Contain("`Reason = \"DeleteSlot\"`"));
            Assert.That(source, Does.Contain("Same imported source hash"));
            Assert.That(source, Does.Contain("guarded slots are filtered"));
            Assert.That(source, Does.Contain("Changed imported source hash"));
            Assert.That(source, Does.Contain("`MigrationDeferred`"));
            Assert.That(source, Does.Contain("Empty `ImportedSourceHash`"));
            Assert.That(source, Does.Contain("source-agnostic guard"));
            Assert.That(source, Does.Contain("`LastPlayedSlotNumber`"));
            Assert.That(source, Does.Contain("`ClearAll`"));
            Assert.That(source, Does.Contain("`InitializeNewGame`"));
            Assert.That(source, Does.Contain("Old/null profile compatibility"));
        }

        [Test]
        public void FactoryReadinessGuard_ReportsProductionProviderReady()
        {
            var readiness = CampaignSaveProductionReadinessPolicy.EvaluateDeleteSlotProductionReadiness();

            Assert.That(readiness.IsReady, Is.True);
            Assert.That(readiness.Reason, Does.Contain("deleted-slot guards"));
            Assert.That(readiness.Reason, Does.Contain("profile-backed production provider"));
            Assert.That(readiness.Reason, Does.Contain("legacy import and rollback source"));
        }

        [Test]
        public void DeleteSlotV2Sources_DoNotDeleteRetainedLegacyPlayerPrefsCampaignPayload()
        {
            var serviceSource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs");
            var adapterSource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/SaveSlotStoreCompatibilityAdapter.cs");

            Assert.That(serviceSource, Does.Not.Contain("PlayerPrefs.DeleteKey"));
            Assert.That(adapterSource, Does.Not.Contain("PlayerPrefs.DeleteKey"));
        }
    }

}
