using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SteamCloudFileInventoryPolicyTests
    {
        private const string PolicyDocPath = "Docs/Architecture/Steam-Cloud-File-Inventory-Policy.md";
        private const string Root = "WinAppDataLocalLow";
        private const string Company = "J2M";
        private const string Product = "VectorQuake";
        private const string Subdirectory = "J2M/VectorQuake/Saves";
        private const string Pattern = "profile.json";
        private const bool Recursive = false;

        private static readonly string[] FutureCloudIncludes =
        {
            "Saves/profile.json",
        };

        private static readonly string[] CloudExclusions =
        {
            "Saves/profile.json.bak",
            "Saves/profile.123.tmp",
            "Saves/profile.json.corrupt.202607090000000000000",
            "Saves/profile.json.rejected.reset-1",
            "Saves/profile.json.bak.rejected.reset-1",
            "Saves/profile.reset.pending.json",
            "Saves/profile.reset.pending.json.bak",
            "campaign-save-seed.json",
            "Settings/local-settings.json",
            "Saves/local-launch-state.json",
            "Saves/editor-direct-play.json",
            "Saves/direct-play-temp.json",
            "settings.audio.master.volume",
            "settings.display.width",
            "Game.Feature.Input.KeyboardMovementScheme",
            "Game.Feature.Stages.ActiveStageClearSaveSlot",
            "Game.Feature.Stages.DirectPlay.TempSaveSlots",
            "Game.Feature.Stages.DirectPlay.TempActiveSaveSlot",
            "TestLogs/",
            "TestLogs/SaveReadiness/CampaignProfileReadiness.md",
            "Logs/player.log",
            "TestResults/wsl-unity-full-editmode.xml",
            "ProfilerCaptures/capture.data",
            "player.log",
            "profile.leftover.tmp",
        };

        private static readonly string[] SteamPipeExclusions =
        {
            "steam_appid.txt",
            "TestLogs/",
            "TestResults/",
            "Logs/",
            "ProfilerCaptures/",
            "CampaignProfileReadiness.md",
            "TestLogs/SaveReadiness/20260709/abc123/CampaignProfileReadiness.md",
            "campaign-save-seed.json",
            "Settings/local-settings.json",
            "Saves/local-launch-state.json",
            "Saves/editor-direct-play.json",
            "Saves/direct-play-temp.json",
            "Game.pdb",
            "Game.mdb",
            "player.log",
            "profile.leftover.tmp",
            "Library/",
            "UserSettings/",
            "obj/",
            "Tools/SteamPipe/app/cache",
            "Tools/SteamPipe/app/output",
            "Tools/SteamPipe/app/login",
        };

        [Test]
        public void CurrentProductionStorage_StillUsesPlayerPrefsAndDefersAutoCloud()
        {
            var doc = ReadPolicyDoc();

            Assert.That(SaveSlotStore.DefaultPlayerPrefsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(SaveSlotPrefsKeys.SaveSlotsKey, Is.EqualTo("Game.Feature.Stages.StageClearSaveSlots"));
            Assert.That(SaveSlotPrefsKeys.ActiveSaveSlotKey, Is.EqualTo("Game.Feature.Stages.ActiveStageClearSaveSlot"));
            Assert.That(CampaignSaveMigrationOptions.Default.EnableProfileWrite, Is.False);
            Assert.That(doc, Does.Contain("Campaign progression save truth: `Saves/profile.json`"));
            Assert.That(doc, Does.Contain("Auto-Cloud application is deferred."));
            Assert.That(doc, Does.Contain("Retained legacy import / rollback source key"));
        }

        [Test]
        public void FutureAutoCloudRuleDraft_IsExactProfileJsonOnly()
        {
            var doc = ReadPolicyDoc();

            Assert.That(Root, Is.EqualTo("WinAppDataLocalLow"));
            Assert.That(Subdirectory, Is.EqualTo("J2M/VectorQuake/Saves"));
            Assert.That(Pattern, Is.EqualTo("profile.json"));
            Assert.That(Pattern, Is.Not.EqualTo("*.json"));
            Assert.That(Recursive, Is.False);
            Assert.That(FutureCloudIncludes, Is.EqualTo(new[] { "Saves/profile.json" }));
            Assert.That(IsFutureCloudIncluded("Saves/profile.json"), Is.True);
            Assert.That(IsFutureCloudIncluded("Saves/slot-1.json"), Is.False);
            Assert.That(IsFutureCloudIncluded("Saves/profile.json.bak"), Is.False);

            Assert.That(doc, Does.Contain("Root:\n- WinAppDataLocalLow"));
            Assert.That(doc, Does.Contain("Subdirectory:\n- J2M/VectorQuake/Saves"));
            Assert.That(doc, Does.Contain("Pattern:\n- profile.json"));
            Assert.That(doc, Does.Contain("Recursive:\n- false"));
            Assert.That(doc, Does.Contain("Include:\n- profile.json"));
            Assert.That(doc, Does.Contain("Do not use a `*.json` include pattern."));
            Assert.That(doc, Does.Contain("after a separate Steam Cloud enable decision"));
        }

        [TestCaseSource(nameof(CloudExclusions))]
        public void SteamCloudPolicy_ExcludesNonCanonicalSaveAndArtifactInventory(string candidate)
        {
            Assert.That(IsFutureCloudIncluded(candidate), Is.False, candidate);
            Assert.That(
                ReadPolicyDoc(),
                Does.Contain(PolicyDocTokenForCandidate(candidate)),
                candidate);
        }

        [Test]
        public void SteamCloudPolicy_DocumentsBackupTempCorruptSettingsAndDirectPlayExclusions()
        {
            var doc = ReadPolicyDoc();

            Assert.That(doc, Does.Contain("`Saves/profile.json.bak`"));
            Assert.That(doc, Does.Contain("`Saves/profile.*.tmp`"));
            Assert.That(doc, Does.Contain("`Saves/profile.json.corrupt.*`"));
            Assert.That(doc, Does.Contain("`Saves/profile.json.rejected.*`"));
            Assert.That(doc, Does.Contain("`Saves/profile.json.bak.rejected.*`"));
            Assert.That(doc, Does.Contain("`Saves/profile.reset.pending.json`"));
            Assert.That(doc, Does.Contain("`Saves/profile.reset.pending.json.bak`"));
            Assert.That(doc, Does.Contain("`Settings/local-settings.json`"));
            Assert.That(doc, Does.Contain("`Saves/local-launch-state.json`"));
            Assert.That(doc, Does.Contain("`Saves/editor-direct-play.json`"));
            Assert.That(doc, Does.Contain("`Saves/direct-play-temp.json`"));
            Assert.That(doc, Does.Contain("`settings.audio.*`"));
            Assert.That(doc, Does.Contain("`settings.display.*`"));
            Assert.That(doc, Does.Contain("`Game.Feature.Input.*`"));
            Assert.That(doc, Does.Contain("`Game.Feature.Stages.ActiveStageClearSaveSlot`"));
            Assert.That(doc, Does.Contain("`Game.Feature.Stages.DirectPlay.TempSaveSlots`"));
            Assert.That(doc, Does.Contain("`Game.Feature.Stages.DirectPlay.TempActiveSaveSlot`"));
            Assert.That(doc, Does.Contain("`campaign-save-seed.json`"));
            Assert.That(doc, Does.Contain("`TestLogs/SaveReadiness/`"));
            Assert.That(doc, Does.Contain("Backup clouding requires a separate tested backup-cloud policy"));
            Assert.That(doc, Does.Contain("Direct-play temp save and temp active-slot keys are never Cloud targets."));
            Assert.That(doc, Does.Contain("Readiness reports are CI artifacts only"));
        }

        [Test]
        public void SteamCloudPolicy_ExcludesLocalSettingsLaunchAndEditorTargets()
        {
            Assert.That(IsFutureCloudIncluded("Saves/profile.json"), Is.True);
            Assert.That(IsFutureCloudIncluded("Settings/local-settings.json"), Is.False);
            Assert.That(IsFutureCloudIncluded("Saves/local-launch-state.json"), Is.False);
            Assert.That(IsFutureCloudIncluded("Saves/editor-direct-play.json"), Is.False);
            Assert.That(IsFutureCloudIncluded("Saves/direct-play-temp.json"), Is.False);
            Assert.That(IsFutureCloudIncluded("settings.audio.master.volume"), Is.False);
            Assert.That(IsFutureCloudIncluded("settings.display.width"), Is.False);
            Assert.That(IsFutureCloudIncluded("Game.Feature.Input.KeyboardMovementScheme"), Is.False);
            Assert.That(IsFutureCloudIncluded("Game.Feature.Stages.ActiveStageClearSaveSlot"), Is.False);
            Assert.That(IsFutureCloudIncluded("Game.Feature.Stages.DirectPlay.TempSaveSlots"), Is.False);
        }

        [TestCaseSource(nameof(SteamPipeExclusions))]
        public void SteamPipeContentPolicy_ExcludesReleaseStagingContamination(string candidate)
        {
            Assert.That(IsSteamPipeContentIncluded(candidate), Is.False, candidate);
            Assert.That(
                ReadPolicyDoc(),
                Does.Contain(PolicyDocTokenForCandidate(candidate)),
                candidate);
        }

        [Test]
        public void SteamPipeContentPolicy_DocumentsSanitizerAndDebugSymbolBoundary()
        {
            var doc = ReadPolicyDoc();

            Assert.That(doc, Does.Contain("Do not use the repository root directly as the SteamPipe staging source."));
            Assert.That(doc, Does.Contain("Stage sanitized build output from an explicit release staging directory."));
            Assert.That(doc, Does.Contain("Debug symbols are excluded until a separate shipping-symbol decision"));
            Assert.That(doc, Does.Contain("`steam_appid.txt`"));
            Assert.That(doc, Does.Contain("`Settings/local-settings.json`"));
            Assert.That(doc, Does.Contain("`Saves/local-launch-state.json`"));
            Assert.That(doc, Does.Contain("`Saves/editor-direct-play.json`"));
            Assert.That(doc, Does.Contain("`Saves/direct-play-temp.json`"));
            Assert.That(doc, Does.Contain("`*.pdb`"));
            Assert.That(doc, Does.Contain("`*.mdb`"));
            Assert.That(doc, Does.Contain("`*.log`"));
            Assert.That(doc, Does.Contain("`*.tmp`"));
            Assert.That(doc, Does.Contain("`Tools/SteamPipe/**/cache`"));
            Assert.That(doc, Does.Contain("`Tools/SteamPipe/**/output`"));
            Assert.That(doc, Does.Contain("`Tools/SteamPipe/**/login`"));
        }

        [Test]
        public void CompanyProductPath_MatchesFutureAutoCloudDraft()
        {
            var projectSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            var doc = ReadPolicyDoc();

            Assert.That(PlayerSettings.companyName, Is.EqualTo(Company));
            Assert.That(PlayerSettings.productName, Is.EqualTo(Product));
            Assert.That(projectSettings, Does.Contain("companyName: J2M"));
            Assert.That(projectSettings, Does.Contain("productName: VectorQuake"));
            Assert.That(Subdirectory, Is.EqualTo($"{Company}/{Product}/Saves"));
            Assert.That(doc, Does.Contain("Unity Company: `J2M`."));
            Assert.That(doc, Does.Contain("Unity Product: `VectorQuake`."));
            Assert.That(doc, Does.Contain("AppData/LocalLow/J2M/VectorQuake"));
            Assert.That(doc, Does.Contain("Any pre-release Company/Product rename requires a Steam Auto-Cloud rule review"));
        }

        [Test]
        public void ProductionSources_DoNotIntroduceSteamRuntimeApiOrRemoteStorageIntegration()
        {
            var scannedFiles = EnumerateProductionSourceAndPackageFiles().ToArray();

            Assert.That(scannedFiles, Is.Not.Empty);
            AssertForbiddenTokensAbsent(
                scannedFiles,
                "Steamworks.NET",
                "Steamworks",
                "SteamAPI",
                "ISteamRemoteStorage",
                "SteamRemoteStorage",
                "RemoteStorage");
        }

        [Test]
        public void PolicyDocument_StatesSteamApiIntegrationIsOutOfScope()
        {
            var doc = ReadPolicyDoc();

            Assert.That(doc, Does.Contain("This policy freezes the Steam Cloud file inventory"));
            Assert.That(doc, Does.Contain("does not enable Steam Cloud"));
            Assert.That(doc, Does.Contain("does not enable Steam Cloud, add Steamworks.NET, call Steam APIs"));
            Assert.That(doc, Does.Contain("Steamworks.NET runtime integration."));
            Assert.That(doc, Does.Contain("`SteamAPI` calls."));
            Assert.That(doc, Does.Contain("`ISteamRemoteStorage`."));
            Assert.That(doc, Does.Contain("`SteamRemoteStorage`."));
            Assert.That(doc, Does.Contain("RemoteStorage save paths."));
            Assert.That(doc, Does.Contain("Direct Steam Cloud API integration."));
        }

        private static string ReadPolicyDoc()
        {
            return File.ReadAllText(PolicyDocPath);
        }

        private static bool IsFutureCloudIncluded(string candidate)
        {
            return string.Equals(Normalize(candidate), "Saves/profile.json", StringComparison.Ordinal);
        }

        private static bool IsSteamPipeContentIncluded(string candidate)
        {
            return !MatchesSteamPipeExclusion(candidate);
        }

        private static bool MatchesSteamPipeExclusion(string candidate)
        {
            var value = Normalize(candidate);
            return value == "steam_appid.txt" ||
                   value == "CampaignProfileReadiness.md" ||
                   value == "campaign-save-seed.json" ||
                   value == "Settings/local-settings.json" ||
                   value == "Saves/local-launch-state.json" ||
                   value == "Saves/editor-direct-play.json" ||
                   value == "Saves/direct-play-temp.json" ||
                   value.EndsWith(".pdb", StringComparison.Ordinal) ||
                   value.EndsWith(".mdb", StringComparison.Ordinal) ||
                   value.EndsWith(".log", StringComparison.Ordinal) ||
                   value.EndsWith(".tmp", StringComparison.Ordinal) ||
                   IsUnderDirectory(value, "TestLogs") ||
                   IsUnderDirectory(value, "TestResults") ||
                   IsUnderDirectory(value, "Logs") ||
                   IsUnderDirectory(value, "ProfilerCaptures") ||
                   IsUnderDirectory(value, "Library") ||
                   IsUnderDirectory(value, "UserSettings") ||
                   IsUnderDirectory(value, "obj") ||
                   IsUnderSteamPipeGeneratedDirectory(value, "cache") ||
                   IsUnderSteamPipeGeneratedDirectory(value, "output") ||
                   IsUnderSteamPipeGeneratedDirectory(value, "login");
        }

        private static string Normalize(string candidate)
        {
            return (candidate ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private static bool IsUnderDirectory(string value, string directory)
        {
            return value == $"{directory}/" ||
                   value.StartsWith($"{directory}/", StringComparison.Ordinal);
        }

        private static bool IsUnderSteamPipeGeneratedDirectory(string value, string leaf)
        {
            var marker = $"/{leaf}";
            return value.StartsWith("Tools/SteamPipe/", StringComparison.Ordinal) &&
                   (value.EndsWith(marker, StringComparison.Ordinal) ||
                    value.Contains($"{marker}/", StringComparison.Ordinal));
        }

        private static string PolicyDocTokenForCandidate(string candidate)
        {
            var value = Normalize(candidate);
            if (value.StartsWith("Saves/profile.", StringComparison.Ordinal) &&
                value.EndsWith(".tmp", StringComparison.Ordinal))
            {
                return "`Saves/profile.*.tmp`";
            }

            if (value.StartsWith("Saves/profile.json.corrupt.", StringComparison.Ordinal))
            {
                return "`Saves/profile.json.corrupt.*`";
            }

            if (value.StartsWith("Saves/profile.json.bak.rejected.", StringComparison.Ordinal))
            {
                return "`Saves/profile.json.bak.rejected.*`";
            }

            if (value.StartsWith("Saves/profile.json.rejected.", StringComparison.Ordinal))
            {
                return "`Saves/profile.json.rejected.*`";
            }

            if (value == "Settings/local-settings.json" ||
                value == "Saves/local-launch-state.json" ||
                value == "Saves/editor-direct-play.json" ||
                value == "Saves/direct-play-temp.json")
            {
                return $"`{value}`";
            }

            if (value.StartsWith("settings.audio.", StringComparison.Ordinal))
            {
                return "`settings.audio.*`";
            }

            if (value.StartsWith("settings.display.", StringComparison.Ordinal))
            {
                return "`settings.display.*`";
            }

            if (value.StartsWith("Game.Feature.Input.", StringComparison.Ordinal))
            {
                return "`Game.Feature.Input.*`";
            }

            if (value.StartsWith("TestLogs/SaveReadiness/", StringComparison.Ordinal))
            {
                return "`TestLogs/SaveReadiness/";
            }

            if (value.StartsWith("TestResults/", StringComparison.Ordinal))
            {
                return "`TestResults/`";
            }

            if (value.StartsWith("ProfilerCaptures/", StringComparison.Ordinal))
            {
                return "`ProfilerCaptures/`";
            }

            if (value.EndsWith(".pdb", StringComparison.Ordinal))
            {
                return "`*.pdb`";
            }

            if (value.EndsWith(".mdb", StringComparison.Ordinal))
            {
                return "`*.mdb`";
            }

            if (value.EndsWith(".log", StringComparison.Ordinal))
            {
                return "`*.log`";
            }

            if (value.EndsWith(".tmp", StringComparison.Ordinal))
            {
                return "`*.tmp`";
            }

            if (IsUnderSteamPipeGeneratedDirectory(value, "cache"))
            {
                return "`Tools/SteamPipe/**/cache`";
            }

            if (IsUnderSteamPipeGeneratedDirectory(value, "output"))
            {
                return "`Tools/SteamPipe/**/output`";
            }

            if (IsUnderSteamPipeGeneratedDirectory(value, "login"))
            {
                return "`Tools/SteamPipe/**/login`";
            }

            return $"`{value}`";
        }

        private static IEnumerable<string> EnumerateProductionSourceAndPackageFiles()
        {
            foreach (var path in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
            {
                var normalized = Normalize(path);
                if (normalized.Contains("/Tests/", StringComparison.Ordinal) ||
                    normalized.Contains("_Tests/", StringComparison.Ordinal) ||
                    normalized.StartsWith("Assets/Synty/", StringComparison.Ordinal) ||
                    normalized.StartsWith("Assets/Polygon Arsenal/", StringComparison.Ordinal) ||
                    normalized.StartsWith("Assets/PolygonParticleFX/", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return normalized;
            }

            foreach (var packageFile in new[] { "Packages/manifest.json", "Packages/packages-lock.json" })
            {
                if (File.Exists(packageFile))
                {
                    yield return packageFile;
                }
            }
        }

        private static void AssertForbiddenTokensAbsent(IEnumerable<string> paths, params string[] forbiddenTokens)
        {
            foreach (var path in paths)
            {
                var source = File.ReadAllText(path);
                for (var i = 0; i < forbiddenTokens.Length; i++)
                {
                    Assert.That(source, Does.Not.Contain(forbiddenTokens[i]), $"{path}: {forbiddenTokens[i]}");
                }
            }
        }
    }
}
