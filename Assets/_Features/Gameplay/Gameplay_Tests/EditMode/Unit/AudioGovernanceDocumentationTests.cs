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
            Assert.That(readme, Does.Contain("Gameplay-Audio-Governance.md"));
            Assert.That(readme, Does.Contain("Gameplay-Action-Audio-Governance.md"));
            Assert.That(readme, Does.Contain("Bgm-Flow-V1-Guidelines.md"));
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
            Assert.That(doc, Does.Contain("Gameplay-Audio-Governance.md"));
            Assert.That(doc, Does.Contain("Gameplay-Action-Audio-Governance.md"));
            Assert.That(doc, Does.Contain("Bgm-Flow-V1-Guidelines.md"));
            Assert.That(doc, Does.Contain("DamageOneShot"));
            Assert.That(doc, Does.Contain("EntityExitOneShot"));
            Assert.That(doc, Does.Contain("UI audio는 UI presenter/controller path에 남는다"));
            Assert.That(doc, Does.Contain("BGM/scene-flow audio는 stage/scene flow presenter path에 남는다"));
            Assert.That(doc, Does.Contain("GameplayAudioRequestPlanner"));
            Assert.That(doc, Does.Contain("GameplayAudioPresentationController"));
            Assert.That(doc, Does.Contain("GameplayActionAudioProfile"));
            Assert.That(doc, Does.Contain("GameplayActionAudioRequestPlanner"));
            Assert.That(doc, Does.Contain("GameplayActionAudioPresentationController"));
            Assert.That(doc, Does.Contain("global required gameplay semantic IDs가 아니다"));
            Assert.That(doc, Does.Contain("core enemy damage/death reaction sounds는 existing core one-shot path에 남는다"));
            Assert.That(doc, Does.Contain("GameplayActionAudioMoment v1 no longer includes `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, or `Blocked`"));
            Assert.That(doc, Does.Contain("IGameplayAudioPlaybackPort"));
            Assert.That(doc, Does.Contain("Owner-Bound Persistent Playback"));
            Assert.That(doc, Does.Contain("Audio Runtime Installer"));
            Assert.That(doc, Does.Contain("Audio Runtime Root"));
            Assert.That(doc, Does.Contain("Deprecated Terms"));
            Assert.That(doc, Does.Contain("Play3D"));
            Assert.That(doc, Does.Contain("v1 public contract는 request-based BGM transition을 포함한다"));
            Assert.That(doc, Does.Contain("must remain null"));
            Assert.That(doc, Does.Contain("setup defect"));
            Assert.That(doc, Does.Contain("binding-local validation rule의 canonical owner"));
            Assert.That(doc, Does.Contain("delegated `AudioBinding` diagnostics"));
            Assert.That(doc, Does.Contain("binding-local rule source는 `AudioBinding` 하나다"));
            Assert.That(doc, Does.Contain("validation authority reuse의 immediate policy는 deferred다"));
            Assert.That(doc, Does.Contain("shared `AudioBindingDiagnostics` facade"));
            Assert.That(doc, Does.Contain("권위는 shared에, 조합은 feature에 둔다"));
            Assert.That(doc, Does.Contain("`BGM lane -> source pool and active controller set -> attached registry`"));
            Assert.That(doc, Does.Contain("FadeOutIn은 request-based BGM runtime에 포함되어 있고"));
            Assert.That(doc, Does.Contain("second feature map consumer"));
            Assert.That(doc, Does.Contain("PlayPlannedAudio()"));
            Assert.That(doc, Does.Contain("RefreshAudioPlan(result) 내부에서는 core gameplay one-shot plan과 action-audio plan을 함께 refresh한다."));
            Assert.That(doc, Does.Contain("ApplyEntityExitOwnership()"));
            Assert.That(doc, Does.Contain("persistent runtime owner"));
            Assert.That(doc, Does.Contain("scene-local installer access seam"));
            Assert.That(doc, Does.Contain("Immediate` and `FadeOutIn` are executed transition modes in BGM flow v1"));
            Assert.That(doc, Does.Contain("true `Crossfade` needs shared-runtime multi-lane/capability expansion beyond the current single BGM lane"));
            Assert.That(doc, Does.Contain("prefer a grouped `GameplayPresentationAudioConfig`."));
            Assert.That(doc, Does.Not.Contain("GameplayAudioPresenter"));
            Assert.That(doc, Does.Not.Contain("IGameplayAudioCueProjector"));
            Assert.That(doc, Does.Not.Contain("GameplayAudioCue"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioGovernanceDoc_DefinesSafeExpansionChecklist_AndPendingPlanLifecycle()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-Audio-Governance.md");

            Assert.That(doc, Does.Contain("# Gameplay Audio Governance"));
            Assert.That(doc, Does.Contain("small and stable core required gameplay one-shot semantic set"));
            Assert.That(doc, Does.Contain("push/flip/action-specific sounds must not be added here by default"));
            Assert.That(doc, Does.Contain("action enums are not global required gameplay semantic IDs"));
            Assert.That(doc, Does.Contain("How To Add A New Gameplay Audio Semantic Safely"));
            Assert.That(doc, Does.Contain("catalog entry"));
            Assert.That(doc, Does.Contain("family metadata"));
            Assert.That(doc, Does.Contain("required-set review"));
            Assert.That(doc, Does.Contain("planner review"));
            Assert.That(doc, Does.Contain("tests를 갱신한다"));
            Assert.That(doc, Does.Contain("docs/governance note를 갱신한다"));
            Assert.That(doc, Does.Contain("DamageOneShot"));
            Assert.That(doc, Does.Contain("EntityExitOneShot"));
            Assert.That(doc, Does.Contain("Locomotion"));
            Assert.That(doc, Does.Contain("UiInteraction"));
            Assert.That(doc, Does.Contain("BgmFlow"));
            Assert.That(doc, Does.Contain("ReplacePendingPlan"));
            Assert.That(doc, Does.Contain("PlayPlannedAudio()"));
            Assert.That(doc, Does.Contain("last-write-wins"));
            Assert.That(doc, Does.Contain("Bgm-Flow-V1-Guidelines.md"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioGovernanceDoc_DefinesProfileLocalGovernance_RuntimeVsProductionPolicy_AndFrozenMomentTable()
        {
            var doc = ReadRepoFile("Docs/Architecture/Gameplay-Action-Audio-Governance.md");

            Assert.That(doc, Does.Contain("# Gameplay Action Audio Governance"));
            Assert.That(doc, Does.Contain("profile-local entries"));
            Assert.That(doc, Does.Contain("typed authoring axes"));
            Assert.That(doc, Does.Contain("global required gameplay semantic IDs가 아니다"));
            Assert.That(doc, Does.Contain("모든 action profile이 every action/moment combination을 가져야 한다는 global completeness rule은 없다"));
            Assert.That(doc, Does.Contain("missing owner view => no-op"));
            Assert.That(doc, Does.Contain("missing `GameplayActionAudioAuthoring` => no-op"));
            Assert.That(doc, Does.Contain("component가 존재하면 profile must be non-null and valid"));
            Assert.That(doc, Does.Contain("Push`: `Windup`, `AssistOutOfRange`, `NoTarget`, `Invalid`"));
            Assert.That(doc, Does.Contain("Flip`: `Windup`, `AssistOutOfRange`, `NoTarget`, `Invalid`"));
            Assert.That(doc, Does.Contain("GameplayActionAudioMoment v1 no longer includes `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, or `Blocked`"));
            Assert.That(doc, Does.Contain("gameplay action timeline still has execute/recovery; only the action-audio moments were removed"));
            Assert.That(doc, Does.Contain("Windup` => `StartedThisTick`"));
            Assert.That(doc, Does.Not.Contain("Execute` => `ExecutedThisTick`"));
            Assert.That(doc, Does.Not.Contain("Recovery` => `ExecutedThisTick && IsRecoveryPhase`"));
            Assert.That(doc, Does.Contain("AssistOutOfRange` => `PlayerActionAttemptSignals.FeedbackKind == AssistOutOfRange`"));
            Assert.That(doc, Does.Contain("action lifecycle audio emission은 `Windup` only다"));
            Assert.That(doc, Does.Contain("fake failure moments는 lifecycle moments를 synthesize하지 않고"));
            Assert.That(doc, Does.Contain("same-tick duplicate suppression은 하지 않는다"));
            Assert.That(doc, Does.Contain("impact and blocked gameplay/presentation signals remain owned by their existing gameplay/presentation lanes"));
            Assert.That(doc, Does.Contain("GameplayPresentationAudioConfig"));
            Assert.That(doc, Does.Contain("action audio v1 is one-shot only"));
            Assert.That(doc, Does.Contain("loop/continuous audio requires a separate owner/controller"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTestAutomationGuide_UsesScopedGameplayAudioVerificationVocabulary()
        {
            var doc = ReadRepoFile("Docs/Testing/Gameplay-Test-Automation-Guide.md");

            Assert.That(doc, Does.Contain("build verified"));
            Assert.That(doc, Does.Contain("core lane validated"));
            Assert.That(doc, Does.Contain("targeted orchestration/architecture validated"));
            Assert.That(doc, Does.Contain("full gameplay-wide regression validated"));
            Assert.That(doc, Does.Contain("The new gameplay audio structure/contracts are validated in core lanes."));
            Assert.That(doc, Does.Contain("Gameplay audio host orchestration and governance contracts are validated by targeted architecture tests."));
            Assert.That(doc, Does.Contain("Targeted gameplay-audio integration validation"));
            Assert.That(doc, Does.Contain("host ordering vs VFX / exit ownership timing"));
            Assert.That(doc, Does.Contain("attachment vs 2D fallback at exit boundaries"));
            Assert.That(doc, Does.Contain("bootstrap / authored map invariants"));
            Assert.That(doc, Does.Contain("settings / mixing coexistence with gameplay one-shot SFX"));
            Assert.That(doc, Does.Contain("UI / BGM separation from gameplay host orchestration"));
            Assert.That(doc, Does.Contain("Gameplay audio host-orchestration is validated against adjacent presentation and runtime boundaries via targeted integration tests."));
            Assert.That(doc, Does.Contain("This pass does not validate:"));
            Assert.That(doc, Does.Contain("full gameplay-wide regression closure"));
            Assert.That(doc, Does.Contain("all gameplay-wide regressions are closed"));
            Assert.That(doc, Does.Contain("is not enough to use this claim"));
            Assert.That(doc, Does.Contain("targeted persistent BGM ownership validated"));
            Assert.That(doc, Does.Contain("cross-scene continuity validated"));
            Assert.That(doc, Does.Contain("real transition-effects validation completed"));
            Assert.That(doc, Does.Contain("Persistent BGM ownership, cross-scene continuity, and single-source FadeOutIn are validated; Crossfade remains reserved."));
        }

        [Test]
        [Category("Extended")]
        public void BgmFlowGuidelines_DefinePersistentOwnershipBootstrapAndTransitionRoadmap()
        {
            var doc = ReadRepoFile("Docs/Architecture/Bgm-Flow-V1-Guidelines.md");

            Assert.That(doc, Does.Contain("# BGM Flow v1 Guidelines"));
            Assert.That(doc, Does.Contain("persistent runtime owner"));
            Assert.That(doc, Does.Contain("scene-local installer access seam"));
            Assert.That(doc, Does.Contain("AudioRuntimeExternalRootRegistry"));
            Assert.That(doc, Does.Contain("bootstrap plumbing only"));
            Assert.That(doc, Does.Contain("service locator"));
            Assert.That(doc, Does.Contain("Immediate` and `FadeOutIn` are executed transition modes in BGM flow v1"));
            Assert.That(doc, Does.Contain("true `FadeOutIn` is supported by the request-based playback port and shared audio runtime"));
            Assert.That(doc, Does.Contain("true `Crossfade` needs shared-runtime multi-lane/capability expansion beyond the current single BGM lane"));
            Assert.That(doc, Does.Contain("AudioRuntimeExternalRootRegistry cannot register multiple persistent AudioRuntimeRoot instances."));
            Assert.That(doc, Does.Contain("GlobalAudioFlowRoot cannot exist more than once. Reuse the existing persistent audio-flow root instead of creating another."));
            Assert.That(doc, Does.Contain("SceneBgmRequestSource requires a serialized GlobalAudioFlowBootstrap reference when a BgmProfile is assigned."));
            Assert.That(doc, Does.Contain("Persistent BGM ownership, cross-scene continuity, and single-source FadeOutIn are validated; Crossfade remains reserved."));
        }

        [Test]
        [Category("Extended")]
        public void BgmOwnershipGateAdr_DefinesPhaseSplit_UnsupportedPath_AndImplementationGate()
        {
            var doc = ReadRepoFile("Docs/Architecture/ADR/ADR-003-Persistent-Bgm-Ownership-Implementation-Gate.md");
            var readme = ReadRepoFile("Docs/Architecture/README.md");

            Assert.That(doc, Does.Contain("D0. discovery"));
            Assert.That(doc, Does.Contain("D1. decision closed"));
            Assert.That(doc, Does.Contain("D2. implementation gate ready"));
            Assert.That(doc, Does.Contain("D3. implementation"));
            Assert.That(doc, Does.Contain("`GameplayAudioPresentationController` does not own BGM."));
            Assert.That(doc, Does.Contain("no scene-global lookup"));
            Assert.That(doc, Does.Contain("no builder/result ownership"));
            Assert.That(doc, Does.Contain("canonical root same `GameObject`에 `AudioRuntimeInstaller`와 `GlobalAudioFlowBootstrap`이 co-located"));
            Assert.That(doc, Does.Contain("`UIAudioScene` continuity smoke harness"));
            Assert.That(doc, Does.Contain("decision closed를 implementation approved로 해석"));
            Assert.That(readme, Does.Contain("ADR-003-Persistent-Bgm-Ownership-Implementation-Gate.md"));
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
