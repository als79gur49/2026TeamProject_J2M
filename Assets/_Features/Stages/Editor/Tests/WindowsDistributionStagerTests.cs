using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class WindowsDistributionStagerTests
    {
        private static readonly string ValidThirdPartyNotices = File.ReadAllText(
            Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact));

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
                Directory.Delete(ToExtendedPath(fixtureRoot), recursive: true);
            }
        }

        [Test]
        public void SteamWindows_IncludeListPreservesRuntimeAndSteamDependencies()
        {
            var result = Stage("steam-windows", "steam-output");

            Assert.That(File.Exists(Path.Combine(result.PayloadRoot, "VectorQuake.exe")), Is.True);
            Assert.That(File.Exists(Path.Combine(result.PayloadRoot, "UnityPlayer.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(
                result.PayloadRoot,
                WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact)), Is.True);
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
            Assert.That(File.Exists(Path.Combine(
                result.PayloadRoot,
                WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact)), Is.True);
        }

        [TestCase("direct-windows", "direct-missing-notice-output")]
        [TestCase("steam-windows", "steam-missing-notice-output")]
        public void BothTargets_MissingThirdPartyNoticesFailsClosed(
            string target,
            string outputName)
        {
            File.Delete(Path.Combine(
                sourceRoot,
                WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact));

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage(target, outputName));

            Assert.That(exception.Code,
                Is.EqualTo("STAGING_REQUIRED_PUBLIC_NOTICE_MISSING"));
            AssertPromotedOutputAbsent(Path.Combine(fixtureRoot, outputName));
        }

        [TestCase("")]
        [TestCase("   \r\n\t")]
        [TestCase("VectorQuake Third-Party Notices")]
        public void BothTargets_InvalidThirdPartyNoticesFailClosed(string content)
        {
            File.WriteAllText(
                Path.Combine(
                    sourceRoot,
                    WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact),
                content);

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage("direct-windows", "invalid-notice-output"));

            Assert.That(exception.Code, Is.EqualTo("STAGING_PUBLIC_NOTICE_INVALID"));
            AssertPromotedOutputAbsent(Path.Combine(fixtureRoot, "invalid-notice-output"));
        }

        [Test]
        public void PublicNoticeContract_AcceptsCanonicalDocumentAndRejectsMarkerOnlyFixture()
        {
            Assert.That(
                WindowsDistributionTargetPolicy.HasValidThirdPartyNoticeContent(
                    ValidThirdPartyNotices),
                Is.True);
            Assert.That(
                WindowsDistributionTargetPolicy.HasValidThirdPartyNoticeContent(
                    string.Join(
                        "\n",
                        WindowsDistributionTargetPolicy.ThirdPartyNoticeRequiredMarkers)),
                Is.False);
        }

        [Test]
        public void PublicNoticeContract_RejectsEveryMissingComponentInventoryFragment()
        {
            foreach (var fragment in
                     WindowsDistributionTargetPolicy.ThirdPartyNoticeRequiredFragments)
            {
                var mutated = ValidThirdPartyNotices.Replace(fragment, string.Empty);
                Assert.That(mutated, Is.Not.EqualTo(ValidThirdPartyNotices), fragment);
                Assert.That(
                    WindowsDistributionTargetPolicy.HasValidThirdPartyNoticeContent(mutated),
                    Is.False,
                    fragment);
            }
        }

        [TestCase(
            "are permitted provided that the following conditions are met:",
            "are allowed provided that the following conditions are met:")]
        [TestCase(
            "to use, copy, modify, merge, publish, distribute, sublicense",
            "to use, modify, merge, publish, distribute, sublicense")]
        [TestCase(
            "development of collaborative font projects",
            "development of font projects")]
        public void PublicNoticeContract_RejectsCanonicalLicenseBodyMutation(
            string original,
            string replacement)
        {
            var mutated = ValidThirdPartyNotices.Replace(original, replacement);
            Assert.That(mutated, Is.Not.EqualTo(ValidThirdPartyNotices));
            Assert.That(
                WindowsDistributionTargetPolicy.HasValidThirdPartyNoticeContent(mutated),
                Is.False);
        }

        [Test]
        public void PublicNoticeContract_RejectsDuplicateAndReorderedSections()
        {
            Assert.That(
                WindowsDistributionTargetPolicy.HasValidThirdPartyNoticeContent(
                    ValidThirdPartyNotices + "\nUnity UI Extensions\n"),
                Is.False);

            var reordered = ValidThirdPartyNotices
                .Replace("Unity UI Extensions", "__UI_SECTION__")
                .Replace(
                    "Steamworks.NET (Steam distribution only)",
                    "Unity UI Extensions")
                .Replace("__UI_SECTION__", "Steamworks.NET (Steam distribution only)");
            Assert.That(
                WindowsDistributionTargetPolicy.HasValidThirdPartyNoticeContent(reordered),
                Is.False);
        }

        [Test]
        public void IncorrectlyCasedThirdPartyNoticesNameFailsClosed()
        {
            var canonicalPath = Path.Combine(
                sourceRoot,
                WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact);
            File.Delete(canonicalPath);
            File.WriteAllText(
                Path.Combine(sourceRoot, "thirdpartynotices.txt"),
                ValidThirdPartyNotices);

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage("direct-windows", "wrong-case-notice-output"));

            Assert.That(exception.Code,
                Is.EqualTo("STAGING_REQUIRED_PUBLIC_NOTICE_MISSING"));
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

        [Test]
        public void MonoBackend_WithRuntimeContent_PromotesSuccess()
        {
            var result = Stage("direct-windows", "valid-mono-output");

            Assert.That(File.Exists(Path.Combine(
                result.PayloadRoot, "MonoBleedingEdge", "etc", "mono", "config")), Is.True);
            Assert.That(File.ReadAllText(result.SuccessPath),
                Does.Contain("\"scriptingBackend\": \"Mono2x\""));
        }

        [Test]
        public void MonoBackend_WithoutRuntime_FailsBeforePromotion()
        {
            Directory.Delete(Path.Combine(sourceRoot, "MonoBleedingEdge"), recursive: true);
            var output = Path.Combine(fixtureRoot, "missing-mono-output");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage("direct-windows", "missing-mono-output"));

            Assert.That(exception.Code, Is.EqualTo("STAGING_REQUIRED_RUNTIME_MISSING"));
            AssertPromotedOutputAbsent(output);
        }

        [Test]
        public void MonoBackend_WithEmptyRuntimeDirectory_FailsBeforePromotion()
        {
            var monoRoot = Path.Combine(sourceRoot, "MonoBleedingEdge");
            Directory.Delete(monoRoot, recursive: true);
            Directory.CreateDirectory(monoRoot);
            var output = Path.Combine(fixtureRoot, "empty-mono-output");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage("direct-windows", "empty-mono-output"));

            Assert.That(exception.Code, Is.EqualTo("STAGING_REQUIRED_RUNTIME_MISSING"));
            AssertPromotedOutputAbsent(output);
        }

        [Test]
        public void Il2CppBackend_WithRequiredRuntimeFiles_PromotesSuccess()
        {
            Directory.Delete(Path.Combine(sourceRoot, "MonoBleedingEdge"), recursive: true);
            WriteFile("GameAssembly.dll", "il2cpp-runtime");
            WriteFile("baselib.dll", "il2cpp-baselib");

            var result = Stage(
                "direct-windows",
                "valid-il2cpp-output",
                scriptingBackend: "IL2CPP");

            Assert.That(File.Exists(Path.Combine(result.PayloadRoot, "GameAssembly.dll")),
                Is.True);
            Assert.That(File.Exists(Path.Combine(result.PayloadRoot, "baselib.dll")),
                Is.True);
            Assert.That(File.ReadAllText(result.SuccessPath),
                Does.Contain("\"scriptingBackend\": \"IL2CPP\""));
        }

        [Test]
        public void Il2CppBackend_WithoutGameAssembly_FailsBeforePromotion()
        {
            Directory.Delete(Path.Combine(sourceRoot, "MonoBleedingEdge"), recursive: true);
            WriteFile("baselib.dll", "il2cpp-baselib");
            var output = Path.Combine(fixtureRoot, "missing-il2cpp-output");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage(
                    "direct-windows",
                    "missing-il2cpp-output",
                    scriptingBackend: "IL2CPP"));

            Assert.That(exception.Code, Is.EqualTo("STAGING_REQUIRED_RUNTIME_MISSING"));
            AssertPromotedOutputAbsent(output);
        }

        [Test]
        public void Il2CppBackend_WithoutBaselib_FailsBeforePromotion()
        {
            Directory.Delete(Path.Combine(sourceRoot, "MonoBleedingEdge"), recursive: true);
            WriteFile("GameAssembly.dll", "il2cpp-runtime");
            var output = Path.Combine(fixtureRoot, "missing-il2cpp-baselib-output");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage(
                    "direct-windows",
                    "missing-il2cpp-baselib-output",
                    scriptingBackend: "IL2CPP"));

            Assert.That(exception.Code, Is.EqualTo("STAGING_REQUIRED_RUNTIME_MISSING"));
            Assert.That(exception.Message, Does.Contain("baselib.dll"));
            AssertPromotedOutputAbsent(output);
        }

        [TestCase(null)]
        [TestCase("")]
        public void MissingBackend_FailsBeforePromotion(string scriptingBackend)
        {
            var output = Path.Combine(fixtureRoot, "missing-backend-output");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage(
                    "direct-windows",
                    "missing-backend-output",
                    scriptingBackend: scriptingBackend));

            Assert.That(exception.Code, Is.EqualTo("STAGING_BACKEND_MISSING"));
            AssertPromotedOutputAbsent(output);
        }

        [Test]
        public void UnsupportedBackend_FailsBeforePromotion()
        {
            var output = Path.Combine(fixtureRoot, "unsupported-backend-output");

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                Stage(
                    "direct-windows",
                    "unsupported-backend-output",
                    scriptingBackend: "Unknown"));

            Assert.That(exception.Code, Is.EqualTo("STAGING_BACKEND_UNSUPPORTED"));
            AssertPromotedOutputAbsent(output);
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
            Assert.That(WindowsDistributionStager.IsRuntimeIncludeCandidate(
                WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact), Is.True);
            Assert.That(WindowsDistributionStager.IsRuntimeIncludeCandidate(
                "Docs/ThirdPartyNotices.txt"), Is.False);
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

        [Test]
        public void PromotedValidation_ReusesTypedInventoryAndSteamContract()
        {
            var staged = Stage("steam-windows", "promoted-validation-output");
            var validation = WindowsDistributionStager.ValidatePromotedArtifact(
                CreatePromotedValidationRequest(staged));

            Assert.That(validation.PayloadRoot, Is.EqualTo(staged.PayloadRoot));
            Assert.That(validation.FileCount, Is.EqualTo(staged.FileCount));
            Assert.That(validation.SteamNativeCount, Is.EqualTo(1));
            Assert.That(validation.SteamManagedCount, Is.EqualTo(1));
            Assert.That(validation.SteamAppIdCount, Is.Zero);
            Assert.That(validation.DeniedArtifactCount, Is.Zero);
        }

        [Test]
        public void PromotedValidation_ManifestMismatchFailsClosed()
        {
            var staged = Stage("steam-windows", "promoted-mismatch-output");
            var request = CreatePromotedValidationRequest(staged);
            request.ManifestFiles[0].Sha256 = new string('0', 64);

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.ValidatePromotedArtifact(request));

            Assert.That(exception.Code, Is.EqualTo("STAGING_PROMOTED_MANIFEST_MISMATCH"));
        }

        [Test]
        public void PromotedValidation_ManifestPathEscapeFailsClosed()
        {
            var staged = Stage("steam-windows", "promoted-path-escape-output");
            var request = CreatePromotedValidationRequest(staged);
            request.ManifestFiles[0].RelativePath = "../escaped-file";

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.ValidatePromotedArtifact(request));

            Assert.That(exception.Code, Is.EqualTo("STAGING_PROMOTED_MANIFEST_MISMATCH"));
        }

        [Test]
        public void PromotedValidation_NonAdjacentCaseInsensitiveDuplicatePathFailsClosed()
        {
            var staged = Stage("steam-windows", "promoted-duplicate-path-output");
            var request = CreatePromotedValidationRequest(staged);
            request.ManifestFiles[0].RelativePath = "A.dll";
            request.ManifestFiles[1].RelativePath = "B.dll";
            request.ManifestFiles[2].RelativePath = "a.dll";

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.ValidatePromotedArtifact(request));

            Assert.That(exception.Code, Is.EqualTo("STAGING_PROMOTED_MANIFEST_MISMATCH"));
            Assert.That(exception.Message, Does.Contain("duplicate path"));
        }

        [Test]
        public void PromotedValidation_MatchingIncompleteRuntimeFailsClosed()
        {
            var staged = Stage("steam-windows", "promoted-missing-runtime-output");
            File.Delete(Path.Combine(staged.PayloadRoot, "UnityPlayer.dll"));
            var request = CreatePromotedValidationRequest(staged);

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.ValidatePromotedArtifact(request));

            Assert.That(exception.Code, Is.EqualTo("STAGING_REQUIRED_RUNTIME_MISSING"));
        }

        [Test]
        public void PromotedValidation_UnsupportedBackendFailsClosed()
        {
            var staged = Stage("steam-windows", "promoted-unsupported-backend-output");
            var request = CreatePromotedValidationRequest(staged);
            request.ScriptingBackend = "Unknown";

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.ValidatePromotedArtifact(request));

            Assert.That(exception.Code, Is.EqualTo("STAGING_BACKEND_UNSUPPORTED"));
        }

        [Test]
        public void PromotedValidation_DuplicateSteamNativeFailsClosed()
        {
            var staged = Stage("steam-windows", "promoted-duplicate-native-output");
            var source = Path.Combine(
                staged.PayloadRoot,
                "VectorQuake_Data",
                "Plugins",
                "x86_64",
                "steam_api64.dll");
            var duplicate = Path.Combine(staged.PayloadRoot, "steam_api64.dll");
            File.Copy(source, duplicate);
            var request = CreatePromotedValidationRequest(staged);

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.ValidatePromotedArtifact(request));

            Assert.That(exception.Code,
                Is.EqualTo("STAGING_PROMOTED_ARTIFACT_CONTRACT_FAILED"));
        }

        [Test]
        public void PromotedValidation_RawPlayerRootFailsStructureCheck()
        {
            var request = new WindowsDistributionPromotedValidationRequest
            {
                PromotedRoot = sourceRoot,
                DistributionTargetId = "steam-windows",
                ScriptingBackend = "Mono2x",
                ManifestFiles = Array.Empty<WindowsDistributionManifestFile>(),
            };

            var exception = Assert.Throws<WindowsDistributionStagingException>(() =>
                WindowsDistributionStager.ValidatePromotedArtifact(request));

            Assert.That(exception.Code, Is.EqualTo("STAGING_PROMOTED_STRUCTURE_INVALID"));
        }

        [Test]
        public void PrePromotionValidationFailure_AfterEvidenceWrite_PreventsFinalPromotion()
        {
            const string outputName = "pre-promotion-drift-output";
            const string runId = "pre-promotion-drift-run";
            var output = Path.Combine(fixtureRoot, outputName);
            var temporaryRoot = Path.Combine(
                fixtureRoot,
                ".staging-" + outputName + "-" + runId);
            var temporaryEvidenceRoot = Path.Combine(temporaryRoot, "evidence");
            var observedManifest = false;
            var observedSuccess = false;
            var request = CreateRequest("direct-windows", output, runId);
            request.PrePromotionValidation = () =>
            {
                observedManifest = File.Exists(Path.Combine(
                    temporaryEvidenceRoot,
                    WindowsDistributionStager.ManifestFileName));
                observedSuccess = File.Exists(Path.Combine(
                    temporaryEvidenceRoot,
                    WindowsDistributionStager.SuccessFileName));
                throw new InvalidOperationException("synthetic repository drift");
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                WindowsDistributionStager.Stage(request));

            Assert.That(exception.Message, Does.Contain("synthetic repository drift"));
            Assert.That(observedManifest, Is.True);
            Assert.That(observedSuccess, Is.True);
            AssertPromotedOutputAbsent(output);
            Assert.That(Directory.Exists(temporaryRoot), Is.False);
        }

        [Test]
        public void LongNestedRuntimePath_IsCopiedHashedAndManifested()
        {
            var segment = new string('a', 90);
            var relative = "VectorQuake_Data/StreamingAssets/aa/" +
                segment + "/" + segment + "/content.bundle";
            WriteFileExtended(relative, "long-runtime-content");

            var result = Stage("direct-windows", "long-path-output");
            var destination = Path.Combine(
                result.PayloadRoot,
                relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.That(File.Exists(ToExtendedPath(destination)), Is.True);
            Assert.That(File.ReadAllText(result.ManifestPath), Does.Contain(relative));
        }

        private WindowsDistributionStagingResult Stage(
            string target,
            string outputName,
            string runId = "fixture-run",
            string scriptingBackend = "Mono2x")
        {
            return WindowsDistributionStager.Stage(CreateRequest(
                target,
                Path.Combine(fixtureRoot, outputName),
                runId,
                scriptingBackend));
        }

        private WindowsDistributionStagingRequest CreateRequest(
            string target,
            string output,
            string runId,
            string scriptingBackend = "Mono2x")
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
                ScriptingBackend = scriptingBackend,
            };
        }

        private static WindowsDistributionPromotedValidationRequest
            CreatePromotedValidationRequest(WindowsDistributionStagingResult staged)
        {
            var files = Directory.GetFiles(
                    staged.PayloadRoot,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .Select(info => new WindowsDistributionManifestFile
                {
                    RelativePath = NormalizeRelative(staged.PayloadRoot, info.FullName),
                    Size = info.Length,
                    Sha256 = GetSha256(info.FullName),
                })
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToArray();
            return new WindowsDistributionPromotedValidationRequest
            {
                PromotedRoot = staged.OutputRoot,
                DistributionTargetId = staged.DistributionTargetId,
                ScriptingBackend = "Mono2x",
                ManifestFiles = files,
                ManifestDeniedArtifactCount = 0,
                ManifestFileCount = files.Length,
                ManifestTotalBytes = files.Sum(file => file.Size),
            };
        }

        private void WriteCanonicalRawFixture(bool includeSteamDependencies)
        {
            WriteFile("VectorQuake.exe", "exe");
            WriteFile(
                WindowsDistributionTargetPolicy.ThirdPartyNoticesArtifact,
                ValidThirdPartyNotices);
            WriteFile("UnityPlayer.dll", "unity");
            WriteFile("UnityCrashHandler64.exe", "crash-handler");
            WriteFile("VectorQuake_Data/globalgamemanagers", "managers");
            WriteFile("VectorQuake_Data/Managed/Game.dll", "game-managed");
            WriteFile("MonoBleedingEdge/etc/mono/config", "mono-runtime");
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

        private void WriteFileExtended(string relativePath, string content)
        {
            var path = Path.Combine(
                sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(ToExtendedPath(Path.GetDirectoryName(path)));
            File.WriteAllText(ToExtendedPath(path), content);
        }

        private static void AssertPromotedOutputAbsent(string outputRoot)
        {
            Assert.That(Directory.Exists(outputRoot), Is.False);
            Assert.That(File.Exists(Path.Combine(
                outputRoot, "evidence", WindowsDistributionStager.SuccessFileName)), Is.False);
        }

        private static string ToExtendedPath(string path)
        {
            var full = Path.GetFullPath(path);
            return full.StartsWith(@"\\", StringComparison.Ordinal)
                ? @"\\?\UNC\" + full.Substring(2)
                : @"\\?\" + full;
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
