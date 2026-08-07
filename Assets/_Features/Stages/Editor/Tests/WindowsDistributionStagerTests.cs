using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class WindowsDistributionStagerTests
    {
        private string fixtureRoot;
        private string repositoryRoot;
        private string sourceRoot;

        [SetUp]
        public void SetUp()
        {
            fixtureRoot = Path.Combine(
                Path.GetTempPath(),
                "j2m-windows-distribution-stager-" + Guid.NewGuid().ToString("N"));
            repositoryRoot = Path.Combine(fixtureRoot, "repository");
            sourceRoot = Path.Combine(fixtureRoot, "raw");
            Directory.CreateDirectory(repositoryRoot);
            Directory.CreateDirectory(sourceRoot);
            WriteCanonicalRawFixture(includeSteamDependencies: true);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        [Test]
        public void SteamWindows_IncludeListPreservesRuntimeAndSteamDependencies()
        {
            var result = Stage("steam-windows", "steam-output");

            Assert.That(File.Exists(Path.Combine(result.PayloadRoot, "VectorQuake.exe")), Is.True);
            Assert.That(File.Exists(Path.Combine(result.PayloadRoot, "UnityPlayer.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(
                result.PayloadRoot, "VectorQuake_Data", "globalgamemanagers")), Is.True);
            Assert.That(result.SteamNativeCount, Is.EqualTo(1));
            Assert.That(result.SteamManagedCount, Is.EqualTo(1));
            Assert.That(result.SteamAppIdCount, Is.Zero);
            Assert.That(result.DeniedArtifactCount, Is.Zero);
        }

        [Test]
        public void DirectWindows_IncludeListExcludesEverySteamArtifact()
        {
            var result = Stage("direct-windows", "direct-output");

            Assert.That(result.SteamNativeCount, Is.Zero);
            Assert.That(result.SteamManagedCount, Is.Zero);
            Assert.That(result.SteamAppIdCount, Is.Zero);
            Assert.That(FindFileNames(result.PayloadRoot), Does.Not.Contain("steam_api64.dll"));
            Assert.That(FindFileNames(result.PayloadRoot),
                Does.Not.Contain("com.rlabrecque.steamworks.net.dll"));
        }

        [TestCase("direct-windows", "direct-appid-output")]
        [TestCase("steam-windows", "steam-appid-output")]
        public void BothTargets_ExcludeSteamAppIdAndForbiddenContent(
            string target,
            string outputName)
        {
            var result = Stage(target, outputName);
            var relativeFiles = Directory.GetFiles(
                    result.PayloadRoot, "*", SearchOption.AllDirectories)
                .Select(path => NormalizeRelative(result.PayloadRoot, path))
                .ToArray();

            Assert.That(relativeFiles.Any(SteamPipeStagingSanitizerPolicy.IsDeniedContent),
                Is.False);
            Assert.That(relativeFiles, Does.Not.Contain("steam_appid.txt"));
            Assert.That(relativeFiles.Any(path => path.EndsWith(".pdb")), Is.False);
            Assert.That(relativeFiles.Any(path => path.Contains("TestLogs/")), Is.False);
            Assert.That(relativeFiles.Any(path => path.Contains("Saves/")), Is.False);
        }

        [Test]
        public void SteamWindows_MissingRequiredNativeArtifactFailsWithoutSuccessMarker()
        {
            File.Delete(Path.Combine(
                sourceRoot, "VectorQuake_Data", "Plugins", "x86_64", "steam_api64.dll"));
            var output = Path.Combine(fixtureRoot, "missing-steam-output");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage("steam-windows", "missing-steam-output"));

            Assert.That(exception.Code,
                Is.EqualTo("STAGING_PROMOTED_ARTIFACT_CONTRACT_FAILED"));
            Assert.That(Directory.Exists(output), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown-windows")]
        public void MissingOrUnknownTargetFails(string target)
        {
            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage(target, "unknown-output"));

            Assert.That(exception.Code,
                Is.EqualTo("STAGING_UNKNOWN_DISTRIBUTION_TARGET"));
        }

        [Test]
        public void CopyIntegrityAndSourceImmutabilityUseActualBytes()
        {
            var sourceBefore = HashTree(sourceRoot);
            var result = Stage("steam-windows", "integrity-output");
            var sourceAfter = HashTree(sourceRoot);

            Assert.That(sourceAfter, Is.EqualTo(sourceBefore));
            foreach (var destination in Directory.GetFiles(
                         result.PayloadRoot, "*", SearchOption.AllDirectories))
            {
                var relative = NormalizeRelative(result.PayloadRoot, destination);
                var source = Path.Combine(
                    sourceRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(source), Is.True, relative);
                Assert.That(GetSha256(destination), Is.EqualTo(GetSha256(source)), relative);
            }
        }

        [Test]
        public void DeterministicManifestHasSameOrderedInventoryAndHashes()
        {
            var first = Stage("steam-windows", "deterministic-one", "same-run");
            var second = Stage("steam-windows", "deterministic-two", "same-run");

            Assert.That(File.ReadAllBytes(second.ManifestPath),
                Is.EqualTo(File.ReadAllBytes(first.ManifestPath)));
            var text = File.ReadAllText(first.ManifestPath);
            var paths = ExtractManifestRelativePaths(text);
            var sorted = paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(paths, Is.EqualTo(sorted));
        }

        [Test]
        public void OutputNestedInsideSourceIsRejected()
        {
            var request = CreateRequest(
                "direct-windows",
                Path.Combine(sourceRoot, "promoted"),
                "nested-run");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.Stage(request));

            Assert.That(exception.Code, Is.EqualTo("STAGING_PATH_ESCAPE_DETECTED"));
        }

        [Test]
        public void TraversalSegmentsAndEscapingRuntimeCandidatesAreRejected()
        {
            var request = CreateRequest(
                "direct-windows",
                Path.Combine(fixtureRoot, "traversal-output"),
                "traversal-run");
            request.SourceBuildRoot = Path.Combine(sourceRoot, "..", "raw");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.Stage(request));

            Assert.That(exception.Code, Is.EqualTo("STAGING_PATH_ESCAPE_DETECTED"));
            Assert.That(WindowsDistributionStager.IsRuntimeIncludeCandidate("../outside.dll"),
                Is.False);
            Assert.That(WindowsDistributionStager.IsRuntimeIncludeCandidate(
                "VectorQuake_Data/../../outside.dll"), Is.False);
        }

        [Test]
        public void SourceJunctionIsRejectedFailClosed()
        {
            var junctionTarget = Path.Combine(fixtureRoot, "junction-target");
            var junction = Path.Combine(sourceRoot, "VectorQuake_Data", "JunctionContent");
            Directory.CreateDirectory(junctionTarget);
            File.WriteAllText(Path.Combine(junctionTarget, "outside.bin"), "outside");
            if (!TryCreateJunction(junction, junctionTarget))
            {
                Assert.Ignore("Windows junction creation is unavailable in this environment.");
            }

            try
            {
                var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                    Stage("direct-windows", "junction-output"));
                Assert.That(exception.Code, Is.EqualTo("STAGING_REPARSE_POINT_REJECTED"));
            }
            finally
            {
                if (Directory.Exists(junction))
                {
                    Directory.Delete(junction);
                }
            }
        }

        [Test]
        public void ManifestAndSuccessStayOutsideShippingPayload()
        {
            var result = Stage("direct-windows", "evidence-output");

            Assert.That(File.Exists(result.ManifestPath), Is.True);
            Assert.That(File.Exists(result.SuccessPath), Is.True);
            Assert.That(File.Exists(Path.Combine(
                result.PayloadRoot, WindowsDistributionStager.ManifestFileName)), Is.False);
            Assert.That(File.Exists(Path.Combine(
                result.PayloadRoot, WindowsDistributionStager.SuccessFileName)), Is.False);
        }

        private WindowsDistributionStagingResult Stage(
            string target,
            string outputName,
            string runId = "fixture-run")
        {
            return WindowsDistributionStager.Stage(CreateRequest(
                target,
                Path.Combine(fixtureRoot, outputName),
                runId));
        }

        private WindowsDistributionStagingRequest CreateRequest(
            string target,
            string output,
            string runId)
        {
            return new WindowsDistributionStagingRequest
            {
                SourceBuildRoot = sourceRoot,
                DistributionTargetId = target,
                OutputRoot = output,
                RepositoryRoot = repositoryRoot,
                SourceSha = "source-sha",
                SourceTree = "source-tree",
                ArtifactId = "artifact-id",
                RunId = runId,
            };
        }

        private void WriteCanonicalRawFixture(bool includeSteamDependencies)
        {
            WriteFile("VectorQuake.exe", "exe");
            WriteFile("UnityPlayer.dll", "unity");
            WriteFile("UnityCrashHandler64.exe", "crash-handler");
            WriteFile("VectorQuake_Data/globalgamemanagers", "managers");
            WriteFile("VectorQuake_Data/Managed/Game.dll", "game-managed");
            WriteFile("VectorQuake_Data/TestLogs/a.log", "denied-log");
            WriteFile("VectorQuake_Data/Saves/profile.json", "denied-save");
            WriteFile("debug.pdb", "denied-symbol");
            WriteFile("steam_appid.txt", "480");
            WriteFile("Docs/not-runtime.txt", "repository-content");
            if (includeSteamDependencies)
            {
                WriteFile(
                    "VectorQuake_Data/Managed/com.rlabrecque.steamworks.net.dll",
                    "steam-managed");
                WriteFile(
                    "VectorQuake_Data/Plugins/x86_64/steam_api64.dll",
                    "steam-native");
            }
        }

        private void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(
                sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        private static string[] FindFileNames(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .ToArray();
        }

        private static Dictionary<string, string> HashTree(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    path => NormalizeRelative(root, path),
                    GetSha256,
                    StringComparer.Ordinal);
        }

        private static string NormalizeRelative(string root, string path)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
            return Path.GetFullPath(path).Substring(normalizedRoot.Length + 1)
                .Replace('\\', '/');
        }

        private static string GetSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(stream)
                    .Select(value => value.ToString("x2")));
            }
        }

        private static string[] ExtractManifestRelativePaths(string manifest)
        {
            const string marker = "\"relativePath\": \"";
            return manifest.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains(marker))
                .Select(line =>
                {
                    var start = line.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
                    return line.Substring(start, line.LastIndexOf('"') - start);
                })
                .ToArray();
        }

        private static bool TryCreateJunction(string junction, string target)
        {
            var start = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c mklink /J \"" + junction + "\" \"" + target + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var process = Process.Start(start))
            {
                process.WaitForExit();
                return process.ExitCode == 0 && Directory.Exists(junction);
            }
        }
    }
}
