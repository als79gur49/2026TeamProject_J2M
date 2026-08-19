using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SceneTransitionRoutePolicyCatalogTests
    {
        private static readonly SceneTransitionIntent[] ExpectedProductionIntents =
        {
            SceneTransitionIntent.StageAdvance,
            SceneTransitionIntent.DeathRetry,
            SceneTransitionIntent.ManualRetry,
            SceneTransitionIntent.DemoStageRelaunch,
            SceneTransitionIntent.GameplayEntry,
            SceneTransitionIntent.ReturnToMainMenu,
            SceneTransitionIntent.ComicIntroToGameplay,
            SceneTransitionIntent.ComicOutroToMainMenu,
        };

        [Test]
        public void ProductionManifest_ContainsExactReachableRouteSet()
        {
            var actual = SceneTransitionRoutePolicyCatalog.All
                .Where(policy => policy.Classification == SceneTransitionRouteClassification.Production)
                .Select(policy => policy.Intent)
                .OrderBy(intent => intent)
                .ToArray();

            Assert.That(actual, Has.Length.EqualTo(8));
            Assert.That(
                actual,
                Is.EqualTo(ExpectedProductionIntents.OrderBy(intent => intent).ToArray()));
            Assert.That(actual.Distinct().Count(), Is.EqualTo(actual.Length));
        }

        [Test]
        public void ProductionIntentEnumSubset_EqualsIndependentExpectedSet()
        {
            var actual = Enum.GetValues(typeof(SceneTransitionIntent))
                .Cast<SceneTransitionIntent>()
                .Where(intent => (int)intent > 0 && (int)intent < 100)
                .OrderBy(intent => intent)
                .ToArray();

            Assert.That(actual, Has.Length.EqualTo(8));
            Assert.That(
                actual,
                Is.EqualTo(ExpectedProductionIntents.OrderBy(intent => intent).ToArray()));
        }

        [TestCase(SceneTransitionIntent.StageAdvance, SceneTransitionDestinationKind.Gameplay, StageTransitionKind.StageClearNext, StageTransitionKind.Unknown)]
        [TestCase(SceneTransitionIntent.DeathRetry, SceneTransitionDestinationKind.Gameplay, StageTransitionKind.DeathRetryChanceLost, StageTransitionKind.Unknown)]
        [TestCase(SceneTransitionIntent.ManualRetry, SceneTransitionDestinationKind.Gameplay, StageTransitionKind.StageRetryManual, StageTransitionKind.LevelFailedRestart)]
        [TestCase(SceneTransitionIntent.DemoStageRelaunch, SceneTransitionDestinationKind.Gameplay, StageTransitionKind.StageRetryManual, StageTransitionKind.Unknown)]
        [TestCase(SceneTransitionIntent.GameplayEntry, SceneTransitionDestinationKind.Gameplay, StageTransitionKind.MainToGameplay, StageTransitionKind.Unknown)]
        [TestCase(SceneTransitionIntent.ReturnToMainMenu, SceneTransitionDestinationKind.MainMenu, StageTransitionKind.GameplayToMain, StageTransitionKind.Unknown)]
        [TestCase(SceneTransitionIntent.ComicIntroToGameplay, SceneTransitionDestinationKind.Gameplay, StageTransitionKind.MainToGameplay, StageTransitionKind.Unknown)]
        [TestCase(SceneTransitionIntent.ComicOutroToMainMenu, SceneTransitionDestinationKind.MainMenu, StageTransitionKind.GameplayToMain, StageTransitionKind.Unknown)]
        public void ProductionManifest_MapsEachIntentToExactDestinationAndProfile(
            SceneTransitionIntent intent,
            SceneTransitionDestinationKind destination,
            StageTransitionKind primaryProfile,
            StageTransitionKind secondaryProfile)
        {
            var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(intent);

            Assert.That(policy.DestinationKind, Is.EqualTo(destination));
            Assert.That(policy.PrimaryTransitionKind, Is.EqualTo(primaryProfile));
            Assert.That(policy.SecondaryTransitionKind, Is.EqualTo(secondaryProfile));
            Assert.That(
                policy.DestinationExecutorKind,
                Is.EqualTo(
                    destination == SceneTransitionDestinationKind.Gameplay
                        ? SceneTransitionDestinationExecutorKind.GameplayEntry
                        : SceneTransitionDestinationExecutorKind.MainMenuEntry));
        }

        [Test]
        public void PublicRuntimeApi_HasNoLongClaimCompatibilityOverloads()
        {
            var forbiddenNames = new[]
            {
                "IsReady",
                "Signal",
                "RequestReveal",
                "CompleteHandoff",
                "Cancel",
            };
            var runtimeTypes = new[]
            {
                typeof(TerminalDestinationReadiness),
                typeof(TerminalTransitionPlayback),
            };

            var forbidden = runtimeTypes
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly))
                .Where(method => forbiddenNames.Contains(method.Name))
                .Where(method => method.GetParameters().Any(
                    parameter => parameter.ParameterType == typeof(long)))
                .Select(method => $"{method.DeclaringType?.FullName}.{method}")
                .ToArray();

            Assert.That(forbidden, Is.Empty);
        }

        [Test]
        public void ProductionManifest_HasNoLegacyStatusOrAllowlistSurface()
        {
            var statusNames = Enum.GetNames(typeof(SceneTransitionRouteStatus));
            Assert.That(statusNames, Does.Not.Contain("LegacyPendingMigration"));
            Assert.That(
                typeof(SceneTransitionRoutePolicyCatalog).GetProperty(
                    "KnownLegacyProductionRoutes"),
                Is.Null);
            Assert.That(
                SceneTransitionRoutePolicyCatalog.All
                    .Where(policy => policy.Classification == SceneTransitionRouteClassification.Production)
                    .All(policy => policy.Status == SceneTransitionRouteStatus.Canonical),
                Is.True);
        }

        [TestCase(SceneTransitionIntent.ReturnToMainMenu, SceneTransitionDestinationKind.MainMenu, false)]
        [TestCase(SceneTransitionIntent.ComicIntroToGameplay, SceneTransitionDestinationKind.Gameplay, true)]
        [TestCase(SceneTransitionIntent.ComicOutroToMainMenu, SceneTransitionDestinationKind.MainMenu, true)]
        public void M4Routes_DeclareCompleteCanonicalDestinationLifecycle(
            SceneTransitionIntent intent,
            SceneTransitionDestinationKind destinationKind,
            bool requiresOpaqueOwnerTransfer)
        {
            var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(intent);

            Assert.That(policy.Status, Is.EqualTo(SceneTransitionRouteStatus.Canonical));
            Assert.That(policy.DestinationKind, Is.EqualTo(destinationKind));
            Assert.That(policy.ImplementsSceneTransitionSession, Is.True);
            Assert.That(policy.ImplementsDestinationReadiness, Is.True);
            Assert.That(policy.ImplementsDestinationRenderAcknowledgement, Is.True);
            Assert.That(policy.ImplementsInputAdmission, Is.True);
            Assert.That(
                policy.TargetRequiresOpaqueOwnerTransfer,
                Is.EqualTo(requiresOpaqueOwnerTransfer));
            Assert.That(
                policy.ImplementsOpaqueOwnerTransfer,
                Is.EqualTo(requiresOpaqueOwnerTransfer));
            Assert.That(policy.MigrationMilestone, Does.Contain("M4"));
        }

        [Test]
        public void ProductionManifest_DeclaresDestinationLifecycleDebtWithoutHidingIt()
        {
            foreach (var policy in SceneTransitionRoutePolicyCatalog.All.Where(
                         policy => policy.Classification == SceneTransitionRouteClassification.Production))
            {
                Assert.That(policy.DestinationKind, Is.Not.EqualTo(SceneTransitionDestinationKind.Unknown), policy.Intent.ToString());
                Assert.That(policy.Status, Is.Not.EqualTo(SceneTransitionRouteStatus.Unknown), policy.Intent.ToString());
                Assert.That(policy.Owner, Is.Not.Empty, policy.Intent.ToString());
                Assert.That(policy.CurrentExecution, Is.Not.Empty, policy.Intent.ToString());
                Assert.That(policy.MigrationMilestone, Is.Not.Empty, policy.Intent.ToString());

                Assert.That(policy.Status, Is.EqualTo(SceneTransitionRouteStatus.Canonical), policy.Intent.ToString());
            }

            var gameplayPolicies = SceneTransitionRoutePolicyCatalog.All.Where(
                policy =>
                    policy.Classification == SceneTransitionRouteClassification.Production &&
                    policy.DestinationKind == SceneTransitionDestinationKind.Gameplay);
            foreach (var policy in gameplayPolicies)
            {
                Assert.That(policy.TargetRequiresSceneTransitionSession, Is.True, policy.Intent.ToString());
                Assert.That(policy.TargetRequiresDestinationReadiness, Is.True, policy.Intent.ToString());
                Assert.That(policy.TargetRequiresDestinationRenderAcknowledgement, Is.True, policy.Intent.ToString());
                Assert.That(policy.TargetRequiresInputAdmission, Is.True, policy.Intent.ToString());

                Assert.That(policy.ImplementsSceneTransitionSession, Is.True, policy.Intent.ToString());
                Assert.That(policy.ImplementsDestinationReadiness, Is.True, policy.Intent.ToString());
                Assert.That(policy.ImplementsDestinationRenderAcknowledgement, Is.True, policy.Intent.ToString());
                Assert.That(policy.ImplementsInputAdmission, Is.True, policy.Intent.ToString());
            }
        }

        [TestCase(SceneTransitionIntent.DeathRetry)]
        [TestCase(SceneTransitionIntent.ManualRetry)]
        [TestCase(SceneTransitionIntent.DemoStageRelaunch)]
        public void M2RetryRoutes_DeclareCompleteCanonicalGameplayEntryLifecycle(
            SceneTransitionIntent intent)
        {
            var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(intent);

            Assert.That(policy.Status, Is.EqualTo(SceneTransitionRouteStatus.Canonical));
            Assert.That(policy.DestinationKind, Is.EqualTo(SceneTransitionDestinationKind.Gameplay));
            Assert.That(policy.ImplementsSceneTransitionSession, Is.True);
            Assert.That(policy.ImplementsDestinationReadiness, Is.True);
            Assert.That(policy.ImplementsDestinationRenderAcknowledgement, Is.True);
            Assert.That(policy.ImplementsInputAdmission, Is.True);
            Assert.That(policy.MigrationMilestone, Does.Contain("M2"));
        }

        [Test]
        public void M3GameplayEntry_DeclaresCompleteCanonicalGameplayEntryLifecycle()
        {
            var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(
                SceneTransitionIntent.GameplayEntry);

            Assert.That(policy.Status, Is.EqualTo(SceneTransitionRouteStatus.Canonical));
            Assert.That(
                policy.DestinationKind,
                Is.EqualTo(SceneTransitionDestinationKind.Gameplay));
            Assert.That(policy.ImplementsSceneTransitionSession, Is.True);
            Assert.That(policy.ImplementsDestinationReadiness, Is.True);
            Assert.That(policy.ImplementsDestinationRenderAcknowledgement, Is.True);
            Assert.That(policy.ImplementsInputAdmission, Is.True);
            Assert.That(policy.MigrationMilestone, Does.Contain("M3"));
        }

        [Test]
        public void ResolveProduction_RejectsUnknownDefaultAndExceptionRoutes()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SceneTransitionRoutePolicyCatalog.ResolveProduction(SceneTransitionIntent.Unknown));
            Assert.Throws<InvalidOperationException>(() =>
                SceneTransitionRoutePolicyCatalog.ResolveProduction(SceneTransitionIntent.EditorDirectSceneLoad));
            Assert.Throws<InvalidOperationException>(() =>
                SceneTransitionRoutePolicyCatalog.ResolveProduction(SceneTransitionIntent.TestInjectedSceneLoad));
        }

        [Test]
        public void ResolveException_RequiresExactClassification()
        {
            var editor = SceneTransitionRoutePolicyCatalog.ResolveException(
                SceneTransitionIntent.EditorDirectSceneLoad,
                SceneTransitionRouteClassification.EditorOnly);
            var test = SceneTransitionRoutePolicyCatalog.ResolveException(
                SceneTransitionIntent.TestInjectedSceneLoad,
                SceneTransitionRouteClassification.TestOnly);

            Assert.That(editor.Status, Is.EqualTo(SceneTransitionRouteStatus.EditorOnlyException));
            Assert.That(test.Status, Is.EqualTo(SceneTransitionRouteStatus.TestOnlyException));
            Assert.Throws<InvalidOperationException>(() =>
                SceneTransitionRoutePolicyCatalog.ResolveException(
                    SceneTransitionIntent.EditorDirectSceneLoad,
                    SceneTransitionRouteClassification.TestOnly));
        }

        [TestCase(SceneTransitionIntent.StageAdvance, SceneTransitionDestinationKind.Gameplay, SceneTransitionRouteStatus.Canonical)]
        [TestCase(SceneTransitionIntent.DeathRetry, SceneTransitionDestinationKind.Gameplay, SceneTransitionRouteStatus.Canonical)]
        [TestCase(SceneTransitionIntent.ManualRetry, SceneTransitionDestinationKind.Gameplay, SceneTransitionRouteStatus.Canonical)]
        [TestCase(SceneTransitionIntent.GameplayEntry, SceneTransitionDestinationKind.Gameplay, SceneTransitionRouteStatus.Canonical)]
        [TestCase(SceneTransitionIntent.ReturnToMainMenu, SceneTransitionDestinationKind.MainMenu, SceneTransitionRouteStatus.Canonical)]
        [TestCase(SceneTransitionIntent.ComicIntroToGameplay, SceneTransitionDestinationKind.Gameplay, SceneTransitionRouteStatus.Canonical)]
        [TestCase(SceneTransitionIntent.ComicOutroToMainMenu, SceneTransitionDestinationKind.MainMenu, SceneTransitionRouteStatus.Canonical)]
        [TestCase(SceneTransitionIntent.DemoStageRelaunch, SceneTransitionDestinationKind.Gameplay, SceneTransitionRouteStatus.Canonical)]
        public void ResolveProduction_ReturnsDeclaredRoute(
            SceneTransitionIntent intent,
            SceneTransitionDestinationKind destinationKind,
            SceneTransitionRouteStatus status)
        {
            var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(intent);

            Assert.That(policy.Intent, Is.EqualTo(intent));
            Assert.That(policy.DestinationKind, Is.EqualTo(destinationKind));
            Assert.That(policy.Status, Is.EqualTo(status));
        }

        [Test]
        public void Diagnostic_ContainsRequiredRouteTracePayload()
        {
            var policy =
                SceneTransitionRoutePolicyCatalog.ResolveProduction(SceneTransitionIntent.ManualRetry);

            var diagnostic = SceneTransitionRouteDiagnostic.Format(
                policy,
                "pause-retry",
                StageTransitionKind.StageRetryManual);

            Assert.That(diagnostic, Does.Contain("Intent=ManualRetry"));
            Assert.That(diagnostic, Does.Contain("Destination=Gameplay"));
            Assert.That(diagnostic, Does.Contain("Status=Canonical"));
            Assert.That(diagnostic, Does.Contain("Source=pause-retry"));
            Assert.That(diagnostic, Does.Contain("Profile=StageRetryManual"));
            Assert.That(diagnostic, Does.Contain("Migration=M2"));
        }
    }
}
