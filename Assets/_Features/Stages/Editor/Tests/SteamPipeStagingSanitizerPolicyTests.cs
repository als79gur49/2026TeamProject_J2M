using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SteamPipeStagingSanitizerPolicyTests
    {
        private const string PolicyDocPath = "Docs/Architecture/Steam-Cloud-File-Inventory-Policy.md";

        [Test]
        public void SteamPipeStagingPolicy_DocumentsIncludeListCopyAsDefaultStrategy()
        {
            var doc = ReadPolicyDoc();

            Assert.That(doc, Does.Contain("include-list copy"));
            Assert.That(doc, Does.Contain("separate sanitized"));
            Assert.That(doc, Does.Contain("staging root"));
            Assert.That(doc, Does.Contain("`Builds/Steam/Staging/Windows/`"));
            Assert.That(doc, Does.Contain("Include-list copy is the primary sanitizer strategy."));
            Assert.That(doc, Does.Contain("Exclude-list cleanup"));
            Assert.That(doc, Does.Contain("secondary safety net"));
            Assert.That(doc, Does.Contain("must not be the default"));
            Assert.That(doc, Does.Contain("packaging strategy"));
        }

        [Test]
        public void SteamPipeStagingPolicy_RejectsRepoRootAndProjectGeneratedRoots()
        {
            var doc = ReadPolicyDoc();

            Assert.That(IsAllowedSteamPipeStagingRoot("."), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot(""), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot("Library"), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot("TestLogs"), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot("TestResults"), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot("Logs"), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot("ProfilerCaptures"), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot("obj"), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot("Temp"), Is.False);
            Assert.That(IsAllowedSteamPipeStagingRoot("Builds/Steam/Staging/Windows"), Is.True);
            Assert.That(doc, Does.Contain("The repository root must not be used as SteamPipe content root."));
            Assert.That(doc, Does.Contain("must not be staging roots"));
        }

        [Test]
        public void SteamPipeStagingPolicy_DocumentsReleaseRuntimeIncludeCandidatesOnly()
        {
            var doc = ReadPolicyDoc();

            Assert.That(IsSteamPipeRuntimeIncludeCandidate("VectorQuake.exe"), Is.True);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("VectorQuake_Data/globalgamemanagers"), Is.True);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("UnityPlayer.dll"), Is.True);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("MonoBleedingEdge/etc/mono/config"), Is.True);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("GameAssembly.dll"), Is.True);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("VectorQuake_Data/StreamingAssets/catalog.json"), Is.True);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("Assets/_Features/Stages/file.asset"), Is.False);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("Docs/Architecture/README.md"), Is.False);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("Packages/manifest.json"), Is.False);
            Assert.That(IsSteamPipeRuntimeIncludeCandidate("ProjectSettings/ProjectSettings.asset"), Is.False);
            Assert.That(doc, Does.Contain("Windows x64 Unity release runtime include rules"));
            Assert.That(doc, Does.Contain("`<Game>.exe`"));
            Assert.That(doc, Does.Contain("`<Game>_Data/**`"));
            Assert.That(doc, Does.Contain("`UnityPlayer.dll`"));
            Assert.That(doc, Does.Contain("`MonoBleedingEdge/**`, if Mono backend"));
            Assert.That(doc, Does.Contain("`GameAssembly.dll` and IL2CPP runtime files, if IL2CPP backend"));
            Assert.That(doc, Does.Contain("required managed assemblies"));
            Assert.That(doc, Does.Contain("required native plugins"));
            Assert.That(doc, Does.Contain("`StreamingAssets/**`, only if generated and production-required"));
            Assert.That(doc, Does.Contain("implemented by"));
            Assert.That(doc, Does.Contain("WindowsDistributionStager"));
            Assert.That(doc, Does.Contain("required"));
            Assert.That(doc, Does.Contain("runtime files manifest"));
            Assert.That(doc, Does.Contain("denied pattern scan"));
            Assert.That(doc, Does.Contain("smoke launch"));
        }

        [Test]
        public void SteamPipeStagingPolicy_KeepsVdfDepotUploadDisabled()
        {
            var doc = ReadPolicyDoc();

            Assert.That(doc, Does.Contain("No"));
            Assert.That(doc, Does.Contain("SteamPipe VDF, depot config, or upload command is defined"));
            Assert.That(doc, Does.Contain("SteamPipe VDF/depot upload remains disabled."));
            Assert.That(File.Exists("Tools/SteamPipe/app_build.vdf"), Is.False);
            Assert.That(File.Exists("Tools/SteamPipe/depot_build.vdf"), Is.False);
        }

        private static string ReadPolicyDoc()
        {
            return File.ReadAllText(PolicyDocPath);
        }

        private static bool IsAllowedSteamPipeStagingRoot(string candidate)
        {
            var value = SteamPipeStagingSanitizerPolicy.Normalize(candidate).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(value) || value == ".")
            {
                return false;
            }

            var deniedRoots = new[]
            {
                "Library",
                "TestLogs",
                "TestResults",
                "Logs",
                "ProfilerCaptures",
                "obj",
                "Temp",
            };
            for (var i = 0; i < deniedRoots.Length; i++)
            {
                if (IsPathOrChildOf(value, deniedRoots[i]))
                {
                    return false;
                }
            }

            return !SteamPipeStagingSanitizerPolicy.IsDeniedContent(value);
        }

        private static bool IsSteamPipeRuntimeIncludeCandidate(string candidate)
        {
            var value = SteamPipeStagingSanitizerPolicy.Normalize(candidate);
            return !SteamPipeStagingSanitizerPolicy.IsDeniedContent(value) &&
                   WindowsDistributionStager.IsRuntimeIncludeCandidate(value);
        }

        public static string PolicyDocTokenForDeniedCandidate(string candidate)
        {
            var value = SteamPipeStagingSanitizerPolicy.Normalize(candidate);
            if (value.StartsWith("TestLogs/SaveReadiness/", StringComparison.Ordinal))
            {
                return "`TestLogs/SaveReadiness/`";
            }

            if (value.StartsWith("Saves/profile.", StringComparison.Ordinal) &&
                value.EndsWith(".tmp", StringComparison.Ordinal))
            {
                return "`Saves/profile.*.tmp`";
            }

            if (value.StartsWith("Saves/profile.json.corrupt.", StringComparison.Ordinal))
            {
                return "`Saves/profile.json.corrupt.*`";
            }

            if (value == "Settings/local-settings.json" ||
                value == "Saves/local-launch-state.json" ||
                value == "Saves/editor-direct-play.json" ||
                value == "Saves/direct-play-temp.json")
            {
                return $"`{value}`";
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

            var deniedDirectories = new[]
            {
                "TestLogs",
                "TestResult",
                "TestResults",
                "Logs",
                "ProfilerCaptures",
                "Library",
                "UserSettings",
                "obj",
                "Temp",
                ".git",
                ".github",
                ".vs",
                "Docs",
                "Assets",
                "Packages",
                "ProjectSettings",
            };
            for (var i = 0; i < deniedDirectories.Length; i++)
            {
                if (IsPathOrChildOf(value, deniedDirectories[i]))
                {
                    return $"`{deniedDirectories[i]}/`";
                }
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

        public static IEnumerable<string> DeniedSteamPipeContentSamples()
        {
            yield return "steam_appid.txt";
            yield return "TestLogs/";
            yield return "TestLogs/SaveReadiness/20260709/abc123/CampaignProfileReadiness.md";
            yield return "TestResult/results.xml";
            yield return "TestResults/wsl-unity-full-editmode.xml";
            yield return "Logs/player.log";
            yield return "ProfilerCaptures/capture.data";
            yield return "CampaignProfileReadiness.md";
            yield return "campaign-save-seed.json";
            yield return "Saves/profile.json";
            yield return "Saves/profile.json.bak";
            yield return "Saves/profile.123.tmp";
            yield return "Saves/profile.json.corrupt.202607090000000000000";
            yield return "Settings/local-settings.json";
            yield return "Saves/local-launch-state.json";
            yield return "Saves/editor-direct-play.json";
            yield return "Saves/direct-play-temp.json";
            yield return "Game.pdb";
            yield return "Game.mdb";
            yield return "player.log";
            yield return "profile.leftover.tmp";
            yield return "Library/metadata";
            yield return "UserSettings/EditorUserSettings.asset";
            yield return "obj/Debug/file";
            yield return "Temp/build.tmp";
            yield return ".git/config";
            yield return ".github/workflows/release.yml";
            yield return ".vs/config";
            yield return "Docs/Architecture/README.md";
            yield return "Assets/_Features/Stages/file.asset";
            yield return "Packages/manifest.json";
            yield return "ProjectSettings/ProjectSettings.asset";
            yield return "Tools/SteamPipe/app/cache/session";
            yield return "Tools/SteamPipe/app/output/log.txt";
            yield return "Tools/SteamPipe/app/login/token";
        }

        private static bool IsPathOrChildOf(string value, string directory)
        {
            return value == directory ||
                   value == $"{directory}/" ||
                   value.StartsWith($"{directory}/", StringComparison.Ordinal);
        }

        private static bool IsUnderSteamPipeGeneratedDirectory(string value, string leaf)
        {
            var marker = $"/{leaf}";
            return value.StartsWith("Tools/SteamPipe/", StringComparison.Ordinal) &&
                   (value.EndsWith(marker, StringComparison.Ordinal) ||
                    value.Contains($"{marker}/", StringComparison.Ordinal));
        }
    }

    public sealed class SteamPipeContentExcludePolicyTests
    {
        [TestCaseSource(
            typeof(SteamPipeStagingSanitizerPolicyTests),
            nameof(SteamPipeStagingSanitizerPolicyTests.DeniedSteamPipeContentSamples))]
        public void SteamPipeContentPolicy_DeniedPatternsAreExcludedAndDocumented(string candidate)
        {
            var doc = File.ReadAllText("Docs/Architecture/Steam-Cloud-File-Inventory-Policy.md");

            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent(candidate), Is.True, candidate);
            Assert.That(
                doc,
                Does.Contain(SteamPipeStagingSanitizerPolicyTests.PolicyDocTokenForDeniedCandidate(candidate)),
                candidate);
        }
    }

    public sealed class SteamPipeContentIncludePolicyTests
    {
        [Test]
        public void SteamPipeContentPolicy_DoesNotIncludeRepositorySourcesAsRuntimeContent()
        {
            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent("Assets/Runtime/file.cs"), Is.True);
            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent("Docs/Architecture/README.md"), Is.True);
            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent("Packages/manifest.json"), Is.True);
            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent("ProjectSettings/ProjectSettings.asset"), Is.True);
        }
    }

    public sealed class SteamPipeNoRepoRootStagingTests
    {
        [Test]
        public void SteamPipePolicy_DocumentsRepoRootAndSanitizedRootRequirement()
        {
            var doc = File.ReadAllText("Docs/Architecture/Steam-Cloud-File-Inventory-Policy.md");

            Assert.That(doc, Does.Contain("The repository root must not be used as SteamPipe content root."));
            Assert.That(doc, Does.Contain("separate sanitized"));
            Assert.That(doc, Does.Contain("staging root"));
        }
    }

    public sealed class SteamPipeNoReadinessArtifactsTests
    {
        [Test]
        public void SteamPipePolicy_ExcludesReadinessArtifactsFromReleaseContent()
        {
            var doc = File.ReadAllText("Docs/Architecture/Steam-Cloud-File-Inventory-Policy.md");

            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent(
                "TestLogs/SaveReadiness/run/commit/CampaignProfileReadiness.md"), Is.True);
            Assert.That(doc, Does.Contain("Readiness artifacts are CI artifacts only"));
        }
    }

    public sealed class SteamPipeNoSteamAppIdTests
    {
        [Test]
        public void SteamPipePolicy_ExcludesSteamAppIdTxt()
        {
            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent("steam_appid.txt"), Is.True);
        }
    }

    public sealed class SteamPipeNoDebugSymbolsTests
    {
        [TestCase("Game.pdb")]
        [TestCase("Game.mdb")]
        public void SteamPipePolicy_ExcludesDebugSymbols(string candidate)
        {
            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent(candidate), Is.True);
        }
    }

    public sealed class SteamPipeNoUserSaveFilesTests
    {
        [TestCase("Saves/profile.json")]
        [TestCase("Saves/profile.json.bak")]
        [TestCase("Saves/profile.123.tmp")]
        [TestCase("Saves/profile.json.corrupt.202607090000000000000")]
        [TestCase("Settings/local-settings.json")]
        [TestCase("Saves/local-launch-state.json")]
        [TestCase("Saves/editor-direct-play.json")]
        [TestCase("Saves/direct-play-temp.json")]
        public void SteamPipePolicy_ExcludesLocalUserSaveFiles(string candidate)
        {
            Assert.That(SteamPipeStagingSanitizerPolicy.IsDeniedContent(candidate), Is.True);
        }
    }
}
