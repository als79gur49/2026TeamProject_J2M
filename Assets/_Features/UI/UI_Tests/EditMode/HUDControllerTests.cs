using Game.Feature.Gameplay.UIAccess.Presentation;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class HUDControllerTests
    {
        private const string AllIn1UiMaskShaderName = "AllIn1SpriteShader/AllIn1SpriteShaderUiMask";

        [TestCase(false)]
        [TestCase(true)]
        public void PlayerHudContract_RepeatedFrameDoesNotNotifyOrResurrectCompletedRow(bool satisfied)
        {
            using var fixture = new PlayerHudContractFixture();
            fixture.Publish(2, satisfied);
            fixture.ResetCounts();
            fixture.Publish(2, satisfied);
            Assert.That(fixture.SnapshotChanges, Is.Zero);
            Assert.That(fixture.ObjectiveChanges, Is.Zero);
            Assert.That(fixture.EventCount, Is.Zero);
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.EqualTo(satisfied));
            fixture.AssertVisibleState();
            fixture.Publish(2, satisfied);
            Assert.That(fixture.SnapshotChanges, Is.Zero);
            Assert.That(fixture.ObjectiveChanges, Is.Zero);
            if (satisfied) fixture.FinishDismiss();
            fixture.Publish(3, satisfied);
            Assert.That(fixture.Source.CurrentSnapshot.Tick.LastReducedTickIndex, Is.EqualTo(3));
            Assert.That(fixture.SnapshotChanges, Is.EqualTo(1));
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.False);
            if (satisfied) Assert.That(fixture.ActiveRows, Is.Empty);
            else fixture.AssertVisibleState();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlayerHudContract_RepeatedQueryRefreshDoesNotNotifyOrResurrectCompletedRow(bool satisfied)
        {
            using var fixture = new PlayerHudContractFixture();
            fixture.Publish(2, satisfied);
            fixture.ResetCounts();
            for (var i = 0; i < 3; i++)
                fixture.RefreshPlayer();

            Assert.That(fixture.SnapshotChanges, Is.Zero);
            Assert.That(fixture.ObjectiveChanges, Is.Zero);
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.EqualTo(satisfied));
            Assert.That(fixture.Source.CurrentSnapshot.Tick.LastReducedTickIndex, Is.EqualTo(2));
            Assert.That(fixture.Source.CurrentSnapshot.Chance.RemainingChances, Is.EqualTo(2));
            Assert.That(fixture.EventCount, Is.Zero);
            fixture.AssertVisibleState();
            if (satisfied) fixture.FinishDismiss();
            fixture.RefreshPlayer();
            Assert.That(fixture.SnapshotChanges, Is.Zero);
            fixture.Publish(3, satisfied);
            Assert.That(fixture.SnapshotChanges, Is.EqualTo(1));
            Assert.That(fixture.ObjectiveChanges, Is.EqualTo(satisfied ? 1 : 0));
            Assert.That(fixture.EventCount, Is.Zero);
            Assert.That(fixture.Source.CurrentSnapshot.Tick.LastReducedTickIndex, Is.EqualTo(3));
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.False);
            if (satisfied) Assert.That(fixture.ActiveRows, Is.Empty);
            else fixture.AssertVisibleState();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlayerHudContract_SameTickFramePlayerChangesWithoutEventsPreserveCompletionUntilNextTick(bool satisfied)
        {
            using var fixture = new PlayerHudContractFixture();
            fixture.Publish(2, satisfied, MakeEventlessPlayer(10, GameplayUiActionKind.None, false));
            fixture.ResetCounts();
            var variants = new[]
            {
                MakeEventlessPlayer(11, GameplayUiActionKind.None, false),
                MakeEventlessPlayer(11, GameplayUiActionKind.Push, false),
                MakeEventlessPlayer(11, GameplayUiActionKind.Push, true),
            };
            foreach (var player in variants)
            {
                fixture.Publish(2, satisfied, player);
                Assert.That(fixture.SnapshotChanges, Is.Zero);
                Assert.That(fixture.ObjectiveChanges, Is.Zero);
                Assert.That(fixture.EventCount, Is.Zero);
                Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.EqualTo(satisfied));
                fixture.AssertVisibleState();
            }

            // A frame in recovery followed by a query-only state refresh used to clear the pulse.
            fixture.RefreshPlayer();
            Assert.That(fixture.SnapshotChanges, Is.Zero);
            Assert.That(fixture.ObjectiveChanges, Is.Zero);
            Assert.That(fixture.EventCount, Is.Zero);
            Assert.That(fixture.Source.CurrentSnapshot.Tick.LastReducedTickIndex, Is.EqualTo(2));
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.EqualTo(satisfied));
            if (satisfied) fixture.FinishDismiss();
            fixture.Publish(2, satisfied, variants[2]);
            fixture.RefreshPlayer();
            Assert.That(fixture.SnapshotChanges, Is.Zero);
            if (satisfied) Assert.That(fixture.ActiveRows, Is.Empty);
            else fixture.AssertVisibleState();

            fixture.Publish(3, satisfied, variants[2]);
            Assert.That(fixture.SnapshotChanges, Is.EqualTo(1));
            Assert.That(fixture.ObjectiveChanges, Is.EqualTo(satisfied ? 1 : 0));
            Assert.That(fixture.Source.CurrentSnapshot.Tick.LastReducedTickIndex, Is.EqualTo(3));
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.False);
            Assert.That(fixture.EventCount, Is.Zero);
            if (satisfied) Assert.That(fixture.ActiveRows, Is.Empty);
            else fixture.AssertVisibleState();
        }

        [TestCase(true, 4, 5, 3, 3, true)]
        [TestCase(true, -1, -2, 3, 3, true)]
        [TestCase(false, 4, 5, 3, 9, true)]
        [TestCase(true, -1, -2, 0, 0, true)]
        [TestCase(true, 4, 5, 3, 3, false)]
        public void ChanceContract_NormalizedEqualRefreshPreservesCompletionUntilNextTick(
            bool hasChances, int before, int after, int maxBefore, int maxAfter, bool queryRefresh)
        {
            using var fixture = new PlayerHudContractFixture();
            fixture.Query.SetPlayerHud(new GameplayPlayerHudReadModel(hasChances, before, maxBefore));
            fixture.Publish(2, true);
            var snapshot = fixture.Source.CurrentSnapshot;
            var hint = fixture.Chance.ViewModel.AnimationHint;
            fixture.ResetCounts();

            fixture.Query.SetPlayerHud(new GameplayPlayerHudReadModel(hasChances, after, maxAfter));
            if (queryRefresh) fixture.RefreshState();
            else fixture.Publish(2, true);

            Assert.That(fixture.Source.CurrentSnapshot, Is.EqualTo(snapshot));
            Assert.That(fixture.SnapshotChanges, Is.Zero);
            Assert.That(fixture.ObjectiveChanges, Is.Zero);
            Assert.That(fixture.EventCount, Is.Zero);
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.True);
            Assert.That(fixture.Chance.ViewModel.AnimationHint, Is.EqualTo(hint));
            fixture.FinishDismiss();
            fixture.RefreshState();
            fixture.Publish(2, true);
            Assert.That(fixture.ActiveRows, Is.Empty);
            Assert.That(fixture.SnapshotChanges, Is.Zero);

            fixture.Publish(3, true);
            Assert.That(fixture.SnapshotChanges, Is.EqualTo(1));
            Assert.That(fixture.ObjectiveChanges, Is.EqualTo(1));
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.False);
            Assert.That(fixture.ActiveRows, Is.Empty);
        }

        private static GameplayPlayerPresentationSlice MakeEventlessPlayer(
            int playerEntityId, GameplayUiActionKind actionKind, bool isRecoveryPhase)
        {
            return new GameplayPlayerPresentationSlice(
                playerEntityId, actionKind, activeActionSequence: 7,
                actionDirection: GameplayUiDirection.Right, targetEntityId: 22,
                startedThisTick: false, executedThisTick: false, completedThisTick: false,
                canceledThisTick: false, isRecoveryPhase: isRecoveryPhase,
                resolutionKind: GameplayUiActionResolutionKind.None,
                shouldPlayWalkLoop: false, moveMotionGeneratedThisTick: false,
                waitingForNextMoveCadence: false, tookDamageThisTick: false, damageAmount: 0);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void PlayerHudContract_LocaleOrReplacementAfterSatisfactionKeepsView(bool replace, bool normalizedRefresh)
        {
            using var fixture = new PlayerHudContractFixture();
            if (normalizedRefresh) fixture.Query.SetPlayerHud(new GameplayPlayerHudReadModel(true, 4, 3));
            fixture.Publish(2, true);
            if (normalizedRefresh)
            {
                fixture.ResetCounts();
                fixture.Query.SetPlayerHud(new GameplayPlayerHudReadModel(true, 5, 3));
                fixture.RefreshState();
                Assert.That(fixture.SnapshotChanges, Is.Zero);
            }
            Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.True);
            if (replace)
            {
                var previousObjectiveId = fixture.Objective.ViewModel.ObjectiveStableId;
                fixture.Query.SetStage(new GameplayStageReadModel(StageId.CreateOrThrow("stage-1-2"), "stage.stage-1-2.display_name"));
                fixture.Publish(2, false);
                Assert.That(fixture.Objective.ViewModel.ObjectiveStableId, Is.Not.EqualTo(previousObjectiveId));
                Assert.That(fixture.Objective.ViewModel.Rows[0].IsSatisfied, Is.False);
                Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.False);
                fixture.FinishEnter();
                fixture.AssertVisibleState(normalizedRefresh ? 3 : 2);
            }
            else
            {
                var dismissingRow = fixture.ActiveRows.Single();
                var previousRowText = dismissingRow.GetComponentInChildren<TMP_Text>(true).text;
                fixture.Locale.ChangeLocale("ko-KR");
                Assert.That(fixture.Objective.ViewModel.HeaderText, Does.StartWith("ko-KR:"));
                Assert.That(fixture.Objective.ViewModel.Rows[0].Text, Does.StartWith("ko-KR:"));
                Assert.That(fixture.Objective.ViewModel.Rows[0].JustSatisfied, Is.False);
                Assert.That(fixture.View.HeaderLabel.text, Is.EqualTo(fixture.Objective.ViewModel.HeaderText));
                Assert.That(new[] { ObjectiveRowVisualState.Completing, ObjectiveRowVisualState.WaitingForOut,
                    ObjectiveRowVisualState.Collapsing }, Does.Contain(dismissingRow.VisualState));
                Assert.That(dismissingRow.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(previousRowText),
                    "Existing View behavior freezes outgoing row text during its transition.");
                fixture.FinishDismiss();
                fixture.Locale.ChangeLocale("en-US");
                Assert.That(fixture.ActiveRows, Is.Empty);
            }
        }

        [Test]
        public void PlayerHudContract_DamageAndActionReplayKeepEventOrder()
        {
            using var fixture = new PlayerHudContractFixture();
            var player = new GameplayPlayerPresentationSlice(
                playerEntityId: 10, activeActionKind: GameplayUiActionKind.Push, activeActionSequence: 7,
                actionDirection: GameplayUiDirection.Right, targetEntityId: 22,
                startedThisTick: true, executedThisTick: true, completedThisTick: false,
                canceledThisTick: false, isRecoveryPhase: false,
                resolutionKind: GameplayUiActionResolutionKind.Success,
                shouldPlayWalkLoop: false, moveMotionGeneratedThisTick: false,
                waitingForNextMoveCadence: false, tookDamageThisTick: true, damageAmount: 1);
            var frame = new GameplayPresentationFrame(2, new GameplayUiTopology(GameplayUiFace.Floor), player: player);
            var expectedEvents = new UITickEventRouter().Route(frame);
            Assert.That(expectedEvents.Count, Is.GreaterThan(0));
            fixture.ResetCounts();
            fixture.Publish(2, false, player);
            Assert.That(fixture.Order, Is.EqualTo(new[] { "snapshot", "events" }));
            Assert.That(fixture.Events, Is.EqualTo(expectedEvents));
            Assert.That(fixture.Source.CurrentSnapshot.Player.TookDamageThisTick, Is.True);
            Assert.That(fixture.Source.CurrentSnapshot.Player.LastDamageAmount, Is.EqualTo(1));
            var events = fixture.EventCount;
            fixture.Publish(2, false, player);
            Assert.That(fixture.EventCount, Is.EqualTo(events));
            fixture.Publish(3, false);
            Assert.That(fixture.Source.CurrentSnapshot.Player.TookDamageThisTick, Is.False);
            fixture.AssertVisibleState();
        }

        private sealed class PlayerHudContractFixture : IDisposable
        {
            private readonly GameObject _root = new GameObject("PlayerHudContractFixture");
            private readonly FakeGameplayPresentationFeed _feed = new FakeGameplayPresentationFeed();
            private readonly HUDRootPresenter _presenter;
            private readonly HUDController _controller;
            private readonly RectTransform _list;
            private readonly RectTransform _template;
            public readonly FakeGameplayQueryFacade Query;
            public readonly GameplayUiPresentationSource Source;
            public readonly ProbeLocaleResolver Locale = new ProbeLocaleResolver();
            public readonly ObjectiveHudPresenter Objective;
            public readonly ChancePanelPresenter Chance = new ChancePanelPresenter();
            public readonly ObjectiveHudView View;
            public int SnapshotChanges;
            public int ObjectiveChanges;
            public int EventCount;
            public readonly List<string> Order = new List<string>();
            public readonly List<UITickEvent> Events = new List<UITickEvent>();
            public ObjectiveHudRowView[] ActiveRows => _list.Cast<Transform>()
                .Where(child => child != _template && child.gameObject.activeSelf)
                .Select(child => child.GetComponent<ObjectiveHudRowView>()).Where(row => row != null).ToArray();

            public PlayerHudContractFixture()
            {
                Query = new FakeGameplayQueryFacade(new GameplaySessionReadModel(1, false, true, false),
                    MakePlayer(), MakeObjective(false),
                    new GameplayStageReadModel(StageId.CreateOrThrow("stage-1-1"), "stage.stage-1-1.display_name"));
                Source = new GameplayUiPresentationSource(Query, _feed, new FakeGameplayPauseService());
                Objective = new ObjectiveHudPresenter(Locale);
                var stage = new StageInfoPresenter(Locale);
                var belt = new SurfaceBeltIndicatorPresenter();
                _presenter = new HUDRootPresenter(Source, stage, Objective, Chance, belt);
                _controller = new HUDController(_presenter.ViewModel, stage.ViewModel, Objective.ViewModel, Chance.ViewModel, belt.ViewModel);
                CreateCanonicalRootView(_root, out var hud);
                View = hud.ObjectiveHudView;
                _list = GetSerializedReference<RectTransform>(View, "_objectiveListRoot");
                _template = GetSerializedReference<RectTransform>(View, "_objectiveItemTemplate");
                _controller.AttachView(hud);
                Publish(1, false);
                FinishEnter();
                Source.SnapshotChanged += _ => { SnapshotChanges++; Order.Add("snapshot"); };
                Objective.ViewModel.Changed += () => ObjectiveChanges++;
                Source.TickEventsApplied += batch =>
                {
                    Order.Add("events");
                    EventCount += batch.Events.Count;
                    Events.AddRange(batch.Events);
                };
                ResetCounts();
            }

            public void ResetCounts()
            {
                SnapshotChanges = ObjectiveChanges = EventCount = 0;
                Order.Clear();
                Events.Clear();
            }

            public void Publish(int tick, bool satisfied, GameplayPlayerPresentationSlice? player = null)
            {
                Query.SetObjective(MakeObjective(satisfied));
                _feed.PublishFrame(new GameplayPresentationFrame(tick, new GameplayUiTopology(GameplayUiFace.Floor), player: player));
            }

            public void RefreshPlayer()
            {
                Query.SetPlayerHud(new GameplayPlayerHudReadModel(
                    hasRemainingChances: true, remainingChances: 2, maxChances: 3));
                RefreshState();
            }

            public void RefreshState()
            {
                _feed.PublishState(new GameplayPresentationState(new GameplayUiTopology(GameplayUiFace.Floor),
                    isPresentationActive: false, hasBlockingPresentation: false, isTopologyTransitionActive: false));
            }

            private static GameplayPlayerHudReadModel MakePlayer() => new GameplayPlayerHudReadModel(
                hasRemainingChances: true, remainingChances: 2, maxChances: 3);

            private static GameplayObjectiveReadModel MakeObjective(bool satisfied) => new GameplayObjectiveReadModel(
                true, satisfied, satisfied, false, new[]
                {
                    new GameplayObjectiveConditionReadModel("goal", GameplayObjectivePresentationKind.ReachExit,
                        "goal", GameplayObjectiveConditionRole.PrimaryGoal, true, satisfied, satisfied ? 1 : 0, 1, 0),
                });

            public void FinishEnter()
            {
                ForceObjectiveSchedulerDue(View);
                foreach (var row in ActiveRows) CompleteObjectiveEnter(View, row);
                Assert.That(ActiveRows.Length, Is.EqualTo(1));
            }

            public void FinishDismiss()
            {
                ForceObjectiveSchedulerDue(View);
                foreach (var row in ActiveRows) CompleteObjectiveDismiss(View, row);
                Assert.That(ActiveRows.Length, Is.Zero);
            }

            public void AssertVisibleState(int expectedRemainingChances = 2)
            {
                Assert.That(View.HeaderLabel.text, Is.EqualTo(Objective.ViewModel.HeaderText));
                Assert.That(Chance.ViewModel.RemainingChances, Is.EqualTo(expectedRemainingChances));
                Assert.That(ActiveRows.Length, Is.EqualTo(1));
                Assert.That(ActiveRows[0].GetComponentInChildren<TMP_Text>(true).text,
                    Is.EqualTo(Objective.ViewModel.Rows[0].Text));
            }

            public void Dispose()
            {
                _controller.Dispose();
                _presenter.Dispose();
                Source.Dispose();
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private sealed class ProbeLocaleResolver : ILocalizedTextResolver
        {
            public string CurrentLocaleCode { get; private set; } = "en-US";
            public event Action LocaleChanged;
            public string Resolve(LocalizedTextDescriptor descriptor) => CurrentLocaleCode + ":" + descriptor.Key;
            public void ChangeLocale(string locale)
            {
                CurrentLocaleCode = locale;
                LocaleChanged?.Invoke();
            }
        }

        [Test]
        public void HUDController_AttachView_BindsChildViewModels()
        {
            var rootObject = new GameObject("HUDController_AttachView_BindsChildViewModels");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                var stageInfoPresenter = new StageInfoPresenter(new StaticLocalizedTextResolver("Stage 1-1"));
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot());

                Assert.That(hudView.ViewModel, Is.SameAs(controller.RootViewModel));
                Assert.That(hudView.StageInfoViewModel, Is.SameAs(controller.StageInfoViewModel));
                Assert.That(hudView.ObjectiveHudView.ViewModel, Is.SameAs(controller.ObjectiveHudViewModel));
                Assert.That(hudView.ChancePanelView.ViewModel, Is.SameAs(controller.ChancePanelViewModel));
                Assert.That(hudView.SurfaceBeltIndicatorView.ViewModel, Is.SameAs(controller.SurfaceBeltViewModel));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_BindsChancePanelViewModel()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_BindsChancePanelViewModel");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(hasRemainingChances: true, remainingChances: 2, maxChances: 3));

                Assert.That(hudView.ChancePanelView, Is.Not.Null);
                Assert.That(hudView.ChancePanelView.ViewModel, Is.SameAs(chancePanelPresenter.ViewModel));
                Assert.That(chancePanelPresenter.ViewModel.HasChances, Is.True);
                Assert.That(chancePanelPresenter.ViewModel.RemainingChances, Is.EqualTo(2));
                Assert.That(chancePanelPresenter.ViewModel.MaxChances, Is.EqualTo(3));
                Assert.That(hudView.ChancePanelView.GetComponentsInChildren<ChanceSlotView>(true).Length, Is.EqualTo(3));
                Assert.That(CountSlotsWithChild(hudView.ChancePanelView, "Glow"), Is.EqualTo(3));

                source.PublishSnapshot(CreateSnapshot(hasRemainingChances: true, remainingChances: 1, maxChances: 3));

                Assert.That(hudView.ChancePanelView.GetComponentsInChildren<ChanceSlotView>(true).Length, Is.EqualTo(3));
                Assert.That(CountSlotsWithChild(hudView.ChancePanelView, "Glow"), Is.EqualTo(3));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDRootView_CanonicalPrefab_ValidateAuthoredStructurePasses()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();

            Assert.DoesNotThrow(() => hudPrefab.ValidateAuthoredStructureOrThrow());
        }

        [Test]
        public void HUDRootView_MissingRequiredChildReference_ThrowsAuthoredContractFailure()
        {
            var rootObject = new GameObject("HUDRootView_MissingRequiredChildReference_ThrowsAuthoredContractFailure");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                LogAssert.Expect(
                    LogType.Warning,
                    "HUDRootView on 'GameplayHudRoot(Clone)' is missing serialized reference '_chancePanelView'.");
                SetSerializedReference(hudView, "_chancePanelView", null);

                var exception = Assert.Throws<InvalidOperationException>(() => hudView.ValidateAuthoredStructureOrThrow());
                Assert.That(exception.Message, Does.Contain("_chancePanelView"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_ArrangesVisibleHudElementsIntoStacks()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_ArrangesVisibleHudElementsIntoStacks");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(
                    hasRemainingChances: true,
                    remainingChances: 2,
                    maxChances: 3,
                    stageDisplayNameKey: "stage.stage-1-1.display_name"));

                var topLeftStack = FindRequiredRect(hudView.transform, "HudTopLeftStack");
                var topCenterStack = FindRequiredRect(hudView.transform, "HudTopCenterStack");
                var topRightStack = FindRequiredRect(hudView.transform, "HudTopRightStack");
                var bottomRightStack = FindRequiredRect(hudView.transform, "HudBottomRightStack");

                AssertStackTransform(topLeftStack, new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(24.0f, -24.0f));
                AssertStackTransform(topCenterStack, new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f), new Vector2(0.0f, -24.0f));
                AssertStackTransform(topRightStack, new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(-24.0f, -24.0f));
                AssertStackTransform(bottomRightStack, new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(-24.0f, 24.0f));

                var objectiveListRoot = GetSerializedReference<RectTransform>(hudView.ObjectiveHudView, "_objectiveListRoot");
                AssertOwnedBy(objectiveListRoot, topLeftStack);
                AssertOwnedBy(GetSerializedReference<TMP_Text>(hudView, "_stageNameLabel").transform, topRightStack);
                AssertOwnedBy(GetSerializedReference<Button>(hudView, "_pauseButton").transform, topRightStack);
                AssertOwnedBy(hudView.SurfaceBeltIndicatorView.transform, topRightStack);
                AssertOwnedBy(hudView.ChancePanelView.transform, topCenterStack);
                Assert.That(hudView.ChancePanelView.ViewModel, Is.SameAs(chancePanelPresenter.ViewModel));
                Assert.That(hudView.SurfaceBeltIndicatorView.ViewModel, Is.SameAs(surfaceBeltIndicatorPresenter.ViewModel));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [TestCase(1920.0f, 1080.0f)]
        [TestCase(1280.0f, 720.0f)]
        [TestCase(1440.0f, 1080.0f)]
        [TestCase(1080.0f, 1080.0f)]
        public void HUDController_CanonicalPrefab_HudStacksDoNotOverlapAtSupportedLandscapeResolutions(
            float width,
            float height)
        {
            var rootObject = new GameObject(
                $"HUDController_CanonicalPrefab_HudStacksDoNotOverlapAtSupportedLandscapeResolutions_{width}_{height}",
                typeof(RectTransform));

            try
            {
                var rootRect = (RectTransform)rootObject.transform;
                rootRect.sizeDelta = new Vector2(width, height);

                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(
                    hasRemainingChances: true,
                    remainingChances: 2,
                    maxChances: 3,
                    stageDisplayNameKey: "stage.stage-1-1.display_name"));

                var hudRect = (RectTransform)hudView.transform;
                hudRect.anchorMin = Vector2.zero;
                hudRect.anchorMax = Vector2.zero;
                hudRect.pivot = Vector2.zero;
                hudRect.sizeDelta = new Vector2(width, height);

                LayoutRebuilder.ForceRebuildLayoutImmediate(hudRect);
                Canvas.ForceUpdateCanvases();

                var topLeftStack = FindRequiredRect(hudView.transform, "HudTopLeftStack");
                var topRightStack = FindRequiredRect(hudView.transform, "HudTopRightStack");
                var bottomRightStack = FindRequiredRect(hudView.transform, "HudBottomRightStack");

                AssertNoOverlap(topLeftStack, topRightStack);
                AssertNoOverlap(topLeftStack, bottomRightStack);
                AssertNoOverlap(topRightStack, bottomRightStack);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_RuntimeChanceAndTopologyContentFitsWithinHudModules()
        {
            var rootObject = new GameObject(
                "HUDController_CanonicalPrefab_RuntimeChanceAndTopologyContentFitsWithinHudModules",
                typeof(RectTransform));

            try
            {
                var rootRect = (RectTransform)rootObject.transform;
                rootRect.sizeDelta = new Vector2(1280.0f, 720.0f);

                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(
                    hasRemainingChances: true,
                    remainingChances: 3,
                    maxChances: 3,
                    stageDisplayNameKey: "stage.stage-1-1.display_name"));

                var hudRect = (RectTransform)hudView.transform;
                hudRect.anchorMin = Vector2.zero;
                hudRect.anchorMax = Vector2.zero;
                hudRect.pivot = Vector2.zero;
                hudRect.sizeDelta = new Vector2(1280.0f, 720.0f);

                LayoutRebuilder.ForceRebuildLayoutImmediate(hudRect);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)hudView.ChancePanelView.transform);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)hudView.SurfaceBeltIndicatorView.transform);
                Canvas.ForceUpdateCanvases();

                var chancePanel = (RectTransform)hudView.ChancePanelView.transform;
                var slotContainer = FindRequiredRect(chancePanel, "SlotContainer");
                var floatingFeedbackRoot = FindRequiredRect(chancePanel, "FloatingFeedbackRoot");
                AssertWorldRectContains(chancePanel, slotContainer);
                AssertWorldRectContains(chancePanel, floatingFeedbackRoot);

                var beltView = hudView.SurfaceBeltIndicatorView;
                var maskRoot = GetSerializedReference<RectTransform>(beltView, "_maskRoot");
                var beltContent = GetSerializedReference<RectTransform>(beltView, "_beltContent");
                AssertOwnedBy(maskRoot, (RectTransform)beltView.transform);
                AssertOwnedBy(beltContent, maskRoot);
                Assert.That(maskRoot.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(beltView.Cells.Length, Is.EqualTo(SurfaceBeltViewModel.AuthoredCellCount));
                Assert.DoesNotThrow(() => beltView.ValidateAuthoredStructureOrThrow());

                var badgeGroups = beltView.GetComponentsInChildren<SurfaceBeltButtonBadgeGroupView>(true);
                Assert.That(badgeGroups.Length, Is.EqualTo(1));
                Assert.DoesNotThrow(() => badgeGroups[0].ValidateAuthoredStructureOrThrow());
                Assert.That(badgeGroups[0].NormalBadge.gameObject.activeInHierarchy, Is.True);
                Assert.That(((RectTransform)badgeGroups[0].transform).rect.width, Is.GreaterThanOrEqualTo(32.0f));
                Assert.That(((RectTransform)badgeGroups[0].transform).rect.height, Is.GreaterThanOrEqualTo(32.0f));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [TestCase(SurfaceBeltDirection.Forward)]
        [TestCase(SurfaceBeltDirection.Backward)]
        public void SurfaceBeltIndicator_PreservesAuthoredPositionDuringRefreshAndTransitions(
            SurfaceBeltDirection direction)
        {
            var rootObject = new GameObject("SurfaceBeltIndicator_AuthoredPosition");
            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var beltView = hudView.SurfaceBeltIndicatorView;
                var content = beltView.BeltContent;
                var authoredPosition = new Vector2(17.5f, 12.0f);
                content.anchoredPosition = authoredPosition;
                var presenter = new SurfaceBeltIndicatorPresenter();
                beltView.Bind(presenter.ViewModel);
                Assert.That(content.anchoredPosition, Is.EqualTo(authoredPosition));

                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                var step = Mathf.Abs(
                    ((RectTransform)beltView.Cells[4].transform).anchoredPosition.y -
                    ((RectTransform)beltView.Cells[3].transform).anchoredPosition.y);
                Assert.That(step, Is.GreaterThan(0.0f));
                var easeField = typeof(SurfaceBeltIndicatorView)
                    .GetField("_animationEase", BindingFlags.Instance | BindingFlags.NonPublic);
                easeField.SetValue(beltView, Enum.Parse(easeField.FieldType, "Linear"));
                SetPrivateField(beltView, "_animationDurationSeconds", 1.0f);

                presenter.Apply(new SurfaceBeltSnapshot(0, 0, 1, direction, true, 1));
                var moveField = typeof(SurfaceBeltIndicatorView)
                    .GetField("_moveTween", BindingFlags.Instance | BindingFlags.NonPublic);
                var move = moveField.GetValue(beltView);
                var tweenExtensions = moveField.FieldType.Assembly.GetType("DG.Tweening.TweenExtensions");
                Assert.That(tweenExtensions, Is.Not.Null);
                var gotoMethod = tweenExtensions.GetMethod("Goto", new[] { moveField.FieldType, typeof(float), typeof(bool) });
                var completeMethod = tweenExtensions.GetMethod("Complete", new[] { moveField.FieldType, typeof(bool) });
                Assert.That(gotoMethod, Is.Not.Null);
                Assert.That(completeMethod, Is.Not.Null);
                Assert.That(move, Is.Not.Null);
                gotoMethod.Invoke(null, new[] { move, (object)0.5f, false });
                Assert.That(content.anchoredPosition.x, Is.EqualTo(authoredPosition.x));
                Assert.That(content.anchoredPosition.y, Is.EqualTo(
                    authoredPosition.y + (direction == SurfaceBeltDirection.Forward ? -step : step) * 0.5f)
                    .Within(0.001f));

                completeMethod.Invoke(null, new[] { move, (object)true });
                Assert.That(content.anchoredPosition, Is.EqualTo(authoredPosition));
                presenter.Apply(new SurfaceBeltSnapshot(1, 1, 1, SurfaceBeltDirection.None, false, 1));
                Assert.That(content.anchoredPosition, Is.EqualTo(authoredPosition));

                presenter.Apply(new SurfaceBeltSnapshot(1, 1, 2, direction, true, 2));
                move = moveField.GetValue(beltView);
                gotoMethod.Invoke(null, new[] { move, (object)0.5f, false });
                presenter.Apply(new SurfaceBeltSnapshot(2, 2, 2, SurfaceBeltDirection.None, false, 2));
                Assert.That(content.anchoredPosition, Is.EqualTo(authoredPosition),
                    "An interrupted transition must restore the authored position.");
                beltView.Bind(new SurfaceBeltViewModel());
                Assert.That(content.anchoredPosition, Is.EqualTo(authoredPosition));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDPrefab_AuthorsChanceAndSurfaceBeltIndicatorModulesInPrefabHierarchy()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();
            var serializedChancePanel = GetSerializedReference<ChancePanelView>(hudPrefab, "_chancePanelView");
            var serializedSurfaceBeltIndicator = GetSerializedReference<SurfaceBeltIndicatorView>(hudPrefab, "_surfaceBeltIndicatorView");

            var chancePanels = hudPrefab.GetComponentsInChildren<ChancePanelView>(true);
            Assert.That(chancePanels.Length, Is.EqualTo(1));
            Assert.That(chancePanels[0], Is.SameAs(serializedChancePanel));

            var surfaceIndicators = hudPrefab.GetComponentsInChildren<SurfaceBeltIndicatorView>(true);
            Assert.That(surfaceIndicators.Length, Is.EqualTo(1));
            Assert.That(surfaceIndicators[0], Is.SameAs(serializedSurfaceBeltIndicator));

            var topCenterStack = FindRequiredRect(hudPrefab.transform, "HudTopCenterStack");
            var topRightStack = FindRequiredRect(hudPrefab.transform, "HudTopRightStack");
            AssertOwnedBy(GetSerializedReference<TMP_Text>(hudPrefab, "_stageNameLabel").transform, topRightStack);
            AssertOwnedBy(GetSerializedReference<Button>(hudPrefab, "_pauseButton").transform, topRightStack);
            AssertOwnedBy(serializedSurfaceBeltIndicator.transform, topRightStack);
            AssertOwnedBy(serializedChancePanel.transform, topCenterStack);

            var slotContainer = FindRequiredRect(serializedChancePanel.transform, "SlotContainer");
            var slotViews = serializedChancePanel.GetComponentsInChildren<ChanceSlotView>(true);
            Assert.That(slotViews.Length, Is.EqualTo(3));
            AssertSerializedReference(serializedChancePanel, "_slotContainer", slotContainer);
            AssertSerializedArrayCount(serializedChancePanel, "_slotViews", 3);

            foreach (var slot in slotViews)
            {
                AssertOwnedBy(slot.transform, slotContainer);
                AssertSerializedReferenceIsAssigned(slot, "_filledIcon");
                AssertSerializedReferenceIsAssigned(slot, "_emptyIcon");
                AssertSerializedReferenceIsAssigned(slot, "_glow");
                AssertSerializedReferenceIsAssigned(slot, "_canvasGroup");
            }

            var maskRoot = GetSerializedReference<RectTransform>(serializedSurfaceBeltIndicator, "_maskRoot");
            var beltContent = GetSerializedReference<RectTransform>(serializedSurfaceBeltIndicator, "_beltContent");
            AssertOwnedBy(maskRoot, serializedSurfaceBeltIndicator.transform);
            AssertOwnedBy(beltContent, maskRoot);
            Assert.That(maskRoot.GetComponent<RectMask2D>(), Is.Not.Null);
            AssertSerializedArrayCount(serializedSurfaceBeltIndicator, "_cells", SurfaceBeltViewModel.AuthoredCellCount);
            AssertSerializedReferenceIsAssigned(serializedSurfaceBeltIndicator, "_styleProfile");
            AssertSerializedReferenceIsAssigned(serializedSurfaceBeltIndicator, "_buttonBadgeStyleProfile");
            var buttonBadgeStyleProfile = GetSerializedReference<SurfaceBeltButtonBadgeStyleProfile>(
                serializedSurfaceBeltIndicator,
                "_buttonBadgeStyleProfile");
            Assert.That(buttonBadgeStyleProfile.TryValidate(out _), Is.True);
            Assert.That(
                buttonBadgeStyleProfile.NormalButton.Active.BackgroundColor,
                Is.Not.EqualTo(buttonBadgeStyleProfile.NormalButton.Inactive.BackgroundColor));
            Assert.That(
                buttonBadgeStyleProfile.NormalButton.Inactive.BackgroundColor,
                Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1.0f)));
            Assert.That(
                buttonBadgeStyleProfile.MoonButton.Inactive.BackgroundColor,
                Is.EqualTo(new Color(0.5f, 0.5f, 0.5f, 1.0f)));
            Assert.DoesNotThrow(() => serializedSurfaceBeltIndicator.ValidateAuthoredStructureOrThrow());

            var badgeGroups = serializedSurfaceBeltIndicator.GetComponentsInChildren<SurfaceBeltButtonBadgeGroupView>(true);
            Assert.That(badgeGroups.Length, Is.EqualTo(1));
            for (var i = 0; i < serializedSurfaceBeltIndicator.Cells.Length; i++)
            {
                var cell = serializedSurfaceBeltIndicator.Cells[i];
                AssertSerializedReferenceIsAssigned(cell, "_background");
                AssertOwnedBy(cell.transform, beltContent);
                Assert.DoesNotThrow(() => cell.ValidateAuthoredStructureOrThrow());

                var cellRect = (RectTransform)cell.transform;
                Assert.That(cellRect.rect.width, Is.GreaterThan(0.0f));
                Assert.That(cellRect.rect.height, Is.GreaterThan(0.0f));

                var badgeGroup = GetOptionalSerializedReference<SurfaceBeltButtonBadgeGroupView>(cell, "_buttonBadgeGroup");
                if (i == SurfaceBeltViewModel.AuthoredCellCount / 2)
                {
                    Assert.That(cell.name, Is.EqualTo("Cell_0"));
                    Assert.That(badgeGroup, Is.SameAs(badgeGroups[0]));
                    AssertOwnedBy(badgeGroup.transform, cell.transform);
                    Assert.That(
                        badgeGroup.transform.GetSiblingIndex(),
                        Is.LessThan(FindRequiredRect(cell.transform, "CellVisualAnchor").GetSiblingIndex()),
                        "The current-sector badge must remain to the left of the cell visual.");
                }
                else
                {
                    Assert.That(badgeGroup, Is.Null, $"{cell.name} must not author a button badge group.");
                }
            }

            var authoredBadgeGroup = badgeGroups[0];
            AssertSerializedReferenceIsAssigned(authoredBadgeGroup, "_normalBadge");
            AssertSerializedReferenceIsAssigned(authoredBadgeGroup, "_moonBadge");
            Assert.DoesNotThrow(() => authoredBadgeGroup.ValidateAuthoredStructureOrThrow());
            AssertOwnedBy(authoredBadgeGroup.transform, serializedSurfaceBeltIndicator.BeltContent);
            var badgeGroupLayout = authoredBadgeGroup.GetComponent<LayoutElement>();
            Assert.That(badgeGroupLayout, Is.Not.Null);
            Assert.That(badgeGroupLayout.preferredWidth, Is.GreaterThanOrEqualTo(32.0f));
            Assert.That(badgeGroupLayout.preferredHeight, Is.GreaterThanOrEqualTo(32.0f));

            var badgeViews = authoredBadgeGroup.GetComponentsInChildren<SurfaceBeltButtonBadgeView>(true);
            Assert.That(badgeViews.Length, Is.EqualTo(2));
            Assert.That(authoredBadgeGroup.MoonBadge.name, Is.EqualTo("MoonBadge"));
            Assert.That(authoredBadgeGroup.MoonBadge, Is.Not.SameAs(authoredBadgeGroup.NormalBadge));
            Assert.That(badgeViews[0].name, Is.EqualTo("NormalBadge"));
            AssertSerializedReferenceIsAssigned(badgeViews[0], "_frame");
            AssertSerializedReferenceIsAssigned(badgeViews[0], "_background");
            AssertSerializedReferenceIsAssigned(badgeViews[0], "_motionRoot");
            AssertSerializedReferenceIsAssigned(badgeViews[0], "_shineMaterialTemplate");
            var authoredFrame = GetSerializedReference<Image>(badgeViews[0], "_frame");
            var authoredFill = GetSerializedReference<Image>(badgeViews[0], "_background");
            var authoredMotionRoot = GetSerializedReference<RectTransform>(badgeViews[0], "_motionRoot");
            var shineMaterial = GetSerializedReference<Material>(badgeViews[0], "_shineMaterialTemplate");
            Assert.That(authoredFrame.color.g, Is.GreaterThan(authoredFrame.color.r));
            Assert.That(authoredFill.color, Is.EqualTo(buttonBadgeStyleProfile.NormalButton.Active.BackgroundColor));
            Assert.That(authoredMotionRoot, Is.SameAs(authoredFrame.rectTransform));
            Assert.That(shineMaterial.shader.name, Is.EqualTo(AllIn1UiMaskShaderName));
            Assert.That(shineMaterial.IsKeywordEnabled("SHINE_ON"), Is.True);
            Assert.DoesNotThrow(() => badgeViews[0].ValidateAuthoredStructureOrThrow());
            AssertOwnedBy(badgeViews[0].transform, authoredBadgeGroup.transform);
            Assert.That(
                authoredBadgeGroup.GetComponentsInChildren<TMP_Text>(true),
                Is.Empty,
                "The center remainder badge must not author a numeric label.");

            foreach (var badgeView in badgeViews)
            {
                var badgeViewRect = (RectTransform)badgeView.transform;
                Assert.That(badgeViewRect.rect.width, Is.GreaterThanOrEqualTo(0.0f));
                Assert.That(badgeViewRect.rect.height, Is.GreaterThanOrEqualTo(0.0f));
                var frame = GetSerializedReference<Image>(badgeView, "_frame");
                var frameLayout = frame.GetComponent<LayoutElement>();
                Assert.That(frameLayout, Is.Not.Null);
                Assert.That(frameLayout.preferredWidth, Is.GreaterThanOrEqualTo(32.0f));
                Assert.That(frameLayout.preferredHeight, Is.GreaterThanOrEqualTo(32.0f));
            }

            Assert.That(hudPrefab.GetComponentsInChildren<RawImage>(true), Is.Empty);
        }

        [Test]
        public void HUDPrefab_SurfaceBeltIndicator_AuthoredReferencesStayWithinModule()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();
            var surfaceBeltIndicator = GetSerializedReference<SurfaceBeltIndicatorView>(hudPrefab, "_surfaceBeltIndicatorView");
            var indicatorRoot = (RectTransform)surfaceBeltIndicator.transform;
            var indicatorLayout = surfaceBeltIndicator.GetComponent<LayoutElement>();
            var maskRoot = GetSerializedReference<RectTransform>(surfaceBeltIndicator, "_maskRoot");
            var beltContent = GetSerializedReference<RectTransform>(surfaceBeltIndicator, "_beltContent");
            var centerArrow = GetSerializedReference<RectTransform>(surfaceBeltIndicator, "_centerArrow");
            var cells = surfaceBeltIndicator.Cells;

            Assert.That(indicatorLayout, Is.Not.Null);
            Assert.That(indicatorLayout.preferredWidth, Is.GreaterThan(0.0f));
            Assert.That(indicatorLayout.preferredHeight, Is.GreaterThan(0.0f));
            Assert.That(indicatorRoot.rect.width, Is.GreaterThan(0.0f));
            Assert.That(indicatorRoot.rect.height, Is.GreaterThan(0.0f));
            AssertOwnedBy(maskRoot, indicatorRoot);
            AssertOwnedBy(beltContent, maskRoot);
            AssertOwnedBy(centerArrow, indicatorRoot);
            Assert.That(centerArrow.IsChildOf(beltContent), Is.False);
            Assert.That(maskRoot.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(cells.Length, Is.EqualTo(SurfaceBeltViewModel.AuthoredCellCount));
            Assert.DoesNotThrow(() => surfaceBeltIndicator.ValidateAuthoredStructureOrThrow());

            for (var i = 0; i < cells.Length; i++)
            {
                var cell = (RectTransform)cells[i].transform;
                AssertOwnedBy(cell, beltContent);
                Assert.That(cell.rect.width, Is.GreaterThan(0.0f));
                Assert.That(cell.rect.height, Is.GreaterThan(0.0f));
                Assert.DoesNotThrow(() => cells[i].ValidateAuthoredStructureOrThrow());
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_DuplicateChancePanelView_FailsPrefabContract()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_DuplicateChancePanelView_FailsPrefabContract");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var duplicateObject = UnityEngine.Object.Instantiate(hudView.ChancePanelView.gameObject, hudView.transform, false);
                duplicateObject.name = "DuplicateChancePanel";

                Assert.That(hudView.GetComponentsInChildren<ChancePanelView>(true).Length, Is.GreaterThanOrEqualTo(2));

                var source = new ManualGameplayUiPresentationSource();
                var stageInfoPresenter = new StageInfoPresenter(new StaticLocalizedTextResolver("Stage 1-1"));
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                Assert.Throws<InvalidOperationException>(() => controller.AttachView(hudView));
                Assert.That(hudView.GetComponentsInChildren<ChancePanelView>(true).Length, Is.EqualTo(2));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ChancePanelView_MaxChancesBeyondAuthoredSlots_Throws()
        {
            var rootObject = new GameObject("ChancePanelView_MaxChancesBeyondAuthoredSlots_Throws");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                controller.AttachView(hudView);

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    source.PublishSnapshot(CreateSnapshot(hasRemainingChances: true, remainingChances: 4, maxChances: 4)));
                Assert.That(exception.Message, Does.Contain("authored slots"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_ClonesAuthoredObjectiveItemTemplate()
        {
            var rootObject = new GameObject("ObjectiveHudView_ClonesAuthoredObjectiveItemTemplate");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    string.Empty,
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", false, false),
                    });

                objectiveView.Bind(viewModel);

                var runtimeItems = objectiveListRoot
                    .Cast<Transform>()
                    .Where(child => child != itemTemplate && child.gameObject.activeSelf)
                    .ToArray();

                Assert.That(itemTemplate.gameObject.activeSelf, Is.False);
                Assert.That(runtimeItems, Has.Length.EqualTo(1));
                Assert.That(runtimeItems[0].parent, Is.SameAs(objectiveListRoot));
                Assert.That(runtimeItems[0].gameObject.activeSelf, Is.True);
                Assert.That(FindObjectiveLabel(runtimeItems[0]).text, Is.EqualTo("Reach the exit zone"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_NewObjectiveRow_PlaysInState()
        {
            var rootObject = new GameObject("ObjectiveHudView_NewObjectiveRow_PlaysInState");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    string.Empty,
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", false, false),
                    });

                objectiveView.Bind(viewModel);

                var runtimeItem = FindActiveObjectiveRuntimeItem(objectiveListRoot, itemTemplate);
                var animator = runtimeItem.GetComponent<Animator>();
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("In"), Is.True);
                Assert.That(animator.GetBool("Active"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_InitialRows_EnterSerially()
        {
            var rootObject = new GameObject("ObjectiveHudView_InitialRows_EnterSerially");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);

                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                Assert.That(aRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"), Is.Null);

                CompleteObjectiveEnter(objectiveView, aRow);

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"), Is.Null);

                CompleteObjectiveEnter(objectiveView, bRow);

                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_EnterFinished_DoesNotStartNextEnterSynchronously()
        {
            var rootObject = new GameObject("ObjectiveHudView_EnterFinished_DoesNotStartNextEnterSynchronously");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterWithoutScheduler(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));

                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);
                Assert.That(GetPrivateField<bool>(objectiveView, "_transitionAdvanceRequested"), Is.True);
                Assert.That(GetPrivateField<string>(objectiveView, "_transitioningStableId"), Is.Empty);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_UpdateAfterEnterFinished_StartsNextEnter()
        {
            var rootObject = new GameObject("ObjectiveHudView_UpdateAfterEnterFinished_StartsNextEnter");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterWithoutScheduler(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));

                ForceObjectiveSchedulerDue(objectiveView);

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(GetPrivateField<string>(objectiveView, "_transitioningStableId"), Is.EqualTo("b"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_CollectionTransitionGap_IsRespected()
        {
            var rootObject = new GameObject("ObjectiveHudView_CollectionTransitionGap_IsRespected");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterWithoutScheduler(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));

                var allowedAt = GetPrivateField<float>(objectiveView, "_nextTransitionAllowedAt");
                InvokeObjectiveScheduler(objectiveView, allowedAt - 0.001f);

                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);

                InvokeObjectiveScheduler(objectiveView, allowedAt);

                Assert.That(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b").VisualState,
                    Is.EqualTo(ObjectiveRowVisualState.Entering));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_DismissFinished_DoesNotStartNextTransitionSynchronously()
        {
            var rootObject = new GameObject("ObjectiveHudView_DismissFinished_DoesNotStartNextTransitionSynchronously");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterSequence(objectiveView, objectiveListRoot, itemTemplate, "a", "b", "c");
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));

                CompleteObjectiveDismissWithoutScheduler(bRow);

                Assert.That(cRow.gameObject.activeSelf, Is.True);
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));
                Assert.That(GetPrivateField<bool>(objectiveView, "_transitionAdvanceRequested"), Is.True);
                Assert.That(GetPrivateField<string>(objectiveView, "_transitioningStableId"), Is.Empty);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_EnterTween_ClampsLargeDelta()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_EnterTween_ClampsLargeDelta");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    string.Empty,
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                var finished = false;
                row.TransitionFinished += (_, kind) => finished |= kind == ObjectiveRowTransitionKind.Enter;

                row.Tick(1.0f);

                var layout = row.GetComponent<LayoutElement>();
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(finished, Is.False);
                Assert.That(layout.preferredHeight, Is.GreaterThan(0.0f).And.LessThan(60.0f));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_CollapseTween_ClampsLargeDelta()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_CollapseTween_ClampsLargeDelta");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                CompleteObjectiveEnter(objectiveView, row);
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", true, true),
                    });

                row.Tick(0.0f);
                var animator = row.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                animator.Play("Out", 0, 1.0f);
                animator.Update(1.0f);
                row.Tick(0.0f);
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Collapsing));

                var finished = false;
                row.TransitionFinished += (_, kind) => finished |= kind == ObjectiveRowTransitionKind.Dismiss;
                row.Tick(1.0f);

                var layout = row.GetComponent<LayoutElement>();
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Collapsing));
                Assert.That(finished, Is.False);
                Assert.That(layout.preferredHeight, Is.GreaterThan(0.0f).And.LessThan(60.0f));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_ForceResetForPool_InactiveObject_DoesNotTouchAnimator()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_ForceResetForPool_InactiveObject_DoesNotTouchAnimator");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    string.Empty,
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                row.gameObject.SetActive(false);

                row.ForceResetForPool();

                LogAssert.NoUnexpectedReceived();
                var layout = row.GetComponent<LayoutElement>();
                Assert.That(row.StableId, Is.Empty);
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Hidden));
                Assert.That(layout.ignoreLayout, Is.True);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_ForceClear_CancelsPendingAdvance()
        {
            var rootObject = new GameObject("ObjectiveHudView_ForceClear_CancelsPendingAdvance");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterWithoutScheduler(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));
                Assert.That(GetPrivateField<bool>(objectiveView, "_transitionAdvanceRequested"), Is.True);

                viewModel.Reset();
                ForceObjectiveSchedulerDue(objectiveView);

                Assert.That(GetPrivateField<bool>(objectiveView, "_transitionAdvanceRequested"), Is.False);
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_JustSatisfiedRow_PlaysActiveAndHoldsOutGate()
        {
            var rootObject = new GameObject("ObjectiveHudView_JustSatisfiedRow_PlaysActiveAndHoldsOutGate");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    string.Empty,
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnter(
                    objectiveView,
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "reach-exit"));
                viewModel.SetState(
                    true,
                    string.Empty,
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", true, true),
                    });

                var runtimeItem = FindActiveObjectiveRuntimeItem(objectiveListRoot, itemTemplate);
                var animator = runtimeItem.GetComponent<Animator>();
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Active"), Is.True);
                Assert.That(animator.GetBool("Active"), Is.True);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_GroupedRow_InitialPartialEnter_DoesNotPulse()
        {
            var rootObject = new GameObject("ObjectiveHudView_GroupedRow_InitialPartialEnter_DoesNotPulse");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 2, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);

                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                Assert.That(FindObjectiveLabel(row.transform).text, Is.EqualTo("Place a pushable box on the button (2/4)"));
                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(0));
                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(row.GetComponent<Animator>().GetBool("Active"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_GroupedRow_VisibleIdleProgressIncrease_PulsesOnce()
        {
            var rootObject = new GameObject("ObjectiveHudView_GroupedRow_VisibleIdleProgressIncrease_PulsesOnce");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);

                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 2, 4, isSatisfied: false, justSatisfied: false),
                    });

                Assert.That(FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons"), Is.SameAs(row));
                Assert.That(FindObjectiveLabel(row.transform).text, Is.EqualTo("Place a pushable box on the button (2/4)"));
                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(1));
                Assert.That(GetProgressHighlightAlpha(row), Is.GreaterThan(0.0f));
                Assert.That(GetProgressPulseScaleTarget(row).localScale.x, Is.GreaterThan(1.0f));
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));
                Assert.That(row.GetComponent<Animator>().GetBool("Active"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_GroupedRow_FinalComplete_UsesActiveDismissNotProgressPulse()
        {
            var rootObject = new GameObject("ObjectiveHudView_GroupedRow_FinalComplete_UsesActiveDismissNotProgressPulse");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 3, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);

                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 4, 4, isSatisfied: true, justSatisfied: true),
                    });

                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(0));
                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(row.GetComponent<Animator>().GetBool("Active"), Is.True);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_PendingEnterProgressChange_UpdatesBaselineWithoutPulse()
        {
            var rootObject = new GameObject("ObjectiveHudView_PendingEnterProgressChange_UpdatesBaselineWithoutPulse");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        CreateGroupedObjectiveRow("buttons", 0, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                CompleteObjectiveEnter(objectiveView, aRow);
                var pendingRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");

                Assert.That(FindObjectiveLabel(pendingRow.transform).text, Is.EqualTo("Place a pushable box on the button (1/4)"));
                Assert.That(GetProgressPulsePlayCount(pendingRow), Is.EqualTo(0));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_ProgressPulse_FadesHighlightToZero()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_ProgressPulse_FadesHighlightToZero");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);
                var highlightGraphics = GetPrivateField<Graphic[]>(row, "_progressHighlightGraphics");
                Assert.That(highlightGraphics, Is.Not.Null);
                Assert.That(highlightGraphics.Length, Is.EqualTo(2));

                row.PlayProgressPulse();
                Assert.That(GetProgressHighlightAlpha(row), Is.GreaterThan(0.0f));
                for (var i = 0; i < highlightGraphics.Length; i++)
                {
                    Assert.That(highlightGraphics[i].color.a, Is.GreaterThan(0.99f), $"highlightGraphics[{i}]");
                }

                row.Tick(1.0f);

                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(GetProgressPulseScaleTarget(row).localScale, Is.EqualTo(Vector3.one));
                Assert.That(GetPrivateField<bool>(row, "_isProgressPulsePlaying"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_ProgressPulse_RestartsCleanly()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_ProgressPulse_RestartsCleanly");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);

                row.PlayProgressPulse();
                row.Tick(0.1f);
                var fadedAlpha = GetProgressHighlightAlpha(row);

                row.PlayProgressPulse();

                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(2));
                Assert.That(GetProgressHighlightAlpha(row), Is.GreaterThan(fadedAlpha));

                row.Tick(1.0f);
                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(GetProgressPulseScaleTarget(row).localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_ForceResetForPool_ClearsProgressHighlight()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_ForceResetForPool_ClearsProgressHighlight");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);
                row.PlayProgressPulse();

                row.ForceResetForPool();

                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(GetProgressPulseScaleTarget(row).localScale, Is.EqualTo(Vector3.one));
                Assert.That(GetPrivateField<bool>(row, "_isProgressPulsePlaying"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_CompleteAndDismiss_CancelsProgressPulse()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_CompleteAndDismiss_CancelsProgressPulse");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 3, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);
                row.PlayProgressPulse();

                row.CompleteAndDismiss();

                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(GetProgressPulseScaleTarget(row).localScale, Is.EqualTo(Vector3.one));
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(row.GetComponent<Animator>().GetBool("Active"), Is.True);
                Assert.That(GetPrivateField<bool>(row, "_isProgressPulsePlaying"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_PendingEnterAlreadySatisfied_IsSkipped()
        {
            var rootObject = new GameObject("ObjectiveHudView_PendingEnterAlreadySatisfied_IsSkipped");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        CreateGroupedObjectiveRow("buttons", 0, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        CreateGroupedObjectiveRow("buttons", 4, 4, isSatisfied: true, justSatisfied: true),
                    });

                CompleteObjectiveEnter(objectiveView, aRow);

                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons"), Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_EnteringRowBecomesSatisfied_CompletesAfterEnter()
        {
            var rootObject = new GameObject("ObjectiveHudView_EnteringRowBecomesSatisfied_CompletesAfterEnter");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 3, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 4, 4, isSatisfied: true, justSatisfied: true),
                    });

                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));

                CompleteObjectiveEnter(objectiveView, row);

                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(0));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_ObjectiveIdentityChange_ClearsProgressBaselines()
        {
            var rootObject = new GameObject("ObjectiveHudView_ObjectiveIdentityChange_ClearsProgressBaselines");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnter(
                    objectiveView,
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons"));

                viewModel.SetState(
                    true,
                    "objective-b",
                    string.Empty,
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 2, 4, isSatisfied: false, justSatisfied: false),
                    });

                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                Assert.That(FindObjectiveLabel(row.transform).text, Is.EqualTo("Place a pushable box on the button (2/4)"));
                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(0));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_InsertingMiddleRow_UsesTargetSiblingPosition()
        {
            var rootObject = new GameObject("ObjectiveHudView_InsertingMiddleRow_UsesTargetSiblingPosition");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                CompleteObjectiveEnter(objectiveView, cRow);

                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                Assert.That(FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"), Is.SameAs(cRow));
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));
                Assert.That(aRow.transform.GetSiblingIndex(), Is.LessThan(bRow.transform.GetSiblingIndex()));
                Assert.That(bRow.transform.GetSiblingIndex(), Is.LessThan(cRow.transform.GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_HidesRootAndRuntimeItems_WhenNoObjective()
        {
            var rootObject = new GameObject("ObjectiveHudView_HidesRootAndRuntimeItems_WhenNoObjective");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveRoot = GetSerializedReference<GameObject>(objectiveView, "_root");
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    string.Empty,
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", false, false),
                    });

                objectiveView.Bind(viewModel);
                viewModel.Reset();

                var runtimeItems = objectiveListRoot
                    .Cast<Transform>()
                    .Where(child => child != itemTemplate && child.name.StartsWith("Objective_Item_Runtime_", StringComparison.Ordinal))
                    .ToArray();

                Assert.That(objectiveRoot.activeSelf, Is.False);
                Assert.That(runtimeItems, Has.Length.EqualTo(1));
                Assert.That(runtimeItems[0].gameObject.activeSelf, Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_RemovingMiddleRow_KeepsLowerRowInstance()
        {
            var rootObject = new GameObject("ObjectiveHudView_RemovingMiddleRow_KeepsLowerRowInstance");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));
                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"));
                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"));
                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");

                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                Assert.That(FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"), Is.SameAs(cRow));
                Assert.That(bRow.gameObject.activeSelf, Is.True);
                Assert.That(bRow.transform.GetSiblingIndex(), Is.LessThan(cRow.transform.GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_CompletedMiddleRow_CollapsesLayoutHeightBeforePooling()
        {
            var rootObject = new GameObject("ObjectiveHudView_CompletedMiddleRow_CollapsesLayoutHeightBeforePooling");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterSequence(objectiveView, objectiveListRoot, itemTemplate, "a", "b", "c");
                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                LayoutRebuilder.ForceRebuildLayoutImmediate(objectiveListRoot);

                var initialGap = Mathf.Abs(
                    ((RectTransform)cRow.transform).anchoredPosition.y -
                    ((RectTransform)aRow.transform).anchoredPosition.y);

                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", true, true),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                bRow.Tick(0.0f);

                var animator = bRow.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                animator.Play("Out", 0, 1.0f);
                animator.Update(1.0f);

                bRow.Tick(0.0f);
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Collapsing));
                bRow.Tick(0.1f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(objectiveListRoot);

                var bLayout = bRow.GetComponent<LayoutElement>();
                var collapsedGap = Mathf.Abs(
                    ((RectTransform)cRow.transform).anchoredPosition.y -
                    ((RectTransform)aRow.transform).anchoredPosition.y);

                Assert.That(bRow.gameObject.activeSelf, Is.True);
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Collapsing));
                Assert.That(bLayout.preferredHeight, Is.GreaterThan(0.0f).And.LessThan(60.0f));
                Assert.That(collapsedGap, Is.LessThan(initialGap));
                Assert.That(bRow.transform.GetSiblingIndex(), Is.LessThan(cRow.transform.GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_MultipleRemovedRows_ExitSerially()
        {
            var rootObject = new GameObject("ObjectiveHudView_MultipleRemovedRows_ExitSerially");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                        new ObjectiveConditionHudViewModel("d", "D", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterSequence(objectiveView, objectiveListRoot, itemTemplate, "a", "b", "c", "d");

                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("d", "D", false, false),
                    });

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));

                CompleteObjectiveDismiss(objectiveView, bRow);

                cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_ExitQueueRunsBeforeEnterQueue()
        {
            var rootObject = new GameObject("ObjectiveHudView_ExitQueueRunsBeforeEnterQueue");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterSequence(objectiveView, objectiveListRoot, itemTemplate, "a", "b", "c");

                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("d", "D", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "d"), Is.Null);

                CompleteObjectiveDismiss(objectiveView, bRow);

                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                var dRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "d");
                Assert.That(dRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(aRow.transform.GetSiblingIndex(), Is.LessThan(dRow.transform.GetSiblingIndex()));
                Assert.That(dRow.transform.GetSiblingIndex(), Is.LessThan(cRow.transform.GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_StalePendingEnter_IsSkipped()
        {
            var rootObject = new GameObject("ObjectiveHudView_StalePendingEnter_IsSkipped");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                Assert.That(FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"), Is.Not.Null);

                viewModel.SetState(
                    true,
                    "objective-a",
                    string.Empty,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));

                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_RendersStageNameFromSnapshot()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_RendersStageNameFromSnapshot");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                var stageInfoPresenter = new StageInfoPresenter(new StaticLocalizedTextResolver("Stage 1-1"));
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(stageDisplayNameKey: "stage.stage-1-1.display_name"));

                var stageLabel = GetSerializedReference<TMP_Text>(hudView, "_stageNameLabel");
                Assert.That(stageLabel, Is.Not.Null);
                Assert.That(stageLabel.gameObject.activeSelf, Is.True);
                Assert.That(stageLabel.text, Is.EqualTo("Stage 1-1"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDPrefab_HasObjectiveHudView()
        {
            var rootObject = new GameObject("HUDPrefab_HasObjectiveHudView");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                Assert.That(hudView.ObjectiveHudView, Is.Not.Null);
                hudView.ObjectiveHudView.Bind(new ObjectiveHudViewModel());
                var objectiveListRoot = GetSerializedReference<RectTransform>(hudView.ObjectiveHudView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(hudView.ObjectiveHudView, "_objectiveItemTemplate");
                Assert.That(objectiveListRoot, Is.Not.Null);
                Assert.That(objectiveListRoot.name, Is.EqualTo("Objective_List"));
                Assert.That(itemTemplate, Is.Not.Null);
                Assert.That(itemTemplate.parent, Is.SameAs(objectiveListRoot));
                Assert.That(objectiveListRoot.childCount, Is.EqualTo(1));
                Assert.That(itemTemplate.gameObject.activeSelf, Is.False);
                Assert.That(itemTemplate.GetComponent<Animator>(), Is.Not.Null);
                Assert.That(itemTemplate.GetComponent<LayoutElement>(), Is.Not.Null);
                var rowView = itemTemplate.GetComponent<ObjectiveHudRowView>();
                Assert.That(rowView, Is.Not.Null);
                var layoutGroup = objectiveListRoot.GetComponent<VerticalLayoutGroup>();
                Assert.That(layoutGroup, Is.Not.Null);
                Assert.That(layoutGroup.childControlHeight, Is.True);
                Assert.That(layoutGroup.childForceExpandHeight, Is.False);
                Assert.That(
                    itemTemplate.GetComponentsInChildren<TMP_Text>(true).Any(label => label.name == "Label_Objective"),
                    Is.True);

                var textRoot = FindRequiredRect(itemTemplate, "Text");
                var leftGradient = FindRequiredRect(textRoot, "LeftGradient");
                var rightGradient = FindRequiredRect(textRoot, "RightGradient");
                var leftGradientGraphic = leftGradient.GetComponent<Graphic>();
                var rightGradientGraphic = rightGradient.GetComponent<Graphic>();
                Assert.That(leftGradientGraphic, Is.Not.Null);
                Assert.That(rightGradientGraphic, Is.Not.Null);
                Assert.That(leftGradientGraphic.raycastTarget, Is.False);
                Assert.That(rightGradientGraphic.raycastTarget, Is.False);
                Assert.That(GetPrivateField<Graphic>(rowView, "_progressHighlightGraphic"), Is.Null);
                Assert.That(GetPrivateField<CanvasGroup>(rowView, "_progressHighlightGroup"), Is.Null);
                AssertSerializedReference(rowView, "_progressPulseScaleTarget", textRoot.parent);
                AssertSerializedGraphicArray(
                    rowView,
                    "_progressHighlightGraphics",
                    leftGradientGraphic,
                    rightGradientGraphic);
                var progressHighlightColor = GetPrivateField<Color>(rowView, "_progressHighlightColor");
                Assert.That(progressHighlightColor.g, Is.EqualTo(0.98f).Within(0.001f));
                Assert.That(progressHighlightColor.b, Is.EqualTo(0.65f).Within(0.001f));
                Assert.That(GetPrivateField<float>(rowView, "progressPulseDuration"), Is.EqualTo(0.33f).Within(0.001f));
                Assert.That(GetPrivateField<float>(rowView, "progressPulseScale"), Is.EqualTo(1.05f).Within(0.001f));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        private static TMP_Text FindObjectiveLabel(Transform item)
        {
            return item
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(label => label.name == "Label_Objective");
        }

        private static ObjectiveConditionHudViewModel CreateGroupedObjectiveRow(
            string stableId,
            int completedCount,
            int requiredCount,
            bool isSatisfied,
            bool justSatisfied)
        {
            return new ObjectiveConditionHudViewModel(
                stableId,
                $"Place a pushable box on the button ({completedCount}/{requiredCount})",
                isSatisfied,
                justSatisfied,
                isGrouped: true,
                completedCount: completedCount,
                requiredCount: requiredCount,
                rowKind: ObjectiveHudRowKind.ButtonGroupGeneric,
                groupKey: "button-group-push");
        }

        private static int GetProgressPulsePlayCount(ObjectiveHudRowView rowView)
        {
            return GetPrivateField<int>(rowView, "<ProgressPulsePlayCount>k__BackingField");
        }

        private static float GetProgressHighlightAlpha(ObjectiveHudRowView rowView)
        {
            return GetPrivateField<float>(rowView, "_progressHighlightAlpha");
        }

        private static Transform GetProgressPulseScaleTarget(ObjectiveHudRowView rowView)
        {
            return GetPrivateField<Transform>(rowView, "_progressPulseScaleTarget");
        }

        private static GameObject FindActiveObjectiveRuntimeItem(
            RectTransform objectiveListRoot,
            RectTransform itemTemplate)
        {
            return objectiveListRoot
                .Cast<Transform>()
                .Where(child => child != itemTemplate && child.gameObject.activeSelf)
                .Single()
                .gameObject;
        }

        private static ObjectiveHudRowView FindObjectiveRuntimeRow(
            RectTransform objectiveListRoot,
            RectTransform itemTemplate,
            string stableId)
        {
            return objectiveListRoot
                .Cast<Transform>()
                .Where(child => child != itemTemplate)
                .Select(child => child.GetComponent<ObjectiveHudRowView>())
                .Single(row => row != null && row.gameObject.activeSelf && row.StableId == stableId);
        }

        private static ObjectiveHudRowView TryFindObjectiveRuntimeRow(
            RectTransform objectiveListRoot,
            RectTransform itemTemplate,
            string stableId)
        {
            return objectiveListRoot
                .Cast<Transform>()
                .Where(child => child != itemTemplate)
                .Select(child => child.GetComponent<ObjectiveHudRowView>())
                .SingleOrDefault(row => row != null && row.gameObject.activeSelf && row.StableId == stableId);
        }

        private static void CompleteObjectiveEnterSequence(
            ObjectiveHudView objectiveView,
            RectTransform objectiveListRoot,
            RectTransform itemTemplate,
            params string[] stableIds)
        {
            for (var i = 0; i < stableIds.Length; i++)
            {
                CompleteObjectiveEnter(
                    objectiveView,
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, stableIds[i]));
            }
        }

        private static void CompleteObjectiveEnter(ObjectiveHudView objectiveView, ObjectiveHudRowView rowView)
        {
            Assert.That(objectiveView, Is.Not.Null);
            Assert.That(rowView, Is.Not.Null);
            CompleteObjectiveEnterWithoutScheduler(rowView);
            ForceObjectiveSchedulerDue(objectiveView);
        }

        private static void CompleteObjectiveEnterWithoutScheduler(ObjectiveHudRowView rowView)
        {
            Assert.That(rowView, Is.Not.Null);
            for (var i = 0; i < 16 && rowView.VisualState == ObjectiveRowVisualState.Entering; i++)
            {
                rowView.Tick(1.0f);
            }

            Assert.That(rowView.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));
        }

        private static void CompleteObjectiveDismiss(ObjectiveHudView objectiveView, ObjectiveHudRowView rowView)
        {
            Assert.That(objectiveView, Is.Not.Null);
            Assert.That(rowView, Is.Not.Null);
            CompleteObjectiveDismissWithoutScheduler(rowView);
            ForceObjectiveSchedulerDue(objectiveView);
        }

        private static void CompleteObjectiveDismissWithoutScheduler(ObjectiveHudRowView rowView)
        {
            Assert.That(rowView, Is.Not.Null);
            rowView.Tick(0.0f);

            var animator = rowView.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            animator.Play("Out", 0, 1.0f);
            animator.Update(1.0f);

            rowView.Tick(0.0f);
            for (var i = 0; i < 16 && rowView.VisualState == ObjectiveRowVisualState.Collapsing; i++)
            {
                rowView.Tick(1.0f);
            }
        }

        private static void ForceObjectiveSchedulerDue(ObjectiveHudView objectiveView)
        {
            SetPrivateField(objectiveView, "_nextTransitionAllowedAt", 0.0f);
            InvokeObjectiveScheduler(objectiveView, 0.0f);
        }

        private static void InvokeObjectiveScheduler(ObjectiveHudView objectiveView, float now)
        {
            var processMethod = typeof(ObjectiveHudView).GetMethod(
                "ProcessTransitionAdvance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(processMethod, Is.Not.Null);
            processMethod.Invoke(objectiveView, new object[] { now });
        }

        [Test]
        public void HUDController_Dispose_DetachesAllHudBindings()
        {
            var rootObject = new GameObject("HUDController_Dispose_DetachesAllHudBindings");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var chancePanelPresenter = new ChancePanelPresenter();
                var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    surfaceBeltIndicatorPresenter);
                var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    surfaceBeltIndicatorPresenter.ViewModel);

                controller.AttachView(hudView);
                controller.Dispose();

                Assert.That(hudView.ViewModel, Is.Null);
                Assert.That(hudView.StageInfoViewModel, Is.Null);
                Assert.That(hudView.ObjectiveHudView.ViewModel, Is.Null);
                Assert.That(hudView.ChancePanelView.ViewModel, Is.Null);
                Assert.That(hudView.SurfaceBeltIndicatorView.ViewModel, Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        private static GameplayUiCanvasRootView CreateCanonicalRootView(GameObject rootObject, out HUDRootView hudView)
        {
            var rootShellPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
            Assert.That(rootShellPrefab, Is.Not.Null);

            var rootShellInstance = UnityEngine.Object.Instantiate(rootShellPrefab, rootObject.transform, false);
            var rootView = rootShellInstance.GetComponent<GameplayUiCanvasRootView>();
            Assert.That(rootView, Is.Not.Null);
            rootView.EnsureHierarchy();

            var hudLayer = rootView.transform.Find("HudLayer");
            Assert.That(hudLayer, Is.Not.Null);
            hudView = UiTestPrefabAssetUtility.InstantiateHudPrefab(hudLayer.GetComponent<RectTransform>());
            AttachHudView(rootView, hudView);
            return rootView;
        }

        private static void AttachHudView(GameplayUiCanvasRootView rootView, HUDRootView hudView)
        {
            var attachMethod = typeof(GameplayUiCanvasRootView).GetMethod(
                "AttachHudView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(attachMethod, Is.Not.Null);
            attachMethod.Invoke(rootView, new object[] { hudView });
        }

        private static UIPresentationSnapshot CreateSnapshot(
            bool isPaused = false,
            bool isUiBlocked = false,
            bool hasBlockingPresentation = false,
            GameplayUiActionResolutionKind lastOutcome = GameplayUiActionResolutionKind.None,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0,
            string stageDisplayNameKey = "")
        {
            return new UIPresentationSnapshot(
                new UITickSlice(
                    lastReducedTickIndex: 4,
                    finalTopology: new GameplayUiTopology(GameplayUiFace.Front),
                    isStageCleared: false,
                    isTopologyTransitionActive: false),
                new UIInteractionSlice(
                    isPaused,
                    canAcceptGameplayCommands: !isPaused && !hasBlockingPresentation,
                    hasBlockingGameplayPresentation: hasBlockingPresentation,
                    isUiGameplayInputBlocked: isUiBlocked),
                new UIStageSlice(
                    string.IsNullOrWhiteSpace(stageDisplayNameKey)
                        ? StageId.None
                        : StageId.CreateOrThrow("stage-1-1"),
                    stageDisplayNameKey),
                UIObjectiveSlice.Empty,
                new UIChanceSlice(hasRemainingChances, remainingChances,
                    maxChances > 0 ? maxChances : (hasRemainingChances ? remainingChances : 0)),
                UITopologySlice.FromTopology(new GameplayUiTopology(GameplayUiFace.Front), false),
                SurfaceBeltSnapshot.FromTopology(new GameplayUiTopology(GameplayUiFace.Front), false, 0),
                new UIPlayerActionSlice(
                    lastResolvedOutcome: lastOutcome,
                    lastResolvedTickIndex: lastOutcome == GameplayUiActionResolutionKind.None ? 0 : 4,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0),
                new UINotificationLedgerSlice(new[]
                {
                    new UINotificationRecord(
                        new UITickEventKey(
                            tickIndex: 4,
                            eventKind: UITickEventKind.PlayerActionResolved,
                            actorEntityId: 10,
                            actionKind: GameplayUiActionKind.Flip,
                            actionSequence: 2,
                            resolutionKind: GameplayUiActionResolutionKind.Success),
                        UITickEventKind.PlayerActionResolved,
                        damageAmount: 0,
                        expireAfterTickIndex: 8),
                }));
        }

        private static int CountCharacter(string value, char character)
        {
            var count = 0;
            if (value == null)
            {
                return count;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == character)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSlotsWithChild(ChancePanelView chancePanelView, string childName)
        {
            var count = 0;
            var slots = chancePanelView.GetComponentsInChildren<ChanceSlotView>(true);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].transform.Find(childName) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static RectTransform FindRequiredRect(Transform root, string name)
        {
            var transforms = root.GetComponentsInChildren<RectTransform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name)
                {
                    return transforms[i];
                }
            }

            Assert.Fail($"Expected to find RectTransform named '{name}'.");
            return null;
        }

        private static void AssertStackTransform(
            RectTransform stack,
            Vector2 expectedAnchor,
            Vector2 expectedPivot,
            Vector2 expectedPosition)
        {
            Assert.That(stack.anchorMin, Is.EqualTo(expectedAnchor));
            Assert.That(stack.anchorMax, Is.EqualTo(expectedAnchor));
            Assert.That(stack.pivot, Is.EqualTo(expectedPivot));
            Assert.That(stack.anchoredPosition, Is.EqualTo(expectedPosition));
            var layoutGroup = stack.GetComponent<VerticalLayoutGroup>();
            Assert.That(layoutGroup, Is.Not.Null);
        }

        private static void AssertOwnedBy(Transform child, Transform owner)
        {
            Assert.That(child, Is.Not.Null);
            Assert.That(owner, Is.Not.Null);
            Assert.That(child.IsChildOf(owner), Is.True, $"{child.name} should be under {owner.name}.");
        }

        private static void AssertNoOverlap(RectTransform first, RectTransform second)
        {
            var firstRect = GetWorldRect(first);
            var secondRect = GetWorldRect(second);
            var overlaps = firstRect.xMin < secondRect.xMax
                && firstRect.xMax > secondRect.xMin
                && firstRect.yMin < secondRect.yMax
                && firstRect.yMax > secondRect.yMin;

            Assert.That(overlaps, Is.False, $"{first.name} overlaps {second.name}.");
        }

        private static TReference GetSerializedReference<TReference>(
            UnityEngine.Object target,
            string fieldName)
            where TReference : UnityEngine.Object
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            Assert.That(property.objectReferenceValue, Is.Not.Null, fieldName);

            var reference = property.objectReferenceValue as TReference;
            Assert.That(reference, Is.Not.Null, fieldName);
            return reference;
        }

        private static TReference GetOptionalSerializedReference<TReference>(
            UnityEngine.Object target,
            string fieldName)
            where TReference : UnityEngine.Object
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            return property.objectReferenceValue as TReference;
        }

        private static TValue GetPrivateField<TValue>(object target, string fieldName)
        {
            Assert.That(target, Is.Not.Null);
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (TValue)field.GetValue(target);
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            Assert.That(target, Is.Not.Null);
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void AssertSerializedReference(
            UnityEngine.Object target,
            string fieldName,
            UnityEngine.Object expected)
        {
            var actual = GetSerializedReference<UnityEngine.Object>(target, fieldName);
            Assert.That(actual, Is.SameAs(expected), fieldName);
        }

        private static void AssertSerializedGraphicArray(
            object target,
            string fieldName,
            params Graphic[] expected)
        {
            var actual = GetPrivateField<Graphic[]>(target, fieldName);
            Assert.That(actual, Is.Not.Null, fieldName);
            Assert.That(actual.Length, Is.EqualTo(expected.Length), fieldName);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.SameAs(expected[i]), $"{fieldName}[{i}]");
            }
        }

        private static void SetSerializedReference(
            UnityEngine.Object target,
            string fieldName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void AssertSerializedReferenceIsAssigned(
            UnityEngine.Object target,
            string fieldName)
        {
            GetSerializedReference<UnityEngine.Object>(target, fieldName);
        }

        private static void AssertSerializedArrayCount(
            UnityEngine.Object target,
            string fieldName,
            int expectedCount)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            Assert.That(property.isArray, Is.True, fieldName);
            Assert.That(property.arraySize, Is.EqualTo(expectedCount), fieldName);
            for (var i = 0; i < property.arraySize; i++)
            {
                Assert.That(property.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, $"{fieldName}[{i}]");
            }
        }

        private static void AssertWorldRectContains(RectTransform outer, RectTransform inner)
        {
            var outerRect = GetWorldRect(outer);
            var innerRect = GetWorldRect(inner);
            const float tolerance = 0.1f;
            Assert.That(innerRect.xMin, Is.GreaterThanOrEqualTo(outerRect.xMin - tolerance), $"{inner.name} extends left of {outer.name}.");
            Assert.That(innerRect.xMax, Is.LessThanOrEqualTo(outerRect.xMax + tolerance), $"{inner.name} extends right of {outer.name}.");
            Assert.That(innerRect.yMin, Is.GreaterThanOrEqualTo(outerRect.yMin - tolerance), $"{inner.name} extends below {outer.name}.");
            Assert.That(innerRect.yMax, Is.LessThanOrEqualTo(outerRect.yMax + tolerance), $"{inner.name} extends above {outer.name}.");
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void DestroySupportObjects(GameObject rootObject)
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            }

            if (rootObject != null)
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private sealed class StaticLocalizedTextResolver : ILocalizedTextResolver
        {
            private readonly string _resolvedText;

            public StaticLocalizedTextResolver(string resolvedText)
            {
                _resolvedText = resolvedText ?? string.Empty;
            }

            public string CurrentLocaleCode => "en-US";

            public event Action LocaleChanged;

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                return _resolvedText;
            }
        }
    }
}
