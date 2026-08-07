using System;
using Game.Platform.Runtime;
using NUnit.Framework;

namespace Game.Platform.Tests.EditMode
{
    public sealed class PlatformProviderSelectionTests
    {
        [Test]
        public void NoArgument_IsNoExplicitSelection()
        {
            var request = PlatformProviderSelection.ParseArguments(Array.Empty<string>());

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.None));
            Assert.That(request.HasRequestedProviderId, Is.False);
            Assert.That(request.Source, Is.EqualTo(PlatformProviderSelection.CommandLineSource));
        }

        [Test]
        public void ExactOptInFlag_IsDefaultOffAndMatchesOnlyExactToken()
        {
            const string flag = "-j2mOptionalProbe";

            Assert.That(
                PlatformProviderSelection.HasExactOptInFlag(Array.Empty<string>(), flag),
                Is.False);
            Assert.That(
                PlatformProviderSelection.HasExactOptInFlag(
                    new[] { "player.exe", flag },
                    flag),
                Is.True);
            Assert.That(
                PlatformProviderSelection.HasExactOptInFlag(
                    new[] { flag + "=true", flag + "Extra" },
                    flag),
                Is.False);
        }

        [TestCase("local")]
        [TestCase("steam")]
        [TestCase("unknown-store")]
        public void ExplicitProvider_IsPreserved(string providerId)
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                "player.exe",
                PlatformProviderSelection.ProviderSelectionArgument,
                providerId,
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Explicit));
            Assert.That(request.RequestedProviderId.Value, Is.EqualTo(providerId));
            Assert.That(request.FailureReason, Is.Empty);
        }

        [Test]
        public void EqualsForm_RemainsSupported()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument + "=steam",
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Explicit));
            Assert.That(request.RequestedProviderId.Value, Is.EqualTo("steam"));
        }

        [TestCase(" Steam ", "steam")]
        [TestCase("LOCAL", "local")]
        [TestCase(" Other-Store ", "other-store")]
        public void ProviderId_IsTrimmedAndNormalizedToCanonicalCase(
            string rawProviderId,
            string expectedProviderId)
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                rawProviderId,
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Explicit));
            Assert.That(request.RequestedProviderId.Value, Is.EqualTo(expectedProviderId));
        }

        [Test]
        public void SingleSelector_MissingValue_IsInvalid()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Invalid));
            Assert.That(request.HasRequestedProviderId, Is.False);
            Assert.That(request.FailureReason, Does.Contain("requires a provider ID value"));
        }

        [TestCase("-batchmode")]
        [TestCase("--nographics")]
        public void SingleSelector_FollowedBySwitch_IsInvalid(
            string option)
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                option,
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Invalid));
            Assert.That(request.HasRequestedProviderId, Is.False);
            Assert.That(request.FailureReason, Does.Contain("requires a provider ID value"));
        }

        [Test]
        public void ValidProviderBeforeCommandLineOption_PreservesProviderValue()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
                "-batchmode",
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Explicit));
            Assert.That(request.RequestedProviderId.Value, Is.EqualTo("steam"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void EmptyValue_IsInvalid(string value)
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                value,
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Invalid));
        }

        [Test]
        public void InvalidProviderToken_IsInvalidWithoutRetainingRawInput()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "store provider",
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Invalid));
            Assert.That(request.HasRequestedProviderId, Is.False);
            Assert.That(request.FailureReason, Does.Not.Contain("store provider"));
        }

        [Test]
        public void RepeatedSelectors_ValidAndValid_AreConflicting()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
                PlatformProviderSelection.ProviderSelectionArgument + "=steam",
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Conflicting));
            Assert.That(request.HasRequestedProviderId, Is.False);
        }

        [Test]
        public void RepeatedSelectors_WithDifferentValidValues_AreConflicting()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
                PlatformProviderSelection.ProviderSelectionArgument,
                "local",
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Conflicting));
            Assert.That(request.FailureReason, Does.Contain("Multiple explicit"));
        }

        [Test]
        public void RepeatedSelectors_ValidAndMissing_AreConflicting()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
                PlatformProviderSelection.ProviderSelectionArgument,
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Conflicting));
        }

        [Test]
        public void RepeatedSelectors_MissingAndValid_AreConflicting()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Conflicting));
        }

        [Test]
        public void RepeatedSelectors_MissingAndMissing_AreConflicting()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                PlatformProviderSelection.ProviderSelectionArgument,
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Conflicting));
        }

        [Test]
        public void RepeatedSelectors_ValidAndFollowingSwitch_AreConflicting()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
                PlatformProviderSelection.ProviderSelectionArgument,
                "-batchmode",
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Conflicting));
        }

        [Test]
        public void RepeatedSelectors_FollowingSwitchAndValid_AreConflicting()
        {
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "-batchmode",
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
            });

            Assert.That(request.Kind, Is.EqualTo(PlatformProviderSelectionKind.Conflicting));
        }

        [Test]
        public void Reset_ReplacesPreviousSelectionWithoutStaleIntent()
        {
            PlatformProviderSelection.ResetFromArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "steam",
            });
            Assert.That(PlatformProviderSelection.CurrentRequest.HasRequestedProviderId, Is.True);

            PlatformProviderSelection.ResetFromArguments(Array.Empty<string>());

            Assert.That(
                PlatformProviderSelection.CurrentRequest.Kind,
                Is.EqualTo(PlatformProviderSelectionKind.None));
            Assert.That(
                PlatformProviderSelection.CurrentRequest.HasRequestedProviderId,
                Is.False);
        }
    }
}
