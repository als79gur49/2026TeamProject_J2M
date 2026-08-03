using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Platform.Steam.Tests.EditMode
{
    public sealed class SteamPlatformArchitectureTests
    {
        [Test]
        public void ProviderCoreAssembly_DependsOnPlatformRuntimeButNotSteamworksNet()
        {
            var references = typeof(SteamPlatformRuntime).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Contain("Game.Platform.Runtime"));
            Assert.That(references, Does.Not.Contain("com.rlabrecque.steamworks.net"));
        }

        [Test]
        public void ProviderCoreSource_ContainsNoSteamworksNetTypeReferences()
        {
            var runtimeRoot = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Packages/com.j2m.platform.steam/Runtime"));
            var forbiddenTokens = new[]
            {
                "using Steamworks",
                "SteamAPI.",
                "SteamUser.",
                "SteamUtils.",
                "Packsize.",
                "DllCheck.",
            };

            foreach (var path in Directory.EnumerateFiles(runtimeRoot, "*.cs"))
            {
                var content = File.ReadAllText(path);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(content, Does.Not.Contain(token),
                        Path.GetFileName(path) + " leaks adapter token " + token + ".");
                }
            }
        }

        [Test]
        public void LifecycleSurface_ContainsNoOutOfScopeFeatureMethods()
        {
            var forbidden = new[]
            {
                "Achievement", "Stats", "Cloud", "Leaderboard", "RichPresence",
                "ActivateGameOverlay", "SteamInput", "RestartAppIfNecessary",
            };
            var methodNames = typeof(ISteamNativeApi)
                .GetMethods()
                .Select(method => method.Name)
                .ToArray();

            foreach (var token in forbidden)
            {
                Assert.That(methodNames.Any(name =>
                    name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            }
        }
    }
}
