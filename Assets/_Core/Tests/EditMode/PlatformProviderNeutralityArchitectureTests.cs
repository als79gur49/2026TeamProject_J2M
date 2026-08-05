using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Platform.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Scripting;

namespace Game.Platform.Tests.EditMode
{
    public sealed class PlatformProviderNeutralityArchitectureTests
    {
        private static readonly Regex[] ProviderTokenPatterns =
        {
            new Regex("Steamworks", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("Facepunch", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("EpicOnlineServices", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"\bEOS\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"\bGOG\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("Galaxy", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("steam_api", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("SteamManager", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        };

        private static readonly Regex[] SerializedProviderTokenPatterns =
        {
            new Regex("Steamworks", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("Facepunch", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("EpicOnlineServices", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("Epic\\.OnlineServices", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("EOSSDK", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("GOG\\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("GogGalaxy", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("Galaxy\\.Api", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("steam_api", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex("SteamManager", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        };

        private static string ProjectRoot => Directory.GetCurrentDirectory();

        private static string PlatformRuntimeDirectory =>
            Path.Combine(ProjectRoot, "Assets", "_Core", "Runtime", "Platform");

        [SetUp]
        public void SetUp()
        {
            PlatformRuntimeRegistry.ResetForSubsystemRegistration();
        }

        [Test]
        public void PlatformCoreSourceAndAsmdef_ContainNoProviderSdkVocabulary()
        {
            var files = Directory
                .EnumerateFiles(PlatformRuntimeDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                foreach (var pattern in ProviderTokenPatterns)
                {
                    Assert.That(
                        pattern.IsMatch(content),
                        Is.False,
                        $"{Path.GetRelativePath(ProjectRoot, file)} contains provider token {pattern}.");
                }
            }
        }

        [Test]
        public void PlatformCoreAsmdef_HasNoGameFeatureOrProviderReferences()
        {
            var asmdefPath = Path.Combine(PlatformRuntimeDirectory, "Game.Platform.Runtime.asmdef");
            var asmdef = File.ReadAllText(asmdefPath);

            Assert.That(asmdef, Does.Contain("\"name\": \"Game.Platform.Runtime\""));
            Assert.That(asmdef, Does.Not.Contain("\"references\""));
            Assert.That(asmdef, Does.Not.Contain("Game.Feature"));
            Assert.That(asmdef, Does.Not.Contain("Game.Shared"));
        }

        [Test]
        public void CompiledPlatformAssembly_DependsOnNoGameAssembly()
        {
            var references = typeof(IPlatformRuntime).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith("Game.", StringComparison.Ordinal))
                .ToArray();

            Assert.That(references, Is.Empty);
        }

        [Test]
        public void Bootstrap_HasRequiredRuntimeInitializePhases()
        {
            var hooks = typeof(PlatformRuntimeBootstrap)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SelectMany(method => method
                    .GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false)
                    .Cast<RuntimeInitializeOnLoadMethodAttribute>()
                    .Select(attribute => new { Method = method, Attribute = attribute }))
                .ToArray();

            Assert.That(
                hooks.Any(hook =>
                    hook.Method.Name == "ResetSubsystemState" &&
                    hook.Attribute.loadType == RuntimeInitializeLoadType.SubsystemRegistration),
                Is.True);
            Assert.That(
                hooks.Any(hook =>
                    hook.Method.Name == "RunAutomaticBootstrap" &&
                    hook.Attribute.loadType == RuntimeInitializeLoadType.BeforeSceneLoad),
                Is.True);
            Assert.That(
                typeof(PlatformRuntimeRegistry)
                    .GetMethod(nameof(PlatformRuntimeRegistry.RegisterFactory))
                    .GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false),
                Is.Empty,
                "Provider registration must not depend on the host's BeforeSceneLoad hook.");
        }

        [Test]
        public void RuntimeAssembly_UsesAlwaysLinkAssembly()
        {
            var attributes = typeof(IPlatformRuntime).Assembly
                .GetCustomAttributes(typeof(AlwaysLinkAssemblyAttribute), false);

            Assert.That(attributes, Has.Length.EqualTo(1));
        }

        [Test]
        public void Registry_PublicApi_IsRegistrationOnlyNotServiceLocation()
        {
            var forbiddenNames = new HashSet<string>(
                new[] { "Current", "Instance", "GetService", "Services", "Runtime" },
                StringComparer.Ordinal);
            var publicStaticMembers = typeof(PlatformRuntimeRegistry)
                .GetMembers(BindingFlags.Public | BindingFlags.Static);

            Assert.That(
                publicStaticMembers.Where(member => forbiddenNames.Contains(member.Name)),
                Is.Empty);
            Assert.That(
                publicStaticMembers.OfType<MethodInfo>().Select(method => method.Name),
                Does.Contain(nameof(PlatformRuntimeRegistry.RegisterFactory)));
        }

        [Test]
        public void NewProviderFactory_RegistersWithoutCoreSourceChanges()
        {
            var runtime = new FakePlatformRuntime("com.test.provider");
            var factory = new FakePlatformRuntimeFactory("com.test.provider", () => runtime);

            Assert.That(PlatformRuntimeRegistry.RegisterFactory(factory).IsSuccess, Is.True);
            PlatformRuntimeRegistry.Seal();
            var selection = PlatformRuntimeRegistry.Select(
                PlatformProviderSelection.ParseArguments(new[]
                {
                    PlatformProviderSelection.ProviderSelectionArgument,
                    "com.test.provider",
                }));

            Assert.That(selection.IsSuccess, Is.True);
            Assert.That(selection.Runtime, Is.SameAs(runtime));
            Assert.That(selection.SelectedProviderId.Value, Is.EqualTo("com.test.provider"));
        }

        [Test]
        public void ProviderSelectionParser_IsFoundationOwnedAndAdapterNeutral()
        {
            var foundationSource = File.ReadAllText(
                Path.Combine(PlatformRuntimeDirectory, "PlatformProviderSelection.cs"));
            var steamRegistrationSource = File.ReadAllText(Path.Combine(
                ProjectRoot,
                "Packages",
                "com.j2m.platform.steam",
                "Runtime",
                "SteamPlatformRegistration.cs"));
            var adapterRegistrationSource = File.ReadAllText(Path.Combine(
                ProjectRoot,
                "Packages",
                "com.j2m.platform.steam.steamworksnet",
                "Runtime",
                "SteamworksNetPlatformRegistration.cs"));
            var adapterAsmdef = File.ReadAllText(Path.Combine(
                ProjectRoot,
                "Packages",
                "com.j2m.platform.steam.steamworksnet",
                "Runtime",
                "Game.Platform.Steam.SteamworksNet.asmdef"));

            Assert.That(foundationSource,
                Does.Contain("public const string ProviderSelectionArgument"));
            Assert.That(foundationSource, Does.Not.Contain("Steam"));
            Assert.That(steamRegistrationSource, Does.Not.Contain("ProviderSelectionArgument"));
            Assert.That(steamRegistrationSource, Does.Not.Contain("RegisterIfExplicitlySelected"));
            Assert.That(adapterRegistrationSource, Does.Not.Contain("GetCommandLineArgs"));
            Assert.That(adapterRegistrationSource, Does.Not.Contain("ProviderSelectionArgument"));
            Assert.That(adapterAsmdef, Does.Contain("\"Game.Platform.Runtime\""));
        }

        [Test]
        public void SerializedAssets_ReferenceNoPlatformRuntimeScriptsOrProviderTokens()
        {
            var platformScriptGuids = Directory
                .EnumerateFiles(PlatformRuntimeDirectory, "*.cs.meta", SearchOption.TopDirectoryOnly)
                .Select(ReadGuid)
                .Where(guid => guid.Length > 0)
                .ToArray();
            var serializedFiles = EnumerateSerializedAssets().ToArray();

            foreach (var file in serializedFiles)
            {
                var content = File.ReadAllText(file);
                foreach (var guid in platformScriptGuids)
                {
                    Assert.That(
                        content.IndexOf(guid, StringComparison.OrdinalIgnoreCase),
                        Is.LessThan(0),
                        $"{Path.GetRelativePath(ProjectRoot, file)} serializes Platform runtime script {guid}.");
                }

                foreach (var pattern in SerializedProviderTokenPatterns)
                {
                    Assert.That(
                        pattern.IsMatch(content),
                        Is.False,
                        $"{Path.GetRelativePath(ProjectRoot, file)} contains provider serialization token {pattern}.");
                }
            }
        }

        [Test]
        public void HostCallbacks_ConvergeOnSingleShutdownSeam()
        {
            var hostSource = File.ReadAllText(
                Path.Combine(PlatformRuntimeDirectory, "PlatformRuntimeApplicationHost.cs"));

            Assert.That(hostSource, Does.Contain("private void OnApplicationQuit()"));
            Assert.That(hostSource, Does.Contain("private void OnDestroy()"));
            Assert.That(
                Regex.IsMatch(
                    hostSource,
                    @"private void OnApplicationQuit\(\)\s*\{\s*ShutdownOnce\(\);\s*\}"),
                Is.True);
            Assert.That(
                Regex.IsMatch(
                    hostSource,
                    @"private void OnDestroy\(\)\s*\{\s*ShutdownOnce\(\);"),
                Is.True,
                "OnApplicationQuit and OnDestroy must both call the same ShutdownOnce seam.");
        }

        private static IEnumerable<string> EnumerateSerializedAssets()
        {
            var assetsRoot = Path.Combine(ProjectRoot, "Assets");
            return Directory
                .EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories)
                .Where(path =>
                    path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase));
        }

        private static string ReadGuid(string metaPath)
        {
            const string prefix = "guid: ";
            var line = File.ReadLines(metaPath)
                .FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
            return line == null ? string.Empty : line.Substring(prefix.Length).Trim();
        }
    }
}
