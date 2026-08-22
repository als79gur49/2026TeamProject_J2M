using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessReportWordingTests
    {
        private const string CiGuidePath = "Docs/Testing/Save-Readiness-CI-Guide.md";

        [Test]
        public void ReportMarkdown_UsesDiagnosticsReadinessOnlyWording()
        {
            using var harness = new ProfileHarness();
            harness.WriteProfile(CreateProfile());

            var markdown = new CampaignProfileReadinessReportBuilder()
                .Build(new CampaignProfileReadinessReportOptions(harness.SaveRootPath))
                .ToMarkdown();

            Assert.That(markdown, Does.Contain("Editor-only diagnostics/readiness report"));
            Assert.That(markdown, Does.Contain("Report findings are diagnostics/readiness-only."));
            Assert.That(markdown, Does.Contain("Report findings do not block build or release."));
            Assert.That(markdown, Does.Contain("profile.json is the production campaign progression save truth"));
            Assert.That(markdown, Does.Contain("Current production UX truth uses the profile-backed campaign save provider"));
            Assert.That(markdown, Does.Contain("metadata load status"));
            Assert.That(markdown, Does.Contain("diagnostic LastPlayedSlotNumber"));
            Assert.That(markdown, Does.Contain("Profile metadata is readiness inventory"));
            Assert.That(markdown, Does.Contain("not used for pending launch selection"));
        }

        [Test]
        public void ReportMarkdown_DoesNotDescribeProfileAsPendingLaunchTruth()
        {
            var markdown = MissingProfileMarkdown();

            AssertForbiddenWordingAbsent(
                markdown,
                "profile slot list is MainMenu slot list",
                "profile slot document count is MainMenu slot list",
                "ImportDisabled blocks current PlayerPrefs UX",
                "DeletedSlotGuards hide current PlayerPrefs slots");
        }

        [Test]
        public void ReportMarkdown_DoesNotDescribeLastPlayedAsResumeQuickContinueOrDefaultFocus()
        {
            var markdown = MissingProfileMarkdown();

            AssertForbiddenWordingAbsent(
                markdown,
                "LastPlayedSlotNumber is resume target",
                "LastPlayedSlotNumber is Quick Continue target",
                "LastPlayedSlotNumber is default focus target",
                "Quick Continue target",
                "default focus target");
        }

        [Test]
        public void ReportMarkdown_DoesNotDescribeSteamCloudCanonicalSource()
        {
            var markdown = MissingProfileMarkdown();

            AssertForbiddenWordingAbsent(
                markdown,
                "Steam Cloud upload source",
                "Steam Cloud canonical source",
                "Steam Cloud canonical file",
                "Steam Cloud canonical file selection",
                "ISteamRemoteStorage",
                "SteamRemoteStorage");
        }

        [Test]
        public void CiGuide_DoesNotPromoteWarningsOrSteamCloudFileSelection()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("This phase does not:"));
            Assert.That(guide, Does.Contain("Promote report warnings to release or build gates."));
            Assert.That(guide, Does.Contain("Decide Steam Cloud upload/source file selection."));
            Assert.That(guide, Does.Contain("Use the report as a Steam Cloud canonical source."));
            Assert.That(
                guide,
                Does.Contain("Profile missing, corrupt, stale, mismatch, `LastPlayedSlotNumber` mismatch, or `importedSourceHash` mismatch is not a release blocker."));
            Assert.That(guide, Does.Not.Contain("Steam Cloud canonical file"));
            Assert.That(guide, Does.Not.Contain("ISteamRemoteStorage"));
            Assert.That(guide, Does.Not.Contain("SteamRemoteStorage"));
            Assert.That(guide, Does.Not.Contain("build blocker"));
            Assert.That(guide, Does.Not.Contain("blocks release"));
            Assert.That(guide, Does.Not.Contain("must fix before release"));
        }

        private static string MissingProfileMarkdown()
        {
            using var harness = new ProfileHarness();
            return new CampaignProfileReadinessReportBuilder()
                .Build(new CampaignProfileReadinessReportOptions(harness.SaveRootPath))
                .ToMarkdown();
        }

        private static CampaignProfileDocument CreateProfile()
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = "tests",
                SavedAtUtc = "2026-07-08T00:00:00.0000000Z",
                ProfileId = "profile-tests",
                LastPlayedSlotNumber = 3,
                LegacyImport = new CampaignLegacyImportDocument(),
                Slots = Array.Empty<CampaignSlotDocument>(),
            };
        }

        private static void AssertForbiddenWordingAbsent(string markdown, params string[] forbiddenPhrases)
        {
            for (var i = 0; i < forbiddenPhrases.Length; i++)
            {
                Assert.That(markdown, Does.Not.Contain(forbiddenPhrases[i]), forbiddenPhrases[i]);
            }
        }

        private sealed class ProfileHarness : IDisposable
        {
            private readonly string _testRootPath;

            public ProfileHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignProfileReadinessReportWordingTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
            }

            public string SaveRootPath { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, CampaignProfileMetadataProbe.ProfileFileName);

            public void WriteProfile(CampaignProfileDocument document)
            {
                Directory.CreateDirectory(SaveRootPath);
                File.WriteAllText(ProfilePath, JsonUtility.ToJson(document));
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_testRootPath))
                    {
                        Directory.Delete(_testRootPath, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
