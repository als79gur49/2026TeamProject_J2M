using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Steamworks;

namespace Game.Platform.Steam.SteamworksNet.Tests.EditMode
{
    public sealed class SteamworksNetDependencyInventoryTests
    {
        private const string DependencyRoot =
            "Packages/com.j2m.thirdparty.steamworksnet";

        [Test]
        public void Dependency_IsPinnedToOfficialSteamworksNetSdk165Runtime()
        {
            Assert.That(Steamworks.Version.SteamworksNETVersion, Is.EqualTo("2025.165.0"));
            Assert.That(Steamworks.Version.SteamworksSDKVersion, Is.EqualTo("1.65"));
            Assert.That(Steamworks.Version.SteamAPI64DLLSize, Is.EqualTo(319128));

            var packageJson = File.ReadAllText(Path.Combine(DependencyRoot, "package.json"));
            Assert.That(packageJson, Does.Contain("\"version\": \"2025.165.0-j2m.1\""));
            Assert.That(packageJson, Does.Contain(
                "3c236146fe55e48eb8776a6b912a7675ef591b08"));
        }

        [Test]
        public void NativeInventory_HasOneValveSdk165WindowsX64Owner()
        {
            var nativeCandidates = Directory
                .EnumerateFiles("Packages", "*", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories))
                .Where(IsSteamNativeCandidate)
                .Select(NormalizePath)
                .ToArray();

            Assert.That(nativeCandidates, Is.EqualTo(new[]
            {
                DependencyRoot + "/Plugins/steam_api64.dll",
            }));

            var dllPath = nativeCandidates[0];
            Assert.That(new FileInfo(dllPath).Length, Is.EqualTo(319128));
            Assert.That(ComputeSha256(dllPath), Is.EqualTo(
                "8de54d32508e216c9135b8bf025749243d44e404c1c22a8e5fe35acecabe7a9c"));
        }

        [Test]
        public void CuratedPackage_ExcludesAppIdEditorAutomationAndWrongPlatformPayloads()
        {
            Assert.That(Directory.Exists(Path.Combine(DependencyRoot, "Editor")), Is.False);
            Assert.That(File.Exists("steam_appid.txt"), Is.False);
            Assert.That(Directory.EnumerateFiles("Assets", "steam_appid.txt",
                SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.EnumerateFiles("Packages", "steam_appid.txt",
                SearchOption.AllDirectories), Is.Empty);

            var pluginMeta = File.ReadAllText(
                Path.Combine(DependencyRoot, "Plugins/steam_api64.dll.meta"));
            Assert.That(pluginMeta, Does.Contain("OS: Windows"));
            Assert.That(pluginMeta, Does.Contain("CPU: x86_64"));
            Assert.That(pluginMeta, Does.Contain(": Any\n    second:\n      enabled: 0"));
            Assert.That(pluginMeta, Does.Contain("Editor: Editor\n    second:\n      enabled: 1"));
            Assert.That(pluginMeta, Does.Contain("Standalone: Win64"));
            Assert.That(pluginMeta, Does.Contain("Standalone: Win\n    second:\n      enabled: 0"));
            Assert.That(pluginMeta, Does.Contain("Standalone: Linux64\n    second:\n      enabled: 0"));
            Assert.That(pluginMeta, Does.Contain("Standalone: OSXUniversal\n    second:\n      enabled: 0"));
        }

        [Test]
        public void Adapter_UsesBooleanLoggedOnAndOwnedOverlayCallbackLifecycle()
        {
            var source = File.ReadAllText(
                "Packages/com.j2m.platform.steam.steamworksnet/Runtime/SteamworksNetNativeApi.cs");

            Assert.That(source, Does.Contain("return SteamUser.BLoggedOn();"));
            Assert.That(source, Does.Contain(
                "private Callback<GameOverlayActivated_t> overlayActivatedCallback;"));
            Assert.That(source, Does.Contain(
                "Callback<GameOverlayActivated_t>.Create(OnOverlayActivated)"));
            Assert.That(source, Does.Contain("callback?.Dispose();"));
            Assert.That(source, Does.Contain("Callback<UserStatsStored_t>.Create"));
            Assert.That(source, Does.Contain("Callback<UserAchievementStored_t>.Create"));
            Assert.That(source, Does.Not.Contain("UserStatsReceived_t"));
            Assert.That(source, Does.Not.Contain("RequestCurrentStats"));
            Assert.That(source, Does.Not.Contain("ClearAchievement"));
            Assert.That(source, Does.Not.Contain("ResetAllStats"));
            Assert.That(source, Does.Not.Contain("GetPersonaName"));
            Assert.That(source, Does.Not.Contain("GetFriendPersonaName"));
        }

        private static bool IsSteamNativeCandidate(string path)
        {
            var fileName = Path.GetFileName(path);
            return string.Equals(fileName, "steam_api64.dll", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "steam_api.dll", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "libsteam_api.so", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "libsteam_api.dylib", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, "steamclient64.dll", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
