using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SceneTransitionOverlayShellContentTests
    {
        private const string ThemePath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";

        [Test]
        public void SceneTransitionOverlayContentResolver_UsesStageTransitionKindExactMatch()
        {
            using var fallback = ContentHandle.Create<ChanceLostOverlayContentView>("FallbackRestart");
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            using var catalog = CatalogHandle.Create(
                Entry(StageTransitionKind.Unknown, fallback.View),
                Entry(StageTransitionKind.LevelFailedRestart, generic.View));
            var resolver = new SceneTransitionOverlayContentResolver();
            var model = Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart);

            var resolved = resolver.Resolve(model, catalog.Catalog);

            Assert.That(resolved, Is.SameAs(generic.View));
        }

        [Test]
        public void SceneTransitionOverlayContentResolver_CommonRestartSemanticIdsCanShareGenericContent()
        {
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            using var catalog = CatalogHandle.Create(
                Entry(StageTransitionKind.StageRetryManual, generic.View),
                Entry(StageTransitionKind.LevelFailedRestart, generic.View));
            var resolver = new SceneTransitionOverlayContentResolver();

            var manualResolved = resolver.Resolve(
                Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart),
                catalog.Catalog);
            var levelFailedResolved = resolver.Resolve(
                Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart),
                catalog.Catalog);

            Assert.That(manualResolved, Is.SameAs(generic.View));
            Assert.That(levelFailedResolved, Is.SameAs(generic.View));
        }

        [Test]
        public void SceneTransitionOverlayContentResolver_UnmatchedKindFailsClosed()
        {
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            using var catalog = CatalogHandle.Create(
                Entry(StageTransitionKind.StageClearNext, generic.View));
            var resolver = new SceneTransitionOverlayContentResolver();

            var resolved = resolver.Resolve(
                Model(StageTransitionKind.Unknown, TransitionOverlayKind.Restart),
                catalog.Catalog);

            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void SceneTransitionOverlayShell_ShowBlockerOnly_HidesContentMount()
        {
            using var shell = ShellHandle.Create();

            shell.View.ShowBlockerOnly(true);

            Assert.That(shell.Root.activeSelf, Is.True);
            Assert.That(shell.Blocker.activeSelf, Is.True);
            Assert.That(shell.VisualRoot.activeSelf, Is.False);
        }

        [Test]
        public void SceneTransitionOverlayShell_ShowContent_MountsOnlySelectedContent()
        {
            using var shell = ShellHandle.Create();
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");

            var content = shell.View.MountContent(generic.View);
            shell.View.ShowContent(Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart), content);

            Assert.That(shell.VisualRoot.activeSelf, Is.True);
            Assert.That(shell.ContentMount.childCount, Is.EqualTo(1));
            Assert.That(shell.ContentMount.GetChild(0).name, Is.EqualTo("GenericLoadingOverlayContent"));
        }

        [Test]
        public void SceneTransitionOverlayShell_HideAll_DisablesBlockerAndVisual()
        {
            using var shell = ShellHandle.Create();
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            var content = shell.View.MountContent(generic.View);
            shell.View.ShowContent(Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart), content);

            shell.View.HideAll();

            Assert.That(shell.Root.activeSelf, Is.False);
            Assert.That(shell.Blocker.activeSelf, Is.False);
            Assert.That(shell.VisualRoot.activeSelf, Is.False);
        }

        [Test]
        public void SceneTransitionOverlayShell_MountContentWithoutCatalogPrefabReportsSetupDefect()
        {
            using var shell = ShellHandle.Create();

            var exception = Assert.Throws<InvalidOperationException>(() => shell.View.MountContent(null));

            Assert.That(exception.Message, Does.Contain("catalog-authored content prefab"));
        }

        [Test]
        public void ChanceLostOverlayContentView_DoesNotExposeRetiredChanceTextBindings()
        {
            var fieldNames = typeof(ChanceLostOverlayContentView)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .ToArray();

            Assert.That(fieldNames, Does.Not.Contain("_previousChanceText"));
            Assert.That(fieldNames, Does.Not.Contain("_currentChanceText"));
            Assert.That(fieldNames, Does.Not.Contain("_totalChanceText"));
            Assert.That(fieldNames, Does.Not.Contain("_deathCountText"));
            Assert.That(fieldNames, Does.Not.Contain("_currentTextPulseScalePunch"));
            Assert.That(fieldNames, Does.Not.Contain("_currentTextPulseDurationSeconds"));
            Assert.That(fieldNames, Does.Not.Contain("_previousTextDimAlpha"));
            Assert.That(fieldNames, Does.Not.Contain("_previousTextDimDurationSeconds"));
            Assert.That(fieldNames, Does.Contain("_chanceSlotRoots"));
            Assert.That(fieldNames, Does.Contain("_remainingChancesLabel"));
            Assert.That(fieldNames, Does.Contain("_loadingLabel"));
        }

        [Test]
        public void ChanceLostOverlayContent_BindsLocalizedLabelsAndKeepsProgressIndependent()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;
            var model = new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
                TransitionOverlayKind.ChanceLost,
                blockInput: true,
                showProgress: true,
                progress01: 0.25f,
                hasChanceLost: true,
                previousRemainingChances: 3,
                currentRemainingChances: 2,
                totalChances: 3,
                deathCount: 1,
                text: new SceneTransitionOverlayTextSnapshot(
                    "ko-KR",
                    "남은 목숨",
                    "불러오는 중..."));

            view.Bind(model);

            Assert.That(content.RemainingChancesLabel.text, Is.EqualTo("남은 목숨"));
            Assert.That(content.LoadingLabel.text, Is.EqualTo("불러오는 중..."));
            Assert.That(content.ProgressText.text, Is.EqualTo("25%"));

            view.SetProgress(1f);

            Assert.That(content.LoadingLabel.text, Is.EqualTo("불러오는 중..."));
            Assert.That(content.ProgressText.text, Is.EqualTo("100%"));
        }

        [Test]
        public void GenericLoadingOverlayContent_BindsLocalizedLoadingLabelAndKeepsProgressIndependent()
        {
            using var content = ContentHandle.Create<GenericLoadingOverlayContentView>(
                "GenericLoadingOverlayContent");
            var view = (GenericLoadingOverlayContentView)content.View;
            var model = new SceneTransitionOverlayModel(
                StageTransitionKind.StageClearNext,
                TransitionOverlayKind.StageClear,
                blockInput: true,
                showProgress: true,
                progress01: 0.25f,
                hasChanceLost: false,
                previousRemainingChances: 0,
                currentRemainingChances: 0,
                totalChances: 0,
                deathCount: 0,
                text: new SceneTransitionOverlayTextSnapshot(
                    "ko-KR",
                    string.Empty,
                    "불러오는 중..."));

            view.Bind(model);

            Assert.That(content.LoadingLabel.text, Is.EqualTo("불러오는 중..."));
            Assert.That(content.ProgressText.text, Is.EqualTo("25%"));

            view.SetProgress(1f);

            Assert.That(content.LoadingLabel.text, Is.EqualTo("불러오는 중..."));
            Assert.That(content.ProgressText.text, Is.EqualTo("100%"));
        }

        [Test]
        public void ChanceLostOverlayContentView_UsesInspectorSlotRootsAsPrimarySource()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;

            Assert.That(view.CollectValidationIssues(), Is.Empty);

            var extraFallbackSlot = new GameObject("ChanceSlotView 99", typeof(RectTransform), typeof(CanvasGroup));
            extraFallbackSlot.transform.SetParent(content.Root.transform, false);

            Assert.That(view.ResolvedChanceSlotCountForTests, Is.EqualTo(3));
            Assert.That(view.ResolvedChanceSlotsForTests, Is.EqualTo(content.ChanceSlots));
            Assert.That(view.CollectValidationIssues(), Has.Some.Contains("_chanceSlotRoots count 3"));
        }

        [Test]
        public void ChanceLostOverlayContentView_FallbackIsSafetyNet_NotCurrentPrefabContract()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>(
                "ChanceLost",
                bindChanceSlotRoots: false);
            var view = (ChanceLostOverlayContentView)content.View;

            Assert.That(view.ResolvedChanceSlotCountForTests, Is.EqualTo(3));
            Assert.That(
                view.CollectValidationIssues(),
                Has.Some.Contains("requires explicit _chanceSlotRoots inspector bindings"));
        }

        [Test]
        public void SceneTransitionOverlayContentView_RequiresOnlyRootGroupAndProgressTextBaseBindings()
        {
            using var content = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");

            var issues = content.View.CollectValidationIssues();

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void SceneTransitionOverlayContentView_DoesNotExposeRetiredBaseTextProgressAnimatorBindings()
        {
            var fieldNames = typeof(SceneTransitionOverlayContentView)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .ToArray();

            Assert.That(fieldNames, Does.Contain("_rootGroup"));
            Assert.That(fieldNames, Does.Contain("_progressText"));
            Assert.That(fieldNames, Does.Not.Contain("_titleText"));
            Assert.That(fieldNames, Does.Not.Contain("_messageText"));
            Assert.That(fieldNames, Does.Not.Contain("_progressRoot"));
            Assert.That(fieldNames, Does.Not.Contain("_progressFill"));
            Assert.That(fieldNames, Does.Not.Contain("_animator"));
        }

        [Test]
        public void ChanceLostOverlayContent_Show_AnimatesLostChanceSlot()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;
            var lostSlot = content.ChanceSlots[1];
            var startPosition = lostSlot.anchoredPosition;
            var effectImage = FindEffectImage(lostSlot);
            var filledIcon = FindImage(lostSlot, "FilledIcon");
            var authoredEffectColor = effectImage.color;
            var authoredEffectMaterial = effectImage.material;
            var authoredFilledIconColor = filledIcon.color;
            var authoredFilledIconMaterial = filledIcon.material;

            view.Bind(new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
                TransitionOverlayKind.ChanceLost,
                blockInput: true,
                showProgress: true,
                progress01: 0.25f,
                hasChanceLost: true,
                previousRemainingChances: 2,
                currentRemainingChances: 1,
                totalChances: 3,
                deathCount: 4));

            view.Show();

            Assert.That(view.ResolvedChanceSlotCountForTests, Is.EqualTo(3));
            Assert.That(view.ActiveLostChanceAnimationCountForTests, Is.EqualTo(1));
            Assert.That(lostSlot.anchoredPosition, Is.EqualTo(startPosition));
            var lostImpactRoot = (RectTransform)lostSlot.Find("LostChanceImpactRoot");
            Assert.That(lostImpactRoot, Is.Not.Null);
            var lostTweenRoot = (RectTransform)lostImpactRoot.Find("LostChanceTweenRoot");
            Assert.That(lostTweenRoot, Is.Not.Null);
            var allIn1Shader = Shader.Find("AllIn1SpriteShader/AllIn1SpriteShaderUiMask");
            if (allIn1Shader != null)
            {
                Assert.That(effectImage.material.shader, Is.SameAs(allIn1Shader));
                Assert.That(filledIcon.material.shader, Is.SameAs(allIn1Shader));
            }

            view.ResetView();

            Assert.That(view.ActiveLostChanceAnimationCountForTests, Is.Zero);
            Assert.That(lostSlot.anchoredPosition, Is.EqualTo(startPosition));
            Assert.That(lostImpactRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(lostImpactRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(lostTweenRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(effectImage.color, Is.EqualTo(authoredEffectColor));
            Assert.That(effectImage.material, Is.SameAs(authoredEffectMaterial));
            Assert.That(filledIcon.color, Is.EqualTo(authoredFilledIconColor));
            Assert.That(filledIcon.material, Is.SameAs(authoredFilledIconMaterial));
            Assert.That(CountCrackLines(lostTweenRoot), Is.EqualTo(5));
            Assert.That(CountCrackShards(lostTweenRoot), Is.EqualTo(9));
            AssertCrackShardsRestored(lostTweenRoot);
            Assert.That(lostSlot.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [Test]
        public void ChanceLostProductionContent_CracksBeforeDebrisPreservesFramesAndRestoresOnRebind()
        {
            var prefab = Resources.Load<ChanceLostOverlayContentView>("UI/Transitions/Contents/ChanceLostOverlayContent");
            var view = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Assert.That(view.GetComponentsInChildren<Image>(true)
                    .Count(image => image.name.StartsWith("CrackShard", StringComparison.Ordinal)), Is.EqualTo(27),
                    "All three production slots must contain their nine authored sprite fragments before Bind.");
                var model = new SceneTransitionOverlayModel(
                    StageTransitionKind.DeathRetryChanceLost, TransitionOverlayKind.ChanceLost,
                    blockInput: true, showProgress: true, progress01: 0f, hasChanceLost: true,
                    previousRemainingChances: 2, currentRemainingChances: 1, totalChances: 3, deathCount: 1);
                view.Bind(model);
                var slots = view.ResolvedChanceSlotsForTests;
                var impactRoot = (RectTransform)slots[1].Find("LostChanceImpactRoot");
                var body = (RectTransform)impactRoot.Find("LostChanceTweenRoot");
                var survivor = (RectTransform)slots[0].Find("LostChanceImpactRoot/LostChanceTweenRoot");
                var frame = (RectTransform)impactRoot.Find("Frame (1)");
                Assert.That(frame.gameObject.activeSelf, Is.True, "The visible middle frame must follow impact motion.");
                var framePosition = frame.anchoredPosition;
                var frameRotation = frame.localRotation;
                var frameWorldPosition = frame.position;
                var icon = FindImage(body, "FilledIcon");
                var authoredMaterial = icon.material;
                var shards = body.GetComponentsInChildren<Image>(true)
                    .Where(image => image.name.StartsWith("CrackShard", StringComparison.Ordinal)).ToArray();
                Assert.That(shards, Has.Length.EqualTo(9));
                Assert.That(shards.All(image => image.sprite == icon.sprite), Is.True,
                    "Detached pieces must sample the original helmet sprite.");
                Assert.That(slots[2].Find("LostChanceImpactRoot/LostChanceTweenRoot").GetComponent<CanvasGroup>().alpha, Is.Zero);

                view.Show();
                var duration = view.RootSequenceDurationSecondsForTests;
                view.GotoRootSequenceForTests(0.33f);
                Assert.That(icon.material.GetFloat("_CrackReveal"), Is.GreaterThan(0.3f));
                Assert.That(shards.All(image => image.color.a == 0f), Is.True, "Cracks must precede debris.");
                Assert.That(survivor.localScale, Is.EqualTo(Vector3.one), "Survivor acknowledgement belongs at the end.");
                Assert.That(Mathf.Abs(impactRoot.anchoredPosition.y), Is.GreaterThan(0.1f),
                    "The frame and icon must share the vertical impact shake.");
                Assert.That(Vector3.Distance(frame.position, frameWorldPosition), Is.GreaterThan(0.1f));
                Assert.That(body.anchoredPosition, Is.EqualTo(Vector2.zero),
                    "The icon's fall root must remain centered during the shared impact.");

                view.GotoRootSequenceForTests(1.15f);
                Assert.That(icon.material.GetFloat("_Decay"), Is.GreaterThan(0.1f));
                Assert.That(icon.material.GetFloat("_Detach"), Is.GreaterThan(0.1f));
                for (var shardIndex = 0; shardIndex < shards.Length; shardIndex++)
                {
                    Assert.That(shards[shardIndex].material.GetFloat("_Fragment"),
                        Is.EqualTo(shardIndex + 1).Within(0.001f));
                }
                Assert.That(body.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "The cracked icon must remain readable before the final fade.");
                Assert.That(body.anchoredPosition.y, Is.LessThan(0f));
                Assert.That(impactRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(impactRoot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(frame.position, Is.EqualTo(frameWorldPosition));
                Assert.That(frame.anchoredPosition, Is.EqualTo(framePosition));
                Assert.That(frame.localRotation, Is.EqualTo(frameRotation));
                Assert.That(shards.Any(image => image.color.a > 0.1f), Is.True);

                view.GotoRootSequenceForTests(1.79f);
                Assert.That(survivor.localScale.x, Is.GreaterThan(1f));
                view.GotoRootSequenceForTests(duration);
                Assert.That(view.IsCompleted, Is.True);
                Assert.That(body.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(slots[1].GetComponent<CanvasGroup>().alpha,
                    Is.EqualTo(slots[2].GetComponent<CanvasGroup>().alpha).Within(0.001f));

                view.Bind(model);
                Assert.That(impactRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(impactRoot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(body.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(body.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(body.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(icon.material, Is.SameAs(authoredMaterial));
                Assert.That(shards.All(image => image.color.a == 0f), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void ChanceLostOverlayContent_CrackShardColor_DecaysBySpawnTiming()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;

            var firstShardColor = view.CrackShardVisibleColorForTests(0f);
            var middleShardColor = view.CrackShardVisibleColorForTests(0.55f);
            var lateShardColor = view.CrackShardVisibleColorForTests(1.1f);

            Assert.That(firstShardColor.r, Is.GreaterThan(middleShardColor.r));
            Assert.That(middleShardColor.r, Is.GreaterThan(lateShardColor.r));
            Assert.That(firstShardColor.g, Is.GreaterThan(middleShardColor.g));
            Assert.That(middleShardColor.g, Is.GreaterThan(lateShardColor.g));
            Assert.That(firstShardColor.b, Is.GreaterThan(middleShardColor.b));
            Assert.That(middleShardColor.b, Is.GreaterThan(lateShardColor.b));
        }

        [Test]
        public void ChanceLostOverlayContent_RootSequenceAggregatesMultipleSlotsAndCompletesAfterShardsAndSettleExactlyOnce()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;
            var completedCount = 0;
            var cancelledCount = 0;
            view.Completed += () => completedCount++;
            view.Cancelled += () => cancelledCount++;
            view.Bind(new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
                TransitionOverlayKind.ChanceLost,
                blockInput: true,
                showProgress: true,
                progress01: 0f,
                hasChanceLost: true,
                previousRemainingChances: 3,
                currentRemainingChances: 1,
                totalChances: 3,
                deathCount: 1));

            view.Show();
            var completedHandle = view.Playback;
            var sequence = typeof(ChanceLostOverlayContentView)
                .GetField("_lostChanceSequence", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(view);

            Assert.That(sequence, Is.Not.Null);
            Assert.That(view.CrackShardCountPerLostSlotForTests, Is.EqualTo(9));
            Assert.That(view.PostShatterSettleDurationSecondsForTests, Is.EqualTo(0.15f));
            Assert.That(view.IsCompleted, Is.False);
            Assert.That(view.IsCancelled, Is.False);

            var sequenceDuration = view.RootSequenceDurationSecondsForTests;
            view.GotoRootSequenceForTests(
                sequenceDuration - view.PostShatterSettleDurationSecondsForTests * 0.5f);
            Assert.That(view.IsCompleted, Is.False);

            view.GotoRootSequenceForTests(sequenceDuration);
            view.GotoRootSequenceForTests(sequenceDuration);

            Assert.That(view.IsCompleted, Is.True);
            Assert.That(view.IsCancelled, Is.False);
            Assert.That(completedCount, Is.EqualTo(1));
            Assert.That(cancelledCount, Is.Zero);

            view.Hide();
            Assert.That(cancelledCount, Is.Zero);
            Assert.That(
                completedHandle.Outcome,
                Is.EqualTo(TransitionContentPlaybackOutcome.Completed));
            view.ResetView();
            Assert.That(
                completedHandle.Outcome,
                Is.EqualTo(TransitionContentPlaybackOutcome.Completed));
        }

        [Test]
        public void ChanceLostOverlayContent_DuplicateShowDoesNotRestartAndResetCancels()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;
            var cancelledCount = 0;
            view.Cancelled += () => cancelledCount++;
            view.Bind(new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
                TransitionOverlayKind.ChanceLost,
                blockInput: true,
                showProgress: true,
                progress01: 0f,
                hasChanceLost: true,
                previousRemainingChances: 3,
                currentRemainingChances: 1,
                totalChances: 3,
                deathCount: 1));

            view.Show();
            var cancelledHandle = view.Playback;
            var firstSequence = typeof(ChanceLostOverlayContentView)
                .GetField("_lostChanceSequence", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(view);
            view.Show();
            var duplicateSequence = typeof(ChanceLostOverlayContentView)
                .GetField("_lostChanceSequence", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(view);

            Assert.That(duplicateSequence, Is.SameAs(firstSequence));
            Assert.That(view.ActiveLostChanceAnimationCountForTests, Is.EqualTo(1));

            view.ResetView();

            Assert.That(view.IsCompleted, Is.False);
            Assert.That(view.IsCancelled, Is.False);
            Assert.That(cancelledCount, Is.EqualTo(1));
            Assert.That(
                cancelledHandle.Outcome,
                Is.EqualTo(TransitionContentPlaybackOutcome.Cancelled));
            view.ResetView();
            Assert.That(
                cancelledHandle.Outcome,
                Is.EqualTo(TransitionContentPlaybackOutcome.Cancelled));
        }

        [Test]
        public void ChanceLostOverlayContent_InvalidPayloadCompletesImmediatelyWithoutDeadlock()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;
            var completedCount = 0;
            view.Completed += () => completedCount++;
            view.Bind(new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
                TransitionOverlayKind.ChanceLost,
                blockInput: true,
                showProgress: true,
                progress01: 0f,
                hasChanceLost: false,
                previousRemainingChances: 1,
                currentRemainingChances: 1,
                totalChances: 3,
                deathCount: 1));

            view.Show();

            Assert.That(view.IsCompleted, Is.True);
            Assert.That(view.IsCancelled, Is.False);
            Assert.That(completedCount, Is.EqualTo(1));
            Assert.That(view.ActiveLostChanceAnimationCountForTests, Is.Zero);
        }

        [Test]
        public void ChanceLostOverlayContent_ShowWithoutChanceLost_DoesNotAnimate()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;

            view.Bind(new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
                TransitionOverlayKind.ChanceLost,
                blockInput: true,
                showProgress: true,
                progress01: 0.25f,
                hasChanceLost: false,
                previousRemainingChances: 1,
                currentRemainingChances: 1,
                totalChances: 3,
                deathCount: 4));

            view.Show();

            Assert.That(view.ActiveLostChanceAnimationCountForTests, Is.Zero);
        }

        [Test]
        public void GenericLoadingOverlayContent_LevelFailedRestartDoesNotShowChanceLostFields()
        {
            using var content = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");

            content.View.Bind(Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart));

            Assert.That(content.Root.GetComponentsInChildren<ChanceLostOverlayContentView>(true), Is.Empty);
            Assert.That(
                content.Root.GetComponentsInChildren<TMP_Text>(true).Select(text => text.name),
                Does.Not.Contain("PreviousChanceText_TMP"));
        }

        [Test]
        public void TransitionOverlayShell_RejectsEventSystemChild()
        {
            using var shell = ShellHandle.Create();
            new GameObject("EventSystem", typeof(EventSystem)).transform.SetParent(shell.Root.transform, false);

            Assert.That(shell.View.CollectValidationIssues(), Has.Some.Contains("EventSystem"));
        }

        [Test]
        public void TransitionOverlayShell_OpaqueTakeoverRequiresExplicitFullScreenAcknowledgement()
        {
            using var shell = ShellHandle.Create();
            var blockerRect = shell.Blocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;

            shell.View.RequestOpaqueTakeover(Color.black);
            var targetCanvas = shell.Root.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.targetDisplay = 0;
            targetCanvas.sortingOrder = 5000;
            shell.Blocker.GetComponent<CanvasRenderer>().cull = false;

            Assert.That(shell.Blocker.activeInHierarchy, Is.True);
            Assert.That(shell.Blocker.GetComponent<Image>().color, Is.EqualTo(Color.black));
            Assert.That(shell.View.IsOpaqueHandoffReady, Is.False);

            Assert.Throws<InvalidOperationException>(
                () => shell.View.AcknowledgeOpaqueHandoffReady(),
                "Property setup in the request frame must not count as rendered coverage.");
            typeof(SceneTransitionOverlayShellView)
                .GetMethod(
                    "HandleWillRenderCanvases",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(shell.View, null);
            typeof(SceneTransitionOverlayShellView)
                .GetField(
                    "_opaqueRenderCallbackFrame",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(shell.View, Time.frameCount + 1);
            shell.View.AcknowledgeOpaqueHandoffReady();

            Assert.That(shell.View.IsOpaqueHandoffReady, Is.True);
            shell.View.HideAll();
            Assert.That(shell.View.IsOpaqueHandoffReady, Is.False);
        }

        [Test]
        public void TransitionOverlayShell_AuthoredBlueTakeoverForcesOpaqueColorWithoutMutatingRgb()
        {
            using var shell = ShellHandle.Create();
            var authoredBlue = new Color(0f, 0.25133762f, 0.4811321f, 0.2f);

            shell.View.RequestOpaqueTakeover(authoredBlue);

            var actual = shell.View.PersistentCoverColorForTests;
            Assert.That(actual.r, Is.EqualTo(authoredBlue.r).Within(0.000001f));
            Assert.That(actual.g, Is.EqualTo(authoredBlue.g).Within(0.000001f));
            Assert.That(actual.b, Is.EqualTo(authoredBlue.b).Within(0.000001f));
            Assert.That(actual.a, Is.EqualTo(1f));
            Assert.That(shell.Blocker.GetComponent<Image>().raycastTarget, Is.True);
        }

        [Test]
        public void TransitionOverlayShell_ContentPreservesExactAuthoredCoverColor()
        {
            using var shell = ShellHandle.Create();
            using var content =
                ContentHandle.Create<GenericLoadingOverlayContentView>(
                    "GenericLoadingOverlayContent");
            var authoredBlue = new Color(0f, 0.25133762f, 0.4811321f, 1f);

            shell.View.RequestOpaqueTakeover(authoredBlue);
            var mounted = shell.View.MountContent(content.View);
            shell.View.ShowContent(
                Model(
                    StageTransitionKind.StageClearNext,
                    TransitionOverlayKind.StageClear),
                mounted);

            Assert.That(
                shell.View.PersistentCoverColorForTests,
                Is.EqualTo(authoredBlue));
        }

        [Test]
        public void TransitionOverlayShell_OtherCanvasRenderDoesNotAcknowledgeDisabledTransitionCanvas()
        {
            using var shell = ShellHandle.Create();
            var blockerRect = shell.Blocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            shell.View.RequestOpaqueTakeover(Color.black);
            var transitionCanvas = shell.Root.GetComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.targetDisplay = 0;
            transitionCanvas.sortingOrder = 5000;
            shell.Blocker.GetComponent<CanvasRenderer>().cull = false;
            transitionCanvas.enabled = false;

            var otherCanvasRoot = new GameObject(
                "OtherCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            try
            {
                otherCanvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                typeof(SceneTransitionOverlayShellView)
                    .GetMethod(
                        "HandleWillRenderCanvases",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(shell.View, null);
                transitionCanvas.enabled = true;

                Assert.Throws<InvalidOperationException>(
                    () => shell.View.AcknowledgeOpaqueHandoffReady(),
                    "A global callback caused only by another Canvas must not acknowledge the transition Canvas.");
                Assert.That(shell.View.IsOpaqueHandoffReady, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherCanvasRoot);
            }
        }

        [TestCase(false, true, true, false, false)]
        [TestCase(true, false, true, false, false)]
        [TestCase(true, true, false, false, false)]
        [TestCase(true, true, true, true, false)]
        [TestCase(true, true, true, false, true)]
        public void SceneTransitionCoordinator_ExplicitActivationJoinsExactlyThreeReadinessFlags(
            bool opaqueReady,
            bool chanceLostCompleted,
            bool asyncLoadReady,
            bool cancelled,
            bool expected)
        {
            Assert.That(
                SceneTransitionCoordinator.CanActivateExplicitTransition(
                    opaqueReady,
                    chanceLostCompleted,
                    asyncLoadReady,
                    cancelled),
                Is.EqualTo(expected));
        }

        private static SceneTransitionOverlayModel Model(
            StageTransitionKind transitionKind,
            TransitionOverlayKind overlayKind)
        {
            return new SceneTransitionOverlayModel(
                transitionKind,
                overlayKind,
                blockInput: true,
                showProgress: true,
                progress01: 0f,
                hasChanceLost: false,
                previousRemainingChances: 0,
                currentRemainingChances: 0,
                totalChances: 0,
                deathCount: 0);
        }

        private static Image FindEffectImage(RectTransform slot)
        {
            return FindImage(slot, "Effect");
        }

        private static Image FindImage(RectTransform slot, string name)
        {
            var images = slot.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i].name == name)
                {
                    return images[i];
                }
            }

            throw new AssertionException($"Expected chance slot to contain a {name} image.");
        }

        private static int CountCrackLines(RectTransform root)
        {
            return CountImagesByPrefix(root, "CrackLine");
        }

        private static int CountCrackShards(RectTransform root)
        {
            return CountImagesByPrefix(root, "CrackShard");
        }

        private static int CountImagesByPrefix(RectTransform root, string prefix)
        {
            var images = root.GetComponentsInChildren<Image>(true);
            var count = 0;
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i].name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertCrackShardsRestored(RectTransform root)
        {
            var images = root.GetComponentsInChildren<Image>(true);
            var shardCount = 0;
            var largeShardCount = 0;
            for (var i = 0; i < images.Length; i++)
            {
                if (!images[i].name.StartsWith("CrackShard", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                shardCount++;
                Assert.That(images[i].color.a, Is.EqualTo(0f));
                Assert.That(images[i].raycastTarget, Is.False);
                Assert.That(images[i].rectTransform.sizeDelta.x, Is.GreaterThanOrEqualTo(9f));
                Assert.That(images[i].rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(9f));
                if (images[i].rectTransform.sizeDelta.x >= 20f &&
                    images[i].rectTransform.sizeDelta.y >= 20f)
                {
                    largeShardCount++;
                }
                Assert.That(images[i].rectTransform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(images[i].GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(images[i].GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);

                var anchoredPosition = images[i].rectTransform.anchoredPosition;
                Assert.That(Mathf.Abs(anchoredPosition.x), Is.LessThanOrEqualTo(100f));
                Assert.That(Mathf.Abs(anchoredPosition.y), Is.LessThanOrEqualTo(100f));
            }

            Assert.That(shardCount, Is.EqualTo(9));
            Assert.That(largeShardCount, Is.GreaterThanOrEqualTo(2),
                "The early fracture needs visible chunks, not only fine debris.");
        }

        private static CatalogEntrySpec Entry(
            StageTransitionKind transitionKind,
            SceneTransitionOverlayContentView prefab)
        {
            return new CatalogEntrySpec(transitionKind, prefab);
        }

        private readonly struct CatalogEntrySpec
        {
            public CatalogEntrySpec(
                StageTransitionKind transitionKind,
                SceneTransitionOverlayContentView prefab)
            {
                TransitionKind = transitionKind;
                Prefab = prefab;
            }

            public StageTransitionKind TransitionKind { get; }
            public SceneTransitionOverlayContentView Prefab { get; }
        }

        private sealed class CatalogHandle : IDisposable
        {
            private CatalogHandle(SceneTransitionOverlayContentCatalog catalog)
            {
                Catalog = catalog;
            }

            public SceneTransitionOverlayContentCatalog Catalog { get; }

            public static CatalogHandle Create(params CatalogEntrySpec[] specs)
            {
                var catalog = ScriptableObject.CreateInstance<SceneTransitionOverlayContentCatalog>();
                var serialized = new SerializedObject(catalog);
                var entries = serialized.FindProperty("_entries");
                entries.arraySize = specs.Length;
                for (var i = 0; i < specs.Length; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("_transitionKind").enumValueIndex = (int)specs[i].TransitionKind;
                    entry.FindPropertyRelative("_contentPrefab").objectReferenceValue = specs[i].Prefab;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                return new CatalogHandle(catalog);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Catalog);
            }
        }

        private sealed class ShellHandle : IDisposable
        {
            private ShellHandle(
                GameObject root,
                SceneTransitionOverlayShellView view,
                GameObject blocker,
                GameObject visualRoot,
                Transform contentMount)
            {
                Root = root;
                View = view;
                Blocker = blocker;
                VisualRoot = visualRoot;
                ContentMount = contentMount;
            }

            public GameObject Root { get; }
            public SceneTransitionOverlayShellView View { get; }
            public GameObject Blocker { get; }
            public GameObject VisualRoot { get; }
            public Transform ContentMount { get; }

            public static ShellHandle Create()
            {
                var root = new GameObject("Shell", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
                var view = root.AddComponent<SceneTransitionOverlayShellView>();
                var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
                blocker.transform.SetParent(root.transform, false);
                var visualRoot = new GameObject("VisualRoot", typeof(RectTransform), typeof(CanvasGroup));
                visualRoot.transform.SetParent(root.transform, false);
                var contentMount = new GameObject("ContentMount", typeof(RectTransform));
                contentMount.transform.SetParent(visualRoot.transform, false);

                var serialized = new SerializedObject(view);
                serialized.FindProperty("_canvas").objectReferenceValue = root.GetComponent<Canvas>();
                serialized.FindProperty("_rootGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                serialized.FindProperty("_blocker").objectReferenceValue = blocker;
                serialized.FindProperty("_blockerImage").objectReferenceValue = blocker.GetComponent<Image>();
                serialized.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
                serialized.FindProperty("_visualGroup").objectReferenceValue = visualRoot.GetComponent<CanvasGroup>();
                serialized.FindProperty("_contentMount").objectReferenceValue = contentMount.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                view.HideAll();
                return new ShellHandle(root, view, blocker, visualRoot, contentMount.transform);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class ContentHandle : IDisposable
        {
            private ContentHandle(
                GameObject root,
                SceneTransitionOverlayContentView view,
                RectTransform[] chanceSlots,
                TMP_Text progressText,
                TMP_Text remainingChancesLabel,
                TMP_Text loadingLabel)
            {
                Root = root;
                View = view;
                ChanceSlots = chanceSlots;
                ProgressText = progressText;
                RemainingChancesLabel = remainingChancesLabel;
                LoadingLabel = loadingLabel;
            }

            public GameObject Root { get; }
            public SceneTransitionOverlayContentView View { get; }
            public RectTransform[] ChanceSlots { get; }
            public TMP_Text ProgressText { get; }
            public TMP_Text RemainingChancesLabel { get; }
            public TMP_Text LoadingLabel { get; }

            public static ContentHandle Create<T>(string name, bool bindChanceSlotRoots = true)
                where T : SceneTransitionOverlayContentView
            {
                var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
                var view = root.AddComponent<T>();
                var progressText = CreateText(root.transform, "ProgressText_TMP");

                var chanceSlots = Array.Empty<RectTransform>();
                TMP_Text remainingChancesLabel = null;
                TMP_Text loadingLabel = null;

                var serialized = new SerializedObject(view);
                serialized.FindProperty("_rootGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                serialized.FindProperty("_progressText").objectReferenceValue = progressText;

                if (view is GenericLoadingOverlayContentView)
                {
                    loadingLabel = CreateText(root.transform, "LoadingLabel_TMP");
                    serialized.FindProperty("_loadingLabel").objectReferenceValue = loadingLabel;
                    serialized.FindProperty("_typographyTheme").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemePath);
                }
                else if (view is ChanceLostOverlayContentView)
                {
                    remainingChancesLabel = CreateText(root.transform, "RemainingChancesLabel_TMP");
                    loadingLabel = CreateText(root.transform, "LoadingLabel_TMP");
                    serialized.FindProperty("_remainingChancesLabel").objectReferenceValue =
                        remainingChancesLabel;
                    serialized.FindProperty("_loadingLabel").objectReferenceValue = loadingLabel;
                    serialized.FindProperty("_typographyTheme").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemePath);
                    chanceSlots = CreateChanceSlots(root.transform);
                    if (bindChanceSlotRoots)
                    {
                        var chanceSlotRoots = serialized.FindProperty("_chanceSlotRoots");
                        chanceSlotRoots.arraySize = chanceSlots.Length;
                        for (var i = 0; i < chanceSlots.Length; i++)
                        {
                            chanceSlotRoots.GetArrayElementAtIndex(i).objectReferenceValue = chanceSlots[i];
                        }
                    }
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                return new ContentHandle(
                    root,
                    view,
                    chanceSlots,
                    progressText,
                    remainingChancesLabel,
                    loadingLabel);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }

            private static TMP_Text CreateText(Transform parent, string name)
            {
                var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(parent, false);
                return textObject.GetComponent<TMP_Text>();
            }

            private static RectTransform[] CreateChanceSlots(Transform parent)
            {
                var row = new GameObject("BottomRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(parent, false);
                var slots = new RectTransform[3];
                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = new GameObject($"ChanceSlotView {i}", typeof(RectTransform), typeof(CanvasGroup));
                    slot.transform.SetParent(row.transform, false);
                    var rect = slot.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2((i + 1) * 48f, 0f);
                    var visual = new GameObject("FilledIcon", typeof(RectTransform), typeof(Image));
                    visual.transform.SetParent(slot.transform, false);
                    var effect = new GameObject("Effect", typeof(RectTransform), typeof(Image));
                    effect.transform.SetParent(slot.transform, false);
                    effect.GetComponent<Image>().color = new Color(1f, 0.1f, 0.1f, 0.12f);
                    slots[i] = rect;
                }

                return slots;
            }
        }
    }
}
