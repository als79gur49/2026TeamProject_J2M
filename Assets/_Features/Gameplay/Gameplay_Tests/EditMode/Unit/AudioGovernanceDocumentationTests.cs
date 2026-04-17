using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class AudioGovernanceDocumentationTests
    {
        [Test]
        [Category("Extended")]
        public void ArchitectureReadme_ListsAudioArchitectureGuidelines_AsSupportingTruthSource()
        {
            var readme = ReadRepoFile("Docs/Architecture/README.md");

            Assert.That(readme, Does.Contain("Audio-Architecture-Guidelines.md"));
            Assert.That(readme, Does.Contain("2D non-spatial audio contracts"));
        }

        [Test]
        [Category("Extended")]
        public void AudioArchitectureGuidelines_DefineSeamVocabularyAndAttachedContract()
        {
            var doc = ReadRepoFile("Docs/Architecture/Audio-Architecture-Guidelines.md");

            Assert.That(doc, Does.Contain("# Audio Architecture Guidelines"));
            Assert.That(doc, Does.Contain("Authoritative Presentation Signal Seam"));
            Assert.That(doc, Does.Contain("Mapped Presentation Seam"));
            Assert.That(doc, Does.Contain("Owner-Bound Persistent Playback"));
            Assert.That(doc, Does.Contain("Audio Runtime Installer"));
            Assert.That(doc, Does.Contain("Audio Runtime Root"));
            Assert.That(doc, Does.Contain("Deprecated Terms"));
            Assert.That(doc, Does.Contain("Play3D"));
            Assert.That(doc, Does.Contain("v1 public contract는 fade/crossfade를 포함하지 않는다"));
            Assert.That(doc, Does.Contain("must remain null"));
            Assert.That(doc, Does.Contain("setup defect"));
            Assert.That(doc, Does.Contain("binding-local validation rule의 canonical owner"));
            Assert.That(doc, Does.Contain("delegated `AudioBinding` diagnostics"));
            Assert.That(doc, Does.Contain("binding-local rule source는 `AudioBinding` 하나다"));
        }

        [Test]
        [Category("Extended")]
        public void ArchivedUnityAudioBlueprint_PointsToActiveAudioGuidelines()
        {
            var archive = ReadRepoFile("Docs/Archive/Architecture/Unity-Audio-System-Blueprint.md");

            Assert.That(archive, Does.Contain("Active audio companion"));
            Assert.That(archive, Does.Contain("Audio-Architecture-Guidelines.md"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
