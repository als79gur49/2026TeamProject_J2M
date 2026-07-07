using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class DeleteSlotRemigrationGuardPolicyTests
    {
        private const string PolicyPath = "Docs/Architecture/Save-Architecture-V2-Phase4-Policy-Closeout.md";

        [Test]
        public void PolicyCloseout_RequiresSlotLevelRemigrationGuardBeforeProductionDeleteSlotSwitch()
        {
            var source = File.ReadAllText(PolicyPath);

            Assert.That(source, Does.Contain("DeleteSlot"));
            Assert.That(source, Does.Contain("slot-level legacy deletion marker or tombstone"));
            Assert.That(source, Does.Contain("retained legacy PlayerPrefs may still contain the deleted slot"));
            Assert.That(source, Does.Contain("resurrect"));
            Assert.That(source, Does.Contain("deleted slots"));
            Assert.That(source, Does.Contain("profile-document deletion only"));
        }

        [Test]
        public void FactoryReadinessGuard_ReportsDeleteSlotProductionIntegrationNotReady()
        {
            var readiness = CampaignSaveProductionReadinessPolicy.EvaluateDeleteSlotProductionReadiness();

            Assert.That(readiness.IsReady, Is.False);
            Assert.That(readiness.Reason, Does.Contain("slot-level"));
            Assert.That(readiness.Reason, Does.Contain("tombstone"));
            Assert.That(readiness.Reason, Does.Contain("remigration"));
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
