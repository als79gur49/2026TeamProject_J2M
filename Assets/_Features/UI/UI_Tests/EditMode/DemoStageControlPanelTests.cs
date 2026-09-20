using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Game.Feature.UI.Tests
{
    public sealed class DemoStageControlPanelTests
    {
        private const string PrefabPath =
            "Assets/_Features/DemoStageControl/UI/Prefabs/DemoStageControlPanel.prefab";
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void DemoStageControlPanel_RepositoryPrefab_IsAuthoredAndImplementsPopupView()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab, Is.Not.Null, PrefabPath);
            Assert.That(prefab.GetComponent<DemoStageControlPanelView>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<IPopupView>(), Is.Not.Null);
        }

        [Test]
        public void DemoStageControlPanel_RepositoryPrefab_HasRequiredReferencesAndTreatments()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var view = prefab.GetComponent<DemoStageControlPanelView>();
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero);
            Assert.DoesNotThrow(view.ValidateAuthoredReferences);

            var buttons = prefab.GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Has.Length.EqualTo(6));
            foreach (var button in buttons)
            {
                Assert.That(button.GetComponents<UiHoverScaleEffect>(), Has.Length.EqualTo(1), button.name);
                var frame = button.transform.Find("SelectionFrame")?.GetComponent<Image>();
                Assert.That(frame, Is.Not.Null, button.name);
                Assert.That(frame.raycastTarget, Is.False, button.name);
            }

            var texts = prefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.That(texts, Is.Not.Empty);
            foreach (var text in texts)
                Assert.That(TypographyBinding.FindFor(text), Is.Not.Null, text.name);
        }

        [Test]
        public void DemoStageControlPanel_MalformedRoot_FailsFast()
        {
            var malformed = new GameObject("MalformedDemo", typeof(RectTransform));
            malformed.SetActive(false);
            var view = malformed.AddComponent<DemoStageControlPanelView>();
            try
            {
                Assert.Throws<InvalidOperationException>(view.ValidateAuthoredReferences);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(malformed);
            }
        }

        [Test]
        public void DemoStageControlPanel_ProductionView_HasNoRuntimeHierarchyBuilder()
        {
            var source = File.ReadAllText(
                "Assets/_Features/DemoStageControl/UI/DemoStageControlPanelView.cs");

            Assert.That(source, Does.Not.Contain("CreateRuntime"));
            Assert.That(source, Does.Not.Contain("BuildHierarchy"));
            Assert.That(source, Does.Not.Contain("EnsureHierarchy"));
            Assert.That(source, Does.Not.Contain("AddRow"));
            Assert.That(source, Does.Not.Contain("AddText"));
            Assert.That(source, Does.Not.Contain("AddButton"));
            Assert.That(source, Does.Not.Contain("AddToggleButton"));
        }

        [Test]
        public void DemoStageControlPanel_StartSelectedStage_CallsCommandPort()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = new DemoStageControlPanelRuntime(
                view,
                commandPort,
                commandPort,
                CreatePayload(commandPort),
                new StageNameResolver(),
                () => { });

            FindButton(view.transform, "Start Selected Stage").onClick.Invoke();

            Assert.That(commandPort.StartStageCalls, Is.EqualTo(1));
            Assert.That(commandPort.LastStartedStageId, Is.EqualTo(commandPort.StageId));
        }

        [Test]
        public void DemoStageControlPanel_StartPointerAndSubmit_ConvergeExactlyOnce()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);

            FindButton(view.transform, "Start Selected Stage").onClick.Invoke();
            view.OnNavigationFocusGained();
            Assert.That(view.HandleSubmit(), Is.True);

            Assert.That(commandPort.StartStageCalls, Is.EqualTo(2));
        }

        [Test]
        public void DemoStageControlPanel_VerticalNavigationSkipsDisabledAction()
        {
            var commandPort = new RecordingCommandPort { IsCompletionInProgress = true };
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);

            view.OnNavigationFocusGained();
            Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(view.HandleSubmit(), Is.True);

            Assert.That(commandPort.ForceClearCalls, Is.EqualTo(0));
            Assert.That(commandPort.SetPlayerInvincibleCalls, Is.EqualTo(1));
        }

        [Test]
        public void DemoStageControlPanel_LiveTransitionSnapshotRecoversStartFocus()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);
            view.OnNavigationFocusGained();

            commandPort.IsSceneTransitionInProgress = true;
            commandPort.Publish();
            Assert.That(FindButton(view.transform, "Start Selected Stage").interactable, Is.False);
            Assert.That(view.HandleSubmit(), Is.True);
            Assert.That(commandPort.ForceClearCalls, Is.EqualTo(1));

            commandPort.IsSceneTransitionInProgress = false;
            commandPort.Publish();
            Assert.That(FindButton(view.transform, "Start Selected Stage").interactable, Is.True);
        }

        [Test]
        public void DemoStageControlPanel_CloseSubmitAndCancel_RequestOneCloseEach()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = CreateRuntime(view, commandPort);
            var closes = 0;
            view.CompletionRequested += _ => closes++;

            view.OnNavigationFocusGained();
            view.HandleNavigate(UiNavigationCommand.Down);
            view.HandleNavigate(UiNavigationCommand.Down);
            view.HandleNavigate(UiNavigationCommand.Down);
            Assert.That(view.HandleSubmit(), Is.True);
            Assert.That(view.HandleCancel(), Is.True);

            Assert.That(closes, Is.EqualTo(2));
        }

        [Test]
        public void DemoStageControlPanel_Dispose_UnsubscribesPresentationSource()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            var runtime = CreateRuntime(view, commandPort);
            Assert.That(commandPort.PresentationSubscriberCount, Is.EqualTo(1));

            runtime.Dispose();

            Assert.That(commandPort.PresentationSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void DemoStageControlPanel_ForceClear_CallsCommandPort_AndDisplaysLastResult()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = new DemoStageControlPanelRuntime(
                view,
                commandPort,
                commandPort,
                CreatePayload(commandPort),
                new StageNameResolver(),
                () => { });

            FindButton(view.transform, "Force Clear Current Stage").onClick.Invoke();

            Assert.That(commandPort.ForceClearCalls, Is.EqualTo(1));
            Assert.That(CollectText(view.transform), Does.Contain("forced clear"));
        }

        [Test]
        public void DemoStageControlPanel_ViewEmitsEventsOnly()
        {
            var source = File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelView.cs");

            Assert.That(source, Does.Not.Contain("IDemoStageControlCommandPort"));
            Assert.That(source, Does.Not.Contain("TransientCampaignSaveSlotStore"));
            Assert.That(source, Does.Not.Contain("ObjectiveTracker"));
            Assert.That(source, Does.Not.Contain("WorldState"));
            Assert.That(source, Does.Not.Contain("EntityState"));
            Assert.That(source, Does.Not.Contain("TickPipeline"));
            Assert.That(source, Does.Not.Contain("SaveService"));
        }

        [Test]
        public void DemoStageControlPanel_ShowsPlayerInvincibleToggleButton()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = new DemoStageControlPanelRuntime(
                view,
                commandPort,
                commandPort,
                CreatePayload(commandPort),
                new StageNameResolver(),
                () => { });

            Assert.That(CollectText(view.transform), Does.Contain("Player Invincible"));
            Assert.That(FindButton(view.transform, "Player Invincible"), Is.Not.Null);
            Assert.That(view.GetComponentsInChildren<Toggle>(true), Is.Empty);
        }

        [Test]
        public void DemoStageControlPanel_PlayerInvincibleToggleButtonCallsCommandPort()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = new DemoStageControlPanelRuntime(
                view,
                commandPort,
                commandPort,
                CreatePayload(commandPort),
                new StageNameResolver(),
                () => { });

            FindButton(view.transform, "Player Invincible").onClick.Invoke();

            Assert.That(commandPort.SetPlayerInvincibleCalls, Is.EqualTo(1));
            Assert.That(commandPort.PlayerInvincible, Is.True);
            Assert.That(CollectText(view.transform), Does.Contain("Player Invincible ON"));
        }

        [Test]
        public void DemoStageControlPanel_PlayerInvincibleToggleButtonEmitsEventOnly()
        {
            var commandPort = new RecordingCommandPort();
            var view = CreateView();
            using var runtime = new DemoStageControlPanelRuntime(
                view,
                commandPort,
                null,
                CreatePayload(commandPort),
                new StageNameResolver(),
                () => { });
            var observed = false;
            view.PlayerInvincibleToggled += enabled => observed = enabled;

            FindButton(view.transform, "Player Invincible").onClick.Invoke();

            Assert.That(observed, Is.True);
            Assert.That(commandPort.SetPlayerInvincibleCalls, Is.EqualTo(0));
        }

        [Test]
        public void DemoStageControlPanel_DisplaysPlayerInvincibleState()
        {
            var commandPort = new RecordingCommandPort();
            commandPort.SetPlayerInvincible(true);
            var view = CreateView();
            using var runtime = new DemoStageControlPanelRuntime(
                view,
                commandPort,
                commandPort,
                CreatePayload(commandPort),
                new StageNameResolver(),
                () => { });

            Assert.That(CollectText(view.transform), Does.Contain("Player Invincible: ON"));
            Assert.That(FindButton(view.transform, "Player Invincible").interactable, Is.True);
        }

        [Test]
        public void DemoStageControlPanel_TextWrappingModes_PreservePanelAndButtonIntent()
        {
            var view = CreateView();
            var texts = view.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            var panelTextCount = 0;
            var buttonLabelCount = 0;

            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text.transform.parent == view.transform)
                {
                    panelTextCount++;
                    Assert.That(text.textWrappingMode, Is.EqualTo(TMPro.TextWrappingModes.Normal));
                    continue;
                }

                if (text.transform.parent != null &&
                    text.transform.parent.GetComponent<Button>() != null)
                {
                    buttonLabelCount++;
                    Assert.That(text.textWrappingMode, Is.EqualTo(TMPro.TextWrappingModes.NoWrap));
                }
            }

            Assert.That(panelTextCount, Is.GreaterThan(0));
            Assert.That(buttonLabelCount, Is.GreaterThan(0));
        }

        [Test]
        public void DemoStageControlPanel_ResolvesStageDisplayNameAndRefreshesLocaleUntilDisposed()
        {
            var commandPort = new RecordingCommandPort();
            var resolver = new StageNameResolver();
            var view = CreateView();
            var runtime = new DemoStageControlPanelRuntime(
                view,
                commandPort,
                commandPort,
                CreatePayload(commandPort),
                resolver,
                () => { });

            try
            {
                Assert.That(
                    CollectText(view.transform),
                    Does.Contain("Stage Zero One (stage-0-1, current)"));
                Assert.That(
                    CollectText(view.transform),
                    Does.Not.Contain("stage-0-1 (stage-0-1, current)"));
                Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(1));

                resolver.SetLocale("ko-KR");

                Assert.That(
                    CollectText(view.transform),
                    Does.Contain("스테이지 영 일 (stage-0-1, current)"));
            }
            finally
            {
                runtime.Dispose();
            }

            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void DemoGameplayOverride_ViewDoesNotReferenceWorldState()
        {
            var source = File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelView.cs");

            Assert.That(source, Does.Not.Contain("WorldState"));
            Assert.That(source, Does.Not.Contain("EntityState"));
            Assert.That(source, Does.Not.Contain("TickPipeline"));
            Assert.That(source, Does.Not.Contain("TransientCampaignSaveSlotStore"));
        }

        [Test]
        public void DemoStageControlPanel_DoesNotDependOnLegacyDeveloperPopup()
        {
            var sources = string.Join(
                Environment.NewLine,
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelModels.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelRuntime.cs"),
                File.ReadAllText("Assets/_Features/DemoStageControl/UI/DemoStageControlPanelView.cs"));
            var removedPopupName = "Debug" + "Commands";
            var removedAccessName = "Debug" + "CommandAccess";

            Assert.That(sources, Does.Not.Contain(removedPopupName));
            Assert.That(sources, Does.Not.Contain(removedAccessName));
        }

        private DemoStageControlPanelView CreateView()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            _root = UnityEngine.Object.Instantiate(prefab);
            return _root.GetComponent<DemoStageControlPanelView>();
        }

        private static DemoStageControlPanelPayload CreatePayload(RecordingCommandPort commandPort)
        {
            return new DemoStageControlPanelPayload(commandPort);
        }

        private static DemoStageControlPanelRuntime CreateRuntime(
            DemoStageControlPanelView view,
            RecordingCommandPort commandPort)
        {
            return new DemoStageControlPanelRuntime(
                view,
                commandPort,
                commandPort,
                CreatePayload(commandPort),
                new StageNameResolver(),
                () => { });
        }

        private static Button FindButton(Transform root, string name)
        {
            var child = root.Find(name);
            Assert.That(child, Is.Not.Null, name);
            var button = child.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, name);
            return button;
        }

        private static string CollectText(Transform root)
        {
            var texts = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            var lines = new List<string>(texts.Length);
            for (var i = 0; i < texts.Length; i++)
            {
                lines.Add(texts[i].text);
            }

            return string.Join("\n", lines);
        }

        private sealed class StageNameResolver : ILocalizedTextResolver
        {
            private Action _localeChanged;
            private string _localeCode = "en-US";

            public string CurrentLocaleCode => _localeCode;

            public int LocaleChangedSubscriberCount { get; private set; }

            public event Action LocaleChanged
            {
                add
                {
                    _localeChanged += value;
                    LocaleChangedSubscriberCount++;
                }
                remove
                {
                    _localeChanged -= value;
                    LocaleChangedSubscriberCount--;
                }
            }

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                Assert.That(descriptor.Table, Is.EqualTo(StageDisplayNameKeys.Table));
                Assert.That(descriptor.Key, Is.EqualTo("stage.stage-0-1.display_name"));
                return string.Equals(_localeCode, "ko-KR", StringComparison.Ordinal)
                    ? "스테이지 영 일"
                    : "Stage Zero One";
            }

            public void SetLocale(string localeCode)
            {
                _localeCode = localeCode;
                _localeChanged?.Invoke();
            }
        }

        private sealed class RecordingCommandPort :
            IDemoStageControlCommandPort,
            IDemoGameplayOverrideCommandPort,
            IDemoStageControlPresentationSource
        {
            public readonly StageId StageId = StageId.CreateOrThrow("stage-0-1");
            private string _overrideMessage = string.Empty;
            private string _message = string.Empty;

            public int ForceClearCalls { get; private set; }

            public StageId LastStartedStageId { get; private set; } = StageId.None;

            public int StartStageCalls { get; private set; }

            public bool PlayerInvincible { get; private set; }

            public int SetPlayerInvincibleCalls { get; private set; }

            public bool IsSceneTransitionInProgress { get; set; }

            public bool IsCompletionInProgress { get; set; }

            public int PresentationSubscriberCount { get; private set; }

            public DemoStageControlPresentationSnapshot Current => new(
                GetStages(), GetStatus(), GetOverrideStatus());

            private event Action<DemoStageControlPresentationSnapshot> PresentationChanged;

            public event Action<DemoStageControlPresentationSnapshot> Changed
            {
                add
                {
                    PresentationChanged += value;
                    PresentationSubscriberCount++;
                }
                remove
                {
                    PresentationChanged -= value;
                    PresentationSubscriberCount--;
                }
            }

            public IReadOnlyList<DemoStageControlStageItem> GetStages()
            {
                return new[]
                {
                    new DemoStageControlStageItem(StageId, "stage.stage-0-1.display_name", true, true),
                };
            }

            public DemoStageControlStatus GetStatus()
            {
                return new DemoStageControlStatus(
                    StageId,
                    StageId,
                    isSceneTransitionInProgress: IsSceneTransitionInProgress,
                    isCompletionInProgress: IsCompletionInProgress,
                    _message);
            }

            public DemoStageControlResult StartStage(StageId stageId)
            {
                StartStageCalls++;
                LastStartedStageId = stageId;
                _message = $"started {stageId.Value}";
                Publish();
                return DemoStageControlResult.Ok(_message);
            }

            public DemoStageControlResult ForceClearCurrentStage()
            {
                ForceClearCalls++;
                _message = "forced clear";
                Publish();
                return DemoStageControlResult.Ok(_message);
            }

            public DemoStageControlResult SetPlayerInvincible(bool enabled)
            {
                SetPlayerInvincibleCalls++;
                PlayerInvincible = enabled;
                _overrideMessage = enabled ? "Player Invincible ON" : "Player Invincible OFF";
                Publish();
                return DemoStageControlResult.Ok(_overrideMessage);
            }

            public DemoStageControlResult TogglePlayerInvincible()
            {
                return SetPlayerInvincible(!PlayerInvincible);
            }

            public Game.Feature.Gameplay.Loop.DemoGameplayOverrideSnapshot GetSnapshot()
            {
                return new Game.Feature.Gameplay.Loop.DemoGameplayOverrideSnapshot(PlayerInvincible);
            }

            public DemoGameplayOverrideStatus GetOverrideStatus()
            {
                return new DemoGameplayOverrideStatus(PlayerInvincible, _overrideMessage);
            }

            public void Publish()
            {
                PresentationChanged?.Invoke(Current);
            }
        }
    }
}
