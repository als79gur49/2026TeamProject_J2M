using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SteamCloudFileInventoryPolicyTests
    {
        private const string PolicyPath = "Docs/Architecture/Steam-Cloud-File-Inventory-Policy.md";

        [Test]
        public void CurrentPersistenceInventory_IsJsonOnlyForCampaignState()
        {
            var policy = File.ReadAllText(PolicyPath);

            Assert.That(policy, Does.Contain("Campaign progression: `Saves/profile.json`"));
            Assert.That(policy, Does.Contain("Committed local active state: `Saves/local-launch-state.json`"));
            Assert.That(policy, Does.Contain("PlayerPrefs campaign progression: unsupported"));
            Assert.That(policy, Does.Contain("Audio/display/input/locale PlayerPrefs settings: supported local settings"));
        }

        [Test]
        public void FutureCloudDraft_TargetsExactProfileOnly()
        {
            var policy = File.ReadAllText(PolicyPath);

            Assert.That(policy, Does.Contain("Root:\n- WinAppDataLocalLow"));
            Assert.That(policy, Does.Contain("Subdirectory:\n- J2M/VectorQuake/Saves"));
            Assert.That(policy, Does.Contain("Pattern:\n- profile.json"));
            Assert.That(policy, Does.Contain("Recursive:\n- false"));
            Assert.That(policy, Does.Contain("Do not use `*.json`"));
            Assert.That(policy, Does.Contain("Steam Cloud remains disabled"));
        }

        [Test]
        public void RecoveryAndLocalStateFiles_AreNotCloudInputs()
        {
            var policy = File.ReadAllText(PolicyPath);

            Assert.That(policy, Does.Contain("`Saves/profile.json.bak`"));
            Assert.That(policy, Does.Contain("`Saves/profile.*.tmp`"));
            Assert.That(policy, Does.Contain("`Saves/profile.json.corrupt.*`"));
            Assert.That(policy, Does.Contain("`Saves/local-launch-state.json`"));
            Assert.That(policy, Does.Contain("`Saves/local-launch-state.json.bak`"));
            Assert.That(policy, Does.Contain("Profile backups and corrupt quarantine files are current local recovery"));
            Assert.That(policy, Does.Contain("Local launch-state backups remain excluded"));
        }

        [Test]
        public void PlayerPrefsResetBoundary_PreservesSettingsAndUnknownKeys()
        {
            var policy = File.ReadAllText(PolicyPath);

            Assert.That(policy, Does.Contain("| Audio settings | supported | no | preserve |"));
            Assert.That(policy, Does.Contain("| Display settings | supported | no | preserve |"));
            Assert.That(policy, Does.Contain("| Input settings | supported | no | preserve |"));
            Assert.That(policy, Does.Contain("| Unknown keys | owner decision required | no | do not delete |"));
            Assert.That(policy, Does.Contain("Broad deletion of `HKCU\\\\Software\\\\J2M\\\\VectorQuake` is forbidden."));
        }

        [Test]
        public void CompanyProductPath_RemainsCurrentCanonicalInput()
        {
            var projectSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            var policy = File.ReadAllText(PolicyPath);

            Assert.That(PlayerSettings.companyName, Is.EqualTo("J2M"));
            Assert.That(PlayerSettings.productName, Is.EqualTo("VectorQuake"));
            Assert.That(projectSettings, Does.Contain("companyName: J2M"));
            Assert.That(projectSettings, Does.Contain("productName: VectorQuake"));
            Assert.That(policy, Does.Contain("AppData/LocalLow/J2M/VectorQuake/Saves"));
        }

        [Test]
        public void HistoricalArtifacts_AreImmutableAndNotReleaseInputs()
        {
            var policy = File.ReadAllText(PolicyPath);

            Assert.That(policy, Does.Contain("Historical Store artifacts are immutable private evidence."));
            Assert.That(policy, Does.Contain("not current\nrelease-pipeline input"));
            Assert.That(policy, Does.Contain("No Store artifact\nis required"));
        }
    }
}
