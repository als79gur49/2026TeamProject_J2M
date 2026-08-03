using System.Collections;
using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Platform.Tests.PlayMode
{
    public sealed class PlatformRuntimeMainMenuPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var existing = PlatformRuntimeApplicationHost.CurrentForTests;
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
                yield return null;
            }

            PlatformRuntimeBootstrap.ResetSubsystemStateForTests();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var existing = PlatformRuntimeApplicationHost.CurrentForTests;
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
                yield return null;
            }

            PlatformRuntimeBootstrap.ResetSubsystemStateForTests();
        }

        [UnityTest]
        public IEnumerator LocalRuntime_MainMenuLoadsWithoutSerializedPlatformSetup()
        {
            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.ProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(host.InitializationResult.IsSuccess, Is.True);

            yield return SceneManager.LoadSceneAsync("MainMenuScene", LoadSceneMode.Single);
            yield return null;

            var hosts = Object.FindObjectsByType<PlatformRuntimeApplicationHost>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenuScene"));
            Assert.That(PlatformRuntimeApplicationHost.CurrentForTests, Is.SameAs(host));
            Assert.That(hosts, Has.Length.EqualTo(1));
            Assert.That(host.ProviderId, Is.EqualTo(PlatformProviderId.Local));
            Assert.That(host.InitializationResult.IsSuccess, Is.True);
        }

        [UnityTest]
        public IEnumerator FakeRuntime_ContinuesTickingAcrossMainMenuReload()
        {
            var runtime = new CountingPlatformRuntime("scene-runtime");
            var factory = new CountingPlatformRuntimeFactory(runtime);
            Assert.That(
                PlatformRuntimeBootstrap.TryConfigureTestOverride(
                    suppressAutomaticBootstrap: false,
                    fakeFactory: factory,
                    out var failureReason),
                Is.True,
                failureReason);
            var host = PlatformRuntimeBootstrap.BootstrapNowForTests();

            yield return SceneManager.LoadSceneAsync("MainMenuScene", LoadSceneMode.Single);
            yield return null;
            var ticksAfterFirstLoad = runtime.TickCount;
            yield return SceneManager.LoadSceneAsync("MainMenuScene", LoadSceneMode.Single);
            yield return null;

            Assert.That(PlatformRuntimeApplicationHost.CurrentForTests, Is.SameAs(host));
            Assert.That(runtime.InitializeCount, Is.EqualTo(1));
            Assert.That(runtime.TickCount, Is.GreaterThan(ticksAfterFirstLoad));
            Assert.That(runtime.ShutdownCount, Is.Zero);
        }
    }
}
