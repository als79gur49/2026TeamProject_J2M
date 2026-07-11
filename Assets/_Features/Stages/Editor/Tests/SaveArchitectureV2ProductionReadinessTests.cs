using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SaveArchitectureV2ProductionReadinessTests
    {
        private const string PolicyDocPath = "Docs/Architecture/Steam-Cloud-File-Inventory-Policy.md";

        private static readonly string[] ProductionCompositionFiles =
        {
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
            "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs",
            "Assets/_Features/DemoStageControl/Runtime/DemoStageControlBridges.cs",
        };

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.Save();
        }

        [TestCase("CampaignProfileReadinessReport")]
        [TestCase("CampaignProfileReadinessReportWriter")]
        [TestCase("CampaignProfileMetadataProbe")]
        [TestCase("SaveSlotStoreCompatibilityAdapter")]
        [TestCase("CampaignSaveMigrationCoordinator")]
        [TestCase("FileCampaignProfileRepository")]
        [TestCase("profile.json")]
        public void ProductionComposition_DoesNotReferenceProfileInternalsDirectly(string forbiddenToken)
        {
            foreach (var path in EnumerateProductionReadinessSourceFiles())
            {
                Assert.That(File.ReadAllText(path), Does.Not.Contain(forbiddenToken), path);
            }
        }

        [TestCase("CampaignSaveServiceFactory")]
        [TestCase("FileCampaignProfileRepository")]
        [TestCase("ICampaignProfileRepository")]
        [TestCase("CampaignProfileDocument")]
        [TestCase("profile.json")]
        [TestCase("LastPlayedSlotNumber")]
        public void MainMenuProductionPath_DoesNotReferenceV2MetadataTruthTokens(string forbiddenToken)
        {
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs"),
                Does.Not.Contain(forbiddenToken),
                forbiddenToken);
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Not.Contain(forbiddenToken),
                forbiddenToken);
        }

        [Test]
        public void MainMenuProductionPath_UsesProfileBackedProviderAsUxSource()
        {
            var controller = File.ReadAllText("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            var installer = File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(controller, Does.Contain("_saveSlotStore.LoadAllWithReport()"));
            Assert.That(installer, Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(installer, Does.Not.Contain("ProfileJsonExplicit"));
            Assert.That(installer, Does.Not.Contain("EnableProfileWrite"));
            Assert.That(installer, Does.Contain("new ActiveSlotProviderPendingLaunchAdapter(activeSlotProvider)"));
        }

        [Test]
        public void SaveSlotStorePublicConstructor_DefaultStillUsesPlayerPrefsBackend()
        {
            var store = new SaveSlotStore();

            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });

            Assert.That(store.PlayerPrefsKey, Is.EqualTo(SaveSlotStore.DefaultPlayerPrefsKey));
            Assert.That(SaveSlotStore.DefaultPlayerPrefsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
        }

        [Test]
        public void CampaignSaveMigrationOptions_DefaultEnableProfileWriteIsFalse()
        {
            var options = new CampaignSaveMigrationOptions();

            Assert.That(options.EnableProfileWrite, Is.False);
            Assert.That(CampaignSaveMigrationOptions.Default.EnableProfileWrite, Is.False);
        }

        [Test]
        public void V2RuntimeSources_DoNotCallSteamApis()
        {
            foreach (var path in Directory.GetFiles(
                         "Assets/_Features/Stages/Runtime/Campaign/Save",
                         "*.cs"))
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("Steamworks"), path);
                Assert.That(source, Does.Not.Contain("ISteamRemoteStorage"), path);
                Assert.That(source, Does.Not.Contain("SteamRemoteStorage"), path);
            }
        }

        [Test]
        public void PlayerPrefsInventoryPolicy_DocumentsRequiredKeysAndTargets()
        {
            var doc = File.ReadAllText(PolicyDocPath);

            foreach (var entry in PlayerPrefsInventoryEntries())
            {
                Assert.That(doc, Does.Contain($"`{PolicyDocTokenForEntry(entry)}`"), entry.KeyOrPrefix);
                Assert.That(doc, Does.Contain($"`{entry.Target}`"), entry.KeyOrPrefix);
            }
        }

        [TestCase(SaveSlotPrefsKeys.SaveSlotsKey, JsonTargetClassification.CampaignProfileJson)]
        [TestCase(SaveSlotPrefsKeys.ActiveSaveSlotKey, JsonTargetClassification.LocalLaunchStateJson)]
        [TestCase(SaveSlotPrefsKeys.LegacySaveSlotsKey, JsonTargetClassification.DeleteOnlyLegacy)]
        [TestCase(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey, JsonTargetClassification.DeleteOnlyLegacy)]
        [TestCase(EditorDirectPlayContextStore.TempSaveSlotStoreKey, JsonTargetClassification.EditorOnlyJson)]
        [TestCase(EditorDirectPlayContextStore.TempActiveSlotProviderKey, JsonTargetClassification.EditorOnlyJson)]
        [TestCase(CampaignLegacyImportMarkerStore.ImportDisabledKey, JsonTargetClassification.CampaignProfileJson)]
        [TestCase(CampaignLegacyImportMarkerStore.ImportedSourceHashKey, JsonTargetClassification.CampaignProfileJson)]
        [TestCase(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey, JsonTargetClassification.CampaignProfileJson)]
        [TestCase(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey, JsonTargetClassification.CampaignProfileJson)]
        [TestCase("settings.audio.master.volume", JsonTargetClassification.LocalSettingsJson)]
        [TestCase("settings.display.width", JsonTargetClassification.LocalSettingsJson)]
        [TestCase("Game.Feature.Input.KeyboardMovementScheme", JsonTargetClassification.LocalSettingsJson)]
        [TestCase("Game.Feature.Input.KeyboardBindingOverridesJson", JsonTargetClassification.LocalSettingsJson)]
        [TestCase("All1ShaderMaterials", JsonTargetClassification.EditorOnlyJson)]
        [TestCase("allIn1DefaultShader", JsonTargetClassification.EditorOnlyJson)]
        [TestCase("Game.Feature.Stages.Editor.Tests.SomeFixture.abc123", JsonTargetClassification.Remove)]
        public void PlayerPrefsInventory_ClassifiesKnownKeysAndPrefixes(
            string key,
            JsonTargetClassification expectedTarget)
        {
            var entry = FindInventoryEntryForKey(key);

            Assert.That(entry, Is.Not.Null, key);
            Assert.That(entry.Target, Is.EqualTo(expectedTarget), key);
        }

        [Test]
        public void PlayerPrefsInventory_SourceScanFindsNoUnclassifiedPlayerPrefsKeys()
        {
            var discovered = DiscoverPlayerPrefsKeyTokensFromSource().ToArray();
            var unclassified = discovered
                .Where(token => FindInventoryEntryForKey(token) == null)
                .OrderBy(token => token, StringComparer.Ordinal)
                .ToArray();

            Assert.That(discovered, Is.Not.Empty);
            Assert.That(
                unclassified,
                Is.Empty,
                "Unclassified PlayerPrefs key/prefix tokens: " + string.Join(", ", unclassified));
        }

        [Test]
        public void PlayerPrefsInventory_TargetClassificationGuardMatchesPolicyBoundaries()
        {
            Assert.That(FindInventoryEntryForKey(SaveSlotPrefsKeys.SaveSlotsKey).Target, Is.EqualTo(JsonTargetClassification.CampaignProfileJson));
            Assert.That(FindInventoryEntryForKey(SaveSlotPrefsKeys.ActiveSaveSlotKey).Target, Is.EqualTo(JsonTargetClassification.LocalLaunchStateJson));
            Assert.That(FindInventoryEntryForKey("settings.audio.sfx.volume").Target, Is.EqualTo(JsonTargetClassification.LocalSettingsJson));
            Assert.That(FindInventoryEntryForKey("settings.display.refreshNumerator").Target, Is.EqualTo(JsonTargetClassification.LocalSettingsJson));
            Assert.That(FindInventoryEntryForKey("Game.Feature.Input.KeyboardBindingOverridesJson").Target, Is.EqualTo(JsonTargetClassification.LocalSettingsJson));
            Assert.That(FindInventoryEntryForKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey).Target, Is.EqualTo(JsonTargetClassification.EditorOnlyJson));
            Assert.That(FindInventoryEntryForKey(SaveSlotPrefsKeys.LegacySaveSlotsKey).Target, Is.EqualTo(JsonTargetClassification.DeleteOnlyLegacy));
            Assert.That(FindInventoryEntryForKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey).Target, Is.EqualTo(JsonTargetClassification.CampaignProfileJson));
        }

        [Test]
        public void ProductionStorageSwitch_UsesProviderAndKeepsLowLevelDefaultsLegacy()
        {
            Assert.That(SaveSlotStore.DefaultPlayerPrefsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(new SaveSlotStore().PlayerPrefsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(CampaignSaveMigrationOptions.Default.EnableProfileWrite, Is.False);

            foreach (var path in EnumerateProductionReadinessSourceFiles())
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("CampaignSaveServiceFactory"), path);
                Assert.That(source, Does.Not.Contain("CampaignSaveService"), path);
                Assert.That(source, Does.Not.Contain("FileCampaignProfileRepository"), path);
                Assert.That(source, Does.Not.Contain("profile.json"), path);
            }

            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
        }

        private static IEnumerable<string> EnumerateProductionReadinessSourceFiles()
        {
            foreach (var path in ProductionCompositionFiles)
            {
                yield return path;
            }

            foreach (var path in Directory.GetFiles(
                         "Assets/_Features/Stages/Runtime/Load",
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                yield return path;
            }
        }

        private static PlayerPrefsInventoryEntry FindInventoryEntryForKey(string key)
        {
            return PlayerPrefsInventoryEntries()
                .FirstOrDefault(entry => entry.Covers(key));
        }

        private static IEnumerable<PlayerPrefsInventoryEntry> PlayerPrefsInventoryEntries()
        {
            yield return PlayerPrefsInventoryEntry.Exact(
                SaveSlotPrefsKeys.SaveSlotsKey,
                "SaveSlotStore / CampaignLegacySourceReader",
                productionReadWrite: true,
                JsonTargetClassification.CampaignProfileJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                SaveSlotPrefsKeys.ActiveSaveSlotKey,
                "ActiveSlotProvider / PendingLaunchSlotProvider",
                productionReadWrite: true,
                JsonTargetClassification.LocalLaunchStateJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                SaveSlotPrefsKeys.LegacySaveSlotsKey,
                "legacy stage clear cleanup",
                productionReadWrite: false,
                JsonTargetClassification.DeleteOnlyLegacy);
            yield return PlayerPrefsInventoryEntry.Exact(
                SaveSlotPrefsKeys.LegacyActiveSaveSlotKey,
                "legacy active slot cleanup",
                productionReadWrite: false,
                JsonTargetClassification.DeleteOnlyLegacy);
            yield return PlayerPrefsInventoryEntry.Exact(
                EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                "EditorDirectPlayContextStore",
                productionReadWrite: false,
                JsonTargetClassification.EditorOnlyJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                EditorDirectPlayContextStore.TempActiveSlotProviderKey,
                "EditorDirectPlayContextStore",
                productionReadWrite: false,
                JsonTargetClassification.EditorOnlyJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                CampaignLegacyImportMarkerStore.ImportDisabledKey,
                "CampaignLegacyImportMarkerStore",
                productionReadWrite: false,
                JsonTargetClassification.CampaignProfileJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                CampaignLegacyImportMarkerStore.ImportedSourceHashKey,
                "CampaignLegacyImportMarkerStore",
                productionReadWrite: false,
                JsonTargetClassification.CampaignProfileJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey,
                "CampaignLegacyImportMarkerStore",
                productionReadWrite: false,
                JsonTargetClassification.CampaignProfileJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey,
                "CampaignLegacyImportMarkerStore",
                productionReadWrite: false,
                JsonTargetClassification.CampaignProfileJson);
            yield return PlayerPrefsInventoryEntry.Prefix(
                "settings.audio.",
                "PlayerPrefsAudioSettingsStore",
                productionReadWrite: true,
                JsonTargetClassification.LocalSettingsJson);
            yield return PlayerPrefsInventoryEntry.Prefix(
                "settings.display.",
                "PlayerPrefsDisplaySettingsStore",
                productionReadWrite: true,
                JsonTargetClassification.LocalSettingsJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                "Game.Feature.Input.KeyboardMovementScheme",
                "PlayerPrefsKeyboardBindingStore",
                productionReadWrite: true,
                JsonTargetClassification.LocalSettingsJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                "Game.Feature.Input.KeyboardBindingOverridesJson",
                "PlayerPrefsKeyboardBindingStore",
                productionReadWrite: true,
                JsonTargetClassification.LocalSettingsJson);
            yield return PlayerPrefsInventoryEntry.Prefix(
                "All1Shader",
                "All In 1 Sprite Shader editor tooling",
                productionReadWrite: false,
                JsonTargetClassification.EditorOnlyJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                "allIn1DefaultShader",
                "All In 1 Sprite Shader editor tooling",
                productionReadWrite: false,
                JsonTargetClassification.EditorOnlyJson);
            yield return PlayerPrefsInventoryEntry.Exact(
                "Assets/",
                "All In 1 Sprite Shader editor tooling",
                productionReadWrite: false,
                JsonTargetClassification.UnknownNeedsDecision);
            yield return PlayerPrefsInventoryEntry.Prefix(
                "Game.Feature.Stages.Editor.Tests.",
                "test-only dynamic keys",
                productionReadWrite: false,
                JsonTargetClassification.Remove);
            yield return PlayerPrefsInventoryEntry.Prefix(
                "Game.Feature.Stages.Tests.",
                "test-only dynamic keys",
                productionReadWrite: false,
                JsonTargetClassification.Remove);
            yield return PlayerPrefsInventoryEntry.Prefix(
                "Game.Feature.UI.Tests.",
                "test-only dynamic keys",
                productionReadWrite: false,
                JsonTargetClassification.Remove);
            yield return PlayerPrefsInventoryEntry.Prefix(
                "Game.Feature.Tests.",
                "test-only dynamic keys",
                productionReadWrite: false,
                JsonTargetClassification.Remove);
            yield return PlayerPrefsInventoryEntry.Prefix(
                "pending-launch-slot-provider-tests-",
                "test-only dynamic keys",
                productionReadWrite: false,
                JsonTargetClassification.Remove);
        }

        private static IEnumerable<string> DiscoverPlayerPrefsKeyTokensFromSource()
        {
            var regex = new Regex("\"(?:\\\\.|[^\"])*\"", RegexOptions.Compiled);
            var playerPrefsCallRegex = new Regex(
                @"PlayerPrefs\.(GetString|SetString|GetInt|SetInt|GetFloat|SetFloat|HasKey|DeleteKey|Save)\b",
                RegexOptions.Compiled);
            foreach (var path in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
            {
                var normalizedPath = path.Replace('\\', '/');
                if (normalizedPath.Contains("/obj/", StringComparison.Ordinal) ||
                    normalizedPath.Contains("/Library/", StringComparison.Ordinal) ||
                    normalizedPath.Contains("/Temp/", StringComparison.Ordinal) ||
                    normalizedPath.EndsWith(
                        "Assets/_Features/Stages/Editor/Tests/SaveArchitectureV2ProductionReadinessTests.cs",
                        StringComparison.Ordinal) ||
                    normalizedPath.EndsWith(
                        "/Assets/_Features/Stages/Editor/Tests/SaveArchitectureV2ProductionReadinessTests.cs",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var source = File.ReadAllText(path);
                if (!playerPrefsCallRegex.IsMatch(source))
                {
                    continue;
                }

                foreach (Match match in regex.Matches(source))
                {
                    var token = DecodeStringLiteral(match.Value);
                    if (IsPlayerPrefsInventoryCandidate(token))
                    {
                        yield return token;
                    }
                }
            }
        }

        private static bool IsPlayerPrefsInventoryCandidate(string token)
        {
            return token.StartsWith("Game.Feature.Stages.", StringComparison.Ordinal) ||
                   token.StartsWith("Game.Feature.Input.", StringComparison.Ordinal) ||
                   token.StartsWith("Game.Feature.UI.Tests.", StringComparison.Ordinal) ||
                   token.StartsWith("Game.Feature.Tests.", StringComparison.Ordinal) ||
                   token.StartsWith("settings.audio.", StringComparison.Ordinal) ||
                   token.StartsWith("settings.display.", StringComparison.Ordinal) ||
                   token.StartsWith("All1Shader", StringComparison.Ordinal) ||
                   token.StartsWith("pending-launch-slot-provider-tests-", StringComparison.Ordinal) ||
                   string.Equals(token, "allIn1DefaultShader", StringComparison.Ordinal) ||
                   string.Equals(token, "Assets/", StringComparison.Ordinal);
        }

        private static string DecodeStringLiteral(string literal)
        {
            if (literal.Length < 2)
            {
                return string.Empty;
            }

            return literal.Substring(1, literal.Length - 2)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        private static string PolicyDocTokenForEntry(PlayerPrefsInventoryEntry entry)
        {
            if (!entry.IsPrefix)
            {
                return entry.KeyOrPrefix;
            }

            return entry.KeyOrPrefix.EndsWith(".", StringComparison.Ordinal) ||
                   entry.KeyOrPrefix.EndsWith("-", StringComparison.Ordinal)
                ? entry.KeyOrPrefix + "*"
                : entry.KeyOrPrefix + "*";
        }

        private sealed class PlayerPrefsInventoryEntry
        {
            private PlayerPrefsInventoryEntry(
                string keyOrPrefix,
                bool isPrefix,
                string owner,
                bool productionReadWrite,
                JsonTargetClassification target)
            {
                KeyOrPrefix = keyOrPrefix;
                IsPrefix = isPrefix;
                Owner = owner;
                ProductionReadWrite = productionReadWrite;
                Target = target;
            }

            public string KeyOrPrefix { get; }

            public bool IsPrefix { get; }

            public string Owner { get; }

            public bool ProductionReadWrite { get; }

            public JsonTargetClassification Target { get; }

            public static PlayerPrefsInventoryEntry Exact(
                string key,
                string owner,
                bool productionReadWrite,
                JsonTargetClassification target)
            {
                return new PlayerPrefsInventoryEntry(key, isPrefix: false, owner, productionReadWrite, target);
            }

            public static PlayerPrefsInventoryEntry Prefix(
                string prefix,
                string owner,
                bool productionReadWrite,
                JsonTargetClassification target)
            {
                return new PlayerPrefsInventoryEntry(prefix, isPrefix: true, owner, productionReadWrite, target);
            }

            public bool Covers(string key)
            {
                return IsPrefix
                    ? key.StartsWith(KeyOrPrefix, StringComparison.Ordinal)
                    : string.Equals(key, KeyOrPrefix, StringComparison.Ordinal);
            }
        }
    }

    public enum JsonTargetClassification
    {
        CampaignProfileJson = 0,
        LocalSettingsJson = 1,
        LocalLaunchStateJson = 2,
        EditorOnlyJson = 3,
        DeleteOnlyLegacy = 4,
        ImportOnlyLegacy = 5,
        Remove = 6,
        UnknownNeedsDecision = 7,
    }
}
