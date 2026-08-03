using System;
using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Platform.Tests.EditMode
{
    public sealed class PlatformRuntimeRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetRegistryAndSelection();
        }

        [TearDown]
        public void TearDown()
        {
            ResetRegistryAndSelection();
        }

        [Test]
        public void NoExplicitRequest_SelectsDefaultLocal()
        {
            PlatformRuntimeRegistry.Seal();

            var selection = PlatformRuntimeRegistry.Select();

            Assert.That(selection.IsSuccess, Is.True);
            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.DefaultLocalSelected));
            Assert.That(selection.SelectionKind,
                Is.EqualTo(PlatformProviderSelectionKind.None));
            Assert.That(selection.SelectedProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(selection.Runtime, Is.TypeOf<LocalPlatformRuntime>());
            Assert.That(selection.FallbackUsed, Is.True);
            Assert.That(selection.HasRequestedProviderId, Is.False);
        }

        [Test]
        public void NoExplicitRequest_IgnoresRegisteredFactoriesAndKeepsLocalDefault()
        {
            var runtime = new FakePlatformRuntime("available-provider");
            var factory = new FakePlatformRuntimeFactory("available-provider", () => runtime);
            PlatformRuntimeRegistry.RegisterFactory(factory);
            PlatformRuntimeRegistry.Seal();

            var selection = PlatformRuntimeRegistry.Select();

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.DefaultLocalSelected));
            Assert.That(selection.Runtime, Is.TypeOf<LocalPlatformRuntime>());
            Assert.That(selection.RegisteredProviderCount, Is.EqualTo(1));
            Assert.That(factory.CreateCount, Is.Zero);
        }

        [Test]
        public void ExplicitLocal_SelectsLocalWithoutFallback()
        {
            var request = ExplicitRequest("local");
            PlatformRuntimeRegistry.Seal();

            var selection = PlatformRuntimeRegistry.Select(request);

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ExplicitProviderSelected));
            Assert.That(selection.SelectionKind,
                Is.EqualTo(PlatformProviderSelectionKind.Explicit));
            Assert.That(selection.RequestedProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(selection.SelectedProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(selection.Runtime, Is.TypeOf<LocalPlatformRuntime>());
            Assert.That(selection.RequestedProviderRegistered, Is.True);
            Assert.That(selection.FallbackUsed, Is.False);
        }

        [Test]
        public void ExplicitProvider_SelectsMatchingFactoryExactlyOnce()
        {
            var runtime = new FakePlatformRuntime("test-provider");
            var factory = new FakePlatformRuntimeFactory("test-provider", () => runtime);
            PlatformRuntimeRegistry.RegisterFactory(factory);
            PlatformRuntimeRegistry.Seal();
            var request = ExplicitRequest("test-provider");

            var first = PlatformRuntimeRegistry.Select(request);
            var second = PlatformRuntimeRegistry.Select(request);

            Assert.That(first.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ExplicitProviderSelected));
            Assert.That(first.Runtime, Is.SameAs(runtime));
            Assert.That(first.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId("test-provider")));
            Assert.That(first.SelectedProviderId,
                Is.EqualTo(new PlatformProviderId("test-provider")));
            Assert.That(first.RequestedProviderRegistered, Is.True);
            Assert.That(first.FallbackUsed, Is.False);
            Assert.That(second.Runtime, Is.SameAs(runtime));
            Assert.That(factory.CreateCount, Is.EqualTo(1));
        }

        [Test]
        public void MultipleFactories_SelectsOnlyExplicitMatch()
        {
            var requestedRuntime = new FakePlatformRuntime("requested");
            var requestedFactory = new FakePlatformRuntimeFactory(
                "requested",
                () => requestedRuntime);
            var otherFactory = new FakePlatformRuntimeFactory(
                "other",
                () => new FakePlatformRuntime("other"));
            PlatformRuntimeRegistry.RegisterFactory(otherFactory);
            PlatformRuntimeRegistry.RegisterFactory(requestedFactory);
            PlatformRuntimeRegistry.Seal();

            var selection = PlatformRuntimeRegistry.Select(ExplicitRequest("requested"));

            Assert.That(selection.IsSuccess, Is.True);
            Assert.That(selection.Runtime, Is.SameAs(requestedRuntime));
            Assert.That(selection.RegisteredProviderCount, Is.EqualTo(2));
            Assert.That(requestedFactory.CreateCount, Is.EqualTo(1));
            Assert.That(otherFactory.CreateCount, Is.Zero);
        }

        [TestCase("steam")]
        [TestCase("unknown-store")]
        public void ExplicitUnregisteredProvider_FailsWithoutLocalMasking(string providerId)
        {
            var localFactory = new FakePlatformRuntimeFactory(
                "local",
                () => new FakePlatformRuntime("local"));
            PlatformRuntimeRegistry.RegisterFactory(localFactory);
            PlatformRuntimeRegistry.Seal();
            LogAssert.Expect(
                LogType.Error,
                "Requested platform provider '" + providerId +
                "' is not registered. No fallback provider was selected because the request was explicit.");

            var selection = PlatformRuntimeRegistry.Select(ExplicitRequest(providerId));

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderNotRegistered));
            Assert.That(selection.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId(providerId)));
            Assert.That(selection.HasSelectedProviderId, Is.False);
            Assert.That(selection.Runtime, Is.Null);
            Assert.That(selection.RequestedProviderRegistered, Is.False);
            Assert.That(selection.FallbackUsed, Is.False);
            Assert.That(localFactory.CreateCount, Is.Zero);
        }

        [Test]
        public void InvalidSelection_FailsBeforeFactoryCreation()
        {
            var factory = RegisterCountingFactory("available");
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
            });
            PlatformRuntimeRegistry.Seal();
            LogAssert.Expect(
                LogType.Error,
                PlatformProviderSelection.ProviderSelectionArgument +
                " requires a provider ID value.");

            var selection = PlatformRuntimeRegistry.Select(request);

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.InvalidProviderSelection));
            Assert.That(selection.Runtime, Is.Null);
            Assert.That(selection.FallbackUsed, Is.False);
            Assert.That(factory.CreateCount, Is.Zero);
        }

        [Test]
        public void ConflictingSelection_FailsBeforeFactoryCreation()
        {
            var factory = RegisterCountingFactory("available");
            var request = PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                "available",
                PlatformProviderSelection.ProviderSelectionArgument,
                "local",
            });
            PlatformRuntimeRegistry.Seal();
            LogAssert.Expect(
                LogType.Error,
                "Multiple explicit platform provider selections were supplied.");

            var selection = PlatformRuntimeRegistry.Select(request);

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ConflictingProviderSelection));
            Assert.That(selection.Runtime, Is.Null);
            Assert.That(selection.FallbackUsed, Is.False);
            Assert.That(factory.CreateCount, Is.Zero);
        }

        [Test]
        public void DuplicateProvider_IsRejectedWithoutReplacement()
        {
            var firstRuntime = new FakePlatformRuntime("duplicate");
            var first = new FakePlatformRuntimeFactory("duplicate", () => firstRuntime);
            var second = new FakePlatformRuntimeFactory(
                "duplicate",
                () => new FakePlatformRuntime("duplicate"));
            Assert.That(PlatformRuntimeRegistry.RegisterFactory(first).IsSuccess, Is.True);
            LogAssert.Expect(
                LogType.Error,
                "Duplicate platform runtime factory registration for provider 'duplicate'.");

            var duplicate = PlatformRuntimeRegistry.RegisterFactory(second);
            PlatformRuntimeRegistry.Seal();
            var selection = PlatformRuntimeRegistry.Select(ExplicitRequest("duplicate"));

            Assert.That(duplicate.Status,
                Is.EqualTo(PlatformRuntimeRegistrationStatus.DuplicateProvider));
            Assert.That(selection.Runtime, Is.SameAs(firstRuntime));
            Assert.That(second.CreateCount, Is.Zero);
        }

        [Test]
        public void RegistrationAfterSeal_IsRejected()
        {
            PlatformRuntimeRegistry.Seal();
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime factory registration is closed after registry seal.");

            var result = PlatformRuntimeRegistry.RegisterFactory(
                new FakePlatformRuntimeFactory("late", () => new FakePlatformRuntime("late")));

            Assert.That(result.Status, Is.EqualTo(PlatformRuntimeRegistrationStatus.RegistrySealed));
        }

        [Test]
        public void SelectionBeforeSeal_FailsExplicitly()
        {
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime registry must be sealed before selection.");

            var selection = PlatformRuntimeRegistry.Select(ExplicitRequest("requested"));

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RegistryNotSealed));
            Assert.That(selection.RequestedProviderId,
                Is.EqualTo(new PlatformProviderId("requested")));
            Assert.That(selection.Runtime, Is.Null);
        }

        [Test]
        public void FactoryException_IsContainedWithoutLocalMasking()
        {
            var factory = new FakePlatformRuntimeFactory(
                "throwing",
                () => throw new InvalidOperationException("factory failure"));
            PlatformRuntimeRegistry.RegisterFactory(factory);
            PlatformRuntimeRegistry.Seal();
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime factory for 'throwing' threw InvalidOperationException with message 'factory failure'.");

            var selection = PlatformRuntimeRegistry.Select(ExplicitRequest("throwing"));

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.FactoryCreationFailed));
            Assert.That(selection.SelectedProviderId,
                Is.EqualTo(new PlatformProviderId("throwing")));
            Assert.That(selection.RequestedProviderRegistered, Is.True);
            Assert.That(selection.Runtime, Is.Null);
            Assert.That(selection.FallbackUsed, Is.False);
        }

        [Test]
        public void NullRuntime_IsExplicitFailureWithoutLocalMasking()
        {
            PlatformRuntimeRegistry.RegisterFactory(
                new FakePlatformRuntimeFactory("null-provider", () => null));
            PlatformRuntimeRegistry.Seal();
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime factory for 'null-provider' returned null.");

            var selection = PlatformRuntimeRegistry.Select(ExplicitRequest("null-provider"));

            Assert.That(selection.Status, Is.EqualTo(PlatformRuntimeSelectionStatus.NullRuntime));
            Assert.That(selection.Runtime, Is.Null);
            Assert.That(selection.SelectedProviderId,
                Is.EqualTo(new PlatformProviderId("null-provider")));
        }

        [Test]
        public void RuntimeProviderMismatch_IsExplicitFailure()
        {
            PlatformRuntimeRegistry.RegisterFactory(
                new FakePlatformRuntimeFactory(
                    "factory-id",
                    () => new FakePlatformRuntime("runtime-id")));
            PlatformRuntimeRegistry.Seal();
            LogAssert.Expect(
                LogType.Error,
                "Platform runtime provider ID 'runtime-id' does not match factory provider ID 'factory-id'.");

            var selection = PlatformRuntimeRegistry.Select(ExplicitRequest("factory-id"));

            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.ProviderIdMismatch));
            Assert.That(selection.Runtime, Is.Null);
            Assert.That(selection.SelectedProviderId,
                Is.EqualTo(new PlatformProviderId("factory-id")));
        }

        [Test]
        public void Reset_ClearsRegistryDiagnosticsAndCachedSelection()
        {
            PlatformRuntimeRegistry.RegisterFactory(
                new FakePlatformRuntimeFactory(
                    "temporary",
                    () => new FakePlatformRuntime("temporary")));
            PlatformRuntimeRegistry.Seal();
            PlatformRuntimeRegistry.Select(ExplicitRequest("temporary"));

            ResetRegistryAndSelection();
            PlatformRuntimeRegistry.Seal();
            LogAssert.Expect(
                LogType.Error,
                "Requested platform provider 'temporary' is not registered. " +
                "No fallback provider was selected because the request was explicit.");
            var selection = PlatformRuntimeRegistry.Select(ExplicitRequest("temporary"));

            Assert.That(PlatformRuntimeRegistry.IsSealed, Is.True);
            Assert.That(PlatformRuntimeRegistry.RegisteredFactoryCount, Is.Zero);
            Assert.That(selection.Status,
                Is.EqualTo(PlatformRuntimeSelectionStatus.RequestedProviderNotRegistered));
            Assert.That(selection.Runtime, Is.Null);
        }

        private static PlatformProviderSelectionRequest ExplicitRequest(string providerId)
        {
            return PlatformProviderSelection.ParseArguments(new[]
            {
                PlatformProviderSelection.ProviderSelectionArgument,
                providerId,
            });
        }

        private static FakePlatformRuntimeFactory RegisterCountingFactory(string providerId)
        {
            var factory = new FakePlatformRuntimeFactory(
                providerId,
                () => new FakePlatformRuntime(providerId));
            PlatformRuntimeRegistry.RegisterFactory(factory);
            return factory;
        }

        private static void ResetRegistryAndSelection()
        {
            PlatformRuntimeRegistry.ResetForSubsystemRegistration();
            PlatformProviderSelection.ResetFromArguments(Array.Empty<string>());
        }
    }
}
