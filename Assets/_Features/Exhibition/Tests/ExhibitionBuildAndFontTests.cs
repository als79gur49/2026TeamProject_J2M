using System;
using System.IO;
using System.Linq;
using Game.Exhibition.Editor;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace Game.Exhibition.Tests
{
    public sealed class ExhibitionBuildAndFontTests
    {
        [Test]
        public void NormalWindowsPlayerIncludesParticipantReset()
        {
            var names = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name);
            Assert.That(names, Does.Contain("Game.Exhibition.Application"));
            Assert.That(names, Does.Contain("Game.Exhibition.Integration"));
            Assert.That(WindowsReleaseBuildPolicy.Scenes, Has.None.Contains("ExhibitionBoot"));
        }

        [Test]
        public void HelperIsCopiedExactlyAndIsAnExplicitDistributionArtifact()
        {
            var root = Path.Combine(Path.GetTempPath(), "j2m-reset-build-" + Guid.NewGuid().ToString("N"));
            try
            {
                ParticipantResetBuildPostprocessor.CopyHelper(root);
                var name = WindowsDistributionTargetPolicy.ParticipantRestartArtifact;
                Assert.That(File.ReadAllBytes(Path.Combine(root, name)),
                    Is.EqualTo(File.ReadAllBytes(ParticipantResetBuildPostprocessor.HelperSource)));
                Assert.That(WindowsDistributionTargetPolicy.DirectWindows.RequiredArtifacts, Does.Contain(name));
                Assert.That(WindowsDistributionTargetPolicy.SteamWindows.RequiredArtifacts, Does.Contain(name));
                Assert.That(WindowsDistributionStager.IsRuntimeIncludeCandidate(name), Is.True);
                Assert.That(WindowsDistributionStager.IsRuntimeIncludeCandidate("unrelated.ps1"), Is.False);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
