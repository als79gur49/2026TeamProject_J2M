using System;
using System.IO;
using System.Linq;
using Game.Platform.Runtime;
using Game.Product.Achievements;
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

            foreach (var path in Directory.EnumerateFiles(
                runtimeRoot,
                "*.cs",
                SearchOption.AllDirectories))
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

        [Test]
        public void AchievementCapability_ContainsOnlyBoundedStoreSmokeSurface()
        {
            var methodNames = typeof(ISteamAchievementApi)
                .GetMethods()
                .Select(method => method.Name)
                .ToArray();
            var forbidden = new[]
            {
                "RequestCurrentStats",
                "ClearAchievement",
                "ResetAllStats",
                "IndicateAchievementProgress",
                "GetStat",
                "SetStat",
                "Leaderboard",
            };

            foreach (var method in forbidden)
            {
                Assert.That(methodNames, Does.Not.Contain(method));
            }

            Assert.That(methodNames, Is.EquivalentTo(new[]
            {
                "GetNumAchievements",
                "GetAchievementName",
                "GetAchievement",
                "SetAchievement",
                "StoreStats",
                "RegisterAchievementStoreCallbacks",
                "DisposeAchievementStoreCallbacks",
            }));
        }

        [Test]
        public void PlatformRegistry_RemainsFactoryRegistryWithoutFeatureServiceLocator()
        {
            var methodNames = typeof(PlatformRuntimeRegistry)
                .GetMethods(System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(methodNames.Any(name =>
                name.IndexOf("GetService", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("ResolveFeature", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("AchievementService", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void SpacewarSmokeTokens_DoNotLeakIntoProductProductionSources()
        {
            var productionRoots = new[]
            {
                "Assets/_Core/Runtime/Platform",
                "Assets/_Features/Gameplay",
                "Assets/_Features/UI",
                "Assets/_Features/Stages/Runtime",
            };
            var forbiddenTokens = new[]
            {
                "ACH_WIN_ONE_GAME",
                "SpacewarAchievementSmokePolicy",
                "j2mSteamAchievementSmoke",
            };

            foreach (var root in productionRoots)
            {
                foreach (var path in Directory.EnumerateFiles(
                    root,
                    "*.cs",
                    SearchOption.AllDirectories))
                {
                    var content = File.ReadAllText(path);
                    foreach (var token in forbiddenTokens)
                    {
                        Assert.That(content, Does.Not.Contain(token),
                            path + " leaks Spacewar smoke token " + token + ".");
                    }
                }
            }
        }

        [Test]
        public void ProductPublisher_OwnsNoSpacewarPolicyOrSecondCallbackPump()
        {
            var productRoot =
                "Packages/com.j2m.platform.steam/Runtime/ProductAchievements";
            var forbiddenTokens = new[]
            {
                "ACH_WIN_ONE_GAME",
                "Spacewar",
                "SpacewarAchievementSmokePolicy",
                "SteamAchievementSmokeCoordinator",
                "SteamAPI.RunCallbacks",
                "480",
            };

            foreach (var path in Directory.EnumerateFiles(
                productRoot,
                "*.cs",
                SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(path);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(content, Does.Not.Contain(token),
                        path + " leaks forbidden product publisher token " + token + ".");
                }
            }
        }

        [Test]
        public void ExpectedSteamNameLiteral_HasOneProductionOwner()
        {
            const string expectedName = "VQ_LEVEL_4_CLEAR";
            var productionRoots = new[]
            {
                "Assets/_Features/Achievements",
                "Assets/_Features/Gameplay",
                "Assets/_Features/UI",
                "Assets/_Features/Stages/Runtime",
                "Packages/com.j2m.platform.steam/Runtime",
            };
            var matchingPaths = productionRoots
                .SelectMany(root => Directory.EnumerateFiles(
                    root,
                    "*.cs",
                    SearchOption.AllDirectories))
                .Where(path => File.ReadAllText(path).Contains(expectedName))
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            Assert.That(matchingPaths, Is.EqualTo(new[]
            {
                "Packages/com.j2m.platform.steam/Runtime/ProductAchievements/SteamAchievementMapping.cs",
            }));
        }

        [Test]
        public void ProductAndGameplayAssemblies_DoNotDependOnSteam()
        {
            var productDomainAsmdef = File.ReadAllText(
                "Assets/_Features/Achievements/Achievement_Domain/Runtime/" +
                "Game.Product.Achievements.Domain.asmdef");
            var productCompositionAsmdef = File.ReadAllText(
                "Assets/_Features/Achievements/Achievement_Composition/Runtime/" +
                "Game.Product.Achievements.Composition.asmdef");

            Assert.That(productDomainAsmdef, Does.Not.Contain("Steam"));
            Assert.That(productCompositionAsmdef, Does.Not.Contain("Steam"));
            Assert.That(
                typeof(GameAchievementId).Assembly.GetReferencedAssemblies()
                    .Any(reference => reference.Name.Contains("Steam")),
                Is.False);
        }
    }
}
