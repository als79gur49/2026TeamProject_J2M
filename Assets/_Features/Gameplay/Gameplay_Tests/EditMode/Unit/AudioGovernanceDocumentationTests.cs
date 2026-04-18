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
            Assert.That(doc, Does.Contain("GameplayAudioSemanticId"));
            Assert.That(doc, Does.Contain("GameplayAudioSemanticCatalog"));
            Assert.That(doc, Does.Contain("RequiredOneShotV1"));
            Assert.That(doc, Does.Contain("GameplayAudioRequestPlanner"));
            Assert.That(doc, Does.Contain("GameplayAudioPresentationController"));
            Assert.That(doc, Does.Contain("IGameplayAudioPlaybackPort"));
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
            Assert.That(doc, Does.Contain("validation authority reuse의 immediate policy는 deferred다"));
            Assert.That(doc, Does.Contain("shared `AudioBindingDiagnostics` facade"));
            Assert.That(doc, Does.Contain("권위는 shared에, 조합은 feature에 둔다"));
            Assert.That(doc, Does.Contain("`BGM lane -> source pool and active controller set -> attached registry`"));
            Assert.That(doc, Does.Contain("fade/crossfade와 ducking은 `AudioBgmChannel` 또는 `AudioBgmController`"));
            Assert.That(doc, Does.Contain("second feature map consumer"));
            Assert.That(doc, Does.Contain("PlayEntityExitEffects()"));
            Assert.That(doc, Does.Contain("PlayPlayerHitEffects(result)"));
            Assert.That(doc, Does.Contain("PlayPlannedAudio()"));
            Assert.That(doc, Does.Contain("ApplyEntityExitOwnership()"));
            Assert.That(doc, Does.Not.Contain("GameplayAudioPresenter"));
            Assert.That(doc, Does.Not.Contain("IGameplayAudioCueProjector"));
            Assert.That(doc, Does.Not.Contain("GameplayAudioCue"));
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
